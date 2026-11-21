# QA — 카페 첫 화면 검수본 r7: 메뉴 접근·물체 조작 연출 + 거리 배경 + 메뉴 나무·종이 톤 (2026-11-20)

- r7: `Builds/CafeTitle-2026-11-20-r7`, ZIP `Delivery/CocktailCoffeeTea_CafeTitle_2026-11-20_r7.zip` sha256 `0f7000abd0da2c2b712317664f3f8437a1c19c538b1e53930498ded14022359c` (209 파일). r1~r5 ZIP 보존·해시 재확인 OK. 중간 빌드 `CafeTitle-2026-11-20-r6`(라디오 수정 전)·`-r6-interim`(톤 보정 확인용)은 검수 증거로만 보존, 납품 아님.
- 반입(해시 일치, 원본 ArtSource 미편집): v7 거리 배경(layout 89692689…, g0 교체·street_backdrop z-10) → v9 메뉴 조작(manifest e6611b02…, layout a93a383f… = v7 + approach 8블록, 48 PNG) → v10 라디오(manifest 42385 6a8…, settings_00~05 6 PNG만, layout 불변). 기록 `Delivery/Verification/2026-11-20/r6/import-v9.json`, `import-v10-radio.json`.
- 보호: 실행 전마다 게임 프로세스 0 확인, PID37784는 `unity mcp`(에디터 아님), gated-unity.sh(RAM 예외 없음), 격리 data-dir·음량 0·회당 150초 상한, 실제 저장 7파일 해시 불변. 커밋/게시 없음. 사용자 게임 창·입력 미조작.

## 동작
1. 실내 물체 클릭·Enter → 그 물체 쪽으로 시점 접근(배우 없음, 실내와 같은 contain 변환의 카메라 center/zoom 연속 이동, smoothstep) → 물체 레이어 조작 프레임 → **모든 입력이 떼어진 다음 프레임**에 기존 메뉴.
2. 메뉴 닫기 → 조작 역재생 → 원시점 → 입력 재개·기본 선택(이전처럼 Enter/Space 단독 시작 없음).
3. 진행 중: 실내 클릭·Tab/방향키·중복/다른 메뉴 확정 차단, 명패·상단 제목 숨김. 새 키/클릭 = 건너뛰기(끝 상태, 그 입력은 메뉴로 안 샘), Esc = 취소(메뉴 안 열고 원위치).
4. 저이펙트: 시점 이동 없이 마지막 조작 프레임 0.15초 → 메뉴.
5. 시작: 카운터 영업 준비 연출 뒤 기존 직업/무기/제조 선택(순서 불변). 미래 버전 보호면 카운터에서 물러나 임시 플레이 안내.
6. 종료: 문 연출 뒤 기존 RequestQuit(저장·QuitGuard 선행). 미저장 확인창 취소 시 문에서 물러남. 연출만으로 종료하지 않음.
7. 입장 입력 소비·재진입 짧은 페이드 등 기존 입장 흐름 불변.
8. 메뉴 톤: 타이틀이 만든 6개 메뉴 화면에만 — 카페 패널 그림 흰 틴트 → 따뜻한 곱 틴트(1,.92,.62), 무채색·보라 단색 → 나무/종이(어두운 바탕만 약간 밝힘), 글자 휘도·빨강/청록·파랑/초록/금색·알파 유지. 전투 일시정지·결과·직업 선택 인스턴스 미적용(PlayMode PauseView/SettingsView/ResultView 통과).
9. 창·출입문 밖 회색/검은 면 → 거리 풍경(v7).

## 변경 파일
`UI/CafeMenuApproach.cs`(신규), `UI/CafeMenuTheme.cs`(신규), `UI/CafeTitleLayout.cs`(approach 파싱), `UI/CafeTitleScene.cs`(명패 숨김·카메라 조회·조작 중 조명 억제), `UI/TitleView.cs`(접근 연결·입력 게이트·원위치·제목 숨김), `Core/CafeTitleCapture.cs`(QA), `Tests/EditMode/FirstPassArtTests.cs`, `Tests/PlayMode/TitleViewTests.cs`, `Resources/CafeTitle/layout.json`·`ready/interior/{g0,street_backdrop}.png`·`ready/approach/*.png`(48).

## 검사
| 검사 | 결과 |
|---|---|
| EditMode `TitleLayoutTests|FeedbackBatch3Tests` | 17/17 (최종 코드) |
| PlayMode `TitleViewTests|KeyboardNavigationTests|PauseView|SettingsView|ResultView` | 28/28 (라디오 PNG 교체·톤 보정 전 코드; 이후 변경은 표시 전용·QA) |
| **r7 실빌드** normal720 / normal1080 / normal4x3 | **85/0 ×3**: 6메뉴+도감 Enter+시작 각각 접근 시작→목표 zoom 도달→물체 프레임 변화→메뉴 열림→닫기→원시점·그림 원복·명패·입력 재개, 중복/다른 메뉴 차단, Space 건너뛰기 누출 없음, Esc 취소, 메뉴 톤 적용, 선반/의상장 클릭·명패·키보드·조명 반응·입장·시작→직업 선택·재진입 |
| r7 low720 | 10/0: 저이펙트 메뉴 시점 이동 0, 라디오 마지막 프레임만 표시 후 설정 열림 |
| r6 빌드(라디오·톤 보정 전) future720 / savefail720 / np720 | 21/0, 22/0, 7/0 — 미래 보호 시작 원위치, 문 연출 동안 확인창 없음→확인창→취소 시 원위치, 입장 1회 후 Enter/Space 자동 시작 0. r7에서 재실행 안 함(해당 경로 코드 불변) |

