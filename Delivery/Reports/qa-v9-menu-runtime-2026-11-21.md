# QA — v9 메뉴 조작 아트 반입 확인·실행 검수 (2026-11-21)

- 작업 공간: `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing`, 브랜치 `codex/coffee-brewing`
- 검수 대상: 공개 ZIP `Delivery/Beverage-Master-Windows-Demo-2026-11-20_cafe-r7b.zip`(sha256 `f4756761…4518`)을 스크래치 폴더에 새로 풀어 실행. 모든 실행은 격리 data-dir·음량 0. 실제 저장 7파일 해시 전후 동일(`Delivery/Verification/2026-11-21/v9-menu/real-save-hash-{before,after}.txt`). 실행 후 게임 프로세스 0, 모든 player.log exception 0.
- 증거 루트(로컬, git 제외): `Delivery/Verification/2026-11-21/v9-menu/`
- 공개본·기존 ZIP·ArtSource 원본 미변경. push·main 병합·itch 업로드·릴리스 없음.

## 1. 반입 상태 — 이미 반입됨, 재반입 불필요

| 확인 | 결과 | 근거 |
|---|---|---|
| v9 layout | `Resources/CafeTitle/layout.json` sha256 `a93a383f…` = v9 manifest `layoutSha256` | sha256 대조 |
| v9 PNG 48장 | 43장 v9 manifest 해시 일치. `settings_01~05` 5장은 v9와 다르고 **v10 라디오 manifest**(`revisions/v10-radio-dial`) `settings_00~05` 6장과 모두 일치. v10은 v9 위에 라디오 6장만 교체하는 후속판(HANDOFF 명시) → 의도된 상태, v9로 되돌리지 않음 | 스크립트 대조 |
| 이전 버전 혼입 | `ready/approach/` 48장 외 없음, layout v9 그대로 | 위와 같음 |
| r7b 포함 | r7b ZIP 273개 중 게임 파일 208개가 `Builds/CafeTitle-2026-11-20-r7`과 바이트 동일, 나머지 65개는 라이선스·README·SHA256SUMS. r7은 v9+v10 반입 후 빌드(`qa-cafetitle-2026-11-20-r7.md`) | ZIP sha256 대조 |

결론: **v9(+v10 라디오)는 r7 → r7b 공개본에 이미 들어가 있음.** 이번 작업은 실행 검수만 함.

## 2. 빌드/컴파일·자동 시험

| 검사 | 상태 | 결과 | 증거 |
|---|---|---|---|
| Unity 재빌드 | NOT RUN | 코드·자산 변경 없음 → r7 빌드 그대로 검수 | — |
| EditMode `TitleLayoutTests\|FeedbackBatch3Tests\|ArtRuntimeConsumerTests\|BakedArtCatalogTests\|CodexDetailTests\|QaUiDefectTests` | **PASS** | **50/50** (10:41, RAM 게이트 5345MB 통과) | `editmode.xml`, `editmode.log` |
| PlayMode `TitleViewTests\|KeyboardNavigationTests\|PauseView\|SettingsView\|ResultView` | **PASS** | **28/28** (10:42) | `playmode.xml`, `playmode.log` |

참고: 10:15 첫 시도는 RAM 게이트(2736MB < 4096MB)로 거부됨. 사용자가 메모리를 비운 뒤 실행.

## 3. QA 드라이버 실행(자동 진행 — 정상 사용자 화면 아님)

`-cct-title-capture` 드라이버가 Input System 가상 장치로 입력을 넣는 자동 진행. 기능 판정 근거로만 쓰고 사람 조작 화면으로 보고하지 않음. 스크립트 `run-v9.sh <빌드>`.

| 실행 | 해상도 | 결과 |
|---|---|---|
| normal720 / normal1080 / normal4x3 | 1280×720 / 1920×1080 / 1440×1080 | **85/0 ×3** |
| low720(저이펙트) | 1280×720 | **10/0** |
| np720(입장 후 Enter/Space 자동 시작 0) | 1280×720 | **7/0** |

## 4. 실제 OS 입력 검수(사람 조작과 같은 경로)

