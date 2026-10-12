# 요구사항 증거표 G01~G35 (2026-10-06, 1차 정리)

Orca task `task_f357b59abbb4` / dispatch `ctx_ae31466d1986`. 기준: 기획 10 §1·§6, 05~09와 최신 사용자/코디네이터 결정(아래 "최신 기준"). **이 표는 game_complete 판정이 아니다.** 필수 항목에 fail/unverified가 남아 있다.

## 판정 규칙

- **pass**: 합격 조건 전체에 현재 소스나 빌드의 실제 증거가 있다.
- **unverified**: 일부 조건에 증거가 없다. 기능 테스트는 통과했지만 화면, 영상, 청취, 네이티브 입력, 출시 빌드 증거가 없는 경우를 포함한다. 테스트 이름이나 체크박스만으로는 통과로 올리지 않는다.
- **fail**: 증거가 조건 위반을 보여 준다.
- 출시 빌드(`BuildRelease.Windows64`, 엄격 검사)는 아직 만들 수 없다(G32). 그래서 빌드 의존 항목의 증거는 **1차 프리뷰 빌드**다. 출시 후보(RC)에서 다시 확인해야 한다(열 "RC").

## 빌드·소스 해시

| 대상 | 값 |
|---|---|
| r8 프리뷰 ZIP `Builds/CocktailCoffeeTea-FirstPassPreview-2026-10-06-r8.zip` | SHA256 `A4961B4970A30D6DF8F54BA845A92AC3AE007C80B029A44F03D7C8C29104CCD2` (재계산 일치) |
| r8 `CCT.Runtime.dll` | SHA256 `E874F2CCBC3D1404C19599CEBDCDACA2F60D6DEC8A9E8FD69B405E824DFEA10B` (재계산 일치) |
| r7 프리뷰 ZIP | SHA256 `ABC64C5AF0BED19BC2338F6C8986A7D1172F37CA324560493DFB1796F11A6C36` (재계산 일치, 보존) |
| r9 프리뷰(이번 변경 포함) | §"r9" 참고 |
| 출시 빌드 1.0.0 | **없음.** 엄격 검사 실패: `Delivery/logs/strict-release-attempt-2026-10-05-r7.txt`("bartender 폴더 없음 / 1차 임시 아트 포함 — 출시 불가"). `Builds/Windows64`는 2026-09-27의 오래된 출력이라 증거로 쓰지 않는다 |

## 최신 기준 (오래된 문서 기술 정리, 게이트는 완화하지 않음)

| 항목 | 오래된 기술 | 최신 결정 / 근거 | 이 표의 적용 |
|---|---|---|---|
| 엔진 | 05/10/11: Unity 6000.3.16f1 | 실제 6000.3.25f1, 되돌리지 않음(기획 26 "기준점", ProjectVersion) | 모든 증거는 6000.3.25f1 |
| 일반 몹 로스터 | 05 "일반/엘리트 11", 06 §3 11종·§4 칵테일/차 50:50 | 02(2026-09-25 정정): 커피 일반 몹 2종(에스프레소 벅, 밀크 포머) 포함 → 13종, 19몸체/363프레임. 150초 이후 트랙 비율은 2026-10-06 결정(기획 27, 02/07 갱신)으로 칵테일 49 / 차 49 / 커피 2(이전 40/40/20) | G07·G08은 13종과 새 비율 기준 |
| 근접 판정 | 06 §2: 폭=길이, 높이 8.6 전체 레인 박스 | 99 로그 2026-09-25 사용자 요청: 레이피어/검 "반원 횡베기", 판정도 VFX와 같은 반원(`RapierWeapon.Slash`) | G04는 앞쪽 반원(반지름=최종 사거리) 기준. 박스 8.6 기준으로는 판정하지 않는다 |
| 커피 경제 | 07 §5: 한 런 8/9포인트 배분 의도 / §2: 80은 상한 아님 | 기획 27: A안(커피 비중 2%), 보스 80=8포인트·임계 10·노드 비용 1 유지. 9~12/≤14포인트는 관측 목표이고 상한이 아님 | G17 단언은 그대로. 원두·포인트 실측은 playthroughs |
| 아트 | 20: 바텐더 v5 걷기·대기 1차 잠정 허용 | 출시 검사는 그대로 엄격하게 둔다(26 단계 3에서 잠정 허용 해소) | G32는 fail 유지 |

