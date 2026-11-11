# QA — 다음 웨이브 예고와 음료 준비 (2026-11-12, task_18f1ff2305e7)

기준 지시: 기획 `개발 기록/2026-11-12_다음웨이브예고_구현지시.md`. 기준 커밋 `cc787fb`. 증거 폴더: `Delivery/Reports/next-wave-preview-2026-11-12/`(git 미추적, 로컬).

## 1. 구현 요약

| 항목 | 내용 | 파일 |
|---|---|---|
| 확정 생성 계획 | 수집 웨이브는 (런 seed·절대 웨이브·반복 회차) 전용 seed로 목표 수만큼 기존 `EnemyRoster.PickEndless`를 미리 돌려 목록 확정·캐시. 박람회=작은 진상 손님, 보상=무해 보상 손님 고정 목록. 실제 생성(`SpawnOne`)이 같은 목록의 `Spawned` 번째 정의를 쓴다 → 예고 = 생성. UI 재열기·음료 변경·제출로 재추첨 없음(`PlansBuilt` 불변) | `Core/MasterWavePlan.cs`(신규), `Core/MasterChallengeDirector.cs` |
| 분류·문구 | 강적 = 기본 체력 ≥70 또는 정예 탄막, 돌진 = 속도 ≥3 또는 자폭, 나머지 무리. 60% 이상이면 그 이름, 아니면 혼합. 박람회·무해 보상은 사실대로. 특성: 자폭·멀리서 공격·제자리 소환·빠르게 달려옴·천천히 다가옴. 조언은 적 특성에 대한 일반 전투 조언(범위·관통 / 한 대상 화력 / 거리 유지·둔화·밀치기 '효과가 있다면')과 **이미 구현된** 순환 상성 안내만. 커피 추출·차 우림 공격 변화 문구 없음(시험으로 금지어 확인) | `MasterWavePlan.cs` |
| 준비 지점 | 새 단계 `MasterPhase.Prep`(값 4, 기존 값 유지): 런 시작·다음 주기 1웨이브 전. 수집 5/5 뒤 선택 대기도 준비 지점. 준비 중 `GameManager.MasterPrepActive` → timeScale 0(전투 시간·생성·피해 정지). 제조 창·일시정지·설정을 위에 열 수 있고 닫히면 `PlayScale`이 0을 유지. [전투 시작]·[반복]·[박람회]만 해제. 반복 선택 후 1웨이브는 선택이 곧 준비라 바로 진행 | `Core/MasterChallenge.cs`, `Core/GameManager.cs`, Director |
| 준비 패널 | 좌측 정보 패널과 우측 음료 버튼 사이(기준 1080: x 610–1550, y 130–). Prep 1블록(486)·선택 2블록(782). 블록: 제목·분류, 총 수·무리/강적/돌진·약화(접수 전 '제출에 따라 변동'), 대표 적 3종(기존 몸체 아트 첫 프레임·기존 이름·×수·체력 약 N·특성·[분류]), 그 외 n종, 조언·상성 안내. 버튼: [음료 만들기 B] [전투 시작] / [재료 모으기 반복] [음료 만들기] [박람회 도전]. 불투명(뒤 HUD 배너·피해 숫자 비침 방지) | `UI/MasterPrepView.cs`(신규), `UI/MasterChallengeHud.cs` |
| 사전 예고 | 전투 중(비모달) 우측 '음료 만들기' 아래 330×112 상자: "다음 · 웨이브 2 · 재료 모으기 2/5 / 무리 웨이브 · 10명 · 대표 적 ×n", 수집 5 진행 중에는 두 선택지 요약. 매 웨이브 모달 없음 | `MasterPrepView.cs` |
| 보존 | 박람회 제출 약화·스냅샷·전달, 반복(주기·난이도 불변), 박람회 성공 보상 1회, 보상 무해, 주기 증가 — 상태 로직 변경은 Prep 추가뿐. 세이브 형식 변경 없음(마스터 상태는 원래 저장하지 않음). 상태 기본 생성자는 Prep 없음(기존 규칙 시험 그대로) | — |

표시 수치의 확정 여부: 수·대표 적·순서는 **확정**(실제 생성과 동일). 체력은 '약 N' — 정의 체력×웨이브 배율×(1−약화)×현재 도전 체력 배율로 계산하며, 도전 배율(커피 스킬 해금 수)이 전투 중 바뀔 수 있어 확정값으로 표시하지 않는다. 박람회 약화는 접수 전 '제출에 따라 변동'.
설계 판단(사용자 의도 안): 계획은 해당 웨이브 직전(앞 웨이브 시작 시점)에 확정하므로 기존 시간 기반 트랙 가중치가 최대 한 웨이브 앞선 전투 시간으로 계산된다(재추첨 금지 요구의 결과, 난이도 규칙 자체 변경 없음).

