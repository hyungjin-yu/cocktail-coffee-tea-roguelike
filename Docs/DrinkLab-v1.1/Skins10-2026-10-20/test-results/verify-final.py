import csv,hashlib,os
from PIL import Image
art='ArtSource/skins-aseprite-pixellab-2026-10-20/'
res='Assets/_Project/Resources/CosmeticSkins/'
rows=list(csv.DictReader(open(art+'final-source-sha256.csv',encoding='utf-8-sig')))
want={}
for r in rows: want.setdefault(r['bundle'],{})[r['filename']]=r['PNG_SHA256'].lower()
print('csv bundles', {k:len(v) for k,v in want.items()})
tot=bad=0
for sid,files in sorted(want.items()):
    if not sid.startswith('skin_'): continue
    have=sorted(f for f in os.listdir(res+sid) if f.endswith('.png'))
    extra=set(have)^set(files)
    for fn,h in files.items():
        tot+=1
        if hashlib.sha256(open(res+sid+'/'+fn,'rb').read()).hexdigest()!=h: bad+=1; print('BAD',sid,fn)
    n='bartender' if 'bartender' in sid else 'tea_sommelier'
    bb=Image.open(f'{res}{sid}/{n}_idle_right_00.png').getbbox()
    print(sid,len(have),'setdiff',extra or '-','idle bbox',bb,'OUTSIDE' if bb[0]<74 or bb[1]<26 or bb[2]>182 or bb[3]>230 else 'fits')
print('total',tot,'bad',bad)