- 실행: r7b를 QA 인자 없이 일반 실행. 720 = 1280×720 창, 1080 = 1920×1080 테두리 없는 전체 화면(설정 `borderless`), 4:3 = 1024×768 창(위아래 레터박스).
- 입력: 게임 창이 전경일 때만 OS 수준 입력 — 마우스 이동/클릭 `mouse_event`(절대 좌표), 키 `keybd_event` 80ms 누름. 동작 전마다 전경 창을 확인하고 아니면 중단(포커스를 빼앗지 않음). 화면은 `orca computer get-app-state` 캡처.
- 무효 처리: (a) `orca computer press-key`는 down/up이 한 프레임에 들어가 Unity `wasPressedThisFrame`이 놓침, (b) `SetCursorPos`만으로는 hover가 생기지 않음 → 두 방식의 캡처는 `manual-invalid/`로 분리하고 판정에 쓰지 않음. 캡처 지연이 약 1초라 접근 이동의 중간 프레임은 일부만 잡힘(프레임 단위 근거는 §3 드라이버 캡처).
- 1차 진행 중 Orca 창이 전경으로 와(사용자 사용) 규칙대로 중단·보고했고, 허락 후 재개함.

### 결과

| 검사 | 720 | 1080 | 4:3 | 증거 |
|---|---|---|---|---|
| Enter로 외관→실내 입장 | PASS | PASS | PASS | `manual/r{720,1080,4x3}_00_exterior`, `_01_interior` |
| hover 8개(조명 반응 + 설명 명패) | **PASS 8/8** | **PASS 8/8** | **PASS 8/8** | `real720_hover_pairs.png`; `manual/r*_10_hover_*.png`(바닥 대비 변화 영역 8개 모두 해당 물체 위치) |
| 설정(라디오) 클릭→다이얼 조작→메뉴→닫기→역재생·원시점 | PASS | PASS | PASS | `manual720_contact.png`, `real1080_menus.png`, `real4x3_menus.png` |
| 제작 정보(게시판) 같은 흐름 | PASS | PASS | PASS | 위와 같음 |
| 영구 강화(노트북) 클릭→메뉴→**Esc**→원위치 | PASS | PASS | PASS | 위와 같음 |
| 최근 기록(장부) 클릭→메뉴→**Esc**→원위치 | PASS | PASS | PASS | 위와 같음 |
| 도감(책장) 클릭→메뉴→뒤로 버튼→원위치 | PASS | PASS | PASS | 위와 같음 |
| 스킨(진열장) 클릭→메뉴→뒤로 버튼→원위치 | PASS | PASS | PASS | 위와 같음 |
| 시작(카운터) 클릭→카운터 접근→직업 선택 | PASS | PASS | PASS | `real_start_quit.png`, `manual/r*_5*_start_*` |
| 종료(문) 클릭→문 접근·열림→정상 종료(저장 정상 경로라 확인창 없이 종료, exception 0) | PASS | PASS | PASS | `manual/r*_60_quit_acting.png`, `data/real*q/player.log` |
| Tab ×9: 시작→최근 기록→영구 강화→제작 정보→설정→스킨→도감→종료→시작(순환), Shift+Tab 역순, →/↓ 공간 이웃 | PASS | Tab 5회→설정 PASS | Tab 5회→설정 PASS | `real720_keyboard_tab.png`, `manual/r*_2*` |
| 키보드 Enter로 메뉴 열기(720: 도감·영구 강화, 1080/4:3: 설정) → 닫기 → 원위치 | PASS | PASS | PASS | `manual/r720_3*`, `r720_4*`, `r*_2[2-4]_*` |
| 메뉴에서 돌아온 직후 Space → 시작 안 됨 | PASS | — | — | `manual/r720_45_space_after_return_no_start.png` |
| 설정 화면 Esc(버튼 표기 "닫기 · Esc") | **FAIL** | 미재실행(같은 코드) | 미재실행 | `manual/720_settings_3_return.png`, `_4_settled` (1차 세션) |

## 5. 발견 사항

### F1. 타이틀 설정 화면 "닫기 · Esc" 표기인데 데스크톱 Esc 미동작 (Minor, 기존 결함, v9 무관)
- 재현: r7b 실행 → Enter 입장 → 라디오 클릭 → 설정 → Esc(실제 키 80ms) → 설정이 그대로 열려 있음.
- 원인: `SettingsView`는 Esc를 직접 처리하지 않음. 전투 중에는 `GameManager.RequestEscape` → `PauseView.CloseSettings`가 닫지만 타이틀에는 대응 처리가 없음(`TitleView`에 Esc 처리 없음 — HEAD `a2d0f5e`에도 없음). `MobileBackRouter`는 터치 UI에서만 설치됨.
- 같은 계열: 제작 정보·도감·스킨도 타이틀에서 Esc로 닫히지 않음. 버튼 표기에 Esc가 없어 표기와 모순은 없지만, Esc로 닫히는 영구 강화·최근 기록과 동작이 다름.

