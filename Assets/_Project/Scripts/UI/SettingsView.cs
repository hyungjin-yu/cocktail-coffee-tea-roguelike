using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 08장 "## 4. 설정". 모든 값은 SettingsService(settings.json v2)를 통해 읽고 쓴다 — 이 화면은 PlayerPrefs를 직접
    /// 만지지 않는다. 실제 동작이 있는 항목만 노출: 마스터/BGM/SFX 음량(0..100 정수, 즉시 미리듣기·저장), 조작
    /// 키보드/마우스, 해상도/표시모드(10초 확인 미리보기 — 확인 전엔 파일에 기록하지 않음), VSync(꺼짐=60fps 제한),
    /// 조작 안내 다시보기, 기본값 복원, 그리고 08:122~124 화면 흔들림(0/25/50/75/100 — CameraRig.Shake), 효과 강도
    /// (보통/낮음 — 장식 파티클 50%), 피해 숫자(켜짐/꺼짐 — DamageNumbers). 2열 배치(왼쪽 소리·조작, 오른쪽 화면·효과).
    /// 조작 안내 다시보기는 자기 안에서만 닫힌다("닫기" 버튼).
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        public const float ResolutionConfirmSeconds = 10f;

        GameObject root;
        GameObject confirmRoot;
        GameObject retryButtonRoot;
        Text schemeLabel;
        Text resolutionLabel;
        Text fullscreenLabel;
        Text vsyncLabel;
        Text shakeLabel;
        Text effectsLabel;
        Text damageNumbersLabel;
        Text confirmCountdownLabel;
        Text noticeLabel;
        Slider masterSlider, musicSlider, sfxSlider;
        Text masterValue, musicValue, sfxValue;
        Button revertButton;
        FirstRunTutorialView tutorialView;

        float confirmRemaining;
        bool refreshing;
        System.Action onClose;

        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>onClose는 이 화면을 닫을 때(닫기 버튼/Esc) 한 번 더 부르는 훅 — 호출한 쪽이 자기
        /// 화면(예: CharacterSelectView의 직업 선택 루트)을 다시 보여주고 싶을 때 씀. Pause처럼 자기
        /// 위에 얹히기만 하고 따로 복원할 화면이 없으면 null로 둔다.</summary>
        public static SettingsView Create(Transform canvas, System.Action onClose = null)
        {
            var rootRt = UiFactory.Stretch("Settings", canvas);
            var view = rootRt.gameObject.AddComponent<SettingsView>();
            view.root = rootRt.gameObject;
            view.onClose = onClose;

            UiFactory.AddImage(rootRt, new Color(0f, 0f, 0f, 0.82f));
            UiFactory.AddCafeBackdrop(rootRt);

            var title = UiFactory.Place("Title", rootRt, new Vector2(0.5f, 1f), new Vector2(0f, -90f),
                new Vector2(700f, 90f));
            UiFactory.AddText(title, "설정", 54, TextAnchor.MiddleCenter, Color.white);

            // 왼쪽 열: 소리·조작·안내 / 오른쪽 열: 화면·효과
            float y = -220f;
            ColumnX = LeftColumnX;
            view.masterSlider = AddVolumeSlider(view, rootRt, "MasterVolume", "마스터 음량", ref y, VolumeChannel.Master, out view.masterValue);
            view.musicSlider = AddVolumeSlider(view, rootRt, "MusicVolume", "BGM 음량", ref y, VolumeChannel.Music, out view.musicValue);
            view.sfxSlider = AddVolumeSlider(view, rootRt, "SfxVolume", "효과음 음량", ref y, VolumeChannel.Sfx, out view.sfxValue);

            view.schemeLabel = AddRow(rootRt, "Scheme", "조작 방식", ref y, out var schemeBtn);
            schemeBtn.onClick.AddListener(view.ToggleScheme);

            var tutorialRowLabel = AddRow(rootRt, "TutorialReplay", "조작 안내", ref y, out var tutorialBtn);
            tutorialRowLabel.text = "다시 보기";
            tutorialBtn.onClick.AddListener(view.ShowTutorialReplay);

            y = -220f;
            ColumnX = RightColumnX;
            view.resolutionLabel = AddRow(rootRt, "Resolution", "해상도", ref y, out var resBtn);
            resBtn.onClick.AddListener(view.CycleResolution);

            view.fullscreenLabel = AddRow(rootRt, "Fullscreen", "표시 모드", ref y, out var fsBtn);
            fsBtn.onClick.AddListener(view.ToggleFullscreen);

            view.vsyncLabel = AddRow(rootRt, "VSync", "VSync", ref y, out var vsyncBtn);
            vsyncBtn.onClick.AddListener(view.ToggleVSync);

            view.shakeLabel = AddRow(rootRt, "Shake", "화면 흔들림", ref y, out var shakeBtn);
            shakeBtn.onClick.AddListener(view.CycleShake);

            view.effectsLabel = AddRow(rootRt, "Effects", "효과 강도", ref y, out var effectsBtn);
            effectsBtn.onClick.AddListener(view.ToggleEffectsLevel);

            view.damageNumbersLabel = AddRow(rootRt, "DamageNumbers", "피해 숫자", ref y, out var dmgBtn);
            dmgBtn.onClick.AddListener(view.ToggleDamageNumbers);
            ColumnX = 0f;

            // 08:175와 같은 짧은 오류/알림 한 줄(설정 전용 — 진행 저장 오류와 섞지 않는다).
            var noticeRt = UiFactory.Place("Notice", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 145f),
                new Vector2(1200f, 32f)); // 마지막 행(아래 182) 과 버튼(위 140) 사이
            view.noticeLabel = UiFactory.AddText(noticeRt, "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.6f));

            var resetRt = UiFactory.Place("ResetDefaults", rootRt, new Vector2(0.5f, 0f), new Vector2(-360f, 60f),
                new Vector2(320f, 80f));
            var resetBg = UiFactory.AddImage(resetRt, new Color(0.32f, 0.3f, 0.34f));
            MakeButton(resetRt, resetBg, view.ResetToDefaults);
            UiFactory.AddText(UiFactory.Stretch("Label", resetRt), "기본값 복원", 28, TextAnchor.MiddleCenter, Color.white);

            var closeRt = UiFactory.Place("CloseButton", rootRt, new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                new Vector2(320f, 80f));
            var closeBg = UiFactory.AddImage(closeRt, new Color(0.24f, 0.4f, 0.5f));
            MakeButton(closeRt, closeBg, () => { AudioService.PlaySfx("ui_cancel"); view.Hide(); });
            UiFactory.AddText(UiFactory.Stretch("Label", closeRt), "닫기  [Esc]", 30, TextAnchor.MiddleCenter, Color.white);

            var retryRt = UiFactory.Place("RetrySettingsSave", rootRt, new Vector2(0.5f, 0f), new Vector2(360f, 60f),
                new Vector2(320f, 80f));
            var retryBg = UiFactory.AddImage(retryRt, new Color(0.24f, 0.4f, 0.5f));
            MakeButton(retryRt, retryBg, view.RetrySave);
            UiFactory.AddText(UiFactory.Stretch("Label", retryRt), "다시 시도", 28, TextAnchor.MiddleCenter, Color.white);
            view.retryButtonRoot = retryRt.gameObject;
            view.retryButtonRoot.SetActive(false);

            view.BuildResolutionConfirm(rootRt);
            view.tutorialView = FirstRunTutorialView.Create(canvas);

            view.root.SetActive(false);
            return view;
        }

        const float LeftColumnX = -430f;
        const float RightColumnX = 430f;
        const float RowWidth = 780f;
        static float ColumnX;

        static Text AddRow(Transform parent, string name, string label, ref float y, out Button button)
        {
            var rowRt = UiFactory.Place(name, parent, new Vector2(0.5f, 1f), new Vector2(ColumnX, y), new Vector2(RowWidth, 70f));
            var bg = UiFactory.AddImage(rowRt, new Color(0.16f, 0.13f, 0.2f));
            button = rowRt.gameObject.AddComponent<Button>();
            UiFactory.AddFocusSfx(button);
            button.targetGraphic = bg;
            UiFactory.ApplyHoverColors(button);

            UiFactory.AddText(UiFactory.Place("Name", rowRt, new Vector2(0f, 0.5f), new Vector2(30f, 0f),
                new Vector2(300f, 60f)), label, 28, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));
            var valueText = UiFactory.AddText(UiFactory.Place("Value", rowRt, new Vector2(1f, 0.5f),
                new Vector2(-30f, 0f), new Vector2(360f, 60f)), "", 28, TextAnchor.MiddleRight, Color.white);

            y -= 84f;
            return valueText;
        }

        /// <summary>08:117 0..100 정수 슬라이더. 값이 바뀌면 SettingsService가 AudioBus에 /100으로 적용하고 저장한다.</summary>
        static Slider AddVolumeSlider(SettingsView view, Transform parent, string name, string label, ref float y,
            VolumeChannel channel, out Text valueText)
        {
            var rowRt = UiFactory.Place(name, parent, new Vector2(0.5f, 1f), new Vector2(ColumnX, y), new Vector2(RowWidth, 70f));
            UiFactory.SkinPanel(UiFactory.AddImage(rowRt, new Color(0.16f, 0.13f, 0.2f)));

            UiFactory.AddText(UiFactory.Place("Name", rowRt, new Vector2(0f, 0.5f), new Vector2(30f, 0f),
                new Vector2(220f, 60f)), label, 28, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f));

            var sliderRt = UiFactory.Place("Slider", rowRt, new Vector2(0.5f, 0.5f), new Vector2(-10f, 0f),
                new Vector2(380f, 20f));
            var bgImg = UiFactory.AddImage(sliderRt, new Color(0.3f, 0.28f, 0.34f));
            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.targetGraphic = bgImg;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;

            var fillAreaRt = UiFactory.Stretch("FillArea", sliderRt);
            var fillRt = UiFactory.Stretch("Fill", fillAreaRt);
            UiFactory.AddImage(fillRt, new Color(0.35f, 0.75f, 0.7f));
            slider.fillRect = fillRt;

            var value = UiFactory.AddText(UiFactory.Place("Value", rowRt, new Vector2(1f, 0.5f),
                new Vector2(-30f, 0f), new Vector2(90f, 60f)), "", 26, TextAnchor.MiddleRight, Color.white);
            valueText = value;

            slider.onValueChanged.AddListener(v =>
            {
                if (view.refreshing) return;
                int percent = Mathf.RoundToInt(v);
                SettingsService.SetVolume(channel, percent); // 08:117 "즉시 미리듣기, 저장"
                value.text = $"{percent}";
                view.RefreshNotice();
            });

            y -= 84f;
            return slider;
        }

        /// <summary>테스트/버튼 공용 진입점(0..1 비율) — 슬라이더 값을 바꿔 onValueChanged를 그대로 태운다.</summary>
        public void SetMasterVolume(float v) => masterSlider.value = Mathf.Round(Mathf.Clamp01(v) * 100f);
        public void SetMusicVolume(float v) => musicSlider.value = Mathf.Round(Mathf.Clamp01(v) * 100f);
        public void SetSfxVolume(float v) => sfxSlider.value = Mathf.Round(Mathf.Clamp01(v) * 100f);

        void BuildResolutionConfirm(Transform parent)
        {
            var rt = UiFactory.Place("ResolutionConfirm", parent, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(700f, 260f));
            confirmRoot = rt.gameObject;
            UiFactory.SkinPanel(UiFactory.AddImage(rt, new Color(0.1f, 0.08f, 0.1f, 0.98f))); // 04 §8 ui_panel

            confirmCountdownLabel = UiFactory.AddText(UiFactory.Place("Message", rt, new Vector2(0.5f, 1f),
                    new Vector2(0f, -60f), new Vector2(620f, 100f)),
                "", 28, TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.85f));

            var keepRt = UiFactory.Place("Keep", rt, new Vector2(0.5f, 0f), new Vector2(-160f, 50f),
                new Vector2(280f, 78f));
            var keepBg = UiFactory.AddImage(keepRt, new Color(0.24f, 0.4f, 0.5f));
            MakeButton(keepRt, keepBg, ConfirmResolution);
            UiFactory.AddText(UiFactory.Stretch("Label", keepRt), "유지", 28, TextAnchor.MiddleCenter, Color.white);

            var revertRt = UiFactory.Place("Revert", rt, new Vector2(0.5f, 0f), new Vector2(160f, 50f),
                new Vector2(280f, 78f));
            var revertBg = UiFactory.AddImage(revertRt, new Color(0.5f, 0.24f, 0.24f));
            revertButton = MakeButton(revertRt, revertBg, RevertResolution);
            UiFactory.AddText(UiFactory.Stretch("Label", revertRt), "되돌리기", 28, TextAnchor.MiddleCenter, Color.white);

            confirmRoot.SetActive(false);
        }

        GameObject focusBeforeShow;

        public void Show()
        {
            focusBeforeShow = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            RefreshAll();
            confirmRoot.SetActive(false);
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(masterSlider.gameObject);
        }

        public void Hide()
        {
            // 08:128 "미확인 해상도 변경만 복원" — 확인 대기 중에 닫으면(Esc 포함) 직전 확정값으로 되돌린다.
            if (SettingsService.IsPreviewActive || confirmRoot.activeSelf) RevertResolution();
            root.SetActive(false);
            onClose?.Invoke();
            // QA 2026-10-04: 설정을 연 버튼(예: Pause의 설정)으로 키보드 포커스를 되돌린다.
            if (EventSystem.current != null && focusBeforeShow != null && focusBeforeShow.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(focusBeforeShow);
        }

        void RefreshAll()
        {
            refreshing = true;
            masterSlider.value = SettingsService.GetVolume(VolumeChannel.Master);
            musicSlider.value = SettingsService.GetVolume(VolumeChannel.Music);
            sfxSlider.value = SettingsService.GetVolume(VolumeChannel.Sfx);
            refreshing = false;
            masterValue.text = $"{SettingsService.GetVolume(VolumeChannel.Master)}";
            musicValue.text = $"{SettingsService.GetVolume(VolumeChannel.Music)}";
            sfxValue.text = $"{SettingsService.GetVolume(VolumeChannel.Sfx)}";
            RefreshLabels();
        }

        void RefreshLabels()
        {
            var d = SettingsService.EffectiveDisplay;
            schemeLabel.text = InputSettings.Label(InputSettings.Scheme);
            resolutionLabel.text = $"{d.width}×{d.height}";
            fullscreenLabel.text = d.windowed ? "창 모드" : "테두리 없는 전체 화면";
            vsyncLabel.text = SettingsService.Current.vsync ? "켜짐" : "꺼짐 (60fps 제한)";
            shakeLabel.text = $"{SettingsService.Current.shakePercent}%" + (SettingsService.Current.shakePercent == 0 ? " (끔)" : "");
            effectsLabel.text = SettingsService.Current.effectsLevel == SettingsData.EffectsLow ? "낮음 (장식 50%)" : "보통";
            damageNumbersLabel.text = SettingsService.Current.damageNumbers ? "켜짐" : "꺼짐";
            RefreshNotice();
        }

        void RefreshNotice()
        {
            bool failed = SettingsService.LastSaveFailed;
            string notice = failed ? SettingsService.NoticeSaveFailed : SettingsService.LoadNotice;
            noticeLabel.text = notice ?? "";
            retryButtonRoot.SetActive(failed);
        }

        /// <summary>테스트/버튼 공용 진입점 — 이 코드베이스는 실제 키/클릭을 흉내내는 대신 공개 API를
        /// 직접 호출해 검증하는 관례를 따른다(CardSelectView.Choose 등과 동일 패턴).</summary>
        public void ToggleScheme()
        {
            AudioService.PlaySfx("ui_confirm");
            InputSettings.Toggle();
            RefreshLabels();
        }

        /// <summary>08:118 "1920×1080, 모니터보다 크면1280×720" — 두 해상도만 순환. 미리보기 후 10초 확인.</summary>
        public void CycleResolution()
        {
            AudioService.PlaySfx("ui_confirm");
            var d = SettingsService.EffectiveDisplay;
            var target = d.width >= 1920 ? new Vector2Int(1280, 720) : new Vector2Int(1920, 1080);
            target = SettingsService.FitToDisplay(target.x, target.y);
            StartPreview(target.x, target.y, d.windowed);
        }

        /// <summary>08:119 표시 모드도 해상도와 같은 10초 확인 미리보기를 거친다.</summary>
        public void ToggleFullscreen()
        {
            AudioService.PlaySfx("ui_confirm");
            var d = SettingsService.EffectiveDisplay;
            StartPreview(d.width, d.height, !d.windowed);
        }

        void StartPreview(int width, int height, bool windowed)
        {
            SettingsService.BeginDisplayPreview(width, height, windowed);
            confirmRemaining = ResolutionConfirmSeconds;
            confirmRoot.SetActive(true);
            UpdateConfirmMessage();
            RefreshLabels();
            // 08:100과 같은 "안전한 선택 기본 포커스" 원칙 — 되돌리기(안전) 쪽에 둔다.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(revertButton.gameObject);
        }

        public void ConfirmResolution()
        {
            AudioService.PlaySfx("ui_confirm");
            SettingsService.ConfirmDisplayPreview();
            confirmRoot.SetActive(false);
            RefreshLabels();
        }

        public void RevertResolution()
        {
            AudioService.PlaySfx("ui_cancel");
            SettingsService.RevertDisplayPreview();
            confirmRoot.SetActive(false);
            RefreshLabels();
        }

        /// <summary>08:122 0/25/50/75/100 순환(0이면 완전 비활성).</summary>
        public void CycleShake()
        {
            AudioService.PlaySfx("ui_confirm");
            int next = (SettingsService.Current.shakePercent / 25 + 1) * 25;
            SettingsService.SetShakePercent(next > 100 ? 0 : next);
            RefreshLabels();
        }

        /// <summary>08:123 보통 ↔ 낮음(장식 파티클 50%, 경고/탄환/판정은 유지).</summary>
        public void ToggleEffectsLevel()
        {
            AudioService.PlaySfx("ui_confirm");
            SettingsService.SetEffectsLevel(SettingsService.Current.effectsLevel == SettingsData.EffectsLow
                ? SettingsData.EffectsNormal : SettingsData.EffectsLow);
            RefreshLabels();
        }

        /// <summary>08:124 피해 숫자 켜기/끄기(끄면 숫자만 숨김).</summary>
        public void ToggleDamageNumbers()
        {
            AudioService.PlaySfx("ui_confirm");
            SettingsService.SetDamageNumbers(!SettingsService.Current.damageNumbers);
            RefreshLabels();
        }

        public string ShakeLabelText => shakeLabel.text;
        public string EffectsLabelText => effectsLabel.text;
        public string DamageNumbersLabelText => damageNumbersLabel.text;

        public void ToggleVSync()
        {
            AudioService.PlaySfx("ui_confirm");
            SettingsService.SetVSync(!SettingsService.Current.vsync);
            RefreshLabels();
        }

        /// <summary>08:127 "기본값 복원" — 설정만 초기화(진행 save 불변). 해상도/표시모드가 기본과 다르면 그것만은 10초 확인
        /// 미리보기로 띄운다(확인 전 파일 기록 없음).</summary>
        public void ResetToDefaults()
        {
            AudioService.PlaySfx("ui_confirm");
            SettingsService.ResetToDefaults();
            RefreshAll();
            var def = SettingsService.DefaultResolution;
            var d = SettingsService.EffectiveDisplay;
            if (d.width != def.x || d.height != def.y || d.windowed) StartPreview(def.x, def.y, false);
        }

        public void RetrySave()
        {
            SettingsService.RetrySave();
            RefreshNotice();
        }

        public bool IsResolutionConfirmPending => confirmRoot != null && confirmRoot.activeSelf;
        public float ResolutionConfirmRemaining => confirmRemaining;
        /// <summary>테스트용 표시 문자열.</summary>
        public string ResolutionLabelText => resolutionLabel.text;
        public string WindowModeLabelText => fullscreenLabel.text;
        public string VSyncLabelText => vsyncLabel.text;
        public string NoticeText => noticeLabel.text;
        public bool RetryButtonVisible => retryButtonRoot != null && retryButtonRoot.activeSelf;

        /// <summary>08:125 "조작 안내 다시보기". 테스트용으로도 참조(SettingsViewTests).</summary>
        public FirstRunTutorialView TutorialView => tutorialView;
        public void ShowTutorialReplay() => tutorialView.Show("닫기", () => { });

        void Update()
        {
            if (!IsOpen) return;
            if (retryButtonRoot.activeSelf != SettingsService.LastSaveFailed) RefreshNotice(); // 5초 자동 재시도 반영
            TickConfirm(Time.unscaledDeltaTime);
        }

        /// <summary>확인 카운트다운(실제 시간). Update와 EditMode 테스트가 같은 경로를 쓴다. 0초가 되면 되돌린다.</summary>
        public void TickConfirm(float unscaledDeltaTime)
        {
            if (!confirmRoot.activeSelf) return;
            confirmRemaining -= unscaledDeltaTime;
            UpdateConfirmMessage();
            if (confirmRemaining <= 0f) RevertResolution();
        }

        void UpdateConfirmMessage()
        {
            var d = SettingsService.EffectiveDisplay;
            string mode = d.windowed ? "창 모드" : "전체 화면";
            confirmCountdownLabel.text = $"화면을 {d.width}×{d.height} {mode}(으)로 바꿨습니다.\n" +
                                          $"{Mathf.CeilToInt(Mathf.Max(0f, confirmRemaining))}초 후 자동으로 되돌립니다.";
        }

        static Button MakeButton(RectTransform rt, Image bg, System.Action onClick)
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
