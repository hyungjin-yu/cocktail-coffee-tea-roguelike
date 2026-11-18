# 최종 아트 납품 — 2026-11-16

**아트 소스 ready 36/36. Unity integrated/verified는 이 작업에서 0이며 Claude가 반입·실화면 검수한다.**

인덱스: [manifest.ready.json](manifest.ready.json). 개별 에셋: `ready/<assetId>.ready.json` 36개. 이전 `manifest.blocked.json`은 과거 차단 증거이며 새 인덱스로 대체됐다.

| 납품 | 수량 | 규격 |
|---|---:|---|
| 라떼 유리잔 / 낮고 넓은 플랫화이트 / 볼 잔+받침 카푸치노 | 3 | 36×36 / 32×26 / 32×26 |
| 같은 캔버스의 액체 mask | 3 | 불투명 흰색 + 투명 |
| 거품 층 / 잠금 배지 | 2 | 32×8 가로 반복 / 16×16 |
| 현재 데이터에 정의된 산지 원두 아이콘 | 28 | 각 32×32, 실제 ingredient/icon ID 대응 |
| **합계** | **36** | PNG 36 + Aseprite 소스 36 + 개별 ready manifest 36 |

35개 raw PNG와 대응 native source도 보존했다. 여기에는 선택하지 않은 얕은 카푸치노 1차와 플랫화이트 2차 시안이 포함되므로 **raw 또는 provisional-preserved 폴더를 반입하지 않는다.** 원본 생성물 33개를 선택하고 mask 3개는 해당 잔의 기하에서 Aseprite로 파생했다.

## 실제 도구와 복구 증거

공식 계정에서 다운로드된 `PixelLab (2).aseprite-extension`만 사용했다(1,187,138 bytes, SHA256 `609161c5b72475b0b49a6cf593db74c421d4b7ed8a99259343c6884043a014f5`). Aseprite 공식 Add Extension으로 **0.4.130 → 0.4.131** 설치 후 재시작했고, 설치 Lua 116/116개가 해당 공식 배포 파일과 정확히 일치한다.

라떼는 native GUI 실생성 완료(seed 4169501354)와 원본 저장을 확인했다. 이후 Aseprite 안에서 설치된 공식 PixelLab `generate_no_style.generate()`를 호출해 순차 제작하고 공식 생성 archive의 최종 PNG를 사용했다. 별도 생성기·비공식 바이너리·버전 속임·직접 비밀키 읽기는 사용하지 않았다. 스크립트는 프롬프트/크기/표현 옵션만 설정하며 인증은 기존 공식 확장이 처리한다. 과거 첫 차단 작업의 직접 요청 스크립트는 후속 지시 이후 실행하지 않은 이력용이다.

백업은 저장소 밖 `C:/Users/user/AppData/Local/OrcaRecovery/PixelLab-before-update-20261116/`에 보존했다. 비밀 설정 백업과 다운로드 확장 파일은 ArtSource에 복사하지 않았다. 사용자 전경 조치 이후 플랫화이트 `.pixellab.aseprite`/`.aseprite`/`.png` 실제 저장을 확인해 코디네이터에 보고했다. worker는 새 full-trust 체크박스를 부여하지 않았다.

증거: `provenance/official-install-and-probe.json`, `batch-native-specs.json`, `batch-native-receipts.json`, `cappuccino-revision-spec.json`, `native-test-spec.json`, `native-finish-records.json`, `recovery-screenshots/` 및 [복구 이력](RECOVERY-INVESTIGATION.md).

## 마감과 검사

- 기존 컵·머그·원두·잠금 노드 및 문화 재료 원본을 직접 보고 픽셀 윤곽·시점·따뜻한 팔레트를 유지했다. 모든 픽셀 마감은 Aseprite native 작업이다. Python은 목록/검사/미리보기 구성에만 사용했다.
- 라떼는 생성된 높은 유리 실루엣을 유지하면서 액체 내부를 비우고 은회색/아이보리 유리 가장자리와 alpha96 반사선을 남겼다. 카푸치노는 볼+분리된 받침이 읽히는 실제 재생성본을 선택했다. 플랫화이트는 생성된 림·바닥·손잡이를 보존하고 반복된 몸통 3행을 native 편집으로 덜어 낮고 넓게 정리했다. 비균일 확대 대신 픽셀 편집과 균일 nearest 배치를 사용했다.
- 현재 `DrinkVesselView`는 액체 뒤에 잔을 만들고 잔 그림이 액체 위에 오므로, 도자기 잔에도 **mask 내부의 투명 액체 창**을 만들었다. opaque 그림 때문에 우유·거품이 가려지는 문제를 방지한다. mask는 손잡이·받침 밖으로 나가지 않는다.
- 산지 28종은 자루·틴·병·바구니·스쿱·접힌 봉투·원두 배열과 무늬가 다르다. 단순 색 교체가 아니며 흑백 비교를 직접 검토했다. 포장/무늬는 게임 식별용 표현으로, 산지의 실제 포장·맛·가공에 대한 사실 주장이 아니다.
- 거품은 아이보리 3색, binary alpha, 좌우 seam 일치와 중앙 연속 띠를 확인했다. 잠금은 닫힌 고리와 몸체/열쇠구멍으로 읽힌다.

