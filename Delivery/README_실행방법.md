# 칵테일 · 커피 · 차 — 실행 방법 (Windows 64비트, 버전 1.0.0)

최신 UI·직업·활 수정 실행본: `CocktailCoffeeTea_1.0.0_review-fixes-final-2026-10-12.zip` (2026-10-12). 아래 압축 해제 방법은 동일합니다.

## 실행
1. `CocktailCoffeeTea_1.0.0_Windows64.zip`을 원하는 폴더에 압축 해제합니다(경로에 한글·공백이 있어도 됩니다).
2. `CocktailCoffeeTea.exe`를 실행합니다. Unity 에디터나 별도 설치는 필요 없습니다. 인터넷 연결·계정이 필요 없습니다.
3. 폴더 안의 `CocktailCoffeeTea_Data`, `MonoBleedingEdge`, `D3D12`, `UnityPlayer.dll`, `UnityCrashHandler64.exe`를 지우거나 옮기지 마세요.

## 조작
| 동작 | 키보드 | 마우스(설정에서 "조작: 마우스") |
|---|---|---|
| 이동 | WASD 또는 방향키 (대각선 속도는 직선과 같음) | 커서 쪽으로 이동(캐릭터 가까이 두면 정지) |
| 공격 | 자동 | 자동 |
| 메뉴 이동/선택 | 방향키 + Enter/Space | 클릭 |
| 카드 선택 | 1 / 2 / 3 | 클릭 |
| 히든 효과 확인 | H (Esc로 즉시 재개) | 왼쪽 히든 효과 버튼 |
| 커피 트리 | K | 일시정지 메뉴의 "커피 트리" 버튼 |
| 일시정지/닫기 | Esc | 오른쪽 위 일시정지 버튼 |

조작 방식은 화면 왼쪽 아래 "조작: 키보드/마우스" 표시를 클릭하거나 설정에서 바꿀 수 있으며 저장됩니다.

## 설정
해상도(1920×1080 / 1280×720), 창/테두리 없는 전체화면, VSync, 마스터·BGM·효과음 볼륨(0이면 완전 무음), 화면 흔들림, 효과 품질(보통/낮음), 피해 숫자, 조작 방식, 조작 안내 다시 보기.

## 저장
- 저장 위치: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea\save.json`(진행), `settings.json`(설정). 같은 폴더에 백업(`.bak`)이 함께 생깁니다.
- 런 도중 이어하기는 없습니다. 런 중에 게임을 끄면 그 런은 "포기"로 1회 집계되고 모은 재료 체크포인트는 보존됩니다.
- 저장에 실패하면 화면에 표시되며 저장 다시 시도 버튼으로 재시도할 수 있습니다. 손상된 파일은 백업에서 복구합니다.

## 로그
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea\Player.log`

## 검증 PC와 알려진 사항
- 검증 PC: Windows 11 Pro 22631, Intel i7-14700F, RAM 16GB, NVIDIA RTX 5060, 1920×1080@100Hz. 이 PC 기준으로 측정했으며 모든 PC의 최소 사양을 보장하지 않습니다.
- 게임패드는 지원 대상이 아닙니다(키보드·마우스 완전 지원).
- 검증 결과와 G01~G35 증거표는 프로젝트의 `Delivery/Reports/release-resume-2026-10-09.md`에 기록됩니다(이전 후보 기록은 `release-candidate-2026-10-08.md`).

## 라이선스
`THIRD_PARTY_NOTICES.md`, `LICENSE_NotoSansCJK.txt`, `Licenses/Packages/`의 원문 고지를 함께 배포합니다. 포함된 Unity 패키지와 버전은 `Licenses/Packages/package-inventory.csv`에 있습니다. 게임 코드·아트·음악·효과음은 이 프로젝트에서 제작한 것입니다.

`SHA256SUMS.csv`는 배포 파일의 SHA-256 목록입니다. ZIP의 SHA-256은 별도 `.zip.sha256` 파일에서 확인할 수 있습니다.

