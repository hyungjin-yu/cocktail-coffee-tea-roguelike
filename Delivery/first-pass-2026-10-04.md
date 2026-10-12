# 1차 통합 실행본(First-Pass Preview) — 2026-10-04

Orca task `task_dd1fd30944b9` / dispatch `ctx_ed1de9e66890` (Run `run_b69bb3be0912`). 대상 `C:/Users/user/Documents/Projects/CocktailCoffeeTea`, Unity 6000.3.25f1(변경·재설치 없음).
입력: 기획 20(최신 지시), 05·06·07·08·09·10·11, 04 §5~§9, 최신 Delivery 보고(settings/save v2, coffee growth). AGENTS.md 없음.

**판정: 출시(release) 합격 아님.** 엄격한 `BuildRelease.Windows64`는 유지했고 여전히 빌드를 거부한다(아래 §5). 이 결과물은 기획 20의 "1차 통합 실행본"이며, 별도로 이름 붙인 프리뷰 빌드(버전 `1.0.0-firstpass`)다. 정상 난도 완주·키보드 실조작·PlayMode는 검증하지 않았다(§7).

## 1. 아트 반입·적용 상태 (source ready → Unity integrated → runtime 관찰)

통합기 `CCT/Art/Integrate First Pass Art`(`Scripts/Editor/FirstPassArtIntegrator.cs`, 입력 `Tools/Art/firstpass-sources.json`)가 manifest가 참조하는 PNG와 manifest만 바이트 그대로 복사하고 SHA-256을 `Delivery/logs/art-integration-firstpass.json`에 기록한다. 원본(ArtSource/기획 스테이징) 수정 없음.

| 구분 | 상태 | 경로 |
|---|---|---|
| status ready 71종 | `Assets/_Project/Art/v1/<id>/` (04 §9 임포트: 128PPU·Point·mip off·무압축·manifest 피벗·UI 9-slice) | 공용 42종(81프레임) + 배경 4(cafe_wall/floor/counter/lamp) + ice_shard·water_drop·rage_overlay + 장착 무기 4(weapon_rapier/gun/sword/bow) + **몸체 18종**(일반·엘리트 13 + tea_sommelier + 보스 4) |
| 사용자 1차 허용(출시 ready 아님) | `Assets/_Project/Art/firstpass/bartender/` + `firstpass-acceptance.json` | ArtSource/v1/bartender 23프레임 6클립(v5 idle 4/3fps·move 6/10fps 바이트 보존 + melee/ranged/hurt/defeat), status needs_fix, 프레임별 hand/grip/offHand anchor 92개. v3/v4 보간본은 통합기에서 금지 경로로 거부 |
| 런타임 목록 | `Assets/_Project/Resources/ArtV1/ArtCatalog.asset` | 클립·fps·loop·피벗·runtimeOpacity·tint·anchor·targetBodyHeightWorld·bodyHeightPx·알파 영역을 구운 ScriptableObject |

계약: 04 최신 **19종 / 기본 355 + 09 진상 손님 8 = 363프레임**으로 `ArtV1Spec` 갱신(옛 17종/321·329 폐기, espresso_bug R·milk_foamer T 추가, 테스트 갱신).

## 2. 런타임 소비 지점(코드)

