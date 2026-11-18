# QA — 싱글 첫 화면 "카페 입장형" (2026-11-18, 중간: 임시 배치 아트)

상태: **코드·계약·관련 시험·실빌드 자동입력 검수 완료(임시 배치 아트 기준). 최종 카페 아트(Codex) 미도착 → 현실성(공간·문 경첩·문턱 그림) 검수 미완. 사람 직접 플레이 미실시.**

- 기준: 기획/싱글_카페입장형_첫화면_2026-11-18.md(현실성 최상위), AGENTS.md, 프로젝트 CLAUDE.md·madia-ui-ux·game-feel-first, 기존 TitleView/TitleIntro·관련 시험.
- 메모리 게이트: 모든 Unity 실행 `gated-unity.sh` 정상 통과(4619~5714MB), 예외 미사용. 협동 Prototype·기존 아트 원본·ArtSource(Codex 소유) 미변경. 커밋/푸시/게시 없음.

## 1. 아트 계약 (Claude 구현 ↔ Codex)
- `Delivery/CafeTitle-2026-11-18/layout-contract.md` v1.1: 기준 1920x1080, **정규화 좌상단 (0,0) → 우하단 (1,1)**, 장면 480x270 아트px, feature id ↔ 기존 버튼 이름 고정(start=StartButton, permanent=PermanentShopButton, codex=CardCodexButton, skins=SkinShopButton, history=RunHistoryButton, settings=SettingsButton, credits=CreditsButton, quit=QuitButton), hotspot rect|polygon, label_anchor/side, nav_order, exterior door frames+hinge+threshold+glimpse+camera, interior arrival, **bridge{seconds, blend cut|crossfade, views[png,t,camera]}**, 4:3 `contain`(aspectPolicy.mode) 지원.
- Codex 초안 v1(서쪽 입구 + 90도 회전 bridge 5뷰) → 코디네이터 지시로 **v2 단순안 승인**: 남쪽 정면 문 → 북향 직진 → 문 개폐 8프레임·문턱 40cm 통과 뷰 1장 → **명시적 cut** → 같은 방 남향 메뉴 쇼트. 구현은 blend=cut이면 교차·가짜 회전 없이 끊어 전환(bridge 0장이어도 외부→실내 cut).
- Codex v2 `layout.json`(draft, PNG 없음) 키 호환 확인. 최종 PNG·ready 미도착.

## 2. 구현 (싱글만)
| 파일 | 내용 |
|---|---|
| `Scripts/UI/CafeTitleLayout.cs`(신규) | layout.json 읽기(최소 JSON), 최종 `Resources/CafeTitle/layout` → 없으면 `layout_temp` |
| `Scripts/UI/CafeTitleScene.cs`(신규) | 외부/bridge/실내 컨테이너, 레이어(타일은 같은 아트 밀도, 늘림 없음), hotspot Button(기존 이름)·다각형 판정·이름표(Unity 텍스트)·hover/focus 얇은 테두리+짧은 설명, cover/contain 맞춤, 카메라(중심·배율) 같은 변환, 공간 기준 방향키 이웃, Tab 순서 |
| `Scripts/UI/TitleIntro.cs`(교체) | 입장: 외부 0.45s → 문 프레임(경첩, 아트) 0.8s → 문턱 접근(exterior.camera) → [bridge views] → 실내 도착 → 정착, 전체 2.6s. 입력 = 건너뛰기만, 누른 입력이 떼어질 때까지 메뉴 차단. 재진입 0.25s 실내 페이드(문 반복 없음), 저이펙트 0.7s(확대 없음). 느린 프레임 한 프레임 최대 0.1s |
| `Scripts/UI/TitleView.cs`(수정) | 중앙 세로 메뉴·오른쪽 위 스킨/왼쪽 위 기록 버튼 제거 → 실내 물체 8개(같은 동작). 제목 상단 안전영역, 임시 배치 배지, 저장 알림·버전/저장 상태·임시 플레이·다시 시도·종료 확인 그대로(+알림 외곽선). 배치 파일 실패 시 기존 세로 메뉴로 기능 보존. 기본 선택은 입장 후 |
| `Scripts/Core/CafeTitleCapture.cs`(신규) | 실빌드 검수 드라이버(`-cct-title-capture` + `-cct-data-dir` 필수, 정상 실행 경로 무관) |
| `Scripts/Editor/CafeTitleImportSettings.cs`, `CafeTitleBuild.cs`(신규) | Resources/CafeTitle 픽셀 스프라이트 임포트, 별도 폴더 빌드 |
| `Resources/CafeTitle/layout_temp.json`(신규) | **임시 배치**(기존 바 벽·바닥·카운터·찻집 벽/카운터·아이콘·의상 스프라이트) — 최종 아님 |
| 시험 수정 | FeedbackBatch3(옛 소품/어둠판 순서 → 입장 차단·건너뛰기·재진입), FirstPassArt TitleLayoutTests(옛 세로 버튼 위치 → 8물체 이름·안전영역·다각형 표본 겹침·계약 파싱·임시 표시), SaveV2Ui(저장 알림 ↔ 실내 물체 겹침 추가), KeyboardNavigation(기본 선택은 입장 종료 후) |
| 보존 | 작업 전 `TitleView.before.cs.txt`, `TitleIntro.before.cs.txt` (Delivery/CafeTitle-2026-11-18/) |

