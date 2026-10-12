#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace CCT
{
    /// <summary>
    /// Google Play 스토어 어댑터 — Unity Purchasing 5.x(StoreController) 호출은 이 파일에만 있다.
    /// Android 대상에서만 컴파일되고(UNITY_ANDROID), 실제 생성은 Android 플레이어에서만 한다(CommercePlatform).
    /// 보류 주문은 스스로 다루기 위해 ProcessPendingOrdersOnPurchasesFetched(false)로 두고, FetchPurchases 결과의
    /// PendingOrders를 직접 상거래 로직에 넘긴다(로직이 토큰으로 중복 제거).
    /// </summary>
    public sealed class UnityIapSkinStore : ISkinStore
    {
        StoreController controller;
        ISkinStoreListener listener;
        List<string> productIds = new List<string>();
        bool productsRequested;

        public string StorefrontId => Storefronts.GooglePlay;
        public bool SupportsPurchasing => true;
        public string UnsupportedMessage => null;

        public void Initialize(IReadOnlyList<string> ids, ISkinStoreListener l)
        {
            listener = l;
            productIds = new List<string>(ids);
            try
            {
                if (controller == null)
                {
                    controller = UnityIAPServices.StoreController();
                    controller.ProcessPendingOrdersOnPurchasesFetched(false);
                    controller.OnStoreConnected += OnStoreConnected;
                    controller.OnStoreDisconnected += OnStoreDisconnected;
                    controller.OnProductsFetched += OnProductsFetched;
                    controller.OnProductsFetchFailed += OnProductsFetchFailed;
                    controller.OnPurchasesFetched += OnPurchasesFetched;
                    controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                    controller.OnPurchasePending += OnPurchasePending;
                    controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                    controller.OnPurchaseFailed += OnPurchaseFailed;
                    controller.OnPurchaseDeferred += OnPurchaseDeferred;
                }
                Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] 스토어 초기화 실패: {e.GetType().Name}");
                listener?.OnStoreUnavailable("스토어에 연결하지 못했습니다");
            }
        }

        async void Connect()
        {
            try
            {
                if (controller.GetConnectionState() != ConnectionState.Connected)
                    await controller.Connect();
                if (controller.GetConnectionState() == ConnectionState.Connected) RequestProducts();
                else listener?.OnStoreUnavailable("스토어에 연결하지 못했습니다");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] 스토어 연결 실패: {e.GetType().Name}");
                listener?.OnStoreUnavailable("스토어에 연결하지 못했습니다");
            }
        }

        void RequestProducts()
        {
            if (productsRequested) return;
            productsRequested = true;
            var defs = productIds.Select(id => new ProductDefinition(id, ProductType.NonConsumable)).ToList();
            controller.FetchProducts(defs);
        }

        void OnStoreConnected() => RequestProducts();

        void OnStoreDisconnected(StoreConnectionFailureDescription d)
        {
            productsRequested = false;
            Debug.LogWarning("[Commerce] 스토어 연결 끊김");
            listener?.OnStoreUnavailable("스토어 연결이 끊겼습니다");
        }

        void OnProductsFetched(List<Product> products)
        {
            var infos = new List<SkinProductInfo>();
            foreach (var p in products)
            {
                if (p?.definition == null) continue;
                infos.Add(new SkinProductInfo
                {
                    ProductId = p.definition.id,
                    LocalizedPrice = p.metadata?.localizedPriceString,
                    LocalizedTitle = p.metadata?.localizedTitle,
                    AvailableToPurchase = p.availableToPurchase,
                });
            }
            listener?.OnStoreReady(infos);
            controller.FetchPurchases();
        }

        void OnProductsFetchFailed(ProductFetchFailed f)
        {
            productsRequested = false;
            Debug.LogWarning("[Commerce] 상품 조회 실패");
            listener?.OnStoreUnavailable("상품 정보를 불러오지 못했습니다");
        }

        void OnPurchasesFetched(Orders orders)
        {
            if (orders == null) return;
            if (orders.ConfirmedOrders != null)
                foreach (var o in orders.ConfirmedOrders) listener?.OnPurchaseAlreadyConfirmed(ToPurchase(o));
            if (orders.PendingOrders != null)
                foreach (var o in orders.PendingOrders) listener?.OnPurchasePending(ToPurchase(o));
            if (orders.DeferredOrders != null)
                foreach (var o in orders.DeferredOrders) listener?.OnPurchaseDeferred(ProductIdOf(o));
        }

        void OnPurchasesFetchFailed(PurchasesFetchFailureDescription d) =>
            Debug.LogWarning($"[Commerce] 구매 내역 조회 실패: {d?.FailureReason}");

        void OnPurchasePending(PendingOrder order) => listener?.OnPurchasePending(ToPurchase(order));

        void OnPurchaseConfirmed(Order order) =>
            listener?.OnConfirmFinished(ProductIdOf(order), order is ConfirmedOrder);

        void OnPurchaseFailed(FailedOrder order)
        {
            var reason = order.FailureReason switch
            {
                PurchaseFailureReason.UserCancelled => StoreFailure.Cancelled,
                PurchaseFailureReason.OrderCancelled => StoreFailure.Cancelled,
                PurchaseFailureReason.DuplicateTransaction => StoreFailure.AlreadyOwned,
                PurchaseFailureReason.ProductUnavailable => StoreFailure.ProductUnavailable,
                PurchaseFailureReason.PurchasingUnavailable => StoreFailure.StoreUnavailable,
                PurchaseFailureReason.StoreNotConnected => StoreFailure.StoreUnavailable,
                PurchaseFailureReason.PaymentDeclined => StoreFailure.PaymentDeclined,
                PurchaseFailureReason.ExistingPurchasePending => StoreFailure.ExistingPending,
                _ => StoreFailure.Unknown,
            };
            listener?.OnPurchaseFailed(ProductIdOf(order), reason, order.FailureReason.ToString()); // Details(스토어 원문)는 넘기지 않는다
        }

        void OnPurchaseDeferred(DeferredOrder order) => listener?.OnPurchaseDeferred(ProductIdOf(order));

        static string ProductIdOf(Order order)
        {
            var fromCart = order?.CartOrdered?.Items()?.FirstOrDefault()?.Product?.definition?.id;
            if (!string.IsNullOrEmpty(fromCart)) return fromCart;
            return order?.Info?.PurchasedProductInfo?.FirstOrDefault()?.productId;
        }

        static StorePurchase ToPurchase(Order order)
        {
            // Google 검증에는 purchaseToken만 쓴다. 없으면 TransactionID로 대체하지 않고 비워 둔다 →
            // 상거래 로직이 지급·승인 없이 무시(다음 구매 내역 조회 때 다시 온다). 토큰은 로그에 남기지 않는다.
            var token = order?.Info?.Google?.PurchaseToken;
            if (string.IsNullOrEmpty(token)) Debug.LogWarning("[Commerce] Google purchaseToken 없음 — 검증 불가, 지급·승인 보류");
            return new StorePurchase { ProductId = ProductIdOf(order), PurchaseToken = token, Handle = order };
        }

        public void Purchase(string productId)
        {
            if (controller == null) { listener?.OnPurchaseFailed(productId, StoreFailure.StoreUnavailable, "not initialized"); return; }
            try { controller.PurchaseProduct(productId); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] 구매 시작 실패: {e.GetType().Name}");
                listener?.OnPurchaseFailed(productId, StoreFailure.Unknown, e.GetType().Name);
            }
        }

        public void Confirm(StorePurchase purchase)
        {
            if (controller == null || !(purchase?.Handle is PendingOrder order)) return;
            try { controller.ConfirmPurchase(order); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] 구매 승인 실패: {e.GetType().Name}");
                listener?.OnConfirmFinished(purchase.ProductId, false);
            }
        }

        public void FetchPurchases()
        {
            if (controller == null) return;
            if (controller.GetConnectionState() != ConnectionState.Connected) { Connect(); return; }
            try { controller.FetchPurchases(); }
            catch (Exception e) { Debug.LogWarning($"[Commerce] 구매 내역 조회 실패: {e.GetType().Name}"); }
        }
    }
}
#endif
