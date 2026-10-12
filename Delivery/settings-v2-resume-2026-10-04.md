# 설정 v2(settings.json) · 도감 커피 노드 — 2026-10-04

Orca task `task_66b2e0db2563` / dispatch `ctx_c101afa28f2b` (Run `run_b69bb3be0912`). 대상 `C:/Users/user/Documents/Projects/CocktailCoffeeTea`, Unity 6000.3.25f1(재설치·변경 없음).
입력: 기획 05, 07 §7(도감), 08 §4/§5(설정·저장), 10, 11, 17(저장 v2 기록). 프로젝트에 AGENTS.md 없음(확인), README는 없어서 이번에 새로 만듦.

## 보존

- 직전 save v2 작업(EditMode 303/303)과 기존 미커밋 변경 전부 유지. 수정 전 사본: 세션 scratchpad `backup-settings-v2/`(SettingsView, InputSettings, FirstRunTutorial, AudioBus, GameBootstrap, CardCodexView, SaveRetryRunner, EditMode/PlayMode TestSaveIsolation, InputSettingsTests, PlayMode SettingsViewTests·FirstRunTutorialTests).
- `SaveSystem`/`PermanentProgress`(진행 저장)는 수정하지 않았다 — 공용으로 쓴 것은 기존 공개 `JsonSyntax`(완전 JSON·루트 키 검사) 하나뿐. 설정은 자체 파일 안전 로직·dirty·오류 주입 훅을 따로 둔다. 재시도 러너(`SaveRetryRunner`)만 공유하며 각자의 `Tick`을 따로 부른다.
- 아트 원본·기획 문서 미수정, 커밋·푸시 없음. 실제 사용자 save/settings/PlayerPrefs/화면은 테스트에서 읽거나 쓰지 않는다(아래 격리).

## 구현

