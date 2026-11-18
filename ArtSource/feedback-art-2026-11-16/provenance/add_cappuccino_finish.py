from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/prepare_finish.py');s=p.read_text(encoding='utf-8');needle='raw=load("dl_foam_layer")'
block='''raw=load("dl_coffee_cappuccino_cup_v2")
if raw then
 local im,t=fit(raw,bbox(raw),32,26,28,23,true)
 local mask=whiteMask(raw,function(x,y)
  if y>=9 and y<=12 then return x>=10 and x<=22 end
  if y==13 then return x>=11 and x<=21 end
  if y==14 then return x>=12 and x<=20 end
  return false end)
 local m=transformMask(mask,t,32,26)
 save("dl_coffee_cappuccino_cup",im,raw,"Second actual PixelLab generation selected for round bowl and separate saucer; native palette/alpha cleanup, uniform crop-fit, bottom contact")
 save("dl_coffee_cappuccino_cup_mask",m,nil,"Native cup-bowl interior; left handle and broad saucer excluded")
 anchor("dl_coffee_cappuccino_cup",m,{17,10},{3,14},32,26)
end
'''
assert needle in s;s=s.replace(needle,block+needle);p.write_text(s,encoding='utf-8')
