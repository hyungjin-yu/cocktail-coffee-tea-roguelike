using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 79 검 외형 오버레이: 비취가 아닌 몸체 스킨 + 검 외형이면 몸체 프레임은 몸체 스킨 쪽 그대로(기본/비취 휘두르기로 교체 안 함)이고,
    /// 휘두르기 프레임 동안만 무기-only 오버레이가 같은 이름으로 몸 위(정렬 +2)에 보이며 피격·쓰러짐·대기에서는 숨는다.
    /// 신규 몸체 아트 승인 전이라 몸체 스킨 자리는 기존 미드나잇(비취 아님) 스와퍼로 대신한다 — 몸체 판정 경로만 확인.
    /// </summary>
    public class SwordOverlayPlayTests
    {
        [DefaultExecutionOrder(10000)]
        class OverlayProbe : MonoBehaviour
        {
            public SpriteRenderer body;
            public WeaponSkinApplier applier;
            public readonly List<(string bodyName, bool on, string overlayName, float bodySign, float overlaySign)> frames =
                new List<(string, bool, string, float, float)>();
            void LateUpdate()
            {
                var o = applier != null ? applier.SwingOverlay : null;
                frames.Add((body.sprite != null ? body.sprite.name : null, o != null && o.enabled, o != null && o.sprite != null ? o.sprite.name : null,
                    Mathf.Sign(body.transform.lossyScale.x), o != null ? Mathf.Sign(o.transform.lossyScale.x) : 0f));
            }
        }

        string tempPath;
        GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempPath = Path.Combine(Application.temporaryCachePath, $"cct_overlay_{System.Guid.NewGuid():N}", "save.json");
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
            WeaponSkinStore.Equip(WeaponKind.Sword, null);
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            try { Directory.Delete(Path.GetDirectoryName(tempPath), true); } catch { }
        }

        [UnityTest]
        public IEnumerator NonJadeBodySkin_SwordSkin_OverlayOnlyDuringSwing_BodyNotReplaced()
        {
            var def = WeaponSkinCatalog.ForWeapon(WeaponKind.Sword);
            var overlayArt = Resources.LoadAll<Sprite>(def.SwingOverlayFolder).ToDictionary(s => s.name);
            if (overlayArt.Count == 0) Assert.Ignore("검 오버레이 아트 없음");
            Assert.IsTrue(WeaponSkinStore.Equip(WeaponKind.Sword, def.id));
            game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            yield return null;
            var visual = game.Player.Visual;
            var applier = visual.GetComponent<WeaponSkinApplier>();
            Assert.IsNotNull(applier, "검 외형 적용");
            Assert.IsNotNull(SkinSpriteSwapper.Attach(visual.Body, SkinCatalog.Find(SkinCatalog.BartenderMidnight)), "비취 아닌 몸체 스와퍼(대역)");
            var baseSwing = new HashSet<Sprite>(PlayerVisual.SwordSwingFrames());
            var weaponSwing = new HashSet<Sprite>(Resources.LoadAll<Sprite>(def.SwingFolder(false)).Concat(Resources.LoadAll<Sprite>(def.SwingFolder(true))));
            var probe = visual.gameObject.AddComponent<OverlayProbe>();
            probe.body = visual.Body;
            probe.applier = applier;

            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(visual.PlaySwordSwing());
            yield return new WaitForSeconds(0.35f);
            visual.PlayHurt();
            yield return new WaitForSeconds(0.2f);
            visual.PlayDefeat();
            yield return new WaitForSeconds(0.3f);

            int swing = 0, on = 0;
            foreach (var f in probe.frames)
            {
                bool isSwing = f.bodyName != null && f.bodyName.StartsWith(WeaponSkinApplier.SwingPrefix, System.StringComparison.Ordinal);
                if (isSwing)
                {
                    swing++;
                    Assert.IsTrue(f.on, "휘두르기 동안 오버레이 표시: " + f.bodyName);
                    Assert.AreEqual(f.bodyName, f.overlayName, "같은 프레임 이름");
                    Assert.AreEqual(f.bodySign, f.overlaySign, "좌우 반전 동일");
                    on++;
                }
                else Assert.IsFalse(f.on, "휘두르기 밖(대기·피격·쓰러짐)에는 숨김: " + f.bodyName);
            }
            Assert.Greater(swing, 0, "휘두르기 프레임이 렌더됨");
            Assert.IsFalse(visual.Body.sprite != null && weaponSwing.Contains(visual.Body.sprite), "몸체를 검 외형 휘두르기 몸체 프레임으로 교체하지 않음");
            var o = applier.SwingOverlay;
            Assert.IsNotNull(o);
            Assert.AreSame(visual.Body.transform, o.transform.parent, "몸체 자식 → 위치·반전 동일");
            Assert.AreEqual(Vector3.zero, o.transform.localPosition);
            Assert.AreEqual(Vector3.one, o.transform.localScale);
            Assert.Greater(o.sortingOrder, visual.Body.sortingOrder);
            Assert.Greater(o.sortingOrder, visual.Weapon.sortingOrder, "손 무기보다 앞");
            foreach (var s in overlayArt.Values)
                Assert.IsFalse(baseSwing.Contains(s));

            // 반대 방향(왼쪽)도 같은 부모 변환을 따른다.
            var t = visual.transform;
            var sc = t.localScale; sc.x = -Mathf.Abs(sc.x); t.localScale = sc;
            Assert.AreEqual(Mathf.Sign(visual.Body.transform.lossyScale.x), Mathf.Sign(o.transform.lossyScale.x));

            // 게임 종료(파괴) 후 오버레이도 함께 사라진다.
            var overlayGo = o.gameObject;
            Object.Destroy(game.gameObject); game = null;
            yield return null;
            Assert.IsTrue(overlayGo == null, "런 종료와 함께 제거");
        }

        /// <summary>비취·스킨 없음 경로는 기존 그대로(오버레이 없음).</summary>
        [UnityTest]
        public IEnumerator JadeOrNoBodySkin_NoOverlay_ExistingSwingPathKept()
        {
            var def = WeaponSkinCatalog.ForWeapon(WeaponKind.Sword);
            Assert.IsTrue(WeaponSkinStore.Equip(WeaponKind.Sword, def.id));
            game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            yield return null;
            var visual = game.Player.Visual;
            var applier = visual.GetComponent<WeaponSkinApplier>();
            Assert.IsNotNull(applier);
            var probe = visual.gameObject.AddComponent<OverlayProbe>();
            probe.body = visual.Body;
            probe.applier = applier;
            Assert.IsTrue(visual.PlaySwordSwing());
            yield return new WaitForSeconds(0.3f);
            SkinSpriteSwapper.Attach(visual.Body, SkinCatalog.Find(SkinCatalog.TeaJade));
            Assert.IsTrue(visual.PlaySwordSwing());
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(probe.frames.Count > 0);
            Assert.IsFalse(probe.frames.Any(f => f.on), "스킨 없음/비취에는 오버레이 없음(기존 swing_base/swing_jade 경로)");
        }
    }
}
