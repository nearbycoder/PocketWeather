#!/usr/bin/env python3
"""Writes the twelve Pocket Weather levels to Assets/Resources/Levels/levelNN.json.

Coordinates are Unity world units: x right, z away from the camera (z = -d/2 is the near
edge), the island top sits near y = 0. Run, then rebuild terrain in Blender:

    python3 Tools/make_levels.py
    blender -b -P ArtSource/build_all.py -- --group terrain
"""
import json
import math
import os
import random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Levels")


# ----------------------------------------------------------------------------- helpers

def P(m, x, z, ry=0, s=1.0, **kw):
    d = {"m": m, "x": round(x, 3), "z": round(z, 3), "ry": ry, "s": s}
    d.update(kw)
    return d


def fence_run(x0, z0, x1, z1, model="fence"):
    """Fence segments (1 unit each) from (x0,z0) to (x1,z1)."""
    out = []
    L = math.hypot(x1 - x0, z1 - z0)
    n = max(1, int(round(L)))
    ang = math.degrees(math.atan2(x1 - x0, z1 - z0)) - 90
    for i in range(n):
        t = (i + 0.5) / n
        out.append(P(model, x0 + (x1 - x0) * t, z0 + (z1 - z0) * t, ry=round(ang, 1), s=L / n))
    return out


def trees(spots, seed=0, kinds=("tree_round", "tree_round_b", "tree_fruit")):
    rnd = random.Random(seed)
    return [P(rnd.choice(kinds), x, z, ry=rnd.randint(0, 359), s=round(rnd.uniform(1.1, 1.45), 2)) for (x, z) in spots]


def scatter(model_list, spots, seed=0, smin=0.9, smax=1.3):
    rnd = random.Random(seed)
    return [P(rnd.choice(model_list), x, z, ry=rnd.randint(0, 359), s=round(rnd.uniform(smin, smax), 2)) for (x, z) in spots]


def bed(id, x, z, plant, w=1.6, d=1.0, band=(18, 34), mx=None, **kw):
    n = {"type": "bed", "id": id, "x": x, "z": z, "plant": plant, "w": w, "d": d, "target": list(band), "max": mx or band[1] + 20}
    n.update(kw)
    return n


def shade(id, x, z, model, ry=180, time=4.0, s=1.8, **kw):
    n = {"type": "shade", "id": id, "x": x, "z": z, "model": model, "ry": ry, "time": time, "s": s}
    n.update(kw)
    return n


def level(id, title, story, icon, music, amb, day, start, end, par, water, cloud, island, props, needs,
          sources=(), delight=None, teach="", events=()):
    return {
        "id": id, "title": title, "story": story, "icon": icon, "music": music, "ambience": amb,
        "dayLength": day, "startHour": start, "endHour": end, "par": par, "startWater": water,
        "cloudX": cloud[0], "cloudZ": cloud[1], "teach": teach, "island": island, "props": props,
        "needs": needs, "sources": list(sources), "delight": delight or {}, "events": list(events),
    }


def isl(w=14, d=9, grass="8CCB5E", dryness=0.2, bumps=(), water=(), paths=(), sand=(), sea=None, fields=(), dry_rate=0.0,
        corner=1.7, scatter=1.0):
    d_ = {"w": w, "d": d, "corner": corner, "depth": 1.3, "grass": grass, "dryness": dryness, "dryRate": dry_rate,
          "bumps": list(bumps), "water": list(water), "paths": list(paths), "sand": list(sand), "fields": list(fields),
          "scatter": scatter}
    if sea:
        d_["sea"] = sea
    return d_


def pond(id, x, z, rx, rz, depth=0.5, level=-0.1, **kw):
    w = {"id": id, "shape": "ellipse", "x": x, "z": z, "rx": rx, "rz": rz, "depth": depth, "level": level}
    w.update(kw)
    return w


LEVELS = []

