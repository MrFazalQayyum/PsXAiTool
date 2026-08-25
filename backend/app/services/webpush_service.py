import json
import logging
from pywebpush import webpush, WebPushException
from app.core.config import settings
from app.core.database import SessionLocal
from app.models.news import PushSubscription

logger = logging.getLogger(__name__)


def _vapid_claims() -> dict:
    return {"sub": settings.VAPID_CLAIMS_EMAIL}


def send_push_to_all(payload: dict) -> int:
    """Send a Web Push notification to all active subscribers. Returns sent count."""
    if not settings.VAPID_PRIVATE_KEY or not settings.VAPID_PUBLIC_KEY:
        logger.info("VAPID keys not configured — skipping Web Push")
        return 0

    db = SessionLocal()
    sent = 0
    dead = []

    try:
        subs = db.query(PushSubscription).filter(PushSubscription.is_active == True).all()
        for sub in subs:
            try:
                webpush(
                    subscription_info={
                        "endpoint": sub.endpoint,
                        "keys": {"p256dh": sub.p256dh, "auth": sub.auth},
                    },
                    data=json.dumps(payload),
                    vapid_private_key=settings.VAPID_PRIVATE_KEY,
                    vapid_claims=_vapid_claims(),
                )
                sent += 1
            except WebPushException as e:
                status = getattr(e.response, "status_code", None)
                if status in (404, 410):
                    dead.append(sub.id)
                    logger.info(f"Push subscription expired: {sub.endpoint[:40]}")
                else:
                    logger.warning(f"Push failed ({status}): {e}")

        # Deactivate dead subscriptions
        if dead:
            db.query(PushSubscription).filter(PushSubscription.id.in_(dead)).update(
                {"is_active": False}, synchronize_session=False
            )
            db.commit()

    finally:
        db.close()

    logger.info(f"Web Push sent to {sent} subscribers")
    return sent


def send_signal_push(signal: dict) -> int:
    direction = signal.get("direction", "neutral")
    icon = "📈" if direction == "bullish" else "📉" if direction == "bearish" else "📊"
    tickers = ", ".join(signal.get("entities") or []) or "Market"
    return send_push_to_all({
        "title": f"{icon} PSX Signal — {tickers}",
        "body": signal.get("summary", "New market signal detected"),
        "signal_type": signal.get("signal_type"),
        "direction": direction,
        "confidence": signal.get("confidence"),
        "url": "/signals",
    })


def send_briefing_push(briefing_text: str) -> int:
    return send_push_to_all({
        "title": "📋 PSX Morning Briefing",
        "body": briefing_text[:120] + "…" if len(briefing_text) > 120 else briefing_text,
        "url": "/signals",
    })
