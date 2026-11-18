from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/extract-latte.lua')
p.write_text('''local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-16/"
local spr=app.open(root.."pixellab-native-raw.aseprite")
local img=Image(spr.width,spr.height)
img:drawSprite(spr,#spr.frames)
local one=Sprite(spr.width,spr.height,ColorMode.RGB)
one:newCel(one.layers[1],1,img,Point(0,0))
one:saveAs(root.."raw/dl_coffee_latte_glass.aseprite")
one:saveCopyAs(root.."raw/dl_coffee_latte_glass.png")
''',encoding='utf-8')
import subprocess
r=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(p)],capture_output=True,text=True,timeout=25)
print(r.stdout);print(r.stderr);print(r.returncode)
