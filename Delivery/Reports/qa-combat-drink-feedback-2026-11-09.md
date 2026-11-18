# QA report — combat & drink feedback (friend feedback: intuitiveness, hit feel) — 2026-11-09

## Scope

The friend's feedback said the systems and data were good, but:
- it wasn't intuitive when stat boosts and special effects apply, or what they change;
- the drink theme didn't show in actions;
- hits felt weak.

In scope:
1. Short notices when a drink is actually applied, a stat changes, or a special effect triggers. Each notice gives the source, effect, integer values and remaining time. Repeats are merged or put on cooldown. Run-long, timed and instant effects are labelled differently.
2. A short drink-use/trigger visual that reuses existing art and sound and never blocks input.
3. Better enemy hit feedback, with normal, critical and heavy hits told apart. No feedback on invulnerable or 0-damage hits. Quiet DOT ticks. Capped multi-hit effects. No global hitstop and no camera shake on every hit.

Out of scope, and unchanged: numeric balance, enemy behavior, shop, saves, skins, new art. The integer-only fixes from 2026-11-06 are kept. Nothing was committed, pushed or deployed. Existing dirty files from other workers were not cleaned up.

Owner: Claude (implementation). Branch: `codex/coffee-brewing` (uncommitted working tree).

## Environment / blockers

- Free RAM during the session was **448–754 MB**. Total RAM is 16 GB, and the user's game process `DNF` used 3.1–3.7 GB. The task forbids Unity runs below 4 GB free.
- Then the coordinator sent this instruction (msg_05c587331e8d): *the user is playing another game; no screen, input, focus, Unity Editor, batch, build, player or render capture until the user says to resume.*
- So **no Unity process was started** in this session. My only background process was a memory watcher, and I stopped it.
- A stale `Temp/UnityLockfile` (dated 2026-11-06) exists. An unrelated `unity mcp` helper process (PID 22980) was left untouched.
- The user's default save was not touched. All new tests isolate saves and settings under `Application.temporaryCachePath`.

## Source changes (this task's hunks only)

Several of these files were already dirty from earlier work. Only the hunks listed here are this task's.

| File | Change |
|---|---|
| `Assets/_Project/Scripts/Enemies/HitFeedback.cs` (new) | Adds `HitKind {Normal, Critical, Heavy, Dot, Effect}`. Per-kind settings: visible tint (multiplying by white was invisible on real art), tint time, recoil distance, sound ID and spark size/color. Spark budget: 6 per frame and 18 per 0.3 s of game time, halved when Effects is set to Low. |
| `Enemies/Enemy.cs` | `TakeDamage(float)` now forwards to `TakeDamage(amount, HitKind, Vector2? hitPoint)`. Feedback plays only when the integer damage is > 0; invulnerable hits still return early. Per kind: tint, plus a short recoil of only the `Visual` child (the hit-detection root does not move). Hit sound via AudioService. `hit_spark` spawns at the hit point within the budget. Critical and heavy hits add a short `FadeVfx` circle ring (skipped on Low effects). DOT hits get no sound, spark or recoil — only a short tint with a 0.45 s per-enemy cooldown. Everything runs on game time, so it freezes during pause. Test accessors added: `HitTintActive`, `CurrentBodyColor`, `VisualOffset`. |
| `Core/DamageNumbers.cs` | `Show(enemy, dmg, kind)` added; the old overload is kept. Critical: larger gold number with `!` and a 0.1 s pop. Heavy: orange. DOT: small and pale, offset to one side, and never merged with direct-hit numbers. The 0.12 s merge window, the 30-number cap and the damage-numbers setting still apply. |
| `Core/AudioService.cs` | New aliases `hit_crit` (the `hit_enemy` clip at pitch 1.3) and `hit_heavy` (pitch 0.78). No new audio files. Pitch is reset on every voice reuse. The existing 50 ms dedup, 4-voices-per-ID and 24-total limits apply, and pause muting (`CombatSuppressed`) still applies. |
| `Player/HiddenSkillRuntime.cs` | Adds `LastHitCritical` and `DirectKind()`. The drink-crit notice now goes through the new notice queue, with the drink's name as source and repeats shown as ×N. Double Finish → Heavy; Tray Guard → Effect. Repeated activations of the same hidden skill show `×N` instead of being dropped. |
| `Player/PlayerProjectile.cs`, `Player/RapierWeapon.cs` | Pass the hit kind: crit > final chain hit (heavy) > normal; pierce fragments use Effect. The hit point is the real contact point (projectile) or the body edge facing the player (melee). |
| `Player/EnemyHitStatus.cs` | DOT ticks are marked `HitKind.Dot`. |
| `Player/CoffeeBrewRuntime.cs` | Espresso shot: `[지속 3초] 에스프레소 · 공격 속도 +30%` (live countdown on game time), a cup pop and the steam sound. Pour-over bloom/pour: Timed notices with a 20 s per-phase cooldown; the ring pulse and HUD line still update on every change. Moka burst: Heavy hits, `[발동] 모카포트 · 증기 폭발 · 기본 피해 120% · N명 적중`, a cup pop plus steam sound, and a single `CameraRig.Shake(0.08, 0.12)` per burst, scaled by the shake setting. |
| `Drinks/DrinkRunHud.cs` | `DrinkNoticeKind {Run, Timed, Instant}` with tags `[상시]` / `[지속 n초]` / `[발동]` and separate background tones. A key merges into the current notice as ×N; other notices queue (max 3, each shown at least 0.6 s). Optional per-key cooldown. Notices are hidden outside combat (pause, card screens) and cleared at game over. On the first combat frame it shows `[상시] <drink> 적용 · <up to 2 stats> 외 N개` and plays the cup effect. `ShowDrinkEffect` is kept as a compatibility wrapper. |
| `Drinks/DrinkUseFx.cs` (new) | Uses the existing cup sprite (`DrinkStageView.ServeFor`). It rises 0.55 s above the player's head, with a ring at the feet in the drink's color. It follows the player, never blocks input or auto-attack, runs on game time, and fires at most once per 0.4 s. |
| `UI/HudView.cs` | Hidden-skill activation prefix changed from `효과 발동:` to `[발동]`. |
| `Tests/PlayMode/CombatDrinkFeedbackPlayTests.cs` (new) | 8 regression tests; see the matrix below. |
| `Tests/PlayMode/CombatDrinkFeedbackEvidence.cs` (new, `[Explicit]`) | Offscreen evidence capture: renders the camera plus HUD canvas to a 1280×720 RenderTexture as PNG frames, without opening a window or taking focus. Writes to `Delivery/Verification/2026-11-09/combat-drink-feedback/frames`. |

