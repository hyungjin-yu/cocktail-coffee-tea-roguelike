using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 72 §4 장착 숙련 특성의 런 중 효과(출발 때 장착한 것만, 런 시작 스냅샷). 플레이어에 붙어 런과 함께 사라진다.
    /// 피해 보너스는 히든의 직접 타격 보너스와 같은 합산 자리, 치명타 확률은 같은 ×2 판정에 더한다. 이동·공격 속도·피해 감소는
    /// PlayerStats의 기존 상한에 합산된다. 타이머는 게임 시간(Playing 중만 흐름, 일시정지·카드 창의 timeScale 0에서 멈춤).
    /// 반사·히든 추가타·장판 같은 2차 피해는 직접 타격 훅을 부르지 않으므로 횟수·발동을 재귀 생성하지 않는다.
    /// </summary>
    public sealed class MasteryRuntime : MonoBehaviour
    {
        public const float OpeningSeconds = 30f, RegularWindow = 2f, CalmSeconds = 8f, LastSipSeconds = 4f, SignatureSeconds = 30f;

        PlayerController player;
        readonly HashSet<string> traits = new HashSet<string>();
        float runTime, noHitTime, lastSipTimer, signatureTimer, regularTimer;
        bool lastSipUsed, signatureUsed;
        Enemy regular;
        int regularHits;
        readonly HashSet<int> healedBosses = new HashSet<int>();

        public IReadOnlyCollection<string> Traits => traits;
        public bool Has(string id) => traits.Contains(id);
        public float RunTime => runTime;
        public bool SignatureActive => signatureTimer > 0f;
        public bool LastSipActive => lastSipTimer > 0f;
        /// <summary>최종 보스 생존 여부(서비스4). GameManager가 보스 등장/처치 때 바꾼다.</summary>
        public bool FinalBossAlive { get; set; }
        /// <summary>기교3 판정용: 이번 런 칵테일·차 카드 고유 종류 수(GameManager가 카드 획득 때 갱신).</summary>
        public int DistinctDrinkCards { get; set; }

        public static MasteryRuntime Attach(PlayerController player, IEnumerable<string> equipped)
        {
            var rt = player.gameObject.AddComponent<MasteryRuntime>();
            rt.player = player;
            if (equipped != null) foreach (var id in equipped) if (DrinkResearch.Trait(id) != null) rt.traits.Add(id);
            return rt;
        }

        void Update()
        {
            if (player == null || player.IsDead || traits.Count == 0) return;
            float dt = Time.deltaTime; // timeScale 0(일시정지·카드 선택)에서는 흐르지 않는다
            if (dt <= 0f) return;
            runTime += dt;
            noHitTime += dt;
            if (regularTimer > 0f) { regularTimer -= dt; if (regularTimer <= 0f) { regular = null; regularHits = 0; } }
            if (lastSipTimer > 0f) lastSipTimer -= dt;
            if (signatureTimer > 0f) signatureTimer -= dt;
            if (Has("stability_2") && !lastSipUsed && player.Hp <= player.MaxHp * 0.25f && player.Hp > 0f)
            {
                lastSipUsed = true;
                lastSipTimer = LastSipSeconds;
            }
            var gm = GameManager.Instance;
            if (gm != null)
            {
                var boss = gm.ActiveBoss;
                FinalBossAlive = boss != null && !boss.IsDead && boss.Def != null && boss.Def.boss == BossKind.Final;
                if (gm.Cards != null) DistinctDrinkCards = gm.Cards.CountKinds(CardCategory.Cocktail) + gm.Cards.CountKinds(CardCategory.Tea);
            }
            var s = player.Stats;
            if (s == null) return;
            s.masteryMoveSpeed = (Has("stability_1") && noHitTime >= CalmSeconds ? 0.04f : 0f) + (Has("stability_4") && player.Hp <= player.MaxHp * 0.30f ? 0.06f : 0f);
            s.masteryAttackSpeed = Has("service_4") && FinalBossAlive ? 0.05f : 0f;
            s.masteryDamageReduction = lastSipTimer > 0f ? 0.10f : 0f;
        }

        /// <summary>직접 타격 보너스(합산 비율). 타격 시점의 거리·적 체력으로 판정하고, 단골 응대 적중 수를 센다.</summary>
        public float DirectBonus(Enemy enemy, float distance)
        {
            if (enemy == null || traits.Count == 0) return 0f;
            float b = 0f;
            if (Has("service_1") && runTime < OpeningSeconds) b += 0.08f;
            if (Has("service_2"))
            {
                if (enemy == regular && regularTimer > 0f) regularHits++;
                else { regular = enemy; regularHits = 1; }
                regularTimer = RegularWindow;
                if (regularHits >= 3) { b += 0.15f; regularHits = 0; }
            }
            if (Has("service_3") && signatureTimer > 0f) b += 0.10f;
            if (Has("finesse_1") && enemy.Hp >= enemy.MaxHp - 0.001f) b += 0.08f;
            if (Has("finesse_2") && player.Stats != null && distance >= player.Stats.Range * 0.7f) b += 0.05f;
            return b;
        }

        /// <summary>추가 치명타 확률(기교3).</summary>
        public float CritBonus => Has("finesse_3") && DistinctDrinkCards >= 4 ? 0.01f : 0f;

        /// <summary>피격(실제 HP 감소): 여유로운 서비스 해제.</summary>
        public void OnDamaged() => noHitTime = 0f;

        /// <summary>목표 조합(히든) 완성: 시그니처 준비 1회 30초.</summary>
        public void OnSignatureCompleted()
        {
            if (!Has("service_3") || signatureUsed) return;
            signatureUsed = true;
            signatureTimer = SignatureSeconds;
        }

        /// <summary>보스 처치(보스별 1회): 쉬는 시간 최대 체력 3% 회복(초과 없음, 사망 후 부활 없음).</summary>
        public void OnBossKilled(int bossIndex)
        {
            if (!Has("stability_3") || player == null || player.IsDead || !healedBosses.Add(bossIndex)) return;
            player.Heal(0.03f);
        }

        public void ForceRunTimeForTests(float t) => runTime = t;
    }
}
