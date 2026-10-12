# 1차 통합 실행본 r5 — 2026-10-05

Orca task `task_3063cd380512` / dispatch `ctx_56d3efeba120` (Run `run_b69bb3be0912`). Unity 6000.3.25f1(변경 없음). 이전 보고: `first-pass-2026-10-04.md`(r4).
**판정: 출시 아님.** 엄격 `BuildRelease.Windows64`는 여전히 거부(남은 사유 = bartender 1차 임시 아트 하나). r5는 버전 `1.0.0-firstpass` 프리뷰다.

## 1. r4 이후 변경

| 영역 | 파일 | 내용 |
|---|---|---|
| 적 발사 지점 | `Scripts/Enemies/Enemy.cs` | `TryGetActionAnchorWorld(clip, anchor, out world, frame=-1)`: manifest anchorsByFrame(좌하단 px)를 **지금 보이는 프레임** 기준으로 Visual 자식의 발 피벗·배율·좌우 반전을 거쳐 월드로 변환. 없으면 false + 루트 위치. `FaceTowards(x, s)`: 행동 동안 Visual 좌우만 목표 쪽으로 고정(이동·판정 불변). 루트 스케일 1·CircleCollider 반지름 그대로 |
| T/E 투척 | `RangedAttackDriver.cs` | 탄 시작점 = action 프레임 `muzzle`(kettle_cannon·shaker_golem·tea_ceremony_guardian) → 없으면 `emitter`(martini_spirit·milk_foamer) → 없으면 몸 중심(아이콘 몹). 발사 타이밍·간격·탄속·피해 동일, 쏘는 동안 플레이어 쪽을 봄(뒤로 물러나며 쏠 때 총구가 등 뒤에 붙던 문제) |
| S 소환 | `SummonerDriver.cs` | leaf_warden·swirl_stick_conjuror action `summon` anchor에 같은 tint의 작은 summon_ring 시전 표시. **소환 위치·시점은 그대로**(게임 판정 불변) |
| 진상 손님 별점 | `CustomerBossDriver.cs` | 별점 탄 5발 = 발사 순간 보이는 review 프레임의 `reviewOrigin`(휴대폰)에서 발사. 부채 방향은 예고 시작 때 고정한 그대로, 예고 0.8초·피해·슬로우 동일. review_star 예고 그림은 예고 대부분 머무는 마지막 review 프레임의 reviewOrigin에 표시. 예고 동안 플레이어 쪽을 봄 |
| 직업/무기 문구 (r4 GUI QA 지적) | `Scripts/Data/JobPreset.cs` | 08:61~66 그대로: 바텐더 "칵테일 도구를 다루는 전투가", 티 소믈리에 "차의 도구를 다루는 전투가"(옛 "칵테일/차 재료로 성장하는 클래스"는 08이 금지한 숨은 편향처럼 읽혀서 교체). 레이피어/총/검/활 문구도 08 문장으로 |
| 시작 흐름 (r4 GUI QA 지적) | `Scripts/UI/CharacterSelectView.cs` | 08:67: 무기 카드는 **선택만** 하고(선택 카드 강조, 포커스가 영업 시작으로 이동) **"영업 시작"** 버튼으로 확정, **"뒤로"**로 직업 선택에 복귀. 이번 실행 중 지난 직업/무기 기억, 기본값 바텐더/레이피어. ※ 기억은 실행 동안만 유지되고 settings/save에는 저장하지 않는다 |
| 투어·테스트 연동 | `Core/AutoTour.cs`, `Tests/PlayMode/CharacterSelectTests.cs`, `FirstRunTutorialTests.cs` | 무기 선택 뒤 StartButton 클릭 추가(PlayMode는 컴파일만) |
| 빌드 | `Scripts/Editor/BuildRelease.cs` | 프리뷰 배치에 선택 인자 `-cct-preview-out <폴더>` 추가(기본값 `Builds/FirstPassPreview` 그대로). QA가 r4 EXE를 실행 중이라 r5는 `Builds/FirstPassPreview-r5`에 빌드. 엄격 출시 검사 코드는 그대로 |

활 긴 변 기준 길이 보정은 이미 들어가 있었다(`PlayerVisual.AttachWeaponArt`, 알파 영역의 긴 변) — 다시 하지 않았다. 아트 원본·manifest·임포트·.meta 변경 없음, 새 .cs 파일 없음, 이미지 생성 없음.

계약 확인(실제 manifest): difficult_customer review 4프레임 `reviewOrigin`(=phoneScreen), kettle/shaker/tea_ceremony action 4프레임 `muzzle`, martini/milk_foamer `emitter`, leaf_warden/swirl_stick `summon` — 구운 ArtCatalog에 모두 있음. 진상 손님 summon·refund·reject와 중간보스 행동 클립에는 anchor가 없어 해당 없음.

## 2. 테스트 (EditMode 배치, 실제 실행)

| 회차 | 결과 | 파일 |
|---|---|---|
| anchors-r1 | 353/353 Passed (22:52:32~43Z) | `logs/editmode-results-2026-10-05-anchors-r1.xml`, `editmode-log-2026-10-05-anchors-r1.txt` |
| **r5 (최종, 빌드와 같은 코드)** | **354/354 Passed, 실패·skip 0** (2026-10-04 22:56:31~38Z), `error CS` 0(PlayMode 어셈블리 포함 컴파일) | `logs/editmode-results-2026-10-05-r5.xml`, `editmode-log-2026-10-05-r5.txt` |

