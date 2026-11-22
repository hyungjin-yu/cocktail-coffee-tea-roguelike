#if CCT_QA_TOUR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 QA 전용(CCT_QA_TOUR) "feedback3" 모드 — 셋째·넷째 묶음(1 첫 화면, 11 등급, 12 목표 카드, 14 획득 그림, 15 보스 안내,
    /// 16 타격감, 20 몹 등급 탄, 21 HP 20% 미만) 실제 화면. 강제 경로: 스폰 정지·체력 증가, 카드 창 직접 제시(CardView.Show),
    /// 목표 조합 SetComboTarget, 보스 SpawnNow·런 시간 이동(SetRunTimeForTests), 재료 보상 RecordBossAward + DrinkHud.OnSupplyAward,
    /// 적 탄은 EnemyProjectile.Spawn(피해 0), 플레이어 HP는 실제 TakeDamage 경로, 적 피격은 Enemy.TakeDamage(막타 포함).
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator Feedback3Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(0.2f);
            }
            string H = $"{Screen.width}x{Screen.height}";
            TitleView title = null;
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return null; }
            if (title == null) { Fail("feedback3: no title"); yield break; }
            // ── 1 첫 화면(첫 진입 전체 연출 중간·끝) ──
            var intro = title.Intro;
            Line($"F3 intro full={intro?.PlayedFull} duration={intro?.Duration} finishedAtTourStart={intro?.Finished}");
            // 투어 준비(해상도 변경) 동안 첫 연출이 끝나므로 같은 연출을 처음부터 다시 재생해 중간 프레임을 찍는다(QA 전용 재생).
            intro?.ReplayForQa();
            intro?.StartEntering(); // 2026-11-18: 외관 입장 대기 → 명시 입장(자동 입장 폐기)
            yield return Wait(0.25f); yield return Shot($"f3_{H}_00_title_intro_dark");
            yield return Wait(0.6f); yield return Shot($"f3_{H}_01_title_intro_mid");
            yield return Wait(2f); yield return Shot($"f3_{H}_02_title_intro_end");
            Line($"F3 intro finished={intro?.Finished}");

            foreach (var combo in new[] { Combos[0], Combos[3] })
            {
                title = FindFirstObjectByType<TitleView>();
                if (title != null) title.RequestStart();
                yield return StartCombo(combo, false);
                var game = GameManager.Instance;
                if (game == null) { Fail("feedback3: no game"); continue; }
                string tag = $"f3_{H}_{combo.label}";
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 900f));
                game.Player.Heal(1f);
                FreezeWorld(game);
                yield return Wait(0.5f);

                // ── 11·12 카드 등급 + 목표 표식 ──
                var cat = combo.job == Job.Bartender ? CardCategory.Cocktail : CardCategory.Tea;
                var pool = CardDatabase.LoadAll().Where(c => c.category == cat).ToList();
                var pick = new List<CardData>();
                foreach (var r in new[] { CardRarity.Common, CardRarity.Rare, CardRarity.Epic }) { var c = pool.FirstOrDefault(x => x.rarity == r); if (c != null) pick.Add(c); }
                var target = HiddenSkillDatabase.All.FirstOrDefault(d => pick.Any(c => HiddenRequirements.CardAdvances(d, game.Job, game.HiddenState, c.id, out _, out _)));
                if (target != null) game.SetComboTarget(target.skill);
                game.CardView.Show(pick.Select(c => new CardOffer { card = game.Cards.FindInPool(c.id) ?? c, rarityPercent = 30f }).ToList(), _ => { });
                yield return Wait(0.08f);
                if (game.CardView.IsLearningOpen) { game.CardView.CloseLearning(); yield return Wait(0.2f); }
                yield return Shot($"{tag}_10_card_rarity_enter");
                yield return WaitCardSettled(game);
                Line($"F3 {tag} cards [{string.Join(",", pick.Select(c => c.id + ":" + c.rarity))}] frames=[{game.CardView.RarityFrameWidth(0)},{game.CardView.RarityFrameWidth(1)},{game.CardView.RarityFrameWidth(2)}] goal=['{game.CardView.GoalMarkText(0)}','{game.CardView.GoalMarkText(1)}','{game.CardView.GoalMarkText(2)}'] target={target?.id}");
                yield return Shot($"{tag}_11_card_rarity_goal");
                game.CardView.Choose(0); yield return Wait(0.3f);
                yield return ClearOverlays(game, tag + " cards");

                // ── 14 획득 그림(같은 종류 합침) ──
                var items = new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.Gin, 150), new DrinkAmount(DrinkIngredients.LemonJuice, 100), new DrinkAmount(DrinkIngredients.Gin, 150), new DrinkAmount(DrinkIngredients.Honey, 50) };
                if (DrinkLab.RecordBossAward(game.RunId, "qa-f3-a", "QA", items)) game.DrinkHud.OnSupplyAward(game.Player.Position, items);
                var more = new List<DrinkAmount> { new DrinkAmount(DrinkIngredients.BlackTea, 20) };
                if (DrinkLab.RecordBossAward(game.RunId, "qa-f3-b", "QA", more)) game.DrinkHud.OnSupplyAward(game.Player.Position, more);
                yield return Wait(0.3f);
                Line($"F3 {tag} toast chips={game.DrinkHud.ToastChipCount} summary='{game.DrinkHud.ToastSummary}'");
                yield return Shot($"{tag}_20_award_toast_chips", game);

                // ── 21 HP 20% 미만 ──
                float max = game.Player.MaxHp;
                game.Player.TakeDamage(game.Player.Hp - max * 0.15f);
                yield return Wait(0.6f);
                Line($"F3 {tag} lowhp hp={game.Player.Hp:0}/{max:0} active={game.Hud.LowHp.Active} chip={game.Hud.LowHp.ChipVisible} edge={game.Hud.LowHp.EdgeAlpha:0.00}");
                yield return Shot($"{tag}_30_low_hp", game);
                game.Player.Heal(1f); yield return Wait(0.2f);
                Line($"F3 {tag} healed active={game.Hud.LowHp.Active}");

                // ── 20 몹 등급 탄(피해 0, 느리게) ──
                var p0 = game.Player.Position;
                EnemyProjectile.Spawn(p0 + new Vector2(5f, 1.2f), Vector2.left, 0.4f, 0f, null, game.transform, EnemyProjectile.ArtPrefix + "glass_shard", null, default, EnemyProjectile.Tier.Normal);
                EnemyProjectile.Spawn(p0 + new Vector2(5f, 0f), Vector2.left, 0.4f, 0f, null, game.transform, EnemyProjectile.ArtPrefix + "glass_shard", null, default, EnemyProjectile.Tier.Elite);
                EnemyProjectile.Spawn(p0 + new Vector2(5f, -1.2f), Vector2.left, 0.4f, 0f, null, game.transform, EnemyProjectile.ArtPrefix + "glass_shard", null, default, EnemyProjectile.Tier.Boss);
                yield return Wait(0.5f);
                yield return Shot($"{tag}_40_projectile_tiers", game);

                // ── 15 보스 접근(시간 이동) → 화면 밖 방향 표시 ──
                var next = game.Director.NextSpec;
                if (next != null)
                {
                    game.Director.enabled = true; // FreezeWorld가 끈 보스 일정 — 접근 예고는 실제 일정(NextSpec)으로 계산된다
                    game.SetRunTimeForTests(next.time - 8f);
                    yield return Wait(0.5f);
                    Line($"F3 {tag} incoming badge='{game.Hud.PhaseBadge.Text}' portrait={game.Hud.PhaseBadge.PortraitVisible}");
                    yield return Shot($"{tag}_50_boss_incoming", game);
                    var boss = game.Director.SpawnNow(next);
                    yield return Wait(0.6f);
                    if (boss != null)
                    {
                        boss.transform.position = game.Player.Position + new Vector2(-30f, 0f);
                        yield return Wait(0.5f);
                        Line($"F3 {tag} offscreen visible={game.Hud.BossIndicator.Visible} text='{game.Hud.BossIndicator.LabelText.Replace('\n', '/')}' portrait={(game.Hud.BossIndicator.PortraitSprite != null)}");
                        yield return Shot($"{tag}_51_boss_offscreen", game);
                        game.OpenPause(); yield return Wait(0.3f);
                        Line($"F3 {tag} paused indicatorVisible={game.Hud.BossIndicator.Visible} (expected false)");
                        if (game.Hud.BossIndicator.Visible) Fail(tag + " boss indicator over pause");
                        game.RequestEscape(); yield return Wait(0.3f);
                        // ── 16 타격감: 보스 근처로 가져와 일반 타격 → 막타 ──
                        boss.transform.position = game.Player.Position + new Vector2(2.5f, 0f);
                        yield return Wait(0.2f);
                        boss.TakeDamage(1f, HitKind.Normal, (Vector2)boss.transform.position + new Vector2(-0.5f, 0f));
                        yield return Wait(0.03f);
                        yield return Shot($"{tag}_60_hit_streak", game);
                        int before = Enemy.LethalVfxCount;
                        boss.TakeDamage(boss.Hp + 10f, HitKind.Normal, (Vector2)boss.transform.position);
                        yield return Wait(0.05f);
                        Line($"F3 {tag} lethal vfx +{Enemy.LethalVfxCount - before}");
                        yield return Shot($"{tag}_61_killing_blow", game);
                        yield return Wait(1f);
                        yield return ClearOverlays(game, tag + " boss");
                    }
                }
                game.GoToTitle(); yield return Wait(0.9f);
            }
            // 재진입 짧은 연출
            title = FindFirstObjectByType<TitleView>();
            Line($"F3 title reentry full={title?.Intro?.PlayedFull} duration={title?.Intro?.Duration}");
            yield return Wait(0.5f);
            yield return Shot($"f3_{H}_90_title_reentry");
        }
    }
}
#endif
