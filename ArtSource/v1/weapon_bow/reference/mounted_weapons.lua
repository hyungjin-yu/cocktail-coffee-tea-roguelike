local root='ArtProduction/first-pass-ui'
local srcroot='C:/Users/user/Documents/Projects/CocktailCoffeeTea/Assets/_Project/Resources/Icons/Weapons/'
local pc=app.pixelColor
local function bounds(im)
 local b={im.width,im.height,-1,-1}
 for it in im:pixels() do if pc.rgbaA(it())>=128 then b[1]=math.min(b[1],it.x);b[2]=math.min(b[2],it.y);b[3]=math.max(b[3],it.x);b[4]=math.max(b[4],it.y) end end
 return b
end
-- Source icons point northeast; inverse nearest sampling rotates clockwise 45 degrees.
for _,v in ipairs({{'rapier',.8},{'gun',.4},{'sword',.65},{'bow',.7}}) do
 local name,relative=v[1],v[2];local id='weapon_'..name
 local s=assert(app.open(srcroot..name..'.png'));local original=s.cels[1].image:clone();s:close()
 local b=bounds(original);local cx,cy=(b[1]+b[3])/2,(b[2]+b[4])/2
 local w,h=b[3]-b[1]+1,b[4]-b[2]+1;local rotated=(w+h)/math.sqrt(2);local scale=220/rotated
 local im=Image(256,256,ColorMode.RGB);local cs,sn=math.cos(math.pi/4),math.sin(math.pi/4)
 -- Bow imagery also has northeast axis, but bows need a vertical stave and right-facing opening.
 if name=='bow' then cs=math.cos(-math.pi/4);sn=math.sin(-math.pi/4);scale=216/rotated end
 for y=0,254,2 do for x=0,254,2 do
  local dx,dy=(x+1-128)/scale,(y+1-128)/scale
  if name=='bow' then dx=-dx end
  local sx,sy=math.floor(cx+cs*dx+sn*dy),math.floor(cy-sn*dx+cs*dy)
  if sx>=0 and sy>=0 and sx<original.width and sy<original.height then
   local c=original:getPixel(sx,sy)
   if pc.rgbaA(c)>=128 then c=pc.rgba(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),255);for yy=y,y+1 do for xx=x,x+1 do im:drawPixel(xx,yy,c) end end end
  end
 end end
 if name=='bow' then local rot=Image(256,256,ColorMode.RGB);for y=0,255 do for x=0,255 do rot:drawPixel(x,y,im:getPixel(255-y,x)) end end;im=rot end
 local dir=root..'/'..id;app.fs.makeAllDirectories(dir..'/frames');app.fs.makeAllDirectories(dir..'/reference')
 local f='frames/'..id..'_effect_right_00.png';im:saveAs(dir..'/'..f);im:saveAs(dir..'/preview.png')
 local out=Sprite(256,256,ColorMode.RGB);out.cels[1].image=im;out.layers[1].name='mounted right';out:saveAs(dir..'/'..id..'.aseprite');out:close()
 local grip=name=='bow' and {76,128} or name=='gun' and {104,144} or {46,128};local muzzle=name=='bow' and {142,128} or {226,128}
 local anchors={grip=grip,muzzle=muzzle};if name=='bow' then anchors.arrow=muzzle;anchors.string=muzzle end
 local m={specVersion='1.0',assetId=id,scope='full_game_v1',kind='vfx',status='needs_fix',frameSizePx={256,256},pixelsPerUnit=128,pivotNormalized={grip[1]/256,grip[2]/256},targetVisualLengthRelativeToPlayer=relative,clips={effect={fps=0,loop=false,files={f}}},files={f},sourceReferences={srcroot..name..'.png'},checks={structural='pending',visual='pending',unity='not_run'},deviations={},anchorsByFrame={[f]=anchors},provenance={method='existing icon sampled and oriented in native Aseprite; original unchanged',tool='Aseprite 1.3.18.6 Lua'}}
 local fo=assert(io.open(dir..'/manifest.json','w'));fo:write(json.encode(m));fo:close()
end
