# 마스터 도전(Steam 본편) — 구현 현황

작성 2026-11-11. 브랜치 `codex/coffee-brewing`, 기준 HEAD `50daa0ae`(WIP 체크포인트) + 이후 미커밋 작업. **Steam 업로드/공개 배포 아님.** 공모전판(pps-contest-2026)은 변경하지 않았다.
검증 상태는 `Delivery/Reports/qa-steam-master-2026-11-11.md`가 기준이다. 이 문서는 "무엇을 어디에 만들었나"만 적는다.

## 1. 플레이 루프 (RunMode.Endless 런 = 표시명 "마스터 도전")

| 항목 | 구현 | 위치 |
|---|---|---|
| 10웨이브 주기: 수집1~5 / 박람회6~8 / 보상9~10 | `MasterRules.SegmentOf/InCycle/CycleOf` (절대 웨이브 long) | `Scripts/Core/MasterChallenge.cs` |
| 수집5 완료 → 선택 대기(반복 / 박람회 도전), 자동 진입 없음 | `MasterChallengeState.ChooseRepeat/ChooseFair`, HUD 비모달 선택 패널 | 같은 파일, `Scripts/UI/MasterChallengeHud.cs` |
| 반복은 같은 주기·난이도·감소율 유지(반복 횟수는 표시만) | `ChooseRepeat` → `CycleStart(Cycle)` | |
| 도전 접수 → 남은 적 처리 대기(삭제·보상 누락 없음) → 현수막 설치 | `MasterPhase.ClearingForFair` → `Transition` | `Scripts/Core/MasterChallengeDirector.cs` |
| 현수막 3~4초(기본 3.5) 동안 전투/스폰/피해/시간 정지, 중복 입력 무시 | `GameManager.BeginMasterFreeze/EndMasterFreeze`(timeScale 0·IsPaused), 실제 시간 타이머 | `GameManager.cs`, Director |
| 박람회 3웨이브: 원본 보스와 분리한 작은 진상 손님(boss=None, 클리어 플래그 없음) | `MasterDefs.FairCustomer` (HP60·접촉7 제안값) | Director |
| 보상 2웨이브: 절대 웨이브1 HP, 공격 0, 행동 없음, 모든 공격 경로 무피해 | `MasterDefs.RewardMonster` + `Enemy.Harmless`(접촉·자폭 경로 차단) + `EnemyRoster.AttachBehavior` 미부착 + `MasterRules.Apply` 0 보존 | Director, `Enemy.cs`, `EnemyRoster.cs` |
| 유한 목표 스폰량, 실제 처치로 완료, 생성 큐 보존(버리지/폭주하지 않음) | `TickWave` 해제량·프레임 예산 3·동시 상한 | Director |
| 박람회 성공 시 모든 현재 재료 데이터 기본 묶음 1단위, 주기 ID당 1회 | `FairRewardItems`(DrinkSupply.bundle×1), `RecordBossAward(runId, "master_fair_c{n}", endless=true)` | Director |
| 고웨이브 오버플로 | decimal 계산·long 포화, `SumWaveCounts` 포화 | `MasterChallenge.cs`, `EndlessRules` |

## 2. 런 내 제조·음용·제출

- 우상단 "음료 만들기 [B] · 가능 n · 보관 m" 단일 버튼. 재고 변화 때 문구만 갱신, 창 강제 팝업 없음. 제조 창 동안 전투 정지.
- 런 임시 재료 → 부족분 로비 재고 순서로 한 트랜잭션 차감. 잔마다 출처(fromRun / fromStockNormal / fromStockEndless) 기록.
- 잔 하나는 마시기 또는 제출 중 하나만(`UseRunCup`). 로비 준비 음료는 제출 불가.
- 런 종료/비정상 종료 복구 시 보관 잔은 로비 재고를 원래 출처로 환원, 사용한 잔의 런 재료는 정산에서 차감, 중복 정산 없음(`SettleRunCups`).
- 음용은 기존 활성 음료 교체 규칙(중첩 없음, 구효과 역적용·현재 HP 보존). 실제 마신 음료의 순환 상성만 적용.
- **저장 스키마 v5(2026-11-11 재개분)**: `runCups` 추가에 맞춰 `DrinkStore.CurrentVersion` 4→5. v4 이하 파일은 처음 읽을 때 `drinks.json.pre-runcups-v{n}.bak`로 1회 백업, 값 변환 없음. v4 빌드는 v5 파일을 미래 버전으로 보고 쓰지 않는다(보관 잔 = 이미 차감된 재고의 환원 근거 소실 방지). 단일 로비 `prepared` 슬롯은 그대로.

## 3. 성장·약화 수치 (조정 가능, **사용자 미확정 제안값** 표시)

| 규칙 | 식 | 기본값 |
|---|---|---|
| 제출 감소율 | R = round(points × p / (100 × C)), 일반 잔 100p / 최고 품질 커피 150p | p = 5 (제안) |
| 적용 | 박람회 접수 때 스냅샷 확정 → 박람회와 다음 주기 수집에 한 번씩. 누적 없음, 제출은 그 주기 소비 | |
| 최종 능력치 | max(1, round(base × (1 − R/100))), base ≤ 0이면 0 유지(보상 몬스터) | |
| 칵테일 다양성 | 공격 +U × 2% (U = 이번 런 성공 제조 칵테일 정식 레시피 ID 고유 수, 이름·양·얼음·물 무시) | 2% (제안) |
| 커피 품질 | Q = round(Strength01 × 100) 0~100, 보통 0~59 / 좋음 60~84 / 최고 85~100 | 코디네이터 승인 첫 값 |
| 최고 품질 효과 | 기존 효과(이미 Strength01 반영) × 1.05, 항목 상한 유지, Strength 중복 없음 | |
| 차 숙련 성장 | **미구현**(사용자 미확정) | |

