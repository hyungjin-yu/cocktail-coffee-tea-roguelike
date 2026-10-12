"""QA: AutoTour 모션 녹화(frames.csv)를 구간별로 요약한다. 사람이 보는 영상 검수를 대신하지 않는다.
사용: python -I analyze_motion.py <tour out dir> <report.md>
측정: 구간별 클립 순서(런 길이), 루프 횟수, 전환 지연(프레임@30fps 게임시간), 좌우 반전 일치,
손 anchor 오프셋 점프(같은 스프라이트 내 0이어야 함 / 프레임 경계 최대), 임시 손 위치 사용, 피격/사망 표시."""
import csv, glob, os, sys
from collections import OrderedDict

def runs(seq):
    out = []
    for v in seq:
        if out and out[-1][0] == v: out[-1][1] += 1
        else: out.append([v, 1])
    return out

def fnum(x):
    try: return float(x)
    except: return None

def analyze(path):
    rows = list(csv.DictReader(open(path, encoding='utf-8')))
    segs = OrderedDict()
    for r in rows: segs.setdefault(r['segment'], []).append(r)
    res = []
    flip_mismatch = sum(1 for r in rows if fnum(r['facing']) and fnum(r['visualScaleX']) and (fnum(r['facing']) > 0) != (fnum(r['visualScaleX']) > 0) and r['dead'] == '0')
    prov = sum(1 for r in rows if r['provisionalHand'] == '1')
    same_sprite_jump = 0.0; boundary_jump = 0.0; boundary_where = ''
    for a, b in zip(rows, rows[1:]):
        if a['dead'] == '1' or b['dead'] == '1': continue
        ha = (fnum(a['handDX']), fnum(a['handDY'])); hb = (fnum(b['handDX']), fnum(b['handDY']))
        if None in ha or None in hb: continue
        # 같은 방향에서만 비교(좌우 반전 시 X 부호가 바뀌는 것은 정상)
        if a['facing'] != b['facing']: continue
        d = ((ha[0]-hb[0])**2 + (ha[1]-hb[1])**2) ** 0.5
        if a['sprite'] == b['sprite'] and abs(fnum(a['posX']) - fnum(b['posX'])) < 1e-6: same_sprite_jump = max(same_sprite_jump, d)
        elif a['sprite'] != b['sprite'] and d > boundary_jump: boundary_jump = d; boundary_where = f"{a['sprite']}->{b['sprite']} @frame {b['frame']}"
    for name, rs in segs.items():
        clips = runs([r['clip'] for r in rs])
        frames = [int(r['clipFrame']) for r in rs if r['clipFrame'] != '']
        wraps = sum(1 for x, y in zip(frames, frames[1:]) if y < x)
        dx = fnum(rs[-1]['posX']) - fnum(rs[0]['posX'])
        hurt = sum(1 for r in rs if r['clip'] == 'hurt')
        tint = sum(1 for r in rs if fnum(r['tintG']) is not None and fnum(r['tintG']) < 0.99)
        attacks = sum(1 for x, y in zip(rs, rs[1:]) if y['clip'] in ('melee', 'ranged') and (x['clip'] != y['clip'] or int(y['clipFrame'] or 0) < int(x['clipFrame'] or 0)))
        states = sorted(set(r['state'] for r in rs))
        res.append((name, len(rs), ' '.join(f"{c}x{n}" for c, n in clips), wraps, f"{dx:+.2f}", hurt, tint, attacks, ','.join(states), rs[-1]['clip'], rs[-1]['clipFrame']))
    return rows, res, flip_mismatch, prov, same_sprite_jump, boundary_jump, boundary_where

def main():
    out_dir, report = sys.argv[1], sys.argv[2]
    lines = ['# 모션 녹화 자동 요약', '', f'원본: `{out_dir}`. 30fps 게임시간(Time.captureFramerate) 기준. 사람 영상 검수를 대체하지 않는다.', '']
    for path in sorted(glob.glob(os.path.join(out_dir, 'video', 'motion_*', 'frames.csv'))):
        clip = os.path.basename(os.path.dirname(path))
        rows, res, flip, prov, sj, bj, bw = analyze(path)
        lines += [f'## {clip}', '', f'- 프레임 {len(rows)}, 좌우 반전 불일치(생존 중) {flip}, 임시 손 위치 프레임 {prov}',
                  f'- 손 anchor: 같은 스프라이트·정지 중 최대 이동 {sj:.4f} world, 스프라이트 경계 최대 {bj:.4f} world ({bw})', '',
                  '| 구간 | 프레임 | 클립 런(클립x프레임수) | 루프 감김 | X이동 | hurt 프레임 | 적색 tint 프레임 | 공격 시작 | 상태 | 마지막 클립/프레임 |',
                  '|---|---|---|---|---|---|---|---|---|---|']
        for r in res: lines.append('| ' + ' | '.join(str(x) for x in r) + ' |')
        lines.append('')
    open(report, 'w', encoding='utf-8').write('\n'.join(lines))
    print('\n'.join(lines))

if __name__ == '__main__': main()
