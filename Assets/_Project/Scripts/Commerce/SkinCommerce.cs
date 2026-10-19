using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CCT
{
    public enum SkinStoreStatus { Disabled, NotStarted, Connecting, Ready, Unavailable }

    /// <summary>
    /// 스킨 상거래 로직(유니티 비의존 부분은 테스트에서 가짜 스토어/검증기/저장기로 구동).
    /// 순서 계약: 검증(서버 verified && productId 일치) → 소유권 디스크 저장 성공 → 그 다음에만 스토어 승인(Confirm).
    /// - 네트워크/서버 오류·저장 실패: 승인하지 않고 보류 목록에 남겨 백오프 재시도(5초→…→10분), 상점 열기/앱 복귀 때 즉시 재시도.
    /// - verified=false/불일치: 지급·승인 없음(같은 세션에서 같은 토큰 재검증 안 함). Google은 미승인 구매를 3일 뒤 환불한다.
    /// - 보류 결제(Deferred): "결제 대기 중", 결제 완료(Pending)가 올 때만 처리.
    /// - 보유 재확인: 스토어가 돌려준 승인 구매는 보유 중이어도 세션당 1회 + "구매 복원" 때 서버에 다시 확인. 거부면 소유·장착 해제
    ///   후 저장, 오류면 캐시 유지. 스토어 내역에서 아예 사라진 구매(환불로 목록에서 빠진 경우)는 아직 감지하지 않는다.
    /// 결제는 config.PurchasesEnabled + 스토어가 결제를 지원할 때만. 아니면 스토어 연결도 하지 않는다.
    /// </summary>
    public sealed class SkinCommerce : ISkinStoreListener
    {
        public const string MsgDisabled = "결제 준비 중";
        public const string MsgPriceUnavailable = "가격 정보를 불러올 수 없음";
        public const string MsgDeferred = "결제 대기 중";
        public const string MsgVerifying = "구매 확인 중…";
        public const string MsgPurchased = "구매 완료! 장착할 수 있습니다.";
        public const string MsgRetryLater = "구매 확인 서버에 연결하지 못했습니다. 잠시 후 자동으로 다시 시도합니다.";
        public const string MsgSaveFailed = "구매 정보를 저장하지 못했습니다. 잠시 후 자동으로 다시 시도합니다.";
        public const string MsgRevoked = "구매가 확인되지 않아(환불·취소 등) 스킨 소유가 해제되었습니다.";
        public const string MsgRejected = "구매를 확인하지 못했습니다. 결제가 승인되지 않아 자동 환불될 수 있습니다.";
        public const string MsgCancelled = "구매를 취소했습니다.";
        public const string MsgAlreadyOwned = "이미 보유한 스킨입니다.";
        public const string MsgProductUnavailable = "현재 구매할 수 없는 상품입니다.";
        public const string MsgStoreUnavailable = "스토어에 연결할 수 없습니다. 네트워크를 확인해 주세요.";
        public const string MsgDeclined = "결제가 거절되었습니다.";
        public const string MsgPendingInProgress = "이전 결제를 처리 중입니다.";
        public const string MsgFailed = "구매하지 못했습니다.";
        public const string MsgRestoring = "구매 내역을 확인하는 중…";
        public const string MsgEquipSaveFailed = "장착 정보를 저장하지 못했습니다.";

        public const double RetryBaseSeconds = 5d;
        public const double RetryMaxSeconds = 600d;

        sealed class PendingEntry
        {
            public StorePurchase purchase;
            public bool needsConfirm;
            public int attempts;
            public double nextAt;
            public bool inFlight;
        }

        readonly CommerceConfig config;
        readonly ISkinStore store;
        readonly IReceiptVerifier verifier;
        readonly ICommercePersister persister;
        readonly Func<double> clock;

        CommerceState state;
        readonly Dictionary<string, SkinProductInfo> products = new Dictionary<string, SkinProductInfo>();
        readonly List<PendingEntry> queue = new List<PendingEntry>();
        readonly HashSet<string> rejectedTokens = new HashSet<string>();
        /// <summary>이번 세션에 이미 서버 재확인을 시작한 보유 구매 토큰(세션당 1회, "구매 복원"이 비운다).</summary>
        readonly HashSet<string> revalidatedTokens = new HashSet<string>();
        readonly HashSet<string> deferred = new HashSet<string>();
        readonly Dictionary<string, string> messages = new Dictionary<string, string>();
        string purchasingProduct;

        public SkinStoreStatus Status { get; private set; }
        public string StoreMessage { get; private set; }
        public string GlobalMessage { get; private set; }
        public event Action Changed;

        public SkinCommerce(CommerceConfig config, ISkinStore store, IReceiptVerifier verifier, ICommercePersister persister, Func<double> clock)
        {
            this.config = config ?? CommerceConfig.Disabled();
            this.store = store ?? new UnavailableSkinStore();
            this.verifier = verifier;
            this.persister = persister;
            this.clock = clock ?? (() => Time.realtimeSinceStartupAsDouble);
            Status = PurchasesEnabled ? SkinStoreStatus.NotStarted : SkinStoreStatus.Disabled;
            StoreMessage = !this.store.SupportsPurchasing ? this.store.UnsupportedMessage : !this.config.PurchasesEnabled ? MsgDisabled : null;
        }

        /// <summary>결제 가능 구성(설정 켜짐 + https + 결제 지원 스토어).</summary>
        public bool PurchasesEnabled => config.PurchasesEnabled && store.SupportsPurchasing;
        public string StorefrontId => store.StorefrontId;

        public CommerceState State
        {
            get
            {
                if (state == null)
                {
                    state = persister?.Load() ?? new CommerceState();
                    state.Normalize();
                }
                return state;
            }
        }

        public int PendingCount => queue.Count;

        // ── 시작/재시도 ──────────────────────────────────────────

        /// <summary>스토어 연결(결제 가능 구성일 때만). Google Play는 연결 후 구매 내역을 자동 재조회 → 미승인 구매 처리.</summary>
        public void Start()
        {
            if (!PurchasesEnabled) { Status = SkinStoreStatus.Disabled; Notify(); return; }
            if (Status == SkinStoreStatus.Connecting || Status == SkinStoreStatus.Ready) return;
            Status = SkinStoreStatus.Connecting;
            StoreMessage = null;
            Notify();
            store.Initialize(SkinCatalog.ProductIds, this);
        }

        /// <summary>실제 시간 경과마다(CommerceRunner) — 백오프 시각이 된 보류 구매를 다시 검증.</summary>
        public void Tick() => ProcessDue();

        /// <summary>상점 열기/앱 복귀: 끊긴 스토어 재연결 + 보류 구매 즉시 재시도.</summary>
        public void RetryNow()
        {
            if (!PurchasesEnabled) return;
            if (Status == SkinStoreStatus.Unavailable || Status == SkinStoreStatus.NotStarted) { Status = SkinStoreStatus.NotStarted; Start(); }
            double now = clock();
            foreach (var e in queue) if (!e.inFlight) e.nextAt = now;
            ProcessDue();
        }

        /// <summary>"구매 복원" — 스토어 구매 내역 재조회.</summary>
        public bool Restore()
        {
            if (!PurchasesEnabled) { GlobalMessage = StoreMessage ?? MsgDisabled; Notify(); return false; }
            if (Status != SkinStoreStatus.Ready) { RetryNow(); GlobalMessage = MsgStoreUnavailable; Notify(); return false; }
            GlobalMessage = MsgRestoring;
            revalidatedTokens.Clear(); // 명시적 복원 = 보유 구매도 서버에 다시 확인
            rejectedTokens.Clear();
            store.FetchPurchases();
            RetryNow();
            Notify();
            return true;
        }

        // ── 조회 ───────────────────────────────────────────────

        /// <summary>게임 소유 = 이 스토어프런트 현금 구매 기록 또는 72 판매 코인 구매(drinks.json, 영수증 기록과 분리 저장).</summary>
        public bool IsOwned(string productId) => State.IsOwned(StorefrontId, productId) || DrinkLab.CoinOwns(productId);
        public bool IsCashOwned(string productId) => State.IsOwned(StorefrontId, productId);
        public bool IsEquipped(string productId)
        {
            var def = SkinCatalog.Find(productId);
            return def != null && State.EquippedFor(def.job) == productId;
        }
        public bool IsDeferred(string productId) => deferred.Contains(productId);
        public bool IsProcessing(string productId)
        {
            foreach (var e in queue) if (e.purchase.ProductId == productId) return true;
            return purchasingProduct == productId;
        }

        /// <summary>표시할 현지화 가격. 모르면 null(코드에 가격 없음).</summary>
        public string PriceText(string productId)
        {
            if (!PurchasesEnabled || Status != SkinStoreStatus.Ready) return null;
            return products.TryGetValue(productId, out var p) && !string.IsNullOrEmpty(p.LocalizedPrice) ? p.LocalizedPrice : null;
        }

        public bool CanBuy(string productId) =>
            PurchasesEnabled && Status == SkinStoreStatus.Ready && SkinCatalog.IsKnown(productId) && !IsOwned(productId) &&
            !IsProcessing(productId) && !IsDeferred(productId) &&
            products.TryGetValue(productId, out var p) && p.AvailableToPurchase && !string.IsNullOrEmpty(p.LocalizedPrice);

        public string MessageFor(string productId) => messages.TryGetValue(productId, out var m) ? m : null;

        /// <summary>구매 버튼 아래 상태 문구.</summary>
        public string BuyStateText(string productId)
        {
            if (!store.SupportsPurchasing) return store.UnsupportedMessage;
            if (!config.PurchasesEnabled) return MsgDisabled;
            if (IsOwned(productId)) return "보유 중";
            if (IsDeferred(productId)) return MsgDeferred;
            if (IsProcessing(productId)) return MsgVerifying;
            if (Status == SkinStoreStatus.Connecting || Status == SkinStoreStatus.NotStarted) return "스토어 연결 중…";
            if (Status != SkinStoreStatus.Ready) return StoreMessage ?? MsgStoreUnavailable;
            return PriceText(productId) ?? MsgPriceUnavailable;
        }

        // ── 구매/장착 ───────────────────────────────────────────

        public bool Buy(string productId)
        {
            string block = !PurchasesEnabled ? (store.SupportsPurchasing ? MsgDisabled : store.UnsupportedMessage)
                : !SkinCatalog.IsKnown(productId) ? MsgProductUnavailable
                : IsOwned(productId) ? MsgAlreadyOwned
                : Status != SkinStoreStatus.Ready ? MsgStoreUnavailable
                : IsProcessing(productId) ? MsgPendingInProgress
                : IsDeferred(productId) ? MsgDeferred
                : !CanBuy(productId) ? MsgProductUnavailable : null;
            if (block != null)
            {
                SetMessage(productId, block);
                return false;
            }
            purchasingProduct = productId;
            SetMessage(productId, null);
            store.Purchase(productId);
            return true;
        }

        /// <summary>장착(소유한 스킨만). 아트가 없어도 장착은 되고 런에서는 기본 아트로 떨어진다.</summary>
        public bool Equip(string productId)
        {
            var def = SkinCatalog.Find(productId);
            if (def == null || !IsOwned(productId)) return false;
            return SaveEquip(def.job, productId);
        }

        public bool Unequip(Job job) => SaveEquip(job, null);

        bool SaveEquip(Job job, string productId)
        {
            var next = State.Clone();
            next.SetEquipped(job, productId);
            if (persister != null && !persister.Save(next))
            {
                GlobalMessage = MsgEquipSaveFailed;
                Notify();
                return false;
            }
            state = next;
            GlobalMessage = null;
            Notify();
            return true;
        }

        /// <summary>이번 런 직업에 입힐 스킨(없으면 null).</summary>
        public SkinDefinition ResolveForRun(Job job, Func<SkinDefinition, bool> artExists) =>
            SkinCatalog.ResolveEquipped(job, State, StorefrontId, artExists);

        // ── 스토어 콜백 ─────────────────────────────────────────

        public void OnStoreReady(IReadOnlyList<SkinProductInfo> list)
        {
            products.Clear();
            if (list != null)
                foreach (var p in list)
                    if (p != null && !string.IsNullOrEmpty(p.ProductId)) products[p.ProductId] = p;
            Status = SkinStoreStatus.Ready;
            StoreMessage = null;
            Notify();
            ProcessDue();
        }

        public void OnStoreUnavailable(string message)
        {
            Status = PurchasesEnabled ? SkinStoreStatus.Unavailable : SkinStoreStatus.Disabled;
            StoreMessage = string.IsNullOrEmpty(message) ? MsgStoreUnavailable : message;
            if (purchasingProduct != null) { SetMessage(purchasingProduct, StoreMessage); purchasingProduct = null; }
            Notify();
        }

        public void OnPurchasePending(StorePurchase p) => Enqueue(p, true);

        public void OnPurchaseAlreadyConfirmed(StorePurchase p)
        {
            // 이미 승인된 구매(승인 호출 없음). 미보유면 검증 후 지급(복원). 보유 중이어도 세션마다 1회 + "구매 복원" 때
            // 서버에 다시 확인한다 — verified=false(환불 등)면 소유 해제·장착 해제·저장, 네트워크 오류면 캐시 유지.
            if (p == null || !SkinCatalog.IsKnown(p.ProductId) || string.IsNullOrEmpty(p.PurchaseToken))
            {
                Enqueue(p, false); // 토큰 없음/알 수 없는 상품 — Enqueue가 경고만 남기고 무시
                return;
            }
            if (IsOwned(p.ProductId))
            {
                if (revalidatedTokens.Contains(p.PurchaseToken)) return; // 이번 세션에 이미 재확인
                revalidatedTokens.Add(p.PurchaseToken);
            }
            Enqueue(p, false);
        }

        public void OnPurchaseDeferred(string productId)
        {
            if (string.IsNullOrEmpty(productId)) return;
            deferred.Add(productId);
            if (purchasingProduct == productId) purchasingProduct = null;
            SetMessage(productId, MsgDeferred);
        }

        public void OnPurchaseFailed(string productId, StoreFailure reason, string detail)
        {
            if (purchasingProduct == productId || productId == null) purchasingProduct = null;
            Debug.Log($"[Commerce] 구매 실패 {productId}: {reason}"); // 스토어 상세 문구는 로그에 남기지 않는다
            string msg = reason switch
            {
                StoreFailure.Cancelled => MsgCancelled,
                StoreFailure.AlreadyOwned => MsgAlreadyOwned,
                StoreFailure.ProductUnavailable => MsgProductUnavailable,
                StoreFailure.StoreUnavailable => MsgStoreUnavailable,
                StoreFailure.PaymentDeclined => MsgDeclined,
                StoreFailure.ExistingPending => MsgPendingInProgress,
                _ => MsgFailed,
            };
            if (productId != null) SetMessage(productId, msg);
            else { GlobalMessage = msg; Notify(); }
            // 이미 보유(Google ITEM_ALREADY_OWNED) — 기록이 없으면 구매 내역을 다시 받아 복원 경로로 처리.
            if (reason == StoreFailure.AlreadyOwned && productId != null && !IsOwned(productId) && Status == SkinStoreStatus.Ready)
                store.FetchPurchases();
        }

        public void OnConfirmFinished(string productId, bool success)
        {
            if (!success) Debug.LogWarning($"[Commerce] 스토어 승인 실패 {productId} — 다음 구매 내역 조회 때 다시 승인한다(소유 기록은 이미 저장됨)");
        }

        // ── 내부 ───────────────────────────────────────────────

        void Enqueue(StorePurchase p, bool needsConfirm)
        {
            if (p == null) return;
            if (purchasingProduct == p.ProductId) purchasingProduct = null;
            if (!string.IsNullOrEmpty(p.ProductId)) deferred.Remove(p.ProductId);
            if (!PurchasesEnabled)
            {
                // 결제 꺼진 구성에서는 처리하지 않는다(승인 없음 → 스토어에 그대로 남는다).
                Debug.Log($"[Commerce] 결제 비활성 — 보류 구매 {p.ProductId} 처리 보류");
                return;
            }
            if (string.IsNullOrEmpty(p.PurchaseToken) || !SkinCatalog.IsKnown(p.ProductId))
            {
                Debug.LogWarning($"[Commerce] 알 수 없는 구매(상품 {p.ProductId}, 토큰 {(string.IsNullOrEmpty(p.PurchaseToken) ? "없음" : "있음")}) — 지급·승인 안 함");
                return;
            }
            if (rejectedTokens.Contains(p.PurchaseToken)) return;
            // 이미 저장된 토큰이어도 건너뛰지 않고 서버에 다시 확인한다(환불·취소 재검증). 확인되면 승인만 다시.
            foreach (var e in queue)
            {
                if (e.purchase.PurchaseToken == p.PurchaseToken)
                {
                    e.purchase = p; // 최신 핸들로 교체
                    e.needsConfirm |= needsConfirm;
                    return;
                }
            }
            queue.Add(new PendingEntry { purchase = p, needsConfirm = needsConfirm, nextAt = clock() });
            SetMessage(p.ProductId, MsgVerifying);
            ProcessDue();
        }

        void ProcessDue()
        {
            if (!PurchasesEnabled || verifier == null) return;
            double now = clock();
            foreach (var e in queue.ToArray())
                if (!e.inFlight && e.nextAt <= now && queue.Contains(e)) Attempt(e);
        }

        void Attempt(PendingEntry e)
        {
            e.inFlight = true;
            var req = new VerifyRequest
            {
                productId = e.purchase.ProductId,
                purchaseToken = e.purchase.PurchaseToken,
                installationId = State.installationId,
            };
            verifier.Verify(req, outcome => OnVerified(e, outcome));
        }

        void OnVerified(PendingEntry e, VerifyOutcome outcome)
        {
            e.inFlight = false;
            if (!queue.Contains(e)) return;
            var p = e.purchase;
            bool wasOwned = IsOwned(p.ProductId);
            switch (outcome.Result)
            {
                case VerifyResult.Rejected:
                    queue.Remove(e);
                    rejectedTokens.Add(p.PurchaseToken);
                    Debug.LogWarning($"[Commerce] 검증 거부 {p.ProductId}: {outcome.Detail} — 지급·승인 안 함");
                    if (wasOwned) Revoke(p.ProductId);
                    SetMessage(p.ProductId, wasOwned ? MsgRevoked : MsgRejected);
                    return;
                case VerifyResult.Error:
                    if (wasOwned && !e.needsConfirm)
                    {
                        // 보유 재확인 중 오류 — 캐시된 소유 유지, 다음 세션/구매 복원 때 다시 확인.
                        queue.Remove(e);
                        Debug.LogWarning($"[Commerce] 보유 재확인 실패 {p.ProductId}: {outcome.Detail} — 기존 소유 유지");
                        SetMessage(p.ProductId, null);
                        return;
                    }
                    Debug.LogWarning($"[Commerce] 검증 오류 {p.ProductId}: {outcome.Detail} — 재시도 예정");
                    Backoff(e);
                    SetMessage(p.ProductId, MsgRetryLater);
                    return;
            }

            // 검증됨 → 먼저 디스크에 소유 기록 → 성공했을 때만 스토어 승인.
            var next = State.Clone();
            next.Grant(StorefrontId, p.ProductId, CommerceState.HashToken(p.PurchaseToken));
            bool saved = persister == null || persister.Save(next);
            if (!saved)
            {
                Debug.LogWarning($"[Commerce] 소유 기록 저장 실패 {p.ProductId} — 승인하지 않고 재시도 예정");
                Backoff(e);
                SetMessage(p.ProductId, MsgSaveFailed);
                return;
            }
            state = next;
            queue.Remove(e);
            if (e.needsConfirm) store.Confirm(p);
            SetMessage(p.ProductId, wasOwned ? null : MsgPurchased);
        }

        /// <summary>서버가 거부한 보유 스킨: 이 스토어프런트 소유 기록 삭제 + 장착 해제 + 저장. 저장이 실패해도 이번 세션
        /// 메모리에서는 해제한다(다음 실행 때 다시 재확인 대상).</summary>
        void Revoke(string productId)
        {
            var def = SkinCatalog.Find(productId);
            var next = State.Clone();
            next.owned.RemoveAll(o => o != null && o.storefront == StorefrontId && o.productId == productId);
            if (def != null && next.EquippedFor(def.job) == productId) next.SetEquipped(def.job, null);
            if (persister != null && !persister.Save(next))
                Debug.LogWarning($"[Commerce] 소유 해제 저장 실패 {productId} — 이번 세션만 해제");
            state = next;
            Debug.LogWarning($"[Commerce] 서버 거부로 소유 해제 {productId}");
        }

        void Backoff(PendingEntry e)
        {
            e.attempts++;
            double delay = Math.Min(RetryMaxSeconds, RetryBaseSeconds * Math.Pow(2, Math.Max(0, e.attempts - 1)));
            e.nextAt = clock() + delay;
        }

        void SetMessage(string productId, string msg)
        {
            if (productId != null)
            {
                if (msg == null) messages.Remove(productId);
                else messages[productId] = msg;
            }
            Notify();
        }

        void Notify() => Changed?.Invoke();

        // ── 공용 인스턴스 ───────────────────────────────────────

        static SkinCommerce shared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => shared = null;

        /// <summary>게임 전체 인스턴스(Resources 설정 + 플랫폼 스토어 + HTTP 검증 + commerce.json). 스토어 연결은 Start()에서.</summary>
        public static SkinCommerce Shared
        {
            get
            {
                if (shared == null)
                {
                    var config = CommerceConfig.LoadFromResources();
                    shared = new SkinCommerce(config, CommercePlatform.CreateStore(), new HttpReceiptVerifier(config),
                        new FileCommercePersister(FileCommercePersister.DefaultPath), null);
                }
                return shared;
            }
        }

        /// <summary>테스트 전용 — 공용 인스턴스 교체(null이면 다음 접근 때 새로 만든다).</summary>
        public static void SetSharedForTests(SkinCommerce value) => shared = value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (!Application.isPlaying) return;
            if (SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return; // 테스트 러너에서는 자동 시작 안 함
            var go = new GameObject("SkinCommerceRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SkinCommerceRunner>();
        }
    }

    /// <summary>공용 SkinCommerce를 다음 프레임(LaunchOptions 경로 적용 뒤)에 시작하고 재시도 시계를 돌린다.
    /// 앱 복귀(OnApplicationPause(false)) 때 보류 구매를 즉시 재시도.</summary>
    public class SkinCommerceRunner : MonoBehaviour
    {
        void Start() => SkinCommerce.Shared.Start();
        void Update() => SkinCommerce.Shared.Tick();

        void OnApplicationPause(bool paused)
        {
            if (!paused) SkinCommerce.Shared.RetryNow();
        }
    }
}
