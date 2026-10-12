local base='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local out=base..'ArtSource/bow-ready-2026-10-12/'
local bow=Image{fromFile=base..'Assets/_Project/Resources/ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00.png'}
local preview=Image(768,256,ColorMode.RGB)
for i=0,2 do
  local file=i==0 and base..'ArtSource/v1/tea_sommelier/frames/tea_sommelier_idle_right_00.png'
    or out..'frames/'..(i==1 and 'bow_idle_right_00.png' or 'bow_move_right_00.png')
  local im=Image{fromFile=file}
  local hx,hy=i==0 and 162 or 198,i==0 and 168 or 103
  local angle=math.rad(i==0 and -12 or 0)
  for y=0,255 do for x=0,255 do
    local c=bow:getPixel(x,y)
    if app.pixelColor.rgbaA(c)>0 then
      local dx=-(x-76)*.48;local dy=(y-128)*.48
      local xx=math.floor(hx+dx*math.cos(angle)+dy*math.sin(angle)+.5)
      local yy=math.floor(hy-dx*math.sin(angle)+dy*math.cos(angle)+.5)
      if xx>=0 and xx<256 and yy>=0 and yy<256 then im:drawPixel(xx,yy,c) end
    end
  end end
  preview:drawImage(im,Point(i*256,0))
end
preview:saveAs(out..'before-after-ready.png')
