using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CCT
{
    /// <summary>
    /// 씬에 아무것도 배치하지 않아도 재생 즉시 게임이 구성되게 하는 진입점 (MVP 단계 임시 구조).
    /// 메뉴 씬 등이 생기면 이 자동 시작을 끄고 씬 기반으로 옮긴다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart() => Build();

        public static GameManager Build()
        {
            if (GameManager.Instance != null) return GameManager.Instance;

            EnsureEventSystem();

            var root = new GameObject("Game");
            var game = root.AddComponent<GameManager>();
            game.Initialize();
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
