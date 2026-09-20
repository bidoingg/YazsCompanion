# Generates the mod's artwork (python + Pillow + numpy; no other dependencies):
#   YazsCompanion.Mod/Art/emblem.png    512x512  the mod's crest: gold diamond, crosshair ring, rank chevrons, claw marks
#   YazsCompanion.Mod/Art/glyphs.png    640x640  5 x 5 atlas of 128 px white glyphs (tinted in game); order = GLYPHS below
#   YazsCompanion.Mod/Art/panel.png     128x128  9-slice panel: chamfered corners, gold hairline, dark gradient body
#   YazsCompanion.Mod/Art/glow.png      128x128  9-slice soft gold glow for the focused / selected element
#   YazsCompanion.Mod/Art/backdrop.jpg  1280x720 the menu's backdrop: warm black, diamond lattice, hatch, vignette
#   YazsCompanion.Mod/Art/field.jpg     740x582  a stand-in for the field behind the DISPLAY tab's readout preview
#   art/banner.png                      1280x640 README / social preview
# The PNGs under YazsCompanion.Mod/Art are embedded into the DLL (see the csproj) and loaded by Art.cs.
# Everything is drawn supersampled and scaled down, so the edges are clean at any size the menu uses.
#   usage: python make_art.py
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "YazsCompanion.Mod", "Art")
os.makedirs(OUT, exist_ok=True)

GOLD = (245, 199, 82)
GOLD_HI = (255, 229, 150)
GOLD_LO = (150, 104, 30)
INK = (13, 12, 10)
RUST = (170, 62, 48)

GLYPHS = ["crosshair", "bullets", "blast", "flame", "bolt",
          "snow", "flask", "blade", "turret", "shield",
          "cross", "paw", "gear", "magnet", "chevrons",
          "clock", "skull", "link", "coin", "heart",
          "hash", "radio", "eye", "diamond", "up"]


# ---------------------------------------------------------------- helpers
class Pen:
    """Draws in a 100 x 100 unit space onto a supersampled single-channel mask."""
    def __init__(self, size, ss=8):
        self.n = size * ss
        self.k = self.n / 100.0
        self.size = size
        self.img = Image.new("L", (self.n, self.n), 0)
        self.d = ImageDraw.Draw(self.img)

    def P(self, pts): return [(x * self.k, y * self.k) for x, y in pts]
    def poly(self, pts, fill=255): self.d.polygon(self.P(pts), fill=fill)
    def circle(self, cx, cy, r, fill=255): self.d.ellipse([(cx - r) * self.k, (cy - r) * self.k, (cx + r) * self.k, (cy + r) * self.k], fill=fill)
    def ellipse(self, x0, y0, x1, y1, fill=255): self.d.ellipse([x0 * self.k, y0 * self.k, x1 * self.k, y1 * self.k], fill=fill)
    def ring(self, cx, cy, r, w, fill=255):
        self.circle(cx, cy, r + w / 2, fill); self.circle(cx, cy, r - w / 2, 0 if fill else 255)
    def rect(self, x0, y0, x1, y1, fill=255, r=0):
        box = [x0 * self.k, y0 * self.k, x1 * self.k, y1 * self.k]
        if r > 0: self.d.rounded_rectangle(box, radius=r * self.k, fill=fill)
        else: self.d.rectangle(box, fill=fill)
    def line(self, x0, y0, x1, y1, w, fill=255, cap=True):
        self.d.line(self.P([(x0, y0), (x1, y1)]), fill=fill, width=max(1, int(round(w * self.k))))
        if cap: self.circle(x0, y0, w / 2, fill); self.circle(x1, y1, w / 2, fill)
    def arc(self, cx, cy, r, a0, a1, w, fill=255):
        self.d.arc([(cx - r) * self.k, (cy - r) * self.k, (cx + r) * self.k, (cy + r) * self.k], a0, a1, fill=fill, width=max(1, int(round(w * self.k))))
    def done(self): return self.img.resize((self.size, self.size), Image.LANCZOS)


def rot(pts, deg, cx=50, cy=50):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def chevron(p, cy, half, thick, drop, fill=255):
    """An upward chevron centred on x = 50 whose tip is at cy."""
    p.poly([(50, cy), (50 + half, cy + drop), (50 + half, cy + drop + thick), (50, cy + thick), (50 - half, cy + drop + thick), (50 - half, cy + drop)], fill)


