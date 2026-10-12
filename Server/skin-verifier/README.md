# Skin purchase verification

This service is prepared locally, **not deployed**. The mobile shop must keep purchases disabled until a working HTTPS endpoint and Google Play products are configured.

`POST /verify`: `{ "productId": "skin_tea_jade", "purchaseToken": "...", "installationId": "..." }`

Response: `{ "verified": true, "productId": "skin_tea_jade" }`. `false` does not grant a skin. HTTP 503 means retry later; keep a paid order pending instead of acknowledging it before fulfillment.

The server queries Google Play ProductPurchaseV2 for the fixed package name. It accepts only a purchased, unconsumed, non-rental item with the exact configured product ID and remaining refundable quantity. Pending/cancelled/refunded purchases are denied. Test purchases require `ALLOW_TEST_PURCHASES=true` on a separate test deployment. Repeated verification is safe; the ledger stores token hashes and checks Google again for restores. Installation IDs are not an account identity and do not prevent legitimate reinstall restores.

## Run

1. Create a service account with access to this app's purchases in Play Console; enable the Google Play Android Developer API. Keep credentials outside the repo and APK.
2. Set `PLAY_PACKAGE_NAME` to the actual registered package, `GOOGLE_APPLICATION_CREDENTIALS` to the server-only credential file (or use workload identity), and `ENTITLEMENT_DB` to a persistent writable path.
3. Install `requirements.txt`. Start `uvicorn app:app --host 127.0.0.1 --port 8080` behind an HTTPS reverse proxy.
4. Apply gateway rate limiting and body-size limits. Configure proxies so the app receives the real client address. Do not log purchase tokens or request bodies.
5. Set the Unity commerce endpoint to `https://YOUR-HOST/verify`. Test purchases and restores through Play internal testing before enabling sales.

The client grants and durably saves entitlement after successful verification, then calls Unity IAP confirmation. This server does not acknowledge orders early. Purchase authentication comes from Google-issued tokens; this initial version has no game account/login or Play Integrity integration. Add those before requiring stronger protection against shared tokens or modified clients. Refund checks occur on store synchronization; real-time notification-based revocation is a further deployment task.

Tests: `python -m unittest -v test_verifier` (no Google credentials required). These test decision logic, not live Play payments.

API reference: https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2/getproductpurchasev2
