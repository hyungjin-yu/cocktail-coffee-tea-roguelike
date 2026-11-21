local root=assert(app.params.root).."/"
for _,file in ipairs({"g0","street_backdrop","ct_interior_master"}) do
 local sprite=app.open(root.."ready/interior/"..file..".aseprite")
 assert(sprite.width==480 and sprite.height==270 and #sprite.frames==1)
 local pivot=sprite.slices[1].pivot
 assert(pivot.x==240 and pivot.y==135)
 sprite:saveCopyAs(root.."verification/roundtrip/"..file..".png")
 sprite:close()
end
