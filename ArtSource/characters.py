"""Pocketvale's residents: wooden peg-doll people and toy animals. All face Unity +Z.
Parts: Body (+ Head for nods/glances). Head pivots sit at the neck.
"""
import math
import random

from pw_lib import Model, tint
from props_core import (WOOL, SHEEP_FACE, WHITE, BLUSH, CORAL, SKY, BUTTER, LILAC, PINK, RED, LEAF, WOOD, WOOD_DK,
                        peg_person, sheep)

SKIN = ("F4C9A0", "E8B48A", "C98E64", "8D5A3B", "F6D5B5")
HAIR = ("5A3A2A", "2E2622", "8A3B2E", "D9A84A", "B9B3AE", "1E1A1A")


def person(name, body, skin=0, hair=0, hat=None, dress=False, scale=1.0, extra=None):
    out = peg_person(name, body=body, skin=SKIN[skin], hair=HAIR[hair], hat=hat, dress=dress, scale=scale)
    parts = out[0][1]
    b, h = parts
    if extra:
        extra(b, h, scale)
    return [(name, [b, h])]


def chef_hat(b, h, s):
    h.push(pos=(0, 0.43 * s, 0), scale=s)
    h.cyl(0.08, 0.1, pos=(0, 0.13, 0), color=WHITE, segs=12, bevel=0.01)
    h.sphere(0.1, pos=(0, 0.2, 0), scale=(1, 0.7, 1), color=WHITE, subdiv=2)
    h.pop()


def cap(col):
    def f(b, h, s):
        h.push(pos=(0, 0.43 * s, 0), scale=s)
        h.sphere(0.105, pos=(0, 0.035, -0.01), scale=(1, 0.6, 1), color=col, subdiv=2)
        h.box((0.14, 0.015, 0.1), pos=(0, 0.04, 0.1), color=tint(col, 0.85), bevel=0.005)
        h.pop()
    return f


def veil(b, h, s):
    h.push(pos=(0, 0.43 * s, 0), scale=s)
    h.sphere(0.13, pos=(0, -0.02, -0.04), scale=(1.05, 1.25, 0.9), color="FFFFFF", subdiv=2)
    for k in range(5):
        a = k / 5 * math.tau
        h.sphere(0.02, pos=(math.cos(a) * 0.07, 0.09, math.sin(a) * 0.07), color=PINK if k % 2 else BUTTER, subdiv=1)
    h.pop()
    b.push(scale=s)
    for k in range(3):
        b.sphere(0.03, pos=(-0.04 + k * 0.04, 0.22, 0.1), color=(CORAL, BUTTER, PINK)[k], subdiv=1)
    b.pop()


def bowtie(b, h, s):
    b.push(scale=s)
    b.box((0.08, 0.035, 0.02), pos=(0, 0.3, 0.075), color="E8574A", bevel=0.005)
    b.box((0.02, 0.2, 0.01), pos=(0, 0.18, 0.096), color="FFFFFF", bevel=0.0)
    b.pop()


def raincoat(b, h, s):
    h.push(pos=(0, 0.43 * s, 0), scale=s)
    h.sphere(0.11, pos=(0, 0.03, -0.015), scale=(1.05, 0.8, 1.05), color=BUTTER, subdiv=2)
    h.cyl(0.13, 0.012, pos=(0, 0.0, 0.0), color=BUTTER, segs=14, bevel=0.0)
    h.pop()
    b.push(scale=s)
    b.cyl(0.07, 0.08, pos=(-0.04, 0.03, 0), color="E8574A", segs=8, bevel=0.01)
    b.cyl(0.07, 0.08, pos=(0.04, 0.03, 0), color="E8574A", segs=8, bevel=0.01)
    b.pop()


def sunglasses(b, h, s):
    h.push(pos=(0, 0.43 * s, 0), scale=s)
    h.box((0.13, 0.03, 0.02), pos=(0, 0.005, 0.1), color="2A2633", bevel=0.005)
    h.pop()


