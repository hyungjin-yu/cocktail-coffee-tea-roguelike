# QA — 커피·차 전투 연결 / 박람회 밀도 / 그림 중심 UI (2026-11-12, task_bfe1771a04e3)

기준: 기획 `개발 기록/2026-11-12_커피차_전투연결_후속지시.md` + 작업 중 코디네이터 전달 사용자 지시(HUD 스크린샷 2장, 결과·전체 UI 점검, 계열명→잔 그림, 박람회 밀도 최소 2배, 전체 UI 그림 중심 기준).
읽은 스킬: `.claude/skills/madia-ui-ux/SKILL.md`(이번 세션 중 직접 읽음 — HUD 1차 구현은 읽기 전에 시작했음을 사실대로 기록), `game-feel-first/SKILL.md`(codex-accounts 경로, 직접 읽음).
기준 커밋 `34956c1` → 체크포인트 `653b89e`(커피·차) → 최종 커밋(아래 §6). Unity 6000.3.25f1. 기본 확인 해상도 1280×720(실제 창 ScreenCapture) / 1920×1080(게임 렌더 텍스처 — 업스케일 아님).
증거 정리: `Delivery/Verification/2026-11-12/coffee-tea-ui/`(before/after/audit/traits/fair). 원본 로그·전체 캡처: `Delivery/Reports/coffee-tea-combat-2026-11-12/`, `Delivery/Reports/ui-visibility-2026-11-12/`.
조작 구분: 실제 빌드 실행은 모두 **QA 빌드 자동 조작**(실제 버튼 onClick + 이동 입력 대행)이며 사람 직접 플레이가 아니다. 데이터: run5 격리 데이터 무수정 복사(정상 누적) 또는 **fixture**(같은 복사 + 원두 +400, `QA-FIXTURE.txt`)·빈 폴더(첫 시작). 사용자 세이브 폴더 메타데이터 전후 동일(TestResults.xml만 — Unity 시험 실행기 사본). 메모리 ≥4096MB 게이트·동시 1개 모두 준수.

## 1. 적용값(검토판 기본값)과 이유

| 음료 | 실제 레시피 조사 | 적용 | 이유·제한 |
|---|---|---|---|
| 에스프레소 머신 | 예시 `더블 에스프레소 1:2`(coffeeMethod=EspressoMachine) | 직접 적중 대상이 강적(기본 체력 ≥70·정예 탄막·보스·박람회 진상 손님)이면 B의 +20% | 문서 초기값 20%. 상성 A는 보너스 전 B로 계산(증폭 중복 없음). 기존 정지 충전→공속 샷 리듬 유지 |
| 핸드 드립 | `SCA 비율 핸드 드립`(PourOver) | 직접 적중 시 대상 1.6월드 안 가장 가까운 다른 적 2명에게 B×35% | 문서 '인접 최대 2명 낮은 계수'. 0.2초 간격(다중 적중 무기 폭주 방지). 보조 적중 = TakeDamage(Effect) → 직접 적중 훅 재진입 없음(재귀·상성·히든 없음). 기존 뜸/붓기 리듬 유지 |
| 모카포트 | `모카포트 3컵`(MokaPot) | 신규 특성 없음, 기존 증기압 폭발 유지 | 문서 '근거 없이 같은 방식 분류 금지' |
| 차 1번째 | 예시 센차·호지차(Tea) | 공격 속도 +15%(전용 스탯 슬롯) | 다른 스탯 재계산과 독립, 교체 시 0 |
| 차 2번째 | 〃 | 직접 적중 대상 이동 ×0.6 1.5초, 보스 제외, 다시 맞히면 시간만 갱신(더 강한 배율 하나) | 기존 상태 둔화(StatusSpeedMultiplier)와 별도 곱 — 덮어쓰지 않음 |
| 차 3번째 | 〃 | 대상 1.3월드 안 다른 적 최대 3명 B×25%, 0.25초 간격 | 단계 하나만 활성(누적 없음) |
| 차 우림 규칙 | 기존 차는 1회 제조·보관·소비 | 1번째 = 기존 레시피 재료 소비. [이어 우리기] = **마신** 차 잔에서만, 원 잔당 1회, 최대 3번째, 재료 0, 2·3번째 박람회 제출 불가. 새 찻잎 = 새 계보 1번째 | 같은 차 종류 자동 연결 없음(계보 ID). 저장: RunCup teaLineage/teaSteep/continuedTo — 누락 시 1번째·자기 계보 |
| 박람회 밀도 | 12명/웨이브, 16초 균등(≈0.75명/초), 실측 동시 6~8 | **36명**(3배), 4명 묶음·같은 쪽, ≈1.56초 간격(14초), 개체 HP·공격 배율 불변 | 2배(24)도 16초 균등이면 줄 서기 — 총수·방출 시간·묶음으로 군중감. 전역 동시 상한 160 |

