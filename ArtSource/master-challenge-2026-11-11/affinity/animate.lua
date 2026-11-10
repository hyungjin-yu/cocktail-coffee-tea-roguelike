local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/'
local out=root..'ArtSource/master-challenge-2026-11-11/affinity/'
local pc=app.pixelColor
local function col(h) return Color{r=tonumber(h:sub(1,2),16),g=tonumber(h:sub(3,4),16),b=tonumber(h:sub(5,6),16),a=255}.rgbaPixel end
local O=col('30232f');local C=col('f4e4bf');local L=col('fff6da');local M=col('c6a878');local S=col('927451');local G=col('d9b66d')
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and yy>=0 and xx<im.width and yy<im.height then im:drawPixel(xx,yy,c) end end end end
local function poly(im,pts,c)
 local ymin=im.height;local ymax=0;for _,p in ipairs(pts) do ymin=math.min(ymin,p[2]);ymax=math.max(ymax,p[2]) end
 for y=math.max(0,ymin),math.min(im.height-1,ymax) do local xs={};for i,p in ipairs(pts) do local q=pts[i%#pts+1];if (p[2]<=y and q[2]>y) or (q[2]<=y and p[2]>y) then xs[#xs+1]=p[1]+(y-p[2])*(q[1]-p[1])/(q[2]-p[2]) end end; table.sort(xs);for i=1,#xs-1,2 do for x=math.max(0,math.ceil(xs[i])),math.min(im.width-1,math.floor(xs[i+1])) do im:drawPixel(x,y,c) end end end
end
local function layer(sp,name,im) local l=sp:newLayer();l.name=name;sp:newCel(l,1,im,Point(0,0));return l end
local function save(sp,name) sp:saveAs(out..name..'.aseprite');sp:saveCopyAs(out..name..'.png');sp:close() end
local function rolled(im,x,y,w,h)
 rect(im,x+3,y,w-6,h,O);rect(im,x,y+3,w,h-6,O);rect(im,x+4,y+3,w-8,h-6,M);rect(im,x+6,y+3,w-12,3,L);rect(im,x+6,y+7,w-12,h-12,C);rect(im,x+6,y+h-6,w-12,3,S)
 rect(im,x+12,y+2,5,h-4,S);rect(im,x+13,y+2,2,h-4,G);rect(im,x+w-18,y+2,5,h-4,S);rect(im,x+w-17,y+2,2,h-4,G)
end

-- Shape representatives only, effects point upward-right from hit centre.
local function bubble(im,x,y,r)
 for yy=-r,r do for xx=-r,r do local ds=xx*xx+yy*yy;if ds<=r*r and ds>=(r-2)*(r-2) then im:drawPixel(x+xx,y+yy,col(yy<0 and xx<2 and 'fff0db' or yy>2 and '598e8b' or 'a8d4ce')) end end end
 rect(im,x-r+2,y-r+3,2,3,col('fff9e5'))
end
local function bean(im,x,y,k)
 local pts={{-9,-8},{-4,-13},{3,-14},{9,-9},{11,-2},{8,7},{2,13},{-5,13},{-11,8},{-13,1}}
 local function shape(t,c) local q={};for _,v in ipairs(t) do q[#q+1]={math.floor(x+v[1]*k),math.floor(y+v[2]*k)} end;poly(im,q,col(c)) end
 shape(pts,'42302c');shape({{-8,-6},{-3,-11},{2,-11},{7,-7},{8,-1},{5,6},{0,10},{-5,10},{-9,6},{-10,0}},'98664b')
 shape({{-7,-5},{-2,-9},{2,-9},{4,-7},{0,-5},{-3,-1},{-4,4},{-7,6},{-8,1}},'c59165')
 shape({{2,-10},{4,-9},{2,-3},{-2,2},{-2,7},{-5,10},{-4,4},{-5,0},{0,-5}},'4e332d')
 shape({{4,-8},{5,-6},{2,-1},{-1,3},{-1,7},{-3,9},{-2,2},{1,-3}},'e5b77c');rect(im,math.floor(x-7*k),math.floor(y-6*k),2,2,col('f2d49b'))
end
local function leaf(im,x,y,k,flip)
 local function sh(t,c) local q={};for _,v in ipairs(t) do q[#q+1]={math.floor(x+v[1]*k*(flip and -1 or 1)),math.floor(y+v[2]*k)} end;poly(im,q,col(c)) end
 sh({{-17,10},{-10,-4},{-1,-10},{17,-13},{10,3},{1,12},{-8,14}},'354438')
 sh({{-13,9},{-8,-2},{0,-7},{13,-10},{6,2},{0,9},{-7,11}},'6e9564')
 sh({{-13,8},{-7,-1},{1,-6},{11,-8},{2,0},{-4,5}},'b1cb83')
 sh({{-13,9},{-2,2},{12,-9},{9,-3},{0,5},{-9,11}},'456540')
end

local function blank() return Image(128,128,ColorMode.RGB) end
local function transformedPoly(im,pts,color,k,dy)
 local q={};for _,v in ipairs(pts) do q[#q+1]={math.floor(64+(v[1]-64)*k+.5),math.floor(64+(v[2]-64)*k+dy+.5)} end;poly(im,q,col(color))
end
local function exportClip(sp,name)
 local tag=sp:newTag(sp.frames[1],sp.frames[#sp.frames]);tag.name=name
 for i=1,#sp.frames do local im=Image(sp.spec);im:drawSprite(sp,i);im:saveAs(out..'frames/'..name..'_'..string.format('%02d',i-1)..'.png') end
 sp:saveAs(out..name..'.aseprite');sp:close()
end
local function makeClip(name,low)
 local count=low and 3 or 6;local sp=Sprite(128,128,ColorMode.RGB);sp.layers[1].name='affinity shape';local trail=sp:newLayer();trail.name='short supporting motion'
 for i=1,count do
  if i>1 then sp:newEmptyFrame() end;sp.frames[i].duration=.083
  local im,t=blank(),blank()
  if i<count then
   local k=low and (i==1 and .75 or .5) or ({.35,.7,1,.75,.4})[i]
   local spread=low and 1 or ({.25,.6,1,1.15,1.3})[i]
   local drift=math.max(0,i-3)*3
   if name=='cocktail_foam' then
    if low then bubble(im,64+(i-1)*3,64-(i-1)*4,i==1 and 7 or 5);bubble(im,76+(i-1)*4,53-(i-1)*4,i==1 and 4 or 3)
    else
     bubble(im,math.floor(64-10*spread),math.floor(64+8*spread)-drift,math.max(3,math.floor(10*k)))
     if i>=2 then bubble(im,math.floor(64+9*spread),math.floor(64-7*spread)-drift,math.max(3,math.floor(8*k))) end
     if i>=3 then bubble(im,math.floor(64+24*spread),math.floor(64-25*spread)-drift,math.max(3,math.floor(6*k)));bubble(im,math.floor(64+24*spread),math.floor(64+7*spread)-drift,3) end
     if i==2 or i==3 then transformedPoly(t,{{39,85},{44,82},{49,84},{52,88},{48,91},{41,89}},'dbe9d1',spread,0) end
    end
   elseif name=='coffee_bean_steam' then
    if low then bean(im,64+(i-1)*4,64-(i-1)*5,k)
    else
     if i<=4 then bean(im,math.floor(64-3*spread),math.floor(64+8*spread)-drift,k) end
     if i>=2 then bean(im,math.floor(64+19*spread),math.floor(64-10*spread)-drift,k*.7) end
     if i>=2 then
      transformedPoly(t,{{48,60},{42,53},{43,46},{50,40},{54,34},{52,27},{56,31},{57,37},{51,44},{47,48},{47,53},{53,57}},'b6a58b',spread,3-drift)
      transformedPoly(t,{{48,58},{45,52},{46,47},{53,40},{55,34},{55,38},{49,45},{47,50},{49,55},{53,58}},'f4e4bf',spread,3-drift)
     end
    end
   else
    if low then leaf(im,64+(i-1)*5,64-(i-1)*4,k,false);transformedPoly(t,{{47,74},{53,77},{61,76},{66,73},{61,79},{54,80},{48,77}},'d0dcb1',1,-(i-1)*4)
    else
     leaf(im,math.floor(64-4*spread),math.floor(64+4*spread)-drift,k*.85,false)
     if i>=2 then leaf(im,math.floor(64+25*spread),math.floor(64-17*spread)-drift,k*.55,true) end
     if i>=2 then
      transformedPoly(t,{{34,83},{44,89},{57,91},{71,87},{84,78},{94,65},{99,51},{98,63},{92,77},{80,89},{65,96},{52,97},{40,92}},'718f78',spread,0)
      if i<5 then transformedPoly(t,{{36,83},{45,87},{58,89},{71,85},{84,77},{93,65},{92,71},{82,84},{68,92},{54,94},{44,90}},'d0dcb1',spread,0) end
     end
    end
   end
  end
  if i==1 then sp.cels[1].image=im else sp:newCel(sp.layers[1],i,im,Point(0,0)) end
  sp:newCel(trail,i,t,Point(0,0))
 end
 exportClip(sp,name..(low and '_low' or ''))
end
for _,n in ipairs({'cocktail_foam','coffee_bean_steam','tea_leaf_wind'}) do makeClip(n,false);makeClip(n,true) end
