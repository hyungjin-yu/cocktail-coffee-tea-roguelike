using UnityEngine;

namespace CCT
{
    /// <summary>
    /// SaveData를 세션 동안 캐시해두는 진입점. GameManager는 런 시작 시 Current를 읽어 영구 보너스를
    /// 적용하고, 강화 보스를 처치하면 AddSpecialty로 즉시 저장한다 (런 종료를 기다리지 않음 —
    /// 사망해도 이미 처치한 보스의 스페셜티는 유지되어야 하므로).
    /// </summary>
    public static class PermanentProgress
    {
        static SaveData cached;
        static string overridePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cached = null;
            overridePath = null;
        }

        public static SaveData Current => cached ??= SaveSystem.Load(overridePath);

        public static void AddSpecialty(int amount)
        {
            if (amount <= 0) return;
            Current.specialtyPoints += amount;
            SaveSystem.Save(Current, overridePath);
        }

        /// <summary>테스트 전용 — 실제 세이브 파일 대신 임시 경로를 쓰게 한다. null을 주면 원래 경로로 되돌린다.</summary>
        public static void SetPathForTests(string path)
        {
            overridePath = path;
            cached = null;
        }
    }
}
