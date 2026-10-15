"""Generation provenance, immutable source and material inspection, metadata only."""
import json,hashlib,csv,shutil
from pathlib import Path
from PIL import Image
root=Path(__file__).parent
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
manifest=json.loads((root/'asset_mapping_manifest.json').read_text(encoding='utf8'))
old=json.loads((root.parent/'skins-2026-10-13/v2/asset_mapping_manifest.json').read_text(encoding='utf8'))
gold={(111,80,41),(167,137,70),(222,192,103),(234,221,179)}
steel={(15,25,57),(34,57,92),(55,87,124),(74,119,176)}
fail=[]
for a,previous in zip(manifest['assets'],old['assets']):
 a['originalSourceSHAUnchanged']=a['sourceSHA256']==previous['sourceSHA256']
 if not a['originalSourceSHAUnchanged']: fail.append(('sourceSHA',a['original']))
 a['sourceMetaSHA256']=sha(a['original']+'.meta') if Path(a['original']+'.meta').exists() else None
 a['readbackSHA256']=sha(Path(a['output']).with_suffix('.readback.png'))
 a['anchorPolicy']='Original pixel coordinates and source pivot/PPU/.meta retained; no geometry transformations'
 if a['kind']=='embedded_sword':
  im=Image.open(a['output']).convert('RGBA');rm=Image.open(a['rim']).convert('L')
  colors={c[:3] for c,m in zip(im.get_flattened_data(),rm.get_flattened_data()) if m and c[3]}
  a['goldRimGuardPaletteExact']=colors<=gold
  if not a['goldRimGuardPaletteExact']:fail.append(('rimPalette',a['output'],sorted(colors-gold)))
manifest['materials']=dict(goldRimGuard=[list(c) for c in sorted(gold)],blueSteel=[list(c) for c in sorted(steel)],source='Actual PixelLab output material colors used by Aseprite Lua; uniform ramp across held and swing')
manifest['validationLimit']='Source verification only; Unity integration and runtime testing belong to Claude'
(root/'asset_mapping_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
bodySource=manifest['assets'][0]['original']
shutil.copyfile(bodySource,root/'evidence/bartender-original-input.png')
prompts=[
('bartender',145336185.0,'bartender-pixellab-raw.aseprite','bartender-original-input.png','bartender-pixellab-output.png','Recolor only the vest and trousers to vivid deep navy blue with clear cobalt highlights, and the teal necktie to warm metallic gold. Preserve face, hair, skin, white shirt, shoes, pose, silhouette, pixel scale and transparent background exactly. Elegant midnight bartender uniform, no new accessories.'),
('weapons-four-designs',704154967.0,'weapons-pixellab-generated.aseprite','weapons-original-input.png','weapons-pixellab-output.png','Recolor these four separate pixel weapons, preserve every original shape and placement. Top left rapier: amethyst purple blade with gold rim and guard. Top right gun: copper barrel, brass metal and small violet details. Bottom left sword: blue steel blade with gold rim and guard. Bottom right bow: midnight blue limbs with moonlight blue highlights and gold tips, preserve ivory string. Transparent background, no characters, no extra objects, crisp pixel art.')]
calls=[]
for name,seed,native,inp,out,prompt in prompts:
 im=Image.open(root/'evidence'/out).convert('RGBA')
 calls.append(dict(name=name,seed=seed,mode='Edit image (Pro)',method='Edit with text',input=str(root/'evidence'/inp),inputSHA256=sha(root/'evidence'/inp),output=str(root/'evidence'/out),outputSHA256=sha(root/'evidence'/out),rawNative=str(root/native),rawNativeSHA256=sha(root/native),inputNativeFrame=1,generatedNativeFrame=2,prompt=prompt,outputMethod='New frame',removeBackground=True,outputCanvas=list(im.size),outputNonzeroAlphaPixels=sum(c[3]>0 for c in im.get_flattened_data()),successEvidence=str(root/'evidence'/('bartender-finished.png' if name=='bartender' else 'weapons-finished.png'))))
(root/'generation-log.json').write_text(json.dumps(dict(actualCalls=2,designs=5,retries=0,otherGenerationTools=0,bulkAnimationGeneration=0,creditDebit='unknown; UI stated 10–25 generations per call, no measured debit',calls=calls,saveExportCorrection='First weapon export came from unsaved reference native; GUI generated result was preserved, saved with verified filename and re-exported from frame2 without additional calls',accountSettingsReadOrChanged=False),ensure_ascii=False,indent=2),encoding='utf8')
with (root/'final-source-sha256.csv').open('w',encoding='utf8',newline='') as f:
 w=csv.writer(f);w.writerow(['runtimePath','PNG','PNG_SHA256','native','native_SHA256'])
 for a in manifest['assets']:w.writerow([a['recommendedResourcesPath'],a['output'],a['outputSHA256'],a['nativeFile'],a['nativeSHA256']])
(root/'material-source-audit.json').write_text(json.dumps(dict(sourceCount=43,sourceSHAUnchanged=all(a['originalSourceSHAUnchanged'] for a in manifest['assets']),goldRimGuardPaletteExact=not fail,failures=fail),indent=2),encoding='utf8')
print('FINAL_METADATA',len(manifest['assets']),'source/material failures:',fail)
if fail:raise SystemExit(1)
