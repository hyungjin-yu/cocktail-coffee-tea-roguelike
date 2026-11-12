local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/ui-icons-2026-11-12/'
local sf=io.open(root..'specs.json','r');local specs=json.decode(sf:read('*a'));sf:close()
local names={};for _,s in ipairs(specs) do if app.fs.isFile(root..'raw/'..s.file..'.png') then names[#names+1]=s.file end end
local pc=app.pixelColor
for _,name in ipairs(names) do
 if not app.fs.isFile(root..name..'.aseprite') then
 local sp=Sprite{fromFile=root..'raw/'..name..'.png',oneFrame=true}
 while #sp.frames>1 do sp:deleteFrame(sp.frames[#sp.frames]) end
 local src=sp.cels[1].image:clone(); local origin=sp.cels[1].position
 sp.layers[1].name='PixelLab original - unchanged';sp.layers[1].isVisible=false
 local clean=Image(64,64,ColorMode.RGB);clean:drawImage(src,origin)
 local x0,y0,x1,y1=64,64,-1,-1
 for it in clean:pixels() do
  local p=it();local a=pc.rgbaA(p)
  if a<128 then it(0) else it(pc.rgba(pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p),255));x0=math.min(x0,it.x);y0=math.min(y0,it.y);x1=math.max(x1,it.x);y1=math.max(y1,it.y) end
 end
 assert(x1>=x0,'Empty source '..name)
 local crop=Image(clean,Rectangle(x0,y0,x1-x0+1,y1-y0+1))
 local scale=math.min(56/crop.width,56/crop.height)
 crop:resize{width=math.floor(crop.width*scale+.5),height=math.floor(crop.height*scale+.5),method='nearest'}
 local layer=sp:newLayer();layer.name='UI icon - alpha cleaned and centered';sp:newCel(layer,1,crop,Point(math.floor((64-crop.width)/2),math.floor((64-crop.height)/2)))
 sp:saveAs(root..name..'.aseprite');sp:saveCopyAs(root..name..'.png');sp:close()
 local reopened=app.open(root..name..'.aseprite');assert(#reopened.frames==1,'Must be static '..name);reopened:saveCopyAs(root..'evidence/'..name..'.roundtrip.png');reopened:close()
 end
end
local cols=math.min(8,#names);local rows=math.ceil(#names/cols)
local sheet=Sprite(cols*80,rows*80,ColorMode.RGB);sheet.layers[1].name='Icons in specs order - left to right then down'
for i,name in ipairs(names) do local src=Sprite{fromFile=root..name..'.png',oneFrame=true};local im=Image(src.spec);im:drawSprite(src,1);sheet.cels[1].image:drawImage(im,Point(((i-1)%cols)*80+8,math.floor((i-1)/cols)*80+8));src:close() end
sheet:saveCopyAs(root..'evidence/contact-1x.png');sheet:resize(cols*80*4,rows*80*4);sheet:saveCopyAs(root..'evidence/contact-4x.png');sheet:close()
for _,group in ipairs({'card','research','research_tea','mastery'}) do
 local subset={};for _,s in ipairs(specs) do if s.group==group and app.fs.isFile(root..s.file..'.png') then subset[#subset+1]=s.file end end
 if #subset>0 then
  local cols=4;local rows=math.ceil(#subset/cols);local sp=Sprite(cols*80,rows*80,ColorMode.RGB)
  for i,name in ipairs(subset) do local src=Sprite{fromFile=root..name..'.png',oneFrame=true};local im=Image(src.spec);im:drawSprite(src,1);sp.cels[1].image:drawImage(im,Point(((i-1)%cols)*80+8,math.floor((i-1)/cols)*80+8));src:close() end
  sp:saveCopyAs(root..'evidence/'..group..'-1x.png');sp:resize(cols*320,rows*320);sp:saveCopyAs(root..'evidence/'..group..'-4x.png');sp:close()
 end
end
