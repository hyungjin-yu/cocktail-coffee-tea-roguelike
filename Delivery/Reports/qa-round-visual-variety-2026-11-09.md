# QA report — round visual variety — 2026-11-09

The friend's exact feedback was: "전반적으로 라운드 내내 그래픽의 변화가 없어서 살짝 무료" (the graphics barely change during a round, so it feels a little dull).

Owner: Claude (design + implementation).
Branch: `codex/coffee-brewing`, uncommitted working tree.

Builds on, and preserves:
- `qa-combat-drink-feedback-2026-11-09.md` (hit feel and drink notices);
- `qa-persistent-buff-round-feedback-2026-11-09.md` (buff strip, markers, phase badge, first `RunAmbience`).

Integrated runtime results for all three tasks are in the **Runtime verification** section, which is filled in only from actual runs.

## 1. Investigation: real structure and assets

| Item | Fact (source) |
|---|---|
| Run structure | One run per session. Bosses spawn on a schedule at 150 / 320 / 500 / 680 s (`BossDirector.DefaultSchedule`; tea job swaps the boss defs), and only when no boss is alive. |
| Waves | Every 14 s from both screen edges (`EnemySpawner.SpawnWave`), paused during boss fights. A per-side count grows every 180 s. Spawns double after 420 s (`Balance.LateSpawnStartSeconds`). A "몹 웨이브!" banner already exists. |
| Elites | `EliteBarrage` archetype, weight 0.08 after 120 s (`EnemyRoster`). Previously there was no visual cue on spawn. |
| Bosses | Existing `boss_arrival` ground VFX at the boss's feet (off-screen at spawn), banner, `boss_warning` SFX, boss music, HP bar. The previous task added the badge (`보스 접근 n초` / `보스전`) and lamp tint. |
| Map assets per job (`Resources/ArtExtra`) | `bg_bar_*` and `bg_tea_*` each have exactly **one sprite** each for floor, wall, counter, lamp and boundary. Shared cafe: `cafe_floor/wall/counter/lamp`. `CafeBackground` lays out 1 tiled floor, 1 tiled wall, **6 counters** (x = −30…30, step 12), **6 lamps** (x = −24…36), 1 `ReadabilityVeil`, and boundary posts. |
| Reusable VFX | `steam_jet` (band clip), `SpriteFactory.Circle/Square`. `leaf_mote` belongs to an enemy, so it was not reused for the environment. |

Before this task, the scene looked the same from 0 s to the final boss, apart from transient banners and the boss-only lamp tint.

## 2. Design: what each stage looks like (same camera position)

The stage comes only from real progress, via `RunAmbience.StageFor`:
- **LateNight** if `BossDirector.SpawnedCount ≥ 3` or `RunTime ≥ 420 s` (the real spawn-doubling time);
- otherwise **Midservice** if `SpawnedCount ≥ 1`;
- otherwise **Opening**.

Boss state comes from `RunPhaseBadge.Compute` (actual `BossDirector` + `ActiveBoss`).

| Stage | Composition at the same camera position |
|---|---|
| **Opening** (0 s → first boss) | Clean base scene: original lamp, wall and veil colours; **no halos, no steam**. Only actors and pickups move. |
| **Midservice** (after the first boss) | "Open for service." A **warm halo behind every lamp** (circle, alpha 0.16) and a **warm light pool behind every counter** in the visible wall band. **Occasional steam wisps** from on-screen counters (`steam_jet`, one per 3.2 s, at most 2 alive). **Light shade at the screen's left/right floor edges** (outer step alpha 0.12). Walls keep their original colour. |
| **LateNight** (after the enhanced boss, or ≥ 420 s) | "Closing rush." **Walls darker** (×0.88), **veil alpha +0.04** (night deepens), **deeper amber halos and counter pools** (alpha 0.22), **more frequent steam** (one per 2 s, at most 4 alive), **deeper floor-edge shade** (outer step alpha 0.26; the centre combat area is untouched). |
| **Boss approaching** (real spawn − 15 s) | Lamps pulse slowly red, halos are pulled toward red (steady on Low effects). The badge countdown comes from the previous task. |
| **Boss arrives** (the frame `ActiveBoss` appears) | **0.6 s transition:** lamps and halos dim and flicker twice, then come back red. **Background steam is cleared** so attention goes to the boss. A **red edge cue** (">" / "<") appears at the screen edge where the boss actually spawned. This complements the existing off-screen `boss_arrival`; it doesn't duplicate it. |
| **Boss fight** | Red halos, lamps steady red, veil alpha +0.06 at most. No steam. |
| **Boss defeated** | **0.4 s warm brighten**, then back to the current stage's composition. |
| **Wave spawned** (`EnemySpawner.WaveSpawned`, real sides and count) | Cream edge cues **only on the sides that actually spawned**: 2 per side, or 3 if ≥ 12 enemies actually spawned. They last 0.9 s. |
| **Elite spawned** (`EliteSpawned`, real position) | One larger orange edge cue at the elite's actual height on its side, with a 2 s cooldown. |

