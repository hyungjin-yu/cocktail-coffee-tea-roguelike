# QA report — persistent buffs, character markers & round feedback — 2026-11-09

This extends `qa-combat-drink-feedback-2026-11-09.md` (one-off notices and hit feel). That work is kept unchanged; the new code builds on it.

## Feedback addressed

The friend said that, beyond the one-off notices, nothing on screen changes during a round, so they can't tell what they drank or what buff is active.

Owner: Claude (design + implementation). Branch: `codex/coffee-brewing`, uncommitted working tree.

Excluded and unchanged: enemy composition, balance, round rules, new background/character art, shop, saves, skins, deployment. The integer-only fixes and the earlier hit-feel work are preserved.

## Constraint in force

The coordinator relayed that the user is playing another game. That means:
- no screen, input, focus or live screenshot;
- no Unity Editor, batch, build, player, render or heavy test;
- no memory-watch or wait scripts;
- no automatic runs until the user resumes.

Only code edits, light static review and lightweight Roslyn compiles were done. **All runtime checks are NOT RUN.** I am not assuming runtime permission.

## Investigation (what existed before)

| Area | Existing structure | Gap |
|---|---|---|
| Buff display | Top-right `DrinkRunHud` chip: drink name, buff text, coffee status line, preservation line. Hidden skills appear only as a "히든 N · 효과 보기 [H]" button, a 3 s "[발동]" box, and the [H] details panel. | Everything is text. There were no icons, no remaining-time or stack shapes, and no view of *what is on right now* near the play area. The [H] panel listed hidden skills only, not the drink or coffee state. |
| Character / weapon | Hit and trigger effects from the previous task were one-shot only. | No persistent in-world sign of an active drink or state. |
| Round structure | One run: time-scheduled bosses (`BossDirector.DefaultSchedule`: 150/320/500/680 s, mid-bosses → enhanced → final; each spawns only when no boss is alive), mob waves every 14 s (a banner already announces them), and difficulty growing with time (`Balance.TimeSpawnMultiplier`, enemy HP growth). | Segment, danger and boss approach showed only as a banner at the moment of spawn. Each job already has its own background map (bar / tea house / cafe), so no extra map variation was added. |

## Design: before → after

### Scope 1 — Persistent effect strip and details

- **`UI/ActiveEffects.cs` (new).** A read-only collector. Its only inputs are actual state:
  - `GameManager.DrinkBuff` (integer % effects only);
  - `PlayerStats.BonusesSuppressed` and `SuppressedRemaining` (refund seal);
  - `CoffeeBrewRuntime`: espresso charge %, shot remaining time, recharge time, pour-over bloom/pour phase remaining time, moka heat n/10;
  - `HiddenSkillRuntime`: iced cup ready or recharge time, shake combo n/10, bitter wake (HP ≤ 35%), weekend rush remaining time, herb garden or bar tempo while standing/moving, thick mug %.

  Conditional per-hit bonuses (first sip and similar) are not "on now", so they get no tile; they stay in the details panel.
- **`UI/ActiveEffectsStrip.cs` (new).** A row of tiles at bottom-left (desktop) or bottom-centre above the notices (touch).
  - Each tile shows a short name, an icon and a short integer value.
  - Icons reuse the drink/coffee cup sprites; shapes are used where no art exists.
  - The kind is shown by shape, not colour alone:

    | Kind | Shape |
    |---|---|
    | Run-long | border |
    | Timed | remaining-time bar |
    | Stacking | pips |
    | Ready | top/bottom bands |
    | Suppressed | grey icon + `무효 n초` |

  - Up to 6 tiles, or 4 on narrow screens (where text must be scaled up). Beyond that, the last tile becomes a `더보기 +N` bundle.
  - Tapping or clicking the strip opens the details panel, the same as [H].
  - It is visible only on the combat screen, refreshes every 0.1 s and reuses its tile objects.
- **Narrow screens and Korean text.** `DrinkRunHud.ReadableScale` keeps glyphs at ≥ 12 px. Labels and values use best-fit shrinking (min 10) so they don't overflow. On narrow screens the strip moves to y=240 to avoid the enlarged award toast at 150..230. On desktop it spans x 30..408, which clears the 1100-wide award toast that starts at x 410.
- **`UI/HudView.cs`.**
  - Creates the strip and the phase badge.
  - The details panel is retitled "이번 런 효과 · 음료 · 히든 스킬".
  - The panel gains a "지금 적용 중" section. Each row is tagged `[상시]/[지속 n초]/[누적]/[준비]/[무효]` with source, effect and state. Bundled (+N) tiles are always readable here.
  - The existing hidden-skill rows are unchanged.

### Scope 2 — Restrained persistent markers on the character

**`Player/PlayerEffectMarkers.cs` (new)**, attached once per run in `GameManager`. It shows only what is actually active:

