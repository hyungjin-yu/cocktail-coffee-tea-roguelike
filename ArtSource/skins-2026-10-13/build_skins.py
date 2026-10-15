from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image, ImageDraw
def label(mask):
    labs=np.zeros(mask.shape,int);n=0
    for y,x in zip(*np.where(mask)):
        if labs[y,x]:continue
        n+=1;stack=[(y,x)];labs[y,x]=n
        while stack:
            yy,xx=stack.pop()
            for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:
                ny,nx=yy+dy,xx+dx
                if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not labs[ny,nx]:
                    labs[ny,nx]=n;stack.append((ny,nx))
    return labs,n

ROOT=Path(__file__).resolve().parent
P=ROOT.parent.parent/'Assets/_Project'
records=[]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p): return np.array(Image.open(p).convert('RGBA'))
def save(src,out,b,skin,resource,anchor,mask,kind):
    a=load(src); out=ROOT/out;out.parent.mkdir(parents=True,exist_ok=True)
    assert a.shape==b.shape==(256,256,4)
    assert np.array_equal(a[:,:,3],b[:,:,3])
    assert np.array_equal(a[~mask],b[~mask])
    Image.fromarray(b).save(out)
    m=ROOT/'masks'/out.relative_to(ROOT);m.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray((mask*255).astype('uint8')).save(m)
    changed=np.any(a[:,:,:3]!=b[:,:,:3],axis=2)&(a[:,:,3]>0)
    records.append(dict(original=str(src),newFile=str(out),recommendedResourcesPath=resource,skinID=skin,anchor=anchor,kind=kind,canvas=[256,256],mode='RGBA',alphaExact=True,silhouetteExact=True,outsideMaskExact=True,changedPixels=int(changed.sum()),visiblePixels=int((a[:,:,3]>0).sum()),changeRatioVisible=round(float(changed.sum()/(a[:,:,3]>0).sum()),5),mask=str(m),sourceSHA256=sha(src),outputSHA256=sha(out)))

bm=json.loads((P/'Art/v1/bartender/manifest.json').read_text(encoding='utf-8-sig'))
for fn in [f for c in bm['clips'].values() for f in c['files']]:
    src=P/'Art/v1/bartender'/fn;a=load(src);b=a.copy();r,g,bl=[a[:,:,i].astype(int) for i in range(3)]
    cloth=(a[:,:,3]>0)&(bl>r)&(bl>=g)&(r>=18)&(r<100)&((bl-r)>=3)
    hair=(a[:,:,3]>0)&(r>g*1.12)&(g>bl)&(r>35)&(r<165)
    labels,n=label(hair)
    biggest=max(range(1,n+1),key=lambda i:int((labels==i).sum()))
    yy,xx=np.where(labels==biggest)
    head=np.zeros(cloth.shape,bool)
    head[max(0,yy.min()-8):min(256,yy.max()+22),max(0,xx.min()-8):min(256,xx.max()+10)]=True
    cloth &= ~head
    # Keep deepest outlines. Preserve source light ordering in a luminous navy ramp.
    lum=(r+g+bl)/3
    ramp=[(22,38,78),(32,56,111),(44,77,148),(65,104,178),(103,142,205)]
    for lo,hi,col in zip([0,28,38,52,70],[28,38,52,70,300],ramp): b[cloth&(lum>=lo)&(lum<hi),:3]=col
    metal=(a[:,:,3]>0)&(r>90)&(g>120)&(bl<g*.85)&(abs(r-g)<65)
    # Existing cyan necktie becomes brass/gold; no new pixels or ornaments.
    tie=(a[:,:,3]>0)&(g>r*1.30)&(bl>r*1.15)&(g>70)&(r<150)
    gold=tie&~head
    for lo,hi,col in zip([0,100,160],[100,160,300],[(140,98,39),(218,165,66),(255,224,132)]):b[gold&(lum>=lo)&(lum<hi),:3]=col
    mask=cloth|gold
    name=Path(fn).name
    save(src,Path('body/skin_bartender_midnight')/name,b,'skin_bartender_midnight','CosmeticSkins/skin_bartender_midnight/'+Path(name).stem,{'frameAnchors':bm['anchorsByFrame'].get(fn,{}),'pivot':'copy existing .meta unchanged'},mask,'body')
    skin=(a[:,:,3]>0)&(r>g)&(g>bl)&(r>160)
    white=(a[:,:,3]>0)&(r>150)&(g>150)&(bl>150)
    assert np.array_equal(a[head|skin|white],b[head|skin|white])
    records[-1]['protectedHeadRectTopLeft']=[int(xx.min()-8),int(yy.min()-8),int(xx.max()+10),int(yy.max()+22)]
    records[-1]['headSkinWhiteExact']=True

def weapon_color(c,typ):
    r,g,b=c
    if typ in ('sword','rapier'):
        if r>g*1.3 and r>110:
            # pink fruit becomes blue tempered steel / violet amethyst.
            if typ=='sword':return (65,130,199) if g<135 else ((128,199,239) if g<180 else (215,242,255))
            return (155,98,208) if g<120 else ((203,154,235) if g<180 else (242,220,255))
        if g>r*1.15 and g>b*1.08:return (109,70,24) if g<130 else ((215,160,56) if g<220 else (255,221,125))
        if r>160 and g>150:return (225,191,105) if min(c)<220 else (255,237,174)
        return c
    if typ=='gun':
        lum=sum(c)/3
        if max(c)<65:return (53,29,23) if lum<30 else (93,47,30)
        if max(c)-min(c)<65 and lum>70:return (141,73,38) if lum<155 else ((220,140,69) if lum<220 else (255,220,149))
        return (87,48,112) if lum<100 else ((156,97,184) if lum<160 else (219,168,237))
    if typ=='bow':
        if r>g*1.3:return (21,31,70) if r<70 else ((47,68,131) if r<190 else (112,161,225))
        if g>r*1.1:return (112,75,25) if g<100 else ((216,164,69) if g<190 else (255,226,146))
        return (192,215,249)
    return c

