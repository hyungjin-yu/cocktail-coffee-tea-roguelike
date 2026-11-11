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

---

## 6. 재개 세션 2 (2026-11-11 13:00~, 작업 task_8c0850e54a49, 기준 HEAD b0dc5b78)

모든 Unity 실행은 게이트 스크립트로만 시작: 실행 직전 free RAM ≥ 4096MB이고 Editor 미실행일 때만, 아니면 실행 거부(로그 `GATE` 줄). 이번 세션에서 규칙 위반 실행 없음. 거부 1회(13:23:49, 3,878MB).

### 6.1 실행 결과

| 실행 | 시작 | free RAM | 범위 | 결과 |
|---|---|---|---|---|
| playmode-resume-2 | 13:00:07 | 5,537 | 배율0.8·위치1.45 + 연속프레임 | 정규 9 PASS, 증거 시험 FAIL(상성 PlayWorld null) |
| playmode-resume-3 | 13:03:24 | 5,090 | + ReleaseAll, HUD 배너 조정, 성공 배너 | **10/10 PASS** |
| playmode-resume-4 | 13:06:17 | 4,987 | + 펼침 줄 | **10/10 PASS** |
| editmode-full-1 | 13:08:01 | 4,897 | 전체 EditMode | 933 PASS / **1 FAIL** / 1 SKIP / 1 Inconclusive |
| editmode-stdfamily-rerun | 13:08:54 | 4,904 | 실패 fixture 단독 | 2/3 (재현됨) |
| editmode-stdfamily-rerun2 | 13:10:36 | 4,888 | 시험 수정 후 | **3/3 PASS** |
| playmode-full-1 | 13:10:57 | 4,786 | 전체 PlayMode | **358 PASS / 0 FAIL / 11 SKIP**(전부 기존 Explicit/Ignored 증거 캡처) |
| playmode-resume-5 | 13:22:17 | 4,684 | + 최소 16:9 시작 위치·16:9 캡처 | **10/10 PASS** |
| playmode-resume-6 | 13:23:49 | 3,878 | + 화면 밖 여백 0.7×배율 | **GATE 거부 — NOT RUN** |

전체 EditMode Inconclusive 1건: `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`(기존, 이번 변경과 무관 — 미조사).
전체 PlayMode는 resume-5 이전 코드(16:9 시작 위치 수정 전) 기준.

### 6.2 발견·수정한 결함

