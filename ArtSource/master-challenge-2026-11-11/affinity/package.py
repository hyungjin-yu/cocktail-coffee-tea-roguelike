from pathlib import Path
from PIL import Image,ImageDraw
import json,csv,hashlib
p=Path(__file__).resolve().parent;rows=list(csv.DictReader((p/'native-frames.csv').open()));names=list(dict.fromkeys(x['clip'] for x in rows));clips=[];checks=[]
for name in names:
 rs=[x for x in rows if x['clip']==name];files=[p/'frames'/f'{name}_{int(x["index"]):02}.png' for x in rs];ims=[Image.open(f).convert('RGBA') for f in files];frames=[]
 board=Image.new('RGB',(len(ims)*260,3*284),'#282930');d=ImageDraw.Draw(board)
 for j,(bg,label) in enumerate([('#282930','dark'),('#eadfc1','light'),('#6c4933','cafe brown')]):
  d.rectangle((0,j*284,board.width,j*284+283),fill=bg)
  for i,im in enumerate(ims):
   y=j*284;z=im.resize((256,256),Image.Resampling.NEAREST);board.paste(z,(i*260,y+25),z);d.text((i*260+4,y+4),f'{label} {i:02} / 83ms',fill='white' if j!=1 else '#30232f')
 board.save(p/(name+'_contact.png'))
 sheet=Image.new('RGBA',(128*len(ims),128),(0,0,0,0));gif=[]
 for i,(im,f) in enumerate(zip(ims,files)):
  sheet.paste(im,(i*128,0));b=Image.new('RGB',(256,256),'#282930');z=im.resize((256,256),Image.Resampling.NEAREST);b.paste(z,(0,0),z);gif.append(b)
  re=Image.open(p/'roundtrip'/f.name).convert('RGBA');a=im.getchannel('A');opaque=sum(1 for v in a.getdata() if v);edge=any(a.getpixel((x,0)) or a.getpixel((x,127)) for x in range(128)) or any(a.getpixel((0,y)) or a.getpixel((127,y)) for y in range(128));checks.append({'frame':f.name,'native_reopen_pixel_exact':im.tobytes()==re.tobytes(),'transparent_border':not edge,'binary_alpha':set(a.getdata())<={0,255}});frames.append({'file':'frames/'+f.name,'index':i,'duration_ms':83,'bbox':im.getbbox(),'opaque_pixels':opaque,'coverage':round(opaque/16384,5),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
 sheet.save(p/(name+'_sheet.png'));gif[0].save(p/(name+'_preview.gif'),save_all=True,append_images=gif[1:],duration=83,loop=0,disposal=2)
 clips.append({'name':name,'native':name+'.aseprite','native_sha256':hashlib.sha256((p/(name+'.aseprite')).read_bytes()).hexdigest(),'tag':name,'canvas':[128,128],'sheet':name+'_sheet.png','frames':frames,'exposure_count':len(ims),'unique_pixel_frames':len(set(im.tobytes() for im in ims)),'duration_ms':83*len(ims),'loop':False,'last_frame_fully_transparent':ims[-1].getbbox() is None,'ppu':128,'pivot':[.5,.5],'burst_origin_px':[64,64],'direction':'compact fan upward-right; optional rotate whole effect to impact direction','nominal_fps':12,'actual_frame_ms':83})
# Side-by-side peak at native, nearest2x and 64px on all three backgrounds.
b=Image.new('RGB',(880,3*310),'#282930');d=ImageDraw.Draw(b)
for j,(bg,label) in enumerate([('#282930','dark'),('#eadfc1','light'),('#6c4933','cafe brown')]):
 d.rectangle((0,j*310,880,j*310+309),fill=bg)
 for i,name in enumerate(['cocktail_foam','coffee_bean_steam','tea_leaf_wind']):
  im=Image.open(p/'frames'/f'{name}_02.png').convert('RGBA');x=i*290;y=j*310;d.text((x+5,y+4),label+' '+name,fill='white' if j!=1 else '#30232f');b.paste(im,(x,y+24),im);z=im.resize((128,128),Image.Resampling.NEAREST);b.paste(z,(x+132,y+24),z);z=im.resize((64,64),Image.Resampling.NEAREST);b.paste(z,(x+32,y+190),z)
b.save(p/'02-peak-backgrounds-64px.png')
core=[]
for line in (p.parent/'checkpoint-sha256.txt').read_text().splitlines():
 expected,name=line.split('  ',1);f=p.parent/name;core.append({'file':name,'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'matches_frozen_checkpoint':hashlib.sha256(f.read_bytes()).hexdigest().lower()==expected.lower()})
core_art=[]
for clip in json.loads((p.parent/'manifest.json').read_text(encoding='utf8'))['clips']:
 native=p.parent/clip['native'];core_art.append({'file':clip['native'],'matches_manifest':hashlib.sha256(native.read_bytes()).hexdigest()==clip['native_sha256']})
 for frame in clip['frames']:
  art=p.parent/frame['file'];core_art.append({'file':frame['file'],'matches_manifest':hashlib.sha256(art.read_bytes()).hexdigest()==frame['sha256']})
(p/'core-freeze-check.json').write_text(json.dumps({'all_match':all(x['matches_frozen_checkpoint'] for x in core) and all(x['matches_manifest'] for x in core_art),'checkpoint_files':core,'core15_native_and83_frame_checks':core_art},indent=2),encoding='utf8')
manifest={'scope':'affinity follow-up only, core15 clips frozen and coordinator approved for Unity handoff','status':'exported; runtime aesthetic/actual damage integration NOT RUN','representative_design':'coordinator approved representative01 comparison before animation expansion','tools':{'Aseprite':'actual hidden-batch Lua pixel drawing, layered/tagged native, reopen/export','Pillow':'contact/GIF/sheet assembly and verification','PixelLab':'not used; zero generation requests','external_AI':'none','GUI':'none'},'texture_import':{'ppu':128,'pivot':[.5,.5],'filter':'Point','wrap':'Clamp','compression':'None','mipmaps':False,'blend':'standard alpha, no additive','sorting':'AirVfx 4'},'hooks':{'trigger':'implementation invokes after actual positive affinity bonus damage has been committed, not on every normal hit','repeat':'one instance per confirmed qualifying hit; no visual-only bonus claim','rotation':'Canonical fan upward-right; rotate whole effect uniformly using impact direction, keep unit scale and central impact pivot','low':'replace full by matching *_low; keep bubble/bean/leaf silhouette; do not play both','pool':'return after full498ms/low249ms, clear sprite/disable renderer, stop animation, reset frame0 elapsed0 white tint scale1 rotation0 flipXYfalse on next acquire','budget':'Claude must set active-instance caps and coalescing; do not spawn one per damage tick without budget','visibility':'small bean/leaf shapes retained; brown-background highlight seam/lead edge maintained; actual small-screen combat QA pending'},'validation':{'contact_review':'all27 frames opened via six dark/light/brown sheets plus peak64px comparison','native_roundtrip':'27/27 pixel-exact after reopening six natives','last_frame':'all six clips fully transparent at final exposure','runtime':'NOT RUN: actual bonus-damage signal, pooling reset/cleanup, active budget, actual cafe visibility and collision/impact direction'},'clips':clips}
(p/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8');(p/'validation.json').write_text(json.dumps({'frame_count':len(checks),'all_pixel_exact':all(x['native_reopen_pixel_exact'] for x in checks),'all_transparent_border':all(x['transparent_border'] for x in checks),'all_binary_alpha':all(x['binary_alpha'] for x in checks),'all_last_frames_empty':all(x['last_frame_fully_transparent'] for x in clips),'core_checkpoint_unchanged':all(x['matches_frozen_checkpoint'] for x in core),'frames':checks},indent=2),encoding='utf8')
print([(x['name'],x['exposure_count'],x['unique_pixel_frames'],x['duration_ms'],x['last_frame_fully_transparent']) for x in clips]);print('core frozen',len(core),all(x['matches_frozen_checkpoint'] for x in core))
