"""Actual PixelLab installed extension protocol. Pillow only decodes; never draws."""
import base64,io,json,os,pathlib,sys,time
import websocket
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parent
EXT=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
def run(spec):
    name=spec['file']; cfg=json.loads((EXT/'package.json').read_text(encoding='utf-8'))
    ref=ROOT/'reference'/spec.get('reference','card_jigger.png')
    style=Image.open(ref).convert('RGBA')
    req=dict(description=spec['description']+' Single static cafe inventory teaching icon. Match the provided reference pixel art: dark charcoal outline, muted warm ivory, sage and silver palette, three tones per material, upper left light, ground level three quarter side view. Centered complete objects with 5 pixel transparent margin. No text, letters, numbers, border, floor, glow, halo or background.',image_size=dict(width=64,height=64),seed=0,no_background=True,model_name='generate_image_pro_flash',output_format='frames',style_options=dict(color_palette=True,outline=True,detail=True,shading=True))
    req['style_image']=dict(image=dict(base64=base64.b64encode(style.tobytes()).decode()),size=dict(width=64,height=64))
    (ROOT/'raw'/f'{name}.request.json').write_text(json.dumps(req,indent=2),encoding='utf-8')
    req.update({k:cfg[k] for k in ['secret','tier','version']})
    ws=websocket.create_connection('wss://api.pixellab.ai/generate-image-pro-flash',timeout=90)
    ws.send(json.dumps(req)); start=time.time(); final=None; events=[]
    try:
        while time.time()-start<300:
            data=json.loads(ws.recv())
            if 'detail' in data: raise RuntimeError(str(data['detail']))
            imgs=data.get('images',[])+([data['image']] if data.get('image') else [])
            for im in imgs:
                raw=base64.b64decode(im['base64']); size=(im.get('width',64),im.get('height',64))
                final=Image.open(io.BytesIO(raw)) if raw.startswith(b'\x89PNG') else Image.frombytes('RGBA',size,raw)
            events.append({k:v for k,v in data.items() if k in ['type','seed','generation_id','progress','message']})
            if data.get('type')=='message_done':
                if final is None: raise RuntimeError('Completed without image')
                final.save(ROOT/'raw'/f'{name}.png')
                (ROOT/'raw'/f'{name}.receipt.json').write_text(json.dumps(dict(endpoint='generate-image-pro-flash',reference=str(ref),events=events),indent=2),encoding='utf-8')
                print(name,'PixelLab complete',flush=True); return
        raise RuntimeError('PixelLab did not finish')
    finally: ws.close()
if __name__=='__main__':
    specs=json.loads((ROOT/'specs.json').read_text(encoding='utf-8'))
    wanted=sys.argv[1:]
    for spec in specs:
        if wanted and spec['file'] not in wanted: continue
        if (ROOT/'raw'/f"{spec['file']}.png").exists(): continue
        try: run(spec)
        except Exception as exc:
            print(spec['file'],type(exc).__name__,str(exc)[:300],flush=True)
            (ROOT/'evidence'/'generation-error.txt').write_text(spec['file']+' '+str(exc),encoding='utf-8')
            sys.exit(1)
