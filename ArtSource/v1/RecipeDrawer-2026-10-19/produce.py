from pathlib import Path
from PIL import Image,ImageChops,ImageDraw
import json, shutil, hashlib,subprocess
P=Path(__file__).parent
V=P.parent
A=Path('C:/Program Files/Aseprite/Aseprite.exe')
OPEN=Path('C:/Users/user/AppData/Roaming/orca/codex-accounts/b9b7063f-5dd2-4f55-b926-d600f95cb5a0/home/generated_images/01a15374-a553-7e00-9aa7-b1a1ea1d35c4/exec-83e370e6-a6c2-41da-9f0c-27ec1c83af28.png')
def clean(im):
 im=im.convert('RGBA'); im.putalpha(im.getchannel('A').point(lambda a:255 if a>=128 else 0)); bg=Image.new('RGBA',im.size);bg.paste(im,(0,0),im.getchannel('A'));return bg
shutil.copy2(OPEN,P/'raw/open_generated.png')
im=clean(Image.open(OPEN)).resize((480,320),Image.Resampling.NEAREST)
body=im.copy();body.paste((0,0,0,0),(0,176,480,320))
front=im.copy();front.paste((0,0,0,0),(0,0,480,176))
body.save(P/'dl_recipe_drawer_body.png');front.save(P/'dl_recipe_drawer_front.png')
print('bounds',body.getbbox(),front.getbbox())
CLOSED=Path('C:/Users/user/AppData/Roaming/orca/codex-accounts/b9b7063f-5dd2-4f55-b926-d600f95cb5a0/home/generated_images/01a15374-a553-7e00-9aa7-b1a1ea1d35c4/exec-e2e21c71-a703-4a74-8eb0-2eb4bca90c70.png')
shutil.copy2(CLOSED,P/'raw/closed_generated.png')
closedtop=clean(Image.open(CLOSED)).resize((480,320),Image.Resampling.NEAREST)
closedtop.paste((0,0,0,0),(0,125,480,320))
closed=closedtop.copy();closed.alpha_composite(front,(0,-51));closed.save(P/'dl_recipe_drawer_closed.png')
# Original generated brass/front retained byte-exact through both endpoint states.
# All illustrated content comes from imagegen; only layer separation/coordinate transforms here.
paperpath=V/'ProgressionUI-2026-10-19/assets/torn_recipe/torn_recipe.png'
paper=Image.open(paperpath).convert('RGBA')
shutil.copy2(paperpath,P/'raw/torn_recipe_reference.png')
refs=[]
for k in ['recipe_storage','torn_recipe','spring_notebook','recipe_tear_store']:
 folder=V/'ProgressionUI-2026-10-19/assets'/k
 native=folder/(k+'.aseprite')
 out=P/'qa'/('reference_'+k+'.png')
 args=[str(A),'-b',str(native),'--frame-range','0,0','--save-as',str(out)]
 r=subprocess.run(args,capture_output=True)
 refs.append({'name':k,'native':str(native),'native_sha256':hashlib.sha256(native.read_bytes()).hexdigest(),'native_read_exit':r.returncode})
json.dump(refs,open(P/'qa/reference-native.json','w'),indent=2)
# Runtime preview exact state descriptions. Top-left coordinates, no baked text.
slots=[{'id':i,'position':[82+i*21,94+i*12],'size':[258,112],'rotation':0,'depth':i} for i in range(5)]
steps=[{'name':'closed','t':0},{'name':'open','t':1},{'name':'selected','t':1,'paper':{'position':[124,110],'size':[258,112],'layer':'behind_front'}},{'name':'pull_mid','t':1,'paper':{'position':[116,63],'size':[275,150],'layer':'behind_front'}},{'name':'clear_rim','t':1,'paper':{'position':[106,8],'size':[275,160],'layer':'behind_front'}},{'name':'unfolded','t':1,'paper':{'position':[68,18],'size':[344,256],'layer':'overlay'}},{'name':'reinsert_mid','t':1,'paper':{'position':[124,133],'size':[258,112],'layer':'behind_front'}},{'name':'returned','t':1}]
def translated_front(t):
 f=Image.new('RGBA',(480,320));f.alpha_composite(front,(0,round(-51*(1-t))));return f
def base(t):
 b=closedtop.copy()
 # Fixed back. Body revealed progressively behind the sliding front.
 if t>0:b.alpha_composite(body)
 return b
