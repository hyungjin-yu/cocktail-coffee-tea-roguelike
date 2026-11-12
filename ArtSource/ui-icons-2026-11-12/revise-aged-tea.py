import json,pathlib,shutil
from generate import run
root=pathlib.Path(__file__).resolve().parent
specs=json.loads((root/'specs.json').read_text())
name='research_full_body_roast_tea'
spec=next(s for s in specs if s['file']==name)
archive=root/'rejected'/name/'attempt-02';archive.mkdir(parents=True,exist_ok=True)
for folder,suffix in [('raw','.png'),('raw','.request.json'),('raw','.receipt.json'),('','.png'),('','.aseprite'),('evidence','.roundtrip.png')]:
 src=root/folder/(name+suffix)
 if src.exists():
  dst=archive/(folder or 'final')/(name+suffix);dst.parent.mkdir(parents=True,exist_ok=True)
  assert not dst.exists();shutil.move(str(src),str(dst))
spec['reference']='card_oolong.png'
spec['description']='An open rounded dark brown clay TEA STORAGE JAR with its lid leaning separately beside it, filled to the mouth with clearly recognizable long curled dark olive and brown dried tea leaves. Several large individual VEINED TEA LEAVES lie beside the jar. A tiny hourglass on the other side signifies deep aging and patient storage. Main silhouette is a broad open clay storage jar full of dry tea leaves, not a teapot, no spout, no coffee beans, no cup, no food, no chocolate, no rectangular brick, no writing or label.'
(root/'specs.json').write_text(json.dumps(specs,ensure_ascii=False,indent=2),encoding='utf-8')
run(spec)
(root/'evidence/aged-tea-second-revision.json').write_text(json.dumps(dict(file=name,reason='Compressed brick can resemble chocolate; replaced by dry-leaf storage and time motif with visible veined tea leaves.',previousAttempt=str(archive),status='generated-needs-review'),indent=2))
