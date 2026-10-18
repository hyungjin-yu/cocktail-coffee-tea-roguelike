local root=app.params.root
local pc=app.pixelColor
local white=pc.rgba(255,255,255,255)
local function clean(im)
 for p in im:pixels() do local c=p(); if pc.rgbaA(c)>=128 then p(pc.rgba(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),255)) else p(0) end end
end
local function normalized(raw,w,h)
 clean(raw)
 local l,t,r,b=raw.width,raw.height,-1,-1
 for p in raw:pixels() do if pc.rgbaA(p())>0 then l=math.min(l,p.x);t=math.min(t,p.y);r=math.max(r,p.x);b=math.max(b,p.y) end end
 assert(r>=l,'empty source')
 local im=Image(r-l+1,b-t+1,ColorMode.RGB);im:drawImage(raw,Point(-l,-t))
 local z=math.min((w-4)/im.width,(h-4)/im.height)
 im:resize{width=math.max(1,math.floor(im.width*z+.5)),height=math.max(1,math.floor(im.height*z+.5)),method='nearest'}
 local out=Image(w,h,ColorMode.RGB)
 local x,y=(w-im.width)//2,(h-im.height)//2
 out:drawImage(im,Point(x,y))
 return out,{x,y,im.width,im.height}
end
local function inside(x,y,pts)
 local yes=false;local j=#pts
 for i=1,#pts do local a,b=pts[i],pts[j]
  if ((a[2]>y)~=(b[2]>y)) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then yes=not yes end;j=i
 end
 return yes
end
local function save(id,im,raw,mask)
 local s=Sprite(im.width,im.height,ColorMode.RGB);s.layers[1].name='Finished PixelLab sprite';s:newCel(s.layers[1],1,im,Point(0,0))
 if mask then local layer=s:newLayer();layer.name='Liquid mask - white interior';layer.isVisible=false;s:newCel(layer,1,mask,Point(0,0));mask:saveAs(root..'/'..id..'_mask.png') end
 if raw then local l=s:newLayer();l.name='PixelLab source - preserved';l.isVisible=false;s:newCel(l,1,raw,Point(0,0)) end
 s:saveAs(root..'/'..id..'.aseprite');im:saveAs(root..'/'..id..'.png');s:close()
end
local vessels={
 dl_jigger={{.20,.14},{.80,.14},{.60,.46},{.40,.46}},
 dl_mixing_glass={{.18,.17},{.84,.17},{.73,.87},{.28,.87}},
 dl_glass_cocktail={{.14,.20},{.85,.20},{.80,.84},{.20,.84}},
 dl_chaha={{.18,.30},{.83,.30},{.69,.67},{.33,.74}},
 dl_teapot={{.34,.42},{.62,.40},{.72,.65},{.62,.85},{.30,.85},{.23,.65}},
 dl_teacup={{.15,.29},{.85,.29},{.73,.79},{.27,.79}},
 dl_shaker_open={{.27,.33},{.73,.33},{.66,.91},{.34,.91}}
}
local defs={
 {'dl_jigger',32,48},{'dl_shaker',48,80},{'dl_mixing_glass',48,64},{'dl_glass_cocktail',48,64},{'dl_chaha',48,24},{'dl_teapot',64,56},{'dl_teacup',40,32},{'dl_water_kettle',48,56},{'dl_barspoon',16,80},
 {'dl_bottle_gin',24,56},{'dl_bottle_rum',24,56},{'dl_bottle_blue_curacao',24,56},{'dl_bottle_grenadine',24,56},{'dl_bottle_lemon_juice',24,56},{'dl_bottle_honey',24,56},{'dl_tin_black_tea',32,32},{'dl_tin_green_tea',32,32}
}
local finished={}
for _,d in ipairs(defs) do
 local path=root..'/provenance/'..d[1]..'.raw.png'
 if app.fs.isFile(path) then
 local raw=Image{fromFile=path};local im,bb=normalized(Image(raw),d[2],d[3])
 local ids={d[1]};if d[1]=='dl_shaker' then table.insert(ids,'dl_shaker_open') end
 for _,id in ipairs(ids) do
  local img=Image(im);local mask=nil
  if id=='dl_shaker_open' then for y=0,bb[2]+math.floor(bb[4]*.28) do for x=0,img.width-1 do img:drawPixel(x,y,0) end end end
  if vessels[id] then
   mask=Image(img.width,img.height,ColorMode.RGB);local pts={}
   for _,p in ipairs(vessels[id]) do table.insert(pts,{bb[1]+p[1]*bb[3],bb[2]+p[2]*bb[4]}) end
   for y=0,img.height-1 do for x=0,img.width-1 do if inside(x+.5,y+.5,pts) then mask:drawPixel(x,y,white);img:drawPixel(x,y,0) end end end
  end
  save(id,img,raw,mask);finished[id]=img
  print('finished '..id)
 end
 end
