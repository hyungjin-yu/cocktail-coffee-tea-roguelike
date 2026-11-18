# QA — 2026-11-14 플레이 피드백 첫 묶음(2·3·6·8·9·10·17·18·22) 구현·실화면 검수 (2026-11-15, task_2c6e20836d46)

기준 문서(UTF-8 직접 읽음): `기획/플레이 피드백 개선 기획_2026-11-14.md` 전체, `개발 기록/플레이 검수/피드백 개선 실행지시_2026-11-15.md`.
적용 지침: `.claude/skills/madia-ui-ux/SKILL.md`, `game-feel-first/SKILL.md`(codex-accounts 경로), `Delivery/Reports/qa-protocol.md`.
작업 공간: `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing`(브랜치 `codex/coffee-brewing`, HEAD `6578edd` + 기존 미커밋 변경 238개 보존 — 이번 작업은 커밋·푸시하지 않음).
Unity 6000.3.25f1. 증거 루트 `Delivery/Verification/2026-11-15/feedback-batch1/`(아래 경로는 이 폴더 기준).

**조작 구분**: 실제 빌드 실행은 모두 **QA 빌드 자동 조작**(실제 버튼 onClick·공개 API). 사람 직접 플레이·실제 마우스/키보드 입력은 **NOT RUN**.
**강제 경로(fixture)**: 카드 발견 절반 `PermanentProgress.DiscoverCard`, 카드 창은 `CardView.Show`로 직접 제시, 원두 30 지급으로 연구 보상 창, 스폰 정지·체력 +100000. 격리 데이터 `-cct-data-dir Builds/Feedback-QaTour-2026-11-15-qa-data/fb720|fb1080`. 사용자 세이브 미접촉.
**메모리 게이트**: `Tools/QA/gated-unity.sh`(여유 RAM ≥ 4096MB·에디터 미실행). 첫 측정 3.0~3.98GB로 BLOCKED → 코디네이터 재측정 4717MB 통보 후 재개, 각 실행 `*.gate.txt`(4526~5170MB) 기록. 사용자 앱 종료 시도 없음.

## 1. 화면에서 달라진 것

| # | 화면 | 전(증거 `before/icons720/`) | 후 |
|---|---|---|---|
| 2 | 카드 도감 행 | 카드 그림 오른쪽 위 38(720p ≈25px), 미발견이면 그림 숨김, 긴 설명·풍미 글 3줄 | 왼쪽 112 그림 칸(720p ≈75px) → 이름 → 짧은 게임 효과 → 획득 상태. 긴 설명·지식·출처는 클릭 상세. 직업 연구 행도 승인된 연구 노드 그림 |
| 3 | 카드 도감 미획득 | "???" 글만, 그림 없음 | 같은 그림의 어두운 실루엣 + "미발견" 배지 + "플레이 중 획득하면 등록됩니다" + "미발견 · 칵테일 카드". 클릭하면 안내 상세(획득 경로 = 카드 창, 이름·효과 비공개). 히든은 기존처럼 정답 비공개 |
| 2·3·6 | 재료 도감(음료 제조 > 재료 도감) | 재료마다 큰 종이 카드(96 그림 + 본문 11줄) 3열, 한 화면 3~6장 | 위: 검색(이름·맛·향, 이름 일치 우선 정렬) + 지우기 + 종류(전체/칵테일/차/커피)·상태(전체/발견/미발견) 칩(✓·굵게·색) + 필터 초기화 + "결과 n / 87종 · 발견 m". 아래: 그림 칸 격자(128 그림 → 이름 → 보유/미발견, 720p 7열). 칸 클릭 = 큰 그림(192)·실제 색 잔·기존 본문·문화·산지 상세, "목록으로"/Esc로 같은 위치 복귀. 빈 결과 안내 + 초기화 버튼 |
| 8 | 레벨업 카드 | 카드 왼쪽 위 [1][2][3] | 카드 안 순번 제거. 1·2·3 키 선택 그대로, 화면 아래 공통 한 줄 "숫자 키 1 · 2 · 3으로 왼쪽부터 카드 선택"(터치 UI는 표시 안 함) |
| 9 | 레벨업 카드 글 계층 | 효과·지식·해석 모두 24 같은 색 | 이름 → 게임 효과 32(밝은 크림, 최대 2줄, 자동 축소 없음) → 음료 지식·해석 21(흐린 색) |
| 10 | 지식 상세(카드 선택 > 지식 상세, 도감 상세) | 본문 끝에 긴 URL 원문이 그대로 | 본문(효과·지식·해석·안내)과 구분선 아래 "출처 n" 영역: 문서명 24 · 짧은 사이트명 22(예 `iba-world.com`) · [주소 복사](원문 URL 전체를 클립보드로). 스크롤 안내 줄이 본문과 겹치던 기존 결함도 수정 |
| 17 | 커피/우림 연구 노드 | 왼쪽 위 [1]~[6] | 노드 순번 제거. 배분 모드에서 재개 버튼 오른쪽 "숫자 키 1~5 = 공격 1~5단계 · 6 = 생존 1단계"(실제 동작과 일치, 조회 전용에서는 숨김) |
| 18 | 연구 노드 그림 | 단축키 회피용 상수 -24 → 노드 아래쪽으로 치우침 | 노드 세로 가운데(그림은 기존 불투명 영역 자르기 그대로) |
| 22 | 결과 화면 버튼 | "이번 판 빌드" | "성장·조합 보기", 상세 맨 위 제목 "획득 카드와 완성 조합 · 연구"(상세 범위 = 카드·조합·연구). 내부 저장 키·기록 형식 불변 |

