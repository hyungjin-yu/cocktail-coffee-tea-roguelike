local root=app.params.root
local pc=app.pixelColor
local pal={{32,29,41},{74,64,80},{83,84,111},{167,177,194},{244,241,231},{234,212,165},{184,142,101},{70,107,69},{142,173,104},{48,32,40},{113,71,50},{220,174,114},{82,197,193},{239,101,88},{255,226,172},{49,81,87}}
local function color(i) local c=pal[i];return pc.rgba(c[1],c[2],c[3],255) end
local function clean(im)
 for p in im:pixels() do local c=p();if pc.rgbaA(c)<128 then p(0) else
 local best,dist=1,1e9
 for i,v in ipairs(pal) do local d=(pc.rgbaR(c)-v[1])^2+(pc.rgbaG(c)-v[2])^2+(pc.rgbaB(c)-v[3])^2;if d<dist then best,dist=i,d end end
 p(color(best)) end end
end
local function crop(im)
 clean(im);local l,t,r,b=im.width,im.height,-1,-1
 for p in im:pixels() do if pc.rgbaA(p())>0 then l=math.min(l,p.x);t=math.min(t,p.y);r=math.max(r,p.x);b=math.max(b,p.y) end end
 assert(r>=l,'empty PixelLab source');local out=Image(r-l+1,b-t+1,ColorMode.RGB);out:drawImage(im,Point(-l,-t));return out
end
local function dot(im,x,y,c) if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end end
local function fill(im,x,y,w,h,c) for v=y,y+h-1 do for u=x,x+w-1 do dot(im,u,v,c) end end end
local function line(im,x,y,x2,y2,c)
 local n=math.max(math.abs(x2-x),math.abs(y2-y));if n==0 then dot(im,x,y,c);return end
 for i=0,n do dot(im,math.floor(x+(x2-x)*i/n+.5),math.floor(y+(y2-y)*i/n+.5),c) end
end
local function scale9(src,w,h,border)
 local out=Image(w,h,ColorMode.RGB);local ww,hh=w-8,h-8
 local bx=math.min(border,math.floor(src.width/3));local by=math.min(border,math.floor(src.height/3))
 for y=0,hh-1 do for x=0,ww-1 do
 local u=x<bx and x or (x>=ww-bx and src.width-(ww-x) or bx+math.floor((x-bx)*(src.width-2*bx)/(ww-2*bx)))
 local v=y<by and y or (y>=hh-by and src.height-(hh-y) or by+math.floor((y-by)*(src.height-2*by)/(hh-2*by)))
 out:drawPixel(x+4,y+4,src:getPixel(u,v)) end end
 return out
