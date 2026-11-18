from PIL import Image,ImageDraw
from pathlib import Path
r=Path('ArtSource/feedback-art-2026-11-16'); ns=['dl_coffee_cappuccino_cup','dl_foam_layer','ui_lock_badge']; sheet=Image.new('RGB',(960,310),'#38313d');d=ImageDraw.Draw(sheet)
for i,n in enumerate(ns):
 f=r/'raw'/(n+'.png')
 if not f.exists(): continue
 im=Image.open(f).convert('RGBA');up=im.resize((im.width*8,im.height*8),Image.Resampling.NEAREST);sheet.paste(up,(i*320,30),up);d.text((i*320+4,8),n,fill='white');print(n,im.size,im.getbbox())
sheet.save(r/'provenance/raw-cup-foam-lock-preview.png')