## 2. 빌드·자동 시험(컴파일/시험 — 시각 판정 아님)

| # | 항목 | setup / steps | expected | actual | 증거 | 상태 |
|---|---|---|---|---|---|---|
| B0 | 정적 컴파일 | Roslyn csc로 Runtime/Editor/EditMode/PlayMode, `CCT_QA_TOUR` 포함 | 오류 0 | 4 어셈블리 × 2 설정 오류 0 | 세션 로그 | PASS(컴파일만) |
| T1 | EditMode 전체 1차 | `-runTests -testPlatform EditMode` | 실패 0 | 963 중 5 실패(도감 미발견 제목 "???" 기대, 연구 안내 22<24, 효과 칸/이름판 겹침, 편집 모드 Destroy, 시험 경로) | `editmode-1.xml` | FAIL → 수정 |
| T2 | EditMode 전체 2차 | 같은 | 실패 0 | 963 중 961 통과·실패 0·불확정 1(`IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`, 기존)·건너뜀 1(`IntegerOnlyTests.Migration_UserSaveCopy_IsolatedRun`, 기존) — 신규 `FeedbackBatch1Tests` 9건 포함 | `editmode-2.xml` | PASS |
| T3 | PlayMode 관련 18묶음 | 도감·카드 교육·재료 확장·음료 제조·정수·커피·아이콘·기록·HUD·키보드·터치·결과·목표 조합·추천·일시정지·스모크·찻집 연구 | 실패 0 | 135 중 133 통과·실패 0·건너뜀 2(`CoffeeLabPlayTests` 증거/Explicit 2건, 기존) | `playmode-1.xml` | PASS |
| B1 | QA 투어 빌드 | `CCT.Editor.BuildRelease.Windows64ReleaseQaTourBatch -cct-preview-out Builds/Feedback-QaTour-2026-11-15` | 성공 | Build Finished, Result: Success | `build-qatour-1.log` | PASS |

신규 시험 `Assets/_Project/Tests/EditMode/FeedbackBatch1Tests.cs`: 카드 순번 없음·안내 줄 위치·터치 숨김, 효과>지식 글 크기·모든 카드 효과 2줄 안(1080/720 배율 실제 글 생성기), 출처 파싱·짧은 주소·본문 URL 없음, 상세 출처 영역 위치·글 크기·복사=원문·재열기 중복 없음, 도감 그림 칸 크기·실루엣·배지·비공개·발견 후 원색·비겹침, 히든 비공개, 재료 검색·필터 판정, 연구 노드 순번 없음·그림 세로 가운데·비겹침·안내(배분만)·좁은 화면비 안, 결과 버튼 문구.
기존 시험 갱신(구조 변경 반영): `DrinkIngredientExpansionPlayTests.Codex_CultureSeparateFromTaste…`, `DrinkLabPlayTests.Codex_78…`(재료마다 상세를 열어 같은 검사), `FirstPassArtTests`(상세 경로 `…/Viewport/Content/Body`), `UiReadabilityTests`(Key 칸 제거), `CardEducationPlayTests`(본문 + 출처 영역).

## 3. 실제 화면(새 QA 빌드 자동 조작, 1280×720 / 1920×1080 실제 창)

투어 `-cct-autotour-mode feedback1`(신규 QA 전용 `AutoTourFeedback.cs`). 두 해상도 각 34장, `errors=0 exceptions=0`(`fb720/tour-log.txt`, `fb1080/tour-log.txt`), Player.log 예외 0.

