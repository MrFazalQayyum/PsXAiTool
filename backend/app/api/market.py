"""
Market data API endpoints for the dashboard.
"""
from datetime import datetime, timedelta, timezone
from typing import Optional

from fastapi import APIRouter, Depends, Query
from sqlalchemy import func, desc, cast, Date
from sqlalchemy.orm import Session

from app.core.database import get_db
from app.models.market import Company, DailyPrice, MarketIndex

router = APIRouter(prefix="/api/market", tags=["market"])

# Helper: cast a datetime column to date for comparison
def _date(col):
    return cast(col, Date)


def _latest_price_date(db: Session):
    """Return the most recent trading date (date-only) that has price data."""
    row = db.query(func.max(_date(DailyPrice.date))).scalar()
    return row  # a datetime.date object


# ─── Indices ──────────────────────────────────────────────────────────────────

@router.get("/indices")
def get_indices(db: Session = Depends(get_db)):
    subq = (
        db.query(
            MarketIndex.index_name,
            func.max(MarketIndex.date).label("max_date"),
        )
        .group_by(MarketIndex.index_name)
        .subquery()
    )
    rows = (
        db.query(MarketIndex)
        .join(
            subq,
            (MarketIndex.index_name == subq.c.index_name)
            & (MarketIndex.date == subq.c.max_date),
        )
        .all()
    )
    return {
        "indices": [
            {
                "name": r.index_name,
                "value": float(r.value) if r.value else None,
                "change": float(r.change) if r.change else None,
                "change_pct": float(r.change_pct) if r.change_pct else None,
                "volume": r.volume,
                "date": r.date.isoformat() if r.date else None,
            }
            for r in rows
        ]
    }


# ─── Top Movers ───────────────────────────────────────────────────────────────

@router.get("/top-movers")
def get_top_movers(limit: int = Query(5, ge=1, le=20), db: Session = Depends(get_db)):
    latest_date = _latest_price_date(db)
    if not latest_date:
        return {"gainers": [], "losers": [], "date": None}

    # For each stock get the record closest to market close on the latest date
    latest_subq = (
        db.query(
            DailyPrice.symbol,
            func.max(DailyPrice.date).label("max_dt"),
        )
        .filter(_date(DailyPrice.date) == latest_date)
        .group_by(DailyPrice.symbol)
        .subquery()
    )
    latest_prices = (
        db.query(DailyPrice, Company.name, Company.sector)
        .join(
            latest_subq,
            (DailyPrice.symbol == latest_subq.c.symbol)
            & (DailyPrice.date == latest_subq.c.max_dt),
        )
        .join(Company, DailyPrice.symbol == Company.symbol)
        .filter(DailyPrice.change_pct.isnot(None))
        .all()
    )

    def _fmt(row):
        price, name, sector = row
        return {
            "symbol": price.symbol,
            "name": name,
            "sector": sector,
            "close": float(price.close) if price.close else None,
            "change_pct": float(price.change_pct) if price.change_pct else None,
            "volume": price.volume,
        }

    gainers = sorted(latest_prices, key=lambda r: float(r[0].change_pct or 0), reverse=True)[:limit]
    losers = sorted(latest_prices, key=lambda r: float(r[0].change_pct or 0))[:limit]

    return {
        "gainers": [_fmt(r) for r in gainers],
        "losers": [_fmt(r) for r in losers],
        "date": str(latest_date),
    }


# ─── Sector Performance ───────────────────────────────────────────────────────

