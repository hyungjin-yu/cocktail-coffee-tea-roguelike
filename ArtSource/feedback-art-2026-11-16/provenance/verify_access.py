import json,pathlib,hashlib,struct
from PIL import Image,ImageChops
root=pathlib.Path.cwd(); out=root/'ArtSource/feedback-art-2026-11-16'; p=out/'provenance'
refs=['ArtSource/coffee-brew-2026-10-21/raw/dl_coffee_cup.png','ArtSource/coffee-brew-2026-10-21/raw/dl_coffee_mug.png','ArtSource/coffee-brew-2026-10-21/raw/dl_coffee_cup_mask.png','ArtSource/coffee-brew-2026-10-21/raw/dl_coffee_mug_mask.png','Assets/_Project/Resources/DrinkLab/dl_icon_coffee_bean.png','Assets/_Project/Resources/ProgressionUI/tree_node_locked.png']
checks=[]
for name in refs:
 f=root/name; im=Image.open(f).convert('RGBA'); a=im.getchannel('A'); checks.append(dict(path=name,size=list(im.size),alpha_bounds=a.getbbox(),alpha_values=sorted(set(a.getdata())),sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
ase=root/'ArtSource/coffee-brew-2026-10-21/aseprite/dl_coffee_cup.aseprite'; b=ase.read_bytes()
roundtrip=Image.open(p/'reference-cup-roundtrip.png').convert('RGBA'); original=Image.open(root/refs[0]).convert('RGBA')
record={'aseprite':{'executable':'C:/Program Files/Aseprite/Aseprite.exe','native_source':str(ase.relative_to(root)),'header_magic':hex(struct.unpack_from('<H',b,4)[0]),'frames':struct.unpack_from('<H',b,6)[0],'size':list(struct.unpack_from('<HH',b,8)),'batch_exit_code':0,'roundtrip_pixels_equal':roundtrip.tobytes()==original.tobytes(),'extension_warning':"handle-pose.lua:58: attempt to index a nil value (local 'dlg')"},'pixellab':{'installed_version':'0.4.130','credential_present':True,'endpoint':'wss://api.pixellab.ai/generate-style','actual_request_asset':'dl_coffee_latte_glass','result':'rejected','exact_error':'Your PixelLab version is outdated. Please update','process_exit_code':1,'images_returned':0,'fallback_used':False},'references':checks}
(p/'access-and-reference-checks.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
assets=['dl_coffee_latte_glass','dl_coffee_latte_glass_mask','dl_coffee_flatwhite_cup','dl_coffee_flatwhite_cup_mask','dl_coffee_cappuccino_cup','dl_coffee_cappuccino_cup_mask','dl_foam_layer','ui_lock_badge','dl_icon_coffee_<origin>']
(out/'manifest.blocked.json').write_text(json.dumps({'status':'blocked','ready':False,'ready_assets':[],'reason':record['pixellab']['exact_error'],'assets':[{'assetId':a,'status':'blocked','ready':False,'png':None,'aseprite':None} for a in assets],'evidence':'provenance/access-and-reference-checks.json','note':'Reference roundtrip PNG is diagnostic only, never an import deliverable. Origin inventory and final pivots remain pending.'},indent=2),encoding='utf-8')
print(json.dumps(record,indent=2))
