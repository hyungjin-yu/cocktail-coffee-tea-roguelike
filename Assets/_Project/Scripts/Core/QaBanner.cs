using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>검수 드라이버 창 표시(명시 QA 인자 실행에서만 생성): 화면 위에 "QA 자동 시험 중 · 사람 플레이 아님"과 현재 단계를 띄워
    /// 사용자가 시험 창을 보고 정상 플레이로 오해하지 않게 한다. 캡처에도 남는다(검수 증거와 정상 화면 구분).</summary>
    public static class QaBanner
    {
        static Text text;
        static string head;

        public static void Show(string message)
        {
            head = message;
            if (text == null)
            {
                var canvas = UiFactory.CreateCanvas(null, "QaBanner", 32000);
                Object.DontDestroyOnLoad(canvas.gameObject);
                Object.Destroy(canvas.GetComponent<GraphicRaycaster>());
                var rt = UiFactory.Place("Banner", canvas.transform, new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(1100f, 34f));
                var bg = UiFactory.AddImage(rt, new Color(0.6f, 0.05f, 0.05f, 0.85f));
                bg.raycastTarget = false;
                text = UiFactory.AddText(UiFactory.Stretch("Text", rt), "", 20, TextAnchor.MiddleLeft, Color.white);
                text.raycastTarget = false;
            }
            text.text = "  " + head;
        }

        public static void Step(string step) { if (text != null) text.text = $"  {head} · 단계: {step}"; }
    }
}
