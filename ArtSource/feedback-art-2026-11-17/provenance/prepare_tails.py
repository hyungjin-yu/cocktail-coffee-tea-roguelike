from pathlib import Path
r=Path('ArtSource/feedback-art-2026-11-17');s=(r/'provenance/draft-hidden.lua').read_text(encoding='utf-8').split('-- Native preview fitting only.')[0]
s+='''
local function inside(x,y,poly)
 local c=false local j=#poly
 for i=1,#poly do local a,b=poly[i],poly[j] if ((a[2]>y)~=(b[2]>y)) and x<(b[1]-a[1])*(y-a[2])/(b[2]-a[2])+a[1] then c=not c end j=i end return c
end
local polys={
fire={{{30,8},{21,6},{15,5},{3,2},{8,6},{2,8},{9,10},{4,13},{14,12},{21,10},{30,9}}},
bitter={{{30,8},{22,6},{15,3},{8,2},{3,4},{5,7},{10,8},{18,8},{24,9},{30,9}},{{30,8},{22,8},{15,8},{9,9},{4,12},{5,14},{11,13},{17,11},{24,10},{30,9}}},
dual={{{30,8},{22,6},{18,4},{14,2},{12,4},{7,2},{8,5},{3,4},{8,7},{19,8},{30,9}},{{30,8},{22,9},{15,10},{9,10},{4,12},{5,14},{12,13},{19,11},{30,9}}}}
local dark=rgba(32,29,41) local ivory=rgba(244,241,231)
local all={}
for _,state in ipairs({"fire","bitter","dual"}) do
 local id="vfx_arrow_"..state.."_tail" local raw=assert(load(id))
 local source=app.open(root.."raw/"..id..".png") source.layers[1].name="Unmodified actual official PixelLab output" source:saveAs(root.."raw/"..id..".aseprite") source:close()
 local sp=Sprite(32,16,ColorMode.RGB) sp.layers[1].name="Native 3-frame contour and generated material finish"
 for frame=1,3 do
  if frame>1 then sp:newEmptyFrame() end
  local mask={} local shift=frame==2 and -1 or frame==3 and 1 or 0
  for y=0,15 do for x=0,31 do
   local sy=y-(x<15 and shift or 0) local active=false
   for _,poly in ipairs(polys[state]) do if inside(x+.5,sy+.5,poly) then active=true end end
   mask[y*32+x]=active
  end end
  -- Constant socket region remains identical across every frame.
  for x=25,30 do mask[8*32+x]=true end
  local im=Image(32,16,ColorMode.RGB)
  for y=1,14 do for x=1,30 do if mask[y*32+x] then
   local boundary=not mask[y*32+x-1] or not mask[y*32+x+1] or not mask[(y-1)*32+x] or not mask[(y+1)*32+x]
   local nextEdge=not mask[y*32+x-2] or not mask[y*32+x+2] or not mask[(y-2)*32+x] or not mask[(y+2)*32+x]
   local c
   if boundary then c=dark else
    local sx=math.min(raw.width-1,math.floor(x*raw.width/32)) local sy=math.min(raw.height-1,math.floor(y*raw.height/16))
    local rc=raw:getPixel(sx,sy) local lum=(pc.rgbaR(rc)+pc.rgbaG(rc)+pc.rgbaB(rc))/3
    local leaf=state=="bitter" or (state=="dual" and y>=9)
    if leaf then c=lum>160 and rgba(184,203,151) or rgba(95,131,78) else c=lum>160 and rgba(242,192,105) or rgba(200,102,54) end
   end
   im:drawPixel(x,y,c)
  end end end
  for x=22,29 do im:drawPixel(x,8,ivory) end
  if state=="fire" then for x=9,21 do im:drawPixel(x,8,ivory) end elseif state=="dual" then for x=9,18 do im:drawPixel(x,11,ivory) end for x=12,20 do im:drawPixel(x,6,ivory) end end
  if state=="bitter" then for x=8,18 do local yy=x<13 and 5 or 6 if frame==2 then yy=yy-1 elseif frame==3 then yy=yy+1 end im:drawPixel(x,yy,ivory) end end
  sp:newCel(sp.layers[1],frame,im,Point(0,0)) sp.frames[frame].duration=.125
  local export=Sprite(32,16,ColorMode.RGB) export.layers[1].name="Delivery frame" export:newCel(export.layers[1],1,im,Point(0,0)) local sl=export:newSlice(Rectangle(0,0,32,16)) sl.name="delivery_pivot" sl.pivot=Point(30,8)
  local fid=id..string.format("_%02d",frame-1)
  export:saveAs(root.."ready/"..fid..".aseprite") export:saveCopyAs(root.."ready/"..fid..".png") export:close()
  all[#all+1]={assetId=fid,state=state,frame=frame-1,sourceAssetId=id,width=32,height=16,finish="Official generated flame/leaf material retained as tone sampling; native contour correction defines three flame lobes / two leaf wisps / separated hybrid fork. Three actual native frames with terminal flicker and fixed socket; dark outline plus ivory contrast edge."}
 end
 local ref=sp:newLayer() ref.name="Actual generated source (hidden)" ref.isVisible=false sp:newCel(ref,1,raw,Point(0,0))
 local sl=sp:newSlice(Rectangle(0,0,32,16)) sl.name="delivery_pivot" sl.pivot=Point(30,8)
 sp:saveAs(root.."ready/"..id..".aseprite") sp:close()
end
local f=assert(io.open(root.."provenance/native-finish-tails.json","w")) f:write(json.encode(all)) f:close()
'''
(r/'provenance/finish-tails.lua').write_text(s,encoding='utf-8')
import subprocess
p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/finish-tails.lua')],capture_output=True,text=True,timeout=45);(r/'verification/native-finish-tails.log').write_text(p.stdout+p.stderr,encoding='utf-8');print('Native tails',p.returncode,p.stdout[-150:]);assert p.returncode==0
from PIL import Image,ImageDraw,ImageOps
sheet=Image.new('RGB',(840,420),'#302b34');d=ImageDraw.Draw(sheet)
for i,state in enumerate(['fire','bitter','dual']):
 d.text((i*280+5,8),state+' / 8fps / 3 native frames',fill='white')
 for fr in range(3):
  im=Image.open(r/f'ready/vfx_arrow_{state}_tail_{fr:02d}.png').convert('RGBA');base=Image.new('RGBA',im.size,'#47634b');base.alpha_composite(im);sheet.paste(base.resize((256,128),Image.Resampling.NEAREST).convert('RGB'),(i*280+8,30+fr*128))
sheet.save(r/'previews/tail-frames-contact.png')
