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
            UiFactory.AddText(UiFactory.Place("ScrollHint",panel,new Vector2(.5f,1),new Vector2(0,-112),new Vector2(1300,32)),
                MobilePlatform.IsTouchUI ? "본문을 위아래로 밀어 지식과 출처 보기" : "휠 · 위아래 키 · 패드 오른쪽 스틱으로 지식과 출처 보기",22,TextAnchor.MiddleCenter,new Color(.7f,.8f,.85f));
            var area=UiFactory.Place("Scroll",panel,new Vector2(.5f,1),new Vector2(0,-135),new Vector2(1340,600));
            UiFactory.AddImage(area,new Color(0,0,0,.15f));
            var viewport=UiFactory.Stretch("Viewport",area);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=UiFactory.Place("Body",viewport,new Vector2(.5f,1),Vector2.zero,new Vector2(1290,600));
            v.body=UiFactory.AddText(content,"",30,TextAnchor.UpperLeft,new Color(.94f,.93f,.9f));
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
            title.text=card.displayName;body.text=CardEducationData.Detail(card);
            gameObject.SetActive(true);transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            body.rectTransform.sizeDelta=new Vector2(1290,Mathf.Max(600,body.preferredHeight+32));
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
