import pathlib,math
from PIL import Image,ImageDraw
r=pathlib.Path(__file__).resolve().parent
q=r/'qa';q.mkdir(exist_ok=True)
for aid in ['laptop_opening','laptop_closing','laptop_sequence','recipe_tear_store']:
 paths=sorted((r/'assets'/aid/'frames').glob('*.png'))
 if not paths:continue
 tw,th=(256,112) if aid=='recipe_tear_store' else (192,144)
 cols=5 if aid in ['laptop_sequence','recipe_tear_store'] else 4
 out=Image.new('RGB',(cols*(tw+12),math.ceil(len(paths)/cols)*(th+30)),(50,45,53));d=ImageDraw.Draw(out)
 for i,p in enumerate(paths):
  im=Image.open(p).convert('RGBA');im.thumbnail((tw,th),Image.Resampling.NEAREST)
  x=(i%cols)*(tw+12)+6;y=(i//cols)*(th+30)+24
  out.paste(im,(x+(tw-im.width)//2,y),im);d.text((x,y-18),f'{aid} {i:02d} 50ms',fill='white')
 out.save(q/(aid+'-contact.png'))
static=sorted(p for p in (r/'assets').glob('*/*.png'))
out=Image.new('RGB',(800,math.ceil(len(static)/4)*190),(50,45,53));d=ImageDraw.Draw(out)
for i,p in enumerate(static):
 im=Image.open(p).convert('RGBA');im.thumbnail((184,154),Image.Resampling.NEAREST)
 x=(i%4)*200+8;y=(i//4)*190+24;out.paste(im,(x,y),im);d.text((x,y-18),p.stem,fill='white')
out.save(q/'static-contact.png')
