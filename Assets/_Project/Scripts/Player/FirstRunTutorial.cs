namespace CCT
{
    /// <summary>08:70~72 "첫 런 안내" — 처음 한 번만 자동으로 보여줬는지 기억한다. settings.json의 tutorialSeen
    /// (SettingsService)에 저장한다. 예전 PlayerPrefs "cct_tutorial_seen"은 설정 파일이 없을 때만 이전한다.</summary>
    public static class FirstRunTutorial
    {
        public static bool Seen => SettingsService.Current.tutorialSeen;

        public static void MarkSeen() => SettingsService.SetTutorialSeen(true);

        /// <summary>테스트 전용 — "처음 실행" 상태로 되돌린다(테스트가 주입한 설정 경로에만 기록).</summary>
        public static void ResetForTests() => SettingsService.SetTutorialSeen(false);
    }
}
