from pathlib import Path
import json, hashlib, subprocess
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parent
ase=Path('C:/Program Files/Aseprite/Aseprite.exe')
ids=['absinthe','creme_cassis','limoncello','white_tea','oolong_tea','jasmine_tea','chamomile_flower','ethiopia_yirgacheffe_kochere','colombia_huila_vergel','brazil_mantiqueira_alves_lima']
refs=[
 {'url':'https://www.tea.co.uk/types-of-tea','supports':'White tea buds and leaves; oolong processing category. Shape differentiation is an art interpretation, not a universal cultivar description.'},
 {'url':'https://www.tea.co.uk/about/infusions','supports':'Jasmine scented tea versus chamomile floral infusion. The former uses five-petal pale stars; chamomile uses many white rays and raised yellow centers.'},
 {'url':'https://www.aboutswitzerland.eda.admin.ch/en/absinthe-swiss-local-product-leaves-behind-its-shady-past','supports':'Val-de-Travers provenance and wormwood-based spirit. Green selected for this game variant; not all absinthe is green.'},
 {'url':'https://www.inao.gouv.fr/produit/cassis-de-dijon-24568','supports':'Blackcurrant maceration and intense garnet to violet colour. Generic creme_cassis ID does not claim protected Dijon certification.'},
 {'url':'https://www.limoncello.com/en/our-roots/','supports':'Oval Sorrento lemons; lemon emblem and yellow liqueur. No producer branding reproduced.'},
 {'url':'https://egtethiopia.com/yirgacheffe-coffee/','supports':'Indexed EGT excerpt identifies Kochere in Yirgacheffe region; direct page returned 404 during this run. Mountain and pale floral badge are illustrative cues, not farm or tasting certification.','access':'Search index excerpt only; direct 404'},
 {'url':'https://www.kundecoffee.com/es/farm/fincaelvergel/','supports':'Finca El Vergel in Pitalito, Huila, Colombia, with Bourbon Rosado Natural and Bourbon Rayado Lavado listings. Pink/red coffee cherry motif references crop identity; not added fruit ingredients.'},
 {'url':'https://cafealveslima.com/pages/nossa-fazenda','supports':'Alves Lima family coffee farm provenance. Mountain/row motif is an illustrative agriculture cue, not a surveyed farm view.'},
 {'url':'https://sca.coffee/what-is-specialty-coffee','supports':'Specialty concerns distinctive attributes and value, not bean silhouette or a guaranteed taste. Shared bean shape deliberately retained across all origins; no grade, score or certification implied.'}
]
qa=root/'qa';qa.mkdir(exist_ok=True)
assets=[]
for f in sorted(root.glob('*.aseprite')):
 p=f.with_suffix('.png'); im=Image.open(p).convert('RGBA')
 rt=qa/(f.stem+'.png')
 r=subprocess.run([str(ase),'-b',str(f),'--save-as',str(rt)],capture_output=True,text=True)
 assert r.returncode==0 and rt.exists(),(f,r.stderr)
 same=im.tobytes()==Image.open(rt).convert('RGBA').tobytes()
 alpha=im.getchannel('A');binary=set(alpha.getdata())<={0,255}
 assert same and binary and alpha.getbbox(),f.name
 assets.append({'assetId':f.stem,'role':'master artwork' if f.stem in ids else 'runtime candidate','png':str(p),'aseprite':str(f),'dimensions':list(im.size),'alphaBounds':alpha.getbbox(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'layerCount':int.from_bytes(f.read_bytes()[0:0] or b'\x00','little'),'checks':{'nativeAsepriteRoundtrip':same,'binaryAlpha':binary,'nonempty':True},'import':{'textureType':'Sprite','spriteMode':'Single','pixelsPerUnit':128,'filter':'Point','compression':'None','mipmaps':False,'sRGB':True,'alphaIsTransparency':True,'pivot':[0.5,0.5]}})
 # Layer chunk count is parsed from the native file rather than assumed.
 import struct
 b=f.read_bytes();offset=128;layers=0
 while offset<len(b):
  size,magic,chunks=struct.unpack_from('<IHH',b,offset);pos=offset+16
  for _ in range(chunks):
   cs,ct=struct.unpack_from('<IH',b,pos)
   if ct==0x2004:layers+=1
   pos+=cs
  offset+=size
 assets[-1]['layerCount']=layers

# 1x means artwork is pasted at its native output size, never enlarged.
sheet=Image.new('RGB',(750,420),'#27232d');d=ImageDraw.Draw(sheet)
names=['ABSINTHE','CREME CASSIS','LIMONCELLO','WHITE TEA','OOLONG TEA','JASMINE TEA','CHAMOMILE','ETHIOPIA / KOCHERE','COLOMBIA / HUILA','BRAZIL / ALVES LIMA']
for i,id in enumerate(ids):
 x=(i%5)*150;y=(i//5)*210
 d.rectangle((x+8,y+6,x+141,y+143),fill='#37323e')
 sheet.paste(Image.open(root/(id+'.png')),(x+11,y+10),Image.open(root/(id+'.png')))
 d.text((x+8,y+151),names[i],fill='#ead4a5')
 icon=Image.open(root/('dl_icon_'+id+'.png'));sheet.paste(icon,(x+57,y+173),icon)
sheet.save(root/'contact-sheet-1x.png')
manifest={'package':'ingredients-cultural-expansion-2026-10-21','authoring':'Aseprite native Lua pixel drawing, 2px grid in 128px masters; nearest-neighbour native Aseprite derivatives. Pillow used only for contact sheet and QA.','pixelLab':'Unavailable: no PixelLab tool exposed in this session.','specsRead':['04 art standard','69 DrinkLab manufacturing and recipes','72 growth/research/sales UI','78 ingredient supply adjustment'],'assets':assets,'references':refs,'integration':'Not imported into Unity; only ArtSource package written. All paths are exact existing files. 128px masters are review/source art; dl_* assets are import candidates. Existing coffee bean falling/scoop animation should be reused.','animation':'Tea leaf and heap _00..02 are measured portion variants, not a looping character animation.','sourceBottle':'dl_srcbottle body, white liquid mask and removable cap share a 24x56 canvas. Mask omits the opaque label.','validation':{'nativeRoundtrips':len(assets),'passed':True,'contactSheetScale':'1x native masters and 1x runtime icons','UnityRuntimeTested':False}}
(root/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(root/'REPORT.md').write_text('''# Ingredient cultural expansion delivery

Created ten original ingredient designs, layered Aseprite masters and contract-named runtime derivatives with native Aseprite, plus the 1x contact sheet and exact-path manifest. PixelLab was unavailable in the exposed tool list. All exported sources were reopened by Aseprite and matched their delivered PNG pixel data; every asset is nonempty with binary alpha.

The package contains 128px review masters; 32px runtime icons/tins; 24x56 spirit bottles/source bodies/caps/masks; and three 8px leaf and 32x16 heap portion variants per tea. Read manifest.json for each actual dimension, hash, import settings, layer count and cultural references. Runtime candidates use the existing code ID prefixes; master names are not extra ingredient IDs.

Spirits use green wormwood, violet blackcurrants and yellow oval lemon emblems. White tea uses pale unopened buds, oolong twisted leaf clusters, jasmine five-petal pale flowers and chamomile many white rays with yellow centers. Coffee tins share the same roasted bean drawing and differ through green/floral highland, violet/cherry highland and gold/agricultural row badges; these are game illustrations rather than claims of a guaranteed flavour, cultivar, lot process, certification or official producer packaging.

Reference access limitation: the EGT Yirgacheffe page was found in search results, but direct opening returned 404; only the indexed Kochere/Yirgacheffe provenance excerpt was used. The supplied farm/origin IDs are preserved exactly. No flags, brand logos, text in sprites, people or health claims were added.

Unity integration and actual screen/runtime validation remain with the coordinator/implementation worker. No Unity Assets, code, settings, external docs or git operations were changed by this work.
''',encoding='utf8')
print(json.dumps({'assetPairs':len(assets),'roundtripPassed':len(assets),'contactSheet':str(root/'contact-sheet-1x.png')}))
