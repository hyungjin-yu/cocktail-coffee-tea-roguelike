# Progression UI 아트 납품 — 2026-10-19

상태: **ready (root 최종 아트 검토 대기), integrated=false, verified=false**.
소유 폴더: `ArtSource/v1/ProgressionUI-2026-10-19/`만 작성했습니다. 기존 원본/Assets/C#/.meta/Editor/기획을 수정하지 않았으며 하위 워커를 생성하지 않았습니다.

## 납품

- 활성 **28 asset IDs / 69 RGBA PNG / 28 Aseprite 원본 / 6 GIF**.
- 재료 종이 카드, 가변 길이 영수증, 스프링 노트, 영구 보관용 찢긴 레시피 카드, 보관함, 레시피 찢기/입고 모션.
- 동일 본체 기반 노트북 닫힘/등장/열기/점등/열림/닫기, 전체 열기 순서 모션.
- 공통 버튼/트리 노드/직원 카드 프레임 각각 neutral + locked / available / owned / equipped. 인물 초상 없음.
- 글자/숫자/이름/비용/효과를 이미지에 굽지 않았습니다. 손/팔/얼굴/몸체 없음. 신규 스킨/무기는 범위에 넣지 않았습니다.

`manifest.json`이 활성 파일/프레임 순서/시간/해시/빈 content rect/9slice borders/pivot/anchor/상태/사용 구분의 기준입니다. `delivery-files.json`에는 실제 산출물의 파일 목록과 SHA-256이 있습니다.

## 검토 자료

- `preview.html`: 오프라인 프레임 재생기와 1280×720 배치 예시. 글자는 실제 HTML UI이며 아트에 포함되지 않습니다. 이 HTML의 브라우저 실행 검증은 하지 않았고 Unity UI 구현 증거로 사용하지 않습니다.
- `qa/static-contact.png`: 모든 정적 활성 아트와 4가지 상태.
- `qa/laptop_opening-contact.png`, `laptop_closing-contact.png`, `laptop_sequence-contact.png`: 연속 프레임.
- `qa/recipe_tear_store-contact.png`: 노트 잔존, 분리 종이 이동, 마지막 3프레임 입고/가림.
- `qa/alpha-black.png`, `alpha-white.png`, `alpha-cafe.png`: 검정/흰색/카페 바닥색 알파 시각 검토판.
- `qa/9slice-ingredient_card.png`, `9slice-receipt.png`, `9slice-button.png`와 `receipt-height-192/288/384.png`: 확장 검토판.
- `preview/*.gif`: Aseprite에서 내보낸 실제 프레임과 시간. 무한반복 NETSCAPE 확장 메타데이터만 제거해 1회 재생합니다. HTML 재생기도 1회 재생 후 마지막 프레임 유지.

## 실제 도구·참조와 접근 문제 처리

03/04/09/11/69/72/73 전체를 읽었습니다. 실제 기존 `ArtSource/card-education-2026-10-16/card_gin.png`를 열어 보고 RGBA 스타일 이미지로 PixelLab에 실제 전송했습니다. 실제 ui_panel과 기존 재료/종이 참조도 확인했고 참고 복사본은 `references/`에 있습니다.

PixelLab Aseprite 확장 설치와 Aseprite **1.3.18.6** 실행을 확인했습니다. GUI 시작 시 **You have an old version/wrong tier of PixelLab** 경고가 있어 취소했고, root 지시에 따라 기존 BeverageCraft-v1.1의 확장 프로토콜을 확인했습니다. 현재 `check-latest-pixellab` 응답은 Not up to date, `get-latest-pixellab` 응답의 서버 package version은 **0.4.130**(설치 0.4.129)이었습니다. 현재 응답 버전으로 기존 계정의 `generate_image_pro_flash` 실제 생성이 성공했습니다. 설치 파일/계정/요금제/로그인 변경 및 구매는 하지 않았습니다. 비밀은 메모리에서만 사용했고 저장된 산출물에 실제 자격값이 없음을 확인했습니다.

9개 PixelLab raw 원본과 비밀 없는 요청/성공 receipt를 `provenance/`에 보존했습니다. 384×256 요청은 최대 256px 지원 제한으로 거부되어 노트북을 256×192로 재요청했고, 낮은 버튼의 스타일 입력 높이 제한은 실제 참조를 nearest 축소해 해결했습니다. 두 validation 실패 원문은 `.failure.json`에 남았습니다. 최종 아트는 PixelLab 생성물에서 Aseprite Lua로 팔레트/알파/읽기면/프레임/고정 deck/덮개 투영/상태 표식을 정리했습니다. 일반 임시 도형을 최종 아트로 납품하지 않았습니다.