end
-- Readable codex icons reuse the matching actual ingredient container, no new substitute art.
for _,id in ipairs({'gin','rum','blue_curacao','grenadine','lemon_juice','honey','black_tea','green_tea'}) do
 local src=finished[(id=='black_tea' or id=='green_tea') and ('dl_tin_'..id) or ('dl_bottle_'..id)]
 if src then local icon=normalized(Image(src),32,32);save('dl_icon_'..id,icon,src,nil);finished['dl_icon_'..id]=icon end
end
-- Shared effects and particles are authored in Aseprite with the project palette.
local palette={outline=pc.rgba(32,29,41,255),silver=pc.rgba(167,177,194,255),cream=pc.rgba(244,241,231,255),green=pc.rgba(70,107,69,255),leaf=pc.rgba(142,173,104,255),brown=pc.rgba(113,71,50,255),amber=pc.rgba(220,174,114,255),teal=pc.rgba(82,197,193,255)}
local function dot(im,x,y,c) if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end end
local function line(im,x,y,x2,y2,c)
 local steps=math.max(math.abs(x2-x),math.abs(y2-y));if steps==0 then dot(im,x,y,c);return end
 for n=0,steps do dot(im,math.floor(x+(x2-x)*n/steps+.5),math.floor(y+(y2-y)*n/steps+.5),c) end
end
local function clip(id,w,h,n,fps,fn)
 local s=Sprite(w,h,ColorMode.RGB);s.layers[1].name='Aseprite motion pieces'
 for f=0,n-1 do if f>0 then s:newEmptyFrame() end;local im=Image(w,h,ColorMode.RGB);fn(im,f);s:newCel(s.layers[1],f+1,im,Point(0,0));s.frames[f+1].duration=1/fps;im:saveAs(root..'/'..id..'_'..string.format('%02d',f)..'.png') end
 if id=='dl_drink_reveal' then local ml=s:newLayer();ml.name='Animated liquid masks - hidden';ml.isVisible=false;for f=0,n-1 do s:newCel(ml,f+1,Image{fromFile=root..'/dl_drink_reveal_mask_'..string.format('%02d',f)..'.png'},Point(0,0)) end end
 s:saveAs(root..'/'..id..'.aseprite');app.activeSprite=s;app.command.SaveFileAs{ui=false,filename=root..'/'..id..'.gif'};s:close()
end
for _,kind in ipairs({'black_tea','green_tea'}) do
 clip('dl_leaf_'..kind,8,8,3,8,function(im,f)
  local c=kind=='green_tea' and palette.green or palette.brown;local hi=kind=='green_tea' and palette.leaf or palette.amber
  line(im,2,5-f%2,5,2+f%2,palette.outline);line(im,2,4,5,3,c);line(im,3,3,5,2,c);dot(im,3,4,hi)
 end)
 clip('dl_leaf_heap_'..kind,32,16,3,8,function(im,f)
  local c=kind=='green_tea' and palette.green or palette.brown;local hi=kind=='green_tea' and palette.leaf or palette.amber
  for j=0,5+f*4 do local x=4+(j*7)%24;local y=12-math.floor(j/4);line(im,x,y,x+2,y-1,palette.outline);dot(im,x+1,y-1,c);dot(im,x,y-1,hi) end
 end)