## 2. 사용자 제보 조사 (작업 중 추가)

1) 직업·무기 선택 화면(1차 위치): 실제 QA 빌드·빈 격리 데이터로 100ms 연속 84장 + 효과 객체 목록 → 프레임 변화 0, 월드 스프라이트·파티클·VFX 풀 0, 메뉴 카메라 없음. 보이는 장식은 CafeUiFocus 청록 링(호버·키보드 포커스·선택)뿐 — 결함 근거 없음, 변경 없음. 증거 `realbuild-selectseq-fresh/`.
2) **도전 시작 첫 제조 창 우측 상단(확정 위치)**: 첫 열기 슬롯은 비어 있음(`realbuild-brewfx-fresh/x01,x02`). 제조 시 의도된 품질 효과(quality_master, 168px)가 빈 구석에 1회 재생되는데, 설명 문구는 HUD 배너로 나가 **불투명 제조 창 뒤에 깔려** 희미한 잔상만 보였다(`x03_craft_250ms.png`) → 맥락 없는 효과로 보임. 또 재생 중 창을 닫으면 UI 효과가 비활성으로 멈춰 다시 열 때 멈춘 프레임이 남을 수 있는 구조.
   수정: 효과는 유지, 제조 창 안 효과 왼쪽에 같은 설명 캡션 1.6초(창이 열려 있으면 뒤 배너 생략, 창 밖 호출은 기존 배너), 창 닫을 때 UI 효과·캡션·제출 컵 즉시 반납(`MasterVfx.ReleaseUi`). 후 캡처 `realbuild-brewfx-fresh-after/x03_craft_250ms.png`(캡션 표시·잔상 없음). 시험 `BrewWindow_QualityFx_CaptionInsideWindow_ReleasedOnClose_EmptyOnFirstOpen` PASS.

## 3. 시험 (빌드·컴파일과 런타임 분리)

| 종류 | 결과 | 증거 |
|---|---|---|
| 컴파일 | 오류 0(모든 실행) | `*.log` |
| EditMode | **50/50 PASS**: 신규 `MasterWavePlanTests` 7(결정성·목표 수·반복 회차 변화·박람회/보상 고정·분류·금지어·예상 체력 = 생성 식(실측 95)·Prep 경계: 시작/중복 시작/선택 대기/반복 무준비/박람회→보상→다음 주기 Prep·약화 8% 전달·제출 가능) + 기존 마스터·끝없는 모드 43(헬퍼가 [전투 시작] 후 진행). 1차 6/7은 내 시험 오류(30웨이브 중 박람회·보상 15개는 고정) → 수정 | `editmode-5.xml` |
| PlayMode | **27/27 PASS**: 신규 `MasterPrepPlayTests` 4 — 준비 정지(실제 1초 동안 피해·시간·생성 0, 접촉 적 배치), 제조 창 열고 닫기·일시정지 1초 카운트다운 복귀 후에도 정지·같은 계획·`PlansBuilt` 불변, [전투 시작] 후 실제 생성 목록 = 계획 순서, 전투 중 사전 예고 상자; 웨이브 1 중 예고한 웨이브 2 계획 = 실제 생성, 선택 대기 예고 2개(반복=1웨이브 반복1·박람회), 제출해도 박람회 계획 불변·약화 8 예상치, 반복 선택 실제 생성 = 예고; 준비 중 포기 정상 종료; 제조 창 품질 캡션·닫기 반납. 기존 MasterChallengePlayTests(전체 1주기 실제 프레임: 반복·박람회·현수막 정지·보상·다음 주기 Prep 단언 추가)·MasterArtPlayTests·EndlessModePlayTests·CharacterSelectTests | `playmode-4.xml` |
| 전체 suite | **NOT RUN**(관련 묶음만 — 지시 범위) | — |

## 4. 실제 Windows 빌드 런타임

