using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CCT.Tests
{
    // ── 테스트 전용 가짜(이 테스트 어셈블리에만 존재 — 런타임에서 선택 불가) ─────────────────

    class CommerceLog
    {
        public readonly List<string> Events = new List<string>();
    }

    class FakeSkinStore : ISkinStore
    {
        readonly CommerceLog log;
        public ISkinStoreListener Listener;
        public int InitializeCalls, FetchCalls;
        public readonly List<string> Purchases = new List<string>();
        public readonly List<StorePurchase> Confirms = new List<StorePurchase>();

        public FakeSkinStore(CommerceLog log) => this.log = log;
        public string StorefrontId => Storefronts.GooglePlay;
        public bool SupportsPurchasing => true;
        public string UnsupportedMessage => null;
        public readonly List<string> RegisteredIds = new List<string>();
        public void Initialize(IReadOnlyList<string> productIds, ISkinStoreListener listener) { InitializeCalls++; Listener = listener; RegisteredIds.AddRange(productIds); }
        public void Purchase(string productId) => Purchases.Add(productId);
        public void Confirm(StorePurchase purchase) { Confirms.Add(purchase); log.Events.Add("confirm:" + purchase.ProductId); }
        public void FetchPurchases() => FetchCalls++;

        public void Ready(string price = "₩1,000") => Listener.OnStoreReady(SkinCatalog.All.Where(s => s.paid).Select(s =>
            new SkinProductInfo { ProductId = s.productId, LocalizedPrice = price, AvailableToPurchase = true }).ToList());
    }

    class FakeVerifier : IReceiptVerifier
    {
        public VerifyResult Next = VerifyResult.Verified;
        public string ResponseProductId; // null이면 요청 productId 그대로
        public readonly List<VerifyRequest> Requests = new List<VerifyRequest>();
        public void Verify(VerifyRequest request, Action<VerifyOutcome> done)
        {
            Requests.Add(request);
            if (Next == VerifyResult.Error) { done(VerifyOutcome.Of(VerifyResult.Error, "fake network")); return; }
            var json = JsonUtility.ToJson(new VerifyResponse
                { verified = Next == VerifyResult.Verified, productId = ResponseProductId ?? request.productId });
            done(ReceiptVerification.ParseResponse(json, request.productId));
        }
    }

    class MemoryPersister : ICommercePersister
    {
        readonly CommerceLog log;
        public bool Fail;
        public string Json;
        public int Saves;
        public MemoryPersister(CommerceLog log) => this.log = log;
        public CommerceState Load()
        {
            var s = string.IsNullOrEmpty(Json) ? new CommerceState() : JsonUtility.FromJson<CommerceState>(Json);
            s.Normalize();
            return s;
        }
        public bool Save(CommerceState state)
        {
            if (Fail) { log.Events.Add("save-fail"); return false; }
            Saves++;
            Json = JsonUtility.ToJson(state);
            log.Events.Add("save");
            return true;
        }
    }

    public class CommerceTests
    {
        const string Https = "https://verify.example.com/verify";
        const string Bartender = SkinCatalog.BartenderMidnight;

        CommerceLog log;
        FakeSkinStore store;
        FakeVerifier verifier;
        MemoryPersister persister;
        double now;

        SkinCommerce Make(CommerceConfig config = null)
        {
            log = new CommerceLog();
            store = new FakeSkinStore(log);
            verifier = new FakeVerifier();
            persister = new MemoryPersister(log);
            now = 100d;
            return new SkinCommerce(config ?? new CommerceConfig { enabled = true, verifierEndpoint = Https }, store, verifier, persister, () => now);
        }

        static StorePurchase Purchase(string productId = Bartender, string token = "tok-1") =>
            new StorePurchase { ProductId = productId, PurchaseToken = token, Handle = new object() };

        // ── 설정 ──

        [Test]
        public void Config_EnabledWithHttps_EnablesPurchases()
        {
            var c = CommerceConfig.Parse("{\"enabled\":true,\"verifierEndpoint\":\" https://a.example.com/verify \"}");
            Assert.IsTrue(c.PurchasesEnabled);
            Assert.AreEqual("https://a.example.com/verify", c.verifierEndpoint);
            Assert.AreEqual(CommerceConfig.DefaultTimeoutSeconds, CommerceConfig.Parse("{\"enabled\":true,\"requestTimeoutSeconds\":0}").TimeoutSeconds);
        }

        [TestCase("{\"enabled\":false,\"verifierEndpoint\":\"https://a.example.com\"}")]
        [TestCase("{\"enabled\":true,\"verifierEndpoint\":\"\"}")]
        [TestCase("{\"enabled\":true,\"verifierEndpoint\":\"http://a.example.com/verify\"}")]
        [TestCase("{\"enabled\":true,\"verifierEndpoint\":\"not a url\"}")]
        [TestCase("{\"enabled\":true}")]
        [TestCase("{broken")]
        [TestCase("")]
        [TestCase(null)]
        public void Config_MissingOrNonHttpsOrDisabled_DisablesPurchases(string json)
        {
            Assert.IsFalse(CommerceConfig.Parse(json).PurchasesEnabled);
        }

        [Test]
        public void ShippedConfig_IsDisabledUntilConfigured()
        {
            var text = Resources.Load<TextAsset>(CommerceConfig.ResourcePath);
            Assert.IsNotNull(text, "Resources/Commerce/commerce_config.json 없음");
            Assert.IsFalse(CommerceConfig.Parse(text.text).PurchasesEnabled, "서버 배포 전에는 결제가 꺼져 있어야 한다");
        }

        [Test]
        public void DisabledConfig_DoesNotConnectStore_BuyBlocked_PendingNotConfirmed()
        {
            var c = Make(new CommerceConfig { enabled = false, verifierEndpoint = Https });
            c.Start();
            Assert.AreEqual(0, store.InitializeCalls);
            Assert.AreEqual(SkinStoreStatus.Disabled, c.Status);
            Assert.IsFalse(c.Buy(Bartender));
            Assert.AreEqual(SkinCommerce.MsgDisabled, c.BuyStateText(Bartender));
            Assert.IsNull(c.PriceText(Bartender));
            c.OnPurchasePending(Purchase());
            Assert.IsEmpty(store.Confirms);
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsEmpty(verifier.Requests);
        }

        [Test]
        public void UnsupportedPlatformStore_ShowsPlatformMessage_AndBlocksPurchase()
        {
            var c = new SkinCommerce(new CommerceConfig { enabled = true, verifierEndpoint = Https }, new UnavailableSkinStore(),
                new FakeVerifier(), new MemoryPersister(new CommerceLog()), () => 0d);
            c.Start();
            Assert.IsFalse(c.PurchasesEnabled);
            Assert.AreEqual(UnavailableSkinStore.Message, c.BuyStateText(Bartender));
            Assert.IsFalse(c.Buy(Bartender));
            Assert.IsFalse(c.Restore());
        }

        // ── 검증 응답 ──

        [Test]
        public void ParseResponse_VerifiedAndMatching_IsVerified()
        {
            Assert.AreEqual(VerifyResult.Verified,
                ReceiptVerification.ParseResponse("{\"verified\":true,\"productId\":\"skin_tea_jade\"}", "skin_tea_jade").Result);
        }

        [Test]
        public void ParseResponse_RejectedOrMismatch_IsRejected()
        {
            Assert.AreEqual(VerifyResult.Rejected,
                ReceiptVerification.ParseResponse("{\"verified\":false,\"productId\":\"skin_tea_jade\"}", "skin_tea_jade").Result);
            Assert.AreEqual(VerifyResult.Rejected,
                ReceiptVerification.ParseResponse("{\"verified\":true,\"productId\":\"skin_bartender_midnight\"}", "skin_tea_jade").Result);
            Assert.AreEqual(VerifyResult.Rejected,
                ReceiptVerification.ParseResponse("{\"verified\":true}", "skin_tea_jade").Result);
        }

        [Test]
        public void ParseResponse_MalformedOrMissingField_IsErrorForRetry()
        {
            Assert.AreEqual(VerifyResult.Error, ReceiptVerification.ParseResponse("{\"verified\":tru", "x").Result);
            Assert.AreEqual(VerifyResult.Error, ReceiptVerification.ParseResponse("{\"productId\":\"x\"}", "x").Result);
            Assert.AreEqual(VerifyResult.Error, ReceiptVerification.ParseResponse("", "x").Result);
        }

        [Test]
        public void FromHttp_ServerAndNetworkErrors_AreRetryable()
        {
            Assert.AreEqual(VerifyResult.Error, ReceiptVerification.FromHttp(500, "", "x", false).Result);
            Assert.AreEqual(VerifyResult.Error, ReceiptVerification.FromHttp(0, null, "x", true).Result);
            Assert.AreEqual(VerifyResult.Error, ReceiptVerification.FromHttp(429, "", "x", false).Result);
            Assert.AreEqual(VerifyResult.Rejected, ReceiptVerification.FromHttp(400, "{\"verified\":false,\"productId\":\"x\"}", "x", false).Result);
            Assert.AreEqual(VerifyResult.Verified, ReceiptVerification.FromHttp(200, "{\"verified\":true,\"productId\":\"x\"}", "x", false).Result);
        }

        // ── 지급/승인 순서 ──

        [Test]
        public void VerifiedPurchase_PersistsBeforeConfirm()
        {
            var c = Make();
            c.Start();
            store.Ready();
            Assert.IsTrue(c.Buy(Bartender));
            CollectionAssert.AreEqual(new[] { Bartender }, store.Purchases);
            c.OnPurchasePending(Purchase());

            CollectionAssert.AreEqual(new[] { "save", "confirm:" + Bartender }, log.Events);
            Assert.IsTrue(c.IsOwned(Bartender));
            Assert.AreEqual(0, c.PendingCount);
            var req = verifier.Requests.Single();
            Assert.AreEqual("tok-1", req.purchaseToken);
            Assert.AreEqual(Bartender, req.productId);
            Assert.IsFalse(string.IsNullOrEmpty(req.installationId));
            StringAssert.DoesNotContain("tok-1", persister.Json, "원문 토큰은 저장하지 않는다(해시만)");
        }

        [Test]
        public void ServerRejected_NoGrant_NoConfirm_NotRetried()
        {
            var c = Make();
            c.Start();
            store.Ready();
            verifier.Next = VerifyResult.Rejected;
            c.OnPurchasePending(Purchase());
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsEmpty(store.Confirms);
            Assert.AreEqual(0, persister.Saves);
            Assert.AreEqual(SkinCommerce.MsgRejected, c.MessageFor(Bartender));

            c.OnPurchasePending(Purchase()); // 같은 세션 같은 토큰 — 다시 검증하지 않는다
            Assert.AreEqual(1, verifier.Requests.Count);
        }

        [Test]
        public void ProductIdMismatch_NoGrant_NoConfirm()
        {
            var c = Make();
            c.Start();
            store.Ready();
            verifier.ResponseProductId = SkinCatalog.TeaJade;
            c.OnPurchasePending(Purchase());
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsFalse(c.IsOwned(SkinCatalog.TeaJade));
            Assert.IsEmpty(store.Confirms);
        }

        [Test]
        public void PersistFailure_NoConfirm_StaysPending_RetryLaterSucceeds()
        {
            var c = Make();
            c.Start();
            store.Ready();
            persister.Fail = true;
            c.OnPurchasePending(Purchase());

            Assert.IsEmpty(store.Confirms, "저장 실패 시 승인하면 안 된다");
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.AreEqual(1, c.PendingCount);
            Assert.AreEqual(SkinCommerce.MsgSaveFailed, c.MessageFor(Bartender));

            persister.Fail = false;
            c.Tick(); // 백오프 전 — 아직 재시도 안 함
            Assert.AreEqual(1, verifier.Requests.Count);
            now += SkinCommerce.RetryBaseSeconds + 0.1;
            c.Tick();

            Assert.AreEqual(2, verifier.Requests.Count);
            Assert.IsTrue(c.IsOwned(Bartender));
            Assert.AreEqual(1, store.Confirms.Count);
            Assert.AreEqual(0, c.PendingCount);
            CollectionAssert.AreEqual(new[] { "save-fail", "save", "confirm:" + Bartender }, log.Events);
        }

        [Test]
        public void NetworkError_NoGrant_BackoffGrows_RetryNowSucceeds()
        {
            var c = Make();
            c.Start();
            store.Ready();
            verifier.Next = VerifyResult.Error;
            c.OnPurchasePending(Purchase());
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsEmpty(store.Confirms);
            Assert.AreEqual(SkinCommerce.MsgRetryLater, c.MessageFor(Bartender));

            now += SkinCommerce.RetryBaseSeconds + 0.1;
            c.Tick(); // 2번째 실패 → 다음은 10초 뒤
            Assert.AreEqual(2, verifier.Requests.Count);
            now += SkinCommerce.RetryBaseSeconds + 0.1;
            c.Tick();
            Assert.AreEqual(2, verifier.Requests.Count, "백오프가 늘어나야 한다");

            verifier.Next = VerifyResult.Verified;
            c.RetryNow(); // 상점 열기/앱 복귀
            Assert.IsTrue(c.IsOwned(Bartender));
            Assert.AreEqual(1, store.Confirms.Count);
        }

        [Test]
        public void AlreadyPersistedToken_RedeliveredPending_ReverifiesThenConfirmsAgain()
        {
            var c = Make();
            c.Start();
            store.Ready();
            c.OnPurchasePending(Purchase());
            Assert.AreEqual(1, verifier.Requests.Count);
            c.OnPurchasePending(Purchase()); // 이전 승인이 스토어에 반영되지 않아 다시 온 경우 — 저장된 토큰이어도 서버 재확인
            Assert.AreEqual(2, verifier.Requests.Count);
            Assert.AreEqual(2, store.Confirms.Count);
            Assert.IsTrue(c.IsOwned(Bartender));
        }

        [TestCase(null)]
        [TestCase("")]
        public void MissingPurchaseToken_NoVerify_NoGrant_NoConfirm(string token)
        {
            var c = Make();
            c.Start();
            store.Ready();
            c.OnPurchasePending(Purchase(Bartender, token));
            c.OnPurchaseAlreadyConfirmed(Purchase(Bartender, token));
            Assert.IsEmpty(verifier.Requests);
            Assert.IsEmpty(store.Confirms);
            Assert.IsFalse(c.IsOwned(Bartender));
        }

        SkinCommerce OwnedAndEquipped()
        {
            var c = Make();
            c.Start();
            store.Ready();
            c.OnPurchasePending(Purchase());
            Assert.IsTrue(c.Equip(Bartender));
            return c;
        }

        [Test]
        public void OwnedRestore_ServerRejects_RevokesPersistsAndUnequips()
        {
            var c = OwnedAndEquipped();
            verifier.Next = VerifyResult.Rejected;
            Assert.IsTrue(c.Restore());
            c.OnPurchaseAlreadyConfirmed(Purchase());

            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsFalse(c.IsEquipped(Bartender));
            Assert.AreEqual(SkinCommerce.MsgRevoked, c.MessageFor(Bartender));
            var disk = persister.Load();
            Assert.IsFalse(disk.IsOwned(Storefronts.GooglePlay, Bartender), "해제가 디스크에 저장돼야 한다");
            Assert.IsNull(disk.EquippedFor(Job.Bartender));
            Assert.AreEqual(1, store.Confirms.Count, "재확인은 승인 호출을 하지 않는다");
        }

        [Test]
        public void OwnedRestore_NetworkError_KeepsCachedOwnership()
        {
            var c = OwnedAndEquipped();
            verifier.Next = VerifyResult.Error;
            c.Restore();
            c.OnPurchaseAlreadyConfirmed(Purchase());

            Assert.IsTrue(c.IsOwned(Bartender));
            Assert.IsTrue(c.IsEquipped(Bartender));
            Assert.AreEqual(0, c.PendingCount, "보유 재확인 오류는 무한 재시도하지 않는다(다음 세션/복원 때)");
            Assert.IsTrue(persister.Load().IsOwned(Storefronts.GooglePlay, Bartender));
        }

        [Test]
        public void NewSession_RevalidatesOwnedConfirmedOrderOnce_RestoreAgain()
        {
            OwnedAndEquipped();
            int before = verifier.Requests.Count;

            // 새 실행: 같은 디스크 상태로 새 인스턴스
            var session2 = new SkinCommerce(new CommerceConfig { enabled = true, verifierEndpoint = Https }, store, verifier, persister, () => now);
            session2.Start();
            store.Ready();
            Assert.IsTrue(session2.IsOwned(Bartender));
            session2.OnPurchaseAlreadyConfirmed(Purchase());
            session2.OnPurchaseAlreadyConfirmed(Purchase()); // 같은 세션 두 번째 조회 — 다시 확인하지 않음
            Assert.AreEqual(before + 1, verifier.Requests.Count);

            session2.Restore();
            session2.OnPurchaseAlreadyConfirmed(Purchase());
            Assert.AreEqual(before + 2, verifier.Requests.Count, "구매 복원은 다시 확인");
            Assert.IsTrue(session2.IsOwned(Bartender));
        }

        [Test]
        public void Deferred_ShowsWaiting_GrantsOnlyWhenPurchasedArrives()
        {
            var c = Make();
            c.Start();
            store.Ready();
            c.Buy(Bartender);
            c.OnPurchaseDeferred(Bartender);
            Assert.AreEqual(SkinCommerce.MsgDeferred, c.BuyStateText(Bartender));
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsFalse(c.CanBuy(Bartender));
            Assert.IsEmpty(verifier.Requests);
            Assert.IsEmpty(store.Confirms);

            c.OnPurchasePending(Purchase());
            Assert.IsTrue(c.IsOwned(Bartender));
            Assert.IsFalse(c.IsDeferred(Bartender));
            Assert.AreEqual(1, store.Confirms.Count);
        }

        [Test]
        public void Restore_AlreadyConfirmedOrder_VerifiesAndGrants_WithoutConfirm()
        {
            var c = Make();
            c.Start();
            store.Ready();
            Assert.IsTrue(c.Restore());
            Assert.AreEqual(1, store.FetchCalls);
            c.OnPurchaseAlreadyConfirmed(Purchase(SkinCatalog.TeaJade, "tok-jade"));
            Assert.IsTrue(c.IsOwned(SkinCatalog.TeaJade));
            Assert.IsEmpty(store.Confirms);
        }

        [Test]
        public void FailureMessages_CancelledAlreadyOwnedUnavailableOffline_NoGrant()
        {
            var c = Make();
            c.Start();
            Assert.IsFalse(c.Buy(Bartender), "스토어 준비 전");
            Assert.AreEqual(SkinCommerce.MsgStoreUnavailable, c.MessageFor(Bartender));

            c.OnStoreUnavailable(null);
            Assert.AreEqual(SkinStoreStatus.Unavailable, c.Status);
            Assert.IsNull(c.PriceText(Bartender));

            store.Ready();
            c.OnPurchaseFailed(Bartender, StoreFailure.Cancelled, "");
            Assert.AreEqual(SkinCommerce.MsgCancelled, c.MessageFor(Bartender));
            c.OnPurchaseFailed(Bartender, StoreFailure.ProductUnavailable, "");
            Assert.AreEqual(SkinCommerce.MsgProductUnavailable, c.MessageFor(Bartender));
            c.OnPurchaseFailed(Bartender, StoreFailure.AlreadyOwned, "");
            Assert.AreEqual(SkinCommerce.MsgAlreadyOwned, c.MessageFor(Bartender));
            Assert.AreEqual(1, store.FetchCalls, "이미 보유 응답이면 구매 내역을 다시 받아 복원한다");
            Assert.IsFalse(c.IsOwned(Bartender));
            Assert.IsEmpty(store.Confirms);
        }

        [Test]
        public void Price_ComesOnlyFromStore()
        {
            var c = Make();
            c.Start();
            Assert.IsNull(c.PriceText(Bartender));
            Assert.AreEqual("스토어 연결 중…", c.BuyStateText(Bartender));
            store.Ready("US$0.79");
            Assert.AreEqual("US$0.79", c.PriceText(Bartender));
            Assert.IsTrue(c.CanBuy(Bartender));
            store.Listener.OnStoreReady(new List<SkinProductInfo>
                { new SkinProductInfo { ProductId = Bartender, LocalizedPrice = "", AvailableToPurchase = true } });
            Assert.IsNull(c.PriceText(Bartender));
            Assert.AreEqual(SkinCommerce.MsgPriceUnavailable, c.BuyStateText(Bartender));
            Assert.IsFalse(c.CanBuy(Bartender));
        }

        // ── 장착/해석 ──

        [Test]
        public void Equip_RequiresOwnership_AndPersists()
        {
            var c = Make();
            Assert.IsFalse(c.Equip(Bartender));
            c.Start();
            store.Ready();
            c.OnPurchasePending(Purchase());
            Assert.IsTrue(c.Equip(Bartender));
            Assert.IsTrue(c.IsEquipped(Bartender));
            StringAssert.Contains(Bartender, persister.Json);
            Assert.IsTrue(c.Unequip(Job.Bartender));
            Assert.IsFalse(c.IsEquipped(Bartender));
        }

        [Test]
        public void Resolver_DefaultUnlessOwnedEquippedSameJobAndArtExists()
        {
            var s = new CommerceState();
            s.Normalize();
            var gp = Storefronts.GooglePlay;
            Func<SkinDefinition, bool> art = _ => true;

            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.Bartender, s, gp, art), "미소유·미장착");
            s.Grant(gp, Bartender, "h");
            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.Bartender, s, gp, art), "소유했지만 미장착");
            s.SetEquipped(Job.Bartender, Bartender);
            Assert.AreEqual(Bartender, SkinCatalog.ResolveEquipped(Job.Bartender, s, gp, art)?.productId);
            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.TeaSommelier, s, gp, art), "다른 직업");
            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.Bartender, s, gp, _ => false), "아트 없음 → 기본");
            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.Bartender, s, "steam", art), "다른 스토어프런트 소유는 인정 안 함");

            var unowned = new CommerceState();
            unowned.Normalize();
            unowned.SetEquipped(Job.Bartender, Bartender);
            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.Bartender, unowned, gp, art), "장착 기록만 있고 미소유");
        }

        // ── 저장 ──

        [Test]
        public void FilePersister_AtomicRoundTrip_KeepsInstallationId()
        {
            var dir = Path.Combine(Application.temporaryCachePath, "cct_commerce_" + Guid.NewGuid().ToString("N"));
            try
            {
                var path = Path.Combine(dir, FileCommercePersister.FileName);
                var p = new FileCommercePersister(path);
                var s = p.Load();
                Assert.IsTrue(Guid.TryParse(s.installationId, out _), "installationId는 임의 GUID");
                s.Grant(Storefronts.GooglePlay, Bartender, "hash");
                Assert.IsTrue(p.Save(s));
                Assert.IsTrue(p.Save(s)); // 두 번째는 File.Replace 경로(.bak 생성)
                Assert.IsFalse(File.Exists(path + ".tmp"));
                Assert.IsTrue(File.Exists(path + ".bak"));
                var again = new FileCommercePersister(path).Load();
                Assert.AreEqual(s.installationId, again.installationId);
                Assert.IsTrue(again.IsOwned(Storefronts.GooglePlay, Bartender));

                File.WriteAllText(path, "{broken");
                Assert.IsTrue(new FileCommercePersister(path).Load().IsOwned(Storefronts.GooglePlay, Bartender), "손상 시 .bak");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void DefaultPath_FollowsSaveDirectory()
        {
            var expected = Path.Combine(Path.GetDirectoryName(PermanentProgress.ActiveSavePath), FileCommercePersister.FileName);
            Assert.AreEqual(expected, FileCommercePersister.DefaultPath());
            StringAssert.DoesNotStartWith(Application.persistentDataPath, FileCommercePersister.DefaultPath(), "테스트는 실제 세이브 폴더를 쓰면 안 된다");
        }

        // ── 가짜 스토어 격리 ──

        [Test]
        public void RuntimeAssembly_HasNoFakeStore_AndEditorUsesUnavailableStore()
        {
            var runtime = typeof(ISkinStore).Assembly;
            var stores = runtime.GetTypes().Where(t => typeof(ISkinStore).IsAssignableFrom(t) && !t.IsInterface).Select(t => t.Name).ToList();
            foreach (var name in stores)
                Assert.IsTrue(name == "UnityIapSkinStore" || name == "UnavailableSkinStore", $"런타임에 예상 밖 스토어: {name}");
            Assert.IsFalse(runtime.GetTypes().Any(t => typeof(IReceiptVerifier).IsAssignableFrom(t) && !t.IsInterface && t != typeof(HttpReceiptVerifier)),
                "런타임 검증기는 HTTP뿐");
            Assert.IsInstanceOf<UnavailableSkinStore>(CommercePlatform.CreateStore());
        }

        // ── 능력치 불변 + 스프라이트 교체 ──

        static string Snapshot(PlayerStats s)
        {
            var parts = new List<string>();
            foreach (var f in typeof(PlayerStats).GetFields(BindingFlags.Public | BindingFlags.Instance))
                parts.Add(f.Name + "=" + f.GetValue(s));
            foreach (var p in typeof(PlayerStats).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (p.GetIndexParameters().Length == 0 && p.CanRead)
                    parts.Add(p.Name + "=" + p.GetValue(s));
            return string.Join(";", parts);
        }

        [Test]
        public void SkinSwap_ChangesOnlySprite_NotStatsOrRadius()
        {
            var def = SkinCatalog.Find(Bartender);
            if (!SkinArt.Exists(def)) Assert.Ignore("스킨 아트 미임포트(SkinArtImport 실행 전)");

            var plainGo = new GameObject("PlainPlayer", typeof(Rigidbody2D));
            var skinGo = new GameObject("SkinPlayer", typeof(Rigidbody2D));
            try
            {
                var plainStats = new PlayerStats();
                var skinStats = new PlayerStats();
                var plain = plainGo.AddComponent<PlayerController>();
                plain.Init(plainStats, Color.white, "bartender");
                var skinned = skinGo.AddComponent<PlayerController>();
                skinned.Init(skinStats, Color.white, "bartender");
                if (!skinned.Visual.UsesBodyArt) Assert.Ignore("기본 몸체 아트 없음");

                var original = skinned.Visual.Body.sprite;
                var swapper = SkinSpriteSwapper.Attach(skinned.Visual.Body, def);
                Assert.IsNotNull(swapper);
                var swapped = skinned.Visual.Body.sprite;
                Assert.AreEqual(original.name, swapped.name, "같은 프레임 이름");
                Assert.AreNotSame(original, swapped, "스킨 스프라이트로 교체");
                Assert.IsFalse(swapper.Apply(), "이미 스킨이면 다시 바꾸지 않는다");

                Assert.AreEqual(Snapshot(plainStats), Snapshot(skinStats), "스킨은 능력치를 바꾸지 않는다");
                Assert.AreEqual(plain.GetComponent<CircleCollider2D>().radius, skinned.GetComponent<CircleCollider2D>().radius);
                Assert.AreEqual(plain.Visual.transform.localScale, skinned.Visual.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(plainGo);
                Object.DestroyImmediate(skinGo);
            }
        }

        /// <summary>스킨 폴더가 원본 몸체의 idle/move/ranged 프레임, 티 소믈리에 활 준비(bow_idle/bow_move/bow_ranged)·
        /// 검 휘두르기 프레임 이름을 모두 덮는지 — 빠지면 그 동작 동안 기본 의상으로 돌아가 보인다.</summary>
        [Test]
        public void SkinFolders_CoverOriginalBodyFrames()
        {
            foreach (var def in SkinCatalog.All.Where(d => d.paid)) // 79 코인 스킨 10종은 CoinSkinTests(실사용 프레임 계약)에서 따로 본다
            {
                var map = SkinArt.Sprites(def);
                Assert.IsTrue(map.Count > 0, $"{def.productId}: Resources/{def.ResourceFolder} 스프라이트 없음(SkinArtImport 실행 필요)");
                var required = new List<string>();
                var art = ArtLibrary.Get(JobPreset.For(def.job).bodyArtId);
                Assert.IsNotNull(art, "원본 몸체 아트");
                foreach (var clip in new[] { "idle", "move", "ranged" })
                {
                    var c = art.Clip(clip);
                    if (c != null) required.AddRange(c.frames.Where(f => f != null).Select(f => f.name));
                }
                if (def.job == Job.TeaSommelier)
                {
                    var swing = Resources.LoadAll<Sprite>("ArtExtra/tea_sommelier_sword_swing");
                    Assert.IsNotEmpty(swing, "원본 검 휘두르기");
                    required.AddRange(swing.Select(s => s.name));
                    var bow = Resources.LoadAll<Sprite>("ArtExtra/tea_sommelier_bow_ready")
                        .Where(s => s.name.StartsWith("bow_idle_") || s.name.StartsWith("bow_move_") || s.name.StartsWith("bow_ranged_")).ToList();
                    Assert.IsNotEmpty(bow, "원본 활 준비 프레임");
                    required.AddRange(bow.Select(s => s.name));
                }
                Assert.IsNotEmpty(required);
                var missing = required.Distinct().Where(n => !map.ContainsKey(n)).ToList();
                Assert.IsEmpty(missing, $"{def.productId} 누락 프레임: {string.Join(", ", missing)}");
            }
        }

        /// <summary>교체 후 애니메이터가 프레임을 안 바꾼 다음 프레임에도 스킨이 유지되고(원본으로 되돌리지 않음),
        /// 애니메이터가 새 원본 프레임을 넣으면 다시 스킨으로 바뀐다.</summary>
        [Test]
        public void Swapper_KeepsSkinSprite_AndReswapsNewOriginalFrame()
        {
            var def = SkinCatalog.Find(Bartender);
            var art = ArtLibrary.Get("bartender");
            var idle = art?.Clip("idle");
            if (!SkinArt.Exists(def) || idle == null || idle.frames.Length < 2) Assert.Ignore("아트 없음");
            var go = new GameObject("SwapTarget");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = idle.frames[0];
                var swapper = SkinSpriteSwapper.Attach(sr, def);
                var skin0 = sr.sprite;
                Assert.AreNotSame(idle.frames[0], skin0);
                Assert.IsFalse(swapper.Apply());
                Assert.AreSame(skin0, sr.sprite, "스킨 스프라이트는 그대로 유지");
                sr.sprite = idle.frames[1]; // 애니메이터가 다음 원본 프레임을 넣음
                Assert.IsTrue(swapper.Apply());
                Assert.AreEqual(idle.frames[1].name, sr.sprite.name);
                Assert.AreNotSame(idle.frames[1], sr.sprite);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SkinArt_PreviewMatchesBaseIdleFrameName()
        {
            foreach (var def in SkinCatalog.All)
            {
                var preview = SkinArt.PreviewSprite(def);
                if (preview == null) continue; // 아트 미임포트 — 상점이 "아트 준비 중"으로 표시
                var baseIdle = SkinArt.BaseIdleSprite(def.job);
                if (baseIdle != null) Assert.AreEqual(baseIdle.name, preview.name);
            }
        }
    }
}
