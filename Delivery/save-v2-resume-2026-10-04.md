# 저장 v2(save.json) 재개 — 2026-10-04

Orca task `task_b39699d4b301` / dispatch `ctx_d37e9d07e62c`. 대상 `C:/Users/user/Documents/Projects/CocktailCoffeeTea`, Unity 6000.3.25f1(ProjectVersion 그대로, 재설치·다운그레이드 없음).
입력: 기획 05, 07 §6(영구 강화), 08 §5/§6(스키마·체크포인트·이전·실패 복구), 10 G27~G30, 11, 16(최신 기록). AGENTS.md는 프로젝트에 없음(이전 보고와 동일하게 확인).

**범위 한정:** 이번 작업은 **save.json 영구 진행 경로만**이다. 08 §4/§5의 **settings.json 전체 스키마(오디오 3값·해상도·창 모드·vsync·movementScheme·shake·effects·damageNumbers·tutorialSeen)와 그 .tmp/.bak 별도 저장은 구현하지 않았다 — 잔여.** 08 전체 완료를 주장하지 않는다.

## 보존

- 시작 시 `git status` 확인: 기존 수정 55+파일·미추적(ArtSource, Audio, Fonts, 커피 255 통과 작업의 CoffeeBeanGauge/RunRewards/InputReleaseGate/GameManagerFrameOrderTests, 09-28 Claude UI의 TitleView/PauseView/SettingsView 등) 모두 유지. 수정 전 사본: 세션 scratchpad `backup-save-v2/`(SaveData, SaveSystem, PermanentProgress, GameManager, PermanentSkillTree, PermanentSkillView, HudView, TitleView, CharacterSelectView, SaveSystemTests).
- GameManager의 종료 우선순위(사망→클리어→시간 초과, `ResolveEndOfFrame`, `DefaultExecutionOrder(1000)`)와 timeScale 0 동결 보상 연쇄(`FinishRewardChoice`)는 구조 변경 없이 저장 호출만 끼워 넣었다. 기존 255개 EditMode 테스트가 그대로 통과(아래).
- ArtSource/데스크톱 아트/기획 문서 미수정. 커밋·푸시 없음. 테스트는 모두 임시 경로(`TestSaveIsolation` + 테스트별 temp 폴더) — 실제 사용자 save.json 미사용.

## 구현한 요구사항 (08 기준)

