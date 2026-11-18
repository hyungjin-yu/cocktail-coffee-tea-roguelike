from pathlib import Path
import json,hashlib,shutil
r=Path('ArtSource/feedback-art-2026-11-16');pending=r/'raw/provisional-preserved';pending.mkdir(exist_ok=True)
ds=json.loads((r/'provenance/batch-native-receipts.json').read_text(encoding='utf-8'));changed=[]
for d in ds:
 p=Path(d['officialArchivePng']);dest=Path(d['rawPng'])
 if p.read_bytes()!=dest.read_bytes():
  shutil.copy2(dest,pending/dest.name);shutil.copy2(p,dest);changed.append(d['assetId'])
 d['sha256']=hashlib.sha256(dest.read_bytes()).hexdigest();d['status']='completed_batch_confirmed_final_archive_copy'
(r/'provenance/batch-native-receipts.json').write_text(json.dumps(ds,indent=2),encoding='utf-8')
(r/'provenance/provisional-refresh.json').write_text(json.dumps({'refreshedFromFinalOfficialArchives':changed,'earlierSnapshots':'raw/provisional-preserved','reason':'PixelLab updates archive PNG during streaming; final complete job archive is authoritative, interim snapshots never delivered ready.'},indent=2),encoding='utf-8')
print('Refreshed after batch completion:',changed)
