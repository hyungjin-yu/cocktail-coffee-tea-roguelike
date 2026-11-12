"""Preserve rejected attempts and regenerate with the same actual PixelLab service."""
import json,pathlib,shutil
from generate import run
root=pathlib.Path(__file__).resolve().parent
revisions=json.loads((root/'revisions.json').read_text(encoding='utf-8-sig'))
specs=json.loads((root/'specs.json').read_text(encoding='utf-8-sig'))
resolved_path=root/'evidence/revision-results.json'
resolved=json.loads(resolved_path.read_text()) if resolved_path.exists() else []
for revision in revisions:
 name=revision['file']
 if name in [x['file'] for x in resolved]: continue
 spec=next(s for s in specs if s['file']==name)
 archive=root/'rejected'/name/'attempt-01';archive.mkdir(parents=True,exist_ok=True)
 for folder,suffix in [('raw','.png'),('raw','.request.json'),('raw','.receipt.json'),('','.png'),('','.aseprite'),('evidence','.roundtrip.png')]:
  src=root/folder/(name+suffix)
  if src.exists():
   dst=archive/((folder+'-') if folder else 'final-')/(name+suffix)
   dst.parent.mkdir(parents=True,exist_ok=True)
   assert not dst.exists(),str(dst)
   shutil.move(str(src),str(dst))
 spec['description']=revision['description']
 if revision.get('reference'): spec['reference']=revision['reference']
 (root/'specs.json').write_text(json.dumps(specs,ensure_ascii=False,indent=2),encoding='utf-8')
 run(spec)
 resolved.append(dict(file=name,reason=revision['reason'],previousAttempt=str(archive),newOutput=str(root/'raw'/(name+'.png')),status='generated-needs-visual-review'))
 resolved_path.write_text(json.dumps(resolved,indent=2),encoding='utf-8')
