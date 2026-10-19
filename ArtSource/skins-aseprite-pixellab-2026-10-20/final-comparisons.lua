local root='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/skins-aseprite-pixellab-2026-10-20/'
local project='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local data=dofile(root..'anatomy-contract.lua')
local designs={{'rail','skin_bartender_rail','bartender'},{'tropic','skin_bartender_tropic','bartender'},{'noir','skin_bartender_noir','bartender'},{'alchemist','skin_bartender_alchemist','bartender'},{'stage','skin_bartender_stage','bartender'},{'mountain','skin_tea_mountain','tea_sommelier'},{'ceremony','skin_tea_ceremony','tea_sommelier'},{'apiary','skin_tea_apiary','tea_sommelier'},{'greenhouse','skin_tea_greenhouse','tea_sommelier'},{'astronomer','skin_tea_astronomer','tea_sommelier'}}
local idle=Image(1280,512,ColorMode.RGB)
local compare=Image(1920,256,ColorMode.RGB)
local mirror=Image(1280,256,ColorMode.RGB)
local tiny=Image(640,128,ColorMode.RGB)
local order=assert(io.open(root..'evidence/final-comparison-order.txt','w'))
for i,d in ipairs(designs)do
 local key,id,job=d[1],d[2],d[3];local col,row=(i-1)%5,math.floor((i-1)/5)
 local frame=job..'_idle_right_00.png'
 local current=Image{fromFile=root..'body/'..id..'/'..frame}
 idle:drawImage(current,Point(col*256,row*256))
 local original=Image{fromFile=project..'Assets/_Project/Art/v1/'..job..'/frames/'..frame}
 local previous=Image{fromFile=project..'Assets/_Project/Resources/CosmeticSkins/'..(job=='bartender' and 'skin_bartender_midnight' or 'skin_tea_jade')..'/'..frame}
 for j,im in ipairs({original,previous,Image(current)})do im:resize(128,128);compare:drawImage(im,Point(col*384+(j-1)*128,row*128))end
 local small=Image(current);small:resize(128,128);mirror:drawImage(small,Point(col*256,row*128))
 local flip=Image(small.spec)
 for y=0,127 do for x=0,127 do flip:drawPixel(127-x,y,small:getPixel(x,y))end end
 mirror:drawImage(flip,Point(col*256+128,row*128))
 small:resize(64,64);flip:resize(64,64);tiny:drawImage(small,Point(col*128,row*64));tiny:drawImage(flip,Point(col*128+64,row*64))
 local selected={};for _,a in ipairs(data)do if a.job==job then table.insert(selected,a)end end
 local sheet=Image(1536,math.ceil(#selected/6)*512,ColorMode.RGB);local reps=Image(1536,512,ColorMode.RGB);local repn=0
 for n,a in ipairs(selected)do
  -- Fresh independent Images avoid retaining cel-owned image references after a native document closes.
  local src=Image{fromFile=a.source};local out=Image{fromFile=root..'body/'..id..'/'..a.filename}
  local x,y=((n-1)%6)*256,math.floor((n-1)/6)*512
  sheet:drawImage(src,Point(x,y));sheet:drawImage(out,Point(x,y+256))
  local representative=a.filename:find('_defeat_right_03')or a.filename:find('_hurt_right_00')or a.filename:find('_idle_right_00')or a.filename:find('_move_right_00')or a.filename:find('_ranged_right_01')or (job=='bartender' and a.filename:find('_melee_right_01'))or a.filename:find('_sword_swing_right_05')
  if representative and repn<6 then reps:drawImage(src,Point(repn*256,0));reps:drawImage(out,Point(repn*256,256));repn=repn+1 end
 end
 sheet:saveAs(root..'evidence/'..key..'-all-frames.png');reps:saveAs(root..'evidence/'..key..'-representative-comparison.png')
 order:write(i..' '..id..'; triplet=original,previous,new; mirror pair=right,left\n')
end
order:close()
idle:saveAs(root..'comparison-ten-idle.png');compare:saveAs(root..'comparison-original-previous-new.png');mirror:saveAs(root..'comparison-mirrored-128.png');tiny:saveAs(root..'comparison-small-64.png')
local raw=Sprite{fromFile=root..'rail-pixellab-raw.aseprite'}
assert(#raw.frames==2)
local out=Image(raw.spec);out:drawSprite(raw,2)
local png=Image{fromFile=root..'evidence/rail-pixellab-output.png'}
local count=0
for y=0,255 do for x=0,255 do assert(out:getPixel(x,y)==png:getPixel(x,y),'Raw output readback differs');if app.pixelColor.rgbaA(out:getPixel(x,y))>0 then count=count+1 end end end
local inp=Image(raw.spec);inp:drawSprite(raw,1);local src=Image{fromFile=root..'evidence/rail-input.png'}
for y=0,255 do for x=0,255 do assert(inp:getPixel(x,y)==src:getPixel(x,y),'Raw input differs')end end
local log=assert(io.open(root..'evidence/rail-raw-readback.log','w'));log:write('RAW_NATIVE_REOPEN_INPUT_AND_OUTPUT_EXACT frames=2 seed=1927687208.0 nonzeroOutputAlpha='..count..'\n');log:close()
