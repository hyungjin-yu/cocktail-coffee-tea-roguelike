# 끝없는 재료 모으기 WIP 체크포인트 파일 근거 — 2026-11-10

목록: [endless-affinity-checkpoint-files-2026-11-10.json](endless-affinity-checkpoint-files-2026-11-10.json) — 66개 경로, 저장소 루트 기준 상대경로 JSON 문자열 배열. **WIP 체크포인트이며 전체 완료가 아니다.** Git 변경은 하지 않았다.

## 선정 방법(검증한 사실)

1. 출발 집합은 본인 작업 28개다(코드 26 + 문서·보고서 2). 내가 만든 새 파일과 미추적 파일에는 `.meta`도 넣었다.
2. **컴파일로 닫힘을 확인했다.** 프로젝트 밖 임시 폴더(scratchpad)에 `git show HEAD:` 기준 트리를 만들었다. 목록의 파일만 작업 트리 내용으로 덮어쓰고, 목록에 없는 미추적 `.cs`는 뺐다. 그 상태에서 Unity 6000.3.25f1에 들어 있는 Roslyn(`csc`)으로 어셈블리 4개를 차례로 컴파일했다.
   - 컴파일 오류가 날 때마다 그 오류를 해결하는 파일 하나만 추가했다. 10번 반복한 뒤 **CCT.Runtime(163), CCT.Editor(21), CCT.Tests.EditMode(70), CCT.Tests.PlayMode(66) 모두 오류 0**이었다.
   - 경고는 뺐다. ArtV1 테스트 어셈블리는 바뀐 파일이 없어 이 확인에서 제외했다.
   - 프로젝트 파일 수정, Unity 실행, 빌드는 하지 않았다.
3. 한계: 이 부분집합으로는 **컴파일만 확인했고 테스트를 다시 돌리지 않았다.** 이전에 기록한 EditMode 897/0과 PlayMode 결과는 전체 작업 트리에서 낸 것이다. 목록에 없는 tracked 수정 파일은 HEAD 내용으로 남는다. 그래서 의미상 결합(동작 차이)이 있으면 컴파일로는 잡히지 않는다.

## 기존 tracked 수정 파일 중 포함한 것(컴파일에 꼭 필요한 이유)

| 파일 | 이유(컴파일 오류 근거) |
|---|---|
| `Scripts/Drinks/DrinkRunHud.cs` | `DrinkNoticeKind` 타입. CoffeeBrewRuntime.cs가 사용 |
| `Scripts/UI/ResponsiveLayout.cs` | `CenterBand.MaxWidth`. DrinkLabView.cs가 사용 |
| `Scripts/Enemies/BossDirector.cs` | `SpawnedCount`, `BossesSpawned`, `ScheduleCount`, `NextSpec`. RunAmbience.cs와 RunPhaseBadge.cs가 사용 |
| `Scripts/Drinks/DrinkData.cs` | `DrinkRules.Whole`, `WholeFloor`(정수 전용 단위). DrinkStore.cs, DrinkLab.cs, DrinkLabView.cs가 사용 |
| `Scripts/Player/PlayerStats.cs` | `coffeeAttackSpeed`, `coffeeMoveSpeed`, `coffeeRangePercent`. CoffeeBrewRuntime.cs가 사용 |
| `Scripts/Drinks/DrinkCalculator.cs` | `DrinkBuff.Shown`. ActiveEffects.cs와 DrinkLabView.cs가 사용 |
| `Scripts/Player/PlayerProjectileWeapon.cs` | `CurrentReach`. PlayerEffectMarkers.cs가 사용 |
| `Scripts/Player/HiddenSkillRuntime.cs` | `HiddenSkillRuntime.DirectKind`(타격감 HitKind). PlayerProjectile.cs와 RapierWeapon.cs가 사용 |
| `Scripts/Drinks/DrinkSales.cs` | `DrinkSales.BlockReason`. DrinkLabView.cs가 사용 |
| `Scripts/UI/HudView.cs` | `HudView.PhaseBadge`. EndlessModePlayTests.cs와 RunPhaseBadge 연결이 사용 |

포함한 미추적 의존 파일(정수화, 타격감, HUD, 음료 연출 계열): `Core/IntNum.cs`, `Core/RunAmbience.cs`, `Enemies/HitFeedback.cs`, `Player/PlayerEffectMarkers.cs`, `UI/ActiveEffectsStrip.cs`, `Drinks/DrinkUseFx.cs`, `Drinks/DrinkVesselGeometry.cs`, `Drinks/DrinkPourMotion.cs`, `Drinks/DrinkRecipeFlow.cs`(각각 `.meta` 포함). `RunPhaseBadge.cs`, `ActiveEffects.cs`, `CoffeeBrewRuntime.cs`, `IntegerOnlyTests.cs`도 원래 미추적 사용자 작업 파일이지만 내가 수정했으므로 본인 변경으로 셌다.

## 제외(이유)

- 미추적 `.cs` 중 컴파일에 필요 없는 10개: `Drinks/DrinkFirstSaleCapture.cs`, `Tests/EditMode/DrinkLabExtractionTests.cs`, `IngredientPackagingIconTests.cs`, `Tests/PlayMode/CoffeeBrewCombatPlayTests.cs`, `CombatDrinkFeedbackEvidence.cs`, `CombatDrinkFeedbackPlayTests.cs`, `DrinkRunHudVesselPlayTests.cs`, `IntegerOnlyPlayTests.cs`, `PersistentBuffRoundFeedbackPlayTests.cs`, `RoundVisualVarietyPlayTests.cs`. 이전 정수화·타격감 작업의 테스트와 캡처 파일이다. 따로 체크포인트할지는 코디네이터가 판단한다.
- 그 밖의 tracked 수정 `.cs` 31개(예: DrinkArt, DrinkStageView, CardAdvisor, HiddenSkills, AudioService, 카드·스킬 데이터, 기존 테스트 다수)는 HEAD로 남겨도 컴파일된다.
- `ProjectSettings/*.asset` 수정 6개.
- Resources 에셋(.asset 카드·스킬 수치, DrinkLab 병 PNG 신규 다수), `ArtSource/`, `.claude/`, `CLAUDE.md`, 다른 날짜 보고서, `Builds/`, `새 폴더/`(실행본), `Delivery/Reports/endless-affinity-2026-11-10/`(테스트 로그·XML), 세이브.

## 위험(코디네이터 판단 필요)

- **포함한 `DrinkData.cs`는 신규 재료 정의를 담고 있다.** 이 정의는 아직 미추적인 `Resources/DrinkLab/dl_bottle_*.png`(지시대로 아트라서 제외)를 경로로 참조한다(DrinkData.cs 16곳, DrinkArt.cs 6곳 일치). 컴파일에는 영향이 없다. 하지만 체크포인트만 받아 실행하면 해당 병 이미지가 비어 대체 표시될 수 있다. DrinkArt.cs는 HEAD 그대로 남는다.
- 같은 맥락에서 카드·스킬 `.asset` 수치 변경은 제외했으므로, 이 체크포인트의 런타임 밸런스는 테스트한 작업 트리와 다를 수 있다.
- 이 부분집합으로 EditMode/PlayMode를 다시 돌리지 않았다(편집·빌드 보류 지시).
