import pathlib
r=pathlib.Path(__file__).resolve().parent;p=r/'finish.lua';s=p.read_text(encoding='utf-8-sig')
s=s.replace(" elseif d[1]=='tree_node' then fill(im,14,14,36,36,color(10))",''' elseif d[1]=='staff_card' then
  for y=4,107 do for x=4,11 do im:drawPixel(x,y,source:getPixel(159-x,y)) end end
  fill(im,12,14,136,84,color(1))
  local portrait=Image(35,43,ColorMode.RGB);portrait:drawImage(source,Point(-4,-21));im:drawImage(portrait,Point(16,24))
  fill(im,18,27,28,34,color(5))
 elseif d[1]=='tree_node' then fill(im,14,14,36,36,color(10))''')
s=s.replace('  im:drawImage(moved,Point(x,y))\n  -- Box foreground', '''  if f>=8 then
   x=({[8]=370,[9]=374,[10]=376})[f]
   y=({[8]=94,[9]=130,[10]=156})[f]
  end
  for py=0,moved.height-1 do for px=0,moved.width-1 do
   if f<8 or y+py<154 then local c=moved:getPixel(px,py);if pc.rgbaA(c)>0 then dot(im,x+px,y+py,c) end end
  end end
  -- Box foreground''')
p.write_text(s,encoding='utf8')
