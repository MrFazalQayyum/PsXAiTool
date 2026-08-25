from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from pydantic import BaseModel
from app.core.database import get_db
from app.core.config import settings
from app.models.news import PushSubscription

router = APIRouter(prefix="/api/notifications", tags=["notifications"])


class PushKeys(BaseModel):
    p256dh: str
    auth: str


class SubscribeRequest(BaseModel):
    endpoint: str
    keys: PushKeys


@router.get("/vapid-public-key")
def get_vapid_public_key():
    if not settings.VAPID_PUBLIC_KEY:
        raise HTTPException(status_code=503, detail="Web Push not configured")
    return {"public_key": settings.VAPID_PUBLIC_KEY}


@router.post("/subscribe")
def subscribe(req: SubscribeRequest, db: Session = Depends(get_db)):
    existing = db.query(PushSubscription).filter(
        PushSubscription.endpoint == req.endpoint
    ).first()

    if existing:
        existing.p256dh = req.keys.p256dh
        existing.auth = req.keys.auth
        existing.is_active = True
        db.commit()
        return {"status": "updated"}

    sub = PushSubscription(
        endpoint=req.endpoint,
        p256dh=req.keys.p256dh,
        auth=req.keys.auth,
    )
    db.add(sub)
    db.commit()
    return {"status": "subscribed"}


@router.post("/unsubscribe")
def unsubscribe(req: SubscribeRequest, db: Session = Depends(get_db)):
    db.query(PushSubscription).filter(
        PushSubscription.endpoint == req.endpoint
    ).update({"is_active": False})
    db.commit()
    return {"status": "unsubscribed"}


@router.get("/subscribers/count")
def subscriber_count(db: Session = Depends(get_db)):
    count = db.query(PushSubscription).filter(PushSubscription.is_active == True).count()
    return {"active_subscribers": count}
