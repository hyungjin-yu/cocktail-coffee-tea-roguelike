# 77 재료 상대 부족 보정 — 구현·검사·빌드 보고 (2026-10-20)

기준: ce00f1e(깨끗한 트리)에서 시작. 범위는 `77_재료 상대 부족 보정_2026-10-20.md`. git index/commit/push 없음.

## 변경 파일 (Unity)

- `Assets/_Project/Scripts/Drinks/DrinkSupply.cs`: 72 절대 보정(`1+0.5·clamp((3−e)/3)`)을 77 후보 중앙값 상대 보정으로 교체.
  - 추첨마다 실제 후보 = 이 획득처 가중치 > 0(희귀 보장 추첨이면 희귀 2종만). e = max(0, 창고+미정산)/기준 사용량.
  - m = 후보 e 중앙값(짝수면 가운데 두 값 평균), c = clamp((m+3)/(e+3), 0.5, 2.0). 후보 1개는 c=1. 확률 = 기본 가중치×c 정규화.
  - 새 공개 API: `Multiplier(e, m)`, `Median(list)`, `Uses(row, held)`, 상수 `MinMultiplier`/`MaxMultiplier`(`CorrectionStrength` 제거). 호출처는 `DrinkLab.RollAward` 한 곳뿐이며 시그니처(`Roll`/`Probabilities`/`Pick`)는 그대로.
  - 바꾸지 않은 것: 묶음 수량, 획득처 가중치 표, 일반 25킬 주기, 추첨 수(일반·정예 1, 중간 3, 최종 6), 희귀 보장 순번, 결정적 난수(`Unit`), 앞 묶음 누적, RP, 저장/정산/재시도 경로(DrinkLab 미수정). 일반 상한 미도입.
- `Assets/_Project/Tests/EditMode/DrinkSupplyTests.cs`: 8 → 16 메서드(1개 교체·8개 추가, `[Values]` 포함 19 케이스). .meta 변경 없음(새 파일 없음).
  - 6회분 vs 24회분 구분, 동일 회분 = 원래 확률(0/6/24/100000, 모든 출처·희귀 보장), 0·int.MaxValue·음수에서 유한·합 1, 중앙값 홀/짝/빈 목록, 상·하한, 희귀 보장은 희귀 2종만 비교, 일반 후보 아닌 희귀는 중앙값 제외.
  - 사용자 잔고(72 원시 단위) 일반 확률을 코디네이터 독립 계산값으로 고정(허용 5e-7): 진 20.1621 · 럼 18.6868 · 홍차 20.7070 · 녹차 16.3013 · 레몬 10.9006 · 꿀 13.2423 %.
  - 앞 묶음 반영: 비포화 fixture(진 0, 나머지 3회분)로 단조 감소 + 실제 `Roll` 경로를 출처 4종×200시드로 재현(각 추첨 = 보상 전 보유+앞 묶음으로 `Pick`).
- `Delivery/DrinkLab-v1.1/Play-DrinkLab-v1.1-supply.cmd`, `-supply-TestMaterials.cmd`: hudfix 바로가기와 같은 격리 저장(`TestData/drinklab-v1.1`)·중복 실행 보호, 새 supply 빌드만 참조. 실행하지 않음. hudfix 바로가기는 그대로.

## 검사

| 검사 | 결과 | 증거 |
|---|---|---|
| Unity EditMode (DrinkSupplyTests·DrinkLabTests·DrinkGameIntegrationTests), 최종 테스트 파일 | 106/106 Passed | tests/edit77b.xml |
| 같은 필터, 첫 테스트판(정확값 고정 전) | 105/105 Passed | tests/edit77a.xml |
| Unity PlayMode (DrinkRunHudTests·DrinkLabPlayTests) | 34/34 Passed | tests/play77a.xml |
| dotnet 순수 하네스 DrinkSupplyTests | 19/19 케이스 pass(16 메서드) | tests/harness-DrinkSupplyTests.txt |

전체 회귀는 다시 돌리지 않았다(변경이 순수 확률 계산 하나이고 DrinkLab·정산·UI 소스는 미변경). 중복 정산·재시도 재추첨 없음·정산 끝난 런 추첨 없음·미정산 반영은 기존 DrinkLabTests/DrinkGameIntegrationTests 항목으로 통과.

## old/new 같은 시드 시뮬레이션 (sim/sim77.md, sim77.csv)

순수 계산 하네스(harness/): new = 현재 DrinkSupply.cs, old = ce00f1e의 DrinkSupply.cs(네임스페이스만 변경). 시나리오 5종 × 조건 3종 × 1000시드, old/new가 같은 runId·awardId(같은 난수). 합성 조건이며 사람의 실제 플레이나 확정된 경제 목표가 아니다. 런 중 소비 없음 → 순획득 = 최종 − 시작.