- **플레이어** `Player/PlayerVisual.cs`: 물리 루트 스케일 1·판정 반지름 유지, Visual 자식만 반전·확대. 발 피벗(0.5,0.125)을 판정 원 바닥에, visualScale = H/(bodyHeightPx/128). idle/move 전환, 공격 시 melee/ranged, 피격 hurt(0.08초), 사망 defeat. 주무기는 장착용 무기 아트(grip anchor, 길이 = manifest 비율 × 플레이어 높이, 긴 변 기준 — 활은 세로)를 몸체 프레임별 hand anchor에, 하이브리드 보조 활(weapon_tea_bow)은 offHand anchor에. 총·활 탄은 muzzle, 하이브리드 화살은 arrow anchor에서 발사. 정렬 그룹(몸 0 / 무기 1 / 보조 활 −1), 발밑 ground_shadow(알파 0.22). 직업 선택 초상화 = 각 몸체 idle_00(04 §8).
- **적·보스** `Enemies/Enemy.cs`: 새 몸체가 있으면 발 피벗·H/(bodyHeightPx/128)(EnemyIconVisualScale 미적용), 이동/대기·좌우 반전, 피격 hurt, `PlayAction`(T/E action·S 소환·그라인더 charge_prepare/charge·드립 drip/vanish/appear·머신 steam/rain/pressure·진상 refund/summon/review/reject — 이벤트가 클립을 부를 뿐 판정 시간 불변), 사망 defeat는 판정 없는 `DefeatVisual`, 진상 손님 분노는 동일 클립 + rage_overlay. 없으면 기존 아이콘(단계적 전환). 그림자·boss_arrival·leaf_mote·hit_spark.
- **탄·장판·VFX**: bullet/arrow/tea_arrow(오른쪽이 앞), glass_shard·ice_shard·water_drop·review_star(`art:` 접두, 없으면 기존 아이콘). olive_fire/coffee_pool/sugar_pool 장판은 원형 판정이 타원 알파 영역 안에 들어가는 배율. warning_ring/warning_line/steam_jet(연결 타일)/pressure_wave(pressure·refund tint)/summon_ring(cocktail·tea tint)/seal_burst/melee_streak/player_melee_area(0.12)/player_fire/player_poison/pickup_glint/levelup_glint(시간정지 중 unscaled). 정확한 기존 판정 표시(원·반원·선)는 유지하고 그 위에 아트를 얹었다.
- **배경** `Core/CafeBackground.cs`: cafe_floor(반복) + cafe_wall(레인 위) + counter/lamp 장식, 카메라 스냅 24유닛(반복 단위 배수). 레인·충돌 불변.
- **UI** `UI/UiFactory.cs` Skin/AddIcon: 버튼 ui_button(12px 9-slice, 커피 트리는 14 메뉴판 팔레트 유지로 제외), 패널 ui_panel, 카드 ui_card_frame. 아이콘: 타이틀(specialty/ui_codex/ui_settings/ui_save_ok·error), HUD(status_slow·status_suppressed·seal_lock 상태, 보스 status_invulnerable, 상단 우측 일시정지 버튼 ui_pause — 08:76, 저장 실패 ui_save_error), 일시정지 ui_pause, 커피 트리·영구 강화 갈래 skill_*, 결과·영구 강화 specialty, 하이브리드 해금 hybrid_tea_martini.
- **정렬** `SortOrders`: 배경 −10 → 그림자 0 → 장판 1 → 개체 2(카메라 Y 깊이) → 탄 3 → 공중 효과 4 → HUD.

## 3. QA·캡처로 찾아 고친 실제 결함

- 실제 빌드 캡처: **타이틀 메뉴가 게임 제목을 덮음**(기존 배치 y=−30 위 고정) → 부제 아래로 이동 + 회귀 테스트. 공용 버튼 아트 때문에 **커피 트리 가능 노드 글자 판독 불가** → 커피 트리 제외. 도감 열 폭/간격(380>350으로 이웃 열 겹침) → 350/360, 하이브리드 설명 넘침 방향 수정, **도감 탭이 재료 행과 44px 겹침** → 이동. 티 세리머니 가디언 탄 placeholder → water_drop(04 §8 공유).
- QA 정적 지적 반영: 설정만 실패해도 조용히 종료 → 진행·설정 통합 확인, **OS 창 닫기(Application.wantsToQuit) 저장 확인창**(재시도/저장 없이 종료/취소, 기본 취소) 신설, 오디오 크로스페이드 중 마스터/BGM 0이 즉시 반영되지 않던 문제, 일시정지·설정 닫을 때 키보드 포커스 복원, 도감 항목 상세(클릭·Enter) + 키보드 이동 시 스크롤 따라가기, 08:122~124 **화면 흔들림(0이면 완전 비활성)·효과 강도(낮음=장식 파티클 50%)·피해 숫자(합산·최대 30·0.6초, 끄면 숫자만)** 실제 기능과 설정 행, anchor 키(파일명/경로) 둘 다 수용.

## 4. 테스트 (EditMode, 실제 실행 — XML/로그 `Delivery/logs/`)

Unity 6000.3.25f1 배치 EditMode(백그라운드 실행 후 XML·종료 코드 확인). PlayMode는 실행하지 않았다.

