using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-09 범위 2: 지금 실제로 켜진 효과만 캐릭터 주변의 절제된 표식으로 보인다(새 아트 없음 — 기존 원 도형·water_drop·FadeVfx).
    /// - 준비 음료: 발밑 납작한 음료색 원(이번 판 내내). 환불 봉인으로 % 보너스가 무효인 동안은 회색으로 옅어진다.
    /// - 핸드 드립 붓기(사거리 +%): 주 무기의 실제 사거리(레이피어/검 베기 반지름, 원거리 조준 사거리 — 붓기 +% 포함) 점선 고리.
    /// - 핸드 드립 뜸(이동 속도 +%): 움직일 때 발밑에 작은 물방울(water_drop).
    /// - 에스프레소 샷(공격 속도 +%): 머리 위 작은 김 퍼프. 모카포트: 증기압이 거의 찼을 때(최대-3 이상)만 김 퍼프.
    /// - 얼음 찻잔 보호막 준비: 몸 둘레 하늘색 점선 고리(다음 단발 피해 1회 차단).
    /// 표식은 플레이어 자식 렌더러 하나씩(재사용)이고 몸체·무기·피격 색·공유 머티리얼을 건드리지 않는다. 일회성 퍼프/물방울은
    /// 간격 + 동시 개수 상한(MaxPuffs)으로 제한하고, 효과 설정 "낮음"이면 만들지 않는다(정보 표식인 원·고리는 유지). 게임 시간 기준.
    /// </summary>
    public class PlayerEffectMarkers : MonoBehaviour
    {
        public const int RangeDots = 20, BarrierDots = 10, MaxPuffs = 6;
        public const float PuffInterval = 0.35f, DropInterval = 0.22f, AuraAlpha = 0.24f;

        PlayerController player;
        GameManager game;
        SpriteRenderer aura;
        Transform rangeRing, barrierRing;
        readonly List<SpriteRenderer> rangeDots = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> barrierDots = new List<SpriteRenderer>();
        readonly List<FadeVfx> puffs = new List<FadeVfx>();
        float puffTimer, dropTimer;
        Vector2 feet = new Vector2(0f, -PlayerController.Radius);

        public bool AuraVisible => aura != null && aura.enabled;
        public Color AuraColor => aura != null ? aura.color : Color.clear;
        public bool RangeRingVisible => rangeRing != null && rangeRing.gameObject.activeSelf;
        public float RangeRingRadius { get; private set; }
        public bool BarrierRingVisible => barrierRing != null && barrierRing.gameObject.activeSelf;
        public int LivePuffs { get { puffs.RemoveAll(p => p == null); return puffs.Count; } }
        public int PuffsSpawned { get; private set; }

        public static PlayerEffectMarkers Attach(PlayerController player, GameManager game)
        {
            if (player == null) return null;
            var m = player.GetComponent<PlayerEffectMarkers>() ?? player.gameObject.AddComponent<PlayerEffectMarkers>();
            m.player = player;
            m.game = game;
            m.Build();
            return m;
        }

        void Build()
        {
            if (aura != null) return;
            var go = new GameObject("DrinkAura");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = feet;
            go.transform.localScale = new Vector3(1.25f, 0.42f, 1f);
            aura = go.AddComponent<SpriteRenderer>();
            aura.sprite = SpriteFactory.Circle;
            aura.sortingOrder = SortOrders.Shadow; // 그림자 높이 — 바닥 위험 장판(GroundZone)·적보다 아래
            aura.enabled = false;
            rangeRing = Ring("RangeRing", RangeDots, 0.09f, rangeDots, SortOrders.Shadow);
            barrierRing = Ring("BarrierRing", BarrierDots, 0.07f, barrierDots, SortOrders.AirVfx);
            barrierRing.localPosition = new Vector3(0f, 0.15f, 0f);
            for (int i = 0; i < barrierDots.Count; i++)
            {
                float a = i * Mathf.PI * 2f / BarrierDots;
                barrierDots[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.62f, 0f);
                barrierDots[i].color = new Color(0.65f, 0.95f, 1f, 0.7f);
            }
        }

        Transform Ring(string name, int count, float dot, List<SpriteRenderer> into, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(player.transform, false);
            for (int i = 0; i < count; i++)
            {
                var d = new GameObject("Dot" + i);
                d.transform.SetParent(root, false);
                d.transform.localScale = Vector3.one * dot;
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.sortingOrder = order;
                into.Add(sr);
            }
            root.gameObject.SetActive(false);
            return root;
        }

        void Update()
        {
            if (player == null || game == null) return;
            float dt = Time.deltaTime;
            bool live = !player.IsDead && game.State == GameState.Playing;
            Tick(live ? dt : 0f, live);
        }

        /// <summary>표식 갱신. dt = 게임 시간(0이면 퍼프 타이머 정지).</summary>
        public void Tick(float dt, bool live = true)
        {
            var stats = player.Stats;
            bool suppressed = stats != null && stats.BonusesSuppressed;

            // 준비 음료 발밑 원
            var drink = live && game.DrinkBuff != null ? DrinkLab.DrinkInRun(game.RunId) : null;
            aura.enabled = drink != null;
            if (drink != null)
            {
                var c = DrinkCalculator.Look(drink.recipe).color;
                aura.color = suppressed ? new Color(0.6f, 0.6f, 0.6f, AuraAlpha * 0.5f) : new Color(c.r, c.g, c.b, AuraAlpha);
            }

            var coffee = live ? player.Coffee : null;
            bool pouring = coffee != null && coffee.Method == CoffeeBrewMethod.PourOver && !coffee.Blooming && !suppressed;
            if (rangeRing.gameObject.activeSelf != pouring) rangeRing.gameObject.SetActive(pouring);
            if (pouring && stats != null)
            {
                // 주 무기의 실제 사거리 그대로(판정 원은 2D 원). 무기를 못 찾으면 PlayerStats.Range.
                float r = WeaponReach(player);
                RangeRingRadius = r;
                for (int i = 0; i < rangeDots.Count; i++)
                {
                    float a = i * Mathf.PI * 2f / RangeDots;
                    rangeDots[i].transform.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                    rangeDots[i].color = new Color(1f, 0.85f, 0.55f, 0.42f);
                }
            }

            var hidden = live ? player.HiddenRuntime : null;
            bool barrier = hidden != null && player.Hidden.Has(HiddenSkill.IcedCup) && hidden.BarrierReady;
            if (barrierRing.gameObject.activeSelf != barrier) barrierRing.gameObject.SetActive(barrier);

            if (dt <= 0f || HitFeedback.LowEffects) return;
            bool steam = coffee != null && !suppressed &&
                ((coffee.Method == CoffeeBrewMethod.EspressoMachine && coffee.ShotActive) ||
                 (coffee.Method == CoffeeBrewMethod.MokaPot && coffee.Heat >= CoffeeBrewRuntime.MokaHeatMax - 3));
            if (steam)
            {
                puffTimer -= dt;
                if (puffTimer <= 0f) { puffTimer = PuffInterval; Puff(); }
            }
            else puffTimer = 0f;
            bool drops = coffee != null && !suppressed && coffee.Method == CoffeeBrewMethod.PourOver && coffee.Blooming && player.IsMovingInput;
            if (drops)
            {
                dropTimer -= dt;
                if (dropTimer <= 0f) { dropTimer = DropInterval; Drop(); }
            }
            else dropTimer = 0f;
        }

        /// <summary>주 무기 사거리: 레이피어/검 베기 반지름 또는 원거리 조준 사거리(둘 다 PlayerStats.Range 기반, 붓기 +% 반영).</summary>
        public static float WeaponReach(PlayerController p)
        {
            var melee = p.GetComponentInChildren<RapierWeapon>();
            if (melee != null && melee.enabled) return melee.CurrentReach;
            var ranged = p.GetComponentInChildren<PlayerProjectileWeapon>();
            if (ranged != null && ranged.enabled) return ranged.CurrentReach;
            return p.Stats != null ? p.Stats.Range : 0f;
        }

        bool Reserve()
        {
            puffs.RemoveAll(p => p == null);
            return puffs.Count < MaxPuffs;
        }

        void Puff()
        {
            if (!Reserve()) return;
            Vector2 at = player.Position + new Vector2(Random.Range(-0.18f, 0.18f), 0.95f);
            var fx = FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * 0.12f, Vector2.one * 0.3f, new Color(1f, 1f, 1f, 0.45f), 0.5f, SortOrders.AirVfx);
            fx.transform.SetParent(player.transform.parent, true);
            fx.gameObject.AddComponent<Drift>().velocity = new Vector2(0f, 0.8f);
            puffs.Add(fx);
            PuffsSpawned++;
        }

        void Drop()
        {
            if (!Reserve()) return;
            Vector2 at = player.Position + feet + new Vector2(Random.Range(-0.15f, 0.15f), 0.02f);
            var fx = ArtVfx.Spawn("water_drop", at, 0f, 0.16f, 0.4f, new Color(0.75f, 0.9f, 1f, 0.8f), SortOrders.Shadow, 0.8f)
                     ?? FadeVfx.Spawn(SpriteFactory.Circle, at, 0f, Vector2.one * 0.1f, Vector2.one * 0.06f, new Color(0.75f, 0.9f, 1f, 0.7f), 0.4f, SortOrders.Shadow);
            fx.transform.SetParent(player.transform.parent, true);
            puffs.Add(fx);
            PuffsSpawned++;
        }

        sealed class Drift : MonoBehaviour
        {
            public Vector2 velocity;
            void Update() => transform.position += (Vector3)(velocity * Time.deltaTime);
        }
    }
}
