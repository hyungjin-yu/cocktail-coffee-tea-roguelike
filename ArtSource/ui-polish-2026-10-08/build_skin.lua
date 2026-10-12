-- Pixel authoring and PNG/native export exclusively in Aseprite.
local root=app.params.root
local pc=app.pixelColor
local function C(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local ink=C('201D29');local wood=C('714732');local shade=C('503C32');local gold=C('DCAE72');local cream=C('F4F1E7');local paper=C('EAD4A5');local dark=C('302028');local teal=C('52C5C1');local muted=C('79624E')
local entries={}
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and yy>=0 and xx<im.width and yy<im.height then im:drawPixel(xx,yy,c) end end end end
local function step(im,inset,c)
 local w,h=im.width,im.height;rect(im,inset+4,inset,w-2*inset-8,h-2*inset,c);rect(im,inset+2,inset+2,w-2*inset-4,h-2*inset-4,c);rect(im,inset,inset+4,w-2*inset,h-2*inset-8,c)
end
local function border(im,state,b,center)
 step(im,0,ink);step(im,2,state=='disabled' and shade or wood);step(im,4,state=='hover' and teal or gold);step(im,6,state=='pressed' and ink or shade);step(im,8,center or dark)
 local w,h=im.width,im.height
 if state~='disabled' then
  for _,x in ipairs({4,w-8}) do for _,y in ipairs({4,h-8}) do rect(im,x,y,4,4,state=='hover' and cream or gold);rect(im,x,y,2,2,cream) end end
 end
 if state=='hover' then
  -- Four inward corner brackets provide a shape cue independent of hue.
  local p=b-4
  for _,x in ipairs({p,w-b}) do for _,y in ipairs({p,h-b}) do rect(im,x,y,4,2,cream);rect(im,x,y,2,4,cream) end end
 elseif state=='pressed' then
  rect(im,10,8,w-20,2,ink);rect(im,8,10,2,h-20,ink);rect(im,10,h-10,w-20,2,gold)
 elseif state=='disabled' then
  for _,x in ipairs({4,w-8}) do rect(im,x,4,4,2,muted);rect(im,x,h-6,4,2,muted) end
 end
end
local function save(id,w,h,b,kind,state,draw)
 local im=Image(w,h,ColorMode.RGB);draw(im)
 local dir=root..'/'..id;app.fs.makeAllDirectories(dir)
 local s=Sprite(w,h,ColorMode.RGB);s.layers[1].name='Cafe skin - editable pixels';s.cels[1].image=im
 local sl=s:newSlice(Rectangle(0,0,w,h));sl.name='NineSlice';sl.center=Rectangle(b,b,w-2*b,h-2*b)
 s:saveAs(dir..'/'..id..'.aseprite');im:saveAs(dir..'/'..id..'.png');s:close()
 entries[#entries+1]={assetId=id,kind=kind,state=state,frameSizePx={w,h},border={b,b,b,b},file=id..'/'..id..'.png',native=id..'/'..id..'.aseprite',textBaked=false,status='pending',acceptance={sourceReady=false,unityVerified=false}}
end
save('ui_panel',96,96,12,'panel','normal',function(im) border(im,'normal',12);rect(im,10,10,76,2,paper);rect(im,10,84,76,2,shade) end)
for _,state in ipairs({'normal','hover','pressed','disabled'}) do
 save('ui_button_'..state,128,40,10,'button',state,function(im) border(im,state,10,state=='disabled' and C('3B3035') or dark) end)
 save('ui_card_'..state,96,128,12,'card',state,function(im) border(im,state,12);rect(im,10,10,76,2,paper);rect(im,10,116,76,2,paper) end)
end
save('ui_card_header',96,24,8,'card_header','normal',function(im) step(im,0,ink);step(im,2,shade);step(im,4,paper);rect(im,8,6,80,2,cream) end)
for name,col in pairs({cocktail=teal,tea=C('86AB68'),coffee=gold,neutral=paper}) do
 for _,state in ipairs({'normal','selected'}) do
  save('ui_tab_'..name..'_'..state,80,32,8,'tab',state,function(im)
   step(im,0,ink);step(im,2,shade);step(im,4,state=='selected' and col or muted);step(im,6,dark)
   if state=='selected' then rect(im,8,24,64,4,col);rect(im,6,8,2,14,cream);rect(im,72,8,2,14,cream) end
  end)
 end
end
save('ui_bar_frame',128,16,6,'bar_frame','normal',function(im) step(im,0,ink);step(im,2,gold);step(im,4,shade);rect(im,6,6,116,4,0) end)
for name,col in pairs({hp=C('EF6558'),cocktail=teal,tea=C('86AB68'),coffee=gold}) do
 save('ui_bar_fill_'..name,128,16,6,'bar_fill','normal',function(im) rect(im,0,0,128,16,col);rect(im,0,0,128,2,cream);rect(im,0,14,128,2,shade) end)
end
save('ui_selection',96,96,12,'selection','selected',function(im)
 step(im,0,ink);step(im,2,teal);step(im,4,cream);step(im,6,0)
 for _,x in ipairs({8,84}) do for _,y in ipairs({8,84}) do rect(im,x,y,4,2,teal);rect(im,x,y,2,4,teal) end end
end)
local f=assert(io.open(root..'/skin-manifest.json','w'));f:write(json.encode({version='ui-polish-2026-10-08',tool='Aseprite 1.3.18.6 Lua',pixelsPerUnit=128,filterMode='Point',compression='None',mipmaps=false,alpha='RGBA8 binary alpha',borderOrder='left,bottom,right,top',assets=entries}));f:close()
print('Created '..#entries..' native skin assets')
