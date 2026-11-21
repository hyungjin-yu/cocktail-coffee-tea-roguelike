# itch.io 개선용 r7 스크린샷 + 공개 ZIP 보완 (2026-11-20)

## ⚠ 먼저: 업로드된 공개 ZIP 보완 필요
- 코디네이터가 업로드한 `Beverage-Master-Windows-Demo-2026-11-20_cafe-r7.zip`(sha256 70574e18…)에는 11-17 공개본에 있던 **`README_실행방법.txt`(실행·조작·저장 백업·생성형 AI 고지), `SHA256SUMS.txt`, `THIRD_PARTY_NOTICES.md`가 빠져 있음**(작업자 누락). 라이선스 고지 62개는 있음.
- 보완본: `Delivery/Beverage-Master-Windows-Demo-2026-11-20_cafe-r7b.zip` sha256 `f475676158fb1256ef204e0f9b568c9472dccc15a6c06512a5b6c52272784518` (273 파일). r7 빌드 게임 파일 동일 + 11-17 공개본의 LICENSE_NotoSansCJK.txt·THIRD_PARTY_NOTICES.md·Licenses 원문 + 11-20용 README(AI 고지 유지) + 전 파일 SHA256SUMS. 11-17 공개본 대비 파일 목록 차이는 `CCT.Prototype.Coop2P.dll` 하나(`-cct-coop2p` 인자 없으면 비활성).
- 확인: 새 폴더 압축 해제 → SHA256SUMS 전부 일치, 격리 data-dir·무음 실행 normalpath 7/0, player.log exception 0, 프로세스 잔존 0, 실제 저장 해시 불변.
- 업로드 교체는 하지 않음(코디네이터 담당). 교체 시 기존 r7 파일은 숨김 처리 권장.
- 참고: 11-17 공개본 노트는 "QA 자동화 타입 미포함"이었으나 r7에는 `CafeTitleCapture`·`CombatToneCapture` 타입이 들어 있음(`-cct-data-dir`+각 캡처 인자 없이는 실행 안 됨). 완전 제거는 재빌드 필요.

## 스크린샷 (공개용 4장 + 예비 1장) — `Delivery/Verification/2026-11-20/itch-shots/public/`
원본 빌드: `Builds/CafeTitle-2026-11-20-r7`(ZIP 0f7000ab…). 소스·빌드 변경 없음, 가짜 합성 없음. 해시·원본 경로: `public/screenshots.json`.

| 파일 | 원본 | 정상 실행/fixture | 선택 이유 |
|---|---|---|---|
| `01_exterior_sign_1920x1012.png` | r7 실빌드 검수 캡처(CafeTitleCapture normal1080) 첫 실행 외관 대기 | 정상 첫 실행 화면(입력 전), 격리 저장 | 게임명 간판·입장 안내 |
| `02_cafe_interior_street_1920x1012.png` | 같은 실행, 메뉴 닫은 뒤 실내 평상시(강조 없음) | 정상 화면(자동 입력으로 도달), 격리 저장 | 물건=메뉴 구조, 창·문 밖 거리, 8개 이름표 |
| `03_bar_combat_bright_1920x950.png` | r7에서 CombatToneCapture `scene=normal` 1회 실행, 런 시작 16초 | **강제 스폰·상태 조작 없음**, 다만 입력 장치를 끈 무입력 런(플레이어는 서 있고 자동 공격만), 선택 화면 생략하고 바텐더·레이피어로 바로 시작 | 밝아진 칵테일 바·적 무리·HUD |
| `04_laptop_approach_1920x1012.png` | r7 검수 캡처, 영구 강화 노트북 접근 시점(메뉴 닫은 직후 역재생 첫 프레임 = 화면 켜진 상태) | 정상 연출 화면(자동 입력) | 물체 접근·조작 연출 |
| `04b_bookshelf_approach_1920x1012.png`(예비) | 같은 방식, 도감 책장 책 펼친 상태 | 정상 연출 화면 | 대체 후보 |

- **크롭**: r7의 지원 캡처 경로 2개(CafeTitleCapture·CombatToneCapture)는 왼쪽 아래에 QA 배너를 그린다 → 배너와 그 행의 하단 UI(버전·저장 상태 / 전투 하단 구간·조작 안내 줄)가 반쯤 잘리지 않도록 아래쪽만 잘라냄: 타이틀 1920×1012, 전투 1920×950. 좌우·내용 변경 없음.
- 음료 제조 화면은 r7에 지원 캡처 경로가 없어(제조 캡처 드라이버는 QA 투어 빌드 전용) 만들지 않고 접근 물체 장면으로 대체.
- 첫 런 안내(바 스푼 카드 등)·레벨업 창이 없는 프레임을 골랐다.

## 페이지 정보(현재 r7 기준 확인)
- 플랫폼: Windows 64비트(Unity 6000.3, D3D12). 다른 플랫폼 빌드 없음.
- 조작(코드 확인 GameManager K/H/T, 카페 입력): 카페 — 아무 키/문 클릭으로 입장, 물건 클릭 또는 방향키·Tab 선택 후 Enter, 연출 중 키=건너뛰기·Esc=취소. 전투 — 이동 WASD/방향키, 자동 공격, 메뉴 마우스, 카드 1/2/3, Esc 닫기/일시정지, K 연구, H 히든, T 목표 조합.
- 언어: 화면 글 한국어만(설정에 언어 선택 없음). 게임명 "음료 마스터"(GameInfo.Title), 영문명 Beverage Master.
- AI 고지: "코드와 아트에 생성형 AI의 생성·보조 결과가 포함" — 11-17 README 문구 유지(r7b README에 포함). 이번 카페 아트도 Aseprite + PixelLab 생성 원본 재배치(아트 manifest 기록).
- 미검증: 사람 직접 플레이·소리·터치·모바일.
