# 추가 피드백 아트 원본 인계 — 2026-11-17

**Art ready 48 PNG / 논리 자산 42종 / 최종 Aseprite 파일 51개.** Unity 반입·게임 검증 완료를 뜻하지 않는다. 실제 사람 플레이는 수행하지 않았다.

- 전체: `manifest.ready.json`
- 묶음: `manifest.americano.ready.json`(2), `manifest.hidden.ready.json`(9), `manifest.tails.ready.json`(9프레임/3클립), `manifest.containers.ready.json`(28)
- 개별: `ready/<assetId>.ready.json` 48개 — PNG/원본/생성 원본 해시, 목적지, 피벗, 검사 결과 포함
- 앵커: `ready/anchors-handoff.json` 29항목(머그1+통28), 각 묶음 앵커 JSON도 유지

## 실제 제작과 도구 증거

Aseprite 1.3.18.6 + 공식 PixelLab 확장 **0.4.131**을 사용했다. 과거 0.4.137 표기는 픽셀 글꼴을 잘못 읽은 작업자 오류이며 사용자 요구 버전이 아니었다. 공식 다운로드 `(2)` 파일 SHA256 `609161c5b72475b0b49a6cf593db74c421d4b7ed8a99259343c6884043a014f5` 및 설치 Lua 116개 일치를 확인했고, 불필요한 재설치·버전 위장·비밀값 읽기를 하지 않았다.

공식 Aseprite 확장 내부 `generate_no_style.generate()` / 머그 `generate_edit.generate()`로 실제 요청했다. 첫 실제 요청 완료를 확인한 뒤 나머지 요청을 실행했다. Orca 재시작 뒤 기존 Aseprite 프로세스가 없는 것을 확인하고, 완료 증거가 있던 히든·머그·꼬리를 중복 생성하지 않았다. 중단된 첫 통 결과는 별도 미확정 원본으로 보존하고 미완28통+신발 수정1건을 복구했다. 최종 공식 Generation queue 화면은 **0 active / 0 waiting / 29 recent**, Session history #29 신발 v2, #28 볼리비아, #27 탄자니아가 **Status: Completed**임을 직접 확인했다.

실제 생성 완료가 확인된 출력42개 중 최종 아트가 참조하는 생성 원본은41개다. 첫 신발이 빗자루처럼 나와 반려하고 실제 부츠 재생성 결과를 사용했다. 반려 원본1개와 중단 미확정 원본도 보존했다. 파일 존재만으로 생성 완료를 주장하지 않았다.

- `provenance/tool-access-current.json`: 공식 파일/설치 비교·실생성 gate·현재 상태
- `provenance/collected-generations.json`: 실제 완료 증거와 원본 PNG 해시42개
- `provenance/native-recovery-completion.json`: 마지막 공식 큐 완료 관찰
- `provenance/generation-specs.json`, `recovery-specs29.json`: 실제 요청 프롬프트
- `provenance/native-finish-*.json`, `finish-*.lua`: 실제 Aseprite 픽셀 마감 기록/원본 작성
- `raw/`: 변경하지 않은 생성 PNG 및 Aseprite 보관본42쌍
- `provenance/interrupted-output/`: 중단 전 첫 통 출력(미확정, 최종에 사용 안 함)

Orca computer의 runtime.ps1 보안 차단은 우회하지 않았다. 승인된 별도 native Windows 도구로 Aseprite 창만 제한적으로 조작했다. physical Escape 중단 후 GUI를 멈추고 파일 작업만 했으며, 사용자 재승인 msg_61b1c4330c86 후 새 상태를 읽어 마지막 큐를 확인했다. 이후 추가 생성 요청은 없었다.

## 납품 계약

| 묶음 | 수 | 실제 계약과 마감 |
|---|---:|---|
| 아메리카노 잔+mask | 2 PNG | `dl_coffee_americano_mug` + `_mask`, 36×36, PPU32, 아래중앙. 승인된 기존 머그의 액체창 바깥 픽셀·오른손잡이·원래 mask를 정확히 보존하고 창만 alpha0. 액체 anchor는 mask bbox와 동일. 용량380mL/채움82%/레시피 최대245mL 계약 기록 |
| 산지 제조통 | 28 PNG | 실제 ingredientId28개 → `dl_tin_<ingredientId>`, 32×32, PPU32, 아래중앙. 생성 몸체를 마감하고 닫힌 뚜껑을 제거해 원두가 보이는 열린 입구로 수정. 기존 도감 콘셉트에 대응하는 Y가지·산형·잎·꽃·왕관·초승달·물결·나선·빗금 등 서로 다른 구조 표식. 단순 색 교체 아님 |
| 히든 HUD | 9 PNG | 실제 확정 ID9종, 24×24, 가운데. 셰이커/펌프/얼음 보호/각성 원두/신발/허브 화분/두꺼운 머그/수직·기울어진 메트로놈으로 역할 구분. 실제 UI 26×24 슬롯 기준 1x 검토 |
| 꼬리 | 9 PNG, 3클립 | fire/bitter/dual 각각3프레임, 32×16, PPU128, pivot(30/32,8/16), 8fps(125ms) 반복. 불꽃3갈래/잎2갈래/분리 혼합 포크 및 명암으로 구분. Aseprite3프레임 master와 프레임별 원본 모두 제공 |

