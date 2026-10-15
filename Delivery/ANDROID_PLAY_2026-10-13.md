# Android Play 준비 — 2026-10-13 (Claude)

Orca Task `task_5dbf3d5a8dad` / Dispatch `ctx_9e5ad0f382a4`. 범위: Android 출시/내부 테스트용 AAB 준비, 표시 제목 `음료 마스터`, 게임 안 개인정보처리방침 링크, 패키지 `com.hyungjinyu.cocktailcoffeetea` 유지, 승인된 활(bow-hand-mirror)·캐릭터/무기 스킨 유지, 결제 비활성 유지.

하지 않은 일: 업로드, 심사 제출, 공개 출시, Play 앱 서명 등록, Console·브라우저 GUI 조작, 아트 생성·수정. AGENTS.md는 Unity·기획 폴더와 상위 경로 어디에도 없다.

## 결과 요약
| 항목 | 상태 |
|---|---|
| 업로드 서명 AAB | **빌드 성공**: `C:\Users\user\Documents\Projects\CocktailCoffeeTea\Builds\Android-2026-10-13\CocktailCoffeeTea-1.0.0-vc1-upload-signed.aab` |
| 납품 사본 | `C:\Users\user\Documents\Projects\CocktailCoffeeTea\Delivery\Android-2026-10-13\CocktailCoffeeTea-1.0.0-vc1-upload-signed.aab` |
| SHA-256(두 파일 동일) | `af546068d11bd7c0e823547b40d38b9a74e061a022b1f879efc03754782259b3` |
| 크기 | 111,726,821 B. base 모듈 압축 약 106 MB(assets 71.1 / lib 31.6 / dex 3.0) < Play base 200 MB |
| 패키지 / versionCode / versionName | `com.hyungjinyu.cocktailcoffeetea` / 1 / 1.0.0 (Console 번들 기록 없음 — root 확인) |
| min / target SDK | 26 / 36 |
| ABI / 백엔드 | arm64-v8a만 / IL2CPP |
| 앱 이름 | `@string/app_name` = `음료 마스터`(bundletool dump 확인) |
| 화면 / 디버그 | 가로(landscape 자동 회전) / `android:debuggable` 없음 |
| 서명 | `jar verified`. 서명 인증서 SHA-256 `A4:20:20:95:7C:AE:6F:A0:0D:66:D0:05:31:78:8B:EB:46:69:5F:2B:6C:A8:58:B4:EF:A0:83:CB:7D:7D:A7:BF` = 새 업로드 키 |
| 16KB 페이지 | ELF: arm64 .so 7개 모두 PT_LOAD 정렬 0x4000 이상(`16kb-page-check.txt`) + 패키징: BundleConfig `optimizations.uncompressNativeLibraries.alignment = PAGE_ALIGNMENT_16K`(`bundletool-dump-config.json`) |
| 권한 | INTERNET, ACCESS_NETWORK_STATE, com.android.vending.BILLING(Unity IAP 패키지 포함 — 앱 설정상 결제는 꺼짐), DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION |
| 결제 | `commerce_config.json` enabled=false, verifierEndpoint 비어 있음 — 구매 불가 상태 유지 |
| 실기기 실행 | **미검증** — adb 연결 기기 없음(`adb devices` 빈 목록) |

