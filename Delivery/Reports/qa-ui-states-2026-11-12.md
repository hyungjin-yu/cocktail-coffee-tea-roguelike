# QA — 정규 범례 720p FAIL 수정 · 남은 상태 캡처 · 화면별 점검(D) 후속 (2026-11-12, task_f76596546901)

기준 HEAD `ddeb39d` + 직전 보고 `Delivery/Reports/qa-coffee-tea-ui-2026-11-12.md`(직접 읽음). 이번 세션에서 직접 읽은 기준: `.claude/skills/madia-ui-ux/SKILL.md`, `game-feel-first/SKILL.md`(codex-accounts 경로).
Unity 6000.3.25f1. 확인 해상도 **1280×720 / 1920×1080 실제 창**(QA 빌드가 시작 시 `Screen.SetResolution(…, Windowed)` 후 `ScreenCapture` — 업스케일 아님).
증거 루트: `Delivery/Verification/2026-11-12/ui-states/`(아래 경로는 이 폴더 기준).

**조작 구분**: 모든 실제 빌드 실행은 **QA 빌드 자동 조작**(실제 버튼 onClick·공개 API)이다. **사람 직접 플레이·실제 마우스/키보드 조작은 하지 않았다(NOT RUN).**
**강제 경로(fixture)**: 스폰 정지·적 제거(FreezeWorld), 플레이어 최대 체력 +100000, 마신 음료 `Player.DrunkDrink` 직접 지정, 히든 `ForceForTests`, 보스 `BossDirector.SpawnNow`, 카드 `GrantCardForTests`(실제 선택과 같은 중첩·혼합 러시·히든 판정 경로). 정상 수집 플레이로 보고하지 않는다.
**데이터 격리**: 실행마다 빈 `-cct-data-dir`(`Builds/UiStates-QaTour-2026-11-12-qa-data/<run>`)와 그 안 `-logFile`. 사용자 세이브(`%LOCALAPPDATA%Low/DefaultCompany/CocktailCoffeeTea/save.json·drinks.json·settings.json`) 수정 시각 09-28/10-27/10-21 그대로 — 이번 세션에 바뀐 것은 Unity 시험 실행기 사본 `TestResults.xml`과 Unity 엔진 Analytics 캐시뿐. 메모리 ≥4096MB 게이트(각 실행 `gate.txt`), Unity 에디터·플레이어 동시 1개.

## 1. 변경 요약(무엇이 화면에서 달라졌나)

| 화면 | 전 | 후 | 이유 |
|---|---|---|---|
| 정규 범례(비보스) | 높이 26, 적 그림 720p ≈16px(V8 FAIL), "마심 →"과 대상 그림 사이 170 칸 | 높이 60(y 14~74, 단계 배지 y 92 아래), 계열 대표 적 그림을 **알파 영역으로 잘라** 칸을 채움(같은 승인 텍스처의 부분 사각형 — 원본 PNG 수정 없음), 라벨 24, 마신 잔 → 대상 그림을 붙임 | 핵심 "지금 음료 → 상성 대상"을 큰 그림으로 |
| 정규 보스전 | (26 높이로 보스 체력바 아래 끼움 — 작음) | **범례 접고** 오른쪽 아래 상성 칩(마신 잔 → 대상 계열 그림, "+20% · 보스도 대상 / 보스는 대상 아님 / 마신 음료 없음"). 터치 UI는 오른쪽 아래 큰 버튼 자리라 단계 배지 위 가운데(y 136) | 보스 HP 가림 금지 + 핵심 그림 유지 |
| 보스 등장 현수막 | 폭 1400 → 부제가 왼쪽 패널 밑으로("[커] 커피 계열"이 가려짐, 새로 발견) | 폭 680 가운데 열, 제목 한 줄 크기 맞춤(64~30), 부제 줄바꿈 크기 맞춤(30~22) | 겹침 결함 |
| 카드 선택 | 카드 그림이 오른쪽 위 64(720p ≈43px, 장식처럼) | 이름판 왼쪽 88 칸에 카드 그림, 이름은 오른쪽(긴 이름 줄바꿈·크기 맞춤) | 그림 먼저 |
| 목표 조합 창 | 조건이 글자뿐 | 행 오른쪽에 조건 카드 그림(커피 = 원두, 하이브리드 = 하이브리드 잔) + 진행 수치, 충족 밝게·미충족 흐리게, 글자 순서와 같은 순서 | 그림 먼저 |
| 최근 기록 목록 | 글자 2줄 | 직업 몸체 초상 + 시작 무기 아이콘 | 그림 먼저 |
| 획득 내역(보상 상세) | 재료 이름·양 글자 | 실제 재료 아트(아이콘 → 없으면 병/틴) 48 칸. **임시 실루엣이면 그림을 넣지 않음** | 임시 그림을 최종처럼 보이지 않게 |
| 연구 노트북 탭 | 선택 탭 `button_equipped`의 ✓ 표식이 글자 끝을 덮음("숙련 트리✓") | 선택 중 글자 칸 오른쪽 40 비움(승인 버튼 아트 그대로) | 잘림 결함 |
| 커피/우림 연구 트리 갈래 아이콘 | 720p ≈10px(그림 여백) | 알파 영역 잘라 56 칸 | 판독 |
| 찻집 HUD 우상단 | "우림 포인트 0 [K] 우림 연구 **연구**" | "[K] 우림 연구"(용어 치환 순서 수정) | 문구 결함 |

