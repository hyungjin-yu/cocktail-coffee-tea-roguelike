# QA — Steam 마스터 도전 재개 세션 (2026-11-11, 부분 완료)

- 작업 공간: `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing`, 브랜치 `codex/coffee-brewing`, HEAD `50daa0ae3ccfa39e268b9dc40068b2445d02e0ba`(WIP). Git 변경(커밋/푸시/stash) 없음.
- 시작 기준선: `git status --porcelain` 239항목 → `Delivery/Reports/steam-master-2026-11-11/resume-status-snapshot.txt`, 시작 시 `git diff` sha256 `9a69ac273708c88bb6a8e436f04d44605bb3a6b6d6e9c4138a980b73447f67c4` (`resume-diff-sha256.txt`). 기준선 항목은 정리·초기화·덮어쓰기하지 않았다.
- 공모전 폴더/저장/제출물, 사용자 세이브, 비밀/개인정보 접근 없음. GUI·포커스·키보드·마우스·스크린샷·소리 사용 없음.
- **상태: 부분 완료(partial).** 코디네이터 지시(msg_b84e1d3f4cb4)로 RAM 4GB 미만 동안 Unity/player/렌더/빌드 실행 보류. 테스트·검수 빌드·화면 검증은 미실행.

## 1. 실행 기록 (headless batchmode, 동시 1개)

| # | 명령 | 시작(KST) | free RAM | 결과 | 로그 |
|---|---|---|---|---|---|
| 1 | `Unity.exe -batchmode -nographics -quit -executeMethod CCT.Editor.MasterChallengeArtImport.ImportBatch` | 12:31:55 | 4,153 MB | 종료 0, 컴파일 오류 0, 83 프레임 검증 로그 | `Delivery/Reports/steam-master-2026-11-11/import-art-1.log` |
| 2 | 같은 명령(상성 27 프레임 추가 후) | 12:39:34 | **3,659 MB (규칙 4GB 미달 — 위반, 보고함)** | 종료 0, 컴파일 오류 0·경고 0, 110 프레임 검증 로그 | `.../import-art-2.log` |

실행 2 이후 Unity 실행 없음. 이후 확인 시 free RAM 3,547 / 3,579 MB.

### 컴파일 범위 (기존 PASS 오인 금지)

| 변경 | 수정 시각 | 실행 2 컴파일 포함 |
|---|---|---|
| `MasterArt.cs`(상성 6 clip·TotalFrames·QualityClip/AffinityClip), `MasterChallengeArtImport.cs`(110 검사) | 12:33 | 포함 — 컴파일 PASS |
| `MasterVfx.cs`(신규), `Enemy.cs`(상성 VFX 호출), `MasterChallengeHud.cs`(품질 슬롯·컵) | 12:34–12:35 | 포함 — 컴파일 PASS |
| `MasterChallengeDirector.cs`(FairBannerShow 인수 아트 재작성·카메라 부착·시험용 속성) | 12:38 | 포함 — 컴파일 PASS |
| `MasterArtPlayTests.cs`, `MasterArtTests.cs`(신규) | 12:39:10 / 12:39:29 | 포함 — 컴파일 PASS(실행은 NOT RUN) |
| `DrinkStore.cs` schema v5 + `NoteRunCupsMigration` + `runCups ??=` | 12:40:41 이후(최종 편집 runCups 정규화 포함) | **미포함 — 컴파일 NOT RUN** |
| `EndlessRewardLedgerTests.cs`, `MasterChallengeTests.cs`(v5 기대값) | 12:40:45 / 12:41:06 | **미포함 — 컴파일 NOT RUN** |

이전 체크포인트의 EditMode 30/30·PlayMode 5/5(`editmode-master-2.xml`, `playmode-master-3.xml`)는 50daa0a 이전 코드 기준이며 이번 변경의 PASS가 아니다.

## 2. 아트 임포트 (Assets 적용)

- `Assets/_Project/Resources/MasterChallenge/`: PNG **110**, `.png.meta` **110**, 기타 0. 핵심 83 = `ArtSource/master-challenge-2026-11-11/frames/`, 상성 27 = `.../affinity/frames/`와 **110/110 바이트 일치**(cmp). PNG 전체 해시 목록의 sha256 `ba1cfeda76ca3ba1a714ee3df465d1aa05e3fcd2b4a1cfa884ca56b1b227cb29`.
- meta 정적 확인: PPU128 110/110, Point 110/110, mipmap 없음 110/110, 플랫폼 override 0, pivot NPC(.5,.125) 44/44, 현수막(.5,1) 8/8, VFX(.5,.5) 58/58, 기본 압축 없음.
- ArtSource 원본은 수정하지 않았다.
- 판정: 임포트·구조 **PASS**(실행 1·2 + 정적 meta). 게임 내 합성/미적 **NOT RUN**.

