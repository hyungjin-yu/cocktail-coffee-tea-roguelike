"""Read-only pixel/anatomy analysis; writes metadata, never edited images."""
import json
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parent
contract=json.loads((ROOT/'source-frame-contract.json').read_text(encoding='utf-8-sig'))
def components(points):
    points=set(points); groups=[]
    while points:
        start=points.pop(); todo=[start]; group=[start]
        while todo:
            x,y=todo.pop()
            for q in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if q in points: points.remove(q);todo.append(q);group.append(q)
        groups.append(group)
    return sorted(groups,key=len,reverse=True)
def bounds(group):
    xs,ys=zip(*group);return [min(xs),min(ys),max(xs),max(ys)]
records=[]
for job,data in contract['jobs'].items():
    for frame in data['frames']:
        if frame['runtimeUse']!='runtime':continue
        im=Image.open(frame['source']).convert('RGBA')
        skin=[]
        for y in range(256):
            for x in range(256):
                r,g,b,a=im.getpixel((x,y))
                if a and r>160 and 85<g<200 and 45<b<160 and r>g*1.2 and g>b*1.15:skin.append((x,y))
        groups=components(skin)
        head= bounds(groups[0])
        hx=(head[0]+head[2])/2;hy=(head[1]+head[3])/2
        vest=[]
        for y in range(256):
            for x in range(256):
                r,g,b,a=im.getpixel((x,y))
                color=(b>r*1.35 and b>g*1.15 and 12<r<110) if job=='bartender' else (g>r*1.10 and g>b*1.25 and 35<g<160)
                if a and color and (x-hx)**2+(y-hy)**2<100**2:vest.append((x,y))
        vg=components(vest)
        chest=bounds(vest) if vg else [int(hx-27),int(hy+20),int(hx+15),int(hy+65)]
        hair=[]
        for y in range(max(0,int(hy-45)),min(256,int(hy+12))):
            for x in range(max(0,int(hx-35)),min(256,int(hx+35))):
                r,g,b,a=im.getpixel((x,y))
                if a and 35<r<165 and 20<g<105 and r>g*1.18 and g>b*1.1:hair.append((x,y))
        haircenter=[sum(p[0] for p in hair)/len(hair),sum(p[1] for p in hair)/len(hair)] if hair else [hx,hy-20]
        record={**frame,'job':job,'headSkinBounds':head,'hairCenter':haircenter,'vestBounds':chest}
        records.append(record)
(ROOT/'anatomy-contract.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
def lua(v):
    if v is None:return 'nil'
    if isinstance(v,bool):return 'true' if v else 'false'
    if isinstance(v,(int,float)):return str(v)
    if isinstance(v,str):return json.dumps(v,ensure_ascii=False)
    if isinstance(v,list):return '{'+','.join(lua(x) for x in v)+'}'
    return '{'+','.join('['+lua(k)+']='+lua(x) for k,x in v.items())+'}'
(ROOT/'anatomy-contract.lua').write_text('return '+lua(records),encoding='utf-8')
print('Read-only anatomy records:',len(records))
