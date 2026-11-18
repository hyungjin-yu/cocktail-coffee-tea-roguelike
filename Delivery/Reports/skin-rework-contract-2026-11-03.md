# 스킨 재제작 통합 계약 · 독립 시각 QA 기준 — I0 (Claude, 2026-11-03)

Orca Run `run_8e6acedf2bc1` · Task `task_2c97707efffc` · Dispatch `ctx_205cd2098d13`.
대상: `skin_bartender_rail`(황동 야간열차), `skin_tea_ceremony`(백자 다례가). 아트(A1/A2)는 Codex가 Aseprite·PixelLab GUI로 전담한다.

> **이 문서의 상태:** I0 계약·기준선 문서다. Assets/C#/.meta/ProjectSettings/ArtSource는 바꾸지 않았다. Unity 테스트·빌드·게임 실행은 하지 않았다.
> 아래 수치는 모두 이번 세션에서 실제 코드·PNG·`.meta`·ArtCatalog를 직접 읽어 얻은 값이다(근거: §10). 과거 문서의 23/28/42는 대조만 했고, 그대로 복제하지 않았다.

---

## 0. 아트 작업자에게 — 한 화면 요약

| 항목 | 계약 (변경 금지) |
|---|---|
| 납품 위치(아트) | `ArtSource/skins-rework-2026-11-03/<skinId>/runtime/<런타임 이름>.png` 권장(+ native `.aseprite`, manifest). Assets에는 쓰지 않는다. |
| 런타임 위치(통합 시 Claude가 덮어씀) | `Assets/_Project/Resources/CosmeticSkins/<skinId>/<런타임 이름>.png` — **같은 경로 PNG 내용만 교체**, `.meta`(GUID·피벗·PPU) 유지 |
| 파일 이름 | 기본 몸체 스프라이트 이름과 **정확히 동일**(`bartender_idle_right_00` 등, `_right_` 포함). 이름이 다르면 스킨 전체가 꺼진다(§3 F1) |
| 프레임 수(실사용) | rail **23** = idle4·move6·melee4·ranged4·hurt1·defeat4 / ceremony **28** = 같은 23 + `tea_sommelier_sword_swing_right_03..07` 5장. 검 00–02·`bow_*`는 미재생이라 불필요 |
| 캔버스 | 256×256 RGBA, sRGB, 투명 배경, 프레임별 자동 잘라내기 금지 |
| PPU / 필터 | 128 / Point(filterMode 0), 압축 기본 None(플랫폼 override는 기존 meta 유지) |
| 피벗 | 몸체 23장: (0.5, 0.125) = 픽셀 (128, 32), 발 접지선 y=32 · 검 휘두르기 03–07: **(0.328125, 0.1875) = (84, 48)** — 기존 휘두르기 원본 그대로, 몸체 위치를 옮기지 말 것 |
| 손 앵커 | 런타임은 **기본 캐릭터 ArtCatalog 앵커**를 쓴다(스킨 별도 앵커 없음). 각 프레임 hand 좌표에 주먹/손이 그대로 있어야 무기가 손에 붙는다(§2 표) |
| 범위 | 본체 x 16..239, 최고점 y ≤ 224(모자 포함). 상점 미리보기는 `idle_right_00`을 x66..178 · y10..224로 잘라 보여 준다 |
| 좌향 | 별도 left 프레임 없음 — Visual 배율 x=−1 전체 거울. 글자·숫자 금지, 비대칭 장식(시계 사슬·매듭)은 거울돼도 되는 형태로 |
| 무기 | 몸체에 무기 그리지 않음. 예외: 검 휘두르기 03–07은 **기본 검(분홍 수박 날) 픽셀을 원본과 같은 위치·같은 색으로 보존**(현재 659~1285px 일치) |
| 광원/명암 | 왼쪽 위 광원, 재질당 기본/그림자/하이라이트 3단계, 굵은 어두운 외곽선(04 §스타일) |
| 품질 | §6 시각 QA 기준. 파일 수·크기·피벗 일치는 **기술 합격일 뿐 미적 합격이 아니다** |

---

## 1. 런타임 경로 (코드 근거)

