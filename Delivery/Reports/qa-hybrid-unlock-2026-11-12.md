# QA — 하이브리드 해금 창 실제 새 빌드 검수 · 시험 불안정 T5/T6 원인 분리 · 커밋 기준 의존성 (2026-11-12, task_c32e8efb5111)

기준: 직전 보고 `Delivery/Reports/qa-ui-states-2026-11-12.md`(커밋 `d18536a`, ZIP `CocktailCoffeeTea_UiStates_2026-11-12.zip` sha256 `dd1a8002…a1e589` — 보존 확인). 기준 스킬 `madia-ui-ux`, `game-feel-first` 유지.
Unity 6000.3.25f1. 증거 루트 `Delivery/Verification/2026-11-12/hybrid/`. 1280×720 / 1920×1080 실제 창(QA 빌드 시작 시 `Screen.SetResolution(…, Windowed)` → `ScreenCapture`).

**조작 구분**: QA 빌드 자동 조작. 키 입력은 **QA 키보드 장치 상태 이벤트**(실제 Input System → GameManager Esc 처리 · EventSystem Submit 경로)이며 OS 키보드·사람 조작이 아니다. 사람 직접 플레이 NOT RUN.
**fixture(강제 경로)**: 바텐더·레이피어 시작, 스폰 정지·적 제거, 최대 체력 +100000, 칵테일 카드 3중첩·차 카드 2중첩을 `GrantCardForTests`(실제 받기 경로). **마지막 차 카드는 실제 카드 창(재료 지급으로 유도)에서 선택** → GameManager의 실제 해금 판정(레이피어 + 칵테일3 + 차3)이 해금 창을 연다.
격리: 실행마다 빈 `-cct-data-dir` + `-logFile`. 사용자 세이브 `save.json`(09-28)·`drinks.json`(10-27)·`settings.json`(10-21) 수정 시각 불변. 메모리 ≥4096MB 게이트(`*/gate.txt`), Unity 동시 1개.

## 1. 하이브리드 해금 창 — 기능·시각(실제 새 QA 빌드, 신규 QA 전용 `-cct-autotour-mode hybrid`)

| # | 항목 | steps | expected | actual (720p / 1080p 동일) | 증거(`hybrid720/hy_1280x720_*`, `hybrid1080/hy_1920x1080_*`) | 상태 |
|---|---|---|---|---|---|---|
| H1 | 조건 전 | 칵테일 3·차 2 | 해금 창 없음 | cocktail=3 tea=2 hybrid=False | `_00_before.png` | PASS(fixture) |
| H2 | 차 카드 창 | 찻잎 게이지 채움 | 차 카드 3장 | 열림(차 카드 3장) | `_01_tea_card_window.png` | PASS |
| H3 | 해금 창 열림 | 차 카드 선택 | state=HybridOffer, timeScale 0, 키보드 초점 = 해금 버튼, 설명 넘침 없음 | 일치, 초점 UnlockButton, 설명 1줄(선호 높이 43.5/칸 90) | `_02_hybrid_open.png` | PASS |
| H4 | **Esc → 일시정지(해금 창 위)** | Esc | 일시정지 메뉴 전체가 보이고 해금 창은 유지 | **수정 전 FAIL**: 일시정지가 해금 창 **뒤**에 그려져 '게임 재개·커피 연구·설정·조작 안내'가 가려지고 '런 포기·타이틀로 종료'만 보였다(초점 버튼이 안 보이는 채로 Enter 가능). 원인: `GameManager`가 해금 창을 일시정지보다 **나중에** 만들어 같은 캔버스에서 위에 그림. 수정: 카드·커피 트리처럼 해금 창을 일시정지보다 먼저 생성. **수정 후**: 일시정지 메뉴 전체가 위, 뒤에 해금 창 흐리게 | 수정 전 캡처는 같은 경로 재실행으로 덮어씀 — 결함 화면 묘사는 이 행, 회귀 방지 PlayMode `HybridWeaponTests.PauseOverHybridOffer_DrawsAboveHybridWindow_AndReturnsToIt`. 수정 후 `_03_pause_over_hybrid.png` | PASS(수정 후) |
| H5 | 일시정지 위 Enter 관통 | Enter | 해금되지 않음 | unlocked=False. Enter는 일시정지의 초점 버튼 '게임 재개'를 눌러 일시정지만 닫음 | tour-log `Enter while paused` | PASS |
| H6 | 복귀 | (일시정지 닫힘) | 해금 창으로 복귀, 초점 해금 버튼 | hybridOpen=True, state=HybridOffer, 초점 UnlockButton | `_04_back_to_hybrid.png` | PASS |
| H7 | Enter 해금 | Enter | 해금·창 닫힘·전투 재개·보조 활 | unlocked=True, Playing, timeScale 1, HybridBowWeapon 있음, 등에 활 | `_05_after_unlock_hud.png` | PASS |
| H8 | 해금 후 일시정지·결과 | 일시정지 → 닫기 → 포기 | 결과에 하이브리드 표시 | 결과 "바텐더 · 레이피어 · 하이브리드 티니 보우" | `_06_pause_effects.png`, `_07_result.png` | PASS |
| H9 | 긴 문구 | 고정 문구 1종(제목·설명) | 잘림 없음 | 제목 1줄, 설명 1줄(820 칸) — 현지화 문자열 없음(한국어 고정) | `_02_hybrid_open.png` | PASS(한국어) |
| H10 | 마우스 클릭 해금 | — | — | 버튼 onClick 경로는 기존 PlayMode(`HybridWeaponTests`)·자동 조작에서만, 실제 마우스 NOT RUN | — | NOT RUN |

