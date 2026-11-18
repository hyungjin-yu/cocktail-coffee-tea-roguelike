from pathlib import Path
import json,math
from PIL import Image,ImageDraw
r=Path('ArtSource/cafe-title-2026-11-18/revisions/v4-bar-end');p=r/'layout.json';d=json.loads(p.read_text(encoding='utf-8'));fp=d['floorPlan'];fp['entrance'].update(openingY=[3,4.05],hingeWorld=[0,4.05,0],thresholdWorld=[[0,3,0],[0,4.05,0]],swing='inward toward east; fixed north hinge; sweep lies south/east of hinge',clearSwingReservation=[0,3,1.1,4.1]);fp['researchTable']=[1.3,4.35,2.5,5.05];fp['publicPathMeters']=[[-1.6,3.5],[.7,3.5],[1.8,2.8],[3.5,.1],[5.4,3.2]];fp['seating']={'roundTable':{'center':[6.6,1.3],'diameter':.7,'height':.72},'chairs':[{'center':[5.9,1.3],'size':[.45,.5]},{'center':[7.3,1.3],'size':[.45,.5]}],'interactive':False,'reason':'Small actual customer seating, outside all door/camera/order paths'}
d['cameraWorld']['horizontalFovDegrees']=110;d['cameraWorld']['limitations']='Camera and110deg lens are design projection only; native production must preserve one rectilinear vanishing system. No geometric art has been approved ready.';w=d['cameraWorld']['worldWaypoints'];w[0]['eye']=[-1.6,3.5,1.55];w[1]['eye']=[-.5,3.5,1.55];w[2]['eye']=[.7,3.5,1.65];w[3]['eye']=[1.8,2.8,2.15]
cam=[3.5,.1,2.7];pitch=math.radians(22);f=1/(2*math.tan(math.radians(55)));fy=f*16/9
def project(x,y,z):
 dx,dy,dz=x-cam[0],y-cam[1],z-cam[2];dep=math.cos(pitch)*dy-math.sin(pitch)*dz;up=math.sin(pitch)*dy+math.cos(pitch)*dz;return [.5+f*dx/dep,.5-fy*up/dep]
for ft in d['interior']['features']:
 id=ft['id']
 if id=='permanent':ft['worldFootprintMeters']=[1.3,4.35,2.5,5.05]
 if id=='codex':ft['worldFootprintMeters']=[2.7,5.6,3.6,6]
 if id=='settings':ft['worldFootprintMeters']=[7.35,3.0,7.95,3.7]
 if id=='quit':ft['worldFootprintMeters']=[0,3,.08,4.05]
 b=ft['worldFootprintMeters'];h=ft['heightMeters'];pts=[project(x,y,z) for x in [b[0],b[2]] for y in [b[1],b[3]] for z in ([1.6,2.15] if id=='credits' else [0,h])];xs=[x[0] for x in pts];ys=[x[1] for x in pts];rect=[min(xs),min(ys),max(xs)-min(xs),max(ys)-min(ys)];assert min(xs)>=0 and max(xs)<=1 and min(ys)>=0 and max(ys)<=.87,(id,rect);ft['projectedRect']=[round(x,5) for x in rect];ft['label_anchor']=[round((min(xs)+max(xs))/2,5),round(max(ys)+.025,5)];x,y,ww,hh=rect
 if id in ['codex','skins']:hh=min(hh,.50-y)
 if id=='history':hh=.06
 if id=='credits':y-=.0075;hh=.06
 ft['hotspot']={'rect':[round(x,5) for x in [x,y,ww,hh]]}
fp['props']=d['interior']['features'];p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
im=Image.new('RGB',(1000,760),'#eee7d7');dr=ImageDraw.Draw(im);ox,oy,sc=120,80,90
def xy(x,y):return ox+x*sc,oy+(6-y)*sc
dr.rectangle((*xy(0,6),*xy(8,0)),outline='#40352f',width=6)
for ft in d['interior']['features']:
 if ft['id']=='quit':continue
 b=ft['worldFootprintMeters'];a=xy(b[0],b[3]);c=xy(b[2],b[1]);dr.rectangle((*a,*c),fill='#bb9a71',outline='#5a4438',width=2);dr.text((a[0]+3,a[1]+3),ft['id'],fill='#201d29')
a=xy(3.9,5.6);c=xy(7.5,4.5);dr.rectangle((*a,*c),outline='#55795a',width=3);dr.text((a[0]+3,a[1]+8),'STAFF AISLE 1.1m',fill='#29452e');dr.line([xy(*v) for v in fp['publicPathMeters']],fill='#307b9c',width=5)
hinge=xy(0,4.05);closed=xy(0,3);opened=xy(1.05,4.05);dr.line([hinge,closed],fill='#b35843',width=5);dr.line([hinge,opened],fill='#519261',width=5)
arc=[xy(1.05*math.sin(math.radians(a)),4.05-1.05*math.cos(math.radians(a))) for a in range(0,91,5)];dr.line(arc,fill='#b28253',width=2)
cx,cy=xy(6.6,1.3);rr=.35*sc;dr.ellipse((cx-rr,cy-rr,cx+rr,cy+rr),fill='#ad8865',outline='#40352f',width=3)
for ch in fp['seating']['chairs']:
 x,y=ch['center'];w,h=ch['size'];dr.rectangle((*xy(x-w/2,y+h/2),*xy(x+w/2,y-h/2)),fill='#6e5949',outline='#40352f',width=2)
dr.text((20,20),'DRAFT FLOOR PLAN - same west doorway /110deg final lens / not game art',fill='#201d29');dr.text((120,655),'Blue: camera/customer route | Green: open leaf and staff aisle | Arc: reserved physical swing',fill='#201d29');dr.text((120,680),'No camera wall-crossing except the actual doorway; final camera remains inside south wall.',fill='#201d29');im.save(r/'design/floor-plan-draft.png')
im=Image.new('RGB',(960,540),'#eee7d7');dr=ImageDraw.Draw(im)
for i,ft in enumerate(d['interior']['features']):
 x,y,w,h=ft['projectedRect'];box=(x*960,y*540,(x+w)*960,(y+h)*540);dr.rectangle(box,outline=['#49634b','#885c40','#67758c'][i%3],width=2);dr.text((box[0]+3,box[1]+3),ft['id'],fill='#201d29')
dr.text((24,20),'DRAFT110deg projection / furniture positions dictated by floor plan, not uniform buttons',fill='#201d29');dr.text((24,495),'Contain required at4:3. Hotspots are still draft; visible polygons and label collision checks remain.',fill='#201d29');im.save(r/'design/projected-objects-draft.png')
q=r/'design/CONTRACT-DRAFT.md';s=q.read_text(encoding='utf-8-sig');s=s.replace('126도 수평 시야 초안은','수정된110도 수평 시야 초안은').replace('필요하면 동쪽 조절장을 같은 동쪽 벽의 더 뒤 위치로 옮기고 시야각을 줄이는 최소 현실성 수정을 먼저 합의한다.','초기126도 투영이 너무 넓어 제작 전에 동쪽 조절장과 서쪽 문을 각각 같은 벽의 조금 뒤 위치로 조정하고110도로 줄였다. 문 회전 구역도 경첩의 남동쪽 실제 부채꼴로 재검산했다. 오른쪽 앞에는 비상호작용 작은 원형 테이블/의자2개를 두되 손님/카메라 동선을 비운다.');q.write_text(s,encoding='utf-8')
print('Refined draft110deg, all8 projected bounds inside scene; no final hotspot/ready claim')
