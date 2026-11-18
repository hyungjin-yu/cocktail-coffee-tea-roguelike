from pathlib import Path
import json,os,shutil,hashlib
from PIL import Image,ImageDraw
r=Path('ArtSource/cafe-title-2026-11-18');ss=json.loads((r/'provenance/native-source-specs.json').read_text(encoding='utf-8'));hist=Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab/generation_history';out=[];sheet=Image.new('RGB',(1000,600),'#302b34');dr=ImageDraw.Draw(sheet)
for i,s in enumerate(ss[:-1]):
 folders=sorted(hist.glob('* - CT18 - '+s['id']+' - *'));nexts=sorted(hist.glob('* - CT18 - '+ss[i+1]['id']+' - *'))
 if not folders or not nexts:continue
 folder=folders[-1];candidates=sorted(folder.glob('*.png'));target=r/'raw/candidates'/s['id'];target.mkdir(parents=True,exist_ok=True)
 for p in candidates:shutil.copy2(p,target/p.name)
 chosen=candidates[-1];dest=r/'raw'/f'{s["id"]}.png';shutil.copy2(chosen,dest);im=Image.open(dest).convert('RGBA');big=im.copy();big.thumbnail((180,240),Image.Resampling.NEAREST);x=(i%5)*200;sheet.paste(big,(x+10,30),big);dr.text((x+3,7),s['id'],fill='white');out.append({'id':s['id'],'archive':str(folder),'candidateFiles':[p.name for p in candidates],'selected':chosen.name,'png':str(dest),'sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'completion':'native sequential queue advanced to next job'})
(r/'provenance/source-generation-receipts.json').write_text(json.dumps(out,indent=2),encoding='utf-8');sheet.save(r/'previews/source-contact.png');print('Actual source requests completed',len(out),'facade request currently native Generating7% visible; permission prompt gone')
