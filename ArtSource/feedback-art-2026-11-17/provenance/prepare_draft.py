from pathlib import Path
r=Path('ArtSource/feedback-art-2026-11-17');old=Path('ArtSource/feedback-art-2026-11-16/provenance/prepare_finish.py').read_text(encoding='utf-8');lua=old.split("lua=r'''",1)[1].split('local raw=load("dl_coffee_latte_glass")',1)[0].replace('feedback-art-2026-11-16/','feedback-art-2026-11-17/')
lua+='''
-- Native preview fitting only. These draft files are not published ready.
local ids={"hidden_iced_cup","hidden_soda_pump","hidden_rhythm_shaker","hidden_bitter_wake","hidden_weekend_rush_steps","hidden_herb_garden","hidden_thick_mug","hidden_bar_tempo","hidden_bar_tempo_move"}
for _,id in ipairs(ids) do local raw=load(id) if raw then local im=fit(raw,bbox(raw),24,24,22,22,false) local sp=Sprite(24,24,ColorMode.RGB) sp:newCel(sp.layers[1],1,im) sp:saveAs(root.."previews/"..id.."-draft.png") sp:close() end end
'''
(r/'provenance/draft-hidden.lua').write_text(lua,encoding='utf-8')
import subprocess
p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/draft-hidden.lua')],capture_output=True,text=True,timeout=45);print(p.returncode,p.stdout[-500:],p.stderr[-500:])
from PIL import Image,ImageDraw
sheet=Image.new('RGB',(9*144,190),'#303039');d=ImageDraw.Draw(sheet)
for i,id in enumerate(['hidden_iced_cup','hidden_soda_pump','hidden_rhythm_shaker','hidden_bitter_wake','hidden_weekend_rush_steps','hidden_herb_garden','hidden_thick_mug','hidden_bar_tempo','hidden_bar_tempo_move']):
 im=Image.open(r/'previews'/f'{id}-draft.png').convert('RGBA');sheet.paste(im.resize((144,144),Image.Resampling.NEAREST),(i*144,0),im.resize((144,144),Image.Resampling.NEAREST));d.text((i*144+4,148),id.replace('hidden_',''),fill='white')
sheet.save(r/'previews/hidden-draft-contact.png')