## 코드 변경
- `Assets/_Project/Scripts/Core/GameInfo.cs`(신규): `Title = "음료 마스터"`, `PrivacyPolicyUrl = https://hyungjin-yu.itch.io/coffee-cocktail-tea/devlog/1704962/-privacy-policy`(HTTP 200 확인), 시험용 교체 가능한 `UrlOpener`.
- `UI/TitleView.cs`: 타이틀 제목 → `GameInfo.Title`.
- `UI/CreditsView.cs`: 첫 줄 제목 → `GameInfo.Title`, 하단 `개인정보처리방침` 버튼(외부 브라우저 `Application.OpenURL`).
- `UI/SettingsView.cs`: 하단 오른쪽 `개인정보처리방침` 버튼(다시 시도 버튼과 겹치지 않음).
- `Editor/BuildRelease.cs` (Android):
  - 빌드 동안만 `PlayerSettings.productName = "음료 마스터"`이고 빌드 후 원래 값(`CocktailCoffeeTea`)으로 복원한다. Windows의 persistentDataPath(세이브 위치)가 productName에 묶여 있어 전역 변경하지 않았다. Android 세이브 경로는 패키지 기준이라 영향이 없다.
  - 업로드 키는 환경 변수(`CCT_ANDROID_KEYSTORE`, `CCT_ANDROID_KEYSTORE_PASS`, `CCT_ANDROID_KEY_ALIAS`, `CCT_ANDROID_KEY_PASS`)가 모두 있고 파일이 있을 때만 적용한다. 값은 로그하지 않는다. 빌드 후 ProjectSettings의 키 경로·별칭·비밀·useCustomKeystore를 초기화한다(빌드 후 확인: productName 복원, androidUseCustomKeystore 0, 별칭 비어 있음).
  - 산출물 이름: 서명 시 `-vc<code>-upload-signed.aab`, 키가 없으면 기존 `debugsigned-NOT-FOR-PLAY-UPLOAD.aab`.
- `Tests/EditMode/StoreInfoTests.cs`(신규): 제목·URL·패키지/targetSdk 상수, 타이틀 제목, 제작 정보 제목 + 이전 가제 미표시 + 버튼 → URL, 설정 버튼 → URL. 실제 브라우저는 열지 않는다.
- `Tests/PlayMode/SkinEvidenceCapture.cs`: `Capture_PrivacyPolicyButtons_CreditsAndSettings`(기존 창 없는 렌더 경로 재사용).

## 시험 (관련만)
| 실행 | 결과 | 파일 |
|---|---|---|
| EditMode StoreInfo·TitleLayout·SettingsEffects·WeaponSkin·BakedArtCatalog(활 방향/발사)·Commerce | 66/66 | `Delivery/Android-2026-10-13/tests/editmode-related-66.xml` |
| PlayMode MeleeSwingVisual(승인 활 자세 4조합·좌우)·CardAdviceAndWeaponSkin·SkinShopView | 9/9 | `tests/playmode-related-9.xml` |
| 개인정보처리방침·제목 렌더 캡처(720·1080) | 통과 | `tests/playmode-privacy-capture.xml`, `evidence/privacy_*`, `evidence/title_1920x1080.png`, `evidence/privacy_title_overview.png` |

캡처는 에디터의 데스크톱 레이아웃 렌더다. Android 터치 UI 배치(`MobilePlatform.IsTouchUI`)와 실기기 화면은 확인하지 않았다.

