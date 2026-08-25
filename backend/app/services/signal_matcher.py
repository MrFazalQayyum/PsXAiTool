import json
import logging
from pathlib import Path
from typing import Optional

logger = logging.getLogger(__name__)

_PATTERNS: Optional[dict] = None


def _load_patterns() -> dict:
    global _PATTERNS
    if _PATTERNS is None:
        path = Path(__file__).parent.parent.parent.parent / "data" / "signal_patterns.json"
        try:
            with open(path, encoding="utf-8") as f:
                _PATTERNS = json.load(f)
        except FileNotFoundError:
            logger.warning(f"signal_patterns.json not found at {path}")
            _PATTERNS = {}
    return _PATTERNS


def get_pattern(signal_type: str) -> Optional[dict]:
    return _load_patterns().get(signal_type)


def enrich_signal(signal_data: dict) -> dict:
    """
    Merge known pattern data into a Claude-generated signal.
    Fills in tickers and sectors if Claude left them empty.
    """
    signal_type = signal_data.get("signal_type", "")
    pattern = get_pattern(signal_type)
    if not pattern:
        return signal_data

    direction = signal_data.get("direction", "neutral")

    if not signal_data.get("entities"):
        if direction == "bullish":
            signal_data["entities"] = pattern.get("bullish_tickers", [])
        elif direction == "bearish":
            signal_data["entities"] = pattern.get("bearish_tickers", [])

    if not signal_data.get("sectors"):
        if direction == "bullish":
            signal_data["sectors"] = pattern.get("bullish_sectors", [])
        elif direction == "bearish":
            signal_data["sectors"] = pattern.get("bearish_sectors", [])

    if not signal_data.get("historical_note") and pattern.get("historical_note"):
        signal_data["historical_note"] = pattern["historical_note"]

    return signal_data