| # | 상태 | expected | actual | 증거(720p — 1080p는 `fb1080/fb_1920x1080_*` 같은 이름) | 상태 |
|---|---|---|---|---|---|
| V1 | 카드 도감 전부 미발견 | 실루엣·배지·안내, 정체 비공개 | 일치 | `fb720/fb_1280x720_01_codex_locked.png` | PASS |
| V2 | 미발견 카드 클릭 | 안내 상세(제목 ???), 획득 경로 | 일치(이름·효과 없음) | `_02_codex_locked_detail.png` | PASS |
| V3 | 절반 발견(강제) | 발견 = 원색 그림·이름·효과·"획득 · 등급", 미발견 = 실루엣 | 일치, 그림 칸 720p 75px | `_03_codex_mixed.png` | PASS |
| V4 | 발견 카드 상세 위/아래 | 본문 → 구분선 → 출처 3(문서명·짧은 주소·주소 복사) | 일치, 출처가 본문과 겹치지 않음 | `_04_codex_detail_top_double_shot.png`, `_05_codex_detail_sources_double_shot.png` | PASS |
| V5 | 주소 복사 | 클립보드 = 원문 URL | `match=True`(두 해상도) | `_06_codex_source_copied.png`, tour-log `FB copy` | PASS |
| V6 | 재료 도감 기본·아래 끝 | 그림 격자, 결과 수, 하단 안내 | "결과 87 / 87종 · 발견 12", 7열(720) | `_10_ingcodex_all.png`, `_11_ingcodex_bottom.png` | PASS |
| V7 | 종류 커피 / 상태 미발견 | 필터·칩 표시·결과 수 | 30 / 75, ✓ 칩 | `_12_ingcodex_coffee.png`, `_13_ingcodex_unseen.png` | PASS |
| V8 | 미발견 재료 상세 | 실루엣 큰 그림 + "미발견 · 플레이 중 획득하면 등록됩니다" + 획득처 | 일치 | `_14_ingcodex_detail_unseen.png` | PASS |
| V9 | 검색 "진" / 빈 결과 | 결과·빈 안내·초기화 | 33(맛 설명 일치 포함 — 이후 이름 일치 우선 정렬 추가, 새 빌드 재확인 NOT RUN) / "조건에 맞는 재료가 없습니다…" | `_15_ingcodex_search.png`, `_16_ingcodex_empty.png` | PASS(정렬 개선분 NOT RUN) |
| V10 | 발견 재료 상세 → 목록으로 | 큰 그림·색 잔·본문·문화, 닫으면 필터·목록 유지 | `detailOpen=False visible=87` | `_17_ingcodex_detail_black_tea.png` | PASS |
| V11 | 카드 선택(바텐더/찻집) | 순번 없음, 효과 큰 글, 지식 작은 글, 아래 키 안내 | 일치(긴 효과 "공격력 +25%, 공격 속도 +20%" 2줄, 지식 칸과 비겹침) | `_bartender_rapier_20_card_select.png`, `_tea_bow_20_card_select.png` | PASS |
| V12 | 지식 상세(카드 선택) 위/출처 | 출처 영역 분리 | 일치. **발견 결함**: 스크롤한 본문이 위 안내 줄 밑으로 겹쳐 보임(기존 배치) → 안내 -100·본문 -144로 수정(새 빌드 재확인 NOT RUN, EditMode 시험 통과 분) | `_21_card_learning_top.png`, `_22_card_learning_sources.png` | PASS(내용) / 결함 수정분 NOT RUN |
| V13 | 연구 트리 조회 전용·배분·혼합(두 직업) | 순번 없음, 그림 세로 가운데, 배분 모드 안내 줄 | 일치(우림 연구 7 차 변형 그림 포함) | `_30_study_readonly.png`, `_31_study_alloc.png`, `_32_study_mixed.png` | PASS |
| V14 | 결과 → 성장·조합 보기 | 버튼 문구·상세 제목 | 일치 | `_40_result.png`, `_41_growth_detail.png` | PASS |

madia-ui-ux 판정: (1) 위계 — 그림·효과가 먼저, 보조 글은 작고 흐리게. (2) 노이즈 — 순번·URL 원문 제거, 상태는 글+배지(색만 아님). (3) Unity 실현성 — 720/1080 모두 잘림·겹침 없음(캡처). 좁은 화면비·터치·다국어: NOT RUN(재료 도감·연구 안내는 시험으로 좁은 폭 1274 계산만).

## 4. 남은 것·한계

- NOT RUN: 사람 직접 플레이·실제 OS 입력, 한글 IME 실제 입력(검색은 API로 같은 경로), 좁은 화면비·터치 캡처, 검색 정렬 개선·지식 상세 안내 줄 수정의 새 빌드 캡처(다음 빌드에서 재촬영 예정).
- 아트 부족(만들지 않음): 산지 원두·일부 찻잎/술의 전용 아이콘이 없어 포장(병·틴) 임시 그림으로 보임(도감 미발견 실루엣도 같은 틴 모양) — 기존 `IngredientPackagingIconTests` 불확정과 같은 원인. 잠금 배지는 글 칩("미발견")으로 처리 — 전용 자물쇠 아이콘이 필요하면 아래 요청표.
- 사용자가 게임 중이라는 코디네이터 통보(2026-11-15) 이후 새 투어·빌드 보류.

### 신규 스프라이트 요청(독립 구현은 계속, Aseprite + PixelLab 규칙)

| asset ID | 규격 | 기존 참조 | 필요 이유 |
|---|---|---|---|
| `ui_lock_badge` | 16×16 투명 PNG, Point, 1px 외곽선, 카페 팔레트 | `Resources/ProgressionUI/tree_node_locked.png` 모서리 자물쇠 | 도감·재료 도감 "미발견" 칩 왼쪽 그림(현재 글 칩만) |
| 산지 원두 아이콘(예 `dl_icon_coffee_ethiopia_guji` 등 커피 산지 전 종) | 32×32 투명, 기존 `dl_icon_*` 규격 | `Resources/DrinkLab/dl_icon_coffee_bean.png` | 재료 도감 그림 우선 — 지금은 같은 틴 임시 그림이라 서로 구분 안 됨 |
