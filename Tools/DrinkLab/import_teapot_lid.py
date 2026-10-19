"""73 다관 뚜껑: Codex TeapotLid-2026-10-19(ready, root 수락) 분리 아트 → Resources/DrinkLab (ArtSource는 읽기만).

- dl_teapot_open_body.png = 뚜껑 없는 몸체(열린 입구). 같은 64x56 캔버스, 물리 마스크는 기존 dl_teapot_mask 그대로.
- dl_teapot_lid.png       = 원래 얹힌 좌표 그대로의 뚜껑(같은 캔버스). 몸체 위에 (0,0)으로 겹치면 원래 dl_teapot과 RGBA 동일.
- anchors.json에 dl_teapot_open_body 항목: 새 입구 mouth(32,20)·pourLip·grip(manifest, 왼쪽 위 원점 px) → 0~1 왼쪽 아래 원점, liquid는 dl_teapot과 같음.
sha256이 manifest와 다르면 복사하지 않는다.
사용: python Tools/DrinkLab/import_teapot_lid.py
"""
import hashlib, json, os, shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'v1', 'TeapotLid-2026-10-19')
DST = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'DrinkLab')


def sha(p):
    return hashlib.sha256(open(p, 'rb').read()).hexdigest()


def main():
    man = json.load(open(os.path.join(SRC, 'manifest.json'), encoding='utf-8-sig'))
    if man.get('status') != 'ready':
        raise SystemExit('manifest not ready')
    anchors_path = os.path.join(DST, 'anchors.json')
    table = json.loads(open(anchors_path, 'rb').read().decode('utf-8-sig'))
    base = next(e for e in table['entries'] if e['assetId'] == 'dl_teapot')
    entries = [e for e in table['entries'] if e['assetId'] != 'dl_teapot_open_body']
    for a in man['assets']:
        src = os.path.join(SRC, a['png'])
        if a.get('status') != 'ready' or sha(src) != a['sha256']:
            raise SystemExit(f"{a['assetId']}: not ready or sha mismatch")
        shutil.copyfile(src, os.path.join(DST, a['assetId'] + '.png'))
        print(a['assetId'], 'copied', a['sha256'][:16])
        if a['assetId'] == 'dl_teapot_open_body':
            w, h = a['size']
            n = lambda p: [(p[0] + 0.5) / w, 1.0 - (p[1] + 0.5) / h]  # 픽셀 중심(기존 dl_teapot 항목과 같은 규약)
            an = a['anchors']
            entries.append({'assetId': 'dl_teapot_open_body', 'pourLip': n(an['pourLip']), 'mouth': n(an['mouth']), 'grip': n(an['grip']),
                            'liquid': base['liquid'], 'width': w, 'height': h})
    table['entries'] = entries
    open(anchors_path, 'w', encoding='utf-8', newline='\n').write(json.dumps(table, ensure_ascii=False, indent=4))


if __name__ == '__main__':
    main()
