"""Import ONLY manifest status=ready files of ArtSource/ui-icons-2026-11-12 into Unity Resources (Unity owner tool).
- Verifies sha256 against the manifest before copying; never edits ArtSource.
- card      -> Assets/_Project/Resources/CardEducation/<artId>.png (+ Card_<id>.asset educationArtId)
- research* -> Assets/_Project/Resources/ResearchIcons/<artId>.png
- mastery   -> Assets/_Project/Resources/ResearchIcons/<artId>.png
- Existing .meta files are preserved; new ones copy the CardEducation import settings (Sprite, Single, PPU 64, Point, no mip, uncompressed)
  with a fresh guid / sprite ids.
usage: python -I import_ui_icons_2026_11_12.py <repo> [--dry-run] [--only artId,artId,...]
"""
import hashlib, json, os, re, shutil, sys, uuid, random

repo = sys.argv[1]
dry = "--dry-run" in sys.argv
only = None
if "--only" in sys.argv: only = set(sys.argv[sys.argv.index("--only") + 1].split(","))  # 코디네이터가 알린 ready 목록만
sys.stdout.reconfigure(encoding="utf-8")
art = os.path.join(repo, "ArtSource", "ui-icons-2026-11-12")
res = os.path.join(repo, "Assets", "_Project", "Resources")
template = open(os.path.join(res, "CardEducation", "card_gin.png.meta"), encoding="utf-8").read()
folder_template = open(os.path.join(res, "CardEducation.meta"), encoding="utf-8").read()
manifest = json.load(open(os.path.join(art, "manifest.json"), encoding="utf-8"))

def sha(p): return hashlib.sha256(open(p, "rb").read()).hexdigest()

