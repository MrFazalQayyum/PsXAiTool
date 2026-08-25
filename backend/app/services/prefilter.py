import re
from typing import FrozenSet

# STRONG keywords — any single match makes an article relevant
_STRONG: FrozenSet[str] = frozenset({
    # PSX tickers
    "ogdc", "ogdcl", "ppl", "mari", "pol", "pso", "atrl", "hubco", "ncpl",
    "kapco", "kel", "luck", "dgkc", "mlcf", "acpl", "kohc", "pioc", "fccl",
    "chcc", "hbl", "ubl", "mcb", "abl", "bafl", "bahl", "nbp", "mebl", "jsbl",
    "nml", "engro", "ffc", "ffbl", "fatima", "trg", "sys", "netsol", "avn",
    "searl", "glaxo", "agtl", "nestle", "colg", "ici", "astl", "isl", "mughal",
    "jdw", "haseeb", "byco", "ulever", "piac",
    # Market & institutions
    "psx", "kse-100", "kse100", "kse 100", "karachi stock exchange",
    "sbp", "state bank of pakistan",
    "secp", "ogra", "nepra",
    "imf", "world bank",
    # Macro events
    "policy rate", "monetary policy", "mpc meeting",
    "pkr", "rupee depreciation", "rupee appreciation",
    "circular debt", "t-bill", "treasury bill",
    "cpec", "psdp",
    # Specific companies
    "lucky cement", "fauji fertilizer", "engro fertilizer",
    "habib bank", "united bank", "mcb bank", "allied bank",
    "bank alfalah", "bank al habib", "national bank", "meezan bank",
    "hub power", "pakistan state oil", "trg pakistan", "systems limited",
    "netsol technologies",
})

# WEAK keywords — need at least 2 different weak matches, OR 1 strong + 1 weak
_WEAK: FrozenSet[str] = frozenset({
    "cement", "fertilizer", "textile", "pharma", "pharmaceutical",
    "exploration", "refinery", "power sector",
    "stock exchange", "stock market", "equity", "shares",
    "interest rate", "inflation", "gdp", "current account",
    "forex", "foreign exchange", "foreign reserves", "remittances",
    "crude oil", "oil price", "brent", "opec", "natural gas", "lng",
    "coal", "palm oil", "cotton", "wheat", "urea", "sugar",
    "steel", "iron", "copper", "gold",
    "dividend", "bonus share", "rights issue", "ipo",
    "quarterly results", "annual results",
    "earnings", "revenue", "profit", "loss",  # generic but relevant if paired
    "sbp", "state bank",  # also in strong but allow as weak pair
    "imf", "world bank",
    "flood", "drought", "crop",
    "budget", "fiscal", "tax",
    "geopolitical", "sanction",
    "msci", "emerging market",
    "capacity expansion", "plant shutdown",
    "npl", "loan", "credit rating",
    "loadshedding", "load shedding", "power outage",
    "subsidy", "tariff",
})

_STRONG_PATTERN = re.compile(
    r"\b(" + "|".join(re.escape(k) for k in sorted(_STRONG, key=len, reverse=True)) + r")\b",
    re.IGNORECASE,
)
_WEAK_PATTERN = re.compile(
    r"\b(" + "|".join(re.escape(k) for k in sorted(_WEAK, key=len, reverse=True)) + r")\b",
    re.IGNORECASE,
)


def is_relevant(text: str) -> bool:
    """
    Return True if the article is PSX-relevant.
    Rules:
    - Any STRONG keyword match → relevant immediately
    - 2+ distinct WEAK keyword matches → relevant
    - 1 weak match alone → not relevant (too generic)
    """
    if not text:
        return False

    if _STRONG_PATTERN.search(text):
        return True

    weak_matches = {m.group(0).lower() for m in _WEAK_PATTERN.finditer(text)}
    return len(weak_matches) >= 2


def get_matching_keywords(text: str) -> list[str]:
    """Return all matching keywords found in text."""
    if not text:
        return []
    strong = {m.group(0).lower() for m in _STRONG_PATTERN.finditer(text)}
    weak = {m.group(0).lower() for m in _WEAK_PATTERN.finditer(text)}
    return sorted(strong | weak)
