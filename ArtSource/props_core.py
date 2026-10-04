"""Core models: Pip's cloud puffs, plants, the first animals and vehicles.

Every builder returns a list of (name, Model-or-[Model,...]) exports. Models face Unity +Z.
Multi-part models export several named objects in one FBX so Unity can animate the parts.
"""
import math
import random

from pw_lib import Model, tint, mix

# ---------------------------------------------------------------- palette
INK = "3B3A5A"
WOOD = "C8935B"
WOOD_DK = "8E6240"
SOIL = "7A4E33"
SOIL_DK = "5E3A26"
LEAF = "5DB548"
LEAF_DK = "3F8F3A"
LEAF_LT = "8FD16A"
WOOL = "F7F3EA"
SHEEP_FACE = "3E3B48"
CREAM = "FFF4DC"
RED = "E8574A"
CORAL = "FF6F61"
BUTTER = "FFD45C"
LILAC = "B79CFF"
PINK = "FF9EC4"
SKY = "6FC3E8"
WHITE = "FBFAF7"
BLUSH = "FF9AA2"


def cloud_puffs():
    """Three lumpy unit-radius puffs for Pip. Smooth, no colour (the cloud shader paints them)."""
    out = []
    for i, (amp, freq) in enumerate(((0.10, 1.7), (0.13, 2.1), (0.08, 2.6))):
        m = Model(f"cloud_puff_{'abc'[i]}")
        m.blob(1.0, color=WHITE, subdiv=4, amp=amp, freq=freq, seed=11 + i * 5)
        out.append((m.name, m))
    return out


def sprout():
    m = Model("plant_sprout")
    m.cyl(0.012, 0.12, pos=(0, 0.06, 0), color=LEAF_DK, segs=6, bevel=0)
    m.sphere(0.05, pos=(-0.04, 0.12, 0), scale=(1.0, 0.35, 0.6), rot=(0, 0, 25), color=LEAF, subdiv=2)
    m.sphere(0.05, pos=(0.04, 0.12, 0), scale=(1.0, 0.35, 0.6), rot=(0, 0, -25), color=LEAF_LT, subdiv=2)
    return [(m.name, m)]


def flower(color, name):
    """A blooming flower ~0.32 tall: stem with leaves and a 5-petal head. Head is a separate part."""
    stem = Model(name + "_stem")
    stem.cyl(0.014, 0.26, pos=(0, 0.13, 0), color=LEAF_DK, segs=6, bevel=0)
    stem.sphere(0.055, pos=(-0.05, 0.08, 0), scale=(1.0, 0.3, 0.55), rot=(0, 20, 30), color=LEAF, subdiv=2)
    stem.sphere(0.05, pos=(0.05, 0.13, 0.01), scale=(1.0, 0.3, 0.55), rot=(0, -30, -30), color=LEAF_LT, subdiv=2)
    head = Model(name + "_head", pivot=(0, 0.27, 0))
    head.push(pos=(0, 0.27, 0))
    for k in range(5):
        a = k / 5 * math.tau
        head.sphere(0.045, pos=(math.cos(a) * 0.045, 0, math.sin(a) * 0.045), scale=(1.0, 0.45, 0.75),
                    rot=(0, -math.degrees(a), 0), color=color, subdiv=2)
    head.sphere(0.03, pos=(0, 0.012, 0), scale=(1, 0.7, 1), color=BUTTER if color != BUTTER else "F29E38", subdiv=2)
    head.pop()
    return stem, head


def flowers():
    out = []
    for nm, c in (("coral", CORAL), ("butter", BUTTER), ("lilac", LILAC), ("pink", PINK), ("white", WHITE)):
        stem, head = flower(c, "flower_" + nm)
        out.append(("flower_" + nm, [stem, head]))
    return out


