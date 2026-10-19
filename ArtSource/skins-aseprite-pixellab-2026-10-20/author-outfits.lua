local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-20/'
local project='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local anatomy=dofile(root..'anatomy-contract.lua')
local pc=app.pixelColor
local function C(r,g,b,a)return pc.rgba(r,g,b,a or 255)end
local ink=C(36,29,40);local cream=C(233,221,194);local silver=C(189,198,211);local gold=C(213,167,81)
local designs={
 rail={job='bartender',id='skin_bartender_rail',pal={C(74,30,46),C(108,43,64),C(142,63,84)},sleeves=true},
 tropic={job='bartender',id='skin_bartender_tropic',pal={C(150,61,61),C(222,104,92),C(246,153,123)},sleeves=true},
 noir={job='bartender',id='skin_bartender_noir',pal={C(70,61,62),C(119,103,91),C(162,143,119)},sleeves=true},
 alchemist={job='bartender',id='skin_bartender_alchemist',pal={C(58,35,78),C(100,61,127),C(150,103,163)},sleeves=true},
 stage={job='bartender',id='skin_bartender_stage',pal={C(26,25,36),C(44,41,55),C(76,71,88)},sleeves=true,gloves=true},
 mountain={job='tea_sommelier',id='skin_tea_mountain',pal={C(49,66,40),C(87,111,62),C(135,155,91)},sleeves=false},
 ceremony={job='tea_sommelier',id='skin_tea_ceremony',pal={C(153,147,137),C(218,211,190),C(249,242,220)},sleeves=true},
 apiary={job='tea_sommelier',id='skin_tea_apiary',pal={C(151,125,78),C(218,198,144),C(248,230,181)},sleeves=true,gloves=true},
 greenhouse={job='tea_sommelier',id='skin_tea_greenhouse',pal={C(113,56,46),C(174,88,65),C(213,133,87)},sleeves=false},
 astronomer={job='tea_sommelier',id='skin_tea_astronomer',pal={C(39,36,80),C(69,59,123),C(112,98,163)},sleeves=true}
}
local order={'rail','tropic','noir','alchemist','stage','mountain','ceremony','apiary','greenhouse','astronomer'}
local rail=Image{fromFile=root..'evidence/rail-pixellab-output.png'}
local function skin(c)
 local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
 return pc.rgbaA(c)>0 and r>150 and g>80 and g<205 and b>40 and b<165 and r>g*1.18 and g>b*1.1
end
local function masked(im,x,y)
 local c=im:getPixel(x,y)
 if im.colorMode==ColorMode.GRAY then return pc.grayaA(c)>0 and pc.grayaV(c)>0 end
 return pc.rgbaA(c)>0 and pc.rgbaR(c)>0
