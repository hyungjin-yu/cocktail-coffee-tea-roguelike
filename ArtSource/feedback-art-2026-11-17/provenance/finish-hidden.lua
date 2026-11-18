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


-- All edits below execute in Aseprite's native Image API on actual generated imagery.
local C={d=rgba(32,29,41),s=rgba(83,84,111),g=rgba(167,177,194),w=rgba(244,241,231),p=rgba(234,212,165),b=rgba(113,71,50),t=rgba(184,142,101),l=rgba(220,174,114),v=rgba(70,107,69),h=rgba(142,173,104),i=rgba(151,200,215),j=rgba(208,237,239),["."]=clear}
local function stamp(im,x,y,rows) for yy,row in ipairs(rows) do for xx=1,#row do local k=row:sub(xx,xx) if k~=" " then im:drawPixel(x+xx-1,y+yy-1,assert(C[k],k)) end end end end
local function rect(im,x,y,w,h,color) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and xx<im.width and yy>=0 and yy<im.height then im:drawPixel(xx,yy,color) end end end end
local function line(im,x0,y0,x1,y1,c)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0) local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1 local e=dx+dy
 while true do if x0>=0 and y0>=0 and x0<im.width and y0<im.height then im:drawPixel(x0,y0,c) end if x0==x1 and y0==y1 then break end local e2=2*e if e2>=dy then e=e+dy x0=x0+sx end if e2<=dx then e=e+dx y0=y0+sy end end
end
local ids={"hidden_iced_cup","hidden_soda_pump","hidden_rhythm_shaker","hidden_bitter_wake","hidden_herb_garden","hidden_thick_mug","hidden_bar_tempo","hidden_bar_tempo_move"}
for _,id in ipairs(ids) do
 local raw=assert(load(id)) local im=fit(raw,bbox(raw),24,24,22,22,false)
 local note="Generated object silhouette/material retained; native palette, alpha cleanup and nearest-neighbour 22px fit. "
 if id=="hidden_iced_cup" then
  -- Remove generated straw/cream and replace with angular ice; frame is an actual protection shield.
  rect(im,0,0,24,8,clear) rect(im,15,0,9,11,clear)
  local shield=Image(24,24,ColorMode.RGB)
  stamp(shield,5,7,{" dwwwwwwwwwwd ","dwjjjjjjjjjjwd","dwj        jwd","dwj        jwd","dwj        jwd","dwj        jwd","dwj        jwd"," dwj      jwd "," dwj      jwd ","  dwj    jwd  ","   dwj  jwd   ","    dwjjwd    ","     dwwd     ","      dd      "})
  shield:drawImage(im) im=shield
  stamp(im,8,3,{" dddd ","dwjjid","dwjiid","diiisd"," dddd "})
  stamp(im,12,6,{" dddd ","dwjjid","dwiisd"," dissd"," dddd "})
  note=note.."Straw/cream removed, angular ice and broad ivory shield edge added so it reads protection rather than a dessert drink."
 elseif id=="hidden_soda_pump" then
  rect(im,8,0,8,7,clear)
  stamp(im,8,2,{" dddddddd ","dwgggggggd"," dddsdd gd","    sd  gd","   dggd dd","   dwwd   "})
  stamp(im,1,11,{"   d  ","dddwdd","dwggwd","dddwd ","   d  "})
  stamp(im,17,14,{"  d   "," ddwdd","dwggwd"," ddd d","  d   "})
  note=note.."Bottle retained; pump plunger, curved outlet and two inward attraction arrows natively added."
 elseif id=="hidden_rhythm_shaker" then
  -- A metal cap/shoulder replaces the generated tiny bottle stopper.
  rect(im,5,0,15,7,clear)
  stamp(im,9,2,{" dddd ","dwggsd","dwwgsd"," dddd ","ddggsdd"})
  stamp(im,2,6,{" dd "," dw "," dd ","    ","dd  ","wwd ","dd  "})
  stamp(im,18,8,{" dd "," ww "," dd ","    ","dd  ","wwd ","dd  "})
  note=note.."Metal cap/shoulders and separated beat ticks replace bottle-like neck; shaker body retained."
 elseif id=="hidden_bitter_wake" then
  -- Close the accidental crescent notch into one roasted bean and engrave an alert eye.
  rect(im,12,9,9,10,C.b) line(im,20,9,20,18,C.d) line(im,19,18,17,20,C.d)
  stamp(im,7,9,{"   dddd   "," ddwwwwdd ","dwwdwddwwd"," ddwwwwdd ","   dddd   "})
  line(im,12,14,10,19,C.d) line(im,11,15,9,19,C.t)
  stamp(im,8,1,{"  dd    dd "," dw wd dw wd","dd  dddd  dd"})
  note=note.."Crescent notch repaired into bean, strong central eye seam and upward alert chevrons added."
 elseif id=="hidden_herb_garden" then
  stamp(im,9,16,{"dd dd","wwdww","wwwww"," dwd ","  d  "})
  note=note.."Retained leaf planter; compact ivory heart on pot distinguishes healing from a generic herb ingredient."
 elseif id=="hidden_thick_mug" then
  stamp(im,9,13,{" dd dd ","dwwdwwd","dwwwwwd"," dwwwd ","  dwd  ","   d   "})
  note=note.."Broad generated ceramic rim/handle retained, heart emblem reinforced for vitality at24px."
 elseif id=="hidden_bar_tempo" or id=="hidden_bar_tempo_move" then
  -- Preserve wooden triangular body; draw an unmistakable metronome dial/pendulum over it.
  rect(im,10,7,4,10,C.d) rect(im,11,8,2,8,C.s)
  line(im,12,4,12,16,C.w)
  if id=="hidden_bar_tempo" then
   stamp(im,10,11,{"ddd","dwd","ddd"})
   stamp(im,8,17,{"dd dd","ww ww","ww ww","dd dd"})
   note=note.."Wooden body retained; vertical ivory pendulum, weight and paired pause marks define stationary defensive tempo."
  else
   line(im,12,5,17,14,C.d) line(im,11,5,16,14,C.w)
   stamp(im,14,11,{" ddd ","dwwgd"," ddd "})
   stamp(im,18,5,{"dd  "," ww ","  dd","  ww"," dd "})
   note=note.."Wooden body retained; diagonal ivory pendulum/weight and outward ticks define moving tempo, not just a recolor."
  end
 end
 if id=="hidden_iced_cup" then
  rect(im,8,20,10,3,clear)
  line(im,5,8,5,14,C.d) line(im,6,8,6,15,C.w)
  line(im,6,16,11,21,C.d) line(im,7,16,12,21,C.w)
  line(im,18,9,18,14,C.d) line(im,17,10,17,15,C.w)
  line(im,17,16,12,21,C.d) line(im,16,16,12,20,C.w)
 elseif id=="hidden_rhythm_shaker" then
  for y=0,23 do for x=0,23 do local c=im:getPixel(x,y) if c==C.v then im:drawPixel(x,y,C.s) elseif c==C.h then im:drawPixel(x,y,C.g) end end end
 end
 -- Delivery margins are always clear, and edits do not introduce antialiasing.
 rect(im,0,0,24,1,clear) rect(im,0,23,24,1,clear) rect(im,0,0,1,24,clear) rect(im,23,0,1,24,clear)
 save(id,im,raw,note)
 local rs=app.open(root.."raw/"..id..".png") rs.layers[1].name="Unmodified actual official PixelLab output" rs:saveAs(root.."raw/"..id..".aseprite") rs:close()
end
local f=assert(io.open(root.."provenance/native-finish-hidden.json","w")) f:write(json.encode(records)) f:close()
