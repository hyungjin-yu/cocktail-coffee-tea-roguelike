local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/ui-icons-2026-11-12/'
local f=io.open(root..'specs.json','r');local specs=json.decode(f:read('*a'));f:close()
for _,s in ipairs(specs) do
 local sp=app.open(root..s.file..'.aseprite')
 assert(#sp.frames==1 and #sp.layers==2 and sp.width==64 and sp.height==64,s.file)
 assert(sp.layers[1].name=='PixelLab original - unchanged' and sp.layers[1].isVisible==false,s.file)
 sp.layers[1].isVisible=true;sp.layers[2].isVisible=false
 sp:saveCopyAs(root..'evidence/'..s.file..'.source-roundtrip.png')
 sp:close()
end
