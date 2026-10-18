namespace CCT { public static class CardEducationSummary {
public static string Effect(CardData c) => c.id switch {
"molotov" => "적중 화염 Lv +1 · 3초 지속",
"blazer" => "적중 화염 Lv +2 · 3초 지속",
"nettle_tea" => "적중 여운 피해 Lv +1 · 3초",
"nightshade_blend" => "적중 여운 피해 Lv +2 · 3초",
"champagne_spray" or "tea_leaf_scatter" => "다중 발사 +1발",
"flambe" or "second_infusion" => "연속 타격 +1회",
"rifled_barrel" or "piercing_arrowhead" => "발사체 관통 +1",
"hybrid_tea_martini" => "보조 활 · 자동 화살 발사",
_ => c.description };
public static string Fact(CardData c) { switch(c.id) {
case "dry_gin": return "주니퍼 향이 특징인 증류주. 드라이는 단맛 관련 표현입니다.";
case "shot_glass": return "재료의 양을 재는 도구. 용량은 제품마다 다릅니다.";
case "long_stirrer": return "긴 손잡이로 얼음과 재료를 저어 섞는 바 도구입니다.";
case "bar_shoes": return "제공할 잔에 재료를 직접 넣어 만드는 방식입니다.";
case "ice_cube": return "얼음은 냉각하고 녹은 물은 맛과 농도에 관여합니다.";
case "liqueur": return "당과 과일·허브 등의 풍미로 베이스를 보완하는 술입니다.";
case "syrup": return "서비스 전에 여러 잔 분량의 재료를 미리 준비합니다.";
case "double_shot": return "IBA는 진·캄파리·스위트 레드 베르무트를 동량으로 씁니다.";
case "mixing_tempo": return "얼음과 흔들어 혼합·냉각·희석하고 질감도 바꿉니다.";
case "flambe": return "바 스트레이너와 고운 체로 작은 고형물을 거릅니다.";
case "champagne_spray": return "베이스 역할을 두 가지 이상의 술로 나눠 구성합니다.";
case "molotov": return "감귤 껍질 향유와 불꽃을 이용하는 전문 바 연출입니다.";
case "scotch_rocks": return "위스키·설탕·비터스·물로 바탕 술의 풍미를 살립니다.";
case "dry_martini": return "진과 드라이 베르무트를 얼음과 젓는 클래식입니다.";
case "blazer": return "불붙인 위스키와 물을 잔 사이로 옮기는 전문 연출입니다.";
case "rifled_barrel": return "따르는 액체에서 얼음·과육 같은 고형물을 거릅니다.";
case "green_tea": return "찻나무 잎을 가열해 효소적 산화를 억제한 차입니다.";
case "tea_whisk": return "말차 가루를 물에 풀어 섞는 대나무 도구입니다.";
case "long_spout": return "가늘고 긴 주둥이로 따르는 물줄기를 조절합니다.";
case "tea_sandals": return "우린 차를 따라 찻잎과 차탕을 분리하는 과정입니다.";
case "warm_cup": return "차를 담기 전 잔을 데워 온도 손실을 줄입니다.";
case "honey": return "잎 양을 가늠하지만 부피는 무게와 같지 않습니다.";
case "dried_leaf": return "제조 중 수분을 줄입니다. 햇볕만 쓰지는 않습니다.";
case "oolong": return "부분 산화한 차. 산화와 로스팅의 폭이 다양합니다.";
case "brew_rhythm": return "작은 다구로 잎 양·시간을 조절하며 여러 번 우립니다.";
case "second_infusion": return "같은 잎을 다시 우려 풍미 변화를 살펴봅니다.";
case "tea_leaf_scatter": return "서로 다른 차 또는 다른 재료를 섞어 풍미를 만듭니다.";
case "chamomile": return "찻나무 잎이 아닌 카모마일 꽃을 우린 허브 음료입니다.";
case "nettle_tea": return "떫은맛과 마신 뒤 남는 감각. 차의 독성을 뜻하지 않습니다.";
case "aged_puerh": return "미생물 발효를 촉진하는 악퇴 가공을 한 보이차입니다.";
case "golden_ratio": return "찻잎량·물·온도·시간이 맛에 영향을 줍니다.";
case "ginseng_root": return "찻나무가 아닌 남아프리카 식물로 만든 음료입니다.";
case "nightshade_blend": return "산화 가공한 찻나무 차의 맛과 잔향을 가리킵니다.";
case "piercing_arrowhead": return "우린 액체와 찻잎을 분리하는 도구입니다.";
case "hybrid_tea_martini": return "차 향을 더한 마티니 변형. 티니 보우는 창작 무기입니다.";
default: return c.realWorldFact; } }
public static string Connection(CardData c) { switch(c.id) {
case "dry_gin": return "베이스의 존재감 → 공격력";
case "shot_glass": return "반복 계량의 리듬 → 공격 속도";
case "long_stirrer": return "긴 도구 → 사거리";
case "bar_shoes": return "간결한 동선 → 이동 속도";
case "ice_cube": return "맛의 완충 → 최대 체력";
case "liqueur": return "소량의 풍미 보완 → 게이지 절약";
case "syrup": return "준비한 분량 → 추가 재료";
case "double_shot": return "세 재료의 구성 → 공격력";
case "mixing_tempo": return "반복 동작 → 공격 속도";
case "flambe": return "두 단계 통과 → 연속 타격";
case "champagne_spray": return "나눠진 기반 → 다중 발사";
case "molotov": return "순간 불꽃 → 적중 화염";
case "scotch_rocks": return "베이스 중심 → 공격력";
case "dry_martini": return "존재감과 스티어 → 공격·공속";
case "blazer": return "이어지는 불꽃 → 적중 화염";
case "rifled_barrel": return "액체의 통과 → 관통";
case "green_tea": return "산뜻한 풍미의 인상 → 공격력";
case "tea_whisk": return "여러 가닥의 움직임 → 공속";
case "long_spout": return "물줄기의 길이 → 사거리";
case "tea_sandals": return "제때 따라내는 동작 → 이동";
case "warm_cup": return "온도 완충 → 최대 체력";
case "honey": return "분량 조절 → 게이지 절약";
case "dried_leaf": return "재료 확보 → 추가 재료";
case "oolong": return "겹치는 풍미 → 공격력";
case "brew_rhythm": return "짧은 우림 반복 → 공속";
case "second_infusion": return "한 번 더 이어짐 → 연속 타격";
case "tea_leaf_scatter": return "여러 갈래 재료 → 다중 발사";
case "chamomile": return "쉬어 가는 인상 → 게임 회복";
case "nettle_tea": return "남는 감각 → 게임 지속 피해";
case "aged_puerh": return "가공으로 형성된 풍미 → 공격력";
case "golden_ratio": return "조절의 균형 → 공격·공속";
case "ginseng_root": return "쉬어 가는 인상 → 게임 회복";
case "nightshade_blend": return "남는 감각 → 게임 지속 피해";
case "piercing_arrowhead": return "차탕의 통과 → 관통";
case "hybrid_tea_martini": return "두 테마의 결합 → 보조 활";
default: return c.gameplayConnection; } } } }
