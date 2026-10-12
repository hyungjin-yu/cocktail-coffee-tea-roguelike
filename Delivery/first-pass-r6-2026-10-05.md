# 1차 통합 실행본 r6 — 2026-10-05 (GUI QA UI 결함 수정)

Orca task `task_2b9371ca0e1b` / dispatch `ctx_e5c364b8a354`. Unity 6000.3.25f1. 입력: 08 §1(49~50)/§3(88~108)/§4, QA 증거 `기획/ArtProduction/first-pass-qa/2026-10-05/`(result-window, coffee-tree-window, pause-settings-window, codex-detail-window, extracted-title/settings, relaunch.log, r4-baseline.md). 이전: `first-pass-r5-2026-10-05.md`.
**판정: 출시 아님.** 엄격 `BuildRelease.Windows64`는 거부(남은 사유는 bartender 1차 임시 아트 하나).

## 1. 수정한 결함 (QA 증거 → 원인 → 수정)

| # | QA 증거 | 원인 | 수정 |
|---|---|---|---|
| 1 | result-window: 제목 "런 종료", 직업/무기/하이브리드·재료·이번 스페셜티·seed 없음 | ResultView가 08:105~106 이전 형식. "보유 스페셜티"가 실제로는 **누적 획득**(잔액 아님), seed는 Development 빌드에서만 표시 | `ResultSummary`로 전체 런 요약 전달. 제목: 영업 완료 / 쓰러졌습니다 / 영업 종료(시간 초과) / 런을 포기했습니다, 클리어 문구 "오늘의 마지막 주문까지 마쳤습니다." 6줄: 직업·시작무기·하이브리드 / 생존(클리어) 시간·처치 / 칵테일·차 Lv·커피 스킬 해금 / 획득 재료 칵테일·찻잎·원두 / 이번 스페셜티 +N·**잔액(누적−영구 강화 사용, 영구 강화 화면과 같은 식)** / seed(06:98 항상 표시). 1920×1080 기준 제목·문구·본문·버튼 세로 분리. 영구 강화 다녀오면 잔액 갱신(기존 동작 유지) |
| 2 | coffee-tree-window·pause-settings-window: Pause 메뉴가 뒤로 비침 | 하위 화면을 Pause 위에 겹쳐 열면서 Pause를 그대로 둠 | Pause의 커피 트리/설정/조작 안내를 열면 Pause 메뉴를 CanvasGroup으로 숨기고(알파 0·입력 차단, Pause는 논리적으로 열린 상태 그대로 — Esc 순서·시간정지 불변), 닫으면(버튼·Esc 모두) 메뉴 복귀 + **연 버튼으로 포커스 복귀** |
| 2b | coffee-tree-window·r4-baseline: 1단계 "포인트 부족" 노드 대비 낮음 | 노드 버튼 비활성 시 Unity 기본 disabled 틴트(회색·알파 0.5)로 크래프트 바탕이 반투명해짐 | 노드 버튼 disabledColor=흰색 — 상태는 바탕색+사유 문구(08:50)로 구분 |
| 3 | codex-detail-window: 마지막 설명 줄(y475)과 닫기 버튼(y476) 겹침 | 본문 상자 210px에 24pt 7줄(빈 줄 포함) — 세로 Overflow | 패널 1100×520, 본문 상자를 닫기 버튼 위 20px에서 끝냄, 빈 줄 제거, 24pt 고정(축소 없음) + 세로 Truncate(넘쳐도 버튼 위로 안 그림) |
| 3b | r4-baseline: 도감 목록 720p ~8px | 목록 글자 14~22(1080 기준) — 08:49 "720p 본문 최소16px(1080기준24)" 미달 | 뷰포트 1450→**1824(안전 여백48 안 최대)**, 열 350→440/간격 452, 이름 26·본문/풍미/선행/재료 24, 행 높이 68→136(설명 2줄+보조 1줄, 행 안에서 잘라 이웃 행과 겹침 없음, 전문은 클릭 상세). 재료 행도 24·590폭 |
| 4 | extracted-title/settings-window·relaunch.log: 저장 1280×720 창 모드로 재실행 → 실제 1920×1080 창(화면 밖) | 부트 첫 프레임 `Screen.SetResolution`이 표시모드만 바꾸고 크기를 놓침(코드상 값은 1280×720/Windowed로 정상 전달 — 화면 재현은 GUI 금지로 못 함) | `DisplayBootVerifier`(새 파일): 부트 후 2프레임+0.25초마다 실제 `Screen.width/height/fullScreenMode`를 확정값과 비교, 다르면 재적용(최대 3회), `[Settings] display check N: committed … actual … -> ok/reapply` 로그. 설정 10초 미리보기 중엔 건드리지 않음, 저장 파일 불변, -batchmode에선 생략 |