| 검사 | 결과 | 증거 |
|---|---|---|
| QA 빌드(CCT_PLAY_VERIFIER, 자동 조작 — 사람 플레이 아님) 준비 패널 | 런 시작 Prep: timeScale 0·runTime 0.00·생성 0, 패널 줄 = 계획(`events.log` prep 줄), [전투 시작] 실제 버튼 → 진행, 수집 1~5 → 선택 패널(정지) → 박람회 → 현수막 → 박람회 군중, 오류·예외 0 | `realbuild-shots-4/events.log` |
| 720p(실제 창 ScreenCapture) / 1080p(게임 렌더 텍스처) 읽기·겹침 | 준비 패널(1블록), 선택 패널(2블록), 전투 중 사전 예고 상자 — 좌측 정보 패널·우측 버튼·하단 상태 줄과 겹침 없음, 글자 잘림 없음(육안) | `realbuild-shots-4/shots/s04b_prep_wave1_{720,1080}.png`, `s07_await_choice_{720,1080}.png`, `s05_combat_wave1_{720,1080}.png` |
| 수정 전 캡처(비교) | 반투명 패널 뒤 배너 비침·1블록 빈 공간·박람회 '상성 없음' 중복 → 수정 | `realbuild-shots-1/shots/` |
| 검토 빌드 실제 창 | 부팅·타이틀 표시(입력 없는 창 캡처), 예외 0. **OS 입력 조작 BLOCKED**: 사용자 앱이 전경(pid 8576)이라 전경 확인 스크립트가 입력 0회로 중단(포커스 강탈하지 않음) | `review-build-real-window/00_boot_title_noinput.png`, `os-input.log` |
| 사람 직접 플레이 | **NOT RUN / 미완료** — 자동 조작으로 대체 통과 주장하지 않음 | — |
| 원본 세이브 | 모든 실행 `-cct-data-dir`(복사/빈 격리) + `-logFile`. 사용자 폴더 save/settings/drinks/Player.log 메타 불변(TestResults.xml만 — Unity 시험 실행기 사본) | `usersave-meta-before/after.txt` |
| 게이트 | Unity·플레이어 매 시작 직전 free RAM ≥4096MB·동시 1개(07:0x 3,725MB·07:43 4,035MB로 2회 거부 후 대기) | 로그 GATE 줄 |

## 5. 빌드 / 패키지 / 해시

- 검토 빌드 `Builds/NextWavePreview-Review-2026-11-12/`(별도 폴더, 이전 검토·공모전 빌드 미변경). 소스 2,809 파일 빌드 전·후 **e743ceb16e55f5ab331133e6ce6abbc80430a185e2523e13a0243ed5a6b29335** 동일(`source-manifest-review-before/after.sha256`); 이전 작업 기준(`cc787fb`, b6328b56…) 대비 변경은 위 파일 + QA 전용 PlayVerifier*.cs. ProjectSettings `5900a053…` 불변.
- 산출물 208 파일 목록 sha256 `2410ea9a2450349b95acfa54efac8eb3c0f2cfe0220c6b956f1308a7e31c1abc`, exe `3070e580…d653`, CCT.Runtime.dll `77a5adfe…1a8b` — DLL 내 QA 문자열(`QaDirectHit/MechanicsFlow/ShotsFlow/SelectSeqFlow/BrewFxFlow/PlayVerifier/HandleMasterPrep`) 0건, 기능 문자열(`MasterWavePlan/MasterPrepView/PrepStart/ReleaseUi`) 있음.
- ZIP `Delivery/CocktailCoffeeTea_NextWavePreview_2026-11-12.zip`(209 항목, DoNotShip 제외, README_실행안내·THIRD_PARTY_NOTICES) sha256 **5ec101bb7215b44ae924e080542526fde71003beb00b743d5bce01f0b6ce10f3**. 이전 패키지 `…SteamMasterReview_2026-11-11.zip`(86e91edb…)·`…_r2.zip`(28ce6e0b…)·공모전 `…WindowsFirstSale_2026-11-02.zip` 보존·해시 확인.
- 공개 배포·Steam 업로드 없음.

## 6. 미검증 / 후속

- 사람 직접 플레이, 실제 창 키보드·마우스 조작(전경 잠금/사용자 사용 중), 연속 녹화.
- 다음 주기(웨이브 11) 준비 패널의 실제 빌드 화면 캡처(자동 1주기 실행은 박람회 군중 장면에서 종료) — PlayMode 실제 프레임 시험으로만 확인.
- 커피 추출별·차 우림별 공격 변화: 후속 기획, 미구현(예고 문구에서도 추천하지 않음).
- 예고 체력은 현재 도전 배율 기준 '약' 값.
