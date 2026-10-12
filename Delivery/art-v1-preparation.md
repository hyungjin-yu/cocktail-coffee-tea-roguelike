# Art v1 임포터·매니페스트 검수 기반 준비 보고

- 작성: 2026-09-23, Orca 워커 task_2bc86a8b3c63 / ctx_03178fec58e8
- 기준: 기획 04(§5~§9), 09(§1~§2), 11(§2 소유권), 선행 감사 `Delivery/orchestration-art-gap-review.md`
- 범위: **새 파일만 추가**. 기존 파일(IconImportSettings, Enemy, PlayerController, 씬, 프리팹, Resources, asmdef, .meta)은 수정하지 않았다(`git diff --stat` 빈 결과). 커밋/푸시 없음.
- 아트 적용 없음: needs_fix(바텐더 23프레임 원본, 레몬·티 소믈리에 기준원본, 그라인더 후보 2장)는 가져오지 않았고 `Assets/_Project/Art/v1` 폴더도 만들지 않았다.
- Unity를 실행하지 않았다. 사용자 Unity 세션과 프로세스는 건드리지 않았다.

## 1. 추가한 파일

`Assets/_Project/Scripts/Editor/` (기존 `CCT.Editor` 어셈블리에 포함)

| 파일 | 역할 | Unity API |
|---|---|---|
| `ArtV1Spec.cs` | 04/09 고정 수치 표: 몸체 17종(H, 세트, 클립·프레임 수·fps·loop, 256²/512², 접지선 32/64, 안전 범위), VFX·UI·배경 49개(크기·프레임 수), 128 PPU, 몸체 피벗 (0.5, 0.125), `VisualScale = H / (bodyHeightPx / 128)` | 없음 |
| `ArtV1Json.cs` | manifest용 최소 JSON 파서 (JsonUtility가 이름→클립 사전을 못 읽어서) | 없음 |
| `ArtV1Png.cs` | PNG 헤더·색 형식·tRNS 읽기 + RGBA8 디코딩(필터 0~4, 8/16비트, 팔레트). 원본에 알파 채널이 **있었는지** 구분하려고 Texture2D 대신 직접 디코딩 | 없음 |
| `ArtV1Manifest.cs` | 04 §7 필드(specVersion, assetId, status, frameSizePx, pixelsPerUnit, pivotNormalized, targetBodyHeightWorld, bodyHeightPx, clips, anchorsByFrame, deviations, sourceReferences) + 선택 필드(kind, scope, files, checks, spriteBorderPx) 파싱. 형식 오류는 수집 | 없음 |
| `ArtV1Validator.cs` | 폴더 단위 검수 → `ArtV1Report`(이슈 목록 + 에셋 요약 + 마크다운). 읽기 전용 | 없음 |
| `ArtV1ImportRules.cs` | 임포트 결정 규칙(순수 함수): 적용 경로, PPU, 피벗 출처, 9-slice, ready 여부 | 없음 |
| `ArtV1ImportSettings.cs` | `AssetPostprocessor`. **`Assets/_Project/Art/v1/` 아래 PNG에만** 적용: Single Sprite, 128 PPU, Point, mipmap off, 무압축, 입력 알파 유지(alphaIsTransparency), FullRect, Clamp, NPOT 없음. 피벗은 ImportRules 결과. manifest.json을 의존성으로 등록 | 있음 |
| `ArtV1ValidationMenu.cs` | 메뉴 `CCT/Art v1/Validate ArtSource v1 (report only)`, `CCT/Art v1/Validate Assets Art v1 (import rules)` → `Logs/ArtV1Validation-<mode>.md` 작성 + 콘솔 요약. 에셋을 복사·수정하지 않음 | 있음 |

`Assets/_Project/Tests/EditMode/ArtV1/` (새 asmdef `ArtV1Tests.asmdef`, 어셈블리 이름 `CCT.Tests.ArtV1`, 참조 CCT.Editor + TestRunner + nunit, Editor 전용, `UNITY_INCLUDE_TESTS`)
기존 `CCT.Tests.EditMode` asmdef가 CCT.Editor를 참조하지 않아서 기존 asmdef를 수정하지 않고 별도 어셈블리로 분리했다.