1. **몸체 생성** — `PlayerVisual.Build` (`Assets/_Project/Scripts/Player/PlayerVisual.cs:68-99`): `ArtLibrary.Get("bartender"|"tea_sommelier")`의 클립(idle/move/melee/ranged/hurt/defeat)을 `SpriteAnimator`로 재생. Visual 위치 `(0,-radius)`, 배율 `BodyVisualScale = 1.6 / (bodyHeightPx/128)`(바텐더 bodyHeightPx 186, 티 192).
2. **스킨 장착** — `GameManager.cs:257` `SkinSpriteSwapper.AttachIfEquipped` → `SkinCommerce.ResolveForRun(job, SkinArt.Exists)` → `SkinCatalog.ResolveEquipped`(`SkinCatalog.cs:107-117`: 직업 일치 + 현금 소유(commerce.json) 또는 코인 소유(`DrinkLab.CoinOwns`) + 아트 완전).
3. **아트 완전성** — `SkinArt.IsComplete`(`SkinCatalog.cs:141`) = `MissingFrames` 0. 필수 이름 = 몸체 카탈로그 전 클립 프레임 + (티 소믈리에만) `PlayerVisual.SwordSwingFrames()`(03–07). **하나라도 없으면 스킨 전체 미적용 + 상점 판매 중지**(`DrinkSales.OnSale` → `SkinArtReady`).
4. **교체 방식** — `SkinSpriteSwapper`(`SkinSpriteSwapper.cs`, `[DefaultExecutionOrder(5000)]` LateUpdate): 현재 `body.sprite.name`과 **같은 이름**의 스킨 스프라이트로 치환. 애니메이션·피벗·앵커는 원본 그대로 따른다 → 스킨 PNG가 원본과 다른 포즈/위치면 무기·그림자와 어긋난다.
5. **손 무기** — `PlayerVisual.UpdateWeaponAnchor`(`:328-354`): 현재 클립·프레임의 **기본 몸체 앵커** `hand`(없으면 `grip`, 없으면 임시 (150,112))에 WeaponPivot을 두고 `HeldWeaponAngle`(`:359-378`)로 회전. 무기 sortingOrder 1(몸 앞); **활은 비겨냥 자세에서 −1(몸 뒤)** — 몸체 그림의 주먹이 손잡이를 덮는 구조.
6. **검 휘두르기** — `PlayerVisual.PlaySwordSwing`(`:412-422`): 티 소믈리에 + `weapon_sword`일 때만 `Resources/ArtExtra/tea_sommelier_sword_swing` 03–07을 30fps 재생, 손 무기 렌더러 숨김(검은 몸체 그림 안).
7. **검 외형 스킨** — `WeaponSkinApplier`(`WeaponSkins.cs:258-364`, order 5001): 비취(jade)·스킨 없음은 `swing_base/swing_jade` 전체 프레임 교체. **신규 몸체 스킨**은 몸체 프레임을 건드리지 않고 `WeaponSkins/weaponskin_sword_01/swing_overlay/` 무기-only 프레임을 `SwordSwingWeaponOverlay`(sortingOrder body+2)로 겹친다.
8. **좌우** — `PlayerVisual.Tick/SyncToCurrentFrame`: Visual `localScale.x` 부호만 바꿈(몸·무기·오버레이 함께 거울).
9. **피격 / 쓰러짐** — 피격: `body.color = (1,0.4,0.4)` 점멸(스프라이트는 `hurt_right_00`, 최소 0.08초). 쓰러짐: `UseUnscaledTime`으로 timeScale 0에서도 재생, 마지막 프레임 유지.

## 2. 프레임별 계약 (실측)

모든 51장: 256×256, PPU 128, filterMode 0, spriteMeshType 0, 압축 `0/1/1/1`(기본/플랫폼 override), 현재 스킨 meta = 기본 meta와 동일(51/51). 좌표는 **좌하단 원점 픽셀**. hand는 grip·rightHand와 같은 값이다.

#### skin_bartender_rail (bartender) — 23장