# ============================================================================ 1. First Drops
LEVELS.append(level(
    "level01", "First Drops", "Rosa's flowers are thirsty this morning. Can you give them a drink?",
    "flower", "morning", "meadow", 120, 6.5, 19.5, 11.0, 30, (-4.8, -2.2),
    isl(13, 8.5, dryness=0.25, bumps=[{"x": -4.6, "z": 2.6, "r": 2.8, "h": 0.45}, {"x": 5.0, "z": 3.0, "r": 2.2, "h": 0.3}],
        water=[pond("pond", 3.9, -0.9, 1.9, 1.35)],
        paths=[{"pts": [[-4.3, 1.55], [-3.6, 0.2], [-2.0, -1.9], [-1.4, -4.4]], "w": 0.7, "color": "DDBE8C"}]),
    [P("cottage_red", -4.6, 2.5, 0, 1.15), P("person_rosa", -3.3, 1.55, 200, 1.79), P("mailbox", -2.9, 0.6, 15, 1.2),
     *fence_run(-6.1, 1.1, -5.2, 1.1, "picket"), *fence_run(-2.3, 3.6, 0.2, 3.6, "picket"),
     *trees([(5.6, 3.1), (-6.0, -2.6), (1.8, 3.4)], seed=1),
     P("bush_c", -6.0, 0.3, 30, 1.3), P("bush_a", 0.9, -3.4, 0, 1.2), P("bush_b", 6.0, -2.9, 0, 1.1),
     P("rock_a", 2.1, -2.6, 40, 1.2), P("rock_b", 2.8, -3.2, 10, 1.0), P("rock_c", 5.9, 0.9, 80, 1.1),
     P("reeds_grass", 5.6, -1.6, 0, 1.2, noCollide=True), P("reeds_grass", 2.3, -0.2, 0, 1.0, noCollide=True),
     P("lily_pads", 4.4, -0.6, 30, 1.4, y=-0.09, noCollide=True), P("lily_pads", 3.3, -1.5, 100, 1.1, y=-0.09, noCollide=True),
     P("flower_pot", -5.4, 1.5, 0, 1.1), P("flower_pot", -3.7, 1.6, 0, 1.0)],
    [bed("bedA", -1.4, -0.7, "coral", 1.7, 1.05, (18, 34)),
     bed("bedB", 0.8, 1.4, "butter", 1.7, 1.05, (18, 34)),
     {"type": "react", "id": "snail", "x": 2.45, "z": -2.0, "model": "snail", "ry": 200, "s": 3.07, "trigger": "rain", "amount": 1.2,
      "react": "snail", "sound": "pip_yay", "hidden": True}],
    sources=[{"type": "dew", "x": -0.5, "z": 2.4, "count": 4, "spread": 1.6, "until": 9.5}],
    delight={"type": "rain_on", "target": "snail", "title": "Woke the snail", "hint": "Someone shy is hiding by the stones."},
    teach="move drink"))

# ============================================================================ 2. Just Right
LEVELS.append(level(
    "level02", "Just Right", "The allotment needs a careful hand: enough rain, but not a drop too much.",
    "carrot", "morning", "meadow", 130, 6.5, 19.5, 12.0, 40, (4.0, -2.4),
    isl(14, 9, dryness=0.3, bumps=[{"x": -4.8, "z": 2.8, "r": 2.6, "h": 0.4}],
        water=[pond("trough", 4.9, 1.7, 1.45, 1.05, depth=0.45)],
        fields=[{"shape": "rect", "x": -0.7, "z": -1.25, "w": 8.8, "d": 2.1, "r": 0.5, "color": "9A6B48"}],
        paths=[{"pts": [[-6.8, 0.6], [0.0, 0.5], [4.0, 0.2], [6.8, -0.2]], "w": 0.6, "color": "DDBE8C"}]),
    [P("cottage_yellow", -5.3, 2.7, 0, 1.0), P("scarecrow", 0.6, 2.4, 190, 1.3), P("rain_barrel", 3.3, 2.6, 0, 1.3),
     P("hay_bale", 6.0, -2.6, 30, 1.2), P("hay_bale", 5.5, -3.3, 70, 1.1), P("signpost", -6.2, -0.4, 20, 1.2),
     *fence_run(-5.4, -2.9, 3.9, -2.9, "fence"), *fence_run(-5.4, 0.15, -5.4, -2.9, "fence"),
     *trees([(6.0, 3.4), (-2.8, 3.6)], seed=2), P("bush_a", -6.3, -3.4, 0, 1.2), P("bush_b", 2.0, 3.6, 0, 1.2),
     P("reeds_grass", 6.2, 1.0, 0, 1.1, noCollide=True), P("rock_b", 3.6, 1.0, 0, 1.0), P("person_baker", -4.0, 1.3, 160, 1.73)],
    [bed("carrots", -3.6, -1.25, "carrot", 1.8, 1.1, (16, 26), 50),
     bed("cabbages", -0.7, -1.25, "cabbage", 1.8, 1.1, (20, 30), 50),
     bed("tomatoes", 2.2, -1.25, "tomato", 1.8, 1.1, (14, 24), 50),
     bed("pumpkin", -4.7, 1.0, "pumpkin", 1.2, 1.0, (55, 80), 90, anim="single", s=1.3, hidden=True)],
    sources=[{"type": "dew", "x": -1.0, "z": 1.9, "count": 6, "spread": 2.2, "until": 9.0}],
    delight={"type": "fill", "target": "pumpkin", "title": "Grew a prize pumpkin", "hint": "Pocketvale's biggest vegetable is very, very thirsty."},
    teach="band"))

