"""The settings panel's page icons, drawn as shapes rather than as seven-by-seven grids.

Run from anywhere: python tools/icons.py. Writes the ten icon_*.png into assets/.

Each is drawn white on transparent at 512 and brought down to 128 with a Lanczos filter, which
is what gives them a clean anti-aliased edge at the twenty-odd pixels they are shown at. The
same file is drawn through CustomSprite and tinted at draw time, so one white picture serves
the lit page (amber) and the rest (grey).

Holes are PUNCHED: ImageDraw on an RGBA image replaces the pixel rather than blending, which is
a trap when compositing a mock-up and exactly the tool here -- drawing in (0,0,0,0) cuts a hole.
"""
import math
import os
import sys
from PIL import Image, ImageDraw

S = 512
OUT = 128
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)
DEST = r"C:\projects\vehicle-tweaks\assets"


def blank():
    return Image.new("RGBA", (S, S), CLEAR)


def rot(im, deg, centre=(S / 2, S / 2)):
    return im.rotate(deg, resample=Image.BICUBIC, center=centre)


def merge(*layers):
    out = blank()
    for l in layers:
        out.alpha_composite(l)
    return out


def rounded(d, box, r, fill=WHITE):
    d.rounded_rectangle(box, radius=r, fill=fill)


def thick_line(d, a, b, w, fill=WHITE):
    d.line([a, b], fill=fill, width=w)
    r = w / 2
    for p in (a, b):
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=fill)


# ---------------------------------------------------------------- the ten

def key():
    """A key, lying flat: a ring, a shaft, two teeth."""
    im = blank()
    d = ImageDraw.Draw(im)
    cx, cy = 130, 256
    d.ellipse([cx - 124, cy - 124, cx + 124, cy + 124], fill=WHITE)
    d.ellipse([cx - 52, cy - 52, cx + 52, cy + 52], fill=CLEAR)
    rounded(d, [220, 226, 500, 286], 22)
    rounded(d, [382, 266, 432, 350], 16)
    rounded(d, [450, 266, 500, 364], 16)
    return rot(im, 38, centre=(256, 256))


def stance():
    """Two wheels leaning in at the top, joined by an axle, which is a stance seen head on."""
    wheel = blank()
    d = ImageDraw.Draw(wheel)
    rounded(d, [204, 96, 308, 416], 40)
    left = rot(wheel, -11)
    right = rot(wheel, 11)
    left = left.transform((S, S), Image.AFFINE, (1, 0, 146, 0, 1, 0))
    right = right.transform((S, S), Image.AFFINE, (1, 0, -146, 0, 1, 0))
    axle = blank()
    d = ImageDraw.Draw(axle)
    rounded(d, [120, 240, 392, 272], 12)
    d.ellipse([226, 226, 286, 286], fill=WHITE)
    return merge(left, right, axle)


def wrench():
    """An open-ended spanner on the diagonal."""
    im = blank()
    d = ImageDraw.Draw(im)
    rounded(d, [222, 190, 290, 470], 34)
    d.ellipse([146, 50, 366, 270], fill=WHITE)
    d.rectangle([224, 20, 288, 160], fill=CLEAR)
    d.ellipse([226, 130, 286, 190], fill=CLEAR)
    return rot(im, -40)


def cog():
    """A gear: a disc, eight teeth, a hole."""
    im = blank()
    d = ImageDraw.Draw(im)
    c = S / 2
    d.ellipse([c - 150, c - 150, c + 150, c + 150], fill=WHITE)
    for k in range(8):
        tooth = blank()
        td = ImageDraw.Draw(tooth)
        rounded(td, [c - 38, c - 220, c + 38, c - 120], 16)
        im.alpha_composite(rot(tooth, k * 45))
    d = ImageDraw.Draw(im)
    d.ellipse([c - 62, c - 62, c + 62, c + 62], fill=CLEAR)
    return im


def tyre():
    """A tyre in section, the way the pressure light draws it: a horseshoe with tread under it."""
    im = blank()
    d = ImageDraw.Draw(im)
    c, r = (256, 236), 180
    d.arc([c[0] - r, c[1] - r, c[0] + r, c[1] + r], start=340, end=200, fill=WHITE, width=62)
    for ang in (200, 340):
        a = math.radians(ang)
        p = (c[0] + math.cos(a) * (r - 31), c[1] + math.sin(a) * (r - 31))
        d.ellipse([p[0] - 31, p[1] - 31, p[0] + 31, p[1] + 31], fill=WHITE)
    for ang in (60, 80, 100, 120):
        a = math.radians(ang)
        p = (c[0] + math.cos(a) * (r + 6), c[1] + math.sin(a) * (r + 6))
        tooth = blank()
        td = ImageDraw.Draw(tooth)
        rounded(td, [p[0] - 22, p[1] - 30, p[0] + 22, p[1] + 30], 10)
        im.alpha_composite(rot(tooth, 90 - ang, centre=p))
    return im


