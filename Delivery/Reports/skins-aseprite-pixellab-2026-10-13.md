# 최종 스킨 아트 적용 — Aseprite + PixelLab (Claude, 2026-10-13)

Orca Task `task_d915ea521e36` / Dispatch `ctx_fae03690c9f4`. 명세 `49_카드 추천과 스킨 비교 무기 스킨 2026-10-13.md` 재개. 카드 추천·몸체 비교·무기 장착 저장 기능과 그 시험 결과는 이전 작업(`Delivery/Reports/skins-2026-10-13.md`)을 그대로 쓰며, 이번 작업은 **아트 교체·문구 정합·관련 회귀·최종 빌드**만 다룬다.

## 결론
- 아트 담당(Codex, Aseprite GUI 소유)이 Aseprite + PixelLab으로 만든 최종 43장(바텐더 몸체 23 + 무기 held 4 + 검 휘두르기 16)만 임포트했다. 이전 Python 팔레트 아트(`ArtSource/skins-2026-10-13`, v2)는 쓰지 않았고, 그 런타임 파일 43장은 모두 새 아트로 덮였다(SHA 대조로 확인).
- 최종 실행본 `Builds/Playable-2026-10-13-aseprite-pixellab`, ZIP `Delivery/CocktailCoffeeTea_1.0.0_aseprite-pixellab-2026-10-13.zip`.

## 사용 도구 증거 (아트 담당 산출물, Claude 미편집)
- 매핑: `ArtSource/skins-aseprite-pixellab-2026-10-13/asset_mapping_manifest.json` — 동결본 SHA256 `51380dc6e8f0dec43003a3ac1ba1d75d6b67d18df8a8b32d6ca9dc1d4062ba37`
- productionTools: PixelLab Aseprite extension GUI "Edit image (Pro)", Aseprite 1.3.18.6 GUI and Lua. pixelLabCalls 2(바텐더 seed 145336185, 무기 시트 seed 704154967), designs 5, pythonUsage "inspection and metadata only".
- 상세: 같은 폴더 `report.md`, `generation-log.json`(프롬프트·입출력 해시), `final-source-sha256.csv`, `evidence/`(프롬프트·완료 화면), native `.aseprite`(프레임마다 `nativeFile`), 비교판 `comparison_body_all23.png`·`comparison_overview.png`·`comparison_sword_all16.png`(root 시각 검수 통과).

## 임포트 (Claude)
- 43장 각각 manifest `outputSHA256`와 원본 대조 → `Assets/_Project/Resources/<recommendedResourcesPath>.png` 복사 → 복사본 SHA 재대조. 동결 manifest 기준으로 다시 전수 대조해 불일치 0. 기록 `Delivery/Reports/skins-aseprite-pixellab-import-2026-10-13.json`.
- 기존 `.meta` 43개 무변경(git diff에 .meta 변경 없음) → GUID·PPU·피벗·필터 유지. `CCT.Editor.WeaponSkinArtImport.ImportBatch`: 무기 20장 기하(rect·pivot·PPU)가 원본과 동일 20/20(`tests/weaponskin-import.log`).
- 바텐더 23장은 먼저(ready 시점) 적용, 무기 20장은 아트 담당 ready 인계(02:48Z) 뒤 적용.

## 이름·차이 문구 (최종 PNG를 확대해 실제 색 확인)
| 스킨 | 이름 | 차이 문구(게임 UI) |
|---|---|---|
| skin_bartender_midnight | 미드나잇 바텐더 | 검정 조끼·바지 → 선명한 미드나잇 남색, 청록 넥타이 → 연한 샴페인 금빛 / 흰 셔츠·머리·얼굴은 기본과 같음 |
| weaponskin_rapier_01 | 자수정 레이피어 | 분홍 날 → 연보라 자수정, 초록 테두리 → 연한 금빛, 손잡이 → 보라 |
| weaponskin_gun_01 | 구리빛 총 | 은빛·청록 총신 → 구리와 황동, 보라 장식 포인트 |
| weaponskin_sword_01 | 푸른 강철 검 | 분홍 날 → 푸른 강철, 초록 코등이·테두리 → 금빛 |
| weaponskin_bow_01 | 달빛 활 | 붉은 활대·흰 시위 → 남색·푸른빛, 양 끝 → 크림 금빛 |

