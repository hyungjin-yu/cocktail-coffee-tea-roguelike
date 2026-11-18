# QA — 피드백 후속: 추가 검수 + 19·25·26 (2026-11-16, task_89556b5c8894)

선행: [최종 검증·검수본](qa-feedback-all-2026-11-16.md). 기획 루트 `AGENTS.md` 준수. 증거 루트 `Delivery/Verification/2026-11-16/followup/`.
QA 빌드 `Builds/Feedback-QaTour-2026-11-16b`(3차), 격리 데이터 `Builds/Feedback-QaTour-2026-11-16b-qa-data/<모드>`. 모든 투어·실행은 `-cct-data-dir` 격리 + 음량 0. 커밋·푸시·공개 배포·PC 종료 없음. ArtSource 미편집.

증거 구분: **[A]** Unity 자동 시험, **[F]** QA 빌드 자동 조작 fixture, **[OS]** 실제 Win32 `SendInput` 스캔코드 키 입력(사람 아님 — 실제 OS 입력 경로만 검증), **[H]** 사람 직접 플레이·청취·OS IME = **NOT RUN**(fixture로 대체 주장하지 않음).

## 1. 자동 시험·빌드

| # | 항목 | actual | 증거 | 상태 |
|---|---|---|---|---|
| A1 | EditMode 관련 9묶음(FeedbackBatch2/3/4, TipsyBadgeSettings, SettingsV2, MobileTouch, HiddenSkill, QaToolIsolation, UiReadability) | 51/51 통과 | `editmode-1.xml` | PASS |
| A2 | PlayMode 관련 7묶음(FeedbackBatch4Play, PersistentBuffRoundFeedback, EndlessMode, MobileTouchPlay, GameSmoke, HybridWeapon, BattleArenaPlay) | 38/38 통과 | `playmode-1.xml` | PASS |
| B1 | QA 투어 빌드 1·2·3차 | Success ×3(2·3차는 QA 투어 코드만 변경 — A1·A2 대상 코드 불변) | `build-qatour*.log` | PASS |
| B2 | 새 검수본 `Builds/Feedback-Review-2026-11-16b`(strict 릴리스) | Success, 544MB | `build-release.log` | PASS |
| Z1 | ZIP `Delivery/CocktailCoffeeTea_FeedbackReview_2026-11-16b.zip` | 109,780,431 B, 207 파일, `testzip None`, SHA-256 `70798b617e52b3744223041631af63bb2b1010076165bba0bd1cb481ca01e908` | `…zip.sha256` | PASS |
| Z0 | 기존 검수본 보존 | `Builds/Feedback-Review-2026-11-16`(08:46) 그대로, 기존 ZIP SHA-256 `3630850f…e874` 재계산 일치 | 세션 기록 | PASS |

전체 시험은 직전 작업(985/393 통과) 뒤 이번 변경 범위에 맞춰 관련 묶음만 다시 돌렸다(지시: 변경 없는 전체 반복 금지).

## 2. 추가 검수

### 2-1 좁은 화면비·터치 UI [F]

| 조건 | 투어 | 결과 | 증거 |
|---|---|---|---|
| 4:3 데스크톱 1440×1080 | feedback1·2·3 | 34/34/24장, errors=0 exceptions=0 | `narrow43-feedback1/2/3/` |
| 터치 UI 20:9 1920×864 (`-cct-touch-ui`) | feedback1·2·3 | 34/34/24장, errors=0 exceptions=0 | `touch209-feedback1/2/3/` |
| 터치 UI 거의 정사각(폴드형) 1200×1080 | feedback3 | 24장, errors=0 exceptions=0 | `touchfold-feedback3/` |

