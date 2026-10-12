local project='C:/Users/user/Documents/Projects/CocktailCoffeeTea/'
local root=project..'ArtSource/tea-encounters-2026-10-12/'
local specs={
 {id='gaiwan_sentinel',world=2.35,extra={'charge_prepare','charge'}},
 {id='steam_kettle_elder',world=2.4,extra={'drip','vanish','appear'}},
 {id='tea_kiln_dragon',world=3.1,extra={'pressure','steam','rain'}},
 {id='spring_leaf_sprite',world=1.15,extra={'action'}},
 {id='bamboo_scoop_archer',world=1.2,extra={'action'}}
}
local function bounds(im)
 local x0,y0,x1,y1=im.width,im.height,0,0
 for y=0,im.height-1 do for x=0,im.width-1 do
  if app.pixelColor.rgbaA(im:getPixel(x,y))>24 then x0=math.min(x0,x);y0=math.min(y0,y);x1=math.max(x1,x+1);y1=math.max(y1,y+1) end
 end end
 return x0,y0,x1,y1
end
local function transform(im,scaleY,alpha,lean)
 local dst=Image(128,128,ColorMode.RGB)
 for y=0,127 do for x=0,127 do
  local sy=math.floor(117-(117-y)/scaleY+.5)
  local sx=math.floor(x-lean*(117-y)/100+.5)
  if sx>=0 and sx<128 and sy>=0 and sy<128 then
   local c=im:getPixel(sx,sy)
   local a=app.pixelColor.rgbaA(c)
   if a>0 then dst:drawPixel(x,y,app.pixelColor.rgba(app.pixelColor.rgbaR(c),app.pixelColor.rgbaG(c),app.pixelColor.rgbaB(c),math.floor(a*alpha+.5))) end
  end
 end end
 return dst
end
for _,spec in ipairs(specs) do
 local base={};local minY=127;local dx
 for i=0,3 do
  local im=Image{fromFile=root..spec.id..'/raw/'..i..'.png'}
  local x0,y0,x1,y1=bounds(im)
  if not dx then dx=math.floor(64-(x0+x1)/2+.5) end
  local aligned=Image(128,128,ColorMode.RGB)
  aligned:drawImage(im,Point(dx,118-y1));base[#base+1]=aligned
  local _,alignedY=bounds(aligned);minY=math.min(minY,alignedY)
 end
 local sprite=Sprite(128,128,ColorMode.RGB);local layer=sprite:newLayer();layer.name='PixelLab body and native variants'
 local clips={'idle','move'};for _,clip in ipairs(spec.extra) do clips[#clips+1]=clip end;clips[#clips+1]='defeat'
 local total=0;local preview=Image(512,128,ColorMode.RGB)
 for i,im in ipairs(base) do preview:drawImage(im,Point((i-1)*128,0)) end
 preview:saveAs(root..spec.id..'/review.png')
 local manifest=io.open(root..spec.id..'/runtime.json','w')
 manifest:write('{"assetId":"'..spec.id..'","bodyHeightPx":'..(118-minY)..',"targetBodyHeightWorld":'..spec.world..',"footPixelY":10,"clips":[')
 for ci,clip in ipairs(clips) do
  local count=4;local fps=clip=='idle' and 4 or clip=='move' and 8 or 8
  local loop=clip=='idle' or clip=='move' or clip=='charge'
  if ci>1 then manifest:write(',') end
  manifest:write('{"name":"'..clip..'","frames":'..count..',"fps":'..fps..',"loop":'..tostring(loop)..'}')
  local start=total+1
  for i=0,count-1 do
   local im=base[i%4+1]
   if clip=='defeat' then im=transform(im,math.max(.08,1-i*.3),1-i/3,0)
   elseif clip=='vanish' then im=transform(im,1,1-i/3,0)
   elseif clip=='appear' then im=transform(im,1,(i+1)/4,0)
   elseif clip=='charge_prepare' then im=transform(im,1,1,i*2)
   elseif clip=='charge' then im=transform(im,1,1,8)
   elseif clip=='pressure' then im=transform(im,1+(i%3)*.02,1,0)
   end
   local frame=total==0 and sprite.frames[1] or sprite:newEmptyFrame()
   frame.duration=1/fps;sprite:newCel(layer,frame,im,Point(0,0));total=total+1
   im:saveAs(project..'Assets/_Project/Resources/ArtExtra/'..spec.id..'/'..spec.id..'_'..clip..string.format('_%02d.png',i))
  end
  local tag=sprite:newTag(start,total);tag.name=clip
 end
 manifest:write(']}');manifest:close()
 sprite:saveAs(root..spec.id..'/'..spec.id..'_authored.aseprite')
end
