# QA — 카페 첫 화면 검수본 r2: 갈색 보스 접지 그림자 (2026-11-20)

- 기준: r1 `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20.zip`(sha256 9fbf101f…fba4, 보존·재검증 OK) + 아래 변경.
- r2: `Builds/CafeTitle-2026-11-20-r2`, ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20_r2.zip` sha256 `117b83fe497c92564aa3169b12e6dde356bd7be0b56d81b06c911ea84bb79441` (209 파일, README r2 문구 포함).
- 아트 계약: v5 manifest 85a8ba37 / layout 9dd36c69 그대로. 책장 polygon은 Codex(ctx_74dbd7bf4fd8) 확인 대기 — 계약 임의 수정 없음.
- 게이트: 모든 Editor 실행 gated-unity.sh 통과, RAM 예외 없음. 실행 창 회당 150초 상한·격리 data-dir·음량 0. 실제 저장 7파일 해시 불변. 커밋/게시 없음.

## 변경

| 파일 | 내용 |
|---|---|
| `Scripts/Enemies/Enemy.cs` | 보스면 발밑 그림자를 `CafeBackground.ApplyWarmBarBossGround`로 넘김 |
| `Scripts/Core/CafeBackground.cs` | 보정 바 바닥 사용 중(`UsesWarmBar`)일 때만 보스 ground_shadow 알파 0.22→0.5, 폭 radius×2.4→×3.0. 정렬 Shadow(0) < GroundZone(1) < Actor(2): 위험 예고·보스 윤곽을 가리지 않음. 스폰·판정·피해·저장·다른 맵·일반 적 불변. QA 비교 스위치 `BossGroundBoostForQa` |
| `Scripts/Core/CombatToneCapture.cs` | `-cct-tone-noboss-ground`(보완 끄기), 보스 장면 마지막 샷에 보스 실루엣 마스크 캡처(측정 전용) |
| `Tests/EditMode/BattleArenaTests.cs` | 보정 바 아닐 때 그림자 불변·알파 상한·정렬 순서 검사 |

## 측정 (보스 실루엣 마스크 기준 가장자리 휘도차, fixture: 보스 즉시 등장 — 정상 플레이 표본 아님)

| 실행 | 보완 끔 | 보완 켬 | 비고 |
|---|---|---|---|
| 1차 1080 (ToneCheck-c) | 가장자리 2.5 / 발 2.4 / 분리도 .311 | **6.2 / 7.6 / .339** | |
| 1차 720 저이펙트 | 1.4 / 0.8 / .311 | **3.5 / 5.0 / .309** | |
| r2 빌드 1080 | 5.6 / 6.8 / .313 | **6.5 / 8.2 / .346** | 보정 전 원본 바: 11.7 / 13.9 / .487 |
| r2 빌드 720 저이펙트(켬만) | — | 7.0 / 7.8 / .429 | |

- 비교안 B(몸 뒤 바닥 국소 어둡게, halo 0.25): 분리도 .31→.13으로 나빠져 **기각**, 코드 제거.
- 판정: 접지 그림자 A는 세 쌍 모두 가장자리·발 대비가 개선됐고 나빠진 항목은 없어 **채택**. 개선 폭은 작고(r2 쌍 +0.9/+1.4) 보정 전 수준(11.7)은 회복하지 못함. 사람 체감 확인 필요.
- 근거: `Delivery/Verification/2026-11-20/bossground/{boss-edge-metrics.json,cmp_boss_variants*.png}`, `…/r2/{boss-edge-metrics-r2.json,cmp_boss_r2_orig_off_on.png}`.

## 검사

| 검사 | 결과 |
|---|---|
| EditMode `BattleArenaTests|FeedbackArtTests|TitleLayoutTests|FeedbackBatch3Tests` | PASS 26/26 |
| PlayMode `JobThemePlayTests|RoundVisualVarietyPlayTests|TitleViewTests` | PASS 10/10 |
| r2 실빌드 첫 화면 회귀 np720 / normal1080 | PASS 7/0, 49/0 |
| r2 실빌드 보스 장면 on/off/저이펙트/원본 4회 | 실행·캡처 완료 |

## 미검증
- 사람 직접 플레이·모니터 체감: NOT RUN.
- r2 실빌드에서 normal720/4:3, low, future, savefail 재실행: NOT RUN(변경이 전투 보스 그림자에 한정. r1 결과 유효).
- 책장 클릭 polygon: Codex 결과 대기.