Readability and style:
- Halos, steam and the wall/veil sit at Background/BackgroundProp order, below GroundZone hazards, enemies and projectiles.
- Halos stay in the wall band (lamp y = wall base + 2.4, radius 1.05), so they never reach into the lane.
- Edge cues are thin chevrons at the screen edge only (AirVfx order), never in the middle of the lane.
- No camera shake, no change to `timeScale`, no new art. Only the drink-shop props (lamps, counters, steam) are used.

## 3. Implementation (reversible, single owner)

- **`Assets/_Project/Scripts/Core/RunAmbience.cs`** — rewritten as the **only** scene-variety controller.
  - `Attach` returns the existing component if one is already on the background, so there are no duplicate effects.
  - At run start it collects the existing `Lamp`, `Counter`, `Wall` and `ReadabilityVeil` renderers and their base colours, and creates **one `LampHalo` per lamp** once.
  - After that it only changes the **instance `SpriteRenderer.color`** and toggles visibility; no material is ever touched.
  - Wisps are `FadeVfx` objects under the background with a live cap. Edge cues come from a fixed **pool of at most 8** (2 bars each), reused oldest-first.
  - Everything runs on game time, so it freezes during pause.
  - Low effects: no steam, no flicker or pulse, 1 cue per side.
  - Restart: the background is destroyed with the run, and the new run starts again from Opening and base colours.
  - It unsubscribes from spawner events in `OnDestroy`.
- **r2, after comparing the real renders:** the first render showed opening and late night looked too similar, because the lamps sit mostly above the visible wall band and under the HUD. Added one **`CounterGlow`** per counter (6 per map) and a **camera-following floor-edge shade** (`EdgeShade`, 3 steps × 2 sides, Background order, follows the camera x in `LateUpdate`). Both are created once at attach and only recoloured or toggled afterwards.
- **`Assets/_Project/Scripts/Enemies/BossDirector.cs`** — adds read-only `BossesSpawned`, counted in `SpawnNow` for every path (schedule and direct QA/tour calls). `StageFor` and the badge segment use `max(SpawnedCount, BossesSpawned)`.
- **`Assets/_Project/Scripts/Drinks/DrinkUseFx.cs`** — render inspection showed the drink-use ring drawing over the player body. It now draws at Shadow order, flattened at the feet.
- **`Assets/_Project/Scripts/Enemies/Enemy.cs`** — recoil no longer jumps in the first paused frame (dt = 0).
- **`Assets/_Project/Scripts/Enemies/EnemySpawner.cs`** — adds the notify-only events `WaveSpawned(left, right, count)` and `EliteSpawned(def, pos)`. Spawn rules, counts, RNG consumption and caps are unchanged.
- **`Assets/_Project/Tests/PlayMode/RoundVisualVarietyPlayTests.cs`** (new) — 4 UnityTests + 1 Test, plus `RoundVisualVarietyEvidence.CaptureSameViewStages` (`[Explicit]`, offscreen, same camera x = 0). The capture covers bar and tea maps × opening / boss transition / boss fight / midservice / late night / wave cues.

Not touched: balance, enemy stats, spawn and round rules, shop, saves, skins, integer-only rules, hit feel, the buff strip.

## 4. Performance budget

| Item | Count |
|---|---|
| Halos | = number of lamps (6 per map), created once |
| Counter pools | = number of counters (6 per map), created once |
| Edge shade | 6 renderers, created once, repositioned only while visible |
| Steam wisps | ≤ 2 alive (mid) / ≤ 4 (late), 0 during boss or Low effects |
| Edge cues | ≤ 8 pooled objects × 3 transforms |
| Per frame | One `RunPhaseBadge.Compute` (O(1)) and a colour loop over ≤ 6 lamps, ≤ 6 halos and 1 wall, only when the state changes or an animation is running |

## 5. Comparison capture procedure (same viewpoint)

1. Run `CCT.Tests.RoundVisualVarietyEvidence.CaptureSameViewStages` in batchmode **without** `-nographics`, so a GPU device exists offscreen with no window.
2. Frames go to `Delivery/Verification/2026-11-09/round-visual-variety/frames/<Job>_<n>_<stage>.png`, with `frames-notes.txt` recording stage, state, halo, wisp, cue and badge values for each frame.
3. Compare `1_opening` → `2_midservice` → `3_latenight` → `4b_boss_fight` for each job at camera x = 0.

## 6. Runtime verification (actual runs, 2026-11-09, after the user lifted the hold)

This section covers all three 2026-11-09 tasks:
- combat/drink feedback;
- persistent buffs and round badge;
- round variety.