def flower_bed(w=1.5, d=0.95, name="flower_bed"):
    """Raised wooden bed. Soil surface at y = 0.12. Origin at ground centre."""
    m = Model(name)
    t = 0.07
    h = 0.16
    m.box((w, h, t), pos=(0, h / 2, -d / 2 + t / 2), color=WOOD, bevel=0.02)
    m.box((w, h, t), pos=(0, h / 2, d / 2 - t / 2), color=tint(WOOD, 0.92), bevel=0.02)
    m.box((t, h, d - 2 * t), pos=(-w / 2 + t / 2, h / 2, 0), color=tint(WOOD, 0.96), bevel=0.02)
    m.box((t, h, d - 2 * t), pos=(w / 2 - t / 2, h / 2, 0), color=tint(WOOD, 0.9), bevel=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box((0.09, h + 0.04, 0.09), pos=(sx * (w / 2 - 0.045), (h + 0.04) / 2, sz * (d / 2 - 0.045)),
                  color=WOOD_DK, bevel=0.02)
    soil = Model(name + "_soil")
    soil.box((w - 2 * t, 0.1, d - 2 * t), pos=(0, 0.07, 0), color=SOIL, bevel=0.01)
    # a few clods
    rnd = random.Random(3)
    for _ in range(9):
        soil.sphere(0.035, pos=(rnd.uniform(-w / 2 + 0.15, w / 2 - 0.15), 0.12, rnd.uniform(-d / 2 + 0.15, d / 2 - 0.15)),
                    scale=(1.2, 0.5, 1.0), color=SOIL_DK, subdiv=1)
    return [(name, [m, soil])]


def sheep(name="sheep", scale=1.0, wool=WOOL):
    """Toy sheep ~0.5 long, facing +Z. Parts: Body, Head (for nods), Legs."""
    body = Model("Body")
    body.push(scale=scale)
    body.blob(0.2, pos=(0, 0.27, 0), scale=(1.0, 0.85, 1.25), color=wool, subdiv=3, amp=0.16, freq=5.5, seed=2)
    for (x, y, z, r) in ((0.0, 0.42, -0.05, 0.1), (0.07, 0.4, 0.07, 0.08), (-0.08, 0.39, 0.06, 0.08), (0, 0.36, -0.2, 0.08)):
        body.blob(r, pos=(x, y, z), color=wool, subdiv=2, amp=0.15, freq=4.0, seed=int(x * 100 + z * 50) + 7)
    body.blob(0.05, pos=(0, 0.3, -0.27), color=wool, subdiv=2, amp=0.2, freq=4, seed=9)  # tail
    body.pop()
    head = Model("Head", pivot=(0, 0.33 * scale, 0.2 * scale))
    head.push(pos=(0, 0.33 * scale, 0.24 * scale), scale=scale)
    head.sphere(0.1, pos=(0, 0, 0.04), scale=(0.75, 0.82, 1.0), color=SHEEP_FACE, subdiv=3)
    head.blob(0.07, pos=(0, 0.08, 0.0), color=wool, subdiv=2, amp=0.2, freq=5, seed=4)  # tuft
    for sx in (-1, 1):
        head.sphere(0.045, pos=(sx * 0.09, 0.02, -0.01), scale=(1.3, 0.45, 0.7), rot=(0, sx * 20, sx * -25), color=SHEEP_FACE, subdiv=2)
        head.sphere(0.017, pos=(sx * 0.042, 0.025, 0.11), color=WHITE, subdiv=1)
        head.sphere(0.009, pos=(sx * 0.043, 0.025, 0.124), color="111118", subdiv=1)
    head.pop()
    legs = Model("Legs")
    legs.push(scale=scale)
    for sx in (-1, 1):
        for sz in (-1, 1):
            legs.cyl(0.032, 0.16, pos=(sx * 0.09, 0.08, sz * 0.13), color=SHEEP_FACE, segs=8, bevel=0.012)
    legs.pop()
    return [(name, [body, head, legs])]


def tree_round(name="tree_round", seed=1, leaf=LEAF, h=1.0):
    rnd = random.Random(seed)
    m = Model(name)
    m.cyl(0.07, 0.6 * h, pos=(0, 0.3 * h, 0), color=WOOD_DK, segs=8, radius2=0.05, bevel=0.0)
    m.cyl(0.11, 0.05, pos=(0, 0.02, 0), color=WOOD_DK, segs=8, radius2=0.07, bevel=0.0)
    canopy = Model(name + "_canopy", pivot=(0, 0.55 * h, 0))
    canopy.blob(0.42 * h, pos=(0, 0.95 * h, 0), color=leaf, subdiv=3, amp=0.14, freq=2.0, seed=seed)
    for k in range(4):
        a = rnd.uniform(0, math.tau)
        canopy.blob(0.24 * h, pos=(math.cos(a) * 0.3 * h, (0.8 + rnd.uniform(0, 0.35)) * h, math.sin(a) * 0.3 * h),
                    color=tint(leaf, rnd.uniform(0.9, 1.12)), subdiv=2, amp=0.15, freq=2.4, seed=seed * 10 + k)
    return [(name, [m, canopy])]


def sailboat(name="sailboat", hull_col=WHITE, stripe=RED, sail_col=CREAM):
    """Small sailboat ~1.0 long (bow toward +Z). Parts: Hull, Mast, Sail."""
    hull = Model("Hull")
    # hull from a lathe-ish loft: stack of rounded cross-sections along z
    verts, faces = [], []
    secs = 10
    ring = 10
    L = 1.0
    for i in range(secs + 1):
        t = i / secs
        z = -L / 2 + t * L
        # width profile: full at stern, pointed at bow
        wprof = math.sin(min(1.0, (1 - t) * 1.35 + 0.15) * math.pi / 2) * (0.22 if t < 0.92 else 0.22 * (1 - t) / 0.08 + 0.01)
        depth = 0.16 * (0.75 + 0.25 * math.sin(t * math.pi))
        for j in range(ring + 1):
            a = math.pi * j / ring  # 0..pi : half ellipse from right gunwale to left gunwale
            x = math.cos(a) * wprof
            y = 0.12 - math.sin(a) * depth
            verts.append((x, y + 0.04 * t * t, z))
    rowlen = ring + 1
    for i in range(secs):
        for j in range(ring):
            a = i * rowlen + j
            faces.append((a, a + 1, a + rowlen + 1, a + rowlen))
    # deck cap
    deck = [i * rowlen for i in range(secs + 1)] + [i * rowlen + ring for i in range(secs, -1, -1)]
    faces.append(tuple(deck))
    # stern transom
    faces.append(tuple(range(0, rowlen)))
    hull.raw(verts, faces, color=hull_col, smooth=True)
    hull.box((0.36, 0.035, 0.6), pos=(0, 0.125, -0.12), color=WOOD, bevel=0.01)
    hull.box((0.42, 0.03, 0.04), pos=(0, 0.07, -0.43), color=stripe, bevel=0.01)
    mast = Model("Mast")
    mast.cyl(0.018, 1.0, pos=(0, 0.6, 0.08), color=WOOD_DK, segs=8, bevel=0.0)
    mast.cyl(0.012, 0.5, pos=(0, 0.3, -0.16), rot=(90, 0, 0), color=WOOD_DK, segs=6, bevel=0.0)
    sail = Model("Sail", pivot=(0, 0.16, 0.08))
    pts = [(0.0, 0.0), (0.0, 0.92), (-0.48, 0.0)]
    sail.prism(pts, 0.012, pos=(0, 0.16, 0.06), rot=(0, 90, 0), color=sail_col, bevel=0.004)
    sail.prism([(0.0, 0.0), (0.0, 0.7), (0.28, 0.0)], 0.012, pos=(0, 0.16, 0.1), rot=(0, 90, 0), color=tint(sail_col, 0.94), bevel=0.004)
    return [(name, [hull, mast, sail])]


def peg_person(name, body=CORAL, skin="F4C9A0", hair="5A3A2A", hat=None, scale=1.0, dress=False):
    """Wooden peg doll ~0.5 tall facing +Z. Parts: Body, Head."""
    b = Model("Body")
    b.push(scale=scale)
    if dress:
        b.lathe([(0.13, 0.0), (0.135, 0.03), (0.1, 0.22), (0.075, 0.3), (0.06, 0.32)], color=body, segs=18)
    else:
        b.lathe([(0.0, 0.0), (0.09, 0.005), (0.1, 0.03), (0.095, 0.22), (0.08, 0.3), (0.05, 0.33)], color=body, segs=18)
    b.cyl(0.04, 0.05, pos=(0, 0.34, 0), color=skin, segs=10, bevel=0.0)
    b.pop()
    h = Model("Head", pivot=(0, 0.36 * scale, 0))
    h.push(pos=(0, 0.43 * scale, 0), scale=scale)
    h.sphere(0.1, color=skin, subdiv=3)
    h.sphere(0.104, pos=(0, 0.02, -0.012), scale=(1.0, 0.86, 1.0), color=hair, subdiv=3)  # hair cap (back/top)
    h.sphere(0.012, pos=(-0.035, 0.0, 0.094), color="2A2633", subdiv=1)
    h.sphere(0.012, pos=(0.035, 0.0, 0.094), color="2A2633", subdiv=1)
    h.sphere(0.016, pos=(-0.06, -0.025, 0.08), scale=(1, 0.6, 0.4), color=BLUSH, subdiv=1)
    h.sphere(0.016, pos=(0.06, -0.025, 0.08), scale=(1, 0.6, 0.4), color=BLUSH, subdiv=1)
    if hat == "straw":
        h.cyl(0.16, 0.015, pos=(0, 0.07, 0), color="EBC779", segs=20, bevel=0.005)
        h.cyl(0.085, 0.07, pos=(0, 0.1, 0), color="EBC779", segs=16, radius2=0.075, bevel=0.01)
        h.cyl(0.087, 0.018, pos=(0, 0.085, 0), color=RED, segs=16, bevel=0.0)
    h.pop()
    return [(name, [b, h])]


# ---------------------------------------------------------------- scatter (set dressing)
def grass_tufts():
    out = []
    for k, (n, hmax, cols) in enumerate(((7, 0.17, (LEAF, LEAF_LT, "6CC24A")), (5, 0.13, (LEAF_DK, LEAF, "7FCB55")),
                                         (9, 0.2, ("6FBF4E", LEAF_LT, LEAF)))):
        rnd = random.Random(40 + k)
        m = Model(f"grass_tuft_{'abc'[k]}")
        for i in range(n):
            a = rnd.uniform(0, math.tau)
            r = rnd.uniform(0.0, 0.06)
            h = rnd.uniform(hmax * 0.55, hmax)
            tilt = rnd.uniform(8, 28)
            m.cone(0.022, h, pos=(math.cos(a) * r, h / 2, math.sin(a) * r), rot=(tilt * math.sin(a), 0, -tilt * math.cos(a)),
                   color=rnd.choice(cols), segs=4, smooth=False)
        out.append((m.name, m))
    return out


def daisies():
    out = []
    for k, (petal, centre) in enumerate((("FBFAF7", BUTTER), (BUTTER, "F29E38"), (LILAC, "FFF0B0"), (PINK, BUTTER))):
        rnd = random.Random(60 + k)
        m = Model(f"wildflower_{'abcd'[k]}")
        for i in range(rnd.randint(3, 5)):
            x, z = rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08)
            h = rnd.uniform(0.06, 0.12)
            m.cyl(0.006, h, pos=(x, h / 2, z), color=LEAF_DK, segs=4, bevel=0)
            for p in range(5):
                a = p / 5 * math.tau
                m.sphere(0.016, pos=(x + math.cos(a) * 0.016, h, z + math.sin(a) * 0.016), scale=(1, 0.4, 0.7),
                         rot=(0, -math.degrees(a), 0), color=petal, subdiv=1)
            m.sphere(0.011, pos=(x, h + 0.004, z), color=centre, subdiv=1)
        m.sphere(0.03, pos=(0.0, 0.01, 0.02), scale=(1.4, 0.4, 1.0), color=LEAF, subdiv=1)
        out.append((m.name, m))
    return out


