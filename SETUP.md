# PSX Intelligence — Phase 1 Setup

## Prerequisites
- Docker Desktop (for PostgreSQL + Redis)
- Python 3.11+
- Node.js 18+

---

## 1. Start the database and Redis

```bash
docker compose up -d
```

Starts TimescaleDB (PostgreSQL) on port 5432 and Redis on port 6379.

---

## 2. Set up the backend

```bash
cd backend
```

Copy the env file and fill in your values (defaults work for local Docker):
```bash
copy .env.example .env
```

Create a virtual environment and install dependencies:
```bash
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
```

---

## 3. Seed companies and load historical data

Run these once, in order:

```bash
python scripts/seed_companies.py
```
Imports 52 PSX companies from `data/psx_companies.csv`.

```bash
python scripts/fetch_history.py
```
Downloads 2 years of daily OHLCV from Yahoo Finance (~5-10 min).

---

## 4. Start the backend API

```bash
uvicorn app.main:app --reload --port 8000
```

API is live at http://localhost:8000
Health check: http://localhost:8000/health
API docs: http://localhost:8000/docs

---

## 5. Start the Celery worker (daily price updates)

Open a second terminal in `backend/` with venv active:

```bash
celery -A celery_worker.celery_app worker --loglevel=info --pool=solo
```

Start the beat scheduler (triggers daily fetch after PSX close):

```bash
celery -A celery_worker.celery_app beat --loglevel=info
```

---

## 6. Start the frontend

```bash
cd frontend
npm install
npm run dev
```

Dashboard is live at http://localhost:3000

---

## Manual price refresh

Click **Fetch Now** in the dashboard header, or call the API directly:

```bash
curl -X POST http://localhost:8000/api/market/admin/fetch-prices
```

---

## Project structure

```
PSX AI Tool/
├── backend/
│   ├── app/
│   │   ├── core/          config, database
│   │   ├── models/        SQLAlchemy models
│   │   ├── scrapers/      yahoo_finance, psx_scraper
│   │   ├── tasks/         Celery price tasks
│   │   └── api/           FastAPI routes
│   ├── scripts/           seed_companies, fetch_history
│   └── celery_worker.py
├── frontend/
│   ├── app/               Next.js App Router
│   ├── components/        KSEIndexCard, TopMovers, SectorGrid, StockChart
│   └── lib/               api.ts, types.ts
├── data/
│   └── psx_companies.csv  52 major PSX companies
└── docker-compose.yml     PostgreSQL (TimescaleDB) + Redis
```
