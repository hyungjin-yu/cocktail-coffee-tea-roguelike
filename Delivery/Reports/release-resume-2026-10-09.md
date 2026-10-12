# 2026-10-09 출시 품질 제작 재개 — 최종 검증 보고 (Claude)

Unity 6000.3.25f1 (e1dba0a9aba4), 기존 Packages 잠금 유지. 사용자 실제 저장·이전 빌드·실패 증거·원본 아트를 보존했고, 모든 시험은 `C:/UnityScratch/resume-2026-10-09` 아래 격리 저장(`-cct-data-dir`)으로 실행했다. Steam 게시·계정·결제 없음. 새 Codex 미실행. 난도 수치 무변경.

## 1. 인계 확인

- 인계 시 실행 중이던 전체 Play(PID17668, 초기 b3fb77d6): 175/175 통과. Codex 미컴파일 QA 3파일·Tools 검토 후 컴파일·빌드 성공 확인.

## 2. 최종 소스와 빌드

| 항목 | 값 |
|---|---|
| 최종 입력 2133파일 (Main=Scratch 0불일치) | `5151b0ab883b762101ad15202b6a89ef6970f070995afd437d1cc9e86180d217` (`source-files-2026-10-09.csv`, `source-summary-2026-10-09.json`) |
| runtime 85 / editor 15 / tests 177 / qa 3 | `d1df45e5…` / `d89b38da…` / `29ac1445…` / `b7fffc28…` |
| 전체 EditMode / PlayMode (같은 runtime·editor·tests) | **408/408**, **180/180**, 실패0·skip0 (`test-editmode.xml`, `test-playmode.xml`) |
| strict Windows64 | 성공(`build.log`), UI 8종 승인 게이트 포함, 출하 DLL QA 타입 0(`shipping-type-isolation.json`) |
| CCT.Runtime.dll (출시) | `a4cac69109707a9b635c8b372562ab10e084eda8e20058d5fd3136ce7a10b67d` |
| ZIP | `Delivery/CocktailCoffeeTea_1.0.0_Windows64.zip` 102,763,035 bytes, SHA256 `27651b46c3d179a1e8ec8fd7b8011a6cd675c4334dac60f1bf84e50ae3dda1ca`, 231파일, `manifest.sha256` 232항목 |
| 새 폴더 압축해제 | `C:/UnityScratch/최종 압축해제 2026-10-09` 231파일 해시 일치, CRC 통과 |
| 라이선스 | 출시 DLL 실제 대조 16패키지 원문 고지 = `Licenses/Packages` 동일 |
| launcher | `Play-Latest.cmd`/`Play-FirstPass.cmd` → `Builds/Playable-2026-10-09-final`(ZIP 내용과 231파일 해시 동일). 기존은 `*-before-2026-10-09.cmd`, 사용자 저장 `ArtProduction/first-pass-preview-user-data` 유지 |

고정 이후 QA 전용 변경: `AutoTour.cs`(활 우선 슬롯 촬영 인자 1줄)와 `PlayVerifier.cs`(입력 선택 플래그) — 출하 DLL에서 제외되며 runtime/editor/tests는 위 시험과 동일.

패키징 결함 수정: 이전 RC부터 ZIP에 `CocktailCoffeeTea_BurstDebugInformation_DoNotShip`이 포함됐다 → `package_rc.py`에 `*_DoNotShip` 제외 추가(기각 패키지 ZIP은 `rejected-package-1-donotship`에 보존).

## 3. 이번 구현 변경