displayMetadata 문구와 다른 곳: 미드나잇 넥타이(밝은 금 아님), 검 손잡이(원래 크림이라 '손잡이 금빛' 제외), 활 시위도 파랗게 바뀜, 레이피어 손잡이 보라 — 실제 그림에 맞췄다. 이름은 displayMetadata 그대로.

## 검증 (이번 아트 대상 회귀만)
| 항목 | 결과 | 증거 |
|---|---|---|
| EditMode WeaponSkin·Commerce·CardAdvisor | 56/56 | `skins-aseprite-pixellab-2026-10-13/tests/editmode-56.xml` |
| PlayMode CardAdviceAndWeaponSkin·SkinShopView·SkinEvidenceCapture | 7/7 | `tests/playmode-7.xml` |
| 경로·기하 | 43 SHA 일치, 무기 20 기하 일치, .meta 무변경 | import JSON, import log |
| 네 무기 실제 렌더 | 레이피어 31948/31948, 총 34607/34607, 검 33601/33601, 활 31662/31662 프레임 스킨 표시; 검 휘두르기 3754/3754 스킨 프레임; 피해 12·사거리 2.2·간격 0.7 기본과 동일 | `evidence/evidence-log.txt`, `combat_*` |
| 몸체+무기 독립 | 비취 몸체 + 푸른 강철 검 휘두르기 프레임(swing_jade) | `combat_jade_body_blue_steel_sword_swing_1920x1080.png` |
| 저장 재실행 | 장착 후 디스크 weapon_skins.json 재읽기 일치(4무기), 알 수 없는 ID 포함 저장으로 ZIP 실행 예외 0 | evidence-log, `zip-smoke-player.log` |
| 화면 720·1080 | 상점 캐릭터 탭·무기 탭(기본/장착), 카드 추천 4종 | `evidence/shop_*`, `card_advice_*` |
| strict 출시 빌드 | Build Finished Success, 430MB | `tests/release-build.log` |
| ZIP 새 폴더 실행(숨김 창, 음소거, 전용 데이터) | 20초 생존, Exception 0 | `zip-smoke-player.log` |

화면 증거는 이전에 고친 창 없는 렌더 경로(`SkinEvidenceCapture`, WaitForEndOfFrame 미사용)를 그대로 썼다. 새 캡처 도구는 만들지 않았고 보이는 창 QA는 실행하지 않았다(사용자 PC 간섭 없음).

## 납품
- 실행본: `Builds/Playable-2026-10-13-aseprite-pixellab/CocktailCoffeeTea.exe` (빌드 원본 `Builds/Release-2026-10-13-aseprite-pixellab`)
- ZIP: `Delivery/CocktailCoffeeTea_1.0.0_aseprite-pixellab-2026-10-13.zip` — SHA256 `def08ce2914eaf934f9bdd1b3fadc38ffcdcc3a1d4bd171309aa6fe008ad44a8`, 257파일, ZIP 내 해시 전수 대조, `_DoNotShip`/BackUp 제외 (`package-proof.json`)
- `CCT.Runtime.dll` SHA256 `43085c0c63ea44def7fd9d61bc22b948e7eed9f5371ffd2c116e636e099c2b17`
- 이전 실행본(`Playable-2026-10-13-skins`, Python 아트 — 최종 아님)·이전 ZIP·`Builds/Windows64`·런처·사용자 세이브는 건드리지 않았다.

## 변경 파일 (이번 작업)
- `Assets/_Project/Resources/CosmeticSkins/skin_bartender_midnight/*.png` 23, `Assets/_Project/Resources/WeaponSkins/**/*.png` 20 (아트 교체, .meta 무변경)
- `Assets/_Project/Scripts/Commerce/SkinCatalog.cs`(미드나잇 차이 문구), `Assets/_Project/Scripts/Commerce/WeaponSkins.cs`(무기 차이 문구)
- `Delivery/Reports/skins-aseprite-pixellab-2026-10-13.md`(이 보고서), `skins-aseprite-pixellab-import-2026-10-13.json`, `skins-aseprite-pixellab-2026-10-13/`(evidence, tests, package.py, package-proof.json, zip-smoke-player.log)

