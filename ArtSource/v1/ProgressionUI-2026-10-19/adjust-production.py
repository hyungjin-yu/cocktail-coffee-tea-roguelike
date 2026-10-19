import pathlib
r=pathlib.Path(__file__).resolve().parent;p=r/'produce.py';s=p.read_text(encoding='utf-8-sig');s=s.replace("style=Image.open(REF).convert('RGBA')","style=Image.open(REF).convert('RGBA'); style.thumbnail((min(w,64),min(h,64)),Image.Resampling.NEAREST)")
p.write_text(s,encoding='utf8')
p=r/'finish.lua';s=p.read_text(encoding='utf-8-sig');s=s.replace(' local closed=lidFrame(0,false)'," local stableOpen=lidFrame(1,true)\n save('laptop_open',stableOpen,original);finished.laptop_open=stableOpen\n local closed=lidFrame(0,false)")
s=s.replace("{'laptop_closed',384,256,0},",'')
p.write_text(s,encoding='utf8')
