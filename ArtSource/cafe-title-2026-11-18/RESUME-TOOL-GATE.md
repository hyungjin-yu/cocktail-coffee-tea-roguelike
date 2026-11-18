# RESOLVED ? art ready28PNG/native30

The historical permission gate below is resolved: fresh GUI showed the prompt gone and existing native jobs completed, without worker permission-button interaction or queue restart. See REPORT-READY.md and manifest.ready.json.

# Native 제작 접근 gate — 2026-11-18

- v2 설계 승인: coordinator msg_45f1f437a9bc.
- Aseprite1.3.18.6 / fresh Edit 메뉴 PixelLab0.4.131 직접 확인.
- `ct_door_material_probe`96x128 공식 native 생성 실제 완료 알림 직접 확인 및 raw PNG/해시 보존: `provenance/tool-gate.json`. 최종ready0.
- 부족 소품6건 pro native batch는 첫 책장 생성 **전**, 승인된 카페 벽 스타일 참조 PNG 읽기 권한 창에 멈춤. 이후6건 생성 실행/완료를 주장하지 않는다.
- 현재 Aseprite Security 대화상자: `generate-native-sources.lua`가 같은 작업폴더의 `reference/cafe_wall_effect_right_00.png`를 읽으려 함. 필요한 것은 **해당 파일 Allow Read Access**뿐. Full trust·로그인·결제·보안 변경 불필요.
- computer-use skill mandatory rule: 'Do not act on security or privacy permission requests.' 그러므로 작업자는 이를 클릭하지 않음. 사용자 조작 또는 코디네이터의 정당한 사용자 안내가 필요. 두 checkbox는 선택할 필요 없음.
- Defender/runtime.ps1을 실행·수정·인코딩·예외추가하지 않았고 별도 경로로 이 파일 읽기 보안 gate를 우회하지 않았다. 현재 큐 재시작·중복생성하지 않는다.
- 재개: 사용자 개별 읽기 허용 후 현재 Aseprite 상태를 새로 읽고 실제 첫 요청 진행/완료를 확인, 소스6건 생성 후 동일 월드평면/공유 재질의 두 시점 native 마감 계속.