| 파일 | 내용 |
|---|---|
| `ArtV1TestPng.cs` | 테스트용 PNG 인코더(RGBA/RGB, 필터 0~4) + 좌하단 원점 이미지 빌더(몸체, 그림으로 넣은 바둑판) |
| `ArtV1TestAssets.cs` | 04 계약을 모두 지키는 ready 에셋 폴더를 시스템 임시 폴더에 생성, 테스트마다 한 가지씩 깨뜨림 |
| `ArtV1ParsingTests.cs` | JSON·manifest·PNG 필터 디코딩·알파 좌표·표 총량(17종, 321/329, 그라인더 21, 바텐더 23, 레몬 브루트 포함, 커피 일반 몹 미포함)·visualScale 공식 |
| `ArtV1ValidatorTests.cs` | 아래 §2 검수 항목별 테스트 |
| `ArtV1ImportRulesTests.cs` | 기존 경로 비적용, 몸체 피벗 강제, VFX/UI 매니페스트 피벗, 피벗 누락 시 중앙+에러, UI 9-slice, needs_fix/매니페스트 누락 에러 |

## 2. 검수 규칙 (에러=적용 불가, 경고=사람 판단, 정보=기록)

- **status**: ready/needs_fix 외 값은 에러. needs_fix는 Import 모드(Art/v1)에서 에러, Source 모드(ArtSource)에서 정보. "적용 가능"은 ready이면서 에러 0일 때만 표시한다.
- **헤더**: specVersion "1.0", assetId = 폴더 이름, pixelsPerUnit 128, frameSizePx(몸체 256²/보스 512², VFX·UI는 표 크기).
- **피벗**: 몸체 [0.5, 0.125]만 허용. VFX/UI/배경은 매니페스트에 반드시 적게 하고, 몸체 피벗을 강제하지 않는다.
- **몸체**: targetBodyHeightWorld = 04 표 H, bodyHeightPx 필수, idle_00을 실측(접지선부터 알파>16인 최고 픽셀까지)해서 ±2px 넘게 다르면 에러. 클립 누락과 프레임 수 불일치는 에러, fps·loop 차이는 경고. 파일 이름 규칙(`frames/<id>_<clip>_right_NN.png`)은 경고, `_left_` 파일은 에러. 플레이어(A)는 프레임마다 anchor가 없으면 에러, anchor가 캔버스 밖이면 에러.
- **파일**: 매니페스트에 있는데 파일이 없으면 에러, 실제 PNG 크기가 매니페스트와 다르면 에러, 참조되지 않은 PNG는 경고(`reference/`, `preview.png`는 제외). Import 모드에서 preview.png가 있으면 경고.
- **알파/배경** (배경 타일은 제외):
  - 알파 채널/tRNS 없음 → 에러
  - 투명 픽셀 0개 → 에러 (UI는 경고)
  - 완전히 빈 프레임 → 에러
  - 가장자리 90% 이상 불투명 → 경고
  - 그중 60% 이상이 흰색/밝은 회색 → "그림으로 넣은 배경일 가능성" 경고
  - 안전 범위를 벗어났거나 접지선 아래에 픽셀이 있으면 → 경고
- **한계(명시)**: 알파만으로는 픽셀로 그려 넣은 바둑판을 **확실하게** 잡을 수 없다. 투명한 캔버스 안쪽 본체 영역에 바둑판이 그려진 경우는 검출되지 않으며, 이 한계를 테스트(`CheckerboardPaintedOnlyInsideTransparentCanvas_IsNotDetected_KnownLimitation`)로 고정했다. 바둑판 관련 문구는 모두 "가능성, 눈으로 확인"으로 낸다.
- **범위 미정**: espresso_bug/milk_foamer는 에러 대신 `ScopeUndecided` 경고를 내고 형식만 검사한다(04 §2에서 제외, 사용자 추가 — 코디네이터 결정 대기).
- **루트 요약**: 04/09 표에는 있는데 폴더가 없는 ID를 정보로 나열한다(레몬 브루트 포함).

## 3. 검증 근거 — 실제로 실행한 것과 실행하지 않은 것

### 실행함 (Unity Editor 밖, 재현 가능)

1. **컴파일 (Unity 6000.3.16f1 번들 Roslyn)**: `Editor/Data/NetCoreRuntime/dotnet.exe Editor/Data/DotNetSdkRoslyn/csc.dll`, LangVersion 9, `CCT.Editor.csproj`/`CCT.Tests.EditMode.csproj`의 DefineConstants와 HintPath 참조 전체, `Library/ScriptAssemblies/CCT.Runtime.dll`.
   - 새 Editor 파일 8개만: 에러 0, 경고 0
   - 새 Editor 파일 8개 + 기존 Editor 파일 2개(`IconImportSettings.cs`, `DefaultCardsCreator.cs`)를 한 어셈블리로: 에러 0, 경고 0 → 이름 충돌 없음
   - 테스트 파일 5개(위 Editor DLL + Unity `com.unity.ext.nunit` 참조): 에러 0, 경고 0
