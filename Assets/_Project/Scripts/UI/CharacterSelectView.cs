using System;
using UnityEngine;
using UnityEngine.EventSystems;
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
        CardCodexView codexView;
        SettingsView settingsView;

        public Button BartenderButton { get; private set; }
        public Button TeaSommelierButton { get; private set; }
        /// <summary>2단계(무기 선택) 진입 후에만 채워진다. weapons[0]/[1] 순서는 JobPreset.weapons 와 같다.</summary>
        public Button WeaponButtonA { get; private set; }
        public Button WeaponButtonB { get; private set; }
        /// <summary>08:67 무기 선택 후 확정하는 "영업 시작" 버튼 / 직업 선택으로 돌아가는 "뒤로" 버튼(2단계에서만).</summary>
        public Button StartButton { get; private set; }
        public Button WeaponBackButton { get; private set; }
        /// <summary>2단계에서 현재 고른 무기(무기 카드를 누르면 바뀌고, 영업 시작으로 확정).</summary>
        public WeaponKind SelectedWeapon { get; private set; }

        // 08:67 이전 선택 기억(이번 실행 동안). 시작 기본값은 바텐더/레이피어.
        static Job lastJob = Job.Bartender;
        static WeaponKind lastWeapon = WeaponKind.Rapier;
        JobPreset currentJob;
        Image weaponBgA, weaponBgB;
        static readonly Color WeaponCardColor = new Color(0.16f, 0.13f, 0.2f, 1f);
        static readonly Color WeaponCardSelectedColor = new Color(0.36f, 0.26f, 0.16f, 1f);
        /// <summary>직업 선택 화면에서 여는 영구 강화 버튼 (테스트용으로도 참조).</summary>
        public Button PermanentShopButton { get; private set; }
        /// <summary>영구 강화 화면 (테스트용으로도 참조).</summary>
        public PermanentSkillView ShopView => shopView;
        /// <summary>직업 선택 화면에서 여는 카드 도감 버튼 (테스트용으로도 참조).</summary>
        public Button CardCodexButton { get; private set; }
        /// <summary>카드 도감 화면 (테스트용으로도 참조).</summary>
        public CardCodexView CodexView => codexView;
        /// <summary>직업 선택 화면에서 여는 설정 버튼 (테스트용으로도 참조).</summary>
        public Button SettingsButton { get; private set; }
        /// <summary>설정 화면 (테스트용으로도 참조).</summary>
        public SettingsView SettingsView => settingsView;

        public static CharacterSelectView Show(Action<Job, WeaponKind> onChosen)
        {
            var canvas = UiFactory.CreateCanvas(null, "CharacterSelect", 100);
            var view = canvas.gameObject.AddComponent<CharacterSelectView>();
            view.canvas = canvas;
            view.onChosen = onChosen;
            view.shopView = PermanentSkillView.Create(canvas.transform, view.ShowJobStep);
            view.codexView = CardCodexView.Create(canvas.transform, view.ShowJobStep);
            view.settingsView = SettingsView.Create(canvas.transform, () => view.root.gameObject.SetActive(true));
            view.ShowJobStep();
            return view;
        }

        void ClearRoot()
        {
            if (root != null) Destroy(root.gameObject);
            WeaponButtonA = null;
            WeaponButtonB = null;
            StartButton = null;
            WeaponBackButton = null;
        }

        void ShowJobStep()
        {
            ClearRoot();
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(root);

            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 90f));
            UiFactory.AddText(title, "직업을 선택하세요", 60, TextAnchor.MiddleCenter, Color.white);

            BartenderButton = BuildJobOption(-1, JobPreset.Bartender);
            TeaSommelierButton = BuildJobOption(1, JobPreset.TeaSommelier);
            PermanentShopButton = BuildPermanentShopButton();
            CardCodexButton = BuildCardCodexButton();
            SettingsButton = BuildSettingsButton();
            BuildSaveIssueBanner();
            // 2026-10-12: 진입 즉시 선택하면 바텐더 카드가 파란 강조로 고정돼 보여 첫 키보드 입력 때 드러낸다.
            var focus = lastJob == Job.TeaSommelier ? TeaSommelierButton : BartenderButton;
            UiInitialFocus.Offer(focus.gameObject);
        }

        /// <summary>G29 — 저장 파일이 손상돼 복구했거나 마지막 저장이 실패했으면 조용히 넘어가지 않고
        /// 알린다. 문제 없으면 아무것도 안 만든다.</summary>
        void BuildSaveIssueBanner()
        {
            string issue = PermanentProgress.LastLoadIssue;
            if (issue == null && !PermanentProgress.LastSaveFailed) return;

            string message = issue ?? "마지막 저장에 실패했습니다 — 다음 저장 때 다시 시도합니다.";
            var rt = UiFactory.Place("SaveIssueBanner", root, new Vector2(0.5f, 0f), new Vector2(0f, 40f),
                new Vector2(900f, 50f));
            UiFactory.AddImage(rt, new Color(0.4f, 0.12f, 0.1f, 0.9f));
            UiFactory.AddText(UiFactory.Stretch("Label", rt), $"⚠ {message}", 22, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.7f));
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

        Button BuildSettingsButton()
        {
            var rt = UiFactory.Place("SettingsButton", root, new Vector2(0.5f, 0f), new Vector2(0f, 130f),
                new Vector2(300f, 70f));
            var bg = UiFactory.AddImage(rt, new Color(0.28f, 0.26f, 0.3f));
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                settingsView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), "설정", 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        Button BuildCardCodexButton()
        {
            var rt = UiFactory.Place("CardCodexButton", root, new Vector2(0f, 1f), new Vector2(210f, -50f),
                new Vector2(380f, 80f));
            var bg = UiFactory.AddImage(rt, new Color(0.24f, 0.4f, 0.5f));
            var button = MakeButton(rt, bg, () =>
            {
                root.gameObject.SetActive(false);
                codexView.Show();
            });
            UiFactory.AddText(UiFactory.Stretch("Label", rt), "카드 도감", 26, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        void ShowWeaponStep(JobPreset job)
        {
            ClearRoot();
            root = UiFactory.Stretch("Root", canvas.transform);
            UiFactory.AddImage(root, new Color(0.06f, 0.05f, 0.08f, 1f));
            UiFactory.AddCafeBackdrop(root);

            var title = UiFactory.Place("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                new Vector2(1200f, 90f));
            UiFactory.AddText(title, $"{job.name} — 무기를 선택하세요", 56, TextAnchor.MiddleCenter, Color.white);

            currentJob = job;
            WeaponButtonA = BuildWeaponOption(-1, job.weapons[0], out weaponBgA);
            WeaponButtonB = BuildWeaponOption(1, job.weapons[1], out weaponBgB);

            var startRt = UiFactory.Place("StartButton", root, new Vector2(0.5f, 0f), new Vector2(0f, 130f),
                new Vector2(360f, 84f));
            StartButton = MakeButton(startRt, UiFactory.AddImage(startRt, new Color(0.55f, 0.32f, 0.14f)), ConfirmStart);
            UiFactory.AddText(UiFactory.Stretch("Label", startRt), "영업 시작", 34, TextAnchor.MiddleCenter, Color.white);

            var backRt = UiFactory.Place("BackButton", root, new Vector2(0f, 0f), new Vector2(170f, 130f),
                new Vector2(240f, 70f));
            WeaponBackButton = MakeButton(backRt, UiFactory.AddImage(backRt, new Color(0.28f, 0.26f, 0.3f)), ShowJobStep);
            UiFactory.AddText(UiFactory.Stretch("Label", backRt), "뒤로", 26, TextAnchor.MiddleCenter, Color.white);

            // 같은 직업을 다시 고르면 지난 무기, 아니면 첫 무기(바텐더=레이피어)를 기본 선택.
            // 2026-10-12: 기본값은 논리 상태로만 두고 "선택됨" 테두리는 사용자가 무기를 직접 고른 뒤에만 그린다.
            SelectWeapon(job.weapons[1].kind == lastWeapon ? job.weapons[1] : job.weapons[0], false);
            var focus = SelectedWeapon == job.weapons[1].kind ? WeaponButtonB : WeaponButtonA;
            UiInitialFocus.Offer(focus.gameObject);
        }

        void SelectWeapon(WeaponPreset weapon, bool showChoice)
        {
            SelectedWeapon = weapon.kind;
            bool a = currentJob.weapons[0].kind == weapon.kind;
            UiFactory.SetChoice(weaponBgA, showChoice && a, showChoice && a ? WeaponCardSelectedColor : WeaponCardColor);
            UiFactory.SetChoice(weaponBgB, showChoice && !a, showChoice && !a ? WeaponCardSelectedColor : WeaponCardColor);
        }

        /// <summary>테스트용 — 무기 카드에 "선택됨" 장식이 켜져 있는지.</summary>
        public bool WeaponChoiceShownForTests(bool first)
        {
            var bg = first ? weaponBgA : weaponBgB;
            var ring = bg != null ? bg.GetComponent<CafeUiFocus>() : null;
            return ring != null ? ring.ChosenForTests : bg != null && bg.color == WeaponCardSelectedColor;
        }

        void ConfirmStart()
        {
            lastJob = currentJob.job;
            lastWeapon = SelectedWeapon;
            var chosen = onChosen;
            Destroy(canvas.gameObject);
            chosen(currentJob.job, SelectedWeapon);
        }

        Button BuildJobOption(float side, JobPreset preset)
        {
            var rt = UiFactory.Place($"Option_{preset.job}", root, new Vector2(0.5f, 1f),
                new Vector2(side * 300f, -220f), new Vector2(520f, 640f));
            var bg = UiFactory.AddImage(rt, new Color(0.16f, 0.13f, 0.2f, 1f));
            UiFactory.SkinCard(bg);

            var button = MakeButton(rt, bg, () => ShowWeaponStep(preset));

            var portrait = ArtLibrary.Get(preset.bodyArtId)?.Clip("idle")?.frames[0];
            if (portrait != null)
            {
                // 04 §8 "직업 선택 초상화는 각 idle_00을 잘라 재사용" — 몸체 알파 영역 위쪽(상반신)을 보여 준다.
                var portraitRt = UiFactory.Place("Portrait", rt, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(150f, 150f));
                var img = portraitRt.gameObject.AddComponent<Image>();
                img.sprite = portrait;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                var swatchRt = UiFactory.Place("Swatch", rt, new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                    new Vector2(90f, 90f));
                UiFactory.AddImage(swatchRt, preset.playerColor);
            }

            UiFactory.SkinHeader(UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -170f),
                new Vector2(460f, 70f)), preset.name, 46, TextAnchor.MiddleCenter, Color.white));
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -260f),
                new Vector2(440f, 260f)), preset.description, 26, TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));

            return button;
        }

        Button BuildWeaponOption(float side, WeaponPreset weapon, out Image bg)
        {
            var rt = UiFactory.Place($"Weapon_{weapon.kind}", root, new Vector2(0.5f, 1f),
                new Vector2(side * 300f, -220f), new Vector2(520f, 580f));
            bg = UiFactory.AddImage(rt, WeaponCardColor);
            UiFactory.SkinCard(bg);

            // 08:67 — 무기 카드는 선택만 하고, 확정은 "영업 시작". 선택 후 포커스를 영업 시작으로 옮겨 Enter 두 번이면 시작.
            // 마우스로 고른 경우엔 영업 시작을 미리 강조하지 않는다(UiInitialFocus.MoveAfterChoice).
            var button = MakeButton(rt, bg, () =>
            {
                SelectWeapon(weapon, true);
                if (StartButton != null) UiInitialFocus.MoveAfterChoice(StartButton.gameObject);
            });

            var icon = string.IsNullOrEmpty(weapon.iconResourcePath)
                ? null : Resources.Load<Sprite>(weapon.iconResourcePath);
            if (icon != null)
            {
                var iconRt = UiFactory.Place("Icon", rt, new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                    new Vector2(140f, 140f));
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            UiFactory.SkinHeader(UiFactory.AddText(UiFactory.Place("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -190f),
                new Vector2(460f, 70f)), weapon.name, 46, TextAnchor.MiddleCenter, Color.white));
            UiFactory.AddText(UiFactory.Place("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -280f),
                new Vector2(440f, 260f)), weapon.description, 30, TextAnchor.UpperCenter,
                new Color(0.9f, 0.9f, 0.9f));

            return button;
        }

        static Button MakeButton(RectTransform rt, Image bg, Action onClick)
        {
            var button = rt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);
            button.onClick.AddListener(() => onClick());
            return button;
        }
    }
}
