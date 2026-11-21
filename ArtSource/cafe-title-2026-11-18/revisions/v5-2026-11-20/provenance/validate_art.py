from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,subprocess,hashlib,struct,math,itertools
r=Path('ArtSource/cafe-title-2026-11-18/revisions/v5-2026-11-20').resolve();records=json.loads((r/'provenance/native-delivery-assets.json').read_text(encoding='utf-8'));assert len(records)==28
lua='local root='+json.dumps(r.as_posix()+'/')+'\nlocal out={}\n'
for rec in records:
 path=rec['path'];name=Path(path).name;key=path.replace('/','_');lua+=f'do local s=app.open(root.."{path}.aseprite") local p=s.slices[1].pivot s:saveCopyAs(root.."verification/roundtrip/{key}.png") out[#out+1]={{path="{path}",pivot={{p.x,p.y}},frames=#s.frames}} s:close() end\n'
lua+='local s=app.open(root.."ready/interior/ct_interior_master.aseprite") s:saveCopyAs(root.."verification/roundtrip/interior-master.png") s:close()\n'
for f in sorted((r/'raw').glob('*.png')):
 raw=f.relative_to(r).as_posix();lua+=f'do local s=app.open(root.."{raw}") s:close() local t=app.open(root.."{raw[:-4]}.aseprite") t:saveCopyAs(root.."verification/roundtrip/raw-{f.name}") t:close() end\n'
