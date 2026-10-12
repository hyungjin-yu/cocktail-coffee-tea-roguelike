-- Reuse the existing PixelLab body poses; edit/assemble in native Aseprite.
-- Keep originals intact. Front hand holds the bow rather than hanging at the hip.
local base = 'C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local source = base .. 'ArtSource/v1/tea_sommelier/frames/'
local out = base .. 'ArtSource/bow-ready-2026-10-12/'
app.fs.makeAllDirectories(out .. 'frames')
local pose = Image{fromFile=source..'tea_sommelier_ranged_right_01.png'}
local native = Sprite(256,256,ColorMode.RGB)
local n=0
for _,clip in ipairs({'idle','move'}) do
  local count = clip=='idle' and 4 or 6
  for i=0,count-1 do
    local legs = Image{fromFile=source..'tea_sommelier_'..clip..'_right_'..string.format('%02d',i)..'.png'}
    local result=Image(256,256,ColorMode.RGB)
    -- Join at the apron/waist: source upper body has a complete two-handed draw pose.
    for y=0,255 do for x=0,255 do
      local oldHand = y>=148 and y<174 and ((x>=78 and x<108) or (x>=154 and x<180))
      result:drawPixel(x,y,oldHand and 0 or (y<148 and pose or legs):getPixel(x,y))
    end end
    result:saveAs(out..'frames/bow_'..clip..'_right_'..string.format('%02d',i)..'.png')
    n=n+1
    if n>1 then native:newEmptyFrame() end
    native:newCel(native.layers[1], native.frames[n], result, Point(0,0))
    native.frames[n].duration=0.12
  end
end
native:saveAs(out..'bow-ready.aseprite')
