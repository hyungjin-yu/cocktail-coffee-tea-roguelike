# 총 사거리 카드 → 탄환 유도 검증 (기준 7907b6c, Unity 6000.3.25f1, 로컬)
- EditMode 전체 1093: 통과 1090 / 실패 1 / 보류 1 / 생략 1. 실패 `IntegerOnlyTests.UserFacingDataTexts_HaveNoDecimalNumbers`는 `HiddenSkills.cs:82`("0.5초 이상 멈추면…") 때문이며 이번 변경과 무관한 기존 문제. GunHomingTests 10/10 통과.
- PlayMode(PlayerProjectileWeaponTests·HybridWeaponTests·CardAdviceAndWeaponSkinPlayTests·SpecCombatInputTests·CharacterSelectTests) 32/32 통과(유도 탄 케이스 포함).
- 증거 캡처 GunHomingEvidenceCapture 2/2: 두 직업 모두 총 카드 문구 "탄환이 움직이는 적을 따라갑니다"(1장)/"…더 빠르게…"(2장 이후), 활·검은 "사거리 +15%" 유지. 3단계 유도 탄은 0.07초마다 12.6°→25.2°→27.3°(표적 방향)로 돌아 맞히고 직진 탄은 y=-2.00 직진(피해 15, hp 40→25). gun_flight_*.png, gun_card_*.png, gun-homing-log.txt.
