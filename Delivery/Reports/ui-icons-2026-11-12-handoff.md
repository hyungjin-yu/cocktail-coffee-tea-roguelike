# UI 아이콘 41종 — 실제 ID·기능·연결점 인계표 (Unity 담당 → 아트 담당, 2026-11-12, task_68388003a158)

출처: 기획 `ArtProduction/ui-icons-2026-11-12-task.md`, Unity HEAD `80026d4` 코드·데이터 직접 조회(정적 조사만 — Unity/게임 창 실행 없음).
아트 담당 소유: `ArtSource/ui-icons-2026-11-12/` + 매니페스트. Unity 담당 소유: `Assets/` 임포트·`.meta`·C# 연결·시험·빌드·Delivery. **아트 원본은 Unity 쪽에서 수정하지 않는다.**

## 0. 실제 수량(예상 41종과 대조)

| 묶음 | 실제 ID 수 | 비고 |
|---|---|---|
| A. 커피/우림 연구 노드 | **15** (`Resources/Skills/Skill_*.asset`) | 같은 ID를 두 직업이 공용, 티 소믈리에는 이름만 바꿔 표시(`JobStudyTheme.teaNames`). 이름이 커피 도구를 가리키는 7종은 차용 변형이 필요할 수 있음(§A-2) → 최대 **22장** |
| B. 전용 그림 없는 카드 | **14** (`Resources/Cards/Card_*.asset`, `educationArtId` 비어 있음) | 칵테일 6·차 8. `hybrid_tea_martini`는 하이브리드 전용 아이콘이 있어 제외 |
| C. 연구 노트북 숙련 노드 | **12** (`DrinkResearch.Traits`: service/stability/finesse × 4) | 로비(직업 무관) 화면 |
| 합계 | **41**(공용) / 차용 변형까지 최대 48 | 실제 데이터가 예상 41과 일치 |

공통 규격(기존 `Resources/CardEducation/*.png`와 동일): **64×64 px, 투명 배경, 정적 1프레임, 글자 굽지 않음**. 기존 임포트 설정 = Sprite(2D/UI) · Single · PPU 64 · Filter Point · 압축 없음 · 밉맵 없음 · Alpha is Transparency (Unity가 `.meta`로 적용 — 아트 쪽에서 `.meta` 만들지 않음).
상태(잠김·해금 가능·해금됨·장착)는 **기존 테두리/글자로 표시** — 아이콘은 상태별 변형 불필요(Unity가 잠김은 흐리게 처리 예정). 같은 계열은 형태·도구·배치로 구분(색 치환 금지).

## A. 커피/우림 연구 노드 15종

사용 화면(모두 런 중, 두 직업): ① 커피/우림 연구 트리 노드(`SkillTreeView`, 노드 580×140 캔버스 → 왼쪽 그림 칸 **88** 예정 = 720p 59px·1080p 88px), ② 목표 조합 창 커피 조건 칸(`ComboTargetView`, 62 — 현재 공용 원두 `Icons/coffee_bean`; 히든 조건에 쓰이는 노드 11종: balanced_extraction, cold_brew, doppio_body, espresso_shot, fine_grind, full_body_roast, insulated_cup, mellow_finish, perfect_extraction, ristretto, wide_tray), ③ 일시정지 → 연구 트리, 터치 연구 버튼(같은 트리).
현재 대체 그림: **없음**(노드는 글 상자, 갈래 헤더만 공용 `skill_attack/survival/ingredient`).
Unity 연결 예정: `Resources/ResearchIcons/research_<id>.png`(공용), 티 변형이 있으면 `research_<id>_tea.png`를 티 소믈리에에서 우선 사용(없으면 공용으로 대체). Skill 에셋은 수정하지 않음(그중 3개에 다른 사용자 미커밋 변경 있음).