def pebbles():
    out = []
    for k in range(3):
        rnd = random.Random(80 + k)
        m = Model(f"pebbles_{'abc'[k]}")
        for i in range(rnd.randint(2, 4)):
            c = rnd.choice(("B8B2A8", "A39E96", "CFC7B8", "9A948C"))
            m.blob(rnd.uniform(0.025, 0.05), pos=(rnd.uniform(-0.07, 0.07), 0.012, rnd.uniform(-0.07, 0.07)),
                   scale=(1.3, 0.6, 1.0), color=c, subdiv=1, amp=0.2, freq=3, seed=k * 10 + i, smooth=False)
        out.append((m.name, m))
    return out


def rocks():
    out = []
    for k, (r, sc) in enumerate(((0.22, (1.3, 0.75, 1.0)), (0.16, (1.0, 0.9, 1.2)), (0.3, (1.5, 0.6, 1.1)))):
        m = Model(f"rock_{'abc'[k]}")
        m.blob(r, pos=(0, r * sc[1] * 0.55, 0), scale=sc, color=rnd_col(k, ("A9A39A", "B5AEA2", "9C978F")), subdiv=2,
               amp=0.22, freq=2.2, seed=90 + k, smooth=False)
        if k != 1:
            m.blob(r * 0.45, pos=(r * 0.9, r * 0.25, r * 0.3), scale=(1.2, 0.7, 1.0), color="B9B3A8", subdiv=1, amp=0.2,
                   freq=3, seed=95 + k, smooth=False)
        out.append((m.name, m))
    return out


