using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-11-23 항목 8 후속: 레벨업 메뉴판 재생 속도 설정 — 기본 50%(약 2배 길이), 기존 settings.json(키 없음) = 기본,
    /// 범위 정리, 저장·재로드, 기본값 복원, 설정 화면 순환·문구, 실제 시간 계산.</summary>
    public class LevelUpMenuSpeedSettingsTests : SettingsV2Fixture
    {
        [Test]
        public void Default_Half_MissingKey_Default_Persists_Clamps_Reset()
        {
            Assert.AreEqual(50, SettingsService.Current.levelUpAnimPercent, "새 설정 = 0.5배");
            Assert.AreEqual(0.5f, LevelUpMenuArt.PlaybackSpeed, 1e-6f);

            System.IO.File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"masterVolume\":40,\"tipsyBadge\":true}");
            SettingsService.SetForTests(settingsPath, legacy, display);
            Assert.AreEqual(50, SettingsService.Current.levelUpAnimPercent, "예전 파일(키 없음) = 기본");
            Assert.IsTrue(SettingsService.Current.tipsyBadge, "다른 값은 그대로 읽힘");

            SettingsService.SetLevelUpAnimPercent(150);
            SettingsService.SetForTests(settingsPath, legacy, display);
            Assert.AreEqual(150, SettingsService.Current.levelUpAnimPercent, "기존 설정 저장 경로(settings.json)로 저장·재로드");
            StringAssert.Contains("\"levelUpAnimPercent\":150", System.IO.File.ReadAllText(settingsPath));

            System.IO.File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"levelUpAnimPercent\":999}");
            SettingsService.SetForTests(settingsPath, legacy, display);
            Assert.AreEqual(200, SettingsService.Current.levelUpAnimPercent, "범위 위 → 200");
            System.IO.File.WriteAllText(settingsPath, "{\"schemaVersion\":2,\"levelUpAnimPercent\":0}");
            SettingsService.SetForTests(settingsPath, legacy, display);
            Assert.AreEqual(25, SettingsService.Current.levelUpAnimPercent, "범위 아래 → 25");

            SettingsService.SetLevelUpAnimPercent(5);
            Assert.AreEqual(25, SettingsService.Current.levelUpAnimPercent, "설정 함수도 정리");
            SettingsService.ResetToDefaults();
            Assert.AreEqual(50, SettingsService.Current.levelUpAnimPercent, "기본값 복원 = 0.5배");
        }

        [Test]
        public void SettingsView_Cycles_Steps_Label_ShowsDefault_And_LowEffectsImmediate()
        {
            var canvas = new GameObject("C", typeof(RectTransform));
            try
            {
                var view = SettingsView.Create(canvas.transform);
                view.Show();
                Assert.AreEqual("0.5배 (기본)", view.LevelUpAnimLabelText);
                var seen = new System.Collections.Generic.List<int>();
                for (int i = 0; i < SettingsService.LevelUpAnimSteps.Length; i++)
                {
                    view.CycleLevelUpAnimSpeed();
                    seen.Add(SettingsService.Current.levelUpAnimPercent);
                }
                CollectionAssert.AreEqual(new[] { 75, 100, 150, 200, 50 }, seen, "0.5 → 0.75 → 1 → 1.5 → 2 → 0.5배 순환");
                SettingsService.SetLevelUpAnimPercent(60); // 단계 밖 값(파일 직접 편집) → 다음 큰 단계
                view.CycleLevelUpAnimSpeed();
                Assert.AreEqual(75, SettingsService.Current.levelUpAnimPercent);
                Assert.AreEqual("0.75배", view.LevelUpAnimLabelText);
                view.ToggleEffectsLevel();
                StringAssert.Contains("즉시", view.LevelUpAnimLabelText, "효과 낮음이면 연출 생략을 함께 표시");
                view.ToggleEffectsLevel();
                Assert.IsNotNull(canvas.transform.Find("Settings/LevelUpAnimSpeed"), "설정 화면 줄");
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void RealSeconds_ScaleWithSpeed_DefaultRoughlyDouble()
        {
            for (int tier = 0; tier < 4; tier++)
            {
                float baseSec = LevelUpMenuArt.SelectMs(tier) / 1000f;
                Assert.AreEqual(baseSec, LevelUpMenuArt.RealSelectSeconds(tier, 1f), 1e-5f);
                Assert.AreEqual(baseSec * 2f, LevelUpMenuArt.RealSelectSeconds(tier, 0.5f), 1e-5f, "기본 0.5배 = 2배 길이");
                Assert.AreEqual(baseSec / 2f, LevelUpMenuArt.RealSelectSeconds(tier, 2f), 1e-5f);
            }
            Assert.AreEqual(0.78f, LevelUpMenuArt.RealSelectSeconds(2, 0.5f), 1e-4f, "영웅 390 ms → 780 ms");
        }
    }
}
