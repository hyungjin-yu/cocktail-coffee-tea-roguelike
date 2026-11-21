local root=assert(app.params.root).."/"
local base=assert(app.params.base).."/"
local file=assert(io.open(root.."verification/rects.json","r"))
local rows=json.decode(file:read("*a")) file:close()
for n,row in ipairs(rows) do
 local x,y,w,h=table.unpack(row.rect)
 local original=Image{fromFile=base.."ready/interior/g"..n..".png"}
 local sprite=Sprite(480,270,ColorMode.RGB)
 local art=sprite.layers[1] art.name="Original object layer"
 local delta=sprite:newLayer() delta.name="Action and revealed background"
 local pivot=sprite:newSlice(Rectangle(0,0,480,270))
 pivot.name="canvas_pivot" pivot.pivot=Point(240,135)
 local stem=root.."ready/approach/"..row.id
 for i=1,6 do
  if i>1 then sprite:newEmptyFrame() end
  sprite:newCel(art,i,original:clone())
  local patch=Image{fromFile=root.."ready/actions/"..row.id..string.format("_%02d.png",i-1)}
  assert(patch.width==w and patch.height==h)
  sprite:newCel(delta,i,patch,Point(x,y))
  sprite.frames[i].duration=.100
 end
 sprite:saveAs(stem..".aseprite")
 sprite:close()
 local reopened=app.open(stem..".aseprite")
 assert(#reopened.frames==6 and reopened.slices[1].pivot==Point(240,135))
 for i=1,6 do
  local composite=Image(480,270,ColorMode.RGB)
  composite:drawSprite(reopened,i)
  local export=Sprite(480,270,ColorMode.RGB)
  export:newCel(export.layers[1],1,composite)
  export:saveCopyAs(stem..string.format("_%02d.png",i-1))
  export:close()
 end
 reopened:close()
end
