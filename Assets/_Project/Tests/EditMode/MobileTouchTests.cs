using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>2026-10-12 Android: 조이스틱 수식, 안전 영역, 모바일 설정 화면, 모바일 화면 어댑터, Android 빌드 설정.</summary>
    public class MobileTouchTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            MobilePlatform.ForceForTests(null);
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void StickValue_DeadZone_Saturation_Direction()
        {
            float r = TouchControlsView.JoystickRadius;
            Assert.AreEqual(Vector2.zero, TouchControlsView.StickValue(new Vector2(r * 0.1f, 0f), r), "데드존 안은 0");
            Assert.AreEqual(1f, TouchControlsView.StickValue(new Vector2(r * 3f, 0f), r).magnitude, 1e-5f, "반지름 밖은 1로 포화");
            var half = TouchControlsView.StickValue(new Vector2(0f, -r * 0.56f), r);
            Assert.AreEqual(0.5f, half.magnitude, 1e-3f, "데드존 뒤 0..1로 다시 펼침");
            Assert.Less(half.y, 0f); Assert.AreEqual(0f, half.x, 1e-6f);
            var diag = TouchControlsView.StickValue(new Vector2(r, r), r);
            Assert.AreEqual(1f, diag.magnitude, 1e-5f, "대각선도 크기 1 이하");
        }

        [Test]
        public void SafeAnchors_NotchAndGestureInsets_FullWhenInvalid()
        {
            // 2400×1080 가로 폰, 왼쪽 노치 90px, 아래 제스처 바 40px.
            MobilePlatform.SafeAnchors(new Rect(90, 40, 2310, 1040), 2400, 1080, out var min, out var max);
            Assert.AreEqual(90f / 2400f, min.x, 1e-6f); Assert.AreEqual(40f / 1080f, min.y, 1e-6f);
            Assert.AreEqual(1f, max.x, 1e-6f); Assert.AreEqual(1f, max.y, 1e-6f);
            MobilePlatform.SafeAnchors(new Rect(0, 0, 0, 0), 2400, 1080, out min, out max);
            Assert.AreEqual(Vector2.zero, min); Assert.AreEqual(Vector2.one, max);
            MobilePlatform.SafeAnchors(new Rect(0, 0, 100, 100), 0, 0, out min, out max);
            Assert.AreEqual(Vector2.zero, min); Assert.AreEqual(Vector2.one, max);
        }

        Transform NewCanvas()
        {
            var go = new GameObject("TestCanvas", typeof(RectTransform));
            created.Add(go);
            return go.transform;
        }

        [Test]
        public void Settings_Touch_HidesDesktopRows_AndPacksRest_DesktopUnchanged()
        {
            MobilePlatform.ForceForTests(false);
            var desk = SettingsView.Create(NewCanvas()).transform;
            foreach (var n in new[] { "Scheme", "Resolution", "Fullscreen", "VSync" })
                Assert.IsTrue(desk.Find(n).gameObject.activeSelf, "데스크톱 " + n);
            float deskShakeY = ((RectTransform)desk.Find("Shake")).anchoredPosition.y;

            MobilePlatform.ForceForTests(true);
            var mob = SettingsView.Create(NewCanvas()).transform;
            foreach (var n in new[] { "Scheme", "Resolution", "Fullscreen", "VSync" })
                Assert.IsFalse(mob.Find(n).gameObject.activeSelf, "모바일은 숨김 " + n);
            foreach (var n in new[] { "MasterVolume", "TutorialReplay", "Shake", "Effects", "DamageNumbers", "CloseButton" })
                Assert.IsTrue(mob.Find(n).gameObject.activeSelf, "모바일 유지 " + n);
            Assert.AreEqual(deskShakeY + 252f, ((RectTransform)mob.Find("Shake")).anchoredPosition.y, 1e-3f, "빈 줄 없이 위로");
            Assert.AreEqual(((RectTransform)mob.Find("Resolution")).anchoredPosition.y,
                ((RectTransform)mob.Find("Shake")).anchoredPosition.y, 1e-3f, "오른쪽 열 첫 줄 자리");
        }

        [Test]
        public void CanvasMatch_NearSquareFold_MatchesWidth_DesktopAndWidePhoneUnchanged()
        {
            Assert.AreEqual(0f, AdaptiveCanvasScaler.MatchFor(1664, 1499), "폴드7 펼침: 폭 맞춤(캔버스 폭 1920)");
            Assert.AreEqual(0f, AdaptiveCanvasScaler.MatchFor(1440, 1080), "4:3도 폭 맞춤");
            Assert.AreEqual(0.5f, AdaptiveCanvasScaler.MatchFor(1920, 1080), "16:9 데스크톱 그대로");
            Assert.AreEqual(0.5f, AdaptiveCanvasScaler.MatchFor(1280, 720));
            Assert.AreEqual(1f, AdaptiveCanvasScaler.MatchFor(2400, 1080), "20:9 폰은 높이 맞춤: 연구 메뉴의 위·아래 겹침 방지");
        }

        [TestCase(1600f, 720f)]
        [TestCase(1664f, 1499f)]
        [TestCase(1280f, 720f)]
        public void StudyTree_AllControlsFitWithoutOverlap_AtSupportedScreenRatios(float width, float height)
        {
            MobilePlatform.ForceForTests(true);
            float match = AdaptiveCanvasScaler.MatchFor(width, height);
            float scale = Mathf.Pow(width / 1920f, 1f - match) * Mathf.Pow(height / 1080f, match);
            var parent = (RectTransform)NewCanvas();
            parent.sizeDelta = new Vector2(width / scale, height / scale);
            var view = SkillTreeView.Create(parent);
            view.Show(new SkillTree(SkillDatabase.LoadAll()), () => 0, _ => false, () => { });
            var root = (RectTransform)view.transform;
            var controls = new List<RectTransform>();
            foreach (RectTransform child in root)
                if (child.name == "Title" || child.name == "Points" || child.name == "CloseButton" || child.name.StartsWith("Header_") || child.name.StartsWith("Node_"))
                    controls.Add(child);
            Rect Bounds(RectTransform rt)
            {
                var corners = new Vector3[4]; rt.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
            Assert.AreEqual(21, controls.Count);
            var bounds = Bounds(root);
            for (int i = 0; i < controls.Count; i++)
            {
                var rect = Bounds(controls[i]);
                Assert.GreaterOrEqual(rect.yMin, bounds.yMin, controls[i].name);
                Assert.LessOrEqual(rect.yMax, bounds.yMax, controls[i].name);
                for (int j = i + 1; j < controls.Count; j++)
                    Assert.IsFalse(rect.Overlaps(Bounds(controls[j])), controls[i].name + " overlaps " + controls[j].name);
            }
        }

        [Test]
        public void CenterBand_TallCanvas_CentersContent_FillsScreen_NoChangeAt1080()
        {
            var parentGo = new GameObject("P", typeof(RectTransform)); created.Add(parentGo);
            var parent = (RectTransform)parentGo.transform;
            parent.sizeDelta = new Vector2(1920f, 1730f); // 1664×1499을 폭 1920으로 맞춘 캔버스
            var root = UiFactory.Stretch("Root", parent);
            var img = UiFactory.AddImage(root, new Color(0f, 0f, 0f, 0.72f));
            var band = CenterBand.Attach(root);
            Assert.AreEqual(650f, band.Extra, 1e-3f);
            Assert.AreEqual(1080f, root.rect.height, 1e-3f, "내용은 가운데 1080 띠");
            Assert.AreEqual(325f, root.offsetMin.y, 1e-3f);
            var fill = (RectTransform)root.Find("BandFill");
            Assert.IsTrue(fill.gameObject.activeSelf);
            Assert.AreEqual(1730f, fill.rect.height, 1e-3f, "배경은 화면 전체");
            Assert.AreEqual(0f, img.color.a, "띠 안 이중 어둡게 방지");

            var deskGo = new GameObject("D", typeof(RectTransform)); created.Add(deskGo);
            ((RectTransform)deskGo.transform).sizeDelta = new Vector2(1920f, 1080f);
            var deskRoot = UiFactory.Stretch("Root", deskGo.transform);
            var deskImg = UiFactory.AddImage(deskRoot, new Color(0f, 0f, 0f, 0.72f));
            CenterBand.Attach(deskRoot);
            Assert.AreEqual(Vector2.zero, deskRoot.offsetMin); Assert.AreEqual(Vector2.zero, deskRoot.offsetMax);
            Assert.IsFalse(deskRoot.Find("BandFill").gameObject.activeSelf, "16:9는 원래 그대로");
            Assert.AreEqual(0.72f, deskImg.color.a, 1e-6f);
        }

        [Test]
        public void Fold_TextAlignment_MobileCentered_DesktopOriginal_TutorialTeaWording()
        {
            MobilePlatform.ForceForTests(true);
            var card = CardSelectView.Create(NewCanvas()).transform;
            Assert.AreEqual(TextAnchor.MiddleCenter, card.Find("Card1/Desc").GetComponent<UnityEngine.UI.Text>().alignment, "모바일 카드 설명 가운데");
            var tut = FirstRunTutorialView.Create(NewCanvas());
            Assert.AreEqual(25f, ((RectTransform)tut.transform.Find("Panel/Body")).anchoredPosition.y, 1e-3f, "모바일 안내 본문 위로");
            tut.Show("시작", () => { }, Job.TeaSommelier);
            StringAssert.DoesNotContain("커피", tut.BodyText, "찻집 첫 안내는 우림 연구 문구");
            StringAssert.DoesNotContain("원두", tut.BodyText);
            tut.Show("시작", () => { }, Job.Bartender);
            StringAssert.Contains("커피", tut.BodyText, "바텐더는 원래 문구");

            MobilePlatform.ForceForTests(false);
            var deskCard = CardSelectView.Create(NewCanvas()).transform;
            Assert.AreEqual(TextAnchor.UpperCenter, deskCard.Find("Card1/Desc").GetComponent<UnityEngine.UI.Text>().alignment, "데스크톱 그대로");
            var deskTut = FirstRunTutorialView.Create(NewCanvas());
            Assert.AreEqual(-20f, ((RectTransform)deskTut.transform.Find("Panel/Body")).anchoredPosition.y, 1e-3f);
        }

        [Test]
        public void MobileDisplayAdapter_NeverResizes_And_Runs60()
        {
            int prevFps = Application.targetFrameRate, prevVsync = QualitySettings.vSyncCount;
            try
            {
                var a = new MobileDisplayAdapter();
                a.Apply(1280, 720, true); // 예외 없이 아무것도 하지 않는다
                Assert.IsFalse(a.CurrentWindowed);
                a.ApplyVSync(false);
                Assert.AreEqual(60, Application.targetFrameRate, "안드로이드 기본 30fps 대신 60");
                a.ApplyVSync(true);
                Assert.AreEqual(60, Application.targetFrameRate);
            }
            finally { Application.targetFrameRate = prevFps; QualitySettings.vSyncCount = prevVsync; }
        }

        [Test]
        public void Tutorial_TouchText_NoKeyboardInstructions()
        {
            MobilePlatform.ForceForTests(true);
            var t = FirstRunTutorialView.BodyFor(MovementScheme.Keyboard);
            StringAssert.Contains("끌어 이동", t);
            StringAssert.DoesNotContain("WASD", t);
            StringAssert.DoesNotContain("Esc", t);
            MobilePlatform.ForceForTests(false);
            StringAssert.Contains("WASD", FirstRunTutorialView.BodyFor(MovementScheme.Keyboard), "데스크톱 그대로");
        }

        [Test]
        public void AndroidPlayerSettings_Il2cppArm64_Api36_Landscape_PackageId()
        {
            var android = NamedBuildTarget.Android;
            var saved = (id: PlayerSettings.GetApplicationIdentifier(android), backend: PlayerSettings.GetScriptingBackend(android),
                arch: PlayerSettings.Android.targetArchitectures, min: PlayerSettings.Android.minSdkVersion,
                target: PlayerSettings.Android.targetSdkVersion, code: PlayerSettings.Android.bundleVersionCode,
                orient: PlayerSettings.defaultInterfaceOrientation, p: PlayerSettings.allowedAutorotateToPortrait,
                pu: PlayerSettings.allowedAutorotateToPortraitUpsideDown, ll: PlayerSettings.allowedAutorotateToLandscapeLeft,
                lr: PlayerSettings.allowedAutorotateToLandscapeRight, ver: PlayerSettings.bundleVersion,
                desk: PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone));
            try
            {
                CCT.Editor.BuildRelease.ApplyAndroidPlayerSettings();
                Assert.AreEqual("com.hyungjinyu.cocktailcoffeetea", PlayerSettings.GetApplicationIdentifier(android));
                Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(android));
                Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);
                Assert.AreEqual(36, (int)PlayerSettings.Android.targetSdkVersion);
                Assert.GreaterOrEqual((int)PlayerSettings.Android.minSdkVersion, 24);
                Assert.IsFalse(PlayerSettings.Android.useCustomKeystore, "서명 키는 나중에 — 디버그 서명 산출물");
                Assert.AreEqual(UIOrientation.AutoRotation, PlayerSettings.defaultInterfaceOrientation);
                Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft && PlayerSettings.allowedAutorotateToLandscapeRight);
                Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown);
                Assert.AreEqual(saved.desk, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone), "데스크톱 백엔드 불변");
                Assert.Greater(CCT.Editor.BuildRelease.ApplyAndroidIcon(), 0, "Android 런처 아이콘(Legacy/Round)에 앱 아이콘 지정");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(android, saved.id);
                PlayerSettings.SetScriptingBackend(android, saved.backend);
                PlayerSettings.Android.targetArchitectures = saved.arch;
                PlayerSettings.Android.minSdkVersion = saved.min;
                PlayerSettings.Android.targetSdkVersion = saved.target;
                PlayerSettings.Android.bundleVersionCode = saved.code;
                PlayerSettings.defaultInterfaceOrientation = saved.orient;
                PlayerSettings.allowedAutorotateToPortrait = saved.p;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = saved.pu;
                PlayerSettings.allowedAutorotateToLandscapeLeft = saved.ll;
                PlayerSettings.allowedAutorotateToLandscapeRight = saved.lr;
                PlayerSettings.bundleVersion = saved.ver;
            }
        }
    }
}
