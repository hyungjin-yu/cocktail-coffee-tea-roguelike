local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local sprite=Sprite(128,128,ColorMode.RGB);local layer=sprite:newLayer();layer.name='Tea infusion from PixelLab pool source'
for i=0,3 do
 local source=Image{fromFile=root..'Assets/_Project/Resources/ArtExtra/zone_coffee_pool_v2/zone_coffee_pool_v2_'..string.format('%02d',i)..'.png'}
 local dst=Image(source.width,source.height,ColorMode.RGB)
 for y=0,source.height-1 do for x=0,source.width-1 do
  local c=source:getPixel(x,y);local a=app.pixelColor.rgbaA(c)
  if a>0 then
   local r,g,b=app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c)
   local l=math.floor(r*.3+g*.6+b*.1)
   if l<205 then r=math.floor(l*.55);g=math.min(255,math.floor(l*.85+35));b=math.floor(l*.5) end
   dst:drawPixel(x,y,app.pixelColor.rgba(r,g,b,a))
  end
 end end
 local frame=i==0 and sprite.frames[1] or sprite:newEmptyFrame();frame.duration=.16
 sprite:newCel(layer,frame,dst,Point(0,0))
 dst:saveAs(root..'Assets/_Project/Resources/ArtExtra/zone_tea_pool_v2/zone_tea_pool_v2_'..string.format('%02d',i)..'.png')
end
sprite:saveAs(root..'ArtSource/tea-encounters-2026-10-12/tea_pool.aseprite')
