from pathlib import Path
import json,os,shutil,hashlib
from PIL import Image,ImageDraw
r=Path('ArtSource/feedback-art-2026-11-17');specs=json.loads((r/'provenance/recovery-specs29.json').read_text(encoding='utf-8'));hist=Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab/generation_history';found={s['id']:sorted(hist.glob('* - FB17R - '+s['id']+' - */0.png')) for s in specs};records=[]
for i,s in enumerate(specs):
 if not found[s['id']] or i+1==len(specs) or not found[specs[i+1]['id']]:continue
 f=found[s['id']][-1];dst=r/f'raw/{s["id"]}.png';shutil.copy2(f,dst);records.append({'id':s['id'],'archive':str(f),'png':str(dst),'sha256':hashlib.sha256(dst.read_bytes()).hexdigest(),'completionEvidence':'native queue advanced to next spec'})
(r/'provenance/collected-recovery-generations.json').write_text(json.dumps(records,indent=2),encoding='utf-8');old=json.loads((r/'provenance/collected-generations.json').read_text(encoding='utf-8'));old=[x for x in old if x['id'] not in {x['id'] for x in records}];(r/'provenance/collected-generations.json').write_text(json.dumps(old+records,indent=2),encoding='utf-8');sheet=Image.new('RGB',(7*170,4*170),'#302b34');d=ImageDraw.Draw(sheet)
for i,rec in enumerate(records[:28]):
 im=Image.open(rec['png']).convert('RGBA');big=im.resize((128,128),Image.Resampling.NEAREST);x=(i%7)*170;y=(i//7)*170;sheet.paste(big,(x+16,y),big);d.text((x+2,y+133),rec['id'].replace('dl_tin_','')[:23],fill='white')
sheet.save(r/'previews/tin-raw-contact.png');print('Recovery completed',len(records))