# ============================================================================ 3. Sunny Pasture
LEVELS.append(level(
    "level03", "Sunny Pasture", "It's hot on the hill. The sheep would love a little shade.",
    "sheep", "morning", "meadow", 130, 7.0, 19.5, 12.0, 60, (-3.5, -2.4),
    isl(14, 9, dryness=0.45, grass="9CCB5A", bumps=[{"x": 2.5, "z": 1.8, "r": 3.5, "h": 0.6}, {"x": -5.2, "z": 3.0, "r": 2.0, "h": 0.3}],
        water=[pond("stream", -4.6, -2.1, 1.7, 1.05, depth=0.45)],
        paths=[{"pts": [[-1.4, -4.5], [-0.6, -1.5], [-1.8, 1.2], [-1.2, 4.5]], "w": 0.55, "color": "D9BC8A"}]),
    [P("barn", 5.3, 3.1, 0, 1.0), *fence_run(-0.2, -1.2, 6.4, -1.2, "fence"), *fence_run(-0.2, -1.2, -0.2, 3.8, "fence"),
     P("hay_bale", 6.1, 0.3, 20, 1.2), P("hay_bale", 6.2, -0.5, 80, 1.1),
     *trees([(-5.9, 1.2), (-3.4, 3.7), (6.0, -3.2)], seed=3), P("bush_b", -6.2, -3.6, 0, 1.2), P("bush_a", 1.6, -3.6, 0, 1.1),
     P("rock_a", -2.9, -2.9, 0, 1.0), P("reeds_grass", -3.0, -1.5, 0, 1.0, noCollide=True), P("rock_c", 3.4, -3.3, 30, 1.2)],
    [shade("sheep1", 1.0, 1.0, "sheep", 200), shade("sheep2", 2.9, 0.1, "sheep", 150), shade("sheep3", 3.9, 2.0, "sheep", 230),
     bed("sapling", -2.6, 2.2, "sapling", 1.0, 1.0, (24, 40), 60, s=1.3),
     {"type": "react", "id": "lamb", "x": 4.6, "z": -2.6, "model": "lamb", "ry": 160, "s": 2.05, "trigger": "rain", "amount": 2.0,
      "react": "splash", "sound": "sheep", "hidden": True}],
    delight={"type": "rain_on", "target": "lamb", "title": "A puddle for the lamb", "hint": "The littlest one has never splashed in a puddle."},
    teach="shade"))

# ============================================================================ 4. Becalmed
LEVELS.append(level(
    "level04", "Becalmed", "Not a breath of wind in the harbour, and Tom's boat is stuck out at sea.",
    "boat", "morning", "sea", 140, 7.0, 19.5, 13.0, 50, (-3.0, 0.5),
    isl(14, 9, grass="8CCB5E", bumps=[{"x": -4.0, "z": 3.0, "r": 2.6, "h": 0.5}, {"x": 3.5, "z": 3.4, "r": 2.2, "h": 0.4}],
        sea={"side": "south", "width": 3.7, "level": -0.12, "depth": 0.75, "beach": 1.4},
        paths=[{"pts": [[-6.8, 1.0], [-2.0, 1.0], [2.5, 0.9], [3.6, -0.6]], "w": 0.6, "color": "D9C49A"}]),
    [P("cottage_blue", -3.6, 2.7, 0, 1.1), P("cottage_yellow", 0.9, 3.1, 0, 1.0), P("dock", 3.7, -2.1, 0, 1.0, y=-0.05),
     P("lamp_post", 2.9, -0.3, 0, 1.2), P("lamp_post", -1.0, 0.4, 0, 1.2), P("rowboat", -5.5, -0.9, 60, 1.2),
     P("person_tom", 3.5, -0.4, 180, 1.79), P("gull", 4.2, -0.9, 220, 2.05), P("gull", -6.2, -0.4, 120, 1.92),
     *trees([(-6.0, 3.0), (5.9, 3.0)], seed=4, kinds=("tree_pine", "tree_round")), P("bush_a", -1.6, 3.6, 0, 1.1),
     P("rock_big", 6.1, -1.0, 30, 1.0), P("crab", -3.6, -0.8, 30, 2.56), P("signpost", -6.3, 1.5, 0, 1.1)],
    [{"type": "boat", "id": "boat", "model": "sailboat", "x": -4.2, "z": -3.3, "ry": 90, "s": 1.3, "water": "sea",
      "goal": [2.85, -3.25], "goalR": 0.7},
     bed("box1", -3.6, 1.8, "pink", 0.9, 0.3, (8, 16), 26, model="window_box"),
     bed("box2", 0.9, 2.3, "butter", 0.9, 0.3, (8, 16), 26, model="window_box"),
     {"type": "react", "id": "buoy", "x": -0.4, "z": -3.75, "model": "buoy", "ry": 0, "s": 1.3, "trigger": "gust", "amount": 0.6,
      "react": "bell", "sound": "bell", "hidden": True, "y": -0.12}],
    delight={"type": "gust_on", "target": "buoy", "title": "Rang the buoy bell", "hint": "Something out at sea would love to make a little music."},
    teach="gust"))

