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


local function inside(x,y,poly)
 local c=false local j=#poly
 for i=1,#poly do local a,b=poly[i],poly[j] if ((a[2]>y)~=(b[2]>y)) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then c=not c end j=i end return c
end
local polys={
fire={{{30,8},{21,6},{15,5},{3,2},{8,6},{2,8},{9,10},{4,13},{14,12},{21,10},{30,9}}},
bitter={{{30,8},{22,6},{15,3},{8,2},{3,4},{5,7},{10,8},{18,8},{24,9},{30,9}},{{30,8},{22,8},{15,8},{9,9},{4,12},{5,14},{11,13},{17,11},{24,10},{30,9}}},
dual={{{30,8},{22,6},{18,4},{14,2},{12,4},{7,2},{8,5},{3,4},{8,7},{19,8},{30,9}},{{30,8},{22,9},{15,10},{9,10},{4,12},{5,14},{12,13},{19,11},{30,9}}}}
local dark=rgba(32,29,41) local ivory=rgba(244,241,231)
local all={}
for _,state in ipairs({"fire","bitter","dual"}) do
 local id="vfx_arrow_"..state.."_tail" local raw=assert(load(id))
 local source=app.open(root.."raw/"..id..".png") source.layers[1].name="Unmodified actual official PixelLab output" source:saveAs(root.."raw/"..id..".aseprite") source:close()
 local sp=Sprite(32,16,ColorMode.RGB) sp.layers[1].name="Native 3-frame contour and generated material finish"
 for frame=1,3 do
  if frame>1 then sp:newEmptyFrame() end
  local mask={} local shift=frame==2 and -1 or frame==3 and 1 or 0
  for y=0,15 do for x=0,31 do
   local sy=y-(x<15 and shift or 0) local active=false
   for _,poly in ipairs(polys[state]) do if inside(x+.5,sy+.5,poly) then active=true end end
   mask[y*32+x]=active
  end end
  -- Constant socket region remains identical across every frame.
  for x=25,30 do mask[8*32+x]=true end
  local im=Image(32,16,ColorMode.RGB)
  for y=1,14 do for x=1,30 do if mask[y*32+x] then
   local boundary=not mask[y*32+x-1] or not mask[y*32+x+1] or not mask[(y-1)*32+x] or not mask[(y+1)*32+x]
   local nextEdge=not mask[y*32+x-2] or not mask[y*32+x+2] or not mask[(y-2)*32+x] or not mask[(y+2)*32+x]
   local c
   if boundary then c=dark else
    local sx=math.min(raw.width-1,math.floor(x*raw.width/32)) local sy=math.min(raw.height-1,math.floor(y*raw.height/16))
    local rc=raw:getPixel(sx,sy) local lum=(pc.rgbaR(rc)+pc.rgbaG(rc)+pc.rgbaB(rc))/3
    local leaf=state=="bitter" or (state=="dual" and y>=9)
    if leaf then c=lum>160 and rgba(184,203,151) or rgba(95,131,78) else c=lum>160 and rgba(242,192,105) or rgba(200,102,54) end
   end
   im:drawPixel(x,y,c)
  end end end
  for x=22,29 do im:drawPixel(x,8,ivory) end
  if state=="fire" then for x=9,21 do im:drawPixel(x,8,ivory) end elseif state=="dual" then for x=9,18 do im:drawPixel(x,11,ivory) end for x=12,20 do im:drawPixel(x,6,ivory) end end
  if state=="bitter" then for x=8,18 do local yy=x<13 and 5 or 6 if frame==2 then yy=yy-1 elseif frame==3 then yy=yy+1 end im:drawPixel(x,yy,ivory) end end
  sp:newCel(sp.layers[1],frame,im,Point(0,0)) sp.frames[frame].duration=.125
  local export=Sprite(32,16,ColorMode.RGB) export.layers[1].name="Delivery frame" export:newCel(export.layers[1],1,im,Point(0,0)) local sl=export:newSlice(Rectangle(0,0,32,16)) sl.name="delivery_pivot" sl.pivot=Point(30,8)
  local fid=id..string.format("_%02d",frame-1)
  export:saveAs(root.."ready/"..fid..".aseprite") export:saveCopyAs(root.."ready/"..fid..".png") export:close()
  all[#all+1]={assetId=fid,state=state,frame=frame-1,sourceAssetId=id,width=32,height=16,finish="Official generated flame/leaf material retained as tone sampling; native contour correction defines three flame lobes / two leaf wisps / separated hybrid fork. Three actual native frames with terminal flicker and fixed socket; dark outline plus ivory contrast edge."}
 end
 local ref=sp:newLayer() ref.name="Actual generated source (hidden)" ref.isVisible=false sp:newCel(ref,1,raw,Point(0,0))
 local sl=sp:newSlice(Rectangle(0,0,32,16)) sl.name="delivery_pivot" sl.pivot=Point(30,8)
 sp:saveAs(root.."ready/"..id..".aseprite") sp:close()
end
local f=assert(io.open(root.."provenance/native-finish-tails.json","w")) f:write(json.encode(all)) f:close()
