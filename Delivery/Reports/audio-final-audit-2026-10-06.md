# 오디오 최종 독립 감사 — 2026-10-06

- **대상**: `C:/Users/user/Documents/Projects/CocktailCoffeeTea`의 오디오 에셋, 런타임, 테스트(읽기 전용). 기준 문서는 기획 09 §4/§6, 10 G33/G34, 26 단계 4.
- **작업 조건**: 코드, 에셋, 설정은 수정하지 않았다. 에디터, 게임, 네이티브 GUI도 실행하지 않았고 오디오를 생성하거나 재생성하지도 않았다. `measure_audio.py`는 `Assets/.../audio_measurements.json`을 덮어쓰기 때문에 **실행하지 않았다**.
- **증거 폴더**: `Delivery/logs/audio-final-audit-2026-10-06/`
  - `audit_wav.py`: 이번 감사용 독립 분석기. `wave` 모듈 대신 RIFF를 직접 파싱하고, 결과는 자기 폴더에만 쓴다.
  - `wav-analysis.json`, `wav-analysis.txt`, `run-stdout.txt`: 파일별 측정 결과
  - `sha256-before.txt`, `sha256-after.txt`: 분석 전후 해시. 동일하므로 원본은 바뀌지 않았다.
  - `code-and-settings-evidence.txt`: 호출 지점, 임포트 설정, 믹서, AudioManager, 최신 EditMode 결과 발췌
  - `audiolistener-verification.txt`, `playmode-listener-warning-by-test.txt`, `player-log-audio-excerpt.txt`
- **이전 보고서 처리**: `Delivery/audio-font-resume-2026-09-27.md`는 참고만 했다. 그 보고서의 수치와 "정상" 주장은 그대로 받아들이지 않고 이번에 다시 측정했다. 그 보고서도 청취는 미검증으로 남겼고, 이번 감사도 청취를 대신하지 못한다.

> **한계(필수)**: 파일 분석, 코드 리딩, 기존 테스트 로그로는 스피커/헤드폰에서 실제로 들리는 소리, 네이티브 오디오 품질, 기기 없음이나 기기 전환 동작, 런타임 믹서의 실제 피크를 **증명할 수 없다**. 아래 PASS는 모두 "구조적 PASS"이고, 실제 청취 결과는 UNVERIFIED다.

---

## 0. 결론 요약

| 판정 | 항목 |
|---|---|
| **FAIL (치명)** | 빌드/런타임 어디에도 `AudioListener`가 없다. Unity 규칙상 이 상태에서는 BGM과 SFX가 **전혀 들리지 않는다**. 25개 파일과 라우팅이 정상이어도 지금 게임은 무음으로 납품될 가능성이 매우 높다(§2 B1). |
| FAIL (중) | `activeVoices`에 끝난 보이스 항목이 남는다. 같은 AudioSource에 항목 두 개가 겹치면 Pause 진입 시 UI 음이 잘릴 수 있다(§2 B2). |
| 위험 / UNVERIFIED | 런타임 최종 믹스 피크 ≤ −1 dBFS(09:71)를 보장하는 장치가 없다. 오프라인 추정으로는 기본 볼륨에서 약 0 dBFS까지 간다(§2 B3). |
| PASS (구조) | 22 SFX + 3 BGM의 ID, 형식, 채널, 길이, 피크, 클리핑, DC, 앞뒤 경계, BGM 루프 접합(§1). 이벤트 라우팅 22/22, 볼륨 0 경로, Pause 덕킹/억제 코드(§3). |

---

## 1. WAV 25개 독립 측정 (`audit_wav.py`, 원본 해시 불변)

측정 항목: RIFF 청크, fmt 태그, 채널/샘플레이트/비트, 프레임 수, 샘플 피크, 4× 오버샘플 true-peak, ±full-scale 샘플 수, DC 평균, 앞뒤 2/5 ms 최대값, 첫/끝 샘플, −60 dBFS 미만 앞뒤 무음 길이. BGM은 루프 접합 지표를 추가로 쟀다.