| # | runtime name (.png) | hand | offHand | 현재 알파 bbox x0,y0–x1,y1 (기본) | 실루엣 IoU |
|---|---|---|---|---|---|
| 1 | `bartender_defeat_right_00` | (145,78) | (112,69) | (72,32,183,189) (=) | 0.989 |
| 2 | `bartender_defeat_right_01` | (156,39) | (123,42) | (56,32,200,155) (…199,155) | 0.985 |
| 3 | `bartender_defeat_right_02` | (176,41) | (121,42) | (38,32,217,120) (…217,**111**) | 0.929 |
| 4 | `bartender_defeat_right_03` | (186,49) | (169,48) | (26,32,**239**,109) (…229,**95**) | 0.904 |
| 5 | `bartender_hurt_right_00` | (161,82) | (121,111) | (76,32,177,199) (=) | 0.973 |
| 6–9 | `bartender_idle_right_00..03` | (164,87)(164,87)(164,89)(164,89) | (105,87/87/89/89) | (80,32,175,217/219/219/217) (=) | 0.971–0.972 |
| 10 | `bartender_melee_right_00` | (171,133) | (111,131) | (72,32,181,220) (…217) | 0.951 |
| 11 | `bartender_melee_right_01` | (161,148) | (79,185) | (62,32,191,221) (…217) | 0.941 |
| 12 | `bartender_melee_right_02` | (200,140) | (103,123) | (28,32,225,211) (…207) | 0.932 |
| 13 | `bartender_melee_right_03` | (169,134) | (108,129) | (74,32,179,220) (…217) | 0.953 |
| 14–19 | `bartender_move_right_00..05` | (160,87)(162,85)(164,89)(166,87)(166,85)(164,89) | (109,87)(107,85)(105,89)(101,87)(103,85)(105,89) | 기본과 동일 | 0.969–0.972 |
| 20 | `bartender_ranged_right_00` | (161,133) | (112,132) | (78,32,175,218) | 0.969 |
| 21 | `bartender_ranged_right_01` | (180,152) | (107,141) | (58,32,197,218) | 0.888 |
| 22 | `bartender_ranged_right_02` | (160,121) | (90,141) | (64,32,189,217) | 0.971 |
| 23 | `bartender_ranged_right_03` | (165,88) | (99,89) | (78,32,177,224) (…223) | 0.978 |

#### skin_tea_ceremony (tea_sommelier) — 28장

| # | runtime name (.png) | hand | offHand | 기타 | 현재 bbox (기본과 동일 여부) | IoU |
|---|---|---|---|---|---|---|
| 1 | `tea_sommelier_defeat_right_00` | (152,61) | (115,47) | – | (62,32,193,171) = | 0.980 |
| 2 | `tea_sommelier_defeat_right_01` | (139,70) | (64,40) | – | (32,32,221,157) = | 0.895 |
| 3 | `tea_sommelier_defeat_right_02` | (86,47) | (65,42) | – | (28,32,225,103) = | 0.868 |
| 4 | `tea_sommelier_defeat_right_03` | (76,45) | (50,42) | – | (20,32,233,95) = | 0.826 |
| 5 | `tea_sommelier_hurt_right_00` | (138,81) | (100,94) | – | (68,32,185,187) = | 0.937 |
| 6–9 | `tea_sommelier_idle_right_00..03` | (162,88)(162,88)(162,90)(162,90) | (98,90/90/92/92) | – | (80,32,175,223/225/225/223) = | 0.952–0.956 |
| 10 | `tea_sommelier_melee_right_00` | (167,125) | (101,124) | – | = | 0.932 |
| 11 | `tea_sommelier_melee_right_01` | (191,163) | (91,136) | – | = | 0.907 |
| 12 | `tea_sommelier_melee_right_02` | (208,148) | (76,124) | – | = | 0.902 |
| 13 | `tea_sommelier_melee_right_03` | (160,119) | (104,117) | – | = | 0.928 |
| 14–19 | `tea_sommelier_move_right_00..05` | (166,88)(164,86)(160,90)(158,88)(160,86)(164,90) | (94,90)(96,88)(100,92)(102,90)(100,88)(96,92) | – | = | 0.934–0.949 |
| 20 | `tea_sommelier_ranged_right_00` | (181,148) | (88,142) | bowString (88,142) | = | 0.920 |
| 21 | `tea_sommelier_ranged_right_01` | (198,153) | (83,159) | bowString (83,159) | = | 0.913 |
| 22 | `tea_sommelier_ranged_right_02` | (201,154) | (53,144) | bowString (53,144) | = | 0.907 |
| 23 | `tea_sommelier_ranged_right_03` | (173,101) | (82,100) | bowString (82,100) | = | 0.911 |
| 24–28 | `tea_sommelier_sword_swing_right_03..07` | 없음(검이 그림 안) | – | **피벗 (84,48)** | (24,16,161,229)(28,16,165,237)(30,16,221,205)(36,16,231,207)(36,16,226,207) = | 0.926–0.951 |

