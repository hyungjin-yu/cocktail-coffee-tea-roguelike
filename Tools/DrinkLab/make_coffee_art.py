"""커피 제조 아트(2026-10-21) 1차 픽셀 원본 생성기.

PixelLab 생성은 Aseprite GUI 확장이라 헤드리스로 부를 수 없어, 이 스크립트가 정해진 크기·팔레트로 1차 픽셀을 찍고
Tools/DrinkLab/coffee_art_aseprite.lua(Aseprite -b)가 .aseprite 원본 저장 + 인덱스 팔레트 정리 + PNG 재출력을 한다.

출력: ArtSource/coffee-brew-2026-10-21/raw/*.png (1차) → Aseprite → ArtSource/.../*.aseprite, Assets/_Project/Resources/DrinkLab/*.png
앵커(픽셀, 왼쪽 위 원점)·액체 영역은 Resources/DrinkLab/anchors.json(0~1, 왼쪽 아래 원점)과 dl_alpha_bottom.json에 합친다.
사용: python Tools/DrinkLab/make_coffee_art.py  (저장소 루트에서)
"""
import json
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtSource", "coffee-brew-2026-10-21", "raw")
RES = os.path.join(ROOT, "Assets", "_Project", "Resources", "DrinkLab")

# 팔레트(기존 제조대 아트의 어두운 갈색 외곽선·따뜻한 금속 톤에 맞춤)
OUT = (40, 30, 28, 255)
OUT2 = (64, 48, 42, 255)
STEEL_D = (96, 100, 110, 255)
STEEL = (150, 156, 166, 255)
STEEL_L = (196, 202, 210, 255)
STEEL_H = (232, 236, 240, 255)
BLACK = (52, 46, 50, 255)
BLACK_L = (78, 70, 74, 255)
RED = (176, 52, 44, 255)
RED_L = (214, 86, 64, 255)
WOOD = (120, 76, 44, 255)
WOOD_L = (156, 104, 62, 255)
BEAN_D = (54, 30, 18, 255)
BEAN = (92, 54, 30, 255)
BEAN_L = (132, 82, 46, 255)
KRAFT = (184, 142, 92, 255)
KRAFT_D = (148, 108, 66, 255)
KRAFT_L = (214, 178, 128, 255)
CREAM = (238, 228, 208, 255)
CREAM_D = (206, 192, 168, 255)
GLASS = (200, 222, 236, 70)
GLASS_E = (150, 180, 200, 200)
GLASS_H = (236, 246, 252, 150)
LAMP = (255, 196, 80, 255)
LAMP_OFF = (120, 84, 40, 255)
FLAME_O = (240, 120, 40, 255)
FLAME_Y = (255, 210, 90, 255)
FLAME_B = (90, 140, 240, 255)
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), CLEAR)
        self.px = self.img.load()

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def box(self, x0, y0, x1, y1, fill, outline=OUT):
        self.rect(x0, y0, x1, y1, outline)
        if x1 - x0 >= 2 and y1 - y0 >= 2:
            self.rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, fill)

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def ellipse(self, cx, cy, rx, ry, fill, outline=OUT):
        for y in range(self.h):
            for x in range(self.w):
                d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
                if d <= 1.0:
                    inner = ((x + 0.5 - cx) / max(0.5, rx - 1)) ** 2 + ((y + 0.5 - cy) / max(0.5, ry - 1)) ** 2
                    self.set(x, y, fill if inner <= 1.0 else outline)

    def poly_rows(self, rows, fill, outline=OUT):
        """rows: {y: (x0, x1)} 행별 가로 범위. 테두리는 각 행 양 끝 + 맨 위/아래 행."""
        ys = sorted(rows)
        for y in ys:
            x0, x1 = rows[y]
            edge = y in (ys[0], ys[-1])
            for x in range(x0, x1 + 1):
                self.set(x, y, outline if edge or x in (x0, x1) else fill)


def bean(c, cx, cy, flip=False):
    c.ellipse(cx, cy, 5.5, 4, BEAN)
    for i in range(-3, 4):
        x = int(cx + i)
        y = int(cy + (0.6 * i if not flip else -0.6 * i) * 0.4)
        c.set(x, y, BEAN_D)
    c.set(int(cx - 2), int(cy - 2), BEAN_L)
    c.set(int(cx - 1), int(cy - 2), BEAN_L)


