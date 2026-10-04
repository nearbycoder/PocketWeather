"""Renders all of Pocket Weather's audio into Assets/Resources/Audio.

    Tools/.venv/bin/python Tools/audio/synth.py            # everything
    Tools/.venv/bin/python Tools/audio/synth.py sfx music  # only some groups
    Tools/.venv/bin/python Tools/audio/synth.py --only gust,sheep

Groups: notes, loops, sfx, amb, stingers, music. Also writes Audio/music.json (tempo + chord
timeline per track) and prints loudness/peak stats for every file.
"""
import json
import os
import subprocess
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import dsp  # noqa: E402
import music  # noqa: E402
import sfx  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")


def stats(x):
    x = np.asarray(x)
    peak = np.max(np.abs(x)) + 1e-12
    rms = np.sqrt(np.mean(x ** 2)) + 1e-12
    return f"peak {20 * np.log10(peak):6.1f} dBFS  rms {20 * np.log10(rms):6.1f} dBFS  len {x.shape[-1] / dsp.SR:5.2f}s"


def write(sub, name, x):
    d = os.path.join(OUT, sub)
    os.makedirs(d, exist_ok=True)
    wav = os.path.join(d, name + ".wav")
    dsp.write_wav(wav, x)
    if sub in ("Music", "Amb"):
        # long files are stored as Ogg Vorbis to keep the repository small
        ogg = os.path.join(d, name + ".ogg")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "6", ogg], check=True)
        os.remove(wav)
    print(f"  {sub}/{name:16s} {stats(x)}")


def main():
    args = sys.argv[1:]
    only = None
    if "--only" in args:
        i = args.index("--only")
        only = set(args[i + 1].split(","))
        args = args[:i] + args[i + 2:]
    groups = set(args) or {"notes", "loops", "sfx", "amb", "stingers", "music"}
    t0 = time.time()

    def want(n):
        return only is None or n in only

    if "notes" in groups:
        for n, fn in sfx.NOTES.items():
            if want(n):
                write("Notes", n, fn())
    if "loops" in groups:
        for n, fn in sfx.LOOPS.items():
            if want(n):
                write("Sfx", n, fn())
    if "sfx" in groups:
        for n, fn in sfx.SFX.items():
            if want(n):
                write("Sfx", n, fn())
    if "amb" in groups:
        for n, fn in sfx.AMBIENCE.items():
            if want(n):
                write("Amb", n, fn())
    if "stingers" in groups:
        for n, fn in music.STINGERS.items():
            if want(n):
                write("Music", "sting_" + n, fn())
    if "music" in groups:
        meta_path = os.path.join(OUT, "music.json")
        meta = {"tracks": []}
        if os.path.exists(meta_path):
            with open(meta_path) as f:
                meta = json.load(f)
        by_name = {t["name"]: t for t in meta.get("tracks", [])}
        for fn in music.TRACKS:
            name = fn.__name__
            if not want(name):
                continue
            song = fn()
            x = song.render()
            write("Music", "music_" + name, x)
            by_name[name] = song.meta()
        meta["tracks"] = [by_name[k] for k in sorted(by_name)]
        os.makedirs(OUT, exist_ok=True)
        with open(meta_path, "w") as f:
            json.dump(meta, f, indent=1)
    print(f"done in {time.time() - t0:.1f}s")


main()