# ---------------------------------------------------------------- glyphs
def glyph(name, size=128):
    p = Pen(size)
    if name == "crosshair":
        p.ring(50, 50, 31, 7)
        for a in (0, 90, 180, 270):
            q = rot([(46.5, 4), (53.5, 4), (53.5, 30), (46.5, 30)], a); p.poly(q)
        p.circle(50, 50, 5.5)
    elif name == "bullets":
        for x in (24, 50, 76):
            p.rect(x - 9, 46, x + 9, 90, r=2)
            p.ellipse(x - 9, 10, x + 9, 82)
            p.rect(x - 9, 46, x + 9, 60)
            p.rect(x - 10, 68, x + 10, 72, fill=0)
    elif name == "blast":
        pts = []
        for i in range(16):
            r = 47 if i % 2 == 0 else 21
            a = math.radians(i * 22.5 - 90)
            pts.append((50 + r * math.cos(a), 50 + r * math.sin(a)))
        p.poly(pts); p.circle(50, 50, 9, 0)
    elif name == "flame":
        def flame(sc, dy, fill):
            base = [(50, 4), (60, 22), (74, 36), (82, 56), (78, 76), (64, 92), (50, 96), (36, 92), (22, 76), (18, 58), (24, 42), (32, 50), (36, 30), (44, 18)]
            p.poly([(50 + (x - 50) * sc, 96 + (y - 96) * sc + dy) for x, y in base], fill)
        flame(1.0, 0, 255); flame(0.48, -4, 0)
    elif name == "bolt":
        p.poly([(60, 3), (20, 56), (44, 56), (34, 97), (82, 40), (56, 40), (70, 3)])
    elif name == "snow":
        for a in (0, 60, 120):
            q = rot([(46.5, 5), (53.5, 5), (53.5, 95), (46.5, 95)], a); p.poly(q)
            for end in (0, 180):
                for side in (-1, 1):
                    q = rot(rot([(47.2, 8), (52.8, 8), (52.8, 24), (47.2, 24)], side * 46, 50, 24), a + end); p.poly(q)
        p.circle(50, 50, 6)
    elif name == "flask":
        p.rect(34, 6, 66, 15, r=3)
        p.poly([(41, 12), (59, 12), (59, 40), (88, 84), (84, 94), (16, 94), (12, 84), (41, 40)])
        p.poly([(33, 62), (67, 62), (79, 84), (21, 84)], 0)
        p.poly([(30, 70), (70, 70), (78, 84), (22, 84)])
        p.circle(44, 60, 4, 255); p.circle(56, 52, 3, 255)
    elif name == "blade":
        p.poly(rot([(46, -6), (50, -14), (54, -6), (54, 64), (46, 64)], 45))
        p.poly(rot([(34, 64), (66, 64), (66, 71), (34, 71)], 45))
        p.poly(rot([(46.5, 71), (53.5, 71), (53.5, 100), (46.5, 100)], 45))
        p.circle(*rot([(50, 102)], 45)[0], 5)
    elif name == "turret":
        p.poly([(24, 92), (76, 92), (62, 70), (38, 70)])
        p.rect(44, 60, 56, 74)
        p.rect(20, 32, 66, 62, r=6)
        p.rect(62, 40, 96, 47, r=1); p.rect(62, 50, 96, 57, r=1)
        p.circle(36, 47, 6, 0)
    elif name == "shield":
        outer = [(50, 4), (88, 16), (88, 50), (76, 76), (50, 96), (24, 76), (12, 50), (12, 16)]
        p.poly(outer)
        p.poly([(50 + (x - 50) * 0.74, 50 + (y - 50) * 0.76) for x, y in outer], 0)
        p.poly([(50, 30), (66, 50), (50, 70), (34, 50)])
    elif name == "cross":
        p.rect(38, 10, 62, 90, r=5); p.rect(10, 38, 90, 62, r=5)
    elif name == "paw":
        p.ellipse(28, 48, 72, 92)
        p.poly([(30, 70), (50, 46), (70, 70)], 255)
        for cx, cy, rx, ry in ((18, 46, 10, 13), (39, 24, 10, 14), (61, 24, 10, 14), (82, 46, 10, 13)):
            p.ellipse(cx - rx, cy - ry, cx + rx, cy + ry)
    elif name == "gear":
        for i in range(8): p.poly(rot([(41, 4), (59, 4), (62, 24), (38, 24)], i * 45))
        p.circle(50, 50, 33); p.circle(50, 50, 14, 0)
    elif name == "magnet":
        p.circle(50, 46, 40); p.circle(50, 46, 17, 0)
        p.rect(0, 46, 100, 100, 0)
        p.rect(10, 44, 33, 90); p.rect(67, 44, 90, 90)
        p.rect(8, 70, 35, 75, 0); p.rect(65, 70, 92, 75, 0)
    elif name == "chevrons":
        chevron(p, 8, 38, 15, 24); chevron(p, 36, 38, 15, 24); chevron(p, 64, 38, 15, 24)
    elif name == "clock":
        p.ring(50, 50, 38, 8)
        p.line(50, 50, 50, 24, 7); p.line(50, 50, 68, 60, 7)
        for a in (0, 90, 180, 270): p.poly(rot([(48, 16), (52, 16), (52, 22), (48, 22)], a))
    elif name == "skull":
        p.ellipse(12, 6, 88, 74)
        p.rect(30, 60, 70, 92, r=5)
        p.ellipse(24, 34, 46, 58, 0); p.ellipse(54, 34, 76, 58, 0)
        p.poly([(50, 56), (56, 68), (44, 68)], 0)
        for x in (41, 50, 59): p.rect(x - 1.6, 78, x + 1.6, 93, 0)
    elif name == "link":
        a, b, c = (50, 16), (16, 80), (84, 80)
        for u, v in ((a, b), (b, c), (c, a)): p.line(u[0], u[1], v[0], v[1], 6, cap=False)
        for q in (a, b, c): p.circle(q[0], q[1], 14)
        for q in (a, b, c): p.circle(q[0], q[1], 6, 0)
    elif name == "coin":
        for y in (72, 52, 32):
            p.ellipse(14, y - 2, 86, y + 26, 0)
            p.ellipse(14, y - 8, 86, y + 20)
            p.ellipse(26, y - 3, 74, y + 12, 0)
            p.ellipse(34, y, 66, y + 9)
    elif name == "heart":
        p.circle(31, 34, 23); p.circle(69, 34, 23)
        p.poly([(10, 44), (90, 44), (50, 94)])
    elif name == "hash":
        for x in (34, 64): p.poly([(x + 4, 8), (x + 14, 8), (x + 2, 92), (x - 8, 92)])
        for y in (30, 60): p.rect(8, y, 92, y + 10)
    elif name == "radio":
        p.poly([(50, 40), (68, 94), (60, 94), (50, 62), (40, 94), (32, 94)])
        p.circle(50, 34, 9)
        for r in (22, 36):
            p.arc(50, 34, r, -55, 55, 6); p.arc(50, 34, r, 125, 235, 6)
    elif name == "eye":
        p.poly([(2, 50), (22, 26), (50, 16), (78, 26), (98, 50), (78, 74), (50, 84), (22, 74)])
        p.poly([(16, 50), (30, 35), (50, 28), (70, 35), (84, 50), (70, 65), (50, 72), (30, 65)], 0)
        p.circle(50, 50, 15); p.circle(45, 45, 4.5, 0)
    elif name == "diamond":
        p.poly([(50, 3), (97, 50), (50, 97), (3, 50)]); p.poly([(50, 16), (84, 50), (50, 84), (16, 50)], 0)
        p.poly([(50, 31), (69, 50), (50, 69), (31, 50)])
    elif name == "up":
        chevron(p, 24, 38, 18, 30)
    else:
        raise ValueError(name)
    return p.done()


