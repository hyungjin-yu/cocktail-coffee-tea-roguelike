using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>32 영구 강화24종/5탭 + 38 가독성 개선(2026-10-09): 상단은 보유 스페셜티·해금 수를 크게, 카드는 이름·효과·가격·상태만,
    /// 선행·클리어·적용 무기·네 무기 몹 성장 예측은 선택한 카드의 상세 영역에 둔다. 구매·저장·집계 규칙은 바꾸지 않는다.</summary>
    public class PermanentSkillView : MonoBehaviour
    {
        class NodeSlot
        {
            public PermanentSkillData data;
            public Image background, chip, lockIcon;
            public Text name, effect, cost, status;
            public Button button;
        }
        readonly List<NodeSlot> slots = new List<NodeSlot>();
        readonly Dictionary<PermanentSkillGroup, Button> tabs = new Dictionary<PermanentSkillGroup, Button>();
        GameObject root;
        Text balanceLabel, unlockedLabel, secondaryLabel, noticeLabel;
        Text detailTitle, detailEffect, detailState, detailBody, detailForecast;
        PermanentSkillTree tree;
        List<PermanentSkillData> nodes;
        Action onClose;
        Button backButton;
        PermanentSkillGroup group;
        PermanentSkillData previewNode;
        public bool IsOpen => root != null && root.activeSelf;
        public IReadOnlyList<PermanentSkillData> Nodes => nodes;
        public PermanentPurchaseResult? LastPurchaseResult { get; private set; }
        public string NoticeText => noticeLabel != null ? noticeLabel.text : "";
        public PermanentSkillGroup SelectedGroup => group;
        public PermanentSkillData DetailNode => previewNode;
        public string DetailText => detailTitle == null ? "" :
            string.Join("\n", detailTitle.text, detailEffect.text, detailState.text, detailBody.text, detailForecast.text);
        public string HeaderText => balanceLabel == null ? "" : balanceLabel.text + "\n" + unlockedLabel.text + "\n" + secondaryLabel.text;

        static readonly Color Cream = new Color(248f / 255f, 241f / 255f, 221f / 255f);
        static readonly Color Gold = new Color(1f, .84f, .45f);
        static readonly Color Muted = new Color(.80f, .74f, .68f);
        static readonly Color Owned = new Color(.62f, 1f, .70f);
        static readonly Color Locked = new Color(1f, .70f, .55f);
        static readonly Color DarkInk = new Color(.13f, .09f, .08f);

        const float Top = -262f, CardLeft = 56f, CardRegion = 1200f, CardHeight = 300f, RowGap = 24f;
        const float DetailWidth = 580f, DetailHeight = 690f;

        public static string GroupLabel(PermanentSkillGroup value) => value switch
        { PermanentSkillGroup.Attack => "공격", PermanentSkillGroup.Survival => "생존", PermanentSkillGroup.Ingredient => "재료", PermanentSkillGroup.Weapon => "무기 특화", _ => "마스터" };
        public static string Conditions(PermanentSkillData data) =>
            "선행: " + (data.Prerequisites.Any() ? string.Join(" · ", data.Prerequisites.Select(n => n.displayName)) : "없음") +
            $"\n클리어 {data.minimumClears}회 필요";
        public static string Target(PermanentSkillData data) => data.weaponSpecific ? $"대상: {WeaponLabel(data.targetWeapon)} 시작" : "대상: 모든 시작 무기";
        static string WeaponLabel(WeaponKind kind) => kind switch { WeaponKind.Rapier=>"레이피어", WeaponKind.Sword=>"검", WeaponKind.Gun=>"총", _=>"활" };
        public static Sprite Icon(PermanentSkillData data) =>
            !string.IsNullOrEmpty(data.iconResourcePath) ? Resources.Load<Sprite>(data.iconResourcePath) : ArtLibrary.Sprite(data.iconArtId);

        public static PermanentSkillView Create(Transform canvas, Action onClose)
        {
            var rt = UiFactory.Stretch("PermanentSkillShop", canvas);
            UiFactory.AddImage(rt, new Color(.05f, .04f, .07f, 1f));
            UiFactory.AddCafeBackdrop(rt);
            var v = rt.gameObject.AddComponent<PermanentSkillView>();
            v.root = rt.gameObject; v.onClose = onClose;
            UiFactory.AddText(UiFactory.Place("Title", rt, new Vector2(.5f,1f), new Vector2(0,-40), new Vector2(600,56)), "영구 강화", 40, TextAnchor.MiddleCenter, Color.white);
            v.balanceLabel = UiFactory.AddText(UiFactory.Place("Balance", rt, new Vector2(.5f,1f), new Vector2(-250,-98), new Vector2(480,54)), "", 36, TextAnchor.MiddleCenter, Gold);
            v.unlockedLabel = UiFactory.AddText(UiFactory.Place("Unlocked", rt, new Vector2(.5f,1f), new Vector2(250,-98), new Vector2(480,54)), "", 36, TextAnchor.MiddleCenter, Color.white);
            v.secondaryLabel = UiFactory.AddText(UiFactory.Place("Secondary", rt, new Vector2(.5f,1f), new Vector2(0,-150), new Vector2(1500,34)), "", 22, TextAnchor.MiddleCenter, Muted);
            foreach (PermanentSkillGroup g in Enum.GetValues(typeof(PermanentSkillGroup)))
            {
                var tab = UiFactory.Place("Tab_"+g,rt,new Vector2(.5f,1f),new Vector2(((int)g-2)*290,-200),new Vector2(270,50));
                var bg = UiFactory.AddImage(tab,Color.white); UiFactory.SkinTab(bg);
                var b = tab.gameObject.AddComponent<Button>(); b.targetGraphic=bg; UiFactory.ApplyHoverColors(b); UiFactory.AddFocusSfx(b);
                var captured=g; b.onClick.AddListener(()=>v.SelectGroup(captured));
                UiFactory.AddText(UiFactory.Stretch("Label",tab),GroupLabel(g),28,TextAnchor.MiddleCenter,Color.white);
                v.tabs.Add(g,b);
            }
            v.BuildDetailPanel(rt);
            var back=UiFactory.Place("BackButton",rt,new Vector2(.5f,0),new Vector2(0,48),new Vector2(320,52));
            var backBg=UiFactory.AddImage(back,Color.white);
            v.backButton=back.gameObject.AddComponent<Button>(); v.backButton.targetGraphic=backBg;
            UiFactory.ApplyHoverColors(v.backButton);UiFactory.AddFocusSfx(v.backButton);v.backButton.onClick.AddListener(v.Close);
            UiFactory.AddText(UiFactory.Stretch("Label",back),"뒤로 · Esc",28,TextAnchor.MiddleCenter,Color.white);
            v.noticeLabel=UiFactory.AddText(UiFactory.Place("Notice",rt,new Vector2(.5f,0),new Vector2(-520,48),new Vector2(680,72)),"",24,TextAnchor.MiddleCenter,new Color(1,.65f,.4f));
            v.root.SetActive(false); return v;
        }

        void BuildDetailPanel(RectTransform parent)
        {
            var rt = UiFactory.Place("Detail", parent, new Vector2(1f,1f), new Vector2(-56f, Top), new Vector2(DetailWidth, DetailHeight));
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(.1f,.08f,.12f,1f)));
            float w = DetailWidth - 48f;
            detailTitle = UiFactory.AddText(UiFactory.Place("DetailTitle", rt, new Vector2(.5f,1f), new Vector2(0,-22), new Vector2(w,48)), "", 32, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SkinHeader(detailTitle);
            detailTitle.color = DarkInk;
            detailEffect = UiFactory.AddText(UiFactory.Place("DetailEffect", rt, new Vector2(.5f,1f), new Vector2(0,-84), new Vector2(w,40)), "", 26, TextAnchor.MiddleCenter, Gold);
            detailState = UiFactory.AddText(UiFactory.Place("DetailState", rt, new Vector2(.5f,1f), new Vector2(0,-128), new Vector2(w,40)), "", 26, TextAnchor.MiddleCenter, Color.white);
            detailBody = UiFactory.AddText(UiFactory.Place("DetailBody", rt, new Vector2(.5f,1f), new Vector2(0,-182), new Vector2(w,200)), "", 24, TextAnchor.UpperLeft, Color.white);
            detailBody.lineSpacing = 1.15f;
            detailForecast = UiFactory.AddText(UiFactory.Place("DetailForecast", rt, new Vector2(.5f,1f), new Vector2(0,-398), new Vector2(w,270)), "", 22, TextAnchor.UpperLeft, Color.white);
            detailForecast.lineSpacing = 1.15f;
        }

        void BuildSlots()
        {
            nodes=PermanentSkillDatabase.LoadAll();
            foreach (var data in nodes.OrderBy(n=>n.group).ThenBy(n=>n.weaponSpecific?(int)n.targetWeapon:0).ThenBy(n=>n.tier))
            {
                float width=CardWidth(data.group);
                var rt=UiFactory.Place("Node_"+data.id,root.transform,new Vector2(0f,1f),new Vector2(CardLeft,Top),new Vector2(width,CardHeight));
                var s=new NodeSlot {data=data,background=UiFactory.AddImage(rt,Color.white)};
                UiFactory.SkinCard(s.background);s.button=rt.gameObject.AddComponent<Button>();s.button.targetGraphic=s.background;
                UiFactory.ApplyHoverColors(s.button);UiFactory.AddFocusSfx(s.button);
                // 클릭/Enter: 구매 가능하면 구매, 잠긴 카드는 상세(이유)만 보여 주고 거절음(TryBuy의 기존 판정 그대로).
                var captured=data;s.button.onClick.AddListener(()=>{previewNode=captured;TryBuy(captured);});
                var trigger=rt.gameObject.AddComponent<EventTrigger>();
                foreach(var kind in new[]{EventTriggerType.PointerEnter,EventTriggerType.Select})
                {
                    var entry=new EventTrigger.Entry { eventID=kind };
                    entry.callback.AddListener(_=>{previewNode=captured;RefreshDetail();});trigger.triggers.Add(entry);
                }
                var icon=Icon(data);
                if(icon!=null)
                {
                    var ir=UiFactory.Place("Icon",rt,new Vector2(.5f,1),new Vector2(0,-18),new Vector2(48,48));
                    var img=ir.gameObject.AddComponent<Image>();img.sprite=icon;img.preserveAspect=true;img.raycastTarget=false;
                }
                s.name=UiFactory.AddText(UiFactory.Place("Name",rt,new Vector2(.5f,1),new Vector2(0,-72),new Vector2(width-24,40)),data.displayName,28,TextAnchor.MiddleCenter,Color.white);
                s.effect=UiFactory.AddText(UiFactory.Place("Desc",rt,new Vector2(.5f,1),new Vector2(0,-116),new Vector2(width-28,64)),data.description,24,TextAnchor.UpperCenter,Gold);
                s.cost=UiFactory.AddText(UiFactory.Place("Cost",rt,new Vector2(.5f,0),new Vector2(0,78),new Vector2(width-28,32)),$"비용 {data.cost} 스페셜티",24,TextAnchor.MiddleCenter,Cream);
                var chip=UiFactory.Place("Chip",rt,new Vector2(.5f,0),new Vector2(0,18),new Vector2(width-36,52));
                s.chip=UiFactory.AddImage(chip,Color.white);s.chip.raycastTarget=false;
                var lockRt=UiFactory.Place("Lock",chip,new Vector2(0f,.5f),new Vector2(14,0),new Vector2(24,28));
                s.lockIcon=lockRt.gameObject.AddComponent<Image>();s.lockIcon.sprite=UiFactory.LockSprite;s.lockIcon.raycastTarget=false;
                var statusRt=UiFactory.Stretch("Status",chip);statusRt.offsetMin=new Vector2(40,0);statusRt.offsetMax=new Vector2(-12,0);
                s.status=UiFactory.AddText(statusRt,"",24,TextAnchor.MiddleCenter,Color.white);
                slots.Add(s);
            }
        }

        static float CardWidth(PermanentSkillGroup g) => g==PermanentSkillGroup.Weapon?(CardRegion-3*16f)/4f:g==PermanentSkillGroup.Master?620f:(CardRegion-2*24f)/3f;

        public void SelectGroup(PermanentSkillGroup value)
        {
            group=value;
            var visible=slots.Where(s=>s.data.group==value).ToList();
            int columns=value==PermanentSkillGroup.Weapon?4:3;
            float width=CardWidth(value), gap=value==PermanentSkillGroup.Weapon?16f:24f;
            for(int i=0;i<visible.Count;i++)
            {
                var rt=(RectTransform)visible[i].background.transform;
                rt.anchoredPosition=new Vector2(CardLeft+(i%columns)*(width+gap),Top-(i/columns)*(CardHeight+RowGap));
            }
            foreach(var s in slots)s.background.gameObject.SetActive(s.data.group==value);
            foreach(var pair in tabs)UiFactory.SetChoice((Image)pair.Value.targetGraphic,pair.Key==value,Color.white);
            previewNode=visible.FirstOrDefault()?.data;
            RefreshDetail();
            if(EventSystem.current!=null && tabs.TryGetValue(value,out var tab))EventSystem.current.SetSelectedGameObject(tab.gameObject);
        }
        void Update()
        {
            if(!IsOpen||Keyboard.current==null)return;
            if(Keyboard.current.escapeKey.wasPressedThisFrame){Close();return;}
            if(Keyboard.current.tabKey.wasPressedThisFrame)
            {
                int direction=Keyboard.current.shiftKey.isPressed?-1:1;
                SelectGroup((PermanentSkillGroup)(((int)group+direction+5)%5));
            }
        }
        public void Show()
        {
            if(slots.Count==0)BuildSlots();Refresh();root.SetActive(true);SelectGroup(PermanentSkillGroup.Attack);
        }

        int Available => PermanentProgress.Current.specialtyPoints - tree.SpentPoints;

        /// <summary>구매 판정(TryUnlock)과 같은 순서의 이유. 음수 잔액 대신 부족한 스페셜티 수를 보인다(38).</summary>
        string LockReason(PermanentSkillData n, int available)
        {
            if(tree.IsInactiveLegacy(n))return "비활성 · 선행 확인";
            if(tree.HasDataErrors)return "데이터 오류 · 구매 잠금";
            if(!tree.PrerequisitesMet(n))return "선행 필요";
            if(!tree.ClearsMet(n))return $"클리어 {n.minimumClears}회 필요";
            if(available<0)return "구매 잠금";
            if(available<n.cost)return $"스페셜티 {n.cost-available} 부족";
            return null;
        }

        void Refresh()
        {
            var save=PermanentProgress.Current;
            tree=new PermanentSkillTree(nodes,save.unlockedPermanentSkillIds,save.clearedRuns);
            int available=Available;
            var lockSprite=UiFactory.LockSprite;
            foreach(var s in slots)
            {
                bool owned=tree.IsUnlocked(s.data);
                string reason=owned?null:LockReason(s.data,available);
                bool buyable=!owned&&reason==null;
                UiFactory.SkinChip(s.chip,owned?"owned":buyable?"buyable":"locked");
                s.lockIcon.gameObject.SetActive(!owned&&!buyable&&lockSprite!=null);
                s.status.text=owned?"✓ 구매 완료":buyable?"구매 가능":reason;
                s.status.color=owned?Owned:buyable?DarkInk:Locked;
                s.status.rectTransform.offsetMin=new Vector2(s.lockIcon.gameObject.activeSelf?40:12,0);
                s.cost.text=owned?$"비용 {s.data.cost} · 적용 중":$"비용 {s.data.cost} 스페셜티";
                UiFactory.SetChoice(s.background,false,Color.white);
            }
            var notices=new List<string>();
            if(available<0)notices.Add("데이터 오류: 음수 잔액으로 구매가 잠겼습니다");
            if(tree.HasInactiveLegacyNodes)notices.Add("이전 데이터의 선행 강화 확인 필요");
            if(tree.HasDataErrors)notices.Add("강화 데이터 선행 누락/순환: 구매 잠금");
            if(PermanentProgress.IsFutureSaveProtected)notices.Add("새 임시 플레이를 시작해야 저장할 수 있습니다.");
            else if(LastPurchaseResult==PermanentPurchaseResult.SaveFailed||PermanentProgress.LastSaveFailed)notices.Add(PermanentProgress.NoticeSaveFailed+" · 구매 미반영");
            noticeLabel.text=string.Join(" · ",notices);
            RefreshDetail();
        }

        void RefreshDetail()
        {
            if(tree==null||balanceLabel==null)return;
            var save=PermanentProgress.Current;
            int available=Available;
            balanceLabel.text=$"보유 스페셜티 {available}";
            unlockedLabel.text=$"해금 {tree.UnlockedCount} / 24";
            secondaryLabel.text=$"누적 획득 {save.specialtyPoints} · 사용 {tree.SpentPoints} · 클리어 {save.clearedRuns}회 · 해금한 영구 강화는 다음 런의 몹도 강하게 합니다";
            var n=previewNode;
            if(n==null){detailTitle.text=detailEffect.text=detailState.text=detailBody.text=detailForecast.text="";return;}
            bool owned=tree.IsUnlocked(n);
            string reason=owned?null:LockReason(n,available);
            detailTitle.text=n.displayName;
            detailEffect.text=n.description;
            detailState.text=owned?"✓ 구매 완료 · 다음 런부터 적용 중":reason==null?$"구매 가능 · 구매 후 잔액 {tree.BalanceAfter(n,save.specialtyPoints)}":"잠김 · "+reason;
            detailState.color=owned?Owned:reason==null?Gold:Locked;
            string prereq=n.Prerequisites.Any()?string.Join(" · ",n.Prerequisites.Select(p=>p.displayName+(tree.IsUnlocked(p)?" ✓":" (미구매)"))):"없음";
            var lines=new List<string>
            {
                $"비용   {n.cost} 스페셜티 (보유 {available})",
                $"선행   {prereq}",
                $"클리어   {n.minimumClears}회 필요 (현재 {save.clearedRuns}회{(tree.ClearsMet(n)?" ✓":"")})",
                n.weaponSpecific?$"적용 무기   {WeaponLabel(n.targetWeapon)}로 시작한 런만":"적용 무기   모든 시작 무기",
            };
            if(!owned&&reason==null)lines.Add("구매   Enter 또는 클릭");
            detailBody.text=string.Join("\n",lines);
            string Pct(float v)=>$"{v*100f:0.#}%";
            var forecast=new List<string>{owned?"다음 런 몹 성장 (현재 적용 중)":"다음 런 몹 성장 (구매 전 → 구매 시)"};
            foreach(var w in new[]{WeaponKind.Rapier,WeaponKind.Sword,WeaponKind.Gun,WeaponKind.Bow})
            {
                int p=tree.AppliedCount(w);
                int after=p;
                if(!owned)
                {
                    if(tree.IsAvailable(n))
                        after=new PermanentSkillTree(nodes,save.unlockedPermanentSkillIds.Concat(new[]{n.id}),save.clearedRuns).AppliedCount(w);
                    else if(n.AppliesTo(w)) after=p+1;
                }
                forecast.Add(owned||after==p
                    ? $"{WeaponLabel(w)}   체력 {Pct(RunChallenge.HpFor(p,0))} · 피해 {Pct(RunChallenge.DamageFor(p,0))}{(owned?"":" (변화 없음)")}"
                    : $"{WeaponLabel(w)}   체력 {Pct(RunChallenge.HpFor(p,0))} → {Pct(RunChallenge.HpFor(after,0))} · 피해 {Pct(RunChallenge.DamageFor(p,0))} → {Pct(RunChallenge.DamageFor(after,0))}");
            }
            int total=tree.UnlockedCount, totalAfter=total;
            if(!owned&&tree.IsAvailable(n))totalAfter=new PermanentSkillTree(nodes,save.unlockedPermanentSkillIds.Concat(new[]{n.id}),save.clearedRuns).UnlockedCount;
            string Spawn(int t)=>$"+{RunChallenge.SpawnBonusPercentFor(t)}% · 동시 상한 {RunChallenge.RegularAliveCapFor(t)}";
            forecast.Add(totalAfter==total
                ? $"전 무기   해금 {total} · 몹 등장 {Spawn(total)}{(owned?"":" (변화 없음)")}"
                : $"전 무기   해금 {total}→{totalAfter} · 몹 등장 {Spawn(total)} → {Spawn(totalAfter)}");
            forecast.Add("적용 노드 1개당 몹 체력 +1%p · 피해 +0.4%p (최대 +40% · +18%)\n영구 해금 1개당 일반 몹 등장 +3% (무기 무관, 최대 +60% · 상한 160)");
            detailForecast.text=string.Join("\n",forecast);
        }

        public void TryBuy(PermanentSkillData node)
        {
            var result=PermanentProgress.TryPurchasePermanentSkill(node,nodes);LastPurchaseResult=result;
            AudioService.PlaySfx(result==PermanentPurchaseResult.Purchased?"skill_unlock":"ui_denied");Refresh();
        }
        void Close(){root.SetActive(false);onClose?.Invoke();}
    }
}
