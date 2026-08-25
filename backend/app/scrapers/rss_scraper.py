import logging
import re
from datetime import datetime, timezone

import feedparser
import requests

logger = logging.getLogger(__name__)

# Only sources confirmed to return articles
RSS_SOURCES = {
    "brecorder": "https://www.brecorder.com/feeds/latest-news",
    "propakistani": "https://propakistani.pk/feed/",
    "arynews": "https://arynews.tv/feed/",
}

_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    ),
    "Accept": "application/rss+xml, application/xml, text/xml, */*",
}

_HTML_RE = re.compile(r"<[^>]+>")


def _strip_html(text: str) -> str:
    return _HTML_RE.sub(" ", text).strip() if text else ""


def _parse_date(entry) -> datetime:
    try:
        if hasattr(entry, "published_parsed") and entry.published_parsed:
            t = entry.published_parsed
            return datetime(t[0], t[1], t[2], t[3], t[4], t[5], tzinfo=timezone.utc)
        if hasattr(entry, "published") and entry.published:
            from email.utils import parsedate_to_datetime
            return parsedate_to_datetime(entry.published)
    except Exception:
        pass
    return datetime.now(timezone.utc)


def _fetch_entries(url: str) -> list:
    # Try feedparser directly first
    try:
        feed = feedparser.parse(url, request_headers=_HEADERS)
        if feed.entries:
            return feed.entries
    except Exception:
        pass

    # Fall back to raw HTTP → feedparser
    try:
        resp = requests.get(url, headers=_HEADERS, timeout=15)
        resp.raise_for_status()
        feed = feedparser.parse(resp.content)
        return feed.entries if feed.entries else []
    except Exception as e:
        logger.warning(f"HTTP fetch failed for {url}: {e}")
        return []


def scrape_rss_feeds() -> list[dict]:
    """Scrape confirmed working RSS feeds. Returns list of article dicts."""
    articles: list[dict] = []

    for source_name, url in RSS_SOURCES.items():
        entries = _fetch_entries(url)
        count = 0

        for entry in entries[:30]:
            link = (entry.get("link") or "").strip()
            title = _strip_html(entry.get("title") or "").strip()
            if not link or not title:
                continue

            description = _strip_html(
                entry.get("summary") or entry.get("description") or ""
            )[:800]

            articles.append({
                "source": source_name,
                "url": link,
                "title": title,
                "description": description,
                "published_at": _parse_date(entry),
            })
            count += 1

        logger.info(f"RSS {source_name}: {count} articles (from {url})")

    logger.info(f"RSS scrape total: {len(articles)} articles")
    return articles
