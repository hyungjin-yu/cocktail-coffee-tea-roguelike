# QA — 카페 첫 화면 검수본 r3: 도감 책장 클릭 영역 v6 (+r2 보스 접지 그림자) (2026-11-20)

- r3: `Builds/CafeTitle-2026-11-20-r3`, ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20_r3.zip` sha256 `f2d21cd18257acd52132b40709fe2e9afb74d956b2c6370d6a0d8fea6178580b` (209 파일). r1(9fbf101f…)·r2(117b83fe…) ZIP 보존, 해시 재확인 OK.
- 반입: `ArtSource/cafe-title-2026-11-18/revisions/v6-codex-hotspot/layout.json`(sha256 0d5d4e12…78bc) → `Resources/CafeTitle/layout.json`. v5 대비 차이는 version 5→6과 도감 polygon(4→7 꼭짓점)뿐(구조 diff로 확인). 아트 PNG는 v5 그대로.
- 포함: r2 보스 접지 그림자(보고 `qa-cafetitle-2026-11-20-r2.md`).
- 게이트·보호: gated-unity.sh(RAM 예외 없음), 격리 data-dir·음량 0·회당 150초 상한, 실제 저장 7파일 해시 불변, 게임 프로세스 잔존 0. 커밋/게시 없음.

## 코드
- `CafeTitleCapture.cs`(QA 전용): 도감 polygon의 세 선반 높이(20/50/80%) 안쪽 점 hover·실제 클릭 대상 확인, 아래 선반 클릭으로 열기, 의상장 polygon 안쪽 3점이 스킨에 맞는지(이웃 경계) 검사 추가.

## 검사
| 검사 | 결과 |
|---|---|
| EditMode `TitleLayoutTests|FeedbackBatch3Tests|BattleArenaTests` | PASS 24/24 |
| PlayMode `TitleViewTests|KeyboardNavigationTests` | PASS 15/15 |
| r3 실빌드 normal720 / normal1080 / normal4x3 | PASS 54/0 ×3 — 도감 위·가운데·아래 선반 hover+클릭 대상=도감, 아래 선반 클릭 열림, 의상장 3/3 스킨, 명패 8개 겹침 0·화면 안, 방향키/Tab 8개 순환·Enter로 도감 열기, 기존 8메뉴·입장 검사 전부 |
| r3 실빌드 np720 | PASS 7/0 (입력 1회 입장, 추가 Enter/Space 시작 0) |

증거: `Delivery/Verification/2026-11-20/r3/<run>/{results.json,log.txt,codex_shelf_*_hover_*.png}`, `r3/codex_hover_r3_crop.png`.

## 관찰·미검증
- 도감 포커스 테두리는 polygon 외접 사각형이라 화면상 의상장 오른쪽 가장자리에 걸쳐 그려진다. 실제 클릭은 polygon 기준이라 의상장 클릭은 스킨으로 간다(검사 통과). 테두리를 polygon 모양으로 그리는 것은 범위 밖.
- r3에서 low/future/savefail, np 1080/4:3, click 경로 재실행: NOT RUN(layout 도감 polygon만 변경. r1 결과 유효).
- 사람 직접 플레이·체감: NOT RUN.