def person_lying(name, body, skin=0, hair=0, glasses=True):
    b = Model("Body")
    b.capsule(0.09, 0.42, pos=(0, 0.09, 0.0), rot=(90, 0, 0), color=body)
    b.sphere(0.04, pos=(-0.04, 0.09, -0.24), color=SKIN[skin], subdiv=1)
    b.sphere(0.04, pos=(0.04, 0.09, -0.24), color=SKIN[skin], subdiv=1)
    h = Model("Head", pivot=(0, 0.1, 0.22))
    h.sphere(0.09, pos=(0, 0.11, 0.3), color=SKIN[skin], subdiv=3)
    h.sphere(0.093, pos=(0, 0.11, 0.33), scale=(1, 1.0, 0.85), color=HAIR[hair], subdiv=2)
    h.sphere(0.011, pos=(-0.03, 0.2, 0.29), color="2A2633", subdiv=1)
    h.sphere(0.011, pos=(0.03, 0.2, 0.29), color="2A2633", subdiv=1)
    if glasses:
        h.box((0.12, 0.02, 0.03), pos=(0, 0.2, 0.28), color="2A2633", bevel=0.005)
    return [(name, [b, h])]


def cow():
    b = Model("Body")
    b.capsule(0.17, 0.62, pos=(0, 0.33, 0), rot=(90, 0, 0), color="FBFAF7")
    rnd = random.Random(3)
    for k in range(5):
        b.sphere(0.08, pos=(rnd.choice((-1, 1)) * 0.15, 0.36 + rnd.uniform(-0.05, 0.1), rnd.uniform(-0.2, 0.2)), scale=(0.4, 1, 1.2), color="2E2A2E", subdiv=2)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.cyl(0.045, 0.22, pos=(sx * 0.1, 0.11, sz * 0.2), color="FBFAF7", segs=8, bevel=0.01)
            b.cyl(0.048, 0.04, pos=(sx * 0.1, 0.02, sz * 0.2), color="5A4A44", segs=8, bevel=0.005)
    b.sphere(0.06, pos=(0, 0.22, -0.12), scale=(1, 0.6, 1), color="FFB3C7", subdiv=1)
    b.cyl(0.015, 0.2, pos=(0, 0.35, -0.33), rot=(30, 0, 0), color="2E2A2E", segs=4, bevel=0)
    h = Model("Head", pivot=(0, 0.42, 0.28))
    h.sphere(0.13, pos=(0, 0.45, 0.38), scale=(0.9, 0.95, 1.1), color="FBFAF7", subdiv=3)
    h.sphere(0.09, pos=(0, 0.4, 0.48), scale=(1.1, 0.8, 0.8), color="FFB3C7", subdiv=2)
    for sx in (-1, 1):
        h.sphere(0.012, pos=(sx * 0.035, 0.4, 0.55), color="8A4A5A", subdiv=1)
        h.sphere(0.02, pos=(sx * 0.06, 0.5, 0.48), color="2A2633", subdiv=1)
        h.cone(0.025, 0.08, pos=(sx * 0.08, 0.58, 0.36), rot=(0, 0, -sx * 25), color="F2E6CC", segs=6)
        h.sphere(0.04, pos=(sx * 0.14, 0.5, 0.36), scale=(1.4, 0.5, 0.8), color="2E2A2E", subdiv=1)
    return [("cow", [b, h])]


def pig():
    b = Model("Body")
    b.sphere(0.2, pos=(0, 0.2, 0), scale=(0.9, 0.85, 1.2), color="FFB3C7", subdiv=3)
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.cyl(0.04, 0.1, pos=(sx * 0.09, 0.05, sz * 0.12), color="FF9AB4", segs=8, bevel=0.01)
    b.torus(0.03, 0.01, pos=(0, 0.25, -0.24), rot=(90, 0, 0), color="FF9AB4", segs=10, rsegs=4)
    h = Model("Head", pivot=(0, 0.24, 0.18))
    h.sphere(0.13, pos=(0, 0.26, 0.25), color="FFB3C7", subdiv=3)
    h.cyl(0.06, 0.05, pos=(0, 0.24, 0.37), rot=(90, 0, 0), color="FF9AB4", segs=12, bevel=0.01)
    for sx in (-1, 1):
        h.sphere(0.012, pos=(sx * 0.022, 0.245, 0.4), color="8A4A5A", subdiv=1)
        h.sphere(0.018, pos=(sx * 0.055, 0.31, 0.35), color="2A2633", subdiv=1)
        h.cone(0.05, 0.08, pos=(sx * 0.08, 0.38, 0.22), rot=(-20, 0, -sx * 25), color="FF9AB4", segs=8)
    return [("pig", [b, h])]


