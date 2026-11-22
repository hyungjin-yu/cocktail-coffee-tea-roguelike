# QA — 타이틀 메뉴 데스크톱 Esc + 직업 선택에서 카페로 돌아가기 (r7c, 2026-11-21)

- 작업 공간 `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing`, 브랜치 `codex/coffee-brewing`. Unity 6000.3.25f1.
- 출발점: `qa-v9-menu-runtime-2026-11-21.md` F1(설정 "닫기 · Esc" 표기인데 Esc 미동작, 제작 정보·도감·스킨 Esc 미처리)·F2(직업 선택 1단계에서 타이틀로 돌아갈 수단 없음). 사용자 결정 "지금 고쳐".
- 빌드 `Builds/CafeTitle-2026-11-21-r7c`(CafeTitleBuild.Windows64Batch, 소스 최종본 10:54 이후 10:58 빌드).
- 공개 ZIP `Delivery/Beverage-Master-Windows-Demo-2026-11-21_cafe-r7c.zip` sha256 `2b69bbbadd7af533944d7e54ec0b68a0fcacd725dc64b9d15a335379e0c676ce` (273 파일).
- 증거 루트(로컬, git 제외): `Delivery/Verification/2026-11-21/esc-back/`. 모든 실행은 격리 data-dir·음량 0. 실제 저장 7파일 해시 전후 동일(`v9-menu/real-save-hash-before.txt`와 대조).
- push·main 병합·itch 업로드·릴리스 없음. r7b 및 이전 ZIP 미변경.

## 변경
| 파일 | 내용 |
|---|---|
| `UI/TitleView.cs` | `HandleMenuEscape()` + `Update`에서 데스크톱 Esc 연결. 메뉴가 열려 있을 때만(루트 숨김) 설정→제작 정보→도감→스킨 순으로 가장 위 하나만 닫음. 영구 강화·최근 기록은 자체 Esc라 제외(이중 처리 없음). 실내(메뉴 없음)·접근 연출 중 Esc는 기존 CafeMenuApproach 취소 경로 그대로. 터치 UI는 MobileBackRouter 담당. |
| `UI/SettingsView.cs` | `HandleEscape()`: 조작 안내 → 해상도 확인창(되돌리기, 설정 유지) → 닫기 버튼과 같은 경로(ui_cancel+Hide). 전투 일시정지 쪽은 GameManager.RequestEscape가 계속 담당(호출 안 함). |
| `UI/CreditsView.cs` | `HandleEscape()`, 버튼 표기 "닫기 · Esc"(터치는 "닫기"). |
| `UI/CardCodexView.cs` | `HandleEscape()`: 상세 열림 → 상세만, 아니면 뒤로와 같은 Close. 표기 "뒤로 · Esc". |
| `UI/SkinShopView.cs` | `HandleEscape()`, 표기 "뒤로 · Esc". |
| `UI/CharacterSelectView.cs` | 직업 단계에 `TitleBackButton`(이름 BackButton, 무기 단계 "뒤로"와 같은 왼쪽 아래 자리, 표기 "카페로 · Esc") → `BackToTitle()`(선택 화면 파괴 → `GameBootstrap.ShowTitle`: 짧은 재진입 페이드, 미리 선택 없음, 전투 시작 없음). `HandleEscape()`: 설정/도감 → 무기 단계면 직업 단계 → 직업 단계면 카페. 같은 프레임 Esc는 한 단계만, 다른 화면(영구 강화·연구 노트북·음료 제조)이 같은 Esc로 닫히며 다시 그려진 프레임은 무시. 무기 단계 "뒤로" 표기도 "뒤로 · Esc". |
| `Core/CafeTitleCapture.cs` | QA 드라이버 검사 추가: `esc_{settings,credits,codex,skins}_label/_closes_and_returns`, `esc_codex_detail_first`, `select_title_back_button`(화면 안·겹침 없음·표기·최상단 클릭), `select_weapon_escape_to_job`, `select_job_escape_to_title`, `select_back_no_preselect`. 기존 "선택 화면 강제 파괴 후 ShowTitle"을 실제 Esc 경로로 교체. |
| 시험 | `TitleViewTests` +5, 기존 실패 시험 4건 보정(아래). |

## 1. 빌드·자동 시험
| 검사 | 상태 | 결과 | 증거 |
|---|---|---|---|
| EditMode 전체 | PASS | 1049 중 1047 통과, 실패 0 (Inconclusive 1 `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`, Skip 1 `IntegerOnlyTests.Migration_UserSaveCopy_IsolatedRun` — 둘 다 이번 변경 무관) | `editmode.xml` |
| PlayMode `TitleViewTests\|CharacterSelectTests\|KeyboardNavigationTests\|SettingsViewTests\|PauseTests\|ResultViewTests\|MobileTouchPlayTests\|CardCodexViewTests\|SkinShopViewTests\|SkinShopListTests\|RunHistoryPlayTests\|PermanentSkillViewTests\|FirstRunTutorialTests` | PASS | 85 중 84 통과, 실패 0, Skip 1 | `playmode.xml` |
| 기존 실패 확인(변경 전 HEAD에서 같은 4건) | 기록 | HEAD 68c79bc에서 4/4 실패 확인 → 이번 변경 무관, r7 접근 연출(즉시 열리지 않음) 이전 가정의 시험 | `playmode-baseline-HEAD.xml` |
| Windows 빌드 r7c | PASS | 580MB, 오류 0 | `build.log` |

