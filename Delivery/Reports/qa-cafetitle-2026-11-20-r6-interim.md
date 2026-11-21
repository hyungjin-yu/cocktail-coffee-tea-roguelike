# 중간 기록 — 카페 메뉴 접근·조작 연출 + v7 거리 배경 + 메뉴 나무·종이 톤 (2026-11-20, 라디오 수정 대기)

상태: **통합·자동 검수 진행 중, 최종 아님.** 라디오(settings) 조작 cel 판독 불가로 Codex 국소 수정 대기. 최종 ZIP 미작성. r5 이하 보존.

## 반입(해시 일치)
- v7 거리 배경: layout 89692689…, `g0.png` 교체·`street_backdrop.png` 추가(z -10).
- v9 메뉴 조작: manifest e6611b02…, layout a93a383f…(v7 대비 approach 8블록만), `ready/approach/*` 48 PNG(`Delivery/Verification/2026-11-20/r6/import-v9.json`). 독립 확인: 8개 모두 첫 프레임 = 원 물체 레이어, 레이어 id 존재, 변화 10~1760 art px.

## 코드
- `UI/CafeMenuApproach.cs`(신규): 확정 → 시점 이동(실내 카메라 center/zoom, 같은 contain 변환·배우 없음) → 물체 레이어 조작 프레임 → 모든 입력 떼어진 다음 프레임에 기존 메뉴. 닫기 → 역재생·원시점·입력 재개. 진행 중 중복/다른 메뉴 차단, 새 입력=건너뛰기, Esc=취소(메뉴 미열림), 저이펙트=이동 없음·마지막 프레임 0.15초. 계약 없는 물체는 바로 열기(중간 fallback — 현재 8개 모두 계약 있음). 시작: 카운터 연출 뒤 기존 직업 선택, 미래 보호면 원위치. 종료: 문 연출 뒤 기존 RequestQuit(저장·QuitGuard 선행), 확인창 취소 시 원위치.
- `UI/CafeTitleLayout.cs`: feature.approach 파싱(camera/seconds/returnSeconds/action{layer,frames,fps,holdSeconds,reverseOnReturn,rect,hideLayer}), 범위 제한.
- `UI/CafeTitleScene.cs`: 명패 숨김/복귀, 카메라 조회, 조작 중 조명 사본 억제.
- `UI/TitleView.cs`: 물체 확정을 접근 연출로 감쌈(같은 기존 동작), 입력 게이트·Tab 차단, 메뉴 닫기 → 원위치 후 기본 선택, 접근 중 상단 제목 숨김.
- `UI/CafeMenuTheme.cs`(신규): 타이틀이 만든 메뉴 6화면에만(전투 일시정지·결과·직업 선택 인스턴스 미적용) — 카페 패널 그림(흰 틴트) → 따뜻한 곱 틴트(1,.92,.62), 무채색·보라 단색 → 나무/종이(어두운 바탕만 0.05+1.15×휘도로 조금 밝힘), 글자 휘도 유지, 빨강·파랑/청록·초록·금색·알파 유지.
- `Core/CafeTitleCapture.cs`(QA): 8메뉴 접근·조작·메뉴·원위치, 중복/건너뛰기/취소, 저이펙트, 미래 보호 시작 원위치, 저장 실패 종료 확인 전 문 연출·취소 원위치 검사.

## 검사(자동, 사람 플레이 아님)
| 검사 | 결과 |
|---|---|
| EditMode `TitleLayoutTests|FeedbackBatch3Tests` | 17/17 (테마 보완 후 재실행 포함) |
| PlayMode `TitleViewTests|KeyboardNavigationTests|PauseView|SettingsView|ResultView` | 28/28 |
| r6 빌드 실검수(`Delivery/Verification/2026-11-20/r6/`) | normal 720/1080/4:3 각 85/0, low720 8/0, future720 21/0, savefail720 22/0, np720 7/0 |
| 보완 빌드(제목 숨김·테마 보정) normal1080 | 85/0 (`…/r6b/`), 메뉴 톤 r5 대비 `r6b/menu_theme_r5_vs_r6b_1080.png` |

## 판독(캡처 기준)
- 노트북 화면 켜짐·책 펼침·앞치마 이동·장부 펼침·게시물·카운터 컵·출입문: 읽힘(`r6/contact_acting_1080.png`).
- 라디오: **판독 불가** — 3×6 노브에서 밝은 1픽셀이 위치만 바뀜, 표시등 변화 안 보임(`r6/radio_frames_{720,1080,4x3}.png`). Codex 국소 수정 요청.
- 메뉴 톤: 보라→나무 갈색, 글자 대비·의미 색 유지(사람 체감 미검증).

## 남은 것
라디오 수정 반입 → 3해상도 라디오 연속 프레임·저이펙트 최종 상태·원복/가림 검증 → 최종 빌드·ZIP·SHA·보고. 사람 플레이 체감 미검증.