## 2. 기능 검증

| # | 항목 | setup | steps | expected | actual | 증거 | 상태 |
|---|---|---|---|---|---|---|---|
| F1 | 대응표·강적 판정·설명 | EditMode | RunDrinkTraitTests | 레시피 추출법→특성, 모카 None 신규, 제한 문구 포함 | 일치 | `coffee-tea-combat-2026-11-12/editmode-1.xml`, `ui-visibility-2026-11-12/editmode-final.xml` | PASS |
| F2 | 차 계보·소비·중복·저장 | EditMode 격리 drinks.json | 제조→보관 중 이어 우리기 거부→마심→2번째→중복 클릭 거부→3번째→4번째 거부→새로 우리기 새 계보, 제출 잔 재사용 거부, 다시 읽기 후 1회 기록 유지, 필드 없는 기존 잔 기본값 | 모두 기대대로, 이어 우린 잔 재료 0 | PASS | 〃 | PASS |
| F3 | 에스프레소 실제 적중 | PlayMode 마스터 런, 실제 제조·마시기 | 차 계열 강적/일반, 칵테일 계열 강적에 TakeDirectHit | 강적 +20, 일반 0, 상성 A=보너스 전 B 기준, 지속 피해 무변화, 다른 잔 마시면 제거 | 일치 | `RunDrinkTraitPlayTests` (`playmode-final.xml`) | PASS |
| F4 | 드립 보조 적중 | PlayMode | 대상+가까운 3·먼 1 | 가까운 2명만 35%, 먼 적 0, 0.2초 안 재발동 없음, 보조로 처치 1회 | 일치 | 〃 | PASS |
| F5 | 차 단계 | PlayMode | 1→이어 2→이어 3 마시기 | 공속 +15%→제거, 둔화 0.6 중첩 없음·갱신·보스 제외·만료, 3번째 3명 보조 적중·둔화 없음, 칵테일 마시면 특성 제거 | 일치 | 〃 | PASS |
| F6 | 실제 빌드 특성 발동 | QA 빌드, fixture(원두+400), seed 20261112 | 차1→이어2→이어3, 박람회에서 드립·에스프레소 | 차1 공속 0.15, 차2 둔화 10~11회, 차3 보조 6회(다른 실행 0회 — 적 드묾), 드립 박람회 보조 11/29회, 에스프레소 진상 손님 B16+3 37/123회, 예외 0 | 일치 | `coffee-tea-combat-2026-11-12/realbuild-traits-1,2/events.log`, `ui-visibility-2026-11-12/realbuild-traits-2/` | PASS(자동 조작·fixture) |
| F7 | 다음 주기 준비 패널(이전 미검증) | QA 빌드 마스터 1주기 자동, 정상 누적 데이터 | 수집→반복→박람회→보상→웨이브 11 | Prep·timeScale 0·패널 표시·전달 약화 5% | Prep, prepActive, timeScale 0, 패널 "웨이브 11", red=5 | `coffee-tea-combat-2026-11-12/realbuild-cycle-1/events.log`, `shots/15_next_cycle_wave11*.png` | PASS |
| F8 | 박람회 밀도 | PlayMode + 실제 빌드 3회 | 박람회 1/3 | 36명·묶음 4·같은 쪽·간격, 계획=예고=36, 처리 분모 36, 개체 HP 배율 불변 | 시험 PASS; 실측 최대 동시 25/30/30, 처리 22.3/40.3/35.6초, 체력 100→62/94, 85→70(즉사 없음) | `Fair_Density_*`, `ui-visibility-2026-11-12/realbuild-shots-1..3/events.log`, `Verification/.../fair/fair-crowd-10fps-realtime.mp4` | PASS |
| F9 | 회귀(마스터·준비·현수막·보상·연출·직업 선택) | PlayMode | 관련 묶음 | 기존 단언 유지 | 45/45 | `ui-visibility-2026-11-12/playmode-final.xml` | PASS |
| F10 | 회귀(EditMode 마스터·끝없는·범례·UI 가독성·세이브 UI) | EditMode | 관련 묶음 | 기존 단언 유지 | 75/75 | `ui-visibility-2026-11-12/editmode-final.xml` | PASS |
| F11 | 전체 suite | — | — | — | 실행 안 함(관련 묶음만) | — | NOT RUN |

