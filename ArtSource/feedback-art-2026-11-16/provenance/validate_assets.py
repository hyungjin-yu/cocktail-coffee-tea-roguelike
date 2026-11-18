from pathlib import Path
import json,subprocess,hashlib,struct,itertools
from PIL import Image
r=Path('ArtSource/feedback-art-2026-11-16').resolve();ready=r/'ready';verify=r/'verification';verify.mkdir(exist_ok=True);(verify/'roundtrip').mkdir(exist_ok=True)
origins=json.loads((r/'origin-inventory.json').read_text(encoding='utf-8'))['assets']
ids=['dl_coffee_latte_glass','dl_coffee_latte_glass_mask','dl_coffee_flatwhite_cup','dl_coffee_flatwhite_cup_mask','dl_coffee_cappuccino_cup','dl_coffee_cappuccino_cup_mask','dl_foam_layer','ui_lock_badge']+[x['assetId'] for x in origins]
missing=[x for x in ids if not (ready/(x+'.png')).exists() or not (ready/(x+'.aseprite')).exists()]
if missing: print('Missing final assets:',missing);raise SystemExit(2)
lua='local pivots={}\nlocal root='+json.dumps(str(r).replace('\\','/')+'/')+'\n'
for identifier in ids:
 lua+='do local s=app.open(root+"ready/'+identifier+'.aseprite") s:saveCopyAs(root+"verification/roundtrip/'+identifier+'.png") local p=s.slices[1].pivot pivots[#pivots+1]={assetId="'+identifier+'",x=p.x,y=p.y} s:close() end\n'
lua+='local f=assert(io.open(root+"verification/source-pivots.json","w")) f:write(json.encode(pivots)) f:close()\n'
lua=lua.replace('root+','root..');(verify/'roundtrip.lua').write_text(lua,encoding='utf-8')
res=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(verify/'roundtrip.lua')],capture_output=True,text=True,timeout=60)
(verify/'roundtrip-result.json').write_text(json.dumps({'exitCode':res.returncode,'knownExtensionWarning':"handle-pose.lua:58: attempt to index a nil value (local 'dlg')" if 'handle-pose.lua:58' in res.stdout else None}),encoding='utf-8')
assert res.returncode==0,res.stderr
checks=[]; images={};source_pivots={x['assetId']:[x['x'],x['y']] for x in json.loads((verify/'source-pivots.json').read_text(encoding='utf-8'))}
for identifier in ids:
 p=ready/(identifier+'.png');src=ready/(identifier+'.aseprite');im=Image.open(p).convert('RGBA');images[identifier]=im; a=im.getchannel('A');alphas=set(a.getdata());pixels=list(im.getdata()); b=src.read_bytes();expected=(36,36) if identifier.startswith('dl_coffee_latte_glass') else (32,26) if identifier.startswith('dl_coffee_') else (32,8) if identifier=='dl_foam_layer' else (16,16) if identifier=='ui_lock_badge' else (32,32)
 roundtrip=Image.open(verify/'roundtrip'/(identifier+'.png')).convert('RGBA')
 c={'assetId':identifier,'dimensions':list(im.size),'expectedDimensions':list(expected),'alphaBounds':a.getbbox(),'alphaValues':sorted(alphas),'nonempty':a.getbbox() is not None,'transparentCorners':all(im.getpixel(t)[3]==0 for t in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)]),'sizePass':im.size==expected,'nativeMagicPass':struct.unpack_from('<H',b,4)[0]==0xa5e0,'nativeFrames':struct.unpack_from('<H',b,6)[0],'nativeCanvasPass':tuple(struct.unpack_from('<HH',b,8))==expected,'roundtripPixelsEqual':im.size==roundtrip.size and im.tobytes()==roundtrip.tobytes(),'alphaPolicyPass':alphas<={0,96,255} if identifier=='dl_coffee_latte_glass' else alphas<={0,255},'nativePivotTopLeftPx':source_pivots[identifier],'nativePivotPass':source_pivots[identifier]==([im.width/2,im.height] if identifier.startswith('dl_coffee_') else [0,im.height] if identifier=='dl_foam_layer' else [im.width/2,im.height/2]),'pngSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'sourceSha256':hashlib.sha256(b).hexdigest(),'opaqueColors':len({x[:3] for x in pixels if x[3]==255})}
 if identifier.endswith('_mask'): c['opaqueWhitePass']=all(v[:3]==(255,255,255) for v in pixels if v[3])
 c['pass']=all(c[k] for k in ['nonempty','transparentCorners','sizePass','nativeMagicPass','nativeCanvasPass','roundtripPixelsEqual','alphaPolicyPass','nativePivotPass']) and c['nativeFrames']==1 and c.get('opaqueWhitePass',True)
 checks.append(c)
