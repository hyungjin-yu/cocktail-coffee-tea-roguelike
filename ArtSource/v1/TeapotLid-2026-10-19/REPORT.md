# 다관 뚜껑 분리 아트 — 2026-10-19

**ready: PNG 2장 + native Aseprite 2개. integrated=false, verified=false.**
편집 범위는 신규 `ArtSource/v1/TeapotLid-2026-10-19/`뿐입니다. 기존 BeverageCraft/기타 ArtSource/Assets/.meta/C#/기획을 수정하지 않았습니다. 손·인물·새 스타일 생성·각도별 프레임은 없습니다.

## 결과

- `dl_teapot_open_body.png` / `.aseprite`: 기존 64×56 캔버스와 몸체/손잡이/주둥이 픽셀 유지. 뚜껑이 가리던 영역 안에서 열린 입구의 밝은 테두리와 어두운 안쪽 면을 기존 팔레트로 정리했습니다.
- `dl_teapot_lid.png` / `.aseprite`: 기존 뚜껑 픽셀을 **원래 얹힌 좌표 그대로** 추출한 64×56 이미지입니다. crop하거나 캔버스 중심으로 재배치하지 않았습니다.
- `manifest.json`: PNG/native SHA-256, alphaBounds, mouth/lidSeat/pourLip/grip, 기존 마스크 경로/해시, 합성 순서와 이동 예시 좌표.
- `delivery-files.json`: 실제 파일 목록과 해시.

두 PNG를 같은 좌표에 `open body → lid` 순서로 합성하면 원래 닫힌 `dl_teapot.png`와 **RGBA 픽셀 전체가 동일**합니다. 새 입구 면은 닫힌 뚜껑에 완전히 가려지고 기존 몸체의 뚜껑 영역 밖 픽셀은 원본과 동일합니다. 모든 출력색은 실제 원본 팔레트에 있습니다.

## 기존 참조와 마스크

기존 `BeverageCraft-v1.1/dl_teapot.png`, `dl_teapot.aseprite`, `manifest.json`, `dl_teapot_mask.png`를 실제로 읽고 PNG/마스크 및 확대 좌표판을 열어 확인했습니다. 네이티브 원본의 보이는 Finished PixelLab sprite와 숨긴 Liquid mask / preserved source 레이어도 열어 확인했습니다. 기존 native 기본 렌더링은 원래 PNG와 일치합니다.

기존 물리 `../BeverageCraft-v1.1/dl_teapot_mask.png`는 **그대로 사용**합니다. SHA-256은 `39f9ef4f01762116f7b5f1798c5e49a7ab8425b8ec3e97c8a4d9c000c17d2153`, alphaBounds는 `[16,23,45,45]`입니다. 새 두 native에도 이 마스크를 숨긴 레이어로 넣었으며 재출력 결과가 원래 마스크와 동일합니다. 열린 몸체 전경과 기존 물리 마스크의 불투명 픽셀 중첩은 없습니다.

## 좌표와 구현 인계

모든 좌표는 **좌상단 원점 px**, alphaBounds는 left/top/right/bottom exclusive입니다.

- 공통 캔버스: 64×56. 공통 Unity normalized pivot `(0.5,0.5)` / 픽셀 중심 `(32,28)`.
- 실제 새 열린 입구 `mouth=(32,20)`, 뚜껑 안착 기준 `lidSeat=(32,22)`.
- 기존 주둥이 `pourLip=(61,21)` / 손잡이 `grip=(12,29)` 유지.
- 기존 manifest의 mouth `(30,23)`은 원래 참조값으로 따로 보존했습니다. 새 열린 입구를 표시할 때는 manifest의 새 mouth를 사용합니다.
- 렌더 순서: 기존 마스크로 제한한 동적 내용물 → open body → lid.
- 두 스프라이트의 크기/캔버스/피벗을 같게 유지하고 lid Transform만 이동합니다. 닫힘은 상대 offset `(0,0)`으로 정확히 복원됩니다. 각도별 프레임은 필요하지 않습니다.
- Point / 128 PPU / mipmap off / compression none / RGBA binary alpha.

검수판의 body 원점은 `(32,40)`, lid 원점은 닫힘 `(32,40)` → 들어올림 `(32,20)` → 옆 테이블 예시 `(112,68)`입니다. 이는 배치 예시이고 실제 Unity 이동 시간/경로/좌표 변환은 Claude가 구현합니다.

## 검수

- `qa/separation-contact.png`: 실제 원본 / 열린 몸체 / 제자리 뚜껑 / 닫힌 합성 6배 확대.
- `qa/lid-movement-contact.png`: 닫힘 / 들어올림 / 옆에 놓기 합성. 최종본을 직접 열어 확인했습니다.
- `qa/original-grid.png`: 기존 실물의 좌표 확대판.
- `qa/*roundtrip.png`: 두 native의 전경과 숨긴 물리 마스크 재출력.
- `qa/verification.json`: **28/28 통과**. RGBA/64×56/비어 있지 않음/binary alpha/투명 RGB 0/원본 팔레트/native 재출력 동일/원래 마스크 동일/마스크와 전경 비중첩/원본 body 픽셀 유지/원래 좌표의 뚜껑 픽셀 유지/닫힌 합성 원본 전체 픽셀 일치/기존 PNG·native·mask·manifest 해시 불변.

root가 separation-contact를 직접 보고 원본 실루엣 보존, 열린 어두운 입구/밝은 테두리, 분리 뚜껑, 닫힌 합성의 시각 방향을 통과시켰습니다.

제작은 기존 PNG에 대한 Aseprite 1.3.18.6 batch Lua 편집으로 수행했습니다. 이미지 생성/대체 스타일/임시 도형 최종화를 사용하지 않았습니다. 기존 PixelLab 확장 초기화의 `handle-pose.lua:58` 경고는 로그에 있으나 batch exit 0 및 실제 PNG/native 재출력 검사로 제작 성공을 확인했습니다. 설치/계정/결제/비밀에 접근하거나 변경하지 않았습니다.

재검사: `python -W ignore verify.py`. 재생성은 Aseprite batch `separate.lua`에 root/source 파라미터 전달 → `preview.py` → Aseprite `roundtrip.lua` → `verify.py`입니다. 기존 원본은 항상 읽기 전용입니다.

**남은 일:** root 최종 ready 수락 후 Claude의 Assets 임포트, 실제 뚜껑 들기/옆에 놓기/되덮기와 차 제조 모션 통합·실행 검증입니다. 이 납품은 아트 ready이며 게임 integrated/verified를 주장하지 않습니다.
