import pathlib
r=pathlib.Path(__file__).resolve().parent;p=r/'finish.lua';s=p.read_text(encoding='utf-8-sig')
a=s.index('  local height=math.max(5,math.floor(200*t+.5));local top=hinge-height')
b=s.index('  return out\n end\n local closed=',a)
s=s[:a]+'''  local angle=t*math.pi/2
  local top=math.floor(hinge+40*math.cos(angle)-200*math.sin(angle)+.5)
  if top==hinge then top=hinge-1 end
  local ymin,ymax=math.min(top,hinge),math.max(top,hinge)
  for y=ymin,ymax do
   local v=(y-top)/(hinge-top);local sy=math.min(203,math.max(4,4+math.floor(v*199)))
   local width=(376-62*math.sin(angle))*(1-v)+314*v
   local left=192-width/2
   for x=math.ceil(left),math.floor(left+width-1) do
    local sx=35+math.min(313,math.floor((x-left)*314/width))
    local c=im:getPixel(sx,sy)
    if pc.rgbaA(c)>0 then
     if t<.15 then
      if c~=color(1) then c=color(y==ymax and 6 or 4) end
     elseif sx>=56 and sx<324 and sy>=28 and sy<188 and not lit then c=color(1) end
     out:drawPixel(x,y,c)
    end
   end
  end
'''+s[b:]
s=s.replace(" for y=199,203 do for x=35,348 do if pc.rgbaA(closed:getPixel(x,y))>0 then closed:drawPixel(x,y,color(y==199 and 5 or 4)) end end end",'')
s=s.replace("({.04,.12,.28,.50,.74,.92,1})","({0,.12,.28,.50,.74,.92,1})")
p.write_text(s,encoding='utf8')