It also covers the integer-only regressions left open in `qa-integer-only-2026-11-06.md`.

### Environment and save protection

- Unity 6000.3.25f1. Windows 11, 16 GB RAM, about 5.2–5.8 GB free during runs. Only one Unity process ran at a time; no user process was stopped.
- Tests ran `-batchmode -nographics -noaudio`. Captures ran `-batchmode -noaudio`, which has a GPU device but no window and does not take focus.
- **User save.** Before any run, `save/drinks/settings.json(.bak)` and the PlayerPrefs registry export were hashed (`Delivery/Verification/2026-11-09/round-visual-variety/user-save-core-hash-before.txt`) and backed up to `%TEMP%\cct-save-backup-2026-11-09`.
  - After all runs, the 6 json files are **byte-identical** (`user-save-core-hash-after-all.txt`).
  - The headless player smoke changed Unity engine-owned PlayerPrefs: `Screenmanager` resolution, fullscreen mode and window position, plus the `unity.player_session_*` / `unity_connect.*` session counters (`playerprefs-diff.txt`). These were **restored by `reg import` of the pre-run export**, and the restored hash equals the pre-run hash `7B6E7FF3…`.
  - Lesson for future runs: launching the player writes engine PlayerPrefs even with `-cct-data-dir`.

### Test results (all paths relative to `Delivery/Verification/2026-11-09/integrated-runtime/`)

| ID | Check | Result | Status | Evidence |
|---|---|---|---|---|
| R-EM | EditMode full (imports the project, generates `.meta` for all new files) | 871 total · 869 passed · 0 failed · 1 inconclusive (existing `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`) · 1 skipped (`IntegerOnlyTests.Migration_UserSaveCopy_IsolatedRun`, ignored by design) | PASS | `EditMode-all.xml`, `.log` |
| R-PM1 | PlayMode full, first pass | 359 · 337 passed · **12 failed** · 10 skipped (Explicit/Ignored captures) | FAIL → triaged below | `PlayMode-all.xml` |
| R-PM2 | Targeted rerun after fixes (9 classes) | 79 · 76 passed · 3 failed | FAIL → fixed | `PlayMode-rerun1.xml` |
| R-PM3 | Combat feedback + round variety rerun, plus captures | 15 / 15 passed | PASS | `PlayMode-rerun2-capture.xml` |
| R-PM4 | Round variety (r2) + persistent buff rerun, plus stage capture | 14 / 14 passed | PASS | `PlayMode-rerun3-round.xml` |
| R-PM5 | Final captures + combat feedback (ring order, HUD layering fix) | 10 / 10 passed | PASS | `PlayMode-capture-final.xml` |
| R-PM6 | **PlayMode full, final source** | 359 · 347 passed · 2 failed · 10 skipped | FAIL → resolved by R-PM7 | `PlayMode-final.xml` |
| R-PM7 | Rerun of the 2 classes failing in R-PM6 (`DrinkLabPlayTests` with its fix, `BattleArenaPlayTests`) | 38 / 38 passed | PASS | `PlayMode-final-rerun.xml` |

Taken together, the final source has every PlayMode test passing in either R-PM6 or R-PM7, and no single full run is green: R-PM6 itself had 2 failures. One is the DrinkLab test whose fix landed after R-PM6 started; the other is the BattleArena flake described in the triage table.

### Triage of the first-pass failures

