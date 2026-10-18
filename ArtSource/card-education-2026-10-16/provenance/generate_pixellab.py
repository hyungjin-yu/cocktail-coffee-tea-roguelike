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
 req=dict(description=desc+' Chunky pixel art cafe game teaching icon, warm consistent palette cream EAD4A5 sage 466B45 silver A7B1C2 dark outline 201D29, three shading tones per material, upper left lighting, ground level three quarter side view, centered complete object, 5 pixel transparent margin, transparent background.',negative_description='background, glow, halo, aura, floor, text, label, logo, letters, numbers, smooth, antialiasing, blurry, top down, isometric, cropped object',text_guidance_scale=8,color_image='No',init_image='No',init_image_strength=300,image_size=dict(width=w,height=h),inpainting_image='No',reference_image='No',view='side',direction='none',no_background=True,seed=0,use_inpainting=False,map_tile=False,force_colors=False,isometric=False,oblique_projection=False,coverage_percentage=.9,outline='single color black outline',shading='basic shading',detail='medium detail',output_method='New frame',model_name='generate_style',style_guidance_scale=3,style_strength=0)
 (ROOT/(name+'.request.json')).write_text(json.dumps(req,indent=2),encoding='utf-8')
 req.update({k:cfg[k] for k in ['secret','tier','version']})
 for k in ['inpainting_image','init_image','color_image','reference_image']: req.pop(k,None)
 req.update(init_image={'base64':''},color_image={'base64':''})
 version_info=ROOT/'official-version.json'
 if version_info.exists(): req['version']=json.loads(version_info.read_text())['current']
 ws=websocket.create_connection('wss://api.pixellab.ai/generate-style',timeout=90)
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
