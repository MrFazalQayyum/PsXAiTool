"""
One-time script: fetch 2 years of historical price data for all seeded companies.
Run from the backend/ directory:  python scripts/fetch_history.py
"""
import os
import sys
import time
import logging
from datetime import datetime, timedelta, timezone

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
logger = logging.getLogger(__name__)

from sqlalchemy.dialects.postgresql import insert as pg_insert
import pandas as pd

from app.core.config import settings
from app.core.database import SessionLocal, init_db
from app.models.market import Company, DailyPrice, MarketIndex
from app.scrapers.yahoo_finance import (
    fetch_stock_history,
    fetch_index_history,
    INDEX_TICKERS,
)

BATCH_SIZE = 5
PAUSE = 3


def save_prices(db, symbol: str, df: pd.DataFrame) -> int:
    if df.empty:
        return 0
    df = df.sort_index()
    closes = df["Close"].tolist()
    saved = 0
    for i, (ts, row) in enumerate(df.iterrows()):
        dt = ts.to_pydatetime()
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        change_pct = None
        if i > 0 and closes[i - 1]:
            change_pct = round((closes[i] - closes[i - 1]) / closes[i - 1] * 100, 4)
        stmt = (
            pg_insert(DailyPrice)
            .values(
                date=dt,
                symbol=symbol,
                open=float(row["Open"]) if pd.notna(row.get("Open")) else None,
                high=float(row["High"]) if pd.notna(row.get("High")) else None,
                low=float(row["Low"]) if pd.notna(row.get("Low")) else None,
                close=float(row["Close"]),
                volume=int(row["Volume"]) if pd.notna(row.get("Volume")) else None,
                change_pct=change_pct,
            )
            .on_conflict_do_nothing(constraint="uq_daily_price_date_symbol")
        )
        db.execute(stmt)
        saved += 1
    db.commit()
    return saved


def main():
    init_db()
    db = SessionLocal()
    end_date = datetime.now(tz=timezone.utc)
    start_date = end_date - timedelta(days=settings.INITIAL_HISTORY_DAYS)

    try:
        logger.info("Fetching index history (%d days)...", settings.INITIAL_HISTORY_DAYS)
        for index_name in INDEX_TICKERS:
            df = fetch_index_history(index_name, start_date, end_date)
            if df.empty:
                logger.warning("No data for index %s", index_name)
                continue
            count = 0
            closes = df["Close"].tolist()
            for i, (ts, row) in enumerate(df.iterrows()):
                dt = ts.to_pydatetime()
                if dt.tzinfo is None:
                    dt = dt.replace(tzinfo=timezone.utc)
                change = None
                change_pct = None
                if i > 0 and closes[i - 1]:
                    change = round(float(closes[i]) - float(closes[i - 1]), 4)
                    change_pct = round(change / float(closes[i - 1]) * 100, 4)
                stmt = (
                    pg_insert(MarketIndex)
                    .values(
                        date=dt,
                        index_name=index_name,
                        value=float(row["Close"]),
                        change=change,
                        change_pct=change_pct,
                        volume=int(row["Volume"]) if pd.notna(row.get("Volume")) else None,
                    )
                    .on_conflict_do_nothing(constraint="uq_market_index_date_name")
                )
                db.execute(stmt)
                count += 1
            db.commit()
            logger.info("  %s — saved %d records", index_name, count)

        companies = db.query(Company).filter(Company.is_active == True).all()  # noqa: E712
        logger.info("Fetching history for %d companies...", len(companies))

        for i, company in enumerate(companies):
            if not company.yahoo_ticker:
                continue
            logger.info("  [%d/%d] %s (%s)", i + 1, len(companies), company.symbol, company.yahoo_ticker)
            df = fetch_stock_history(company.yahoo_ticker, start_date, end_date)
            saved = save_prices(db, company.symbol, df)
            logger.info("    saved %d records", saved)

            if (i + 1) % BATCH_SIZE == 0 and i + 1 < len(companies):
                time.sleep(PAUSE)

    finally:
        db.close()

    logger.info("Historical data load complete.")


if __name__ == "__main__":
    main()