# 따르는 주둥이를 오른쪽(손잡이 왼쪽)으로: 잔이 도구 오른쪽에 놓이고 제조대 공정이 왼쪽→오른쪽으로 흐른다.
# 그리기는 주둥이 왼쪽 기준으로 하고 저장·앵커에서 좌우를 뒤집는다.
FLIP = {"dl_coffee_carafe", "dl_coffee_moka"}


def save(c, name):
    os.makedirs(RAW, exist_ok=True)
    img = c.img
    if name.replace("_mask", "") in FLIP:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    img.save(os.path.join(RAW, name + ".png"))


def mask_from(w, h, rows):
    m = Canvas(w, h)
    for y, (x0, x1) in rows.items():
        m.hline(x0, x1, y, WHITE)
    return m


anchors = {}  # assetId -> dict(px 좌표, 왼쪽 위 원점)


def anchor(asset, w, h, grip=None, pour=None, mouth=None, liquid=None):
    anchors[asset] = dict(w=w, h=h, grip=grip, pourLip=pour, mouth=mouth, liquid=liquid)


# ── 아이콘·원두 통·입자 ──
c = Canvas(32, 32)
bean(c, 12, 14)
bean(c, 20, 19, flip=True)
c.ellipse(16, 27, 10, 2.5, (0, 0, 0, 0), (0, 0, 0, 0))
save(c, "dl_icon_coffee_bean")

c = Canvas(32, 32)  # 크래프트 원두 봉투(입구 접힘 + 원두 창)
rows = {y: (7, 24) for y in range(6, 31)}
c.poly_rows(rows, KRAFT)
c.rect(8, 7, 23, 9, KRAFT_L)
c.hline(7, 24, 10, OUT2)
c.rect(8, 11, 9, 29, KRAFT_D)
c.box(11, 15, 20, 24, BEAN)
for (x, y) in [(13, 17), (17, 19), (14, 21), (18, 22)]:
    c.set(x, y, BEAN_L); c.set(x + 1, y, BEAN_D)
c.hline(11, 20, 26, OUT2)
save(c, "dl_tin_coffee_bean")
anchor("dl_tin_coffee_bean", 32, 32, grip=(16, 20), pour=(23, 6), mouth=(16, 6))

for i in range(3):
    c = Canvas(8, 8)
    c.ellipse(4, 4, 3.5 - (i == 2) * 0.5, 2.6, BEAN)
    c.set(2 + i % 2, 3 + (i == 1), BEAN_D); c.set(3 + i % 2, 4, BEAN_D); c.set(4, 4 - (i == 1), BEAN_D)
    c.set(3, 2, BEAN_L)
    save(c, f"dl_leaf_coffee_bean_{i:02}")

