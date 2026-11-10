using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 따르기·젓기 동작 계산(순수 함수). 화면 요소·입력·장부는 DrinkStageView가 맡고, 이 클래스는 기울기 → 속도, 양 → 흐름 시간,
    /// 시간 → 바스푼 자세 수치만 낸다. DrinkStageView의 같은 이름 공개 상수·함수는 이쪽을 그대로 넘긴다(기존 호출자 유지).
    /// </summary>
    public static class DrinkPourMotion
    {
        public const float LiquidMaxRate = 250f; // 0.1mL/초 → 25mL/s
        public const float LeafMaxRate = 25f;    // 0.1g/초 → 2.5g/s
        public const float BeanMaxRate = 80f;    // 0.1g/초 → 8g/s (원두는 알갱이가 커서 빨리 떨어진다)

        /// <summary>흐름 시작 각도 onset에서 50° 더 기울이면 최대 속도(제곱 곡선).</summary>
        public static float Rate(float tilt, DrinkIngredientDef def, float onset)
        {
            if (def == null || tilt <= onset) return 0f;
            float t = Mathf.Clamp01((tilt - onset) / 50f);
            float max = def.form == IngredientForm.Leaf ? (def.IsCoffeeBean ? BeanMaxRate : LeafMaxRate) : LiquidMaxRate;
            return t * t * max * (1f - 0.6f * def.viscosity);
        }

        /// <summary>찻잎·원두 계량 도구 용량(차하 또는 원두 스쿱, 0.1g).</summary>
        public static int LeafToolCapacity(DrinkIngredientDef def) => def != null && def.IsCoffeeBean ? DrinkRules.CoffeeScoopCapacity : DrinkRules.ChahaCapacity;

        /// <summary>흐름 시간: 양·점성 비례(보통 0.8~1.2초, 많거나 걸쭉하면 길게). 찻잎은 잎이 다 떨어지는 시간.</summary>
        public static float PourSeconds(DrinkIngredientDef def, int tenths)
        {
            if (def == null) return 0.8f;
            if (def.form == IngredientForm.Leaf) return Mathf.Lerp(0.7f, 1.1f, Mathf.Clamp01(tenths / (float)LeafToolCapacity(def)));
            float baseT = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(tenths / (float)DrinkRules.JiggerCapacity));
            return baseT * (1f + 0.6f * def.viscosity);
        }

        public static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>그 양의 액면이 입구 높이에 처음 닿는 기울기(도). 프로필이 없거나 비었으면 90°.</summary>
        public static float SpillOnsetAngle(FluidVesselProfile profile, double volumeMl)
        {
            if (profile == null || volumeMl <= 0) return 90f;
            bool Reached(float a)
            {
                var s = DrinkFluidPhysics.SolveTilt(profile, new[] { volumeMl }, a);
                return s.LipReached || s.SpillMl > 1e-6;
            }
            for (float a = 0f; a <= 178f; a += 3f)
            {
                if (!Reached(a)) continue;
                float lo = Mathf.Max(0f, a - 3f), hi = a;
                for (int i = 0; i < 6; i++) { float mid = (lo + hi) * 0.5f; if (Reached(mid)) hi = mid; else lo = mid; }
                return hi;
            }
            return 178f;
        }

        // ───── 바스푼 젓기 ─────

        /// <summary>스터링 넣기/빼기 구간과 전체 길이(초). ramp = Clamp01(min(t, dur - t) / insert).</summary>
        public const float StirInsert = 0.25f, StirDuration = 1.8f + 2f * StirInsert;

        /// <summary>넣기(0→1)·젓기(1)·빼기(1→0) 깊이.</summary>
        public static float StirRamp(float t) => Mathf.Clamp01(Mathf.Min(t, StirDuration - t) / StirInsert);

        /// <summary>장부를 섞는 구간(넣기·빼기 제외)인가.</summary>
        public static bool StirMixing(float t) => t > StirInsert && t < StirDuration - StirInsert;

        /// <summary>그 프레임의 혼합 강도(젓는 구간 전체 합 = 6).</summary>
        public static double StirMixStep(float dt) => dt * 6.0 / (StirDuration - 2f * StirInsert);

        /// <summary>바스푼 애니메이션 프레임(초당 10장, 한 장이면 0).</summary>
        public static int SpoonFrame(float t, int frameCount) => frameCount > 1 ? Mathf.FloorToInt(t * 10f) % frameCount : 0;

        /// <summary>끝 높이(정규화): ramp 0 = 림 위 대기(above), 1 = 바닥 위 + 0~2 아트 픽셀 들썩임.</summary>
        public static float SpoonTipY(float t, float ramp, float above, float floor, float artH)
        {
            float bob = (0.5f + 0.5f * Mathf.Sin(t * 7f)) * 2f / artH;
            return Mathf.Lerp(above, floor + bob, Ease(Mathf.Clamp01(ramp)));
        }

        /// <summary>젓는 각위상(라디안).</summary>
        public static float StirPhase(float t) => t * 9f;

        /// <summary>젓는 가로 반폭(정규화) = 마스크 구간 반폭 − 볼 반폭 − 1.5 아트 픽셀.</summary>
        public static float StirReach(float spanMin, float spanMax, float bowl, float artW) => Mathf.Max(0f, (spanMax - spanMin) * 0.5f - bowl - 1.5f / artW);

        /// <summary>그 위상의 끝 x(정규화)와 기울기(도).</summary>
        public static float SpoonX(float cx, float amp, float reach, float phase) => cx + amp * reach * Mathf.Sin(phase);
        public static float SpoonLean(float amp, float phase) => amp * 4f * Mathf.Cos(phase);

        /// <summary>바스푼 아래쪽(볼) 행들 중 끝 중심에서 가장 먼 가로 거리(아트 픽셀).</summary>
        public static float BowlHalfWidth(int[] rows, int sw)
        {
            int tip = 0;
            while (tip * 2 < rows.Length && rows[tip * 2] < 0) tip++;
            if (tip * 2 >= rows.Length) return 3f;
            float c = (rows[tip * 2] + rows[tip * 2 + 1]) * 0.5f, half = 0f;
            for (int y = tip; y < Mathf.Min(rows.Length / 2, tip + 14); y++)
            {
                if (rows[y * 2] < 0) continue;
                half = Mathf.Max(half, Mathf.Max(c - rows[y * 2], rows[y * 2 + 1] - c));
            }
            return half;
        }
    }
}