## 3. 시각 검증(실제 새 QA 빌드, 720p/1080p) — 전후

| # | 화면/요소 | 전(재현) | 후 | 증거 | 상태 |
|---|---|---|---|---|---|
| V1 | 마스터 하단 상태(사용자 스샷 1) | 작은 동일 글자 2줄 | 구간 그림 + "주기·웨이브·단계" 주요 줄 + 처리 막대·수치 / 보조 칩(적 체력·공격·약화, 잔 그림+제출, 칵테일잔+보너스) | `Verification/.../before/user_report_bottom_hud.png`, `before/s05_combat_wave1_720.png` → `after/s05_combat_wave1_{720,1080}.png` | PASS |
| V2 | 계열 범례(사용자 스샷 1, 계열명→잔) | "[칵] 칵테일 · [커] 커피…" 글자 | 계열 대표 적 그림(높이 가득) + 잔 배지(칵테일잔/커피잔/찻잔), 중립 = 글자, 마신 잔 그림 → 대상 계열 적 그림 + %·특성(없음/박람회 중립 정직 표시). 1차 시안 적 그림 10px대 판독 불가 → 확대 수정 | 〃 + `ui-visibility-2026-11-12/realbuild-shots-2`(1차) → `realbuild-shots-3`(최종) | PASS(720p 적 그림 ≈30px) |
| V3 | 좌측 3패널(사용자 스샷 2) | 같은 직사각 글자 상자 | 히든 = 개수 원 배지(0개 흐림) + 짧은 문구 [H], 직업 = 몸체 초상 + 칵테일잔/찻잔 + 진행 칸(바텐더 3칸, 강화 중 주황·글자 "혼합 러시 n초", 티 소믈리에 6칸), 목표 = 첫 미충족 카드 그림/빈 칸 + [T] | `before/user_report_left_hud.png` → `after/s05_combat_wave1_720.png`, `audit/audit_hud_tea_*.png` | PASS(강화 중·카드 선택 상태 캡처는 NOT RUN — 아래) |
| V4 | 준비 패널 | 적 그림 줄 + "[커]" 글자 | 계열 잔 배지(적 그림 모서리), 조언 3줄(일반 조언·상성·구현된 음료 특성), 현재 잔 특성 줄. 1차 시안 조언 9px대·잔 배지가 이름과 겹침 → 수정 | `after/s04b_prep_wave1_*.png`, `after/s07_await_choice_*.png` | PASS |
| V5 | 제조 창 | 글자 줄 | 레시피·잔 줄 왼쪽 완성 잔 그림(칵테일잔·찻잔·에스프레소·드리퍼·모카), 마시면 바뀌는 특성 짧은 라벨, 이어 우리기 줄, 현재 잔 특성 상자(실제 수치·제한·교체·우림 규칙). 1차 시안 바닥글 3줄 9px → 특성 상자로 분리 | `after/s06_brew_quality_*.png`, `traits/t3_tea2_before_continue_*.png` | PASS |
| V6 | 결과 | 글자 2줄 | 직업 초상·무기 아이콘, 칩(칵테일/차 Lv·커피/우림 연구·처치(직업 무대 대표 적)·획득 재료 3종·마스터 잔/마심/제출), 부제 주기 | `before/s10_result_720.png` → `after/s10_result_*.png`, `audit/audit_result_*.png`(티 소믈리에는 칵테일 칩 숨김) | PASS |
| V7 | 박람회 군중·현수막·예고 겹침 | — | 36명 군중이 HUD에 가리지 않음, 상자·배지·현수막 겹침 없음 | `after/s09_fair_crowd_*.png`, `fair/` | PASS |
| V8 | 정규 모드 범례(보스 체력바 아래 26 높이) | — | 그림 들어가나 720p에서 작음(≈16px) | `audit/audit_hud_*.png` | FAIL(작음 — 남은 문제, 보스 체력바와의 공간 제약) |

## 4. 화면별 점검(D) — 개선/유지 이유/남은 문제

