"""Inspect actual ECMA-335 TypeDef metadata, rather than binary text matches."""
import sys, json, hashlib
from pathlib import Path
import dnfile
folder, output = map(Path, sys.argv[1:3])
forbidden = {'CCT.AutoTour','CCT.PlayVerifier','CCT.LoadScenario','CCT.QaAudioTap','CCT.CardEducationCapture','CCT.RunHistoryCapture'}
found, inventory = [], []
for dll in sorted(folder.glob('*.dll')):
    pe = dnfile.dnPE(str(dll))
    if not pe.net: continue
    types = [str(r.TypeNamespace)+'.'+str(r.TypeName) for r in pe.net.mdtables.TypeDef]
    bad = [t for t in types if t in forbidden or t.startswith('CCT.Tests.') or t.startswith('CCT.Editor.')]
    found += bad
    inventory.append(dict(file=dll.name, sha256=hashlib.sha256(dll.read_bytes()).hexdigest(), typeCount=len(types), forbiddenTypes=bad))
assert any(r['file']=='CCT.Runtime.dll' for r in inventory)
output.write_text(json.dumps(dict(passed=not found, forbidden=sorted(forbidden), assemblies=inventory),indent=2),encoding='utf-8')
print(f'Metadata: {len(inventory)} assemblies; forbidden types={found}')
if found: sys.exit(1)
