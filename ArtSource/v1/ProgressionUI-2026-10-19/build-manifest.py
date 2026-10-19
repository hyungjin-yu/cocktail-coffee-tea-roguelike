import pathlib,json,math,hashlib,shutil
from PIL import Image
R=pathlib.Path(__file__).resolve().parent
# Retain the separate generated closed design as source-only: final closed uses same-laptop geometry.
p=R/'assets/laptop_closed';q=R/'source-candidates/laptop_closed'
if p.exists():
 assert p.resolve().is_relative_to(R.resolve()) and q.resolve().is_relative_to(R.resolve())
 q.parent.mkdir(exist_ok=True)
 if not q.exists():shutil.move(str(p),str(q))
rects={'ingredient_card':[16,16,96,48],'receipt':[16,20,96,152],'spring_notebook':[20,44,152,96],'torn_recipe':[10,20,152,96],'laptop_open':[64,36,256,144],'tree_node':[16,22,32,26],'button':[24,10,48,12],'staff_card':[62,24,72,64]}
borders={'ingredient_card':[16,16,16,16],'receipt':[12,20,12,20],'spring_notebook':[20,20,20,44],'torn_recipe':[10,12,10,16],'tree_node':[24,24,24,24],'button':[24,10,24,10]}
assets=[]
for d in sorted((R/'assets').iterdir()):
 if not d.is_dir():continue
 aid=d.name;paths=sorted((d/'frames').glob('*.png')) if (d/'frames').exists() else sorted(d.glob('*.png'))
 if not paths:continue
 base=next((b for b in ['tree_node','staff_card','button'] if aid.startswith(b)),aid)
 im=Image.open(paths[0]);w,h=im.size
 a={'assetId':aid,'status':'ready','role':'motion_composite' if len(paths)>1 else 'ui_art','nativeSize':[w,h],'source':str((d/(aid+'.aseprite')).relative_to(R)).replace('\\','/'),'frames':[],'framePaths':[p.relative_to(R).as_posix() for p in paths],'contentRect':rects.get(base),'bordersLTRB':borders.get(base),'bordersUnityLBRTop':None,'textBaked':False,'integrationAllowed':False,'awaiting':'root art acceptance and manufacturing physics gate'}
 if base in borders:
  l,t,rr,b=borders[base];a['bordersUnityLBRTop']=[l,b,rr,t]
 a['scaling']='9slice; fixed corners, stretch centre' if base in borders else 'uniform nearest; do not independently stretch width/height'
 if base=='spring_notebook':a['scaling']='uniform nearest; preserve discrete spiral rings; borders describe paper safe insets only, do not stretch binding'
 if base=='staff_card':a.update(portraitRect=[18,27,28,34],scaling='uniform nearest; left portrait and right text are separate actual UI regions')
 if base=='tree_node':a['labelPlacement']='actual UI outside the icon contentRect; full effects/cost in details panel'
 if aid.startswith('laptop'):
  a['anchors']={'hinge':[192,204],'tableContact':[192,252],'screenRect':[56,28,268,160]};a['pivotPixelsTopLeft']=[192,204];a['pivotNormalizedUnity']=[192/w,1-204/h]
  a['scaling']='common pixels-per-unit and hinge pivot across clips; never stretch each canvas to same rect'
 else:a['pivotPixelsTopLeft']=[w/2,h/2];a['pivotNormalizedUnity']=[.5,.5]
 for i,p in enumerate(paths):
  a['frames'].append({'index':i,'path':p.relative_to(R).as_posix(),'durationMs':50 if len(paths)>1 else None,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
 if len(paths)>1:
  a['totalDurationMs']=50*len(paths);a['loop']=False;a['holdFinal']=True;a['previewGif']='preview/'+aid+'.gif'
 if aid=='recipe_tear_store':
  a.update(role='decorative_success_motion',transactionTrigger='play only after successful recipe save; never creates or consumes items',notebookOrigin=[8,40],boxOrigin=[336,108],permanentRecipeAsset='torn_recipe',paperSourceSize=[172,128],paperSourceContentRect=rects['torn_recipe'],paperTextOverlay='optional actual UI; apply each frame transform and front occlusion; no baked glyphs',paperTransforms=[])
  for i in range(10):
   t=i/9;z=1-.64*(t*t*(3-2*t));ww=math.floor(172*z+.5);hh=math.floor(128*z+.5)
   x=18+math.floor(352*(t*t*(3-2*t)));y=64-math.floor(math.sin(t*math.pi)*48)+math.floor(t*t*18)
   if i>=7:x=[370,374,376][i-7];y=[94,130,156][i-7]
   a['paperTransforms'].append({'index':i,'translationPixels':[x,y],'scaleXY':[ww/172,hh/128],'renderSize':[ww,hh],'sourceContentRect':rects['torn_recipe'],'contentRectOnFrame':[x+10*ww/172,y+20*hh/128,152*ww/172,96*hh/128],'preBoxClipRect':[0,0,512,154] if i>=7 else [0,0,512,224],'occlusionForeground':'recipe_storage at (336,108); front threshold y154; final paper fully occluded','textVisible':i<9})
 if aid=='torn_recipe':a.update(role='permanent_recipe_card',description='Stored recipe list/reuse/edit/share card; independent of decorative success motion')
 if aid=='recipe_storage':a.update(role='decorative_storage_box',anchors={'slot':[104,46]},contentRect=None)
 if base in ['button','tree_node','staff_card']:
  a['state']=aid[len(base)+1:] if aid!=base else 'neutral'
  a['stateGeometry']={'locked':'padlock','available':'four corner ticks','owned':'check','equipped':'inset second outline and check'}.get(a['state'],'unmarked')
 assets.append(a)
m={'specVersion':'1.1-2026-10-19','scope':'progression_ui_73','status':'ready_pending_root_review','ready':True,'integrated':False,'verified':False,'integrationAllowed':False,'coordinateOrigin':'top_left; rects half-open x,y,width,height','pivotNote':'pivotNormalizedUnity is bottom-left convention; pivotPixelsTopLeft/anchors are top-left','filter':'Point','pixelsPerUnit':128,'mipmap':False,'compression':'none','colorSpace':'sRGB RGBA','alpha':'binary 0/255; transparent RGB zero','text':'all glyphs and values actual UI; no text baked into sprites','prohibitedAnatomy':'no hand/arm/face/body in active art','referenceTransmitted':'references/card_gin.png RGBA, actual project original','toolAccess':'tool-access.json; server-version.json; provenance sanitized requests and receipts','productionTools':['PixelLab installed-extension protocol generate_image_pro_flash','Aseprite 1.3.18.6 batch Lua'],'motionSequence':{'opening':['laptop_arrive','laptop_opening','laptop_light'],'openingTotalMs':750,'closing':'laptop_closing','closingTotalMs':350,'recipeSave':'recipe_tear_store','recipeSaveMs':500,'skip':'click/Enter jumps to exact final art; show actual internal controls only after laptop_light final frame'},'minimumRuntimeUi':{'bodyTextAt720':18,'touchTargetPixels':44,'laptopCanvasAt720':[960,640],'laptopContentAt720':[640,360],'layout':'tabs/scroll/detail; do not shrink entire content','note':'art layout target only, Unity/UI runtime validation remains Claude responsibility'},'assets':assets,'sourceOnlyExcluded':['source-candidates/laptop_closed','provenance/laptop_closed.raw.png','aseprite-access-probe.aseprite'],'qa':'qa/verification.json'}
(R/'manifest.json').write_text(json.dumps(m,indent=2,ensure_ascii=False),encoding='utf8')
print('manifest',len(assets),'IDs',sum(len(a['frames']) for a in assets),'sprite PNGs')
