using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-23 영웅 양손 전달(혼합 카드)·특수 깊은 목례(하이브리드 확정 해금 경로)를 실제 렌더 연속 프레임으로 확인(창 없음).
    /// CCT_EVIDENCE_DIR(+선택 CCT_SEQUENCE_DIR: 프레임 PNG 폴더)이 있을 때만 실행. 렌더 캡처는 매 프레임 실행되고(unscaled 시간은 실제 시간 — 렌더 비용 때문에 720p 약 30 fps, 1080p 약 16 fps 간격),
    /// 열림 첫 프레임부터 정착 뒤까지 매 프레임 저장하고, 프레임마다 슬롯별 등급·클립 프레임·노출·입력 열림을 CSV로 남긴다.
    /// 기본 재생 속도 0.5배 + 속도 옵션(1.0배·2.0배·0.25배)·저이펙트 회귀(선택 가능 시점만 기록).
    /// </summary>
    public class LevelUpHandoffSequenceCapture
    {
        string dir, seqDir;
        readonly StringBuilder log = new StringBuilder();
        GameManager game; Canvas canvas; bool setUp, savedSuppress; int savedPercent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음 — 증거 캡처 전용");
            seqDir = Environment.GetEnvironmentVariable("CCT_SEQUENCE_DIR");
            if (string.IsNullOrEmpty(seqDir)) seqDir = Path.Combine(dir, "frames");
            Directory.CreateDirectory(dir); Directory.CreateDirectory(seqDir);
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            savedSuppress = CardEducationGuide.SuppressAutomaticForTests;
            CardEducationGuide.SuppressAutomaticForTests = true;
            savedPercent = SettingsService.Current.levelUpAnimPercent;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            SettingsService.SetLevelUpAnimPercent(SettingsService.LevelUpAnimDefaultPercent);
            setUp = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!setUp) yield break;
            Time.captureFramerate = 0;
            File.AppendAllText(Path.Combine(dir, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false)); log.Clear();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            CardEducationGuide.SuppressAutomaticForTests = savedSuppress;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            SettingsService.SetLevelUpAnimPercent(savedPercent);
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[HandoffSeq] " + s); }

        IEnumerator Start(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            canvas = game.CardView.GetComponentInParent<Canvas>();
        }

        static CardData Pick(CardRarity rarity, CardCategory category, int skip = 0) =>
            Resources.LoadAll<CardData>("Cards").Where(x => x.rarity == rarity && x.category == category).OrderBy(x => x.id).Skip(skip).First();

        static List<CardOffer> Offers(CardCategory cat, params CardRarity[] rarities)
        {
            var list = new List<CardOffer>(); var used = new Dictionary<CardRarity, int>();
            foreach (var r in rarities)
            {
                used.TryGetValue(r, out int n); used[r] = n + 1;
                list.Add(new CardOffer { card = Pick(r, cat, n), currentStacks = 0 });
            }
            return list;
        }

        /// <summary>열림 프레임부터 정착 뒤 extra 프레임까지 매 프레임 렌더(save면 PNG). 첫 입력 열림 프레임 번호를 돌려준다.</summary>
        IEnumerator CardSequence(string tag, List<CardOffer> offers, int w, int h, bool save, int[] readyOut)
        {
            var v = game.CardView;
            var csv = new StringBuilder("frame,t_ms,unscaled_dt_ms,slot,tier,clip_frame,reveal,card_alpha,interactive\n");
            yield return null;
            v.Show(offers, _ => { });
            float start = Time.unscaledTime;
            int ready = -1, extra = 0; float readyMs = -1f;
            for (int f = 0; f < 600 && Time.unscaledTime - start < 4f; f++)
            {
                string name = $"{tag}_{w}x{h}_f{f:000}";
                if (save) yield return SkinEvidenceCapture.CaptureTo(seqDir, _ => { }, Camera.main, new[] { canvas }, w, h, name);
                else yield return new WaitForSecondsRealtime(1f / 60f); // 렌더 없이 약 60 Hz로 상태만 표본
                bool any = false;
                for (int i = 0; i < offers.Count; i++)
                {
                    bool inter = v.SlotInteractive(i);
                    any |= inter;
                    csv.AppendLine($"{f},{(Time.unscaledTime - start) * 1000f:0},{Time.unscaledDeltaTime * 1000f:0.0},{i},{v.SlotHandoffTier(i)},{v.SlotHandoffFrame(i)},{(v.SlotHandoffPlaying(i) ? "playing" : "done")},{v.CardAlpha(i):0.00},{inter}");
                }
                if (any && ready < 0) { ready = f; readyMs = (Time.unscaledTime - start) * 1000f; }
                if (ready >= 0 && ++extra > 6) break;
            }
            File.WriteAllText(Path.Combine(dir, $"{tag}_{w}x{h}_frames.csv"), csv.ToString(), new UTF8Encoding(false));
            int maxTier = LevelUpMenuArt.MaxTier(offers);
            float speed = LevelUpMenuArt.PlaybackSpeed;
            float expected = HitFeedback.LowEffects ? 0f : LevelUpMenuArt.RealSelectSeconds(maxTier, speed);
            Line($"{tag} {w}x{h}: offers={string.Join(",", offers.Select(o => o.card.rarity))} speed={speed:0.00} low={HitFeedback.LowEffects} firstInteractiveFrame={ready} at {readyMs:0} ms (real time since Show) expectedSelect≈{expected * 1000f:0} ms");
            readyOut[0] = ready;
            v.Choose(0);
            yield return null; yield return null;
        }

        IEnumerator HybridSequence(string tag, int w, int h, bool save, int[] readyOut)
        {
            var v = game.HybridView;
            var csv = new StringBuilder("frame,t_ms,unscaled_dt_ms,tier,clip_frame,playing,content_alpha,interactive\n");
            yield return null;
            v.Show(() => { });
            float start = Time.unscaledTime;
            int ready = -1, extra = 0; float readyMs = -1f;
            for (int f = 0; f < 600 && Time.unscaledTime - start < 4f; f++)
            {
                string name = $"{tag}_{w}x{h}_f{f:000}";
                if (save) yield return SkinEvidenceCapture.CaptureTo(seqDir, _ => { }, Camera.main, new[] { canvas }, w, h, name);
                else yield return new WaitForSecondsRealtime(1f / 60f); // 렌더 없이 약 60 Hz로 상태만 표본
                csv.AppendLine($"{f},{(Time.unscaledTime - start) * 1000f:0},{Time.unscaledDeltaTime * 1000f:0.0},{v.HandoffTier},{v.HandoffFrame},{v.HandoffPlaying},{v.ContentAlpha:0.00},{v.ContentInteractive}");
                if (v.ContentInteractive && ready < 0) { ready = f; readyMs = (Time.unscaledTime - start) * 1000f; }
                if (ready >= 0 && ++extra > 6) break;
            }
            File.WriteAllText(Path.Combine(dir, $"{tag}_{w}x{h}_frames.csv"), csv.ToString(), new UTF8Encoding(false));
            float expected = HitFeedback.LowEffects ? 0f : LevelUpMenuArt.RealSelectSeconds(3, LevelUpMenuArt.PlaybackSpeed);
            Line($"{tag} {w}x{h}: speed={LevelUpMenuArt.PlaybackSpeed:0.00} low={HitFeedback.LowEffects} firstInteractiveFrame={ready} at {readyMs:0} ms (real time since Show) expectedSelect≈{expected * 1000f:0} ms");
            readyOut[0] = ready;
            v.Unlock();
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Epic_MixedCards_Bartender_Default05x_720p_1080p()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            var r = new int[1];
            yield return CardSequence("bartender_mixed_epic_rare_common_05x", Offers(CardCategory.Cocktail, CardRarity.Epic, CardRarity.Rare, CardRarity.Common), 1280, 720, true, r);
            Assert.Greater(r[0], 0);
            yield return CardSequence("bartender_mixed_epic_rare_common_05x", Offers(CardCategory.Cocktail, CardRarity.Epic, CardRarity.Rare, CardRarity.Common), 1920, 1080, true, r);
            yield return CardSequence("bartender_mixed_common_epic_epic_05x", Offers(CardCategory.Cocktail, CardRarity.Common, CardRarity.Epic, CardRarity.Epic), 1280, 720, true, r);
        }

        [UnityTest]
        public IEnumerator Epic_MixedCards_Tea_Default05x_720p()
        {
            yield return Start(Job.TeaSommelier, WeaponKind.Bow);
            var r = new int[1];
            yield return CardSequence("tea_mixed_rare_epic_common_05x", Offers(CardCategory.Tea, CardRarity.Rare, CardRarity.Epic, CardRarity.Common), 1280, 720, true, r);
            Assert.Greater(r[0], 0);
        }

        [UnityTest]
        public IEnumerator Special_Hybrid_Bartender_Default05x_720p_1080p()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            var r = new int[1];
            yield return HybridSequence("hybrid_special_05x", 1280, 720, true, r);
            Assert.Greater(r[0], 0);
            yield return HybridSequence("hybrid_special_05x", 1920, 1080, true, r);
        }

        [UnityTest]
        public IEnumerator SpeedOptions_And_LowEffects_SelectTiming()
        {
            yield return Start(Job.Bartender, WeaponKind.Rapier);
            var r = new int[1];
            foreach (int pct in new[] { 100, 200, 25 })
            {
                SettingsService.SetLevelUpAnimPercent(pct);
                yield return CardSequence($"speed{pct}_mixed_epic_rare_common", Offers(CardCategory.Cocktail, CardRarity.Epic, CardRarity.Rare, CardRarity.Common), 1280, 720, false, r);
                yield return HybridSequence($"speed{pct}_hybrid_special", 1280, 720, false, r);
            }
            SettingsService.SetLevelUpAnimPercent(SettingsService.LevelUpAnimDefaultPercent);
            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            yield return CardSequence("low_mixed_epic_rare_common", Offers(CardCategory.Cocktail, CardRarity.Epic, CardRarity.Rare, CardRarity.Common), 1280, 720, true, r);
            Assert.LessOrEqual(r[0], 1, "저이펙트: 전달 없이 바로 선택 가능");
            yield return HybridSequence("low_hybrid_special", 1280, 720, true, r);
            Assert.LessOrEqual(r[0], 1, "저이펙트: 하이브리드도 바로");
        }
    }
}
