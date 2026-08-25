from sqlalchemy import (
    Column, String, Integer, Text, Boolean,
    DateTime, Numeric, JSON, ForeignKey, text,
)
from app.core.database import Base


class NewsArticle(Base):
    __tablename__ = "news_articles"

    id = Column(Integer, primary_key=True, autoincrement=True)
    source = Column(String(100), nullable=False)
    url = Column(String(500), unique=True, nullable=False)
    title = Column(String(500), nullable=False)
    description = Column(Text)
    content = Column(Text)
    published_at = Column(DateTime(timezone=True), index=True)
    fetched_at = Column(DateTime(timezone=True), server_default=text("now()"))
    is_relevant = Column(Boolean, default=False)
    processed_at = Column(DateTime(timezone=True))

    def __repr__(self):
        return f"<NewsArticle {self.source}: {self.title[:60]}>"


class Signal(Base):
    __tablename__ = "signals"

    id = Column(Integer, primary_key=True, autoincrement=True)
    article_id = Column(Integer, ForeignKey("news_articles.id"), nullable=True)
    signal_type = Column(String(100), nullable=False, index=True)
    entities = Column(JSON)           # list of PSX ticker symbols
    sectors = Column(JSON)            # list of sector names
    direction = Column(String(20))    # "bullish", "bearish", "neutral"
    confidence = Column(Numeric(4, 3))
    summary = Column(Text)
    historical_note = Column(Text)
    raw_headline = Column(Text)
    created_at = Column(DateTime(timezone=True), server_default=text("now()"), index=True)
    is_notified = Column(Boolean, default=False)

    def __repr__(self):
        return f"<Signal {self.signal_type} {self.direction}>"


class SignalValidation(Base):
    __tablename__ = "signal_validations"

    id = Column(Integer, primary_key=True, autoincrement=True)
    signal_id = Column(Integer, ForeignKey("signals.id"), nullable=False)
    symbol = Column(String(20), nullable=False)
    predicted_direction = Column(String(20))
    actual_change_pct = Column(Numeric(8, 4))
    verdict = Column(String(20))      # "CORRECT", "WRONG", "NEUTRAL"
    validated_at = Column(DateTime(timezone=True), server_default=text("now()"))

    def __repr__(self):
        return f"<SignalValidation {self.symbol} {self.verdict}>"


class DailyBriefing(Base):
    __tablename__ = "daily_briefings"

    id = Column(Integer, primary_key=True, autoincrement=True)
    briefing_date = Column(DateTime(timezone=True), unique=True, nullable=False, index=True)
    content = Column(Text, nullable=False)          # Full briefing text
    signal_count = Column(Integer, default=0)       # Signals included
    accuracy_pct = Column(Numeric(5, 2))            # Overall accuracy at time of briefing
    created_at = Column(DateTime(timezone=True), server_default=text("now()"))
    is_pushed = Column(Boolean, default=False)

    def __repr__(self):
        return f"<DailyBriefing {self.briefing_date}>"


class PushSubscription(Base):
    __tablename__ = "push_subscriptions"

    id = Column(Integer, primary_key=True, autoincrement=True)
    endpoint = Column(Text, unique=True, nullable=False)
    p256dh = Column(Text, nullable=False)
    auth = Column(Text, nullable=False)
    is_active = Column(Boolean, default=True)
    created_at = Column(DateTime(timezone=True), server_default=text("now()"))

    def __repr__(self):
        return f"<PushSubscription {self.endpoint[:50]}>"