| Test | Expected → actual | Classification | Fix |
|---|---|---|---|
| `CombatDrinkFeedbackPlayTests.NormalCritHeavy…` | body colour ≠ clear, but was clear | **My test.** The test `EnemyDef` had no colour, so the placeholder was transparent. | Give the test enemy a colour. |
| `CombatDrinkFeedbackPlayTests.Dot_IsQuiet…` | 2 numbers → 3 | **My test assumption.** DOT ticks are spaced at the 0.12 s merge-window edge. | Assert that DOT and direct numbers never merge, and that the DOT sum is correct. |
| `CombatDrinkFeedbackPlayTests.Pause_Freezes…` | offset 0 → 0.09; then `timeScale` 1 → 0 | **My code:** recoil was applied in the first paused frame. **My test:** it didn't wait for the resume countdown. | `Enemy.TickRecoil` returns when dt = 0; the test waits for the countdown. |
| `RoundVisualVarietyPlayTests.Stages…` | Midservice → Opening; then a STALL in SkillSelect | **My code/test:** a direct `SpawnNow` doesn't advance the schedule index; killing the boss opens the real reward screen (`timeScale` 0). | Add `BossDirector.BossesSpawned`; the test ends the boss via `ActiveBoss`, which is the signal the ambience actually watches. |
| `FlameBurstTests` ×3, `PoisonAuraTests` ×1, `HiddenSkillPlayTests.LongSteep…` | 692.5 → 691, 697.5 → 697, 685 → 684, 694.75 → 694, 5.3125 → 5 | **Integer-only regression in test expectations.** Per-tick damage now goes through `IntNum.Damage`. | Expectations use `IntNum.Damage` per tick. |
| `RunHistoryPlayTests.ContactDeath…` | "레몬 브루트 · 몸통 접촉" → "전투 피해" | **Integer-only:** `TakeDamage(MaxHp-0.5)` rounds to MaxHp, so the setup hit itself was lethal. | Use `MaxHp-1`. |
| `IntegerOnlyPlayTests.CoffeeTeaCodexOrders_NoDecimalsVisible` | flagged "1,850m" | **Test regex:** a thousands separator is not a decimal. | The regex now catches `d.d` and `d,d` except a comma followed by exactly 3 digits (validated: `1,850` and `2,000` pass; `1.5`, `1,5` and `12,3456` are caught). |
| `DrinkLabPlayTests.Panel_NoRebuildWhileSameIngredientPours` (`DrinkLabPlayTests.cs:460`) | `PanelRebuilds` 2 → 3; reproduced in R-PM1, R-PM2 and R-PM6 | **Integer-only related.** `BuildSpecs` topology swaps `BuffNone` → `Buff_<stat>` when the integer % first reaches 1; before integerization, `Shown()` exposed any value > 0 from the first mL. The 2026-11-06 test edit (BuffText → MixText, wait up to 3 s) measured across that threshold. This is legitimate behaviour, not a product defect. | The test waits until the effect row exists, then asserts no rebuild while only the amount changes. Passes in R-PM7. Product code unchanged. |
| `BattleArenaPlayTests.HoldingOneDirection(-1)` (R-PM6 only) | STALL, `timeScale` 0 in LevelUp | **Existing timing flake.** The test dismisses popups only inside its 16 s loop; a level-up opening in the final 0.5 s wait stalls it. It passed in R-PM1 and R-PM7, and my changes don't touch level-up or arena logic. | None (reported). |

### Visual inspection (rendered offscreen at 1280×720; I opened and inspected the PNGs)

Files are in `Delivery/Verification/2026-11-09/round-visual-variety/frames/` and `combat-drink-feedback/frames/`, each with `frames-notes.txt`.

| ID | Frame | Observed | Status |
|---|---|---|---|
| V-1 | `Bartender_1_opening` → `_2_midservice` → `_3_latenight` → `_4b_boss_fight`, same camera x = 0 | Opening: no halos or counter pools. Mid: warm pools behind the bar counters plus a steam wisp, badge `구간 2/4`. Late: deeper amber pools, darker wall, badge `위험 4`. Boss: red wall/counter accent, red entry cue at the boss's real side, `보스전 · 그라인더 골렘` badge above the HP bar. The centre floor and actors keep their contrast. | PASS (render) |
| V-2 | `TeaSommelier_*` (tea map) | Same progression on `bg_tea`; boss fight shows the red accent and a right-side cue next to the arriving 개완 수호자. | PASS (render) |
| V-3 | `*_5_wave_edge_cues` | 3 cream chevrons on each side that actually spawned (wave ≥ 12) plus one orange elite chevron. Chevrons stay at the screen edge. | PASS (render) |
| V-4 | `combat-drink-feedback/04b_normal_crit_heavy_f1` | Numbers `12` / `24!` (gold, larger) / `18` (orange); distinct sparks and rings. | PASS (render) |
| V-5 | `02_drink_applied_notice_cup`, `03b_espresso_shot_timed_notice` | `[상시] 시험 커피 적용 · 사거리 +6%`, `[지속 3초] 에스프레소 · 공격 속도 +30%`, cup over the head, buff tiles bottom-left. | PASS (render) |
| V-6 | `09_paused` | Pause panel over the world; strip, badge and notices hidden. | PASS (render) |
| D-1 | First `03b` render | **Defect:** the drink-use ring drew at AirVfx order over the player body. Fixed (Shadow order, at the feet) and recaptured in R-PM5. | FIXED |
| D-2 | Capture tooling | ScreenSpaceCamera canvases sorted under world sprites, so HUD-over-world layering was not representative. Fixed in the capture code only (canvas sort order temporarily +1000). | FIXED (tooling) |

Not judged from frames:
- **Badge size:** in some frames the badge renders larger and softer, because `ReadableScale` used the batch default screen's canvas scale before the render-texture canvas took effect (it refreshes every 0.2 s).
- **Narrow window:** the narrow-window (836×705) layout was **not** rendered.

### QA build and source match

