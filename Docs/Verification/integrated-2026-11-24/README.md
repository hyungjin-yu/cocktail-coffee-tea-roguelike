# 통합 검수 2026-11-24 (#15 도감·0.25초 + #16 차 음료 오라 + #17 영구 강화 상태)

- 통합: #15 12dad76 + #16 9d30d11 병합(충돌 없음) + #17 + 통합 수정.
- EditMode 전체 1088: 1086 통과 / 0 실패 (1 보류·1 inconclusive는 기존).
- PlayMode 관련 94: 통합 수정 전 1 실패(TeaDrinkAura) → 수정 후 TeaDrinkAuraPlayTests 2/2 통과. 나머지 92 통과, 1 건너뜀(증거 전용).
- 통합 중 발견·수정: (1) TeaDrinkAura가 자기 오브젝트를 비활성으로 두어 LateUpdate가 돌지 않아 오라가 안 켜짐 → 시각 root를 자식으로 분리. (2) 마스터 준비처럼 시간이 멈춘 구간에서 페이드가 0에 머묾 → 투명도는 실시간 포함.
  (3) "소수점 금지" 테스트는 초 단위 소수("0.25초")만 예외 허용.
- 캡처(shots/, 1280x720·1024x768·1920x1080): codex_*, perm17_*, tea_boss6s_longname, weapon_select_*. 전(前) 비교: ../ui-readability-followup-2026-11-23/ (codex_*, permanent_shop_*).
- 로그: editmode.log/xml, playmode.log/xml, playmode-aura.*, build.log.
