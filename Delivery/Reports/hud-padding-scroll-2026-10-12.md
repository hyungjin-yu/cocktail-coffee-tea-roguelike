# HUD 여백과 히든 목록 스크롤 수정 — 2026-10-12

## 문제와 수정

HUD 글자가 박스 테두리에 붙고 긴 발동 설명이 박스를 넘었다. 해당 라벨에 좌우 16, 상하 10의 내부 여백을 적용하고 Wrap/Truncate 경계를 지정했다. 발동 안내는 이름과 효과를 별도 줄에 배치하고 정보 박스 및 조작 버튼 높이를 조정했다. 작은 히든 버튼도 텍스트가 사라지지 않도록 높이를 늘렸다.

히든 상세 목록은 행 이미지와 텍스트가 raycastTarget=false이고 viewport에는 Graphic이 없었다. 그래서 ScrollRect가 드래그/휠 입력을 받을 경로가 없었다. viewport에 투명한 raycast 그래픽을 추가하고 Clamped 이동과 자동 숨김 스크롤바를 연결했다. 내용이 화면 안에 모두 들어가면 스크롤바는 숨겨진다.

## 실행 검증

출시 빌드와 QA 투어 빌드 성공. 1280×720 및 1920×1080 QA 실행에서 각각 화면 19장을 캡처했고 오류 0, 예외 0이었다. 긴 핫 토디 설명과 양쪽 정보 박스 및 조작 버튼을 육안 확인했다.

히든 스킬 24개를 QA로 직접 부여한 뒤 EventSystem.RaycastAll로 viewport의 드래그 대상이 ScrollRect인지 확인했다. 주입한 pointer begin/drag/end 이벤트로 내용의 Y 위치가 720p에서 270, 1080p에서 180 이동했다. 주입한 휠 이벤트는 두 해상도에서 Y=120으로 이동했다. 실제 OS 마우스 수동 조작이나 자연 해금 플레이 검증으로 보고하지 않는다. 게임 전투 수치는 변경하지 않았다.

- [HUD 긴 문구와 내부 여백](../../Docs/Verification/2026-10-12-hud/review_hidden_long_text.png)
- [24개 목록 드래그와 스크롤바](../../Docs/Verification/2026-10-12-hud/review_hidden_drag.png)

## 최신 로컬 실행본

`Builds/Playable-2026-10-12-hud-padding-scroll/CocktailCoffeeTea.exe`

ZIP: `CocktailCoffeeTea_1.0.0_hud-padding-scroll-2026-10-12.zip`

230개 파일 SHA-256 및 ZIP CRC 확인.

ZIP SHA-256: `73365d8a0d05bd01560624554655dc52f5cd9dd9bf7929f286f73690aa0b6438`

실행 바로가기는 이 버전으로 갱신했다. 이미 실행 중인 이전 버전에는 재시작 전까지 적용되지 않는다. 이전 회귀 결과는 기존 보고서에 보존하며 이번 UI 수정 후 전체 테스트를 다시 실행한 것으로 표기하지 않는다.
