using System;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 런 시작 전 직업 선택 화면 (기획서 5장). 지금은 직업당 무기가 1개뿐이라(레이피어/검)
    /// 별도의 무기 선택 단계 없이 직업 선택에 합쳐뒀다 — 총/활이 추가되면 2단계로 나눌 것.
    /// GameManager 가 아직 없는 시점에 뜨는 화면이라 스스로 최상위 Canvas를 만든다.
    /// </summary>
    public class CharacterSelectView : MonoBehaviour
    {
        public Button BartenderButton { get; private set; }
        public Button TeaSommelierButton { get; private set; }

        public static CharacterSelectView Show(Action<Job> onChosen)
        {
            var canvas = UiFactory.CreateCanvas(null, "CharacterSelect", 100);
            var view = canvas.gameObject.AddComponent<CharacterSelectView>();

            var root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));

            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 90f));
            UiFactory.AddText(title, "직업을 선택하세요", 60, TextAnchor.MiddleCenter, Color.white);

            view.BartenderButton = BuildOption(root, -1, JobPreset.Bartender, canvas.gameObject, onChosen);
            view.TeaSommelierButton = BuildOption(root, 1, JobPreset.TeaSommelier, canvas.gameObject, onChosen);
            return view;
        }

        static Button BuildOption(Transform root, float side, JobPreset preset, GameObject canvasGo,
            Action<Job> onChosen)
        {
            var rt = UiFactory.Place($"Option_{preset.job}", root, new Vector2(0.5f, 0.5f),
                new Vector2(side * 300f, 0f), new Vector2(520f, 640f));
            var bg = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Destroy(canvasGo);
                onChosen(preset.job);
            });

            var swatchRt = UiFactory.Place("Swatch", rt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(90f, 90f));
            UiFactory.AddImage(swatchRt, preset.playerColor);

            UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -170f),
                new Vector2(460f, 70f)), preset.name, 46, TextAnchor.MiddleCenter, Color.white);
            UiFactory.AddText(UiFactory.Place("Weapon", rt, new Vector2(0.5f, 1f), new Vector2(0f, -240f),
                new Vector2(460f, 50f)), $"시작 무기: {preset.weaponName}", 30, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.5f));
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f),
                new Vector2(440f, 260f)), preset.description, 30, TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));

            return button;
        }
    }
}
