local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-20/'
local project='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
app.fs.makeDirectory(root..'sword_overlay')
local pc=app.pixelColor
local function inside(im,x,y)
 local c=im:getPixel(x,y)
 if im.colorMode==ColorMode.GRAY then return pc.grayaA(c)>0 and pc.grayaV(c)>0 end
 return pc.rgbaA(c)>0 and pc.rgbaR(c)>0
end
local report=assert(io.open(root..'evidence/sword-overlay-native-readback.log','w'))
local contact=Image(1280,512,ColorMode.RGB)
for i=3,7 do
 local name=string.format('tea_sommelier_sword_swing_right_%02d',i)
 local source=Image{fromFile=project..'Assets/_Project/Resources/WeaponSkins/weaponskin_sword_01/swing_base/'..name..'.png'}
 local mask=Image{fromFile=project..'ArtSource/skins-2026-10-13/v2/masks/sword_swing/swing_base/'..name..'.png'}
 local overlay=Image(source.spec)
 local count=0
 for y=0,255 do for x=0,255 do
  if inside(mask,x,y) then local c=source:getPixel(x,y);overlay:drawPixel(x,y,c);if pc.rgbaA(c)>0 then count=count+1 end end
 end end
 assert(count>0,'Empty sword overlay')
 contact:drawImage(source,Point((i-3)*256,0))
 contact:drawImage(overlay,Point((i-3)*256,256))
 local s=Sprite(256,256,ColorMode.RGB)
 s.layers[1].name='Approved sword pixels - original coordinates';s.cels[1].image=overlay
 local ref=s:newLayer();ref.name='Approved full-frame reference - hidden';s:newCel(ref,s.frames[1],source);ref.isVisible=false
 local tag=s:newTag(1,1);tag.name=name
 local native=root..'sword_overlay/'..name..'.aseprite'
 s:saveAs(native)
 app.command.SaveFileAs{filename=root..'sword_overlay/'..name..'.png'}
 s:close()
 local reopened=Sprite{fromFile=native};local flat=Image(reopened.spec);flat:drawSprite(reopened,1)
 for y=0,255 do for x=0,255 do assert(flat:getPixel(x,y)==overlay:getPixel(x,y),'Native readback differs') end end
 reopened:close()
 report:write(name..' NATIVE_EXPORT_READBACK_OK nonzeroAlpha='..count..'\n')
end
report:close()
contact:saveAs(root..'evidence/sword-overlay-comparison.png')
