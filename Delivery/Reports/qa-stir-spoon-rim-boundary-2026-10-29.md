# QA — Stir barspoon rim-boundary false positive (2026-10-29)

Task: `task_617a9318c367`. This covers the PlayMode failure in `CCT.Tests.DrinkLabPlayTests.Stir_BarspoonTipStaysAboveFloorAndInsideWalls`
(full-suite log `Delivery/Verification/2026-10-29/drink-combat-feedback/AllPlayModeTests-AfterRichTextFix.log`).

## Root cause

`DrinkStageView.SpoonOutside` and `Measure` skip spoon pixels that sit above the rim with `n.y > lr.yMax`. `DrinkArt.InsideMask` samples the mask by row with `floor(n.y * h)`.
For `dl_mixing_glass`, `lr.yMax = 51/64` and the last interior mask row is 50. If a shaft pixel's center lands exactly on `n.y == 51/64`, it is not skipped. It is then sampled as row 51, which is outside the mask, so it counts as "outside". The shaft is 2 px wide in frame 3, which gives `outsidePixels=2` and `clearL = clearR = 0` (the whole row is outside the mask).

This happens only when the bob term is exactly 0 (`sin(7t) = -1`). At that point the tip sits exactly on `floor = yMin + 1.5/64`, and the pixel centers fall on whole art-pixel rows. The pose occurs at t = 1.571 s (frame 3). A 60 fps run almost never samples that instant. The full suite was running at about 2.4 ms per frame and hit it. Shrinking the amplitude cannot clear it, because it is not a wall penetration. That is why the report shows `amp=0`.

Fix: the rim test is now `n.y >= lr.yMax` in both `SpoonOutside` and `Measure`, so the rim boundary matches the mask's row-floor semantics. The spoon pose, the placement and the test thresholds are unchanged.

## Changed files

- `Assets/_Project/Scripts/Drinks/DrinkStageView.cs`: rim boundary `>=` (2 places). Adds `StirInsert` / `StirDuration` consts, which the craft routine now uses with the same values as before. Adds the QA hook `PoseSpoonAt(t, ramp)`.
- `Assets/_Project/Tests/PlayMode/DrinkLabPlayTests.cs`: new test `Stir_BarspoonEveryPoseInsideWalls_FrameRateIndependent`. During a real stir it sweeps every pose at 1 ms steps, plus the four exact bob-zero times, using both the real ramp and ramp = 1. It applies the same assertions as the original test (`outsidePixels == 0`, tip ≥ floor, wall clearance ≥ 2 px below 100 px, min tip < 16 px). The original test is unchanged.

## Commands

All runs used `"C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -nographics -noaudio -projectPath <repo> -runTests -testPlatform PlayMode -testFilter <filter> -testResults <xml> -logFile <log>`.
Evidence is in `Delivery/Verification/2026-10-29/stir-spoon-fix/`.

| # | Check | Filter | Evidence | Result |
|---|---|---|---|---|
| 1 | Build/compile (Unity script compile in each batch run) | — | `After-Run1.log` | PASS |
| 2 | Before fix, 1 ms sweep only | `CCT.Tests.DrinkLabPlayTests.Stir_Barspoon` | `Before.xml` | Both passed. The 1 ms grid misses the exact bob-zero pose. |
| 3 | Before fix, sweep plus exact bob-zero times (reproduction) | same | `Before-ExactBob.xml` | New test FAIL: `t=1.571 ramp=1 sf=3 amp=0 above=6.00 clearL=0 clearR=0`, outside = 2. This matches the suite failure. |
| 4 | After fix, run 1/2/3 | same | `After-Run1..3.xml` | PASS ×3 (both tests each run) |
| 5 | After fix, whole fixture | `CCT.Tests.DrinkLabPlayTests` | `DrinkLabPlayTests-After.xml` | PASS, 33/33 |
| 5b | Full PlayMode suite with the fix (run by coordinator/QA, PID 29988) | all | `Delivery/Verification/2026-10-29/drink-combat-feedback/AllPlayModeTests-Final.xml` | Both stir tests PASS. Suite: 324 passed, 1 failed out of 333. The one failure is unrelated: `Camera_FollowsProcess_…_SmoothNoTeleport` (per-frame camera step 43.57 > 40). It passed in #5 and in the earlier suite run, and the camera does not read the spoon code. |
| 6 | Visual runtime screenshot of the stir | — | — | NOT RUN. The brief said not to launch the game GUI. Rendering is unchanged, because only the QA measurement boundary changed. |

## Remaining limitation

At the bob-zero instant, the 2 shaft pixels whose centers sit on the rim line straddle the glass mouth: half of each pixel is inside the rim and half above it. This is the spoon going through the opening, which is intended, not a wall crossing. Layout: the failing suite run and the focused runs share the same geometry (vesselH = 256, i.e. 4 px per art px). `lossyScale` does not enter the vessel-normalized math, and `>=` covers all three float cases at the rim line: exactly on it, just above, just below. A dedicated rerun with `-screen-width 836 -screen-height 705` was blocked because the project was locked by the full-suite run. The camera smoothing flake in #5b is still open as a separate issue.
