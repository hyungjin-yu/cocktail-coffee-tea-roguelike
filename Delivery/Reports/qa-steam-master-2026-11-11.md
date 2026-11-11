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

## 10. 세션 5 (task_55168e048abc, 18:05~18:50) — 미검증 3항목 집중 검증 + 720p 효과 가독성 국소 개선

기준 HEAD a9985a13(root 체크포인트). root가 run4 `transition-key-crops.png`로 카운터 사이 현수막/글자 수정 승인(재확인: 두 NPC 사이 월드 칸, "박람회" 글자 단독·겹침 없음). 기존 정상 1주기·전체 suite는 반복하지 않음.

### 10.1 검증 방식 — **QA 시나리오(자연 플레이 아님)**

| 항목 | 내용 |
|---|---|
| 빌드 | QA 전용 `Builds/SteamMaster-PlayVerify-2026-11-11-mech/`(CCT_PLAY_VERIFIER, 배포 금지) |
| 흐름 | `-cct-playverify-master-mechanics`(신규 `Scripts/Core/PlayVerifierMechanics.cs`, QA 심볼 빌드에만 컴파일). 화면 선택 = 실제 버튼 onClick, 이동 = 기존 입력 대행. HP·적·생성·시간·보상 값 쓰기 없음. 저이펙트 전환만 격리 settings 저장(`SetEffectsLevel`) |
| **고정 seed** | `-cct-playverify-seed 20261111`(QA seed override, 기존 기능) |
| **격리 fixture** | `Builds/SteamMaster-Review-2026-11-11-qa-data/run8-mech-fixture/`(run5 격리 데이터 복사 + drinks.json 일반 창고 gin +900, campari +600, sweet_vermouth +600, dry_vermouth +100, coffee_bean +200 — `QA-FIXTURE.txt` 명시). 1웨이브에서 바로 칵테일 2종·커피 2잔을 만들기 위한 재료 강제 지급이므로 **자연 수집 결과가 아님** |
| 관찰 | `Enemy.QaDirectHit`(QA 빌드 전용 `#if CCT_PLAY_VERIFIER` 읽기 알림, 판정·값 불변) → `direct-hits.log`. 효과 재생 감지 후 게임 자신의 ScreenCapture |
| 저장 격리 | `-cct-data-dir` + `-logFile` 격리 폴더. 사용자 폴더 save/settings/drinks/Player.log 메타데이터 전후 동일(`usersave-meta-before-mech.txt`/`usersave-meta-after-mech.txt`; 바뀐 것은 Unity 테스트 실행기 사본 `TestResults.xml`뿐) |
| 게이트 | Unity 시작 7회·플레이어 실행 4회 모두 직전 free RAM ≥ 4096MB(4,512~5,417MB), 동시 1개 |

실행 기록: run1 = **중단**(fixture 부족: `Owned`는 일반 창고만 셈 → 마티니 2잔째 불가. 폴더 이름 변경이 잠겨 `realbuild-mech-run1/` 이름 그대로 — 마티니→네그로니 U=2 증거만 유효, 반복 단계 미실행). run2 = gin 부족으로 네그로니 미제조(반복 증거·제출·상성·박람회 HP 유효). run3 = 정렬 수정 전 상성 캡처(불꽃에 가림 발견). **run4 = 최종**(모든 수정 후, 박람회 생략 `-cct-playverify-mech-skipfair`).

### 10.2 결과

