using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 10 §1 G07·G08 중 기존 테스트가 없던 조건만 실제 런타임 동작으로 검증한다(2026-10-08).
    /// G07: 60/90/120초 경계 가중치(시드 고정 전 구간 스윕), 실제 스포너의 시작 간격·보스 중 2배·전체 100 상한.
    /// G08: 레몬·티백·케틀·잎 감시자·다도 수호자·에스프레소 벅·밀크 포머 각각 강제 스폰 후 이동/투척/탄막/소환 행동.
    /// </summary>
    public class SpecSpawnAndEnemyTests
    {
        sealed class Fixed : IRandomSource
        {
            readonly Queue<float> q;
            public Fixed(params float[] v) { q = new Queue<float>(v); }
            public float Value() => q.Count > 0 ? q.Dequeue() : 0f;
        }

        static HashSet<EnemyDef> Sweep(float t, float trackRoll)
        {
            var seen = new HashSet<EnemyDef>();
            for (int i = 0; i < 1000; i++) seen.Add(EnemyRoster.PickCommon(t, new Fixed(trackRoll, i / 1000f)));
            return seen;
        }

        [TestCase(0.75f)] // 칵테일 트랙
        [TestCase(0.25f)] // 차 트랙
        public void ArchetypeBoundaries_60_90_120_AreExact(float trackRoll)
        {
            bool tea = trackRoll < 0.5f;
            var suicide = tea ? EnemyDef.SugarCubeBomber : EnemyDef.OliveBomber;
            var summoner = tea ? EnemyDef.LeafWarden : EnemyDef.SwirlConjuror;
            var elite = tea ? EnemyDef.TeaCeremonyGuardian : EnemyDef.MartiniSpirit;
            Assert.IsFalse(Sweep(59.99f, trackRoll).Contains(suicide), "60초 전 자폭형 없음");
            Assert.IsTrue(Sweep(60f, trackRoll).Contains(suicide), "60초부터 자폭형");
            Assert.IsFalse(Sweep(89.99f, trackRoll).Contains(summoner), "90초 전 소환형 없음");
            Assert.IsTrue(Sweep(90f, trackRoll).Contains(summoner), "90초부터 소환형");
            Assert.IsFalse(Sweep(119.99f, trackRoll).Contains(elite), "120초 전 엘리트 없음");
            Assert.IsTrue(Sweep(120f, trackRoll).Contains(elite), "120초부터 엘리트");
            var at120 = Sweep(120f, trackRoll);
            Assert.IsFalse(at120.Contains(tea ? EnemyDef.OliveBomber : EnemyDef.SugarCubeBomber), "150초 전 트랙 섞임 없음");
        }

        // ── 실제 스포너 ─────────────────────────────────────────────

        static GameManager BuildQuietGame()
        {
            var game = GameBootstrap.Build(Job.Bartender, WeaponKind.Gun);
            foreach (var mb in game.Player.GetComponents<MonoBehaviour>())
                if (mb is PlayerProjectileWeapon) Object.Destroy(mb);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            if (game.Director != null) game.Director.enabled = false;
            return game;
        }

        /// <summary>일반 스폰 주기만 센다. 2026-10-09: 예전에는 새로 나타난 모든 적을 세어 t=900에 뽑힌 소환형의 소환(시드마다 다름)까지
        /// 포함돼 간헐 실패했다 — 같은 기대 범위로 스포너가 직접 만든 수만 비교한다.</summary>
        static IEnumerator CountSpawns(float seconds, List<int> result)
        {
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            int start = spawner.RegularSpawns;
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
            result.Add(spawner.RegularSpawns - start);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spawner_FirstAttemptAtZero_IntervalShrinks_DoublesWithBoss_AndStopsAt100()
        {
            var game = BuildQuietGame();
            yield return null;
            Assert.GreaterOrEqual(Enemy.Active.Count, 1, "런 시간 0에 첫 스폰 시도");

            // t≈0: 간격 1.2초 → 6초 동안 약 5회(웨이브 14초 전)
            var counts = new List<int>();
            yield return CountSpawns(6f, counts);
            Assert.That(counts[0], Is.InRange(4, 6), "시작 간격 1.2초");

            // t=900: 기본 0.35초 / 시간 2.5 / 승인된 7분 이후 ×2 → 3초 동안 약43회.
            game.SetRunTimeForTests(900f);
            yield return new WaitForSeconds(1.3f); // 이전 1.2초 주기의 남은 타이머를 소진
            yield return CountSpawns(3f, counts);
            Assert.That(counts[1], Is.InRange(38, 48), "15분에는 시간 배율2.5와 7분 이후 배율2로 스폰 증가");

            // 보스 생존 중 간격 2배 → 같은 3초에 약21회.
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(30f, 0f), EnemyDef.GrinderGolem, 1f, game.Player, game.transform);
            game.ActiveBoss = boss;
            yield return new WaitForSeconds(0.8f); // 진행 중 주기를 소진
            yield return CountSpawns(3f, counts);
            Assert.That(counts[2], Is.InRange(18, 26), "후반에도 보스 중 간격 2배");
            game.ActiveBoss = null;
            Object.Destroy(boss.gameObject);

            // 전체 적 100 이상이면 일반 스폰 시도를 버린다
            var filler = new EnemyDef { name = "Filler", color = Color.white, radius = 0.2f, hp = 1000f, speed = 0f };
            while (Enemy.Active.Count < Balance.TimeAliveCap(game.RunTime, Balance.MaxAliveEnemies))
                Enemy.SpawnAt(game.Player.Position + new Vector2(40f + Enemy.Active.Count * 0.3f, -5f), filler, 1f, game.Player, game.transform);
            yield return null;
            int before = Enemy.Active.Count;
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(before, Enemy.Active.Count, "100 상한에서는 새 일반 스폰 없음");
        }
    }

    public class SpecEnemyBehaviorTests
    {
        GameObject playerGo;
        PlayerController player;
        readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            playerGo = new GameObject("SpecPlayer", typeof(Rigidbody2D));
            player = playerGo.AddComponent<PlayerController>();
            var stats = new PlayerStats();
            stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            player.Init(stats, Color.white);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var g in spawned) if (g != null) Object.Destroy(g);
            foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            Object.Destroy(playerGo);
            yield return null;
        }

        Enemy Spawn(EnemyDef def, Vector2 offset)
        {
            var e = Enemy.SpawnAt(player.Position + offset, def, 1f, player, null);
            EnemyRoster.AttachBehavior(e, null, player);
            spawned.Add(e.gameObject);
            return e;
        }

        [UnityTest]
        public IEnumerator Chasers_Lemon_Teabag_EspressoBug_MoveTowardPlayer_AtDefSpeed()
        {
            foreach (var def in new[] { EnemyDef.Brute, EnemyDef.TeabagImp, EnemyDef.EspressoBug })
            {
                var e = Spawn(def, new Vector2(12f, 0f));
                yield return new WaitForSeconds(0.2f);
                Vector2 a = e.transform.position;
                yield return new WaitForSeconds(0.5f);
                Vector2 b = e.transform.position;
                float speed = (b - a).magnitude / 0.5f;
                Assert.Less(b.x, a.x, $"{def.name}: 플레이어 쪽으로");
                Assert.AreEqual(def.speed, speed, def.speed * 0.15f, $"{def.name}: 정의 속도");
                Object.Destroy(e.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Throwers_Kettle_MilkFoamer_HoldBand_FirstShotAtHalfInterval_AimedSingleShot()
        {
            foreach (var def in new[] { EnemyDef.KettleCannon, EnemyDef.MilkFoamer })
            {
                var e = Spawn(def, new Vector2(def.keepDistance, 0f));
                float firstAt = def.attackInterval * 0.5f;
                yield return new WaitForSeconds(firstAt - 0.15f);
                Assert.AreEqual(0, Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length, $"{def.name}: 첫 사격 전");
                yield return new WaitForSeconds(0.3f);
                var shots = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
                Assert.AreEqual(1, shots.Length, $"{def.name}: 첫 사격 1발(조준 단발)");
                Vector2 s0 = shots[0].transform.position;
                yield return new WaitForSeconds(0.2f);
                Assert.IsNotNull(shots[0], $"{def.name}: 첫 탄이 속도/방향 측정까지 살아 있어야 한다");
                Vector2 d = (Vector2)shots[0].transform.position - s0;
                Assert.AreEqual(def.projectileSpeed, d.magnitude / 0.2f, def.projectileSpeed * 0.15f, $"{def.name}: 탄 속도");
                Assert.Less(d.x, 0f, $"{def.name}: 플레이어 쪽으로 조준");
                float dist = Vector2.Distance(e.transform.position, player.Position);
                Assert.That(dist, Is.InRange(def.keepDistance * 0.8f, def.keepDistance * 1.2f), $"{def.name}: 거리 유지 구간");
                Object.Destroy(e.gameObject);
                foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TeaCeremonyGuardian_FiresEight_45DegreesApart_FirstTowardPlayer()
        {
            var def = EnemyDef.TeaCeremonyGuardian;
            var e = Spawn(def, new Vector2(def.keepDistance, 0f));
            yield return new WaitForSeconds(def.attackInterval * 0.5f + 0.05f);
            var shots = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
            Assert.AreEqual(8, shots.Length, "8발 원형");
            Vector2 origin = shots.Aggregate(Vector2.zero, (acc, p) => acc + (Vector2)p.transform.position) / shots.Length;
            var start = shots.Select(p => (Vector2)p.transform.position).ToArray();
            yield return new WaitForSeconds(0.2f);
            var dirs = shots.Where(p => p != null).Select((p, i) => ((Vector2)p.transform.position - start[i])).ToList();
            Vector2 toPlayer = (player.Position - origin).normalized; // 발사 지점(emitter anchor)에서 플레이어 방향
            var angles = dirs.Select(d => Mathf.Repeat(Vector2.SignedAngle(toPlayer, d) + 360f, 360f)).OrderBy(a => a).ToList();
            Assert.AreEqual(8, angles.Count);
            for (int i = 0; i < 8; i++)
            {
                float expected = 45f * i;
                float diff = Mathf.Abs(Mathf.DeltaAngle(angles[i], expected));
                Assert.Less(diff, 6f, $"{i}번 탄 {angles[i]:0.0}° — 첫 방향은 플레이어, 45° 간격");
            }
            float speed = dirs[0].magnitude / 0.2f;
            Assert.AreEqual(def.projectileSpeed, speed, def.projectileSpeed * 0.15f, "탄 속도 5");
        }

        [UnityTest]
        public IEnumerator LeafWarden_StaysStill_SummonsTeabagAtRadiusPlusOne_AtThreeSeconds()
        {
            var def = EnemyDef.LeafWarden;
            var w = Spawn(def, new Vector2(4f, 0f));
            Vector2 p0 = w.transform.position;
            yield return new WaitForSeconds(def.summonInterval * 0.5f - 0.2f);
            Assert.IsFalse(Enemy.Active.Any(x => x.Def == EnemyDef.TeabagImp), "3초 전 소환 없음");
            // 소환된 임프는 곧바로 플레이어를 쫓는다 — 0.35초 뒤에 재면 프레임 길이만큼 다가와 간헐 실패했다(2026-10-09).
            // 나타난 첫 프레임에 같은 기준(반지름+1)으로 잰다.
            Enemy imp = null;
            float until = Time.time + 0.35f;
            while (imp == null && Time.time < until)
            {
                yield return null;
                imp = Enemy.Active.FirstOrDefault(x => x.Def == EnemyDef.TeabagImp);
            }
            Assert.IsNotNull(imp, "3초에 자기 트랙(차) 추적 몹 소환");
            spawned.Add(imp.gameObject);
            Assert.Less(Vector2.Distance(p0, w.transform.position), 0.05f, "소환형은 이동하지 않는다");
            Assert.AreEqual(def.radius + 1f, Vector2.Distance(w.transform.position, imp.transform.position), 0.35f, "자기 반지름+1 거리");
        }
    }

    /// <summary>G12·G13·G20 중 기존 테스트에 없던 조건.</summary>
    public class SpecBossTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null) Object.Destroy(GameManager.Instance.gameObject);
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            Time.timeScale = 1f;
            yield return null;
        }

        static GameManager Quiet(WeaponKind weapon = WeaponKind.Gun)
        {
            var game = GameBootstrap.Build(weapon == WeaponKind.Sword || weapon == WeaponKind.Bow ? Job.TeaSommelier : Job.Bartender, weapon);
            foreach (var mb in game.Player.GetComponents<MonoBehaviour>())
                if (mb is PlayerProjectileWeapon || mb is RapierWeapon) Object.Destroy(mb);
            game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            var spawner = game.GetComponentInChildren<EnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (game.Director != null) game.Director.enabled = false;
            return game;
        }

        // ── G12 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Leviathan_PressureRate_6_9_13_ByHpBand()
        {
            var game = Quiet();
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(6f, 0f), EnemyDef.EspressoLeviathan, 1f, game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 0.1f);
            foreach (var (hpFrac, rate) in new[] { (1f, 6f), (0.5f, 9f), (0.25f, 13f) })
            {
                if (boss.Hp > boss.MaxHp * hpFrac) boss.TakeDamage(boss.Hp - boss.MaxHp * hpFrac); // 피해로 압력도 내려간다
                yield return null;
                float p0 = driver.Pressure, t0 = Time.time;
                yield return new WaitForSeconds(1f);
                float measured = (driver.Pressure - p0) / (Time.time - t0);
                Assert.AreEqual(rate, measured, rate * 0.12f, $"HP {hpFrac:P0}: 압력 {rate}/초");
            }
        }

        static void SetPressure(EspressoLeviathanDriver d, float v) =>
            typeof(EspressoLeviathanDriver).GetField("pressure", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(d, v);

        [UnityTest]
        public IEnumerator Leviathan_PressureCast_CancelsPendingSteamWarning_AndCastsAlone()
        {
            var game = Quiet();
            game.Player.transform.position = new Vector3(0f, -2f, 0f);
            var preexisting = new HashSet<FadeVfx>(Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None));
            var boss = Enemy.SpawnAt(new Vector2(11.5f, -2f), EnemyDef.EspressoLeviathan  /* 증기 사거리 12 안, 4초 이동 뒤에도 접촉·폭발 반경 밖 */, 1f, game.Player, game.transform);
            var driver = boss.gameObject.AddComponent<EspressoLeviathanDriver>();
            driver.Init(boss, game);
            // 등장 1초 + 첫 지연 3초 → 증기 예고 시작 직후
            yield return new WaitForSeconds(Balance.BossArrivalGraceSeconds + 3f + 0.15f);
            var steamWarnings = Object.FindObjectsByType<FadeVfx>(FindObjectsSortMode.None)
                .Where(v => !preexisting.Contains(v) && v.transform.localScale.x > 8f).ToList(); // 길이 12 증기 예고 띠
            string desc = string.Join("; ", steamWarnings.Select(v => $"{v.name} sx={v.transform.localScale.x:0.0} sr={v.GetComponent<SpriteRenderer>()?.sprite?.name}"));
            Assert.Greater(steamWarnings.Count, 0, "전제: 증기 예고가 표시 중");
            float hpBefore = game.Player.Hp;

            SetPressure(driver, 99.99f); // [고정 장치] 압력 100 도달 순간을 만든다
            // 배치 러너 프레임은 매우 짧아(dt≈0.0001) 프레임 수가 아니라 게임 시간으로 기다린다(0.05초 = 6/초×0.05 > 0.01)
            yield return new WaitForSeconds(0.05f);
            Assert.IsTrue(steamWarnings.All(v => v == null), $"[pressure={driver.Pressure:0.00} state={game.State} bossClip={boss.Animator?.Current?.name}] " +
                "압력 시전으로 바뀌면 미발동 증기 경고는 즉시 사라진다(06:130). 처음: " + desc +
                " / 남음: " + string.Join("; ", steamWarnings.Where(v => v != null).Select(v => $"{v.name} sx={v.transform.localScale.x:0.0}")));
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(hpBefore, game.Player.Hp, 0.01f, "취소된 증기는 피해를 주지 않는다(압력 폭발 반경 3.5 밖)");
            Assert.Less(driver.Pressure, 8f, "압력 시전(0.6초) 뒤 0으로 리셋된 다음 6/초로 다시 찬다(1.2초 대기 후 약 3.6)");
        }

        // ── G13 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator FinalBoss_FixedOrder_Enraged49_Cooldown5_Summon16_RefundRestoresStats()
        {
            var game = Quiet();
            Time.timeScale = 3f;
            game.Stats.Apply(new CardEffect(CardEffectType.DamagePercent, 0.5f));
            float boosted = game.Stats.Damage;
            Assert.Greater(boosted, game.Stats.baseDamage, "전제: 공격력 보너스 보유");
            var boss = Enemy.SpawnAt(game.Player.Position + new Vector2(2.5f, 0f), EnemyDef.DifficultCustomer, 1f, game.Player, game.transform);
            boss.gameObject.AddComponent<CustomerBossDriver>().Init(boss, game);
            game.ActiveBoss = boss;

            var order = new List<string>();
            var times = new List<float>();
            string last = null;
            bool sawSuppressed = false, restored = false;
            float suppressedAt = -1f;
            int enemiesBeforeSummon2 = -1, summon2 = -1;
            float t0 = Time.time;
            while (order.Count < 6 && Time.time - t0 < 60f)
            {
                string clip = boss.Animator != null ? boss.Animator.Current?.name : null;
                if (clip != last && clip != null && clip != "idle" && clip != "move" && clip != "hurt")
                {
                    order.Add(clip);
                    times.Add(Time.time - t0);
                    if (order.Count == 2) // 소환 1회차 뒤 분노(HP 49%)로 만든다
                    {
                        boss.Invulnerable = false;
                        boss.TakeDamage(boss.Hp - boss.MaxHp * 0.49f);
                    }
                    if (order.Count == 5) enemiesBeforeSummon2 = Enemy.Active.Count;
                }
                if (order.Count == 6 && summon2 < 0) summon2 = Enemy.Active.Count - enemiesBeforeSummon2;
                last = clip;
                if (game.Stats.BonusesSuppressed && !sawSuppressed)
                {
                    sawSuppressed = true;
                    suppressedAt = Time.time;
                    Assert.AreEqual(game.Stats.baseDamage, game.Stats.Damage, 0.001f, "환불 중 %보너스 무효");
                }
                if (sawSuppressed && !restored && !game.Stats.BonusesSuppressed)
                {
                    restored = true;
                    Assert.AreEqual(5f, Time.time - suppressedAt, 0.4f, "환불 지속 5초");
                    Assert.AreEqual(boosted, game.Stats.Damage, 0.001f, "환불 종료 후 원래 강화 복원");
                }
                if (game.Player.Hp < game.Player.MaxHp * 0.5f) game.Player.Heal(1f);
                yield return null;
            }
            yield return new WaitForSeconds(1.2f);
            if (summon2 < 0 || summon2 == 0) summon2 = Enemy.Active.Count - enemiesBeforeSummon2;
            Time.timeScale = 1f;
            Assert.That(order.Take(6), Is.EqualTo(new[] { "refund", "summon", "review", "reject", "refund", "summon" }), "고정 순서 환불→소환→별점→주문 부정");
            Assert.IsTrue(sawSuppressed && restored, "환불 범위 안: 무효화 후 복원");
            Assert.AreEqual(Balance.BossArrivalGraceSeconds + 3f, times[0], 0.4f, "등장 안내 1초 + 첫 지연 3초 — 측정 " + times[0]);
            Assert.AreEqual(7f, times[1] - times[0], 0.4f, "일반 대기 7초");
            // 소환(2번째) 시작 직후 HP 49% — 분노 여부는 패턴 시작 시 고정되므로 2→3 간격은 7초, 3번째부터 5초.
            Assert.AreEqual(7f, times[2] - times[1], 0.4f, "분노 전 시작한 패턴의 대기는 7초 유지");
            Assert.AreEqual(5f, times[3] - times[2], 0.4f, "HP 49% 분노 → 대기 5초");
            Assert.GreaterOrEqual(summon2, 16, "분노 소환 16마리");
        }

        // ── G20 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SwordAndBowStart_Cocktail3Tea3_NeverOfferHybrid()
        {
            foreach (var weapon in new[] { WeaponKind.Sword, WeaponKind.Bow })
            {
                var game = GameBootstrap.Build(Job.TeaSommelier, weapon);
                yield return null;
                game.AddIngredient(IngredientType.Cocktail, 5000);
                game.AddIngredient(IngredientType.Tea, 5000);
                bool offered = false;
                for (int i = 0; i < 200; i++)
                {
                    if (game.State == GameState.HybridOffer) { offered = true; break; }
                    if (game.State == GameState.LevelUp) game.CardView.Choose(0);
                    else if (game.State == GameState.SkillSelect)
                        game.SkillView.Choose(game.Skills.Nodes.First(n => game.Skills.IsAvailable(n)));
                    else if (game.Cards.CountOwned(CardCategory.Cocktail) >= 3 && game.Cards.CountOwned(CardCategory.Tea) >= 3 && i > 60) break;
                    yield return null;
                }
                Assert.GreaterOrEqual(game.Cards.CountOwned(CardCategory.Cocktail), 3, $"{weapon}: 전제 칵테일 3장 이상");
                Assert.GreaterOrEqual(game.Cards.CountOwned(CardCategory.Tea), 3, $"{weapon}: 전제 차 3장 이상");
                Assert.IsFalse(offered, $"{weapon} 시작은 하이브리드 제안 없음(07:120)");
                Assert.IsFalse(game.HybridUnlocked);
                Object.Destroy(game.gameObject);
                yield return null;
            }
        }
    }
}
