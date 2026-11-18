from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,re,hashlib
r=Path('ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end');code=Path('Assets/_Project/Scripts/Core/GameInfo.cs').read_text(encoding='utf-8-sig');title=re.search(r'const string Title\s*=\s*"([^"]+)"',code).group(1);d=json.loads((r/'layout.json').read_text(encoding='utf-8'));assert d['exterior']['sign']['actualTitle']==title
im=Image.open(r/'previews/entry-door-proof-00.png').convert('RGB').resize((1920,1080),Image.Resampling.NEAREST);dr=ImageDraw.Draw(im)
for text,size,cx,cy in [(title,40,960,96),('입장하기',28,560,490),('아무 키나 입력하세요',22,560,557)]:
 f=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',size);b=dr.textbbox((0,0),text,font=f);dr.text((cx-(b[2]-b[0])/2,cy-(b[3]-b[1])/2-b[1]),text,font=f,fill='#F4E6CF',stroke_width=1,stroke_fill='#3B291F')
for w,h in [(1920,1080),(1280,720),(1440,1080)]:
 scale=min(w/1920,h/1080);p=im.resize((round(1920*scale),round(1080*scale)),Image.Resampling.NEAREST);out=Image.new('RGB',(w,h),'#201d29');out.paste(p,((w-p.width)//2,(h-p.height)//2));out.save(r/f'previews/exterior-sign-prompt-{w}x{h}.png')
proof=Image.open(r/'previews/hotspots-labels-proof.png').convert('RGB')
for w,h in [(1920,1080),(1280,720),(1440,1080)]:
 scale=min(w/1440,h/810);p=proof.resize((round(1440*scale),round(810*scale)),Image.Resampling.NEAREST);out=Image.new('RGB',(w,h),'#201d29');out.paste(p,((w-p.width)//2,(h-p.height)//2));out.save(r/f'previews/menu-contain-{w}x{h}.png')
files=[r/f'previews/entry-door-proof-{i:02}.png' for i in range(8)]+[r/f'previews/entry-threshold-proof-{i:02}.png' for i in range(8)]+[r/'previews/interior-composite.png'];sheet=Image.new('RGB',(1920,1470),'#201d29');dr=ImageDraw.Draw(sheet)
for i,p in enumerate(files):
 x=(i%4)*480;y=(i//4)*294;sheet.paste(Image.open(p).convert('RGB'),(x,y+24));dr.text((x+7,y+6),p.stem+(' / EXPLICIT SOUTH CUT' if i==16 else ''),fill='white')
sheet.save(r/'previews/entry-continuity-all17.png')
ref=json.loads((r/'reference/reuse-inventory.json').read_text(encoding='utf-8'));assert all(hashlib.sha256(Path(x['original']).read_bytes()).hexdigest()==x['sha256'] for x in ref)
base=Path('ArtSource/cafe-title-2026-11-18');before=json.loads((r/'previous-v3-before.json').read_text(encoding='utf-8'));changed=[n for n,sha in before.items() if hashlib.sha256((base/n).read_bytes()).hexdigest()!=sha];assert not changed
(r/'verification/v3-preservation.json').write_text(json.dumps({'pass':True,'filesChecked':len(before),'changed':changed,'referenceSourcesChecked':len(ref)},indent=2),encoding='utf-8');print('v4 previews current; exactGameInfo.Title',title.encode('unicode_escape').decode(),'v3 original277 and8reference sources preserved')
