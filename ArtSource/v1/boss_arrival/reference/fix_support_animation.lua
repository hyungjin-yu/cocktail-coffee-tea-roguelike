local root='ArtProduction/first-pass-ui'
local unity='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/v1'
local pc=app.pixelColor
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and yy>=0 and xx<im.width and yy<im.height then im:drawPixel(xx,yy,c) end end end end
for _,id in ipairs({'boss_arrival','summon_ring'}) do
 local dir=root..'/'..id;app.fs.makeAllDirectories(dir..'/frames');app.fs.makeAllDirectories(dir..'/reference')
 local f=assert(io.open(unity..'/'..id..'/manifest.json'));local m=json.decode(f:read('*a'));f:close()
 local sprite=Sprite(256,128,ColorMode.RGB);sprite.layers[1].name='distinct phase animation'
 local sheet=Image(1024,128,ColorMode.RGB)
 for phase=0,3 do
  local im=Image(128,64,ColorMode.RGB)
  if id=='boss_arrival' then
   local s=assert(app.open(unity..'/'..id..'/'..m.files[phase+1]));local base=s.cels[1].image:clone();s:close()
   im=Image(256,128,ColorMode.RGB)
   for it in base:pixels() do local c=it();if pc.rgbaA(c)>0 then im:drawPixel(it.x,it.y,pc.rgba(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),({255,225,195,165})[phase+1])) end end
  else
   local cream=pc.rgba(244,241,231,255);local faint=pc.rgba(234,212,165,220)
   for y=0,63 do for x=0,127 do local q=((x-64)/53)^2+((y-32)/18)^2;if q<=1 and q>=.86 then im:drawPixel(x,y,faint) end end end
   for k=0,3 do local a=k*math.pi/2+phase*math.pi/8;local cx,cy=math.floor(64+math.cos(a)*48),math.floor(32+math.sin(a)*15)
    for y=cy-2,cy+2 do for x=cx-2,cx+2 do if math.abs(x-cx)+math.abs(y-cy)<=2 then im:drawPixel(x,y,cream) end end end
   end
   local enlarged=Image(256,128,ColorMode.RGB);for y=0,127 do for x=0,255 do enlarged:drawPixel(x,y,im:getPixel(math.floor(x/2),math.floor(y/2))) end end;im=enlarged
  end
  im:saveAs(dir..'/'..m.files[phase+1]);sheet:drawImage(im,Point(phase*256,0))
  if phase==0 then sprite.cels[1].image=im else sprite:newFrame();sprite:newCel(sprite.layers[1],phase+1,im,Point(0,0)) end
  sprite.frames[phase+1].duration=1/8
 end
 sprite:saveAs(dir..'/'..id..'.aseprite');sprite:close();sheet:saveAs(dir..'/preview.png')
 m.status='needs_fix';m.checks={structural='pending',visual='pending',unity='not_run'};m.sourceReferences={unity..'/'..id..'/'..id..'.aseprite',root..'/fix_support_animation.lua'}
 m.provenance={tool='Aseprite 1.3.18.6 Lua',method='four visually distinct phase correction; original source preserved in reference'}
 local out=assert(io.open(dir..'/manifest.json','w'));local j=json.encode(m);j=j:gsub('"anchorsByFrame": %[%]','"anchorsByFrame": {}');out:write(j);out:close()
end