| 회차 | 결과 | 비고 |
|---|---|---|
| firstpass-r1 | 347/350, 실패 3 | 기존 EnemySprite 2건(새 몸체가 들어와 옛 "아이콘 사용" 기대가 바뀜 — 정당한 갱신), 내 흔들림 테스트의 SendMessage 오용 1건. 통과로 집계 안 함 |
| firstpass-r2~r5 | 350/350 → 351/351 | 테스트 수정·타이틀 레이아웃 회귀 테스트 추가, 통합 3·4회차 반영 |
| firstpass-r6 | 350/351, 실패 1 | 보스 몸체 반입 후 진상 손님 분노가 아이콘 교체 → rage_overlay로 바뀐 것(정당한 갱신). 통과로 집계 안 함 |
| firstpass-r7 | 351/351 | 보스 4종 포함 통합 5회차 |
| **firstpass-r8 (최종)** | **351/351 Passed, 실패·skip 0** (2026-10-04 08:07:11~18Z) | `Delivery/logs/editmode-results-2026-10-04-firstpass-r8.xml` / `editmode-log-2026-10-04-firstpass-r8.txt`, 최종 빌드 r4와 같은 코드 |

351 = 이전 329(설정 v2 보고 기준) + 신규/갱신. 신규 `Tests/EditMode/FirstPassArtTests.cs`: 합성 카탈로그로 PlayerVisual 발 피벗·H/bodyHeight·Visual만 반전·idle/move/melee/hurt/defeat·hand anchor·그림자 알파, 캡슐 대체, 적 몸체 배율·hurt/action, 장판 판정 원 포함 배율, 탄 방향/아이콘 대체, UI 9-slice·아이콘 선택성, 배경 스냅; **실제 구운 카탈로그**의 공용 42종 81프레임·피벗·opacity·tint·anchor·9-slice, 바텐더 1차 허용 표시와 v5 4/3·6/10, ready 몸체 측정 높이·04 클립 수; 흔들림 0 비활성·효과 낮음 50%·피해 숫자 합산/최대 30/끔; 종료 가드의 서비스별 미저장; 도감 상세·키보드 스크롤·탭 겹침; 타이틀 메뉴/제목 겹침. `ArtV1Spec` 19종/355/363, espresso_bug R세트. 정당하게 바꾼 기존 단언: EnemySpriteTests(새 몸체 우선, 레몬 브루트 placeholder→몸체, 진상 분노→rage_overlay), EnemyProjectileIconTests(ice_shard/water_drop v1 아트 우선·아이콘 대체), ArtV1Parsing/Validator(17→19종). UI 검사는 RectTransform 명목 좌표·합성 데이터 기반이며 실제 렌더를 대신하지 않는다.

## 5. 빌드·실행 증거

| 항목 | 값 |
|---|---|
| 엄격 출시 검사 `BuildRelease.Windows64` | **거부(정상)** — `Delivery/logs/strict-release-attempt-2026-10-04-r4.txt`: 남은 사유는 bartender 몸체가 1차 허용(needs_fix)이라 Art/v1에 없음 + 런타임 목록에 1차 임시 아트 포함. (r1 때는 보스 4종·tea_sommelier·rage_overlay 누락과 manifest 4건 오류도 있었고 아트 담당이 해소) |
| 1차 프리뷰 빌드 | `Builds/FirstPassPreview/CocktailCoffeeTea.exe`, 버전 **1.0.0-firstpass**(빌드 후 ProjectSettings 원래 값 복원), Mono·Windows x86_64·Development off, 상태 파일 `FIRST_PASS_PREVIEW_NOT_RELEASE.txt`(사본 `Delivery/logs/firstpass-preview-status.txt`), 로그 `Delivery/logs/firstpass-preview-build-2026-10-04-r4.txt` |
| ZIP | `Builds/CocktailCoffeeTea-FirstPassPreview-2026-10-04.zip`, 77,648,610 bytes, SHA-256 `C01FB2A5A983266246355A396F49D3A592388172DBE9E345AF330AB2907DBBD9`. Unity 플레이어 exe 스텁 해시는 빌드마다 같다(`3070E580…D653`) — 빌드 식별은 ZIP 해시. ZIP에 Unity가 만든 `*_BurstDebugInformation_DoNotShip` 폴더가 포함됨(실행 무관, 배포 전 제외 필요) |
| 새 폴더 실행 | ZIP을 `C:\UnityScratch\fp-run-r4\game`에 풀어 `CocktailCoffeeTea.exe -cct-data-dir C:\UnityScratch\fp-run-r4\data -cct-autotour <캡처 폴더>` 실행, 종료 코드 0, 자체 프로세스만 대기/정리 |
| 캡처 39장 + 로그 | `Delivery/screens/first-pass-2026-10-04-r4/`(1920×1080): 타이틀, 도감·상세, 설정(2열·흔들림/효과/피해 숫자), 영구 강화, 직업/무기 선택(idle_00 초상화), 첫 안내, 두 직업·네 무기 걷기·전투, 일반/엘리트 13종 라인업, 카드 선택, 커피 트리, 하이브리드 해금, 화염/독, 보스 4종 등장·관찰 2장씩, 일시정지, 결과 4종, 런 후 타이틀. `tour-log.txt`(오류 0·예외 0), `player.log` |
| 저장 확인 | 격리 폴더 save.json: totalRuns=4, failed=1, abandoned=3(포기 3·사망 1), cards=5, varieties=14, activeRun=null, saveFailed=False; settings.json 생성(첫 안내 확인 기록) |
| 이전 회차 | r1(결함 발견 회차) `…-r1/`, r2 `…-r2/`, r3 `…-r3/`(보스 관찰 중 자연 레벨업으로 화면 정지 → 투어 수정) 보존 |
| 아트 상태표 | `Delivery/logs/firstpass-asset-table-r4.md`(72종: ID·종류·상태·프레임·소비 지점·투어 중 조회 여부) |

