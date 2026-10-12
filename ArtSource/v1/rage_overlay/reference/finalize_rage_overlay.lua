-- Requires an actually finalized customer frame and crown bounds; never substitutes a body.
local root='ArtProduction/first-pass-ui'
local bodyPath=assert(app.params.body,'body path required')
local crown={};for n in string.gmatch(assert(app.params.crown,'crown xMin,yMin,xMax,yMax required'),'[%d.]+') do crown[#crown+1]=tonumber(n) end
assert(#crown==4,'four crown bounds required')
local s=assert(app.open(bodyPath));local body=s.cels[1].image:clone();s:close();assert(body.width==512 and body.height==512,'customer must be finalized512')
local pc=app.pixelColor;local overlay=Image(512,512,ColorMode.RGB)
local function occupied(x,y) return x>=0 and y>=0 and x<256 and y<256 and pc.rgbaA(body:getPixel(x*2,y*2))>=128 end
local function block(x,y,c) if x>=0 and y>=0 and x<256 and y<256 and not occupied(x,y) then for dy=0,1 do for dx=0,1 do overlay:drawPixel(x*2+dx,y*2+dy,c) end end end end
for y=0,255 do for x=0,255 do if not occupied(x,y) then
 local distance=100
 for dy=-3,3 do for dx=-3,3 do if occupied(x+dx,y+dy) then distance=math.min(distance,dx*dx+dy*dy) end end end
 if distance<=2 then block(x,y,pc.rgba(239,101,88,170)) elseif distance<=8 then block(x,y,pc.rgba(239,101,88,65)) end
end end end
-- One small bent hook outside the central crown tip; all body pixels remain clear.
local xmin,ymin,xmax=crown[1]/2,crown[2]/2,crown[3]/2
local function line(x1,y1,x2,y2,c)
 local n=math.max(math.abs(x2-x1),math.abs(y2-y1),1)
 for i=0,n do block(math.floor(x1+(x2-x1)*i/n),math.floor(y1+(y2-y1)*i/n),c) end
end
local tip={};for n in string.gmatch(app.params.tip or '', '[%d.]+') do tip[#tip+1]=tonumber(n) end
for k=0,0 do local x=tip[1] and math.floor(tip[1]/2) or math.floor((xmin+xmax)/2);local y=tip[2] and math.floor(tip[2]/2) or math.floor(ymin)
 line(x-3,y-1,x+1,y+1,pc.rgba(239,101,88,225));line(x+1,y+1,x-1,y+3,pc.rgba(255,226,172,190))
end
local dir=root..'/rage_overlay';app.fs.makeAllDirectories(dir..'/frames');app.fs.makeAllDirectories(dir..'/reference')
local frame='frames/rage_overlay_effect_right_00.png';overlay:saveAs(dir..'/'..frame);overlay:saveAs(dir..'/preview.png')
local out=Sprite(512,512,ColorMode.RGB);out.layers[1].name='outer aura and folded crown accents';out.cels[1].image=overlay;out:saveAs(dir..'/rage_overlay.aseprite');out:close()
local report={bodyPath=bodyPath,crownBoundsTopLeftPx=crown,bodyOverlapPixels=0,canvas={512,512},pivot={.5,.125}}
for y=0,511 do for x=0,511 do if pc.rgbaA(body:getPixel(x,y))>=128 and pc.rgbaA(overlay:getPixel(x,y))>0 then report.bodyOverlapPixels=report.bodyOverlapPixels+1 end end end
assert(report.bodyOverlapPixels==0,'overlay must not obscure body')
local comparison=Image(1536,512,ColorMode.RGB)
for y=0,511 do for x=0,1535 do comparison:drawPixel(x,y,pc.rgba(59,48,53,255)) end end
comparison:drawImage(body,Point(0,0));comparison:drawImage(overlay,Point(512,0));comparison:drawImage(body,Point(1024,0));comparison:drawImage(overlay,Point(1024,0));comparison:saveAs(root..'/review/rage-alignment.png')
local m={specVersion='1.0',assetId='rage_overlay',kind='vfx',status='needs_fix',scope='full_game_v1',frameSizePx={512,512},pixelsPerUnit=128,pivotNormalized={.5,.125},clips={effect={fps=0,loop=false,files={frame}}},files={frame},sourceReferences={bodyPath,root..'/finalize_rage_overlay.lua'},deviations={},checks={structural='pending',visual='pending',unity='not_run'},alignment=report,provenance={method='native pixel aura traced outside finalized body alpha; crown-tip fold accents',tool='Aseprite 1.3.18.6 Lua'}}
local j=json.encode(m);j=j:sub(1,-2)..',"anchorsByFrame":{}}';local f=assert(io.open(dir..'/manifest.json','w'));f:write(j);f:close()
local rf=assert(io.open(root..'/review/rage-overlap-check.json','w'));rf:write(json.encode(report));rf:close()
print('Rage candidate produced with zero body overlap; visual alignment review required')