기존 시험 보정: `SkinShopViewTests.TitleSkinButton_OpensShop_AndBackReturnsToTitle`, `RunHistoryPlayTests.PauseQuitToTitle_…`, `MobileTouchPlayTests.BackRouter_InRun_…`은 메뉴가 접근 연출 뒤 열릴 때까지 기다리도록(최대 4초) 수정. `SkinShopViewTests.TitleSkinButton_StaysInsideMenuFrame`은 배치 파일이 없을 때의 옛 세로 메뉴 전용 검사라 카페 장면이 있으면 Ignore(위치·겹침은 `TitleLayoutTests.CafeHotspots_AreNamedButtons_InSafeArea_NoOverlap` 담당).

새 PlayMode 시험(모두 PASS): `Escape_ClosesSettingsCreditsCodexSkins_AndReturnsToRoom`, `Escape_LeavesPermanentAndHistoryToOwnHandlers`, `Escape_ClosesTopmostLayerOnly`(도감 상세·해상도 확인창·조작 안내), `Escape_KeyboardEvent_ClosesCredits`(가상 키보드 Esc 이벤트로 Update 경로), `CharacterSelect_BackAndEscape_ReturnToTitle_NoRunStart`.

## 2. r7c 실빌드 QA 드라이버(자동 진행 — 사람 조작 화면 아님)
스크립트 `run-r7c.sh <빌드>`. 가상 장치 입력.

| 실행 | 결과 | player.log |
|---|---|---|
| normal720 (1280×720) | **PASS 102/0** (r7 85항목 + 신규 17) | exception 0 |
| normal1080 (1920×1080) | **PASS 102/0** | exception 0 |
| normal4x3 (1440×1080) | **PASS 102/0** | exception 0 |
| low720 | PASS 10/0 | 0 |
| np720 (입장 후 Enter/Space 자동 시작 0) | PASS 7/0 | 0 |
| future720 (새 버전 저장 보호) | PASS 21/0 | 0 |
| savefail720 (저장 실패 확인창) | PASS 22/0 | 4줄 = 의도된 저장 실패 fixture의 `IOException` 로그(r7 같은 실행도 4줄) |

기존 8개 메뉴 회귀: 위 85항목(접근→조작→메뉴→닫기→역재생·원시점, hover, 키보드 Tab/방향키/Enter, 시작→직업 선택, 종료 문·저장 실패 확인창) 세 해상도 모두 통과.

대표 캡처: `driver_new_screens.png`(720·4:3 직업 단계 "카페로 · Esc", 무기 단계 "뒤로 · Esc", 도감 상세, 스킨 "뒤로 · Esc"), 각 해상도 폴더의 `esc_*_menu/_returned`, `select_job_step_back_button`, `select_weapon_step`, `select_back_room`.

시각 판독(캡처 기준): "카페로 · Esc" 버튼은 무기 단계 "뒤로 · Esc"와 같은 자리, 연구 노트북 왼쪽, 겹침·잘림 없음(드라이버 `select_title_back_button` overlaps=[]). 각 메뉴 버튼 표기 한 줄 안에 들어감.

## 3. 실제 OS 입력(사람 조작 경로)
| 검사 | 상태 | 비고 |
|---|---|---|
| 720/1080/4:3 실제 키 Esc로 4화면 닫기·복귀, 직업 선택 버튼·Esc로 카페 복귀, 8개 메뉴 회귀 | **NOT RUN — 주인 일정 대기** | 게임 창 전면 입력이 필요해 주인이 PC를 비켜줄 때 진행하기로 함(코디네이터 지시). |

## 4. 공개 ZIP r7c
- 구성: r7c 빌드 게임 파일 208개(r7b와 같은 목록) + r7b의 `LICENSE_NotoSansCJK.txt`·`THIRD_PARTY_NOTICES.md`·`Licenses/` 원문 그대로 + 11-21 README(이번 변경·조작 안내 추가, AI 고지 유지) + 전 파일 `SHA256SUMS.txt`. 스크립트 `make_public_zip.py`, README 원문 `README_r7c.txt`.
- 검증(`public-zip-verify.txt`, `verify_public_zip.py`): 새 폴더에 풀기 → 파일 273개, r7b와 파일 목록 동일, SHA256SUMS 272항목 불일치 0, 게임 파일 208개 r7c 빌드와 바이트 동일, BurstDebug(DoNotShip)·pdb·저장/설정/QA 결과 파일 없음, README 파일명 정상(`README_실행방법.txt`).
- 풀린 공개 ZIP 자체의 실행: NOT RUN(게임 파일이 위 드라이버 검수한 r7c 빌드와 바이트 동일 — 주인 PC 사용 중이라 창을 띄우지 않음).

## 5. 남은 것 / 다음 제안
1. 실제 OS 입력 검수(§3) — 주인이 PC를 비켜줄 때.
2. 도감 상세 창의 "닫기" 버튼은 Esc로 닫히지만 표기에 Esc가 없음(표기와 모순은 아님). 원하면 다음 빌드에서 "닫기 · Esc"로 맞춤.
3. itch 업로드(r7c ZIP 교체)·push·병합은 별도 지시.
