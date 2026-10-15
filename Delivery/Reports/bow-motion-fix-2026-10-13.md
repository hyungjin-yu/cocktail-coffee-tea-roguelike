# 활 대기·이동 모션 복구 (Claude, 2026-10-13)

Orca Task `task_97ad715e1915` / Dispatch `ctx_01d6368a798e`. 사용자 피드백: "활 장착하면 대기와 걷기가 바뀌고 상체·하체가 분리돼 이상함".

## 원인
`PlayerVisual.UsesBowReady`(티 소믈리에 + weapon_bow)가 Tick/PlayAction에서 `ArtExtra/tea_sommelier_bow_ready`(기획 45, 2026-10-12: PixelLab ranged01/02 상체 + native 다리 합성) `bow_idle/bow_move/bow_ranged`를 재생했다. 무기 위치도 합성 전용 고정 손 (198,153)·(201,154) + `BowMoveBob`, 각도는 bow_* 클립에서 0°였다. 그래서 대기·걷기가 '겨누는 상체 + 걷는 다리' 합성으로 보였다(비취 스킨의 bow_* 프레임도 같은 합성본).

## 수정 (Scripts/Player/PlayerVisual.cs만)
- 합성 경로 제거: `UsesBowReady`, `BowClip`, `BowMoveBob`, 고정 손 좌표, bow_* 각도 분기, 장착 시 bow_idle 재생 삭제. 활도 다른 무기처럼 원본 `Art/v1/tea_sommelier` 전신 idle(4)/move(6)/ranged(4)/hurt(1)/defeat 클립을 쓴다.
- 손: manifest `anchorsByFrame` 프레임별 `hand`(모든 프레임에 있음). PNG 위 anchor 위치 검수(기본·비취 15프레임 오버레이)에서 모두 앞 손 주먹과 일치해 보완 좌표가 필요 없었다. 예) idle (162,88~90), move (158~166,86~90), ranged (181,148)(198,153)(201,154)(173,101), hurt (138,81).
- 각도(grip 기준, 위치는 손에 고정): 대기·이동·발사 03 복귀 −10°, 발사 00~02 0°(앞 손을 뻗어 겨냥, 시위 세로), 피격 −20°, 쓰러짐 −30°. 실제 PNG 합성 비교에서 −25°는 시위가 몸통을 대각선으로 가로질러 어색했고, −10°가 옆에 내려 든 활로 읽혔다.
- 유지: 실루엣·pivot·움직임·공격 판정·피해·공속·발사 위치(muzzle anchor), 활 v2·달빛 활 PNG, 비취 스킨 PNG. 합성 원본(ArtSource/bow-ready-2026-10-12)과 런타임 PNG(ArtExtra/tea_sommelier_bow_ready, 비취 bow_*)는 지우지 않고 재생만 하지 않는다(이력 보존).

## 검증 (이번 범위만)
- PlayMode 16/16 (`bow-motion-fix-2026-10-13/tests/playmode-related-16.xml`): MeleeSwingVisualTests(활 시험을 새 요구로 교체 — 기본/비취 몸체 × 기본/달빛 활 4조합, 좌·우, idle·실제 이동 move 6프레임 전부·ranged 4프레임·hurt·복귀에서 bow_* 미사용, 원본 전신 이름, 비취는 비취 의상 프레임, 활 grip = 그 프레임 hand anchor 오차 < 0.001, 임시 손 위치 미사용), CardAdviceAndWeaponSkinPlayTests, ChallengeAttackBehaviorTests, SkinEvidenceCapture.Capture_BowMotion_BodyAndBowSkinCombos.
- 근거 캡처(기존 SkinEvidenceCapture 렌더 경로 재사용, 플레이어 확대 카메라, 창 없음): `bow-motion-fix-2026-10-13/evidence/` — 4조합 × 좌우 × 대기·이동 2컷·발사·복귀·피격 48장, `bow_motion_contact_sheet.png`, `evidence-log.txt`(각 컷 body 프레임 이름·provisional=False·무기 각도·좌우 배율). root가 contact sheet를 보고 수락했다.
- 캡처 주의: 기본 몸체·기본 활의 왼쪽 발사 컷은 남아 있던 몹과 접촉해 피격 프레임으로 찍혔다(실제 피격, 다른 3조합의 왼쪽 발사 컷은 정상).

## 후보 실행본
- `Builds/Playable-2026-10-13-bow-motion-fix/CocktailCoffeeTea.exe` — strict 출시 빌드 Success(`tests/candidate-build.log`), 257파일 해시 `manifest-bow-motion-fix-2026-10-13.json` 읽기 전용 재대조 불일치 0, `CCT.Runtime.dll` SHA256 `bcc0beb491615c086b8c7a04aafa0a1720d4a8c1fe88df0fe10ed47ce464e7fb`. ZIP 없음(지시).
- 이 새 폴더 안에서만 Burst 디버그 `_DoNotShip` 폴더를 지우고 README/라이선스를 복사했다(코디네이터의 정리 생략 지시 전에 실행됨). 기존 실행본·ZIP·런처·사용자 세이브·사용자 게임 프로세스는 건드리지 않았다. 런처 연결은 root 담당.
- 보이는 창 QA는 root가 캡처를 수락해 실행하지 않았다(QA 투어 변형 빌드 `C:\UnityScratch\bowfix-qatour`는 만들었지만 실행하지 않음).

## 한계
- 전체 EditMode/PlayMode는 다시 돌리지 않았다. 실행본 자체의 화면은 창으로 보지 않았다(에디터 렌더 캡처 + 해시 검증).