화면 확인(대표): 터치 카드 선택에 숫자 키 안내 숨김(8번 규칙), 조이스틱·히든·커피 연구 버튼과 재료 알림·HP 경고 비겹침(`touch209-feedback3/f3_1920x864_bartender_rapier_11_card_rarity_goal.png`, `_30_low_hp.png`, `touchfold-feedback3/…_11_…png`, `…_20_award_toast_chips.png`).
한계: 실제 모바일 기기·실제 터치 입력 NOT RUN(데스크톱 창 + 터치 UI 강제 플래그). 폴드 실측 해상도(1664×1499)는 모니터 높이 제한으로 1200×1080 대체.

### 2-2 이동 끊김 13 — 군중·맵 경계 A/B [F] (`smooth13x`, 1920×1080)

강제 경로: 체력 증가(접촉은 받음), 런 시간 10분으로 이동, **무기 끄기**(1차 실행에서 대량 처치 → 레벨업 창 연쇄로 계측이 오버레이 정지에 덮임 → 2차부터 무기 꺼서 처치 없음), 경계 구간 시작점으로 순간 이동.

| 구간 | 보간(현재) 멈춘 프레임 / 화면 x σ | 보간 없음(이전) | 비고 |
|---|---|---|---|
| 군중(적 47~78) 4방향 × 두 직업 | 0% / 0.82~2.20px | 49.8~50.2% / 2.74~5.63px | 보간 없음 바텐더는 오른쪽·왼쪽만 측정(이후 원인 미상 오버레이로 2구간 + 경계 누락), 티 소믈리에는 전 구간 |
| 경계: 벽 쪽 이동(벽 정지 프레임 제외) | 0% / 0.01px (카메라 고정 구간) | 51% / 4.50px | 벽 닿은 뒤 150프레임은 정지가 정상이라 제외 |
| 경계: 벽에서 빠져나오기(카메라 고정 → 추적 재개) | 0% / 0.90~0.91px | 49.8% / 4.61px | 카메라 고정 181프레임 |
| maxDt | 10.1~10.3ms | 10.1~10.2ms(군중 1회 30ms) | 프레임 시간 급등 없음 |

증거: `smooth13x-interp/`, `smooth13x-nointerp/`(`tour-log.txt`, `smooth13x_*.csv`, 군중·경계 캡처). 이전 실행 `-run1`, `-run2`는 실패 원인 기록용으로 보존.
판정: 군중·경계에서도 보간이 멈칫을 없앤다(PASS, 측정). 체감 [H] NOT RUN.

### 2-3 실제 릴리스 정상 흐름 + 저장 격리 [OS]

- 대상: 새 검수본 릴리스 exe(QA 코드 없음), `-cct-data-dir Builds/Feedback-Review-2026-11-16b-input-data`, 1280×720 창, 음량 0.
- 입력 경로: `Tools/QA/Send-GameScanKey.ps1`(Win32 SendInput 스캔코드, 앞 창 PID 확인 후에만 전송), 화면은 `Tools/QA/Capture-ComputerAction.ps1`(orca computer 앱 상태 캡처). 사람 입력 아님.
- 순서(증거 `release-input/00~20_*.png`, `*-input.json`, `move-input.jsonl`, `menu-input.jsonl`, `actions.jsonl`): 타이틀(첫 Enter = 창 포커스·연출 건너뛰기 후 시작 버튼 포커스) → Enter 로비 → Enter 바텐더 → Enter 레이피어(포커스가 "영업 시작"으로 이동) → Enter 첫 안내 → Enter 전투 → D×4·W×2(0.5초) 실제 이동, 처치 1·HP 85/100 → Esc 일시정지 → ↓×4 "런 포기" → Enter 확인 창(기본 포커스 "취소") → → "포기" → Enter → 결과 화면("런을 포기했습니다", 성장·조합 보기, 획득 내역).
- 저장: 격리 `save.json` `totalRuns=1 abandonedRuns=1 activeRun=null`, Player.log `Exception` 0, `[Launch] data dir` 격리 경로.
- 실제 사용자 저장 폴더(`AppData/LocalLow/DefaultCompany/CocktailCoffeeTea`, 34 파일) 전후 SHA-256: 게임 저장 파일 전부 동일. 바뀐 것은 Unity 엔진 자체 분석 파일(`./Unity/…/Analytics/ArchivedEvents`, `Insights/…/session_end_event`, `ShaderVariantAnalytics`)뿐 — 엔진이 실행마다 쓰는 파일로 게임 저장 아님(`realsave-before/after.sha256`).
- 상태: PASS(실제 OS 키 입력 경로). 마우스 클릭·한글 IME·사람 체감 NOT RUN.

