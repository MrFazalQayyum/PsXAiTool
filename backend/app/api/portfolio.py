from decimal import Decimal
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session
from sqlalchemy import desc, cast, Text

from app.core.database import get_db
from app.models.market import Portfolio, DailyPrice, Company
from app.models.news import Signal

router = APIRouter(prefix="/api/portfolio", tags=["portfolio"])


class HoldingIn(BaseModel):
    symbol: str = Field(..., min_length=1, max_length=20)
    shares_held: float = Field(..., gt=0)
    avg_buy_price: float = Field(..., gt=0)
    notes: str = ""


class HoldingUpdate(BaseModel):
    shares_held: float = Field(..., gt=0)
    avg_buy_price: float = Field(..., gt=0)
    notes: str = ""


def _latest_price(db: Session, symbol: str) -> float | None:
    row = (
        db.query(DailyPrice)
        .filter(DailyPrice.symbol == symbol.upper())
        .order_by(desc(DailyPrice.date))
        .first()
    )
    return float(row.close) if row and row.close else None


def _holding_dict(h: Portfolio, db: Session) -> dict:
    current_price = _latest_price(db, h.symbol)
    shares = float(h.shares_held)
    avg = float(h.avg_buy_price)
    cost_basis = shares * avg

    if current_price is not None:
        current_value = shares * current_price
        pnl = current_value - cost_basis
        pnl_pct = (pnl / cost_basis * 100) if cost_basis else 0
    else:
        current_value = None
        pnl = None
        pnl_pct = None

    # Recent signals for this ticker
    recent_signals = (
        db.query(Signal)
        .filter(Signal.entities.contains([h.symbol.upper()]))
        .order_by(desc(Signal.created_at))
        .limit(3)
        .all()
    )
    signals = [
        {
            "direction": s.direction,
            "confidence": float(s.confidence or 0),
            "summary": s.summary,
            "signal_type": s.signal_type,
            "created_at": s.created_at.isoformat() if s.created_at else None,
        }
        for s in recent_signals
    ]

    return {
        "id": h.id,
        "symbol": h.symbol,
        "shares_held": shares,
        "avg_buy_price": avg,
        "cost_basis": round(cost_basis, 2),
        "current_price": current_price,
        "current_value": round(current_value, 2) if current_value is not None else None,
        "pnl": round(pnl, 2) if pnl is not None else None,
        "pnl_pct": round(pnl_pct, 2) if pnl_pct is not None else None,
        "notes": h.notes or "",
        "signals": signals,
        "updated_at": h.updated_at.isoformat() if h.updated_at else None,
    }


@router.get("")
def list_holdings(db: Session = Depends(get_db)):
    holdings = db.query(Portfolio).order_by(Portfolio.symbol).all()
    rows = [_holding_dict(h, db) for h in holdings]

    total_cost = sum(r["cost_basis"] for r in rows)
    total_value = sum(r["current_value"] for r in rows if r["current_value"] is not None)
    total_pnl = total_value - total_cost if total_value else None
    total_pnl_pct = (total_pnl / total_cost * 100) if (total_pnl is not None and total_cost) else None

    return {
        "holdings": rows,
        "summary": {
            "total_cost": round(total_cost, 2),
            "total_value": round(total_value, 2) if total_value else None,
            "total_pnl": round(total_pnl, 2) if total_pnl is not None else None,
            "total_pnl_pct": round(total_pnl_pct, 2) if total_pnl_pct is not None else None,
            "count": len(rows),
        },
    }


@router.post("", status_code=201)
def add_holding(body: HoldingIn, db: Session = Depends(get_db)):
    symbol = body.symbol.upper()
    if db.query(Portfolio).filter(Portfolio.symbol == symbol).first():
        raise HTTPException(400, f"{symbol} already in portfolio — use PUT to update")

    h = Portfolio(
        symbol=symbol,
        shares_held=Decimal(str(body.shares_held)),
        avg_buy_price=Decimal(str(body.avg_buy_price)),
        notes=body.notes,
    )
    db.add(h)
    db.commit()
    db.refresh(h)
    return _holding_dict(h, db)


@router.put("/{symbol}")
def update_holding(symbol: str, body: HoldingUpdate, db: Session = Depends(get_db)):
    h = db.query(Portfolio).filter(Portfolio.symbol == symbol.upper()).first()
    if not h:
        raise HTTPException(404, f"{symbol} not in portfolio")

    h.shares_held = Decimal(str(body.shares_held))
    h.avg_buy_price = Decimal(str(body.avg_buy_price))
    h.notes = body.notes
    db.commit()
    db.refresh(h)
    return _holding_dict(h, db)


@router.delete("/{symbol}", status_code=204)
def remove_holding(symbol: str, db: Session = Depends(get_db)):
    h = db.query(Portfolio).filter(Portfolio.symbol == symbol.upper()).first()
    if not h:
        raise HTTPException(404, f"{symbol} not in portfolio")
    db.delete(h)
    db.commit()