- 실제 추첨 수/판: 500킬 55(일반20+정예20+중간9+최종6), 2000킬 115, 5000킬 235 — old/new 동일, 77 기준과 일치. 원장 기록 수는 한 보상 안 같은 재료를 합쳐 중간 ≈7.6–8.1, 최종 ≈3.7–4.1건으로 더 적다(수량 손실 아님).
- 단위: 표는 원시 units(0.1mL/0.1g)와 회분을 분리. 합계 units는 액체와 찻잎이 섞여 내지 않고 액체 mL / 찻잎 g을 따로 적었다. ‘회분 합’은 재료별 환산 합일 뿐 만들 수 있는 완성 음료 잔 수가 아니다.
- 총량: old/new 거의 같다(회분 합 ≈ 28.7 / 60 / 123, 빈 창고 액체 385 / 828 / 1722–1726mL). **이번 변경은 종류 편중 보정이며 총량 제한이 아니다. 높은 처치 수의 총수급 문제는 해결되지 않았다.**
- 사용자 제보 잔고의 첫 일반 추첨 확률: old 18/18/18/18/14/14% → new 위 고정값. 희귀 2종은 일반 후보가 아니므로 일반 확률로는 늘지 않는다.
- 편중(일반 6종 최종 회분 CV, 판 평균) old → new:
  - 사용자 잔고 500/2000/5000킬: 0.155→0.134, 0.138→0.105, 0.119→0.080. 일반 최소 회분 평균 18.76→18.99, 23.50→24.19, 32.95→34.16.
  - 홍차만 0(나머지 24): 홍차 순획득 회분 6.22→6.71, 11.68→15.71, 22.50→30.67(P5 18→27). old는 홍차가 6회분을 넘으면 포화로 우대가 사라지고, new는 중앙값까지 계속 우대.
  - 빈 창고·균등 풍부: 거의 변화 없음(동일 회분 = 원래 확률).
- 남은 편중/주의:
  - 전체 8종 CV는 모든 경우 일반 6종 CV보다 크다(예: 사용자 5000킬 0.395 vs 0.080). 희귀 2종은 정예·보스에서만 나와 수급 차이가 유지된다. 사용자 잔고에서 큐라소 순획득은 2.37→3.33회분으로 늘지만 일반 재료(각 ≈16–21회분)와의 격차는 남는다.
  - 한 재료만 0이고 나머지가 모두 6회분 이상인 경우, old는 ×1.5 vs ×0.5(비 3)였고 new 상한은 ×2(비 2)다. 큐라소만 0인 경우 큐라소 순획득 4.07→4.00회분으로 new가 아주 약간 약하다(500킬). 77의 0.5–2.0 범위에서 나오는 결과이며 수치는 바꾸지 않았다.

## 빌드·납품

| 항목 | 값 |
|---|---|
| 빌드 | `Builds/Playable-2026-10-20-drinklab-v1.1-supply` (`BuildRelease.Windows64DrinkLabMvpBatch`, `-cct-preview-out`, exit 0, bundleVersion 1.0.0 복원 확인) |
| ZIP | `Builds/CocktailCoffeeTea-DrinkLab-v1.1-supply-2026-10-20-win64.zip` 106,276,420 bytes |
| SHA256 | `0ED922DDEFC2DA4B21580B6400B3D6B4AFF81206EAE3D8E856FF6DE5CA069D3B` (.sha256 파일 동봉) |
| 코드 반영 | `Managed/CCT.Runtime.dll` 해시가 hudfix와 다르고 `MaxMultiplier` 심볼은 supply에만 있음. 플레이어 exe 스텁 해시는 hudfix와 같음(Unity 공용 스텁, 정상) |
| hudfix 보존 | hudfix ZIP SHA256 작업 전후 모두 `704E5B1E…FB47F`, 폴더 미변경 |
| 새 폴더 격리 실행 | ZIP을 `C:\UnityScratch\r77\fresh-supply`에 풀고 `-batchmode -nographics -cct-data-dir C:\UnityScratch\r77\fresh-supply-data`로 창 없이 실행: 30초 생존, Exception 0, 격리 폴더에 drinks.json 생성, 그 PID만 종료. 사용자 저장·창 사용 없음 |

증거: build/final-supply-output.txt, build/fresh-supply-player.log, build/final-supply.ps1, DrinkSupply.diff.

## 미검증·남은 일

- 사람의 실제 플레이 감각, 판당 목표 제조 횟수, 일반 묶음 상한은 판단하지 않았다(77: 후속 결정).
- 화면/아트 변경 없음, 캡처 순회 없음(77에 따라). 도감 문구 “적게 가진 재료일수록 잘 나옴”은 그대로 둠.
- 창이 있는 게임 실행은 하지 않았다(격리 headless 실행만).
