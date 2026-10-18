using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CCT
{
    public class CardEducationGuide : MonoBehaviour
    {
        public const int Version=1;
        public static bool SuppressAutomaticForTests;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => SuppressAutomaticForTests=false;
        public static bool NeedsIntroduction => SettingsService.Current.cardEducationGuideVersion < Version;
        public bool IsOpen => gameObject.activeSelf;
        public int Step { get; private set; }
        public string CardId => card?.id;
        public string BodyText => body.text;
        public Sprite ShownSprite => icon.sprite;
        CardData card;
        Text heading,body,artLabel;
        Image icon;
        Button previous,next,detail,skip;
        Action onClose,onDetail;
        GameObject returnFocus;
        readonly InputReleaseGate gate=new InputReleaseGate();
        public static CardEducationGuide Create(Transform parent,Action closed,Action openDetail)
        {
            var root=UiFactory.Stretch("CardEducationGuide",parent);
            UiFactory.AddImage(root,new Color(.015f,.012f,.02f,1));
            var v=root.gameObject.AddComponent<CardEducationGuide>();v.onClose=closed;v.onDetail=openDetail;
            var panel=UiFactory.Place("Panel",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1580,900));
            UiFactory.SkinPanel(UiFactory.AddImage(panel,new Color(.1f,.08f,.12f)));
            v.heading=UiFactory.AddText(UiFactory.Place("Heading",panel,new Vector2(.5f,1),new Vector2(-70,-35),new Vector2(1300,90)),"",38,TextAnchor.MiddleCenter,new Color(1,.9f,.7f));
            var i=UiFactory.Place("ActualCardArt",panel,new Vector2(0,1),new Vector2(65,-200),new Vector2(256,256));
            v.icon=i.gameObject.AddComponent<Image>();v.icon.preserveAspect=true;v.icon.raycastTarget=false;
            v.artLabel=UiFactory.AddText(UiFactory.Place("ArtLabel",panel,new Vector2(0,1),new Vector2(45,-490),new Vector2(300,140)),"",28,TextAnchor.UpperCenter,Color.white);
            v.body=UiFactory.AddText(UiFactory.Place("Body",panel,new Vector2(0,1),new Vector2(390,-175),new Vector2(1110,480)),"",30,TextAnchor.UpperLeft,new Color(.94f,.94f,.91f));
            v.body.supportRichText=false;
            v.previous=v.Button(panel,"Previous",new Vector2(-490,55),new Vector2(260,80),"이전",()=>v.PreviousFromInput());
            v.detail=v.Button(panel,"ReadDetail",new Vector2(0,55),new Vector2(440,80),"이 카드 상세 열기",()=>v.DetailFromInput());
            v.next=v.Button(panel,"Next",new Vector2(490,55),new Vector2(300,80),"다음",()=>v.NextFromInput());
            var skipRt=UiFactory.Place("Skip",panel,new Vector2(1,1),new Vector2(-30,-35),new Vector2(210,65));
            var bg=UiFactory.AddImage(skipRt,new Color(.25f,.23f,.3f));v.skip=skipRt.gameObject.AddComponent<Button>();v.skip.targetGraphic=bg;
            UiFactory.AddText(UiFactory.Stretch("Label",skipRt),"건너뛰기",26,TextAnchor.MiddleCenter,Color.white);
            v.skip.onClick.AddListener(()=>{if(v.AllowsInput())v.Close();});
            root.gameObject.SetActive(false);return v;
        }
        Button Button(Transform parent,string name,Vector2 position,Vector2 size,string label,Action action)
        {
            var rt=UiFactory.Place(name,parent,new Vector2(.5f,0),position,size);
            var bg=UiFactory.AddImage(rt,new Color(.2f,.32f,.34f));
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=bg;
            UiFactory.AddText(UiFactory.Stretch("Label",rt),label,28,TextAnchor.MiddleCenter,Color.white);
            b.onClick.AddListener(()=>action());return b;
        }
        bool AllowsInput() => !(GameManager.Instance!=null && GameManager.Instance.IsPaused) && gate.Allows(Time.frameCount,InputReleaseGate.AnyConfirmHeld());
        void Update() { if(IsOpen)gate.Poll(Time.frameCount,InputReleaseGate.AnyConfirmHeld()); }
        public void Show(CardData actualCard)
        {
            card=actualCard;Step=0;returnFocus=EventSystem.current?.currentSelectedGameObject;
            icon.sprite=CardEducationIcons.For(card);
            artLabel.text=card.displayName+(card.category==CardCategory.Hybrid?"\n창작 보조 활 도안":string.IsNullOrEmpty(card.educationArtId)?"\n공용 카테고리 도안":"\n카드 그림");
            gameObject.SetActive(true);transform.SetAsLastSibling();gate.Arm(Time.frameCount);Refresh();
        }
        void Refresh()
        {
            string[] titles={"이름의 뜻부터 알아보기","그림에서 특징 찾기","음료 지식과 게임 효과 연결하기","상세를 읽고 직접 선택하기"};
            heading.text=$"음료 지식 안내 {Step+1}/4 · {titles[Step]}";
            body.text=Step switch
            {
                0=>card.displayName+"\n\n"+CardEducationSummary.Fact(card)+Glossary(card),
                1=>Visual(card)+"\n\n그림의 재료와 도구를 이름·설명과 연결해 보세요.",
                2=>CardEducationSummary.Fact(card)+"\n\n게임 · "+card.gameplayConnection+"\n"+card.description+"\n\n"+CardEducationData.SymbolNotice,
                _=>"이 카드 상세 열기에서 전체 지식·최대 중첩 효과·출처를 읽을 수 있어요.\n\n안내나 상세를 닫아도 카드가 골라지지 않고 선택 화면으로 돌아갑니다.\n\n카드 본문을 직접 누르거나, 선택할 카드의 숫자 키를 눌러 선택하세요. 패드는 카드 본문에 초점을 옮겨 결정합니다.\n\n카드 선택 화면 오른쪽 위 ‘학습 안내’로 언제든 다시 볼 수 있어요."
            };
            previous.interactable=Step>0;detail.gameObject.SetActive(Step==3);
            next.GetComponentInChildren<Text>().text=Step==3?"안내 마치기":"다음";
            Link(previous,next,skip);Link(next,Step==3?detail:skip,previous);Link(skip,previous,next);Link(detail,next,previous);
            EventSystem.current?.SetSelectedGameObject(next.gameObject);
        }
        static void Link(Button b,Button forward,Button back) => b.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=forward,selectOnDown=forward,selectOnLeft=back,selectOnUp=back};
        void NextFromInput(){if(AllowsInput())Next();}
        void PreviousFromInput(){if(AllowsInput())Previous();}
        void DetailFromInput(){if(AllowsInput())OpenDetail();}
        bool Paused => GameManager.Instance!=null && GameManager.Instance.IsPaused;
        public void Next(){if(!IsOpen || Paused)return;if(Step==3)Close();else{Step++;gate.Arm(Time.frameCount);Refresh();}}
        public void Previous(){if(!IsOpen || Step==0 || Paused)return;Step--;gate.Arm(Time.frameCount);Refresh();}
        public void OpenDetail(){if(!IsOpen || Step!=3 || Paused)return;Close();onDetail?.Invoke();}
        public void Close()
        {
            if(!IsOpen || (GameManager.Instance!=null && GameManager.Instance.IsPaused))return;
            SettingsService.CompleteCardEducationGuide(Version);gameObject.SetActive(false);onClose?.Invoke();
            if(returnFocus!=null)EventSystem.current?.SetSelectedGameObject(returnFocus);
        }
        public static string Glossary(CardData c)
        {
            string text=c.realWorldFact+" "+c.gameplayConnection;string result="";
            if(text.Contains("베이스"))result+="\n베이스: 음료 맛의 바탕이 되는 술.";
            if(text.Contains("베르무트"))result+="\n베르무트: 와인에 술과 식물 향을 더한 재료.";
            if(text.Contains("스티어"))result+="\n스티어: 얼음과 재료를 저어 섞는 동작.";
            if(text.Contains("인퓨전"))result+="\n인퓨전: 식물 재료를 물에 우린 음료.";
            if(text.Contains("산화"))result+="\n산화: 잎 성분이 산소와 반응해 달라지는 과정.";
            return result;
        }
        public static string Visual(CardData c) => c.educationArtId switch
        {
            "card_gin"=>"무라벨 병과 주니퍼 열매 가지를 찾아보세요. 주니퍼 향은 진을 알아보는 특징입니다.",
            "card_jigger"=>"크기가 다른 두 금속 컵이 이어진 모양을 보세요. 재료의 양을 재는 지거입니다.",
            "card_bar_spoon"=>"길고 꼬인 손잡이 끝의 작은 스푼 볼을 보세요. 잔 안 재료를 저을 때 씁니다.",
            "card_shaker"=>"금속 몸통과 뚜껑을 보세요. 얼음과 재료를 넣어 흔들어 섞고 차갑게 만드는 셰이커입니다.",
            "card_double_strain"=>"스프링이 달린 바 스트레이너와 고운 망 체, 서로 다른 두 거름 도구를 찾아보세요.",
            "card_negroni"=>"낮은 잔의 붉은 액체, 큰 얼음과 오렌지 껍질을 찾아보세요.",
            "card_old_fashioned"=>"낮은 잔의 앰버빛 액체와 큰 얼음, 오렌지 껍질을 보세요.",
            "card_martini"=>"V자 잔, 가느다란 잔 다리, 맑은 액체와 올리브를 보세요.",
            "card_blue_blazer"=>"손잡이 달린 두 금속 머그 사이의 짧은 파란 불꽃을 보세요. 전문 바 연출의 그림입니다.",
            "card_green_tea"=>"녹색 잎과 연한 연두빛 찻잔을 보세요. 이 그림은 녹차와 녹차의 떫은 여운 카드가 함께 씁니다.",
            "card_tea_whisk"=>"여러 가닥으로 갈라진 대나무 차선과 말차 그릇을 찾아보세요.",
            "card_gaiwan"=>"뚜껑을 기울인 개완과 작은 잔, 그 사이로 흐르는 차탕을 보세요. 차탕은 우린 차의 액체입니다.",
            "card_oolong"=>"둥글게 말린 우롱 찻잎과 금빛 작은 찻잔을 보세요.",
            "card_puerh"=>"눌러 만든 둥근 차 덩어리의 조각과 갈색 차탕을 보세요. 모든 보이차가 이 모양인 것은 아닙니다.",
            "card_chamomile"=>"흰 꽃잎과 노란 꽃심, 연한 금빛 음료를 보세요. 원료가 잎이 아니라 꽃입니다.",
            "card_rooibos"=>"가늘고 붉은 갈색의 잎·줄기와 적갈색 음료를 보세요. 넓은 녹차 잎과 모양이 다릅니다.",
            _=>c.category==CardCategory.Hybrid?"이 그림은 게임 속 창작 보조 활 티니 보우입니다. 티 마티니 음료를 그린 그림이 아닙니다.":"지금 보이는 그림은 이 카드만의 그림이 아니라 "+(c.category==CardCategory.Tea?"찻잎":"칵테일 재료")+" 공용 도안입니다. 구체적인 도구나 재료는 이름과 음료 지식 설명으로 확인하세요."
        };
    }
}
