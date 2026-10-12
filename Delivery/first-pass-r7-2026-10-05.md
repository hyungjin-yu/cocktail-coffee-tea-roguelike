# 1차 통합 실행본 r7 — 2026-10-05 (도감 목록 렌더 회귀 수정)

Orca task `task_db14fa2979c5` / dispatch `ctx_a3a4ba602194`. Unity 6000.3.25f1. 이전: `first-pass-r6-2026-10-05.md`.
**판정: 출시 아님.** 엄격 `BuildRelease.Windows64` 거부 유지(남은 사유는 bartender 1차 임시 아트 하나).

## 1. r6 회귀 (QA 실제 720p 캡처)

`기획/ArtProduction/first-pass-qa/2026-10-05/r6-codex.png`, `r6-codex-list.png`: 카드 이름과 "???" 전부 공백, 재료 합계 줄("칵테일 재료 — 총 N개 수집 (6/6종)") 없음, 설명이 첫 줄에서 끊김(예: "해금 조건: 레이피어 시작, 이번 런 칵테일"). 품종 줄·상세 패널은 보였다.

**원인:** r6이 글자를 24/26으로 키우면서 행 글 상자를 고정 px(이름 34, 설명 64, 풍미 32, 재료 머리 34)로 두고 `VerticalWrapMode.Truncate`를 켰다. Unity Text는 Truncate일 때 상자에 다 들어가지 않는 줄을 **통째로 그리지 않는다**. 동봉 Noto Sans KR의 줄 높이(≈1.45em)는 26pt ≈ 38, 24pt ≈ 35, 24pt 두 줄 ≈ 70이라 이름·합계는 0줄, 설명은 1줄만 남았다(720p 캔버스 배율 0.667의 픽셀 반올림이면 더 빡빡해진다). r6 테스트는 상자 좌표와 글자 크기만 봐서 "문자열이 있다 = 보인다"로 잘못 통과했다.

## 2. 수정

- `UiFactory.LineHeight(size, lines)`: 동봉 폰트로 `TextGenerator.GetPreferredHeight`를 재서 실제 줄 높이를 구한다(1080 기준). `TruncatedBoxHeight = ceil(줄 높이 × 1.2 + 4)`: 720p 반올림 여유, 정수 px라 행 경계에 누적 오차가 없다.
- `CardCodexView`: 행 이름(26pt 1줄)·설명(24pt 2줄)·풍미/선행(24pt 1줄)·영구 강화 설명(3줄 상자)·재료 머리/품종 상자 높이를 모두 위 측정값으로 계산한다. 행 간격과 재료 행 높이도 거기서 나온다. 재료 행은 y −136(발견 수 아래, 탭 위). 글자 크기 24/26은 유지(720p 16px 이상, 08:49), 뷰포트 1824·열 440 유지. 상세 패널은 r6 그대로(QA 통과).
- 다른 화면은 QA가 r6 실기에서 확인 완료(결과 전 항목, Pause 숨김·포커스 복귀, 문구·영업 시작/뒤로, 저장 720p 창 모드 재실행 `display check 1 … ok`)라 건드리지 않았다.

## 3. 렌더 기준 테스트 (`Tests/EditMode/FirstPassArtTests.cs` `RenderedTextLayoutTests`)

