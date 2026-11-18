using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using T = CCT.Prototype.Coop2P.CoopTuning;

namespace CCT.Prototype.Coop2P.Tests
{
    /// <summary>로컬 2인 실험판 핵심 규칙(EditMode, 순수 C#). 위치·재고를 직접 놓는 fixture 검사이며 실제 플레이 검증과 구분한다.</summary>
    public class CoopSimTests
    {
        static readonly PlayerInput None = default;
        static PlayerInput Move(float x, float y) => new PlayerInput { Move = new Vector2(x, y) };
        static PlayerInput Use => new PlayerInput { Interact = true };
        static PlayerInput Sip => new PlayerInput { Drink = true };

        static CoopSim Quiet()
        {
            var s = new CoopSim { SpawningEnabled = false };
            return s;
        }

        static void Place(CoopSim s, int i, Vector2 pos)
        {
            s.Players[i].Pos = pos;
            s.Players[i].SnapRole();
        }

        static Vector2 AtStation(CoopDrink d) => T.StationStand(d);

        // ── 입력 분리 ──

        [Test]
        public void KeySets_AreDisjoint_AndAvoidSystemKeys()
        {
            var a = CoopInput.P1.All();
            var b = CoopInput.P2.All();
            Assert.That(a.Intersect(b), Is.Empty, "P1/P2 키 겹침");
            Assert.That(a.Intersect(CoopInput.SystemKeys), Is.Empty);
            Assert.That(b.Intersect(CoopInput.SystemKeys), Is.Empty);
            Assert.That(a.Distinct().Count(), Is.EqualTo(a.Length));
            Assert.That(b.Distinct().Count(), Is.EqualTo(b.Length));
        }

        [Test]
        public void SimultaneousMovement_EachInputMovesOnlyItsPlayer()
        {
            var s = Quiet();
            var a0 = s.Players[0].Pos; var b0 = s.Players[1].Pos;
            for (int i = 0; i < 20; i++) s.Tick(0.05f, Move(-1f, 0f), Move(1f, 0f));
            Assert.That(s.Players[0].Pos.x, Is.LessThan(a0.x - 2f));
            Assert.That(s.Players[1].Pos.x, Is.GreaterThan(b0.x + 2f));
            Assert.That(s.Players[0].Pos.y, Is.EqualTo(a0.y).Within(0.001f));
        }

        // ── 역할 경계 ──

