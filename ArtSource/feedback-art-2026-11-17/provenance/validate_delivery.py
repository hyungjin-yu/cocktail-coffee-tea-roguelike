from pathlib import Path
from PIL import Image,ImageDraw
import json,subprocess,hashlib,struct,sys
r=Path('ArtSource/feedback-art-2026-11-17').resolve();group=sys.argv[1];records=json.loads((r/f'provenance/native-finish-{group}.json').read_text(encoding='utf-8'));ids=[x['assetId'] for x in records];v=r/'verification';(v/'roundtrip').mkdir(exist_ok=True)
lua='local root='+json.dumps(r.as_posix()+'/')+'\nlocal records={}\n'
for id in ids:
 lua+='do local s=app.open(root.."ready/'+id+'.aseprite") s:saveCopyAs(root.."verification/roundtrip/'+id+'.png") local p=s.slices[1].pivot records[#records+1]={assetId="'+id+'",x=p.x,y=p.y,frames=#s.frames} s:close() end\n'
lua+='local f=assert(io.open(root.."verification/pivots-'+group+'.json","w")) f:write(json.encode(records)) f:close()\n'
(v/f'roundtrip-{group}.lua').write_text(lua,encoding='utf-8');res=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(v/f'roundtrip-{group}.lua')],capture_output=True,text=True,timeout=60);assert res.returncode==0; piv={x['assetId']:x for x in json.loads((v/f'pivots-{group}.json').read_text(encoding='utf-8'))};out=[]
for id in ids:
 png=r/f'ready/{id}.png';src=png.with_suffix('.aseprite');im=Image.open(png).convert('RGBA');rt=Image.open(v/f'roundtrip/{id}.png').convert('RGBA');b=src.read_bytes();expected=(36,36) if group=='americano' else (24,24) if group=='hidden' else (32,32);pp=[im.width/2,im.height] if group in ['americano','containers'] else [im.width/2,im.height/2]
 c={'assetId':id,'dimensions':list(im.size),'alphaBounds':im.getchannel('A').getbbox(),'alphaValues':sorted(set(im.getchannel('A').getdata())),'pngSha256':hashlib.sha256(png.read_bytes()).hexdigest(),'sourceSha256':hashlib.sha256(b).hexdigest(),'nativePivotTopLeftPx':[piv[id]['x'],piv[id]['y']]}
 c['checks']={'dimensions':im.size==expected,'nativeCanvas':tuple(struct.unpack_from('<HH',b,8))==expected,'nativeMagic':struct.unpack_from('<H',b,4)[0]==0xa5e0,'nativeFrames':piv[id]['frames']==1,'roundtripPixels':im.tobytes()==rt.tobytes(),'pivot':c['nativePivotTopLeftPx']==pp,'alpha':set(c['alphaValues'])<={0,255},'nonempty':bool(c['alphaBounds']),'cornersTransparent':all(im.getpixel(p)[3]==0 for p in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)])}
 if id.endswith('_mask'):c['checks']['whiteMask']=all(p[:3]==(255,255,255) for p in im.getdata() if p[3])
 c['pass']=all(c['checks'].values());out.append(c)
result={'group':group,'assetCount':len(out),'assets':out,'structuralPass':all(x['pass'] for x in out),'visualReview':'pending','unityImport':False,'unityRuntime':False}
if group=='americano':
 mug=Image.open(r/'ready/dl_coffee_americano_mug.png').convert('RGBA');mask=Image.open(r/'ready/dl_coffee_americano_mug_mask.png').convert('RGBA');box=mask.getchannel('A').getbbox();a=json.loads((r/'ready/anchors-americano.json').read_text(encoding='utf-8'))['entries'][0];norm=[box[0]/36,(36-box[3])/36,box[2]/36,(36-box[1])/36];result['liquidChecks']={'sameCanvas':mug.size==mask.size,'maskEqualsLiquidAnchor':a['liquid']==norm,'windowAlphaZero':all(mug.getpixel((x,y))[3]==0 for y in range(36) for x in range(36) if mask.getpixel((x,y))[3]),'withinWalls':all(any(mug.getpixel((xx,y))[3] for xx in range(x)) and any(mug.getpixel((xx,y))[3] for xx in range(x+1,36)) for y in range(36) for x in range(36) if mask.getpixel((x,y))[3])};result['structuralPass'] &= all(result['liquidChecks'].values())
 sheet=Image.new('RGB',(840,370),'#28272e');d=ImageDraw.Draw(sheet)
 for i,(bg,label,fill) in enumerate([('#ece4d4','empty',0),('#47624b','245mL / 380mL x .82',245/380*.82),('#a27d50','82% maximum',.82),('#201d29','mask',-1)]):
  base=Image.new('RGBA',(36,36),bg)
  if fill<0:base.alpha_composite(mask)
  else:
   layer=Image.new('RGBA',(36,36));p=layer.load();fy=box[3]-round((box[3]-box[1])*fill)
   for y in range(fy,box[3]):
    for x in range(box[0],box[2]):
     if mask.getpixel((x,y))[3]:p[x,y]=(174,115,67,255) if y==fy else (88,49,34,255)
   base.alpha_composite(layer);base.alpha_composite(mug)
  sheet.paste(base.resize((180,180),Image.Resampling.NEAREST).convert('RGB'),(i*210+15,35));sheet.paste(base.resize((36,36)),(i*210+15,240));d.text((i*210+10,10),label,fill='white')
 d.text((12,310),'Art compositing proof only; renderer formula .82 * volume/capacity. No Unity/human play claim.',fill='white');sheet.save(r/'previews/americano-window-fill.png')
(v/f'{group}-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({'group':group,'structuralPass':result['structuralPass'],'count':len(out),'failures':[x for x in out if not x['pass']],'liquidChecks':result.get('liquidChecks')},indent=2))