| # | 검사 | 기대 | 실제(실제 Windows QA 빌드) | 판정 | 증거 |
|---|---|---|---|---|---|
| 1a | 칵테일 새 종류 | 마티니 → U=1, +2% | U=1, masterDamagePercent 0.02, Stats.Damage 12→12.24 | PASS | run4 `events.log` `[craft 1_martini]`, `shots/m_craft_1_martini.png` |
| 1b | 같은 종류 반복 | 마티니 재제조 → U·공격 불변 | 2잔째 제조 성공, U=1·+2%·12.24 그대로 | PASS | run4 `[craft 2_martini_repeat]`(run2 동일) |
| 1c | 두 번째 종류 | 네그로니 → U=2, +4%(덮어쓰기, 곱 누적 없음) | U=2, 0.04, Stats.Damage 12.48 = 12×1.04 | PASS | run4 `COCKTAIL CHECK`, `m_craft_3_negroni.png` |
| 2a | 최고 품질 커피 제출 | 150p, 미리보기 round(150×5/100)=8% | points=150, bonus +50p, 예상 약화 8%, 버튼 "제출 +50%", 컵 표시 | PASS | run4 `[6_submit_best]`, `m_6_submit_best.png` |
| 2b | 일반 잔 추가 | 250p → round(12.5)=13% | submitted=2 points=250 preview=13%, 하단 "제출 2잔(품질 추가 가치 +50p) → 약화 13%" | PASS | run4 `SUBMIT CHECK`, `m_9_brew_footer.png` |
| 2c | 박람회 접수·실제 적 약화 | 스냅샷 13%, 적 hpScale = HpScale(6)×0.87 | 스냅샷 13%; 박람회 진상 손님 hpScale 1.5225 = 1.75×0.87; maxHp 95 = round(60×1.5225×1.036) — 1.036은 기존 RunChallenge 커피 스킬 3개 적 HP 배율(telemetry `enemyHpMultiplier`) | PASS | run2 `FAIR CHECK`, `telemetry.csv`, `shots/m_10_fair_enemies.png` |
| 3a | 상성 대상 적중 | 커피 마심 → 차 계열 A=max(1,round(B×20%)), T=B+A 한 번 적용 | 대상 6회 전부 일치(B12→A2 T14, B15→A3 T18), 숫자 "14 (추가 2) 상성!" | PASS | run4 `direct-hits.log`(MISMATCH 0), `AFFINITY CHECK` |
| 3b | 비대상 | A=0 | 59회 전부 A=0 | PASS | 같은 로그 |
| 3c | 효과 full | 확정 상성 적중에 coffee_bean_steam 1회 | 3회 캡처, scale 1.5·외곽선 4·frame 2, skipped 0 | PASS | run4 `shots/m_aff_full_1..3.png`, `affinity-key-crops.png` |
| 3d | 효과 low | 저이펙트에서 _low로 교체 | coffee_bean_steam_low 3회, frame 1(3노출 중 마지막은 투명) | PASS | run4 `shots/m_aff_low_1..3.png` |

주: 칵테일 상성(커피 계열)·차 상성(칵테일 계열) 실제 적중은 이번 실제 빌드에서 돌리지 않음 — 그 두 clip은 PlayMode(`AffinityVfx_*`)·오프스크린 프레임만.

### 10.3 720p 가독성 국소 개선 (승인 ArtSource/Resources PNG 무수정, 풀·동시 상한 6·간격 0.06초 유지)

| 문제(실제 빌드 확인) | 수정 | 파일 |
|---|---|---|
| 품질 효과 슬롯 720p ≈45px | 슬롯 112(컵 기준)는 그대로, 효과 그림만 `FxArtPx` 168(1.5배) → ≈65px. 보이는 내용은 패널 위·오른쪽 안, 첫 잔 줄(−118) 위(PlayMode 단언) | `UI/MasterChallengeHud.cs` |
| 원두 효과 작음 | 상성 월드 배율 원두 1.5 · 거품/찻잎 1.25 | `Core/MasterVfx.cs` |
| 갈색 적 위 갈색 원두 대비 | 원두 clip만 같은 프레임 크림색(1,.93,.78,.9) 실루엣 4방향 2원본px 외곽선(글꼴과 같은 GUI/Text 셰이더 — 기본 스프라이트 색 곱은 어두운 테두리가 됨을 1차 시도에서 확인 후 교체). 풀 개체에 붙어 재사용, 반납 시 끔 | `Core/MasterVfx.cs` |
| 실제 판에서 원두가 같은 적중점 피격 불꽃(AirVfx) 아래 가림(run3 발견) | 본 효과 AirVfx+1(불꽃 위, 피해 숫자와 같은 단), 외곽선 AirVfx | `Core/MasterVfx.cs` |

