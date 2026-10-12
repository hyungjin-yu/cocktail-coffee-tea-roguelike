using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 09 §사운드·10 G33 객관 데이터(2026-10-08): 실제 Unity가 임포트·디코딩한 AudioClip 샘플로 22 SFX/3 BGM의 수·채널·샘플레이트·피크·
    /// BGM 루프 접합 연속성을 측정하고, AudioService의 같은 ID 4 / 전체 24 / 50·30ms 중복 합산을 실제 재생 요청으로 검증한다.
    /// 사람 청취(음색·체감 균형)를 대신하지 않는다.
    /// </summary>
    public class AudioSpecTests
    {
        static readonly string[] Sfx =
        {
            "boss_warning", "clear", "defeat", "enemy_defeat", "explosion", "hit_enemy", "hit_player", "hybrid_unlock",
            "levelup", "pickup", "shot_bow", "shot_gun", "shot_hybrid", "skill_unlock", "spell", "steam", "swing_rapier",
            "swing_sword", "ui_cancel", "ui_confirm", "ui_denied", "ui_focus",
        };
        static readonly string[] Bgm = { "bgm_menu", "bgm_run", "bgm_boss" };

        static float Peak(float[] d) { float p = 0f; foreach (var v in d) { float a = v < 0 ? -v : v; if (a > p) p = a; } return p; }

        [Test]
        public void DecodedClips_22Sfx3Bgm_Format_Peak_AndLoopSeam()
        {
            Assert.AreEqual(22, Sfx.Length);
            Assert.AreEqual(25, Resources.LoadAll<AudioClip>("Audio/v1").Length, "실제 카탈로그 전체 수");
            var audit = new System.Text.StringBuilder("id,channels,sampleRate,frames,samples,peak,clippedSamples,nonFiniteSamples,seamJump\n");
            foreach (var id in Sfx.Concat(Bgm))
            {
                var clip = Resources.Load<AudioClip>("Audio/v1/" + id);
                Assert.IsNotNull(clip, id);
                Assert.IsTrue(clip.LoadAudioData() || clip.loadState == AudioDataLoadState.Loaded, $"{id} 디코딩");
                bool bgm = Bgm.Contains(id);
                Assert.AreEqual(48000, clip.frequency, $"{id} 48kHz");
                Assert.AreEqual(bgm ? 2 : 1, clip.channels, $"{id} {(bgm ? "BGM stereo" : "SFX mono")}");
                var data = new float[clip.samples * clip.channels];
                Assert.IsTrue(clip.GetData(data, 0), $"{id} GetData");
                Assert.IsFalse(data.Any(v => float.IsNaN(v) || float.IsInfinity(v)), $"{id}: 모든 PCM 표본이 유한값");
                float peak = Peak(data);
                int clipped = data.Count(v => Mathf.Abs(v) >= 1f);
                Assert.AreEqual(0, clipped, $"{id}: PCM 클리핑 표본0");
                float seamJump = 0f;
                for (int c = 0; c < clip.channels; c++) seamJump = Mathf.Max(seamJump, Mathf.Abs(data[c] - data[data.Length - clip.channels + c]));
                audit.AppendLine(string.Join(",", id, clip.channels, clip.frequency, clip.samples, data.Length,
                    peak.ToString("R", System.Globalization.CultureInfo.InvariantCulture), clipped, 0,
                    seamJump.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                Assert.Greater(peak, 0.01f, $"{id}: 무음 파일 아님");
                // 09: 효과별 peak −6dBFS 이하(디코딩 오차 0.5dB 허용), 최종 클리핑(1.0) 없음
                Assert.Less(peak, Mathf.Pow(10f, -5.5f / 20f), $"{id}: 디코딩 피크 {20f * Mathf.Log10(peak):0.00} dBFS");
                if (bgm)
                {
                    // 09: 루프 구간=전체 파일, 마지막→처음 접합에서 잡음 없음 — 접합 점프가 파일 내부 이웃 표본 차이 수준인지
                    int ch = clip.channels, n = clip.samples;
                    float typical = 0f;
                    for (int i = 1; i < n; i += 97)
                        for (int c = 0; c < ch; c++) typical = Mathf.Max(typical, Mathf.Abs(data[i * ch + c] - data[(i - 1) * ch + c]));
                    for (int c = 0; c < ch; c++)
                    {
                        float seam = Mathf.Abs(data[c] - data[(n - 1) * ch + c]);
                        Assert.LessOrEqual(seam, typical + 0.005f, $"{id} ch{c}: 루프 접합 점프 {seam:0.0000} (내부 최대 이웃차 {typical:0.0000})");
                    }
                }
            }
            string auditPath = System.IO.Path.Combine(Application.temporaryCachePath, "cct-native-pcm-audit.csv");
            System.IO.File.WriteAllText(auditPath, audit.ToString());
            Debug.Log("[AudioSpec] native PCM data: " + auditPath);
        }

        static AudioService Svc() => AudioService.EnsureCreated();

        static int Voices(AudioService svc, string id = null)
        {
            var list = (IList)typeof(AudioService).GetField("activeVoices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(svc);
            int n = 0;
            foreach (var v in list)
            {
                var src = (AudioSource)v.GetType().GetField("source").GetValue(v);
                if (src == null || !src.isPlaying) continue;
                if (id == null || (string)v.GetType().GetField("id").GetValue(v) == id) n++;
            }
            return n;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AudioService.SetCombatSuppressed(false);
            Svc();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dedupe_HitEnemy50ms_Other30ms_SameIdCap4()
        {
            var svc = Svc();
            for (int i = 0; i < 5; i++) AudioService.PlaySfx("hit_enemy");
            Assert.AreEqual(1, Voices(svc, "hit_enemy"), "hit_enemy: 같은 프레임 5회 → 1개로 합산");
            yield return new WaitForSecondsRealtime(0.035f);
            AudioService.PlaySfx("hit_enemy");
            Assert.LessOrEqual(Voices(svc, "hit_enemy"), 1, "hit_enemy: 50ms 안 재요청은 합산");

            for (int i = 0; i < 3; i++) AudioService.PlaySfx("clear");
            Assert.AreEqual(1, Voices(svc, "clear"), "기타 ID: 같은 순간 중복 1개");
            for (int k = 0; k < 6; k++)
            {
                yield return new WaitForSecondsRealtime(0.04f); // 30ms 간격 넘김
                AudioService.PlaySfx("clear"); // 1.2초 길이 — 아직 재생 중
            }
            Assert.AreEqual(4, Voices(svc, "clear"), "같은 ID 동시 최대 4");
        }

        [UnityTest]
        public IEnumerator Polyphony_NeverExceeds24()
        {
            var svc = Svc();
            var longIds = Sfx.Where(id => (Resources.Load<AudioClip>("Audio/v1/" + id)?.length ?? 0f) >= 0.3f).ToArray();
            Assert.GreaterOrEqual(longIds.Length * 4, 24, "전제: 24 음성을 채울 만큼 긴 효과");
            int max = 0;
            for (int round = 0; round < 6; round++)
            {
                foreach (var id in longIds) AudioService.PlaySfx(id);
                max = Mathf.Max(max, Voices(svc));
                yield return new WaitForSecondsRealtime(0.035f);
            }
            Assert.LessOrEqual(max, 24, "전체 동시 24 이하");
            Assert.GreaterOrEqual(max, 20, "실제로 한도 근처까지 채워 검증했다");
            foreach (var id in longIds) Assert.LessOrEqual(Voices(svc, id), 4, $"{id} 같은 ID 4 이하");
        }
    }
}