| Real state | Marker |
|---|---|
| Prepared drink applied | Flat ellipse at the feet in the drink's colour (`SpriteFactory.Circle`). Sorted at shadow height, below ground hazards (GroundZone) and enemies. Turns grey and faint while the refund seal is active. |
| Pour-over **pour** (range +%) | Dotted ring (20 dots) at the **main weapon's real reach**: rapier/sword slash radius `RapierWeapon.CurrentReach`, or ranged aim range `PlayerProjectileWeapon.CurrentReach`, both read-only. Hidden while suppressed. |
| Pour-over **bloom** (move speed +%) | `water_drop` art droplets at the feet, only while moving. |
| Espresso **shot** (attack speed +%) / moka heat ≥ max−3 | Small rising steam puffs above the head. |
| Iced cup barrier ready | Small cyan dotted ring around the body; disappears as soon as it is used. |

Rules followed:
- Markers are separate child renderers. They never touch the body or weapon renderers, hit tint, skins, weapon pivots or shared materials.
- Puffs and droplets are limited by an interval plus at most 6 alive at once. Effects=Low turns them off, but the information markers (ellipse, rings) stay.
- Everything runs on game time.
- No projectile visuals were changed: crit is decided at the moment of impact, so tinting projectiles would show a property they don't have.

### Scope 3 — Round segment, danger and real boss warning

- **`UI/RunPhaseBadge.cs` (new).** A one-line badge at bottom-centre, just above the boss HP bar.
  - Normal: `구간 n/4 · 다음 보스 M:SS · 위험 m`, with 5 danger pips.
  - From 15 s before the real spawn: `보스 접근 n초 · <boss name>`, shown only while `BossDirector` is enabled and no boss is alive (matching the real spawn condition). Adds a "!" mark that blinks softly; it is steady on Low effects.
  - While a boss is alive: `보스전 · <name> · 위험 m`.
  - The danger tier is `1 + floor(log2(spawn multiplier × enemy HP multiplier))`, from the real `Balance` time formulas, clamped to 1–5. It is integer-only and never decreasing.
  - Wave banners are not duplicated.
- **`Enemies/BossDirector.cs`.** Adds only read-only `SpawnedCount`, `ScheduleCount` and `NextSpec`. The schedule and spawn logic are unchanged.
- **`Core/RunAmbience.cs` (new).** Attached to the existing background. It changes only the instance `SpriteRenderer.color` of the existing `Lamp` props and the `ReadabilityVeil`:
  - Boss approaching: lamps pulse a slow warm red (steady on Low).
  - Boss fight: lamps stay red, and the veil turns slightly red with alpha +0.06 at most, so ground hazards and enemy warnings stay readable.
  - Calm: original colours restored.
  - On the placeholder background (no lamps or veil) it does nothing.

### Hidden-skill read-only accessors

`Player/HiddenSkillRuntime.cs` gains `BarrierRemaining`, `SpeedBurstRemaining` and `BitterWakeActive`. No logic changed.

## Changed files (this extension)

New:
- `Assets/_Project/Scripts/UI/ActiveEffects.cs`
- `Assets/_Project/Scripts/UI/ActiveEffectsStrip.cs`
- `Assets/_Project/Scripts/UI/RunPhaseBadge.cs`
- `Assets/_Project/Scripts/Core/RunAmbience.cs`
- `Assets/_Project/Scripts/Player/PlayerEffectMarkers.cs`
- `Assets/_Project/Tests/PlayMode/PersistentBuffRoundFeedbackPlayTests.cs`

Edited (hunks for this task only; files were already dirty):
- `Scripts/UI/HudView.cs`
- `Scripts/Core/GameManager.cs` — `Markers` and `Ambience` attach + properties
- `Scripts/Enemies/BossDirector.cs`
- `Scripts/Player/HiddenSkillRuntime.cs`
- `Scripts/Player/RapierWeapon.cs` — `CurrentReach`
- `Scripts/Player/PlayerProjectileWeapon.cs` — `CurrentReach`

Unity must import the project to create `.meta` files for the 6 new scripts.

## Build / compile

| ID | Check | Result | Status | Evidence |
|---|---|---|---|---|
| B-01 | Roslyn (Unity 6000.3.25f1 `DotNetSdkRoslyn/csc.dll`), project defines: Runtime (162 files) | exit 0, 0 errors | PASS (compile only) | `Delivery/Verification/2026-11-09/persistent-buff-round-feedback/roslyn-CCT.Runtime.log` (empty = no diagnostics), `roslyn-cscheck.py` |
| B-02 | Editor (21), EditMode tests (70), PlayMode tests (71) against the new Runtime DLL | exit 0, 0 errors each | PASS (compile only) | `roslyn-CCT.Editor.log`, `roslyn-CCT.Tests.EditMode.log`, `roslyn-CCT.Tests.PlayMode.log` |
| B-03 | Unity import / `.meta` generation / Editor compile | — | NOT RUN (user resume required) | — |
| B-04 | Separate QA Windows build | — | NOT RUN | No build path exists |

