"""Generates the pixel-style UI sprites in Assets/Core/Sprites (UI and Icons).

Square panels with a hard dark border, flat blue fills, stair-stepped circles and icons snapped to a
32 x 32 pixel grid. Run from the repository root:  python3 Tools/ui_art/make_pixel_sprites.py
A "__b<N>" suffix in a file name is the 9-slice border Unity uses (see CevirSpriteImportRules).
Icons are pixelated from the files already there, so run it once on fresh Lucide icons only.
"""
import glob
import os
import sys

from PIL import Image

ROOT = os.path.join("Assets", "Core", "Sprites")
UI = os.path.join(ROOT, "UI")
ICONS = os.path.join(ROOT, "Icons")

INK = (10, 25, 49, 255)        # #0A1931 borders and shadows
BLUE = (0, 67, 230, 255)       # #0043E6 buttons, selected tab, active tiles
WHITE = (255, 255, 255, 255)
BACKGROUND = (240, 246, 255)   # #F0F6FF app background
CLEAR = (0, 0, 0, 0)
BORDER = 6                     # border thickness in sprite pixels (1 sprite pixel = 1 canvas unit)


def remove(prefix):
    for path in glob.glob(os.path.join(UI, prefix + "*.png")):
        os.remove(path)


def square(size, fill, border=BORDER, edge=INK):
    image = Image.new("RGBA", (size, size), edge)
    inner = Image.new("RGBA", (size - 2 * border, size - 2 * border), fill)
    image.paste(inner, (border, border))
    return image


def outline(size, border=BORDER):
    image = Image.new("RGBA", (size, size), WHITE)
    image.paste(Image.new("RGBA", (size - 2 * border, size - 2 * border), CLEAR), (border, border))
    return image


def pixel_circle(grid, scale, fill, border_px=0, edge=INK):
    """Stair-stepped circle on a grid x grid canvas, optionally with a one-cell border."""
    small = Image.new("RGBA", (grid, grid), CLEAR)
    c = (grid - 1) / 2.0
    r = grid / 2.0
    for y in range(grid):
        for x in range(grid):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d <= r - 0.35:
                small.putpixel((x, y), edge if border_px and d > r - 0.35 - border_px else fill)
    return small.resize((grid * scale, grid * scale), Image.NEAREST)


def save(image, name):
    image.save(os.path.join(UI, name + ".png"))


def make_ui():
    for prefix in ("panel_r24", "panel_r36", "panel_r48", "outline_r36", "outline_r48", "pill", "button_gradient",
                   "navsel_gradient", "shadow_r36", "shadow_r48"):
        remove(prefix + "__")

    save(square(40, WHITE), "panel_r24__b12")
    save(square(40, WHITE), "panel_r36__b12")
    save(square(40, WHITE), "panel_r48__b12")
    save(outline(40), "outline_r36__b12")
    save(outline(40), "outline_r48__b12")
    save(square(40, WHITE), "pill__b12")
    save(square(40, BLUE), "button_gradient__b12")
    save(square(40, BLUE), "navsel_gradient__b12")
    save(Image.new("RGBA", (40, 40), INK), "shadow_r36__b12")
    save(Image.new("RGBA", (40, 40), INK), "shadow_r48__b12")

    # Simple (stretched) images: border scaled to stay about 6 units at the size they are shown.
    save(square(300, BLUE, border=12), "tile_gradient")
    save(square(300, BLUE, border=12), "tile_active_gradient")
    save(square(960, BLUE, border=6), "profile_header")
    save(Image.new("RGBA", (256, 256), CLEAR), "glow")
    save(pixel_circle(32, 8, WHITE), "circle")
    save(pixel_circle(32, 8, CLEAR, border_px=3, edge=WHITE), "ring")
    save(Image.new("RGBA", (8, 8), WHITE), "white")
    save(az_flag(), "flag_az")
    Image.new("RGB", (540, 960), BACKGROUND).save(os.path.join(UI, "app_background.png"))
    pixelate(os.path.join(UI, "league_cup.png"), 32)


def az_flag():
    """Azerbaijani flag, 16 x 11 pixels inside a dark border, with a white crescent and star, scaled 4x."""
    blue, red, green = (0, 181, 226, 255), (239, 51, 64, 255), (80, 158, 47, 255)
    small = Image.new("RGBA", (16, 11), INK)
    for y in range(1, 10):
        for x in range(1, 15):
            small.putpixel((x, y), blue if y < 4 else red if y < 7 else green)
    for x, y in ((7, 4), (6, 5), (7, 6), (9, 5)):
        small.putpixel((x, y), WHITE)
    return small.resize((64, 44), Image.NEAREST)


def pixelate(path, grid):
    """Snaps an icon to a grid x grid pixel raster (hard alpha), then scales it back up without smoothing."""
    image = Image.open(path).convert("RGBA")
    size = image.size
    small = image.resize((grid, grid), Image.BOX)
    pixels = small.load()
    for y in range(grid):
        for x in range(grid):
            r, g, b, a = pixels[x, y]
            if a >= 110:
                k = 255.0 / a if a else 1.0  # undo premultiplied darkening at edges
                pixels[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), 255)
            else:
                pixels[x, y] = CLEAR
    small.resize(size, Image.NEAREST).save(path)


def make_icons():
    for path in sorted(glob.glob(os.path.join(ICONS, "icon_*.png"))):
        pixelate(path, 32)


if __name__ == "__main__":
    if not os.path.isdir(UI):
        sys.exit("Run from the repository root.")
    make_ui()
    if "--icons" in sys.argv:
        make_icons()
    print("Pixel sprites written to " + ROOT)
