# v1.1 hudfix — 준비 음료 HUD 칩 · 결과 화면 재료 목록 (2026-10-19, task_cc780f4250cd / ctx_7901f9784fe0)

상태: 구현 + 대상 회귀 + 실제 빌드 캡처 완료 / **root 최종 시각 판정 대기**. 커밋·push·index 변경 없음. 사용자 실행 중 게임(PID 43288, 옛 빌드·실제 MVP TestData)과 그 저장은 건드리지 않았다(모든 실행은 r73 격리 폴더).

## 1. 준비 음료 칩(사용자 스크린샷: 음료 지식 안내 1/4 위에 칩, 오른쪽 잘림)
원인(실측):
- 칩은 `GameManager`에서 카드 선택·안내·일시정지·결과 창보다 **나중에** 만들어져 모든 모달 위에 그려졌고, 표시 조건이 "준비 음료가 있으면 항상"이었다(`before/*/H2~H6`: chipShown=True, chipAboveCardView=True).
- 글 상자 높이 고정(제목30/효과28/상태26) — 긴 이름·여러 효과는 상자보다 큰 높이가 필요(1080: 제목 64>30, 효과 55>28)해 줄이 겹치고 칩 밖으로 넘침(`before/hb1080/H2`).
- `UiFactory.Place`는 이미 피벗=앵커(1,1)이라 칩 사각형 자체는 1280×720·1920×1080·836×705 모두 화면 안이었다. 사용자 스크린샷의 오른쪽 끝 잘림은 1622폭 이미지가 실제 게임 화면의 일부(패널 1580 논리폭이 ≈1570px로 보임 → 배율≈1, 화면이 더 넓음)로 보이며, 정확히 재현하지는 못했다. 1622×935 창 캡처(`after-hud/ha1622`, `after-result/hr1622`)에서 칩 px=(1174,623)-(1597,769), 모든 글자 화면 안.

수정(`Scripts/Drinks/DrinkRunHud.cs`, `Scripts/Core/GameManager.cs`):
- `DrinkRunHud.CombatVisible`: State=Playing·비일시정지이고 CardView(열림/IsLearningOpen/IsTutorialOpen)·PauseView(열림/IsTutorialOpen)·SkillView·HybridView·ComboView가 모두 닫혔을 때만 칩·알림 표시. 전투 복귀 다음 프레임에 바로 다시 표시. 음료·버프·5킬 의미는 표시와 무관(그대로).
- DrinkHud 층을 HUD 바로 다음(모든 모달 뒤)으로 옮김.
- 재료 알림은 전투 화면에서만 보이고 시간도 전투에서만 흐름(모달 동안 보류 → 복귀 후 남은 시간 표시, 결과 화면에서는 0).
- 칩: 폭 500 고정·오른쪽 위 기준, 제목/효과/상태 높이를 실제 preferredHeight로 다시 잼, 효과는 한 효과("공격 속도 +3.5%")가 줄 사이에서 끊기지 않게 줄 단위로 묶음. 글자 크기 22/20/20(줄이기 없음). 좁은 창은 칩만 키워 실제 글자 ≥12px(`MinGlyphPx`, 836×705에서 ×1.38 → 12.0px). 터치는 HUD와 같은 SafeAreaFitter.
- 알림 문구의 재료도 같은 재료끼리 합침.

증거(실제 QA 빌드, 격리 저장, 실제 준비 음료 → 새 설정 첫 카드 제안의 음료 지식 안내 1/4):
- 전: `before/hb720|hb1080|hb836` H1~H8 + report.json.
- 후: `after-hud/ha720|ha1080|ha836|ha1622|ha720t(터치)` — H2 안내·H3 상세·H4 카드 선택·H6 일시정지 chipShown=False, H1·H5·H7·H8 전투 True, 모든 단계 ALL_OK=True(칩 화면 안·모든 글자 칩 안·preferredHeight ≤ 상자). 실제 글자 px(유효 배율 포함): 720 14.7/13.3, 1080 22/20, 836 13.2/12.0, 1622 18.6/16.9 (`after-result/*` H노트, `natural-name/hn720`).
- 자연스러운 이름 캡처: `natural-name/hn720`("블루 큐라소 진 스터링"). 공백 없는 16자 "한밤의블루큐라소진레몬셰이킹특제"는 우리가 넣은 스트레스 고정값.