과장 방지: 반짝임·발광·추가 프레임 없음, 재생 길이·횟수 동일. 비교: 오프스크린 `frames/`(전) ↔ `frames-vfxfix/`(후); 실제 빌드 품질 슬롯 전 `realbuild-master-run2/shots/05_craft_2.png` ↔ 후 `realbuild-mech-run4/shots/m_craft_4_espresso_best.png`.
남은 판단: 피해 숫자와 같은 정렬 단이라 겹칠 때 앞뒤는 Y 정렬에 따름(이번 캡처에서 숫자 가림 없음). 크림 외곽선 굵기는 아트/사용자 최종 판단.

### 10.4 테스트 / 빌드 / 해시

- PlayMode targeted: `playmode-vfxfix-1`(MasterArtPlayTests 7/8 — 기존 `AffinityVfx_*`가 x=3 적에 플레이어 실제 자동 공격이 닿아 추가 효과로 반납 단언 실패; 적을 ±14로 옮겨 시험 독립화), `-2` 3/3, `-3` 4/4(외곽선 셰이더 교체), **`-4` 4/4 PASS**(정렬 수정 후: AffinityVfx·QualityVfx·신규 `Vfx_ReadabilityScaleOutline_ResetOnReuse_UiArtInsidePanel`·오프스크린 증거).
- QA 빌드 `build-playverify-mech-1..3.log` 성공. 검수 빌드 **`Builds/SteamMaster-Review-2026-11-11-vfx/`**(`build-review-vfx-1.log` 성공, 545MB). 이전 검수 빌드 `SteamMaster-Review-2026-11-11/`·공모전 빌드 미변경.
- 소스 목록(2,805 파일) 변경 전 `fbbb3d11…`(2,803, 직전 빌드와 동일 — `source-manifest-mech-before.sha256`) → 빌드 직전/직후 **`c93d06d8d50a8b460592433feb7ced25a36fe8645ff6119fe922e4f7308cdd91`** 동일(`source-manifest-vfx-build-before/after.sha256`). 변경 파일: MasterVfx.cs, MasterChallengeHud.cs, Enemy.cs(QA 심볼 블록만), PlayVerifier.cs(분기 1줄), PlayVerifierMechanics.cs(+meta, 신규), 테스트 MasterArtPlayTests.cs. ProjectSettings.asset `5900a053…` 불변.
- 검수 빌드 208 파일 목록 sha256 `7cff9dc1307bcc5baa07bfb8c9dcdad23f8b3e998fdc8b78d1267c12b17e8015`(`build-review-vfx-files.sha256`), CCT.Runtime.dll `ddff75e4…1cc22`. 검수 빌드 DLL에 `QaDirectHit`·`MechanicsFlow`·`PlayVerifier` 문자열 0건(QA 빌드에는 있음) — QA 강제 데이터/관찰 코드 미포함.
- 실제 창 사람 조작: 이번에도 하지 않음(자동 대행). 공개 배포/Steam 업로드 없음.

## 11. 세션 6 (task_0e63f27c2749, 18:50~19:10) — 칵테일/차 상성 실제 빌드, 연출 검수, 실제 창 OS 입력, 최종 패키지

### 11.1 칵테일·차 상성 (실제 Windows QA 빌드, **QA 시나리오 — 자연 플레이 아님**)

조건: QA 빌드 `Builds/SteamMaster-PlayVerify-2026-11-11-mech/`(build-playverify-mech-5), `-cct-playverify-mech-affinity cocktail|tea`, 고정 seed 20261111, 데이터 = run5 격리 데이터 **무수정 복사**(`qa-data/run10-aff-cocktail|tea/`, sha256 6a28ffbf… = run5) — 이번에는 재료 편집 없음(자연 누적 재고로 마티니 1·센차 1 제조 가능). 화면 선택은 실제 버튼, 이동은 입력 대행.