### F2(관찰). 직업 선택 1단계에서 타이틀로 돌아가는 수단 없음 (기존 동작, v9 무관)
- 시작 → 직업 선택 화면에 뒤로 버튼이 없고 Esc도 반응 없음. `CharacterSelectView`는 무기 단계에만 "뒤로"(직업 단계로)가 있음. 이번 변경과 무관한 기존 설계라 기록만 함(코드 미변경).

### F1 수정 방안(코드 변경 안 함 — 사용자 결정 대기)
1. **타이틀 전용 Esc 라우터 하나**: `TitleView.Update`(이미 `HandleTab`이 있는 곳)에 `HandleMenuEscape()` 추가. 조건은 데스크톱(`!MobilePlatform.IsTouchUI`), `Approach`가 접근 중이 아닐 것(접근 중 Esc는 기존 취소 경로), `quitConfirmRoot` 비활성.
   - 우선순위: 설정의 해상도 확인창이 열려 있으면 `RevertResolution()`(안전 쪽) → 도감 상세가 열려 있으면 `HideDetail()` → 그 외 `settingsView`·`creditsView`·`codexView`·`skinView` 중 열린 것을 각 닫기 버튼과 같은 경로로 닫음(`ui_cancel` 효과음 + `Hide()` → 기존 `ShowRoot` 콜백 → 역재생·원시점).
   - 영구 강화·최근 기록은 자체 Esc가 있으므로 제외(이중 처리 방지). 한 프레임에 한 화면만 닫음.
   - 닫은 프레임의 Esc가 이어지는 역재생에서 "새 입력=건너뛰기/취소"로 다시 읽히지 않게 `CafeMenuApproach.PollSkip`의 `armed`(떼어짐 대기) 동작을 확인하고 시험으로 고정.
   - 전투 일시정지의 `SettingsView`는 별도 인스턴스(TitleView가 만든 것만 대상) → 전투 Esc 동작 불변.
2. **표기 일관성**: 제작 정보 "닫기", 도감·스킨 "뒤로"에도 `UiFactory.InputLabel(…, "Esc")`를 붙여 영구 강화("뒤로 · Esc")와 맞춤(터치 UI는 기존처럼 숨김).
3. **시험**: PlayMode `TitleViewTests`에 화면별 "열기→Esc→닫힘·원시점 zoom 1.00·입력 재개" 4건, 해상도 확인창 위 Esc→되돌리기 1건. `CafeTitleCapture`에 Esc 경로 항목 추가(현재 `Back()`은 버튼이 있으면 버튼을 누름).
4. 범위: `UI/TitleView.cs`, `UI/CreditsView.cs`·`UI/CardCodexView.cs`·`UI/SkinShopView.cs`(표기), 시험 1~2파일. 이후 재빌드, 회귀 실행(normal 3해상도·np720·실제 Esc 입력), 필요 시 공개 ZIP 갱신(r7c)은 별도 지시.

## 6. 미검증
- 사람이 직접 하는 플레이·모니터 체감(연출 속도·자연스러움·작은 동작 판독·톤): NOT RUN.
- 터치 입력: NOT RUN.
- 1440×1080(4:3 테두리 없는 전체 화면) 실제 입력: NOT RUN(4:3은 1024×768 창으로 대체, 1440×1080은 §3 드라이버만).
- 저이펙트 실제 입력: NOT RUN(§3 드라이버 10/0만).
- 저장 실패(QuitGuard 확인창) 경로 실제 입력: NOT RUN(r7 드라이버 savefail 22/0 인용).

## 7. 다음 제안
1. F1 수정 여부 결정 → 수정하면 위 방안대로 구현·시험·재빌드 후 r7c 공개 ZIP(업로드는 별도 지시).
2. F2(직업 선택에서 타이틀로 돌아가기)는 기획 판단 필요.
3. push·main 병합·itch 업로드는 이번 범위 밖 — 필요하면 별도 지시.
