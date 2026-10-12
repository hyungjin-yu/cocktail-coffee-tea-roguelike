# Claude 구현·검증 인계 — 2026-10-09 06:59 KST

코디네이터 최신 지시: 사용자 Codex 할당량 절약에 따라 이번 Task를 안전한 Claude 인계까지로 축소. 추가 구현/검증 시작 중단. 전체 게임·출시 검증 완료 아님. Task task_aed309b98de6 / Dispatch ctx_d19194304e90.

## 확인과 보존

실제 Unity6000.3.25f1(e1dba0a9aba4), 기존 Packages 유지. 경로상 AGENTS.md 없음. 기획05~11,26~36, Delivery/Reports/today-resume-2026-10-08.md 및 release-candidate 읽음. 최신31~35 우선, 화염독 자동지대/잔불삭제 유지. 사용자 실제 저장을 읽거나 시험에 쓰지 않음; 기획/ArtProduction/ArtSource 원본 편집 없음; 기존 사용자 미커밋 코드·빌드·실패이력 보존. Unity MCP PID30528/25976 및 타 사용자 앱/게임 종료하지 않음.

## 실제 증거와 실행 프로세스

초기 Main/Scratch2110파일 동일 SHA b3fb77d65aea2a8ed815a69227b25897618adda43406b0d9ea1bef0e9112eb50. 20:03 BuildRelease null/empty 수정으로 이전1e1b055f 입력해시는 낡았음. C:/UnityScratch/resume-2026-10-09/source와 scratch-source의 source-files.csv/summary.json 기록.

전체 Edit407/407 실패0 skip0 완료: C:/UnityScratch/resume-2026-10-09/edit.xml,edit.log,pipeline.log. 전체 Play는 실제 실행중 Unity PID17668, C:/UnityScratch/resume-2026-10-09/play.log,play-progress.txt,play.xml(완료시 생성). 마지막 확인06:58:19 SpecBossTests.Leviathan_PressureRate_6_9_13_ByHpBand 시작, 아직 전체 결과 미판정. 입력은 초기 b3fb77d6 소스이며 아래 Main QA 수정은 이 실행에 미반영. 이전결과를 최신최종으로 쓰지 말것.

원래 helper PID13516은 전체Play 시작 후 종료했음: 뒤 strict QA 단계가 미승인 UI에서 실패하므로 자동진행 중단. 준비 helper PID33024도 코디네이터 즉시인계 요청으로 종료했음. 자체 Unity PID17668은 자연완료하도록 그대로 넘김. 신규 빌드/게임/캡처 실행 없음. 후임은 PID17668 자연종료와 XML 결과 확인 후 Scratch 편집/다음 Unity 실행. 실행중 Scratch에 파일을 복사하지 말것.

## 이번 변경 — 전부 미컴파일 QA 확장, 후임 검토·컴파일 필수

Main에서만 수정(기존 Scratch는 이전 입력 유지):
- Assets/_Project/Scripts/Core/AutoTour.cs: System.Linq 추가, 실제 title button/tab Selectable pointer 상태 normal/hover/pressed/disabled PNG. 이미지 CSV를 Image.sprite에서 실제 overrideSprite로 변경. 매 Shot에서 partial 로그/CSV flush. 영구5탭 및 격리QA save specialty129/clears5에서 실제24노드 TryBuy 경로와 최대진행5탭 촬영. 도감24영구+15커피+5최종형태 상세44장. 4무기 카드선택 각 촬영. 커피 추가재료/실제Choose 노드로15해금후 재조회. 실제Gun최대카드/4고HP표적/6초 직접적중/파열·확산·부착marker 영상. 별도 강제최종보스 실제살해→success Result 촬영. 강제QA 상태이며 정상런증거가 아님.
- Assets/_Project/Scripts/Core/LoadScenario.cs: 실제최대카드8종 적용해 관통6/확산7발, 커피 최종노드에 해당하는 ExtraThrust+1로 기존원본8발+최종보조2 유지. 실제최종피어스/확산/화염독 켬. 기존160적/100적탄/80플레이어탄/10적보스장판/200픽업부하 유지. 파열 삭제·수 제한 없음. 추가 실부하 카운터/GC 최종측정은 아직 없음.
- Assets/_Project/Scripts/Core/PlayVerifier.cs: 읽기전용 telemetry CSV에 permanentCount/challengeCoffeeCount/enemyHpMultiplier/enemyDamageMultiplier 추가. 헤더와 WriteTelemetry 양쪽 수정. 실제 정상4무기 성장활성 수치확인을 위한 로그이며 입력대행/정상난도 규칙 유지.
- Tools/QA/Run-RcPipeline.ps1: 기존strict tour/pv/load 유지, 승인전 별도 qa-tour/qa-pv/qa-load preview 실행단계 추가. 미승인 UI의 strictRelease 게이트 완화 없음.
- Tools/QA/ui_capture_audit.py 새 파일: 실제PNG 해상도/hash와 canonical14PNG 원본→반입 바이트/assetID·clip·frame→실제sprite CSV 매핑. 아직 실행·검증 안함. PNG와 Pillow, CSV 형식 확인 필요.
- Delivery/Reports/release-resume-2026-10-09.md 초기진행보고 및 본 인계보고.