## 3. 정적 검토 결과 (코드 읽기만, 실행 아님)

| 항목 | 근거 | 판정 |
|---|---|---|
| schema v5: v4 이하 원본 1회 백업 | `NoteRunCupsMigration` — 대상 파일이 이미 있으면 복사 안 함, Original/Backup 두 로드 경로 모두 호출 | 정적 OK / 실행 NOT RUN |
| 백업 실패 시 | 경고 후 진행(값 변환 없는 추가 필드라 롤백할 변환 없음). 원본은 다음 저장 때 `.bak`로도 남음 | 정적 OK |
| 이전 v4 파일의 보관 잔 보존 | JsonUtility는 버전과 무관하게 `runCups` 읽음 → WIP v4로 저장된 잔도 유지. 없으면 `??=` 빈 목록 | 정적 OK |
| 미래 버전 쓰기 금지 | `Read`: version > 5 → Future → `FutureProtected`, `SaveNow` false. v4 빌드는 v5 파일을 Future로 보고 쓰지 않음 | 정적 OK |
| 음용 교체 | `GameManager.DrinkRunCup`: 잔 사용 확정 → 구효과 `Revert` → 새 효과 `ApplyTracked`(중첩 없음) → `PreserveHpAfterDrinkSwap` → `DrunkDrink`=실제 마신 종류 | 정적 OK(기존 Edit 시험 있음, 이번 미실행) |
| 기본 세이브 격리 | 신규 PlayMode 시험은 전역 `TestSaveIsolation`(세션 임시 경로·메모리 설정)에 의존, 저이펙트 설정 변경도 세션 경로 | 정적 OK |
| 상성 VFX 1 hit 1회 | 호출은 `TakeDirectHit`에서 A>0일 때만. `TakeDamage`(DOT·반사·파편)·0 피해·무적·미일치는 호출 없음. 같은 순간 다중 대상은 0.06초 간격으로 첫 1개만 | 정적 OK |
| 풀 상한 / low 교체 | 월드 동시 6, UI 1(연타 시 이전 것 반납 후 교체). `QualityClip/AffinityClip`이 low 여부로 이름 하나만 반환 → full·low 동시 재생 경로 없음 | 정적 OK |
| 종료 반납·재사용 초기화 | `Step`이 총 길이 도달 시 `Release`(sprite null·renderer off·비활성·풀). `Begin`이 frame0·elapsed0·white·scale·rotation0·flip false | 정적 OK |
| 재시작 | GameManager 파괴 시 UI 풀은 HUD와 함께 파괴 → `Purge`로 null 제거. 월드 풀 루트는 런 사이 유지(비활성 객체만) | 정적 OK, 실행 NOT RUN |
| 보상 몬스터 무피해 경로 | `Enemy.Harmless` 접촉·자폭 return, `AttachBehavior` 미부착, 정의 공격 0, `Apply` base≤0 → 0 | 정적 OK(기존 Play 시험 접촉만) |

## 4. 체크 판정 요약

| 체크 | 판정 |
|---|---|
| 아트 연결 코드 컴파일(실행 2 범위) | PASS |
| schema v5 및 관련 시험 컴파일 | NOT RUN |
| 110 프레임 임포트·설정 검증 | PASS |
| EditMode `MasterArtTests` / `MasterChallengeTests` / `EndlessRewardLedgerTests` | NOT RUN |
| PlayMode `MasterArtPlayTests`(일정·손-핀·VFX 풀·상성) / `MasterChallengePlayTests` 회귀 | NOT RUN |
| 오프스크린 증거 프레임(`MasterArt_OffscreenEvidenceFrames`, Explicit) | NOT RUN |
| 현수막 3.5초 실제 합성·손/천 연결·발 미끄럼 미적 검수 | NOT RUN |
| 정규 모드 회귀 전체 | NOT RUN |
| 별도 Windows 검수 빌드 / 원본 세이브 격리 실행 / 해시 일치 / 영상 | NOT RUN |
| 실행 자원(RAM ≥ 4GB) | BLOCKED(코디네이터 보류 지시) |

## 5. 알려진 위험 (실행 검증 필요)

