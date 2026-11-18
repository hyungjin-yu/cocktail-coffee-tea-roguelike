import pathlib,json,os,hashlib,shutil
out=pathlib.Path('ArtSource/feedback-art-2026-11-16'); hist=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab/generation_history'; specs=json.loads((out/'provenance/batch-native-specs.json').read_text(encoding='utf-8')); rec=[]
for s in specs:
 dirs=sorted(hist.glob('* - Feedback art - '+s['id']+' - *'))
 files=[d/'0.png' for d in dirs if (d/'0.png').exists()]
 if not files: continue
 f=files[-1]; dst=out/'raw'/(s['id']+'.png')
 if not dst.exists(): shutil.copy2(f,dst)
 rec.append({'assetId':s['id'],'officialArchivePng':str(f),'rawPng':str(dst),'sha256':hashlib.sha256(dst.read_bytes()).hexdigest(),'nativeModel':'installed official generate_no_style.generate() in Aseprite 1.3.18.6 / PixelLab 0.4.131','status':'archived_generated_raw_not_ready'})
(out/'provenance/batch-native-receipts.json').write_text(json.dumps(rec,indent=2),encoding='utf-8');print('Collected',len(rec),'of',len(specs),'native generated raw images')
