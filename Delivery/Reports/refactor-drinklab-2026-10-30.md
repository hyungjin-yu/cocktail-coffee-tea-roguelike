# DrinkLab refactor — 2026-10-30

Task `task_b2268f81f10f`. Small extractions meant to keep behavior the same, in DrinkStageView, DrinkLabView and DrinkArt. Every existing public API stays in place as a forwarder, so no caller or test hook changed. Gameplay, timing, visuals, balance, localization strings, serialization, asset IDs and scenes are unchanged.

## What moved

| Slice | New file | Moved out of | Kept in the original as |
|---|---|---|---|
| 1. Pour/stir math | `Assets/_Project/Scripts/Drinks/DrinkPourMotion.cs` (static, pure) | `DrinkStageView`: `Rate`, `PourSeconds`, `LeafToolCapacity`, `Ease`, `SpillOnsetAngle` (now takes `FluidVesselProfile`), `BowlHalfWidth`, the max-rate constants, `StirInsert`/`StirDuration`, plus the stir timeline and spoon-pose formulas (ramp, mix window, mix step, frame index, tip height/bob, phase, reach, x, lean) | `DrinkStageView.Rate/PourSeconds` forwarders; `LiquidMaxRate/LeafMaxRate/BeanMaxRate/StirInsert/StirDuration` as const aliases; private `Ease`, `SpillOnsetAngle`, `LeafToolCapacity` forwarders. `PoseSpoon`, `SpoonOutside`, `Measure` and the coroutine stay in the view (they touch RectTransforms) and now call the pure functions. |
| 2. Recipe save/load/share/import | `Assets/_Project/Scripts/Drinks/DrinkRecipeFlow.cs` (plain class, owned by the view) | `DrinkLabView`: editing-index state, import-preview state, save-draft transaction and its messages, preset/recipe load (copy + editing index), load message and shortage line, share-code copy (clipboard), import preview/save | `DrinkLabView.LoadPreset/LoadRecipe/CopyCode/PreviewImport/SaveImport/ImportPreview` keep their signatures. Busy guards, tab switches, name-field updates, notice messages and the tear overlay stay in the view in their original order. |
| 3. Mask geometry/probes | `Assets/_Project/Scripts/Drinks/DrinkVesselGeometry.cs` (static) | `DrinkArt`: `Profile`, `InsideMask`, `MaskSpan`, `AlphaBottom`, `SpoonExtent` and their caches (with their own `SubsystemRegistration` static reset) | `DrinkArt` forwarders with the same signatures. `DrinkArt` keeps sprite/frame lookup, packaging fallback, placeholders, anchors/`LiquidRect`, and the contract lists. |

Message strings were moved word for word. No formula was rewritten: each expression was moved unchanged, and float constant folding is the same.

## Changed files

- Modified (these already had user changes, which are kept): `Assets/_Project/Scripts/Drinks/DrinkArt.cs` (433 → 305 lines), `Assets/_Project/Scripts/Drinks/DrinkLabView.cs` (1537 → 1514), `Assets/_Project/Scripts/Drinks/DrinkStageView.cs` (2785 → 2741)
- New: `Assets/_Project/Scripts/Drinks/DrinkPourMotion.cs`, `DrinkRecipeFlow.cs`, `DrinkVesselGeometry.cs` (plus Unity-generated `.meta`)
- New test: `Assets/_Project/Tests/EditMode/DrinkLabExtractionTests.cs` (+ `.meta`). It has 7 tests: the forwarders match the extracted functions over all ingredients and a grid of tilts and amounts; the stir ramp, mix window and total mix strength (6); the spoon-pose pieces and `BowlHalfWidth`; `SpillOnsetAngle` bounds; the DrinkArt-to-geometry forwarders on a mask/span grid; and the recipe flow's save/edit/load, share and import, with exact message strings.
- No existing tests were changed. No other files were touched. Other modified/untracked user work (DrinkRunHud, Player, UI, ProjectSettings, art PNGs, etc.) was not modified, reverted or cleaned.

## Commands

All runs used `"C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -nographics -noaudio -projectPath <repo> -runTests -testPlatform <P> [-testFilter <F>] -testResults <xml> -logFile <log>`. The game GUI was never opened. Evidence is in `Delivery/Verification/2026-10-30/refactor-drinklab/`.

| # | Run | Tree | Result | Evidence |
|---|---|---|---|---|
| 1 | PlayMode, full | baseline (before edits) | 333 total: **325 passed, 0 failed, 8 skipped** | `Baseline-PlayMode.xml/.log` |
| 2 | EditMode, focused: `DrinkLabExtractionTests`, `DrinkLabTests`, `CoffeeRecipeTests`, `DrinkFluidPhysicsTests`, `DrinkTransferTests`, `DrinkIngredientExpansionTests` | after | **150/150 passed** (including all 7 new tests); compiles with no `error CS` | `Focused-EditMode.xml/.log` |
| 3 | PlayMode, full (includes `DrinkLabPlayTests`, `CoffeeLabPlayTests`, `DrinkIngredientExpansionPlayTests`, `DrinkRunHud*`) | after | 333 total: **325 passed, 0 failed, 8 skipped** — the same 8 skipped tests as baseline (`Baseline-nonpass.txt` = `After-nonpass.txt`) | `After-PlayMode.xml/.log` |
| 4 | EditMode, full | after | 805 total: **804 passed, 0 failed, 1 inconclusive** (Unity exit code 2 because of the inconclusive) | `After-EditMode-All.xml/.log` |
| 5 | `git diff --check` on the 3 modified files, and a trailing-whitespace scan of the 4 new files | after | clean | — |

**Full-suite delta (PlayMode): none.** 325/0/8 both before and after.

**Known camera assertion (43.57 > 40):** it **did not reproduce** in either run. `Camera_FollowsProcess_PourKeepsBothInView_FinalGlassCentered_SmoothNoTeleport` passed in both the baseline and the post-refactor run. Earlier reports show it as intermittent, and nothing was changed to hide it.

**EditMode inconclusive:** `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle` calls `Assume.That(def != null && !DrinkArt.Has(def.bottleId))`. That precondition needs an ingredient with no tin art, and the untracked `dl_tin_*` PNGs in the tree now give every ingredient one. `DrinkArt.Has`/`Sprite` were not changed. No full EditMode baseline was run before the edits, so this is an assessment, not a measured delta.

## Known limits / deferred

- `DrinkStageView` is still large (2741 lines). These parts were deliberately left alone because pulling them out would mean broad changes to rendering and state:
  - the transfer-cycle state machine (`UpdateCycle`/`SetPhase`/`PoseTool`)
  - the coffee close-up (espresso extraction visuals, ~300 lines)
  - the moka/pour-over coroutines
  - the spoon pixel-collision probes (`SpoonOutside`/`Measure`, which need the spoon's RectTransform)

  The close-up and the coffee apparatus are the next cohesive candidates for extraction.
- `DrinkLabView`'s remaining size is UI layout (craft receipt, codex, orders, recipe drawer). Extraction was limited to the recipe flow, as the brief asked. `RecipeLine`/`LookWords` are text formatting and stay in the view.
- In `DrinkArt`, `LiquidRect`/`Anchor`/`DefaultLiquid` (anchor metadata) stay, because placeholder drawing also uses `DefaultLiquid`.
- Pixel-level visual output was not compared by screenshot. Preservation rests on the expressions being moved unchanged, the new tests showing the forwarders match, and the full PlayMode suite (stir/spoon rim, pour and camera tests) giving the same result.
- The work is not committed (no commit was requested).