직업 문구·영업 시작 흐름(r5)은 그대로 두었고 이 결함들의 대체로 쓰지 않았다. 아트 원본·manifest·임포트·사용자 세이브 변경 없음, 이미지 생성 없음.

## 2. 수정 파일

- `Assets/_Project/Scripts/UI/ResultView.cs` (ResultSummary 추가, 제목·본문·배치)
- `Assets/_Project/Scripts/Core/GameManager.cs` (이번 런 스페셜티 누적, ResultSummary 전달)
- `Assets/_Project/Scripts/UI/PauseView.cs` (하위 화면 중 메뉴 숨김·포커스 복귀)
- `Assets/_Project/Scripts/UI/SkillTreeView.cs` (비활성 노드 불투명)
- `Assets/_Project/Scripts/UI/CardCodexView.cs` (목록 24/26, 1824 뷰포트, 행 136, 상세 패널)
- `Assets/_Project/Scripts/Meta/SettingsService.cs` (IDisplayAdapter 현재값, VerifyCommittedDisplay, 테스트용 IgnoreNextSize)
- `Assets/_Project/Scripts/Meta/DisplayBootVerifier.cs` + `.meta`(신규, Unity가 생성 guid 5ca22eb6215721d459fa240417e281b8)
- `Assets/_Project/Tests/EditMode/FirstPassArtTests.cs` (QaUiDefectTests 6개)
- `Assets/_Project/Tests/EditMode/SettingsV2Tests.cs` (SettingsV2BootDisplayVerifyTests 1개, 기존 도감 겹침 테스트의 하드코딩 뷰포트 1450→`CardCodexView.ViewportWidth`)
- `Delivery/first-pass-r6-2026-10-05.md`

## 3. 테스트 (EditMode 배치, 실제 실행)

| 회차 | 결과 | 비고 |
|---|---|---|
| r6-1 | 컴파일 실패(내 테스트가 `DefaultCards.LoadAll` 오기 — `CardDatabase`) | 통과로 집계 안 함 |
| r6-2 | 359/361, 실패 2 | ① 기존 `CodexCoffeeNodeTests`가 옛 뷰포트 폭 1450 하드코딩 → 새 폭 상수로 갱신(정당) ② 내 커피 트리 테스트가 Show 없이 노드 조회 → ShowReadOnly 후 검사 |
| **r6-3 (최종, 빌드와 같은 코드)** | **361/361 Passed, 실패·skip 0** (2026-10-04 23:12:35~42Z), `error CS` 0 | `Delivery/logs/editmode-results-2026-10-05-r6-3.xml`, `editmode-log-2026-10-05-r6-3.txt` |