def door():
    """A door frame with an arrow leaving through it."""
    im = blank()
    d = ImageDraw.Draw(im)
    rounded(d, [86, 76, 300, 436], 30)
    d.rectangle([146, 136, 340, 376], fill=CLEAR)
    d.rectangle([180, 76, 340, 436], fill=CLEAR)
    d.rectangle([86, 136, 300, 376], fill=CLEAR)
    rounded(d, [86, 76, 300, 136], 24)
    rounded(d, [86, 376, 300, 436], 24)
    rounded(d, [86, 76, 146, 436], 24)
    rounded(d, [196, 230, 380, 282], 18)
    d.polygon([(340, 150), (470, 256), (340, 362)], fill=WHITE)
    return im


def wheel():
    """A steering wheel: rim, hub, three spokes."""
    im = blank()
    d = ImageDraw.Draw(im)
    c = S / 2
    d.ellipse([c - 210, c - 210, c + 210, c + 210], fill=WHITE)
    d.ellipse([c - 156, c - 156, c + 156, c + 156], fill=CLEAR)
    for ang in (0, 180, 90):
        spoke = blank()
        sd = ImageDraw.Draw(spoke)
        sd.rectangle([c, c - 24, c + 200, c + 24], fill=WHITE)
        im.alpha_composite(rot(spoke, ang))
    d = ImageDraw.Draw(im)
    d.ellipse([c - 66, c - 66, c + 66, c + 66], fill=WHITE)
    return im


def arrow():
    """A turn-signal arrow, the shape on every dashboard."""
    im = blank()
    d = ImageDraw.Draw(im)
    d.polygon([(70, 256), (262, 84), (262, 428)], fill=WHITE)
    rounded(d, [232, 188, 446, 324], 26)
    return im


def gauge():
    """A dial: the arc, the needle, the hub."""
    im = blank()
    d = ImageDraw.Draw(im)
    c, r = (256, 290), 200
    d.arc([c[0] - r, c[1] - r, c[0] + r, c[1] + r], start=150, end=30, fill=WHITE, width=58)
    for ang in (150, 30):
        a = math.radians(ang)
        p = (c[0] + math.cos(a) * (r - 29), c[1] + math.sin(a) * (r - 29))
        d.ellipse([p[0] - 29, p[1] - 29, p[0] + 29, p[1] + 29], fill=WHITE)
    a = math.radians(-50)
    tip = (c[0] + math.cos(a) * 150, c[1] + math.sin(a) * 150)
    thick_line(d, c, tip, 44)
    d.ellipse([c[0] - 52, c[1] - 52, c[0] + 52, c[1] + 52], fill=WHITE)
    return im


def sliders():
    """Three sliders, each knob somewhere different."""
    im = blank()
    d = ImageDraw.Draw(im)
    for y, kx in ((134, 200), (256, 336), (378, 160)):
        rounded(d, [80, y - 15, 432, y + 15], 15)
        d.ellipse([kx - 64, y - 64, kx + 64, y + 64], fill=CLEAR)
        d.ellipse([kx - 46, y - 46, kx + 46, y + 46], fill=WHITE)
    return im


ICONS = {
    "icon_key.png": key,
    "icon_stance.png": stance,
    "icon_wrench.png": wrench,
    "icon_cog.png": cog,
    "icon_tyre.png": tyre,
    "icon_door.png": door,
    "icon_wheel.png": wheel,
    "icon_arrow.png": arrow,
    "icon_gauge.png": gauge,
    "icon_sliders.png": sliders,
}

if __name__ == "__main__":
    args = [x for x in sys.argv[1:] if not x.startswith("--")]
    dest = args[0] if args else DEST
    sheet = Image.new("RGBA", (len(ICONS) * 140 + 20, 160), (30, 30, 34, 255))
    for i, (name, fn) in enumerate(ICONS.items()):
        im = fn().resize((OUT, OUT), Image.LANCZOS)
        im.save(os.path.join(dest, name), optimize=True)
        tint = Image.new("RGBA", im.size, (245, 196, 60, 255))
        tint.putalpha(im.getchannel("A"))
        sheet.alpha_composite(tint, (20 + i * 140, 16))
        print(name, im.size)
    if "--sheet" in sys.argv:
        sheet.save(os.path.join(dest, "..", "iconsheet.png"))
        print("iconsheet.png beside assets, for looking at; not shipped")
