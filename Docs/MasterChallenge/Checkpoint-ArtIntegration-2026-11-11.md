# Steam 마스터 도전 — 검증 재개 후 중단 기록

2026-11-11. Claude dispatch ctx_d83d37da22f1.

현수막 HUD 가림, VFX 풀 해제, 박람회 성공 알림, 720p 부제, 펼침 중 손과 천 연결, NPC 화면 가장자리 출현, StandardFamily 테스트의 빈 대상 검사 등 D1–D8 수정.

검증: targeted PlayMode 10/10. 전체 PlayMode 358 PASS / 0 FAIL / 11 SKIP는 후속 위치 수정 이전 기준. 전체 EditMode 933 PASS / 1 FAIL / 1 SKIP / 1 Inconclusive; 실패 fixture 수정 후 3/3 PASS. Inconclusive는 미조사.

미완료: NPC 발 미끄러짐, D9 화면 밖 여백 축소 이후 테스트 및 캡처, 런타임 줄 아트 검수, Windows 검토용 빌드 및 빌드 전후 소스 해시 비교. 빌드는 여유 메모리 3878/3775MB로 최소 4096MB 조건 미달하여 실행 안 함. 출시 완료 아님.

상세: Unity 프로젝트 Delivery/Reports/qa-steam-master-2026-11-11.md 6–7절. 다음 작업은 D9 검증 → 증거 검수 → 메모리 조건 충족 시 별도 Windows 검토 빌드. 기존 공모전은 변경하지 않음.

## 후속 갱신 — Windows 검토 빌드 성공

Claude dispatch ctx_a9dcd3493d10. 이전 메모리 중단 상태를 대체하는 최신 결과.

승인 보행 아트 보폭에 맞춰 이동 속도를 보정하고 총 3.5초는 유지: 접근 1초 / 설치 1.5초 / 퇴장 1초. 근거리 400ms 페이드로 등장·퇴장. 최신 targeted PlayMode 10/10 PASS. 전체 suite는 불필요하게 반복하지 않음.

Windows 검토용 빌드 성공(544MB): Builds/SteamMaster-Review-2026-11-11/CocktailCoffeeTea.exe. 입력 2801파일 목록 SHA256 2f46718fcd88ae5af318b7a4d3ba1a2d9df36028cb38bb8d77875142facb876e 빌드 전후 동일. ProjectSettings 변경 없음. 기존 Inconclusive는 assam_tea 아트가 이미 존재해 시험 전제가 충족되지 않은 결과로 게임 결함 아님.

아직 완료 아님: 실제 창에서 1주기 플레이·영상, 펼침 줄/근거리 페이드/품질·원두 효과 아트 검수, 스킨 저장 경로 격리 확인. 사용자 원본 세이브 보호를 위해 실행 전 스킨 경로부터 확인. 실제 실행은 -cct-data-dir의 빈 검수 폴더 사용. 공모전/기존 빌드 미변경, Steam 업로드 미수행.