end
for _,key in ipairs(order) do if app.params.design==nil or app.params.design==key then
 local d=designs[key];local folder=root..'body/'..d.id..'/'
 app.fs.makeAllDirectories(folder)
 local count=0;for _,a in ipairs(anatomy)do if a.job==d.job then count=count+1 end end
 local contact=Image(1536,math.ceil(count/6)*512,ColorMode.RGB)
 local reps=Image(1536,512,ColorMode.RGB);local repn=0
 local log=assert(io.open(root..'evidence/'..key..'-native-readback.log','w'))
 local num=0
 for _,a in ipairs(anatomy)do if a.job==d.job then
  num=num+1
  local original=Image{fromFile=a.source};local body=Image(original);local shapes=Image(original.spec);local hats=Image(original.spec);local back=Image(original.spec)
  local idx=tonumber(a.filename:match('_(%d+)%.png$'))
  local clip=a.filename:match('^[^_]+_(%a+)_right_')
  if d.job=='tea_sommelier' then clip=a.filename:match('tea_sommelier_(.-)_right_') end
  local head=a.headSkinBounds;local hx,hy=(head[1]+head[3])/2,(head[2]+head[4])/2
  local cx,cy,angle=128,137,0
  local bob=0
  if clip=='idle' then bob=idx>=2 and -2 or 0
  elseif clip=='move' then bob=({0,2,-2,0,2,-2})[idx+1] end
  cy=cy+bob
  if clip=='hurt' then cx,cy,angle=125,146,0.15
  elseif clip=='melee' then local p=({{125,140,-.05},{129,135,-.10},{120,143,.20},{125,140,-.05}})[idx+1];cx,cy,angle=p[1],p[2],p[3]
  elseif clip=='ranged' then local p=({{124,143,.08},{124,138,.15},{124,139,0},{128,141,0}})[idx+1];cx,cy,angle=p[1],p[2],p[3]
  elseif clip=='defeat' then local p=({{116,149,.47},{123,176,.61},{136,197,1.12},{145,202,1.57}})[idx+1];cx,cy,angle=p[1],p[2],p[3] end
  if d.job=='tea_sommelier' then
   cy=cy-8;cx=cx-3
   if clip=='defeat' then local p=({{127,167,.20},{128,180,-1.0},{147,194,-1.4},{137,194,-1.57}})[idx+1];cx,cy,angle=p[1],p[2],p[3]
    if idx==1 then head={82,132,119,168};hx,hy=100.5,150 elseif idx==3 then head={74,174,113,205};hx,hy=93.5,192 end
   elseif clip=='hurt' then cx,cy,angle=130,161,.35
   elseif clip=='sword_swing' then cx,cy,angle=84+(hx-87.5)*.7,149,0 end
  end
  local co,si=math.cos(angle),math.sin(angle)
  local function localXY(x,y)local dx,dy=x-cx,y-cy;return dx*co+dy*si,-dx*si+dy*co end
  local active=shapes
  local function put(x,y,c)
   x=math.floor(x+.5);y=math.floor(y+.5)
   if x>=1 and x<=254 and y>=1 and y<=254 then active:drawPixel(x,y,c) end
  end
  local function dot(x,y,c)put(cx+x*co-y*si,cy+x*si+y*co,c)end
  local function poly(points,color)
   local minx,miny,maxx,maxy=999,999,-999,-999
   for _,p in ipairs(points)do local wx,wy=cx+p[1]*co-p[2]*si,cy+p[1]*si+p[2]*co;minx=math.min(minx,wx);maxx=math.max(maxx,wx);miny=math.min(miny,wy);maxy=math.max(maxy,wy)end
   for y=math.floor(miny),math.ceil(maxy)do for x=math.floor(minx),math.ceil(maxx)do
    local tx,ty=(x-cx)*co+(y-cy)*si,-(x-cx)*si+(y-cy)*co
    local hit=false;local j=#points
    for i=1,#points do local p,q=points[i],points[j];if (p[2]>ty)~=(q[2]>ty) and tx<(q[1]-p[1])*(ty-p[2])/(q[2]-p[2])+p[1]then hit=not hit end;j=i end
    if hit then put(x,y,color)end
   end end
  end
  local function rect(x,y,w,h,color)poly({{x,y},{x+w,y},{x+w,y+h},{x,y+h}},color)end
  local function line(x1,y1,x2,y2,color,width)
   local steps=math.max(math.abs(x2-x1),math.abs(y2-y1));for i=0,steps do local t=steps==0 and 0 or i/steps;for oy=0,(width or 1)-1 do dot(x1+(x2-x1)*t,y1+(y2-y1)*t+oy,color)end end
  end
  local function ellipse(x,y,rx,ry,color)
   local points={};for i=0,31 do local t=i*math.pi/16;table.insert(points,{x+rx*math.cos(t),y+ry*math.sin(t)})end;poly(points,color)
  end
  local dark,mid,light=d.pal[1],d.pal[2],d.pal[3]
  -- Re-materialize articulated source sleeves; actual new garment construction is drawn below on separate layers.
  local foot,maxDown=0,-999
  for y=0,255 do for x=0,255 do if pc.rgbaA(original:getPixel(x,y))>0 then foot=math.max(foot,y);local _,ly=localXY(x,y);maxDown=math.max(maxDown,ly)end end end
  for y=0,255 do for x=0,255 do local c=original:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c);local lx,ly=localXY(x,y)
   local inHead=x>=head[1]-12 and x<=head[3]+9 and y>=head[2]-24 and y<=head[4]
   if pc.rgbaA(c)>0 and not inHead then
    if d.sleeves and not skin(c) and ly<28 and ly> -40 and r>130 and g>125 and b>105 and math.max(r,g,b)-math.min(r,g,b)<65 then body:drawPixel(x,y,(r+g+b)>620 and light or ((r+g+b)>450 and mid or dark))end
    if ly>23 and not skin(c) then
     if key=='tropic' then
      if ly<52 then body:drawPixel(x,y,(r+g+b)>180 and C(43,139,142) or C(26,83,94))
      elseif y<foot-7 then body:drawPixel(x,y,(r+g+b)>190 and C(218,160,112) or C(165,111,78))
      else body:drawPixel(x,y,(y>=foot-1 or (y>=foot-7 and y<=foot-5))and C(57,43,36)or C(202,145,99))end
     elseif key=='apiary' then body:drawPixel(x,y,(r+g+b)>170 and mid or dark)
     elseif key=='mountain' and ly>maxDown-27 and ly<maxDown-14 and (r+g+b)>165 then body:drawPixel(x,y,(math.floor(ly)%7<2)and C(168,152,108)or C(93,101,62))
     elseif key=='greenhouse' and y>foot-37 then body:drawPixel(x,y,(r+g+b)>150 and C(85,114,54)or C(43,66,38))
     elseif key=='astronomer' then body:drawPixel(x,y,(r+g+b)>170 and mid or dark)
     end
    end
   end
  end end
  if key=='tropic' then
   local function alpha(x,y)x=math.floor(x+.5);y=math.floor(y+.5);return x>=0 and x<256 and y>=0 and y<256 and pc.rgbaA(original:getPixel(x,y))>0 end
   for y=0,255 do for x=0,255 do local c=original:getPixel(x,y);local lx,ly=localXY(x,y)
    if pc.rgbaA(c)>0 and ly>52 and not skin(c)then
     local left,right=0,0
     for k=1,32 do if not alpha(x-k*co,y-k*si)then break end;left=k end
     for k=1,32 do if not alpha(x+k*co,y+k*si)then break end;right=k end
     local target=ly>maxDown-9 and 18 or 10
     local trim=math.max(1,math.floor((left+right+1-target)/2))
     if left<trim or right<trim then body:drawPixel(x,y,0)
     else
      local fill=(left-trim<2)and C(237,186,139)or((right-trim<2)and C(179,121,82)or C(221,166,118))
      local forward=0
      for k=1,9 do if not alpha(x-k*si,y+k*co)then break end;forward=k end
      if ly>maxDown-13 and (forward==0 or (forward>=3 and forward<=4))then fill=C(48,62,57)end
      body:drawPixel(x,y,fill)
     end
    end
   end end
   for y=math.max(0,foot-1),foot do for x=0,255 do if pc.rgbaA(original:getPixel(x,y))>0 then body:drawPixel(x,y,C(48,62,57))end end end
  end
  if key=='greenhouse' then
   for y=0,foot do for x=2,253 do local c=original:getPixel(x,y);local lx,ly=localXY(x,y)
    if pc.rgbaA(c)>0 and ly>42 and not skin(c)then
     for dx=-2,2 do if pc.rgbaA(original:getPixel(x+dx,y))==0 then back:drawPixel(x+dx,y,math.abs(dx)==2 and ink or C(60,86,42))end end
    end
   end end
  end
  if key=='rail' then
   poly({{-26,-23},{-14,-30},{0,-22},{12,-28},{24,-20},{27,26},{4,29},{0,24},{-4,29},{-25,25}},ink)
   poly({{-23,-22},{-13,-27},{0,-18},{12,-25},{21,-18},{24,23},{4,26},{0,21},{-4,26},{-22,23}},mid)
   poly({{0,-18},{12,-25},{21,-18},{24,23},{4,26},{0,21}},dark)
   poly({{-14,-28},{3,-16},{-8,-5},{-17,-15}},light);poly({{12,-26},{-1,-7},{14,-14},{18,-20}},light)
   poly({{-5,-24},{0,-20},{6,-24},{0,-14}},cream)
   for _,xx in ipairs({-7,8})do for _,yy in ipairs({-1,9,19})do rect(xx,yy,2,2,gold);dot(xx,yy,C(247,214,129))end end
   for dx=0,12 do dot(5+dx,10+math.floor(4*math.sin(dx*math.pi/12)),gold)end
  elseif key=='tropic' then
   poly({{-29,-24},{-13,-30},{0,-18},{12,-28},{28,-20},{31,27},{12,32},{0,24},{-21,31},{-28,21}},ink)
   poly({{-26,-22},{-13,-27},{0,-15},{12,-25},{25,-18},{28,24},{13,29},{0,21},{-19,28},{-25,20}},mid)
   poly({{-8,-23},{0,-17},{9,-25},{7,23},{-5,23}},C(218,160,112))
   poly({{-13,-28},{0,-16},{-10,-4},{-19,-17}},light);poly({{12,-26},{0,-16},{11,-5},{19,-18}},light)
   for _,p in ipairs({{-20,-7},{19,3},{-15,16},{17,21}})do
    rect(p[1]-2,p[2],7,2,C(247,218,153));rect(p[1],p[2]-2,2,7,C(247,218,153));rect(p[1],p[2],2,2,C(43,139,142))
   end
   line(-18,36,19,36,C(24,72,85),2)
  elseif key=='noir' then
   poly({{-28,-24},{-12,-31},{0,-23},{13,-30},{28,-20},{34,59},{12,65},{0,47},{-15,66},{-32,57}},ink)
   poly({{-25,-22},{-12,-28},{0,-20},{13,-27},{25,-18},{31,56},{13,61},{0,43},{-15,62},{-29,55}},mid)
   poly({{2,-20},{13,-27},{25,-18},{31,56},{13,61},{0,43}},dark)
   poly({{-16,-29},{5,-12},{-13,3},{-23,-16}},light);poly({{14,-29},{-2,-8},{16,-1},{23,-18}},light)
   rect(-25,17,50,5,C(72,43,47));rect(-3,16,8,7,gold)
   rect(-5,-24,8,17,C(110,39,57));for _,yy in ipairs({0,10,30})do rect(8,yy,2,2,gold)end
  elseif key=='alchemist' then
   poly({{-24,-24},{-10,-29},{13,-25},{24,-18},{25,25},{-24,27}},ink)
   poly({{-21,-23},{-9,-26},{13,-23},{21,-17},{22,23},{-21,24}},mid)
   poly({{-20,-14},{4,-25},{16,-11},{11,47},{-24,40}},ink)
   poly({{-17,-12},{4,-22},{13,-9},{8,43},{-21,37}},C(140,79,48))
   line(-15,-9,6,39,C(211,145,84),2);line(16,-20,-17,28,C(73,42,37),4)
   for i=0,2 do local xx=9+i*7;rect(xx,16,6,12,ink);rect(xx+1,18,4,8,i==1 and C(104,153,101)or C(137,93,166));rect(xx+1,14,4,4,gold)end
   rect(-25,-3,7,5,C(179,113,66));rect(19,-5,7,5,C(179,113,66))
  elseif key=='stage' then
   poly({{-28,-23},{-12,-29},{12,-27},{27,-20},{24,28},{-24,28}},ink)
   poly({{-25,-22},{-12,-26},{12,-24},{24,-18},{21,25},{-21,25}},mid)
   poly({{-24,12},{-9,22},{-19,51},{-44,64},{-35,35}},ink);poly({{15,15},{27,12},{40,59},{17,49},{8,24}},ink)
   poly({{-24,17},{-13,23},{-22,48},{-39,58},{-32,35}},dark);poly({{18,20},{25,17},{35,53},{19,46},{12,25}},dark)
   line(-32,36,-38,55,light);line(27,33,34,51,light)
   poly({{-12,-26},{0,-18},{11,-25},{8,17},{-9,17}},cream)
   poly({{-19,-27},{-4,-14},{-13,3},{-24,-16}},dark);poly({{17,-25},{4,-14},{13,4},{24,-15}},dark)
   poly({{-14,-18},{-2,-14},{-14,-7}},C(223,43,54));poly({{14,-18},{2,-14},{14,-7}},C(223,43,54));rect(-3,-17,6,6,C(139,27,43))
   for _,yy in ipairs({-3,5,13})do rect(0,yy,2,2,ink)end
  elseif key=='mountain' then
   active=back;poly({{-58,-28},{-30,-33},{-24,31},{-54,35}},ink);poly({{-55,-25},{-32,-29},{-28,28},{-51,31}},C(151,106,57))
   for yy=-20,25,7 do line(-54,yy,-30,yy-3,C(211,167,94),2)end;for xx=-51,-30,6 do line(xx,-24,xx+3,28,C(89,67,40),1)end
   active=shapes;poly({{-25,-24},{-12,-29},{12,-26},{23,-18},{24,28},{-23,29}},ink);poly({{-22,-23},{-11,-26},{11,-23},{20,-17},{21,25},{-20,26}},mid)
   poly({{-6,-25},{0,-18},{6,-24},{4,20},{-4,20}},cream);line(-18,-25,-10,29,C(98,66,40),5);line(17,-23,12,28,C(98,66,40),5)
   rect(-19,6,13,12,dark);rect(7,8,12,11,dark)
  elseif key=='ceremony' then
   poly({{-30,-25},{-12,-31},{12,-29},{30,-20},{35,56},{20,70},{-23,70},{-36,54}},ink)
   poly({{-27,-23},{-11,-28},{12,-26},{27,-18},{32,53},{18,67},{-21,67},{-33,52}},mid)
   poly({{-22,-25},{5,-11},{26,39},{16,64},{-4,22}},light)
   line(-16,-28,18,23,ink,2);line(13,-27,-19,17,ink,2);rect(-30,17,61,7,ink)
   poly({{19,20},{25,21},{30,70},{24,76},{20,47}},ink)
   for _,p in ipairs({{-23,49},{-16,54},{-22,59}})do rect(p[1],p[2],3,3,C(74,103,141))end
  elseif key=='apiary' then
   poly({{-27,-24},{-10,-30},{12,-28},{26,-19},{28,31},{-27,32}},ink)
   poly({{-24,-23},{-9,-27},{12,-25},{23,-17},{25,29},{-24,29}},mid)
   rect(-23,9,47,6,C(168,111,37));rect(-3,-25,3,54,dark);rect(-20,-9,13,12,light);rect(9,-9,11,12,light)
   for yy=-18,27,9 do rect(0,yy,2,2,gold)end
   rect(-23,28,18,6,C(168,111,37));rect(8,28,17,6,C(168,111,37))
  elseif key=='greenhouse' then
   poly({{-20,-24},{-10,-29},{10,-27},{20,-22},{27,46},{15,54},{-20,53},{-28,44}},ink)
   poly({{-17,-23},{-9,-26},{9,-24},{17,-20},{24,43},{14,51},{-18,50},{-25,42}},mid)
   line(-17,-26,-11,1,C(162,185,104),6);line(16,-25,11,1,C(162,185,104),6)
   rect(-17,7,34,23,dark);rect(-15,7,30,20,light);line(0,8,0,26,dark,2)
   rect(-22,-3,45,5,C(157,177,92));for _,xx in ipairs({-15,15})do rect(xx,-1,2,2,gold)end
  elseif key=='astronomer' then
   poly({{-24,-24},{-9,-30},{12,-27},{25,-19},{27,37},{10,48},{-23,44}},ink)
   poly({{-21,-22},{-8,-27},{12,-24},{22,-17},{24,34},{9,45},{-20,41}},mid)
   poly({{-25,-28},{-12,-35},{13,-33},{29,-25},{38,4},{21,14},{9,-1},{-12,13},{-36,2}},ink)
   poly({{-23,-26},{-11,-32},{13,-30},{26,-23},{34,2},{22,10},{8,-4},{-12,9},{-32,0}},dark)
   line(-30,0,-12,9,silver,2);line(22,10,34,2,silver,2);rect(-21,22,45,4,silver)
   poly({{-17,30},{16,28},{23,37},{9,45},{-20,41}},light)
   for _,p in ipairs({{-20,-17},{19,-13},{25,0},{-7,1}})do line(p[1]-3,p[2],p[1]+3,p[2],silver);line(p[1],p[2]-3,p[1],p[2]+3,silver)end
  end
  -- Preserve original face/hair in a bounded region. Headwear is then authored above it.
  for y=math.max(0,head[2]-24),math.min(255,head[4])do for x=math.max(0,head[1]-12),math.min(255,head[3]+9)do
   local c=original:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
   if d.sleeves and not skin(c) and pc.rgbaA(c)>0 and r>130 and g>125 and b>105 and math.max(r,g,b)-math.min(r,g,b)<65 and (x<head[1]+3 or x>head[3]-3 or y>head[4]-4)then c=(r+g+b)>620 and light or mid end
   body:drawPixel(x,y,c);shapes:drawPixel(x,y,0)
  end end
  if key=='ceremony' then
   for y=math.max(0,math.floor(hy+4)),math.min(255,math.floor(hy+36))do for x=math.max(0,math.floor(hx-51)),math.min(255,math.floor(hx-24))do
    local c=original:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    if pc.rgbaA(c)>0 and r<170 and g<105 and r>g*1.18 and g>b*1.1 then body:drawPixel(x,y,0)end
   end end
  end
  active=hats
  local oldcx,oldcy,oldangle=cx,cy,angle
  cx,cy=hx,hy
  angle=clip=='defeat' and ((d.job=='bartender' and ({.28,.45,.70,.85}) or ({.15,-.60,-.15,-.1}))[idx+1]) or 0
  co,si=math.cos(angle),math.sin(angle)
  if key=='rail' then
   for y=0,255 do for x=0,255 do local rx=(x-cx)*co+(y-cy)*si+131.5;local ry=-(x-cx)*si+(y-cy)*co+84.5
    if rx>=92 and rx<=166 and ry>=39 and ry<=80 then local c=rail:getPixel(math.floor(rx+.5),math.floor(ry+.5));if pc.rgbaA(c)>0 then put(x,y,c)end end
   end end
  elseif key=='tropic' then
   ellipse(0,-21,37,7,ink);ellipse(0,-22,34,5,C(218,181,107));poly({{-24,-25},{-21,-43},{17,-44},{24,-25}},ink);poly({{-21,-27},{-19,-40},{15,-41},{21,-27}},C(236,203,134));rect(-21,-28,43,4,C(45,128,133));line(-28,-23,28,-23,C(159,115,66))
  elseif key=='noir' then
   ellipse(1,-19,36,5,ink);ellipse(0,-21,32,3,dark);poly({{-25,-23},{-19,-42},{-5,-47},{8,-41},{20,-44},{25,-23}},ink);poly({{-22,-26},{-17,-39},{-5,-44},{8,-38},{17,-41},{22,-26}},mid);rect(-23,-29,46,6,C(108,39,56))
  elseif key=='alchemist' then
   line(-28,-17,23,-19,C(133,78,43),5);ellipse(-10,-18,9,7,ink);ellipse(-10,-18,7,5,C(176,112,65));ellipse(-10,-18,4,3,C(88,156,167));ellipse(12,-19,9,7,ink);ellipse(12,-19,7,5,C(176,112,65));ellipse(12,-19,4,3,C(105,162,177));line(-18,-21,-8,-21,silver)
  elseif key=='stage' then
   ellipse(0,-18,33,5,ink);poly({{-23,-21},{-22,-48},{19,-48},{22,-21}},ink);poly({{-20,-22},{-19,-45},{16,-45},{19,-22}},mid);rect(-21,-26,41,6,C(217,43,54));line(-18,-43,-18,-28,light,2)
  elseif key=='mountain' then
   poly({{-31,-14},{-25,-37},{-10,-43},{15,-39},{28,-24},{21,-15}},ink);poly({{-28,-16},{-22,-35},{-10,-40},{13,-36},{25,-24},{20,-18}},C(139,149,92));poly({{-27,-19},{-37,-1},{-44,5},{-38,-19}},C(87,111,62));line(-24,-22,18,-28,C(184,184,119),2)
  elseif key=='ceremony' then
   -- An undecorated low bun replaces the outer ponytail mass in the native layer.
   ellipse(-31,9,8,7,ink);ellipse(-31,8,6,5,C(74,48,40))
  elseif key=='apiary' then
   ellipse(0,-30,32,17,ink);ellipse(0,-31,29,14,cream);ellipse(0,-20,36,5,ink);ellipse(0,-21,33,3,C(205,172,101));line(-31,-18,-28,18,C(190,181,151),2);line(31,-18,28,18,C(190,181,151),2);line(-28,18,28,18,C(190,181,151));for yy=-13,13,6 do rect(-30,yy,3,1,silver);rect(28,yy,3,1,silver)end
  elseif key=='greenhouse' then
   poly({{-30,-15},{-25,-36},{-10,-44},{13,-39},{26,-24},{20,-16}},ink);poly({{-27,-17},{-22,-34},{-10,-41},{11,-36},{23,-24},{19,-19}},C(178,98,68));poly({{-25,-18},{-34,5},{-43,13},{-38,-9}},C(146,72,54));line(-23,-23,18,-28,C(223,162,112),2)
  elseif key=='astronomer' then
   poly({{-30,-20},{-17,-35},{-8,-67},{7,-57},{26,-21}},ink);poly({{-26,-22},{-15,-34},{-6,-62},{4,-54},{22,-22}},mid);ellipse(0,-20,35,5,ink);ellipse(0,-22,31,3,light);line(-17,-27,18,-27,silver,3);line(-7,-45,3,-45,silver);line(-2,-50,-2,-40,silver)
   -- Square spectacles, distinct from the forehead goggles of the alchemist.
   line(0,-2,8,-2,silver);line(0,5,8,5,silver);line(0,-2,0,5,ink);line(8,-2,8,5,ink);line(11,-2,19,-2,silver);line(11,5,19,5,silver);line(11,-2,11,5,ink);line(19,-2,19,5,ink);line(8,1,11,1,silver)
  end
  cx,cy,angle=oldcx,oldcy,oldangle;co,si=math.cos(angle),math.sin(angle)
  -- Restore exact articulated source hands, so all attachment positions remain unchanged.
  if a.anchors and a.anchors.hand then for _,handKey in ipairs({'hand','offHand'})do
   local an=a.anchors[handKey]
   if an then local xx,yy=an[1],256-an[2]
    for y=math.max(0,yy-15),math.min(255,yy+13)do for x=math.max(0,xx-14),math.min(255,xx+14)do
     local c=original:getPixel(x,y)
     local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
     local innerFace=x>=head[1]+4 and x<=head[3]-4 and y>=head[2]+8 and y<=head[4]-8
     if d.sleeves and not skin(c) and not innerFace and pc.rgbaA(c)>0 and r>130 and g>125 and b>105 and math.max(r,g,b)-math.min(r,g,b)<65 then c=(r+g+b)>620 and light or mid end
     if d.gloves and skin(c)then local v=(pc.rgbaR(c)+pc.rgbaG(c)+pc.rgbaB(c))/3;c=v>150 and cream or silver end
     body:drawPixel(x,y,c);shapes:drawPixel(x,y,0);hats:drawPixel(x,y,0);back:drawPixel(x,y,0)
    end end
   end
  end end
  -- Native sword actions embed their original weapon; this exact source mask always wins.
  local weaponMask=nil
  if clip=='sword_swing' then
   weaponMask=Image{fromFile=project..'ArtSource/skins-2026-10-13/v2/masks/sword_swing/swing_base/'..a.filename}
   for y=0,255 do for x=0,255 do if masked(weaponMask,x,y)then body:drawPixel(x,y,original:getPixel(x,y));shapes:drawPixel(x,y,0);hats:drawPixel(x,y,0);back:drawPixel(x,y,0)end end end
  end
  -- Resting poses keep the original floor plane; cloth may not hang beneath it.
  for y=foot+1,255 do for x=0,255 do shapes:drawPixel(x,y,0);hats:drawPixel(x,y,0);back:drawPixel(x,y,0)end end
  local s=Sprite(256,256,ColorMode.RGB);s.layers[1].name='Original source reference - hidden';s.cels[1].image=original;s.layers[1].isVisible=false
  local l=s:newLayer();l.name='Back outfit structure';s:newCel(l,1,back)
  l=s:newLayer();l.name='Articulated source material and protected hands';s:newCel(l,1,body)
  l=s:newLayer();l.name='New garment silhouette, seams and structural details';s:newCel(l,1,shapes)
  l=s:newLayer();l.name=key=='rail' and 'Actual PixelLab conductor cap' or 'Native authored headwear and accessories';s:newCel(l,1,hats)
  local tag=s:newTag(1,1);tag.name=a.filename:gsub('%.png$','')
  local native=folder..a.filename:gsub('%.png$','.aseprite');s:saveAs(native)
  local flat=Image(s.spec);flat:drawSprite(s,1);flat:saveAs(folder..a.filename);s:close()
  local read=Sprite{fromFile=native};local check=Image(read.spec);check:drawSprite(read,1)
  for y=0,255 do for x=0,255 do
   assert(flat:getPixel(x,y)==check:getPixel(x,y),'Native readback differs')
   if weaponMask and masked(weaponMask,x,y)then assert(flat:getPixel(x,y)==original:getPixel(x,y),'Embedded weapon changed')end
  end end;read:close()
  local col,row=(num-1)%6,math.floor((num-1)/6)
  contact:drawImage(original,Point(col*256,row*512));contact:drawImage(flat,Point(col*256,row*512+256))
  if (idx==0 and (clip=='idle' or clip=='move' or clip=='hurt' or clip=='defeat' or clip=='ranged')) or (clip=='sword_swing' and idx==5) or (d.job=='bartender' and clip=='melee' and idx==1)then
   if repn<6 then reps:drawImage(original,Point(repn*256,0));reps:drawImage(flat,Point(repn*256,256));repn=repn+1 end
  end
  log:write(a.filename..' NATIVE_EXPORT_READBACK_OK'..(weaponMask and ' EMBEDDED_WEAPON_EXACT' or '')..'\n')
 end end
 log:close();contact:saveAs(root..'evidence/'..key..'-all-frames.png');reps:saveAs(root..'evidence/'..key..'-representative-comparison.png')
end end