| ID | ch | 길이 s (명세) | 샘플 피크 dBFS | TP4× | FS 샘플 | DC dBFS | 첫/끝 샘플 | 끝 5 ms 최대 dBFS |
|---|---:|---|---:|---:|---:|---:|---|---:|
| ui_focus | 1 | 0.050 (0.05) | **−12.00** | −12.00 | 0 | −88.8 | 0/0 | −53.8 |
| ui_confirm | 1 | 0.120 (0.12) | −6.00 | −6.00 | 0 | −89.3 | 0/0 | −53.8 |
| ui_cancel | 1 | 0.120 (0.12) | −6.00 | −6.00 | 0 | −75.2 | 0/0 | −54.1 |
| ui_denied | 1 | 0.160 (0.16) | −6.00 | −5.99 | 0 | −79.9 | 0/0 | −54.9 |
| swing_rapier | 1 | 0.120 (0.12) | −6.00 | −5.83 | 0 | −73.3 | 0/0 | −57.4 |
| swing_sword | 1 | 0.160 (0.16) | −6.00 | −5.70 | 0 | −48.1 | 0/0 | −58.7 |
| shot_gun | 1 | 0.130 (0.13) | −6.00 | −5.84 | 0 | −54.1 | 0/0 | −60.5 |
| shot_bow | 1 | 0.140 (0.14) | −6.00 | −6.00 | 0 | −67.6 | 0/0 | −55.2 |
| shot_hybrid | 1 | 0.180 (0.18) | −6.00 | −6.00 | 0 | −92.7 | 0/0 | −55.2 |
| hit_enemy | 1 | 0.080 (0.08) | −6.00 | −5.98 | 0 | −52.2 | 0/0 | −52.9 |
| hit_player | 1 | 0.220 (0.22) | −6.00 | −5.99 | 0 | −69.8 | 0/0 | −55.3 |
| enemy_defeat | 1 | 0.180 (0.18) | −6.00 | −6.00 | 0 | −68.2 | 0/0 | −55.0 |
| pickup | 1 | 0.080 (0.08) | −6.00 | −6.00 | 0 | −86.7 | 0/0 | −51.6 |
| levelup | 1 | 0.450 (0.45) | −6.00 | −6.00 | 0 | −91.2 | 0/0 | −27.9 |
| skill_unlock | 1 | 0.360 (0.36) | −6.00 | −6.00 | 0 | −90.0 | 0/0 | −29.0 |
| hybrid_unlock | 1 | 0.600 (0.60) | −6.00 | −6.00 | 0 | −95.7 | 0/0 | −27.6 |
| boss_warning | 1 | 0.500 (0.50) | −6.00 | −6.00 | 0 | −63.7 | 0/0 | −55.3 |
| explosion | 1 | 0.350 (0.35) | −6.00 | −5.78 | 0 | −73.2 | 0/0 | −60.2 |
| steam | 1 | 0.800 (0.80) | −6.00 | −5.83 | 0 | −52.1 | 0/0 | −36.0 |
| spell | 1 | 0.300 (0.30) | −6.00 | −6.00 | 0 | −88.4 | 0/0 | −56.5 |
| clear | 1 | 1.200 (1.20) | −6.00 | −6.00 | 0 | −106.4 | 0/0 | −33.7 |
| defeat | 1 | 1.000 (1.00) | −6.00 | −6.00 | 0 | −89.4 | 0/0 | −37.6 |
| bgm_menu | 2 | 42.6667 (90bpm×16마디, 오차 0프레임) | −12.15 | −12.15 | 0 | −94.4 | 0/0 | 무음 |
| bgm_run | 2 | 68.5714 (112bpm×32, 0프레임) | −11.71 | −11.71 | 0 | −81.4 | 0/0 | 무음 |
| bgm_boss | 2 | 60.0000 (128bpm×32, 0프레임) | −11.73 | −11.70 | 0 | −81.0 | 0/0 | 무음 |

**구조 판정**

