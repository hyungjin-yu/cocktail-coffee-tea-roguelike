# QA — 피드백 최종 아트 36개 Unity 통합·실화면 검증 (2026-11-16, task_e8a7c8e4bdaf)

원본(읽기만): `ArtSource/feedback-art-2026-11-16/manifest.ready.json`(status ready, 36), `ready/<assetId>.ready.json` 36개, `ready/anchors-handoff.json`, `origin-inventory.json`. ArtSource 수정 없음.
증거 루트: `Delivery/Verification/2026-11-16/art-import/`. QA 빌드 `Builds/Feedback-QaTour-2026-11-16c`, 격리 데이터 `Builds/Feedback-QaTour-2026-11-16c-qa-data/`. 음량 0, 실제 저장 미접촉, 커밋·푸시·공개 배포·PC 종료 없음.
증거 구분: [A] Unity 자동 시험, [F] QA 빌드 자동 조작 화면, [H] 사람 직접 플레이 = NOT RUN.

## 1. 반입(통합) — 36 / 36

도구: `Tools/DrinkLab/import_feedback_art.py`(신규). manifest status/ready와 PNG sha256 일치 시에만 `recommendedUnityPngDestination`으로 복사, 기존 PNG는 내용만 교체해 `.meta` GUID 유지. 결과 `art-import/import-report.json`.

| 묶음 | 수 | 경로 | 비고 |
|---|---|---|---|
| 잔 `dl_coffee_latte_glass`(36×36)·`dl_coffee_flatwhite_cup`(32×26)·`dl_coffee_cappuccino_cup`(32×26) | 3 | `Assets/_Project/Resources/DrinkLab/` | 신규 |
| 잔 마스크 `*_mask` | 3 | 같은 곳 | 잔과 같은 캔버스(시험 확인) |
| 거품 `dl_foam_layer`(32×8) | 1 | 같은 곳 | 신규 |
| 잠금 `ui_lock_badge`(16×16) | 1 | `Assets/_Project/Resources/ProgressionUI/` | 신규 |
| 산지 원두 아이콘(32×32) | 28 | `Assets/_Project/Resources/DrinkLab/dl_icon_*` | 25 신규 + 3 교체(`dl_icon_ethiopia_yirgacheffe_kochere`, `dl_icon_colombia_huila_vergel`, `dl_icon_brazil_mantiqueira_alves_lima` — GUID 유지 확인, 이전 sha는 inventory의 6fbc4c2a…/f6148922…/800189f0…) |

- 반입 후 복사본 sha256 = manifest 값 36/36, `.meta` 36/36(신규 33개는 Unity 임포트가 생성).
- 임포트 설정: 프로젝트 규칙(기존 AssetPostprocessor) 적용 — DrinkLab = Point·무압축·밉맵 없음·PPU 32·아래 가운데 피벗, ProgressionUI = Point·PPU 100·가운데. manifest 권장 PPU 128과 다르나 UI·잔 표시는 픽셀 배율(×4) 기준이라 기존 자산과 같은 규칙을 유지(차이 기록).
- 잔 앵커: `Resources/DrinkLab/anchors.json`에 3항목(mouth·grip·liquid·width·height) 추가만(기존 57항목 불변, 파일 형식 유지 — diff +57줄). liquid 창 = 마스크 불투명 영역과 일치(정규화 검산).
- 산지 매핑: inventory의 `ingredientId` → 실제 재료 정의 `iconId` 28/28 일치(`DrinkData.cs` 정적 + 시험).

## 2. 코드 연결

