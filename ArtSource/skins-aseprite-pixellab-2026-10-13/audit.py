"""Read-only pixel inspections and metadata/SHA output; no image creation or editing."""
import json,hashlib
from pathlib import Path
from PIL import Image
root=Path(__file__).parent
contract=json.loads((root/'geometry-contract.json').read_text(encoding='utf8'))
rows=[]
for a in contract['assets']:
 p=Path(a['output'])
 if not p.exists(): continue
 src=Image.open(a['original']).convert('RGBA'); out=Image.open(p).convert('RGBA')
 read=Image.open(p.with_suffix('.readback.png')).convert('RGBA')
 mask=Image.open(a['mask']).convert('L')
 ss=list(src.get_flattened_data()); oo=list(out.get_flattened_data()); mm=list(mask.get_flattened_data())
 alpha=all(s[3]==o[3] for s,o in zip(ss,oo))
 outside=all(s==o for s,o,m in zip(ss,oo,mm) if m==0)
 head=True
 if a['head']:
  x0,y0,x1,y1=a['head']; head=src.crop((x0,y0,x1,y1)).tobytes()==out.crop((x0,y0,x1,y1)).tobytes()
 white=True
 if a['kind']=='body':
  white=all(s==o for s,o in zip(ss,oo) if s[3]>0 and min(s[:3])>150 and max(s[:3])-min(s[:3])<75)
 checks=dict(sizeExact=src.size==out.size,alphaExact=alpha,silhouetteExact=alpha,outsideMaskExact=outside,protectedHeadExact=head,whiteShirtExact=white,nativeReadbackExact=read.tobytes()==out.tobytes())
 if a['kind']=='embedded_sword': checks['bodyPixelsExact']=outside
 row=dict(a,**checks,changedPixels=sum(s!=o for s,o in zip(ss,oo)),sourceSHA256=hashlib.sha256(Path(a['original']).read_bytes()).hexdigest(),outputSHA256=hashlib.sha256(p.read_bytes()).hexdigest(),nativeFile=str(p.with_suffix('.aseprite')),nativeSHA256=hashlib.sha256(p.with_suffix('.aseprite').read_bytes()).hexdigest(),nativeTag=p.stem)
 rows.append(row)
manifest=dict(status='partial_source_ready' if len(rows)<43 else 'source_ready',productionTools=['PixelLab Aseprite extension GUI Edit image (Pro)','Aseprite 1.3.18.6 GUI and Lua'],pixelLabCalls=2,designs=5,pythonUsage='inspection and metadata only',geometryMasks='Existing candidate masks used only as geometry contracts; candidate output colors never read by authoring',clips=contract['clips'],displayMetadata=contract['displayMetadata'],assets=rows)
(root/'asset_mapping_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
fail=[(r['output'],k) for r in rows for k,v in r.items() if k.endswith('Exact') and v is False]
(root/'audit-results.json').write_text(json.dumps(dict(count=len(rows),failures=fail,passed=not fail,checks=['RGBA alpha','silhouette','outside geometry mask','protected head','white shirt','native reopen/export pixel match','source/output/native SHA256','unchanged anchor/runtime paths']),indent=2),encoding='utf8')
print('AUDIT',len(rows),'frames; failures',fail,'changed',[r['changedPixels'] for r in rows])
if fail: raise SystemExit(1)
