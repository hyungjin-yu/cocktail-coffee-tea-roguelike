"""73 제조 전용 배경: Codex CraftingBackdrops-2026-10-19(ready, root 수락) → Resources/DrinkLab (ArtSource는 읽기만).

- dl_craft_{bar,tea}_wall.png (400x287) / dl_craft_{bar,tea}_table.png (400x12): 정수 2배 = 벽 800x574 + 상판 800x24 (+ 코드 정보띠 92).
- dl_alpha_bottom.json: Resources/DrinkLab의 dl_*.png마다 아래쪽 완전 투명 행 수(아트 px). 잔·다관의 실제 보이는 바닥을 상판 윗면에 맞추는 데 쓴다.
sha256이 manifest와 다르면 복사하지 않는다.
사용: python Tools/DrinkLab/import_crafting_backdrops.py
"""
import glob, hashlib, json, os, shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'v1', 'CraftingBackdrops-2026-10-19')
DST = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'DrinkLab')


def main():
    man = json.load(open(os.path.join(SRC, 'manifest.json'), encoding='utf-8-sig'))
    if man.get('state') != 'ready':
        raise SystemExit('manifest not ready')
    for a in man['assets']:
        name = a['png']
        src = os.path.join(SRC, name)
        if hashlib.sha256(open(src, 'rb').read()).hexdigest() != a['sha256'][name]:
            raise SystemExit(name + ': sha mismatch')
        shutil.copyfile(src, os.path.join(DST, name))
        print(name, 'copied')
    bottoms = {}
    for f in sorted(glob.glob(os.path.join(DST, 'dl_*.png'))):
        im = Image.open(f).convert('RGBA')
        bbox = im.getchannel('A').getbbox()
        if bbox:
            bottoms[os.path.splitext(os.path.basename(f))[0]] = im.height - bbox[3]
    json.dump({'note': '아래쪽 완전 투명 행 수(아트 px). Tools/DrinkLab/import_crafting_backdrops.py가 실제 PNG로 생성.', 'rows': bottoms},
              open(os.path.join(DST, 'dl_alpha_bottom.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('alpha bottoms', {k: v for k, v in bottoms.items() if k in ('dl_teapot', 'dl_mixing_glass', 'dl_glass_cocktail', 'dl_teacup', 'dl_shaker_open', 'dl_teapot_open_body')})


if __name__ == '__main__':
    main()