# ============================================================================ 5. Washing Day
LEVELS.append(level(
    "level05", "Washing Day", "The washing's on the line, the veg is thirsty, and the dog is far too hot.",
    "shirt", "afternoon", "village", 150, 8.0, 19.5, 13.0, 40, (-1.0, -2.6),
    isl(14, 9, dryness=0.3, bumps=[{"x": -4.5, "z": 3.0, "r": 2.5, "h": 0.4}],
        water=[pond("well", 4.9, -2.0, 1.4, 1.0, depth=0.45)],
        paths=[{"pts": [[-4.2, 1.6], [-3.6, -0.5], [-2.0, -4.5]], "w": 0.6, "color": "DDBE8C"}]),
    [P("cottage_red", -4.3, 2.7, 0, 1.15), P("well", 3.6, -1.1, 0, 1.2), *fence_run(-6.2, -0.6, -4.6, -0.6, "picket"),
     *fence_run(-2.4, 3.9, 4.0, 3.9, "picket"), *trees([(6.0, 3.0), (-6.1, -3.2)], seed=5), P("bush_c", 6.2, 0.6, 0, 1.2),
     P("flower_pot", -3.4, 1.6, 0, 1.0), P("cat", -5.5, 1.5, 160, 2.56), P("mailbox", -2.5, -1.2, 0, 1.2), P("bush_a", 1.2, -3.6, 0, 1.0)],
    [{"type": "laundry", "id": "laundry1", "x": -0.6, "z": 2.0, "ry": 0, "s": 1.25, "start": 1},
     {"type": "laundry", "id": "laundry2", "x": 3.0, "z": 2.6, "ry": -10, "s": 1.25, "start": 1},
     bed("veg", 0.7, -1.3, "cabbage", 1.8, 1.1, (18, 30), 50),
     shade("dog", 5.1, 0.7, "dog", 210, time=4.0, s=2.18),
     {"type": "react", "id": "kid", "x": -2.0, "z": -2.6, "model": "person_kid_rain", "ry": 190, "s": 1.79, "trigger": "rain", "amount": 1.5,
      "react": "splash", "sound": "person_happy", "hidden": True}],
    sources=[{"type": "steam", "x": -3.82, "z": 2.88, "y": 2.0, "rate": 0.35, "spread": 0.2}],
    delight={"type": "rain_on", "target": "kid", "title": "Puddle jumping!", "hint": "Someone in yellow boots is hoping for puddles."},
    teach=""))

# ============================================================================ 6. Rainbow Picnic
LEVELS.append(level(
    "level06", "Rainbow Picnic", "Rosa and Tom are having a picnic, and a little one dearly wants to see a rainbow.",
    "rainbow", "afternoon", "meadow", 150, 9.0, 19.5, 14.0, 50, (-4.5, -2.5),
    isl(14, 9, dryness=0.25, bumps=[{"x": 2.4, "z": 1.2, "r": 3.2, "h": 0.65}],
        water=[pond("brook", 4.9, -2.5, 1.5, 1.0, depth=0.45)],
        paths=[{"pts": [[-6.8, -1.6], [-3.5, -0.4], [-0.6, 0.0], [2.0, -0.2]], "w": 0.55, "color": "DDBE8C"}]),
    [P("picnic", 2.4, 1.3, 15, 1.3), P("person_tom", 2.85, 1.55, 200, 1.73), P("picnic", -0.2, 2.5, -10, 1.2),
     *trees([(-5.8, 2.9), (5.9, 3.1), (-2.6, 3.7), (6.1, 0.6)], seed=6), P("bush_c", -6.2, 0.4, 0, 1.2),
     P("bush_a", 0.4, -3.6, 0, 1.2), P("rock_a", 3.5, -3.4, 0, 1.1), P("reeds_grass", 6.2, -1.6, 0, 1.0, noCollide=True)],
    [{"type": "rainbow", "id": "kid", "x": -2.3, "z": -1.1, "model": "person_kid", "ry": 200, "s": 1.79},
     shade("mum", -0.6, 2.2, "person_guest_c", 190, time=3.5, dislike="umbrella"),
     shade("grandad", 0.3, 2.3, "person_old", 170, time=3.5, dislike="umbrella"),
     bed("wildflowers", -4.6, 1.4, "lilac", 1.8, 1.1, (18, 32), 50),
     {"type": "react", "id": "rosa", "x": 2.15, "z": 1.35, "model": "person_rosa", "ry": 160, "s": 1.73, "trigger": "rainbow",
      "react": "cheer", "sound": "person_happy", "hidden": True}],
    delight={"type": "rainbow_on", "target": "rosa", "title": "A rainbow for Rosa and Tom", "hint": "Two sweethearts on the hill would love one too."},
    teach="rainbow"))

