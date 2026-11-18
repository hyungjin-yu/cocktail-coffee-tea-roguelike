# itch.io Windows 데모 업데이트 — 2026-11-17

상태: **공개 패키지 준비·검증 완료 / 사이트 업로드 차단(미완료)**. 업로드·Save·공개 다운로드 재수신 검증은 아직 수행하지 못했다 — 성공으로 기록하지 않는다.

- 사용자 지시(2026-11-17): itch.io 업데이트. 이번 범위에서 itch.io 공개 업로드 허용(GitHub/Android 게시 금지 유지).
- 공개 페이지: https://hyungjin-yu.itch.io/coffee-cocktail-tea · 관리 프로젝트 ID 5148920.
- 새 공개 파일: `Upload/Beverage-Master-Windows-Demo-2026-11-17.zip`
  - 크기 109,819,305 bytes, 272 files
  - SHA256 `03caffc02bee1bf6b56f2aa2ab1b9f5e47a9ead653397434b406867bfa45b1db` (`.zip.sha256`)

## 패키지(완료)

원본: `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/Delivery/CocktailCoffeeTea_FeedbackReview_2026-11-17.zip`
SHA256 `39a09d87f0df1522bd719088b87d66f8802c29cc317e6063fb483aea935a64de`(스크립트에서 일치 확인). 게임 재빌드 없음.

도구: `Upload/package_itch_update_20261117.py`, 결과 `itch-update-20261117-package.json`(11-12와 같은 방식).

- 상위 폴더 제거, Windows 게임 파일을 ZIP 루트에 배치(`CocktailCoffeeTea.exe` 루트 확인).
- 원본 게임 파일 207개 SHA256 전수 대조 — 변경 없음. ZIP CRC(testzip) 정상.
- 제외: 검수본 전용 `Play-Review-IsolatedSave.cmd`, `README_실행안내.txt`. 개인 세이브(save/settings/drinks/run-history.json)·review-data·DoNotShip 없음(검사 통과).
- QA 자동화 타입 미포함: `CCT.Runtime.dll`(SHA256 `7b517c490d7a1fac8dea63212acb407d860598bbc6b4402b17ada99638eb3553`)에 PlayVerifier·UiStatesRun·HybridRun·UiAuditRun·Feedback2Run·Feedback4Run·AmericanoRun·ContainersRun·Smooth13Run·QaTourHelpers 문자열 없음, `AutoTour`는 릴리스 실행 인자 파서 속성 `AutoTourDir`(릴리스는 인자 거부)만 존재.
- 유지: 11-12 공개본의 `Licenses/*`, `THIRD_PARTY_NOTICES.md`, `LICENSE_NotoSansCJK.txt`(63개).
- 추가: `README_실행방법.txt`(11-17 변경 사항, 실행·짧은 경로 압축 해제, 저장 위치·업데이트 전 백업 권장, 개발 중·미검증 범위, 생성형 AI 고지), `SHA256SUMS.txt`(전 파일 해시).

## 사이트 업로드(차단)

- 시도 경로: 기존 로그인 Chrome을 `orca-computer-use`(11-12와 같은 경로)로 조작.
- 차단 원인: Windows Defender AMSI가 Orca 컴퓨터 제어 런타임 `C:/Users/user/AppData/Local/Programs/orca/resources/computer-use-windows/runtime.ps1`(2026-11-16 19:26 갱신, 135,565 bytes)을 악성 스크립트로 판정해 실행 차단 — PowerShell `ScriptContainedMaliciousContent`, Defender ThreatID 2147743659, 탐지 2026-11-17 15:30:51·15:31:01(ActionSuccess True). `orca computer capabilities`·`list-windows` 모두 같은 오류.
- 보안 차단 우회·쿠키/키/세션 토큰 읽기 금지 지시에 따라 우회하지 않았다. 이 세션에는 Chrome 확장 도구도 없다.
- 기존 11-12 공개 파일·페이지 설정은 손대지 않았다(가격·공개 범위·제목·이미지·계정 설정 변경 없음).

## 남은 단계(차단 해제 후)

1. itch 관리 페이지(5148920) → Upload files → 위 ZIP 선택 → Success 확인 → Executable·Windows 체크 → 다운로드 허용.
2. 11-12 파일 `Beverage-Master-Windows-Demo-2026-11-12.zip`: 삭제하지 말고 Hide this file.
3. 설치 안내: 새 파일명·짧은 경로·실행/조작/저장 백업·개발 중 한계 갱신. AI 고지 유지.
4. Save → Saved 확인 → 공개 페이지에 새 파일 표시 확인 → 공개 Download 재수신 → SHA256 `03caffc0…b1db`·CRC 대조 후 이 문서 갱신.
