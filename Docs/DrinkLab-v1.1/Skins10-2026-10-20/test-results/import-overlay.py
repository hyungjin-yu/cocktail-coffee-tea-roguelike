import hashlib, json, os, re, shutil, sys, uuid
proj = r"C:\Users\user\Documents\Projects\CocktailCoffeeTea"
art = os.path.join(proj, r"ArtSource\skins-aseprite-pixellab-2026-10-20")
man = json.load(open(os.path.join(art, "sword-overlay-manifest.json"), encoding="utf-8-sig"))
src_meta_dir = os.path.join(proj, r"Assets\_Project\Resources\WeaponSkins\weaponskin_sword_01\swing_base")
dst = os.path.join(proj, r"Assets\_Project\Resources\WeaponSkins\weaponskin_sword_01\swing_overlay")
if os.path.exists(dst):
    sys.exit("refuse: target exists " + dst)
frames = man["frames"]
assert len(frames) == 5, len(frames)
names = sorted(f["filename"] for f in frames)
assert names == [f"tea_sommelier_sword_swing_right_0{i}.png" for i in range(3, 8)], names
for f in frames:
    p = os.path.join(art, f["output"])
    h = hashlib.sha256(open(p, "rb").read()).hexdigest()
    assert h.lower() == f["outputSHA256"].lower(), (p, h)
    assert f["sizePx"] == [256, 256] and f["pixelsPerUnit"] == 128
os.makedirs(dst)
# folder meta
open(dst + ".meta", "w", newline="\n").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)
for f in frames:
    name = f["filename"]
    shutil.copyfile(os.path.join(art, f["output"]), os.path.join(dst, name))
    meta = open(os.path.join(src_meta_dir, name + ".meta"), encoding="utf-8", newline="").read()
    meta, n = re.subn(r"^guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, meta, count=1, flags=re.M)
    assert n == 1
    open(os.path.join(dst, name + ".meta"), "w", encoding="utf-8", newline="").write(meta)
    print("imported", name, f["outputSHA256"])
