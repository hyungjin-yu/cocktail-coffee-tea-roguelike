from pathlib import Path
import shutil,json,hashlib,re
old=Path('ArtSource/cafe-title-2026-11-18');r=old/'revisions/v4-bar-end';r.mkdir(parents=True,exist_ok=True)
before={p.relative_to(old).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in old.rglob('*') if p.is_file() and 'revisions' not in p.relative_to(old).parts};(r/'previous-v3-before.json').write_text(json.dumps(before,indent=2),encoding='utf-8')
for sub in ['reference','raw','ready','provenance']:
 shutil.copytree(old/sub,r/sub,dirs_exist_ok=True)
for sub in ['previews','design','verification/roundtrip']:(r/sub).mkdir(parents=True,exist_ok=True)
shutil.copy2(old/'layout.json',r/'layout.json')
for p in (r/'provenance').glob('*'):
 if p.suffix in ['.py','.lua']:
  s=p.read_text(encoding='utf-8-sig').replace('ArtSource/cafe-title-2026-11-18/','ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end/').replace("Path('ArtSource/cafe-title-2026-11-18')","Path('ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end')");p.write_text(s,encoding='utf-8')
p=r/'provenance/author-scene.lua';s=p.read_text(encoding='utf-8');a=s.index(' -- Research table, chair');b=s.index(' -- Radio sits',a)
s=s[:a]+''' -- Same40cm laptop is now at SOUTH bar end; no separate research table/chair.
 box(5.15,1.34,.998,5.55,1.61,1.018,'slate',2,2)
 quad({{5.15,1.34,1.019},{5.55,1.34,1.019},{5.55,1.61,1.019},{5.15,1.61,1.019}},'keyboard',2,2)
 box(5.15,1.34,1.018,5.55,1.363,1.300,'slate',2,2)
 northface(1.367,5.165,5.535,1.040,1.285,'screen',2)
''' +s[b:]
s=s.replace('{{1.3,1.95,5},{1.95,2.2,1},{2.2,3.25,1},{3.25,4.8,1}}','{{1.3,1.90,2},{1.90,2.28,5},{2.28,3.25,1},{3.25,4.8,1}}').replace('{5.05,1.42,1.003},{5.45,1.42,1.003},{5.45,1.78,1.003},{5.05,1.78,1.003}','{5.05,1.92,1.003},{5.45,1.92,1.003},{5.45,2.24,1.003},{5.05,2.24,1.003}').replace('{1.4,2.6,2.6,3.25,.78},','')
s=s.replace("box(1.25,-.10,2.32,2.60,-.035,2.62,'trim',0,1)","box(1.25,-.10,2.32,2.60,-.035,2.62,'trim',0,1)\n -- Small physical entry instruction plate, separate from the game-name sign.\n box(.50,-.08,1.13,1.24,-.03,1.52,'trim',0,1)")
p.write_text(s,encoding='utf-8');f=r/'provenance/finish-scene.lua';t=f.read_text(encoding='utf-8');f.write_text(s.split('-- First native composition is a review draft')[0]+t[t.index('local delivered={}'):],encoding='utf-8')
print('v3 snapshot hashes',len(before),'newroot',r)
