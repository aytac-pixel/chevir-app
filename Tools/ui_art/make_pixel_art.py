"""Draws the pixel-art pictures of the app: story scenes, the story girl and hands, unit icons, home-screen
decorations and the night-theme panels.

Everything is drawn pixel by pixel at its real (small) size and saved without smoothing; Unity shows it with
Point filtering (see CevirSpriteImportRules), so each art pixel stays a crisp square on screen.
Run from the repository root:  python3 Tools/ui_art/make_pixel_art.py   (add --preview to also write
enlarged copies to Tools/ui_art/preview/ for checking by eye).
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw

ROOT = os.path.join("Assets", "Core", "Sprites")
STORY = os.path.join(ROOT, "Story")
DECOR = os.path.join(ROOT, "Decor")
UNITS = os.path.join(ROOT, "Units")
UI = os.path.join(ROOT, "UI")
PREVIEW = os.path.join("Tools", "ui_art", "preview")

CLEAR = (0, 0, 0, 0)
INK = (10, 25, 49, 255)
WHITE = (255, 255, 255, 255)

W, H = 216, 384  # story scene: 1080 x 1920 on screen at 5x


def rgb(hex_value, alpha=255):
    hex_value = hex_value.lstrip("#")
    return (int(hex_value[0:2], 16), int(hex_value[2:4], 16), int(hex_value[4:6], 16), alpha)


# ---------------------------------------------------------------------------------------------- drawing kit

class Canvas:
    def __init__(self, w, h, fill=CLEAR):
        self.img = Image.new("RGBA", (w, h), fill)
        self.px = self.img.load()
        self.w, self.h = w, h
        self.d = ImageDraw.Draw(self.img)

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = c

    def rect(self, x, y, w, h, c):
        if w <= 0 or h <= 0:
            return
        self.d.rectangle([x, y, x + w - 1, y + h - 1], fill=c)

    def hline(self, x, y, w, c):
        self.rect(x, y, w, 1, c)

    def vline(self, x, y, h, c):
        self.rect(x, y, 1, h, c)

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                    self.put(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                    self.put(x, y, c)

    def dither(self, x, y, w, h, c, phase=0):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                if (xx + yy + phase) % 2 == 0:
                    self.put(xx, yy, c)

    def tri(self, points, c):
        self.d.polygon(points, fill=c)

    def line(self, x0, y0, x1, y1, c):
        self.d.line([x0, y0, x1, y1], fill=c)

    def outline(self, edge=INK):
        """Adds a one-pixel dark edge around everything opaque (for sprites drawn on a clear background)."""
        src = self.img.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3] != 0:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3] != 0:
                        self.px[x, y] = edge
                        break


def sky(cv, colors, y0, y1):
    """Banded gradient with a dithered row between bands, the classic pixel-art sky."""
    n = len(colors)
    cv.rect(0, y1, cv.w, cv.h - y1, colors[-1])  # fill below so nothing stays see-through behind the ground
    band = (y1 - y0) / float(n)
    for i, c in enumerate(colors):
        top = int(y0 + i * band)
        cv.rect(0, top, cv.w, int(y0 + (i + 1) * band) - top + 1, c)
    for i in range(1, n):
        y = int(y0 + i * band)
        cv.dither(0, y - 2, cv.w, 2, colors[i - 1], 0)
        cv.dither(0, y, cv.w, 2, colors[i], 1)


def cloud(cv, x, y, size, light=rgb("FFFFFF"), shade=rgb("D6E6FF")):
    """Puffy cloud built from a few discs, with a flat shaded underside."""
    s = size
    parts = [(0, 0, s), (s * 1.1, -s * 0.5, s * 1.25), (s * 2.3, -s * 0.1, s * 1.0), (s * 3.2, s * 0.25, s * 0.75)]
    for dx, dy, r in parts:
        cv.disc(x + dx, y + dy, r, shade)
    for dx, dy, r in parts:
        cv.disc(x + dx, y + dy - 1, r - 1, light)
    cv.rect(int(x - s * 0.6), int(y + s * 0.4), int(s * 4.4), max(1, int(s * 0.5)), shade)


def sun(cv, x, y, r):
    cv.disc(x, y, r + 3, rgb("FFE9A6"))
    cv.disc(x, y, r, rgb("FFC93C"))
    cv.disc(x - 1, y - 1, r - 3, rgb("FFE07A"))
    for i in range(8):
        a = i * math.pi / 4
        for k in range(r + 5, r + 9):
            cv.put(int(round(x + math.cos(a) * k)), int(round(y + math.sin(a) * k)), rgb("FFD45C"))


def moon(cv, x, y, r, sky_color):
    cv.disc(x, y, r + 2, rgb("3B4E86"))
    cv.disc(x, y, r, rgb("FFF4C8"))
    cv.disc(x + r * 0.55, y - r * 0.35, r * 0.85, sky_color)


def stars(cv, seed, y0, y1, count, colors=(rgb("FFFFFF"), rgb("FFE9A6"), rgb("A9C3FF"))):
    rnd = random.Random(seed)
    for _ in range(count):
        x, y = rnd.randrange(cv.w), rnd.randrange(y0, y1)
        c = rnd.choice(colors)
        cv.put(x, y, c)
        if rnd.random() < 0.18:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                cv.put(x + dx, y + dy, c[:3] + (150,))


def building(cv, x, y_base, w, h, wall, trim, window, lit=None, seed=0, roof=None, win=(3, 4), gap=(3, 4)):
    top = y_base - h
    cv.rect(x, top, w, h, wall)
    cv.rect(x, top, w, 2, trim)
    cv.vline(x + w - 1, top, h, trim)
    if roof:
        cv.rect(x - 1, top - 2, w + 2, 2, roof)
    rnd = random.Random(seed)
    ww, wh = win
    gx, gy = gap
    cols = max(1, (w - 4) // (ww + gx))
    rows = max(1, (h - 10) // (wh + gy))
    ox = x + (w - (cols * ww + (cols - 1) * gx)) // 2
    for r in range(rows):
        for c in range(cols):
            wx, wy = ox + c * (ww + gx), top + 5 + r * (wh + gy)
            colour = lit if lit and rnd.random() < 0.45 else window
            cv.rect(wx, wy, ww, wh, colour)
            cv.hline(wx, wy + wh, ww, trim)


def tree(cv, x, y_base, r=9, trunk=rgb("7A4E2D"), dark=rgb("2F8F5B"), mid=rgb("3FB06E"), light=rgb("7AD69A")):
    cv.rect(x - 1, y_base - r - 6, 3, r + 6, trunk)
    cy = y_base - r - 8
    cv.disc(x, cy, r, dark)
    cv.disc(x - 1, cy - 1, r - 2, mid)
    cv.disc(x - 3, cy - 4, r * 0.4, light)


def bush(cv, x, y_base, w, dark=rgb("2F8F5B"), mid=rgb("3FB06E")):
    for i in range(0, w, 5):
        cv.disc(x + i, y_base - 3, 4, dark)
    for i in range(0, w, 5):
        cv.disc(x + i - 1, y_base - 4, 2.5, mid)


def lamp(cv, x, y_base, h=46, pole=rgb("2A3858"), glow=rgb("FFE07A")):
    cv.rect(x, y_base - h, 2, h, pole)
    cv.rect(x - 2, y_base - 2, 6, 2, pole)
    cv.rect(x - 3, y_base - h - 5, 8, 5, pole)
    cv.rect(x - 2, y_base - h - 4, 6, 3, glow)


def pavement(cv, y, h, base=rgb("D9DEE8"), line=rgb("BCC4D4"), kerb=rgb("9AA6BD")):
    cv.rect(0, y, cv.w, h, base)
    cv.rect(0, y, cv.w, 2, kerb)
    for yy in range(y + 8, y + h, 10):
        cv.hline(0, yy, cv.w, line)
    off = 0
    for yy in range(y + 2, y + h, 10):
        for xx in range(off, cv.w, 18):
            cv.vline(xx, yy, 6 if yy + 6 < y + h else y + h - yy, line)
        off = 9 - off


def grass(cv, y, h, base=rgb("62C77F"), dark=rgb("47A866"), seed=1):
    cv.rect(0, y, cv.w, h, base)
    rnd = random.Random(seed)
    for _ in range(cv.w * h // 22):
        x, yy = rnd.randrange(cv.w), rnd.randrange(y + 1, y + h)
        cv.put(x, yy, dark)
        cv.put(x, yy - 1, dark)


def bird(cv, x, y, c=rgb("2A3858")):
    for dx, dy in ((0, 0), (1, -1), (2, 0), (3, 0), (4, -1), (5, 0)):
        cv.put(x + dx, y + dy, c)


def bunting(cv, y, colors, sag=6):
    for x in range(cv.w):
        yy = y + int(sag * math.sin(math.pi * (x % 72) / 72.0))
        cv.put(x, yy, rgb("2A3858"))
        if x % 9 == 2:
            c = colors[(x // 9) % len(colors)]
            for k in range(5):
                cv.hline(x - 2 + k // 2, yy + 1 + k, 5 - k, c)


def save_scene(cv, name, preview):
    path = os.path.join(STORY, name + ".png")
    cv.img.save(path)
    if preview:
        cv.img.resize((cv.w * 3, cv.h * 3), Image.NEAREST).save(os.path.join(PREVIEW, name + ".png"))


# --------------------------------------------------------------------------------------------- story scenes
# Layout: the girl stands in the middle with her feet near y = 318, so the ground line sits around y = 296 and
# the busy details stay at the sides. The bottom ~124 rows are under the hands and the answer panel.

DAY_SKY = [rgb("6FB7FF"), rgb("86C4FF"), rgb("9ED0FF"), rgb("B6DCFF"), rgb("CDE7FF"), rgb("E2F1FF")]


def scene_street_sunny():
    """Story 1, meeting Aysel and saying your name: a sunny street, blue sky, fluffy clouds."""
    cv = Canvas(W, H)
    sky(cv, DAY_SKY, 0, 250)
    sun(cv, 170, 46, 13)
    cloud(cv, 18, 70, 7)
    cloud(cv, 120, 112, 6)
    cloud(cv, 60, 150, 4)
    cloud(cv, 168, 178, 5)
    bird(cv, 92, 58)
    bird(cv, 104, 66)
    # back row of houses
    building(cv, -4, 262, 46, 70, rgb("F4B7A3"), rgb("D9907B"), rgb("8FC2FF"), seed=1, roof=rgb("B5654F"))
    building(cv, 42, 262, 30, 52, rgb("FFE1A8"), rgb("E0B56A"), rgb("8FC2FF"), seed=2, roof=rgb("C9893F"))
    building(cv, 148, 262, 34, 60, rgb("BFE3D0"), rgb("8CC2A7"), rgb("8FC2FF"), seed=3, roof=rgb("5E9C80"))
    building(cv, 182, 262, 38, 80, rgb("C9D5FF"), rgb("97A8E8"), rgb("8FC2FF"), seed=4, roof=rgb("5D6FB8"))
    tree(cv, 80, 266, 10)
    tree(cv, 140, 266, 8)
    bush(cv, 0, 270, 40)
    bush(cv, 178, 270, 40)
    cv.rect(0, 266, W, 30, rgb("8E9AB3"))  # road
    for x in range(4, W, 24):
        cv.rect(x, 280, 12, 2, rgb("F2F4F8"))
    pavement(cv, 296, H - 296)
    lamp(cv, 22, 300)
    lamp(cv, 192, 300)
    return cv


def scene_morning():
    """Story 2, good morning: sunrise over low rooftops, warm sky."""
    cv = Canvas(W, H)
    sky(cv, [rgb("6E8BE8"), rgb("8E9DF0"), rgb("C3A5E8"), rgb("F2AFC2"), rgb("FFC7A0"), rgb("FFE2A8")], 0, 262)
    cv.disc(60, 238, 30, rgb("FFE9A6"))
    cv.disc(60, 238, 24, rgb("FFC93C"))
    cv.disc(58, 235, 17, rgb("FFE07A"))
    cloud(cv, 14, 88, 5, rgb("FFE4EC"), rgb("F2B9CF"))
    cloud(cv, 150, 132, 6, rgb("FFE4EC"), rgb("F2B9CF"))
    cloud(cv, 40, 190, 4, rgb("FFF0D6"), rgb("FFCFA8"))
    for x, w, h, c in ((0, 40, 40, "6C5B9E"), (38, 26, 28, "7A68AD"), (150, 30, 34, "7A68AD"), (178, 40, 48, "6C5B9E")):
        building(cv, x, 270, w, h, rgb(c), rgb("574A86"), rgb("FFD98A"), seed=x, roof=rgb("4A3F75"))
    grass(cv, 268, 30, rgb("8FD39B"), rgb("6DB97F"), seed=2)
    pavement(cv, 296, H - 296, rgb("E6E0EE"), rgb("CCC3DA"), rgb("A99CC0"))
    tree(cv, 26, 300, 9)
    tree(cv, 190, 300, 9)
    return cv


def scene_park():
    """Story 3, "do you know?": a park with trees, a bench and a pond."""
    cv = Canvas(W, H)
    sky(cv, DAY_SKY, 0, 230)
    sun(cv, 36, 44, 10)
    cloud(cv, 120, 60, 6)
    cloud(cv, 30, 128, 4)
    cloud(cv, 150, 160, 5)
    # distant hills
    for x in range(W):
        h = int(18 + 8 * math.sin(x / 23.0) + 5 * math.sin(x / 9.0))
        cv.vline(x, 236 - h, h, rgb("9BD6A6"))
    grass(cv, 236, H - 236, seed=3)
    tree(cv, 14, 262, 13)
    tree(cv, 54, 250, 9)
    tree(cv, 168, 252, 10)
    tree(cv, 204, 266, 13)
    cv.ellipse(170, 292, 30, 8, rgb("4E93E8"))  # pond
    cv.ellipse(170, 291, 26, 5, rgb("7DB6FF"))
    cv.hline(160, 289, 8, rgb("CFE5FF"))
    # bench
    cv.rect(18, 288, 36, 3, rgb("B0703F"))
    cv.rect(18, 282, 36, 3, rgb("C98450"))
    cv.rect(20, 291, 2, 8, rgb("2A3858"))
    cv.rect(50, 291, 2, 8, rgb("2A3858"))
    # path
    for y in range(300, H):
        half = 20 + (y - 300) // 2
        cv.hline(108 - half, y, half * 2, rgb("EAD9B5"))
    for flower_x, c in ((80, "FF7B86"), (140, "FFC54A"), (96, "FFFFFF"), (128, "FF9ECF")):
        cv.put(flower_x, 296, rgb(c))
        cv.put(flower_x - 1, 297, rgb(c))
        cv.put(flower_x + 1, 297, rgb(c))
        cv.put(flower_x, 298, rgb("2F8F5B"))
    return cv


def scene_plaza():
    """Story 4, who / where / when: a city square with a fountain and a clock."""
    cv = Canvas(W, H)
    sky(cv, DAY_SKY, 0, 240)
    cloud(cv, 140, 54, 7)
    cloud(cv, 16, 104, 5)
    building(cv, -2, 268, 52, 96, rgb("E9DCC8"), rgb("C9B697"), rgb("8FC2FF"), seed=5, roof=rgb("A68B62"))
    building(cv, 166, 268, 52, 104, rgb("E9DCC8"), rgb("C9B697"), rgb("8FC2FF"), seed=6, roof=rgb("A68B62"))
    # arcade with columns at the back
    cv.rect(50, 214, 116, 54, rgb("F3EADB"))
    cv.rect(50, 210, 116, 5, rgb("C9B697"))
    for x in range(56, 166, 18):
        cv.rect(x, 224, 10, 44, rgb("D5C5A8"))
        cv.disc(x + 5, 224, 5, rgb("D5C5A8"))
    cv.disc(108, 196, 9, rgb("C9B697"))
    cv.disc(108, 196, 7, WHITE)
    cv.vline(108, 191, 5, INK)
    cv.hline(108, 196, 4, INK)
    pavement(cv, 268, H - 268, rgb("E3D8C6"), rgb("CBBDA5"), rgb("B8A688"))
    # fountain to the side
    cv.ellipse(30, 300, 22, 6, rgb("B8A688"))
    cv.ellipse(30, 299, 19, 4, rgb("7DB6FF"))
    cv.rect(28, 276, 4, 22, rgb("B8A688"))
    for k in range(6):
        cv.put(24 - k // 2, 272 + k, rgb("A9D2FF"))
        cv.put(36 + k // 2, 272 + k, rgb("A9D2FF"))
    cv.rect(27, 268, 6, 4, rgb("A9D2FF"))
    tree(cv, 196, 300, 9)
    return cv


def scene_home():
    """Story 5, family: a cosy living room with a window, family photos and a sofa."""
    cv = Canvas(W, H)
    cv.rect(0, 0, W, 300, rgb("FFE8C7"))
    for x in range(0, W, 12):
        cv.rect(x, 0, 6, 300, rgb("FDDDB4"))
    cv.rect(0, 238, W, 62, rgb("D9B48A"))  # wainscot
    cv.rect(0, 236, W, 3, rgb("B8895A"))
    # window with a day sky
    cv.rect(132, 60, 70, 86, rgb("FFFFFF"))
    sky(_Sub(cv, 136, 64, 62, 78), [rgb("8CC6FF"), rgb("B6DCFF"), rgb("DDEEFF")], 0, 78)
    cloud(cv, 146, 90, 3)
    cv.rect(166, 64, 2, 78, WHITE)
    cv.rect(136, 102, 62, 2, WHITE)
    cv.rect(128, 58, 6, 92, rgb("FF9AA2"))
    cv.rect(200, 58, 6, 92, rgb("FF9AA2"))
    # photo frames
    for fx, fy, c in ((16, 70, "8FC2FF"), (52, 60, "FFC54A"), (40, 104, "9EE6B8")):
        cv.rect(fx, fy, 26, 22, rgb("8A5A33"))
        cv.rect(fx + 2, fy + 2, 22, 18, rgb(c))
        cv.disc(fx + 9, fy + 9, 3, rgb("F1C6A2"))
        cv.disc(fx + 17, fy + 10, 3, rgb("F1C6A2"))
        cv.rect(fx + 5, fy + 13, 16, 7, rgb("2A3858"))
    # sofa on the left, lamp on the right
    cv.rect(-4, 250, 70, 26, rgb("4E7BE8"))
    cv.rect(-4, 240, 70, 14, rgb("6C95F2"))
    cv.rect(-4, 276, 70, 6, rgb("2C4FB0"))
    cv.rect(60, 246, 10, 34, rgb("3A63CF"))
    cv.rect(188, 196, 2, 98, rgb("2A3858"))
    cv.tri([(178, 196), (200, 196), (194, 178), (184, 178)], rgb("FFC54A"))
    cv.rect(182, 292, 14, 4, rgb("2A3858"))
    # floor and rug
    cv.rect(0, 296, W, H - 296, rgb("C98450"))
    for y in range(300, H, 8):
        cv.hline(0, y, W, rgb("B0703F"))
    cv.ellipse(108, 330, 80, 22, rgb("E0525F"))
    cv.ellipse(108, 330, 70, 16, rgb("FF7B86"))
    return cv


class _Sub:
    """Draws into a rectangle of a canvas (used for the sky seen through a window)."""

    def __init__(self, cv, x, y, w, h):
        self.cv, self.x, self.y, self.w, self.h = cv, x, y, w, h

    def rect(self, x, y, w, h, c):
        x0, y0 = max(0, x), max(0, y)
        x1, y1 = min(self.w, x + w), min(self.h, y + h)
        self.cv.rect(self.x + x0, self.y + y0, x1 - x0, y1 - y0, c)

    def dither(self, x, y, w, h, c, phase=0):
        for yy in range(max(0, y), min(self.h, y + h)):
            for xx in range(max(0, x), min(self.w, x + w)):
                if (xx + yy + phase) % 2 == 0:
                    self.cv.put(self.x + xx, self.y + yy, c)


def scene_market():
    """Story 6, counting: a sunny market with fruit stalls and striped awnings."""
    cv = Canvas(W, H)
    sky(cv, DAY_SKY, 0, 220)
    sun(cv, 108, 40, 11)
    cloud(cv, 10, 92, 5)
    cloud(cv, 150, 112, 6)
    building(cv, -2, 250, 60, 60, rgb("F4B7A3"), rgb("D9907B"), rgb("8FC2FF"), seed=7)
    building(cv, 158, 250, 60, 66, rgb("FFE1A8"), rgb("E0B56A"), rgb("8FC2FF"), seed=8)
    pavement(cv, 250, H - 250, rgb("EFE3CF"), rgb("D9C8AA"), rgb("BFA984"))

    def stall(x, y, w, stripe_a, stripe_b, fruits):
        for i in range(0, w, 6):
            cv.rect(x + i, y, 6, 12, stripe_a if (i // 6) % 2 == 0 else stripe_b)
        for i in range(0, w, 6):
            cv.disc(x + i + 3, y + 12, 3, stripe_a if (i // 6) % 2 == 0 else stripe_b)
        cv.rect(x + 1, y + 14, 2, 40, rgb("8A5A33"))
        cv.rect(x + w - 3, y + 14, 2, 40, rgb("8A5A33"))
        cv.rect(x - 2, y + 38, w + 4, 18, rgb("B0703F"))
        cv.rect(x - 2, y + 38, w + 4, 3, rgb("C98450"))
        rnd = random.Random(x)
        for k in range(w // 4):
            fx = x + 2 + (k * 4) % (w - 4)
            fy = y + 34 - (k * 4) // (w - 4) * 3
            cv.disc(fx, fy, 2, rnd.choice(fruits))

    stall(4, 236, 58, rgb("FF6B77"), WHITE, [rgb("FF6B77"), rgb("FFC54A"), rgb("45CF8A")])
    stall(156, 236, 58, rgb("4E7BE8"), WHITE, [rgb("FF9A3C"), rgb("FFC54A"), rgb("A35BD6")])
    cv.rect(76, 290, 18, 10, rgb("C98450"))  # crates
    cv.rect(122, 290, 18, 10, rgb("C98450"))
    for k in range(3):
        cv.disc(80 + k * 5, 288, 2, rgb("FF6B77"))
        cv.disc(126 + k * 5, 288, 2, rgb("45CF8A"))
    return cv


def scene_clock_evening():
    """Story 7, time and "see you in the evening": a clock tower against a sunset."""
    cv = Canvas(W, H)
    sky(cv, [rgb("3B3F8F"), rgb("6A4CA6"), rgb("B05CA8"), rgb("E9728E"), rgb("FF9A70"), rgb("FFC37A")], 0, 270)
    stars(cv, 7, 0, 70, 18)
    cv.disc(40, 220, 14, rgb("FFD98A"))
    cv.disc(40, 220, 10, rgb("FFE9B8"))
    cloud(cv, 120, 150, 5, rgb("FFB3A6"), rgb("D9768A"))
    # tower
    tx = 150
    cv.rect(tx, 120, 36, 150, rgb("5B4A8E"))
    cv.rect(tx + 32, 120, 4, 150, rgb("473A73"))
    cv.tri([(tx - 4, 120), (tx + 40, 120), (tx + 18, 92)], rgb("473A73"))
    cv.disc(tx + 18, 146, 12, rgb("FFE9B8"))
    cv.disc(tx + 18, 146, 10, WHITE)
    cv.vline(tx + 18, 138, 8, INK)
    cv.hline(tx + 18, 146, 7, INK)  # five o'clock-ish
    for y in range(176, 260, 16):
        cv.rect(tx + 8, y, 6, 9, rgb("FFD98A"))
        cv.rect(tx + 22, y, 6, 9, rgb("FFD98A"))
    building(cv, -2, 272, 44, 50, rgb("4A3F75"), rgb("3B3260"), rgb("362C58"), lit=rgb("FFD98A"), seed=9)
    building(cv, 186, 272, 32, 40, rgb("4A3F75"), rgb("3B3260"), rgb("362C58"), lit=rgb("FFD98A"), seed=10)
    pavement(cv, 272, H - 272, rgb("C9B7D9"), rgb("B09CC6"), rgb("8E78AE"))
    lamp(cv, 60, 280, glow=rgb("FFF0A8"))
    return cv


def scene_cafe():
    """Story 8, objects (book, bag, phone, tea): a cosy café-library with shelves and a teapot."""
    cv = Canvas(W, H)
    cv.rect(0, 0, W, 296, rgb("E8F0FF"))
    for y in range(0, 296, 14):
        cv.hline(0, y, W, rgb("DCE6FA"))
    # bookshelf on the left
    cv.rect(0, 70, 62, 190, rgb("8A5A33"))
    book_colours = [rgb(c) for c in ("FF6B77", "4E7BE8", "FFC54A", "45CF8A", "A35BD6", "FF9A3C", "2A3858")]
    rnd = random.Random(8)
    for shelf_y in range(78, 250, 36):
        cv.rect(4, shelf_y, 54, 30, rgb("5E3A20"))
        x = 6
        while x < 54:
            bw = rnd.choice((3, 4, 5))
            bh = rnd.randrange(20, 28)
            cv.rect(x, shelf_y + 30 - bh, bw, bh, rnd.choice(book_colours))
            x += bw + 1
        cv.rect(0, shelf_y + 30, 62, 3, rgb("B0703F"))
    # window with a view and a hanging plant on the right
    cv.rect(150, 56, 58, 70, WHITE)
    sky(_Sub(cv, 154, 60, 50, 62), [rgb("8CC6FF"), rgb("B6DCFF"), rgb("DDEEFF")], 0, 62)
    cloud(cv, 160, 84, 3)
    cv.rect(178, 60, 2, 62, WHITE)
    cv.vline(126, 0, 30, rgb("2A3858"))
    cv.rect(120, 30, 14, 10, rgb("C98450"))
    for k in range(6):
        cv.put(118 - k // 2, 40 + k, rgb("3FB06E"))
        cv.put(135 + k // 2, 40 + k, rgb("3FB06E"))
    # counter with tea
    cv.rect(140, 236, 80, 60, rgb("B0703F"))
    cv.rect(138, 232, 82, 6, rgb("C98450"))
    cv.rect(160, 220, 16, 12, WHITE)  # teapot
    cv.disc(168, 220, 7, WHITE)
    cv.rect(176, 222, 5, 2, WHITE)
    cv.rect(165, 211, 6, 2, rgb("FF6B77"))
    cv.rect(190, 224, 8, 8, rgb("FF6B77"))  # cup
    cv.rect(191, 225, 6, 2, rgb("8A3A20"))
    cv.vline(193, 216, 4, rgb("C9D5FF"))
    cv.vline(195, 214, 4, rgb("C9D5FF"))
    cv.rect(146, 226, 10, 6, rgb("4E7BE8"))  # a book on the counter
    cv.rect(0, 296, W, H - 296, rgb("C98450"))
    for y in range(300, H, 8):
        cv.hline(0, y, W, rgb("B0703F"))
    for x in range(0, W, 32):
        for y in range(300, H, 16):
            cv.vline(x + (16 if (y // 16) % 2 else 0), y, 8, rgb("B0703F"))
    return cv


def scene_city():
    """Story 9, places: a city street with a pharmacy (green cross) and a metro entrance."""
    cv = Canvas(W, H)
    sky(cv, DAY_SKY, 0, 200)
    cloud(cv, 90, 40, 6)
    cloud(cv, 10, 84, 4)
    building(cv, 60, 250, 40, 150, rgb("B7C6E8"), rgb("8FA0CC"), rgb("E2F1FF"), lit=rgb("FFE9A6"), seed=11)
    building(cv, 116, 250, 44, 170, rgb("9FB2E0"), rgb("7C8FC4"), rgb("E2F1FF"), lit=rgb("FFE9A6"), seed=12)
    # pharmacy on the left
    building(cv, -2, 268, 64, 110, rgb("F3F7FF"), rgb("C9D5EA"), rgb("8FC2FF"), seed=13)
    cv.rect(4, 238, 52, 30, rgb("8FC2FF"))
    cv.rect(26, 248, 10, 20, rgb("C9D5EA"))
    cv.rect(16, 204, 26, 26, rgb("2FB36B"))
    cv.rect(26, 207, 6, 20, WHITE)
    cv.rect(19, 214, 20, 6, WHITE)
    # metro entrance on the right
    building(cv, 160, 268, 58, 96, rgb("E9DCC8"), rgb("C9B697"), rgb("8FC2FF"), seed=14)
    cv.rect(166, 236, 46, 32, rgb("2A3858"))
    cv.rect(170, 240, 38, 28, rgb("101A30"))
    for k in range(4):
        cv.rect(172 + k * 2, 246 + k * 5, 34 - k * 4, 2, rgb("4A5878"))
    cv.vline(188, 196, 40, rgb("2A3858"))
    cv.disc(188, 192, 10, rgb("E2232A"))
    cv.disc(188, 192, 8, WHITE)
    for k in range(7):  # the letter M
        cv.put(184, 189 + k, rgb("E2232A"))
        cv.put(192, 189 + k, rgb("E2232A"))
    cv.put(185, 190, rgb("E2232A"))
    cv.put(186, 191, rgb("E2232A"))
    cv.put(187, 192, rgb("E2232A"))
    cv.put(188, 193, rgb("E2232A"))
    cv.put(189, 192, rgb("E2232A"))
    cv.put(190, 191, rgb("E2232A"))
    cv.put(191, 190, rgb("E2232A"))
    pavement(cv, 268, H - 268)
    # bus stop sign
    cv.vline(74, 250, 46, rgb("2A3858"))
    cv.rect(68, 240, 14, 12, rgb("4E7BE8"))
    cv.rect(71, 243, 8, 4, WHITE)
    return cv


def scene_celebration():
    """Story 10, "nice to meet you": an evening party in the square with bunting, lights and fireworks."""
    cv = Canvas(W, H)
    night = [rgb("101B3D"), rgb("17265A"), rgb("223478"), rgb("2E4592"), rgb("4057A8"), rgb("5A6CB8")]
    sky(cv, night, 0, 270)
    stars(cv, 10, 0, 200, 60)

    def firework(x, y, r, c, c2):
        for i in range(12):
            a = i * math.pi / 6
            for k in range(r // 3, r):
                cv.put(int(round(x + math.cos(a) * k)), int(round(y + math.sin(a) * k)), c if k < r * 0.7 else c2)
        cv.disc(x, y, 1.5, WHITE)

    firework(50, 70, 20, rgb("FF7B86"), rgb("FFC54A"))
    firework(166, 54, 16, rgb("74E0A8"), rgb("A9C3FF"))
    firework(140, 140, 12, rgb("FFC54A"), rgb("FF9ECF"))
    building(cv, -2, 272, 50, 70, rgb("2C3E66"), rgb("22324F"), rgb("1B2640"), lit=rgb("FFD98A"), seed=15)
    building(cv, 170, 272, 48, 84, rgb("2C3E66"), rgb("22324F"), rgb("1B2640"), lit=rgb("FFD98A"), seed=16)
    bunting(cv, 206, [rgb("FF6B77"), rgb("FFC54A"), rgb("45CF8A"), rgb("6B9BFF")])
    for x in range(6, W, 14):  # string lights
        y = 232 + int(4 * math.sin(math.pi * (x % 108) / 108.0))
        cv.put(x, y, rgb("FFE9A6"))
        cv.put(x, y + 1, rgb("FFC54A"))
    pavement(cv, 272, H - 272, rgb("47577E"), rgb("3B4A6E"), rgb("2C3A5C"))
    lamp(cv, 20, 280, glow=rgb("FFF0A8"))
    lamp(cv, 194, 280, glow=rgb("FFF0A8"))
    for x, c in ((60, "FF6B77"), (150, "6B9BFF")):  # balloons
        cv.vline(x, 250, 30, rgb("C9D5FF"))
        cv.ellipse(x, 244, 5, 7, rgb(c))
        cv.put(x - 2, 240, WHITE)
    return cv


SCENES = [scene_street_sunny, scene_morning, scene_park, scene_plaza, scene_home, scene_market,
          scene_clock_evening, scene_cafe, scene_city, scene_celebration]


# -------------------------------------------------------------------------- story girl and hands (pixelated)

def pixelate_sprite(path, size, edge=None, colours=24):
    """Turns a smooth picture into pixel art: box-downscale, hard alpha, a small palette, optional dark edge."""
    src = Image.open(path).convert("RGBA")
    small = src.resize(size, Image.BOX)
    alpha = small.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    flat = Image.new("RGBA", size, (255, 255, 255, 255))
    flat.alpha_composite(small)
    quant = flat.convert("RGB").quantize(colors=colours, method=Image.MEDIANCUT, dither=Image.NONE).convert("RGBA")
    quant.putalpha(alpha)
    cv = Canvas(size[0], size[1])
    cv.img.paste(quant)
    cv.px = cv.img.load()
    if edge:
        cv.outline(edge)
    return cv.img


def make_story_people(preview):
    """The story girl (98 x 245, shown 490 x 1225) and the first-person hands (216 x 124, shown 1080 x 620).

    The smooth originals are kept next to this script (Tools/ui_art/source) so this step can be run again.
    """
    source = os.path.join("Tools", "ui_art", "source")
    os.makedirs(source, exist_ok=True)
    jobs = [("npc_idle", (98, 245)), ("npc_talk", (98, 245)), ("npc_wave", (98, 245)), ("npc_wave_idle", (98, 245)),
            ("pov_hands_bag", (216, 124))]
    for name, size in jobs:
        original = os.path.join(source, name + ".png")
        current = os.path.join(STORY, name + ".png")
        if not os.path.exists(original):
            if Image.open(current).size == size:
                continue  # already pixel art and no original kept
            Image.open(current).save(original)
        image = pixelate_sprite(original, size, edge=rgb("1E2A44"))
        image.save(current)
        if preview:
            image.resize((size[0] * 3, size[1] * 3), Image.NEAREST).save(os.path.join(PREVIEW, name + ".png"))


# ------------------------------------------------------------------------------------------------ unit icons
# 16 x 16 white shapes (the tile tints them), saved 6x so they also look right without Point filtering.

UNIT_ICONS = {
    "alphabet": [  # "Aa" letters
        "................",
        "................",
        "...XXXX.........",
        "..XX..XX........",
        "..XX..XX........",
        "..XX..XX..XXXX..",
        "..XXXXXX.....XX.",
        "..XX..XX..XXXXX.",
        "..XX..XX.XX..XX.",
        "..XX..XX.XX..XX.",
        "..XX..XX..XXXXX.",
        "................",
        "..XXXXXXXXXXXX..",
        "................",
        "................",
        "................"],
    "greetings": [  # waving hand
        "................",
        "......X.........",
        "....X.XX.X......",
        "...XX.XX.XX.....",
        "...XX.XX.XX.....",
        "...XX.XX.XX.X...",
        "...XX.XX.XX.XX..",
        "...XXXXXXXX.XX..",
        "...XXXXXXXXXXX..",
        "...XXXXXXXXXX...",
        ".X.XXXXXXXXXX...",
        ".XXXXXXXXXXX....",
        "..XXXXXXXXXX....",
        "...XXXXXXXX.....",
        "....XXXXXX......",
        "................"],
    "pronouns": [  # two people
        "................",
        "...XXX..........",
        "..XXXXX....XXX..",
        "..XXXXX...XXXXX.",
        "..XXXXX...XXXXX.",
        "...XXX.....XXX..",
        "................",
        ".XXXXXXX..XXXXX.",
        "XXXXXXXXX.XXXXXX",
        "XXXXXXXXX.XXXXXX",
        "XXXXXXXXX.XXXXXX",
        "XXXXXXXXX.XXXXXX",
        "................",
        "................",
        "................",
        "................"],
    "questions": [  # question mark in a speech bubble
        "................",
        ".XXXXXXXXXXXXXX.",
        "XXXXXX....XXXXXX",
        "XXXXX.XXXX.XXXXX",
        "XXXXXXXXXX.XXXXX",
        "XXXXXXXXX.XXXXXX",
        "XXXXXXXX.XXXXXXX",
        "XXXXXXXX.XXXXXXX",
        "XXXXXXXXXXXXXXXX",
        "XXXXXXXX.XXXXXXX",
        ".XXXXXXXXXXXXXX.",
        "...XXX..........",
        "..XXX...........",
        ".XX.............",
        "................",
        "................"],
    "family": [  # house with a heart
        ".......XX.......",
        "......XXXX......",
        ".....XXXXXX.....",
        "....XXXXXXXX....",
        "...XXXXXXXXXX...",
        "..XXXXXXXXXXXX..",
        ".XXXXXXXXXXXXXX.",
        "..XXXXXXXXXXXX..",
        "..XXX..XX..XXX..",
        "..XX........XX..",
        "..XX........XX..",
        "..XXX......XXX..",
        "..XXXX....XXXX..",
        "..XXXXX..XXXXX..",
        "..XXXXXXXXXXXX..",
        "................"],
    "numbers": [  # 1 2 3
        "................",
        "................",
        "................",
        "..X..XXX..XXX...",
        ".XX.....X....X..",
        "..X.....X....X..",
        "..X....X...XX...",
        "..X...X......X..",
        "..X..X.......X..",
        ".XXX.XXXX.XXX...",
        "................",
        "................",
        ".XXXXXXXXXXXXX..",
        "................",
        "................",
        "................"],
    "time": [  # clock
        "................",
        ".....XXXXXX.....",
        "...XXXXXXXXXX...",
        "..XXXXXX.XXXXX..",
        ".XXXXXXX.XXXXXX.",
        ".XXXXXXX.XXXXXX.",
        "XXXXXXXX.XXXXXXX",
        "XXXXXXXX.XXXXXXX",
        "XXXXXXXX....XXXX",
        "XXXXXXXXXXXXXXXX",
        ".XXXXXXXXXXXXXX.",
        ".XXXXXXXXXXXXXX.",
        "..XXXXXXXXXXXX..",
        "...XXXXXXXXXX...",
        ".....XXXXXX.....",
        "................"],
    "objects": [  # book
        "................",
        "................",
        ".XXXXXX..XXXXXX.",
        ".XXXXXXX.XXXXXX.",
        ".X....XX.X....X.",
        ".XXXXXXX.XXXXXX.",
        ".X....XX.X....X.",
        ".XXXXXXX.XXXXXX.",
        ".X....XX.X....X.",
        ".XXXXXXX.XXXXXX.",
        ".XXXXXXX.XXXXXX.",
        ".XXXXXXXXXXXXXX.",
        "..XXXXXXXXXXXX..",
        "................",
        "................",
        "................"],
    "places": [  # map pin
        "................",
        ".....XXXXXX.....",
        "....XXXXXXXX....",
        "...XXXXXXXXXX...",
        "...XXXX..XXXX...",
        "...XXX....XXX...",
        "...XXX....XXX...",
        "...XXXX..XXXX...",
        "...XXXXXXXXXX...",
        "....XXXXXXXX....",
        ".....XXXXXX.....",
        "......XXXX......",
        ".......XX.......",
        "................",
        "..XXXXXXXXXXXX..",
        "................"],
    "review": [  # star
        "................",
        ".......XX.......",
        ".......XX.......",
        "......XXXX......",
        "......XXXX......",
        "XXXXXXXXXXXXXXXX",
        ".XXXXXXXXXXXXXX.",
        "..XXXXXXXXXXXX..",
        "...XXXXXXXXXX...",
        "...XXXXXXXXXX...",
        "..XXXXX..XXXXX..",
        "..XXXX....XXXX..",
        ".XXX........XXX.",
        ".XX..........XX.",
        "................",
        "................"],
}


def make_unit_icons(preview):
    os.makedirs(UNITS, exist_ok=True)
    for name, rows in UNIT_ICONS.items():
        assert len(rows) == 16 and all(len(r) == 16 for r in rows), name
        small = Image.new("RGBA", (16, 16), CLEAR)
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch == "X":
                    small.putpixel((x, y), WHITE)
        big = small.resize((96, 96), Image.NEAREST)
        big.save(os.path.join(UNITS, "unit_" + name + ".png"))
        if preview:
            bg = Image.new("RGBA", (96, 96), rgb("0043E6"))
            bg.alpha_composite(big)
            bg.save(os.path.join(PREVIEW, "unit_" + name + ".png"))


# --------------------------------------------------------------------------------------- home decorations
# Saved at native size; HomeDecorUI shows them at 5x.

def make_decor(preview):
    os.makedirs(DECOR, exist_ok=True)
    items = {}

    cv = Canvas(30, 14)
    cloud(cv, 5, 8, 4)
    items["decor_cloud_big"] = cv
    cv = Canvas(20, 10)
    cloud(cv, 4, 6, 2.6)
    items["decor_cloud_small"] = cv

    def sparkle(colour, core):
        s = Canvas(9, 9)
        for k in range(9):
            if k != 4:
                s.put(4, k, colour if abs(k - 4) > 1 else core)
                s.put(k, 4, colour if abs(k - 4) > 1 else core)
        s.put(4, 4, WHITE)
        return s

    items["decor_sparkle"] = sparkle(rgb("FFC54A"), rgb("FFE07A"))
    items["decor_sparkle_night"] = sparkle(rgb("A9C3FF"), rgb("E8EEFA"))

    cv = Canvas(11, 5)
    for dx, dy in ((0, 2), (1, 1), (2, 1), (3, 2), (4, 3), (5, 2), (6, 1), (7, 1), (8, 2), (9, 2), (10, 3)):
        cv.put(dx, dy, rgb("4E5F86"))
    cv.put(5, 3, rgb("4E5F86"))
    items["decor_bird"] = cv

    cv = Canvas(15, 24)  # kite with a tail
    for y in range(12):
        half = y if y < 6 else 11 - y
        for x in range(7 - half, 8 + half):
            cv.put(x, y, rgb("FF6B77") if (x < 7) == (y < 6) else rgb("FFC54A"))
    cv.vline(7, 0, 12, rgb("2A3858"))
    for k in range(12):
        cv.put(7 + int(2 * math.sin(k / 2.0)), 12 + k, rgb("2A3858"))
    for k, c in ((3, "45CF8A"), (7, "6B9BFF"), (10, "FF6B77")):
        x = 7 + int(2 * math.sin(k / 2.0))
        cv.rect(x - 1, 12 + k, 3, 2, rgb(c))
    items["decor_kite"] = cv

    def star(size):
        s = Canvas(size, size)
        c = size // 2
        for k in range(size):
            d = abs(k - c)
            col = rgb("FFE9A6") if d > size // 4 else rgb("FFF7D6")
            s.put(c, k, col)
            s.put(k, c, col)
        s.disc(c, c, size // 6, rgb("FFF7D6"))
        return s

    items["decor_star_big"] = star(11)
    items["decor_star_small"] = star(7)

    cv = Canvas(18, 18)
    cv.disc(9, 9, 8, rgb("FFF4C8"))
    cv.disc(7, 7, 2, rgb("EBDDA8"))
    cv.disc(11, 12, 1.5, rgb("EBDDA8"))
    cv.disc(14, 6, 6, CLEAR)
    items["decor_moon"] = cv

    cv = Canvas(22, 10)  # comet
    for k in range(16):
        a = 255 - k * 14
        cv.put(4 + k, 6 - k // 4, rgb("A9C3FF", a))
        if k < 10:
            cv.put(4 + k, 7 - k // 4, rgb("6B9BFF", a))
    cv.disc(3, 6, 2.2, rgb("FFF7D6"))
    items["decor_comet"] = cv

    for name, canvas in items.items():
        canvas.img.save(os.path.join(DECOR, name + ".png"))
        if preview:
            canvas.img.resize((canvas.w * 5, canvas.h * 5), Image.NEAREST).save(os.path.join(PREVIEW, name + ".png"))


# --------------------------------------------------------------------------------------- night-theme panels

def make_night_panels():
    """White border and mid-grey fill: ThemeSweeper tints them so the border is a light blue and the fill dark."""
    for name in ("panel_r24", "panel_r36", "panel_r48", "pill"):
        image = Image.new("RGBA", (40, 40), WHITE)
        image.paste(Image.new("RGBA", (28, 28), (107, 107, 107, 255)), (6, 6))
        image.save(os.path.join(UI, name + "_night__b12.png"))


if __name__ == "__main__":
    if not os.path.isdir(STORY):
        sys.exit("Run from the repository root.")
    want_preview = "--preview" in sys.argv
    if want_preview:
        os.makedirs(PREVIEW, exist_ok=True)
    for index, draw in enumerate(SCENES):
        save_scene(draw(), "story_bg_%02d" % (index + 1), want_preview)
    make_story_people(want_preview)
    make_unit_icons(want_preview)
    make_decor(want_preview)
    make_night_panels()
    print("Pixel art written to " + ROOT)