현재 Main 마지막2110입력 SHA cda998f0882d84b260556733d7c55113bd65fc860bcb15e1ae1094231077e2c7, runtime85 SHA531f0493bed855e8e54044927d9ed4db5f0c3911a2a3e5011118a5ed41176e5f 불변, editor15 df41b1486c4c997311c88f167fcc11f29421681fb5500b74e6af263e4980e2a3 불변, QA3 SHA310a90abef671d5f4a7271eea236fa4e8be7865cbbaa5fa6debc40f33b83a7c0. handoff-source/source-files.csv,source-summary.json 정확목록. Tools는 이 입력해시 범위 밖이므로 후임별도해시필요.

## 정확한 다음 안전 단계

1. 실행중 PID17668 종료후 play.xml total/passed/failed/skipped/결과를 실제 확인. 이번 전체Edit/Play는 initial b3fb77d6 증거로 보존. 현재 QA수정 컴파일 전이므로 성공으로 과장 금지.
2. Main의 AutoTour/LoadScenario/PlayVerifier 3파일을 Scratch 동일 상대경로에 복사. 기존 C:/UnityScratch/resume-2026-10-09/prepare-qa.ps1은 중단된 helper이며 다시 자동실행하지 말고 읽기만 할것(처음 실행AST는2파일, 디스크본은3파일로 바뀜). 명시복사 후 Main/Scratch snapshot_source.py 양방향 정합검사.
3. 승인전 실제 화면은 preview QA: powershell -NoProfile -ExecutionPolicy Bypass -File C:/Users/user/Documents/Projects/CocktailCoffeeTea/Tools/QA/Run-RcPipeline.ps1 -Steps qa-tour,qa-pv,qa-load -Project C:/UnityScratch/CCT -Evidence C:/UnityScratch/resume-2026-10-09/qa. 새스텝이 정상Compiles하는지 로그 확인; 실제스크립트/QA코드는미검증.
4. tour/CocktailCoffeeTea.exe를 별도data/out으로 -cct-autotour <out> -cct-autotour-mode screens -cct-data-dir <data> -screen-width1280 -screen-height720 및1920x1080 실행. 이 후보빌드는 qa-tour폴더임. 완료마커+PNG/JPG/frames.csv/sprites.csv/shots-sprites.csv 원본보존. 추가 save 모드가 오류/복구 연속15/10frame를 제공. 정상OS입력은 별도 computer-use 또는 Tools/QA/Capture-ComputerAction.ps1로 실제화면검증.
5. ui_capture_audit.py project output run-out720 run-out1080 호출하여 캡처 CSV/14상태CSV/hash 생성. 원본UI8개승인false 그대로 유지. 현재 아트담당Task도원본감사인계로축소되어 최종실제승인아님. 기획 ArtProduction/release-review-2026-10-09/report.md, runtime-checklist.csv128행,source-14-png-audit.csv,manifest-baseline.csv,review-artifacts-sha256.csv 확인.94파일0불일치/native8·PNG14결함0 보고받음. 실제화면검수와 승인담당 연결은 코디네이터에 요청.
6. 증거상누락가능: 직업/무기선택4화면은 기존 StartCombo shootSelect가첫조합만true; 모든무기 선택화면 촬영범위 확인. 도감 미발견카드는 실제 설명숨김이 정상이므로 final설명은 discover 경로 또는 실획득후 재촬영 필요할수있음. 커피15전후/구매상태와실제HP·피해예고 확인. 최종연속타/전염실부하·전염비재귀·경고 가독성,14PNG상태 실제노출누락은 검수표와 대조. AutoTour successGame/finalBoss nil 및 종료타이밍도 첫 실행검토. 이미작성된QA초안이므로 아직통과근거아님.
7. 후속전체범위: 실제아트승인→반입/import/catalog검사, 최종소스고정 전체Edit/Play, QA/runtime/strictRelease격리, 새저장 성장활성4무기정상클리어(무기최대3시도,레이피어hybrid), 추가실패/포기/강화보스후사망, G30, 단독1080G35최대최종파열/확산(30+180초/10restart), 최신G01~35. strictWindows64 실행폴더/ZIP/README/실제DLL라이선스원문/hash/new extraction Editor없는G34. 사용자launcher는코디네이터에경로/hash전달하여갱신요청. Steam계정/게시/결제금지.

최종 전체결과·실전로그·파일·해시·남은조건은 release-resume-2026-10-09.md에 이어갱신. 이번 worker_done succeeded는 최신사용자지시로축소된안전인계완료만뜻함.