| # | 결함(증거) | 수정 | 검증 |
|---|---|---|---|
| D1 | 배율1·위치1.15에서 현수막이 HUD 레벨 막대 밑에 깔림 (`archive-scale1/frames-scale1-top1.15/`) | 배율0.8·위치1.45(이전 세션) | resume-3 이후 캡처에서 겹침 없음 PASS |
| D2 | 상성 증거 시험 null: 앞 시험이 채운 월드 VFX 6개가 timeScale 0 동안 끝나지 않고 풀이 GameManager보다 오래 살아 동시 상한 점유 | `MasterVfx.ReleaseAll()` — `GameManager.EndRun`·`OnDestroy`에서 호출 | resume-3~5 PASS |
| D3 | 설치/박람회 동안 HUD 큰 제목(y≈190/1080, 노란 글자)이 천 아랫부분과 겹쳐 대비 낮음 (`frames-run1`류 2050ms) | 현수막이 걸린 동안 제목은 천 글자("박람회 n/3"), HUD는 부제만. 설치 시작 부제 "NPC가 현수막을 거는 중 · 전투 정지" | `banner_settled_hud_720/1080.png` PASS, `MasterChallengePlayTests` 단언 추가 PASS |
| D4 | "박람회 성공!"이 같은 호출 사슬의 보상 1웨이브 제목에 즉시 덮여 보이지 않음(코드 확인) | 보상 1웨이브 배너에 합쳐 표시 | `LastBannerTitle == "박람회 성공!"` 단언 PASS |
| D5 | 720p에서 "박람회 시작! 현수막 설치 완료 · …" 부제가 너무 길어 좌측 정보 패널 밑으로 들어감 | "박람회 시작! 진상 손님 N명 막기(· 약화 R%)"로 단축 | `banner_settled_hud_720.png` PASS |
| D6 | 펼침 00–04 동안 천은 중앙에서 펼쳐지는데 NPC 손은 최종 핀 위치 → 손이 빈 손(`archive-scale1/frames-run3-before-cords/banner_1300ms_*`) | 발 고정, 손 앵커→그 프레임 천 끝 핀(승인 PNG 알파에서 측정한 `BannerPinsPx`)까지 런타임 줄 | 시험 단언(줄 시작=손, 끝=프레임 천 끝, 05부터 없음) PASS, 연속 프레임 육안 PASS(줄은 얇아 720p에서 잘 안 보일 수 있음 — 아트 검수 대상) |
| D7 | 배치 화면 비율이 16:9보다 좁아 16:9 화면에서 NPC가 첫 프레임에 화면 가장자리에 갑자기 나타남 (`archive-scale1/frames-run4-aspect-popin/seq/seq_0000.png`) | 시작·퇴장 위치를 최소 16:9 폭 기준으로 계산, 증거 캡처는 16:9 고정 | resume-5 `seq_0000.png`에서 NPC 없음 PASS, 16:9 시작 단언 PASS |
| D8 | 정규 모드 회귀 `StandardFamilyTableGameTests.TeaHouse_AllFamilies_*` FAIL — 시험이 EditMode에서 비어 있는 `Enemy.Active`(OnEnable 미호출)를 검사. 같은 fixture의 `Default_JobTheme_*`는 검사 대상 0개로 공허하게 통과 중이었음. 11-10 중단 작업의 미검증 시험(게임 결함 아님) | 두 시험 모두 실제 생성 계층(`root.GetComponentsInChildren<Enemy>`)에서 수집 + "실제로 생성됨" 단언 | rerun2 3/3 PASS |
| D9 | D7 수정 뒤 이동 거리 증가 → 접근 속도 ≈9.5 월드/초(0.8초) | 화면 밖 여백 1.6→0.7×배율(몸 절반 폭 0.375×배율 측정) | **NOT RUN**(GATE 거부) |

### 6.3 연속 프레임 동작 검수 (resume-5, 0~3500ms 100ms 간격, 720p)

증거: `frames/seq/seq_0000..3500.png`, `frames/seq-contactsheet-top.png`(D7 수정 전 run4 기준), `frames/seq-top.gif`, `frames/frames-notes.txt`(프레임별 NPC 좌표·clip·현수막 프레임).

| 항목 | 판정 | 근거 |
|---|---|---|
| 발 접지(지면 y 고정) | PASS | 전 프레임 NPC y=2.798 일정, 설치 800–2300ms 발 x 고정 |
| 손-핀(최종) | PASS | 단언 오차 < 0.01, 1700–2300ms 프레임 육안 |
| 손-천(펼침 중) | PASS(런타임 줄) / 아트 검수 필요 | D6 |
| 천 펼침·글자 | PASS | 1050–2050ms 8프레임, 1925ms부터 "박람회" |
| HUD 가림 | PASS(현수막·글자) / 경미 | 접근 0–800ms에 rigger 다리가 좌측 "히든" 패널 뒤로, porter 머리가 우측 "커피 포인트" 글줄과 잠깐 교차(이동 중 0.8초 한정) |
| 걷기 속도 대비 발 미끄럼 | **FAIL(미해결)** | 접근 6.8→(D7 후)9.5 월드/초, 퇴장 4.6 — 걷기 600ms 주기 대비 빠름. D9로 완화 시도(NOT RUN). 근본 해결은 계약 일정(접근 0.8초) 조정 또는 더 가까운 등장 위치 결정 필요 — 코디네이터/사용자 결정 |
| 소리 | 무음 | `QuietBatchAudio` |

