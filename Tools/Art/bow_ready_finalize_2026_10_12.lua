local base='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local out=base..'ArtSource/bow-ready-2026-10-12/'
local src=base..'ArtSource/v1/tea_sommelier/frames/'
local native=Sprite(256,256,ColorMode.RGB)
local n=0
local function add(im,name,duration)
  im:saveAs(out..'frames/'..name..'.png')
  n=n+1;if n>1 then native:newEmptyFrame() end
  native:newCel(native.layers[1],native.frames[n],im,Point(0,0))
  native.frames[n].duration=duration
end
add(Image{fromFile=src..'tea_sommelier_ranged_right_01.png'},'bow_idle_right_00',.12)
for i=0,5 do add(Image{fromFile=out..'frames/tea_sommelier_move_right_'..string.format('%02d',i)..'.png'},'bow_move_right_'..string.format('%02d',i),.12) end
for i=0,3 do local p=i==2 and 2 or 1
  add(Image{fromFile=src..'tea_sommelier_ranged_right_'..string.format('%02d',p)..'.png'},'bow_ranged_right_'..string.format('%02d',i),.09)
end
native:saveAs(out..'bow-ready.aseprite')
