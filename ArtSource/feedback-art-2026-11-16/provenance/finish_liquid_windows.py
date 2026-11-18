from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/prepare_finish.py');s=p.read_text(encoding='utf-8')
s=s.replace('raw=load("dl_coffee_flatwhite_cup")\nif raw then\n local im,t=fit(raw,bbox(raw),32,26,28,20,true)','''raw=load("dl_coffee_flatwhite_cup")
if raw then
 local rawRef=raw
 -- Artist correction: remove three repeated plain body rows, preserving generated rim/base/handle pixels.
 local low=Image(raw.width,raw.height-3,ColorMode.RGB)
 for y=0,low.height-1 do local sy=y<14 and y or y+3 for x=0,low.width-1 do low:drawPixel(x,y,raw:getPixel(x,sy)) end end
 raw=low
 local im,t=fit(raw,bbox(raw),32,26,28,20,true)''')
s=s.replace('if y>=10 and y<=16 then return x>=9 and x<=21 end\n  if y==17 then return x>=10 and x<=20 end\n  if y==18 then return x>=11 and x<=19 end','if y>=10 and y<=13 then return x>=9 and x<=21 end\n  if y==14 then return x>=10 and x<=20 end\n  if y==15 then return x>=11 and x<=19 end')
s=s.replace('save("dl_coffee_flatwhite_cup",im,raw,"PixelLab low wide ceramic cup retained; palette and alpha cleanup, uniform nearest crop-fit to bottom contact")','''for y=0,m.height-1 do for x=0,m.width-1 do if pc.rgbaA(m:getPixel(x,y))>0 then im:drawPixel(x,y,clear) end end end
 save("dl_coffee_flatwhite_cup",im,rawRef,"PixelLab rim/base/handle retained; native artist lowers cup by removing three plain body rows, palette cleanup, uniform crop-fit and transparent liquid window required by renderer")''')
s=s.replace('anchor("dl_coffee_flatwhite_cup",m,{15,11},{27,17},32,26)','anchor("dl_coffee_flatwhite_cup",m,{15,15},{27,19},32,26)')
s=s.replace('save("dl_coffee_cappuccino_cup",im,raw,"Second actual PixelLab generation selected for round bowl and separate saucer; native palette/alpha cleanup, uniform crop-fit, bottom contact")','''for y=0,m.height-1 do for x=0,m.width-1 do if pc.rgbaA(m:getPixel(x,y))>0 then im:drawPixel(x,y,clear) end end end
 save("dl_coffee_cappuccino_cup",im,raw,"Second actual PixelLab generation selected for round bowl and separate saucer; native palette/alpha cleanup, transparent liquid window required by renderer, uniform crop-fit and bottom contact")''')
p.write_text(s,encoding='utf-8')
p=Path('ArtSource/feedback-art-2026-11-16/provenance/validate_assets.py');s=p.read_text(encoding='utf-8-sig');s=s.replace("'outsidePixelCount':len(outside)","'outsidePixelCount':len(outside),'liquidVisibleThroughArt':all(vessel.getpixel((x,y))[3]<=96 for y in range(mask.height) for x in range(mask.width) if mask.getpixel((x,y))[3])");s=s.replace("x['sameCanvas'] and x['maskStrictlyWithinVesselRowEnvelope']","x['sameCanvas'] and x['maskStrictlyWithinVesselRowEnvelope'] and x['liquidVisibleThroughArt']");p.write_text(s,encoding='utf-8')
