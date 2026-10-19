import pathlib,json,hashlib,os
R=pathlib.Path(__file__).resolve().parent
cfg=json.loads((pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab/package.json').read_text(encoding='utf8'))
needle=str(cfg.get('secret','')).encode();leaks=[]
if needle:
 for p in R.rglob('*'):
  if p.is_file() and needle in p.read_bytes():leaks.append(p.relative_to(R).as_posix())
assert not leaks,'Credential content found in owned output; do not publish'
for start in [R.parents[2],pathlib.Path.cwd()]:
 for p in [start,*start.parents]:
  a=p/'AGENTS.md'
  if a.exists():print('AGENTS found',str(a));print(a.read_text(encoding='utf8'))
items=[]
for p in sorted(R.rglob('*')):
 if p.is_file() and (p.suffix.lower() in ['.png','.aseprite','.gif'] or p.name in ['manifest.json','preview.html','server-version.json','tool-access.json']):
  items.append({'path':p.relative_to(R).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(R/'delivery-files.json').write_text(json.dumps({'files':items,'credentialContentScan':'no credential value present in owned outputs'},indent=2),encoding='utf8')
print('artifact inventory',len(items),'files; credential values absent')
