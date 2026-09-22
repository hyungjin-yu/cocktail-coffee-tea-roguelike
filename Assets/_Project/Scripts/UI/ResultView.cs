using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>런 종료 화면. R 키 또는 버튼으로 재시작.</summary>
    public class ResultView : MonoBehaviour
    {
        GameManager game;
        GameObject root;
        Text title;
        Text stats;

        public bool IsOpen => root != null && root.activeSelf;

        public static ResultView Create(Transform canvas, GameManager game)
        {
            var rootRt = UiFactory.Stretch("Result", canvas);
            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.8f));

            var view = rootRt.gameObject.AddComponent<ResultView>();
            view.game = game;
            view.root = rootRt.gameObject;

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 240f),
                new Vector2(1000f, 120f));
            view.title = UiFactory.AddText(title, "런 종료", 96, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.6f));

            var statsRt = UiFactory.Place("Stats", rootRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                new Vector2(800f, 240f));
            view.stats = UiFactory.AddText(statsRt, "", 44, TextAnchor.MiddleCenter, Color.white);

            var buttonRt = UiFactory.Place("RestartButton", rootRt, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -190f), new Vector2(460f, 100f));
            var bg = UiFactory.AddImage(buttonRt, new Color(0.9f, 0.35f, 0.75f));
            var button = buttonRt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(view.Restart);
            var labelRt = UiFactory.Stretch("Label", buttonRt);
            UiFactory.AddText(labelRt, "다시 시작  [R]", 40, TextAnchor.MiddleCenter, Color.white);

            view.root.SetActive(false);
            return view;
        }

        public void Show(bool cleared, float runTime, int kills, int level, int skillsUnlocked)
        {
            title.text = cleared ? "런 클리어!" : "런 종료";
            title.color = cleared ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.5f, 0.6f);

            int seconds = Mathf.FloorToInt(runTime);
            stats.text = $"{(cleared ? "클리어 시간" : "생존 시간")}  {seconds / 60:00}:{seconds % 60:00}\n" +
                         $"처치 수  {kills}\n도달 레벨  {level}\n해금한 커피 스킬  {skillsUnlocked}";
            root.SetActive(true);
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        void Restart()
        {
            if (!IsOpen) return;
            game.Restart();
        }
    }
}