## 판독(실제 화면 캡처 기준, 사람 판정 아님)
- 라디오(v10): 720에서도 둥근 노브의 어두운 지시선이 각도를 바꾸고 아래 표시등이 어두운 호박→밝은 금색으로 켜짐, 역재생 후 원 그림(`r7/radio_ingame_{normal720,normal1080,normal4x3}.png`). 저이펙트는 시점 이동이 없어 실내 전체 크기에서 마지막 상태만 보여 작게 보임(`r7/low_radio_final_small.png`).
- 나머지 7개 조작: 읽힘(`r6/contact_acting_1080.png`, 각 해상도 `approach_*_acting/moving`, `return_*_moving`).
- 메뉴 톤: `r6b/menu_theme_r5_vs_r6b_1080.png`.

## 최종 공백 마감(2026-11-20 10:43~, r7 그대로, 재빌드·새 ZIP 없음)
- 실행 전: 게임/다른 게임 프로세스 0, Unity 에디터 0(PID37784 `unity mcp` CLI만), RAM 4316MB 게이트 통과.
- PlayMode `TitleViewTests|KeyboardNavigationTests|PauseView|SettingsView|ResultView` **최종 소스(라디오 v10·메뉴 톤 보정·제목 숨김 포함) 28/28** (`r7/playmode-final.xml`) — 전투 일시정지·결과 화면 불변 포함.
- **r7 빌드** future720 **21/0**(시작 차단 후 카운터에서 원위치 zoom 1.00), savefail720 **22/0**(문 연출 동안 확인창 없음→확인창→취소 시 원위치), np720 **7/0**(입장 1회 후 Enter/Space 자동 시작 0). r6 인용 공백 해소. 실행 후 게임 프로세스 0, 실제 저장 해시 불변.
- 카메라 목표 범위: 8개 모두 16:9·4:3에서 장면 경계 안(빈 영역 노출 0) — 계산표는 보고 생성 스크립트 출력.
- 대표 contact sheet(r7 실제 캡처만): `r7/r7_runtime_contact.png` — 6메뉴 접근 이동/조작/복귀(역재생), 시작 카운터 접근, 종료 문 접근→저장 확인→취소 복귀, 평상시 실내(거리 풍경), 메뉴 톤(설정·스킨). r6 contact_acting은 제목 숨김 전 중간본이라 최종 근거로 쓰지 않음.
- 마지막 조작 프레임(8개)은 실행 중 캡처가 없어 **오프라인 재구성**(r7 Resources 레이어+프레임을 layout 카메라 center/zoom으로 같은 방식 크롭·Point 확대, 런타임 캡처 아님): `r7/offline_final_frames_first_last_full.png`.
- 검토 결과: 카메라 이탈·원근/가림 훼손·동작 의미 훼손 없음 → **수정 없음, r7 최종**. 시작(컵 이동)과 스킨(앞치마 이동)은 변화가 작아 은은함(결함 아님, 사람 체감 확인 필요). 저이펙트 라디오는 설계상 시점 이동 없음이라 작게 보임(새 줌 추가 안 함).

## 미검증
- 사람 직접 플레이·모니터 체감(연출 속도·자연스러움·톤·작은 동작 판독): NOT RUN.
- 터치 입력: NOT RUN.
- 8개 마지막 조작 프레임의 런타임 캡처: NOT RUN(오프라인 재구성으로 대체, r7 빌드 재실행 없이).

## 공개 업로드용 ZIP (itch.io 기존 게임 업데이트용, 업로드는 코디네이터 담당 — 작업자 미업로드)
- `Delivery/Beverage-Master-Windows-Demo-2026-11-20_cafe-r7.zip` sha256 `70574e18866ce621e8159da5cbd18123173dbe0a851147a25ccbc95583e9e44a` (270 파일). r7 빌드 파일 그대로 + 11-17 공개본과 같은 배치(최상위 폴더 없음)·같은 라이선스 고지 62개(LICENSE_NotoSansCJK.txt, Licenses/Packages/*)를 11-17 공개 ZIP에서 원본 그대로 복사. 검수용 README·BurstDebug(DoNotShip)·pdb·저장/설정/fixture/QA 결과 파일 없음.
- 왜 별도: r7 검수 ZIP에는 라이선스 고지가 빠져 있고 검수용 README가 들어 있어 공개용으로 그대로 쓰면 안 됨.
- DLL 비교: 11-17 공개본 대비 추가는 `CCT.Prototype.Coop2P.dll` 하나(로컬 2인 실험판). `-cct-coop2p` 명시 인자가 없으면 시작하지 않으며(LaunchOptions.Coop2PRequested) 일반 실행 영향 없음. 빌드 후 DLL만 빼면 시작 시 어셈블리 누락 위험이 있어 그대로 둠 — 공개본에 숨은 실험 코드가 포함된다는 점은 판단 필요. 다른 Unity 패키지 DLL 차이 없음 → 고지 범위 동일.
- QA 드라이버(CafeTitleCapture/CombatToneCapture/Coop 검증)는 코드에 있으나 `-cct-data-dir`+각 캡처 인자를 함께 줄 때만 실행 → 일반 실행 자동 실행 없음.
- 실행 확인: 새 폴더에 풀어 격리 data-dir·음량 0으로 np720 **7/0**, player.log exception/error 0, 종료 후 프로세스 0. 실제 저장 해시 불변.
