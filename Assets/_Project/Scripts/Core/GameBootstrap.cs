using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CCT
{
    /// <summary>
    /// 씬에 아무것도 배치하지 않아도 재생 즉시 타이틀 → 직업/무기 선택 → 게임이 구성되게 하는 진입점
    /// (MVP 단계 임시 구조). 메뉴 씬 등이 생기면 이 자동 시작을 끄고 씬 기반으로 옮긴다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            // -cct-data-dir: save/settings를 실제 사용자 폴더 대신 지정 폴더로(빌드 검증용 격리). -cct-autotour: 자동 캡처.
            LaunchOptions.Apply();
            QuitGuard.Install(); // 08:178 OS 창 닫기도 미저장 확인
            // 08:22 Boot — UI를 열기 전에 settings.json(없으면 구형 PlayerPrefs 이전값)을 오디오/화면/VSync에 적용한다.
            SettingsService.ApplyAtBoot();
            ShowTitle();
        }

        /// <summary>08장 "### 타이틀" — "처음 실행도 타이틀을 거친다". 재시작(R)이나 결과 화면의
        /// "다시 시작"은 타이틀을 다시 거치지 않고 RestartToSelect로 바로 간다 — 타이틀은 최초 진입과
        /// 결과 화면의 "타이틀" 버튼에서만 보인다.</summary>
        public static void ShowTitle()
        {
            if (GameManager.Instance != null) return;
            EnsureEventSystem();
            AudioService.EnsureCreated();
            AudioService.SetMusicState(AudioService.MusicState.Menu);
            TitleView.Show();
        }

        /// <summary>직업/무기 선택 화면부터 다시 띄운다. 재시작 시에도 이걸 탄다 (매 런 새로 고른다).</summary>
        public static void RestartToSelect()
        {
            if (GameManager.Instance != null) return;
            EnsureEventSystem();
            AudioService.EnsureCreated();
            AudioService.SetMusicState(AudioService.MusicState.Menu); // 09:135 "선택/도감/강화는 menu"
            CharacterSelectView.Show((job, weapon) => StartRunWithFirstTimeGate(job, weapon));
        }

        /// <summary>08:70~72 "첫 런 안내" — 전투를 시작하기 직전, 딱 한 번만 자동으로 보여준다. 이미 봤으면
        /// (FirstRunTutorial.Seen) 바로 Build로 넘어간다. 안내가 떠 있는 동안은 GameManager가 아직
        /// 존재하지 않으므로 "첫 안내 중 런 시간은0"이 별도 처리 없이 그대로 성립한다.</summary>
        static void StartRunWithFirstTimeGate(Job job, WeaponKind weapon)
        {
            if (FirstRunTutorial.Seen)
            {
                Build(job, weapon);
                return;
            }

            var canvas = UiFactory.CreateCanvas(null, "FirstRunTutorial", 100);
            var view = FirstRunTutorialView.Create(canvas.transform);
            view.Show("시작", () =>
            {
                FirstRunTutorial.MarkSeen();
                Object.Destroy(canvas.gameObject);
                Build(job, weapon);
            });
        }

        /// <summary>선택 화면 없이 바로 게임을 만든다. 테스트나 직업/무기가 이미 정해진 경우에 쓴다.</summary>
        public static GameManager Build(Job job = Job.Bartender, WeaponKind weapon = WeaponKind.Rapier)
        {
            if (GameManager.Instance != null) return GameManager.Instance;

            EnsureEventSystem();
            AudioService.EnsureCreated();

            var root = new GameObject("Game");
            var game = root.AddComponent<GameManager>();
            game.Initialize(job, weapon);
            return game;
        }

        // 재시작 시 파괴되지 않도록 Game 루트 밖에 둔다.
        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