| 검사 (기준) | 결과 | 근거 |
|---|---|---|
| ID 집합 = 09 §4의 22+3 | **PASS** | missing [] / extra [] (`wav-analysis.json` summary) |
| PCM(fmt 1), 48 kHz, 16 bit, SFX mono / BGM stereo (09:71) | **PASS** | 25/25. 청크는 `fmt,data`뿐이고 RIFF 크기와 블록 정렬이 일치한다. |
| 길이 = 09 표 / BPM×마디 | **PASS** | SFX 오차 0.0 ms, BGM 오차 0 프레임 |
| SFX 효과별 peak ≤ −6 dBFS | **PASS** | 21개는 −6.00. ui_focus는 "낮은 크기"를 위해 의도적으로 −12.00이다. TP4× 최대는 −5.70(swing_sword)으로 샘플 피크보다 0.3 dB 높지만 클리핑과는 거리가 멀다. |
| BGM 파일 peak ≤ −1 dBFS | **PASS (파일 기준만)** | −11.7 ~ −12.2. 런타임 합산 피크는 §2 B3 참고. |
| 클리핑 | **PASS** | full-scale 샘플 0, 4× true-peak 모두 < −5.7 dBFS |
| DC 오프셋 | **PASS** | 최대 −48 dBFS(swing_sword)이고 나머지는 대부분 −60 dBFS 이하. 노이즈 기반 짧은 음의 평균값 수준이라 문제없다. |
| 앞뒤 2~5 ms 페이드 (09:71) | **PASS (구조)** | 25개 모두 첫/끝 샘플이 0이다. levelup/skill_unlock/hybrid_unlock/clear/defeat/steam은 끝 5 ms에 −28~−38 dBFS 신호가 남아 있다. 즉 마지막 음이 약 3 ms 선형 페이드로 끝나며, 명세 범위 안이지만 끝이 짧게 잘려 들릴 수 있다. 청취는 UNVERIFIED. |
| 톤 주파수 샘플 검사 | **PASS** | ui_focus 659.9 Hz(명세 660), ui_denied 180.2 Hz(180), boss_warning 두 번 모두 159.7 Hz(160). 0.20~0.30 s 쉼 구간은 완전 무음이다. |
| BGM 루프 접합 (09:72 "잡음 없음") | **PASS (구조)** | 아래 참고 |
| BGM 스테레오 | **PASS** | L/R 상관 0.993~0.995, 두 채널이 동일하지 않음(멜로디 −0.15, 코드 +0.15 팬과 부합) |

**BGM 루프 지표.** 고정 임계값이 아니라 같은 파일 내부 분포와 비교했다. 원값은 `wav-analysis.txt`의 `LOOP` 행에 있다.
- 끝 샘플 → 첫 샘플 값 차이는 세 곡 모두 **0.0**이다. 곡 끝은 menu 134.5 ms, run 54.4 ms, boss 24.2 ms의 디지털 무음(−60 dBFS 미만)이고, 길이가 BPM 격자와 0프레임 오차로 맞아 박자 구멍은 생기지 않는다.
- 접합부를 가로지르는 512샘플 창의 고역 에너지(2차 차분 RMS)를 내부 전체 창과 비교했다. menu는 27 퍼센타일, run 95, boss 89이고, 셋 다 **내부 최댓값보다 작다**(run 0.0033 < 0.0078, boss 0.0037 < 0.0079). 마디 첫 박의 일반적인 어택보다 거칠지 않다는 뜻이다.
- 곡율 퍼센타일은 81~94다. 접합 직후 곡이 첫 박 어택(첫 5 ms가 −17~−21 dBFS)으로 시작하는 정상적인 형태다.
- 한계: Unity는 BGM을 **Vorbis**(compressionFormat 1, quality 1.0)로 재인코딩한다. 인코딩 후의 루프 경계 샘플 정확성은 원본 WAV 분석으로 증명할 수 없어 UNVERIFIED다. §4 T4 테스트로 임포트 후 클립을 확인할 수 있다.

**이전 보고서와의 대조**: 피크, 길이, 채널 값은 2026-09-27 보고서 및 `audio_measurements.json`과 일치한다. WAV mtime(2026-09-27 11:27:08)이 리포트(11:27:11)보다 앞서므로 리포트는 현재 파일 기준이다. 다만 그 도구의 루프 임계값(`Tools/Audio/measure_audio.py:41-42`, `LOOP_SEAM_MAX = LOOP_SLOPE_MAX = 0.35`, 약 −9 dBFS 크기의 계단)은 클릭을 잡기에 너무 느슨하다. 이번 PASS는 그 도구가 아니라 위의 상대 지표를 근거로 한다.

---

## 2. 실행 가능한 버그 / 위험 (파일:라인)

