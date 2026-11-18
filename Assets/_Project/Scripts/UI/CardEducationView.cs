using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    public static class CardEducationIcons
    {
        public static Sprite For(CardData card) => !string.IsNullOrEmpty(card.educationArtId)
            ? Resources.Load<Sprite>("CardEducation/" + card.educationArtId) ?? CardSelectView.CategoryIcon(card.category)
            : CardSelectView.CategoryIcon(card.category);
    }

    /// <summary>Scrollable, modal learning panel. It never changes game pause state or chooses a card.</summary>
    public class CardEducationView : MonoBehaviour
    {
        Text title, body;
        RectTransform content;
        /// <summary>테스트용 — 지금 보이는 출처 영역의 주소 복사 버튼.</summary>
        public readonly System.Collections.Generic.List<Button> SourceButtons = new System.Collections.Generic.List<Button>();
        public RectTransform SourcesRect => content != null ? content.Find("Sources") as RectTransform : null;
        public RectTransform BodyRect => body != null ? body.rectTransform : null;
        ScrollRect scroll;
        Button close;
        GameObject returnFocus;
        Action closed;
        public bool IsOpen => gameObject.activeSelf;
        public string BodyText => body.text;
        public static CardEducationView Create(Transform parent, Action onClosed)
        {
            var root = UiFactory.Stretch("CardLearningDetail", parent);
            UiFactory.AddImage(root, new Color(0.02f,0.02f,0.03f,0.99f));
            var v = root.gameObject.AddComponent<CardEducationView>();
            v.closed = onClosed;
            var panel = UiFactory.Place("Panel", root, new Vector2(.5f,.5f), Vector2.zero, new Vector2(1500,900));
            UiFactory.SkinPanel(UiFactory.AddImage(panel,new Color(.1f,.08f,.12f)));
            v.title=UiFactory.AddText(UiFactory.Place("Title",panel,new Vector2(.5f,1),new Vector2(0,-28),new Vector2(1380,90)),"",40,TextAnchor.MiddleCenter,Color.white);
            UiFactory.AddText(UiFactory.Place("ScrollHint",panel,new Vector2(.5f,1),new Vector2(0,-100),new Vector2(1300,32)),
                MobilePlatform.IsTouchUI ? "본문을 위아래로 밀어 지식과 출처 보기" : "휠 · 위아래 키 · 패드 오른쪽 스틱으로 지식과 출처 보기",22,TextAnchor.MiddleCenter,new Color(.7f,.8f,.85f));
            var area=UiFactory.Place("Scroll",panel,new Vector2(.5f,1),new Vector2(0,-144),new Vector2(1340,592)); // 2026-11-15: 안내 줄(-100~-148 글 상자) 아래에서 시작 — 스크롤한 본문이 안내 글 밑으로 겹쳐 보이던 문제
            UiFactory.AddImage(area,new Color(0,0,0,.15f));
            var viewport=UiFactory.Stretch("Viewport",area);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 2026-11-15 피드백 10: 스크롤 내용 = 본문 글(위) + 출처 영역(아래, 작은 보조 글꼴·사이트명·주소 복사). 이전에는 긴 URL이 본문 끝에 그대로 붙었다.
            var content=UiFactory.Place("Content",viewport,new Vector2(.5f,1),Vector2.zero,new Vector2(1290,600));
            v.content=content;
            v.body=UiFactory.AddText(UiFactory.Place("Body",content,new Vector2(.5f,1),Vector2.zero,new Vector2(1290,600)),"",30,TextAnchor.UpperLeft,new Color(.94f,.93f,.9f));
            v.body.supportRichText=false;
            v.scroll=area.gameObject.AddComponent<ScrollRect>();
            v.scroll.content=content;v.scroll.viewport=viewport;v.scroll.horizontal=false;
            v.scroll.movementType=ScrollRect.MovementType.Clamped;v.scroll.scrollSensitivity=45;
            var b=UiFactory.Place("Close",panel,new Vector2(.5f,0),new Vector2(0,40),new Vector2(420,90));
            var bg=UiFactory.AddImage(b,new Color(.25f,.32f,.36f));
            v.close=b.gameObject.AddComponent<Button>();v.close.targetGraphic=bg;
            v.close.navigation=new Navigation {mode=Navigation.Mode.None};
            v.close.onClick.AddListener(v.Close);
            UiFactory.AddText(UiFactory.Stretch("Label",b),"닫기 · 카드 선택으로",30,TextAnchor.MiddleCenter,Color.white);
            root.gameObject.SetActive(false);return v;
        }
        public void Show(CardData card)
        {
            returnFocus=EventSystem.current?.currentSelectedGameObject;
            title.text=card.displayName;body.text=CardEducationData.Body(card);
            gameObject.SetActive(true);transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            float bodyH=Mathf.Ceil(body.preferredHeight)+24;
            body.rectTransform.sizeDelta=new Vector2(1290,bodyH);
            var old=content.Find("Sources"); if(old!=null){old.SetParent(null,false);if(Application.isPlaying)Destroy(old.gameObject);else DestroyImmediate(old.gameObject);}
            SourceButtons.Clear();
            float srcH=CardSourceList.Build(content,-bodyH-8,1290,card,SourceButtons);
            content.sizeDelta=new Vector2(1290,Mathf.Max(600,bodyH+8+srcH+24));
            scroll.verticalNormalizedPosition=1;
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }
        public void Close()
        {
            if(!IsOpen)return;
            if(GameManager.Instance!=null && GameManager.Instance.IsPaused)return;
            gameObject.SetActive(false);closed?.Invoke();
            if(returnFocus!=null)EventSystem.current?.SetSelectedGameObject(returnFocus);
        }
        void Update()
        {
            if(GameManager.Instance!=null && GameManager.Instance.IsPaused)return;
            var k=Keyboard.current;var pad=Gamepad.current;
            float direction=k!=null && (k.downArrowKey.isPressed || k.pageDownKey.isPressed)?-1:k!=null && (k.upArrowKey.isPressed || k.pageUpKey.isPressed)?1:0;
            if(pad!=null)direction+=pad.rightStick.ReadValue().y;
            float overflow=scroll.content.rect.height-scroll.viewport.rect.height;
            if(overflow>0)scroll.verticalNormalizedPosition+=direction*600*Time.unscaledDeltaTime/overflow;
        }
    }
}
