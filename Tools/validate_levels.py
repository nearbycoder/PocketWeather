#!/usr/bin/env python3
"""Static checks for every level JSON (complements the in-game AutoPilot, which actually plays them).

    python3 Tools/validate_levels.py

For each level it checks that every model/icon referenced exists, that needs sit inside Pip's
reachable area and on land (boats and their goals on their water), that bed bands are sane, and
it estimates a water budget and a lower-bound completion time (travel + raining + drinking +
gusting + shading) against the par hour and sundown. Exits non-zero on any error.
"""
import glob
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVELS = os.path.join(ROOT, "Assets", "Resources", "Levels")
MODELS = os.path.join(ROOT, "Assets", "Resources", "Models")

RAIN_RATE, DRINK_RATE, GUST_COST, SPEED = 14.0, 32.0, 6.0, 7.0   # Pip's numbers (speed: comfortable cruising)
MAX_WATER = 100.0
CROPS = {"carrot", "cabbage", "tomato", "wheat", "pumpkin", "sunflower"}


def model_exists(name):
    return os.path.exists(os.path.join(MODELS, name + ".fbx"))


def in_ellipse(w, x, z, pad=0.0):
    return ((x - w["x"]) / (w["rx"] + pad)) ** 2 + ((z - w["z"]) / (w["rz"] + pad)) ** 2 <= 1.0


def on_sea(lv, x, z):
    sea = lv["island"].get("sea")
    if not sea:
        return False
    d, w = lv["island"]["d"], lv["island"]["w"]
    width = sea["width"] - 0.3
    return {"south": z < -d / 2 + width, "north": z > d / 2 - width,
            "east": x > w / 2 - width, "west": x < -w / 2 + width}[sea.get("side", "south")]


