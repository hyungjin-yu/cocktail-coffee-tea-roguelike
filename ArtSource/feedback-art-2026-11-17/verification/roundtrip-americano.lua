local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-17/"
local records={}
do local s=app.open(root.."ready/dl_coffee_americano_mug.aseprite") s:saveCopyAs(root.."verification/roundtrip/dl_coffee_americano_mug.png") local p=s.slices[1].pivot records[#records+1]={assetId="dl_coffee_americano_mug",x=p.x,y=p.y,frames=#s.frames} s:close() end
do local s=app.open(root.."ready/dl_coffee_americano_mug_mask.aseprite") s:saveCopyAs(root.."verification/roundtrip/dl_coffee_americano_mug_mask.png") local p=s.slices[1].pivot records[#records+1]={assetId="dl_coffee_americano_mug_mask",x=p.x,y=p.y,frames=#s.frames} s:close() end
local f=assert(io.open(root.."verification/pivots-americano.json","w")) f:write(json.encode(records)) f:close()
