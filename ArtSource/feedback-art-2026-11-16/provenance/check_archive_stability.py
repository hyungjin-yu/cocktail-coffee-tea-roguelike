import pathlib,os,hashlib,json
r=pathlib.Path('ArtSource/feedback-art-2026-11-16'); ds=json.loads((r/'provenance/batch-native-receipts.json').read_text());changed=[]
for d in ds:
 p=pathlib.Path(d['officialArchivePng']);dest=pathlib.Path(d['rawPng']);
 if p.read_bytes()!=dest.read_bytes():changed.append(d['assetId'])
print('Changed official archive after early collection:',changed)
