from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/prepare_finish.py');s=p.read_text(encoding='utf-8')
a='for y=0,7 do local edge=im:getPixel(1,y) if pc.rgbaA(edge)==0 then edge=im:getPixel(30,y) end im:drawPixel(0,y,edge) im:drawPixel(31,y,edge) end'
b='''for y=3,5 do for x=0,31 do if pc.rgbaA(im:getPixel(x,y))==0 then im:drawPixel(x,y,y==5 and rgba(218,201,169) or rgba(244,241,231)) end end end
 for y=0,7 do local edge=im:getPixel(1,y) if pc.rgbaA(edge)==0 then edge=im:getPixel(30,y) end if y>=3 and y<=5 then edge=y==5 and rgba(218,201,169) or rgba(244,241,231) end im:drawPixel(0,y,edge) im:drawPixel(31,y,edge) end'''
assert a in s;s=s.replace(a,b);p.write_text(s,encoding='utf-8')
p=Path('ArtSource/feedback-art-2026-11-16/provenance/validate_assets.py');s=p.read_text(encoding='utf-8');s=s.replace("'opaqueColorCount':len({p[:3] for p in foam.getdata() if p[3]})","'opaqueColorCount':len({p[:3] for p in foam.getdata() if p[3]}),'continuousFoamBand':all(foam.getpixel((x,y))[3]==255 for y in range(3,6) for x in range(32))");s=s.replace("foamcheck['opaqueColorCount']==3","foamcheck['opaqueColorCount']==3 and foamcheck['continuousFoamBand']");p.write_text(s,encoding='utf-8')
r=Path('ArtSource/feedback-art-2026-11-16');(r/'provenance/archive-raw-native.lua').write_text('''local root="C:/Users/user/orca/workspaces/CocktailCoffeeTea/coffee-brewing/ArtSource/feedback-art-2026-11-16/raw/"
local count=0
for _,f in ipairs(app.fs.listFiles(root)) do
 if f:match("%.png$") then
  local path=root..f:gsub("%.png$",".aseprite")
  if not app.fs.isFile(path) then local s=app.open(root..f) s.layers[1].name="Original PixelLab output" s:saveAs(path) s:close() end
  count=count+1
 end
end
print("RAW_NATIVE_ARCHIVE_COUNT "..count)
''',encoding='utf-8')
