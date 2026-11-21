# QA — 싱글 카페 입장형 첫 화면 v5 + 칵테일 바 전투 톤 (2026-11-20)

- 기준 아트: `ArtSource/cafe-title-2026-11-18/revisions/v5-2026-11-20/manifest.ready.json` (sha256 85a8ba37…8f04), layout v5 (sha256 9dd36c69…32b7). Codex 소유, 원본 미편집.
- 검수본: `Builds/CafeTitle-2026-11-20` / ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20.zip` (sha256 `9fbf101f0117c45c126498afa167485f76756a6fba09b471347b480dcba4fba4`, 209 파일, BurstDebug 제외, README-검수본.txt 포함).
- 보존: 11-17 싱글 ZIP, 11-18 협동 ZIP, 공개 ZIP, v3/v4 아트 모두 그대로. 협동 Prototype 코드·TitleView 공개 API 미변경. 커밋/푸시/게시 없음. 음량 0.
- 단일 Unity: 모든 Editor 실행은 `Tools/QA/gated-unity.sh` 통과(5388~6272MB, editor 0). RAM 예외 사용 안 함.
- 실제 저장: `%LocalLow%/DefaultCompany/CocktailCoffeeTea` 저장 7파일 sha256 작업 전후 동일 (`Delivery/Verification/2026-11-20/real-save-hash-{before,after}.txt`). 자동 실행은 전부 `-cct-data-dir` 격리 fixture.

## 1. 변경

| 파일 | 내용 |
|---|---|
| Resources/CafeTitle/** | v5 PNG 28개 + layout.json 반입(해시 일치 28/28, 최종 32 manifest와 대조 32/32 일치. bg_barwarm 4종은 11-18 반입본과 동일 해시) — `Delivery/Verification/2026-11-20/import-v5.json` |
| CafeTitleLayout.cs | v5 계약 파싱: sign.textRect/surfaceQuad/textColor/outlineColor/fontPxAt1080, exterior.entrance(doorInteraction, prompt rect·surfaceRect·문구·글자 크기), bridge.views[].signTextRect/signSurfaceQuad |
| CafeTitleScene.cs | 간판 글자를 textRect 가운데·간판 면 RectMask2D로 잘라 외부 장면 변환 안에 표시(별도 UI 머리글 아님). bridge 첫 뷰에만 같은 간판의 투영 글자. 포커스 설명: 화면 가장자리 안으로 밀어 넣기, 옆 명패(left/right)는 물체 반대쪽으로 펼침(노트북 덮음 수정) |
| TitleIntro.cs | v5 입장: 문 클릭 영역 = doorInteraction, 문 옆 실물 명패 위 "입장하기 / 아무 키나 눌러 입장"(Unity 텍스트, 굽지 않음), 명패도 같은 입장 버튼. 자동 입장 없음·입력 소비 규칙 기존 유지 |
| CafeBackground.cs | QA 전용 `ForceOriginalBarForQa`(같은 빌드 전후 비교). 정상 경로 동작 불변 |
| CombatToneCapture.cs | `-cct-tone-original`, `-cct-tone-scene normal|crowd|boss|drop|ranged|rangedcrowd` 측정 fixture(강제 등장, 정상 플레이 아님) |
| CafeTitleCapture.cs | v5 검사 추가(간판 화면 안, 입장 명패 위치, 명패 클릭 입장, 명패 렌더 겹침/잘림, 설명 화면 안). QA 종료 가드 수정(아래 4) |
| 시험 | BattleArenaTests(보정 4종·곱1·veil≤.08·타 맵 불변), TitleLayoutTests(v5 간판·입장·edge-pivot 명패) |

## 2. 빌드·자동 시험 (build/compile)

| 검사 | 결과 |
|---|---|
| EditMode `BattleArenaTests|FeedbackArtTests|TitleLayoutTests|FeedbackBatch3Tests|SaveV2UiTests|StoreInfoTests` | **PASS 38/38** (`Delivery/Verification/2026-11-20/cafetitle/editmode.xml`) |
| PlayMode `TitleViewTests|KeyboardNavigationTests|JobThemePlayTests|RoundVisualVarietyPlayTests` | **PASS 21/21** (`…/cafetitle/playmode.xml`) |
| Windows 빌드 `CafeTitleBuild.Windows64Batch` | **PASS** (`…/cafetitle/build.log`) |

## 3. 실빌드 화면 검수 (자동 입력 주입, QA 배너 표시, 실제 입력 격리, 사람 플레이 아님)

최종 빌드에서 실행 — `Delivery/Verification/2026-11-20/cafetitle/<run>/{results.json,log.txt,*.png}`

| 실행 | 해상도 | 결과 | 주요 내용 |
|---|---|---|---|
| normal720 / normal1080 / normal4x3 | 1280x720 / 1920x1080 / 1440x1080 | **PASS 49/0 ×3** | 무입력 3.2초 외관 대기, 간판 "음료 마스터" 화면 안, 문 옆 입장 명패(문·간판과 겹침 0), 이미 눌린 키 무시, 새 키·문 클릭·명패 클릭 입장, 입장 연속 프레임(문 열림 8단계→문턱 bridge 8뷰→컷→실내), 시작 클릭/키 click-through 0, 스킵 입력 소비, 6메뉴 hover/열기/닫기, 렌더 위치=클릭 결과(8곳), 명패 8개 화면 안·겹침 0·글자 맞음, 설명 화면 안, 방향키/Tab 8개 순환, Enter 열기, 시작→직업 선택, 재진입 짧은 실내 페이드(문 반복 없음), 출입문 종료 |
| np720 / np1080 / np4x3 / nplow720 | 각 해상도, 저이펙트 | **PASS 7/0 ×4** | 3초 무입력 대기 → Enter 1회 입장 → 도착 후 추가 Enter·Space → 5초간 시작/직업선택/전투 0, 미리 선택 없음, 방향키는 시작 표시만, 표시 후 5초 무입력 시작 0 |
| npclick720 / npclick4x3 | 720, 4:3 | **PASS 7/0 ×2** | 문 클릭 1회 입장 + 빈 곳 클릭·Enter·Space → 시작 0 |
| savefail720 (fixture: save.json 자리 폴더) | 720 | **PASS 21/0** | 저장 실패 알림·재시도 버튼, 재시도 실패 유지, 출입문(종료) → 미저장 확인창, 취소 → 카페. 이번엔 검사 후 창이 스스로 종료됨 |
| low720 / future720 | 720 | pass1 빌드에서 **PASS 5/0, 20/0** — 최종 빌드 재실행 안 함(NOT RUN) | 변경(설명 위치·QA 종료 가드)과 무관 경로라 반복 금지 지시에 따라 pass1 결과 인용. 저이펙트 외관 대기·확대 없음 / 미래 버전 보호·시작 차단 안내·임시 플레이 |

pass1(`cafetitle-pass1`, 빌드 `Builds/CafeTitle-2026-11-20-pass1`): normal×3 49/0, low 5/0, future 20/0, savefail 21/0, 이후 **사용자 요청 중단**(STOPPED.md). 실패가 아님.

### 사람 눈 검토(캡처 기준, 실제 모니터 체감 아님)
- 외관: 간판 글자는 간판 판 안 가운데, 4:3 contain에서도 잘림 없음. 입장 명패는 문 왼쪽 벽 실물 판 위.
- 연속성: 문짝이 같은 경첩(왼쪽 문설주)에서 안쪽으로 열림, 프레임 간 경첩 위치 고정, 문턱 접근 뷰는 같은 방(2인석·카운터 끝)이 이어짐. 첫 bridge 뷰에서 간판 하단만 보이고 글자도 간판 면에서 잘림(같은 세계 간판). 마지막 문턱 뷰 → 남향 실내는 명시적 컷(연속 카메라 이동 주장 아님). 페이드만으로 입장 처리하지 않음.
- 실내: 바 남단(화면 오른쪽 끝) 노트북=영구 강화, 장부=최근 기록, 라디오=설정, 게시판=제작 정보 명패가 각 물체 옆 실물 정합. 독립 연구 테이블 없음.
- 발견·수정: 영구 강화 hover 설명이 노트북을 덮던 것 → 물체 반대쪽으로 펼침(`hover_permanent_fixed_crop.png`). 오른쪽 끝 책장 설명이 화면 밖으로 나갈 위험 → 화면 안으로 밀어 넣기.
- 미해결(아트 계약): 도감 책장 클릭 polygon이 책장 그림의 가운데~오른쪽 일부만 덮음(`cafetitle-pass1/hover_crops_1080.png`). 이웃과 겹침은 없음. Codex 확인 요청.

## 4. QA 창이 닫히지 않던 문제 (11-20 사용자 관찰)

- 재현 조건: savefail fixture QA 창. 원인 ① fixture가 일부러 저장 실패 상태 → 드라이버 마지막 `Application.Quit()`와 OS X 닫기 둘 다 `QuitGuard`(Application.wantsToQuit)의 미저장 확인창으로 취소됨. ② QA 드라이버가 실제 키보드·마우스를 꺼 둬 그 확인창을 사람이 누를 수 없음. Windows 닫기 요청을 무시하는 별도 코드는 없음.
- 일반 게임: 변경 없음. 저장 정상이면 바로 종료, 미저장이면 실제 입력으로 "저장 재시도/저장 없이 종료/취소" 선택(08:178 설계).
- 수정(QA 경로만): 드라이버 시작 시 `QuitGuard.AllowNextQuit()`(사람 X 닫기 즉시 종료), 결과 기록 후 실제 입력 장치 복구, 실패 분기도 종료, 실행 스크립트 회당 `timeout -k 5 150`.
- 확인: 최종 savefail720이 확인창 취소 검사 뒤 스스로 종료(PASS). 사람이 X로 닫는 동작 자체는 자동 검수하지 않음(NOT RUN).

## 5. 칵테일 바 전투 톤 (싱글 바텐더 bg_bar만)

- 채택: 보정 아트 4부속 override + 곱 RGB 1 + veil 0.06(≤0.08). 맵 ID `bg_bar`·스폰·판정·저장 불변. 찻집/공용 카페/협동 불변(EditMode).
- 측정: 같은 빌드(`Builds/ToneCheck-2026-11-20`, 적탄용 `-20b`)에서 원본/보정 전환, 정상·군중·보스·드롭·적탄·적탄+군중 × 1080/720/저이펙트, 총 32회. 일시정지 프레임 제외. `Delivery/Verification/2026-11-20/tone/summary.json`, `cmp_*.png`(`_gray` 흑백).

| 항목 | 보정 전 → 후 |
|---|---|
| 배경 평균 휘도 | 28~29 → 58~60 |
| 어두운 면적(<40) | 0.86~0.89 → 0.16~0.17 |
| 밝은 면적(>200, 씻김) | 최대 1.25% → 최대 1.55% (흰 막 없음) |
| 적 대비(주변 링 휘도차) | normal 24→20, crowd 33~37→21~27, boss 16~17→12~15 |
| 드롭 대비 | 58~62 → 43~53 |
| 적탄 대비(원거리 적 6 강제 등장 fixture) | 67~73 → 46~53 |

- 판정: 배경 가독성(어두워 묻힘)은 개선, 씻김 없음. 적·드롭·적탄은 흑백에서도 윤곽 유지, 플레이어는 더 잘 보임. 대비 수치는 전부 감소 — 개선이라 주장하지 않음.
- 보스(갈색 그라인더 골렘): 갈색 바닥 위 대비가 가장 낮음. 어두운 외곽선·금색 테두리로 실루엣은 구분됨(`cmp_boss_crop_1080.png`) → 명백한 구별 불가 아님, 그림/판정 변경 안 함. 필요 시 최소안: 보스 발밑 접지 그림자(반투명 타원) 또는 보스 주변 바닥 국소 어둡게(veil 국소). 사람 확인 필요.
- 적탄은 실제 RangedAttackDriver 발사 경로지만 등장은 측정용 강제(정상 플레이 표본 아님).

## 6. 미검증 / 남은 것
- 사람 직접 플레이·실제 모니터 체감: NOT RUN (톤, 보스 대비, 입장 연출 체감).
- 사람이 QA 창 X 닫기: NOT RUN(코드 경로 수정·자동 종료만 확인).
- low720/future720 최종 빌드 재실행: NOT RUN(pass1 PASS 인용).
- 도감 책장 클릭 영역 범위: 아트 확인 대기.
