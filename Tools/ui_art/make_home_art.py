"""Draws the pixel art of the Learn (home) page: one picture per unit for the path tiles (plus a blurred copy for
locked units) and the full-screen sky backgrounds that change with the time of day and the weather.

Run from the repository root:  python3 Tools/ui_art/make_home_art.py   (add --preview to also write enlarged
copies and a contact sheet to Tools/ui_art/preview/).
"""
import math
import os
import random
import sys

from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_pixel_art import Canvas, rgb, sky, cloud, sun, moon, stars, building, tree, grass, INK, WHITE, CLEAR  # noqa: E402

ROOT = os.path.join("Assets", "Core", "Sprites")
UNITS = os.path.join(ROOT, "Units")
HOME = os.path.join(ROOT, "Home")
PREVIEW = os.path.join("Tools", "ui_art", "preview")

P = 40  # unit picture size; the tile shows it at 4x

# 5 x 7 letters for the pictures
GLYPHS = {
    "A": [".XXX.", "X...X", "X...X", "XXXXX", "X...X", "X...X", "X...X"],
    "B": ["XXXX.", "X...X", "X...X", "XXXX.", "X...X", "X...X", "XXXX."],
    "Ə": [".XXX.", "X...X", "....X", "XXXXX", "X...X", "X...X", ".XXX."],
    "1": ["..X..", ".XX..", "..X..", "..X..", "..X..", "..X..", ".XXX."],
    "2": [".XXX.", "X...X", "....X", "...X.", "..X..", ".X...", "XXXXX"],
    "3": ["XXXX.", "....X", "....X", ".XXX.", "....X", "....X", "XXXX."],
    "?": [".XXX.", "X...X", "....X", "...X.", "..X..", ".....", "..X.."],
}


def glyph(cv, ch, x, y, c, scale=1):
    for gy, row in enumerate(GLYPHS[ch]):
        for gx, v in enumerate(row):
            if v == "X":
                cv.rect(x + gx * scale, y + gy * scale, scale, scale, c)


def bevel_block(cv, x, y, s, face, light, dark):
    cv.rect(x, y, s, s, INK)
    cv.rect(x + 1, y + 1, s - 2, s - 2, face)
    cv.hline(x + 1, y + 1, s - 2, light)
    cv.vline(x + 1, y + 1, s - 2, light)
    cv.hline(x + 1, y + s - 2, s - 2, dark)
    cv.vline(x + s - 2, y + 1, s - 2, dark)


def person(cv, x, y_feet, shirt, skin=rgb("F2C29B"), hair=rgb("3B2A20"), h=18, long_hair=False):
    """Little front-facing figure, x = centre."""
    head = 3 if h >= 16 else 2
    body_top = y_feet - h + head * 2 + 1
    cv.rect(x - 3, body_top, 7, y_feet - body_top - 3, shirt)  # body
    cv.rect(x - 2, y_feet - 3, 2, 3, rgb("2A3858"))  # legs
    cv.rect(x + 1, y_feet - 3, 2, 3, rgb("2A3858"))
    cy = y_feet - h + head
    cv.disc(x, cy, head + 0.5, skin)
    cv.rect(x - head, cy - head - 1, head * 2 + 1, 2, hair)
    if long_hair:
        cv.rect(x - head - 1, cy - head, 1, head * 2 + 2, hair)
        cv.rect(x + head + 1, cy - head, 1, head * 2 + 2, hair)
    cv.put(x - 1, cy, INK)
    cv.put(x + 1, cy, INK)


def pic_canvas(top, bottom, ground=None):
    cv = Canvas(P, P)
    cols = [top, bottom]
    for y in range(P):
        t = y / (P - 1)
        c = tuple(int(cols[0][i] + (cols[1][i] - cols[0][i]) * t) for i in range(3)) + (255,)
        cv.hline(0, y, P, c)
    if ground:
        cv.rect(0, P - 7, P, 7, ground)
        cv.hline(0, P - 7, P, tuple(max(0, v - 30) for v in ground[:3]) + (255,))
    return cv


