# 치장 스킨 인앱 결제 — 클라이언트

외형만 바꾸는 스킨 2종(Google Play 비소모성)입니다. 능력치, 판정, 밸런스에는 전혀 관여하지 않습니다.

| 상품 ID | 직업 | 아트 폴더 |
|---|---|---|
| `skin_bartender_midnight` | 바텐더 | `Resources/CosmeticSkins/skin_bartender_midnight/` |
| `skin_tea_jade` | 티 소믈리에 | `Resources/CosmeticSkins/skin_tea_jade/` |

가격은 Play Console에서만 정합니다(현재 계획 KRW 1,000). 코드에는 가격이 없고, 화면에는 스토어가 준 `localizedPriceString`만 표시합니다. 가격 정보가 없으면 "가격 정보를 불러올 수 없음"을 띄우고 구매 버튼을 끕니다.

## 설정 (`Assets/_Project/Resources/Commerce/commerce_config.json`)

```json
{ "enabled": false, "verifierEndpoint": "", "requestTimeoutSeconds": 15 }
```

- 결제는 `enabled=true`이고 `verifierEndpoint`가 `https://` 절대 URL일 때만 켜집니다. 그 밖에는 꺼진 상태로 동작합니다(fail-closed). 꺼져 있으면 스토어에 연결하지 않고, 상점은 미리보기만 보여 주며 "결제 준비 중"을 표시합니다.
- 출시된 설정은 꺼져 있습니다(`CommerceTests.ShippedConfig_IsDisabledUntilConfigured`).

## 스토어프런트

- Android 플레이어: `UnityIapSkinStore`를 씁니다(Unity Purchasing 5.4.4 `StoreController`). 이 파일은 `#if UNITY_ANDROID`로 감쌌고, 인스턴스는 `UNITY_ANDROID && !UNITY_EDITOR`일 때만 만듭니다.
- Windows와 에디터: `UnavailableSkinStore`를 씁니다. "현재 플랫폼에서는 구매할 수 없습니다(준비 중)"를 표시하고 구매·복원을 끕니다. 가짜 구매는 없습니다.
- 테스트용 가짜 스토어와 검증기는 테스트 어셈블리에만 있습니다. 런타임 어셈블리에 그런 타입이 없다는 것은 테스트로 확인합니다.
- Steam은 나중에 `ISkinStore` 구현을 하나 추가하고 `CommercePlatform.CreateStore`에서 고르게 하면 됩니다. 소유권은 `owned[].storefront`(`googleplay`/`steam`) 키로 나눠 기록합니다. 따라서 계정 연동이 없는 한 Google 구매가 Steam에서 열리지 않습니다.

## 구매 흐름 (`SkinCommerce`)

1. 앱이 시작되면 `SkinCommerceRunner`가 다음 프레임에 `Start()`를 호출합니다. 결제가 켜져 있을 때만 스토어에 연결합니다. 이어서 상품을 조회(현지화 가격)하고 `FetchPurchases`를 호출합니다. 이 호출로 미승인 구매와 이미 승인된 구매가 복원됩니다.
2. 결제 완료(PendingOrder)가 오면 검증 서버에 `POST {productId, purchaseToken, installationId}`를 보냅니다. 응답은 `{verified, productId}`입니다.
3. `verified && productId` 일치를 확인한 다음 `commerce.json`에 소유를 기록합니다(원자적 저장). 저장에 성공했을 때만 `ConfirmPurchase`로 승인합니다.
4. 네트워크·서버 오류(5xx, 408, 429, 깨진 JSON)나 저장 실패가 나면 승인하지 않고 보류 목록에 남깁니다. 5초부터 시작해 두 배씩, 최대 10분 간격으로 재시도합니다. 상점을 열거나 앱으로 돌아오면(`OnApplicationPause(false)`) 즉시 재시도합니다. 앱을 다시 시작해도 Google이 같은 미승인 구매를 다시 넘겨줍니다.
5. `verified=false`이거나 상품 ID가 다르면 지급하지 않고 승인하지 않습니다. 같은 세션에서는 다시 검증하지 않습니다. Google은 3일 동안 승인되지 않은 구매를 자동 환불합니다.
6. 보류 결제(DeferredOrder)는 "결제 대기 중"으로 표시합니다. 결제 완료가 왔을 때만 2번부터 진행합니다.
7. 이미 저장된 토큰의 미승인 구매가 다시 와도 건너뛰지 않습니다. 서버에 다시 확인한 뒤 승인합니다.
8. "구매 복원" 버튼은 `FetchPurchases`를 다시 호출합니다. 이미 승인된 구매가 이 스토어프런트에 기록되어 있지 않으면, 검증과 저장을 거친 뒤 지급합니다(승인 호출은 하지 않습니다).
9. 보유 재확인(환불·취소 대응)
   - 스토어가 돌려준 승인 구매는 이미 보유한 것이어도 다시 확인합니다. 실행(세션)마다 토큰당 1회, 그리고 "구매 복원"을 누를 때마다 서버에 보냅니다.
   - 서버가 `verified=false`(또는 상품 ID 불일치)를 주면 이 스토어프런트의 소유 기록을 지우고, 장착을 해제한 뒤 `commerce.json`에 저장합니다. 저장에 실패하면 이번 세션 메모리에서만 해제됩니다.
   - 네트워크나 서버 오류가 나면 캐시된 소유를 유지합니다. 다음 실행 때나 복원 때 다시 확인합니다.
   - 한계: 환불되어 Google 구매 내역에서 아예 빠진 주문은 재확인 대상에 오지 않으므로, 이 클라이언트는 아직 감지하지 못합니다. 서버 쪽 Voided Purchases 처리나 "조회 결과에 없는 보유 해제"가 필요합니다(미구현).