occlusions=[]
def render(s):
 t=s['t'];b=base(t)
 if t>0:
  for slot in slots:
   if slot['id']==2 and s.get('paper'):continue
   b.alpha_composite(paper.resize(tuple(slot['size']),Image.Resampling.NEAREST),tuple(slot['position']))
 p=s.get('paper');f=translated_front(t)
 if p and p['layer']=='behind_front':b.alpha_composite(paper.resize(tuple(p['size']),Image.Resampling.NEAREST),tuple(p['position']))
 b.alpha_composite(f)
 if p and p['layer']=='overlay':b.alpha_composite(paper.resize(tuple(p['size']),Image.Resampling.NEAREST),tuple(p['position']))
 if p:
  mask=Image.new('L',(480,320));mask.paste(paper.getchannel('A').resize(tuple(p['size']),Image.Resampling.NEAREST),tuple(p['position']))
  overlap=ImageChops.multiply(mask,f.getchannel('A'))
  occlusions.append({'state':s['name'],'paper_pixels_behind_front':sum(1 for v in overlap.getdata() if v),'layer':p['layer']})
 return b
wall=Image.open(V/'CraftingBackdrops-2026-10-19/dl_craft_bar_wall.png').convert('RGBA').resize((480,320),Image.Resampling.NEAREST)
previews=[]
for s in steps:
 b=wall.copy();b.alpha_composite(render(s));b.save(P/'qa'/('state_'+s['name']+'.png'));previews.append(b)
