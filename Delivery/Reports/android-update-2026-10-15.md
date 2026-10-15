# Android 내부 테스트 업데이트 — 2026-10-15

음료 마스터 최신 수정본을 기존 내부 테스트 트랙에 배포 완료.

- 버전: 1.0.1 / versionCode 2 / 패키지 com.hyungjinyu.cocktailcoffeetea
- 콘솔 확인: 내부 테스터에게 제공됨 / 게시일 10월 15일 오전 9:12
- 테스트 링크: https://play.google.com/apps/internaltest/4701303261501617472
- 버전 상세: https://play.google.com/console/u/0/developers/7049446260299838873/app/4973761563020484327/tracks/4701303261501617472/releases/2/details

재료 수집·조합 조건 안내와 목표 선택, 새 드롭 스프라이트, 배경 가독성, 좌우 경계, 보스 방향 표시, 물방울 방향과 물방울·얼음 크기, 스킨 버튼 위치, 7분 이후 일반 소환 빈도·웨이브 수량 2배를 포함한다. 기존 생존 상한은 유지.

AAB: CocktailCoffeeTea-1.0.1-vc2-upload-signed.aab (111,798,089 bytes)
SHA256: e1b5b411f16f1b9eb4fb0f20d186de78bfe2fcfe84ad7bf70a775bb773ba6d6d
Unity Android IL2CPP ARM64 빌드 성공(exit 0), min API 26 / target API 36.
bundletool validate / jarsigner verify 모두 exit 0.
업로드 인증서는 이전 버전과 일치: A4:20:20:95:7C:AE:6F:A0:0D:66:D0:05:31:78:8B:EB:46:69:5F:2B:6C:A8:58:B4:EF:A0:83:CB:7D:7D:A7:BF.
ARM64 라이브러리 7개 PT_LOAD >= 0x4000 및 PAGE_ALIGNMENT_16K 확인. AD_ID 권한 없음.
네이티브 디버그 기호 ZIP 등록 확인. 처음 Windows ZIP 경로가 거부되어 슬래시 경로로 재압축 후 정상 등록.
기존 서명 도구가 임시 Gradle/JSON 비밀 값 4곳 제거. 키·비밀번호는 보고서에 기록하지 않음.

콘솔 경고 2개: 기존 대한민국 GRAC 추가 검토, Java 가독화 매핑 파일 없음. 배포 차단 오류 없음.
비공개 테스트와 프로덕션 트랙은 이번에 변경하지 않았다. Android 실기기 실행/설치 검증은 아직 필요하다.
증거: Delivery/Android-2026-10-15/evidence/internal-test-release.txt 및 .png, 이 폴더의 manifest/config/signature/16KB/build 로그.