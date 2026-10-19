import pathlib,json,hashlib
R=pathlib.Path(__file__).resolve().parent
baseline=json.loads((R/'source-comparison.json').read_text());unchanged=[]
for a in baseline:
 p=R.parent/'BeverageCraft-v1.1'/(a['id']+'.png');assert hashlib.sha256(p.read_bytes()).hexdigest()==a['sourceSHA256'];unchanged.append(a['id'])
files=[]
for p in sorted(R.rglob('*')):
 if p.is_file() and p.name!='delivery-files.json':files.append({'path':p.relative_to(R).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(R/'delivery-files.json').write_text(json.dumps({'unchangedOriginals':unchanged,'files':files},indent=2),encoding='utf8');print('inventory',len(files),'files; 6 originals unchanged')
