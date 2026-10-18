"""Read-only PNG QA, native roundtrip comparison and truthful handoff manifest."""
import hashlib, json, pathlib, subprocess
from PIL import Image
import produce
ROOT=pathlib.Path(__file__).resolve().parent
ASE='C:/Program Files/Aseprite/Aseprite.exe'
sizes={s[0]:[s[1],s[2]] for s in produce.SPECS}
sizes.pop('dl_hand',None)
sizes.update(dl_shaker_open=[48,80],dl_shaker_shake=[64,80],dl_drink_reveal=[64,64],dl_steam=[16,24],dl_pour_stream=[12,48],dl_bubbles=[16,24],dl_ice_cube=[12,12],dl_buff_gain=[32,32])
for k in ['black_tea','green_tea']:
 sizes['dl_leaf_'+k]=[8,8];sizes['dl_leaf_heap_'+k]=[32,16]
for k in ['gin','rum','blue_curacao','grenadine','lemon_juice','honey','black_tea','green_tea']: sizes['dl_icon_'+k]=[32,32]
fps={'dl_shaker_shake':12,'dl_drink_reveal':10,'dl_barspoon':10,'dl_steam':8,'dl_pour_stream':12,'dl_bubbles':10,'dl_ice_cube':6,'dl_buff_gain':12}
rounddir=ROOT/'qa/native-roundtrip';rounddir.mkdir(parents=True,exist_ok=True)
lua=['local root=app.params.root']
for id in sizes:
 if not (ROOT/(id+'.aseprite')).exists():continue
 lua.append(f'do local s=app.open(root.."/{id}.aseprite"); for i=1,#s.frames do local im=Image(s.width,s.height,ColorMode.RGB); im:drawSprite(s,i); im:saveAs(root.."/qa/native-roundtrip/{id}_"..string.format("%02d",i-1)..".png") end;s:close() end')
