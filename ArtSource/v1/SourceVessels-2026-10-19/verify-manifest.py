import pathlib,json,hashlib,collections
from PIL import Image,ImageDraw
R=pathlib.Path(__file__).resolve().parent;P=R.parents[2]
keys=['gin','rum','blue_curacao','grenadine','lemon_juice','honey'];mouths=[10,15,13,9,12,22];labels=[[7,24,12,20],[4,26,11,17],[6,24,8,24],[6,25,10,20],[4,26,16,17],[6,28,14,10]]
checks=[];assets=[];sheet=Image.new('RGB',(6*180,520),(70,65,73));draw=ImageDraw.Draw(sheet)
def ck(n,v):checks.append({'name':n,'passed':bool(v)})
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for i,k in enumerate(keys):
 aid='dl_bottle_'+k;source=P/f'ArtSource/v1/BeverageCraft-v1.1/{aid}.png';raw=Image.open(source).convert('RGBA');fg=Image.open(R/f'{aid}_empty.png').convert('RGBA');mask=Image.open(R/f'{aid}_mask.png').convert('RGBA');vis=Image.open(R/f'{aid}_visible_mask.png').convert('RGBA');opened=Image.open(R/f'{aid}_open_empty.png').convert('RGBA');cap=Image.open(R/f'{aid}_cap.png').convert('RGBA')
 files={suf:f'{aid}_{suf}.png' for suf in ['empty','mask','visible_mask','open_empty','cap']};files['aseprite']=aid+'.aseprite'
 for typ,f in files.items():
  ck(aid+':'+typ+':exists',(R/f).exists())
  if typ not in ['empty','aseprite']:
   ck(aid+':'+typ+':nativeLayerRoundtrip',Image.open(R/f).convert('RGBA').tobytes()==Image.open(R/f'qa/{aid}_{typ}_roundtrip.png').convert('RGBA').tobytes())
  if typ!='aseprite':
   im=Image.open(R/f);pix=list(im.convert('RGBA').getdata());ck(aid+':'+typ+':RGBA_size_alpha',im.mode=='RGBA' and im.size==(24,56) and {v[3] for v in pix}=={0,255});ck(aid+':'+typ+':zeroTransparentRGB',all(p[:3]==(0,0,0) for p in pix if p[3]==0))
 ck(aid+':nativeRoundtrip',fg.tobytes()==Image.open(R/f'qa/{aid}_roundtrip.png').convert('RGBA').tobytes())
 ck(aid+':capOpenReconstruct',Image.alpha_composite(opened,cap).tobytes()==fg.tobytes())
 x,y,w,h=labels[i];ck(aid+':labelUnchanged',raw.crop((x,y,x+w,y+h)).tobytes()==fg.crop((x,y,x+w,y+h)).tobytes())
 ck(aid+':onlyInteriorCleared',all(fg.getpixel((x,y))==raw.getpixel((x,y)) for y in range(56) for x in range(24) if vis.getpixel((x,y))[3]==0))
 ck(aid+':visibleInteriorEmpty',all(fg.getpixel((x,y))[3]==0 for y in range(56) for x in range(24) if vis.getpixel((x,y))[3]>0))
 ck(aid+':maskWhiteInsideOriginal',all(mask.getpixel((x,y))==(255,255,255,255) and raw.getpixel((x,y))[3]==255 for y in range(56) for x in range(24) if mask.getpixel((x,y))[3]))
 ck(aid+':bottleNotEntirelyTransparent',sum(v[3]>0 for v in fg.getdata())>50)
 rows=[]
 for y in range(56):
  xs=[x for x in range(24) if mask.getpixel((x,y))[3]]
  if xs:rows.append({'y':y,'left':min(xs),'rightExclusive':max(xs)+1,'widthPixels':len(xs),'axisymmetricAreaProxy':len(xs)**2})
 total=sum(t['axisymmetricAreaProxy'] for t in rows);acc=0
 for row in reversed(rows):acc+=row['axisymmetricAreaProxy'];row['cumulativeCapacityFromBottomNormalized']=acc/total
 top=rows[0];mouth=[(top['left']+top['rightExclusive'])/2,mouths[i]];lip=[top['rightExclusive'],mouths[i]]
 ck(aid+':mouthTouchesMask',top['y']==mouths[i])
 coords={'mouth':mouth,'pourLip':lip,'pourLipRight':lip,'rotationAxis':[12,28],'grip':[12,28],'capTop':[(raw.getbbox()[0]+raw.getbbox()[2])/2,raw.getbbox()[1]],'mouthApertureEndpoints':[[top['left'],mouths[i]],[top['rightExclusive'],mouths[i]]]}
 for name,pt in [('mouth',mouth),('lip',lip),('pivot',[12,28])]:
  ck(aid+':'+name+':inCanvas',0<=pt[0]<=24 and 0<=pt[1]<=56)
 z=opened.resize((144,336),Image.Resampling.NEAREST);x0=i*180+18;y0=64;sheet.paste(z,(x0,y0),z);draw.text((i*180+2,8),k,fill='white')
 for name,pt,c in [('mouth',mouth,'cyan'),('right lip',lip,'red'),('pivot',[12,28],'yellow')]:
  px=x0+pt[0]*6;py=y0+pt[1]*6;draw.line((px-5,py,px+5,py),fill=c,width=1);draw.line((px,py-5,px,py+5),fill=c,width=1)
 draw.text((i*180+2,416),f'mouth {mouth}\nlip {lip}\npivot [12,28]',fill='white')
 a={'assetId':aid,'maskAssetId':aid+'_mask','status':'ready','size':[24,56],'files':files,'sha256':{typ:sha(R/f) for typ,f in files.items()},'sourcePath':str(source),'sourceSHA256':sha(source),'foregroundAlphaBounds':list(fg.getbbox()),'interiorMaskBBox':list(mask.getbbox()),'labelPreserveRect':labels[i],'capYRange':[raw.getbbox()[1],mouths[i]],'anchors':coords,'pivotNormalizedUnity':[.5,.5],'profile':{'origin':'top_left','rows':rows,'geometry':'2D silhouette-derived axisymmetric width-squared proxy; no measured 3D calibration','capacityMl':None,'physicalMaskIncludesUnderLabel':True,'visibleMaskForVolume':False,'runtimeInstruction':'cache profile; compute gravity-horizontal clipping after rotation and conserve actual volume; do not rotate a prefilled liquid texture'},'layers':'dynamic liquid clipped by physical mask, open_empty foreground, cap (hide while pouring)','integrated':False,'gameVerified':False}
 assets.append(a)
