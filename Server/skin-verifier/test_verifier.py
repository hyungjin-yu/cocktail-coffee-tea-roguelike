import copy
import unittest
from types import SimpleNamespace
from unittest.mock import patch
from fastapi import HTTPException
import app
from app import is_entitled


class EntitlementTests(unittest.TestCase):
    def setUp(self):
        self.purchase = {"purchaseStateContext": {"purchaseState": "PURCHASED"},
                         "productLineItem": [{"productId": "skin_tea_jade", "productOfferDetails": {
                             "refundableQuantity": 1, "consumptionState": "CONSUMPTION_STATE_YET_TO_BE_CONSUMED"}}]}

    def test_paid_nonconsumable(self):
        self.assertTrue(is_entitled(self.purchase, "skin_tea_jade"))

    def test_pending_cancelled_unknown_not_granted(self):
        for state in ("PENDING", "CANCELLED", "PURCHASE_STATE_UNSPECIFIED"):
            self.purchase["purchaseStateContext"]["purchaseState"] = state
            self.assertFalse(is_entitled(self.purchase, "skin_tea_jade"))
        self.assertFalse(is_entitled({}, "unknown"))

    def test_wrong_product_not_granted(self):
        self.assertFalse(is_entitled(self.purchase, "skin_bartender_midnight"))

    def test_refunded_consumed_or_rented_not_granted(self):
        for change in ({"refundableQuantity": 0}, {"consumptionState": "CONSUMPTION_STATE_CONSUMED"}, {"rentOfferDetails": {}}):
            purchase = copy.deepcopy(self.purchase)
            purchase["productLineItem"][0]["productOfferDetails"].update(change)
            self.assertFalse(is_entitled(purchase, "skin_tea_jade"))

    def test_test_purchases_require_explicit_test_environment(self):
        self.purchase["testPurchaseContext"] = {"fopType": "TEST"}
        self.assertFalse(is_entitled(self.purchase, "skin_tea_jade"))
        self.assertTrue(is_entitled(self.purchase, "skin_tea_jade", allow_test=True))

    def test_verification_does_not_trust_client_claim(self):
        app._requests.clear()
        body=app.VerifyRequest(productId="skin_tea_jade", purchaseToken="unverified", installationId="test-install")
        with patch("app.fetch_purchase", return_value={}), patch("app.record_verification") as record:
            result=app.verify(body, SimpleNamespace(client=SimpleNamespace(host="test")))
        self.assertFalse(result["verified"])
        record.assert_not_called()

    def test_ledger_failure_never_returns_success(self):
        app._requests.clear()
        body=app.VerifyRequest(productId="skin_tea_jade", purchaseToken="paid-token", installationId="test-install")
        with patch("app.fetch_purchase", return_value=self.purchase), patch("app.record_verification", side_effect=OSError("private error")):
            with self.assertRaises(HTTPException) as error:
                app.verify(body, SimpleNamespace(client=SimpleNamespace(host="test")))
        self.assertEqual(503, error.exception.status_code)
        self.assertNotIn("private", error.exception.detail)

    def test_repeated_paid_verification_supports_restore(self):
        app._requests.clear()
        body=app.VerifyRequest(productId="skin_tea_jade", purchaseToken="paid-token", installationId="test-install")
        with patch("app.fetch_purchase", return_value=self.purchase) as fetch, patch("app.record_verification") as record:
            for _ in range(2):
                self.assertTrue(app.verify(body, SimpleNamespace(client=SimpleNamespace(host="test")))["verified"])
        self.assertEqual(2, fetch.call_count)
        self.assertEqual(2, record.call_count)

    def test_invalid_product_does_not_call_google(self):
        body=app.VerifyRequest(productId="unknown", purchaseToken="token", installationId="test-install")
        with patch("app.fetch_purchase") as fetch:
            with self.assertRaises(HTTPException) as error:
                app.verify(body, SimpleNamespace(client=SimpleNamespace(host="test")))
        self.assertEqual(400, error.exception.status_code)
        fetch.assert_not_called()


if __name__ == "__main__":
    unittest.main()
