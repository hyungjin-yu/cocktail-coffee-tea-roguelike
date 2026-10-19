# 연구 직원 프로필 초상 4종 — 2026-10-19

**아트 ready: 4 PNG + 4 Aseprite. integrated=false, verified=false.**
소유한 신규 `ArtSource/v1/ResearchStaff-2026-10-19/`만 작성했습니다. 이전 ProgressionUI/SourceVessels/BeverageCraft와 Assets/C#/.meta/기획은 편집하지 않았습니다.

72/73을 읽고 실제 게임 바텐더 PNG와 기존 staff_card PNG를 열어 확인했습니다. 기존 카드 portrait rect **[18,27,28,34]**에 맞춰 모든 최종 초상을 **28×34 RGBA**로 제작했습니다. 카드 좌표는 좌상단 원점 x/y/width/height이며 전체 카드는 기존 160×112입니다. 최종 PNG를 그 rect에 같은 크기로 배치하고 카드와 함께 균일 nearest 확대하세요. Point, mipmap off, compression none, centered pivot을 권장합니다.

| staffId / 파일 | 구별되는 외형 | 72 연구/조건/비용 |
|---|---|---|
| cocktail_aroma / staff_cocktail_aroma | 성인 여성, 은색 단발, 길고 좁은 얼굴, 직사각 안경, 자주 재킷/아이보리 블라우스 | 향미 분석가·스터링 전문: 스터링 음료 공격력 버프 ×1.08 / 조건 없음 / 6 RP |
| cocktail_blend / staff_cocktail_blend | 성인 남성, 삭발, 넓은 턱과 짧은 수염, 주황 작업 재킷/짙은 안쪽 옷/앞치마 끈 | 배합 기술자·셰이킹 전문: 셰이킹 음료 최대HP 버프 ×1.08 / 칵테일 제조 누적 3회 / 10 RP |
| tea_aroma / staff_tea_aroma | 성인 여성, 높은 검은 번, 긴 얼굴/안경 없음, 녹색 스탠드칼라와 삼각형 아이보리 숄 | 차향 평가사·맑은 우림: 차 음료 공격력 버프 ×1.08 / 조건 없음 / 6 RP |
| tea_blend / staff_tea_blend | 성인 남성, 깊은 갈색 피부, 짧은 흰 곱슬머리/둥근 얼굴과 안경, 남색 스모크/베이지 목장식 | 차 블렌딩 연구원·포근한 우림: 차 음료 최대HP 버프 ×1.08 / 차 제조 누적 3회 / 10 RP |

이름·비용·연구·효과는 PNG에 굽지 않았습니다. 각각 얼굴과 머리/의상 구조/어깨 실루엣이 다르며 기존 바텐더 복제나 동일 그림의 색변형이 아닙니다. 이번 명시된 직원 프로필의 얼굴·어깨만 그렸고 손·팔 동작·음용 연출·전투 몸체는 없습니다. 신규 게임플레이 직업/전투 동료/스킨/무기가 아닙니다.

## 실제 제작 경로

- 실제 게임 `Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png`를 열어 스타일을 확인하고, 해당 RGBA를 nearest 64×64 이하로 만들어 **각 PixelLab 요청의 style_image로 실제 전송**했습니다. 복사본은 `references/bartender-style.png`입니다. 바텐더의 얼굴·머리·의상 복제는 프롬프트에서 금지했습니다.
- 설치된 PixelLab 확장 프로토콜의 현재 `get-latest-pixellab` 응답을 조회해 서버 package version 0.4.130을 확인했고, 기존 계정으로 `generate_image_pro_flash` **4건 모두 성공**했습니다. 설치 버전 0.4.129는 변경하지 않았습니다. 계정/로그인/설치/요금제/결제 변경은 하지 않았습니다.
- `provenance/`에 실제 raw 4개와 비밀 없는 request/receipt를 보존했습니다. 최종 프롬프트 세트는 `produce.py`와 요청 JSON에 있습니다. 자격값은 메모리에서만 사용했습니다.
- Aseprite 1.3.18.6 batch Lua에서 공통 32색 팔레트/0·255 alpha/투명 RGB 0/여백과 28×34 캔버스를 정리하고 native를 저장했습니다. 원본은 새 스타일로 재생성하거나 기존 캐릭터를 리컬러한 결과가 아닙니다.
- batch 확장 초기화의 기존 `handle-pose.lua:58` 경고는 로그에 남았지만 exit 0, 실제 파일 및 native roundtrip 일치로 정상 제작을 확인했습니다.

## 검수

- `qa/portraits-frame-contact.png`: 4종 6배 확대 + native 1배 + 기존 staff_card 실물 합성. 레이블은 검수판 바깥에만 있습니다.
- `qa/alpha-white.png`, `alpha-black.png`: 실제 alpha를 흰색/검정 위에서 직접 열어 확인했습니다.
- `qa/staff_*_frame.png`: 각 초상을 기존 카드 rect에 합성한 정확한 160×112 PNG.
- `qa/staff_*_roundtrip.png`: 각 native Aseprite를 재출력한 PNG.
- `qa/verification.json`: **42/42 통과**. 28×34 크기, RGBA, binary alpha, 투명 RGB 0, 비어 있지 않음/여백, native 존재 및 RGBA 재출력 일치, 원래 카드와의 정확한 합성/rect 일치, 4개 역할 및 서로 다른 이미지 해시를 확인했습니다. 성인 프로필/외형 구별/손·음용 없음은 네 raw와 최종 검수판을 직접 보고 확인했습니다.

`manifest.json`에 assetId/staffId/효과·비용 연결/원본 provenance/크기/alpha bbox/프레임 rect/PNG·native SHA-256을 기록했습니다. `delivery-files.json`에는 실제 산출물 목록과 해시가 있습니다. 72의 한 번에 제조 전문 1개 활성화, 해당 음료/기법에만 적용, 없는 버프 생성 금지 및 기존 상한 준수는 그대로이며 이 아트 작업이 새 효과를 추가하거나 활성화하지 않습니다.

남은 일: root 최종 시각 검토 후 Claude의 Assets/UI 임포트, 실제 노트북 직원 사이트의 배치/상세/채용·소유·전문 장착과 720/1080/터치 검증입니다. 실제 Unity 통합이나 게임 검증을 완료했다고 주장하지 않습니다.

재현: 현재 서버 접근 확인 후 `produce.py`(이미 raw가 있으면 재생성하지 않음), Aseprite batch `finish.lua`(root와 기존 frame 경로 전달), `python -W ignore verify-preview.py`. 다른 ArtSource 폴더는 수정하지 마세요.