- **38 영구 강화/결과 가독성**: 보유 스페셜티·해금 수 크게; 카드=이름/효과/가격/상태 칩(✓ 완료 · 밝은 "구매 가능" · 픽셀 자물쇠+이유); 선행·클리어·적용 무기·네 무기 몹 성장 예측은 선택 카드 상세(잠긴 카드도 키보드/마우스); 음수 잔액 → "스페셜티 N 부족". 결과=결과/시간/스페셜티 크게, 보조줄, 작은 통계, seed 접힘, 배경 더 어둡게, 전투 HUD 숨김(저장 실패 알림 유지), "다시 영업 시작 [R]" 주 버튼. 구매·저장·집계 수치 무변경.
- **실제 화면/키보드 검수 결함 수정**: 선행 미충족 노드 잔액 오표시; 커피 15노드 모두 해금 뒤 불가능한 "다음 해금"; 도감 상세 모달 비침; 일시정지 확인창 키 관통(→ 키가 뒤 메뉴로 빠져 Enter가 "설정"을 열던 G24 결함).
- **근접 표시**(06/10 전방 범위 시각표시를 사용자 지시로 대체): 파란 반원·바닥 채움 제거. 검=얇은 반월 베기(타격 프레임 동기), 레이피어=얇은 찌르기 궤적. 판정·사거리·피해·추가타 무변경.
- **검 휘두르기 몸체 클립(제작·적용 완료)**: PixelLab Aseprite 확장 0.4.127 "Animate with Text (v3)"를 Aseprite 1.3.18.6 GUI에서 실제 사용(seed 3270830880, 8프레임). 원본 `ArtSource/melee-swing-2026-10-09/`(native .aseprite, 입력 프레임, raw, manifest, sheet, gif). 게임에서 실제 타격마다 재생, 별도 손 검 숨김. 기존 tea_sommelier·weapon_sword 원본 무변경.
- **활 원본 최소 보정 v2(제작·적용 완료)**: PixelLab "Edit image (Pro)"(Edit with text)를 같은 GUI에서 실제 사용. 결과는 확장의 로컬 생성기록에서 바이트 그대로 보존 후 +4px 이동만 적용(grip 정렬). 원본 `ArtSource/weapon-bow-v2-2026-10-09/`(v1 참조, raw, native `weapon_bow_v2.aseprite`, `build_v2.lua`, manifest). 런타임 `Resources/ArtExtra/weapon_bow_v2`, v1 anchor 재사용, 표시 길이 몸 높이 0.5. v1 `weapon_bow` 파일 무변경.
- **무기–손 연결 체계 보정**: 네 무기 grip↔손 anchor 원본 프레임 오프라인 합성 감사(grip 위치는 일치, 모든 무기가 모든 클립에서 수평 고정이던 것이 원인) → 무기·클립·프레임별 grip 기준 각도. 하이브리드 티니 보우: 다리 뒤에 매달리던 배치 → 평소 어깨 뒤, 발사 때 앞 손+당기기 자세.
- 불안정 시험 2건 근본 수정(단언 기준 유지), QA 도구 보강(AutoTour `remaining` 모드, PlayVerifier 입력 플래그, Send-GameScanKey 포그라운드 처리).

## 4. UI 아트 승인 (위임)

8개 cafe UI `unityVerified/strictReleaseReady=true`(ArtSource·반입 사본 동일 바이트, `userApproved=false` 유지). 128행 검수표 `ArtProduction/release-review-2026-10-09/claude-unity-review/runtime-checklist-claude.csv`: **pass 128/128** — 활 초반 우선 슬롯(720/1080), 레이피어·검 최대 연속타 최종형태 좌우 연속 프레임(720/1080), 20:00 시간초과 결과(720/1080)를 실제 캡처로 채웠다(강제 QA 경로, 정상 플레이 근거 아님).

## 5. 정상 플레이 (strict PV, 새 저장, 영구 미구매, 성장 활성, 입력만)

