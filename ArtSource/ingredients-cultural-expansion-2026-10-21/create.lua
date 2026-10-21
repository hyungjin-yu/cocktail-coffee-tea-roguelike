local out=app.params['out']
local ink='201d29'
local img,spr
local sourceMode=false
local function col(h) return app.pixelColor.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local function p(x,y,c) for j=0,1 do for i=0,1 do img:drawPixel(x*2+i,y*2+j,col(c)) end end end
local function rect(x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do p(xx,yy,c) end end end
local function poly(v,c)
 for y=0,63 do for x=0,63 do local inside=false;local j=#v
  for i=1,#v do local a,b=v[i],v[j]; if ((a[2]>y)~=(b[2]>y)) and (x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1]) then inside=not inside end;j=i end
  if inside then p(x,y,c) end
 end end
end
local function ellipse(x,y,rx,ry,c)
 for yy=math.floor(y-ry),math.ceil(y+ry) do for xx=math.floor(x-rx),math.ceil(x+rx) do if ((xx-x)/rx)^2+((yy-y)/ry)^2<=1 then p(xx,yy,c) end end end
end
local function line(x,y,a,b,c,w)
 local n=math.max(math.abs(a-x),math.abs(b-y));for i=0,n do rect(math.floor(x+(a-x)*i/n),math.floor(y+(b-y)*i/n),w or 1,w or 1,c) end
end
local function layer(name) local l=spr:newLayer();l.name=name;local im=Image(128,128,ColorMode.RGB);img=spr:newCel(l,1,im).image end
local function leaf(x,y,dx,dy,base,hi)
 poly({{x,y+2},{x+dx*.35-3,y+dy*.3},{x+dx,y+dy},{x+dx*.65+3,y+dy*.6}},ink)
 poly({{x,y},{x+dx*.35-1,y+dy*.3},{x+dx,y+dy+1},{x+dx*.65+1,y+dy*.6}},base)
 line(x,y,x+dx-1,y+dy+2,hi)
end
local function bud(x,y,dx,dy)
 local bx=x+dx;local by=y+dy
 poly({{x-2,y},{bx-4,by+7},{bx,by-2},{bx+3,by+7},{x+2,y+1}},ink)
 poly({{x,y},{bx-2,by+7},{bx,by},{bx+1,by+7},{x+1,y}},'b4bba2')
 line(x,y-2,bx,by+3,'f4f1e7');p(bx,by+1,'f4f1e7')
end
local function bean(x,y)
 ellipse(x,y,6,8,ink);ellipse(x-1,y-1,4,6,'714732');ellipse(x-2,y-3,2,3,'ad784b')
 line(x+1,y-5,x-1,y,ink,1);line(x-1,y,x+1,y+5,ink,1);p(x+2,y+4,'dcae72')
end
local function bottle(kind)
 local base,shade,hi
 if kind==1 then base='87b943';shade='48582d';hi='c5db87' elseif kind==2 then base='74396e';shade='422740';hi='bc73ae' else base='ebc650';shade='a87930';hi='ffe2ac' end
 if sourceMode then base='a7b1c2';shade='53546f';hi='f4f1e7' end
 poly({{25,8},{39,8},{39,22},{46,29},{46,53},{42,57},{22,57},{18,53},{18,29},{25,22}},ink)
 poly({{27,16},{37,16},{37,24},{43,30},{43,52},{40,54},{24,54},{21,51},{21,30},{27,24}},shade)
 poly({{27,17},{34,17},{34,25},{39,30},{39,51},{24,51},{23,48},{23,30},{29,25}},base)
 rect(25,8,14,7,ink);rect(27,9,10,4,'b88e65');rect(27,9,7,1,'ead4a5');rect(27,15,10,3,'a7b1c2');rect(28,15,5,1,'f4f1e7')
 layer('Paper seal and ingredient emblem')
 rect(21,34,22,13,ink);rect(23,35,18,10,'ead4a5');rect(24,35,2,10,'f4f1e7');rect(39,35,2,10,'b88e65')
 if kind==1 then
  line(31,44,32,35,'48582d');for i=0,2 do leaf(32,42-i*2,-5,-3,'466b45','8ead68');leaf(32,42-i*2,5,-3,'466b45','8ead68') end
 elseif kind==2 then
  for _,v in ipairs({{29,39},{35,39},{32,43}}) do ellipse(v[1],v[2],3,3,ink);ellipse(v[1]-1,v[2]-1,2,2,base);p(v[1]-1,v[2]-2,hi) end
  leaf(32,37,4,-3,'466b45','8ead68')
 else
  ellipse(32,40,7,4,shade);ellipse(31,39,5,3,base);rect(25,39,1,2,hi);leaf(36,37,5,-3,'466b45','8ead68')
 end
 layer('Glass glints')
 rect(23,29,2,4,hi);rect(23,48,2,3,hi);rect(28,20,1,4,'f4f1e7');rect(26,52,10,1,hi);p(22,30,'f4f1e7')
end
local function flower(x,y,cham)
 local petal=cham and 'f4f1e7' or 'ead4a5'
 local count=cham and 9 or 5
 for i=0,count-1 do local a=i*math.pi*2/count;local xx=x+math.cos(a)*5;local yy=y+math.sin(a)*5
  ellipse(xx,yy,3,3,ink);ellipse(xx,yy-1,2,2,petal)
 end
 ellipse(x,y,cham and 4 or 2,cham and 4 or 2,'b88e65');ellipse(x-1,y-1,cham and 3 or 1,cham and 3 or 1,'ebc650');p(x-1,y-2,'ffe2ac')
end
local function tea(kind)
 if kind==1 then
  for _,v in ipairs({{21,50,-5,-24},{30,51,-1,-30},{36,52,9,-27},{42,49,12,-16}}) do
   line(v[1],v[2],v[1]+v[3],v[2]+v[4],'48582d',2)
   leaf(v[1],v[2]-2,v[3]-5,v[4]+7,'466b45','8ead68')
   bud(v[1],v[2]-4,v[3],v[4])
  end
 elseif kind==2 then
  for _,v in ipairs({{17,43,9,-17},{23,51,13,-26},{33,49,12,-24},{40,51,14,-15},{14,49,10,-9}}) do
   leaf(v[1],v[2],v[3],v[4],'48582d','8ead68');line(v[1]+2,v[2]-5,v[1]+7,v[2]-8,'714732',2);line(v[1]+7,v[2]-8,v[1]+4,v[2]-11,'201d29',2)
  end
 else
  line(23,52,24,31,'48582d',2);line(35,51,39,24,'48582d',2);line(37,45,48,35,'48582d',2)
  leaf(26,48,-12,-12,'466b45','8ead68');leaf(35,43,12,-12,'466b45','8ead68');leaf(29,51,4,-12,'466b45','8ead68')
  layer('Botanical flowers')
  flower(23,29,kind==4);flower(39,22,kind==4);flower(48,36,kind==4)
 end
 layer('Loose botanical accents')
 if kind<3 then leaf(43,54,10,-5,kind==1 and 'b4bba2' or '48582d',kind==1 and 'f4f1e7' or '8ead68');p(16,53,'b88e65');p(33,55,'b88e65') else rect(18,52,2,2,'ead4a5');rect(49,49,2,2,'ead4a5') end
end
local function coffee(kind)
 local base=({'466b45','74396e','a87930'})[kind]
 local hi=({'8ead68','bc73ae','ebc650'})[kind]
 ellipse(31,53,17,5,ink);rect(14,17,35,35,ink);rect(16,18,31,34,base);rect(17,20,4,29,hi);rect(44,20,3,29,'302028')
 ellipse(31,17,17,5,ink);ellipse(31,17,15,3,'a7b1c2');ellipse(30,16,12,1,'f4f1e7');line(18,53,41,53,'a7b1c2');line(21,55,39,55,'53546f')
 layer('Origin paper badge')
 rect(21,25,22,22,ink);rect(23,26,18,19,'ead4a5');rect(23,26,2,19,'f4f1e7');rect(40,26,1,19,'b88e65')
 if kind==1 then
  poly({{25,34},{29,28},{32,32},{35,28},{40,34}},base);line(26,34,39,34,hi)
  -- Floral origin cue, not a guaranteed tasting note.
  p(28,38,'f4f1e7');p(27,39,'f4f1e7');p(29,39,'f4f1e7');p(28,40,'ebc650')
 elseif kind==2 then
  poly({{25,34},{30,28},{33,33},{37,29},{40,34}},base)
  ellipse(28,39,2,2,'ef6558');ellipse(34,39,2,2,'ef6558');line(28,36,34,36,'466b45')
 else
  poly({{25,33},{29,29},{32,31},{36,27},{40,33}},base)
  for i=0,2 do line(25,37+i*2,37-i*2,37+i*2,'466b45') end
 end
 layer('Shared roasted bean identity')
 bean(40,48);bean(50,52)
 layer('Tin glints');rect(17,23,1,15,'f4f1e7');rect(19,19,4,1,'f4f1e7')
end
local ids={'absinthe','creme_cassis','limoncello','white_tea','oolong_tea','jasmine_tea','chamomile_flower','ethiopia_yirgacheffe_kochere','colombia_huila_vergel','brazil_mantiqueira_alves_lima'}
for i,id in ipairs(ids) do
 spr=Sprite(128,128,ColorMode.RGB);spr.layers[1].name='Silhouette and base materials';img=spr.cels[1].image
 if i<=3 then bottle(i) elseif i<=7 then tea(i-3) else coffee(i-7) end
 spr:saveAs(out..'/'..id..'.aseprite');spr:saveCopyAs(out..'/'..id..'.png')
 local master=spr
 local function variant(name,w,h,crop)
  spr=Sprite(master);app.activeSprite=spr
  if crop then spr:crop(Rectangle(crop[1],crop[2],crop[3],crop[4])) end
  app.command.SpriteSize{ui=false,width=w,height=h,method='nearest'}
  spr:saveAs(out..'/'..name..'.aseprite');spr:saveCopyAs(out..'/'..name..'.png');spr:close();spr=master
 end
 variant('dl_icon_'..id,32,32)
 if i<=3 then
  variant('dl_bottle_'..id,24,56,{34,8,60,112})
  local old=master
  spr=Sprite(128,128,ColorMode.RGB);img=spr.cels[1].image;spr.layers[1].name='Empty glass body';sourceMode=true;bottle(i);sourceMode=false
  -- The cap is a separate aligned layer/asset for opening animation.
  for _,c in ipairs(spr.cels) do for y=0,29 do for x=0,127 do c.image:drawPixel(x,y,0) end end end
  master=spr;variant('dl_srcbottle_'..id,24,56,{34,8,60,112});spr:close()
  spr=Sprite(128,128,ColorMode.RGB);img=spr.cels[1].image;spr.layers[1].name='White liquid fill mask'
  poly({{27,17},{37,17},{37,24},{43,30},{43,52},{40,54},{24,54},{21,51},{21,30},{27,24}},'ffffff')
  -- Keep the paper label readable over the dynamic liquid.
  for y=68,93 do for x=42,85 do img:drawPixel(x,y,0) end end
  master=spr;variant('dl_srcbottle_'..id..'_mask',24,56,{34,8,60,112});spr:close()
  spr=Sprite(128,128,ColorMode.RGB);img=spr.cels[1].image;spr.layers[1].name='Aligned removable cap'
  rect(25,8,14,7,ink);rect(27,9,10,4,'b88e65');rect(27,9,7,1,'ead4a5')
  master=spr;variant('dl_srcbottle_'..id..'_cap',24,56,{34,8,60,112});spr:close();master=old;spr=master
 elseif i<=7 then
  -- Ingredient tin carries the actual botanical identity, without text.
  local botanical=Sprite(master)
  spr=Sprite(128,128,ColorMode.RGB);img=spr.cels[1].image;spr.layers[1].name='Paper tea tin'
  rect(12,15,40,39,ink);rect(14,17,36,35,'b88e65');rect(15,18,31,31,'ead4a5');rect(15,18,4,30,'f4f1e7')
  ellipse(32,15,20,5,ink);ellipse(32,15,18,3,'8ead68');ellipse(30,14,14,1,'ead4a5');rect(15,51,33,2,'48582d')
  layer('Botanical label');local bi=Image(128,128,ColorMode.RGB);bi:drawSprite(botanical,1);img:drawImage(bi,Point(0,-2));botanical:close()
  local teaMaster=master;master=spr;variant('dl_tin_'..id,32,32);spr:close();master=teaMaster;spr=master
  for f=0,2 do
   local old=master
   spr=Sprite(128,128,ColorMode.RGB);img=spr.cels[1].image;spr.layers[1].name='Botanical measured portion'
   if i==4 then bud(24,48,12+f*2,-32) elseif i==5 then leaf(24,48,13+f*2,-32,'48582d','8ead68');line(28,38,34,33,'714732',2);line(34,33,31,28,ink,2) else flower(32,32,i==7) end
   master=spr;variant('dl_leaf_'..id..'_'..string.format('%02d',f),8,8,{24,20,72,80});spr:close();master=old;spr=master
   variant('dl_leaf_heap_'..id..'_'..string.format('%02d',f),32,16,{12+f*2,28+f*2,104-f*4,88-f*2})
  end
 else variant('dl_tin_'..id,32,32) end
 master:close()
end
print('Created 10 layered Aseprite sources and PNGs at 128x128.')
