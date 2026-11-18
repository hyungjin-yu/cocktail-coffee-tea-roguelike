from PIL import Image,ImageDraw
from pathlib import Path
r=Path('ArtSource/feedback-art-2026-11-16'); sheet=Image.new('RGB',(640,300),'#38313d'); d=ImageDraw.Draw(sheet)
for i,n in enumerate(['dl_coffee_latte_glass','dl_coffee_flatwhite_cup']):
 im=Image.open(r/'raw'/(n+'.png')).convert('RGBA'); sheet.paste(im.resize((im.width*8,im.height*8),Image.Resampling.NEAREST),(i*320,30),im.resize((im.width*8,im.height*8),Image.Resampling.NEAREST)); d.text((i*320+4,8),n,fill='white'); print(n,im.size,im.getbbox(),len(im.getcolors(10000)))
sheet.save(r/'provenance/raw-probe-preview.png')
