import json,pathlib,hashlib,struct
from PIL import Image
root=pathlib.Path(__file__).resolve().parent
specs=json.loads((root/'specs.json').read_text(encoding='utf-8'))
items=[]
reviewed=json.loads((root/'reviewed.json').read_text())
handoff=root.parent.parent/'Delivery/Reports/ui-icons-2026-11-12-handoff.md'
labels={};section=''
for line in handoff.read_text(encoding='utf-8').splitlines():
 if line.startswith('## A.'): section='research'
 elif line.startswith('## B.'): section='card'
 elif line.startswith('## C.'): section='mastery'
 elif line.startswith('## D.'): section=''
 if section and line.startswith('| '):
  cells=[x.strip().strip('`') for x in line.strip().strip('|').split('|')]
  if len(cells)>3: labels[(section,cells[0])]=cells
for spec in specs:
 name=spec['file']; png=root/(name+'.png'); native=root/(name+'.aseprite')
 if not png.exists(): continue
 frame_count,width,height,depth=struct.unpack_from('<HHHH',native.read_bytes(),6)
 assert (frame_count,width,height,depth)==(1,64,64,32),name
 im=Image.open(png).convert('RGBA'); alpha=im.getchannel('A'); box=alpha.getbbox()
 assert im.size==(64,64) and box and min(box[0],box[1],64-box[2],64-box[3])>=3,name
 assert set(alpha.get_flattened_data())=={0,255},name
 rt=Image.open(root/'evidence'/(name+'.roundtrip.png')).convert('RGBA')
 assert im.tobytes()==rt.tobytes(),name
 items.append(dict(id=spec['id'],group=spec['group'],artId=name,status='ready' if name in reviewed else 'needs_review',png=str(png),aseprite=str(native),size=[64,64],alpha='binary transparent',bounds=list(box),opaquePixels=sum(a>0 for a in alpha.get_flattened_data()),sha256=hashlib.sha256(png.read_bytes()).hexdigest(),nativeSha256=hashlib.sha256(native.read_bytes()).hexdigest(),pixelSha256=hashlib.sha256(im.tobytes()).hexdigest(),nativeReopenPixelEqual=True,request=str(root/'raw'/(name+'.request.json')),receipt=str(root/'raw'/(name+'.receipt.json')),reference=str(root/'reference'/spec.get('reference','card_jigger.png'))))
assert len({x['pixelSha256'] for x in items})==len(items),'Duplicate image'
for ix,item in enumerate(items):
 item['contactPosition']={'row':ix//8+1,'column':ix%8+1}
 item['functionalMotif']=next(s['description'] for s in specs if s['file']==item['artId'])
 group='research' if item['group']=='research_tea' else item['group']
 row=labels[(group,item['id'])]
 item['displayName']=row[2] if item['group']=='research_tea' else row[1]
 item['gameEffect']=row[4] if group=='research' else row[3]
 item['handoffAuthority']=str(handoff)
 item['nativeFrameCount']=1
ready=sum(x['status']=='ready' for x in items)
manifest=json.dumps(dict(scope='ui-icons-2026-11-12',expectedLogicalIds=41,expectedFiles=48,readyFiles=ready,integrated='Unity owner pending',verified='runtime pending',items=items),ensure_ascii=False,indent=2)
(root/'manifest.json').write_text(manifest,encoding='utf-8')
(root/'evidence'/f'manifest-ready-{ready:02d}-exported-{len(items):02d}.json').write_text(manifest,encoding='utf-8')
print('ready',ready,'exported',len(items),'size/alpha/nonempty/margins/unique/native-reopen PASS')
