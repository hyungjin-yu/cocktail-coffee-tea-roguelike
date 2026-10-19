using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>72 §6 손님 주문 1건. 핵심 조건 = 음료 계열 + 지정 재료 2종 최소량(기준 사용량의 50%). 기법·색은 선택 보너스.</summary>
    [Serializable]
    public class DrinkOrder
    {
        public string id = "";
        public int kind;
        public string ingA = "", ingB = "";
        public int minA, minB;
        public int technique;
        public string colorFamily = "";
        /// <summary>"Open" / "Cancelled"(다음 런 종료 정산 때 새 주문).</summary>
        public string state = "";
        public bool IsOpen => state == "Open";
    }

    /// <summary>코인으로 산 외형(현금 결제 영수증 기록과 분리 저장, 게임 소유 판정에서 합친다).</summary>
    public sealed class CoinCosmetic
    {
        public string id, name, kind, description;
        public int price;
    }

    /// <summary>72 §6 판매·주문·코인 외형(최초 기본값). 판매 코인은 외형 구매 전용이며 RP·스페셜티·재료와 바꾸지 않는다.</summary>
    public static class DrinkSales
    {
        public const int Slots = 3, BasePrice = 100, TechniqueBonus = 25, ColorBonus = 25;
        public const string CafeMap = "map_cafe_common";

        /// <summary>판매 상점(기존 준비된 외형만: 캐릭터 외형 2종, 공용 카페 맵 외형 1종). 새 스킨 제작 수량 규칙과 별개.</summary>
        public static readonly CoinCosmetic[] Catalog =
        {
            new CoinCosmetic { id = SkinCatalog.BartenderMidnight, name = "미드나잇 바텐더", kind = "skin", price = 1000, description = "바텐더 캐릭터 외형(능력치 같음)" },
            new CoinCosmetic { id = SkinCatalog.TeaJade, name = "비취 티 소믈리에", kind = "skin", price = 1000, description = "티 소믈리에 캐릭터 외형(능력치 같음)" },
            new CoinCosmetic { id = CafeMap, name = "공용 야간 카페", kind = "map", price = 1500, description = "전투 배경만 바뀝니다(충돌·스폰·드랍·난도 그대로)" },
        };

        public static CoinCosmetic Cosmetic(string id) { foreach (var c in Catalog) if (c.id == id) return c; return null; }

        /// <summary>색 계열 이름(사용자 표시·주문 판정 공용, 색 코드 노출 없음).</summary>
        public static string ColorFamily(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (max - min < 0.08f) return c.a < 0.45f ? "무색" : "옅은 색";
            if (c.r >= c.g && c.r >= c.b) return c.g > c.b + 0.15f ? "주황·호박색" : c.b > c.g + 0.1f ? "보랏빛 붉은색" : "붉은색";
            if (c.b >= c.r && c.b >= c.g) return c.r > c.g + 0.05f ? "보랏빛" : "푸른색";
            return "초록빛";
        }

        public static string FamilyOf(DrinkRecipe r) => ColorFamily(DrinkCalculator.Look(r).color);

        /// <summary>주문의 최소량(0.1 단위) = 기준 사용량 d의 50%.</summary>
        public static int MinFor(string id) { var row = DrinkSupply.Get(id); return row != null ? row.baseUse / 2 : 0; }

        /// <summary>판매 가능 = 계열이 같고 지정 재료 둘 다 최소량 이상.</summary>
        public static bool Matches(DrinkOrder o, DrinkRecipe r) =>
            o != null && o.IsOpen && r != null && (int)r.Kind == o.kind && r.AmountOf(o.ingA) >= o.minA && r.AmountOf(o.ingB) >= o.minB;

        public static bool TechniqueMatches(DrinkOrder o, DrinkRecipe r) => o != null && r != null && (int)r.Technique == o.technique;
        public static bool ColorMatches(DrinkOrder o, DrinkRecipe r) => o != null && r != null && FamilyOf(r) == o.colorFamily;

        /// <summary>가격(재료를 더 넣거나 버프가 커도 오르지 않는다): 100 + 기법 25 + 색 25, 최대 150. 조건 미달이면 0.</summary>
        public static int Price(DrinkOrder o, DrinkRecipe r) =>
            !Matches(o, r) ? 0 : BasePrice + (TechniqueMatches(o, r) ? TechniqueBonus : 0) + (ColorMatches(o, r) ? ColorBonus : 0);

        /// <summary>
        /// 새 주문(결정적: serial). 보유/발견한 재료(seen)와 그 계열에서 쓸 수 있는 기법만. 두 재료는 서로 다르고 같은 계열에서 쓸 수 있으며,
        /// 최소량 합이 용량 한도를 넘지 않는다. 색 계열은 지정 재료 최소량(+차는 찻잎) 배합의 실제 색으로 정해 모순이 없다.
        /// basic=true면 기본 조합(진·레몬 / 홍차·꿀)으로 만든다.
        /// </summary>
        public static DrinkOrder Generate(int serial, Func<string, bool> seen, bool basic = false)
        {
            var kind = basic ? (serial % 2 == 0 ? DrinkKind.Cocktail : DrinkKind.Tea) : (Hash(serial, 7) % 2 == 0 ? DrinkKind.Cocktail : DrinkKind.Tea);
            var pool = new List<string>();
            foreach (var row in DrinkSupply.Table)
            {
                var def = DrinkIngredients.Get(row.id);
                if (def != null && def.UsableIn(kind) && (seen == null || seen(row.id))) pool.Add(row.id);
            }
            if (pool.Count < 2)
            {
                kind = kind == DrinkKind.Tea ? DrinkKind.Cocktail : DrinkKind.Tea;
                pool.Clear();
                foreach (var row in DrinkSupply.Table) { var def = DrinkIngredients.Get(row.id); if (def != null && def.UsableIn(kind) && (seen == null || seen(row.id))) pool.Add(row.id); }
            }
            string a, b;
            if (basic || pool.Count < 2)
            {
                a = kind == DrinkKind.Tea ? DrinkIngredients.BlackTea : DrinkIngredients.Gin;
                b = kind == DrinkKind.Tea ? DrinkIngredients.Honey : DrinkIngredients.LemonJuice;
            }
            else
            {
                int i = Hash(serial, 11) % pool.Count;
                int j = Hash(serial, 13) % (pool.Count - 1);
                if (j >= i) j++;
                a = pool[i]; b = pool[j];
            }
            var techs = kind == DrinkKind.Tea ? new[] { DrinkTechnique.Steep } : new[] { DrinkTechnique.Shake, DrinkTechnique.Stir, DrinkTechnique.Build, DrinkTechnique.Layer };
            var tech = basic && kind == DrinkKind.Cocktail ? DrinkTechnique.Shake : techs[Hash(serial, 17) % techs.Length];
            var o = new DrinkOrder { id = "order" + serial, kind = (int)kind, ingA = a, ingB = b, minA = MinFor(a), minB = MinFor(b), technique = (int)tech, state = "Open" };
            o.colorFamily = FamilyOf(Reference(o));
            return o;
        }

        /// <summary>주문을 그대로 만족하는 최소 배합(색 계열 기준·시험용). 차에 찻잎이 없으면 홍차 1g, 칵테일에 베이스 술이 없으면 진 15mL를 더한다.</summary>
        public static DrinkRecipe Reference(DrinkOrder o)
        {
            var r = DrinkRecipe.NewDraft((DrinkKind)o.kind);
            r.technique = o.technique;
            r.SetAmount(o.ingA, o.minA);
            r.SetAmount(o.ingB, o.minB);
            if (!DrinkValidator.Check(r).Ok)
            {
                if (r.Kind == DrinkKind.Tea && r.AmountOf(DrinkIngredients.BlackTea) == 0) r.SetAmount(DrinkIngredients.BlackTea, 10);
                else if (r.Kind == DrinkKind.Cocktail && r.AmountOf(DrinkIngredients.Gin) == 0) r.SetAmount(DrinkIngredients.Gin, 150);
            }
            return r;
        }

        static int Hash(int serial, int salt)
        {
            unchecked
            {
                uint h = (uint)(serial * 374761393 + salt * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (int)((h ^ (h >> 16)) & 0x7FFFFFFF);
            }
        }

        public static string Describe(DrinkOrder o)
        {
            if (o == null || !o.IsOpen) return "주문 없음";
            var a = DrinkIngredients.Get(o.ingA); var b = DrinkIngredients.Get(o.ingB);
            return $"{((DrinkKind)o.kind == DrinkKind.Tea ? "차" : "칵테일")} · {a?.name} {DrinkRules.Format(o.minA, a != null ? a.form : IngredientForm.Liquid)} 이상 + {b?.name} {DrinkRules.Format(o.minB, b != null ? b.form : IngredientForm.Liquid)} 이상";
        }
    }
}
