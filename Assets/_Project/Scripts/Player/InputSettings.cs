using UnityEngine;

namespace CCT
{
    public enum MovementScheme { Keyboard, Mouse }

    /// <summary>플레이어 조작 방식 설정. PlayerPrefs 에 저장해 다음 실행에도 유지된다.</summary>
    public static class InputSettings
    {
        const string PrefKey = "cct_movement_scheme";
        static MovementScheme? cached;

        public static MovementScheme Scheme
        {
            get
            {
                cached ??= (MovementScheme)PlayerPrefs.GetInt(PrefKey, (int)MovementScheme.Keyboard);
                return cached.Value;
            }
            set
            {
                cached = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static void Toggle() =>
            Scheme = Scheme == MovementScheme.Keyboard ? MovementScheme.Mouse : MovementScheme.Keyboard;

        public static string Label(MovementScheme scheme) =>
            scheme == MovementScheme.Keyboard ? "키보드" : "마우스";
    }
}
