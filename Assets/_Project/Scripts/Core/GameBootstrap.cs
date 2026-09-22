using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CCT
{
    /// <summary>
    /// 씬에 아무것도 배치하지 않아도 재생 즉시 직업/무기 선택 → 게임이 구성되게 하는 진입점 (MVP 단계 임시 구조).
    /// 메뉴 씬 등이 생기면 이 자동 시작을 끄고 씬 기반으로 옮긴다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart() => RestartToSelect();

        /// <summary>직업/무기 선택 화면부터 다시 띄운다. 재시작 시에도 이걸 탄다 (매 런 새로 고른다).</summary>
        public static void RestartToSelect()
        {
            if (GameManager.Instance != null) return;
            EnsureEventSystem();
            CharacterSelectView.Show((job, weapon) => Build(job, weapon));
        }

        /// <summary>선택 화면 없이 바로 게임을 만든다. 테스트나 직업/무기가 이미 정해진 경우에 쓴다.</summary>
        public static GameManager Build(Job job = Job.Bartender, WeaponKind weapon = WeaponKind.Rapier)
        {
            if (GameManager.Instance != null) return GameManager.Instance;

            EnsureEventSystem();

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
