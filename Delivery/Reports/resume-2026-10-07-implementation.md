# 2026-10-07 재개 구현 기록 — G35 부하 도구 · G30 강제 종료 검증

Orca task `task_b9954517075e` / dispatch `ctx_851334fc99a5`. 기준: Unity 6000.3.25f1, r13 소스(미커밋 작업 보존), 기획 05~11·26·27, `Delivery/Reports/requirements-matrix.md`.
**이 기록은 game_complete 판정이 아니다.** G35는 최종 측정이 없어서 **unverified**로 남는다.

## 진행 중 제약 변경 (코디네이터 msg_31f1d7520001 · msg_cc1fae58ed6d)

- 11:44Z 이후 지시: 사용자가 다른 게임을 플레이 중이다. 창 활성화, visible 창 생성, 해상도 변경, 합성 입력, 새 성능 부하 런을 하지 않는다. hidden 실행, 컴파일, 자동 테스트, 파일 기반 G30만 진행한다.
- 180초 본 부하 런(`full1`)은 시작 전에 사용자가 거부했다. **실행되지 않았다.**
- 그 전에 이미 실행한 짧은 smoke 런이 1건 있다(`smoke1`, 11:42:19~11:42:47Z, 1920×1080 전체화면 창 약 28초). 당시 사용자 게임과 자원이 경합했을 수 있다. 화면을 가렸을 가능성도 있다. 이 런은 **참고용으로만** 기록하고 G35 통과 근거에서 제외한다.
- 내가 띄운 검증 프로세스는 남아 있지 않다(종료 확인). 사용자 소유인 `unity mcp` 프로세스 3개와 사용자 게임은 건드리지 않았다.

## 1. G35 — 기존 도구 확인과 누락 도구 구현

### 확인 결과
기존 도구 가운데 10 §3 고정 부하 시나리오에 해당하는 것은 없었다.
- AutoTour: 시각 스모크다.
- PlayVerifier: 입력만 쓰는 정상 플레이다. 헤드리스로 돌기 때문에 FPS를 성능 증거로 쓸 수 없다.
- requirements-matrix G35 행에도 "시나리오 자체 미구현"으로 적혀 있었다.

### 구현 (Unity Assets 코드/Tests, Tools)
| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Core/LoadScenario.cs` (신규, `#if CCT_QA_LOAD`) | 실제 타이틀→선택→바텐더 총 런을 시작한다. 적은 준비 첫 10초에 일반/소환 100, 그 뒤에는 최종 보스(진상 손님)와 최종 소환 보충으로 전체 160을 유지한다. 적 탄 100, 플레이어 탄 80, 장판 10, 픽업 200을 매 프레임 보충한다. 설정은 효과 normal, VSync off(→targetFrameRate 60), 1920×1080을 확인/적용한다. 준비 30초 뒤 180초 동안 프레임별로 프레임 시간, Playing 여부, 5종 개수, working set/private/managed 메모리를 기록한다. 출력은 `performance.csv`, `load-summary.txt`(all/playing-only 통계, 기준별 PASS/FAIL, 환경), `load-log.txt`다. `-cct-loadtest-restarts N`을 주면 포기→결과 재시작 경로로 N회 재시작하고, GC 뒤 managed heap을 `restart-heap.csv`에 남긴다(마지막3/처음3 비율). |
| 같은 파일 | 프로세스 메모리는 Win32 `K32GetProcessMemoryInfo`로 읽는다. 첫 smoke에서 Mono 플레이어의 `Process.WorkingSet64`가 0을 반환해 메모리 기준을 판정할 수 없었다(실측). 측정값이 0이면 메모리 기준은 FAIL이다. |
| `Core/LaunchOptions.cs` | `-cct-loadtest <dir>` (+`-warmup`, `-seconds`, `-restarts`). `CCT_QA_LOAD` 심볼에서만 컴파일된다. `-cct-data-dir`가 없거나 AutoTour와 함께 쓰면 실행하지 않는다. |
| `Editor/BuildRelease.cs` | `CCT.Editor.BuildRelease.Windows64LoadTestBatch`: 1차 프리뷰와 같은 검사에 `CCT_QA_LOAD`만 더한다. 기본 출력은 `Builds/LoadTest`다. 상태 파일에 검증 빌드 용도를 표기한다. **엄격 출시 검사(`Windows64`)는 바꾸지 않았다.** |
| `EnemyProjectile`/`PlayerProjectile`/`BurstZone`/`SlowZone`/`IngredientPickup` | `static ActiveCount`(OnEnable/OnDisable, SubsystemRegistration 초기화)를 추가했다. 게임 규칙에는 영향이 없고 계측에만 쓴다. |
| `Tests/PlayMode/LoadCountersTests.cs` (신규) | 5종 카운터가 생성·파괴와 일치하는지 검증한다. |
| `Tests/EditMode/QaToolIsolationTests.cs` (신규) | 일반 `CCT.Runtime`에 `CCT.LoadScenario`와 `CCT.PlayVerifier` 타입이 없음을 확인한다(검증 도구가 일반 배포에 포함되지 않음). |
| `Tools/Run-G35LoadTest.ps1` | 실행 스크립트. 외부 1초 간격 working set/private 샘플(`ext-memory.csv`)도 남긴다. |

