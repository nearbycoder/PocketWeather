"""Set dressing and interactive props for the twelve dioramas (buildings, farm, beach, park,
campsite, wedding). Parts that animate are separate named objects with sensible pivots.
Vertex alpha < 1 marks emissive parts (windows, lamps) that glow at dusk.
"""
import math
import random

from pw_lib import Model, tint, mix, hexcol
from props_core import (INK, WOOD, WOOD_DK, SOIL, SOIL_DK, LEAF, LEAF_DK, LEAF_LT, CREAM, RED, CORAL, BUTTER, LILAC,
                        PINK, SKY, WHITE, tree_round)

WALL = "F6EBDD"
WALL_B = "FFFFFF"
ROOF_RED = "D9644A"
ROOF_SLATE = "5C7A99"
ROOF_YEL = "E8B04A"
STONE = "BDB6AA"
STONE_DK = "9E978C"
GLASS = (1.0, 0.86, 0.55, 0.0)   # emissive window (alpha 0 = full glow)


def window(m, pos, w=0.22, h=0.28, rot=None, frame=WHITE):
    m.box((w + 0.06, h + 0.06, 0.04), pos=pos, rot=rot, color=frame, bevel=0.01)
    m.push(pos=pos, rot=rot)
    m.box((w, h, 0.05), pos=(0, 0, -0.005), color=GLASS, bevel=0.0)
    m.box((0.02, h, 0.06), pos=(0, 0, -0.01), color=frame, bevel=0.0)
    m.box((w, 0.02, 0.06), pos=(0, 0, -0.01), color=frame, bevel=0.0)
    m.pop()


def gable_roof(m, w, d, h, y, color, overhang=0.12):
    pts = [(-w / 2 - overhang, 0), (w / 2 + overhang, 0), (0, h)]
    m.prism(pts, d + overhang * 2, pos=(0, y, 0), color=color, bevel=0.03)
    # ridge cap
    m.box((0.08, 0.06, d + overhang * 2 + 0.02), pos=(0, y + h - 0.01, 0), color=tint(color, 0.8), bevel=0.02)


def cottage(name, roof=ROOF_RED, wall=WALL, door="7A5A9E", w=1.6, d=1.2, h=0.95):
    m = Model(name)
    m.box((w, 0.12, d), pos=(0, 0.06, 0), color=STONE_DK, bevel=0.03)
    m.box((w - 0.05, h, d - 0.05), pos=(0, 0.12 + h / 2, 0), color=wall, bevel=0.04)
    gable_roof(m, w, d, 0.72, 0.12 + h, roof)
    # door (front = -Z faces the camera)
    m.box((0.32, 0.56, 0.05), pos=(-0.32, 0.12 + 0.28, -d / 2 + 0.005), color=door, bevel=0.03)
    m.sphere(0.025, pos=(-0.22, 0.4, -d / 2 - 0.03), color=BUTTER, subdiv=1)
    window(m, (0.32, 0.12 + h * 0.6, -d / 2 + 0.0))
    window(m, (w / 2 + 0.0, 0.12 + h * 0.6, 0.0), rot=(0, 90, 0))
    window(m, (-w / 2 + 0.0, 0.12 + h * 0.6, 0.0), rot=(0, -90, 0))
    # flower box
    m.box((0.36, 0.08, 0.1), pos=(0.32, 0.12 + h * 0.6 - 0.2, -d / 2 - 0.05), color=WOOD, bevel=0.01)
    for k in range(4):
        m.sphere(0.04, pos=(0.2 + k * 0.08, 0.12 + h * 0.6 - 0.14, -d / 2 - 0.06), color=(CORAL, BUTTER, PINK, LILAC)[k], subdiv=1)
    # chimney
    m.box((0.18, 0.5, 0.18), pos=(w * 0.28, 0.12 + h + 0.5, d * 0.15), color="B8735A", bevel=0.02)
    m.box((0.22, 0.06, 0.22), pos=(w * 0.28, 0.12 + h + 0.76, d * 0.15), color="9E5E48", bevel=0.01)
    # step
    m.box((0.4, 0.06, 0.16), pos=(-0.32, 0.03, -d / 2 - 0.08), color=STONE, bevel=0.02)
    return [(name, m)]


def barn():
    m = Model("barn")
    w, d, h = 2.0, 1.4, 1.0
    red = "C9453A"
    m.box((w, h, d), pos=(0, h / 2, 0), color=red, bevel=0.04)
    pts = [(-w / 2 - 0.1, 0), (-w / 2 + 0.25, 0.42), (0, 0.72), (w / 2 - 0.25, 0.42), (w / 2 + 0.1, 0)]
    m.prism(pts, d + 0.2, pos=(0, h, 0), color="7E3A35", bevel=0.03)
    m.box((0.8, 0.8, 0.05), pos=(0, 0.4, -d / 2 - 0.005), color="A83A30", bevel=0.02)
    for s in (-1, 1):
        m.box((0.05, 0.84, 0.06), pos=(s * 0.4, 0.42, -d / 2 - 0.02), color=WHITE, bevel=0.01)
    m.box((0.84, 0.05, 0.06), pos=(0, 0.82, -d / 2 - 0.02), color=WHITE, bevel=0.01)
    m.box((1.1, 0.05, 0.06), pos=(0, 0.4, -d / 2 - 0.03), rot=(0, 0, 44), color=WHITE, bevel=0.01)
    m.box((1.1, 0.05, 0.06), pos=(0, 0.4, -d / 2 - 0.03), rot=(0, 0, -44), color=WHITE, bevel=0.01)
    window(m, (0, 1.25, -d / 2 - 0.0), w=0.26, h=0.22)
    return [("barn", m)]


