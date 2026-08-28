web: cd backend && uvicorn app.main:app --host 0.0.0.0 --port $PORT
worker: cd backend && celery -A app.core.celery_app worker --loglevel=info --concurrency=2
beat: cd backend && celery -A app.core.celery_app beat --loglevel=info
