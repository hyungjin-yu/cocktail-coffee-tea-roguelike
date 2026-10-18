"""Package the verified local Windows player and write reproducible delivery evidence."""
from pathlib import Path
import datetime,hashlib,json,zipfile,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
out=root/'Delivery/CardEducation-2026-10-16'
build=root/'Builds/Playable-2026-10-16-card-education'
archive=root/'Builds/CocktailCoffeeTea-CardEducation-2026-10-16-win64.zip'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
files=[p for p in sorted(build.rglob('*')) if p.is_file() and not any('DoNotShip' in part for part in p.parts)]
assert (build/'CocktailCoffeeTea.exe') in files
manifest=[{'path':p.relative_to(build).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in files]
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files:z.write(p,'CocktailCoffeeTea-CardEducation-2026-10-16/'+p.relative_to(build).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert len(z.namelist())==len(files)
    for p in manifest:
        assert hashlib.sha256(z.read('CocktailCoffeeTea-CardEducation-2026-10-16/'+p['path'])).hexdigest()==p['sha256']
proof={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'exeSha256':sha(build/'CocktailCoffeeTea.exe'),'zipSha256':sha(archive),'zipBytes':archive.stat().st_size,'fileCount':len(files),'files':manifest,'zipMatchesBuild':True}
(out/'package-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
tests={}
for name in ['editmode.xml','editmode-final.xml','editmode-tutorial.xml','editmode-verified.xml','playmode.xml','playmode-followup.xml','playmode-tutorial.xml']:
    x=ET.parse(out/name).getroot()
    tests[name]={k:x.attrib.get(k) for k in ['result','total','passed','failed','skipped','duration']}
(out/'test-summary.json').write_text(json.dumps(tests,indent=2),encoding='utf-8')
captures={}
for mode in ['720','1080','touch']:
    p=out/'screenshots'/mode/'capture-result.json'
    assert p.exists(),mode
    captures[mode]=json.loads(p.read_text(encoding='utf-8'))
    assert captures[mode]['overflowCount']==0 and not captures[mode]['modalSelectedCard'],mode
static=json.loads((out/'static-contract.json').read_text(encoding='utf-8'))
assert not static['issues']
types=json.loads((out/'production-types.json').read_text(encoding='utf-8'));assert types['passed']
rows=json.loads((root/'Tools/CardEducation/authored-data.json').read_text(encoding='utf-8'))
lines=['# 음료 지식 카드 개편 + 학습 튜토리얼 검증 보고서','',f"검증 시각: {proof['utc']}",'',
'## 결과','',
'기본 카드 34장과 하이브리드 1장의 확정 표시명·실제 지식·게임 해석·출처를 반영했습니다. 런타임 Default 생성과 Resources/Cards 직렬화 35장 데이터가 일치하며, 로딩 후 이름을 덮어쓰는 경로는 없습니다. 기존 ID·등급·중첩·수치·무기 제한은 유지했습니다.',
'',
'차의 PoisonLevel enum과 저장 인덱스는 유지하고, 플레이어 교육 표시를 떫은 여운/여운 피해로 변경했습니다. 히든 조건·추천·목표 진행·최종 형태 표시를 함께 갱신했습니다. 적의 유독 공격은 그대로 구별됩니다.',
'',
'실제 사실과 상징적인 게임 효과를 나누어 표시합니다. 산화와 미생물 발효, 찻나무와 허브 인퓨전, 보이 생차와 숙차를 구별합니다. 치료 효능·알코올 도수와 공격력의 관계·숙성 기간과 품질의 일률적인 우열을 가르치지 않습니다.',
'',
'## 새 콘텐츠 학습 안내','',
'첫 실제 카드 선택에서 제시된 첫 카드와 동일한 그림을 사용하는 4단계 안내가 자동으로 열립니다. 이름/쉬운 용어 풀이 → 그림의 재료·도구 찾기 → 현실 지식과 게임 효과 구분 → 상세 읽기와 직접 선택 순서입니다. 이전·다음·건너뛰기·완료·상세 이동과 카드 화면의 학습 안내 다시 보기를 제공합니다.',
'',
'settings.json의 추가 필드 cardEducationGuideVersion=1에 안내 확인을 기록합니다. 건너뛰기도 확인으로 기록하여 자동 안내를 반복하지 않습니다. 기존 tutorialSeen과 save.json 진행은 별개이며 기존 v2 설정에는 0을 기본값으로 적용합니다. 미래 버전 설정 원본은 덮지 않습니다.',
'',
'안내/상세가 열린 동안 카드 클릭·숫자 키 선택을 막고, 닫을 때 입력 해제 게이트를 다시 적용합니다. Esc는 최상위 안내/상세만 닫으며 LevelUp 상태와 timeScale=0을 유지합니다. 패드 초점 이동, 오른쪽 스틱 및 키보드 스크롤, 터치 스크롤 경로를 제공합니다.',
'',
'## 아트와 UI','',
'PixelLab 생성과 Aseprite 정리를 거쳐 코디네이터가 식별성을 승인한 최종 16종을 임포트했습니다. 각 PNG는 64×64, Point, 무압축, mipmap 없음으로 설정했습니다. 선택과 도감에서 실제 매핑을 사용하며, 전용 신규 아트가 없는 카드는 기존 카테고리 그림으로 표시합니다. 학습 안내에서는 공용 도안임을 명시합니다.',
'',
'원본 PNG와 Resources 임포트 PNG 16쌍의 SHA-256이 모두 같습니다. 전체 목록은 [static-contract.json](static-contract.json), 제작 기록은 ArtSource/card-education-2026-10-16의 manifest/report를 참고합니다.',
'',
'선택 카드에 짧은 실제 특징과 효과 연결을 표시하고 전문·최대 중첩 효과·출처는 별도 모달에서 제공합니다. 모달은 카드 선택을 수행하지 않습니다. 도감 전문은 스크롤하며 미발견 일반 카드는 이름·지식·전용 아이콘을 숨깁니다. 하이브리드는 기존 해금 조건 공개 규칙을 유지합니다.',
'',
'## 테스트','',
'| 결과 파일 | 총수 | 통과 | 실패 | 건너뜀 |','|---|---:|---:|---:|---:|']
for name,t in tests.items():lines.append(f"| [{name}]({name}) | {t['total']} | {t['passed']} | {t['failed']} | {t['skipped']} |")
lines+=['',
'최종 전체 EditMode는 542/542 통과했습니다. 첫 전체 PlayMode는 248개 중 241 통과, 3 실패, 4 선택적 증거 캡처가 Ignore였습니다. 세 실패는 옛 카드명 2개와 승인된 7분 이후 스폰 ×2를 반영하지 않은 기존 기대값 1개였고 게임 규칙은 변경하지 않았습니다. 해당 회귀와 교육 UI 후속 10/10, 튜토리얼·카드·추천·목표·도감 후속 19/19가 통과했습니다. 선택적 네 증거 테스트는 CCT_EVIDENCE_DIR가 없는 기존 조건으로 실행하지 않았습니다.',
'',
'튜토리얼 도중 실제 카드 획득이 발생하지 않음, 시간/일시정지 유지, 실제 카드 ID와 그림 일치, 이전/다음/상세/다시 보기, 기존 설정에서 최초 안내 및 완료 후 재노출 방지, 미래 설정 보존, 35장×4단계 본문 높이를 검사했습니다. 전체 회귀에는 활/외형, 모션, 저장, 7분 이후 및 영구 해금 스폰 배율 검사가 포함됩니다.',
'',
'## 실제 플레이어 화면 캡처','',
'| 모드 | 실제 렌더 크기 | 터치 UI | 텍스트 넘침 | 모달 자동 선택 |','|---|---|---|---:|---|']
for mode,c in captures.items():lines.append(f"| [{mode}](screenshots/{mode}/capture-result.json) | {c['width']}×{c['height']} | {c['touch']} | {c['overflowCount']} | {c['modalSelectedCard']} |")
lines+=['',
'캡처는 동일 런타임 소스의 Strict QA 플레이어에서 격리 저장과 명시적인 카드 제시 드라이버로 만들었습니다. 자연 플레이 완주 기록은 아닙니다. 자동 최초 안내, 드라이 진/루이보스 안내 4단계, 선택 카드 3그룹, 상세 창, 미발견·발견 도감과 출처 스크롤을 저장했습니다. 본문 검사는 카드 35장과 안내 140단계를 실제 렌더 크기에서 측정했습니다.',
'',
'대표 캡처: [720p 선택](screenshots/720/select-0.png), [720p 용어 안내](screenshots/720/tutorial-0-0.png), [720p 해석 안내](screenshots/720/tutorial-1-2.png), [1080p 안내](screenshots/1080/tutorial-1-3.png), [터치 안내](screenshots/touch/tutorial-1-3.png), [도감 출처](screenshots/720/codex-puerh-source.png).',
'',
'배포용 실행 파일도 -cct-data-dir로 사용자 저장을 격리해 직접 실행하고 OS 창/UI를 확인했습니다. 해당 수동 입력 증거는 screenshots/production 및 production-launch.log에 기록합니다.',
'',
'## 배포 산출물과 해시','',
'Windows x86_64 / Mono / 비 Development / 비 Script Debugging. 기존 씬·폰트·오디오·아트·데이터 검사를 통과한 Strict 빌드이며 최종 35카드/16스프라이트 검사도 통과했습니다. [production-build.log](production-build.log), [production-types.json](production-types.json), [source-summary.json](source-snapshot/source-summary.json).',
'',
'165개 관리 어셈블리의 TypeDef 메타데이터를 검사해 테스트·에디터·AutoTour·PlayVerifier·LoadScenario·CardEducationCapture 타입이 배포본에 없음을 확인했습니다. 새 학습 안내는 정식 런타임에 포함됩니다.',
'',
'- 실행 폴더: Builds/Playable-2026-10-16-card-education/',
'- ZIP: Builds/CocktailCoffeeTea-CardEducation-2026-10-16-win64.zip',
f"- 실행 파일 SHA-256: `{proof['exeSha256']}`",
f"- ZIP SHA-256: `{proof['zipSha256']}`",
f"- ZIP: {proof['zipBytes']:,} bytes, {proof['fileCount']} files; CRC 검사 및 모든 ZIP 파일의 빌드 파일 SHA-256 대조 통과.",
'- 상세 파일 해시: [package-proof.json](package-proof.json). DoNotShip 디버그 산출물은 ZIP에서 제외했습니다.',
'',
'## 보존 및 검증 범위','',
'PlayerVisual.cs, WeaponSkins.cs 및 기존 1.0.1 업로드 AAB는 코디네이터의 사전 기준 SHA-256과 일치합니다. 기존 Builds, 공개 ZIP, 사용자 저장을 덮어쓰지 않았습니다. 모든 실행 검증은 별도 -cct-data-dir를 사용했습니다. 스토어 업로드, Git push, Android 빌드와 AAB 재배포는 수행하지 않았습니다.',
'',
'터치는 Windows 터치 UI 모드와 모바일 비율 검증이며 Android 실기기 실행 검증은 아닙니다. 패드 내비게이션/스크롤 경로는 구현했지만 실제 하드웨어 패드 실기 검증은 하지 않았습니다. 종료 로그의 기존 D3D12/ComputeBuffer 정리 경고는 캡처 결과에 영향을 주지 않았으며 런타임 예외·교육 텍스트 넘침은 검증 결과에 없습니다.',
'',
'## 카드별 지식 출처','',
'각 문장은 출처의 식물·제조·도구·레시피 사실을 자체 한국어로 정리한 것이며, 게임 효과 해석은 별도 창작입니다. 생산자 자료의 건강·숙성 우열 마케팅은 사용하지 않았습니다.',
'',
'| ID | 표시명 | 1차 출처 |','|---|---|---|']
for row in rows:lines.append(f"| {row['id']} | {row['name']} | [{row['sourceName']}]({row['sourceUrl']}) |")
lines+=['','베르무트/캄파리 및 홍차 떫은맛의 추가 직접 출처는 각 카드의 additionalSources와 [authored-data.json](../../Tools/CardEducation/authored-data.json)에 함께 기록했습니다.']
(out/'REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in proof.items() if k!='files'},indent=2))
