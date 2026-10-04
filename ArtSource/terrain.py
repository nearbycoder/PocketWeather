"""Builds one diorama island per level JSON (Assets/Resources/Levels/*.json).

Each terrain FBX holds:
  Ground        top surface (vertex colours: grass alpha=1 / path, sand, mud alpha=0),
                strata side walls with a grass lip, and a bottom cap
  Water_<id>    one surface mesh per water body at its level (ponds are polar grids)
  Water_sea     (if the level has a sea) a surface over the whole footprint plus a glassy
                skirt down the island side where the sea floor meets the edge

Heights come from the JSON: base + bumps (cosine bells), carved water basins with shelving
shores, slightly sunken paths, sand and beach zones.
"""
import glob
import json
import math
import os

from mathutils import noise, Vector

from pw_lib import ROOT, Model, hexcol, mix, tint

LEVELS_DIR = os.path.join(ROOT, "Assets", "Resources", "Levels")

STRATA = [  # (top y, colour) from the surface down
    (0.0, "6E4630"),
    (-0.32, "9A6440"),
    (-0.62, "C08A5A"),
    (-0.86, "8F7B8E"),
    (-1.15, "6F6680"),
]
LIP = 0.07


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def sd_shape(s, x, z):
    """Signed distance (approx) to a water/sand shape: negative inside."""
    shape = s.get("shape", "ellipse")
    cx, cz = s.get("x", 0.0), s.get("z", 0.0)
    if shape == "ellipse":
        rx, rz = s["rx"], s["rz"]
        dx, dz = (x - cx) / rx, (z - cz) / rz
        k = math.sqrt(dx * dx + dz * dz)
        return (k - 1.0) * min(rx, rz) if k > 1e-6 else -min(rx, rz)
    if shape == "rect":
        hx, hz = s["w"] / 2, s["d"] / 2
        r = s.get("r", 0.3)
        qx, qz = abs(x - cx) - hx + r, abs(z - cz) - hz + r
        out = math.hypot(max(qx, 0), max(qz, 0))
        return out + min(max(qx, qz), 0) - r
    if shape == "river":
        # polyline with width
        pts = s["pts"]
        best = 1e9
        for (ax, az), (bx, bz) in zip(pts, pts[1:]):
            vx, vz = bx - ax, bz - az
            L2 = vx * vx + vz * vz
            t = max(0.0, min(1.0, ((x - ax) * vx + (z - az) * vz) / L2)) if L2 > 0 else 0
            px, pz = ax + vx * t, az + vz * t
            best = min(best, math.hypot(x - px, z - pz))
        return best - s["w"] / 2
    return 1e9


def seg_dist(pts, x, z):
    best = 1e9
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        vx, vz = bx - ax, bz - az
        L2 = vx * vx + vz * vz
        t = max(0.0, min(1.0, ((x - ax) * vx + (z - az) * vz) / L2)) if L2 > 0 else 0
        best = min(best, math.hypot(x - (ax + vx * t), z - (az + vz * t)))
    return best


