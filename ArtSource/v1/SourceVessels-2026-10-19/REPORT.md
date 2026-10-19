# 기존 제조 병 잔량 표시용 분리 아트 — 2026-10-19

**ready: 6병 / 런타임 PNG 30장 / native Aseprite 6개. integrated=false, verified=false.**
작성 범위는 `ArtSource/v1/SourceVessels-2026-10-19/`뿐입니다. Assets/C#/.meta/기획/기존 BeverageCraft/기존 ProgressionUI 원본은 편집하지 않았습니다. 이미지 재생성, 스타일 변경, 설치·계정 변경·구매·비밀 접근, 하위 워커 실행은 하지 않았습니다.

69/71/73을 읽고 기존 ArtSource 및 Resources/DrinkLab의 여섯 실제 PNG를 확인했습니다. 최초 비교에서 여섯 ArtSource/Resources 파일은 SHA-256까지 같았습니다(`source-comparison.json`). 여섯 원본을 확대해 뚜껑, 목, 몸체, 라벨과 꿀/레몬 병의 실제 형태를 확인한 후 Aseprite 1.3.18.6 Lua로 편집·분리했습니다.

## 파일 계약

재료 ID는 `gin / rum / blue_curacao / grenadine / lemon_juice / honey`입니다. 각 `dl_bottle_{id}`마다 다음을 제공합니다.

| 접미사 | 용도 |
|---|---|
| `_empty.png` | 닫힌 뚜껑을 포함한 빈 전경. 기존 라벨/뚜껑/외곽 픽셀 유지, 노출된 액체 내부만 alpha 0 |
| `_mask.png` | 요청된 물리 내부 마스크. 흰색/투명 RGBA, **라벨 뒤까지 연결** |
| `_visible_mask.png` | 라벨/전경 가림을 제외한 노출부 확인용. **부피·프로필 계산에 사용 금지** |
| `_open_empty.png` | 붓기 때 사용할 전경. 기존 뚜껑 레이어만 분리한 상태 |
| `_cap.png` | 기존 뚜껑을 그대로 추출. `_open_empty`와 겹치면 `_empty`와 픽셀 일치 |
| `.aseprite` | open foreground와 cap을 보이는 레이어로, physical/visible mask와 원본 참조를 숨긴 레이어로 보존 |

전부 원본과 같은 **24×56** 캔버스입니다. 몸체의 투명도를 통째로 낮추지 않았습니다. 기존 외곽의 유리/재료색 경계와 라벨은 유지하고 내부 배경색을 제거했습니다. 꿀병은 원본 그대로 짧고 넓은 병이며, 레몬병의 잎과 레몬 라벨을 보존했습니다.

합성 순서: runtime 액체 → `_mask`로 내부 클립 → `_open_empty` → 필요 시 `_cap`. 일반 닫힌 상태는 `_empty` 사용도 가능합니다. 불투명 라벨이 액체 앞에서 가리는 것이 의도된 결과이므로 physical mask와 foreground 라벨의 중첩을 오류로 취급하지 마세요. 물리 프로필을 visible mask로 만들면 라벨 높이에서 용량이 끊기므로 full `_mask`를 사용해야 합니다.

## 앵커와 프로필

`manifest.json`은 정확한 파일 경로·파일별 SHA-256·alpha bbox·mask bbox·라벨 보존 rect·앵커·행별 프로필을 담습니다. 좌표는 **좌상단 원점 px**, bbox는 left/top/right/bottom exclusive, rect는 x/y/width/height입니다. 모든 병의 회전축/기본 피벗은 기존 캔버스 중심 `(12,28)`이고 Unity normalized pivot은 `(0.5,0.5)`입니다.

| 병 | 실제 열린 mouth | 오른쪽 pourLip |
|---|---|---|
| gin | (12.5,10) | (15,10) |
| rum | (12.5,15) | (14,15) |
| blue_curacao | (11.5,13) | (14,13) |
| grenadine | (12,9) | (14,9) |
| lemon_juice | (12,12) | (15,12) |
| honey | (12.5,22) | (18,22) |