전체 행 단위 원본: `Delivery/Verification/2026-11-03/skin-rework-baseline/frame-anchor-table.md`, `frames.csv`, `frames.json`.

**몸체/무기 경계 규칙**
- 위 hand 좌표 주변(7×7)에 현재 49px 불투명(ceremony ranged_00만 35) — 신규 아트도 hand 좌표에 **주먹/손 픽셀**이 있어야 한다. 넓은 소매가 주먹을 덮거나 손을 다른 위치로 옮기면 무기가 허공·소매 위에 뜬다.
- 활(비겨냥, 몸 뒤 −1)은 몸체 주먹이 손잡이를 덮어야 "쥔" 것으로 보인다. 넓은 랩 로브 소매가 손잡이~활대를 통째로 가리면 활이 사라져 보인다 → ceremony는 활 장착 idle/move를 따로 확인.
- 겨냥 ranged 00–02는 활이 몸 앞(+1), 시위 쪽 손 = offHand/bowString. 소매·허리띠 끝이 bowString 좌표를 가리지 않게.
- 검 휘두르기 03–07: 검 픽셀(현재 overlay 마스크 영역 659/1008/1285/1271/1280px)은 **기본 휘두르기와 픽셀 동일** 유지. 소매를 검 위로 덮지 않는다(검 외형 오버레이가 몸 위에 그려지므로 덮으면 검이 소매를 뚫고 보임).
- 높은 모자·옷자락은 x 16..239, y ≤224 안. rail defeat_03 현재 x 239(경계값) — 신규 아트에서 넘지 말 것.

## 3. 옷판 고정 · 잘림 · 기본복장 flash 경로

| ID | 경로 | 증상 | 현재 상태 / 원인 | 계약·검사 |
|---|---|---|---|---|
| F1 | 필수 프레임 이름 누락·오타(`_right_` 빠짐, `bartender_idle_00` 같은 native 이름 그대로) | 스킨 **전체**가 기본 복장으로(부분 flash는 구조상 없음) + 상점 "아트 준비 중", 판매 중지 | 현재 51/51 존재 | 통합 시 `CoinSkinTests.CoinSkinArt_EitherAbsentOrCompleteAndAligned` + 이름 대조표 |
| F2 | 캔버스·피벗 다른 PNG | 발이 뜨거나 무기가 손에서 떨어짐 | 현재 일치 | PNG만 교체·meta 유지, 크기 256² 강제 |
| F3 | 포즈를 바꿔 그림(손 위치 이동) | 무기 부착 어긋남 | 현재 IoU 0.83–0.99(기본 포즈 유지) | hand 좌표 픽셀 검사 + 무기별 캡처 |
| F4 | **고정 옷판** | 머리·팔다리만 움직이고 몸통 옷이 정지 | **현재 결함(실측)**: move 00–05 6장 동안 완전히 같은 픽셀 rail 2580/10756(24%), ceremony 3450/12814(27%) — 기본은 16, 32px. 몸통 띠(y60–150) 프레임 간 변화량이 기본의 40–60%(ceremony move 2817 vs 6672) | §6 V3 / 정적 픽셀 지표 |
| F5 | 평면 단색 면 | 큰 사각 앞판, 명암 없음 | **현재 결함**: 3×3 동일색 비율 rail 0.16–0.28, ceremony 0.20–0.36 vs 기본 0.00–0.07; 사용 색 수 기본의 약 45–70% | §6 V2 |
| F6 | 쓰러짐/피격 회전 시 옷판이 몸과 따로 회전·잘림 | ceremony defeat_01–03의 직사각 판이 몸 위에 걸치고, hurt에서 판 아래가 잘림; rail defeat_02/03은 bbox 위로 9~14px 솟음 | 현재 결함(비교판) | 대표 쓰러짐 2장 + 피격 1장 필수 비교 |
| F7 | 검 외형 + 신규 몸체 스킨 | 오버레이가 기본 검 날을 다 덮지 못해 **분홍 날이 파란 날 가장자리로 비침** | **기존 결함(이번 아트와 별개)**: swing 04·05·07에서 오버레이 밖 기본 날 픽셀 132/91/139 — `ceremony_swing_overlay_composite.png` | 코디네이터 판단 필요(§9 R2). 몸체 재작화 범위 아님 |
| F8 | 피격 점멸 | 붉은 tint(1,0.4,0.4)가 상아색 로브를 분홍으로 | 정상 동작(곱연산) — 기본복장 flash 아님 | 캡처에서 오판 금지 |
| F9 | 상점 미리보기 잘림 | `idle_right_00`의 x<66, x>178, y>224 부분이 잘림 | 현재 rail/ceremony idle 범위 80..175 × 32..217/223 | idle_00 bbox 검사 |
| F10 | 좌향 | 비대칭 장식이 반대쪽으로, 광원 오른쪽 위로 | 구조상 정상(거울 재생) | 좌향 캡처, 글자 금지 |

