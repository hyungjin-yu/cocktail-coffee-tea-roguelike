# 79 신규 캐릭터 스킨 10종 — 게임 통합 보고 (2026-10-20)

Orca task_e5ca8a5f3581 / dispatch ctx_6a84b2c53fa4. 기준 커밋 0413263. 커밋·스테이징 없음(작업 트리에만 반영).
아트(Aseprite·PixelLab, `ArtSource/skins-aseprite-pixellab-2026-10-20/`)는 아트 담당 소유이며, root 승인(frozen) 후 PNG만 import했다. 아트를 만들거나 고치지 않았다.

## 결과 요약

- 신규 코인 스킨 정확히 10종(바텐더 5·티 소믈리에 5, 79 표의 ID·이름·디자인). 각 **판매 코인 1000**, 현금 상품 아님.
- 기존 유료 2종(미드나잇·비취)·영수증 복원·코인 소유·장착·기본 직업·무료 무기 외형 4종은 그대로 유지. 새 실제 현금 SKU·스토어 등록·서버 허용 목록 변경 없음(`Server/skin-verifier`는 손대지 않음).
- 실제 10종 전투 장착(실제 코인 구매 → 장착 → 런 시작 경로)에서 **기본 복장 프레임 0**, 좌우 반전 정상, 능력치·충돌·크기 불변.
- 빌드 `Builds/Playable-2026-10-20-drinklab-v1.1-skins10`, ZIP `Builds/CocktailCoffeeTea-DrinkLab-v1.1-skins10-2026-10-20-win64.zip`
  SHA-256 `0D26AAA8060A82CFF7E6EC84A80963CC25BFED7B7E4900A1F978C7367ABA35C6` (108,704,648 B, `.sha256` 파일 동봉).
- 실행기: `Delivery/DrinkLab-v1.1/Play-DrinkLab-v1.1-skins10.cmd`, `…-skins10-TestMaterials.cmd` (supply2 실행기와 같은 격리 저장 `TestData/drinklab-v1.1`, 이미 실행 중이면 거부, 자동 실행 없음).

## IAP 등록 점검(핵심 위험)

기존 `SkinCommerce.Start`는 `SkinCatalog.ProductIds`(카탈로그 **전부**)를 스토어에 등록했다 — 코인 스킨을 그냥 추가하면 유료 상품으로 조회·등록될 뻔했다.
변경: `SkinDefinition.paid` + `SkinCatalog.PaidProductIds`(기존 2종만) / `AllProductIds` / `IsPaid`.
현금 경로 전부 paid 게이트: `Start`(Initialize), `CanBuy`, `Buy`(코인 스킨은 `MsgCoinOnly`), `OnStoreReady`(현금 상품만 가격 보관), `OnPurchaseAlreadyConfirmed`, `Enqueue`(검증·승인), `CommerceState.Normalize`(commerce.json의 코인 스킨 현금 기록 무시). 시험: `CoinSkinTests.Store_RegistersOnlyPaidProducts_NotCoinSkins`, `CoinSkin_CashBuyVerifyRestorePaths_AllRejected`, `ForgedCashRecordForCoinSkin_DroppedOnNormalize_PaidRecordKept`.

## 판매·적용 게이트(아트 없는 판매 금지)

- `SkinArt.RequiredFrameNames`: 실제 런타임 사용 프레임 = 바텐더 23(몸체 클립 전체), 티 28(몸체 23 + 검 휘두르기 재생 구간 03~07). 활 준비(bow_*)·검 00~02는 미재생이라 제외(아트 계약과 일치).
- `SkinArt.Exists = IsComplete`: 하나라도 빠지면 런에서 기본 아트(번쩍임 방지). `DrinkSales.OnSale`: 신규 스킨은 완성 아트일 때만 판매. 상점 미리보기도 완성일 때만.

## 상점(12종 + 맵)

