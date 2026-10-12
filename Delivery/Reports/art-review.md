# G32 아트 검수 — 2026-10-08

아트 담당 `ctx_db39a1e69bf4`의 직접 시각 검수와 승인 인계를 정식 import/catalog에 반영했다. 상세 원본 검수는 `art-approval-2026-10-08/report.md`, `manifest-sha256.csv`, `source-direction-evidence.csv`, `common-full92-evidence.csv`, `body-720-final-evidence.csv`를 따른다.

- body19/363 source 프레임(726좌우 행), common53/92 source 프레임. 활성 원본과 검수 staged PNG455/455 SHA-256 동일. 재사용4primary와 추가49common의 검수 범위를 구분한다.
- 기존122누락 목록은 실제 클립/renderer 방향 매핑과 native 시각 검수로 해소했다. rc-t1080/rc-t720 203shots·7916frames, rc-b1080 19shots·5628frames, clean death1080와 rc3-deaths720 18방향72포즈·540frames를 포함한다. 각 완료 실행 errors0/exceptions0. 미완료 rc-b720는 완료로 쓰지 않고 기존 완료 b720의 기본270포즈와 rc-t720 보스특수46, 새 clean death를 결합했다.
- 실제 사망 defeat00..03은 4무기 좌우/두 해상도에서 결과 패널 전에 노출된다. hybrid 주검 타격과 보조 활 실제 탄 발사 양방향, 그라인더/머신 경고 가림 해소, 실제 Shaker ice projectile, boss_arrival00..03, 환불 상태 종료, 저장 오류 HUD 정상 복구를 담당이 직접 확인했다.
- enemy clean death 갤러리는 피격 섬광만 QA에서 제거해 원본 포즈를 확인한다. 실제 AI/전투·UI 맥락 영상과 갤러리 포즈를 구분하며 정상 완주 증거로 사용하지 않는다.
- 총은 길이1.28/body1.6=0.8, 이전0.64/body1.6=0.4로 정확히2.000배. 좌우 손 연결·총구 방향과1080/720 화면 검수 완료. PNG/anchor/userApproved/기존 history를 보존했다.
- 승인69매니페스트의 정확 경로와 최종 해시는 copied index에 있으며 index SHA-256 `20d0ce582f04b498d51ad546aef43827ed654d864f894fb5f395e336e3de8a44`, 검수 보고 SHA-256 `141da0dfddd79147d08e182212957e71c8c24ecb9be58c7728478c33da17b651`이다.
- `../logs/art-integration-final-2026-10-08.json`: 정식 ready72, provisional0. 승인 후보를 readyRoots 마지막에 지정하고 프리뷰 override를 제거했다. ArtSource/ArtProduction은 구현 담당이 편집하지 않았다. strict 출시 게이트는 변경하지 않았다.

최종 고정 art 입력 SHA-256: `8d132fe65f52b5cf6a7aea62002ead9eecf5de79f346a4bb234069eb91754f94`(실제 Unity Assets의PNG/meta/매니페스트/catalog 포함1306파일). 모든 source/runtime 해시는 frozen-source 자료와 최종 빌드 기록에서 대조한다. 아트 승인만으로 전체 game_complete를 주장하지 않는다.
