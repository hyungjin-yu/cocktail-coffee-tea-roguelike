import base64,io,json,os,pathlib,sys,time,websocket
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parent
EXT=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
PROJECT=ROOT.parents[2]
REF=PROJECT/'Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png'
SPECS=[
('cocktail_aroma',64,80,'Distinct mature woman aged about 55, light warm skin, neat chin-length SILVER BOB haircut with straight silhouette, narrow oval face, thin rectangular navy eyeglasses, plum high-neck tailored jacket over ivory blouse. Slender upright shoulders. Cocktail aroma analyst, stirring specialist.'),
('cocktail_blend',64,80,'Distinct adult man aged about 35, medium brown skin, SHAVED BALD HEAD, broad square jaw, neatly trimmed short dark full beard, broad shoulders, burnt orange utility jacket over charcoal crewneck shirt, dark apron strap on one shoulder. Cocktail blending technician, shaking specialist.'),
('tea_aroma',64,80,'Distinct adult woman aged about 40, light olive skin, dark black hair gathered in a TALL ROUND HIGH BUN, soft elongated face, no glasses, moss green mandarin-collar blouse with an ivory shawl forming triangular shoulders. Tea aroma evaluator, clear infusion specialist.'),
('tea_blend',64,80,'Distinct mature man aged about 60, deep brown skin, SHORT WAVY IVORY HAIR, rounded face, small round gold-rim eyeglasses, no beard, deep indigo work smock with a sand beige neck scarf, gently rounded shoulders. Tea blending researcher, warm infusion specialist.')]

def generate(spec):
 name,w,h,desc=spec
 raw=ROOT/'provenance'/f'{name}.raw.png'
 if raw.exists():return
 cfg=json.loads((EXT/'package.json').read_text(encoding='utf8'))
 version=json.loads((ROOT/'server-version.json').read_text())['serverPackageVersion']
 req=dict(description=desc+' One original adult cafe research employee HEAD AND SHOULDERS portrait only. Face and neck visible, cropped at shoulder line, no arms or hands. Calm professional expression, frontal or very slight three-quarter face. Crisp chunky cafe-game pixel art matching the supplied style reference ONLY in pixel clusters and three-tone shading, do not copy its face, haircut or bartender uniform. Dark clean outlines #201D29, upper-left lighting, restrained palette. Single complete portrait with two pixel transparent margin around head and shoulders. Genuine transparent background, no background panel, no text, numbers, lettering, badge text, props, drinks, weapons, logos, watermarks, photorealism, blur or gradients. No child, no full body, no hand gestures or sipping.',image_size=dict(width=w,height=h),seed=0,no_background=True,model_name='generate_image_pro_flash',output_format='frames',style_options=dict(color_palette=True,outline=True,detail=True,shading=True))
 style=Image.open(REF).convert('RGBA'); style.thumbnail((min(w,64),min(h,64)),Image.Resampling.NEAREST)
 req['style_image']=dict(image=dict(base64=base64.b64encode(style.tobytes()).decode()),size=dict(width=style.width,height=style.height))
 raw.parent.mkdir(exist_ok=True)
 (raw.parent/f'{name}.request.json').write_text(json.dumps(req,indent=2),encoding='utf8')
 req.update({k:cfg[k] for k in ('secret','tier')});req['version']=version
 ws=websocket.create_connection('wss://api.pixellab.ai/generate-image-pro-flash',timeout=60)
 ws.send(json.dumps(req)); start=time.time();saved=False
 try:
  while time.time()-start<240:
   data=json.loads(ws.recv())
   if 'detail' in data:
    (raw.parent/f'{name}.failure.json').write_text(json.dumps({'detail':data['detail']},indent=2))
    raise RuntimeError(str(data['detail']))
   for im in data.get('images',[])+([data['image']] if data.get('image') else []):
    binary=base64.b64decode(im['base64']);size=(im.get('width',w),im.get('height',h))
    out=Image.open(io.BytesIO(binary)) if binary.startswith(b'\x89PNG') else Image.frombytes('RGBA',size,binary)
    out.save(raw);saved=True
   if data.get('type')=='message_done':
    receipt={k:v for k,v in data.items() if k in ('seed','generation_id','type')}
    receipt.update(model='generate_image_pro_flash',styleReference=str(REF),requestedSize=[w,h],actualProtocolVersion=version)
    (raw.parent/f'{name}.receipt.json').write_text(json.dumps(receipt,indent=2));break
 finally:ws.close()
 if not saved:raise RuntimeError('No image returned')
 print(name,'PixelLab image saved',flush=True)
if __name__=='__main__':
 for spec in SPECS:
  if len(sys.argv)>1 and spec[0]!=sys.argv[1]:continue
  try:generate(spec)
  except Exception as err:print(spec[0],type(err).__name__,str(err)[:220],flush=True);sys.exit(1)

