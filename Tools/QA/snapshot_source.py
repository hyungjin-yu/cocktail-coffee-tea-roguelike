"""Record exact build inputs and separate production runtime from conditional QA code."""
import sys, csv, hashlib, json
from pathlib import Path
root, out = map(Path, sys.argv[1:3])
out.mkdir(parents=True, exist_ok=True)
qa = {'AutoTour.cs', 'LoadScenario.cs', 'PlayVerifier.cs'}
rows = []
for folder in ['Assets', 'Packages', 'ProjectSettings']:
    for p in (root / folder).rglob('*'):
        if not p.is_file(): continue
        rel = p.relative_to(root).as_posix()
        category = 'input'
        if '/Scripts/' in rel and p.suffix == '.cs':
            category = 'editor' if '/Editor/' in rel else ('qa' if p.name in qa else 'runtime')
        elif '/Tests/' in rel: category = 'tests'
        elif '/Art/' in rel or '/ArtV1/' in rel: category = 'art'
        elif '/Audio/' in rel: category = 'audio'
        rows.append(dict(path=rel, sha256=hashlib.sha256(p.read_bytes()).hexdigest(), category=category, bytes=p.stat().st_size))
rows.sort(key=lambda r:r['path'])
with (out/'source-files.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.DictWriter(f,fieldnames=['path','sha256','category','bytes']);w.writeheader();w.writerows(rows)
summary={}
for cat in ['all','runtime','editor','qa','tests','art','audio','input']:
    selected = rows if cat == 'all' else [r for r in rows if r['category']==cat]
    digest=hashlib.sha256(''.join(r['path']+'\0'+r['sha256']+'\n' for r in selected).encode()).hexdigest()
    summary[cat]=dict(files=len(selected),sha256=digest)
(out/'source-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
