namespace CCT
{
    public enum MovementScheme { Keyboard, Mouse }

    /// <summary>플레이어 조작 방식 설정. settings.json(SettingsService)의 movementScheme을 읽고 쓴다 — 다음 실행에도
    /// 유지된다. 예전 PlayerPrefs "cct_movement_scheme"은 설정 파일이 없을 때만 이전하고 지우지 않는다.</summary>
    public static class InputSettings
    {
        public static MovementScheme Scheme
        {
            get => SettingsService.Current.Scheme;
            set => SettingsService.SetMovementScheme(value);
        }

        public static void Toggle() =>
            Scheme = Scheme == MovementScheme.Keyboard ? MovementScheme.Mouse : MovementScheme.Keyboard;

        public static string Label(MovementScheme scheme) =>
            scheme == MovementScheme.Keyboard ? "키보드" : "마우스";
    }
}