## 2. 빌드·시험 결과(컴파일/자동 시험 — 시각 판정 아님)

| # | 항목 | setup | steps | expected | actual | 증거 | 상태 |
|---|---|---|---|---|---|---|---|
| B1 | QA 투어 빌드 | `BuildRelease.Windows64ReleaseQaTourBatch` → `Builds/UiStates-QaTour-2026-11-12` | 배치 빌드 | 성공 | Build Finished Success(최종 `build-qatour-final.log`) | `build-qatour-*.log` | PASS |
| B2 | 검토 빌드(QA 코드 없음) | `Windows64ReleaseToBatch` → `Builds/UiStates-Review-2026-11-12` | 배치 빌드, 소스 해시 전후 | 성공, 소스 불변 | Success, 2,815 파일 전후 동일 `9d24d0c2…77a7f8`, CCT.Runtime.dll에 UiStatesRun/DrinkLabCapture/PlayVerifier/UiAuditRun 0건(AutoTour 1건 = LaunchOptions 속성명) | `build-review.log`, `source-review-before/after.sha256` | PASS |
| T1 | EditMode 전체 | 에디터 배치 | 전체 suite | 실패 0 | 949 중 947 통과·실패 0·불확정 1(`IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`, 기존)·건너뜀 1(`IntegerOnlyTests.Migration_UserSaveCopy_IsolatedRun`, 사용자 세이브 보호용 Ignore) | `editmode-final.xml` | PASS |
| T2 | PlayMode 관련 23묶음 | 범례·보스·카드·교육·목표·히든·기록·HUD·마스터·제조실·직업선택 등 | 실행 | 실패 0 | 140/140 | `playmode-final3.xml` | PASS |
| T3 | 신규 `StandardLegendBossChipPlayTests` | 정규 런 | 비보스 범례 60 높이·대상 그림 → 보스 소환 → 범례 접힘·칩 표시·칩/보스바 비겹침·현수막 제목 한 줄·현수막이 왼쪽 패널 3개와 비겹침 → 마신 음료 없음 칩 문구 → 보스 처치 후 범례 복귀, 계열 그림 = 같은 텍스처·여백 잘림 | 모두 | 통과 | `playmode-final3.xml` | PASS |
| T4 | 신규 `JobStudyThemeTextTests` | EditMode | 찻집 문구 치환 | "연구 연구" 없음, 바텐더 불변 | 통과 | `editmode-final.xml` | PASS |
| T5 | `BattleArenaPlayTests` | PlayMode | 단독 실행 | 5/5 | 5/5. **주의**: 묶음 실행 1회에서 `HoldingOneDirection(1.0f)`가 LevelUp 시간정지로 STALL(`playmode-final.xml`). **정정(2026-11-12 후속)**: "카드 교육 시험 뒤 순서 의존"은 틀린 설명 — 실패 실행에서 BattleArena가 첫 번째였다. 같은 STALL이 2026-11-09 로그에도 있어 기존 간헐 불안정 — `qa-hybrid-unlock-2026-11-12.md` §2 | `playmode-battlearena-final.xml`, `playmode-final.xml` | PASS(단독) / 기존 간헐 불안정 남음 |
| T6 | `MasterChallengePlayTests.FullCycle…` | PlayMode | 1회차 | — | 1회 3.296초(범위 3.3~4.4) 실패, 재실행 2회 통과 — 실시간 타이머 경계 불안정(기존) | `playmode-1.xml`, `playmode-2/3/final3.xml` | PASS(재실행) / 경계 불안정 남음 |