        [Test]
        public void CrossingDoor_SwitchesRoleImmediately_BothWays()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, 1.6f));
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Brew));
            int frames = 0;
            while (s.Players[0].Role == CoopRole.Brew && frames++ < 60) s.Tick(0.02f, Move(0f, -1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Fight));
            Assert.That(s.Players[0].Pos.y, Is.LessThanOrEqualTo(T.BoundaryY));
            Assert.That(s.RoleSwaps, Is.EqualTo(1));
            while (s.Players[0].Role == CoopRole.Fight && frames++ < 200) s.Tick(0.02f, Move(0f, 1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Brew));
            Assert.That(s.RoleSwaps, Is.EqualTo(2));
        }

        [Test]
        public void BothPlayers_CanPassDoorInOppositeDirectionsAtOnce()
        {
            var s = Quiet();
            // 정면으로 같은 줄(거의 x 동일)에서 엇갈려도 막히지 않아야 한다.
            Place(s, 0, new Vector2(-0.02f, 2.2f));
            Place(s, 1, new Vector2(0.02f, -0.4f));
            for (int i = 0; i < 120; i++) s.Tick(0.025f, Move(0f, -1f), Move(0f, 1f));
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Fight), "P1 문 막힘");
            Assert.That(s.Players[1].Role, Is.EqualTo(CoopRole.Brew), "P2 문 막힘");
        }

        [Test]
        public void Counter_BlocksCrossingOutsideDoor()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(-5f, 2f));
            for (int i = 0; i < 80; i++) s.Tick(0.025f, Move(0f, -1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Brew));
            Assert.That(s.Players[0].Pos.y, Is.GreaterThanOrEqualTo(T.CounterMaxY + T.PlayerRadius - 0.001f));
        }

        [Test]
        public void BothPlayers_MayHoldSameRole()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(-3f, -2f));
            Place(s, 1, new Vector2(3f, -2f));
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Fight));
            Assert.That(s.Players[1].Role, Is.EqualTo(CoopRole.Fight));
            Place(s, 0, AtStation(CoopDrink.LeafTonic));
            Place(s, 1, AtStation(CoopDrink.CitrusBurst));
            Assert.That(s.Players.All(p => p.Role == CoopRole.Brew));
        }

        // ── 제조·재고 ──

        [Test]
        public void Brew_DeductsOnceOnConfirm_AndRejectsWhenShort()
        {
            var s = Quiet();
            Place(s, 1, AtStation(CoopDrink.RushShot));
            s.Stock[(int)Ingredient.Bean] = 1;
            Assert.That(s.Interact(1), Is.EqualTo(CoopAction.RejectedInsufficient));
            Assert.That(s.Stock[(int)Ingredient.Bean], Is.EqualTo(1));
            s.Stock[(int)Ingredient.Bean] = 3;
            Assert.That(s.Interact(1), Is.EqualTo(CoopAction.BrewStarted));
            Assert.That(s.Stock[(int)Ingredient.Bean], Is.EqualTo(1));
            for (int i = 0; i < 10; i++) s.Tick(0.05f, None, None);
            Assert.That(s.Stock[(int)Ingredient.Bean], Is.EqualTo(1), "제조 중 추가 차감 없음");
        }

        [Test]
        public void TwoStations_SameFrame_SharedStock_OnlyOneAffordableSucceeds()
        {
            var s = Quiet();
            Place(s, 0, AtStation(CoopDrink.RushShot));       // 원두 2
            Place(s, 1, AtStation(CoopDrink.CitrusBurst));    // 라임 2 + 원두 1
            s.Stock[(int)Ingredient.Bean] = 2;
            s.Stock[(int)Ingredient.Lime] = 2;
            s.Tick(0.016f, Use, Use);
            int started = s.Stations.Count(st => st.Brewing);
            Assert.That(started, Is.EqualTo(1), "공유 재고는 한 쪽만 감당");
            Assert.That(s.Stock.All(v => v >= 0), "음수 재고 없음");
            int beanSpent = 2 - s.Stock[(int)Ingredient.Bean];
            Assert.That(beanSpent == 2 || beanSpent == 1);
        }

        [Test]
        public void SameStation_SimultaneousPress_StartsOnce_OtherIsBusy_CancelOnlyByOwner()
        {
            var s = Quiet();
            var at = AtStation(CoopDrink.LeafTonic);
            Place(s, 0, at + new Vector2(-0.35f, 0f));
            Place(s, 1, at + new Vector2(0.35f, 0f));
            s.Stock[(int)Ingredient.Leaf] = 4;
            s.Tick(0.016f, Use, Use);
            var st = s.Stations[(int)CoopDrink.LeafTonic];
            Assert.IsTrue(st.Brewing);
            Assert.That(s.Stock[(int)Ingredient.Leaf], Is.EqualTo(2), "한 번만 차감");
            int other = 1 - st.Owner;
            Assert.That(s.Interact(other), Is.EqualTo(CoopAction.RejectedBusy));
            Assert.That(s.Interact(st.Owner), Is.EqualTo(CoopAction.BrewCancelled));
            Assert.That(s.Stock[(int)Ingredient.Leaf], Is.EqualTo(4), "취소 = 전액 환불");
            Assert.IsFalse(st.Brewing);
        }

        [Test]
        public void Brewing_PausesWhenUnattended_CompletesWhenAttended()
        {
            var s = Quiet();
            Place(s, 0, AtStation(CoopDrink.RushShot));
            s.Stock[(int)Ingredient.Bean] = 2;
            s.Interact(0);
            Place(s, 0, new Vector2(3f, 2f));
            for (int i = 0; i < 100; i++) s.Tick(0.05f, None, None);
            var st = s.Stations[(int)CoopDrink.RushShot];
            Assert.IsTrue(st.Brewing);
            Assert.That(st.Progress, Is.EqualTo(0f));
            Place(s, 1, AtStation(CoopDrink.RushShot)); // 동료가 대신 지켜도 진행
            for (int i = 0; i < 70; i++) s.Tick(0.05f, None, None);
            Assert.IsNotNull(st.Ready);
            Assert.That(s.CupsBrewed, Is.EqualTo(1));
        }

        // ── 잔 소유권 ──

        static CoopSim WithCupOnCounter()
        {
            var s = Quiet();
            Place(s, 0, AtStation(CoopDrink.RushShot));
            s.Stock[(int)Ingredient.Bean] = 2;
            s.Interact(0);
            for (int i = 0; i < 70; i++) s.Tick(0.05f, None, None);
            Assert.That(s.Interact(0), Is.EqualTo(CoopAction.TakenFromStation));
            Place(s, 0, T.PassCounterPos + new Vector2(0f, 0.85f));
            Assert.That(s.Interact(0), Is.EqualTo(CoopAction.Placed));
            return s;
        }

        [Test]
        public void Counter_SimultaneousTake_OnlyOnePlayerGetsTheCup()
        {
            var s = WithCupOnCounter();
            Place(s, 0, T.PassCounterPos + new Vector2(-0.4f, 0.85f));   // 주방 쪽
            Place(s, 1, T.PassCounterPos + new Vector2(0.4f, -0.85f));   // 홀 쪽
            Assert.That(s.LiveCupCount(), Is.EqualTo(1));
            s.Tick(0.016f, Use, Use);
            int holders = s.Players.Count(p => p.Held != null);
            Assert.That(holders, Is.EqualTo(1));
            Assert.That(s.LiveCupCount(), Is.EqualTo(1), "잔 복제 없음");
            Assert.That(s.Counter.All(c => c == null));
        }

        [Test]
        public void Counter_HasCapacity_AndRejectsWhenFull()
        {
            var s = Quiet();
            Place(s, 0, T.PassCounterPos + new Vector2(0f, 0.85f));
            for (int i = 0; i < 3; i++)
            {
                s.Players[0].Held = new CoopCup { Id = 100 + i, Drink = CoopDrink.LeafTonic, Holder = 0, Place = CupPlace.Hand };
                var r = s.Interact(0);
                Assert.That(r, Is.EqualTo(i < T.PassCounterCapacity ? CoopAction.Placed : CoopAction.RejectedCounterFull));
            }
            Assert.IsNotNull(s.Players[0].Held, "가득이면 손에 그대로");
        }

        [Test]
        public void Drink_AppliesOnlyToDrinker_AndCupIsConsumedOnce()
        {
            var s = WithCupOnCounter();
            Place(s, 1, T.PassCounterPos + new Vector2(0f, -0.85f));
            Assert.That(s.Interact(1), Is.EqualTo(CoopAction.TakenFromCounter));
            Assert.IsTrue(s.Drink(1));
            Assert.IsFalse(s.Drink(1), "같은 잔 두 번 마시기 불가");
            Assert.IsFalse(s.Drink(0));
            Assert.That(s.Players[1].RushTime, Is.EqualTo(T.RushSeconds));
            Assert.That(s.Players[0].RushTime, Is.EqualTo(0f));
            Assert.That(s.LiveCupCount(), Is.EqualTo(0));
            Assert.That(s.CupsDrunk, Is.EqualTo(1));
        }

        [Test]
        public void Give_HandsCupToEmptyHandedMate_Only()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(-3f, -2f));
            Place(s, 1, new Vector2(-2.2f, -2f));
            s.Players[0].Held = new CoopCup { Id = 1, Drink = CoopDrink.LeafTonic, Holder = 0, Place = CupPlace.Hand };
            s.Players[1].Held = new CoopCup { Id = 2, Drink = CoopDrink.RushShot, Holder = 1, Place = CupPlace.Hand };
            Assert.That(s.Interact(0), Is.EqualTo(CoopAction.HoldingNoTarget), "상대 손이 차 있으면 건네기 불가");
            s.Drink(1);
            Assert.That(s.Interact(0), Is.EqualTo(CoopAction.Given));
            Assert.IsNull(s.Players[0].Held);
            Assert.That(s.Players[1].Held.Holder, Is.EqualTo(1));
        }

        // ── 효과 ──

        [Test]
        public void Tonic_HealsOverTime_CappedAtMax()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(-3f, -2f));
            s.Players[0].Hp = 30f;
            s.Players[0].Held = new CoopCup { Drink = CoopDrink.LeafTonic, Holder = 0, Place = CupPlace.Hand };
            s.Drink(0);
            s.Tick(0.5f * 0.1f, None, None);
            Assert.That(s.Players[0].Hp, Is.LessThan(40f), "즉시 전량이 아니라 시간에 걸쳐 회복");
            for (int i = 0; i < 40; i++) s.Tick(0.1f, None, None);
            Assert.That(s.Players[0].Hp, Is.EqualTo(80f).Within(0.01f));
            Assert.That(s.Healed[0], Is.EqualTo(50f).Within(0.01f));
            s.Players[0].Hp = 90f;
            s.Players[0].Held = new CoopCup { Drink = CoopDrink.LeafTonic, Holder = 0, Place = CupPlace.Hand };
            s.Drink(0);
            for (int i = 0; i < 40; i++) s.Tick(0.1f, None, None);
            Assert.That(s.Players[0].Hp, Is.EqualTo(T.PlayerMaxHp));
            Assert.That(s.Players[0].HealPool, Is.EqualTo(0f));
        }

        [Test]
        public void Rush_RefreshesNotStacks_AndExpires_AndRaisesAttackRate()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, -2f));
            s.Players[0].Held = new CoopCup { Drink = CoopDrink.RushShot, Holder = 0, Place = CupPlace.Hand };
            s.Drink(0);
            for (int i = 0; i < 30; i++) s.Tick(0.1f, None, None);
            s.Players[0].Held = new CoopCup { Drink = CoopDrink.RushShot, Holder = 0, Place = CupPlace.Hand };
            s.Drink(0);
            Assert.That(s.Players[0].RushTime, Is.EqualTo(T.RushSeconds), "누적 없이 10초로 갱신");
            // 공격 횟수 비교: 고정 표적(체력 큼) 하나
            s.Enemies.Add(new CoopEnemy { Id = 1, Pos = new Vector2(2f, -2f), Hp = 9999f, MaxHp = 9999f, Stun = 999f });
            int h0 = s.Hits[0];
            for (int i = 0; i < 250; i++) s.Tick(0.02f, None, None); // 5초 러시
            int rushHits = s.Hits[0] - h0;
            for (int i = 0; i < 300; i++) s.Tick(0.02f, None, None); // 만료
            Assert.That(s.Players[0].RushTime, Is.EqualTo(0f));
            int h1 = s.Hits[0];
            for (int i = 0; i < 250; i++) s.Tick(0.02f, None, None); // 5초 기본
            int baseHits = s.Hits[0] - h1;
            Assert.That(rushHits, Is.GreaterThanOrEqualTo(baseHits * 2), $"rush {rushHits} vs base {baseHits}");
        }

        [Test]
        public void Burst_StunsAndPushesNearbyEnemies_AndShieldsDrinker()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, -2f));
            s.Enemies.Add(new CoopEnemy { Id = 1, Pos = new Vector2(1f, -2f), Hp = 100f, MaxHp = 100f });
            s.Enemies.Add(new CoopEnemy { Id = 2, Pos = new Vector2(7f, -2f), Hp = 100f, MaxHp = 100f });
            s.Players[0].Held = new CoopCup { Drink = CoopDrink.CitrusBurst, Holder = 0, Place = CupPlace.Hand };
            s.Drink(0);
            Assert.That(s.Enemies[0].Stun, Is.EqualTo(T.BurstStun));
            Assert.That(s.Enemies[0].Hp, Is.EqualTo(100f - T.BurstDamage));
            Assert.That(s.Enemies[1].Stun, Is.EqualTo(0f), "반경 밖 무영향");
            Assert.That(s.Players[0].InvulnTime, Is.EqualTo(T.BurstInvuln));
            float x0 = s.Enemies[0].Pos.x;
            s.Tick(0.1f, None, None);
            Assert.That(s.Enemies[0].Pos.x, Is.GreaterThan(x0 + 0.5f), "밀려남");
        }

        // ── 전투·지급·압박 ──

        [Test]
        public void Kills_GrantIngredientsInGuaranteedCycle_WithCap()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, -2f));
            for (int k = 0; k < 6; k++)
            {
                s.Enemies.Add(new CoopEnemy { Id = k, Pos = new Vector2(1f, -2f), Hp = 1f, MaxHp = 1f, Stun = 99f });
                s.Tick(0.6f * 0.1f, None, None);
                for (int i = 0; i < 12 && s.Enemies.Count > 0; i++) s.Tick(0.1f, None, None);
            }
            Assert.That(s.Kills, Is.EqualTo(6));
            Assert.That(s.Stock[(int)Ingredient.Bean], Is.EqualTo(2));
            Assert.That(s.Stock[(int)Ingredient.Leaf], Is.EqualTo(2));
            Assert.That(s.Stock[(int)Ingredient.Lime], Is.EqualTo(2));
        }

        [Test]
        public void BrewerDoesNotAutoAttack_FighterDoes()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(-6f, 2.2f));
            Place(s, 1, new Vector2(6f, -2f));
            s.Enemies.Add(new CoopEnemy { Id = 1, Pos = new Vector2(-6f, 2.2f + 1f), Hp = 999f, MaxHp = 999f, Stun = 99f });
            s.Enemies.Add(new CoopEnemy { Id = 2, Pos = new Vector2(7f, -2f), Hp = 999f, MaxHp = 999f, Stun = 99f });
            for (int i = 0; i < 20; i++) s.Tick(0.1f, None, None);
            Assert.That(s.Hits[0], Is.EqualTo(0));
            Assert.That(s.Hits[1], Is.GreaterThan(0));
        }

        // ── 2026-11-18 안전 주방 · 적 홀 경계 ──

        [Test]
        public void EmptyHall_EnemiesStayInHall_AndKitchenPlayersAreSafe()
        {
            var s = Quiet();
            Place(s, 0, AtStation(CoopDrink.LeafTonic));
            Place(s, 1, T.PassStandKitchen);
            var e = new CoopEnemy { Id = 1, Pos = new Vector2(6f, -3f), Hp = 30f, MaxHp = 30f };
            var e2 = new CoopEnemy { Id = 2, Pos = new Vector2(0f, -0.5f), Hp = 30f, MaxHp = 30f };
            s.Enemies.Add(e); s.Enemies.Add(e2);
            float maxY = float.MinValue;
            for (int i = 0; i < 1200; i++)
            {
                s.Tick(0.05f, None, None);
                foreach (var en in s.Enemies) maxY = Mathf.Max(maxY, en.Pos.y);
            }
            Assert.That(maxY, Is.LessThanOrEqualTo(T.EnemyMaxY + 1e-4f), "주방 침범 0");
            Assert.That(s.Players.All(p => p.Hp == T.PlayerMaxHp), "주방 인원 피격 0");
            Assert.That(s.Hits[0] + s.Hits[1], Is.EqualTo(0), "주방에서 자동 공격 없음");
        }

        [Test]
        public void Chase_Knockback_Separation_Spawn_AllClampedToHall()
        {
            var s = new CoopSim(); // 정상 스폰 켜짐
            Place(s, 0, new Vector2(0f, 0.6f)); // 문 통로(전투 역할)에 서서 적을 끌어당김
            Place(s, 1, AtStation(CoopDrink.RushShot));
            for (int k = 0; k < 20; k++) s.Enemies.Add(new CoopEnemy { Id = 100 + k, Pos = new Vector2(-0.4f + 0.04f * k, -0.2f), Hp = 999f, MaxHp = 999f });
            float maxY = float.MinValue;
            for (int i = 0; i < 400; i++)
            {
                if (i == 200)
                {
                    // 버스트 충격파: 아래에서 위로 밀어내도 주방으로 넘어가지 않음
                    Place(s, 0, new Vector2(0f, -1.6f));
                    s.Players[0].Held = new CoopCup { Drink = CoopDrink.CitrusBurst, Holder = 0, Place = CupPlace.Hand };
                    s.Drink(0);
                }
                s.Tick(0.05f, None, None);
                foreach (var en in s.Enemies) maxY = Mathf.Max(maxY, en.Pos.y);
            }
            Assert.That(maxY, Is.LessThanOrEqualTo(T.EnemyMaxY + 1e-4f));
            Assert.That(s.Players[1].Hp, Is.EqualTo(T.PlayerMaxHp));
        }

        [Test]
        public void Spawns_ComeFromDrawnEntrances()
        {
            var s = new CoopSim();
            Place(s, 0, new Vector2(0f, -2f));
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                s.Tick(0.05f, None, None);
                foreach (var e in s.Enemies)
                {
                    if (!seen.Add(e.Id)) continue;
                    float best = T.Entrances.Min(ent => Vector2.Distance(ent, e.Pos));
                    Assert.That(best, Is.LessThan(1.0f), $"입구 밖 스폰 {e.Pos}");
                    Assert.That(e.Pos.y, Is.LessThanOrEqualTo(T.EnemyMaxY + 1e-4f));
                }
            }
            Assert.That(seen.Count, Is.GreaterThan(3));
        }

        [Test]
        public void KitchenRolePlayer_InDoorPassage_IsNotTargetedOrHit()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, T.CounterMinY + 0.3f)); // 문 통로 안, 홀 끝 적과 접촉 거리
            s.Players[0].RoleState = CoopRole.Brew; // 주방에서 나오는 중(문 통로, 아직 제조 역할)
            s.Players[0].LastRole = CoopRole.Brew;
            Place(s, 1, AtStation(CoopDrink.LeafTonic));
            s.Enemies.Add(new CoopEnemy { Id = 1, Pos = new Vector2(0f, T.EnemyMaxY), Hp = 999f, MaxHp = 999f });
            for (int i = 0; i < 40; i++) s.Tick(0.05f, None, None);
            Assert.That(s.Players[0].Hp, Is.EqualTo(T.PlayerMaxHp));
        }

        [Test]
        public void Role_DoesNotFlicker_InsideDoorPassage()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, -0.4f));
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Fight));
            while (s.Players[0].Pos.y < 1.0f) s.Tick(0.02f, Move(0f, 1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Fight), "통로 가운데를 지나도 아직 전투(카운터 뒤 바닥 전)");
            for (int k = 0; k < 6; k++) { s.Tick(0.02f, Move(0f, -1f), None); s.Tick(0.02f, Move(0f, 1f), None); }
            Assert.That(s.RoleSwaps, Is.EqualTo(0), "통로 안 왕복으로 역할이 흔들리지 않음");
            while (s.Players[0].Pos.y < T.RoleBrewY + 0.01f) s.Tick(0.02f, Move(0f, 1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Brew));
            while (s.Players[0].Pos.y > 1.0f) s.Tick(0.02f, Move(0f, -1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Brew), "주방에서 통로로 내려와도 홀 바닥 전까지 제조");
            Assert.That(s.RoleSwaps, Is.EqualTo(1));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void DoorCorner_PressingUpNearJamb_SlidesIntoDoor(float side)
        {
            var s = Quiet();
            Place(s, 0, new Vector2(side * (T.DoorHalfWidth + 0.25f), -0.3f)); // 문설주 바로 아래(카운터 밑)
            for (int i = 0; i < 60; i++) s.Tick(0.025f, Move(0f, 1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Brew), $"문 모서리에 끼임: {s.Players[0].Pos}");
            Place(s, 0, new Vector2(side * (T.DoorHalfWidth + 0.25f), T.CounterMaxY + 0.4f));
            for (int i = 0; i < 60; i++) s.Tick(0.025f, Move(0f, -1f), None);
            Assert.That(s.Players[0].Role, Is.EqualTo(CoopRole.Fight), $"주방 쪽 문 모서리에 끼임: {s.Players[0].Pos}");
        }

        // ── 전투 진행 시간 ──

        [Test]
        public void CombatClock_StopsWhenNobodyStandsInHall_OtherSystemsContinue()
        {
            var s = new CoopSim();
            Place(s, 0, AtStation(CoopDrink.RushShot));
            Place(s, 1, T.PassStandKitchen);
            s.Stock[(int)Ingredient.Bean] = 2;
            s.Interact(0);
            s.Players[1].RushTime = 5f;
            for (int i = 0; i < 100; i++) s.Tick(0.1f, None, None); // 10초 둘 다 주방
            Assert.That(s.CombatElapsed, Is.EqualTo(0f));
            Assert.That(s.Elapsed, Is.EqualTo(10f).Within(0.01f));
            Assert.IsFalse(s.HallActive);
            Assert.That(s.Stage, Is.EqualTo(0));
            Assert.IsNotNull(s.Stations[(int)CoopDrink.RushShot].Ready, "제조는 계속");
            Assert.That(s.Players[1].RushTime, Is.EqualTo(0f), "효과 시간은 계속 흐름");
            Assert.That(s.Enemies.Count, Is.GreaterThan(0), "적 스폰(상한 안)은 계속");
            var before = s.Enemies.Select(e => e.Pos).ToArray();
            for (int i = 0; i < 20; i++) s.Tick(0.1f, None, None);
            Assert.That(s.Enemies.Take(before.Length).Select(e => e.Pos).ToArray(), Is.Not.EqualTo(before), "홀 적 움직임 계속");
            Assert.That(s.Enemies.Count, Is.LessThanOrEqualTo(T.MaxAlive[0]));

            Place(s, 1, new Vector2(4f, -2.5f)); // 한 명 홀로
            s.Players[1].InvulnTime = 10f; // 시험 고정: 5초 측정 중 쓰러짐 배제
            for (int i = 0; i < 50; i++) s.Tick(0.1f, None, None);
            Assert.That(s.CombatElapsed, Is.GreaterThan(4.9f));
            Assert.IsTrue(s.HallActive || s.Players[1].Downed);
        }

        [Test]
        public void BothInKitchen_NeverWinsByWaiting()
        {
            var s = Quiet();
            Place(s, 0, AtStation(CoopDrink.LeafTonic));
            Place(s, 1, AtStation(CoopDrink.CitrusBurst));
            for (int i = 0; i < 4000; i++) s.Tick(0.1f, None, None);
            Assert.That(s.Outcome, Is.EqualTo(CoopOutcome.Running));
            Assert.That(s.CombatElapsed, Is.EqualTo(0f));
            Assert.That(s.Remaining, Is.EqualTo(T.RunSeconds));
        }

        [Test]
        public void SpawnCap_RespectedPerStage()
        {
            var s = new CoopSim();
            Place(s, 0, AtStation(CoopDrink.LeafTonic));
            Place(s, 1, AtStation(CoopDrink.RushShot));
            for (int i = 0; i < 600; i++)
            {
                s.Tick(0.1f, None, None);
                Assert.That(s.Enemies.Count, Is.LessThanOrEqualTo(T.MaxAlive[s.Stage]));
                if (s.Outcome != CoopOutcome.Running) break;
            }
        }

        // ── 쓰러짐·결과·재시작·일시정지 ──

        [Test]
        public void Downed_RevivedByMateProximity_OrAutoAfterDelay()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(-3f, -2f));
            Place(s, 1, new Vector2(3f, -2f));
            s.Players[0].Hp = 0.01f;
            s.Enemies.Add(new CoopEnemy { Id = 1, Pos = new Vector2(-3f, -2f), Hp = 999f, MaxHp = 999f });
            s.Tick(0.05f, None, None);
            Assert.IsTrue(s.Players[0].Downed);
            s.Enemies.Clear();
            Place(s, 1, new Vector2(-2.2f, -2f));
            for (int i = 0; i < 21; i++) s.Tick(0.1f, None, None);
            Assert.IsFalse(s.Players[0].Downed);
            Assert.That(s.Players[0].Hp, Is.EqualTo(T.ReviveHp));

            s.Players[0].Hp = 0.01f;
            Place(s, 1, new Vector2(6f, -2f));
            s.Enemies.Add(new CoopEnemy { Id = 2, Pos = s.Players[0].Pos, Hp = 999f, MaxHp = 999f });
            s.Players[0].InvulnTime = 0f;
            s.Tick(0.05f, None, None);
            Assert.IsTrue(s.Players[0].Downed);
            s.Enemies.Clear();
            for (int i = 0; i < 125; i++) s.Tick(0.1f, None, None);
            Assert.IsFalse(s.Players[0].Downed, "12초 뒤 자력 회복");
            Assert.That(s.Players[0].Hp, Is.EqualTo(T.AutoReviveHp));
            Assert.That(s.Outcome, Is.EqualTo(CoopOutcome.Running));
        }

        [Test]
        public void BothDowned_Fails_AndInputIsIgnoredAfterwards()
        {
            var s = Quiet();
            foreach (var p in s.Players) { p.Hp = 0.01f; }
            Place(s, 0, new Vector2(-3f, -2f));
            Place(s, 1, new Vector2(3f, -2f));
            s.Enemies.Add(new CoopEnemy { Id = 1, Pos = new Vector2(-3f, -2f), Hp = 999f, MaxHp = 999f });
            s.Enemies.Add(new CoopEnemy { Id = 2, Pos = new Vector2(3f, -2f), Hp = 999f, MaxHp = 999f });
            s.Tick(0.05f, None, None);
            Assert.That(s.Outcome, Is.EqualTo(CoopOutcome.Failed));
            float t = s.Elapsed;
            s.Tick(1f, Move(1f, 0f), Use);
            Assert.That(s.Elapsed, Is.EqualTo(t));
        }

        [Test]
        public void SurvivingFiveMinutes_Succeeds()
        {
            var s = Quiet();
            Place(s, 0, new Vector2(0f, -2f)); // 홀에 서 있는 사람이 있어야 전투 시간이 흐른다
            for (int i = 0; i < 3001 && s.Outcome == CoopOutcome.Running; i++) s.Tick(0.1f, None, None);
            Assert.That(s.Outcome, Is.EqualTo(CoopOutcome.Success));
            Assert.That(s.CombatElapsed, Is.GreaterThanOrEqualTo(T.RunSeconds));
        }

        [Test]
        public void Pause_FreezesTimeBrewEffectsEnemiesAndPlayers()
        {
            var s = new CoopSim();
            Place(s, 0, AtStation(CoopDrink.RushShot));
            s.Stock[(int)Ingredient.Bean] = 2;
            s.Interact(0);
            s.Players[1].RushTime = 5f;
            for (int i = 0; i < 20; i++) s.Tick(0.1f, None, None);
            var st = s.Stations[(int)CoopDrink.RushShot];
            s.Paused = true;
            float el = s.Elapsed, prog = st.Progress, rush = s.Players[1].RushTime;
            var p1 = s.Players[0].Pos;
            var enemies = s.Enemies.Select(e => e.Pos).ToArray();
            for (int i = 0; i < 50; i++) s.Tick(0.1f, Move(1f, 0f), Use);
            Assert.That(s.Elapsed, Is.EqualTo(el));
            Assert.That(st.Progress, Is.EqualTo(prog));
            Assert.That(s.Players[1].RushTime, Is.EqualTo(rush));
            Assert.That(s.Players[0].Pos, Is.EqualTo(p1));
            Assert.That(s.Enemies.Select(e => e.Pos).ToArray(), Is.EqualTo(enemies));
            s.Paused = false;
            s.Tick(0.1f, None, None);
            Assert.That(s.Elapsed, Is.GreaterThan(el));
        }

        [Test]
        public void Restart_ResetsOnlyRunState()
        {
            var s = WithCupOnCounter();
            s.Stock[0] = 3;
            for (int i = 0; i < 100; i++) s.Tick(0.1f, None, None);
            s.Reset();
            Assert.That(s.Elapsed, Is.EqualTo(0f));
            Assert.That(s.Stock.Sum(), Is.EqualTo(0));
            Assert.That(s.LiveCupCount(), Is.EqualTo(0));
            Assert.That(s.Enemies, Is.Empty);
            Assert.That(s.Outcome, Is.EqualTo(CoopOutcome.Running));
            Assert.That(s.Players[0].Pos, Is.EqualTo(T.P1Start));
            Assert.That(s.Players.All(p => p.Hp == T.PlayerMaxHp && !p.Downed && p.Held == null));
        }

        [Test]
        public void NormalStart_HasNoFixtureStockOrInvulnerability()
        {
            var s = new CoopSim();
            Assert.That(s.Stock.Sum(), Is.EqualTo(0));
            Assert.IsTrue(s.SpawningEnabled);
            Assert.That(s.Players.All(p => p.InvulnTime == 0f && p.Hp == T.PlayerMaxHp));
        }

        [Test]
        public void DrinkEffects_AreMechanicallyDistinct()
        {
            // 이름·색만 다른 복제가 아님: 회복(체력) / 공격 지원(공격 간격·사거리·이속, 시간제) / 위기 대응(즉발 범위 기절+짧은 보호)
            var recipes = T.Recipes;
            Assert.That(recipes.Select(r => r.Drink).Distinct().Count(), Is.EqualTo(3));
            Assert.That(recipes.Select(r => (r.Lime, r.Bean, r.Leaf)).Distinct().Count(), Is.EqualTo(3), "배합 서로 다름");
        }
    }
}
