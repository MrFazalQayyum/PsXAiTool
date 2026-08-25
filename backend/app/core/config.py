from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    DATABASE_URL: str = "postgresql://psx_user:psx_password@localhost:5432/psx_intelligence"
    REDIS_URL: str = "redis://localhost:6379/0"
    ANTHROPIC_API_KEY: str = ""
    FRONTEND_URL: str = "http://localhost:3000"

    # Celery beat schedule — PSX closes at 15:30 PKT = 10:30 UTC
    PRICE_FETCH_HOUR_UTC: int = 10
    PRICE_FETCH_MINUTE_UTC: int = 45

    # How many days of history to load on first run
    INITIAL_HISTORY_DAYS: int = 730  # 2 years

    # Email notifications (Gmail SMTP)
    SMTP_USER: str = ""
    SMTP_PASSWORD: str = ""          # Gmail app password
    ALERT_EMAIL: str = ""            # where to send signal alerts

    # News fetch schedule (every 30 min during trading + off-hours)
    NEWS_FETCH_INTERVAL_MINUTES: int = 30

    # Web Push (VAPID)
    VAPID_PUBLIC_KEY: str = ""
    VAPID_PRIVATE_KEY: str = ""
    VAPID_CLAIMS_EMAIL: str = "mailto:admin@psx.local"

    class Config:
        env_file = ".env"


settings = Settings()