## 4. 인수 아트 연결 (2026-11-11 재개분)

- `Resources/MasterChallenge/` 110 PNG = ArtSource 핵심 15 clip 83 프레임 + `affinity/` 6 clip 27 프레임, 바이트 일치 복사(원본 미수정).
- 임포트: `CCT.Editor.MasterChallengeArtImport.ImportBatch` (PPU128, Point, Clamp, 무압축, mipmap 없음, pivot NPC (.5,.125) / 현수막 (.5,1) / VFX (.5,.5)) + `Validate()` 110 프레임 검사.
- 표: `Scripts/Core/MasterArt.cs` (clip별 노출 ms, loop 여부, 3500ms 일정, 핀·손 좌표, flipX 대칭 `x = 255 − x`).
- **현수막 연출** `FairBannerShow`(Director 파일): rigger 왼쪽(정방향, 앞손), porter 오른쪽(접근 flipX, 설치·퇴장 정방향, 뒷손). 설치 손 좌표가 최종 핀에 맞도록 발 위치 계산. 접근 0–1000 / 설치 1000–2500 / 퇴장 2500–3500(코디네이터 승인 재분배 — 원래 manifest 800/2300), 현수막 1250–2250 마지막 프레임 고정, 글자 "박람회"는 07 프레임(2125ms)부터 짙은 갈색. NPC는 화면 밖이 아니라 설치 위치에서 실측 보폭 속도(52px/걸음, 600ms 2걸음 → 배율1.10에서 1.49월드/초)×1초만큼 떨어진 곳에서 400ms 투명도 전환과 함께 나타나 걸어오고, 퇴장도 같은 거리 후 사라짐(걸음과 이동 속도 일치). 설정 길이 3~4초에 비례. 배율 0.8(폭 8월드)·상단 = 카메라 위에서 1.45월드(HUD 레벨 막대 아래 — 2026-11-11 13시 재캡처로 겹침 해소 확인). 카메라에 붙여 박람회 동안 화면 위 벽 영역에 유지, 보상 구간 시작 때 제거.
- **펼침 줄(런타임 보조)**: 펼침 프레임 00–04 동안 손(설치 최고점 앵커)에서 그 프레임의 천 끝 핀(`MasterArt.BannerPinsPx`, 승인 PNG 알파에서 측정)까지 갈색 2px급 줄을 그려 "줄을 당겨 펼친다"로 연결. 발은 고정(미끄럼 없음). 05부터 끝 = 최종 핀이라 줄 없음. 아트 원본 아님 → 아트 검수 대상.
- **HUD 배너 조정**: 현수막이 걸린 동안 박람회 제목은 천 글자("박람회 n/3"), HUD는 큰 제목 없이 짧은 부제만(HUD 큰 제목 y≈190/1080이 천과 겹쳤음). 설치 시작 부제 "NPC가 현수막을 거는 중 · 전투 정지". "박람회 성공!"은 보상 1웨이브 배너에 합쳐 표시(이전에는 같은 호출 사슬에서 보상 웨이브 제목에 즉시 덮여 보이지 않았음).
- **품질 VFX** `MasterVfx.PlayUi`: 제조/음용/제출 성공 1건마다 제조 창 오른쪽 위 112px 슬롯에 1회. 연타 시 이전 것 즉시 반납 후 교체(겹침 없음). 제출 때 기존 컵 `DrinkLab/dl_coffee_cup`(64×52, 새 컵 자산 없음) 표시. 저이펙트 설정이면 `_low`로 교체.
- **상성 VFX** `MasterVfx.PlayAffinity`: `Enemy.TakeDirectHit`에서 상성 추가 피해 A > 0이 확정된 직접 적중에만 1회(DOT·0 피해·무적·미일치 없음). 동시 6개 상한, 0.06초 간격 예산 — 넘으면 건너뜀(판정 불변).
- 풀: 끝나면 renderer 끄고 sprite 해제, 비활성 후 반납. 재사용 시 frame0·elapsed0·white·scale1·rotation0·flip false. 런 종료(`GameManager.EndRun`)·GameManager 파괴 때 `MasterVfx.ReleaseAll()` — 정지 상태로 끝난 런의 효과가 다음 런의 동시 상한을 차지하던 문제 수정.

## 5. 알려진 한계 / 미완

- 현수막 펼침 00–04 손-천 간격은 런타임 줄로 연결(위). 전용 "당기며 옆걸음" 아트가 생기면 줄을 대체 가능 — 아트 결정 사항.
- 발 미끄럼: 이동 속도를 승인 걷기 그림 보폭에 맞춰 해결(시험 단언: 100ms 이동 = 보폭 속도×100ms). 대신 등장/퇴장은 화면 밖 보행이 아닌 근거리 투명도 전환 — 연출 선택으로 아트/사용자 검수 대상.
- 품질 VFX는 커피만(칵테일·차는 품질 값 없음 → 효과 없음, 제출 컵만).
- 진상 손님/보상 몬스터 수, HP, 생성 속도, p, 종류당 증가율은 제안값.
- 세계 1등 대회, 차 전용 숙련 성장, 스킨 재작업은 범위 밖.
