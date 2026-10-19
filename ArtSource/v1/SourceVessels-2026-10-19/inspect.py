import pathlib,hashlib,json
from PIL import Image,ImageDraw
r=pathlib.Path('C:/Users/user/Documents/Projects/CocktailCoffeeTea');out=r/'ArtSource/v1/SourceVessels-2026-10-19'
keys=['gin','rum','blue_curacao','grenadine','lemon_juice','honey'];sheet=Image.new('RGB',(6*264,712),(70,65,73));d=ImageDraw.Draw(sheet);rows=[]
for i,k in enumerate(keys):
 p=r/f'ArtSource/v1/BeverageCraft-v1.1/dl_bottle_{k}.png';q=r/f'Assets/_Project/Resources/DrinkLab/dl_bottle_{k}.png';im=Image.open(p).convert('RGBA');z=im.resize((240,560),Image.Resampling.NEAREST);x=i*264+20;sheet.paste(z,(x,48),z)
 for y in range(56):
  if y%2==0:d.text((i*264,48+y*10),str(y),fill='white')
 for v in range(24):
  if v%2==0:d.text((x+v*10,28),str(v),fill='white')
 d.text((i*264,8),k,fill='white');rows.append({'id':'dl_bottle_'+k,'sourceSHA256':hashlib.sha256(p.read_bytes()).hexdigest(),'runtimeSHA256':hashlib.sha256(q.read_bytes()).hexdigest(),'same':p.read_bytes()==q.read_bytes()})
sheet.save(out/'qa/source-pixel-grid.png');(out/'source-comparison.json').write_text(json.dumps(rows,indent=2));print(rows)
