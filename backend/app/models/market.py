from datetime import datetime
from decimal import Decimal
from sqlalchemy import (
    Column, String, Integer, BigInteger, Numeric,
    DateTime, Boolean, UniqueConstraint, Index, text,
)
from app.core.database import Base


class Company(Base):
    __tablename__ = "companies"

    id = Column(Integer, primary_key=True, autoincrement=True)
    symbol = Column(String(20), unique=True, nullable=False, index=True)
    yahoo_ticker = Column(String(30))           # e.g. "OGDC.KA"
    name = Column(String(200), nullable=False)
    sector = Column(String(100), index=True)
    market_cap = Column(Numeric(22, 2))
    shares_outstanding = Column(BigInteger)
    is_active = Column(Boolean, default=True, nullable=False)
    created_at = Column(DateTime(timezone=True), server_default=text("now()"))
    updated_at = Column(DateTime(timezone=True), server_default=text("now()"), onupdate=datetime.utcnow)

    def __repr__(self):
        return f"<Company {self.symbol}>"


class DailyPrice(Base):
    __tablename__ = "daily_prices"

    id = Column(Integer, primary_key=True, autoincrement=True)
    date = Column(DateTime(timezone=True), nullable=False, index=True)
    symbol = Column(String(20), nullable=False, index=True)
    open = Column(Numeric(14, 4))
    high = Column(Numeric(14, 4))
    low = Column(Numeric(14, 4))
    close = Column(Numeric(14, 4))
    volume = Column(BigInteger)
    change_pct = Column(Numeric(8, 4))   # % change vs previous close

    __table_args__ = (
        UniqueConstraint("date", "symbol", name="uq_daily_price_date_symbol"),
        Index("ix_daily_prices_symbol_date", "symbol", "date"),
    )

    def __repr__(self):
        return f"<DailyPrice {self.symbol} {self.date.date()} close={self.close}>"


class MarketIndex(Base):
    __tablename__ = "market_indices"

    id = Column(Integer, primary_key=True, autoincrement=True)
    date = Column(DateTime(timezone=True), nullable=False, index=True)
    index_name = Column(String(50), nullable=False, index=True)  # KSE-100, KSE-30, KMI-30
    value = Column(Numeric(16, 4))
    change = Column(Numeric(16, 4))
    change_pct = Column(Numeric(8, 4))
    volume = Column(BigInteger)

    __table_args__ = (
        UniqueConstraint("date", "index_name", name="uq_market_index_date_name"),
    )

    def __repr__(self):
        return f"<MarketIndex {self.index_name} {self.date.date()} value={self.value}>"


class Portfolio(Base):
    __tablename__ = "portfolio"

    id = Column(Integer, primary_key=True, autoincrement=True)
    symbol = Column(String(20), nullable=False, unique=True, index=True)
    shares_held = Column(Numeric(18, 4), nullable=False)
    avg_buy_price = Column(Numeric(14, 4), nullable=False)
    notes = Column(String(500))
    created_at = Column(DateTime(timezone=True), server_default=text("now()"))
    updated_at = Column(DateTime(timezone=True), server_default=text("now()"), onupdate=datetime.utcnow)

    def __repr__(self):
        return f"<Portfolio {self.symbol} x{self.shares_held}>"