## 3. 19 화살 속성 표현(구현)

- 내용: 원래 화살(파편 아님)이 직접 적중 때 붙이는 화염/여운과 **같은 조건**(`flameLevel>0 && !FlameLocked`, `poisonLevel>0 && !PoisonLocked`)이면 화살 머리 색 + 꼬리 점. 화염 = 주황, 여운 = 녹색, 둘 다 = 주황 머리 + 녹색 꼬리. 저이펙트 = 머리 색만. 활 화살(`arrow`/`tea_arrow`)만, 총알 제외.
- 불변: 피해·확률·습득(카드)·판정 반지름(`HitRadius`)·속도·관통·크기(로그 scale 0.7053 동일). 새 화살 종류·중복 피해 없음, 삭제된 화염/독 지대 미부활.
- 검증: [A] `FeedbackBatch4Tests` 3건 PASS. [F] `feedback4-720/1080/f4_*_10~13_arrow_{base,flame,bitter,both}.png` + 로그(원소·색·꼬리 색).
- 한계: 꼬리 점은 전용 그림이 없어 원형 임시. 찻집 녹색 바닥에서 녹색 꼬리 대비가 낮음 → 전용 아트(아래 요청)로 해결 권장.

## 4. 25 히든 스킬 공통 HUD 차별화(구현, 최소안)

- 큰 개별 패널 없이 기존 지속 효과 칸(종류 모양: 충전 = 남은 시간 막대, 준비 = 굵은 띠, 누적 = 칸 점, 조건 충족 중 = 테두리)을 유지하고 **히든별 모티프 그림**을 기존 승인 아트로 붙였다: 얼음 찻잔 `ice_shard`, 탄산 펌프 `skill_ingredient`, 셰이킹 리듬 `hit_spark`, 각성 `skill_attack`, 주말 러시 `melee_streak`, 허브 정원 `leaf_mote`, 바 템포 정지 `skill_survival`/이동 `skill_attack`, 두꺼운 머그 커피잔. 투명 여백 잘라 1회 생성 후 재사용(0.1초 갱신 누수 방지).
- 추가: 충전형 **탄산 펌프** 칸(12초 주기 남은 초, 읽기 전용 `MagnetRemaining`). 순간 발동형은 기존 머리 위 표식·상세, 상시형은 H 상세 그대로.
- 검증: [A] `FeedbackBatch4Tests.HiddenIcons_MapToExistingApprovedArt_Cached` PASS, `PersistentBuffRoundFeedbackPlayTests` PASS. [F] `feedback4-*/f4_*_20_hidden_tiles.png`(회복·보호막·흡인·머그 4칸 모두 그림, 로그 `+icon`).
- 신규 아이콘(선택, 승인 시): 공용 4종 `hidden_kind_charge/stack/proc/state`(24×24) 또는 히든별 `hidden_<id>`(24종, 24×24). 현재 재사용 그림 중 `melee_streak`(질주)·`hit_spark`(콤보)는 의미 연결이 약하다.

## 5. 26 취기 — 선택형 분위기 표시 프로토타입(구현, 원문 26 전체 완성 아님)

