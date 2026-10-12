import hashlib, json, shutil, zipfile
from pathlib import Path

project = Path(__file__).resolve().parents[2]
build = project / 'Builds/Windows64'
target = project / 'Builds/Playable-2026-10-12-ui-final'
archive = project / 'Delivery/CocktailCoffeeTea_1.0.0_ui-final-2026-10-12.zip'
if target.exists() or archive.exists():
    raise SystemExit('Versioned output already exists; refusing replacement')
shutil.copytree(build, target, ignore=shutil.ignore_patterns('*BackUpThisFolder_ButDontShipItWithYourGame*', '*_DoNotShip', '*.pdb', '*.mdb'))
delivery = project / 'Delivery'
for filename in ['README_실행방법.md', 'CHANGELOG.md', 'THIRD_PARTY_NOTICES.md']:
    if (delivery / filename).exists():
        shutil.copy2(delivery / filename, target / filename)
shutil.copytree(delivery / 'Licenses', target / 'Licenses')
rows = {p.relative_to(target).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in sorted(target.rglob('*')) if p.is_file()}
(target / 'manifest-ui-final.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for p in sorted(target.rglob('*')):
        if p.is_file():
            z.write(p, p.relative_to(target).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    for name, expected in rows.items():
        assert hashlib.sha256(z.read(name)).hexdigest() == expected, name
proof = dict(build=str(target), zip=str(archive), files=len(rows),
             zipSha256=hashlib.sha256(archive.read_bytes()).hexdigest(), verified=True)
(Path(__file__).parent / 'package-proof.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps(proof))






