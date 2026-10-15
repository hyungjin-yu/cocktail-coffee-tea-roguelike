# 활 몸통 가로지름 수정 + 무기 스킨 기하 계약 (Claude, 2026-10-13)

Orca Task `task_ca66035fcf00` / Dispatch `ctx_83e7594348e3`. 사용자 피드백: bow-motion-fix 후에도 활(시위·활대)이 몸통을 가로지름. 원본 전신 idle/move/ranged/hurt 클립은 유지했고, 합성 bow_ready는 되돌리지 않았다.

## 원인 (실제 PNG 알파 기하)
- `weapon_bow_v2_right_00.png`(256 캔버스): 활대는 왼쪽으로 휘고, grip(76,128)이 활대 배 정점이다. 시위는 x≈144~151의 200px 세로선이다(알파 열 측정).
- 손에 들 때 `HeldWeaponScale`이 x를 반전(−s)해서 활대가 앞, 시위가 손 뒤 (147−76)×0.51 ≈ 36 몸체px에 왔다. 전신 idle/move의 앞 손은 엉덩이 앞 가장자리(x 158~166)에 있어서, 각도와 무관하게 시위·활대가 몸통 한가운데(x≈126)를 지났다. 앵커 일치만으로는 해결되지 않는다.
- 실제 PNG 합성의 손 뒤 몸체 겹침 px(idle00+move02+ranged03 합): 반전 −10°(이전 후보) 2456, 반전 0° 2802, 반전 +90°/−90° 1432/1864(가로로 허리·다리를 가로지름), 비반전 0°/±10° 0, 비반전 −20° 14. 근거 `bow-clearance-fix-2026-10-13/root-cause/`(bow_orient_sim.png, bow_aim_hurt_sim.png, current_ingame_base_basebow_R_idle.png).

## 수정 — 선택지 A (root 승인)
`Scripts/Player/PlayerVisual.cs`
- 겨냥(ranged 00~02)일 때만 반전(시위가 당기는 손 쪽 — 정상 조준). 대기·이동·복귀(ranged 03)·피격·쓰러짐은 비반전이다. 손은 그대로 활대 배를 잡고, 활 전체가 손 앞쪽에 세로로 선다. 각도는 0°, 쓰러짐만 −30°.
- 방향은 하드코딩하지 않고 무기 manifest의 `string`·`grip` anchor에서 계산한다(들기 = 시위가 손 앞, 겨냥 = 시위가 손 뒤). string anchor가 없는 무기는 기존 고정 반전을 유지한다.
- 크기·위치 이동이나 몸 뒤 렌더는 쓰지 않았다(grip = 프레임별 hand anchor 유지).
- 발사 위치: `TryGetMuzzleWorld`가 활은 항상 겨냥 그림 기준 시위 위치를 계산한다. 대기 표시 방향과 무관하므로 화살 발사 지점·판정·피해·공속은 유지된다. 각도가 −10°→0°로 바뀌어 발사점이 약 0.03 유닛 이동하는 시각적 차이만 있다.

## 스킨 확장 계약 (새 스킨 작업 지침)
1. **같은 실루엣 빠른 경로(권장):** 무기 스킨 PNG는 원본과 캔버스·pivot·PPU가 같고 알파 실루엣이 픽셀 단위로 같아야 한다. 그러면 기본 무기 manifest의 grip/muzzle/string anchor와 몸체 프레임별 hand anchor로 정한 배치·방향·발사 위치를 스킨별 보정 없이 자동 공유한다. 색·재질만 다른 스킨이 여기에 해당한다.
   - 런타임: `WeaponSkinCatalog.HeldSprite`가 rect·pivot·PPU 불일치를 거부하고 기본 외형으로 되돌린다.
   - 임포트: `CCT.Editor.WeaponSkinArtImport.ImportBatch`가 원본 .meta 설정을 복제하고, 기하 + `WeaponSkinGeometry.SameSilhouette` 알파 실루엣 동일을 강제한다(다르면 실패).
   - 시험: `WeaponSkinTests.AllWeaponSkinPngs_ShareOriginalSilhouette`가 등록된 모든 무기 스킨(현재 20장)을 자동 검사한다.
