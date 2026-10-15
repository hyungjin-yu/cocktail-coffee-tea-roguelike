local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local out=root..'ArtSource/skins-aseprite-pixellab-2026-10-13/'
local files={
'Assets/_Project/Art/v1/weapon_rapier/frames/weapon_rapier_effect_right_00.png',
'Assets/_Project/Art/v1/weapon_gun/frames/weapon_gun_effect_right_00.png',
'Assets/_Project/Art/v1/weapon_sword/frames/weapon_sword_effect_right_00.png',
'Assets/_Project/Resources/ArtExtra/weapon_bow_v2/weapon_bow_v2_right_00.png'}
local s=Sprite(256,256,ColorMode.RGB)
local img=Image(256,256,ColorMode.RGB)
for i,p in ipairs(files) do
 local a=Image{fromFile=root..p}
 a:resize(128,128)
 img:drawImage(a,Point(((i-1)%2)*128,math.floor((i-1)/2)*128))
end
s.cels[1].image=img
s:saveAs(out..'weapons-original-input.aseprite')
img:saveAs(out..'evidence/weapons-original-input.png')
print('native original reference assembly complete')
