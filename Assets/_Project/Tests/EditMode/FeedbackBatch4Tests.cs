using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-16 피드백 19(화살 속성 표현)·25(히든 칸 모티프) 규칙 시험. 화면은 QA 투어 feedback4로 따로.</summary>
    public class FeedbackBatch4Tests
    {
        // ── 19 ──
        [Test]
        public void ArrowElement_SameConditionAsDirectHitStatus_LockHidesIt()
        {
            var s = new PlayerStats();
            Assert.AreEqual(PlayerProjectile.ArrowElement.None, PlayerProjectile.ArrowElementOf(s), "카드 없음 = 기본 화살");
            s.flameLevel = 1;
            Assert.AreEqual(PlayerProjectile.ArrowElement.Flame, PlayerProjectile.ArrowElementOf(s));
            s.poisonLevel = 2;
            Assert.AreEqual(PlayerProjectile.ArrowElement.Flame | PlayerProjectile.ArrowElement.Bitter, PlayerProjectile.ArrowElementOf(s), "둘 다 = 두 색");
            s.flameLevel = 0;
            Assert.AreEqual(PlayerProjectile.ArrowElement.Bitter, PlayerProjectile.ArrowElementOf(s));
            Assert.AreEqual(PlayerProjectile.ArrowElement.None, PlayerProjectile.ArrowElementOf(null));
        }

        [Test]
        public void ArrowElement_OnlyArrowArt_NotBullets()
        {
            Assert.IsTrue(PlayerProjectile.IsArrowArt("arrow"));
            Assert.IsTrue(PlayerProjectile.IsArrowArt("tea_arrow"));
            Assert.IsFalse(PlayerProjectile.IsArrowArt("bullet"), "총알은 이번 범위 아님(화살 표현만)");
            Assert.IsFalse(PlayerProjectile.IsArrowArt(null));
        }

        [Test]
        public void ArrowSpawn_WithoutOwnerOrNotOriginal_NoElementTail_SameScale()
        {
            var parent = new GameObject("P");
            try
            {
                PlayerProjectile.Spawn(Vector2.zero, Vector2.right, 10f, 5f, 0, parent.transform, "tea_arrow", null, true);
                var go = parent.transform.GetChild(0);
                Assert.IsNull(go.Find("ElementTail"), "주인 없는 화살(속성 판단 불가)은 표시 없음");
                Assert.AreEqual(Color.white, go.GetComponent<SpriteRenderer>().color, "기본 화살 색 그대로");
            }
            finally { Object.DestroyImmediate(parent); }
        }

        // ── 25 ──
        [Test]
        public void HiddenIcons_MapToExistingApprovedArt_Cached()
        {
            foreach (var id in new[] { "iced_cup", "soda_pump", "shake_rhythm", "bitter_wake", "weekend_rush", "herb_garden", "bar_tempo" })
            {
                string art = ActiveEffects.HiddenIconArt(id);
                Assert.IsNotNull(art, id);
                Assert.IsNotNull(ArtLibrary.Get(art), id + " → " + art + " 기존 아트 존재");
                var sp = ActiveEffects.HiddenIcon(id);
                Assert.IsNotNull(sp, id + " 그림");
                Assert.AreSame(sp, ActiveEffects.HiddenIcon(id), id + " 매번 새로 만들지 않음(0.1초 갱신 누수 방지)");
            }
            Assert.AreNotEqual(ActiveEffects.HiddenIconArt("bar_tempo", false), ActiveEffects.HiddenIconArt("bar_tempo", true), "템포 정지/이동 그림 구분");
            Assert.IsNull(ActiveEffects.HiddenIconArt("drink"), "음료·커피 칸은 대상 아님");
        }
    }

    /// <summary>피드백 26 설정: 기본 꺼짐, 예전 settings.json(키 없음) = 꺼짐, 켬 저장·재로드, 기본값 복원 = 꺼짐, 설정 화면 문구에 '표시만'.</summary>
    public class TipsyBadgeSettingsTests : SettingsV2Fixture
    {
        [Test]
        public void TipsyBadge_DefaultOff_MissingKeyOff_Persists_ResetOff_LabelSaysDisplayOnly()
        {
            Assert.IsFalse(SettingsService.Current.tipsyBadge, "새 설정 = 꺼짐");
            System.IO.File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"masterVolume\":40,\"damageNumbers\":false}");
            SettingsService.SetForTests(settingsPath, legacy, display);
            Assert.IsFalse(SettingsService.Current.tipsyBadge, "예전 파일(키 없음) = 꺼짐");
            Assert.IsFalse(SettingsService.Current.damageNumbers, "다른 값은 그대로 읽힘");
            SettingsService.SetTipsyBadge(true);
            SettingsService.SetForTests(settingsPath, legacy, display);
            Assert.IsTrue(SettingsService.Current.tipsyBadge, "켬 저장·재로드");
            var canvas = new GameObject("C", typeof(RectTransform));
            try
            {
                var view = SettingsView.Create(canvas.transform);
                view.Show();
                StringAssert.Contains("표시만", view.TipsyLabelText);
                view.ToggleTipsyBadge();
                Assert.IsFalse(SettingsService.Current.tipsyBadge);
                Assert.AreEqual("꺼짐", view.TipsyLabelText);
            }
            finally { Object.DestroyImmediate(canvas); }
            SettingsService.SetTipsyBadge(true);
            SettingsService.ResetToDefaults();
            Assert.IsFalse(SettingsService.Current.tipsyBadge, "기본값 복원 = 꺼짐");
            Assert.IsFalse(ActiveEffects.TipsyShown(null));
        }
    }
}
