from PIL import Image,ImageDraw
import pathlib,json,hashlib
R=pathlib.Path(__file__).resolve().parent;S=R.parent/'BeverageCraft-v1.1'
im=Image.open(S/'dl_teapot.png').convert('RGBA');out=Image.new('RGB',(704,640),(70,65,73));out.paste(im.resize((640,560),Image.Resampling.NEAREST),(40,40),im.resize((640,560),Image.Resampling.NEAREST));d=ImageDraw.Draw(out)
for y in range(0,56,2):d.text((12,40+y*10),str(y),fill='white')
for x in range(0,64,2):d.text((40+x*10,20),str(x),fill='white')
out.save(R/'qa/original-grid.png')
(R/'source-sha.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [S/'dl_teapot.png',S/'dl_teapot.aseprite',S/'dl_teapot_mask.png',S/'manifest.json']},indent=2))
