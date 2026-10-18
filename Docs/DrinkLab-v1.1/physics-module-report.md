# DrinkLab v1.1 — 음료 물리 축약 모듈 보고서 (2026-10-19)

담당: 물리 모듈 작업자(task_0f23e058935a / ctx_87ce9444159e). 기준: 기획 71 `음료 제조 물리와 시각 검증`, `ArtProduction/beverage-v1.1/physics-reference-cases.json`.

## 산출물

| 파일 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Drinks/DrinkFluidPhysics.cs` | 순수 System C#(UnityEngine 미사용), `namespace CCT`. DTO는 모두 `Fluid` 접두사. |
| `Assets/_Project/Tests/EditMode/DrinkFluidPhysicsTests.cs` | NUnit EditMode 33개 사례(해석 기준 8 + 보존/단조/기울기/빈 잔/넘침/혼합/검증). |
| 이 보고서 | 가정, 축약, 제외한 물리, 실행 증거, 통합 예시. |

기존 파일, `.meta`, ArtSource, UI, 렌더러, DrinkCalculator, 빌드/설정, 기획 문서는 수정하지 않았다. 새 두 `.cs`의 `.meta`는 메인 작업자의 에디터가 가져오기(import)할 때 생성된다. Unity 에디터는 실행하지 않았다.

## 시험 증거(실제 실행)

Unity 밖의 독립 하네스로 실행했다. 하네스는 scratchpad의 `harness.csproj`이며, .NET SDK 10.0.401과 C# `LangVersion 9.0`(Unity 6과 같은 언어 수준)을 쓴다. 두 소스 파일을 그대로 링크하고, Unity 설치본의 `com.unity.ext.nunit` `nunit.framework.dll`을 참조했다. 반사(reflection) 기반 실행기가 `[Test]`/`[TestCase]`를 호출한다.

```
TOTAL pass=33 fail=0
```

Unity Test Runner에서는 **아직 실행하지 않았다**. 메인 작업자가 에디터를 쓰고 있어 실행하지 않았으며, 메인이 EditMode에서 `CCT.Tests.DrinkFluidPhysicsTests`를 실행해야 한다. Mono/Unity 컴파일러 차이로 생길 수 있는 문제는 C# 9 문법(`is > a and < b` 패턴)뿐이며, 이는 Unity 6에서 지원된다.

성능(.NET Release, Mono는 더 느릴 수 있음):

| 호출 | 조건 | 시간 |
|---|---|---|
| `SolveIce` | 160행 잔, 얼음 4개, 17층 | ≈2.2 ms |
| `SolveTilt` | 3층 | ≈1.5 ms |
| `FluidColumn.Step(1/60)` | — | ≈0.015 ms |
| `GetLayers` | — | ≈0.05 ms |

권장: 마스크 행을 64~100행으로 줄이고, 층이나 얼음이 바뀔 때만 `SolveIce`를 다시 호출한다. 붓는 중에는 약 10 Hz로 갱신한다.

## 해석 기준 사례(정확값 일치)

일정 단면 200 mL, 내부 높이 10 cm(단면 20 cm²), 정육면체 얼음 10 mL, 밀도 0.917.

| 사례 | 기대 | 결과 |
|---|---|---|
| uniform_no_ice | 배제 0, 높이 0.5 | 일치(1e-8) |
| uniform_one_cube / two_cubes | 9.17 / 18.34, 0.54585 / 0.5917, 잠김 0.917 | 일치. 떠 있음, 윗면이 수면 위 |
| dense_liquid_one_cube (1.2) | 7.641667, 0.538208 | 일치 |
| light_liquid_cube_sinks (0.8) | 10, 0.55, 바닥까지 가라앉음 | 일치. `Support=Floor` |
| layer_interface_buoyancy | 아래층 2.925 / 위층 7.075, 부력 등가 9.17, 0.55 | 일치(1e-6). 경계에 걸쳐 완전히 잠긴 채 평형 |
| empty_vessel_with_cube | 바닥, 수면 없음, 가짜 액체 없음 | `HasLiquid=false`, `Surface01=0`, 잠김 0, `Floor` |
| pour_conservation | 32.7 / 42.3 / 합 75 | 일치(1e-9), 질량 합도 보존 |

## 모델과 수식

### 1. 재료·혼합 밀도

- `FluidPart{id, volumeMl, density(g/mL), viscosity 0..1, miscibilityGroup}`로 나타낸다. `mass = ρV`이다.
- 혼합 밀도는 `Σm/ΣV`이다. **부피 가산 근사**이며, 실제 에탄올/물 혼합의 체적 수축(최대 약 3~4%)은 무시한다.
- 같은 `id`를 다른 밀도로 다시 등록하면 거부한다. 농도가 다르면 별도 id를 쓴다.
- 대표 물성은 메인이 소유한 `DrinkIngredientDef.density`를 쓴다. 현재 값은 진 0.95(40% ABV 근사), 블루 큐라소 1.08, 그레나딘 1.30, 레몬 1.03, 꿀 1.40이다. 모두 브랜드 실측값이 아닌 게임용 대표값이다. 이 모듈은 재료 값을 정의하지 않는다.

### 2. 잔 형태 C(h)

- `FluidVesselProfile.FromRowWidths(widthsPx 아래→위, capacityMl)`: **회전 대칭 가정**(행 단면 ∝ 폭²)이다. 실제 cm 척도 `s = ∛(4C/(πΣw²))`를 마스크 종횡비에서 유도하므로, 얼음 크기와 잔 크기가 같은 척도를 쓴다.
- `FromCapacityTable(h01[], cumulativeMl[], heightCm)`: 비대칭 용기의 보정표이다. 단조성을 검증한다.
- C(h)는 구간 선형이고 `HeightForVolume`이 정확한 역함수이다. 2D 마스크가 정확한 3D 용량을 보증한다고 주장하지 않는다.

### 3. 얼음 정지 평형(직립 잔)

- 얼음은 정육면체이며 한 변은 `∛V` cm이다. 단면과 높이는 잔의 cm 척도로 정규화한다.
- 액체 가용 누적 부피는 `O(h) = C(h) − Σ 얼음단면 × 겹친높이`이다. 각 층의 경계는 누적 층 부피에서 `O⁻¹`로 구한다. 수면 조건 `C(h) − V_ice_sub(h) = V_liquid`가 이것으로 정확히 성립한다.
- 부력 평형은 `Σ_k ρ_k · V_ice∩layer_k = m_ice`이다. **현재 주변 층의 국소 밀도**를 쓰며, 최종 혼합 밀도를 미리 넣지 않는다(시험 `IceBuoyancyUsesLocalLayerDensityNotFinalMix`). 공기 부력은 무시한다.
- 해법:
  - 열(column)마다 아래→위 PAVA(인접 위반 병합)를 쓴다. 붙은 얼음 더미 하나의 평형을 Illinois 가위치법으로 푼다. 구간을 유지하며, 최대 52회 반복한다.
  - 열 사이는 Gauss–Seidel 외부 반복(최대 200회, |Δz| < 1e-11)으로 푼다.
  - 질량이 부력보다 크면 바닥이나 좁은 벽에 닿고(`Floor`), 아래 얼음 위에 얹히면 `IceBelow`, 아니면 `Floating`이다. 모든 얼음을 위에 고정하는 처리는 없다.
- 접촉 축약:
  - 정육면체는 대각선 ≤ 지름이고 단면 ≥ 얼음 단면인 가장 낮은 높이에 놓인다. V형 잔에서는 꼭짓점이 아니라 벽에 걸린다.
  - 한 단의 열 수는 그 높이대 최소 단면의 0.55(`IcePacking`, 원형 잔 안 사각 배치 근사)로 정한다. 위가 넓은 잔에서도 보수적으로 더 쌓는다. 얼음끼리의 회전, 벽 마찰, 기울어진 얼음 자세는 계산하지 않는다.
- 양 구분:
  - `LiquidVolume`(실제 액체), `SubmergedIceVolume`(배제 부피), `OccupiedBelowSurface = C(Surface01)`.
  - `HeadspaceMl = Capacity − C(Surface01)`, `FreeVolumeMl = Capacity − 액체 − 림 아래 얼음 전체`.
  - `OverflowMl`이 있으면 맨 위층부터 성분 비율을 유지해 제거하고 보고한다. 몰래 줄이지 않는다. 메인은 `OverflowMl > 0`이면 추가를 막아야 한다.
- 빈 잔: 수면이 없고, 얼음은 바닥에 있으며, 배제 부피로 수면을 만들지 않는다.

### 4. 기울어진 잔의 중력 수평 액면

- 국소점 `(x,y)`의 세계 높이는 `−x·sinθ + y·cosθ`이다. 액체는 이 값이 `c` 이하인 회전된 내부 영역이다. `θ>0`이면 림이 +x로 기울고, 입구는 +x 쪽 림이다.
- 3D 부피(회전 대칭)는 각 높이의 원판에서 원 활꼴 면적 `r²acos(d/r) − d√(r²−d²)`을 Gauss–Legendre 3점(총 192 하위 구간 이상)으로 적분한다. `c`는 이분법 56회로 푼다. 회전해도 부피는 보존된다.
- 원통에서 해석해 `c/cosθ = V/C`와 일치한다(0/30/60/−45°, 1e-6). 테이퍼 잔은 독립 3D 격자 적분과 2% 이내로 일치한다.
- 입구 수위 `cosθ − R_top|sinθ|`를 넘는 부피는 `SpillMl`로 보고하고 `LipReached`를 표시한다. 세워진 잔에서는 유출이 0이다. 호출자는 SpillMl을 원천에서 빼고(`Withdraw`) 받는 용기에 더한다(`Pour`).
- `LayerPolygons`/`SurfaceLine`은 국소 단면의 계단형 다각형을 수평 반평면으로 자른 것이다. 렌더러가 국소→세계 회전만 적용한다. 단면 다각형 면적은 그림용이며 3D 부피와 같지 않다.
- **기울어진 상태의 얼음은 풀지 않는다.** 필요하면 직립 해의 `SubmergedIceVolume`을 누적 부피에 더하는 근사를 쓴다(보고 필요).

### 5. 붓기·밀도 불안정·혼합(`FluidColumn`)

부피 좌표 1차원 기둥이다. 아래→위로 64개 동일 부피 셀을 두고 각 셀은 성분별 부피를 가진다. 모든 연산은 성분 부피를 정확히 옮기며, 재표본도 구간 상수 보존 재분배이다. 따라서 성분 부피와 질량이 1e-9 이내로 보존된다.

- **Pour(parts, gentleness01)**: 새 액체는 위에 들어간다. 위쪽 `유입 + 유입×1.5×(1−gentleness)` 부피를 `(1−gentleness)`만큼 섞는 동반 혼합을 적용한다. 빠르고 높은 붓기일수록 경계가 더 섞인다. 프레임마다 작은 양을 넣어도 된다.
- **Step(dt)**: 하위 단계 ≤ 0.02 s, 상한 50000회.
  - 밀도 역전 전복: 위 셀이 더 무거우면 위치별 예산이 `25·√((Δρ/ρ)/0.1)/점성저항` 셀/s로 쌓인다. 예산이 1이 되면 두 셀 덩어리를 교환하고, `0.4/(셀수·저항)`만큼 경계를 섞는다. 무거운 재료는 순간이동하지 않고 시간에 걸쳐 가라앉으며 지나간 자리에 혼합 꼬리를 남긴다.
  - 예: 그레나딘 20 mL를 진 60 mL 위에 부으면 약 2.5 s에 바닥에 도달하고 약 80%가 원액으로 남는다. 꿀(점성 1)은 약 12 s가 걸리고 덜 희석된다.
  - 혼화 확산: 정지 시 0.02 셀쌍/s이다. 실제 분자 확산보다 빠른 게임값으로, 취급 진동과 대류를 포함한다. 안정된 층도 수 분에 걸쳐 흐려지며 영구 띠가 아니다.
  - 비혼화 그룹(기름 등, 현재 재료에는 없음): 확산이 없고, 같은 부피를 맞바꾸어 밀도 순으로 분리된다.
- **Mix(strength01, seconds)**: 젓기 ≈ 0.6, 셰이킹 1.0이다. 셀쌍 확산 20·s와 전역 완화 `1−e^(−1.2·s·dt)`를 쓴다. 셰이킹 6 s면 셀별 조성 편차가 0.01 이하이고, 이후 120 s 정지해도 다시 분리되지 않는다.
- **GetLayers(tol=0.04, fingerSmoothingPasses=3)**: 비슷한 조성의 인접 셀을 층으로 묶는다.
  - 1차원 전복은 침강 중 무거운/가벼운 셀이 번갈아 놓이는 손가락 무늬를 만든다. 실제로는 옆으로 나란한 흐름이다.
  - 그래서 표시·부력용 사본에서만 밀도가 뒤집힌 이웃 쌍을 질량 보존 평균한다. 안정된 경계는 그대로 날카롭다.
- **Withdraw(volume, fromTop)**: 정확히 그 양을 뺀다. 총량을 넘으면 예외를 던진다.
- **DrinkFluidPhysics.Transfer(src, dst, v, gentleness)**: 이동량을 반환한다. 공중 줄기를 추적하려면 반환 배열을 들고 있다가 `Pour`한다(시험의 45/30/12.3 보존).

### 6. 입력 검증

NaN/∞, 음수 부피, 0 이하 밀도, 0..1 밖의 점성·gentleness·strength, 단조가 아닌 용량표, 0 폭 마스크, 얼음 개수(0..64 밖), |각도| > 179°, 감소하는 누적 부피를 거부한다. 모든 반복에는 상한이 있다.

## 제외한 물리(주장하지 않음)

- CFD는 쓰지 않았다. Navier–Stokes, 난류, 표면장력, 메니스커스, 물결, 기포, 셰이커 안 유동은 계산하지 않는다. 혼합은 1차원 게임 계수 모델이다.
- 열역학은 계산하지 않는다. 얼음 융해, 희석수 생성, 온도에 따른 밀도·점성, 증발, 차 우림 화학이 해당한다. 얼음 질량은 제조 시간 동안 고정이다.
- 에탄올/물 체적 수축과 혼합열은 계산하지 않는다(부피 가산).
- 얼음끼리의 회전, 마찰, 벽 접촉력, 기울어진 잔 안의 얼음, 얼음 동역학(튀김·흔들림)은 계산하지 않는다. 정지 평형만 푼다.
- 비대칭 용기의 정확한 3D 형상은 다루지 않는다. 회전 대칭 또는 보정표만 쓴다.
- 계수(25, 0.4, 0.02, 20, 1.2, 4.0, 1.5, 0.55)는 모두 게임 축약값이며 측정값이 아니다. 실제 재생 화면을 보고 조정해야 한다.

## 통합 호출 예시(메인 작업자용)

```csharp
// 재료 → FluidPart (0.1 정수 단위 → mL)
var def = DrinkIngredients.Get(step.id);
var part = new FluidPart(def.id, step.amount / 10.0, def.density, def.viscosity);
var water = new FluidPart("water", recipe.water / 10.0, 1.0);

