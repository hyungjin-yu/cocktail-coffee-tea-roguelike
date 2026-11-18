# 로컬 2인 협동 최소 실험판 — 재개 기록 (2026-11-17)

## 상태 (작성 시점: 코드 작성 완료, Unity 컴파일/시험 전)
- 진입: `-cct-coop2p` 인자 (LaunchOptions.Coop2PRequested). 인자 없으면 기존 싱글 그대로.
  - 기존 파일 최소 수정: `Scripts/Core/LaunchOptions.cs`(인자 상수/판정), `Scripts/Core/GameBootstrap.cs`(인자 시 싱글 부트 생략), `Scripts/Commerce/SkinCommerce.cs`(인자 시 상점 시작 생략).
- 새 폴더: `Assets/_Project/Prototype/Coop2P/` (namespace `CCT.Prototype.Coop2P`)
  - Runtime: CoopTypes(수치), CoopSim(규칙, 순수 C#), CoopInput(키 배치), CoopGame(화면/HUD), CoopVerifier(-cct-coop2p-verify 자동 입력)
  - Editor: CoopBuild.Windows64Batch (출력 `Builds/Coop2P-Prototype-2026-11-17`, 비어있지 않으면 실패)
  - Tests: CoopSimTests (EditMode, assembly CCT.Prototype.Coop2P.Tests)
- 실제 저장 해시 기준: `Delivery/Verification/2026-11-17/coop2p/save-hash-before.txt`

## 다음 단계
1. gated Unity EditMode: `-runTests -testPlatform EditMode -assemblyNames CCT.Prototype.Coop2P.Tests`
2. 빌드: `-executeMethod CCT.Prototype.Coop2P.Editor.CoopBuild.Windows64Batch`
3. 검증 실행 720p/1080p: `CocktailCoffeeTea_Coop2P.exe -cct-coop2p -cct-coop2p-verify <dir> -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile <log>`
4. 싱글 스모크(격리 -cct-data-dir) + 실제 저장 해시 재확인
5. README/cmd/ZIP/sha256, QA 보고서 `Delivery/Reports/qa-coop2p-2026-11-17.md`

## 진행 17:34
- 오프라인 Roslyn(Unity csc + Unity 생성 참조 목록) 컴파일: Runtime/Editor/Tests 오류 0 (구 CCT.Runtime.dll + LaunchOptions 스텁 기준, Unity 정식 컴파일 아님).
- 오프라인 반사 실행 sim 시험 28 PASS / 1 SKIP(InputSystem 장치 시험은 Unity 필요). Unity 정식 결과 아님.
- 배포 README/cmd 준비: `Delivery/coop2p-2026-11-17/package/`
- 차단: gated-unity 메모리 조건(free RAM ≥ 4096MB) 미달(2.4~3.2GB, 사용자 앱·다른 에이전트 점유). 프로세스 종료하지 않고 대기 중.

## 진행 17:37 (코디네이터 지시: 4096 예외 없음, 장기 poll 금지)
- 백그라운드 RAM poll 중지. 내 dotnet build가 남긴 VBCSCompiler(PID 4376) 정리 → free 3406MB, 여전히 미달.
- QA 보고서: `Delivery/Reports/qa-coop2p-2026-11-17.md` (런타임 행 BLOCKED, 재개 절차 6장)
- 재개: 게이트 1회 확인 → 보고서 6장 순서대로.

## 완료 18:12 (예외 진행 승인 후)
- Unity EditMode 30/30 PASS, 빌드 성공, 실빌드 자동 입력 720p/1080p/ZIP 해제본 각 20 PASS/0 FAIL, 실저장 해시 불변, ZIP+sha256 납품.
- 싱글 기동 PASS(로그), 싱글 화면 캡처 BLOCKED(백신이 orca computer 스크립트 차단).
- 상세: Delivery/Reports/qa-coop2p-2026-11-17.md. 사람 2인 플레이 미실시.

## 마무리 18:50
- V6 수정, final 빌드 표준 흐름 720/1080 각 20/0. 실빌드 5분 자동 생존 3회 모두 실패(282/214/213s) — 성공 전환은 EditMode로만 검증. final ZIP: Delivery/CocktailCoffeeTea_Coop2P_2026-11-17_final.zip (v1 보존). 상세 QA 7장.