별도 생성된 닫힌 노트북은 열린 노트북과의 identity 차이가 있어 source-only로 `source-candidates/laptop_closed/`에 보존하고 활성 인계에서 제외했습니다. 활성 닫힘은 **laptop_closed_matched**이며 열린 본체의 실제 Aseprite 부분으로 제작했습니다. `aseprite-access-probe.aseprite`는 도구 접근 확인용이며 임포트 대상이 아닙니다.

Aseprite batch 초기 확장 `handle-pose.lua:58`의 nil dlg 경고는 `aseprite-batch.log`에 기록했습니다. batch exit 0, 실제 원본 저장, 69장 native roundtrip 픽셀 일치로 아트 출력 성공을 확인했습니다.

## 모션/실제 UI 계약

- 등장 250ms + 열기 350ms + 점등 150ms = **750ms**. 닫기 **350ms**. 레시피 찢기와 입고 **500ms**. 모든 모션 런타임 loop=false / holdFinal=true.
- 노트북 hinge `(192,204)`는 **좌상단 원점 px**입니다. deck의 위치/크기는 고정되고 덮개만 투영되며 닫히는 덮개가 키보드를 자연스럽게 가립니다. 246..255행 책상 접점 픽셀은 모든 열기/닫기/점등 프레임에서 일치합니다.
- 열린 스크린 전체 `[56,28,268,160]`, 실제 안전 content rect `[64,36,256,144]`. 960×640 노트북 배치에서 실제 UI 안전 면은 640×360이며 본문 18px / 클릭 44px 이상, 탭/스크롤/상세 패널을 사용합니다. 이는 아트 배치 목표이고 실제 Unity 720/1080/터치 검수는 미수행입니다.
- static/opening/light/closing 캔버스는 384×256이고 arrive/sequence는 384×288입니다. **모든 클립은 공통 PPU 및 실제 hinge pivot으로 정렬**하고 각 캔버스를 동일 UI rect에 개별 stretch하지 마세요. manifest의 bottom-left Unity normalized pivot과 top-left pixel anchor를 구별하세요.
- `laptop_light` 마지막 프레임과 `laptop_open`의 RGBA 픽셀은 동일합니다. 전체 sequence 마지막의 상단 384×256도 동일합니다. 닫기 마지막은 closed_matched와 동일합니다. 클릭/Enter 건너뛰기도 이 끝 상태로 이동합니다.
- `torn_recipe`는 영구 레시피 목록/재사용/수정/공유용 별도 카드입니다. `recipe_tear_store`는 **저장 성공 후** 재생하는 장식 모션이며 저장/재료/음료를 생성하거나 복제하지 않습니다.
- 레시피 모션의 각 frame `paperTransforms`에 번역/실제 nearest 배율/본래 content rect/이동된 content rect/보관함 앞면 가림/글자 표시 여부를 명시했습니다. 실제 UI 글자도 같은 transform과 clip을 적용하세요. 마지막은 종이가 완전히 가려진 노트+보관함 정적 상태와 픽셀 일치합니다.
- 9slice borders는 LTRB 및 Unity Left/Bottom/Right/Top 양쪽 순서로 제공합니다. receipt/ingredient/button의 모서리와 읽기면을 확장 검사했습니다. 스프링 바인딩은 개별 고리가 늘어나지 않게 **균일 nearest 확대**하세요. 직원 카드도 초상/본문 inset을 유지하는 균일 확대입니다.

## 검수와 남은 일

`qa/verification.json`: **413/413 통과**. 69장 크기/RGBA/비어 있지 않음/0·255 알파/투명 RGB 0/SHA/네이티브 Aseprite 재출력 픽셀 일치, 빈 본문 면 단색, GIF 프레임 수/시간/1회 재생, 모션 종료 동일성, 접점, 영수증 확장을 확인했습니다. 실제 정적/상태/열기/닫기/점등/입고 연속 프레임과 검정·흰색 알파 검토판을 직접 열어 보았습니다. root가 노트북 힌지 방향과 수정된 레시피 완전 입고를 중간 시각 검토로 통과시켰습니다.

남은 일: root의 최종 ready 아트 검토 → 제조 물리 게이트 통과 후 Claude의 Assets 임포트/실제 UI 구현 → Unity 720/1080/터치/거래·스킵·저장 실패 흐름 검증입니다. **이 납품은 ready 원본이며 integrated/verified/게임 완성을 주장하지 않습니다.**

재검사: `python -W ignore verify.py`. 재생성은 `produce.py` → Aseprite `finish.lua` → `finish-preview-metadata.py` → Aseprite `roundtrip.lua` → `build-manifest.py` → `verify.py` → `build-contact.py` / `build-preview.py` / `build-inventory.py` 순서이며 PixelLab 재호출에는 실제 현재 서버 응답과 접근이 필요합니다. 기존 reference나 ArtSource 원본을 덮어쓰지 마세요.
