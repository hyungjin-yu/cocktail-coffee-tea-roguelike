# 도감 책장 클릭 영역 v6

기존 Unity 캡처 `Delivery/Verification/2026-11-20/cafetitle-pass1/hover_crops_1080.png`에서 포커스 표시가 책장 중앙과 오른쪽 측판에 걸쳤다. v5 polygon을 카메라에서 보이는 책장 앞면과 대조하니 아래 선반과 왼쪽 책 등판 대부분이 클릭 영역 밖이었다.

책이 놓인 앞면 세 단을 따라 도감 polygon만 조정했다. 왼쪽 아래는 의상 진열장 클릭 영역과 간격을 두고, 오른쪽 측판은 클릭 영역에서 제외했다. 보이는 앞면 2,680픽셀 중 기존 543픽셀(20.3%)에서 2,575픽셀(96.1%)로 개선했다. 기준 아트와 4배 확대 좌표에서 다른 클릭 영역·명패와의 겹침은 0픽셀이다. 금색 선은 도감, 파란색 선은 의상 진열장, 청록색 선은 명패를 표시한다. [전후 비교](bookshelf-hotspot-compare.png) · [픽셀 검증](hotspot-validation.json)

구현 반입에는 v5의 아트를 유지한 채 v6 [layout.json](layout.json)만 사용한다. 배치 버전과 도감 polygon 외의 layout 값은 동일하다. [계약·해시](manifest.layout.ready.json)에 원본 v5와 기존 검수 ZIP의 SHA-256을 기록했다. Unity v6 실행 검수는 남아 있다.
