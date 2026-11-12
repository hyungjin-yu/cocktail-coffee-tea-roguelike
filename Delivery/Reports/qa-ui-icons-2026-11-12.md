# QA — UI 아이콘 48종(연구 15·차 변형 7·카드 14·숙련 12) Unity 통합·실제 화면 검수 (2026-11-12, task_68388003a158)

아트: 아트 담당(ctx_04e18f8b56c0) `ArtSource/ui-icons-2026-11-12/` — Aseprite + PixelLab 제작, `manifest.json` **ready 48/48**(sha256 `e1db9fd2…5d94709`, `DELIVERY.md`, `evidence/final-audit.json`). Unity 담당은 아트 원본을 수정하지 않았다(임포트 전 해시 대조·원본 해시 불변).
인계표: `Delivery/Reports/ui-icons-2026-11-12-handoff.md`. 기준 스킬 madia-ui-ux·game-feel-first. Unity 6000.3.25f1. 증거 루트 `Delivery/Verification/2026-11-12/ui-icons/`.
조작 구분: 실제 빌드는 **QA 빌드 자동 조작**(사람 직접 플레이 NOT RUN). fixture: 스폰 정지·체력 증가, 카드 창에 새 그림 카드를 직접 제시(`CardView.Show`), 원두 30 지급 후 실제 커피 트리 보상 창에서 노드 선택, 연구 포인트 40 지급 후 숙련 해금·장착, 카드 보유는 `GrantCardForTests`. 격리 데이터(`-cct-data-dir`)·사용자 세이브 불변(save.json 09-28·drinks.json 10-27·settings.json 10-21). 4096MB 게이트·동시 1개 준수. GUI 슬롯은 코디네이터·아트 담당 양도 후 사용.

## 1. 상태 구분

| 묶음 | 실제 ID | ready(아트) | integrated(Unity 연결) | verified(실제 화면) |
|---|---|---|---|---|
| 커피/우림 연구 노드 | 15 | 15 | 15 — `Resources/ResearchIcons/research_<id>.png`, 연구 트리 노드 112×84 칸 · 목표 조합 커피 조건 칸 | 두 직업 720p/1080p PASS |
| 차 변형 | 7(espresso_shot·ristretto·full_body_roast·perfect_extraction·caffeine_rush·doppio_body·fine_grind) | 7 | 7 — `research_<id>_tea.png`, 티 소믈리에 우선. **변형이 계획된 노드는 차 그림이 없으면 커피 그림을 보이지 않고 글 상자 유지**(`tea_variant_required.txt`) | 티 소믈리에 720p/1080p PASS |
| 전용 카드 그림 | 14 | 14 | 14 — `Resources/CardEducation/card_*.png` + `Card_<id>.asset`·`CardEducationData` `educationArtId` | 카드 선택·도감·이번 판 빌드·목표 조합 PASS |
| 숙련 노드 | 12 | 12 | 12 — `mastery_<id>.png`, 노트북 숙련 노드 72×56 칸 · 상세 88 | 720p/1080p PASS(직업 무관 화면) |
| 합계 | 41 ID / 48 파일 | **48** | **48** | 아래 §3 |

## 2. 임포트·연결(정적·시험)

| # | 항목 | 결과 | 증거 | 상태 |
|---|---|---|---|---|
| I1 | 임포트 도구 `Tools/Art/import_ui_icons_2026_11_12.py` | 매니페스트 status=ready만, sha256 대조, 64×64·단일 프레임 PNG(acTL/fcTL 없음) 검사, 기존 `.meta` 보존·새 `.meta`는 CardEducation 설정(Sprite·Single·PPU 64·Point·밉맵 없음·무압축) 복제·새 GUID, 카드 에셋·`CardEducationData` 연결, 차 변형 계획 목록·불투명 영역(bounds) 생성. 최종 48 임포트·건너뜀 0 | `import-final.txt` | PASS |
| I2 | 진행 중 관리 | 진행 스냅샷마다 ready만 반영. 공지 전 ready로 바뀐 `card_flamed_zest`를 1회 임포트 후 되돌림(코디네이터 기준 정정 뒤 재적용), needs_review는 한 번도 반영 안 함 | 이 보고 | PASS |
| I3 | 가로로 긴 그림 판독 | 원본 변형 없이 런타임에서 불투명 영역으로 잘라(같은 텍스처의 부분 사각형) 가로형 칸(연구 112×84, 숙련 72×56)에 채움 | `ResearchIcons.cs`, 시험 `ResearchAndMasteryIcons_AreCroppedToOpaqueBounds_SameTexture` | PASS |
| I4 | 카드 그림 검증 확장 | `CardEducationImport.ValidateAll`: 원래 16종(2026-10-16 원본) + 새 묶음은 **매니페스트 ready 항목만** 허용(바이트 동일·64×64·Point) | `CardEducationImport.cs` | PASS |
| T1 | EditMode 전체 | 954 중 952 통과·실패 0·불확정 1·건너뜀 1(기존) — 신규 `UiIconImportTests`(매니페스트 대조·설정·미ready 미임포트·카드 자기 그림·차 변형 규칙·잘림) 포함 | `editmode-final.xml` | PASS |
| T2 | PlayMode 15묶음 | 103/103 + 수정 후 관련 3묶음 10/10 — 신규 `UiIconWiringPlayTests`(두 직업 노드 그림·글과 비겹침·효과 한 줄·그림 없는 노드 기존 배치) | `playmode-final.xml`, `playmode-final2.xml` | PASS |
| D1 | 커밋 기준 의존성 | 스테이징 트리(다른 사용자 미커밋 제외)만 꺼내 Roslyn csc: Runtime/Editor/EditMode/PlayMode 오류 0, `CCT_QA_TOUR` 포함 오류 0 | `depcheck-index-tree.txt` | PASS(컴파일만) |

