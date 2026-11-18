# QA — 정수 전용 표기·거래값 (2026-11-06)

사용자 지시: "소수점 싹다 없애. 정수로만 쓸거야". 상태: **구현 완료 · EditMode PASS · PlayMode/빌드/화면 증거 미완 (사용량 한도·메모리로 중단)**.

## 1. 반올림 규칙 (`Assets/_Project/Scripts/Core/IntNum.cs`)
| 대상 | 규칙 |
|---|---|
| 일반 수치·%·확률 | 중간값은 0에서 먼 쪽(MidpointRounding.AwayFromZero), Mathf.RoundToInt(짝수) 사용 안 함 |
| 남은 시간(샷·뜸·혼합 러시·환불 무효·히든 재충전) | 올림(Ceil, 1e-4 오차 무시) |
| 배율 | 정수 % (×1.15 → 115%, ×1.2 → 120%) |
| 1초 미만/소수 고정 지속시간 | ms 정수 (0.5초 → 500ms, 1.5초 → 1500ms) |
| 고정 거리·반경 (월드 1 = 1m) | cm 정수 (2.5 → 250cm, 줍기 +0.5 → +50cm) |
| 초당 회복 | 분당 회복 정수 (1.2/초 → 분당 72) |
| 전투 피해 | TakeDamage 경계에서 정수(양수면 최소 1), 적 최대 HP 정수(원값 보관 후 1회 반올림) |
| 현재 HP 표시 | 기존 Ceil 유지(살아 있는데 0 표시 방지) |
| 경과 시간 시계(mm:ss) | 기존 Floor 유지(시계 관례) |
| 음료 버프 | 화면 정수 % = 실제 적용값(ApplyTo에서 양자화, 0%는 숨김·미적용) |
| 음료 양 | 내부 0.1 단위 저장 유지(세이브·공유 코드 호환), 실제 값은 항상 1mL/1g 배수: 입력 정수만, 병→지거 1 단위씩, SetAmount/재고 증감/주문 최소량 정수 |

예외(수정 안 함): 디버그/QA 로그 포맷, 버전 표기(v1.1), 스토어 제공 실제 통화 가격, 좌표·deltaTime·보간·오디오.

## 2. 세이브 마이그레이션 (drinks.json v2 → v3)
- `DrinkStore.MigrateToWholeUnits`: 원본 내용만으로 계산하는 순수·멱등 함수(지급 아님 → 반복 이득 없음). 정규화 전에 실행(옛 0.5g 준비 음료 보존).
- 원본을 `{path}.pre-integer-v{n}.bak`로 1회 복사(있으면 덮어쓰지 않음), DrinkLab이 즉시 v3 저장 + 1회 안내. 미래 버전 파일은 건드리지 않음.
- 실제 사용자 세이브는 수정하지 않았다. 복사본(scratchpad) 격리 실행 결과: `Delivery/Verification/2026-11-06/integer-only/migration-user-copy.txt` — 재고 변화 없음, 주문 레몬즙 7.5mL → 8mL, 코인 보존.
- 사용자 세이브 해시(작업 전): `user-save-hashes-before.txt`. 작업 후 재확인: NOT RUN.

