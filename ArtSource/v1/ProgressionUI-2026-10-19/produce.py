import base64,io,json,os,pathlib,sys,time,websocket
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parent
EXT=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'
PROJECT=ROOT.parents[2]
REF=PROJECT/'ArtSource/card-education-2026-10-16/card_gin.png'
SPECS=[
('ingredient_card',128,80,'A single blank warm cream paper ingredient information card, landscape rectangle, thick dark brown pixel outline, small dog-eared upper right corner, restrained two-tone paper edges. Completely clean broad blank writing area without any marks, symbols, stamps, lines or icons. Straight-on flat UI view.'),
('receipt',128,192,'A single narrow long blank ivory thermal paper receipt, straight vertical sides, tiny regular zigzag perforated top and bottom edges, subtle warm shaded paper edge only. Broad unmarked clean blank central writing space. No printed marks or lines. Straight-on flat UI view.'),
('spring_notebook',192,160,'One small spiral-bound cream recipe notepad, straight-on flat view, bronze metal spiral binding only across top edge, warm dark brown backing, blank pale cream paper page with broad clean writing area. Fine small perforation holes below spiral, NO horizontal writing lines, NO marks, NO icons.'),
('recipe_storage',160,112,'One shallow cafe recipe index card storage box, warm walnut brown, horizontal landscape shape, open top holding three cream index card edges with staggered blank tabs, front cream label panel completely blank. Ground-level three-quarter side view, modest visible top, no table.'),
('laptop_open',256,192,'One open cafe laptop computer, centered front view with slight visible keyboard deck, large broad rectangular completely blank screen occupying upper 80 percent, thin dark plum outer outline and silver metal bezel, muted teal screen inner surface empty, thin keyboard deck with compact subtle keys below. Screen upright, lower hinge axis horizontal, both corners symmetric. No logo, no text, no icons, no room.'),
('laptop_closed',256,192,'One closed cafe laptop computer, centered front view with slightly visible lid top, dark plum outline and three-tone silver metal lid, thin horizontal hinge at back and matching compact keyboard base. Symmetric, no logo, no screen visible, no other objects.'),
('tree_node',64,64,'One blank square cafe game skill tree node frame, dark plum two-pixel outer outline, silver three-tone beveled edge, deep warm brown flat square empty center, small clipped corners. No icon, no letter, no status marks. Straight-on flat UI view.'),
('staff_card',160,112,'One blank landscape employee research card UI frame, dark plum outer border, cream and silver three-tone raised edging, broad clean pale cream writing area, a single empty square inset portrait opening on left and generous blank text space right, no portrait, no people, no text, no icons. Straight-on flat UI view.'),
('button',96,32,'One blank wide short rectangular cafe game button, dark plum crisp outer outline, warm cream three-tone beveled raised surface, small clipped corners, clean blank center. No text, no icons, no marks. Straight-on flat UI view.')]

def generate(spec):
 name,w,h,desc=spec
 raw=ROOT/'provenance'/f'{name}.raw.png'
 if raw.exists():return
 cfg=json.loads((EXT/'package.json').read_text(encoding='utf8'))
 version=json.loads((ROOT/'server-version.json').read_text())['serverPackageVersion']
 req=dict(description=desc+' Crisp chunky pixel art matching supplied cafe-game style image. Dark clean outline #201D29, paper #EAD4A5, shade #B88E65, metal #A7B1C2. Three tones per material. Upper-left lighting. Exactly one whole isolated object, 3 pixel transparent margin. Genuine transparent background outside object, no baked checkerboard. No hands, arms, faces, people, body parts, character, text, numbers, lettering, gradients, blur, shadows, floor, watermark.',image_size=dict(width=w,height=h),seed=0,no_background=True,model_name='generate_image_pro_flash',output_format='frames',style_options=dict(color_palette=True,outline=True,detail=True,shading=True))
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

