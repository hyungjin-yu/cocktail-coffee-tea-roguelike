import json,hashlib,pathlib
r=pathlib.Path('ArtSource/feedback-art-2026-11-16');d=json.loads((r/'manifest.ready.json').read_text(encoding='utf-8'));assert d['ready'] and d['assetCount']==36
for x in d['assets']:
 for k in ['png','aseprite']:
  p=r/x[k];assert p.exists() and hashlib.sha256(p.read_bytes()).hexdigest()==x['sha256'][k],str(p)
 for k in ['sourceRawPng','sourceRawAseprite']:assert (r/x[k]).exists(),x[k]
 p=r/'ready'/(x['assetId']+'.ready.json');one=json.loads(p.read_text(encoding='utf-8'));assert one['ready'] and one['assetId']==x['assetId']
assert all((r/p).exists() for p in d['previews'])
print('FINAL HANDOFF VALID: 36 individual manifests, 72 final file hashes, all raw sources and 4 preview paths exist.')
