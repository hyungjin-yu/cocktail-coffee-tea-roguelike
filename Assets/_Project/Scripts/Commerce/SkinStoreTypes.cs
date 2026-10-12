using System;
using System.Collections.Generic;

namespace CCT
{
    /// <summary>스토어가 알려 준 상품 정보. 가격은 스토어 현지화 문자열 그대로(코드에 가격 없음).</summary>
    public class SkinProductInfo
    {
        public string ProductId;
        public string LocalizedPrice;
        public string LocalizedTitle;
        public bool AvailableToPurchase;
    }

    /// <summary>스토어가 넘긴 구매 1건. Handle은 스토어 어댑터 내부 객체(확인/승인할 때 그대로 돌려준다).</summary>
    public class StorePurchase
    {
        public string ProductId;
        public string PurchaseToken;
        public object Handle;
    }

    public enum StoreFailure { Cancelled, AlreadyOwned, ProductUnavailable, StoreUnavailable, PaymentDeclined, ExistingPending, Unknown }

    /// <summary>스토어 → 상거래 로직 콜백. 모든 콜백은 메인 스레드에서 호출되어야 한다.</summary>
    public interface ISkinStoreListener
    {
        void OnStoreReady(IReadOnlyList<SkinProductInfo> products);
        void OnStoreUnavailable(string message);
        /// <summary>결제 완료·승인 전(검증 후 Confirm 필요).</summary>
        void OnPurchasePending(StorePurchase purchase);
        /// <summary>이미 승인된 구매(복원/재설치).</summary>
        void OnPurchaseAlreadyConfirmed(StorePurchase purchase);
        /// <summary>Google Play 보류 결제(현금 결제 등) — 완료 전에는 지급하지 않는다.</summary>
        void OnPurchaseDeferred(string productId);
        void OnPurchaseFailed(string productId, StoreFailure reason, string detail);
        void OnConfirmFinished(string productId, bool success);
    }

    /// <summary>
    /// 스토어프런트 추상화(현재 Google Play / 없음, Steam은 나중에 어댑터 추가). 소유권은 스토어프런트별로 따로 기록한다 —
    /// Google 구매가 Steam에서 열리지 않는다(계정 연동은 미구현).
    /// </summary>
    public interface ISkinStore
    {
        /// <summary>"googleplay" / "none"(향후 "steam"). 소유권 기록 키.</summary>
        string StorefrontId { get; }
        /// <summary>이 플랫폼에서 실제 결제가 가능한 스토어인지. false면 구매/복원 버튼을 막는다.</summary>
        bool SupportsPurchasing { get; }
        /// <summary>SupportsPurchasing=false일 때 사용자 안내 문구.</summary>
        string UnsupportedMessage { get; }
        void Initialize(IReadOnlyList<string> productIds, ISkinStoreListener listener);
        void Purchase(string productId);
        /// <summary>검증+저장이 끝난 구매만 승인한다.</summary>
        void Confirm(StorePurchase purchase);
        /// <summary>구매 내역 재조회(복원).</summary>
        void FetchPurchases();
    }

    public static class Storefronts
    {
        public const string GooglePlay = "googleplay";
        // 향후 Steam 어댑터를 추가하면 "steam" 키를 쓴다(소유권은 스토어프런트별로 분리 — 계정 연동 없이 이전되지 않음).
        public const string None = "none";
    }

    /// <summary>Windows/에디터 기본값 — 결제 없음(가짜 구매 없음).</summary>
    public sealed class UnavailableSkinStore : ISkinStore
    {
        public const string Message = "현재 플랫폼에서는 구매할 수 없습니다(준비 중)";
        public string StorefrontId => Storefronts.None;
        public bool SupportsPurchasing => false;
        public string UnsupportedMessage => Message;
        public void Initialize(IReadOnlyList<string> productIds, ISkinStoreListener listener) => listener?.OnStoreUnavailable(Message);
        public void Purchase(string productId) { }
        public void Confirm(StorePurchase purchase) { }
        public void FetchPurchases() { }
    }

    /// <summary>플랫폼별 실제 스토어 선택. 테스트용 가짜 스토어는 테스트 어셈블리에만 있다(여기서 고를 수 없음).</summary>
    public static class CommercePlatform
    {
        public static ISkinStore CreateStore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new UnityIapSkinStore();
#else
            return new UnavailableSkinStore();
#endif
        }
    }
}
