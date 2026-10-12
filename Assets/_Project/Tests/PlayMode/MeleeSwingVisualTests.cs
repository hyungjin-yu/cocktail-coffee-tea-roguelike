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

        [UnityTest]
        public IEnumerator Bow_UsesTwoHandedReadyPose_IdleMoveAndRangedHaveRealFrontHandAnchors()
        {
            var game=GameBootstrap.Build(Job.TeaSommelier,WeaponKind.Bow);
            game.GetComponentInChildren<EnemySpawner>().enabled=false; game.Director.enabled=false;
            var visual=game.Player.Visual;
            yield return null;
            visual.Tick(1f,false,false); visual.SyncToCurrentFrame();
            StringAssert.StartsWith("bow_idle",visual.Body.sprite.name);
            Assert.IsFalse(visual.WeaponUsesProvisionalAnchor);
            visual.Tick(1f,true,false); visual.SyncToCurrentFrame();
            StringAssert.StartsWith("bow_move",visual.Body.sprite.name);
            Assert.IsFalse(visual.WeaponUsesProvisionalAnchor);
            Assert.IsTrue(visual.PlayAction("ranged")); visual.SyncToCurrentFrame();
            StringAssert.StartsWith("bow_ranged",visual.Body.sprite.name);
            var art=ArtLibrary.Get("weapon_bow");
            Assert.IsTrue(art.TryGetAnchor(art.Main.name,0,"grip",out var grip));
            Vector3 weaponGrip=visual.Weapon.transform.TransformPoint(art.AnchorToLocal(grip));
            Vector3 hand=visual.transform.TransformPoint(visual.BodyArt.AnchorToLocal(new Vector2(198,153)));
            Assert.Less(Vector3.Distance(weaponGrip,hand),.001f);
            visual.Tick(-1f,false,false); visual.SyncToCurrentFrame();
            Assert.Less(visual.transform.localScale.x,0f);
            Assert.IsFalse(visual.WeaponUsesProvisionalAnchor);
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
