using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>2026-11-24 #25 시그니처(정식 레시피 1회 전투 효과) 종류. 저장에는 id 문자열만 쓴다(이 값은 저장하지 않음).</summary>
    public enum SignatureEffect { None = 0, SaltRim = 1, Sidecar = 2, WhiteOut = 3 }

    /// <summary>정식 레시피 한 종(공식 1차 출처의 배합). amountsMl은 공식 mL — 판정은 비율로 한다(잔 한도 때문에 축소 배합도 인정).</summary>
    public sealed class SignatureDef
    {
        public string id;
        public string name;
        /// <summary>IBA 분류 표기(확인하지 못했으면 "").</summary>
        public string category;
        public string sourceUrl;
        /// <summary>공식 방법 요약(도감 사실 표기, 발견 후에만 보인다).</summary>
        public string method;
        public string garnish;
        public string[] ingredientIds;
        public float[] amountsMl;
        public DrinkTechnique[] techniques;
        public SignatureEffect effect;
        public string effectName;
        /// <summary>HUD·공개 화면의 쉬운 한 줄(무엇을 하는지).</summary>
        public string effectShort;
        /// <summary>도감 상세(수치 포함).</summary>
        public string effectDetail;
        public Color accent;

        public float OfficialTotalMl
        {
            get { float t = 0f; foreach (var a in amountsMl) t += a; return t; }
        }
    }

    /// <summary>
    /// #25 정식 레시피 판정(1차 3종). 정답표는 코드에만 있고 저장하지 않는다. 판정은 **실제 제조 확정**(DrinkLab.Craft / 런 잔 사용)에서만 부른다 —
    /// 미리보기·불러오기·공유 코드·목표 음료·판매 주문 경로는 Match를 부르지 않는다(정답 누출 방지). 미발견 레시피의 이름·재료·효과는
    /// 어떤 화면에도 나오지 않고, 발견 전에 알 수 있는 것은 개수(Count)뿐이다.
    ///
    /// 판정: 칵테일 · 재료 집합 정확히 일치(빠지거나 남으면 불일치) · 허용 기법 · 재료별 비율 |s−t| ≤ max(1.5%p, 12%×t) ·
    /// 액체 합(희석수 제외) ≥ max(45mL, 공식 총량의 50%). 희석수·얼음·투입 순서·이름은 보지 않는다.
    /// 1차 범위: 출처 확인 + 현재 재료로 정확히 만들 수 있고 아직 공개되지 않은 3종. 네그로니·드라이 마티니(교본·예시로 공개됨),
    /// 대체 재료(Cointreau→트리플 섹 등)·비터스·차·커피 정답은 넣지 않는다(설계 보고서 #25 §6·§7).
    /// </summary>
    public static class DrinkSignatures
    {
        public const float AbsTolerance = 0.015f;
        public const float RelTolerance = 0.12f;
        public const float MinTotalMl = 45f;
        public const float MinTotalFraction = 0.5f;

        public static readonly IReadOnlyList<SignatureDef> All = new List<SignatureDef>
        {
            new SignatureDef
            {
                id = "iba_margarita", name = "마가리타", category = "",
                sourceUrl = "https://iba-world.com/iba-cocktail/margarita/",
                method = "얼음과 셰이킹 후 차갑게 한 칵테일 잔에 거르기", garnish = "잔 반쪽 소금 테두리(선택)",
                ingredientIds = new[] { DrinkIngredients.TequilaBlanco, DrinkIngredients.TripleSec, DrinkIngredients.LimeJuice },
                amountsMl = new[] { 50f, 20f, 15f }, techniques = new[] { DrinkTechnique.Shake },
                effect = SignatureEffect.SaltRim, effectName = "솔트 림",
                effectShort = "주변 적을 소금 고리로 날리고 잠시 멈춤",
                effectDetail = "내 주변(반경 4) 적에게 공격력의 250% 피해 + 일반·정예 2초 경직. 보스는 피해만(최대 체력 12% 상한).",
                accent = new Color(0.86f, 0.95f, 0.62f),
            },
            new SignatureDef
            {
                id = "iba_sidecar", name = "사이드카", category = "The Unforgettables",
                sourceUrl = "https://iba-world.com/iba-cocktail/sidecar/",
                method = "얼음과 충분히 셰이킹 후 차갑게 한 칵테일 잔에 거르기", garnish = "",
                ingredientIds = new[] { DrinkIngredients.Cognac, DrinkIngredients.TripleSec, DrinkIngredients.LemonJuice },
                amountsMl = new[] { 50f, 20f, 20f }, techniques = new[] { DrinkTechnique.Shake },
                effect = SignatureEffect.Sidecar, effectName = "사이드카 질주",
                effectShort = "5초 무적 + 빠른 이동",
                effectDetail = "5초 동안 받는 피해 무효 + 이동 속도 +40%(기존 이동 속도 상한 유지).",
                accent = new Color(0.98f, 0.72f, 0.32f),
            },
            new SignatureDef
            {
                id = "iba_white_lady", name = "화이트 레이디", category = "The Unforgettables",
                sourceUrl = "https://iba-world.com/iba-cocktail/white-lady/",
                method = "얼음과 충분히 셰이킹 후 차갑게 한 칵테일 잔에 거르기", garnish = "",
                ingredientIds = new[] { DrinkIngredients.Gin, DrinkIngredients.TripleSec, DrinkIngredients.LemonJuice },
                amountsMl = new[] { 40f, 30f, 20f }, techniques = new[] { DrinkTechnique.Shake },
                effect = SignatureEffect.WhiteOut, effectName = "화이트 아웃",
                effectShort = "화면의 적을 얼리고 피해",
                effectDetail = "화면 안 모든 적에게 공격력의 150% 피해 + 일반·정예 4초 경직. 보스는 피해만(최대 체력 12% 상한).",
                accent = new Color(0.82f, 0.93f, 1f),
            },
        };

        public static SignatureDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        /// <summary>판정 대상 개수(발견 전에 보여 줄 수 있는 유일한 정보). 1차는 칵테일만.</summary>
        public static int Count(DrinkKind kind)
        {
            if (kind != DrinkKind.Cocktail) return 0;
            return All.Count;
        }

        /// <summary>
        /// 실제 제조 확정 배합이 정식 레시피인지(아니면 null). **미리보기·불러오기 경로에서 부르지 않는다**(호출 지점: DrinkLab.Craft, DrinkLab.CraftInRun).
        /// 손상·음수·알 수 없는 재료·중복 줄은 불일치로 본다.
        /// </summary>
        public static SignatureDef Match(DrinkRecipe r)
        {
            if (r?.steps == null || r.Kind != DrinkKind.Cocktail) return null;
            var amounts = new Dictionary<string, int>();
            foreach (var s in r.steps)
            {
                if (s == null || s.amount <= 0) continue;
                if (DrinkIngredients.Get(s.id) == null || amounts.ContainsKey(s.id)) return null;
                amounts[s.id] = s.amount;
            }
            foreach (var def in All)
                if (Matches(def, amounts, r.Technique)) return def;
            return null;
        }

        static bool Matches(SignatureDef def, Dictionary<string, int> amounts, DrinkTechnique technique)
        {
            if (amounts.Count != def.ingredientIds.Length) return false;
            if (Array.IndexOf(def.techniques, technique) < 0) return false;
            long totalTenths = 0;
            foreach (var id in def.ingredientIds)
            {
                if (!amounts.TryGetValue(id, out int a)) return false;
                totalTenths += a;
            }
            float totalMl = totalTenths / 10f;
            float official = def.OfficialTotalMl;
            if (totalMl < Mathf.Max(MinTotalMl, official * MinTotalFraction)) return false;
            for (int i = 0; i < def.ingredientIds.Length; i++)
            {
                float t = def.amountsMl[i] / official;
                float s = amounts[def.ingredientIds[i]] / 10f / totalMl;
                if (Mathf.Abs(s - t) > Mathf.Max(AbsTolerance, RelTolerance * t) + 1e-6f) return false;
            }
            return true;
        }

        /// <summary>도감 공개 줄(발견한 것만 호출). 공식 배합 mL·방법.</summary>
        public static string OfficialLine(SignatureDef d)
        {
            if (d == null) return "";
            var parts = new List<string>();
            for (int i = 0; i < d.ingredientIds.Length; i++)
                parts.Add($"{DrinkIngredients.Get(d.ingredientIds[i])?.name ?? d.ingredientIds[i]} {d.amountsMl[i]:0}mL");
            return string.Join(" · ", parts);
        }

        /// <summary>쉬운 판정 안내(발견 후 도감).</summary>
        public const string ToleranceNote = "공식 배합과 같은 재료·같은 기법이면, 비율이 조금 달라도(재료마다 약 1~4%p) 인정합니다. 잔이 작으면 같은 비율로 줄여도 됩니다(최소 45mL).";
    }
}