| 08 항목 | 구현 |
|---|---|
| §5 settings.json v2 | `SettingsData`: `schemaVersion` 2, `masterVolume/musicVolume/sfxVolume` 0..100 정수(80/60/80), `width/height`(1920×1080), `windowMode`("borderless" 기본/"windowed"), `vsync`(true), `movementScheme`("keyboard"/"mouse"), `shakePercent`(50), `effectsLevel`("normal"/"low"), `damageNumbers`(true), `tutorialSeen`(false). 경로 `Application.persistentDataPath/settings.json`. |
| 단일 설정 서비스 | `SettingsService`가 로드·정리·이전·저장·재시도·적용을 담당. `SettingsView`, `InputSettings`, `FirstRunTutorial`, `AudioBus`(값 적용 대상), 부트(`GameBootstrap.AutoStart`)가 모두 이 경로를 쓴다. 기존 공개 API(`InputSettings.Scheme/Toggle`, `FirstRunTutorial.Seen/MarkSeen/ResetForTests`, `SettingsView.SetMasterVolume(0..1)` 등)는 유지. |
| 부트 적용 | `GameBootstrap.AutoStart`가 타이틀 UI를 열기 전에 `SettingsService.ApplyAtBoot()` — 오디오(정수/100 → AudioBus), 해상도·표시모드, VSync. 입력·튜토리얼은 Current를 직접 읽어 다음 실행에도 유지. (이전에는 SettingsView가 만들어질 때만 PlayerPrefs를 적용했다.) |
| 구형 PlayerPrefs 이전 | `cct_vol_master/music/sfx`(0..1 float → 반올림 정수, 비유한·범위 정리), `cct_fullscreen`(0=창), `cct_vsync`, `cct_movement_scheme`, `cct_tutorial_seen`. **settings.json과 .bak이 모두 없을 때만** 이전, 구형 키는 지우지 않음. 로드는 읽기 전용이라 이전값은 사용자가 처음 설정을 바꿀 때 파일에 기록(그 전에는 매 실행 같은 값을 다시 이전). 파일이 손상돼 있으면 이전하지 않고 기본값+알림. 실제 PlayerPrefs 접근은 `ILegacySettingsStore`(기본 `PlayerPrefsLegacySettingsStore`) 뒤로 숨겨 테스트는 메모리 저장소를 주입. |
| 파일 우선·정리 | JSON이 있으면 기준(구형 값 무시). 필드별: 누락/null/숫자 아님/비유한 → 기본값, 소수 → 반올림, 음량·흔들림 범위 밖 → 0/100으로 clamp, 해상도 무효(640..7680×360..4320 밖) → 기본 쌍, 잘못된 열거값(대소문자 무관 비교)·불리언 아님 → 기본값. 진단로그 `[Settings]`. 루트 `schemaVersion`이 정수가 아니거나 루트 중복 키/불완전 JSON이면 손상. schemaVersion 없음은 v2로 정리. |
| 원자 저장·복구 | `settings.json.tmp` WriteThrough 완전 쓰기 → 재파싱 검증 → 원본이 정상일 때만 `.bak`으로 `File.Replace`, 원본 없음이면 Move(.bak 유지), 손상 원본이면 같은 내용 `.corrupt` 사본 확보 후 백업 없이 교체(확보 실패 시 교체 중단). 로드: 정상 원본 → 정상 .bak("설정 파일을 읽지 못해 백업에서 복구했습니다.") → (파일 없음) 구형 이전/기본값, (손상) 기본값+"설정 파일을 읽지 못해 기본 설정으로 시작합니다. 기존 파일은 보관했습니다.". `.tmp`는 자동 적용 안 함. |
| 미래 schema | schemaVersion>2(원본, 또는 원본 없음+.bak): 알 수 있는 필드를 정리해 **이번 실행 메모리에서만** 사용·적용, 원본/.bak은 절대 쓰지 않음(쓰기 실패로 표시하지도 않음). 알림 "새 버전 설정 파일입니다. 기존 파일을 보존하고 이번 실행에서만 설정을 적용합니다.". `SettingsService.Save`도 schemaVersion>2 데이터는 객체를 바꾸지 않고 거부, 미래 .bak은 덮지 않음. |
| 쓰기 실패·재시도 | 메모리 설정·적용 유지 + dirty. 설정 화면에 짧은 "설정을 저장하지 못했습니다. 다시 시도" + "다시 시도" 버튼(예외문은 로그 Warning만), 실제 시간 5초 간격 자동 재시도(unscaled, `SaveRetryRunner`→`SettingsService.Tick`). 진행 저장 실패 표시와 섞이지 않음. |
| 해상도·표시모드 미리보기 | `BeginDisplayPreview`가 화면에만 적용하고 settings에는 기록하지 않음. "유지" → `ConfirmDisplayPreview` 저장. 10초 타임아웃(실제 시간)/되돌리기/Esc·닫기(`Hide`)/미리보기 중 다시 미리보기 → 기준은 항상 **직전 확정값**. 미리보기 중 음량·입력 변경은 따로 저장되며 그 저장에 미리보기 해상도가 섞이지 않고, 되돌리기도 그 변경을 되돌리지 않는다. 표시모드 토글도 같은 확인을 거친다(이전엔 즉시 저장). 크기는 유효 범위 검사 후 모니터 맞춤(모니터보다 크면 1280×720)으로 적용, 작은 모니터에서 확정값 1920×1080은 파일에 보존하고 화면·UI 표시는 실제 적용 크기. |
| VSync | 켜짐 → `vSyncCount=1`, `targetFrameRate=-1`; 꺼짐 → `vSyncCount=0`, `targetFrameRate=60`. 라벨 "꺼짐 (60fps 제한)". |
| 음량 | 슬라이더 0..100 정수(`wholeNumbers`), 즉시 AudioBus(=정수/100, 기존 0..1 런타임 오디오 그대로) 적용·저장. |
| 기본값 복원 | 설정 화면 "기본값 복원": 음량·조작·VSync·흔들림·효과·피해 숫자를 기본값으로, 즉시 적용·저장. save.json(진행/영구 강화/발견)은 건드리지 않음. 해상도/표시모드는 확인 없이 바꾸지 않으므로 기본 화면값을 10초 확인 미리보기로 띄움. `tutorialSeen`은 사용자 설정이 아니라 첫 안내 표시 기록이라 유지(판단 — 기획 확인 필요 시 코디네이터 결정). |
| 효과 없는 항목 | 화면 흔들림·효과 강도·피해 숫자는 실제 게임 효과가 아직 없어 **설정 화면 항목을 만들지 않았다**. 스키마·정리·저장과 `SetShakePercent/SetEffectsLevel/SetDamageNumbers` API만 있다 — **런타임 효과 미구현(후속).** |
| 도감 커피 노드 | `CardCodexView`에 커피 15노드 섹션(하이브리드 열 아래, 탭 필터와 무관하게 항상 표시). 07:169대로 처음부터 이름·갈래/단계·효과·선행을 보여 주고, `discoveredSkillIds` 기준 "최초 획득 기록 있음"/"아직 획득 기록 없음"으로만 구분(카드 미발견 "???", 영구 "미해금"과 다른 문구). 상단 집계 "커피 노드 최초 획득: N / 15". 저장의 알 수 없는 id는 표시·집계에서 제외하고 원본 목록은 그대로 보존. 영구 강화 열도 효과·비용과 같은 기준(`PermanentSkillTree`)으로 "해금됨/비활성(선행 확인 필요)/미해금" 표시. 도감 열 폭 380/간격 350이라 이웃 열이 30px 겹치던 배치를 폭 350/간격 360으로 최소 조정(4열 1430 ≤ 뷰포트 1450). |
| README | 프로젝트 루트 `README.md` 신규: 런타임 `persistentDataPath`(Windows 기본 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea\`)와 로그에 찍히는 실제 경로, save/settings/guest 파일 분리, 원자 저장·이전 규칙, 잔여 범위. |

## 테스트 격리

- EditMode·PlayMode `TestSaveIsolation`([SetUpFixture], 네임스페이스 `CCT.Tests` 전체)이 스위트 시작 시 `SettingsService.SetForTests(임시 settings 경로, 빈 MemoryLegacySettingsStore, 화면 어댑터)`를 주입하고 끝나면 실제 기본값으로 복원·임시 파일 삭제. EditMode는 `MemoryDisplayAdapter`(실제 창/QualitySettings/targetFrameRate 불변), PlayMode는 기존 화면 토글 테스트 의미를 유지하려 `UnityDisplayAdapter`(PlayMode는 실행하지 않음). 기존 303개 중 TitleView/SettingsView/InputSettings를 쓰는 테스트도 이 격리 경로를 탄다. `FirstRunTutorial.ResetForTests`는 이제 주입된 설정만 바꾸며 실제 PlayerPrefs `DeleteKey`를 하지 않는다.
- 신규 테스트는 테스트별 임시 폴더 + 자체 메모리 저장소/화면 어댑터.

## 테스트 결과 (EditMode, 실제 실행)

실행: Unity 6000.3.25f1 배치 EditMode(백그라운드 실행, XML·프로세스 종료 확인, 자신이 띄운 PID만 대상).

| 회차 | 결과 | XML / 로그 |
|---|---|---|
| r1 | **328/329, 실패 1** (01:35:15~26Z) — `SettingsV2ViewTests.NewButtonsAndNotice_DoNotOverlapRows_AndStayInSafeArea`: 새 알림 줄(y150..190)이 마지막 행 "조작 안내"(아래 182)와 실제로 겹침. 통과로 집계하지 않음. 기존 303개는 이 회차에서도 통과 | `Delivery/logs/editmode-results-2026-10-04-settings-v2.xml`, `…editmode-log-2026-10-04-settings-v2.txt` |
| **r2 (최종)** | **329/329 Passed**, 실패·skip·inconclusive 0, error CS 0 (2026-10-04 01:36:42~53Z). 알림 줄을 y145..177로 옮기고, 도감 열 폭/간격 350/360 조정 후 커피 섹션의 이웃 열 겹침 검사(전체 열)를 복구한 상태 | `Delivery/logs/editmode-results-2026-10-04-settings-v2-r2.xml`, `…editmode-log-2026-10-04-settings-v2-r2.txt` |

329 = 기존 303(save v2 r4 기준, 단언 수정 없음 — InputSettingsTests 등은 격리된 임시 설정 경로로 그대로 통과) + 신규 26(`SettingsV2Tests.cs`):
- 로드(6): 파일·구형 없음 → 기본값·부트 적용(음량/1920×1080 테두리 없음/VSync·-1fps)·파일 미기록; 구형 PlayerPrefs 이전(반올림·범위 정리, 창 모드, VSync 꺼짐=60fps, 마우스, 튜토리얼) → 첫 변경 시 파일 기록·구형 키 유지·다음 실행은 파일 기준; 파일이 구형보다 우선(파일에 없는 필드는 구형이 아니라 기본값); null/범위/비유한(1e999)/잘못된 열거·불리언/중첩 키 무시 정리; 소수 반올림·schemaVersion 없음=v2; schemaVersion 타입 불일치는 손상 → 기본값+알림, 구형 이전 안 함.
- 파일 안전(7): 첫 저장 백업 없음·둘째부터 직전 정상 .bak·.tmp 미적용; 잘린 원본 → 정상 .bak 복구·알림·손상 사본 그대로·이후 저장이 정상 .bak 보존·사본 1회; 손상+백업 없음 → 기본값+알림; 미래 원본 → 메모리 전용 적용(AudioBus 반영)·원본 바이트/백업 불변·실패 표시 없음; 원본 없음+미래 .bak 보호; 직접 Save가 미래 데이터 거부(객체 불변)·미래 원본 미교체; 쓰기 실패 → 메모리·적용 유지·dirty·4.9초 재시도 없음·5초 재시도·성공 후 정리.
- 독립(2): 설정 실패가 진행 저장 상태에 번지지 않고 진행 오류 주입이 설정 저장을 막지 않으며 설정 성공이 진행 실패 표시를 지우지 않음; 기본값 복원이 설정만 초기화(tutorialSeen·확정 해상도 유지)·save.json 바이트 불변.
- 미리보기(3): 확인 전 미기록·미리보기 중 음량/입력 저장에 미리보기 해상도 미혼입·되돌리기가 그 변경을 되돌리지 않음; 반복 미리보기도 직전 확정값으로 복원·유지 시 저장·다음 실행 적용; 작은 모니터(1600×900) 적용·UI 표시 1280×720·확정값 보존·미리보기 맞춤·무효 크기 처리.
- 설정 화면(6, 실제 SettingsView 위젯): 정수 음량 저장·AudioBus 호환; 해상도 10초 타임아웃 복원·유지 저장·표시모드 미리보기 후 닫기(Esc 경로) 복원; VSync 꺼짐 60fps·저장; 저장 실패 짧은 알림·다시 시도 버튼·재시도로 해제; 기본값 복원 버튼 + 기본 화면값 확인 미리보기; 알림/기본값 복원/닫기/다시 시도가 행과 안 겹치고 안전 여백 48 안(명목 좌표).
- 도감(2): 커피 15노드, 최초 획득/미획득 문구·이름 상시 표시(??? 아님)·영구 "미해금"과 구분·선행 표시·unknown id 집계 제외·원본 id(커피/영구) 보존·선행 빠진 영구 노드 "비활성"·카드 미발견 "???"; 커피 섹션 16개 RectTransform이 같은 열·이웃 열 어떤 요소와도 겹치지 않고 콘텐츠 세로·뷰포트 가로 안(명목 좌표 — 실제 렌더 아님).

## 미검증 / 잔여

- **PlayMode 미실행**(이 기기 배치 PlayMode 러너 행 기록 — 반복·재설치 안 함). PlayMode `SettingsViewTests`(표시모드 토글이 이제 미리보기 경로), `FirstRunTutorialTests`, `TitleViewTests` 등은 **컴파일만** 확인, 통과로 보고하지 않는다. 실제 `GameBootstrap.AutoStart` 부트 순서, `SaveRetryRunner`의 프레임 구동, `SettingsView.Update`의 카운트다운/자동 재시도 반영은 엔진 루프에서 돌려 보지 않았다(같은 메서드를 EditMode에서 직접 호출해 검증).
- 실제 `Screen.SetResolution`/`QualitySettings`/`Application.targetFrameRate`/PlayerPrefs 동작은 어댑터 뒤라 실기 미검증(에디터 Play에서는 해상도 변경이 반영되지 않음). 720p/1080p 실제 렌더·한글 줄바꿈 육안 확인 없음 — UI 배치는 1920×1080 명목 좌표 계산만. 도감 열 폭 축소(380→350)로 긴 카드 이름이 줄바꿈될 수 있는지는 육안 미확인.
- 화면 흔들림·효과 강도·피해 숫자 런타임 효과 및 설정 UI 미구현(스키마/저장만).
- OS 창 닫기(X) 미저장 확인창, 실제 OS 강제종료·별도 빌드 검증 미완료(이전 보고와 동일).
- 구형 PlayerPrefs 이전값은 첫 설정 변경 전까지 파일로 기록하지 않는다(설계 선택: 부트 로드를 읽기 전용으로 유지). 그 사이 구형 키는 아무도 쓰지 않으므로 매 실행 같은 값이 된다.