## 3. 검증
| 항목 | 설정/절차 | 기대 | 실제 | 상태 | 증거 |
|---|---|---|---|---|---|
| 컴파일 | Unity 6000.3.25f1 배치 | 오류 0 | 1차 DrinkLabView 문자열 개행 오류 → 수정 후 0 | PASS | `EditMode-attempt1-compile-fail.log`, `EditMode.log` |
| EditMode 전체 | `-runTests -testPlatform EditMode -nographics`, 격리 저장 | 실패 0 | 870 통과 · 0 실패 · 1 Inconclusive(기존 `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`, 무관) | PASS | `EditMode.xml` |
| IntegerOnlyTests 68건 | 반올림·Ceil·피해·입력·재고·주문·제조/판매·공유코드·마이그레이션(1회·백업·멱등·미래파일)·버프 표시=적용·적/플레이어 피해·문구 감사 | 전부 통과 | 68/68 | PASS | `EditMode.xml` |
| 2차 EditMode 중단 | 같은 명령 | — | `DrinkGameIntegrationTests` 중 Editor "Shut down."(원인 미상, 재실행 시 재현 안 됨) | NOTE | `EditMode-attempt2-shutdown.log` |
| PlayMode 전체 | `-testPlatform PlayMode -nographics` | 실패 0 | 시스템 메모리 부족으로 Claude Code가 중단(131 통과·9 실패 시점). 9건 원인(소수 기대값·0.1 단위 계량) 분석 후 코드/테스트 수정: 병→지거 1 단위 양자화, 적 HP 1회 반올림, 테스트 기대값 정수화. **재실행 안 함** | BLOCKED | `PlayMode.log`(부분) |
| IntegerOnlyPlayTests(화면 글자 소수 0, 소수 입력 거부, RenderTexture 캡처) | `CCT_EVIDENCE_DIR` 설정 PlayMode | PNG·텍스트 | 작성만, 실행 안 함 | NOT RUN | — |
| 검수용 Windows 빌드 | `BuildRelease.Windows64ReleaseToBatch -cct-preview-out Builds/IntegerOnly-2026-11-06` | exe | 실행 안 함 | NOT RUN | — |
| 새 빌드 화면 확인 | 빌드 실행·캡처 | 스크린샷 | 빌드 없음 | NOT RUN | — |

## 4. 변경 파일 (기존 dirty 작업 위에 범위 한정 수정, 커밋 없음)
신규: `Scripts/Core/IntNum.cs`, `Tests/EditMode/IntegerOnlyTests.cs`, `Tests/PlayMode/IntegerOnlyPlayTests.cs` (.meta는 Unity 생성).
런타임: DrinkData(DrinkRules), DrinkRecipe, DrinkSales, DrinkShareCode, DrinkStore, DrinkLab, DrinkCalculator, DrinkLabView, DrinkStageView, DrinkResearch, DrinkRunHud(주석), CoffeeBrewRuntime, HiddenSkillRuntime, GameManager, HudView, CardAdvisor, CardSelectView, DamageNumbers, BossDirectionIndicator, SettingsService, Enemy, PlayerController, HiddenSkills, JobPreset, DefaultTeaCards, PermanentSkillData, SkillData.
에셋 설명: Card_chamomile, Card_ginseng_root, PermanentSkill_restorative_break/tidy_counter/master_blend, Skill_mellow_finish/wide_tray/zero_waste.
테스트 기대값 갱신: DrinkLabTests, DrinkSalesTests, DrinkIngredientExpansionTests, DrinkGameIntegrationTests, CoffeeRecipeTests, FirstPassArtTests, CoffeeLabPlayTests, DrinkLabPlayTests, ChallengeAttackBehaviorTests, CoffeeBrewCombatPlayTests.

## 5. 남은 일
1. 메모리 확인 후 PlayMode 전체 재실행(수정한 9건 확인), `IntegerOnlyPlayTests`를 `CCT_EVIDENCE_DIR`로 실행해 1080p/720p 캡처.
2. 검수용 Windows 빌드(새 폴더) → 격리 `-cct-data-dir`로 실행·캡처, 사용자 세이브 해시 재확인.

## Addendum — integrated runtime run, 2026-11-09 (after the user lifted the hold)

The NOT RUN rows above were executed later on the final source. Full results, triage, frames and the QA build are in **`qa-round-visual-variety-2026-11-09.md` §6**.

| Run | Result |
|---|---|
| EditMode full | 869 / 871 passed, 0 failed |
| PlayMode full on final source | 347 / 359 passed, 2 failed; both classes then 38 / 38 on rerun. The 2 were a DrinkLab integer-threshold test assumption (fixed) and an existing BattleArena level-up timing flake. |
| This task's suite | All tests pass (after the fixes listed in §6 triage) |
| Offscreen frames | Rendered and inspected |
| QA build | `Builds/QA-RoundVariety-2026-11-09/CocktailCoffeeTea.exe`: Succeeded, 0 errors |
| Headless smoke | 25 s boot, PASS |

Still NOT RUN:
- windowed real-input play;
- audible sound;
- narrow-window (836×705) and touch renders.

User save files are byte-identical. Unity engine PlayerPrefs written by the player smoke were restored to the pre-run hash.
