from pathlib import Path
import json,math,itertools
from PIL import Image,ImageDraw,ImageOps
r=Path('ArtSource/feedback-art-2026-11-17');anchors=json.loads((r/'ready/anchors-containers.json').read_text(encoding='utf-8'))['entries'];sheet=Image.new('RGB',(7*170,4*190),'#302b34');d=ImageDraw.Draw(sheet);checks=[];thumbs={}
for i,a in enumerate(anchors):
 id=a['assetId'];im=Image.open(r/f'ready/{id}.png').convert('RGBA');im=im.resize((96,96),Image.Resampling.NEAREST);tile=Image.new('RGBA',(150,150),'#49634b');tile.alpha_composite(im,(27,25));td=ImageDraw.Draw(tile)
 pts={}
 for k,col in [('mouth','#e8dd76'),('pourLip','#ff827b'),('grip','#72e6ef')]:
  x=a[k][0]*96+27;y=(1-a[k][1])*96+25;td.ellipse((x-2,y-2,x+2,y+2),fill=col);pts[k]=[round(x,2),round(y,2)]
 # Rotate artwork and markers together as a registered turn/pour illustration.
 rot=tile.rotate(-35,resample=Image.Resampling.NEAREST,expand=False,fillcolor='#49634b');x=(i%7)*170;y=(i//7)*190;sheet.paste(rot.convert('RGB'),(x,y+15));d.text((x+2,y+1),id.removeprefix('dl_tin_')[:24],fill='white');d.text((x+2,y+164),'mouth / lip / grip',fill='white')
 src=Image.open(r/f'ready/{id}.png').convert('RGBA');positions={k:(min(31,int(a[k][0]*32)),min(31,int((1-a[k][1])*32))) for k in ['mouth','pourLip','grip']};c={'assetId':id,'allNormalized':all(0<=n<=1 for k in ['mouth','pourLip','grip'] for n in a[k]),'allOnOpaqueArt':all(src.getpixel(p)[3]==255 for p in positions.values()),'lipRightOfMouth':a['pourLip'][0]>a['mouth'][0],'mouthAndLipSameOpeningHeight':a['pourLip'][1]==a['mouth'][1],'gripBelowMouth':a['grip'][1]<a['mouth'][1],'noUnusedLiquidMask':not(r/f'ready/{id}_mask.png').exists(),'positionsTopLeft':positions};c['pass']=all(x for k,x in c.items() if k not in ['assetId','positionsTopLeft']);checks.append(c)
 thumbs[id]=ImageOps.grayscale(src).tobytes()
exact=[list(p) for p in itertools.combinations(thumbs,2) if thumbs[p[0]]==thumbs[p[1]]];sheet.save(r/'previews/container-anchors-pour.png');result={'scope':'Art registration proof only; actual renderer integration not run','containerCount':len(checks),'anchorChecks':checks,'exactGrayscaleDuplicates':exact,'pass':all(x['pass'] for x in checks) and not exact};(r/'verification/container-contract-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print('Anchor contract pass',result['pass'],'count',len(checks),'failed',[x for x in checks if not x['pass']])
