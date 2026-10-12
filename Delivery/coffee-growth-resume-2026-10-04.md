# 커피 성장 모델 재개 — 2026-10-04

Orca task `task_8d305c3729ba` / dispatch `ctx_2d50e0539a4d`. 대상 프로젝트 `C:/Users/user/Documents/Projects/CocktailCoffeeTea`, Unity 6000.3.25f1(ProjectVersion 그대로, 재설치·다운그레이드 없음).
입력: 기획 05, 07 §2/§5, 08 §2/§3(HUD·커피 트리·일시정지), 10 G16~G18, 11, 14, 최신 13/99. AGENTS.md는 프로젝트에 존재하지 않음(확인함).

## 범위와 보존

- 시작 시 `git status`/`git diff --stat` 확인: 55개 수정 파일 + 미추적(ArtSource, Audio, Fonts, 신규 Skill/Card asset) 상태. 09-28 Claude UI 작업과 10-04 ResultView 영구 강화 버튼(`ResultView.cs`)은 건드리지 않았다.
- 수정 전 사본: 세션 scratchpad `backup-2026-10-04/`(GameManager, Balance, IngredientGauge, SkillTreeView, PauseView, HudView, CardSelectView, BossFlowTests, EnhancedBossFlowTests, PauseTests, KeyboardNavigationTests).
- ArtSource/데스크톱 아트/기획 13·99 미수정. 커밋·푸시 없음. 실제 사용자 세이브 미사용(테스트는 기존 `TestSaveIsolation`). 아트 게이트(BuildRelease) 미변경. 튜닝·신규 기능 없음.

