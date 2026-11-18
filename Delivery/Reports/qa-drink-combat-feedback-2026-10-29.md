# QA report — drink combat feedback and prepared-drink HUD

## Scope

Check prepared coffee flow, combat drink feedback, the prepared-drink HUD art, and ingredient-codex text emphasis. Testing used an isolated temporary save; the normal player save was not used.

## Build identity

- Unity: `6000.3.25f1`
- Build method: `CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch`
- Output: `Delivery/Preview-DrinkCombatFeedback-2026-10-29/CocktailCoffeeTea.exe`
- Result: `Succeeded`, 0 errors, 521 MB, 14.793 seconds. This is a preview build, not a release build.
- Build status: [FIRST_PASS_PREVIEW_NOT_RELEASE.txt](../Verification/2026-10-29/drink-combat-feedback/build-status.txt)
- Unity log: `Delivery/logs/drink-combat-feedback-2026-10-29.log`
- Final runtime assembly timestamp: `2026-10-29 09:42:04` local time.
- Executable SHA-256: `3070E580E019576B9AFCDEB4FEC18D03CEBC119F6AE84D597412905C749AD653`
- Runtime assembly SHA-256: `B70549FD13970BC96652A3BB6A4B6211291AD6CB5E6272BD1D8C7A3640914AFF`

## Environment

- Windows desktop, 1920×1080, keyboard/mouse.
- Game save override: `C:/Users/user/AppData/Local/Temp/CCT-DrinkFeedback-Dev` with `-cct-drinklab-dev`.
- A second launch attempt used `C:/Users/user/AppData/Local/Temp/CCT-DrinkFeedback-Icon-Verify`.
- Preview executable; the isolated automated tests below were run headlessly with `-batchmode -nographics -noaudio` after the visual runtime capture attempt was blocked. No audible game window was opened.

## Check matrix

| ID | Setup and steps | Expected | Actual | Status | Evidence |
|---|---|---|---|---|---|
| F-01 | On the isolated profile, open DrinkLab → Coffee, add 20.0 g of beans, confirm espresso, select Bartender, start a run. | The recipe prepares, consumes 20.0 g, and the prepared-drink state reaches the run. | The mix read 20.0 g before confirmation; bean stock changed from 120.0 g to 100.0 g. Prepared espresso was shown at weapon selection and the combat run started. | PASS | [Prepared after craft](../Verification/2026-10-29/drink-combat-feedback/espresso-prepared-after-craft.png); [ready before departure](../Verification/2026-10-29/drink-combat-feedback/espresso-ready-before-departure.png). |
| F-02 | In that run, wait for the espresso event. | The +30% attack-speed effect is communicated during combat; the active drink bonus stays readable. | Toast `에스프레소 샷 · 공격 속도 +30%` appeared. The HUD listed the prepared drink, +4.8% ingredient bonus, espresso shot cooldown, and drink-preservation state. | PASS | [Combat screenshot before icon fix](../Verification/2026-10-29/drink-combat-feedback/combat-before-icon-fix.png) (SHA-256 `B5A9D2A2C2513CF8CE776CF1653086EC137B079619A086170BE2A7B6CD677501`). |
| V-01 | Inspect the prepared-drink icon in the user-reported combat capture and the reproduced run. | A recognizable vessel sprite is visible with the drink's color/fill. | A dark, empty-looking square was visible. Code inspection confirmed the old HUD used an Image without a sprite and only changed its color. | FAIL — defect reproduced | [User-reported empty icon](../Verification/2026-10-29/drink-combat-feedback/reported-empty-prepared-drink.png) (SHA-256 `FE93FBE775F1F8D1C44C05C320CA56ED8DF9CDB05AF38292B8C9648D4B7C8CF3`); [reproduced combat capture](../Verification/2026-10-29/drink-combat-feedback/combat-before-icon-fix.png). |
| V-02 | Repeat the same combat scenario using the final rebuilt executable after the cup-art change. | The method-appropriate cup renders centered in the HUD with the recipe's liquid color and level. | The source now creates a `DrinkVesselView` from `DrinkStageView.ServeFor` and `DrinkStageView.CapacityFor`, fits it to the icon slot, and sets liquid color/volume/turbidity. The final build succeeded. A post-fix screenshot could not be captured: Orca returned `app_not_found` for the launched Unity process, and desktop capture failed with `The handle is invalid`. | BLOCKED — visual runtime proof missing | Build status above. No post-fix screenshot exists. |
| V-03 | Open the ingredient codex in the final build and inspect labels. | Key labels (`맛·향`, `게임 효과`, `획득처`, `문화·산지`, and color/clarity labels) render in bold without clipping. | Rich-text tags and `supportRichText` were added. The updated codex was not visually captured in the final build. | BLOCKED — visual runtime proof missing | Source: `Assets/_Project/Scripts/Drinks/DrinkLabView.cs`; no post-fix screenshot exists. |
| F-03 | Run espresso, pour-over, and moka-pot scenarios and inspect their individual gameplay notices. | Each coffee method produces the intended method-specific feedback and no toast overlap/spam. | Espresso was checked. Pour-over and moka-pot were not run in this QA session. | NOT RUN | None. |
| R-01 | Run `CCT.Tests.DrinkRunHudTests` against the project in Unity PlayMode, headless and muted. | All selected HUD regression tests pass. | 4/4 passed, 0 failed, 0 skipped. Includes narrow-window scaling, chip glyph containment, pause/tutorial visibility, and result-panel bounds. | PASS | [Test XML](../Verification/2026-10-29/drink-combat-feedback/DrinkRunHudTests.xml) SHA-256 `0F95EAF41ADFC787B5DE64AFC38E54140A1C178606B2A1B5F3A4834CE3E36271`; [Unity log](../Verification/2026-10-29/drink-combat-feedback/DrinkRunHudTests.log) SHA-256 `D815416A2F18533C4DF329B73D5B118361377AD71F4906F72BC6F393A5BEBC61`. |
| R-02 | Run `CCT.Tests.CoffeeBrewCombatPlayTests` against the project in Unity PlayMode, headless and muted. | Espresso, pour-over, moka pot, no-coffee behavior, one-time attachment, and deterministic feedback tests pass. | 6/6 passed, 0 failed, 0 skipped. | PASS | [Test XML](../Verification/2026-10-29/drink-combat-feedback/CoffeeBrewCombatPlayTests.xml) SHA-256 `950B8FF085EE8399CB75180401A8222157941937796CD0B94038D23A45A17561`; [Unity log](../Verification/2026-10-29/drink-combat-feedback/CoffeeBrewCombatPlayTests.log) SHA-256 `41020CCBF06F4BE1BF230A0F7092220FFB7EF8761081312DDE05895DE4219CB8`. |