- `CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch -cct-preview-out Builds/QA-RoundVariety-2026-11-09`
- Result: **Succeeded, 0 errors, 521 MB** (`Builds/QA-RoundVariety-2026-11-09/FIRST_PASS_PREVIEW_NOT_RELEASE.txt`, `QA-build.log`). This is a preview build, not a release build.
- Executable: `C:\Users\user\orca\workspaces\CocktailCoffeeTea\coffee-brewing\Builds\QA-RoundVariety-2026-11-09\CocktailCoffeeTea.exe`
  - exe SHA-256 `3070e580e019576b9afcdeb4fec18d03cebc119f6ae84d597412905c749ad653` (Unity player stub, identical across builds);
  - player `CCT.Runtime.dll` SHA-256 `4b7522f8d41639c2354bbc9444d97dee5e3642931dc64cd977f9d9d410d584af`.
- **Source/build match:** no runtime `.cs` under `Assets/_Project/Scripts` (Editor excluded) is newer than the build's `CCT.Runtime.dll` (15:02:37). Runtime source-set hash: `31eb815ada0ee9c1ae945f39f2cf18e9b4d461ea130c2fc70f4fc2394cc903fb` (`runtime-source-set.sha256`). Later edits touched only test files.
- Existing builds, `Play-Latest` and `Play-Windows-Review.cmd` were not modified. Nothing was deployed, committed or pushed.

### Player runtime

| ID | Check | Result | Status |
|---|---|---|---|
| P-1 | Headless isolated smoke: new exe, `-batchmode -nographics -cct-data-dir %TEMP%\cct-qa-round-2026-11-09`, 25 s | Process alive at 25 s; save/settings paths point to the isolated folder; 0 `Exception` / `Error` lines in `QA-build-headless-smoke.log`. This is **boot only (title screen)**. | PASS (boot only) |
| P-2 | Windowed play of the new exe with real keyboard/mouse input: start → mid → late → boss, buff readability | Not run. This needs a visible window and focus, and the earlier no-focus rule was not explicitly lifted for interactive play. | NOT RUN |
| P-3 | Audible sound (hit_crit/hit_heavy pitch, steam, pickup) | All runs used `-noaudio`; only alias and ID logic is test-checked. | NOT RUN |
| P-4 | Narrow window 836×705 and touch layout of strip / badge / notices | Not rendered. | NOT RUN |
| P-5 | Low-effects / shake-off / pause / restart inside the built player | Covered by PlayMode tests only, not in the built player. | NOT RUN (player) |

### Remaining

- P-2 to P-5 above.
- `BattleArenaPlayTests.HoldingOneDirection` popup-timing flake: existing test, not fixed.
- "More fun" (재미 개선) is a player judgement and is **not claimed**: these renders show the scene does change by stage, not that it is more fun.

## 7. Windowed build run with real input (2026-11-09, partial — stopped because of a real blocker)

### Setup

- Build: `Builds/QA-RoundVariety-2026-11-09/CocktailCoffeeTea.exe`. One test player, launched windowed at 1280×720 client size.
- Isolated profile `%TEMP%\cct-qa-windowed-2026-11-09`:
  - `settings.json` with the default accessibility values (shake 50, effects normal, damage numbers on, keyboard scheme);
  - a **read-only copy** of the user's `drinks.json`, so the run uses a real prepared coffee ("원두 커피 추출", moka pot). The original file was never opened for writing.
- Free RAM 4.7 GB.
- Input: `orca computer` returned `app_not_found` for this Unity player (it is visible only in `list-windows`; the same limitation was recorded on 2026-10-29). I used **real OS input** instead: Win32 `SetForegroundWindow` + `keybd_event` / `mouse_event`. Captures are `CopyFromScreen` of the game client rect.
  - These are **real build renders**, unlike the offscreen test renders in §6.
  - Helper scripts: `scratchpad/win.ps1` (not committed). Evidence: `Delivery/Verification/2026-11-09/windowed-build/`.
- **First launch closed itself** shortly after boot (`player.log`: clean shutdown, no error) while I was making failed `orca computer` attempts. I relaunched (`player2.log`, `player3.log`).

### Checks

