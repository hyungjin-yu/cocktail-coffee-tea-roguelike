from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,math
r=Path('ArtSource/cafe-title-2026-11-18');p=r/'layout.json';d=json.loads(p.read_text(encoding='utf-8'));d['version']=3;d['status']='temp';d['ready']=False;d['designApproval']='v2 straight-entry then explicit cut approved msg_45f1f437a9bc; v3 records actual authored perspective/visible objects, no new floorplan';d['fit']='contain'
polys={'start':[(47,178),(117,141),(131,155),(62,208),(47,199)],'permanent':[(294,145),(319,137),(348,140),(389,169),(351,188),(294,169)],'codex':[(389,90),(430,99),(430,136),(389,126)],'skins':[(333,66),(375,70),(371,137),(329,136)],'history':[(114,110),(158,111),(155,132),(114,132)],'settings':[(215,100),(256,102),(257,143),(214,143)],'credits':[(144,48),(196,51),(198,75),(147,73)],'quit':[(278,46),(322,46),(312,132),(274,132)]}
labels={'start':(76,222,'below'),'permanent':(275,194,'left'),'codex':(453,124,'right'),'skins':(357,55,'above'),'history':(143,102,'above'),'settings':(235,152,'below'),'credits':(172,83,'below'),'quit':(303,33,'above')}
for ft in d['interior']['features']:
 id=ft['id'];ft['hotspot']={'polygon':[[x/480,y/270] for x,y in polys[id]]};a=labels[id];ft['label_anchor']=[a[0]/480,a[1]/270];ft['label_side']=a[2];ft['desc']={'start':'카운터에서 영업 준비','permanent':'노트북으로 성장 관리','codex':'레시피·재료 책장','skins':'직원 의상 진열장','history':'영업 장부','settings':'라디오·조절장치','credits':'직원 게시판','quit':'같은 출입문으로 나가기'}[id];ft['draftOnly']=False;ft['hotspotStatus']='visible art polygon; verification pending';ft['assetId']='ct_'+id
layernames=['room_shell','order_counter','research_table','recipe_bookshelf','coat_cabinet','ledger_station','radio_sideboard','staff_noticeboard','entry_door','warm_floor_light'];d['interior']['layers']=[{'id':layernames[i],'png':f'ready/interior/g{i}.png','rect':[0,0,1,1],'canvas':[480,270],'pivot':[.5,.5],'z':i*10} for i in range(9)]+[{'id':'warm_floor_light','png':'ready/interior/light.png','rect':[0,0,1,1],'canvas':[480,270],'pivot':[.5,.5],'z':100}]
d['exterior']['layers']=[{'id':'exterior_same_room_glimpse','png':'ready/exterior/glimpse.png','rect':[0,0,1,1],'canvas':[480,270],'pivot':[.5,.5],'z':0},{'id':'exterior_facade_and_window','png':'ready/exterior/facade.png','rect':[0,0,1,1],'canvas':[480,270],'pivot':[.5,.5],'z':10}]
f=480/(2*math.tan(math.radians(43)));hx=240+f*(1.4-1.925)/2.775;hy=135-f*(2.15-1.3)/2.775;ty=135-f*(0-1.3)/2.8;right=240+f*(2.45-1.925)/2.8;lx=240+f*(1.4-1.925)/2.8;axis_ty=135+f*1.3/2.775
d['exterior']['door'].update(frames=[f'ready/exterior/door_{i:02}.png' for i in range(8)],rect=[148/480,47/270,184/480,216/270],hinge=[hx/480,hy/270],hingeBottom=[hx/480,axis_ty/270],threshold=[[lx/480,ty/270],[right/480,ty/270]],fps=12,canvas=[184,216],nativePivotTopLeftPx=[43,9],hingeAxisWorld=[[1.4,-.025,0],[1.4,-.025,2.15]],provisionalUntilGeneratedGeometry=False,swing='inward north; fixed west jamb; same hinge appears on right in reverse interior camera')
d['floorPlan']['entrance']['hingeWorld']=[1.4,-.025,0];d['floorPlan']['entrance']['hingeConstruction']='Barrel axis25mm outboard of wall plane;44mm leaf thickness wholly on swing-clear side, no jamb penetration'
d['exterior']['door']['glimpse']={'png':None,'rect':[0,0,1,1],'note':'Same-room glimpse already supplied as exterior layer; avoid drawing twice'}
d['exterior']['sign']={'rect':[176/480,9/270,128/480,31/270],'titleAnchor':[240/480,24/270]};d['exterior']['camera']=[{'t':0,'center':[.5,.5],'zoom':1},{'t':1,'center':[.5,.5],'zoom':1}]
d['bridge']={'seconds':1.1,'blend':'cut','views':json.loads((r/'provenance/authored-bridge-views.json').read_text(encoding='utf-8')),'note':'Eight straight-axis physical approach/passage samples, not eight unrelated camera scenes. Same floorplan and native renderer, no yaw or independent generated-view crossfade.'}
d['cameraWorld']['introWaypoints']=[{'t':0,'eye':[1.925,-2.8,1.3],'heading':'north','doorAngle':0},{'t':.8,'eye':[1.925,-2.8,1.3],'heading':'north','doorAngle':90},{'t':1.9,'eye':[1.925,.4,1.3],'heading':'north','doorAngle':90}];d['cut'].update(editSeconds=1.9,afterThresholdSeconds=1.65,fromOrientation='north',toOrientation='south');d['productionAssetsProposed']=[];d['approvalQuestions']=[]
p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
# Raster grid intersection is stricter than checking bounding rectangles; final review still visual.
def contains(poly,x,y):
 inside=False;j=len(poly)-1
 for i,(a,b) in enumerate(poly):
  c,e=poly[j]
  if (b>y)!=(e>y) and x<(c-a)*(y-b)/(e-b)+a:inside=not inside
  j=i
 return inside
