using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 08:178 종료 시 미저장 확인 — 타이틀 "종료" 버튼뿐 아니라 OS 창 닫기(Application.wantsToQuit)도 가로챈다.
    /// 종료 요청이 오면 먼저 저장을 시도하고(진행 중 런은 포기로 1회 집계 + 진행/설정 저장), 그래도 진행 save나 설정
    /// 저장이 실패 상태면 종료를 취소하고 "저장 재시도 / 저장 없이 종료 / 취소" 창을 띄운다(기본 포커스 = 취소).
    /// 두 저장 서비스의 오류 상태는 섞지 않고 문구에서 각각 알린다. 강제 OS 종료까지 보존한다고 주장하지 않는다(08:179).
    /// </summary>
    public class QuitGuard : MonoBehaviour
    {
        static QuitGuard instance;
        static bool allowQuit;

        GameObject root;
        Text message;
        Button cancelButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            allowQuit = false;
        }

        /// <summary>진행 save 또는 설정 저장 중 하나라도 실패해 디스크에 못 쓴 변경이 남았는지.</summary>
        public static bool HasUnsaved =>
            PermanentProgress.LastSaveFailed || SettingsService.LastSaveFailed || SettingsService.IsDirty ||
            RunHistory.IsDirty; // 67 최근 기록 저장 실패도 성공처럼 조용히 넘기지 않는다(미래 버전 보존은 실패가 아님)

        public static string UnsavedMessage
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (PermanentProgress.LastSaveFailed) parts.Add("진행(save.json)");
                if (SettingsService.LastSaveFailed || SettingsService.IsDirty) parts.Add("설정(settings.json)");
                if (RunHistory.IsDirty) parts.Add("최근 기록(run-history.json)");
                return $"저장하지 못한 {string.Join("·", parts)} 변경이 있습니다.\n다시 시도하거나, 저장 없이 종료할 수 있습니다.";
            }
        }

        public static void Install()
        {
            if (instance != null || !Application.isPlaying) return;
            var go = new GameObject("QuitGuard");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<QuitGuard>();
            Application.wantsToQuit += OnWantsToQuit;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            Application.wantsToQuit -= OnWantsToQuit;
            instance = null;
        }

        /// <summary>사용자가 확인창에서 "저장 없이 종료"를 고른 뒤 등, 다음 종료 요청은 그대로 통과시킨다.</summary>
        public static void AllowNextQuit() => allowQuit = true;

        /// <summary>종료 전에 할 수 있는 저장을 모두 시도한다. 남은 미저장이 없으면 true.</summary>
        public static bool TrySaveEverything()
        {
            GameManager.Instance?.PrepareForQuit();
            PermanentProgress.SaveIfDirty();
            if (SettingsService.IsDirty || SettingsService.LastSaveFailed) SettingsService.RetrySave();
            if (RunHistory.IsDirty) RunHistory.SaveNow();
            return !HasUnsaved;
        }

        static bool OnWantsToQuit()
        {
            if (allowQuit) return true;
            if (TrySaveEverything()) return true;
            instance?.ShowPrompt();
            return false;
        }

        void ShowPrompt()
        {
            if (root == null) Build();
            message.text = UnsavedMessage;
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        }

        void Build()
        {
            var canvas = UiFactory.CreateCanvas(transform, "QuitGuardCanvas", 1000);
            var dim = UiFactory.Stretch("Dim", canvas.transform);
            UiFactory.AddImage(dim, new Color(0f, 0f, 0f, 0.6f));
            root = dim.gameObject;
            var panel = UiFactory.Place("Panel", dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 320f));
            UiFactory.SkinPanel(UiFactory.AddImage(panel, new Color(0.1f, 0.08f, 0.1f, 0.98f)));
            message = UiFactory.AddText(UiFactory.Place("Message", panel, new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                new Vector2(780f, 140f)), "", 28, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.85f));
            MakeButton(panel, "Retry", new Vector2(-270f, 40f), "저장 재시도", () =>
            {
                if (TrySaveEverything())
                {
                    AllowNextQuit();
                    Application.Quit();
                }
                else message.text = "다시 저장하지 못했습니다.\n" + UnsavedMessage;
            });
            MakeButton(panel, "QuitAnyway", new Vector2(0f, 40f), "저장 없이 종료", () =>
            {
                AllowNextQuit();
                Application.Quit();
            });
            cancelButton = MakeButton(panel, "Cancel", new Vector2(270f, 40f), "취소", () => root.SetActive(false));
            root.SetActive(false);
        }

        static Button MakeButton(RectTransform parent, string name, Vector2 pos, string label, System.Action onClick)
        {
            var rt = UiFactory.Place(name, parent, new Vector2(0.5f, 0f), pos, new Vector2(250f, 78f));
            var bg = UiFactory.AddImage(rt, new Color(0.3f, 0.32f, 0.36f));
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => onClick());
            UiFactory.AddText(UiFactory.Stretch("Label", rt), label, 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }
    }
}
