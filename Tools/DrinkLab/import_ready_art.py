"""Codex ready manifest(묶음별) → Unity Resources 반입 공용 도구. ArtSource는 읽기만 한다.

사용: python Tools/DrinkLab/import_ready_art.py <ArtSource 패키지 폴더> <manifest 파일명> <보고서 경로>
예:   python Tools/DrinkLab/import_ready_art.py ArtSource/feedback-art-2026-11-17 manifest.americano.ready.json Delivery/Verification/2026-11-17/americano/import-report.json

- manifest: status=ready·ready=true, 자산 목록 = asset_manifests(ready.json 경로) 또는 ready_assets(ID → ready/<ID>.ready.json).
- 자산마다 status/ready 확인, PNG sha256 = ready.json 값일 때만 recommendedUnityPngDestination으로 복사.
  기존 PNG가 있으면 내용만 덮어쓰고 .meta(GUID)는 그대로(기존 참조 유지). 새 자산 .meta는 Unity 임포트가 만든다.
- manifest의 anchors(파일) 항목은 Resources/DrinkLab/anchors.json에 같은 ID 교체·추가(mouth·pourLip·grip·liquid·width·height만, 형식 유지).
- 하나라도 실패하면 아무 파일도 복사하지 않는다(사전 검증 후 일괄 복사).
"""
import hashlib, json, os, shutil, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ANCHORS = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'DrinkLab', 'anchors.json')
CRLF = chr(13) + chr(10)
LF = chr(10)
KEYS = ('pourLip', 'mouth', 'grip', 'liquid', 'width', 'height')


def sha(p):
    return hashlib.sha256(open(p, 'rb').read()).hexdigest()


def load(p):
    return json.load(open(p, encoding='utf-8-sig'))


def main(pkg_rel, manifest_name, report_rel):
    pkg = os.path.join(ROOT, pkg_rel)
    man = load(os.path.join(pkg, manifest_name))
    if man.get('status') != 'ready' or not man.get('ready'):
        raise SystemExit('manifest not ready')
    paths = man.get('asset_manifests') or [f'ready/{a}.ready.json' for a in man.get('ready_assets', [])]
    plan, errors = [], []
    for rel in paths:
        a = load(os.path.join(pkg, rel))
        src = os.path.join(pkg, a['png'])
        dest = os.path.join(ROOT, a['recommendedUnityPngDestination'])
        if a.get('status') != 'ready' or not a.get('ready'):
            errors.append(f"{a['assetId']}: not ready"); continue
        got = sha(src)
        if got != a['sha256']['png']:
            errors.append(f"{a['assetId']}: sha mismatch {got}"); continue
        plan.append((a, src, dest, got))
    if errors:
        for e in errors: print('  ', e)
        raise SystemExit(f'abort: {len(errors)} errors, nothing copied')

    rows = []
    for a, src, dest, got in plan:
        meta = dest + '.meta'
        guid = None
        if os.path.exists(meta):
            for line in open(meta, encoding='utf-8'):
                if line.startswith('guid:'): guid = line.split(':', 1)[1].strip()
        prev = sha(dest) if os.path.exists(dest) else None
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(src, dest)
        rows.append({'assetId': a['assetId'], 'kind': a.get('kind'), 'dest': a['recommendedUnityPngDestination'], 'dimensions': a['dimensions'],
                     'pngSha256': got, 'previousSha256': prev if prev != got else (prev and 'same'), 'keptGuid': guid, 'ingredientId': a.get('ingredientId')})

    added = []
    if man.get('anchors'):
        handoff = load(os.path.join(pkg, man['anchors']))
        table = json.loads(open(ANCHORS, 'rb').read().decode('utf-8-sig'))
        ids = {e['assetId'] for e in handoff['entries']}
        entries = [e for e in table['entries'] if e['assetId'] not in ids]
        for e in handoff['entries']:
            entries.append({'assetId': e['assetId'], **{k: e[k] for k in KEYS if k in e}})
            added.append(e['assetId'])
        table['entries'] = entries
        open(ANCHORS, 'wb').write(json.dumps(table, ensure_ascii=False, indent=4).replace(LF, CRLF).encode('utf-8'))

    report = os.path.join(ROOT, report_rel)
    os.makedirs(os.path.dirname(report), exist_ok=True)
    json.dump({'manifest': f'{pkg_rel}/{manifest_name}', 'imported': len(rows), 'anchors': added, 'assets': rows},
              open(report, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    print(f'imported {len(rows)} / {len(paths)}; anchors {added}')
    return 0


if __name__ == '__main__':
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    sys.exit(main(sys.argv[1], sys.argv[2], sys.argv[3]))
