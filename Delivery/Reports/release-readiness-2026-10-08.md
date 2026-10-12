# 출시 준비 1차 wave 보고 — 2026-10-08

Orca task `task_f621d88d1c91` / dispatch `ctx_3b0f224a8c55`. 기준: Unity 6000.3.25f1, 기획 05~11·14·26~28, `Delivery/Reports/requirements-matrix.md`, 2026-10-07 구현/UI 보고.
사용자가 PC 화면·GPU 검수를 허용했다(코디네이터 확인). **이 보고는 game_complete 판정이 아니다.** G32(정식 아트 승인)와 엄격 출시 검사는 여전히 막혀 있다.

보존: 기존 사용자 저장, 미커밋 작업(커밋하지 않음), ArtSource 원본(아트 담당 소유, 이번에 수정하지 않음), r13 빌드·ZIP·`Play-FirstPass.cmd`(r13을 가리킨 채 그대로). 엄격 출시 검사(`BuildRelease.Windows64`)는 바꾸지 않았다. Steam 업로드·게시·결제 없음. 다른 GUI·게임·agent 프로세스는 종료하지 않았다(내가 띄운 QA 플레이어/Unity만 자체 종료).

## 1. 산출물

| 산출물 | 경로 | 해시 / 비고 |
|---|---|---|
| 프리뷰 r14 (최신 UI U1/U2/U3 + 3결함 수정 전 소스 일부) | `Builds/FirstPassPreview-r14/`, `Builds/FirstPassPreview-r14.zip` | ZIP SHA256 `3BB97DACEC4812228DBB956D866B8BC6C5BC081FBCF08274406ED2EC47A00B6E`, Runtime `f7b14c2e…641da`. 첫 빌드라 아래 결함 수정이 들어가지 않았다 — 참고용 |
| 프리뷰 r15 (3결함 수정 + 총 2배) | `Builds/FirstPassPreview-r15/`, `.zip` | ZIP `EA2BC3E317EE4A717B9DED2273785346012B10B70F797F2E192C8B68E5FA00B6`, Runtime `a96a6347…5d55db` |
| 프리뷰 r16 (r15 + 오디오 덕킹 수정, 이번 wave 최종) | `Builds/FirstPassPreview-r16/`, `.zip` | ZIP `BB25CF0950721ADAC4B3E93F4156E67ED21745ACD72C4993BDE19028099097A9`, Runtime `f57cb8e1…a02f8b` |
| 일반 빌드 QA 도구 제외 확인 | r14/r15/r16 `CCT.Runtime.dll` 메타데이터에 `AutoTour`·`QaAudioTap`·`LoadScenario`·`PlayVerifier` 타입명 없음(정확한 #Strings 검색) + EditMode `QaToolIsolationTests` 4건 | AutoTour는 이번에 `CCT_QA_TOUR` 심볼 전용으로 옮겼다(이전 r13까지는 일반 프리뷰에 포함돼 있었다) |
| QA 빌드 | `CCT.Editor.BuildRelease.Windows64QaTourBatch`(신규, CCT_QA_TOUR), `Windows64LoadTestBatch`(CCT_QA_LOAD) | 엄격 검사 아님. 출력 `C:\UnityScratch\r14\qatour*`, `loadtest*` |
| 화면·영상 증거 | `C:\UnityScratch\r14\runs\<run>\out`(PNG, 연속 JPG, frames.csv), 영상 `Delivery/video/r14-2026-10-08/<run>/*.mp4`(+`-zoom2x.mp4`, `.frames.csv`) | §3 |
| 로그·테스트 XML·요약 | `Delivery/logs/r14-2026-10-08/` | §2, §3 |
| 도구 | `Tools/QA/analyze_motion.py`, `encode_videos.ps1`, `frame_coverage.py`, `analyze_audio.py`, `Tools/AudioProbe/`(WASAPI 루프백+세션 미터, .NET 10), `Tools/Run-G35LoadTest.ps1`(수정) | |

## 2. 코드 변경과 실제 결함 수정

| 구분 | 파일 | 내용 | 회귀 검증 |
|---|---|---|---|
| 결함 1 (코드 검토) | `Enemies/Enemy.cs` | 행동 클립(예고·공격: charge_prepare/refund/steam/action…) 재생 중 피격되면 hurt가 클립을 덮고, hurt 끝 프레임이 남은 행동 시간 내내 멈춰 예고가 사라졌다. 행동 중에는 hurt 클립을 재생하지 않는다(흰 섬광·숫자·불꽃은 유지). **정정:** 첫 보스 녹화에서 hurt만 보인 것은 이 결함 때문이 아니라 기존 AutoTour가 보스 패턴 드라이버를 붙이지 않았기 때문이었다(아래 E2). 결함 자체는 코드 경로로 확인했다 | `FirstPassArtTests.Enemy_HurtDuringActionClip_KeepsActionClip_ThenHurtAfterActionEnds` |
| 결함 2 (모션 CSV) | `Player/PlayerVisual.cs` | 손 anchor·좌우 반전이 몸체 SpriteAnimator 진행 전(Tick)에만 갱신돼 무기가 프레임 전환마다 한 프레임 이전 손 위치에 있었다(수정 전 같은 스프라이트 안 손 위치 점프 최대 0.62 world). `LateUpdate`에서 현재 프레임 손·방향을 다시 맞춘다 | `PlayerVisual_SyncToCurrentFrame_…` / 수정 후 실측 같은 스프라이트 내 점프 0.0000, 반전 불일치 0 |
| 결함 3 (모션 CSV) | `PlayerVisual.cs`, `Enemies/DefeatVisual.cs` | 사망 즉시 런 종료(timeScale 0)로 플레이어 defeat가 첫 프레임에 고정됐다(최종 보스 처치 DefeatVisual도 동일). 플레이어 defeat와 런 종료 후 DefeatVisual은 실시간으로 재생 | `PlayerDefeat_PlaysOnUnscaledTime_…` / 수정 후 실측 defeat 마지막 프레임 3 도달(좌우, 4무기) |
| 결함 4 (오디오 탭) | `Core/GameManager.cs` | 결과 화면에서 `OpenPause`가 가드 없이 열리면(HUD 일시정지 버튼은 직접 호출) 음악 덕킹(-8dB)이 다음 런까지 남았다(실측: 재시작 런 BGM 0.191, -31.5 vs -28 dBFS). GameOver에서 OpenPause 무시 + 새 런 시작 시 덕킹 해제 | `GameManagerFrameOrderTests.OpenPause_AfterGameOver_IsIgnored_AndNewRunStartsWithoutMusicDuck` / §5 재측정 |
| 사용자 지시: 총 2배 | `Art/v1/weapon_gun/manifest.json`, `Resources/ArtV1/ArtCatalog.asset` | 아트 담당이 바꾼 ArtSource 값과 맞춰 `targetVisualLengthRelativeToPlayer` 0.4→0.8 한 줄만 반영(그 외 차이 없음, PNG 동일, grip [104,144]·pivot 보존, 코드 배율 변경 없음 → 중복 2배 없음). 공격력·사거리·투사체 불변 | `BakedArtCatalogTests.HeldGun_IsTwiceOldLength_GripOnHand_MuzzleForward(±1)`, 실측 §3.3 |
| QA 도구 | `Core/AutoTour.cs`(CCT_QA_TOUR 전용으로 이동·확장), `LaunchOptions.cs`, `Editor/BuildRelease.cs` | 모드 screens/motion/all/bestiary/audio. Time.captureFramerate 30 연속 실제 엔진 프레임 + 프레임별 CSV. 보스는 실제 `BossDirector.SpawnNow` 경로. 전투 정지 화면은 보상창이 열려 있으면 닫고 재촬영 | `QaToolIsolationTests` |
| G35 실행 스크립트 | `Tools/Run-G35LoadTest.ps1` | 이전 실행이 `-Build ../../r14/loadtest`를 `C:\r14`로 해석해 시작조차 못 했고 PID 없는 채 대기했다. `-BuildPath` 절대 경로, exe 부재/시작 실패 즉시 exit 3, 타임아웃 exit 4, summary 없음 exit 5 | 가짜 경로로 `LAUNCH FAILED … exit 3` 확인 |

테스트(전부 hidden batch, 스크래치 `C:\UnityScratch\CCT`):

| 시점 | EditMode | PlayMode | XML |
|---|---|---|---|
| 수정 전(r14) | 385/385 | 140/140 | `logs/r14-2026-10-08/prefix/` |
| 3결함 수정 | 388/388 | 140/140 | `b3/` |
| + 총 2배 (r15) | 390/390 | 140/140 | `final-gun2x/` |
| + 덕킹 수정 (r16) | 391/391 | 140/140 | `final-r16/` |

## 3. 네이티브 화면·동작 증거 (실제 엔진 렌더)

모든 실행: 격리 저장(`-cct-data-dir`), QA 투어 빌드, 1080p = 1920×1080 테두리 없는 전체화면, 720p = 1280×720 창 모드. 연속 프레임은 Time.captureFramerate 30(게임 시간 1/30초 고정)으로 매 프레임 캡처 — 프레임 번호 = 게임 시간×30. 강제 경로(보스 즉시 소환, 녹화 구간 일반 스폰 정지·적 정리, 정지 표적, 관찰 중 회복, 화염/독 강제 부여, 장판 피해 사망, 보스 관찰 중 플레이어 무기 정지와 HP 문턱 직접 피해)는 `AutoTour.cs` 머리말과 각 tour-log에 기록돼 있다. 정상 플레이 완주·밸런스·키보드 UX 증거가 아니다.

| 실행 | 빌드 | 내용 | 완료 marker |
|---|---|---|---|
| `runs/t1080-prefix` | 수정 전 | 참고(결함 1~3 이전, 보스 드라이버 없음) | shots 184, frames 7226, errors 0 |
| `runs/t1080`, `runs/t720` | `qatour-b3`(3결함 수정, 총 0.4) | 필수 화면 + 보스 4 + 불/독 + 4무기 모션 | 각 shots 199, frames 7736, errors 0, exceptions 0 |
| `runs/t1080-gun2x`, `runs/t720-gun2x` | `qatour-g2`(+총 2배) | 1080 전체, 720 모션 | 199/7736/0/0, 136/4826/0/0 |
| `runs/b1080`, `runs/b720` | `qatour-best`(+bestiary) | 17몸체 노출 갤러리 + olive_fire | 각 19/4468/0/0 |

### 3.1 필수 화면 (1080p·720p 각각)
01 타이틀, 02 도감/02b 상세, 03 설정, 04 영구 강화, 05 제작 정보, 10 직업 선택, 11 무기 선택, 12 첫 안내, 20 걷기×4, 21 전투×4, 22 적 13종 라인업, 30 카드 선택(U2 재료 아이콘 표시 확인), 31 커피 트리, 32 하이브리드 해금(U1 불투명 패널·밝은 버튼 글자 확인), 33 불/독, 40/41/42 보스 4종, 50 일시정지, 51 일시정지→설정, 52 일시정지→안내 다시보기(키보드), 53 같은 경로(마우스 방식), 60 결과×4, 70 재시작 후 타이틀. 중앙 HUD 게이지 바탕(U3) 표시 확인. 글자 겹침/잘림의 사람 판정은 정지 화면 검토 수준이다.

- **E1 해소:** `33_hybrid_fire_poison`은 보상창 없이 촬영(촬영 순간 열리면 닫고 재촬영, 최종 실행 재시도 로그 0). 불/독 5초 연속 녹화 `video/33_fire_poison`.
- **E2 해소:** 보스는 실제 등장 경로로 화면 가장자리에서 입장, 패턴 드라이버 작동, 메뉴 없음. 기록된 클립 — 그라인더 charge_prepare·charge / 드립 drip·vanish·appear / 머신 steam·rain·pressure(HP 29% 강제 후) / 진상 손님 refund·summon·review·reject(HP 48% 강제 후 분노). 각 패턴 예고 시작(`_a`)과 0.5초 뒤(`_b`) PNG, 16~32초 연속 녹화와 프레임별 보스 클립·위치·HP·무적·투명도·적탄/장판 수 CSV.

### 3.2 플레이어 4무기 × 좌우 전환 (`video/motion_<조합>_run1`, `_run2_deathL`)
구간: idle_R 90f → move_R 75f → stop_R 45f → move_L 75f → idle_L 90f → 접근·공격 R 120f → 공격 끝 idle 45f → 접근·공격 L → 공격 끝 idle → 피격 중 이동 R(2회) → 피격 중 공격 L(2회) → 피격 정지 R → (레이피어: 하이브리드 해금 후 이동 R/L·보조 활) → 사망 R / 런2: 좌향 대기 → 사망 L → 결과 → 재시작.
자동 요약(`runs/<run>/motion-summary.md`, `Tools/QA/analyze_motion.py`), 최종 실행 기준:
- idle 90f 동안 루프 감김 2(4장@3fps = 1.33초/루프 → 2루프 이상), move 74f 동안 4~5(6장@10fps = 0.6초/루프 → 3루프 이상).
- 전환 지연: idle→move 1프레임(33ms), move→idle 1프레임. 공격→idle 복귀, 피격 hurt 3프레임 후 move/attack 재개.
- 좌우 반전 불일치(생존 중) 0, 임시 손 위치 0, 같은 스프라이트 내 손 이동 0.0000 world(수정 전 0.62).
- defeat: 좌우 모두 마지막 프레임(3) 도달 후 유지(수정 전 0에 고정).
- 이동 속도: 12.2 world / 74프레임(약 4.94 world/s) = 걷기 1루프(0.6초)당 약 2.96 world. 발 미끄러짐은 아트 담당 영상 판정 대상(`-zoom2x.mp4`).
- 프레임 경계 손 위치 최대 차이는 hurt↔ranged/melee 같은 자세 전환에서 0.44~0.81 world — 원본 자세 차이이며 지연 결함은 아님. 아트 판정 대상.

### 3.3 총 2배 실측 (`runs/t1080-gun2x/out/tour-log.txt` run 행)
`weaponTarget=0.8 weaponLengthWorld=1.28 bodyHeightWorld=1.6 ratio=0.8` (이전 빌드 0.4 → 0.64 world, 비율 정확히 2.000). provisionalHand=False. 원거리 공격 손 anchor 좌표는 좌우 거울 일치(ranged 0~3 각 프레임 ±0.284/0.447/0.275/0.318, y 동일). 대조판 `Delivery/screens/r14-2026-10-08/gun2x-left-right-sheet.png`.
**아트 관찰(미수정, 아트 담당 판단):** grip이 원본 가로 104/256 지점이라 2배 후 총 뒤쪽 약 40%(흰 개머리)가 손 뒤로 나와 허벅지를 가린다.

### 3.4 19몸체 363 원본 프레임 실제 노출 (`Tools/QA/frame_coverage.py`)
아트 검수 `ArtProduction/release-review-2026-10-08/frame-checklist.csv`의 363 프레임을 QA CSV `sprite`(플레이어·bestiary)와 보스 `bossClip/bossFrame`으로 대조:
- 1080p(t1080-gun2x + b1080 + t1080): **363/363** 노출, 양방향 기록 249. `Delivery/logs/r14-2026-10-08/frame-coverage-363-1080p.csv`
- 720p(t720-gun2x + b720 + t720): 362/363(진상 손님 defeat_03 미기록 — 런이 클리어로 끝나는 시점). `frame-coverage-363-720p.csv`
- 단방향/미기록: 모든 몸체 defeat는 오른쪽 향함만(4×17), 보스 패턴 클립은 노출됐지만 보스 CSV에 facing 열이 없다 → 다음 보완.
- bestiary는 정지 복제 정의(속도0·접촉0, 드라이버 없음)로 원본 프레임의 실제 엔진 노출·좌우·전환을 기록한 것이며 AI 행동 검증이 아니다. 아트 동작 승인(dynamicApproved)은 아트 담당 몫이다.

### 3.5 olive_fire 가림 (`runs/b1080/out/video/bestiary_olive_fire`)
실제 올리브 폭탄병(원래 정의)이 접촉해 0.6초(f18)에 폭발, BurstZone 지속 75프레임 = 2.5초(정의값). `91_olive_fire_f45/f75.png`: 플레이어는 장판 위에 그려져 계속 보이고 불꽃 그림은 적색 판정 원 안에 있다 → 과도 가림으로 확정하지 않음. 불꽃 그림 하단이 직선으로 잘린 사각 띠 형태 — 아트 관찰로 인계, 판정·크기 변경 없음.

## 4. 오디오 실제 출력 경로

도구: (1) QA 빌드 `QaAudioTap` — AudioListener `OnAudioFilterRead` 최종 출력(기기로 보내는 샘플) 100ms RMS/피크 + WAV. (2) `Tools/AudioProbe` — Windows 세션 미터(IAudioMeterInformation, CocktailCoffeeTea.exe 세션만, 50ms)와 기본 장치 WASAPI 루프백 WAV/RMS. 장치 `스피커(EDIFIER QR30)`, 48kHz 2ch float, 엔드포인트 볼륨 1.0·비음소거.

1차(r15 소스, `C:\UnityScratch\r14\runs\audio1`, 요약 사본 `Delivery/logs/r14-2026-10-08/audio-prefix-duckfix/audio-summary.md`) — 세 경로 수치가 일치했다:

| 구간 | tap RMS dBFS | tap 피크 | OS 세션 피크 평균 | 루프백 dBFS |
|---|---|---|---|---|
| 타이틀 메뉴 BGM | -30.4 | 0.119 | 0.083 | -30.4 |
| 마스터 0 (타이틀) | -inf (28/28 창 0) | 0 | 0 | -inf |
| BGM 0 (타이틀) | -inf | 0 | 0 | -inf |
| 런 BGM+전투 | -26.1 | 0.459 | 0.172 | -26.3 |
| 일시정지 | -37.0 | 0.048 | 0.040 | -36.9 |
| 런 SFX 0 | -28.9 | 0.117 | 0.100 | -28.9 |
| 런 마스터 0 | -inf | 0 | 0 | -inf |
| 재시작 1/2/3 런 | -31.5 / -27.7 / -28.1 | | | |

- 볼륨 0 = 완전 무음(디지털 0), 일시정지 약 -11dB(음악 -8dB + 전투 SFX 중단), 재시작마다 리스너 1·BGM 1. 측정 구간 tap 피크 최대 0.47 → 디지털 클리핑 없음.
- 결함 4(덕킹 잔류)를 이 측정에서 발견해 수정. 이 런은 측정 중 플레이어가 사망해 카드 오버레이 구간이 무효 → r16로 재측정(§5).
- **한계:** 사람 청취 도구/청취자가 없다. 음색, 루프 이음새 클릭, 체감 균형, 22 SFX 각각의 실제 재생 개별 확인은 미검증이다. 루프백에는 다른 앱 소리가 섞일 수 있으나 이번 측정은 세션 미터·tap과 일치했다.

## 5. 오디오 재측정 (r16, 덕킹 수정 후)

`C:\UnityScratch\r14\runs\audio2`, 사본 `Delivery/logs/r14-2026-10-08/audio-r16/`(tour-log, audio-tap.csv, session-meter.csv, loopback.csv, endpoint-info.txt, audio-summary.md). QA 빌드 Runtime `17ed1b6a…5ead6e`(r16 소스). 측정 동안 플레이어 회복·정지 표적으로 런이 끝나지 않게 했다(강제).

| 구간 | tap RMS dBFS | tap 피크 | OS 세션 피크 평균 | 비고 |
|---|---|---|---|---|
| 타이틀 메뉴 BGM | -30.3 | 0.119 | 0.083 | |
| 마스터 0 / BGM 0 (타이틀) | -inf / -inf | 0 / 0 | 0 / 0 | 창 28/28, 19/19 전부 0 |
| 런 BGM+전투 | -26.8 | 0.460 | 0.174 | |
| 일시정지 | -37.0 | 0.048 | 0.041 | 덕킹 + 전투 SFX 중단 |
| 런 SFX 0 / BGM 0 / 마스터 0 | -28.9 / -24.2 / -inf | | 0.100 / 0.108 / 0 | BGM 0 구간 45% 창 무음(SFX만) |
| 카드 보상창 열림 | -36.6 | 0.100 | 0.040 | 덕킹 + 억제 |
| 재시작 1/2/3 런 | **-27.8 / -28.0 / -28.0** | | 0.143 / 0.132 / 0.131 | 덕킹 잔류 없음(수정 전 1회차 -31.5) |

- 모든 MARK에서 리스너 1, BGM 소스 1(교차 페이드 외 중복 없음).
- 이번 회차에는 다른 앱 소리가 기본 장치에 섞였다(게임 마스터 0 구간에도 루프백 약 -26dB, 같은 시각 게임 세션 피크 0). 이 회차의 게임 출력 판정 근거는 tap과 OS 세션 미터이고 루프백은 참고다(1차 회차는 세 경로 일치).
- 최대 tap 피크 0.46 → 클리핑 없음. 사람 청취는 여전히 미검증(§4 한계).

## 6. G35 정식 측정 (단독 실행)

- 빌드: `CCT.Editor.BuildRelease.Windows64LoadTestBatch`(r16 소스 + CCT_QA_LOAD), `C:\UnityScratch\r14\loadtest`, Runtime SHA256 `21E99E2076E2BC3516F2BA7F0E4FEAD40FB5E2D1CF58C8153A53AB86F53149D7`. 실행 `Tools/Run-G35LoadTest.ps1 -Name r16-g35 -BuildPath C:\UnityScratch\r14\loadtest -Restarts 10`, 16:15:22~16:19:50 KST. 측정 중 내 다른 Unity/QA/ffmpeg 작업 없음(사용자 브라우저는 열려 있었다).
- 이전 시도 `r14-g35-final`은 경로 오류로 **시작하지 못했다**(증거 `Delivery/logs/r14-2026-10-08/g35-launch-failed-2026-10-08/`). 측정으로 쓰지 않는다.
- 검증 PC: Windows 11 Pro 10.0.22631, Intel i7-14700F(20코어/28스레드), RAM 16GB(3200MHz), NVIDIA GeForce RTX 5060(드라이버 32.0.16.1714, VRAM 7896MB, D3D12), 모니터 1920×1080@100Hz, 전원 고성능.
- 조건: Release 플레이어(debugBuild False), 1920×1080 FullScreenWindow, 효과 normal, vSyncCount 0, targetFrameRate 60, Quality "Ultra"(프로젝트 기본 품질), 준비 30초 + 측정 180초.
- 부하(측정 구간): 적 평균 160.0(최소 155 — 처치 직후 보충 프레임), 적 탄 평균 113(최소 100), 플레이어 탄 80, 장판 평균 10.3(최소 10), 픽업 평균 872(최소 418). 처치 11,137.

| 기준(10 §3) | 결과 | 판정 |
|---|---|---|
| 평균 FPS ≥ 58 | 60.00 (10,800프레임/180.01초) | PASS |
| p95 ≤ 25ms | 16.671ms (p99 16.681, 최대 16.854) | PASS |
| 반복 100ms 정지 없음 | 25ms 초과 0, 50ms 초과 0, 100ms 초과 0 | PASS |
| 프로세스 메모리 ≤ 1.5GB | 최대 working set 631.6MB, private 1144.3MB | PASS |
| 오류 로그 0 | errors 0, exceptions 0 | PASS |
| 재시작 10회 heap | 첫 3회 평균 12.318MB → 마지막 3회 12.848MB, 비율 1.043(<1.10), 루트 오브젝트 매회 3 | PASS |

판정: **G35 pass 제안**(검증 PC 1대 기준, 최소사양 보장 아님). 증거: `Delivery/Reports/performance.csv`(프레임별 10,800행), `Delivery/logs/r14-2026-10-08/g35-r16/`(load-summary.txt, load-log.txt, restart-heap.csv, ext-memory.csv 1초 외부 샘플, player.log, launch.txt). 60fps 제한 때문에 실제 여유는 측정하지 않았다. RC 고정 뒤 RC 소스로 다시 측정해야 한다.

## 7. 요구사항 표 좁히기 (근거별, 이번 wave)

테스트 이름만으로 pass를 올리지 않았다. "pass 제안"은 코디네이터 판정 전 상태이며 모두 프리뷰/QA 빌드 증거라 RC에서 다시 확인해야 한다.

| ID | 이전 | 이번 근거 | 상태 / 남은 것 |
|---|---|---|---|
| G02 | unverified | 4조합 모션 CSV 첫 행 pos (0,−2)·HP 100·Playing, 공격 구간 melee/ranged 반복, tour-log weaponArt 4종 실제 장착·provisionalHand False, 4조합×좌우 정지 화면/영상(1080·720). 루트 1개는 기존 `RunLifecycleStressTests` | **pass 제안**(RC 재확인) |
| G10 | unverified | 그라인더 실제 드라이버 녹화: charge_prepare(예고선 표시)→charge, 프레임별 보스 위치 CSV. 기존 `ChargerDriverTests.DashDirection_LocksAtTelegraphStart` | **pass 제안**(영상 판정은 코디네이터/아트) |
| G11 | unverified | 드립 drip→vanish(무적 열 1)→appear 녹화·CSV, 장판 수 열 | **pass 제안** |
| G12 | unverified | 머신 steam/rain/pressure 녹화, HP 29% 강제 후 패턴 | unverified 유지 — 9/13 단계 속도·압력 단독 시전 전용 단언 없음(다음 wave) |
| G13 | unverified | 진상 손님 refund→summon→review→reject 순서 녹화, HP 48% 강제 후 분노 | unverified 유지 — 고정 순서·분노 값·원래 강화 복원 전용 단언 없음 |
| G18 | unverified | 커피 트리 15노드 화면 1080·720 실제 렌더(`31_coffee_tree`) | **pass 제안** |
| G23 | unverified | 도감·상세 1080·720 현재 빌드 캡처 | **pass 제안** |
| G24 | unverified | 일시정지→설정→닫기, 일시정지→안내(키보드/마우스) 경로와 Pause 복귀 상태 로그. 결과 화면 OpenPause 결함 수정(결함 4) | unverified 유지 — 실제 키보드 입력 수동 검증 없음 |
| G26 | unverified | §6 출시 플레이어 프로세스 재시작 10회 heap 비율 1.043·루트 3 유지, 기존 20분/포기 증거 | **pass 제안**(RC 재측정) |
| G31 | unverified | §3.1 필수 화면 1080(테두리 없는 전체화면)·720(창) 실제 렌더 세트, D6 남은 경로(설정→안내, 마우스 안내) 포함 | unverified 유지 — 실제 키보드 전용 조작 증거 없음. 화면 세트는 확보 |
| G32 | fail | 363/363 원본 프레임 실제 엔진 노출 매핑(1080), 4무기 좌우 전환 연속 프레임, 결함 2·3 수정 | **fail 유지** — bartender/tea_sommelier provisional, 아트 동작 승인·카탈로그 정식화·엄격 검사 미통과 |
| G33 | unverified | §4·§5 실제 출력 경로(tap·OS 세션·루프백) 일치, 볼륨 0 완전 무음, Pause −8dB+전투 SFX 중단, 재시작 리스너 1·BGM 1, 피크 ≤0.47(클리핑 없음), 덕킹 잔류 결함 수정 | unverified 유지(좁힘) — 사람 청취, 22 SFX 개별 재생·루프 이음새 미검증 |
| G34 | unverified | 이번 wave 미실행 | 유지 — 출시 ZIP 없음(G32). r16 압축 해제 실행은 다음 wave |
| G35 | unverified | §6 30+180초 단독 측정, 전 기준 PASS | **pass 제안**(RC 재측정) |

코디네이터 지정 다음 wave 미검증 목록(그대로 남음): G03 대각 정규화·마우스 설정 전용 단언, G04 사거리 밖 다른 레인 미스·한 휘두름 1회, G05 1/3/8발 각도·관통 0/1/6·고속 스윕, G07 60/90/120초 경계·스폰 간격 공식·상한 100, G08 레몬·티백·케틀·잎 감시자·다도 수호자·커피 2종 강제 스폰 행동, G12/G13 위, G20 검·활 시작 3+3, G31·G24 실제 키보드 입력, G34 압축 해제 실행, G33 청취.

## 8. 이번 wave 최종 빌드 r16과 실제 통과 / 차단

- `Builds/FirstPassPreview-r16.zip` SHA256 `BB25CF0950721ADAC4B3E93F4156E67ED21745ACD72C4993BDE19028099097A9`, Runtime `f57cb8e1d1ae477eb0fb1a245f779d19a64a79a527915bd97a4f934f1da02f8b`. 포함: U1/U2/U3, 결함 1~4 수정, 총 2배. QA 타입 미포함 확인. 상태 파일 기준 아트 예외(bartender 정식 폴더 없음, bartender·tea_sommelier 1차 임시 허용)로 **엄격 출시 검사는 실패 상태 그대로**.
- r16 전체 테스트: EditMode **391/391**, PlayMode **140/140**, 실패·건너뜀 0(`Delivery/logs/r14-2026-10-08/final-r16/`). 프리뷰·QA·부하 빌드 로그 모두 `Result: Success`.
- `Play-FirstPass.cmd`는 r13 그대로(이번에 바꾸지 않음). r14/r15는 중간 산출물로 보존.
- 영상 갤러리 `Delivery/video/r14-2026-10-08/`: t1080, t720(3결함 수정·총 0.4), t1080-gun2x, t720-gun2x(총 2배), b1080(bestiary). 각 mp4 + `.frames.csv`, 모션은 `-zoom2x.mp4`(플레이어 중심 2배 nearest). 원본 연속 JPG는 `C:\UnityScratch\r14\runs\<run>\out\video\`에 보존.

**실제 통과(이번 wave 실측):** 1080/720 필수 화면 세트, E1/E2 재촬영, 4무기×좌우 idle/move/attack/hit/death 연속 프레임·타이밍 CSV, 보스 4종 패턴 연속 녹화, 363 원본 프레임 실제 노출(1080 363/363, 720 362/363), 총 2배(1.28/1.6 = 0.8, 좌우 손 거울 일치), 오디오 실제 출력 경로·볼륨 0·Pause·보상창·재시작, G35 전 기준과 재시작 heap, QA 도구의 일반 빌드 제외.

**차단 / 남음:**
1. G32 — 플레이어 2종 provisional, bartender 정식 폴더 없음. 아트 담당의 동작 승인(이번 영상·CSV 근거) → 카탈로그 정식화 → 엄격 검사. 이번에 manifest/catalog 상태 라벨은 바꾸지 않았다.
2. 아트 관찰 인계(수정하지 않음): 총 2배 후 개머리가 허벅지를 가림(grip 104/256), olive_fire 하단 직선 사각 띠, 자세 전환 시 손 위치 차이(0.44~0.81 world), 모든 defeat가 오른쪽 향함으로만 기록됨, 보스 패턴 CSV에 facing 없음.
3. G33 사람 청취, G31/G24 실제 키보드 입력, G34 r16 압축 해제 실행, G03/04/05/07/08/12/13/20 전용 단언(§7 목록).
4. RC 고정 후 전체 재검증(G35·G30·4무기 정상 완주 포함).