## 3. 기능·시각 검증 — 실제 새 QA 빌드(자동 조작)

투어 `-cct-autotour <out> -cct-autotour-mode ui-states`(신규 QA 전용 `AutoTourUiStates.cs`), 바텐더·레이피어 / 티 소믈리에·활, 각 720p·1080p. 최종 실행 `us720/`, `us1080/` 각 39장, `errors=0 exceptions=0`(`tour-log.txt`, `Player.log`). 화면 픽셀 사각형은 `tour-log.txt`의 `US LEGEND` 줄.

| # | 상태 | 재현 steps | expected | actual(720p / 1080p) | 증거(720p, 1080p는 `us1080/us_1920x1080_*` 같은 이름) | 상태 |
|---|---|---|---|---|---|---|
| V1 | **정규 범례 720p (이전 V8 FAIL 재현→수정)** | 정규 런 시작 → 스폰 정지 → 마신 음료 없음/칵테일(차) | 계열 그림·대상 그림이 읽히는 크기, 보스바와 무관 | 전(이전 빌드): 높이 26, 적 그림 ≈16px(`Delivery/Reports/ui-visibility-2026-11-12/qatour-ui-audit/audit_hud_*.png`). 후: 범례 733×40px, 대상 그림 칸 38.7px(1080: 58px), 그림이 칸을 채움, "마심 → [대상] +20%" 붙음 | `us720/us_1280x720_bartender_rapier_10_legend_nodrink.png`, `_11_legend_drink.png`, `tea_bow_11_legend_drink.png` | PASS |
| V2 | 정규 보스전 범례 접힘 + 상성 칩 | 위 상태에서 보스 소환 → 등장 무적 대기 | 범례 숨김, 칩 = 마신 잔 → 대상 + 보스 대상 여부, 보스 체력바 비겹침 | 칩 (1007,16 253×43) vs 보스바 (307,33 667×23) 겹침 없음, "+20% · 보스도 대상"(바텐더/커피 보스), "+20% · 보스는 대상 아님"(찻집/차 보스), 마신 음료 없음 칩 문구 | `_12_boss_chip.png`, `_13_boss_chip_nodrink.png` | PASS |
| V3 | 보스 처치 후 범례 복귀 | 보스 처치 → 오버레이 정리 | 범례 다시 보임, 칩 숨김 | 일치 | `_14_legend_after_boss.png` | PASS |
| V4 | 보스 현수막 겹침(새로 발견) | 보스 소환 순간 | 왼쪽 패널과 겹치지 않음, 제목 한 줄 | 전: 부제 "[커] 커피 계열"이 직업 패널 밑(이번 1차 실행 캡처 — 덮어씀, 같은 장면은 T3 시험 단언으로 재현). 후: 제목 한 줄, 부제 2줄, 왼쪽 패널 오른쪽 끝(405px@720) 밖 | `_12_boss_chip.png` | PASS |
| V5 | 히든 0 → 1개 배지 | 히든 1개 강제 | 배지 숫자 1·금색 | badge='1'(0 → 1), 금색 원 | `_20_hidden1_badge.png` | PASS(fixture) |
| V6 | 히든 상세 1개 / 여러 개(긴 목록) | [H] 경로 `OpenHiddenDetails` → Esc | 열림, 긴 목록 스크롤, Esc로 재개 | 1개·24개(바텐더)/17개(찻집) 열림, 스크롤바, Esc 재개 | `_21_hidden_details_1.png`, `_23_hidden_details_many.png` | PASS(fixture) |
| V7 | **혼합 러시 강화 중** | 칵테일 카드 3종 받기(실제 판정 경로) | 주황 칸 3·"혼합 러시 12초 · 피해 +20% · 공속 +15%" | rush=11.49초, pips=3 주황 | `bartender_rapier_22_job_active.png` | PASS |
| V8 | 깊은 우림 진행(찻집) | 같은 차 카드 반복 받기 | 초록 칸 n/6 | 3/6, 피해 +6%(실제 카드 히든 해금으로 배지 2도 함께) | `tea_bow_22_job_active.png` | PASS |
| V9 | 첫 카드 안내(첫 진입) | 빈 데이터 첫 카드 창 | 교육 안내 1/4 | 표시 후 닫음 | `bartender_rapier_29_card_guide_first.png` | PASS |
| V10 | **목표 조합 + 카드 선택** | 목표 지정 → 카드 창 → 제시 카드와 맞는 목표로 변경 | 목표 줄, 해당 카드 아래 "목표 진행 0→1/3", 카드 그림 | 일치, 카드 그림 이름판 왼쪽 | `_30_card_select_target.png` | PASS |
| V11 | 목표 바꾸기 창 | [T] 경로 `OpenComboTarget` | 조건 그림·수치, 글자 순서와 같은 순서 | 일치(1차 실행에서 역순 발견 → 수정 후 재확인) | `_31_combo_target_view.png` | PASS |
| V12 | HUD 목표 카드 | 카드 선택 후 전투 | 왼쪽 목표 칸에 첫 미충족 카드 그림 | 표시(얼음과 희석 = 전용 카드 그림 없어 칵테일 재료 공용 아이콘 — §5) | `_32_hud_goal_card.png` | PASS |
| V13 | 커피/우림 연구 트리 | [K] 경로 `OpenCoffeeTree` | 갈래 아이콘 판독, 글자 잘림 없음 | 갈래 아이콘 56칸, 노드는 글 상자(아트 없음 §5), "우림 연구" 용어 | `_15_study_tree.png` | PASS(노드 아트 부족은 §5) |
| V14 | 결과 → 획득 내역 → 이번 판 빌드 | 포기 → 획득 내역 버튼 → 닫기 → 빌드 버튼 | 열림·닫힘 | 둘 다 열림, 재료 그림 | `_40_result.png`, `_41_award_detail.png`, `_42_build_detail.png` | PASS |
| V15 | 최근 기록 빈 목록 → 2판 → 상세 → 닫고 재진입 | 첫 실행 타이틀 → 최근 기록 / 두 판 뒤 다시 | 빈 상태 문구, 행 2, 상세, 재진입 행 유지 | rows 0 empty=True → rows 2 → detail open → 목록 복귀 → 재진입 rows 2 | `us_1280x720_01_history_empty.png`, `_50_history_list.png`, `_51_history_detail.png`, `_52_history_reenter.png` | PASS |