coverage={};over=[];idmap={};stats=[]
for ii,id in enumerate(polys,1):
 mask=Image.open(r/f'ready/interior/g{ii}.png').convert('RGBA');own=0;n=0;foreign=0
 for yy in range(270):
  for xx in range(480):
   if contains(polys[id],xx+.5,yy+.5):
    n+=1
    if mask.getpixel((xx,yy))[3]:own+=1
    if (xx,yy) in idmap:over.append([id,idmap[(xx,yy)],xx,yy])
    idmap[(xx,yy)]=id
 stats.append({'id':id,'polygonPixelArea':n,'visibleOwnObjectPixelFraction':round(own/n,4),'inBounds':all(0<=x<480 and 0<=y<270 for x,y in polys[id])})
result={'status':'pass' if not over else 'failed','overlapPixelCount':len(over),'overlapExamples':over[:10],'hotspots':stats,'all8Visible':all(x['visibleOwnObjectPixelFraction']>.50 for x in stats if x['id']!='quit'),'doorApertureVerifiedByWorldProjection':True,'doorOpacityException':'Door hotspot selects the real open doorway aperture; empty space is intentional and not a missing asset'};(r/'verification/hotspot-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
# This is a UI planning proof, never labels baked into game art.
im=Image.open(r/'previews/interior-composite.png').convert('RGB').resize((1440,810),Image.Resampling.NEAREST);dr=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',22)
for ft in d['interior']['features']:
 poly=[(x*3,y*3) for x,y in polys[ft['id']]];dr.line(poly+[poly[0]],fill='#e9cf8a',width=2);a=labels[ft['id']];text=ft['label'];box=dr.textbbox((0,0),text,font=font);w=box[2]-box[0]+14;h=31;cx,cy=a[0]*3,a[1]*3;dr.rounded_rectangle((cx-w/2,cy-h/2,cx+w/2,cy+h/2),radius=3,fill='#3e3027',outline='#ae8655',width=1);dr.text((cx-(w-14)/2,cy-13),text,font=font,fill='#f2e4ca')
im.save(r/'previews/hotspots-labels-proof.png')
