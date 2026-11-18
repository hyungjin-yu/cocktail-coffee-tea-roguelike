# QA — 2026-11-14 플레이 피드백 26항목 최종 검증·검수본 (2026-11-16, task_2c6e20836d46)

기준 문서(UTF-8 직접 읽음): `기획/플레이 피드백 개선 기획_2026-11-14.md` 전체, `개발 기록/플레이 검수/피드백 개선 실행지시_2026-11-15.md`, 기획 루트 `AGENTS.md`.
적용 지침: `.claude/skills/madia-ui-ux/SKILL.md`, game-feel-first, `Delivery/Reports/qa-protocol.md`.
작업 공간: `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing`(브랜치 `codex/coffee-brewing`, HEAD `6578edd` + 미커밋 변경 보존). 커밋·푸시·공개 업로드 없음. 사용자 실제 저장 미접촉(모든 실행은 `-cct-data-dir` 격리). 투어·스모크는 설정 파일로 주·음악·효과음 0.
선행 보고: [첫 묶음 QA](qa-feedback-batch1-2026-11-15.md), [19·25·26 검토 조사표 + 4 데이터 조사](feedback-review-19-25-26-and-4-2026-11-15.md).

## 0. 진행 기록

1. 오전(사용자 게임 중): GUI·Unity 보류 — 정적 조사로 결함 원인 확인, 코드 수정, Roslyn 정적 컴파일만.
2. 사용자 "던파 종료" 후 코디네이터 재개 지시: `Tools/QA/gated-unity.sh`(여유 RAM 6914~7869MB, Hub 에디터 0 — 각 `*.gate.txt`)로 시험·빌드·투어.
3. 증거 구분: **[C]** 컴파일, **[A]** Unity 자동 시험, **[F]** QA 빌드 자동 조작 화면(실제 버튼 onClick·공개 API, 일부 강제 경로 — 아래 표기), **[H]** 사람 직접 플레이 = 전부 **NOT RUN**.
4. 증거 루트: `Delivery/Verification/2026-11-16/feedback-all/`. QA 빌드 `Builds/Feedback-QaTour-2026-11-16`(3차), 격리 데이터 `Builds/Feedback-QaTour-2026-11-16-qa-data/<모드>`.

## 1. 빌드·자동 시험(최종 소스 기준)

| # | 항목 | actual | 증거 | 상태 |
|---|---|---|---|---|
| C1 | Roslyn 정적 컴파일 4 어셈블리 × 기본/`CCT_QA_TOUR` | 오류 0(수정마다 반복) | 세션 기록 | PASS(컴파일만) |
| A1 | EditMode 전체 1차(오전 수정분) | 984 중 982 통과·실패 0·불확정 1·건너뜀 1(둘 다 기존: `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`, `IntegerOnlyTests.Migration_UserSaveCopy_IsolatedRun`) | `editmode-1.xml` | PASS |
| A2 | PlayMode 전체 1차 | 393 중 382 통과·실패 0·건너뜀 11(기존) — 11-15 간헐 실패 `Joystick_DragMovesPlayer_ReleaseStops` 통과 | `playmode-1.xml` | PASS |
| A3 | EditMode 전체 최종 | 985 중 983 통과·실패 0·불확정 1·건너뜀 1(기존 같은 2건) | `editmode-2.xml` | PASS |
| A4 | PlayMode 전체 최종 | 393 중 382 통과·실패 0·건너뜀 11 | `playmode-2.xml` | PASS |
| B1 | QA 투어 빌드 1·2·3차 | `Build Finished, Result: Success` ×3 | `build-qatour.log`, `-2.log`, `-3.log` | PASS |
| B2 | 최종 검수본(비QA strict 릴리스, `Windows64ReleaseToBatch`) | `Build Finished, Result: Success`, 544MB | `build-release.log` | PASS |
| S1 | 검수본 스모크(격리 데이터, 15초) | 실행·저장 경로 격리·`Exception` 0 | `Builds/Feedback-Review-2026-11-16-smoke-data/Player.log` | PASS(기동만) |
| S2 | ZIP 무결성 + 압축 해제본 실행 안내 cmd | `testzip None`, 해제본이 `review-data`에만 저장 | 세션 기록 | PASS |