| id | 바텐더 이름 | 티 소믈리에 이름 | 갈래·단계 | 실제 효과 | 공용 판단 |
|---|---|---|---|---|---|
| espresso_shot | 에스프레소 샷 | 첫물 우림 | 공격 1 | 공격력 +20% | **변형 필요** — 에스프레소 잔/포터필터는 "첫물 우림"이 아님 |
| ristretto | 리스트레토 | 농축 우림 | 공격 2 | 공격 속도 +25% | **변형 필요** — 짧은 추출 샷 vs 진한 찻물 |
| balanced_extraction | 균형 추출 | 균형 우림 | 공격 3 | 사거리 +15% | 공용 가능(저울·균형 모티프, 커피/찻잎 특정 도구 없이) |
| full_body_roast | 풀바디 로스트 | 깊은 숙성 | 공격 4 | 공격력 +25% | **변형 필요** — 로스팅 원두 vs 숙성 찻잎 |
| perfect_extraction | 완전 추출 | 완전 우림 | 공격 5 | 연속타/동시발사 +1 | **변형 필요**(추출 도구가 다름) — 또는 중립 '완벽한 한 방울' 모티프면 공용 |
| caffeine_rush | 카페인 각성 | 차 향의 활력 | 생존 1 | 이동 속도 +12% | **변형 필요** — 카페인/커피콩 vs 차 향 |
| doppio_body | 도피오 체력 | 겹겹의 향 | 생존 2 | 최대 체력 +50 | **변형 필요** — 도피오(두 샷) vs 겹친 향 |
| mellow_finish | 부드러운 여운 | 부드러운 여운 | 생존 3 | 분당 회복량 +60 | 공용 가능(이름 같음, 김·온기 모티프) |
| insulated_cup | 보온 컵 | 보온 찻잔 | 생존 4 | 최대 체력 +30 | 공용 가능 조건부(중립 보온 용기) — 머그로 그리면 차 쪽에 변형 |
| steady_hand | 흔들림 없는 손 | 안정된 찻손 | 생존 5 | 받은 피해 -15% | 공용 가능(손+따르는 물줄기, 용기 중립) |
| cold_brew | 콜드브루 | 냉침 | 재료 1 | 재료 게이지 필요량 -10% | 공용 가능(찬물 우림 병·얼음 — 두 음료 공통 기법) |
| fine_grind | 곱게 갈기 | 찻잎 고르기 | 재료 2 | 재료 추가 드랍 확률 +15% | **변형 필요** — 그라인더 vs 찻잎 선별 |
| wide_tray | 넓은 트레이 | 다구 쟁반 | 재료 3 | 줍기 반경 +100cm | 공용 가능(넓은 쟁반) |
| measured_pour | 정량 따르기 | 정량 따르기 | 재료 4 | 재료 게이지 필요량 -10% | 공용 가능(이름 같음, 눈금 계량) |
| zero_waste | 남김없는 추출 | 찻물 남김없이 | 재료 5 | 재료 추가 드랍 +20%, 줍기 반경 +50cm | 공용 가능(마지막 한 방울까지) |

같은 효과 쌍(기능 구분은 그림 형태로): 공격력 +20/+25(espresso_shot/full_body_roast), 게이지 -10%(cold_brew/measured_pour), 최대 체력 +50/+30(doppio_body/insulated_cup).

## B. 전용 그림 없는 카드 14종

사용 화면: 카드 선택(이름판 왼쪽 **88**), 카드 학습 안내 1/4·지식 상세(`CardEducationGuide` 300×140 칸), 도감 목록(38)·상세, 목표 조합 창(62), HUD 목표 칸(42), 최근 기록 상세 카드 행, 결과 '이번 판 빌드'. 두 직업(칵테일 카드는 바텐더 런에서만 카드 창에 나옴, 도감·기록은 공통).
현재 대체 그림: 칵테일 = 공용 칵테일 재료 아이콘(주황 병), 차 = 공용 찻잎 더미.
Unity 연결 예정: `Resources/CardEducation/card_<slug>.png` + 해당 `Card_<id>.asset`의 `educationArtId = card_<slug>`(14개 에셋 모두 다른 사용자 미커밋 변경 없음). 아래 slug는 제안 — 아트 매니페스트가 다른 이름이면 매니페스트를 따른다.

| id | 표시 이름 | 계열·희귀 | 게임 효과 | 실제 의미(카드 지식) | 제안 파일 |
|---|---|---|---|---|---|
| bar_shoes | 빌드 | 칵테일·일반 | 이동 속도 +10% | 제공할 잔에 재료를 직접 넣어 만드는 방식 | card_build |
| champagne_spray | 스플릿 베이스 | 칵테일·희귀 | 다중 발사 +1발(부채꼴) | 베이스를 두 가지 이상의 술로 나눔 | card_split_base |
| ice_cube | 얼음과 희석 | 칵테일·일반 | 최대 체력 +20 | 얼음 냉각·녹은 물이 맛과 농도에 관여 | card_ice_dilution |
| liqueur | 리큐르 | 칵테일·일반 | 재료 게이지 필요량 -8% | 당·과일·허브 풍미로 베이스 보완 | card_liqueur |
| molotov | 플레임드 제스트 | 칵테일·희귀 | 적중 화염 Lv +1·3초 | 감귤 껍질 향유와 불꽃 연출 | card_flamed_zest |
| syrup | 프리배치 | 칵테일·일반 | 재료 추가 드랍 +10% | 여러 잔 분량 재료를 미리 준비 | card_prebatch |
| dried_leaf | 찻잎 건조 | 차·일반 | 재료 추가 드랍 +10% | 제조 중 수분 감소(햇볕만은 아님) | card_leaf_drying |
| golden_ratio | 추출 균형 | 차·영웅 | 공격력 +25%, 공격 속도 +20% | 찻잎량·물·온도·시간 | card_brew_balance |
| honey | 티스푼 계량 | 차·일반 | 재료 게이지 필요량 -8% | 잎 양 가늠(부피≠무게) | card_teaspoon |
| long_spout | 긴 주둥이 주전자 | 차·일반 | 사거리 +15% | 가늘고 긴 주둥이로 물줄기 조절 | card_long_spout |
| nightshade_blend | 홍차의 떫은 여운 | 차·영웅 | 적중 여운 피해 Lv +2·3초 | 산화 가공 찻나무 차의 맛·잔향 | card_black_tea_finish |
| piercing_arrowhead | 차 거름망 | 차·영웅 | 발사체 관통 +1 | 우린 액체와 찻잎 분리 도구 | card_tea_strainer |
| tea_leaf_scatter | 블렌딩 | 차·희귀 | 다중 발사 +1발(부채꼴) | 서로 다른 차·재료를 섞음 | card_blending |
| warm_cup | 찻잔 예열 | 차·일반 | 최대 체력 +20 | 담기 전 잔을 데움 | card_cup_warming |

