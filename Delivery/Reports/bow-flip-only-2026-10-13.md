# 활 좌우만 반전 (Claude, 2026-10-13)

Orca Task `task_dfc920b8b986` / Dispatch `ctx_fd45dbf2bdd1`. 사용자 지시: "아니 좌우만 바꿔봐". bow-clearance-fix에서 비겨냥 자세(idle/move/return03/hurt/defeat)의 활을 겨냥과 반대 방향(비반전)으로 들던 것을 좌우만 뒤집었다. 이제 겨냥과 같은 바깥 발사 방향이다. 각도·위치·크기·클립·스킨 PNG·발사/피해/공속·임포트 기하 검사는 그대로다.

## 정확한 코드 변경
`Assets/_Project/Scripts/Player/PlayerVisual.cs` (실질 변경 1줄 + 주석)
```diff
-        /// <summary>들기/겨냥 자세의 무기 x 배율 부호. 시위 anchor가 없으면 기존처럼 항상 반전.</summary>
-        float BowScaleSign(bool aim) => bowStringSide == 0f ? -1f : (aim ? -bowStringSide : bowStringSide);
+        /// <summary>무기 x 배율 부호(겨냥·비겨냥 동일). 위 비반전 들기 설명은 아래 사용자 지시로 대체됨. 시위 anchor가 없으면 기존처럼 반전.</summary>
+        // 2026-10-13 사용자 지시("좌우만 바꿔봐"): 비겨냥 자세도 겨냥과 같은 방향(좌우 반전)으로 든다 — 각도·위치·크기·클립은 그대로,
+        // grip 고정 이동만 ApplyBowOrientation이 반전 배율로 다시 계산한다.
+        float BowScaleSign(bool aim) => bowStringSide == 0f ? -1f : -bowStringSide;
```
grip 고정은 기존 `ApplyBowOrientation`이 `localPosition = -Scale(gripLocal, 반전 배율)`로 수학적으로만 다시 계산한다. 그래서 활은 같은 손 anchor에 붙어 있다. 겨냥 방향과 발사 위치(`TryGetMuzzleWorld`, 겨냥 배율 기준)는 바뀌지 않았다.

## 시험 변경 (방향 기대값만)
- `Tests/EditMode/FirstPassArtTests.cs`: 주무기 활 대기 `localScale.x > 0` → `< 0`(겨냥과 같은 방향). 겨냥 `< 0`, grip 고정, 발사 앵커 검사는 유지.
- `Tests/PlayMode/MeleeSwingVisualTests.cs`: 몸통 관통 회귀(비겨냥 프레임에서 활 알파 경계가 손 앞)는 사용자 범위(좌우만)와 모순이라 제거했다. 대신 모든 프레임에서 활 좌우 방향이 겨냥과 같은지(`localScale.x < 0`) 확인한다. 원본 전신 클립·비취 의상·grip = 프레임 hand anchor 검사는 유지.
- 겹침을 줄이는 다른 변경은 하지 않았다(지시). 시위가 손 뒤로 와서 대기·이동 중 몸통 앞을 지나는 것은 이 방향의 결과로 그대로 남는다(이전 bow-clearance-fix에서 피한 부분).

## 검증
| 항목 | 결과 | 증거 |
|---|---|---|
| EditMode BakedArtCatalogTests·WeaponSkinTests(실루엣 계약 포함) | 16/16 | `bow-flip-only-2026-10-13/tests/editmode-16.xml` |
| PlayMode MeleeSwingVisualTests·SpecCombatInputTests(발사·관통)·활 캡처 | 12/12 | `tests/playmode-12.xml` |
| 엔진 렌더 캡처(기존 SkinEvidenceCapture, 몹 제거) | 4조합 × R/L × idle·move×2·aim·return03·idle_after·hurt | `evidence/bow_flip_only_contact_sheet.png`, 원본 56장, `evidence/evidence-log.txt` |
| strict 빌드 | Build Finished Success | `tests/candidate-build.log` |
| 격리 실행(숨김, 음소거, 전용 데이터) | 20초 생존, Exception 0, 내 PID만 종료 | `tests/candidate-smoke-player.log` |

## 후보 실행본
- `Builds/Playable-2026-10-13-bow-flip-only/CocktailCoffeeTea.exe`
  - `CCT.Runtime.dll` SHA256 `5F03D12993B31082F05C553DC08640EE47B7300B29A85274CB3383AA12E9F07A`
  - exe `3070E580…`(Unity 플레이어, 동일)
- ZIP·정리 없음. Burst `_DoNotShip` 폴더가 빌드 폴더에 남아 있다. 런처는 root가 갱신한다. 사용자 게임·세이브·기존 실행본은 건드리지 않았다.