## 4. 아트 → Unity 인계 형식 (A2 ready 조건)

- `ArtSource/skins-rework-2026-11-03/manifest.json`(또는 스킨별) 각 항목: `skinId`, `runtimeName`, `sourceNative`(.aseprite + 레이어/태그), `outputPng`, `outputSHA256`, `sizePx [256,256]`, `pivotPx`(몸체 128,32 / swing 84,48), `handPx`(위 표 값을 그대로 기록 — 앵커는 바꾸지 않는다는 확인용), `targetResourcesPath = CosmeticSkins/<skinId>/<runtimeName>`, `pixelLabCall`(입력·프롬프트·seed·출력 해시) 또는 `aseprite_manual`, `status: ready|needs_fix`.
- 스킨별 실사용 프레임 전량(rail 23 / ceremony 28). 일부만 ready면 **통합하지 않는다**(F1: 섞이면 안 되고 일부 적용은 구조상 불가).
- 비교판: 기본 / 현재(10-20) / 신규를 같은 순서·같은 배율(1×, nearest 4×)로, 대표 idle·move×2·hurt·melee(또는 swing)·defeat×2. 64px 실제 크기판 별도.
- 동결 후 SHA 변경 금지. 재수정 시 새 manifest 버전.

## 5. 보존 항목 (통합 단계에서 바뀌면 FAIL)

| 항목 | 현재 값 / 위치 |
|---|---|
| 스킨 수·ID | 12종 = 현금 2(`skin_bartender_midnight`, `skin_tea_jade`) + 코인 10(`SkinCatalog.All`) |
| 현금 상품 | `PaidProductIds` = 2종만, 코인 스킨은 스토어 SKU 아님 |
| 가격 | `CoinSkinPrice = 1000` 판매 코인 |
| 이름·설명 | `displayName`/`difference`(예: 황동 야간열차 "각진 차장모·짧은 더블브레스트 재킷·시계 사슬·황동 단추, 짙은 적갈색") — 그림이 바뀌어도 테마 유지, 문구 변경은 코디네이터 승인 |
| 능력치·판정 | 스킨은 스프라이트만; 반지름·속도·HP·공격 범위 불변 |
| 저장 | `commerce.json`(현금 소유·장착), DrinkLab 데이터 `coinOwned`/`coins`, `weapon_skins.json`(무기 외형) — 스키마·키 불변, 실제 사용자 저장 미접촉(격리 저장만) |
| 무기·무기 위치 | `HeldWeaponAngle`, 앵커, 길이 비율, 활 방향 규칙, 무기 외형 4종(`weaponskin_*_01`) |
| GUID | 51개 `.meta` 그대로(`runtime-sha256-before.txt`에 meta 해시 포함) |
| 기타 아트 | 기본 몸체(`Assets/_Project/Art/v1/*`), midnight/jade, 나머지 8종 코인 스킨, `ArtSource/skins-aseprite-pixellab-2026-10-20/` 원본 — 수정·삭제 금지 |
| 기존 빌드·ZIP | `Builds/CocktailCoffeeTea-DrinkLab-v1.1-skins10-2026-10-20-win64.zip` 등 교체 금지, itch.io·공개 ZIP 미교체 |

## 6. 독립 시각 QA 기준 (원본 대비)

판정은 **기본 캐릭터 같은 프레임과 나란히** 본다(1× 실제 게임 크기 + nearest 4×). 아래 수치는 결함을 찾는 **보조 신호**이며, 수치 통과만으로 PASS를 주지 않는다. 수치 측정 스크립트는 기준선과 같은 방식(§10)으로 재실행한다.

