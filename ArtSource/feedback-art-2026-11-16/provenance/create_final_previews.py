from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
import json,math
r=Path('ArtSource/feedback-art-2026-11-16');p=r/'previews';p.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',13);big=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf',18);small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',11)
def paste(s,im,pos,k=1):
 im=im.convert('RGBA');im=im.resize((im.width*k,im.height*k),Image.Resampling.NEAREST);s.paste(im,pos,im)
def lines(t,maxlen=16): return [t[i:i+maxlen] for i in range(0,len(t),maxlen)]
origins=json.loads((r/'origin-inventory.json').read_text(encoding='utf-8'))['assets']
for gray in [False,True]:
 sheet=Image.new('RGB',(1120,790),'#38313d');d=ImageDraw.Draw(sheet);d.text((16,8),'산지 28종 — '+('흑백 형태 비교 (1× + 3×)' if gray else '최종 원두 아이콘 (1× + 3×)'),font=big,fill='#F4F1E7')
 for i,o in enumerate(origins):
  x=(i%7)*160;y=48+(i//7)*182;im=Image.open(r/'ready'/(o['assetId']+'.png')).convert('RGBA')
  if gray: g=ImageOps.grayscale(im);g=Image.merge('RGBA',(g,g,g,im.getchannel('A')));im=g
  paste(sheet,im,(x+8,y+4));paste(sheet,im,(x+52,y+1),3)
  for j,l in enumerate(lines(o['name'],12)):d.text((x+6,y+104+j*19),l,font=font,fill='#F4F1E7')
  d.text((x+6,y+153),o['ingredientId'][:24],font=small,fill='#B88E65')
 sheet.save(p/('origins-gray.png' if gray else 'origins-color.png'))
ids=['dl_coffee_latte_glass','dl_coffee_flatwhite_cup','dl_coffee_cappuccino_cup'];names=['라떼 유리잔','플랫화이트 낮은 잔','카푸치노 볼 잔 + 받침']
sheet=Image.new('RGB',(1080,750),'#38313d');d=ImageDraw.Draw(sheet);d.text((14,8),'신규 잔 / 마스크 / 채움 참고 — 아트 검사 이미지, Unity 실행 화면 아님',font=big,fill='#F4F1E7')
for i,(n,label) in enumerate(zip(ids,names)):
 x=i*360;im=Image.open(r/'ready'/(n+'.png')).convert('RGBA');mask=Image.open(r/'ready'/(n+'_mask.png')).convert('RGBA');d.text((x+22,50),label,font=big,fill='#F4F1E7');paste(sheet,im,(x+104,85),4);d.text((x+20,235),'빈 잔 · 바닥 가운데 피벗 (0.5, 0)',font=font,fill='#F4F1E7');paste(sheet,mask,(x+104,265),4);d.text((x+20,416),'흰색 = 액체 내부 / 그 외 투명',font=font,fill='#F4F1E7')
 fill=Image.new('RGBA',im.size);rows=[]
 for y in range(mask.height-1,-1,-1):
  xs=[xx for xx in range(mask.width) if mask.getpixel((xx,y))[3]]
  if xs: rows.append((y,xs,len(xs)**2))
 target=sum(z[2] for z in rows)*.82;amount=0;selected=[]
 for y,xs,v in rows:
  if amount>=target:break
  selected.append((y,xs));amount+=v
 if selected:
  top=min(z[0] for z in selected);bottom=max(z[0] for z in selected)
  for y,xs in selected:
   color=(244,231,205,255) if y<top+(bottom-top+1)*.65 else (113,71,50,255)
   if y<=top+1:color=(255,253,244,255)
   for xx in xs:fill.putpixel((xx,y),color)
 composed=Image.alpha_composite(fill,im);paste(sheet,composed,(x+104,456),4);d.text((x+20,608),'약 82% 채움 참고 (마스크 폭² 누적 근사)',font=font,fill='#F4F1E7');d.text((x+20,636),'포말·우유 층 배치/상한은 통합 후 재검수',font=font,fill='#B88E65')
d.text((18,700),'모든 잔의 액체 창은 앞쪽 그림에 가려지지 않도록 투명 처리. 원본·마스크 PNG는 동일 캔버스.',font=font,fill='#F4F1E7');sheet.save(p/'cups-masks-fill.png')
sheet=Image.new('RGB',(900,340),'#38313d');d=ImageDraw.Draw(sheet);d.text((18,10),'거품 32×8 가로 반복 / 잠금 배지 16×16',font=big,fill='#F4F1E7');foam=Image.open(r/'ready/dl_foam_layer.png');tile=Image.new('RGBA',(32*6,8))
for k in range(6):tile.paste(foam,(32*k,0))
for y,color in [(60,'#201D29'),(110,'#EAD4A5'),(160,'#466B45')]:
 d.rectangle((15,y-3,815,y+38),fill=color);paste(sheet,tile,(20,y),4)
lock=Image.open(r/'ready/ui_lock_badge.png');paste(sheet,lock,(20,235),1);paste(sheet,lock,(75,215),6);d.text((210,245),'배경색만으로 잠금을 표시하지 않는 닫힌 자물쇠 형태',font=font,fill='#F4F1E7');sheet.save(p/'foam-repeat-lock.png')
print('Preview artifacts saved:',str(p))