def windmill():
    body = Model("Tower")
    body.cyl(0.62, 1.7, pos=(0, 0.85, 0), radius2=0.42, color="F2E8D8", segs=20, bevel=0.03)
    body.cyl(0.66, 0.12, pos=(0, 0.06, 0), color=STONE_DK, segs=20, bevel=0.03)
    body.cone(0.52, 0.55, pos=(0, 1.95, 0), color="B8503E", segs=20)
    body.box((0.3, 0.5, 0.06), pos=(0, 0.3, -0.62), rot=(-5, 0, 0), color="7A5A3A", bevel=0.02)
    window(body, (0, 1.15, -0.5), w=0.2, h=0.24, rot=(-6, 0, 0))
    body.cyl(0.08, 0.3, pos=(0, 1.75, -0.5), rot=(90, 0, 0), color=WOOD_DK, segs=10, bevel=0.01)
    sails = Model("Sails", pivot=(0, 1.75, -0.68))
    for k in range(4):
        sails.push(pos=(0, 1.75, -0.68), rot=(0, 0, k * 90 + 12))
        sails.box((0.07, 1.25, 0.05), pos=(0, 0.66, 0), color=WOOD_DK, bevel=0.01)
        sails.box((0.3, 0.95, 0.025), pos=(0.17, 0.75, 0.02), color=CREAM, bevel=0.01)
        for j in range(4):
            sails.box((0.32, 0.02, 0.035), pos=(0.17, 0.35 + j * 0.27, 0.0), color=WOOD, bevel=0.0)
        sails.pop()
    sails.sphere(0.1, pos=(0, 1.75, -0.7), color=WOOD_DK, subdiv=2)
    return [("windmill", [body, sails])]


def fence(name="fence", length=1.0, color="E8D9C0"):
    m = Model(name)
    for x in (-length / 2 + 0.04, length / 2 - 0.04):
        m.box((0.07, 0.42, 0.07), pos=(x, 0.21, 0), color=color, bevel=0.015)
        m.cone(0.05, 0.06, pos=(x, 0.45, 0), color=color, segs=4, rot=(0, 45, 0))
    for y in (0.15, 0.32):
        m.box((length, 0.05, 0.04), pos=(0, y, 0), color=tint(color, 0.95), bevel=0.01)
    return [(name, m)]


def picket(name="picket", length=1.0):
    m = Model(name)
    for i in range(6):
        x = -length / 2 + (i + 0.5) * length / 6
        m.box((0.08, 0.36, 0.03), pos=(x, 0.18, 0), color=WHITE, bevel=0.01)
        m.cone(0.057, 0.05, pos=(x, 0.38, 0), color=WHITE, segs=4, rot=(0, 45, 0))
    m.box((length, 0.04, 0.03), pos=(0, 0.12, 0.025), color="EDEDED", bevel=0.005)
    m.box((length, 0.04, 0.03), pos=(0, 0.27, 0.025), color="EDEDED", bevel=0.005)
    return [(name, m)]


def bench():
    m = Model("bench")
    for x in (-0.3, 0.3):
        m.box((0.05, 0.22, 0.24), pos=(x, 0.11, 0), color="5E5A66", bevel=0.01)
        m.box((0.05, 0.3, 0.05), pos=(x, 0.36, 0.11), color="5E5A66", bevel=0.01)
    for z in (-0.07, 0.02, 0.1):
        m.box((0.76, 0.03, 0.07), pos=(0, 0.23, z - 0.02), color=WOOD, bevel=0.01)
    for y in (0.33, 0.43):
        m.box((0.76, 0.06, 0.03), pos=(0, y, 0.13), color=WOOD, bevel=0.01)
    return [("bench", m)]


def well():
    m = Model("well")
    m.cyl(0.32, 0.36, pos=(0, 0.18, 0), color=STONE, segs=16, bevel=0.03)
    m.cyl(0.25, 0.02, pos=(0, 0.35, 0), color="3F7FA8", segs=16, bevel=0)
    for x in (-0.28, 0.28):
        m.box((0.06, 0.6, 0.06), pos=(x, 0.6, 0), color=WOOD_DK, bevel=0.01)
    m.prism([(-0.42, 0), (0.42, 0), (0, 0.3)], 0.5, pos=(0, 0.88, 0), color=ROOF_RED, bevel=0.02)
    m.cyl(0.04, 0.56, pos=(0, 0.72, 0), rot=(0, 0, 90), color=WOOD, segs=8, bevel=0)
    m.box((0.12, 0.12, 0.12), pos=(0.0, 0.55, 0), color=WOOD, bevel=0.02)
    return [("well", m)]


def lamp_post():
    m = Model("lamp_post")
    m.cyl(0.05, 1.1, pos=(0, 0.55, 0), color="4A4858", segs=8, bevel=0.01)
    m.cyl(0.1, 0.06, pos=(0, 0.03, 0), color="4A4858", segs=10, bevel=0.01)
    m.box((0.16, 0.18, 0.16), pos=(0, 1.18, 0), color=(1.0, 0.9, 0.6, 0.0), bevel=0.02)
    m.cone(0.14, 0.1, pos=(0, 1.32, 0), color="4A4858", segs=4, rot=(0, 45, 0))
    return [("lamp_post", m)]


def signpost():
    m = Model("signpost")
    m.box((0.06, 0.8, 0.06), pos=(0, 0.4, 0), color=WOOD_DK, bevel=0.01)
    m.prism([(-0.25, 0), (0.18, 0), (0.25, 0.07), (0.18, 0.14), (-0.25, 0.14)], 0.04, pos=(0.05, 0.6, -0.03), color=WOOD, bevel=0.01)
    m.prism([(-0.18, 0), (0.25, 0), (0.25, 0.14), (-0.18, 0.14), (-0.25, 0.07)], 0.04, pos=(-0.05, 0.42, -0.03), rot=(0, 15, 0), color=tint(WOOD, 0.9), bevel=0.01)
    return [("signpost", m)]