## 테스트 실행 증거

| 실행 | 결과 | 파일 |
|---|---|---|
| r8 기준 EditMode | 365/365, 실패·건너뜀 0 | `Delivery/logs/editmode-results-2026-10-06-normalplay-edit1.xml` |
| r8 기준 PlayMode | 135/135, 실패·건너뜀 0 | `Delivery/logs/playmode-results-2026-10-06-normalplay-play1.xml` |
| 이번 변경 EditMode | 369/369, 실패·건너뜀 0 (2026-10-06 07:32Z) | `Delivery/logs/editmode-results-2026-10-06-econ-edit1.xml` |
| 이번 변경 PlayMode | §"이번 실행" 참고 | `Delivery/logs/playmode-results-2026-10-06-econ-play1.xml` |

## G01~G35

| ID | 상태 | 증거 (경로 / 테스트) | 남은 것 | RC |
|---|---|---|---|---|
| G01 새 세이브 부팅 | **pass**(r8) | r8 ZIP을 새 폴더에 풀어 실행: `logs/normal-play-2026-10-06/r8-unzip-fresh-player.log`(`source=Fresh, v2`). r8 네이티브 타이틀 `기획/ArtProduction/normal-play-2026-10-06/native-r8/title.png`(한글). 잔액 0: `TitleViewTests.FooterShowsCurrentSpecialtyBalance`. 메뉴 진입: `TitleViewTests.OpeningEachSubScreen_HidesTitleRoot_AndClosingRestoresIt` | | 필요 |
| G02 2직업×2무기 시작 | **unverified** | 기능: `CharacterSelectTests` 6개. 정상 런 4회 `events.log` "run bound … hp=100/100 pos=(0.00,-2.00)"(p1-gun/p1-bow/p3-rapier/p3-sword). 루트 1개: `RunLifecycleStressTests.Restart10Times…`(GameManager·Player·AudioService·EventSystem 각 1). 무기 아트 화면: `Delivery/screens/first-pass-2026-10-04-r4/21_combat_*.png`(r4, 이전 빌드) | 현재 빌드에서 4조합 무기 아트 캡처 | 필요 |
| G03 이동 대각/레인/마우스 | **unverified** | `BeltScrollTests.PlayerDepth_IsClampedToLane`, `PlayerStatsTeaTests.MoveSpeed_CappedAt10…`, `InputSettingsTests` 3, 검증기 입력은 `ClampMagnitude` 8방향 | 대각선 정규화와 "마우스 설정만 활성" 전용 단언 확인 필요. 실제 키보드·마우스 장치 입력은 미검증(25) | |
| G04 근접 앞/뒤/다른 레인 | **unverified** | 최신 기준은 반원. `RapierWeaponTests` 정면/대각 반원 안/등 뒤 미스. 잔광=판정 모양(99 로그 09-25) | "사거리 밖 다른 레인 미스", "한 휘두름에 적당 1회" 전용 테스트와 캡처 | |
| G05 원거리 발수/관통 | **unverified** | `PlayerProjectileWeaponTests` 5(부채, 관통 중복 없음, 수명). `PlayerStatsTeaTests.ExtraThrusts_And_PierceCount_AreCappedAtSpecMax` | 1/3/8발 각도 값과 관통 0/1/6, 고속 스윕 전용 단언 | |
| G06 접촉/단발/장판 중첩 | **pass** | `PlayerDamagePathTests` 4(0.8초 무적, 장판은 무적 없음·무적 중에도 적용), `PlayerZoneDamageTests.SameFrame_MultipleRequests_OnlyHighestSurvives`, `SaveV2GameManagerTests.Death_CommitsFailedOnce…`, `RunEndPriorityTests` | 0.4초 틱 간격의 정밀 값은 코드 대조만 | |
| G07 시드 스폰 경계 | **unverified** | `EnemyRosterTests` 11(15/45초 경계, 120초 이후 전 종류, 커피 150초 경계·2% 분할·시드 표본), `RunRandomTests` 4 | 60/90/120초 각 경계 단언, 스폰 간격 공식과 상한 100의 전용 테스트 | |
| G08 11(+2)종 행동 | **unverified** | `EnemyBehaviorTests` 5(셰이커·마티니·소용돌이), `EnemyBombTests` 4(올리브·슈가), 정상 런의 전 종류 등장 | 레몬·티백·케틀·잎 감시자·다도 수호자·커피 2종 각각의 강제 스폰 행동 단언 | |
| G09 보스 일정 | **pass** | `BossDirectorScheduleTests` 2, `BossFlowTests.Schedule_SpawnsMidBossByRunTime`, 정상 클리어 4회 150/320/500/680.00 실측, p2-sword 드립 364.28(이전 보스 생존 지연) — `first-pass-normal-play-2026-10-06.md` §2 | | 재측정 |
| G10 그라인더 예고 고정 | **unverified** | `ChargerDriverTests.DashDirection_LocksAtTelegraphStart…` | 영상 증거 | |
| G11 드립 장판/투명화 | **unverified** | `DripSpecterDriverTests` 4 | 영상 증거 | |
| G12 머신 압력 | **unverified** | `EspressoLeviathanDriverTests` 5(기본 속도, 피해 완화, 폭발, 증기 정지, 폭우 예고) | 9/13 단계 속도·압력 중 단독 시전 전용 단언, 영상 | |
| G13 최종 4패턴 | **unverified** | `CustomerBossDriverPatternTests` 2(별점, 주문 부정), `BossFlowTests.FinalBoss_Refund…ThenSummonsMinions`, `EnemySpriteTests.DifficultCustomer_SwitchesToEnragedArt…` | 고정 순서·HP49% 분노 값·원래 강화 복원 전용 단언, 영상 | |
| G14 카드 34행 데이터 | **pass** | `Spec07CardTableTests.AssetDatabase_BasicCards_MatchSpec07Table_RowByRow`, `RuntimeDefaults_BasicCards_MatchSpec07Table_RowByRow`, `Hybrid_IsSingleSpecialMaxOne`: 07 표34행의 ID·등급·중첩·효과·제한 전체 대조. r11 전체 EditMode374/374 XML에서3개 통과 확인 | | |
| G15 등급 후보/정비 | **pass** | `GrowthSpecTests.Draw_*` 4(정규화, 1/2장, 0장 정비, 하이브리드 미출현), `CardSystemTests` 10 | | |
| G16 동시 레벨/요구 감소 | **pass** | `CoffeeEconomyTests.SameTick_CocktailThenTea_ThenCoffeeTree_FIFO`, `RequirementReduction_ReevaluatesImmediately…`, `GameManagerFrameOrderTests` 8, `GrowthSpecTests.RealGauge_*` | | |
| G17 원두 20+20+40/20% | **pass** | `CoffeeEconomyTests.BossSequence_20_20_40_Base_GivesEightPoints`, `…WithPermanentPlus20_GivesNinePoints_Remainder6`, `BonusPartitionInvariance_*`, `PermanentBeanCupper_DataValue_ProducesNinePoints`. 커피 비중 변경 뒤에도 그대로 통과(econ-edit1) | | |
| G18 커피 15노드 | **unverified** | `SkillTreeTests` 9, `CoffeeEconomyTests.TryUnlockNode_*`/`FullyUnlockedTree_*`/`TreeAutoOpen_*`, `CoffeeTreeUiTests` 7, `BossFlowTests.Continue_KeepsPoints…`/`K_Opens…` | UI 실기 화면(720p/1080p) | |
| G19 레이피어 하이브리드 | **pass** | `HybridWeaponTests` 4, `CardCodexViewTests.HybridCard_*`, 레이피어 정상 런 자연 해금(p1 66.8초, p3 75.3초)과 저장 `hybrid_tea_martini` | | 재측정 |
| G20 다른 3무기 하이브리드 없음 | **unverified** | `HybridWeaponTests.GunStart_NeverOffersHybrid_EvenWithCocktail3Tea3`, `CharacterSelectTests.Melee/RangedWeapon_Never…`, 정상 런 총·활·검 `hybrid=False` | 검·활 시작 3+3 전용 단언 | |
| G21 환불/봉인 Pause | **pass** | `PlayerStatsSuppressTests` 4(원본 보존, 봉인은 Playing에서만 감소), `PauseTests` | | |
| G22 영구 6노드 | **pass** | `PermanentSkillTreeTests` 9, `SaveV2PurchaseTests` 3, `EnhancedBossFlowTests.PermanentSkill_*`, `PermanentSkillViewTests` 4 | 총비용 27 전용 합계 단언은 데이터 대조로만 확인 | |
| G23 도감 | **unverified** | `CardCodexTests` 9, `CardCodexViewTests` 7, `CodexCoffeeNodeTests`, `RenderedTextLayoutTests` | 현재 빌드 도감 캡처(r4 `02_codex.png`는 이전 빌드) | 필요 |
| G24 상태 전환/오버레이 | **unverified** | `PauseTests` 10, `KeyboardNavigationTests` 8, `CoffeeTreeUiTests.Gate_*` | 수동 실기(키 관통, 설정 열기/닫기 후 입력·시간 복구) | 필요 |
| G25 동시 종료 우선순위 | **pass** | `RunEndPriorityTests` 3, `GameManagerFrameOrderTests.SameFrame_Death/Timeout_Beats…`, `SaveV2RunLifecycleTests.RunStats_AllOutcomes_ExactlyOneCounterEach…`, 신규 `RunLifecycleStressTests.EnhancedBossKill_ThenDeath…`(사망 뒤 카드/트리 없음, 집계 1회) | | |
| G26 20분/포기/재시작10회 | **unverified** | 20분: p1-sword 정상 런 1200.0초 "영업 종료". 포기: p1-gun 보조 확인. 재시작 10회: 신규 `RunLifecycleStressTests.Restart10Times…`(결과 버튼 경로, 루트/static/오디오/픽업/탄/장판 누수 0, heap 마지막3/처음3 ≤1.10 단언) | 신규 테스트는 Editor PlayMode 기준이다. 출시 플레이어 프로세스의 재시작 10회 메모리 로그(10 §3)는 G35와 함께 남음 | 필요 |
| G27 v0→v2 이전 | **pass** | `SaveV2MigrationTests` 7, `SaveSystemTests` 12 | | |
| G28 쓰기 중단/손상 | **pass** | `SaveV2IntegrityTests` 8 | | |
| G29 쓰기/구매 실패 | **pass** | `SaveV2RetryTests`, `SaveV2PurchaseTests.Purchase_SaveFailure_RollsBack…`, `SaveV2UiTests` 8, `SaveSystemTests.Save_Failure_*` | | |
| G30 강제 종료/다음 실행 | **unverified** | `SaveV2RunLifecycleTests.ForcedTermination_NextLoad_AbandonsOnce…`(임시 파일) | 10은 "별도 테스트 저장 빌드" 증거를 요구한다: 실제 플레이어 강제 종료 → 재실행 확인이 없음 | 필요 |
| G31 720p/1080p+키보드 | **unverified** | `RenderedTextLayoutTests`, `QaUiDefectTests`, `TitleLayoutTests`, r7 QA의 720p 도감 수정(`first-pass-r7-2026-10-05.md` §1), `KeyboardNavigationTests` | 현재 빌드의 모든 화면 720p/1080p 캡처 세트, 실제 키보드 전용 조작 | 필요 |
| G32 363몸체+manifest | **fail** | 엄격 검사 실패(바텐더 몸체 미완·잠정 아트), `logs/firstpass-preview-status-r8.txt` "bartender (body, needs_fix, 1차 임시 허용)". 아트 검수 `기획/ArtProduction/final-review-2026-10-06/report.md`(코디네이터 27) | 아트 담당 후보 → 적용 → 엄격 검사 통과 | 필요 |
| G33 22SFX/3BGM | **unverified** | 파일 측정 `Delivery/audio-font-resume-2026-09-27.md` §2(ID 25/25, 길이·피크·루프 통과), `AudioServiceTests` 6 | 실제 청취·런타임 합산 피크·볼륨 0/Pause 실기(26 단계 4) | 필요 |
| G34 압축 해제 실행 | **unverified** | r8 헤드리스 새 폴더 실행(새 저장/기존 저장 재로드, 해시 유지), r8 네이티브 타이틀/설정은 코디네이터 확인 | 출시 ZIP 자체가 없음. 네이티브 부팅→런→종료→재실행, 한글·오디오 | 필요 |
| G35 부하 성능 | **unverified** | 검증 PC: Windows 11 Pro, i7-14700F(28 논리 코어), RAM 15.8GB, RTX 5060 | 10 §3 QA 고정 부하 시나리오·Release 1080p 측정 CSV가 없음(시나리오 자체 미구현) | 필요 |

