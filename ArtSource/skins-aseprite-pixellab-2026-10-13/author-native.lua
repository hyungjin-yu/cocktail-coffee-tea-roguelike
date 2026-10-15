local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-13/'
local data=dofile(root..'contract.lua')
local index=tonumber(app.params.index or '1')
local a=data[index]
local pc=app.pixelColor
local original=Image{fromFile=a.original}
local mask=Image{fromFile=a.mask}
local rim=a.rim and Image{fromFile=a.rim} or nil
local result=Image(original)
local function contains(im,x,y)
 local c=im:getPixel(x,y)
 if im.colorMode==ColorMode.GRAY then return pc.grayaA(c)>0 and pc.grayaV(c)>0 end
 return pc.rgbaA(c)>0 and pc.rgbaR(c)>0
end
local function ramp(l,p)
 local i=l<40 and 1 or (l<65 and 2 or (l<100 and 3 or 4))
 return p[i]
end
local navy={{6,21,74},{26,37,94},{43,69,166},{74,119,176}}
local gold={{111,80,41},{167,137,70},{222,192,103},{234,221,179}}
local steel={{15,25,57},{34,57,92},{55,87,124},{74,119,176}}
local purple={{58,29,85},{106,47,139},{152,80,176},{204,147,221}}
local copper={{75,39,30},{126,61,42},{177,82,55},{225,151,100}}
for y=0,original.height-1 do for x=0,original.width-1 do
 local c=original:getPixel(x,y)
 local r,g,b,alpha=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),pc.rgbaA(c)
 local inside=contains(mask,x,y)
 local protected=a.head and x>=a.head[1] and y>=a.head[2] and x<a.head[3] and y<a.head[4]
 if inside and alpha>0 and not protected then
  local l=(r*0.25+g*0.5+b*0.25)
  local pal=nil
  if a.kind=='body' then
   -- Geometry mask excludes face/shirt. Teal source pixels define the necktie.
   if g>r*1.35 and b>r*1.25 then pal=gold else pal=navy end
  elseif a.kind=='embedded_sword' then
   pal=(rim and contains(rim,x,y)) and gold or steel
  elseif a.skinID=='weaponskin_rapier_01' then
   pal=(g>r*1.2 or (r>g and g>b*1.2)) and gold or purple
  elseif a.skinID=='weaponskin_sword_01' then
   pal=(g>r*1.2 or (r>g and g>b*1.2)) and gold or steel
  elseif a.skinID=='weaponskin_gun_01' then
   pal=(b>r*1.25 and b>g*1.15) and purple or ((g>r*1.2) and gold or copper)
  elseif a.skinID=='weaponskin_bow_01' then
   pal=(g>r*1.2) and gold or steel
  end
  if pal then local n=ramp(l,pal); result:drawPixel(x,y,pc.rgba(n[1],n[2],n[3],alpha)) end
 end
end end
local s=Sprite(original.width,original.height,ColorMode.RGB)
s.layers[1].name='Original reference - immutable'
s.cels[1].image=original
s.layers[1].isVisible=false
local skin=s:newLayer(); skin.name='PixelLab material applied to original geometry'
s:newCel(skin,s.frames[1],result,Point(0,0))
local tag=s:newTag(s.frames[1],s.frames[1]); tag.name=app.fs.fileTitle(a.output)
local native=a.output:gsub('%.png$','.aseprite')
s:saveAs(native)
app.command.SaveFileAs{filename=a.output}
s:close()
local read=Sprite{fromFile=native}
local flat=Image(read.spec); flat:drawSprite(read,1)
flat:saveAs(a.output:gsub('%.png$','.readback.png'))
for y=0,result.height-1 do for x=0,result.width-1 do assert(result:getPixel(x,y)==flat:getPixel(x,y),'Native readback mismatch') end end
print('NATIVE_EXPORT_READBACK_OK '..index..' '..a.output)
