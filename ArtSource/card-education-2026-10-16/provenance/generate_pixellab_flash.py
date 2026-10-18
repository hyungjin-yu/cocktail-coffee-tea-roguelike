"""PixelLab installed-extension protocol; credentials stay in memory, never logged."""
import base64, json, os, pathlib, time, io
import websocket
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parent
EXT=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
common=''
SPECS=[(x[0],64,64,x[1]) for x in json.loads((ROOT/'specs.json').read_text())]
def run(spec):
 name,w,h,desc=spec
 cfg=json.loads((EXT/'package.json').read_text(encoding='utf-8'))
 req=dict(description=desc+' Chunky pixel art game item icon, dark clean outlines, warm cream and silver and sage palette, three shading tones per material, upper-left light, front three-quarter side view, transparent background. No text, logos, glow or halo. All objects inside a 5 pixel transparent margin.',image_size=dict(width=w,height=h),seed=0,no_background=True,model_name='generate_image_pro_flash',output_format='frames',style_options=dict(color_palette=True,outline=True,detail=True,shading=True))
 style=Image.open(ROOT/'card_gin.raw.png').convert('RGBA')
 req['style_image']=dict(image=dict(base64=base64.b64encode(style.tobytes()).decode()),size=dict(width=64,height=64))
 (ROOT/(name+'.request.json')).write_text(json.dumps(req,indent=2),encoding='utf-8')
 req.update({k:cfg[k] for k in ['secret','tier','version']})
 req['version']=json.loads((ROOT/'official-version.json').read_text())['current']
 ws=websocket.create_connection('wss://api.pixellab.ai/generate-image-pro-flash',timeout=90)
 ws.send(json.dumps(req)); start=time.time(); saved=False
 while time.time()-start<240:
  data=json.loads(ws.recv())
  if 'detail' in data: raise RuntimeError(str(data['detail']))
  imgs=data.get('images',[])+([data['image']] if data.get('image') else [])
  for im in imgs:
   raw=base64.b64decode(im['base64']); size=(im.get('width',w),im.get('height',h))
   image=Image.open(io.BytesIO(raw)) if raw.startswith(b'\x89PNG') else Image.frombytes('RGBA',size,raw)
   image.save(ROOT/(name+'.raw.png')); saved=True
  if data.get('type')=='message_done':
   (ROOT/(name+'.receipt.json')).write_text(json.dumps({k:v for k,v in data.items() if k in ['seed','generation_id','type']},indent=2))
   break
 ws.close()
 if not saved: raise RuntimeError('No image returned')
 print(name,'PixelLab output saved',flush=True)
if __name__=='__main__':
 import sys
 for s in SPECS:
  if len(sys.argv)>1 and s[0]!=sys.argv[1]: continue
  if (ROOT/(s[0]+'.raw.png')).exists(): continue
  for attempt in range(5):
   try: run(s); break
   except Exception as e:
    print(s[0],type(e).__name__,str(e)[:250],flush=True)
    if 'concurrent jobs' in str(e) and attempt<4: time.sleep(35); continue
    raise SystemExit(1)