1. 현수막 프레임 00–05의 천 끝은 매 프레임 바깥으로 이동, NPC 손은 설치 자세 고정 → 펼침 중간 손-천 분리 가능. 최종 06–07만 정렬(시험은 최종 핀 정렬만 단언).
2. 접근 0.8초 동안 화면 밖에서 설치 위치까지 이동 → 걷기 주기 대비 빠른 이동(발 미끄럼) 가능.
3. 품질 VFX는 제조 창 오른쪽 위 112px 슬롯(머리글 오른쪽 빈 영역) — 1080p/720p 실제 겹침 미확인.

## 6. 다음 실행 명령 (자원 확보 신호 또는 재할당 후, 하나씩)

```
U="C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe"; P=C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing; D=$P/Delivery/Reports/steam-master-2026-11-11
"$U" -batchmode -projectPath $P -runTests -testPlatform EditMode -testFilter "CCT.Tests.MasterArtTests|CCT.Tests.MasterChallengeRuleTests|CCT.Tests.MasterRunCupLedgerTests|CCT.Tests.MasterChallengeGameTests|CCT.Tests.EndlessRewardLedgerTests|CCT.Tests.EndlessModeTests" -testResults $D/editmode-resume-1.xml -logFile $D/editmode-resume-1.log
"$U" -batchmode -projectPath $P -runTests -testPlatform PlayMode -testFilter "CCT.Tests.MasterArtPlayTests|CCT.Tests.MasterChallengePlayTests|CCT.Tests.EndlessModePlayTests" -testResults $D/playmode-resume-1.xml -logFile $D/playmode-resume-1.log
"$U" -batchmode -projectPath $P -runTests -testPlatform PlayMode -testFilter "CCT.Tests.MasterArtPlayTests.MasterArt_OffscreenEvidenceFrames" -testResults $D/evidence-resume-1.xml -logFile $D/evidence-resume-1.log   # 출력 $D/frames/*.png (오프스크린, 창/소리 없음)
```
이후: 증거 PNG 육안 검수 → 필요 시 현수막 손 추적 결정 → 정규 모드 회귀 → 별도 검수 빌드(원본 세이브 격리).

## 7. 수정 파일 (기준선 239항목과 구분)

기준선 대비 새로 바뀐 항목만(`git status` 비교):
- 수정(M, 기준선에는 깨끗): `Scripts/Core/MasterArt.cs`, `Scripts/Core/MasterChallengeDirector.cs`, `Scripts/Drinks/DrinkStore.cs`, `Scripts/Editor/MasterChallengeArtImport.cs`, `Scripts/Enemies/Enemy.cs`, `Scripts/UI/MasterChallengeHud.cs`, `Tests/EditMode/EndlessRewardLedgerTests.cs`, `Tests/EditMode/MasterChallengeTests.cs`
- 신규: `Scripts/Core/MasterVfx.cs`(+meta), `Tests/EditMode/MasterArtTests.cs`(+meta), `Tests/PlayMode/MasterArtPlayTests.cs`(+meta), `Docs/MasterChallenge/IMPLEMENTED.md`, 이 보고서, `Delivery/Reports/qa-steam-master-2026-11-11.md`
- Unity 생성 meta: `Resources/MasterChallenge.meta`, `Scripts/Core/MasterArt.cs.meta`, `Scripts/Editor/MasterChallengeArtImport.cs.meta`, `Resources/MasterChallenge/*.png.meta` 110
- 아트 복사: `Resources/MasterChallenge/` 상성 PNG 27(핵심 83은 기준선부터 미추적 존재)

| 파일 | sha256 |
|---|---|
| Scripts/Core/MasterArt.cs | 38a67cc5a1a170ce31deafff7980a29c60e94e3eda255c14becc0faf827db963 |
| Scripts/Core/MasterVfx.cs | 86c53b8fefae779e21e60b94c978a9f3c5b921eeaaaafe3997d210396a004fe7 |
| Scripts/Core/MasterChallengeDirector.cs | b4b20d9c6db76b4ac66b881f0879e2c0a8f3ea773e1117df8cbbaad41bb5e976 |
| Scripts/Drinks/DrinkStore.cs | 1294a2304cb637f67b2f2829081b9aee862cf1a27d4e28fcacfcbc6dcee124e9 |
| Scripts/Editor/MasterChallengeArtImport.cs | a4e80d752f45e1ad4af133ba557093f1054803966d740b16c50e3f8aa91a4f20 |
| Scripts/Enemies/Enemy.cs | 2392e3755cd70c4d11dcf489a1a7e2a4b36ff1d451cb728d798dab4ee6f75420 |
| Scripts/UI/MasterChallengeHud.cs | 4428eff95b8b3469b6e5a53ef695a084d87e567351c8b2b16e5982ef70400511 |
| Tests/EditMode/EndlessRewardLedgerTests.cs | ae952e0257c3d9c97b7f8480b51b27ce5399e47f43952c614f842df7fcf03a3f |
| Tests/EditMode/MasterChallengeTests.cs | fb41751dee096e87ba0bc04075fd34a55d9d8fbbb0c4cc3120eeb820dd468f52 |
| Tests/EditMode/MasterArtTests.cs | 13fee043372ed0b1097f7b32996910792a9fb3978d3a7b8fc9c1c0fa19a9139b |
| Tests/PlayMode/MasterArtPlayTests.cs | 13d6573372d60a05a19797bd1aed805255ad358033bbf8bf6700fa86d0634ae5 |

