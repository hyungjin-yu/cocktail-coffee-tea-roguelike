import json,pathlib,shutil
r=pathlib.Path(__file__).resolve().parent
p=r.parents[2]
(r/'references').mkdir(exist_ok=True)
refs=[p/'ArtSource/card-education-2026-10-16/card_gin.png',p/'ArtSource/ui-polish-2026-10-08/ui_panel/ui_panel.png',p/'Assets/_Project/Resources/Icons/Enemies/teabag_imp.png']
for ref in refs:
 if ref.exists(): shutil.copy2(ref,r/'references'/ref.name)
contract={'specVersion':'1.1-2026-10-19','scope':'progression_ui_73','status':'in_progress','coordinateOrigin':'top_left','rectFormat':'x,y,width,height; half-open','unityBordersOrder':'left,bottom,right,top','pointFiltering':True,'mipmaps':False,'compression':'none','textBaked':False,'handsArmsFacesBodies':False,'states':['locked','available','owned','equipped'],'integrationAllowed':False,'runtimeIntegrationGate':'root ready-art review and manufacturing physics acceptance','motionTargetsMs':{'recipe_tear':500,'laptop_arrive':250,'laptop_open':350,'laptop_light':150,'laptop_close':350},'references':[str(x) for x in refs if x.exists()],'referenceActuallyTransmittedToPixelLab':str(refs[0]),'staffPortraits':'out of scope; frame only; no people','ready':False,'integrated':False,'verified':False}
(r/'contract-plan.json').write_text(json.dumps(contract,indent=2),encoding='utf8')
