local o='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/master-challenge-2026-11-11/'
for _,n in ipairs({'npc_porter','npc_rigger','banner_rolled','banner_unfolding','banner_settled','quality_normal','quality_excellent','quality_master'}) do local s=app.open(o..n..'_representative.aseprite');s:saveCopyAs(o..n..'_roundtrip.png');s:close() end