def dock(length=2.4):
    m = Model("dock")
    n = int(length / 0.16)
    for i in range(n):
        z = -length / 2 + (i + 0.5) * length / n
        m.box((0.7, 0.04, length / n - 0.02), pos=(0, 0.12, z), color=WOOD if i % 2 else tint(WOOD, 0.93), bevel=0.008)
    for z in (-length / 2 + 0.1, 0, length / 2 - 0.1):
        for x in (-0.32, 0.32):
            m.cyl(0.045, 0.55, pos=(x, -0.12, z), color=WOOD_DK, segs=8, bevel=0.01)
    m.cyl(0.05, 0.3, pos=(0.32, 0.27, -length / 2 + 0.1), color=WOOD_DK, segs=8, bevel=0.01)
    m.torus(0.06, 0.015, pos=(0.32, 0.36, -length / 2 + 0.1), color="E8D9B0", segs=12, rsegs=6)
    return [("dock", m)]


def market_stall(name="market_stall", stripe=CORAL):
    m = Model(name)
    m.box((1.1, 0.06, 0.5), pos=(0, 0.42, 0), color=WOOD, bevel=0.02)
    m.box((1.1, 0.38, 0.04), pos=(0, 0.21, -0.23), color=tint(WOOD, 0.9), bevel=0.01)
    for x in (-0.52, 0.52):
        for z in (-0.22, 0.22):
            m.box((0.05, 1.0 if z > 0 else 0.86, 0.05), pos=(x, 0.5 if z > 0 else 0.43, z), color=WOOD_DK, bevel=0.01)
    for k in range(6):
        c = stripe if k % 2 == 0 else WHITE
        m.box((1.2 / 6, 0.03, 0.66), pos=(-0.5 + k * 0.2, 0.95, 0.02), rot=(-14, 0, 0), color=c, bevel=0.005)
    for k in range(6):
        x = -0.4 + k * 0.16
        m.sphere(0.06, pos=(x, 0.5, -0.05), color=(RED, BUTTER, "8FD16A", "F28C38", RED, BUTTER)[k], subdiv=1)
    return [(name, m)]


def laundry_line():
    poles = Model("Poles")
    for x in (-0.9, 0.9):
        poles.box((0.06, 1.0, 0.06), pos=(x, 0.5, 0), color=WOOD_DK, bevel=0.01)
        poles.box((0.3, 0.05, 0.05), pos=(x, 0.98, 0), color=WOOD_DK, bevel=0.01)
    poles.cyl(0.008, 1.8, pos=(0, 0.95, 0), rot=(0, 0, 90), color="E8E4DA", segs=4, bevel=0)
    parts = [poles]
    clothes = [("shirt", SKY, -0.62), ("sheet", WHITE, -0.12), ("sock", CORAL, 0.28), ("dress", PINK, 0.58)]
    for i, (kind, col, x) in enumerate(clothes):
        c = Model(f"Cloth{i}", pivot=(x, 0.95, 0))
        if kind == "shirt":
            pts = [(-0.14, 0.0), (-0.2, -0.08), (-0.15, -0.14), (-0.11, -0.1), (-0.11, -0.4), (0.11, -0.4), (0.11, -0.1), (0.15, -0.14), (0.2, -0.08), (0.14, 0.0)]
            c.prism([(px + x, py + 0.95) for px, py in pts], 0.02, color=col, bevel=0.005)
        elif kind == "sheet":
            c.box((0.42, 0.5, 0.015), pos=(x, 0.7, 0), color=col, bevel=0.005)
            c.box((0.42, 0.05, 0.02), pos=(x, 0.52, 0), color=LILAC, bevel=0.003)
        elif kind == "sock":
            c.box((0.08, 0.22, 0.03), pos=(x, 0.84, 0), color=col, bevel=0.01)
            c.box((0.14, 0.07, 0.03), pos=(x + 0.03, 0.72, 0), color=col, bevel=0.01)
        else:
            c.prism([(x - 0.07, 0.95), (x + 0.07, 0.95), (x + 0.16, 0.55), (x - 0.16, 0.55)], 0.02, color=col, bevel=0.005)
        c.box((0.025, 0.05, 0.03), pos=(x - 0.05, 0.95, 0), color=WOOD, bevel=0.003)
        c.box((0.025, 0.05, 0.03), pos=(x + 0.05, 0.95, 0), color=WOOD, bevel=0.003)
        parts.append(c)
    return [("laundry_line", parts)]


def tent():
    m = Model("tent")
    m.prism([(-0.6, 0), (0.6, 0), (0, 0.75)], 1.0, color="F28C38", bevel=0.02)
    m.prism([(-0.25, 0), (0.25, 0), (0, 0.42)], 0.02, pos=(0, 0.0, -0.51), color="6E3A1E", bevel=0.0)
    m.box((0.04, 0.85, 0.04), pos=(0, 0.42, -0.52), color=WOOD_DK, bevel=0.01)
    return [("tent", m)]


def campfire():
    m = Model("campfire")
    for k in range(9):
        a = k / 9 * math.tau
        m.blob(0.08, pos=(math.cos(a) * 0.28, 0.04, math.sin(a) * 0.28), color=STONE if k % 2 else STONE_DK, subdiv=1, amp=0.2, seed=k, smooth=False)
    for k in range(3):
        m.cyl(0.04, 0.42, pos=(0, 0.1, 0), rot=(75, k * 60, 0), color=WOOD_DK, segs=7, bevel=0.01)
    m.cyl(0.18, 0.02, pos=(0, 0.01, 0), color="3A2E2A", segs=12, bevel=0)
    return [("campfire", m)]


def haystack():
    m = Model("haystack")
    m.blob(0.5, pos=(0, 0.3, 0), scale=(1.0, 0.85, 1.0), color="E8C25A", subdiv=3, amp=0.12, freq=3, seed=21, flat_bottom=-0.05)
    m.blob(0.3, pos=(0.1, 0.65, 0.05), color="F0CF6A", subdiv=2, amp=0.15, freq=3, seed=22)
    rnd = random.Random(2)
    for _ in range(14):
        a = rnd.uniform(0, math.tau)
        m.cyl(0.006, 0.18, pos=(math.cos(a) * 0.48, 0.12 + rnd.uniform(0, 0.4), math.sin(a) * 0.48), rot=(rnd.uniform(-60, 60), 0, rnd.uniform(-60, 60)), color="D9B04A", segs=3, bevel=0)
    return [("haystack", m)]