## 한계
- 전체 EditMode/PlayMode는 다시 돌리지 않았다(지시대로 관련 회귀만, 기능 시험 근거는 이전 보고서의 508/230·79/17).
- 화면은 창 없는 렌더 캡처다. 모바일/터치 배치는 확인하지 않았다. git 커밋·런처 갱신·Android 빌드는 하지 않았다.
- 무기 외형은 손에 든 무기와 검 휘두르기 몸체 프레임만 바꾼다(탄환·화살·VFX·하이브리드 보조 활·아이콘은 기본).

## 원본 vs 새 아트 매핑 43장
| # | 종류 | 원본(기본 외형) | 새 아트(ArtSource) | 런타임 Resources | SHA256 앞 12 | native .aseprite |
|---|---|---|---|---|---|---|
| 1 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_idle_right_00.png` | `CosmeticSkins/skin_bartender_midnight/bartender_idle_right_00.png` | a4932b7f9a3c | 있음 |
| 2 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_idle_right_01.png` | `CosmeticSkins/skin_bartender_midnight/bartender_idle_right_01.png` | 5408b2eca697 | 있음 |
| 3 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_idle_right_02.png` | `CosmeticSkins/skin_bartender_midnight/bartender_idle_right_02.png` | 003ee2c017dc | 있음 |
| 4 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_idle_right_03.png` | `CosmeticSkins/skin_bartender_midnight/bartender_idle_right_03.png` | ef175475e24b | 있음 |
| 5 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_move_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_move_right_00.png` | `CosmeticSkins/skin_bartender_midnight/bartender_move_right_00.png` | 2579aa97544a | 있음 |
| 6 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_move_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_move_right_01.png` | `CosmeticSkins/skin_bartender_midnight/bartender_move_right_01.png` | 71cf7b306b0f | 있음 |
| 7 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_move_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_move_right_02.png` | `CosmeticSkins/skin_bartender_midnight/bartender_move_right_02.png` | 280580735c92 | 있음 |
| 8 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_move_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_move_right_03.png` | `CosmeticSkins/skin_bartender_midnight/bartender_move_right_03.png` | f9867434792b | 있음 |
| 9 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_move_right_04.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_move_right_04.png` | `CosmeticSkins/skin_bartender_midnight/bartender_move_right_04.png` | 120bf7377ae7 | 있음 |
| 10 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_move_right_05.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_move_right_05.png` | `CosmeticSkins/skin_bartender_midnight/bartender_move_right_05.png` | b2aaefbcf5ae | 있음 |
| 11 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_melee_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_melee_right_00.png` | `CosmeticSkins/skin_bartender_midnight/bartender_melee_right_00.png` | a859415cb202 | 있음 |
| 12 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_melee_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_melee_right_01.png` | `CosmeticSkins/skin_bartender_midnight/bartender_melee_right_01.png` | 165698e2003e | 있음 |
| 13 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_melee_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_melee_right_02.png` | `CosmeticSkins/skin_bartender_midnight/bartender_melee_right_02.png` | aec3e23d5770 | 있음 |
| 14 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_melee_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_melee_right_03.png` | `CosmeticSkins/skin_bartender_midnight/bartender_melee_right_03.png` | e580a18c8b1d | 있음 |
| 15 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_defeat_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_defeat_right_00.png` | `CosmeticSkins/skin_bartender_midnight/bartender_defeat_right_00.png` | b6b07e67f232 | 있음 |
| 16 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_defeat_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_defeat_right_01.png` | `CosmeticSkins/skin_bartender_midnight/bartender_defeat_right_01.png` | 1e8bfe999e65 | 있음 |
| 17 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_defeat_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_defeat_right_02.png` | `CosmeticSkins/skin_bartender_midnight/bartender_defeat_right_02.png` | abdff15734fa | 있음 |
| 18 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_defeat_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_defeat_right_03.png` | `CosmeticSkins/skin_bartender_midnight/bartender_defeat_right_03.png` | 39568985b78c | 있음 |
| 19 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_hurt_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_hurt_right_00.png` | `CosmeticSkins/skin_bartender_midnight/bartender_hurt_right_00.png` | 8101e73f0495 | 있음 |
| 20 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_ranged_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_ranged_right_00.png` | `CosmeticSkins/skin_bartender_midnight/bartender_ranged_right_00.png` | 48508d353c8e | 있음 |
| 21 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_ranged_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_ranged_right_01.png` | `CosmeticSkins/skin_bartender_midnight/bartender_ranged_right_01.png` | d6444024b7b9 | 있음 |
| 22 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_ranged_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_ranged_right_02.png` | `CosmeticSkins/skin_bartender_midnight/bartender_ranged_right_02.png` | e118153f88f2 | 있음 |
| 23 | body | `Assets/_Project/Art/v1/bartender/frames/bartender_ranged_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/body/skin_bartender_midnight/bartender_ranged_right_03.png` | `CosmeticSkins/skin_bartender_midnight/bartender_ranged_right_03.png` | ee800b054ea1 | 있음 |
| 24 | weapon | `Assets/_Project/Art/v1/weapon_rapier/frames/weapon_rapier_effect_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/weapons/weaponskin_rapier_01/weaponskin_rapier_01_held.png` | `WeaponSkins/weaponskin_rapier_01/weaponskin_rapier_01_held.png` | f43ec65c473a | 있음 |
| 25 | weapon | `Assets/_Project/Art/v1/weapon_gun/frames/weapon_gun_effect_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/weapons/weaponskin_gun_01/weaponskin_gun_01_held.png` | `WeaponSkins/weaponskin_gun_01/weaponskin_gun_01_held.png` | 57bfd01bcbab | 있음 |
| 26 | weapon | `Assets/_Project/Art/v1/weapon_sword/frames/weapon_sword_effect_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/weapons/weaponskin_sword_01/weaponskin_sword_01_held.png` | `WeaponSkins/weaponskin_sword_01/weaponskin_sword_01_held.png` | 164f3605d110 | 있음 |
| 27 | weapon | `Assets/_Project/Resources/ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/weapons/weaponskin_bow_01/weaponskin_bow_01_held.png` | `WeaponSkins/weaponskin_bow_01/weaponskin_bow_01_held.png` | b96968f9151c | 있음 |
| 28 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_00.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_00.png` | 59427acd4784 | 있음 |
| 29 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_01.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_01.png` | c9fe232edd6d | 있음 |
| 30 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_02.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_02.png` | 97c6a784a3e3 | 있음 |
| 31 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_03.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_03.png` | 8200327bc50d | 있음 |
| 32 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_04.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_04.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_04.png` | 8834c846de26 | 있음 |
| 33 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_05.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_05.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_05.png` | 5526bce34cf8 | 있음 |
| 34 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_06.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_06.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_06.png` | 75ee959965e1 | 있음 |
| 35 | embedded_sword | `Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing/tea_sommelier_sword_swing_right_07.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_base/tea_sommelier_sword_swing_right_07.png` | `WeaponSkins/weaponskin_sword_01/swing_base/tea_sommelier_sword_swing_right_07.png` | 5f9ea8e42203 | 있음 |
| 36 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_00.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_00.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_00.png` | 004c4eab5cfb | 있음 |
| 37 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_01.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_01.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_01.png` | faff010904db | 있음 |
| 38 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_02.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_02.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_02.png` | c98e82baa876 | 있음 |
| 39 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_03.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_03.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_03.png` | bd60e9649380 | 있음 |
| 40 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_04.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_04.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_04.png` | 0a0fa6e58810 | 있음 |
| 41 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_05.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_05.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_05.png` | aad4e10de4b9 | 있음 |
| 42 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_06.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_06.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_06.png` | 7782d321b36c | 있음 |
| 43 | embedded_sword | `Assets/_Project/Resources/CosmeticSkins/skin_tea_jade/tea_sommelier_sword_swing_right_07.png` | `ArtSource/skins-aseprite-pixellab-2026-10-13/sword_swing/swing_jade/tea_sommelier_sword_swing_right_07.png` | `WeaponSkins/weaponskin_sword_01/swing_jade/tea_sommelier_sword_swing_right_07.png` | c29ded577204 | 있음 |

productionTools: PixelLab Aseprite extension GUI Edit image (Pro), Aseprite 1.3.18.6 GUI and Lua / pixelLabCalls: 2 / designs: 5 / pythonUsage: inspection and metadata only