검증 빌드: `C:\UnityScratch\lt\build2`(Runtime DLL SHA256 `1a23d8d5…89bac7`), 빌드 로그 `C:\UnityScratch\lt\build2.log`(Build Finished, Result: Success). `build1`(`f7142f5d…`)은 메모리 측정이 수정되기 전 빌드다.

### smoke 결과 (참고 전용, 통과 근거 아님)
`Delivery/logs/g35-loadtest-smoke-2026-10-07-REFERENCE-ONLY/` — build1, 준비 5초 + 측정 10초, 재시작 2회.
- 환경 기록: Windows 11 (10.0.22631), i7-14700F 28 논리 코어, RAM 16223MB, RTX 5060 (D3D12, VRAM 7896MB), 1920×1080 FullScreenWindow, 모니터 1920×1080@100, vSyncCount 0, targetFrameRate 60, Quality "Ultra", effects normal, debugBuild False.
- 10초 측정: 600프레임, 평균 59.97fps, p95 16.68ms, 최대 17.5ms, 100ms 이상 0, 오류/예외 0.
- 유지 수량: 측정 구간 최소 적 120(준비가 5초뿐이라 100→160 전환 직후였다), 적 탄 100, 플레이어 탄 80, 장판 10, 픽업 204. 평균 적은 159.9다.
- 메모리는 0으로 기록됐다. 이 결함은 build2에서 수정했고, 수정 후 실행 측정은 하지 않았다.
- 한계: 측정이 30+180초가 아니었다. 다른 게임과 동시에 실행됐을 수 있다. 메모리를 측정하지 못했다. **이 결과로 G35를 판정하지 않는다.**

### G35 남은 일
사용자 PC가 비어 있을 때(다른 게임이나 GPU 부하가 없을 때) 아래 명령을 한 번 실행한다. 전체화면 창이 약 4~5분 뜬다.
`powershell -File Tools\Run-G35LoadTest.ps1 -Name <새이름> -Build build2 -Restarts 10` (빌드 경로는 `C:\UnityScratch\lt\<Build>` 기준)
결과 `load-summary.txt`의 RESULT, `performance.csv`, `restart-heap.csv`, `ext-memory.csv`를 `Delivery/Reports/performance.csv`로 옮기고 판정한다. 출시 후보(RC)가 고정되면 RC 소스로 LoadTest 빌드를 다시 만들어 측정해야 한다.

## 2. G30 — 실제 강제 종료와 재실행 (격리 저장, hidden)

스크립트: `Tools/Verify-G30ForcedKill.ps1`. 증거: `Delivery/logs/g30-forced-kill-2026-10-07/` (g30-log.txt, save 스냅샷 3개, 실행별 player.log, 격리 data).
모든 실행은 `-batchmode -nographics -WindowStyle Hidden -cct-data-dir C:\UnityScratch\g30\run1\data`로 했다. 사용자 저장은 건드리지 않았다.

