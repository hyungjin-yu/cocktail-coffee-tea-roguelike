# 전체 아트 인수 체크포인트

작업폴더: `ArtSource/master-challenge-2026-11-11/`만. 기존핵심15clip은코디네이터가Unity인수승인했고,승인이후파일고정. 상성후속은`affinity/`만추가. Assets/C#/meta/Git/스킨/공모전/개인세이브변경없음.

## 핵심15clip 인수

상위 `HANDOFF.md`, `manifest.json`, `validation.json`, `native-frames.csv`, `checkpoint-sha256.txt`. NPC2역할의walk_in/install/idle/walk_out8clip, 무문자현수막1clip, 품질full3+low3clip. 83노출의시간/손anchor/PPU128/pivot/파일해시/native레이어/tag/재사용내역은상위manifest그대로. 83/83native재열기PNG일치·투명경계검사. 대표승인→수정NPC승인→확장15clip의Unity인수승인까지완료. **runtime합성승인이아님.** 상위문서의pending표기는인수승인이전작성본으로보존했고실제게임내검수는계속미검증.

핵심고정검사: `core-freeze-check.json`에서상위42개체크포인트파일및**15native+83PNG**를이전저장해시와재비교하여모두일치. 보정요청시기존파일덮지않고revision별도저장.

## 상성6clip 인수

`affinity/manifest.json`, `validation.json`, `native-frames.csv`, `frames/`와아래6native/6sheet/6contact/6GIF. 대표01comparison은코디네이터실물승인후확장.

| 이름 | 노출/clip내고유pixel | 시간 |
|---|---:|---:|
| cocktail_foam | 6/6 | 498ms |
| coffee_bean_steam | 6/6 | 498ms |
| tea_leaf_wind | 6/6 | 498ms |
| cocktail_foam_low | 3/3 | 249ms |
| coffee_bean_steam_low | 3/3 | 249ms |
| tea_leaf_wind_low | 3/3 | 249ms |

128x128,PPU128,pivot(.5,.5),Point/Clamp/무압축/mipmap없음/표준alpha/AirVfx4. 각83ms(명목12fps),1회. 전6clip의최종프레임완전투명. low는full대신재생하며거품구멍/원두골/잎실루엣단서유지. 노출합27이며모든노출이새손작화라는주장아님: Aseprite Lua형태작화후위치/크기/증기·바람분기를조정한pixel합성. 마지막빈그림은clip간동일할수있음.

27/27프레임을6native로부터실제재열기/export해pixel-exact확인. 이진alpha0/255·투명테두리27/27. 모든full/low전프레임을`*_contact.png`의dark/light/cafebrown3배경에서직접열어확인했고`02-peak-backgrounds-64px.png`도열어64px프록시확인. 커피원두의밝은골과선행가장자리2px하이라이트유지. 거품하단은짙은청록경계로밝은배경가독성보완. 원두/잎각각작은도형으로유지하고화면전체오라없음. 구조검사나GIF파일생성으로미적PASS주장하지않음.

## 제출컵 계약

**새64x64컵자산미제작,기존컵재사용.** runtime `Assets/_Project/Resources/DrinkLab/dl_coffee_cup.png`(32x26)를직접열어확인. Unity Resources키`DrinkLab/dl_coffee_cup`. 필요UI표시는비율보존64x52/Point로사용하고64x64영역안중앙정렬. 원본수정/새이미지서비스호출없음. 해시는`cup-reuse-contract.json`;실제제출UI합성은Claude검수.

## 구현담당 인수 검증

- 상성추가피해가실제로적용된확정hit에정확히1회만연결. 일반명중/취소거래/중복콜백에가짜상성피드백금지.
- 끝나면renderer끄고sprite해제/pool반납. 재획득시frame0,elapsed0,white tint,scale1,rotation0,flipXYfalse를초기화. 동시개체상한/합치기/다수DOT당효과예산은Claude검증.
- effect중심(64,64)을실제적중점에놓고필요하면전체효과를명중방향으로같이회전. smallenemy얼굴·플레이어가림과실제갈색카페·밝은맵/저효과720p검수필수.
- 핵심NPC천인계/움직이는banner핀과손연결/발접지/미러방향/3.5초동안전투무피해는상위HANDOFF계약대로runtime검증. 별도revision으로해당프레임만보정가능.
- 실제게임연속재생/과밀도/풀링/상성데미지검증은**NOT RUN**. 코디네이터와Claude소유이며이번아트worker완료는게임완료나미적runtime PASS가아님.

## 실제 도구 기록

Aseprite설치실행파일로숨김배치Lua픽셀작화/native레이어/tag/PNG저장/재열기export수행. Pillow는원본읽기/검수판/투명sheet/GIF합성과해시검사. PixelLab확장은설치확인했지만**실사용·생성0**,다른AI이미지서비스0. GUI슬롯은배정받았으나실제GUI입력/앱포커스조작없이반납. 사용자앱종료없음. 생성수·프레임수로미적완료를대체하지않음.