### 6.4 남은 것

- D9 이후 targeted PlayMode 재실행·재캡처 — NOT RUN(RAM).
- Windows 검수 빌드(`BuildRelease.Windows64ReleaseToBatch -cct-preview-out Builds/SteamMaster-Review-2026-11-11`)와 source hash 기록 — 7절 참고.
- 실제 창 실행 영상·사용자 화면 검수 — NOT RUN(GUI 슬롯 미요청).

## 7. Windows 검수 빌드 / source hash

- 빌드: **NOT RUN** — 13:23:49(3,878MB)·13:26:21(3,775MB) 두 번 GATE 거부. 대기 루프 없이 중단.
- 빌드 전 source manifest(빌드 입력 전체: Scripts·Resources·Scenes·Packages manifest/lock·ProjectSettings): `Delivery/Reports/steam-master-2026-11-11/source-manifest-pre-build.sha256`, 2,801 파일, 목록 sha256 **ce035c052997c437752a6ab4bec24c6b45698fa4f3ed692a00053ac98b8964c7**.
- ProjectSettings.asset 빌드 전 sha256 `5900a05320859108adc3530f1b41a3290510db78d00c59a7b0498ad7e11bda8c`(bundleVersion 이미 1.0.0 — 빌드 스크립트가 같은 값을 쓰므로 변경 없어야 함, 빌드 후 비교 필요).

### 재개 명령(각 실행 직전 게이트, 하나씩)

```
G="bash Tools/QA/gated-unity.sh"   # 게이트: free RAM ≥ 4096MB, Editor 미실행일 때만 Unity 시작
P=C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing; D=$P/Delivery/Reports/steam-master-2026-11-11
# 1) D9 검증 + 증거 재캡처
$G -batchmode -projectPath $P -runTests -testPlatform PlayMode -testFilter "CCT.Tests.MasterArtPlayTests|CCT.Tests.MasterChallengePlayTests|CCT.Tests.EndlessModePlayTests" -testResults $D/playmode-resume-6.xml -logFile $D/playmode-resume-6.log
# 2) bash Tools/QA/source-hash.sh $D/source-manifest-build.sha256 — 빌드 직전 source manifest 재계산 → ce035c05… 와 일치 확인(다르면 그 차이를 기록)
# 3) 별도 검수 빌드(기존 Builds/Windows64·공모전 빌드 덮어쓰지 않음)
$G -batchmode -quit -projectPath $P -executeMethod CCT.Editor.BuildRelease.Windows64ReleaseToBatch -cct-preview-out Builds/SteamMaster-Review-2026-11-11 -logFile $D/build-review-1.log
# 4) 빌드 후 source manifest 재계산·ProjectSettings 해시 비교, exe/Data sha256 기록
```
게이트 스크립트 내용(재작성용): `powershell (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1024 < 4096 → exit 99`, `Get-Process Unity` 중 경로가 `Hub\Editor`인 것이 있으면 exit 98, 아니면 Unity.exe 실행.

## 8. 재개 세션 3 (task_13015f497cb6, 13:28~, 기준 HEAD 38c2d5ac)

모든 Unity 실행은 `Tools/QA/gated-unity.sh`(free RAM ≥ 4096MB·Editor 미실행) 통과 후 하나씩. 거부 0회. 전체 suite 반복 안 함.

### 8.1 발 미끄럼 보정 (D9 대체, 코디네이터 승인: 총 3.5초 안 재분배 허용)

