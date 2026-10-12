"""QA: 아트 검수 frame-checklist.csv(19몸체 363 원본 프레임)를 QA 투어 CSV(sprite 열)의 실제 엔진 노출과 대조한다.
사용: python -I frame_coverage.py <frame-checklist.csv> <out.csv> <run dir> [<run dir> ...]
플레이어 motion은 sprite, bestiary는 sprite, 보스 녹화는 bossClip/bossFrame에서 이름을 만든다."""
import csv, sys, collections
from pathlib import Path
check, out, runs = sys.argv[1], sys.argv[2], sys.argv[3:]
frames = list(csv.DictReader(open(check, encoding='utf-8-sig')))
seen = collections.defaultdict(list)
for run in runs:
    for p in Path(run, 'out', 'video').glob('*/frames.csv'):
        for r in csv.DictReader(p.open(encoding='utf-8-sig')):
            name = r.get('sprite')
            if not name and r.get('bossClip'):
                name = p.parent.name.removeprefix('40_boss_') + '_' + r['bossClip'] + '_right_' + r['bossFrame'].zfill(2)
            if name:
                seen[name].append((Path(run).name, p.parent.name, r['frame'], r['gameTime'], r.get('facing', '')))
rows = []
for f in frames:
    m = seen.get(Path(f['frame']).stem, [])
    rows.append({'asset': f['asset'], 'clip': f['clip'], 'frame': f['frame'], 'runtimeRows': len(m),
                 'facings': ';'.join(sorted(set(x[4] for x in m))), 'runs': ';'.join(sorted(set(x[0] for x in m))),
                 'first': '/'.join(m[0]) if m else ''})
with open(out, 'w', newline='', encoding='utf-8-sig') as fh:
    w = csv.DictWriter(fh, fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)
miss = [r for r in rows if not r['runtimeRows']]
both = [r for r in rows if r['runtimeRows'] and '-1' in r['facings'].split(';') and '1' in r['facings'].split(';')]
print(f'total {len(rows)} mapped {len(rows)-len(miss)} missing {len(miss)} bothFacings {len(both)}')
for r in miss: print('MISSING', r['asset'], r['clip'], r['frame'])
