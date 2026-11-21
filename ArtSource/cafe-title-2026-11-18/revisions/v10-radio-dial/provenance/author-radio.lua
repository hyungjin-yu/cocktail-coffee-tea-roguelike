local root=assert(app.params.root).."/"
local original=Image{fromFile=assert(app.params.original)}
local sprite=Sprite(480,270,ColorMode.RGB)
local pc=app.pixelColor
local function color(r,g,b) return pc.rgba(r,g,b,255) end
local rim=color(88,52,33)
local bevel=color(199,148,90)
local brass=color(166,111,64)
local lower=color(129,78,44)
local notch=color(44,24,16)
local dial={
 {nil,bevel,bevel,bevel,nil},
 {bevel,brass,brass,brass,rim},
 {bevel,brass,brass,brass,rim},
 {rim,brass,brass,lower,rim},
 {nil,rim,rim,rim,nil}
}
local direction={
 {{-1,1},{-2,1}},
 {{-1,-1},{-2,-1}},
 {{0,-1},{0,-2}},
 {{1,-1},{2,-1}},
 {{1,-1},{2,-1}}
}
local lamp={
 {color(130,79,35),color(102,59,29)},
 {color(143,83,35),color(113,65,31)},
 {color(168,98,39),color(126,72,32)},
 {color(212,136,57),color(159,89,36)},
 {color(255,201,111),color(216,143,59)}
}
sprite.layers[1].name='Original radio and sideboard'
local controls=sprite:newLayer() controls.name='Fixed-axis dial and pilot lamp'
local pivot=sprite:newSlice(Rectangle(0,0,480,270))
pivot.name='canvas_pivot' pivot.pivot=Point(240,135)
for i=1,6 do
 if i>1 then sprite:newEmptyFrame() end
 sprite:newCel(sprite.layers[1],i,original:clone())
 if i>1 then
  local drawing=Image(5,7,ColorMode.RGB)
  for y=1,5 do for x=1,5 do
   local ink=dial[y][x]
   if ink then drawing:drawPixel(x-1,y-1,ink) end
  end end
  drawing:drawPixel(2,2,notch)
  for _,p in ipairs(direction[i-1]) do drawing:drawPixel(2+p[1],2+p[2],notch) end
  drawing:drawPixel(1,5,lamp[i-1][1])
  drawing:drawPixel(2,5,lamp[i-1][2])
  drawing:drawPixel(1,6,lamp[i-1][2])
  drawing:drawPixel(2,6,lamp[i-1][2])
  sprite:newCel(controls,i,drawing,Point(244,102))
 end
 sprite.frames[i].duration=.100
end
local stem=root.."ready/approach/settings"
sprite:saveAs(stem..".aseprite") sprite:close()
local reopened=app.open(stem..".aseprite")
assert(#reopened.frames==6 and reopened.slices[1].pivot==Point(240,135))
for i=1,6 do
 local img=Image(480,270,ColorMode.RGB) img:drawSprite(reopened,i)
 local frame=Sprite(480,270,ColorMode.RGB) frame:newCel(frame.layers[1],1,img)
 frame:saveCopyAs(stem..string.format("_%02d.png",i-1)) frame:close()
end
reopened:close()