end
clip('dl_steam',16,24,4,8,function(im,f)
 for j=0,1 do for y=3,21 do local x=5+j*6+math.floor(math.sin((y+f*4)*.35)*2);dot(im,x,y,palette.silver);if y%3~=0 then dot(im,x+1,y,palette.cream) end end end
end)
clip('dl_pour_stream',12,48,4,12,function(im,f)
 for y=0,42 do local x=5+math.floor(math.sin((y+f*4)*.4));dot(im,x,y,palette.cream);dot(im,x+1,y,palette.silver) end
 dot(im,4+f%3,45,palette.cream);dot(im,7-f%2,47,palette.silver)
end)
clip('dl_bubbles',16,24,4,10,function(im,f)
 for j=0,3 do local x=3+(j*7)%10;local y=21-(j*4+f*3)%19;dot(im,x-1,y,palette.cream);dot(im,x,y-1,palette.cream);dot(im,x+1,y,palette.silver);dot(im,x,y+1,palette.silver) end
end)
clip('dl_ice_cube',12,12,2,6,function(im,f)
 line(im,2,3,8,2,palette.silver);line(im,8,2,10,8,palette.silver);line(im,10,8,3,10,palette.cream);line(im,3,10,2,3,palette.cream);line(im,3,4,5+f,4,palette.cream)
end)
clip('dl_buff_gain',32,32,6,12,function(im,f)
 for j=0,3 do local a=j*math.pi/2+.4;local r=4+f*2;local x=math.floor(16+math.cos(a)*r);local y=math.floor(16+math.sin(a)*r);local arm=f<4 and 2 or 1;line(im,x-arm,y,x+arm,y,palette.teal);line(im,x,y-arm,x,y+arm,palette.cream) end
end)
-- Transform reusable PixelLab parts with nearest sampling; do not generate a frame per quantity increment.
local function transformed(dest,src,cx,cy,angle,scale)
 local ca,sa=math.cos(angle),math.sin(angle)
 for y=0,dest.height-1 do for x=0,dest.width-1 do
  local dx,dy=x-cx,y-cy;local u=math.floor((dx*ca+dy*sa)/scale+src.width/2);local v=math.floor((-dx*sa+dy*ca)/scale+src.height/2)
  if u>=0 and v>=0 and u<src.width and v<src.height then local c=src:getPixel(u,v);if pc.rgbaA(c)>0 then dest:drawPixel(x,y,c) end end
 end end
end
if finished.dl_shaker then
 clip('dl_shaker_shake',64,80,6,12,function(im,f)
  local dx=({-3,0,3,2,0,-2})[f+1];local a=({-.35,0,.35,.2,0,-.2})[f+1]
  transformed(im,finished.dl_shaker,32+dx,38,a,.78)
 end)
end
if finished.dl_glass_cocktail then
 clip('dl_drink_reveal',64,64,6,10,function(im,f)
  local cx,cy=32,({37,34,32,31,32,32})[f+1]
  local z=({.72,.78,.83,.86,.84,.84})[f+1]
  transformed(im,finished.dl_glass_cocktail,cx,cy,0,z)
  local mask=Image(im.width,im.height,ColorMode.RGB)
  transformed(mask,Image{fromFile=root..'/dl_glass_cocktail_mask.png'},cx,cy,0,z)
  for p in mask:pixels() do if pc.rgbaA(im:getPixel(p.x,p.y))>0 then p(0) end end
  mask:saveAs(root..'/dl_drink_reveal_mask_'..string.format('%02d',f)..'.png')
 end)
end
if finished.dl_barspoon then
 local original=finished.dl_barspoon
 clip('dl_barspoon',16,80,4,10,function(im,f)
  local z=({1,.6,.32,.6})[f+1];local narrow=Image(original);narrow:resize{width=math.max(2,math.floor(16*z)),height=80,method='nearest'};im:drawImage(narrow,Point((16-narrow.width)//2,0))
 end)
end
-- Contact sheet, nearest 4x, labels enumerated in companion manifest.
local names={};for id,_ in pairs(finished) do table.insert(names,id) end;table.sort(names)
local sheet=Image(768,math.max(1,math.ceil(#names/6))*352,ColorMode.RGB);sheet:clear(pc.rgba(32,29,41,255))
for i,id in ipairs(names) do local im=Image(finished[id]);local z=math.min(4,120/im.width,328/im.height);im:resize{width=math.floor(im.width*z),height=math.floor(im.height*z),method='nearest'};sheet:drawImage(im,Point(((i-1)%6)*128+(128-im.width)//2,((i-1)//6)*352+8));print('contact '..i..' '..id) end
sheet:saveAs(root..'/contact-sheet.png')
