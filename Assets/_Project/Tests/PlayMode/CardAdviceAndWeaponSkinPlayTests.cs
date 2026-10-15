using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>49 실제 런: 카드 선택 화면의 추천 표시(실제 보유 상태), 혼합 러시 고유 종류, 무기 외형의 실제 전투 렌더·능력치 불변.</summary>
    public class CardAdviceAndWeaponSkinPlayTests
    {
        string tempPath;
        GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_advice_{System.Guid.NewGuid():N}", "save.json");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
            PermanentProgress.SetPathForTests(tempPath);
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            try { Directory.Delete(Path.GetDirectoryName(tempPath), true); } catch { }
        }

        GameManager Build(Job job, WeaponKind weapon)
        {
            var g = GameBootstrap.Build(job, weapon);
            var spawner = g.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (g.Director != null) g.Director.enabled = false;
            return g;
        }

        static CardData C(string id) => CardDatabase.LoadAll().First(c => c.id == id);

        [UnityTest]
        public IEnumerator CardView_ShowsRealRunAdvice_DoesNotAutoChoose()
        {
            game = Build(Job.Bartender, WeaponKind.Gun);
            yield return null;
            game.GrantCardForTests(C("dry_gin"));
            game.GrantCardForTests(C("shot_glass"));
            var offers = new List<CardOffer>
            {
                new CardOffer { card = C("dry_gin"), currentStacks = 1 },
                new CardOffer { card = C("bar_shoes"), currentStacks = 0 },
            };
            CardOffer? chosen = null;
            game.CardView.Show(offers, o => chosen = o);
            yield return null;
            yield return null;
            Assert.IsTrue(game.CardView.IsOpen, "추천은 자동 선택하지 않는다");
            Assert.IsNull(chosen);
            StringAssert.Contains("혼합 러시 발동", game.CardView.AdviceText(1));
            Assert.IsTrue(game.CardView.BadgeVisible(1));
            Assert.IsFalse(game.CardView.BadgeVisible(0));
            StringAssert.DoesNotContain("러시", game.CardView.AdviceText(0));
            game.CardView.Choose(1);
            Assert.AreEqual("bar_shoes", chosen.Value.card.id);
        }

        [UnityTest]
        public IEnumerator Bartender_SameCocktailThreeTimes_NoRush_ThreeKindsRush()
        {
            game = Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            var gin = C("dry_gin");
            game.GrantCardForTests(gin); game.GrantCardForTests(gin); game.GrantCardForTests(gin);
            Assert.AreEqual(0f, game.MixingRushRemaining, "같은 카드 반복 중첩은 새 종류가 아님(46)");
            game.GrantCardForTests(C("shot_glass"));
            Assert.AreEqual(0f, game.MixingRushRemaining);
            game.GrantCardForTests(C("bar_shoes"));
            Assert.AreEqual(12f, game.MixingRushRemaining, "새 종류 3번째");
        }

        static readonly (Job job, WeaponKind weapon)[] Combos =
        {
            (Job.Bartender, WeaponKind.Rapier), (Job.Bartender, WeaponKind.Gun),
            (Job.TeaSommelier, WeaponKind.Sword), (Job.TeaSommelier, WeaponKind.Bow),
        };

        [UnityTest]
        public IEnumerator WeaponSkin_AppliesToRealHeldWeapon_StatsAndPlacementUnchanged()
        {
            foreach (var (job, weapon) in Combos)
            {
                var def = WeaponSkinCatalog.ForWeapon(weapon);
                Assert.IsTrue(WeaponSkinStore.Equip(weapon, null));
                game = Build(job, weapon);
                yield return null;
                var baseSprite = game.Player.Visual.Weapon.sprite;
                var baseT = game.Player.Visual.Weapon.transform;
                var basePos = baseT.localPosition; var baseScale = baseT.localScale;
                float dmg = game.Stats.Damage, range = game.Stats.Range, interval = game.Stats.AttackInterval;
                Assert.IsNull(game.Player.Visual.GetComponent<WeaponSkinApplier>(), "선택 없음 = 기본");
                Object.Destroy(game.gameObject); game = null;
                yield return null;

                Assert.IsTrue(WeaponSkinStore.Equip(weapon, def.id));
                Assert.AreEqual(def.id, WeaponSkinStore.Load(WeaponSkinStore.DefaultPath()).EquippedFor(weapon), "디스크 저장");
                game = Build(job, weapon);
                yield return null;
                var w = game.Player.Visual.Weapon;
                var skin = WeaponSkinCatalog.HeldSprite(def);
                if (skin != null)
                {
                    Assert.AreSame(skin, w.sprite, $"{weapon} 실제 손 무기에 스킨");
                    Assert.AreNotSame(baseSprite, w.sprite);
                }
                else Assert.AreSame(baseSprite, w.sprite, $"{weapon} 아트 없음 → 기본 외형");
                Assert.AreEqual(basePos, w.transform.localPosition, "손 위치 동일");
                Assert.AreEqual(baseScale, w.transform.localScale, "크기 동일");
                Assert.AreEqual(dmg, game.Stats.Damage); Assert.AreEqual(range, game.Stats.Range);
                Assert.AreEqual(interval, game.Stats.AttackInterval);

                if (weapon == WeaponKind.Sword && skin != null)
                {
                    // 배치 모드는 WaitForEndOfFrame이 없으므로 모든 교체 뒤(LateUpdate 10000) 렌더될 스프라이트를 탐침으로 기록한다.
                    var probe = LateSpriteProbe.Attach(game.Player.Visual);
                    Assert.IsTrue(game.Player.Visual.PlaySwordSwing());
                    yield return new WaitForSeconds(0.3f);
                    var skinFrames = new HashSet<Sprite>(Resources.LoadAll<Sprite>(def.SwingFolder(false)));
                    var swings = probe.bodyFrames.Where(s => s != null && s.name.StartsWith(WeaponSkinApplier.SwingPrefix, System.StringComparison.Ordinal)).ToList();
                    Assert.Greater(swings.Count, 0, "휘두르기 프레임 렌더");
                    foreach (var s in swings) Assert.IsTrue(skinFrames.Contains(s), $"렌더 시점 휘두르기 프레임이 스킨판: {s.name}");

                    // 비취 몸체 스킨 + 검 외형: 비취 의상 프레임 위에 검만 바뀐 swing_jade 프레임(몸체 스킨과 독립 조합).
                    SkinSpriteSwapper.Attach(game.Player.Visual.Body, SkinCatalog.Find(SkinCatalog.TeaJade));
                    var jadeFrames = new HashSet<Sprite>(Resources.LoadAll<Sprite>(def.SwingFolder(true)));
                    probe.bodyFrames.Clear();
                    Assert.IsTrue(game.Player.Visual.PlaySwordSwing());
                    yield return new WaitForSeconds(0.3f);
                    swings = probe.bodyFrames.Where(s => s != null && s.name.StartsWith(WeaponSkinApplier.SwingPrefix, System.StringComparison.Ordinal)).ToList();
                    Assert.Greater(swings.Count, 0);
                    foreach (var s in swings) Assert.IsTrue(jadeFrames.Contains(s), $"비취+검 외형 프레임: {s.name}");
                }
                Object.Destroy(game.gameObject); game = null;
                yield return null;
                WeaponSkinStore.Equip(weapon, null);
            }
        }
    }
}
