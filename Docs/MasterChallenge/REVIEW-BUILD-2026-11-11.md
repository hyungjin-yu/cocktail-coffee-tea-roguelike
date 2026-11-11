# 마스터 도전 검수 빌드 안내 (2026-11-11)

**Steam 업로드/공개 배포용 아님.** 내부 검수 전용 Windows 64비트 빌드.

- 위치: `Builds/SteamMaster-Review-2026-11-11/CocktailCoffeeTea.exe` (Release 설정, 544MB, 빌드 13:33 KST)
- 빌드 방법: `CCT.Editor.BuildRelease.Windows64ReleaseToBatch -cct-preview-out Builds/SteamMaster-Review-2026-11-11` (strict 검사 포함, 기존 `Builds/Windows64`·공모전 빌드 미변경)
- 소스: HEAD `38c2d5ac` + 미커밋 작업(아래 해시). 빌드 전후 소스 목록 동일.

## 원본 세이브 격리 (반드시)

이 빌드는 기본적으로 실제 사용자 저장 위치(productName `CocktailCoffeeTea`)를 쓴다. 검수할 때는 **항상 빈 폴더를 지정해서** 실행한다:

```
CocktailCoffeeTea.exe -cct-data-dir "C:\CCT-Review\save-2026-11-11"
```

`-cct-data-dir`는 save.json·settings.json을 그 폴더에서만 읽고 쓰며, drinks.json(음료 창고·런 제조 잔·v5 스키마)과 최근 기록도 같은 폴더를 따른다(코드 확인: `LaunchOptions`, `DrinkLab.ActivePath`). 스킨·상점 상태 저장도 같은 폴더를 따른다(2026-11-11 정적 확인: `WeaponSkins`·`CommerceState`가 `ActiveSavePath` 폴더 사용).

## 검수 순서 (마스터 도전)

1. 시작 → 캐릭터 선택에서 모드를 "마스터 도전"으로 바꾸고 출발.
2. 재료 모으기 1~5 진행. 우상단 "음료 만들기 [B]"로 제조 창(전투 정지) → 커피를 만들어 품질 효과(오른쪽 위 슬롯) 확인 → 몇 잔은 "제출", 한 잔은 "마시기".
3. 5/5 후 상단 선택 패널: "재료 모으기 반복"을 한 번 눌러 1~5가 같은 주기로 다시 도는지 확인 → 다시 5/5에서 "박람회 도전".
4. 남은 손님이 있으면 먼저 처리 안내 → 모두 처리되면 **3.5초 현수막 설치**: NPC 둘이 설치 위치 가까이에서 나타나 걸어오고(0–1.0초), 무대 벽의 카운터 사이 빈 칸(월드 고정)에 현수막을 줄로 당겨 펼치고(1.25–2.25초, 2.1초부터 "박람회" 글자), 걸어가며 사라짐(2.5–3.5초). 이 동안 피해·생성·시간 정지.
5. 박람회 3웨이브(작은 진상 손님만, 천 글자 "박람회 n/3") → "박람회 성공!"(재료 기본 묶음 지급) → 공격하지 않는 보상 손님 2웨이브(접촉해도 HP 그대로) → 다음 주기 1웨이브에 직전 제출 약화율 적용.
6. 설정의 효과 항목을 "낮음 (장식 50%)"로 바꾸고 품질·상성 효과가 low 버전으로 바뀌는지.

## 보고 형식

문제마다: 단계 번호, 화면 해상도, 기대/실제, 스크린샷. 특히 확인: NPC 발이 미끄러져 보이는지, 펼침 줄이 보이는지/어색한지, 품질 효과 크기, 갈색 적 위 원두 효과 가독성.

## 해시 (2026-11-11 15:45 재빌드 — 현수막 월드 고정 수정 반영)

- 빌드 입력 소스 2,803 파일 목록 sha256 `fbbb3d11a48a5f82106a585aee9126f36d9fa6bcc10f7717728cb6e1eb2adb06` — 빌드 전(`source-manifest-build3-before.sha256`) = 빌드 후(`source-manifest-build3-after.sha256`).
- ProjectSettings.asset 빌드 전후 `5900a05320859108adc3530f1b41a3290510db78d00c59a7b0498ad7e11bda8c`.
- 산출물 208 파일 목록 `build-review3-files.sha256`, 목록 sha256 `c9935d0c933ab091c7b32fb64faa9252e0a193ea7654cdb03f7b755980273616`. `CocktailCoffeeTea.exe` `3070e580e019576b9afcdeb4fec18d03cebc119f6ae84d597412905c749ad653`, `CCT.Runtime.dll` `7a896cbdbd7ead277896074154b68854ac1889fb3bc6c11bd3fadfa02440c84d`.
- 실행 예: `CocktailCoffeeTea.exe -cct-data-dir "C:\CCT-Review\save" -logFile "C:\CCT-Review\save\Player.log"` — `-logFile`로 Unity Player.log도 사용자 폴더 대신 격리 폴더에 쓴다. 스킨·상점 상태도 데이터 폴더를 따른다(정적 확인).
- 이전 화면 고정 현수막 빌드: `Builds/SteamMaster-Review-2026-11-11-prefix-screenfixed/`(비교용, 검수 대상 아님).
- `CocktailCoffeeTea_BurstDebugInformation_DoNotShip/`는 배포물에 넣지 않는다.