### B1 — 치명: `AudioListener` 없음 → 게임 전체 무음 (구조적 FAIL)
- **근거 (`audiolistener-verification.txt`)**
  - 빌드 씬은 `Assets/_Project/Scenes/Boot.unity` 하나뿐이다. 이 씬의 YAML 헤더는 `!u!29/104/157/196/1660057539`(설정 블록)뿐이며, **GameObject(1), Camera(20), AudioListener(81)가 하나도 없다**.
  - `Assets/` 전체에서 class 81 헤더는 빌드 대상이 아닌 `Assets/Scenes/SampleScene.unity`와 `Assets/Settings/Scenes/URP2DSceneTemplate.unity`에만 있다. `Assets/_Project`에는 프리팹이 0개다.
  - C# 전체에서 `AudioListener`를 참조하는 곳은 테스트 `Assets/_Project/Tests/PlayMode/RunLifecycleStressTests.cs:122`(`LessOrEqual(..., 1)`)뿐이다. `AddComponent<AudioListener>`는 없다.
  - 카메라는 `Assets/_Project/Scripts/Core/GameManager.cs:181-182`에서 `AddComponent<Camera>()`로만 생성되고 리스너는 붙지 않는다. 타이틀/선택 화면(`GameBootstrap.cs:31-32`, `:41-42`)에서는 카메라조차 없다. 그런데도 `SetMusicState(Menu)`로 bgm_menu 재생은 시작된다.
- **런타임 증거**: 최신 PlayMode 결과 `Delivery/logs/playmode-results-2026-10-06-normalplay-play1.xml`에서 **135/135 테스트 케이스**의 출력에 Unity 경고 "There are no audio listeners in the scene. Please ensure there is always one audio listener in the scene"가 찍혀 있다. 합계 531,818줄이고 `functional-play5.xml`은 931,878줄, tutorial은 3/3 케이스다(`playmode-listener-warning-by-test.txt`). 릴리스 플레이어 로그(`normal-play-2026-10-06/*/player.log`, LocalLow `Player.log`)에는 이 경고가 없다. 다만 비개발 빌드 로그이고 오디오 관련 줄이 아예 없으므로 리스너가 있다는 증거는 되지 못한다.
- **영향**: 09 §4 전환, 10 G33/G34 "오디오정상", 26 단계 4 "소리·BGM 루프"가 실제로는 무음이 된다. 이전 보고서의 라우팅/덕킹 검증은 신호 경로까지만 맞다.
- **고칠 곳 (메인 소유자 ctx_ae31466d1986, 제안만)**: 리스너는 정확히 1개, 항상 활성 상태로 둔다. 가장 단순한 방법은 `AudioService.EnsureCreated()`(`AudioService.cs:56-68`)가 만드는 영속 GameObject에 `AudioListener`를 붙이는 것이다. 모든 소스가 `spatialBlend=0`(`AudioService.cs:100`)이라 리스너 위치는 결과에 영향이 없고, 재시작이나 카메라 파괴와도 무관하다. 이 경우 카메라에는 붙이지 않는다.
- **회귀 테스트**: `RunLifecycleStressTests.cs:122`의 `LessOrEqual(..., 1)`은 0개도 통과시킨다. "enabled 리스너가 **정확히 1**"로 바꾸고 타이틀, 런, 재시작 각 시점에서 단언하도록 한다(§4 T1).

### B2 — 중: 끝난 보이스 항목이 남아 같은 AudioSource에 항목 두 개가 생김
- **코드**
  - `AudioService.cs:154`의 `FindFreeSource()`(`:185-194`)는 `!isPlaying`인 소스를 고른다.
  - 그런데 `activeVoices`의 끝난 항목은 `Update()`(`:198`)에서만 정리된다.
  - 소리가 끝났지만 같은 프레임에 AudioService.Update가 아직 돌지 않은 상태에서 다른 스크립트가 `PlaySfx`를 부르면, 같은 소스가 재사용되고 새 항목이 추가된다(`:174`). 이때 옛 항목(예: `hit_enemy`)이 그대로 남는다.
  - 새 소리가 재생 중이라 `isPlaying==true`이므로 옛 항목은 **새 소리가 끝날 때까지 정리되지 않는다**.
