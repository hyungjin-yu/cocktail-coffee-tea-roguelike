"""67 run history: package the strict Windows player and write reproducible delivery evidence.
usage: python Tools/RunHistory/package.py   (run from anywhere; paths are relative to the project root)"""
from pathlib import Path
import datetime, hashlib, json, shutil, zipfile, csv
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
out = root / 'Delivery/RunHistory-2026-10-16'
build = root / 'Builds/Playable-2026-10-16-run-history'
archive = root / 'Builds/CocktailCoffeeTea-RunHistory-2026-10-16-win64.zip'
prefix = 'CocktailCoffeeTea-RunHistory-2026-10-16/'
scratch = Path('C:/UnityScratch/rh1016')


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


# ── ZIP: every shipped file except DoNotShip debug output, CRC + byte hash comparison ──
files = [p for p in sorted(build.rglob('*')) if p.is_file() and not any('DoNotShip' in part for part in p.parts)]
assert (build / 'CocktailCoffeeTea.exe') in files
manifest = [{'path': p.relative_to(build).as_posix(), 'bytes': p.stat().st_size, 'sha256': sha(p)} for p in files]
if not archive.exists():
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for p in files:
            z.write(p, prefix + p.relative_to(build).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None, 'CRC'
    names = z.namelist()
    assert len(names) == len(files), (len(names), len(files))
    assert not any('DoNotShip' in n for n in names)
    for m in manifest:
        assert hashlib.sha256(z.read(prefix + m['path'])).hexdigest() == m['sha256'], m['path']
proof = {
    'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'exeSha256': sha(build / 'CocktailCoffeeTea.exe'),
    'runtimeDllSha256': sha(build / 'CocktailCoffeeTea_Data/Managed/CCT.Runtime.dll'),
    'zip': archive.relative_to(root).as_posix(), 'zipSha256': sha(archive), 'zipBytes': archive.stat().st_size,
    'fileCount': len(files), 'crcOk': True, 'zipMatchesBuild': True, 'doNotShipExcluded': True, 'files': manifest,
}
(out / 'package-proof.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')

# ── test XML copies + summary ──
results = scratch / 'results'
(out / 'tests').mkdir(exist_ok=True)
tests = {}
for name in ['edit-rh1', 'play-rh1', 'edit-rh2', 'play-rh2', 'edit-full1', 'play-full1', 'edit-final', 'play-final']:
    x = results / f'{name}.xml'
    if not x.exists():
        continue
    shutil.copy2(x, out / 'tests' / x.name)
    for ext in ['.log', '.progress.txt']:
        if (results / f'{name}{ext}').exists():
            shutil.copy2(results / f'{name}{ext}', out / 'tests' / f'{name}{ext}')
    r = ET.parse(x).getroot()
    tests[name] = {k: r.attrib.get(k) for k in ['result', 'total', 'passed', 'failed', 'skipped']}
(out / 'test-summary.json').write_text(json.dumps(tests, indent=2), encoding='utf-8')

# ── screenshots + capture json ──
captures = {}
for mode in ['720', '1080', 'touch']:
    src = scratch / 'shots' / mode
    dst = out / 'screenshots' / mode
    dst.mkdir(parents=True, exist_ok=True)
    for p in src.iterdir():
        if p.suffix in ('.png', '.json', '.log'):
            shutil.copy2(p, dst / p.name)
    c = json.loads((src / 'capture-result.json').read_text(encoding='utf-8'))
    assert c['overflowCount'] == 0 and not c['error'] and c['historyRows'] == 20, mode
    captures[mode] = {k: c[k] for k in ['width', 'height', 'touch', 'overflowCount', 'minFontPx', 'historyRows', 'preexisting']}
    hist = scratch / f'qa-data-{mode}' / 'run-history.json'
    if hist.exists():
        shutil.copy2(hist, dst / 'isolated-run-history.json')
(out / 'capture-summary.json').write_text(json.dumps(captures, indent=2, ensure_ascii=False), encoding='utf-8')

# ── build logs and type proof ──
for name in ['production-build.log', 'qa-build3.log']:
    if (results / name).exists():
        shutil.copy2(results / name, out / name)
types = json.loads((out / 'production-types.json').read_text(encoding='utf-8'))
assert types['passed']

# ── final source == build input snapshot ──
snap = {}
with (out / 'source-snapshot/source-files.csv').open(encoding='utf-8-sig') as f:
    for row in csv.DictReader(f):
        snap[row['path']] = row['sha256']
mismatch = [p for p, h in snap.items() if not (root / p).exists() or sha(root / p) != h]
current = [p.relative_to(root).as_posix() for folder in ['Assets', 'Packages', 'ProjectSettings'] for p in (root / folder).rglob('*') if p.is_file()]
added = sorted(set(current) - set(snap))
(out / 'source-snapshot/current-match.json').write_text(json.dumps({'snapshotFiles': len(snap), 'mismatched': mismatch, 'addedSince': added}, indent=2), encoding='utf-8')
assert not mismatch and not added, (mismatch[:5], added[:5])

# ── preservation baseline from the coordinator ──
base = json.loads((out / 'preservation-baseline.json').read_text(encoding='utf-8-sig'))
pres = [{'path': b['path'], 'expected': b['sha256'].lower(), 'actual': sha(root / b['path']), 'match': sha(root / b['path']) == b['sha256'].lower()} for b in base]
(out / 'preservation-check.json').write_text(json.dumps(pres, indent=2), encoding='utf-8')
assert all(p['match'] for p in pres)
print(json.dumps({'zip': proof['zipSha256'], 'exe': proof['exeSha256'], 'files': len(files), 'tests': tests, 'captures': {k: v['overflowCount'] for k, v in captures.items()}, 'preservation': all(p['match'] for p in pres)}, indent=1))