A compile pass shows only that the code builds; it says nothing about runtime behaviour or visuals.

## Check matrix (all runtime NOT RUN)

| ID | Setup / steps | Expected | Actual | Status | Evidence |
|---|---|---|---|---|---|
| F-01 Drink + timed shot tiles | Espresso run; `Strip_ShowsActualDrink_AndTimedShotWithRemaining_IntegerOnly` | `음료` tile shows the real integer % summary with the cup icon; charge → `샷|공속+30%` (Timed, sorted first, "3초 남음"); after the shot → `샷 대기` | Not run | NOT RUN | — |
| F-02 Suppression / no drink | Pour-over → pour phase → `SuppressBonuses(5)`; no-drink run | Drink tile `무효 n초`, coffee `무효`, range ring hidden; no-drink run: no drink/coffee tile, no feet ellipse | Not run | NOT RUN | — |
| F-03 Cap / bundle / details | Moka + 5 hidden skills forced (≥ 7 effects) | 6 tiles (4 on narrow); last is `더보기|+N`; [H] panel lists every effect's detail plus `[준비]`/`[누적]` tags | Not run | NOT RUN | — |
| F-04 Pause | Open pause | Strip and badge hidden; visible again on resume | Not run | NOT RUN | — |
| F-05 Markers | Pour-over bloom/pour; espresso shot; Low effects | Ellipse while a drink is active; ring only during pour, radius = `WeaponReach`; puffs ≤ 6 alive and none on Low; ellipse stays | Not run | NOT RUN | — |
| F-06 Barrier ring | Iced cup ready → `TryBlockHit` | Ring only while ready; gone as soon as it is used | Not run | NOT RUN | — |
| F-07 Phase badge + ambience | Director kept; `SetRunTimeForTests(10)` → `first.time-5` → `SpawnNow` | Calm `구간 1/4 · 다음 보스 2:20`; disabled director → no warning; enabled → `보스 접근 5초 · <name>`; boss → `보스전 · <name>`; lamps change; veil alpha boost ≤ 0.06; Calm restores the original colours | Not run | NOT RUN | — |
| F-08 Danger tier | `DangerTier_IsIntegerOneToFive_NonDecreasing` | Tier 1 at 0 s, 5 at 1200 s; never decreasing | Not run | NOT RUN | — |
| V-01 Rendered strip/badge/markers | Fresh QA build or headless offscreen capture, 1920×1080 and 836×705 | Readable Korean, no clipping or overlap with award/proc toasts, boss bar, movement-scheme box or joystick (touch) | Not run | NOT RUN | — |
| V-02 Ground-hazard readability during a boss | Boss fight with floor zones and warning rings | Hazards and warnings remain clearer than the boss-fight veil; the drink ellipse never covers hazards | Not run | NOT RUN | — |
| R-01 Regressions | `HiddenSkillPlayTests` (details text "보호막 준비 완료"), `MobileTouchPlayTests`, `DrinkRunHudTests`, `CoffeeBrewCombatPlayTests`, `CombatDrinkFeedbackPlayTests`, `BossFlowTests`, EditMode `SaveV2UiTests` (`HudView.Create(…, null)` path is guarded), `UiReadabilityTests` | All pass | Not run | NOT RUN | — |

**Totals: PASS 2 (compile only) · FAIL 0 · BLOCKED 0 · NOT RUN 13.**

## Known risks to check at runtime

- **Touch layout.** The strip at bottom-centre y=306 may overlap the touch skill/research buttons; this is unverified.
- **Bottom-centre stacking.** The badge (y 92..128, scaled on narrow screens) sits between the boss bar (50..84) and the award toast (150). On narrow screens the scaled badge could touch the toast while it is showing.
- **Shapes.** Hidden-skill tiles without art use generic shapes (circle, crescent, capsule, semicircle, square). The label text is the main identifier.
- **Ring and drops.** The dotted range ring is drawn as a true circle, because the game's hit checks are 2D circles. `water_drop` art falls back to a small circle if it is missing.

## Commands once the user resumes (one batch at a time, enough free memory, isolated saves)

```
Unity.exe -batchmode -noaudio -projectPath <worktree> -runTests -testPlatform PlayMode -testFilter "CCT.Tests.PersistentBuffRoundFeedbackPlayTests" -testResults Delivery/Verification/2026-11-09/persistent-buff-round-feedback/PersistentBuffRoundFeedbackPlayTests.xml -logFile .../PersistentBuffRoundFeedbackPlayTests.log
# then the R-01 suites one by one, then the QA build:
Unity.exe -batchmode -quit -projectPath <worktree> -executeMethod CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch -cct-preview-out Builds/QA-PersistentBuffRound-2026-11-09
```

## Verdict

All three scopes are implemented in source and compile. **No functional or visual runtime check has been done.** This UI change must not be reported as passed until the suites, captured frames (normal and narrow viewport) and a fresh QA build have been inspected.

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