# ============================================================================ 7. Windmill Hill
LEVELS.append(level(
    "level07", "Windmill Hill", "The miller needs flour for the wedding cake, but the sails won't turn.",
    "windmill", "afternoon", "meadow", 160, 8.0, 19.5, 14.0, 50, (-1.5, -2.5),
    isl(14, 9, dryness=0.35, bumps=[{"x": 3.8, "z": 2.4, "r": 3.0, "h": 0.8}],
        water=[pond("pond", 5.0, -2.5, 1.4, 1.0, depth=0.45)],
        fields=[{"shape": "rect", "x": -2.6, "z": -0.9, "w": 3.6, "d": 2.1, "r": 0.4, "color": "A07448"}],
        paths=[{"pts": [[-6.8, 1.4], [-1.0, 1.6], [2.4, 1.6], [3.4, 2.0]], "w": 0.6, "color": "DDBE8C"}]),
    [P("person_miller", 2.5, 1.0, 200, 1.79), P("hay_bale", 0.6, -3.3, 20, 1.2), P("hay_bale", 1.4, -3.5, 60, 1.1),
     *fence_run(-4.8, -2.4, -0.4, -2.4, "fence"), *trees([(-6.0, 3.0), (-3.0, 3.6), (6.1, -0.4)], seed=7, kinds=("tree_round", "tree_pine")),
     P("bush_b", -6.3, -3.4, 0, 1.2), P("rock_c", 3.2, -3.4, 0, 1.0), P("cottage_yellow", 0.9, 3.4, 0, 0.95)],
    [{"type": "windmill", "id": "windmill", "x": 4.0, "z": 2.5, "ry": 0, "s": 1.15, "time": 3},
     bed("wheat", -2.6, -0.9, "wheat", 3.2, 1.7, (34, 52), 70),
     shade("donkey", 1.2, 0.0, "donkey", 210, time=4.0, s=2.05),
     {"type": "react", "id": "kite", "x": -5.0, "z": 1.0, "model": "person_kid_b", "ry": 160, "s": 1.79, "trigger": "gust", "amount": 0.6,
      "react": "kite", "sound": "person_happy", "hidden": True}],
    sources=[{"type": "dew", "x": -1.0, "z": 2.4, "count": 6, "spread": 2.0, "until": 10.0}],
    delight={"type": "gust_on", "target": "kite", "title": "The kite flies!", "hint": "A kite is waiting for a puff of wind."},
    teach=""))