(경로 접두 `Assets/_Project/` 생략.)

### 관련 diff 구간
- `Enemy.TakeDirectHit`: `Apply` 직전 `if (a > 0 && player.AffinityDrink is DrinkKind ak) MasterVfx.PlayAffinity(...)` 3줄.
- `MasterChallengeHud`: 필드 `fxSlot/submitCup`, `BuildBrewPanel`의 QualityFx 슬롯·SubmitCup, `PlayQuality`, `DrinkQualityFx.Play(..., slot)`에서 `MasterVfx.PlayUi`.
- `MasterChallengeDirector`: 292행 이후 `FairBannerShow` 전체 교체(Director 본문 무변경).
- `DrinkStore`: `CurrentVersion 5`, `RunCupsVersion`, `LoadResult.RunCupsBackupPath`, Load 두 경로의 `NoteRunCupsMigration`, `Normalize`의 `runCups ??=`.

## 8. GitHub/vault 반영용 필수 경로 (JSON)

```json
{
  "source": [
    "Assets/_Project/Scripts/Core/MasterArt.cs", "Assets/_Project/Scripts/Core/MasterArt.cs.meta",
    "Assets/_Project/Scripts/Core/MasterVfx.cs", "Assets/_Project/Scripts/Core/MasterVfx.cs.meta",
    "Assets/_Project/Scripts/Core/MasterChallengeDirector.cs",
    "Assets/_Project/Scripts/Drinks/DrinkStore.cs",
    "Assets/_Project/Scripts/Editor/MasterChallengeArtImport.cs", "Assets/_Project/Scripts/Editor/MasterChallengeArtImport.cs.meta",
    "Assets/_Project/Scripts/Enemies/Enemy.cs",
    "Assets/_Project/Scripts/UI/MasterChallengeHud.cs"
  ],
  "art": [
    "Assets/_Project/Resources/MasterChallenge.meta",
    "Assets/_Project/Resources/MasterChallenge/ (110 png + 110 png.meta)"
  ],
  "tests": [
    "Assets/_Project/Tests/EditMode/MasterArtTests.cs", "Assets/_Project/Tests/EditMode/MasterArtTests.cs.meta",
    "Assets/_Project/Tests/EditMode/MasterChallengeTests.cs",
    "Assets/_Project/Tests/EditMode/EndlessRewardLedgerTests.cs",
    "Assets/_Project/Tests/PlayMode/MasterArtPlayTests.cs", "Assets/_Project/Tests/PlayMode/MasterArtPlayTests.cs.meta"
  ],
  "docs": [
    "Docs/MasterChallenge/IMPLEMENTED.md",
    "Delivery/Reports/qa-steam-master-resume-2026-11-11.md",
    "Delivery/Reports/qa-steam-master-2026-11-11.md",
    "Delivery/Reports/steam-master-2026-11-11/import-art-1.log",
    "Delivery/Reports/steam-master-2026-11-11/import-art-2.log",
    "Delivery/Reports/steam-master-2026-11-11/resume-status-snapshot.txt",
    "Delivery/Reports/steam-master-2026-11-11/resume-diff-sha256.txt"
  ]
}
```

재개 지점: 위 6절 EditMode → PlayMode → 증거 프레임 순. 소스는 이 보고서 해시로 고정.

## 9. 갱신 — 코디네이터 재개 승인(msg_2f6bbebb5f5d) 후 실행 결과