def new_meta(name):
    rid = random.randint(10**17, 9 * 10**18)
    m = template
    m = re.sub(r"^guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, m, flags=re.M)
    m = m.replace("card_gin_0", name + "_0").replace("470225062640293657", str(rid))
    m = m.replace("917192ff333968600800000000000000", uuid.uuid4().hex).replace("5e97eb03825dee720800000000000000", uuid.uuid4().hex)
    return m

def ensure_folder(path):
    if not os.path.isdir(path):
        print("mkdir", path)
        if not dry: os.makedirs(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        print("folder meta", meta)
        if not dry: open(meta, "w", encoding="utf-8", newline="\n").write(re.sub(r"^guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, folder_template, flags=re.M))

done, skipped = [], []
for it in manifest["items"]:
    if it.get("status") != "ready": skipped.append((it.get("artId"), it.get("status"))); continue
    if only is not None and it["artId"] not in only: skipped.append((it["artId"], "ready-not-announced")); continue
    src = it["png"]
    if not os.path.isabs(src): src = os.path.join(art, src)
    got = sha(src)
    if got != it["sha256"]: raise SystemExit(f"HASH MISMATCH {it['artId']}: {got} != {it['sha256']}")
    raw = open(src, "rb").read()
    if raw[:8] != bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) or b"acTL" in raw or b"fcTL" in raw: raise SystemExit("NOT A SINGLE-FRAME PNG " + it["artId"])
    import struct
    w, h = struct.unpack(">II", raw[16:24])
    if (w, h) != (64, 64): raise SystemExit(f"SIZE {it['artId']} {w}x{h}")
    group = it["group"]
    dst_dir = os.path.join(res, "CardEducation") if group == "card" else os.path.join(res, "ResearchIcons")
    ensure_folder(dst_dir)
    dst = os.path.join(dst_dir, it["artId"] + ".png")
    print(f"copy {it['artId']} ({group}, id={it['id']}) -> {os.path.relpath(dst, repo)}")
    if not dry:
        shutil.copyfile(src, dst)
        assert sha(dst) == it["sha256"]
        if not os.path.exists(dst + ".meta"):
            open(dst + ".meta", "w", encoding="utf-8", newline="\n").write(new_meta(it["artId"]))
        else:
            print("  keep existing meta")
    if group == "card":
        asset = os.path.join(res, "Cards", f"Card_{it['id']}.asset")
        s = open(asset, encoding="utf-8", newline="").read()
        s2 = re.sub(r"^(  educationArtId:)[^\r\n]*", r"\1 " + it["artId"], s, count=1, flags=re.M)
        if s2 == s and f"educationArtId: {it['artId']}" not in s: raise SystemExit("educationArtId field missing in " + asset)
        print(f"  {os.path.basename(asset)} educationArtId = {it['artId']}")
        if not dry: open(asset, "w", encoding="utf-8", newline="").write(s2)
        # 코드 기본값(CardEducationData — 시험이 에셋과 같은지 대조)도 같은 그림 ID로.
        cs_path = os.path.join(repo, "Assets", "_Project", "Scripts", "Data", "CardEducationData.cs")
        cs = open(cs_path, encoding="utf-8", newline="").read()
        i = cs.index(f'case "{it["id"]}":'); j = cs.index("card.educationArtId = ", i); k = cs.index(";", j)
        line = f'card.educationArtId = "{it["artId"]}"'
        if cs[j:k] != line:
            cs = cs[:j] + line + cs[k:]
            if "// 2026-11-12 ui-icons ready" not in cs[k:k + 40]: cs = cs[:j + len(line) + 1] + " // 2026-11-12 ui-icons ready" + cs[j + len(line) + 1:]
            print(f"  CardEducationData {it['id']} = {it['artId']}")
            if not dry: open(cs_path, "w", encoding="utf-8", newline="").write(cs)
    done.append(it["artId"])
# 티 변형이 계획된 노드(매니페스트 research_tea 항목 — 상태 무관). 티 소믈리에는 이 노드에서 커피 그림으로 대체하지 않는다.
# 인계표(2026-11-12)에서 차 변형이 필요하다고 정한 7종 + 매니페스트 research_tea 항목(재작업으로 항목이 빠져도 커피 그림 대체 금지 유지).
PLANNED_TEA = {"espresso_shot", "ristretto", "full_body_roast", "perfect_extraction", "caffeine_rush", "doppio_body", "fine_grind"}
tea_ids = sorted(PLANNED_TEA | {it["id"] for it in manifest["items"] if it.get("group") == "research_tea"})
tea_path = os.path.join(res, "ResearchIcons", "tea_variant_required.txt")
print(f"tea variant required ({len(tea_ids)}): {tea_ids}")
if not dry and tea_ids:
    ensure_folder(os.path.dirname(tea_path))
    open(tea_path, "w", encoding="utf-8", newline="\n").write("\n".join(tea_ids) + "\n")
    if not os.path.exists(tea_path + ".meta"):
        meta_text = "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        open(tea_path + ".meta", "w", encoding="utf-8", newline="\n").write(meta_text)
# 연구·숙련 그림의 불투명 영역(위 기준 x0 y0 x1 y1, 끝 미포함) — 런타임이 같은 텍스처의 부분 사각형으로 잘라 표시 칸을 채운다(원본 불변).
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from png_alpha_bounds import alpha_bounds
ri = os.path.join(res, "ResearchIcons")
if os.path.isdir(ri):
    rows = []
    for f in sorted(os.listdir(ri)):
        if f.endswith(".png"):
            b = alpha_bounds(os.path.join(ri, f))
            if b: rows.append(f"{f[:-4]} {b[0]} {b[1]} {b[2]} {b[3]}")
    bpath = os.path.join(ri, "bounds.txt")
    print(f"bounds rows: {len(rows)}")
    if not dry:
        open(bpath, "w", encoding="utf-8", newline=chr(10)).write(chr(10).join(rows) + chr(10))
        if not os.path.exists(bpath + ".meta"):
            open(bpath + ".meta", "w", encoding="utf-8", newline=chr(10)).write(
                "fileFormatVersion: 2" + chr(10) + "guid: " + uuid.uuid4().hex + chr(10) + "TextScriptImporter:" + chr(10) + "  externalObjects: {}" + chr(10) + "  userData: " + chr(10) + "  assetBundleName: " + chr(10) + "  assetBundleVariant: " + chr(10))
print(f"ready imported: {len(done)} {done}")
print(f"skipped: {len(skipped)} {skipped}")
