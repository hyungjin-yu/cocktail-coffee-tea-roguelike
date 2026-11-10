# Steam 마스터 도전 아트 인수 — 애니메이션 검수 대기

대표 NPC revision02, 현수막/VFX 디자인은 코디네이터 승인. 아래 확장 클립은 제작·구조 검사를 마쳤으며 **실제 Unity 합성/움직임/720p 미적 PASS는 아직 아님**.

## 가져갈 파일

- `manifest.json`: 모든 15 clip, 83 frame의 파일명·tag·시간·PPU·pivot·손좌표·해시·재사용 내역과 구현 계약.
- `frames/`: 실제 사용할 개별 투명 PNG. `*_sheet.png`: 같은 프레임의 수평 RGBA atlas. 현수막 sheet는 10240px이므로 runtime에는 개별 1280x192 PNG 권장.
- `npc_{porter,rigger}_{walk_in,install,idle,walk_out}.aseprite`: 편집 가능 layer/tag native. `quality_{normal,excellent,master}[_low].aseprite`, `banner_install.aseprite`.
- `*_contact.png`: 전 프레임 비교. `*_preview.gif`: 시간 프리뷰(검수 편의상 반복 재생하는 GIF이며 runtime VFX는 1회).
- `07-npc-mirror-64px.png`: 정방향/flipX 및64px 표시 프록시. `08-banner-all-frames-native.png`: 현수막8프레임을 원래크기로 쌓은 검수판.
- `validation.json`, `native-frames.csv`: 네이티브 실제 재열기/재출력83프레임 pixel-exact, RGBA0/255와투명경계83/83.

## 실제 프레임과 시간

| 클립 | 노출 / 고유 그림 | 시간 / 반복 |
|---|---:|---|
| NPC walk_in/out | 각6 / 6 | 100ms씩600ms, loop. 기존runtime의6하체걷기재사용 |
| NPC install | 각6 / 3 | 125,125,500,500,125,125ms =1500ms, 1회 |
| NPC idle | 각4 / 1 | 333ms씩1332ms, 정적hold반복이며4고유작화아님 |
| banner_install | 8 / 8 | 125ms씩1000ms, 마지막프레임고정 |
| 품질normal | 6 / 6 | 83ms씩498ms, 1회 |
| 품질excellent/master | 각8 / 8 | 83ms씩664ms, 1회 |
| low3종 | 각3 / 3 | 83ms씩249ms, 1회 |

NPC 상체는 승인된 역할별 그림을 유지하고 하체에기존걷기6종을합성했음. 상체2px bob에손과천이함께이동하며 새팔휘두르기주장없음. 설치는승인된굽힌팔을어깨기준세각도로변환,운반담당은반대편팔에미러재사용. rigger walk_out은walk_in과동일6그림재사용; porter는퇴장시천을제거. PixelLab설치확인했지만생성0회,어떤AI서비스도호출하지않음. Aseprite는실제배치Lua작화/native저장/재열기/export에사용했고GUI입력없음.

## Unity 계약 / 검수해야 할 점

PPU128, Point, Clamp, 무압축, mipmap없음, 보통alpha블렌드. NPC256 pivot(.5,.125), 발ground는이미지상단기준y224. banner1280x192 pivot(.5,1), BackgroundProp -9, 최종핀(43,20)/(1237,20). VFX128 pivot(.5,.5), AirVfx4; NPC Actor2/Y정렬.

한글은아트에없음. Unity텍스트‘박람회’를 중앙80%폭/60%높이영역에별도로올림. 펼침완료07프레임부터읽히게표시. 전체3500ms제안: 접근0–800,설치800–2300,퇴장2300–3500. 현수막1050–2050ms. 이것은state timer 계약이며재사용걷기loop로시간을채움.

설치의안정된최고손좌표는노출02/03: porter(61,93),rigger(194,93);각노출손좌표는manifest에있음. flipX하면sprite와anchor함께x=255−x. 역할별화면위치는미리정하지않고**실제벽끝단과손앵커에맞춰**적용해야함. 정방향두캐릭터는오른쪽을보고있으며팔방향은서로반대.

**미검증:** 현재현수막rollout끝점은프레임마다밖으로이동함. NPC가실제어느시점에어느끝을잡고서발을딛는지,운반천에서벽천으로전환할때갑자기사라지는지,3.5초전환에서슬라이딩/미러방향/손분리가없는지는Unity합성필수검수. PNG완성이나대표승인으로이검사를PASS하지않음. 필요하면runtime진행중손/천노출시점에맞춰후속프레임보정.

품질은제조/음용/제출성공거래후단한번재생. 마지막프레임완전투명,끝나면renderer끄고pool반납. 재사용시frame0/elapsed0/색white/scale/flip초기화. low대체는full과함께재생하지말고교체. 최고효과반복loop금지. low도normal1점/excellent3점/master큰중앙별+2점으로등급형태유지. 실제전투과밀도/작은화면가독성과최고효과과다번쩍임은아직runtime미검증.

상성칵테일거품/커피원두증기/차잎바람은후속인수항목,이번핵심세트에미제작. 세계대회/차고유성장/기존스킨재작화/Assets적용범위없음.