통마다 mouth/pourLip/grip를 실제 열린 입구와 몸체에 맞춰 기록했다. 모든 앵커가 정규화 범위 안·불투명 그림 위에 있고 오른쪽 pourLip 및 mouth 높이가 같은지 검산했다. 통은 액체 용기가 아니므로 불필요한 liquid/mask를 추가하지 않았다.

꼬리는 원래 화살 본체·크기·촉 tint·HitRadius를 바꾸지 않는 별도 시각 자산이다. 기존128px 화살의 깃 소켓 arrow(18,62), tea_arrow(18,63), 왼쪽아래 원점을 manifest에 기록했다. 별도 tip child는 제작하지 않았다.

목적지는 각 manifest의 `recommendedUnityPngDestination`을 사용한다. 기존 PNG의 `.meta` GUID를 보존하고 앵커29항목을 병합하는 작업은 Claude 담당이다. ArtSource 작업자가 Assets/C#/.meta를 수정하지 않았다.

## 실제 검사

- PNG48개: 캔버스·비어 있지 않음·투명 모서리·binary alpha·원본 magic/canvas·native pivot·Aseprite 재출력 픽셀 일치 모두 통과.
- 생성 원본42쌍: 전체 Aseprite 재개방/PNG 재출력 픽셀 일치, 최종 참조41개와 반려1개 명시.
- 머그: 흰불투명/투명 mask, 동일36×36 등록, 창 alpha0, mask가 잔 벽 안, liquid MinMaxRect와 mask bbox 일치. 창 바깥 승인 픽셀 및 원래 mask 전체가 정확히 같음.
- 꼬리: 실제 native3프레임·125ms·고정 소켓 영역·프레임3개 차이·상태3개 서로 다른 alpha 실루엣·master와 각 원본/PNG 왕복 일치.
- 통:28개 actual ID 매핑 일치·mouth/pourLip/grip 계약 검사 전부 통과·회색 전체 픽셀 동일한 자산 쌍0. 작은 크기에서도 몸체와 고유 표식의 차이를 직접 검토.
- 기존36: PNG36+Aseprite36 총72파일의 작업 전/후 SHA256 전부 일치. 승인된 버전 표기 정정은 이전 텍스트45개에만 적용했으며 경로 목록은 `provenance/prior-version-metadata-corrections.json`에 보존.

근거: `verification/asset-validation.json`, `raw-source-validation.json`, `container-contract-validation.json`, `*-validation.json`, `previous-36-preserved.json`.

## 미리보기와 검증 한계

- `previews/containers-color.png`, `containers-gray.png`:28통 1x/확대/3배경
- `previews/container-anchors-pour.png`:28통35도 회전 등록·mouth/입구끝/grip 표시
- `previews/hidden-finish-review.png`:9아이콘 1x/5x/회색/크림·녹색·암색
- `previews/tail-frames-contact.png`:3상태×3프레임
- `previews/tail-native-small-gray.png`:native32×16 및3x,4배경·회색
- `previews/tail-original-arrow-backgrounds.png`:원래 tea_arrow를 바꾸지 않은 소켓 합성
- `previews/americano-window-fill.png`, `americano-tilted-levels.png`:빈 잔/mask/예시 채움/0·35·65도 수평 액면 합성

모두 **아트 합성/내보낸 파일의 직접 시각 검토**이며 실제 Unity 화면·게임 물리·사람 플레이 증거가 아니다. 기울어진 액면은 등록/투명창을 보는 면적 기반 예시이므로 실제 유체 solver 검증은 Claude가 수행해야 한다. 런타임에서 원두가 입구끝에서 수신 스쿱으로 들어가는지, 실제 HUD 크기, 화살 소켓/속도/소팅, 머그 채움/거품 두께는 통합 후 검증 대상이다.

## 범위 보존

현행 `기획/기준 명세/04_아트 제작 기준 v1.0.md`의 2026-10-19 규칙은 **“스킨 또는 무기 제작을 요청할 때마다 해당 종류 최소10개”**다. 최신 사용자는 기존 임시 제조통·꼬리·히든표시·머그 내부액체 보완을 승인했고, 새 스킨/무기종 제작을 요청하지 않았다. 실제28산지·9HUD·3꼬리 상태·머그1+mask를 기준으로만 제작했으며10개를 채우기 위한 새 디자인/촉/히든을 자동 추가하지 않았다.

이전 확정36의 `dl_foam_layer` 및 잔/도감 원본은 그대로 보존했다. 새 거품 종류를 발명하지 않았으며 기존 거품의 정밀 런타임 검사는 통합 담당 영역이다. 재로그인·결제·구독 변경·새 fulltrust·보안 설정 변경·소리·PC 종료·커밋/푸시·공개배포·사용자 실제 저장 조작은 하지 않았다.
