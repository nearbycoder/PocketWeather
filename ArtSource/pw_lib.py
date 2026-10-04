"""Modeling helpers for Pocket Weather (Blender 4.5, run headless).

Everything is authored in *Unity* coordinates (x right, y up, z forward = away from the camera)
and converted to Blender space when written. The FBX exporter settings in `export_fbx` map
Blender (x, y, z) -> Unity (-x, z, -y); `U()` is the inverse for points.

Models are built as one bmesh per model. Each part gets a flat sRGB vertex colour (attribute
"Col"), so every prop in the game can share one toon material. Vertex alpha < 1 marks emissive
parts (windows that glow at dusk): emission = 1 - alpha.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS_DIR = os.path.join(ROOT, "Assets", "Resources", "Models")

# Unity -> Blender basis (points and directions).
U2B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def U(x, y=None, z=None):
    """Unity point -> Blender Vector."""
    if y is None:
        x, y, z = x
    return Vector((-x, -z, y))


def hexcol(h, a=1.0):
    h = h.lstrip("#")
    return (int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0, a)


def tint(h, f):
    """Lighten (f > 1) or darken (f < 1) a hex colour; returns hex."""
    r, g, b, _ = hexcol(h)
    if f >= 1:
        r, g, b = (c + (1 - c) * (f - 1) for c in (r, g, b))
    else:
        r, g, b = (c * f for c in (r, g, b))
    return "%02X%02X%02X" % tuple(max(0, min(255, int(round(c * 255)))) for c in (r, g, b))


def mix(h1, h2, t):
    a, b = hexcol(h1), hexcol(h2)
    return "%02X%02X%02X" % tuple(int(round((a[i] * (1 - t) + b[i] * t) * 255)) for i in range(3))


# ----------------------------------------------------------------------------- scene

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# ----------------------------------------------------------------------------- transforms

def _rot_matrix(rot):
    """rot = (rx, ry, rz) Euler degrees in Unity space (applied Z, X, Y like Unity)."""
    if rot is None:
        return Matrix.Identity(4)
    rx, ry, rz = (math.radians(a) for a in rot)
    # Unity applies Z, then X, then Y (extrinsic), i.e. R = Ry * Rx * Rz.
    mx = Matrix.Rotation(rx, 4, "X")
    my = Matrix.Rotation(ry, 4, "Y")
    mz = Matrix.Rotation(rz, 4, "Z")
    return my @ mx @ mz


def unity_matrix(pos=(0, 0, 0), rot=None, scale=(1, 1, 1)):
    """A transform expressed in Unity space."""
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    t = Matrix.Translation(Vector(pos))
    s = Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1.0)))
    return t @ _rot_matrix(rot) @ s


# ----------------------------------------------------------------------------- builder

class Model:
    """Accumulates coloured parts into one mesh, authored in Unity space.

    Geometry is generated in a temporary bmesh in *Unity* coordinates; at the end the whole
    mesh is converted to Blender space once. Each part is a closed primitive with its own
    colour and smooth/flat flag.
    """

    def __init__(self, name, pivot=(0, 0, 0)):
        self.name = name
        self.pivot = pivot
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.stack = [Matrix.Identity(4)]

    # -- grouping transforms -------------------------------------------------------------
    def push(self, pos=(0, 0, 0), rot=None, scale=1.0):
        self.stack.append(self.stack[-1] @ unity_matrix(pos, rot, scale))
        return self

    def pop(self):
        self.stack.pop()
        return self

    # -- core ----------------------------------------------------------------------------
    def _add(self, tmp, color, smooth, matrix):
        """Copy temp bmesh `tmp` (Unity space, local) into the model with a flat colour."""
        m = self.stack[-1] @ matrix
        bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
        if m.determinant() < 0:
            bmesh.ops.reverse_faces(tmp, faces=tmp.faces)
        rgba = hexcol(color) if isinstance(color, str) else color
        vmap = {}
        for v in tmp.verts:
            vmap[v] = self.bm.verts.new(v.co)
        new_faces = []
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.smooth = smooth
            nf.normal_update()
            new_faces.append(nf)
            for loop in nf.loops:
                loop[self.col] = rgba
        if smooth:
            # keep hard creases (e.g. unbevelled cylinder caps) sharp
            for nf in new_faces:
                for e in nf.edges:
                    lf = e.link_faces
                    if len(lf) == 2 and lf[0].normal.dot(lf[1].normal) < 0.5:
                        e.smooth = False
        tmp.free()

    @staticmethod
    def _bevel(tmp, width, segments=2, dot=0.7):
        """Round the sharp edges (face normals differing by more than ~45 degrees)."""
        if width <= 0:
            return
        tmp.normal_update()
        edges = [e for e in tmp.edges if len(e.link_faces) == 2
                 and e.link_faces[0].normal.dot(e.link_faces[1].normal) < dot]
        if edges:
            bmesh.ops.bevel(tmp, geom=edges, offset=width, segments=segments, profile=0.5,
                            affect="EDGES", clamp_overlap=True)

    # -- primitives (all sizes in Unity units, centred unless noted) ------------------------
    def box(self, size, pos=(0, 0, 0), color="FFFFFF", rot=None, bevel=0.04, seg=2, smooth=False):
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        bmesh.ops.scale(tmp, vec=Vector(size), verts=tmp.verts)
        self._bevel(tmp, min(bevel, min(size) * 0.45), seg)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def cyl(self, radius, height, pos=(0, 0, 0), color="FFFFFF", rot=None, segs=16, bevel=0.03,
            radius2=None, smooth=True, cap=True):
        """Cylinder along local +Y, centred (use radius2 for a cone/frustum)."""
        tmp = bmesh.new()
        r2 = radius if radius2 is None else radius2
        bmesh.ops.create_cone(tmp, cap_ends=cap, cap_tris=False, segments=segs, radius1=radius,
                              radius2=r2, depth=height)
        # create_cone is along Z; rotate to Y
        bmesh.ops.rotate(tmp, verts=tmp.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, "X"))
        if bevel > 0 and cap:
            self._bevel(tmp, min(bevel, radius * 0.4, height * 0.4), 2)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def cone(self, radius, height, pos=(0, 0, 0), color="FFFFFF", rot=None, segs=16, smooth=True, tip=0.0):
        return self.cyl(radius, height, pos, color, rot, segs, bevel=0.0, radius2=tip, smooth=smooth)

    def sphere(self, radius, pos=(0, 0, 0), color="FFFFFF", scale=(1, 1, 1), rot=None, subdiv=2,
               smooth=True, uv=False, segs=16):
        tmp = bmesh.new()
        if uv:
            bmesh.ops.create_uvsphere(tmp, u_segments=segs, v_segments=max(6, segs // 2), radius=radius)
        else:
            bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=radius)
        bmesh.ops.scale(tmp, vec=Vector(scale), verts=tmp.verts)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def blob(self, radius, pos=(0, 0, 0), color="FFFFFF", scale=(1, 1, 1), rot=None, subdiv=3,
             amp=0.12, freq=2.2, seed=0, smooth=True, flat_bottom=None):
        """Noise-displaced icosphere (bushes, canopies, rocks, cloud puffs)."""
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=radius)
        off = Vector((seed * 7.31, seed * 3.17, seed * 5.53))
        for v in tmp.verts:
            n = v.co.normalized()
            d = noise.noise(n * freq + off) * 0.7 + noise.noise(n * freq * 2.3 + off) * 0.3
            v.co = n * radius * (1.0 + amp * d)
        bmesh.ops.scale(tmp, vec=Vector(scale), verts=tmp.verts)
        if flat_bottom is not None:
            for v in tmp.verts:
                if v.co.y < flat_bottom:
                    v.co.y = flat_bottom + (v.co.y - flat_bottom) * 0.15
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def capsule(self, radius, height, pos=(0, 0, 0), color="FFFFFF", rot=None, segs=16, smooth=True):
        """Capsule along local +Y; `height` is total height."""
        tmp = bmesh.new()
        bmesh.ops.create_uvsphere(tmp, u_segments=segs, v_segments=max(8, segs // 2), radius=radius)
        half = max(0.0, height / 2 - radius)
        for v in tmp.verts:
            # bmesh sphere is z-up; stretch along z then rotate to y
            if v.co.z > 1e-5:
                v.co.z += half
            elif v.co.z < -1e-5:
                v.co.z -= half
        bmesh.ops.rotate(tmp, verts=tmp.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, "X"))
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def torus(self, major, minor, pos=(0, 0, 0), color="FFFFFF", rot=None, segs=24, rsegs=10,
              arc=360.0, smooth=True):
        """Torus in the local XZ plane (ring around +Y). `arc` < 360 makes an open arc."""
        tmp = bmesh.new()
        rings = []
        full = arc >= 359.9
        n = segs if full else segs + 1
        for i in range(n):
            a = math.radians(arc) * i / segs
            c, s = math.cos(a), math.sin(a)
            ring = []
            for j in range(rsegs):
                b = 2 * math.pi * j / rsegs
                r = major + minor * math.cos(b)
                ring.append(tmp.verts.new((r * c, minor * math.sin(b), r * s)))
            rings.append(ring)
        for i in range(n if full else n - 1):
            r0, r1 = rings[i], rings[(i + 1) % n]
            for j in range(rsegs):
                tmp.faces.new((r0[j], r0[(j + 1) % rsegs], r1[(j + 1) % rsegs], r1[j]))
        if not full:
            tmp.faces.new(list(reversed(rings[0])))
            tmp.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def lathe(self, profile, pos=(0, 0, 0), color="FFFFFF", rot=None, segs=20, smooth=True, cap_top=True,
              cap_bottom=True):
        """Revolve [(radius, y), ...] (bottom to top) around local +Y."""
        tmp = bmesh.new()
        rings = []
        for (r, y) in profile:
            ring = []
            for i in range(segs):
                a = 2 * math.pi * i / segs
                ring.append(tmp.verts.new((r * math.cos(a), y, r * math.sin(a))))
            rings.append(ring)
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for i in range(segs):
                tmp.faces.new((a[i], a[(i + 1) % segs], b[(i + 1) % segs], b[i]))
        if cap_bottom and profile[0][0] > 1e-4:
            tmp.faces.new(list(reversed(rings[0])))
        if cap_top and profile[-1][0] > 1e-4:
            tmp.faces.new(rings[-1])
        bmesh.ops.remove_doubles(tmp, verts=tmp.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def prism(self, points, depth, pos=(0, 0, 0), color="FFFFFF", rot=None, bevel=0.02, smooth=False):
        """Extrude a 2D polygon [(x, y), ...] (in local XY plane) along local Z by `depth`, centred."""
        tmp = bmesh.new()
        front = [tmp.verts.new((x, y, -depth / 2)) for (x, y) in points]
        back = [tmp.verts.new((x, y, depth / 2)) for (x, y) in points]
        n = len(points)
        tmp.faces.new(list(reversed(front)))
        tmp.faces.new(back)
        for i in range(n):
            j = (i + 1) % n
            tmp.faces.new((front[i], front[j], back[j], back[i]))
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        self._bevel(tmp, bevel, 1)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def quad(self, size, pos=(0, 0, 0), color="FFFFFF", rot=None, double=True):
        """Flat rectangle in local XZ plane facing +Y (double-sided by default)."""
        tmp = bmesh.new()
        w, d = size
        vs = [tmp.verts.new(p) for p in ((-w / 2, 0, -d / 2), (w / 2, 0, -d / 2), (w / 2, 0, d / 2), (-w / 2, 0, d / 2))]
        tmp.faces.new(list(reversed(vs)))
        if double:
            vs2 = [tmp.verts.new(v.co + Vector((0, -0.002, 0))) for v in vs]
            tmp.faces.new(vs2)
        self._add(tmp, color, False, unity_matrix(pos, rot))
        return self

    def raw(self, verts, faces, color="FFFFFF", smooth=False, pos=(0, 0, 0), rot=None):
        tmp = bmesh.new()
        vs = [tmp.verts.new(v) for v in verts]
        for f in faces:
            try:
                tmp.faces.new([vs[i] for i in f])
            except ValueError:
                pass
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        self._add(tmp, color, smooth, unity_matrix(pos, rot))
        return self

    def raw_vc(self, verts, faces, vcols, smooth=True, face_cols=None):
        """Mesh in Unity space with per-vertex RGBA colours (or per-face if face_cols given)."""
        bm = self.bm
        m = self.stack[-1]
        vs = [bm.verts.new(m @ Vector(v)) for v in verts]
        for fi, f in enumerate(faces):
            try:
                nf = bm.faces.new([vs[i] for i in f])
            except ValueError:
                continue
            nf.smooth = smooth
            for loop, vi in zip(nf.loops, f):
                c = face_cols[fi] if face_cols is not None else vcols[vi]
                loop[self.col] = c
        return self

    # -- output --------------------------------------------------------------------------
    def build(self):
        """Create a Blender object (converted to Blender space)."""
        px, py, pz = self.pivot
        bmesh.ops.translate(self.bm, vec=Vector((-px, -py, -pz)), verts=self.bm.verts)
        bmesh.ops.transform(self.bm, matrix=U2B, verts=self.bm.verts)
        # U2B has det -1: flip winding back so normals point outward.
        bmesh.ops.reverse_faces(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        me.update()
        ca = me.color_attributes
        if "Col" in ca:
            ca.active_color = ca["Col"]
            try:
                ca.render_color_index = list(ca).index(ca["Col"])
            except Exception:
                pass
        obj = bpy.data.objects.new(self.name, me)
        obj.location = U(px, py, pz)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ----------------------------------------------------------------------------- export

def export_fbx(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", colors_type="SRGB", use_custom_props=False)


def export_model(model_or_obj, name=None, subdir=""):
    """Build (if needed), export to Assets/Resources/Models/<subdir>/<name>.fbx and return the object."""
    obj = model_or_obj.build() if isinstance(model_or_obj, Model) else model_or_obj
    name = name or obj.name
    obj.name = name
    export_fbx([obj], os.path.join(MODELS_DIR, subdir, name + ".fbx"))
    return obj


# ----------------------------------------------------------------------------- previews

def preview(objs, path, size=512, angle=(52, 25), ortho=False, bg=(0.93, 0.95, 0.98)):
    """Workbench render of `objs` from a game-like camera (pitch, yaw degrees), vertex colours."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "VERTEX"
    shading.show_shadows = True
    shading.show_cavity = True
    shading.cavity_type = "WORLD"
    shading.background_type = "VIEWPORT"
    shading.background_color = bg
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    # bounds
    pts = []
    for o in objs:
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.1)
    cam_data = bpy.data.cameras.new("pcam")
    cam = bpy.data.objects.new("pcam", cam_data)
    scene.collection.objects.link(cam)
    pitch, yaw = (math.radians(a) for a in angle)
    # Unity camera looks along +Z (away); in Blender that's -Y. Place camera at -Z(unity) side.
    d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    d = Vector((-d.x, d.y, d.z))
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = radius * 2.3
        dist = radius * 4
    else:
        cam_data.lens = 70
        dist = radius / math.tan(math.radians(14)) * 1.05
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_end = dist * 4
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)


def hide_all_but(objs):
    keep = set(o.name for o in objs)
    for o in bpy.context.scene.objects:
        o.hide_render = o.name not in keep and o.type == "MESH"
