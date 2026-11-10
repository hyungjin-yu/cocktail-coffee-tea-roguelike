# QA 보고 — 끝없는 재료 모으기 · 음료 순환 상성 · 계열 스폰 · 보상 분리 — 2026-11-10

담당: Claude(설계·구현). 브랜치는 `codex/coffee-brewing`이며 커밋하지 않은 작업 트리다. **Git 변경은 하지 않았다**(커밋·푸시·vault 기록은 코디네이터 담당).
상세 설계와 규칙: [Docs/EndlessAffinity-2026-11-10.md](../../Docs/EndlessAffinity-2026-11-10.md).
최신 지시에 따라 **신규 VFX·환경 연출·상성 전용 이펙트는 만들지 않았다.** 기존 연출, 정수 표시, 타격감, HUD도 바꾸지 않았다.

## 1. 조사 근거(읽은 파일)

- vault 명세 3종 전체. CLAUDE.md, `.claude/skills/madia-ui-ux`. AGENTS.md는 저장소에 없다.
- 읽은 코드: `GameManager`(EndRun, OnEnemyKilled, 정산, 기록), `EnemySpawner`, `EnemyRoster`, `BossDirector`, `Balance`, `Enemy`(EnemyDef, TakeDamage), `DamageNumbers`, `RangedAttackDriver`, `SummonerDriver`, `PlayerProjectile`, `RapierWeapon`, `CoffeeBrewRuntime`, `DrinkLab`(Craft, Sell, EndRun, RecoverAbandoned, Coins), `DrinkStore`, `DrinkLabView`, `SkinCatalog`, `SkinShopView`, `DrinkShareCode`, `CharacterSelectView`, `RunPhaseBadge`, `ResultView`, `RunHistory`, `ActiveEffects`.
- 재화 조사 결과
  - 스킨 재화는 `drinks.json`의 `coins`(판매 코인)다. `coins`가 늘어나는 곳은 `DrinkLab.Sell` 하나뿐이다.
  - 연구 포인트와 스페셜티가 코인으로 바뀌는 경로는 없다(`coins` 증가 위치를 전부 검색).
  - 공유 코드는 레시피만 담는다.
  - **무한 재료 → 제조 → 판매 → 코인 → 스킨 우회 경로가 실제로 있었다.** 그래서 A안(출처 장부)을 적용했다(코디네이터 승인).

## 2. 실제 변경(내가 수정한 파일)

신규:
- `Assets/_Project/Scripts/Core/RunMode.cs` — 모드, 선택 상태, 끝없는 모드 수치(EndlessRules)
- `Assets/_Project/Scripts/Enemies/MonsterFamily.cs` — 명시 계열 표, DrinkAffinity
- `Docs/EndlessAffinity-2026-11-10.md`
- 테스트: `Tests/EditMode/EndlessModeTests.cs`, `Tests/EditMode/EndlessRewardLedgerTests.cs`, `Tests/PlayMode/EndlessModePlayTests.cs`

기존 파일 수정(모두 범위 안의 추가·분기만):
- 전투: `Enemy.cs`(AttackScale, `TakeDirectHit` 단일 피해 결과, DamageResult), `EnemySpawner.cs`(끝없는 모드 틱), `EnemyRoster.cs`(정규 표 읽기, 끝없는 모드 표·추첨·검증), `RangedAttackDriver.cs`, `SummonerDriver.cs`, `PlayerController.cs`(DrunkDrink, AffinityDrink), `PlayerProjectile.cs`, `RapierWeapon.cs`, `CoffeeBrewRuntime.cs`(직접 공격 경로로 교체)
- 런 흐름과 기록: `GameManager.cs`(모드, 보스 스케줄 비활성, 종료 분기, 스페셜티 가드, 기록), `RunHistory.cs`(mode, endlessWave, ModeSuffix)
- 화면: `DamageNumbers.cs`(`120 (추가 20)`), `CharacterSelectView.cs`(모드 줄, 순환 상성 줄), `RunPhaseBadge.cs`(끝없는 모드 두 줄 배지), `ResultView.cs`(모드, 웨이브, 스킨 재화 제외 이유), `RunHistoryView.cs`(제목에 모드 표기), `ActiveEffects.cs`(상성 줄)
- 저장: `DrinkStore.cs`(v4 장부, 정규화, 백업), `DrinkLab.cs`(출처별 정산, 일반 출처 우선 소모, 판매 거부, 출처 유지 환불), `DrinkLabView.cs`(제조 전 예고, 판매 불가 표시)
- 기존 테스트 1건 수정: `IntegerOnlyTests.cs:273`. schema v3 기대를 현재 버전(v4)으로 바꾸고 장부 백업 확인을 추가했다. 의도된 스키마 상승이다.

