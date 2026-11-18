# Windows first-sale review build — 2026-11-02

Orca task `task_b6e79c56abb6` / dispatch `ctx_b5d746815c2c`. Owner: Claude (implementation, tests, build, evidence). Coordinator ran independent usability review in parallel.
Scope: the first-play journey (title → drink lab → inventory → recipe/manufacture → first sale → save/reload) on a new dated Windows review build, 720p and 1080p manufacture UI, and a ZIP extraction smoke. Out of scope and not changed: economy/prices, starter kit amounts, art, planning docs, launchers, combat.

Nothing was committed, pushed, reset, stashed, or cleaned. The real user saves were never used (see §6).

## 1. Source identity

| Item | Value |
|---|---|
| Worktree | `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing`, branch `codex/coffee-brewing` |
| HEAD | `bdc8a4f1cf22fd9ac68d21bdc07b0269841113cb` |
| Baseline | 177 dirty/untracked entries from the user (kept as they are). Snapshot: `Delivery/Verification/2026-11-02/windows-first-sale/baseline-status.txt`, `baseline-tracked.diff` (sha256 `3cd5f329…02c2`) |
| Final | `final-status.txt`, `final-tracked.diff`. Incremental record: `incremental-vs-baseline.diffdiff` (diff of the two diffs) |
| Unity | 6000.3.25f1. Batchmode on the worktree. No editor had the project open; a `unity mcp` helper process was left alone |

## 2. Changes (incremental over the user's baseline)

| File | Status before | Change |
|---|---|---|
| `Assets/_Project/Scripts/Drinks/DrinkLabView.cs` | already user-modified | (a) The notice band under the tabs now runs from `-106` with height 36, where it used to start at `-110` with height 32. (b) The prepared-drink header in the receipt is ellipsized line by line, and its second line now reads `출발 또는 주문·판매에 사용`. (c) When a sell button is disabled, it now shows a reason: `✗ 음료 없음 / 종류 다름 / 재료 부족 / 도구 다름` |
| `Assets/_Project/Scripts/Drinks/DrinkSales.cs` | clean | Added `BlockReason(order, recipe)`. It returns null exactly when `Price > 0` |
| `Assets/_Project/Scripts/Drinks/DrinkStore.cs` | clean | `Sanitize` now validates each order according to its kind. Coffee orders are checked on bean / defined method / `minA > 0`. Other kinds keep the previous check |
| `Assets/_Project/Tests/EditMode/DrinkSalesTests.cs` | clean | +2 tests: `Reload_KeepsOpenCoffeeOrder_StillCancelsBrokenOrders`, `BlockReason_NamesFirstUnmetCondition_NullExactlyWhenSellable` |
| `Assets/_Project/Tests/PlayMode/CoffeeLabPlayTests.cs` | already user-modified | +1 test `OrdersTab_DisabledSellShowsReason_PreparedHeaderStaysTwoLines`. The `CoffeeScreen_TopAndBottomControlsStayInsideFrame` check is now stricter: it measures the notice band's real rendered line height. This adds an assertion; nothing was weakened |
| `Assets/_Project/Scripts/Drinks/DrinkFirstSaleCapture.cs` (+`.meta`) | new | QA harness that only compiles under `CCT_QA_TOUR` and requires `-cct-data-dir`. It clicks real UI buttons. Phases: `sale`, `reload`, `states`. It is not present in the release DLL (checked by string search: 0 hits in release, 2 in QA) |

No ProjectSettings, scenes, art, or economy constants changed. `ProjectSettings.asset` is byte-identical before and after all builds.

## 3. Defects found and fixed

| # | Defect | Evidence (before) | Fix | Evidence (after) |
|---|---|---|---|---|
| D1 | **The drink-lab notice line rendered nothing at 720p or 1080p.** The band was 32 ref-px tall, but a Noto Sans KR line at size 24 needs about 35 px, and with `Truncate` Unity drops the whole line. As a result the player never saw `판매했습니다: +150코인`, `제조 완료 — …`, the starter-kit grant, or errors such as `재료가 부족합니다`. The old test compared against `fontSize*1.1` (26 px), so it passed. This is in committed code, not the user's dirty edits. | `c1-720/F11_sold.png`, `c1-1080/F11_sold.png` (band empty). Harness: `CLIPPED Notice pref=35 box=32` | The band now uses the free 4 px between the tab row bottom (106) and the band. The test now measures the real line height | `c3-720/F09_craft_done.png`, `c3-720/F11_sold.png`: the notice is visible |
| D2 | After crafting, the prepared header in the receipt wrapped to 3 lines and overlapped the `배합 / 합계` row. It also implied that the drink could only be used for departure | `c1-720/F09_craft_done.png`. Harness `OVERLAP '준비된 음료' x '배합'` at both resolutions | Two lines, each ellipsized. Second line `출발 또는 주문·판매에 사용` | `c3-720/F09_craft_done.png`; 16-char name: `c3b-720/states/S05_long_name_sellable_craft_receipt.png` |
| D3 | **Every app restart silently cancelled the open coffee order.** `DrinkStore.Sanitize` rejected orders with `ingB==""` / `minB==0`, but those values are normal for every coffee order. The slot came back only after a run ended. Committed in `2d0a858` | `c2-720/reload/firstsale.json`: `order2:Open` → `order2:Cancelled` | Validation now depends on the order's kind | `c3-720/reload`, `c3-1080/reload`: the exact state line matches |
| U1 | A disabled sell button gave no reason (only ✓/✗ marks) | `c1-720/F05_orders_before_craft.png` | Reason shown on the button | `c3b-720/states/S0{1..5}_*_orders.png` |

