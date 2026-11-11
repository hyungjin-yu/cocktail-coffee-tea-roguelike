# QA — Steam 본편 마스터 도전 (2026-11-11)

범위: 마스터 도전 루프 + 런 제조/음용/제출 + 인수 아트(NPC 현수막·품질·상성 VFX) 연결. **Steam 업로드/공개 배포 아님.** 공모전판·사용자 세이브 미접근. Git 변경 없음(코디네이터 소유).
재개 세션 세부(기준선 스냅샷, 파일 해시, 필수 경로 JSON)는 `qa-steam-master-resume-2026-11-11.md`.

## 환경 / 설정

- Unity 6000.3.25f1, `-batchmode`(컴파일·EditMode는 `-nographics`, PlayMode·캡처는 그래픽 장치만), 동시 1개, 창/포커스/입력 없음.
- 저장 격리: 시험 어셈블리 `TestSaveIsolation`(세션 임시 save/settings/drinks). PlayMode 배치 실행은 신규 `Tests/PlayMode/QuietBatchAudio.cs`로 AudioListener 음량 0(시험 전용, 빌드 미포함).
- 증거 출력: `Delivery/Reports/steam-master-2026-11-11/`.
- **자원 규칙 위반 기록**: free RAM 4GB 규칙 미달 상태에서 2회 실행 — import-art-2(3,659MB), evidence-resume-2(3,789MB, 확인값을 출력만 하고 게이트하지 않음). 두 실행 모두 정상 종료, 사용자 앱 영향 확인 안 됨. 이후 실행은 게이트 후에만.

## 1. 빌드/컴파일

| 실행 | 시작(KST) | 범위 | 결과 |
|---|---|---|---|
| import-art-1 | 12:31:55 | 체크포인트 코드 + MasterArt/임포터 | PASS (오류 0) |
| import-art-2 | 12:39:34 | + 상성 clip, MasterVfx, 상성/품질 연결, FairBannerShow 재작성, 신규 시험 | PASS (오류 0, 경고 0) |
| editmode-resume-1 | 12:44:40 | + schema v5, 시험 기대값 갱신 | PASS (컴파일 포함) |
| playmode-resume-1 | 12:45:37 | + QuietBatchAudio | PASS (컴파일 포함) |
| 이후 변경(현수막 배율 0.8·위치 1.45, 시험 핀 배율) | 12:50 이후 | | **NOT RUN** (RAM 3,662MB) |
| 별도 Windows 검수 빌드 | — | | **NOT RUN** |

## 2. 기능 시험 (자동)

| 시험 | 결과 | 근거 |
|---|---|---|
| EditMode: MasterArtTests 3, MasterChallengeRule/RunCupLedger/Game 25, EndlessRewardLedger, EndlessMode, IntegerOnly (총 95) | **94 PASS / 0 FAIL / 1 SKIP**(`Migration_UserSaveCopy_IsolatedRun` 기존 Ignored — 사용자 세이브 복사 시험, 의도적 미실행) | `editmode-resume-1.xml` |
| schema v5: v4 파일 → `.pre-runcups-v4.bak` 1회 백업·원본 동일·준비 음료 보존·다음 저장 v5 | PASS | 같은 XML `OldV4File_WithoutRunCups_LoadsEmpty_PreparedKept` |
| 미래 버전(v6) 파일 쓰기 금지 | PASS | `FutureVersionFile_IsProtected_NoWrite` |
| PlayMode: MasterChallengePlayTests(5 수집→반복→직접 도전→정지→박람회3→보상2→다음 주기, 제조 창 정지/강제 팝업 없음) | PASS 2/2 | `playmode-resume-1.xml` |
| PlayMode: EndlessModePlayTests(정규 모드 회귀 포함) | PASS 3/3 | 같은 XML |
| PlayMode: 현수막 인수 아트 일정·발 고정·07프레임 글자·퇴장 방향·최종 핀-손 정렬(오차 < 0.01) | PASS (배율 1 기준) | `BannerShow_UsesAcceptedArt_*` |
| PlayMode: 3초/4초 비례 일정 | PASS | `BannerShow_ScheduleScalesWithPrepSeconds_3And4` |
| PlayMode: 품질 VFX 거래당 1회·연타 교체·풀 재사용 frame0·끝나면 반납·품질 없음 무효과·저이펙트 low 교체 | PASS | `QualityVfx_*` |
| PlayMode: 상성 VFX 상성 적중만 1회·미일치/DOT 없음·연타 간격 건너뜀·반납·동시 상한 6 | PASS | `AffinityVfx_*` |
| 위 PlayMode 현수막 시험의 배율 0.8 변경 후 재실행 | **NOT RUN** | |