1. **런 시작**: PlayVerifier build6(Runtime `4eb491eb…`)를 썼다. 이 빌드는 저장/런 수명주기 소스(PermanentProgress/SaveSystem/SaveData/GameManager)의 최종 수정 이후에 만들어졌다. 총 런을 입력 대행으로 시작했다. 저장에 activeRun 체크포인트 24.68초가 반영되고 재료가 0보다 큰 것을 확인한 뒤, `Stop-Process -Force`(TerminateProcess)로 종료했다. exit -1.
   - kill 직전 save: revision 12, totalRuns 1, abandoned 0, 칵테일 9/차 5, activeRun `12dada69…`(checkpoint 24.68, resultCommitted false), cards 1, varieties 8.
2. **재실행 1**: 일반 r13 프리뷰(Runtime `e9ac6c12…`)를 썼다. 로드 후 타이틀에서 다시 강제 종료했다.
   - save: revision 13, **abandonedRuns 1**, **activeRun null**, 칵테일 9/차 5/원두 0 **그대로**(체크포인트 보존), cards 1/varieties 8 보존, totalRuns 1 그대로(새 런 아님), cleared/failed 0.
3. **재실행 2**: r13. 타이틀에서 강제 종료했다.
   - save가 재실행 1과 **바이트 동일**하다(SHA256 `E8567D98…609F6D`). 포기는 다시 집계되지 않았다.

판정: 10의 G30 조건 가운데 "activeRun 포기 1회", "체크포인트 보존"은 실제 플레이어 프로세스 강제 종료로 확인했다. "런 이어하기 없음"은 두 가지로 확인했다. 재실행하면 activeRun이 null이 된다(이어갈 런 레코드가 없다). TitleView 메뉴에는 시작/영구 강화/도감/설정/제작 정보/종료만 있다(코드). **pass(파일 기반)로 제안한다.**
미검증: 타이틀 "이전 런은 중단되었습니다…" 알림의 실제 화면 표시. 화면 방해 금지 때문에 캡처하지 못했다. 같은 경로는 EditMode `SaveV2Tests.ForcedTermination_NextLoad_AbandonsOnce_KeepsCheckpointOnly_ShowsNotice`가 검증한다. kill 직전 이후 메모리에 있던 미저장분은 PlayVerifier 출력도 버퍼째 사라져 수치로 대조하지 못했다(설계상 복원하지 않는 부분이다). 출시 후보 빌드에서 다시 확인해야 한다.

## 3. 자동 테스트 (hidden batch, 스크래치 `C:\UnityScratch\CCT`, 소스는 robocopy /MIR)

| 실행 | 결과 | 증거 |
|---|---|---|
| EditMode | **380/380**, 실패·건너뜀 0 (기존 378 + QaToolIsolation 2) | `Delivery/logs/final-2026-10-07-g35g30-edit.xml` |
| PlayMode | **140/140**, 실패·건너뜀 0 (기존 139 + LoadCounters 1) | `Delivery/logs/final-2026-10-07-g35g30-play.xml`, progress `final-2026-10-07-g35g30-play-progress.txt` |

실 저장 경로 검사: 로그에서 `LocalLow/DefaultCompany`는 Unity 테스트 러너의 `TestResults.xml` 기본 사본 저장 줄 1건뿐이다. save.json/settings.json을 읽거나 쓴 기록은 없다.
스크래치에서 생성된 신규 `.meta`(LoadScenario, QaToolIsolationTests, LoadCountersTests, 그리고 실제 프로젝트에 없던 Spec07CardTableTests·RunLifecycleStressTests)를 실제 프로젝트로 복사했다.

## 보존·미변경
- 기존 빌드 r7~r13, ZIP, `Play-FirstPass.cmd` 바로가기, 사용자 저장, ArtSource, 기획 원본은 변경하지 않았다. 기존 미커밋 변경도 유지했다(커밋하지 않음).
- 엄격 출시 검사는 완화하지 않았다. G32 fail은 유지한다.

## 인계
- 코디네이터 msg_200d6bd6eb76의 UI 결함 U1(하이브리드 안내 solid panel/버튼 글자), U2(카드 재료 공용 아이콘), U3(중앙 HUD 어두운 바탕)은 이번 Dispatch에서 착수하지 않았다. 다음 Dispatch로 넘긴다.
- G35 최종 측정은 사용자 PC가 비어 있을 때 위 명령으로 실행해야 한다.