신규: `Enemy_ActionAnchors_MapThroughFootPivotScaleAndFlip_RootColliderUnchanged`(합성 — px→월드 수식, 현재 프레임 추적, anchor 없는 프레임/클립 대체, 왼쪽 반전, muzzle 우선/emitter 대체/아이콘 몸 중심, 루트 스케일·판정 반지름 불변), `BakedCatalog_EmitterAnchors_CoverEveryActionFrame_ForTheirFiringDrivers`(실제 카탈로그 8종 모든 행동 프레임에 anchor가 있고 캔버스 안에 있음, 담당 드라이버 아키타입 일치), `JobWeaponCopyTests`(08 문구, "재료" 없음). PlayMode는 실행하지 않았다(이 기기 러너 크래시 기록). 영업 시작 흐름은 PlayMode 테스트를 갱신만 했고(미실행), 아래 빌드 투어에서 실제 버튼 경로로 4회 통과했다.

## 3. 빌드·패키지·실행 증거

| 항목 | 값 |
|---|---|
| 엄격 출시 검사 | **거부(정상)** `logs/strict-release-attempt-2026-10-05-r5.txt`, 종료 코드 1: Art/v1에 bartender 없음 + 1차 임시 아트(bartender) 포함 |
| 프리뷰 빌드 | `Builds/FirstPassPreview-r5/CocktailCoffeeTea.exe`, 1.0.0-firstpass, Succeeded·에러 0, 로그 `logs/firstpass-preview-build-2026-10-05-r5.txt`, 상태 파일 `FIRST_PASS_PREVIEW_NOT_RELEASE.txt`(사본 `logs/firstpass-preview-status-r5.txt` — bartender "needs_fix, 1차 임시 허용" 표시). 빌드 후 ProjectSettings bundleVersion 1.0.0 복원 확인 |
| **ZIP** | `Builds/CocktailCoffeeTea-FirstPassPreview-2026-10-05-r5.zip`, **77,634,508 bytes**, SHA-256 **`0351A6768278D57783D7B394A6C15CB79E1C83DA68E331A6D95E3FB388A1D17A`**, 181개 항목(경로 구분자 `/`), `*_BurstDebugInformation_DoNotShip` **제외·검증 0건** |
| EXE / 코드 | `CocktailCoffeeTea.exe` SHA-256 `3070E580E019576B9AFCDEB4FEC18D03CEBC119F6AE84D597412905C749AD653`(Unity 스텁, r4와 동일 — 빌드 식별은 ZIP 해시). `CCT.Runtime.dll` SHA-256 `4FC839202BF569F3B0E56944CA0FDBB050556FB346E5CF48D122B686CB4F3EBC` |
| 압축 해제 실행 | ZIP → `C:\UnityScratch\fp-run-r5\game`(새 폴더), `CocktailCoffeeTea.exe -batchmode -cct-data-dir C:\UnityScratch\fp-run-r5\data -cct-autotour …\shots -logFile …\player.log`, 07:58:02~07:59:55 KST, **종료 코드 0**, 자기 PID만 감시 |
| 투어 결과 | `screens/first-pass-2026-10-05-r5-headless/tour-log.txt`: 4직업·무기 조합 각각 직업→무기→**영업 시작** 실제 버튼 경로, 보스 4종 등장·관찰, 결과·재시작, **exceptions=0**. errors=39는 전부 `Failed to capture screen shot`(창 없는 -batchmode라 캡처 불가 — 조정자 지시로 화면에 보이는 실행 금지). 아트 사용 64종 |
| 저장 격리 | 빈 data 폴더로 시작 → `save.json`(totalRuns=4, failed=1, abandoned=3, cards=7, varieties=14, activeRun=null, saveFailed=False)·`settings.json`이 그 폴더에만 생성(`isolated-save.json` 사본). 사용자 실제 `%USERPROFILE%\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea\save.json`은 2026-09-28 17:58 그대로 |

r4 산출물(`Builds/FirstPassPreview/`, r4 ZIP `C01FB2…DBBD9`)은 그대로 둠 — Codex QA가 r4 EXE를 실행 중. 단, 빌드 코드가 `Delivery/logs/firstpass-preview-status.txt`를 r5 내용으로 덮었다(r4 상태 원문은 `Builds/FirstPassPreview/FIRST_PASS_PREVIEW_NOT_RELEASE.txt`와 r4 ZIP 안에 남아 있음).

## 4. 미검증 / 남은 범위

- **r5 화면 캡처 없음**: 조정자 지시(화면 점유는 Codex QA)로 헤드리스 실행만 했다. 별점 탄이 휴대폰에서 나가는 모습, 총구/emitter 정렬, 소환 시전 표시, 영업 시작/뒤로 버튼 배치(시작 y=130, 뒤로 좌하단)는 **GUI QA 육안 확인 필요**. 투어 관찰 창에 진상 손님 별점 패턴이 걸리지 않았다(review_star 미사용 — r4와 같음).
- 영업 시작 흐름의 키보드 조작(Enter 두 번, Esc 뒤로 없음 — 뒤로는 버튼), 이전 선택 기억이 재실행 후에도 남아야 하는지(현재는 실행 동안만) 미정.
- PlayMode 미실행, 정상 난도 완주·실입력·밸런스 미검증(r4와 동일). 커밋·푸시 없음.

**GUI QA 인계 명령(r5):**
```
<압축 해제 폴더>\CocktailCoffeeTea.exe -cct-data-dir C:\UnityScratch\qa-data-r5
```