### 요약

- pass 14: G01 G06 G09 G14 G15 G16 G17 G19 G21 G22 G25 G27 G28 G29. (G01·G09·G19는 프리뷰 빌드 증거라 RC에서 다시 확인)
- fail 1: G32
- unverified 20: G02 G03 G04 G05 G07 G08 G10 G11 G12 G13 G18 G20 G23 G24 G26 G30 G31 G33 G34 G35

(요약 수치는 아래 "이번 실행"에서 확정한다.)

## r11 활 기본 관통 반영 (2026-10-06)

- 일반 프리뷰 `Builds/FirstPassPreview-r11/CocktailCoffeeTea.exe`, ZIP `Builds/FirstPassPreview-r11.zip`.
- ZIP SHA256: `87D6AD3E49F37A700BE3E0F809F1F51D4EB90AFD8699AF0358A77ADC928855D3`.
- Runtime DLL SHA256: `D8AD84F13A97396C34A4E917B126714AACAC5D265A8792966F25F3F7696E2565`.
- 활 시작 관통1 / 총0 / 카드 누적 / 원거리 발사체 동작 / 결과→상점 복귀: PlayMode12/12. 무기 설명 EditMode1/1. XML·로그 `Delivery/logs/final-2026-10-06-bow-r11-*`.
- 최초 EditMode 필터가 파일명과 실제 클래스명을 혼동해0개 실행됐다. 이 실행은 통과 증거에서 제외하고 실제 클래스 JobWeaponCopyTests 재실행1/1만 사용한다.
- 새 저장 네이티브 부팅 로그에서 엔진/해상도/저장 경로 확인, 정상 창 닫기까지 오류·예외 없음. 화면 캡처는 다른 창에 가려 확인 불가하므로 시각 게이트 통과로 사용하지 않는다.
- 기존 `Play-FirstPass.cmd`는 r11로 갱신했다. 기존 사용자 저장 경로 유지.
- r11은 두 캐릭터의 provisional 아트 후보를 포함한 프리뷰다. 엄격 출시 판정이나 game_complete가 아니다.
- 입력 전용 검증 build6에서 새 저장 활 런 진행 중. 헤드리스 실행이므로 프레임 수치는 그래픽 성능 증거에서 제외한다.

