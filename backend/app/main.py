import logging

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import settings
from app.core.database import init_db
from app.api.market import router as market_router
from app.api.signals import router as signals_router
from app.api.notifications import router as notifications_router
from app.api.portfolio import router as portfolio_router

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s  %(name)s — %(message)s",
)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="PSX Intelligence API",
    description="Pakistan Stock Exchange AI-powered market intelligence",
    version="0.1.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=[settings.FRONTEND_URL, "http://localhost:3000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(market_router)
app.include_router(signals_router)
app.include_router(notifications_router)
app.include_router(portfolio_router)


@app.on_event("startup")
def on_startup():
    logger.info("Initialising database schema")
    init_db()
    logger.info("PSX Intelligence API ready")


@app.get("/health")
def health():
    return {"status": "ok"}