| 08 항목 | 구현 |
|---|---|
| §5 save.json v2 필드 | `SaveData`: `schemaVersion`(2), `saveRevision`(성공 저장마다 +1, 실패 시 되돌림), `specialtyPoints`(누적 획득), `unlockedPermanentSkillIds`, `discoveredCardIds`, **`discoveredSkillIds`(신규, 커피 노드 첫 해금 시 즉시 저장)**, `discoveredMaterialVarietyIds`, 재료 3종 누적, `totalRuns/clearedRuns/failedRuns/abandonedRuns`, `bestClearSeconds`(클리어 없음 0, 최단), `activeRun`(null 또는 {runId, seed, startedUtc, checkpointGameSeconds, cocktail, tea, coffee, specialtyAwarded, resultCommitted}), `lastCommittedRunId`. activeRun 없음은 파일에 JSON `null`로 쓴다(JsonUtility 기본 인스턴스 치환). |
| §5 결과 집계 | 시작 시 totalRuns+1. 종료 시 결과 하나만 +1: 클리어→cleared, 사망·시간 초과→failed, 런 포기·타이틀로 종료·앱 정상 종료·강제종료 복구→abandoned. (06의 "실패"는 일반적 실패 결과를 뜻하며, 포기는 failed에 이중 집계하지 않는다.) |
| §5 lastCommittedRunId | `PermanentProgress.CommitRunEnd(runId, …)`는 같은 runId에 한 번만 true. GameManager의 EndRun/GiveUp/QuitToTitle/Restart/GoToTitle/OnApplicationQuit 모두 같은 `CommitRun` 경유 → 어떤 순서로 겹쳐도 1회. |
| §6 체크포인트 | 런 재료는 메모리 누적(`SetRunProgress`), 모든 저장 직전에 "activeRun 반영량과의 차이"만 전역 합계에 더하고 반영량을 같은 스냅샷에 기록(`FlushRunDeltas`). Playing 중 변화가 있으면 **게임시간 5초**마다(`CheckpointIfDue`). |
| §6 즉시 저장 | 런 시작, 첫 카드/커피 노드/품종 발견, 강화 보스 스페셜티(`AwardRunSpecialty`, 런당 상한 3 — 중복 보스 이벤트 2회 지급 없음, 종료 집계가 다시 더하지 않음), 영구 구매, 런 종료, **카드/하이브리드/커피 트리 창 진입**·선택 완료, Pause, 앱 정상 종료(`SaveIfDirty`). AddIngredient는 재료를 먼저 누적한 뒤 첫 품종 발견 저장을 해 그 픽업이 같은 저장에 포함된다. |
| §6 강제종료 복구 | 로드 시 activeRun이 남아 있으면 abandonedRuns+1, activeRun=null로 1회 저장, 체크포인트까지만 유지(미저장분 복원 없음), "이전 런은 중단되었습니다. 마지막 저장까지의 영구 진행이 유지됩니다." 알림. 복구 저장이 실패해도 메모리 캐시에서 1회만 세고 dirty 재시도. 같은 세션에 종료 경로 없이 남은 activeRun이 있으면 다음 BeginRun이 포기로 1회 집계. |
| §6 저장 방식 | 직렬화 → `.tmp`를 WriteThrough로 완전 쓰기/Flush/닫기 → `.tmp` 재읽기로 완전 JSON·리비전 검증 → 현재 원본이 **정상일 때만** `File.Replace(tmp, 원본, .bak)` / 원본 없음이면 Move(.bak 유지) / 원본 손상이면 .corrupt 사본 확보 후 백업 없이 교체(손상 원본으로 정상 .bak을 덮지 않음, 사본 확보 실패면 교체 중단). |
| §6 로드 순서 | 정상 원본 → (원본 없음/손상) 정상 .bak → 새 데이터. `.tmp`는 자동 적용 안 함. 손상 파일은 `{파일}.{UTC yyyyMMdd-HHmmss-fff}.corrupt` 복사 보존. 알림: 손상→"저장 파일을 읽지 못해 새 진행으로 시작합니다. 기존 파일은 보관했습니다.", 정상 .bak 복구→복구 사실만. |
| 완전 JSON 검증 | 이전 코드는 `{`로 시작만 보면 통과(잘린 파일 수용). 최소 JSON 구문 검사기(`JsonSyntax`)로 단일 완전 객체만 허용. 버전은 **루트 단계 키에서만** 정수로 읽음(중첩 객체 안의 schemaVersion/version로 오판 금지, 타입 불일치·루트 중복 키는 손상). |
| §6 v0/v1 이전 | 버전 없음(v0)·`"version":1`(v1)을 v2로: specialtyPoints 그대로(누적 획득), 목록 null→빈 목록·빈 값 제거·중복 1회(순서 유지)·**알 수 없는 id 보존**, 음수 카운터 0 + "일부 누적치가 음수로 손상돼 0으로 되돌렸습니다." 알림, 새 필드 기본값(런 통계 추정 생성 없음, 옛 파일의 같은 이름 쓰레기 값도 0). 알 수 없는 영구 노드 id는 진단로그에 기록, 효과/비용에서 제외. 로드만으로 원본을 바꾸지 않고 첫 저장 때 옛 정상 원본이 .bak으로 보존된다. activeRun.checkpointGameSeconds 음수/비유한·saveRevision 음수도 보정+알림. |
| §6 선행 빠진 옛 노드 | `PermanentSkillTree`가 "유효 노드"(알려진 id + 선행도 유효)만 효과·비용에 포함. 단계2만 있는 기록은 비활성으로 보존, 선행 무료 생성 없음, 화면 "비활성 · 선행 강화 확인 필요"와 상단 "이전 데이터의 선행 강화 확인 필요". 정상 구매로 복구하되, 선행 구매로 비활성 노드가 되살아나 잔액이 음수가 되는 구매는 거부(07:163 음수잔액 금지). |
| §6 음수 잔액 | 잔액<0이면 구매 잠금(`Locked`), 기존 유효 노드 효과 유지, "데이터 오류: 스페셜티 잔액이 음수라 구매가 잠겼습니다." 표시. 총량·잔액을 임의로 늘리지 않음. |
| §6 미래 schemaVersion>2 | 원본(또는 원본 없음 + 미래 .bak)을 절대 쓰지 않음 — 손상으로 취급하지 않고 .bak 폴백도 하지 않음. "새 버전 저장입니다. 기존 파일을 보존하고 임시 플레이합니다." 알림과 타이틀의 **"임시 플레이 시작" 버튼**; 누르기 전에는 어떤 파일도 쓰지 않고 **"시작" 버튼도 막고** 그 버튼으로 안내. 누른 뒤에는 같은 폴더 `guest_session.json`에만 저장. `SaveSystem.Save`도 schemaVersion>2 데이터는 아무것도 바꾸지 않고 거부, 정상 원본 + 미래 .bak이면 .bak을 덮지 않고 백업 없이 교체. |
| §6 쓰기 실패 | 메모리 진행 유지 + dirty, 실제 시간 5초 간격 자동 재시도(`SaveRetryRunner`, unscaled — Pause/결과 중에도) 또는 버튼. HUD 좌상단 "⚠ 저장하지 못했습니다. 다시 시도" 버튼, 타이틀 하단 알림 + "다시 시도" 버튼(기존 종료 확인창 유지), 영구 강화 화면 알림. 예외 상세는 로그(Warning)만. |
| §6 영구 구매 트랜잭션 | `TryPurchasePermanentSkill`: 검증 → 메모리 추가 → 저장 성공 시에만 확정. 실패 시 id 제거(비용은 목록에서 계산되므로 차감도 롤백), 결과 `SaveFailed`, 버튼은 다시 누를 수 있음, 소유하지 않은 노드 효과 없음. 런 진행 중(GameManager 살아 있고 GameOver 아님) 구매 거부. |