| ID | 기준 | 불합격 예 | 보조 신호(기준선 → 목표 방향) |
|---|---|---|---|
| V1 | 3/4 측면·3등신·외곽선·픽셀 밀도·왼쪽 위 광원이 기본과 같다 | 매끈한 벡터풍, 외곽선 없는 면, 광원 반대 | – |
| V2 | **입체감**: 옷에 기본/그림자/하이라이트 3단계, 접힘·겹침·재질이 몸통과 팔다리 부피를 따른다 | 큰 단색 사각 앞판, 직선 대각 X 무늬로 면 채우기 | 3×3 동일색 비율: 기본 ≤0.07, 현재 0.16–0.36 → 기본에 근접해야 함(예: ≤0.12 근방이면 재검, 수치만으로 판정 금지) |
| V3 | **포즈 추종**: 이동·공격·피격·쓰러짐마다 소매·옷자락·허리띠의 방향·주름·가림이 달라진다 | 머리/피부만 움직이고 옷판이 정지; 쓰러짐에서 판이 그대로 회전 | move 6장 동일 픽셀: 기본 16/32 vs 현재 2580/3450 → 기본 수준(수십 px)으로. 몸통 띠 프레임 변화량 기본 대비 ≥ 약 80% |
| V4 | 테마가 실루엣으로 읽힌다(차장모·더블브레스트·시계 사슬 / 낮은 머리 묶음·넓은 소매 랩 로브·긴 허리띠·청화 점) | 색만 바뀐 기본 조끼, 테마 소품이 1px 점 | 64px 축소판에서 이름 없이 구별 |
| V5 | 묘사 밀도가 기본보다 크게 떨어지지 않음 | 얼굴·머리는 정밀, 옷만 단순 | 사용 색 수 기본 대비(현재 45–70%) |
| V6 | 손·무기: 4무기(레이피어·검·총·활)를 들었을 때 주먹이 손잡이에 붙고 소매가 무기를 먹지 않는다 | 활이 소매 뒤로 사라짐, 검 손잡이 위로 소매 | hand 7×7 불투명 픽셀 유지 |
| V7 | 잘림·경계: 프레임 내부, 모자 y≤224, x16..239, 발 y=32 | 모자 위 잘림, 쓰러짐 옷자락 캔버스 밖 | bbox |
| V8 | 프레임 간 연속성: 장식 개수·단추 위치·청화 무늬 수가 프레임마다 튀지 않는다 | 단추 수 변동, 무늬가 순간이동 | 비교판 |
| V9 | 좌향 거울에서 어색한 비대칭·글자 없음 | 글자/숫자, 거울 시 의미 깨짐 | 좌향 캡처 |
| V10 | 검 휘두르기: 기본 검 픽셀 보존, 검 외형 오버레이와 겹칠 때 소매·로브가 검을 덮지 않음 | 날 위로 소매 | overlay 마스크 내부 픽셀 = 기본 |

판정 단위: 스킨마다 V1–V10 각각 PASS/FAIL/NOT RUN + 증거 경로. **V2·V3·V4는 코디네이터 실물 확인 없이는 PASS로 확정하지 않는다.** 현재(10-20) 아트 기준선 판정: rail V2 FAIL · V3 FAIL · V6 기술적 OK / ceremony V2 FAIL · V3 FAIL · F6 FAIL (근거: `static_move_*.png`, `compare_*.png`).

## 7. 후속 통합·검수 계획 (A2 승인 뒤, 이번 I0에서는 실행 안 함)

**전제:** 코디네이터가 A2 실물 승인, manifest `ready`, Unity Editor가 이 프로젝트를 열고 있지 않음(`Temp/UnityLockfile` 없음), 사용자 Unity/Aseprite 창 미종료. 메모리 부족 이력 → **검사·빌드는 한 번에 하나**, 각 단계 전에 사용 가능 메모리 기록(현재 2.2 GB 여유 / 16 GB, §8).