검사 결과: **최종 36/36** 캔버스·알파 정책·투명 모서리·native source 1프레임·실제 native slice 피벗·원본→PNG 왕복 픽셀 일치 통과. **raw 원본 35/35** native 왕복 일치. mask **3/3** 캔버스/행별 그릇 내부/액체 창 가림 없음 통과. 거품은 3색·seam·연속 띠 통과. 산지 28종 동일 alpha 실루엣 0쌍; 겹침이 높은 16쌍은 별도 흑백/구조·무늬로 검토했다.

검증 원본: [asset-validation.json](verification/asset-validation.json), [raw-source-validation.json](verification/raw-source-validation.json), `verification/source-pivots.json`. PNG/소스 SHA256는 개별 manifest에 있다. Aseprite batch가 출력하는 기존 `handle-pose.lua:58` 확장 경고는 기록했으며 각 native export exit 0·픽셀 왕복 통과와 구분했다.

## 미리보기

- [잔·mask·채움 참고](previews/cups-masks-fill.png)
- [산지 28종 컬러](previews/origins-color.png)
- [산지 28종 흑백](previews/origins-gray.png)
- [거품 반복·잠금](previews/foam-repeat-lock.png)

미리보기는 정적 아트 검사이며 Unity 캡처가 아니다. 채움은 mask 폭² 누적의 약82% 참고 그림이며 실제 배합/거품 단계/헤드룸 판정은 Claude의 통합 후 검수가 필요하다.

## Claude 반입 인계

1. `manifest.ready.json`의 각 항목과 `ready/*.ready.json`만 기준으로 PNG를 지정 Resources 경로에 반입한다. 기존 3개 원두 아이콘을 교체할 때 GUID를 보존하고 .meta/코드는 Claude가 담당한다.
2. 잔과 mask는 동일 캔버스·바닥 가운데 피벗 `(0.5,0)`, 아이콘/잠금은 중심, 거품은 왼쪽 아래. 실제 source slice에도 같은 피벗을 기록했다. 기본 임포트는 Single Sprite, 128PPU, Point, mipmap off, 무압축 RGBA. 거품은 가로 Repeat/세로 Clamp.
3. `ready/anchors-handoff.json`의 새 잔 3개 mouth/grip/liquid를 기존 anchors 표에 병합한다. 좌표는 Unity식 왼쪽 아래 원점 정규화이며 카푸치노 손잡이/grip은 왼쪽이다. 용량은 확정 340/220/240mL 및 기존 coffeeStyle 판정을 유지한다.
4. 현재 `CoffeeStyles.CupArt/ResolvedCupArt`의 신규 잔 ID는 준비돼 있다. 거품 텍스처와 잠금 배지는 실제 UI 연결을 추가하고 임시 그림 문구/단색 띠를 해제해야 한다. 원두의 도감/획득 iconId는 `origin-inventory.json`의 실제 혼합 이름을 사용한다. 낙하 원두·스쿱·틴 등 기존 공용 제조 아트를 이번 아이콘으로 임의 교체하지 않는다.
5. 720p/1080p에서 빈 잔/최대 배합·우유·거품, 취소·저장·판매·음용, 도감 잠김/획득/검색과 아이콘 가독성, 누락 sprite/alpha/피벗을 실제 실행으로 검수한다. 원본 데이터·소비 규칙은 변경하지 않는다.

[19 꼬리·25 모티프 소량 검토표](ARROW-TAIL-AND-HIDDEN-REVIEW.md)는 원본 화살과 실제 녹색 바닥 QA 화면을 보고 작성한 **추가 규격 제안만**이다. 꼬리 3종×3프레임과 의미 약한 히든 모티프 2개를 최소안으로 적었으며 실제 추가 제작/반입은 없다. 스킨·무기 종류당 최소10 규칙은 이번 잔/도감 보완에 적용되지 않으며 새 디자인을 자동 확장하지 않았다.

이 worker는 Assets C#/.meta·씬·프리팹·사용자 저장을 수정하지 않았고 PC 종료·소리·커밋·푸시·공개 배포를 수행하지 않았다. 남은 작업은 Claude의 Unity 통합과 실화면 검수다.
