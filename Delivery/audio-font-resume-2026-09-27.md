# 09 I3 재개 — 폰트 + 오디오 (2026-09-27, 4차 — 아트 게이트 실제 연결·ui_focus 배선·전투SFX 명시적 억제)

**범위**: [[09_완성판 아트 사운드 명세 v1.0]] I3 — Noto Sans CJK KR Regular 동봉·적용, 결정적 합성으로
22 SFX + 3 BGM 제작, 형식/길이/피크/루프 검사, Master/Music/SFX 그룹 분리·이벤트·전환·동결(Pause 덕킹) 연결.

이 문서는 [[15_제작 재개 기록 2026-09-27]]에서 코디네이터가 지적한 항목들을 여러 차례에 걸쳐 실제로
고친 뒤 다시 측정·테스트·빌드한 결과다(1~3차: 폰트/오디오 버그, AudioService 경합, 실제 AudioMixer.
**4차(이 갱신)**: BuildRelease 아트 v1 매니페스트 게이트를 실제로 연결하고 의도한 실패까지 실행·기록,
ui_focus를 실제 Button hover/select 이벤트에 배선, Pause/보상창/결과 화면의 전투SFX 억제를 명시적
플래그로 전환하고 보상·결과 음은 항상 보존되게 수정, AudioBus의 낡은 주석 정정, BGM 측정치가 "파일
자체 피크"이지 "런타임 합산 피크"가 아님을 명시). **전체 게임 완료를 주장하지 않는다.** I3 범위의 실제
산출물과 남은 제한만 기록한다.

## 1. 폰트 (변경 없음, 재확인만)

- **파일**: `Assets/_Project/Resources/Fonts/NotoSansKR-Regular.otf` (원본 파일명 `NotoSansCJKkr-Regular.otf`, Resources 로드 경로를 위해 리네임만 함)
- **출처**: https://github.com/notofonts/noto-cjk , 경로 `Sans/OTF/Korean/NotoSansCJKkr-Regular.otf`
- **크기**: 16,433,112 bytes — 이번 세션에 `sha256sum`으로 재검증: `6bcb2a0703aa137e874fc2dffa85f6c21ba9a67fa329e81b8c801663af7e992a` (기존 기록과 일치, 파일 변경 없음)
- **라이선스**: SIL OFL 1.1 — `Assets/_Project/Resources/Fonts/LICENSE_NotoSansCJK.txt`에 원문 동봉, 이번 세션에 헤더까지 재확인
- **THIRD_PARTY_NOTICES**: `Delivery/THIRD_PARTY_NOTICES.md`
- **런타임 연결**: `UiFactory.Font`가 `Resources.Load<Font>("Fonts/NotoSansKR-Regular")`로 로드(legacy uGUI `Text` 컴포넌트용 `UnityEngine.Font` — 이 프로젝트는 TextMeshPro를 안 쓰므로 타입 불일치 없음). 못 찾으면 OS 폰트로 대체 + 경고.
- **BuildRelease 검사 경로 일치 확인**: `BuildRelease.FontFolder`="Assets/_Project/Resources/Fonts", `FontFileStem`="NotoSansKR-Regular" — `UiFactory`가 읽는 `Resources.Load<Font>("Fonts/NotoSansKR-Regular")`와 파일시스템상 정확히 같은 대상이다. 코디네이터가 지적한 "런타임 폰트 경로와 검사 경로 일치"는 코드 대조 결과 **이미 일치**했음을 확인(추가 수정 불필요, 아래 4절 실제 빌드로도 통과 확인).
- **검증**: EditMode 배치 실제 재실행(아래 4절), 16MB OTF 임포트 정상. 한글 렌더 육안 확인은 PlayMode가 막혀있어 여전히 **미검증**.

## 2. 오디오 — 25개 WAV (이번 세션에 3개 버그 수정 후 재생성)

### 코디네이터 지적 3건 — generate_audio.py 수정 내역

1. **ui_focus 볼륨 감소가 정규화로 상쇄됨(수정 완료)**: 기존엔 `sfx_ui_focus()`가 진폭에 `*0.5`를 곱해 "낮은 크기"를 표현했지만, `main()`이 모든 SFX에 `normalize_peak(samples, -6.0)`을 일괄 적용해 그 차이를 지워버렸다(어차피 -6dBFS로 재정규화되므로 원래 진폭이 뭐였든 결과가 같았음). **수정**: `SFX_TARGET_DBFS = {"ui_focus": -12.0}` 딕셔너리를 신규 도입해 ui_focus만 다른 SFX(-6dBFS)보다 6dB 낮은 목표치로 정규화. 재생성 후 측정값: ui_focus만 유일하게 peak -12.00dBFS, 나머지 21개는 -6.00dBFS.
2. **SFX 공통 끝 fade 누락(수정 완료)**: 09:71 "앞뒤2~5ms fade로 클릭 제거"가 톤 시퀀스 계열(levelup/clear 등)에만 적용돼 있고 노이즈/사인 기반 SFX는 attack+exp감쇠 엔벨로프에만 의존했다(이론상 끝에서 0에 가깝지만 정확히 0은 아님). **수정**: 정규화 직전에 `fade_io(samples, 0.003)`(3ms)를 모든 SFX 공통으로 한 번 더 적용.
3. **BGM 메뉴 곡의 마지막 마디 처리(수정 완료)**: "마지막 마디 마지막박 멜로디 쉼으로 루프 숨을 준다"가 run/boss 곡에서는 진짜 "마지막 1박"만 쉬도록 구현돼 있었는데, menu 곡만 마지막 마디의 3·4박 멜로디 음(2박 전체)을 통째로 스킵하고 있었다 — "마지막 박" 쉼이 아니라 "마지막 2박" 쉼이 되는 불일치. **수정**: 마지막 마디에서도 3박째 음은 그대로 재생하되 길이만 1박으로 줄여(4박째만 남기고 쉼) run/boss와 같은 규칙으로 통일.

### measure_audio.py 강화 내역 (코디네이터 지적 4건)

1. **기대 ID 집합 검사(신규)**: `EXPECTED_IDS`(22 SFX + 3 BGM 이름 집합)와 실제 파일명을 대조해 누락/여분 ID를 별도 issue로 보고.
2. **길이 검사(신규)**: 09 §4 표의 SFX 고정 길이(±0.03s 허용) 및 BGM은 BPM·마디 수에서 산출한 이론 루프 길이(±0.01s 허용)와 대조.
3. **SFX -6dBFS 기준 분리(수정)**: 기존엔 SFX·BGM 모두 "-1dBFS 이하"라는 느슨한 기준 하나만 썼다. **수정**: SFX는 -6dBFS(+0.1dB 허용오차) 이하, BGM(최종 믹스)만 -1dBFS 이하로 분리 — 09:71의 "효과별"과 "최종 믹스"가 서로 다른 기준임을 반영.
4. **실패 종료 코드(신규)**: 문제가 있으면 `main()`이 1을 반환하고 `sys.exit(main())`으로 실제 비정상 종료 — 이전엔 문제를 콘솔에 출력만 하고 항상 exit 0이라 CI/빌드 게이트로 못 썼다.
5. **루프 실청 여부 구분(신규)**: 기존 `loopSeamJump`(첫/끝 샘플 값 차이)만으로는 "값은 비슷한데 파형 기울기가 튀어서 실제로는 클릭으로 들리는" 경우를 못 잡는다. **수정**: `loopSlopeJump`(접합부 앞뒤 1차 차분의 불연속)를 추가 계산해 별도 임계값(0.35)으로 검사.

### 재생성·재측정 실행 결과 (이번 세션에 실제로 실행)

```
python Tools/Audio/generate_audio.py   # 25개 파일 재생성
python Tools/Audio/measure_audio.py    # 종료 코드 0
```

| ID | 채널 | 길이(초) | 피크(dBFS) | seam | slope |
|---|---|---:|---:|---:|---:|
| ui_focus | mono | 0.050 | **-12.00**(수정 전 -6.00) | 0.0000 | 0.0000 |
| ui_confirm | mono | 0.120 | -6.00 | 0.0000 | 0.0000 |
| ui_cancel | mono | 0.120 | -6.00 | 0.0000 | 0.0000 |
| ui_denied | mono | 0.160 | -6.00 | 0.0000 | 0.0000 |
| swing_rapier | mono | 0.120 | -6.00 | 0.0000 | 0.0000 |
| swing_sword | mono | 0.160 | -6.00 | 0.0000 | 0.0000 |
| shot_gun | mono | 0.130 | -6.00 | 0.0000 | 0.0000 |
| shot_bow | mono | 0.140 | -6.00 | 0.0000 | 0.0000 |
| shot_hybrid | mono | 0.180 | -6.00 | 0.0000 | 0.0000 |
| hit_enemy | mono | 0.080 | -6.00 | 0.0000 | 0.0000 |
| hit_player | mono | 0.220 | -6.00 | 0.0000 | 0.0000 |
| enemy_defeat | mono | 0.180 | -6.00 | 0.0000 | 0.0000 |
| pickup | mono | 0.080 | -6.00 | 0.0000 | 0.0000 |
| levelup | mono | 0.450 | -6.00 | 0.0000 | 0.0000 |
| skill_unlock | mono | 0.360 | -6.00 | 0.0000 | 0.0000 |
| hybrid_unlock | mono | 0.600 | -6.00 | 0.0000 | 0.0000 |
| boss_warning | mono | 0.500 | -6.00 | 0.0000 | 0.0000 |
| explosion | mono | 0.350 | -6.00 | 0.0000 | 0.0000 |
| steam | mono | 0.800 | -6.00 | 0.0000 | 0.0000 |
| spell | mono | 0.300 | -6.00 | 0.0000 | 0.0000 |
| clear | mono | 1.200 | -6.00 | 0.0000 | 0.0000 |
| defeat | mono | 1.000 | -6.00 | 0.0000 | 0.0000 |
| bgm_menu | stereo | 42.667 | -12.15 | 0.0000 | 0.0003 |
| bgm_run | stereo | 68.571 | -11.71 | 0.0000 | 0.0003 |
| bgm_boss | stereo | 60.000 | -11.73 | 0.0000 | 0.0003 |

전부 48kHz/16bit PCM. **ID 25/25 일치, 길이 전부 허용오차 내, SFX 전부 -6dBFS(ui_focus만 의도적으로 -12dBFS), BGM 전부 -1dBFS 이하, 루프 seam/slope 전부 임계값 이내. `measure_audio.py` 종료 코드 0(문제 0건).**
리포트: `Assets/_Project/Resources/Audio/audio_measurements.json`(measure_audio.py가 매 실행마다 덮어씀).

**중요한 구분(코디네이터 지적, 4차 세션에 명시): 위 BGM 피크는 "파일 자체의 피크"이지 "런타임 합산
피크"가 아니다.** `measure_audio.py`가 재는 −12.15/−11.71/−11.73dBFS는 `bgm_menu`/`bgm_run`/`bgm_boss`
각 WAV 파일 하나를 독립적으로 열어서 잰 값이다(그 파일 내부의 멜로디+베이스+코드+드럼은 이미 합쳐진
상태). 하지만 실제 게임 실행 중에는 같은 Master 출력에 **BGM 위에 동시에 여러 SFX 보이스(최대 24개
폴리포니)가 함께 섞인다** — 그 "지금 스피커로 실제로 나가는 합산 결과"의 피크는 이 25개 WAV 파일
어디에도 기록돼 있지 않고, Unity AudioMixer가 런타임에 실시간으로 만드는 값이라 오프라인 스크립트로는
측정할 수 없다(에디터에서 믹서 창의 VU 미터를 보거나, PlayMode에서 실제로 들어봐야 함). 09:71의
"최종 믹스 peak −1dBFS 이하"를 이 25개 파일에 대해서는 충족했다고 확인했지만, **"게임을 실제로
플레이할 때 스피커에서 나오는 합산 신호의 피크"는 별개 항목이며 미검증이다.**

## 3. 런타임 연결 — AudioService/GameManager 수정 (코디네이터 3차례 재검토 반영)

### 3-1. 실제 Unity AudioMixer(Master/Music/SFX) — 재검토 후 실제로 만듦

1차 보고에선 "`.mixer`를 코드로 만드는 공식 public API가 없다"는 이유로 `AudioBus` 코드 버스로
대체했었다. 코디네이터가 "코드버스 대체는 명세 미충족"이라고 재차 지적해 리플렉션으로 실제 API 표면을
덤프해본 결과 — `UnityEditor.Audio.AudioMixerController`/`AudioMixerGroupController` **클래스 선언**은
internal이지만 `CreateMixerControllerAtPath`/`CreateNewGroup`/`AddChildToParent`/`masterGroup`/
`children` 등 필요한 멤버는 전부 **public**이었다(6000.3.25f1 기준 실측). 리플렉션으로 이 public
멤버들만 호출하면 안전하게 실제 에셋을 만들 수 있음을 확인하고 구현했다.

- **신규**: `Assets/_Project/Scripts/Editor/AudioMixerBuilder.cs` — `CCT/Build Audio Mixer (Master Music SFX)` 메뉴.
- **산출물**: `Assets/_Project/Resources/Audio/CCTMixer.mixer` — **실제 Master(루트)/Music/SFX 3그룹**.
  `-executeMethod`로 실행 후 `AudioMixer.FindMatchingGroups("Music"/"SFX")`(공개 런타임 API)로 재로드해
  각각 그룹이 1개씩 실제로 조회되는지까지 검증했다(리플렉션 호출이 예외 없이 끝난 것과 "진짜 쓸 수 있는
  그룹이 생겼다"는 다른 문제라서 별도로 확인).
- **런타임 연결**: `AudioService.BuildVoicePool()`이 `Resources.Load<AudioMixer>("Audio/CCTMixer")`로 로드해
  SFX 보이스 24개는 SFX 그룹, `musicA`/`musicB`는 Music 그룹으로 `outputAudioMixerGroup`을 라우팅한다.
  볼륨 실시간 계산(마스터/BGM/SFX 슬라이더, Pause 덕킹)은 여전히 `AudioBus`가 `AudioSource.volume`에
  직접 곱하는 방식이다 — 믹서 그룹 페이더는 0dB(단위 이득)로 둬서 그 값을 그대로 통과시킨다. 즉 **그룹
  분리·라우팅은 실제 AudioMixer**, 볼륨 숫자 제어는 기존 코드가 담당하는 조합이다(08 설정 화면이 생기면
  같은 구조 위에서 슬라이더만 연결하면 됨).
- **BuildRelease 연결**: `.mixer` 에셋 존재 여부도 빌드 전 필수조건에 추가(`Assets/_Project/Resources/Audio/CCTMixer.mixer` 없으면 명시적 실패).
- **한계**: `AddGroupToCurrentView`(에디터 창에 그룹을 보여주는 순수 UI 편의 기능)는 배치모드에서
  `IndexOutOfRangeException`을 던져 호출을 생략했다 — 그룹 생성·부모연결·라우팅 기능과 무관하고, 실제
  에디터에서 믹서 창을 한 번 열면 정상적으로 보인다(Unity가 처음 여는 시점에 view를 만들어줌).

### 3-2. AudioService 코루틴 경합 3건 (재검토 후 근본 원인까지 수정)

1. **결과 fade와 Update 볼륨 복원 경합(수정)**: `ResultSequence`가 `musicFadeRoutine`에 자기 자신을 등록하지 않아 `Update()`가 매 프레임 볼륨을 되돌려 페이드를 무효화했다. `ResultSequence`도 크로스페이드와 같은 `musicFadeRoutine` 핸들을 공유하도록 수정.
2. **빠른 재시작이 여전히 결과 코루틴을 못 끄는 문제(1차 수정 불완전 — 코디네이터가 재검토로 발견, 2차 수정 완료)**: `SetMusicState`는 `currentMusicState == state`면 `CrossfadeTo` 호출 전에 그냥 return한다. `ResultSequence`는 fade가 끝나야 `currentMusicState = None`을 설정했으므로, 결과 시퀀스 첫 0.5초 안에 **같은 상태**(예: Run 종료 직후 Run 재시작)로 새 런이 시작되면 `SetMusicState`의 동일성 검사에 막혀 `CrossfadeTo`가 아예 안 불리고, `musicFadeRoutine`도 안 바뀌어 결과 시퀀스가 끝까지 돌아 나중에 Menu로 덮어썼다. **수정**: `PlayResultStingerThenReturnToMenu`가 코루틴을 시작하기 **직전**에 `currentMusicState = MusicState.None`을 미리 설정 — 결과 시퀀스 진행 중 어떤 상태로 재진입해도(같은 상태였든 아니든) "다른 상태 요청"으로 인식돼 `CrossfadeTo`가 정상 호출되고, 공유 `musicFadeRoutine` 덕분에 결과 시퀀스가 취소된다.
3. **크로스페이드 도중 결과 진입 시 반대쪽 소스 미정리(코디네이터가 재검토로 발견, 수정 완료)**: 크로스페이드 중(예: Run→Boss 전환 중간) 결과 시퀀스가 끼어들면, `musicFadeRoutine` 공유로 크로스페이드 코루틴 자체는 취소되지만 `CrossfadeRoutine`이 관리하던 두 소스 중 `ResultSequence`가 다루지 않는 쪽(반대편 소스)이 중간 볼륨으로 계속 재생될 수 있었다. **수정**: `ResultSequence` 시작 시 `active`(자신이 페이드시킬 소스)의 반대쪽도 즉시 `Stop()`+`volume=0`으로 강제 정리.
4. **Pause 시 이미 재생 중인 전투 SFX 미중단(수정)**: `AudioService.StopCombatSfx()` 신규 — UI 음 제외 활성 보이스 즉시 `Stop()`.
5. **덕킹/전투SFX 차단 범위가 Pause에만 적용됨(코디네이터가 재검토로 발견, 수정 완료)**: 09:139 "Pause/**보상창**은 음악 계속 재생하되 Music-8dB, 전투SFX 중단"의 "보상창"은 LevelUp(카드 선택)/SkillSelect(커피 스킬)/HybridOffer(하이브리드 해금)도 포함하는데, 1차 구현은 `OpenPause`에만 덕킹+SFX 차단을 걸었다. **수정**: `GameManager.EnterFrozenAudio()`/`ExitFrozenAudioIfClear()` 헬퍼 신규 — `BeginLevelUp`/`BeginHybridOffer`/`BeginSkillSelect`/`OpenPause` 진입 시 덕킹+전투SFX 중단, `ChooseCard`/`ChooseHybrid`/`ChooseSkill`/`ClosePause` 종료 시 (IsPaused 오버레이가 남아있지 않을 때만) 해제 — Pause가 보상창 위에 겹쳐 열린 "오버레이 스택" 상태에서 보상창만 먼저 닫혀도 Pause가 남아있으면 덕킹을 계속 유지한다.

### 3-3. ui_focus — 4차 세션에 실제로 배선함 (기존 조사는 "전체 키보드 내비게이션 시스템" 기준이었음)

3차 세션엔 "방향키/Tab으로 하이라이트가 옮겨 다니는 전체 내비게이션 시스템"이 코드에 없다는 이유로
ui_focus를 미배선으로 남겼다. 코디네이터가 "그런 시스템 전체가 필요한 게 아니라, 기존 Button에
`ISelectHandler`/`IPointerEnterHandler`를 붙이기만 해도 된다"고 정정해 실제로 배선했다:

- **신규**: `Assets/_Project/Scripts/UI/UiFocusSfx.cs` — `MonoBehaviour, ISelectHandler, IPointerEnterHandler`.
  `OnSelect`(Unity uGUI `Selectable`이 EventSystem에 의해 선택 상태가 될 때 호출하는 표준 콜백)와
  `OnPointerEnter`(마우스 hover) 양쪽에서 `Selectable.interactable`을 확인한 뒤(잠긴 버튼은 무음)
  `AudioService.PlaySfx("ui_focus")`를 호출한다. **정정**: 이 프로젝트에서 Tab/방향키 입력이 실제로
  Selectable 간 포커스를 이동시켜 `OnSelect`를 호출하는지는 검증하지 않았다 — `OnSelect` 자체는
  uGUI 표준 이벤트라 지원되지만, 그걸 트리거하는 실제 키 입력 연결은 별개 문제이고 이번 범위에서
  확인하지 않았다.
- **연결 지점**: `UiFactory.AddFocusSfx(Button)` 헬퍼 신규 + 이 프로젝트가 Button을 만드는 실제 10곳
  전부(`PauseView`/`HudView`/`CardSelectView`/`CharacterSelectView`/`HybridUnlockView`/`ResultView`/
  `CardCodexView`/`SkillTreeView`(1곳)/`PermanentSkillView`(2곳))에 `AddComponent<Button>()` 직후 한 줄씩 추가.
- **한계**: 여전히 마우스 클릭·숫자키 단축키([1]~[6])가 주 조작 수단이라, Tab으로 실제 포커스를 옮기는
  빈도 자체는 낮을 수 있다(08의 정식 키보드/게임패드 내비게이션 UX가 없다는 사실은 그대로) — 하지만
  "이벤트가 오면 소리가 난다"는 배선 자체는 완료됐고, EditMode에서 컴포넌트 부착과 `interactable` 가드
  로직은 코드로 확인했다(실제 hover/select 이벤트 발생은 PlayMode/사람 조작이 필요해 미검증).

### 3-4. 전투SFX 억제를 "우연한 부작용"에서 "명시적 게이트"로 (4차 세션)

3차 세션까지는 "Time.timeScale=0이라 새 전투 트리거가 자연히 안 일어난다"는 **우연한 부작용**에
의존했고, `StopCombatSfx()`는 UI 음 4개만 예외로 뒀다 — 보상창 자체가 트리거하는 levelup/skill_unlock/
hybrid_unlock 같은 소리는 이론상 그 순간 `StopCombatSfx()`가 먼저 불려도 아직 재생 전이라 우연히
문제가 없었을 뿐, 순서가 조금만 달랐어도(예: 결과 화면의 clear/defeat) 자기 자신이 자기 자신에 의해
끊기는 경합이 날 수 있는 구조였다. 코디네이터 지적대로 이번에 명시적 게이트로 바꿨다:

- **`AudioService.AlwaysAllowedIds`** 신규(집합) — ui_focus/confirm/cancel/denied **+ levelup/skill_unlock/
  hybrid_unlock/clear/defeat**. `StopCombatSfx()`와 아래 억제 게이트 양쪽이 같은 기준을 쓴다.
- **`AudioService.CombatSuppressed`**(공개 읽기전용 상태) + **`SetCombatSuppressed(bool)`** 신규 —
  `true`로 걸면 (1) 즉시 `StopCombatSfx()`로 이미 울리던 전투 SFX를 끊고 (2) 그 뒤 `PlaySfxInternal`
  진입부에서 `AlwaysAllowedIds`에 없는 새 재생 요청을 전부 무시한다. `Time.timeScale`과 무관한
  명시적 차단이라, timeScale을 안 따르는 코드(`WaitForSecondsRealtime` 기반 코루틴 등)가 있어도
  전투 SFX가 새지 않는다.
- **배선**: `GameManager.EnterFrozenAudio/ExitFrozenAudioIfClear`가 `SetMusicDucked`와 함께
  `SetCombatSuppressed`를 호출하도록 교체(Pause/LevelUp/SkillSelect/HybridOffer). `EndRun`(결과 화면
  진입)에서도 `SetCombatSuppressed(true)` 신규 호출 — GameOver 이후 지연 이벤트(예: 코루틴에 물린
  폭발)가 결과 화면 위로 전투음을 내지 않게 한다(clear/defeat는 AlwaysAllowedIds라 결과 스팅어는
  그대로 남). **새 런 진입**(`GameManager.Initialize`)에서 `SetCombatSuppressed(false)`를 명시적으로
  호출 — 직전 런이 Pause/보상창이 열린 채로 포기 등으로 끝나 억제 플래그가 `true`로 남아있었을 경우를
  대비한 방어적 리셋(`CombatSuppressed`는 static bool이라 AudioService 인스턴스 재생성만으로는 안
  풀림).
- **AudioBus 주석 정정**: 기존 주석이 "실제 .mixer 에셋 생성은 언도큐먼트 내부 API에 의존해 버전마다
  깨지기 쉬워서 코드 버스로 대체했다"고 3차 이전 결정을 그대로 남겨둔 채였다(3-1절에서 이미 실제
  `.mixer`를 만들었는데도) — 실제로는 안 지워진 낡은 주석이었을 뿐 동작에는 영향 없었다. `AudioBus.cs`
  헤더를 현재 상태(그룹 분리·라우팅은 실제 AudioMixer, 볼륨 숫자 계산은 AudioBus)로 다시 썼다.

## 4. BuildRelease — 오디오/믹서 검사를 실제로 연결

- 기존엔 `Validate()`가 `.wav` 파일 개수만 셌다(형식/피크/루프는 안 봄). **수정**: `ValidateAudio()`로 교체 —
  `Tools/Audio/measure_audio.py`가 만든 `audio_measurements.json`을 열어 `issues` 배열이 비어있는지 확인하고,
  wav 파일들의 최종 수정시각이 그 리포트보다 최신이면("오디오는 새로 합성했는데 측정을 다시 안 돌림")
  별도 항목으로 명시적 실패 처리한다.
- **코디네이터 재지적 반영**: `report == null || report.issues == null`이면(파싱은 됐지만 형식이 이상하거나
  issues 필드 자체가 없는 경우) 이제 "문제 없음"이 아니라 **명시적 실패**로 처리한다 — 기존엔
  `report?.issues != null && report.issues.Length > 0`이라 issues가 null이면 조용히 통과했다.
- `.mixer` 에셋(3-1절) 존재 여부도 필수조건에 추가.

### 아트 v1 매니페스트 게이트 — 4차 세션에 실제로 연결함(3차의 스코프 보류 판단을 코디네이터가 명시적으로 철회시킴)

3차 세션엔 "다른 워커 소유 파이프라인과 충돌 우려"로 손대지 않았으나, 코디네이터가 "이번엔 다른 아트
워커가 활동 중이지 않고 소유권 충돌이 없다, 미루지 말 것"이라고 명시적으로 재지시해 실제로 연결했다:

- **`BuildRelease.ValidateArt(problems)`** 신규 — `ArtV1Validator.ValidateRoot("Assets/_Project/Art/v1",
  ArtV1ValidationMode.Import)`를 실제로 호출한다.
- **핵심 수정**: `ArtV1Validator.ValidateRoot`는 폴더 자체가 없거나 04/09 표의 에셋 폴더가 안 보이면
  `MissingAsset`을 **Info** 심각도로만 기록한다(Source 단계의 "아직 없음"은 정상적인 중간 상태라
  Error로 잡으면 안 되는 게 원래 설계 의도라서). 그래서 `report.ErrorCount`만 보면 Import 폴더가
  통째로 없어도 0건으로 통과해버린다 — 이게 코디네이터가 지적한 "부정확한 통과"의 정확한 원인이었다.
  **수정**: 빌드 게이트에서는 `Severity == Error`인 이슈와 `Code == MissingAsset`인 이슈를 **심각도와
  무관하게 둘 다** 실패로 취급한다.
- **실제 실행 결과(이번 세션에 `-executeMethod`로 직접 실행, 재현 가능)**:
  ```
  [CCT] 빌드 전 필수 검사 실패(10:91) — 아래 항목이 갖춰질 때까지 빌드를 만들지 않습니다:
  - 아트 v1 필수 에셋 누락 1건(Assets/_Project/Art/v1) — 검수 대상 폴더가 아직 없다
  BuildFailedException: ... (Assets/_Project/Scripts/Editor/BuildRelease.cs:73)
  executeMethod method CCT.Editor.BuildRelease.Windows64 threw exception.
  종료 코드: 1
  ```
  전체 로그: [`Delivery/logs/build-release-2026-09-27-art-gate-fail.log`](logs/build-release-2026-09-27-art-gate-fail.log).
  **이 실패는 의도한 정상 동작이다** — `Assets/_Project/Art/v1`가 실제로 존재하지 않으므로(G32 아트
  실물 검수가 아직 착수 전) 게이트가 정확히 걸려야 정상이고, 이걸 "고쳐서 통과시키는" 것은 이번 범위가
  아니다. 10:91 "누락은 명시적 빌드 실패다"가 이제 아트 자체에 대해서도 실제로 작동한다는 뜻이다.
- **부작용**: 이 게이트가 생긴 뒤로는 실제 아트가 `Assets/_Project/Art/v1`에 채워지기 전까지
  `BuildRelease.Windows64()`가 항상 실패한다 — **3차 세션에서 만든 기존 `Builds/Windows64/CocktailCoffeeTea.exe`
  (2026-09-27 11:35~11:44 사이 빌드)는 이 게이트가 생기기 전 산출물이라 지금 다시 빌드하면 안
  나온다.** 그 exe를 정식 납품본으로 취급하면 안 된다 — 7절 참고.

## 5. 테스트 / 빌드 결과 (이번 세션에 실제로 실행, 콘솔 로그·XML 확인)

Unity 6000.3.25f1(`ProjectSettings/ProjectVersion.txt` 실제 확인) 배치모드로 이번 세션에 총 6회 EditMode 실행:

- **최초 시도**: `-runTests`와 `-quit`를 같이 줬더니 스크립트 컴파일만 하고 테스트 실행 전에 종료 —
  결과 XML이 **아예 안 만들어짐**(코디네이터가 지적한 "즉시 종료 위험"이 실제로 재현됨). **원인**: `-quit`가
  비동기로 스케줄되는 테스트 실행보다 먼저 처리됨. **수정**: `-quit` 제거(`-runTests`는 완료 시 스스로 종료).
- 이후 5회는 전부 XML이 정상 생성됐고 `test-run` 루트 요소로 실제 결과를 직접 확인(콘솔 exit code만 믿지 않음):
  1. 폰트/오디오/BuildRelease 1차 수정 직후: **198/198 통과**.
  2. `AudioService`용 EditMode 테스트를 추가했다가 `DontDestroyOnLoad`가 `System.InvalidOperationException`
     ("can only be used in play mode")을 던지는 걸 실측 확인 — **EditMode에서 AudioService를 아예 인스턴스화할
     수 없었다**(1건 실패, 197/198 그 외 전부 통과).
  3. `EnsureCreated()`의 `DontDestroyOnLoad`를 `if (Application.isPlaying)`로 감싸 EditMode에서도 안전하게
     건너뛰도록 수정 → **198/198 복귀**.
  4. 믹서/GameManager 오버레이 덕킹 수정 반영 후: **198/198 유지**.
  5. `StopCombatSfx` 회귀 테스트 2개 추가 후: **200/200 통과**(`test-run ... result="Passed" total="200" passed="200" failed="0"`, 2026-09-27 02:47 UTC).
  6. **(4차 세션)** ArtV1 게이트/ui_focus 배선/`CombatSuppressed` 명시적 억제/믹서 라우팅 테스트 4개 추가 후:
     **204/204 통과**(`test-run ... result="Passed" total="204" passed="204" failed="0"`,
     2026-09-27 02:59 UTC). XML/로그 보존:
     [`Delivery/logs/editmode-results-2026-09-27-204.xml`](logs/editmode-results-2026-09-27-204.xml),
     [`Delivery/logs/editmode-log-2026-09-27-204.txt`](logs/editmode-log-2026-09-27-204.txt).
- **신규 오디오 회귀 테스트(EditMode, 실제 통과 확인)**: `AudioServiceTests.cs`, 총 6개 —
  - `StopCombatSfx_StopsCombatVoice_KeepsUiVoice` / `StopCombatSfx_CalledTwice_...`(3차, 09:139 "전투SFX 중단" 기본 동작)
  - `SetCombatSuppressed_True_BlocksNewCombatSfx_ButAllowsRewardAndUiSfx`(4차, 억제 중 새 전투 SFX 차단 + 보상/UI 음 허용)
  - `SetCombatSuppressed_True_StopsAlreadyPlayingCombatSfx_PreservesRewardAndUiSfx`(4차, 억제 진입 순간 이미
    재생 중이던 전투 SFX는 끊기고 levelup/ui_confirm은 유지 — StopCombatSfx가 보상/결과 음을 보존하는지의
    직접 증거)
  - `SetCombatSuppressed_False_RestoresCombatSfx`(4차, 억제 해제 후 전투 SFX가 다시 정상 재생되는지 — "복원"의 직접 증거)
  - `BuildVoicePool_RoutesSourcesToRealMixerGroups`(4차, SFX 보이스 24개 전부 실제 `.mixer`의 SFX 그룹으로,
    MusicA/MusicB가 Music 그룹으로 라우팅되는지 — 믹서 라우팅의 직접 증거)
- **PlayMode/코루틴 타이밍은 여전히 미검증**: 코디네이터가 요청한 "페이드/빠른 재시작/크로스페이드 중 종료"
  회귀 테스트는 실제로 시도해봤으나, Unity의 코루틴 스케줄러(PlayerLoop)가 EditMode에서는 안 돌아서
  `StartCoroutine`으로 시작한 코루틴이 **첫 `yield` 이후로 절대 진행되지 않는다**(이 프로젝트의 기존 PlayMode
  테스트들이 전부 `[UnityTest] + yield return null`을 쓰는 이유와 같은 제약). 즉 크로스페이드/결과 시퀀스의
  실제 타이밍 동작은 구조적으로 EditMode로 검증 불가능하고 PlayMode가 필요하다 — 이 머신은 기존에 알려진
  배치모드 hang 문제(2026-09-25부터)가 있어 재시도하지 않았다(지시대로 무한 대기·에디터 재설치 금지).
  3-2절의 3가지 코루틴 경합 수정은 **코드 리뷰 + 논리 검증 수준**이며, 사람이 에디터에서 직접 재생(런 종료
  직후 빠른 재시작, 보스 전환 중 사망 등)해봐야 최종 확인된다.
- **BuildRelease**: 3차 세션까지 `-executeMethod`로 2회 실행해 성공(폰트/오디오/믹서 게이트만 있던 시점).
  **4차 세션에 아트 v1 게이트를 추가한 뒤 3번째로 실제 실행** — 이번엔 **의도한 대로 실패**:
  ```
  아트 v1 필수 에셋 누락 1건(Assets/_Project/Art/v1) — 검수 대상 폴더가 아직 없다
  ```
  종료 코드 1, `BuildFailedException` 정상 발생. 전체 로그
  [`Delivery/logs/build-release-2026-09-27-art-gate-fail.log`](logs/build-release-2026-09-27-art-gate-fail.log)에 보존.
  폰트/오디오/믹서 조건 자체는 이 실행에서 아트 검사보다 먼저 통과했다(로그에 다른 항목 안 찍힘 — 아트
  항목 단 1건만 실패로 보고됨). **3차 세션에서 만든 `Builds/Windows64/CocktailCoffeeTea.exe`(182MB)는
  이 게이트가 생기기 전 산출물이며, 지금 기준으로는 재현 불가능한 낡은 빌드다 — 정식 납품본이 아니다.**
  실제 아트가 채워진 뒤 이 게이트를 통과하는 빌드를 다시 만들어야 한다.

## 6. 부수 효과 (의도한 변경 아님, 투명하게 기록)

Unity 6000.3.25f1 배치모드를 이번 세션에 처음 실행하면서 아래 파일들이 재직렬화됐다(이전 세션이
6000.3.16f1로 마지막 저장했을 가능성 — 로그 99 참고). 오디오/폰트 작업과 무관한 엔진 버전 차이에 의한
필드 추가/포맷 변경으로 보이며, `BuildRelease.Windows64()`가 의도적으로 설정하는 `bundleVersion=1.0.0`,
`scriptingBackend=Mono` 변경도 같은 파일(ProjectSettings.asset)에 섞여 있다:
- `Assets/DefaultVolumeProfile.asset`, `Assets/Settings/UniversalRP.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`(URP 관련, 신규 필드 추가/기본값 채움으로 보임)
- `ProjectSettings/ProjectSettings.asset`(위 URP류 필드 + BuildRelease가 의도적으로 쓰는 bundleVersion/scriptingBackend)
커밋하지 않았으므로 원하면 그대로 되돌릴 수 있다 — 내용을 직접 diff로 확인했고 파괴적이거나 의미 있는 게임
로직 변경은 없다.

## 7. 남은 제한 (완료 아님)

- **`Assets/_Project/Art/v1`가 아직 존재하지 않아 BuildRelease가 항상 실패한다(의도한 동작).** 4-절의
  아트 게이트가 실제로 걸려 있는 지금, 실제 아트가 04/09 표대로 채워지고 `status: ready`로 검수를
  통과하기 전까지는 어떤 빌드 시도도 성공하지 않는다 — 이건 버그가 아니라 10:91이 요구하는 정상 동작이다.
- **기존 `Builds/Windows64/CocktailCoffeeTea.exe`(182MB, 3차 세션 산출물)는 정식 납품본이 아니다.** 이
  게이트가 생기기 전에 만들어졌고, 지금 다시 빌드하면 재현되지 않는다(위 항목 참고). 사람이 이 exe를
  실행해 화면/소리를 확인하는 것도 여전히 이번 범위 밖 — 시도하지 않았다.
- **BGM 25개 WAV 중 3개(BGM)의 측정 피크는 "파일 자체" 피크이지 "런타임에 SFX와 합산된 최종 출력" 피크가
  아니다**(2절 참고) — 오프라인 스크립트로는 측정 불가능한 항목이며 PlayMode/실청이 필요하다.
- **AudioMixer의 그룹 페이더/볼륨 슬라이더는 여전히 AudioBus(코드)가 계산** — 그룹 자체(Master/Music/SFX)와
  라우팅(`outputAudioMixerGroup`)은 실제 `.mixer` 에셋이지만(3-1절), 08 설정 화면이 생기기 전까지
  마스터/BGM/SFX 볼륨은 `AudioBus`의 기본값(80/60/80)에 고정 — 사용자가 못 바꾼다.
- **ui_focus는 배선은 됐지만(3-3절) 실제 hover/select 이벤트 발생 자체는 미검증** — `UiFocusSfx` 컴포넌트가
  10개 Button 생성 지점에 붙는다는 것과 `interactable` 가드 로직은 EditMode 코드 레벨로 확인했지만, 실제
  마우스가 버튼 위로 지나가거나 Tab으로 포커스가 옮겨갈 때 소리가 나는지는 사람이 플레이해봐야 한다.
- **PlayMode/코루틴 타이밍 실행 증거 전무.** 크로스페이드·결과 시퀀스·빠른 재시작 취소·`CombatSuppressed`가
  실제 프레임 시퀀스 안에서 정확히 언제 걸리고 풀리는지 같은 시간 기반 동작은 EditMode의 코루틴 스케줄러
  제약(5절) 때문에 자동 테스트가 구조적으로 불가능하고, 이 머신의 PlayMode 배치 hang 문제도 그대로다.
  3-2/3-4절의 수정은 코드 리뷰 + 논리 검증 + 동기적 부분에 한한 EditMode 테스트 수준 — 사람이 에디터에서
  직접 재생(런 종료 직후 빠른 재시작, 보스 전환 중 사망, Pause↔보상창 겹침 등)해봐야 최종 확인된다.
- 08 나머지(설정/타이틀/도감4탭/정식 키보드·게임패드 내비게이션 UX), G32(아트 실물 검수), G35(성능 측정)는 그대로 남아있다.
- 기존 미커밋 변경(오늘 작업분 포함)은 커밋/푸시하지 않았다 — 지시대로. 사용자 세이브 파일과 아트 원본은 건드리지 않았다.
- 6절의 URP/ProjectSettings 부수 재직렬화는 이번 4차 세션에서도 그대로(추가 변경 없음).
