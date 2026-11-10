# QA — 공모전판(음료 마스터: 공공조달 체험편) 최종 2026-11-11

- worktree `C:/Users/user/orca/workspaces/CocktailCoffeeTea/pps-contest-2026`, 브랜치 `hyungjin-yu/pps-contest-2026`, 시작 체크포인트 `3cc981e` + 이번 작업의 커밋 전 변경(아래 §7)
- Unity 6000.3.25f1, Windows 64비트, Mono
- 저장 프로필: 모든 테스트·캡처·실행 확인은 격리 폴더에서 진행했다(`%TEMP%/pps-contest-*`, 세션 임시 폴더 `ppsdata-*`, `-pps-data-dir`). 사용자의 본편 save와 PlayerPrefs는 읽거나 쓰지 않았다.
- 화면 입력 원칙: 게임 창을 띄운 GUI 조작, 전역 키·마우스 입력, 포커스 변경을 모두 하지 않았다. 실제 렌더는 플레이어 `-batchmode`에서 오프스크린 RenderTexture로 받았다(창 없음).
- 이전 기록: `qa-pps-contest-2026-11-10.md`(2026-11-10 중단 시점)

## 1. 빌드·컴파일

| 검사 | 설정 | 기대 | 실제 | 증거 | 상태 |
|---|---|---|---|---|---|
| 컴파일 | 배치 실행 | CS 오류 0 | 오류 0, 공모전 코드 경고 0 | `logs/editmode.log` | PASS |
| 제출 빌드 | `CCT.Contest.ContestBuild.ReleaseBatch` | 성공 | Succeeded, 546,821,515 B, 오류 0 | `logs/build-release-final.log` | PASS |
| 캡처 빌드 | `ContestBuild.CaptureBatch` (`PPS_CAPTURE`) | 성공 | Succeeded, 오류 0 | `logs/build-capture-final2.log` | PASS |
| 제출 빌드에 캡처 코드 없음 | `CCT.Contest.dll`에서 `ContestCapture` 문자열 검색 | 0 | 제출 0건, 캡처 7건 | 명령 출력 | PASS |
| 두 빌드의 게임 로직 동일 | `PPS_CAPTURE`가 쓰인 곳 검색 | 캡처 전용 파일만 | `ContestCapture.cs`(전체가 `#if PPS_CAPTURE`)와 `ContestBuild.cs`(정의 지정)뿐. 게임 로직·UI 소스는 두 빌드가 같다 | grep 결과 | PASS |
| 제출 실행 파일 기동 | `-batchmode -pps-data-dir <임시>`, 20초 후 종료 | 예외 없음 | 예외·오류 0건, 엔진 초기화 확인 | `logs/release-smoke.log` | PASS |
| ZIP 무결성·압축 해제 후 기동 | Python `testzip`, 압축 해제 후 같은 방식으로 기동 | 정상 | testzip 이상 없음, 209개 항목, UTF-8 파일 이름 플래그 0x800, 기동 시 오류 0건 | 세션 로그 | PASS |

## 2. 기능(자동 테스트)

| 검사 | 범위 | 실제 | 증거 | 상태 |
|---|---|---|---|---|
| EditMode `CCT.Contest.Tests` | 19개(임무 데이터·요청 확인·공급 반려·입고·제조·검수 보완·재주문·결함 재료·납품 중복·납기 실패·3임무 최적 진행·재시작·오배송·저장 4개) | 19/19 Passed | `logs/editmode-results.xml` (sha256 05df6e1e7237e13a…) | PASS |
| PlayMode `CCT.Contest.PlayTests` | 4개: 전용 타이틀(본편 메뉴 없음, 오인 방지 문구, 도움말 출처) / 임무 1 전체 UI(오답 공급안 반려 → 재선택 → 파손 포장 입고 → 붓기 1초 이상 유지 → 검수 불합격 → 제외 → 재제조 → 합격 → 납품 → 배지 1회 → 재시작 시 새 상태) / 잠긴 임무 / 실패 후 재시작 | 4/4 Passed (UI 최종 수정 후 다시 실행) | `logs/playmode-results-final.xml` (d2d5811e75d445c0…) | PASS |

PlayMode 테스트의 입력은 버튼 `onClick` 호출과, 붓기를 누르고 있는 상태를 흉내 낸 게임 내부 플래그다. 실제 마우스·키보드 이벤트 경로는 아니다(§4 참고).

## 3. 화면(실제 렌더, 오프스크린)

설정: 캡처본 `-batchmode -pps-shots <폴더> -pps-size W H`. 단계마다 17장을 찍고, 각 장면에서 활성 Text 전체를 대상으로 글자 넘침을 검사했다(`layout-audit.txt`).

| 검사 | 해상도 | 실제 | 증거 | 상태 |
|---|---|---|---|---|
| 1차 촬영 | 1920×1080 | 결함 발견: 메인 제목 "음료 마스터"가 넘침으로 사라짐 / 제조 화면의 "재료·물 온도·용기" 소제목 사라짐 / 선택한 온도·용기가 스킨에 가려 표시되지 않음 / 안내 메시지 1줄로 잘림 / 검수 행 불일치 사유 잘림 / 도움말 뒤 글자가 비쳐 보임 / 학습 요약 마지막 줄이 테두리에 닿음 / 실패 화면 단계 표시가 1~5 "완료", 6 "납품"으로 잘못 표시 | 수정 전 세션 기록 | FAIL → 수정 |
| 수정 후 재촬영 | 1920×1080 | 17장 모두 넘침 0, 최소 글자 20px | `Delivery/Verification/2026-11-11/pps-contest/shots-1080/` | PASS |
| 수정 후 재촬영 | 1280×720 | 17장 모두 넘침 0(완성 개수 칸 2px 부족을 고친 뒤), 최소 글자 약 13px | `shots-720/` | PASS(검수 행 글자 작음, 아래 참고) |
| 직접 확인한 장면 | 1080: 01·02·06·09·11·13·15·17, 720: 02·13 | 이미지를 직접 열어 겹침·잘림·대비·선택 표시를 확인 | 위 폴더 | PASS |

