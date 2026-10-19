import pathlib,json,hashlib,os
R=pathlib.Path(__file__).resolve().parent
cfg=json.loads((pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab/package.json').read_text(encoding='utf8'));needle=str(cfg.get('secret','')).encode()
files=[]
for p in sorted(R.rglob('*')):
 if p.is_file() and p.name!='delivery-files.json':
  data=p.read_bytes();assert not needle or needle not in data,'Credential value found; do not publish'
  files.append({'path':p.relative_to(R).as_posix(),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
(R/'delivery-files.json').write_text(json.dumps({'files':files,'credentialValuesInArtifacts':False},indent=2),encoding='utf8');print('artifact inventory',len(files),'files; no credential value in saved output')
