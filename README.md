# 칵테일 · 커피 · 차

음료 조합으로 성장하는 2D 픽셀 아트 액션 로그라이크입니다. 바텐더와 티 소믈리에가 적을 상대하고, 음료 카드와 직업 연구를 조합하며 영구 강화를 해금합니다.

> 개발 중인 Unity 프로젝트입니다. 저장소의 최신 소스와 배포 실행 파일의 적용 시점은 다를 수 있습니다.

## 주요 기능

- 바텐더 / 티 소믈리에 직업과 레이피어·검·총·활 무기
- 직업별 맵: 바텐더는 칵테일 바, 티 소믈리에는 찻집(Aseprite + PixelLab 제작 배경). 찻집은 칵테일 몹과 카드 없이 차 조합으로 성장하며, 티 전용 일반 몬스터와 보스가 등장합니다
- 칵테일·차 카드 중첩, 바텐더의 커피 연구 / 티 소믈리에의 우림 연구, 영구 강화 및 진행 저장
- 조합 조건으로 해금되는 히든 스킬(바텐더 24종, 티 소믈리에 17종)과 효과 상태 UI
- 진행 시간과 영구 강화 해금 수에 따라 증가하는 몬스터 압박
- 보스 전투, 화상·중독 상태 효과, 몬스터 장판
- 키보드·마우스 이동, 음량·화면·효과 설정
- Android 가로 화면 터치 조이스틱, 연구·히든 버튼, 노치 안전 영역과 백그라운드 일시정지 대응
- 폴드 펼침과 가로 휴대폰 비율에 맞춘 공용 메뉴 배치, 직업별 연구 명칭과 간소화한 HUD. [UI 점검 기록](Delivery/UI_통일성_검증.md)
- 외형만 바꾸는 스킨 미리보기·장착·구매 복원 흐름. 실제 판매는 Google Play 상품과 검증 서버 설정 후 활성화합니다

최근 작업에는 커피 트리에서 Esc로 바로 재개하는 동작, 활 준비 자세, 히든 스킬 발동 피드백, 직업별 특성과 안내 UI가 포함되어 있습니다. 바텐더는 새로운 칵테일 3종마다 12초간 피해 +20% / 공격 속도 +15%를 얻습니다. 티 소믈리에는 가장 많이 중첩한 차의 추가 중첩당 피해 +2% / 초당 회복 +0.1을 얻으며 최대 6단계입니다. 수치는 밸런스 검증을 계속 진행 중입니다.

## 개발 환경과 실행

- Unity **6000.3.25f1**
- Universal Render Pipeline / Unity Input System
- Windows 64비트 빌드
- Android ARM64 / IL2CPP, 목표 API 36. 휴대폰 실기기·실제 결제 검증과 스토어 제출 절차는 별도로 필요합니다

```sh
git clone https://github.com/hyungjin-yu/cocktail-coffee-tea-roguelike.git
```

Unity Hub에서 저장소 루트 폴더를 프로젝트로 추가하고 위 버전의 에디터로 엽니다. 패키지 복원과 임포트가 끝나면 `ProjectSettings/EditorBuildSettings.asset`에 등록된 씬을 열어 Play를 실행합니다.

빌드는 Unity 메뉴 `CCT/Build Windows64 Release`에서 생성합니다. 기본 출력은 `Builds/Windows64/`입니다. 출시 빌드는 아트·데이터·오디오·폰트 검사를 수행합니다. 검증 전용 빌드와 First-Pass Preview는 출시용으로 사용하지 않습니다.

빌드 캐시와 실행 파일 ZIP은 Git에 포함하지 않습니다. 이 저장소에 GitHub Release 배포 파일을 별도로 등록한 것은 아닙니다.

Android 테스트 빌드는 `CCT.Editor.BuildRelease.AndroidApkBatch`와 `AndroidAabBatch`로 생성합니다. 출력은 `Builds/Android/`이며, 현재 빌드 진입점은 디버그 서명을 사용합니다. `NOT-FOR-PLAY-UPLOAD.aab`는 Play 제출용이 아닙니다. 출시 준비와 각 1,000원 스킨 상품 정보는 [Google Play 준비 문서](Delivery/GOOGLE_PLAY_출시준비.md), 서버 설정은 [구매 검증 서버](Server/skin-verifier/README.md)를 참고하세요. Steam 준비는 Google Play 출시 이후로 미뤘습니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| 마우스 이동 모드 | 커서 방향으로 이동 |
| K | 커피 스킬 트리 |
| H | 해금한 히든 스킬과 현재 효과 확인 |
| Esc | 열린 화면 닫기 또는 일시 정지 |

