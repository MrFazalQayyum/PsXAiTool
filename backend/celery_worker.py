"""
Celery application entry point.
Run the worker:  celery -A celery_worker.celery_app worker --loglevel=info --pool=solo
Run the beat:    celery -A celery_worker.celery_app beat  --loglevel=info
"""
from celery.schedules import crontab
from app.core.celery_app import celery_app
from app.core.config import settings

# Import tasks so they register with the app
import app.tasks.price_tasks  # noqa: F401
import app.tasks.news_tasks   # noqa: F401

celery_app.conf.beat_schedule = {
    "fetch-prices-daily": {
        "task": "tasks.fetch_all_prices",
        "schedule": crontab(
            hour=settings.PRICE_FETCH_HOUR_UTC,
            minute=settings.PRICE_FETCH_MINUTE_UTC,
            day_of_week="mon-fri",
        ),
    },
    "fetch-news-every-30min": {
        "task": "tasks.fetch_and_process_news",
        "schedule": crontab(minute="*/30"),   # every 30 minutes, all days
    },
    "validate-signals-after-close": {
        "task": "tasks.validate_signals",
        "schedule": crontab(hour=11, minute=30, day_of_week="mon-fri"),  # 4:30 PM PKT
    },
    # Morning briefing — weekdays 8:00 AM PKT (3:00 UTC)
    "daily-briefing-morning": {
        "task": "tasks.generate_daily_briefing",
        "schedule": crontab(hour=3, minute=0, day_of_week="mon-fri"),
    },
}
