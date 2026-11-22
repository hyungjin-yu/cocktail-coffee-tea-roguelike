"""Author and export the level-up UI sprites on an integer-pixel grid."""
import json
import struct
import zlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
INK = "#17141d"
SKIN = ("#a35e48", "#e6a671", "#ffcc91")
TIERS = {
    "common": ("일반", "#d1d1d1", "#201d24", "#574039", "#a78663", [55, 65, 70]),
    "rare": ("희귀", "#66b8ff", "#192329", "#365464", "#7eb4be", [55, 70, 65, 60]),
    "epic": ("영웅", "#d180ff", "#241c2b", "#563f67", "#bc8ec7", [55, 65, 70, 65, 65]),
    "special": ("특수", "#ffbf59", "#261e21", "#78502e", "#edb86c", [55, 65, 65, 65, 65, 65]),
}
JOBS = ("bartender", "tea_sommelier")


def blank(size):
    return Image.new("RGBA", size)


def save(image, relative):
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def composite(layers):
    result = blank(layers[0].size)
    for layer in layers:
        result.alpha_composite(layer)
    return result


def decoration(tier, size=(86, 124)):
    _, _, field, base, light, _ = TIERS[tier]
    width, height = size
    right, bottom = width-1, height-1
    paper = blank(size)
    trim = blank(paper.size)
    d = ImageDraw.Draw(paper)
    d.polygon([(3,0),(right-3,0),(right,3),(right,bottom-3),(right-3,bottom),
               (3,bottom),(0,bottom-3),(0,3)], INK)
    d.rectangle((2,2,right-2,bottom-2), fill=field)
    d = ImageDraw.Draw(trim)
    for y in (2, bottom-2):
        d.line((4,y,right-4,y), fill=base)
    for x in (2, right-2):
        d.line((x,5,x,bottom-5), fill=base)
    # Mirror four physical corner fittings on independent transparent pixels.
    corner = blank((7,15))
    c = ImageDraw.Draw(corner)
    if tier == "common":
        c.line([(2,8),(2,4),(4,2),(6,2)], fill=light)
        c.point((4,5), fill=base)
    elif tier == "rare":
        c.polygon([(3,2),(6,2),(6,5),(4,5),(4,9),(2,11),(2,4)], base)
        c.line([(2,8),(2,4),(4,2),(6,2)], fill=light)
        c.point((4,4), fill="#d0e4d8")
    elif tier == "epic":
        c.polygon([(4,1),(6,1),(6,5),(4,5),(4,10),(2,13),(1,10),(1,4)], base)
        c.line([(2,10),(2,5),(5,2),(6,2)], fill=light)
        c.polygon([(4,4),(5,5),(4,6),(3,5)], "#ead1e9")
    else:
        c.polygon([(4,1),(6,1),(6,6),(4,8),(5,10),(3,14),(1,12),(1,5)], base)
        c.line([(2,12),(3,10),(2,8),(2,5),(5,2),(6,2)], fill=light)
        c.polygon([(4,3),(6,5),(4,7),(2,5)], light)
        c.point((4,5), fill="#fff0b4")
        c.point((3,12), fill="#fff0b4")
    for right in (False, True):
        for bottom in (False, True):
            tile = corner.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if right else corner
            tile = tile.transpose(Image.Transpose.FLIP_TOP_BOTTOM) if bottom else tile
            trim.alpha_composite(tile, (width-7 if right else 0, height-15 if bottom else 0))
    for x, sign in ((2, 1), (width-3, -1)):
        if tier == "rare":
            for y in (36, 87):
                d.line((x,y,x+sign,y+3), fill=light)
        if tier == "epic":
            for y in (31, 61, 92):
                d.polygon([(x,y-4),(x+2*sign,y),(x,y+4),(x-sign,y)], base)
                d.line((x,y-2,x+sign,y), fill=light)
        if tier == "special":
            for y in (round(height*fraction) for fraction in (.19,.35,.50,.65,.81)):
                d.polygon([(x,y-5),(x+3*sign,y),(x,y+5),(x-sign,y)], base)
                d.line((x,y-3,x+2*sign,y), fill=light)
                d.line((x+2*sign,y,x,y+3), fill=light)
                d.point((x,y), fill="#fff0b4")
    return [paper, trim]


