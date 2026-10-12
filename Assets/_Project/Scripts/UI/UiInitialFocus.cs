using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CCT
{
    /// <summary>2026-10-12 사용자 피드백: 타이틀 "시작"과 직업 선택 "바텐더"가 화면이 뜨자마자 파란 강조로 고정돼
    /// 보였다(G31 기본 선택이 SpriteSwap selectedSprite/CafeUiFocus 링을 켬). 화면 진입 기본 항목은 미리 고르지 않고
    /// 보관만 했다가, 키보드/패드로 첫 내비게이션(화살표·Tab·Enter·Space·D-pad)을 하면 그때 기본 항목을 선택해
    /// 보여 준다(첫 입력은 이동·실행 없이 기본 항목만 드러냄). 직전 입력이 키보드였으면(키보드로 화면을 넘어온 경우)
    /// 바로 선택해 키보드 전용 흐름을 끊지 않는다. 마우스 클릭은 다시 포인터 모드로 돌린다.</summary>
    [DefaultExecutionOrder(-2000)] // 입력 모듈(EventSystem.Update)보다 먼저 이번 프레임 입력 종류를 기록
    public sealed class UiInitialFocus : MonoBehaviour
    {
        static UiInitialFocus instance;
        static GameObject pending;
        static bool keyboardMode;
        static int evaluatedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; pending = null; keyboardMode = false; evaluatedFrame = -1; }

        /// <summary>직전 UI 입력이 키보드/패드 내비게이션이었는지(같은 프레임 안 실행 순서와 무관하게 계산).</summary>
        public static bool KeyboardMode { get { Evaluate(); return keyboardMode; } }

        /// <summary>화면 진입 시 기본 선택 후보를 넘긴다. 키보드 모드면 즉시 선택, 아니면 선택을 비우고 보관.</summary>
        public static void Offer(GameObject target)
        {
            var es = EventSystem.current;
            if (es == null || target == null) return;
            EnsureRunner();
            if (KeyboardMode) { pending = null; es.SetSelectedGameObject(target); return; }
            pending = target;
            es.SetSelectedGameObject(null);
        }

        /// <summary>포인터로 고른 뒤 포커스를 다음 버튼으로 넘길 때: 키보드 모드에서만 옮기고, 마우스면 선택을 비운다
        /// (클릭한 버튼이 마우스가 떠난 뒤에도 강조로 남지 않게).</summary>
        public static void MoveAfterChoice(GameObject next)
        {
            var es = EventSystem.current;
            if (es == null) return;
            if (KeyboardMode) es.SetSelectedGameObject(next);
            else Offer(next);
        }

        public static GameObject PendingForTests => pending;
        public static void SetKeyboardModeForTests(bool value) { keyboardMode = value; evaluatedFrame = Time.frameCount; }
        /// <summary>테스트용 — 실제 첫 내비게이션 키 입력과 같은 처리.</summary>
        public static void SimulateNavigationForTests() { keyboardMode = true; evaluatedFrame = Time.frameCount; RevealPending(); }

        /// <summary>첫 화면 전에 러너를 만들어 둔다(타이틀 진입 이전 입력도 기록).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureRunner()
        {
            if (instance != null) return;
            var go = new GameObject("UiInitialFocus");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<UiInitialFocus>();
        }

        static void Evaluate()
        {
            if (evaluatedFrame == Time.frameCount) return;
            evaluatedFrame = Time.frameCount;
            if (NavigationPressed()) keyboardMode = true;
            else if (PointerPressed()) keyboardMode = false;
        }

        static bool NavigationPressed()
        {
            var k = Keyboard.current;
            if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame ||
                k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            return pad != null && (pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
                pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame);
        }

        static bool PointerPressed()
        {
            var m = Mouse.current;
            return m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame);
        }

        static void RevealPending()
        {
            var es = EventSystem.current;
            if (es == null || pending == null) return;
            if (es.currentSelectedGameObject == null && pending.activeInHierarchy) es.SetSelectedGameObject(pending);
            pending = null;
        }

        // 매 프레임 입력 종류를 갱신해야 클릭(누른 프레임)과 키 입력을 놓치지 않는다 — 필요할 때만 계산하면
        // wasPressedThisFrame이 이미 지난 프레임이라 마우스 클릭 후에도 키보드 모드가 남았다(2026-10-12 투어 발견).
        void Update() => Evaluate();

        // LateUpdate: 입력 모듈(EventSystem.Update)이 같은 키로 이동/실행을 처리한 뒤에 기본 항목을 드러내므로,
        // 첫 키는 항상 "기본 항목 표시"만 하고 두 번째 키부터 이동/확정한다.
        void LateUpdate()
        {
            if (pending == null) return;
            if (KeyboardMode && NavigationPressed()) RevealPending();
            else if (!pending.activeInHierarchy) pending = null;
        }
    }
}
