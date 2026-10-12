# Android (Google Play) 빌드·터치 조작 — 2026-10-12

무료 게임 + 외형 스킨 인앱 결제(결제 클라이언트는 `Docs/Commerce/skin-iap-client.md`, 서버는 `Server/skin-verifier`). 이 문서는 Android 플레이어·터치 조작·빌드 설정과 **아직 안 된 것**을 적는다.

## 빌드 설정 (`BuildRelease.ApplyAndroidPlayerSettings`)

| 항목 | 값 |
|---|---|
| 패키지 | `com.hyungjinyu.cocktailcoffeetea` (잠정) |
| 버전 | 1.0.0 / versionCode 1 |
| 스크립팅 | IL2CPP, ARM64만(arm64-v8a) |
| minSdk / targetSdk | 26 (Android 8.0) / 36 — 로컬 SDK `platforms/android-36`, build-tools 36.0.0, NDK r27c(27.2.12479018), Unity 동봉 OpenJDK |
| 화면 | 가로 전용(좌·우 자동 회전), 세로 금지 |
| 16KB 페이지 | Unity 6000.3.25f1 기본 출력이 16KB 정렬. 빌드 후 `check16k.py`(모든 arm64 .so의 PT_LOAD p_align ≥ 0x4000)와 `zipalign -c -P 16 4`로 확인 |
| 서명 | **사용자 키스토어 미설정 → Unity 기본 디버그 키** |

배치 진입점: `CCT.Editor.BuildRelease.AndroidApkBatch` / `AndroidAabBatch` (`-buildTarget Android`, 출력 폴더는 `-cct-preview-out`, 기본 `Builds/Android`).
산출물 이름에 `debugsigned`를 넣는다:
- `CocktailCoffeeTea-1.0.0-debugsigned-sideload.apk` — 기기에 직접 설치해 시험하는 용도.
- `CocktailCoffeeTea-1.0.0-debugsigned-NOT-FOR-PLAY-UPLOAD.aab` — 번들 구조 확인용. **Play Console 업로드용 아님**(업로드 키로 다시 서명해야 함).

Windows 데스크톱 빌드(`Windows64`)는 그대로(Mono, Standalone 설정 불변). Android 빌드는 프로젝트 Library를 Android로 바꾸지 않도록 별도 복사본(`C:\UnityScratch\cct-android`)에서 만들었다.

## 터치·모바일 동작 (`MobilePlatform.IsTouchUI` — Android 플레이어에서 true, 데스크톱은 기본 false)

- **이동**: 왼쪽 아래 떠 있는 가상 조이스틱(누른 자리에 받침, 반지름 130 캔버스 단위, 데드존 12%). 공격은 원래 자동. 놓으면 정지, 연결된 키보드/게임패드도 동작.
- **버튼**: 오른쪽 아래 `연구 트리`(K와 같은 `RequestCoffeeTree`)·`히든`(H와 같은 `OpenHiddenDetails`), 오른쪽 위 일시정지(터치에서 100 크기). 터치에서는 HUD의 `[K]`/`[H]`/`Esc` 키 안내 문구를 뺀다. 키보드/마우스 전환 버튼은 숨긴다.
- **안전 영역**: HUD 루트에 `SafeAreaFitter`(노치·펀치홀·제스처 바, 회전 시 갱신). 메뉴 화면은 가운데 정렬이라 그대로.
- **뒤로 가기**: Input System이 안드로이드 뒤로 버튼을 Esc로 보낸다. 런 중은 기존 Esc 우선순위(창 닫기 → 일시정지). 메뉴는 `MobileBackRouter`가 맨 위 `Cancel/CloseButton/Close/BackButton/TitleButton`을 누르고, 없으면 타이틀 `종료`(QuitGuard 미저장 확인 포함).
- **수명주기**: 백그라운드 전환(`OnApplicationPause(true)`, 모바일 OS만) 시 전투 중이면 일시정지를 열고 진행·설정을 즉시 저장한다. 런은 포기로 집계하지 않는다. OS가 강제로 끝내면 다음 실행 때 기존 "중단된 런" 복구 경로. 화면 꺼짐 방지(NeverSleep), 60fps 고정(안드로이드 기본 30fps 방지).
- **설정**: 해상도·표시 모드·VSync·조작 방식 줄을 숨기고 나머지를 위로 당긴다. 저장 형식은 그대로(데스크톱 값이 남아 있어도 모바일 화면 어댑터는 해상도를 바꾸지 않는다).
- PC에서 터치 배치 확인: 실행 인자 `-cct-touch-ui`(QA). 자동 화면 검수: QA 투어 빌드 `-cct-autotour-mode touch -cct-touch-ui`.

## 시험

- EditMode `MobileTouchTests`: 조이스틱 수식(데드존·포화·방향), 안전 영역 앵커, 모바일 설정 줄 숨김/재배치(데스크톱 불변), 모바일 화면 어댑터 60fps·해상도 불변, 터치 안내 문구, Android 플레이어 설정(IL2CPP·ARM64·API 36·가로·패키지, 데스크톱 백엔드 불변).
- PlayMode `MobileTouchPlayTests`: 데스크톱엔 터치 UI 없음, 조이스틱 포인터 이벤트로 실제 이동/놓으면 정지/다른 손가락 무시, 연구·히든 버튼, 백그라운드 전환 일시정지·조이스틱 해제·런 유지, 안전 영역 적용, 뒤로 가기(런 중 위임·도감 닫기).

## 아직 안 된 것 / 사용자가 할 일

1. **실기기 시험 없음** — 이 PC에 연결된 Android 기기가 없다(`adb devices` 빈 목록). 에뮬레이터도 내려받지 않았다. 터치 배치는 PC 창(1600×720, 1280×720)에서 `-cct-touch-ui`로만 확인했다. 실제 기기에서 설치·멀티터치·노치·뒤로 제스처·백그라운드 복귀·발열/프레임을 확인해야 한다(APK를 기기에 설치: `adb install -r <apk>` 또는 파일 전송 후 "알 수 없는 앱 설치" 허용).
2. **업로드 키스토어** — 사용자가 만들어 안전하게 보관(저장소에 넣지 않음, `.gitignore`에 `*.keystore *.jks *.p12 keystore.properties`). Play App Signing 사용 권장. 키스토어를 Unity Publishing Settings에 지정한 뒤 AAB를 다시 빌드.
3. **Play Console** — 앱 생성, 패키지명 확정(현재 잠정), 개인 개발자 계정의 비공개 테스트(테스터 12명·14일) 요건, 스토어 등록정보·데이터 보안·콘텐츠 등급, 인앱 상품 `skin_bartender_midnight`/`skin_tea_jade`(각 1,000원, 비소모성) 생성, 라이선스 키. 체크리스트는 `Delivery/GOOGLE_PLAY_출시준비.md`(root).
4. **결제 활성화** — `Resources/Commerce/commerce_config.json`의 `enabled=false`, 검증 서버(https) 배포 전에는 구매 불가가 정상.
5. 아이콘·스플래시·앱 이름 현지화(현재 기본 Unity 아이콘), 개인정보처리방침 URL.