sheet.save(R/'qa/anchors-contact.png')
failed=[x['name'] for x in checks if not x['passed']]
report={'checkCount':len(checks),'passed':not failed,'failed':failed,'checks':checks,'visualPreviewCount':72,'previewFractions':[0,.25,.5,.75],'previewClockwiseAngles':[0,30,60],'previewModel':'art composite only; fraction of rotated 2D mask area, horizontal clipping with fractional boundary row; NOT Unity simulation or 3D fluid validation'}
(R/'qa/verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
m={'specVersion':'source-vessels-2026-10-19','scope':'six_existing_source_bottle_separations','status':'ready' if not failed else 'needs_fix','coordinateOrigin':'top_left pixels; bboxes left,top,right,bottom exclusive; rects x,y,width,height','pointFilter':True,'pixelsPerUnit':128,'mipmaps':False,'compression':'none','alpha':'binary RGBA for deliverables; preview boundary rows may use fractional alpha','assetCount':6,'runtimePngCount':30,'nativeCount':6,'assets':assets,'qa':report,'integrationAllowed':'after root review; runtime code and Assets owned by Claude','integrated':False,'verified':False,'tool':'Aseprite 1.3.18.6 existing PNG editing; no new image generation or style change','maskNames':'dl_bottle_{ingredient}_mask.png','labelOcclusion':'physical mask deliberately overlaps opaque label; render liquid behind foreground. visible_mask excludes label and is never a capacity/profile mask','capRule':'closed empty preserves cap; hide original cap and use open_empty before pouring; old cap-top source anchor is not the mouth','excluded':'all earlier ProgressionUI and BeverageCraft originals are read-only'}
(R/'manifest.json').write_text(json.dumps(m,indent=2),encoding='utf8');print(json.dumps({'checkCount':len(checks),'failed':failed,'assetCount':6,'pngCount':30},indent=2));raise SystemExit(bool(failed))