## 3. 검사
### 3.1 Unity 시험 (gated, 관련 범위만)
- EditMode `TitleLayoutTests|FeedbackBatch3Tests|SaveV2UiTests|StoreInfoTests`: **24/24 PASS**
- PlayMode `TitleViewTests|KeyboardNavigationTests`: **15/15 PASS**
- 전체 오래된 시험 반복 안 함.

### 3.2 실빌드 자동입력 (Builds/CafeTitle-2026-11-18, 가상 마우스·키보드 → 실제 InputSystem 이벤트, 격리 저장)
| 실행 | 해상도 | 결과 | 비고 |
|---|---|---|---|
| normal | 1280x720 | **36/0** | 최종 빌드 |
| normal | 1920x1080 | **36/0** | r1 빌드(차이=저장 알림 외곽선뿐) |
| normal | 1024x768 (4:3) | **36/0** | r1 빌드, cover 크롭에서도 8물체·이름표·저장 띠 화면 안 |
| low(저이펙트 fixture) | 1280x720 | **3/0** | 0.7s, 확대 없음(배율 1.000 고정) |
| future(미래 버전 fixture) | 1280x720 | **8/0** | 최종 빌드: 시작 물체 눌러도 진행 안 함+안내, 임시 플레이 선택 |
| savefail(저장 실패 fixture) | 1280x720 | **9/0** | r1 빌드: 다시 시도 유지, 출입문(종료) → 확인창 → 취소. 로그 예외 54건 = 의도한 fixture 쓰기 실패(IOException) |

normal 항목: 첫 진입 전체 연출, 실제 2.61s에 실내 도착, **시작 물체 위 건너뛰기 클릭·Enter 건너뛰기가 메뉴를 누르지 않음**, 6물체 각각 **그려진 위치=레이캐스트 최상위 대상(hit=render)**, hover 테두리, 열기→화면 캡처→뒤로/닫기→카페 복귀(입장 반복 없음), 방향키 이동(StartButton→…), Tab 8개 순환, Enter로 도감 열기, 시작=직업 선택, 타이틀 재진입 0.25s, **출입문 클릭 → 정상 종료(프로세스 exit 0)**.
증거: `Delivery/Verification/2026-11-18/cafetitle/<run>/` (results.json, log.txt, intro_t*.png 연속 프레임, hover_*/menu_*/keyboard_focus/reentry_room/start_to_select/future_*/savefail_*/low_* 캡처).

### 3.3 화면 검수 (직접 확인)
| 항목 | 결과 |
|---|---|
| 기능 흐름·이름표·포커스 | 이름표 작게 물체 옆/아래, hover/focus만 얇은 테두리+설명, 큰 공중 버튼·네온 없음 — PASS |
| 저장 안전 안내 | 미래 버전 안내·임시 플레이 버튼 읽힘(외곽선 추가) — PASS |
| 4:3·1080 | 잘림·겹침 없음 — PASS(임시 배치 기준) |
| **현실성(공간·원근·접지·한 광원·문 경첩 개폐·문턱 통과·외부/실내 문 연속성)** | **미검수 — 임시 배치는 기존 바 벽 반복·임의 소품 조합이라 현실적 카페가 아님. 입장 문은 프레임 아트가 없어 문짝을 투명하게 할 뿐(최종 연출 아님).** 최종 아트 도착 후 같은 드라이버로 연속 프레임 재검수 필요 |

## 4. 저장·보존·납품
- 실제 사용자 저장: 08:34 기준 해시 **불변**(`save-hash-after.txt`). 모든 실행이 격리 `-cct-data-dir`.
- 기존 ZIP 보존 확인: 싱글 11-17 FeedbackReview(sha 39a09d87…), 협동 11-17 final(3974c22a…), 협동 11-18 Map(4a5edc06…).
- **중간 검수본**: `Builds/CafeTitle-2026-11-18/`(README, `Play-CafeTitle-Review.cmd` = 폴더 안 review-save 격리 저장으로 실행) · `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-18_interim-temp-art.zip` 210파일 104MB sha256 `be6d6a62f5a5e70bf94c96f1e14de7b6255a219565bec877896779bfd178dba1` — **임시 아트, 최종 아님**.

## 5. 남은 일 / 재개 지점
1. Codex 최종 PNG + `layout.json`(status ready) → `Resources/CafeTitle/`로 복사(원본 편집 없음) → 임포트 → 같은 시험 + 드라이버로 720/1080/4:3 연속 프레임(외부/문 열림/문턱/cut/실내) 재검수, hotspot = 최종 visible polygon 검사.
2. 현실성 검수(문 경첩 회전·문턱·외부 glimpse와 실내 같은 평면·광원) — 수치/한 장 통과로 판정하지 않음.
3. 최종 검수본 ZIP(새 이름) + 보고 갱신. 사람 직접 플레이 미실시.
