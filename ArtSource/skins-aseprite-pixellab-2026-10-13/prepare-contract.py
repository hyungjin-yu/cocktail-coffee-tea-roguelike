"""Metadata only: carries the existing geometry/runtime contract, never edits pixels."""
import json
from pathlib import Path
root=Path(__file__).parent
old=root.parent/'skins-2026-10-13'
m=json.loads((old/'v2/asset_mapping_manifest.json').read_text(encoding='utf-8'))
rows=[]
for a in m['assets']:
    rel=Path(a['newFile']).relative_to(old)
    if rel.parts[0]=='v2': rel=Path(*rel.parts[1:])
    dest=root/rel
    dest.parent.mkdir(parents=True,exist_ok=True)
    rows.append(dict(original=a['original'],output=str(dest),mask=a['mask'],rim=a.get('rimMask'),kind=a['kind'],skinID=a['skinID'],recommendedResourcesPath=a['recommendedResourcesPath'],anchor=a['anchor'],head=a.get('protectedHeadRectTopLeft')))
(root/'geometry-contract.json').write_text(json.dumps(dict(assets=rows,clips=m['clips'],displayMetadata=m['displayMetadata']),ensure_ascii=False,indent=2),encoding='utf-8')
def q(v): return '[['+str(v).replace('\\','/')+']]'
lines=['return {']
for a in rows:
    fields=[k+'='+q(a[k]) for k in ['original','output','mask','kind','skinID']]
    if a['rim']: fields+=['rim='+q(a['rim'])]
    if a['head']: fields+=['head={'+','.join(map(str,a['head']))+'}']
    lines+=['{'+','.join(fields)+'},']
lines+=['}']
(root/'contract.lua').write_text('\n'.join(lines),encoding='utf-8')
print('43 geometry/runtime contracts copied; no candidate output pixels read')