sheet=Image.new('RGB',(480*4,350*2),(35,29,28));d=ImageDraw.Draw(sheet)
for i,(s,b) in enumerate(zip(steps,previews)):
 x=(i%4)*480;y=(i//4)*350;sheet.paste(b,(x,y+26));d.text((x+12,y+7),s['name'],fill=(245,224,185))
sheet.save(P/'qa/contact_states.png')
# Six actual shared-layer opening transforms, no perspective morph or endpoint sprite jump.
opening=[]
for i,t in enumerate([0,.2,.4,.6,.8,1]):
 b=wall.copy();s={'t':t,'name':f'opening_{i}'};b.alpha_composite(render(s));b.save(P/'qa'/f'opening_{i}.png');opening.append(b)
opening[0].save(P/'qa/opening.gif',save_all=True,append_images=opening[1:]+opening[-2:0:-1],duration=95,loop=0)
seq=[previews[i] for i in [0,1,2,3,4,5,4,3,6,7,0]]
seq[0].save(P/'qa/paper_cycle.gif',save_all=True,append_images=seq[1:],duration=[500,500,300,350,200,1100,200,250,350,550,500],loop=0)
for w,h in [(1280,720),(836,705),(640,480)]:
 out=Image.new('RGB',(w,h),(32,27,27));scale=min((w-48)/480,(h-100)/320);size=(round(480*scale),round(320*scale));b=previews[3].resize(size,Image.Resampling.NEAREST);out.paste(b,((w-size[0])//2,50));out.save(P/'qa'/f'preview_{w}x{h}.png')
# Contract uses actual UI content rect inherited from torn_recipe.
path=[{'phase':'stored','position':[124,118],'size':[258,112],'layer':'paperstack','textVisible':False},{'phase':'selected','position':[124,110],'size':[258,112],'layer':'paperstack','textVisible':True},{'phase':'pull_mid','position':[116,63],'size':[275,150],'layer':'paperstack','textVisible':True},{'phase':'clear_rim','position':[106,8],'size':[275,160],'layer':'paperstack','textVisible':True},{'phase':'unfolded','position':[68,18],'size':[344,256],'layer':'overlay','textVisible':True}]
for p in path:
 sx=p['size'][0]/172;sy=p['size'][1]/128;p['scaleXY']=[sx,sy];p['textSafeRect']=[p['position'][0]+10*sx,p['position'][1]+20*sy,152*sx,96*sy]
manifest={'status':'pending_root_visual_review','ready':False,'integrated':False,'verified':False,'coordinateOrigin':'top-left pixels; x,y,width,height; half-open rect','canvas':[480,320],'pivotPixelsTopLeft':[240,160],'pivotNormalizedUnity':[.5,.5],'filter':'Point','mipmap':False,'compression':'None','alpha':'binary; transparent RGB zero','textBaked':False,'paperSource':str(paperpath),'paperSourceSize':[172,128],'paperTextSafeRect':[10,20,152,96],'layerOrder':['body','paperstack runtime plus dividers','front','extracted paper runtime overlay'],'paperCavityRect':[96,91,288,85],'paperCavityPolygon':[[96,91],[385,91],[424,176],[57,176]],'frontOcclusionRect':[22,176,435,86],'frontOcclusion':'use actual dl_recipe_drawer_front alpha as SpriteMask/stencil; paper text shares identical mask and transform','paperSlots':slots,'paperPath':path,'returnPath':'reverse paperPath to the same selected slot; switch from overlay to behind-front only after entire paper bottom <176','tearStoreConnection':{'source':'recipe_tear_store frames00..05; replace old box destination after tear','start':[68,18],'destinationSlot':2,'insertPath':'reverse paperPath; publish save only after successful persistence; failed save restores unfolded sheet'},'opening':{'durationMs':450,'fixedRearPivot':[240,77],'bodyScale':[1,1],'frontScale':[1,1],'frontTranslationClosed':[0,-51],'frontTranslationOpen':[0,0],'frames':[{'progress':t,'frontTranslation':[0,round(-51*(1-t))],'bodyVisible':t>0} for t in [0,.2,.4,.6,.8,1]],'closedSprite':'dl_recipe_drawer_closed','endpointRule':'closed is closed top plus IDENTICAL open front translated -51; no front/handle scale or pivot jump','bodyReveal':'render rear/body at fixed coordinates behind front; paperstack clip yMax=176-51*(1-progress), hide stack at progress0; overlay paper unmasked only after clear-rim'},'touch':'runtime tap targets at least44 screen px; page slots in groups for30 sheets; no extra30 baked sheets','runtimeResponsibilities':['actual text and buttons','selection focus and scroll','empty state','30-sheet paging','long-name wrap','rapid reselect/cancel','save failure rollback','skip state convergence'],'provenance':'provenance.json','assets':[]}
json.dump(manifest,open(P/'manifest.json','w'),indent=2)
json.dump({'occlusionEvidence':occlusions,'rootVisualReview':'pending','runtimeVerified':False},open(P/'qa.json','w'),indent=2)
print('previews and contract written')
DIV=Path('C:/Users/user/AppData/Roaming/orca/codex-accounts/b9b7063f-5dd2-4f55-b926-d600f95cb5a0/home/generated_images/01a15374-a553-7e00-9aa7-b1a1ea1d35c4/exec-5ac979b1-b9f5-4369-a145-dc39a0576b3d.png')
shutil.copy2(DIV,P/'raw/divider_generated.png')
divider=clean(Image.open(DIV)).resize((128,64),Image.Resampling.NEAREST);divider.save(P/'dl_recipe_divider.png')
assets=[];checks=[]
for name in ['dl_recipe_drawer_body','dl_recipe_drawer_front','dl_recipe_drawer_closed','dl_recipe_divider']:
 png=P/(name+'.png');native=P/(name+'.aseprite');sprite=Image.open(png).convert('RGBA')
 a=sprite.getchannel('A');sprite=sprite.convert('RGB').quantize(colors=48,dither=Image.Dither.NONE).convert('RGBA');sprite.putalpha(a);zero=Image.new('RGBA',sprite.size);zero.paste(sprite,(0,0),a);sprite=zero
 if name=='dl_recipe_drawer_closed':
  sprite.paste((0,0,0,0),(0,125,480,320));sprite.alpha_composite(Image.open(P/'dl_recipe_drawer_front.png').convert('RGBA'),(0,-51))
 sprite.save(png)
 r=subprocess.run([str(A),'-b',str(png),'--save-as',str(native)],capture_output=True)
 rt=P/'qa'/(name+'_roundtrip.png');r2=subprocess.run([str(A),'-b',str(native),'--save-as',str(rt)],capture_output=True)
 exact=rt.exists() and Image.open(rt).convert('RGBA').tobytes()==sprite.tobytes()
 check={'id':name,'rgba':sprite.mode=='RGBA','size':list(sprite.size),'alphaBinary':set(a.getdata())<={0,255},'alphaBounds':list(sprite.getbbox()),'nativeExit':r.returncode,'nativeRoundtripExit':r2.returncode,'nativeRoundtripExact':exact,'transparentRGBZero':all(px[:3]==(0,0,0) for px in sprite.getdata() if px[3]==0)}
 checks.append(check);assets.append({'id':name,'png':png.name,'native':native.name,'size':list(sprite.size),'alphaBounds':list(sprite.getbbox()),'pivotPixelsTopLeft':[sprite.width/2,sprite.height/2],'pivotNormalizedUnity':[.5,.5],'sha256':{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in [png,native]}})
manifest['assets']=assets
manifest['divider']={'size':[128,64],'tabSafeRect':[26,12,22,6],'tabNote':'blank tab for runtime label; runtime may enlarge physical touch target independently','anchors':[{'slot':i,'position':[82+i*21,70+i*12],'renderSize':[258,112]} for i in range(5)]}
json.dump(manifest,open(P/'manifest.json','w'),indent=2)
qa=json.load(open(P/'qa.json'));qa['assetChecks']=checks;qa['passed']=all(c['alphaBinary'] and c['nativeRoundtripExact'] and c['transparentRGBZero'] and c['nativeExit']==0 for c in checks);json.dump(qa,open(P/'qa.json','w'),indent=2)
# Repeat previews with final exported quantized layers and generated dividers.
body=Image.open(P/'dl_recipe_drawer_body.png');front=Image.open(P/'dl_recipe_drawer_front.png');divider=Image.open(P/'dl_recipe_divider.png');closed=Image.open(P/'dl_recipe_drawer_closed.png');closedtop=closed.copy();closedtop.paste((0,0,0,0),(0,125,480,320))
def render_final(s):
 t=s['t']
 if t==0:return closed.copy()
 b=base(t)
 for slot in slots:
  pos=slot['position'];di=divider.resize((258,112),Image.Resampling.NEAREST)
  b.alpha_composite(di,(pos[0],pos[1]-24))
  if slot['id']==2 and s.get('paper'):continue
  b.alpha_composite(paper.resize(tuple(slot['size']),Image.Resampling.NEAREST),tuple(pos))
 p=s.get('paper')
 if p and p['layer']=='behind_front':b.alpha_composite(paper.resize(tuple(p['size']),Image.Resampling.NEAREST),tuple(p['position']))
 clipped=b.crop((0,0,480,round(176-51*(1-t))));b=base(t);b.alpha_composite(clipped)
 b.alpha_composite(translated_front(t))
 if p and p['layer']=='overlay':b.alpha_composite(paper.resize(tuple(p['size']),Image.Resampling.NEAREST),tuple(p['position']))
 return b
previews=[]
for s in steps:
 b=wall.copy();b.alpha_composite(render_final(s));b.save(P/'qa'/('state_'+s['name']+'.png'));previews.append(b)
sheet=Image.new('RGB',(1920,700),(35,29,28));d=ImageDraw.Draw(sheet)
for i,(s,b) in enumerate(zip(steps,previews)):
 x=(i%4)*480;y=(i//4)*350;sheet.paste(b,(x,y+26));d.text((x+12,y+7),s['name'],fill=(245,224,185))
sheet.save(P/'qa/contact_states.png')
opening=[]
for i,t in enumerate([0,.2,.4,.6,.8,1]):
 b=wall.copy();b.alpha_composite(render_final({'t':t,'name':f'opening_{i}'}));b.save(P/'qa'/f'opening_{i}.png');opening.append(b)
opening[0].save(P/'qa/opening.gif',save_all=True,append_images=opening[1:]+opening[-2:0:-1],duration=95,loop=0)
seq=[previews[i] for i in [0,1,2,3,4,5,4,3,6,7,0]]
seq[0].save(P/'qa/paper_cycle.gif',save_all=True,append_images=seq[1:],duration=[500,500,300,350,200,1100,200,250,350,550,500],loop=0)
for w,h in [(1280,720),(836,705),(640,480)]:
 for idx in [1,3,5,6]:
  out=Image.new('RGB',(w,h),(32,27,27));scale=min((w-48)/480,(h-100)/320);size=(round(480*scale),round(320*scale));b=previews[idx].resize(size,Image.Resampling.NEAREST);out.paste(b,((w-size[0])//2,50));out.save(P/'qa'/f'preview_{steps[idx]["name"]}_{w}x{h}.png')
# Opening contact sheet exposes alignment at every transform.
contact=Image.new('RGB',(1440,640))
for i,b in enumerate(opening):contact.paste(b,((i%3)*480,(i//3)*320))
contact.save(P/'qa/contact_opening.png')
print(json.dumps({'passed':qa['passed'],'assets':checks}))
