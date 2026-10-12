"""Stage reviewed sprite bytes and manifests; never alter original ArtSource/v1."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CANDIDATES = Path('C:/Users/user/Documents/Personal_Note/03_프로젝트/칵테일 커피 티 로그라이크 기획/ArtProduction/final-candidates-2026-10-06')
DERIVED = ROOT / 'ArtSource/v1-final-candidates-2026-10-06'
SETS = {
    'bartender': ['d4_bartender_idle', 'd1_bartender_walk'],
    'tea_sommelier': ['d3_tea_idle', 'd2_move_gap_fill/tea_sommelier'],
}

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

records = []
for actor, batches in SETS.items():
    original = ROOT / 'ArtSource/v1' / actor
    destination = DERIVED / actor
    if destination.exists():
        raise RuntimeError(f'Refusing to overwrite an existing staged source: {destination}')
    parent_path = original / 'manifest.json'
    parent_hash = sha(parent_path)
    manifest = read(parent_path)
    shutil.copytree(original, destination)
    provenance = []
    for batch in batches:
        folder = CANDIDATES / batch
        candidate = read(folder / 'candidate-manifest.json')
        assert candidate['parentManifestSha256'] == parent_hash, 'Parent changed since candidate review'
        for frame in candidate['frames']:
            relative = frame['file']
            assert sha(folder / relative) == frame['sha256'], f'Candidate checksum mismatch: {relative}'
            shutil.copy2(folder / relative, destination / relative)
        manifest['clips'].update(candidate['clips'])
        manifest['anchorsByFrame'].update(candidate['anchorsByFrame'])
        provenance.append({'candidateId': candidate['candidateId'], 'manifest': str(folder / 'candidate-manifest.json'),
                           'sha256': sha(folder / 'candidate-manifest.json')})
    manifest['status'] = 'needs_fix'
    manifest['acceptance'] = {'coordinatorAcceptedForStagedIntegration': True, 'strictReleaseReady': False,
                              'userApproved': False, 'unityVerified': False, 'runtimeVisualReview': 'pending'}
    manifest['finalCandidateProvenance'] = {'parentManifestSha256': parent_hash, 'batches': provenance,
                                          'note': 'Reviewed candidates staged for native runtime QA; original sources preserved.'}
    manifest.setdefault('deviations', []).append('Final coordinator decision: idle 4 frames at 3fps; feet fixed.')
    (destination / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    assert sha(parent_path) == parent_hash
    records.append({'assetId': actor, 'source': str(destination), 'parentManifestSha256': parent_hash,
                    'candidateManifestSha256': sha(destination / 'manifest.json')})

config_path = ROOT / 'Tools/Art/firstpass-sources.json'
config = read(config_path)
archive = ROOT / 'Delivery/logs/final-art-stage-config-before.json'
if archive.exists():
    raise RuntimeError('Configuration archive already exists; inspect before restaging')
shutil.copy2(config_path, archive)
config['provisional'] = [x for x in config['provisional'] if x['assetId'] not in SETS]
for actor in SETS:
    config['provisional'].append({'assetId': actor, 'source': f'ArtSource/v1-final-candidates-2026-10-06/{actor}',
        'clips': [], 'overrideReadyForReview': True,
        'reason': '2026-10-06 coordinator-reviewed final candidates; runtime QA pending, strict release forbidden.'})
config_path.write_text(json.dumps(config, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(ROOT / 'Delivery/logs/final-art-stage-provenance.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(records, ensure_ascii=False))
