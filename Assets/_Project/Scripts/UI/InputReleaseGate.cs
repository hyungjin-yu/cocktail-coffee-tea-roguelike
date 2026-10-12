using UnityEngine.InputSystem;

namespace CCT
{
    /// <summary>
    /// 08:39 "선택을 닫은 키/클릭이 다음 화면을 즉시 눌러버리지 않도록 눌림 해제까지 동일 액션 재사용을
    /// 막는다." 보상 화면은 같은 호출 안에서 연달아 열리므로(07:46), 화면을 연 프레임과 그 뒤 확정 입력
    /// (Enter/Space/숫자키/마우스 왼쪽)이 눌려 있는 동안은 사용자 입력 경로(버튼 클릭·Submit·단축키)를
    /// 잠근다. 테스트가 쓰는 공개 API(Choose 등)는 이 게이트를 거치지 않는다.
    /// 판정 로직은 프레임 번호와 "눌림 여부"만 받는 순수 함수라 EditMode에서 검증한다.
    /// </summary>
    public class InputReleaseGate
    {
        int armedFrame = -1;

        public bool Locked { get; private set; }

        /// <summary>화면을 연 프레임에 호출.</summary>
        public void Arm(int frame)
        {
            armedFrame = frame;
            Locked = true;
        }

        /// <summary>매 프레임(timeScale과 무관하게 Update에서) 호출. 연 프레임이 지났고 확정 입력이 모두
        /// 떨어져 있으면 잠금을 푼다.</summary>
        public void Poll(int frame, bool anyConfirmHeld)
        {
            if (Locked && frame > armedFrame && !anyConfirmHeld) Locked = false;
        }

        /// <summary>사용자 입력 경로에서 확인 — 이번 프레임 상태를 반영한 뒤 허용 여부.</summary>
        public bool Allows(int frame, bool anyConfirmHeld)
        {
            Poll(frame, anyConfirmHeld);
            return !Locked;
        }

        public static bool AnyConfirmHeld()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed || kb.spaceKey.isPressed) return true;
                if (kb.digit1Key.isPressed || kb.digit2Key.isPressed || kb.digit3Key.isPressed ||
                    kb.digit4Key.isPressed || kb.digit5Key.isPressed || kb.digit6Key.isPressed) return true;
                if (kb.numpad1Key.isPressed || kb.numpad2Key.isPressed || kb.numpad3Key.isPressed) return true;
            }
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }
}