상세 판정은 `qa-steam-master-2026-11-11.md`. 요약:
- editmode-resume-1(12:44:40 KST, schema v5 포함 컴파일): 94 PASS / 0 FAIL / 1 SKIP(기존 Ignored).
- playmode-resume-1(12:45:37): 정규 9 PASS, Explicit 증거 시험 첫 판 1 FAIL(구조 수정).
- evidence-resume-1(12:47:46): 타이틀 캔버스 덮임 → 무효(`frames-run1-titleoverlay/`).
- evidence-resume-2(12:48:19, **free RAM 3,789MB에서 시작 — 4GB 규칙 위반, 확인값을 게이트하지 않았음**): 26장 캡처. 현수막이 HUD 레벨 막대에 가려짐 FAIL → 배율 0.8·위치 1.45로 수정(이후 NOT RUN).
- 추가 파일: `Tests/PlayMode/QuietBatchAudio.cs`(+meta, 배치 시험 무음, 시험 전용).
- 4절 표의 "NOT RUN" 중 schema v5 컴파일/EditMode, MasterArt Edit/Play(배율 1 기준)는 위 실행으로 PASS 갱신. 배율 0.8 변경 이후는 NOT RUN.

갱신 해시(이전 표 대체):
| 파일 | sha256 |
|---|---|
| Scripts/Core/MasterChallengeDirector.cs | 4e3b63bc18d4826b042c877928f4b924e362b3a5a43a14ff108325a8f9270036 |
| Tests/PlayMode/MasterArtPlayTests.cs | 28caa21f8af52b84825df0bede4e8e161cd72a296fc2c7f9e7b3b241fcbab54f |
| Tests/PlayMode/QuietBatchAudio.cs | d9161eb3da6d092bc51db183969c42df525be4d040b14f01853fd21842623337 |

필수 경로 JSON 추가 항목: `"Assets/_Project/Tests/PlayMode/QuietBatchAudio.cs"`, `".meta"`, `"Delivery/Reports/steam-master-2026-11-11/frames/"`, `"Delivery/Reports/steam-master-2026-11-11/frames-run1-titleoverlay/"`, `editmode-resume-1.{xml,log}`, `playmode-resume-1.{xml,log}`, `evidence-resume-{1,2}.{xml,log}`.

## 10. 재개 세션 2 (task_8c0850e54a49, 13:00~13:26)

상세는 `qa-steam-master-2026-11-11.md` 6·7절. 요약: targeted PlayMode 10/10(resume-3·4·5), 전체 PlayMode 358/0/11skip, 전체 EditMode 933/1fail(시험 결함 D8 → 수정 후 fixture 3/3)/1skip/1inconclusive. 결함 D1–D8 수정·검증, D9(화면 밖 여백) 수정 NOT RUN. 빌드 NOT RUN(GATE 2회 거부). Git 변경 없음, ArtSource·공모전 미수정.

이번 세션 수정 파일(이전 해시 표 대체):
| 파일 | sha256 |
|---|---|
| Scripts/Core/MasterChallengeDirector.cs | c1e0af0f18d10e0162c8068045dad2c32dd1b4501aa45e0d20f5a5aedfd7be03 |
| Scripts/Core/MasterVfx.cs | 8b188f56027764ffd9a55716a9e0cd1d62ec21eacb16579f0b171d6322b22ec3 |
| Scripts/Core/MasterArt.cs | c8633b299feb313b6dcc1b9817418b95c42c791896efd9c9702be2289decda32 |
| Scripts/Core/GameManager.cs | 62c86e384bfe7ea9728d90313b448ea35f92128ad7a728219f49be4600dda967 |
| Tests/PlayMode/MasterArtPlayTests.cs | 85b6b34c5c370d85b49b8904a03bc8314e2feddb19adbfc0df5e07823b2d4b6a |
| Tests/PlayMode/MasterChallengePlayTests.cs | 19c05deb839fab49da898496698ee1079fb74f8bd78083880ff6c4e6f529ed88 |
| Tests/EditMode/StandardFamilyTableTests.cs | 8c7a3e516df36e2cfd39a468e0da8b6cc9186279636bd3196e2f83cb9d140b2a |
| Docs/MasterChallenge/IMPLEMENTED.md | 01a1cb4ebee309945735241d2da7c1c819860c66d0909adab9345efccbbe6727 |

추가 증거 경로(JSON 추가분): `Delivery/Reports/steam-master-2026-11-11/{playmode-resume-2..5,editmode-full-1,editmode-stdfamily-rerun,editmode-stdfamily-rerun2,playmode-full-1}.{xml,log}`, `frames/`(resume-5: 현수막 키프레임 720/1080, seq/ 36장, 설치완료+HUD, 품질 full/low, 상성 full/low, notes), `archive-scale1/`(수정 전 증거 3세트), `source-manifest-pre-build.sha256`.
재개 지점: `qa-steam-master-2026-11-11.md` 7절 명령 1→4.
