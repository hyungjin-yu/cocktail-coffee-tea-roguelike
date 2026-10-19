import pathlib
from PIL import Image,ImageDraw
r=pathlib.Path(__file__).resolve().parent;keys=['gin','rum','blue_curacao','grenadine','lemon_juice','honey']
for angle in [0,30,60]:
 sheet=Image.new('RGB',(4*224,6*220),(70,65,73));d=ImageDraw.Draw(sheet)
 for row,k in enumerate(keys):
  for col,pct in enumerate([0,25,50,75]):
   im=Image.open(r/f'qa/dl_bottle_{k}_{angle}deg_{pct}pct.png').convert('RGBA').resize((192,192),Image.Resampling.NEAREST)
   x=col*224+16;y=row*220+24;sheet.paste(im,(x,y),im);d.text((col*224+4,row*220+4),f'{k} {angle}deg {pct}%',fill='white')
 sheet.save(r/f'qa/fill-{angle}deg-contact.png')
