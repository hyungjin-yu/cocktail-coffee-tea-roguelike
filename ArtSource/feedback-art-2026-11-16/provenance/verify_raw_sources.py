from pathlib import Path
import json,subprocess
from PIL import Image
r=Path('ArtSource/feedback-art-2026-11-16').resolve(); d=r/'verification/raw-roundtrip';d.mkdir(exist_ok=True);files=sorted((r/'raw').glob('*.png'));script=''
for p in files:
 script+='do local s=app.open('+json.dumps(str(p.with_suffix('.aseprite')).replace('\\','/'))+') s:saveCopyAs('+json.dumps(str(d/p.name).replace('\\','/'))+') s:close() end\n'
f=r/'verification/raw-roundtrip.lua';f.write_text(script,encoding='utf-8');res=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(f)],capture_output=True,text=True,timeout=50);checks=[]
for p in files:
 a=Image.open(p).convert('RGBA');b=Image.open(d/p.name).convert('RGBA');checks.append({'file':p.name,'pixelsEqual':a.size==b.size and a.tobytes()==b.tobytes()})
(r/'verification/raw-source-validation.json').write_text(json.dumps({'exitCode':res.returncode,'count':len(checks),'allPass':res.returncode==0 and all(x['pixelsEqual'] for x in checks),'files':checks},indent=2),encoding='utf-8');print('Raw native source roundtrips',sum(x['pixelsEqual'] for x in checks),'/',len(checks),'exit',res.returncode)
