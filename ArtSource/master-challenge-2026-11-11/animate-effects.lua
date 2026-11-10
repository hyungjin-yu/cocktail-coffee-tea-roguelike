local root='C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/'
local out=root..'ArtSource/master-challenge-2026-11-11/'
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
local function star(im,x,y,size)
 poly(im,{{x,y-size},{x+3,y-3},{x+size,y},{x+3,y+3},{x,y+size},{x-3,y+3},{x-size,y},{x-3,y-3}},S)
 poly(im,{{x,y-size+2},{x+2,y-2},{x+size-2,y},{x+2,y+2},{x,y+size-2},{x-2,y+2},{x-size+2,y},{x-2,y-2}},G)
 poly(im,{{x,y-size+4},{x+1,y-1},{x+size-4,y},{x+1,y+1},{x,y+size-4},{x-1,y+1},{x-size+4,y},{x-1,y-1}},L)
end

local function blank(w,h) return Image(w,h,ColorMode.RGB) end
local function newClip(name,w,h,layerNames)
 local sp=Sprite(w,h,ColorMode.RGB);sp.layers[1].name=layerNames[1];for i=2,#layerNames do local l=sp:newLayer();l.name=layerNames[i] end;return sp
end
local function setFrame(sp,i,ims,dt)
 if i>1 then sp:newEmptyFrame() end;sp.frames[i].duration=dt
 for j,im in ipairs(ims) do if i==1 and j==1 then sp.cels[1].image=im else sp:newCel(sp.layers[j],i,im,Point(0,0)) end end
end
local function exportClip(sp,name)
 for i=1,#sp.frames do local im=Image(sp.spec);im:drawSprite(sp,i);im:saveAs(out..'frames/'..name..'_'..string.format('%02d',i-1)..'.png') end
 local tag=sp:newTag(sp.frames[1],sp.frames[#sp.frames]);tag.name=name;sp:saveAs(out..name..'.aseprite');sp:close()
end
local function path(im,pts,c,scale,dy)
 local t={};for _,p in ipairs(pts) do t[#t+1]={math.floor(64+(p[1]-64)*scale+.5),math.floor(64+(p[2]-64)*scale+dy+.5)} end;poly(im,t,c)
end
for _,grade in ipairs({'normal','excellent','master'}) do
 local total=grade=='normal' and 6 or 8
 local sp=newClip('quality_'..grade,128,128,{'central spark','satellite stars','arc or rising steam'})
 for i=1,total do
  local a,b,c=blank(128,128),blank(128,128),blank(128,128)
  if i<total then
   if grade=='normal' then local sizes={3,7,10,7,3};star(a,64,65-i,sizes[i])
   else
    local peak=({.28,.52,.85,1,.85,.52,.25})[i];local cy=grade=='master' and 50 or 35
    star(a,64,cy+math.max(0,3-i)*3-math.max(0,i-4)*2,math.max(4,math.floor((grade=='master' and 22 or 13)*peak)))
    if i>=2 and i<=6 then local k=({0,.35,.7,1,.85,.5,0})[i];star(b,38-math.max(0,i-4)*2,68-math.max(0,i-4)*3,math.max(4,math.floor(10*k)));star(b,90+math.max(0,i-4)*2,68-math.max(0,i-4)*3,math.max(4,math.floor(10*k))) end
    if grade=='excellent' and i>=3 then
      local sc=.65+(i-3)*.1;path(c,{{33,78},{38,84},{48,88},{64,91},{80,88},{90,84},{95,78},{94,83},{85,90},{66,95},{48,92},{37,87}},M,sc,-math.max(0,i-4)*2)
      if i<=5 then path(c,{{37,80},{43,85},{57,89},{73,89},{87,84},{92,80},{87,87},{74,92},{55,92},{42,87}},L,sc,0) end
    elseif grade=='master' and i>=2 then
     local sc=.65+(i-2)*.07;local dy=8-(i-2)*4
     path(c,{{42,103},{32,96},{26,85},{24,73},{27,60},{32,48},{33,40},{32,33},{37,39},{37,49},{32,64},{30,76},{33,87},{39,94},{48,99}},col('a2afb0'),sc,dy)
     path(c,{{42,101},{34,95},{28,84},{27,73},{30,59},{34,47},{34,40},{35,48},{31,63},{30,75},{33,86},{39,93},{48,98}},col('e8ebd8'),sc,dy)
     if i>=3 then path(c,{{82,105},{94,99},{102,90},{106,77},{104,66},{99,55},{99,45},{102,40},{103,48},{104,55},{109,67},{111,78},{107,92},{97,103},{87,109}},col('a2afb0'),sc,dy+3);path(c,{{84,103},{95,97},{103,87},{107,76},{104,64},{101,54},{101,47},{102,54},{107,66},{109,78},{105,91},{96,101},{87,107}},col('e8ebd8'),sc,dy+3) end
    end
   end
  end
  setFrame(sp,i,{a,b,c},1/12)
 end
 exportClip(sp,'quality_'..grade)
 -- Reduced effect: same grade silhouette, no arc/steam, 3 exposures, 0.25sec.
 local low=newClip('quality_'..grade..'_low',128,128,{'reduced grade signal'})
 for i=1,3 do local a=blank(128,128);if i<3 then star(a,64,60-i*2,grade=='master' and (i==1 and 13 or 9) or (i==1 and 8 or 5));if grade~='normal' then star(a,49,71-i*2,5);star(a,79,71-i*2,5) end end;setFrame(low,i,{a},1/12) end
 exportClip(low,'quality_'..grade..'_low')
end
-- Banner cloth unrolls laterally from carried roll, then drops and settles.
local rolled=app.open(out..'banner_rolled_representative.aseprite');local roll=Image(rolled.spec);roll:drawSprite(rolled,1);rolled:close()
local settled=app.open(out..'banner_settled_representative.aseprite');local cloth=Image(settled.spec);cloth:drawSprite(settled,1);settled:close()
local sp=newClip('banner_install',1280,192,{'cloth unroll, no letters'})
for i=1,8 do local im=blank(1280,192)
 if i==1 then im:drawImage(roll,Point(0,-55))
 else
  local sx=({0,.46,.64,.82,.96,1,1,1})[i];local sy=({0,.3,.45,.65,.86,1.03,.98,1})[i]
  local w=math.floor(1280*sx);local h=math.min(185,math.floor(174*sy));local scaled=Image(cloth);scaled:resize(w,math.floor(192*sy));im:drawImage(scaled,Point(math.floor((1280-w)/2),0))
 end
 setFrame(sp,i,{im},i==1 and .125 or .125)
end
exportClip(sp,'banner_install')
