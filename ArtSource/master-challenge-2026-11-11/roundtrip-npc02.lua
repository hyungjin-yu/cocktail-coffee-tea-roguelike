local o='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/master-challenge-2026-11-11/'
for _,n in ipairs({'npc_porter','npc_rigger'}) do local s=app.open(o..n..'_revision02.aseprite');s:saveCopyAs(o..n..'_revision02_roundtrip.png');s:close() end