혼동 주의(같은 효과): 다중 발사 champagne_spray/tea_leaf_scatter, 드랍 syrup/dried_leaf, 게이지 liqueur/honey, 체력 ice_cube/warm_cup — 계열(칵테일/차) 도구로 구분. 기존 그림과 겹침 주의: card_double_strain(스트레이너·더블 스트레인 공용 중) ↔ 차 거름망은 다른 형태.

## C. 연구 노트북 숙련 노드 12종

사용 화면: 로비 → 연구 노트북 → 숙련 트리 탭(`ResearchLaptopView`), 노드 420×74 판(상태 테두리 아트 `ProgressionUI/tree_node_locked|available|owned|equipped`) 안 왼쪽 그림 칸 **56** 예정, 오른쪽 상세 제목 옆 **88** 예정. 직업 무관(로비) → 커피/차 구분 없음, 카페 서비스 공용 모티프면 됨.
현재 대체 그림: 없음(글 버튼). Unity 연결 예정: `Resources/ResearchIcons/mastery_<id>.png`. 비용 4/6/8/10 RP(단계별), 장착 슬롯 3.

| id | 이름 | 갈래·단계 | 실제 효과 |
|---|---|---|---|
| service_1 | 첫 잔의 기세 | 서비스 1 | 런 시작 30초 동안 피해 +8% |
| service_2 | 단골 응대 | 서비스 2 | 같은 적에게 주무기 직접 적중 3회째 피해 +15%(대상 변경·2초 미적중 시 초기화) |
| service_3 | 시그니처 준비 | 서비스 3 | 출발 전 고른 목표 조합을 런 중 완성하면 30초 피해 +10%(1회) |
| service_4 | 마감 집중 | 서비스 4 | 최종 보스 생존 중 주무기 공격 속도 +5% |
| stability_1 | 여유로운 서비스 | 안정 1 | 피해 없이 8초 유지 시 이동 속도 +4%(피격 해제) |
| stability_2 | 마지막 한 모금 | 안정 2 | 체력 25% 이하 첫 진입 시 4초 받는 피해 -10%p(런당 1회) |
| stability_3 | 쉬는 시간 | 안정 3 | 보스 처치 후 최대 체력 3% 회복(보스별 1회) |
| stability_4 | 침착한 복귀 | 안정 4 | 체력 30% 이하에서 이동 속도 +6% |
| finesse_1 | 첫 타의 정확성 | 기교 1 | 체력 가득한 적에 첫 주무기 직접 적중 피해 +8% |
| finesse_2 | 거리 조절 | 기교 2 | 주무기 유효 사거리 70% 이상 거리 직접 적중 피해 +5% |
| finesse_3 | 다채로운 서비스 | 기교 3 | 서로 다른 칵테일·차 카드 4종 이상 보유 시 치명타 +1%p |
| finesse_4 | 한 잔의 집중 | 기교 4 | 출발 음료 양수 전투 버프 105%(항목별 상한 유지) |

(`DrinkResearch.cs`에 다른 사용자 미커밋 변경 있음 — 효과 문구 표기 `×1.05`→`105%` 등만, ID·이름 동일.)

## D. 인계 절차(아트 → Unity)

1. 아트 담당이 묶음별 `ArtSource/ui-icons-2026-11-12/` 원본(.aseprite)·PNG·매니페스트(실제 ID → 파일, 크기·알파·해시, ready 여부)를 코디네이터로 알림.
2. Unity 담당은 **매니페스트에 있는 ready 파일만** `Assets/_Project/Resources/...`로 복사(원본 불변, 해시 대조), `.meta`는 Unity가 생성 후 위 임포트 설정 확인, 기존 `.meta` 있는 파일은 보존.
3. 연결·시험 후 새 QA 빌드에서 720p/1080p·잠김/해금 가능/해금/장착·두 직업 캡처 → integrated/verified 표기. Unity·게임 창 실행은 아트 GUI 슬롯과 코디네이터 조율 후.