| # | 검사 | 실제 | 판정 | 증거(realbuild-aff-*-r2/) |
|---|---|---|---|---|
| A1 | 칵테일(마티니) 마심 → 대상 커피 계열 | drunk=Cocktail, target=Coffee, 안내 "[커] 커피 계열 몬스터 추가 피해 +20%" | PASS | cocktail `events.log` |
| A2 | 대상 적중 B+A | 6회 전부 A=max(1,round(B×20%)), T=B+A(예: 16 (추가 3)), 비대상 4회 A=0, MISMATCH 0 | PASS | `direct-hits.log` |
| A3 | cocktail_foam full / _low | full 3(scale 1.25, frame 2) / low 3(frame 1), skipped 0 | PASS | `shots/m_aff_*`, `affinity-key-crops.png` |
| T1 | 차(센차) 마심 → 대상 칵테일 계열 | drunk=Tea, target=Cocktail | PASS | tea `events.log` |
| T2 | 대상 적중 B+A | 6회 일치(14 (추가 2)), 비대상 13회 A=0 | PASS | `direct-hits.log` |
| T3 | tea_leaf_wind full / _low | full 3 / low 3, skipped 0 | PASS | `shots/`, `affinity-key-crops.png` |

**연속 영상(게임 자체 캡처)**: 각 실행에서 효과 단계 동안 게임 ScreenCapture 100ms 간격(실제 시간 10fps, 렌더 프레임 전부는 아님) → `continuous-{full,low}-{cocktail,tea}-10fps-realtime.mp4`(54/49/176/19 프레임). 기존 `master-cycle-periodic-1fps-x4.mp4`(1초 간격 모음)와 다름. 첫 실행(`realbuild-aff-cocktail|tea/`)은 연속 캡처 두 개가 같은 중지 플래그를 공유해 full 폴더에 low 구간이 섞인 QA 도구 결함 → 폴더 삭제, 캡처별 토큰으로 수정 후 r2로 재실행(판정 수치는 첫 실행도 동일 PASS).
시각 판정: 거품 고리·찻잎은 불꽃 위에 보이나 선이 가늘어 원두보다 약함 — 아트 판단(코드 결함 아님, 미수정).

### 11.2 연출 실제 증거 검수 (실제 빌드 run4-final 100ms 프레임)

| 항목 | 판정 | 증거 |
|---|---|---|
| 현수막 펼침 줄 | 손에서 천 끝까지 가는 선 연결, 정착 후 핀 — PASS(굵기 아트 판단) | `review-session6/banner-cord-hands-run4final.png` |
| NPC 근거리 페이드 | 퇴장 마지막 프레임 반투명 확인, 등장도 가까이서 나타남 — PASS | `review-session6/banner-npc-fade-sequence-run4final.png` |
| 원두 외곽선 | 크림 외곽선·불꽃 위 정렬 — PASS(10.3) | `realbuild-mech-run4/affinity-key-crops.png` |

명백한 코드 결함 없음 → 연출 코드 수정 없음. 승인 PNG/ArtSource 무수정(소스 목록상 Resources/ArtSource 변경 0).

### 11.3 실제 창 OS 입력 (최종 검토 빌드) — **자동 조작이며 사람 직접 조작 아님**

