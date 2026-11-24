# #25 정식 레시피 시그니처 통합 (df2e99c + 124c928) — 2026-11-27
- 병합 충돌: Enemy.cs(양쪽 유지), PlayerStats.cs(EOL 전체 충돌 → HEAD 기준으로 DepartureCap·signatureMoveSpeed 이식).
- Signature Art Validate 통과(batch exit 0). EditMode 1148/1150 통과 0 실패, PlayMode 538/544 통과 0 실패(6 증거 전용 건너뜀), 최종 커밋 fdefcd6.
- 새 PlayMode `SignatureLivePlayTests` 5건: 세 레시피 실제 제조·첫 발견/반복 판정, 미발견 도감 비공개, 최초(전체)/반복(짧은)/저이펙트 연출, 출발 버프+런 컵 공존, Q/HUD 1회·재사용·재충전 방지, 화이트 아웃 배지는 경직 성공 일반 적만, 사이드카 5초 후 원복.
- 수정: DrinkLabTests 쿨다운 상한 기대(5%→출발 상한 12%), DrinkSignatureTests의 EditMode 적 등록, 사용 문구 조사(솔트 림은), HUD 효과 줄 2줄·18pt.
- 캡처: shots/signature (720p/1080p/4:3, 저이펙트 일부).