| ID | Steps (real input) | Observed in the real build | Status | Evidence |
|---|---|---|---|---|
| W-1 | Launch → title | Title menu renders at 1280×720 with no clipping | PASS | `w01_title.png` |
| W-2 | Mouse click 시작 → 바텐더 → 레이피어 | Character and weapon select; prepared drink line `준비 음료: 원두 커피 추출 — 효과 없음` | PASS | `w02_after_start.png`, `w03_after_bartender.png` |
| W-3 | Mouse click 영업 시작 | The button got focus (cyan) but **did not activate**. Enter on the focused button started it. Title and job buttons did activate on click, so the cause is unknown (it may be my synthetic click timing). | FAIL (unexplained, not investigated) | `w04_run_start_0s.png`, `w05_run_start_2s.png`, `w06_after_enter.png` |
| W-4 | Tutorial → mouse click 시작 | Run starts. `[상시] 원두 커피 추출 적용 · 효과 없음` notice; buff strip tiles `증기압 0/10` and `음료 / 효과 없음`; drink aura at the feet; badge `구간 1/4 · 다음 보스 2:29 · 위험 1` | PASS | `w07_run_0s.png` |
| W-5 | Auto-attack (no input) | Real direct hit: number `12` with spark; strip `증기압 1/10` (real moka heat) | PASS | `w08_run_2s.png` |
| W-6 | **Movement scheme flipped to "마우스" by itself, twice**, with the scheme button left selected (cyan). Once within 2 s of the run starting, once while holding A/W. | Only the HUD scheme button changes this setting (`InputSettings.Toggle`). Some input reached the selected button, but the exact trigger was not identified. This is existing HUD code, not part of the 2026-11-09 changes. | FAIL (reproduced twice; root cause not found) | `w08_run_2s.png`, `w14_details_H.png` |
| W-7 | Level-up during play (real time) | Card select renders with three cards, recommendation tag and target hint; a key press picked a card and the run continued | PASS (render) | `w09_scheme_back.png`, `w11_after_move_up_700ms.png` |
| W-8 | Keyboard scheme restored by click; hold A for 1.2 s | Camera and counters shifted about 360 px → player moved left. `[발동] 모카포트 · 증기 폭발 · 기본 피해 120% · 1명 적중`; heavy number `30`; strip reset `9/10 → 2/10` | PASS | `w12_before_move.png`, `w13_after_move_left_1200ms.png` |
| W-9 | H | Details panel `이번 런 효과 · 음료 · 히든 스킬` with `지금 적용 중`: `[누적] 모카포트 · 증기압 5/10 …`, `[상시] 준비 음료 · 원두 커피 추출 · 효과 없음 · 이번 판 내내`; game paused. Wave edge cues visible at both edges behind the panel. | PASS | `w14_details_H.png` |
| W-10 | Esc ×2 (close details, open pause) | **Blocked.** The first Esc was sent while `SetForegroundWindow` reported `fg=False`: the user's own game window **LimbusCompany** (PID 33908, started 14:46) was in front. That keystroke went to the user's app. I stopped all input immediately and closed only my test player (`Stop-Process` on my own PID; no input sent). | BLOCKED | `w15_pause_esc.png` (shows the user's window over the game) |

### Save state after this run

- The user's 6 save/drinks/settings files are byte-identical (`user-save-hash-before.txt` vs `user-save-hash-after.txt`).
- PlayerPrefs were changed by the player run (hash `D9D01BE3…`), then restored with `reg import` to the pre-run hash `7B6E7FF3…`.

### Not done (all still NOT RUN)

- Pause and resume countdown in the real build.
- Restart path (run → title → new run).
- Narrow-window (836×705) real render.
- Effects-low / shake-off in the real build.
- Audible sound: no loopback capture tool was set up; nothing is claimed as audible.

**Real blocker:** the user is active in another game (LimbusCompany). Continuing foreground input would keep taking the user's focus and sending keys to their app.

## 8. Static analysis of the two windowed-run observations (code and existing evidence only — no new runs)

After the Esc incident, the coordinator (msg_d585a39bb64c) stopped all OS input, focus changes, live capture, GUI play and Unity/player runs. Everything below is static analysis: nothing was reproduced again, and nothing was fixed.

### Incident record

- At W-10 (§7), one Esc keystroke reached the user's **LimbusCompany** window. `SetForegroundWindow` had returned `fg=False`, but my helper still sent the key.
- Immediate actions:
  - stopped all input;
  - closed **only my own test player** (`Stop-Process`, no input sent);
  - left the user's app untouched;
  - verified the 6 user save files are byte-identical;
  - restored PlayerPrefs to the pre-run hash `7B6E7FF3…`.
- Process lesson: `fg=False` must abort input. The helper did not enforce that.

### O-1 — mouse click on "영업 시작" only focused it (W-3)

- **Code path.**
  - `CharacterSelectView` builds the button with `MakeButton` → `onClick` → `ConfirmStart()`.
  - `ConfirmStart()` has **no gate or condition**: it records the job and weapon, destroys the canvas and calls `onChosen`.
  - `InputReleaseGate` is not used on this screen.
  - The Enter path, which worked, is the same `onClick` via Submit.
- **Analysis.**
  - The cyan border is the Selectable's selected state. Unity selects on pointer-down, but `onClick` fires only on pointer-up over the same object.
  - So the evidence fits "pointer-down was processed, a matching click was not", which points to the input event sequence, not to game logic.
  - My helper calls `SetCursorPos` (absolute jump, no intermediate move), waits 80 ms, then down → 60 ms → up. `InputSystemUIInputModule` (default actions, `GameBootstrap.cs:113-114`) may resolve the press and release against different pointer states with that synthetic sequence.
  - The same method did activate the title and job buttons, so this is **inconsistent, not deterministic**.
- **Classification.** Not reproduced with human input. The most likely cause is a test-input artifact; a game defect is **not established**. Needs one human-click check.

### O-2 — movement scheme flipped to "마우스" by itself, twice (W-6)

- **Code path.**
  - The setting changes **only** through `InputSettings.Toggle()`, whose only caller is the HUD `MovementScheme` button's `onClick` (`HudView.cs:179-183`).
  - Nothing switches schemes automatically. Mouse vs keyboard reading is just `PlayerController.cs:141`.
  - The button is a normal `Button` with **default (Automatic) navigation**. It persists to settings (`SettingsService.SetMovementScheme`) with no confirmation.
- **Evidence.**
  - Both times the button showed the **cyan selected state** during gameplay (`w08_run_2s.png`, `w14_details_H.png`).
  - The flips happened in windows where no click on it was sent: w07 → w08, and w13 → w14 (only the H key).
- **Plausible mechanism (not proven).**
  - With default UI actions (`AssignDefaultActions`), gameplay keys are also UI keys: WASD and arrows are `Navigate`, Enter is `Submit`.
  - While a HUD button holds EventSystem selection during play, a Submit, or a click resolved to it, invokes `onClick`. The selection can come from a previous click on it, or from focus restoration such as `GameManager.cs:396` / `499`.
  - Static reading can't tell which event fired each time. Synthetic input (O-1) may also be involved.
- **Impact if real.** A persisted control setting changes silently mid-run, and the player then moves toward the cursor.
- **Classification.** Observed twice in the real build with synthetic input. Root cause **not determined**; existing HUD code, not part of the 2026-11-09 changes.
- **Candidate fix (not applied).** Give the scheme button `Navigation.Mode.None` and clear the EventSystem selection after its click, so gameplay keys can never submit it. Then verify with a human-input reproduction.

### Exact unverified list (all NOT RUN)

- Pause and resume countdown in the real build.
- Restart path in the real build.
- Narrow-window 836×705 real render.
- Effects-low and shake-off in the real build.
- Audible hit, crit, steam and drink sounds (no loopback capture tool).
- Human-input reproduction of O-1 and O-2.
- Any claim of improved fun.

The earlier XML facts are unchanged: the final full PlayMode run had **2 failures**, and those two classes passed 38/38 in a separate rerun. **No single full run is green.**

## 9. Resumed real-build verification with a guarded, app-targeted tool (2026-11-09, user-approved resume)

### Safety and setup

- **Approval.** The user explicitly approved resuming after being told the state, the remaining checks and the "only when the PC is not in use" condition.
- **Pre-checks.**
  - RAM 6.7 GB free.
  - No other game process; only KakaoTalk, Orca, RAON and the input host had windows.
  - **One** test player: `Builds/QA-RoundVariety-2026-11-09/CocktailCoffeeTea.exe`.
  - Isolated profile `%TEMP%\cct-qa-windowed-r2`; the user's `drinks.json` was copied read-only for one sub-run.
- **Source/build match re-checked:** no runtime `.cs` is newer than the build's `CCT.Runtime.dll`, and the runtime source-set hash is still `31eb815a…`.
- **Input path.** This time `orca computer` resolved the player (`list-apps` / `get-app-state --app pid:<pid> --window-id <hwnd>`).
  - Each action went through `scratchpad/safe.py`, which first does a **read-only** `GetForegroundWindow()` check that the foreground is exactly the player's HWND, plus a process check for LimbusCompany/DNF.
  - It calls `orca computer <action> --app pid:… --window-id …` **without `--restore-window`**, so focus is never forced, then re-checks afterwards.
  - Any mismatch, refusal or error aborts. **No global `keybd_event` / `mouse_event`** was used.
  - Every action reported `post-guard fg_ok=True games=[]`.
- **Keyboard limitation.** `press-key` (Esc) through this tool reported success but **did not reach the game**. The likely cause is that the tool's synthetic key delivery isn't seen by Unity's raw-input keyboard reader. Mouse clicks were delivered. So keyboard paths (Esc, H, WASD, R) were **not testable** with the safe tool, and I did not fall back to global input.

### Checks (real build renders; evidence in `Delivery/Verification/2026-11-09/resume-2/`)

| ID | Steps (guarded mouse input) | Observed | Status | Evidence |
|---|---|---|---|---|
| R2-1 | Title → 시작 → 바텐더 → **one click on 영업 시작** | The run started on the first click | PASS — **O-1 did not reproduce** | `r01_title`, `r02_select`, `r03_weapon`, `r04_after_start_click` |
| R2-2 | Run idle for several seconds; scheme state across all later frames | "조작: 키보드" in every frame (r04–r27, n05–n09); no unprompted flip | O-2 **not reproduced** | `r05_idle_3s` and later |
| R2-3 | Esc via `press-key` | No pause panel (key not delivered by the tool) | BLOCKED (tool limitation) | `r06_pause` |
| R2-4 | Level-up → card-education guide → 건너뛰기 → card picks (queued windows) | Guide, card select and queued second window work by mouse | PASS | `r07_paused_1s`, `r11_after_skip`, `r12_after_card_then_esc` |
| R2-5 | **Pause via HUD II button** → wait 3 s → **게임 재개** | Pause panel; strip, badge and notices hidden; clock frozen at 00:32; "계속..." resume countdown; then play resumed (00:34) | PASS | `r17_pause_panel`, `r18_paused_3s`, `r19_resume_click`, `r20_resume_after` |
| R2-6 | Player died while idle in a swarm (00:41) → result → **다시 시작** → job select → new run | Result screen ("쓰러졌습니다", stats, saved to history); restart returned to job select; the new run started clean at 00:00 / 0 kills | PASS (restart path) | `r21_pause_again`, `r22_after_abandon`, `r23_after_restart`, `r27_low_effects_shake0_run` |
| R2-7 | Settings: effects 보통 → **낮음 (장식 50%)**; shake 50 → 75 → 100 → **0%** | Values shown in the UI and persisted to the isolated `settings.json` (`shakePercent 0`, `effectsLevel low`); a new run played under them | PASS (setting path). Visual effect of Low not distinguishable with only a few enemies; no shake event occurred. | `r24_settings`, `r25_settings_after_clicks`, `r26_shake_cycle`, `r27_low_effects_shake0_run` |
| R2-8 | Settings screen: existing accessibility and audio options | Master 80, BGM 60, SFX 80, scheme, 조작 안내, resolution, window mode, VSync, shake, effects, damage numbers; no clipping at 1280×720 | PASS (render only) | `r24_settings` |
| R2-9 | **Narrow window 836×705** (isolated settings; boot log `display check 1: committed 836x705 windowed, actual 836x705 windowed -> ok`): title, select, weapon, drink-ready splash, run HUD | Menus centred and unclipped. HUD: drink chip top-right below the gauges and guide line; effect strip (`증기압 1/10`, `음료 / 효과 없음`) above the 조작 box; round badge bottom-centre; no overlaps. Text is small but legible. | PASS (render) | `n01_title` … `n05_run_2s` |
| R2-10 | 836×705: card select (×2) and details panel via the HUD 히든 button | Cards unclipped; details panel `지금 적용 중`: `[누적] 모카포트 · 증기압 2/10 …`, `[상시] 준비 음료 · 원두 커피 추출 · 효과 없음 · 이번 판 내내`; resume by button | PASS (render) | `n06_details_narrow`, `n07_details_narrow`, `n08_details_narrow`, `n09_resumed` |
| R2-11 | Audible sound (hit / crit / steam / pickup), volume and mute effect | FFmpeg present, but **no audio capture device** (dshow lists no audio input; no Stereo Mix or loopback; no WASAPI input in this build); no Python loopback modules | NOT RUN (no loopback tool) | — |

### O-1 and O-2 conclusions

- **O-1 (영업 시작 click only focused): not a game defect.** It did not reproduce with the app-targeted tool (R2-1); the earlier failure came from my Win32 helper's synthetic click.
- **O-2 (scheme flips by itself): not confirmed.** No flip occurred in this run, and keyboard input, the suspected Submit path, couldn't be delivered by the safe tool.
  - Observed again: HUD buttons stay EventSystem-selected (cyan) after a mouse click — the pause button (`r20`) and the 히든 button (`n08`).
  - That is the precondition for the §8 hypothesis. It remains **unconfirmed** and **no code was changed**.

### Save state

- The user's 6 save files are byte-identical (`resume-2/user-save-hash-before.txt` vs `user-save-hash-after.txt`).
- PlayerPrefs were changed by the player run (`353B0792…`), then restored to the pre-run hash `7B6E7FF3…`.
- My player was closed with `Stop-Process` on my own PID; the user's apps were not touched.

### Final remaining NOT RUN / BLOCKED

- **Keyboard paths in the real build** (Esc pause/resume, H, WASD movement, R restart) through a **safe** targeted tool: BLOCKED, because the tool's keys aren't delivered to the Unity player. They are only verified by mouse, by the earlier Win32 run (§7) and by PlayMode tests.
- **Audible sound and loopback level checks**, including the effect of volume and mute: NOT RUN (no loopback device or tool).
- **Low-effects spark halving and shake-off with an actual shake event in the real build:** setting persistence PASS; visual difference NOT RUN.
- **Human-input reproduction of O-2:** NOT RUN.
- **Boss / late-stage scenes in the real build:** NOT RUN. Real play only reached about 00:41; stage art is covered by offscreen renders (§6).
- Test record unchanged: the final full PlayMode run had **2 failures**; those two classes passed **38/38** in a separate rerun; **no single full run is green.**
- No claim of improved fun.
