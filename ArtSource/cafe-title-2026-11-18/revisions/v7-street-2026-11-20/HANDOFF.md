# 카페 창과 출입문 바깥 풍경 v7

사용자 캡처에서 창과 열린 문 뒤가 회색·검은색 면으로 보였다. 카페 남쪽에 인도, 차도, 맞은편 인도와 따뜻한 벽돌 건물을 두고 기존 실내 카메라로 함께 투영했다. 왼쪽 창과 오른쪽 출입문에서 연석과 건물 띠가 같은 높이와 원근으로 이어진다. 창의 옅은 유리 색과 열린 문짝, 경첩, 문턱은 기존 깊이 순서를 유지한다. 정면 매장 간판 및 북향 입장 구도는 기존 v5 아트와 v6 배치를 사용한다. [전후 비교](previews/apertures-before-after.png)

## Unity 반입

| 순서 | 파일 | 배치 |
|---|---|---|
| z -10 | `ready/interior/street_backdrop.png` 추가 | 개구부에서만 알파가 있는 거리 배경 |
| z 0 | `ready/interior/g0.png` 교체 | 개구부만 투명하게 뚫은 기존 실내 쉘 |
| 기존 순서 | g1–g8, light와 명패 | 기존 프레임과 가구, 물체 조명, 메뉴 사용 |

두 PNG 모두 480×270, 좌상단 정규화 rect `[0,0,1,1]`, 중앙 pivot `[.5,.5]`이다. 동일 contain과 Point 샘플링을 사용한다. v6 대신 [v7 layout](layout.json)을 반입하며 기존 도감 polygon을 포함한 메뉴 계약을 유지한다. Unity 목적지와 SHA-256은 [manifest](manifest.ready.json)를 따른다.

검수에서는 개구부 5,596픽셀만 달라졌고 마스크 밖 변경은 0픽셀이었다. Aseprite에서 레이어 PNG와 합성 마스터의 왕복 픽셀이 일치했다. 기존 v5/v6과 r5 ZIP을 보존했다. Unity 실제 반입과 화면 검수는 구현 담당 범위다. [픽셀 검증](verification/pixel-validation.json)