| 런 | 결과 |
|---|---|
| 레이피어 | 1회차 클리어 11:36, 하이브리드 해금 |
| 총 / 검 / 활 | 1회차 클리어 11:29 / 11:31 / 11:26 |
| 활(v2 그림 최종 빌드 재확인) | 1회차 클리어 11:28, 오류0 |
| 포기 | "런을 포기했습니다", abandoned 1회 |
| 실패 | reckless 입력 34.6s 사망, failed 1 |
| 강화 보스 처치 후 사망 | 커피 노드 미선택 + 처치 뒤 회피 없이 접근: **4/4 재현**(레이피어 9:21, 총 8:50, 활 9:47, 검 10:50), 결과 "쓰러졌습니다"·스페셜티 +3·failed 1회·스페셜티 보존 |
| 실제 시간초과 | 약한 빌드 총 런 20:00 "영업 종료" |

보스 4종 150/320/500/680s 실제 경과. 모든 런 errors=0·exceptions=0. 클리어 시간이 목표 하한에 붙음(난도 수치 무변경, 사용자 결정 사항).

## 6. G01~G35

| G | 상태 | 근거 |
|---|---|---|
| G01~G09, G14~G17, G19~G21, G25~G29 | pass | EditMode 408 / PlayMode 180 + 해당 화면 |
| G10~G13 보스 | pass | 시험 + 투어 40/42 연속 프레임 |
| G18 커피 / G22 영구 / G23 도감 | pass | 시험 + 720/1080 캡처 |
| G24 전환·키 관통 | pass | 실제 OS 키보드 720/1080, 확인창 결함 수정 |
| G30 강제 종료 | pass | 최종 빌드 `final/g30`: 포기 1회, 체크포인트 5/8, 재개 없음 |
| G31 720/1080 + 키보드 | pass | 투어 + OS 키보드 2해상도 |
| G32 아트 | pass | strict 게이트 + 128/128 검수표 |
| G33 오디오 | pass | 현재 파일 25/25 재측정 |
| G34 압축해제 실행 | pass | 최종 ZIP(`27651b46…`) 새 폴더 해제본: 부팅→직업/무기→런→일시정지→런 포기(확인창 포커스 "포기")→"런을 포기했습니다"→타이틀→메뉴 종료(exit 0)→재실행→종료(exit 0), 한글·저장(포기 1회)·오디오(peak 0.52) 정상, 화면 13장 모두 게임 화면(`C:/UnityScratch/resume-2026-10-09/final/g34`). 앞선 3회는 사용자 포커스 전환으로 안전 중단(보존) |
| G35 부하 | pass | 1080 전체화면 단독, avg 59.94fps, p95 16.67ms, 100ms 정지0, private 1,273MB, 재시작 heap 1.057 (`performance*.csv/txt`) |

## 7. 비교 이미지

`Delivery/Reports/screenshots/2026-10-09-final/`: 상점·결과 전/후, 무기–손 감사 전/후, 활 v1/v2 원본·게임 모션, 검 휘두르기·레이피어 찌르기, 하이브리드, 활 우선 카드, 시간초과 결과, 최대 연속타.

## 8. 남은 조건

- 아트 `userApproved`(UI 8종, 검 휘두르기, 활 v2)는 사용자 확인 대기.
- 하이브리드 보조 활(`weapon_tea_bow`) 그림은 원본 그대로(배치만 수정).

## 9. 디스크 정리 (사용자 승인)

17:50 C: 가득 참 → 1차로 오늘 중간 산출물 정리. 이후 사용자 최대 정리 승인으로 `C:/UnityScratch/resume-2026-10-09/cleanup-2026-10-09-max.ps1` 실행: 생성 빌드·압축해제 복사본, Scratch Library 캐시, ZIP과 중복인 이전 빌드 폴더, 대용량 과거 로그, QA 녹화 프레임(CSV와 30프레임마다 1장 보존) 정리. C: 여유 11.61 → 66.57GB(+54.96GB). 경로별 바이트·사유 `cleanup-report-2026-10-09.csv`/`.txt`. 최종 ZIP·`Builds/Windows64`·launcher 빌드(231/231)·ArtSource·사용자 저장 재검증 완료. Scratch(`C:/UnityScratch/CCT`)는 다음 빌드 전에 Library 재임포트가 필요하다.
