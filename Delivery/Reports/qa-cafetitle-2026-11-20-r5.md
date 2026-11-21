# QA — 카페 첫 화면 검수본 r5: 윤곽선 제거 → 물체 조명 반응 (2026-11-20)

- r5: `Builds/CafeTitle-2026-11-20-r5`, ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20_r5.zip` sha256 `e55dff97a26039362b581d92a3801c86a7145c9c9f2bc26e9b8c2d18158504a5` (209 파일). r1~r4 ZIP 보존·해시 재확인 OK.
- 범위: 카페 실내 메뉴 hover/키보드 선택 표시만. 클릭 다각형(layout v6 0d5d4e12…)·클릭·저장·입장·전투 불변. ArtSource 미편집, 새 아트 생성 없음.
- 보호: gated-unity.sh(RAM 예외 없음), 격리 data-dir·음량 0·회당 150초 상한, 실제 저장 7파일 해시 불변, 게임 프로세스 잔존 0. 커밋/게시 없음.

## 사전 확인(가용성)
- 실내 아트는 물체별 분리 레이어다: g1 카운터(시작), g2 노트북(영구 강화), g3 책장(도감), g4 의상장(스킨), g5 장부(최근 기록), g6 라디오(설정), g7 게시판(제작 정보), g8 출입문(종료) — 각각 480x270 투명 PNG, 물체 픽셀만 불투명(manifest alphaBounds 확인). → 명패만 바꾸는 대체안이 아니라 물체 자체 조명 반응을 쓸 수 있음.
- layout feature에는 레이어 연결 필드가 없어 구현 기본 매핑(위 목록)을 쓰고, 향후 layout에 `layer`가 오면 그 값을 우선.

## 변경
| 파일 | 내용 |
|---|---|
| `Scripts/UI/CafeTitleScene.cs` | 윤곽선(r4 다각형 변·사각 테두리) 코드 제거. 각 메뉴 물체 레이어에서 밝은 사본(같은 크기·같은 알파, 면은 남은 밝기의 최대 14%를 따뜻하게 올리고 어두운 외곽선 픽셀은 거의 유지)을 만들어 원 레이어 바로 위·앞 물체 아래에 두고, hover/선택 때 0.12초 한 방향 페이드(펄스·빛기둥·화면 밝힘 없음). 명패 바탕·글자 색을 조금 따뜻하게. 짧은 설명 유지. FocusVisible = hover 또는 키보드 선택 |
| `Scripts/UI/CafeTitleLayout.cs` | feature 선택 필드 `layer` 읽기 |
| `Scripts/Editor/CafeTitleImportSettings.cs` + 실내 PNG 10개 `.meta` | 실내 레이어만 isReadable(실행 중 밝은 사본 생성용). 픽셀·크기·필터 불변 |
| `Scripts/Core/CafeTitleCapture.cs` (QA) | 평상시 강조 0, 도감 hover 시 도감만 조명 반응, 의상장 hover 시 의상장만, 키보드 선택 물체만 조명, 윤곽선 없음 검사와 비교 캡처 추가 |
| `Tests/EditMode/FirstPassArtTests.cs` | 8개 물체: 윤곽선 없음, 조명 사본이 원 레이어 바로 위·같은 rect, 평상시 0, 클릭 판정 없음, 알파 동일·밝기만 상한 안에서 상승 |

## 검사
| 검사 | 결과 |
|---|---|
| EditMode `TitleLayoutTests|FeedbackBatch3Tests` | PASS 15/15 |
| PlayMode `TitleViewTests|KeyboardNavigationTests` | PASS 15/15 |
| r5 실빌드 normal720 / normal1080 / normal4x3 | PASS 58/0 ×3 — 평상시 강조 0, 도감 hover: 도감만 조명 1.00·나머지 0·윤곽선 없음, 의상장 hover: 의상장만, 키보드 선택(최근 기록)만 조명, 선반 3점·의상장 3점 클릭 대상 유지, 명패 겹침 0·화면 안, 설명 화면 안, 6메뉴 열기/닫기, 키보드 Tab/방향/Enter, 입장·시작·재진입 |
| 화면 픽셀 비교(평상시 vs 도감 hover 정착, `r5/highlight-diff.json`) | 720/1080/4:3 모두 **바뀐 픽셀 중 책장 물체·도감 명패/설명 밖 0개**, 책장 평균 휘도 +11.3(최대 +20.6), 정착 후 0.3초 간격 두 장 차이 0픽셀(깜빡임 없음) |

증거: `Delivery/Verification/2026-11-20/r5/<run>/{results.json,log.txt,idle_room_*.png,codex_hover_settled_*.png,skins_hover_neighbor_*.png,keyboard_focus_*.png,hover_*.png}`, `r5/codex_idle_vs_hover_1080.png`.

## 미검증
- 사람 직접 플레이·실제 모니터에서 "은은함" 체감: NOT RUN(밝기 상승량 14%는 조정 가능 상수 `CafeTitleScene.LightLift`).
- 터치 기기: NOT RUN(터치에서는 hover가 없고 명패는 항상 보임 — 기존과 같음).
- r5에서 normalpath·low·future·savefail 재실행: NOT RUN(표시만 변경, 입력·저장 경로 불변).
