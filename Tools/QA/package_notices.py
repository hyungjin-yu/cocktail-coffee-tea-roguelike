"""Copy unmodified package notices for assemblies actually bundled in a player."""
import sys, json, csv, shutil, hashlib
from pathlib import Path
cache, managed, out = map(Path, sys.argv[1:4])
out.mkdir(parents=True, exist_ok=True)
dlls = {p.stem for p in managed.glob('*.dll')}
rows = []
for package in sorted(cache.iterdir()):
    if not package.is_dir(): continue
    info = package / 'package.json'
    if not info.exists(): continue
    metadata = json.loads(info.read_text(encoding='utf-8-sig'))
    found = []
    for asm in package.rglob('*.asmdef'):
        try: name = json.loads(asm.read_text(encoding='utf-8-sig')).get('name')
        except (ValueError, OSError): continue
        if name in dlls: found.append(name + '.dll')
    if not found: continue
    target = out / metadata['name']
    target.mkdir(exist_ok=True)
    copied = []
    for p in package.rglob('*'):
        if not p.is_file() or p.suffix == '.meta': continue
        name = p.name.lower()
        if name.startswith(('license', 'third party notices', 'third_party_notices', 'notice')):
            rel = p.relative_to(package)
            dest = target / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, dest)
            copied.append(str(rel))
    # Mathematics Noise carries its third-party copyright/license in source headers.
    if metadata['name'] == 'com.unity.mathematics':
        for p in package.rglob('Noise/*.cs'):
            lines = p.read_text(encoding='utf-8-sig').splitlines()
            header = []
            for line in lines:
                if line.startswith('//') or not line.strip(): header.append(line)
                else: break
            dest = target / ('Noise-' + p.stem + '-copyright.txt')
            dest.write_text('\n'.join(header) + '\n', encoding='utf-8')
            copied.append(dest.name)
    rows.append(dict(package=metadata['name'], version=metadata.get('version'), assemblies=';'.join(sorted(set(found))), notices=';'.join(sorted(copied))))
with (out / 'package-inventory.csv').open('w', newline='', encoding='utf-8-sig') as f:
    w = csv.DictWriter(f, fieldnames=['package','version','assemblies','notices']); w.writeheader(); w.writerows(rows)
print(f'{len(rows)} packages with matching bundled assemblies; original notices copied to {out}')