## r13 피드백 반영

활 원본의 좌우 방향을 장착 시 보정했고 손잡이/발사 앵커도 같은 변환을 적용했다. 플레이어·적·보스 투사체는 아트/아이콘/대체 원형 모두 시각 크기50%, 판정·피해·속도 유지. 전체 EditMode378/378, 관련 PlayMode15/15. 실제 렌더 시각 투어39장/오류0/예외0, tea_bow 걷기/전투·결과 화면 직접 확인. 투어는 강제 스폰·재료·회복·사망을 포함하므로 정상 완주나 성능 증거가 아니다.

- 최신 일반 프리뷰: `Builds/FirstPassPreview-r13/CocktailCoffeeTea.exe`.
- ZIP SHA256: `32E019070ED783184E0C0D9CFBB24354092715B5F3E5570843396D97E68B8A6F`.
- 화면/투어 증거: `Delivery/screens/final-r13-1080p-2026-10-06/`.
- 테스트 XML: `Delivery/logs/final-2026-10-06-bow-projectile-final-edit.xml`, `final-2026-10-06-bow-projectile-final-play.xml`.
- 활 방향 회귀: `Delivery/logs/final-2026-10-06-bow-facing-edit.xml`.
- 활 관통1 자연 완주 증거는 방향 보정 전 r11: `Delivery/Reports/bow-start-balance-2026-10-06.md`.
- `Play-FirstPass.cmd`는 r13으로 갱신했다. 엄격 출시 검증은 미완료.

