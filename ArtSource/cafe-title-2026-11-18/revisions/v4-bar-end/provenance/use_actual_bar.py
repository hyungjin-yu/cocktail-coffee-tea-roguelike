from pathlib import Path
from PIL import Image
import json,hashlib,shutil,subprocess
r=Path('ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end');ledger=json.loads((r/'reference/reuse-inventory.json').read_text(encoding='utf-8'))
for part in ['floor','wall','counter','lamp']:
 src=Path(f'Assets/_Project/Resources/ArtExtra/bg_bar_{part}/bg_bar_{part}_00.png');dst=r/'reference'/src.name;shutil.copy2(src,dst);im=Image.open(src);ledger=[x for x in ledger if x['original']!=src.as_posix()];ledger.append({'original':src.as_posix(),'copy':dst.relative_to(r).as_posix(),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'dimensions':list(im.size),'use':'actual single-player cocktail bar source, PPU64/center pivot; new ID only, original preserved'})
(r/'reference/reuse-inventory.json').write_text(json.dumps(ledger,indent=2),encoding='utf-8')
s='local root='+json.dumps(r.resolve().as_posix()+'/')+'\nlocal pc=app.pixelColor\n'
for part in ['floor','wall','counter','lamp']:
 (r/f'ready/gameplay/bg_barwarm_{part}').mkdir(parents=True,exist_ok=True);s+='do local im=Image{fromFile=root.."reference/bg_bar_'+part+'_00.png"} for y=0,im.height-1 do for x=0,im.width-1 do local c=im:getPixel(x,y) if pc.rgbaA(c)>0 then local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c) local yy=(54*r+183*g+19*b)/256 local lift=32*(1-yy/255) im:drawPixel(x,y,pc.rgba(math.min(255,math.floor(r+lift*1.1+.5)),math.min(255,math.floor(g+lift*.86+.5)),math.min(255,math.floor(b+lift*.55+.5)),pc.rgbaA(c))) end end end local sp=Sprite(im.width,im.height,ColorMode.RGB) sp:newCel(sp.layers[1],1,im) local sl=sp:newSlice(Rectangle(0,0,im.width,im.height)) sl.name="delivery_pivot" sl.pivot=Point(im.width/2,im.height/2) sp:saveAs(root.."ready/gameplay/bg_barwarm_'+part+'/bg_barwarm_'+part+'_00.aseprite") sp:saveCopyAs(root.."ready/gameplay/bg_barwarm_'+part+'/bg_barwarm_'+part+'_00.png") sp:close() end\n'
(r/'provenance/grade-actual-bar.lua').write_text(s,encoding='utf-8');p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/grade-actual-bar.lua')],capture_output=True,text=True,timeout=45);assert p.returncode==0
p=r/'provenance/author-scene.lua';s=p.read_text(encoding='utf-8').replace("tex('floor','reference/cafe_floor_effect_right_00.png',{0,0,512,256})","tex('floor','reference/bg_bar_floor_00.png',{4,36,250,62})").replace("tex('counter','reference/cafe_counter_effect_right_00.png',{8,103,500,192})","tex('counter','reference/bg_bar_counter_00.png',{3,62,253,110})").replace("tex('woodtop','reference/cafe_counter_effect_right_00.png',{32,64,478,91})","tex('woodtop','reference/bg_bar_counter_00.png',{8,46,248,54})")
s=s.replace("local mats={","tex('barwalltrim','reference/bg_bar_wall_00.png',{37,0,220,18})\ntex('barlamp','reference/bg_bar_lamp_00.png',{0,0,55,103})\nlocal mats={")
s=s.replace("plaster={rgb=","barwalltrim={texture='barwalltrim'},barlamp={texture='barlamp',masked=true},plaster={rgb=",1)
s=s.replace("m.texture=='facade' then","m.texture=='facade' or m.texture=='barwalltrim' or m.texture=='barlamp' then")
s=s.replace("u=math.max(0,math.min(.999999,u)) v=math.max(0,math.min(.999999,v))","if mat=='floor' then u=(u*7.5/(246/64))%1 v=(v*6/(26/64))%1 end\n  u=math.max(0,math.min(.999999,u)) v=math.max(0,math.min(.999999,v))")
a=s.index(' -- One dominant light source;');b=s.index(' -- Window on north wall',a)
s=s[:a]+''' -- Shared actual bg_bar pendant design, one fixed world light source.
 northface(3.0,4.96,5.24,2.32,2.9,'barlamp',0)
 quad({{4.96,3.001,2.32},{5.24,3.001,2.32},{5.24,3.001,2.9},{4.96,3.001,2.9}},'barlamp',0,2)
 -- Existing wall cornice uses the actual bg_bar carved molding strip.
 quad({{0,5.99,2.72},{7.5,5.99,2.72},{7.5,5.99,2.9},{0,5.99,2.9}},'barwalltrim',0,2)
 quad({{.01,0,2.72},{.01,6,2.72},{.01,6,2.9},{.01,0,2.9}},'barwalltrim',0,2)
 quad({{7.49,6,2.72},{7.49,0,2.72},{7.49,0,2.9},{7.49,6,2.9}},'barwalltrim',0,2)
''' +s[b:]
p.write_text(s,encoding='utf-8');f=r/'provenance/finish-scene.lua';t=f.read_text(encoding='utf-8');f.write_text(s.split('-- First native composition is a review draft')[0]+t[t.index('local delivered={}'):],encoding='utf-8');print('Actual4 sources copied/grading native done; original refs8 preserve; title uses actual bar floor/counter/cornice/lamp material')
