from datetime import datetime, timedelta
from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session
from sqlalchemy import desc, func
from typing import Optional

from app.core.database import get_db
from app.models.news import Signal, NewsArticle, SignalValidation

router = APIRouter(prefix="/api/signals", tags=["signals"])


def _to_dict(signal: Signal, article: Optional[NewsArticle] = None) -> dict:
    return {
        "id": signal.id,
        "signal_type": signal.signal_type,
        "direction": signal.direction,
        "confidence": float(signal.confidence) if signal.confidence is not None else 0.0,
        "entities": signal.entities or [],
        "sectors": signal.sectors or [],
        "summary": signal.summary or "",
        "historical_note": signal.historical_note,
        "raw_headline": signal.raw_headline,
        "is_notified": signal.is_notified,
        "created_at": signal.created_at.isoformat() if signal.created_at else None,
        "source": article.source if article else None,
        "source_url": article.url if article else None,
    }


@router.get("")
def list_signals(
    limit: int = Query(50, ge=1, le=200),
    direction: Optional[str] = Query(None, regex="^(bullish|bearish|neutral)$"),
    min_confidence: float = Query(0.0, ge=0.0, le=1.0),
    signal_type: Optional[str] = None,
    db: Session = Depends(get_db),
):
    q = db.query(Signal).order_by(desc(Signal.created_at))

    if direction:
        q = q.filter(Signal.direction == direction)
    if min_confidence > 0:
        q = q.filter(Signal.confidence >= min_confidence)
    if signal_type:
        q = q.filter(Signal.signal_type == signal_type)

    signals = q.limit(limit).all()

    article_ids = [s.article_id for s in signals if s.article_id]
    articles = {}
    if article_ids:
        articles = {
            a.id: a
            for a in db.query(NewsArticle).filter(NewsArticle.id.in_(article_ids)).all()
        }

    return {
        "signals": [_to_dict(s, articles.get(s.article_id)) for s in signals],
        "count": len(signals),
    }


@router.get("/conflicts")
def signal_conflicts(hours: int = Query(24, ge=1, le=168), db: Session = Depends(get_db)):
    """Return entities that have BOTH bullish and bearish signals in the given window."""
    from datetime import timezone
    window = datetime.now(timezone.utc) - timedelta(hours=hours)
    recent = db.query(Signal).filter(Signal.created_at >= window).all()

    bullish_entities: set = set()
    bearish_entities: set = set()
    for s in recent:
        for e in (s.entities or []):
            if s.direction == "bullish":
                bullish_entities.add(e)
            elif s.direction == "bearish":
                bearish_entities.add(e)

    conflicted = sorted(bullish_entities & bearish_entities)
    return {"conflicted_entities": conflicted, "count": len(conflicted)}


@router.get("/stats")
def signal_stats(db: Session = Depends(get_db)):
    total = db.query(func.count(Signal.id)).scalar() or 0
    bullish = db.query(func.count(Signal.id)).filter(Signal.direction == "bullish").scalar() or 0
    bearish = db.query(func.count(Signal.id)).filter(Signal.direction == "bearish").scalar() or 0

    total_articles = db.query(func.count(NewsArticle.id)).scalar() or 0
    relevant_articles = db.query(func.count(NewsArticle.id)).filter(NewsArticle.is_relevant).scalar() or 0

    correct = db.query(func.count(SignalValidation.id)).filter(SignalValidation.verdict == "CORRECT").scalar() or 0
    wrong = db.query(func.count(SignalValidation.id)).filter(SignalValidation.verdict == "WRONG").scalar() or 0
    validated = correct + wrong
    accuracy = round(correct / validated * 100, 1) if validated > 0 else None

    return {
        "total_signals": total,
        "bullish": bullish,
        "bearish": bearish,
        "total_articles": total_articles,
        "relevant_articles": relevant_articles,
        "filter_rate_pct": round((1 - relevant_articles / total_articles) * 100, 1) if total_articles > 0 else None,
        "validated": validated,
        "correct": correct,
        "wrong": wrong,
        "accuracy_pct": accuracy,
    }


@router.post("/trigger")
def trigger_news_fetch():
    from app.tasks.news_tasks import fetch_and_process_news
    fetch_and_process_news.apply_async()
    return {"status": "queued", "message": "News fetch queued — check back in 60 seconds"}


@router.post("/briefing/trigger")
def trigger_briefing():
    from app.tasks.news_tasks import generate_daily_briefing_task
    generate_daily_briefing_task.apply_async()
    return {"status": "queued", "message": "Briefing generation queued"}


@router.post("/validate/trigger")
def trigger_validation():
    from app.tasks.news_tasks import validate_signals
    validate_signals.apply_async()
    return {"status": "queued", "message": "Validation queued"}


@router.get("/briefings")
def list_briefings(limit: int = Query(7, le=30), db: Session = Depends(get_db)):
    from app.models.news import DailyBriefing
    briefings = (
        db.query(DailyBriefing)
        .order_by(desc(DailyBriefing.briefing_date))
        .limit(limit)
        .all()
    )
    return {
        "briefings": [
            {
                "id": b.id,
                "briefing_date": b.briefing_date.isoformat(),
                "content": b.content,
                "signal_count": b.signal_count,
                "accuracy_pct": float(b.accuracy_pct) if b.accuracy_pct else None,
                "is_pushed": b.is_pushed,
            }
            for b in briefings
        ]
    }
