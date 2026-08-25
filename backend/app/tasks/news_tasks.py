import logging
from datetime import datetime, date, timedelta, timezone
from decimal import Decimal

from app.core.celery_app import celery_app
from app.core.database import SessionLocal
from app.scrapers.rss_scraper import scrape_rss_feeds
from app.scrapers.psx_announcements import scrape_announcements
from app.services.prefilter import is_relevant
from app.services.claude_processor import process_article, generate_daily_briefing
from app.services.signal_matcher import enrich_signal
from app.services.webpush_service import send_signal_push, send_briefing_push
from app.models.news import NewsArticle, Signal, SignalValidation, DailyBriefing
from app.models.market import DailyPrice

logger = logging.getLogger(__name__)


@celery_app.task(name="tasks.fetch_and_process_news")
def fetch_and_process_news():
    """Scrape RSS feeds → pre-filter → Claude Haiku → store signals → push notify."""
    db = SessionLocal()
    new_articles = 0
    new_signals = 0

    try:
        articles = scrape_rss_feeds()

        # Augment with PSX official company announcements
        for ann in scrape_announcements():
            sym = ann.get("symbol", "")
            company = ann.get("company", "")
            title = ann.get("title", "")
            if title:
                articles.append({
                    "source": "psx_announcements",
                    "url": ann.get("pdf_url") or f"https://dps.psx.com.pk/announcements",
                    "title": f"{sym} — {title}" if sym else title,
                    "description": (
                        f"{company} ({sym}): {title}. "
                        f"Date: {ann.get('date', '')} {ann.get('time', '')}."
                    ),
                    "published_at": None,
                })

        for art in articles:
            if not art.get("url"):
                continue

            if db.query(NewsArticle).filter(NewsArticle.url == art["url"]).first():
                continue

            text = f"{art.get('title', '')} {art.get('description', '')}"
            relevant = is_relevant(text)

            article_obj = NewsArticle(
                source=art["source"],
                url=art["url"],
                title=art["title"][:495],
                description=(art.get("description") or "")[:2000],
                published_at=art.get("published_at"),
                is_relevant=relevant,
            )
            db.add(article_obj)
            db.flush()
            new_articles += 1

            if not relevant:
                continue

            signal_data = process_article(
                title=art.get("title", ""),
                description=art.get("description", ""),
            )

            if signal_data:
                signal_data = enrich_signal(signal_data)
                new_entities = sorted(signal_data.get("entities") or [])
                new_type = signal_data.get("signal_type", "unknown")
                new_dir = signal_data.get("direction", "neutral")

                # Deduplication: skip if same signal_type + direction + overlapping
                # entities already exists in the last 8 hours
                window = datetime.now(timezone.utc) - timedelta(hours=8)
                recent = (
                    db.query(Signal)
                    .filter(
                        Signal.signal_type == new_type,
                        Signal.direction == new_dir,
                        Signal.created_at >= window,
                    )
                    .all()
                )
                is_dup = any(
                    bool(set(new_entities) & set(s.entities or []))
                    for s in recent
                )
                if is_dup:
                    logger.debug(f"Skipping duplicate signal: {new_type}/{new_dir} {new_entities}")
                    continue

                signal_obj = Signal(
                    article_id=article_obj.id,
                    signal_type=new_type,
                    entities=new_entities,
                    sectors=signal_data.get("sectors") or [],
                    direction=new_dir,
                    confidence=Decimal(str(round(signal_data.get("confidence") or 0, 3))),
                    summary=signal_data.get("summary", ""),
                    historical_note=signal_data.get("historical_note"),
                    raw_headline=art.get("title", ""),
                )
                db.add(signal_obj)
                db.flush()
                new_signals += 1
                article_obj.processed_at = datetime.now(timezone.utc)

                # Push notify for high-confidence signals
                if (signal_data.get("confidence") or 0) >= 0.65:
                    send_signal_push(signal_data)
                    signal_obj.is_notified = True

        db.commit()
        logger.info(f"News task: {new_articles} articles, {new_signals} new signals")
        return {"articles": new_articles, "signals": new_signals}

    except Exception as e:
        db.rollback()
        logger.error(f"News task failed: {e}", exc_info=True)
        raise
    finally:
        db.close()