- **실패 시나리오**: `hit_enemy`가 끝난 프레임에 `ui_confirm`이 같은 소스를 받는다. 이어서 Pause 진입 시 `SetCombatSuppressed(true)` → `StopCombatSfx()`(`:300-306`)가 옛 `hit_enemy` 항목을 찾아 `v.source.Stop()`을 호출하고, **AlwaysAllowed인 ui_confirm(또는 levelup/clear)이 잘린다**.
  - 같은 원인으로 `:151` 같은 ID 4개 제한에 죽은 항목이 섞여 계산되고, `:159-165` 우선순위 강탈 때 엉뚱한 항목이 기준이 될 수 있다.
  - 발생 확률은 스크립트 실행 순서에 달려 있다. 결정적이지 않고 짧은 창에서만 생긴다.
- **고칠 곳**: `PlaySfxInternal`에서 소스를 고른 직후 `activeVoices.RemoveAll(v => v.source == freeSource)`를 넣거나, 진입부에서 `!isPlaying` 항목을 먼저 정리한다.

### B3 — 위험/UNVERIFIED: 런타임 최종 믹스 −1 dBFS(09:71) 보장 장치 없음
- 런타임 이득 계산은 `AudioBus.cs:29-32`(SFX = master×sfx, 기본 0.8×0.8)와 `AudioService.cs:170`이다. `CCTMixer.mixer`에는 Attenuation 이펙트만 있고 리미터/컴프레서가 없으며, 스냅샷 `m_FloatValues: {}`라 그룹 페이더도 0 dB다.
- **오프라인 추정**(`audit_wav.py` `mix_estimate`): 실제 Unity 출력 측정이 아니다. bgm_run의 가장 큰 지점 위에 SFX를 같은 시점에 겹치고, dedup 간격(50/30 ms)과 ID당 4개 제한은 반영했다.
  - 서로 다른 전투 SFX 8종 동시 → **+0.04 dBFS**(0 dBFS 초과 1샘플, −1 dBFS 초과 12샘플)
  - 24보이스 스택 → −0.04 dBFS
  - 단발 hit_enemy → −12.0 dBFS
  - 마스터 100 / SFX 100이면 위 값에 +3.9 dB가 더해진다.
- **판정**: 밀집 전투에서 −1 dBFS 목표를 넘을 수 있다. 실제 피크는 믹서 VU나 출력 녹음으로만 확인할 수 있어 UNVERIFIED다. 선택지는 SFX 그룹 −3~−6 dB 헤드룸, Master에 Limiter, 또는 기획에서 "최종 믹스" 정의를 조정하는 것이다.

### B4 — 낮음/위험: BGM 첫 로드가 메인 스레드에서 동기 디코드될 가능성
- `bgm_*.wav.meta`: `loadType: 0`(DecompressOnLoad), `compressionFormat: 1`(Vorbis), `preloadAudioData: 0`, `loadInBackground: 0`
- 첫 `Resources.Load`(`AudioService.cs:121`)와 재생이 런 시작(`GameManager.cs:168`)과 **보스 입장 전투 중**(`BossDirector.cs:78`)에 일어난다. 60~69 s 스테레오 Vorbis 디코드가 프레임 끊김을 만들 수 있다. 측정 데이터는 없어 UNVERIFIED다.
- 디코드된 PCM은 3곡 합계 약 33 MB이고 `clipCache`에 계속 남는다.
- 선택지: BGM을 Streaming 또는 CompressedInMemory로 바꾸거나, 부트 시 3곡을 미리 로드한다.

### B5 — 낮음 (검사 도구/게이트)
- `Tools/Audio/measure_audio.py:41-42`의 루프 임계값 0.35는 사실상 무검사 수준이다. 클리핑 샘플 수, DC, SFX 앞뒤 경계도 보지 않는다.
- `measure_audio.py`는 매 실행마다 `Assets/.../audio_measurements.json`을 덮어쓴다.
- `BuildRelease.cs:303`은 개수가 부족할 때만 잡는다(`<`). `:317`은 mtime만 비교하므로, 내용이 바뀌었는데 mtime이 오래된 파일은 통과한다.

### B6 — 문서 불일치
- 09:70은 WAV 출력 위치를 `Assets/_Project/Audio/v1/`로 적고 있지만 실제 위치는 `Assets/_Project/Resources/Audio/v1/`이다. 런타임은 `Resources.Load` 경로라 실제 위치가 맞다.
- 10:117이 요구하는 `Delivery/Reports/audio-review.md`는 존재하지 않는다. 이 보고서가 그 역할의 일부를 대신하는지는 코디네이터가 판단한다.

