# 레벨업 메뉴판 Unity 반입·검수 (2026-11-22)

- 베이스 7fb00e4 + 아트 1c965a6(cherry-pick). 항목 7(coffee-brewing)은 검수 시점에 7fb00e4에서 새 커밋이 없어 통합 대기(읽기 전용 확인만).
- 실행: Unity 6000.3.25f1 batchmode(창 없음), 가상 키보드 — 실제 PC 입력·포커스 미사용.
- EditMode LevelUpMenuArtTests 6/6(재실행 전 float 허용오차 수정분은 PlayMode 실행 때 같이 컴파일), PlayMode LevelUpMenu* + 회귀(PlayFeedback1122, HybridWeapon, CardEducation, KeyboardNavigation, CardAdvice, Pause) 55/55.
- PNG: `*_1280x720` / `*_1920x1080` (flight=전달 중, lastpose=마지막 포즈, reveal=70 ms 전환 중, settled=정착). 후보는 실제 카드 자산에서 등급별로 고른 강제 제시(Show 직접 호출).
- 증거 로그: evidence-log.txt(같은 시나리오 두 번 기록된 줄은 두 시험 클래스 실행 흔적).
