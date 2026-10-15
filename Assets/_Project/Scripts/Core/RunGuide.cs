namespace CCT
{
    /// <summary>
    /// 57(2026-10-15) P1 첫 수집 → 게이지 → 카드 창을 실제 행동에 맞춰 짧게 잇는 안내. 처음 몇 판(저장의 totalRuns ≤ 3)만 켠다.
    /// 규칙은 실제 코드 기준: 적 처치 → 재료 드롭(def.dropType) → 가까이 가면 끌려와 주움 → 같은 종류 게이지(IngredientGauge)가 차고
    /// 가득 차면 그 종류 카드 3장 중 1장(RunRewards). 원두는 10개마다 커피 포인트 1(07 §2).
    /// </summary>
    public sealed class RunGuide
    {
        public const int GuidedRuns = 3;
        const float IntroDelay = 1.5f;

        readonly GameManager game;
        float elapsed;
        bool intro, firstCocktail, firstTea, firstBean, firstCard;

        public bool Active { get; }
        /// <summary>테스트/보고용 — 마지막으로 띄운 안내.</summary>
        public string LastText { get; private set; } = "";

        public RunGuide(GameManager game)
        {
            this.game = game;
            var save = PermanentProgress.Current;
            Active = save == null || save.totalRuns <= GuidedRuns;
        }

        public static string IngredientName(IngredientType t, Job job) =>
            t == IngredientType.Tea ? "찻잎" : t == IngredientType.Coffee ? JobStudyTheme.Text("원두", job) : "칵테일 재료";

        public static string CardKind(IngredientType t) => t == IngredientType.Tea ? "차" : "칵테일";

        /// <summary>다음 카드 창 조건 — 실제 게이지 잔량/요구량. 찻집은 칵테일 몹이 없어 찻잎만(47).</summary>
        public static string NextCardStatus(GameManager g)
        {
            var tea = g.TeaGauge;
            if (g.Job == Job.TeaSommelier) return $"찻잎 {tea.Current}/{tea.Threshold}";
            var c = g.CocktailGauge;
            return $"칵테일 재료 {c.Current}/{c.Threshold} · 찻잎 {tea.Current}/{tea.Threshold} (먼저 가득 차는 쪽)";
        }

        void Show(string text, float seconds)
        {
            LastText = text;
            game.Hud?.ShowGuide(text, seconds);
        }

        public void Tick(float dt)
        {
            if (!Active || intro) return;
            elapsed += dt;
            if (elapsed < IntroDelay) return;
            intro = true;
            Show("적을 처치하면 반짝이는 재료가 떨어집니다 · 가까이 가면 끌려와 주워집니다", 7f);
        }

        public void OnPickup(IngredientType type)
        {
            if (!Active) return;
            switch (type)
            {
                case IngredientType.Cocktail:
                case IngredientType.Tea:
                    if (type == IngredientType.Cocktail ? firstCocktail : firstTea) return;
                    if (type == IngredientType.Cocktail) firstCocktail = true; else firstTea = true;
                    var g = type == IngredientType.Cocktail ? game.CocktailGauge : game.TeaGauge;
                    Show($"{IngredientName(type, game.Job)} {g.Current}/{g.Threshold} · 위 게이지가 가득 차면 {CardKind(type)} 카드 3장 중 1장을 고릅니다", 6f);
                    game.Hud?.PulseGauge(type);
                    break;
                case IngredientType.Coffee:
                    if (firstBean) return;
                    firstBean = true;
                    Show(JobStudyTheme.Text($"원두 {Balance.BeanPointThreshold}개마다 커피 포인트 1 · " +
                        (MobilePlatform.IsTouchUI ? "오른쪽 아래 커피 연구 버튼에서 사용" : "[K] 커피 연구에서 사용"), game.Job), 6f);
                    game.Hud?.PulseGauge(type);
                    break;
            }
        }

        public void OnCardChosen()
        {
            if (!Active || firstCard) return;
            firstCard = true;
            Show($"다음 카드 창: {NextCardStatus(game)} · " +
                (MobilePlatform.IsTouchUI ? "왼쪽 목표 조합 버튼으로 원하는 히든 조건 확인" : "[T] 목표 조합으로 원하는 히든 조건 확인"), 7f);
        }
    }
}
