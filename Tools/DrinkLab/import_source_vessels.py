"""73 단계1: Codex SourceVessels-2026-10-19(ready) 병 분리 아트 → Resources/DrinkLab (ArtSource는 읽기만).

- dl_srcbottle_{id}.png      = _open_empty (붓기 전경, 뚜껑 분리)
- dl_srcbottle_{id}_mask.png = _mask (라벨 뒤까지 이어진 물리 내부 전체; _visible_mask는 쓰지 않음)
- dl_srcbottle_{id}_cap.png  = _cap (세워 둔 동안만 표시)
- anchors.json에 dl_srcbottle_{id} 항목: manifest 앵커(왼쪽 위 원점 px) → 0~1 왼쪽 아래 원점. grip = 회전축(12,28).
sha256이 manifest와 다르면 복사하지 않는다. 바스푼 행 구간표(dl_barspoon_extent.json)도 실제 PNG에서 다시 만든다.
사용: python Tools/DrinkLab/import_source_vessels.py
"""
import hashlib, json, os, shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'v1', 'SourceVessels-2026-10-19')
DST = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'DrinkLab')

def sha(p):
    return hashlib.sha256(open(p, 'rb').read()).hexdigest()

def main():
    man = json.load(open(os.path.join(SRC, 'manifest.json'), encoding='utf-8-sig'))
    anchors_path = os.path.join(DST, 'anchors.json')
    raw = open(anchors_path, 'rb').read()
    table = json.loads(raw.decode('utf-8-sig'))
    entries = [e for e in table['entries'] if not e['assetId'].startswith('dl_srcbottle_')]
    log = []
    for a in man['assets']:
        if a.get('status') != 'ready':
            log.append((a['assetId'], 'skip: not ready')); continue
        bid = a['assetId'].replace('dl_bottle_', '')
        new = 'dl_srcbottle_' + bid
        ok = True
        for kind, suffix in (('open_empty', ''), ('mask', '_mask'), ('cap', '_cap')):
            src = os.path.join(SRC, a['files'][kind])
            if sha(src) != a['sha256'][kind]:
                log.append((a['assetId'], f'sha mismatch {kind}')); ok = False; break
            shutil.copyfile(src, os.path.join(DST, new + suffix + '.png'))
        if not ok:
            continue
        w, h = a['size']
        n = lambda p: [p[0] / w, 1.0 - p[1] / h]
        l, t, r, b = a['interiorMaskBBox']
        an = a['anchors']
        entries.append({'assetId': new, 'pourLip': n(an['pourLip']), 'mouth': n(an['mouth']), 'grip': n(an['grip']),
                        'liquid': [l / w, 1.0 - b / h, r / w, 1.0 - t / h], 'width': w, 'height': h})
        log.append((a['assetId'], f'imported as {new}'))
    table['entries'] = entries
    open(anchors_path, 'w', encoding='utf-8', newline='\n').write(json.dumps(table, ensure_ascii=False, indent=4))

    frames = []
    for i in range(4):
        f = os.path.join(DST, 'dl_barspoon_%02d.png' % i)
        im = Image.open(f).convert('RGBA'); px = im.load(); W, H = im.size
        rows = []
        for yb in range(H):
            y = H - 1 - yb
            xs = [x for x in range(W) if px[x, y][3] > 0]
            rows.append([min(xs), max(xs) + 1] if xs else [-1, -1])
        frames.append({'file': os.path.basename(f), 'sha256': sha(f)[:16], 'width': W, 'height': H, 'rows': rows})
    json.dump({'note': 'dl_barspoon 프레임별 행(아래→위) 불투명 x 구간 [min,max). Tools/DrinkLab/import_source_vessels.py가 실제 PNG로 생성, 아트 원본 수정 아님.', 'frames': frames},
              open(os.path.join(DST, 'dl_barspoon_extent.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    for row in log:
        print(*row)

if __name__ == '__main__':
    main()