## 2. 결과 화면 재료 목록(사용자: 수백 줄 재료가 결과 화면 전체를 덮음)
원인: `ResultView.DrinkStatusLine`이 정산 기록(출처 묶음 단위) 전체를 `DrinkLab.Describe`로 그대로 이어 붙이고, 1500×80 칸에 세로 넘침 허용 → 긴 판에서 화면 전체를 덮음.

수정(`Scripts/UI/ResultView.cs`, 새 `Scripts/UI/DrinkAwardDetailView.cs`, `Scripts/Drinks/DrinkLab.cs` Totals/Summarize):
- 표시 전용 합계 `DrinkLab.Totals`(long, checked)·`Summarize`(재료표 순서, 빠지는 재료 없음). 정산 기록·창고·RP는 변경 없음.
- 결과 화면 통계 판(-142)과 버튼(-226) 사이에는 **한 줄 요약**(글자 26, 720에서 17.3px; 예: "음료 재료 8종 획득 · 창고에 저장했습니다 · 연구 포인트 +11 · 준비 음료 사용됨") + **"획득 내역" 버튼**(280×64)만. 시간·스페셜티·통계·버튼 위치는 그대로.
- "획득 내역" 스크롤 창: 재료별 전체 합계(8종 전부)·획득 기록 건수("획득 기록 N건을 재료별로 합친 양")·연구 포인트(현재값)·준비 음료 상태, 본문 28. 좁은 창은 실제 캔버스 배율로 창만 키워 본문 18px 목표(836×705: 창 788px·여백 24px·×1.476 → 최소 18.0px), 닫기 화면 안. 불투명 차단판(뒤 버튼 안 눌림), Esc/닫기/안드로이드 뒤로(Close 버튼).
- 이전 시도(84px 칸 글자20+재료 줄이기 "외 n종")는 root 반려로 폐기.

증거(실제 QA 빌드, 일반 5000·정예 20·중간 보스·최종 보스 → 영업 완료; **합성 스트레스 고정값: 실제 추첨 229묶음 = 정산 기록 226~227건, 00:03 — 실제 20분 사람 플레이 아님**):
- `after-result/hr720|hr1080|hr1622|hr720t`(R1 결과, R2/R3 획득 내역 위/아래, R4 닫은 뒤) + `hr836b`(좁은 창 맞춤 후). RESULTGEOM: 요약 1줄·칸 안, 통계 판·모든 버튼과 겹침 없음, 다시 시작/영구 강화/타이틀/이번 판 빌드/획득 내역 가운데 실제 레이캐스트 = 자기 자신. 획득 내역 실제 클릭으로 열림 rows=8/8, 닫기 클릭 후 결과 화면 복귀. 창고 차이 = 합계(8종 모두, 5개 해상도 불일치 0). Exception 0.
- **정정(후속 ctx_e1a286851587)**: `DrinkLab.RollAward`(559~562행)는 한 보상 안에서 같은 재료 추첨을 한 줄로 합친다. 그래서 226~227은 **정산 기록 줄 수**(`items.Count`)이지 실제 추첨 묶음 수가 아니다. 실제 추첨 수 = 일반 200 · 정예 20 · 중간 보스 3(`MidBossDraws`) · 최종 보스 6(`FinalBossDraws`) = **229**. 앞서 적은 "중간 보스 2~3 · 최종 보스 3~4"는 보상별로 합쳐진 재료 줄 수다. 획득 내역 창 문구도 "받은 묶음 N개" → "획득 기록 N건을 재료별로 합친 양"으로 고쳤다(보상·경제·합계 계산 변경 없음).
- hr720 재료 합계/실제 추첨 묶음 수(합계÷묶음 크기, 합 229): 진 645.0mL/43 · 럼 540.0mL/36 · 블루 큐라소 20.0mL/4 · 그레나딘 40.0mL/8 · 홍차 48.0g/48 · 녹차 30.0g/30 · 레몬즙 330.0mL/33 · 꿀 135.0mL/27 (각 캡처 report.json `RESULT <id>` 줄).
- 좁은 창 결과 화면의 다른 기존 글자(시간·통계 등)는 전체 폭 맞춤 0.435배 그대로 — 범위 밖(root 확인).

## 3. 이름 띄어쓰기(사용자 질문)
생산 코드는 이미 띄어 쓴다: `DrinkLab.DefaultName` = 첫 재료 이름 + 공백 + 기법, `SanitizeName`은 가운데 공백 유지(앞뒤만 정리). 새 시험으로 기본 이름("블루 큐라소 스터링")·이름 없이 제조·사용자 입력("진 레몬 셰이킹") 저장→다시 읽기·공유 코드 왕복·HUD 칩 제목까지 띄어쓰기 유지 확인. 생산 코드 변경 없음.