## Build / compile results

| ID | Check | Result | Status | Evidence |
|---|---|---|---|---|
| B-01 | Roslyn (Unity 6000.3.25f1 `DotNetSdkRoslyn/csc.dll`), Unity project defines, 157 Runtime files | exit 0, 0 errors | PASS (compile only) | `Delivery/Verification/2026-11-09/combat-drink-feedback/roslyn-CCT.Runtime.log` (empty = no diagnostics), script `roslyn-cscheck.py` |
| B-02 | Same compile for Editor (21 files), EditMode tests (70 files) and PlayMode tests (70 files), against the new Runtime DLL | exit 0, 0 errors each | PASS (compile only) | `roslyn-CCT.Editor.log`, `roslyn-CCT.Tests.EditMode.log`, `roslyn-CCT.Tests.PlayMode.log` |
| B-03 | Unity import/compile in the Editor (`.meta` creation for 4 new files) | — | NOT RUN (user resume needed) | — |
| B-04 | Separate QA Windows build, e.g. `-executeMethod CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch -cct-preview-out Builds/QA-CombatDrinkFeedback-2026-11-09` | — | NOT RUN | No build path exists yet |

A compile pass shows only that the code builds. It is not evidence of how the game behaves at runtime.

## Functional / visual / edge / regression matrix

Each check below was planned with these steps. None were executed because of the hold.

