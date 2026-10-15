# 활을 손에 쥔 휴대 자세 (Claude, 2026-10-13)

Orca Task `task_bd86e665030a` / Dispatch `ctx_6a6a62ab4715`. 사용자: "활은 제대로 해놔야 할 거 아니야. 플레이어 캐릭터 손에 활이 들리게."

## 근거
- 앞 손: manifest `hand`가 전신 idle/move의 앞(바깥) 주먹과 일치한다(이전 PNG 오버레이 검수). 엉덩이 높이 주먹이다.
- 겨냥과 같은 바깥 방향의 세로 활은 −90°~+90° 어느 각도든 시위·활대가 몸통·허리와 겹쳤다(실제 PNG 합성, idle/move×2/return 합계 1941~3746px). 각도나 반전 한 줄로는 해결되지 않는다.
- 몸 뒤 세로 배치는 활이 거의 보이지 않는다.

## 수정 (기존 아트·렌더 순서만, 몸체 재편집·오프셋·크기 변경 없음) — `Scripts/Player/PlayerVisual.cs`
- **비겨냥**(idle/move/ranged 03 복귀/hurt):
  - 활은 겨냥과 같은 좌우 방향 그대로 손잡이(grip = 앞 주먹 anchor)를 쥐고, 활대를 앞뒤 수평으로 낮게 드는 휴대 자세(90°, 활대 배가 위·시위가 아래)다.
  - `sortingOrder −1`로 몸 뒤에 그려서 몸체 그림의 원래 주먹 픽셀이 손잡이를 덮는다(손가락이 쥔 모양). 몸통 겹침은 0이다.
- **겨냥**(ranged 00~02): 기존 그대로다(0°, 몸 앞 `sortingOrder 1`).
- **쓰러짐**: −30°.
- **발사 위치**: `TryGetMuzzleWorld`가 활은 겨냥 각도(0°)·겨냥 방향 기준으로 계산한다. 대기 휴대 자세에서 발사가 시작돼도 화살 위치가 돌지 않으며, 판정·피해·공속은 유지된다.
- 전신 클립·타이밍, 활·달빛 활·비취 PNG, 임포트 기하 검사는 그대로다.

## 검증 (관련만, 최종 수정 후 1회)
| 항목 | 결과 | 증거 |
|---|---|---|
| EditMode BakedArtCatalogTests(활 방향·grip 고정·발사 앵커) | 10/10 | `bow-hand-fix-2026-10-13/tests/editmode-10.xml` |
| PlayMode MeleeSwingVisualTests(4조합·좌우, 전신 프레임, grip = hand anchor, 새 검사) · SpecCombatInputTests(발사·관통) · 활 캡처 | 12/12 | `tests/playmode-12.xml` |

새 검사: 비겨냥 프레임은 무기 정렬이 몸체보다 뒤이고 활 알파 영역(실제 렌더 PNG 경계)이 손 높이 +6 몸체px 위로 올라가지 않는다. 겨냥 프레임은 몸 앞이다.

엔진 렌더(기존 SkinEvidenceCapture, 몹 제거)의 `evidence/bow_hand_zoom.png`, `evidence/bow_hand_contact_sheet.png`(4조합 × R/L idle·move·aim·return03·hurt), 원본 56장과 `evidence-log.txt`는 root가 보고 수락했다.

## 후보 실행본
- `Builds/Playable-2026-10-13-bow-hand-fix/CocktailCoffeeTea.exe` — strict 빌드 Success. `CCT.Runtime.dll` SHA256 `4B0BCC3DD3173950A4C35BD2860627F50BDAB406F227A7199BF4402EF00AF151`.
- 격리 실행: 숨김, 음소거, 전용 데이터. 20초 생존, Exception 0, 내 PID만 종료(`tests/candidate-smoke-player.log`).
- ZIP·정리 없음(`_DoNotShip` 남음). 런처는 root가 갱신한다. 사용자 게임·세이브·기존 실행본은 건드리지 않았다.

## 한계
- 이것은 기존 전신 자세로 만든 휴대 자세다. 활을 몸 앞에 세워 든 대기·이동 그림이 필요하면 Aseprite+PixelLab 프레임 제작이 필요하다.
- 전체 시험은 다시 돌리지 않았다.

## 추가: 휴대 자세 좌우 거울 (사용자 "거기서 활 좌우반전해봐")
- 비겨냥 자세(휴대·이동·복귀·피격·쓰러짐)만 화면 좌우 거울로 바꿨다. 각도 부호 반전(90°→−90°, 쓰러짐 −30°→+30°) + 그림 x 배율 부호 반전이다(R_x·Rot(θ)·S(sx) = Rot(−θ)·S(−sx)).
- grip·앞 주먹·몸 뒤 정렬(주먹이 손잡이를 덮음)·몸체·크기·겨냥(0°, 반전, 몸 앞)·발사 위치(겨냥 기준)는 그대로다.
- 관찰: 휴대 자세의 활은 grip(활대 길이 중앙)을 지나는 세로축에 대해 거의 대칭이라, 거울 결과가 수락본과 거의 같아 보인다(양 끝 장식 위치만 바뀜). 다른 변형(세로 반전 등)은 하지 않았다.
- 증거: `bow-hand-fix-2026-10-13/evidence-mirror/bow_hand_mirror_zoom.png`, `bow_hand_mirror_contact_sheet.png`, 원본 56장. 수락본 `evidence/`는 보존했다.
- 시험: EditMode 10/10(대기 x 배율 >0 기대값 갱신), PlayMode 11/12(`MeleeSwingVisualTests` 기대값: 겨냥 x<0·휴대 x>0, 몸 뒤·손 높이 아래 검사 유지). 실패 1건은 레이피어 근접 `SpecCombatInputTests.Melee_OtherLaneOutsideSemicircle`(활 코드 무관, 이전 같은 실행 통과)이다. 해당 클래스 단독 재실행 7/7 통과로 교차 시험 간섭으로 판단했다. 결과 파일은 `tests-mirror/`에 있다.
- 실행본 `Builds/Playable-2026-10-13-bow-hand-mirror/CocktailCoffeeTea.exe`(비거울 수락본 `bow-hand-fix`를 덮지 않음): strict Success, `CCT.Runtime.dll` SHA256 `2BCA52C3441AA5DA58E16E9E48CFB38741026B56EF9DFD2C3C2802EB5F37BEE7`. 격리 실행 20초, Exception 0, 내 PID만 종료. ZIP·정리 없음.
