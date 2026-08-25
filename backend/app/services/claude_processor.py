import json
import logging
import re
from typing import Optional

import anthropic

logger = logging.getLogger(__name__)

_client: Optional[anthropic.Anthropic] = None

HAIKU_MODEL = "claude-haiku-4-5"
SONNET_MODEL = "claude-sonnet-4-6"

_SYSTEM_PROMPT = """You are a Pakistan Stock Exchange (PSX) market intelligence analyst.
Analyze news articles for signals that affect PSX-listed companies.

PSX Tickers to recognise (no .KA suffix): OGDC, PPL, MARI, POL, PSO, ATRL, HUBCO, NCPL,
KAPCO, KEL, LUCK, DGKC, MLCF, ACPL, KOHC, PIOC, FCCL, CHCC, HBL, UBL, MCB, ABL, BAFL,
BAHL, NBP, MEBL, JSBL, NML, ENGRO, FFC, FFBL, FATIMA, TRG, SYS, NETSOL, AVN, SEARL,
GLAXO, AGTL, NESTLE, COLG, ICI, ASTL, ISL, MUGHAL, JDW.

Respond ONLY with a single valid JSON object matching this schema:
{
  "signal_type": "<snake_case_label>",
  "entities": ["OGDC", "PPL"],
  "sectors": ["Oil & Gas Exploration"],
  "direction": "bullish|bearish|neutral",
  "confidence": 0.85,
  "summary": "One sentence market signal summary.",
  "historical_note": "Optional similar past event, or null"
}

Common signal_type values: oil_price_up, oil_price_down, sbp_rate_cut, sbp_rate_hike,
pkr_depreciation, pkr_appreciation, imf_disbursement, imf_program_risk,
circular_debt_resolved, energy_subsidy_removed, gas_shortage, power_outages,
cement_demand_up, cement_oversupply, psdp_spending, steel_price_up, fertilizer_demand_up,
wheat_price_up, cotton_price_up, sugar_surplus, sugar_shortage, palm_oil_price_up,
flood_damage, drought_warning, npl_concerns, t_bill_yield_up, msci_upgrade,
us_fed_rate_cut, us_recession_fear, geopolitical_tension, foreign_reserves_up,
quarterly_earnings_beat, quarterly_earnings_miss, dividend_declared, corporate_expansion,
political_instability.

If the article has NO meaningful PSX market signal, respond with:
{"signal_type":"none","direction":"neutral","confidence":0.0}

Rules:
- Only list tickers DIRECTLY affected (max 8)
- Confidence 0.9+ = very clear macro/corporate event; 0.6-0.9 = probable; <0.6 = speculative
- Return raw JSON only, no markdown fences"""


def _get_client() -> anthropic.Anthropic:
    global _client
    if _client is None:
        from app.core.config import settings
        _client = anthropic.Anthropic(api_key=settings.ANTHROPIC_API_KEY)
    return _client


def _extract_json(text: str) -> Optional[dict]:
    text = text.strip()
    # Strip markdown fences if present
    match = re.search(r"```(?:json)?\s*([\s\S]*?)```", text)
    if match:
        text = match.group(1).strip()
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        # Try to extract first {...} block
        match = re.search(r"\{[\s\S]*\}", text)
        if match:
            try:
                return json.loads(match.group(0))
            except json.JSONDecodeError:
                pass
    return None


def _get_accuracy_note() -> str:
    """Return accuracy stats per signal_type to inject into Claude prompt."""
    try:
        from app.core.database import SessionLocal
        from app.models.news import SignalValidation, Signal
        from sqlalchemy import func
        db = SessionLocal()
        rows = (
            db.query(
                Signal.signal_type,
                func.count(SignalValidation.id).label("total"),
                func.sum(
                    __import__("sqlalchemy").case(
                        (SignalValidation.verdict == "CORRECT", 1), else_=0
                    )
                ).label("correct"),
            )
            .join(SignalValidation, SignalValidation.signal_id == Signal.id)
            .group_by(Signal.signal_type)
            .all()
        )
        db.close()
        if not rows:
            return ""
        parts = [
            f"{r.signal_type}: {int(r.correct or 0)}/{r.total} correct"
            for r in rows if (r.total or 0) >= 3
        ]
        return "\nHistorical accuracy:\n" + "\n".join(parts) if parts else ""
    except Exception:
        return ""


def process_article(title: str, description: str, content: str = "") -> Optional[dict]:
    """
    Extract a structured PSX market signal from a news article using claude-haiku-4-5.
    Returns the signal dict, or None if no signal or API error.
    """
    client = _get_client()

    article_text = f"Headline: {title}\n\nSummary: {description}"
    if content:
        article_text += f"\n\nContent: {content[:1500]}"

    system = _SYSTEM_PROMPT + _get_accuracy_note()

    try:
        response = client.messages.create(
            model=HAIKU_MODEL,
            max_tokens=400,
            system=system,
            messages=[{"role": "user", "content": article_text}],
        )

        raw = response.content[0].text if response.content else ""
        data = _extract_json(raw)

        if not data:
            logger.warning(f"Could not parse Claude response: {raw[:200]}")
            return None

        if data.get("signal_type") == "none" or (data.get("confidence") or 0) < 0.3:
            return None

        return data

    except anthropic.APIStatusError as e:
        logger.error(f"Claude API status error {e.status_code}: {e.message}")
        return None
    except anthropic.APIConnectionError as e:
        logger.error(f"Claude API connection error: {e}")
        return None
    except Exception as e:
        logger.error(f"Unexpected error in Claude processing: {e}")
        return None


def generate_daily_briefing(market_summary: str, top_signals: list[dict]) -> str:
    """
    Generate a morning market briefing using claude-sonnet-4-6.
    Returns formatted text briefing.
    """
    client = _get_client()

    signals_text = "\n".join(
        f"- {s.get('signal_type', '').replace('_', ' ').title()}: {s.get('summary', '')}"
        for s in top_signals[:10]
    )

    prompt = f"""Generate a concise PSX morning market briefing (max 200 words) for a Pakistani retail investor.

Market context:
{market_summary}

Recent signals:
{signals_text}

Write in clear English. Include: key themes, affected sectors, what to watch today. Be specific and actionable."""

    try:
        response = client.messages.create(
            model=SONNET_MODEL,
            max_tokens=512,
            messages=[{"role": "user", "content": prompt}],
        )
        return response.content[0].text.strip()
    except Exception as e:
        logger.error(f"Daily briefing generation failed: {e}")
        return "Market briefing unavailable today."