def donkey():
    b = Model("Body")
    b.capsule(0.14, 0.52, pos=(0, 0.33, 0), rot=(90, 0, 0), color="9A939E")
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.cyl(0.04, 0.26, pos=(sx * 0.08, 0.13, sz * 0.17), color="8A838E", segs=8, bevel=0.01)
    b.cyl(0.02, 0.18, pos=(0, 0.32, -0.3), rot=(30, 0, 0), color="6E6874", segs=5, bevel=0)
    h = Model("Head", pivot=(0, 0.42, 0.22))
    h.capsule(0.08, 0.28, pos=(0, 0.5, 0.33), rot=(60, 0, 0), color="9A939E")
    h.sphere(0.07, pos=(0, 0.43, 0.44), color="E8E2EA", subdiv=2)
    for sx in (-1, 1):
        h.sphere(0.018, pos=(sx * 0.055, 0.56, 0.38), color="2A2633", subdiv=1)
        h.capsule(0.03, 0.22, pos=(sx * 0.06, 0.7, 0.28), rot=(-15, 0, sx * 12), color="8A838E")
    return [("donkey", [b, h])]


def dog():
    b = Model("Body")
    b.capsule(0.1, 0.36, pos=(0, 0.17, 0), rot=(90, 0, 0), color="C98E5A")
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.cyl(0.03, 0.12, pos=(sx * 0.06, 0.06, sz * 0.1), color="C98E5A", segs=6, bevel=0.01)
    b.capsule(0.02, 0.14, pos=(0, 0.25, -0.2), rot=(-40, 0, 0), color="C98E5A")
    h = Model("Head", pivot=(0, 0.24, 0.13))
    h.sphere(0.1, pos=(0, 0.28, 0.2), color="C98E5A", subdiv=3)
    h.sphere(0.05, pos=(0, 0.25, 0.29), scale=(1, 0.8, 1.1), color="F2DDBE", subdiv=2)
    h.sphere(0.02, pos=(0, 0.27, 0.34), color="2A2633", subdiv=1)
    for sx in (-1, 1):
        h.sphere(0.016, pos=(sx * 0.04, 0.31, 0.28), color="2A2633", subdiv=1)
        h.sphere(0.05, pos=(sx * 0.1, 0.27, 0.18), scale=(0.5, 1.2, 0.8), color="8E5A34", subdiv=2)
    return [("dog", [b, h])]


def cat():
    b = Model("Body")
    b.capsule(0.08, 0.3, pos=(0, 0.13, 0), rot=(90, 0, 0), color="F2A15A")
    b.capsule(0.018, 0.2, pos=(0, 0.2, -0.19), rot=(-35, 0, 0), color="F2A15A")
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.cyl(0.025, 0.08, pos=(sx * 0.045, 0.04, sz * 0.08), color="FBF0E0", segs=6, bevel=0.005)
    for k in range(3):
        b.box((0.17, 0.012, 0.03), pos=(0, 0.21, -0.06 + k * 0.06), color="D9823E", bevel=0.0)
    h = Model("Head", pivot=(0, 0.2, 0.1))
    h.sphere(0.085, pos=(0, 0.24, 0.16), color="F2A15A", subdiv=3)
    h.sphere(0.04, pos=(0, 0.21, 0.23), scale=(1.2, 0.7, 0.8), color="FBF0E0", subdiv=2)
    for sx in (-1, 1):
        h.cone(0.035, 0.07, pos=(sx * 0.05, 0.32, 0.15), color="F2A15A", segs=5)
        h.sphere(0.014, pos=(sx * 0.033, 0.26, 0.235), color="2A6A3A", subdiv=1)
    h.sphere(0.01, pos=(0, 0.225, 0.25), color="FF8AA0", subdiv=1)
    return [("cat", [b, h])]


