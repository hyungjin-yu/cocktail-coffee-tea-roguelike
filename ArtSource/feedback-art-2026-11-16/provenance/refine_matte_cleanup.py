from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/prepare_finish.py');s=p.read_text(encoding='utf-8')
s=s.replace('local count=0 for _,cc in ipairs(corners) do if cc==c then count=count+1 end end','local count=0 for _,cc in ipairs(corners) do if pc.rgbaA(cc)>127 and math.abs(pc.rgbaR(cc)-pc.rgbaR(c))<=16 and math.abs(pc.rgbaG(cc)-pc.rgbaG(c))<=16 and math.abs(pc.rgbaB(cc)-pc.rgbaB(c))<=16 then count=count+1 end end')
s=s.replace('if visited[key] or im:getPixel(x,y)~=bg then return end','local c=im:getPixel(x,y)\n   if visited[key] or pc.rgbaA(c)<128 or math.abs(pc.rgbaR(c)-pc.rgbaR(bg))>18 or math.abs(pc.rgbaG(c)-pc.rgbaG(bg))>18 or math.abs(pc.rgbaB(c)-pc.rgbaB(bg))>18 then return end')
p.write_text(s,encoding='utf-8')
