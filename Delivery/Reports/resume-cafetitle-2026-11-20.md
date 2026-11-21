# 재개 기록 — 카페 첫 화면 v5 + 바 톤 (2026-11-20 08:35)

- 상태: v5(최종 manifest 85a8ba37) 반입·코드 반영·관련 시험(Edit 38/38, Play 21/21)·최종 빌드 실검수(normal×3 49/0, normalpath×6 7/0, savefail 21/0) 완료. ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20.zip` sha256 9fbf101f…fba4. 보고 `Delivery/Reports/qa-cafetitle-2026-11-20.md`.
- 미커밋 변경(커밋 금지 지시): Resources/CafeTitle v5, CafeTitleLayout/Scene/TitleIntro, CafeBackground(QA 전환), CafeTitleCapture/CombatToneCapture(QA), 시험 2파일, Tools/QA/tone_metrics.py.
- 빌드 폴더: CafeTitle-2026-11-20(최종), CafeTitle-2026-11-20-pass1(중단된 1차), ToneCheck-2026-11-20(-b) 측정용. 공개/기존 ZIP 미변경.
- 남은 것: 사람 플레이 체감(톤·보스 대비·입장), 도감 책장 클릭 polygon 범위(Codex), 필요 시 보스 접지 그림자 최소안.
- 재실행: `bash Delivery/Verification/2026-11-20/cafetitle/run-final.sh <빌드>`(회당 150초 상한, 격리 data-dir, 음량 0). 톤: `tone/run-tone.sh`, `tone/run-ranged.sh`.

## r2 (2026-11-20 오후 이어서)
- 보스 접지 그림자 채택, r2 빌드/ZIP 완료(sha256 117b83fe…9441). 보고 `Delivery/Reports/qa-cafetitle-2026-11-20-r2.md`. r1 ZIP 보존.
- 대기: Codex ctx_74dbd7bf4fd8 책장 polygon 결과. 변경 필요 → 계약 반입·TitleLayoutTests·normal 3해상도·r3 ZIP. 불필요 → 증거 포함 r2 기준 worker_done.

## r3 (완료)
- v6 layout(도감 polygon) 반입, r3 빌드/ZIP sha256 f2d21cd1…580b, 보고 `Delivery/Reports/qa-cafetitle-2026-11-20-r3.md`. r1/r2 보존. 남은 것: 사람 플레이 체감.

## r4 (완료)
- 포커스 테두리를 클릭 다각형 모양으로. r4 ZIP sha256 541b7a53…4608, 보고 `Delivery/Reports/qa-cafetitle-2026-11-20-r4.md`. r1~r3 보존.

## r5 (완료)
- hover/선택 윤곽선 제거 → 물체 레이어 조명 반응+명패 색. r5 ZIP sha256 e55dff97…04a5, 보고 `Delivery/Reports/qa-cafetitle-2026-11-20-r5.md`. r1~r4 보존.

## r7 (완료)
- 메뉴 접근·물체 조작(v9 + v10 라디오) + v7 거리 배경 + 메뉴 나무·종이 톤. r7 ZIP sha256 0f7000ab…359c, 보고 `Delivery/Reports/qa-cafetitle-2026-11-20-r7.md`. r1~r5 보존(r6는 중간 빌드).
- r7 최종 공백 마감(10:43~): PlayMode 최종 소스 28/28, r7 future 21/0·savefail 22/0·np720 7/0, 카메라 범위 8/8 경계 안, contact sheet 검토 결함 없음 → r7 그대로 최종(재빌드·새 ZIP 없음).
