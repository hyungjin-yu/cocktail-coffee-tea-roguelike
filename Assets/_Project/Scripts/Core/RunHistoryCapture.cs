#if CCT_QA_TOUR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 67 QA 플레이어 전용(CCT_QA_TOUR 심볼에서만 컴파일, -cct-data-dir 격리 필수): 실제 런타임 UI로 타이틀·빈 기록·결과·이번 판 빌드 상세·
    /// 20판 목록·긴 원인 상세를 캡처하고 글 상자 넘침을 측정한다. 카드는 실제 선택과 같은 경로(GrantCardForTests)로, 사망은 실제 피해 경로
    /// (PlayerController.TakeDamage + 출처)로 만든다. 20판 목록 중 19판은 QA가 넣은 기록(qa-seeded)이며 보고서에 그렇게 적는다.
    /// </summary>
    public class RunHistoryCapture : MonoBehaviour
    {
        string output;
        readonly List<string> overflow = new List<string>();
        readonly List<string> shots = new List<string>();
        readonly List<string> preexisting = new List<string>();
        int minFontPx = int.MaxValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-cct-runhistory-capture");
            if (i < 0 || i + 1 >= args.Length || Array.IndexOf(args, "-cct-data-dir") < 0) return;
            var go = new GameObject("RunHistoryCapture");
            DontDestroyOnLoad(go);
            var v = go.AddComponent<RunHistoryCapture>();
            v.output = args[i + 1];
            v.StartCoroutine(v.Run());
        }

        IEnumerator Shot(string label, Transform measureRoot)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            if (measureRoot != null) { Measure(label, measureRoot); Geometry(label, measureRoot); }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, label + ".png"));
            shots.Add(label);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        /// <summary>활성 글 상자마다 실제 줄바꿈 높이/폭이 상자를 넘는지(넘치면 행·버튼 경계를 침범하거나 잘린다).</summary>
        void Measure(string label, Transform root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var t in root.GetComponentsInChildren<Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || !t.isActiveAndEnabled) continue;
                var rect = t.rectTransform.rect;
                float h = t.preferredHeight;
                bool tooTall = h > rect.height + 1.5f;
                bool tooWide = t.horizontalOverflow == HorizontalWrapMode.Overflow && t.preferredWidth > rect.width + 1.5f;
                if ((tooTall || tooWide) && HierPath(t.transform).EndsWith("UI/Result/Title"))
                    preexisting.Add($"{label}: 기존 결과 제목(67 이전 UI) pref={h:0} box={rect.height:0}");
                else if (tooTall || tooWide) overflow.Add($"{label}: {HierPath(t.transform)} '{t.text.Replace("\n", "⏎")}' pref={t.preferredWidth:0}x{h:0} box={rect.width:0}x{rect.height:0}");
                var canvas = t.canvas;
                if (canvas != null) minFontPx = Mathf.Min(minFontPx, Mathf.RoundToInt(t.fontSize * canvas.scaleFactor));
            }
        }

        /// <summary>글 상자가 자기 행(RunRow_/Card_) 밖으로 나가거나, 상세 패널의 머리 글과 스크롤 영역이 겹치는지.</summary>
        void Geometry(string label, Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Text>(false))
            {
                var row = t.transform.parent;
                if (row == null || !(row.name.StartsWith("RunRow_") || row.name.StartsWith("Card_"))) continue;
                if (!Inside(t.rectTransform, (RectTransform)row)) overflow.Add($"{label}: {HierPath(t.transform)} outside row");
            }
            foreach (var panel in root.GetComponentsInChildren<RectTransform>(false).Where(r => r.name == "Panel" && r.parent != null && r.parent.name == "RunDetail"))
            {
                var kids = panel.Cast<Transform>().Where(k => k.gameObject.activeSelf && k.name != "Close" && k.name != "ScrollHint").Select(k => (RectTransform)k).ToList();
                for (int a = 0; a < kids.Count; a++)
                    for (int b = a + 1; b < kids.Count; b++)
                        if (WorldRect(kids[a]).Overlaps(WorldRect(kids[b]))) overflow.Add($"{label}: {kids[a].name} overlaps {kids[b].name}");
                if (!Inside((RectTransform)panel.Find("Scroll"), panel)) overflow.Add($"{label}: Scroll outside panel");
            }
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x + 0.5f, c[0].y + 0.5f, c[2].x - 0.5f, c[2].y - 0.5f);
        }

        static bool Inside(RectTransform child, RectTransform parent)
        {
            var a = WorldRect(child); var b = WorldRect(parent);
            return a.xMin >= b.xMin - 1f && a.xMax <= b.xMax + 1f && a.yMin >= b.yMin - 1f && a.yMax <= b.yMax + 1f;
        }

        static string HierPath(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null && parts.Count < 5; x = x.parent) parts.Insert(0, x.name);
            return string.Join("/", parts);
        }

        static CardData Card(string id) => CardDatabase.LoadAll().First(c => c.id == id);

        IEnumerator Run()
        {
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2f);
            var args = Environment.GetCommandLineArgs();
            int w = Array.IndexOf(args, "-cct-capture-width"), h = Array.IndexOf(args, "-cct-capture-height");
            if (w >= 0 && h >= 0)
            {
                Screen.SetResolution(int.Parse(args[w + 1]), int.Parse(args[h + 1]), FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(2f);
            }
            string error = null;
            var log = new List<string>();

            // 1) 타이틀 + 빈 기록
            var title = FindFirstObjectByType<TitleView>();
            if (title == null) { GameBootstrap.ShowTitle(); yield return null; title = FindFirstObjectByType<TitleView>(); }
            yield return Shot("01-title", title.transform);
            title.OpenHistory();
            yield return Shot("02-history-empty", title.HistoryView.transform);
            if (!title.HistoryView.EmptyVisible) error = "empty state not shown";
            title.HistoryView.Close();
            Destroy(title.gameObject);
            yield return null;

            var seedCards = CardDatabase.LoadAll().Where(c => c.category != CardCategory.Hybrid).ToList();
            // 2) QA 기록 19판 먼저(긴 원인·이름·여러 종료, 오래된 순으로 추가) — 실제 런이 가장 최신(맨 위)이 된다
            string[] causes =
            {
                "에스프레소 머신 리바이어던 · 압력 게이지 폭발", "드립 서버 스펙터 · 재등장 장판", "티 세리머니 가디언 · 원거리 공격",
                "올리브 바머 · 자폭", "까다로운 차 손님 · 뜨거운 찻물 장판", DamageSource.UnknownLabel,
            };
            var kinds = new[] { RunEndKind.Died, RunEndKind.Cleared, RunEndKind.TimeUp, RunEndKind.Abandoned };
            for (int i = 0; i < 19; i++)
            {
                var kind = kinds[i % kinds.Length];
                var r = new RunRecord
                {
                    runId = $"qa-seeded-{i:00}", endKind = kind.ToString(),
                    endedUtc = DateTime.UtcNow.AddHours(-(19 - i)).ToString("o"),
                    job = (i % 2 == 0 ? Job.TeaSommelier : Job.Bartender).ToString(), weapon = ((WeaponKind)(i % 4)).ToString(),
                    runSeconds = 40f + i * 61f, kills = 10 + i * 13, cocktailLevel = i % 7, teaLevel = 1 + i % 6, researchUnlocked = i % 5,
                    deathCause = kind == RunEndKind.Died ? causes[i % causes.Length] : "", deathCauseKind = kind == RunEndKind.Died ? "BossSkill" : "",
                    appVersion = Application.version,
                };
                foreach (var c in seedCards.Where(c => i % 2 != 0 || c.category == CardCategory.Tea).Take(4 + i % 9)) r.cards.Add(new RunCardEntry { id = c.id, name = c.displayName, category = c.category.ToString(), stacks = 1 + i % c.maxStacks, maxStacks = c.maxStacks });
                if (i % 3 == 0) r.cards.Add(new RunCardEntry { id = "qa_removed_card", name = "삭제된 옛 카드 이름(저장 당시)", category = "Tea", stacks = 2, maxStacks = 5 });
                RunHistory.Add(r);
            }
            // 3) 카드가 많은 실제 런 → 긴 이름의 보스 기술로 사망 → 결과 → 이번 판 빌드
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            foreach (var spawner in game.GetComponentsInChildren<EnemySpawner>()) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            var melee = CardDatabase.LoadAll().Where(c => c.category != CardCategory.Hybrid && CardSystem.IsWeaponCompatible(c, false)).ToList();
            int n = 0;
            foreach (var c in melee)
            {
                int stacks = c.id == "molotov" || c.id == "flambe" ? c.maxStacks : 1 + n % Mathf.Max(1, c.maxStacks);
                for (int s = 0; s < stacks; s++) game.GrantCardForTests(c);
                n++;
            }
            game.SetRunTimeForTests(15 * 60 + 42);
            yield return null;
            var lethal = new DamageSource(DamageKind.BossSkill, "에스프레소 머신 리바이어던", "압력 게이지 폭발");
            game.Player.TakeDamage(game.Player.MaxHp * 50f, lethal);
            yield return null;
            yield return new WaitForSecondsRealtime(GameManager.DeathResultDelaySeconds + 0.3f);
            var result = FindObjectsByType<ResultView>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsOpen);
            if (result == null) { error = "result not open"; }
            else
            {
                log.Add($"real run: {game.RunRecord.cards.Count} kinds, combos {game.RunRecord.combos.Count}, cause '{game.RunRecord.deathCause}', save={game.RunHistoryResult}");
                yield return Shot("03-result", result.transform);
                result.OpenDetail();
                yield return Shot("04-build-detail-top", result.Detail.transform);
                result.Detail.Scroll.verticalNormalizedPosition = 0f;
                yield return Shot("05-build-detail-bottom", result.Detail.transform);
                result.Detail.Close();
                if (result.Detail.IsOpen || !result.IsOpen) error = "detail close leaked";
            }

            game.GoToTitle();
            yield return null;
            yield return null;
            title = FindFirstObjectByType<TitleView>();
            title.OpenHistory();
            var list = title.HistoryView;
            log.Add($"history rows {list.RowCount}, file {RunHistory.ActivePath} exists={File.Exists(RunHistory.ActivePath)} dirty={RunHistory.IsDirty}");
            if (list.RowCount != 20) error = $"expected 20 rows, got {list.RowCount}";
            yield return Shot("06-history-20-top", list.transform);
            list.Scroll.verticalNormalizedPosition = 0f;
            yield return Shot("07-history-20-bottom", list.transform);
            list.Scroll.verticalNormalizedPosition = 1f;
            list.Row(0).onClick.Invoke();
            yield return Shot("08-history-detail-real", list.Detail.transform);
            list.Detail.Close();
            list.Row(7).onClick.Invoke(); // qa-seeded-12: 사망 · 에스프레소 머신 리바이어던 원인 + 삭제된 옛 카드 이름
            yield return Shot("09-history-detail-long-cause", list.Detail.transform);
            if (!list.Detail.AllText.Contains("삭제된 옛 카드 이름(저장 당시)")) error = "removed card stored name not shown";
            list.Detail.Close();
            if (list.Detail.IsOpen || !list.IsOpen) error = "history detail close leaked";

            var resultJson = new Result
            {
                width = Screen.width, height = Screen.height, touch = MobilePlatform.IsTouchUI, overflowCount = overflow.Count,
                overflow = overflow.ToArray(), preexisting = preexisting.Distinct().ToArray(), shots = shots.ToArray(), log = log.ToArray(), error = error ?? "",
                minFontPx = minFontPx == int.MaxValue ? 0 : minFontPx, historyRows = list.RowCount,
            };
            File.WriteAllText(System.IO.Path.Combine(output, "capture-result.json"), JsonUtility.ToJson(resultJson, true));
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(error == null && overflow.Count == 0 ? 0 : 1);
        }

        [Serializable]
        class Result
        {
            public int width, height, overflowCount, minFontPx, historyRows;
            public bool touch;
            public string error;
            public string[] overflow, preexisting, shots, log;
        }
    }
}
#endif
