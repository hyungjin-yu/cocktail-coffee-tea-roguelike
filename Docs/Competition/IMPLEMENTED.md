# 실제 구현 범위 — 음료 마스터: 공공조달 체험편 (v0.1.0-pps)

작성일: 2026-11-10 · 상태: **소스 작성 완료, 컴파일·테스트·빌드·실제 렌더 미검증(보류)**

> 2026-11-10 사용자가 게임 중이라는 코디네이터 지시로 Unity 실행(가져오기·컴파일·테스트·빌드·캡처·영상)을 모두 중단했다. 아래 "구현(소스)" 항목은 코드가 작성되었다는 뜻이며 **동작이 검증되었다는 뜻이 아니다.** 신청서에는 아직 완료형으로 쓰면 안 된다(§5 참고).

## 1. 소스로 구현한 것

| 기능 | 위치 | 상태 |
|---|---|---|
| 전용 진입: `PPS_CONTEST`일 때 본편 타이틀·무한 모드·상점·스킨을 시작하지 않고 공모전 타이틀로 시작 | `Contest/ContestApp.cs` AutoStart, `Scripts/Core/GameBootstrap.cs`, `Scripts/Commerce/SkinCommerce.cs` | 구현(소스), 미검증 |
| 타이틀(제목·소개·오인 방지 문구·임무 시작·도움말·종료), 임무 선택(잠금/도전/완료·배지) | `ContestApp.DrawTitle/DrawSelect` | 구현(소스), 미검증 |
| 임무 3개(가상 기관·행사, 정수 수치, 결정적 데이터) | `Contest/ContestData.cs` | 구현(소스), 미검증 |
| 1 요청 확인: 조건 6~7개를 모두 확인해야 진행 | `ContestSession.CheckCondition/FinishRequest` | 구현(소스), 미검증 |
| 2 공급안 선택: 3개 비교, 포장 수 조절, 품명·카페인·원산지·수량·예산 검사, 반려 사유 표시·재선택 | `ContestSession.Evaluate/Order`, `DrawSupply` | 구현(소스), 미검증 |
| 3 입고 확인: 포장 하나씩 입고/반품, 결함(파손·원산지 없음·오배송) 섞임, 정상 반품 시 재배송+5분 | `AcceptPack/RejectPack`, `DrawReceive` | 구현(소스), 미검증 |
| 4 제조: 재료·온도(70/80/95℃)·용기(찻잔/머그/커피잔)·잔 수 선택, 붓기 버튼 길게 누르기(약 1.2초), 오래된 재고부터 사용, 기존 붓기·김 프레임 재생 | `Brew`, `DrawBrew`, `HoldButton` | 구현(소스), 미검증 |
| 5 검사·검수: 잔마다 품명·온도·용기·재료 이력 비교, 문제·보완 안내, 불일치 제외(같은 결함 재료 반품), 재검수 필요 | `Inspect/ExcludeNonconforming`, `DrawInspect` | 구현(소스), 미검증 |
| 추가 확보: 같은 공급안으로 남은 예산 안에서 재주문 | `Order`(재주문 경로) | 구현(소스), 미검증 |
| 6 납품: 최신 검수 합격 때만, 한 번만 인수(중복 납품 무시), 적합 물품만 인수·초과분 제외, 기록·학습 요약·신뢰도 | `Deliver`, `DrawResult` | 구현(소스), 미검증 |
| 실패: 납기 초과, 예산으로 부족분 확보 불가 → 사유 표시, 처음부터 다시(새 상태) | `Fail/CheckFeasible`, `DrawFailed` | 구현(소스), 미검증 |
| 분리 저장: `pps_contest_save.json`(+.bak, 임시 파일 교체), 손상 시 백업→새 데이터, 배지 임무당 1회, PlayerPrefs 미사용 | `Contest/ContestSave.cs` | 구현(소스), 미검증 |
| 도움말·정책 해설·출처(H/Esc) | `ContestApp.HelpText` | 구현(소스), 미검증 |
| 키보드 대안(숫자·Enter·A/R·T/V·←/→·Space 길게·I·X·B·R·Backspace) | `ContestApp.HandleKeys` | 구현(소스), 미검증 |
| 공모전 빌드 메뉴/배치(제출본·캡처본 분리, 이름·정의 확인) | `Contest/Editor/ContestBuild.cs` | 구현(소스), 미실행 |
| 캡처 빌드 전용 시연·스크린샷·프레임 녹화(`PPS_CAPTURE`, 게임 내부 호출만, OS 입력 없음, 오프스크린 렌더) | `Contest/ContestCapture.cs` | 구현(소스), 미실행 |
| EditMode 테스트 19개, PlayMode 테스트 4개 | `Contest/Tests/` | 작성, **미실행** |

## 2. 구현하지 않은 것(허위 기능 없음)