def hay_bale():
    m = Model("hay_bale")
    m.cyl(0.24, 0.42, pos=(0, 0.24, 0), rot=(0, 0, 90), color="E8C25A", segs=16, bevel=0.04)
    m.torus(0.245, 0.012, pos=(-0.1, 0.24, 0), rot=(0, 0, 90), color="B8903A", segs=16, rsegs=4)
    m.torus(0.245, 0.012, pos=(0.1, 0.24, 0), rot=(0, 0, 90), color="B8903A", segs=16, rsegs=4)
    return [("hay_bale", m)]


def picnic():
    m = Model("picnic")
    n = 6
    for i in range(n):
        for j in range(n):
            c = "E8574A" if (i + j) % 2 == 0 else "FFF4E8"
            m.box((0.16, 0.012, 0.16), pos=(-0.4 + i * 0.16, 0.006, -0.4 + j * 0.16), color=c, bevel=0.0)
    m.box((0.3, 0.16, 0.2), pos=(0.22, 0.09, 0.15), color="C8935B", bevel=0.03)
    m.torus(0.12, 0.015, pos=(0.22, 0.18, 0.15), rot=(90, 0, 0), color=WOOD_DK, arc=180, segs=12, rsegs=5)
    m.sphere(0.05, pos=(-0.15, 0.04, -0.12), color=RED, subdiv=1)
    m.cyl(0.05, 0.12, pos=(-0.25, 0.06, 0.1), color="F6EEDC", segs=10, bevel=0.01)
    m.cyl(0.08, 0.03, pos=(0.0, 0.02, -0.2), color="FFFFFF", segs=12, bevel=0.01)
    return [("picnic", m)]


def beach_umbrella(name="beach_umbrella", c1=CORAL, c2=WHITE):
    m = Model(name)
    m.cyl(0.025, 1.0, pos=(0, 0.5, 0), color="E8E4DA", segs=8, bevel=0)
    for k in range(8):
        col = c1 if k % 2 == 0 else c2
        a0, a1 = k / 8 * math.tau, (k + 1) / 8 * math.tau
        verts = [(0, 1.08, 0), (math.cos(a0) * 0.62, 0.86, math.sin(a0) * 0.62), (math.cos(a1) * 0.62, 0.86, math.sin(a1) * 0.62)]
        m.raw(verts + [(v[0], v[1] - 0.012, v[2]) for v in verts], [(0, 1, 2), (3, 5, 4), (1, 4, 5, 2), (0, 3, 4, 1), (0, 2, 5, 3)], color=col, smooth=False)
    m.sphere(0.04, pos=(0, 1.1, 0), color=c1, subdiv=1)
    return [(name, m)]


def towel(name="towel", c1=SKY, c2=WHITE):
    m = Model(name)
    for i in range(5):
        m.box((0.5, 0.012, 0.18), pos=(0, 0.006, -0.36 + i * 0.18), color=c1 if i % 2 == 0 else c2, bevel=0.0)
    return [(name, m)]


def sandcastle():
    m = Model("sandcastle")
    sand = "EBCB8A"
    m.box((0.6, 0.22, 0.5), pos=(0, 0.11, 0), color=sand, bevel=0.04)
    for (x, z) in ((-0.27, -0.22), (0.27, -0.22), (-0.27, 0.22), (0.27, 0.22)):
        m.cyl(0.1, 0.36, pos=(x, 0.18, z), color=tint(sand, 1.04), segs=12, bevel=0.03)
        for k in range(5):
            a = k / 5 * math.tau
            m.box((0.04, 0.05, 0.04), pos=(x + math.cos(a) * 0.08, 0.38, z + math.sin(a) * 0.08), color=sand, bevel=0.01)
    m.cyl(0.13, 0.55, pos=(0, 0.3, 0.02), color=sand, segs=14, bevel=0.03)
    m.cone(0.15, 0.2, pos=(0, 0.67, 0.02), color="D9AE68", segs=14)
    m.box((0.01, 0.2, 0.01), pos=(0, 0.85, 0.02), color=WOOD_DK)
    m.prism([(0, 0), (0.14, 0.04), (0, 0.08)], 0.01, pos=(0.0, 0.86, 0.02), color=CORAL)
    m.box((0.12, 0.14, 0.03), pos=(0, 0.07, -0.26), color="B89058", bevel=0.02)
    m.cyl(0.07, 0.1, pos=(0.45, 0.05, -0.1), radius2=0.09, color=RED, segs=10, bevel=0.01)
    return [("sandcastle", m)]


def icecream_cart():
    m = Model("icecream_cart")
    m.box((0.8, 0.45, 0.42), pos=(0, 0.38, 0), color="FFF4F8", bevel=0.05)
    m.box((0.82, 0.1, 0.44), pos=(0, 0.2, 0), color=PINK, bevel=0.03)
    for x in (-0.28, 0.28):
        m.cyl(0.13, 0.05, pos=(x, 0.13, -0.23), rot=(90, 0, 0), color="4A4858", segs=14, bevel=0.01)
    m.cyl(0.02, 0.6, pos=(0.3, 0.85, 0.1), color="E8E4DA", segs=6, bevel=0)
    for k in range(6):
        col = PINK if k % 2 == 0 else WHITE
        a0, a1 = k / 6 * math.tau, (k + 1) / 6 * math.tau
        verts = [(0.3, 1.22, 0.1), (0.3 + math.cos(a0) * 0.45, 1.06, 0.1 + math.sin(a0) * 0.45), (0.3 + math.cos(a1) * 0.45, 1.06, 0.1 + math.sin(a1) * 0.45)]
        m.raw(verts + [(v[0], v[1] - 0.012, v[2]) for v in verts], [(0, 1, 2), (3, 5, 4), (1, 4, 5, 2), (0, 3, 4, 1), (0, 2, 5, 3)], color=col)
    for k, c in enumerate(("FF9EC4", "FFF4DC", "8FD16A")):
        m.sphere(0.07, pos=(-0.2 + k * 0.14, 0.66, -0.05), color=c, subdiv=2)
    return [("icecream_cart", m)]


