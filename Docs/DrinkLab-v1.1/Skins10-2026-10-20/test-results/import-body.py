# usage: python import-body.py <skinId>   (only after root import approval for that skin)
import hashlib, json, os, re, shutil, sys, uuid
from PIL import Image
skin = sys.argv[1]
proj = r"C:\Users\user\Documents\Projects\CocktailCoffeeTea"
art = os.path.join(proj, r"ArtSource\skins-aseprite-pixellab-2026-10-20")
man = json.load(open(os.path.join(art, skin + "-manifest.json"), encoding="utf-8-sig"))
assert man["skinId"] == skin
job = man["job"]
ref = {"bartender": "skin_bartender_midnight", "tea_sommelier": "skin_tea_jade"}[job]
required = {"bartender": 23, "tea_sommelier": 28}[job]
res = os.path.join(proj, r"Assets\_Project\Resources\CosmeticSkins")
dst = os.path.join(res, skin)
update = "--update" in sys.argv  # 기존 폴더: 같은 파일 이름 집합일 때 바뀐 PNG만 덮고 기존 meta(GUID/pivot/PPU) 보존
if os.path.exists(dst) and not update: sys.exit("refuse: exists (use --update) " + dst)
assert not man.get("failures"), man.get("failures")
frames = [f for f in man["frames"] if f.get("runtimeUse") == "runtime"]
names = sorted(f["filename"] for f in frames)
assert len(names) == required == len(set(names)), (len(names), required)
for f in frames:
    data = open(f["output"], "rb").read()
    assert hashlib.sha256(data).hexdigest() == f["outputSHA256"].lower(), f["filename"]
    im = Image.open(f["output"])
    assert im.mode == "RGBA" and im.size == (256, 256), (f["filename"], im.mode, im.size)
    refmeta = os.path.join(res, ref, f["filename"] + ".meta")
    assert os.path.exists(refmeta), refmeta
    meta = open(refmeta, encoding="utf-8").read()
    px = re.search(r"spritePivot: \{x: ([\d.]+), y: ([\d.]+)\}", meta)
    assert [float(px.group(1)), float(px.group(2))] == [float(v) for v in f["pivotNormalized"]], f["filename"]
    assert "spritePixelsToUnits: %g" % f["pixelsPerUnit"] in meta
    c = all(f["checks"].values())
    assert c, (f["filename"], f["checks"])
if update and os.path.exists(dst):
    have = sorted(n for n in os.listdir(dst) if n.endswith(".png"))
    assert have == names, ("file set differs", set(have) ^ set(names))
    changed = 0
    for f in frames:
        t = os.path.join(dst, f["filename"])
        if hashlib.sha256(open(t, "rb").read()).hexdigest() != f["outputSHA256"].lower():
            shutil.copyfile(f["output"], t); changed += 1
    print("updated", skin, "changed PNGs", changed, "metas preserved")
    sys.exit(0)
os.makedirs(dst)
open(dst + ".meta", "w", newline="\n").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)
for f in frames:
    shutil.copyfile(f["output"], os.path.join(dst, f["filename"]))
    raw = open(os.path.join(res, ref, f["filename"] + ".meta"), encoding="utf-8", newline="").read()
    raw, n = re.subn(r"^guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, raw, count=1, flags=re.M)
    assert n == 1
    open(os.path.join(dst, f["filename"] + ".meta"), "w", encoding="utf-8", newline="").write(raw)
print("imported", skin, len(frames), "frames from", ref, "meta template")