Observed and left unchanged:
- Title-screen OVERLAP and `TALL Title/Hint/Label` flags: the boxes touch, but the glyphs visibly do not collide, and these text boxes use Overflow.
- The session notice (starter kit / RP grant) stays appended to every message for the rest of the session. This is existing behaviour.

## 4. Automated tests (actual XML)

| Run | Tree | Total | Passed | Failed | Skipped/Inconclusive | File |
|---|---|---|---|---|---|---|
| EditMode full | baseline (pre-edit) | 805 | 804 | 0 | 1 inconclusive | `Baseline-EditMode.xml` |
| PlayMode full | baseline | 333 | 325 | 0 | 8 skipped | `Baseline-PlayMode.xml`, `-nonpass.txt` |
| EditMode full | after D1/D2/U1 | 806 | 805 | 0 | 1 inconclusive | `After-EditMode.xml` |
| PlayMode full | after D1/D2/U1 | 334 | 326 | 0 | 8 skipped (same 8) | `After-PlayMode.xml` (sha256 `191adca6…`) |
| EditMode full | **final** (+D3) | 807 | 806 | 0 | 1 inconclusive | `Final-EditMode.xml` (sha256 `a7c799e8…`) |
| PlayMode focused: `DrinkLabPlayTests; CoffeeLabPlayTests; DrinkIngredientExpansionPlayTests` | **final** | 60 | 58 | 0 | 2 skipped | `Final-PlayMode-Focused.xml` (sha256 `61d68a2d…`) |

- The inconclusive test is `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`. Its `Assume` precondition can't hold now that every ingredient has tin art. It was already inconclusive in the baseline.
- The 8 skipped tests are `[Ignore]` evidence captures (CoffeeLab ×2, SkinEvidenceCapture ×4, Skins10EvidenceCapture ×2).
- **No full PlayMode run on the final tree.** The final full PlayMode run was killed about 2 minutes in, because the system ran critically low on memory. With the coordinator's approval it was replaced by the focused run above. Between the "after" full run and the final tree, the only runtime change is D3 (`DrinkStore.Sanitize`) plus a QA-only harness file.

## 5. Builds and ZIP

| Artifact | Path | sha256 |
|---|---|---|
| **Final release (non-QA)** | `Builds/WindowsFirstSale-2026-11-02/CocktailCoffeeTea.exe` (method `CCT.Editor.BuildRelease.Windows64ReleaseToBatch`, Mono, no dev build, 1.0.0) | exe `3070e580e019576b9afcdeb4fec18d03cebc119f6ae84d597412905c749ad653`; `CCT.Runtime.dll` `7eea9dfaafbcc41fefb4c2e4967cd8743e7be85d1d34b574240ffbcaf3edb286` |
| Release build log | `Delivery/Verification/2026-11-02/windows-first-sale/Build-c3-release.log` | `2bf68655…f04d`. Result `빌드 완료 … 521MB`; 0 `error CS`; only the Unity licensing-client noise |
| **ZIP** | `Delivery/CocktailCoffeeTea_WindowsFirstSale_2026-11-02.zip` (109,001,862 B, 207 files; the `_BurstDebugInformation_DoNotShip` folder is excluded) | `c99865a7ab6ac43d2e237f60d797d184462dcdba5e51b34880ff6239ad8629e3` |
| QA harness build (not for distribution) | `Builds/WindowsFirstSaleQa-2026-11-02-c3b/` (`CCT_QA_TOUR`). States phase evidence comes from this build; sale/reload evidence comes from `Builds/WindowsFirstSaleQa-2026-11-02` (c3). Both share the release runtime source; the harness file differs | — |
| Earlier candidates (kept, superseded) | `Builds/WindowsFirstSale{,Qa}-2026-11-02-c1`, `-c2` | — |

## 6. Runtime checks

Environment: Windows 11 Pro 22631, windowed 1280×720 and 1920×1080. Every run used a new empty `-cct-data-dir`. The real save `%USERPROFILE%/AppData/LocalLow/DefaultCompany/CocktailCoffeeTea/{save,drinks,settings}.json` has hashes `496e3fa9… / 6bc61041… / 0f9fbe9f…` both before and after all work: unchanged.

**Natural vs. seeded:** the first-sale path uses only the game's own new-save `StarterKit` grant (gin 60 mL, lemon 30 mL, …), which every new player gets. No `DevGrant`, no coin or inventory edits, no combat farming. The `states` phase is labelled separately as **API-crafted** (`DrinkLab.Craft`); it is used only to show the reason labels, and its ingredients are refunded with the real `취소·회수` button.

