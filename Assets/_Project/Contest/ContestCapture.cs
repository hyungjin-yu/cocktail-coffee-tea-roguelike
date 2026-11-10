#if PPS_CAPTURE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT.Contest
{
    /// <summary>캡처 빌드(PPS_CAPTURE) 전용. 배포 빌드에는 컴파일되지 않는다.
    /// 게임 내부 메서드 호출로만 진행하며 OS 전역 키보드·마우스 입력을 만들지 않는다.
    /// 실행 인자:
    ///  -pps-data-dir &lt;폴더&gt;  (필수, 격리 저장)
    ///  -pps-shots &lt;폴더&gt;     단계별 스크린샷 + 글자 잘림 감사(layout-audit.txt)
    ///  -pps-video &lt;폴더&gt;     30fps 고정 프레임 PNG 시퀀스(약 60초)
    ///  -pps-size W H          오프스크린 렌더 해상도(기본 1920 1080)</summary>
    public sealed class ContestCapture : MonoBehaviour
    {
        string output;
        bool video;
        int width = 1920, height = 1080;
        RenderTexture rt;
        Texture2D readback;
        Camera cam;
        int frame;
        bool recording;
        readonly StringBuilder audit = new StringBuilder();
        readonly List<string> actions = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-pps-data-dir") < 0) return;
            int s = Array.IndexOf(args, "-pps-shots"), v = Array.IndexOf(args, "-pps-video");
            if (s < 0 && v < 0) return;
            Application.runInBackground = true;
            var go = new GameObject("PpsCapture");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<ContestCapture>();
            c.video = v >= 0;
            c.output = args[(c.video ? v : s) + 1];
            int z = Array.IndexOf(args, "-pps-size");
            if (z >= 0 && z + 2 < args.Length) { c.width = int.Parse(args[z + 1]); c.height = int.Parse(args[z + 2]); }
            Directory.CreateDirectory(c.output);
            c.StartCoroutine(c.Run());
        }

        IEnumerator Run()
        {
            yield return null;
            var app = ContestApp.Instance;
            while (app == null) { yield return null; app = ContestApp.Instance; }
            SetupOffscreen(app);
            audit.AppendLine("size " + width + "x" + height + " data=" + app.Save.Path + " load=" + app.Save.LoadNote);
            if (video)
            {
                var c = UiFactory.CreateCanvas(null, "PpsCaptureNote", 900);
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 0.5f;
                Kit.Label(c.transform, "실제 플레이 화면 녹화 · 캡처 빌드 내부 자동 입력", 1380, 8, 530, 30, 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.UpperRight, "CaptureNote");
            }
            if (video) { Time.captureFramerate = 30; recording = true; yield return Demo(app); recording = false; }
            else yield return Shots(app);
            File.WriteAllText(Path.Combine(output, video ? "video-actions.txt" : "layout-audit.txt"), audit + "\n" + string.Join("\n", actions));
            File.WriteAllText(Path.Combine(output, "DONE.txt"), "frames=" + frame + "\n");
            yield return null;
            Application.Quit(0);
        }

        void SetupOffscreen(ContestApp app)
        {
            cam = Camera.main;
            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            cam.nearClipPlane = 0.1f;
        }

        void LateUpdate()
        {
            if (recording) StartCoroutine(CaptureFrame("frame_" + (frame++).ToString("00000")));
        }

        IEnumerator CaptureFrame(string name)
        {
            yield return new WaitForEndOfFrame();
            Grab(name);
        }

        void Grab(string name)
        {
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(output, name + ".png"), readback.EncodeToPNG());
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.6f); // 행 표시 연출이 끝난 뒤
            yield return new WaitForEndOfFrame();
            Grab(name);
            AuditText(name);
        }

        void AuditText(string shot)
        {
            int truncated = 0, minSize = int.MaxValue;
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            foreach (var t in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) continue;
                var r = t.rectTransform.rect;
                var settings = t.GetGenerationSettings(r.size);
                float need = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;

                minSize = Mathf.Min(minSize, Mathf.RoundToInt(t.fontSize * scale));
                if (!t.resizeTextForBestFit && need > r.height + 1f)
                {
                    truncated++;
                    audit.AppendLine(shot + " TRUNCATED " + HierPath(t.transform) + " need=" + need.ToString("0") + " have=" + r.height.ToString("0"));
                }
            }
            audit.AppendLine(shot + " texts-ok truncated=" + truncated + " minScaledFontPx=" + minSize);
        }

        static string HierPath(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        // 시연: 대상 버튼을 잠깐 선택(강조) 표시한 뒤 같은 게임 메서드를 호출한다.
        IEnumerator Press(string button, Action act, float before = 0.45f, float after = 0.5f)
        {
            var b = FindButton(button);
            if (b != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(b.gameObject);
            yield return new WaitForSeconds(before);
            actions.Add(Time.time.ToString("0.00") + "s " + button);
            act();
            yield return new WaitForSeconds(after);
        }

        static Button FindButton(string name) =>
            FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);

        IEnumerator Pour(ContestApp app)
        {
            yield return new WaitForSeconds(0.3f);
            var b = FindButton("PourButton");
            if (b != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(b.gameObject);
            actions.Add(Time.time.ToString("0.00") + "s hold PourButton");
            app.VirtualHold = true;
            int brewed = app.Session.Tray.Count;
            float t = 0f;
            while (app.Session.Tray.Count == brewed && t < 3f) { t += Time.deltaTime; yield return null; }
            app.VirtualHold = false;
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>약 60초 시연: 요청 확인 → 잘못된 공급안 반려 → 재선택 → 입고(결함 포장 확인 누락) → 제조 →
        /// 검수 불합격 → 불일치 제외·재제조 → 재검수 합격 → 납품 → 결과.</summary>
        IEnumerator Demo(ContestApp app)
        {
            yield return new WaitForSeconds(3.5f);
            yield return Press("StartButton", app.ShowSelect, 0.6f, 2.2f);
            yield return Press("StartMission0", () => app.StartMission(0), 0.5f, 1.4f);
            for (int i = 0; i < app.Session.Conditions.Count; i++)
            {
                int idx = i;
                yield return Press("CheckCond" + i, () => app.CheckCondition(idx), 0.3f, 0.25f);
            }
            yield return Press("FinishRequest", app.FinishRequest, 0.5f, 1.2f);
            yield return Press("Offer1", () => app.SelectOffer(1), 0.4f, 1.0f);   // 가장 저렴하지만 품명 불일치
            yield return Press("PlaceOrder", app.PlaceOrder, 0.5f, 3.6f);
            yield return Press("Offer0", () => app.SelectOffer(0), 0.4f, 1.2f);
            yield return Press("PlaceOrder", app.PlaceOrder, 0.5f, 1.4f);
            yield return Press("Accept", app.AcceptPack, 0.6f, 0.9f);
            yield return new WaitForSeconds(0.8f);
            yield return Press("Accept", app.AcceptPack, 0.6f, 0.9f);           // 파손 포장을 확인 없이 입고(실수)
            yield return Press("Accept", app.AcceptPack, 0.6f, 1.0f);
            yield return Press("Temp1", () => app.SetTemp(Temp.T80), 0.4f, 0.4f);
            yield return Press("Vessel0", () => app.SetVessel(Vessel.TeaCup), 0.4f, 0.5f);
            yield return Pour(app);
            yield return Press("Inspect", app.RequestInspection, 0.5f, 4.6f);   // 불합격: 재료 이력 불일치 + 수량 부족
            yield return Press("Exclude", app.ExcludeNonconforming, 0.5f, 1.4f);
            yield return Press("CupsMinus", () => app.ChangeCups(-1), 0.3f, 0.2f);
            yield return Press("CupsMinus", () => app.ChangeCups(-1), 0.3f, 0.4f);
            yield return Pour(app);
            yield return Press("Inspect", app.RequestInspection, 0.5f, 3.2f);   // 합격
            yield return Press("Deliver", app.Deliver, 0.5f, 6.5f);
        }

        IEnumerator Shots(ContestApp app)
        {
            yield return Shot("01_title");
            app.ToggleHelp(); yield return Shot("02_help"); app.ToggleHelp();
            app.ShowSelect(); yield return Shot("03_select");
            app.StartMission(0); yield return Shot("04_request_unchecked");
            for (int i = 0; i < app.Session.Conditions.Count; i++) app.CheckCondition(i);
            yield return Shot("05_request_checked");
            app.FinishRequest(); app.SelectOffer(1); app.PlaceOrder(); yield return Shot("06_supply_rejected");
            app.SelectOffer(0); yield return Shot("07_supply_valid");
            app.PlaceOrder(); yield return Shot("08_receive");
            app.AcceptPack(); yield return Shot("09_receive_damaged");
            app.AcceptPack(); app.AcceptPack();
            yield return Shot("10_brew_unset");
            app.SetTemp(Temp.T80); app.SetVessel(Vessel.TeaCup); yield return Shot("11_brew_ready");
            app.BrewNow(); yield return Shot("12_brew_done");
            app.RequestInspection(); yield return Shot("13_inspect_fail");
            app.ExcludeNonconforming(); app.ChangeCups(-2); app.BrewNow(); app.RequestInspection(); yield return Shot("14_inspect_pass");
            app.Deliver(); yield return Shot("15_result");
            app.ShowSelect(); yield return Shot("16_select_after");
            // 실패 화면: 임무 1 재실행에서 납기 초과까지 정상 포장 반품 반복
            app.StartMission(0);
            for (int i = 0; i < app.Session.Conditions.Count; i++) app.CheckCondition(i);
            app.FinishRequest(); app.SelectOffer(0); app.PlaceOrder();
            for (int i = 0; i < 40 && app.Session.Stage == Stage.Receive; i++) app.RejectPack();
            yield return Shot("17_failed_or_late");
        }
    }
}
#endif