- 나라장터 실제 접속·API·온라인 접수·입찰·계약·대금 청구: 없음(네트워크 코드 없음).
- 신규 아트·VFX·음향: 없음. 오디오는 공모전 화면에서 따로 켜지 않았다(본편 AudioService 미호출) — 버튼 포커스 효과음 훅(`UiFocusSfx`)이 소리를 내는지는 미확인.
- 설정 화면(음량·해상도): 없음. 창 모드 1920×1080 기본, 창 크기 조절 가능.
- 전투·무한 모드·상점·스킨: 공모전판에서 노출하지 않음.

## 3. 임무 데이터(창작 값)

| 임무 | 가상 기관/행사 | 요청 | 납기 | 예산 | 공급안(단위/봉·코인/봉) | 입고 결함 |
|---|---|---|---|---|---|---|
| 1 | 햇살구 작은도서관 / 가을 독서 모임 | 녹차 4잔, 80℃, 찻잔 | 80분 | 12 | 초록잎상회 녹차 2·3 ✓ / 새벽차방 홍차 4·2 ✕품명 / 들판농원 녹차 5·8 ✓ | 파손 1 |
| 2 | 물빛동 주민센터 / 어르신 건강 강좌 | 캐모마일 6잔, 95℃, 머그, 카페인 없음 | 70분 | 14 | 들꽃허브 3·5 ✓ / 노을블렌드(홍차 혼합) 6·4 ✕카페인 / 바람농장 2·3 ✓ | 홍차 오배송 1 |
| 3 | 바람시 자원봉사센터 / 시민 자원봉사자 감사 행사 | 핸드드립 커피 8잔, 95℃, 커피잔, 원산지 표시 | 60분 | 16 | 언덕로스터 4·6 ✓ / 항구상사 5·3 ✕원산지 / 솔밭커피 10·18 ✕예산 | 원산지 없음 1, 파손 1 |

시간 규칙: 주문 10분, 입고 완료 5분, 정상 포장 반품 5분, 제조 배치(최대 4잔) 10분, 검수 5분. 임무 1 최적 진행 이론값 30분(실측 아님).

## 4. AI 사용 범위

- Claude(Anthropic, Claude Code 에이전트): 공모전판 C# 코드·테스트·빌드 스크립트·문서 초안 작성, 공식 절차 페이지 열람·인용 정리.
- 아트: 기존 「음료 마스터」 픽셀 아트 재사용(이전 제작 기록상 Aseprite·PixelLab 사용). 이번 작업에서 생성형 이미지·영상·음성 생성 없음.

## 5. 신청서 담당용 사실 목록(현재 시점)

현재 **사실로 써도 되는 것**:
- Unity 6000.3.25f1 프로젝트에서 본편과 분리한 공모전 전용 브랜치·어셈블리로 제작 중.
- 기존 픽셀 아트만 재사용, 신규 아트·VFX 없음.
- 정책 근거는 조달청 누리집 물품구매 업무처리절차 페이지 1개(SOURCES.md), 실제 조달 절차를 재현하지 않는 단순화 창작 체험이며 조달청 제작·후원이 아님을 게임 안에 표시하도록 작성.
- 나라장터·실제 입찰 기능 없음.

현재 **완료형으로 쓰면 안 되는 것**(검증 전): 게임이 실행된다, 테스트 통과, 2~3분 안에 임무 1 완료, Windows 실행본·ZIP, 시연 영상, 화면 해상도 대응.

## 6. 재개 지점

1. 사용자/코디네이터의 재개 지시 후 메모리 확인, Unity 1개만 실행.
2. EditMode: `Unity.exe -batchmode -projectPath <worktree> -runTests -testPlatform EditMode -assemblyNames CCT.Contest.Tests -testResults Delivery/Verification/2026-11-10/pps-contest/logs/editmode-results.xml -logFile .../editmode.log` (첫 실행은 Library 새로 가져오기 필요, 이전 실행은 가져오기 중 중단).
3. 컴파일 오류 수정 → PlayMode(`-testPlatform PlayMode -assemblyNames CCT.Contest.PlayTests`).
4. `-executeMethod CCT.Contest.ContestBuild.ReleaseBatch -quit` / `CaptureBatch`.
5. 캡처본으로 `-batchmode -pps-data-dir <임시> -pps-shots <폴더> -pps-size 1920 1080` 및 1280 720 실행(오프스크린 렌더, 창·입력 없음). batchmode에서 렌더가 검게 나오면 코디네이터 GUI 슬롯 요청 후 창 모드로.
6. `-pps-video <폴더>` 30fps PNG → `ffmpeg -framerate 30 -i frame_%05d.png -c:v libx264 -pix_fmt yuv420p -crf 18 <mp4>` (ffmpeg 9.0.1 설치 확인됨).
7. ZIP `AI게임_실행파일_음료마스터_공공조달체험편_유형진.zip`, Steam 폴더 상태 사후 비교, 문서 갱신.
