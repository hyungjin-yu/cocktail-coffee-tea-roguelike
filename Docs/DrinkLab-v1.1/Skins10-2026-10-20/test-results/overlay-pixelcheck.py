import json, os
from PIL import Image
proj = r"C:\Users\user\Documents\Projects\CocktailCoffeeTea"
art = os.path.join(proj, r"ArtSource\skins-aseprite-pixellab-2026-10-20")
man = json.load(open(os.path.join(art, "sword-overlay-manifest.json"), encoding="utf-8-sig"))
for f in man["frames"]:
    o = Image.open(os.path.join(art, f["output"])).convert("RGBA")
    s = Image.open(f["source"]).convert("RGBA")
    m = Image.open(f["mask"]).convert("RGBA")
    op, sp, mp = o.load(), s.load(), m.load()
    opaque = outside_mask = mismatch = 0
    for y in range(256):
        for x in range(256):
            a = op[x, y]
            if a[3] == 0: continue
            opaque += 1
            mk = mp[x, y]
            if mk[3] == 0 and max(mk[:3]) == 0: outside_mask += 1
            if a != sp[x, y]: mismatch += 1
    print(f["filename"], o.size, "opaque", opaque, "outsideMask", outside_mask, "pixelsDifferFromApprovedSource", mismatch)
