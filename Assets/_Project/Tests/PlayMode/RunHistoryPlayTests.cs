using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CCT.Tests
{
    /// <summary>
    /// 67(2026-10-16) 플레이 결과 기록 — 실제 게임 루프에서: 적 투사체·몸 접촉·장판 지속 피해로 쓰러졌을 때의 사망 원인,
    /// 결과 "이번 판 빌드" → 상세 → 닫기(Esc/모바일 뒤로는 상세부터, R·클릭이 다시 시작으로 관통하지 않음),
    /// 다시 시작·타이틀 이동 뒤 중복 기록 없음, 타이틀 "최근 기록" 목록 → 상세 → 뒤로. 저장은 테스트마다 새 임시 폴더.
    /// </summary>
    public class RunHistoryPlayTests
    {
        string dir;
        Keyboard kb;
        UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
        UnityEngine.InputSystem.InputSettings.BackgroundBehavior savedBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            DestroyMenus();
            dir = Path.Combine(Application.temporaryCachePath, $"cct_rh_play_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            PermanentProgress.SetPathForTests(Path.Combine(dir, "save.json"));
            RunHistory.SetPathForTests(null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (kb != null)
            {
                InputSystem.RemoveDevice(kb);
                kb = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
                InputSystem.settings.backgroundBehavior = savedBackground;
            }
            if (GameManager.Instance != null) UnityEngine.Object.Destroy(GameManager.Instance.gameObject);
            DestroyMenus();
            Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            RunHistory.SetPathForTests(null);
            try { Directory.Delete(dir, true); } catch { /* 임시 */ }
        }

        static void DestroyMenus()
        {
            foreach (var v in UnityEngine.Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<CharacterSelectView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
            foreach (var v in UnityEngine.Object.FindObjectsByType<FirstRunTutorialView>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(v.gameObject);
        }

        static GameManager StartQuietRun(Job job = Job.Bartender, WeaponKind weapon = WeaponKind.Rapier)
        {
            var game = GameBootstrap.Build(job, weapon);
            foreach (var spawner in game.GetComponentsInChildren<EnemySpawner>()) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            return game;
        }

        static CardData Card(string id) => CardDatabase.LoadAll().First(c => c.id == id);

        bool sawPress;

        IEnumerator Key(Key key)
        {
            if (kb == null)
            {
                // 배치 PlayMode에는 게임 뷰 포커스가 없어 기본 설정이면 키 입력이 게임에 전달되지 않는다 — 이 시험 동안만 항상 전달.
                savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                savedBackground = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                kb = InputSystem.AddDevice<Keyboard>("RunHistoryKeyboard");
            }
            kb.MakeCurrent();
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null;
            sawPress = kb[key].isPressed;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        static IEnumerator WaitUntilDead(PlayerController p, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!p.IsDead && Time.realtimeSinceStartup < end) yield return null;
        }

        static ResultView OpenResult() => UnityEngine.Object.FindObjectsByType<ResultView>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsOpen);

        [UnityTest]
        public IEnumerator ProjectileDeath_ResultBuildDetail_EscClosesDetailFirst_RDoesNotPassThrough()
        {
            var game = StartQuietRun();
            game.GrantCardForTests(Card("dry_gin"));
            game.GrantCardForTests(Card("dry_gin"));
            game.GrantCardForTests(Card("green_tea"));
            var p = game.Player;
            EnemyProjectile.Spawn(p.Position + Vector2.right * 0.3f, Vector2.left, 0.5f, p.MaxHp * 10f, p, game.transform, null, null,
                DamageSource.Projectile(EnemyDef.Imp));
            yield return WaitUntilDead(p, 2f);
            Assert.IsTrue(p.IsDead, "실제 적 투사체 경로로 쓰러진다");
            yield return null;
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(RunEndKind.Died, game.RunRecord.EndKind);
            Assert.AreEqual("라임웨지 · 원거리 공격", game.RunRecord.deathCause);
            Assert.AreEqual(1, RunHistory.Records.Count, "결과 패널 지연 중에도 종료 즉시 기록");

            yield return new WaitForSecondsRealtime(GameManager.DeathResultDelaySeconds + 0.2f);
            var result = OpenResult();
            Assert.IsNotNull(result, "결과 화면");
            Assert.IsNotNull(result.transform.Find("RestartButton"), "기존 버튼 유지");
            StringAssert.Contains("저장했습니다", result.HistoryStatusText);

            result.BuildDetailButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(result.Detail.IsOpen);
            Assert.AreEqual(result.Detail.CloseButton.gameObject, EventSystem.current.currentSelectedGameObject, "초점은 상세의 닫기");
            StringAssert.Contains("라임웨지 · 원거리 공격", result.Detail.AllText);
            StringAssert.Contains(Card("dry_gin").displayName, result.Detail.AllText);
            StringAssert.Contains("×2 / ", result.Detail.AllText);

            yield return Key(UnityEngine.InputSystem.Key.R);
            Assert.IsTrue(sawPress, "전제: 가상 키 입력이 게임 프레임에 도달");
            Assert.AreSame(game, GameManager.Instance, "상세가 열린 동안 R은 다시 시작으로 관통하지 않는다");
            Assert.IsTrue(result.Detail.IsOpen);

            yield return Key(UnityEngine.InputSystem.Key.Escape);
            Assert.IsFalse(result.Detail.IsOpen, "Esc는 상세부터 닫는다");
            Assert.IsTrue(result.IsOpen, "결과 화면은 그대로");
            Assert.AreEqual(GameState.GameOver, game.State);
            Assert.AreEqual(0f, Time.timeScale, "결과 동안 시간 정지 유지");
            Assert.AreEqual(result.BuildDetailButton.gameObject, EventSystem.current.currentSelectedGameObject, "초점은 열었던 버튼으로");

            game.Restart(); // 결과의 "다시 시작"
            yield return null;
            Assert.AreEqual(1, RunHistory.Records.Count, "다시 시작이 같은 판을 다시 기록하지 않는다");
            Assert.AreEqual(1, PermanentProgress.Current.failedRuns);
            Assert.AreEqual(0, PermanentProgress.Current.abandonedRuns, "보상·집계 중복 없음");
        }

        [UnityTest]
        public IEnumerator MobileBack_ClosesDetailBeforeResultTitleButton()
        {
            var game = StartQuietRun();
            game.SetRunTimeForTests(Balance.RunTimeLimitSeconds);
            yield return null;
            yield return null;
            var result = OpenResult();
            Assert.IsNotNull(result);
            Assert.AreEqual(RunEndKind.TimeUp, game.RunRecord.EndKind);
            result.OpenDetail();
            Assert.IsTrue(result.Detail.IsOpen);
            Assert.AreEqual("Close", MobileBackRouter.HandleBack(), "가장 위 상세의 닫기");
            Assert.IsFalse(result.Detail.IsOpen);
            Assert.AreSame(game, GameManager.Instance, "결과 타이틀 버튼으로 관통하지 않는다");
            Assert.IsTrue(result.IsOpen);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContactDeath_ByRealEnemy_NamesThatEnemy()
        {
            var game = StartQuietRun();
            var p = game.Player;
            p.TakeDamage(p.MaxHp - 0.5f); // 출처 없는 단발 피해 후 0.8초 무적
            Assert.IsNull(p.LastDamageSource.Value.attacker);
            var brute = Enemy.SpawnAt(p.Position + Vector2.right * 0.2f, EnemyDef.Brute, 1f, p, game.transform);
            yield return WaitUntilDead(p, 4f);
            Assert.IsTrue(p.IsDead, "몸 접촉으로 쓰러진다");
            yield return null;
            Assert.AreEqual($"{EnemyDef.Brute.name} · 몸통 접촉", game.RunRecord.deathCause);
            Assert.AreEqual("Contact", game.RunRecord.deathCauseKind);
            Assert.IsTrue(brute != null);
        }

        [UnityTest]
        public IEnumerator ZoneDoTDeath_DuringInvulnerability_NamesZone_NotEarlierAttacker()
        {
            var game = StartQuietRun();
            var p = game.Player;
            p.TakeDamage(1f, DamageSource.Contact(EnemyDef.Imp)); // 이전 공격자 + 단발 무적
            BurstZone.Spawn(p.Position, 2f, p.MaxHp * 20f, 5f, p, game.transform, BurstZone.OliveFireArt,
                DamageSource.Zone(EnemyDef.OliveBomber, "자폭 장판"));
            yield return WaitUntilDead(p, 3f);
            Assert.IsTrue(p.IsDead, "장판 지속 피해는 단발 무적에 막히지 않는다");
            yield return null;
            Assert.AreEqual($"{EnemyDef.OliveBomber.name} · 자폭 장판", game.RunRecord.deathCause);
            Assert.AreEqual("Zone", game.RunRecord.deathCauseKind);
        }

        [UnityTest]
        public IEnumerator PauseQuitToTitle_RecordsAbandonedOnce_AndTitleHistoryListDetailBack()
        {
            var game = StartQuietRun(Job.TeaSommelier, WeaponKind.Bow);
            game.GrantCardForTests(Card("green_tea"));
            game.OpenPause();
            game.QuitToTitle();
            yield return null;
            Assert.AreEqual(1, RunHistory.Records.Count);
            Assert.AreEqual(RunEndKind.Abandoned, RunHistory.Records[0].EndKind);
            Assert.AreEqual("", RunHistory.Records[0].deathCause);

            var title = UnityEngine.Object.FindFirstObjectByType<TitleView>();
            Assert.IsNotNull(title);
            var button = title.GetComponentsInChildren<Button>(true).First(b => b.name == "RunHistoryButton");
            button.onClick.Invoke();
            yield return null;
            var list = title.HistoryView;
            Assert.IsTrue(list.IsOpen);
            Assert.IsFalse(title.IsOpen, "타이틀 메뉴는 가려져 클릭이 닿지 않는다");
            Assert.AreEqual(1, list.RowCount);
            Assert.IsFalse(list.EmptyVisible);
            Assert.AreEqual(list.Row(0).gameObject, EventSystem.current.currentSelectedGameObject);
            StringAssert.Contains("포기", list.Row(0).GetComponentsInChildren<Text>().First().text);

            list.Row(0).onClick.Invoke();
            yield return null;
            Assert.IsTrue(list.Detail.IsOpen);
            StringAssert.Contains(Card("green_tea").displayName, list.Detail.AllText);
            StringAssert.Contains("플레이어가 판을 중단", list.Detail.AllText);

            yield return Key(UnityEngine.InputSystem.Key.Escape);
            Assert.IsFalse(list.Detail.IsOpen, "Esc 한 번은 상세만");
            Assert.IsTrue(list.IsOpen);
            Assert.AreEqual(list.Row(0).gameObject, EventSystem.current.currentSelectedGameObject);

            list.Row(0).onClick.Invoke();
            Assert.AreEqual("Close", MobileBackRouter.HandleBack());
            Assert.IsTrue(list.IsOpen);
            Assert.AreEqual("BackButton", MobileBackRouter.HandleBack());
            Assert.IsFalse(list.IsOpen);
            Assert.IsTrue(title.IsOpen, "뒤로 → 타이틀");
        }

        [UnityTest]
        public IEnumerator TitleHistory_Empty_ThenTwentyNewestFirst()
        {
            GameBootstrap.ShowTitle();
            yield return null;
            var title = UnityEngine.Object.FindFirstObjectByType<TitleView>();
            title.OpenHistory();
            yield return null;
            Assert.IsTrue(title.HistoryView.EmptyVisible, "기록 없음 안내");
            Assert.AreEqual(0, title.HistoryView.RowCount);
            Assert.AreEqual(title.HistoryView.BackButton.gameObject, EventSystem.current.currentSelectedGameObject);
            title.HistoryView.Close();

            for (int i = 0; i < 23; i++)
                RunHistory.Add(new RunRecord
                {
                    runId = "r" + i, endKind = (i % 4 == 0 ? RunEndKind.Died : RunEndKind.Cleared).ToString(), job = "Bartender",
                    weapon = "Gun", runSeconds = 60 * i, endedUtc = DateTime.UtcNow.AddMinutes(i).ToString("o"),
                    deathCause = i % 4 == 0 ? "에스프레소 머신 리바이어던 · 압력 게이지 폭발" : "",
                });
            title.OpenHistory();
            yield return null;
            Assert.AreEqual(20, title.HistoryView.RowCount);
            StringAssert.StartsWith("1.", title.HistoryView.Row(0).GetComponentsInChildren<Text>().First().text);
            StringAssert.Contains("22:00", title.HistoryView.Row(0).GetComponentsInChildren<Text>().First().text, "최신(22분 판)이 맨 위");
            var nav = title.HistoryView.Row(19).navigation;
            Assert.AreEqual(title.HistoryView.BackButton, nav.selectOnDown, "마지막 행 아래 = 뒤로");
            Assert.Greater(title.HistoryView.Content.rect.height, title.HistoryView.Scroll.viewport.rect.height, "20판은 스크롤");
        }
    }
}
