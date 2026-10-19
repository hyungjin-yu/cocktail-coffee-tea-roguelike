import pathlib
from PIL import Image,ImageDraw
r=pathlib.Path(__file__).resolve().parent
keys=['gin','rum','blue_curacao','grenadine','lemon_juice','honey'];s=Image.new('RGB',(6*160,3*390),(70,65,73));d=ImageDraw.Draw(s)
for i,k in enumerate(keys):
 for j,suf in enumerate(['empty','mask','open_empty']):
  im=Image.open(r/f'dl_bottle_{k}_{suf}.png').convert('RGBA').resize((144,336),Image.Resampling.NEAREST);x=i*160+8;y=j*390+40;s.paste(im,(x,y),im);d.text((i*160+4,j*390+8),k+' '+suf,fill='white')
s.save(r/'qa/separation-contact.png')