투어 중 조회되지 않은(=실행 화면에서 관찰하지 못한) 소비 지점 8개: coffee_pool, pressure_wave, rage_overlay(진상 50% 미만), review_star, seal_burst, steam_jet, warning_line, warning_ring — 코드 연결과 일부 EditMode 검사(장판 배율 등)만 있고 실제 실행 화면 확인 없음. 보스 패턴 VFX는 관찰 창에 패턴이 걸리지 않았다.

## 6. 저장 격리

빌드 검증 실행은 `-cct-data-dir <폴더>`로 save.json/settings.json을 그 폴더에서만 읽고 썼다(빈 메모리 구형 PlayerPrefs 저장소). player.log의 `[Save]`/`[Settings]` 경로로 확인. 사용자 실제 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea\`는 건드리지 않았다. `-cct-autotour`는 `-cct-data-dir` 없이 실행되지 않는다.

**GUI QA 인계 명령(사람/QA가 직접 조작할 때, 실제 진행 보존):**
```
<압축 해제 폴더>\CocktailCoffeeTea.exe -cct-data-dir C:\UnityScratch\qa-data
```

## 7. 미검증 / 남은 범위 (정직한 구분)

- **강제 경로 투어**: AutoTour는 실제 버튼 onClick·실제 게이지/보상/일시정지/결과/재시작 경로를 쓰지만 보스 즉시 소환, AddIngredient 보상 유도, 하이브리드 예약, 화염/독 레벨 직접 부여, 보스 관찰 중 회복, MarkPlayerDiedForTests/GiveUp 종료, AutoInputOverride 이동을 쓴다. **정상 난도 20분 완주·클리어 경로·실제 키보드/마우스 입력·조작감·밸런스는 미검증.** 클리어(최종 보스 처치) 결과 화면은 캡처하지 않았다.
- **PlayMode 미실행**(이 기기 배치 PlayMode 러너 행 기록). 기존 PlayMode 테스트는 컴파일만.
- 오디오는 빌드에 포함·재생 경로 연결만 확인(청취 검증 없음). 1280×720 렌더·다른 해상도·창 모드 캡처 없음(1920×1080 테두리 없는 전체 화면만).
- 아트 품질 판정은 아트 담당/QA 몫: 바텐더 걷기 보정 보류(사용자 지시), 바텐더 status needs_fix → 출시 불가. bodyHeightPx는 각 manifest 값(바텐더만 186 측정값 사용).
- 진상 손님 manifest의 review 발사 지점(reviewOrigins) 갱신은 소비하지 않는다 — 별점 탄은 보스 위치에서 발사. 적 action 프레임의 emitter/summon anchor도 탄 발사 위치에 쓰지 않는다(시각 정렬 후속).
- 도감 카드 행 디자인·하이라이트, 카드 선택 화면 색 대비 등은 육안 1회 검토만. 보스 패턴 시각 타이밍·판정 일치는 캡처 기준 확인이며 프레임 단위 검증 아님.
- 커밋·푸시 없음. Steam/외부 업로드 없음.