- 실측(승인 ArtSource 읽기만): 걷기 6노출 600ms = 2걸음, 발 사이 최대 폭 77px − 발 길이 ≈25px → **걸음당 52px**. NPC 배율 1.1011 → 자연 보행 1.49 월드/초. 기존 화면 밖 등장은 6.8–9.5 월드/초(보폭의 5–6배)라 3.5초 안 어떤 분배로도 해결 불가.
- 변경: 일정 접근 0–1000 / 설치 1000–2500 / 퇴장 2500–3500, 현수막 1250–2250(설치 시작+250ms 유지), 글자 2125ms부터. NPC 이동 거리 = 보폭 속도 × 1000ms(1.49 월드), 시작·끝 400ms 투명도 전환(화면 밖 보행 대신 근거리 등장/퇴장). 3·4초 설정에서도 걸음과 이동이 같은 계약 시각을 써 비율 유지.
- 판정: 발-이동 속도 일치 **PASS**(시험 단언: 100ms 이동 = 보폭 속도×100ms, 오차 1e-3; 연속 프레임 x 증분 0.149/100ms 일정). 연속 프레임 육안 PASS(`frames/seq-walk-contactsheet.png`, `frames/seq-rigger-walk-zoom.png`, `frames/seq-top.gif`). 남은 판단: 근거리 페이드 등장이 연출로 자연스러운지 — 아트/사용자 검수(실제 재생 영상은 NOT RUN).
- HUD: 새 이동 범위에서 NPC가 좌측 정보 패널·우측 "커피 포인트" 글줄과 겹치지 않음(접근 시작 rigger x≈292px@720, 패널 오른쪽 끝 350px보다 위쪽 벽 영역) — 육안 PASS.

### 8.2 실행

| 실행 | 시작 | free RAM | 결과 |
|---|---|---|---|
| playmode-resume-7 (MasterArt·MasterChallenge·EndlessMode + 증거) | 13:30:47 | 4,761 | **10/10 PASS** |
| build-review-1 (`BuildRelease.Windows64ReleaseToBatch`) | 13:33:37 | 4,769 | **Build Finished, Result: Success**, 544MB, 22초 |

빌드 로그의 "Error"는 `[Licensing::Module] Access token is unavailable` 1건(라이선스 토큰 갱신, 빌드 결과 무관).

### 8.3 Windows 검수 빌드 / source hash

- 출력: `Builds/SteamMaster-Review-2026-11-11/` (기존 `Builds/Windows64`·공모전 빌드 미변경). 검수 안내: `Delivery/Reports/steam-master-2026-11-11/REVIEW-BUILD.md`(원본 세이브 격리 `-cct-data-dir` 필수).
- 빌드 입력 소스 2,801 파일 목록 sha256 **2f46718fcd88ae5af318b7a4d3ba1a2d9df36028cb38bb8d77875142facb876e** — 빌드 전·후 **완전 동일**(`source-manifest-build-before/after.sha256`, diff 없음). ProjectSettings.asset 전후 `5900a053…` 동일.
- 산출물 208 파일 목록 `build-review-files.sha256`, 목록 sha256 `a52f83a293199dce1d0910789933faf17ff635a7b7c681e569cbef8ece9e3a8e`; exe `3070e580…d653`, CCT.Runtime.dll `095b1976…908c`.
- 빌드 실제 실행(창)·실플레이·영상: **NOT RUN** — 창을 띄우려면 GUI 슬롯 요청 필요, 이번엔 요청하지 않음.

### 8.4 기존 Inconclusive 원인

`IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`: 전제 `Assume.That(!DrinkArt.Has("assam_tea"))`가 거짓 — 지금은 `Resources/DrinkLab/dl_tin_assam_tea.png`가 있어 "틴 아트 없는 차" 사례가 사라짐. NUnit Assume 실패 = Inconclusive(설계대로), 게임 결함 아님. 시험은 다른 미보유 차를 찾도록 고치거나 제거할 대상(이번 범위 밖, 미수정).

### 8.5 남은 것

- 검수 빌드 실제 창 실행·마스터 도전 1주기 실플레이·화면 영상(사용자/GUI 슬롯).
- 아트 검수: 펼침 줄(런타임 보조), 근거리 페이드 등장/퇴장, 품질 VFX 크기, 갈색 적 위 원두 가독성.
- 스킨 저장 경로가 `-cct-data-dir`를 따르는지 미확인.

