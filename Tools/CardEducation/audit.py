"""Static specification, mechanics, source/import identity and preservation audit."""
from pathlib import Path
import hashlib,json,re,subprocess,datetime
r=Path(__file__).resolve().parents[2]
out=r/'Delivery/CardEducation-2026-10-16';out.mkdir(parents=True,exist_ok=True)
planning=next(Path('C:/Users/user/Documents/Personal_Note').rglob('64_음료 지식과 카드 효과 개편_2026-10-16.md'))
expected={}
for line in planning.read_text(encoding='utf-8-sig').splitlines():
 c=[v.strip() for v in line.split('|')]
 if len(c)==5 and re.fullmatch('[a-z_]+',c[1]) and '→' in c[3]:expected[c[1]]=c[2]
rows=json.loads((r/'Tools/CardEducation/authored-data.json').read_text(encoding='utf-8'))
issues=[];result=[]
def val(text,key):
 m=re.search(r'^  '+key+r':\s*(.*)$',text,re.M)
 return json.loads(m[1]) if m and m[1].startswith('"') else (m[1] if m else None)
def mechanics(text):
 return {k:val(text,k) for k in ['id','category','rarity','maxStacks','weaponRestriction']} | {'effects':re.search(r'^  effects:.*?(?=^  weaponRestriction:)',text,re.M|re.S)[0]}
for row in rows:
 path=r/'Assets/_Project/Resources/Cards'/('Card_'+row['id']+'.asset');text=path.read_text(encoding='utf-8')
 old=subprocess.check_output(['git','show','HEAD:'+path.relative_to(r).as_posix()],cwd=r).decode('utf-8-sig')
 same=mechanics(text)==mechanics(old)
 exact=val(text,'displayName')==expected[row['id']]==row['name']
 if not same or not exact:issues.append(row['id'])
 result.append({'id':row['id'],'name':val(text,'displayName'),'specNameMatches':exact,'mechanicsPreserved':same,'source':row['sourceUrl']})
art=[]
for id in sorted({row['art'] for row in rows if row['art']}):
 source=r/'ArtSource/card-education-2026-10-16'/(id+'.png');target=r/'Assets/_Project/Resources/CardEducation'/(id+'.png')
 digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
 same=source.exists() and target.exists() and digest(source)==digest(target)
 art.append({'id':id,'sourceSha256':digest(source),'importSha256':digest(target),'identical':same})
 if not same:issues.append('art:'+id)
baseline=json.loads(next(planning.parent.rglob('card-education-preservation-baseline-2026-10-16.json')).read_text(encoding='utf-8-sig'))
preserved=[]
for item in baseline:
 hash=hashlib.sha256((r/item['path']).read_bytes()).hexdigest();same=hash==item['sha256']
 preserved.append(item|{'currentSha256':hash,'preserved':same})
 if not same:issues.append('preservation:'+item['path'])
report={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'cards':result,'art':art,'preservation':preserved,'issues':issues}
(out/'static-contract.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Cards:',len(result),'Art:',len(art),'Issues:',issues)
raise SystemExit(bool(issues))
