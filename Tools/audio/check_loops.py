#!/usr/bin/env python3
"""Checks every looping clip (music, ambience, *_loop SFX) for an audible seam.

    Tools/.venv/bin/python Tools/audio/check_loops.py

At the loop point the jump between the last and first sample must be small compared with the
clip's typical sample-to-sample step (else: a click every repeat), and for ambience and SFX loops
the level change across the seam must be no bigger than the clip's own normal window-to-window
movement (else: an audible swell or drop each repeat; music is exempt, since it loops on a
downbeat after a decaying tail). Exits non-zero on failure.
"""
import glob
import os
import subprocess
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 48000


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                         capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2)


def level_db(x):
    return 20 * np.log10(np.sqrt((x.astype(np.float64) ** 2).mean()) + 1e-9)


def main():
    files = sorted(glob.glob(os.path.join(ROOT, "Assets", "Music", "music_*.ogg")) + glob.glob(os.path.join(ROOT, "Assets", "Ambience", "amb_*.ogg")) +
                   glob.glob(os.path.join(AUDIO, "Sfx", "*_loop.wav")))
    bad = 0
    for f in files:
        a = decode(f)
        name = os.path.relpath(f, os.path.join(ROOT, "Assets"))
        if len(a) < SR:
            print(f"  FAIL {name}: could not decode"); bad += 1; continue
        typical = np.abs(np.diff(a[:SR], axis=0)).mean(axis=0).max() + 1e-9
        jump = np.abs(a[0] - a[-1]).max() / typical
        w = SR // 4
        step = abs(level_db(a[:w]) - level_db(a[-w:]))
        # how much the level normally moves between neighbouring windows inside the clip (rhythmic
        # loops like gulps or creaks swing a lot); the seam may not move more than that
        levels = [level_db(a[i:i + w]) for i in range(0, len(a) - w, w)]
        normal = max(np.percentile(np.abs(np.diff(levels)), 95), 1.0)
        music = "Music" in name
        ok = jump < 4.0 and (music or step <= normal + 1.0)
        bad += not ok
        print(f"  {'PASS' if ok else 'FAIL'} {name:28} seam step {jump:4.1f}x typical, level change {step:4.1f} dB "
              f"(within the clip: up to {normal:4.1f} dB){' music: level not checked' if music else ''}")
    print("all loops seamless" if bad == 0 else f"{bad} loop(s) with a seam")
    sys.exit(1 if bad else 0)


main()