## 3. 시각 런타임 (오프스크린 캡처, 실제 게임 씬)

| 실행 | 결과 | 비고 |
|---|---|---|
| playmode-resume-1 안의 증거 시험(첫 판, 클래스 필터로 함께 실행) | FAIL | 상성 적중 후 VFX 조회 null(`MasterArtPlayTests.cs:238`). 증거 시험을 "적 적중점에 PlayWorld 배치"로 바꿈(적중 경로 판정은 `AffinityVfx_*`가 PASS). 이 판의 부분 프레임은 내가 생성한 출력이라 삭제 |
| evidence-resume-1 | 시험 PASS / 화면 FAIL(무효) | 자동 타이틀 캔버스가 런 화면을 덮음 → `frames-run1-titleoverlay/`에 보관, TitleView 제거 후 재실행 |
| evidence-resume-2 | 시험 PASS, 프레임 26장 + notes | `frames/` (720p 전부, 1080p 현수막 6·품질 최고 1) |

육안 검수(evidence-resume-2, 현수막 배율 1·위치 1.15 — **수정 전 상태**):

| 항목 | 판정 | 관찰 |
|---|---|---|
| 현수막 위치 | **FAIL** | 현수막 윗부분이 HUD 레벨 막대 3줄(720p y≈20–80px) 밑에 깔려 천 상단·핀이 가려짐(`banner_1700ms_720.png`, `banner_2050ms_720.png`) |
| "박람회" 글자 | PASS(가독) / 겹침 위험 | 크림 천 위 갈색 글자 읽힘. 정지 중 HUD 웨이브 배너 글자와 세로로 맞붙음 |
| NPC 접근/설치/퇴장 | 부분 확인 | 두 NPC가 벽 영역 카운터 높이에 서고 방향 정상. 손-핀 접점은 레벨 막대에 가려 육안 판정 불가 → NOT RUN 취급 |
| 펼침 중 손-천 분리 / 발 미끄럼 | NOT RUN | 정지 프레임으로 판정 불가, 영상 필요 |
| 품질 VFX(제조 창 슬롯) | PASS(표시) | 오른쪽 위 슬롯에 3등급 형태 구분, 머리글·첫 잔 버튼과 겹침 없음. 효과가 작아(112px@1080 기준) 강한 피드백인지는 사용자 판단 필요 |
| 제출 컵 | PASS(표시) | 제출 때 기존 컵 64×52 표시 |
| 상성 VFX | PASS(표시) / 작음 | 적 위에 원두·거품·잎 형태 보임. 갈색 적 위 원두는 작고 대비 낮음(`crop` 확인), 무기 자체 섬광과 겹침 |

**수정**: 현수막 배율 1→0.8, 상단 위치 1.15→1.45 월드(레벨 막대 바로 아래, 폭 8월드가 좌측 정보 패널·우측 음료 버튼 열 사이에 들어가도록). 계산상 겹침 해소지만 **재캡처·재시험 NOT RUN**.

## 4. 남은 체크 (NOT RUN / BLOCKED)

- 현수막 배율 0.8 상태 PlayMode 재실행 + 재캡처(손-핀 접점 육안) — BLOCKED(RAM < 4GB 시점).
- 3.5초 실제 연속 재생 영상(발 미끄럼·펼침 중 손-천 분리) — NOT RUN.
- 전체 정규 모드 회귀(전 EditMode/PlayMode) — NOT RUN.
- 별도 Windows 검수 빌드, source/build 해시 일치, 격리 세이브 실플레이, 화면 영상, 새 설명서 — NOT RUN.
- 사용자 실제 화면 검수 — NOT RUN(GUI 슬롯 미요청).

## 5. 재개 명령 (RAM ≥ 4096MB 확인 후 하나씩)

```
U="C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe"; P=C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing; D=$P/Delivery/Reports/steam-master-2026-11-11
"$U" -batchmode -projectPath $P -runTests -testPlatform PlayMode -testFilter "CCT.Tests.MasterArtPlayTests|CCT.Tests.MasterChallengePlayTests|CCT.Tests.EndlessModePlayTests" -testResults $D/playmode-resume-2.xml -logFile $D/playmode-resume-2.log
# (클래스 필터는 Explicit 증거 시험도 함께 돈다 — frames/ 덮어씀)
```