- 타이틀 → 스킨 → 캐릭터 탭: 왼쪽 **스크롤 목록**(직업별 머리글, 행 = 이름 / "직업 전용 · 상태", 스크롤바, 키보드 포커스 따라 스크롤) + 오른쪽 **선택 상세**(기본/스킨 같은 크기 미리보기, 차이 설명, 외형 전용 표기, 상태, 구매·장착). 유료 2종은 스토어 가격, 코인 10종은 "구매 1000코인"(DrinkLab.BuyCosmetic). 키보드: 행 ↑↓, → 상세의 쓸 수 있는 버튼, ← 행(비활성 버튼으로 보내지 않음). 버튼 터치 높이 120.
- 음료 연구소 주문·판매 탭의 판매 코인 목록: 13카드(스킨 12+맵), 스킨 카드에 미리보기·아트 준비 상태.
- 미리보기 잘라내기 상자 확장(x66..178, y10..224): 별자리 높은 모자·산길 등짐이 이전 상자(80..176/32..224)에서 잘렸음. 표시 전용. (`test-results/verify-final.txt`의 "OUTSIDE"는 이전 상자 기준 측정값.)
- 836×705 상세 칸 겹침(능력치 동일/상태 문구) 및 코인 스킨 선택 시 잘못된 "결제 불가" 안내를 발견·수정. 4개 크기에서 상세 칸 요소 겹침 자동 검사 0.

## 검 외형 오버레이(무료 무기 외형 보존)

신규 몸체 스킨 + 무료 검 외형일 때 기존 경로는 몸체를 기본색 휘두르기 프레임으로 덮어 기본 복장이 번쩍였다 → 비취가 아닌 몸체 스킨은 몸체를 덮지 않고, 아트 담당이 승인 마스크로 Aseprite export한 **검-only 5장**(03~07, root 승인)을 몸체 자식 SpriteRenderer(정렬 +2)로 휘두르기 프레임 동안만 표시. 5장 해시 일치, 픽셀 검사: 불투명 픽셀 전부 마스크 안·승인 원본과 0 차이. 비취·스킨 없음 경로는 기존 그대로.

## 검사 결과(실제)

| 실행 | 결과 | 파일 |
|---|---|---|
| EditMode 전체 edit2(코드 준비 시점) | 752/752 Passed | test-results/edit2.xml |
| PlayMode 전체 play1(오버레이·아트 전 코드) | 295 중 289 pass, **2 fail**, 4 skip(증거 전용) | test-results/play1.xml |
| play1 실패 2건 단독 재실행 play2 | 둘 다 Pass(재현 안 됨, **원인 미증명**): DrinkLabPlayTests.Camera_FollowsProcess(41.6>40, r78과 같은 흔들림), SpecSpawnAndEnemyTests.Spawner STALL(LevelUp 중 timeScale 0 30초) | test-results/play2.xml |
| EditMode 관련 edit5(10종 import 후) | 62/62 | test-results/edit5.xml |
| PlayMode 관련+증거 play3(10종 import 후) | 16/16 (Skins10EvidenceCapture 포함) | test-results/play3.xml |
| 상점 레이아웃 수정 후 play6 | 6/6 | test-results/play6.xml |

전체 PlayMode는 play1 이후 다시 돌리지 않았다(코디네이터 지시: 관련 범위만). 따라서 "전체 통과"가 아니다.

## 실제 10종 전투 증거(창 없음)

배치 에디터(그래픽 사용, 창 없음)에서 카메라·UI를 RenderTexture로 렌더(`Tests/PlayMode/Skins10EvidenceCapture.cs`, `CCT_EVIDENCE_DIR` 있을 때만). 격리 임시 저장, 메모리 상거래.
실제 경로: 판매 코인 10×1000 구매(`BuyCosmetic`, 아트 게이트 통과) → `SkinCommerce.Equip` → `GameBootstrap.Build`(런 시작 `AttachIfEquipped`). 스킨마다 직업 무기 2종(바텐더 레이피어·총 / 티 검(무료 검 외형 장착)·활), 오른쪽→왼쪽 이동, 일반 스폰 실제 전투.
**강제 경로(표기)**: 체력 크게, 검 휘두르기 0.5초 간격 직접 재생, 끝에 피격·쓰러짐 직접 재생, 이동 입력 대체.

