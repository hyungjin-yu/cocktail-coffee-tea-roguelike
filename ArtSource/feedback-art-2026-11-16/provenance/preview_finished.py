from pathlib import Path
from PIL import Image,ImageDraw
r=Path('ArtSource/feedback-art-2026-11-16'); files=list((r/'ready').glob('*.png')); files.sort(); w=1000;cellw=200;cellh=210;sheet=Image.new('RGB',(w,((len(files)+4)//5)*cellh),'#38313d');d=ImageDraw.Draw(sheet)
for i,f in enumerate(files):
 x=(i%5)*cellw;y=(i//5)*cellh;im=Image.open(f).convert('RGBA');up=im.resize((im.width*4,im.height*4),Image.Resampling.NEAREST);sheet.paste(up,(x+(cellw-up.width)//2,y+14),up);d.text((x+4,y+166),f.stem.replace('dl_icon_','').replace('dl_coffee_','')[:25],fill='#F4F1E7');d.text((x+4,y+184),str(im.size),fill='#B88E65')
sheet.save(r/'provenance/first-finished-preview.png')