2. **모양이 다른 스킨:** 같은 실루엣 경로를 쓸 수 없다. 별도 무기 아트로 manifest(grip/muzzle/string anchor, pivot, PPU, targetVisualLength)부터 만들어 새 무기 ID로 등록해야 한다. 스킨이 모양을 영원히 재채색으로만 만들어야 한다는 뜻은 아니며, 다른 모양은 명시적 기하 manifest가 필요하다. 지금 코드는 스킨별 기하 선언을 지원하지 않으므로, 이런 PNG는 임포트에서 실패해 잘못된 배치가 조용히 들어가지 않는다.
3. **시각 확인(기존 도구 재사용):** `SkinEvidenceCapture`(PlayMode, `CCT_EVIDENCE_DIR`) — 무기 4종 전투, 상점 비교, 활 4조합 × 좌우 × idle/move/aim/return/hurt 확대 컷. 새 활 스킨은 `Capture_BowMotion_BodyAndBowSkinCombos`의 조합에 추가하고, `MeleeSwingVisualTests`의 몸통 관통 회귀가 그 PNG의 알파 경계로 자동 검사한다.

## 검증 (관련 시험만)
| 항목 | 결과 | 증거 |
|---|---|---|
| EditMode BakedArtCatalogTests·ArtRuntimeConsumerTests·WeaponSkinTests | 31/31 | `bow-clearance-fix-2026-10-13/tests/editmode-related-31.xml` |
| PlayMode MeleeSwingVisual·CardAdviceAndWeaponSkin·ChallengeAttackBehavior·SpecCombatInput·Bow 캡처 | 23/23 | `tests/playmode-related-23.xml` |

갱신·추가한 시험:
- 활 방향 시험을 새 규칙으로 갱신했다(대기 비반전, 겨냥 반전, grip 고정, 발사 앵커 손 뒤).
- 몸통 관통 회귀를 추가했다. 4조합 × 좌우에서 비겨냥 프레임(idle, 실제 이동 move 6프레임, 복귀 03, hurt)마다, 실제 렌더되는 활 PNG(기본 v2/달빛 활) 알파 경계의 네 모서리가 앞 손보다 6 몸체px(손 접촉 범위)를 넘어 뒤로 가면 실패한다. 이전 반전 배치라면 시위가 36px 뒤라 실패한다.
- 무기 스킨 20장 실루엣 계약 시험을 추가했다.

엔진 렌더 contact sheet는 `bow-clearance-fix-2026-10-13/evidence/bow_clearance_contact_sheet.png`다(4행: 기본몸·기본활 / 기본몸·달빛활 / 비취·기본활 / 비취·달빛활 × R/L idle·move×2·aim(ranged_01)·return03(ranged_03)·idle_after·hurt, 원본 56장과 evidence-log.txt). 몹·적 탄을 제거한 격리 월드라 피격 오염이 없다. root가 열어 보고 수락했다.

## 후보 실행본
- `Builds/Playable-2026-10-13-bow-clearance-fix/CocktailCoffeeTea.exe` — strict 출시 빌드 Success(`tests/candidate-build.log`).
  - exe SHA256 `3070E580E019576B9AFCDEB4FEC18D03CEBC119F6AE84D597412905C749AD653`
  - `CCT.Runtime.dll` SHA256 `E827705E932E09599DC3F1D4FB729FD2329F46B259DE78786C52C5D65FCD0F59`
- 격리 실행: 숨김 창, 음소거 settings, 전용 데이터 폴더(달빛 활 장착 weapon_skins.json), 20초 생존, Exception 0, 내 PID만 종료(`tests/candidate-smoke-player.log`).
- ZIP·정리 없음(지시). 빌드 폴더에 Unity Burst `_DoNotShip` 디버그 폴더가 그대로 있다(배포 ZIP을 만들 때는 제외할 것). 사용자 게임·실제 세이브·기존 실행본·런처는 건드리지 않았다. 런처 연결은 root 담당.

## 변경 파일
- `Assets/_Project/Scripts/Player/PlayerVisual.cs`
- `Assets/_Project/Scripts/Commerce/WeaponSkins.cs`(WeaponSkinGeometry)
- `Assets/_Project/Scripts/Editor/WeaponSkinArtImport.cs`(실루엣 검사)
- `Assets/_Project/Tests/EditMode/FirstPassArtTests.cs`, `WeaponSkinTests.cs`
- `Assets/_Project/Tests/PlayMode/MeleeSwingVisualTests.cs`, `SkinEvidenceCapture.cs`(격리·복귀 컷)
- `Delivery/Reports/bow-clearance-fix-2026-10-13*`

## 한계
- 전체 시험은 다시 돌리지 않았다. 실행본은 숨김 실행만 했고 화면은 에디터 엔진 렌더 캡처다. 하이브리드 보조 활(등에 멘 weapon_tea_bow)은 이번 범위가 아니다.
