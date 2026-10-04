"""Renders Pocket Weather's UI icons from small 3D models (Eevee, transparent PNGs).

    blender -b -P ArtSource/icons.py -- [--only drop,sun] [--size 256]

Output: Assets/Resources/Icons/<name>.png. The models reuse pw_lib's vertex-colour Model
builder; a shared material reads the colour attribute so icons match the game's palette.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import pw_lib  # noqa: E402
from pw_lib import Model, tint  # noqa: E402
import props_core as pc  # noqa: E402

OUT = os.path.join(pw_lib.ROOT, "Assets", "Resources", "Icons")


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only, size = None, 256
    for i, x in enumerate(a):
        if x == "--only":
            only = set(a[i + 1].split(","))
        if x == "--size":
            size = int(a[i + 1])
    return only, size


# ----------------------------------------------------------------------------- icon models (Unity space, ~unit size, facing -Z = camera)

def drop():
    m = Model("drop")
    prof = [(0.0, -0.45)]
    for i in range(1, 9):
        y = -0.45 + 0.4 * i / 8
        prof.append((0.38 * math.sqrt(max(0.0, 1 - ((y + 0.05) / 0.4) ** 2)), y))
    for i in range(1, 11):
        y = -0.05 + 0.6 * i / 10
        prof.append((0.38 * max(0.0, 1 - (y + 0.05) / 0.6) ** 1.3, y))
    prof[-1] = (0.0, 0.55)
    m.lathe(prof, color="4FB6D8", segs=28)
    m.sphere(0.08, pos=(-0.13, 0.0, -0.3), scale=(0.7, 1.3, 0.4), color="E8F7FF", subdiv=2)
    return m


def sun():
    m = Model("sun")
    m.cyl(0.36, 0.16, rot=(90, 0, 0), color="FFD45C", segs=32, bevel=0.06)
    for k in range(8):
        a = k * 45
        m.push(rot=(0, 0, a))
        m.cone(0.09, 0.2, pos=(0, 0.52, 0), color="FFA94D", segs=12, rot=(0, 0, 0))
        m.pop()
    m.sphere(0.035, pos=(-0.1, 0.05, -0.08), color="5A3A2A", subdiv=1)
    m.sphere(0.035, pos=(0.1, 0.05, -0.08), color="5A3A2A", subdiv=1)
    m.torus(0.08, 0.018, pos=(0, -0.06, -0.08), rot=(90, 0, 180), color="5A3A2A", arc=180, segs=12, rsegs=6)
    return m


def wind():
    m = Model("wind")
    for i, (y, ln, rr, col) in enumerate(((0.26, 0.55, 0.12, "8FD3F0"), (0.0, 0.8, 0.15, "BDE8FA"), (-0.26, 0.45, 0.1, "8FD3F0"))):
        x0 = -0.45
        pts = []
        steps = 18
        for k in range(steps):
            pts.append((x0 + ln * k / (steps - 1), y))
        cx, cy = x0 + ln, y + rr
        for k in range(1, 16):
            a = -math.pi / 2 + k / 15 * math.pi * 1.4
            r = rr * (1 - k / 15 * 0.35)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        for (x, yy) in pts:
            m.sphere(0.05, pos=(x, yy, 0), color=col, subdiv=2)
    return m


def flower(petal="FF6F61"):
    m = Model("flower")
    for k in range(6):
        a = k / 6 * math.tau
        m.sphere(0.2, pos=(math.cos(a) * 0.24, math.sin(a) * 0.24, 0), scale=(1.0, 0.75, 0.35), rot=(0, 0, math.degrees(a)), color=petal, subdiv=2)
    m.sphere(0.16, pos=(0, 0, -0.06), scale=(1, 1, 0.6), color="FFD45C", subdiv=2)
    return m


def carrot():
    m = Model("carrot")
    m.cone(0.2, 0.75, pos=(0, -0.12, 0), rot=(0, 0, 180), color="F28C38", segs=16)
    for k in range(3):
        m.sphere(0.12, pos=(-0.08 + k * 0.08, 0.38 + (k % 2) * 0.05, 0), scale=(0.4, 1.3, 0.4), rot=(0, 0, -25 + k * 25), color="5DB548", subdiv=2)
    return m


def cabbage():
    m = Model("cabbage")
    m.blob(0.36, color="8FD16A", subdiv=3, amp=0.08, freq=3)
    for k in range(5):
        a = k / 5 * math.tau
        m.sphere(0.24, pos=(math.cos(a) * 0.22, math.sin(a) * 0.18 - 0.05, -0.05), scale=(1, 0.8, 0.5), color="6CC24A", subdiv=2)
    return m


def tomato():
    m = Model("tomato")
    m.sphere(0.36, scale=(1.05, 0.92, 1.0), color="E8574A", subdiv=3)
    for k in range(5):
        m.push(rot=(0, 0, k * 72))
        m.sphere(0.06, pos=(0, 0.32, -0.02), scale=(0.6, 1.6, 0.5), color="3F8F3A", subdiv=1)
        m.pop()
    m.sphere(0.07, pos=(-0.12, 0.12, -0.3), scale=(0.8, 1.2, 0.4), color="FFB0A8", subdiv=1)
    return m


def wheat():
    m = Model("wheat")
    m.cyl(0.025, 0.9, pos=(0, -0.05, 0), color="C99A3A", segs=6, bevel=0)
    for k in range(6):
        for s in (-1, 1):
            m.sphere(0.07, pos=(s * 0.07, 0.1 + k * 0.09, 0), scale=(0.7, 1.2, 0.6), rot=(0, 0, -s * 30), color="F2C14E", subdiv=1)
    m.sphere(0.06, pos=(0, 0.66, 0), scale=(0.7, 1.3, 0.6), color="F2C14E", subdiv=1)
    return m


def pumpkin():
    m = Model("pumpkin")
    for k in range(8):
        a = k / 8 * math.tau
        m.sphere(0.24, pos=(math.cos(a) * 0.17, 0, math.sin(a) * 0.17), scale=(0.75, 0.9, 0.75), color="F28C38" if k % 2 else "E67A2E", subdiv=2)
    m.cyl(0.05, 0.18, pos=(0, 0.28, 0), rot=(0, 0, 15), color="6B8E3A", segs=8, bevel=0.01)
    return m


def pig():
    m = Model("pig")
    m.sphere(0.4, color="FFB3C7", subdiv=3)
    m.cyl(0.15, 0.12, pos=(0, -0.06, -0.38), rot=(90, 0, 0), color="FF9AB4", segs=20, bevel=0.03)
    m.sphere(0.03, pos=(-0.05, -0.06, -0.45), color="8A4A5A", subdiv=1)
    m.sphere(0.03, pos=(0.05, -0.06, -0.45), color="8A4A5A", subdiv=1)
    for s in (-1, 1):
        m.sphere(0.04, pos=(s * 0.15, 0.12, -0.34), color="2A2633", subdiv=1)
        m.cone(0.11, 0.18, pos=(s * 0.26, 0.36, -0.05), rot=(0, 0, -s * 30), color="FF9AB4", segs=10)
    return m


def boat():
    m = Model("boat")
    m.prism([(-0.45, 0.0), (0.45, 0.0), (0.32, -0.2), (-0.32, -0.2)], 0.3, pos=(0, -0.25, 0), color="FBFAF7", bevel=0.03)
    m.box((0.95, 0.05, 0.32), pos=(0, -0.24, 0), color="E8574A", bevel=0.02)
    m.cyl(0.025, 0.85, pos=(0.05, 0.18, 0), color="8E6240", segs=8, bevel=0)
    m.prism([(0, 0), (0, 0.72), (-0.42, 0)], 0.02, pos=(0.03, -0.2, 0), color="FFF4DC", bevel=0.005)
    m.prism([(0, 0), (0, 0.5), (0.3, 0)], 0.02, pos=(0.08, -0.2, 0), color="F2E6CC", bevel=0.005)
    return m


def shirt():
    m = Model("shirt")
    pts = [(-0.18, 0.35), (-0.45, 0.2), (-0.36, 0.0), (-0.24, 0.06), (-0.24, -0.4), (0.24, -0.4), (0.24, 0.06), (0.36, 0.0), (0.45, 0.2), (0.18, 0.35), (0.08, 0.28), (-0.08, 0.28)]
    m.prism(pts, 0.08, color="6FC3E8", bevel=0.03)
    m.box((0.06, 0.16, 0.1), pos=(0, 0.38, -0.02), color="C8935B", bevel=0.02)
    m.cyl(0.03, 0.02, pos=(0, 0.0, -0.05), rot=(90, 0, 0), color="FBFAF7", segs=10, bevel=0)
    m.cyl(0.03, 0.02, pos=(0, -0.15, -0.05), rot=(90, 0, 0), color="FBFAF7", segs=10, bevel=0)
    return m


def windmill():
    m = Model("windmill")
    m.cyl(0.24, 0.65, pos=(0, -0.2, 0.05), radius2=0.16, color="F4EADA", segs=16, bevel=0.02)
    m.cone(0.22, 0.22, pos=(0, 0.23, 0.05), color="C2553F", segs=16)
    m.box((0.08, 0.16, 0.03), pos=(0, -0.45, -0.18), color="8E6240", bevel=0.01)
    for k in range(4):
        m.push(pos=(0, 0.12, -0.18), rot=(0, 0, k * 90 + 20))
        m.box((0.13, 0.5, 0.02), pos=(0, 0.3, 0), color="FFF4DC", bevel=0.01)
        m.box((0.03, 0.55, 0.03), pos=(-0.06, 0.28, -0.01), color="8E6240", bevel=0.005)
        m.pop()
    m.sphere(0.05, pos=(0, 0.12, -0.2), color="8E6240", subdiv=1)
    return m


def rainbow():
    m = Model("rainbow")
    cols = ["FF5D66", "FF9E4D", "FFE15A", "73E070", "5AB4FF", "9E7CFF"]
    for i, c in enumerate(cols):
        m.torus(0.48 - i * 0.065, 0.034, pos=(0, -0.25, 0), rot=(-90, 0, 0), color=c, arc=180, segs=28, rsegs=8)
    for s in (-1, 1):
        m.blob(0.13, pos=(s * 0.33, -0.27, -0.05), color="FFFFFF", subdiv=2, amp=0.1, seed=3)
        m.blob(0.1, pos=(s * 0.45, -0.3, -0.03), color="F2F4FA", subdiv=2, amp=0.1, seed=5)
    return m


def fire():
    m = Model("fire")
    def flame(scale, col, y):
        prof = [(0.0, -0.35), (0.25, -0.3), (0.32, -0.15), (0.28, 0.05), (0.18, 0.25), (0.08, 0.42), (0.0, 0.52)]
        m.lathe([(r * scale, yy * scale + y) for r, yy in prof], color=col, segs=20)
    flame(1.0, "FF7A3D", 0.0)
    m.push(pos=(0, -0.1, -0.2))
    flame(0.62, "FFC23D", 0.0)
    m.pop()
    m.push(pos=(0, -0.16, -0.32))
    flame(0.3, "FFF2B0", 0.0)
    m.pop()
    return m


def campfire():
    m = Model("campfire")
    for k in range(3):
        m.cyl(0.07, 0.7, pos=(0, -0.3, 0), rot=(70, k * 60, 0), color="8E6240", segs=8, bevel=0.02)
    m.push(pos=(0, 0.08, 0), scale=0.75)
    prof = [(0.0, -0.35), (0.25, -0.3), (0.32, -0.15), (0.28, 0.05), (0.18, 0.25), (0.08, 0.42), (0.0, 0.52)]
    m.lathe(prof, color="FF7A3D", segs=20)
    m.lathe([(r * 0.6, y * 0.6 - 0.1) for r, y in prof], pos=(0, 0, -0.22), color="FFC23D", segs=20)
    m.pop()
    return m


def castle():
    m = Model("castle")
    sand = "F2D9A0"
    m.box((0.7, 0.35, 0.4), pos=(0, -0.2, 0), color=sand, bevel=0.04)
    for s in (-1, 1):
        m.cyl(0.13, 0.6, pos=(s * 0.3, -0.05, 0), color=tint(sand, 1.04), segs=14, bevel=0.03)
        m.cone(0.15, 0.2, pos=(s * 0.3, 0.35, 0), color="E8B86A", segs=14)
    m.cyl(0.11, 0.75, pos=(0, 0.02, 0.05), color=sand, segs=14, bevel=0.03)
    m.cone(0.13, 0.22, pos=(0, 0.5, 0.05), color="E8B86A", segs=14)
    m.box((0.01, 0.22, 0.12), pos=(0, 0.72, 0.05), color="8E6240")
    m.prism([(0, 0), (0.16, 0.05), (0, 0.1)], 0.01, pos=(0.0, 0.73, 0.05), color="FF6F61")
    m.box((0.14, 0.2, 0.05), pos=(0, -0.28, -0.2), color="C9A670", bevel=0.03)
    return m


def cake():
    m = Model("cake")
    m.cyl(0.4, 0.26, pos=(0, -0.3, 0), color="FFF6EC", segs=28, bevel=0.04)
    m.cyl(0.29, 0.24, pos=(0, -0.05, 0), color="FFF6EC", segs=28, bevel=0.04)
    m.cyl(0.18, 0.22, pos=(0, 0.18, 0), color="FFF6EC", segs=28, bevel=0.04)
    for y, r in ((-0.18, 0.4), (0.07, 0.29), (0.29, 0.18)):
        m.torus(r, 0.025, pos=(0, y, 0), color="FF9EC4", segs=28, rsegs=6)
    m.sphere(0.07, pos=(0, 0.36, 0), color="FF6F61", subdiv=2)
    return m


def sunflower():
    m = Model("sunflower")
    for k in range(12):
        a = k / 12 * math.tau
        m.sphere(0.13, pos=(math.cos(a) * 0.3, math.sin(a) * 0.3, 0), scale=(1.0, 0.45, 0.25), rot=(0, 0, math.degrees(a)), color="FFD23F", subdiv=2)
    m.sphere(0.22, pos=(0, 0, -0.05), scale=(1, 1, 0.5), color="7A4E2D", subdiv=2)
    return m


def duck():
    m = Model("duck")
    m.sphere(0.32, pos=(0, -0.15, 0), scale=(1.2, 0.8, 1), color="FFD84D", subdiv=3)
    m.sphere(0.2, pos=(-0.18, 0.18, -0.02), color="FFD84D", subdiv=3)
    m.sphere(0.09, pos=(-0.36, 0.14, -0.04), scale=(1.4, 0.5, 0.9), color="FF9A3D", subdiv=2)
    m.sphere(0.035, pos=(-0.22, 0.24, -0.18), color="2A2633", subdiv=1)
    return m


def heart():
    m = Model("heart")
    m.sphere(0.25, pos=(-0.17, 0.12, 0), scale=(1, 1, 0.55), color="FF6B8A", subdiv=3)
    m.sphere(0.25, pos=(0.17, 0.12, 0), scale=(1, 1, 0.55), color="FF6B8A", subdiv=3)
    m.prism([(-0.4, 0.05), (0.4, 0.05), (0, -0.45)], 0.25, color="FF6B8A", bevel=0.08)
    m.sphere(0.06, pos=(-0.22, 0.2, -0.13), scale=(1, 0.7, 0.4), color="FFC2CF", subdiv=1)
    return m


def star(col="FFD23F", name="star"):
    m = Model(name)
    pts = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = 0.48 if k % 2 == 0 else 0.21
        pts.append((math.cos(a) * r, math.sin(a) * r))
    m.prism(pts, 0.16, color=col, bevel=0.05)
    return m


def gear():
    m = Model("gear")
    pts, teeth = [], 8
    step = 2 * math.pi / teeth
    for k in range(teeth):
        a = math.pi / 2 + k * step
        # base-left, tip-left, tip-right, base-right: flat-topped teeth narrower than the gaps
        for off, r in ((-0.30, 0.35), (-0.17, 0.49), (0.17, 0.49), (0.30, 0.35)):
            pts.append((math.cos(a + off * step) * r, math.sin(a + off * step) * r))
    m.prism(pts, 0.18, color="FFD86B", bevel=0.035)
    # the axle hole, faked with a recessed disc in the button's lilac
    m.cyl(0.15, 0.04, pos=(0, 0, -0.08), rot=(90, 0, 0), color="8E7BD8", segs=28, bevel=0.01)
    return m


def check():
    m = Model("check")
    m.prism([(-0.4, 0.05), (-0.25, 0.2), (-0.08, 0.02), (0.3, 0.42), (0.45, 0.27), (-0.08, -0.3)], 0.16, color="5DC25A", bevel=0.05)
    return m


def lock():
    m = Model("lock")
    m.box((0.6, 0.48, 0.24), pos=(0, -0.15, 0), color="F2B73F", bevel=0.08)
    m.torus(0.19, 0.06, pos=(0, 0.1, 0), rot=(-90, 0, 0), color="A9A3B8", arc=180, segs=18, rsegs=8)
    m.cyl(0.06, 0.12, pos=(-0.19, 0.06, 0), color="A9A3B8", segs=10, bevel=0)
    m.cyl(0.06, 0.12, pos=(0.19, 0.06, 0), color="A9A3B8", segs=10, bevel=0)
    m.cyl(0.06, 0.05, pos=(0, -0.12, -0.12), rot=(90, 0, 0), color="6B4A2A", segs=12, bevel=0)
    return m


def soggy():
    m = Model("soggy")
    m.cyl(0.42, 0.06, pos=(0, -0.3, 0), color="6FB8E0", segs=28, bevel=0.03)
    prof = [(0.0, -0.45), (0.3, -0.35), (0.36, -0.15), (0.3, 0.05), (0.18, 0.28), (0.06, 0.45), (0.0, 0.55)]
    m.lathe([(r * 0.55, y * 0.55 + 0.1) for r, y in prof], color="4FB6D8", segs=24)
    return m


def oops():
    m = Model("oops")
    m.box((0.14, 0.5, 0.14), pos=(0, 0.12, 0), color="FF5D66", bevel=0.06)
    m.sphere(0.09, pos=(0, -0.3, 0), color="FF5D66", subdiv=2)
    return m


def grumpy():
    m = Model("grumpy")
    m.blob(0.3, pos=(0, 0, 0), color="9AA3B8", subdiv=3, amp=0.12, seed=4)
    m.blob(0.22, pos=(-0.28, -0.06, 0), color="8E97AC", subdiv=2, amp=0.12, seed=5)
    m.blob(0.22, pos=(0.28, -0.04, 0), color="8E97AC", subdiv=2, amp=0.12, seed=6)
    m.prism([(0.0, 0.0), (0.12, 0.0), (0.03, -0.18), (0.13, -0.18), (-0.06, -0.42), (0.0, -0.22), (-0.09, -0.22)], 0.06, pos=(0, -0.2, -0.1), color="FFD23F", bevel=0.01)
    return m


def clock():
    m = Model("clock")
    m.cyl(0.42, 0.12, rot=(90, 0, 0), color="FBFAF7", segs=32, bevel=0.04)
    m.torus(0.42, 0.05, rot=(90, 0, 0), color="FF8A5C", segs=32, rsegs=8)
    m.box((0.05, 0.28, 0.03), pos=(0, 0.12, -0.08), color="3B3A5A", bevel=0.01)
    m.box((0.2, 0.05, 0.03), pos=(0.09, 0, -0.08), color="3B3A5A", bevel=0.01)
    m.sphere(0.04, pos=(0, 0, -0.09), color="FF8A5C", subdiv=1)
    return m


def stamp(col, emblem, name):
    m = Model(name)
    m.cyl(0.46, 0.1, rot=(90, 0, 0), color=col, segs=36, bevel=0.03)
    m.torus(0.4, 0.025, pos=(0, 0, -0.05), rot=(90, 0, 0), color=tint(col, 1.25), segs=36, rsegs=6)
    m.push(pos=(0, 0, -0.08), scale=0.62)
    emblem(m)
    m.pop()
    return m


def em_sun(m):
    m.cyl(0.3, 0.06, rot=(90, 0, 0), color="FFF2B0", segs=24, bevel=0.02)
    for k in range(8):
        m.push(rot=(0, 0, k * 45))
        m.cone(0.08, 0.16, pos=(0, 0.44, 0), color="FFF2B0", segs=8)
        m.pop()


def em_clock(m):
    m.torus(0.33, 0.06, rot=(90, 0, 0), color="FFF2E8", segs=28, rsegs=6)
    m.box((0.07, 0.3, 0.04), pos=(0, 0.12, 0), color="FFF2E8", bevel=0.01)
    m.box((0.24, 0.07, 0.04), pos=(0.1, 0, 0), color="FFF2E8", bevel=0.01)


def em_flower(m):
    for k in range(5):
        a = k / 5 * math.tau + math.pi / 2
        m.sphere(0.17, pos=(math.cos(a) * 0.2, math.sin(a) * 0.2, 0), scale=(1, 0.8, 0.3), rot=(0, 0, math.degrees(a)), color="FFF0F6", subdiv=2)
    m.sphere(0.12, pos=(0, 0, -0.04), scale=(1, 1, 0.5), color="FFE38A", subdiv=2)


def pip():
    m = Model("pip")
    for (x, y, r) in ((0, 0.08, 0.32), (-0.3, -0.04, 0.24), (0.31, -0.02, 0.25), (0.1, 0.28, 0.22), (-0.16, 0.22, 0.2), (-0.12, -0.14, 0.2), (0.15, -0.15, 0.2)):
        m.blob(r, pos=(x, y, 0), color="FFFFFF", subdiv=3, amp=0.05, seed=int(x * 10 + 20))
    return m


def sheep_icon():
    m = Model("sheep")
    m.blob(0.36, pos=(0, 0.02, 0.05), color="F7F3EA", subdiv=3, amp=0.16, freq=5.5, seed=2)
    m.sphere(0.2, pos=(0, -0.05, -0.25), scale=(0.8, 0.9, 0.8), color="3E3B48", subdiv=3)
    m.blob(0.12, pos=(0, 0.12, -0.25), color="F7F3EA", subdiv=2, amp=0.2, freq=5, seed=4)
    for s in (-1, 1):
        m.sphere(0.035, pos=(s * 0.07, -0.03, -0.42), color="FFFFFF", subdiv=1)
        m.sphere(0.07, pos=(s * 0.2, 0.0, -0.22), scale=(1.4, 0.5, 0.7), color="3E3B48", subdiv=2)
    return m


def person():
    m = Model("person")
    m.sphere(0.3, pos=(0, 0.12, 0), color="F4C9A0", subdiv=3)
    m.sphere(0.31, pos=(0, 0.19, 0.04), scale=(1, 0.86, 1), color="5A3A2A", subdiv=3)
    m.lathe([(0.32, -0.5), (0.3, -0.3), (0.2, -0.17), (0.1, -0.15)], color="FF6F61", segs=20)
    for s in (-1, 1):
        m.sphere(0.035, pos=(s * 0.1, 0.1, -0.28), color="2A2633", subdiv=1)
        m.sphere(0.05, pos=(s * 0.18, 0.02, -0.24), scale=(1, 0.6, 0.4), color="FF9AA2", subdiv=1)
    return m


def bell_icon():
    m = Model("bell")
    m.lathe([(0.0, 0.42), (0.1, 0.4), (0.18, 0.3), (0.22, 0.05), (0.3, -0.2), (0.38, -0.28), (0.0, -0.28)], color="F2B73F", segs=24)
    m.sphere(0.07, pos=(0, -0.33, 0), color="C98A2A", subdiv=2)
    return m


def kite():
    m = Model("kite")
    m.prism([(0, 0.45), (0.3, 0.05), (0, -0.4), (-0.3, 0.05)], 0.04, color="FF6F61", bevel=0.01)
    m.prism([(0, 0.45), (0.3, 0.05), (0, 0.05)], 0.05, pos=(0, 0, -0.01), color="FFD45C", bevel=0.01)
    m.prism([(0, -0.4), (-0.3, 0.05), (0, 0.05)], 0.05, pos=(0, 0, -0.01), color="6FC3E8", bevel=0.01)
    for k in range(3):
        m.box((0.08, 0.05, 0.02), pos=(0.05 * k, -0.48 - k * 0.1, 0), rot=(0, 0, 30), color="B79CFF", bevel=0.01)
    return m


def snail():
    m = Model("snail")
    m.capsule(0.1, 0.6, pos=(0, -0.25, 0), rot=(0, 0, 90), color="C9D98A")
    m.torus(0.18, 0.1, pos=(0.05, 0.02, 0), rot=(90, 0, 0), color="D98A5A", segs=20, rsegs=10)
    m.sphere(0.12, pos=(0.05, 0.02, 0), scale=(1, 1, 0.8), color="E8A070", subdiv=2)
    for s in (-1, 1):
        m.cyl(0.015, 0.16, pos=(-0.25 + s * 0.03, -0.08, 0), rot=(0, 0, 20 * s), color="C9D98A", segs=6, bevel=0)
        m.sphere(0.03, pos=(-0.27 + s * 0.06, 0.0, 0), color="2A2633", subdiv=1)
    return m


def birdbath():
    m = Model("birdbath")
    m.cyl(0.08, 0.5, pos=(0, -0.2, 0), color="C8C2B8", segs=12, bevel=0.02)
    m.cyl(0.36, 0.1, pos=(0, 0.1, 0), radius2=0.42, color="D8D2C8", segs=24, bevel=0.03)
    m.cyl(0.33, 0.02, pos=(0, 0.16, 0), color="6FC3E8", segs=24, bevel=0)
    m.sphere(0.1, pos=(0.15, 0.27, 0), scale=(1.3, 0.9, 0.9), color="E8574A", subdiv=2)
    return m


def seal():
    m = Model("seal")
    m.sphere(0.35, pos=(0, -0.1, 0), scale=(1.3, 0.8, 0.9), color="9AA3B8", subdiv=3)
    m.sphere(0.22, pos=(-0.3, 0.15, -0.05), color="A9B2C6", subdiv=3)
    m.sphere(0.04, pos=(-0.38, 0.2, -0.22), color="2A2633", subdiv=1)
    m.sphere(0.04, pos=(-0.24, 0.2, -0.24), color="2A2633", subdiv=1)
    m.sphere(0.05, pos=(-0.33, 0.1, -0.25), color="3B3A5A", subdiv=1)
    return m


def icecream():
    m = Model("icecream")
    m.cone(0.2, 0.55, pos=(0, -0.2, 0), rot=(0, 0, 180), color="E8B86A", segs=14)
    m.sphere(0.2, pos=(0, 0.16, 0), color="FF9EC4", subdiv=3)
    m.sphere(0.17, pos=(0, 0.36, 0), color="FFF4DC", subdiv=3)
    m.sphere(0.05, pos=(0.03, 0.52, 0), color="E8574A", subdiv=2)
    return m


def bouquet():
    m = Model("bouquet")
    for k, c in enumerate(("FF6F61", "FFD45C", "FF9EC4", "B79CFF", "FBFAF7")):
        a = k / 5 * math.tau
        m.sphere(0.13, pos=(math.cos(a) * 0.15, 0.22 + math.sin(a) * 0.08, math.sin(a) * 0.1), color=c, subdiv=2)
    m.cone(0.16, 0.45, pos=(0, -0.15, 0), rot=(0, 0, 180), color="8FD16A", segs=12)
    m.torus(0.1, 0.03, pos=(0, -0.02, 0), color="FBFAF7", segs=16, rsegs=6)
    return m


def hand():
    """Cartoon glove pointing up-left (for gesture hints)."""
    m = Model("hand")
    glove = "FBFAF7"
    m.push(rot=(0, 0, 20))
    m.box((0.42, 0.4, 0.2), pos=(0, -0.1, 0), color=glove, bevel=0.12)
    m.capsule(0.08, 0.5, pos=(-0.13, 0.28, 0), color=glove)
    for k, x in enumerate((-0.0, 0.11)):
        m.sphere(0.09, pos=(x + 0.02, 0.1, -0.08), color=tint(glove, 0.95), subdiv=2)
    m.sphere(0.075, pos=(0.17, 0.0, -0.06), color=tint(glove, 0.95), subdiv=2)
    m.capsule(0.075, 0.26, pos=(-0.24, -0.08, -0.04), rot=(0, 0, 50), color=glove)
    m.box((0.46, 0.12, 0.24), pos=(0, -0.34, 0), color="B79CFF", bevel=0.05)
    for x in (-0.07, 0.07):
        m.box((0.02, 0.18, 0.01), pos=(x, -0.02, -0.105), color="D9D4E8", bevel=0.0)
    m.pop()
    return m


def laundry_icon():
    return shirt()


ICONS = {
    "drop": drop, "sun": sun, "wind": wind, "flower": flower, "carrot": carrot, "cabbage": cabbage, "tomato": tomato,
    "wheat": wheat, "pumpkin": pumpkin, "mud": pig, "pig": pig, "boat": boat, "shirt": shirt, "windmill": windmill,
    "rainbow": rainbow, "fire": fire, "campfire": campfire, "castle": castle, "cake": cake, "sunflower": sunflower,
    "duck": duck, "heart": heart, "star": star, "gear": gear, "check": check, "lock": lock, "soggy": soggy, "oops": oops,
    "grumpy": grumpy, "clock": clock, "pip": pip, "sheep": sheep_icon, "person": person, "bell": bell_icon,
    "kite": kite, "hand": hand, "snail": snail, "birdbath": birdbath, "seal": seal, "icecream": icecream, "bouquet": bouquet,
    "stamp_sun": lambda: stamp("F2A93B", em_sun, "stamp_sun"),
    "stamp_clock": lambda: stamp("5AA9E6", em_clock, "stamp_clock"),
    "stamp_flower": lambda: stamp("E8689A", em_flower, "stamp_flower"),
    "stamp_empty": lambda: stamp("D9D4E8", lambda m: None, "stamp_empty"),
}


# ----------------------------------------------------------------------------- rendering

def setup_scene(size):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = size
    sc.render.resolution_y = size
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    try:
        sc.eevee.taa_render_samples = 32
    except Exception:
        pass
    world = bpy.data.worlds.new("w")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.82, 0.86, 0.95, 1)
    bg.inputs[1].default_value = 0.9
    sun = bpy.data.lights.new("key", "SUN")
    sun.energy = 3.2
    sun.angle = math.radians(12)
    so = bpy.data.objects.new("key", sun)
    so.rotation_euler = (math.radians(50), math.radians(-25), math.radians(-35))
    sc.collection.objects.link(so)
    fill = bpy.data.lights.new("fill", "SUN")
    fill.energy = 0.9
    fo = bpy.data.objects.new("fill", fill)
    fo.rotation_euler = (math.radians(70), math.radians(40), math.radians(140))
    sc.collection.objects.link(fo)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    mat = bpy.data.materials.new("vc")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.42
    try:
        bsdf.inputs["Coat Weight"].default_value = 0.25
    except Exception:
        pass
    # gentle emission keeps colours bright and cartoony
    try:
        nt.links.new(attr.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 0.22
    except Exception:
        pass
    return cam, mat


def render_icon(name, fn, cam, mat, size):
    for o in list(bpy.data.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    model = fn()
    obj = model.build()
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = poly.use_smooth
    # frame: camera looks from Unity -Z (Blender +Y) slightly from above
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2
    ext = max(hi.x - lo.x, hi.z - lo.z, (hi.y - lo.y) * 0.6)
    pitch = math.radians(14)
    yaw = math.radians(-12)
    d = Vector((math.sin(yaw) * math.cos(pitch), math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = c + d * 10
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = ext * 1.22
    cam.data.clip_end = 50
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)


def main():
    only, size = args()
    pw_lib.reset_scene()
    os.makedirs(OUT, exist_ok=True)
    cam, mat = setup_scene(size)
    for name, fn in ICONS.items():
        if only and name not in only:
            continue
        render_icon(name, fn, cam, mat, size)
        print("[icon]", name)


main()
