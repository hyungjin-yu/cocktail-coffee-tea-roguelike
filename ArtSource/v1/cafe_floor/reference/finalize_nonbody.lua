-- Native Aseprite finalization; generated originals and existing icons remain intact.
local root=app.params.root or 'ArtProduction/first-pass-ui'
local unity='C:/Users/user/Documents/Projects/CocktailCoffeeTea'
local pc=app.pixelColor
local function color(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local palette={}
for _,h in ipairs({'201D29','302028','3B3035','44373B','4A4050','503C32','604637','714732','695346','705A49','79624E','8B6950','A1805B','B88E65','DCAE72','EAD4A5','F4F1E7','384539','466B45','677951','53546F','A7B1C2','49C9F2'}) do palette[#palette+1]=color(h) end
local cache={}
local function quant(c)
 if pc.rgbaA(c)<128 then return 0 end
 if cache[c] then return cache[c] end
 local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c);local best,d=palette[1],math.huge
 for _,p in ipairs(palette) do local q=(r-pc.rgbaR(p))^2+(g-pc.rgbaG(p))^2+(b-pc.rgbaB(p))^2;if q<d then d=q;best=p end end
 cache[c]=best;return best
end
local function rect(im,x,y,w,h,c) for yy=y,y+h-1 do for xx=x,x+w-1 do if xx>=0 and yy>=0 and xx<im.width and yy<im.height then im:drawPixel(xx,yy,c) end end end end
local function bounds(im)
 local x1,y1,x2,y2=im.width,im.height,-1,-1
 for it in im:pixels() do if pc.rgbaA(it())>127 then x1=math.min(x1,it.x);y1=math.min(y1,it.y);x2=math.max(x2,it.x);y2=math.max(y2,it.y) end end
 return {x1,y1,x2,y2}
end
local function source(path) local s=assert(app.open(path));local im=s.cels[1].image:clone();s:close();return im end
local function fit(src,w,h,crop,doQuant)
 local b=crop and bounds(src) or {0,0,src.width-1,src.height-1}
 local sw,sh=b[3]-b[1]+1,b[4]-b[2]+1
 local out=Image(w,h,ColorMode.RGB)
 local scale=math.min((w-8)/sw,(h-8)/sh);if not crop then scale=math.min(w/sw,h/sh) end
 local tw,th=math.floor(sw*scale/2)*2,math.floor(sh*scale/2)*2
 local ox,oy=math.floor((w-tw)/4)*2,math.floor((h-th)/4)*2
 for y=0,th-1,2 do for x=0,tw-1,2 do
  local c=src:getPixel(math.min(b[3],b[1]+math.floor(x/tw*sw)),math.min(b[4],b[2]+math.floor(y/th*sh)))
  if doQuant then c=quant(c) elseif pc.rgbaA(c)<128 then c=0 else c=pc.rgba(pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c),255) end
  rect(out,ox+x,oy+y,2,2,c)
 end end
 return out
end
local entries={}
local function save(id,im,kind,ref,extra)
 local dir=root..'/'..id;app.fs.makeAllDirectories(dir..'/frames');app.fs.makeAllDirectories(dir..'/reference')
 local f='frames/'..id..'_effect_right_00.png';im:saveAs(dir..'/'..f)
 local s=Sprite(im.width,im.height,ColorMode.RGB);s.layers[1].name=id;s.cels[1].image=im;s:saveAs(dir..'/'..id..'.aseprite');s:close();im:saveAs(dir..'/preview.png')
 local m={specVersion='1.0',assetId=id,kind=kind,status='needs_fix',scope='full_game_v1',frameSizePx={im.width,im.height},pixelsPerUnit=128,pivotNormalized={.5,.5},clips={effect={fps=0,loop=false,files={f}}},files={f},sourceReferences={ref},checks={structural='pending',visual='pending',unity='not_run'},deviations={},provenance={tool='Aseprite 1.3.18.6 Lua',method='native pixel finalization'}}
 if extra then for k,v in pairs(extra) do m[k]=v end end
 local jsonText=json.encode(m);jsonText=jsonText:sub(1,-2)..',"anchorsByFrame":'..(extra and extra.anchorsJson or '{}')..'}'
 local fo=assert(io.open(dir..'/manifest.json','w'));fo:write(jsonText);fo:close()
 entries[#entries+1]={assetId=id,path=dir,files={f},bounds=bounds(im)}
end
local wall=fit(source(root..'/reference/cafe_wall-generated-original.png'),1024,512,false,true)
-- Replace the outer eight-pixel strip with a continuous quiet timber pillar.
for x=0,7 do for y=0,511 do local c=color(x<4 and '302028' or '44373B');wall:drawPixel(x,y,c);wall:drawPixel(1023-x,y,c) end end
save('cafe_wall',wall,'background',root..'/reference/cafe_wall-generated-original.png',{tileAxes={'x'}})
-- Existing floor candidate already has clean board topology; enrich using generated grain.
local floor=source('ArtProduction/first-pass-v1/backgrounds/cafe_floor/frames/cafe_floor_static_right_00.png')
local grain=fit(source(root..'/reference/cafe_floor-generated-original.png'),512,256,false,true)
for y=8,247,2 do for x=8,503,2 do
 if y%64>8 and y%64<56 and x%128>8 then
 local c=grain:getPixel(x,y);local rr=pc.rgbaR(c)
 if rr>114 then rect(floor,x,y,2,2,color('79624E')) elseif rr<83 then rect(floor,x,y,2,2,color('604C40')) end
 end
end end
-- Matching edge pixels in both directions, no thin black seam.
for y=0,255 do floor:drawPixel(511,y,floor:getPixel(0,y));floor:drawPixel(510,y,floor:getPixel(1,y)) end
for x=0,511 do floor:drawPixel(x,255,floor:getPixel(x,0));floor:drawPixel(x,254,floor:getPixel(x,1)) end
save('cafe_floor',floor,'background',root..'/reference/cafe_floor-generated-original.png',{tileAxes={'x','y'}})
local counter=fit(source(root..'/reference/cafe_counter-generated-original.png'),512,256,true,true)
save('cafe_counter',counter,'background',root..'/reference/cafe_counter-generated-original.png',{tileAxes={}})
local lamp=fit(source(root..'/reference/cafe_lamp-generated-original.png'),128,256,true,true)
save('cafe_lamp',lamp,'background',root..'/reference/cafe_lamp-generated-original.png',{tileAxes={}})
for _,id in ipairs({'ice_shard','water_drop'}) do
 local p=unity..'/Assets/_Project/Resources/Icons/Enemies/'..id..'.png'
 save(id,fit(source(p),128,128,true,false),'vfx',p)
end
local fo=assert(io.open(root..'/new-assets.json','w'));fo:write(json.encode(entries));fo:close()
print('Native finalization complete: '..#entries..' assets, pending visual verification')
