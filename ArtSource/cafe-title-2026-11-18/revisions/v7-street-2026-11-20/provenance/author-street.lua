-- Authored Aseprite scene finishing: one room geometry and shared actual PixelLab/approved textures.
-- Raster pixels, depth tests, source images and native source layers are created by Aseprite Image API.
local root=assert(app.params.root, "Supply package root with --script-param root=...").."/"
local output=assert(app.params.output).."/"
local pc=app.pixelColor
local function rgba(r,g,b,a) return pc.rgba(r,g,b,a or 255) end
local clear=rgba(0,0,0,0)
local textures={}
local function tex(id,path,bounds) textures[id]={image=Image{fromFile=root..path},bounds=bounds} end
tex('wall','reference/cafe_wall_effect_right_00.png',{132,326,456,494})
tex('floor','reference/bg_bar_floor_00.png',{24,36,64,92})
tex('counter','reference/bg_bar_counter_00.png',{3,62,253,110})
tex('woodtop','reference/bg_bar_counter_00.png',{8,51,248,55})
tex('doorwood','raw/ct_door_material_probe.png',{31,86,64,113})
tex('books','raw/ct_bookshelf_source.png',{4,16,124,186})
tex('coat','raw/ct_coat_cabinet_source.png',{16,4,112,192})
tex('radio','raw/ct_radio_source.png',{25,7,105,57})
tex('ledger','raw/ct_ledger_source.png',{12,1,125,62})
tex('machine','raw/ct_espresso_source.png',{15,22,113,80})
tex('facade','raw/ct_facade_material_source.png',{0,4,60,124})
tex('screen','reference/laptop_open.png',{38,18,338,203})
tex('keyboard','reference/laptop_open.png',{29,212,355,242})
tex('barwalltrim','reference/bg_bar_wall_00.png',{37,0,220,18})
tex('barlamp','reference/bg_bar_lamp_00.png',{11,0,53,103})
local mats={
 barwalltrim={texture='barwalltrim'},barlamp={texture='barlamp',masked=true},plaster={rgb={178,145,103},grain=true},ceiling={rgb={118,94,69}},facadewood={texture='facade',shade=1.12},wood={texture='wall',shade=.97},woodtop={texture='woodtop'},floor={texture='floor',shade=1.05},
 counter={texture='counter',shade=1.05},dark={rgb={37,30,34}},trim={rgb={99,68,47}},edge={rgb={165,123,76}},
 brass={rgb={199,159,94}},ivory={rgb={236,224,197}},slate={rgb={70,77,77}},outside={rgb={90,91,86}},
 doorwood={texture='woodtop',shade=.83},books={texture='books',masked=true,shade=1.1},coat={texture='coat',masked=true},
 radio={texture='radio',masked=true},ledger={texture='ledger',masked=true},machine={texture='machine',masked=true},
 screen={texture='screen',shade=1.1},keyboard={texture='keyboard'},glass={rgb={157,192,190},alpha=42},paper={rgb={224,207,172}},
}
mats.street={rgb={58,57,58},grain=true}
mats.curbstone={rgb={112,103,91}}
mats.pavement={rgb={91,89,83},grain=true}
mats.streetBrick={rgb={119,85,67},grain=true}
mats.streetBrickAlt={rgb={127,93,72},grain=true}
mats.streetStone={rgb={91,69,58},grain=true}
mats.streetWindow={rgb={65,68,68}}
mats.streetLit={rgb={141,112,77},grain=true}
mats.streetTrim={rgb={101,79,65}}
mats.streetBase={rgb={85,76,69},grain=true}
local faces={}
local function quad(v,mat,group,category,uv)
 local a={} local coords=uv or {{0,1},{1,1},{1,0},{0,0}}
 for i=1,4 do a[i]={v[i][1],v[i][2],v[i][3],coords[i][1],coords[i][2]} end
 faces[#faces+1]={v=a,mat=mat,group=group or 0,category=category or 0}
end
local function box(x0,y0,z0,x1,y1,z1,mat,group,cat)
 quad({{x0,y0,z0},{x1,y0,z0},{x1,y0,z1},{x0,y0,z1}},mat,group,cat)
 quad({{x1,y1,z0},{x0,y1,z0},{x0,y1,z1},{x1,y1,z1}},mat,group,cat)
 quad({{x0,y1,z0},{x0,y0,z0},{x0,y0,z1},{x0,y1,z1}},mat,group,cat)
 quad({{x1,y0,z0},{x1,y1,z0},{x1,y1,z1},{x1,y0,z1}},mat,group,cat)
 quad({{x0,y0,z1},{x1,y0,z1},{x1,y1,z1},{x0,y1,z1}},'woodtop',group,cat)
end
local function westface(x,y0,y1,z0,z1,mat,group) quad({{x,y1,z0},{x,y0,z0},{x,y0,z1},{x,y1,z1}},mat,group,2) end
local function northface(y,x0,x1,z0,z1,mat,group) quad({{x1,y,z0},{x0,y,z0},{x0,y,z1},{x1,y,z1}},mat,group,2) end
local function furnitureTable(x0,y0,x1,y1,h,group)
 for _,x in ipairs({x0+.035,x1-.095}) do for _,y in ipairs({y0+.035,y1-.095}) do box(x,y,0,x+.06,y+.06,h-.055,'trim',group,2) end end
 box(x0,y0,h-.055,x1,y1,h,'woodtop',group,2)
end
local function scene(angle)
 faces={}
 -- Room envelope, a real south opening and the exterior approach/pavement.
 quad({{-4,0,0},{0,0,0},{0,0,2.9},{-4,0,2.9}},'plaster',0,1)
 quad({{-5,-8,-.01},{12,-8,-.01},{12,8,-.01},{-5,8,-.01}},'outside',0,1)
 quad({{0,0,0},{7.5,0,0},{7.5,6,0},{0,6,0}},'floor',0,2)
 quad({{0,0,2.9},{7.5,0,2.9},{7.5,6,2.9},{0,6,2.9}},'ceiling',0,2)
 quad({{0,6,0},{7.5,6,0},{7.5,6,2.9},{0,6,2.9}},'plaster',0,2)
 quad({{0,0,0},{0,6,0},{0,6,2.9},{0,0,2.9}},'plaster',0,2)
 quad({{7.5,6,0},{7.5,0,0},{7.5,0,2.9},{7.5,6,2.9}},'plaster',0,2)
 -- Wainscoting is sampled only from approved unlit wood, not the old multiple lamps.
 quad({{.008,0,0},{.008,6,0},{.008,6,.92},{.008,0,.92}},'wood',0,2)
 quad({{7.492,6,0},{7.492,0,0},{7.492,0,.92},{7.492,6,.92}},'wood',0,2)
 quad({{0,5.992,0},{7.5,5.992,0},{7.5,5.992,.92},{0,5.992,.92}},'wood',0,2)
 for _,b in ipairs({{0,1.4,0,2.9},{2.45,2.85,0,2.9},{3.95,7.5,0,2.9},{2.85,3.95,0,1.15},{2.85,3.95,2.18,2.9},{1.4,2.45,2.15,2.9}}) do
  quad({{b[1],0,b[3]},{b[2],0,b[3]},{b[2],0,b[4]},{b[1],0,b[4]}},'plaster',0,1)
  if b[3]==0 then quad({{b[1],-.005,0},{b[2],-.005,0},{b[2],-.005,.90},{b[1],-.005,.90}},'wood',0,1) end
  if b[3]==0 then quad({{b[2],.008,0},{b[1],.008,0},{b[1],.008,.92},{b[2],.008,.92}},'wood',0,2) end
 end
 -- Hinged doorway trim and threshold. West hinge is the SAME physical jamb in both views.
 box(1.31,-.07,0,1.4,.10,2.24,'facadewood',8,1) box(2.45,-.07,0,2.54,.10,2.24,'facadewood',8,1)
 box(1.31,-.07,2.15,2.54,.10,2.24,'edge',8,1) box(1.36,-.10,0,2.49,.16,.035,'edge',8,1)
 -- Same actual south window, directly above radio sideboard, not an unrelated painted opening.
 box(2.79,-.06,1.08,2.85,.07,2.24,'facadewood',0,1) box(3.95,-.06,1.08,4.01,.07,2.24,'facadewood',0,1)
 box(2.79,-.06,1.08,4.01,.07,1.15,'edge',0,1) box(2.79,-.06,2.18,4.01,.07,2.24,'trim',0,1)
 quad({{2.85,0,1.15},{3.95,0,1.15},{3.95,0,2.18},{2.85,0,2.18}},'glass',0,4)
 -- A small blank physical sign above the entry; lettering is Unity text.
 box(1.25,-.10,2.32,2.60,-.035,2.62,'trim',0,1)
 -- Small physical entry instruction plate, separate from the game-name sign.
 box(.50,-.08,1.13,1.24,-.03,1.52,'trim',0,1)
 -- East service counter and physically clear1.1m staff aisle behind it.
 box(5.07,1.32,0,5.64,4.78,.12,'dark',1,2)
 box(5,1.3,.10,5.7,4.8,.94,'wood',1,2)
 for _,seg in ipairs({{1.3,1.90,1},{1.90,2.28,5},{2.28,3.25,1},{3.25,4.8,1}}) do
  westface(4.995,seg[1],seg[2],.10,.94,'counter',seg[3])
  quad({{5,seg[1],.995},{5.7,seg[1],.995},{5.7,seg[2],.995},{5,seg[2],.995}},'woodtop',seg[3],2)
 end
 box(4.96,1.26,.94,5.74,4.84,.99,'trim',1,2)
 box(6.8,1.3,0,7.3,4.8,.92,'wood',0,2)
 -- Recipe bookshelf on west wall, front facing EAST, source depicts actual shelf contents.
 box(.14,1.13,0,.56,1.97,.10,'dark',3,2)
 box(.10,1.1,.03,.60,2.0,1.75,'wood',3,2)
 quad({{.606,1.1,0.08},{.606,2.0,.08},{.606,2.0,1.75},{.606,1.1,1.75}},'books',3,2)
 -- Apron cabinet against south wall, all hangers attach to rail/source cabinet.
 box(.19,.14,0,1.06,.51,.08,'dark',4,2)
 box(.15,.10,.02,1.10,.55,1.80,'wood',4,2)
 northface(.556,.15,1.10,.02,1.80,'coat',4)
 -- South end workstation. Turn the keyboard toward the staff aisle.
 local firstLaptopFace=#faces+1
 box(-.20,-.135,.995,.20,.135,1.017,'slate',2,2)
 quad({{-.20,-.135,1.018},{.20,-.135,1.018},{.20,.135,1.018},{-.20,.135,1.018}},'keyboard',2,2)
 box(-.20,-.135,1.017,.20,-.112,1.280,'slate',2,2)
 northface(-.108,-.185,.185,1.035,1.263,'screen',2)
 local c,s=math.cos(math.rad(25)),math.sin(math.rad(25))
 for i=firstLaptopFace,#faces do
  for _,v in ipairs(faces[i].v) do
   local x,y=v[1],v[2]
   v[1],v[2]=5.39+c*x+s*y,1.53-s*x+c*y
  end
 end
 -- Radio sits on the south sideboard. Source device width45cm, height25cm.
 box(2.9,.15,0,3.9,.70,.76,'wood',6,2)
 box(3.10,.36,.76,3.55,.57,1.01,'trim',6,2)
 northface(.575,3.10,3.55,.76,1.01,'radio',6)
 -- Ledger on the actual service countertop, no floating book plane.
 quad({{5.05,1.92,1.003},{5.45,1.92,1.003},{5.45,2.24,1.003},{5.05,2.24,1.003}},'ledger',5,2)
 -- Espresso appliance on staff back counter, source cups are to scale.
 box(6.84,2.05,.92,7.24,2.65,1.45,'slate',0,2)
 westface(6.833,2.05,2.65,.92,1.45,'machine',0)
 -- Two correctly scaled cups stand on a supported prep tray under the one pendant.
 box(5.04,2.42,1.001,5.56,2.82,1.019,'trim',1,2)
 for _,cx in ipairs({5.18,5.40}) do
  local cy=2.61 local rad=.045 local z0,z1=1.020,1.107
  for i=0,11 do local a,b=i*math.pi/6,(i+1)*math.pi/6
   quad({{cx+rad*math.cos(a),cy+rad*math.sin(a),z0},{cx+rad*math.cos(b),cy+rad*math.sin(b),z0},{cx+rad*math.cos(b),cy+rad*math.sin(b),z1},{cx+rad*math.cos(a),cy+rad*math.sin(a),z1}},'ivory',1,2)
  end
  quad({{cx-.032,cy-.032,z1+.001},{cx+.032,cy-.032,z1+.001},{cx+.032,cy+.032,z1+.001},{cx-.032,cy+.032,z1+.001}},'trim',1,2)
  box(cx+.042,cy-.019,z0+.025,cx+.067,cy+.019,z1-.012,'ivory',1,2)
 end
 -- Staff notice board is mounted on south wall; small unlettered papers pinned to it.
 box(4.3,.02,1.5,5.45,.07,2.10,'trim',7,2)
 northface(.077,4.36,5.39,1.56,2.04,'edge',7)
 for _,n in ipairs({{4.43,1.62,4.68,1.94},{4.77,1.72,5.02,1.99},{5.10,1.61,5.31,1.88}}) do
  northface(.083,n[1],n[3],n[2],n[4],'paper',7)
  box(n[1]+.10,.084,n[4]-.025,n[1]+.125,.095,n[4]-.005,'brass',7,2)
 end
 -- Customer table and two actual grounded chairs outside all circulation areas.
 furnitureTable(1.08,4.48,1.72,5.12,.72,0)
 for _,x in ipairs({.51,1.88}) do furnitureTable(x,4.58,x+.42,5.02,.44,0) box(x,4.98,.44,x+.42,5.04,.88,'wood',0,2) end
 -- Shared actual bg_bar pendant design, one fixed world light source.
 northface(3.0,4.96,5.24,2.32,2.9,'barlamp',0)
 quad({{4.96,3.001,2.32},{5.24,3.001,2.32},{5.24,3.001,2.9},{4.96,3.001,2.9}},'barlamp',0,2)
 -- Existing wall cornice uses the actual bg_bar carved molding strip.
 quad({{0,5.99,2.72},{7.5,5.99,2.72},{7.5,5.99,2.9},{0,5.99,2.9}},'barwalltrim',0,2)
 quad({{.01,0,2.72},{.01,6,2.72},{.01,6,2.9},{.01,0,2.9}},'barwalltrim',0,2)
 quad({{7.49,6,2.72},{7.49,0,2.72},{7.49,0,2.9},{7.49,6,2.9}},'barwalltrim',0,2)
 -- Window on north wall, table daylight kept subdued; dominant lamp remains the same world source.
 box(2.6,5.95,1.1,4.45,6.01,2.45,'trim',0,2)
 quad({{2.67,5.94,1.17},{4.38,5.94,1.17},{4.38,5.94,2.38},{2.67,5.94,2.38}},'slate',0,2)
 -- Door leaf is built in local coordinates and rigidly rotated about the west hinge axis.
 local a=math.rad(angle) local ca,sa=math.cos(a),math.sin(a)
 local function dp(u,v,z) u=u*(1.046/1.05) return {1.4+u*ca-(v-.022)*sa,-.025+u*sa+(v-.022)*ca,z} end
 local function dq(u0,v0,z0,u1,v1,z1,mat)
  local coords={{u0,v0,z0},{u1,v0,z0},{u1,v0,z1},{u0,v0,z1}} local vv={} for _,q in ipairs(coords) do vv[#vv+1]=dp(table.unpack(q)) end quad(vv,mat,8,3)
  vv={} for _,q in ipairs({{u1,v1,z0},{u0,v1,z0},{u0,v1,z1},{u1,v1,z1}}) do vv[#vv+1]=dp(table.unpack(q)) end quad(vv,mat,8,3)
  vv={dp(u0,v0,z1),dp(u1,v0,z1),dp(u1,v1,z1),dp(u0,v1,z1)} quad(vv,'edge',8,3)
  vv={dp(u0,v1,z0),dp(u0,v0,z0),dp(u0,v0,z1),dp(u0,v1,z1)} quad(vv,'trim',8,3)
  vv={dp(u1,v0,z0),dp(u1,v1,z0),dp(u1,v1,z1),dp(u1,v0,z1)} quad(vv,'trim',8,3)
 end
 dq(0,-.022,0,.08,.022,2.15,'doorwood') dq(.97,-.022,0,1.05,.022,2.15,'doorwood')
 dq(.08,-.022,0,.97,.022,.78,'doorwood') dq(.16,-.030,.12,.89,.030,.66,'wood') dq(.08,-.022,2.04,.97,.022,2.15,'doorwood')
 dq(.08,-.022,.78,.97,.022,.85,'edge')
 quad({dp(.08,0,.85),dp(.97,0,.85),dp(.97,0,2.04),dp(.08,0,2.04)},'glass',8,3)
 -- Brass lever exists on BOTH faces, carried by the same rigid leaf transform.
 dq(.86,-.065,.97,.90,.065,1.04,'brass') dq(.77,-.072,.99,.92,-.045,1.015,'brass') dq(.77,.045,.99,.92,.072,1.015,'brass')
 for _,z in ipairs({.28,1.80}) do box(1.377,-.057,z,1.423,.007,z+.09,'brass',8,3) end
end
-- All exterior details share one surveyed street south of the cafe.
local function southStreet()
 local function ground(y0,y1,z,mat)
  quad({{-18,y0,z},{18,y0,z},{18,y1,z},{-18,y1,z}},mat,9,6)
 end
 local function facadeRect(x0,x1,z0,z1,y,mat)
  quad({{x1,y,z0},{x0,y,z0},{x0,y,z1},{x1,y,z1}},mat,9,6)
 end
 ground(-3,0,.003,'pavement')
 ground(-3.14,-3,.002,'curbstone')
 ground(-7.15,-3.14,.001,'street')
 ground(-7.31,-7.15,.002,'curbstone')
 ground(-9,-7.31,.003,'pavement')
 -- Joint lines taper with the same camera as both openings.
 for x=-16,16 do
  quad({{x,-3,.004},{x+.018,-3,.004},{x+.018,0,.004},{x,0,.004}},'streetBase',9,6)
  quad({{x,-9,.004},{x+.018,-9,.004},{x+.018,-7.31,.004},{x,-7.31,.004}},'streetBase',9,6)
 end
 facadeRect(-18,18,0,14,-9,'streetBrick')
 facadeRect(-18,-.9,0,14,-8.99,'streetBrickAlt')
 facadeRect(-18,18,0,.53,-8.98,'streetBase')
 for z=.65,8,.27 do
  facadeRect(-18,18,z,z+.017,-8.975,'streetStone')
  local shift=(math.floor(z*100+.5)%2)*.32
  for x=-16,16,.64 do
   facadeRect(x+shift,x+shift+.017,z,z+.27,-8.974,'streetStone')
  end
 end
 for _,x in ipairs({-6.4,-3.7,-.9,2.25,5.15,8.05}) do
  facadeRect(x-.13,x+.13,0,10,-8.92,'streetStone')
 end
 for _,row in ipairs({{.84,2.6},{3.43,5.03},{5.75,7.26},{7.97,9.48}}) do
  for j,x in ipairs({-5.05,-2.32,.66,3.7,6.6}) do
   local lo,hi=row[1],row[2]
   facadeRect(x-.87,x+.87,lo-.13,hi+.12,-8.91,'streetTrim')
   facadeRect(x-.73,x+.73,lo,hi,-8.90,j%2==0 and 'streetLit' or 'streetWindow')
   facadeRect(x-.043,x+.043,lo,hi,-8.89,'streetTrim')
   facadeRect(x-.73,x+.73,lo+(hi-lo)*.64,lo+(hi-lo)*.64+.07,-8.88,'streetTrim')
   facadeRect(x-.93,x+.93,lo-.12,lo-.065,-8.89,'curbstone')
  end
 end
 facadeRect(-18,18,2.89,3.07,-8.91,'streetStone')
 facadeRect(-18,18,5.31,5.43,-8.91,'streetStone')
end
local shadowFootprints={{5,1.3,5.7,4.8,.995},{.1,1.1,.6,2,1.75},{.15,.1,1.1,.55,1.8},{2.9,.15,3.9,.7,.76},{1.08,4.48,1.72,5.12,.72}}
local function floorShadow(wx,wy)
 local L={5.1,3,2.8}
 for _,b in ipairs(shadowFootprints) do
  local t=L[3]/(L[3]-b[5]) local sx0=L[1]+t*(b[1]-L[1]) local sx1=L[1]+t*(b[3]-L[1]) local sy0=L[2]+t*(b[2]-L[2]) local sy1=L[2]+t*(b[4]-L[2])
  -- The convex top-to-footprint hull is evaluated using triangles in world floor.
  local points={{b[1],b[2]},{b[3],b[2]},{b[3],b[4]},{b[1],b[4]},{sx0,sy0},{sx1,sy0},{sx1,sy1},{sx0,sy1}}
  table.sort(points,function(a,c) return a[1]<c[1] or (a[1]==c[1] and a[2]<c[2]) end)
  local hull={}
  local function cross(o,a,c)return (a[1]-o[1])*(c[2]-o[2])-(a[2]-o[2])*(c[1]-o[1]) end
  for _,a in ipairs(points) do while #hull>=2 and cross(hull[#hull-1],hull[#hull],a)<=0 do table.remove(hull) end hull[#hull+1]=a end
  local n=#hull for i=#points-1,1,-1 do local a=points[i] while #hull>n and cross(hull[#hull-1],hull[#hull],a)<=0 do table.remove(hull) end hull[#hull+1]=a end
  local inside=true for i=1,#hull-1 do if cross(hull[i],hull[i+1],{wx,wy})<0 then inside=false break end end
  if inside then return .76 end
 end
 return 1
end
local shadowCache={} for yy=0,127 do for xx=0,159 do shadowCache[yy*160+xx]=floorShadow((xx+.5)/160*7.5,(127-yy+.5)/128*6) end end
local W,H=480,270
local function sample(mat,u,v,shade)
 local m=mats[mat] local r,g,b,a
 if m.texture then local t=textures[m.texture] local bb=t.bounds
  if mat=='floor' then u=(u*7.5/(40/64))%1 v=(v*6/(56/64))%1 end
  u=math.max(0,math.min(.999999,u)) v=math.max(0,math.min(.999999,v))
  local c=t.image:getPixel(bb[1]+math.floor(u*(bb[3]-bb[1])),bb[2]+math.floor(v*(bb[4]-bb[2])))
  a=pc.rgbaA(c) if a<128 and m.masked then return nil end r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
 else r,g,b=table.unpack(m.rgb) a=255 if m.grain then local t=textures.wall local c=t.image:getPixel(t.bounds[1]+math.floor(math.max(0,math.min(.9999,u))*(t.bounds[3]-t.bounds[1])),t.bounds[2]+math.floor(math.max(0,math.min(.9999,v))*(t.bounds[4]-t.bounds[2]))) local lum=(pc.rgbaR(c)+pc.rgbaG(c)+pc.rgbaB(c))/3 local d=math.floor((lum-35)/6) r,g,b=r+d,g+d,b+d end end
 if m.texture=='doorwood' then local lum=(r+g+b)/3 if lum<65 then r,g,b=42,30,33 elseif lum<105 then r,g,b=86,57,42 elseif lum<155 then r,g,b=132,91,58 else r,g,b=176,132,86 end end
 if m.texture=='wall' or m.texture=='floor' or m.texture=='counter' or m.texture=='woodtop' or m.texture=='facade' or m.texture=='barwalltrim' or m.texture=='barlamp' then local yy=(54*r+183*g+19*b)/256 local lift=32*(1-yy/255) r=math.min(255,r+lift*1.1) g=math.min(255,g+lift*.86) b=math.min(255,b+lift*.55) end
 local factor=(m.shade or 1)*shade
 -- Native limited tonal steps preserve pixel art instead of smooth synthetic shading.
 r=math.min(255,math.floor(r*factor/4+.5)*4) g=math.min(255,math.floor(g*factor/4+.5)*4) b=math.min(255,math.floor(b*factor/4+.5)*4)
 return rgba(r,g,b,m.alpha or 255)
end
local laptopVisibility={}
local function render(cam,angle,id)
 scene(angle) if cam.street then southStreet() end local image=Image(W,H,ColorMode.RGB) local zbuf,cols,groups,cats,glass,warm={},{},{},{},{},{}
 local yaw,pitch=math.rad(cam.yaw or 0),math.rad(cam.pitch or 0) local fx=W/(2*math.tan(math.rad((cam.fov or 90)/2)))
 local right={math.cos(yaw),-math.sin(yaw),0} local forward={math.sin(yaw),math.cos(yaw),0}
 local function convert(v)
  local dx,dy,dz=v[1]-cam.x,v[2]-cam.y,v[3]-cam.z local across=dx*right[1]+dy*right[2] local along=dx*forward[1]+dy*forward[2]
  return {across,math.sin(pitch)*along+math.cos(pitch)*dz,math.cos(pitch)*along-math.sin(pitch)*dz,v[4],v[5]}
 end
 local function clip(vs)
  local out={} local prev=vs[#vs]
  for _,cur in ipairs(vs) do
   if (prev[3]>.04)~=(cur[3]>.04) then local t=(.04-prev[3])/(cur[3]-prev[3]) local p={} for k=1,5 do p[k]=prev[k]+(cur[k]-prev[k])*t end out[#out+1]=p end
   if cur[3]>.04 then out[#out+1]=cur end prev=cur
  end return out
 end
 local function tri(a,b,c,face,isglass)
  local vs={a,b,c} for _,v in ipairs(vs) do v.sx=W/2+fx*v[1]/v[3] v.sy=H/2-fx*v[2]/v[3] end
  local den=(b.sy-c.sy)*(a.sx-c.sx)+(c.sx-b.sx)*(a.sy-c.sy) if math.abs(den)<.0001 then return end
  local minx,maxx=math.max(0,math.floor(math.min(a.sx,b.sx,c.sx))),math.min(W-1,math.ceil(math.max(a.sx,b.sx,c.sx)))
  local miny,maxy=math.max(0,math.floor(math.min(a.sy,b.sy,c.sy))),math.min(H-1,math.ceil(math.max(a.sy,b.sy,c.sy)))
  for y=miny,maxy do for x=minx,maxx do
   local px,py=x+.5,y+.5 local aa=((b.sy-c.sy)*(px-c.sx)+(c.sx-b.sx)*(py-c.sy))/den local bb=((c.sy-a.sy)*(px-c.sx)+(a.sx-c.sx)*(py-c.sy))/den local cc=1-aa-bb
   if aa>=-.00001 and bb>=-.00001 and cc>=-.00001 then
    local iz=aa/a[3]+bb/b[3]+cc/c[3] local depth=1/iz local k=y*W+x
    if not zbuf[k] or depth<zbuf[k]-.000001 then
     local u=(aa*a[4]/a[3]+bb*b[4]/b[3]+cc*c[4]/c[3])/iz local v=(aa*a[5]/a[3]+bb*b[5]/b[3]+cc*c[5]/c[3])/iz
     local shading=face.shade or 1 if face.mat=='floor' then shading=shadowCache[math.min(127,math.max(0,math.floor(v*128)))*160+math.min(159,math.max(0,math.floor(u*160)))] or 1 end
     local color=sample(face.mat,u,v,shading)
     if color then if isglass then if not glass[k] or depth<glass[k].depth then glass[k]={color=color,category=face.category,depth=depth} end else zbuf[k]=depth cols[k]=color groups[k]=face.group cats[k]=face.category if face.mat=='floor' then local wx,wy=u*7.5,(1-v)*6 local d=math.sqrt((wx-5.1)^2+(wy-3)^2) local aa=math.floor(math.max(0,1-d/2.8)*18*shading) warm[k]=aa>0 and rgba(248,190,108,aa) or nil else warm[k]=nil end end end
    end
   end
  end end
 end
 for pass=1,2 do for _,face in ipairs(faces) do
  local isglass=mats[face.mat].alpha~=nil
  if (pass==2)==isglass and not(cam.hideDoor and face.category==3) then
   local verts={} for _,v in ipairs(face.v) do verts[#verts+1]=convert(v) end verts=clip(verts)
   if #verts>=3 then for i=2,#verts-1 do tri(verts[1],verts[i],verts[i+1],face,isglass) end end
  end
 end end
 local layers={} local full=Image(W,H,ColorMode.RGB)
 local laptopPixels=0
 for y=0,H-1 do for x=0,W-1 do local k=y*W+x local c=cols[k] or rgba(37,30,34) local group=groups[k] or 0
  if group==2 then laptopPixels=laptopPixels+1 end
  local ln=cam.exterior and ((cats[k]==3 and 'door') or (cats[k]==1 and 'facade') or 'glimpse') or ('g'..group)
  if not layers[ln] then layers[ln]=Image(W,H,ColorMode.RGB) end layers[ln]:drawPixel(x,y,c)
  if warm[k] then
   local wc=warm[k] local a=pc.rgbaA(wc)/255
   local lit=rgba(math.floor(pc.rgbaR(wc)*a+pc.rgbaR(c)*(1-a)),math.floor(pc.rgbaG(wc)*a+pc.rgbaG(c)*(1-a)),math.floor(pc.rgbaB(wc)*a+pc.rgbaB(c)*(1-a)))
   if cam.exterior then layers[ln]:drawPixel(x,y,lit) else if not layers.light then layers.light=Image(W,H,ColorMode.RGB) end layers.light:drawPixel(x,y,wc) end c=lit
  end
  if glass[k] then
   if layers.light then layers.light:drawPixel(x,y,clear) end
   local gc=glass[k].color local alpha=pc.rgbaA(gc)/255
   local blended=rgba(math.floor(pc.rgbaR(gc)*alpha+pc.rgbaR(c)*(1-alpha)),math.floor(pc.rgbaG(gc)*alpha+pc.rgbaG(c)*(1-alpha)),math.floor(pc.rgbaB(gc)*alpha+pc.rgbaB(c)*(1-alpha)))
   if cam.exterior then local gn=glass[k].category==3 and 'door' or 'facade' if not layers[gn] then layers[gn]=Image(W,H,ColorMode.RGB) end layers[gn]:drawPixel(x,y,gn==ln and blended or gc) else layers[ln]:drawPixel(x,y,blended) end
   c=blended
  end
  full:drawPixel(x,y,c)
 end end
 return full,layers
end
local delivered={}
local function saveAsset(path,img,pivot,kind,extra)
 local s=Sprite(img.width,img.height,ColorMode.RGB) s.layers[1].name=kind s:newCel(s.layers[1],1,img)
 local sl=s:newSlice(Rectangle(0,0,img.width,img.height)) sl.name='delivery_pivot' sl.pivot=Point(pivot[1],pivot[2])
 s:saveAs(output..path..'.aseprite') s:saveCopyAs(output..path..'.png') s:close()
 delivered[#delivered+1]={path=path,width=img.width,height=img.height,pivot=pivot,kind=kind,extra=extra}
end
local cam={x=3.3,y=5.8,z=2.3,yaw=180,pitch=22,fov=90}
local original,originalLayers=render(cam,90)
cam.street=true
local updated,streetLayers=render(cam,90)
local shell=Image{fromFile=root..'ready/interior/g0.png'}
local backdrop=Image(W,H,ColorMode.RGB)
local mask=Image(W,H,ColorMode.RGB)
local wall=Image(W,H,ColorMode.RGB)
local counts={window=0,door=0,leafGlazing=0}
local sin,cos=math.sin(math.rad(cam.pitch)),math.cos(math.rad(cam.pitch))
for y=0,H-1 do for x=0,W-1 do
 local old=shell:getPixel(x,y)
 local hit=streetLayers.g9 and streetLayers.g9:getPixel(x,y) or clear
 if pc.rgbaA(hit)>0 then
  assert(pc.rgbaA(old)==255, "Exterior reached foreground artwork")
  local h,v=(x+.5-W/2)/240,(H/2-y-.5)/240
  local t=cam.y/(cos+v*sin)
  local wx,wz=cam.x-t*h,cam.z+t*(v*cos-sin)
  local id=wx>=2.785 and wx<=4.015 and wz>=1.075 and wz<=2.245 and 'window'
     or wx>=1.30 and wx<=2.55 and wz>=-.06 and wz<=2.25 and 'door'
  if not id and wx>=2.45 and wx<=2.75 and wz>=0 and wz<=2.20 then id='leafGlazing' end
  assert(id, string.format("Exterior escaped apertures at %d,%d world %.3f,%.3f",x,y,wx,wz))
  backdrop:drawPixel(x,y,hit)
  mask:drawPixel(x,y,rgba(255,255,255))
  counts[id]=counts[id]+1
 else
  wall:drawPixel(x,y,old)
 end
end end
saveAsset('ready/interior/street_backdrop',backdrop,{240,135},'Door and window masked southern street')
saveAsset('ready/interior/g0',wall,{240,135},'Original room shell with exact aperture cutouts')
saveAsset('verification/aperture_mask',mask,{240,135},'Verification mask')
local master=Sprite(W,H,ColorMode.RGB)
master.layers[1].name='Street backdrop'
master:newCel(master.layers[1],1,backdrop)
local wallLayer=master:newLayer() wallLayer.name='Room shell with apertures' master:newCel(wallLayer,1,wall)
for _,name in ipairs({'g1','g2','g3','g4','g5','g6','g7','g8','light'}) do
 local l=master:newLayer() l.name=name
 master:newCel(l,1,Image{fromFile=root..'ready/interior/'..name..'.png'})
end
local pivot=master:newSlice(Rectangle(0,0,W,H)) pivot.name='delivery_pivot' pivot.pivot=Point(240,135)
master:saveAs(output..'ready/interior/ct_interior_master.aseprite')
master:saveCopyAs(output..'previews/interior-composite.png')
master:close()
local f=assert(io.open(output..'verification/aperture-counts.json','w')) f:write(json.encode(counts)) f:close()
