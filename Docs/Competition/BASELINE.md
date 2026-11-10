# 기반·분리 기록 — 공모전판

작성일: 2026-11-10 · 갱신 2026-11-11(Steam 폴더 사후 비교 재확인: 상태 587항목·해시 동일, HEAD f7f3449)

## 작업 위치

| 항목 | 값 |
|---|---|
| 공모전 worktree | `C:/Users/user/orca/workspaces/CocktailCoffeeTea/pps-contest-2026` |
| 브랜치 | `hyungjin-yu/pps-contest-2026` |
| 기반 커밋 | `f7f3449470d40a88e21ad987e32ce772ea7e166a` (WIP: checkpoint interrupted endless continuation), 시작 시 작업 트리 깨끗함 |
| Steam 작업 폴더(읽기 전용) | `C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing` (브랜치 `codex/coffee-brewing`, HEAD 동일 커밋, 추적 수정·미추적 파일 587개 항목) |
| Unity | 6000.3.25f1 |

## Steam 폴더에서 읽은 것(수정 없음)

- `CLAUDE.md`, `.claude/skills/madia-ui-ux/SKILL.md` — 공모전 폴더에 AGENTS/CLAUDE/.claude가 없어 지침만 읽음.
- `git status --porcelain -uall` 목록과 수정·미추적 파일 sha256(작업 전 스냅샷) — 작업 후 비교용. 스냅샷 파일은 세션 임시 폴더에 보관하고 비교 결과를 QA 보고서에 적음.

## Steam 폴더에서 복사한 파일

**없음(0개).** 공모전판이 쓰는 코드·아트·폰트는 모두 기반 커밋에 이미 추적되어 있다. Steam 폴더의 미추적 파일(술병 아트 67개 등, `DrinkFirstSaleCapture.cs`, 시험 파일)은 공모전 흐름에 필요 없고, 기반 커밋의 추적 코드에서 참조하지 않는다(`grep DrinkFirstSaleCapture` 결과 없음). 따라서 복사하지 않았다.

## 재사용한 기존 아트(신규 아트·VFX 없음)

모두 `Assets/_Project/Resources/DrinkLab/`의 기존 추적 파일. 해시는 sha256 앞 16자리, 커밋은 마지막 변경 커밋.

| 파일 | sha256(16) | 커밋 | 용도 |
|---|---|---|---|
| dl_tin_green_tea.png | 134af3e919e00c2f | 123ec45 | 녹차 재료·포장 |
| dl_tin_chamomile_flower.png | 61a0f1c69c376e53 | dcfe425 | 캐모마일 재료·포장 |
| dl_tin_black_tea.png | c3030129020f9f22 | 123ec45 | 홍차(오답 공급안·오배송) |
| dl_tin_coffee_bean.png | 7e0004faa5e31bd6 | 43df8bd | 커피 재료·포장 |
| dl_teacup.png / _mask | 94f75a80fc3abbc7 / 383045861c3d7501 | 123ec45 | 찻잔 + 액체 스텐실 |
| dl_coffee_mug.png / _mask | 097f873d56def448 / bfdf5818808c7e53 | 43df8bd | 머그 |
| dl_coffee_cup.png / _mask | 1f815a822384277e / 22c6051a8f58d7c2 | 43df8bd | 커피잔 |
| dl_teapot.png | c5bce8ad699627ca | 123ec45 | 다관(제조대) |
| dl_water_kettle.png | 463cf5a6d5abcf27 | 123ec45 | 주전자(제조대) |
| dl_coffee_dripper.png | d7278e0256c94166 | 43df8bd | 드리퍼(커피 제조) |
| dl_craft_tea_wall.png / dl_craft_tea_table.png | 526840bd89cce6f4 / 0786fc8612b07eca | b18c03e | 배경 벽 / 입고 컨베이어 |
| dl_pour_stream_00~03.png | 5015eebecb26a22e 외 | 123ec45 | 붓기 프레임(기존 시퀀스 재생) |
| dl_steam_00~03.png | af32b20b3a47e42e 외 | 123ec45 | 김 프레임(기존 시퀀스 재생) |
| Resources/Fonts/NotoSansKR-Regular.otf | 6bcb2a0703aa137e | — | 기존 동봉 폰트(OFL) |
| 카페 UI 스킨(ArtV1 카탈로그 ui_cafe_*) | — | — | 기존 버튼·패널 9-slice, `UiFactory` 경유 |

## 본편과의 분리

| 항목 | 본편(기반) | 공모전판 |
|---|---|---|
| PlayerSettings companyName / productName | DefaultCompany / CocktailCoffeeTea | PPSContest2026 / BeverageMasterPPS |
| productGUID | 6006bcf4… | 9b7d2c4e… |
| Standalone 식별자 | com.DefaultCompany.2D-URP | com.PPSContest2026.BeverageMasterPPS |
| 버전 | 1.0.0 | 0.1.0-pps |
| Standalone 정의 기호 | 없음 | `PPS_CONTEST` |
| 시작 화면 | 본편 TitleView(무한 모드·상점·스킨 등) | `CCT.Contest.ContestApp` 전용 타이틀 |
| 저장 | persistentDataPath/save.json, settings.json, PlayerPrefs 구형 키 | `%USERPROFILE%/AppData/LocalLow/PPSContest2026/BeverageMasterPPS/pps_contest_save.json` (+ .bak), 또는 `-pps-data-dir`. PlayerPrefs 미사용 |
| 빌드 출력 | Builds/Windows64 등 | Builds/PPSContest(제출), Builds/PPSContestCapture(촬영 전용, 제출 금지) |

본편 코드 변경은 두 군데뿐: `GameBootstrap.AutoStart`와 `SkinCommerce.AutoStart`가 `PPS_CONTEST`일 때 바로 반환(본편 타이틀·상점 미노출, 본편 commerce 파일 미생성). 공모전 코드는 별도 어셈블리 `Assets/_Project/Contest/` (CCT.Contest, CCT.Contest.Editor, 테스트 2개).
