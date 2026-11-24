using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>#20 총 유도 증거: 두 직업 카드 창 문구(총/활/검 비교)와 실제 탄 궤적·렌더. CCT_EVIDENCE_DIR가 있을 때만.</summary>
    public class GunHomingEvidenceCapture
    {
        string dir, tempPath; GameManager game; readonly StringBuilder log = new StringBuilder();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            dir = Environment.GetEnvironmentVariable("CCT_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("CCT_EVIDENCE_DIR 없음");
            Directory.CreateDirectory(dir);
            tempPath = Path.Combine(Path.GetTempPath(), "cct-gunhoming-" + Guid.NewGuid().ToString("N"), "save.json");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
            PermanentProgress.SetPathForTests(tempPath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (dir != null) File.AppendAllText(Path.Combine(dir, "gun-homing-log.txt"), log.ToString(), new UTF8Encoding(false));
            log.Clear();
            if (game != null) UnityEngine.Object.Destroy(game.gameObject);
            game = null; Time.timeScale = 1f;
            yield return null;
            PermanentProgress.SetPathForTests(TestSaveIsolation.SessionPath);
            try { Directory.Delete(Path.GetDirectoryName(tempPath), true); } catch { }
        }

        void Line(string s) { log.AppendLine(s); Debug.Log("[GunHoming] " + s); }
        static CardData C(string id) => CardDatabase.LoadAll().First(c => c.id == id);

        GameManager Build(Job job, WeaponKind weapon)
        {
            var g = GameBootstrap.Build(job, weapon);
            var sp = g.GetComponentInChildren<EnemySpawner>(); if (sp != null) sp.enabled = false;
            if (g.Director != null) g.Director.enabled = false;
            return g;
        }

        [UnityTest]
        public IEnumerator CardText_BothJobs_GunVsBowVsSword()
        {
            foreach (var (job, id) in new[] { (Job.Bartender, "long_stirrer"), (Job.TeaSommelier, "long_spout") })
                foreach (var weapon in new[] { WeaponKind.Gun, WeaponKind.Bow, WeaponKind.Sword })
                {
                    game = Build(job, weapon);
                    yield return null;
                    var offers = new List<CardOffer> { new CardOffer { card = C(id), currentStacks = 0 }, new CardOffer { card = C(id), currentStacks = 1 } };
                    game.CardView.Show(offers, o => { });
                    for (int w = 0; w < 600 && !(game.CardView.EntranceDone(0) && game.CardView.EntranceDone(1)); w++) yield return null; // 등장 연출 끝날 때까지
                    yield return new WaitForSecondsRealtime(4f); // 카드 전달 연출(진행자 그림)이 끝나고 카드가 보일 때까지
                    yield return null; yield return null;
                    for (int i = 0; i < 2; i++)
                        Line($"{job}/{weapon}/{id} stacks={i} desc='{game.CardView.DescriptionText(i)}' lines='{game.CardView.EffectLines(i)}'");
                    var cc = game.CardView.GetComponentInParent<Canvas>(); var canvases = cc != null ? new[] { cc.rootCanvas } : new Canvas[0];
                    Line("card canvas: " + (cc != null ? cc.rootCanvas.name : "none"));
                    yield return SkinEvidenceCapture.CaptureTo(dir, Line, null, canvases, 1280, 720, $"gun_card_{job}_{weapon}");
                    bool gun = weapon == WeaponKind.Gun;
                    if (gun)
                    {
                        Assert.AreEqual("탄환이 움직이는 적을 따라갑니다", game.CardView.DescriptionText(0));
                        Assert.AreEqual("탄환이 적을 더 빠르게 따라갑니다", game.CardView.DescriptionText(1));
                        StringAssert.DoesNotContain("사거리", game.CardView.EffectLines(0));
                    }
                    else StringAssert.Contains("사거리", game.CardView.EffectLines(0), $"{weapon}: 사거리 카드는 기존 그대로");
                    UnityEngine.Object.Destroy(game.gameObject); game = null;
                    yield return null;
                }
        }

        [UnityTest]
        public IEnumerator Bullets_RealFlight_Render_AndTrajectory()
        {
            foreach (var job in new[] { Job.Bartender, Job.TeaSommelier })
            {
                game = Build(job, WeaponKind.Gun);
                yield return null;
                var card = C(job == Job.Bartender ? "long_stirrer" : "long_spout");
                for (int n = 0; n < 3; n++) game.GrantCardForTests(card);
                Assert.AreEqual(3, game.Stats.homingLevel);
                foreach (var w in game.Player.GetComponentsInChildren<PlayerProjectileWeapon>()) w.enabled = false; // 실제 자동 사격을 끄고 손으로 쏜 두 발만 비교
                var p = game.Player.Position;
                var enemy = Enemy.SpawnAt(p + new Vector2(3f, 1.2f), EnemyDef.SwirlConjuror, 1f, game.Player, null);
                float hp0 = enemy.Hp;
                var straight = new List<float>(); var homed = new List<float>();
                PlayerProjectile.Spawn(p, Vector2.right, 9f, 15f, 0, null, "bullet", game.Player, true, null, 2f, float.PositiveInfinity, 0);
                PlayerProjectile.Spawn(p, Vector2.right, 9f, 15f, 0, null, "bullet", game.Player, true, null, 2f, float.PositiveInfinity, 3);
                for (int f = 0; f < 6; f++)
                {
                    yield return new WaitForSeconds(0.07f);
                    foreach (var b in UnityEngine.Object.FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None))
                        Line($"{job} t{f} bullet pos=({b.transform.position.x:0.00},{b.transform.position.y:0.00}) angle={b.transform.eulerAngles.z:0.0}");
                    if (f == 1 || f == 3)
                        yield return SkinEvidenceCapture.CaptureTo(dir, Line, Camera.main, new List<Canvas>(), 1280, 720, $"gun_flight_{job}_f{f}");
                }
                yield return new WaitForSeconds(0.6f);
                Line($"{job} enemy hp {hp0} -> {enemy.Hp}");
                Assert.Less(enemy.Hp, hp0, "유도 탄이 맞힌다");
                Assert.AreEqual(hp0 - 15f, enemy.Hp, 1.0f, "직진 탄은 빗나가고 유도 탄 한 발만 맞힌다(피해 15)");
                UnityEngine.Object.Destroy(enemy.gameObject);
                UnityEngine.Object.Destroy(game.gameObject); game = null;
                yield return null;
            }
        }
    }
}
