using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>09 §4 ui_focus SFX. 이 코드베이스엔 방향키/Tab으로 하이라이트가 옮겨 다니는 전체 키보드
    /// 내비게이션 시스템이 없지만(08 나머지 범위, 2026-09-27 확인), Unity EventSystem은 Selectable에
    /// 이미 OnSelect(키보드/게임패드 포커스 이동, Tab 포함)와 마우스 hover(OnPointerEnter)를 보내주므로
    /// 이 작은 컴포넌트 하나만 각 Button에 붙이면 "포커스 이동" 이벤트에 훅을 걸 수 있다 — 새 내비게이션
    /// 시스템을 만들 필요가 없다. interactable=false인 버튼(예: 잠긴 스킬)에는 소리를 내지 않는다.</summary>
    public class UiFocusSfx : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnSelect(BaseEventData eventData) => TryPlay();
        public void OnPointerEnter(PointerEventData eventData) => TryPlay();

        void TryPlay()
        {
            if (selectable != null && !selectable.interactable) return;
            AudioService.PlaySfx("ui_focus");
        }
    }
}