# ============================================================================ 8. Duck Pond Park
LEVELS.append(level(
    "level08", "Duck Pond Park", "The park's flowers need water, but the ducks need their pond. Share carefully.",
    "duck", "afternoon", "village", 170, 8.0, 19.5, 15.0, 40, (-4.0, -2.0),
    isl(14, 9, dryness=0.35, bumps=[],
        water=[pond("duckpond", 0.6, 0.3, 2.4, 1.55, depth=0.55, finite=True, capacity=140)],
        paths=[{"pts": [[-6.8, -0.6], [-2.7, -1.0], [-2.4, 2.6], [3.4, 2.6], [3.6, -1.9], [-2.6, -1.9]], "w": 0.6, "color": "E2CDA6"}]),
    [P("fountain", -4.7, -2.3, 0, 1.0), P("bench", 4.5, 2.95, 180, 1.4), P("lamp_post", -3.2, 2.0, 0, 1.2),
     P("lamp_post", 4.4, -2.6, 0, 1.2), P("hedge_long", -0.5, 3.9, 0, 1.0), P("hedge_long", 3.0, 3.9, 0, 1.0),
     *trees([(-6.0, 2.9), (6.1, 3.0), (6.2, -0.4), (-6.2, 0.5)], seed=8, kinds=("tree_round", "tree_round_b")),
     P("birdbath", -2.1, -3.0, 0, 1.2), P("flower_pot", 3.2, 3.3, 0, 1.1), P("flower_pot", 5.7, 2.6, 0, 1.0),
     P("reeds_grass", 2.7, -0.9, 0, 1.0, noCollide=True), P("lily_pads", -0.6, 0.8, 0, 1.3, y=-0.09, noCollide=True)],
    [bed("bedA", -4.6, 1.5, "pink", 2.0, 1.1, (24, 38), 56),
     bed("bedB", 4.7, -1.0, "white", 1.8, 1.1, (24, 38), 56),
     shade("oldman", 4.5, 2.7, "person_old", 180, time=4.5),
     {"type": "pondline", "id": "ducks", "x": 0.6, "z": 0.3, "water": "duckpond", "target": [0.42], "time": 3},
     {"type": "react", "id": "robin", "x": -2.1, "z": -3.0, "model": "robin", "ry": 180, "s": 2.82, "yOff": 0.5, "trigger": "rain",
      "amount": 1.5, "react": "splash", "sound": "robin", "hidden": True}],
    sources=[{"type": "fountain", "x": -4.7, "z": -2.3, "y": 1.1, "rate": 0.7, "spread": 0.6}],
    delight={"type": "rain_on", "target": "robin", "title": "Bath time for the robin", "hint": "The birdbath is bone dry."},
    teach="pond"))

# ============================================================================ 9. Campfire Night
LEVELS.append(level(
    "level09", "Campfire Night", "A spark has jumped into the hay! Put out the fire, but keep the campfire going.",
    "campfire", "night", "night", 160, 17.5, 22.0, 19.5, 60, (-1.0, -1.5),
    isl(14, 9, grass="7FBF5A", dryness=0.3, bumps=[{"x": -4.6, "z": -2.6, "r": 2.2, "h": 0.35}],
        sea={"side": "north", "width": 2.6, "level": -0.12, "depth": 0.7, "beach": 1.0}),
    [P("tent", -1.4, 0.9, 20, 1.3), P("tent", 1.6, 1.1, -25, 1.1), P("person_camper_a", -0.2, -0.3, 120, 1.73),
     P("person_camper_b", 1.0, -0.4, 230, 1.73), P("hay_bale", 4.9, -2.8, 0, 1.2),
     *trees([(-6.0, 1.0), (-5.6, -3.4), (6.1, 0.4), (2.4, -3.6), (-3.2, -3.7)], seed=9, kinds=("tree_pine", "tree_pine_small")),
     P("rowboat", -3.8, 2.6, 80, 1.2, y=-0.1), P("rock_a", 5.8, -1.0, 0, 1.2), P("lamp_post", -2.8, -0.2, 0, 1.2),
     P("reeds_grass", 3.4, 1.9, 0, 1.0, noCollide=True)],
    [{"type": "fire", "id": "hay1", "x": 4.2, "z": -1.2, "model": "haystack", "s": 1.2, "start": 1, "links": ["bush1"]},
     {"type": "fire", "id": "bush1", "x": 5.6, "z": 0.9, "model": "bush_b", "s": 1.5, "start": 0, "links": []},
     {"type": "fire", "id": "hay2", "x": -4.2, "z": -1.8, "model": "haystack", "s": 1.1, "start": 0, "links": []},
     {"type": "campfire", "id": "campfire", "x": 0.4, "z": -1.1},
     bed("herbs", -3.6, 1.0, "tomato", 1.6, 1.0, (16, 28), 46)],
    events=[{"type": "ignite", "hour": 18.6, "target": "hay2"}],
    delight={"type": "gust_on", "target": "campfire", "title": "A roaring campfire", "hint": "The campers would love a bigger blaze."},
    teach="fire"))

