import pathlib,json,hashlib
from PIL import Image
R=pathlib.Path(__file__).resolve().parent;S=R.parent/'BeverageCraft-v1.1'
raw=Image.open(S/'dl_teapot.png').convert('RGBA');mask=Image.open(S/'dl_teapot_mask.png').convert('RGBA');body=Image.open(R/'dl_teapot_open_body.png').convert('RGBA');lid=Image.open(R/'dl_teapot_lid.png').convert('RGBA');selection=Image.open(R/'qa/lid-selection.png').convert('RGBA');checks=[]
def ck(n,v):checks.append({'check':n,'passed':bool(v)})
ck('originalNativeMatchesPNG',raw.tobytes()==Image.open(R/'qa/original-native.png').convert('RGBA').tobytes())
ck('closedCompositeOriginalPixelExact',raw.tobytes()==Image.alpha_composite(body,lid).tobytes())
ck('closedPreviewOriginalPixelExact',raw.tobytes()==Image.open(R/'qa/closed-composite.png').convert('RGBA').tobytes())
ck('bodyOriginalPixelsOutsideLidUntouched',all(raw.getpixel((x,y))==body.getpixel((x,y)) for y in range(56) for x in range(64) if selection.getpixel((x,y))[3]==0))
ck('lidOriginalPixelsOriginalCoordinates',all(lid.getpixel((x,y))==raw.getpixel((x,y)) for y in range(56) for x in range(64) if lid.getpixel((x,y))[3]))
ck('noForegroundPhysicalMaskOverlap',all(body.getpixel((x,y))[3]==0 for y in range(56) for x in range(64) if mask.getpixel((x,y))[3]))
ck('mouthInsideDarkOpening',body.getpixel((32,20))[3]==255 and body.getpixel((32,20))==raw.getpixel((30,4)))
ck('lidSeatInCanvas',0<=32<64 and 0<=22<56)
palette=set(raw.getdata());assets=[]
for aid in ['dl_teapot_open_body','dl_teapot_lid']:
 p=R/(aid+'.png');im=Image.open(p).convert('RGBA');rt=Image.open(R/'qa'/(aid+'_roundtrip.png')).convert('RGBA');pm=Image.open(R/'qa'/(aid+'_mask_roundtrip.png')).convert('RGBA');pix=list(im.getdata())
 ck(aid+':RGBA',Image.open(p).mode=='RGBA');ck(aid+':sameCanvas',im.size==(64,56));ck(aid+':nonempty',im.getbbox() is not None);ck(aid+':binaryAlpha',{v[3] for v in pix}=={0,255});ck(aid+':transparentRGBzero',all(v[:3]==(0,0,0) for v in pix if v[3]==0));ck(aid+':sourcePalette',all(v in palette for v in pix));ck(aid+':nativePixelsMatch',im.tobytes()==rt.tobytes());ck(aid+':nativeMaskUnchanged',pm.tobytes()==mask.tobytes())
 native=R/(aid+'.aseprite');assets.append({'assetId':aid,'status':'ready','png':p.name,'aseprite':native.name,'size':[64,56],'alphaBounds':list(im.getbbox()),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'nativeSHA256':hashlib.sha256(native.read_bytes()).hexdigest(),'pivotNormalizedUnity':[.5,.5],'anchors':{'mouth':[32,20],'lidSeat':[32,22],'pourLip':[61,21],'grip':[12,29]},'integrated':False,'verified':False})
baseline=json.loads((R/'source-sha.json').read_text())
for f,h in baseline.items():ck('sourceUnchanged:'+f,hashlib.sha256((S/f).read_bytes()).hexdigest()==h)
qa={'passed':all(c['passed'] for c in checks),'checkCount':len(checks),'failed':[c['check'] for c in checks if not c['passed']],'checks':checks}
(R/'qa/verification.json').write_text(json.dumps(qa,indent=2),encoding='utf8')
m={'specVersion':'teapot-lid-2026-10-19','scope':'existing_teapot_body_lid_separation','status':'ready' if qa['passed'] else 'needs_fix','coordinateOrigin':'top_left pixel coordinates; alphaBounds left,top,right,bottom exclusive','assets':assets,'source':{'png':'../BeverageCraft-v1.1/dl_teapot.png','aseprite':'../BeverageCraft-v1.1/dl_teapot.aseprite','originalAnchors':{'mouth':[30,23],'pourLip':[61,21],'grip':[12,29]}},'physicalMask':{'path':'../BeverageCraft-v1.1/dl_teapot_mask.png','sha256':baseline['dl_teapot_mask.png'],'alphaBounds':list(mask.getbbox()),'unchanged':True},'opening':{'apertureRect':[24,18,16,5],'mouth':[32,20],'lidSeat':[32,22],'frontLipRow':22,'innerFace':'opaque original outline palette; physical liquid interior mask below remains unchanged'},'placement':{'bodyOriginForPreview':[32,40],'lidOriginClosed':[32,40],'lidOriginLifted':[32,20],'lidOriginOnSideTable':[112,68],'closedRelativeLidOffset':[0,0],'lidLocalSeat':[32,22],'note':'keep 64x56 canvas and common scale/pivot; translate lid independently; no angle frames needed; exact original closed pixels on overlay'},'renderOrder':['dynamic liquid through unchanged mask','dl_teapot_open_body','dl_teapot_lid when seated or moving'], 'filter':'Point','pixelsPerUnit':128,'mipmap':False,'compression':'none','hands':False,'integrated':False,'verified':False,'integrationAllowed':False,'integrationGate':'root review then Claude owns Assets/code/movement integration','qa':qa}
(R/'manifest.json').write_text(json.dumps(m,indent=2),encoding='utf8');print(json.dumps({'checkCount':len(checks),'failed':qa['failed']}));raise SystemExit(not qa['passed'])