| ID | Setup / steps | Expected | Actual | Status | Evidence |
|---|---|---|---|---|---|
| F-01 Drink before/after | No-drink run vs espresso run; first combat frame (`DrinkApplied_RunNotice_AndCupFx_ThenEspressoTimedNotice`, frames 01/02) | No notice without a drink; with a drink, `[상시] 시험 커피 적용 · 사거리 +N% …` with integer values, plus the cup rising | Not run | NOT RUN | — |
| F-02 Espresso shot | `Step(1.01, standing)` | `[지속 3초]`→`2초` countdown `에스프레소 · 공격 속도 +30%`, cup pop, steam sound | Not run | NOT RUN | — |
| F-03 Notice merge/queue/cooldown | `RepeatedNotices_MergeAsCount_PourOverCooldownPreventsSpam` | 5 crits → one notice `×5`; 6 different notices → 3 queued; 30 s of pour-over gives far fewer than 10 notices | Not run | NOT RUN | — |
| F-04 Normal/crit/heavy | `NormalCritHeavy_HaveDistinctTintNumberAndSound_NoGlobalHitstopOrShake`, frames 04b_f0–f5 | 3 different tints, numbers `5` / `5!` / `5`, sound IDs hit_enemy/hit_crit/hit_heavy, `timeScale` 1, shake 0, recoil returns to 0 | Not run | NOT RUN | — |
| F-05 Invulnerable / 0 damage | `Invulnerable_ZeroDamage_ShowNoHitFeedback`, frame 06 | No tint, spark, number or recoil; HP unchanged | Not run | NOT RUN | — |
| F-06 DOT | `Dot_IsQuietAndRateLimited_SeparateFromDirectNumbers`, frame 07 | No spark, sound or recoil; the next tick inside the cooldown gets no tint; DOT number kept separate from the direct-hit number | Not run | NOT RUN | — |
| E-01 Many enemies | 24 enemies hit in the same frame; Effects Low (`ManyEnemiesSameFrame_…`, frame 08) | Sparks = 6 (Low: 3); every enemy tinted; numbers ≤ 30 | Not run | NOT RUN | — |
| E-02 Pause | `Pause_FreezesHitFeedback_And_SettingsOffRespected`, frame 09 | `timeScale` 0, tint/recoil frozen, no new notice, HUD notice hidden | Not run | NOT RUN | — |
| E-03 Settings off | damageNumbers=false, shake=0% (same test, frame 10) | No numbers but tint stays; moka burst does not shake at 0% and shakes once at 50% | Not run | NOT RUN | — |
| V-01 Rendered frames | `CombatDrinkFeedbackEvidence.CaptureEvidenceFrames` (headless, offscreen, 1280×720) | 01–12 PNG frames + `frames-notes.txt`; inspect contrast, clipping, and tag/notice readability | Not run | NOT RUN | — |
| V-02 Narrow viewport (836×705) notice scale | Fresh QA build, isolated `-cct-data-dir` | Notice not clipped; `ReadableScale` keeps text ≥ 12 px | Not run | NOT RUN | — |
| R-01 Existing suites | `CoffeeBrewCombatPlayTests`, `DrinkRunHudTests`, `HiddenSkillPlayTests`, `AudioSpecTests`, `IntegerOnlyPlayTests`; EditMode `FirstPassArtTests`, `EnemyDamageEventsTests`, `AudioServiceTests`, `IntegerOnlyTests` | All pass (APIs kept: `TakeDamage(float)`, `DamageNumbers.Show(e, d)`, `ShowDrinkEffect`) | Not run | NOT RUN | — |
| R-02 Audio cost | Source review: aliases share the per-ID/total voice limits; DOT is silent | No extra simultaneous voices beyond existing limits | Source review only, not runtime | NOT RUN | — |

**Totals: PASS 2 (compile only) · FAIL 0 · BLOCKED 0 · NOT RUN 13** (the build check B-03/B-04 plus all 11 runtime checks).

## Commands to run after the user resumes (one batch at a time, ≥ 4 GB free)

```
Unity.exe -batchmode -noaudio -projectPath <worktree> -runTests -testPlatform PlayMode -testFilter "CCT.Tests.CombatDrinkFeedbackPlayTests" -testResults Delivery/Verification/2026-11-09/combat-drink-feedback/CombatDrinkFeedbackPlayTests.xml -logFile .../CombatDrinkFeedbackPlayTests.log
# then the regression suites in R-01 (one invocation each)
# evidence frames (needs a graphics device — omit -nographics, still windowless):
Unity.exe -batchmode -noaudio -projectPath <worktree> -runTests -testPlatform PlayMode -testFilter "CCT.Tests.CombatDrinkFeedbackEvidence.CaptureEvidenceFrames" ...
# QA build:
Unity.exe -batchmode -quit -projectPath <worktree> -executeMethod CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch -cct-preview-out Builds/QA-CombatDrinkFeedback-2026-11-09
```

## Known risks to check at runtime

- Recoil moves only the enemy `Visual` child by ≤ 0.12 world units. Weapon or muzzle anchors on that child would move with it for 0.09 s.
- The tint replaces the earlier white flash, which was invisible on white-tinted real art. Charger and drip-specter `BaseColor` effects are still multiplied in, and alpha is kept.
- Pour-over notice cooldowns use game time. The automated loop advances inside one frame, so it checks the upper limit, not the cooldown expiry.

## Verdict

The code is complete and compiles. **No functional or visual runtime verification has been done.** All runtime checks are NOT RUN because of the coordinator/user hold (and RAM below 4 GB). The UI change must not be called passed until the suites, frames and a fresh QA build have been run and inspected.

## Addendum — integrated runtime run, 2026-11-09 (after the user lifted the hold)

The NOT RUN rows above were executed later on the final source. Full results, triage, frames and the QA build are in **`qa-round-visual-variety-2026-11-09.md` §6**.

| Run | Result |
|---|---|
| EditMode full | 869 / 871 passed, 0 failed |
| PlayMode full on final source | 347 / 359 passed, 2 failed; both classes then 38 / 38 on rerun. The 2 were a DrinkLab integer-threshold test assumption (fixed) and an existing BattleArena level-up timing flake. |
| This task's suite | All tests pass (after the fixes listed in §6 triage) |
| Offscreen frames | Rendered and inspected |
| QA build | `Builds/QA-RoundVariety-2026-11-09/CocktailCoffeeTea.exe`: Succeeded, 0 errors |
| Headless smoke | 25 s boot, PASS |

Still NOT RUN:
- windowed real-input play;
- audible sound;
- narrow-window (836×705) and touch renders.

User save files are byte-identical. Unity engine PlayerPrefs written by the player smoke were restored to the pre-run hash.
