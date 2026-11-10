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

local function loadFile(path)
 local s=app.open(path);local im=Image(s.spec);im:drawSprite(s,1);s:close();return im
end
local function nativeLayer(path,index)
 local s=app.open(path);local im=Image(s.spec);for _,cel in ipairs(s.cels) do if cel.layer==s.layers[index] and cel.frameNumber==1 then im:drawImage(cel.image,cel.position) end end;s:close();return im
end
local function cropRegion(im,x0,y0,x1,y1)
 local o=blank(256,256);for y=y0,y1 do for x=x0,x1 do o:drawPixel(x,y,im:getPixel(x,y)) end end;return o
end
local function mirrored(im)
 local o=blank(256,256);for y=0,255 do for x=0,255 do o:drawPixel(255-x,y,im:getPixel(x,y)) end end;return o
end
local function rotateArm(im,deg)
 local o=blank(256,256);local a=deg*math.pi/180;local c,ss=math.cos(a),math.sin(a)
 -- inverse mapped nearest sampling avoids forward-map pinholes
 for y=35,182 do for x=130,225 do local ox=150+(x-150)*c+(y-117)*ss;local oy=117-(x-150)*ss+(y-117)*c;local xx,yy=math.floor(ox+.5),math.floor(oy+.5);if xx>=0 and yy>=0 and xx<256 and yy<256 then o:drawPixel(x,y,im:getPixel(xx,yy)) end end end
 return o
end
local fixprop=nativeLayer(out..'npc_rigger_revision02.aseprite',2)
local fixarm=cropRegion(fixprop,146,65,204,145)
for _,role in ipairs({'porter','rigger'}) do
 local source=role=='porter' and 'tea_sommelier' or 'bartender'
 local approved=loadFile(out..'npc_'..role..'_revision02.aseprite')
 local rest=loadFile(root..'Assets/_Project/Art/v1/'..source..'/frames/'..source..'_idle_right_00.png')
 if role=='porter' then rest=nativeLayer(out..'npc_porter_revision02.aseprite',1)
 else
  for it in rest:pixels() do local v=it();local x,y=it.x,it.y;local r,g,b,a=pc.rgbaR(v),pc.rgbaG(v),pc.rgbaB(v),pc.rgbaA(v);if a>0 and y>=109 and y<163 and x>=106 and x<155 and b>r*1.15 and b<130 and r>20 then local t=(r+g+b)/3;it(col(t<45 and '293d35' or t<61 and '3d5949' or t<80 and '58785d' or '83a17c')) end end
  rest:drawImage(cropRegion(fixprop,98,148,152,194),Point(0,0))
 end
 -- All six gait poses come from runtime lower-body source, no invented walk frames.
 local gait={};for i=0,5 do gait[#gait+1]=loadFile(root..'Assets/_Project/Art/v1/'..source..'/frames/'..source..'_move_right_'..string.format('%02d',i)..'.png') end
 for _,clip in ipairs({'walk_in','walk_out'}) do
  local sp=newClip('npc_'..role..'_'..clip,256,256,{'runtime lower-body gait reused','approved upper body and carried prop'})
  for i=1,6 do
   local bob=({0,2,-2,0,2,-2})[i];local lower=cropRegion(gait[i],0,168,255,255)
   local top=role=='porter' and clip=='walk_in' and approved or rest
   local upper=cropRegion(top,0,0,255,role=='rigger' and 194 or clip=='walk_in' and 178 or 180)
   local translated=blank(256,256);translated:drawImage(upper,Point(0,bob))
   setFrame(sp,i,{lower,translated},.1)
  end
  exportClip(sp,'npc_'..role..'_'..clip)
 end
 local idle=newClip('npc_'..role..'_idle',256,256,{'static rest hold - 1 unique drawing 4 exposures'})
 for i=1,4 do setFrame(idle,i,{Image(rest)},1/3) end;exportClip(idle,'npc_'..role..'_idle')
 -- Installation: three articulated reach angles, each exposed twice; hold at grip.
 local base=Image(rest)
 if role=='rigger' then for y=111,181 do for x=154,183 do base:drawPixel(x,y,0) end end
 else for y=116,181 do for x=79,112 do base:drawPixel(x,y,0) end end end
 local install=newClip('npc_'..role..'_install',256,256,{'approved standing body, task arm removed','three reach angles from approved bent arm'})
 for i=1,6 do
  local angle=({30,15,0,0,15,30})[i];local arm=rotateArm(fixarm,angle);if role=='porter' then arm=mirrored(arm) end
  setFrame(install,i,{Image(base),arm},({.125,.125,.5,.5,.125,.125})[i])
 end
 exportClip(install,'npc_'..role..'_install')
end