# ============================================================================ 10. Regatta
LEVELS.append(level(
    "level10", "Regatta", "Race day at the beach! Blow the boats to their buoys, but mind the sandcastle.",
    "castle", "afternoon", "sea", 170, 9.0, 19.5, 15.0, 60, (0.0, -0.5),
    isl(14, 9, grass="8CCB5E", bumps=[{"x": -3.6, "z": 3.4, "r": 2.6, "h": 0.4}],
        sea={"side": "south", "width": 4.2, "level": -0.12, "depth": 0.8, "beach": 2.2}),
    [P("beach_umbrella", 2.1, 0.5, 0, 1.3), P("beach_umbrella_b", 4.4, 1.0, 0, 1.2), P("towel", 2.5, 0.0, 10, 1.3),
     P("towel_b", 4.0, 0.5, -15, 1.3), P("icecream_cart", -4.8, 1.0, 20, 1.2), P("person_kid", -1.1, -0.6, 200, 1.73),
     P("cottage_blue", -3.2, 3.2, 0, 1.0), P("cottage_red", 2.6, 3.4, 0, 0.95), P("crab", 0.9, -1.4, 40, 2.56),
     P("gull", -5.6, -1.0, 90, 1.92), P("rock_big", 6.0, -0.9, 0, 1.0), *trees([(-6.2, 3.0), (6.0, 3.1)], seed=10, kinds=("tree_pine",)),
     P("lamp_post", 0.2, 2.2, 0, 1.2), {"m": "buoy", "x": -1.3, "z": -3.7, "ry": 0, "s": 1.2, "y": -0.12, "noCollide": True},
     {"m": "buoy", "x": 1.9, "z": -3.75, "ry": 0, "s": 1.2, "y": -0.12, "noCollide": True}],
    [{"type": "boat", "id": "boatA", "model": "sailboat", "x": -5.3, "z": -3.3, "ry": 90, "s": 1.25, "water": "sea", "goal": [-1.9, -3.4], "goalR": 0.65},
     {"type": "boat", "id": "boatB", "model": "sailboat", "x": 5.3, "z": -3.2, "ry": 270, "s": 1.25, "water": "sea", "goal": [2.5, -3.4], "goalR": 0.65},
     {"type": "keepdry", "id": "castle", "x": -1.9, "z": -0.9, "model": "sandcastle", "s": 1.4, "time": 7},
     shade("bather1", 2.5, 0.0, "person_sunbather_a", 0, time=4.0),
     shade("bather2", 4.0, 0.5, "person_sunbather_b", 0, time=4.0),
     {"type": "react", "id": "seal", "x": 5.2, "z": -1.6, "model": "seal", "ry": 200, "s": 2.18, "trigger": "rain", "amount": 2.0,
      "react": "cheer", "sound": "seal", "hidden": True}],
    delight={"type": "rain_on", "target": "seal", "title": "A shower for the seal", "hint": "A sunbather on the rocks is feeling dry."},
    teach=""))

# ============================================================================ 11. Heatwave Farm
LEVELS.append(level(
    "level11", "Heatwave", "The hottest day of the year. Everything dries out fast, so keep moving!",
    "sunflower", "afternoon", "meadow", 180, 8.0, 19.5, 15.5, 50, (-0.5, -2.6),
    isl(14, 9, grass="B9C46A", dryness=0.8, dry_rate=0.55, bumps=[{"x": 4.2, "z": 2.8, "r": 2.6, "h": 0.4}],
        water=[pond("trough", -0.4, 2.6, 1.25, 0.8, depth=0.4)],
        fields=[{"shape": "rect", "x": -3.3, "z": -1.3, "w": 2.6, "d": 1.8, "r": 0.4, "color": "A07448"}],
        paths=[{"pts": [[-6.8, 0.6], [-1.6, 0.8], [3.0, 0.4], [6.8, 0.6]], "w": 0.6, "color": "DCC08E"}]),
    [P("barn", 4.6, 3.2, 0, 1.0), P("pig", 0.3, -1.0, 140, 2.3), P("scarecrow", -1.7, -3.2, 180, 1.2),
     *fence_run(1.6, 1.0, 6.3, 1.0, "fence"), *fence_run(1.6, 1.0, 1.6, -0.2, "fence"), P("hay_bale", -6.0, -3.2, 20, 1.2),
     P("windmill", -5.3, 3.0, 0, 0.9), P("rock_a", 6.0, -3.2, 0, 1.1), P("tree_round", -2.9, 3.7, 50, 1.2)],
    [bed("pumpkins", -3.3, -1.3, "pumpkin", 2.2, 1.4, (20, 38), 56),
     bed("wallow", 1.1, -1.7, "mud", 1.4, 1.0, (16, 32), 48),
     shade("cow1", 2.8, 2.1, "cow", 200, time=4.5, s=1.73),
     shade("cow2", 4.6, 1.7, "cow", 160, time=4.5, s=1.73),
     {"type": "sunny", "id": "sunflowers", "x": -5.2, "z": 1.4, "plant": "sunflower", "w": 1.4, "d": 0.9, "target": [14, 28], "max": 46, "s": 1.3},
     {"type": "react", "id": "icecream", "x": 5.4, "z": -2.6, "model": "icecream_cart", "ry": 200, "s": 1.2, "trigger": "shade", "amount": 2.5,
      "react": "cheer", "sound": "person_happy", "hidden": True}],
    sources=[{"type": "dew", "x": 0.0, "z": -0.2, "count": 7, "spread": 2.6, "until": 9.0}],
    delight={"type": "shade_on", "target": "icecream", "title": "Saved the ice cream", "hint": "Something sweet is melting in the sun."},
    teach="sunny"))

