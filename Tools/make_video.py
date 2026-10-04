#!/usr/bin/env python3
"""Turns a recorded run into a captioned gameplay video.

    Tools/play.sh -pwAutopilot /tmp/auto -pwVideo /tmp/vid -pwOnly 1,2,3,4,6,9,12 -pwDelights \
        -pwFreshSave -logFile /tmp/vid.log
    python3 Tools/make_video.py /tmp/vid /tmp/vid.log Builds/PocketWeather_gameplay.mp4

Reads the "[PW] mark <frame> <what>" lines the AutoPilot/Recorder log, writes timed captions
(ASS, in the game's Fredoka font) into a cream bar under the picture, and muxes the recorded
audio through a gentle limiter.
"""
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "Assets", "Resources", "Fonts")
LEVELS = os.path.join(ROOT, "Assets", "Resources", "Levels")
FPS = 30
BAR = 110

LEVEL_CAPTIONS = {
    "level01": "Move Pip with the mouse or a finger. Hold to rain on thirsty flowers, and float over water to drink.",
    "level02": "Each bed wants \"just right\": fill its ring into the green band. Too much rain makes it soggy.",
    "level03": "Pip's shadow cools hot animals. Hover over each sheep until it sighs happily.",
    "level04": "Right-drag (or flick) to blow a gust, which costs a little water. Puff Tom's boat home to the dock.",
    "level05": "Gusts dry the washing, rain soaks it again. Juggle needs that want opposite things.",
    "level06": "Rain beside someone, then move away: sunshine on the mist makes a rainbow.",
    "level07": "A few strong gusts spin the windmill up. Meanwhile the wheat wants rain and the donkey wants shade.",
    "level08": "The duck pond is finite: drink too much and the ducks fret. Catch fountain spray for water.",
    "level09": "Night falls. Rain out the fires before they spread, but don't drown the campers' campfire.",
    "level10": "Several boats to their buoys, and the sandcastle must stay dry.",
    "level11": "A heatwave: beds dry out fast, and the sunflowers want sun, not shade.",
    "level12": "The finale. Water the wedding arch...",
}


def ass_time(frame):
    t = max(0.0, frame / FPS)
    h, rem = divmod(t, 3600)
    m, s = divmod(rem, 60)
    return f"{int(h)}:{int(m):02d}:{s:05.2f}"


def main():
    vid, log, out = sys.argv[1], sys.argv[2], sys.argv[3]
    marks = []
    for line in open(log, errors="ignore"):
        m = re.match(r"\[PW\] mark (\d+) (.+)", line.strip())
        if m:
            marks.append((int(m.group(1)), m.group(2)))
    frames = len([f for f in os.listdir(vid) if f.endswith(".jpg")])
    w, h = 1600, 900
    try:
        probe = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0",
                                os.path.join(vid, "f00000.jpg")], capture_output=True, text=True).stdout.strip()
        w, h = (int(v) for v in probe.split(","))
    except Exception:
        pass

    cues = []   # (start, end, text)

    def cue(start, seconds, text):
        # never overlap: queue after the previous caption
        if cues and start < cues[-1][1]:
            start = cues[-1][1]
        cues.append((start, start + int(seconds * FPS), text))

    cue(15, 4.0, "Pocket Weather: you're Pip, a tiny cloud helping a miniature world through its day.")
    cue(int(4.5 * FPS), 3.2, "Twelve little dioramas, one summer in Pocketvale. Each level is one day.")
    first_results = True
    for frame, what in marks:
        kind, _, arg = what.partition(" ")
        if kind == "play" and arg in LEVEL_CAPTIONS:
            cue(frame, 5.5, LEVEL_CAPTIONS[arg])
        elif kind == "delight":
            d = json.load(open(os.path.join(LEVELS, arg + ".json"))).get("delight", {})
            if d.get("title") and d.get("type") != "catch":
                cue(frame, 3.5, f"Every level hides a secret delight: {d['title'][:1].lower() + d['title'][1:]}.")
        elif kind == "sneeze":
            cue(frame, 4.0, "...the pollen makes Pip sneeze all over the wedding! Make a rainbow to save the day.")
        elif kind == "bouquet":
            cue(frame, 3.0, "And the secret: catch the bride's bouquet!")
        elif kind == "results" and first_results:
            first_results = False
            cue(frame, 4.5, "Meet every need at once to save the day. Stamps: day saved, before par, and the secret.")

    ass = os.path.join(vid, "captions.ass")
    size = 40
    with open(ass, "w") as f:
        f.write(f"""[Script Info]
ScriptType: v4.00+
PlayResX: {w}
PlayResY: {h + BAR}
WrapStyle: 0

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Cap,Fredoka SemiBold,{size},&H005A3A3B,&H005A3A3B,&H00FFFFFF,&H00000000,0,0,0,0,100,100,0,0,1,0,0,2,60,60,{(BAR - size) // 2 - 4},1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
""")
        for s, e, text in cues:
            e = min(e, frames)
            if s >= e:
                continue
            f.write(f"Dialogue: 0,{ass_time(s)},{ass_time(e)},Cap,,0,0,0,,{{\\fad(250,250)}}{text}\n")

    sr, ch = open(os.path.join(vid, "audio.txt")).read().split()
    vf = f"pad={w}:{h + BAR}:0:0:color=0xFFF8EC,subtitles={ass}:fontsdir={FONTS}"
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", os.path.join(vid, "f%05d.jpg"),
           "-f", "f32le", "-ar", sr, "-ac", ch, "-i", os.path.join(vid, "audio.f32"),
           "-vf", vf, "-af", "alimiter=limit=0.89:level=false,afade=t=out:st=%.2f:d=1.5" % (frames / FPS - 1.5),
           "-c:v", "libx264", "-preset", "slow", "-crf", "20", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
           "-c:a", "aac", "-b:a", "160k", "-shortest", out]
    subprocess.run(cmd, check=True)
    print(f"{out}: {frames / FPS:.1f} s, {len(cues)} captions")


if __name__ == "__main__":
    main()