`evidence/skins10-evidence-log.txt` 요약(20회 실행 모두):
- baseCostumeFrames=0(렌더 시점 LateUpdate 10000 탐침, 수만 프레임), facing=-1/1.
- 티 검: 휘두르기 프레임 = 오버레이 표시 프레임(예 astronomer 13827/13827), 휘두르기 밖 오버레이 0, 이름 불일치 0. 활·바텐더: 오버레이 0.
- 능력치 dmg 12 / range 2.2 / interval 0.7 / hp 100 / radius 0.4 / 크기 — 스킨 없음 기준과 동일.
- **실제 재생 coverage**: 바텐더 20/23(대기 01~03 미표시 — 계속 이동), 티 21/28(대기 01~03, 일반 melee 4 미표시 — 검은 휘두르기, 활은 ranged). 미표시 프레임은 정적 검사(`CoinSkinTests.CoinSkinArt_EitherAbsentOrCompleteAndAligned`: 10종 전 필수 프레임 존재·캔버스·PPU·피벗 일치)로만 확인 — 실제 재생으로는 보지 않았다.

캡처(최소 컷): 상점 4크기(1920×1080·1280×720·960×720·836×705) 첫/마지막 행, 10종 상세 1080, 구매+중복클릭+장착, 음료 연구소 코인 목록, 전투 오른쪽 1280×720 20장, 왼쪽 836×705 10장, 검 휘두르기 5장, 쓰러짐 2장. root가 836×705 상점·tropic 왼쪽·astronomer 휘두르기 확인.

## 아트 import

- 255 몸체 PNG + 5 오버레이: `final-source-sha256.csv`와 전부 해시 일치, 파일 집합 정확(23/28). 메타는 기존 미드나잇/비취 같은 이름 프레임 메타 복제(새 GUID, 피벗·PPU·필터 동일).
- rail: rev1을 보류 메시지 전에 import → 최종 frozen과 PNG 해시 동일(변경 0)이었으나, "메타 보존" 지시 전에 폴더를 지우고 재import해서 rail 메타 GUID가 한 번 바뀌었다(미커밋·참조 없음). 이후 `--update` 모드로 메타 보존.
- 빌드 데이터(globalgamemanagers)에 10종 경로 23/28개씩, 오버레이 5개 포함 확인.

## 빌드·실행·보존

- `test-results/final-skins10.out.txt`: 빌드 성공, ZIP·해시, 새 폴더 압축 해제 후 `-batchmode -nographics` 격리 데이터 폴더 30초 생존(예외 0, 결제 비활성 로그), 그 PID만 종료. 스크립트 종료 코드 1은 robocopy 성공 코드(1) 누출(이전과 같음).
- EXE 해시가 supply2와 같음(Unity 플레이어 스텁 동일, 데이터가 다름).
- 보존: 실제 사용자 저장(LocalLow save/settings), 격리 테스트 저장 `Delivery/DrinkLab-v1.1/TestData`, supply2 EXE·ZIP 해시 전후 동일(`preserve-before/after.txt`, changed=0). 사용자 게임 창·포커스 사용 없음.

## 변경 파일

수정: Commerce/CommerceState.cs, SkinCatalog.cs, SkinCommerce.cs, WeaponSkins.cs; Core/AutoTour.cs(QA 투어 skins 모드 확장); Drinks/DrinkLab.cs, DrinkLabView.cs, DrinkSales.cs; Player/PlayerVisual.cs(`SwordSwingFrames`); UI/SkinShopView.cs; Tests EditMode CommerceTests, WeaponSkinTests; PlayMode SkinEvidenceCapture(캡처 함수 공유), SkinShopViewTests.
추가: Editor/CoinSkinArtImport.cs(에디터 import 도구, 이번에는 미사용 — 실제 import는 `test-results/import-body.py`), Tests EditMode CoinSkinTests; PlayMode SkinShopListTests, SwordOverlayPlayTests, Skins10EvidenceCapture; Resources/CosmeticSkins/skin_*(10폴더), WeaponSkins/weaponskin_sword_01/swing_overlay; Delivery 실행기 2개.

## 한계·미검사

- 사람의 실제 플레이(마우스·터치·게임패드 실기기)는 하지 않았다. 키보드 탐색은 Selectable 탐색 구조와 포커스 이벤트로 검사.
- 실제 재생 coverage는 위 수치(대기 01~03, 티 일반 melee 미표시). 정적 정렬 검사로만 보장.
- 음료 연구소 코인 목록 캡처는 배치 에디터 화면 크기 기준 배치(1열)로 찍혔다 — 1080 실제 창의 2열 배치는 이번에 찍지 않았다.
- play1의 2건 실패는 단독 재실행에서 재현되지 않아 원인 미증명.
- Android/스토어 빌드, 서버 검증기는 이번 범위 밖(변경 없음).
