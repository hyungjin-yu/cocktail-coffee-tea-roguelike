"""Author pixel cels, export Aseprite sources and 720p review composites."""
from pathlib import Path
from math import cos, sin, pi, hypot
from struct import pack
import json
import random
import subprocess
import tempfile
import zlib

from PIL import Image, ImageColor, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[1]
ASE = Path("C:/Program Files/Aseprite/Aseprite.exe")
NN = Image.Resampling.NEAREST
PALETTES = [
    ("#142c2b", "#31644d", "#83b75b", "#dbed92", "#fffbea"),
    ("#301b27", "#934632", "#e08a3b", "#ffd17b", "#fff0cc"),
    ("#22253e", "#5d729b", "#a5c8d6", "#e0f2e8", "#ffffff"),
]
IDS = ["iba_margarita", "iba_sidecar", "iba_white_lady"]
NAMES = ["마가리타", "사이드카", "화이트 레이디"]
EFFECTS = ["솔트 림", "사이드카 질주", "화이트 아웃"]
ASSETS = {}


def canvas(size):
    image = Image.new("RGBA", size)
    return image, ImageDraw.Draw(image)


def diamond(draw, x, y, r, color):
    draw.polygon([(x, y-r), (x+r, y), (x, y+r), (x-r, y)], fill=color)


def glass(kind):
    image, draw = canvas((128, 128))
    ink, dark, mid, light, glint = PALETTES[kind]
    rims = [(27, 101, 60), (32, 96, 67), (39, 89, 70)]
    left, right, cup = rims[kind]
    lip = [42, 37, 34][kind]
    draw.ellipse((30, 108, 98, 119), fill=(10, 9, 15, 110))
    draw.polygon([(60, cup-3), (68, cup-3), (68, 99), (74, 103),
                  (86, 106), (86, 111), (80, 114), (47, 114), (42, 111),
                  (42, 106), (56, 102), (60, 98)], fill=dark, outline=ink, width=2)
    draw.line([(63, cup), (63, 99), (59, 104), (50, 107)], fill=light, width=2)
    draw.line((51, 110, 76, 110), fill=mid, width=2)
    shapes = [
        [(27,42),(101,42),(96,51),(81,55),(76,63),(68,68),(60,68),(52,63),(47,55),(32,51)],
        [(32,37),(96,37),(94,46),(88,56),(78,63),(70,66),(58,66),(50,63),(40,56),(34,46)],
        [(39,34),(89,34),(85,45),(71,65),(68,71),(60,71),(57,65),(43,45)],
    ]
    draw.polygon(shapes[kind], fill=dark, outline=ink, width=2)
    draw.polygon([(left+4, lip+2), (right-4, lip+2), (79, cup-6),
                  (66, cup+2), (56, cup-1), (left+8, lip+7)], fill=mid)
    draw.line([(left+5, lip+4), (left+10, lip+10), (56, cup+2)], fill=light, width=2)
    draw.line([(right-5, lip+5), (76, cup-3), (68, cup+5)], fill=glint)
    draw.ellipse((left, lip-6, right, lip+5), fill=ink)
    draw.ellipse((left+2, lip-5, right-2, lip+2), fill=mid, outline=light)
    draw.arc((left, lip-6, right, lip+4), 8, 174, glint, 2)
    draw.line((55, lip-1, 76, lip-1), fill=light)
    if kind == 0:
        for i, x in enumerate(range(left, right+1, 3)):
            y = lip + 5 - round(abs(x-64)**2 / 245)
            draw.rectangle((x, y, x+1+(i%3==0), y+1), fill=glint)
        draw.pieslice((75, 16, 102, 43), 205, 15, fill=dark, outline=ink, width=2)
        draw.pieslice((78, 19, 99, 40), 207, 12, fill=mid, outline=light)
        for x, y in [(81,25), (89,20), (96,25)]:
            draw.line((89,30,x,y), fill=light)
    elif kind == 1:
        draw.line([(81,36),(91,27),(101,28),(103,23),(98,18),(91,20)], fill=ink, width=5)
        draw.line([(81,35),(91,26),(101,27),(102,23),(98,20),(92,21)], fill=mid, width=3)
        draw.line([(84,33),(91,25),(100,26)], fill=light)
    else:
        draw.line((45, 31, 80, 31), fill=glint, width=2)
        draw.line((47, 32, 74, 32), fill=light)
        diamond(draw, 86, 25, 4, glint)
        diamond(draw, 79, 19, 2, light)
    return image