## 변경 파일

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Drops/CoffeeBeanGauge.cs` (신규) | 07 §2 원두 게이지. 임계치 10 고정, Level=1+누적 포인트, 잔량 0..9, 포인트 캡핑 없음. 누적 원량 R → E=floor(R×(1+보너스)), 이번 지급=새E−이전E. 보너스를 1/10000 정수로 계산해 float 경계 오차 제거 |
| `Assets/_Project/Scripts/Core/RunRewards.cs` (신규) | 07 §2 보상 대기열(순수 C#): 하이브리드 → 카드 FIFO(같은 틱은 칵테일→차) → 새 포인트 후 커피 트리 1회. 미사용 포인트 보존, 자동 열림 1회 예약(`TreeAutoOpenArmed`), 15노드 전부 해금 시 포인트 보존·자동창 없음, `TryUnlockNode`(1포인트/선행/중복 방지 + 즉시 게이지 재평가) |
| `Assets/_Project/Scripts/Drops/IngredientGauge.cs` | `Reevaluate()` 추가 — 재료 추가 없이 현재 잔량으로 레벨업 루프(07:31) |
| `Assets/_Project/Scripts/Cards/SkillTree.cs` | `CoffeeNodeState`(해금/가능/선행필요/포인트부족)와 `StateOf`, `NodeCost=1` |
| `Assets/_Project/Scripts/Core/Balance.cs` | `BeanGaugeStart/Growth`(8→11→15 성장) 삭제, `BeanPointThreshold=10` |
| `Assets/_Project/Scripts/Core/GameManager.cs` | `pendingSkillPoints` 1캡·전부 해금 시 포인트 폐기 제거. `RunRewards` 사용. 선택 완료 시 `FinishRewardChoice`가 같은 호출 안에서 다음 보상을 열어 timeScale 0 유지(Playing 프레임 끼워넣기 제거, 07:46). 카드 선택 후 게이지 재평가. 원두는 `AddRaw`(픽업별 RoundToInt 제거), 도감 원두 집계는 E 증가분. K 입력 → `RequestCoffeeTree()`(Playing·비Pause·비카운트다운일 때만 받아 두고 프레임 끝에서 처리, 0포인트도 조회). **2차(아래 §프레임 순서 수정):** 자동 보상 열기를 Update에서 LateUpdate(`ResolveEndOfFrame`)로 옮김 |
| `Assets/_Project/Scripts/UI/InputReleaseGate.cs` (신규) | 08:39 입력 잠금. 화면을 연 프레임 + Enter/Space/숫자키/마우스 왼쪽이 눌린 동안 사용자 입력 경로(버튼 클릭·EventSystem Submit·단축키)를 막고 눌림 해제가 관찰되면 푼다. 판정은 순수 함수(EditMode 테스트) |
| `Assets/_Project/Scripts/UI/SkillTreeView.cs` | 다중 배분: 상단 "남은 커피 포인트 N · 원두 10개마다 1포인트, 런 종료 시 초기화", 노드 상태 4종+비용1, 클릭/1~6키로 1포인트씩, 포인트 소진·찍을 노드 없음이면 닫힘, "계속하기"는 항상 표시(남은 포인트 보존). 노드 버튼·계속하기·1~6키 모두 `InputReleaseGate` 경유(공개 `Choose`/`Continue`는 테스트용 직접 API로 게이트 없음). 14 "카페 메뉴판" 색(크림/크라프트/원목/차초록/커피 글자) 적용, 제목 48px(높이 50 안), 본문·상태·단축키 24px, 08:47 안전 여백48 안에 겹침 없이 재배치. Pause용 `ShowReadOnly(tree, points, closed)`도 같은 헤더/상태 표시. |
| `Assets/_Project/Scripts/UI/PauseView.cs` | 커피 트리 조회에 잔여 포인트 전달. `IsSkillTreeOpen`을 조회 전용 창만으로 한정 — 이전엔 보상 트리가 열린 상태에서 Esc가 아무 동작도 안 하던 문제(조회 전용 닫기 분기에 걸림)도 해소, 이제 다른 보상창처럼 Pause가 위에 열린다 |
| `Assets/_Project/Scripts/UI/HudView.cs` | 상단 우측 "커피 포인트 N  [K] 커피 트리"(08:79), 원두 바에 Lv 표시 |
| `Assets/_Project/Scripts/UI/CardSelectView.cs`, `HybridUnlockView.cs` | 연쇄 보상(07:46)에서 직전 화면을 닫은 키/클릭/Submit이 다음 화면을 바로 누르지 않도록 `InputReleaseGate` 적용(카드 버튼·1/2/3, 해금 버튼). 실제 입력 모듈은 `GameBootstrap`의 `InputSystemUIInputModule`(AssignDefaultActions) — Submit은 누름 시점 1회라 원래도 같은 프레임 재확정 가능성은 낮지만 게이트로 명시 차단 |
| `Assets/_Project/Tests/EditMode/CoffeeEconomyTests.cs`, `CoffeeTreeUiTests.cs` (신규) | 아래 "테스트" 참고 |
| `Assets/_Project/Tests/PlayMode/BossFlowTests.cs` | 옛 규칙("여러 단계 넘어도 1회·1포인트") 테스트를 새 규칙으로 교체 + 신규 3개 |
| `Assets/_Project/Tests/PlayMode/EnhancedBossFlowTests.cs` | 원두 +20% 기대값을 픽업별 RoundToInt(5)에서 누적 floor(4, 다음 1개로 6)로 변경 — 규칙 변경(07:34)에 따른 수정 |
| `Assets/_Project/Tests/PlayMode/PauseTests.cs`, `KeyboardNavigationTests.cs` | `ShowReadOnly` 시그니처 변경에 맞춰 인자만 추가(단언 변경 없음) |

새 `.cs` 파일(CoffeeBeanGauge, RunRewards, InputReleaseGate, CoffeeEconomyTests, CoffeeTreeUiTests)의 `.meta`는 이번 배치 Unity 실행이 생성했다. PauseTests/KeyboardNavigationTests 백업도 scratchpad에 있다.

## 테스트

**최종 EditMode (Unity 6000.3.25f1 배치, 실제 프로젝트 경로, 잠금 없음 확인 후 실행): 247/247 통과 — 실패 0, 건너뜀 0, inconclusive 0.**
- XML `Delivery/logs/editmode-results-2026-10-04-coffee-r3.xml`, 로그 `Delivery/logs/editmode-log-2026-10-04-coffee-r3.txt` (종료 2026-10-04 00:26:42Z).
- 실행 이력(모두 보존): r1 `…-coffee.xml` 240/240(게이트·레이아웃 테스트 추가 전) → r2 `…-coffee-r2.xml` 245/247, **레이아웃 테스트 2건 실패** — `UiFactory.Place`가 pivot=anchor라 위/아래 고정 요소의 오프셋이 중심이 아닌 가장자리였음(제목/잔여포인트, 노드 단축키/이름 겹침). 좌표를 고친 뒤 r3 247/247. 단언은 약화하지 않았다.
- 이전 기준 219(10-04 ResultView 작업 시점) → 247 = 신규 28: `CoffeeEconomyTests` 21, `CoffeeTreeUiTests` 7. 기존 테스트의 단언 변경 없음(EditMode).

검증 내용(EditMode, 실제 실행):
- 임계치 10 고정·성장 없음(12회 연속), 9+1 경계, 원두40 한 번에 4포인트(캡핑 없음, 이벤트 1회), 0/음수 무시.
- 20+20+40 → 8포인트·잔량0·E=80. +20% → E=96, 9포인트·잔량6·Lv10. 실제 `bean_cupper` 영구 노드 데이터를 스탯에 적용해 계산한 경우도 9/6.
- 분할 불변성: 80 한 번 = 1×80 = 4×20 = 실제 보스 픽업 단위(5×4,5×4,8×5). 원량 1..40 각각 무작위 분할 20회씩 E=floor(R×1.2), 포인트/잔량 일치. 원두1씩 E=1,2,3,4,6(픽업별 반올림 차익 없음).
- 포인트 보존: 8포인트 전부 적립, 1포인트/선행/중복 해금 방지, 15노드 전부 해금 후 생긴 포인트 보존·자동창 없음(반복 호출에도).
- 노드 상태 4종, 새 포인트 후 자동 1회·"계속하기" 뒤 10프레임 재열림 없음·새 포인트 시 다시 1회·K 열기 시 예약 소모.
- 순서(**`RunRewards` 대기열 단위 검증**): 같은 틱은 칵테일→칵테일→차(차가 먼저 들어와도)→커피 트리, 이전 틱 보상은 앞 유지, 하이브리드 → 카드 → 트리. 1차 시점의 GameManager는 Update에서 대기열을 비워 픽업 Update보다 먼저 돌 수 있었으므로 **이 대기열 테스트만으로 프레임 단위 순서가 보장되지는 않았다** — 2차에서 수정·검증(아래).
- 재평가: 7/8 상태에서 리큐르(−8%) 적용 → 재료 추가 없이 칵테일→차 순 예약, 잔량 차감. 콜드브루 노드 해금 직후 칵테일 보상 예약, 트리는 새 포인트 없이 재열림 없음. 감소 없으면 재평가 무변화.
- 입력 잠금 `InputReleaseGate`: 연 프레임 차단, 눌린 동안 차단, 떼면 해제 후 새 입력 허용, 재무장.
- 커피 트리 배치(RectTransform 계산, 1920×1080): 제목·잔여포인트·헤더3·노드15·계속하기 상호 겹침 없음, 08:47 안전 여백48 안. 노드 내부 글자 겹침 없음, 모든 글자 ≥24px. 배분 후 잔여 포인트 문구/선행 풀린 노드 "해금 가능"/"해금됨" 갱신.

## 미검증 / 남은 항목

- **PlayMode 미실행.** 이 기기의 배치 PlayMode 러너 행(2026-09-25 기록) 때문에 반복 시도·환경 재설치를 하지 않았다. 아래 PlayMode 테스트는 **작성·컴파일만** 되었고 통과로 보고하지 않는다:
  `BossFlowTests.BossBeans20_GiveTwoPoints_AllocatedInOneOpening`, `Continue_KeepsPoints_NoAutoReopen_UntilNewPointOrK`, `K_OpensWithZeroPoints_ButNotDuringPauseOrCountdown`, `CardThenTree_ChainWithoutPlayingFrame`, 수정된 `EnhancedBossFlowTests.ChargerBoss_EventuallyDashes`, `PauseTests`/`KeyboardNavigationTests`의 조회 전용 트리 2건, 기존 `BeanGauge_Full_OpensSkillTree_AndUnlockAppliesEffect`/`LevelUpAndSkillPoint_Together_AreHandledOneAfterAnother`.
- **육안/장치 미검증:** 실제 키보드·마우스 장치 경로(K, 1~6, Enter/Space Submit, 클릭 직후 연쇄 화면)는 실행해 보지 않았다 — `InputReleaseGate` 판정 로직만 EditMode로 검증. 커피 트리 배치는 RectTransform 계산으로만 겹침/여백/글자 크기를 확인했고 실제 렌더 캡처·720p 표시·한글 줄바꿈 결과·색 대비는 육안 미확인. HUD 커피 포인트 문구 위치도 미캡처.
- 14 색은 이번에 재설계한 커피 트리 화면에만 적용했다. 다른 화면은 여전히 기존 어두운 placeholder 색이라 화면 간 톤이 다를 수 있다. 포커스 청록 테두리(08:49)는 기존 공통 hover 색 그대로.
- 보상 연쇄·종료 우선·K 처리는 2차에서 실제 GameManager로 EditMode 통합 검증했지만(아래), 엔진이 실제로 Update/LateUpdate를 호출하는 순서는 `DefaultExecutionOrder` 속성 검사와 Unity 문서상 보장(모든 Update 뒤 LateUpdate)에 근거한다 — 실제 프레임 루프는 PlayMode(미실행) 몫.
- 커피 일반 몹(`EspressoBug` 원두1·`MilkFoamer` 원두2) 드랍: 코디네이터가 00/07 기준으로 정리 — 07의 "원두80이면 8포인트"는 원량 80일 때의 계산 예시이고 일반 몹의 추가 원두는 의도된 것. 변경 없음(1차 보고의 "불일치" 표현 철회).
- **아트 잔여(코디네이터 전달, 이번 범위 밖·미수정):** `Assets/_Project/Scripts/Editor/ArtV1Spec.cs`의 `BaseBodyFrameTotal=321`/`FullBodyFrameTotal=329`가 기획 355/363과 다르다. bartender_v02는 needs_fix로 임포트하지 않았고 ready 아트 없음. 아트 게이트 미변경.
- SaveData v2는 이번 작업 범위 밖(코디네이터 확인) — 미수정.
- 후보 0장 카드 보상의 "정비 — 최대 체력30% 회복" 버튼(07 §3)은 이번 범위가 아니라 기존 동작(건너뜀) 그대로.
- Esc는 커피 트리(보상/K)에서 기존 선택창과 동일하게 Pause를 연다. "계속하기"는 마우스/Enter로 닫는다.

## 2차 dispatch — 프레임 순서 수정 (task_0ccaa76a6a27 / ctx_80c7860086b9)

검수 지적: 1차 GameManager는 `Update`에서 `OpenNextReward()`를 불러, GameManager Update가 일부 `IngredientPickup.Update`보다 먼저 돌면(Unity 기본 순서 미정) "차 픽업 → GM이 대기열 비움(차 화면) → 같은 프레임 칵테일 픽업"이 되어 07:43 같은 틱 칵테일 우선이 깨질 수 있었다. 1차의 247/247은 이 수정 **전** 증거다(해당 XML 그대로 보존).

수정(`Assets/_Project/Scripts/Core/GameManager.cs`, 요구사항 변경 없음):
- Playing 중 자동 보상 열기를 `Update` → `LateUpdate`의 `ResolveEndOfFrame()`으로 이동. 순서: ① 종료 판정(사망 → 클리어 → 시간 초과, 06:148) ② 종료면 대기 보상·K 요청 폐기(07:47) ③ Playing·비Pause·비카운트다운이면 이 프레임까지 쌓인 보상 열기(모든 픽업 Update 뒤 → `RunRewards` 스테이징이 칵테일→차로 정리) ④ 연 보상이 없을 때만 K 요청으로 트리.
- K는 Update에서 `RequestCoffeeTree()`로 요청만 기록하고 ④에서 처리 — 같은 프레임 카드 보상·종료가 우선이며 자동 트리 예약을 소모하지 않는다. 보상창 중/Pause/카운트다운 중 K는 받지 않는다.
- `[DefaultExecutionOrder(1000)]`: LateUpdate끼리의 순서는 미정이라, `PlayerController.LateUpdate`의 장판 피해 사망이 같은 프레임 보상보다 먼저 판정되도록 GameManager를 뒤로 보냄.
- 선택 완료 시 같은 호출에서 다음 보상을 여는 동작(`FinishRewardChoice`, timeScale 0 유지)은 그대로.
- 테스트용 공개 API: `ResolveEndOfFrame()`(LateUpdate가 부르는 바로 그 메서드), `MarkPlayerDiedForTests()`, `LastOpenedCardCategory`. `OpenCoffeeTree()`는 즉시 여는 테스트 진입점으로 유지.
- `SkillTreeView` 제목 44→48px(08 주제목48, 높이 50 안). 그 외 외형 확장 없음.

신규 `Assets/_Project/Tests/EditMode/GameManagerFrameOrderTests.cs`(8개) — 실제 `GameManager.Initialize` 전체 구성(카메라·HUD·뷰 포함, 저장은 `TestSaveIsolation` 임시 경로, AudioService 미생성 상태)에서 한 프레임을 흉내 냄:
- 같은 프레임 차 픽업 → 칵테일 픽업 → 프레임 끝: 칵테일 카드 먼저, 프레임 도중엔 Playing 유지; 선택 직후 같은 호출에서 차 카드, timeScale 0 유지; 마지막 선택 후 Playing·timeScale 1.
- 같은 프레임 보상+사망 → GameOver "사망", 카드 화면 없음(다음 프레임에도). 보상+시간 초과 → "시간 초과", 카드 없음.
- 같은 프레임 K + 새 커피 포인트 + 칵테일 보상 → 카드 먼저, 자동 트리 예약 유지 → 카드 선택 직후 트리(포인트1) → 계속하기 후 재열림 없음.
- K 단독 → 프레임 끝에서 0포인트 조회 트리. K+사망 → 트리 안 열림. 보상창 중 K 무시.
- GameManager의 `DefaultExecutionOrder`가 `IngredientPickup`/`PlayerController`(기본 0)보다 뒤, 프레임 끝 처리는 `LateUpdate`.

**최종 EditMode(1회, Unity 6000.3.25f1 배치, 실제 프로젝트): 255/255 통과 — 실패 0, 건너뜀 0.** 247 + 신규 8. XML `Delivery/logs/editmode-results-2026-10-04-frameorder.xml`, 로그 `Delivery/logs/editmode-log-2026-10-04-frameorder.txt`(종료 2026-10-04 00:31:04Z). 이전 r1/r2/r3 XML·로그 보존.

검증 구분:
- 대기열 단위(`RunRewards`, 순수 C#): 1차 테스트.
- GameManager 통합(실제 인스턴스, 프레임 끝 메서드 직접 호출): 2차 EditMode 8개 — 통과.
- 엔진 프레임 루프(Update/LateUpdate 실제 호출, 실제 픽업 오브젝트 흡인): **미검증.** `DefaultExecutionOrder` 속성과 Unity의 "모든 Update 후 LateUpdate" 보장에 근거. PlayMode는 여전히 미실행(반복 시도·재설치 안 함).

잔여(범위 밖): `ArtV1Spec` 321/329 vs 기획 355/363 — 미수정. 커밋·푸시 없음, 실제 세이브·아트 게이트 미변경.