추가 발견·수정: 정규 범례 마신 음료 없음 라벨 "마신 음료 없음 →"이 라벨 칸 124에서 두 줄로 접힘 → "안 마심 →"(오른쪽 "상성 없음"). 증거 `ui-states720/us_1280x720_bartender_rapier_10_legend_nodrink.png`(같은 새 빌드 ui-states 재실행 39장, 오류 0).

## 2. T5 · T6 원인과 영향 분리(신규 회귀 여부)

| # | 시험 | 근거 대조 | 판정 | 조치 |
|---|---|---|---|---|
| T5 | `BattleArenaPlayTests.HoldingOneDirection_StopsAtWall_AndCameraStopsAtLimit` | 같은 STALL(LevelUp에서 timeScale 0이 30초 지속)이 **2026-11-09** `Delivery/Verification/2026-11-09/integrated-runtime/PlayMode-final.log`(-1.0f)에도 있음 — 이번 변경(11-12) 이전. 2026-10-29 이후 PlayMode 로그 19개 중 2개만 발생. **정정**: 직전 보고의 "카드 교육 시험 뒤에 돌리면" 설명은 틀렸다 — 실패한 실행에서 BattleArena는 **첫 번째로** 실행됐다(`ui-states/playmode-final.log` 723~1112행). 단독 재실행 2회·이번 묶음 2회 모두 통과 | **기존 간헐 불안정, 신규 회귀 아님**. 정확한 원인 미확정(빈 제시 카드는 게임이 이미 건너뜀 — `GameManager.cs` 787행 확인, Choose 무시 조건 중 무엇인지 기록 없음) | 시험 전용 1회 진단 로그 추가(`[BattleArenaDiag]` — LevelUp 600프레임 지속 시 카드 창·안내·일시정지·하이브리드 상태 기록). 게임 코드 변경 없음. 재발 시 원인 확정 가능 |
| T6 | `MasterChallengePlayTests.FullCycle_…` 134행 현수막 3.5초 벽시계 하한 3.3 | 1회 3.296초(`ui-states/playmode-1.xml`). 이전 실패(11-11 steam-master, 11-12 next-wave)는 **다른 단언**(문구·피해 0·TearDown) — 이 하한 실패는 이번 세션 첫 관측. 원인: 설치 타이머는 다음 프레임 `unscaledDeltaTime`으로 진행하는데, 시험은 상태 전환을 본 뒤 `started`를 잰다 — 시작 프레임의 앞부분이 길면(예: 남은 적 처치·연출 생성) 게임 타이머가 시험 벽시계보다 그만큼 먼저 끝난다. 게임 동작(3.5초 정지·피해 0)은 같은 시험의 다른 단언이 모두 통과 | **시험 측정 오차, 게임 회귀 아님**(이번 UI 변경은 마스터 타이머 경로 무관) | 시험 수정(작음·관련): 게임 타이머 `TransitionElapsed ≥ 3.5` 단언 추가 + 벽시계 하한을 관측 최대 프레임 길이만큼 허용(상한 4.4 유지). 이후 실행 통과 |

## 3. 빌드·시험(컴파일/자동 시험 — 시각 판정 아님)

