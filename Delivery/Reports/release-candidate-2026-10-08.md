# 출시 후보(RC) 작업 보고 — 2026-10-08

## 최신 상태: 사용자 요청에 따른 오늘 마감

사용자 **“거기까지 적용시키고 오늘은 그만해”**에 따라 완료 범위를 새 기획31~35의 코드·데이터·UI 적용 및 컴파일/변경 기본 시험으로 변경했다. 최신 소스의 정식 RC 완료를 주장하지 않는다. 정확한 적용 내용, 실패 수정 감사, 오늘 최종 시험/소스 해시/프로세스 종료 및 다음 명령은 [today-resume-2026-10-08.md](today-resume-2026-10-08.md)에 기록한다.

기존 f073 strict 빌드/ZIP/압축 해제/성능 근거는 `Delivery/PreviousRC-f073-2026-10-08` 및 기존 빌드 경로에 보존한 **31~35 이전 후보**다. 새 UI8ID의 최종 실제1080/720 승인과14상태 매핑은 아직 없으며 `unityVerified/strictReleaseReady=false`를 유지한다. 최종 전체 Edit/Play, 새 4무기 정상 완주, 최종 형태 실제 부하 G35/G30, strict 새 ZIP 및 새 절대 경로 Editor 없는 G34, launcher 갱신은 다음 재개 작업이다. 오늘 새 GUI/완주/성능/ZIP/Steam 작업을 시작하지 않았다.

Orca task `task_dfb062acc4d7` / active dispatch `ctx_f8ab73b715be` (이전 `ctx_9da512c4dbc5` 사용량 제한 중단 후 인계30으로 재개). 기준: Unity 6000.3.25f1, 기획 04~11·14·26~30, `Delivery/Reports/release-readiness-2026-10-08.md`, `requirements-matrix.md`, 아트 검수 `ArtProduction/release-review-2026-10-08/motion-final-review.md`와 요청 CSV 2종.
보존: 사용자 저장, 미커밋 작업(커밋 없음), r13~r16 빌드·ZIP, ArtSource/ArtProduction(아트 담당 소유 — 이번에 편집하지 않음), 기획 문서(편집하지 않음). Steam 게시·계정·결제 없음. 사용자 앱/게임·agent 프로세스 종료 없음.

## 진행 중 — 아직 RC 완료 판정 아님

이 보고는 현재 작업 근거를 보존하는 중간 기록이다. 최종 strict 빌드, ZIP, 압축 해제 실제 입력 검증과 최종 G01~35 표가 아래에 확정되기 전 game_complete를 주장하지 않는다.

## 인계 소스의 수정과 검증 감사

- 증기/돌진 경고만 GroundZone 정렬로 변경했다. 증기 폭1.4, 사거리12, 예고0.8초, 피해24는 그대로다. 아트 담당은 rc-t1080/rc-t720 원본 화면에서 플레이어/보스 윤곽 가림 해소를 확인했다.
- 사망은 즉시 GameOver/timeScale0/저장·실패집계1회를 확정하고, defeat를 unscaled 재생한 뒤 1.2초 후 결과 패널을 연다. 이번 인계 후 모든 타이틀/재시작/파괴 경로에서 예약 코루틴을 명시 취소하고 결과 표시 전에 Instance 동일성도 확인한다. 이미 있던 재시작 중 옛 결과 미노출 행동시험의 애니메이터 null을 통과로 치던 대체값은 제거했다.
- 총 아트는 1.28/0.64=2.000배를 유지한다. 기존 구현이 확인한 총구 뒤 0.5~0.7 world 표적의 역방향 발사는 표적이 총구 뒤일 때만 몸 중심에서 발사하도록 보정했다. 좌우0.5/0.7/2 world 실제 발사 직후 전방 속도와 명중을 검사하며 사라진 탄에 대한 조건부 단언으로 대신하지 않는다. 피해/사거리/속도/발수/관통은 보존했다.
- 압력 시전이 진행 중 증기 패턴을 취소할 때 pendingWarnings도 제거한다. 기존 실패 기록은 r14-play.xml 162/163, playf.xml 3/4이며 압력 리셋 이후 정상 재충전을 0이라고 단언하던 테스트가 수정되어 최종 전체 재실행 대상이다.
- AudioSpecTests는 원본 목록22/3, 실제 Resources AudioClip PCM의 채널·48kHz·피크·루프 이웃표본 비교, 중복50/30ms·동일ID4·전체24 한도를 검사한다. 인계 후 전체 PCM NaN/Infinity도 확인한다.

## 기존 추가 화면의 완성 근거