### 참고 (버그 아님)
- 모든 meta에 `normalize: 1`이 있지만 이 옵션은 `forceToMono: 1`일 때만 작동하고 현재 0이다. 임포트 후 레벨이 바뀌지 않았는지는 T4로 확인할 것을 권한다.
- AudioManager는 RealVoice 32 ≥ 26(24+2), DisableAudio 0, DSP 1024다.
- `EnterPlayModeOptions: 0`이라 도메인 리로드가 켜져 있다. static 볼륨/억제 플래그는 에디터에서도 매 Play마다 초기화된다.

---

## 3. 이벤트 라우팅 / 볼륨 0 / Pause (코드 + 기존 테스트)

| 항목 | 판정 | 근거 |
|---|---|---|
| 22 SFX 호출 지점 | **PASS (22/22)** | ui_focus `UiFocusSfx.cs:24` · ui_confirm/cancel는 Title/Pause/Settings/Codex 등 다수 · ui_denied·skill_unlock `PermanentSkillView.cs:209-211` · swing_* `RapierWeapon.cs:28,55`(첫타만) · shot_gun/bow `PlayerProjectileWeapon.cs:33,61` · shot_hybrid `HybridBowWeapon.cs:57` · hit_enemy `Enemy.cs:760` · enemy_defeat `Enemy.cs:783`(보스 제외) · explosion `Enemy.cs:731`, `EspressoLeviathanDriver.cs:139` · steam `EspressoLeviathanDriver.cs:184` · spell `FlameBurst.cs:40`, `PoisonAura.cs:40`, `CustomerBossDriver.cs:229` · pickup `IngredientPickup.cs:79` · hit_player `PlayerController.cs:197`(무적이면 `:180`에서 차단) · levelup `GameManager.cs:518` · hybrid_unlock `:563` · skill_unlock `:590` · boss_warning `BossDirector.cs:77` · clear/defeat `AudioService.cs:350` |
| 3 BGM 전환 (09:124) | **PASS (코드)** | Title/선택은 Menu(`GameBootstrap.cs:32,42`), 런은 Run(`GameManager.cs:168`), 보스는 Boss(`BossDirector.cs:78`), 보스 사망 시 Run(`GameManager.cs:488`, 최종보스 제외), 결과는 0.5 s 페이드 → 스팅어 → 1.5 s → Menu(`AudioService.cs:312-353`). 0.5 s 크로스페이드 `:23,235-262`. 타이밍은 PlayMode 실측이 없어 UNVERIFIED다. |
| 폴리포니 24 / 같은 ID 4 / dedup 50·30 ms / 우선순위 / 팬 | **PASS (코드)** · B2 결함 있음 | `AudioService.cs:21-22,44-54,145-166,172,177-183`. 팬은 `viewportX−0.5`이며 정규화 x×0.5와 같다. 보스 경고는 중앙. |
| 볼륨 0 = 완전 무음 (09:124) | **PASS (구조)** | 설정 0..100은 클램프(`SettingsService.cs:329`, 로드 시 `:619-621`) 후 /100으로 `AudioBus`에 들어간다(`:278-280`). Effective 값은 곱이라 0이 정확히 0이다(`AudioBus.cs:29-32`). SFX는 재생 시(`AudioService.cs:170`)와 매 프레임(`:199-201`) 적용되고, 음악은 Update(`:208`), 크로스페이드(`:251-255`), 결과 페이드(`:344`)에서 모두 0으로 상한된다. AudioSource는 AudioService 밖에 없다(`code-and-settings-evidence.txt`). 무음 효과를 가리는 B1 때문에 지금은 "볼륨과 상관없이 무음"이다. |
| Pause/보상창: 음악 −8 dB, 전투 SFX 중단 (09:124) | **PASS (코드) + 테스트 일부** | `AudioBus.cs:27,30`(10^(−8/20)=0.398) · `GameManager.cs:372-388`(Pause/LevelUp/SkillSelect/HybridOffer 진입·해제, 오버레이 스택 고려) · `EndRun` `:617-622` · 억제와 허용 목록 `AudioService.cs:274-307`. **기존 EditMode 6개 PASS**: `editmode-results-2026-10-06-normalplay-edit1.xml`, 365/365(AudioServiceTests 6/6). 덕킹 수치와 실제 Pause 진입 흐름을 단언하는 테스트는 없다. |
| 설정 슬라이더 → AudioBus | **PASS (테스트)** | `SettingsViewTests.VolumeSliders_WriteThroughToAudioBus`, `SettingsV2Tests` 다수(범위 밖/ null 값 클램프 포함) |
| 기기 없음 (09:139) | **UNVERIFIED** | 장치 감지나 대체 처리 코드가 없다. Unity 기본 동작에 의존하며 실험하지 못했다. |
| 실제 청취 (음색, 루프 이음, 밸런스) | **UNVERIFIED** | 범위 밖이다. 게다가 B1 때문에 현재 빌드는 청취 검수 자체가 성립하지 않을 가능성이 높다. |

