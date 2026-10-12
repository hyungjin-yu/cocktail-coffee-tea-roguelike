# UI polish sprite 계약 — 기획31 / 2026-10-08

구현 담당 ctx_f8ab73b715be. 기존 승인된 ArtSource/v1와 캐릭터/무기 PNG는 덮어쓰지 않는다. 신규 원본은 `ArtSource/ui-polish-2026-10-08/<assetId>/manifest.json`, Unity 적용은 구현 담당이 `Assets/_Project/Art/v1/<assetId>/`와 Resources/ArtV1/ArtCatalog에 한다. readyRoots에 신규 원본을 추가하며 신규 ID를 사용해 04의 기존 flat 계약도 보존한다.

| assetId | canvas px | 파일/clip 계약 | spriteBorderPx [L,B,R,T] | 사용 |
|---|---:|---|---|---|
| ui_cafe_panel |96×96|effect 1장|12,12,12,12|화면 안쪽·모달·정보 패널|
| ui_cafe_button |128×40|normal/hover/pressed/disabled 각1장|10,10,10,10|일반 버튼,hover=keyboard selected|
| ui_cafe_card |96×128|effect 1장|12,12,12,12|카드·영구 강화·커피 노드|
| ui_cafe_tab |80×32|normal/hover/pressed/disabled 각1장|8,8,8,8|도감 활성·포커스 탭|
| ui_cafe_bar_frame |128×16|effect 1장|6,6,6,6|HP/칵테일/차/원두 바 프레임|
| ui_cafe_bar_fill |32×8|effect 1장|3,2,3,2|기존 fill색 유지하는 중립 크림/white fill 무늬|
| ui_cafe_focus |96×96|effect 1장,중앙 투명|12,12,12,12|선택 카드/노드 테두리,raycast 없음|
| ui_cafe_header |96×24|effect 1장|8,8,8,8|카드 제목 크림 종이판,진한 커피색 live text|
| ui_cafe_header |96×24|effect 1장|8,8,8,8|카드 제목 크림 종이판,진한 커피색 live text|

총8ID/14PNG. 변경 필요 시 파일 제작 전 구현 담당과 직접 합의한다. clip fps0/loopfalse, pivot[.5,.5], kind ui, specVersion1.0, pixelsPerUnit128을 명시한다. frames/<assetId>_<clip>_right_00.png 형식을 쓴다. files는 실제14PNG 전체 목록, 각 clip은 해당1PNG를 가리킨다. pressed는 inset·음영 변화, disabled는 패턴/톤 변화, hover/selected는 청록 모서리 또는 이중선 등 색 외 형태 차이도 둔다. 한글/숫자/버튼 이름/희귀도는 그리지 않는다.

PNG RGBA, Point/no compression/mipmap off/Clamp. 2px 픽셀 격자, 투명 모서리와 유효한 border를 유지하며 물결 feather/fake alpha 배경은 쓰지 않는다. Source 팔레트와 alpha 검사·PNG SHA/9slice 확대검수/native .aseprite/PixelLab 실제 시도 로그·프롬프트를 함께 인계한다. Sprite의128 PPU와 uGUI100 reference PPU 차이는 구현에서 Image.pixelsPerUnitMultiplier로 보정해1080 reference에서 border를 source pixel 크기로 유지한다. 720에서 약2/3로 축소됨을 고려해 필수 선·모서리는 충분한 두께로 둔다.

실제 UI 참조 크기(1920×1080 CanvasScaler,720은2/3): Title 일반 버튼410×66,Pause420×70 안팎,Settings controls 및 close300×80,카드/강화390×240 이상,도감 탭200×40,도감 정보 pane440×560,모달640×300 이상. 패널은 작게180×80/크게1700×900까지 펼쳐도 중앙 장식이 글자를 침범하지 않아야 한다. HUD는 HP420×30,칵테일900×30,차900×26,원두600×24이며 기존 rect/전투 가림 면적을 확대하지 않는다.

현재 loader: `ArtLibrary.Get(id)?.Clip(name)?.frames[0]`. `ArtLibrary.Sprite(id)`는 FirstSprite만 주므로 신규 state는 명시 clip으로 읽는다. `UiFactory`는 `SkinPanel/SkinCard/SkinButton`과 `ApplyHoverColors`로 연결하며 SpriteSwap.normal Image/hover highlighted+selected/pressed/disabled를 실제 Button.SpriteState에 지정한다. fill은 기존 anchorMax 기반 진행율과 색을 보존한다. 아트 부재 때의 기존 sprite fallback은 개발 캡처용이며 새 UI 최종 strict 검사에서는8ID/14PNG/state/border가 모두 필요하다.

적용 범위: Title/직업·무기선택/영구강화/도감/설정/Pause/커피트리/카드선택/Result/조작안내/확인창/HUD. 경고·몸체·총2배·경제/키입력 계약은 보존한다. 승인은 실제 game UI1080/720,hover/selected/pressed/disabled·글자/접근성 화면 검수 후 수행하며 이전 f073 RC 결과를 새 UI 빌드 결과로 쓰지 않는다.