## 업로드 키 (새로 생성, root 승인)
- 이 앱 전용 키는 없었다. 다른 프로젝트 키(Our_First_Year, project-k, OneDrive 백업 release.keystore)는 파일명만 확인했고 열거나 쓰지 않았다. Console에 번들·출시 기록이 없음을 root가 확인했다.
- 위치: `%LOCALAPPDATA%\CCT-Signing\` (ACL: 현재 사용자만).
  - `cocktailcoffeetea-upload.p12`: PKCS12, alias `upload`, RSA 4096, SHA384withRSA, `CN=hyungjin-yu, O=OPAD`, 유효 2026-10-13 ~ 2054-02-28
  - `upload_certificate.pem`(공개 인증서, 납품 폴더에도 사본)
  - `upload-keystore-password.dpapi`: 무작위 32자 비밀. 저장소·키 공용이며 Windows DPAPI CurrentUser로 암호화했다.
  - `README.md`, `Show-CctUploadPassword.ps1`(비밀을 출력하지 않고 클립보드로 복사), `Build-CctSignedAab.ps1`(서명 빌드), `Scan-CctSecretLeak.ps1`(유출 검사, 경로만 출력)
- **주의: DPAPI 비밀은 이 PC의 이 Windows 사용자만 풀 수 있다. 휴대 가능한 백업이 아니다.** 첫 업로드 전에 소유자가 `Show-CctUploadPassword.ps1`로 비밀을 비밀번호 관리자에 옮기고, `.p12`를 오프라인 백업해야 한다. 키와 비밀을 모두 잃으면 Play 지원에 업로드 키 재설정을 요청해야 한다.
- 비밀 처리: keytool에는 `-storepass:env`로만 전달했다.
  - Unity가 생성 Gradle 파일에 서명 비밀을 평문으로 써서, 스크래치 `Library/Bee`의 build.gradle·build.gradle.xml·inputdata.json 4곳을 `***`로 지웠다.
  - 바이너리 캐시(.dag/.payloads/IntermediateFiles 정보 3개)와 `~/.gradle` 컴파일 스크립트 캐시 2개 디렉터리를 삭제했다(재생성됨).
  - 납품 폴더·Editor 스크립트·로그 재검사: 0건.
- 첫 키 생성 시도는 keytool의 표준 오류 안내를 PowerShell이 중단 오류로 처리해서 키 없이 끝났다. 쓰이지 않은 비밀 파일만 지운 뒤 다시 생성했다.

## 빌드 환경
- Unity 6000.3.25f1. Android SDK platforms 34·35·36·37.0, build-tools 36.0.0, NDK r27c(27.2.12479018), OpenJDK 17.0.18, Gradle 9.3.1, bundletool 1.17.2.
- 메인 프로젝트에서 다른 Unity 에디터가 돌고 있지 않았다(사용자 `unity mcp`만).
- 빌드는 Android Library를 가진 스크래치 사본 `C:\UnityScratch\cct-android`에 Assets·Packages·ProjectSettings를 미러해서 실행했다. 메인 Library는 Windows 대상 그대로다. 로그는 `Delivery/Android-2026-10-13/android-aab-build.log`(Unity가 비밀을 `***`로 가림, 재검사 0건).
- 빌드 폴더의 `*_BackUpThisFolder_ButDontShipItWithYourGame`, `*_BurstDebugInformation_DoNotShip`은 디버그 심볼이다(업로드 대상 아님).

## 남은 블로커·사람 작업
1. **실기기 검증 없음**(adb 기기 없음): 설치·실행, 터치 UI 배치, 개인정보처리방침 버튼으로 브라우저 열기, 글자 크기, 발열·장시간, 백그라운드 복귀.
2. **업로드 키 백업**: 비밀은 DPAPI 전용이다 — 소유자가 비밀번호 관리자와 오프라인 백업을 하기 전에는 첫 업로드를 하지 않는 것을 권장한다. 첫 업로드 때 Play 앱 서명이 이 키를 업로드 키로 등록한다(root/사용자 작업).
3. **결제**: Play 상품 등록, 서비스 계정, HTTPS 검증 서버가 없다. 앱은 enabled=false로 구매가 불가하다. BILLING 권한은 매니페스트에 있으니 데이터 보안·인앱 구매 표기는 root가 실제 상태로 판단해야 한다.
4. **스토어 자료**: 실제 Android 스크린샷 없음(Windows QA 캡처는 업로드용 아님).
5. **첫 출시 요건**: 계정 유형에 따른 비공개 테스트(12명·14일) 요건은 Console 확인 사항이다.
6. versionCode는 1이다. 이후 재업로드할 때마다 `BuildRelease.AndroidVersionCode`를 올려야 한다.

## 추가 확인
- `bundletool validate --bundle`(bundletool 1.17.2) 종료 0 — `Android-2026-10-13/bundletool-validate.txt`.
- 납품 폴더 `upload_certificate.pem`의 SHA-256 = AAB 서명 인증서 SHA-256(`A4:20:20:95:…:7D:A7:BF`). `jarsigner -verify` = `jar verified`(자체 서명·타임스탬프 없음 경고는 업로드 키에서 정상).
- 빌드 래퍼의 "exit 1" 원인: Unity는 **종료 0**이었고 로그에 `[CCT] Android 빌드 완료 … CocktailCoffeeTea-1.0.0-vc1-upload-signed.aab`가 있다. exit 1은 Unity 종료 뒤 같은 PowerShell 스크립트(`Build-CctSignedAab.ps1`)가 돌린 비밀 유출 검사 단계에서 나왔다. 일부 파일 읽기의 비종료 오류(ErrorAction SilentlyContinue)로 마지막 명령 상태가 실패로 남아 PowerShell 프로세스 종료 코드가 1이 됐다. 빌드 실패가 아니며, 산출물 AAB가 생성·검증됐다.

## 16KB 패키징 설정 확인 (후속, 2026-10-13)
- `bundletool dump config --bundle CocktailCoffeeTea-1.0.0-vc1-upload-signed.aab`(Unity 동봉 bundletool 1.17.2) 종료 0. 출력은 `Delivery/Android-2026-10-13/bundletool-dump-config.json`(비밀 없음)에 있다.
- 결과: `optimizations.uncompressNativeLibraries.alignment = "PAGE_ALIGNMENT_16K"`, `storeArchive.enabled = true`, 분할 차원 ABI·TEXTURE_COMPRESSION_FORMAT(밀도·언어 분할 해제). 번들 생성 bundletool 버전은 1.18.3(Unity Gradle)이다.
- 따라서 Play가 생성할 APK는 압축하지 않은 네이티브 라이브러리를 16KB 경계로 정렬하도록 설정되어 있다. 앞의 ELF PT_LOAD 0x4000 확인과 합쳐 16KB 페이지 요건(ELF + 패키징 정렬)을 충족한다. APK 생성·zipalign 검증은 따로 하지 않았다(설정으로 확인).
- AAB는 변경하지 않았다: SHA-256 `af546068d11bd7c0e823547b40d38b9a74e061a022b1f879efc03754782259b3`(재확인). 재빌드·시험·업로드는 하지 않았다.

## 네이티브 디버그 기호·R8 매핑 (Console 경고 후속, 2026-10-13)
- Console 경고 2건(네이티브 디버그 기호 없음, R8 매핑 없음)은 모두 업로드를 막지 않는 경고다.
- **R8 매핑**: 이 빌드 Gradle(`launcher/build.gradle`) release·debug 모두 `minifyEnabled false`라 코드 축소·난독화를 하지 않았다. 그래서 mapping.txt가 생성되지 않으며 올릴 필요도 없다(경고는 정보성).
- **네이티브 기호 파일**: `C:/Users/user/Documents/Projects/CocktailCoffeeTea/Delivery/Android-2026-10-13/native-debug-symbols.zip` — 26,735,669 B, SHA-256 `b3a7191a4021ac454f348b19e957ffb49cae744cca91b0a916b96ca4ecd4d4ce`. Console 해당 버전(vc1)의 App Bundle 탐색기 → 네이티브 디버그 기호 업로드용이다.

  | 항목 | 원본(이 빌드의 스크래치 Gradle `unityLibrary/symbols/arm64-v8a`) | GNU Build ID (= AAB 안 라이브러리) |
  |---|---|---|
  | `arm64-v8a/libil2cpp.so` | 73,955,672 B, .symtab 포함 | `ec01bf66155131f611da1095496bd51716709e49` |
  | `arm64-v8a/libunity.so` | 9,270,864 B, .symtab 포함 | `403b9006d2569719` |
  | `arm64-v8a/libmain.so` | 8,440 B, .symtab 포함 | `c5498ab8c791f1473a54c79a9d3c5b738ff6d941` |

  - 같은 빌드에서 나온 Unity "공개 기호(symbol table)"다. 전체 DWARF(`libil2cpp.dbg.so` 778 MB)는 크기 때문에 넣지 않았다.
  - 소스(il2cppOutput)·BackUpThisFolder 전체·비밀은 포함하지 않았다.
  - zip 안에서 build ID를 다시 읽어 AAB의 각 .so와 일치를 확인했다.
- 기호를 찾지 못한 라이브러리:
  - `lib_burst_generated.so`: Burst 디버그 폴더에 .txt만 있고 .so 기호가 없다.
  - `libgame.so`, `libswappywrapper.so`: Unity 설치본에 이 build ID의 기호가 없다.
  - `libc++_shared.so`: NDK 기본, 기호가 없다.
  - 이 넷은 충돌 보고서에서 함수 이름 없이 표시될 수 있다. 출시를 막지 않는다.
- AAB는 변경하지 않았다(SHA-256 `af546068d11bd7c0e823547b40d38b9a74e061a022b1f879efc03754782259b3` 재확인). 재빌드·코드 변경·업로드는 하지 않았다.
