-- Native Aseprite finishing of actual PixelLab outputs; no replacement generator.
local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-17/"
local pc=app.pixelColor
local function rgba(r,g,b,a) return pc.rgba(r,g,b,a or 255) end
local clear=rgba(0,0,0,0)
local pal={{32,29,41},{74,64,80},{83,84,111},{167,177,194},{244,241,231},{234,212,165},{184,142,101},{48,32,40},{113,71,50},{220,174,114},{70,107,69},{142,173,104}}
local function remap(c)
 if pc.rgbaA(c)<128 then return clear end
 local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
 local best,dist=pal[1],math.huge
 for _,p in ipairs(pal) do local d=(r-p[1])^2+(g-p[2])^2+(b-p[3])^2 if d<dist then best=p dist=d end end
 return rgba(best[1],best[2],best[3])
end
local matteRemoved={}
local function load(id)
 local f=root.."raw/"..id..".png" if not app.fs.isFile(f) then return nil end
 local im=Image{fromFile=f}
 local corners={im:getPixel(0,0),im:getPixel(im.width-1,0),im:getPixel(0,im.height-1),im:getPixel(im.width-1,im.height-1)}
 local bg=nil
 for _,c in ipairs(corners) do
  local count=0 for _,cc in ipairs(corners) do if pc.rgbaA(cc)>127 and math.abs(pc.rgbaR(cc)-pc.rgbaR(c))<=16 and math.abs(pc.rgbaG(cc)-pc.rgbaG(c))<=16 and math.abs(pc.rgbaB(cc)-pc.rgbaB(c))<=16 then count=count+1 end end
  if count>=3 and pc.rgbaA(c)>127 then bg=c break end
 end
 if bg then
  local q={} local visited={}
  local function add(x,y)
   if x<0 or y<0 or x>=im.width or y>=im.height then return end
   local key=y*im.width+x
   local c=im:getPixel(x,y)
   if visited[key] or pc.rgbaA(c)<128 or math.abs(pc.rgbaR(c)-pc.rgbaR(bg))>18 or math.abs(pc.rgbaG(c)-pc.rgbaG(bg))>18 or math.abs(pc.rgbaB(c)-pc.rgbaB(bg))>18 then return end
   visited[key]=true q[#q+1]={x,y}
  end
  for x=0,im.width-1 do add(x,0) add(x,im.height-1) end
  for y=0,im.height-1 do add(0,y) add(im.width-1,y) end
  local k=1 while k<=#q do local point=q[k] local x,y=point[1],point[2] im:drawPixel(x,y,clear) add(x-1,y) add(x+1,y) add(x,y-1) add(x,y+1) k=k+1 end
  matteRemoved[id]=#q
 end
 return im
end
local function bbox(im)
 local a,b,c,d=im.width,im.height,-1,-1
 for y=0,im.height-1 do for x=0,im.width-1 do if pc.rgbaA(im:getPixel(x,y))>127 then a=math.min(a,x) b=math.min(b,y) c=math.max(c,x) d=math.max(d,y) end end end
 assert(c>=a,"Empty raw image") return {a,b,c+1,d+1}
end
local function fit(im,b,w,h,tw,th,bottom,mapper)
 local bw,bh=b[3]-b[1],b[4]-b[2]
 local k=math.min(tw/bw,th/bh)
 local nw,nh=math.max(1,math.floor(bw*k+.5)),math.max(1,math.floor(bh*k+.5))
 local dx,dy=math.floor((w-nw)/2),bottom and h-nh or math.floor((h-nh)/2)
 local dest=Image(w,h,ColorMode.RGB)
 for y=0,nh-1 do for x=0,nw-1 do
 local sx=b[1]+math.min(bw-1,math.floor(x*bw/nw)) local sy=b[2]+math.min(bh-1,math.floor(y*bh/nh))
 dest:drawPixel(dx+x,dy+y,(mapper or remap)(im:getPixel(sx,sy),sx,sy))
 end end
 return dest,{crop=b,dest={dx,dy,nw,nh}}
end
local records={}
local anchors={entries={}}
local function save(id,im,raw,extra)
 local spr=Sprite(im.width,im.height,ColorMode.RGB)
 spr.layers[1].name="Native finish from PixelLab"
 spr:newCel(spr.layers[1],1,im,Point(0,0))
 if raw then local layer=spr:newLayer() layer.name="Matte-cleaned PixelLab source (hidden)" layer.isVisible=false spr:newCel(layer,1,raw,Point(0,0)) end
 local slice=spr:newSlice(Rectangle(0,0,im.width,im.height))
 slice.name="delivery_pivot"
 if id:find("^dl_coffee_") or id:find("^dl_tin_") then slice.pivot=Point(im.width/2,im.height)
 elseif id=="dl_foam_layer" then slice.pivot=Point(0,im.height)
 else slice.pivot=Point(im.width/2,im.height/2) end
 spr:saveAs(root.."ready/"..id..".aseprite")
 spr:saveCopyAs(root.."ready/"..id..".png")
 spr:close()
 records[#records+1]={assetId=id,width=im.width,height=im.height,removedMattePixels=matteRemoved[id] or 0,finish=extra or "native edge-connected matte removal when needed, palette and alpha cleanup; uniform nearest-neighbour crop fit"}
end
local function whiteMask(raw,predicate)
 local m=Image(raw.width,raw.height,ColorMode.RGB)
 for y=0,m.height-1 do for x=0,m.width-1 do if predicate(x,y) then m:drawPixel(x,y,rgba(255,255,255)) end end end return m
end
local function transformMask(mask,transform,w,h)
 local dest=Image(w,h,ColorMode.RGB) local b=transform.crop local d=transform.dest
 for y=0,d[4]-1 do for x=0,d[3]-1 do
 local sx=b[1]+math.min(b[3]-b[1]-1,math.floor(x*(b[3]-b[1])/d[3])) local sy=b[2]+math.min(b[4]-b[2]-1,math.floor(y*(b[4]-b[2])/d[4]))
 dest:drawPixel(d[1]+x,d[2]+y,mask:getPixel(sx,sy)) end end return dest
end
local function anchor(id,mask,mouth,grip,w,h)
 local b=bbox(mask)
 anchors.entries[#anchors.entries+1]={assetId=id,mouth={mouth[1]/w,1-mouth[2]/h},grip={grip[1]/w,1-grip[2]/h},liquid={b[1]/w,1-b[4]/h,b[3]/w,1-b[2]/h},pivot={.5,0},pixelOrigin="bottom-left normalized; mask PNG uses top-left pixels"}
end

local specs={{id="dl_tin_ethiopia_yirgacheffe_kochere",index=0,crop=0.1,pattern={"#.....#",".#...#.","..#.#..","...#...","...#...","...#...","...#..."}},{id="dl_tin_colombia_huila_vergel",index=1,crop=0.12,pattern={"...#...","..#.#..",".#...#.","#..#..#","..#.#..",".#...#.","#.....#"}},{id="dl_tin_brazil_mantiqueira_alves_lima",index=2,crop=0,pattern={"#....#.",".#..#..","..##...","..##...",".#..#..","#....#.","......."}},{id="dl_tin_panama_esmeralda_geisha",index=3,crop=0,pattern={".....##","...###.","..###..",".###...",".##....","..#....",".#....."}},{id="dl_tin_colombia_huila_san_pedro_geisha",index=4,crop=0.23,pattern={"..##...","..##...","##..##.","##..##.","..##...","..##...","......."}},{id="dl_tin_colombia_la_esperanza_bourbon",index=5,crop=0.2,pattern={"#..#..#","##.#.##","#######",".#####.","..###..","..###..","......."}},{id="dl_tin_coffee_ethiopia_guji",index=6,crop=0.2,pattern={"##...##","###.###",".#####.","..###..","...#...","...#...","...#..."}},{id="dl_tin_coffee_ethiopia_sidama",index=7,crop=0,pattern={"#.#.#.#",".#####.","#######",".#####.","#######",".#####.",".#.#.#."}},{id="dl_tin_coffee_kenya_nyeri",index=8,crop=0.12,pattern={"...#...","..#.#..",".#...#.","#.....#",".#...#.","..#.#..","...#..."}},{id="dl_tin_coffee_rwanda",index=9,crop=0,pattern={"..####.",".##....","##.....","##.....","##.....",".##....","..####."}},{id="dl_tin_coffee_burundi",index=10,crop=0.2,pattern={"#######","#.....#","#.###.#","#.#.#.#","#.###.#","#.....#","#######"}},{id="dl_tin_coffee_uganda",index=11,crop=0.12,pattern={"#.....#","#.....#",".#...#.",".#...#.","..#.#..","..#.#..","...#..."}},{id="dl_tin_coffee_indonesia_gayo",index=12,crop=0.2,pattern={".##..##","#..##..",".......",".##..##","#..##..",".......","......."}},{id="dl_tin_coffee_indonesia_java",index=13,crop=0.2,pattern={"...#...","..#.#..","..#.#..",".#...#.",".#...#.","#.....#","#######"}},{id="dl_tin_coffee_vietnam_dalat",index=14,crop=0,pattern={".#...#.","###.###",".#...#.","###.###",".#...#.",".#...#.","...#..."}},{id="dl_tin_coffee_india_malabar",index=15,crop=0,pattern={".##.##.","#.##.##","#.##.##",".##.##.",".......","..###..","..#.#.."}},{id="dl_tin_coffee_costa_rica_tarrazu",index=16,crop=0,pattern={"..##...","..##...",".####..",".####..","######.","######.","......."}},{id="dl_tin_coffee_guatemala_antigua",index=17,crop=0.12,pattern={"#..#..#",".#####.",".#####.","#######",".#####.",".#####.","#..#..#"}},{id="dl_tin_coffee_honduras_marcala",index=18,crop=0,pattern={"..###..","..###..","###.###","###.###","..###..","...#...","...#..."}},{id="dl_tin_coffee_peru_chanchamayo",index=19,crop=0,pattern={"#######","##...##","#.#.#.#","#..#..#","#.#.#.#","##...##","#######"}},{id="dl_tin_coffee_mexico_chiapas",index=20,crop=0,pattern={"#..#..#",".#.#.#.","..###..","...#...","...#...","...#...","...#..."}},{id="dl_tin_coffee_jamaica_blue_mountain",index=21,crop=0,pattern={".....##","....##.","...##..","..##...",".##....","##.....","......."}},{id="dl_tin_coffee_brazil_cerrado",index=22,crop=0,pattern={".#####.","#.....#","#.....#","#.....#","##...##",".#####.","......."}},{id="dl_tin_coffee_colombia_narino",index=23,crop=0,pattern={".#####.","#.....#","#.###.#","#.#...#","#.#####","#......",".######"}},{id="dl_tin_coffee_el_salvador",index=24,crop=0.17,pattern={"#.....#",".#...#.","..#.#..","...#...","..#.#..",".#...#.","#.....#"}},{id="dl_tin_coffee_nicaragua",index=25,crop=0,pattern={"..###..",".#...#.","#.....#","#.....#","#.....#","#.....#","#.....#"}},{id="dl_tin_coffee_tanzania",index=26,crop=0.12,pattern={"..#.#..",".#...#.","#.....#","..#.#..",".#...#.","#.....#","......."}},{id="dl_tin_coffee_bolivia",index=27,crop=0,pattern={"#######",".......",".#...#.",".#...#.",".#...#.",".......","#######"}}}

local dark=rgba(32,29,41) local ivory=rgba(244,241,231) local silver=rgba(167,177,194) local bean=rgba(113,71,50) local warm=rgba(220,174,114)
local function dot(im,x,y,c) if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end end
for _,spec in ipairs(specs) do
 local id=spec.id local raw=load(id)
 if raw then
  local b=bbox(raw) b[2]=b[2]+math.floor((b[4]-b[2])*spec.crop)
  local bw,bh=b[3]-b[1],b[4]-b[2]
  local scale=math.min(26/bw,28/bh)
  local nw,nh=math.max(18,math.floor(bw*scale+.5)),math.max(18,math.floor(bh*scale+.5))
  local im=Image(32,32,ColorMode.RGB) local dx,dy=math.floor((32-nw)/2),32-nh
  for y=0,nh-1 do for x=0,nw-1 do
   local sx=b[1]+math.min(bw-1,math.floor(x*bw/nw)) local sy=b[2]+math.min(bh-1,math.floor(y*bh/nh))
   dot(im,dx+x,dy+y,remap(raw:getPixel(sx,sy)))
  end end
  -- Open rim at actual body top. Closed/generated caps were explicitly cropped above.
  local centerY=dy+3 local half=math.floor((nw-2)/2) local cx=15.5
  for y=0,centerY-2 do for x=0,31 do dot(im,x,y,clear) end end
  for oy=-2,2 do
   local width=half
   if oy==-2 or oy==2 then width=half-2 else width=half end
   for x=math.ceil(cx-width),math.floor(cx+width) do
    local edge=x==math.ceil(cx-width) or x==math.floor(cx+width) or math.abs(oy)==2
    dot(im,x,centerY+oy,edge and dark or (oy==-1 and ivory or oy==1 and silver or dark))
   end
  end
  -- Visible three-bean dark opening, kept above label, with a stable top-right pour lip.
  for _,ox in ipairs({-4,0,4}) do dot(im,math.floor(cx+ox),centerY,warm) dot(im,math.floor(cx+ox)+1,centerY,bean) end
  -- A small paper/metal label carries one unique high-contrast structural motif.
  local labelY=math.min(21,centerY+math.max(5,math.floor((31-centerY)*.42)))
  local labelX=12
  for yy=-1,7 do for xx=-1,7 do dot(im,labelX+xx,labelY+yy,(xx==-1 or xx==7 or yy==-1 or yy==7) and dark or ivory) end end
  for yy,row in ipairs(spec.pattern) do for xx=1,#row do if row:sub(xx,xx)=="#" then dot(im,labelX+xx-1,labelY+yy-1,dark) end end end
  -- Ensure opening is supported by material walls and not a detached overlay.
  for x=math.floor(cx-half)+1,math.ceil(cx+half)-1 do if pc.rgbaA(im:getPixel(x,centerY+3))==0 then dot(im,x,centerY+3,rgba(184,142,101)) end end
  save(id,im,raw,"Actual official PixelLab container body/material retained; native closed-cap crop where needed, minimum readable body-width correction, open dark ellipse with bean pixels and ivory/silver rim. Unique structural origin motif follows approved inventory concept; no color-only distinction. Opening determines per-container mouth/pourLip and body grip.")
  anchors.entries[#anchors.entries+1]={assetId=id,width=32,height=32,mouth={(cx+.5)/32,1-(centerY+.5)/32},pourLip={(math.floor(cx+half)+.5)/32,1-(centerY+.5)/32},grip={.5,(32-(centerY+3+(31-centerY-3)*.60))/32},pivot={.5,0},mouthPixelCenterTopLeft={cx+.5,centerY+.5},pourLipPixelCenterTopLeft={math.floor(cx+half)+.5,centerY+.5},openingBoundsTopLeft={math.ceil(cx-half),centerY-2,math.floor(cx+half)+1,centerY+3},pixelOrigin="normalized bottom-left; opening dimensions use top-left PNG pixels"}
  local rs=app.open(root.."raw/"..id..".png") rs.layers[1].name="Unmodified actual official PixelLab output" rs:saveAs(root.."raw/"..id..".aseprite") rs:close()
 end
end
local f=assert(io.open(root.."provenance/native-finish-containers.json","w")) f:write(json.encode(records)) f:close()
f=assert(io.open(root.."ready/anchors-containers.json","w")) f:write(json.encode(anchors)) f:close()