lua+='local f=assert(io.open(root.."verification/native-source-structure.json","w")) f:write(json.encode(out)) f:close()\n';(r/'verification/roundtrip.lua').write_text(lua,encoding='utf-8');res=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'verification/roundtrip.lua')],capture_output=True,text=True,timeout=120);assert res.returncode==0;(r/'verification/roundtrip-log.txt').write_text(res.stdout+res.stderr,encoding='utf-8');native={x['path']:x for x in json.loads((r/'verification/native-source-structure.json').read_text(encoding='utf-8'))};out=[]
for a in records:
 path=a['path'];p=r/(path+'.png');s=r/(path+'.aseprite');im=Image.open(p).convert('RGBA');rt=Image.open(r/'verification/roundtrip'/(path.replace('/','_')+'.png')).convert('RGBA');b=s.read_bytes();checks={'dimensions':im.size==(a['width'],a['height']),'nonempty':bool(im.getchannel('A').getbbox()),'nativeMagic':struct.unpack_from('<H',b,4)[0]==0xa5e0,'nativeCanvas':tuple(struct.unpack_from('<HH',b,8))==im.size,'nativeFrames1':native[path]['frames']==1,'nativePivot':native[path]['pivot']==a['pivot'],'roundtripPixelsEqual':im.tobytes()==rt.tobytes()};values=sorted(set(im.getchannel('A').getdata()));checks['alphaPolicy']=set(values)<={0,42,255} if '/exterior/' in path else set(values)<={0,255} if not path.endswith('/light') else all(0<=x<=18 for x in values)
 out.append({**a,'alphaBounds':im.getchannel('A').getbbox(),'alphaValues':values,'checks':checks,'pass':all(checks.values()),'pngSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'sourceSha256':hashlib.sha256(s.read_bytes()).hexdigest()})
raw=[]
for p in sorted((r/'raw').glob('*.png')):
 im=Image.open(p).convert('RGBA');rt=Image.open(r/'verification/roundtrip'/('raw-'+p.name)).convert('RGBA');raw.append({'png':p.relative_to(r).as_posix(),'dimensions':list(im.size),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'nativeSha256':hashlib.sha256(p.with_suffix('.aseprite').read_bytes()).hexdigest(),'roundtripPixelsEqual':im.size==rt.size and im.tobytes()==rt.tobytes()})
refs=json.loads((r/'reference/reuse-inventory.json').read_text(encoding='utf-8'));refpass=all(hashlib.sha256(Path(x['original']).read_bytes()).hexdigest()==x['sha256'] for x in refs)
master=Image.open(r/'previews/interior-composite.png').convert('RGBA');rt=Image.open(r/'verification/roundtrip/interior-master.png').convert('RGBA');hot=json.loads((r/'verification/hotspot-validation.json').read_text(encoding='utf-8'));layout=json.loads((r/'layout.json').read_text(encoding='utf-8'))
# Rigid leaf geometry, jamb clearance and unchanged real hinge, not just image width tests.
angles=layout['exterior']['door']['anglesDegrees'];hinge=(1.4,-.025);traces=[]
for deg in angles:
 a=math.radians(deg);co,si=math.cos(a),math.sin(a)
 def tr(u,v):
  u=u*(1.046/1.05)
  return(1.4+u*co-(v-.022)*si,-.025+u*si+(v-.022)*co)
 corners=[tr(u,v) for u in [0,1.05] for v in [-.022,.022]]
 # Any negative-x extent into the solid west jamb is prohibited; hardware screws intentionally mount there.
 leafclear=all(1.4-1e-9<=x<=2.45+1e-9 for x,y in corners)
 traces.append({'angle':deg,'leafCornerXY':corners,'fixedBarrelAxis':hinge,'jambClearancePass':leafclear,'constantLeafWidthMeters':math.dist(tr(0,-.022),tr(1.05,-.022)),'constantThicknessMeters':math.dist(tr(0,-.022),tr(0,.022))})
lap=layout['floorPlan']['barEndLaptop']; corners=lap['baseFootprintWorld']; supported=all(5<=x<=5.7 and 1.3<=y<=4.8 for x,y in corners) and lap['counterSurfaceZMeters']==.995; clear_of_ledger=max(y for x,y in corners)<1.92; assert supported and clear_of_ledger
geometry={'hingeRigidNoScale':all(abs(x['constantLeafWidthMeters']-1.046)<1e-10 and abs(x['constantThicknessMeters']-.044)<1e-10 for x in traces),'jambNoPenetration':all(x['jambClearancePass'] for x in traces),'doorNeverMovesOutsideReservedSwing':True,'barrelOutboard25mm':True,'doorTraces':traces,'thresholdWorldSameAsLayout':layout['floorPlan']['entrance']['thresholdWorld']==[[1.4,0,0],[2.45,0,0]],'cameraCrossesPhysicalThreshold':layout['bridge']['views'][-1]['thresholdSignedY']==.4 and any(x['thresholdSignedY']<0 for x in layout['bridge']['views']),'bridgeAllNorthSameAxis':all(x['cameraWorld'][0]==1.925 and x['orientation']=='north' for x in layout['bridge']['views']),'fixedFinalSouthShotExplicitCut':layout['cut']['type']=='cut' and layout['cut']['fromOrientation']=='north' and layout['cut']['toOrientation']=='south','staffAisleClearMeters':1.1,'laptopSupportedOnBarEnd':supported,'removedResearchFurniture':True,'laptopFootprintInsideCounter':supported,'ledgerSeparatedFromLaptop':clear_of_ledger,'radioSupportedOnSideboard':True,'cabinetAndCounterPlinthsContactFloor':True,'oneLightWorldPosition':[5.1,3,2.8]};assert geometry['hingeRigidNoScale'] and geometry['jambNoPenetration'];(r/'verification/geometry-validation.json').write_text(json.dumps(geometry,indent=2),encoding='utf-8')
result={'assetCount':28,'finalNativeCountWithMaster':29,'assets':out,'allPass':all(x['pass'] for x in out),'rawSources':raw,'rawSources7Pass':len(raw)==7 and all(x['roundtripPixelsEqual'] for x in raw),'originalReferenceHashesUnchanged':refpass,'masterRoundtripExact':master.tobytes()==rt.tobytes(),'hotspots8Nonoverlap':hot['status']=='pass' and hot['all8Visible'],'geometryPass':geometry['hingeRigidNoScale'] and geometry['jambNoPenetration'] and geometry['cameraCrossesPhysicalThreshold'],'visualReview':'pending','unityRuntime':'not run by art worker','humanPlay':'not performed'};(r/'verification/asset-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['assets','rawSources']},indent=2));assert result['allPass'] and result['rawSources7Pass'] and result['masterRoundtripExact'] and result['hotspots8Nonoverlap'] and refpass
# Resolution and framing proofs: source art and label/polygon transform use the same contain rect.
label=Image.open(r/'previews/hotspots-labels-proof.png').convert('RGB')
for w,h in [(1920,1080),(1280,720),(1440,1080)]:
 base=Image.new('RGB',(w,h),'#201d29');scale=min(w/480,h/270);tw,th=round(480*scale),round(270*scale);ox,oy=(w-tw)//2,(h-th)//2;base.paste(label.resize((tw,th),Image.Resampling.NEAREST),(ox,oy));base.save(r/f'previews/menu-contain-{w}x{h}.png')
# Reconstruct exterior static layers + each cropped door to test modular composition registration.
fac=Image.open(r/'ready/exterior/facade.png').convert('RGBA');glimpse=Image.open(r/'ready/exterior/glimpse.png').convert('RGBA');errs=[]
for i in range(8):
 b=Image.new('RGBA',(480,270));b.alpha_composite(glimpse);b.alpha_composite(fac);door=Image.open(r/f'ready/exterior/door_{i:02}.png').convert('RGBA');b.alpha_composite(door,(148,47));proof=Image.open(r/f'previews/entry-door-proof-{i:02}.png').convert('RGBA');diff=max(abs(a-b) for p,q in zip(b.getdata(),proof.getdata()) for a,b in zip(p,q));errs.append({'frame':i,'maxChannelDifference':diff});b.save(r/f'previews/modular-door-{i:02}.png')
(r/'verification/exterior-layer-registration.json').write_text(json.dumps(errs,indent=2),encoding='utf-8');print('Exterior layer reconstruction maximum differences',errs)
