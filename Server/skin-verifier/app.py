"""Google Play cosmetic receipt verification. Credentials remain on the server."""
import hashlib
import os
import sqlite3
import threading
import time
from collections import defaultdict, deque
from pathlib import Path
from urllib.parse import quote

from fastapi import FastAPI, HTTPException, Request
from pydantic import BaseModel, Field

PRODUCTS = frozenset({"skin_bartender_midnight", "skin_tea_jade"})
PACKAGE = os.getenv("PLAY_PACKAGE_NAME", "com.hyungjinyu.cocktailcoffeetea")
app = FastAPI(title="Cocktail Coffee Tea cosmetic verification")
_requests = defaultdict(deque)
_lock = threading.Lock()


class VerifyRequest(BaseModel):
    productId: str = Field(min_length=1, max_length=100)
    purchaseToken: str = Field(min_length=1, max_length=4096)
    installationId: str = Field(min_length=1, max_length=128)


def is_entitled(purchase: dict, product: str, allow_test: bool = False) -> bool:
    if product not in PRODUCTS:
        return False
    if purchase.get("purchaseStateContext", {}).get("purchaseState") != "PURCHASED":
        return False
    if "testPurchaseContext" in purchase and not allow_test:
        return False
    for item in purchase.get("productLineItem", []):
        if item.get("productId") != product:
            continue
        offer = item.get("productOfferDetails", {})
        if "rentOfferDetails" in offer or "preorderOfferDetails" in offer:
            return False
        if offer.get("consumptionState") != "CONSUMPTION_STATE_YET_TO_BE_CONSUMED":
            return False
        return int(offer.get("refundableQuantity", 0)) > 0
    return False


def fetch_purchase(token: str) -> dict:
    import google.auth
    from google.auth.transport.requests import AuthorizedSession
    credentials, _ = google.auth.default(scopes=["https://www.googleapis.com/auth/androidpublisher"])
    url = ("https://androidpublisher.googleapis.com/androidpublisher/v3/applications/"
           + quote(PACKAGE, safe="") + "/purchases/productsv2/tokens/" + quote(token, safe=""))
    with AuthorizedSession(credentials) as session:
        response = session.get(url, timeout=15)
        if response.status_code in (400, 404, 410):
            return {}
        response.raise_for_status()
        return response.json()


def record_verification(token: str, product: str) -> None:
    path = Path(os.getenv("ENTITLEMENT_DB", "/var/lib/cct/entitlements.sqlite"))
    path.parent.mkdir(parents=True, exist_ok=True)
    # Keep only a token hash. Re-check Google on every restore to catch refunds.
    with sqlite3.connect(path, timeout=10) as connection:
        connection.execute("CREATE TABLE IF NOT EXISTS verified (token_hash TEXT PRIMARY KEY, product TEXT NOT NULL, checked INTEGER NOT NULL)")
        connection.execute("INSERT INTO verified VALUES (?, ?, ?) ON CONFLICT(token_hash) DO UPDATE SET checked=excluded.checked",
                           (hashlib.sha256(token.encode()).hexdigest(), product, int(time.time())))


@app.post("/verify")
def verify(body: VerifyRequest, request: Request):
    if body.productId not in PRODUCTS:
        raise HTTPException(400, "Unknown product")
    address = request.client.host if request.client else "unknown"
    now = time.monotonic()
    with _lock:
        # Bounded retention; use gateway rate limiting in deployment as well.
        for key in list(_requests):
            if not _requests[key] or _requests[key][-1] < now - 60:
                del _requests[key]
        recent = _requests[address]
        while recent and recent[0] < now - 60:
            recent.popleft()
        if len(recent) >= 12 or len(_requests) > 10000:
            raise HTTPException(429, "Try again later")
        recent.append(now)
    try:
        purchased = fetch_purchase(body.purchaseToken)
        entitled = is_entitled(purchased, body.productId, os.getenv("ALLOW_TEST_PURCHASES") == "true")
        if entitled:
            record_verification(body.purchaseToken, body.productId)
    except Exception:
        # Never emit an upstream exception containing tokens or credentials.
        raise HTTPException(503, "Verification unavailable") from None
    return {"verified": entitled, "productId": body.productId}


@app.get("/health")
def health():
    return {"status": "ok"}