def make_glyphs():
    cell = 128
    atlas = Image.new("RGBA", (cell * 5, cell * 5), (255, 255, 255, 0))
    for i, name in enumerate(GLYPHS):
        m = glyph(name, cell - 8)                       # 4 px of air so bilinear sampling never bleeds into a neighbour
        tile = Image.new("RGBA", (cell, cell), (255, 255, 255, 0))
        white = Image.new("RGBA", m.size, (255, 255, 255, 255)); white.putalpha(m)
        tile.paste(white, (4, 4))
        atlas.paste(tile, ((i % 5) * cell, (i // 5) * cell))
    atlas.save(os.path.join(OUT, "glyphs.png"), optimize=True)
    return atlas


# ---------------------------------------------------------------- shading
def gold_fill(mask, angle=125, lo=GOLD_LO, mid=GOLD, hi=GOLD_HI):
    """Brushed gold: a diagonal gradient with a bright band, clipped by the mask."""
    w, h = mask.size
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    a = math.radians(angle)
    t = (xs * math.cos(a) + ys * math.sin(a)); t = (t - t.min()) / (t.max() - t.min())
    band = np.exp(-((t - 0.38) ** 2) / 0.012)
    out = np.zeros((h, w, 4), np.float32)
    for c in range(3):
        base = hi[c] + (lo[c] - hi[c]) * (1 - t) ** 1.1 if False else lo[c] + (mid[c] - lo[c]) * (1 - t)
        out[..., c] = np.clip(base + (hi[c] - base) * band * 0.85, 0, 255)
    out[..., 3] = np.asarray(mask, np.float32)
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def glow_of(mask, radius, color, strength=1.0):
    g = mask.filter(ImageFilter.GaussianBlur(radius))
    arr = np.asarray(g, np.float32) * strength
    out = np.zeros((mask.size[1], mask.size[0], 4), np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = color
    out[..., 3] = np.clip(arr, 0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


# ---------------------------------------------------------------- emblem
def make_emblem(size=512):
    ss = 4; n = size * ss; k = n / 100.0
    def mask(): return Image.new("L", (n, n), 0)
    def P(pts): return [(x * k, y * k) for x, y in pts]
    dia = lambda r: [(50, 50 - r), (50 + r, 50), (50, 50 + r), (50 - r, 50)]

    body = mask(); ImageDraw.Draw(body).polygon(P(dia(44)), fill=255)
    frame = mask(); d = ImageDraw.Draw(frame)
    d.polygon(P(dia(46)), fill=255); d.polygon(P(dia(41.5)), fill=0)
    d.polygon(P(dia(38.6)), fill=255); d.polygon(P(dia(37.6)), fill=0)
    for cx, cy in ((50, 4), (96, 50), (50, 96), (4, 50)):
        d.polygon(P([(cx, cy - 4), (cx + 4, cy), (cx, cy + 4), (cx - 4, cy)]), fill=255)

    sym = mask(); d = ImageDraw.Draw(sym)
    # crosshair ring with four ticks
    r, w = 24.5, 2.6
    d.ellipse([(50 - r - w / 2) * k, (50 - r - w / 2) * k, (50 + r + w / 2) * k, (50 + r + w / 2) * k], fill=255)
    d.ellipse([(50 - r + w / 2) * k, (50 - r + w / 2) * k, (50 + r - w / 2) * k, (50 + r - w / 2) * k], fill=0)
    for a in (0, 90, 180, 270):
        d.polygon(P(rot([(48.7, 19), (51.3, 19), (51.3, 30), (48.7, 30)], a)), fill=255)
    # rank chevrons: the advice, the level-up, the way up
    for i, cy in enumerate((32.5, 45.5, 58.5)):
        half, thick, drop = 15, 5.6, 9.5
        d.polygon(P([(50, cy), (50 + half, cy + drop), (50 + half, cy + drop + thick), (50, cy + thick), (50 - half, cy + drop + thick), (50 - half, cy + drop)]), fill=255)
    # the ring does not cross the chevrons
    cut = mask(); dc = ImageDraw.Draw(cut)
    for cy in (32.5, 45.5, 58.5):
        half, thick, drop, g = 17.4, 5.6, 9.5, 2.2
        dc.polygon(P([(50, cy - g), (50 + half, cy + drop - g), (50 + half, cy + drop + thick + g), (50, cy + thick + g), (50 - half, cy + drop + thick + g), (50 - half, cy + drop - g)]), fill=255)
    ring_only = mask(); dr = ImageDraw.Draw(ring_only)
    dr.ellipse([(50 - r - w / 2) * k, (50 - r - w / 2) * k, (50 + r + w / 2) * k, (50 + r + w / 2) * k], fill=255)
    dr.ellipse([(50 - r + w / 2) * k, (50 - r + w / 2) * k, (50 + r - w / 2) * k, (50 + r - w / 2) * k], fill=0)
    ring_cut = ImageChops.multiply(ring_only, ImageChops.invert(cut))
    chev = ImageChops.subtract(sym, ring_only)
    ticks = mask(); dt = ImageDraw.Draw(ticks)
    for a in (0, 90, 180, 270): dt.polygon(P(rot([(48.7, 19), (51.3, 19), (51.3, 30), (48.7, 30)], a)), fill=255)
    sym = ImageChops.lighter(ImageChops.lighter(chev, ring_cut), ImageChops.multiply(ticks, ImageChops.invert(cut)))

    # three claw marks raking the lower right: what the squad is up against
    claws = mask(); dcl = ImageDraw.Draw(claws)
    for off, a, b, wmax in ((-8.0, (55.5, 50.0), (69.0, 80.0), 1.7), (0.0, (61.0, 45.0), (79.5, 86.0), 2.1), (8.0, (69.5, 47.0), (84.0, 78.0), 1.6)):
        (x0, y0), (x1, y1) = a, b
        dx, dy = x1 - x0, y1 - y0; ln = math.hypot(dx, dy); nx, ny = -dy / ln, dx / ln
        left, right = [], []
        for i in range(25):
            t = i / 24.0
            bow = 1.6 * math.sin(math.pi * t)                     # the rake is not a ruler line
            w = wmax * (math.sin(math.pi * t) ** 0.7)
            cx, cy2 = x0 + dx * t + nx * bow, y0 + dy * t + ny * bow
            left.append((cx + nx * w, cy2 + ny * w)); right.append((cx - nx * w, cy2 - ny * w))
        dcl.polygon(P(left + right[::-1]), fill=255)
    claws = ImageChops.multiply(claws, body)

    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    img.alpha_composite(glow_of(frame, 9 * ss, GOLD, 0.55))
    # body: warm black with a lift in the middle
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    rad = np.sqrt((xs - n / 2) ** 2 + (ys - n * 0.46) ** 2) / (n * 0.5)
    lift = np.clip(1 - rad, 0, 1) ** 1.6
    b = np.zeros((n, n, 4), np.float32)
    b[..., 0] = 14 + 40 * lift; b[..., 1] = 12 + 31 * lift; b[..., 2] = 10 + 18 * lift
    b[..., 3] = np.asarray(body, np.float32)
    img.alpha_composite(Image.fromarray(b.astype(np.uint8), "RGBA"))
    img.alpha_composite(glow_of(ImageChops.lighter(ring_cut, ticks), 3 * ss, GOLD, 0.32))
    sym_lit = ImageChops.multiply(sym, ImageChops.invert(claws.filter(ImageFilter.MaxFilter(3))))
    img.alpha_composite(gold_fill(sym_lit))
    img.alpha_composite(gold_fill(frame))
    # the claw marks: dark cuts with a rust edge
    edge = claws.filter(ImageFilter.MaxFilter(2 * ss + 1))
    img.alpha_composite(glow_of(edge, 1.2 * ss, RUST, 0.9))
    cutimg = np.zeros((n, n, 4), np.uint8); cutimg[..., 0:3] = (8, 6, 5); cutimg[..., 3] = np.asarray(claws)
    img.alpha_composite(Image.fromarray(cutimg, "RGBA"))
    inner = int(size * 0.9)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(img.resize((inner, inner), Image.LANCZOS), ((size - inner) // 2, (size - inner) // 2))
    out.save(os.path.join(OUT, "emblem.png"), optimize=True)
    return out


# ---------------------------------------------------------------- 9-slice panel and glow
def chamfer(n, inset, cut):
    a, b = inset, n - inset
    return [(a + cut, a), (b - cut, a), (b, a + cut), (b, b - cut), (b - cut, b), (a + cut, b), (a, b - cut), (a, a + cut)]


def make_panel(size=128):
    ss = 4; n = size * ss
    body = Image.new("L", (n, n), 0); ImageDraw.Draw(body).polygon([(x * ss, y * ss) for x, y in chamfer(size, 3, 22)], fill=255)
    line = Image.new("L", (n, n), 0); d = ImageDraw.Draw(line)
    d.polygon([(x * ss, y * ss) for x, y in chamfer(size, 3, 22)], fill=255)
    d.polygon([(x * ss, y * ss) for x, y in chamfer(size, 7, 20.4)], fill=0)
    inner = Image.new("L", (n, n), 0); d2 = ImageDraw.Draw(inner)
    d2.polygon([(x * ss, y * ss) for x, y in chamfer(size, 12, 18.4)], fill=255)
    d2.polygon([(x * ss, y * ss) for x, y in chamfer(size, 13.5, 17.8)], fill=0)
    # notches: a small solid diamond where each chamfer sits
    dia = Image.new("L", (n, n), 0); d3 = ImageDraw.Draw(dia)
    for cx, cy in ((14, 14), (size - 14, 14), (14, size - 14), (size - 14, size - 14)):
        r = 5.2
        d3.polygon([((cx) * ss, (cy - r) * ss), ((cx + r) * ss, cy * ss), (cx * ss, (cy + r) * ss), ((cx - r) * ss, cy * ss)], fill=255)

    ys = np.linspace(0, 1, n, dtype=np.float32)[:, None] * np.ones((1, n), np.float32)
    arr = np.zeros((n, n, 4), np.float32)
    arr[..., 0] = 24 - 12 * ys; arr[..., 1] = 21 - 10 * ys; arr[..., 2] = 18 - 8 * ys
    arr[..., 3] = np.asarray(body, np.float32) * 0.955
    img = Image.fromarray(arr.astype(np.uint8), "RGBA")
    dim = np.zeros((n, n, 4), np.uint8); dim[..., 0:3] = GOLD; dim[..., 3] = (np.asarray(inner, np.float32) * 0.30).astype(np.uint8)
    img.alpha_composite(Image.fromarray(dim, "RGBA"))
    img.alpha_composite(gold_fill(line))
    img.alpha_composite(gold_fill(dia))
    out = img.resize((size, size), Image.LANCZOS)
    out.save(os.path.join(OUT, "panel.png"), optimize=True)
    return out


def make_glow(size=128):
    ss = 2; n = size * ss
    m = Image.new("L", (n, n), 0); d = ImageDraw.Draw(m)
    d.polygon([(x * ss, y * ss) for x, y in chamfer(size, 30, 12)], fill=255)
    d.polygon([(x * ss, y * ss) for x, y in chamfer(size, 36, 10)], fill=0)
    g = glow_of(m, 9 * ss, GOLD_HI, 2.2)
    core = glow_of(m, 2.5 * ss, (255, 244, 205), 1.4)
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0)); img.alpha_composite(g); img.alpha_composite(core)
    out = img.resize((size, size), Image.LANCZOS)
    out.save(os.path.join(OUT, "glow.png"), optimize=True)
    return out


# ---------------------------------------------------------------- backdrop
def make_backdrop(w=1280, h=720):
    rng = np.random.default_rng(7)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    # vignette with the light sitting upper-left of centre, like a lamp over a map table
    r = np.sqrt(((xs - w * 0.42) / (w * 0.75)) ** 2 + ((ys - h * 0.38) / (h * 0.85)) ** 2)
    light = np.clip(1 - r, 0, 1) ** 1.5
    # a diamond lattice, faint
    period = 96.0
    u = (xs + ys) / period; v = (xs - ys) / period
    lat = np.minimum(np.abs(u - np.round(u)), np.abs(v - np.round(v)))
    lattice = np.clip(1 - lat * period / 1.4, 0, 1)
    nodes = np.clip(1 - (np.abs(u - np.round(u)) + np.abs(v - np.round(v))) * period / 9.0, 0, 1)
    # diagonal hatch, finer
    hatch = (np.sin((xs * 0.8 + ys) * 0.55) * 0.5 + 0.5) ** 6
    grain = rng.normal(0, 1, (h, w)).astype(np.float32)
    grain = np.asarray(Image.fromarray(((grain * 24) + 128).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)), np.float32) / 255 - 0.5
    base = 9 + 30 * light
    img = np.zeros((h, w, 3), np.float32)
    warm = np.array([1.0, 0.86, 0.66], np.float32)
    for c in range(3):
        img[..., c] = base * warm[c] + lattice * (5 + 9 * light) * (GOLD[c] / 255) + nodes * (10 + 22 * light) * (GOLD[c] / 255) + hatch * 2.2 * warm[c] + grain * 7
    out = Image.fromarray(img.clip(0, 255).astype(np.uint8), "RGB")
    out.save(os.path.join(OUT, "backdrop.jpg"), quality=88, optimize=True)
    return out


# ---------------------------------------------------------------- field (the readout preview's stand-in for the game)
def make_field(w=740, h=582):
    """Not a screenshot: a top-down dusk field in the game's register - dark ground with worn patches and a dirt track,
    a crowd of small dark figures, pickups, and bright effects where the readout can sit (fire in the bottom-left corner,
    an electric burst at the right edge), because what the preview has to show is how the text and its backing read over
    dark ground and over bright effects."""
    rng = np.random.default_rng(21)
    S = 2
    W, H = w * S, h * S
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)

    def blur(a, r):
        lo, hi = float(a.min()), float(a.max())
        im = Image.fromarray(((a - lo) / max(1e-6, hi - lo) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
        return np.asarray(im, np.float32) / 255 * (hi - lo) + lo

    big = blur(rng.normal(0, 1, (H, W)).astype(np.float32), 60 * S)
    big = (big - big.min()) / (big.max() - big.min())
    fine = blur(rng.normal(0, 1, (H, W)).astype(np.float32), 1.2 * S)
    fine = (fine - fine.mean()) / (fine.std() + 1e-6)
    ground = np.zeros((H, W, 3), np.float32)
    dark, lit = np.array([30, 36, 30], np.float32), np.array([58, 64, 48], np.float32)
    for c in range(3): ground[..., c] = dark[c] + (lit[c] - dark[c]) * big + fine * 3.0
    # a dirt track from the lower left to the upper right
    t = (ys - (H * 0.80 - xs * 0.52)) / (34 * S)
    track = np.exp(-t * t) * (0.55 + 0.45 * big)
    dirt = np.array([86, 72, 52], np.float32)
    for c in range(3): ground[..., c] = ground[..., c] * (1 - 0.55 * track) + dirt[c] * 0.55 * track
    # paving seams, faint
    seams = ((xs % (96 * S)) < 1.5 * S) | ((ys % (96 * S)) < 1.5 * S)
    ground[seams] *= 0.86

    img = Image.fromarray(ground.clip(0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(img, "RGBA")
    # the horde: small dark figures with a soft shadow, thicker towards the top right
    for _ in range(150):
        x = rng.uniform(0, W); y = rng.uniform(0, H)
        if rng.uniform() > 0.35 + 0.65 * (x / W) * (1 - y / H) * 1.6: continue
        r = rng.uniform(5, 8) * S
        d.ellipse([x - r * 1.2, y - r * 0.2, x + r * 1.2, y + r * 1.3], fill=(0, 0, 0, 70))
        tone = int(rng.uniform(52, 78))
        d.ellipse([x - r, y - r, x + r, y + r], fill=(tone, tone + int(rng.uniform(4, 16)), tone - 8, 255))
        d.ellipse([x - r * 0.45, y - r * 0.55, x + r * 0.45, y + r * 0.2], fill=(118, 40, 34, 255))
    # pickups: XP gems and coins
    for _ in range(46):
        x = rng.uniform(0, W); y = rng.uniform(0, H); r = rng.uniform(2.2, 3.4) * S
        col = (92, 214, 255, 255) if rng.uniform() < 0.7 else (250, 205, 90, 255)
        d.polygon([(x, y - r * 1.4), (x + r, y), (x, y + r * 1.4), (x - r, y)], fill=col)
    base = np.asarray(img.convert("RGB"), np.float32)

    # bright effects, added as light
    glow = np.zeros((H, W, 3), np.float32)
    def burst(cx, cy, radius, color, power):
        rr = np.sqrt((xs - cx * W) ** 2 + (ys - cy * H) ** 2) / (radius * S)
        a = np.clip(1 - rr, 0, 1) ** 2 * power
        for c in range(3): glow[..., c] += a * color[c]
    burst(0.16, 0.80, 150, (255, 140, 40), 1.0); burst(0.13, 0.84, 60, (255, 236, 170), 1.0)      # fire, bottom left
    burst(0.30, 0.92, 90, (255, 120, 30), 0.8)
    burst(0.84, 0.30, 130, (90, 190, 255), 0.9); burst(0.86, 0.28, 44, (230, 250, 255), 1.0)      # electric, right edge
    burst(0.52, 0.50, 34, (255, 250, 220), 0.9)                                                    # a muzzle flash by the squad
    burst(0.62, 0.72, 80, (140, 230, 90), 0.5)                                                     # a chemical cloud
    out = base + glow * 0.9
    # the squad in the middle: three lighter figures
    sq = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGB").convert("RGBA")
    d = ImageDraw.Draw(sq, "RGBA")
    for dx, dy, col in ((-16, 6, (70, 110, 170)), (12, -8, (170, 120, 60)), (20, 16, (90, 150, 90))):
        x = W * 0.5 + dx * S; y = H * 0.52 + dy * S; r = 8 * S
        d.ellipse([x - r * 1.2, y - r * 0.2, x + r * 1.2, y + r * 1.3], fill=(0, 0, 0, 90))
        d.ellipse([x - r, y - r, x + r, y + r], fill=col + (255,))
        d.ellipse([x - r * 0.5, y - r * 0.6, x + r * 0.5, y + r * 0.1], fill=(225, 190, 150, 255))
    res = sq.convert("RGB").resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6))
    # a vignette, so the window sits in the menu
    vy, vx = np.mgrid[0:h, 0:w].astype(np.float32)
    vr = np.sqrt(((vx - w / 2) / (w * 0.72)) ** 2 + ((vy - h / 2) / (h * 0.72)) ** 2)
    vig = np.clip(1.08 - vr ** 2.4 * 0.55, 0.55, 1.0)
    arr = np.asarray(res, np.float32) * vig[..., None]
    res = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGB")
    res.save(os.path.join(OUT, "field.jpg"), quality=84, optimize=True)
    return res


# ---------------------------------------------------------------- banner
def font(size, weight="Bold", width="Condensed"):
    for path in (r"C:\Windows\Fonts\bahnschrift.ttf",):
        if os.path.exists(path):
            f = ImageFont.truetype(path, size)
            try:
                names = [n.decode() if isinstance(n, bytes) else n for n in f.get_variation_names()]
                want = (weight + " " + width).strip()
                for cand in (want, "Bold Condensed", "SemiBold Condensed", "Condensed", "Bold"):
                    if cand in names: f.set_variation_by_name(cand); break
            except Exception:
                pass
            return f
    return ImageFont.load_default()


def make_banner(emblem, w=1280, h=640):
    bg = make_backdrop(w, h).convert("RGBA")
    d = ImageDraw.Draw(bg)
    em = emblem.resize((400, 400), Image.LANCZOS)
    bg.alpha_composite(em, (70, (h - 400) // 2))
    x = 520
    title = font(124); sub = font(40, "SemiBold"); small = font(30, "Regular")
    d.text((x, 170), "YAZS", font=title, fill=GOLD_HI)
    tw = d.textlength("YAZS ", font=title)
    d.text((x + tw, 170), "COMPANION", font=title, fill=(237, 237, 237))
    d.rectangle([x, 318, x + 640, 321], fill=GOLD)
    d.polygon([(x - 2, 319.5), (x + 9, 308.5), (x + 20, 319.5), (x + 9, 330.5)], fill=GOLD)
    d.text((x, 344), "IN-GAME ADVISOR FOR YET ANOTHER ZOMBIE SURVIVORS", font=sub, fill=(237, 237, 237))
    d.text((x, 404), "card verdicts  ·  live run plan  ·  build guides  ·  Training Yard advice", font=small, fill=(176, 170, 156))
    d.text((x, 446), "BepInEx 6 IL2CPP  ·  Windows + Steam Deck", font=small, fill=(176, 170, 156))
    bg.convert("RGB").save(os.path.join(HERE, "banner.png"), optimize=True)
    return bg


if __name__ == "__main__":
    make_glyphs()
    e = make_emblem()
    make_panel(); make_glow(); make_backdrop(); make_field()
    make_banner(e)
    for f in sorted(os.listdir(OUT)): print(f, os.path.getsize(os.path.join(OUT, f)))
    print("banner.png", os.path.getsize(os.path.join(HERE, "banner.png")))
