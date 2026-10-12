using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>
    /// 10 §1 G03·G04·G05에서 기존 테스트가 다루지 않던 조건만 실제 동작으로 검증한다(2026-10-08).
    /// G03: Input System 가상 키보드/마우스 장치 상태로 대각선 정규화와 조작 방식별 입력 분리.
    /// G04: 반원 밖(다른 레인) 미스, 한 번 휘두름당 적 하나에 1회 피해.
    /// G05: 1/3/8발 실제 발사 각도, 관통 0/1/6의 최대 타격 수, 큰 프레임 간격에서도 얇은 적을 놓치지 않는 스윕.
    /// 총 2배: 총구보다 가까운 표적에도 탄이 앞으로 나가 명중.
    /// </summary>
    public class SpecCombatInputTests
    {
        readonly List<Object> created = new List<Object>();
        PlayerController player;
        PlayerStats stats;
        MovementScheme schemeBefore;

        static EnemyDef Still(float radius = 0.35f, float hp = 1000f) => new EnemyDef
        {
            name = "SpecDummy", color = Color.white, radius = radius, hp = hp, speed = 0f, contactDamage = 0f,
        };

        UnityEngine.InputSystem.InputSettings.BackgroundBehavior bgBefore;
        UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode editorBefore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 배치 테스트 러너는 창 포커스가 없어 Input System이 장치 입력을 무시한다 — 이 테스트 동안만 포커스와 무관하게 받는다.
            bgBefore = InputSystem.settings.backgroundBehavior;
            editorBefore = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            schemeBefore = InputSettings.Scheme;
            var go = new GameObject("SpecPlayer", typeof(Rigidbody2D));
            created.Add(go);
            player = go.AddComponent<PlayerController>();
            stats = new PlayerStats();
            player.Init(stats, Color.white);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;
            InputSettings.Scheme = schemeBefore;
            InputSystem.settings.backgroundBehavior = bgBefore;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorBefore;
            foreach (var o in created) if (o != null) Object.Destroy(o);
            created.Clear();
            foreach (var e in Enemy.Active.ToArray()) if (e != null) Object.Destroy(e.gameObject);
            foreach (var p in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
            yield return null;
        }

        Enemy Spawn(Vector2 offset, EnemyDef def = null)
        {
            var e = Enemy.SpawnAt(player.Position + offset, def ?? Still(), 1f, player, null);
            created.Add(e.gameObject);
            return e;
        }

        // ── G03 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Keyboard_DiagonalInput_IsNormalized_ToMoveSpeed()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            var kb = InputSystem.AddDevice<Keyboard>("SpecKeyboard");
            try
            {
                kb.MakeCurrent();
                Vector2 start = player.Position;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D, Key.W));
                float t0 = Time.time;
                yield return new WaitForSeconds(0.5f);
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                float elapsed = Time.time - t0;
                Vector2 moved = player.Position - start;
                float speed = moved.magnitude / elapsed;
                Assert.Greater(moved.x, 0.3f, "D: 오른쪽");
                Assert.Greater(moved.y, 0.3f, "W: 위쪽");
                Assert.AreEqual(moved.x, moved.y, 0.15f, "대각선 45°");
                Assert.Less(speed, stats.MoveSpeed * 1.12f, "대각선이 √2배 빨라지면 안 된다(길이 1로 정규화)");
                Assert.Greater(speed, stats.MoveSpeed * 0.8f);
            }
            finally { InputSystem.RemoveDevice(kb); }
        }

        [UnityTest]
        public IEnumerator MouseScheme_IgnoresKeyboard_AndKeyboardScheme_IgnoresMouse()
        {
            var camGo = new GameObject("SpecCam");
            created.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(player.Position.x, player.Position.y, -10f);
            var kb = InputSystem.AddDevice<Keyboard>("SpecKeyboard2");
            var mouse = InputSystem.AddDevice<Mouse>("SpecMouse");
            try
            {
                kb.MakeCurrent();
                mouse.MakeCurrent();
                yield return null;
                Vector2 playerScreen = cam.WorldToScreenPoint(player.transform.position);

                // 마우스 방식: 커서는 왼쪽 멀리, 키보드는 D(오른쪽) — 키보드가 무시되면 왼쪽으로 간다.
                InputSettings.Scheme = MovementScheme.Mouse;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = playerScreen + new Vector2(-300f, 0f) });
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D));
                Vector2 a = player.Position;
                yield return new WaitForSeconds(0.3f);
                Assert.Less(player.Position.x - a.x, -0.5f, "마우스 방식: 커서 쪽(왼쪽)으로, 키보드 D는 비활성");

                // 키보드 방식: 커서는 오른쪽 멀리, 키보드는 A(왼쪽) — 마우스가 무시되면 왼쪽으로 간다.
                InputSettings.Scheme = MovementScheme.Keyboard;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.A));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = (Vector2)cam.WorldToScreenPoint(player.transform.position) + new Vector2(300f, 0f) });
                yield return null;
                Vector2 b = player.Position;
                yield return new WaitForSeconds(0.3f);
                Assert.Less(player.Position.x - b.x, -0.5f, "키보드 방식: A(왼쪽)로, 마우스 커서 위치는 비활성");
                InputSystem.QueueStateEvent(kb, new KeyboardState());
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                InputSystem.RemoveDevice(mouse);
            }
        }

        // ── G04 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Melee_OtherLaneOutsideSemicircle_Misses_AndOneSwingHitsEachEnemyOnce()
        {
            player.gameObject.AddComponent<RapierWeapon>().Init(stats, player);
            var near = Spawn(new Vector2(1.2f, 0f));
            // 앞쪽이지만 반원 반지름(최종 사거리)+적 반지름 밖, 다른 레인(위쪽 2.7). 획득 반경(사거리+0.6) 안이라 휘두름은 일어난다.
            float r = 0.35f;
            var far = Spawn(new Vector2(0.4f, stats.Range + r + 0.25f));
            Assert.Less(Vector2.Distance(far.transform.position, player.Position), stats.Range + 0.6f + 0.6f);
            float nearHp = near.Hp, farHp = far.Hp;
            yield return new WaitForSeconds(0.25f); // 첫 휘두름 1회(공격 간격 0.7초 전)
            Assert.AreEqual(nearHp - stats.Damage, near.Hp, 0.001f, "한 번 휘두름에 반원 안 적은 정확히 1회 피해");
            Assert.AreEqual(farHp, far.Hp, 0.001f, "반원 밖 다른 레인 적은 맞지 않는다");
        }

        // ── G05 ─────────────────────────────────────────────

        static float SignedAngle(Vector2 from, Vector2 to) => Vector2.SignedAngle(from, to);
        string lastDiag;

        IEnumerator FireOnceAndReadAngles(int count, List<float> angles)
        {
            stats.extraThrusts = count - 1;
            var target = Spawn(new Vector2(5f, 0f));
            var weapon = player.gameObject.AddComponent<PlayerProjectileWeapon>();
            weapon.Init(stats, player);
            created.Add(weapon);
            yield return new WaitForSeconds(0.08f); // 발사 후 탄이 약 0.5 이동(프레임 수가 아니라 게임 시간 — 배치 러너 dt≈0.0001)
            string diag = $"[enemies={Enemy.Active.Count} nearest={(Enemy.FindNearest(player.Position, stats.Range + 3.6f) != null)} shots={PlayerProjectile.ActiveCount} weapon={(weapon != null && weapon.enabled)} pos={player.Position}]";
            Object.Destroy(weapon);
            var origin = player.Position;
            Vector2 baseDir = ((Vector2)target.transform.position - origin).normalized;
            foreach (var p in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None))
            {
                Vector2 d = (Vector2)p.transform.position - origin;
                if (d.sqrMagnitude > 0.0001f) angles.Add(SignedAngle(baseDir, d));
                Object.Destroy(p.gameObject);
            }
            angles.Sort();
            lastDiag = diag;
            Object.Destroy(target.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Ranged_1_3_8Shots_FanAngles_MatchSpec()
        {
            foreach (var (count, expected) in new[]
                     {
                         (1, new[] { 0f }),
                         (3, new[] { -10f, 0f, 10f }),
                         (8, Enumerable.Range(0, 8).Select(i => -30f + 60f / 7f * i).ToArray()),
                     })
            {
                var angles = new List<float>();
                yield return FireOnceAndReadAngles(count, angles);
                Assert.AreEqual(count, angles.Count, $"{count}발 {lastDiag}");
                for (int i = 0; i < count; i++)
                    Assert.AreEqual(expected[i], angles[i], 0.6f, $"{count}발 중 {i}번째 각도");
            }
        }

        [UnityTest]
        public IEnumerator Ranged_Pierce_0_1_6_HitsAtMost1PlusPierce_NoDoubleHit()
        {
            foreach (int pierce in new[] { 0, 1, 6 })
            {
                var line = new List<Enemy>();
                for (int i = 0; i < 9; i++) line.Add(Spawn(new Vector2(1.5f + i * 0.45f, 0f), Still(0.2f)));
                PlayerProjectile.Spawn(player.Position, Vector2.right, 9f, 10f, pierce, null);
                yield return new WaitForSeconds(0.8f);
                int hit = line.Count(e => e.Hp < e.MaxHp);
                Assert.AreEqual(1 + pierce, hit, $"관통 {pierce}: 최대 타격 수 1+관통");
                Assert.IsTrue(line.All(e => e.Hp == e.MaxHp || Mathf.Approximately(e.MaxHp - e.Hp, 10f)), $"관통 {pierce}: 같은 적 중복 타격 없음");
                foreach (var e in line) Object.Destroy(e.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Ranged_LargeFrameStep_StillHitsThinEnemy_BySweep()
        {
            var thin = Spawn(new Vector2(1.4f, 0f), Still(0.05f));
            Time.captureFramerate = 4; // 프레임당 0.25초 → 탄이 한 프레임에 2.25 이동(적 지름 0.1을 건너뜀)
            PlayerProjectile.Spawn(player.Position + new Vector2(0.2f, 0f), Vector2.right, 9f, 10f, 0, null);
            for (int i = 0; i < 4; i++) yield return null;
            Time.captureFramerate = 0;
            Assert.Less(thin.Hp, thin.MaxHp, "고속 이동 중 프레임 사이에 있는 얇은 적도 선분 스윕으로 맞는다");
        }

        // ── 총 2배 영향 ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Gun2x_CloseTargets_BothSides_ShotGoesForward_AndHits()
        {
            var go = new GameObject("GunPlayer", typeof(Rigidbody2D));
            created.Add(go);
            var gunner = go.AddComponent<PlayerController>();
            var s = new PlayerStats();
            gunner.Init(s, Color.white, "bartender");
            Assert.IsTrue(gunner.Visual.AttachWeaponArt("weapon_gun", 0.4f), "실제 bartender/weapon_gun 카탈로그 필요");
            Object.Destroy(player.gameObject);
            player = gunner;
            yield return null;

            foreach (float side in new[] { 1f, -1f })
            foreach (float dist in new[] { 0.5f, 0.7f, 2f })
            {
                Assert.IsTrue(gunner.Visual.TryGetMuzzleWorld(side, out var muzzle));
                if (dist <= 0.7f)
                    Assert.Greater((muzzle.x - gunner.Position.x) * side, dist, "전제: 2배 총구가 이 표적보다 앞에 있다");
                var target = Spawn(new Vector2(dist * side, 0f), Still(0.35f));
                var weapon = go.AddComponent<PlayerProjectileWeapon>();
                weapon.Init(s, gunner);
                weapon.enabled = false;
                typeof(PlayerProjectileWeapon).GetMethod("Fire", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(weapon, new object[] { target }); // 발사 직후(근거리 충돌로 소멸하기 전)의 실제 탄 속도를 측정
                var shots = Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None);
                Assert.Greater(shots.Length, 0, "실제 탄을 발사했다");
                foreach (var shot in shots)
                {
                    var velocity = (Vector2)typeof(PlayerProjectile).GetField("velocity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(shot);
                    Assert.Greater(velocity.x * side, 0f, $"거리 {dist} 방향 {side}: 발사 직후 속도가 전방");
                }
                Object.Destroy(weapon);
                yield return new WaitForSeconds(0.3f);
                Assert.Less(target.Hp, target.MaxHp, $"거리 {dist} 방향 {side}: 명중");
                Assert.AreEqual(side, gunner.Facing, $"거리 {dist} 방향 {side}: 표적 쪽을 바라본다");
                foreach (var p in shots)
                    if (p != null) Assert.Greater(((Vector2)p.transform.position - gunner.Position).x * side, -0.01f, "탄이 몸 뒤로 날아가지 않는다");
                Object.Destroy(target.gameObject);
                foreach (var p in Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
                yield return null;
            }
        }
    }
}
