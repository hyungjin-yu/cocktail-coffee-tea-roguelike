using System.Collections.Generic;

namespace CCT
{
    public enum RewardKind { None, Hybrid, Card, CoffeeTree }

    /// <summary>
    /// 07 §2 "보상 처리" 대기열 — 런 상태(게이지·카드 대기열·하이브리드 예약·미사용 커피 포인트)만 다루는
    /// 순수 C# 클래스. GameManager가 화면 전환을 맡고, 다음에 무엇을 열지는 여기서 정한다(EditMode에서
    /// MonoBehaviour 없이 순서를 검증하기 위해 분리).
    /// 순서: 하이브리드 안내 → 카드 대기열(같은 틱이면 칵테일→차, FIFO) → 커피 트리(새 포인트가 생겼을
    /// 때만 1회 자동). 커피 포인트는 캡핑 없이 전부 적립하고, 트리를 "계속하기"로 닫아도 남는다.
    /// </summary>
    public class RunRewards
    {
        readonly IngredientGauge cocktail;
        readonly IngredientGauge tea;
        readonly Queue<CardCategory> pendingCards = new Queue<CardCategory>();
        /// <summary>07:43 "같은 틱의 칵테일 보상→차 보상 순으로 FIFO" — 다음 보상 판정(TakeNext) 전까지
        /// 생긴 레벨업은 여기 모았다가, 판정 시점에 칵테일 먼저·차 나중으로 대기열 끝에 붙인다.
        /// 이미 대기열에 들어간 이전 틱 보상의 순서는 건드리지 않는다.</summary>
        int stagedCocktail;
        int stagedTea;

        /// <summary>미사용 커피 포인트. 런 중 보존, 런 종료(새 GameManager) 시 초기화.</summary>
        public int UnspentPoints { get; private set; }
        /// <summary>07:49 — 새 포인트가 생긴 뒤 트리를 아직 자동으로 열지 않았으면 true. 트리를 한 번 열면
        /// (자동이든 K든) 꺼지고, 다음 새 포인트가 생길 때까지 다시 자동으로 열리지 않는다.</summary>
        public bool TreeAutoOpenArmed { get; private set; }
        public bool HybridPending { get; private set; }
        public int PendingCardCount => pendingCards.Count + stagedCocktail + stagedTea;

        public RunRewards(IngredientGauge cocktail, IngredientGauge tea, CoffeeBeanGauge beans)
        {
            this.cocktail = cocktail;
            this.tea = tea;
            cocktail.LeveledUp += _ => stagedCocktail++;
            tea.LeveledUp += _ => stagedTea++;
            beans.PointsGained += n =>
            {
                UnspentPoints += n;
                TreeAutoOpenArmed = true;
            };
        }

        /// <summary>07:31 — 요구 감소 카드/커피 노드를 적용한 직후 호출. 칵테일→차 순서로 평가해 같은 틱
        /// 보상 순서(07:43)를 지킨다.</summary>
        public void ReevaluateGauges()
        {
            cocktail.Reevaluate();
            tea.Reevaluate();
        }

        public void RequestHybrid() => HybridPending = true;

        /// <summary>다음에 열 보상을 꺼낸다. Card면 category가 채워진다. CoffeeTree는 자동 열림 조건
        /// (새 포인트 발생 이후 아직 안 열었음 + 미사용 포인트 있음 + 찍을 노드 있음)을 만족할 때만 —
        /// 찍을 노드가 없으면(15개 전부 해금) 포인트는 보존한 채 자동 열림만 해제해 무한 팝업을 막는다.</summary>
        public RewardKind TakeNext(bool anyNodeAvailable, out CardCategory category)
        {
            category = default;
            FlushStagedCards();
            if (HybridPending)
            {
                HybridPending = false;
                return RewardKind.Hybrid;
            }
            if (pendingCards.Count > 0)
            {
                category = pendingCards.Dequeue();
                return RewardKind.Card;
            }
            if (TreeAutoOpenArmed)
            {
                TreeAutoOpenArmed = false;
                if (UnspentPoints > 0 && anyNodeAvailable) return RewardKind.CoffeeTree;
            }
            return RewardKind.None;
        }

        void FlushStagedCards()
        {
            for (; stagedCocktail > 0; stagedCocktail--) pendingCards.Enqueue(CardCategory.Cocktail);
            for (; stagedTea > 0; stagedTea--) pendingCards.Enqueue(CardCategory.Tea);
        }

        /// <summary>K로 직접 열 때 — 열었으니 자동 열림 예약은 소모된 것으로 본다.</summary>
        public void DisarmTreeAutoOpen() => TreeAutoOpenArmed = false;

        /// <summary>08:90 "노드 클릭으로1포인트 사용, 입력1회 처리, 효과 즉시 적용". 포인트가 없거나 선행/
        /// 중복으로 해금할 수 없으면 아무것도 바꾸지 않는다. 해금 직후 요구 감소 노드(콜드브루 등)를 반영해
        /// 게이지를 재평가한다(07:31).</summary>
        public bool TryUnlockNode(SkillTree tree, SkillData node, PlayerStats stats)
        {
            if (UnspentPoints <= 0 || node == null || !tree.IsAvailable(node)) return false;
            if (!tree.Unlock(node, stats)) return false;
            UnspentPoints--;
            ReevaluateGauges();
            return true;
        }
    }
}