// 열린 셰이커/믹싱 글라스/서빙 잔마다 FluidColumn 하나
var glass = new FluidColumn();
glass.Pour(part, gentleness01: technique == DrinkTechnique.Layer ? 0.95 : 0.4); // 프레임별 소량 호출 가능
glass.Step(Time.deltaTime);                       // 밀도 침강·확산(호출자가 시간 공급)
glass.Mix(1.0, 6);                                // 셰이킹 (젓기는 0.6)

// 지거 → 셰이커 옮겨 붓기(보존)
var moved = DrinkFluidPhysics.Transfer(jigger, shaker, pourMlThisFrame, 0.5);

// 잔 마스크 → 프로파일(행 폭 px, 아래→위). 잔마다 한 번 만들고 캐시
var vessel = FluidVesselProfile.FromRowWidths(maskRowWidthsPx, 150);
var sol = DrinkFluidPhysics.SolveIce(vessel, glass.GetLayers(), recipe.ice, cubeVolumeMl: 10);
// 층: sol.Layers[k].bottom01..top01 (얼음 공간 제외), 색은 sol.Layers[k].parts로 기존 색 계산
// 얼음: cube.Bottom01, cube.CentreX * heightPx(가로 px), cube.SidePx(heightPx), cube.Support
// 수면: sol.HasLiquid ? sol.Surface01 : 그리지 않음;  여유: sol.HeadspaceMl;  sol.OverflowMl > 0 → 추가 차단 안내

// 기울인 지거/잔: 층 누적 부피로 수평 액면
var cumul = Cumulative(glass.GetLayers());        // 아래→위 누적 mL
var tilt = DrinkFluidPhysics.SolveTilt(jiggerProfile, cumul, angleDeg);
// tilt.LayerPolygons[k] (국소 단면, ×heightPx 후 도구 TransformPoint), tilt.SurfaceLine, tilt.LipReached → 흐름 시작 가능
```

좌표: `h01`/`x`는 내부 높이로 정규화한 등방 단위이다. px 좌표는 `× 내부 높이 px`이며, `FromRowWidths`에서는 행 수와 같다. x=0은 잔 축이다.

## 남은 일

- 메인이 Unity EditMode에서 33개 시험을 실행하고, `.meta` 생성을 확인해야 한다.
- 실제 잔 마스크 폭과 서빙 잔 최종 용량(최대 액체 + 얼음 4개에서도 여유 유지)을 확정해야 한다.
- 계수는 시각 검수(71 §6, 실제 재생 GIF와 시간 로그)로 조정해야 한다.
- 기울어진 잔의 얼음은 직립 근사로 처리하거나 생략하며, 이를 보고해야 한다.