참고: `RunPhaseBadge.cs`, `ActiveEffects.cs`, `CoffeeBrewRuntime.cs`, `IntegerOnlyTests.cs`는 원래 git에 없는(미추적) 기존 사용자 작업 파일이다. 내 변경은 그 위에 더한 것뿐이다. 그 밖의 기존 dirty 파일, 아트, 세이브, 이전 빌드는 손대지 않았다.

## 3. 빌드·컴파일 결과(런타임 결과와 별도)

| 검사 | 결과 | 근거 |
|---|---|---|
| 스크립트 컴파일(EditMode·PlayMode 어셈블리 포함) | PASS | `Delivery/Reports/endless-affinity-2026-11-10/editmode-all-2.log` |
| 별도 검수 빌드(1차 프리뷰, 출시 아님) | PASS — `Build Finished, Result: Success` | `Builds/QA-EndlessAffinity-2026-11-10/CocktailCoffeeTea.exe`, 로그 `…/build.log`. 기존 빌드 폴더는 덮어쓰지 않았다. |

## 4. 자동 테스트(Unity 6000.3.25f1 배치, 격리 임시 저장)

| 검사 | 결과 | 근거 |
|---|---|---|
| 신규 EditMode 28건 | PASS 28/28(1차 실행의 1건 실패는 EditMode의 Destroy 로그 처리 문제였고 테스트를 고쳤다) | `editmode-new.xml`, `editmode-all-2.xml` |
| EditMode 전체 회귀 | PASS — 899건 중 897 통과, 실패 0. 기존 Inconclusive 1(`MissingTinArt_DrawsTinNotCircle`)과 Skipped 1(`Migration_UserSaveCopy_IsolatedRun`)은 실제 사용자 세이브 복사 테스트라서 원래 건너뛴다 | `editmode-all-2.xml` |
| 신규 PlayMode 3건(실제 프레임) | PASS 3/3 | `playmode-all.xml` |
| PlayMode 전체 회귀 | 362건 중 351 통과, 10 Skipped(증거 캡처 전용), **FAIL 1**: `SpawnScalingPlayTests.ActualRegularSpawns_IncreaseAtTenAndFifteenMinutes_WithSameUnlocks`. 원인은 30초 LevelUp STALL(카드 창이 열려 timeScale 0). **단독 재실행에서 SpawnScalingPlayTests 6/6 PASS** → 실행 순서·무작위 시드에 따라 갈리는 기존 불안정으로 판단한다. 정규 모드 스폰 경로는 이번에 바꾸지 않았다. | `playmode-all.log`(28461행), `playmode-spawnscaling.xml` |
| PlayMode 전체 실행 시점 | RunHistory 제목 표기와 그 테스트를 추가하기 **전**에 돌렸다. 이 변경은 EditMode 전체 재실행(위)으로만 확인했다. | — |

