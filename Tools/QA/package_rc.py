"""Package a fixed release, recording each archived file and verifying extraction."""
import csv, hashlib, json, shutil, sys, zipfile
from pathlib import Path
build, delivery, extract = map(Path, sys.argv[1:4])
stage = delivery / 'RC-Package-1.0.0'
archive = delivery / 'CocktailCoffeeTea_1.0.0_Windows64.zip'
if stage.exists() or archive.exists() or extract.exists():
    raise SystemExit('Refusing to replace an existing package/extraction')
assert (build / 'CocktailCoffeeTea.exe').is_file()
shutil.copytree(build, stage, ignore=shutil.ignore_patterns('*BackUpThisFolder_ButDontShipItWithYourGame*','*_DoNotShip','*.pdb','*.mdb'))
for name in ['README_실행방법.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md']:
    shutil.copy2(delivery / name, stage / name)
shutil.copytree(delivery / 'Licenses', stage / 'Licenses')
font = delivery / 'Licenses/LICENSE_NotoSansCJK.txt'
if font.exists(): shutil.copy2(font, stage / font.name)
rows = []
for p in sorted(stage.rglob('*')):
    if p.is_file(): rows.append(dict(path=p.relative_to(stage).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
with (stage / 'SHA256SUMS.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.DictWriter(f,fieldnames=['path','bytes','sha256']);w.writeheader();w.writerows(rows)
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): z.write(p,p.relative_to(stage).as_posix())
extract.mkdir(parents=True)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    z.extractall(extract)
for r in rows:
    assert hashlib.sha256((extract/r['path']).read_bytes()).hexdigest()==r['sha256'],r['path']
proof=dict(archive=str(archive.resolve()),archiveBytes=archive.stat().st_size,
           archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),extractedPath=str(extract.resolve()),
           verifiedFiles=len(rows),allExtractedHashesMatch=True,zipCrcPassed=True)
(delivery/'Reports/package-extraction.json').write_text(json.dumps(proof,indent=2,ensure_ascii=False),encoding='utf-8')
(delivery/'CocktailCoffeeTea_1.0.0_Windows64.zip.sha256').write_text(proof['archiveSha256']+'  '+archive.name+'\n',encoding='ascii')
print(json.dumps(proof,indent=2,ensure_ascii=False))
