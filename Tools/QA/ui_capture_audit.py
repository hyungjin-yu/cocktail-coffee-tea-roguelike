"""Map live UI override sprites to canonical PNGs and hash actual capture files.
Usage: ui_capture_audit.py project output run-out [run-out ...]
Only engine PNGs and recorded live sprite rows count as runtime evidence.
"""
import csv, hashlib, json, re, sys
from pathlib import Path
from PIL import Image

project, output, *runs = map(Path, sys.argv[1:])
output.mkdir(parents=True, exist_ok=True)
digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
captures, seen = [], {}
for run in runs:
    if not run.exists():
        continue
    shot_names = {}
    sprite_csv = run / 'shots-sprites.csv'
    if sprite_csv.exists():
        current = ''
        for line in sprite_csv.read_text(encoding='utf-8-sig').splitlines():
            if line.startswith('#shot '):
                _, index, current = line.split(' ', 2)
                shot_names[index] = current
            elif not line.startswith('frame,'):
                row = next(csv.reader([line]))
                if len(row) == 9:
                    seen.setdefault(row[2], []).append((str(run), current, row[3], row[1]))
    for p in sorted(run.glob('*.png')):
        with Image.open(p) as im:
            width, height = im.size
        captures.append(dict(run=str(run),screen=p.stem,width=width,height=height,
                             path=str(p),sha256=digest(p)))
    for p in sorted((run / 'video').glob('*/sprites.csv')):
        for row in csv.DictReader(p.open(encoding='utf-8-sig')):
            seen.setdefault(row['sprite'], []).append((str(run),p.parent.name+'/f'+row['frame'],row['object'],row['kind']))
    log = run / 'tour-log.txt'
    if log.exists():
        captures.append(dict(run=str(run),screen='completion-log',width='',height='',path=str(log),sha256=digest(log)))

mapping=[]
for folder in sorted((project/'ArtSource/ui-polish-2026-10-08').glob('ui_cafe_*')):
    manifest=folder/'manifest.json'
    data=json.loads(manifest.read_text(encoding='utf-8-sig'))
    for clip, contract in data['clips'].items():
        for index, name in enumerate(contract['files']):
            source=folder/name
            imported=project/'Assets/_Project/Art/v1'/folder.name/name
            matches=seen.get(source.stem, [])
            mapping.append(dict(assetID=data['assetId'],clip=clip,frame=index,source=str(source),
                sourceSha256=digest(source),imported=str(imported),
                importedSha256=digest(imported) if imported.exists() else '',
                bytesMatch=imported.exists() and source.read_bytes()==imported.read_bytes(),
                runtimeRows=len(matches),firstRuntime='/'.join(matches[0]) if matches else '',
                runs=';'.join(sorted(set(x[0] for x in matches)))))
for name,rows in [('capture-files.csv',captures),('ui-14-state-mapping.csv',mapping)]:
    with (output/name).open('w',newline='',encoding='utf-8-sig') as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]) if rows else ['empty']);w.writeheader();w.writerows(rows)
summary=dict(captures=len(captures),states=len(mapping),mappedStates=sum(bool(r['runtimeRows']) for r in mapping),
    allImportedBytesMatch=all(r['bytesMatch'] for r in mapping),
    runs=[dict(path=str(run),completion=(run/'tour-log.txt').exists() and
        bool(re.search(r'done shots=\d+ recordedFrames=\d+ errors=0 exceptions=0',(run/'tour-log.txt').read_text(encoding='utf-8-sig')))) for run in runs],
    captureCsvSha256=digest(output/'capture-files.csv'),mappingCsvSha256=digest(output/'ui-14-state-mapping.csv'))
(output/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
