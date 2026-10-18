using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 장판(DOT) 피해가 겹쳤을 때 "그 틱의 가장 높은 DPS 하나만 적용"을 강제한다 (06:23).
    /// 개별 장판(BurstZone 등)은 자기 틱마다 플레이어에게 직접 피해를 주지 않고 여기에 후보로
    /// 등록만 한다. 같은 프레임에 여러 장판이 후보를 등록해도 가장 높은 dps 하나만 실제로 적용되며,
    /// 실제 적용은 PlayerController.LateUpdate에서 그 프레임의 모든 Update가 끝난 뒤 한 번 일어난다.
    /// </summary>
    public static class PlayerZoneDamage
    {
        static int pendingFrame = -1;
        static float pendingDps;
        /// <summary>67: 가장 높은 dps 후보의 출처(같으면 먼저 등록한 것). 피해 규칙에는 관여하지 않는다.</summary>
        static DamageSource pendingSource;

        /// <summary>67: 마지막 ConsumeFrame이 돌려준 후보의 출처(후보가 없었으면 기본값 = 확인 불가).</summary>
        public static DamageSource LastConsumedSource { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pendingFrame = -1;
            pendingDps = 0f;
            pendingSource = default;
            LastConsumedSource = default;
        }

        /// <summary>이번 프레임에 dps짜리 장판 피해 후보를 등록한다. 장판의 Update()에서 자기 틱마다 호출.</summary>
        public static void RequestTick(float dps) => RequestTick(dps, default);

        public static void RequestTick(float dps, DamageSource source)
        {
            int frame = Time.frameCount;
            if (frame != pendingFrame)
            {
                pendingFrame = frame;
                pendingDps = dps;
                pendingSource = source;
            }
            else
            {
                float next = Mathf.Max(pendingDps, dps); // 가장 높은 dps 하나만(06:23) — 기존 계산 그대로
                if (dps > pendingDps) pendingSource = source;
                pendingDps = next;
            }
        }

        /// <summary>이번 프레임에 등록된 후보 중 가장 높은 dps를 반환하고 비운다. 후보가 없으면 0.
        /// PlayerController.LateUpdate에서 한 번만 호출한다.</summary>
        public static float ConsumeFrame()
        {
            if (pendingFrame != Time.frameCount)
            {
                LastConsumedSource = default;
                return 0f;
            }
            float dps = pendingDps;
            LastConsumedSource = pendingSource;
            pendingFrame = -1;
            pendingDps = 0f;
            pendingSource = default;
            return dps;
        }
    }
}
