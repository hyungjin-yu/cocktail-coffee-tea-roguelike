from pathlib import Path
r=Path('ArtSource/feedback-art-2026-11-17');s=(r/'provenance/draft-hidden.lua').read_text(encoding='utf-8').split('-- Native preview fitting only.')[0]
s+='''
-- All edits below execute in Aseprite's native Image API on actual generated imagery.
local C={d=rgba(32,29,41),s=rgba(83,84,111),g=rgba(167,177,194),w=rgba(244,241,231),p=rgba(234,212,165),b=rgba(113,71,50),t=rgba(184,142,101),l=rgba(220,174,114),v=rgba(70,107,69),h=rgba(142,173,104),i=rgba(151,200,215),j=rgba(208,237,239),["."]=clear}
local function stamp(im,x,y,rows) for yy,row in ipairs(rows) do for xx=1,#row do local k=row:sub(xx,xx) if k~=" " then im:drawPixel(x+xx-1,y+yy-1,assert(C[k],k)) end end end end
local function rect(im,x,y,w,h,color) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and xx<im.width and yy>=0 and yy<im.height then im:drawPixel(xx,yy,color) end end end end
local function line(im,x0,y0,x1,y1,c)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0) local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1 local e=dx+dy
 while true do if x0>=0 and y0>=0 and x0<im.width and y0<im.height then im:drawPixel(x0,y0,c) end if x0==x1 and y0==y1 then break end local e2=2*e if e2>=dy then e=e+dy x0=x0+sx end if e2<=dx then e=e+dx y0=y0+sy end end
end
local ids={"hidden_iced_cup","hidden_soda_pump","hidden_rhythm_shaker","hidden_bitter_wake","hidden_herb_garden","hidden_thick_mug","hidden_bar_tempo","hidden_bar_tempo_move"}
for _,id in ipairs(ids) do
 local raw=assert(load(id)) local im=fit(raw,bbox(raw),24,24,22,22,false)
 local note="Generated object silhouette/material retained; native palette, alpha cleanup and nearest-neighbour 22px fit. "
 if id=="hidden_iced_cup" then
  -- Remove generated straw/cream and replace with angular ice; frame is an actual protection shield.
  rect(im,0,0,24,8,clear) rect(im,15,0,9,11,clear)
  local shield=Image(24,24,ColorMode.RGB)
  stamp(shield,5,7,{" dwwwwwwwwwwd ","dwjjjjjjjjjjwd","dwj        jwd","dwj        jwd","dwj        jwd","dwj        jwd","dwj        jwd"," dwj      jwd "," dwj      jwd ","  dwj    jwd  ","   dwj  jwd   ","    dwjjwd    ","     dwwd     ","      dd      "})
  shield:drawImage(im) im=shield
  stamp(im,8,3,{" dddd ","dwjjid","dwjiid","diiisd"," dddd "})
  stamp(im,12,6,{" dddd ","dwjjid","dwiisd"," dissd"," dddd "})
  note=note.."Straw/cream removed, angular ice and broad ivory shield edge added so it reads protection rather than a dessert drink."
 elseif id=="hidden_soda_pump" then
  rect(im,8,0,8,7,clear)
  stamp(im,8,2,{" dddddddd ","dwgggggggd"," dddsdd gd","    sd  gd","   dggd dd","   dwwd   "})
  stamp(im,1,11,{"   d  ","dddwdd","dwggwd","dddwd ","   d  "})
  stamp(im,17,14,{"  d   "," ddwdd","dwggwd"," ddd d","  d   "})
  note=note.."Bottle retained; pump plunger, curved outlet and two inward attraction arrows natively added."
 elseif id=="hidden_rhythm_shaker" then
  -- A metal cap/shoulder replaces the generated tiny bottle stopper.
  rect(im,5,0,15,7,clear)
  stamp(im,9,2,{" dddd ","dwggsd","dwwgsd"," dddd ","ddggsdd"})
  stamp(im,2,6,{" dd "," dw "," dd ","    ","dd  ","wwd ","dd  "})
  stamp(im,18,8,{" dd "," ww "," dd ","    ","dd  ","wwd ","dd  "})
  note=note.."Metal cap/shoulders and separated beat ticks replace bottle-like neck; shaker body retained."
 elseif id=="hidden_bitter_wake" then
  -- Close the accidental crescent notch into one roasted bean and engrave an alert eye.
  rect(im,12,9,9,10,C.b) line(im,20,9,20,18,C.d) line(im,19,18,17,20,C.d)
  stamp(im,7,9,{"   dddd   "," ddwwwwdd ","dwwdwddwwd"," ddwwwwdd ","   dddd   "})
  line(im,12,14,10,19,C.d) line(im,11,15,9,19,C.t)
  stamp(im,8,1,{"  dd    dd "," dw wd dw wd","dd  dddd  dd"})
  note=note.."Crescent notch repaired into bean, strong central eye seam and upward alert chevrons added."
 elseif id=="hidden_herb_garden" then
  stamp(im,9,16,{"dd dd","wwdww","wwwww"," dwd ","  d  "})
  note=note.."Retained leaf planter; compact ivory heart on pot distinguishes healing from a generic herb ingredient."
 elseif id=="hidden_thick_mug" then
  stamp(im,9,13,{" dd dd ","dwwdwwd","dwwwwwd"," dwwwd ","  dwd  ","   d   "})
  note=note.."Broad generated ceramic rim/handle retained, heart emblem reinforced for vitality at24px."
 elseif id=="hidden_bar_tempo" or id=="hidden_bar_tempo_move" then
  -- Preserve wooden triangular body; draw an unmistakable metronome dial/pendulum over it.
  rect(im,10,7,4,10,C.d) rect(im,11,8,2,8,C.s)
  line(im,12,4,12,16,C.w)
  if id=="hidden_bar_tempo" then
   stamp(im,10,11,{"ddd","dwd","ddd"})
   stamp(im,8,17,{"dd dd","ww ww","ww ww","dd dd"})
   note=note.."Wooden body retained; vertical ivory pendulum, weight and paired pause marks define stationary defensive tempo."
  else
   line(im,12,5,17,14,C.d) line(im,11,5,16,14,C.w)
   stamp(im,14,11,{" ddd ","dwwgd"," ddd "})
   stamp(im,18,5,{"dd  "," ww ","  dd","  ww"," dd "})
   note=note.."Wooden body retained; diagonal ivory pendulum/weight and outward ticks define moving tempo, not just a recolor."
  end
 end
 if id=="hidden_iced_cup" then
  rect(im,8,20,10,3,clear)
  line(im,5,8,5,14,C.d) line(im,6,8,6,15,C.w)
  line(im,6,16,11,21,C.d) line(im,7,16,12,21,C.w)
  line(im,18,9,18,14,C.d) line(im,17,10,17,15,C.w)
  line(im,17,16,12,21,C.d) line(im,16,16,12,20,C.w)
 elseif id=="hidden_rhythm_shaker" then
  for y=0,23 do for x=0,23 do local c=im:getPixel(x,y) if c==C.v then im:drawPixel(x,y,C.s) elseif c==C.h then im:drawPixel(x,y,C.g) end end end
 end
 -- Delivery margins are always clear, and edits do not introduce antialiasing.
 rect(im,0,0,24,1,clear) rect(im,0,23,24,1,clear) rect(im,0,0,1,24,clear) rect(im,23,0,1,24,clear)
 save(id,im,raw,note)
 local rs=app.open(root.."raw/"..id..".png") rs.layers[1].name="Unmodified actual official PixelLab output" rs:saveAs(root.."raw/"..id..".aseprite") rs:close()
end
local f=assert(io.open(root.."provenance/native-finish-hidden.json","w")) f:write(json.encode(records)) f:close()
'''
(r/'provenance/finish-hidden.lua').write_text(s,encoding='utf-8')
import subprocess
p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/finish-hidden.lua')],capture_output=True,text=True,timeout=45);(r/'verification/native-finish-hidden.log').write_text(p.stdout+p.stderr,encoding='utf-8');print('Native hidden finish',p.returncode,p.stdout[-300:]);assert p.returncode==0
from PIL import Image,ImageDraw,ImageOps
ids=['hidden_iced_cup','hidden_soda_pump','hidden_rhythm_shaker','hidden_bitter_wake','hidden_herb_garden','hidden_thick_mug','hidden_bar_tempo','hidden_bar_tempo_move'];sheet=Image.new('RGB',(8*150,340),'#303039');d=ImageDraw.Draw(sheet)
for i,id in enumerate(ids):
 im=Image.open(r/'ready'/f'{id}.png').convert('RGBA');sheet.paste(im.resize((144,144),Image.Resampling.NEAREST),(i*150,0),im.resize((144,144),Image.Resampling.NEAREST));d.text((i*150+2,146),id.replace('hidden_',''),fill='white')
 for row,bg in enumerate(['#ebe4d5','#44624b','#302b34']):
  b=Image.new('RGBA',(28,28),bg);b.alpha_composite(im,(2,2));sheet.paste(b.convert('RGB'),(i*150+3,170+row*34));g=ImageOps.grayscale(b).convert('RGB');sheet.paste(g,(i*150+40,170+row*34))
 d.text((i*150+2,285),'1x/color+gray',fill='white')
sheet.save(r/'previews/hidden-finish-review.png')