- 코디네이터 승인 범위: 설정 "취기 표시 (시험)"(기본 꺼짐, `settings.json` `tipsyBadge`, 키 없으면 꺼짐, 기본값 복원 = 꺼짐). 켰을 때만 **끝없는 모드(마스터 도전)**에서 지금 실제로 마신 음료(`Player.DrunkDrink`)가 칵테일이면 HUD 칸 "취기 · 표시만" 1개, 상세 "분위기 표시 전용: 능력치·조작·보상 변화 없음".
- 실제 경로 확인(코드): `DrunkDrink`는 출발 준비 음료(`GameManager` 219행, 1잔 규칙) 또는 마스터 도전 런 잔 마시기(`DrinkRunCup`, 1047행)에서만 설정되고 판 종료 때 해제(1099행). 새 음용 경로 추가 없음. 1잔 규칙이라 누적 단계 없음, 난수 없음.
- 넣지 않은 것: 조작 반전, 판정·능력치·보상 배수, 화면 번짐, 캐릭터 흔들 걸음(지시의 "예상 불이익 없는 시각 배지"에 맞춰 제외 — 기획 26의 '캐릭터 반응·절제된 시각 효과' 후보와 다름).
- 검증: [A] `TipsyBadgeSettingsTests`(기본 꺼짐·예전 파일·저장 재로드·설정 화면 "표시만"·기본값 복원) PASS, `FeedbackBatch4PlayTests` 2건(끝없는+칵테일만 표시, 켬/끔에도 공격·공속·이속·최대 체력·HP 동일, 포기 뒤 해제, 기본 모드·커피·마신 음료 없음 미표시, 일시정지 중 숨김·재개 후 표시) PASS. [F] `feedback4-*/f4_*_01/02_settings_tipsy_*.png`, `_30_tipsy_Endless.png`(음료 칸 옆 취기 칸), `_31_tipsy_Standard.png`(없음), `_32_tipsy_paused.png`, 로그 `stats … same=True`.
- 미검증: 런 중 잔 마시기 경로의 화면(로직은 같은 `DrunkDrink`), 결과 화면·로드 직후의 화면 캡처(시험으로 판 종료 해제만 확인).

## 6. 아트 integrated / verified

| 자산 | 상태 |
|---|---|
| 라떼·플랫화이트·카푸치노 잔 + mask, 거품 층, 잠금 배지, 산지 원두 아이콘 | **integrated 0 / verified 0** — Codex 아트 워커 `ArtSource/feedback-art-2026-11-16/manifest.blocked.json` `ready_assets: []`(PixelLab 버전 차단). 게임은 기존 그림 임시 유지 |
| 19 화살 꼬리, 25 히든 아이콘 | 신규 아트 없음(코드 원형·기존 아트 재사용) |

## 7. 변경 파일(이번 작업)

- `Assets/_Project/Scripts/Player/PlayerProjectile.cs` — 19 화살 속성 표시.
- `Assets/_Project/Scripts/Player/HiddenSkillRuntime.cs` — `MagnetRemaining`(읽기 전용).
- `Assets/_Project/Scripts/UI/ActiveEffects.cs` — 25 히든 그림·탄산 펌프 칸, 26 취기 칸.
- `Assets/_Project/Scripts/Meta/SettingsService.cs`, `Assets/_Project/Scripts/UI/SettingsView.cs` — 26 설정.
- QA 전용(`CCT_QA_TOUR`): `Assets/_Project/Scripts/Core/AutoTourFeedback4.cs`(신규), `AutoTourSmooth.cs`(smooth13x), `AutoTour.cs`(모드 등록).
- 시험: `Assets/_Project/Tests/EditMode/FeedbackBatch4Tests.cs`(신규), `Assets/_Project/Tests/PlayMode/FeedbackBatch4PlayTests.cs`(신규).

## 8. 남은 것

- 최종 아트(차단 해소 후 manifest 수신 → 반입·화면 재검수).
- [H] 사람 직접 플레이·청취·한글 IME·실제 모바일 기기.
- 보간 없음 바텐더 군중 2구간·경계 재측정(현재 보간 결과는 완전, 비교 기준은 티 소믈리에·이전 실행으로 충분).
- 19·25 전용 아트 결정(요청표 §3·§4).
