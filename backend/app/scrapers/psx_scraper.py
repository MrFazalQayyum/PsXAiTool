"""
PSX website scraper for supplementary market data.
Used to get real-time sector summaries and announcement data.
Falls back gracefully when PSX site is unreachable.
"""
import logging
from typing import Optional

import requests
from bs4 import BeautifulSoup

logger = logging.getLogger(__name__)

PSX_BASE = "https://www.psx.com.pk"
HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/120.0.0.0 Safari/537.36"
    ),
    "Accept-Language": "en-US,en;q=0.9",
}
TIMEOUT = 15


def _get(url: str) -> Optional[BeautifulSoup]:
    try:
        resp = requests.get(url, headers=HEADERS, timeout=TIMEOUT)
        resp.raise_for_status()
        return BeautifulSoup(resp.text, "lxml")
    except Exception as exc:
        logger.warning("PSX scrape failed for %s: %s", url, exc)
        return None


def fetch_market_summary() -> dict:
    """
    Scrape the PSX market summary page for index values and broad stats.
    Returns a dict with whatever could be parsed; empty dict on failure.
    """
    soup = _get(f"{PSX_BASE}/market-summary/")
    if not soup:
        return {}

    result: dict = {}
    try:
        # PSX renders index data in table rows — structure may change
        rows = soup.select("table tr")
        for row in rows:
            cells = row.find_all("td")
            if len(cells) >= 3:
                label = cells[0].get_text(strip=True)
                value = cells[1].get_text(strip=True).replace(",", "")
                change = cells[2].get_text(strip=True).replace(",", "")
                if label and value:
                    result[label] = {"value": value, "change": change}
    except Exception as exc:
        logger.warning("PSX market summary parse error: %s", exc)

    return result


def fetch_company_announcements(limit: int = 20) -> list[dict]:
    """
    Scrape the PSX notice board for latest company announcements.
    Returns list of {symbol, title, date, url}.
    """
    soup = _get(f"{PSX_BASE}/announcements/")
    if not soup:
        return []

    items = []
    try:
        rows = soup.select("table.table tbody tr")
        for row in rows[:limit]:
            cells = row.find_all("td")
            if len(cells) >= 3:
                items.append(
                    {
                        "symbol": cells[0].get_text(strip=True),
                        "title": cells[1].get_text(strip=True),
                        "date": cells[2].get_text(strip=True),
                        "url": PSX_BASE + (cells[1].find("a") or {}).get("href", ""),
                    }
                )
    except Exception as exc:
        logger.warning("PSX announcement parse error: %s", exc)

    return items
