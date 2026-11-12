local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/ui-icons-2026-11-12/'
for _,name in ipairs({'research_measured_pour','research_ristretto_tea'}) do
 local sp=app.open(root..name..'.png');sp:resize(256,256);sp:saveCopyAs(root..'evidence/'..name..'.4x.png');sp:close()
end
