import pathlib
from PIL import Image,ImageDraw
R=pathlib.Path(__file__).resolve().parent
items=[('original',R.parent/'BeverageCraft-v1.1/dl_teapot.png'),('open body',R/'dl_teapot_open_body.png'),('lid seated coordinates',R/'dl_teapot_lid.png'),('closed composite',R/'qa/closed-composite.png')]
out=Image.new('RGB',(4*400,400),(70,65,73));d=ImageDraw.Draw(out)
for i,(name,p) in enumerate(items):
 im=Image.open(p).convert('RGBA').resize((384,336),Image.Resampling.NEAREST);out.paste(im,(i*400+8,40),im);d.text((i*400+8,12),name,fill='white')
out.save(R/'qa/separation-contact.png')
out=Image.new('RGB',(1152,288),(70,65,73));d=ImageDraw.Draw(out)
for i,name in enumerate(['closed','lifted','side_table']):
 im=Image.open(R/f'qa/preview_{name}.png').convert('RGBA').resize((384,240),Image.Resampling.NEAREST);out.paste(im,(i*384,32),im);d.text((i*384+8,8),name,fill='white')
out.save(R/'qa/lid-movement-contact.png')
