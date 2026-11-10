# QA — 공모전판(음료 마스터: 공공조달 체험편) 2026-11-10

- worktree: `C:/Users/user/orca/workspaces/CocktailCoffeeTea/pps-contest-2026`, 브랜치 `hyungjin-yu/pps-contest-2026`, 기반 `f7f3449`
- Unity 6000.3.25f1 · 저장 프로필: 테스트는 임시 폴더(`%TEMP%/pps-contest-*`), 사용자 save/PlayerPrefs 미접근
- **중단 사유:** 작업 중 코디네이터가 "사용자 게임 중 — 화면·입력·무거운 작업(Unity·빌드·렌더·인코딩) 보류"를 지시. 본인이 띄운 Unity 배치(PID 40436, Library 첫 가져오기 단계)만 강제 종료. 사용자 앱·다른 프로세스는 건드리지 않음.

## 1. 빌드/컴파일

| 검사 | 설정 | 기대 | 실제 | 증거 | 상태 |
|---|---|---|---|---|---|
| 컴파일 | EditMode 배치 실행 | CS 오류 0 | 패키지 등록·가져오기 중 중단, 컴파일 단계 도달 전 | `Delivery/Verification/2026-11-10/pps-contest/logs/editmode.log`(1667줄, `error CS` 없음 = 미도달) | BLOCKED |
| 정적 검토 | 코드 수동 검토 | 이름 충돌 등 | `ContestCapture.Path()`가 `System.IO.Path`를 가림 → `HierPath`로 수정 | 소스 | 수정함(컴파일 미확인) |
| 제출 빌드 | `ContestBuild.ReleaseBatch` | exe 생성 | 미실행 | — | NOT RUN |
| 캡처 빌드 | `ContestBuild.CaptureBatch` | exe 생성 | 미실행 | — | NOT RUN |
| ZIP | `AI게임_실행파일_음료마스터_공공조달체험편_유형진.zip` | — | 미생성 | — | NOT RUN |

## 2. 기능(자동 테스트)

| 검사 | 범위 | 상태 |
|---|---|---|
| EditMode 19개(`CCT.Contest.Tests`): 데이터 결정성·최저가 오답, 요청 확인, 공급 반려(품명·수량·예산·카페인·원산지), 입고(반품 재배송·결함 저장), 제조(재고·배치 한도·선입선출), 검수 불일치·보완·재검수, 재주문, 결함 재료, 납품 최신 검수 요구·중복 납품, 납기 실패, 3임무 최적 진행, 재시작 상태, 오배송, 저장(배지 1회·백업 복구·외부 JSON 거부·잠금) | 소스 | NOT RUN |
| PlayMode 4개(`CCT.Contest.PlayTests`): 전용 타이틀·본편 메뉴 부재·오인 방지·도움말 출처, 임무 1 UI 전체(반려→재선택→결함 입고→붓기 유지→불합격→제외→재제조→합격→납품→재실행), 잠금, 실패·재시작 | 소스 | NOT RUN |

## 3. 화면(실제 렌더)

| 검사 | 설정 | 상태 |
|---|---|---|
| 1920×1080 단계별 화면 17장 + 글자 잘림 감사 | 캡처본 `-pps-shots`, 오프스크린 | NOT RUN |
| 1280×720(낮은 해상도) 동일 | 〃 | NOT RUN |
| 키보드 실제 입력 | — | NOT RUN(GUI 슬롯 필요) |
| 오디오 | — | NOT RUN |
| 재미·난이도 평가 | — | NOT RUN |

## 4. 영상

| 항목 | 상태 |
|---|---|
| 수단 | 캡처 빌드 내부 시연 스크립트(게임 메서드 호출, OS 입력 없음) + `Time.captureFramerate=30` 오프스크린 프레임 PNG + ffmpeg 9.0.1(설치 확인: WinGet Gyan.FFmpeg) → H.264 MP4. 시연 장면: 타이틀→임무 선택→요청 확인→최저가 오답 반려→재선택→입고(파손 포장 확인 누락)→붓기→검수 불합격(재료 이력·수량)→제외·재제조→합격→납품→결과, 약 60초 설계. 녹화 화면 우상단에 "실제 플레이 화면 녹화 · 캡처 빌드 내부 자동 입력" 표시(소스) |
| 실제 촬영·인코딩 | NOT RUN |

## 5. 회귀·안전

| 검사 | 실제 | 상태 |
|---|---|---|
| Steam 폴더 무변경 | 작업 전/후 `git status --porcelain -uall`(587항목)과 수정·미추적 파일 sha256 목록 동일, HEAD `f7f3449` 동일 | PASS |
| Steam 폴더에서 복사 | 0개(필요 없음, BASELINE.md) | PASS |
| 사용자 save/PlayerPrefs | 공모전 코드에 PlayerPrefs 호출 없음(정적 확인). 실행 확인은 미실시 | 정적 PASS / 실행 NOT RUN |
| Unity 가져오기 부산물 | Unity가 기반 커밋에 없던 `.meta` 2개 생성(`Scripts/UI/FamilyLegend.cs.meta`, `Tests/EditMode/StandardFamilyTableTests.cs.meta`) | 기록 |