def duck():
    b = Model("Body")
    b.sphere(0.1, pos=(0, 0.08, 0), scale=(0.9, 0.75, 1.25), color="FBFAF7", subdiv=3)
    b.cone(0.05, 0.08, pos=(0, 0.11, -0.13), rot=(-110, 0, 0), color="FBFAF7", segs=6)
    h = Model("Head", pivot=(0, 0.13, 0.08))
    h.sphere(0.06, pos=(0, 0.19, 0.1), color="FBFAF7", subdiv=2)
    h.sphere(0.03, pos=(0, 0.18, 0.17), scale=(1.2, 0.5, 1.2), color="FF9A3D", subdiv=1)
    for sx in (-1, 1):
        h.sphere(0.01, pos=(sx * 0.03, 0.21, 0.14), color="2A2633", subdiv=1)
    return [("duck", [b, h])]


def duckling():
    out = duck()
    name, parts = out[0]
    return [("duckling", parts)]


def frog():
    b = Model("Body")
    b.sphere(0.08, pos=(0, 0.06, 0), scale=(1.1, 0.7, 1.0), color="6CC24A", subdiv=2)
    for sx in (-1, 1):
        b.sphere(0.04, pos=(sx * 0.07, 0.03, -0.03), scale=(1, 0.6, 1.4), color="5DAA48", subdiv=1)
    h = Model("Head", pivot=(0, 0.08, 0.04))
    for sx in (-1, 1):
        h.sphere(0.03, pos=(sx * 0.04, 0.12, 0.05), color="6CC24A", subdiv=2)
        h.sphere(0.015, pos=(sx * 0.04, 0.13, 0.075), color="2A2633", subdiv=1)
    h.torus(0.04, 0.006, pos=(0, 0.07, 0.07), rot=(70, 0, 0), color="3F7F3A", arc=180, segs=10, rsegs=4)
    return [("frog", [b, h])]


def snail():
    b = Model("Body")
    b.capsule(0.03, 0.2, pos=(0, 0.03, 0), rot=(90, 0, 0), color="C9D98A")
    b.torus(0.05, 0.03, pos=(0, 0.08, -0.02), rot=(0, 0, 90), color="D98A5A", segs=16, rsegs=8)
    b.sphere(0.035, pos=(0, 0.08, -0.02), scale=(0.8, 1, 1), color="E8A070", subdiv=2)
    h = Model("Head", pivot=(0, 0.04, 0.09))
    for sx in (-1, 1):
        h.cyl(0.005, 0.06, pos=(sx * 0.012, 0.08, 0.1), rot=(-20, 0, sx * 15), color="C9D98A", segs=4, bevel=0)
        h.sphere(0.009, pos=(sx * 0.02, 0.11, 0.11), color="2A2633", subdiv=1)
    return [("snail", [b, h])]


def robin():
    b = Model("Body")
    b.sphere(0.06, pos=(0, 0.06, 0), scale=(0.9, 0.85, 1.2), color="8A6A4A", subdiv=2)
    b.sphere(0.045, pos=(0, 0.06, 0.03), scale=(0.9, 0.9, 0.6), color="E8574A", subdiv=2)
    b.cone(0.03, 0.06, pos=(0, 0.07, -0.08), rot=(-100, 0, 0), color="6E523A", segs=5)
    h = Model("Head", pivot=(0, 0.09, 0.03))
    h.sphere(0.035, pos=(0, 0.11, 0.05), color="8A6A4A", subdiv=2)
    h.cone(0.01, 0.03, pos=(0, 0.11, 0.09), rot=(90, 0, 0), color="E8B04A", segs=5)
    return [("robin", [b, h])]


