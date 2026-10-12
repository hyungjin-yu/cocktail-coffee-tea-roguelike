using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    // Decorative only: does not alter navigation, clicks, purchases, or selection ownership.
    public sealed class CafeUiFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        Button button;
        Image ring;
        bool hovered, focused, chosen;

        public void Initialize(Button owner, Sprite sprite)
        {
            button = owner;
            var rt = UiFactory.Stretch("CafeFocus", transform);
            rt.SetAsFirstSibling();
            ring = rt.gameObject.AddComponent<Image>();
            ring.sprite = sprite;
            ring.type = Image.Type.Sliced;
            ring.pixelsPerUnitMultiplier = 100f / 128f;
            ring.raycastTarget = false;
            Refresh();
        }
        public void SetChosen(bool value) { chosen = value; Refresh(); }
        public bool ChosenForTests => chosen;
        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Refresh(); }
        public void OnSelect(BaseEventData e) { focused = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { focused = false; Refresh(); }
        void OnDisable() { hovered = focused = false; Refresh(); }
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (ring != null) ring.enabled = chosen || (button != null && button.interactable && (hovered || focused));
        }
    }
}