# ============================================================================ 12. The Wedding
LEVELS.append(level(
    "level12", "The Wedding", "Rosa and Tom are getting married by the lake! Make it perfect.",
    "cake", "wedding", "village", 200, 13.0, 20.0, 18.0, 60, (-1.0, -2.6),
    isl(14, 9, grass="8CCB5E", dryness=0.2, bumps=[{"x": -4.6, "z": 2.0, "r": 2.2, "h": 0.35}],
        sea={"side": "north", "width": 2.4, "level": -0.12, "depth": 0.7, "beach": 0.9},
        paths=[{"pts": [[0.2, -4.5], [0.2, -2.2], [0.2, 0.9]], "w": 0.8, "color": "F2E6D8"}]),
    [P("chapel", -4.7, 1.6, 0, 1.0), P("dock", 4.2, 2.9, 0, 0.9, y=-0.05), P("person_musician", 4.6, 1.9, 200, 1.73),
     *[P("chair", x, z, 180, 1.3) for (x, z) in ((-2.2, -1.6), (-1.3, -2.0), (1.7, -1.6), (2.6, -2.0), (-2.4, -2.6), (2.8, -2.8))],
     P("person_guest_d", 2.6, -2.0, 190, 1.73), P("person_kid_b", -2.4, -2.6, 160, 1.54),
     *trees([(-6.1, -2.6), (6.1, -2.4), (6.2, 0.4)], seed=12, kinds=("tree_round", "tree_fruit")),
     P("hedge", -3.6, -3.7, 0, 1.0), P("hedge", 3.6, -3.7, 0, 1.0), P("flower_pot", -1.0, 1.0, 0, 1.1), P("flower_pot", 1.4, 1.0, 0, 1.1),
     P("lamp_post", -2.4, 0.6, 0, 1.2), P("lamp_post", 2.8, 0.6, 0, 1.2)],
    [bed("arch", 0.2, 1.05, "pink", 1.4, 0.4, (18, 30), 46, model="wedding_arch"),
     {"type": "rainbow", "id": "couple", "x": 0.05, "z": 0.45, "model": "person_bride", "ry": 180, "s": 1.79, "links": ["person_groom"], "dormant": True},
     shade("guest1", -2.2, -1.6, "person_guest_a", 180, time=3.5, dislike="umbrella"),
     shade("guest2", -1.3, -2.0, "person_guest_b", 180, time=3.5, dislike="umbrella"),
     shade("guest3", 1.7, -1.6, "person_guest_c", 180, time=3.5, dislike="umbrella"),
     {"type": "keepdry", "id": "cake", "x": 4.6, "z": -0.6, "model": "cake_table", "ry": 0, "s": 1.3, "time": 8},
     {"type": "boat", "id": "boat", "model": "sailboat", "x": -3.2, "z": 3.7, "ry": 90, "s": 1.2, "water": "sea", "goal": [3.5, 3.6], "goalR": 0.7}],
    delight={"type": "catch", "target": "bouquet", "title": "Caught the bouquet!", "hint": "When the bride throws her flowers, be there."},
    teach=""))


THANKS = {'level01': "Rosa's garden is blooming!", 'level02': 'The allotment is just right.', 'level03': 'The sheep are cool and comfy.', 'level04': "Tom's boat is safely home.", 'level05': 'Dry washing, happy veg, sleepy dog.', 'level06': 'A rainbow for a little one.', 'level07': 'The sails are turning: flour for the cake!', 'level08': 'Flowers, ducks and a happy old friend.', 'level09': 'The fires are out and the campfire crackles.', 'level10': 'Every boat home, every castle standing.', 'level11': 'Pocketvale made it through the heat.', 'level12': 'Married under a rainbow!'}


def main():
    os.makedirs(OUT, exist_ok=True)
    for lv in LEVELS:
        lv["thanks"] = THANKS.get(lv["id"], "")
        path = os.path.join(OUT, lv["id"] + ".json")
        with open(path, "w") as f:
            json.dump(lv, f, indent=1)
        print("wrote", os.path.relpath(path, ROOT), "-", lv["title"], f"({len(lv['needs'])} needs, {len(lv['props'])} props)")


main()