### 3b. 로비 음료 제조·연구 노트북·서랍·카드 상세·보상 상세(기존 QA 전용 `DrinkLabCapture`, 같은 새 QA 빌드)

| # | 모드 | 화면 | 결과 | 증거 | 상태 |
|---|---|---|---|---|---|
| L1 | `progress` 720/1080 | 주문·판매·상점·**연구 노트북**(숙련·직원·기존 강화)·**레시피 서랍**(빈·열림·펼침·쪽 넘김) 22장 | 예외 0. 노트북 선택 탭 ✓가 글자를 덮던 문제 발견 → 수정 후 재실행 확인 | `lab-progress-720/P30_laptop_mastery_start.png` 등, `lab-progress-1080/` | PASS |
| L2 | `hudfix` 720/1080 | 긴 이름 다효과 칩·첫 카드 안내·**카드 상세(학습)**·카드 선택·일시정지·긴 결과·**획득 내역 위/아래(53종 긴 목록)** 10장 | 예외 0, 글 상자 넘침 0, 최소 글자 19(캔버스 단위), placeholderArtUsed=false. 획득 내역 재료 그림 표시 | `lab-hudfix-720/report.json`, `R2_award_detail_top.png`, `R3_award_detail_bottom.png` | PASS |
| L3 | `ui` 720/1080 | **로비 음료 제조**(칵테일 최대·비용 펼침·스크롤·부족·차 최대·레시피 예시·도감·영수증·레시피 찢기) 12장 | 예외 0, 넘침 0 | `lab-ui-720/`, `lab-ui-1080/` | PASS(변경 없음 — 이미 그림 중심) |

### 3c. 검토 빌드(QA 코드 없음)

| # | 항목 | steps | actual | 증거 | 상태 |
|---|---|---|---|---|---|
| R1 | 부팅 | 빈 격리 폴더, 입력 없음, 14초 후 창 캡처 | 타이틀 정상, 예외 0(창은 새 설정 기본값 1920×1080) | `review-build/boot_title_noinput.png`, `review-build/Player.log` | PASS |
| R2 | 검토 빌드에서 위 V1~V15 상태 직접 확인 | — | 검토 빌드에는 자동 조작 코드가 없어 실행하지 않음. 주석 1곳만 다른 소스(`f007b7f8…`)의 QA 빌드로 확인 | — | NOT RUN(검토 빌드 자체) |