## 3. 실제 화면(새 QA 빌드 `Builds/UiStates-QaTour-2026-11-12`, 소스 `f35921b4…fc91e9`)

| # | 화면·상태 | 결과 | 증거(`ic720/`, `ic1080/`, `ui-states720/`, `ui-states1080/`) | 상태 |
|---|---|---|---|---|
| V1 | 커피 연구 트리 — 해금됨·해금 가능·선행 필요(바텐더) | 15노드 모두 그림, 기능 구분(에스프레소/포터필터/저울/원두/그라인더/쟁반 등), 잠김 흐림, 글·단축키 비겹침 | `ic*/ic_*_bartender_rapier_20_study_locked.png`, `_21_study_mixed.png` | PASS |
| V2 | 우림 연구 트리(티 소믈리에) | 차 변형 7종(개완·찻주전자·찻잎 바구니·항아리 등) 표시, 공용 8종, 커피 도구 그림이 차 이름에 붙지 않음 | `ic*/ic_*_tea_bow_21_study_mixed.png` | PASS |
| V3 | 카드 선택(새 그림 카드 3장씩) | 칵테일 6·차 8 모두 자기 그림, 이름판 왼쪽 88 | `ic*/ic_*_10_card_select_*.png` | PASS(fixture 제시) |
| V4 | 도감 목록·상세 | 목록 38칸·상세에 새 그림 | `ic*/ic_*_01_codex_Tab_*.png`, `_02_codex_detail_*.png` | PASS |
| V5 | 이번 판 빌드(기록 카드 행) | 보유 새 카드 행 그림 | `ic*/ic_*_30_build_detail_cards.png` | PASS |
| V6 | 목표 조합 창 | 카드 조건 = 새 카드 그림, 커피 조건 = 노드 그림(티 소믈리에 차 변형) | `ui-states*/us_*_31_combo_target_view.png` | PASS |
| V7 | 연구 노트북 숙련 3갈래 — 장착·해금·해금 가능·잠김 | 12노드 그림, 잠김 흐림, 상세 88 그림 | `ic*/ic_*_40_laptop_mastery_{service,stability,finesse}.png` | PASS |
| V8 | 발견 결함 → 수정 | (a) 연구 노드 그림이 단축키 [n]와 겹침 → 칸 아래로. (b) 차 변형 없는 노드에 커피 그림 대체 → 계획 노드 글 상자 유지. (c) 좁아진 칸에서 '재료 추가 드랍 확률 +20%, 줍기 반경 +50cm'가 두 줄로 상태 줄과 겹침 → 한 줄 크기 맞춤(시험 추가) | 수정 후 같은 경로 재캡처 | PASS(수정 후) |
| V9 | 투어 전체 | icons 20장·ui-states 41장 × 720p/1080p, errors 0·exceptions 0 | `*/tour-log.txt` | PASS |
| V10 | 판독 주의(남김) | 720p에서 연구 칸 84 높이 ≈56px(64px 원본의 0.875배, 비정수 축소). 판독 가능하나 일부 가는 선(저울·주전자 손잡이)은 1px 손실 가능 | V1·V2 캡처 | 관찰(수용) |

## 4. 검토 빌드·패키지·커밋

- 검토 빌드 `Builds/UiIcons-Review-2026-11-12`(QA 코드 0건: IconsRun/UiStatesRun/HybridRun/DrinkLabCapture/PlayVerifier). 소스 2,918 파일 빌드 전·후 `f35921b4e203cbcffb2a0a2ad701b885186e841e05dce7d4d703eb74eefc91e9` 동일(= 최종 QA 빌드). 부팅: 빈 격리 폴더·입력 없음·예외 0(`review-build/boot_title_noinput.png`).
- ZIP `Delivery/CocktailCoffeeTea_UiIcons_2026-11-12.zip`(208 항목, testzip 정상, DoNotShip 0) sha256 **09bf97798a3d9d4d8c4fa3b9c17bcb344a94c0f7f379bdd72dc67f3565587abf**, 산출물 목록 `3c7991ee…2aa7480e`, CCT.Runtime.dll `6ed7daf5…ac83ce`. 기존 ZIP 7개(공모전·이전 검토) 해시 전후 일치.
- 빌드와 커밋 기준 차이: 빌드는 작업 트리 전체(다른 사용자 미커밋 포함, 예: `Card_chamomile.asset`·`Card_ginseng_root.asset`·`DefaultTeaCards.cs`·`ProjectSettings`), 커밋은 이번 범위만(+아트 납품 폴더를 원본 그대로 포함 — 임포트 검증 시험이 매니페스트를 읽음). 커밋 트리는 D1 컴파일만 확인.
- 공개 배포·Steam·추가 결제 없음. 기존 백업 브랜치 `origin/codex/coffee-brewing`에 push.

## 5. 남은 것(아트 부족 해소 후)

- FAIL / BLOCKED: 없음.
- NOT RUN: 사람 직접 플레이·실제 OS 입력, 커밋 트리 단독 Unity 시험·빌드(컴파일만), 검토 빌드 자체 상태 재현(같은 소스 QA 빌드로 확인), 터치 UI·다국어(PC 범위 밖 유지).
- 관찰: 720p 비정수 축소(V10).