(ROOT/'qa/roundtrip.lua').write_text('\n'.join(lua))
subprocess.run([ASE,'--batch','--script-param','root='+ROOT.as_posix(),'--script',str(ROOT/'qa/roundtrip.lua')],check=True,capture_output=True)
visual=json.loads((ROOT/'qa/visual-review.json').read_text()) if (ROOT/'qa/visual-review.json').exists() else {}
assets=[]
for id,size in sizes.items():
 files=sorted(ROOT.glob(id+'_[0-9][0-9].png')) or ([ROOT/(id+'.png')] if (ROOT/(id+'.png')).exists() else [])
 if not files:
  assets.append(dict(assetId=id,size=size,status='planned',checks={'exists':False},reason='PixelLab generation or assembly pending'));continue
 checks=[];frames=[]
 for n,p in enumerate(files):
  im=Image.open(p);rgba=im.convert('RGBA');alpha=rgba.getchannel('A');bounds=alpha.getbbox();hist=alpha.histogram()
  rt=rounddir/f'{id}_{n:02}.png'
  same=rt.exists() and Image.open(rt).convert('RGBA').tobytes()==rgba.tobytes()
  c=dict(rgba=im.mode=='RGBA',binaryAlpha=sum(hist[1:255])==0,correctSize=list(im.size)==size,nonempty=bounds is not None,nativeRoundtrip=same)
  checks.append(c);frames.append(dict(png=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),alphaBounds=bounds,checks=c))
 merged={k:all(c[k] for c in checks) for k in checks[0]}
 mask=ROOT/(id+'_mask.png');anchors={};mask_info=None
 if mask.exists():
  m=Image.open(mask).convert('RGBA');mb=m.getchannel('A').getbbox();invalid=sum(1 for r,g,b,a in m.getdata() if a not in [0,255] or (a and (r,g,b)!=(255,255,255)))
  holes=sum(1 for i,(p1,p2) in enumerate(zip(m.getdata(),Image.open(files[0]).convert('RGBA').getdata())) if p1[3] and p2[3])
  merged['maskValid']=list(m.size)==size and mb is not None and invalid==0 and holes==0
  mask_info=dict(png=str(mask),sha256=hashlib.sha256(mask.read_bytes()).hexdigest(),alphaBounds=mb,emptyRow=mb[3]-1,fullRow=mb[1],maskConvention='white RGB, binary alpha; liquid permitted where alpha=255; reveal from bottom upward')
  anchors={'mouth':[int((mb[0]+mb[2]-1)/2),mb[1]],'pourLip':[mb[2]-1,mb[1]],'grip':[size[0]//2,int(size[1]*.62)]}
 elif id.startswith('dl_bottle') or id=='dl_water_kettle':
  bb=frames[0]['alphaBounds'];anchors={'pourLip':[int((bb[0]+bb[2]-1)/2),bb[1]],'mouth':[int((bb[0]+bb[2]-1)/2),bb[1]],'grip':[size[0]//2,int((bb[1]+bb[3])* .5)]}
 if id=='dl_teapot':anchors['pourLip']=[61,21];anchors['grip']=[12,29]
 if id=='dl_water_kettle':anchors['pourLip']=[45,14];anchors['grip']=[8,27]
 if id=='dl_chaha':anchors['pourLip']=[42,8];anchors['grip']=[15,15]
 if id=='dl_hand':anchors={'grip':[10,10],'cuff':[24,25]}
 if id in ['dl_pour_stream','dl_steam']:anchors['origin']=[size[0]//2,0 if id=='dl_pour_stream' else size[1]-1]
 if id=='dl_barspoon':anchors['grip']=[size[0]//2,16]
 source=id.replace('dl_shaker_open','dl_shaker')
 if id=='dl_shaker_shake':source='dl_shaker'
 if id=='dl_drink_reveal':source='dl_glass_cocktail'
 if id.startswith('dl_icon_'):
  k=id[len('dl_icon_'):];source=('dl_tin_' if k in ['black_tea','green_tea'] else 'dl_bottle_')+k
 receipt=ROOT/'provenance'/f'{source}.receipt.json'
 reviewed=visual.get(id,{});merged['visual']=reviewed.get('pass',False)
 ready=all(merged.values())
 a=dict(assetId=id,png=str(files[0]),aseprite=str(ROOT/(id+'.aseprite')),size=size,alphaBounds=frames[0]['alphaBounds'],sha256=frames[0]['sha256'],checks=merged,status='ready' if ready else 'needs_fix',integrationAllowed=ready,sourceModel='generate_image_pro_flash + Aseprite' if receipt.exists() else 'Aseprite Lua pixel art (04 project palette)',sourceReceipt=str(receipt) if receipt.exists() else None,authoringScript=str(ROOT/'finish.lua'),pixelsPerUnit=128,pivotNormalized=[.5,.5],anchors=anchors,frames=frames,fps=fps.get(id,8),loop=id not in ['dl_drink_reveal','dl_buff_gain'],visualNotes=reviewed.get('notes','Visual review pending'),integrated=False,unityVerified=False)
 if mask_info:a['mask']=mask_info
 if id=='dl_drink_reveal':
  a['animatedMasks']=[str(ROOT/f'dl_drink_reveal_mask_{i:02}.png') for i in range(6)]
  maskchecks=[]
  for i,path in enumerate(a['animatedMasks']):
   mm=Image.open(path).convert('RGBA');ff=Image.open(files[i]).convert('RGBA')
   maskchecks.append(list(mm.size)==size and mm.getbbox() is not None and all(v[3] in [0,255] and (not v[3] or v[:3]==(255,255,255)) for v in mm.getdata()) and not any(m[3] and f[3] for m,f in zip(mm.getdata(),ff.getdata())))
  a['checks']['animatedMasks']=all(maskchecks)
  if not all(maskchecks):a['status']='needs_fix'
  a['contentsTransformByFrame']=[dict(center=[32,y],angleRadians=0,scale=z,sourceMask='dl_glass_cocktail_mask.png') for y,z in zip([37,34,32,31,32,32],[.72,.78,.83,.86,.84,.84])]
  a['animationEvents']={'revealStart':0,'settledFinishedDrink':5,'buffGainAfter':5}
 if id=='dl_shaker_shake':a['animationEvents']={'shakeLoopStart':0,'shakeLoopEnd':5}
 a['framePaths']=[f['png'] for f in frames]
 assets.append(a)
result=dict(specVersion='69_2026-10-19',filterMode='Point',colorMode='RGBA',binaryAlpha=True,pixelsPerUnit=128,pivotConvention='Unity normalized from lower left; default canvas center; anchors are integer pixels from top left',assets=assets,readyCount=sum(a['status']=='ready' for a in assets),needsFixCount=sum(a['status']=='needs_fix' for a in assets),plannedCount=sum(a['status']=='planned' for a in assets),quantityContract='Integer tenths drive masks and transforms continuously; never one frame per tenth. White masks are clipped by continuous liquid height and tinted dynamically. Tint stream/effects using computed mixture colour; show leaf particle pile amount with masks/transforms.',ownership='Art originals only; Claude owns import/integration/runtime verification',ingredientAppearance=dict(gin={'swatch':'#DFE8EB','form':'colourless clear liquid'},rum={'swatch':'#D5A45F','form':'pale amber clear liquid'},blue_curacao={'swatch':'#248EDB','form':'saturated blue clear liquid'},grenadine={'swatch':'#BA284B','form':'strong ruby syrup'},black_tea={'swatch':'#714732','brewed':'#BD7847','form':'curled dry brown leaves'},green_tea={'swatch':'#466B45','brewed':'#B0B65C','form':'pointed dry green leaves'},lemon_juice={'swatch':'#EAD889','form':'cloudy pale yellow liquid'},honey={'swatch':'#D6A23C','form':'viscous golden liquid'}))
result['userRevision']='2026-10-19: all hands, faces and sip gestures removed; tool-only shaking and finished-drink reveal replaces sipping.'
result['excludedAssets']=[dict(assetId=id,status='superseded',integrationAllowed=False,archivePath=str(ROOT/'archive/superseded-hand-motion'),reason='Latest user explicitly removed all hand/face/sip presentation') for id in ['dl_hand','dl_drink_sip','dl_shaker_shake_hands_v1']]
(ROOT/'manifest.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps({k:result[k] for k in ['readyCount','needsFixCount','plannedCount']}))
print('Failed checks:',[(a['assetId'],[k for k,v in a['checks'].items() if not v]) for a in assets if a['status']=='needs_fix'])