2. **테스트 실행 — 독립 하네스 (Unity Test Runner 아님)**: .NET 10 콘솔에 순수 C# 소스 6개(Json/Png/Spec/Manifest/Validator/ImportRules)와 테스트 소스를 함께 컴파일하고, Unity nunit.framework.dll을 참조해 `[Test]`/`[TestCase]`/`[SetUp]`/`[TearDown]`을 리플렉션으로 호출했다. 최종 결과 **62 통과 / 0 실패**.
   - 실행하지 않은 부분: `ArtV1ImportSettings`/`ArtV1ValidationMenu`(UnityEditor API)는 컴파일만 확인했고, 하네스에서는 호출하지 않았다.
   - 변이 확인(테스트가 실제로 결함을 잡는지):
     - 불투명 검사 제거, 몸체 피벗 0.5로 변경, needs_fix를 경고로 낮춤, Paeth 동점 처리 변경 → 각각 해당 테스트 1개 실패. 원복 후 전체 통과.
     - 첫 Paeth 변이(`pa<=pb`→`pa<pb`)는 수학적으로 동치라 잡히지 않는 것이 정상이었다.
3. **실제 파일에 읽기 전용 실행**:
   - `ArtSource/v1` (Source 모드)
     - 결과: 에러 0 / 경고 2
     - bartender: manifest 없음, 기대 23프레임, 적용 불가
     - melee_thrust_prototype: manifest 없음
     - 04/09 몸체 16종과 flat 49개 폴더 없음 → 정보
   - 현재 `Resources/Icons/Enemies/grinder_golem.png`를 **임시 폴더에 복사**하고 가상의 ready manifest로 감싸 Import 모드로 실행:
     - 에러: NoTransparentPixels, ImageSizeMismatch(128×128 대 512×512), ClipMissing ×5, FrameCountMismatch
     - 경고: OpaqueCanvasBorder(가장자리 100%), PossiblePaintedBackground(흰/회색 100%)
     - 원본 파일은 읽기만 했다.
   - 이 실행에서 버그 1건을 발견해 수정했다: 캔버스 크기가 틀린 프레임에도 접지선·높이 검사가 돌아서 엉터리 수치가 나왔다. 이제 크기 에러만 남긴다. 회귀 테스트 `WrongSizedIdle00_ReportsOnlySize_NotBogusGeometry`를 추가했다.

### 실행하지 않음 (미검증으로 남김)

- **Unity Test Runner에서 `CCT.Tests.ArtV1` 실행**: 사용자 Unity 세션과 겹치지 않도록 실행하지 않았다. 새 asmdef가 Unity에서 정상 인식되는지도 확인하지 않았다.
- **실제 TextureImporter 적용 결과** (PPU, Point, mip, 피벗이 .meta에 기록되는지): `Art/v1`에 ready 에셋이 없고, Editor를 실행할 수 없어서 확인하지 못했다.
- **Unity 안에서의 메뉴 동작**.
- 사용자 Editor가 새 파일을 감지하면 자동으로 다시 컴파일하고 .meta를 생성한다. 이 결과는 확인하지 않았다(외부 Roslyn 컴파일은 통과).

## 4. Unity 안에서 확인할 순서 (다음 세션이 Editor를 쓸 수 있을 때)

1. Editor 콘솔에 컴파일 에러가 없는지 확인한다. Test Runner EditMode에서 `CCT.Tests.ArtV1` 62개를 실행하고, 기존 `CCT.Tests.EditMode`의 회귀 여부도 본다.
2. `CCT/Art v1/Validate ArtSource v1` 실행 → `Logs/ArtV1Validation-Source.md`.
3. 첫 ready 에셋이 생기면 그 폴더만 `Assets/_Project/Art/v1/<id>/`에 복사한다(frames + manifest.json, preview 제외). 그다음 Inspector에서 Sprite/Single/128/Point/No mip/None 압축/Custom 피벗 (0.5, 0.125)을 확인하고, `Validate Assets Art v1` 에러 0을 확인한다.
4. 규칙을 바꾸면 `ArtV1ImportSettings.GetVersion()`을 올린다.

## 5. 남은 것 / 블로커

- ready 아트 0개: 임포터 실제 적용 검증은 아트가 들어올 때까지 보류.
- 커피 일반 몹 범위, 레몬 브루트 원본 부재 등은 선행 감사 보고서의 결정 사항 그대로다.
- 런타임 적용(Visual 배율 공식 연결, 플레이어 Visual 분리, 애니메이션 재생기)은 이번 범위가 아니며 시작하지 않았다(사용자 지시: 현재 작업까지만).