각 Text의 **실제 생성 설정과 상자 크기**로 `TextGenerator.Populate`를 돌려, 1080(배율 1)과 720p(배율 2/3)에서 그려지는 글자 수를 잘림 없는 생성과 비교한다.
- `Checker_DetectsR6Geometry_…`: r6 구성(26pt 이름, 34px Truncate 상자)을 이 검사가 "안 보임"으로 잡는지 먼저 확인하고(26pt 줄 높이 > 34 포함), r7 높이에서는 전부 보이는지 확인.
- `Codex_UndiscoveredState_…`(빈 임시 세이브: 대부분 "???", 합계 0, 품종 ???)와 `Codex_FullyDiscoveredState_…`(카드 35장·품종 전부 발견, 합계 12345/6789/4321): 발견 수, 재료 3칸 합계·품종, **56행 전부의 이름(카드 35 + 영구 6 + 커피 15)이 두 배율에서 모든 글자 표시**, 설명은 2줄(영구 강화 3줄)까지 표시, 풍미/선행은 첫 줄 표시. 가장 긴 카드 상세 본문·제목은 전부 표시.
- `Result_StatsBlock_RendersAllLines_AboveButtons`: 최대값 결과 요약이 두 배율에서 줄바꿈 없이 6줄, 상자 아래가 버튼 위, 제목·클리어 문구 전부 표시.

| 회차 | 결과 | 비고 |
|---|---|---|
| r7-1 | 364/365, 실패 1 | 기존 `CodexCoffeeNodeTests`의 커피 머리 ↔ 첫 커피 행 Overlaps — 측정 높이가 소수(≈169.3)라 맞닿은 경계에 부동소수 오차. 상자 높이를 정수 px(ceil)로 바꿔 해결. 새 렌더 테스트는 이 회차에도 전부 통과 |
| **r7-2 (최종, 빌드와 같은 코드)** | **365/365 Passed, 실패·skip 0** (2026-10-04 23:25:01~08Z), `error CS` 0 | `Delivery/logs/editmode-results-2026-10-05-r7-2.xml`, `editmode-log-2026-10-05-r7-2.txt` |

## 4. 빌드·패키지

| 항목 | 값 |
|---|---|
| 엄격 출시 검사 | 거부(정상) `logs/strict-release-attempt-2026-10-05-r7.txt`, 종료 1: Art/v1 bartender 없음 + 1차 임시 아트(bartender) |
| 프리뷰 빌드 | `Builds/FirstPassPreview-r7/CocktailCoffeeTea.exe`, 1.0.0-firstpass, Succeeded·에러 0, `logs/firstpass-preview-build-2026-10-05-r7.txt`, 상태 파일 bartender "needs_fix, 1차 임시 허용". bundleVersion 1.0.0 복원 확인 |
| **ZIP** | `Builds/CocktailCoffeeTea-FirstPassPreview-2026-10-05-r7.zip`, **77,637,140 bytes**, SHA-256 **`ABC64C5AF0BED19BC2338F6C8986A7D1172F37CA324560493DFB1796F11A6C36`**, 181개 항목, BurstDebugInformation_DoNotShip 0건 |
| ZIP 안 파일 | `CocktailCoffeeTea.exe` `3070E580…D653`(Unity 스텁, 이전과 동일), `CCT.Runtime.dll` `E2B12BAC9B3A196D8B19CE51DF95EDB36CD6A418CC52721A60C23A8057F97E8C` — `C:\UnityScratch\fp-r7-zipcheck`에 풀어 해시만 확인, 실행 안 함 |

r4~r6 빌드·ZIP·증거 보존. 빌드 코드가 `Delivery/logs/firstpass-preview-status.txt`를 r7 내용으로 덮었다.

## 5. 수정 파일

`Assets/_Project/Scripts/UI/UiFactory.cs`, `Assets/_Project/Scripts/UI/CardCodexView.cs`, `Assets/_Project/Tests/EditMode/FirstPassArtTests.cs`, `Delivery/first-pass-r7-2026-10-05.md`.

## 6. 한계

- 렌더 검사는 Unity TextGenerator(실제 Text와 같은 생성기·설정)를 쓰지만 화면 캡처가 아니다. **실제 720p 도감 화면 확인은 QA 몫**: 이름·???·합계 표시, 설명 2줄, 행 간격 증가로 생긴 스크롤 길이.
- 행에서 설명은 2줄, 풍미/선행은 1줄까지만 보이고 그 이상은 잘린다. 전문은 클릭 상세에 있다.
- PlayMode 미실행, 커밋·푸시 없음.
