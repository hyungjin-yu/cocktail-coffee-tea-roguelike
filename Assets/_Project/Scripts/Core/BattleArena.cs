using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 57(2026-10-15) P0-1 유한 전투 구역. 첫 플레이 피드백 "한 방향으로 계속 가다 보스를 못 찾았다"의 원인은 X축이 무한이라
    /// 플레이어(이동 5)가 보스(1.3~3.6)보다 빨라 화면 밖 스폰 보스가 영영 화면에 들어오지 못한 것이다.
    /// 플레이어·적·보스·카메라·스폰이 모두 이 구역 하나를 기준으로 맞춘다. 깊이(Y) 레인은 기존 Balance.DepthMin/Max 그대로.
    /// </summary>
    public static class BattleArena
    {
        /// <summary>구역 반폭(월드 유닛). 16:9 화면 폭(약 21.3) 기준 약 2.8화면 — 이동 5로 끝에서 끝까지 약 12초.</summary>
        public const float HalfWidth = 30f;
        public const float MinX = -HalfWidth;
        public const float MaxX = HalfWidth;
        /// <summary>카메라가 경계 바깥을 이만큼 더 보여 줘서 벽(경계 표시)이 화면 안에 보이게 한다.</summary>
        public const float OutsideReveal = 1.2f;

        /// <summary>반지름 radius인 몸이 구역 안에 머무는 X.</summary>
        public static float ClampX(float x, float radius = 0f)
        {
            float r = Mathf.Clamp(radius, 0f, HalfWidth);
            return Mathf.Clamp(x, MinX + r, MaxX - r);
        }

        public static bool Contains(float x, float radius = 0f) => Mathf.Approximately(ClampX(x, radius), x);

        /// <summary>화면 반폭 halfView일 때 카메라 중심 X의 허용 한계(양수). 구역이 화면보다 좁으면 0(중앙 고정).</summary>
        public static float CameraLimit(float halfView) => Mathf.Max(0f, HalfWidth + OutsideReveal - halfView);

        public static float ClampCameraX(float x, float halfView)
        {
            float limit = CameraLimit(halfView);
            return Mathf.Clamp(x, -limit, limit);
        }

        /// <summary>
        /// 화면 밖 side쪽(−1 왼쪽/+1 오른쪽) 진입 X. 그쪽이 구역 밖(벽 너머)이면 반대쪽 화면 밖으로 바꾸고,
        /// 양쪽 다 막히면(구역이 화면보다 좁을 때) 원래 쪽 벽 안쪽에 둔다. 실제 사용한 쪽을 usedSide로 돌려준다.
        /// </summary>
        public static float SpawnX(float camX, float halfView, float side, float margin, float radius, out float usedSide)
        {
            side = side < 0f ? -1f : 1f;
            float preferred = camX + side * (halfView + margin);
            if (Contains(preferred, radius)) { usedSide = side; return preferred; }
            float other = camX - side * (halfView + margin);
            if (Contains(other, radius)) { usedSide = -side; return other; }
            usedSide = side;
            return ClampX(preferred, radius);
        }

        /// <summary>안내 문구용 — 실제로 쓴 쪽(왼쪽/오른쪽/양쪽).</summary>
        public static string SideLabel(bool left, bool right) =>
            left && right ? "양쪽" : left ? "왼쪽" : right ? "오른쪽" : "양쪽";
    }
}