신규 회귀 시험(모두 [A] PASS): `FeedbackBatch2Tests.GoalHud_UnderPlainTransformRoot_AwardUpdatesChipsAndFlies`, `FeedbackBatch2Tests.AwardDetail_DestroyedThenLateInput_NoException`, `FeedbackBatch3Tests.TitleIntro_PropsAboveBackdrop_DarkBetweenPropsAndMenu_EndStateOpaque`. 수정 시험: `MobileTouchPlayTests.Joystick_DragMovesPlayer_ReleaseStops`(물리 단계 대기).

## 2. 이번에 찾은 결함과 수정(수정 전 증거 → 수정 후 같은 경로 증거)

| # | 결함 | 원인 | 수정 | 수정 후 증거 | 상태 |
|---|---|---|---|---|---|
| D1 | **24** 목표 음료 칸이 보상 후에도 `30/60mL` 그대로, 비행 0 | `DrinkGoalHud.OnAward`의 미사용 `(RectTransform)root.root` — 실제 런 Canvas는 GameManager(일반 Transform) 아래라 `InvalidCastException`(11-15 Player.log 41·42행) → 갱신 전부 생략. 지급은 1회 정상 | 형변환 제거 + 시험용 runId 오버로드 | 로그 `flights=2`, `60/60 mL · 판+30`, 완성 순간 `flashing=True`·제목 "✓ 재료 충분". `feedback2-720/f2_1280x720_21~23_hud_goal_*.png`, 1080 같은 이름 | PASS |
| D2 | **24** 720p 칸 글자 ≈9px, `30/60`/`mL` 줄바꿈 | 칸 폭 항상 4등분(값 38) | 칸 수로 분할·두 줄(값 22 / 단위·이번 판 16) | `f2_1280x720_22_hud_goal_after.png`(값 ≈15px, 비겹침) | PASS |
| D3 | **1** 첫 화면 직업·잔 소품이 거의 안 보임 | `SetSiblingIndex(1)` = 97% 불투명 `CafeBackdrop` 아래 | 배경 판 바로 위에 순서대로 | `feedback3-720/f3_1280x720_02_title_intro_end.png`(바텐더·티 소믈리에·잔 3 선명) | PASS |
| D4 | **4** 라떼 거품 버튼 글 두 줄로 넘침 | 긴 안내를 버튼 안에 | 버튼 = 한 줄 상태, 안내 = 아래 `FoamNote` | `f2_1280x720_15_coffee_latte_ready.png` | PASS |
| D5 | **4** 버튼 첫 글자 "✓"가 "/"로 잘림(720·1080) | 왼쪽 정렬 글 여백 10 < 종이 버튼 테두리 | 토글 줄 글 왼쪽 여백 24 | 같은 파일(3차 빌드) | PASS |
| D6 | **7** 로비 노트북 안내 말풍선: 제목 두 줄로 본문과 겹침, 본문 잘림, 반투명 판 뒤로 직업 카드 글 비침 | 글 칸 528(버튼 230×2), 판 스킨 가운데 반투명 | 테두리 안 불투명 판, 버튼 200, 폭 1100, 제목·본문 각각 한 줄 최적 크기 | `f2_1280x720_40_lobby_notebook_guide.png`, 1080 같은 이름 | PASS |
| D7 | 결과 → 획득 내역 → 타이틀 직후 늦은 연속 입력에서 `NullReferenceException`(2026-11-16 1차 투어, `feedback2-720-run1-nre/`) | 파괴된 `DrinkAwardDetailView.IsOpen`이 `gameObject` 접근. 실제 사용자 클릭으로는 파괴 후 도달 어려움(투어의 C# `?.`가 Unity 파괴 null을 통과) | `IsOpen`에 Unity null 검사, 결과 화면 콜백 null 방어 | 2·3차 투어 `exceptions=0`, 회귀 시험 PASS | PASS |
| D8 | (QA 도구) 11-15 투어 `no result` | 사망 처리 다음 프레임 + 1.2초 실시간 지연을 고정 1.2초로 대기 | 최대 6초 폴링 | 결과·내역·노트북 단계까지 완주 | PASS |
| D9 | (시험) Joystick 간헐 실패 | 놓은 뒤 예약 `MovePosition` 1 물리 단계 | `WaitForFixedUpdate` 2회 | A2·A4 통과 | PASS |

## 3. 실제 화면 투어(최종 QA 빌드, 1280×720 / 1920×1080 실제 창)

| 투어 | 720 | 1080 | 결과 |
|---|---|---|---|
| feedback1(2·3·6·8·9·10·17·18·22, 23 빈 내역) | `feedback1-720/` 34장 | `feedback1-1080/` 34장 | errors=0 exceptions=0 (2차 빌드) |
| feedback2(5·7·23·24·4) | `feedback2-720/` 34장 | `feedback2-1080/` 34장 | errors=0 exceptions=0 (3차 빌드) |
| feedback3(1·11·12·14·15·16·20·21) | `feedback3-720/` 24장 | `feedback3-1080/` 24장 | errors=0 exceptions=0 (2차 빌드) |
| smooth13 A/B(1080, 두 직업·네 무기 × 오른쪽/왼쪽/대각 2) | `smooth13-interp/` | `smooth13-nointerp/` | 보간: 멈춘 프레임 0%(16/16 구간), 화면 흔들림 σ 0.43~0.67px / 보간 없음: 49.8~50.2%, σ 3.39~4.82px, maxDt 10.1~10.2ms |

feedback1·3은 2차 빌드 캡처이며 3차 빌드 변경(D5·D6)은 두 투어 화면에 영향 없음(노트북 말풍선·종이 토글 줄 미포함 — 단, 실습 말풍선은 같은 `NotebookGuide.Bubble`을 써서 feedback2 3차에서 재확인 `f2_*_01_practice_intro.png`·`_02_practice_gin.png`).
강제 경로: 스폰 정지·체력 증가, 카드 창 직접 제시, 목표 음료 `SetGoal`, 보스 재료 보상 `RecordBossAward` 직접 호출, 사망 `MarkPlayerDiedForTests`, 보스 `SpawnNow` 후 위치 이동(15번 화면의 "오른쪽에서 접근" 문구는 이 이동의 흔적), 적 탄 피해 0 생성.

## 4. 번호별 최종 상태

| # | 내용 | 구현 | [A] | [F] 720/1080 | 대표 증거(`feedback-all/` 기준, 1080은 `-1080` 폴더 같은 이름) | 남은 것·한계 |
|---|---|---|---|---|---|---|
| 2 | 도감 그림 확대 | 완료 | PASS | PASS | `feedback1-720/fb_1280x720_03_codex_mixed.png`, `_10_ingcodex_all.png` | — |
| 3 | 미획득 잠금 | 완료 | PASS | PASS | `_01_codex_locked.png`, `_02_codex_locked_detail.png`, `_14_ingcodex_detail_unseen.png` | 자물쇠 아이콘 없음(글 칩 "미발견") |
| 6 | 재료 도감 검색·필터 | 완료 | PASS | PASS | `_12_ingcodex_coffee.png` ~ `_17_*.png` | 한글 IME 실제 입력 [H] NOT RUN |
| 8 | 카드 순번 제거 | 완료 | PASS | PASS | `_bartender_rapier_20_card_select.png` | — |
| 9 | 효과/지식 글 계층 | 완료 | PASS | PASS | 같은 파일, `_tea_bow_20_card_select.png` | — |
| 10 | 지식 상세 출처 | 완료 | PASS | PASS | `_21_card_learning_top.png`, `_22_card_learning_sources.png`, `_06_codex_source_copied.png` | — |
| 17 | 연구 노드 순번 제거 | 완료 | PASS | PASS | `_30_study_readonly.png`, `_31_study_alloc.png` | 노트북 숙련 트리 노드의 "1. 첫 잔의 기세" 번호·줄넘침은 17 범위(우림/커피 연구) 밖 기존 화면 — 별도 판단 필요 |
| 18 | 연구 그림 세로 정렬 | 완료 | PASS | PASS | `_32_study_mixed.png` | — |
| 22 | "성장·조합 보기" | 완료 | PASS | PASS | `_40_result.png`, `_41_growth_detail.png` | — |
| 5 | 1 단위 제거·지거 따르기·실습 | 완료 | PASS | PASS | `feedback2-720/f2_1280x720_01_practice_intro.png` ~ `_09_orders_sale_blocked.png`, `_23_unit_buttons_liquid.png` | 지거 안내 "45mL씩 나눠 붓기"와 +30mL 단위가 같이 보임 — 사람 검수 필요 |
| 7 | 첫 판 뒤 노트북 안내 | 완료 + D6 수정 | PASS | PASS | `_40_lobby_notebook_guide.png`, `_41_laptop_guide_step2.png`, `_42_laptop_guide_step3.png`, `_43_laptop_guide_replay.png`, `_44_lobby_after_guide.png`(완료 뒤 재표시 없음) | — |
| 23 | 결과 → 획득 내역 | 완료 + D7 방어 | PASS | PASS | 보상 있는 판 `_30_result_with_award.png`(포커스 "획득 내역 확인 ▶"), `_31_award_detail_items.png`(4종 그림·+양·창고); 빈 판 `feedback1-720/fb_*_42_award_detail.png` | — |
| 24 | 목표 음료 재료 | 완료 + D1·D2 수정 | PASS | PASS | `_20_hud_goal_start.png` ~ `_23_hud_goal_ready.png` | 일부 재료 그림이 칸 안에서 작음(기존 아이콘 여백) |
| 13 | 이동 끊김 | 완료(보간, 판정 위치 불변) | PASS | PASS(측정) | §3 smooth13 | 군중 구간·카메라 경계 측정 NOT RUN, 체감 [H] NOT RUN |
| 14 | 재료 획득 그림 | 완료 | PASS | PASS | `feedback3-720/f3_1280x720_*_20_award_toast_chips.png` | — |
| 15 | 보스 접근 안내 | 완료 | PASS | PASS | `_50_boss_incoming.png`, `_51_boss_offscreen.png` | 등장음 [H] NOT RUN |
| 16 | 타격감 | 완료 | PASS | PASS(정지 화면) | `_60_hit_streak.png`, `_61_killing_blow.png` | 체감·소리 [H] NOT RUN |
| 20 | 몹 등급별 탄 | 완료(테두리·꼬리, 판정 동일) | PASS | PASS | `_40_projectile_tiers.png` | — |
| 21 | HP 20% 미만 | 완료 | PASS | PASS | `_30_low_hp.png` | 경고음 [H] NOT RUN |
| 1 | 첫 화면 연출 | 완료 + D3 수정 | PASS | PASS | `f3_1280x720_00/01/02_title_intro_*.png`, `_90_title_reentry.png` | 연속 영상·첫인상 [H] NOT RUN |
| 11 | 등급별 카드 | 완료(확률·효과 불변) | PASS | PASS | `_10_card_rarity_enter.png`, `_11_card_rarity_goal.png` | 등장음 [H] NOT RUN |
| 12 | 목표 진행 카드 | 완료 | PASS | PASS | `_11_card_rarity_goal.png` | — |
| 4 | 커피 4종 | 기능 완료 + D4·D5 수정. **아트는 임시** | PASS | PASS(제조·우유 1회 차감·거품 단계) | `feedback2-720/f2_1280x720_12_coffee_americano.png` ~ `_22_coffee_Americano_done.png` | **신규 잔 3종·거품 층 아트 없음 — 기존 머그·컵 임시, 화면에 "잔 그림 임시" 표기. 아트 완료로 보지 않음** |
| 19 | 화살 종류 | 검토 조사표만 | — | — | 검토 문서 | 결정 대기, 중복 피해 추가 없음 |
| 25 | 히든별 UI | 검토 조사표만 | — | — | 검토 문서 | 결정 대기 |
| 26 | 취기 | 검토 조사표만 | — | — | 검토 문서 | 결정 대기, 조작 반전 추가 없음 |

## 5. 소비·보상 1회 처리(실화면 로그 근거)

- 실습 재료 1회 지급·재지급 거부(`실습 재료는 이미 받았습니다.`), 실습 잔 판매 거부·재화 0→0.
- 단위 따르기 중 두 번째 입력 무시(`second=False`), 배합 증가 1회.
- 커피 4종 우유 차감 1회(6150→4650→3750→2850, 아메리카노는 0).
- 보스 재료 보상 같은 id 중복 기록 거부(`dup=False`), 판 정산 1회(`kind=Settled granted=6`, 획득 내역 6건 → 재료 4종 합산).
- 획득 내역 → 타이틀 연속 입력: 두 번째 무효, 예외 0(D7).
- 노트북 안내 완료 플래그 저장(`notebookGuideVersion=1`), 다시 보기 가능, 완료 뒤 로비 재표시 없음.

## 6. 최종 Windows 검수본

| 항목 | 값 |
|---|---|
| 실행 폴더 | `Builds/Feedback-Review-2026-11-16/CocktailCoffeeTea.exe`(strict 릴리스 설정, QA 투어 코드 없음) |
| ZIP | `Delivery/CocktailCoffeeTea_FeedbackReview_2026-11-16.zip`(109,778,591 B, 207개 파일, Burst 디버그 폴더 제외) |
| SHA-256 | `3630850f91d57dab23c35b0afbdc8ff31ae76435613e867c3bc897b4b259e874`(`…zip.sha256`) |
| 실행 안내 | ZIP 안 `README_실행안내.txt`, `Play-Review-IsolatedSave.cmd`(권장 — 저장을 같은 폴더 `review-data`에 격리). exe 직접 실행은 평소 사용자 저장 사용 |
| 기존 보존 | `Play-Windows-Review.cmd`(11-02 빌드), 이전 Builds/Delivery ZIP·공개본 덮어쓰지 않음 |

## 7. 11-16 변경 파일

- `Assets/_Project/Scripts/UI/DrinkGoalHud.cs` — 형변환 예외 제거, 시험 runId 오버로드, 칸 폭·두 줄.
- `Assets/_Project/Scripts/UI/TitleIntro.cs` — 소품·어둠 판 순서.
- `Assets/_Project/Scripts/Drinks/DrinkLabView.cs` — 거품 버튼 한 줄 + `FoamNote`, 토글 글 왼쪽 여백.
- `Assets/_Project/Scripts/UI/NotebookGuide.cs` — 말풍선 불투명 판·글 칸.
- `Assets/_Project/Scripts/UI/CharacterSelectView.cs` — 로비 말풍선 위치·폭.
- `Assets/_Project/Scripts/UI/DrinkAwardDetailView.cs`, `Assets/_Project/Scripts/UI/ResultView.cs` — 파괴 후 입력 방어.
- `Assets/_Project/Scripts/Core/AutoTourFeedback2.cs` — 결과창 폴링(QA 전용).
- `Assets/_Project/Tests/EditMode/FeedbackBatch2Tests.cs`(+2), `Assets/_Project/Tests/EditMode/FeedbackBatch3Tests.cs`(+1), `Assets/_Project/Tests/PlayMode/MobileTouchPlayTests.cs`.

11-15 피드백 작업 변경 파일 목록은 이전 판 보고(git 미커밋 상태 그대로 보존). ProjectSettings·Packages `git diff` 없음. ArtSource 미수정, 새 PNG 없음.

## 8. 미검증·한계

- [H] 사람 직접 플레이 전부 NOT RUN: 13·16 체감, 11·15·21 소리(투어는 음량 0), 6 한글 IME, 첫 화면 연속 영상.
- 좁은 화면비·터치 UI 캡처 NOT RUN. 13 군중 구간·카메라 경계 측정 NOT RUN.
- 검수본(릴리스)은 15초 기동 스모크만 — 릴리스 빌드 자체로 전 흐름을 다시 돌리지는 않았다(같은 소스의 QA 빌드에서 [F] 확인).
- 4 커피 신규 아트·자물쇠·산지 원두 아이콘은 임시(아래 요청표).

## 9. 신규 아트 요청(만들지 않음, Aseprite + PixelLab, ArtSource 원본 직접 수정 금지)

| asset ID | 규격 | 기존 참조 | 필요 이유 |
|---|---|---|---|
| `dl_coffee_latte_glass`, `dl_coffee_flatwhite_cup`, `dl_coffee_cappuccino_cup` (+`_mask`) | 기존 `dl_coffee_mug`/`dl_coffee_cup` 캔버스·4배 표시·액면 마스크 규칙, 바닥 가운데 앵커 | `Assets/_Project/Resources/DrinkLab/dl_coffee_*` | 4 완성 음료 잔(현재 임시) |
| `dl_foam_layer` | 32×8 가로 반복, 흰색~아이보리 3단 | — | 4 거품 층(현재 단색 띠) |
| `ui_lock_badge` | 16×16 투명, Point, 1px 외곽선 | `Resources/ProgressionUI/tree_node_locked.png` | 3·6 미발견 칩 |
| `dl_icon_coffee_<산지>` | 32×32 투명, 기존 `dl_icon_*` | `Resources/DrinkLab/dl_icon_coffee_bean.png` | 6 산지 원두 구분 |
| (승인 시) `vfx_arrow_*`, `hidden_kind_*` | 검토 문서 | — | 19·25 결정 후 |