이번에는 새로 띄운 게임 창이 전경(GetForegroundWindow pid = 게임)이어서 `orca computer click`(실제 OS 마우스 입력)을 매 동작 직전 전경 확인 후에만 보냄(전경이 아니면 입력 0, `os-input.log`). 최종 검토 빌드·빈 격리 폴더(`qa-data/final-smoke`)·`-logFile`.
경로: 타이틀 시작 → 바텐더 → 마스터 도전 → 레이피어 → 영업 시작 → 튜토리얼 시작 → 마스터 1웨이브 HUD 정상 → (키 `b` 주입은 반응 없음 — 키보드 입력 전달 **미확인**) → [음료 만들기] 버튼 클릭 시점에 이동 입력이 없어 18초에 쓰러짐 → 결과 화면 → 타이틀 → 종료(정상 종료, Player.log 예외 0).
결론: 실제 창 마우스 입력으로 메뉴·모드 시작·결과·종료 확인 PASS. 키보드 이동을 통한 실제 창 전투/제조/박람회 플레이는 **BLOCKED**(단발 키만 제공되는 도구, 누르고 있기 불가·키 전달 미확인) — 사람 직접 플레이 통과 아님. 증거: `review-session6/real-window-os-input/01..10_*.png`, `os-input.log`.
관찰(범위 밖, 미수정): 무기 선택 화면에서 모드 줄이 무기 카드 아래 테두리와 겹치고 상단 안내 두 번째 줄이 카드 위에 잘림(`02_click_bartender.png`) — 별도 UI 과제 후보.

### 11.4 시험 / 최종 빌드 / 패키지

- PlayMode targeted `playmode-final-1`: MasterArtPlayTests 8 + MasterChallengePlayTests 2 = **10/10 PASS**(전체 suite 미실행 — 범위 밖 반복 금지).
- 최종 검토 빌드 `Builds/SteamMaster-Review-2026-11-11-final/`(`build-review-final-1.log` 성공). 소스 2,805 파일 빌드 전·후 **37c808dae8ec8e2e06459aa6b89a4bdbe5fd919c013dc84ec30851036be51b38** 동일(`source-manifest-final-before/after.sha256`), 직전 -vfx 빌드 대비 차이는 QA 전용 PlayVerifierMechanics.cs뿐. ProjectSettings `5900a053…` 불변.
- 산출물 208 파일 목록 sha256 `fc8cbe9649f9c1a60dd6dea9e41663403cfb675ae76047e7b211c636e098c2f8`, exe `3070e580…d653`, CCT.Runtime.dll `ad81c84d…e91d`. DLL 내 `QaDirectHit/MechanicsFlow/AffinityOnly/PlayVerifier/CCT_PLAY_VERIFIER` 0건 — 배포용 QA 코드 미포함.
- 패키지 `Delivery/CocktailCoffeeTea_SteamMasterReview_2026-11-11.zip`(209 항목, BurstDebugInformation_DoNotShip 제외, README_실행안내·THIRD_PARTY_NOTICES 포함, zip 무결성 OK), sha256 **86e91edbda57828784b427e0901aa6d601b6a37b62123298d3e88cae54653abe**(`.zip.sha256`). 공개 배포·Steam 업로드 없음.
- 상점 보강 초안: `Delivery/Reports/steam-store-master-draft-2026-11-11.md`(스크린샷 후보는 720p 구도 후보 — 1920×1080 재캡처 필요).
- 저장 격리: 사용자 폴더 메타데이터 세션 전후 동일(TestResults.xml만 — Unity 시험 실행기 사본). 게이트: Unity 3회·플레이어 4회 모두 ≥4096MB, 동시 1개.

## 12. 세션 7 (task_80f191cf89db, 19:05~19:30) — 무기 선택 화면 겹침/잘림 수정, Steam 1920×1080 후보, r2 패키지

root 확인: 이전 ZIP 해시 86e91edb… 일치(이번 세션에도 `sha256sum -c` OK, 이전 ZIP 보존).

### 12.1 결함과 수정 (`Scripts/UI/CharacterSelectView.cs` 무기 단계만)

| 결함(11.3 실제 창 발견) | 원인(기준 1920×1080) | 수정 |
|---|---|---|
| 모드 줄이 무기 카드 아래 테두리와 겹침 | 카드 위 220+높이 580 → 하단 800, 모드 줄 782–834 | 카드 위 248·높이 510(하단 758), 설명 칸 260→200(내용 최대 4줄 ≈144) |
| 상단 안내 2번째 줄이 카드 뒤로 잘림 | 안내 190–242(2줄)과 카드 위 220 겹침 | 제목 96–176, 안내 180–240(Truncate+best-fit 14–20), 카드 248부터 |
| 모드 안내가 영업 시작 위쪽과 겹침 | 안내 아래 192–222, 영업 시작 130–214 | 안내 218–244(best-fit 16–22), 모드 줄 246–298 그대로 |