# ── 원두 스쿱(손잡이 왼쪽, 오목한 컵 오른쪽) ──
c = Canvas(40, 20)
c.box(1, 9, 17, 12, WOOD_L)
c.hline(2, 16, 10, WOOD)
bowl = {y: (18 + max(0, y - 14) // 1, 38 - max(0, y - 14) // 1) for y in range(6, 19)}
bowl = {y: (18 + (y - 12) // 2 if y > 12 else 18, 38 - (y - 12) // 2 if y > 12 else 38) for y in range(6, 19)}
c.poly_rows(bowl, STEEL)
for y in range(7, 18):
    c.set(bowl[y][0] + 1, y, STEEL_L)
c.hline(19, 37, 7, STEEL_H)
save(c, "dl_coffee_scoop")
m = mask_from(40, 20, {y: (bowl[y][0] + 1, bowl[y][1] - 1) for y in range(7, 18)})
save(m, "dl_coffee_scoop_mask")
anchor("dl_coffee_scoop", 40, 20, grip=(8, 10), pour=(38, 7), mouth=(28, 7), liquid=(19, 7, 37, 17))

# ── 에스프레소 머신 64×64(위 원두 호퍼, 가운데 그룹헤드·추출구, 아래 받침) ──
def espresso(lamp_on, gauge):
    c = Canvas(64, 64)
    # 호퍼(유리, 위)
    hop = {y: (14 + (y - 1) // 2, 34 - (y - 1) // 2) for y in range(1, 10)}
    c.poly_rows(hop, (150, 110, 80, 160))
    c.rect(17, 5, 29, 8, BEAN)
    c.set(19, 6, BEAN_L); c.set(24, 7, BEAN_L); c.set(27, 6, BEAN_D)
    c.box(20, 10, 28, 12, BLACK_L)
    # 본체
    c.box(6, 12, 57, 30, RED)
    c.rect(7, 13, 56, 14, RED_L)
    c.rect(7, 13, 8, 29, RED_L)
    c.box(10, 16, 53, 19, STEEL_L)  # 상판 금속 띠
    # 압력계
    c.ellipse(46, 24.5, 4, 4, CREAM)
    nx = [(46, 22), (48, 23), (49, 25)][gauge]
    c.set(46, 24, OUT); c.set(nx[0], nx[1], OUT)
    # 램프
    c.box(12, 22, 15, 25, LAMP if lamp_on else LAMP_OFF)
    # 그룹헤드 + 포타필터(손잡이 왼쪽). 잔(데미타스 18px)이 받침(아래 6행) 위에 들어가는 높이.
    c.box(24, 30, 40, 33, STEEL)
    c.box(26, 34, 38, 36, STEEL_D)
    c.box(14, 33, 25, 35, BLACK)
    c.vline(31, 37, 37, STEEL_D); c.vline(33, 37, 37, STEEL_D)  # 추출구 두 갈래
    # 기둥 + 받침
    c.box(6, 30, 13, 59, RED)
    c.box(50, 30, 57, 59, RED)
    c.box(14, 58, 49, 63, STEEL)
    for x in range(16, 48, 3):
        c.vline(x, 59, 62, STEEL_D)
    c.box(4, 59, 59, 63, STEEL_D)
    c.rect(15, 59, 48, 59, STEEL_H)
    return c

save(espresso(False, 0), "dl_coffee_espresso")
for i, (on, g) in enumerate([(False, 0), (True, 1), (True, 2), (True, 1)]):
    save(espresso(on, g), f"dl_coffee_espresso_brew_{i:02}")
anchor("dl_coffee_espresso", 64, 64, grip=(32, 32), pour=(32, 37), mouth=(24, 1), liquid=(27, 34, 37, 35))
m = mask_from(64, 64, {y: (27, 37) for y in range(34, 36)})
save(m, "dl_coffee_espresso_mask")

# ── 핸드 드립: 드리퍼(원뿔) + 서버(유리, 주둥이 왼쪽·손잡이 오른쪽) ──
c = Canvas(40, 24)
cone = {y: (2 + y * 9 // 16, 37 - y * 9 // 16) for y in range(1, 17)}
c.poly_rows(cone, CREAM)
for y in range(2, 16):
    c.set(cone[y][0] + 1, y, CREAM_D)
    if y % 3 == 0:
        c.hline(cone[y][0] + 3, cone[y][1] - 3, y, CREAM_D)  # 안쪽 리브
c.hline(0, 39, 1, OUT); c.hline(1, 38, 2, CREAM)
c.box(10, 17, 29, 19, CREAM)  # 받침 테
c.box(17, 20, 22, 23, CREAM_D)  # 배출구
save(c, "dl_coffee_dripper")
anchor("dl_coffee_dripper", 40, 24, grip=(20, 10), pour=(20, 23), mouth=(20, 2))

c = Canvas(40, 48)
body = {}
for y in range(8, 47):
    t = (y - 8) / 38
    half = 11 + int(4 * (1 - (2 * t - 1) ** 2))
    body[y] = (18 - half, 18 + half)
c.poly_rows(body, GLASS, GLASS_E)
c.hline(5, 31, 7, OUT2); c.hline(5, 31, 8, GLASS_E)
c.set(3, 6, GLASS_E); c.set(2, 5, GLASS_E); c.set(4, 7, GLASS_E)  # 주둥이
for y in range(12, 40):
    c.set(body[y][0] + 2, y, GLASS_H)
c.rect(31, 14, 37, 15, WOOD); c.rect(36, 16, 37, 34, WOOD); c.rect(31, 35, 37, 36, WOOD)  # 손잡이
c.vline(37, 16, 34, OUT2)
c.hline(body[46][0], body[46][1], 47, OUT2)
save(c, "dl_coffee_carafe")
m = mask_from(40, 48, {y: (body[y][0] + 1, body[y][1] - 1) for y in range(9, 46)})
save(m, "dl_coffee_carafe_mask")
anchor("dl_coffee_carafe", 40, 48, grip=(36, 25), pour=(2, 5), mouth=(18, 8), liquid=(body[30][0] + 1, 9, body[30][1] - 1, 45))

# ── 모카포트 40×56(위 칸 + 허리 + 아래 보일러, 주둥이 왼쪽·손잡이 오른쪽, 불투명 금속) ──
c = Canvas(40, 56)
top = {y: (6 + (y - 6) // 6, 28 - (y - 6) // 6) for y in range(6, 27)}
c.poly_rows(top, STEEL)
c.box(14, 2, 20, 5, BLACK)  # 뚜껑 꼭지
c.hline(5, 29, 6, OUT)
c.set(3, 5, OUT); c.set(4, 6, OUT); c.set(2, 4, OUT); c.set(4, 5, STEEL)  # 주둥이
for y in range(8, 26):
    c.set(top[y][0] + 1, y, STEEL_H); c.set(top[y][0] + 2, y, STEEL_L); c.set(top[y][1] - 2, y, STEEL_D)
c.box(11, 27, 25, 29, STEEL_D)  # 허리
bot = {y: (6 + (y - 30) // 8, 30 - (y - 30) // 8) for y in range(30, 54)}
bot = {y: (6 + (53 - y) // 8, 30 - (53 - y) // 8) for y in range(30, 54)}
c.poly_rows(bot, STEEL)
for y in range(31, 53):
    c.set(bot[y][0] + 1, y, STEEL_H); c.set(bot[y][0] + 2, y, STEEL_L); c.set(bot[y][1] - 2, y, STEEL_D)
c.set(26, 42, BLACK_L); c.set(27, 42, BLACK_L)  # 안전 밸브
c.box(28, 9, 37, 11, BLACK); c.box(35, 12, 38, 24, BLACK); c.rect(36, 13, 37, 23, BLACK_L)  # 손잡이
save(c, "dl_coffee_moka")
m = mask_from(40, 56, {y: (top[y][0] + 2, top[y][1] - 2) for y in range(8, 26)})
save(m, "dl_coffee_moka_mask")
anchor("dl_coffee_moka", 40, 56, grip=(36, 18), pour=(2, 4), mouth=(17, 6), liquid=(top[16][0] + 2, 8, top[16][1] - 2, 25))

for i in range(3):
    c = Canvas(16, 8)
    for x in range(1, 15, 3):
        h = 3 + ((x + i * 2) % 4)
        c.vline(x, 7 - h, 7, FLAME_O); c.vline(x + 1, 8 - h, 7, FLAME_Y)
        c.set(x, 7, FLAME_B); c.set(x + 1, 7, FLAME_B)
    save(c, f"dl_coffee_flame_{i:02}")

# ── 잔: 데미타스(에스프레소) / 컵(모카) / 머그(핸드 드립). 하얀 도자기, 손잡이 오른쪽, 안쪽 액체 영역 ──
def cup(w, h, top_y, wall, handle, name, saucer):
    c = Canvas(w, h)
    bottom = h - (3 if saucer else 1)
    cw = w - 8
    rows = {}
    for y in range(top_y, bottom + 1):
        t = (y - top_y) / max(1, bottom - top_y)
        inset = int(t * t * (2 if saucer else 1))
        rows[y] = (1 + inset, cw - inset)
    c.poly_rows(rows, CREAM)
    for y in range(top_y + 1, bottom):
        c.set(rows[y][0] + 1, y, WHITE)
        c.set(rows[y][1] - 1, y, CREAM_D)
    hx = cw + 1
    c.rect(hx, top_y + handle[0], hx + 4, top_y + handle[0], OUT); c.rect(hx + 4, top_y + handle[0], hx + 4, top_y + handle[1], OUT)
    c.rect(hx, top_y + handle[1], hx + 4, top_y + handle[1], OUT); c.rect(hx + 1, top_y + handle[0] + 1, hx + 3, top_y + handle[1] - 1, CREAM)
    c.rect(hx + 1, top_y + handle[0] + 2, hx + 2, top_y + handle[1] - 2, CLEAR)
    if saucer:
        c.box(0, h - 3, w - 5, h - 1, CREAM)
    save(c, name)
    inner = {y: (rows[y][0] + wall, rows[y][1] - wall) for y in range(top_y + 1, bottom - wall + 1)}
    save(mask_from(w, h, inner), name + "_mask")
    ys = sorted(inner)
    anchor(name, w, h, grip=(hx + 2, (top_y + bottom) // 2), pour=(rows[top_y][0], top_y), mouth=((rows[top_y][0] + rows[top_y][1]) // 2, top_y),
           liquid=(inner[ys[0]][0], ys[0], inner[ys[0]][1], ys[-1]))

cup(24, 18, 4, 1, (2, 7), "dl_coffee_demitasse", True)
cup(32, 26, 4, 1, (3, 11), "dl_coffee_cup", True)
cup(36, 36, 3, 2, (4, 18), "dl_coffee_mug", False)

# ── 스팀 우유: 스테인리스 밀크 피처(병 자리, 주둥이 오른쪽 위·손잡이 왼쪽) + 아이콘 ──
MILK = (246, 240, 226, 255)
c = Canvas(28, 40)
pit = {y: (5 + max(0, (14 - y)) // 5, 22 - max(0, (14 - y)) // 5) for y in range(8, 39)}
c.poly_rows(pit, STEEL)
for y in range(9, 38):
    c.set(pit[y][0] + 1, y, STEEL_H); c.set(pit[y][0] + 2, y, STEEL_L); c.set(pit[y][1] - 1, y, STEEL_D)
c.set(23, 7, OUT); c.set(24, 6, OUT); c.set(25, 5, OUT); c.set(23, 8, STEEL_L); c.set(24, 7, STEEL_L)  # 주둥이
c.hline(7, 21, 9, MILK); c.hline(8, 20, 10, CREAM_D)  # 우유 거품
c.rect(1, 14, 4, 15, BLACK); c.rect(1, 16, 2, 28, BLACK); c.rect(1, 29, 4, 30, BLACK)  # 손잡이
c.hline(6, 21, 39, OUT2)
save(c, "dl_bottle_steamed_milk")
anchor("dl_bottle_steamed_milk", 28, 40, grip=(8, 24), pour=(25, 5), mouth=(14, 8))

c = Canvas(32, 32)
cup_rows = {y: (8 + (y - 12) // 6, 23 - (y - 12) // 6) for y in range(12, 28)}
c.poly_rows(cup_rows, MILK)
c.ellipse(16, 12, 8.5, 3, (255, 252, 244, 255), OUT)
c.ellipse(16, 12, 4, 1.5, (210, 170, 120, 255), (190, 150, 100, 255))  # 라테 아트 점
c.rect(24, 15, 27, 16, OUT); c.rect(27, 16, 27, 22, OUT); c.rect(24, 22, 27, 23, OUT)
c.hline(5, 26, 28, OUT2)
save(c, "dl_icon_steamed_milk")


def write_anchor_table():
    """anchors.json·dl_alpha_bottom.json에 커피 항목을 합친다(같은 assetId는 교체, 다른 항목은 그대로)."""
    def norm(pt, w, h):
        return [] if pt is None else [(pt[0] + 0.5) / w, 1 - (pt[1] + 0.5) / h]

    path = os.path.join(RES, "anchors.json")
    with open(path, encoding="utf-8") as f:
        table = json.load(f)
    entries = [e for e in table["entries"] if e["assetId"] not in anchors]
    for aid, a in anchors.items():
        w, h = a["w"], a["h"]
        liq = a["liquid"]
        if aid in FLIP:
            fx = lambda p: None if p is None else (w - 1 - p[0], p[1])
            a = dict(a, grip=fx(a["grip"]), pourLip=fx(a["pourLip"]), mouth=fx(a["mouth"]))
            if liq is not None:
                liq = (w - 1 - liq[2], liq[1], w - 1 - liq[0], liq[3])
        entries.append({
            "assetId": aid,
            "pourLip": norm(a["pourLip"], w, h),
            "mouth": norm(a["mouth"], w, h),
            "grip": norm(a["grip"], w, h),
            "liquid": [] if liq is None else [liq[0] / w, 1 - (liq[3] + 1) / h, (liq[2] + 1) / w, 1 - liq[1] / h],
            "width": w, "height": h,
        })
    table["entries"] = entries
    with open(path, "w", encoding="utf-8") as f:
        json.dump(table, f, ensure_ascii=False, indent=4)


if __name__ == "__main__":
    write_anchor_table()
    print("raw pngs:", len(os.listdir(RAW)), "anchors:", len(anchors))


def write_alpha_bottom():
    """dl_alpha_bottom.json에 커피 아트의 아래쪽 완전 투명 행 수를 합친다(Resources의 최종 PNG 기준)."""
    path = os.path.join(RES, "dl_alpha_bottom.json")
    with open(path, encoding="utf-8") as f:
        table = json.load(f)
    for name in sorted(os.listdir(RES)):
        if ("coffee" not in name and "steamed_milk" not in name) or not name.endswith(".png") or "_mask" in name:
            continue
        im = Image.open(os.path.join(RES, name)).convert("RGBA")
        px = im.load()
        n = 0
        for y in range(im.height - 1, -1, -1):
            if any(px[x, y][3] > 0 for x in range(im.width)):
                break
            n += 1
        table["rows"][name[:-4]] = n
    with open(path, "w", encoding="utf-8") as f:
        json.dump(table, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    write_alpha_bottom()  # Aseprite 출력 뒤 다시 실행해도 안전(같은 키 덮어쓰기)