## 4. 화면별 점검표(D) 갱신

| 화면 | 판정 | 근거 |
|---|---|---|
| 카드 선택 | 개선 | V10 |
| 첫 카드 안내·카드 상세(학습) | 유지 | V9, L2 — 카드 그림+짧은 설명, 상세는 버튼 |
| 히든 상세 | 유지 | V6 — 효과 문장이 본질(글 적합), 스크롤 정상 |
| 목표 조합 창 | 개선 | V11 |
| 최근 기록 목록·상세 | 개선(목록) / 유지(상세 — 카드 그림 행 기존) | V15 |
| 보상 상세(획득 내역) | 개선 | V14, L2 |
| 연구 노트북 | 개선(탭 잘림) / 남은 문제(숙련 노드 그림 없음 — §5) | L1 |
| 로비 음료 제조·레시피 서랍 | 유지 | L3, L1 — 병·지거·잔·서랍 그림 중심 |
| 커피/우림 연구 트리 | 개선(갈래 아이콘) / 남은 문제(노드 그림 — §5) | V13 |
| 정규 범례·보스전 | 개선 | V1~V4 |
| 하이브리드 해금 창 | NOT RUN | 이번 투어에서 해금 조건 미유도(자동 흐름 범위 밖) |
| 다른 언어·긴 현지화 문자열 | NOT RUN | 한국어만 |
| 터치 UI 보스 칩 위치(y 136 가운데) | NOT RUN(캡처) | 코드 경로만, `-cct-touch-ui` 실행 없음 |

## 5. 승인 아트 부족 — 별도 명시(이번에 만들지 않음, 임시 그림을 최종으로 쓰지 않음)

**A. 커피/우림 연구 트리 노드 아이콘 15종** — 현재 노드는 글 상자(갈래 헤더만 `skill_attack/survival/ingredient` 공용 아이콘). 용도: 트리 노드 왼쪽 그림(잠김·해금 가능·해금 상태는 기존 테두리·문구로 표시), 바텐더 = 커피 용어 / 티 소믈리에 = 같은 노드를 우림 용어로 재사용(그림 1세트로 두 직업 공용 가능 여부는 아트 결정 필요). 규격 제안: 기존 카드 교육 그림(`Resources/CardEducation/*.png`)과 같은 **64×64 px 픽셀 아트, 투명 배경**, 화면 표시 72~88 캔버스 단위.

| id | 이름(바텐더) | 갈래·단계 | 실제 기능 |
|---|---|---|---|
| espresso_shot | 에스프레소 샷 | 공격 1 | 공격력 +20% |
| ristretto | 리스트레토 | 공격 2 | 공격 속도 +25% |
| balanced_extraction | 균형 추출 | 공격 3 | 사거리 +15% |
| full_body_roast | 풀바디 로스트 | 공격 4 | 공격력 +25% |
| perfect_extraction | 완전 추출 | 공격 5 | 연속타/동시발사 +1 |
| caffeine_rush | 카페인 각성 | 생존 1 | 이동 속도 +12% |
| doppio_body | 도피오 체력 | 생존 2 | 최대 체력 +50 |
| mellow_finish | 부드러운 여운 | 생존 3 | 분당 회복량 +60 |
| insulated_cup | 보온 컵 | 생존 4 | 최대 체력 +30 |
| steady_hand | 흔들림 없는 손 | 생존 5 | 받은 피해 -15% |
| cold_brew | 콜드브루 | 재료 1 | 재료 게이지 필요량 -10% |
| fine_grind | 곱게 갈기 | 재료 2 | 재료 추가 드랍 확률 +15% |
| wide_tray | 넓은 트레이 | 재료 3 | 줍기 반경 +100cm |
| measured_pour | 정량 따르기 | 재료 4 | 재료 게이지 필요량 -10% |
| zero_waste | 남김없는 추출 | 재료 5 | 재료 추가 드랍 +20%, 줍기 반경 +50cm |

