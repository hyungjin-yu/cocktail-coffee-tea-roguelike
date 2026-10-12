# 1차 기능 PlayMode 실패 6건 해결: 2026-10-06

Orca task `task_339971fb3b73` / dispatch `ctx_1ed8182e9748`. Unity 6000.3.25f1. 이전 보고서: `first-pass-functional-playmode-2026-10-05.md` (play2: 134건 중 128건 통과, 실패 6건).
**상태: 완료.** 실패 6건의 원인을 판정했고, 모두 테스트 쪽 원인(스펙 변경 이후 낡은 기대값, 에디터 환경, 우회 경로)이었다. 게임 런타임 결함은 없었다. 격리와 진단 도구의 결함 4건(그중 2건은 새로 발견)도 고쳤다. 마지막 소스에서 **PlayMode 134/134, EditMode 365/365, 건너뜀 0**이다.
**런타임 코드, 아트, 경제 수치, 걷기는 바꾸지 않았다. 그래서 r8은 만들지 않았고 r7이 현재 실행본이다.**

## 1. 실패 6건 원인과 처리

| 테스트 | 원인 (근거) | 처리 |
|---|---|---|
| HybridWeaponTests.ChoosingHybrid_UnlocksAndAttachesBow_WithoutRemovingRapier | 테스트 기대값이 낡았다. 07 §2-2~4에 따르면 하이브리드 해금 뒤에도 남은 일반 카드 보상은 Playing을 거치지 않고 바로 이어서 열린다. 재료를 5000 넣었으므로 해금 직후 상태는 LevelUp이 맞다. 런타임 `FinishRewardChoice`도 같은 규칙으로 동작한다. | 대기 보상(`Rewards.PendingCardCount`)이 있으면 LevelUp과 timeScale 0을, 없으면 Playing을 단언하도록 바꿨다. 이어서 남은 보상을 모두 고른 뒤 Playing, timeScale 1, 레이피어와 활이 유지되는지 확인한다. 같은 런에서 하이브리드가 다시 제안되지 않는지도 단언에 추가했다. |
| HybridWeaponTests.HybridBow_FiresAtEnemy_… (STALL 30초) | 위와 같은 원인이다. LevelUp 창(timeScale 0)이 열린 상태에서 `WaitForSeconds`가 끝나지 않았다. 고친 뒤 fix2 실행에서 결함이 하나 더 드러났다. 카드 약 30장을 고른 플레이어가 2유닛 앞 Brute를 레이피어로 한 번에 처치해 MissingReference가 간헐적으로 났고, 레이피어 피해만으로도 테스트가 통과할 수 있는 구조였다. | 남은 보상을 모두 처리한 뒤 적을 둔다. 적은 레이피어 획득 범위(Range+0.6) 밖, 활 획득 범위(Range+3.6) 안에 체력 ×1000으로 둔다. 0.35초 뒤 화살(PlayerProjectile)이 있는지, 맞을 때 적이 레이피어 범위 밖이었는지 단언한다. 이제 보조 활 자체를 검증한다. |
| MaterialCollectionTests.RunEnd_AddsThisRunsMaterials_ToPermanentTotals | 테스트 기대값이 낡았다(저장 v1 규칙). 08 §6(v2)은 체크포인트(첫 품종 발견 즉시 저장 등)마다 마지막 반영량과의 차이만 전역 합계에 더하라고 정한다. 그래서 런 중에 4가 반영되는 것이 맞다. | 런 중에는 activeRun이 있고, 전역 합계가 activeRun 반영량과 같으며, 실제 획득량 이하인지 단언한다. 종료 뒤에는 정확히 4/6/3인지(중복 가산 없음)와 activeRun이 null인지 단언한다. |
| PauseTests.SkillTreeButton_DisabledOverCardSelect_… | 테스트가 08:102의 1초 "계속" 카운트다운이 생기기 전에 작성됐다. `ResolveEndOfFrame`은 카운트다운 중(`resuming`)에는 보상을 처리하지 않으므로 다음 프레임에 LevelUp이 될 수 없다. | 카운트다운 시작을 단언하고, 실제 시간 기준 최대 3초 안에 끝나기를 기다린 뒤 기존 단언(LevelUp 상태, 커피 트리 버튼 비활성과 사유)을 그대로 검사한다. |
| SettingsViewTests.ToggleFullscreen_And_ToggleVSync_FlipUnityState | 에디터 환경 문제다. 에디터 PlayMode(배치 포함)에서는 `Screen.SetResolution`이 창 모드를 바꾸지 않는다. 이 테스트는 실제 `Screen`과 `QualitySettings`를 읽었기 때문에 이 기기에서는 통과할 수 없었고, 에디터 품질 설정도 바꿨다. | 스위트 전체를 EditMode와 같은 `MemoryDisplayAdapter`로 격리했다. 이름을 `…ApplyFlippedStateThroughDisplayAdapter`로 바꾸고 다음을 검증한다: 적용 1회, 반대 모드 전달, 10초 확인, 확인 뒤 설정 기록, VSync와 60fps 제한. 끝나면 원래 상태로 돌린다. 실제 Windows 창 전환은 네이티브 QA(r6 표시모드 저장·복원)가 확인한 항목이다. |
| TitleViewTests.OpeningEachSubScreen_HidesTitleRoot_AndClosingRestoresIt | 테스트가 실제 경로를 우회했다. 타이틀 루트는 타이틀 메뉴 버튼 핸들러(`TitleView.BuildRoot`)가 숨긴다. 테스트는 `SettingsView.Show()`를 직접 불러 그 경로를 건너뛰었다. | 타이틀 안의 실제 "설정"과 "제작 정보" 버튼을 눌러 검증한다. 하위 화면이 열렸는지 단언을 추가했다. |

