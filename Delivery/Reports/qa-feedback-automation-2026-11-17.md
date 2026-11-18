# QA — 11-17 자동 처리 가능한 표현 보완·검수 (task_7fa2c578c3f1, 진행 중: 아트 대기)

기획 루트 `AGENTS.md` 준수. 증거 루트 `Delivery/Verification/2026-11-17/automation/`. QA 빌드 `Builds/Feedback-QaTour-2026-11-17`(격리 데이터 `Builds/Feedback-QaTour-2026-11-17-qa-data/`). 음량 0, 실제 저장 미접촉, 커밋·푸시·공개 배포·PC 종료 없음, ArtSource 미편집, 하위 워커 없음. 기존 검수본 b·c 보존.
증거 구분: [C] 컴파일, [A] Unity 자동 시험, [F] QA 빌드 자동 조작 화면·계측. 사람 직접 플레이·청취·IME·실기기는 이번 범위에서 제외(지시).

## 1. 렌더러 계약 조사 → Codex 규격(코디네이터 전달 완료)

| 대상 | 조사 결과(코드·파일 근거) | 전달 규격 |
|---|---|---|
| 아메리카노 잔 | `DrinkVesselView.Build`: 액체(마스크 스텐실) 위에 `Glass` 잔 그림을 덮는다. `dl_coffee_mug.png` 불투명 962px 모두 알파 255 → 액체 불가시(라떼 유리잔은 알파 96 유리 15px) | `dl_coffee_americano_mug`(+`_mask`) 36×36, 액면창 알파 0(유리 ≤96), 마스크 = 창 영역, anchors(mouth·grip·liquid). 용량 380·채움 상한 82%(최대 245mL = 64%) 코드 고정 |
| 산지 원두통 28 | `Resources/DrinkLab/dl_tin_<id>.png` 28개 서로 다른 그림 존재, 그러나 `anchors.json` 항목 없음(`dl_tin_coffee_bean`만 있음) → 스쿱 위치가 기본값(입구 위 가운데) | 같은 ID·32×32 교체(GUID 유지), ready.json에 mouth·pourLip·grip 필수(`dl_tin_coffee_bean` 규약 참고) |
| 화살 전용 꼬리 | 화살 아트 `arrow`/`tea_arrow` 128px 프레임, 불투명 영역 x 0.1406 → 깃 끝 소켓 로컬 (-0.359, ≈0), 화면 배율 0.705 | `Resources/ArtExtra/vfx_arrow_tail/vfx_arrow_{fire,bitter,dual}_tail_00~02.png` 32×16·PPU128·피벗(30/32, 8/16)·8fps. 코디네이터 확정: 화살 본체·촉 색 기존 유지, 꼬리 3클립만 |
| 히든 모티프 | HUD 칸 그림 26×24 | `Resources/Icons/Hidden/<ID>.png` 24×24, `hidden_rhythm_shaker`, `hidden_weekend_rush_steps`, 그 밖 `hidden_<히든id>`(바 템포 이동 `hidden_bar_tempo_move`) |

## 2. 코드(아트 납품 시 자동 사용, 없으면 기존 대체 표현)

- `Scripts/Player/PlayerProjectile.cs`: `TailArtId`·`TailFrames`(Resources)·`FletchSocketLocal`(화살 아트 불투명 영역에서 깃 끝 계산)·`ArrowTailAnim`(8fps, 일시정지 중 멈춤). 아트 없으면 현재 원형 점 + 어두운 테두리. 판정·피해·속성·크기 불변.
- `Scripts/Editor/IconImportSettings.cs`: `ArtExtra/vfx_arrow_tail/` 임포트 규칙(Point·무압축·PPU128·소켓 피벗).
- `Scripts/UI/ActiveEffects.cs`: `DedicatedHiddenArt` + `Icons/Hidden/` 우선 로드, 없으면 기존 승인 아트.
- `Scripts/Drinks/CoffeeStyles.cs`: 아메리카노 목표 잔 = `dl_coffee_americano_mug`, 없으면 기존 머그 + "잔 그림 임시"(사실대로 표시 — 액체가 보이지 않는 임시 상태).
- 거품 두께 계측: `DrinkStageView.FoamPixels()`(QA·시험용 읽기), 소스 `UpdateFoamBand`와 같은 식.
- QA 투어: `AutoTourSmooth.cs` 군중 계측 중 재료 픽업 제거(강제 — 카드 창 연쇄로 비었던 구간 해소), `AutoTourFeedback2.cs` 거품 픽셀 로그.

