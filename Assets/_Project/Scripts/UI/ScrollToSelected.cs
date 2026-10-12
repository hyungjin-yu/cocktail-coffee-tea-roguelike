using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// G31 키보드 접근: EventSystem 선택이 스크롤 콘텐츠 안의 항목으로 옮겨지면 그 항목이 뷰포트 안에 보이도록
    /// 세로 스크롤을 맞춘다(방향키/Tab으로 도감 아래쪽 행까지 도달 가능). 마우스 휠 스크롤은 ScrollRect 그대로.
    /// </summary>
    public class ScrollToSelected : MonoBehaviour
    {
        ScrollRect scroll;
        RectTransform content;
        RectTransform viewport;
        GameObject lastSelected;

        public void Init(ScrollRect scrollRect, RectTransform contentRt, RectTransform viewportRt)
        {
            scroll = scrollRect;
            content = contentRt;
            viewport = viewportRt;
        }

        void LateUpdate()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null || selected == lastSelected) return;
            lastSelected = selected;
            if (selected.transform is RectTransform rt && rt.IsChildOf(content)) Reveal(rt);
        }

        /// <summary>항목이 뷰포트 위/아래로 벗어나 있으면 content.anchoredPosition.y를 옮긴다. 테스트도 이 경로를 쓴다.</summary>
        public void Reveal(RectTransform item)
        {
            if (scroll == null || content == null || viewport == null) return;
            float viewHeight = viewport.rect.height;
            // content 기준 항목의 위·아래(위가 0, 아래로 음수)
            float itemTop = item.anchoredPosition.y + item.rect.height * (1f - item.pivot.y);
            float itemBottom = itemTop - item.rect.height;
            float scrollY = content.anchoredPosition.y; // 아래로 스크롤할수록 커진다
            float visibleTop = -scrollY;
            float visibleBottom = visibleTop - viewHeight;
            float maxScroll = Mathf.Max(0f, content.rect.height - viewHeight);
            if (itemTop > visibleTop) scrollY = -itemTop;
            else if (itemBottom < visibleBottom) scrollY = -(itemBottom + viewHeight);
            else return;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(scrollY, 0f, maxScroll));
        }
    }
}