상수 `WeaponCardTop/WeaponCardHeight/ModeRowBottom/ModeRowHeight/ModeHintBottom` 공개. 직업 단계·다른 화면 미변경. 승인 아트 무수정.

### 12.2 검증

| 검사 | 결과 | 증거 |
|---|---|---|
| PlayMode `CharacterSelectTests`+`EndlessModePlayTests` | 1차 12/13(신규 시험이 정적 RunModeSelection을 Endless로 남겨 기존 기본값 시험 실패 → 시험 끝에 복원) → **2차 13/13 PASS**. 신규 `WeaponStep_Bands_NoOverlap_InfoLineNotClipped` 2직업×2모드: 제목·안내·카드·모드 줄·모드 안내·영업 시작 사각형 겹침 0, 안내·모드 안내 글자 전부 보임 | `playmode-weaponui-1/2.xml` |
| 실제 720p 창 화면(QA 빌드, 게임 ScreenCapture) | 바텐더/티 소믈리에 × 진상 보스/마스터 4장 모두 겹침·잘림 없음 | `realbuild-shots-run2/shots/s03_*_720.png`, `s04_*_720.png` |
| 1920×1080 렌더(같은 실행) | 같은 4장 PASS | `realbuild-shots-run2/shots/s03_*_1080.png`, `s04_*_1080.png` |
| **검수 빌드 실제 창**(r2, 빈 격리 폴더, 전경 확인 후 OS 마우스 클릭) | 타이틀→바텐더→마스터 도전: 수정 배치 표시, 창 닫기로 정상 종료, 예외 0 | `review-session7-final2-real-window/01..05_*.png`, `os-input.log` |

run1(`realbuild-shots-run1`)은 메뉴 1080 렌더가 검정(QA 임시 카메라 cullingMask 0 — QA 도구 결함) → 수정 후 run2. 게임 화면 1080은 run1도 정상.

### 12.3 Steam 스크린샷 후보

8장 `Delivery/SteamScreenshotCandidates-2026-11-11/`(1920×1080, SHA256SUMS.txt). 출처·한계·확정/제안/미검증 구분은 `Delivery/Reports/steam-store-master-draft-2026-11-11.md`. QA 빌드 자동 조작 화면이며 사람 플레이 화면 아님. 가격·표시명·사양 미확정 유지.

### 12.4 빌드 / 패키지

- 새 검토 빌드 `Builds/SteamMaster-Review-2026-11-11-final2/`(이전 `-final`·`-vfx`·원 검수·공모전 빌드 보존). 소스 2,805 파일 빌드 전·후 **b6328b567f77e590c15db7e6b4dcc6e3e1fd207c9805f18f37d1ab6db9c63079** 동일(직전 대비 CharacterSelectView.cs + QA 전용 PlayVerifier.cs/PlayVerifierMechanics.cs). ProjectSettings 불변. 산출물 208 파일 목록 `830d481e…2e39`, CCT.Runtime.dll `2a201b8c…8719` — `QaDirectHit/MechanicsFlow/ShotsFlow/RtShot/PlayVerifier` 0건.
- 패키지 `Delivery/CocktailCoffeeTea_SteamMasterReview_2026-11-11_r2.zip`(209 항목, DoNotShip 제외, 무결성 OK) sha256 **28ce6e0b2f15cabc4f8d87684272b9022e77ded39cfcf4101c562d40f9769c03**. 이전 `…_2026-11-11.zip`(86e91edb…) 보존·검증 OK.
- 게이트: Unity 4회·플레이어 3회 ≥4096MB, 동시 1개. 사용자 저장 메타 불변(아래 확인).
- **미완료**: 사람 직접 플레이(자동 조작으로 대체하지 않음), 실제 창 키보드 조작, 공개 배포·Steam 제출(하지 않음).
