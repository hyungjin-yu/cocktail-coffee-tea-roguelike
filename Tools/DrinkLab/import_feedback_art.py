"""2026-11-16 피드백 최종 아트 36개(Codex ready manifest) → Unity Resources 반입. ArtSource는 읽기만 한다.

- 원본: ArtSource/feedback-art-2026-11-16/manifest.ready.json + ready/<assetId>.ready.json + ready/anchors-handoff.json
- 각 자산: status=ready·ready=true, PNG sha256 = manifest 값일 때만 recommendedUnityPngDestination으로 복사.
  기존 PNG가 있으면 내용만 덮어쓰고 .meta(GUID)는 그대로 둔다(기존 참조 유지). 새 자산의 .meta는 Unity 임포트가 만든다.
  이전 그림의 sha는 origin-inventory.json(pngSha256)에서 기록(재실행해도 원래 값 유지).
- 잔 3종: Resources/DrinkLab/anchors.json에 mouth·grip·liquid·width·height 항목(anchors-handoff, 0~1 왼쪽 아래 원점)을 넣는다.
  파일 형식은 기존 그대로(4칸 들여쓰기·CRLF·끝 줄바꿈 없음).
- 결과 기록: Delivery/Verification/2026-11-16/art-import/import-report.json
사용: python Tools/DrinkLab/import_feedback_art.py
"""
import hashlib, json, os, shutil, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PKG = os.path.join(ROOT, 'ArtSource', 'feedback-art-2026-11-16')
ANCHORS = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'DrinkLab', 'anchors.json')
REPORT = os.path.join(ROOT, 'Delivery', 'Verification', '2026-11-16', 'art-import', 'import-report.json')
CRLF = chr(13) + chr(10)
LF = chr(10)


def sha(p):
    return hashlib.sha256(open(p, 'rb').read()).hexdigest()


def main():
    man = json.load(open(os.path.join(PKG, 'manifest.ready.json'), encoding='utf-8-sig'))
    if man.get('status') != 'ready' or not man.get('ready'):
        raise SystemExit('manifest not ready')
    inv = json.load(open(os.path.join(PKG, 'origin-inventory.json'), encoding='utf-8-sig'))
    original = {x['assetId']: x.get('pngSha256') for x in inv['assets'] if x.get('existingPng')}
    rows, errors = [], []
    for aid in man['ready_assets']:
        a = json.load(open(os.path.join(PKG, 'ready', aid + '.ready.json'), encoding='utf-8-sig'))
        src = os.path.join(PKG, a['png'])
        dest = os.path.join(ROOT, a['recommendedUnityPngDestination'])
        row = {'assetId': aid, 'kind': a.get('kind'), 'dest': a['recommendedUnityPngDestination'], 'dimensions': a['dimensions']}
        if a.get('status') != 'ready' or not a.get('ready'):
            errors.append(f'{aid}: not ready'); continue
        got = sha(src)
        if got != a['sha256']['png']:
            errors.append(f'{aid}: sha mismatch {got} != {a["sha256"]["png"]}'); continue
        meta = dest + '.meta'
        guid = None
        if os.path.exists(meta):
            for line in open(meta, encoding='utf-8'):
                if line.startswith('guid:'): guid = line.split(':', 1)[1].strip()
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(src, dest)
        row.update({'pngSha256': got, 'replacedExisting': aid in original, 'previousSha256': original.get(aid), 'keptGuid': guid,
                    'ingredientId': a.get('ingredientId')})
        rows.append(row)

    handoff = json.load(open(os.path.join(PKG, 'ready', 'anchors-handoff.json'), encoding='utf-8-sig'))
    table = json.loads(open(ANCHORS, 'rb').read().decode('utf-8-sig'))
    ids = {e['assetId'] for e in handoff['entries']}
    entries = [e for e in table['entries'] if e['assetId'] not in ids]
    for e in handoff['entries']:
        a = json.load(open(os.path.join(PKG, 'ready', e['assetId'] + '.ready.json'), encoding='utf-8-sig'))
        w, h = a['dimensions']
        entries.append({'assetId': e['assetId'], 'mouth': e['mouth'], 'grip': e['grip'], 'liquid': e['liquid'], 'width': w, 'height': h})
    table['entries'] = entries
    text = json.dumps(table, ensure_ascii=False, indent=4).replace(LF, CRLF)
    open(ANCHORS, 'wb').write(text.encode('utf-8'))

    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    json.dump({'manifest': 'ArtSource/feedback-art-2026-11-16/manifest.ready.json', 'imported': len(rows), 'errors': errors,
               'anchorsAdded': sorted(ids), 'assets': rows}, open(REPORT, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    print(f'imported {len(rows)} / {len(man["ready_assets"])}; replaced existing {sum(r["replacedExisting"] for r in rows)}; errors {len(errors)}')
    for e in errors: print('  ', e)
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
