# 1차 기능 PlayMode 복구 — 2026-10-05 (사용자 지시로 중단, 미완)

Orca task `task_2ee36138aeec` / dispatch `ctx_2662ca1634d8`. Unity 6000.3.25f1. 이전: `first-pass-r7-2026-10-05.md`.
**상태: 미완(사용자 중단 지시).** 러너 정지 원인은 찾고 고쳤으며 실제 PlayMode XML을 얻었다. 남은 실패 6건은 분석 도중 중단했고 수정하지 않았다. r8 빌드/ZIP 없음(r7 그대로).

## 1. 실행 환경

- 격리 사본 `C:\UnityScratch\CCT` (robocopy /MIR, Library/Temp/Logs/UserSettings/Builds/Delivery 제외). Assets·Packages·ProjectSettings SHA-256이 실제 프로젝트와 1867개 파일 전부 일치(러너 임시 InitTestScene만 추가), r7 빌드 로그(08:28:23)보다 새 소스 없음 — 첫 실행은 r7 소스 그대로.
- 세이브/설정: `TestSaveIsolation`이 `%TEMP%\DefaultCompany\CocktailCoffeeTea\cct_playmode_*`로 리다이렉트(로그로 확인). 실제 사용자 세이브·`first-pass-preview-user-data`·사용자 Unity/프리뷰 프로세스는 건드리지 않음. 강제 AutoTour 미사용.

## 2. 러너 "크래시/행" 원인 (재현)

| 실행 | 결과 | 증거 |
|---|---|---|
| play1 (전체, r7 소스) | 09-25의 정지 지점(플레이모드 도메인 리로드 후 backup scene 로드)은 **이번엔 통과**. 이후 `EspressoLeviathanDriverTests` SetUp에서 무진행, 로그는 매 프레임 "no audio listeners" 경고만 432MB. 코디네이터 결정으로 내 PID 25712만 종료 | `C:\UnityScratch\pm\play1.log` (XML 없음) |
| esp1 (Espresso 클래스만) | `DripRain_DoesNotSpawnZones_BeforeTelegraphElapses` START 후 196초간 END 없음 → **UTF 기본 180초 타임아웃이 발동하지 않음** 확인, 종료 | `logs/playmode-progress-2026-10-05-esp1-stall-repro.txt`, `C:\UnityScratch\pm\esp1.log` |
| esp2 (DripRain 단독 + 하트비트) | t≈5 `Playing` → t≈9.1 `GameOver`, `timeScale=0`, 이후 Time.time 9.1 고정 | `logs/playmode-progress-2026-10-05-esp2-stall-repro.txt` |

**원인:** Espresso 테스트는 레이피어를 떼고 보스 옆에 플레이어를 세워 둔다. 무기 없는 플레이어가 보스+일반 몹에게 ~8초 안에 죽고 → `EndRun`이 `timeScale=0` → 테스트의 `WaitForSeconds(10.7)`이 영원히 끝나지 않는다. UTF `TimeoutCommand`는 코루틴이 양보할 때만 검사하므로(WaitForSeconds에 묶이면 양보 안 함) 180초 타임아웃도 안 걸려 **스위트 전체가 무한 대기**. 09-25의 에디터 재설치 가설과 무관.

## 3. 수정 (테스트 코드만, 게임 코드·아트·경제·걷기 무변경)

- `Assets/_Project/Tests/PlayMode/EspressoLeviathanDriverTests.cs`: SetUp에서 `MaxHpFlat 100000` 적용(같은 클래스 폭발 테스트·DripSpecter 테스트와 같은 방식). 단언은 그대로.
- 새 `Assets/_Project/Tests/PlayMode/TestProgressLog.cs` (`TestRunCallback`): 테스트 START/END/결과와 5초 하트비트(frame, dt, state, 적 수, timeScale)를 `-cctTestProgress <파일>`에 즉시 기록. **정지 감시:** 한 테스트 안에서 timeScale=0이 실제 30초 이어지면 LogError로 그 테스트를 실패 처리하고 timeScale=1로 풀어 다음 테스트로 진행(진행 파일 인자와 무관하게 항상 켬). .meta는 격리 사본에서 생성된 것을 실제 프로젝트로 복사.

## 4. 실제 PlayMode 결과 (play2, 수정 후 전체)

`logs/playmode-results-2026-10-05-functional-play2.xml` (원본 `C:\UnityScratch\pm\play2.xml`), 진행 `logs/playmode-progress-2026-10-05-functional-play2.txt`, 로그 `C:\UnityScratch\pm\play2.log`(118MB, 미복사).
**total 134, passed 128, failed 6, skipped 0**, 2026-10-05 12:39:22Z~12:42:57Z (215초). 무한 대기 없음. Espresso 5/5 통과.

| 실패 테스트 | 메시지 | 중단 시점 판단(미검증) |
|---|---|---|
| HybridWeaponTests.ChoosingHybrid_UnlocksAndAttachesBow_WithoutRemovingRapier | Expected Playing, was LevelUp | 재료 5000으로 쌓인 다음 보상이 해금 직후 이어서 열림 — 보상 대기열 도입 후 테스트 기대가 낡은 것으로 보임 |
| HybridWeaponTests.HybridBow_FiresAtEnemy_… | STALL 30초 (state=LevelUp) — 새 정지 감시가 잡음 | 위와 같은 원인으로 LevelUp 중 WaitForSeconds. 감시가 없었다면 다시 무한 대기했을 지점 |
| MaterialCollectionTests.RunEnd_AddsThisRunsMaterials_ToPermanentTotals | "런이 끝나기 전 영구 누적 0이어야" Expected 0, was 4 | 선택 창 열림 시 `SaveOnSelectionOpen→SetRunProgress`(08:155 체크포인트)가 런 중 누적에 반영하는지 확인 중이었음 — 스펙 변경인지 결함인지 미판정 |
| PauseTests.SkillTreeButton_DisabledOverCardSelect_WithReason_AndEnabledWhilePlaying | Expected LevelUp, was Playing | 미분석 |
| SettingsViewTests.ToggleFullscreen_And_ToggleVSync_FlipUnityState | Expected False, was True | 미분석. 배치모드에서 실제 화면 상태 변경이 반영되지 않는 환경 요인 가능성(미확인) |
| TitleViewTests.OpeningEachSubScreen_HidesTitleRoot_AndClosingRestoresIt | "하위 화면을 열면 타이틀 루트는 숨어야" Expected False, was True | 미분석 |

## 5. 남은 일 / 미확인

- 위 6건의 원인 판정과 수정, 수정 후 PlayMode 재실행, 영향 EditMode 확인 — **사용자 중단으로 미수행**.
- 테스트 파일 변경에 대한 EditMode 재실행 미수행(PlayMode 어셈블리 전용 변경이라 EditMode 영향은 없을 것으로 보이나 미검증).
- 게임 코드 변경이 없으므로 r8 빌드/ZIP은 만들지 않았다. r7 빌드·ZIP 그대로.
- "no audio listeners" 매 프레임 경고로 PlayMode 로그가 수백 MB가 된다(기능 결함 아님, 미수정).
- 커밋·리셋·푸시 없음.
