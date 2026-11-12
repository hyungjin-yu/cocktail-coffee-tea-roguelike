# UI 아이콘 아트 인계 — 2026-11-12

아트 **48파일 ready**. 연구 기본 15 + 차 전용 변형 7 + 카드 14 + 숙련 12이며 실제 기능 ID는 41개다. Unity 적용/실제 실행 화면 검증은 이 아트 보고서에서 합격 판정하지 않는다.

- 최종 매핑·규격·PNG/원본/픽셀 해시: `manifest.json`
- 최종 독립 검증: `evidence/final-audit.json`
- 제작 방법·원본·시드/요청·도구 실사용 설명: `PRODUCTION.md`, `raw/*.request.json`, `raw/*.receipt.json`
- 전체 비교판: `evidence/contact-1x.png`, `evidence/contact-4x.png`
- 분야별 비교판: `evidence/card-1x.png`, `card-4x.png`, `research-1x.png`, `research-4x.png`, `research_tea-1x.png`, `research_tea-4x.png`, `mastery-1x.png`, `mastery-4x.png`
- 납품 증거 해시: `evidence/delivery-hashes.json`

모든 최종 PNG는 64×64 RGBA, 실제 투명 배경, 비어 있지 않음, 최소 3px 여백, 픽셀 내용 중복 없음이다. 모든 `.aseprite`는 64×64 32비트 정적 1프레임, 보존된 PixelLab 원본 숨김 레이어와 정리된 표시 레이어를 갖는다. 원본을 재열어 내보낸 픽셀과 최종 PNG가 같고, 숨김 원본을 다시 내보낸 픽셀도 실제 PixelLab 출력과 같다.

Aseprite GUI에서 PixelLab 시험 1회 성공(seed 3680239207) 후 실제 PixelLab Pro Flash 프로토콜 API로 기존 카드 RGBA 스타일 참조를 넣어 제작했다. 실제 완료 API 출력 55개 + GUI 시험 1개이며, 그중 최종 48개만 납품 수로 센다. 다른 생성기·Python 도형/팔레트 대체·새 결제는 없고, Python은 요청/디코딩/검증/메타데이터만 처리했다. Aseprite batch가 알파 정리·중앙 정렬·레이어·PNG/원본 저장·재열기·비교판을 처리했다.

품질 수정: 제스트 불꽃 확대, 정량 용기 눈금, 농축차의 갈색 다관, 숙성차의 찻잎 보관 항아리+시간, 시그니처 준비의 가짜 글리프 제거, 거리 조절의 대각선 스푼 구도. 거절한 각 출력·요청·응답과 초기 번호 PNG 자동 시퀀스 임포트 중간물은 `rejected/`에 보존했다. 초기 GUI 거절 출력은 `raw/rejected-gui-bitforge.*`다.

미완료/인계: Unity 담당의 Assets 임포트·연결, 두 직업 720p/1080p 실제 UI, 잠김/획득 가능/획득 완료/장착 상태·작은 목표 칸 등에서 판독 확인. 이 아트 작업자는 Assets/C#/.meta/기존 원본을 편집하지 않았다. 전체 UI 완료나 runtime verified로 오해하지 않도록 아트 ready와 구분한다.