알려진 한계: 720p 검수 화면의 행 글자는 자동 맞춤으로 약 10px까지 작아진다. 읽을 수는 있지만 작다. 그 밖의 장면 이미지는 자동 감사만 했고 사람 눈으로는 보지 않았다.

1080 스크린샷 sha256(앞 12자리): 01 f6d26400e47b, 02 76f59201840e, 03 4b75df9f7ae2, 04 dd211ef7e1d0, 05 2d7bf45b1146, 06 52d306a7d5c6, 07 2d01c2365dfc, 08 a1777bb39a0e, 09 861ee9ba521f, 10 59a7bd0b30df, 11 01561552ca36, 12 8baacfdd6814, 13 26103d16c946, 14 54488a4a40bf, 15 cf5a951f409d, 16 1170b420acc4, 17 bd7ea298be3d

## 4. 입력·기타

| 검사 | 상태 | 비고 |
|---|---|---|
| 실제 마우스 클릭·키보드(InputSystem 이벤트) 플레이 | NOT RUN | 창 GUI 조작을 최소화하라는 지시에 따라 하지 않음. 키보드 단축키 코드는 PlayMode 테스트 범위 밖 |
| 사람이 직접 플레이한 소요 시간 | NOT RUN | 자동 시연 기준으로 임무 1은 실수와 보완 1회를 포함해 약 40초(6.9초 → 46.9초) |
| 오디오 | NOT RUN | 공모전 화면은 오디오 서비스를 켜지 않음 |
| 재미·난이도 평가 | NOT RUN | |
| 창 모드 크기 조절 | NOT RUN | 빌드 설정은 창 모드 1920×1080, 크기 조절 가능 |

## 5. 영상

| 항목 | 실제 | 상태 |
|---|---|---|
| 촬영 방식 | 캡처 빌드를 `-batchmode`로 실행. 게임 내부 시연 스크립트가 제출본과 같은 게임 메서드를 호출하고, `Time.captureFramerate=30`으로 매 프레임을 오프스크린 렌더해 PNG 1742장을 남김. OS 전역 입력 없음. 화면 오른쪽 위에 "실제 플레이 화면 녹화 · 캡처 빌드 내부 자동 입력" 표시. 스틸 슬라이드 아님 | PASS |
| 장면 | 타이틀 → 임무 선택 → 요청 6항목 확인 → 최저가 공급안(홍차) 반려 사유 → 초록잎상회 선택 → 입고 3봉(파손 포장을 확인하지 않고 입고) → 80℃·찻잔 선택 → 붓기 → 검수 불합격(재료 이력 불일치 2잔, 수량 부족) → 2잔 제외 → 2잔 재제조 → 재검수 합격 → 납품 → 결과 기록 → 임무 2 요청서 | PASS |
| 인코딩 | ffmpeg 9.0.1, libx264 crf18 yuv420p faststart | PASS |
| 결과 확인 | ffprobe: h264 1920×1080, 30fps, 1742프레임, 58.07초, 2,284,840 B. 디코딩한 520·1050·1660번 프레임을 직접 확인 | PASS |

## 6. 회귀·안전

| 검사 | 실제 | 상태 |
|---|---|---|
| Steam 폴더 무변경 | 2026-11-11 작업 전·후 `git status --porcelain -uall`(587항목)과 수정·미추적 파일 sha256 목록이 같음. 2026-11-10 스냅샷과도 같음. HEAD f7f3449 | PASS |
| Steam 폴더에서 복사한 파일 | 0개 | PASS |
| 사용자 save·PlayerPrefs | 공모전 코드에 PlayerPrefs 호출 없음. 실행은 모두 `-pps-data-dir` 격리 폴더에서 함. 공모전 제품 폴더 `LocalLow/PPSContest2026/BeverageMasterPPS`에는 Unity 테스트 러너의 TestResults.xml과 Unity 캐시만 있고 게임 저장 파일은 없음 | PASS |
| 프로세스 | Unity·플레이어를 한 번에 하나만 실행. 다른 앱은 종료하지 않음. 2026-11-10에는 본인이 띄운 Unity(PID 40436)만 종료 | PASS |

## 7. 이번 변경(커밋 전, worktree)

- `Assets/_Project/Contest/ContestApp.cs`: 제목 높이, 소제목·메시지 높이, 선택 테두리, 검수 행 자동 맞춤, 도움말 불투명 바탕, 학습 요약 패널, 실패 시 단계 표시
- `ContestUiKit.cs`: `MarkSelected`
- `ContestCapture.cs`: batchmode에서 프레임을 직접 렌더, 캡처 표시 문구, 시연 마지막에 다음 임무로 이어지는 장면
- `ProjectSettings/ProjectSettings.asset`: 창 모드(3), 창 크기 조절 가능(빌드 스크립트가 설정)
- Unity가 공모전 폴더 `.meta` 9개에 importer 항목을 보충(GUID 같음). URP·Graphics·ShaderGraph·EditorBuildSettings 자산은 줄 끝만 달라짐(diff 내용 없음)
- 새 파일: `Docs/Competition/README_실행안내.txt`(ZIP에 포함)
