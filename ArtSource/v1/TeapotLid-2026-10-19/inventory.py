import pathlib,json,hashlib
R=pathlib.Path(__file__).resolve().parent
files=[{'path':p.relative_to(R).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(R.rglob('*')) if p.is_file() and p.name!='delivery-files.json']
(R/'delivery-files.json').write_text(json.dumps({'files':files},indent=2),encoding='utf8');print('inventory',len(files),'files')