| 실행 | 실제 완료 marker | 원본 경로 |
|---|---|---|
| rc-t1080 | shots203 / recordedFrames7916 / errors0 / exceptions0 | C:/UnityScratch/r14/runs/rc-t1080/out |
| rc-t720 | shots203 / recordedFrames7916 / errors0 / exceptions0 | C:/UnityScratch/r14/runs/rc-t720/out |
| rc-b1080 | shots19 / recordedFrames5628 / errors0 / exceptions0 | C:/UnityScratch/r14/runs/rc-b1080/out |
| rc-b720 | **중단, 완료 아님** — swirl 구간 이후 marker 없음 | C:/UnityScratch/r14/runs/rc-b720/out |

`Delivery/logs/rc-2026-10-08/coverage-before/body-122-coverage.csv`: 요청122/122 방향 매핑. `common-88-coverage.csv`:87/88 실제 renderer 매핑(ui_save_error 미노출). 매핑은 시각 승인을 대체하지 않는다. 아트 담당 ctx_db39a1e69bf4는4무기 death00..03 L/R 결과 패널 이전 노출, hybrid 주검+보조활 L/R, warning_line 가림 해소를 직접 확인했으며, 적 사망00..02는 hit_spark 가림 때문에 원본 포즈 전용 추가촬영을 요청했다.

추가 모드 `AutoTour additional`은 CCT_QA_TOUR 전용이다. 요청된17적 사망L+customer R에서만 hit_spark를 제거하며 일반 게임의 효과·판정은 바꾸지 않는다. 보스 등장 경로를 호출한 뒤 화면 중앙으로 옮겨 boss_arrival00..03을 관찰했다. additional의 Martini 탄은 실제 glass_shard여서 ice 증거로 쓰지 않고 별도 rc2-ice에서 실제 ShakerGolem/RangedAttackDriver의 ice_shard와 정상 충돌·수명195프레임을 촬영했다. rc2-add1080 첫 시도는 소멸 DefeatVisual의 ?. 접근 예외로18프레임 뒤 중단했다. 자체 QA 프로세스만 즉시 종료하고 Unity null 검사로 수정했으며 이 실패를 완료 증거로 사용하지 않는다. 저장 오류45f 뒤 정상 saveFailed=False 로그만으로 화면 복구를 주장하지 않고, rc2-save-recovery의 오류10f/정상15f에서 실제 HUD True→False를 보완했다.

## 정상 플레이와 오디오 파일의 현재 근거

`C:/UnityScratch/pv/runs/rc1-bow/out/events.log`: done anyClear=True/errors0/exceptions0,1회 클리어685.9초, 원두101/커피노드10/카드17, totalRuns1/cleared1/activeRun=null. 입력 대행8방향/정상 버튼만 사용한 런이며 HP·재료·시간·보스 조작이 없다. 이전 rc1-gun은 약756초에서 중단된 기록이라 클리어로 쓰지 않는다. 레이피어/검의 새 저장 rc2-rapier/rc2-sword는 각각690.3/701.9초 최초 클리어, errors0/exceptions0, 레이피어 hybrid=True, 원두109/101·노드10을 확인했다. 이들은 인계 중간 빌드이며 최종 고정 소스4무기 런의 대체물로 쓰지 않는다.

`Delivery/Reports/audio-original-files.csv`: 실제 원본 WAV25개=SFX22/BGM3,48kHz/16bit,mono/stereo 규격, 디지털 클리핑 표본0, 피크/양끝/루프 접합 측정25/25 통과. 실제22SFX 개별 출력과3BGM 한 루프 이상 출력 tap/Windows 게임 세션 미터를 완료했고 `audio-review.md`, `audio-output-loops.json`, `audio-sfx-output-rc3.md`에 기록했다. 초단 SFX를 누락하던 분석 첫창 제외를 수정했고 정상 Run22/22 실제 출력>0,3BGM 한주기뒤 파형 RMS오차0/클리핑0이다. 사람 청취·음색/체감 균형 판정을 했다고 주장하지 않는다. G33의10문서 명시 객관 조건을 임의 면제하거나 사람 청취를 추가 필수조건으로 만들지 않는다.

## 배포 고지 감사

`Tools/QA/package_notices.py`는 실제 player Managed DLL과 PackageCache asmdef 이름을 대조하고 해당16패키지의 원문 LICENSE/THIRD PARTY NOTICES를 `Delivery/Licenses/Packages`에 복사했다. Mathematics Noise의 실제 LICENSE와 copyright도 포함한다. 실제 배포 DLL에 VisualScripting/Multiplayer.Center.Common이 있어 기존 Editor-only 설명은 틀리며 최종 고지를 수정한다. InputSystem은 Unity Companion License, VisualScripting/Multiplayer.Center는 각 원문의 Unity Package Distribution License이며 일괄 Companion으로 설명하지 않는다. 최종 strict DLL 목록으로 다시 대조한다.
