from pathlib import Path
import os,json,hashlib,shutil
from PIL import Image,ImageDraw
r=Path('ArtSource/feedback-art-2026-11-17'); specs=json.loads((r/'provenance/generation-specs.json').read_text(encoding='utf-8')); hist=Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab/generation_history'
found={s['id']:sorted(hist.glob('* - FB17 - '+s['id']+' - */0.png')) for s in specs}
records=[]
for i,s in enumerate(specs):
 fs=found[s['id']]
 if not fs:continue
 if i!=0 and (i+1>=len(specs) or not found[specs[i+1]['id']]):continue
 f=fs[-1];dst=r/'raw'/f"{s['id']}.png";shutil.copy2(f,dst)
 records.append({'id':s['id'],'archive':str(f),'png':str(dst),'sha256':hashlib.sha256(dst.read_bytes()).hexdigest(),'completionEvidence':'probe final native alert' if i==0 else 'native sequential queue advanced to next spec'})
(r/'provenance/collected-generations.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
can=Image.new('RGB',(6*180,3*190),'#302b34');d=ImageDraw.Draw(can)
for i,rec in enumerate(records):
 if i>=18:break
 im=Image.open(rec['png']).convert('RGBA');im=im.resize((128,128),Image.Resampling.NEAREST);x=(i%6)*180;y=(i//6)*190;can.paste(im,(x+20,y),im);d.text((x+3,y+134),rec['id'].replace('hidden_','').replace('vfx_arrow_',''),fill='white')
can.save(r/'previews/raw-contact-early.png');print('Completed collected',len(records),[x['id'] for x in records])