---

## 4. 메인 소유자용 결정적 회귀 테스트 제안 (구현 복제 금지, 기획 상수로 단언)

- **T1 (B1, 최우선)**
  - EditMode: `AudioService.EnsureCreated()` 후 `FindObjectsByType<AudioListener>(FindObjectsInactive.Include)`에서 enabled인 것이 **정확히 1개**인지 확인한다.
  - PlayMode: 타이틀(`GameBootstrap.ShowTitle`), 런(`Build`), 재시작 10회 각각에서 같은 조건을 단언한다. `RunLifecycleStressTests.cs:122`의 `LessOrEqual(…,1)`을 `AreEqual(1, enabled count)`로 교체한다.
  - 보조 장치: PlayMode 로그에 "no audio listeners"가 한 줄이라도 있으면 실패 처리한다(`LogAssert.NoUnexpectedReceived` 또는 결과 XML grep).
- **T2 (B2)**, EditMode
  1. `PlaySfx("hit_enemy")`
  2. 그 Voice 소스를 직접 `Stop()`해서 "끝났지만 Update 전" 상태를 재현한다.
  3. `PlaySfx("ui_confirm")`
  4. `StopCombatSfx()`
  5. ui_confirm이 여전히 `isPlaying`인지 단언한다.

  지금 코드에서는 실패해야 정상이고, 수정 후 통과해야 한다.
- **T3 (볼륨 0/덕킹)**, EditMode, 기획 상수로 단언
  - Master=0이면 `EffectiveMusicVolume==0`, `EffectiveSfxVolume==0`. Music=0, Sfx=0도 각각 확인한다.
  - SfxVolume=0에서 `PlaySfx("ui_confirm")` 후 해당 소스의 `volume==0`.
  - `MusicDucked=true`이면 비율이 0.398±1e-3(−8 dB 상수)인지 확인한다.
- **T4 (임포트 후 클립)**, EditMode
  - `Resources.Load<AudioClip>("Audio/v1/<id>")`로 09 표의 25개 ID를 모두 불러 null이 없는지 본다.
  - `channels`(SFX 1/BGM 2), `frequency==48000`, `samples == 명세 길이×48000`(BGM은 정확히 2,048,000 / 3,291,429 / 2,880,000 프레임)을 확인한다.
  - `GetData` 피크가 파일 피크(−6/−12/−11.7 dBFS)와 ±0.5 dB 이내인지 확인한다. Vorbis, normalize, 샘플레이트 오버라이드로 인한 회귀를 잡는다.
- **T5 (Pause 흐름)**, PlayMode
  - `OpenPause` 후 `AudioBus.MusicDucked==true`이고 `AudioService.CombatSuppressed==true`인지 확인한다.
  - `ClosePause` 후 둘 다 false인지 확인한다.
  - LevelUp 위에 Pause를 겹친 상태에서 LevelUp만 닫히면 둘 다 계속 true인지 확인한다.
- 비권장: ID별 4개 제한 테스트. EditMode에서는 `Time.unscaledTime`이 고정되어 dedup이 반복 재생을 막으므로 결정적인 테스트가 어렵다.

---

## 5. 남은 일

1. B1을 수정하고 T1을 추가한다(메인 소유자). **이것 없이 오디오 G33/G34를 통과로 볼 수 없다.**
2. B2 수정 + T2.
3. 사람이 실제 기기로 청취 검수: 3곡 루프 이음, SFX 끝 잘림(levelup/clear 등), 밀집 전투 클리핑(B3), 볼륨 0, Pause −8 dB, 기기 없음/분리.
4. B3 헤드룸/리미터 결정, B4 로드 방식 결정, B5/B6 정리.
