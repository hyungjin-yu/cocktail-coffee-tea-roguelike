# QA — first-sale drink-lab UI fixes (2026-11-02)

The full report is `Delivery/Reports/windows-first-sale-review-2026-11-02.md`. Evidence is under `Delivery/Verification/2026-11-02/windows-first-sale/`.

## Build / compile (separate from runtime)

| Check | Result | Evidence |
|---|---|---|
| Release build `Builds/WindowsFirstSale-2026-11-02` (exe sha256 `3070e580…d653`) | PASS: build succeeded, 0 `error CS` | `Build-c3-release.log` |
| EditMode full (final tree) | 807 total / 806 passed / 0 failed / 1 inconclusive (existing Assume) | `Final-EditMode.xml` |
| PlayMode focused (final tree) | 60 / 58 passed / 0 failed / 2 skipped (Ignored) | `Final-PlayMode-Focused.xml` |
| PlayMode full (before the DrinkStore fix only) | 334 / 326 / 0 / 8 skipped | `After-PlayMode.xml` |

## Functional and visual runtime (new build, isolated saves)

| Defect / check | Repro steps | Expected | Actual after fix | Status | Screenshot |
|---|---|---|---|---|---|
| D1 notice line invisible | Open 음료 제조, craft, then sell | the notice text is visible | `제조 완료 — …` and `판매했습니다: +150코인` are visible at 720p and 1080p | PASS | before `c1-720/F11_sold.png`; after `c3-720/F11_sold.png`, `c3-1080/F11_sold.png` |
| D2 prepared header overlapped the receipt | Craft any drink, then look at the receipt top | 2 lines, no overlap with 배합 | 2 lines; long names ellipsized | PASS | before `c1-720/F09_craft_done.png`; after `c3-720/F09_craft_done.png`, `c3b-720/states/S05_long_name_sellable_craft_receipt.png` |
| D3 coffee order cancelled after restart | New save, close the app, relaunch, open 주문·판매 | order 3 stays open | state identical after restart | PASS | before `c2-720/reload/firstsale.json`; after `c3-720/reload/R02_orders_after_restart.png` |
| U1 no reason shown on a disabled sell button | 주문·판매 with no drink / wrong kind / under minimum / wrong coffee tool | the reason is shown on the button | `✗ 음료 없음 / 종류 다름 / 재료 부족 / 도구 다름` | PASS (API-crafted states) | `c3b-720/states/S01…S05_*_orders.png`, `c3b-1080/states/` |
| Keyboard-only navigation | — | — | not verified (synthetic keys did not reach the Unity window) | NOT RUN | — |
| Human-paced first-player timing | — | — | not measured | NOT RUN | — |

Totals: PASS 4 · FAIL 0 · BLOCKED 0 · NOT RUN 2.
