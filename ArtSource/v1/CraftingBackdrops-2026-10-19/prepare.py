from pathlib import Path
from PIL import Image,ImageDraw
import shutil, subprocess,json,hashlib
P=Path(__file__).parent
G=Path('C:/Users/user/AppData/Roaming/orca/codex-accounts/b9b7063f-5dd2-4f55-b926-d600f95cb5a0/home/generated_images/01a15360-8909-71f3-b4ea-06199bc19aaa')
source={'bar_wall':'eb593039-9537-4dc4-915a-61cbdaba4f99','tea_wall':'c7bd8040-26b8-4f6b-aaa9-1393aabc4403','bar_table':'8a56c0a8-db0f-472c-94de-aaeae2310e54','tea_table':'0cdfb48b-5334-48d1-b75a-e464e1224292'}
assets=[]
checks=[]
for kind,uid in source.items():
    raw=P/'raw'/f'{kind}_generated.png';shutil.copyfile(G/f'exec-{uid}.png',raw)
    size=(400,287 if 'wall' in kind else 12)
    # Mechanical pixel-grid export only; all illustrated content is generated.
    im=Image.open(raw).convert('RGB').resize(size,Image.Resampling.NEAREST).quantize(colors=48,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGBA')
    name='dl_craft_'+kind
    png=P/(name+'.png');native=P/(name+'.aseprite');im.save(png)
    result=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b',str(png),'--save-as',str(native)],capture_output=True)
    export=P/'raw'/(name+'_native_roundtrip.png')
    rt=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b',str(native),'--save-as',str(export)],capture_output=True)
    equal=export.exists() and Image.open(export).convert('RGBA').tobytes()==im.tobytes()
    checks.append({'id':name,'size_ok':im.size==size,'opaque':im.getchannel('A').getextrema()==(255,255),'native_exists':native.exists(),'native_roundtrip_exact':equal,'aseprite_exit':result.returncode,'roundtrip_exit':rt.returncode})
    assets.append({'id':name,'size':size,'png':png.name,'native':native.name,'opaque':True,'filterMode':'Point','textureCompression':'Uncompressed','mipmaps':False,'wrapMode':'Clamp','pivot':[0.5,0.5],'pixelsPerUnit':100,'integerScale':2,'unityLogicalSize':[size[0]*2,size[1]*2],'sha256':{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in [png,native]}})
V=P.parent/'BeverageCraft-v1.1'
for theme in ['bar','tea']:
    stage=Image.new('RGBA',(800,690),(30,27,25,255))
    stage.alpha_composite(Image.open(P/f'dl_craft_{theme}_wall.png').resize((800,574),Image.Resampling.NEAREST))
    stage.alpha_composite(Image.open(P/f'dl_craft_{theme}_table.png').resize((800,24),Image.Resampling.NEAREST),(0,574))
    for j,color in enumerate([(45,170,240,230),(218,48,79,240),(243,189,38,235),(69,179,109,235)]):
        tool=Image.open(V/'dl_mixing_glass.png').convert('RGBA');mask=Image.open(V/'dl_mixing_glass_mask.png').convert('RGBA').getchannel('A')
        fill=Image.new('RGBA',tool.size,color); d=ImageDraw.Draw(mask); d.rectangle((0,0,tool.width,tool.height//2),fill=0)
        vessel=Image.new('RGBA',tool.size);vessel.paste(fill,(0,0),mask);vessel.alpha_composite(tool)
        vessel=vessel.resize((tool.width*3,tool.height*3),Image.Resampling.NEAREST)
        stage.alpha_composite(vessel,(20+j*140,574-vessel.getbbox()[3]))
    pot=Image.open(V/'dl_teapot.png').convert('RGBA');pot=pot.resize((pot.width*3,pot.height*3),Image.Resampling.NEAREST);stage.alpha_composite(pot,(600,574-pot.getbbox()[3]))
    # HUD is only a QA marker; it is never part of a delivered background.
    d=ImageDraw.Draw(stage);d.text((18,620),'QA HUD RESERVED 800 x 92 - CODE OWNED',fill=(220,216,203))
    stage.save(P/f'preview_{theme}_stage_800x690.png')
    screen=Image.new('RGBA',(1280,720),(43,40,37,255));screen.alpha_composite(stage,(240,15));screen.save(P/f'preview_{theme}_720.png')
    screen.resize((853,480),Image.Resampling.NEAREST).save(P/f'preview_{theme}_480.png')
json.dump({'state':'ready','root_visual_review':'accepted; requested preview alpha-bottom alignment applied','integrated':False,'verified':False,'layout':{'stage':[800,690],'wall':[0,0,800,574],'table':[0,574,800,24],'hud_reserved':[0,598,800,92]},'assets':assets},open(P/'manifest.json','w'),indent=2)
json.dump({'checks':checks,'passed':all(all(c[k] for k in ['size_ok','opaque','native_exists','native_roundtrip_exact']) for c in checks),'runtime_verified':False,'root_visual_review':'accepted','preview_alpha_bottom_y':574,'preview_game_anchor_verified':False},open(P/'qa.json','w'),indent=2)
print(json.dumps(checks))
