# 싱글 카페 입장·공간 메뉴 아트 인계 — 2026-11-18

**Art ready:28PNG / Aseprite30개(개별28+실내 레이어 master+문8프레임 master).** `manifest.ready.json`과 `layout.json` v3/status ready가 통합 기준이다. 기존 싱글에 적용하는 아트이며 협동판·Assets·C#·meta·기존 원본을 편집하지 않았다. 실제 Unity 실행·사람 플레이 완료를 주장하지 않는다.

## 구현자가 사용할 파일

- `manifest.ready.json`:28개 PNG와 원본 해시, 목적지, 캔버스/피벗/검사 기록.
- `ready/<assetId>.ready.json`:각 자산별 ready manifest.
- `layout.json`:480×270 아트px /1920×1080 기준, **좌상단 정규화 좌표**, rect[x,y,w,h],8feature→기존 버튼 이름, 실제 가림 이후 hotspot polygon·label anchor·z순서·문 경첩/문턱·카메라 방향·컷 타임.
- 경로는 **ready/ 접두어까지** `Assets/_Project/Resources/CafeTitle/` 아래 그대로 복사한다. 예: layout의 `ready/exterior/facade.png` → Resources/CafeTitle/ready/exterior/facade.png. 레이아웃은 Resources/CafeTitle/layout.json.
- 모든 이미지 Point/무압축/mipmap off/alpha transparency, 이미지와 polygon·라벨에 **같은 contain 변환**. 4:3에서8사물을 잘라내거나 가구를 다시 배열하지 않는다.

| 묶음 | PNG | 원본 |
|---|---:|---|
| 실내 room shell+8feature 가림 레이어+따뜻한 바닥 빛 |10|각480×270,피벗 가운데; 전체 `ready/interior/ct_interior_master.aseprite` |
| 외관/문틀/정적 유리창 + 같은 방 북향 glimpse |2|각480×270,피벗 가운데 |
| 실제 경첩 개폐 0/15/30/45/60/75/85/90도 |8|184×216,전체장면 crop 원점(148,47),고정 native hinge pivot(43,9); `ct_door_open_master.aseprite`8프레임 |
| 같은 축 접근·문턱 통과 |8|480×270,카메라 북향 고정,동일 실내 월드/재질을 실제 위치에서 투영 |

개별 프레임과 레이어는 full-canvas 등록을 보존한다. 가구 가림은 실제 depth visibility로 만들었으므로 PNG를 임의로 이동/늘이면 소품·그림자·핫스팟 관계가 깨진다. 프리뷰의 한국어 이름표/검사선은 검수 합성물이며 게임 PNG에는 굽지 않았다. 게임 이름표는 Unity텍스트만 사용한다.

## 현실성과 승인된 연출 계약

코디네이터 msg_45f1f437a9bc에서 v2 **남쪽 정면문으로 북향 직진→실제 문턱 통과→같은 카페의 남향 고정 메뉴 쇼트로 명시적 컷**을 승인했다. 이전 초안110도 광각/90도 이동 회전을 폐기했다. 최종90도 수평 시야에서 실제 동일 출입문을 보여 주며 연속 카메라 회전이라고 위장하지 않는다.

7.5×6m 카페의 동쪽에는 주문대·그 뒤 직원 통로1.1m·제조장비, 서쪽에는 책장/옷장/노트북 연구 테이블, 북서쪽에는2인 좌석이 있다. 입구1.05m와 손님/주문 동선 및 문짝 회전 부채꼴을 비운다. 가구는 메뉴 균등 배치를 위해 이동·확대하지 않았다. 노트북40cm·라디오45cm·컵 지름9cm, 의자 좌면44cm, 연구 테이블78cm와 같이 실제 크기를 기준으로 한다. 노트북/라디오/장부/컵은 지지면에 놓이고, 장·카운터는 바닥 plinth/toe-kick으로 접지한다.

문은1.05m 개구부의 **강체 leaf 폭1.046m/두께44mm**, 고정 서쪽 경첩 축(1.4,-0.025)에서 회전한다. 경첩 barrel25mm outboard와 자유변4mm 틈을 두어 회전 중 양쪽 jamb에 박히지 않게 했다. 모양을 열별로 줄이는 가짜 회전이 아니다. 0~90도를1도 간격으로 검산했고 각 프레임 폭·두께가 일정하다. 앞면/뒷면 손잡이는 같은 leaf에 붙어 있다. 역방향 쇼트에서 경첩이 화면 반대편으로 보이는 것은 같은 물체를 반대에서 보는 결과다.

북향 외관 glimpse·진입 뷰와 남향 최종 뷰는 **같은 Aseprite authored world geometry·공유 실제 생성 소품·승인 재질·동일 광원**에서 마감했다. 두 실내 이미지를 독립 생성한 뒤 크로스페이드해 공간 연속성처럼 꾸미지 않았다. 북향에서는 동쪽 카운터가 오른쪽·서쪽 책장이 왼쪽, 남향에서는 반대로 보인다. 광원은 같은 세계 위치(5.1,3.0,2.8)의 주 조명이며 바닥 그림자·소품 가림도 같은 기준이다.

### 재생 순서/시간

