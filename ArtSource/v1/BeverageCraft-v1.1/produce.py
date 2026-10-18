"""PixelLab installed-extension protocol, following prior card-education pipeline.
Credentials stay only in memory. Never writes auth fields to receipts.
"""
import base64, io, json, os, pathlib, sys, time
import websocket
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parent
EXT=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
REF=ROOT.parents[1]/'card-education-2026-10-16/card_gin.png'
SPECS=[
 ('dl_jigger',32,48,'One empty silver double cocktail jigger, hourglass shaped measuring tool, both cups open, transparent inner bowl, no liquid. No hands.'),
 ('dl_shaker',48,80,'One closed silver cobbler cocktail shaker, removable small cap, sturdy tapered body, silver three tone shading. No hands.'),
 ('dl_mixing_glass',48,64,'One empty transparent cocktail mixing glass with pouring lip on right, thick bottom, sparse silver rim and reflections, completely transparent empty interior. No liquid, no spoon.'),
 ('dl_glass_cocktail',48,64,'One empty low rocks drinking glass, broad open oval rim, thick flat base, sparse silver highlights, entirely transparent empty interior. No liquid, no garnish.'),
 ('dl_chaha',48,24,'One empty ivory porcelain Chinese chaha tea leaf presentation scoop, boat shape open shallow tray, tapered pouring lip at right. No leaves, no liquid.'),
 ('dl_teapot',64,56,'One small ivory ceramic Chinese tea teapot, round belly, short spout pointing right, loop handle left, separate small lid. Plain warm cream porcelain. No liquid, no steam.'),
 ('dl_teacup',40,32,'One small empty ivory porcelain Chinese tea tasting cup, no handle, broad visible open oval rim, transparent empty bowl interior. No liquid or steam.'),
 ('dl_water_kettle',48,56,'One silver water pitcher with open oval mouth, long curved pouring spout to right and handle left, no steam, no liquid stream.'),
 ('dl_barspoon',16,80,'One long silver bar spoon upright, small oval bowl at bottom, narrow twisted shaft, tiny rounded knob on top. Entire long spoon visible, no hands.'),
 ('dl_bottle_gin',24,56,'One slender upright transparent gin bottle, silver cap, ivory plain blank label, colorless clear contents, no decoration or plants.'),
 ('dl_bottle_rum',24,56,'One squat upright amber rum bottle, brown cap, plain ivory blank label, pale amber liquid visible.'),
 ('dl_bottle_blue_curacao',24,56,'One upright narrow blue curacao bottle, silver cap, plain ivory blank label, saturated cyan blue liquid visible.'),
 ('dl_bottle_grenadine',24,56,'One upright grenadine syrup bottle, red cap, plain ivory blank label, strong ruby red contents.'),
 ('dl_bottle_lemon_juice',24,56,'One small upright lemon juice bottle, yellow cap, plain ivory blank label with single lemon shape, cloudy pale yellow contents.'),
 ('dl_bottle_honey',24,56,'One small golden honey jar, wide mouth and cream screw lid, thick gold contents, blank ivory label.'),
 ('dl_tin_black_tea',32,32,'One open warm brown tea tin containing curled dark brown dry black tea leaves, ivory plain blank label, no liquid.'),
 ('dl_tin_green_tea',32,32,'One open sage green tea tin containing pointed green dry tea leaves, ivory plain blank label, no liquid.'),
 ('dl_hand',32,32,'One human right hand with cream shirt cuff and dark bartender vest sleeve, holding an invisible narrow glass by fingers, palm facing viewer, fingers anatomically clear, no object, no arm beyond cuff.'),
]

def generate(spec):
 name,w,h,desc=spec
 raw=ROOT/'provenance'/f'{name}.raw.png'
 if raw.exists(): return
 cfg=json.loads((EXT/'package.json').read_text(encoding='utf8'))
 req=dict(description=desc+' Chunky crisp pixel art cafe game item, dark outline #201D29, cream #EAD4A5, sage #466B45, silver #A7B1C2, three tones per material, upper-left lighting, front three-quarter side view. Exactly one centered object filling canvas with 2 pixel margin. Transparent background. No floor, shadows, glow, lettering, logos or text.',image_size=dict(width=w,height=h),seed=0,no_background=True,model_name='generate_image_pro_flash',output_format='frames',style_options=dict(color_palette=True,outline=True,detail=True,shading=True))
 style=Image.open(REF).convert('RGBA'); style.thumbnail((min(w,32),min(h,32)),Image.Resampling.NEAREST)
 req['style_image']=dict(image=dict(base64=base64.b64encode(style.tobytes()).decode()),size=dict(width=style.width,height=style.height))
 raw.parent.mkdir(exist_ok=True)
 (raw.parent/f'{name}.request.json').write_text(json.dumps(req,indent=2),encoding='utf8')
 req.update({k:cfg[k] for k in ('secret','tier','version')})
 version=pathlib.Path('C:/Users/user/Documents/Personal_Note/03_프로젝트/칵테일 커피 티 로그라이크 기획/ArtProduction/card-education-2026-10-16/official-version.json')
 req['version']=json.loads(version.read_text())['current']
 ws=websocket.create_connection('wss://api.pixellab.ai/generate-image-pro-flash',timeout=60)
 ws.send(json.dumps(req)); start=time.time(); saved=False
 while time.time()-start<240:
  data=json.loads(ws.recv())
  if 'detail' in data: raise RuntimeError(str(data['detail']))
  for im in data.get('images',[])+([data['image']] if data.get('image') else []):
   binary=base64.b64decode(im['base64']); size=(im.get('width',w),im.get('height',h))
   out=Image.open(io.BytesIO(binary)) if binary.startswith(b'\x89PNG') else Image.frombytes('RGBA',size,binary)
   out.save(raw); saved=True
  if data.get('type')=='message_done':
   receipt={k:v for k,v in data.items() if k in ('seed','generation_id','type')}
   receipt.update(model='generate_image_pro_flash',styleReference=str(REF),requestedSize=[w,h])
   (raw.parent/f'{name}.receipt.json').write_text(json.dumps(receipt,indent=2)); break
 ws.close()
 if not saved: raise RuntimeError('No image returned')
 print(name,'PixelLab image saved',flush=True)

if __name__=='__main__':
 for spec in SPECS:
  if len(sys.argv)>1 and spec[0]!=sys.argv[1]: continue
  try: generate(spec)
  except Exception as err:
   print(spec[0],type(err).__name__,str(err)[:300],flush=True); sys.exit(1)
