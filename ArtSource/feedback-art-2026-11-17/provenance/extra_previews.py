from pathlib import Path
from PIL import Image,ImageDraw,ImageOps
r=Path('ArtSource/feedback-art-2026-11-17');sheet=Image.new('RGB',(720,420),'#302b34');d=ImageDraw.Draw(sheet)
for row,st in enumerate(['fire','bitter','dual']):
 im=Image.open(r/f'ready/vfx_arrow_{st}_tail_00.png').convert('RGBA');d.text((5,5+row*140),st+' 1x / 3x color and gray',fill='white')
 for col,bg in enumerate(['#47634b','#ece4d4','#302b34','#aa845c']):
  b=Image.new('RGBA',(40,24),bg);b.alpha_composite(im,(4,4));g=ImageOps.grayscale(b).convert('RGB');x=col*180;sheet.paste(b.convert('RGB'),(x+5,row*140+23));sheet.paste(g,(x+48,row*140+23));sheet.paste(b.resize((120,72),Image.Resampling.NEAREST).convert('RGB'),(x+5,row*140+48));sheet.paste(g,(x+131,row*140+48))
sheet.save(r/'previews/tail-native-small-gray.png')
# Upright / tilted gravity-level demonstration: clip world-horizontal fill to the registered rotated mask.
sheet=Image.new('RGB',(720,520),'#302b34');d=ImageDraw.Draw(sheet);mug=Image.open(r/'ready/dl_coffee_americano_mug.png').convert('RGBA');mask=Image.open(r/'ready/dl_coffee_americano_mug_mask.png').convert('RGBA')
for row,fraction in enumerate([245/380*.82,.82]):
 for col,angle in enumerate([0,-35,-65]):
  art=Image.new('RGBA',(80,80));art.alpha_composite(mug,(22,22));art=art.rotate(angle,Image.Resampling.NEAREST);m=Image.new('RGBA',(80,80));m.alpha_composite(mask,(22,22));m=m.rotate(angle,Image.Resampling.NEAREST);coords=[(x,y) for y in range(80) for x in range(80) if m.getpixel((x,y))[3]];target=round(len(coords)*fraction);sorted_coords=sorted(coords,key=lambda p:p[1],reverse=True);level=sorted_coords[max(0,target-1)][1];base=Image.new('RGBA',(80,80),'#49634b');fill=Image.new('RGBA',(80,80));pix=fill.load()
  for x,y in coords:
   if y>=level:pix[x,y]=(178,122,74,255) if y==level else (88,49,34,255)
  base.alpha_composite(fill);base.alpha_composite(art);sheet.paste(base.resize((240,240),Image.Resampling.NEAREST).convert('RGB'),(col*240,row*255+15));d.text((col*240+4,row*255+1),f'{abs(angle)}deg / illustrative area {fraction:.3f}',fill='white')
sheet.save(r/'previews/americano-tilted-levels.png')