def pic_alphabet():
    cv = pic_canvas(rgb("CFE4FF"), rgb("EEF5FF"), ground=rgb("8FD19E"))
    bevel_block(cv, 4, 20, 14, rgb("FF6B6B"), rgb("FFA3A3"), rgb("C94545"))
    glyph(cv, "A", 8, 23, WHITE)
    bevel_block(cv, 21, 20, 14, rgb("2F7BFF"), rgb("7FB0FF"), rgb("1A4FC2"))
    glyph(cv, "B", 25, 23, WHITE)
    bevel_block(cv, 12, 6, 14, rgb("FFC93C"), rgb("FFE58A"), rgb("D19A10"))
    glyph(cv, "Ə", 16, 9, INK)
    return cv


def hand(cv, x, y, skin=rgb("F2C29B"), shade=rgb("D9A27A")):
    """Open palm, about 16 x 20, top-left at x, y."""
    for i, (fx, top) in enumerate(((2, 3), (5, 0), (8, 0), (11, 2))):
        cv.rect(x + fx, y + top, 3, 10 - top, skin)
        cv.vline(x + fx + 2, y + top + 1, 9 - top, shade)
    cv.rect(x + 2, y + 9, 12, 9, skin)
    cv.rect(x - 1, y + 9, 3, 3, skin)  # thumb
    cv.rect(x, y + 11, 3, 3, skin)
    cv.hline(x + 3, y + 17, 10, shade)
    cv.rect(x + 4, y + 18, 8, 3, skin)


def pic_greetings():
    cv = pic_canvas(rgb("FFE2B8"), rgb("FFF4E2"))
    sun(cv, 31, 8, 4)
    hand(cv, 11, 9)
    edge = Canvas(P, P)
    # outline the hand: re-draw onto a clear canvas and outline it
    hand(edge, 11, 9)
    edge.outline(INK)
    cv.img.alpha_composite(edge.img)
    cv.px = cv.img.load()
    for k in range(3):  # wave lines
        cv.put(6 - k, 10 + k * 3, rgb("FF8A3D"))
        cv.put(7 - k, 10 + k * 3, rgb("FF8A3D"))
        cv.put(31 + k, 18 + k * 3, rgb("FF8A3D"))
        cv.put(32 + k, 18 + k * 3, rgb("FF8A3D"))
    return cv


def pic_pronouns():
    cv = pic_canvas(rgb("D9F0FF"), rgb("F0FAFF"), ground=rgb("8FD19E"))
    person(cv, 12, 33, rgb("2F7BFF"))
    person(cv, 28, 33, rgb("FF6B9A"), hair=rgb("7A4E2D"), long_hair=True)
    # pointing arm from the left figure to the right one
    cv.rect(16, 22, 6, 2, rgb("2F7BFF"))
    cv.rect(22, 22, 2, 2, rgb("F2C29B"))
    # little arrows above: "me" / "you"
    cv.rect(11, 5, 3, 3, rgb("FFC93C"))
    cv.rect(27, 5, 3, 3, rgb("FFC93C"))
    return cv


def bubble(cv, x, y, w, h, fill=WHITE):
    cv.rect(x, y, w, h, INK)
    cv.rect(x + 1, y + 1, w - 2, h - 2, fill)
    cv.rect(x + 3, y + h, 3, 2, INK)
    cv.rect(x + 4, y + h - 1, 1, 2, fill)


def pic_questions():
    cv = pic_canvas(rgb("E6DCFF"), rgb("F6F2FF"), ground=rgb("A9B8E8"))
    person(cv, 11, 33, rgb("7C5CFF"), h=20)
    bubble(cv, 17, 4, 18, 17)
    glyph(cv, "?", 21, 6, rgb("7C5CFF"), 2)
    return cv


