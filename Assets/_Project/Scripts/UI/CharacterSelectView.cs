using System;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 런 시작 전 직업 → 무기 2단계 선택 화면 (기획서 5장).
    /// GameManager 가 아직 없는 시점에 뜨는 화면이라 스스로 최상위 Canvas를 만든다.
    /// </summary>
    public class CharacterSelectView : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root;
        Action<Job, WeaponKind> onChosen;
        PermanentSkillView shopView;

        public Button BartenderButton { get; private set; }
        public Button TeaSommelierButton { get; private set; }
        /// <summary>2단계(무기 선택) 진입 후에만 채워진다. weapons[0]/[1] 순서는 JobPreset.weapons 와 같다.</summary>
        public Button WeaponButtonA { get; private set; }
        public Button WeaponButtonB { get; private set; }
        /// <summary>직업 선택 화면에서 여는 영구 강화 버튼 (테스트용으로도 참조).</summary>
        public Button PermanentShopButton { get; private set; }
        /// <summary>영구 강화 화면 (테스트용으로도 참조).</summary>
        public PermanentSkillView ShopView => shopView;

        public static CharacterSelectView Show(Action<Job, WeaponKind> onChosen)
        {
            var canvas = UiFactory.CreateCanvas(null, "CharacterSelect", 100);
            var view = canvas.gameObject.AddComponent<CharacterSelectView>();
            view.canvas = canvas;
            view.onChosen = onChosen;
            view.shopView = PermanentSkillView.Create(canvas.transform, view.ShowJobStep);
            view.ShowJobStep();
            return view;
        }

        void ClearRoot()
        {
            if (root != null) Destroy(root.gameObject);
            WeaponButtonA = null;
            WeaponButtonB = null;
        }

        void ShowJobStep()
        {
            ClearRoot();
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));

            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 90f));
            UiFactory.AddText(title, "직업을 선택하세요", 60, TextAnchor.MiddleCenter, Color.white);

            BartenderButton = BuildJobOption(-1, JobPreset.Bartender);
            TeaSommelierButton = BuildJobOption(1, JobPreset.TeaSommelier);
            PermanentShopButton = BuildPermanentShopButton();
        }

        Button BuildPermanentShopButton()
        {
            int specialty = PermanentProgress.Current.specialtyPoints;
            var rt = UiFactory.Place("PermanentShopButton", root, new Vector2(1f, 1f), new Vector2(-210f, -50f),
                new Vector2(380f, 80f));
            var bg = UiFactory.AddImage(rt, new Color(0.4f, 0.24f, 0.5f));
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                shopView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), $"영구 강화 (스페셜티 {specialty})", 26,
                TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        void ShowWeaponStep(JobPreset job)
        {
            ClearRoot();
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));

            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 90f));
            UiFactory.AddText(title, $"{job.name} — 무기를 선택하세요", 56, TextAnchor.MiddleCenter, Color.white);

            WeaponButtonA = BuildWeaponOption(-1, job, job.weapons[0]);
            WeaponButtonB = BuildWeaponOption(1, job, job.weapons[1]);
        }

        Button BuildJobOption(float side, JobPreset preset)
        {
            var rt = UiFactory.Place($"Option_{preset.job}", root, new Vector2(0.5f, 1f),
                new Vector2(side * 300f, -220f), new Vector2(520f, 640f));
            var bg = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));

            var button = MakeButton(rt, bg, () => ShowWeaponStep(preset));

            var swatchRt = UiFactory.Place("Swatch", rt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(90f, 90f));
            UiFactory.AddImage(swatchRt, preset.playerColor);

            UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -170f),
                new Vector2(460f, 70f)), preset.name, 46, TextAnchor.MiddleCenter, Color.white);
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -260f),
                new Vector2(440f, 260f)), preset.description, 30, TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));

            return button;
        }

        Button BuildWeaponOption(float side, JobPreset job, WeaponPreset weapon)
        {
            var rt = UiFactory.Place($"Weapon_{weapon.kind}", root, new Vector2(0.5f, 1f),
                new Vector2(side * 300f, -220f), new Vector2(520f, 500f));
            var bg = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));

            var button = MakeButton(rt, bg, () =>
            {
                var chosen = onChosen;
                Destroy(canvas.gameObject);
                chosen(job.job, weapon.kind);
            });

            UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(460f, 70f)), weapon.name, 46, TextAnchor.MiddleCenter, Color.white);
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                new Vector2(440f, 260f)), weapon.description, 30, TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));

            return button;
        }

        static Button MakeButton(RectTransform rt, Image bg, Action onClick)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
