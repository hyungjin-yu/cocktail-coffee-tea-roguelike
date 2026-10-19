using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>49 무기 외형: 카탈로그(네 무기 1종씩·무료), 무기별 독립 저장, 알 수 없는 id/손상 파일은 기본 외형, commerce.json과 분리.</summary>
    public class WeaponSkinTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "cct-weaponskin-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        [Test]
        public void Catalog_OneSkinPerWeapon_NotACommerceProduct()
        {
            foreach (var w in WeaponSkinCatalog.Weapons)
            {
                var def = WeaponSkinCatalog.ForWeapon(w);
                Assert.IsNotNull(def, w.ToString());
                Assert.AreEqual(w, def.weapon);
                Assert.IsFalse(string.IsNullOrEmpty(def.displayName));
                Assert.IsFalse(string.IsNullOrEmpty(def.difference));
                Assert.IsFalse(SkinCatalog.IsKnown(def.id), "무기 외형은 유료 상품 목록에 없다");
                CollectionAssert.DoesNotContain(SkinCatalog.AllProductIds, def.id);
            }
            Assert.AreEqual(4, WeaponSkinCatalog.All.Length);
        }

        [Test]
        public void State_PerWeaponIndependent_UnknownOrMismatchedIdsFallBackToBase()
        {
            var s = new WeaponSkinState();
            s.SetEquipped(WeaponKind.Sword, "weaponskin_sword_01");
            s.SetEquipped(WeaponKind.Gun, "weaponskin_gun_01");
            Assert.AreEqual("weaponskin_sword_01", s.EquippedFor(WeaponKind.Sword));
            Assert.AreEqual("weaponskin_gun_01", s.EquippedFor(WeaponKind.Gun));
            Assert.IsNull(s.EquippedFor(WeaponKind.Bow));
            s.SetEquipped(WeaponKind.Gun, null);
            Assert.IsNull(s.EquippedFor(WeaponKind.Gun));
            Assert.AreEqual("weaponskin_sword_01", s.EquippedFor(WeaponKind.Sword), "다른 무기 선택은 그대로");

            s.SetEquipped(WeaponKind.Bow, "weaponskin_sword_01"); // 다른 무기의 스킨
            Assert.IsNull(s.EquippedFor(WeaponKind.Bow));
            s.equipped.Add(new WeaponSkinChoice { weapon = "Rapier", skinId = "weaponskin_unknown" });
            s.equipped.Add(new WeaponSkinChoice { weapon = "Laser", skinId = "weaponskin_gun_01" });
            Assert.IsNull(s.EquippedFor(WeaponKind.Rapier));
            s.Normalize();
            Assert.AreEqual(1, s.equipped.Count, "알 수 없는 항목 정리");
        }

        [Test]
        public void Store_RoundTrip_CorruptFileIsBase_MissingFileIsBase()
        {
            var path = Path.Combine(dir, WeaponSkinStore.FileName);
            Assert.IsNull(WeaponSkinStore.Load(path).EquippedFor(WeaponKind.Rapier), "기존 세이브(파일 없음) = 기본");

            var s = new WeaponSkinState();
            s.SetEquipped(WeaponKind.Rapier, "weaponskin_rapier_01");
            Assert.IsTrue(WeaponSkinStore.Save(path, s));
            Assert.AreEqual("weaponskin_rapier_01", WeaponSkinStore.Load(path).EquippedFor(WeaponKind.Rapier));
            Assert.IsFalse(File.Exists(path + ".tmp"));

            s.SetEquipped(WeaponKind.Bow, "weaponskin_bow_01");
            Assert.IsTrue(WeaponSkinStore.Save(path, s)); // .bak = 이전 판
            File.WriteAllText(path, "{\"equipped\":[{\"weapon\":"); // 손상
            var recovered = WeaponSkinStore.Load(path);
            Assert.AreEqual("weaponskin_rapier_01", recovered.EquippedFor(WeaponKind.Rapier), ".bak 복구");

            File.WriteAllText(path, "garbage");
            File.Delete(path + ".bak");
            Assert.IsNull(WeaponSkinStore.Load(path).EquippedFor(WeaponKind.Rapier), "읽을 수 없으면 기본 외형");
        }

        [Test]
        public void Store_DoesNotTouchCommerceFile()
        {
            var commercePath = Path.Combine(dir, FileCommercePersister.FileName);
            var cs = new CommerceState();
            cs.Grant(Storefronts.GooglePlay, SkinCatalog.BartenderMidnight, "hash");
            cs.SetEquipped(Job.Bartender, SkinCatalog.BartenderMidnight);
            Assert.IsTrue(new FileCommercePersister(commercePath).Save(cs));
            var before = File.ReadAllBytes(commercePath);

            var s = new WeaponSkinState();
            s.SetEquipped(WeaponKind.Sword, "weaponskin_sword_01");
            WeaponSkinStore.Save(Path.Combine(dir, WeaponSkinStore.FileName), s);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(commercePath));
            var loaded = new FileCommercePersister(commercePath).Load();
            Assert.IsTrue(loaded.IsOwned(Storefronts.GooglePlay, SkinCatalog.BartenderMidnight));
            Assert.AreEqual(SkinCatalog.BartenderMidnight, loaded.EquippedFor(Job.Bartender));
        }

        /// <summary>2026-10-13 기하 계약: 모든 무기 스킨 PNG(손 무기 4 + 검 휘두르기 16)는 원본과 알파 실루엣이 같아야
        /// 기본 무기 anchor·방향·발사 위치를 공유한다(WeaponSkinArtImport와 같은 검사). 새 스킨도 이 시험으로 자동 확인된다.</summary>
        [Test]
        public void AllWeaponSkinPngs_ShareOriginalSilhouette()
        {
            int checkedCount = 0;
            foreach (var (target, original) in CCT.Editor.WeaponSkinArtImport.Pairs())
            {
                if (!File.Exists(target)) continue; // 아트 준비 전 — 런타임은 기본 외형
                var a = WeaponSkinGeometry.LoadPng(original); var b = WeaponSkinGeometry.LoadPng(target);
                try { Assert.IsTrue(WeaponSkinGeometry.SameSilhouette(a, b, out var why), $"{target}: {why}"); }
                finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
                checkedCount++;
            }
            Assert.AreEqual(20, checkedCount, "현재 무기 스킨 20장(손 4 + 검 휘두르기 16)");
        }

        [Test]
        public void HeldSprite_NullUnlessArtMatchesBaseGeometry()
        {
            foreach (var def in WeaponSkinCatalog.All)
            {
                var skin = WeaponSkinCatalog.HeldSprite(def);
                var b = WeaponSkinCatalog.BaseHeldSprite(def.weapon);
                Assert.IsNotNull(b, "기본 무기 스프라이트");
                if (skin == null) continue; // 아트 준비 전 — 기본 외형
                Assert.AreEqual(b.rect.size, skin.rect.size);
                Assert.Less((b.pivot - skin.pivot).sqrMagnitude, 0.01f);
                Assert.AreEqual(b.pixelsPerUnit, skin.pixelsPerUnit, 0.01f);
            }
        }
    }
}
