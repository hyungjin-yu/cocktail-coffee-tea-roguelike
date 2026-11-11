# Steam 마스터 도전 아트 연결 · 메모리 중단

2026-11-11 현재 여유RAM3470MB 확인. 4GB 기준 미달이므로 Unity·캡처·빌드 추가 실행 보류. 사용자 앱을 종료하거나 자동 감시로 재실행하지 않는다.

진행분:
- ArtSource 인수110프레임을 Resources로 임포트, 원본과 바이트 일치·PPU128/Point/pivot 검사 통과.
- NPC·현수막3500ms 연출, 품질·상성 VFX one-shot/풀/low대체, schema v5/한 번 백업·미래버전 보호 구현.
- schema5 포함 대상 EditMode94PASS·0FAIL·기존SKIP1. PlayMode 정규9PASS. 명시 증거 시험 초기 실패와 무효 타이틀 캡처는 별도 기록.
- 26장 실제 오프스크린 화면에서 현수막이 HUD에 가리는 FAIL 발견. 배율0.8/상단위치1.45로 수정했으나 그 후 재시험·재캡처는 NOT RUN. 현재 코드 전체 통과로 표시하지 않는다.
- 자원기준 미달에서 두 번 실행한 위반은 보고서에 보존(import3659MB/evidence3789MB). 정상 종료했다는 사실이 실행 기준 준수를 의미하지 않는다.

미완: 위치 수정 후 손·핀/천인계·발미끄럼/3.5초연속영상·720p합성 재검증, 정규 모드 회귀, 새Windows검수빌드·sourcehash 일치·실제플레이. 공모전·기존세이브는 별도 보존.

근거 원본: Unity Delivery/Reports/qa-steam-master-2026-11-11.md 및 qa-steam-master-resume-2026-11-11.md. 두 보고서 복사본을 이 vault에 보관한다. 관련 코드·임포트 아트·테스트·문서를 WIP로 GitHub에 저장하며 성공 SHA는 후속 기록.
