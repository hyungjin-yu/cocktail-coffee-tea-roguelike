local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-20/'
local base='C:/Users/user/Documents/Projects/CocktailCoffeeTea/Assets/_Project/Art/v1/bartender/frames/'
local files=app.fs.listFiles(base)
local names={}
for _,f in ipairs(files) do if f:match('%.png$') then table.insert(names,f) end end
table.sort(names)
local sheet=Image(1536,1024,ColorMode.RGB)
local map=assert(io.open(root..'evidence/bartender-source-contact-order.txt','w'))
for i,f in ipairs(names) do sheet:drawImage(Image{fromFile=base..f},Point(((i-1)%6)*256,math.floor((i-1)/6)*256));map:write(i..' '..f..'\n') end
map:close();sheet:saveAs(root..'evidence/bartender-source-contact.png')
local data=dofile(root..'anatomy-contract.lua')
local tea=Image(1792,1024,ColorMode.RGB);local count=0
local order=assert(io.open(root..'evidence/tea-source-contact-order.txt','w'))
for _,a in ipairs(data) do if a.job=='tea_sommelier' then
 count=count+1;tea:drawImage(Image{fromFile=a.source},Point(((count-1)%7)*256,math.floor((count-1)/7)*256));order:write(count..' '..a.filename..'\n')
end end
order:close();tea:saveAs(root..'evidence/tea-source-contact.png')