def stage(kind, size):
    art, d = canvas(size)
    _, dark, mid, _, _ = PALETTES[kind]
    rgb = ImageColor.getrgb(dark)
    cx, cy = size[0]//2, size[1]//2
    for y in range(cy-57, cy+58):
        for x in range(cx-131, cx+132):
            radius = hypot((x-cx)/2.24, y-cy)
            alpha = max(0, int((1-radius/57)*170)) // 12 * 12
            if alpha:
                d.point((x,y), fill=(*rgb,alpha))
    for side in (-1, 1):
        for j in range(3):
            x = cx + side*(54+j*10)
            if kind == 0:
                d.line([(x,cy-47+j*3),(x+side*29,cy-47+j*3),(x+side*48,cy-24+j*7)],
                       fill=dark)
                diamond(d,x+side*48,cy-24+j*7,2,mid)
            elif kind == 1:
                d.line([(x,cy+32-j*6),(x+side*19,cy+32-j*6),(x+side*48,cy+8-j*9)],
                       fill=dark,width=2)
                d.line((x+side*7,cy+28-j*6,x+side*17,cy+28-j*6),fill=mid)
            else:
                y = cy-20+j*22
                d.line([(x,y+22),(x+side*13,y),(x+side*25,y-10)],fill=dark)
                d.line([(x+side*2,y-6),(x+side*13,y),(x+side*25,y+4)],fill=mid)
    return art