| # | 항목 | 결과 | 증거 | 상태 |
|---|---|---|---|---|
| B1 | QA 투어 빌드(최종) | Success, 소스 2,815 `bfb9e458…24da54f6` | `build-qatour-final.log`, `source-qatour-final.sha256` | PASS |
| B2 | 검토 빌드 `Builds/UiStates-Review-2026-11-12b` | Success, 소스 전후 동일 `bfb9e4580d6ac1ab55caed10cc4e19cd97bbb4bd9a6ee764d298d57924da54f6`(QA 빌드와 동일), dll에 HybridRun/UiStatesRun/DrinkLabCapture/PlayVerifier/UiAuditRun 0건 | `build-review.log`, `source-review-before/after.sha256` | PASS |
| B3 | 검토 빌드 부팅 | 빈 격리 폴더·입력 없음, 타이틀, 예외 0 | `review-build/boot_title_noinput.png`, `review-build/Player.log` | PASS |
| T1 | EditMode 전체 | 949 중 947 통과, 실패 0, 불확정 1·건너뜀 1(기존) | `editmode-final.xml` | PASS |
| T2 | PlayMode 18묶음(하이브리드·일시정지·마스터·BattleArena·범례·카드교육·목표·히든·기록·HUD 등) | 100/100 (`[BattleArenaDiag]` 미발생) | `playmode-final.xml` | PASS |
| D1 | **커밋 기준 의존성**(다른 사용자 미커밋 변경 제외) | 스테이징 트리 `ed02cc70…`(=커밋 내용)만 `git archive`로 꺼내 Roslyn csc로 CCT.Runtime(177)·CCT.Editor(22)·CCT.Tests.EditMode(76, ArtV1 별도 asmdef 제외)·CCT.Tests.PlayMode(72) 컴파일 — 오류 0, `CCT_QA_TOUR` 정의 추가해도 오류 0. 대조: 작업 트리 스냅샷(다른 사용자 변경 포함)도 오류 0 | `depcheck-index-tree.txt`, `depcheck.py` | PASS(컴파일만 — 커밋 트리 단독 Unity 시험 실행·빌드는 NOT RUN) |

**빌드와 커밋의 기준 차이(명시)**: 위 빌드·시험은 작업 트리 전체로 만들었다 — 다른 사용자의 미커밋 추적 파일 39개(예: `CardSelectView.cs`의 `IntNum.Round` 줄, `DrinkArt.cs`, `ProjectSettings/*.asset` 등) 포함. 커밋에는 이번 범위 파일만 들어가며, 그 트리 자체는 D1처럼 **컴파일만** 확인했다.

## 4. 패키지·커밋

- ZIP `Delivery/CocktailCoffeeTea_UiStates_2026-11-12b.zip`(208 항목, testzip 정상, DoNotShip 0) sha256 **68c70619630726e176280ef5df0b655286ac00698d39937a6083ac1664246dd6**. 산출물 목록 `7958825a…2c6e56`(`review-build-files.sha256`), CCT.Runtime.dll `a77f1f53dd3a8b3cc532d549824b7ea5aa72fe633d63fbd17a4f0f4ae2fdb99d`.
- 기존 ZIP 6개(공모전·이전 검토·직전 `UiStates_2026-11-12.zip`) 해시 전후 일치(`existing-zips-before.sha256`). 이전 빌드 폴더 미변경.
- 공개 배포·Steam 업로드 없음. 범위 커밋을 기존 백업 브랜치 `origin/codex/coffee-brewing`에 push.

## 5. PC UI 점검 — 아트 부족 외 남은 FAIL / BLOCKED / NOT RUN

- FAIL: 없음(이번 범위 확인 항목 기준).
- BLOCKED: 없음.
- NOT RUN: 사람 직접 플레이·실제 OS 마우스/키보드(모든 화면), 하이브리드 해금 실제 마우스 클릭(H10), 커밋 트리 단독 Unity 시험·빌드(D1은 컴파일만), 검토 빌드(QA 코드 없음) 자체에서의 상태별 화면 재현(같은 소스의 QA 빌드로 확인).
- 범위 밖으로 미검증 유지(확대하지 않음): 터치 UI(보스 칩 터치 위치 포함), 다른 언어·현지화 긴 문자열.
- 시험 불안정: T5 기존 간헐(진단 로그 추가, 원인 미확정), T6 시험 측정 수정 후 통과.
- 아트 부족(별도 자산 제작 필요 — 직전 보고 §5 그대로 유지, 공용 아이콘을 전용 그림 완료로 보지 않음): 커피/우림 연구 노드 15종, 전용 카드 그림 없는 카드 14종, 연구 노트북 숙련 노드.
