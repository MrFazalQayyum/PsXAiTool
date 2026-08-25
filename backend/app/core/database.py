from sqlalchemy import create_engine, text
from sqlalchemy.orm import sessionmaker, DeclarativeBase
from .config import settings

engine = create_engine(
    settings.DATABASE_URL,
    pool_pre_ping=True,
    pool_size=10,
    max_overflow=20,
)

SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)


class Base(DeclarativeBase):
    pass


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


def init_db():
    """Create all tables and enable TimescaleDB hypertables if available."""
    from app.models import market  # noqa: F401
    from app.models import news    # noqa: F401

    Base.metadata.create_all(bind=engine)

    with engine.connect() as conn:
        _try_enable_timescaledb(conn)
        conn.commit()


def _try_enable_timescaledb(conn):
    """Convert time-series tables to hypertables if TimescaleDB is installed."""
    try:
        conn.execute(text("CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;"))

        for table, time_col in [
            ("daily_prices", "date"),
            ("market_indices", "date"),
        ]:
            result = conn.execute(
                text(
                    "SELECT 1 FROM timescaledb_information.hypertables "
                    "WHERE hypertable_name = :t"
                ),
                {"t": table},
            )
            if not result.fetchone():
                conn.execute(
                    text(
                        f"SELECT create_hypertable('{table}', '{time_col}', "
                        f"if_not_exists => TRUE);"
                    )
                )
    except Exception:
        # TimescaleDB not installed — standard PostgreSQL continues fine
        pass