1. 외관 인식 hold 권장0.35초.
2. 문8프레임12fps =0.667초, 카메라는 아직 문 밖.
3. `bridge.seconds=1.1`,8개 위치 샘플은 새로운 방8개가 아니라 같은 북향 직진 경로다. 눈높이1.3m, y=-2.30~-0.06의 외부 위치를 지나 +0.16/+0.40m로 문턱 안에 들어온다. 마지막뷰 t=.82라서 **40cm 안쪽 상태를0.198초 보여 준 뒤** 컷한다.
4. 권장 cut/menu ready 시각 **2.117초**, 북향→남향 고정 쇼트. `layout.cut`, `timing`, `cameraWorld` 메타 참고. 컷은 명시적이며 fake orbit·방 모핑·어둠페이드로 입장 대체하지 않는다.
5. 메뉴 복귀 시 입장 반복 강제 금지, 저이펙트·스킵 입력 소비·포커스/저장 오류는 Claude 구현/런타임 검증 대상.

door master의 정수 밀리초는83/83/84/83/83/84/83/84ms(합667ms)로 저장했다. Unity는 manifest의12fps 비반복 클립을 사용한다. GIF는 프리뷰용 루프와 마지막 정지 구간이 있어 실제 게임 시간 증거가 아니다.

## 실제 도구 사용과 원본 보존

Aseprite1.3.18.6와 fresh Edit메뉴 **공식 PixelLab0.4.131**을 따로 확인했다. 첫96×128 문 prototype native 실제 생성 완료 알림을 확인한 다음, 부족한 책장/의상장/라디오/장부/머신/외관 재질6건을 공식 pro native로 생성했다. 승인된 기존 카페 벽을 스타일 참조로 사용했다. 마지막 공식 Generation queue는0active/0waiting/6recent, #6 외벽·#5 머신·#4 장부 Status:Completed를 직접 읽었다.

- `provenance/tool-gate.json`, `source-generation-receipts.json`, `native-source-specs.json`, `probe-spec.json`
- `raw/` 원본 PNG/native7쌍, pro 후보들은 `raw/candidates/`에 그대로 보존.
- 기존 승인 카운터/바닥/등 없는 벽 재질/노트북4파일은 직접 확인하고 복사·SHA 보존: `reference/reuse-inventory.json`.
- noisy 문 prototype의 몸통은 승인 walnut와 생성 건축 몰딩으로 **native 재제작**했다. prototype 픽셀을 그대로 보존한 최종 문이라고 주장하지 않는다. 외벽의 낡은 벽돌 부분은 카페 전체 벽으로 채택하지 않고 생성된 건축 몰딩을 활용했다.
- `provenance/finish-scene.lua`의 Aseprite Image API가 최종 픽셀·원근 텍스처·depth 가림·강체 leaf·소스 레이어를 작성했다. Python은 계획/인벤토리/검사/프리뷰 합성에만 사용했다.

첫 소스 요청은 참조 PNG의 Aseprite Security 개별 읽기 권한에서 대기했다. 작업자는 보안 권한 버튼을 누르지 않았고, 나중에 새 화면에서 권한 창이 사라지고 기존 큐가 실제 진행/완료된 것을 확인해 이어받았다. 큐 중복 시작·보안 우회·재설치·비밀키 접근·새 로그인/구독/결제·full trust 조작은 하지 않았다.

## 검사 결과와 증거

- **28/28 PNG/native**:캔버스·비어 있지 않음·알파 정책·native magic/프레임/캔버스·slice pivot·재개방 재출력 픽셀 정확히 일치.
- raw7쌍과 실내 master도 native 재출력 픽셀 일치. 문 master8프레임→PNG 전부 일치.
- 외관/정적 glimpse/184×216 문 프레임을 등록 합성한 결과가 full scene과 동일 위치이며, 알파 산술 반올림 차이만 최대1/255.
- 8hotspot polygon은 실제 pixel grid에서 **겹침0**. 가려진 책장 아래/옷장 옆은 제외했다. 종료는 불투명 frame만이 아니라 실제 열린 **문 개구부**를 타깃으로 하므로 투명한 aperture를 누르는 것이 정상이다.
- 작은 이름표8개도 겹침0, 실제720p 및4:3 contain 프리뷰에서 읽기/잘림을 직접 확인. 실제 Unity font metrics와 클릭 결과는 아직 검증하지 않았다.
- 같은 문턱·강체 leaf·경첩/문틀·실제 threshold signed camera position·단일 광원·직원 통로/소품 지지/발 접지 검사 기록.
- 기존 승인 참조4개 원본 SHA 모두 불변. 새 스킨/무기를 요청받은 것이 아니므로 최소10종 규칙을 배경에 적용하지 않았다.

검사: `verification/asset-validation.json`, `geometry-validation.json`, `hotspot-validation.json`, `label-validation.json`, `exterior-layer-registration.json`, `final-handoff-check.json`.

미리보기: `previews/interior-composite.png`, `hotspots-labels-proof.png`, `entry-continuity-all17.png`, `entry-preview.gif`, `menu-contain-1920x1080.png`, `menu-contain-1280x720.png`, `menu-contain-1440x1080.png`. **모두 아트 파일/합성 증거이며 실제 빌드 캡처가 아니다.**

## 남은 검증/권한 경계

Claude가 Assets 통합 후 실제 문 프레임/문턱/컷의 재생 순서·최종 inside 뷰 hold,8메뉴 실제 클릭·키보드 탐색·스킵 입력 소비·재진입·저이펙트·720/1080/4:3·저장 오류/미래버전 보호·검수본 ZIP을 확인해야 한다. Art source ready를 게임 전체 완료로 보지 않는다. 협동판·사용자 실제 저장은 사용하지 않았고, 소리·PC종료·예약종료·게시·커밋/푸시는 하지 않았다.