@router.get("/sectors")
def get_sectors(db: Session = Depends(get_db)):
    latest_date = _latest_price_date(db)
    if not latest_date:
        return {"sectors": [], "date": None}

    # Use one price row per symbol for the latest date
    latest_subq = (
        db.query(
            DailyPrice.symbol,
            func.max(DailyPrice.date).label("max_dt"),
        )
        .filter(_date(DailyPrice.date) == latest_date)
        .group_by(DailyPrice.symbol)
        .subquery()
    )
    rows = (
        db.query(
            Company.sector,
            func.avg(DailyPrice.change_pct).label("avg_change_pct"),
            func.count(DailyPrice.symbol).label("stock_count"),
        )
        .select_from(DailyPrice)
        .join(
            latest_subq,
            (DailyPrice.symbol == latest_subq.c.symbol)
            & (DailyPrice.date == latest_subq.c.max_dt),
        )
        .join(Company, DailyPrice.symbol == Company.symbol)
        .filter(DailyPrice.change_pct.isnot(None))
        .filter(Company.sector.isnot(None))
        .group_by(Company.sector)
        .order_by(desc("avg_change_pct"))
        .all()
    )

    return {
        "sectors": [
            {
                "sector": r.sector,
                "avg_change_pct": round(float(r.avg_change_pct), 4) if r.avg_change_pct else 0,
                "stock_count": r.stock_count,
            }
            for r in rows
        ],
        "date": str(latest_date),
    }


# ─── Stocks ───────────────────────────────────────────────────────────────────

@router.get("/stocks")
def list_stocks(sector: Optional[str] = None, db: Session = Depends(get_db)):
    latest_date = _latest_price_date(db)

    companies = db.query(Company).filter(Company.is_active == True)  # noqa: E712
    if sector:
        companies = companies.filter(Company.sector == sector)
    companies = companies.order_by(Company.sector, Company.symbol).all()

    # Build a map of symbol -> latest price for the latest trading day
    price_map: dict = {}
    if latest_date:
        latest_subq = (
            db.query(
                DailyPrice.symbol,
                func.max(DailyPrice.date).label("max_dt"),
            )
            .filter(_date(DailyPrice.date) == latest_date)
            .group_by(DailyPrice.symbol)
            .subquery()
        )
        price_rows = (
            db.query(DailyPrice)
            .join(
                latest_subq,
                (DailyPrice.symbol == latest_subq.c.symbol)
                & (DailyPrice.date == latest_subq.c.max_dt),
            )
            .all()
        )
        price_map = {p.symbol: p for p in price_rows}

    return {
        "stocks": [
            {
                "symbol": c.symbol,
                "name": c.name,
                "sector": c.sector,
                "close": float(price_map[c.symbol].close) if c.symbol in price_map and price_map[c.symbol].close else None,
                "change_pct": float(price_map[c.symbol].change_pct) if c.symbol in price_map and price_map[c.symbol].change_pct else None,
                "volume": price_map[c.symbol].volume if c.symbol in price_map else None,
            }
            for c in companies
        ]
    }


@router.get("/stocks/{symbol}/prices")
def get_stock_prices(
    symbol: str,
    days: int = Query(30, ge=1, le=730),
    db: Session = Depends(get_db),
):
    since = datetime.now(tz=timezone.utc) - timedelta(days=days)
    company = db.query(Company).filter(Company.symbol == symbol.upper()).first()
    if not company:
        return {"error": "Stock not found", "symbol": symbol}

    prices = (
        db.query(DailyPrice)
        .filter(DailyPrice.symbol == symbol.upper())
        .filter(DailyPrice.date >= since)
        .order_by(DailyPrice.date)
        .all()
    )

    return {
        "symbol": company.symbol,
        "name": company.name,
        "sector": company.sector,
        "prices": [
            {
                "date": p.date.strftime("%Y-%m-%d"),
                "open": float(p.open) if p.open else None,
                "high": float(p.high) if p.high else None,
                "low": float(p.low) if p.low else None,
                "close": float(p.close) if p.close else None,
                "volume": p.volume,
                "change_pct": float(p.change_pct) if p.change_pct else None,
            }
            for p in prices
        ],
    }


# ─── Admin: manual price fetch ─────────────────────────────────────────────

@router.post("/admin/fetch-prices")
def trigger_price_fetch():
    """Manually trigger a price fetch (fires Celery task)."""
    from app.tasks.price_tasks import fetch_all_prices
    task = fetch_all_prices.apply_async()
    return {"task_id": task.id, "status": "queued"}
