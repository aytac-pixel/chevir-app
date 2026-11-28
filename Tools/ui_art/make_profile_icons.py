"""Draws the Profile page icons that the icon set did not have: icon_chart (statistics), icon_globe (website) and icon_sun (daily greetings).
32x32 pixel grid, white on transparent, scaled to 128 without smoothing like the other icons. Run from the repo root."""
from PIL import Image, ImageDraw

W = (255, 255, 255, 255)
OUT = "Assets/Core/Sprites/Icons/"


def save(small, name):
    small.resize((128, 128), Image.NEAREST).save(OUT + name + ".png")


def chart():
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([3, 27, 28, 29], fill=W)          # base line
    for x0, top in ((5, 17), (12, 10), (19, 14), (25, 5)):
        d.rectangle([x0, top, x0 + 3, 25], fill=W)  # bars
    return im


def globe():
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([3, 3, 28, 28], outline=W, width=3)
    d.ellipse([10, 3, 21, 28], outline=W, width=2)
    d.rectangle([4, 14, 27, 16], fill=W)
    d.rectangle([15, 3, 16, 28], fill=W)
    # hard alpha, as in the rest of the set
    px = im.load()
    for y in range(32):
        for x in range(32):
            px[x, y] = W if px[x, y][3] >= 110 else (0, 0, 0, 0)
    return im


def sun():
    im = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([9, 9, 22, 22], fill=W)
    for box in ([15, 1, 16, 5], [15, 26, 16, 30], [1, 15, 5, 16], [26, 15, 30, 16]):
        d.rectangle(box, fill=W)
    for x, y in ((5, 5), (24, 5), (5, 24), (24, 24)):
        d.rectangle([x, y, x + 2, y + 2], fill=W)
    return im


if __name__ == "__main__":
    save(sun(), "icon_sun")
    save(chart(), "icon_chart")
    save(globe(), "icon_globe")
    print("icons written")
