# QA — 11월 22일 플레이 피드백 정식 픽셀 아트 21종 반입 (2026-11-23)

- 작업 공간 `coffee-brewing`, 브랜치 `codex/coffee-brewing`, 아트 기준 커밋 `7fb00e4`. 반입 원본 `ArtSource/feedback-art-2026-11-22/revisions/v1-formal-pixels`(HANDOFF.md, manifest.ready.json, revision 1).
- **범위 제한(코디네이터 지시, 주인이 다른 게임 플레이 중)**: 창 띄우기·OS 입력·Unity 전체 테스트·무거운 빌드·QA 드라이버/캡처는 하지 않았다. 이 문서의 모든 런타임 항목은 `NOT RUN`이다.
- Unity 시작 전 여유 RAM 확인: 여유 약 1.1 GB / 전체 15.8 GB(< 4 GB 기준) → **Unity 실행 금지, 중단 상태로 보고**. 사용자 앱은 닫지 않았다.

## 1. 반입 내용

| 항목 | 내용 |
|---|---|
| 반입 도구 | `Tools/Art/import_feedback_art_2026_11_22.py` — manifest `destination`으로 복사 후 SHA-256 대조, 신규 PNG에 .meta(Point·비압축·mipmap 끔·FullRect·128 PPU) 생성 |
| 몸체 46장 | `Resources/FeedbackArt/bitter_mist`(21), `glass_thief`(25). 128px, 발 피벗 (0.5, 0.125). 기존 ArtV1 256px 일괄 검수표와 분리해 `FeedbackArt`(Core)에 별도 등록, `ArtLibrary.Get`이 목록에 없는 ID를 여기서 조회 |
| 바닥 효과 20장 | `bitter_mist_pulse`(charge 6·burst 4), `mixing_rush_aura`(active 6·off 4) |
| HUD 2장 | `Resources/FeedbackArt/Status/status_attack_speed_down.png`, `status_heal_block.png` |
| 능력치 15장 | `Resources/StatIcons/stat_*.png` 동일 파일명 교체, 기존 .meta 보존 |
| 공용 기존 몹 파일 | `swirl_stick_conjuror`, `milk_foamer_bgfix` 미변경(fallback 경로로만 유지) |
| 임시 아이콘 생성기 | `Tools/Art/make_stat_icons.py` 삭제(다시 돌리면 정식 아이콘을 16px 임시본으로 덮어쓰므로) |

## 2. 코드 변경(표시 전용, 판정 불변)

- `Enemy.cs`: BitterMist/GlassThief `artId` 지정, `iconTint` 흰색(임시 틴트 제거).
- `DebuffMobs.cs`: 절차 원(PulseWarn/PulseEdge)을 `bitter_mist_pulse`로 교체(예고 1초 = charge 6장·6fps, 바깥 테두리 가로 244px = 반경 2.4×2, Y 압축·코드 틴트 없음, 파동 시 burst). 몸체 `action`은 예고 시작에 재생하고 1초까지 마지막 컷 유지. 도둑: 머리 위 임시 원 제거, 접촉 시 `action` 후 `flee` 반복(방향은 실제 도주 속도 — 기존 `Enemy.TickBodyAnimation`). 2.8 거리 깜빡임·접촉/도주 타이머·판정 수치 그대로.
- `MixingRushAura.cs`: 절차 원(Disc/Ring)을 `mixing_rush_aura`로 교체(active 반복, off 4컷을 기존 0.45초에 샘플링, 저이펙트 = active 첫 컷·꺼짐 생략).
- `HudView.cs`: `AttackSlowed` → `status_attack_speed_down`, `HealBlocked` → `status_heal_block`(함께 표시 가능), `status_slow`는 이동 둔화 전용. 아이콘 2칸 늘어난 만큼 상태 문구를 88px 오른쪽으로.
- 아트 누락 시 기존 절차 표현으로 떨어지는 fallback 유지.
- 시험: `PlayFeedback1122RushDebuffTests` 틴트 단언 갱신, `FeedbackArtFormalTests` 신규(장수·피벗·예고 1초·아이콘 크기).

## 3. 검증

### 3-1. 정적(실행함)

| # | 검사 | 방법 | 기대 | 실제 | 상태 |
|---|---|---|---|---|---|
| S1 | 반입 83개 해시 | 반입 스크립트 + 별도 python 재대조(manifest sha256 vs Resources 파일) | 83/83 일치 | 83/83 일치, 불일치 0 | PASS |
| S2 | 신규 .meta 68개 | 파일 파싱: Point, 비압축, mipmap 끔, FullRect, 128 PPU, 몸체 피벗 0.125/Custom | 전부 충족 | 68/68 충족 | PASS |
| S3 | `FeedbackArt` 클립 표 = manifest | 클립 이름·장수·fps·반복 문자열 대조 | 불일치 없음 | 불일치 없음 | PASS |
| S4 | 능력치 .meta 보존 | `git status`: stat PNG 15개만 M, .meta 변경 없음 | .meta 변경 0 | .meta 변경 0 | PASS |

### 3-2. 컴파일·시험·런타임

| # | 검사 | 상태 | 비고 |
|---|---|---|---|
| B1 | Unity batchmode 컴파일 | NOT RUN | RAM 1.1 GB·주인 플레이 중 |
| T1 | EditMode / PlayMode batchmode | NOT RUN | 같은 이유. 신규·수정 시험은 아직 한 번도 실행되지 않음 |
| R1 | 720/1080/4:3·저이펙트 투어 캡처 | NOT RUN | 주인 허락·여유 RAM 필요 |
| R2 | 디버프 몹(쓴맛 안개 예고 1초·도둑 flee) 캡처 | NOT RUN | 〃 |
| R3 | 혼합 러시 오라 켜짐/꺼짐/저이펙트 캡처 | NOT RUN | 〃 |
| R4 | HUD 상태 아이콘 2종 + 문구 겹침 | NOT RUN | 〃 |
| R5 | 안개/도둑 화면상 크기(`targetBodyHeightWorld` 1.5 / 1.15 추정치) | NOT RUN | 실제 캡처로 조정 필요 |

## 4. 남은 위험

- 코드는 컴파일되지 않았다(Unity 미실행). 첫 Unity 실행에서 컴파일·시험·.meta 재직렬화 변경 여부를 확인해야 한다.
- 몸체 목표 높이는 추정값(기존 같은 반경 계열 일반 몹 기준). 오라 가로 200px 기준 배율도 캡처 확인 필요.
- 아이콘은 PPU 16 .meta를 보존(HANDOFF 지시)했고 HUD/카드에서 크기를 명시 지정하는 경로이므로 영향 없을 것으로 보지만 화면 확인 전이다.