def wedding_arch():
    m = Model("wedding_arch")
    m.torus(0.62, 0.05, pos=(0, 0.95, 0), rot=(-90, 0, 0), color=WHITE, arc=180, segs=24, rsegs=8)
    for x in (-0.62, 0.62):
        m.cyl(0.05, 0.95, pos=(x, 0.475, 0), color=WHITE, segs=10, bevel=0.01)
        m.box((0.18, 0.06, 0.18), pos=(x, 0.03, 0), color="E8E4DA", bevel=0.02)
    for k in range(13):
        a = math.pi * k / 12
        m.sphere(0.07, pos=(math.cos(a) * 0.62, 0.95 + math.sin(a) * 0.62, -0.03), color=LEAF if k % 2 else LEAF_DK, subdiv=1)
    return [("wedding_arch", m)]


def cake_table():
    m = Model("cake_table")
    m.box((0.8, 0.04, 0.5), pos=(0, 0.42, 0), color=WOOD, bevel=0.01)
    m.box((0.84, 0.36, 0.54), pos=(0, 0.25, 0), color="FFFFFF", bevel=0.03)
    cake = [(0.18, 0.12, 0.44), (0.13, 0.11, 0.56), (0.08, 0.1, 0.67)]
    for (r, h, y) in cake:
        m.cyl(r, h, pos=(0, y + h / 2, 0), color="FFF6EC", segs=20, bevel=0.02)
        m.torus(r, 0.012, pos=(0, y + h * 0.15, 0), color="FF9EC4", segs=20, rsegs=5)
    m.sphere(0.035, pos=(0, 0.8, 0), color=CORAL, subdiv=1)
    return [("cake_table", m)]


def chair(name="chair", col=WHITE):
    m = Model(name)
    for x in (-0.12, 0.12):
        for z in (-0.12, 0.12):
            m.box((0.03, 0.24, 0.03), pos=(x, 0.12, z), color=col, bevel=0.005)
    m.box((0.3, 0.03, 0.3), pos=(0, 0.25, 0), color=col, bevel=0.01)
    m.box((0.3, 0.26, 0.03), pos=(0, 0.39, 0.135), color=col, bevel=0.01)
    return [(name, m)]


def chapel():
    m = Model("chapel")
    w, d, h = 1.4, 1.9, 1.05
    m.box((w, h, d), pos=(0, h / 2, 0.2), color=WALL_B, bevel=0.04)
    gable_roof(m, w, d, 0.75, h, ROOF_SLATE)
    m.box((0.5, 1.5, 0.5), pos=(0, 0.75 + 0.3, -0.62), color=WALL_B, bevel=0.03)
    m.cone(0.4, 0.75, pos=(0, 2.15, -0.62), color=ROOF_SLATE, segs=4, rot=(0, 45, 0))
    m.box((0.03, 0.3, 0.03), pos=(0, 2.65, -0.62), color=BUTTER)
    m.box((0.16, 0.03, 0.03), pos=(0, 2.7, -0.62), color=BUTTER)
    m.box((0.26, 0.44, 0.05), pos=(0, 0.22, -0.88), color="8A5A3C", bevel=0.03)
    window(m, (0, 1.4, -0.88), w=0.18, h=0.24)
    for z in (-0.1, 0.5):
        window(m, (w / 2, 0.6, z), w=0.16, h=0.3, rot=(0, 90, 0))
        window(m, (-w / 2, 0.6, z), w=0.16, h=0.3, rot=(0, -90, 0))
    return [("chapel", m)]


def rowboat():
    m = Model("rowboat")
    m.prism([(-0.4, 0.0), (0.4, 0.0), (0.3, -0.16), (-0.3, -0.16)], 0.36, pos=(0, 0.12, 0), rot=(0, 90, 0), color="5FA8D3", bevel=0.03)
    m.box((0.3, 0.03, 0.08), pos=(0, 0.1, 0), color=WOOD, bevel=0.01)
    m.cyl(0.015, 0.7, pos=(0.0, 0.14, 0.0), rot=(0, 0, 80), color=WOOD_DK, segs=6, bevel=0)
    return [("rowboat", m)]


def buoy():
    m = Model("buoy")
    m.cyl(0.16, 0.2, pos=(0, 0.0, 0), color=RED, segs=14, bevel=0.04)
    m.cyl(0.165, 0.06, pos=(0, 0.07, 0), color=WHITE, segs=14, bevel=0.01)
    for k in range(3):
        m.cyl(0.015, 0.45, pos=(math.cos(k * 2.1) * 0.08, 0.3, math.sin(k * 2.1) * 0.08), rot=(math.sin(k * 2.1) * 8, 0, -math.cos(k * 2.1) * 8), color="4A4858", segs=5, bevel=0)
    bell = Model("Bell", pivot=(0, 0.5, 0))
    bell.lathe([(0.0, 0.52), (0.04, 0.51), (0.07, 0.46), (0.08, 0.38), (0.1, 0.33), (0.0, 0.33)], color="F2B73F", segs=14)
    return [("buoy", [m, bell])]


def birdbath():
    m = Model("birdbath")
    m.cyl(0.07, 0.42, pos=(0, 0.21, 0), color="C8C2B8", segs=10, bevel=0.02)
    m.cyl(0.14, 0.05, pos=(0, 0.025, 0), color="B8B2A8", segs=12, bevel=0.01)
    m.cyl(0.28, 0.08, pos=(0, 0.46, 0), radius2=0.32, color="D8D2C8", segs=20, bevel=0.03)
    return [("birdbath", m)]


