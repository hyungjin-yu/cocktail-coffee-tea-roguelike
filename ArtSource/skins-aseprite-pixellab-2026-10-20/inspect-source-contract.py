"""Read-only source metadata inspection; never creates or edits image pixels."""
import hashlib, json, re, struct
from pathlib import Path

ROOT = Path('C:/Users/user/Documents/Projects/CocktailCoffeeTea')
OUT = Path(__file__).resolve().parent
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
catalog = (ROOT/'Assets/_Project/Resources/ArtV1/ArtCatalog.asset').read_text(encoding='utf-8-sig')
result = {'status':'source_contract_only_not_ready', 'generatedSkinCount':0,
          'runtimeEvidence':'Static current ArtCatalog GUIDs plus PlayerVisual.SwordSwingFrames, not a Unity execution',
          'anchorCoordinates':'Source manifests use bottom-left pixel origin; preserve hand/grip and mirror with visual',
          'jobs':{}}
for job, old in [('bartender','skin_bartender_midnight'),('tea_sommelier','skin_tea_jade')]:
    base=ROOT/f'Assets/_Project/Art/v1/{job}'
    manifest=load(base/'manifest.json')
    section=catalog.split('  - assetId: '+job+'\n',1)[1].split('  - assetId:',1)[0]
    runtime_guids=set(re.findall(r'fileID: 21300000, guid: ([a-f0-9]+)',section))
    existing=ROOT/f'Assets/_Project/Resources/CosmeticSkins/{old}'
    records=[]
    for oldpng in sorted(existing.glob('*.png')):
        name=oldpng.name
        if name.startswith('bow_'):
            src=ROOT/'Assets/_Project/Resources/ArtExtra/tea_sommelier_bow_ready'/name
            use='historical_bow_not_played_since_2026-10-13'
            anchor=None
        elif '_sword_swing_' in name:
            src=ROOT/'Assets/_Project/Resources/ArtExtra/tea_sommelier_sword_swing'/name
            use='runtime' if int(src.stem[-2:])>=3 else 'sword_pre_roll_not_currently_played'
            anchor={'policy':'Embedded original sword unchanged; separate held weapon hidden; preserve source pivot'}
        else:
            src=base/'frames'/name
            use='runtime'
            anchor=manifest.get('anchorsByFrame',{}).get('frames/'+name)
        data=src.read_bytes(); width,height=struct.unpack('>II',data[16:24])
        meta=Path(str(src)+'.meta'); text=meta.read_text(encoding='utf-8-sig')
        guid=re.search(r'^guid: (\w+)',text,re.M).group(1)
        if '_sword_swing_' not in name and not name.startswith('bow_'):
            assert guid in runtime_guids, (job,name,'not in runtime catalog')
        pivot=re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}',text)
        ppu=re.search(r'spritePixelsToUnits: ([\d.]+)',text)
        records.append({'filename':name,'source':str(src),'sourceSHA256':sha(src),
                        'sourceMeta':str(meta),'sourceMetaSHA256':sha(meta),'sourceGUID':guid,
                        'existingSkinReference':str(oldpng),'existingSkinSHA256':sha(oldpng),
                        'sizePx':[width,height],'pivotNormalized':[float(x) for x in pivot.groups()],
                        'pixelsPerUnit':float(ppu.group(1)) if ppu else None,
                        'runtimeUse':use,'anchors':anchor})
    result['jobs'][job]={'observedExistingSkinFrames':len(records),
                         'currentlyUsedFrames':sum(r['runtimeUse']=='runtime' for r in records),
                         'clips':manifest['clips'],'sourceManifest':str(base/'manifest.json'),
                         'sourceManifestSHA256':sha(base/'manifest.json'),'frames':records}
target=OUT/'source-frame-contract.json'
target.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({j:{k:v for k,v in d.items() if k in ('observedExistingSkinFrames','currentlyUsedFrames')} for j,d in result['jobs'].items()}))
