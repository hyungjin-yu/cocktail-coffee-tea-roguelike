local pc=app.pixelColor
local bg=Image(512,512,ColorMode.RGB)
for y=0,511 do
 for x=0,511 do
  local shade=math.floor(y/64)
  bg:drawPixel(x,y,pc.rgba(38+shade,25+shade,30+shade,255))
 end
end
local inputs={
 {path='Assets/_Project/Art/v1/bartender/frames/bartender_idle_right_00.png',x=32,flip=false},
 {path='Assets/_Project/Art/v1/tea_sommelier/frames/tea_sommelier_idle_right_00.png',x=256,flip=true}
}
for _,src in ipairs(inputs) do
 local im=Image{fromFile=src.path}
 local bounds=im:shrinkBounds()
 bounds.height=math.floor(bounds.height*.72)
 local portrait=Image(im,bounds)
 local scale=220/portrait.width
 portrait:resize(220,math.floor(portrait.height*scale))
 if src.flip then portrait:flip() end
 bg:drawImage(portrait,Point(src.x,82))
end
for y=398,511 do
 for x=0,511 do
  local c=(y<409) and pc.rgba(190,142,73,255) or pc.rgba(77,44,34,255)
  bg:drawPixel(x,y,c)
 end
end
local s=Sprite(512,512,ColorMode.RGB)
s:newCel(s.layers[1],1,bg,Point(0,0))
s:saveAs('ArtSource/mobile-store-2026-10-12/app-icon.aseprite')
bg:saveAs('ArtSource/mobile-store-2026-10-12/app-icon-512.png')
s:close()
