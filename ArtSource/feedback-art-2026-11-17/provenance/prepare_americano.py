from pathlib import Path
r=Path('ArtSource/feedback-art-2026-11-17');s=(r/'provenance/draft-hidden.lua').read_text(encoding='utf-8');s=s.split('-- Native preview fitting only.')[0]
s+='''
local original=Image{fromFile=root.."reference/dl_coffee_mug.png"}
local generated=load("dl_coffee_americano_mug") assert(generated,"Actual PixelLab edit must exist")
local mask=Image{fromFile=root.."reference/dl_coffee_mug_mask.png"}
local mug=Image(original)
for y=0,35 do for x=0,35 do if pc.rgbaA(mask:getPixel(x,y))>127 then mug:drawPixel(x,y,clear) end end end
save("dl_coffee_americano_mug",mug,generated,"Actual official PixelLab edit reviewed; native correction restores approved original rectangular silhouette, rim, base and right handle exactly. Clear the existing liquid-mask interior to alpha0 so renderer liquid remains visible. No new vessel design.")
save("dl_coffee_americano_mug_mask",mask,nil,"Native technical derivative: preserved original approved mug mask; white opaque / transparent, identical 36x36 registration.")
anchors.entries[#anchors.entries+1]={assetId="dl_coffee_americano_mug",width=36,height=36,pourLip={1.5/36,32.5/36},mouth={14.5/36,32.5/36},grip={31.5/36,16.5/36},liquid={3/36,2/36,27/36,32/36},pivot={.5,0},pixelOrigin="normalized bottom-left; liquid is MinMaxRect matching mask bbox"}
local function write(path,data) local f=assert(io.open(root..path,"w")) f:write(json.encode(data)) f:close() end
write("provenance/native-finish-americano.json",records)
write("ready/anchors-americano.json",anchors)
-- Preserve exact generated source in an independently reopenable native archive.
local raw=app.open(root.."raw/dl_coffee_americano_mug.png") raw.layers[1].name="Unmodified official PixelLab edit" raw:saveAs(root.."raw/dl_coffee_americano_mug.aseprite") raw:close()
'''
(r/'provenance/finish-americano.lua').write_text(s,encoding='utf-8')
import subprocess
p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/finish-americano.lua')],capture_output=True,text=True,timeout=45);(r/'verification/native-finish-americano.log').write_text(p.stdout+p.stderr,encoding='utf-8');print('Native finish',p.returncode)
assert p.returncode==0