## 4. 시험(최종 소스)
- EditMode 대상 `tests/edit_final_hud.xml` 131/131 (DrinkGameIntegration 18 · DrinkLab 68 · DrinkResearch 6 · DrinkSupply 8 · RunHistory 31) + 이름 시험 추가 후 `tests/edit_name.xml` DrinkGameIntegration 19/19.
- PlayMode 대상 `tests/play_final_hud.xml` 38/38 (DrinkRunHud 4 · CardEducationGuide 2 · CardEducationPlay 3 · ResultView 2 · RunHistoryPlay 6 · Pause 10 · KeyboardNavigation 11).
- 새 시험: `DrinkRunHudTests`(안내 1/4·상세·카드 선택·일시정지·조작 안내·결과에서 칩/알림 숨김 + 복귀 즉시 표시 + 음료/버프/0킬 보존 의미, 긴 이름 4효과 실제 화면 사각형·글자 정점 경계·preferredHeight·효과 줄 끊김 없음·실제 px, 긴 클리어 결과 요약 칸·겹침·버튼 레이캐스트·획득 내역 실제 포인터 클릭·8행·본문 px·창 화면 안·뒤 버튼 차단/복귀), `DrinkGameIntegrationTests`(240개 기록 합계·재료표 순서·long 합계, 정산 기록 227건(실제 추첨 229) 클리어 창고=합계·RP 1회·재처리 없음·요약 줄, 사망 무음료/보존 포기, 이름 띄어쓰기).
- 전체 EditMode 725/725(`tests/edit_hudfull.xml`)는 HUD 수정만 들어간 소스 기준(결과 화면 수정 전). 전체 PlayMode는 root 지시로 반복하지 않음 — 최종 소스 전체 회귀라고 주장하지 않는다.

## 5. 빌드·실행
- 새 형제 폴더 `Builds/Playable-2026-10-19-drinklab-v1.1-hudfix` (옛 `Playable-2026-10-19-drinklab-v1.1`는 그대로 — 실행 중인 exe 덮어쓰지 않음).
- ZIP `Builds/CocktailCoffeeTea-DrinkLab-v1.1-hudfix-2026-10-19-win64.zip` SHA256 `704E5B1E22D1FF045D24D2725E47CDB4142A154E146251CDAA0C5968390FB47F` (106,276,235 bytes, DoNotShip 제외) — 후속 ctx_e1a286851587에서 "획득 기록 N건" 문구로 같은 경로 재빌드(이전 8DAB1953…0A10 대체). `CCT.Runtime.dll`에 새 문구 있음·옛 "받은 묶음" 없음 확인.
- 새 폴더 압축 해제 실행: `C:/UnityScratch/r73/fresh-hudfix` + 격리 저장 `fresh-hudfix-data` → 30초 생존, 로그 Exception 0, drinks.json 생성(`tests/fresh-hudfix-player.log`). 재빌드 ZIP도 같은 방법으로 다시 확인(30초 생존·Exception 0). 획득 내역 창 캡처(hr*)는 옛 문구 "받은 묶음 N개"로 찍힘 — 새 문구가 더 짧아 같은 줄 안이므로 기존 배치 측정 유효(새 캡처 생략).
- 실행기: `Delivery/DrinkLab-v1.1/Play-DrinkLab-v1.1-hudfix.cmd`(+`-TestMaterials`) — 기존 v1.1 격리 TestData를 쓰며, CocktailCoffeeTea가 이미 실행 중이면 **시작하지 않고** 먼저 닫으라고 안내(자동 종료 없음). 자동 실행하지 않았다.
- 빌드 이후 바뀐 것은 시험 파일과 QA 전용 캡처(`#if CCT_QA_TOUR`)뿐 — 출시 빌드 런타임 소스와 동일.

## 남은 것
- root의 실제 스크린샷 최종 판정(특히 `after-result/hr836b/R2`, `after-hud/ha836/H1`, `natural-name/hn720`).
- 사용자 원래 1622 스크린샷의 오른쪽 잘림 자체는 정확히 재현 못 함(위 분석).
- 경제 수치는 바꾸지 않음(추첨 수·기록 건수·합계만 기록).
