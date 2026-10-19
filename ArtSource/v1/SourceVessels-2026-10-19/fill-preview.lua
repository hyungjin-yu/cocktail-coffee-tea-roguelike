local root=app.params.root;local pc=app.pixelColor
local keys={'gin','rum','blue_curacao','grenadine','lemon_juice','honey'}
local colors={{183,216,232},{205,144,66},{0,178,224},{175,7,33},{231,224,116},{235,158,17}}
local function rot(src,angle)
 local out=Image(96,96,ColorMode.RGB);local a=angle*math.pi/180;local co,si=math.cos(a),math.sin(a)
 for y=0,95 do for x=0,95 do local dx,dy=x+.5-48,y+.5-48;local u=math.floor(dx*co+dy*si+12);local v=math.floor(-dx*si+dy*co+28)
 if u>=0 and u<24 and v>=0 and v<56 then out:drawPixel(x,y,src:getPixel(u,v)) end end end
 return out
end
for k,id in ipairs(keys) do
 local name='dl_bottle_'..id;local original=Image{fromFile=root..'/'..name..'_empty.png'};local opened=Image{fromFile=root..'/'..name..'_open_empty.png'};local mask=Image{fromFile=root..'/'..name..'_mask.png'}
 for _,angle in ipairs({0,30,60}) do
  local fg=rot(angle==0 and original or opened,angle);local rm=rot(mask,angle);local total=0;local counts={}
  for y=0,95 do local n=0;for x=0,95 do if pc.rgbaA(rm:getPixel(x,y))>0 then n=n+1 end end;counts[y]=n;total=total+n end
  for _,pct in ipairs({0,25,50,75}) do
   local im=Image(96,96,ColorMode.RGB);local target=total*pct/100;local acc=0
   for y=95,0,-1 do
    local count=counts[y];local coverage=count>0 and math.min(1,math.max(0,(target-acc)/count)) or 0
    if coverage>0 then for x=0,95 do if pc.rgbaA(rm:getPixel(x,y))>0 then local c=colors[k];im:drawPixel(x,y,pc.rgba(c[1],c[2],c[3],math.floor(coverage*255+.5))) end end end
    acc=acc+count
   end
   im:drawImage(fg,Point(0,0));im:saveAs(root..'/qa/'..name..'_'..angle..'deg_'..pct..'pct.png')
  end
 end
end
print('72 Aseprite upright/tilt fill previews saved')
