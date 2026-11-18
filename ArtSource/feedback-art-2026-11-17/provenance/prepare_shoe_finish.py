from pathlib import Path
import json,subprocess
r=Path('ArtSource/feedback-art-2026-11-17');s=(r/'provenance/draft-hidden.lua').read_text(encoding='utf-8').split('-- Native preview fitting only.')[0]
s+='''
local raw=assert(load("hidden_weekend_rush_steps_v2"))
local fitim=fit(raw,bbox(raw),24,24,20,19,false)
local im=Image(24,24,ColorMode.RGB)
for y=0,23 do for x=0,21 do local c=fitim:getPixel(23-x,y) if pc.rgbaA(c)>0 then im:drawPixel(x+2,y,c) end end end
-- Heel trails are short marks, separate from the sole; never a broom/long shaft.
local dark=rgba(32,29,41) local ivory=rgba(244,241,231)
for _,mark in ipairs({{1,9,3},{1,13,2}}) do for x=mark[1],mark[1]+mark[3]-1 do im:drawPixel(x,mark[2]-1,dark) im:drawPixel(x,mark[2],ivory) im:drawPixel(x,mark[2]+1,dark) end end
for y=0,23 do im:drawPixel(0,y,clear) im:drawPixel(23,y,clear) end
for x=0,23 do im:drawPixel(x,0,clear) im:drawPixel(x,23,clear) end
save("hidden_weekend_rush_steps",im,raw,"Rejected first generated broom-like object. Actual official PixelLab footwear revision retained; native horizontal flip faces movement right, warm/silver palette, sole/lace preservation,20x19 body fit and two short separated heel speed ticks. Not a hue variant.")
local rs=app.open(root.."raw/hidden_weekend_rush_steps_v2.png") rs.layers[1].name="Unmodified actual official PixelLab footwear revision" rs:saveAs(root.."raw/hidden_weekend_rush_steps_v2.aseprite") rs:close()
local f=assert(io.open(root.."provenance/native-finish-shoe.json","w")) f:write(json.encode(records)) f:close()
'''
(r/'provenance/finish-shoe.lua').write_text(s,encoding='utf-8');p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/finish-shoe.lua')],capture_output=True,text=True,timeout=45);assert p.returncode==0;(r/'verification/native-finish-shoe.log').write_text(p.stdout+p.stderr,encoding='utf-8');f=r/'provenance/native-finish-hidden.json';recs=json.loads(f.read_text(encoding='utf-8'));recs=[x for x in recs if x['assetId']!='hidden_weekend_rush_steps'];recs+=json.loads((r/'provenance/native-finish-shoe.json').read_text(encoding='utf-8'));f.write_text(json.dumps(recs,indent=2),encoding='utf-8')
from PIL import Image,ImageDraw,ImageOps
ids=['hidden_iced_cup','hidden_soda_pump','hidden_rhythm_shaker','hidden_bitter_wake','hidden_weekend_rush_steps','hidden_herb_garden','hidden_thick_mug','hidden_bar_tempo','hidden_bar_tempo_move'];sheet=Image.new('RGB',(720,600),'#302b34');d=ImageDraw.Draw(sheet)
for i,id in enumerate(ids):
 x=(i%3)*240;y=(i//3)*200;im=Image.open(r/f'ready/{id}.png').convert('RGBA');big=im.resize((120,120),Image.Resampling.NEAREST);sheet.paste(big,(x+4,y+25),big);d.text((x+4,y+5),id.replace('hidden_',''),fill='white')
 for j,bg in enumerate(['#ece4d4','#46634b','#201d29']):
  b=Image.new('RGBA',(28,28),bg);b.alpha_composite(im,(2,2));sheet.paste(b.convert('RGB'),(x+134,y+25+j*34));sheet.paste(ImageOps.grayscale(b).convert('RGB'),(x+172,y+25+j*34))
 d.text((x+4,y+160),'5x / real24px color+gray',fill='white');d.text((x+4,y+180),'Art proof only / no play claim',fill='#c4bfb6')
sheet.save(r/'previews/hidden-finish-review.png');print('Native shoe finished; all9 hidden sources and preview assembled')