## 3. 검사

| # | 항목 | actual | 증거 | 상태 |
|---|---|---|---|---|
| C1 | Roslyn 정적 컴파일 | 오류 0 | 세션 | PASS |
| A1 | EditMode 관련 7묶음(신규 `FeedbackArtContractTests` 3건 포함) | 49/49 | `editmode-1.xml` | PASS |
| A2 | PlayMode 관련 4묶음 | 59 중 57 통과·실패 0·건너뜀 2(기존 Explicit) | `playmode-1.xml` | PASS |
| B1 | QA 빌드 | Success | `build-qatour.log` | PASS |

## 4. 계측 [F]

### 4-1 이동 13 — 군중·맵 경계 A/B (1920×1080, 이전 미측정 구간 포함 전 구간 완료)

| 구간 | 보간(현재) 멈춘 프레임 / 화면 x σ | 보간 없음 | 비고 |
|---|---|---|---|
| 군중(적 46~77) 두 직업 × 4방향 = 8구간 | 전부 0% / 0.76~1.86px | 전부 49.8~50.2% / 2.50~5.02px | 보간 없음 바텐더 4구간 **새로 완료**(11-16 미측정) |
| 경계: 벽 쪽(벽 정지 제외) | 0% / 0.10~0.20px | 50~51% / 4.50px | |
| 경계: 벽에서 빠져나오기 | 0% / 0.87px | 50.2% / 4.63px | |
| maxDt | 10.1~10.2ms | 10.1~10.2ms | 급등 없음 |

증거: `smooth13x-interp/`, `smooth13x-nointerp/`(`tour-log.txt`, CSV, 캡처). 강제 경로: 체력 증가·무기 끄기·재료 픽업 제거·런 시간 10분·경계 순간 이동. 사람 체감과 별개(계측).

### 4-2 거품 두께 픽셀 검수

| 음료 | 거품 mL | 720p 띠 / 기대(px) | 1080p 띠 / 기대(px) | 윗면 타일 | 잔 1픽셀 = 화면 px |
|---|---|---|---|---|---|
| 카페라떼 | 15 | 2.8 / 2.8 | 4.2 / 4.2 | 있음 | 2.67 / 4.00 |
| 플랫화이트 | 9 | 0.9 / 0.9 | 1.3 / 1.3 | 있음 | 2.67 / 4.00 |
| 카푸치노 | 45 | 3.5 / 3.5 | 5.3 / 5.3 | 있음 | 2.67 / 4.00 |
| 아메리카노 | 0 | 0 | 0 | 없음(정상) | — |

판정: 띠 높이 = 부피/용량 × 액체 창 높이 식과 두 해상도에서 일치(PASS). 관찰: 플랫화이트 얇은 거품은 잔 픽셀 1칸 미만(0.9~1.3px)이라 띠 몸통은 거의 보이지 않고 윗면 타일(3.5~5.3px)이 거품을 보여 준다 — 부피 비례 규칙의 결과이며 수치 변경은 하지 않았다. 증거 `foam_zoom_1080_x3.png`, `feedback2-720/1080` 로그 `F2 foam px`.

### 4-3 화살 꼬리(대체 표현 회귀)

`feedback4-720` errors=0 — 전용 아트 전까지 원형 점 + 테두리 유지.

## 5. 아트 대기 상태

- `ArtSource/feedback-art-2026-11-17/`: 제작 진행 중(`TOOL-GATE.md`, provenance), **ready manifest 없음** → 반입·새 11-17 검수본·ZIP은 대기.
- `feedback-art-2026-11-16/manifest.ready.json`은 11-17 12:15 갱신(Unity integrated/verified 기록 필드 추가, 자산 36 동일).
- 남은 일: ready 수신 → 반입(sha·GUID·앵커) → 관련 시험 → 720/1080·좁은 화면 실화면 → `Builds/Feedback-Review-2026-11-17` + ZIP·해시·보고.