## 2. 격리와 진단 결함 수정 (테스트 코드만)

- **TitleViewTests의 저장 경로 복원:** `FooterShowsCurrentSpecialtyBalance`가 본문 끝에서 `SetPathForTests(null)`을 호출했다. 그 때문에 다음 테스트가 실제 `LocalLow\…\save.json`(v1)을 읽었다(play2 로그). 이제 TearDown에서 실패 여부와 상관없이 `TestSaveIsolation.SessionPath`로 되돌리고 임시 파일 `.bak`/`.tmp`도 지운다.
- **새로 발견: AutoStart가 실제 저장 파일을 읽음.** `GameBootstrap.AutoStart`(AfterSceneLoad)는 러너의 RUN START와 `[OneTimeSetUp]`보다 먼저 실행된다. 그래서 실제 save.json, settings.json, 구형 PlayerPrefs를 읽었다(`logs/playmode-2026-10-06-fix1-autostart-realpath-excerpt.txt`). 실제 저장에 activeRun이 남아 있었다면 `RecoverInterruptedRun`이 실제 파일에 쓸 수도 있었다. `TestSaveIsolation`에 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`를 추가해, 러너 초기 씬(`InitTestScene*`)일 때만 세션 임시 경로, 메모리 구형 저장소, 메모리 화면 어댑터로 먼저 돌린다. 테스트 어셈블리는 `UNITY_INCLUDE_TESTS` 조건이라 플레이어 빌드에는 들어가지 않고, 일반 에디터 Play에도 영향이 없다. 이후 실행 fix2, play4, play5 모두 실제 경로 접근 0건.
- **TestProgressLog 반복 실행 수명주기:** `RunStarted`에서 정적 필드(path, currentTest)를 초기화하고, 이전 실행이 남긴 하트비트를 파괴한다. `RunFinished`에서는 하트비트를 파괴하고 path를 비운다. 정지 감시(timeScale 0이 30초 이어지면 LogError로 실패시키고 timeScale을 1로 되돌림)는 그대로 명시적인 실패로 남겼다.
- **새로 발견: TestProgressLog 때문에 EditMode가 RunError로 끝남.** 이 콜백은 EditMode 실행에서도 호출되는데, 에디트 모드에서 `DontDestroyOnLoad`가 예외를 던져 EditMode 러너 전체가 **종료 코드 3으로 끝나고 XML이 남지 않았다**(edit1, `logs/editmode-2026-10-06-edit1-runerror-excerpt.txt`). 10-05에 넣은 도구가 만든 회귀이고, 그날은 EditMode를 다시 돌리지 않아 놓쳤다. 하트비트는 이제 `Application.isPlaying`일 때만 만든다.
- **새로 발견: Espresso 테스트 간헐 실패.** play4에서 `EspressoLeviathanDriverTests.SteamBeam_FreezesMovement_DuringTelegraph`가 "Expected (0.00, 0.00) But was (0.00, 0.00)"으로 실패했다. play2와 play3에서는 통과했다. `Enemy.FixedUpdate`가 매 스텝 속도를 0으로 맞춰도, 그 뒤 물리 스텝이 웨이브 적과의 접촉을 풀면서 0.005 미만의 잔여 속도가 남는다. 정확한 float 비교가 원인이었다. 이제 드라이버 속도가 정확히 0인지(멈춤 의도)와 강체 속도가 0.05 미만인지(추적 속도 1.3 대비) 단언한다.
- **감사했지만 바꾸지 않은 것:** 다른 테스트의 SetPathForTests 호출은 모두 TearDown이나 기반 클래스 TearDown에서 SessionPath로 복원한다(CardCodexView, EnhancedBossFlow, Material, PermanentSkillView, Result, EditMode SaveV2/SettingsV2/FirstPassArt). 일부 PlayMode 테스트는 `%TEMP%`에 `.bak` 임시 파일을 남긴다. 사용자 데이터는 아니라서 고치지 않았다. Unity 테스트 프레임워크는 기본 동작으로 결과 사본 `TestResults.xml`을 persistentDataPath에 덮어쓴다. 이것은 저장 파일이 아니며 이번 작업 전부터 있던 동작이다.

## 3. 변경 파일 (8개, 모두 `Assets/_Project/Tests/PlayMode/`)

`EspressoLeviathanDriverTests.cs`, `HybridWeaponTests.cs`, `MaterialCollectionTests.cs`, `PauseTests.cs`, `SettingsViewTests.cs`, `TestProgressLog.cs`, `TestSaveIsolation.cs`, `TitleViewTests.cs`. 새 파일과 .meta 변경은 없다. 런타임, 아트, 에셋, 설정은 바꾸지 않았다.

## 4. 실제 결과 (격리 사본 `C:\UnityScratch\CCT`)

| 실행 | 범위 | 결과 | 증거 |
|---|---|---|---|
| fix1 | 영향 5개 클래스 | 31/31 통과. 단, AutoStart의 실제 경로 읽기를 발견 | `C:\UnityScratch\pm\fix1.*` |
| fix2 | 같은 범위, 리다이렉트 적용 | 30/31. 활 테스트 간헐 실패(위 설명) | `C:\UnityScratch\pm\fix2.*` |
| fix3 | Hybrid | 4/4 | `C:\UnityScratch\pm\fix3.*` |
| play3 | 전체 PlayMode | 134/134 (TestProgressLog와 Espresso 수정 전 소스) | `C:\UnityScratch\pm\play3.*` |
| edit1 | 전체 EditMode | RunError, 종료 코드 3, XML 없음 → TestProgressLog 수정 | `logs/editmode-2026-10-06-edit1-runerror-excerpt.txt` |
| play4 | 전체 PlayMode | 133/134 (Espresso 간헐 실패) | `logs/playmode-results-2026-10-06-play4-espresso-intermittent.xml` |
| **play5** | **전체 PlayMode, 최종 소스** | **total 134, passed 134, failed 0, skipped 0**, 05:43:45Z~05:46:51Z, 정지 0, 실제 경로 접근 0 | `logs/playmode-results-2026-10-06-functional-play5.xml` (SHA256 e523b8a3…), 진행 기록 `logs/playmode-progress-2026-10-06-functional-play5.txt` |
| **edit3** | **전체 EditMode, 최종 소스** | **total 365, passed 365, failed 0, skipped 0**, 05:47:34Z~05:47:42Z | `logs/editmode-results-2026-10-06-functional-edit3.xml` (SHA256 2466c311…) |

- **소스 일치:** play5와 edit3 실행 뒤 Assets, Packages, ProjectSettings 1869개 파일의 SHA-256이 실제 프로젝트와 격리 사본에서 모두 같았다(diff 0). 바뀐 파일은 위 8개뿐이다.
- **실행 방식:** 매 실행은 `C:\UnityScratch\pm\run.ps1`로 고정된 결과·로그·진행 파일 경로를 쓰고, 시간 제한을 두며, 시간이 넘으면 자기 PID만 종료한다. 사용자의 `unity.exe`(mcp) 프로세스 3개는 건드리지 않았다.
- **사용자 데이터:** 실제 `LocalLow\DefaultCompany\CocktailCoffeeTea\save.json`과 `.bak`의 수정 시각은 2026-09-28 그대로다(v1 내용 유지). `ArtProduction/first-pass-preview-user-data`, 사용자 프리뷰, 플레이어, 에디터는 건드리지 않았다. GUI 입력도 하지 않았다.

## 5. 실행본

런타임 변경이 없어서 r8은 만들지 않았다. 현재 실행본은 **r7**이다: `Builds/CocktailCoffeeTea-FirstPassPreview-2026-10-05-r7.zip`, SHA256 `ABC64C5AF0BED19BC2338F6C8986A7D1172F37CA324560493DFB1796F11A6C36`(오늘 다시 계산해 일치 확인). 실행 폴더는 `Builds/FirstPassPreview-r7`이다. 이번에 바꾼 테스트 파일은 플레이어 빌드에 들어가지 않으므로 r7 동작과 무관하다.

## 6. 미확인 (그대로 남음)

- 일반 난도 20분 완주, 보스 전체를 실제 입력으로 상대하기, 밸런스, 성능, 청취 검수: 미검증.
- 네이티브 키보드, 포커스, 스크롤 입력 전달: 미검증(이전 QA에서도 UNKNOWN).
- 강제 AutoTour는 사용하지 않았다. 자동 테스트 통과를 일반 플레이 검증으로 주장하지 않는다.
- PlayMode 로그에 "no audio listeners" 경고가 매 프레임 찍혀 로그 크기가 커지는 문제는 기능 결함이 아니어서 고치지 않았다.
- 커밋, 리셋, 푸시는 하지 않았다.