def seal():
    b = Model("Body")
    b.capsule(0.12, 0.5, pos=(0, 0.1, 0), rot=(80, 0, 0), color="9AA3B8")
    for sx in (-1, 1):
        b.sphere(0.05, pos=(sx * 0.12, 0.05, 0.08), scale=(1.4, 0.3, 0.8), color="8A93A8", subdiv=1)
    b.sphere(0.06, pos=(0, 0.04, -0.26), scale=(1.6, 0.3, 0.8), color="8A93A8", subdiv=1)
    h = Model("Head", pivot=(0, 0.18, 0.16))
    h.sphere(0.1, pos=(0, 0.24, 0.22), color="A9B2C6", subdiv=3)
    for sx in (-1, 1):
        h.sphere(0.018, pos=(sx * 0.04, 0.27, 0.3), color="2A2633", subdiv=1)
    h.sphere(0.022, pos=(0, 0.23, 0.32), color="3B3A5A", subdiv=1)
    return [("seal", [b, h])]


def crab():
    b = Model("Body")
    b.sphere(0.08, pos=(0, 0.05, 0), scale=(1.3, 0.6, 1.0), color="E8574A", subdiv=2)
    for sx in (-1, 1):
        for k in range(3):
            b.cyl(0.008, 0.08, pos=(sx * 0.1, 0.03, -0.04 + k * 0.04), rot=(0, 0, sx * 60), color="C9453A", segs=4, bevel=0)
        b.sphere(0.03, pos=(sx * 0.11, 0.07, 0.07), scale=(1, 0.7, 1.2), color="E8574A", subdiv=1)
    h = Model("Head", pivot=(0, 0.06, 0.05))
    for sx in (-1, 1):
        h.cyl(0.004, 0.04, pos=(sx * 0.025, 0.1, 0.05), color="C9453A", segs=4, bevel=0)
        h.sphere(0.012, pos=(sx * 0.025, 0.12, 0.05), color="2A2633", subdiv=1)
    return [("crab", [b, h])]


def gull():
    b = Model("Body")
    b.sphere(0.07, pos=(0, 0.08, 0), scale=(0.9, 0.85, 1.4), color="FBFAF7", subdiv=2)
    for sx in (-1, 1):
        b.sphere(0.05, pos=(sx * 0.06, 0.1, -0.02), scale=(0.3, 0.6, 1.6), color="AEB4C0", subdiv=1)
    h = Model("Head", pivot=(0, 0.11, 0.06))
    h.sphere(0.045, pos=(0, 0.14, 0.08), color="FBFAF7", subdiv=2)
    h.cone(0.012, 0.05, pos=(0, 0.13, 0.14), rot=(90, 0, 0), color="F2B73F", segs=5)
    return [("gull", [b, h])]


def lamb():
    return sheep("lamb", scale=0.7)


def all_characters():
    out = []
    out += person("person_kid", "6CCB8A", 0, 3, scale=0.78)
    out += person("person_kid_rain", BUTTER, 4, 0, scale=0.78, extra=raincoat)
    out += person("person_kid_b", "E8574A", 2, 1, scale=0.78)
    out += person("person_old", "5F8F6A", 0, 4, extra=cap("6E6874"))
    out += person("person_baker", WHITE, 1, 1, extra=chef_hat)
    out += person("person_miller", "B8865A", 2, 0, extra=cap("8A6A4A"))
    out += person("person_camper_a", "F28C38", 3, 1, extra=cap("3E9A5A"))
    out += person("person_camper_b", "5FB8E6", 4, 3)
    out += person("person_guest_a", LILAC, 0, 1, dress=True)
    out += person("person_guest_b", "5C7A99", 2, 0, extra=bowtie)
    out += person("person_guest_c", PINK, 1, 3, dress=True)
    out += person("person_guest_d", "6CCB8A", 3, 5, extra=bowtie)
    out += person("person_bride", "FFFFFF", 0, 2, dress=True, extra=veil)
    out += person("person_groom", "3B3A5A", 1, 0, extra=bowtie)
    out += person("person_musician", "E8574A", 3, 1, extra=cap("2E2622"))
    out += person("person_sunglasses", "FFD45C", 0, 3, extra=sunglasses)
    out += person_lying("person_sunbather_a", "FF6F61", 0, 3)
    out += person_lying("person_sunbather_b", "5FB8E6", 3, 1)
    out += cow() + pig() + donkey() + dog() + cat() + duck() + duckling() + frog() + snail() + robin() + seal()
    out += crab() + gull() + lamb()
    return out
