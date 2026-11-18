using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using T = CCT.Prototype.Coop2P.CoopTuning;

namespace CCT.Prototype.Coop2P
{
    /// <summary>-cct-coop2p 명시 인자로만 진입. 싱글 부트(GameBootstrap)는 같은 인자에서 아무것도 열지 않는다.</summary>
    public static class CoopBoot
    {
        public const string VerifyArg = "-cct-coop2p-verify";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (!Application.isPlaying || !LaunchOptions.Coop2PRequested) return;
            var go = new GameObject("Coop2P");
            var game = go.AddComponent<CoopGame>();
            var verifyDir = ArgValue(VerifyArg);
            if (!string.IsNullOrEmpty(verifyDir)) go.AddComponent<CoopVerifier>().Init(game, verifyDir);
        }

        public static string ArgValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }

    /// <summary>실험판 화면: 한 화면 바(주방 위 / 홀 아래), 기존 승인 아트 재사용, 무음.</summary>
    public sealed class CoopGame : MonoBehaviour
    {
        public static readonly Color P1Color = new Color(1f, 0.62f, 0.25f);
        public static readonly Color P2Color = new Color(0.35f, 0.88f, 0.85f);
        static readonly Color BrewColor = new Color(0.55f, 0.36f, 0.2f, 0.95f);
        static readonly Color FightColor = new Color(0.72f, 0.2f, 0.2f, 0.95f);
        static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);
        static readonly string[] IngredientNames = { "라임", "원두", "찻잎" };

        public CoopSim Sim { get; private set; }
        public Keyboard KeyboardOverride;
        public bool VerifyMode;
        public string PauseReason { get; private set; }
        public int FramesRendered { get; private set; }

        Camera cam;
        Canvas canvas;
        RectTransform canvasRt;
        Sprite[] ingredientSprites, drinkSprites;
        Sprite[][] stationAnim;
        readonly ActorView[] playerViews = new ActorView[2];
        readonly Dictionary<CoopEnemy, ActorView> enemyViews = new Dictionary<CoopEnemy, ActorView>();
        readonly List<ActorView> enemyPool = new List<ActorView>();
        readonly SpriteRenderer[] stationTool = new SpriteRenderer[3];
        readonly SpriteRenderer[] stationCup = new SpriteRenderer[3];
        readonly SpriteRenderer[] counterCups = new SpriteRenderer[T.PassCounterCapacity];

        // HUD
        readonly PlayerHud[] huds = new PlayerHud[2];
        Text timerText, stageText, counterText, bannerText;
        RectTransform topBar, bottomBar, worldLayer;
        readonly Text[] offChip = new Text[2];
        readonly Text[] stockText = new Text[3];
        readonly RectTransform[] stockIcon = new RectTransform[3];
        readonly StationHud[] stationHuds = new StationHud[3];
        readonly Text[] playerTag = new Text[2], playerPrompt = new Text[2];
        readonly RectTransform[] reviveBar = new RectTransform[2];
        readonly RectTransform[] reviveRoot = new RectTransform[2];
        Text counterWorldText, kitchenLabel, hallLabel;
        RectTransform pausePanel, resultPanel;
        Text pauseText, resultTitle, resultBody;
        float bannerTime;
        readonly List<Floater> floaters = new List<Floater>();
        float animClock;

        sealed class ActorView
        {
            public GameObject Go;
            public SpriteRenderer Body, Cup;
            public ArtAsset Art;
            public float Scale;
        }

        sealed class PlayerHud
        {
            public Text Role, Hp, Effects, HeldLabel;
            public Image RoleBg, HeldIcon;
            public RectTransform HpFill;
        }

        sealed class StationHud
        {
            public RectTransform Root, Fill;
            public CanvasGroup Group;
            public Text Title, Status;
        }

        sealed class Floater
        {
            public RectTransform Rt;
            public Text Text;
            public Image Image;
            public Vector3 World;
            public Vector2 FromScreen, ToScreen;
            public bool Fly;
            public float Age, Life;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            AudioListener.volume = 0f; // 실험판은 무음
            Sim = new CoopSim();
            LoadSprites();
            BuildCamera();
            BuildWorld();
            BuildHud();
            playerViews[0] = MakeActor("P1", "bartender", 1.25f, true);
            playerViews[1] = MakeActor("P2", "tea_sommelier", 1.25f, true);
            Banner("5분 버티기 — 홀(아래)에서 싸워 재료를 모으고, 주방(위)에서 음료를 만들어 건네 마시세요", 7f);
        }

        // ───────── 입력·진행 ─────────

        void Update()
        {
            var kb = KeyboardOverride ?? Keyboard.current;
            if (kb != null)
            {
                if (CoopInput.Pressed(kb, CoopInput.Quit) && (Sim.Paused || Sim.Outcome != CoopOutcome.Running)) { Application.Quit(); return; }
                if (CoopInput.Pressed(kb, CoopInput.Pause) && Sim.Outcome == CoopOutcome.Running) SetPaused(!Sim.Paused, null);
                if (CoopInput.Pressed(kb, CoopInput.Restart) && (Sim.Paused || Sim.Outcome != CoopOutcome.Running)) Restart();
            }

            var p1 = Sim.Paused ? default : CoopInput.Read(kb, CoopInput.P1);
            var p2 = Sim.Paused ? default : CoopInput.Read(kb, CoopInput.P2);
            Sim.Tick(Time.deltaTime, p1, p2);
            if (!Sim.Paused) animClock += Time.deltaTime;

            foreach (var e in Sim.Events) Handle(e);
            Sim.Events.Clear();

            SyncWorld();
            FitCamera();
            SyncHud();
            FramesRendered++;
        }

        public void SetPaused(bool paused, string reason)
        {
            if (Sim.Outcome != CoopOutcome.Running) paused = false;
            Sim.Paused = paused;
            PauseReason = paused ? reason : null;
            Time.timeScale = paused ? 0f : 1f;
            pausePanel.gameObject.SetActive(paused);
            pauseText.text = (reason != null ? reason + "\n\n" : "") + "일시정지 — 두 사람·손님·제조·효과·시간 모두 정지\n\nEsc 계속   ·   R 다시 시작   ·   F10 종료";
        }

        public void Restart()
        {
            Sim.Reset();
            Time.timeScale = 1f;
            foreach (var v in enemyViews.Values) { v.Go.SetActive(false); enemyPool.Add(v); }
            enemyViews.Clear();
            foreach (var f in floaters) Destroy(f.Rt.gameObject);
            floaters.Clear();
            foreach (var vfx in FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)) Destroy(vfx.gameObject);
            pausePanel.gameObject.SetActive(false);
            resultPanel.gameObject.SetActive(false);
            animClock = 0f;
            SyncWorld();
            FitCamera(true);
            Banner("다시 시작 — 5분 버티기", 3f);
        }

        void OnApplicationFocus(bool focus)
        {
            // 창 비활성: 입력이 끊기므로 진행을 멈춘다(돌아와 Esc로 계속). 검증 실행은 가상 키보드라 예외.
            if (!focus && !VerifyMode && Sim != null && Sim.Outcome == CoopOutcome.Running && !Sim.Paused)
                SetPaused(true, "창이 비활성화되어 자동 일시정지");
        }

        // ───────── 사건 연출 ─────────

        void Handle(CoopEvent e)
        {
            switch (e.Kind)
            {
                case CoopEventKind.Hit:
                    Line(e.From + new Vector2(0f, 0.55f), e.Pos + new Vector2(0f, 0.35f), e.Player == 0 ? P1Color : P2Color, e.A == 1 ? 0.07f : 0.045f);
                    ArtVfx.Spawn("hit_spark", e.Pos + new Vector2(0f, 0.35f), Random.Range(0f, 360f), e.A == 1 ? 0.6f : 0.45f, 0.18f, Color.white, 2000);
                    break;
                case CoopEventKind.Kill:
                    ArtVfx.Spawn("hit_spark", e.Pos + new Vector2(0f, 0.35f), 0f, 0.9f, 0.25f, new Color(1f, 0.9f, 0.6f), 2000);
                    break;
                case CoopEventKind.IngredientGained:
                    FlyIcon(ingredientSprites[e.A], e.Pos + new Vector2(0f, 0.4f), stockIcon[e.A]);
                    break;
                case CoopEventKind.IngredientOverflow:
                    break; // 가득 찬 재고는 상단 숫자(9)로 충분 — 처치마다 문구가 쌓이지 않게
                case CoopEventKind.BrewStart:
                {
                    var r = T.Recipe((CoopDrink)e.A);
                    Hint(e.Pos + new Vector2(0f, -0.2f), $"{r.Name} 제조 시작  {CostText(r, "-")}", Cream);
                    break;
                }
                case CoopEventKind.BrewCancel:
                    Hint(e.Pos + new Vector2(0f, -0.2f), "제조 취소 — 재료 전부 환불", Cream);
                    break;
                case CoopEventKind.BrewDone:
                    ArtVfx.Spawn("pickup_glint", e.Pos + new Vector2(0f, 0.3f), 0f, 1.1f, 0.6f, Color.white, 2000);
                    Hint(e.Pos + new Vector2(0f, -0.2f), $"{T.Recipe((CoopDrink)e.A).Name} 완성! 집어서 카운터로", new Color(1f, 0.92f, 0.5f));
                    break;
                case CoopEventKind.CupPlaced:
                    Hint(T.PassCounterPos + new Vector2(0f, -0.9f), $"P{e.Player + 1} → 공용 카운터에 놓음", PlayerColor(e.Player));
                    break;
                case CoopEventKind.CupTaken:
                    Hint(Sim.Players[e.Player].Pos + new Vector2(0f, 1.7f), $"{T.Recipe((CoopDrink)e.A).Name} 집음 (P{e.Player + 1} 소유)", PlayerColor(e.Player));
                    break;
                case CoopEventKind.CupGiven:
                    Hint(e.Pos + new Vector2(0f, 1.7f), $"P{e.Player + 1} → P{2 - e.Player} 건넴", PlayerColor(1 - e.Player));
                    break;
                case CoopEventKind.Drank:
                    OnDrank(e);
                    break;
                case CoopEventKind.Shockwave:
                    FadeVfx.Spawn(SpriteFactory.Circle, e.Pos, 0f, Vector2.one * 0.8f, Vector2.one * (T.BurstRadius * 2f), new Color(1f, 0.92f, 0.5f, 0.4f), 0.45f, 0); // 반경 4 충격파(바닥 원)
                    Hint(e.Pos + new Vector2(0f, 2.0f), $"충격파! {e.A}마리 명중 (30 피해·밀쳐냄·생존 시 기절 2초)", new Color(1f, 0.95f, 0.5f));
                    break;
                case CoopEventKind.Rejected:
                    Hint(Sim.Players[e.Player].Pos + new Vector2(0f, 1.7f), RejectText(e), new Color(1f, 0.55f, 0.5f));
                    break;
                case CoopEventKind.Downed:
                    Hint(e.Pos + new Vector2(0f, 1.2f), $"P{e.Player + 1} 쓰러짐! 동료가 곁에 2초 서면 구조 (12초 뒤 자력 회복)", new Color(1f, 0.5f, 0.45f));
                    break;
                case CoopEventKind.Revived:
                    Hint(e.Pos + new Vector2(0f, 1.6f), e.A >= (int)T.ReviveHp ? $"P{e.Player + 1} 구조됨" : $"P{e.Player + 1} 자력 회복", new Color(0.6f, 1f, 0.6f));
                    break;
                case CoopEventKind.RoleChanged:
                    Hint(e.Pos + new Vector2(0f, 1.7f), e.A == (int)CoopRole.Brew ? "제조 역할" : "전투 역할",
                        e.A == (int)CoopRole.Brew ? new Color(1f, 0.82f, 0.55f) : new Color(1f, 0.55f, 0.5f));
                    break;
                case CoopEventKind.StageUp:
                    Banner($"{e.A + 1}단계 — 손님이 더 빨리, 더 많이 몰려온다", 3f);
                    break;
                case CoopEventKind.EnemySpawn:
                    ArtVfx.GroundRing("summon_ring", e.Pos, 0.45f, 0.5f, new Color(1f, 0.7f, 0.5f, 0.8f), true, 0); // 입구에서 손님 등장 표시
                    break;
                case CoopEventKind.Outcome:
                    ShowResult();
                    break;
            }
        }

        void OnDrank(CoopEvent e)
        {
            var d = (CoopDrink)e.A;
            var c = PlayerColor(e.Player);
            switch (d)
            {
                case CoopDrink.LeafTonic:
                    ArtVfx.Spawn("leaf_mote", e.Pos + new Vector2(0f, 0.7f), 0f, 1.2f, 0.9f, new Color(0.6f, 1f, 0.6f), 2000, 1.6f);
                    Hint(e.Pos + new Vector2(0f, 1.9f), $"P{e.Player + 1} 찻잎 토닉 — 회복 +50", new Color(0.6f, 1f, 0.6f));
                    break;
                case CoopDrink.RushShot:
                    ArtVfx.Spawn("levelup_glint", e.Pos + new Vector2(0f, 0.6f), 0f, 1.4f, 0.6f, new Color(1f, 0.75f, 0.4f), 2000, 1.5f);
                    Hint(e.Pos + new Vector2(0f, 1.9f), $"P{e.Player + 1} 러시 샷 — 10초 연사·사거리·이속", new Color(1f, 0.78f, 0.45f));
                    break;
                default:
                    Hint(e.Pos + new Vector2(0f, 2.3f), $"P{e.Player + 1} 시트러스 버스트", c);
                    break;
            }
        }

        string RejectText(CoopEvent e)
        {
            var p = Sim.Players[e.Player];
            switch (e.Action)
            {
                case CoopAction.RejectedInsufficient:
                {
                    var st = Sim.NearestStation(p.Pos);
                    if (st == null) return "재료 부족";
                    var r = T.Recipe(st.Drink);
                    var parts = new List<string>();
                    for (int i = 0; i < 3; i++)
                    {
                        int need = r.Cost((Ingredient)i) - Sim.Stock[i];
                        if (need > 0) parts.Add($"{IngredientNames[i]} {need}");
                    }
                    return "재료 부족: " + string.Join(", ", parts) + " 더 필요 (차감 없음)";
                }
                case CoopAction.RejectedBusy:
                {
                    var st = Sim.NearestStation(p.Pos);
                    return st != null && st.Owner >= 0 ? $"P{st.Owner + 1}이(가) 제조 중" : "사용 중";
                }
                case CoopAction.RejectedCounterFull: return $"카운터 가득 ({T.PassCounterCapacity}/{T.PassCounterCapacity})";
                case CoopAction.HoldingNoTarget:
                {
                    var map = e.Player == 0 ? CoopInput.P1 : CoopInput.P2;
                    return $"{map.DrinkText}: 마시기 · 카운터/빈손 동료 곁에서 {map.InteractText}: 놓기/건네기";
                }
                default: return "지금은 할 수 없음";
            }
        }

        // ───────── 표시 동기화 ─────────

        void SyncWorld()
        {
            for (int i = 0; i < 2; i++)
            {
                var p = Sim.Players[i];
                var v = playerViews[i];
                v.Go.transform.position = new Vector3(p.Pos.x, p.Pos.y, 0f);
                string clip = p.Downed ? "defeat" : p.HurtFlash > 0f ? "hurt" : p.Moving ? "move" : "idle";
                SetFrame(v, clip, !p.Downed);
                if (Mathf.Abs(p.Facing.x) > 0.05f) v.Body.flipX = p.Facing.x < 0f;
                var tint = Color.white;
                if (p.RushTime > 0f) tint = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.42f), 0.45f + 0.25f * Mathf.Sin(animClock * 14f));
                if (p.HealPool > 0f) tint = Color.Lerp(tint, new Color(0.6f, 1f, 0.6f), 0.5f);
                if (p.InvulnTime > 0f && Mathf.Repeat(animClock * 10f, 1f) < 0.5f) tint.a = 0.55f;
                if (p.Downed) tint = new Color(0.7f, 0.7f, 0.7f, 0.85f);
                v.Body.color = tint;
                int order = Order(p.Pos.y);
                v.Body.sortingOrder = order;
                v.Cup.sortingOrder = order + 1;
                v.Cup.enabled = p.Held != null;
                if (p.Held != null)
                {
                    SetSized(v.Cup, drinkSprites[(int)p.Held.Drink], 0.5f / v.Go.transform.localScale.y);
                    v.Cup.transform.localPosition = new Vector3(v.Body.flipX ? -0.38f : 0.38f, 0.62f, 0f);
                }
            }

            foreach (var e in Sim.Enemies)
            {
                if (!enemyViews.TryGetValue(e, out var v))
                {
                    v = enemyPool.Count > 0 ? enemyPool[enemyPool.Count - 1] : MakeActor("Enemy", "espresso_bug", 0.75f, false);
                    if (enemyPool.Count > 0) enemyPool.RemoveAt(enemyPool.Count - 1);
                    v.Go.SetActive(true);
                    enemyViews[e] = v;
                }
                v.Go.transform.position = new Vector3(e.Pos.x, e.Pos.y, 0f);
                SetFrame(v, e.HitFlash > 0f ? "hurt" : e.Stun > 0f ? "idle" : "move", true);
                if (Mathf.Abs(e.Facing.x) > 0.05f) v.Body.flipX = e.Facing.x < 0f;
                v.Body.color = e.Stun > 0f ? new Color(0.6f, 0.75f, 1f) : e.HitFlash > 0f ? new Color(1f, 0.6f, 0.6f) : Color.white;
                v.Body.sortingOrder = Order(e.Pos.y);
            }
            if (enemyViews.Count > Sim.Enemies.Count)
            {
                var gone = new List<CoopEnemy>();
                foreach (var kv in enemyViews) if (!Sim.Enemies.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var g in gone)
                {
                    enemyViews[g].Go.SetActive(false);
                    enemyPool.Add(enemyViews[g]);
                    enemyViews.Remove(g);
                }
            }

            for (int s = 0; s < 3; s++)
            {
                var st = Sim.Stations[s];
                var frames = stationAnim[s];
                if (frames.Length > 0)
                {
                    int f = st.Brewing && st.Attended ? (int)(animClock * 8f) % frames.Length : 0;
                    stationTool[s].sprite = frames[f];
                }
                stationTool[s].color = st.Brewing && !st.Attended ? new Color(0.75f, 0.75f, 0.75f) : Color.white;
                stationCup[s].enabled = st.Ready != null;
            }
            for (int c = 0; c < counterCups.Length; c++)
            {
                var cup = Sim.Counter[c];
                counterCups[c].enabled = cup != null;
                if (cup != null) SetSized(counterCups[c], drinkSprites[(int)cup.Drink], 0.6f);
            }
        }

        void SetFrame(ActorView v, string clipName, bool loop)
        {
            var clip = v.Art?.Clip(clipName) ?? v.Art?.Clip("idle");
            if (clip == null || clip.frames.Length == 0) return;
            float t = animClock;
            int i = clip.fps > 0f ? (int)(t * clip.fps) : 0;
            i = loop ? i % clip.frames.Length : clip.frames.Length - 1;
            v.Body.sprite = clip.frames[i];
        }

        void SyncHud()
        {
            int secs = Mathf.CeilToInt(Sim.Remaining);
            // 5분 목표 = 전투 진행 시간(홀에 서 있는 사람이 있을 때만 흐름)
            timerText.text = $"전투 남은 시간 {secs / 60}:{secs % 60:00}";
            timerText.color = !Sim.HallActive ? new Color(0.62f, 0.62f, 0.62f) : Sim.Remaining <= 30f ? new Color(1f, 0.6f, 0.45f) : Cream;
            stageText.text = Sim.HallActive || Sim.Outcome != CoopOutcome.Running
                ? $"{Sim.Stage + 1}/5단계 · 손님 {Sim.Enemies.Count} · 처치 {Sim.Kills}"
                : "홀에 서 있는 사람이 없어 전투 시간·단계 멈춤 (제조·회복·손님 이동은 계속)";
            stageText.color = Sim.HallActive ? new Color(0.85f, 0.8f, 0.72f) : new Color(1f, 0.78f, 0.45f);
            for (int i = 0; i < 3; i++) stockText[i].text = $"{Sim.Stock[i]}";
            int onCounter = 0;
            foreach (var c in Sim.Counter) if (c != null) onCounter++;
            counterText.text = $"{onCounter}/{T.PassCounterCapacity}";
            counterWorldText.text = $"공용 카운터 {onCounter}/{T.PassCounterCapacity}";

            for (int i = 0; i < 2; i++)
            {
                var p = Sim.Players[i];
                var h = huds[i];
                bool brew = p.Role == CoopRole.Brew;
                h.Role.text = p.Downed ? "쓰러짐" : brew ? "주방 · 제조" : "홀 · 전투";
                h.RoleBg.color = p.Downed ? new Color(0.3f, 0.3f, 0.3f, 0.95f) : brew ? BrewColor : FightColor;
                h.HpFill.sizeDelta = new Vector2(356f * Mathf.Clamp01(p.Hp / T.PlayerMaxHp), h.HpFill.sizeDelta.y);
                h.HpFill.GetComponent<Image>().color = p.Hp > 35f ? new Color(0.45f, 0.85f, 0.4f) : new Color(0.95f, 0.4f, 0.3f);
                h.Hp.text = $"HP {Mathf.CeilToInt(p.Hp)}/{(int)T.PlayerMaxHp}";
                var fx = new List<string>();
                if (p.RushTime > 0f) fx.Add($"러시 {p.RushTime:0.0}초");
                if (p.HealPool > 0f) fx.Add($"회복 +{Mathf.CeilToInt(p.HealPool)}");
                if (p.InvulnTime > 0f) fx.Add($"보호 {p.InvulnTime:0.0}초");
                if (p.Downed) fx.Add($"구조 {p.ReviveProgress:0.0}/{T.ReviveSeconds:0}초 · 자력 {Mathf.Max(0f, T.AutoReviveSeconds - p.DownedTime):0}초");
                h.Effects.text = fx.Count > 0 ? string.Join(" · ", fx) : "효과 없음";
                h.HeldIcon.enabled = p.Held != null;
                if (p.Held != null) h.HeldIcon.sprite = drinkSprites[(int)p.Held.Drink];
                h.HeldLabel.text = p.Held != null ? T.Recipe(p.Held.Drink).Name : "빈손";

                // 머리 위 태그와 행동 안내
                PlaceAtWorld(playerTag[i].rectTransform, p.Pos + new Vector2(0f, 1.45f));
                playerTag[i].text = p.Held != null ? $"P{i + 1} · {T.Recipe(p.Held.Drink).Name}" : $"P{i + 1}";
                // 발밑 안내가 카운터(잔 자리) 띠에 걸리면 머리 위로 올려 잔을 가리지 않는다(V6).
                // 주방 쪽(카운터 뒤) 사람의 안내는 머리 위, 홀 쪽은 발밑 — 카운터 위 잔·반대편 사람 안내와 겹치지 않게
                var promptAt = p.Pos + new Vector2(0f, -0.38f);
                if (p.Pos.y > T.CounterMinY && promptAt.y < T.CounterMaxY + 0.9f) promptAt = p.Pos + new Vector2(0f, 1.8f);
                PlaceAtWorld(playerPrompt[i].rectTransform, promptAt);
                playerPrompt[i].text = PromptFor(i);
                reviveRoot[i].gameObject.SetActive(p.Downed);
                if (p.Downed)
                {
                    PlaceAtWorld(reviveRoot[i], p.Pos + new Vector2(0f, 1.2f));
                    reviveBar[i].sizeDelta = new Vector2(116f * Mathf.Clamp01(p.ReviveProgress / T.ReviveSeconds), reviveBar[i].sizeDelta.y);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                bool vis = BodyFullyVisible(i, out var r);
                offChip[i].gameObject.SetActive(!vis);
                if (vis) continue;
                var u = UsableRect;
                bool above = r.center.y > u.center.y;
                float d = Vector2.Distance(Sim.Players[i].Pos, Sim.Players[1 - i].Pos);
                offChip[i].text = $"{(above ? "▲" : "▼")} P{i + 1} {(Sim.Players[i].Role == CoopRole.Brew ? "주방" : "홀")} · {d:0.0}m";
                float sfc = canvas.scaleFactor;
                float x = Mathf.Clamp(r.center.x, u.xMin + 160f * sfc, u.xMax - 160f * sfc);
                float y = above ? u.yMax - 18f * sfc : u.yMin + 18f * sfc;
                offChip[i].rectTransform.position = new Vector3(x, y, 0f);
            }

            // 두 사람 안내가 화면에서 겹치면 P2 안내를 한 줄 위로
            {
                var a = playerPrompt[0].rectTransform; var b = playerPrompt[1].rectTransform;
                float sf = canvas.scaleFactor;
                float wa = playerPrompt[0].preferredWidth * sf, wb = playerPrompt[1].preferredWidth * sf;
                if (playerPrompt[0].text.Length > 0 && playerPrompt[1].text.Length > 0 &&
                    Mathf.Abs(a.position.x - b.position.x) < (wa + wb) * 0.5f + 8f && Mathf.Abs(a.position.y - b.position.y) < 30f * sf)
                    b.position = new Vector3(b.position.x, (a.position.y >= b.position.y ? a.position.y : b.position.y) + 32f * sf, 0f);
            }

            for (int s = 0; s < 3; s++)
            {
                var st = Sim.Stations[s];
                var sh = stationHuds[s];
                var r = T.Recipe(st.Drink);
                // 작업대 옆(같은 높이)에 둬서 작업대 앞에 선 사람을 가리지 않는다. 토닉·버스트는 바깥쪽 반대편, 러시는 오른쪽.
                // 작업대 위 벽에 라벨(사람 머리 높이보다 위 — 작업대 앞 사람을 가리지 않음)
                var panelAt = st.Pos + new Vector2(0f, 2.15f);
                PlaceAtWorld(sh.Root, panelAt);
                ClampBelowTop(sh.Root, 40f); // 카메라가 홀 쪽으로 내려가도 작업대 라벨은 사용 영역 위 끝에 남는다(사람 머리 높이보다 위)
                // 사람이 라벨 뒤를 지나가면 라벨을 흐리게(사람이 가려지지 않게, V6)
                bool behind = false;
                foreach (var pl in Sim.Players)
                    if (Mathf.Abs(pl.Pos.x - panelAt.x) < 1.45f && pl.Pos.y + 1.3f > panelAt.y - 0.35f && pl.Pos.y < panelAt.y + 0.35f) behind = true;
                sh.Group.alpha = Mathf.MoveTowards(sh.Group.alpha, behind ? 0.3f : 1f, Time.unscaledDeltaTime * 6f);
                sh.Fill.sizeDelta = new Vector2(224f * st.Progress01, sh.Fill.sizeDelta.y);
                sh.Fill.GetComponent<Image>().color = st.Ready != null ? new Color(1f, 0.85f, 0.35f) : new Color(0.5f, 0.85f, 0.95f);
                if (st.Ready != null) sh.Status.text = "완성! 집기";
                else if (st.Brewing) sh.Status.text = st.Attended ? $"P{st.Owner + 1} 제조 {Mathf.Max(0f, r.BrewSeconds - st.Progress):0.0}초" : "자리 비움 · 멈춤";
                else sh.Status.text = Sim.CanAfford(r) ? $"{CostText(r, "")} · {r.BrewSeconds:0.#}초" : $"{CostText(r, "")} · 재료 부족";
                sh.Status.color = !st.Brewing && st.Ready == null && !Sim.CanAfford(r) ? new Color(1f, 0.6f, 0.55f) : Cream;
            }
            PlaceAtWorld(counterWorldText.rectTransform, new Vector2(T.PassCounterPos.x, 1.05f));
            // 카운터 앞/뒤에 사람이 서면 카운터 라벨을 흐리게(사람·안내 우선)
            bool nearPass = false;
            foreach (var pl in Sim.Players) if (Mathf.Abs(pl.Pos.x - T.PassCounterPos.x) < 1.1f && Mathf.Abs(pl.Pos.y - T.PassCounterPos.y) < 1.2f) nearPass = true;
            var cc = counterWorldText.color; cc.a = nearPass ? 0.25f : 1f; counterWorldText.color = cc;
            PlaceAtWorld(kitchenLabel.rectTransform, new Vector2(2.2f, 5.85f));
            ClampBelowTop(kitchenLabel.rectTransform, 22f);
            PlaceAtWorld(hallLabel.rectTransform, new Vector2(-6.2f, -4.1f));

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.Age += Time.deltaTime;
                float k = Mathf.Clamp01(f.Age / f.Life);
                if (f.Fly)
                {
                    float e = 1f - (1f - k) * (1f - k);
                    f.Rt.position = Vector2.Lerp(f.FromScreen, f.ToScreen, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 60f);
                }
                else
                {
                    PlaceAtWorld(f.Rt, f.World + new Vector3(0f, k * 0.5f, 0f));
                    // 화면 가장자리에서 잘리지 않게 글자 폭만큼 안쪽으로
                    float half = f.Text.preferredWidth * canvas.scaleFactor * 0.5f + 8f;
                    var pos = f.Rt.position;
                    pos.x = Mathf.Clamp(pos.x, half, Mathf.Max(half, Screen.width - half));
                    f.Rt.position = pos;
                    var c = f.Text.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; f.Text.color = c;
                }
                if (f.Age >= f.Life) { Destroy(f.Rt.gameObject); floaters.RemoveAt(i); }
            }

            if (bannerTime > 0f)
            {
                bannerTime -= Time.deltaTime;
                var c = bannerText.color; c.a = Mathf.Clamp01(bannerTime / 0.6f); bannerText.color = c;
                bannerText.transform.parent.gameObject.SetActive(bannerTime > 0f);
            }
        }

        string PromptFor(int i)
        {
            var p = Sim.Players[i];
            if (p.Downed || Sim.Outcome != CoopOutcome.Running) return "";
            var map = i == 0 ? CoopInput.P1 : CoopInput.P2;
            var a = Sim.PredictInteract(i);
            var st = Sim.NearestStation(p.Pos);
            string label = null;
            switch (a)
            {
                case CoopAction.Placed: label = "카운터에 놓기"; break;
                case CoopAction.Given: label = $"P{2 - i}에게 건네기"; break;
                case CoopAction.TakenFromStation: label = "완성 잔 집기"; break;
                case CoopAction.TakenFromCounter: label = "카운터 잔 집기"; break;
                case CoopAction.BrewStarted: label = st != null ? $"{T.Recipe(st.Drink).Name} 제조" : "제조"; break;
                case CoopAction.BrewCancelled: label = "제조 취소(환불)"; break;
                case CoopAction.RejectedInsufficient: label = "재료 부족"; break;
                case CoopAction.RejectedBusy: label = "사용 중"; break;
                case CoopAction.RejectedCounterFull: label = "카운터 가득"; break;
            }
            string s = label != null ? $"[{map.InteractText}] {label}" : "";
            if (p.Held != null) s += (s.Length > 0 ? "  " : "") + $"[{map.DrinkText}] 마시기";
            return s;
        }

        void ShowResult()
        {
            Time.timeScale = 1f;
            pausePanel.gameObject.SetActive(false);
            resultPanel.gameObject.SetActive(true);
            bool win = Sim.Outcome == CoopOutcome.Success;
            resultTitle.text = win ? "성공! 5분 동안 바를 지켜냈다" : "실패 — 두 사람 모두 쓰러졌다";
            resultTitle.color = win ? new Color(1f, 0.88f, 0.45f) : new Color(1f, 0.55f, 0.5f);
            int t = Mathf.FloorToInt(Sim.Elapsed);
            resultBody.text = $"버틴 시간 {t / 60}:{t % 60:00}   ·   처치 {Sim.Kills}\n제조 {Sim.CupsBrewed}잔   ·   음용 {Sim.CupsDrunk}잔   ·   역할 교대 {Sim.RoleSwaps}회\n\nR 다시 시작   ·   F10 종료";
        }

        // ───────── 생성 ─────────

        void LoadSprites()
        {
            ingredientSprites = new[] { Res("Icons/cocktail_ingredient"), Res("Icons/coffee_bean"), Res("Icons/tea") };
            // 완성 잔은 액체가 그려진 기존 아이콘(DrinkLab 잔 원본은 런타임 마스크 채색용 빈 잔/머신이라 완성품으로 읽히지 않음)
            drinkSprites = new[] { Res("ResearchIcons/research_mellow_finish"), Res("ResearchIcons/research_espresso_shot"), Extra("drop_cocktail") };
            stationAnim = new[]
            {
                Frames("DrinkLab/dl_steam_", 4),
                Frames("DrinkLab/dl_coffee_espresso_brew_", 4),
                Frames("DrinkLab/dl_shaker_shake_", 6),
            };
        }

        static Sprite Res(string path) => Resources.Load<Sprite>(path);

        static Sprite[] Frames(string prefix, int n)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < n; i++) { var s = Res($"{prefix}{i:00}"); if (s != null) list.Add(s); }
            return list.ToArray();
        }

        static Sprite Extra(string id)
        {
            var all = Resources.LoadAll<Sprite>("ArtExtra/" + id);
            return all != null && all.Length > 0 ? all[0] : null;
        }

        void BuildCamera()
        {
            var go = new GameObject("CoopCamera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.05f);
        }

        /// <summary>카메라 배율은 11-17과 같은 규칙(세로 10.8u 또는 가로 19.2u 중 좁은 쪽에 맞춤 — 캐릭터·타일 체감 크기 유지, 확장 맵 전체를 보이려고 줌아웃하지 않음).
        /// 가로는 항상 맵 전체(19.2u). 세로는 위·아래 HUD 띠를 뺀 사용 영역(UsableRect) 안에 두 사람 몸 전체가 들어가도록 두 사람의 위아래 범위 중앙을 따라가고, 월드 위·아래 끝에서 멈춘다.</summary>
        void FitCamera(bool snap = false)
        {
            var rect = cam.pixelRect;
            float sf = canvas != null ? canvas.scaleFactor : 1f;
            float topPx = topBar != null ? topBar.rect.height * sf : 0f;
            float botPx = bottomBar != null ? bottomBar.rect.height * sf : 0f;
            float ppu = Mathf.Min(rect.height / 10.8f, rect.width / (ViewMaxX - ViewMinX));
            PixelsPerUnit = ppu;
            cam.orthographicSize = rect.height / (2f * ppu);
            UsableRect = new Rect(rect.x, rect.y + botPx, rect.width, Mathf.Max(10f, rect.height - topPx - botPx));
            float topU = topPx / ppu, botU = botPx / ppu;
            float visH = UsableRect.height / ppu;
            // 두 사람 몸(발 아래 그림자 ~ 머리)의 위아래 범위
            float low = float.MaxValue, high = float.MinValue;
            foreach (var pv in playerViews)
            {
                if (pv == null) continue;
                var b = pv.Body.bounds;
                low = Mathf.Min(low, Mathf.Min(b.min.y, pv.Go.transform.position.y - 0.2f));
                high = Mathf.Max(high, b.max.y);
            }
            if (low > high) { low = ViewMinY; high = ViewMaxY; }
            float center = (low + high) * 0.5f;
            float minC = ViewMinY + visH * 0.5f, maxC = ViewMaxY - visH * 0.5f;
            center = minC > maxC ? (ViewMinY + ViewMaxY) * 0.5f : Mathf.Clamp(center, minC, maxC);
            // 사용 영역 가운데 = 카메라 중심 + (아래띠 - 위띠)/2
            float targetY = center - (botU - topU) * 0.5f;
            float y;
            if (snap || float.IsInfinity(camVelY)) { y = targetY; camVelY = 0f; }
            else y = Mathf.SmoothDamp(cam.transform.position.y, targetY, ref camVelY, 0.25f, 30f, Time.deltaTime);
            // 월드 끝 밖이 사용 영역에 드러나지 않게 다시 제한
            float yMin = minC - (botU - topU) * 0.5f, yMax = maxC - (botU - topU) * 0.5f;
            if (yMin <= yMax) y = Mathf.Clamp(y, yMin, yMax);
            cam.transform.position = new Vector3((ViewMinX + ViewMaxX) * 0.5f, y, -10f);
        }

        float camVelY = float.PositiveInfinity; // 첫 프레임은 바로 맞춤
        /// <summary>화면에서 HUD 띠를 뺀 월드가 보이는 영역(픽셀).</summary>
        public Rect UsableRect { get; private set; }

        /// <summary>플레이어 몸 전체(스프라이트 bounds)가 사용 영역 안에 보이는가 — 검증·화면 밖 안내용.</summary>
        public bool BodyFullyVisible(int i, out Rect screenRect)
        {
            var b = playerViews[i].Body.bounds;
            var a = cam.WorldToScreenPoint(b.min);
            var c = cam.WorldToScreenPoint(b.max);
            screenRect = Rect.MinMaxRect(a.x, a.y, c.x, c.y);
            return UsableRect.Contains(screenRect.min) && UsableRect.Contains(screenRect.max);
        }

        /// <summary>현재 화면에서 월드 1칸이 차지하는 픽셀(검증 기록용).</summary>
        public float PixelsPerUnit { get; private set; }

        static void SetSized(SpriteRenderer sr, Sprite sprite, float height)
        {
            if (sr.sprite == sprite) return;
            sr.sprite = sprite;
            if (sprite != null) sr.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
        }

        static int Order(float y) => 1000 - Mathf.RoundToInt(y * 50f);

        // 월드 표시 범위: 가로는 11-17과 같은 19.2u(홀 18.4 + 입구 기둥), 세로는 홀 아래 ~ 주방 뒷벽 위.
        const float ViewMinX = -9.6f, ViewMaxX = 9.6f;
        static float ViewMinY => T.MapBottomY;
        static float ViewMaxY => T.MapTopY;

        /// <summary>2026-11-18 증축 맵. 홀(11-17과 같은 18.4x5.1) 위에 바 카운터를 경계로 주방을 증축했다.
        /// 배경 아트는 모두 원본 배율(T.ArtScale)로 놓고 바닥 타일은 해당 구역 사각형 안에만 반복한다(늘림·화면 전체 반복 없음).
        /// 카운터·문 기둥·작업대·입구 기둥 위치는 시뮬레이션 충돌 상수와 같다.</summary>
        void BuildWorld()
        {
            var root = new GameObject("CoopWorld").transform;
            float w = T.ArenaMaxX - T.ArenaMinX;
            // 홀 바닥(바, 진한 나무): 홀 + 문 통로
            Tiled(root, "HallFloor", Extra("bg_bar_floor"), new Rect(T.ArenaMinX, T.MapBottomY, w, T.CounterMaxY - T.MapBottomY), -100, new Color(0.82f, 0.78f, 0.76f));
            // 주방 바닥(찻집): 카운터 뒤 ~ 벽 아래
            Tiled(root, "KitchenFloor", Extra("bg_tea_floor"), new Rect(T.ArenaMinX, T.CounterMaxY, w, 5.9f - T.CounterMaxY), -99, Color.white);
            // 주방 뒷벽(찻집 벽): 원본 높이 그대로
            var wallSprite = Extra("bg_tea_wall");
            float wallH = wallSprite != null ? wallSprite.bounds.size.y * T.ArtScale : 2.4f;
            Tiled(root, "KitchenWall", wallSprite, new Rect(T.ArenaMinX, T.MapTopY - wallH, w, wallH), -98, new Color(0.92f, 0.88f, 0.84f));

            // 바 카운터 = 홀/주방 경계. 원본 4u 폭 좌우 2개씩, 가운데 문 폭 2.4(충돌 문 폭과 같음).
            var counter = Extra("bg_bar_counter");
            foreach (float cx in new[] { -7.2f, -3.2f, 3.2f, 7.2f })
                Prop(root, "Counter", counter, new Vector2(cx, T.CounterMinY - 0.1f), Order(T.CounterMinY));
            var post = Extra("bg_bar_boundary");
            foreach (float x in new[] { -T.DoorHalfWidth, T.DoorHalfWidth })
                Prop(root, "DoorPost", post, new Vector2(x, T.CounterMinY - 0.1f), Order(T.CounterMinY) + 1);
            // 적 진입점: 홀 좌·우 벽 입구마다 기둥 한 쌍(T.Entrances와 같은 자리)
            foreach (var ent in T.Entrances)
                for (int k = -1; k <= 1; k += 2)
                {
                    var at = new Vector2(ent.x, ent.y + k * 0.75f - 0.35f);
                    Prop(root, "EntrancePost", post, at, Order(at.y));
                }

            // 공용 카운터(패스): 오른쪽 첫 카운터 윗면, 잔 두 자리
            Quad(root, "PassMat", new Vector2(T.PassCounterPos.x, 1.88f), new Vector2(1.8f, 0.26f), new Color(1f, 0.88f, 0.5f, 0.45f), Order(T.CounterMinY) + 2);
            for (int c = 0; c < counterCups.Length; c++)
            {
                counterCups[c] = MakeSprite(root, "CounterCup", null, new Vector2(T.PassCounterPos.x + (c == 0 ? -0.45f : 0.45f), 2.12f), 0.6f, Order(T.CounterMinY) + 3);
                counterCups[c].enabled = false;
            }

            // 작업대(찻집 카운터, 원본 4x2u) — 앞면 아래 끝이 충돌 앞면(StationPos.y)과 같다.
            var table = Extra("bg_tea_counter");
            for (int st = 0; st < 3; st++)
            {
                var pos = T.StationPos[st];
                int baseOrder = Order(pos.y);
                Prop(root, "Station", table, new Vector2(pos.x, pos.y - 0.05f), baseOrder);
                stationTool[st] = MakeSprite(root, "Tool", stationAnim[st].Length > 0 ? stationAnim[st][0] : null, new Vector2(pos.x - 0.95f, pos.y + 1.45f), 0.7f, baseOrder + 1);
                stationCup[st] = MakeSprite(root, "ReadyCup", drinkSprites[st], new Vector2(pos.x + 1.0f, pos.y + 1.4f), 0.6f, baseOrder + 3);
                stationCup[st].enabled = false;
                var sign = MakeSprite(root, "Sign", drinkSprites[st], new Vector2(pos.x + 1.0f, pos.y + 1.4f), 0.6f, baseOrder + 2);
                sign.color = new Color(1f, 1f, 1f, 0.35f);
            }
            // 벽 등(장식) — 작업대 사이 빈 벽
            var lamp = Extra("bg_tea_lamp");
            foreach (float lx in new[] { -4.4f, 2.2f })
                Prop(root, "Lamp", lamp, new Vector2(lx, 6.15f), -97);
        }

        /// <summary>배경 소품을 공통 배율로 아래 가운데 기준 배치.</summary>
        static void Prop(Transform root, string name, Sprite sprite, Vector2 bottomCenter, int order)
        {
            if (sprite == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * T.ArtScale;
            var b = sprite.bounds;
            go.transform.position = new Vector3(bottomCenter.x - b.center.x * T.ArtScale, bottomCenter.y - b.min.y * T.ArtScale, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
        }

        /// <summary>구역 사각형 안에만 타일 반복(공통 배율, 늘림 없음).</summary>
        static void Tiled(Transform root, string name, Sprite sprite, Rect rect, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = tint;
            if (sprite == null) return;
            go.transform.localScale = Vector3.one * T.ArtScale;
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = rect.size / T.ArtScale;
            var off = sprite.bounds.center * T.ArtScale;
            go.transform.position = new Vector3(rect.center.x - off.x, rect.center.y - off.y, 0f);
        }

        static void Quad(Transform root, string name, Vector2 pos, Vector2 size, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = c;
            sr.sortingOrder = order;
        }

        /// <summary>height&gt;0이면 그 높이(월드)로 균일 배율.</summary>
        static SpriteRenderer MakeSprite(Transform root, string name, Sprite sprite, Vector2 pos, float height, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (height > 0f && sprite != null) go.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
            return sr;
        }

        ActorView MakeActor(string name, string artId, float height, bool player)
        {
            var art = ArtLibrary.Get(artId);
            var go = new GameObject(name);
            var v = new ActorView { Go = go, Art = art };
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            v.Body = bodyGo.AddComponent<SpriteRenderer>();
            v.Body.sprite = art?.Clip("idle")?.frames.Length > 0 ? art.Clip("idle").frames[0] : art?.FirstSprite ?? SpriteFactory.Circle;
            float bodyWorld = art != null && art.bodyHeightPx > 0f ? art.bodyHeightPx / art.pixelsPerUnit : v.Body.sprite.bounds.size.y;
            v.Scale = height / Mathf.Max(0.05f, bodyWorld);
            bodyGo.transform.localScale = Vector3.one * v.Scale;
            var shadow = GroundShadow.Attach(go.transform, Vector2.zero, player ? 0.8f : 0.6f);
            if (shadow != null) shadow.sortingOrder = -60;
            var cupGo = new GameObject("Cup");
            cupGo.transform.SetParent(go.transform, false);
            v.Cup = cupGo.AddComponent<SpriteRenderer>();
            v.Cup.enabled = false;
            return v;
        }

        void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            var d = b - a;
            if (d.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            c.a = 0.85f;
            FadeVfx.Spawn(SpriteFactory.Square, (a + b) * 0.5f, ang, new Vector2(d.magnitude, width), new Vector2(d.magnitude, width * 0.3f), c, 0.09f, 1990);
        }

        // ───────── HUD ─────────

        void BuildHud()
        {
            canvas = UiFactory.CreateCanvas(transform, "CoopHud", 50);
            canvasRt = canvas.GetComponent<RectTransform>();
            Destroy(canvas.GetComponent<GraphicRaycaster>()); // 마우스 불필요
            // 월드에 붙는 라벨(태그·안내·작업대·카운터·떠오르는 글자)은 HUD 띠 사이 사용 영역 안에서만 보이게 잘라낸다.
            worldLayer = UiFactory.Stretch("WorldLayer", canvas.transform);
            worldLayer.offsetMin = new Vector2(0f, 66f);
            worldLayer.offsetMax = new Vector2(0f, -110f);
            worldLayer.gameObject.AddComponent<RectMask2D>();

            var top = UiFactory.Place("TopBar", canvas.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920f, 110f));
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f); top.sizeDelta = new Vector2(0f, 110f);
            topBar = top;
            UiFactory.AddImage(top, new Color(0.08f, 0.055f, 0.045f, 1f)); // 리니어 색공간에서 반투명이면 뒤 월드가 비쳐 보임

            huds[0] = BuildPlayerHud(top, 0);
            huds[1] = BuildPlayerHud(top, 1);

            timerText = Label(top, new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(560f, 50f), "5:00", 40, TextAnchor.MiddleCenter, Cream);
            stageText = Label(top, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(620f, 24f), "", 19, TextAnchor.MiddleCenter, new Color(0.85f, 0.8f, 0.72f));
            // 공유 재고 3종 + 공용 카운터 대기잔
            for (int i = 0; i < 4; i++)
            {
                float x = -195f + i * 130f;
                var icon = UiFactory.Place("StockIcon", top, new Vector2(0.5f, 1f), new Vector2(x - 22f, -76f), new Vector2(32f, 32f));
                var img = UiFactory.AddImage(icon, Color.white);
                img.sprite = i < 3 ? ingredientSprites[i] : drinkSprites[1];
                img.preserveAspect = true;
                var t = Label(top, new Vector2(0.5f, 1f), new Vector2(x + 30f, -76f), new Vector2(72f, 32f), "0", 26, TextAnchor.MiddleLeft, Cream);
                if (i < 3) { stockIcon[i] = icon; stockText[i] = t; }
                else counterText = t;
            }

            var bottom = UiFactory.Place("BottomBar", canvas.transform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1920f, 66f));
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1f, 0f); bottom.sizeDelta = new Vector2(0f, 66f);
            bottomBar = bottom;
            UiFactory.AddImage(bottom, new Color(0.08f, 0.055f, 0.045f, 1f));
            var b1 = Label(bottom, new Vector2(0f, 1f), new Vector2(24f, -2f), new Vector2(930f, 34f),
                $"P1  {CoopInput.P1.MoveText} 이동 · {CoopInput.P1.InteractText} 집기/놓기/건네기/제조/취소 · {CoopInput.P1.DrinkText} 마시기", 24, TextAnchor.MiddleLeft, P1Color);
            var b2 = Label(bottom, new Vector2(1f, 1f), new Vector2(-24f, -2f), new Vector2(930f, 34f),
                $"P2  {CoopInput.P2.MoveText} 이동 · {CoopInput.P2.InteractText} 집기/놓기/제조 · {CoopInput.P2.DrinkText} 마시기", 24, TextAnchor.MiddleRight, P2Color);
            Label(bottom, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(1880f, 28f),
                "문을 지나 주방 바닥(위)에 서면 제조(안전) · 홀 바닥(아래)에 서면 전투(자동 공격) · 전투 시간은 홀에 누가 있을 때만 흐름 · Esc 일시정지", 18, TextAnchor.MiddleCenter, new Color(0.85f, 0.8f, 0.72f));
            b1.resizeTextForBestFit = b2.resizeTextForBestFit = true;
            b1.resizeTextMinSize = b2.resizeTextMinSize = 16;
            b1.resizeTextMaxSize = b2.resizeTextMaxSize = 24;

            for (int i = 0; i < 2; i++)
            {
                var col = PlayerColor(i);
                playerTag[i] = Label(worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 32f), $"P{i + 1}", 24, TextAnchor.MiddleCenter, col);
                Outline(playerTag[i]);
                playerPrompt[i] = Label(worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 30f), "", 21, TextAnchor.MiddleCenter, col);
                Outline(playerPrompt[i]);
                reviveRoot[i] = UiFactory.Place("Revive", worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 14f));
                UiFactory.AddImage(reviveRoot[i], new Color(0f, 0f, 0f, 0.7f));
                reviveBar[i] = UiFactory.Place("Fill", reviveRoot[i], new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(0f, 10f));
                UiFactory.AddImage(reviveBar[i], new Color(0.5f, 1f, 0.5f));
                reviveRoot[i].gameObject.SetActive(false);
            }

            for (int s = 0; s < 3; s++)
            {
                var r = T.Recipe((CoopDrink)s);
                var root = UiFactory.Place("StationHud", worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 66f));
                UiFactory.AddImage(root, new Color(0.08f, 0.05f, 0.04f, 0.72f));
                var title = Label(root, new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(236f, 24f), $"{r.Name} · {ShortEffect((CoopDrink)s)}", 18, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.6f));
                title.resizeTextForBestFit = true; title.resizeTextMinSize = 13; title.resizeTextMaxSize = 18;
                var status = Label(root, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(236f, 24f), "", 18, TextAnchor.MiddleCenter, Cream);
                status.resizeTextForBestFit = true; status.resizeTextMinSize = 13; status.resizeTextMaxSize = 18;
                var barBg = UiFactory.Place("Bar", root, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(228f, 10f));
                UiFactory.AddImage(barBg, new Color(0f, 0f, 0f, 0.8f));
                var fill = UiFactory.Place("Fill", barBg, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(0f, 6f));
                UiFactory.AddImage(fill, new Color(0.5f, 0.85f, 0.95f));
                var group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                stationHuds[s] = new StationHud { Root = root, Fill = fill, Title = title, Status = status, Group = group };
            }

            counterWorldText = Label(worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 28f), "", 20, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.55f));
            Outline(counterWorldText);
            kitchenLabel = Label(worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 30f), "주방 — 제조 (적이 들어오지 않음)", 20, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.6f, 0.8f));
            Outline(kitchenLabel);
            hallLabel = Label(worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 30f), "바 홀 — 전투 구역", 22, TextAnchor.MiddleCenter, new Color(1f, 0.65f, 0.6f, 0.8f));
            Outline(hallLabel);

            var bannerRoot = UiFactory.Place("Banner", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(1500f, 46f));
            UiFactory.AddImage(bannerRoot, new Color(0.08f, 0.05f, 0.04f, 0.8f));
            bannerText = Label(bannerRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 46f), "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.7f));

            // 사용 영역 밖(위·아래 HUD 띠 뒤/화면 밖)에 몸이 걸린 사람: 가장자리 방향·거리 안내
            for (int i = 0; i < 2; i++)
            {
                offChip[i] = Label(canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 30f), "", 22, TextAnchor.MiddleCenter, PlayerColor(i));
                Outline(offChip[i]);
                var bg = offChip[i].gameObject.AddComponent<Shadow>();
                bg.effectColor = new Color(0f, 0f, 0f, 0.9f); bg.effectDistance = new Vector2(-2f, 2f);
            }
            pausePanel = Overlay("Pause", out pauseText, out _);
            pausePanel.gameObject.SetActive(false);
            resultPanel = Overlay("Result", out resultBody, out resultTitle);
            resultPanel.gameObject.SetActive(false);
        }

        static string ShortEffect(CoopDrink d) => d == CoopDrink.LeafTonic ? "회복" : d == CoopDrink.RushShot ? "연사" : "충격파";

        PlayerHud BuildPlayerHud(RectTransform top, int i)
        {
            bool right = i == 1;
            var anchor = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            float sx = right ? -1f : 1f;
            var h = new PlayerHud();
            var col = PlayerColor(i);
            var name = Label(top, anchor, new Vector2(sx * 20f, -8f), new Vector2(170f, 36f), i == 0 ? "P1 바텐더" : "P2 티 소믈리에", 26, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, col);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = 26;
            var roleRt = UiFactory.Place("Role", top, anchor, new Vector2(sx * 196f, -8f), new Vector2(180f, 36f));
            h.RoleBg = UiFactory.AddImage(roleRt, BrewColor);
            h.Role = Label(roleRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176f, 34f), "", 24, TextAnchor.MiddleCenter, Color.white);
            var hpBg = UiFactory.Place("Hp", top, anchor, new Vector2(sx * 20f, -48f), new Vector2(360f, 24f));
            UiFactory.AddImage(hpBg, new Color(0f, 0f, 0f, 0.8f));
            h.HpFill = UiFactory.Place("Fill", hpBg, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(356f, 20f));
            UiFactory.AddImage(h.HpFill, new Color(0.45f, 0.85f, 0.4f));
            h.Hp = Label(hpBg, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(356f, 24f), "", 18, TextAnchor.MiddleCenter, Color.white);
            Outline(h.Hp);
            h.Effects = Label(top, anchor, new Vector2(sx * 20f, -78f), new Vector2(560f, 28f), "", 20, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, new Color(0.95f, 0.88f, 0.7f));
            h.Effects.resizeTextForBestFit = true; h.Effects.resizeTextMinSize = 14; h.Effects.resizeTextMaxSize = 21;
            var slot = UiFactory.Place("Held", top, anchor, new Vector2(sx * 396f, -8f), new Vector2(68f, 68f));
            UiFactory.AddImage(slot, new Color(1f, 1f, 1f, 0.08f));
            var icon = UiFactory.Place("Icon", slot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
            h.HeldIcon = UiFactory.AddImage(icon, Color.white);
            h.HeldIcon.preserveAspect = true;
            h.HeldLabel = Label(top, anchor, new Vector2(sx * 480f, -30f), new Vector2(130f, 30f), "빈손", 20, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Cream);
            h.HeldLabel.resizeTextForBestFit = true; h.HeldLabel.resizeTextMinSize = 13; h.HeldLabel.resizeTextMaxSize = 20;
            return h;
        }

        RectTransform Overlay(string name, out Text body, out Text title)
        {
            var dim = UiFactory.Stretch(name, canvas.transform);
            UiFactory.AddImage(dim, new Color(0f, 0f, 0f, 0.6f));
            var panel = UiFactory.Place("Panel", dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 380f));
            UiFactory.AddImage(panel, new Color(0.12f, 0.08f, 0.06f, 0.96f));
            title = Label(panel, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(860f, 64f), "", 42, TextAnchor.MiddleCenter, Cream);
            body = Label(panel, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(860f, 260f), "", 28, TextAnchor.MiddleCenter, Cream);
            return dim;
        }

        static Text Label(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string content, int fontSize, TextAnchor align, Color color)
        {
            var rt = UiFactory.Place("Text", parent, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFactory.Font;
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void Outline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        void ClampBelowTop(RectTransform rt, float refPx)
        {
            var pos = rt.position;
            float maxY = UsableRect.yMax - refPx * canvas.scaleFactor;
            if (pos.y > maxY) rt.position = new Vector3(pos.x, maxY, 0f);
        }

        void PlaceAtWorld(RectTransform rt, Vector2 world)
        {
            var sp = cam.WorldToScreenPoint(world);
            rt.position = new Vector3(sp.x, sp.y, 0f);
        }

        void Hint(Vector2 world, string text, Color color)
        {
            var t = Label(worldLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 32f), text, 22, TextAnchor.MiddleCenter, color);
            Outline(t);
            floaters.Add(new Floater { Rt = t.rectTransform, Text = t, World = world, Life = 1.6f });
            PlaceAtWorld(t.rectTransform, world);
        }

        void FlyIcon(Sprite sprite, Vector2 world, RectTransform target)
        {
            var rt = UiFactory.Place("Fly", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            var img = UiFactory.AddImage(rt, Color.white);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var from = (Vector2)cam.WorldToScreenPoint(world);
            rt.position = from;
            floaters.Add(new Floater { Rt = rt, Image = img, Fly = true, FromScreen = from, ToScreen = target.position, Life = 0.7f });
        }

        void Banner(string text, float seconds)
        {
            bannerText.text = text;
            bannerText.color = new Color(1f, 0.92f, 0.7f);
            bannerTime = seconds;
            bannerText.transform.parent.gameObject.SetActive(true);
        }

        static string CostText(CoopRecipe r, string sign)
        {
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int c = r.Cost((Ingredient)i);
                if (c > 0) parts.Add($"{sign}{IngredientNames[i]}{c}");
            }
            return string.Join(" ", parts);
        }

        public static Color PlayerColor(int i) => i == 0 ? P1Color : P2Color;
    }
}
