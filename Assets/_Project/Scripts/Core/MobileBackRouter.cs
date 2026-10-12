using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-10-12 Android 뒤로 가기. Input System은 안드로이드 뒤로 버튼을 Keyboard.escapeKey로 보낸다.
    /// - 런 진행 중: GameManager.RequestEscape(기존 Esc 우선순위 — 창 닫기 → 일시정지 열기/닫기)가 그대로 처리한다.
    /// - 메뉴 화면(타이틀·직업 선택·도감·설정·제작진·결과): 맨 위에 보이는 닫기/뒤로/취소 버튼을 누른 것과 같게 한다.
    ///   그런 버튼이 없고 타이틀이면 "종료" 버튼 경로(QuitGuard 미저장 확인 포함)를 탄다.
    /// 영구 강화 화면은 자체 Esc 처리가 있어 건드리지 않는다. 터치 UI(모바일)에서만 설치 — 데스크톱 Esc 동작은 그대로.
    /// </summary>
    public class MobileBackRouter : MonoBehaviour
    {
        static MobileBackRouter instance;
        static readonly string[] BackNames = { "Cancel", "CloseButton", "Close", "BackButton", "TitleButton" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void Install()
        {
            if (instance != null || !Application.isPlaying || !MobilePlatform.IsTouchUI) return;
            var go = new GameObject("MobileBackRouter");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MobileBackRouter>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HandleBack();
        }

        /// <summary>뒤로 가기 1회. 처리한 버튼 이름(타이틀 종료면 "QuitButton"), 처리 안 했으면 null. 시험이 직접 부른다.</summary>
        public static string HandleBack()
        {
            var game = GameManager.Instance;
            if (game != null && game.State != GameState.GameOver) return null; // GameManager가 Esc로 처리
            if (Object.FindObjectsByType<PermanentSkillView>(FindObjectsSortMode.None).Any(v => v.IsOpen)) return null;

            var target = TopmostBackButton();
            if (target == null)
            {
                var title = Object.FindFirstObjectByType<TitleView>();
                if (title != null && title.IsOpen)
                    target = title.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "QuitButton" && b.interactable);
            }
            if (target == null) return null;
            target.onClick.Invoke();
            return target.name;
        }

        /// <summary>정렬 순서가 가장 높은 캔버스에서, 그리기 순서상 마지막(=맨 위)인 활성 뒤로 버튼.</summary>
        static Button TopmostBackButton()
        {
            Button best = null;
            int bestOrder = int.MinValue;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled) continue;
                foreach (var b in canvas.GetComponentsInChildren<Button>(false)) // 깊이 우선 = 그리기 순서
                {
                    if (!b.interactable || System.Array.IndexOf(BackNames, b.name) < 0) continue;
                    var group = b.GetComponentInParent<CanvasGroup>();
                    if (group != null && (group.alpha <= 0.01f || !group.interactable)) continue;
                    if (canvas.sortingOrder >= bestOrder) { best = b; bestOrder = canvas.sortingOrder; }
                }
            }
            return best;
        }
    }
}
