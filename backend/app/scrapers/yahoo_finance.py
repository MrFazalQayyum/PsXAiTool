"""
Yahoo Finance data fetcher for PSX stocks.
Uses direct HTTP calls with browser headers — yfinance gets bot-detected.
PSX stocks use the .KA suffix (e.g., OGDC.KA). KSE-100: ^KSE100.
"""
import logging
import time
from datetime import datetime, timedelta, timezone
from typing import Optional

import pandas as pd
import requests

logger = logging.getLogger(__name__)

# PSX index tickers on Yahoo Finance
INDEX_TICKERS = {
    "KSE-100": "^KSE100",
    "KSE-30":  "^KSE30",
}

BATCH_PAUSE_SECONDS = 3
BATCH_SIZE = 10
TIMEOUT = 20

_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/120.0.0.0 Safari/537.36"
    ),
    "Accept": "application/json, text/plain, */*",
    "Accept-Language": "en-US,en;q=0.9",
    "Referer": "https://finance.yahoo.com",
}

_session = requests.Session()
_session.headers.update(_HEADERS)


def _chart_url(ticker: str, period1: int, period2: int) -> str:
    return (
        f"https://query2.finance.yahoo.com/v8/finance/chart/{ticker}"
        f"?interval=1d&period1={period1}&period2={period2}&events=div,splits"
    )


def _fetch_chart(ticker: str, start: datetime, end: datetime) -> dict:
    p1 = int(start.timestamp())
    p2 = int(end.timestamp())
    try:
        r = _session.get(_chart_url(ticker, p1, p2), timeout=TIMEOUT)
        r.raise_for_status()
        data = r.json()
        results = data.get("chart", {}).get("result")
        if not results:
            err = data.get("chart", {}).get("error", {})
            logger.warning("No chart result for %s: %s", ticker, err)
            return {}
        return results[0]
    except Exception as exc:
        logger.error("Failed to fetch chart for %s: %s", ticker, exc)
        return {}


def _chart_to_df(result: dict) -> pd.DataFrame:
    """Convert Yahoo Finance chart result dict to a clean OHLCV DataFrame."""
    if not result:
        return pd.DataFrame()
    timestamps = result.get("timestamp", [])
    quotes = result.get("indicators", {}).get("quote", [{}])[0]
    if not timestamps or not quotes:
        return pd.DataFrame()

    df = pd.DataFrame(
        {
            "Open": quotes.get("open", [None] * len(timestamps)),
            "High": quotes.get("high", [None] * len(timestamps)),
            "Low": quotes.get("low", [None] * len(timestamps)),
            "Close": quotes.get("close", [None] * len(timestamps)),
            "Volume": quotes.get("volume", [None] * len(timestamps)),
        },
        index=pd.to_datetime(timestamps, unit="s", utc=True),
    )
    df.index.name = "Date"
    df = df.dropna(subset=["Close"])
    return df


def fetch_stock_history(
    yahoo_ticker: str,
    start_date: datetime,
    end_date: Optional[datetime] = None,
) -> pd.DataFrame:
    end = end_date or datetime.now(tz=timezone.utc)
    result = _fetch_chart(yahoo_ticker, start_date, end)
    return _chart_to_df(result)


def fetch_latest_prices(yahoo_tickers: list[str]) -> dict[str, dict]:
    results: dict[str, dict] = {}
    end = datetime.now(tz=timezone.utc)
    start = end - timedelta(days=10)

    for i, ticker in enumerate(yahoo_tickers):
        df = fetch_stock_history(ticker, start, end)
        if df.empty:
            continue
        today = df.iloc[-1]
        prev = df.iloc[-2] if len(df) >= 2 else None
        change_pct = None
        if prev is not None and prev["Close"]:
            change_pct = round(
                float((today["Close"] - prev["Close"]) / prev["Close"] * 100), 4
            )
        results[ticker] = {
            "date": df.index[-1].to_pydatetime(),
            "open": float(today["Open"]) if pd.notna(today.get("Open")) else None,
            "high": float(today["High"]) if pd.notna(today.get("High")) else None,
            "low": float(today["Low"]) if pd.notna(today.get("Low")) else None,
            "close": float(today["Close"]),
            "volume": int(today["Volume"]) if pd.notna(today.get("Volume")) else None,
            "change_pct": change_pct,
        }
        if (i + 1) % BATCH_SIZE == 0 and i + 1 < len(yahoo_tickers):
            time.sleep(BATCH_PAUSE_SECONDS)

    return results


def fetch_index_history(
    index_name: str,
    start_date: datetime,
    end_date: Optional[datetime] = None,
) -> pd.DataFrame:
    ticker_symbol = INDEX_TICKERS.get(index_name)
    if not ticker_symbol:
        logger.error("Unknown index: %s", index_name)
        return pd.DataFrame()
    return fetch_stock_history(ticker_symbol, start_date, end_date)


def fetch_latest_indices() -> dict[str, dict]:
    results: dict[str, dict] = {}
    for index_name, ticker_symbol in INDEX_TICKERS.items():
        end = datetime.now(tz=timezone.utc)
        start = end - timedelta(days=10)
        df = fetch_stock_history(ticker_symbol, start, end)
        if df.empty:
            continue
        today = df.iloc[-1]
        prev = df.iloc[-2] if len(df) >= 2 else None
        change = None
        change_pct = None
        if prev is not None and float(prev["Close"]):
            change = round(float(today["Close"]) - float(prev["Close"]), 2)
            change_pct = round(change / float(prev["Close"]) * 100, 4)
        results[index_name] = {
            "date": df.index[-1].to_pydatetime(),
            "value": float(today["Close"]),
            "change": change,
            "change_pct": change_pct,
            "volume": int(today["Volume"]) if pd.notna(today.get("Volume")) else None,
        }
    return results
