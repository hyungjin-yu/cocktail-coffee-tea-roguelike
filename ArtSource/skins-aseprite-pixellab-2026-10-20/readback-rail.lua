local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-20/'
local s=Sprite{fromFile=root..'rail-pixellab-raw.aseprite'}
assert(#s.frames==2,'Expected input and generated frames')
for i,name in ipairs({'rail-native-input.png','rail-pixellab-output.png'}) do
 local im=Image(s.spec);im:drawSprite(s,i);im:saveAs(root..'evidence/'..name)
end
local input=Image{fromFile=root..'evidence/rail-input.png'}
local read=Image{fromFile=root..'evidence/rail-native-input.png'}
for y=0,255 do for x=0,255 do assert(input:getPixel(x,y)==read:getPixel(x,y),'Input changed') end end
local out=Image{fromFile=root..'evidence/rail-pixellab-output.png'}
local n=0
for y=0,255 do for x=0,255 do if app.pixelColor.rgbaA(out:getPixel(x,y))>0 then n=n+1 end end end
assert(n>0,'Blank generated output')
print('RAW_NATIVE_REOPEN_INPUT_EXACT frames=2 outputNonzeroAlpha='..n)