end
local function fit(src,w,h)
 local z=math.min((w-8)/src.width,(h-8)/src.height)
 src:resize{width=math.max(1,math.floor(src.width*z+.5)),height=math.max(1,math.floor(src.height*z+.5)),method='nearest'}
 local out=Image(w,h,ColorMode.RGB);out:drawImage(src,Point((w-src.width)//2,(h-src.height)//2));return out
end
local function save(id,im,source)
 local dir=root..'/assets/'..id;app.fs.makeAllDirectories(dir)
 local s=Sprite(im.width,im.height,ColorMode.RGB);s.layers[1].name='Finished PixelLab and Aseprite art';s:newCel(s.layers[1],1,im,Point(0,0))
 if source then local l=s:newLayer();l.name='PixelLab normalized source (hidden)';l.isVisible=false;s:newCel(l,1,source,Point(0,0)) end
 s:saveAs(dir..'/'..id..'.aseprite');im:saveAs(dir..'/'..id..'.png');s:close();print('saved '..id)
end
local function clip(id,w,h,durations,fn)
 local dir=root..'/assets/'..id;app.fs.makeAllDirectories(dir..'/frames')
 local s=Sprite(w,h,ColorMode.RGB);s.layers[1].name='Aseprite motion from PixelLab parts'
 for i,ms in ipairs(durations) do if i>1 then s:newEmptyFrame() end
 local im=Image(w,h,ColorMode.RGB);fn(im,i);s:newCel(s.layers[1],i,im,Point(0,0));s.frames[i].duration=ms/1000
 im:saveAs(dir..'/frames/'..id..'_'..string.format('%02d',i-1)..'.png') end
 s:saveAs(dir..'/'..id..'.aseprite');app.activeSprite=s
 app.command.SaveFileAs{ui=false,filename=root..'/preview/'..id..'.gif'};s:close();print('clip '..id)
end
app.fs.makeAllDirectories(root..'/preview');app.fs.makeAllDirectories(root..'/qa')
local finished={}
local defs={{'ingredient_card',128,80,12},{'receipt',128,192,10},{'spring_notebook',192,160,16},{'recipe_storage',160,112,0},{'laptop_open',384,256,0},{'tree_node',64,64,10},{'staff_card',160,112,14},{'button',96,32,8}}
for _,d in ipairs(defs) do
 local path=root..'/provenance/'..d[1]..'.raw.png'
 if app.fs.isFile(path) then
 local src=crop(Image{fromFile=path});local im=d[4]>0 and scale9(src,d[2],d[3],d[4]) or fit(src,d[2],d[3]);local source=Image(im)
 if d[1]=='ingredient_card' then fill(im,16,16,96,48,color(6))
 elseif d[1]=='receipt' then fill(im,12,20,104,152,color(5));for y=20,171 do for x=4,11 do im:drawPixel(x,y,source:getPixel(x,96));im:drawPixel(127-x,y,source:getPixel(127-x,96)) end end
 elseif d[1]=='spring_notebook' then fill(im,20,44,152,96,color(6))
 elseif d[1]=='button' then fill(im,14,10,68,12,color(6))
 elseif d[1]=='staff_card' then
  for y=4,107 do for x=4,11 do im:drawPixel(x,y,source:getPixel(159-x,y)) end end
  fill(im,12,14,136,84,color(1))
  local portrait=Image(35,43,ColorMode.RGB);portrait:drawImage(source,Point(-4,-21));im:drawImage(portrait,Point(16,24))
  fill(im,18,27,28,34,color(5))
 elseif d[1]=='tree_node' then fill(im,14,14,36,36,color(10))
 end
 save(d[1],im,source);finished[d[1]]=im
 end
end
-- Shared frame states have distinct geometry as well as color.
local function marks(im,state,w,h)
 local c=state=='equipped' and color(13) or state=='available' and color(12) or state=='owned' and color(9) or color(2)
 if state=='locked' then
  line(im,w-19,5,w-19,11,c);line(im,w-19,5,w-12,5,c);line(im,w-12,5,w-12,11,c);fill(im,w-21,10,12,9,c);dot(im,w-16,13,color(1));dot(im,w-16,14,color(1))
 elseif state=='available' then
  for _,xy in ipairs({{7,7},{w-8,7},{7,h-8},{w-8,h-8}}) do local x,y=xy[1],xy[2];line(im,x-3,y,x+3,y,c);line(im,x,y-3,x,y+3,c) end
 elseif state=='owned' then
  line(im,w-22,11,w-18,15,c);line(im,w-18,15,w-10,7,c);line(im,w-22,12,w-18,16,c);line(im,w-18,16,w-10,8,c)
 elseif state=='equipped' then
  line(im,7,7,w-8,7,c);line(im,7,h-8,w-8,h-8,c);line(im,7,7,7,h-8,c);line(im,w-8,7,w-8,h-8,c)
  line(im,w-23,13,w-18,18,c);line(im,w-18,18,w-9,9,c)
 end
end
for _,id in ipairs({'tree_node','button','staff_card'}) do if finished[id] then
 for _,state in ipairs({'locked','available','owned','equipped'}) do local im=Image(finished[id]);marks(im,state,im.width,im.height);save(id..'_'..state,im,finished[id]) end
end end
-- Stretch proof uses Aseprite's same 9-slice pixel source mapping.
if finished.receipt then
 for _,h in ipairs({192,288,384}) do
  local src=finished.receipt;local im=Image(128,h,ColorMode.RGB)
  for y=0,h-1 do local v=y<20 and y or (y>=h-20 and 192-(h-y) or 20+(y-20)%152)
   for x=0,127 do im:drawPixel(x,y,src:getPixel(x,v)) end end
  im:saveAs(root..'/qa/receipt-height-'..h..'.png')
 end
end
-- Paper torn from the actual PixelLab notebook, then stored behind actual box front.
if finished.spring_notebook then
 local notebook=finished.spring_notebook
 local page=Image(172,128,ColorMode.RGB)
 for y=0,127 do for x=0,171 do
  local sy=y+24;local c=notebook:getPixel(x+10,sy)
  if y>=((x//5)%2)*2 then page:drawPixel(x,y,c) end
 end end
 -- The torn upper edge is ivory paper fibre, with no binding attached.
 for x=1,170 do local y=((x//5)%2)*2;dot(page,x,y,color(5));dot(page,x,y+1,color(6)) end
 save('torn_recipe',page,nil);finished.torn_recipe=page
 clip('recipe_tear_store',512,224,{50,50,50,50,50,50,50,50,50,50},function(im,f)
  im:drawImage(notebook,Point(8,40))
  local t=(f-1)/9;local z=1-.64*(t*t*(3-2*t));local moved=Image(page)
  moved:resize{width=math.floor(page.width*z+.5),height=math.floor(page.height*z+.5),method='nearest'}
  local x=18+math.floor(352*(t*t*(3-2*t)));local y=64-math.floor(math.sin(t*math.pi)*48)+math.floor(t*t*18)
  if f>=8 then
   x=({[8]=370,[9]=374,[10]=376})[f]
   y=({[8]=94,[9]=130,[10]=156})[f]
  end
  for py=0,moved.height-1 do for px=0,moved.width-1 do
   if f<8 or y+py<154 then local c=moved:getPixel(px,py);if pc.rgbaA(c)>0 then dot(im,x+px,y+py,c) end end
  end end
  -- Box foreground masks the descending page naturally at the final stage.
  if finished.recipe_storage then
   if f<9 then im:drawImage(finished.recipe_storage,Point(336,108))
   else
    local box=finished.recipe_storage
    im:drawImage(box,Point(336,108))
   end
  end
 end)
end
-- Enlarge the actual generated screen relative to the deck for readable 720p UI.
if finished.laptop_open then
 local original=finished.laptop_open
 local im=Image(384,256,ColorMode.RGB)
 for y=4,251 do
  local sy=y<204 and (4+math.floor((y-4)*172/200)) or (176+math.floor((y-204)*76/48))
  for x=4,379 do local sx=41+math.floor((x-4)*301/376);im:drawPixel(x,y,original:getPixel(sx,sy)) end
 end
 -- The actual UI screen is unmarked and uniform; glyphs are rendered at runtime.
 fill(im,56,28,268,160,color(16));finished.laptop_open=im
 save('laptop_open',im,original)
 local hinge=204
 local function lidFrame(t,lit)
  local out=Image(384,256,ColorMode.RGB)
  -- keyboard deck never changes origin or scale across frames
  for y=hinge,255 do for x=0,383 do out:drawPixel(x,y,im:getPixel(x,y)) end end
  local angle=t*math.pi/2
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
  return out
 end
 local stableOpen=lidFrame(1,false);fill(stableOpen,56,28,268,160,color(16))
 save('laptop_open',stableOpen,original);finished.laptop_open=stableOpen
 local closed=lidFrame(0,false)
 -- Closed lid is a generated silver bezel silhouette flattened at the same hinge.

 save('laptop_closed_matched',closed,nil);finished.laptop_closed_matched=closed
 local function drawLid(out,t,lit)out:drawImage(lidFrame(t,lit),Point(0,0)) end
 clip('laptop_arrive',384,288,{50,50,50,50,50},function(out,f)
  out:drawImage(closed,Point(0,({28,17,8,2,0})[f]))
 end)
 clip('laptop_opening',384,256,{50,50,50,50,50,50,50},function(out,f)
  drawLid(out,({0,.12,.28,.50,.74,.92,1})[f],false)
 end)
 clip('laptop_light',384,256,{50,50,50},function(out,f)
  out:drawImage(lidFrame(1,false),Point(0,0));fill(out,56,28,268,160,color(({1,3,16})[f]))
 end)
 clip('laptop_closing',384,256,{50,50,50,50,50,50,50},function(out,f)
  if f==7 then out:drawImage(closed,Point(0,0)) else drawLid(out,({.92,.74,.50,.28,.12,.04,0})[f],false) end
 end)
 clip('laptop_sequence',384,288,{50,50,50,50,50,50,50,50,50,50,50,50,50,50,50},function(out,f)
  if f<=5 then out:drawImage(closed,Point(0,({28,17,8,2,0})[f]))
  elseif f<=12 then drawLid(out,({0,.12,.28,.50,.74,.92,1})[f-5],false)
  else out:drawImage(lidFrame(1,false),Point(0,0));fill(out,56,28,268,160,color(({1,3,16})[f-12])) end
 end)
end