maskchecks=[]
for name in ['dl_coffee_latte_glass','dl_coffee_flatwhite_cup','dl_coffee_cappuccino_cup']:
 vessel=images[name];mask=images[name+'_mask'];outside=[]
 for y in range(mask.height):
  xs=[x for x in range(vessel.width) if vessel.getpixel((x,y))[3]>0]
  for x in range(mask.width):
   if mask.getpixel((x,y))[3] and (not xs or x<=min(xs) or x>=max(xs)): outside.append([x,y])
 maskchecks.append({'assetId':name,'sameCanvas':vessel.size==mask.size,'maskStrictlyWithinVesselRowEnvelope':not outside,'outsidePixelCount':len(outside),'liquidVisibleThroughArt':all(vessel.getpixel((x,y))[3]<=96 for y in range(mask.height) for x in range(mask.width) if mask.getpixel((x,y))[3])})
foam=images['dl_foam_layer'];foamcheck={'horizontalSeamEqual':all(foam.getpixel((0,y))==foam.getpixel((31,y)) for y in range(8)),'opaqueColorCount':len({p[:3] for p in foam.getdata() if p[3]}),'continuousFoamBand':all(foam.getpixel((x,y))[3]==255 for y in range(3,6) for x in range(32))}
orig_im={o['assetId']:images[o['assetId']] for o in origins};duplicate=[];near=[]
for (a,ia),(b,ib) in itertools.combinations(orig_im.items(),2):
 aa=list(ia.getchannel('A').getdata());bb=list(ib.getchannel('A').getdata());inter=sum(x>0 and y>0 for x,y in zip(aa,bb));union=sum(x>0 or y>0 for x,y in zip(aa,bb));iou=inter/union
 if aa==bb: duplicate.append([a,b])
 if iou>.85: near.append({'a':a,'b':b,'silhouetteIoU':round(iou,4)})
anchor_data=json.loads((ready/'anchors-handoff.json').read_text(encoding='utf-8'));anchorchecks=[]
for ent in anchor_data['entries']:
 anchorchecks.append({'assetId':ent['assetId'],'normalized':all(0<=v<=1 for k in ['mouth','grip','liquid'] for v in ent[k]),'liquidOrdered':ent['liquid'][0]<ent['liquid'][2] and ent['liquid'][1]<ent['liquid'][3],'pivotBottomCenter':ent['pivot']==[.5,0]})
result={'expectedAssetCount':36,'actualAssetCount':len(checks),'assets':checks,'maskChecks':maskchecks,'foamCheck':foamcheck,'originCount':len(origins),'exactSilhouetteDuplicates':duplicate,'highSilhouetteOverlapForVisualReview':near,'anchorChecks':anchorchecks,'structuralPass':all(x['pass'] for x in checks) and all(x['sameCanvas'] and x['maskStrictlyWithinVesselRowEnvelope'] and x['liquidVisibleThroughArt'] for x in maskchecks) and foamcheck['horizontalSeamEqual'] and foamcheck['opaqueColorCount']==3 and foamcheck['continuousFoamBand'] and not duplicate and len(anchorchecks)==3 and all(all(v for k,v in x.items() if k!='assetId') for x in anchorchecks),'visualReview':'pending','unityImportOrRuntimeVerification':'not run by art worker; Claude owns integration'}
(verify/'asset-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'structuralPass':result['structuralPass'],'assetCount':len(checks),'failures':[x['assetId'] for x in checks if not x['pass']],'maskChecks':maskchecks,'foam':foamcheck,'duplicates':duplicate,'highOverlapPairs':len(near)},indent=2))
