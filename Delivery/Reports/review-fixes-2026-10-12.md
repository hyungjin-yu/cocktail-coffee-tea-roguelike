# UI·직업·활 수정 검증 — 2026-10-12

## 적용 내용

- 커피 트리를 열고 Esc를 누르면 일시 정지 화면을 추가로 열지 않고 게임을 즉시 재개한다.
- 활은 기존 PixelLab 원본을 Aseprite에서 재편집한 양손 준비 자세를 사용한다. 대기·이동·공격에 적용하고 좌우 손잡이 정렬을 확인했다.
- 히든 효과는 H 화면에서 수치·활성 조건·재충전을 보여주며 실제 발동 때 안내와 짧은 VFX를 표시한다.
- 바텐더의 혼합 러시와 티 소믈리에의 깊은 우림에 실제 보너스와 HUD 안내를 적용했다.
- 긴 직업 강화 안내와 히든 도움말의 줄 길이를 정리했다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| EditMode 전체 | 430/430 통과 |
| PlayMode 전체, 테스트 수정 전 | 202/203 통과 |
| 활 테스트 수정 후 관련 PlayMode | 25/25 통과, 실패·건너뜀 0 |
| 출시 빌드 / QA 투어 빌드 | 성공 |
| QA 투어 1280×720 / 1920×1080 | 각각 캡처 16장, 오류 0, 예외 0 |
| 커피 트리 Esc 실제 입력 | Playing, paused=false, tree=false, timeScale=1 |
| 압축 패키지 | 230개 파일 SHA-256 및 ZIP CRC 확인 |

실패했던 활 테스트는 PlayerController.Facing을 변경하지 않아 SyncToCurrentFrame이 캐릭터의 기존 방향으로 복원하는 정상 동작을 실패로 판단했다. 실제 SetFacing 경로를 사용하도록 바꾸고 왼쪽 손과 무기 손잡이 좌표 일치도 추가 확인했다. 수정 후 전체 203개를 다시 실행한 결과로 보고하지 않는다.

QA 투어는 적 스폰을 정지하고 카드·히든 효과를 직접 부여한 검사다. 실제 Input System 키 이벤트를 사용하지만 일반 플레이로 조건을 해금한 완주 증거는 아니다. 장시간 자연 플레이와 직업 밸런스는 추가 검증 대상이다.

## 화면 증거

- [활 왼쪽 준비 자세](../../Docs/Verification/2026-10-12/review_bow_idle_L.png)
- [히든 효과 상세](../../Docs/Verification/2026-10-12/review_hidden_details.png)
- [보호막 실제 발동과 직업 강화](../../Docs/Verification/2026-10-12/review_hidden_block_activation.png)
- [커피 트리 Esc 재개](../../Docs/Verification/2026-10-12/review_coffee_escaped.png)

## 로컬 실행본

`Builds/Playable-2026-10-12-review-fixes-final/CocktailCoffeeTea.exe`

패키지: `CocktailCoffeeTea_1.0.0_review-fixes-final-2026-10-12.zip`

SHA-256: `adc79d7befbe45010004ed3e7aa9d058289facb5ef4289ee2f9e1e91ecd2096d`

실행 바로가기는 위 실행본으로 갱신했다. GitHub에는 소스와 화면 증거를 올리며, ZIP은 로컬 Delivery 폴더에 보관한다.
