"""
Scraper for PSX DPS company announcements (dps.psx.com.pk).
Uses cloudscraper to bypass Cloudflare, then POSTs to /announcements.

Column structure for type='C' (Companies):
  0: Date | 1: Time | 2: Symbol | 3: Company name | 4: Announcement title | 5: PDF/image links
"""
import logging
from datetime import date, timedelta
from typing import Optional

import cloudscraper
from bs4 import BeautifulSoup

logger = logging.getLogger(__name__)

BASE_URL = "https://dps.psx.com.pk/announcements"

# Announcement titles that indicate signal-worthy corporate events
SIGNAL_KEYWORDS = [
    "dividend", "bonus", "right", "earning", "profit", "loss", "revenue",
    "quarterly result", "annual result", "financial result", "eps",
    "acquisition", "merger", "agreement", "contract", "default",
    "suspension", "delisting", "book closure", "agm", "egm",
    "capacity", "expansion", "material information", "board meeting",
    "corporate briefing", "production", "sales", "revenue", "takeover",
]

_scraper = None


def _get_scraper():
    global _scraper
    if _scraper is None:
        _scraper = cloudscraper.create_scraper(
            browser={"browser": "chrome", "platform": "windows", "mobile": False}
        )
    return _scraper


def _post_announcements(
    type_val: str = "C",
    symbol: str = "",
    keyword: str = "",
    date_from: str = "",
    date_to: str = "",
    count: int = 50,
    offset: int = 0,
) -> Optional[str]:
    data = {
        "type": type_val,
        "symbol": symbol,
        "query": keyword,
        "count": count,
        "offset": offset,
        "date_from": date_from,
        "date_to": date_to,
        "page": "annc",
    }
    headers = {
        "X-Requested-With": "XMLHttpRequest",
        "Referer": "https://dps.psx.com.pk/",
    }
    try:
        scraper = _get_scraper()
        resp = scraper.post(BASE_URL, data=data, headers=headers, timeout=25)
        if resp.status_code == 200:
            return resp.text
        logger.warning(f"PSX announcements POST returned {resp.status_code}")
    except Exception as e:
        logger.error(f"PSX announcements fetch failed: {e}")
    return None


def _parse_company_html(html: str) -> list[dict]:
    """
    Parse the company announcement table (type='C').
    Columns: Date | Time | Symbol | Company Name | Title | Links
    """
    soup = BeautifulSoup(html, "html.parser")
    announcements = []

    rows = soup.select("table tbody tr")
    for row in rows:
        cells = row.find_all("td")
        if len(cells) < 5:
            continue

        date_text = cells[0].get_text(strip=True)
        time_text = cells[1].get_text(strip=True)
        symbol = cells[2].get_text(strip=True)
        company = cells[3].get_text(strip=True)
        title = cells[4].get_text(strip=True)

        # Extract PDF link
        pdf_link = ""
        for a in row.find_all("a", href=True):
            href = a["href"]
            if "/download/document/" in href or ".pdf" in href.lower():
                pdf_link = f"https://dps.psx.com.pk{href}" if href.startswith("/") else href
                break

        announcements.append({
            "date": date_text,
            "time": time_text,
            "symbol": symbol,
            "company": company,
            "title": title,
            "pdf_url": pdf_link,
        })

    return announcements


def _is_relevant(ann: dict) -> bool:
    title_lower = ann.get("title", "").lower()
    return any(kw in title_lower for kw in SIGNAL_KEYWORDS)


def scrape_announcements(
    for_date: Optional[date] = None,
    days_back: int = 1,
    all_types: bool = False,
) -> list[dict]:
    """
    Fetch PSX company announcements for recent days.
    Returns list of dicts: symbol, company, title, date, time, pdf_url.
    Filter to signal-relevant titles only.
    """
    if for_date is None:
        for_date = date.today()

    date_from = (for_date - timedelta(days=days_back)).strftime("%Y-%m-%d")
    date_to = for_date.strftime("%Y-%m-%d")

    type_val = "A" if all_types else "C"
    html = _post_announcements(
        type_val=type_val,
        date_from=date_from,
        date_to=date_to,
        count=100,
        offset=0,
    )

    if not html:
        logger.warning("PSX announcements: no HTML returned")
        return []

    anns = _parse_company_html(html)
    logger.info(f"PSX announcements: {len(anns)} rows for {date_from}–{date_to}")

    relevant = [a for a in anns if _is_relevant(a)]
    logger.info(f"PSX announcements: {len(relevant)} signal-relevant")
    return relevant


def scrape_announcements_for_symbol(symbol: str, days_back: int = 7) -> list[dict]:
    """Fetch recent announcements for a specific PSX symbol."""
    today = date.today()
    date_from = (today - timedelta(days=days_back)).strftime("%Y-%m-%d")
    date_to = today.strftime("%Y-%m-%d")

    html = _post_announcements(
        type_val="C",
        symbol=symbol,
        date_from=date_from,
        date_to=date_to,
        count=50,
        offset=0,
    )

    if not html:
        return []

    return _parse_company_html(html)
