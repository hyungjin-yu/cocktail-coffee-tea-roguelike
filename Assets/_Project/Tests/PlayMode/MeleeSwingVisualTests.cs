using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-10-09 사용자 지시: 검 계열 공격의 파란 반원/바닥 범위 채움을 없애고 반월 베기와(검) PixelLab 휘두르기 몸체 클립으로 바꾼다.
    /// 06/10의 "전방 실제 범위 표시" 시각 기대값만 이 지시로 대체한다 — 판정·피해는 RapierWeaponTests/SpecCombatInputTests가 그대로 지킨다.</summary>
    public class MeleeSwingVisualTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        static bool AnyRangeFill() => Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None).Any(v =>
            v.Renderer != null && v.Renderer.sprite != null &&
            (v.Renderer.sprite == SpriteFactory.Semicircle || v.Renderer.sprite.name.StartsWith("player_melee_area")));

        /// <summary>2026-10-13 사용자 피드백: 활도 합성 준비 자세(bow_*)가 아니라 원본 전신 idle/move/ranged/hurt 프레임을 쓰고,
        /// 활 grip이 매 프레임 manifest hand anchor(앞 손)에 붙는다 — 좌우, 기본/비취 몸체, 기본/달빛 활 조합 모두.</summary>
        [UnityTest]
        public IEnumerator Bow_UsesOriginalFullBodyClips_GripFollowsFrameHandAnchor_AllSkinCombos()
        {
            var bowArt = ArtLibrary.Get("weapon_bow");
            Assert.IsTrue(bowArt.TryGetAnchor(bowArt.Main.name, 0, "grip", out var grip));
            foreach (bool jade in new[] { false, true })
            foreach (bool moonBow in new[] { false, true })
            {
                WeaponSkinStore.Equip(WeaponKind.Bow, moonBow ? "weaponskin_bow_01" : null);
                var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Bow);
                game.GetComponentInChildren<EnemySpawner>().enabled = false; game.Director.enabled = false;
                foreach (var mb in game.Player.GetComponents<MonoBehaviour>()) if (mb is PlayerProjectileWeapon) mb.enabled = false;
                var visual = game.Player.Visual;
                if (jade) SkinSpriteSwapper.Attach(visual.Body, SkinCatalog.Find(SkinCatalog.TeaJade));
                yield return null;
                string label = $"jade={jade} moonBow={moonBow}";
                var png = WeaponSkinGeometry.LoadPng(moonBow ? "Assets/_Project/Resources/WeaponSkins/weaponskin_bow_01/weaponskin_bow_01_held.png"
                    : "Assets/_Project/Resources/ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00.png");
                var content = WeaponSkinGeometry.AlphaBounds(png);
                Object.DestroyImmediate(png);
                if (moonBow) Assert.AreSame(WeaponSkinCatalog.HeldSprite(WeaponSkinCatalog.ForWeapon(WeaponKind.Bow)), visual.Weapon.sprite, label);

                void Check(string clip, float facing)
                {
                    var swap = visual.Body.GetComponent<SkinSpriteSwapper>();
                    if (swap != null) swap.Apply();
                    var name = visual.Body.sprite.name;
                    StringAssert.StartsWith("tea_sommelier_" + clip + "_right_", name, label + " 원본 전신 클립");
                    StringAssert.DoesNotStartWith("bow_", name);
                    if (jade) Assert.IsTrue(SkinArt.Sprites(SkinCatalog.Find(SkinCatalog.TeaJade)).ContainsValue(visual.Body.sprite), label + " 비취 의상 프레임");
                    int frame = int.Parse(name.Substring(name.Length - 2));
                    Assert.IsTrue(visual.BodyArt.TryGetAnchor(clip, frame, "hand", out var handPx), name);
                    Assert.IsFalse(visual.WeaponUsesProvisionalAnchor, name);
                    Vector3 weaponGrip = visual.Weapon.transform.TransformPoint(bowArt.AnchorToLocal(grip));
                    Vector3 hand = visual.transform.TransformPoint(visual.BodyArt.AnchorToLocal(handPx));
                    Assert.Less(Vector3.Distance(weaponGrip, hand), .001f, $"{label} {name} 활 grip = 앞 손");
                    // 2026-10-13 사용자 지시("좌우만 바꿔봐")로 몸통 관통 회귀는 제거(비겨냥도 겨냥과 같은 좌우 방향). 대신 방향을 확인한다.
                    bool aimFrame = PlayerVisual.BowAimOrientation(clip, frame);
                    // 겨냥은 반전(x<0, 0°), 휴대 자세는 그 화면 좌우 거울(x>0, -90°).
                    Assert.AreEqual(aimFrame, visual.Weapon.transform.localScale.x < 0f, $"{label} {name} 활 x 배율 부호");
                    // 2026-10-13 손에 든 활: 비겨냥 프레임은 몸 뒤(앞 주먹이 손잡이를 덮음)이고, 활 알파 영역이 손 높이(+6 몸체px) 위로
                    // 올라가지 않아 몸통(엉덩이 위)을 지나지 않는다.
                    if (!PlayerVisual.BowAimOrientation(clip, frame))
                    {
                        Assert.Less(visual.Weapon.sortingOrder, visual.Body.sortingOrder, $"{name} 주먹이 손잡이 위");
                        Vector3 handLocal = visual.BodyArt.AnchorToLocal(handPx);
                        float tol = 6f / visual.BodyArt.pixelsPerUnit;
                        var c0 = new Vector2(content.xMin, content.yMin); var c1 = new Vector2(content.xMax, content.yMax);
                        foreach (var c in new[] { c0, c1, new Vector2(c0.x, c1.y), new Vector2(c1.x, c0.y) })
                        {
                            Vector3 local = visual.transform.InverseTransformPoint(visual.Weapon.transform.TransformPoint(bowArt.AnchorToLocal(c)));
                            Assert.LessOrEqual(local.y, handLocal.y + tol, $"{label} {name} 활이 손 위 몸통으로 올라감 corner={c}");
                        }
                    }
                    else Assert.Greater(visual.Weapon.sortingOrder, visual.Body.sortingOrder, $"{name} 겨냥은 몸 앞");
                    Assert.AreEqual(facing < 0f, visual.transform.localScale.x < 0f, "좌우 반전");
                }

                foreach (float facing in new[] { 1f, -1f })
                {
                    game.Player.SetFacing(game.Player.Position.x + facing);
                    visual.Tick(game.Player.Facing, false, false); visual.SyncToCurrentFrame(); Check("idle", facing);
                    game.Player.AutoInputOverride = new Vector2(facing, 0f); // 실제 이동 경로로 move 6프레임 전부
                    var seenMove = new System.Collections.Generic.HashSet<string>();
                    for (float end = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < end && seenMove.Count < 6;)
                    {
                        yield return null;
                        visual.SyncToCurrentFrame(); Check("move", facing); seenMove.Add(visual.Body.sprite.name);
                    }
                    game.Player.AutoInputOverride = null;
                    Assert.AreEqual(6, seenMove.Count, "move 6프레임 모두 확인");
                    yield return null;
                    Assert.IsTrue(visual.PlayAction("ranged"));
                    for (int f = 0; f < 4; f++)
                    {
                        visual.SyncToCurrentFrame(); Check("ranged", facing);
                        yield return new WaitForSeconds(1f / 12f);
                    }
                    visual.PlayHurt(); visual.SyncToCurrentFrame(); Check("hurt", facing);
                    yield return new WaitForSeconds(0.5f);
                    visual.Tick(game.Player.Facing, false, false); visual.SyncToCurrentFrame(); Check("idle", facing); // 복귀
                }
                Object.Destroy(game.gameObject);
                yield return null;
            }
            WeaponSkinStore.Equip(WeaponKind.Bow, null);
        }

        IEnumerator HitOnce(GameManager game, System.Action<float> afterHit)
        {
            var player = game.Player;
            var enemy = Enemy.SpawnAt(player.Position + new Vector2(1f, 0f), EnemyDef.Brute, 1f, player, game.transform);
            float hp = enemy.Hp;
            for (int i = 0; i < 30 && enemy != null && enemy.Hp >= hp; i++) yield return null;
            Assert.Less(enemy.Hp, hp, "실제 타격(판정 유지)");
            afterHit(hp - enemy.Hp);
        }

        [UnityTest]
        public IEnumerator Sword_PlaysSwingClip_HidesHeldSword_ThenRestores_NoRangeFill()
        {
            var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            yield return null;
            var visual = game.Player.Visual;
            Assert.AreEqual("weapon_sword", visual.WeaponArtId);
            yield return HitOnce(game, _ => { });
            Assert.IsTrue(visual.SwordSwingActive, "검은 휘두르기 몸체 클립");
            Assert.IsFalse(visual.Weapon.enabled, "휘두르기 그림에 검이 들어 있어 손 무기는 숨긴다");
            Assert.AreEqual("sword_swing", visual.GetComponent<SpriteAnimator>().Current.name);
            Assert.IsFalse(AnyRangeFill(), "파란 반원/바닥 범위 채움 없음");
            yield return new WaitForSeconds(PlayerVisual.SwordSwingImpactDelay + 0.02f);
            Assert.IsTrue(Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)
                .Any(v => v.Renderer.sprite == SpriteFactory.Crescent), "타격 프레임에 반월 베기");
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(visual.SwordSwingActive);
            Assert.IsTrue(visual.Weapon.enabled, "휘두르기가 끝나면 손 무기 복귀");
        }

        [UnityTest]
        public IEnumerator Rapier_KeepsThrustClip_WithCrescent_NoRangeFill()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            var visual = game.Player.Visual;
            yield return HitOnce(game, _ => { });
            Assert.IsFalse(visual.SwordSwingActive, "레이피어는 찌르기(기존 melee) 유지");
            Assert.IsTrue(visual.Weapon.enabled);
            Assert.IsFalse(AnyRangeFill(), "파란 반원/바닥 범위 채움 없음");
            Assert.IsTrue(Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)
                .Any(v => v.Renderer.sprite == SpriteFactory.Crescent), "레이피어도 얇은 반월");
        }

        [UnityTest]
        public IEnumerator SwordSwing_PauseFreezes_AndRestartLeavesNoSlashVfx()
        {
            var game = GameBootstrap.Build(Job.TeaSommelier, WeaponKind.Sword);
            yield return null;
            yield return HitOnce(game, _ => { });
            game.OpenPause();
            yield return null;
            int frame = game.Player.Visual.GetComponent<SpriteAnimator>().FrameIndex;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(frame, game.Player.Visual.GetComponent<SpriteAnimator>().FrameIndex, "일시정지 중 휘두르기 정지");
            Object.Destroy(game.gameObject);
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            Assert.IsFalse(Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)
                .Any(v => v.Renderer.sprite == SpriteFactory.Crescent), "런 정리 후 반월 잔상 없음");
        }
    }
}