# (cover half-width, cover top, cover height, cover offset, head dip, support arm)
# The last pose is shared in position and size across every clip.
POSES = {
    "common": [(23,59,12,-14,0,False),(40,53,19,-6,0,False),(50,51,27,0,0,False)],
    "rare": [(22,61,13,-12,0,False),(31,51,18,-8,-1,False),
             (43,49,23,-3,1,False),(50,51,27,0,0,False)],
    "epic": [(23,61,13,0,0,True),(31,53,17,0,1,True),
             (40,52,21,0,6,True),(48,51,26,0,3,True),(50,51,27,0,0,True)],
    "special": [(22,63,13,0,0,True),(29,57,17,0,2,True),
                (35,55,20,0,8,True),(43,54,23,0,9,True),
                (49,51,27,0,4,True),(50,51,27,0,0,True)],
}


def delivery(job, tier, pose):
    tea = job == "tea_sommelier"
    half, y, height, offset, dip, support = pose
    cx = 64 + offset
    left, right, end = cx-half, cx+half, y+height
    coat = ("#393b31","#61644d","#8a8a68") if tea else ("#242538","#383d56","#565e76")
    apron = ("#4a2b27","#784b3c") if tea else ("#20212b","#333344")
    hair = ("#372221","#624037","#905e4d")
    shirt = ("#b39c89","#e0cdb5","#fff0d8")
    body, head, arms, board, hands = [blank((128,96)) for _ in range(5)]
    b = ImageDraw.Draw(body)

    b.polygon([(41,42),(52,38),(75,38),(88,42),(94,50),(90,62),(89,86),
               (82,90),(45,90),(38,86),(38,64),(34,53)], coat[0], INK)
    b.polygon([(45,44),(55,41),(73,41),(83,45),(82,68),(46,68)], coat[1])
    b.line([(46,47),(49,44),(54,43)], fill=coat[2], width=2)
    b.polygon([(44,66),(83,66),(88,85),(81,89),(46,89),(40,84)], apron[1], INK)
    b.rectangle((44,68,84,71), fill=apron[0])
    b.line([(48,76),(48,84),(57,84)], fill=apron[0])
    b.polygon([(53,39),(59,36),(69,36),(76,41),(72,52),(64,47),(56,51)], shirt[2], INK)
    if tea:
        b.line((64,48,64,67), fill="#bda889", width=2)
        b.ellipse((74,47,79,50), fill="#b8bd79")
        b.line((74,51,78,47), fill="#e9d49a")
    else:
        b.polygon([(61,47),(66,47),(68,56),(64,61),(60,56)], "#359ca0", INK)
        b.line((63,49,62,56), fill="#7ec5bb")
        b.ellipse((74,47,78,51), fill="#d7b269")
        b.point((75,48), fill="#ffdf91")

    h = ImageDraw.Draw(head)
    def hp(points, color, outline=None):
        h.polygon([(x, yy+dip) for x, yy in points], fill=color, outline=outline)
    if tea:
        hp([(50,22),(43,26),(36,32),(34,39),(39,43),(46,40),(50,42),(55,34)], hair[0], INK)
        hp([(40,32),(46,28),(51,28),(44,36),(38,39),(37,37)], hair[1])
        hp([(45,30),(46,28),(49,30),(48,32)], "#d3a358")
    hp([(53,10),(61,7),(72,8),(81,13),(85,22),(82,30),(79,34),(74,39),
        (69,42),(59,42),(52,39),(48,33),(44,31),(43,26),(46,17)], SKIN[1], INK)
    hp([(46,26),(49,26),(52,33),(58,38),(65,39),(74,37),(70,41),(59,41),
        (53,38),(49,33),(45,30)], SKIN[0])
    hp([(55,19),(62,15),(73,15),(79,20),(77,24),(70,22),(66,25),(58,23)], SKIN[2])
    hp([(43,18),(47,10),(55,6),(67,5),(77,7),(84,12),(87,19),(83,27),(79,30),
        (79,22),(75,16),(69,15),(63,18),(56,20),(54,27),(50,29),(47,25),(43,24)], hair[0], INK)
    hp([(46,17),(50,11),(58,8),(68,7),(77,9),(82,13),(82,19),(76,13),(70,12),
        (64,15),(56,16),(51,21),(46,23)], hair[1])
    hp([(51,12),(57,10),(65,9),(70,10),(62,11),(57,14),(51,15)], hair[2])
    if tea:
        hp([(48,18),(53,14),(63,12),(60,16),(55,19),(51,25),(47,24)], hair[1])
    else:
        hp([(68,8),(72,9),(76,12),(71,12),(68,14),(65,14)], "#b47c63")
    for ex in (58, 73):
        eyebrow = 24 + dip
        if dip >= 6:
            h.line([(ex,eyebrow+5),(ex+2,eyebrow+6),(ex+5,eyebrow+5)], fill=hair[0])
        else:
            h.line([(ex-1,eyebrow),(ex+1,eyebrow-1),(ex+4,eyebrow-1)], fill=hair[0])
            h.rectangle((ex,eyebrow+2,ex+4,eyebrow+4), fill=shirt[2])
            h.rectangle((ex+2,eyebrow+2,ex+3,eyebrow+4), fill=INK)
    h.line((68,29+dip,69,32+dip), fill=SKIN[0])
    h.line((64,36+dip,69,36+dip), fill=hair[1])
    h.point((61,33+dip), fill=SKIN[2])

    a = ImageDraw.Draw(arms)
    f = ImageDraw.Draw(hands)
    for sign in (-1,1):
        side = sign == -1
        shoulder = 42 if side else 86
        grip = left+9 if side else right-9
        gy = end-3
        if not support and not side:
            grip, gy = 92, 70
        elbow = (shoulder + grip)//2 + sign*7
        ey = min(gy-4, 65)
        a.polygon([(shoulder-sign*5,43),(shoulder+sign*5,44),
                   (elbow+sign*5,ey-6),(elbow-sign*1,ey+4),
                   (elbow-sign*8,ey),(shoulder-sign*5,51)], shirt[1], INK)
        a.line([(shoulder,45),(shoulder+sign*4,48),(elbow+sign*2,ey-5)], fill=shirt[2], width=2)
        a.line([(elbow+sign*3,ey-4),(elbow-sign*5,ey+1)], fill=shirt[0], width=2)
        a.polygon([(elbow+sign*3,ey-1),(grip+sign*5,gy-4),(grip+sign*4,gy+6),
                   (elbow-sign*4,ey+7),(elbow-sign*6,ey+2)], SKIN[0], INK)
        a.line([(elbow,ey+1),(grip,gy)], fill=SKIN[1], width=3)
        hand = f if support or side else a
        hand.polygon([(grip-6,gy+4),(grip-5,gy-1),(grip-2,gy-3),(grip+2,gy-3),
                      (grip+6,gy),(grip+5,gy+5),(grip+2,gy+7),(grip-3,gy+7)], SKIN[1], INK)
        hand.line((grip-3,gy+1,grip+3,gy+1), fill=SKIN[2])
        hand.line((grip-2,gy+5,grip+3,gy+5), fill=SKIN[0])

    d = ImageDraw.Draw(board)
    _, _, field, base, light, _ = TIERS[tier]
    inset = max(2, 7 - height//5)
    d.polygon([(left+inset,y),(right-inset,y),(right,end-5),(right-2,end),
               (left+2,end),(left,end-5)], field, INK)
    d.line([(left+inset+1,y+1),(right-inset-1,y+1),(right-2,end-5),
            (left+2,end-5),(left+inset+1,y+1)], fill=base)
    d.line((left+3,end-2,right-3,end-2), fill=base)
    page = "#d9c291" if tea else "#e3caa0"
    for dx in (-1,1):
        a0 = cx+dx*3
        a1 = cx+dx*(half-10)
        d.polygon([(a0,y+4),(a1,y+3),(a1+dx,end-8),(a0,end-7)], page)
        for yy in range(y+7,end-9,4):
            d.line((a0+dx*3,yy,a1-dx*3,yy), fill="#a38466")
    d.line((cx,y+4,cx,end-7), fill=light)
    if height >= 18:
        # Each trade stamps its own mark in the top left of the menu.
        mx, my = cx-half+15, y+6
        d.rectangle((mx-2,my-1,mx+8,my+6), fill=page)
        if tea:
            d.polygon([(mx,my+5),(mx+1,my+1),(mx+7,my),(mx+6,my+3),(mx+3,my+5)], "#64714a")
            d.line((mx,my+5,mx+5,my+1), fill="#a2a66c")
        else:
            d.polygon([(mx,my),(mx+7,my),(mx+4,my+3),(mx+4,my+5),
                       (mx+6,my+5),(mx+6,my+6),(mx+1,my+6),(mx+1,my+5),
                       (mx+3,my+5),(mx+3,my+3)], "#386568")
    return [body, head, arms, board, hands]


def aseprite(relative, frames, names, durations):
    """Write native RGBA frames with independently editable named cel layers."""
    def chunk(kind, payload):
        return struct.pack("<IH", len(payload)+6, kind) + payload

    encoded = []
    for index, (layers, duration) in enumerate(zip(frames, durations, strict=True)):
        chunks = []
        for layer_id, (name, layer) in enumerate(zip(names, layers, strict=True)):
            if index == 0:
                label = name.encode()
                chunks.append(chunk(0x2004, struct.pack("<HHHHHHB3xH",
                                    3,0,0,0,0,0,255,len(label)) + label))
            bounds = layer.getbbox()
            if bounds:
                x, y, _, _ = bounds
                cel = layer.crop(bounds)
                payload = struct.pack("<HhhBH7xHH", layer_id,x,y,255,2,*cel.size)
                chunks.append(chunk(0x2005, payload + zlib.compress(cel.tobytes())))
        payload = b"".join(chunks)
        encoded.append(struct.pack("<IHHH6x", len(payload)+16,0xF1FA,len(chunks),duration) + payload)
    width, height = frames[0][0].size
    payload = b"".join(encoded)
    header = struct.pack("<IHHHHHIH", len(payload)+128,0xA5E0,len(frames),width,height,32,1,durations[0])
    header += bytes(14) + bytes((1,1))
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(header.ljust(128,b"\0") + payload)


def main():
    manifest = {
        "sourceCommit": "7fb00e4",
        "runtimeBaseline": "dfcc628",
        "origin": "bottom-left pixel edges for anchors and rects",
        "import": {"mode":"Single", "filter":"Point", "mipmaps":False,
                   "compression":"None", "sRGB":True, "pixelsPerUnit":128, "raycastTarget":False},
        "rules": {"sharedClip":"maximum rarity among visible offers",
                  "perCardDecoration":"each offer's own rarity",
                  "emptyOffers":"skip presentation", "clock":"unscaled",
                  "lowEffects":"cards immediately available subject to existing release gate",
                  "specialEntry":"HybridUnlockView (separate from normal Draw)"},
        "handoff": {"canvasSizePx":[128,96], "targetAnchorPx":[64,32],
                    "finalBoardRectPx":[14,17,101,28], "revealMs":70,
                    "lowEffectsRevealMs":0},
        "tiers":[]
    }
    for rank, (tier, (label, tint, _, _, _, ms)) in enumerate(TIERS.items()):
        layers = decoration(tier)
        source = f"native/card_{tier}.aseprite"
        png = f"ready/card_{tier}.png"
        save(composite(layers), png)
        aseprite(source, [layers], ["Dark interior", "Fittings"], [100])
        item = {"id":tier, "runtimeEnum":tier.title(), "value":rank, "runtimeLabel":label,
                "labelTint":tint, "decoration":{"source":source, "png":png, "sizePx":[86,124],
                "safeContentPx":[7,3,72,118]}, "clips":{}}
        if tier == "special":
            panel = decoration(tier, (196,112))
            source = "native/hybrid_special.aseprite"
            png = "ready/hybrid_special.png"
            aseprite(source, [panel], ["Dark interior", "Fittings"], [100])
            save(composite(panel), png)
            item["hybridPanel"] = {"source":source, "png":png, "sizePx":[196,112],
                                   "safeContentPx":[7,3,182,106]}
        for job in JOBS:
            frames = [delivery(job,tier,pose) for pose in POSES[tier]]
            source = f"native/{job}_{tier}.aseprite"
            aseprite(source, frames, ["Uniform","Head","Arms","Menu","Fingers"], ms)
            paths = []
            for index, layers in enumerate(frames):
                path = f"ready/{job}/{tier}/{index:02}.png"
                save(composite(layers), path)
                paths.append(path)
            item["clips"][job] = {"source":source, "frames":[{"png":p,"durationMs":d}
                                  for p,d in zip(paths,ms,strict=True)], "handoffAtMs":sum(ms),
                                  "selectAtMs":sum(ms)+70, "loop":False}
        manifest["tiers"].append(item)
    (ROOT/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n", encoding="utf-8")
    previews(manifest)


def previews(manifest):
    gallery = Image.new("RGBA",(1272, 4*218+46),"#151522")
    d = ImageDraw.Draw(gallery)
    title = ImageFont.load_default(size=23)
    label = ImageFont.load_default(size=15)
    d.text((20,11),"MENU SERVICE / POSE COMPARISON",font=title,fill="#ebdfc9")
    for row, item in enumerate(manifest["tiers"]):
        y = 46+row*218
        d.rectangle((12,y,1259,y+211),fill="#242637")
        d.text((26,y+8),item["runtimeEnum"],font=label,fill=item["labelTint"])
        for j, job in enumerate(JOBS):
            x = 18+j*620
            d.text((x+365,y+7),job.replace("_"," ").title(),font=label,fill="#eee4d6")
            for i, frame in enumerate(item["clips"][job]["frames"]):
                sprite = Image.open(ROOT/frame["png"])
                gallery.alpha_composite(sprite.resize((103,77),Image.Resampling.NEAREST),(x+i*103,y+31))
                d.text((x+i*103+12,y+108),str(frame["durationMs"])+" ms",font=label,fill="#b9aab1")
            # Larger final pose beside the earlier poses, preserving its 1:1 pixel scale.
            d.rectangle((x,y+127,x+617,y+210),fill="#1b1c2c")
            final = Image.open(ROOT/item["clips"][job]["frames"][-1]["png"])
            gallery.alpha_composite(final,(x+225,y+117))
            d.text((x+11,y+157),"Final pose",font=label,fill="#e5d8c7")
    save(gallery,"previews/pose-comparison.png")

    for item in manifest["tiers"]:
        for job, clip in item["clips"].items():
            frames = []
            for frame in clip["frames"]:
                stage = Image.new("RGBA",(384,288),"#242637")
                sprite = Image.open(ROOT/frame["png"])
                stage.alpha_composite(sprite.resize(stage.size,Image.Resampling.NEAREST))
                frames.append(stage.convert("RGB"))
            durations = [frame["durationMs"] for frame in clip["frames"]]
            # The final display pause is preview-only; the manifest drives Unity.
            durations[-1] += 450
            frames[0].save(ROOT/f"previews/{job}-{item['id']}.gif", save_all=True,
                           append_images=frames[1:],duration=durations,loop=0)

    repo = ROOT.parents[2]
    icons = [repo/"ArtSource/card-education-2026-10-16/roundtrip"/(name+".png")
             for name in ("card_old_fashioned","card_oolong","card_martini")]
    icons.append(repo/"Assets/_Project/Art/v1/hybrid_tea_martini/frames/hybrid_tea_martini_effect_right_00.png")
    tests = [("OLD FASHIONED","+15% Damage","1 > 2 stacks"),("OOLONG","+25% Attack speed","1 > 2 stacks"),
             ("MARTINI","+50% Damage","2 > 3 stacks"),("TEA MARTINI","+1 Hybrid recipe","Recipe unlock")]
    for height in (720,1080):
        scale = height/1080
        width = round(1920*scale)
        w,h = round(430*scale),round(620*scale)
        sheet = Image.new("RGBA",(width,height),"#101019")
        sd = ImageDraw.Draw(sheet)
        sd.text((round(40*scale),round(34*scale)),f"{height}p   /   DECORATION AND CONTENT MOCKUP",
                font=ImageFont.load_default(size=round(24*scale)),fill="#e5dfd4")
        for i, item in enumerate(manifest["tiers"]):
            x = round((60+i*460)*scale)
            y = round(178*scale)
            card = Image.open(ROOT/item["decoration"]["png"]).resize((w,h),Image.Resampling.NEAREST)
            sheet.alpha_composite(card,(x,y))
            icon = Image.open(icons[i]).convert("RGBA")
            icon = icon.crop(icon.getbbox())
            icon.thumbnail((round(112*scale),round(112*scale)),Image.Resampling.NEAREST)
            sheet.alpha_composite(icon,(x+round(46*scale),y+round(121*scale)))
            name, stat, count = tests[i]
            sd.text((x+w//2,y+round(49*scale)),item["runtimeEnum"],anchor="mm",
                    font=ImageFont.load_default(size=round(30*scale)),fill=item["labelTint"])
            sd.text((x+round(290*scale),y+round(170*scale)),name,anchor="mm",
                    font=ImageFont.load_default(size=round(23*scale)),fill="#fff5e9")
            sd.text((x+w//2,y+round(305*scale)),stat,anchor="mm",
                    font=ImageFont.load_default(size=round(28*scale)),fill="#efba80")
            sd.text((x+w//2,y+round(530*scale)),count,anchor="mm",
                    font=ImageFont.load_default(size=round(25*scale)),fill="#d0dff7")
        save(sheet,f"previews/card-comparison-{height}p.png")

    # Mixed Common / Rare / Epic, with the portrait anchor on the shared card center.
    size = (640, 360)
    center = (320, 190)
    epic = manifest["tiers"][2]["clips"]["bartender"]
    frames = []
    for entry in epic["frames"]:
        stage = Image.new("RGBA",size,"#101019")
        stage.alpha_composite(Image.open(ROOT/entry["png"]),(center[0]-64,center[1]-64))
        frames.append(stage)
    last = frames[-1]
    for progress in (0.35,0.7,1):
        stage = Image.blend(last,Image.new("RGBA",size,"#101019"),progress)
        for index, item in enumerate(manifest["tiers"][:3]):
            x = round(center[0]+(index-1)*160*(.88+.12*progress))
            width = round(143*(.94+.06*progress))
            height = round(207*(.94+.06*progress))
            tile = Image.open(ROOT/item["decoration"]["png"]).resize((width,height),Image.Resampling.NEAREST)
            icon = Image.open(icons[index]).convert("RGBA")
            icon = icon.crop(icon.getbbox())
            icon.thumbnail((40,40),Image.Resampling.NEAREST)
            tile.alpha_composite(icon,(12,37))
            d = ImageDraw.Draw(tile)
            d.text((width//2,17),item["runtimeEnum"],anchor="mm",fill=item["labelTint"])
            d.text((width//2,108),tests[index][1],anchor="mm",fill="#f3d09a")
            tile.putalpha(tile.getchannel("A").point(lambda a: round(a*progress)))
            stage.alpha_composite(tile,(x-width//2,center[1]-height//2))
        frames.append(stage)
    frames[0].save(ROOT/"previews/mixed-handoff.gif", save_all=True, append_images=frames[1:],
                   duration=[entry["durationMs"] for entry in epic["frames"]]+[20,20,930],loop=0)

    hybrid = Image.open(ROOT/manifest["tiers"][3]["hybridPanel"]["png"])
    hybrid = hybrid.resize((980,560),Image.Resampling.NEAREST)
    icon = Image.open(icons[3]).convert("RGBA")
    icon = icon.crop(icon.getbbox())
    icon.thumbnail((150,150),Image.Resampling.NEAREST)
    hybrid.alpha_composite(icon,(490-icon.width//2,115-icon.height//2))
    d = ImageDraw.Draw(hybrid)
    d.text((490,245),"HYBRID UNLOCK",font=ImageFont.load_default(size=48),
           anchor="mm",fill="#ffda72")
    d.text((490,355),"Rapier and bow / Automatically fires a support arrow.",
           font=ImageFont.load_default(size=28),anchor="mm",fill="#fff5e9")
    d.rounded_rectangle((340,433,640,517),radius=8,fill="#77502f")
    d.text((490,475),"Unlock",font=ImageFont.load_default(size=32),
           anchor="mm",fill="#fff5e9")
    save(hybrid,"previews/hybrid-panel.png")


if __name__ == "__main__":
    main()
