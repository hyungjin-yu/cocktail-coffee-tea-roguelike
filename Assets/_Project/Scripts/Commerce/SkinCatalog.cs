using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>치장 스킨 1종. 능력치·밸런스에는 전혀 관여하지 않는다(몸체 스프라이트만 교체).</summary>
    public class SkinDefinition
    {
        public string productId;
        public Job job;
        public string displayName;
        public string description;
        /// <summary>49: 기본 외형과 실제로 달라지는 색·복장(현재 스킨 PNG 기준). 아트가 바뀌면 함께 갱신한다.</summary>
        public string difference;
        /// <summary>79: true = 현금 결제 상품(스토어 등록·영수증 복원 대상, 기존 2종만). false = 판매 코인 전용(스토어에 등록하지 않는다).</summary>
        public bool paid;
        /// <summary>Resources/CosmeticSkins/&lt;productId&gt;/ — 파일 이름이 원본 몸체 프레임 이름과 같다.</summary>
        public string ResourceFolder => SkinCatalog.ResourceRoot + productId;
    }

    /// <summary>스킨 목록 + 장착 해석(이번 런 직업·스토어 소유·장착·아트 유무).</summary>
    public static class SkinCatalog
    {
        public const string ResourceRoot = "CosmeticSkins/";
        public const string BartenderMidnight = "skin_bartender_midnight";
        public const string TeaJade = "skin_tea_jade";

        public static readonly SkinDefinition[] All =
        {
            new SkinDefinition
            {
                productId = BartenderMidnight, job = Job.Bartender, displayName = "미드나잇 바텐더",
                description = "바텐더 전용 · 한밤의 바 색감 스킨\n외형만 바뀌며 능력치는 같습니다.",
                difference = "검정 조끼·바지 → 선명한 미드나잇 남색, 청록 넥타이 → 연한 샴페인 금빛.\n흰 셔츠·머리·얼굴은 기본과 같습니다.",
                paid = true,
            },
            new SkinDefinition
            {
                productId = TeaJade, job = Job.TeaSommelier, displayName = "비취 티 소믈리에",
                description = "티 소믈리에 전용 · 비취빛 찻잎 색감 스킨\n외형만 바뀌며 능력치는 같습니다.",
                difference = "갈색 조끼·앞치마·바지·신발이 비취색(녹색)으로 바뀝니다.\n흰 셔츠·머리·얼굴은 기본과 같습니다.",
                paid = true,
            },
            // 79: 신규 10종(바텐더 5·티 소믈리에 5) — 판매 코인 1000 전용, 현금 상품 아님. 아트(Aseprite·PixelLab 승인본)가 모든
            // 실제 사용 몸체 프레임을 덮을 때만 판매·적용한다(SkinArt.IsComplete).
            Coin(BartenderRail, Job.Bartender, "황동 야간열차", "각진 차장모·짧은 더블브레스트 재킷·시계 사슬·황동 단추, 짙은 적갈색"),
            Coin(BartenderTropic, Job.Bartender, "파도빛 휴양지", "짧은 밀짚모·넓게 열린 꽃무늬 셔츠·무릎바지·샌들, 코랄/청록"),
            Coin(BartenderNoir, Job.Bartender, "안개 골목 탐정", "낮은 중절모·무릎 위 트렌치코트·넓은 옷깃·부츠, 회갈색/버건디"),
            Coin(BartenderAlchemist, Job.Bartender, "탄산 연금술사", "이마 고글·비대칭 가죽 앞가리개·닫힌 작은 허리병·보호 소매, 구리/보라"),
            Coin(BartenderStage, Job.Bartender, "자정 무대 진행자", "짧은 실크햇·뒤로 갈라진 연미복·넓은 보타이·흰 장갑, 흑백/선명한 빨강"),
            Coin(TeaMountain, Job.TeaSommelier, "산길 찻잎 채집가", "천 두건·작은 등짐 바구니·조끼·각반, 흙빛/풀빛"),
            Coin(TeaCeremony, Job.TeaSommelier, "백자 다례가", "낮은 머리 묶음·넓은 소매의 랩 로브·긴 허리띠, 상아/먹색/작은 청화 무늬"),
            Coin(TeaApiary, Job.TeaSommelier, "꿀벌 정원지기", "짧고 둥근 망사 양봉모·하나로 연결된 작업복·손목 장갑, 크림/호박"),
            Coin(TeaGreenhouse, Job.TeaSommelier, "유리온실 원예가", "머리 스카프·포켓 달린 원예 앞치마·걷은 소매·큰 고무장화, 테라코타/연두"),
            Coin(TeaAstronomer, Job.TeaSommelier, "별자리 티 연구자", "높은 뾰족 모자·짧은 별무늬 어깨망토·겹친 튜닉·사각 안경, 남보라/은빛"),
        };

        public const string BartenderRail = "skin_bartender_rail", BartenderTropic = "skin_bartender_tropic", BartenderNoir = "skin_bartender_noir",
            BartenderAlchemist = "skin_bartender_alchemist", BartenderStage = "skin_bartender_stage";
        public const string TeaMountain = "skin_tea_mountain", TeaCeremony = "skin_tea_ceremony", TeaApiary = "skin_tea_apiary",
            TeaGreenhouse = "skin_tea_greenhouse", TeaAstronomer = "skin_tea_astronomer";
        /// <summary>79 신규 코인 스킨 1개 가격(판매 코인).</summary>
        public const int CoinSkinPrice = 1000;

        static SkinDefinition Coin(string id, Job job, string name, string look) => new SkinDefinition
        {
            productId = id, job = job, displayName = name, paid = false,
            description = (job == Job.Bartender ? "바텐더" : "티 소믈리에") + " 전용 · 판매 코인 " + CoinSkinPrice + "\n외형만 바뀌며 능력치는 같습니다.",
            difference = look + "\n모자·복장·실루엣이 바뀌고 능력치·판정은 같습니다.",
        };

        /// <summary>79: 스토어(IAP)에 등록·조회·복원하는 현금 상품 ID — paid 스킨만. 코인 전용 스킨은 절대 넣지 않는다.</summary>
        public static IReadOnlyList<string> PaidProductIds
        {
            get
            {
                var ids = new List<string>();
                foreach (var s in All) if (s.paid) ids.Add(s.productId);
                return ids;
            }
        }

        /// <summary>모든 캐릭터 스킨 ID(현금+코인). 스토어 등록에는 쓰지 않는다.</summary>
        public static IReadOnlyList<string> AllProductIds
        {
            get
            {
                var ids = new List<string>();
                foreach (var s in All) ids.Add(s.productId);
                return ids;
            }
        }

        public static SkinDefinition Find(string productId)
        {
            foreach (var s in All) if (s.productId == productId) return s;
            return null;
        }

        public static bool IsKnown(string productId) => Find(productId) != null;
        /// <summary>현금 결제 상품인지(스토어 구매·검증·복원 대상).</summary>
        public static bool IsPaid(string productId) => Find(productId)?.paid ?? false;

        /// <summary>
        /// 이번 런에 입힐 스킨(없으면 null = 기본 아트). 조건: 해당 직업 스킨이 장착돼 있고, 현재 스토어프런트에서 소유,
        /// 아트가 실제로 있음. 하나라도 빠지면 기본 아트.
        /// </summary>
        public static SkinDefinition ResolveEquipped(Job job, CommerceState state, string storefront, Func<SkinDefinition, bool> artExists)
        {
            if (state == null) return null;
            var id = state.EquippedFor(job);
            var def = Find(id);
            if (def == null || def.job != job) return null;
            if (!state.IsOwned(storefront, def.productId) && !DrinkLab.CoinOwns(def.productId)) return null; // 72 코인 소유도 게임 소유
            if (artExists != null && !artExists(def)) return null;
            return def;
        }
    }

    /// <summary>스킨 스프라이트 사전(원본 스프라이트 이름 → 스킨 스프라이트). Resources에서 한 번 읽어 캐시.</summary>
    public static class SkinArt
    {
        static readonly Dictionary<string, Dictionary<string, Sprite>> cache = new Dictionary<string, Dictionary<string, Sprite>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        /// <summary>스킨 프레임 사전. 아트가 없으면 빈 사전.</summary>
        public static Dictionary<string, Sprite> Sprites(SkinDefinition def)
        {
            if (def == null) return new Dictionary<string, Sprite>();
            if (cache.TryGetValue(def.productId, out var map) && map != null) return map;
            map = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var s in Resources.LoadAll<Sprite>(def.ResourceFolder))
                if (s != null && !map.ContainsKey(s.name)) map[s.name] = s;
            cache[def.productId] = map;
            return map;
        }

        /// <summary>런에서 입힐 수 있는지 = 실제 사용 몸체 프레임을 모두 덮음(79: 일부만 있으면 그 동작에서 기본 복장이 번쩍이므로 기본 아트 유지).</summary>
        public static bool Exists(SkinDefinition def) => IsComplete(def);

        public static bool IsComplete(SkinDefinition def) => def != null && MissingFrames(def).Count == 0 && Sprites(def).Count > 0;

        /// <summary>직업 몸체가 런에서 실제로 그리는 프레임 이름: 몸체 카탈로그의 모든 클립 + 티 소믈리에 검 휘두르기(재생 구간).
        /// 활 준비(bow_*) 합성 클립은 2026-10-13부터 재생하지 않아 제외.</summary>
        public static List<string> RequiredFrameNames(Job job)
        {
            var names = new List<string>();
            var art = ArtLibrary.Get(JobPreset.For(job).bodyArtId);
            if (art != null)
                foreach (var clip in art.clips)
                    if (clip != null)
                        foreach (var f in clip.frames)
                            if (f != null && !names.Contains(f.name)) names.Add(f.name);
            if (job == Job.TeaSommelier)
                foreach (var f in PlayerVisual.SwordSwingFrames())
                    if (f != null && !names.Contains(f.name)) names.Add(f.name);
            return names;
        }

        /// <summary>스킨 폴더에 없는 필수 프레임 이름(아트 없음이면 필수 전체).</summary>
        public static List<string> MissingFrames(SkinDefinition def)
        {
            var missing = new List<string>();
            if (def == null) return missing;
            var map = Sprites(def);
            foreach (var n in RequiredFrameNames(def.job))
                if (!map.TryGetValue(n, out var s) || s == null) missing.Add(n);
            return missing;
        }

        /// <summary>테스트 전용: 캐시 비우기.</summary>
        public static void ClearCacheForTests() => cache.Clear();

        /// <summary>직업 기본 대기 첫 프레임(미리보기 기준).</summary>
        public static Sprite BaseIdleSprite(Job job)
        {
            var art = ArtLibrary.Get(JobPreset.For(job).bodyArtId);
            var clip = art?.Clip("idle");
            return clip != null && clip.frames.Length > 0 ? clip.frames[0] : null;
        }

        /// <summary>미리보기: 기본 대기 첫 프레임의 스킨 버전. 없으면 null(호출부가 기본 아트 + "아트 준비 중").</summary>
        public static Sprite PreviewSprite(SkinDefinition def)
        {
            if (def == null) return null;
            var map = Sprites(def);
            var baseSprite = BaseIdleSprite(def.job);
            if (baseSprite != null && map.TryGetValue(baseSprite.name, out var s)) return s;
            // 기본 아트가 없는 환경 — 이름 규칙으로 찾는다.
            var fallbackName = JobPreset.For(def.job).bodyArtId + "_idle_right_00";
            return map.TryGetValue(fallbackName, out s) ? s : null;
        }
    }
}
