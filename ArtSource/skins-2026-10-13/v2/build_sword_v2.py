from pathlib import Path
import json, hashlib, copy
import numpy as np
from PIL import Image, ImageDraw
R=Path(__file__).resolve().parent;V1=R.parent
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def load(p):return np.array(Image.open(p).convert('RGBA'))
def dilate(m):
    p=np.pad(m,1);return np.logical_or.reduce([p[dy:dy+256,dx:dx+256] for dy in range(3) for dx in range(3)])
old=json.loads((V1/'asset_mapping_manifest.json').read_text(encoding='utf-8'))
m=copy.deepcopy(old);checks=[]
for rec in m['assets']:
    if rec['kind']!='embedded_sword':
        assert sha(rec['newFile'])==rec['outputSHA256'];continue
    prev=copy.deepcopy(rec);a=load(rec['original']);b=load(rec['newFile']);v1=b.copy()
    baseRec=next(x for x in old['assets'] if x['kind']=='embedded_sword' and '/swing_base/' in x['recommendedResourcesPath'] and Path(x['original']).name==Path(rec['original']).name)
    base=load(baseRec['original']);oldmask=np.array(Image.open(baseRec['mask']))>0
    rr,gg,bb=[base[:,:,i].astype(int) for i in range(3)]
    green=(gg>rr*1.15)&(gg>bb*1.08)&(gg>25)&(base[:,:,3]>0)
    rim=green&dilate(dilate(oldmask))
    while True:
        nxt=green&dilate(rim)
        if np.array_equal(nxt,rim):break
        rim=nxt
    # Same geometric sword region for both body skins; reject any costume pixels.
    jadeRec=next(x for x in old['assets'] if x['kind']=='embedded_sword' and '/swing_jade/' in x['recommendedResourcesPath'] and Path(x['original']).name==Path(rec['original']).name)
    jade=load(jadeRec['original'])
    assert np.array_equal(base[rim],jade[rim]),'rim overlaps clothing'
    for lo,hi,col in [(25,130,(109,70,24)),(130,220,(215,160,56)),(220,256,(255,221,125))]:
        b[rim&(gg>=lo)&(gg<hi),:3]=col
    mask=(np.array(Image.open(rec['mask']))>0)|rim
    assert np.array_equal(a[:,:,3],b[:,:,3])
    assert np.array_equal(a[~mask],b[~mask])
    assert np.array_equal(v1[~rim],b[~rim])
    assert not np.any((b[:,:,1]>b[:,:,0].astype(int)*1.15)&(b[:,:,1]>b[:,:,2].astype(int)*1.08)&rim)
    out=R/'sword_swing'/Path(rec['newFile']).parent.name/Path(rec['newFile']).name
    out.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(b).save(out)
    mp=R/'masks'/out.relative_to(R);mp.parent.mkdir(parents=True,exist_ok=True);Image.fromarray((mask*255).astype('uint8')).save(mp)
    rp=R/'rim_masks'/out.relative_to(R);rp.parent.mkdir(parents=True,exist_ok=True);Image.fromarray((rim*255).astype('uint8')).save(rp)
    changed=np.any(a!=b,axis=2)&(a[:,:,3]>0)
    rec.update(newFile=str(out),mask=str(mp),outputSHA256=sha(out),previousV1File=prev['newFile'],previousV1SHA256=prev['outputSHA256'],rimMask=str(rp),rimPixels=int(rim.sum()),changedPixels=int(changed.sum()),changeRatioVisible=round(float(changed.sum()/(a[:,:,3]>0).sum()),5),bodyPixelsExact=True,rimMatchesHeldPalette=True)
    checks.append(dict(file=str(out),sha256=sha(out),alphaChanged=0,outsideSwordMaskChanged=0,outsideRimComparedV1Changed=0,baseJadeIdenticalOriginalRim=True,remainingGreenRim=0,rimPixels=int(rim.sum())))
assert len(checks)==16
# Reopen PNGs and verify all 43 authoritative outputs, including frozen 27 v1 assets.
for rec in m['assets']:
    assert sha(rec['newFile'])==rec['outputSHA256']
    if rec['kind']=='embedded_sword':
        a=load(rec['original']);b=load(rec['newFile']);mask=np.array(Image.open(rec['mask']))>0
        assert Image.open(rec['newFile']).mode=='RGBA' and a.shape==b.shape==(256,256,4)
        assert np.array_equal(a[:,:,3],b[:,:,3]) and np.array_equal(a[~mask],b[~mask])