신규: 결과 제목 4종+클리어 문구, 결과 본문 08:106 전 항목(직업·무기·하이브리드 유/무·시간·처치·레벨·커피 해금·재료 3종·이번 스페셜티·잔액·seed, 6줄), Pause 설정/조작 안내 중 메뉴 숨김(알파0·레이캐스트 차단)·닫으면 복귀, 커피 트리 비활성 노드 disabledColor 흰색(15노드), 도감 상세 본문 하단<닫기 상단·Truncate·24 고정·가장 긴 카드 전문 preferredHeight ≤ 상자, 도감 모든 Text ≥24·뷰포트 ≤1824·행 글줄이 행 안에서 끝남, 저장 1280×720 창 모드에서 첫 적용이 크기를 놓치면 검증이 재적용하고 다음 검증은 일치·추가 적용 없음·파일 불변. 커피 트리 Pause 경로(ShowReadOnly 콜백)는 PlayMode 영역 — 미실행. PlayMode 미실행(러너 크래시 기록).

## 4. 빌드·패키지

| 항목 | 값 |
|---|---|
| 엄격 출시 검사 | 거부(정상) `logs/strict-release-attempt-2026-10-05-r6.txt`, 종료 1: Art/v1 bartender 없음 + 1차 임시 아트(bartender) |
| 프리뷰 빌드 | `Builds/FirstPassPreview-r6/CocktailCoffeeTea.exe`, 1.0.0-firstpass, Succeeded·에러 0, `logs/firstpass-preview-build-2026-10-05-r6.txt`, 상태 파일 bartender "needs_fix, 1차 임시 허용". ProjectSettings bundleVersion 1.0.0 복원 확인 |
| **ZIP** | `Builds/CocktailCoffeeTea-FirstPassPreview-2026-10-05-r6.zip`, **77,636,481 bytes**, SHA-256 **`E3BB949E767CFF5FE80C8567A3E27ADE512950A262FD6FF0D8E091B382826FE9`**, 181개 항목, `*_BurstDebugInformation_DoNotShip` 제외 확인(0건) |
| ZIP 안 파일 | `CocktailCoffeeTea.exe` `3070E580…D653`(Unity 스텁, r4/r5와 동일), `CCT.Runtime.dll` `7D30988E376A24902866B3864285EFEF1175A94DC7DDAFA85AD3C72108C3A57F` — `C:\UnityScratch\fp-r6-zipcheck`에 풀어 해시만 확인, **실행 안 함** |
| 실행 | 지시대로 autotour·헤드리스 캡처 없음. 압축 해제 실행 증명은 Codex QA 담당 |

r4(`Builds/FirstPassPreview`, ZIP `C01FB2…`)·r5(`Builds/FirstPassPreview-r5`, ZIP `0351A6…`, `Delivery/screens/first-pass-2026-10-05-r5-headless/`) 보존. 빌드 코드가 `Delivery/logs/firstpass-preview-status.txt`를 r6 내용으로 덮음(r5 사본 `firstpass-preview-status-r5.txt` 보존).

## 5. 알려진 한계 / QA 확인 요청

- **실제 화면 미확인**: 모든 배치·글자 크기는 1920×1080 기준 명목 좌표 테스트. 720p 실제 렌더(결과 6줄 줄바꿈, 도감 2줄 설명 잘림 정도, 상세 패널, Pause 숨김)는 QA 육안 확인 필요.
- **해상도 재적용은 화면 재현 없이 넣은 방어 수정**: 원인(첫 프레임 크기 누락)은 QA 증상과 코드 경로로 추정. QA가 r6을 저장 1280×720 창 모드 data로 재실행해 player.log의 `[Settings] display check` 줄과 실제 창 크기를 확인해야 한다. 재적용 후에도 다르면 `display after 3 reapply` 경고가 남는다.
- 도감 목록은 설명 최대 2줄·풍미/선행 1줄로 잘린다(전문은 상세). 결과 화면 "획득 재료"는 이번 런에서 주운 양(게이지 소모와 무관한 수집량).
- 커피 트리 Pause 경로·결과 화면 실제 런 경로는 PlayMode 영역이라 미실행. 키보드 실입력 미검증(QA 도구 한계와 동일).
- 커밋·푸시 없음.
