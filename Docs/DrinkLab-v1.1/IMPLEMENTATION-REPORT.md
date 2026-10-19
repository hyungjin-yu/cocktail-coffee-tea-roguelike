# v1.1 음료 제조 · 72 성장/연구/판매 — 재개 작업 보고 (2026-10-19, task_1a31889fdc1f / ctx_d2350d3e1d0b)

상태 구분: **implementation + 자동 회귀 + 실제 화면 캡처 완료 / root 최종 판정 대기**. 최종 game_complete를 주장하지 않는다.
Git: 코디네이터가 이전 작업을 dec3311로 백업한 뒤 이어 진행했다. 구현 담당은 Git index를 변경하지 않았으며 최종 백업은 코디네이터가 담당한다.
실제 세이브·Play-Latest·Android 제출물 미접촉(모든 실행은 `-cct-data-dir` 격리 저장, 테스트는 임시 경로).

## 최종 회귀 (최종 소스, 1회)
- 전체 EditMode `edit_full3` **725/725**
- 전체 PlayMode `play_full3` **283 통과 / 0 실패 / 4 무시**(기존 SkinEvidenceCapture: `CCT_EVIDENCE_DIR` 있을 때만 도는 증거 캡처)
- XML: `Final-regression/` (아래 복사)

## 단계별 결과와 증거 폴더
| 항목 | 내용 | 증거 |
|---|---|---|
| 단계1 제조 물리 | 전 과정 양·질량·성분 보존(선택 재료 구간 변동 0, stir 55.1mL, tea 160mL·잎+추출 2.5g 일정), 접근 중 지거 내용물 유지, 스푼 관통 0/바닥 6px/벽 8px, 얼음 0/2/4/최대 + 안정층 | `Stage1-resume-s1i-2026-10-19`, `Backdrop-integrated-*/stage1_s1m` |
| 색 깜박임 원인 | `ColorOfParts`가 같은 성분 조각마다 착색 상한을 더해 띠 구성에 따라 진해짐 → 성분 합친 뒤 계산(+회귀 테스트) | `Stage1-resume-s1i`/colors.txt |
| 다관 뚜껑 | 분리 아트 임포트, 열기→옆 탁자→투입→닫기→우림(추출은 lidOpen=0일 때만)→따르기(닫힌 채), 단계 문구 | `Fix-bg-lid-2026-10-19/tea_lid` |
| 제조 배경 | CraftingBackdrops 벽 800×574·상판 800×24(정수 2배), 잔·다관 실제 알파 바닥=상판 윗면, 불투명 정보띠 | `Backdrop-integrated-2026-10-19` |
| 카메라 | 장면 층만 팬(SmoothDamp 0.18), 편집→옮겨 붓기→제조→따르기→완성 잔 가운데(camX=serveX) | `Camera-2026-10-19` |
| 제조 UI | 상태 23종 × 720/1080/터치/836×705(좁은 창 전용 배치), 자동검사 0 | `UI-resume-2026-10-19`, `Backdrop-integrated-*/ui_*` |
| 72 수급·RP | 고정 묶음+보유량 보정+희귀 보장, 일반 25/정예/보스, RP 정산(보존 포기 제외), 첫 10RP 1회 | `Stage3-4-progress-*`, run72 노트 |
| 숙련 12/직원 4 | 해금·장착 분리, 3칸, 런 시작 스냅샷, 전투 효과(직접 타격 합산·치명·속도·피해감소·보스 회복), 직원 전문 ×1.08 제조 때 고정, 한 잔의 집중 ×1.05 | 테스트 + run72(0.0583→0.0612) |
| 판매·외형 | 주문 3칸·가격 100/125/150·판매=음료 소비+코인 한 거래·취소 갱신, 코인 외형(2스킨·공용 카페 맵) 현금 소유와 합산 | `Stage3-4-progress-*` P20~P24 |
| 노트북 | 손 없는 등장/열기/점등/닫기(아트 프레임), 직업별 바/찻집 배경, 숙련 트리·직원 채용·기존 강화 | `Laptop-room-2026-10-19`, P30~P37 |
| 레시피 서랍 | RecipeDrawer 승인 아트 기하 그대로, 5장 칸·실제 레이캐스트 선택·꺼내 펼침·되돌리기·삭제/복제, 빈 서랍·30장, 찢기 00~05 → 서랍으로 | P40~P44, D0/D1 |

## 근사·한계 (명시)
- 유체는 1차원 부피 셀 축약 모델(CFD 아님), 부피 가산 혼합, 얼음 질량 고정(융해 없음). 2D 마스크 회전체 근사.
- 72 수치(묶음·가중치·RP·가격·외형 가격)는 최초 기본값이며 플레이 밸런스 실측 아님.
- 숙련 효과 중 '시그니처 준비'는 기존 목표 조합(출발 전 기억된 목표 또는 런 중 T로 고른 목표)을 완성했을 때 1회 발동.
- 서랍·종이 효과음은 기존 UI 효과음 재사용(새 사운드 제작 없음).
- 좁은 창(비율 1.18 미만)은 기존 폭 맞춤. 기존 선택 화면 등 다른 화면의 836 최소 글자는 이번 범위 밖.
- 사람 손 조작 감각(드래그 세기 등)은 자동 캡처로 대신할 수 없음 — 사용자 확인 필요.

## 납품물
- 최종 실행 폴더: `Builds/Playable-2026-10-19-drinklab-v1.1` / ZIP: `Builds/CocktailCoffeeTea-DrinkLab-v1.1-2026-10-19-win64.zip`(+.sha256)
- 실행: `Delivery/DrinkLab-v1.1/Play-DrinkLab-v1.1-MVP.cmd`(격리 저장), 테스트 재료: `...-TestMaterials.cmd`
- 새 폴더 압축 해제 실행 결과: 아래 "새 폴더 실행" 절
- 임포트 도구: `Tools/DrinkLab/import_teapot_lid.py`, `import_crafting_backdrops.py`, `import_research_staff.py`, `import_recipe_drawer.py`(모두 sha 검증, ArtSource 읽기만)

## 새 폴더 실행
- ZIP SHA256 `277DF39689C4D79A21ABF1AF3B4EE79A89FF3628CC0B94C20A24308126456073` (106,272,703 bytes, DoNotShip 폴더 제외)
- `C:/UnityScratch/r73/fresh-final`에 압축 해제 → `-cct-data-dir C:/UnityScratch/r73/fresh-final-data`로 실행: 30초 생존, 플레이어 로그 Exception 0.
- 격리 폴더에만 `drinks.json`(+.bak) 생성: schemaVersion 2, 첫 제조 세트 지급, 연구 포인트 첫 10 지급(1회 플래그), 주문 3칸(진+레몬 / 홍차+꿀 / 럼+블루 큐라소), 코인 0.
- 실제 세이브·Play-Latest·Android 제출물 미접촉. 스토어·GitHub 게시 없음.
- 실제 런 통합 캡처(QA 빌드): `Final-regression/run72_capture` (report.json notes: 런 버프·수급·RP·결과 화면 줄).


Root final review: accepted latest pr720i/pr836i drawer, run72b empty/30 recipes and settlement, ui720j manufacturing, s1o label clamp, lap720c actual closed/open bar and tea laptop rooms. Final ZIP hash independently matched. See 74 results note for scope and approximations.
