# 상성 적중 VFX 대표 검수

기존핵심15clip은코디네이터Unity인수승인후고정했으며덮어쓰지않음. 이번작업은affinity/신규파일만.

실제로열어읽은원본: runtime leaf_mote128(초록잎/짙은윤곽),coffee_pool256(커피갈색),hybrid_tea_martini128(청록이펙트)와앞서검수한steam_jet/hit_spark. 원본그림을새효과라고주장하지않고팔레트/밀도참조로사용. 01-affinity-comparison.png는원본과신규대표native/nearest2x/64px프록시이며직접열어확인.

대표: cocktail_foam은속빈거품4개+작은거품바닥,coffee_bean_steam은골이있는두원두와한줄증기,tea_leaf_wind는두잎과뒤따르는열린바람호. 모두중심에서위/오른쪽으로흐르는작은적중효과.128x128/PPU128/pivot(.5,.5)/Point/기본alpha/AirVfx4. 예상full6frame83ms=498ms1회,low3frame249ms1회,마지막완전투명. 원두/잎/속빈원은색을빼도형태가다름. 생성수나프레임수로미적PASS주장하지않음.

실제도구:Aseprite배치Lua작화/native+PNG저장. Pillow는원본읽기와비교판합성/해시만. PixelLab설치되어있지만사용/생성0,외부AI0,GUI0. 대표3native/PNG는1frame뿐,리듬/저효과아직미제작. 실물대표검수후확장. 게임상성발동조건/실피해/히트방향/과밀도/작은실제화면은구현담당runtime검수필수.

## 완료 체크포인트

대표승인후full3/low3=6clip27노출완료. manifest/native/개별PNG/투명sheet/GIF/dark-light-brown전프레임비교모두affinity하위. 모든6contact와64px비교를실제로열어확인. Native재열기export27/27pixel-exact,투명경계와binaryalpha27/27,전clip마지막완전투명. 커피밝은골+선행2px하이라이트보존,거품하단짙은청록보정. core-freeze-check로상위42체크포인트파일및15native+83PNG원래해시유지확인. 실제runtime연속재생/상성실피해1회연결/풀링초기화/동시예산/전투가림검수는Claude인수사항이며미검증. 새컵미제작,실제열어확인한기존32x26 dl_coffee_cup를64x52UI비율보존으로재사용계약. PixelLab0/외부AI0/GUI0. 최종인수목록과재개점은affinity/HANDOFF.md,후속수정필요시기존고정본을덮지않고revision추가.