## 2026-10-07 G30/G35 갱신 (상세: `Delivery/Reports/resume-2026-10-07-implementation.md`)

- **G30 → pass(파일 기반, RC 재확인 필요)**: 격리 저장에서 실제 플레이어 프로세스 강제 종료(TerminateProcess) → r13을 hidden으로 재실행. abandonedRuns 0→1 1회, activeRun null, 체크포인트 재료(칵테일9/차5) 보존, 두 번째 재실행 시 save 바이트 동일. 증거 `Delivery/logs/g30-forced-kill-2026-10-07/`. 타이틀 중단 알림의 실제 화면 표시는 미캡처(화면 방해 금지).
- **G35 → unverified 유지**: 10 §3 고정 부하 도구(`LoadScenario`, `CCT_QA_LOAD` 전용, `BuildRelease.Windows64LoadTestBatch`)를 구현했다. 10초 smoke 결과는 사용자 게임과 경합한 상태여서 참고로만 둔다(`Delivery/logs/g35-loadtest-smoke-2026-10-07-REFERENCE-ONLY/`). 30+180초 최종 측정은 미실행.
- 테스트: EditMode 380/380, PlayMode 140/140 (`Delivery/logs/final-2026-10-07-g35g30-*.xml`).

## 2026-10-08 갱신 (상세: `Delivery/Reports/release-readiness-2026-10-08.md`)

