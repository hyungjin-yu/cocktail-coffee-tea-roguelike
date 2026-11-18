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
 if id:find("^dl_coffee_") then slice.pivot=Point(im.width/2,im.height)
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

-- Native preview fitting only. These draft files are not published ready.
local ids={"hidden_iced_cup","hidden_soda_pump","hidden_rhythm_shaker","hidden_bitter_wake","hidden_weekend_rush_steps","hidden_herb_garden","hidden_thick_mug","hidden_bar_tempo","hidden_bar_tempo_move"}
for _,id in ipairs(ids) do local raw=load(id) if raw then local im=fit(raw,bbox(raw),24,24,22,22,false) local sp=Sprite(24,24,ColorMode.RGB) sp:newCel(sp.layers[1],1,im) sp:saveAs(root.."previews/"..id.."-draft.png") sp:close() end end