## 9. 세션 4 (task_5415dfdd3cb7, 15:00~15:55) — 세이브 격리 확인, 실제 Windows 빌드 1주기, 사용자 현수막 결함 수정

### 9.1 저장 경로 격리 (정적 + 실측)

| 저장물 | 경로 결정 | `-cct-data-dir` 따름 |
|---|---|---|
| save.json(+guest) | `PermanentProgress.PrimaryPath`/`ActiveSavePath` ← `SetPathForTests(dataDir/save.json)` | 예 |
| settings.json | `SettingsService.SetForTests(dataDir/settings.json, 메모리 구형 저장소)` — PlayerPrefs는 읽기 전용 이전값, 데이터 폴더면 메모리 대체 | 예 |
| drinks.json(v5, 런 잔) | `DrinkLab.ActivePath = PathForSave(ActiveSavePath)` | 예 |
| 최근 기록 | `RunHistory.ActivePath = PathForSave(ActiveSavePath)` | 예 |
| **스킨** | `WeaponSkins.cs:178` `Path.GetDirectoryName(ActiveSavePath)` | 예 |
| 상점 상태 | `CommerceState.cs:121` 같은 방식, `SkinCommerceRunner.Start()`(다음 프레임, Apply 이후) | 예 |
| 순서 | Boot 씬 스크립트 0개, BeforeSceneLoad 훅 없음, `GameBootstrap.AutoStart` 첫 줄이 `LaunchOptions.Apply()` | — |
| 캡처 도구 | `-cct-*-capture` + `-cct-data-dir` 둘 다 있을 때만 | — |
| Unity Player.log | 엔진이 persistentDataPath에 씀 → **`-logFile <격리폴더>`로 우회** | 인자로 처리 |