- 최신 프리뷰 r16 `Builds/FirstPassPreview-r16.zip`(SHA256 `BB25CF09…9097A9`), EditMode 391/391, PlayMode 140/140. 엄격 출시 검사는 실패 상태 그대로(G32).
- 실제 결함 4건 수정+회귀 테스트: 적 행동 클립 중 hurt 덮어쓰기, 무기 손 anchor 1프레임 지연, 런 종료 시 defeat 정지, 결과 화면 OpenPause로 인한 음악 덕킹 잔류. 사용자 지시 총 2배(0.4→0.8, 실측 1.28/1.6).
- **pass 제안(코디네이터 판정 전, RC 재확인 필요):** G02, G10, G11, G18, G23, G26, G35(30+180초 단독: 60.00fps, p95 16.67ms, 100ms 정지 0, WS 632MB/private 1144MB, 오류 0, 재시작 heap 1.043).
- **좁힘, 상태 유지:** G12·G13(영상 확보, 전용 단언 없음), G24·G31(화면 세트 1080/720 확보, 실제 키보드 입력 없음), G33(실제 출력 경로·볼륨0·Pause·재시작 측정, 청취 없음), G32 fail(363/363 프레임 실제 노출 매핑 제공, 아트 승인 대기).
- 변경 없음: G03 G04 G05 G07 G08 G20 G34 — 다음 wave 목록은 보고서 §7.