| 항목 | 변경 | 파일 |
|---|---|---|
| 완성 음료 잔 | 코드 변경 없음 — `CoffeeStyles.ResolvedCupArt`가 아트 존재 시 최종 잔 사용. 라떼·플랫화이트·카푸치노 `UsesTempArt=false` → "잔 그림 임시" 문구는 실제 통합된 3잔에서만 자동 제거 | — |
| 액체 순서·82% 채움 | 기존 렌더러(우유 먼저 → 커피 위, `ServeFillMax` 82%) 그대로, 앵커 액체 창으로 그림. 투어 로그 fill 135/180·171/197(상한 이하) | — |
| 거품 층 | 띠 몸통(중간색) 위에 `dl_foam_layer` 한 줄(Tiled, 잔과 같은 ×4 픽셀 배율, 세로 반복 없음). 아트 없으면 단색 띠 | `Scripts/Drinks/DrinkStageView.cs` |
| 잠금 표시 | `UiFactory.AddLockIcon` 신규, 카드 도감 칩(28) + 재료 도감 칩(32, 칩 폭 96→120)에 자물쇠 + "미발견" 글(글+모양) | `Scripts/UI/UiFactory.cs`, `Scripts/UI/CardCodexView.cs`, `Scripts/Drinks/DrinkLabView.Codex.cs` |
| 화살 꼬리 대비(후속 지적) | 기존 꼬리 점 뒤 어두운 테두리 원(#201D29, Codex 검토표 외곽색). 표시만 — 판정·피해·속성 불변 | `Scripts/Player/PlayerProjectile.cs` |
| QA 투어 | feedback1에 산지 28종 발견 처리(격리 저장, 강제) 후 커피 필터·상세 캡처 단계 | `Scripts/Core/AutoTourFeedback.cs`(CCT_QA_TOUR) |

## 3. 검사

| # | 항목 | actual | 증거 | 상태 |
|---|---|---|---|---|
| C1 | Roslyn 정적 컴파일(일반·QA) | 오류 0 | 세션 | PASS |
| A1 | EditMode 관련 10묶음(신규 `FeedbackArtIntegrationTests` 4건 + FeedbackBatch1/2/4, CoffeeStyle, CoffeeRecipe, DrinkIngredientExpansion, IngredientPackagingIcon, UiReadability, QaToolIsolation) | 77 중 76 통과·실패 0·불확정 1(기존 `IngredientPackagingIconTests.MissingTinArt_DrawsTinNotCircle` — 산지 틴 아트 부재, 이번 범위 밖) | `editmode-1.xml` | PASS |
| A2 | PlayMode 관련 6묶음(DrinkLab, DrinkIngredientExpansion, CoffeeLab, FeedbackBatch2/4, CardEducation) | 69 중 67 통과·실패 0·건너뜀 2(기존 Explicit) | `playmode-1.xml` | PASS |
| B1 | QA 빌드 1·2차 | Success ×2(2차는 투어 단계 추가만) | `build-qatour*.log` | PASS |
| B2 | 검수본 c 릴리스 빌드 | Success, 545MB | `build-release.log` | PASS |
| S1 | 검수본 c 격리 기동 12초 | Exception 0, data dir 격리 | `Builds/Feedback-Review-2026-11-16c-smoke-data/Player.log` | PASS(기동만) |

## 4. 실화면 [F] 720p / 1080p

| 대상 | 결과 | 증거 |
|---|---|---|
| 카페라떼 유리잔 | 투명 유리 안 우유 아래·에스프레소 위 층, 거품 띠, 로그 `tempArt=False` | `feedback2-720/f2_1280x720_15_coffee_latte_ready.png`, `_16_…done`, 1080 같은 이름 |
| 플랫화이트·카푸치노 | 낮은 잔·받침 볼 잔 최종 그림, 거품 윗면 | `_17_coffee_FlatWhite_ready.png`, `_19_coffee_Cappuccino_ready.png` |
| 재료 도감 미발견 | 모든 미발견 칩에 자물쇠 + "미발견", 산지별 실루엣 형태 서로 다름 | `feedback1-720/fb_1280x720_12_ingcodex_coffee.png`, `_13_ingcodex_unseen.png` |
| 카드 도감 미발견 | 자물쇠 + "미발견", 글 잘림 없음 | `fb_1280x720_01_codex_locked.png` |
| 산지 28종 발견(강제) | 원색 아이콘, 칸 안 여백 적정(작은 실루엣 없음), 상세 큰 그림 | `fb_1280x720_18_ingcodex_coffee_origins_seen.png`, `_19_…bottom.png`, `_20_ingcodex_detail_origin.png`(자메이카 블루마운틴) |
| 화살 꼬리 테두리 | 찻집 녹색 바닥에서 여운 꼬리 분리 | `feedback4-720/f4_1280x720_12_arrow_bitter.png`, `_11/_13` |

투어 오류: feedback2 720/1080·feedback1 720/1080(2차)·feedback4 720 모두 errors=0 exceptions=0.
관찰(이번 범위 밖, 변경 안 함): 아메리카노 머그(`dl_coffee_mug`, 불투명 도자기)는 액체가 보이지 않는 기존 표현 — 이번 반입 전 캡처와 동일.

## 5. 최종 수·미검증

- **integrated 36 / verified 36**(Unity 반입·자동 시험·720/1080 화면). 단 산지 28종의 원색 확인은 강제 발견 상태의 도감 화면 기준, 제조 화면 원두 통 그림은 기존 틴 임시(산지 틴 아트는 이번 manifest 범위 아님).
- NOT RUN: [H] 사람 직접 플레이·청취·IME, 실제 모바일 기기, 거품 두께별 정밀 픽셀 검수(로그 높이·캡처만).
- Codex 요청 필요 사항: 없음(불일치 0). 산지 틴(`dl_tin_<산지>`)·화살 전용 꼬리·히든 전용 아이콘은 별도 결정 대기.

## 6. 검수본

| 항목 | 값 |
|---|---|
| 실행 폴더 | `Builds/Feedback-Review-2026-11-16c/CocktailCoffeeTea.exe` |
| ZIP | `Delivery/CocktailCoffeeTea_FeedbackReview_2026-11-16c.zip`(109,794,890 B, 207 파일, testzip OK) |
| SHA-256 | `e8bc9bf6e864605de9b5b34c4a08e4d52d709231c397b0e5d3242bf8f36c5b59`(`.sha256`, `sha256sum -c` OK) |
| 실행 안내 | ZIP 안 `README_실행안내.txt`, `Play-Review-IsolatedSave.cmd`(저장 격리) |
| 이전 보존 | b ZIP 해시 `70798b61…e908` 재계산 일치, `Builds/Feedback-Review-2026-11-16b` 그대로 |
