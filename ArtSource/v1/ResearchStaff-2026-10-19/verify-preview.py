import pathlib,json,hashlib,itertools
from PIL import Image,ImageDraw
R=pathlib.Path(__file__).resolve().parent
roles=[('cocktail_aroma','향미 분석가','스터링 전문','stirring','attack',6,None,'silver bob / rectangular glasses / plum tailored jacket'),('cocktail_blend','배합 기술자','셰이킹 전문','shaking','max_hp',10,'cocktail crafts >= 3','bald head / square jaw and beard / orange utility jacket'),('tea_aroma','차향 평가사','맑은 우림','tea','attack',6,None,'high dark bun / long face / green stand collar and ivory shawl'),('tea_blend','차 블렌딩 연구원','포근한 우림','tea','max_hp',10,'tea crafts >= 3','short ivory waves / round glasses / indigo smock and beige scarf')]
assets=[];checks=[]
def ck(n,v):checks.append({'check':n,'passed':bool(v)})
for sid,title,study,tech,stat,rp,pre,design in roles:
 p=R/f'staff_{sid}.png'
 if not p.exists():continue
 im=Image.open(p).convert('RGBA');b=im.getbbox();pixels=list(im.getdata());rt=Image.open(R/f'qa/staff_{sid}_roundtrip.png').convert('RGBA');a=R/f'staff_{sid}.aseprite'
 ck(sid+':size',im.size==(28,34));ck(sid+':rgba',Image.open(p).mode=='RGBA');ck(sid+':binaryAlpha',{v[3] for v in pixels}=={0,255});ck(sid+':transparentRGB',all(v[:3]==(0,0,0) for v in pixels if v[3]==0));ck(sid+':nonempty',b is not None);ck(sid+':margin',b is not None and b[0]>0 and b[1]>0 and b[2]<28 and b[3]<34);ck(sid+':nativeRoundtrip',im.tobytes()==rt.tobytes());ck(sid+':nativeExists',a.exists())
 frame=Image.open(R/'qa'/f'staff_{sid}_frame.png').convert('RGBA');base=Image.open(R.parent/'ProgressionUI-2026-10-19/assets/staff_card/staff_card.png').convert('RGBA');expected=base.copy();expected.alpha_composite(im,(18,27));ck(sid+':frameComposite',expected.tobytes()==frame.tobytes());ck(sid+':frameRect',im.width==28 and im.height==34)
 assets.append({'assetId':'staff_'+sid,'staffId':sid,'titleForActualUI':title,'studyForActualUI':study,'effect':{'technique':tech,'buffField':stat,'relativeMultiplier':1.08,'scope':'only existing positive matching buff; preserve item caps'},'costRP':rp,'condition':pre,'png':p.name,'aseprite':a.name,'size':[28,34],'alphaBBox':list(b),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'nativeSHA256':hashlib.sha256(a.read_bytes()).hexdigest(),'rawSource':'provenance/'+sid+'.raw.png','receipt':'provenance/'+sid+'.receipt.json','frameReference':'../ProgressionUI-2026-10-19/assets/staff_card/staff_card.png','targetPortraitRect':[18,27,28,34],'pivotNormalized':[.5,.5],'design':design,'status':'ready','integrated':False,'verified':False})
ck('fourRoles',len(assets)==4)
if len(assets)==4:ck('uniqueImages',len({a['sha256'] for a in assets})==4)
# Preview sheet contains labels only outside delivery sprites.
sheet=Image.new('RGB',(4*256,420),(53,47,57));d=ImageDraw.Draw(sheet)
for i,a in enumerate(assets):
 im=Image.open(R/a['png']).convert('RGBA').resize((168,204),Image.Resampling.NEAREST);sheet.paste(im,(i*256+44,40),im);d.text((i*256+4,8),a['staffId'],fill='white')
 # native frame at 1x for exact fit, and portrait at native 1x underneath
 frame=Image.open(R/f'qa/staff_{a["staffId"]}_frame.png').convert('RGBA');sheet.paste(frame,(i*256+44,268),frame);native=Image.open(R/a['png']).convert('RGBA');sheet.paste(native,(i*256+8,280),native)
sheet.save(R/'qa/portraits-frame-contact.png')
for color,name in [((0,0,0),'black'),((255,255,255),'white')]:
 out=Image.new('RGB',(4*192,240),color)
 for i,a in enumerate(assets):
  im=Image.open(R/a['png']).convert('RGBA').resize((168,204),Image.Resampling.NEAREST);out.paste(im,(i*192+12,18),im)
 out.save(R/f'qa/alpha-{name}.png')
qa={'passed':all(c['passed'] for c in checks),'checkCount':len(checks),'failed':[c['check'] for c in checks if not c['passed']],'checks':checks}
(R/'qa/verification.json').write_text(json.dumps(qa,indent=2),encoding='utf8')
m={'specVersion':'research-staff-2026-10-19','scope':'four_research_employee_portraits_72','status':'ready' if qa['passed'] else 'in_progress','portraitSize':[28,34],'targetCardPortraitRect':[18,27,28,34],'coordinateOrigin':'top_left; rect x,y,width,height','pointFilter':True,'compression':'none','mipmap':False,'textBaked':False,'actualStyleReference':'references/bartender-style.png','referenceDelivery':'actual RGBA supplied to PixelLab style_image; nearest <=64x64','sourceTool':'PixelLab generate_image_pro_flash installed-extension protocol','finishTool':'Aseprite 1.3.18.6 palette/alpha/canvas normalization','facePermission':'explicitly authorized employee face/shoulder portraits; not manufacture/laptop action','armsHandsSipping':False,'gameplayClassOrCompanion':False,'oneActiveResearchSpecialty':True,'integrationAllowed':False,'integrationGate':'root art review and manufacturing gate; Claude owns Assets/UI','assets':assets,'qa':qa,'integrated':False,'verified':False}
(R/'manifest.json').write_text(json.dumps(m,indent=2,ensure_ascii=False),encoding='utf8');print(json.dumps({'portraits':len(assets),'checks':len(checks),'failed':qa['failed']},ensure_ascii=False))