Check totals: **PASS 4 · FAIL 1 · BLOCKED 2 · NOT RUN 1**.

## Automated test commands

Both commands ran sequentially in Unity `6000.3.25f1`, using `-batchmode -nographics -noaudio -runTests -testPlatform PlayMode` and the isolated project test save fixtures. Selected filters were `CCT.Tests.DrinkRunHudTests` and `CCT.Tests.CoffeeBrewCombatPlayTests`. These are focused regression suites, not a complete project test run.

## Code changes in this scope

- `DrinkRunHud`: replaces the color-only placeholder square with a method-appropriate vessel using the existing vessel art, the prepared recipe's look/volume, and a fitted icon slot.
- `DrinkLabView`: bolds codex field labels and enables rich text on the relevant texts.
- Combat feedback code from the previous pass displays espresso, pour-over, moka-pot, and drink-crit feedback through a rate-limited combat toast. Only espresso was observed in this manual run.

## Verdict

Build/compile gate: **PASS**. Espresso preparation and combat-feedback smoke path: **PASS**. Focused HUD and coffee combat automation: **PASS (10/10)**. Prepared-drink icon defect: **confirmed**, source fix applied, and selected HUD tests pass, but post-fix rendered icon display: **BLOCKED**. Codex emphasis runtime display: **BLOCKED**. Method-specific automated gameplay checks for espresso, pour-over, and moka pot pass; those methods were not manually observed in the rebuilt executable. This task is not marked as fully QA-passed until the blocked visual checks are captured from the new executable.

## Evidence integrity

| File | SHA-256 |
|---|---|
| `reported-empty-prepared-drink.png` | `FE93FBE775F1F8D1C44C05C320CA56ED8DF9CDB05AF38292B8C9648D4B7C8CF3` |
| `combat-before-icon-fix.png` | `B5A9D2A2C2513CF8CE776CF1653086EC137B079619A086170BE2A7B6CD677501` |
| `espresso-prepared-after-craft.png` | `633CEE77C751805987564B23A721FF16F72266A0E182E56B51654CA7BDAF0252` |
| `espresso-ready-before-departure.png` | `B7134042D3D85CB6B479B69004BD75DF40FC3E2BD868693CCD0262DA51B15BF9` |
| `build-status.txt` | `678B4B8525398F388AF2956B9EC9740C9745D508EB5B1154A917350F83C05E82` |
| `DrinkRunHudTests.xml` | `0F95EAF41ADFC787B5DE64AFC38E54140A1C178606B2A1B5F3A4834CE3E36271` |
| `DrinkRunHudTests.log` | `D815416A2F18533C4DF329B73D5B118361377AD71F4906F72BC6F393A5BEBC61` |
| `CoffeeBrewCombatPlayTests.xml` | `950B8FF085EE8399CB75180401A8222157941937796CD0B94038D23A45A17561` |
| `CoffeeBrewCombatPlayTests.log` | `41020CCBF06F4BE1BF230A0F7092220FFB7EF8761081312DDE05895DE4219CB8` |
