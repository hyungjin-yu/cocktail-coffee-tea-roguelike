local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-13/'
local data=dofile(root..'contract.lua')
local pc=app.pixelColor
local function sheet(name,indices)
 local img=Image(1024,math.ceil(#indices/4)*144,ColorMode.RGB)
 for p in img:pixels() do p(pc.rgba(40,37,46,255)) end
 for n,index in ipairs(indices) do
  local a=data[index]
  local x=((n-1)%4)*256;local y=math.floor((n-1)/4)*144+12
  for side,path in ipairs({a.original,a.output}) do
   local im=Image{fromFile=path};im:resize(128,128)
   img:drawImage(im,Point(x+(side-1)*128,y))
  end
  for yy=y-8,y-4 do for xx=x,x+126 do img:drawPixel(xx,yy,pc.rgba(115,104,96,255)) end end
  for yy=y-8,y-4 do for xx=x+128,x+254 do img:drawPixel(xx,yy,pc.rgba(222,192,103,255)) end end
 end
 img:saveAs(root..name..'.png')
 local s=Sprite(img.width,img.height,ColorMode.RGB);s.cels[1].image=img;s:saveAs(root..name..'.aseprite');s:close()
end
local body={};for i=1,23 do table.insert(body,i) end
sheet('comparison_body_all23',body)
if app.params.all=='yes' then
 local swords={};for i=28,43 do table.insert(swords,i) end
 sheet('comparison_sword_all16',swords)
 sheet('comparison_overview',{1,24,25,26,27,28,31,33,35,36,39,41,43})
end
