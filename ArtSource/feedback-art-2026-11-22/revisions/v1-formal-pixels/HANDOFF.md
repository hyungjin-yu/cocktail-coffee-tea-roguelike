# 11월 22일 플레이 피드백 정식 픽셀 아트

[반입 매니페스트](manifest.ready.json) · [기존 몹과 비교](previews/enemy-comparison.png) · [몸체 전체 프레임](previews/body-frames.png) · [바닥 효과 전체 프레임](previews/ground-fx-frames.png) · [아이콘 확대 및 20px 비교](previews/icons.png)

쓴맛 안개는 넓게 깔린 잔증기 위로 회보라 증기가 말린다. 예고 중 밑단이 양옆으로 늘고 증기 입자가 퍼진다. 빈 잔 도둑은 어두운 입구와 크림색 반사광으로 빈 유리잔을 드러내고, 낚아챌 때 앞손을 뻗고 달아날 때 잔이 진행 방향으로 기운다. 두 몸체는 기존 일반 몹에서 쓰는 2px 격자, 짙은 외곽선, 면 단위 명암을 따른다. 지면 비교 시트에는 기존 스월스틱 컨저러·밀크 포머·라임웨지·바텐더의 첫 대기 컷을 128px로 맞춰 붙였다. 정지 자세와 50% 축소에서 신규 몹의 넓은 증기 꼬리와 열린 잔 테두리가 각각 읽히는 것을 확인했다.

## 파일 및 반입

- `native/`에 태그와 편집 레이어가 있는 Aseprite 원본 21개가 있다. `ready/`에 RGBA PNG 83개가 있다. 매니페스트의 `destination`은 제안 반입 위치이며, 능력치 아이콘은 현재의 실제 로드 경로다. PNG는 Point, 압축 없음, mipmap 끄기, Full Rect를 사용한다. `pivot`은 Unity 기준 좌하단 원점의 정규화 좌표다.
- **몸체 46장**은 128×128, 128 PPU, 발 피벗 (0.5, 0.125)이다. 첫 대기 컷의 발선 위 실측 높이는 안개 90px, 도둑 70px이다. 오른쪽 원본으로 왼쪽을 수평 반전한다. `ArtLibrary`에 `bitter_mist`와 `glass_thief`를 등록하고 각 `EnemyDef.ArtId`가 해당 항목을 가리키게 한다. 현재 fallback 경로는 각각 `Resources/Icons/Enemies/swirl_stick_conjuror`와 `Resources/Icons/Enemies/milk_foamer_bgfix`이며 둘 다 다른 몹도 사용하므로 그 파일을 덮어쓰지 않는다. 기존 v1 일괄 검수표는 일반 몸체 256px와 기존 ID 목록을 가정하므로 이 납품의 128px PNG는 별도 등록하거나 반입기가 새 규격을 명시적으로 수용해야 한다. 기존 화면상 높이에 맞춰 `targetBodyHeightWorld`를 정하고 발 피벗부터 균일 배율을 계산한다.
- 안개와 도둑의 평상시 몸체 틴트는 흰색을 기준으로 둔다. 도둑의 2.8 거리 깜빡임은 `GlassThiefDriver`에서 유지하고, 머리 위 임시 원은 정식 실루엣 반입과 함께 제거한다. 접촉 성공 이벤트에서 낚아채기 클립을 시작한 뒤 도주 상태에 맞춰 `flee`를 반복한다. 접촉 및 도주 타이머는 시각 클립과 독립적으로 유지한다. `flee`의 방향은 실제 도주 속도에 맞춰 반전한다.
- **바닥 효과 20장**은 256×128, 센터 피벗이다. `DebuffMobs.cs`의 `PulseWarn`/`PulseEdge` 절차 원을 `bitter_mist_pulse`로 바꾼다. 6장·6fps 예고는 정확히 1초이며, 바깥 테두리가 시작부터 판정 경계를 표시한다. 6장·10fps 몸체 `action`은 예고 첫 0.6초에 재생하고 이후 파동까지 마지막 컷을 유지한다. 충전과 폭발 텍스처 자체에 반투명 타원이 있으므로 기존 Y 압축과 코드 색 틴트를 중복 적용하지 않는다. 외곽선의 화면 가로 지름을 반경 2.4의 두 배에 맞추고 판정은 기존대로 유지한다.
- `MixingRushAura.cs`의 `Disc`/`Ring` 절차 원은 `mixing_rush_aura`로 교체한다. 활성은 6장·8fps 반복, 꺼짐은 4장·12fps 단발이다. 현재 꺼짐 구간 0.45초를 유지할 경우 원본 약 0.33초의 네 컷을 그 구간에 맞춰 샘플링한다. 저이펙트는 활성 첫 컷과 기존 꺼짐 생략을 사용한다.
- **HUD 아이콘 2장**은 40×40, 센터 피벗이다. `HudView.Update`의 `stats.AttackSlowed`, `stats.HealBlocked` 조건에 각각 `status_attack_speed_down`, `status_heal_block`을 연결한다. `status_slow`는 이동 둔화에 사용하고 상태 이름·초 표시는 유지한다. 공격 속도 감소와 회복 차단이 함께 적용될 수 있다.
- **능력치 아이콘 15장**은 32×32, 센터 피벗이다. `Assets/_Project/Resources/StatIcons/stat_*.png`를 동일 파일명으로 교체하고 기존 `.meta`를 보존한다. 각 아이콘 본체에 `StatStyle.cs`의 정확한 RGB가 들어 있다.

## 검수

Aseprite 1.3.18.6 CLI에서 원본 21개를 다시 열었다. PNG 83개 전부를 재출력한 시트와 대조해 보이는 RGBA 픽셀, 캔버스, 프레임 시간, 클립 태그, SHA-256 및 첫 대기 컷의 발선을 확인했다. 레이어가 겹치는 반투명 바닥 효과는 Aseprite 재출력의 채널 반올림을 기준으로 PNG를 확정했다. [재출력 검증 기록](verification/aseprite-roundtrip.json)

[안개 몸체 GIF](previews/bitter_mist.gif) · [도둑 몸체 GIF](previews/glass_thief.gif) · [안개 파동 GIF](previews/bitter_mist_pulse.gif) · [혼합 러시 GIF](previews/mixing_rush_aura.gif). GIF는 클립 경계에 검수용 정지를 넣었으며 실제 재생 시간은 매니페스트를 따른다. 반입 후 Unity 런타임의 크기, 저이펙트 및 표식 교체 확인이 남았다.