def pic_family():
    cv = pic_canvas(rgb("FFE0E8"), rgb("FFF4F6"), ground=rgb("8FD19E"))
    person(cv, 8, 33, rgb("2F7BFF"), h=22)
    person(cv, 18, 33, rgb("FF6B9A"), hair=rgb("7A4E2D"), long_hair=True, h=21)
    person(cv, 27, 33, rgb("FFC93C"), h=14)
    person(cv, 34, 33, rgb("45CF8A"), hair=rgb("7A4E2D"), long_hair=True, h=12)
    for dx, dy in ((0, 0), (1, 0), (3, 0), (4, 0), (-1, 1), (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1),
                   (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (1, 3), (2, 3), (3, 3), (2, 4)):
        cv.put(19 + dx, 3 + dy, rgb("FF4D6D"))
    return cv


def pic_numbers():
    cv = pic_canvas(rgb("D6FFF0"), rgb("F0FFF8"))
    colours = [(rgb("FF6B6B"), rgb("FFA3A3"), rgb("C94545")), (rgb("2F7BFF"), rgb("7FB0FF"), rgb("1A4FC2")),
               (rgb("45CF8A"), rgb("8DE8B6"), rgb("2A9A60"))]
    for i, ch in enumerate("123"):
        x = 2 + i * 12
        y = 22 - i * 7
        bevel_block(cv, x, y, 12, *colours[i])
        glyph(cv, ch, x + 3, y + 2, WHITE)
    for i in range(4):  # little counting dots
        cv.rect(4 + i * 4, 37, 2, 2, rgb("2A3858"))
    return cv


def pic_time():
    cv = pic_canvas(rgb("FFF1C4"), rgb("FFF9E6"))
    cv.disc(19.5, 19.5, 15.5, INK)
    cv.disc(19.5, 19.5, 14.5, rgb("FF8A3D"))
    cv.disc(19.5, 19.5, 12.5, WHITE)
    for i in range(12):
        a = i * math.pi / 6
        cv.put(int(round(19.5 + math.cos(a) * 10.5)), int(round(19.5 + math.sin(a) * 10.5)), rgb("2A3858"))
    cv.rect(19, 11, 2, 9, INK)  # minute hand to 12
    cv.rect(19, 19, 7, 2, rgb("2F7BFF"))  # hour hand to 3
    cv.rect(18, 18, 4, 4, INK)
    cv.rect(10, 2, 5, 3, INK)  # alarm bells
    cv.rect(25, 2, 5, 3, INK)
    return cv


def pic_objects():
    cv = pic_canvas(rgb("FFE8D6"), rgb("FFF6EE"), ground=rgb("D9B48C"))
    # paint palette
    cv.ellipse(15, 20, 12, 9, INK)
    cv.ellipse(15, 20, 11, 8, rgb("F3D7A6"))
    cv.disc(19, 24, 2.2, rgb("FFF6EE"))
    for (x, y), c in zip(((8, 17), (13, 14), (19, 15), (9, 22)), (rgb("FF4D6D"), rgb("FFC93C"), rgb("2F7BFF"), rgb("45CF8A"))):
        cv.disc(x, y, 2, c)
    # pencil
    for k in range(14):
        cv.rect(26 + k // 2, 32 - k, 3, 1, rgb("FFC93C"))
    cv.rect(26, 32, 2, 1, rgb("F2C29B"))
    cv.put(26, 33, INK)
    cv.rect(32, 18, 3, 2, rgb("FF6B9A"))
    return cv


def pic_places():
    cv = pic_canvas(rgb("BFE3FF"), rgb("E8F6FF"), ground=rgb("8FD19E"))
    cloud(cv, 4, 7, 2)
    # house
    cv.tri([(4, 20), (14, 11), (24, 20)], rgb("FF6B6B"))
    cv.rect(6, 20, 16, 13, rgb("FFF1D6"))
    cv.rect(12, 25, 4, 8, rgb("7A4E2D"))
    cv.rect(8, 22, 3, 3, rgb("6FB7FF"))
    cv.rect(17, 22, 3, 3, rgb("6FB7FF"))
    tree(cv, 31, 33, r=5)
    # map pin
    cv.disc(31, 6, 4, rgb("FF4D6D"))
    cv.disc(31, 6, 1.5, WHITE)
    cv.tri([(28, 8), (34, 8), (31, 14)], rgb("FF4D6D"))
    return cv


def pic_review():
    cv = pic_canvas(rgb("1A4FC2"), rgb("2F7BFF"))
    stars(cv, 4, 0, 40, 14)
    # trophy
    gold, light, dark = rgb("FFC93C"), rgb("FFE58A"), rgb("D19A10")
    cv.rect(12, 8, 16, 11, gold)
    cv.ellipse(20, 18, 8, 5, gold)
    cv.rect(13, 9, 3, 8, light)
    cv.rect(8, 10, 4, 2, gold)
    cv.rect(7, 10, 2, 6, gold)
    cv.rect(28, 10, 4, 2, gold)
    cv.rect(31, 10, 2, 6, gold)
    cv.rect(18, 23, 4, 5, dark)
    cv.rect(14, 28, 12, 3, gold)
    cv.rect(12, 31, 16, 3, dark)
    glyph(cv, "1", 18, 10, dark)
    return cv


PICTURES = {
    "alphabet": pic_alphabet, "greetings": pic_greetings, "pronouns": pic_pronouns, "questions": pic_questions,
    "family": pic_family, "numbers": pic_numbers, "time": pic_time, "objects": pic_objects, "places": pic_places,
    "review": pic_review,
}


def locked_version(img):
    """Frosted: blurred, paler and a little grey, so the picture hints at what is coming."""
    big = img.resize((P * 4, P * 4), Image.NEAREST).filter(ImageFilter.GaussianBlur(5))
    small = big.resize((P, P), Image.BILINEAR)
    grey = small.convert("L").convert("RGBA")
    mixed = Image.blend(small, grey, 0.3)
    pale = Image.blend(mixed, Image.new("RGBA", (P, P), (235, 241, 252, 255)), 0.15)
    return pale


def make_pictures(preview):
    sheet = Image.new("RGBA", (len(PICTURES) * (P * 4 + 8), 2 * (P * 4 + 8)), (240, 246, 255, 255))
    for i, (name, draw) in enumerate(PICTURES.items()):
        img = draw().img
        img.save(os.path.join(UNITS, "unitpic_%s.png" % name))
        lock = locked_version(img)
        lock.save(os.path.join(UNITS, "unitpic_%s_locked.png" % name))
        sheet.paste(img.resize((P * 4, P * 4), Image.NEAREST), (i * (P * 4 + 8) + 4, 4))
        sheet.paste(lock.resize((P * 4, P * 4), Image.NEAREST), (i * (P * 4 + 8) + 4, P * 4 + 12))
    if preview:
        sheet.save(os.path.join(PREVIEW, "unit_pictures.png"))


# ------------------------------------------------------------------------------------------ home skies
# 216 x 384 (shown at 5x). The top ~70 rows sit under the level card, the middle is the path, the city skyline and
# the Caspian sit at the bottom just above the tab bar.

W, H = 216, 384

MOODS = {
    "morning": dict(sky=["FFB38A", "FFC79E", "FFD9B3", "FFE6C7", "FFF0DC", "FFF7EC"], body="sun_low",
                    city="B98FA8", city_trim="A67C97", window="FFE3B0", lit=None, sea="9FC4E8", sea_line="C7DDF2",
                    hills="E8B9A8"),
    "noon": dict(sky=["6FB7FF", "86C4FF", "9ED0FF", "B6DCFF", "CDE7FF", "E2F1FF"], body="sun_high",
                 city="7F9CC9", city_trim="6D89B5", window="CFE2FF", lit=None, sea="4FA3E8", sea_line="8CC6F2",
                 hills="A6D8B4"),
    "evening": dict(sky=["3B3F8F", "6A4C9C", "B05C93", "E8738A", "FF9A6E", "FFC37A"], body="sun_set",
                    city="4A3A6E", city_trim="3C2F5C", window="6C5A93", lit="FFD27A", sea="7A5C9C", sea_line="E88A8A",
                    hills="5E4A80"),
    "night": dict(sky=["0B1640", "11204F", "182B60", "203772", "294385", "324F96"], body="moon",
                  city="1A2448", city_trim="131B38", window="26325E", lit="FFD86B", sea="16245A", sea_line="3B4E96",
                  hills="18264F"),
    "rain": dict(sky=["7A8597", "8792A3", "95A0B0", "A3ADBC", "B1BAC7", "C0C8D3"], body=None,
                 city="6C7689", city_trim="5D6678", window="9AA4B5", lit="FFE3A0", sea="6F8299", sea_line="98A8BB",
                 hills="8A9A92", clouds="dark"),
    "rain_night": dict(sky=["141B2C", "1A2236", "212A40", "28324A", "303B55", "38445F"], body=None,
                       city="1C2234", city_trim="151A28", window="283048", lit="F5C86A", sea="1F2A40", sea_line="3A4862",
                       hills="222C3A", clouds="night"),
    "snow": dict(sky=["B8C6DA", "C3D0E2", "CEDAE9", "D9E3F0", "E4ECF6", "EEF3FA"], body=None,
                 city="8E9BB3", city_trim="7D8AA3", window="C2CDE0", lit="FFE3A0", sea="8FA6C4", sea_line="B8C8DD",
                 hills="F4F8FF", clouds="light", snow_caps=True),
    "snow_night": dict(sky=["1B2540", "222D4A", "2A3655", "333F60", "3C496B", "465476"], body="moon",
                       city="232C48", city_trim="1A2139", window="2E3858", lit="FFD86B", sea="243457", sea_line="44567E",
                       hills="C9D3E6", snow_caps=True),
}


def flame_tower(cv, x, base, w, h, wall, trim, window, lit, seed):
    """A flame-shaped tower: wide base narrowing to a pointed, slightly bent top."""
    rnd = random.Random(seed)
    for y in range(h):
        t = y / float(h)
        half = (w / 2.0) * (1.0 - t ** 1.6)
        lean = 3 * t * t
        x0 = int(round(x - half + lean))
        x1 = int(round(x + half + lean))
        cv.hline(x0, base - y, max(1, x1 - x0), wall)
        cv.put(x1 - 1, base - y, trim)
        if y % 4 == 2 and x1 - x0 > 5:
            for xx in range(x0 + 2, x1 - 2, 3):
                colour = lit if lit and rnd.random() < 0.4 else window
                cv.put(xx, base - y, colour)


def smooth(colors, n):
    """Spreads a few key colours over n bands, so the sky changes gently from top to bottom."""
    out = []
    for i in range(n):
        t = i * (len(colors) - 1) / float(n - 1)
        a, b = colors[int(math.floor(t))], colors[min(len(colors) - 1, int(math.floor(t)) + 1)]
        f = t - math.floor(t)
        out.append(tuple(int(a[k] + (b[k] - a[k]) * f) for k in range(3)) + (255,))
    return out


def birds(cv, points, c):
    for x, y in points:
        for dx, dy in ((0, 0), (1, -1), (2, 0), (3, 0), (4, -1), (5, 0)):
            cv.put(x + dx, y + dy, c)


def boulevard_lamps(cv, night):
    for x in range(10, W, 34):
        cv.rect(x, 304, 1, 14, rgb("2A3858"))
        cv.rect(x - 1, 302, 3, 2, rgb("FFE07A") if night else rgb("2A3858"))
        if night:
            cv.put(x - 2, 303, rgb("FFE07A", 140))
            cv.put(x + 2, 303, rgb("FFE07A", 140))


def sky_scene(mood):
    m = MOODS[mood]
    cv = Canvas(W, H)
    sky(cv, smooth([rgb(c) for c in m["sky"]], 12), 0, 300)
    night = mood in ("night", "rain_night", "snow_night")
    if mood in ("night", "snow_night"):
        stars(cv, 7, 0, 250, 90)
    if mood == "evening":
        stars(cv, 9, 0, 70, 18)

    body = m["body"]
    if body == "sun_low":
        sun(cv, 52, 236, 13)
    elif body == "sun_high":
        sun(cv, 160, 112, 12)
    elif body == "sun_set":
        cv.disc(150, 262, 20, rgb("FFB46B"))
        cv.disc(150, 262, 16, rgb("FFD27A"))
    elif body == "moon":
        moon(cv, 160, 110, 11, rgb(m["sky"][1]))

    kind = m.get("clouds")
    if kind == "dark":
        for i, (x, y, s) in enumerate(((-6, 96, 9), (70, 84, 11), (150, 100, 9), (20, 150, 7), (120, 160, 8))):
            cloud(cv, x, y, s, light=rgb("9AA3B2"), shade=rgb("7E8899"))
    elif kind == "night":
        for x, y, s in ((-6, 96, 9), (70, 84, 11), (150, 100, 9), (30, 160, 7)):
            cloud(cv, x, y, s, light=rgb("2E3850"), shade=rgb("252E44"))
    elif kind == "light":
        for x, y, s in ((-6, 100, 8), (90, 88, 10), (160, 140, 7)):
            cloud(cv, x, y, s, light=rgb("F4F7FC"), shade=rgb("D9E2EF"))
    elif mood == "noon":
        cloud(cv, 18, 150, 6)
        cloud(cv, 120, 196, 5)
        cloud(cv, -8, 236, 4)
        cloud(cv, 150, 250, 3)
        birds(cv, ((60, 120), (70, 126), (52, 128)), rgb("2A3858"))
    elif mood == "morning":
        cloud(cv, 120, 180, 5, light=rgb("FFF1E6"), shade=rgb("FFD9C2"))
        cloud(cv, 10, 140, 4, light=rgb("FFF1E6"), shade=rgb("FFD9C2"))
        cloud(cv, 160, 120, 3, light=rgb("FFF1E6"), shade=rgb("FFD9C2"))
        birds(cv, ((90, 210), (100, 205), (82, 214)), rgb("8A5A6A"))
    elif mood == "evening":
        cloud(cv, 20, 190, 5, light=rgb("FFB0A0"), shade=rgb("E8889A"))
        cloud(cv, 120, 150, 4, light=rgb("F59AAE"), shade=rgb("C9789A"))
        cloud(cv, 60, 230, 3, light=rgb("FFC08A"), shade=rgb("F09A7A"))

    # distant hills
    hills = rgb(m["hills"])
    for x in range(W):
        y = 296 - int(8 + 6 * math.sin(x / 23.0) + 4 * math.sin(x / 9.0 + 1))
        cv.vline(x, y, 300 - y, hills)

    # city skyline with three flame towers on the right
    wall, trim, window = rgb(m["city"]), rgb(m["city_trim"]), rgb(m["window"])
    lit = rgb(m["lit"]) if m["lit"] else None
    blocks = ((0, 22, 30), (20, 16, 44), (34, 26, 24), (58, 14, 52), (70, 24, 32), (92, 18, 40), (108, 14, 26))
    for i, (x, w, h) in enumerate(blocks):
        building(cv, x, 318, w, h, wall, trim, window, lit=lit, seed=i + 3, win=(2, 3), gap=(3, 4))
    flame_tower(cv, 142, 318, 20, 70, wall, trim, window, lit, 1)
    flame_tower(cv, 165, 318, 24, 84, wall, trim, window, lit, 2)
    flame_tower(cv, 188, 318, 20, 66, wall, trim, window, lit, 3)
    if m.get("snow_caps"):
        for x in range(W):
            for y in range(200, 318):
                if cv.px[x, y][:3] in (wall[:3], trim[:3]) and cv.px[x, y - 1][:3] not in (wall[:3], trim[:3]):
                    cv.rect(x, y, 1, 2, rgb("F4F8FF"))
                    break

    boulevard_lamps(cv, night or mood == "evening")

    # boulevard and the sea
    cv.rect(0, 318, W, 6, rgb("2A3858") if night else rgb("8A94A8"))
    cv.hline(0, 318, W, rgb("C9D2E3") if not night else rgb("3E4C70"))
    sea, line = rgb(m["sea"]), rgb(m["sea_line"])
    cv.rect(0, 324, W, H - 324, sea)
    rnd = random.Random(5)
    for _ in range(70):
        x, y = rnd.randrange(W), rnd.randrange(328, H)
        cv.hline(x, y, rnd.randrange(3, 8), line)
    if body in ("sun_set", "moon"):  # light path on the water
        for y in range(328, H, 3):
            w = rnd.randrange(4, 12)
            cv.hline(160 - w // 2 if body == "moon" else 150 - w // 2, y, w, rgb("FFD27A") if body == "sun_set" else rgb("FFF4C8"))
    return cv


def make_skies(preview):
    os.makedirs(HOME, exist_ok=True)
    sheet = Image.new("RGBA", (len(MOODS) * (W * 2 + 10), H * 2), (255, 255, 255, 255))
    for i, mood in enumerate(MOODS):
        cv = sky_scene(mood)
        cv.img.save(os.path.join(HOME, "home_bg_%s.png" % mood))
        sheet.paste(cv.img.resize((W * 2, H * 2), Image.NEAREST), (i * (W * 2 + 10), 0))
    if preview:
        sheet.save(os.path.join(PREVIEW, "home_skies.png"))


# ------------------------------------------------------------------------------------------ story cards
# 216 x 50 strips (the Stories list card shows them 4.5x): a slice of the story's own scene with the people or
# things the story is about drawn in front.

CW, CH = 216, 50
STORY = os.path.join(ROOT, "Story")


def wave_person(cv, x, y_feet, shirt, **kw):
    person(cv, x, y_feet, shirt, **kw)
    skin = kw.get("skin", rgb("F2C29B"))
    h = kw.get("h", 18)
    cv.rect(x + 4, y_feet - h + 4, 2, 6, shirt)  # raised arm
    cv.rect(x + 4, y_feet - h + 1, 2, 3, skin)


def small_bubble(cv, x, y, ch, colour):
    cv.rect(x, y, 11, 11, INK)
    cv.rect(x + 1, y + 1, 9, 9, WHITE)
    cv.rect(x + 2, y + 11, 2, 2, INK)
    glyph(cv, ch, x + 3, y + 2, colour)


def heart(cv, x, y, c=rgb("FF4D6D")):
    for dx, dy in ((0, 0), (1, 0), (3, 0), (4, 0), (-1, 1), (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1),
                   (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (1, 3), (2, 3), (3, 3), (2, 4)):
        cv.put(x + dx, y + dy, c)


def backpack(cv, x, y):
    cv.rect(x, y, 14, 16, INK)
    cv.rect(x + 1, y + 1, 12, 14, rgb("FF6B6B"))
    cv.rect(x + 3, y + 7, 8, 6, rgb("C94545"))
    cv.rect(x + 4, y - 3, 6, 3, INK)
    cv.rect(x + 5, y - 2, 4, 2, rgb("FF6B6B"))
    cv.rect(x + 6, y + 9, 2, 2, rgb("FFC93C"))


def firework(cv, x, y, c):
    for k in range(8):
        a = k * math.pi / 4
        for r in (3, 4, 5):
            cv.put(int(round(x + math.cos(a) * r)), int(round(y + math.sin(a) * r)), c)
    cv.put(x, y, WHITE)


FIGURE_SHIFT = 46  # the card text covers the left side, so the people and things move right


def story_base(index, top):
    """A clear canvas for the people and things; the scene slice travels with it as .bg."""
    scene = Image.open(os.path.join(STORY, "story_bg_%02d.png" % index)).convert("RGBA")
    cv = Canvas(CW, CH)
    cv.bg = scene.crop((0, top, CW, top + CH))
    return cv


def compose_card(cv):
    img = cv.bg.copy()
    img.alpha_composite(cv.img, (FIGURE_SHIFT, 0))
    return img


def card_01():
    cv = story_base(1, 236)
    wave_person(cv, 92, 48, rgb("2F7BFF"), h=24)
    wave_person(cv, 124, 48, rgb("FF6B9A"), hair=rgb("7A4E2D"), long_hair=True, h=23)
    small_bubble(cv, 104, 4, "A", rgb("2F7BFF"))
    return cv


def card_02():
    cv = story_base(2, 196)
    wave_person(cv, 112, 48, rgb("FFC93C"), h=24)
    return cv


def card_03():
    cv = story_base(3, 232)
    person(cv, 90, 48, rgb("45CF8A"), h=23)
    person(cv, 120, 48, rgb("7C5CFF"), hair=rgb("7A4E2D"), long_hair=True, h=22)
    small_bubble(cv, 126, 4, "?", rgb("7C5CFF"))
    return cv


def card_04():
    cv = story_base(4, 230)
    person(cv, 70, 48, rgb("2F7BFF"), h=24)
    cv.rect(74, 33, 8, 2, rgb("2F7BFF"))
    cv.rect(82, 33, 2, 2, rgb("F2C29B"))
    person(cv, 150, 48, rgb("FF8A3D"), h=22)
    small_bubble(cv, 76, 8, "?", rgb("FF8A3D"))
    return cv


def card_05():
    cv = story_base(5, 228)
    person(cv, 80, 48, rgb("2F7BFF"), h=26)
    person(cv, 96, 48, rgb("FF6B9A"), hair=rgb("7A4E2D"), long_hair=True, h=25)
    person(cv, 112, 48, rgb("FFC93C"), h=18)
    person(cv, 124, 48, rgb("45CF8A"), hair=rgb("7A4E2D"), long_hair=True, h=15)
    heart(cv, 100, 6)
    return cv


def card_06():
    cv = story_base(6, 244)
    for i, ch in enumerate("123"):
        bevel_block(cv, 86 + i * 15, 6 + (i % 2) * 4, 13, rgb("2F7BFF"), rgb("7FB0FF"), rgb("1A4FC2"))
        glyph(cv, ch, 90 + i * 15, 9 + (i % 2) * 4, WHITE)
    person(cv, 108, 49, rgb("FF6B6B"), h=20)
    return cv


def card_07():
    cv = story_base(7, 120)
    person(cv, 70, 48, rgb("FFC93C"), h=22)
    return cv


def card_08():
    cv = story_base(8, 226)
    backpack(cv, 150, 30)
    person(cv, 120, 48, rgb("2F7BFF"), h=24)
    return cv


def card_09():
    cv = story_base(9, 226)
    person(cv, 80, 48, rgb("FF6B9A"), hair=rgb("7A4E2D"), long_hair=True, h=22)
    person(cv, 96, 48, rgb("2F7BFF"), h=23)
    # map pin
    cv.disc(140, 10, 4, rgb("FF4D6D"))
    cv.disc(140, 10, 1.5, WHITE)
    cv.tri([(137, 12), (143, 12), (140, 18)], rgb("FF4D6D"))
    return cv


def card_10():
    cv = story_base(10, 196)
    firework(cv, 40, 10, rgb("FFC93C"))
    firework(cv, 176, 14, rgb("FF6B9A"))
    firework(cv, 110, 6, rgb("7FB0FF"))
    wave_person(cv, 96, 48, rgb("2F7BFF"), h=24)
    wave_person(cv, 122, 48, rgb("FF6B9A"), hair=rgb("7A4E2D"), long_hair=True, h=23)
    return cv


CARDS = [card_01, card_02, card_03, card_04, card_05, card_06, card_07, card_08, card_09, card_10]


def locked_card(img):
    big = img.resize((CW * 4, CH * 4), Image.NEAREST).filter(ImageFilter.GaussianBlur(6))
    small = big.resize((CW, CH), Image.BILINEAR)
    grey = small.convert("L").convert("RGBA")
    return Image.blend(Image.blend(small, grey, 0.35), Image.new("RGBA", (CW, CH), (40, 52, 80, 255)), 0.12)


def make_story_cards(preview):
    sheet = Image.new("RGBA", (2 * (CW * 3 + 8), len(CARDS) * (CH * 3 + 8)), (255, 255, 255, 255))
    for i, draw in enumerate(CARDS):
        img = compose_card(draw())
        img.save(os.path.join(STORY, "story_card_%02d.png" % (i + 1)))
        lock = locked_card(img)
        lock.save(os.path.join(STORY, "story_card_%02d_locked.png" % (i + 1)))
        sheet.paste(img.resize((CW * 3, CH * 3), Image.NEAREST), (0, i * (CH * 3 + 8)))
        sheet.paste(lock.resize((CW * 3, CH * 3), Image.NEAREST), (CW * 3 + 8, i * (CH * 3 + 8)))
    # Dark fade under the card text: left side darker, fading out to the right (smooth, not pixel art).
    shade = Image.new("RGBA", (128, 4))
    for x in range(128):
        t = x / 127.0
        a = int(215 * max(0.0, 1.0 - t / 0.75) ** 1.3)
        for y in range(4):
            shade.putpixel((x, y), (10, 25, 49, a))
    shade.save(os.path.join(ROOT, "UI", "card_shade.png"))
    if preview:
        sheet.save(os.path.join(PREVIEW, "story_cards.png"))


if __name__ == "__main__":
    if not os.path.isdir(UNITS):
        sys.exit("Run from the repository root.")
    want_preview = "--preview" in sys.argv
    if want_preview:
        os.makedirs(PREVIEW, exist_ok=True)
    make_pictures(want_preview)
    make_skies(want_preview)
    make_story_cards(want_preview)
    print("Home art written to " + ROOT)