def fountain():
    m = Model("fountain")
    m.cyl(0.75, 0.22, pos=(0, 0.11, 0), color=STONE, segs=24, bevel=0.04)
    m.cyl(0.66, 0.02, pos=(0, 0.2, 0), color="6FB8E0", segs=24, bevel=0)
    m.cyl(0.1, 0.55, pos=(0, 0.45, 0), color=STONE_DK, segs=12, bevel=0.02)
    m.cyl(0.32, 0.08, pos=(0, 0.72, 0), radius2=0.36, color=STONE, segs=18, bevel=0.03)
    m.cyl(0.29, 0.02, pos=(0, 0.765, 0), color="6FB8E0", segs=18, bevel=0)
    m.sphere(0.07, pos=(0, 0.83, 0), color=STONE_DK, subdiv=2)
    return [("fountain", m)]


def kite():
    k = Model("Kite", pivot=(0, 0, 0))
    k.prism([(0, 0.3), (0.2, 0.05), (0, -0.25), (-0.2, 0.05)], 0.02, color=CORAL, bevel=0.005)
    k.prism([(0, 0.3), (0.2, 0.05), (0, 0.05)], 0.025, pos=(0, 0, -0.005), color=BUTTER, bevel=0.003)
    k.prism([(0, -0.25), (-0.2, 0.05), (0, 0.05)], 0.025, pos=(0, 0, -0.005), color=SKY, bevel=0.003)
    for i in range(4):
        k.box((0.06, 0.035, 0.01), pos=(0.02 * i, -0.3 - i * 0.07, 0), rot=(0, 0, 25), color=LILAC if i % 2 else PINK, bevel=0.003)
    return [("kite", k)]


def scarecrow():
    m = Model("scarecrow")
    m.box((0.05, 0.9, 0.05), pos=(0, 0.45, 0), color=WOOD_DK, bevel=0.01)
    m.box((0.7, 0.05, 0.05), pos=(0, 0.68, 0), color=WOOD_DK, bevel=0.01)
    m.box((0.3, 0.32, 0.16), pos=(0, 0.6, 0), color="5F8FC9", bevel=0.04)
    m.sphere(0.11, pos=(0, 0.88, 0), color="F2DDAE", subdiv=2)
    m.cyl(0.18, 0.02, pos=(0, 0.96, 0), color="D9A84A", segs=14, bevel=0.005)
    m.cyl(0.09, 0.1, pos=(0, 1.01, 0), color="D9A84A", segs=12, bevel=0.01)
    for x in (-0.36, 0.36):
        m.sphere(0.045, pos=(x, 0.68, 0), color="E8C25A", subdiv=1)
    return [("scarecrow", m)]


def rain_barrel():
    m = Model("rain_barrel")
    m.lathe([(0.2, 0.0), (0.24, 0.12), (0.25, 0.25), (0.24, 0.38), (0.2, 0.5)], color=WOOD, segs=16)
    for y in (0.08, 0.42):
        m.torus(0.235, 0.015, pos=(0, y, 0), color="6E6A74", segs=16, rsegs=4)
    m.cyl(0.19, 0.02, pos=(0, 0.47, 0), color="3F7FA8", segs=16, bevel=0)
    return [("rain_barrel", m)]


def mailbox():
    m = Model("mailbox")
    m.box((0.05, 0.5, 0.05), pos=(0, 0.25, 0), color=WOOD_DK, bevel=0.01)
    m.capsule(0.09, 0.3, pos=(0, 0.55, 0), rot=(90, 0, 0), color=SKY)
    m.box((0.02, 0.12, 0.05), pos=(0.1, 0.62, 0.05), color=RED, bevel=0.005)
    return [("mailbox", m)]


def pine(name="tree_pine", h=1.4):
    m = Model(name)
    m.cyl(0.07, 0.4 * h, pos=(0, 0.2 * h, 0), color=WOOD_DK, segs=8, bevel=0)
    c = Model(name + "_canopy", pivot=(0, 0.3 * h, 0))
    for i, (r, y) in enumerate(((0.42, 0.42), (0.34, 0.72), (0.24, 1.0))):
        c.cone(r * h * 0.9, 0.48 * h, pos=(0, y * h, 0), color=tint("3E9A5A", 1 + i * 0.07), segs=10)
    return [(name, [m, c])]


def fruit_tree():
    out = tree_round("tree_fruit", seed=5, leaf="58B04A", h=1.0)
    name, parts = out[0]
    canopy = parts[1]
    rnd = random.Random(3)
    for _ in range(9):
        a = rnd.uniform(0, math.tau)
        canopy.sphere(0.05, pos=(math.cos(a) * 0.4, 0.8 + rnd.uniform(0, 0.4), math.sin(a) * 0.4), color=RED, subdiv=1)
    return [(name, parts)]


def hedge(name="hedge", length=1.0):
    m = Model(name + "_leaves")
    for i in range(int(length / 0.3) + 1):
        x = -length / 2 + i * 0.3
        m.blob(0.22, pos=(x, 0.2, 0), scale=(1.1, 0.95, 0.85), color=tint(LEAF_DK, 1 + (i % 3) * 0.05), subdiv=2, amp=0.15, freq=2.6, seed=i + 40)
    return [(name, m)]


def reeds():
    m = Model("reeds")
    rnd = random.Random(9)
    for i in range(9):
        x, z = rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15)
        h = rnd.uniform(0.3, 0.55)
        m.cyl(0.01, h, pos=(x, h / 2, z), rot=(rnd.uniform(-10, 10), 0, rnd.uniform(-10, 10)), color=rnd.choice((LEAF_DK, "6BA84A")), segs=4, bevel=0)
        if i % 3 == 0:
            m.capsule(0.025, 0.12, pos=(x, h, z), color="7A4E2D")
    return [("reeds_grass", m)]


