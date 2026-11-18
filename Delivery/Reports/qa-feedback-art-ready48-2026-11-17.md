# QA — 11-17 추가 아트 ready48 Unity 통합·자동 검수·검수본 (task_7fa2c578c3f1)

선행: [11-17 자동 처리 보완](qa-feedback-automation-2026-11-17.md). 기획 루트 `AGENTS.md` 준수. 증거 `Delivery/Verification/2026-11-17/americano/`, `.../ready48/`. QA 빌드 `Builds/Feedback-QaTour-2026-11-17c`(격리 데이터 `Builds/Feedback-QaTour-2026-11-17c-qa-data/`). 음량 0, 실제 저장 미접촉, ArtSource 미편집, 커밋·푸시·공개 배포·PC 종료 없음, 하위 워커 없음.
증거 구분: [A] Unity 자동 시험 · [F] QA 빌드 자동 조작 화면·계측 · [H] 사람 직접 플레이·청취·IME·실모바일 = **이번 범위 제외(미실시)**.

## 1. 반입 — integrated 48 / ready 48

도구 `Tools/DrinkLab/import_ready_art.py`(신규, 묶음 manifest 공용: status·sha 사전 검증 후 일괄 복사, 기존 PNG는 GUID 유지, anchors 병합). 반입 시점 목록·해시 스냅샷 `ready48/ready-snapshot.json`.

| 묶음(manifest) | 수 | 경로 | 비고 |
|---|---|---|---|
| americano | 2 | `Resources/DrinkLab/dl_coffee_americano_mug(+_mask).png` | 신규. anchors 1항목. 마스크 720px 전부 잔 알파 0 위(액면창) |
| hidden | 9 | `Resources/Icons/Hidden/hidden_*.png` 24×24 | 신규. 전용 임포트 규칙(Point·밉맵 없음) 추가 — 일반 Icons 규칙(쌍선형·밉맵)은 픽셀 아트를 흐림 |
| tails | 9 | `Resources/ArtExtra/vfx_arrow_tail/vfx_arrow_{fire,bitter,dual}_tail_00~02.png` | 신규. 임포트 PPU128·피벗(30/32, 8/16) 확인 |
| containers | 28 | `Resources/DrinkLab/dl_tin_<산지>.png` | **28개 모두 기존 교체, GUID 유지**, anchors 28항목(mouth·pourLip·grip) 병합 |

기존 c의 36개 원본·반입본 해시 불변(이번 반입 대상 아님).

## 2. 코드(이번 작업)

- 아트 연결: `PlayerProjectile.cs`(전용 꼬리 로더·깃 끝 소켓·8fps), `ActiveEffects.cs`(히든 전용 아이콘 우선), `CoffeeStyles.cs`(아메리카노 = 액면창 머그, 없으면 기존 머그 + 임시 문구 — 반입으로 해제), `Editor/IconImportSettings.cs`(꼬리·히든 임포트 규칙).
- **결함 수정 1** `DrinkPhysicsSim.CupAdditions`: 아메리카노 뜨거운 물이 잔 표시에서 빠져 있었다(11-15부터, 불투명 머그라 안 보였음 → 액면창 머그에서 9.5%만 찬 것처럼 보임). 물을 잔 첨가 액체에 포함(표시 전용 — 검증·용량·효과 불변). 결과: 편집 중 0.474(180/380) → 완성 0.568(216/380) = 스타일 총 부피.
- **결함 수정 2** 상태 줄 도구 이름: 원두를 덜 때도 "다하"로 표기 → "스쿱"(`DrinkStageView.cs`, `DrinkLabView.cs`).
- 시험: `FeedbackArtContractTests`(+아메리카노 반입·뜨거운 물 표시 회귀), `CoffeeLabPlayTests.OriginTins_All28_TinStaysClearOfScoop_OneScoop`(신규).
- QA 투어(CCT_QA_TOUR): `AutoTourAmericano.cs`·`AutoTourContainers.cs`(신규), `AutoTourFeedback4.cs`(히든 B 세트), `AutoTourSmooth.cs`, `AutoTourFeedback2.cs`(거품 픽셀).

## 3. 자동 시험 [A]