공격은 자동으로 진행됩니다. 설정에서 조작 방식을 변경할 수 있습니다.

## 폴더 구성

| 폴더 | 내용 |
|---|---|
| `Assets/_Project/Scripts/` | 전투, 성장, 저장, UI 구현 |
| `Assets/_Project/Resources/` | 게임 데이터와 런타임 리소스 |
| `Assets/_Project/Tests/` | EditMode / PlayMode 테스트 |
| `ArtSource/` | 픽셀 아트 원본과 제작 자료 |
| `Tools/Art/`, `Tools/Audio/` | 아트·오디오 제작 및 처리 도구 |
| `Tools/QA/` | 빌드와 자동 검증 도구 |
| `Delivery/` | 변경 기록, 검증 보고서, 외부 리소스 고지 |

스프라이트 작업에는 Aseprite와 PixelLab을 사용합니다. 기존 PixelLab 원본을 Aseprite에서 재편집한 작업도 포함됩니다. 리소스 출처와 이용 조건은 [외부 리소스 고지](Delivery/THIRD_PARTY_NOTICES.md)를 확인하세요. 별도 오픈소스 라이선스를 선언하지 않았습니다.

## 테스트와 현재 검증 상태

Unity Test Runner에서 EditMode와 PlayMode 테스트를 실행할 수 있습니다. Windows 자동 실행 예시는 다음과 같습니다. `-Project`와 `-Evidence`는 자신의 경로로 바꿉니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/QA/Run-RcPipeline.ps1 -Steps edit,play -Project "C:/Projects/CocktailCoffeeTea" -Evidence "C:/QA/CCT"
```

2026-10-12 최신 변경을 포함한 회귀 테스트 결과:

- 직업별 맵·몬스터·카드와 검 밸런스(최신): EditMode **436 / 436**, PlayMode **207 / 207** (최종 소스 전체 실행). 직업별 1280×720 / 1920×1080 실제 화면과 정상 플레이 봇 비교는 [보고서](Delivery/Reports/jobmaps-sword-2026-10-12.md)를 참고하세요. 봇 결과는 사람 플레이 체감을 대신하지 않습니다.
- 이전 실행: EditMode **430 / 430 통과**
- PlayMode 전체 실행: **202 / 203 통과**. 방향을 바꾸지 않고 그림만 뒤집었던 테스트 1건을 수정했습니다. 수정 후 관련 테스트 **25 / 25 통과** (활, 히든 효과, 직업 특성, 메뉴 입력). 전체 203개를 수정 후 다시 실행한 결과는 아닙니다.
- 최근 UI·직업·활 수정의 출시 빌드와 ZIP 무결성 확인을 완료했습니다. QA 실행본에서 1280×720 / 1920×1080 화면과 실제 입력 경로를 확인했으며 각 실행의 오류·예외는 0건입니다. [검증 보고서](Delivery/Reports/review-fixes-2026-10-12.md)를 참고하세요.
- 시간별 스폰 증가 변경은 별도 테스트와 빌드 확인을 마쳤습니다. 장시간 자연 플레이 및 전체 밸런스 검증은 별도로 필요합니다.

테스트 수치는 해당 시점의 결과입니다. QA 강제 조건 실행 결과와 일반 플레이 결과는 보고서에서 구분합니다.

최근 HUD 내부 여백과 긴 문구 배치, 히든 목록 드래그 및 휠 스크롤을 개선했습니다. [화면 검증 보고서](Delivery/Reports/hud-padding-scroll-2026-10-12.md)에서 확인할 수 있습니다.

영구 강화창의 긴 조건과 몹 성장 예측도 패널 내부에서 스크롤하도록 개선했습니다. [상세 화면 검증](Delivery/Reports/permanent-detail-scroll-2026-10-12.md)을 참고하세요.

## 저장 데이터

Windows 기본 저장 위치는 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/CocktailCoffeeTea/`입니다.

- `save.json`: 영구 강화, 발견 기록, 누적 진행
- `settings.json`: 음량·화면·조작 설정
- `guest_session.json`: 상위 버전 저장을 보호하는 임시 플레이 데이터

검증 실행 시 `CocktailCoffeeTea.exe -cct-data-dir "C:/QA/isolated-save"`로 저장 경로를 분리할 수 있습니다. 사용자 저장 데이터는 저장소에 포함하지 않습니다.



