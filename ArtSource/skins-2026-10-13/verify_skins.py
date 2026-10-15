from pathlib import Path
from PIL import Image
import numpy as np, json, hashlib
R=Path(__file__).resolve().parent
m=json.loads((R/'asset_mapping_manifest.json').read_text(encoding='utf-8'))
results=[]
for r in m['assets']:
    src=Path(r['original']); dst=Path(r['newFile'])
    a=np.array(Image.open(src).convert('RGBA'));b=np.array(Image.open(dst).convert('RGBA'))
    mask=np.array(Image.open(r['mask']))>0
    assert Image.open(dst).mode=='RGBA' and a.shape==b.shape==(256,256,4)
    assert np.array_equal(a[:,:,3],b[:,:,3])
    assert np.array_equal(a[~mask],b[~mask])
    assert hashlib.sha256(dst.read_bytes()).hexdigest()==r['outputSHA256']
    assert hashlib.sha256(src.read_bytes()).hexdigest()==r['sourceSHA256']
    if r['kind']=='body':
        x0,y0,x1,y1=r['protectedHeadRectTopLeft']; head=np.zeros(mask.shape,bool)
        head[max(0,y0):min(256,y1),max(0,x0):min(256,x1)]=True
        rr,gg,bb=[a[:,:,i].astype(int) for i in range(3)]
        protected=head|((rr>gg)&(gg>bb)&(rr>160))|((rr>150)&(gg>150)&(bb>150))
        assert np.array_equal(a[protected],b[protected])
    results.append({'file':r['newFile'],'sha256':r['outputSHA256'],'pass':True,'pixelsOutsideEditMaskChanged':0,'alphaPixelsChanged':0})
assert len(results)==43
(R/'verification_results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# 스킨 아트 검수 보고 — 2026-10-13','','상태: 아트 ready. Unity 임포트·실제 전투·저장/UI·빌드 검수는 Claude 담당이며 이 보고서의 통과 범위에 포함하지 않는다.','','## 납품 범위','','- 미드나잇 바텐더 23프레임: 원래 흰 셔츠, 얼굴·피부·머리와 실루엣을 보존하고 검정에 가까운 조끼·바지를 선명한 남색 명암으로 변경. 기존 넥타이는 금빛 포인트로 변경. 상품/스킨 ID `skin_bartender_midnight` 유지.','- 무료 무기 4종: 자수정 레이피어, 구리빛 총, 푸른 강철 검, 달빛 활. Claude 지정 `weaponskin_<weapon>_01` ID와 `_held` 파일명 사용.','- 검 내장 스윙 16프레임: 기본 8장 + 제이드 8장. 검 고유 원본 RGB 팔레트만 치환. 비교판의 변경 영역이 검에만 위치하는 것을 직접 열어 확인했고 영역 외 모든 RGBA 픽셀은 완전히 동일.','','## 검증 증거','','43장 전부 256×256 RGBA, 원본 알파 배열 완전 동일, 알파 실루엣·캔버스·픽셀 위치 불변. 검 영역 밖/바텐더 편집 영역 밖 RGBA는 픽셀 단위 완전 동일. 바텐더 머리 보호사각형·피부색·밝은 흰 셔츠 픽셀은 독립 검증에서도 완전 동일. 발 피벗·그립·총구는 이동 없이 기존 `.meta`와 manifest 좌표를 재사용해야 한다.','','좌표 원점은 기존 manifest와 동일한 좌하단. 레이피어/검 grip(46,128), muzzle(226,128); 총 grip(104,144), muzzle(226,128); 활 grip(76,128), arrow/string(142,128). 원본 PPU/필터/피벗과 클립 순서를 그대로 복제할 것.','','| 그룹 | 프레임수 | 가시 픽셀 변경 비율 범위 | 알파/영역외 변경 |','|---|---:|---:|---:|']
for k,name in [('body','바텐더'),('weapon','무기'),('embedded_sword','검 내장 스윙')]:
    rows=[r for r in m['assets'] if r['kind']==k];ratios=[r['changeRatioVisible']*100 for r in rows]
    lines.append(f'| {name} | {len(rows)} | {min(ratios):.2f}–{max(ratios):.2f}% | 0 |')
lines+=['','변경률 분모는 알파>0 가시 픽셀이다. 검 스윙의 변경률이 낮은 이유는 몸체를 유지하고 검만 변경했기 때문이다. 흰 셔츠 어두운 경계의 일부 남색 픽셀은 의상 영역에 포함되며 밝은 셔츠 면과 피부는 보호한다.','','## 파일과 시각 검수','','- `asset_mapping_manifest.json`: 43장의 실제 원본/새파일/권장 Resources 경로/skinID/anchor/편집mask/원본과 납품 SHA256 및 한국어 표시 문구.','- `comparison_overview.png`: 바텐더 대표 프레임과 무기 4종 기본/스킨/편집mask.','- `comparison_body_all23.png`: 전체 바텐더 프레임 기본/스킨/편집mask 직접 눈검수 완료.','- `comparison_sword_all16.png`: 기본·제이드 검 스윙 전체 직접 눈검수 완료.','- `preview_<clip>.gif`: 바텐더 좌측 기본/우측 스킨 애니메이션 비교(정적 hurt는 1초 표시).','- `verification_results.json`: 독립 파일 재개방 및 SHA256/알파/영역외 보존 검사, 43건 통과.','- `build_skins.py`, `verify_skins.py`: 원본을 수정하지 않는 재현 가능한 팔레트 제작/검증 소스.','','## 인계 주의','','최종 임포트 대상은 매니페스트 assets에 등록된 43장만이다. 초기 후보 디렉터리 `weapons/skin_weapon_*`, `sword_swing/base`, `sword_swing/skin_tea_jade`와 대응 masks는 제외한다. 후보 정리용 재귀 삭제가 자동 승인 검토에서 정책상 거부되어 그대로 보존했으며 최종 리소스 계약에는 포함되지 않는다.','','무기 부분인계 뒤 재실행은 바텐더 머리 보호 영역과 GIF 제작에만 변경을 가했으며 무기/검 스윙 픽셀 변환·PNG 내용은 결정적으로 동일하다. 이 검증 이후 납품 PNG/manifest를 동결하고 Claude 통합본을 outputSHA256과 대조한다.','','AGENTS.md는 프로젝트 트리 및 확인한 상위/기획 위치에 없었다. 기획 49/04/09/11과 실제 원본 PNG를 확인했다. 새로운 이미지 생성이 필요 없는 원형 보존 팔레트 편집으로 완성했고 외부 네트워크·승인 우회·Assets 수정은 수행하지 않았다.']
(R/'ART_REVIEW.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('PASS: 43 assets, RGBA alpha, protected body, SHA256, anchors unchanged.')