## 변경 파일

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Meta/SaveData.cs` | v2 스키마 + `ActiveRunRecord` |
| `Assets/_Project/Scripts/Meta/SaveSystem.cs` | 로드 분류(Missing/Valid/Invalid/Future)·이전·정리, .corrupt 보존, 원자적 저장/검증, `JsonSyntax`, 테스트 오류 주입 `FaultHookForTests`(WriteTmp/AfterTmpWritten/BeforeReplace), `LoadDetailed`/`LoadResult`. 기존 `Load(path)`·`LastLoadIssue`·`LastSaveFailed` 호환 유지(`Save`는 bool 반환) |
| `Assets/_Project/Scripts/Meta/PermanentProgress.cs` | 런 수명주기(BeginRun/SetRunProgress/CheckpointIfDue/AwardRunSpecialty/CommitRunEnd), 복구, dirty/재시도(Tick/RetrySave/SaveIfDirty), 미래 버전 보류·`AcceptGuestSession`, 트랜잭션 구매, `DiscoverSkill`. 기존 API(AddSpecialty/DiscoverCard/DiscoverMaterialVariety/AddMaterialsCollected/UnlockPermanentSkill/SetPathForTests) 유지 |
| `Assets/_Project/Scripts/Meta/SaveRetryRunner.cs` (+.meta, 신규) | 저장 실패 시에만 생기는 DontDestroyOnLoad 재시도 러너 |
| `Assets/_Project/Scripts/Cards/PermanentSkillTree.cs` | 유효 노드 계산, 비활성 옛 노드, 재활성 비용을 포함한 구매 검사 |
| `Assets/_Project/Scripts/Core/GameManager.cs` | RunId/BeginRun, 5초 체크포인트, AddIngredient 순서, 스페셜티 런 상한, DiscoverSkill, 선택창 진입·Pause 저장, `CommitRun`(EndRun/QuitToTitle/Restart/GoToTitle/OnApplicationQuit). 기존 `AddMaterialsCollected` 런 종료 호출 제거(차이 반영으로 대체) |
| `Assets/_Project/Scripts/UI/HudView.cs` | 저장 실패 알림/재시도 버튼(좌상단, 안전 여백 48) |
| `Assets/_Project/Scripts/UI/TitleView.cs` | 하단 저장 알림, "임시 플레이 시작"/"다시 시도" 버튼, 미래 버전 보호 중 시작 차단+안내(`RequestStart`, 안내는 상태 갱신에도 유지), 저장 상태(실패/보호/임시)가 바뀔 때만 알림·버튼 갱신(`PollSaveState`, Update) — 5초 자동 재시도 성공이 열린 타이틀에 바로 반영 |
| `Assets/_Project/Scripts/UI/PermanentSkillView.cs` | 트랜잭션 구매 사용, 잠금/비활성/저장 실패 알림 줄, 잔액·누적·사용 표시 |
| `Assets/_Project/Tests/EditMode/SaveSystemTests.cs` | `Load_StampsCurrentVersion…`의 단언만 `.version`→`.schemaVersion`(필드명 변경, 08:138 — 정당한 옛 버전 단언 수정). 손상 사본(.corrupt) 정리 teardown 추가. 그 외 단언 변경 없음 |
| `Assets/_Project/Tests/EditMode/SaveV2Tests.cs` (+.meta, 신규) | 40개 — 아래 |
| `Assets/_Project/Tests/EditMode/SaveV2UiTests.cs` (+.meta, 신규) | 8개 — 아래 |

## 테스트 (EditMode, 실제 실행)

실행: `Unity.exe -batchmode -projectPath <프로젝트> -runTests -testPlatform EditMode -testResults … -logFile …` (6000.3.25f1, 프로젝트 잠금 없음 확인, PlayMode 미실행).

| 회차 | 결과 | XML / 로그 |
|---|---|---|
| r1 (Title 시작 보호·UI 테스트 추가 전) | **295/295 Passed**, 실패·skip 0 (2026-10-04 00:51:29~35Z) | `Delivery/logs/editmode-results-2026-10-04-save-v2.xml`, `…editmode-log-2026-10-04-save-v2.txt` |
| r2 (Title 시작 보호·UI 테스트 6개 추가) | **컴파일 실패 — 테스트 미실행, 통과 아님.** `TitleView.cs(72,110) CS0407`: bool `RequestStart`를 버튼 UnityAction에 직접 전달. XML 없음 | `…editmode-log-2026-10-04-save-v2-r2.txt` |
| r3 (`() => RequestStart()`로 수정) | **301/301 Passed**, 실패·skip 0 (01:06:43~49Z) | `…editmode-results-2026-10-04-save-v2-r3.xml`, `…editmode-log-2026-10-04-save-v2-r3.txt` |
| **r4 (최종 코드 — Title 자동 갱신 + UI 테스트 2개 추가)** | **303/303 Passed**, 실패·skip·inconclusive 0, error CS 0 (2026-10-04 01:07:59~08:05Z) | `Delivery/logs/editmode-results-2026-10-04-save-v2-r4.xml`, `…editmode-log-2026-10-04-save-v2-r4.txt` |

303 = 기존 255(10-04 frameorder 기준, 위 1건 단언 대상 교체 외 수정 없이 통과) + SaveV2Tests 40 + SaveV2UiTests 8. 각 배치 Unity 프로세스는 결과 기록 후 code 0으로 종료(r1 PID 22780은 코디네이터도 종료 확인; r1 때 PowerShell 도구가 `Start-Process -Wait`에서 Unity 종료 뒤에도 600초 대기한 것은 도구 대기 문제였고 r2~r4는 백그라운드 실행). 신규:
- **이전(G27):** v0 진행·알 수 없는 id 보존/중복·null·빈 값 정리/음수 0+알림/통계 비생성/로드가 원본 불변; v1 `version` 필드·쓰레기 통계 무시; 첫 저장 시 옛 원본 .bak 보존·v2 파일에 unknown id 유지; 중첩 객체의 버전 키 무시; schemaVersion 타입 불일치는 손상; activeRun null 직렬화·왕복; activeRun 체크포인트/리비전 음수 보정+알림.
- **미래 보호:** 버튼 전 원본·.bak·guest 파일 모두 미기록(실패로 표시하지 않음) → 버튼 후 guest_session.json만, 원본 바이트 불변, .corrupt 없음; 루트 legacy `version:3` + 중첩 `schemaVersion:0`도 보호·직접 Save 거부; 원본 없음 + 미래 .bak 보호·실제 버전 보고; 정상 원본 + 미래 .bak에서 .bak 불변; schemaVersion 3 데이터 Save 거부·데이터 불변.
- **손상/중단(G28):** 유효 저장 JSON의 모든 접두(잘림) 거부·루트 중복 키 거부; 잘린 원본→정상 .bak 복구·복구 사실만 알림·손상 원본 그대로 .corrupt; 손상 원본 후 저장해도 정상 .bak 불변·같은 손상 내용은 1회 보존; 손상+백업 없음→새 진행+알림; 원본 없음+정상 .bak 로드; 남은 .tmp(완전/쓰레기) 미적용; 백업 직후·교체 전 중단 상태→.bak의 이전 스냅샷; 세 단계 각각 쓰기 중단 시 원본 유지·리비전 불변, 이후 정상 저장.
- **실패/롤백/재시도(G29):** 구매 저장 실패 시 해금·차감·효과 롤백·디스크 불변·dirty 아님, 재시도 구매 성공·중복 구매 거부; 음수 잔액 잠금·유효 효과 유지·총량 불변; 선행 빠진 노드 비활성·보존·무료 선행 없음·재활성 음수 구매 거부·정상 구매로 복구; 쓰기 실패 시 메모리 유지·4.9초 재시도 없음·5초마다 재시도·성공 후 깨끗하면 추가 저장 없음; 런 중 체크포인트 실패 → 메모리 유지 → 종료 집계 → 버튼 재시도로 이중 집계 없이 반영.
- **체크포인트/복구(G30 로직):** BeginRun 즉시 기록; 5초 전 미저장·변화 없으면 미저장·차이만 반영(8이지 13 아님)·activeRun 같은 스냅샷; 즉시 발견 저장에 대기 차이 포함; 강제종료(세션 재시작) 후 포기 1회·체크포인트까지만·알림·다음 실행에서 재집계 없음; 복구 저장 실패 시 세션 내 1회·5초 후 재시도 성공; 중복 종료(같은/다른 결과) 1회·스페셜티 중복 지급 없음·종료가 스페셜티 재가산 안 함; 모든 결과 6런 통계(total=합, best=최소, 실패/포기 재료 보존); 고아 activeRun 1회 포기.
- **GameManager 통합(실제 Initialize):** 시작 시 totalRuns/activeRun/seed; 첫 품종 발견 저장에 그 픽업 포함; 카드 창 진입 시 미저장 픽업 즉시 저장; 강화 보스 처치 이벤트 2회→스페셜티 3; 사망→failed 1회·timeScale 0 유지·이후 GiveUp 무시; 포기→abandoned만; 시간 초과→failed.
- **UI(SaveV2UiTests, RectTransform 명목 좌표):** 타이틀 자동 재시도 성공(`PermanentProgress.Tick`) 후 `PollSaveState`로 실패 문구·버튼 즉시 사라짐, 상태 불변이면 변화 없음; 보호 중 시작 안내가 상태 갱신 뒤에도 유지되고 버튼 전 디스크 쓰기 시도 0회; 타이틀 미래 버전—임시 플레이 버튼 표시·시작 차단+안내·클릭 후 버튼 숨김/guest 저장/원본 불변; 저장 실패—짧은 알림(예외문 없음)·다시 시도 버튼, 실패 지속 시 유지·성공 시 숨김; 두 상태 모두 알림/버튼이 메뉴·푸터와 안 겹치고 안전 여백 48 안, 두 버튼 동시 표시 없음; HUD 실패 알림은 실패 시에만·HP/게이지/정보와 안 겹침; 영구 강화—잠금/선행 확인 알림·잠금 구매 결과, 저장 실패 알림·롤백·재구매.

## 미검증 / 잔여

- **settings.json 전체 스키마·별도 원자 저장은 미구현(잔여).** 기존 PlayerPrefs 기반 설정 저장은 그대로다.
- **PlayMode 미실행**(이 기기의 배치 PlayMode 러너 행, 2026-09-25 기록 — 반복 시도·재설치 안 함). 기존 PlayMode 테스트(PermanentSkillViewTests, TitleViewTests, EnhancedBossFlowTests 등)는 **컴파일만** 확인(EditMode 실행 시 전 어셈블리 컴파일, error CS 0) — 통과로 보고하지 않는다. HUD 실패 알림의 Update 토글, 타이틀 `Update`의 `PollSaveState` 호출(메서드 자체는 EditMode 검증), `SaveRetryRunner`의 실제 프레임 구동, Pause 진입 저장, `OnApplicationQuit` 경로는 엔진 루프에서 실행해 보지 않았다(로직은 EditMode에서 같은 API로 검증).
- **실제 OS 강제종료/전원 차단 미검증.** G30의 "별도 테스트 저장 빌드"로 프로세스를 죽여 보는 검증은 하지 않았다 — 메모리 캐시를 버리고 같은 경로를 다시 로드하는 방식으로 로직만 검증. 강제 OS 종료까지 완전 보존한다고 주장하지 않는다(08:179). WriteThrough/Flush(true)와 File.Replace의 원자성은 OS/파일시스템 보장에 기대며 NTFS 외 환경은 미확인.
- 실제 렌더 캡처·720p·한글 줄바꿈·색 대비 육안 미확인. 배치는 1920×1080 명목 좌표 계산만. 기존 화면 요소끼리의 겹침(예: 타이틀 제목/부제, HUD 상태 문구/찻잎 바)은 이번 범위 밖이라 검사·수정하지 않았다.
- 창 닫기(OS X 버튼) 시 "저장 재시도 / 저장 없이 종료 / 취소"(08:178) 가로채기는 기존 TitleView 주석대로 **미구현(잔여)** — 타이틀 "종료" 버튼 경로만 있다. 앱 정상 종료 시 진행 중 런은 OnApplicationQuit에서 포기 집계+저장 시도.
- 미래 버전 임시 플레이는 매 실행 새 진행으로 시작한다(이전 guest_session.json을 이어 읽지 않음 — "임시 플레이" 해석). 
- CharacterSelectView의 기존 손상/실패 배너는 그대로이며 v2 알림 문구를 함께 보여 준다(새 버튼 없음).
- 도감 화면의 커피 노드 "최초 획득" 표시는 데이터(`discoveredSkillIds`)만 추가했고 CardCodexView 표시 연결은 하지 않았다.
- 진단로그는 Unity 로그(`Debug.Log [Save]`)에 경로·출처·이전·unknown id를 남긴다. README 경로 기록은 하지 않았다.
