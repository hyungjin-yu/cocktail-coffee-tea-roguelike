using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CCT
{
    /// <summary>09장 전체 — SFX/BGM 재생 창구. 딱 한 번 만들어져 런이 재시작돼도 살아남는다
    /// (GameBootstrap이 EventSystem과 같은 패턴으로 한 번만 생성). 모든 게임 코드는 이 클래스의
    /// static 메서드만 호출하고, 실제 AudioSource/풀링/우선순위는 여기 안에 숨겨져 있다.</summary>
    public class AudioService : MonoBehaviour
    {
        public enum MusicState { None, Menu, Run, Boss }

        public static AudioService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        /// <summary>09:80 "폴리포니 전체24, 같은ID4".</summary>
        const int MaxTotalVoices = 24;
        const int MaxVoicesPerId = 4;
        const float CrossfadeSeconds = 0.5f;

        class Voice
        {
            public AudioSource source;
            public string id;
            public int priority;
            public float startTime;
        }

        readonly List<Voice> activeVoices = new List<Voice>();
        readonly Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>();
        readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        AudioSource musicA;
        AudioSource musicB;
        bool musicAActive;
        MusicState currentMusicState = MusicState.None;
        Coroutine musicFadeRoutine;

        /// <summary>09:80 우선순위: 결과/경고 > 플레이어피격 > 선택 > 공격/적/픽업.</summary>
        static int PriorityOf(string id) => id switch
        {
            "clear" or "defeat" or "boss_warning" => 3,
            "hit_player" => 2,
            "ui_focus" or "ui_confirm" or "ui_cancel" or "ui_denied"
                or "levelup" or "skill_unlock" or "hybrid_unlock" => 1,
            _ => 0, // swing_*/shot_*/hit_enemy/enemy_defeat/pickup/explosion/steam/spell 등
        };

        /// <summary>09:80 "중복 hit_enemy/pickup은50ms 안에1개로 합산, 기타 동일이벤트는30ms 간격".</summary>
        static float DedupWindow(string id) => id is "hit_enemy" or "pickup" ? 0.05f : 0.03f;

        public static AudioService EnsureCreated()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioService");
            Instance = go.AddComponent<AudioService>();
            // Boot has no camera/listener; audio must also work on title and selection screens.
            go.AddComponent<AudioListener>();
            Instance.BuildVoicePool();
            // DontDestroyOnLoad는 Play 모드 전용 API라 EditMode 테스트에서 호출하면 예외가 난다
            // (실측 확인됨). 이 프로젝트엔 씬 전환이 없지만, GameManager 재시작(오브젝트 파괴)에도 안
            // 딸려가게 하려는 목적이므로 Play 모드가 아닐 때(EditMode 테스트)는 건너뛴다 — 그래야
            // AudioService의 순수 로직(우선순위/디듀프/StopCombatSfx 등)을 EditMode로 검증할 수 있다.
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return Instance;
        }

        /// <summary>09:71 "Unity AudioMixer의 Master/Music/SFX 그룹 분리" — 실제 .mixer 에셋
        /// (Tools/CCT/Build Audio Mixer로 생성, Assets/_Project/Resources/Audio/CCTMixer.mixer)의
        /// Music/SFX 그룹으로 각 AudioSource를 라우팅한다. 그룹 페이더 자체는 0dB(단위 이득)로 둬서
        /// AudioBus가 AudioSource.volume에 곱하는 실시간 볼륨/덕킹 계산을 그대로 통과시킨다 — 볼륨 숫자
        /// 제어는 여전히 AudioBus 책임, 이 믹서는 그룹 구조·라우팅만 담당. 믹서 에셋이 없어도(빌드 전
        /// 미실행 등) outputAudioMixerGroup을 null로 두고 계속 동작한다(그룹 미분리 상태로 대체).</summary>
        AudioMixerGroup musicMixerGroup;
        AudioMixerGroup sfxMixerGroup;

        void BuildVoicePool()
        {
            var mixer = Resources.Load<AudioMixer>("Audio/CCTMixer");
            if (mixer != null)
            {
                var musicGroups = mixer.FindMatchingGroups("Music");
                var sfxGroups = mixer.FindMatchingGroups("SFX");
                musicMixerGroup = musicGroups.Length > 0 ? musicGroups[0] : null;
                sfxMixerGroup = sfxGroups.Length > 0 ? sfxGroups[0] : null;
            }
            else
            {
                Debug.LogWarning("[CCT] Resources/Audio/CCTMixer.mixer를 못 찾음 — AudioMixer 그룹 라우팅 없이 진행합니다. " +
                                  "CCT/Build Audio Mixer (Master Music SFX) 메뉴로 생성하세요.");
            }

            for (int i = 0; i < MaxTotalVoices; i++)
            {
                var src = new GameObject($"Voice_{i}").AddComponent<AudioSource>();
                src.transform.SetParent(transform, false);
                src.playOnAwake = false;
                src.spatialBlend = 0f; // 2D — pan은 stereoPan으로 수동 계산(09:83)
                src.outputAudioMixerGroup = sfxMixerGroup;
            }
            musicA = MakeMusicSource("MusicA");
            musicB = MakeMusicSource("MusicB");
        }

        AudioSource MakeMusicSource(string name)
        {
            var src = new GameObject(name).AddComponent<AudioSource>();
            src.transform.SetParent(transform, false);
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.outputAudioMixerGroup = musicMixerGroup;
            return src;
        }

        AudioClip LoadClip(string id)
        {
            if (clipCache.TryGetValue(id, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>($"Audio/v1/{id}");
            clipCache[id] = clip; // null도 캐싱 — 없는 파일을 매번 다시 찾지 않는다
            return clip;
        }

        /// <summary>09장 SFX 재생 진입점. worldPos가 있으면 화면 x위치로 좌우 팬(09:83), 없으면(UI음)
        /// 중앙. 무적 상태에서 hit_player를 재생하지 않는 것 등 "피해0이면 재생 안 함" 판단은 호출부
        /// (PlayerController 등 실제 피해 발생 지점) 책임이다 — 이 메서드는 "이미 낼 만한 소리"만 받는다.</summary>
        public static void PlaySfx(string id, Vector2? worldPos = null)
        {
            var svc = Instance;
            if (svc == null) return; // 아직 준비 전(예: 테스트 환경)이면 조용히 무시
            svc.PlaySfxInternal(id, worldPos);
        }

        void PlaySfxInternal(string id, Vector2? worldPos)
        {
            // 09:139 "Pause/보상창은 ... 전투SFX 중단" — Time.timeScale=0라 대부분의 전투 트리거가
            // 자연히 안 일어나지만, 그건 우연한 부작용이지 보장이 아니다(예: WaitForSecondsRealtime로
            // 도는 코드는 timeScale과 무관하게 계속 흐른다). CombatSuppressed는 그 보장을 명시적으로 건다 —
            // UI/보상/결과 음(AlwaysAllowedIds)은 그 상태에서도 계속 나야 하므로 예외로 둔다.
            if (CombatSuppressed && !AlwaysAllowedIds.Contains(id)) return;

            float now = Time.unscaledTime;
            if (lastPlayedAt.TryGetValue(id, out var last) && now - last < DedupWindow(id))
                return; // 09:80 — 너무 가까운 중복 재생은 합산(스킵)

            var clip = LoadClip(id);
            if (clip == null) return; // 리소스가 없으면 조용히 무시(에디터 경고는 남김)

            // A source may finish before Update, then be reused during the same frame.
            // Remove its previous owner before limits or Pause inspect the voice list.
            PruneFinishedVoices();
            int sameIdCount = activeVoices.FindAll(v => v.id == id).Count;
            if (sameIdCount >= MaxVoicesPerId) return; // 09:80 "같은ID4"

            AudioSource freeSource = FindFreeSource();
            int newPriority = PriorityOf(id);
            if (freeSource == null)
            {
                // 09:80 전체24 한도 — 더 낮은 우선순위의 목소리를 빼앗는다. 뺏을 게 없으면 이 재생은 버린다.
                Voice lowest = null;
                foreach (var v in activeVoices)
                    if (lowest == null || v.priority < lowest.priority) lowest = v;
                if (lowest == null || lowest.priority >= newPriority) return;
                activeVoices.Remove(lowest);
                lowest.source.Stop();
                freeSource = lowest.source;
            }

            lastPlayedAt[id] = now;
            freeSource.clip = clip;
            freeSource.volume = AudioBus.EffectiveSfxVolume;
            // 09:83 — 좌우 pan은 발음 위치의 화면정규화 x×0.5, UI는 중앙. 보스 경고는 중앙/최우선.
            freeSource.panStereo = (worldPos.HasValue && id != "boss_warning") ? ScreenPan(worldPos.Value) : 0f;
            freeSource.Play();
            activeVoices.Add(new Voice { source = freeSource, id = id, priority = newPriority, startTime = now });
        }

        static float ScreenPan(Vector2 worldPos)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            float viewportX = cam.WorldToViewportPoint(worldPos).x;
            return Mathf.Clamp((viewportX - 0.5f), -0.5f, 0.5f);
        }

        AudioSource FindFreeSource()
        {
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Voice_")) continue;
                var src = child.GetComponent<AudioSource>();
                if (src != null && !src.isPlaying) return src;
            }
            return null;
        }

        void PruneFinishedVoices() =>
            activeVoices.RemoveAll(v => v.source == null || !v.source.isPlaying);

        void Update()
        {
            PruneFinishedVoices();
            float sfxVol = AudioBus.EffectiveSfxVolume;
            foreach (var v in activeVoices)
                if (v.source != null) v.source.volume = sfxVol;

            // 크로스페이드 코루틴이 도는 동안은 그쪽이 볼륨을 몰고 가게 두고(안 건드림), 아닐 때만
            // 현재 활성 음악 소스가 덕킹 등 실시간 볼륨 변화를 계속 따라가게 한다.
            if (musicFadeRoutine == null)
            {
                var active = musicAActive ? musicA : musicB;
                if (active.isPlaying) active.volume = AudioBus.EffectiveMusicVolume;
            }
        }

        /// <summary>09:135 "전환: Title/선택/도감/강화는 menu, Playing 일반은 run, 보스 생존은 boss.
        /// 0.5초 crossfade." 같은 상태를 다시 요청하면 아무 일도 안 한다.</summary>
        public static void SetMusicState(MusicState state)
        {
            var svc = Instance;
            if (svc == null || svc.currentMusicState == state) return;
            svc.currentMusicState = state;
            var clip = state switch
            {
                MusicState.Menu => svc.LoadClip("bgm_menu"),
                MusicState.Run => svc.LoadClip("bgm_run"),
                MusicState.Boss => svc.LoadClip("bgm_boss"),
                _ => null,
            };
            svc.CrossfadeTo(clip);
        }

        void CrossfadeTo(AudioClip clip)
        {
            if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = StartCoroutine(CrossfadeRoutine(clip));
        }

        IEnumerator CrossfadeRoutine(AudioClip clip)
        {
            var from = musicAActive ? musicA : musicB;
            var to = musicAActive ? musicB : musicA;
            musicAActive = !musicAActive;

            to.clip = clip;
            to.volume = 0f;
            if (clip != null) to.Play();

            float t = 0f;
            float fromStart = from.volume;
            while (t < CrossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / CrossfadeSeconds);
                float target = AudioBus.EffectiveMusicVolume;
                // QA 2026-10-04: 페이드 중에도 현재 버스(마스터/BGM 0 = 즉시 무음)를 매 틱 반영한다 —
                // 시작 시 캡처한 볼륨이 현재 버스 상한을 넘지 않게 한다.
                from.volume = Mathf.Min(Mathf.Lerp(fromStart, 0f, k), target);
                to.volume = Mathf.Lerp(0f, target, k);
                yield return null;
            }
            from.Stop();
            from.volume = 0f;
            to.volume = AudioBus.EffectiveMusicVolume;
            musicFadeRoutine = null;
        }

        /// <summary>09:139 "Pause/보상창은 음악 계속 재생하되 Music-8dB". Time.timeScale=0라 이후
        /// 새 전투 SFX 트리거(피격/처치 등)는 자연히 안 일어나지만, Pause를 여는 그 순간 이미
        /// 재생 "중"이던 전투 SFX(예: steam 0.8초처럼 긴 소리)는 AudioSource가 timeScale과 무관하게
        /// 계속 흘러가므로 별도로 멈춰야 한다 — StopCombatSfx가 그 역할.</summary>
        public static void SetMusicDucked(bool ducked) => AudioBus.MusicDucked = ducked;

        /// <summary>Pause/보상창/결과 화면이 열려 있는 동안에도 계속 나야 하는 소리 — UI 조작음
        /// (ui_focus/confirm/cancel/denied)과 그 화면들 자체가 트리거하는 보상·결과 음(levelup/
        /// skill_unlock/hybrid_unlock/clear/defeat). 이 목록은 "이미 재생 중인 소리를 끊을 때"(StopCombatSfx)와
        /// "새로 재생을 시도할 때"(PlaySfxInternal의 CombatSuppressed 검사) 양쪽에서 같은 기준으로 쓰인다.</summary>
        static readonly HashSet<string> AlwaysAllowedIds = new HashSet<string>
        {
            "ui_focus", "ui_confirm", "ui_cancel", "ui_denied",
            "levelup", "skill_unlock", "hybrid_unlock", "clear", "defeat",
        };

        /// <summary>09:139 "Pause/보상창은 ... 전투SFX 중단"을 명시적으로 게이트하는 플래그. true인 동안
        /// PlaySfxInternal은 AlwaysAllowedIds 외의 새 SFX 재생 요청을 전부 무시한다(Time.timeScale=0에
        /// 의존한 우연한 차단이 아니라 명시적 차단). GameManager가 Pause/LevelUp/SkillSelect/HybridOffer
        /// 진입·해제와 결과 화면에서 이 값을 관리한다.</summary>
        public static bool CombatSuppressed { get; private set; }

        /// <summary>억제를 걸 때는 이미 재생 중이던 전투 SFX도 즉시 끊는다(진입 순간 울리던 소리까지
        /// 대상). 억제를 풀 때는 그냥 플래그만 내린다 — 그 다음 트리거부터 다시 정상 재생된다.</summary>
        public static void SetCombatSuppressed(bool suppressed)
        {
            CombatSuppressed = suppressed;
            if (suppressed) StopCombatSfx();
        }

        /// <summary>AlwaysAllowedIds만 남기고 이미 재생 중인 보이스를 즉시 끊는다. 09:139 "전투SFX 중단"은
        /// 새 트리거뿐 아니라 진입 시점에 이미 울리고 있던 소리도 대상이다.</summary>
        public static void StopCombatSfx()
        {
            var svc = Instance;
            if (svc == null) return;
            for (int i = svc.activeVoices.Count - 1; i >= 0; i--)
            {
                var v = svc.activeVoices[i];
                if (AlwaysAllowedIds.Contains(v.id)) continue;
                if (v.source != null) v.source.Stop();
                svc.activeVoices.RemoveAt(i);
            }
        }

        /// <summary>09:139 "결과는0.5초 fadeout+clear/defeat,1.5초 후 menu". 새 런이 이 시퀀스 도중
        /// 곧바로 시작돼 SetMusicState가 불려도(예: 결과 직후 빠른 재시작) 서로 남의 볼륨을 덮어쓰지
        /// 않도록, 크로스페이드와 결과 시퀀스는 같은 musicFadeRoutine 핸들을 공유해 서로를 취소한다.</summary>
        public static void PlayResultStingerThenReturnToMenu(bool cleared)
        {
            var svc = Instance;
            if (svc == null) return;
            if (svc.musicFadeRoutine != null) svc.StopCoroutine(svc.musicFadeRoutine);
            // SetMusicState는 currentMusicState==state면 아무 것도 안 하고 return한다(09:135 "같은 상태를
            // 다시 요청하면 아무 일도 안 한다") — 결과 시퀀스 도중(1.5초 대기 포함) 같은 상태로 새 런이
            // 곧바로 시작되면(예: Run 종료 직후 Run 재시작) 그 guard에 막혀 CrossfadeTo가 아예 안 불리고,
            // 이 코루틴이 끝까지 돌아 나중에 Menu로 덮어써 버린다. 그래서 결과 시퀀스에 들어가는 순간
            // currentMusicState를 None으로 미리 비워, 어떤 상태로 재진입해도 "다른 상태 요청"으로
            // 인식되게 한다.
            svc.currentMusicState = MusicState.None;
            svc.musicFadeRoutine = svc.StartCoroutine(svc.ResultSequence(cleared));
        }

        IEnumerator ResultSequence(bool cleared)
        {
            var active = musicAActive ? musicA : musicB;
            // 이 결과 시퀀스가 "이전 크로스페이드가 아직 안 끝난 도중"에 시작됐다면(위 PlayResultStingerThenReturnToMenu가
            // 그 크로스페이드 코루틴을 이미 멈췄지만) 반대쪽 소스가 중간 볼륨으로 재생 중인 채 방치될 수 있다.
            // active 하나만 관리하는 이 코루틴이 그 잔여 소스까지 확실히 끈다.
            var other = musicAActive ? musicB : musicA;
            if (other.isPlaying) { other.Stop(); other.volume = 0f; }

            float start = active.volume;
            float t = 0f;
            // musicFadeRoutine이 이 코루틴 자신을 가리키는 동안은 Update()가 active.volume을
            // AudioBus.EffectiveMusicVolume으로 되돌리지 않는다(Update의 가드 참고) — 그래서 아래
            // 페이드가 매 프레임 Update한테 덮어써지는 경쟁 없이 그대로 유지된다.
            while (t < CrossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                active.volume = Mathf.Min(Mathf.Lerp(start, 0f, t / CrossfadeSeconds), AudioBus.EffectiveMusicVolume);
                yield return null;
            }
            active.Stop();
            active.volume = 0f;
            currentMusicState = MusicState.None;
            PlaySfxInternal(cleared ? "clear" : "defeat", null);
            yield return new WaitForSecondsRealtime(1.5f);
            SetMusicState(MusicState.Menu); // CrossfadeTo가 musicFadeRoutine을 새 코루틴으로 교체한다
        }
    }
}
