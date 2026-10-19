import json,pathlib,hashlib,collections
from PIL import Image,ImageChops
R=pathlib.Path(__file__).resolve().parent
m=json.loads((R/'manifest.json').read_text(encoding='utf8'));checks=[];fails=[]
def check(name,result,detail=None):
 checks.append({'check':name,'passed':bool(result),'detail':detail})
 if not result:fails.append(name)
for a in m['assets']:
 aid=a['assetId']
 for f in a['frames']:
  p=R/f['path'];im=Image.open(p).convert('RGBA');pixels=list(im.getdata());alpha={v[3] for v in pixels}
  check(aid+f':{f["index"]}:size',list(im.size)==a['nativeSize'])
  check(aid+f':{f["index"]}:rgba_nonempty_binary',Image.open(p).mode=='RGBA' and im.getbbox() is not None and alpha=={0,255})
  check(aid+f':{f["index"]}:transparent_rgb_zero',all(v[:3]==(0,0,0) for v in pixels if v[3]==0))
  check(aid+f':{f["index"]}:sha',hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'])
  rp=R/'qa/native-roundtrip'/aid/f'{f["index"]:02d}.png'
  check(aid+f':{f["index"]}:aseprite_roundtrip',rp.exists() and Image.open(rp).convert('RGBA').tobytes()==im.tobytes())
 if a['contentRect'] and len(a['frames'])==1:
  x,y,w,h=a['contentRect'];im=Image.open(R/a['frames'][0]['path']).convert('RGBA');vals=set(im.crop((x,y,x+w,y+h)).getdata())
  check(aid+':content_rect_blank',len(vals)==1 and next(iter(vals))[3]==255,{'uniqueColors':len(vals),'rect':a['contentRect']})
 if len(a['frames'])>1:
  gif=Image.open(R/a['previewGif']);dur=[]
  for i in range(gif.n_frames):gif.seek(i);dur.append(gif.info.get('duration',0))
  check(aid+':gif_frames',gif.n_frames==len(a['frames']),gif.n_frames)
  check(aid+':gif_duration',sum(dur)==a['totalDurationMs'],sum(dur))
  check(aid+':runtime_nonloop',a['loop']==False and a['holdFinal']==True)
  check(aid+':preview_play_once','loop' not in gif.info,gif.info.get('loop','absent'))
opened=Image.open(R/'assets/laptop_open/laptop_open.png').convert('RGBA')
light=Image.open(R/'assets/laptop_light/frames/laptop_light_02.png').convert('RGBA')
seq=Image.open(R/'assets/laptop_sequence/frames/laptop_sequence_14.png').convert('RGBA').crop((0,0,384,256))
check('light_final_equals_open',opened.tobytes()==light.tobytes())
check('sequence_final_equals_open',opened.tobytes()==seq.tobytes())
closed=Image.open(R/'assets/laptop_closed_matched/laptop_closed_matched.png').convert('RGBA')
check('closing_final_equals_closed',closed.tobytes()==Image.open(R/'assets/laptop_closing/frames/laptop_closing_06.png').convert('RGBA').tobytes())
for aid in ['laptop_opening','laptop_closing','laptop_light']:
 for p in sorted((R/'assets'/aid/'frames').glob('*.png')):
  im=Image.open(p).convert('RGBA');check(aid+':'+p.stem+':table_contact_fixed',im.crop((0,246,384,256)).tobytes()==opened.crop((0,246,384,256)).tobytes())
# Full insertion is exactly the fixed notebook and box, with no floating page pixels.
last=Image.open(R/'assets/recipe_tear_store/frames/recipe_tear_store_09.png').convert('RGBA')
base=Image.new('RGBA',(512,224));base.alpha_composite(Image.open(R/'assets/spring_notebook/spring_notebook.png').convert('RGBA'),(8,40));base.alpha_composite(Image.open(R/'assets/recipe_storage/recipe_storage.png').convert('RGBA'),(336,108))
check('recipe_final_page_fully_stored',last.tobytes()==base.tobytes())
for h in [192,288,384]:
 im=Image.open(R/f'qa/receipt-height-{h}.png').convert('RGBA')
 vals=set(im.crop((16,20,112,h-20)).getdata());check(f'receipt_stretch_{h}_blank',len(vals)==1)
report={'assetCount':len(m['assets']),'spritePngCount':sum(len(a['frames']) for a in m['assets']),'checkCount':len(checks),'passed':not fails,'failed':fails,'checks':checks,'unityIntegrated':False,'unityVerified':False}
(R/'qa/verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps({k:report[k] for k in ['assetCount','spritePngCount','checkCount','passed','failed']},indent=2))
raise SystemExit(bool(fails))
