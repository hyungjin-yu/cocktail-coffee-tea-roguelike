"""QA: 아트 검수 요청 목록(minimum-additional-body-qa.csv 122 / minimum-additional-common-qa.csv 88)을 QA 투어 CSV와 대조한다.
- body: frames.csv의 sprite(플레이어 motion·bestiary) 또는 bossSprite(보스 녹화), facing(-1=왼쪽/1=오른쪽)으로 요청 방향(L/R/L-R) 충족 여부.
- common: sprites.csv / shots-sprites.csv(실제 SpriteRenderer·UI Image가 그린 스프라이트, 정렬·불투명도)에서 정확한 스프라이트 이름.
사용: python -I codex_list_coverage.py <body.csv> <common.csv> <out_dir> <run dir> [<run dir> ...]
사람/아트 담당의 시각 승인을 대신하지 않는다."""
import csv, sys, collections
from pathlib import Path

body_csv, common_csv, out_dir = sys.argv[1], sys.argv[2], Path(sys.argv[3])
runs = [Path(r) for r in sys.argv[4:]]
out_dir.mkdir(parents=True, exist_ok=True)

seen_body = collections.defaultdict(list)    # stem -> [(run, clip, frame, gameTime, facing)]
seen_common = collections.defaultdict(list)  # stem -> [(run, clip/shot, frame, order, alpha)]
for run in runs:
    vid = run / 'out' / 'video'
    for fcsv in vid.glob('*/frames.csv'):
        for r in csv.DictReader(fcsv.open(encoding='utf-8-sig')):
            name = r.get('sprite') or r.get('bossSprite')
            fac = r.get('facing') or r.get('bossFacing') or ''
            if name:
                seen_body[name].append((run.name, fcsv.parent.name, r['frame'], r['gameTime'], fac))
    for scsv in list(vid.glob('*/sprites.csv')) + [run / 'out' / 'shots-sprites.csv']:
        if not scsv.exists():
            continue
        clip = scsv.parent.name if scsv.name == 'sprites.csv' else 'shots'
        for line in scsv.open(encoding='utf-8-sig'):
            if line.startswith('#') or line.startswith('frame,'):
                continue
            p = line.rstrip('\n').split(',')
            if len(p) < 7:
                continue
            seen_common[p[2]].append((run.name, clip, p[0], p[5], p[6], p[1]))

def facing_ok(req, facings):
    need = {'L': {'-1'}, 'R': {'1'}, 'L/R': {'-1', '1'}}.get(req.strip(), {'-1', '1'})
    return need <= facings

rows = []
for r in csv.DictReader(open(body_csv, encoding='utf-8-sig')):
    stem = Path(r['frame']).stem
    hits = seen_body.get(stem, [])
    facings = {h[4] for h in hits}
    rows.append({'assetId': r['assetId'], 'clip': r['clip'], 'frame': r['frame'], 'requiredFacing': r.get('requiredFacing', ''),
                 'runtimeRows': len(hits), 'facings': ';'.join(sorted(facings)), 'facingSatisfied': facing_ok(r.get('requiredFacing', 'L/R'), facings),
                 'firstLeft': next(('/'.join(h[:4]) for h in hits if h[4] == '-1'), ''),
                 'firstRight': next(('/'.join(h[:4]) for h in hits if h[4] == '1'), '')})
with open(out_dir / 'body-122-coverage.csv', 'w', newline='', encoding='utf-8-sig') as fh:
    w = csv.DictWriter(fh, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
ok = sum(r['facingSatisfied'] for r in rows)
print(f'body: {ok}/{len(rows)} satisfied')
for r in rows:
    if not r['facingSatisfied']:
        print('  MISSING', r['assetId'], r['clip'], r['frame'], 'need', r['requiredFacing'], 'have', r['facings'])

crow = []
for r in csv.DictReader(open(common_csv, encoding='utf-8-sig')):
    stem = Path(r['frame']).stem
    hits = seen_common.get(stem, [])
    crow.append({'assetId': r['assetId'], 'clip': r['clip'], 'frame': r['frame'], 'runtimeRows': len(hits),
                 'kinds': ';'.join(sorted({h[5] for h in hits})), 'sortingOrders': ';'.join(sorted({h[3] for h in hits})),
                 'alphaMin': min((float(h[4]) for h in hits), default=''), 'alphaMax': max((float(h[4]) for h in hits), default=''),
                 'first': '/'.join(hits[0][:3]) if hits else ''})
with open(out_dir / 'common-88-coverage.csv', 'w', newline='', encoding='utf-8-sig') as fh:
    w = csv.DictWriter(fh, fieldnames=list(crow[0])); w.writeheader(); w.writerows(crow)
cok = sum(1 for r in crow if r['runtimeRows'])
print(f'common: {cok}/{len(crow)} rendered')
for r in crow:
    if not r['runtimeRows']:
        print('  NOT RENDERED', r['assetId'], r['frame'])
