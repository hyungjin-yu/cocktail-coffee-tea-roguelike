local root=app.params.root;local source=app.params.source;local pc=app.pixelColor
local native=app.open(source..'/dl_teapot.aseprite')
local originalNative=Image(64,56,ColorMode.RGB);originalNative:drawSprite(native,1);originalNative:saveAs(root..'/qa/original-native.png')
for _,l in ipairs(native.layers) do print('original layer: '..l.name..' visible='..tostring(l.isVisible)) end
native:close()
local raw=Image{fromFile=source..'/dl_teapot.png'};local mask=Image{fromFile=source..'/dl_teapot_mask.png'}
local lid=Image(64,56,ColorMode.RGB);local body=Image(raw);local selected=Image(64,56,ColorMode.RGB)
local function region(x,y)
 if y>=4 and y<=17 then return x>=20 and x<=44 end
 if y>=18 and y<=19 then return x>=22 and x<=41 end
 if y>=20 and y<=21 then return x>=26 and x<=39 end
 return false
end
for y=0,55 do for x=0,63 do if region(x,y) and pc.rgbaA(raw:getPixel(x,y))>0 then
 lid:drawPixel(x,y,raw:getPixel(x,y));body:drawPixel(x,y,0);selected:drawPixel(x,y,pc.rgba(255,255,255,255))
end end end
local dark=raw:getPixel(30,4);local cream=raw:getPixel(30,15);local shade=raw:getPixel(27,17)
local function dot(x,y,c)
 assert(pc.rgbaA(lid:getPixel(x,y))>0,'new rim must be hidden by exact original lid')
 body:drawPixel(x,y,c)
end
-- Shallow opening at the real seat; the original front cream lip at y22 is unchanged.
for x=26,37 do dot(x,18,cream) end
for x=24,39 do dot(x,19,(x==24 or x==39) and shade or dark) end
for x=26,39 do dot(x,20,(x==26 or x==39) and cream or dark) end
for x=27,38 do dot(x,21,dark) end
body:saveAs(root..'/dl_teapot_open_body.png');lid:saveAs(root..'/dl_teapot_lid.png');selected:saveAs(root..'/qa/lid-selection.png')
local function save(id,im)
 local s=Sprite(64,56,ColorMode.RGB);s.layers[1].name=id;s:newCel(s.layers[1],1,im,Point(0,0))
 local l=s:newLayer();l.name='Existing physical interior mask - hidden, unchanged';l.isVisible=false;s:newCel(l,1,mask,Point(0,0))
 s:saveAs(root..'/'..id..'.aseprite');s:close()
 local rt=app.open(root..'/'..id..'.aseprite');local render=Image(64,56,ColorMode.RGB);render:drawSprite(rt,1);render:saveAs(root..'/qa/'..id..'_roundtrip.png');rt:close()
end
save('dl_teapot_open_body',body);save('dl_teapot_lid',lid)
local closed=Image(body);closed:drawImage(lid,Point(0,0));closed:saveAs(root..'/qa/closed-composite.png')
-- Explicit draw order/offset previews, no preauthored angle frames required.
for _,d in ipairs({{'closed',32,40},{'lifted',32,20},{'side_table',112,68}}) do
 local im=Image(192,120,ColorMode.RGB);im:drawImage(body,Point(32,40));im:drawImage(lid,Point(d[2],d[3]));im:saveAs(root..'/qa/preview_'..d[1]..'.png')
end
print('teapot body/lid separated without editing original')
