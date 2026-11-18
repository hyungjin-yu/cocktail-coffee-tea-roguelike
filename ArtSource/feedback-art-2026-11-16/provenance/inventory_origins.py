import pathlib,re,json,hashlib
from PIL import Image
root=pathlib.Path.cwd(); source=root/'Assets/_Project/Scripts/Drinks/DrinkData.cs'; s=source.read_text(encoding='utf-8-sig'); const=dict(re.findall(r'(\w+)\s*=\s*"([a-z0-9_]+)"',s)); rows=[]
for block in re.findall(r'new DrinkIngredientDef\s*\{(.*?)\}',s,re.S):
 if 'use = DrinkUse.Coffee' not in block or 'form = IngredientForm.Leaf' not in block: continue
 symbol=re.search(r'\bid\s*=\s*(\w+)',block).group(1); identifier=const[symbol]
 if identifier=='coffee_bean': continue
 name=re.search(r'\bname\s*=\s*"([^"]+)"',block).group(1); icon=re.search(r'iconId\s*=\s*"([^"]+)"',block).group(1)
 rows.append({'ingredientId':identifier,'name':name,'assetId':icon})
ids=re.search(r'string\[\] coffeeIds = \{(.*?)\}',s,re.S).group(1).split(','); names=re.findall(r'"([^"]+)"',re.search(r'string\[\] coffeeNames = \{(.*?)\}',s,re.S).group(1))
for symbol,name in zip(ids,names):
 identifier=const[symbol.strip()]; rows.append({'ingredientId':identifier,'name':name,'assetId':'dl_icon_'+identifier})
for row in rows:
 f=root/'Assets/_Project/Resources/DrinkLab'/(row['assetId']+'.png'); row['existingPng']=str(f.relative_to(root)) if f.exists() else None
 if f.exists():
  im=Image.open(f).convert('RGBA'); row.update(size=list(im.size),alphaBounds=im.getchannel('A').getbbox(),alphaSha256=hashlib.sha256(im.getchannel('A').tobytes()).hexdigest(),pngSha256=hashlib.sha256(f.read_bytes()).hexdigest())
 row['status']='inventory_only_not_ready'
out=root/'ArtSource/feedback-art-2026-11-16'; (out/'origin-inventory.json').write_text(json.dumps({'source':str(source.relative_to(root)),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'count':len(rows),'assets':rows},ensure_ascii=False,indent=2),encoding='utf-8')
print('Current named origins:',len(rows)); print('Existing icon PNGs:',sum(bool(x['existingPng']) for x in rows)); print('Distinct alpha masks:',len(set(x.get('alphaSha256') for x in rows if x['existingPng'])))
