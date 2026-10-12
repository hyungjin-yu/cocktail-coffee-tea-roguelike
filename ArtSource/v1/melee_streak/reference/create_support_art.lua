-- Original pixel drawings, rendered and exported by Aseprite. No AI credentials.
local root = app.params.out or 'ArtProduction/2026-10-04/assets'
app.fs.makeAllDirectories(root)
local function rgb(h,a)
  return app.pixelColor.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),a or 255)
end
local C={ink=rgb('201D29'),shade=rgb('4A4050'),cream=rgb('F4F1E7'),paper=rgb('EAD4A5'),gold=rgb('DCAE72'),brown=rgb('714732'),coffee=rgb('302028'),teal=rgb('52C5C1'),blue=rgb('49C9F2'),green=rgb('466B45'),leaf=rgb('8EAD68'),red=rgb('EF6558'),light=rgb('FFE2AC'),purple=rgb('A66FAE'),metal=rgb('A7B1C2')}
local im
local function px(x,y,c) x=math.floor(x);y=math.floor(y);if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end end
local function rect(x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do px(xx,yy,c) end end end
local function line(x1,y1,x2,y2,c,t)
  local n=math.max(math.abs(x2-x1),math.abs(y2-y1),1)
  for i=0,n do rect(math.floor(x1+(x2-x1)*i/n),math.floor(y1+(y2-y1)*i/n),t or 1,t or 1,c) end
end
local function ellipse(cx,cy,rx,ry,c,inner)
  for y=math.floor(cy-ry),math.ceil(cy+ry) do for x=math.floor(cx-rx),math.ceil(cx+rx) do
    local q=((x-cx)/rx)^2+((y-cy)/ry)^2
    if q<=1 and (not inner or q>=inner) then px(x,y,c) end
  end end
end
local function poly(points,c)
  for y=0,im.height-1 do
    local xs={}
    for i=1,#points do local a=points[i];local b=points[i%#points+1]
      if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then xs[#xs+1]=a[1]+(y-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
    end
    table.sort(xs)
    for i=1,#xs-1,2 do for x=math.ceil(xs[i]),math.floor(xs[i+1]) do px(x,y,c) end end
  end
end
local function diamond(x,y,r,c) poly({{x,y-r},{x+r,y},{x,y+r},{x-r,y}},c) end
local function bean(x,y,r)
  ellipse(x,y,r*.75,r,C.ink);ellipse(x,y-1,r*.75-2,r-2,C.brown)
  ellipse(x-2,y-3,r*.4,r*.65,C.gold)
  line(x+1,y-r+3,x-2,y,C.coffee,2);line(x-2,y,x+1,y+r-3,C.coffee,2)
end
local function cup(x,y,w,h)
  rect(x+3,y+3,w,h,C.ink);rect(x+5,y+5,w-4,h-5,C.paper)
  rect(x+5,y+5,3,h-6,C.cream);rect(x+3,y+3,w,3,C.cream)
  ellipse(x+w+4,y+h*.45,5,6,C.ink,.4);ellipse(x+w+4,y+h*.45,3,4,C.gold,.4)
  rect(x,y+h+3,w+8,3,C.ink);rect(x+2,y+h+3,w+4,1,C.gold)
end
local function heart(x,y,s,c)
  poly({{x-s,y},{x-s,y-s/2},{x-s/2,y-s},{x,y-s/2},{x+s/2,y-s},{x+s,y-s/2},{x+s,y},{x,y+s}},c)
end
local function leaf(x,y,r)
  poly({{x-r,y+r/2},{x-r/2,y-r/2},{x+r,y-r},{x+r/2,y+r/2},{x,y+r}},C.ink)
  poly({{x-r+2,y+r/2-1},{x-r/2+1,y-r/2+2},{x+r-2,y-r+2},{x+r/2-1,y+r/2-2},{x,y+r-2}},C.leaf)
  line(x-r+2,y+r/2,x+r-3,y-r+3,C.green,2)
end
local function jar()
  rect(16,14,31,7,C.ink);rect(18,15,27,4,C.metal)
  rect(18,21,27,29,C.ink);rect(20,23,23,25,C.brown);rect(22,24,3,23,C.gold)
  bean(31,35,7)
end
local function spark(cx,cy,r,col)
  poly({{cx,cy-r},{cx+2,cy-2},{cx+r,cy},{cx+2,cy+2},{cx,cy+r},{cx-2,cy+2},{cx-r,cy},{cx-2,cy-2}},col)
end
local function star(cx,cy,r,angle,col)
  local p={};for k=0,9 do local a=angle-math.pi/2+k*math.pi/5;local rr=k%2==0 and r or r*.43;p[#p+1]={cx+math.cos(a)*rr,cy+math.sin(a)*rr} end;poly(p,col)
end
local entries={}
local function add(id,w,h,n,fps,loop,kind,draw,border)
  entries[#entries+1]={id=id,w=w,h=h,n=n or 1,fps=fps or 0,loop=loop or false,kind=kind or 'vfx',draw=draw,border=border}
end
-- 128px icons use a consistent 2px pixel grid and upper-left highlights.
add('ui_pause',128,128,1,0,false,'ui',function() rect(17,12,12,40,C.ink);rect(35,12,12,40,C.ink);rect(19,14,8,36,C.cream);rect(37,14,8,36,C.cream);rect(19,47,8,3,C.gold);rect(37,47,8,3,C.gold) end)
add('ui_settings',128,128,1,0,false,'ui',function()
  local function gear(r,col)
    local points={}
    for i=0,5 do for _,q in ipairs({{-.40,r-5},{-.22,r},{.22,r},{.40,r-5}}) do
      local a=i*math.pi/3+q[1];points[#points+1]={32+math.cos(a)*q[2],32+math.sin(a)*q[2]}
    end end;poly(points,col)
  end
  gear(25,C.ink);gear(22,C.cream);ellipse(32,33,10,10,C.gold);ellipse(32,32,8,8,C.ink);ellipse(32,32,5,5,0)
end)
add('ui_codex',128,128,1,0,false,'ui',function()
  poly({{9,15},{29,19},{32,23},{35,19},{55,15},{55,47},{34,52},{30,52},{9,47}},C.ink)
  poly({{11,17},{29,21},{31,24},{31,49},{11,45}},C.brown);poly({{33,24},{35,21},{53,17},{53,45},{33,49}},C.brown)
  poly({{13,18},{28,22},{29,24},{29,46},{13,43}},C.paper);poly({{35,24},{37,22},{51,18},{51,43},{35,46}},C.cream)
  for y=27,38,5 do line(16,y,25,y+2,C.gold);line(38,y+2,48,y,C.paper) end
end)
for _,id in ipairs({'ui_save_ok','ui_save_error'}) do add(id,128,128,1,0,false,'ui',function()
  jar();ellipse(45,46,12,12,C.ink);ellipse(45,46,10,10,id=='ui_save_ok' and C.teal or C.red)
  if id=='ui_save_ok' then line(38,46,43,51,C.cream,2);line(43,51,51,40,C.cream,2) else rect(44,39,3,9,C.cream);rect(44,51,3,3,C.cream) end
end) end
add('seal_lock',128,128,1,0,false,'ui',function()
  poly({{16,10},{45,10},{45,51},{40,48},{36,52},{31,48},{26,52},{21,48},{16,51}},C.ink)
  rect(18,12,25,34,C.purple);line(23,20,36,36,C.cream,3);line(36,20,23,36,C.cream,3)
end)
add('skill_attack',128,128,1,0,false,'ui',function() cup(8,26,25,20);poly({{37,33},{47,9},{51,7},{52,12},{41,36}},C.ink);line(39,31,49,10,C.metal,3);line(33,34,45,38,C.gold,2);line(37,37,34,46,C.brown,3) end)
add('skill_survival',128,128,1,0,false,'ui',function()
  rect(17,12,29,39,C.ink);rect(19,15,25,33,C.paper);rect(17,10,29,6,C.metal);rect(20,17,4,29,C.cream);heart(32,31,8,C.ink);heart(32,30,6,C.red)
end)
add('skill_ingredient',128,128,1,0,false,'ui',function()
  poly({{8,42},{55,42},{50,53},{13,53}},C.ink);poly({{11,44},{52,44},{48,50},{15,50}},C.metal)
  bean(20,31,9);bean(36,29,10);bean(45,36,7)
end)
add('specialty',128,128,1,0,false,'ui',function() ellipse(32,32,24,24,C.ink);ellipse(32,32,22,22,C.gold);ellipse(32,32,18,18,C.coffee);ellipse(27,24,19,18,C.light,.85);bean(32,32,14);spark(48,16,5,C.cream) end)
add('status_suppressed',128,128,1,0,false,'ui',function()
  poly({{24,47},{24,28},{15,28},{32,10},{49,28},{40,28},{40,47}},C.ink);poly({{27,44},{27,25},{22,25},{32,15},{42,25},{37,25},{37,44}},C.gold)
  line(12,50,49,13,C.ink,5);line(13,50,50,13,C.red,2)
end)
add('status_invulnerable',128,128,1,0,false,'ui',function()
  poly({{32,8},{51,16},{48,38},{40,49},{32,56},{24,49},{16,38},{13,16}},C.ink)
  poly({{32,11},{48,18},{45,37},{38,47},{32,52},{26,47},{19,37},{16,18}},rgb('49C9F2',100))
  line(17,18,32,12,C.cream,2);line(17,18,20,34,C.cream,2);line(32,15,32,48,C.blue)
end)
add('status_slow',128,128,1,0,false,'ui',function()
  poly({{32,8},{48,29},{50,39},{45,50},{32,56},{19,50},{14,39},{16,29}},C.ink)
  poly({{32,12},{45,30},{47,39},{42,48},{32,52},{22,48},{17,39},{19,30}},C.gold)
  for _,p in ipairs({{25,34},{38,40}}) do ellipse(p[1],p[2],4,6,C.coffee);ellipse(p[1],p[2]+6,3,3,C.coffee);for t=-1,1 do ellipse(p[1]+t*3,p[2]-8,1.4,1.4,C.coffee) end end
  ellipse(23,24,2,3,C.cream)
end)
add('glass_shard',128,128,1,0,false,'vfx',function() poly({{13,43},{49,16},{38,49}},C.ink);poly({{17,42},{45,20},{36,46}},C.blue);line(17,42,44,21,C.cream,2) end)
add('bullet',128,128,1,0,false,'vfx',function() poly({{15,27},{42,27},{51,32},{42,37},{15,37}},C.ink);poly({{17,29},{41,29},{47,32},{41,35},{17,35}},C.gold);rect(18,29,22,2,C.light) end)
for _,id in ipairs({'arrow','tea_arrow'}) do add(id,128,128,1,0,false,'vfx',function()
  line(12,32,47,32,C.ink,3);line(12,33,46,33,id=='tea_arrow' and C.teal or C.brown)
  poly({{45,25},{57,33},{45,41},{48,33}},C.ink);poly({{47,28},{53,33},{47,37},{49,33}},C.cream)
  if id=='tea_arrow' then leaf(15,27,7);leaf(15,38,7) else poly({{9,24},{17,27},{23,32},{14,31}},C.paper);poly({{9,42},{17,39},{23,34},{14,35}},C.cream) end
end) end
add('leaf_mote',128,128,1,0,false,'vfx',function() leaf(32,32,17) end)
add('ground_shadow',256,128,1,0,false,'vfx',function() ellipse(64,32,52,16,rgb('000000')) end)
add('warning_line',128,128,1,0,false,'vfx',function() rect(5,30,54,4,C.red);rect(5,31,54,1,C.light) end)
local function ring(frame,col,jagged,segmented)
  local cx,cy=64,32;local rx,ry=54,21
  for y=4,59 do for x=5,122 do
    local a=math.atan((y-cy)/ry,(x-cx)/rx);local q=((x-cx)/rx)^2+((y-cy)/ry)^2
    local k=jagged and (.95+.05*math.cos(a*24)) or 1
    if q<k and q>k-.15 and (not segmented or math.floor((a+math.pi)*8/math.pi)%2==0) then px(x,y,col) end
  end end
end
add('warning_ring',256,128,1,0,false,'vfx',function() ring(0,C.red,true,false) end)
add('boss_arrival',256,128,4,8,false,'vfx',function(f) ring(f,f%2==0 and C.red or C.light,false,true) end)
add('summon_ring',256,128,4,8,true,'vfx',function(f)
  ring(f,f%2==0 and C.cream or C.metal,false,false)
  for i=0,3 do local a=i*math.pi/2;diamond(64+math.cos(a)*43,32+math.sin(a)*15,3+f%2,C.cream) end
end)
for _,id in ipairs({'sugar_pool','coffee_pool'}) do add(id,256,128,1,0,false,'vfx',function()
  ellipse(64,32,54,21,C.red);ellipse(64,32,51,18,id=='sugar_pool' and C.gold or C.coffee)
  ellipse(52,26,30,8,id=='sugar_pool' and C.light or C.brown)
  if id=='sugar_pool' then for i=0,15 do local a=i*math.pi/8;rect(math.floor(64+52*math.cos(a))-2,math.floor(32+20*math.sin(a))-1,4,3,i%2==0 and C.red or 0) end end
end) end
add('player_melee_area',256,128,1,0,false,'vfx',function() rect(4,4,120,56,C.teal);rect(6,6,116,52,0) end)
add('hit_spark',128,128,4,16,false,'vfx',function(f)
  local r=({7,19,24,10})[f+1];spark(32,32,r,C.gold);spark(32,32,math.max(2,r-4),C.cream)
  if f>0 then for i=0,3 do local a=i*math.pi/2+.7;diamond(32+math.cos(a)*(12+f*5),32+math.sin(a)*(12+f*5),math.max(1,3-f),C.gold) end end
end)
add('pickup_glint',128,128,4,12,false,'vfx',function(f) spark(32,32,({5,15,22,8})[f+1],C.paper);spark(32,32,({3,10,14,4})[f+1],C.cream) end)
add('levelup_glint',128,128,4,12,false,'vfx',function(f) for i=-1,1 do local y=46-f*8+math.abs(i)*5;line(32+i*13,y,32+i*13,y-10,C.teal,2);rect(32+i*13,y-11,2,2,C.cream) end end)
add('melee_streak',128,128,4,16,false,'vfx',function(f)
  local a=({18,8,5,29})[f+1];local b=({40,55,59,55})[f+1]
  poly({{a,30},{b-5,31},{b,32},{b-5,33},{a,34}},C.cream);line(a,32,b,32,C.gold)
end)
add('steam_jet',128,128,4,12,true,'vfx',function(f)
  for i=0,5 do local x=8+i*8+f*2;local y=32+math.floor(math.sin(i+f*1.3)*5);ellipse(x,y,5+i%2,4+i%3,rgb('A7B1C2',180));ellipse(x-1,y-2,4+i%2,3+i%3,rgb('F4F1E7',225)) end
end)
add('review_star',128,128,4,12,true,'vfx',function(f)
  local a=f*math.pi/2;star(32,32,25,a,C.light);star(32,32,22,a,C.red)
  -- The fracture rotates with the star, preserving identity.
  local function rot(x,y) return 32+x*math.cos(a)-y*math.sin(a),32+x*math.sin(a)+y*math.cos(a) end
  local x1,y1=rot(-1,-12);local x2,y2=rot(2,-2);local x3,y3=rot(-3,2);local x4,y4=rot(1,12)
  line(x1,y1,x2,y2,C.ink,2);line(x2,y2,x3,y3,C.ink,2);line(x3,y3,x4,y4,C.ink,2)
end)
add('seal_burst',128,128,4,12,false,'vfx',function(f)
  local d=({19,11,4,14})[f+1]
  for i=0,3 do local a=math.pi/4+i*math.pi/2;local x=math.floor(32+math.cos(a)*d);local y=math.floor(32+math.sin(a)*d);rect(x-3,y-4,6,8,f==3 and rgb('EAD4A5',100) or C.paper);rect(x-3,y-4,6,2,C.purple) end
  if f==2 then line(24,24,40,40,C.purple,3);line(40,24,24,40,C.purple,3) end
end)
add('pressure_wave',256,128,4,12,false,'vfx',function(f)
  local rx=({16,30,43,54})[f+1];local ry=({6,11,16,21})[f+1]
  ellipse(64,32,rx,ry,C.red,.74);ellipse(64,32,rx-3,math.max(2,ry-2),rgb('F4F1E7',180),.75)
end)
add('hybrid_tea_martini',128,128,1,0,false,'ui',function()
  ellipse(32,32,26,26,C.ink);ellipse(32,32,24,24,C.teal,.78)
  line(17,48,46,16,C.ink,5);line(18,47,46,16,C.metal,3);line(16,38,27,47,C.gold,2);line(18,45,12,52,C.brown,3)
  line(18,16,47,47,C.ink,3);line(18,16,47,47,C.gold)
  for i=0,25 do local t=i/25;local x=18+29*t+7*math.sin(t*math.pi);local y=16+31*t-7*math.sin(t*math.pi);rect(math.floor(x),math.floor(y),3,3,C.green);px(x,y,C.leaf) end
  rect(34,27,5,5,C.metal)
end)
add('weapon_tea_bow',256,256,1,0,false,'vfx',function()
  line(73,30,73,96,C.ink,2);line(74,31,74,95,C.gold)
  for y=30,96 do local t=(y-30)/66;local x=73-29*math.sin(t*math.pi);rect(math.floor(x),y,5,2,C.ink);rect(math.floor(x)+1,y,3,1,C.green);px(x+1,y,C.leaf) end
  rect(43,59,7,12,C.ink);rect(45,60,3,10,C.metal);rect(45,60,1,10,C.cream);leaf(66,36,6)
end)
local function flames(f,cx,ground,col,highlight)
  local profiles={
    {3,7,16,10,6,12,24,13,8,17,28,16,7,10,19,9,3},
    {3,10,20,12,5,9,17,10,6,16,23,14,9,18,25,12,3},
    {3,9,14,8,10,20,28,15,7,12,18,10,6,14,22,10,3},
    {3,6,12,20,12,7,18,24,13,8,15,26,15,7,14,8,3}}
  local heights={};local p=profiles[f+1]
  for x=-40,40 do local t=(x+40)/5;local i=math.min(16,math.floor(t)+1);local q=t-math.floor(t);heights[x]=math.floor(p[i]*(1-q)+p[i+1]*q) end
  local function solid(x,y) return heights[x] and y<=ground and y>=ground-heights[x] end
  for x=-40,40 do for y=ground-30,ground do if solid(x,y) then
    local edge=not solid(x-1,y) or not solid(x+1,y) or not solid(x,y-1) or not solid(x,y+1)
    local core=y>ground-heights[x]*.46+math.sin((x+f*7)*.25)*2
    px(cx+x,y,edge and C.ink or core and highlight or col)
  end end end
end
add('olive_fire',256,128,4,8,true,'vfx',function(f) ring(f,C.red,true,false);flames(f,64,40,C.blue,C.cream) end)
add('player_fire',256,256,4,12,true,'vfx',function(f) ellipse(64,94,49,17,C.teal,.80);flames(f,64,94,rgb('F68B4D'),C.light) end)
add('player_poison',256,256,4,10,true,'vfx',function(f)
  ellipse(64,94,49,17,C.teal,.80)
  for i=-2,2 do local x=64+i*15+math.floor(math.sin(i+f*math.pi/2)*3);local y=82+math.floor(math.sin(i*1.7+f*math.pi/2)*5);ellipse(x,y,16+i%2,8,rgb('A66FAE',140));ellipse(x-3,y-3,10,5,rgb('DFC4E9',130)) end
  for i=-1,1 do local x=64+i*21+f%2*2;local y=72+((f+i+4)%4)*4;poly({{x-4,y+2},{x-1,y-2},{x+4,y-3},{x+2,y+2},{x,y+4}},C.purple);line(x-2,y+1,x+2,y-1,rgb('DFC4E9')) end
end)
for _,id in ipairs({'ui_panel','ui_card_frame','ui_button'}) do
  local w,h=256,id=='ui_card_frame' and 384 or id=='ui_button' and 64 or 256
  local b=id=='ui_button' and 12 or 16
  add(id,w,h,1,0,false,'ui',function()
    rect(2,2,w/2-4,h/2-4,C.ink);rect(3,3,w/2-6,h/2-6,C.paper);rect(4,4,w/2-8,h/2-8,rgb('3B3035'))
    rect(4,4,w/2-8,1,C.gold);rect(4,h/2-5,w/2-8,1,C.coffee)
    for _,p in ipairs({{5,5},{w/2-8,5},{5,h/2-8},{w/2-8,h/2-8}}) do rect(p[1],p[2],3,3,C.gold);px(p[1],p[2],C.cream) end
  end,{b,b,b,b})
end
local catalog={}
local function write(path,data) local f=assert(io.open(path,'w'));f:write(data);f:close() end
for _,e in ipairs(entries) do
  local dir=root..'/'..e.id;app.fs.makeAllDirectories(dir..'/frames')
  local s=Sprite(e.w,e.h,ColorMode.RGB);s.layers[1].name=e.id
  local files={};local preview=Image(e.w*e.n,e.h,ColorMode.RGB)
  for f=0,e.n-1 do
    im=Image(e.w/2,e.h/2,ColorMode.RGB);e.draw(f)
    local full=Image(e.w,e.h,ColorMode.RGB)
    for y=0,im.height-1 do for x=0,im.width-1 do local c=im:getPixel(x,y);for dy=0,1 do for dx=0,1 do full:drawPixel(x*2+dx,y*2+dy,c) end end end end
    if f>0 then s:newEmptyFrame() end
    local cel=s.cels[#s.cels]
    if f==0 and cel then cel.image=full;cel.position=Point(0,0) else s:newCel(s.layers[1],s.frames[f+1],full,Point(0,0)) end
    s.frames[f+1].duration=e.fps>0 and 1/e.fps or .1
    local name='frames/'..e.id..'_effect_right_'..string.format('%02d',f)..'.png'
    full:saveAs(dir..'/'..name);files[#files+1]=name;preview:drawImage(full,Point(e.w*f,0))
  end
  local tag=s:newTag(s.frames[1],s.frames[#s.frames]);tag.name='effect'
  s:saveAs(dir..'/'..e.id..'.aseprite');preview:saveAs(dir..'/preview.png');s:close()
  local m={specVersion='1.0',assetId=e.id,status='needs_fix',kind=e.kind,scope='04+09 support art',frameSizePx={e.w,e.h},pixelsPerUnit=128,pivotNormalized={.5,.5},files=files,sourceReferences={'create_support_art.lua'},deviations={},checks={structural='pending',visual='pending',unity='not_run'},spriteBorderPx=e.border,clips={effect={fps=e.fps,loop=e.loop,files=files}},provenance={tool='Aseprite 1.3.18.6 Lua',method='original pixel drawing',aiGenerationUnits=0}}
  write(dir..'/manifest.json',json.encode(m))
  catalog[#catalog+1]={assetId=e.id,size={e.w,e.h},frames=e.n,fps=e.fps,loop=e.loop,kind=e.kind,files=files}
end
write(root..'/catalog.json',json.encode(catalog))
print('Created '..#entries..' original support assets in '..root)