10. 취소, 이미 보유, 상품 없음, 오프라인, 결제 거절은 모두 한국어 안내만 띄우고 지급하지 않습니다.

## 토큰과 로그

- Google 검증에는 `order.Info.Google.PurchaseToken`만 씁니다. 이 값이 없으면 `TransactionID`로 대신하지 않습니다. 지급도 승인도 하지 않고, 다음 구매 내역 조회 때 다시 받습니다.
- 결제 코드 로그에는 고정 문구, 상품 ID, 예외 타입 이름만 남깁니다. 구매 토큰, 영수증, 서버 응답 본문, 스토어 상세 문구, 예외 메시지는 남기지 않습니다.

## 저장 (`commerce.json`)

- `save.json`과 같은 폴더에 둡니다(`PermanentProgress.ActiveSavePath` 기준). 따라서 `-cct-data-dir`와 테스트 격리 설정을 그대로 따릅니다.
- 쓰기 순서는 `.tmp` 쓰기와 flush, 재읽기 검증, 그다음 `File.Replace`(`.bak` 보존)입니다. 원본이 손상되면 `.bak`에서 읽습니다.
- 필드
  - `installationId`: 설치할 때 만든 임의 GUID입니다. 하드웨어 ID가 아닙니다.
  - `owned[{storefront, productId, tokenHash}]`: 소유 기록입니다. 토큰 원문은 저장하지 않습니다.
  - `equipped[productId]`: 장착한 스킨이며 직업마다 하나입니다.
- 이 파일은 캐시입니다. 근거는 스토어 기록과 서버 검증 결과입니다.

## 런에서 스킨 적용

- `GameManager.CreatePlayer`는 `player.Init` 바로 다음에 `SkinSpriteSwapper.AttachIfEquipped(player.Visual, preset.job)`를 호출합니다. 아래 조건을 모두 만족할 때만 스킨을 붙이고, 하나라도 빠지면 기본 아트를 씁니다.
  - 이번 런의 직업과 스킨 직업이 같습니다.
  - 현재 스토어프런트에서 소유하고 있습니다.
  - 장착되어 있습니다.
  - 아트가 있습니다.
- `SkinSpriteSwapper`는 `[DefaultExecutionOrder(5000)]`의 LateUpdate에서 동작합니다. 몸체 렌더러의 현재 스프라이트를 같은 이름의 스킨 스프라이트로 바꿉니다.
  - 피벗, anchor, 애니메이션, 활 준비(`bow_*`)와 검 휘두르기 클립을 원본 그대로 따릅니다.
  - 이미 스킨인 스프라이트는 건드리지 않습니다.
  - 무기 렌더러와 판정은 바꾸지 않습니다.

## 상점 UI (`SkinShopView`)

- 타이틀 오른쪽 위의 "스킨" 버튼(`SkinShopButton`)으로 엽니다.
- 스킨마다 다음을 보여 줍니다.
  - 기본 미리보기와 스킨 미리보기. 스킨 미리보기는 소유하기 전에도 보이고, 아트가 없으면 기본 아트에 "아트 준비 중"을 붙입니다.
  - 상태 문구.
  - 구매 버튼(현지화 가격 표시)과 장착/해제 버튼. 높이는 120px입니다.
- 화면 아래에는 "구매 복원"(`RestoreButton`)과 "뒤로"(`BackButton`) 버튼이 있습니다. 안드로이드 뒤로 키 라우터가 `BackButton`을 누릅니다.

## 아직 안 한 것

- 검증 서버 배포와 https 주소 설정. 서버 코드는 `Server/skin-verifier`에 있고 root가 담당합니다.
- Play Console 상품 생성: 두 ID 모두 관리형(비소모성) 상품, KRW 1,000, 앱 서명, 라이선스 테스트 계정.
- Unity 프로젝트의 Google Play 라이선스 키(클라우드 프로젝트 연결) 설정 확인.
- 실제 기기 결제 테스트: 구매, 취소, 보류 결제, 오프라인, 재설치 후 복원, 미승인 재시도.
- Steam 스토어프런트(나중에)와 스토어 간 계정 연동.
