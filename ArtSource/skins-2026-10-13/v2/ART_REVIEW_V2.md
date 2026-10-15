# 검 스윙 금빛 테두리 보정 v2

상태: 아트 ready, Assets/Unity 검증은 Claude 담당. v1 전체43장 및 기존manifest 불변. v2 매니페스트는 변경16장과 기존27장을 함께 매핑한다.

원인: 내장 검 원본의 녹색rim/guard RGB가 standalone 원본 팔레트와 달라 v1의 정확한 RGB 치환에서 일부 누락되었다. 기존 검 편집mask에 인접한 기본 프레임의 초록 영역을 연결 성장으로 분리하고 동일한 held 금속 팔레트 #6D4618 / #D7A038 / #FFDD7D로 보정했다. 검푸른날과 기존held는 유지한다.

기본/비취 두 원본에서 rim 픽셀 RGBA가 완전히 동일함을 확인하여 의상 픽셀을 포함하지 않음을 검증했다. 같은 rim 좌표마스크를 양쪽에 적용하며 얼굴/흰셔츠/기본·비취 의상은 모두 검 영역 외 RGBA 완전 불변이다. 원래 검mask와 새rim mask의 합집합 밖 변경0, v1 대비rim 밖 변경0, 알파 변경0, 캔버스256×256 RGBA 및 실루엣 불변. 앵커와 권장Resources 경로는 v1 그대로 보존한다.

held는 금빛 손잡이와rim·푸른 강철날이며 v2의 실제 내장 공격 이미지도 동일한 재질 설명과 일치한다. 검수판에서 held/v1/v2/최종mask를 나란히 확인한다.

| 파일 | 추가 rim 픽셀 | 원본 대비 가시변경률 |
|---|---:|---:|
| swing_base/tea_sommelier_sword_swing_right_00.png | 460 | 7.58% |
| swing_base/tea_sommelier_sword_swing_right_01.png | 428 | 7.84% |
| swing_base/tea_sommelier_sword_swing_right_02.png | 421 | 6.99% |
| swing_base/tea_sommelier_sword_swing_right_03.png | 320 | 4.45% |
| swing_base/tea_sommelier_sword_swing_right_04.png | 398 | 6.54% |
| swing_base/tea_sommelier_sword_swing_right_05.png | 485 | 8.25% |
| swing_base/tea_sommelier_sword_swing_right_06.png | 442 | 8.15% |
| swing_base/tea_sommelier_sword_swing_right_07.png | 440 | 8.53% |
| swing_jade/tea_sommelier_sword_swing_right_00.png | 460 | 7.58% |
| swing_jade/tea_sommelier_sword_swing_right_01.png | 428 | 7.84% |
| swing_jade/tea_sommelier_sword_swing_right_02.png | 421 | 6.99% |
| swing_jade/tea_sommelier_sword_swing_right_03.png | 320 | 4.45% |
| swing_jade/tea_sommelier_sword_swing_right_04.png | 398 | 6.54% |
| swing_jade/tea_sommelier_sword_swing_right_05.png | 485 | 8.25% |
| swing_jade/tea_sommelier_sword_swing_right_06.png | 442 | 8.15% |
| swing_jade/tea_sommelier_sword_swing_right_07.png | 440 | 8.53% |

검증: 최종43장 SHA256 재개방 검사통과(27장기존 그대로), 수정16장 픽셀보존/초록rim잔여0 검사통과. `v1_frozen_sha256.json`은 v1 43장 해시 증거이며 `verification_results.json`은16장 독립재개방 결과. `asset_mapping_manifest.json`에 최종SHA와 v1SHA/경로를 둘 다 기록한다.

인계대상: v2 매니페스트에 등록된43장. 실제 재임포트는 v2/sword_swing/swing_base8장·swing_jade8장만이며 Resources/WeaponSkins/weaponskin_sword_01/ 아래 기존경로에 교체한다. 삭제/기존27장 수정/Assets 수정은 수행하지 않았다.