m.update(version=2,status='art_ready',supersedes=str(V1/'asset_mapping_manifest.json'),changeScope='Only 16 embedded sword frames; other 27 v1 outputs unchanged')
(R/'asset_mapping_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
(R/'verification_results.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
(R/'v1_frozen_sha256.json').write_text(json.dumps({r['newFile']:r['outputSHA256'] for r in old['assets']},indent=2),encoding='utf-8')
held=next(r['newFile'] for r in old['assets'] if r['skinID']=='weaponskin_sword_01' and r['kind']=='weapon')
rows=[r for r in m['assets'] if r['kind']=='embedded_sword']
board=Image.new('RGB',(1320,50+len(rows)*280),(34,37,46));d=ImageDraw.Draw(board)
d.text((10,10),'HELD (frozen v1) / SWING v1 / SWING v2 / FULL SWORD EDIT MASK',fill='white')
for i,r in enumerate(rows):
    y=50+i*280;d.text((10,y),Path(r['newFile']).parent.name+' / '+Path(r['newFile']).stem,fill='white')
    for j,p in enumerate([held,r['previousV1File'],r['newFile'],r['mask']]):
        tile=Image.new('RGBA',(256,256),(65,67,74,255));tile.alpha_composite(Image.open(p).convert('RGBA'));board.paste(tile.convert('RGB'),(10+j*330,y+20))
board.save(R/'comparison_held_swing_all16.png')
board.crop((0,0,1320,50+4*280)).save(R/'comparison_held_swing_detail.png')
report=['# 검 스윙 금빛 테두리 보정 v2','','상태: 아트 ready, Assets/Unity 검증은 Claude 담당. v1 전체43장 및 기존manifest 불변. v2 매니페스트는 변경16장과 기존27장을 함께 매핑한다.','','원인: 내장 검 원본의 녹색rim/guard RGB가 standalone 원본 팔레트와 달라 v1의 정확한 RGB 치환에서 일부 누락되었다. 기존 검 편집mask에 인접한 기본 프레임의 초록 영역을 연결 성장으로 분리하고 동일한 held 금속 팔레트 #6D4618 / #D7A038 / #FFDD7D로 보정했다. 검푸른날과 기존held는 유지한다.','','기본/비취 두 원본에서 rim 픽셀 RGBA가 완전히 동일함을 확인하여 의상 픽셀을 포함하지 않음을 검증했다. 같은 rim 좌표마스크를 양쪽에 적용하며 얼굴/흰셔츠/기본·비취 의상은 모두 검 영역 외 RGBA 완전 불변이다. 원래 검mask와 새rim mask의 합집합 밖 변경0, v1 대비rim 밖 변경0, 알파 변경0, 캔버스256×256 RGBA 및 실루엣 불변. 앵커와 권장Resources 경로는 v1 그대로 보존한다.','','held는 금빛 손잡이와rim·푸른 강철날이며 v2의 실제 내장 공격 이미지도 동일한 재질 설명과 일치한다. 검수판에서 held/v1/v2/최종mask를 나란히 확인한다.','','| 파일 | 추가 rim 픽셀 | 원본 대비 가시변경률 |','|---|---:|---:|']
for r in rows:report.append(f"| {Path(r['newFile']).parent.name}/{Path(r['newFile']).name} | {r['rimPixels']} | {100*r['changeRatioVisible']:.2f}% |")
report+=['','검증: 최종43장 SHA256 재개방 검사통과(27장기존 그대로), 수정16장 픽셀보존/초록rim잔여0 검사통과. `v1_frozen_sha256.json`은 v1 43장 해시 증거이며 `verification_results.json`은16장 독립재개방 결과. `asset_mapping_manifest.json`에 최종SHA와 v1SHA/경로를 둘 다 기록한다.','','인계대상: v2 매니페스트에 등록된43장. 실제 재임포트는 v2/sword_swing/swing_base8장·swing_jade8장만이며 Resources/WeaponSkins/weaponskin_sword_01/ 아래 기존경로에 교체한다. 삭제/기존27장 수정/Assets 수정은 수행하지 않았다.']
(R/'ART_REVIEW_V2.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
print('PASS: v1 43 frozen; 27 unchanged; v2 16 alpha/body/rim/held material verified',[(r['rimPixels']) for r in rows])
