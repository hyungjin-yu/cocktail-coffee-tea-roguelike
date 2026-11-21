from pathlib import Path
import hashlib,json
from PIL import Image,ImageChops,ImageDraw
root=Path('ArtSource/cafe-title-2026-11-18/revisions')
v5=root/'v5-2026-11-20'
v7=root/'v7-street-2026-11-20'
r=root/'v9-menu-clips'
def img(p): return Image.open(p).convert('RGBA')
def data(p):return json.loads(p.read_text(encoding='utf8'))
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
layout=data(v7/'layout.json')
layers=layout['interior']['layers']
assets=[img((v7 if i<=1 else v5)/a['png']) for i,a in enumerate(layers)]
spec={
'start':([.232,.52],2.45,.38,10,.12,'Cup slides on its existing tray'),
'permanent':([.277,.398],3.4,.52,10,.18,'Mounted laptop displays a three-step screen wake'),
'codex':([.774,.455],2.65,.50,10,.15,'Shelf-supported reference book opens around its spine'),
'skins':([.738,.40],2.6,.48,10,.12,'Apron shifts beneath the fixed hanger rail'),
'history':([.253,.45],3.25,.44,10,.15,'Ledger cover lifts around the spine and exposes lined pages'),
'settings':([.492,.405],3.65,.53,10,.18,'Radio dial rotates and its pilot light warms'),
'credits':([.357,.231],3.2,.47,10,.17,'Pinned sheet curls outward along its lower edge'),
'quit':([.637,.345],2.25,.45,10,.12,'Existing hinged leaf yields and swings open before confirmation')
}
base=Image.new('RGBA',(480,270))
for a in assets: base.alpha_composite(a)
reference=img(v7/'previews/interior-composite.png')
assert max(max(c) for c in ImageChops.difference(base,reference).get_flattened_data())<=1
strip=Image.new('RGB',(960,8*141),'#25212a')
d=ImageDraw.Draw(strip)
records=[]
for i,f in enumerate(layout['interior']['features']):
 id=f['id']
 k=list(spec).index(id)+2
 center,zoom,seconds,fps,hold,meaning=spec[id]
 pathStem=f'ready/approach/{id}'
 frames=[f'{pathStem}_{j:02}.png' for j in range(6)]
 anim=[img(r/p) for p in frames]
 assert all(a.size==(480,270) for a in anim) and anim[0].tobytes()==assets[k].tobytes()
 crops=[((center[0]-.5/zoom)*480,(center[1]-.5/zoom)*270,(center[0]+.5/zoom)*480,(center[1]+.5/zoom)*270)]
 crop=tuple(round(x) for x in crops[0])
 assert 0<=crop[0]<crop[2]<=480 and 0<=crop[1]<crop[3]<=270
 distinct=len({im.tobytes() for im in anim})
 assert distinct>=3,(id,distinct)
 patches=[]
 y=i*141
 d.text((8,y+2),id+' / '+meaning,fill='#f4e6cf')
 for j,frame in enumerate(anim):
  composite=Image.new('RGBA',(480,270))
  for n,layer in enumerate(assets):composite.alpha_composite(frame if n==k else layer)
  strip.paste(composite.crop(crop).resize((155,112),Image.Resampling.NEAREST).convert('RGB'),(j*160+3,y+21))
  patches.append(composite)
 target=img(r/'previews'/f'{id}.png')
 assert max(max(c) for c in ImageChops.difference(patches[-1],target).get_flattened_data())<=1,id
 # All changed pixels lie inside the framing, so animation stays visible.
 delta=ImageChops.difference(patches[-1].convert('RGB'),patches[0].convert('RGB')).getbbox()
 assert delta and all([delta[0]>=crop[0],delta[1]>=crop[1],delta[2]<=crop[2],delta[3]<=crop[3]]),(id,delta,crop)
 action=dict(layer=layers[k]['id'],frames=frames,fps=fps,holdSeconds=hold,reverseOnReturn=True)
 f['approach']=dict(camera=dict(center=center,zoom=zoom),seconds=seconds,returnSeconds=.38,action=action)
 source=f'{pathStem}.aseprite'
 records.append(dict(id=id,layer=action['layer'],native=source,nativeSha256=sha(r/source),
  frames=[dict(path=p,sha256=sha(r/p),unityDestination='Assets/_Project/Resources/CafeTitle/'+p) for p in frames],
  uniqueFrames=distinct,finalDifferenceBounds=delta,contactFramingBounds=crop,action=meaning))
strip.save(r/'previews/actions-six-frame-strip.png')
write(r/'layout.json',layout)
baseCopy=data(r/'layout.json')
for f in baseCopy['interior']['features']:f.pop('approach')
assert baseCopy==data(v7/'layout.json')
write(r/'manifest.ready.json',dict(ready=True,revision=9,scope='Eight animation clips and observer camera fields over layout v7',
 layout='layout.json',layoutSha256=sha(r/'layout.json'),baseLayout=(v7/'layout.json').as_posix(),baseLayoutSha256=sha(v7/'layout.json'),
 canvas=[480,270],pivot=[.5,.5],pngCount=48,nativeCount=8,unityRuntimeVerified=False,assets=records,
 preview='previews/actions-six-frame-strip.png',checks=dict(nativeReopen=True,allFirstFramesMatchOriginalObject=True,
  finalFrameMatchesDepthRenderWithinOneChannelUnit=True,allFinalMovementsInsideCamera=True,onlyApproachFieldsChanged=True)))
print(sha(r/'layout.json'),sha(r/'manifest.ready.json'))