실측(파일 이름·크기·수정시각 메타데이터만, 내용 미열람): 실제 사용자 폴더 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea`의 save/settings/drinks(+.bak)·Player.log·Player-prev.log는 실행 전후 **동일**(`usersave-meta-before.txt` / `usersave-meta-after-run1.txt` / `usersave-meta-after-all.txt`). 바뀐 것: `Unity\…\Analytics·Insights·ShaderVariantAnalytics`(엔진 원격측정 캐시, 플레이어 실행마다 갱신 — 게임 저장 아님)와 `TestResults.xml`(**Unity 에디터 시험 실행기가 -testResults와 별도로 persistentDataPath에 사본을 씀** — 이전 세션 시험부터 있던 파일, 게임 저장 아님). 결론: 게임 저장 격리 **PASS**, 엔진 부산물은 이 인자로 격리 불가(보고).

### 9.2 실제 창 직접 조작 — BLOCKED → 자동 조작 대체

검수 빌드(PID 25424, HWND 1051610, 격리 데이터 run1 확인)를 띄웠으나 `orca computer --restore-window`가 전경 전환을 못 함(Windows 전경 잠금; 전경은 Orca pid 39092 유지, capabilities `windows.focus=false`). 전경 강탈(전역 키 주입 등)은 금지라 직접 GUI 조작 **BLOCKED**, 입력 0회. 게임은 정상 종료(내 프로세스). 동시에 시작한 ffmpeg 창 녹화는 종료 중 손상되어 삭제(가려진 창이라 Orca 화면이 찍혔을 것).
대체: QA 전용 `PlayVerifier`(CCT_PLAY_VERIFIER 심볼 빌드에만 컴파일)에 `-cct-playverify-master` 흐름 추가(`Scripts/Core/PlayVerifierMaster.cs`). **자동 조작**: 이동은 키보드와 같은 8방향 입력값(AutoInputOverride), 화면 선택은 실제 버튼 onClick(사람 간격 ≥0.45초). HP·적·생성·재료·시간·무적·보상 값은 쓰지 않음. **시간 단축 없음(실제 시간)**, 체력 보정·치트 없음. 캡처는 게임 자신의 화면(ScreenCapture)이라 다른 창 포커스·OS 입력과 무관. 소리: 격리 settings 볼륨 0.

### 9.3 실제 Windows 빌드 마스터 도전 1주기 (자동 조작, 실제 시간)

| 실행 | 빌드 | 결과 |
|---|---|---|
| realbuild-master-run1 | 수정 전 | FAIL(시험 흐름 결함: 모드 버튼은 무기 단계에서 생김) → 흐름 수정 |
| realbuild-master-run2 | 수정 전(화면 고정 현수막) | 1주기 완료, 오류 0·예외 0 |
| realbuild-master-run3-userfix | 월드 고정(플레이어 최근접 칸) | 1주기 완료, 오류 0 — 좌측 HUD 가림 발견 |
| **realbuild-master-run4-final** | 최종(HUD 회피 칸 선택) | **1주기 완료, 오류 0·예외 0** |

run2(제조·제출 경로를 모두 탄 판) 실측: 수집1~5 도달(실제 125초) → 반복 클릭(웨이브1·주기1·반복1, 난이도 그대로) → 다시 1~5 → 음료 만들기(전투 정지 timeScale 0) → 커피 2잔 제조(좋음62 quality_excellent, 최고100 quality_master) → 최고잔 마시기(활성 음료 교체, DrunkDrink=Coffee) → 좋음잔 제출(1잔·100p·예상 약화 5%, 컵 표시) → 닫기 → 박람회 도전(스냅샷 5%) → 남은 적 0 → **현수막 정지: 측정 3.27초(측정 시작이 전환 시작보다 늦음, 설정 3.5) 동안 HP 변화 0, 전투 시간 +0.07초(전환 종료 뒤 대기 프레임분)** → 박람회 3웨이브(부제 "박람회 시작! 진상 손님 12명 막기 · 약화 5%") → 성공 보상 1회 87종·endless 장부, "박람회 성공!" 표시 → 보상 2웨이브 **HP 180→최저 180, 무해 개체만(비무해 0프레임)** → 주기2 웨이브11, 전달 약화 5% → 포기 → 결과 화면. run4(최종)는 제조 가능 커피 1잔뿐이라 마시기만(제출 0 → 전달 0%) — 나머지 단계 동일 PASS.
미검증: 칵테일 제조·종류 공격 보너스(실제 판에서 제조 가능한 칵테일 없음), 최고품질 **제출** +50%(최고잔은 마심), 실제 판 상성 적중 효과(1초 간격 캡처에 안 잡힘).
증거: 각 폴더 `events.log`, `shots/`, `transition/`(31장, 100ms), `transition-realtime-10fps.mp4`(3.1초), `master-cycle-periodic-1fps-x4.mp4`(1초 간격 프레임 4배속, run2 97초·run4 85초 — 연속 녹화 아님), `transition-key-crops.png`.

### 9.4 사용자 결함: 현수막이 화면을 따라다니며 테이블 뒤에 깔리고 글자가 겹침

- 원인: 현수막이 카메라 자식·카메라 위 기준 높이(이전 D1 수정)라 무대 바 카운터(같은 정렬 BackgroundProp) 뒤로 천이 가려지고 글자만 카운터 위에 떠 갈색-갈색으로 겹쳤다(사용자 스크린샷).
- 실제 무대 측정: 카운터 256×128@64 = 4×2월드, x = −30+12k, 월드 y 2.4–4.4. 칸(카운터 사이) 폭 8, 중앙 x = −24+12k. 칸 중앙 벽 램프 바닥 ≈ y 4.3–4.4. 바닥선(벽 아래 끝) y 2.6.
- 수정: 현수막 **월드 고정**(GameManager 자식, 카메라·플레이어 자식 아님). 설치 순간 칸 하나를 골라 그 칸 중앙에 배율 0.7(7×1.05월드, 양쪽 카운터와 0.5 여유, 램프 아래·바닥선 위). NPC 발은 바닥선, 손은 핀. 글자 "박람회 n/3"는 천 중앙(짙은 갈색, 천보다 위 정렬). 칸 선택은 카메라 기준 좌측 HUD 정보 패널·화면 끝을 피하는 가장 가까운 칸(`ChooseGap`), 없으면 벗어남이 가장 작은 칸. 카메라 위 오프셋 방식 폐기.
- 검증: EditMode 14/14(칸 계산·HUD 회피 선택 단언), PlayMode 12/12(신규 `BannerShow_WorldFixedInCounterGap_NoPropOverlap_CameraMoveInvariant` 바텐더·티 소믈리에 두 무대: 실제 Counter·Lamp 스프라이트와 현수막·글자 경계 겹침 0, 글자 ⊂ 천, 카메라·플레이어 자식 아님, 플레이어 +6·카메라 +3 이동 후 월드 좌표 불변). 오프스크린 `frames/banner_settled_hud_720/1080.png`, `banner_after_player_move_720/1080.png`(이동 후 같은 벽 칸에 남음), `install-zoom-userfix.png`. 실제 빌드 run4 `transition-key-crops.png`에서 카운터 사이·HUD 밖 확인.
- 남은 점: 설치 후 플레이어가 크게 움직이면 화면 고정 HUD 패널이 월드 현수막 일부를 덮을 수 있음(HUD-월드 일반 겹침, 스프라이트 겹침 아님). NPC는 설치 중 카운터 끝 앞을 약 0.25월드 지나감(바닥 위 인물이 가구 앞, 1.5초). 차 무대는 경계 시험으로만 확인(실제 빌드 실행은 바텐더 무대).

### 9.5 아트 실제 검수 (실제 빌드 프레임)

| 항목 | 판정 |
|---|---|
| 펼침 줄 | 보임(가늘다) — 손-천 끝 연결 PASS, 굵기는 아트 판단 |
| 근거리 페이드 등장/퇴장 | 실제 3.1초 영상에서 자연스러움 — 아트/사용자 최종 판단 |
| 품질 VFX(제조창 슬롯) | 등급별 형태 구분 보임, 720p에서 약 45px로 작음 — 확대 검토 권장(미수정) |
| 제출 컵 | 표시 PASS |
| 커피(원두) 상성 효과 | 실제 빌드 미포착(NOT RUN), 오프스크린 기존 판정 유지(작고 대비 낮음) |
| "박람회 n/3" 글자 | 수정 후 천 위 짙은 갈색·중앙 PASS |

### 9.6 빌드 / 해시 (최종)

- 검수 빌드 `Builds/SteamMaster-Review-2026-11-11/` 재빌드(build-review-3 성공). 이전 화면 고정판은 `Builds/SteamMaster-Review-2026-11-11-prefix-screenfixed/`로 보존, 중간판(build-review-2)은 삭제(목록 해시 `build-review2-files.sha256`만 보존).
- QA 자동 조작 빌드 `Builds/SteamMaster-PlayVerify-2026-11-11/`(CCT_PLAY_VERIFIER, 배포 금지).
- 빌드 입력 소스 2,803 파일 목록 sha256 **fbbb3d11a48a5f82106a585aee9126f36d9fa6bcc10f7717728cb6e1eb2adb06** — 빌드 전·후 동일(`source-manifest-build3-before/after.sha256`). ProjectSettings.asset 불변 `5900a053…`.
- 검수 빌드 산출물 208 파일 목록 sha256 `c9935d0c933ab091c7b32fb64faa9252e0a193ea7654cdb03f7b755980273616`(`build-review3-files.sha256`), exe `3070e580…d653`, CCT.Runtime.dll `7a896cbd…c84d`.
- 메모리 게이트: 이번 세션 Unity 시작 8회 모두 ≥4096MB 통과(로그 GATE 줄), 플레이어 실행 5회 모두 직전 ≥4096MB·동시 1개.
