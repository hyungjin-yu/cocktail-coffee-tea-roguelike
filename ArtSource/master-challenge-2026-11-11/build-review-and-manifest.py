from pathlib import Path
from PIL import Image,ImageDraw
import csv,json,hashlib,math
p=Path(__file__).resolve().parent
rows=list(csv.DictReader((p/'native-frames.csv').open()));names=list(dict.fromkeys(x['clip'] for x in rows));clips=[];checks=[]
for name in names:
 rs=[x for x in rows if x['clip']==name];files=[p/'frames'/f'{name}_{int(x["index"]):02}.png' for x in rs];ims=[Image.open(f).convert('RGBA') for f in files];w,h=ims[0].size
 dw,dh=(w,h) if w<1000 else (640,96);cols=min(8,len(ims));board=Image.new('RGB',(cols*(dw+6),dh+36),'#282930');d=ImageDraw.Draw(board);frames=[]
 for i,(im,f,row) in enumerate(zip(ims,files,rs)):
  z=im.resize((dw,dh),Image.Resampling.NEAREST);board.paste(z,(i*(dw+6),25),z);d.text((i*(dw+6)+4,4),f'{i:02} / {row["duration_ms"]}ms',fill='white')
  alpha=im.getchannel('A');bbox=alpha.getbbox();re=Image.open(p/'roundtrip'/f.name).convert('RGBA');same=im.tobytes()==re.tobytes();opaque=sum(1 for a in alpha.getdata() if a);edge=any(alpha.getpixel((x,0)) or alpha.getpixel((x,h-1)) for x in range(w)) or any(alpha.getpixel((0,y)) or alpha.getpixel((w-1,y)) for y in range(h));checks.append({'frame':f.name,'native_reopen_pixel_exact':same,'transparent_border':not edge,'binary_alpha':set(alpha.getdata())<={0,255}})
  hand=None
  if name.startswith('npc_') and '_install' in name:
   ang=math.radians([30,15,0,0,15,30][i]);x=round(150+44*math.cos(ang)+24*math.sin(ang));y=round(117+44*math.sin(ang)-24*math.cos(ang));hand=[255-x,y] if 'porter' in name else [x,y]
  elif name=='npc_porter_walk_in':
   bob=[0,2,-2,0,2,-2][i];hand=[[105,165+bob],[166,161+bob]]
  frames.append({'index':i,'file':'frames/'+f.name,'duration_ms':int(row['duration_ms']),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bbox':bbox,'opaque_pixels':opaque,'coverage':round(opaque/(w*h),5),'banner_hand_px_top_left':hand,'ground_y_top_origin':224 if name.startswith('npc_') else None})
 board.save(p/(name+'_contact.png'))
 if name=='banner_install':
  bb=Image.new('RGB',(1300,len(ims)*226),'#282930');bd=ImageDraw.Draw(bb)
  for i,im in enumerate(ims):bd.text((8,i*226+3),f'{i:02} / {rs[i]["duration_ms"]}ms',fill='white');bb.paste(im,(8,i*226+24),im)
  bb.save(p/'08-banner-all-frames-native.png')
 bg=[]
 for im in ims:
  z=im.resize((dw*2,dh*2),Image.Resampling.NEAREST) if w<1000 else im.resize((dw,dh),Image.Resampling.NEAREST);q=Image.new('RGB',z.size,'#282930');q.paste(z,(0,0),z);bg.append(q)
 bg[0].save(p/(name+'_preview.gif'),save_all=True,append_images=bg[1:],duration=[int(x['duration_ms']) for x in rs],loop=0,disposal=2)
 sh=Image.new('RGBA',(w*len(ims),h),(0,0,0,0))
 for i,im in enumerate(ims):sh.paste(im,(i*w,0))
 sh.save(p/(name+'_sheet.png'))
 unique=len(set(im.tobytes() for im in ims));clips.append({'name':name,'native':name+'.aseprite','native_sha256':hashlib.sha256((p/(name+'.aseprite')).read_bytes()).hexdigest(),'tag':name,'sheet':name+'_sheet.png','frame_count':len(ims),'unique_pixel_frames':unique,'canvas':[w,h],'ppu':128,'pivot':[.5,.125] if name.startswith('npc_') else [.5,1] if name.startswith('banner') else [.5,.5],'duration_ms':sum(int(x['duration_ms']) for x in rs),'loop':'walk_' in name or name.endswith('_idle'),'nominal_fps':10 if 'walk_' in name else 3 if name.endswith('_idle') else 8 if name=='banner_install' else 12 if name.startswith('quality') else 'variable hold','frames':frames,'status':'EXPORTED_REVIEW_PENDING_RUNTIME'})
# Canonical and mirror install pose at 64px: QA guides only.
b=Image.new('RGB',(520,240),'#282930');d=ImageDraw.Draw(b)
for j,role in enumerate(['porter','rigger']):
 for k in range(2):
  im=Image.open(p/'frames'/f'npc_{role}_install_02.png').convert('RGBA')
  if k:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
  im=im.resize((128,128),Image.Resampling.NEAREST);x=j*260+k*130;b.paste(im,(x,30),im);small=im.resize((64,64),Image.Resampling.NEAREST);b.paste(small,(x+32,165),small);d.text((x+3,5),role+(' flipX' if k else ' canonical'),fill='white')
b.save(p/'07-npc-mirror-64px.png')
sources=[]
for n in ['bartender','tea_sommelier']:
 for clip,indices in [('idle',[0]),('move',range(6))]:
  for i in indices:
   f=p.parents[1]/'Assets/_Project/Art/v1'/n/'frames'/f'{n}_{clip}_right_{i:02}.png';sources.append({'path':str(f),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
manifest={'version':1,'scope':'Steam master challenge staged art only; no Assets import','production_status':'COORDINATOR_RUNTIME_REVIEW_REQUIRED','tools':{'Aseprite':'installed local Aseprite.exe, hidden batch Lua raster editing, native save/reopen/export','Python_Pillow':'reference reads, contact sheets, GIF previews, transparent sheet assembly, hashes and alpha verification','PixelLab':'installed extension; zero generation requests; not used to author','image_service':'none','GUI':'none; assigned slot returned'},'quality_hooks':['manufacture_completed','drink_consumed','drink_submitted'],'quality_hook_policy':'Play only after successful transaction, one effect per committed event; grade chosen by implementation, never generate outcome from art','sorting':{'npc':'Actor 2 / Y sort','banner':'BackgroundProp -9','vfx':'AirVfx 4'},'texture_import':{'filter':'Point','compression':'None','mipmaps':False,'alpha':'straight RGBA, binary alpha, no checkerboard baked','wrap':'Clamp','blend':'standard alpha, no additive'},'banner':{'letters_baked':False,'unity_text':'박람회','text_safe_normalized':[.10,.20,.90,.80],'pins_final_px_top_left':[[43,20],[1237,20]],'recommended_import':'individual 1280x192 frames; 10240px QA sheet is not preferred runtime texture','settled_frame':'frames/banner_install_07.png','folded_note':'first runtime roll shifted upward55px from approved representative to avoid vertical jump'},'npc_reuse':{'walk':'6 existing source lower-body gaits, fixed role upper body; upper torso bob [0,2,-2,0,2,-2] px; no new arm-swing animation claimed','install':'3 rotated reach keyposes, 6 exposures [30,15,0,0,15,30]deg; approved bent-arm pixels reused/mirrored for opposite role','idle':'1 still drawing repeated 4 exposures; not 4 unique poses','walk_out':'6 gait reuses; porter cloth removed; rigger walk_out is pixel-identical to walk_in','direction':'Canonical actors face right. flipX transforms body and ALL anchors; mirrored coordinate x=255-x; do not mirror only rope/arm','anchor_coordinates':'top-left pixel grid; local Unity offset ((x-128)/128,(224-y)/128), then apply flipX consistently','attach_window':'install exposures02/03 stable high grip; banner pins fixed only after settled frame07','runtime_caveat':'Cloth rollout endpoints move across canvas. Runtime must stage NPC movement/hand attachment against exported per-frame anchors; no integrated alignment or foot sliding test performed'},'intro_schedule_ms':{'walk_in':[0,800],'install':[800,2300],'walk_out':[2300,3500],'banner_play':[1050,2050],'combat_resume':3500,'combat_policy':'implementation owns full damage/spawn/time pause during all3500ms'},'pooling':{'loop_vfx':False,'clear_on_finish':True,'last_quality_frame':'fully transparent; disable renderer and return instance on exact end','retrigger':'reset frame0, elapsed0, colorwhite, transform scale and flip; never leave previous grade sprite','limits':'one quality effect per successful event, short independent pool; runtime cap/coalescing belongs to implementation','low_effects':'use *_low clips instead of full clip, not in addition'},'validation':{'frame_pixel_roundtrip':'all checked in validation.json','visual':'all clip contact sheets opened; frame silhouette and boundaries reviewed; no aesthetic PASS from counts or hashes','continuous_playback':'GIF exported; in-game playback and composition NOT RUN','runtime_small_screen':'64px image proxies only; actual Unity 720p/low-effects validation NOT RUN'},'followup':['Affinities cocktail foam / coffee bean steam / tea leaf wind not authored in this core checkpoint; separate follow-up after core runtime acceptance','World competition and unique tea growth out of scope'],'source_files':sources,'clips':clips}
(p/'manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf8');(p/'validation.json').write_text(json.dumps({'frame_count':len(checks),'pixel_exact_count':sum(x['native_reopen_pixel_exact'] for x in checks),'transparent_border_count':sum(x['transparent_border'] for x in checks),'binary_alpha_count':sum(x['binary_alpha'] for x in checks),'checks':checks},indent=2),encoding='utf8')
print([(x['name'],x['frame_count'],x['unique_pixel_frames'],x['duration_ms']) for x in clips]);print('validation',len(checks),all(all(v for k,v in x.items() if k!='frame') for x in checks))