원래 manifest의 cap-top anchor와 실제 열린 목을 구별했습니다. 붓기 전에 cap을 숨기고 열린 목의 앵커를 TransformPoint로 변환하세요. `qa/anchors-contact.png`에서 cyan=mouth, red=right lip, yellow=pivot를 실제 열린 PNG와 함께 확인할 수 있습니다.

행별 profile에는 mask의 실제 좌우 경계/폭/width² 단면 proxy와 아래에서 누적한 정규화 capacity를 기록했습니다. **이는 2D 실루엣에서 만든 회전대칭 근사이며 정확한 3D 형태나 실측 용량이 아닙니다.** 용량 mL는 임의로 만들지 않고 null로 두었습니다. Claude가 실제 용량과 물리 모델에 맞춰 보정하고 캐시해야 합니다. 라벨 뒤 공간도 프로필에 포함됩니다.

## 검수 증거

- `qa/separation-contact.png`: 원래 닫힌 외형/빈 내부/물리 마스크/열린 목 비교.
- `qa/fill-0deg-contact.png`, `fill-30deg-contact.png`, `fill-60deg-contact.png`: 여섯 병 각각 0/25/50/75% 잔량, 세움/기울임 총 **72개 Aseprite 합성 예시**.
- `qa/source-pixel-grid.png`: 실제 원본 좌표 확대판.
- `qa/*_roundtrip.png`: native 전경 및 네이티브 hidden mask/visible mask/cap/open foreground 재출력.
- `qa/verification.json`: **186/186 통과**. 24×56 RGBA/0·255 alpha/투명 RGB 0/라벨 원본 픽셀 유지/변경부가 노출 내부에 한정/마스크 흰색 및 원본 내부/뚜껑 재결합/기본 전경과 4개 개별 레이어 native 재출력 동일/입구가 mask 상단과 접함/앵커 범위를 검사했습니다.

직접 separation/0도/30도/60도/anchor 검토판을 열어 보았습니다. root도 separation과 60도 검토판에서 기존 외형·라벨 앞가림·열린 목·잔량 방향을 중간 시각 검수해 통과시켰습니다.

**기울임 검수판은 수평면과 앞가림을 보는 아트 예시입니다.** 회전된 2D mask 픽셀 면적의 비율을 채우며 경계 행만 fractional alpha를 썼습니다. 실제 Unity SolveTilt, 3D 부피/질량 보존, 유출 가능 각도, 점성, 원본 병→지거 전달량을 검증한 증거가 아닙니다. 그 기능과 실제게임 통합은 Claude 소유이며 미완료입니다. 불투명 라벨 때문에 어떤 잔량 구간은 앞에서 수면이 가려지는 것이 정상입니다.

Aseprite batch에서 기존 PixelLab 확장 초기화 `handle-pose.lua:58` 경고는 있었지만 exit 0과 실제 출력/네이티브 전 레이어 픽셀 일치로 성공을 확인했습니다. 설치나 확장을 바꾸지 않았습니다.

## 재현과 인계

분리: Aseprite batch `separate.lua`에 root와 기존 BeverageCraft source 경로를 전달합니다. 이어 `fill-preview.lua` → `contact.py` / `fill-contact.py` → Aseprite `roundtrip.lua` → `python -W ignore verify-manifest.py` 순서입니다. 이미지 원본 수정은 Aseprite에서 하고 Python은 좌표·검수·메타데이터/검토판 제작에 사용했습니다.

`delivery-files.json`에서 실제 파일 목록/해시를 확인하세요. root 최종 ready 검토 후 Claude가 새 PNG를 Assets에 임포트하고 runtime의 mask/profile/앵커·cap 표시를 연결해야 합니다. **아트 ready와 게임 integrated/verified는 구별합니다.** 기존 차통/직원/스킨/무기 추가 작업은 하지 않았습니다.