신규 테스트가 다루는 항목(자동 상태 강제 테스트이며 자연 플레이와 다르다):
- **모드:** 기본값은 정규 모드. 선택한 모드가 반영된다. 끝없는 모드는 보스 스케줄이 0이고 보스 예고가 없다. 20분+500초가 지나도 계속된다. 사망하면 1회 종료하고, 기록에 Endless와 웨이브 57이 남는다. 이후 포기를 눌러도 정산이 중복되지 않는다. 런이 끝나면 상성이 해제된다. 정규 모드는 기존대로 시간 초과로 끝나고 보스 4종이 남아 있다.
- **수치:** 웨이브 1은 8마리, 100%, 100%. 웨이브마다 단조 증가하고 웨이브 999에서 멈춘다. 1e7초와 float 최댓값에서도 NaN이나 오버플로가 없다. 다음 웨이브까지 남은 시간은 정수 초다.
- **스포너:** 구간 안에 분산 생성(15초에 4마리). 남은 적은 대기로 넘어간다. 신규 적만 다음 웨이브 수치(115%·108%)를 받고 기존 적은 그대로다. 15만 초를 한 번에 건너뛰어도 대기는 5000 이하이고 동시 상한을 지킨다. 실제 프레임에서 생성이 일어나고, 일시정지 중에는 시간과 생성이 멈춘다. 포기하면 종료된다.
- **공격 배율:** 투사체 10이 180%에서 18, 접촉 5가 9가 된다. 체력도 배율대로다. 공유 EnemyDef는 바뀌지 않는다. 배율 1이면 원래 값 그대로다.
- **계열 표:** 모든 정적 EnemyDef가 명시 분류되어 있다. 가중치 합은 100이고 두 직업 모두 세 계열에 후보가 있다. 시드가 같으면 같은 순서가 나온다. 2만 회 추첨 비율은 70/15/15(±2%)다. 집중 계열은 커피 → 차 → 칵테일 순서로 바뀌고 1웨이브 전에 예고한다. 정규 표는 기존 그대로이고, 찻집 정규 런 1만 회 추첨은 모두 차 계열이다.
- **상성:** 3×3 조합, 중립, 미음용을 모두 확인했다. 반올림 예: 7 → 1, 8 → 2, 13 → 3. 최소 1. 0 피해에는 없고 int 최댓값에서도 넘치지 않는다. 비율 0이면 끈다. 단일 결과 예: B=10, A=2, T=12. 지속 피해와 중립에는 상성이 없다. 무적이면 결과가 0이다. 처치는 1회만 세고, 죽은 적은 다시 처치되지 않는다. 음료를 바꾸거나 해제하면 즉시 반영된다. 피해 숫자는 `120 (추가 20)` 형식이다.
- **보상과 장부:** 끝없는 모드에서 정예·일반 보급 보상이 endless로 기록되고 정산 뒤 장부에 들어간다. 런 종료와 판매 시도까지 **스킨 재화 증가 0**(엔드투엔드). 무한 재료 음료는 제조와 음용이 되지만 판매는 거부된다. 거부 뒤에도 음료와 주문이 그대로이고, 재로드 뒤에도 유지된다. 일반 출처를 먼저 소모해 일반 재료 음료는 예전처럼 판매된다. 출처가 섞인 음료는 판매 불가이고, 제조를 취소하면 출처대로 환불된다. 저장에 실패하면 재고와 장부가 함께 롤백된다. 비정상 종료 복구도 출처를 유지하고 중복 정산은 없다. v3 파일을 읽으면 값이 그대로이고, 기존 준비 음료는 판매할 수 있으며, 원본 백업이 생기고 v4로 저장된다. v5(미래 버전) 파일은 쓰지 않는다. 손상된 장부는 재고 이하로 맞춘다. 끝없는 모드에서는 스페셜티를 주지 않고, 정규 모드는 기존대로 준다. 이전 기록은 정규 모드로 읽는다.

## 5. 런타임 화면 검증(새 빌드)

| 검사 | 결과 | 사유 |
|---|---|---|
| 새 빌드 실행 후 모드 선택, 끝없는 모드 HUD 배지, 웨이브 배너, 결과 화면, `120 (추가 20)` 숫자, 음료 판매 불가 표시 스크린샷 | **BLOCKED** | 지시에 따라 GUI 검증은 코디네이터에게 따로 요청한다. 앱 지정 입력 도구와 PID/HWND/foreground 일치를 확인하지 못한 상태라 입력하지 않았다. 새 빌드 실행도 하지 않았다. |
| 배치 시각 확인(1920×1080 기준 좌표 계산) | 참고만(PASS 아님) | 모드 버튼(y 220–272)은 무기 카드 하단(y 280)과 영업 시작 버튼 상단(172) 사이에 있다. 안내 줄은 177–207. 끝없는 모드 배지는 두 줄(760×65)이며 보스 체력바가 없는 모드에서만 쓴다. 결과 문구는 best-fit 20–32. 실제 화면 잘림 여부는 확인하지 못했다. |

## 6. 미완·후속

1. **런타임 GUI 검증 BLOCKED**(위). UI 변경 항목은 화면 증거가 없으므로 PASS가 아니다.
2. **정규 찻집 런에서 차 음료(→칵테일)와 칵테일 음료(→커피) 상성이 일어나지 않는다.** 기존 정규 로스터(칵테일 몹 없음, 커피는 차 변형)를 그대로 두었기 때문이다. 정규 바텐더 런의 커피 계열은 2:30 이후 2%와 중간 보스뿐이다. 정규 표를 바꿀지는 사용자·코디네이터가 결정할 일이다.
3. 끝없는 모드 계열 전환 때의 **음료 교체는 구현하지 않았다.** 준비 음료 슬롯이 하나뿐이라 교체할 보유 음료가 없다. 소비 정책을 새로 정해야 한다.
4. 몬스터 상세 화면이 따로 없다. 계열 표식은 첫 안내 배너의 `[커]` 같은 글자 표식에만 있다.
5. 상성 비율 20%와 끝없는 모드 증가율은 조정 가능한 기본값이며 밸런스 검증 전이다.
6. `SpawnScalingPlayTests` 전체 실행 순서 불안정(위)은 이번 범위 밖이라 원인 수정을 하지 않았다.
7. 신규 .cs 파일의 `.meta`는 Unity가 배치 실행 중 만들었다. 커밋할 때 함께 포함해야 한다.