**Timing:** the times below are automated interaction timing (API-invoked UI clicks plus fixed waits). They are **not** how long a human first-player takes to finish.

| ID | Check | Steps | Expected | Actual | Status | Evidence |
|---|---|---|---|---|---|---|
| R1 | First sale on a new save, 720p | QA c3 `-cct-firstsale-capture`: title 시작 → 음료 제조 → 재료 도감 → 주문·판매 (read order 1: 진 15 + 레몬즙 7.5, 셰이킹) → 제조: row 진, type 30.0, 적용; row 레몬즙, 15.0, 적용; 셰이킹 → 제조 확정 → wait for motion → 주문·판매 → 판매 | coins 0→150, prepared drink consumed, visible confirmation | coins 0→150; `판매했습니다: +150코인` visible; craft motion 5.1 s; sale at 20.7 s and close at 21.6 s of automated time | PASS | `c3-720/F01…F12*.png`, `firstsale.json`, `screen-text.md` |
| R2 | Same, 1080p | as R1 | as R1 | same; close at 21.5 s | PASS | `c3-1080/` |
| R3 | Repeat sale blocked | after R1/R2, read the same-slot button, then call `DrinkLab.Sell` again | no coin change | button disabled `✗ 음료 없음`; API returns `팔 음료가 없습니다…`; coins stay 150 | PASS | `c3-*/firstsale.json` |
| R4 | Save → restart → reload | relaunch the app on the same data dir, `-cct-firstsale-phase reload`, compare the full state line | identical coins, prepared, gin, lemon, orderSerial, soldDrinks, order ids and states; starter kit not granted again | identical at 720p and 1080p | PASS (FAIL at c2 → fixed D3) | `c3-720/reload`, `c3-1080/reload`; earlier failure in `c2-*/reload` |
| R5 | Disabled-sale reasons + long name, 720p/1080p | `states` phase: no drink / tea vs cocktail order / lemon under minimum / coffee with wrong tool / 16-char name | correct reason per order, no overlap or clip | S01 all `✗ 음료 없음`; S02 `✗ 종류 다름` + tea order sellable; S03 `✗ 재료 부족`; S04 `✗ 도구 다름`; S05 sellable, name ellipsized; harness issues = none | PASS (API-crafted) | `c3b-720/states`, `c3b-1080/states` |
| R6 | Layout checks: overlap, clip, reachability in the drink lab | harness measures every captured screen | no text overlap, no clipped text, no off-screen button | none in the drink lab at c3. Remaining flags: title-screen box overlap and `TALL` on Overflow text, inspected visually, see §3 | PASS | `c3-*/firstsale.json` |
| R7 | ZIP isolated extraction: boot / close / reboot | Expand to the new empty folder `%TEMP%/cctfs1102`, start with `-cct-data-dir %TEMP%/cctfs1102-data`, wait 15 s, CloseMainWindow; repeat | boots, exits 0, reuses isolated save | exit 0 twice; `[Launch] data dir` correct; no exceptions | PASS | `zip-smoke/boot3.log`, `boot4.log` |
| R8 | ZIP release: run start → end → restart | Foreground input authorized by the coordinator (≤3 min), aimed at the verified PID 13020 / HWND 920446 with `orca computer`: click 시작 → 바텐더 → 영업 시작 → 시작 → pause button → 런 포기 → 포기; then close and relaunch | run plays, abandon result screen, save persists | run reached 00:14 with 8 kills; result `런을 포기했습니다`, `최근 기록에 저장했습니다`; relaunch `save.json source=Original, v2`, exit 0. Keyboard keys (Return/Esc) were reported as unverified synthetic and did not land; mouse clicks did, as the screenshots confirm | PASS (mouse path) | `zip-smoke/run5*.png`, `run5.log`, `boot6-after-run.log` |
| R9 | Deep extraction path | first extraction under a ~200-char path | — | the player exits with code 1 and logs `Unable to load mono library` (the mono DLL path is 263 chars, over Windows MAX_PATH). The files are byte-identical to the build. This is an environment limitation, not a build defect | NOTE | `zip-smoke/boot1.log` |
| R10 | Human first-player timing | — | — | not measured. A GUI pass with real human pace was not done | NOT RUN | — |
| R11 | Earned (combat-drop) progression to a second sale | — | — | not tested. Only the starter-kit path was exercised | NOT RUN | — |

Count: PASS 9 · FAIL 0 (open) · BLOCKED 0 · NOT RUN 2 · NOTE 1.

## 7. Unresolved / limitations

- There is no full PlayMode run on the exact final tree (it was killed for lack of memory). Use the focused final run plus the full "after" run.
- R1/R2 show the flow working at machine speed. They are not a usability measurement with a human. The coordinator's independent GUI review is the human-side evidence.
- Keyboard input through `orca computer` did not reach the Unity window, so keyboard-only navigation (G31) was not verified here.
- Shutdown graphics warnings reported by the coordinator for c1 (ComputeBuffer GC, d3d12 4096 B unreleased) were not investigated. They are not related to these changes.
- The README and launcher should warn against extracting into very deep folders (R9). This belongs to the coordinator.
