# 영구 강화 상세 영역 넘침 수정 — 2026-10-12

구매 조건과 네 무기의 몹 성장 예측을 고정 높이의 Text에 넣어 상세 패널 아래로 넘쳤다. 제목·효과·구매 상태는 고정하고, 조건·예측은 패널 내부의 별도 스크롤 viewport에 넣었다. 본문과 예측의 preferredHeight로 콘텐츠 높이와 배치를 계산하고 여백, RectMask2D, raycast 가능한 viewport, Clamped 스크롤 및 자동 숨김 스크롤바를 적용했다. 구매·저장·성장 수치는 변경하지 않았다.

PermanentSkillView 관련 PlayMode 4/4 통과. 출시 빌드 성공. QA 투어에서는 실제 메뉴 버튼으로 영구 강화창을 열고, 본문 상단과 스크롤 최하단을 1280×720 및 1920×1080에서 확인했다. 마지막 스폰 설명까지 상세 패널 내부에 표시된다. QA는 스크롤 위치를 직접 설정했으며 OS 마우스 수동 조작을 확인한 것으로 보고하지 않는다. 히든 목록 입력은 직전 HUD 보고서의 pointer/휠 검증을 유지한다.

- [상세 상단](../../Docs/Verification/2026-10-12-permanent/review_permanent_detail_top.png)
- [상세 최하단](../../Docs/Verification/2026-10-12-permanent/review_permanent_detail_bottom.png)

최신 로컬 실행본: `Builds/Playable-2026-10-12-ui-final/CocktailCoffeeTea.exe`

ZIP: `CocktailCoffeeTea_1.0.0_ui-final-2026-10-12.zip`

230개 파일 SHA-256 및 ZIP CRC 확인. ZIP SHA-256:
`02b537399da475ddc0d7c0e925ee02db59daa39759da376af982c1e1e299914d`

실행 바로가기는 위 버전으로 갱신했다. 현재 실행 중인 이전 프로세스에는 재시작 후 적용된다. 직업별 신규 맵·몬스터 테마 작업은 별도 진행 중이며 이 실행본에 포함되었다고 보고하지 않는다.