**B. 전용 카드 그림이 없는 카드 14종** — 카드 선택·목표 조합·HUD 목표 칸·기록 상세에서 칵테일 재료/찻잎 **공용 카테고리 아이콘**으로 대체 표시 중(V12 "얼음과 희석" 예). 규격: `Resources/CardEducation/card_<이름>.png` 64×64, `educationArtId` 연결.
칵테일: `bar_shoes` 빌드, `champagne_spray` 스플릿 베이스, `ice_cube` 얼음과 희석, `liqueur` 리큐르, `molotov` 플레임드 제스트, `syrup` 프리배치. 차: `dried_leaf` 찻잎 건조, `golden_ratio` 추출 균형, `honey` 티스푼 계량, `long_spout` 긴 주둥이 주전자, `nightshade_blend` 홍차의 떫은 여운, `piercing_arrowhead` 차 거름망, `tea_leaf_scatter` 블렌딩, `warm_cup` 찻잔 예열. (`hybrid_tea_martini`는 하이브리드 전용 아이콘 사용 — 부족 아님.)

**C. 연구 노트북 숙련 트리 노드(서비스·안정·기교 × 4)** — 글 버튼. 노드별 그림 필요 여부는 기획 결정(이번 범위에서 요청 목록만).

## 6. 남은 문제·미검증(숨기지 않음)

- 사람 직접 플레이·실제 OS 입력: NOT RUN.
- 하이브리드 해금 창, 터치 UI 칩 위치, 다른 언어: NOT RUN.
- 시험 불안정 2건(T5 간헐 STALL — 기존, T6 실시간 경계 — 시험 측정 오차) — 후속 `qa-hybrid-unlock-2026-11-12.md` §2에서 분리·T6 수정.
- 범례의 커피 계열 대표(에스프레소 벌레)는 가로로 납작한 그림이라 잘라도 높이 ≈20px@720 — 판독은 되나 다른 계열보다 작음(아트 형태 한계).
- 검토 빌드는 작업 트리 전체(다른 사용자의 미커밋 변경 포함)로 만들어졌다. 커밋에는 이번 범위 파일만 넣었다(§7) — 다른 사용자의 미커밋 변경(예: `CardSelectView.cs`의 `IntNum.Round` 줄)은 커밋하지 않고 작업 트리에 보존.

## 7. 빌드·패키지·해시·커밋

- QA 투어 빌드 `Builds/UiStates-QaTour-2026-11-12/`(투어 코드 포함 — 배포 금지). 검토 빌드 `Builds/UiStates-Review-2026-11-12/`. 이전 검토·공모전 빌드 미변경.
- 소스 2,815 파일 검토 빌드 전·후 `9d24d0c23dabfb55ca6ba5d39c7afe74b90a0f1af2a70b15e5993b797877a7f8` 동일(`source-review-before/after.sha256`). 최종 QA 투어 빌드 소스 `f007b7f8592b740fc03e911df857f7861a2e9acaff5fa7079a4dfb1d42302c33`(`source-qatour-final.sha256`)와의 차이는 `CardSelectView.cs` 한 파일의 주석 위치(문서 주석 순서)뿐 — 동작 동일, 검토 빌드 재빌드·부팅 재확인.
- 검토 빌드 산출물 208 파일(BurstDebugInformation_DoNotShip 제외, README 포함) 목록 `9a905d78a7a4a61af25b8f38a80b7fc39ef0df3eaa5a8a526b6f1c58b1ff1167`(`review-build-files.sha256`), CCT.Runtime.dll `535a2a886323865f62e45690130f8e008f16998580a543bfa2815ed7a63dc50a`.
- ZIP `Delivery/CocktailCoffeeTea_UiStates_2026-11-12.zip`(208 항목, testzip 정상) sha256 **dd1a80028aecc489a62c3a1b8ac2f884a04ebe0b77e54f1a4123cdb108a1e589**. 기존 ZIP 5개(공모전·이전 검토) 해시 전후 일치(`existing-zips-before.sha256` 검사 OK).
- 공개 배포·Steam 업로드 없음. 커밋은 기존 원격 백업 브랜치 `origin/codex/coffee-brewing`에 push(비공개 저장소 백업).
- 커밋 트리(다른 사용자 미커밋 변경 제외) 자체의 별도 깨끗한 체크아웃 컴파일: NOT RUN — 이번 변경이 쓰는 식별자가 HEAD에 모두 있는지 `git grep HEAD`로만 확인.
