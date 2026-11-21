# 라디오 노브 가독성 v10

r6의 720p·1080p·4:3 연속 캡처와 실제 설정 조작 화면을 확인했다. 기존 동작은 밝은 점이 위치를 바꾸는 것으로 읽혔다. v10은 라디오 오른쪽 면에 고정된 5×5픽셀 노브와 중심에서 이어지는 1픽셀 두께의 어두운 지시선을 사용한다. 지시선은 8시 → 10시 → 12시 → 2시 순서로 움직이며, 아래 2×2픽셀 표시등은 짙은 호박색에서 따뜻한 금색으로 밝아진다. 최종 프레임은 2시 지시선과 켜진 표시등을 함께 보여준다.

[720p 크기 비교](previews/radio-before-after-720.png) · [1080p 크기 비교](previews/radio-before-after-1080.png) · [4:3 크기 비교](previews/radio-before-after-4x3.png)

## 반입

[manifest](manifest.ready.json)에 명시된 `settings_00.png`~`settings_05.png`만 기존 파일 위치에 반입한다. 기존 v9 layout, `radio_sideboard` 레이어, 카메라 3.65배, 10fps와 역재생 계약을 그대로 사용한다. 첫 프레임은 원본 g6와 픽셀 단위로 일치한다. 이후 프레임은 기존 라디오 면 안의 25픽셀만 수정하며, 알파와 물체 크기를 유지한다. v9의 48개 PNG 해시를 다시 대조했다. [프레임 검증](verification/radio-verification.json)

비교 이미지는 기존 r6 표시 배율을 근사해 만든 아트 프리뷰다. 새 클립의 Unity 실행 검수는 남아 있다.
