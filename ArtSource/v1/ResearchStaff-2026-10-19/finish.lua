local root=app.params.root;local pc=app.pixelColor
local palette={{32,29,41},{74,64,80},{70,48,45},{249,220,183},{218,163,117},{164,108,83},{199,168,121},{153,116,80},{164,104,74},{204,145,98},{118,69,47},{157,100,72},{72,42,40},{244,241,231},{234,212,165},{184,142,101},{167,177,194},{83,84,111},{87,47,76},{139,79,110},{192,133,154},{166,77,36},{216,124,53},{246,171,85},{34,53,75},{63,86,116},{70,107,69},{103,145,87},{142,173,104},{220,174,114},{119,83,51},{31,35,44}}
local function clean(im)
 for p in im:pixels() do local c=p();if pc.rgbaA(c)<128 then p(0) else
  local idx,dist=1,1e9;for i,v in ipairs(palette) do local d=(pc.rgbaR(c)-v[1])^2+(pc.rgbaG(c)-v[2])^2+(pc.rgbaB(c)-v[3])^2;if d<dist then idx,dist=i,d end end
  local v=palette[idx];p(pc.rgba(v[1],v[2],v[3],255)) end
 end
end
for _,id in ipairs({'cocktail_aroma','cocktail_blend','tea_aroma','tea_blend'}) do
 local path=root..'/provenance/'..id..'.raw.png'
 if app.fs.isFile(path) then
  local raw=Image{fromFile=path};local src=Image(raw);clean(src)
  local l,t,r,b=src.width,src.height,-1,-1
  for p in src:pixels() do if pc.rgbaA(p())>0 then l=math.min(l,p.x);r=math.max(r,p.x);t=math.min(t,p.y);b=math.max(b,p.y) end end
  assert(r>=l,'empty raw portrait');local cropped=Image(r-l+1,b-t+1,ColorMode.RGB);cropped:drawImage(src,Point(-l,-t));local scale=math.min(26/cropped.width,32/cropped.height)
  cropped:resize{width=math.floor(cropped.width*scale+.5),height=math.floor(cropped.height*scale+.5),method='nearest'}
  local im=Image(28,34,ColorMode.RGB);im:drawImage(cropped,Point((28-cropped.width)//2,33-cropped.height))
  local s=Sprite(28,34,ColorMode.RGB);s.layers[1].name='Finished staff portrait - pixel palette';s:newCel(s.layers[1],1,im,Point(0,0))
  s:saveAs(root..'/staff_'..id..'.aseprite');im:saveAs(root..'/staff_'..id..'.png');s:close()
  local loaded=app.open(root..'/staff_'..id..'.aseprite');local rt=Image(28,34,ColorMode.RGB);rt:drawSprite(loaded,1);rt:saveAs(root..'/qa/staff_'..id..'_roundtrip.png');loaded:close()
  local frame=Image{fromFile=app.params.frame};frame:drawImage(im,Point(18,27));frame:saveAs(root..'/qa/staff_'..id..'_frame.png')
  print(id..' finished')
 end
end
