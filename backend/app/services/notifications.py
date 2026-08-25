import logging
import smtplib
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText

from app.core.config import settings

logger = logging.getLogger(__name__)

_DIRECTION_ICON = {"bullish": "📈", "bearish": "📉", "neutral": "➡️"}


def send_signal_email(signal: dict) -> bool:
    """Send HTML email alert for a new market signal. Returns True on success."""
    if not settings.SMTP_USER or not settings.SMTP_PASSWORD or not settings.ALERT_EMAIL:
        logger.info("Email not configured — skipping notification")
        return False

    direction = signal.get("direction", "neutral")
    icon = _DIRECTION_ICON.get(direction, "")
    signal_label = signal.get("signal_type", "").replace("_", " ").title()
    tickers = ", ".join(signal.get("entities") or []) or "N/A"
    sectors = ", ".join(signal.get("sectors") or []) or "N/A"
    confidence_pct = int((signal.get("confidence") or 0) * 100)

    subject = f"PSX Signal {icon}: {signal_label}"

    historical = ""
    if signal.get("historical_note"):
        historical = f'<p style="color:#888;font-size:13px"><em>📖 {signal["historical_note"]}</em></p>'

    body = f"""<html><body style="font-family:Arial,sans-serif;background:#0A1628;color:#C2D8F0;padding:20px">
<div style="max-width:600px;margin:auto;background:#0F1E38;border-radius:12px;padding:24px;border:1px solid #1C334F">
  <h2 style="color:#00C49A;margin-top:0">{icon} PSX Market Signal</h2>
  <table style="width:100%;border-collapse:collapse">
    <tr><td style="padding:6px 0;color:#7A9BBF;width:140px">Signal</td>
        <td style="padding:6px 0;font-weight:bold">{signal_label}</td></tr>
    <tr><td style="padding:6px 0;color:#7A9BBF">Direction</td>
        <td style="padding:6px 0;color:{"#00C49A" if direction == "bullish" else "#E0536A"};font-weight:bold">{direction.upper()} {icon}</td></tr>
    <tr><td style="padding:6px 0;color:#7A9BBF">Confidence</td>
        <td style="padding:6px 0">{confidence_pct}%</td></tr>
    <tr><td style="padding:6px 0;color:#7A9BBF">Tickers</td>
        <td style="padding:6px 0;font-family:monospace">{tickers}</td></tr>
    <tr><td style="padding:6px 0;color:#7A9BBF">Sectors</td>
        <td style="padding:6px 0">{sectors}</td></tr>
  </table>
  <p style="margin-top:16px;line-height:1.6">{signal.get("summary", "")}</p>
  {historical}
  <p style="font-size:11px;color:#3A5570;margin-top:24px;border-top:1px solid #1C334F;padding-top:12px">
    PSX Intelligence — automated market signal alert
  </p>
</div>
</body></html>"""

    try:
        msg = MIMEMultipart("alternative")
        msg["From"] = settings.SMTP_USER
        msg["To"] = settings.ALERT_EMAIL
        msg["Subject"] = subject
        msg.attach(MIMEText(body, "html"))

        with smtplib.SMTP_SSL("smtp.gmail.com", 465, timeout=10) as server:
            server.login(settings.SMTP_USER, settings.SMTP_PASSWORD)
            server.sendmail(settings.SMTP_USER, settings.ALERT_EMAIL, msg.as_string())

        logger.info(f"Signal email sent: {signal_label}")
        return True

    except Exception as e:
        logger.error(f"Email send failed: {e}")
        return False
