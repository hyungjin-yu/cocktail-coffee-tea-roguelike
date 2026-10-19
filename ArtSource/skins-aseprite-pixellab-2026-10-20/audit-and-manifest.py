"""Read-only pixel validation and metadata manifests. Never edits image pixels."""
import csv, hashlib, json
from pathlib import Path
from PIL import Image, ImageChops
ROOT=Path(__file__).resolve().parent
contract=json.loads((ROOT/'source-frame-contract.json').read_text(encoding='utf-8-sig'))
DESIGNS=[('rail','skin_bartender_rail','bartender'),('tropic','skin_bartender_tropic','bartender'),('noir','skin_bartender_noir','bartender'),('alchemist','skin_bartender_alchemist','bartender'),('stage','skin_bartender_stage','bartender'),('mountain','skin_tea_mountain','tea_sommelier'),('ceremony','skin_tea_ceremony','tea_sommelier'),('apiary','skin_tea_apiary','tea_sommelier'),('greenhouse','skin_tea_greenhouse','tea_sommelier'),('astronomer','skin_tea_astronomer','tea_sommelier')]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
summary={'status':'source_checks_only_pending_coordinator_visual_review','frameCount':0,'floorPlaneChecks':0,'floorPlaneFailures':[],'failures':[],'skins':[]}
hashes=[]
for key,skinid,job in DESIGNS:
    folder=ROOT/'body'/skinid
    frames=[];errors=[]
    expected=[a for a in contract['jobs'][job]['frames'] if a['runtimeUse']=='runtime']
    if len(list(folder.glob('*.png')))!=len(expected):errors.append('PNG count mismatch')
    if len(list(folder.glob('*.aseprite')))!=len(expected):errors.append('Native count mismatch')
    log=(ROOT/'evidence'/f'{key}-native-readback.log').read_text()
    for a in expected:
        out=folder/a['filename'];native=out.with_suffix('.aseprite')
        src=Image.open(a['source']).convert('RGBA');im=Image.open(out).convert('RGBA')
        check={'sizeExact':im.size==src.size== (256,256),'sourceUnchanged':sha(a['source'])==a['sourceSHA256'],
               'sourceMetaUnchanged':sha(a['sourceMeta'])==a['sourceMetaSHA256'],
               'nativeReopenExact':a['filename']+' NATIVE_EXPORT_READBACK_OK' in log,
               'transparentNonblank':im.getbbox() is not None and 0 in set(im.getchannel('A').getdata()),
               'noCanvasEdgePixels':all(im.getpixel((x,y))[3]==0 for x,y in [(x,y) for x in range(256) for y in (0,255)]+[(x,y) for y in range(256) for x in (0,255)])}
        before=list(src.getdata());after=list(im.getdata())
        changed=sum(p!=q for p,q in zip(before,after));silhouette=sum((p[3]>0)!=(q[3]>0) for p,q in zip(before,after))
        check['substantivePixelChanges']=changed>200
        check['originalFloorPlaneExact']=im.getbbox()[3]==src.getbbox()[3]
        hand_alpha=True;hand_pixels=True
        anchors=a.get('anchors') or {}
        if 'hand' in anchors:
            for hand in ('hand','offHand'):
                if hand not in anchors:continue
                hx,hy=anchors[hand];hy=256-hy
                for y in range(max(0,hy-15),min(256,hy+14)):
                    for x in range(max(0,hx-14),min(256,hx+15)):
                        p,q=src.getpixel((x,y)),im.getpixel((x,y))
                        hand_alpha &= p[3]==q[3]
                        r,g,b,alpha=p
                        is_skin=alpha>0 and r>150 and 80<g<205 and 40<b<165 and r>g*1.18 and g>b*1.1
                        if is_skin:hand_pixels &= p==q
            check['handPatchAlphaExact']=bool(hand_alpha)
            if key not in ('stage','apiary'):check['handSkinPixelsExact']=bool(hand_pixels)
        if '_sword_swing_' in a['filename']:
            check['embeddedWeaponExact']=a['filename']+' NATIVE_EXPORT_READBACK_OK EMBEDDED_WEAPON_EXACT' in log
        fail=[k for k,v in check.items() if not v]
        summary['floorPlaneChecks']+=1
        if not check['originalFloorPlaneExact']:summary['floorPlaneFailures'].append({'skinId':skinid,'filename':a['filename']})
        if key=='rail':check.pop('originalFloorPlaneExact') # Keep the approved rail manifest byte-for-byte stable.
        if fail:errors.append({'filename':a['filename'],'checks':fail})
        rec={**a,'output':str(out),'outputSHA256':sha(out),'native':str(native),'nativeSHA256':sha(native),
             'nativeTag':out.stem,'checks':check,'changedPixels':changed,'silhouetteChangedPixels':silhouette,
             'bbox':im.getbbox(),'alphaValues':sorted(set(im.getchannel('A').getdata()))}
        frames.append(rec);hashes.append([skinid,a['filename'],rec['outputSHA256'],rec['nativeSHA256']])
    manifest={'skinId':skinid,'job':job,'status':'ready_for_coordinator_visual_review' if not errors else 'checks_failed',
              'requiredRuntimeFrameCount':len(expected),'observedSourceFrameCount':contract['jobs'][job]['observedExistingSkinFrames'],
              'unusedObservedFrames':'Recorded only in source-frame-contract.json; coordinator msg_bf0f6c514b74 accepts actual runtime scope',
              'authorship':('PixelLab GUI call1 cap/reference plus native Aseprite garment construction' if key=='rail' else 'Direct Aseprite Lua drawing of exact79 outfit; no independent PixelLab call; authorized msg_aa49f7cc7084'),
              'clips':contract['jobs'][job]['clips'],'swordSwingRuntime':{'frames':'03..07','fps':30,'loop':False} if job=='tea_sommelier' else None,
              'anchorPolicy':'Exact source per-frame anchors and pivots/PPU retained; visual and hand anchors mirror together; no hitbox/stat/weapon transform change',
              'handColorException':'White/cream gloves retain exact original alpha and anchor' if key in ('stage','apiary') else None,
              'comparison':str(ROOT/'evidence'/f'{key}-all-frames.png'),'representativeComparison':str(ROOT/'evidence'/f'{key}-representative-comparison.png'),
              'frames':frames,'failures':errors,'unityIntegrationVerified':False}
    path=ROOT/f'{skinid}-manifest.json';payload=json.dumps(manifest,ensure_ascii=False,indent=2)
    if not path.exists() or path.read_text(encoding='utf-8')!=payload:path.write_text(payload,encoding='utf-8')
    summary['skins'].append({'skinId':skinid,'manifest':str(path),'frameCount':len(frames),'failures':errors})
    summary['frameCount']+=len(frames)
    if errors:summary['failures'].append({'skinId':skinid,'errors':errors})
(ROOT/'audit-results.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
with (ROOT/'final-source-sha256.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.writer(f);w.writerow(['skinId','filename','PNG_SHA256','native_SHA256']);w.writerows(hashes)
print(json.dumps({'frames':summary['frameCount'],'failedSkins':len(summary['failures']),'failures':summary['failures']},ensure_ascii=False))
