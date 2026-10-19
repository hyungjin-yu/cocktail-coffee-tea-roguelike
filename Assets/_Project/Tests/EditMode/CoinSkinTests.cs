using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 79 신규 캐릭터 스킨 10종(바텐더 5·티 소믈리에 5): 판매 코인 1000 전용 — 현금 스토어(IAP)에 등록·구매·검증·복원되지 않고,
    /// 아트가 실제 사용 몸체 프레임을 모두 덮기 전에는 판매·적용하지 않는다. 기존 유료 2종·코인·장착은 그대로.
    /// </summary>
    public class CoinSkinTests
    {
        const string Https = "https://verify.example.com/verify";

        static readonly string[] BartenderNew =
        {
            SkinCatalog.BartenderRail, SkinCatalog.BartenderTropic, SkinCatalog.BartenderNoir, SkinCatalog.BartenderAlchemist, SkinCatalog.BartenderStage,
        };
        static readonly string[] TeaNew =
        {
            SkinCatalog.TeaMountain, SkinCatalog.TeaCeremony, SkinCatalog.TeaApiary, SkinCatalog.TeaGreenhouse, SkinCatalog.TeaAstronomer,
        };
        static IEnumerable<string> NewIds => BartenderNew.Concat(TeaNew);

        string dir;
        Func<string, bool> savedReady;
        CommerceLog log;
        FakeSkinStore store;
        FakeVerifier verifier;
        MemoryPersister persister;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "coinskins_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            DrinkLab.SetPathForTests(Path.Combine(dir, "drinks.json"));
            savedReady = DrinkSales.SkinArtReady;
        }

        [TearDown]
        public void TearDown()
        {
            DrinkSales.SkinArtReady = savedReady;
            DrinkLab.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { }
        }

        SkinCommerce Make()
        {
            log = new CommerceLog();
            store = new FakeSkinStore(log);
            verifier = new FakeVerifier();
            persister = new MemoryPersister(log);
            return new SkinCommerce(new CommerceConfig { enabled = true, verifierEndpoint = Https }, store, verifier, persister, () => 100d);
        }

        [Test]
        public void Catalog_ExactlyTenNewCoinSkins_FivePerJob_ExistingTwoPaidKept()
        {
            Assert.AreEqual(12, SkinCatalog.All.Length, "기존 2 + 신규 10");
            CollectionAssert.AreEquivalent(new[] { SkinCatalog.BartenderMidnight, SkinCatalog.TeaJade }, SkinCatalog.PaidProductIds, "현금 상품은 기존 2종뿐");
            CollectionAssert.AllItemsAreUnique(SkinCatalog.AllProductIds);
            foreach (var id in BartenderNew) Assert.AreEqual(Job.Bartender, SkinCatalog.Find(id)?.job, id);
            foreach (var id in TeaNew) Assert.AreEqual(Job.TeaSommelier, SkinCatalog.Find(id)?.job, id);
            CollectionAssert.AreEquivalent(NewIds, SkinCatalog.All.Where(d => !d.paid).Select(d => d.productId), "코인 스킨 = 정확히 신규 10종");
            foreach (var def in SkinCatalog.All.Where(d => !d.paid))
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.displayName));
                StringAssert.Contains("능력치", def.difference, "외형 전용 표기");
            }
            CollectionAssert.AllItemsAreUnique(SkinCatalog.All.Select(d => d.displayName), "목록에서 이름으로 구별");

            Assert.AreEqual(13, DrinkSales.All.Length, "스킨 12 + 맵 1");
            foreach (var id in NewIds)
            {
                var c = DrinkSales.Cosmetic(id);
                Assert.IsNotNull(c, id);
                Assert.AreEqual("skin", c.kind);
                Assert.AreEqual(1000, c.price, id);
            }
            Assert.AreEqual(1000, DrinkSales.Cosmetic(SkinCatalog.BartenderMidnight).price);
            Assert.AreEqual(1000, DrinkSales.Cosmetic(SkinCatalog.TeaJade).price);
            Assert.AreEqual(1500, DrinkSales.Cosmetic(DrinkSales.CafeMap).price);
            Assert.AreEqual(3, DrinkSales.Catalog.Length, "기존 판매 목록 원본은 그대로");
            foreach (var w in WeaponSkinCatalog.All) CollectionAssert.DoesNotContain(SkinCatalog.AllProductIds, w.id, "무기 스킨과 별개");
            Assert.AreEqual(4, WeaponSkinCatalog.All.Length, "무료 무기 외형 4종 유지");
        }

        [Test]
        public void Store_RegistersOnlyPaidProducts_NotCoinSkins()
        {
            var commerce = Make();
            commerce.Start();
            Assert.AreEqual(1, store.InitializeCalls);
            CollectionAssert.AreEquivalent(SkinCatalog.PaidProductIds, store.RegisteredIds);
            foreach (var id in NewIds) CollectionAssert.DoesNotContain(store.RegisteredIds, id);
        }

        [Test]
        public void CoinSkin_CashBuyVerifyRestorePaths_AllRejected()
        {
            var commerce = Make();
            commerce.Start();
            // 스토어가 혹시 코인 ID 상품 정보를 돌려줘도 가격·구매 대상이 되지 않는다.
            var infos = SkinCatalog.All.Select(s => new SkinProductInfo { ProductId = s.productId, LocalizedPrice = "₩1,000", AvailableToPurchase = true }).ToList();
            store.Listener.OnStoreReady(infos);
            foreach (var id in NewIds)
            {
                Assert.IsNull(commerce.PriceText(id), id);
                Assert.IsFalse(commerce.CanBuy(id), id);
                Assert.IsFalse(commerce.Buy(id), id);
                Assert.AreEqual(SkinCommerce.MsgCoinOnly, commerce.MessageFor(id));
                StringAssert.Contains("판매 코인 1000", commerce.BuyStateText(id));

                commerce.OnPurchasePending(new StorePurchase { ProductId = id, PurchaseToken = "tok-" + id, Handle = new object() });
                commerce.OnPurchaseAlreadyConfirmed(new StorePurchase { ProductId = id, PurchaseToken = "old-" + id, Handle = new object() });
                Assert.IsFalse(commerce.IsOwned(id), "영수증 경로로 코인 스킨 지급 없음");
            }
            Assert.IsEmpty(store.Purchases, "스토어 결제 호출 없음");
            Assert.IsEmpty(verifier.Requests, "서버 검증 호출 없음");
            Assert.IsEmpty(store.Confirms, "스토어 승인 없음");
            Assert.AreEqual(0, persister.Saves);

            // 기존 유료 스킨은 그대로 살 수 있다.
            Assert.IsTrue(commerce.CanBuy(SkinCatalog.BartenderMidnight));
            Assert.AreEqual("₩1,000", commerce.PriceText(SkinCatalog.TeaJade));
        }

        [Test]
        public void ForgedCashRecordForCoinSkin_DroppedOnNormalize_PaidRecordKept()
        {
            var s = new CommerceState();
            s.owned.Add(new OwnedSkin { storefront = Storefronts.GooglePlay, productId = SkinCatalog.TeaApiary, tokenHash = "x" });
            s.owned.Add(new OwnedSkin { storefront = Storefronts.GooglePlay, productId = SkinCatalog.TeaJade, tokenHash = "y" });
            s.Normalize();
            Assert.IsFalse(s.IsOwned(Storefronts.GooglePlay, SkinCatalog.TeaApiary), "commerce.json의 코인 스킨 현금 기록은 무시");
            Assert.IsTrue(s.IsOwned(Storefronts.GooglePlay, SkinCatalog.TeaJade), "기존 유료 소유 유지");
        }

        [Test]
        public void CoinSkin_NotSoldUntilArtComplete()
        {
            DrinkSales.SkinArtReady = id => SkinCatalog.IsPaid(id); // 신규 아트 없음
            DrinkLab.Data.coins = 5000;
            foreach (var id in NewIds)
            {
                Assert.IsFalse(DrinkSales.OnSale(DrinkSales.Cosmetic(id)), id);
                Assert.IsFalse(DrinkLab.BuyCosmetic(id, null, out var e), id);
                StringAssert.Contains("아트 준비 중", e);
            }
            Assert.AreEqual(5000, DrinkLab.Coins, "코인 차감 없음");
            Assert.IsTrue(DrinkSales.OnSale(DrinkSales.Cosmetic(SkinCatalog.BartenderMidnight)), "기존 스킨은 계속 판매");
            Assert.IsTrue(DrinkSales.OnSale(DrinkSales.Cosmetic(DrinkSales.CafeMap)));
        }

        [Test]
        public void CoinSkin_Buy1000_DuplicateClick_Reload_EquipPerJob()
        {
            DrinkSales.SkinArtReady = _ => true;
            DrinkLab.Data.coins = 2500;
            Assert.IsTrue(DrinkLab.BuyCosmetic(SkinCatalog.BartenderNoir, null, out var e), e);
            Assert.AreEqual(1500, DrinkLab.Coins);
            Assert.IsFalse(DrinkLab.BuyCosmetic(SkinCatalog.BartenderNoir, null, out e), "중복 클릭");
            StringAssert.Contains("이미 보유", e);
            Assert.AreEqual(1500, DrinkLab.Coins, "중복 클릭은 코인 한 번만");
            Assert.IsTrue(DrinkLab.BuyCosmetic(SkinCatalog.TeaAstronomer, null, out e), e);
            Assert.AreEqual(500, DrinkLab.Coins);
            Assert.IsFalse(DrinkLab.BuyCosmetic(SkinCatalog.TeaMountain, null, out e));
            StringAssert.Contains("500 부족", e);

            DrinkLab.ReloadForTests();
            Assert.AreEqual(500, DrinkLab.Coins, "저장 후 재시작");
            Assert.IsTrue(DrinkLab.CoinOwns(SkinCatalog.BartenderNoir));
            Assert.IsTrue(DrinkLab.CoinOwns(SkinCatalog.TeaAstronomer));
            Assert.IsFalse(DrinkLab.CoinOwns(SkinCatalog.TeaMountain));

            var commerce = Make();
            Assert.IsTrue(commerce.IsOwned(SkinCatalog.BartenderNoir));
            Assert.IsFalse(commerce.IsCashOwned(SkinCatalog.BartenderNoir), "현금 소유 기록 아님");
            Assert.IsFalse(commerce.Equip(SkinCatalog.TeaMountain), "미보유 장착 불가");
            Assert.IsTrue(commerce.Equip(SkinCatalog.BartenderNoir));
            Assert.IsTrue(commerce.Equip(SkinCatalog.TeaAstronomer));
            var reloaded = persister.Load();
            Assert.AreEqual(SkinCatalog.BartenderNoir, reloaded.EquippedFor(Job.Bartender));
            Assert.AreEqual(SkinCatalog.TeaAstronomer, reloaded.EquippedFor(Job.TeaSommelier), "직업마다 따로 장착");
            Assert.AreEqual(0, reloaded.owned.Count, "코인 구매는 commerce.json 소유 기록을 만들지 않는다");

            Func<SkinDefinition, bool> art = _ => true;
            Assert.AreEqual(SkinCatalog.BartenderNoir, SkinCatalog.ResolveEquipped(Job.Bartender, reloaded, Storefronts.GooglePlay, art)?.productId);
            Assert.AreEqual(SkinCatalog.TeaAstronomer, SkinCatalog.ResolveEquipped(Job.TeaSommelier, reloaded, Storefronts.GooglePlay, art)?.productId);
            Assert.IsNull(SkinCatalog.ResolveEquipped(Job.Bartender, reloaded, Storefronts.GooglePlay, _ => false), "아트 미완성이면 기본");

            // 기존 유료 스킨 구매·장착은 그대로 공존한다.
            commerce.Start();
            store.Ready();
            Assert.IsTrue(commerce.Buy(SkinCatalog.BartenderMidnight));
            commerce.OnPurchasePending(new StorePurchase { ProductId = SkinCatalog.BartenderMidnight, PurchaseToken = "paid-1", Handle = new object() });
            Assert.IsTrue(commerce.IsCashOwned(SkinCatalog.BartenderMidnight));
            Assert.AreEqual(1, store.Confirms.Count);
            Assert.IsTrue(commerce.Equip(SkinCatalog.BartenderMidnight));
            Assert.AreEqual(SkinCatalog.TeaAstronomer, commerce.State.EquippedFor(Job.TeaSommelier));
            Assert.AreEqual(500, DrinkLab.Coins, "현금 구매가 코인에 영향 없음");
        }

        /// <summary>아트 계약: 바텐더 23(몸체 클립 전체), 티 소믈리에 28(몸체 23 + 검 휘두르기 재생 구간 03~07). 활 준비·검 00~02는 미재생.</summary>
        [Test]
        public void RequiredFrames_MatchRuntimeContract_ExistingPaidSkinsComplete()
        {
            var bar = SkinArt.RequiredFrameNames(Job.Bartender);
            var tea = SkinArt.RequiredFrameNames(Job.TeaSommelier);
            Assert.AreEqual(23, bar.Count, string.Join(",", bar));
            Assert.AreEqual(28, tea.Count, string.Join(",", tea));
            for (int i = 3; i <= 7; i++) CollectionAssert.Contains(tea, $"tea_sommelier_sword_swing_right_{i:00}");
            for (int i = 0; i <= 2; i++) CollectionAssert.DoesNotContain(tea, $"tea_sommelier_sword_swing_right_{i:00}");
            Assert.IsFalse(tea.Any(n => n.StartsWith("bow_")), "활 준비 합성 클립은 미재생");
            foreach (var clip in new[] { "idle", "move", "melee", "ranged", "hurt", "defeat" })
            {
                Assert.IsTrue(bar.Any(n => n.StartsWith("bartender_" + clip + "_")), clip);
                Assert.IsTrue(tea.Any(n => n.StartsWith("tea_sommelier_" + clip + "_")), clip);
            }
            foreach (var def in SkinCatalog.All.Where(d => d.paid))
            {
                CollectionAssert.IsEmpty(SkinArt.MissingFrames(def), def.productId);
                Assert.IsTrue(SkinArt.Exists(def), def.productId);
            }
        }

        /// <summary>신규 스킨 폴더는 비어 있거나(아트 승인 전) 실제 사용 프레임을 전부 같은 캔버스·피벗으로 덮어야 한다 — 일부만 있으면
        /// 그 동작에서 기본 복장이 번쩍인다. 아트가 들어오면 이 검사가 프레임·크기·피벗 계약을 그대로 확인한다.</summary>
        [Test]
        public void CoinSkinArt_EitherAbsentOrCompleteAndAligned()
        {
            int ready = 0;
            foreach (var id in NewIds)
            {
                var def = SkinCatalog.Find(id);
                var map = SkinArt.Sprites(def);
                if (map.Count == 0)
                {
                    Assert.IsFalse(SkinArt.Exists(def));
                    Assert.IsNull(SkinCatalog.ResolveEquipped(def.job, StateEquipped(def), Storefronts.GooglePlay, SkinArt.Exists), "아트 없음 → 기본 아트");
                    continue;
                }
                ready++;
                CollectionAssert.IsEmpty(SkinArt.MissingFrames(def), id + " 누락 프레임");
                Assert.IsTrue(SkinArt.Exists(def));
                var baseByName = BaseFrames(def.job);
                foreach (var name in SkinArt.RequiredFrameNames(def.job))
                {
                    var skin = map[name];
                    var orig = baseByName[name];
                    Assert.AreEqual(orig.rect.size, skin.rect.size, $"{id}/{name} 캔버스 크기");
                    Assert.AreEqual(orig.pixelsPerUnit, skin.pixelsPerUnit, $"{id}/{name} PPU");
                    Assert.Less((orig.pivot - skin.pivot).sqrMagnitude, 0.01f, $"{id}/{name} 피벗");
                    Assert.AreNotSame(orig, skin);
                }
            }
            TestContext.WriteLine($"79 coin skins with art: {ready}/10");
        }

        /// <summary>79 검 외형 오버레이(무기-only, Aseprite export): 없거나, 재생 구간 03~07 정확히 5장이 원본 휘두르기와 같은 캔버스·피벗.
        /// 불투명 픽셀은 원본 휘두르기 프레임의 불투명 영역 안(몸 밖으로 새 그림을 그리지 않음 — 텍스처가 읽기 가능할 때만 픽셀 확인).</summary>
        [Test]
        public void SwordOverlay_EitherAbsentOrExactPlayedFramesAligned()
        {
            var def = WeaponSkinCatalog.ForWeapon(WeaponKind.Sword);
            var overlay = Resources.LoadAll<Sprite>(def.SwingOverlayFolder);
            if (overlay.Length == 0) { TestContext.WriteLine("sword overlay: not imported"); return; }
            var played = PlayerVisual.SwordSwingFrames().ToDictionary(f => f.name);
            CollectionAssert.AreEquivalent(played.Keys, overlay.Select(o => o.name), "재생 구간 03~07만");
            foreach (var o in overlay)
            {
                var b = played[o.name];
                Assert.AreEqual(b.rect.size, o.rect.size, o.name);
                Assert.Less((b.pivot - o.pivot).sqrMagnitude, 0.01f, o.name);
                Assert.AreEqual(b.pixelsPerUnit, o.pixelsPerUnit, o.name);
                if (!o.texture.isReadable || !b.texture.isReadable) continue;
                var op = o.texture.GetPixels((int)o.textureRect.x, (int)o.textureRect.y, (int)o.textureRect.width, (int)o.textureRect.height);
                var bp = b.texture.GetPixels((int)b.textureRect.x, (int)b.textureRect.y, (int)b.textureRect.width, (int)b.textureRect.height);
                int outside = 0;
                for (int i = 0; i < op.Length; i++) if (op[i].a > 0.01f && bp[i].a <= 0.01f) outside++;
                Assert.AreEqual(0, outside, o.name + " 원본 휘두르기 밖 픽셀");
            }
        }

        static CommerceState StateEquipped(SkinDefinition def)
        {
            var s = new CommerceState();
            s.Normalize();
            s.SetEquipped(def.job, def.productId);
            return s;
        }

        static Dictionary<string, Sprite> BaseFrames(Job job)
        {
            var map = new Dictionary<string, Sprite>();
            var art = ArtLibrary.Get(JobPreset.For(job).bodyArtId);
            foreach (var clip in art.clips)
                foreach (var f in clip.frames)
                    if (f != null) map[f.name] = f;
            if (job == Job.TeaSommelier)
                foreach (var f in PlayerVisual.SwordSwingFrames()) map[f.name] = f;
            return map;
        }
    }
}