def lily_pads():
    m = Model("lily_pads")
    for (x, z, r) in ((0, 0, 0.14), (0.25, 0.12, 0.1), (-0.18, 0.2, 0.09)):
        m.cyl(r, 0.012, pos=(x, 0.006, z), color="5DAA48", segs=14, bevel=0)
    m.sphere(0.05, pos=(0.02, 0.03, 0.02), scale=(1, 0.6, 1), color=PINK, subdiv=1)
    return [("lily_pads", m)]


def window_box():
    m = Model("window_box")
    m.box((0.9, 0.18, 0.3), pos=(0, 0.09, 0), color=WOOD, bevel=0.02)
    soil = Model("window_box_soil")
    soil.box((0.82, 0.04, 0.22), pos=(0, 0.17, 0), color=SOIL, bevel=0.005)
    return [("window_box", [m, soil])]


def crop_bed(name="crop_bed", w=1.5, d=0.95):
    """Low earth mound bed (for vegetables)."""
    m = Model(name)
    m.box((w, 0.08, d), pos=(0, 0.04, 0), color="7E5A3C", bevel=0.04)
    soil = Model(name + "_soil")
    soil.box((w - 0.08, 0.05, d - 0.08), pos=(0, 0.08, 0), color=SOIL, bevel=0.02)
    for i in range(3):
        soil.box((w - 0.16, 0.035, 0.08), pos=(0, 0.11, -d / 3 + i * d / 3), color=SOIL_DK, bevel=0.01)
    return [(name, [m, soil])]


def mud_wallow():
    m = Model("mud_wallow")
    rnd = random.Random(4)
    for k in range(12):
        a = k / 12 * math.tau
        m.blob(0.12, pos=(math.cos(a) * 0.62, 0.03, math.sin(a) * 0.42), scale=(1.4, 0.4, 1.0), color="8A6A48", subdiv=1, amp=0.2, seed=k, smooth=False)
    soil = Model("mud_wallow_soil")
    soil.cyl(0.62, 0.05, pos=(0, 0.04, 0), color="7A5638", segs=20, bevel=0.02)
    soil.push(scale=(1.0, 1.0, 0.7))
    soil.pop()
    return [("mud_wallow", [m, soil])]


def crops():
    out = []
    # carrot plant (sprout uses its tops; bloom = carrot shoulder visible)
    c = Model("crop_carrot_stem")
    for k in range(4):
        c.sphere(0.04, pos=(math.cos(k * 1.6) * 0.03, 0.1 + k * 0.02, math.sin(k * 1.6) * 0.03), scale=(0.35, 1.6, 0.35), rot=(math.sin(k) * 20, 0, math.cos(k) * 20), color=LEAF, subdiv=1)
    h = Model("crop_carrot_head")
    h.cone(0.05, 0.1, pos=(0, 0.01, 0), rot=(0, 0, 180), color="F28C38", segs=10)
    out.append(("crop_carrot", [c, h]))
    c = Model("crop_cabbage_stem")
    c.blob(0.07, pos=(0, 0.06, 0), color="6CC24A", subdiv=2, amp=0.1, seed=3)
    h = Model("crop_cabbage_head")
    for k in range(5):
        a = k / 5 * math.tau
        h.sphere(0.08, pos=(math.cos(a) * 0.06, 0.06, math.sin(a) * 0.06), scale=(1, 0.7, 0.6), rot=(0, -math.degrees(a), 30), color="8FD16A", subdiv=2)
    h.sphere(0.07, pos=(0, 0.08, 0), color="B5E08A", subdiv=2)
    out.append(("crop_cabbage", [c, h]))
    c = Model("crop_tomato_stem")
    c.cyl(0.01, 0.32, pos=(0.04, 0.16, 0), color=WOOD, segs=4, bevel=0)
    c.blob(0.08, pos=(0, 0.16, 0), scale=(0.8, 1.4, 0.8), color=LEAF_DK, subdiv=2, amp=0.2, seed=5)
    h = Model("crop_tomato_head")
    for (x, y, z) in ((0.05, 0.12, -0.05), (-0.05, 0.2, -0.04), (0.02, 0.26, -0.06)):
        h.sphere(0.035, pos=(x, y, z), color=RED, subdiv=2)
    out.append(("crop_tomato", [c, h]))
    c = Model("crop_wheat_stem")
    for k in range(5):
        a = k / 5 * math.tau
        c.cyl(0.006, 0.26, pos=(math.cos(a) * 0.03, 0.13, math.sin(a) * 0.03), color="9CB84A", segs=3, bevel=0)
    h = Model("crop_wheat_head")
    for k in range(5):
        a = k / 5 * math.tau
        h.capsule(0.016, 0.08, pos=(math.cos(a) * 0.03, 0.28, math.sin(a) * 0.03), color="F2C14E")
    out.append(("crop_wheat", [c, h]))
    c = Model("crop_pumpkin_stem")
    for k in range(3):
        c.sphere(0.06, pos=(math.cos(k * 2.1) * 0.08, 0.03, math.sin(k * 2.1) * 0.08), scale=(1, 0.3, 1), color=LEAF, subdiv=1)
    h = Model("crop_pumpkin_head")
    for k in range(8):
        a = k / 8 * math.tau
        h.sphere(0.07, pos=(math.cos(a) * 0.05, 0.06, math.sin(a) * 0.05), scale=(0.75, 0.9, 0.75), color="F28C38" if k % 2 else "E67A2E", subdiv=1)
    h.cyl(0.015, 0.05, pos=(0, 0.13, 0), color="6B8E3A", segs=5, bevel=0)
    out.append(("crop_pumpkin", [c, h]))
    c = Model("crop_sunflower_stem")
    c.cyl(0.015, 0.6, pos=(0, 0.3, 0), color=LEAF_DK, segs=6, bevel=0)
    c.sphere(0.06, pos=(-0.05, 0.25, 0), scale=(1, 0.3, 0.6), rot=(0, 0, 30), color=LEAF, subdiv=1)
    c.sphere(0.06, pos=(0.05, 0.38, 0), scale=(1, 0.3, 0.6), rot=(0, 0, -30), color=LEAF, subdiv=1)
    h = Model("crop_sunflower_head", pivot=(0, 0.62, 0))
    for k in range(12):
        a = k / 12 * math.tau
        h.sphere(0.04, pos=(math.cos(a) * 0.09, 0.62 + math.sin(a) * 0.09, -0.02), scale=(1, 0.45, 0.25), rot=(0, 0, math.degrees(a)), color="FFD23F", subdiv=1)
    h.sphere(0.07, pos=(0, 0.62, -0.03), scale=(1, 1, 0.5), color="7A4E2D", subdiv=2)
    out.append(("crop_sunflower", [c, h]))
    return out