def reveal(kind, quiet=False):
    size = (320, 180)
    base = stage(kind, size)
    drink = glass(kind)
    frames = []
    dark, mid, light, white = PALETTES[kind][1:]
    durations = [300,300,300,400,100,120,140,140,500,300]
    for i in range(len(durations)):
        burst, d = canvas(size)
        cup, _ = canvas(size)
        if i >= 4:
            width = [80, 118, 126, 118, 114, 114][i-4]
            if quiet:
                width = 114
            cup.alpha_composite(drink.resize((width,width), NN),
                                (160-width//2, 87-width//2))
        if not quiet and i:
            rng = random.Random(85 + kind)
            for j in range(44):
                a = rng.uniform(0, 2*pi)
                t = rng.uniform(.4, 1.1)
                r = [0,87,62,37,48,83,103,108,112,116][i]*t
                x = round(160 + cos(a)*r)
                y = round(95 + sin(a)*r*.53)
                color = [dark, mid, light, white][(j+i)%4]
                if kind == 0 and j%5 == 0:
                    d.pieslice((x-4,y-4,x+4,y+4), 200+j, 275+j, fill=mid, outline=light)
                elif kind == 1 and j%3 == 0:
                    d.line((x,y,x+8,y-3), fill=color, width=1)
                elif kind == 2 and j%4 == 0:
                    d.line((x-3,y,x+3,y), fill=color)
                    d.line((x,y-3,x,y+3), fill=color)
                else:
                    diamond(d,x,y,1+j%3,color)
            if 4 <= i <= 8:
                d.arc((47+i,32+i,273-i,154-i), 184, 348, mid)
                d.arc((50,41,270,145), 12, 157, dark)
            for y in range(134,160):
                attenuation = max(0, (143-y)/9)
                for x in range(103,217):
                    r,g,b,a = burst.getpixel((x,y))
                    if a:
                        burst.putpixel((x,y),(r,g,b,round(a*attenuation)))
        if quiet:
            backdrop, _ = canvas(size)
            frames.append([backdrop, burst, cup])
        else:
            frames.append([base, burst, cup])
    if quiet:
        frames = [frames[4]]
        durations = [1300]
    return frames, durations


def salt(quiet=False):
    frames = []
    rng = random.Random(123)
    flecks = [(rng.uniform(0,2*pi),rng.uniform(.66,.97),rng.randint(2,5))
              for _ in range(52)]
    for i, radius in enumerate([61,122,186,248,304,367,430,480] if not quiet else [480]):
        rim, d = canvas((960,960))
        motes, m = canvas(rim.size)
        alpha = [160,196,224,235,231,211,175,129][i] if not quiet else 126
        center = 480
        box = (center-radius,center-radius,center+radius-1,center+radius-1)
        d.ellipse(box, outline=(33,43,33,alpha), width=9 if not quiet else 7)
        d.ellipse(box, outline=(236,246,220,alpha), width=4)
        if not quiet:
            for j, (a, fraction, size) in enumerate(flecks):
                x = round(center + cos(a)*(radius-9))
                y = round(center + sin(a)*(radius-9))
                diamond(d,x,y,size,(255,250,222,alpha))
                if j%3 == i%3:
                    x = round(center + cos(a)*radius*fraction)
                    y = round(center + sin(a)*radius*fraction)
                    m.pieslice((x-7,y-7,x+7,y+7), j*11,j*11+92,
                               fill=(105,166,74,alpha),outline=(221,246,144,alpha),width=2)
        frames.append([rim,motes])
    return frames, [40,45,45,50,55,55,65,95] if not quiet else [260]


def wind():
    frames = []
    for i in range(6):
        image, d = canvas((224,80))
        for j, y in enumerate([12,20,34,51,63,72]):
            end = 175 - (j*11+i*19)%91
            start = max(2, end-43-(j*17+i*7)%69)
            d.line([(start,y+4),(start+14,y),(end,y),(end+7,y-2)], fill=(87,40,30,170), width=6)
            d.line([(start+2,y+4),(start+15,y),(end,y),(end+7,y-2)], fill=(250,158,68,184), width=3)
            d.line((end-16,y,end,y), fill=(255,230,166,213), width=2)
        frames.append([image])
    return frames, [75]*6


def speed_marker():
    image, d = canvas((64,36))
    for x in (12,26,40):
        pts = [(x,9),(x+10,18),(x,27)]
        d.line(pts,fill=(42,25,25,160),width=5)
        d.line(pts,fill=(255,220,166,166),width=2)
    return [[image]], [5000]


def frost(quiet=False):
    frames = []
    rng = random.Random(175)
    marks = [(side, p, rng.randrange(2,12), rng.randrange(2,6))
             for side in range(4) for p in range(7, 320 if side%2==0 else 180, 8)]
    for i, a in enumerate([130,175,127,75,32] if not quiet else [75]):
        art,d = canvas((320,180))
        for side,p,depth,w in marks:
            edge = min(p, (320 if side%2==0 else 180)-p)
            h = min(13,round(depth*(.50+.24*i)*(1.5 if edge<36 else .75)))
            if quiet:
                if edge>27:
                    continue
                h = min(h,3)
            def xy(u,v):
                return [(u,v),(319-v,u),(u,179-v),(v,u)][side]
            d.polygon([xy(p-w,0),xy(p+w,0),xy(p+2,h),xy(p,h//2)],
                      fill=(137,190,215,a))
            d.line([xy(p,1),xy(p+2,h)], fill=(226,249,249,a))
        frames.append([art])
    return frames, [45,55,65,80,105] if not quiet else [220]


def ice(quiet=False):
    image,d = canvas((32,32))
    body = [(16,2),(25,8),(27,20),(19,29),(11,29),(5,22),(6,10)]
    d.polygon(body,fill="#29475b",outline="#0e222e",width=2)
    d.line([(6,20),(9,10),(16,4),(23,9),(25,19),(18,27),(13,27)], fill="#c5f5f3",width=2)
    if not quiet:
        d.polygon([(16,5),(16,15),(8,21),(10,10)],fill="#83c4d9")
        d.polygon([(16,15),(23,11),(25,19),(18,27)],fill="#4f88a7")
    d.line([(16,7),(16,17),(20,21)], fill="#efffff",width=2)
    return [[image]], [4000]


def aseprite(path, frames, durations, layers):
    """Write standard RGBA compressed cels, then round-trip via Aseprite CLI."""
    width,height = frames[0][0].size
    def chunk(kind,data):
        return pack("<IH",len(data)+6,kind)+data
    blocks = []
    for i, (images, duration) in enumerate(zip(frames,durations)):
        chunks = []
        if i == 0:
            for layer in layers:
                name = layer.encode()
                data = pack("<HHHHHHB3xH",3,0,0,0,0,0,255,len(name))
                chunks.append(chunk(0x2004,data+name))
        for n,image in enumerate(images):
            box = image.getbbox()
            if box is None:
                continue
            cropped = image.crop(box)
            data = pack("<HhhBH7xHH",n,*box[:2],255,2,*cropped.size)
            chunks.append(chunk(0x2005,data+zlib.compress(cropped.tobytes())))
        body = b"".join(chunks)
        blocks.append(pack("<IHHH2xI",16+len(body),0xf1fa,len(chunks),duration,0)+body)
    body = b"".join(blocks)
    header = pack("<IHHHHHIH8xB3xHBB",128+len(body),0xa5e0,len(frames),
                  width,height,32,1,100,0,0,1,1)
    with tempfile.TemporaryDirectory(dir=ROOT) as scratch:
        raw = Path(scratch)/path.name
        raw.write_bytes(header.ljust(128,b"\0")+body)
        subprocess.run([str(ASE),"-b",str(raw),"--save-as",str(path)],check=True)
        check = Path(scratch)/"frames.png"
        metadata = Path(scratch)/"frames.json"
        subprocess.run([str(ASE),"-b",str(path),"--sheet-type","horizontal",
                        "--sheet",str(check),"--data",str(metadata)],check=True)
        roundtrip = Image.open(check).convert("RGBA")
        assert roundtrip.size == (width*len(frames),height)
        info = json.loads(metadata.read_text(encoding="utf-8"))
        assert [frame["duration"] for frame in info["frames"].values()] == durations
        flattened = []
        for n,images in enumerate(frames):
            exported = roundtrip.crop((n*width,0,(n+1)*width,height))
            expected = merged(images)
            assert exported.getbbox() == expected.getbbox(), (path,n)
            flattened.append(exported)
        return flattened


def merged(images):
    output,_ = canvas(images[0].size)
    for image in images:
        output.alpha_composite(image)
    return output


def export(name, data, layers, anchor, extent, **extra):
    frames,durations = data
    width,height = frames[0][0].size
    source = Path("aseprite")/f"{name}.aseprite"
    sprite = Path("png")/f"{name}.png"
    for folder in (source.parent, sprite.parent):
        (ROOT/folder).mkdir(exist_ok=True)
    flattened = aseprite(ROOT/source,frames,durations,layers)
    cols = min(4,len(frames))
    rows = (len(frames)+cols-1)//cols
    sheet,_ = canvas((cols*width, rows*height))
    for i,image in enumerate(flattened):
        sheet.paste(image, ((i%cols)*width,(i//cols)*height))
    sheet.save(ROOT/sprite)
    ASSETS[name] = dict(source=source.as_posix(),png=sprite.as_posix(),
                        frame_px=[width,height],sheet_columns=cols,
                        frames_ms=durations,anchor_px_top_left=anchor,display=extent,**extra)
    return flattened


def art(name):
    frames = REPO/"Assets/_Project/Art/v1"/name/"frames"
    idle = frames/f"{name}_idle_right_00.png"
    return Image.open(idle if idle.exists() else next(frames.glob("*_right_00.png"))).convert("RGBA")


def background(kind):
    prefix = ["bg_barwarm",None,"bg_tea"][kind]
    if prefix:
        folder = REPO/"Assets/_Project/Resources/ArtExtra"
        def part(key):
            return Image.open(next((folder/f"{prefix}_{key}").glob("*.png"))).convert("RGBA")
    else:
        def part(key):
            return art(f"cafe_{key}")
    screen = Image.new("RGBA",(1280,720),(21,17,25,255))
    for key, y, end in [("wall",0,160),("floor",160,720)]:
        texture = part(key)
        texture = texture.resize(tuple(round(v*60/128) for v in texture.size),NN)
        section,_ = canvas((1280,end-y))
        if key == "wall":
            texture = texture.crop((0,texture.height-(end-y),texture.width,texture.height))
        for tx in range(0,1280,texture.width):
            for ty in range(0,end-y,texture.height):
                section.alpha_composite(texture,(tx,ty))
        screen.alpha_composite(section,(0,y))
    for key, x, y in [("counter",80,160),("lamp",740,115)]:
        prop = part(key)
        prop = prop.resize(tuple(round(v*60/128) for v in prop.size),NN)
        screen.alpha_composite(prop,(x,y-prop.height))
    screen.alpha_composite(Image.new("RGBA",screen.size,(18,15,21,43)))
    return screen


def put(image, sprite, center):
    image.alpha_composite(sprite,(center[0]-sprite.width//2,center[1]-sprite.height//2))


def font(size):
    return ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf",size)


def body(name, height):
    sprite = art(name)
    sprite = sprite.crop(sprite.getbbox())
    return sprite.resize((round(sprite.width*height/sprite.height),height),NN)


def mono(image):
    return ImageOps.grayscale(image).convert("RGBA")


def review(kind, discovery, low, combat):
    screen = background(kind)
    folder = ROOT/"preview"
    folder.mkdir(exist_ok=True)
    for reduced in (False,True):
        suffix = "low-gray" if reduced else "normal"
        sample = low[0] if reduced else discovery[8]
        found = screen.copy()
        found.alpha_composite(Image.new("RGBA",found.size,(11,9,16,220)))
        found.alpha_composite(sample.resize(found.size, NN))
        draw=ImageDraw.Draw(found)
        for y,size,label,color in [
            (113,24,"정식 레시피 발견",PALETTES[kind][3]),
            (586,47,NAMES[kind],"#fff7ee"),(636,24,EFFECTS[kind],PALETTES[kind][3]),
            (678,18,"도감 · 정식 레시피에 기록했습니다","#d7cfd8"),
        ]:
            draw.text((640,y),label,font=font(size),fill=color,anchor="mm")
        if reduced:
            found = mono(found)
        found.save(folder/f"{IDS[kind]}-discovery-{suffix}.png")
        fight = combat(screen.copy(),reduced)
        if reduced:
            fight = mono(fight)
        fight.save(folder/f"{IDS[kind]}-combat-{suffix}.png")


def combat_preview(kind, scene, reduced, fx):
    player = (640,405)
    p = body("bartender",53)
    if kind == 0:
        put(scene,fx["ring_low" if reduced else "ring"][-1].resize((480,480),NN),player)
    if kind == 1:
        if reduced:
            put(scene,fx["speed_low"][0].resize((32,18),NN),(640,444))
        else:
            ghost = Image.new("RGBA",p.size,(249,153,79,0))
            for dx,dy,alpha in [(104,14,50),(72,9,82),(37,4,114)]:
                ghost.putalpha(p.getchannel("A").point(lambda a: a*alpha//255))
                put(scene,ghost,(640-dx,405+dy))
            line = fx["wind"][2].resize((112,40),NN)
            put(scene,line,(582,428))
    positions = [(475,292),(808,293),(858,445),(425,531),(670,547),(1026,337)]
    targets = ["lime_wedge","lemon_brute","shaker_golem","lime_wedge","martini_spirit","espresso_leviathan"]
    telegraph,d = canvas(scene.size)
    d.rectangle((715,472,1001,498),fill=(137,40,40,215),outline="#ff9f98",width=2)
    for x in range(724,995,16):
        d.line([(x,477),(x+7,485),(x,493)],fill="#f57965",width=2)
    scene.alpha_composite(telegraph)
    for (x,y),name in zip(positions,targets):
        boss = name == "espresso_leviathan"
        enemy = body(name,98 if boss else 56)
        put(scene,enemy,(x,y))
        if kind == 2 and not boss:
            put(scene,fx["ice_low" if reduced else "ice"][0].resize((20,20),NN),(x,y-43))
    put(scene,p,player)
    if kind == 2:
        scene.alpha_composite(fx["frost_low" if reduced else "frost"][0 if reduced else 1].resize(scene.size,NN))
    d = ImageDraw.Draw(scene)
    d.rectangle((1000,602,1250,673),fill="#251f25",outline="#746473",width=1)
    d.text((1014,617),EFFECTS[kind],font=font(19),fill="#fff6e9")
    d.text((1014,644),"사용함 · 4초 남음" if kind==1 else "사용함",font=font(16),fill="#d9c9ce")
    return scene


def main():
    fx = {}
    fx["ring"] = export("salt_ring",salt(),["Salt crystal rim","Lime flecks"],[480,480],
                        {"diameter_world":8}, low="salt_ring_low")
    fx["ring_low"] = export("salt_ring_low",salt(True),["Quiet range outline","Lime flecks"],
                            [480,480],{"diameter_world":8}, static=True)
    fx["wind"] = export("sidecar_wind",wind(),["Wind streaks"],[210,40],
                        {"size_world":[224/128,80/128]},loop_until_ms=5000,low="sidecar_low")
    fx["speed_low"] = export("sidecar_low",speed_marker(),["Static chevrons"],
                             [32,18],{"size_world":[.5,.28125]},static=True)
    fx["frost"] = export("whiteout_edge",frost(),["Perimeter frost"],[160,90],
                         {"viewport_fit":"edge anchored; see HANDOFF"},low="whiteout_edge_low")
    fx["frost_low"] = export("whiteout_edge_low",frost(True),["Static corner crystals"],
                             [160,90],{"viewport_fit":"corner anchored"},static=True)
    fx["ice"] = export("whiteout_hold",ice(),["Enemy ice badge"],[16,32],
                       {"size_ref_720":[20,20]},low="whiteout_hold_low")
    fx["ice_low"] = export("whiteout_hold_low",ice(True),["Static enemy ice badge"],[16,32],
                           {"size_ref_720":[20,20]},static=True)
    for i, key in enumerate(IDS):
        standard = export(key+"_discovery",reveal(i),["Backdrop","Burst","Hero glass"],[160,90],
                          {"reference_720_px":[1280,720]},low=key+"_discovery_low", gate="first_discovery_only")
        low = export(key+"_discovery_low",reveal(i,True),["Backdrop","Burst","Hero glass"],[160,90],
                     {"reference_720_px":[1280,720]},static=True,start_ms=1300,gate="first_discovery_only")
        review(i,standard,low,lambda screen,reduced: combat_preview(i,screen,reduced,fx))
    contract = dict(schema_version=1, reference_resolution=[1280,720], assets=ASSETS,
                    reveal_short_ms=900, sidecar_ghost={"spacing_ms":75,"life_ms":210,"max_visible":3,
                    "source":"snapshot of current player sprite, flip and scale","opacity":[.43,.28,.15],
                    "active_window_ms":5000,"low":"sidecar_low"})
    (ROOT/"manifest.json").write_text(json.dumps(contract,indent=2)+"\n",encoding="utf-8")


if __name__ == "__main__":
    main()