def check(path):
    lv = json.load(open(path))
    errs, warns = [], []
    isl = lv["island"]
    hw, hd = isl["w"] / 2 - 0.35, isl["d"] / 2 - 0.35
    waters = {w["id"]: w for w in isl.get("water", [])}
    infinite = any(not w.get("finite") for w in isl.get("water", [])) or bool(isl.get("sea"))

    for p in lv["props"]:
        if not model_exists(p["m"]):
            errs.append(f"missing prop model {p['m']}")
    if not os.path.exists(os.path.join(MODELS, "Terrain", f"terrain_{lv['id']}.fbx")):
        errs.append("terrain FBX not built")

    water_needed = 0.0
    time_needed = 0.0
    pos = (lv.get("cloudX", 0), lv.get("cloudZ", 0))
    required = [n for n in lv["needs"] if not n.get("hidden")]
    for n in lv["needs"]:
        x, z = n["x"], n["z"]
        t = n["type"]
        if abs(x) > hw + 0.3 or abs(z) > hd + 0.3:
            errs.append(f"{n['id']}: outside Pip's reach ({x}, {z})")
        model = n.get("model", "")
        if model and not model_exists(model):
            errs.append(f"{n['id']}: missing model {model}")
        if t in ("bed", "sunny"):
            lo, hi = n["target"][0], n["target"][1] if len(n["target"]) > 1 else n["target"][0] * 1.8
            if hi - lo < 6:
                warns.append(f"{n['id']}: band only {hi - lo} wide (needs precise taps)")
            if hi > n.get("max", hi + 20):
                errs.append(f"{n['id']}: band above max")
            plant = n.get("plant", "coral")
            pm = ("crop_" + plant) if plant in CROPS else ("flower_" + plant) if plant not in ("mud", "sapling") else None
            if pm and not model_exists(pm):
                errs.append(f"{n['id']}: missing plant model {pm}")
            if not n.get("hidden"):
                water_needed += (lo + hi) / 2
                time_needed += (lo + hi) / 2 / RAIN_RATE
        elif t == "boat":
            wid = n.get("water", "")
            goal = n["goal"]
            if wid == "sea":
                if not on_sea(lv, x, z) or not on_sea(lv, goal[0], goal[1]):
                    errs.append(f"{n['id']}: boat or goal not on the sea")
            elif wid in waters:
                if not in_ellipse(waters[wid], x, z) or not in_ellipse(waters[wid], goal[0], goal[1]):
                    errs.append(f"{n['id']}: boat or goal not on {wid}")
            dist = math.hypot(goal[0] - x, goal[1] - z)
            gusts = math.ceil(dist / 2.6)
            water_needed += gusts * GUST_COST
            time_needed += gusts * 1.6
        elif t == "laundry":
            water_needed += 3 * GUST_COST
            time_needed += 3 * 1.2
        elif t == "windmill":
            g = math.ceil(n.get("time", 3) / 0.9)
            water_needed += g * GUST_COST
            time_needed += g * 1.2
        elif t == "shade":
            time_needed += n.get("time", 4)
        elif t == "fire" and n.get("start", 0) > 0:
            water_needed += 1.0 / 0.045 + 6
            time_needed += 2.2
        elif t == "rainbow" and not n.get("dormant"):
            water_needed += 1.6 * RAIN_RATE
            time_needed += 1.6 + 2.5
        if not n.get("hidden"):
            time_needed += math.hypot(x - pos[0], z - pos[1]) / SPEED
            pos = (x, z)

    for e in lv.get("events", []):
        if e["type"] == "ignite":
            water_needed += 1.0 / 0.045 + 6
            time_needed += 2.5

    start = lv.get("startWater", 40)
    deficit = max(0.0, water_needed - start)
    refills = math.ceil(deficit / 80.0)
    time_needed += deficit / DRINK_RATE + refills * 2 * (isl["w"] * 0.3) / SPEED
    if not infinite:
        finite_total = sum(w.get("capacity", 100) * (1 - 0.45) for w in isl.get("water", []))
        motes = sum(s.get("count", 1) for s in lv.get("sources", []) if s["type"] == "dew") * 6
        motes += sum(s.get("rate", 1) * lv["dayLength"] * 0.5 for s in lv.get("sources", []) if s["type"] in ("fountain", "steam")) * 6
        if start + finite_total + motes < water_needed:
            errs.append(f"not enough water: need ~{water_needed:.0f}, available ~{start + finite_total + motes:.0f}")
    hours = lv["endHour"] - lv["startHour"]
    sec_per_hour = lv["dayLength"] / hours
    par_seconds = (lv["par"] - lv["startHour"]) * sec_per_hour
    if time_needed > lv["dayLength"] * 0.8:
        errs.append(f"estimated {time_needed:.0f}s exceeds 80% of the day ({lv['dayLength']}s)")
    if time_needed > par_seconds:
        errs.append(f"estimated {time_needed:.0f}s exceeds par ({par_seconds:.0f}s)")
    elif time_needed > par_seconds * 0.85:
        warns.append(f"par is tight: estimate {time_needed:.0f}s vs par {par_seconds:.0f}s")
    if not lv.get("delight", {}).get("type"):
        warns.append("no delight")
    return lv, errs, warns, water_needed, time_needed, par_seconds


def main():
    bad = 0
    print(f"{'level':8} {'title':16} {'needs':>5} {'water':>6} {'est s':>6} {'par s':>6} {'day s':>6}  status")
    for path in sorted(glob.glob(os.path.join(LEVELS, "level*.json"))):
        lv, errs, warns, water, est, par = check(path)
        req = len([n for n in lv["needs"] if not n.get("hidden")])
        status = "OK" if not errs else "ERROR"
        print(f"{lv['id']:8} {lv['title'][:16]:16} {req:5d} {water:6.0f} {est:6.0f} {par:6.0f} {lv['dayLength']:6.0f}  {status}")
        for e in errs:
            print("   error:", e)
        for w in warns:
            print("   note: ", w)
        bad += len(errs)
    print("all levels pass static checks" if bad == 0 else f"{bad} errors")
    sys.exit(1 if bad else 0)


main()
