import pathlib,json,hashlib
from PIL import Image,ImageChops
source=pathlib.Path(__file__).resolve().parent
root=pathlib.Path('C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/card-education-2026-10-16')
assets=[]
for name,desc,cards in json.loads((source/'specs.json').read_text()):
 path=root/(name+'.png')
 if not path.exists(): continue
 im=Image.open(path); assert im.mode=='RGBA' and im.size==(64,64)
 bbox=im.getchannel('A').getbbox(); assert bbox and min(bbox[0],bbox[1],64-bbox[2],64-bbox[3])>=3
 assert set(im.getchannel('A').tobytes())<={0,255}
 rt=root/'roundtrip'/(name+'.png')
 assert rt.exists() and Image.open(rt).convert('RGBA').tobytes()==im.tobytes()
 assets.append(dict(assetId=name,cardIds=cards.split(','),png=str(path),aseprite=str(root/(name+'.aseprite')),size=[64,64],alphaBounds=list(bbox),minMargin=min(bbox[0],bbox[1],64-bbox[2],64-bbox[3]),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),checks=dict(rgba=True,binaryAlpha=True,margin3=True,nativeRoundtrip=True),sourceModel=json.loads((source/(name+'.request.json')).read_text())['model_name'],sourceReceipt=str(root/'provenance'/(name+'.receipt.json')),status='ready' if name in json.loads((source/'approved.json').read_text()) else 'needs_visual_review'))
(root/'manifest.json').write_text(json.dumps(dict(specVersion='64_2026-10-16',filterMode='Point',assets=assets),indent=2))
(root/'verification.json').write_text(json.dumps(dict(verified=len(assets),expected=16,checks='64x64 RGBA; binary alpha; >=3px margins; Aseprite native reopen identical pixels'),indent=2))
print('Verified',len(assets),'of 16')
