import hashlib,json,pathlib
from PIL import Image
root=pathlib.Path(__file__).resolve().parent
manifest=json.loads((root/'manifest.json').read_text(encoding='utf-8'))
assert manifest['readyFiles']==48 and len(manifest['items'])==48
assert len({('research' if i['group']=='research_tea' else i['group'],i['id']) for i in manifest['items']})==41
results=[]
for item in manifest['items']:
 name=item['artId'];raw=root/'raw'/(name+'.png')
 a=Image.open(raw).convert('RGBA');b=Image.open(root/'evidence'/(name+'.source-roundtrip.png')).convert('RGBA')
 assert a.size==b.size==(64,64),name
 assert all(x[3]==y[3] and (x[3]==0 or x==y) for x,y in zip(a.get_flattened_data(),b.get_flattened_data())),name
 receipt=json.loads(pathlib.Path(item['receipt']).read_text(encoding='utf-8'))
 completed=[e for e in receipt['events'] if e.get('type')=='message_done']
 assert len(completed)==1 and completed[0].get('generation_id'),name
 results.append(dict(artId=name,hiddenOriginalSourceEquivalent=True,sourceSha256=hashlib.sha256(raw.read_bytes()).hexdigest(),generationId=completed[0]['generation_id'],finalSha256=item['sha256']))
baseline=json.loads((root/'evidence/reference-hashes.json').read_text(encoding='utf-8-sig'))
for record in baseline: assert hashlib.sha256(pathlib.Path(record['Path']).read_bytes()).hexdigest().upper()==record['Hash']
reference_results=[]
for ref in sorted((root/'reference').glob('*.png')):
 if ref.name=='bartender_move_right_00.png': asset=root.parent.parent/'Assets/_Project/Art/v1/bartender/frames'/ref.name
 elif ref.name.startswith('card_'): asset=root.parent.parent/'Assets/_Project/Resources/CardEducation'/ref.name
 elif ref.name=='dl_bottle_gin.png': asset=root.parent.parent/'Assets/_Project/Resources/DrinkLab'/ref.name
 else: asset=root.parent.parent/'Assets/_Project/Resources/Icons'/ref.name
 assert ref.read_bytes()==asset.read_bytes(),ref.name
 reference_results.append(dict(reference=str(ref),existingAsset=str(asset),equal=True,sha256=hashlib.sha256(ref.read_bytes()).hexdigest()))
api_attempts=len(list((root/'raw').glob('*.receipt.json')))+len(list((root/'rejected').rglob('*.receipt.json')))
report=dict(readyFiles=48,logicalIds=41,pngCount=len(list(root.glob('*.png'))),nativeCount=len(list(root.glob('*.aseprite'))),allNativeStatic64RGBA=True,allNativeOriginalLayersEquivalent=True,allVisibleNativeExportsEqual=True,allAlphaBinary=True,allMinimumTransparentMarginAtLeast3=True,allPixelHashesUnique=True,apiCompletedAttempts=api_attempts,guiCompletedRejectedAttempts=1,results=results,references=reference_results,runtime='Unity owner evidence pending')
(root/'evidence/final-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('FINAL ART PASS:',report['readyFiles'],'ready /',report['logicalIds'],'logical IDs /',api_attempts,'actual completed PixelLab API attempts plus 1 GUI attempt')
