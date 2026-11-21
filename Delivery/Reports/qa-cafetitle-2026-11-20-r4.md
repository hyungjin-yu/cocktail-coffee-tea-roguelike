# QA — 카페 첫 화면 검수본 r4: 포커스 테두리 = 실제 클릭 다각형 (2026-11-20)

- r4: `Builds/CafeTitle-2026-11-20-r4`, ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20_r4.zip` sha256 `541b7a53c1a1118a2a6bc0f98277406818eda3ac1f5ea02085b2b5e285834608` (209 파일). r1/r2/r3 ZIP 보존·해시 재확인 OK.
- 기준: r3 + 아래 변경. layout v6(0d5d4e12…) 그대로, ArtSource 미편집. 보스 등 다른 방향 변경 없음.
- 보호: gated-unity.sh(RAM 예외 없음), 격리 data-dir·음량 0·회당 150초 상한, 실제 저장 7파일 해시 불변, 게임 프로세스 잔존 0. 커밋/게시 없음.

## 변경
| 파일 | 내용 |
|---|---|
| `Scripts/UI/CafeTitleScene.cs` (CafeHotspot) | 클릭 영역이 다각형이면 hover/선택 테두리를 다각형 각 변을 따라 3px 선(기존 색·두께)으로 그림. 사각형 영역은 기존 4변 테두리 유지. 설명 문구·키보드 선택 동작 불변 |
| `Scripts/Core/CafeTitleCapture.cs` (QA) | 도감 테두리 변의 화면 위치 = 클릭 다각형 변(2px 이내), 의상장 hover 시 의상장만 강조 검사 추가 |
| `Tests/EditMode/FirstPassArtTests.cs` | 8개 핫스팟 테두리 변 수·위치 = 다각형, 외접 사각형 테두리 없음, 도감 테두리가 의상장 클릭 영역을 지나지 않음 |

## 검사
| 검사 | 결과 |
|---|---|
| EditMode `TitleLayoutTests|FeedbackBatch3Tests` | PASS 15/15 |
| PlayMode `TitleViewTests|KeyboardNavigationTests` | PASS 15/15 |
| r4 실빌드 normal720 / normal1080 / normal4x3 | PASS 56/0 ×3 — 도감 테두리 7/7 변 일치(최대 오차 0.00px), 선반 위·가운데·아래 hover/클릭=도감, 아래 선반 클릭 열림, 의상장 3점=스킨, 의상장 hover 시 도감 강조 없음, 방향키·Tab 8개 순환·Enter로 도감 열기, 명패 겹침 0, 기존 8메뉴·입장 검사 전부 |

증거: `Delivery/Verification/2026-11-20/r4/<run>/{results.json,log.txt,*.png}`, `r4/r4_focus_contact_1080.png`(도감·의상장·영구 강화·설정 테두리).

## 미검증
- 사람 직접 플레이·체감: NOT RUN.
- r4에서 normalpath·low·future·savefail 재실행: NOT RUN(포커스 테두리 표시만 변경, 입력·저장 경로 불변).
