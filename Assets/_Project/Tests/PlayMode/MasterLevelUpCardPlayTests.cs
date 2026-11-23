using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 2026-11-23 마스터 모드에서 레벨업 카드 창이 열렸을 때 관련 없는 제조·준비 UI의 가시성·입력(마우스 레이캐스트·B 키)과
    /// 닫힌 뒤 복원, 일반 모드 대조. 카드 창은 실제 GameManager.BeginLevelUp(추첨·시간 정지·저장 포함) 경로로 연다.
    /// CCT_EVIDENCE_DIR가 있으면 720p/1080p 렌더도 남긴다(없으면 렌더 생략, 판정만).
    /// </summary>
    public class MasterLevelUpCardPlayTests
    {
        GameManager game;
        Keyboard kb;
        UnityEngine.InputSystem.InputSettings.BackgroundBehavior savedBg;
        UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode savedEditor;
        bool savedSuppress;
        RunMode savedMode;
        string dir;
        readonly StringBuilder log = new StringBuilder();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CCT.InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            savedSuppress = CardEducationGuide.SuppressAutomaticForTests;
            CardEducationGuide.SuppressAutomaticForTests = true;
            savedMode = RunModeSelection.Current;
            dir = System.Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
            var s = UnityEngine.InputSystem.InputSystem.settings;
            savedBg = s.backgroundBehavior; savedEditor = s.editorInputBehaviorInPlayMode;
            s.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            s.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            kb = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>("MasterCardKeyboard");
            kb.MakeCurrent();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (kb != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(kb);
            kb = null;
            var s = UnityEngine.InputSystem.InputSystem.settings; s.backgroundBehavior = savedBg; s.editorInputBehaviorInPlayMode = savedEditor;
            if (!string.IsNullOrEmpty(dir) && log.Length > 0) { Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "evidence-log.txt"), log.ToString(), new UTF8Encoding(false)); }
            log.Clear();
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            RunModeSelection.Current = savedMode;
            CardEducationGuide.SuppressAutomaticForTests = savedSuppress;
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[MasterCard] " + s); }

        IEnumerator Build(RunMode mode)
        {
            RunModeSelection.Current = mode;
            game = GameBootstrap.Build(Job.Bartender, WeaponKind.Rapier);
            yield return null;
            game.Spawner.enabled = false;
            game.Stats.baseMaxHp = 100000f;
            game.Player.Heal(1f);
            if (game.Master != null) game.Master.BannersEnabled = false;
        }

        bool OpenLevelUp()
        {
            var m = typeof(GameManager).GetMethod("BeginLevelUp", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)m.Invoke(game, new object[] { CardCategory.Cocktail });
        }

        IEnumerator PressB()
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState(Key.B));
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        static GameObject TopHit(RectTransform target)
        {
            var c = new Vector3[4]; target.GetWorldCorners(c);
            var pos = (Vector2)((c[0] + c[2]) * 0.5f);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        static RectTransform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true)) if (t.name == name) return t;
            return null;
        }

        IEnumerator Snap(string name)
        {
            if (string.IsNullOrEmpty(dir)) yield break;
            var canvas = game.CardView.GetComponentInParent<Canvas>();
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1280, 720, name + "_1280x720");
            yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new[] { canvas }, 1920, 1080, name + "_1920x1080");
        }

        [UnityTest]
        public IEnumerator Master_Wave_CardOpen_BlocksBrewUi_ThenRestoresImmediately()
        {
            yield return Build(RunMode.Endless);
            Assert.IsTrue(game.IsEndless);
            var hud = game.MasterHud;
            // 준비 패널 상태 기록 후 전투 시작(카드는 전투 중 픽업으로만 열린다 — 준비 중에는 시간 정지)
            Assert.IsTrue(game.MasterPrepActive, "주기 시작 = 준비");
            yield return Snap("master_prep_before_battle");
            Assert.IsTrue(game.Master.StartBattle());
            float until = Time.realtimeSinceStartup + 0.7f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(game.CanOpenMasterBrew);
            Assert.IsTrue(hud.BrewButtonInteractable, "전투 중 제조 버튼 활성");
            var brewRt = Find(hud.transform, "BrewButton") ?? (RectTransform)hud.GetComponentInChildren<UnityEngine.UI.Button>(true).transform;
            Line($"master wave: brewButton={brewRt.name} notice={hud.Prep.NoticeVisible} prepPanel={hud.Prep.PanelVisible}");
            yield return Snap("master_wave_before_card");

            Assert.IsTrue(OpenLevelUp(), "실제 레벨업 경로로 카드 창");
            Assert.AreEqual(GameState.LevelUp, game.State);
            yield return null; yield return null;
            Assert.IsFalse(game.CanOpenMasterBrew, "카드 창 위에서 제조 열기 불가");
            Assert.IsFalse(hud.BrewButtonInteractable, "카드 창이 열린 다음 프레임에 제조 버튼 비활성(0.5초 주기 대기 없음)");
            var top = TopHit(brewRt);
            Assert.IsNotNull(top);
            Assert.IsTrue(top.transform.IsChildOf(game.CardView.transform), $"제조 버튼 자리 클릭은 카드 창 덮개가 받는다(맨 위={top.name})");
            Assert.IsFalse(hud.Prep.PanelVisible, "준비 패널 없음(전투 중)");
            yield return PressB();
            Assert.IsFalse(hud.BrewOpen, "카드 창 위 B 키로 제조 창 안 열림");
            Assert.AreEqual(GameState.LevelUp, game.State, "카드 창 유지");
            Assert.IsTrue(game.CardView.IsOpen);
            // 정착 뒤 화면
            until = Time.realtimeSinceStartup + 2f;
            while (game.CardView.HandoffPlaying && Time.realtimeSinceStartup < until) yield return null;
            Line($"master card open: brewInteractable={hud.BrewButtonInteractable} canOpen={game.CanOpenMasterBrew} notice={hud.Prep.NoticeVisible} topAtBrew={top.name}");
            yield return Snap("master_wave_card_open_settled");

            game.CardView.Choose(0);
            yield return null;
            int frames = 0;
            while (!hud.BrewButtonInteractable && frames < 60) { frames++; yield return null; }
            Line($"master card closed: state={game.State} framesUntilBrewInteractable={frames} canOpen={game.CanOpenMasterBrew}");
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.LessOrEqual(frames, 1, "닫힌 직후 제조 버튼 복원");
            Assert.AreEqual(1f, Time.timeScale, "전투 재개");
            yield return PressB();
            Assert.IsTrue(hud.BrewOpen, "닫힌 뒤 B 키로 제조 창 열림(정상 복원)");
            yield return Snap("master_after_card_brew_by_B");
            game.CloseMasterBrew();
            yield return null;
            Assert.IsFalse(hud.BrewOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Master_Prep_BrewButton_FollowsOpenRule()
        {
            yield return Build(RunMode.Endless);
            var prep = game.MasterHud.Prep;
            yield return null; yield return null;
            Assert.IsTrue(prep.PanelVisible);
            Assert.IsTrue(prep.BrewButtonInteractable, "준비 중 제조 버튼 활성");
            game.OpenPause();
            yield return null;
            Assert.IsFalse(prep.BrewButtonInteractable, "일시정지 위에서는 준비 패널 제조 버튼 비활성(다음 프레임)");
            game.ClosePause();
            float until = Time.realtimeSinceStartup + 1.4f; // '계속' 카운트다운
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(prep.BrewButtonInteractable, "복원");
        }

        [UnityTest]
        public IEnumerator Standard_CardOpen_NoMasterUi_BKeyHarmless_Restores()
        {
            yield return Build(RunMode.Standard);
            Assert.IsNull(game.MasterHud, "일반 모드: 마스터 HUD 없음");
            Assert.IsTrue(OpenLevelUp());
            yield return null;
            yield return PressB();
            Assert.AreEqual(GameState.LevelUp, game.State, "B 키 영향 없음");
            float until = Time.realtimeSinceStartup + 2f;
            while (game.CardView.HandoffPlaying && Time.realtimeSinceStartup < until) yield return null;
            yield return Snap("standard_card_open_settled");
            game.CardView.Choose(0);
            yield return null;
            Assert.AreEqual(GameState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Line("standard card closed: state=Playing timeScale=1");
        }
    }
}
