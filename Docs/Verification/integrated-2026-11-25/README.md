# 통합 2026-11-25: #15~17 + 총 유도 e2b7fa8 + 히든/차 카드 bda4266·e46bdb2 + 속성 화살 a5d0ec3 + 바닥 위험 b4c701c
- 순차 병합 충돌 해결: PlayerProjectile(유도 + 발사묶음 volleyId + 속성 element/volley 모두 유지, Spawn 인자 homingLevel→element→volley), PlayerProjectileWeapon(FireVolley, homing+element+volley), CardSelectView(GunRangeCard + currentStacks), CardData(BulletHoming을 속성화살 15~18 뒤 19로 — 저장 호환), PlayerStats(EOL 차이로 전체 충돌 → HEAD 기준으로 속성 화살/바닥 위험 블록을 수동 이식).
- EditMode 1120: 1118 통과, 0 실패(1 건너뜀, 1 inconclusive 기존). IntegerOnlyTests 통과(초 단위 소수 예외).
- PlayMode 522: 515 통과, 1 실패(IntegerOnlyPlayTests 초 단위 소수) → 예외 반영 후 2/2 통과, 6 건너뜀(증거 전용 등).
- 캡처(shots/, 커밋하지 않음 236MB)와 xml/log는 이 폴더.
