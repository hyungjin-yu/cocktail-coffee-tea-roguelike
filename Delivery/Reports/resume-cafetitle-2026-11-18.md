<!-- user-stop-20261118 -->
> 사용자 요청으로 새 개발 중단. 최신 종료 상태는 `Docs/WorkNotes/2026-11-18/2026-11-18_작업종료_카페타이틀과협동_재개.md`와 ArtSource의 `revisions/v4-bar-end/STOP-RESUME.md`를 먼저 읽는다. 아래 09:25 기록은 초기 임시 단계다. 현재 Resources는 v3, focusfix 검증 완료, v4 전체 manifest 미발행·미반입. PC 종료 금지.

# 재개 기록 — 싱글 카페 입장형 첫 화면 (2026-11-18)

- 상태(09:25): 코드·계약·관련 시험(EditMode 24/24, PlayMode 15/15)·실빌드 자동검수(720/1080/4:3/low/future/savefail 전부 PASS) 완료 — **임시 배치 아트 기준**.
- 중간 ZIP: Delivery/CocktailCoffeeTea_CafeTitle_2026-11-18_interim-temp-art.zip (sha256 be6d6a62…dba1).
- 대기: Codex 최종 아트(ArtSource/cafe-title-2026-11-18, v2 단순안: 남쪽 정면 문 → 문턱 뷰 1장 → cut → 남향 메뉴 쇼트).
- 재개 절차: ① ArtSource의 ready PNG와 layout.json을 Assets/_Project/Resources/CafeTitle/로 복사(경로 그대로, layout.json 이름) ② gated Unity EditMode 같은 필터 ③ `-executeMethod CCT.Editor.CafeTitleBuild.Windows64Batch -cct-cafetitle-out Builds/CafeTitle-2026-11-18-final` ④ 드라이버 `-cct-data-dir <격리> -cct-title-capture <out> [-cct-title-capture-mode normal|low|future|savefail]` 720/1080/4:3(설정 json width/height 맞출 것 — 저장된 해상도가 실행 인자를 덮음) ⑤ 연속 프레임 현실성 검수 ⑥ 새 ZIP·보고 갱신.
- 보고서: Delivery/Reports/qa-cafetitle-2026-11-18.md

## 09:30 코디네이터 결정
- 최종 아트 대기로 Task 유지(interim으로 종료 금지). 중간 ZIP은 사용자에게 최종으로 공유하지 않음(내부 검수용).
- Codex: 09:22 기준 책장·옷장·라디오·장부·머신 5건 실생성, 외벽 생성 중, ready 0. ready 전달 받으면 재개 절차 ①~⑥ 진행(불필요한 중간 반복 빌드 없음).
