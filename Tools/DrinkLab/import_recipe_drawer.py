"""73 레시피 서랍: Codex RecipeDrawer-2026-10-19(ready, root 시각 수락) → Resources/ProgressionUI (ArtSource는 읽기만).

body/front/closed 480x320(공통 피벗 240,160) + divider 128x64. 기하(paperSlots·paperPath·opening·frontOcclusion)는
manifest를 Resources/ProgressionUI/recipe_drawer_layout.json으로 그대로 복사해 런타임이 읽는다. sha256이 다르면 복사하지 않는다.
사용: python Tools/DrinkLab/import_recipe_drawer.py
"""
import hashlib, json, os, shutil

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'v1', 'RecipeDrawer-2026-10-19')
DST = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'ProgressionUI')


def main():
    man = json.load(open(os.path.join(SRC, 'manifest.json'), encoding='utf-8-sig'))
    if not man.get('ready'):
        raise SystemExit('manifest not ready')
    for a in man['assets']:
        png = a['png']
        src = os.path.join(SRC, png)
        if hashlib.sha256(open(src, 'rb').read()).hexdigest() != a['sha256'][png]:
            raise SystemExit(png + ': sha mismatch')
        shutil.copyfile(src, os.path.join(DST, png))
        print(png, 'copied')
    layout = {k: man[k] for k in ('canvas', 'pivotPixelsTopLeft', 'paperSourceSize', 'paperTextSafeRect', 'paperCavityRect', 'frontOcclusionRect', 'paperSlots', 'paperPath', 'opening')}
    json.dump(layout, open(os.path.join(DST, 'recipe_drawer_layout.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('layout written')


if __name__ == '__main__':
    main()