def rnd_col(k, cols):
    return cols[k % len(cols)]


def mushrooms():
    m = Model("mushrooms")
    for (x, z, h, r) in ((0, 0, 0.07, 0.045), (0.06, 0.03, 0.05, 0.032), (-0.04, 0.05, 0.04, 0.026)):
        m.cyl(0.012, h, pos=(x, h / 2, z), color="F6EEDC", segs=6, bevel=0)
        m.sphere(r, pos=(x, h, z), scale=(1, 0.65, 1), color=RED, subdiv=2)
        m.sphere(r * 0.22, pos=(x + r * 0.4, h + r * 0.45, z), color=WHITE, subdiv=1)
        m.sphere(r * 0.18, pos=(x - r * 0.35, h + r * 0.5, z + r * 0.2), color=WHITE, subdiv=1)
    return [(m.name, m)]


def clover():
    m = Model("clover")
    rnd = random.Random(7)
    for i in range(5):
        x, z = rnd.uniform(-0.07, 0.07), rnd.uniform(-0.07, 0.07)
        for p in range(3):
            a = p / 3 * math.tau + i
            m.sphere(0.022, pos=(x + math.cos(a) * 0.018, 0.03, z + math.sin(a) * 0.018), scale=(1, 0.3, 1),
                     color=rnd.choice((LEAF, LEAF_LT)), subdiv=1)
    return [(m.name, m)]


