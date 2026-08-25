"""
Celery tasks for scheduled price fetching and data maintenance.
"""
import logging
from datetime import datetime, timezone

from app.core.celery_app import celery_app
from sqlalchemy.dialects.postgresql import insert

from app.core.database import SessionLocal
from app.models.market import Company, DailyPrice, MarketIndex
from app.scrapers.yahoo_finance import fetch_latest_prices, fetch_latest_indices

logger = logging.getLogger(__name__)


@celery_app.task(name="tasks.fetch_all_prices", bind=True, max_retries=3)
def fetch_all_prices(self):
    """
    Daily task: fetch latest closing prices for all active companies
    and update market index values.
    Runs after PSX market close (scheduled via Celery Beat).
    """
    logger.info("Starting daily price fetch")
    db = SessionLocal()
    try:
        companies = db.query(Company).filter(Company.is_active == True).all()  # noqa: E712
        tickers = [c.yahoo_ticker for c in companies if c.yahoo_ticker]
        symbol_map = {c.yahoo_ticker: c.symbol for c in companies if c.yahoo_ticker}

        logger.info("Fetching prices for %d companies", len(tickers))
        price_data = fetch_latest_prices(tickers)

        saved = 0
        for yahoo_ticker, data in price_data.items():
            symbol = symbol_map.get(yahoo_ticker)
            if not symbol:
                continue
            stmt = (
                insert(DailyPrice)
                .values(
                    date=data["date"],
                    symbol=symbol,
                    open=data.get("open"),
                    high=data.get("high"),
                    low=data.get("low"),
                    close=data["close"],
                    volume=data.get("volume"),
                    change_pct=data.get("change_pct"),
                )
                .on_conflict_do_nothing(
                    constraint="uq_daily_price_date_symbol"
                )
            )
            db.execute(stmt)
            saved += 1

        # Fetch and save index values
        index_data = fetch_latest_indices()
        for index_name, data in index_data.items():
            stmt = (
                insert(MarketIndex)
                .values(
                    date=data["date"],
                    index_name=index_name,
                    value=data["value"],
                    change=data.get("change"),
                    change_pct=data.get("change_pct"),
                    volume=data.get("volume"),
                )
                .on_conflict_do_nothing(
                    constraint="uq_market_index_date_name"
                )
            )
            db.execute(stmt)

        db.commit()
        logger.info("Daily price fetch complete — saved %d price records", saved)
        return {"saved": saved, "indices": list(index_data.keys())}

    except Exception as exc:
        db.rollback()
        logger.error("Price fetch failed: %s", exc)
        raise self.retry(exc=exc, countdown=300)
    finally:
        db.close()


@celery_app.task(name="tasks.fetch_prices_now")
def fetch_prices_now():
    """Manual trigger — same as the daily task, callable from admin panel."""
    return fetch_all_prices.apply()
