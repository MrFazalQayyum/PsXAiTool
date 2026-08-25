"""
One-time script: import companies from data/psx_companies.csv into the database.
Run from the backend/ directory:  python scripts/seed_companies.py
"""
import csv
import os
import sys

# Make sure the app package is importable
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.core.database import SessionLocal, init_db
from app.models.market import Company

CSV_PATH = os.path.join(
    os.path.dirname(__file__), "..", "..", "data", "psx_companies.csv"
)


def main():
    init_db()
    db = SessionLocal()
    try:
        with open(CSV_PATH, newline="", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            added = 0
            skipped = 0
            for row in reader:
                exists = (
                    db.query(Company)
                    .filter(Company.symbol == row["symbol"])
                    .first()
                )
                if exists:
                    skipped += 1
                    continue
                db.add(
                    Company(
                        symbol=row["symbol"],
                        yahoo_ticker=row["yahoo_ticker"],
                        name=row["name"],
                        sector=row["sector"],
                    )
                )
                added += 1
        db.commit()
        print(f"Done — added {added} companies, skipped {skipped} already present.")
    finally:
        db.close()


if __name__ == "__main__":
    main()
