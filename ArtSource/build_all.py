"""Builds every Pocket Weather model and exports FBX into Assets/Resources/Models.

    blender -b -P ArtSource/build_all.py -- [--only name1,name2] [--group core,props,...]
                                            [--preview /tmp/prev] [--no-export]

Groups: core (cloud, plants, first animals), props (buildings and set dressing),
characters (people and animals), terrain (one island per level JSON).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import pw_lib  # noqa: E402
from pw_lib import Model  # noqa: E402


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"only": None, "group": None, "preview": None, "export": True}
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--only":
            opts["only"] = set(argv[i + 1].split(",")); i += 1
        elif a == "--group":
            opts["group"] = set(argv[i + 1].split(",")); i += 1
        elif a == "--preview":
            opts["preview"] = argv[i + 1]; i += 1
        elif a == "--no-export":
            opts["export"] = False
        i += 1
    return opts


def groups():
    import props_core
    g = {"core": props_core.all_core}
    try:
        import props_world
        g["props"] = props_world.all_props
    except ImportError:
        pass
    try:
        import characters
        g["characters"] = characters.all_characters
    except ImportError:
        pass
    try:
        import terrain
        g["terrain"] = terrain.all_terrain
    except ImportError:
        pass
    return g


def clear_objects():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)


def main():
    opts = parse_args()
    pw_lib.reset_scene()
    count = 0
    for gname, fn in groups().items():
        if opts["group"] and gname not in opts["group"]:
            continue
        subdir = "" if gname != "terrain" else "Terrain"
        for name, parts in fn():
            if opts["only"] and name not in opts["only"]:
                continue
            clear_objects()
            parts = parts if isinstance(parts, list) else [parts]
            objs = [p.build() if isinstance(p, Model) else p for p in parts]
            for p, o in zip(parts, objs):
                if isinstance(p, Model):
                    o.name = p.name
            if opts["export"]:
                pw_lib.export_fbx(objs, os.path.join(pw_lib.MODELS_DIR, subdir, name + ".fbx"))
            if opts["preview"]:
                os.makedirs(opts["preview"], exist_ok=True)
                pw_lib.preview(objs, os.path.join(opts["preview"], name + ".png"), size=384,
                               angle=(40, 25) if gname != "terrain" else (52, 0))
            count += 1
            print(f"[build] {gname}/{name} ({len(objs)} parts)")
    print(f"[build] done: {count} models")


main()
