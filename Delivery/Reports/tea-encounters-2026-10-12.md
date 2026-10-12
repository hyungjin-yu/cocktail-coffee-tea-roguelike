# Tea encounters QA (Claude, 2026-10-12)

Scope: Claude owned only AutoTour.cs (new `tea-encounters` mode) + 2 focused test files. No production code/art/package/launcher/git changes. User game/save untouched; all runs isolated data dirs, settings volume 0.

## Files added/changed by Claude
- Assets/_Project/Scripts/Core/AutoTour.cs: `TeaEncountersRun` + dispatch `-cct-autotour-mode tea-encounters`
- Assets/_Project/Tests/EditMode/TeaEncounterRosterTests.cs (5)
- Assets/_Project/Tests/PlayMode/TeaEncounterBossTests.cs (7)

## Tests (current working tree incl. Codex uncommitted changes)
- EditMode TeaEncounterRosterTests 5/5 — edit.xml
- PlayMode TeaEncounterBossTests 7/7 + EnemyMotionAndTeaStudyTests 3/3 = 10/10 — play.xml
- QA tour build: Build Finished, Result: Success — build-qatour/, qatour.log

## Visible muted QA windows (tour-t720, tour-t1080; 17 shots each)
| check | 720 | 1080 |
|---|---|---|
| map | bg_tea | bg_tea |
| natural pool (60s/200s) | 새싹 찻잎 정령, 차시 궁수, 케틀 캐논, 티백 임프, 슈가 큐브 바머, 리프 워든, 차 연구 정령; cocktail 0 | 새싹 정령, 케틀 캐논, 티백, 슈가, 리프 워든, 가디언; cocktail 0; 차시 궁수 not drawn in the 16s sample (logged FAIL = sampling, not a bug; pool tests show archer share ≈ kettle) |
| 차시 궁수 real movement | retreat 1.43→3.83 at v=2.0, move clip frames 0→3 cycling, facing toward player every frame (facingWrong 0), clipSwitches 5 in 4s (move→action→idle), then stable idle | same (switches 4) |
| 개완 수호자 | ChargerDriver, clips charge_prepare/charge/idle/move, Mid, kill: no specialty, beans, run continues | same |
| 김 서린 주전자 장로 | DripSpecterDriver, appear/drip/vanish, tea pool zones 4, coffee pool 0, Mid reward | same |
| 차 화로 용 | EspressoLeviathanDriver, steam/rain, tea pool 5, coffee 0, kill specialty +3 | same |
| 까다로운 차 손님 | CustomerBossDriver, refund/summon, kill → cleared, result | same |
| errors/exceptions | 0/0 | 1 (archer sampling above)/0 |

## Observations / blockers
- No blocker for drivers, rewards, tea pool, archer motion.
- 까다로운 차 손님 reuses difficult_customer art (by design in Codex's def; not new art).
- Top-right HUD line "우림 연구 포인트 … [K] 우림 연구 트리" is drawn over the tea-house counter art with no backing and is hard to read at 720 and 1080 (see tea_720_pool_mid.png). Root owns HUD.
- Tour forced paths: big player HP, auto-close rewards, run-time skip, player weapons off during probes, bosses via BossDirector.SpawnNow(tea schedule) then 999999 damage.

## Round 2 (HUD readability, refund feedback, tea spell-block fallback) — r2/
Changed by Claude: HudView.cs (top-right study points: dark padded backing 450x42 at -118, best-fit 16–24, no overlap with gauge/Info/Challenge; refund status: dark backing shown only while suppressed, text "환불 · 강화 무효 N.N초"), CustomerBossDriver.cs (refund telegraph text "빨간 원 밖으로! 안에 있으면 강화 효과 무효", outside-radius banner "환불 회피!"; SpellBlock fallback in tea house uses TeaPoolArt + "뜨거운 찻물" wording; radius/timing/damage unchanged), AutoTour.cs (random-sample absence = info; refund/spellblock shots), TeaEncounterBossTests.cs (+2 tests).
- EditMode focused (TeaEncounterRoster, UiReadability, SaveV2Ui, JobTheme): 23/23 — r2/edit.xml
- PlayMode focused (TeaEncounterBoss 9, EnemyMotionAndTeaStudy 3, JobThemePlay, CustomerBossDriverPattern, BossFlow): 28/28 — r2/play.xml
- Tours r2/tour-t720, r2/tour-t1080: 20 shots each, errors 0, exceptions 0; refund suppressed=True hudBack=True; spellblock fallback teaPool=1 coffeePool=0 (both); 720 logged info "archer not drawn in random sample" (not a failure).
- Strict release: Builds/Windows64 Build Finished Success, CCT.Runtime.dll SHA256 01ED8CA61F7CB29F37F576D43460F841CADAE88CEAF364904F0A056120278942 (contains new strings; QA tour code excluded). log r2/release.log. Not packaged/committed.