| 화면 | 판정 | 이유·남은 문제 | 증거 |
|---|---|---|---|
| 타이틀 | 유지 | 버튼 메뉴는 글이 적합, 아이콘 있음 | `audit/`·이전 캡처 |
| 직업/무기 선택 | 유지 | 이미 초상·무기 그림 카드 | `ui-audit 11_weapon_select_*` |
| 전투 HUD(마스터) | 개선 | V1~V3 | — |
| 범례 | 개선(마스터) / 남은 문제(정규 작음) | V2/V8 | — |
| 준비·예고 | 개선 | V4 | — |
| 제조/보관/제출 | 개선 | V5 | — |
| 결과 | 개선 | V6. 선택 카드·완성 조합 그림은 '이번 판 빌드' 상세에 있음(결과 첫 화면 추가는 남은 과제) | — |
| 일시정지/설정/크레딧 | 유지 | 설정·라이선스는 명료한 글이 적합 | `audit/audit_pause_*`, `audit_SettingsButton`, `audit_CreditsButton` |
| 영구 강화 | 유지 | 카드형 + 아이콘·비용·잠금 이미 표시 | `audit/audit_permanent_*` |
| 커피 연구(트리) | 유지·남은 문제 | 노드가 글 상자(분류 헤더만 아이콘). 노드별 그림은 노드 전용 승인 아이콘이 없어 보류 | `audit/audit_study_*` |
| 도감 | 유지 | 카드 그림·잠금 ??? 상태 기존 | `audit/audit_codex_*` |
| 스킨 상점 | 유지 | 캐릭터 미리보기 기존 | `audit/audit_SkinShopButton.png` |
| 카드 선택·히든 상세·목표 조합·최근 기록·보상 상세·하이브리드·첫 안내·연구 노트북·로비 음료 제조 | 점검 미완(이번 캡처 범위 밖) | 기존 카드/아이콘 사용 화면 — 이번 변경 없음, 실제 캡처 재검수는 NOT RUN | — |

필요할 수 있는 새 아트(제작하지 않음, 요청 목록): 커피 연구 노드별 아이콘(15종), '처치' 전용 아이콘, 결과 화면용 카드 썸네일 배치는 기존 CardEducation 그림으로 가능(새 아트 불필요).

## 5. 경계·미검증

- 직업 패널 '혼합 러시 강화 중'(주황 칸) 상태, 목표 조합 카드 선택 상태, 히든 1개 이상 배지의 실제 빌드 캡처: NOT RUN(코드 경로만, 자동 흐름에서 해당 상태 미도달).
- 다른 언어·긴 문자열: NOT RUN(한국어만).
- 차 3번째 실제 빌드 보조 적중: 한 실행 6회, 다른 실행 0회(수집 웨이브 적 밀도 낮음) — PlayMode에서는 결정적 PASS.
- 사람 직접 플레이·실제 창 키보드/마우스 조작: NOT RUN(자동 조작만, 통과 주장 안 함).
- 정규 모드 범례 크기(V8): FAIL로 남김.

## 6. 빌드·패키지·해시

- 검토 빌드 `Builds/CoffeeTeaUi-Review-2026-11-12/`(이전 검토·공모전 빌드 미변경). 소스 2,813 파일 빌드 전·후 **3dbf6a1f8ae1678b9d13eec90b3ae3cfb051043a4622180a5ab254bc031a6fc5** 동일(`ui-visibility-2026-11-12/source-manifest-review-before/after.sha256`); 이전 작업 검토 빌드 기준(e743ceb1…) 대비 변경 파일은 §1~4 구현 파일 + QA 전용 PlayVerifier*.cs. ProjectSettings 불변.
- 산출물 208 파일 목록 `a6641eef88ca7da686d65318e7b3caa7554594a211ab7ab411da0ed9a5a14057`, CCT.Runtime.dll `c91c1119…cdbee` — QA 문자열(QaDirectHit/TraitsFlow/ShotsFlow/BrewFxFlow/PlayVerifier/UiAuditRun) 0건(‘AutoTour’ 1건은 런처의 '이 빌드에 투어 없음' 안내 문구).
- 검토 빌드 부팅: 빈 격리 폴더, 입력 없는 창 캡처, 예외 0(`ui-visibility-2026-11-12/review-build/boot_title_noinput.png`). 실제 창 조작 없음.
- ZIP `Delivery/CocktailCoffeeTea_CoffeeTeaUi_2026-11-12.zip`(209 항목) sha256 **caf1ce3b0fd17e6db118fb2c4cbc7adb249c59d6b105eb1eef8ed993a4d28ade**. 이전 ZIP 3개·공모전 ZIP 보존·해시 확인.
- 공개 배포·Steam 업로드 없음.
