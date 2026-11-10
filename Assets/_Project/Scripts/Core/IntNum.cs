using System;

namespace CCT
{
    /// <summary>
    /// 2026-11-06 정수 전용 표기 규칙(사용자 지시 "소수점 싹다 없애. 정수로만"). 화면에 보이는 수치·실제 거래값은 모두 이 규칙으로 정수화한다.
    /// - 일반 반올림: 중간값은 0에서 먼 쪽(MidpointRounding.AwayFromZero). Mathf.RoundToInt(짝수 반올림)를 표시에 쓰지 않는다.
    /// - 남은 시간: 올림(Ceil) — 0초가 보이는 순간 = 실제로 끝난 순간.
    /// - 배율: 정수 퍼센트(1.5배 → 150%).
    /// - 1초 미만이거나 소수가 남는 고정 지속시간 설명: 밀리초 정수(0.5초 → 500ms).
    /// - 거리(월드 1 = 1m, 보스 방향 표시와 같은 단위): 고정 반경 설명은 cm 정수(2.5 → 250cm). 좌표·물리는 그대로 float.
    /// - 초당 회복(HP/초): 분당 회복 정수(1.2/초 → 분당 72).
    /// 엔진 연속값(좌표·deltaTime·보간·오디오)은 바꾸지 않고, 의미 있는 수치만 경계에서 정수화한다.
    /// </summary>
    public static class IntNum
    {
        /// <summary>표시·경계용 반올림(중간값은 0에서 먼 쪽). NaN은 0, 범위 밖은 int 범위로 자른다.</summary>
        public static int Round(double v)
        {
            if (double.IsNaN(v)) return 0;
            double r = Math.Round(v, MidpointRounding.AwayFromZero);
            if (r >= int.MaxValue) return int.MaxValue;
            if (r <= int.MinValue) return int.MinValue;
            return (int)r;
        }

        /// <summary>올림. 부동소수 누적 오차(3.0000002 → 4)를 막기 위해 1e-4 이내 초과분은 무시한다.</summary>
        public static int Ceil(double v)
        {
            if (double.IsNaN(v)) return 0;
            double r = Math.Ceiling(v - 1e-4);
            if (r >= int.MaxValue) return int.MaxValue;
            if (r <= int.MinValue) return int.MinValue;
            return (int)r;
        }

        /// <summary>내림(상한·용량처럼 넘으면 안 되는 한도). 1e-4 이내 부족분은 무시한다.</summary>
        public static int Floor(double v)
        {
            if (double.IsNaN(v)) return 0;
            double r = Math.Floor(v + 1e-4);
            if (r >= int.MaxValue) return int.MaxValue;
            if (r <= int.MinValue) return int.MinValue;
            return (int)r;
        }

        /// <summary>남은 시간(초) → 정수 초(올림, 음수는 0).</summary>
        public static int RemainingSeconds(double seconds) => seconds <= 0 ? 0 : Ceil(seconds);

        /// <summary>비율(0.15) → 정수 퍼센트(15).</summary>
        public static int Percent(double fraction) => Round(fraction * 100.0);

        /// <summary>배율(1.15) → 정수 퍼센트(115).</summary>
        public static int MultiplierPercent(double multiplier) => Round(multiplier * 100.0);

        /// <summary>"115%" — 배율 표기(×1.15 대신).</summary>
        public static string Multiplier(double multiplier) => MultiplierPercent(multiplier) + "%";

        /// <summary>정수 퍼센트값을 비율로 되돌린다(15 → 0.15). 실제 적용값을 표시값과 맞출 때 쓴다.</summary>
        public static float QuantizePercent(float fraction) => Percent(fraction) / 100f;

        /// <summary>고정 지속시간 설명: 정수 초면 "3초", 아니면 "500ms"/"1500ms".</summary>
        public static string Duration(double seconds)
        {
            int ms = Round(seconds * 1000.0);
            return ms % 1000 == 0 ? (ms / 1000) + "초" : ms + "ms";
        }

        /// <summary>월드 거리(1 = 1m) → cm 정수(2.5 → 250).</summary>
        public static int Centimeters(double world) => Round(world * 100.0);

        /// <summary>"250cm" — 고정 반경·거리 설명.</summary>
        public static string Distance(double world) => Centimeters(world) + "cm";

        /// <summary>초당 회복 → 분당 회복 정수(1.2 → 72).</summary>
        public static int PerMinute(double perSecond) => Round(perSecond * 60.0);

        /// <summary>
        /// 전투 피해 경계 정수화: 양수 피해는 반올림하되 최소 1(맞았는데 0이 뜨지 않게), 0·음수·NaN은 0.
        /// 무한대는 int 최대값(즉사 처리 그대로).
        /// </summary>
        public static int Damage(double raw)
        {
            if (double.IsNaN(raw) || raw <= 0) return 0;
            if (double.IsPositiveInfinity(raw)) return int.MaxValue;
            return Math.Max(1, Round(raw));
        }
    }
}