1. **import**: manifest SHA 대조 → 승인된 스킨 폴더 PNG만 같은 경로에 복사 → 복사본 SHA 재대조 → `.meta` diff 0 확인(GUID·피벗·PPU 불변). 나머지 8종·midnight·jade·기본 아트 해시 불변 확인(`runtime-sha256-before.txt`와 비교).
2. **EditMode(하나씩)**: `CCT.Tests.CoinSkinTests` → `WeaponSkinTests` → `CommerceTests` → `DrinkSalesTests`(코인 구매). 예: `"C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -projectPath <proj> -runTests -testPlatform EditMode -testFilter CCT.Tests.CoinSkinTests -testResults Delivery/Verification/<date>/skin-rework-integrate/tests/editmode-coinskin.xml -logFile ...`(`-quit` 미사용).
3. **PlayMode(하나씩)**: `SwordOverlayPlayTests`, `SkinShopListTests`, `SkinShopViewTests`, `CardAdviceAndWeaponSkinPlayTests`, 그다음 증거 전용 `Skins10EvidenceCapture`(`CCT_EVIDENCE_DIR` 지정, 격리 저장).
4. **신규 시각 검사(통합 Task에서 추가 제안)**: 기존 검사는 크기·PPU·피벗만 본다. 정적 옷판(F4)·hand 주먹 픽셀(V6)·검 픽셀 보존(V10)을 이번 측정 스크립트 방식으로 보조 리포트화(실패 시 차단이 아니라 보고).
5. **별도 Windows 검수본**: `Builds/Playable-2026-11-03-skin-rework/`(새 폴더), 기존 빌드·ZIP 덮어쓰기 금지. 새 폴더 실행, 격리 저장.
6. **실제 게임 캡처** (`Delivery/Verification/2026-11-03/skin-rework-integrate/`):
   - 상점: 1920×1080, 1280×720, 836×705 — 스킨 목록·상세 비교(기본 vs 스킨)·코인 1000 표시·구매·장착·다른 직업 제한.
   - 전투(각 스킨): 바텐더 rail × 레이피어·총, ceremony × 검·활(+ 검 외형 장착 1회) — 좌/우 idle, 이동, 피격 점멸, 근접/원거리 공격, 검 휘두르기 03–07, 쓰러짐 마지막 프레임.
   - 원본 비교: 같은 장면을 기본 캐릭터로 1회씩.
   - 저장: 구매 → 재시작 → 소유·장착 유지, 중복 구매 거부, 기존 유료 2종 복원 경로 회귀.
7. QA 보고서 `Delivery/Reports/qa-skin-rework-<date>.md`: 빌드/컴파일, 기능, 시각을 분리, 각 행 PASS/FAIL/BLOCKED/NOT RUN. 게임 창 캡처가 안 되면 런타임 시각 BLOCKED.

## 8. 기준선 환경 (2026-11-03 I0 시점)

- Git: 브랜치 `codex/coffee-brewing`, HEAD `git-head.txt`, porcelain 391줄(`git-status-porcelain.txt`), tracked diff 17 files +869/−345(`git-diff-stat.txt`). 이번 I0는 이 상태를 **변경하지 않았다**(새 파일은 이 보고서와 baseline 폴더만).
- `ArtSource/skins-rework-2026-11-03/`: 아직 없음(A1 산출물 미도착).
- Unity: 이 프로젝트 `Temp/` 폴더 없음 → Editor 잠금 없음. 실행 중 `unity.exe` 3개는 `C:\Users\user\AppData\Local\Unity\bin\unity.exe` **`unity mcp`**(각 37–46 MB) — Editor 아님. 종료하지 않았다. 설치 Editor 6000.3.25f1(프로젝트 버전 일치).
- Aseprite: PID 13088 실행 중(`ArtSource/skins-aseprite-pixellab-2026-10-20/skin_bartender_rail/bartender_idle_00.aseprite` 열림, 아트 작업자 소유) — 조작·종료하지 않았다.
- 메모리: 여유 2,251 MB / 전체 16,223 MB → 통합 단계에서 Unity batchmode + 다른 앱 동시 실행 시 위험. 검사 하나씩, 빌드 전 여유 메모리 재확인.

## 9. 미해결 위험

