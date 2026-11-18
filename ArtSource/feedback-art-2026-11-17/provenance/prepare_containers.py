from pathlib import Path
import json
r=Path('ArtSource/feedback-art-2026-11-17');specs=json.loads((r/'provenance/recovery-specs29.json').read_text(encoding='utf-8'))[:28]
# Distinct structural game identifiers follow the corresponding approved origin icon motifs.
patterns=[
['#.....#','.#...#.','..#.#..','...#...','...#...','...#...','...#...'],
['...#...','..#.#..','.#...#.','#..#..#','..#.#..','.#...#.','#.....#'],
['#....#.','.#..#..','..##...','..##...','.#..#..','#....#.','.......'],
['.....##','...###.','..###..','.###...','.##....','..#....','.#.....'],
['..##...','..##...','##..##.','##..##.','..##...','..##...','.......'],
['#..#..#','##.#.##','#######','.#####.','..###..','..###..','.......'],
['##...##','###.###','.#####.','..###..','...#...','...#...','...#...'],
['#.#.#.#','.#####.','#######','.#####.','#######','.#####.','.#.#.#.'],
['...#...','..#.#..','.#...#.','#.....#','.#...#.','..#.#..','...#...'],
['..####.','.##....','##.....','##.....','##.....','.##....','..####.'],
['#######','#.....#','#.###.#','#.#.#.#','#.###.#','#.....#','#######'],
['#.....#','#.....#','.#...#.','.#...#.','..#.#..','..#.#..','...#...'],
['.##..##','#..##..','.......','.##..##','#..##..','.......','.......'],
['...#...','..#.#..','..#.#..','.#...#.','.#...#.','#.....#','#######'],
['.#...#.','###.###','.#...#.','###.###','.#...#.','.#...#.','...#...'],
['.##.##.','#.##.##','#.##.##','.##.##.','.......','..###..','..#.#..'],
['..##...','..##...','.####..','.####..','######.','######.','.......'],
['#..#..#','.#####.','.#####.','#######','.#####.','.#####.','#..#..#'],
['..###..','..###..','###.###','###.###','..###..','...#...','...#...'],
['#######','##...##','#.#.#.#','#..#..#','#.#.#.#','##...##','#######'],
['#..#..#','.#.#.#.','..###..','...#...','...#...','...#...','...#...'],
['.....##','....##.','...##..','..##...','.##....','##.....','.......'],
['.#####.','#.....#','#.....#','#.....#','##...##','.#####.','.......'],
['.#####.','#.....#','#.###.#','#.#...#','#.#####','#......','.######'],
['#.....#','.#...#.','..#.#..','...#...','..#.#..','.#...#.','#.....#'],
['..###..','.#...#.','#.....#','#.....#','#.....#','#.....#','#.....#'],
['..#.#..','.#...#.','#.....#','..#.#..','.#...#.','#.....#','.......'],
['#######','.......','.#...#.','.#...#.','.#...#.','.......','#######']]
assert len(patterns)==28 and len({tuple(p) for p in patterns})==28
crop=[.10,.12,0,0,.23,.20,.20,0,.12,0,.20,.12,.20,.20,0,0,0,.12,0,0,0,0,0,0,.17,0,.12,0]
rows=[]
for i,sp in enumerate(specs):rows.append('{id='+json.dumps(sp['id'])+',index='+str(i)+',crop='+str(crop[i])+',pattern={'+','.join(json.dumps(x) for x in patterns[i])+'}}')
s=(r/'provenance/draft-hidden.lua').read_text(encoding='utf-8').split('-- Native preview fitting only.')[0];s=s.replace('if id:find("^dl_coffee_") then','if id:find("^dl_coffee_") or id:find("^dl_tin_") then')
s+='local specs={'+','.join(rows)+'}\n'
s+='''
local dark=rgba(32,29,41) local ivory=rgba(244,241,231) local silver=rgba(167,177,194) local bean=rgba(113,71,50) local warm=rgba(220,174,114)
local function dot(im,x,y,c) if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end end
for _,spec in ipairs(specs) do
 local id=spec.id local raw=load(id)
 if raw then
  local b=bbox(raw) b[2]=b[2]+math.floor((b[4]-b[2])*spec.crop)
  local bw,bh=b[3]-b[1],b[4]-b[2]
  local scale=math.min(26/bw,28/bh)
  local nw,nh=math.max(18,math.floor(bw*scale+.5)),math.max(18,math.floor(bh*scale+.5))
  local im=Image(32,32,ColorMode.RGB) local dx,dy=math.floor((32-nw)/2),32-nh
  for y=0,nh-1 do for x=0,nw-1 do
   local sx=b[1]+math.min(bw-1,math.floor(x*bw/nw)) local sy=b[2]+math.min(bh-1,math.floor(y*bh/nh))
   dot(im,dx+x,dy+y,remap(raw:getPixel(sx,sy)))
  end end
  -- Open rim at actual body top. Closed/generated caps were explicitly cropped above.
  local centerY=dy+3 local half=math.floor((nw-2)/2) local cx=15.5
  for y=0,centerY-2 do for x=0,31 do dot(im,x,y,clear) end end
  for oy=-2,2 do
   local width=oy==-2 or oy==2 and half-2 or half
   if oy==-2 or oy==2 then width=half-2 else width=half end
   for x=math.ceil(cx-width),math.floor(cx+width) do
    local edge=x==math.ceil(cx-width) or x==math.floor(cx+width) or math.abs(oy)==2
    dot(im,x,centerY+oy,edge and dark or (oy==-1 and ivory or oy==1 and silver or dark))
   end
  end
  -- Visible three-bean dark opening, kept above label, with a stable top-right pour lip.
  for _,ox in ipairs({-4,0,4}) do dot(im,math.floor(cx+ox),centerY,warm) dot(im,math.floor(cx+ox)+1,centerY,bean) end
  -- A small paper/metal label carries one unique high-contrast structural motif.
  local labelY=math.min(21,centerY+math.max(5,math.floor((31-centerY)*.42)))
  local labelX=12
  for yy=-1,7 do for xx=-1,7 do dot(im,labelX+xx,labelY+yy,(xx==-1 or xx==7 or yy==-1 or yy==7) and dark or ivory) end end
  for yy,row in ipairs(spec.pattern) do for xx=1,#row do if row:sub(xx,xx)=="#" then dot(im,labelX+xx-1,labelY+yy-1,dark) end end end
  -- Ensure opening is supported by material walls and not a detached overlay.
  for x=math.floor(cx-half)+1,math.ceil(cx+half)-1 do if pc.rgbaA(im:getPixel(x,centerY+3))==0 then dot(im,x,centerY+3,rgba(184,142,101)) end end
  save(id,im,raw,"Actual official PixelLab container body/material retained; native closed-cap crop where needed, minimum readable body-width correction, open dark ellipse with bean pixels and ivory/silver rim. Unique structural origin motif follows approved inventory concept; no color-only distinction. Opening determines per-container mouth/pourLip and body grip.")
  anchors.entries[#anchors.entries+1]={assetId=id,width=32,height=32,mouth={(cx+.5)/32,1-(centerY+.5)/32},pourLip={(math.floor(cx+half)+.5)/32,1-(centerY+.5)/32},grip={.5,(32-(centerY+3+(31-centerY-3)*.60))/32},pivot={.5,0},mouthPixelCenterTopLeft={cx+.5,centerY+.5},pourLipPixelCenterTopLeft={math.floor(cx+half)+.5,centerY+.5},openingBoundsTopLeft={math.ceil(cx-half),centerY-2,math.floor(cx+half)+1,centerY+3},pixelOrigin="normalized bottom-left; opening dimensions use top-left PNG pixels"}
  local rs=app.open(root.."raw/"..id..".png") rs.layers[1].name="Unmodified actual official PixelLab output" rs:saveAs(root.."raw/"..id..".aseprite") rs:close()
 end
end
local f=assert(io.open(root.."provenance/native-finish-containers.json","w")) f:write(json.encode(records)) f:close()
f=assert(io.open(root.."ready/anchors-containers.json","w")) f:write(json.encode(anchors)) f:close()
'''
# Lua precedence expression is replaced by explicit width assignment below; keep native code simple.
s=s.replace('local width=oy==-2 or oy==2 and half-2 or half','local width=half')
(r/'provenance/finish-containers.lua').write_text(s,encoding='utf-8')
import subprocess
p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/finish-containers.lua')],capture_output=True,text=True,timeout=60);(r/'verification/native-finish-containers.log').write_text(p.stdout+p.stderr,encoding='utf-8');assert p.returncode==0;print('Finished available containers',len(json.loads((r/'provenance/native-finish-containers.json').read_text(encoding='utf-8'))))
from PIL import Image,ImageDraw,ImageOps
for gray in [False,True]:
 sheet=Image.new('RGB',(7*170,4*205),'#302b34');d=ImageDraw.Draw(sheet)
 for i,sp in enumerate(specs):
  p=r/f'ready/{sp["id"]}.png'
  if not p.exists():continue
  im=Image.open(p).convert('RGBA');big=im.resize((128,128),Image.Resampling.NEAREST);x=(i%7)*170;y=(i//7)*205
  if gray:big=ImageOps.grayscale(big).convert('RGBA');big.putalpha(im.getchannel('A').resize((128,128),Image.Resampling.NEAREST))
  sheet.paste(big,(x+18,y),big);d.text((x+2,y+132),sp['id'].replace('dl_tin_','')[:24],fill='white')
  for j,bg in enumerate(['#ece4d4','#46634b','#201d29']):
   b=Image.new('RGBA',(34,34),bg);b.alpha_composite(im,(1,1))
   if gray:b=ImageOps.grayscale(b).convert('RGB')
   sheet.paste(b.convert('RGB'),(x+4+j*40,y+154))
 sheet.save(r/('previews/containers-gray.png' if gray else 'previews/containers-color.png'))
