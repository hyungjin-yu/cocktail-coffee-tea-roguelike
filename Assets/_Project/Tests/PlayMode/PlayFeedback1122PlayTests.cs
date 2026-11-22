using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CCT.Tests
{
    /// <summary>2026-11-22 주인 플레이 피드백 1차 — 실제 런 경로: 보스 시간 배지 위치(F), 바텐더 칵테일 종류 표시·튀어오름(D4), 혼합 러시 오라(D6),
    /// 보스 배율 적용(E), 레벨업 카드 등장·hover 확대(A2·17). 화면 판독은 QA 투어 캡처로 따로 확인한다.</summary>
    public class PlayFeedback1122PlayTests
    {
        GameManager game;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;
            foreach (var v in Object.FindObjectsByType<TitleView>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (game != null) Object.Destroy(game.gameObject);
            game = null;
            yield return null;
            Time.timeScale = 1f;
            SettingsService.SetEffectsLevel(SettingsData.EffectsNormal);
        }

        IEnumerator Run(Job job, WeaponKind weapon)
        {
            game = GameBootstrap.Build(job, weapon);
            yield return null;
            game.Spawner.enabled = false;
            game.Player.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f));
            game.Player.Heal(1f);
        }

        static Rect WorldRect(RectTransform t)
        {
            var c = new Vector3[4]; t.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [UnityTest]
        public IEnumerator BossTimer_AtTopRight_LiveCountdown_NoOverlapWithTopHud()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var badge = game.Hud.PhaseBadge;
            game.SetRunTimeForTests(10f);
            badge.RefreshForTests();
            Assert.IsTrue(badge.AtTop, "정규 모드 배지는 위쪽");
            var rt = (RectTransform)badge.transform;
            Assert.AreEqual(new Vector2(1f, 1f), rt.anchorMin, "오른쪽 위 기준");
            StringAssert.StartsWith("다음 보스 ", badge.Text, "남은 시간이 맨 앞");
            string before = badge.Text;
            game.SetRunTimeForTests(11f);
            badge.RefreshForTests();
            Assert.AreNotEqual(before, badge.Text, "1초 지나면 바로 바뀜");
            Canvas.ForceUpdateCanvases();
            var r = WorldRect(rt);
            var hud = game.Hud.transform;
            foreach (var n in new[] { "Info", "CoffeePoints", "PauseButton", "GaugeBacking", "Guide" })
            {
                var other = hud.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x => x.name == n);
                if (other == null) continue;
                Assert.IsFalse(r.Overlaps(WorldRect(other)), "보스 시간 배지와 " + n + " 비겹침");
            }
        }

        [UnityTest]
        public IEnumerator Bartender_CocktailKinds_CountAndCups_PopOnNewKind_AuraDuringRush()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var hud = game.Hud;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual("칵테일 0종", hud.StackCountText);
            Assert.IsFalse(game.RushAura.Showing, "러시 전 오라 없음");
            var cocktails = new[] { "dry_gin", "shot_glass", "bar_shoes" }.Select(id => game.Cards.FindInPool(id)).ToList();
            game.GrantCardForTests(cocktails[0]);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual("칵테일 1종", hud.StackCountText);
            Assert.AreEqual(1, hud.StackCupsFilled, "잔 하나 채움");
            Assert.IsTrue(hud.StackPopping || hud.StackCountScale > 1f, "새 종류 = 숫자 튀어오름");
            yield return new WaitForSecondsRealtime(HudView.StackPopSeconds + 0.2f);
            Assert.AreEqual(1f, hud.StackCountScale, 1e-4f, "튀어오름 뒤 원래 크기");
            game.GrantCardForTests(cocktails[0]); // 같은 카드 중첩은 종류가 아니다
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual("칵테일 1종", hud.StackCountText);
            game.GrantCardForTests(cocktails[1]);
            game.GrantCardForTests(cocktails[2]);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.Greater(game.MixingRushRemaining, 0f, "3종 = 혼합 러시");
            Assert.AreEqual(3, hud.StackCupsFilled, "러시 중 세 잔");
            yield return null;
            Assert.IsTrue(game.RushAura.Showing, "러시 중 플레이어 오라");
            Assert.Greater(game.RushAura.Alpha, 0.05f);
        }

        [UnityTest]
        public IEnumerator Bosses_SpawnWithTunedHpAndAttack()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var schedule = BossDirector.DefaultSchedule(Job.Bartender);
            game.Director.enabled = false;
            foreach (var spec in schedule)
            {
                var boss = game.Director.SpawnNow(spec);
                Assert.AreEqual(IntNum.Round(spec.def.hp * spec.hpScale * (game.Player.Challenge?.HpMultiplier ?? 1f)), Mathf.RoundToInt(boss.MaxHp), spec.def.name + " 체력 배율");
                Assert.AreEqual(spec.attackScale, boss.AttackScale, 1e-5f, spec.def.name + " 공격 배율");
                Assert.Greater(boss.ScaledAttack(spec.def.contactDamage), spec.def.contactDamage, spec.def.name + " 접촉 피해 상향");
                Object.Destroy(boss.gameObject);
                game.ActiveBoss = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CardSelect_PourEntrance_Finishes_HoverEnlargesFocusedCard_LowEffectsInstant()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var view = game.CardView;
            var cards = new[] { "dry_gin", "shot_glass", "bar_shoes" }.Select(id => game.Cards.FindInPool(id)).ToList();
            view.Show(cards.Select(c => new CardOffer { card = c }).ToList(), _ => { });
            Assert.Less(view.CardAlpha(2), 0.5f, "잔이 차오르기 전엔 내용이 흐림");
            yield return new WaitForSecondsRealtime(CardSelectView.PourSeconds + CardSelectView.PourFadeSeconds + 0.3f);
            for (int i = 0; i < 3; i++) { Assert.IsTrue(view.EntranceDone(i), "등장 완료 " + i); Assert.AreEqual(1f, view.CardAlpha(i), 1e-4f); }
            Assert.AreEqual(CardSelectView.HoverScale, view.CardScale(0), 0.01f, "선택된(첫) 카드 확대");
            Assert.AreEqual(1f, view.CardScale(1), 0.01f, "다른 카드는 원래 크기");
            view.Choose(0);
            yield return null;

            SettingsService.SetEffectsLevel(SettingsData.EffectsLow);
            view.Show(cards.Select(c => new CardOffer { card = c }).ToList(), _ => { });
            for (int i = 0; i < 3; i++) { Assert.IsTrue(view.EntranceDone(i), "저이펙트 = 등장 없음"); Assert.AreEqual(1f, view.CardAlpha(i)); }
            view.Choose(0);
        }

        // ───── 2026-11-22 주인 결정 2차: 혼합 러시 켜짐/꺼짐 표시 · 공용 디버프 몹 ─────

        [UnityTest]
        public IEnumerator Rush_DuplicateCardChipWarns_OffFadesAura_HudShowsState()
        {
            yield return Run(Job.Bartender, WeaponKind.Rapier);
            var ids = new[] { "dry_gin", "shot_glass", "bar_shoes" };
            foreach (var id in ids) game.GrantCardForTests(game.Cards.FindInPool(id));
            yield return new WaitForSecondsRealtime(0.15f); // HUD 왼쪽 패널은 0.1초마다 갱신
            Assert.IsTrue(game.MixingRushActive);
            Assert.IsTrue(game.Hud.StackRushShown, "HUD 러시 켜짐 표시(잔·숫자 주황)");
            StringAssert.Contains("켜짐", game.JobTraitStatus);
            var view = game.CardView;
            var dup = game.Cards.FindInPool("dry_gin");
            var fresh = game.Cards.FindInPool("liqueur");
            view.Show(new List<CardOffer> { new CardOffer { card = dup, currentStacks = 1 }, new CardOffer { card = fresh } }, _ => { });
            Assert.AreEqual("러시 꺼짐!", view.JobChipText(0), "중복 카드 = 꺼짐 경고 칩");
            Assert.AreEqual("러시 유지", view.JobChipText(1), "새 종류 = 유지");
            view.Choose(0);
            game.GrantCardForTests(dup);
            yield return null;
            Assert.IsFalse(game.MixingRushActive, "중복 = 꺼짐");
            Assert.IsTrue(game.RushAura.FadingOut || !game.RushAura.Showing, "꺼짐 연출");
            yield return new WaitForSeconds(MixingRushAura.OffSeconds + 0.2f);
            Assert.IsFalse(game.RushAura.Showing, "오라 사라짐");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsFalse(game.Hud.StackRushShown, "꺼짐 표시");
            StringAssert.Contains("꺼짐", game.JobTraitStatus);
        }

        [UnityTest]
        public IEnumerator DebuffMobs_ScheduledAt120_BitterMistPulseDebuffsInRange_GlassThiefBlocksHealAndFlees()
        {
            yield return Run(Job.TeaSommelier, WeaponKind.Bow); // 공용: 차 모드에서도
            game.Director.enabled = false;
            var spawner = game.Spawner;
            spawner.enabled = true;
            game.SetRunTimeForTests(119f);
            yield return null; yield return null;
            Assert.AreEqual(0, spawner.DebuffSpawns, "120초 전 없음");
            game.SetRunTimeForTests(121f);
            yield return null; yield return null;
            Assert.AreEqual(1, spawner.DebuffSpawns, "120초 첫 등장");
            spawner.enabled = false;
            var mist = Enemy.Active.Find(e => e.Def == EnemyDef.BitterMist);
            Assert.IsNotNull(mist);
            var driver = mist.GetComponent<BitterMistDriver>();
            Assert.IsNotNull(driver);
            mist.Invulnerable = true; // 주기 관찰 동안 플레이어 자동 공격에 죽지 않게(시험 격리)
            var p = game.Player;
            // 범위 밖: 파동이 와도 디버프 없음
            mist.transform.position = p.Position + new Vector2(BitterMistDriver.PulseRadius + 1.5f, 0f);
            driver.Pulse();
            Assert.IsFalse(p.Stats.IsSlowed || p.Stats.AttackSlowed, "범위 밖 무사");
            // 범위 안: 이동 둔화·공격 속도 감소
            mist.transform.position = p.Position + new Vector2(1f, 0f);
            driver.Pulse();
            Assert.IsTrue(p.Stats.IsSlowed); Assert.IsTrue(p.Stats.AttackSlowed);
            yield return new WaitForSeconds(0.2f);
            StringAssert.Contains("공속 -25%", game.Hud.StatusLineText, "상태 줄에 공속 감소 표시");
            // 예고: 시간이 지나면 예고 원 → 파동
            float t0 = Time.time; int pulses = driver.Pulses; bool sawTelegraph = false;
            while (Time.time - t0 < BitterMistDriver.PulseEvery + 2f && driver.Pulses == pulses) { sawTelegraph |= driver.Telegraphing; yield return null; }
            Assert.IsTrue(sawTelegraph, "파동 전 예고");
            Assert.Greater(driver.Pulses, pulses, "주기 파동");

            var thief = spawner.SpawnDebuffMob(game.RunTime);
            Assert.AreSame(EnemyDef.GlassThief, thief.Def, "번갈아 빈 잔 도둑");
            var td = thief.GetComponent<GlassThiefDriver>();
            thief.Invulnerable = true;
            thief.transform.position = p.Position + new Vector2(0.3f, 0f);
            float waited = 0f;
            while (!p.Stats.HealBlocked && waited < 2f) { waited += Time.deltaTime; yield return null; }
            Assert.IsTrue(p.Stats.HealBlocked, "닿으면 회복 차단");
            Assert.AreEqual(0f, p.Stats.RegenPerSecond);
            Assert.IsTrue(td.Fleeing, "치고 빠지기");
            Object.Destroy(mist.gameObject); Object.Destroy(thief.gameObject);
        }
    }
}