| ID | 위험 | 제안 |
|---|---|---|
| R1 | 현재 rail/ceremony를 포함한 10종 모두 같은 "고정 옷판" 제작 방식일 가능성(대표 2종에서 실측) | A3에서 8종 각각 F4/V3 지표 측정 |
| R2 | 검 외형 오버레이가 기본 날을 다 덮지 못함(F7, 04·05·07 분홍 비침) — 신규 몸체 스킨 전체 공통, 이번 몸체 재작화로 해결되지 않음 | 코디네이터 결정: overlay 재export(아트) 범위 포함 여부 |
| R3 | 넓은 소매(ceremony)가 비겨냥 활(몸 뒤)·검 손잡이를 가릴 수 있음 | A2 비교판에 활 장착 idle/move 합성 포함 요청 |
| R4 | 앵커는 기본 카탈로그 고정 — 아트가 포즈를 "개선"해 손을 옮기면 무기 어긋남 | hand 좌표 유지 확인을 manifest·비교판에 |
| R5 | 기존 자동 검사는 기하만 검사 → 품질 회귀를 못 잡음 | §7-4 보조 리포트 + 코디네이터 실물 승인 |
| R6 | 메모리 여유 2.2 GB, 과거 메모리 부족 | 순차 실행, Unity 동시 실행 금지 |
| R7 | 작업 폴더 dirty 391줄(음료 시스템 등 다른 작업) — 통합 빌드에 섞임 | 통합 보고서에 기준선 대비 변경 경로만 명시, 다른 변경 미수정 |
| R8 | ceremony swing 프레임은 피벗(84,48)·발 y16로 몸체 23장과 기준이 다름 | 아트가 23장 기준으로 휘두르기 몸을 옮기지 않도록 §0 명시 |

## 10. 근거 · 증거 파일

source inspection(코드·PNG·meta·ArtCatalog 직접 읽기)이며 런타임 실행 증거가 아니다.

| 파일 (`Delivery/Verification/2026-11-03/skin-rework-baseline/`) | 내용 |
|---|---|
| `frames.csv`, `frames.json` | 51장 크기·모드·meta(PPU/피벗/필터/압축/GUID)·기본 대비·bbox·IoU·색 수·평면 비율·hand 픽셀·overlay 일치, 몸통 띠 프레임 변화량 |
| `frame-anchor-table.md` | ArtCatalog에서 생성한 프레임별 앵커 표 |
| `compare_skin_bartender_rail.png`, `compare_skin_tea_ceremony.png` | 기본/현재 스킨 전 프레임 2× nearest 비교 |
| `static_move_skin_bartender_rail.png`, `static_move_skin_tea_ceremony.png` | move 00–05 동안 변하지 않는 픽셀(빨강) — 고정 옷판 시각화 |
| `ceremony_swing_overlay_composite.png` | ceremony 휘두르기 + 푸른 강철 검 오버레이 합성(F7) |
| `runtime-sha256-before.txt` | rail/ceremony PNG·meta + sword swing_overlay 112개 SHA256(통합 전 기준) |
| `git-head.txt`, `git-status-porcelain.txt`, `git-diff-stat.txt` | 작업 폴더 기준선 |
| `measure.py`, `sheet.py` | 측정·비교판 스크립트 사본(재현용) |

읽은 입력: 프로젝트 `CLAUDE.md`, `Delivery/Reports/qa-protocol.md`, `.claude/skills/madia-ui-ux/SKILL.md`(AGENTS.md 없음), 코드 `SkinCatalog.cs`, `SkinSpriteSwapper.cs`, `PlayerVisual.cs`, `WeaponSkins.cs`, `ArtCatalog.cs`, `SkinShopView.cs`, `DrinkLab.cs`, `CoinSkinTests.cs`, `Skins10EvidenceCapture.cs`; 기획 `PLAN.md`, `art-worker-task.md`, `skin-quality-review-2026-11-03.md`, `79_신규 캐릭터 스킨10종`, 기준 명세 04/05/09/10/11(스킨 관련 조항).

### 상태 집계 (I0)
| 체크 | 상태 |
|---|---|
| 계약 문서 작성 · 경로/규격 원본 대조 | PASS (source inspection, §10) |
| 현재 아트 품질 기준선(F4/F5/F6) | FAIL 기록(현재 아트 결함 확인) |
| 검 오버레이 비침(F7) | FAIL 기록(기존 결함) |
| Unity 테스트 · 빌드 · 게임 캡처 | NOT RUN (I0 범위 밖) |
| 신규 A1/A2 아트 검수 | NOT RUN (아트 미도착) |
