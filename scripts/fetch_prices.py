#!/usr/bin/env python3
"""
Fetches PSX daily prices from stooq.com and stores them in Railway PostgreSQL.
Run daily after market close (PSX closes ~3:30 PM PKT = 10:30 AM UTC).
"""

import os
import time
import requests
import psycopg2
from datetime import datetime, timedelta, date
from urllib.parse import urlparse

DATABASE_URL = os.environ["DATABASE_URL"]
DAYS = 100  # how many days of history to fetch per stock


def get_connection():
    u = urlparse(DATABASE_URL)
    return psycopg2.connect(
        host=u.hostname,
        port=u.port,
        database=u.path[1:],
        user=u.username,
        password=u.password,
        sslmode="require",
    )


HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
    "Accept": "text/html,application/xhtml+xml,*/*",
    "Accept-Language": "en-US,en;q=0.9",
}


def fetch_stooq(ticker: str) -> list[tuple]:
    url = f"https://stooq.com/q/d/l/?s={ticker}&i=d"
    try:
        r = requests.get(url, timeout=20, headers=HEADERS)
        r.raise_for_status()
        text = r.text.strip()
        lines = text.split("\n")
        if len(lines) < 2 or "No data" in text[:50] or "<html" in text[:50].lower():
            print(f"  stooq {ticker}: unexpected response: {text[:120]!r}")
            return []
        return _parse_stooq_csv(lines)
    except Exception as e:
        print(f"  stooq error for {ticker}: {e}")
        return []


def fetch_yahoo(yahoo_ticker: str, psx_symbol: str) -> list[tuple]:
    import time as _time
    period2 = int(_time.time())
    period1 = period2 - DAYS * 86400
    url = (f"https://query1.finance.yahoo.com/v8/finance/chart/{requests.utils.quote(yahoo_ticker)}"
           f"?interval=1d&period1={period1}&period2={period2}")
    try:
        r = requests.get(url, timeout=20, headers=HEADERS)
        r.raise_for_status()
        data = r.json()
        result = data["chart"]["result"][0]
        timestamps = result["timestamp"]
        q = result["indicators"]["quote"][0]
        opens, highs, lows, closes, volumes = q.get("open"), q.get("high"), q.get("low"), q.get("close"), q.get("volume")

        rows = []
        for i, ts in enumerate(timestamps):
            c = closes[i] if closes else None
            if c is None:
                continue
            d = datetime.utcfromtimestamp(ts).date()
            rows.append((d, opens[i] or c, highs[i] or c, lows[i] or c, c, volumes[i] or 0))
        return rows
    except Exception as e:
        print(f"  yahoo error for {yahoo_ticker}: {e}")
        return []


def _parse_stooq_csv(lines: list[str]) -> list[tuple]:
    cutoff = datetime.utcnow().date() - timedelta(days=DAYS)
    rows = []
    for line in lines[1:]:
        cols = line.strip().split(",")
        if len(cols) < 5:
            continue
        try:
            d = datetime.strptime(cols[0].strip(), "%Y-%m-%d").date()
            if d < cutoff:
                continue
            open_ = float(cols[1])
            high  = float(cols[2])
            low   = float(cols[3])
            close = float(cols[4])
            vol   = int(cols[5].strip()) if len(cols) > 5 and cols[5].strip() else 0
            rows.append((d, open_, high, low, close, vol))
        except (ValueError, IndexError):
            continue
    rows.reverse()
    return rows


def main():
    conn = get_connection()
    cur = conn.cursor()

    # Load active companies (symbol + yahoo ticker for fallback)
    cur.execute('SELECT "Symbol", "YahooTicker" FROM "Companies" WHERE "IsActive" = true')
    companies = cur.fetchall()  # [(symbol, yahoo_ticker), ...]
    symbols = [r[0] for r in companies]
    print(f"Fetching prices for {len(symbols)} companies...")

    # Load existing (symbol, date) pairs to avoid duplicate inserts
    cutoff_date = datetime.utcnow().date() - timedelta(days=DAYS + 10)
    cur.execute(
        'SELECT "Symbol", "Date" FROM "DailyPrices" WHERE "Date" >= %s',
        (cutoff_date,),
    )
    existing = {(r[0], r[1]) for r in cur.fetchall()}
    print(f"Existing records in DB: {len(existing)}")

    saved = 0
    failed = 0

    for symbol, yahoo_ticker in companies:
        ticker = f"{symbol.lower()}.pk"
        rows = fetch_stooq(ticker)

        if not rows:
            # Fallback to Yahoo Finance
            rows = fetch_yahoo(yahoo_ticker, symbol)
            if rows:
                print(f"  {symbol}: stooq empty, got {len(rows)} rows from Yahoo")
            else:
                print(f"  {symbol}: no data from stooq or Yahoo")
                failed += 1
                time.sleep(0.3)
                continue

        for i, (d, open_, high, low, close, vol) in enumerate(rows):
            if (symbol, d) in existing:
                continue

            change_pct = None
            if i > 0:
                prev_close = rows[i - 1][4]
                if prev_close != 0:
                    change_pct = round((close - prev_close) / prev_close * 100, 4)

            cur.execute(
                """
                INSERT INTO "DailyPrices"
                    ("Symbol", "Date", "Open", "High", "Low", "Close", "Volume", "ChangePct")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT DO NOTHING
                """,
                (symbol, d, open_, high, low, close, vol, change_pct),
            )
            existing.add((symbol, d))
            saved += 1

        print(f"  {symbol}: {len(rows)} fetched")
        time.sleep(0.5)  # polite delay between stooq requests

    # KSE-100 index
    print("Fetching KSE-100 index...")
    kse_rows = fetch_stooq("^kse100.pk")
    if not kse_rows:
        kse_rows = fetch_stooq("kse100.pk")

    idx_saved = 0
    for d, open_, high, low, close, vol in kse_rows:
        cur.execute(
            """
            INSERT INTO "MarketIndices" ("Date", "IndexName", "Value", "Change", "ChangePct", "CreatedAt")
            SELECT %s, 'KSE-100', %s, NULL, NULL, NOW()
            WHERE NOT EXISTS (
                SELECT 1 FROM "MarketIndices"
                WHERE "Date" = %s AND "IndexName" = 'KSE-100'
            )
            """,
            (d, close, d),
        )
        if cur.rowcount:
            idx_saved += 1

    conn.commit()
    cur.close()
    conn.close()

    print(f"\nDone — {saved} new price records, {idx_saved} new index records.")
    print(f"Companies with no data: {failed}/{len(symbols)}")


if __name__ == "__main__":
    main()