palette={}
for typ in ['rapier','gun','sword','bow']:
    src=P/(f'Art/v1/weapon_{typ}/frames/weapon_{typ}_effect_right_00.png' if typ!='bow' else 'Resources/ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00.png')
    a=load(src);b=a.copy();pal={}
    for c in np.unique(a[a[:,:,3]>0,:3],axis=0):
        cc=tuple(int(v) for v in c); col=weapon_color(cc,typ);pal[cc]=col
        b[np.all(a[:,:,:3]==c,axis=2)&(a[:,:,3]>0),:3]=col
    palette[typ]=pal
    name=src.name;skin='weaponskin_'+typ+'_01'
    anchors= {'grip':[76,128],'muzzle':[142,128],'coordinateSpace':'source manifest pixel coordinates; keep existing runtime convention'} if typ=='bow' else json.loads((P/f'Art/v1/weapon_{typ}/manifest.json').read_text(encoding='utf-8-sig'))['anchorsByFrame']['frames/'+name]
    save(src,Path('weapons')/skin/(skin+'_held.png'),b,skin,'WeaponSkins/'+skin+'/'+skin+'_held',anchors,a[:,:,3]>0,'weapon')

for base,folder in [('Resources/ArtExtra/tea_sommelier_sword_swing','base'),('Resources/CosmeticSkins/skin_tea_jade','skin_tea_jade')]:
    for src in sorted((P/base).glob('tea_sommelier_sword_swing_right_*.png')):
        a=load(src);b=a.copy();mask=np.zeros(a.shape[:2],bool)
        for c,col in palette['sword'].items():
            if c==col:continue
            sel=np.all(a[:,:,:3]==c,axis=2)&(a[:,:,3]>0)
            mask|=sel;b[sel,:3]=col
        target='swing_base' if folder=='base' else 'swing_jade'
        save(src,Path('sword_swing')/target/src.name,b,'weaponskin_sword_01','WeaponSkins/weaponskin_sword_01/'+target+'/'+src.stem,{'pivot':'copy source .meta unchanged','weapon':'embedded; no attachment transform change'},mask,'embedded_sword')

assert len([r for r in records if r['kind']=='body'])==23
assert len([r for r in records if r['kind']=='weapon'])==4
assert len([r for r in records if r['kind']=='embedded_sword'])==16
manifest=dict(status='art_ready',spec='49 / 04 / 09 / 11; current dispatch instruction',bodyProductIDUnchanged=True,weaponSkinsFree=True,clips=bm['clips'],coordinatePolicy='Source manifests use bottom-left pixel coordinates. All pixels unchanged in position; retain original import metadata/PPU/pivot and original frame order.',assets=records,displayMetadata={'skin_bartender_midnight':{'name':'미드나잇 바텐더','description':'흰 셔츠에 선명한 미드나잇 남색 조끼·바지와 금빛 넥타이 포인트'},'weaponskin_rapier_01':{'name':'자수정 레이피어','description':'분홍 날을 자수정 보라로, 초록 테두리를 금빛 금속으로'},'weaponskin_gun_01':{'name':'구리빛 총','description':'은빛·청록 총신을 따뜻한 구리와 황동, 보라 장식으로'},'weaponskin_sword_01':{'name':'푸른 강철 검','description':'분홍 날을 푸른 강철로, 초록 테두리와 손잡이를 금빛으로'},'weaponskin_bow_01':{'name':'달빛 활','description':'붉은 활대를 남색·달빛 파랑으로, 양 끝을 금빛으로'}})
(ROOT/'asset_mapping_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

def board(rows,name):
    w=1040;h=45+len(rows)*290
    im=Image.new('RGB',(w,h),(34,37,46));d=ImageDraw.Draw(im)
    d.text((12,12),'BASE / SKIN / CHANGED PIXELS   (original 256px canvas)',fill='white')
    for i,r in enumerate(rows):
        y=45+i*290;d.text((12,y),Path(r['newFile']).stem+' | '+r['skinID'],fill='white')
        for j,path in enumerate([r['original'],r['newFile'],r['mask']]):
            tile=Image.new('RGBA',(256,256),(65,67,74,255));img=Image.open(path).convert('RGBA');tile.alpha_composite(img)
            im.paste(tile.convert('RGB'),(12+j*340,y+23))
    im.save(ROOT/name)
board([next(r for r in records if r['kind']=='body' and 'idle_right_00' in r['newFile'])]+[r for r in records if r['kind']=='weapon'],'comparison_overview.png')
board([r for r in records if r['kind']=='body'],'comparison_body_all23.png')
board([r for r in records if r['kind']=='embedded_sword'],'comparison_sword_all16.png')
for clip,c in bm['clips'].items():
    frames=[]
    for fn in c['files']:
        canvas=Image.new('RGB',(512,256),(65,67,74))
        for offset,path in [(0,P/'Art/v1/bartender'/fn),(256,ROOT/'body/skin_bartender_midnight'/Path(fn).name)]:
            im=Image.open(path).convert('RGBA');canvas.paste(im,(offset,0),im)
        frames.append(canvas)
    frames[0].save(ROOT/('preview_'+clip+'.gif'),save_all=True,append_images=frames[1:],duration=int(1000/max(1,c['fps'])),loop=0)
print(json.dumps({k:[r['changeRatioVisible'] for r in records if r['kind']==k] for k in ['body','weapon','embedded_sword']}))