def bushes():
    out = []
    for k, leaf in enumerate((LEAF, "4FA84A", "6DBE52")):
        rnd = random.Random(100 + k)
        m = Model(f"bush_{'abc'[k]}_leaves", pivot=(0, 0, 0))
        m.blob(0.3, pos=(0, 0.24, 0), scale=(1.2, 0.85, 1.0), color=leaf, subdiv=3, amp=0.15, freq=2.4, seed=110 + k)
        for i in range(3):
            a = rnd.uniform(0, math.tau)
            m.blob(0.18, pos=(math.cos(a) * 0.25, 0.18 + rnd.uniform(0, 0.1), math.sin(a) * 0.2), color=tint(leaf, rnd.uniform(0.9, 1.1)),
                   subdiv=2, amp=0.15, freq=2.6, seed=120 + k * 5 + i)
        if k == 2:
            for i in range(6):
                a = rnd.uniform(0, math.tau)
                m.sphere(0.03, pos=(math.cos(a) * 0.3, 0.25 + rnd.uniform(-0.05, 0.12), math.sin(a) * 0.26), color=PINK, subdiv=1)
        out.append((f"bush_{'abc'[k]}", m))
    return out


def all_core():
    out = []
    out += cloud_puffs()
    out += sprout()
    out += flowers()
    out += flower_bed()
    out += sheep()
    out += tree_round()
    out += sailboat()
    out += peg_person("person_rosa", body=CORAL, hair="8A3B2E", dress=True)
    out += peg_person("person_tom", body=SKY, hair="4A3426", hat="straw")
    out += grass_tufts() + daisies() + pebbles() + rocks() + mushrooms() + clover() + bushes()
    return out