def sapling():
    out = []
    for k, s in enumerate((0.35, 0.65, 1.0)):
        out += tree_round(f"sapling_{k}", seed=12, leaf="6CC24A", h=s)
    return out


def umbrella_small():
    m = Model("umbrella_small", pivot=(0, 0.42, 0))
    m.cyl(0.008, 0.4, pos=(0, 0.6, 0), color="4A4858", segs=4, bevel=0)
    for k in range(6):
        col = BUTTER if k % 2 == 0 else CORAL
        a0, a1 = k / 6 * math.tau, (k + 1) / 6 * math.tau
        verts = [(0, 0.86, 0), (math.cos(a0) * 0.26, 0.76, math.sin(a0) * 0.26), (math.cos(a1) * 0.26, 0.76, math.sin(a1) * 0.26)]
        m.raw(verts + [(v[0], v[1] - 0.01, v[2]) for v in verts], [(0, 1, 2), (3, 5, 4), (1, 4, 5, 2), (0, 3, 4, 1), (0, 2, 5, 3)], color=col)
    return [("umbrella_small", m)]


def bush_flowers():
    m = Model("flower_pot")
    m.cyl(0.12, 0.18, pos=(0, 0.09, 0), radius2=0.15, color="C8673E", segs=12, bevel=0.02)
    m.blob(0.13, pos=(0, 0.24, 0), color=LEAF, subdiv=2, amp=0.15, seed=7)
    for k in range(5):
        a = k / 5 * math.tau
        m.sphere(0.035, pos=(math.cos(a) * 0.1, 0.28, math.sin(a) * 0.1), color=PINK if k % 2 else BUTTER, subdiv=1)
    return [("flower_pot", m)]


def bouquet():
    """The bride's bouquet (pivot at its middle so it tumbles nicely in flight)."""
    m = Model("bouquet", pivot=(0, 0.16, 0))
    m.cone(0.07, 0.2, pos=(0, 0.06, 0), rot=(0, 0, 180), color="7CC45A", segs=10)
    m.torus(0.045, 0.014, pos=(0, 0.12, 0), color="FBFAF7", segs=14, rsegs=6)
    for k in range(5):
        a = k / 5 * math.tau
        m.blob(0.045, pos=(math.cos(a) * 0.09, 0.19, math.sin(a) * 0.09), scale=(1.4, 0.5, 0.8), rot=(0, -math.degrees(a), 25),
               color=LEAF, subdiv=1, amp=0.1, seed=30 + k)
    for k, (c, r, y) in enumerate(((CORAL, 0.0, 0.27), (BUTTER, 0.07, 0.24), (PINK, 0.07, 0.24), ("B79CFF", 0.07, 0.24),
                                   ("FBFAF7", 0.07, 0.24), (PINK, 0.12, 0.2), (CORAL, 0.12, 0.2), (BUTTER, 0.12, 0.2))):
        a = k * 2.4
        m.sphere(0.045 if k else 0.052, pos=(math.cos(a) * r, y, math.sin(a) * r), color=c, subdiv=2)
    return [("bouquet", m)]


def rock_island():
    m = Model("rock_big")
    m.blob(0.45, pos=(0, 0.15, 0), scale=(1.3, 0.6, 1.0), color="9E978C", subdiv=2, amp=0.2, freq=2, seed=77, smooth=False, flat_bottom=-0.2)
    m.blob(0.25, pos=(0.35, 0.2, 0.15), scale=(1.0, 0.8, 1.0), color="B0A99E", subdiv=2, amp=0.2, seed=78, smooth=False)
    return [("rock_big", m)]


def all_props():
    out = []
    out += cottage("cottage_red", ROOF_RED)
    out += cottage("cottage_blue", ROOF_SLATE, WALL_B, door="D9644A")
    out += cottage("cottage_yellow", ROOF_YEL, "FCEFD8", door="5C7A99", w=1.4, d=1.1)
    out += barn() + windmill() + fence() + fence("fence_long", 2.0) + picket() + bench() + well() + lamp_post()
    out += signpost() + dock() + market_stall() + market_stall("market_stall_blue", SKY) + laundry_line()
    out += tent() + campfire() + haystack() + hay_bale() + picnic() + beach_umbrella() + beach_umbrella("beach_umbrella_b", SKY, BUTTER)
    out += towel() + towel("towel_b", PINK, WHITE) + sandcastle() + icecream_cart() + wedding_arch() + cake_table()
    out += chair() + chapel() + rowboat() + buoy() + birdbath() + fountain() + kite() + scarecrow() + rain_barrel()
    out += mailbox() + pine() + pine("tree_pine_small", 1.0) + fruit_tree() + tree_round("tree_round_b", seed=7, leaf="4FA84A", h=1.15)
    out += hedge() + hedge("hedge_long", 2.0) + reeds() + lily_pads() + window_box() + crop_bed() + mud_wallow()
    out += crops() + sapling() + umbrella_small() + bush_flowers() + bouquet() + rock_island()
    return out
