#if CCT_QA_TOUR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 QA 전용(CCT_QA_TOUR) "smooth13" 모드 — 피드백 13 이동 끊김 측정. 실제 PlayerController 이동 경로(AutoInputOverride)로
    /// 두 직업·네 무기 × 오른쪽/왼쪽/대각 2초씩, 렌더 프레임마다 dt·플레이어 그림 위치·카메라 위치·화면상 플레이어 x를 CSV로 남긴다.
    /// "-cct-qa-nointerp"를 주면 보간 없음(이전 동작)으로 같은 측정을 한다(전/후 A/B). 판정: 이동 중 그림이 0 이동한 프레임 비율(멈칫),
    /// 화면상 플레이어 x 프레임 간 변화의 표준편차(카메라 대비 흔들림), dt 최대값(프레임 시간 급등).
    /// 강제 경로: 스폰 정지(FreezeWorld)·체력 증가(한산한 구간만 — 군중 구간 측정은 NOT RUN으로 보고).
    /// </summary>
    public partial class AutoTour
    {
        IEnumerator Smooth13Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            PlayerController.QaDisableInterpolation = System.Array.IndexOf(args, "-cct-qa-nointerp") >= 0;
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string variant = PlayerController.QaDisableInterpolation ? "nointerp" : "interp";
            Line($"SM screen {Screen.width}x{Screen.height} variant={variant} vsync={QualitySettings.vSyncCount} target={Application.targetFrameRate} fixedDt={Time.fixedDeltaTime}");
            var csv = new StringBuilder("variant,combo,dir,frame,dt,playerX,camX,screenX\n");
            var title = FindFirstObjectByType<TitleView>();
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            foreach (var combo in Combos)
            {
                title = FindFirstObjectByType<TitleView>();
                if (title != null) title.RequestStart();
                yield return StartCombo(combo, false);
                var game = GameManager.Instance;
                if (game == null) { Fail("smooth13: no game " + combo.label); continue; }
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 100000f)); game.Player.Heal(1f);
                FreezeWorld(game);
                yield return Wait(0.6f);
                var player = game.Player;
                var cam = Camera.main;
                foreach (var (dirName, dir) in new[] { ("right", new Vector2(1f, 0f)), ("left", new Vector2(-1f, 0f)), ("diag", new Vector2(0.7071f, 0.7071f)), ("diag2", new Vector2(-0.7071f, -0.7071f)) })
                {
                    player.AutoInputOverride = dir;
                    yield return null; yield return null; // 가속 구간 제외
                    float lastX = player.transform.position.x, lastScreen = cam.WorldToScreenPoint(player.transform.position).x;
                    int frames = 0, still = 0; float maxDt = 0f; var deltas = new List<float>();
                    float t = 0f;
                    while (t < 2f)
                    {
                        yield return null;
                        float dt = Time.unscaledDeltaTime; t += dt;
                        float x = player.transform.position.x;
                        float sx = cam.WorldToScreenPoint(player.transform.position).x;
                        bool wallStop = Mathf.Abs(x - BattleArena.ClampX(x + Mathf.Sign(dir.x) * 0.5f, PlayerController.Radius)) < 0.01f && Mathf.Abs(dir.x) > 0f;
                        if (!wallStop)
                        {
                            frames++;
                            if (Mathf.Abs(x - lastX) < 1e-5f) still++;
                            deltas.Add(sx - lastScreen);
                            maxDt = Mathf.Max(maxDt, dt);
                        }
                        csv.Append(variant).Append(',').Append(combo.label).Append(',').Append(dirName).Append(',').Append(frames).Append(',')
                           .Append(dt.ToString("0.00000", CultureInfo.InvariantCulture)).Append(',').Append(x.ToString("0.00000", CultureInfo.InvariantCulture)).Append(',')
                           .Append(cam.transform.position.x.ToString("0.00000", CultureInfo.InvariantCulture)).Append(',').Append(sx.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
                        lastX = x; lastScreen = sx;
                    }
                    player.AutoInputOverride = Vector2.zero;
                    float mean = 0f; foreach (var d in deltas) mean += d; mean /= Mathf.Max(1, deltas.Count);
                    float var = 0f; foreach (var d in deltas) var += (d - mean) * (d - mean); var /= Mathf.Max(1, deltas.Count);
                    Line($"SM {variant} {combo.label} {dirName}: frames={frames} stillFrames={still} ({(frames > 0 ? 100f * still / frames : 0f):0.0}%) screenDxMean={mean:0.00}px screenDxStd={Mathf.Sqrt(var):0.00}px maxDt={maxDt * 1000f:0.0}ms");
                    yield return Wait(0.3f);
                }
                player.AutoInputOverride = null;
                game.GoToTitle(); yield return Wait(0.9f);
            }
            try { File.WriteAllText(Path.Combine(outDir, $"smooth13_{variant}.csv"), csv.ToString()); } catch (System.Exception e) { Fail("smooth13 csv " + e.Message); }
        }

        /// <summary>
        /// 2026-11-16 "smooth13x" — 13 추가 계측: (1) 군중 구간(스폰 정지 없이 런 시간 10분으로 이동해 실제 생성 압박 속에서 이동),
        /// (2) 맵 경계(오른쪽 벽 근처로 옮겨 벽 쪽 이동 → 카메라 고정 구간에서 벽 반대 방향으로 빠져나오며 카메라 추적 재개).
        /// 같은 지표(멈춘 프레임·화면상 x 흔들림·dt 최대)를 보간/보간 없음(-cct-qa-nointerp)으로 비교한다.
        /// 강제 경로: 체력 증가(무피해 아님 — 접촉은 받지만 죽지 않음), 무기 끄기(처치·레벨업 창 없음), 런 시간 이동(SetRunTimeForTests), 플레이어 위치 이동(경계 구간 시작점).
        /// 군중에서 멈춘 프레임은 적에 막힌 경우도 포함한다(로그의 enemies 수와 함께 해석).
        /// </summary>
        IEnumerator Smooth13xRun()
        {
            var args = System.Environment.GetCommandLineArgs();
            PlayerController.QaDisableInterpolation = System.Array.IndexOf(args, "-cct-qa-nointerp") >= 0;
            int wi = System.Array.IndexOf(args, "-screen-width"), hi = System.Array.IndexOf(args, "-screen-height");
            if (wi >= 0 && hi >= 0 && wi + 1 < args.Length && hi + 1 < args.Length)
            {
                Screen.SetResolution(int.Parse(args[wi + 1]), int.Parse(args[hi + 1]), FullScreenMode.Windowed);
                yield return Wait(2f);
            }
            string variant = PlayerController.QaDisableInterpolation ? "nointerp" : "interp";
            Line($"SX screen {Screen.width}x{Screen.height} variant={variant} fixedDt={Time.fixedDeltaTime}");
            var csv = new StringBuilder("variant,combo,segment,dir,frame,dt,playerX,camX,screenX,enemies\n");
            var title = FindFirstObjectByType<TitleView>();
            for (int i = 0; i < 100 && title == null; i++) { title = FindFirstObjectByType<TitleView>(); yield return Wait(0.1f); }
            foreach (var combo in new[] { Combos[0], Combos[3] })
            {
                title = FindFirstObjectByType<TitleView>();
                if (title != null) title.RequestStart();
                yield return StartCombo(combo, false);
                var game = GameManager.Instance;
                if (game == null) { Fail("smooth13x: no game " + combo.label); continue; }
                game.Stats.Apply(new CardEffect(CardEffectType.MaxHpFlat, 1000000f)); game.Player.Heal(1000000f);
                var player = game.Player; var cam = Camera.main;

                // (1) 군중 — 무기를 끈다(강제): 10분대 대량 처치가 레벨업 카드를 연쇄로 띄워 이동 계측이 오버레이 정지로 덮였다(1차 실행).
                int weaponsOff = 0;
                foreach (var w in player.GetComponentsInChildren<MonoBehaviour>()) if (w != null && w.GetType().Name.EndsWith("Weapon")) { w.enabled = false; weaponsOff++; }
                Line($"SX {variant} {combo.label} weapons disabled={weaponsOff} (forced: no kills → no level-up overlays)");
                game.SetRunTimeForTests(600f);
                for (int k = 0; k < 120 && Enemy.Active.Count < 40; k++) { game.Player.Heal(1000000f); yield return Wait(0.1f); }
                yield return ClearOverlays(game, "smooth13x crowd");
                yield return Shot($"sx_{variant}_{combo.label}_crowd", game);
                foreach (var (dirName, dir) in new[] { ("right", new Vector2(1f, 0f)), ("left", new Vector2(-1f, 0f)), ("diag", new Vector2(0.7071f, 0.7071f)), ("diag2", new Vector2(-0.7071f, -0.7071f)) })
                    yield return Measure(game, player, cam, variant, combo.label, "crowd", dirName, dir, csv);

                // (2) 경계
                yield return ClearOverlays(game, "smooth13x edge");
                FreezeWorld(game);
                var rb = player.GetComponent<Rigidbody2D>();
                Vector2 start = new Vector2(BattleArena.MaxX - 3f, player.Position.y);
                if (rb != null) rb.position = start; player.transform.position = start;
                yield return Wait(1.2f); // 카메라가 경계 고정 위치로 따라올 시간
                yield return Shot($"sx_{variant}_{combo.label}_edge", game);
                yield return Measure(game, player, cam, variant, combo.label, "edge", "into_wall", new Vector2(1f, 0f), csv);
                yield return Measure(game, player, cam, variant, combo.label, "edge", "leave_wall", new Vector2(-1f, 0f), csv);
                player.AutoInputOverride = null;
                game.GoToTitle(); yield return Wait(0.9f);
            }
            try { File.WriteAllText(Path.Combine(outDir, $"smooth13x_{variant}.csv"), csv.ToString()); } catch (System.Exception e) { Fail("smooth13x csv " + e.Message); }
        }

        IEnumerator Measure(GameManager game, PlayerController player, Camera cam, string variant, string combo, string segment, string dirName, Vector2 dir, StringBuilder csv)
        {
            player.AutoInputOverride = dir;
            yield return null; yield return null;
            float lastX = player.transform.position.x, lastScreen = cam.WorldToScreenPoint(player.transform.position).x;
            int frames = 0, still = 0, wall = 0, camFixed = 0, overlayFrames = 0; float maxDt = 0f; var deltas = new List<float>();
            float lastCam = cam.transform.position.x;
            float t = 0f;
            while (t < 2f)
            {
                yield return null;
                if (segment == "crowd")
                {
                    player.Heal(1000000f);
                    // 2026-11-17: 무기를 꺼도 재료 게이지(접촉·자연 드롭)로 카드 창이 연쇄해 보간 없음 바텐더 2구간이 비었다 → 군중 계측 중 재료 픽업 제거(강제).
                    foreach (var pk in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)) Destroy(pk.gameObject);
                }
                float dt = Time.unscaledDeltaTime; t += dt;
                float x = player.transform.position.x, sx = cam.WorldToScreenPoint(player.transform.position).x, cx = cam.transform.position.x;
                // 군중 처치로 자연 발생한 레벨업 카드 등은 첫 선택지로 닫는다(정지 중 프레임은 집계 제외 — 오버레이 정지를 끊김으로 세지 않음).
                if (OverlayOpen(game)) { ResolveRewardScreens(game, "smooth13x " + segment); overlayFrames++; lastX = x; lastScreen = sx; lastCam = cx; continue; }
                bool wallStop = Mathf.Abs(dir.x) > 0f && Mathf.Abs(x - BattleArena.ClampX(x + Mathf.Sign(dir.x) * 0.5f, PlayerController.Radius)) < 0.01f;
                if (wallStop) wall++;
                else
                {
                    frames++;
                    if (Mathf.Abs(x - lastX) < 1e-5f) still++;
                    if (Mathf.Abs(cx - lastCam) < 1e-5f) camFixed++;
                    deltas.Add(sx - lastScreen);
                    maxDt = Mathf.Max(maxDt, dt);
                }
                csv.Append(variant).Append(',').Append(combo).Append(',').Append(segment).Append(',').Append(dirName).Append(',').Append(frames).Append(',')
                   .Append(dt.ToString("0.00000", CultureInfo.InvariantCulture)).Append(',').Append(x.ToString("0.00000", CultureInfo.InvariantCulture)).Append(',')
                   .Append(cx.ToString("0.00000", CultureInfo.InvariantCulture)).Append(',').Append(sx.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(Enemy.Active.Count).Append('\n');
                lastX = x; lastScreen = sx; lastCam = cx;
            }
            player.AutoInputOverride = Vector2.zero;
            float mean = 0f; foreach (var d in deltas) mean += d; mean /= Mathf.Max(1, deltas.Count);
            float v = 0f; foreach (var d in deltas) v += (d - mean) * (d - mean); v /= Mathf.Max(1, deltas.Count);
            Line($"SX {variant} {combo} {segment} {dirName}: frames={frames} stillFrames={still} ({(frames > 0 ? 100f * still / frames : 0f):0.0}%) wallFrames={wall} camFixedFrames={camFixed} overlayFrames={overlayFrames} screenDxStd={Mathf.Sqrt(v):0.00}px maxDt={maxDt * 1000f:0.0}ms enemies={Enemy.Active.Count}");
            yield return Wait(0.3f);
        }
    }
}
#endif