class Island:
    def __init__(self, level):
        isl = level["island"]
        self.lid = level["id"]
        self.w = isl.get("w", 14.0)
        self.d = isl.get("d", 9.0)
        self.corner = isl.get("corner", 1.6)
        self.depth = isl.get("depth", 1.3)
        self.base = isl.get("base", 0.0)
        self.grass = isl.get("grass", "8CCB5E")
        self.bumps = isl.get("bumps", [])
        self.water = isl.get("water", [])
        self.paths = isl.get("paths", [])
        self.sand = isl.get("sand", [])
        self.sea = isl.get("sea")
        self.fields = isl.get("fields", [])  # soil patches (ploughed/allotment), colour + alpha 0
        self.seed = sum(ord(c) for c in self.lid)

    # ---- height & colour fields ------------------------------------------------------
    def sea_sd(self, x, z):
        """Signed distance to the sea region (negative = sea)."""
        if not self.sea:
            return 1e9
        side = self.sea.get("side", "south")
        width = self.sea.get("width", 3.0)
        wob = noise.noise(Vector((x * 0.35, z * 0.35, self.seed))) * self.sea.get("wobble", 0.6)
        if side == "south":
            return (z - (-self.d / 2 + width)) + wob
        if side == "north":
            return ((self.d / 2 - width) - z) + wob
        if side == "east":
            return ((self.w / 2 - width) - x) + wob
        if side == "west":
            return (x - (-self.w / 2 + width)) + wob
        return 1e9

    def height(self, x, z):
        h = self.base
        for b in self.bumps:
            dd = math.hypot((x - b["x"]) / b.get("sx", 1.0), (z - b["z"]) / b.get("sz", 1.0))
            if dd < b["r"]:
                h += b["h"] * (0.5 + 0.5 * math.cos(math.pi * dd / b["r"]))
        # tiny organic undulation
        h += noise.noise(Vector((x * 0.45, z * 0.45, self.seed * 0.1))) * 0.035
        for p in self.paths:
            dd = seg_dist(p["pts"], x, z)
            if dd < p["w"] / 2 + 0.15:
                h -= 0.025 * smooth01((p["w"] / 2 + 0.15 - dd) / 0.15)
        for wb in self.water:
            sd = sd_shape(wb, x, z)
            shore = wb.get("shore", 0.45)
            lvl = wb.get("level", -0.08)
            dep = wb.get("depth", 0.45)
            if sd < 0:
                inner = smooth01(-sd / max(0.2, wb.get("falloff", 0.8)))
                h = min(h, lvl - 0.05 - dep * inner)
            elif sd < shore:
                t = smooth01(sd / shore)
                h = min(h, (lvl - 0.05) * (1 - t) + h * t)
        if self.sea:
            sd = self.sea_sd(x, z)
            lvl = self.sea.get("level", -0.12)
            dep = self.sea.get("depth", 0.7)
            shore = self.sea.get("shore", 1.0)
            if sd < 0:
                h = min(h, lvl - 0.05 - dep * smooth01(-sd / 1.6))
            elif sd < shore:
                t = smooth01(sd / shore)
                h = min(h, (lvl - 0.04) * (1 - t) + h * t)
        return h

    def colour(self, x, z, h):
        """RGBA (sRGB, alpha = grassiness)."""
        g = self.grass
        n = noise.noise(Vector((x * 0.6, z * 0.6, 3.3 + self.seed)))
        c = mix(g, tint(g, 1.1 if n > 0 else 0.93), abs(n) * 0.6)
        a = 1.0
        # fields (ploughed soil / allotment beds)
        for f in self.fields:
            sd = sd_shape(f, x, z)
            if sd < 0.05:
                t = smooth01((0.05 - sd) / 0.12)
                stripe = 0.5 + 0.5 * math.sin((z if f.get("rows", "x") == "x" else x) * math.tau / 0.32)
                fc = mix(f.get("color", "8A5A3C"), tint(f.get("color", "8A5A3C"), 0.82), stripe * 0.6)
                c = mix(c, fc, t)
                a = a * (1 - t)
        for p in self.paths:
            dd = seg_dist(p["pts"], x, z)
            hw = p["w"] / 2
            if dd < hw + 0.06:
                t = smooth01((hw + 0.06 - dd) / 0.12)
                pc = p.get("color", "D9B98A")
                pc = mix(pc, tint(pc, 0.9), 0.5 + 0.5 * noise.noise(Vector((x * 3, z * 3, 1.0))))
                c = mix(c, pc, t)
                a = a * (1 - t)
        for s in self.sand:
            sd = sd_shape(s, x, z)
            if sd < 0.2:
                t = smooth01((0.2 - sd) / 0.4)
                c = mix(c, s.get("color", "F2D9A0"), t)
                a = a * (1 - t)
        # shores: sandy-mud ring around water, darker under water
        for wb in self.water:
            sd = sd_shape(wb, x, z)
            ring = wb.get("shore", 0.45) + 0.18
            if sd < ring:
                t = smooth01((ring - sd) / 0.3)
                sc = wb.get("shoreColor", "D8C08A")
                c = mix(c, sc, t)
                a = a * (1 - t)
            if sd < 0:
                c = mix(c, wb.get("bedColor", "9C8A5E"), smooth01(-sd / 0.5))
        if self.sea:
            sd = self.sea_sd(x, z)
            if sd < self.sea.get("beach", 1.6):
                t = smooth01((self.sea.get("beach", 1.6) - sd) / 0.5)
                c = mix(c, self.sea.get("sandColor", "F2D9A0"), t)
                a = a * (1 - t)
            if sd < 0:
                c = mix(c, "D9C08A", smooth01(-sd / 0.8))
        return hexcol(c, a)

    # ---- footprint mapping -----------------------------------------------------------
    def map_point(self, u, v):
        """Grid (u, v) in [-1, 1]^2 -> rounded-rect footprint (x, z)."""
        a, b, r = self.w / 2, self.d / 2, self.corner
        x, z = u * a, v * b
        cx, cz = a - r, b - r
        if abs(x) > cx and abs(z) > cz:
            sx, sz = math.copysign(1, x), math.copysign(1, z)
            dx, dz = abs(x) - cx, abs(z) - cz
            t = max(dx, dz) / r
            L = math.hypot(dx, dz)
            if L > 1e-9:
                dx, dz = dx / L * t * r, dz / L * t * r
            x, z = sx * (cx + dx), sz * (cz + dz)
        return x, z

    # ---- build -----------------------------------------------------------------------
    def build_ground(self, step=0.14):
        nx = max(8, int(round(self.w / step)))
        nz = max(6, int(round(self.d / step)))
        verts, cols, faces = [], [], []
        idx = {}
        for j in range(nz + 1):
            for i in range(nx + 1):
                u, v = -1 + 2 * i / nx, -1 + 2 * j / nz
                x, z = self.map_point(u, v)
                h = self.height(x, z)
                idx[(i, j)] = len(verts)
                verts.append((x, h, z))
                cols.append(self.colour(x, z, h))
        for j in range(nz):
            for i in range(nx):
                a, b, c, d = idx[(i, j)], idx[(i, j + 1)], idx[(i + 1, j + 1)], idx[(i + 1, j)]
                # split along the shorter diagonal for nicer shading
                faces.append((a, b, c, d))
        # boundary ring (counter-clockwise seen from above in the right-handed math sense)
        ring = [(i, 0) for i in range(nx)] + [(nx, j) for j in range(nz)] + \
               [(i, nz) for i in range(nx, 0, -1)] + [(0, j) for j in range(nz, 0, -1)]
        ring_ids = [idx[k] for k in ring]
        return verts, cols, faces, ring_ids

    def build(self):
        verts, cols, faces, ring = self.build_ground()
        ground = Model("Ground")
        ground.raw_vc(verts, faces, cols, smooth=True)

        # side walls with strata; rings descend and lean inward a little
        wall_v, wall_f, wall_c = [], [], []
        n = len(ring)
        levels = [("lip", 0.0)]
        cx_all = sum(verts[k][0] for k in ring) / n
        cz_all = sum(verts[k][2] for k in ring) / n
        # depths below the top for each ring
        ring_depths = [0.0, LIP, 0.18, 0.32, 0.47, 0.62, 0.74, 0.86, 1.0, self.depth]
        ring_depths = sorted(set(d for d in ring_depths if d <= self.depth))
        rows = []
        for ri, dep in enumerate(ring_depths):
            row = []
            for k, vid in enumerate(ring):
                x, h, z = verts[vid]
                ox, oz = x - cx_all, z - cz_all
                L = math.hypot(ox, oz) or 1.0
                ox, oz = ox / L, oz / L
                if dep == 0.0:
                    y = h
                    out = 0.0
                elif dep == LIP:
                    y = h - LIP
                    out = 0.035  # grass lip overhang
                else:
                    y = min(h - LIP, self.base) - (dep - LIP) * 1.0 + (0.0 if dep < self.depth else 0)
                    y = min(y, h - dep)
                    jitter = noise.noise(Vector((x * 0.9, y * 3.0, z * 0.9))) * 0.05
                    out = -0.18 * ((dep - LIP) / max(0.01, self.depth - LIP)) ** 1.6 + jitter
                row.append(len(wall_v))
                wall_v.append((x + ox * out, y, z + oz * out))
            rows.append(row)
        for r in range(len(rows) - 1):
            top, bot = rows[r], rows[r + 1]
            for k in range(n):
                k2 = (k + 1) % n
                # outward-facing quad
                wall_f.append((top[k], top[k2], bot[k2], bot[k]))
                ymid = (wall_v[top[k]][1] + wall_v[bot[k]][1]) / 2
                if r == 0:
                    wall_c.append(hexcol(tint(self.grass, 0.85), 1.0))
                else:
                    col = STRATA[0][1]
                    rel = ymid - self.base
                    for (sy, sc) in STRATA:
                        if rel <= sy:
                            col = sc
                    # wobble band edges
                    wall_c.append(hexcol(col, 0.0))
        # bottom cap (fan)
        bottom = rows[-1]
        cy = min(wall_v[k][1] for k in bottom)
        centre = len(wall_v)
        wall_v.append((cx_all, cy, cz_all))
        for k in range(n):
            k2 = (k + 1) % n
            wall_f.append((bottom[k], bottom[k2], centre))
            wall_c.append(hexcol("5E566E", 0.0))
        ground.raw_vc(wall_v, wall_f, None, smooth=False, face_cols=wall_c)
        parts = [ground]

        for wb in self.water:
            parts.append(self.water_mesh(wb))
        if self.sea:
            parts.append(self.sea_mesh(verts, ring))
        return parts

    def water_mesh(self, wb):
        m = Model("Water_" + wb.get("id", "pond"))
        lvl = wb.get("level", -0.08)
        shape = wb.get("shape", "ellipse")
        verts, faces = [], []
        if shape == "ellipse":
            rings, segs = 6, 40
            pad = 1.06
            verts.append((wb["x"], lvl, wb["z"]))
            for r in range(1, rings + 1):
                t = r / rings
                for s in range(segs):
                    a = math.tau * s / segs
                    verts.append((wb["x"] + math.cos(a) * wb["rx"] * t * pad, lvl, wb["z"] + math.sin(a) * wb["rz"] * t * pad))
            for s in range(segs):
                s2 = (s + 1) % segs
                faces.append((0, 1 + s2, 1 + s))
            for r in range(1, rings):
                b0, b1 = 1 + (r - 1) * segs, 1 + r * segs
                for s in range(segs):
                    s2 = (s + 1) % segs
                    faces.append((b0 + s, b0 + s2, b1 + s2, b1 + s))
        else:
            # rect / river: grid over bounds, keep cells near/inside the shape
            xs = [wb["x"] - wb.get("w", 2) / 2 - 0.3, wb["x"] + wb.get("w", 2) / 2 + 0.3]
            zs = [wb["z"] - wb.get("d", 2) / 2 - 0.3, wb["z"] + wb.get("d", 2) / 2 + 0.3]
            if shape == "river":
                px = [p[0] for p in wb["pts"]]
                pz = [p[1] for p in wb["pts"]]
                xs = [min(px) - wb["w"], max(px) + wb["w"]]
                zs = [min(pz) - wb["w"], max(pz) + wb["w"]]
            step = 0.25
            nx = max(2, int((xs[1] - xs[0]) / step))
            nz = max(2, int((zs[1] - zs[0]) / step))
            ids = {}
            for j in range(nz + 1):
                for i in range(nx + 1):
                    x = xs[0] + (xs[1] - xs[0]) * i / nx
                    z = zs[0] + (zs[1] - zs[0]) * j / nz
                    ids[(i, j)] = len(verts)
                    verts.append((x, lvl, z))
            for j in range(nz):
                for i in range(nx):
                    cxm = xs[0] + (xs[1] - xs[0]) * (i + 0.5) / nx
                    czm = zs[0] + (zs[1] - zs[0]) * (j + 0.5) / nz
                    if sd_shape(wb, cxm, czm) < 0.25:
                        faces.append((ids[(i, j)], ids[(i, j + 1)], ids[(i + 1, j + 1)], ids[(i + 1, j)]))
        cols = [hexcol("4FB6D8")] * len(verts)
        m.raw_vc(verts, faces, cols, smooth=True)
        return m

    def sea_mesh(self, ground_verts, ring):
        m = Model("Water_sea")
        lvl = self.sea.get("level", -0.12)
        # surface over the footprint (hidden under land by depth)
        verts, faces = [], []
        nx, nz = int(self.w / 0.35), int(self.d / 0.35)
        ids = {}
        for j in range(nz + 1):
            for i in range(nx + 1):
                x, z = self.map_point(-1 + 2 * i / nx, -1 + 2 * j / nz)
                ids[(i, j)] = len(verts)
                verts.append((x * 0.999, lvl, z * 0.999))
        for j in range(nz):
            for i in range(nx):
                cx, cz = self.map_point(-1 + 2 * (i + 0.5) / nx, -1 + 2 * (j + 0.5) / nz)
                if self.sea_sd(cx, cz) < 1.2:
                    faces.append((ids[(i, j)], ids[(i, j + 1)], ids[(i + 1, j + 1)], ids[(i + 1, j)]))
        # skirt: along the boundary where the floor is below the sea level
        n = len(ring)
        for k in range(n):
            k2 = (k + 1) % n
            x0, h0, z0 = ground_verts[ring[k]]
            x1, h1, z1 = ground_verts[ring[k2]]
            if h0 < lvl - 0.01 or h1 < lvl - 0.01:
                b = len(verts)
                verts += [(x0, lvl, z0), (x1, lvl, z1), (x1, min(h1, lvl) - 0.06, z1), (x0, min(h0, lvl) - 0.06, z0)]
                faces.append((b, b + 1, b + 2, b + 3))
        cols = [hexcol("4FB6D8")] * len(verts)
        m.raw_vc(verts, faces, cols, smooth=True)
        return m


def all_terrain():
    out = []
    for path in sorted(glob.glob(os.path.join(LEVELS_DIR, "*.json"))):
        with open(path) as f:
            level = json.load(f)
        if "island" not in level:
            continue
        out.append(("terrain_" + level["id"], Island(level).build()))
    return out
