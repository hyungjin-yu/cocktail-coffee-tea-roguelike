"""2026-11-22 정식 픽셀 아트 반입: manifest.ready.json 의 destination 으로 복사하고 SHA-256을 대조한다.
신규 PNG(몸체·바닥 효과·HUD)는 Point/비압축/mipmap 끔/FullRect .meta 를 함께 만든다(몸체=발 피벗 0.5,0.125, 나머지=센터).
능력치 아이콘 15장은 기존 .meta 를 보존한 채 PNG만 교체한다. 사용: python Tools/Art/import_feedback_art_2026_11_22.py (저장소 루트)"""
import hashlib, json, os, re, shutil, sys, uuid

SRC = "ArtSource/feedback-art-2026-11-22/revisions/v1-formal-pixels"
TEMPLATE = "Assets/_Project/Resources/StatIcons/stat_damage.png.meta"  # 기존 Point 비압축 스프라이트 .meta 형식
FOLDER_META = "fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def make_meta(body):
    t = open(TEMPLATE, encoding="utf8").read()
    t = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, t, count=1)
    t = t.replace("spritePixelsToUnits: 16", "spritePixelsToUnits: 128")
    if body:
        t = t.replace("alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}", "alignment: 9\n  spritePivot: {x: 0.5, y: 0.125}")
    return t


def ensure_folder(d):
    os.makedirs(d, exist_ok=True)
    meta = d + ".meta"
    if not os.path.exists(meta):
        open(meta, "w", encoding="utf8", newline="\n").write(FOLDER_META.format(g=uuid.uuid4().hex))


def main():
    m = json.load(open(f"{SRC}/manifest.ready.json", encoding="utf8"))
    count = bad = 0
    for a in m["assets"]:
        for c in a["clips"]:
            for f in c["frames"]:
                s, d = f"{SRC}/{f['path']}", f["destination"]
                if sha(s) != f["sha256"]:
                    print("SOURCE HASH MISMATCH", s); bad += 1; continue
                is_stat = a["kind"] == "stat"
                if is_stat and not os.path.exists(d + ".meta"):
                    print("stat meta missing", d); bad += 1; continue
                if not is_stat:
                    parts = d.split("/")
                    for i in range(4, len(parts)):  # Assets/_Project/Resources/FeedbackArt[/...]
                        ensure_folder("/".join(parts[:i]))
                shutil.copyfile(s, d)
                if not is_stat and not os.path.exists(d + ".meta"):
                    open(d + ".meta", "w", encoding="utf8", newline="\n").write(make_meta(a["kind"] == "body"))
                if sha(d) != f["sha256"]:
                    print("DEST HASH MISMATCH", d); bad += 1
                count += 1
    print(f"imported {count} files, mismatches {bad}")
    sys.exit(1 if bad else 0)


main()
