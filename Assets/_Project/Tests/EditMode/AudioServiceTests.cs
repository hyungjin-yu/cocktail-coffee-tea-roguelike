using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace CCT.Tests
{
    /// <summary>09:139 "Pause/보상창은 음악 계속 재생하되 Music-8dB, 전투SFX 중단" 회귀 테스트.
    ///
    /// AudioService.EnsureCreated()의 DontDestroyOnLoad를 Application.isPlaying으로 감싼 덕분에
    /// EditMode에서도 인스턴스화가 가능해졌다(이전엔 InvalidOperationException으로 아예 불가능했음 —
    /// 실측 확인). 다만 크로스페이드/결과 시퀀스 코루틴은 Unity의 PlayerLoop가 EditMode에서 안 돌아서
    /// 첫 yield 이후로는 진행되지 않는다 — 그래서 여기서는 코루틴 없이 동기적으로 끝나는 로직(SFX
    /// 재생/중단/억제, 믹서 라우팅)만 검증한다. 페이드 타이밍/빠른 재시작 취소/크로스페이드 경합은
    /// 여전히 PlayMode 실행이 필요하고, 그건 이 머신에서 막혀있어 미검증으로 남는다.</summary>
    public class AudioServiceTests
    {
        [TearDown]
        public void TearDown()
        {
            // CombatSuppressed는 static bool이라 AudioService 인스턴스를 파괴해도 안 풀린다 — 다음
            // 테스트로 억제 상태가 새어나가지 않게 매번 명시적으로 해제한다.
            AudioService.SetCombatSuppressed(false);
            if (AudioService.Instance != null) Object.DestroyImmediate(AudioService.Instance.gameObject);
        }

        [Test]
        public void EnsureCreated_Repeatedly_KeepsOneEnabledListener()
        {
            var service = AudioService.EnsureCreated();
            Assert.AreSame(service, AudioService.EnsureCreated());
            var listeners = service.GetComponentsInChildren<AudioListener>(true);
            Assert.That(listeners, Has.Length.EqualTo(1));
            Assert.IsTrue(listeners[0].isActiveAndEnabled);
        }

        [Test]
        public void FinishedCombatSource_ReusedByUi_IsNotStoppedByPause()
        {
            var service = AudioService.EnsureCreated();
            AudioService.PlaySfx("hit_enemy");
            var reused = service.GetComponentsInChildren<AudioSource>().Single(s => s.isPlaying);
            reused.Stop(); // Ended before the next Update.
            AudioService.PlaySfx("ui_confirm");
            Assert.That(reused.clip.name, Is.EqualTo("ui_confirm"));
            Assert.IsTrue(reused.isPlaying);
            AudioService.SetCombatSuppressed(true);
            Assert.IsTrue(reused.isPlaying, "Pause must retain the new UI voice on a recycled combat source");
        }

        [Test]
        public void StopCombatSfx_StopsCombatVoice_KeepsUiVoice()
        {
            AudioService.EnsureCreated();
            AudioService.PlaySfx("hit_enemy");
            AudioService.PlaySfx("ui_confirm");

            var playingBefore = AudioService.Instance.GetComponentsInChildren<AudioSource>()
                .Where(s => s.isPlaying).Select(s => s.clip.name).ToList();
            Assert.That(playingBefore, Is.EquivalentTo(new[] { "hit_enemy", "ui_confirm" }),
                "전제 조건: 두 SFX 모두 재생 중이어야 함");

            AudioService.StopCombatSfx();

            var playingAfter = AudioService.Instance.GetComponentsInChildren<AudioSource>()
                .Where(s => s.isPlaying).Select(s => s.clip.name).ToList();
            Assert.That(playingAfter, Is.EquivalentTo(new[] { "ui_confirm" }),
                "전투 SFX(hit_enemy)는 끊기고 UI 음(ui_confirm)은 남아야 한다");
        }

        [Test]
        public void StopCombatSfx_CalledTwice_DoesNotThrow_AndLeavesNoCombatVoicePlaying()
        {
            AudioService.EnsureCreated();
            AudioService.PlaySfx("explosion");
            AudioService.PlaySfx("steam");

            AudioService.StopCombatSfx();
            Assert.DoesNotThrow(() => AudioService.StopCombatSfx(), "이미 멈춘 뒤 다시 불러도(예: 중첩 오버레이) 예외 없이 안전해야 함");

            var stillPlaying = AudioService.Instance.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying);
            Assert.IsFalse(stillPlaying, "두 번째 호출 뒤에도 재생 중인 전투 SFX가 없어야 함");
        }

        static System.Collections.Generic.List<string> PlayingClipNames() =>
            AudioService.Instance.GetComponentsInChildren<AudioSource>()
                .Where(s => s.isPlaying).Select(s => s.clip.name).ToList();

        [Test]
        public void SetCombatSuppressed_True_BlocksNewCombatSfx_ButAllowsRewardAndUiSfx()
        {
            AudioService.EnsureCreated();
            AudioService.SetCombatSuppressed(true);

            AudioService.PlaySfx("hit_enemy"); // 전투 — 막혀야 함
            AudioService.PlaySfx("levelup"); // 보상 — 허용
            AudioService.PlaySfx("ui_confirm"); // UI — 허용

            Assert.That(PlayingClipNames(), Is.EquivalentTo(new[] { "levelup", "ui_confirm" }),
                "억제 중엔 전투 SFX는 새로 재생되면 안 되고, 보상/UI 음은 그대로 재생돼야 한다");
        }

        [Test]
        public void SetCombatSuppressed_True_StopsAlreadyPlayingCombatSfx_PreservesRewardAndUiSfx()
        {
            AudioService.EnsureCreated();
            AudioService.PlaySfx("hit_enemy");
            AudioService.PlaySfx("levelup");
            AudioService.PlaySfx("ui_confirm");
            Assert.That(PlayingClipNames(), Is.EquivalentTo(new[] { "hit_enemy", "levelup", "ui_confirm" }),
                "전제 조건: 억제 전에는 셋 다 재생 중이어야 함");

            AudioService.SetCombatSuppressed(true);

            Assert.That(PlayingClipNames(), Is.EquivalentTo(new[] { "levelup", "ui_confirm" }),
                "억제 진입 시 이미 울리던 전투 SFX(hit_enemy)는 끊기고, 보상/UI 음은 유지돼야 한다");
        }

        [Test]
        public void SetCombatSuppressed_False_RestoresCombatSfx()
        {
            AudioService.EnsureCreated();
            AudioService.SetCombatSuppressed(true);
            AudioService.PlaySfx("hit_enemy");
            Assert.That(PlayingClipNames(), Is.Empty, "전제 조건: 억제 중엔 전투 SFX가 안 나야 함");

            AudioService.SetCombatSuppressed(false);
            AudioService.PlaySfx("hit_enemy");

            Assert.That(PlayingClipNames(), Is.EquivalentTo(new[] { "hit_enemy" }),
                "억제 해제 뒤에는(예: 오버레이 닫힘/새 런 시작) 전투 SFX가 다시 정상 재생돼야 한다");
        }

        [Test]
        public void BuildVoicePool_RoutesSourcesToRealMixerGroups()
        {
            var mixer = Resources.Load<AudioMixer>("Audio/CCTMixer");
            Assert.IsNotNull(mixer, "Assets/_Project/Resources/Audio/CCTMixer.mixer가 있어야 이 테스트가 의미가 있다 " +
                                      "(CCT/Build Audio Mixer (Master Music SFX)로 생성)");

            AudioService.EnsureCreated();
            var sources = AudioService.Instance.GetComponentsInChildren<AudioSource>(true);
            var voiceSources = sources.Where(s => s.name.StartsWith("Voice_")).ToList();
            var musicSources = sources.Where(s => s.name == "MusicA" || s.name == "MusicB").ToList();

            Assert.That(voiceSources, Has.Count.EqualTo(24), "09:80 폴리포니 24 — 보이스 소스 개수 전제");
            Assert.That(musicSources, Has.Count.EqualTo(2));

            Assert.That(voiceSources.All(s => s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "SFX"),
                "모든 SFX 보이스는 실제 AudioMixer의 SFX 그룹으로 라우팅돼야 한다");
            Assert.That(musicSources.All(s => s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "Music"),
                "MusicA/MusicB는 실제 AudioMixer의 Music 그룹으로 라우팅돼야 한다");
        }
    }
}