| 묶음 | 결과 | 증거 |
|---|---|---|
| EditMode 관련 9묶음(반입 직후) | 61 중 60 통과·실패 0·불확정 1(기존 `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle`) | `ready48/editmode-1.xml` |
| PlayMode 관련 6묶음 | 75 중 73·실패 0·건너뜀 2(기존 Explicit) | `ready48/playmode-1.xml` |
| 산지 통 28 실제 스쿱(CoffeeLab 전체) | 21 중 19·실패 0·건너뜀 2 — 28종 기울여 덜 때 통-스쿱 간격 ≥ 16px(−0.5 허용), 한 스쿱 20g, 제조 전 재고 불변 | `ready48/playmode-tins-2.xml` |
| 뜨거운 물 수정 후 EditMode 4묶음 | 39/39 | `ready48/editmode-hotwater.xml` |
| 뜨거운 물 수정 후 PlayMode 3묶음 | 58 중 56·실패 0·건너뜀 2 | `ready48/playmode-hotwater.xml` |
| QA 빌드(최종 5차)·릴리스 빌드 | Success | `build-qatour-5.log`, `build-release-3.log` |

중간 실패와 처리: (1) 1차 배치 메모리 게이트 거부(내 이전 Unity가 남긴 컴파일 서버 510MB) → 정리 후 재실행, 단계마다 정리 추가. (2) 통 28 시험 1차 = 시험 코드의 이전 참조 문제(실제 버튼 경로로 수정). (3) 투어 판정 오류 3건(뜨거운 물 상한 누름 수, 통-스쿱 간격 표본 구간·허용값, 각성 HP 피해 경로) — 게임 결함 아님, 투어 수정 후 재실행 PASS.

## 4. 실화면·계측 [F]

| 대상 | 결과 | 증거(`ready48/`) |
|---|---|---|
| 아메리카노 720/1080/4:3 | 빈 잔(액면창 너머 배경 보임) → 뜨거운 물 + 단계별 증가(로그 level01) → 상한 200mL에서 + 무효 → − → 비우기 = 빈 잔 → 제조 중 0.474→0.568, 채움 최대 62%(상한 82%), 거품 띠 없음, 임시 문구 없음 | `americano-*/am_*_01~07_*.png`, tour-log `AM` |
| 산지 통 28 720/1080/4:3 | 28종 통 그림·앵커(기본값 아님)·+20g·재고 불변·비우기(취소) 정상, 0 오류(최종 720) | `containers-*/ct_*_pouring.png`, tour-log `CT` |
| 화살 꼬리 720/1080/터치 20:9 | 화염·여운·혼합 꼬리 3클립이 깃 끝 소켓에 부착, 머리 색·크기(0.7053)·판정 불변 | `feedback4-*/f4_*_11~13_arrow_*.png` |
| 화살 저이펙트 720 | 꼬리 없음·머리 색만(로그 tail=none) | `feedback4-low720/` |
| 히든 전용 모티프 9 | HUD 칸에 9종 모두(iced_cup·soda_pump·herb_garden·thick_mug / weekend_rush_steps·bitter_wake·rhythm_shaker·bar_tempo·bar_tempo_move) | `feedback4-1080/f4_*_20~22_hidden_tiles*.png`, `shots-sprites.csv` |
| 회귀(도감·제조 화면) | feedback1·feedback2 720 errors 0 | `feedback1-720/`, `feedback2-720/` |
| 이동 13 군중·경계 A/B·거품 두께 | 앞 보고(11-17 automation) 완료 | — |

관찰(변경 안 함): 화살 꼬리는 합의 규격(꼬리 불투명 ≈0.155월드, 화살 길이의 약 29%)이라 1080p에서 약 13×5px로 작게 읽힌다 — 크기 변경은 코디네이터 결정 사항.

## 5. 검수본(새 11-17)

| 항목 | 값 |
|---|---|
| 실행 폴더 | `Builds/Feedback-Review-2026-11-17/CocktailCoffeeTea.exe`(strict 릴리스, 13:41, 두 결함 수정 포함) |
| ZIP | `Delivery/CocktailCoffeeTea_FeedbackReview_2026-11-17.zip`(109,746,256 B, 207 파일, testzip OK) |
| SHA-256 | `39a09d87f0df1522bd719088b87d66f8802c29cc317e6063fb483aea935a64de`(`.sha256`, `sha256sum -c` OK) |
| 실행 안내 | ZIP 안 `README_실행안내.txt`, `Play-Review-IsolatedSave.cmd`(review-data 격리 저장) |
| 스모크 | 격리 데이터 기동·종료, Exception 0 |
| 이전 보존 | b `70798b61…e908`, c `e8bc9bf6…5b59` ZIP 해시 재계산 일치, 실행 폴더 그대로 |

## 6. 미실시·한계

- [H] 사람 직접 플레이·청취·한글 IME·실제 모바일 기기 — 이번 범위 제외(지시).
- 아메리카노 제조 연출의 근접 화면은 기존 공용 추출 잔(`dl_coffee_extraction_cup_rim`)을 쓴다(이번 아트 범위 밖).
- 취기는 기존 선택형 표시 범위 유지(변경 없음).