@celery_app.task(name="tasks.validate_signals")
def validate_signals():
    """After PSX close: compare yesterday's signal predictions vs actual price moves."""
    db = SessionLocal()
    validated = 0

    try:
        yesterday_start = datetime.now(timezone.utc).replace(
            hour=0, minute=0, second=0, microsecond=0
        ) - timedelta(days=1)
        yesterday_end = yesterday_start + timedelta(days=1)

        signals = (
            db.query(Signal)
            .filter(
                Signal.created_at >= yesterday_start,
                Signal.created_at < yesterday_end,
            )
            .all()
        )

        for signal in signals:
            for ticker in (signal.entities or []):
                # Skip if already validated
                if db.query(SignalValidation).filter(
                    SignalValidation.signal_id == signal.id,
                    SignalValidation.symbol == ticker,
                ).first():
                    continue

                price = (
                    db.query(DailyPrice)
                    .filter(DailyPrice.symbol == ticker)
                    .order_by(DailyPrice.date.desc())
                    .first()
                )
                if not price or price.change_pct is None:
                    continue

                change = float(price.change_pct)
                predicted = signal.direction

                if change >= 0.5 and predicted == "bullish":
                    verdict = "CORRECT"
                elif change <= -0.5 and predicted == "bearish":
                    verdict = "CORRECT"
                elif abs(change) < 0.5:
                    verdict = "NEUTRAL"
                else:
                    verdict = "WRONG"

                db.add(SignalValidation(
                    signal_id=signal.id,
                    symbol=ticker,
                    predicted_direction=predicted,
                    actual_change_pct=Decimal(str(round(change, 4))),
                    verdict=verdict,
                ))
                validated += 1

        db.commit()
        logger.info(f"Validated {validated} signal-ticker pairs")
        return {"validated": validated}

    except Exception as e:
        db.rollback()
        logger.error(f"Signal validation failed: {e}", exc_info=True)
        raise
    finally:
        db.close()


@celery_app.task(name="tasks.generate_daily_briefing")
def generate_daily_briefing_task():
    """8 AM PKT: generate Claude Sonnet morning briefing and push to subscribers."""
    db = SessionLocal()

    try:
        today = datetime.now(timezone.utc).replace(hour=0, minute=0, second=0, microsecond=0)

        # Skip if already generated today
        if db.query(DailyBriefing).filter(DailyBriefing.briefing_date >= today).first():
            logger.info("Daily briefing already generated for today")
            return {"status": "already_done"}

        # Fetch last 24h signals
        since = today - timedelta(hours=24)
        signals = (
            db.query(Signal)
            .filter(Signal.created_at >= since)
            .order_by(Signal.confidence.desc())
            .limit(15)
            .all()
        )

        # Fetch accuracy stats
        from app.models.news import SignalValidation
        from sqlalchemy import func
        total_v = db.query(func.count(SignalValidation.id)).scalar() or 0
        correct_v = db.query(func.count(SignalValidation.id)).filter(
            SignalValidation.verdict == "CORRECT"
        ).scalar() or 0
        accuracy = round(correct_v / total_v * 100, 1) if total_v > 0 else None

        market_summary = (
            f"PSX signal tracker — {len(signals)} signals in last 24h. "
            f"System accuracy: {accuracy}% ({correct_v}/{total_v} validated)" if accuracy
            else f"PSX signal tracker — {len(signals)} signals in last 24h."
        )

        signal_dicts = [
            {
                "signal_type": s.signal_type,
                "summary": s.summary,
                "direction": s.direction,
                "confidence": float(s.confidence or 0),
                "entities": s.entities or [],
            }
            for s in signals
        ]

        briefing_text = generate_daily_briefing(market_summary, signal_dicts)

        briefing = DailyBriefing(
            briefing_date=today,
            content=briefing_text,
            signal_count=len(signals),
            accuracy_pct=Decimal(str(accuracy)) if accuracy else None,
        )
        db.add(briefing)
        db.flush()

        # Push to subscribers
        pushed = send_briefing_push(briefing_text)
        briefing.is_pushed = pushed > 0
        db.commit()

        logger.info(f"Daily briefing generated ({len(briefing_text)} chars), pushed to {pushed}")
        return {"status": "done", "pushed": pushed, "signals": len(signals)}

    except Exception as e:
        db.rollback()
        logger.error(f"Daily briefing failed: {e}", exc_info=True)
        raise
    finally:
        db.close()
