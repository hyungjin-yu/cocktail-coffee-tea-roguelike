# Steam 마스터 도전 — 검증 재개 후 중단 기록

2026-11-11. Claude dispatch ctx_d83d37da22f1.

현수막 HUD 가림, VFX 풀 해제, 박람회 성공 알림, 720p 부제, 펼침 중 손과 천 연결, NPC 화면 가장자리 출현, StandardFamily 테스트의 빈 대상 검사 등 D1–D8 수정.

검증: targeted PlayMode 10/10. 전체 PlayMode 358 PASS / 0 FAIL / 11 SKIP는 후속 위치 수정 이전 기준. 전체 EditMode 933 PASS / 1 FAIL / 1 SKIP / 1 Inconclusive; 실패 fixture 수정 후 3/3 PASS. Inconclusive는 미조사.

미완료: NPC 발 미끄러짐, D9 화면 밖 여백 축소 이후 테스트 및 캡처, 런타임 줄 아트 검수, Windows 검토용 빌드 및 빌드 전후 소스 해시 비교. 빌드는 여유 메모리 3878/3775MB로 최소 4096MB 조건 미달하여 실행 안 함. 출시 완료 아님.

상세: Unity 프로젝트 Delivery/Reports/qa-steam-master-2026-11-11.md 6–7절. 다음 작업은 D9 검증 → 증거 검수 → 메모리 조건 충족 시 별도 Windows 검토 빌드. 기존 공모전은 변경하지 않음.
