#!/usr/bin/env python3
"""Builds the store-page feature trailer from a scripted capture.

    R=$PWD/Recordings
    PW_W=1920 PW_H=1080 Tools/play.sh -pwTrailer -pwFreshSave -pwVideo $R/trailer -pwVideoQuality 95 \
        -logFile $R/trailer.log
    Tools/.venv/bin/python Tools/make_trailer.py docs/media/pocket-weather-trailer.mp4 $R/trailer $R/trailer.log

More "<frames dir> <log>" pairs can follow: a later capture's shots replace same-named ones, so a
few shots can be re-recorded with -pwShots (e.g. -pwShots 6,map) without redoing the rest.

The capture (Assets/Scripts/Automation/Trailer.cs) stages every shot on a fixed 30 fps clock and
logs "[PW] mark <frame> shot <name> begin|end". This script cuts the beats in TIMELINE out of it,
draws the captions, title card and end card with ImageMagick in the game's own fonts, icons and
colours, chains the beats with ffmpeg crossfades, and mixes a music bed from the game's own tracks
under the captured sound effects (ducked whenever they speak up), loudness-normalised.
It also writes the README's logo, teaser loop and trailer poster next to the trailer, and two
frames per beat (into Recordings/trailer_work/check) for checking.

Needs ffmpeg, ImageMagick 7 (`magick`) and numpy + scipy (Tools/.venv).
"""
import json
import os
import re
import shutil
import subprocess
import sys

import numpy as np
from scipy.io import wavfile
from scipy.ndimage import maximum_filter1d
from scipy.signal import filtfilt

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "Assets", "Resources")
FONT_HEAD = os.path.join(RES, "Fonts", "Fredoka-SemiBold.ttf")
FONT_BODY = os.path.join(RES, "Fonts", "Nunito-Bold.ttf")
ICONS = os.path.join(RES, "Icons")
MUSIC = os.path.join(ROOT, "Assets", "Music")

W, H, FPS, SR = 1920, 1080, 30, 48000
SPF = SR // FPS                       # recorded audio samples per video frame

# the game's UI palette (UiKit)
INK, INK_SOFT, PAPER = "#3B3A5A", "#6E6A8E", "#FFF8EC"
SKY, BUTTER, MINT, CORAL, LILAC = "#5FB8E6", "#FFD45C", "#6CCB8A", "#FF7A6B", "#B79CFF"
LOGO_OUTLINE = "#7A8FD6"
URL = "github.com/nearbycoder/PocketWeather"
TARGET_MB = float(os.environ.get("PW_TRAILER_MB", "37"))      # final MP4 size budget

# ---------------------------------------------------------------------------------------------
# The cut. Each beat: shot parts (shot name, seconds into the shot, duration), the transition
# into it from the previous beat, and its caption (icon, accent, headline, subline).
# ---------------------------------------------------------------------------------------------
FADE = ("fade", 0.45)
TIMELINE = [
    # cold open: the trailer moment, no words
    dict(id="cold_sneeze", parts=[("w_sneeze", 3.4, 6.4)], trans=None),
    dict(id="cold_rainbow", parts=[("w_rainbow", 0.6, 4.4)], trans=("fade", 0.3)),
    dict(id="title", kind="title", parts=[("title_bg", 0.6, 4.8)], trans=("fadewhite", 0.6)),
    # one beat per feature
    dict(id="pip", parts=[("pip_meet", 0.0, 5.0)], trans=FADE,
         cap=("pip", SKY, "Meet Pip, a tiny cloud", "Glide anywhere: Pip squishes after your mouse or finger")),
    dict(id="rain", parts=[("rain_bloom", 0.4, 5.0)], trans=FADE,
         cap=("drop", SKY, "Hold to rain", "Soil darkens, grass greens, flowers pop, and every drop plays a note")),
    dict(id="drink", parts=[("drink", 0.2, 4.2)], trans=FADE,
         cap=("drop", MINT, "Drink to refill", "Water is your only resource: float over ponds and the sea")),
    dict(id="band", parts=[("just_right", 0.5, 4.9)], trans=FADE,
         cap=("carrot", BUTTER, "Not too much, not too little", "Overwater a bed and it goes soggy")),
    dict(id="shade", parts=[("shade", 0.5, 6.6)], trans=FADE,
         cap=("sheep", BUTTER, "Shade the hot animals", "Your shadow cools them down, but rain on a sheep and it sulks")),
    dict(id="gust", parts=[("gust_boat", 0.3, 6.6)], trans=FADE,
         cap=("wind", MINT, "Flick to blow a gust", "Puff becalmed boats home to the harbour")),
    dict(id="laundry", parts=[("laundry", 0.0, 4.4)], trans=FADE,
         cap=("shirt", SKY, "Gusts dry the washing", "...and rain soaks it again, so mind what you water")),
    dict(id="rainbow", parts=[("rainbow", 0.0, 5.3)], trans=FADE,
         cap=("rainbow", LILAC, "Make rainbows", "Rain, then step aside: sunshine on the mist makes a rainbow")),
    dict(id="windmill", parts=[("windmill", 0.6, 4.8)], trans=FADE,
         cap=("windmill", MINT, "Spin up the windmill", "A few strong gusts get the sails turning")),
    dict(id="pond", parts=[("pond", 0.0, 5.0)], trans=FADE,
         cap=("duck", SKY, "Not every pond is endless", "Drink the duck pond below its line and the ducks fret")),
    dict(id="fire", parts=[("fire", 0.0, 5.0)], trans=FADE,
         cap=("fire", CORAL, "Rain out the fires", "...before they spread, but keep the campers' campfire lit")),
    dict(id="regatta", parts=[("regatta", 0.6, 3.4), ("castle", 1.2, 2.6)], trans=FADE,
         cap=("boat", SKY, "Race the regatta", "Boats to their buoys, and keep the sandcastle dry")),
    dict(id="heat", parts=[("heatwave", 0.4, 5.2)], trans=FADE,
         cap=("sunflower", BUTTER, "Survive the heatwave", "Beds dry fast, and sunflowers want sun, not shade")),
    dict(id="delights", parts=[("delight_lamb", 0.9, 1.7), ("delight_kite", 0.2, 1.7), ("delight_buoy", 0.4, 1.7),
                               ("delight_seal", 0.8, 1.7)], trans=FADE,
         cap=("stamp_flower", CORAL, "Every day hides a secret", "Splash the lamb, fly the kite, ring the bell, shower the seal")),
    dict(id="save", parts=[("save_day", 2.2, 7.6)], trans=FADE,
         cap=("stamp_sun", BUTTER, "Save the day", "Meet every need at once, then collect all three stamps")),
    dict(id="map", parts=[("map", 0.3, 2.7), ("postcard", 0.4, 2.4)], trans=FADE,
         cap=("heart", CORAL, "Twelve days, one summer", "From Rosa's first flower bed to her wedding with Tom")),
    dict(id="play", parts=[("touch", 0.3, 3.2), ("settings", 1.15, 1.6)], trans=FADE, cap_until=3.35,
         cap=("hand", LILAC, "Play your way", "Mouse, keyboard, gamepad or touch, with tap-to-rain and more")),
    # escalation montage: hard cuts on the beat (the bed is 92 bpm, so two beats = 1.304 s)
    dict(id="montage", parts=[("m_fire", 0.6, 1.304), ("m_boat", 0.5, 1.304), ("m_windmill", 0.1, 1.304),
                              ("m_rainbow", 3.1, 1.304), ("delight_campfire", 0.15, 1.304), ("w_sneeze", 5.9, 1.304),
                              ("save_day", 1.8, 1.304)], trans=("fadewhite", 0.4)),
    dict(id="end", kind="end", parts=[("end_bg", 0.5, 7.0)], trans=("fadewhite", 0.6)),
]

# the README's looping teaser: (shot, start, duration), crossfaded, the last back into the first
TEASER = [("rain_bloom", 1.2, 2.4), ("rainbow", 2.2, 2.4), ("gust_boat", 0.6, 2.4), ("w_rainbow", 1.2, 2.6)]
TEASER_W, TEASER_FPS, TEASER_X = 800, 20, 0.4

# per-beat level adjustments of the captured game sound (dB)
GAME_GAIN = {"title": -10.0, "end": -10.0, "montage": -1.0}


def run(cmd, **kw):
    kw.setdefault("check", True)
    return subprocess.run(cmd, **kw)


def magick(*args):
    run(["magick", *map(str, args)])


# ------------------------------------------------------------------------------------- capture
def read_shots(rec, log, shots):
    """Adds {shot name: (frames dir, first frame, end frame)} from one capture's log."""
    open_ = {}
    for line in open(log, errors="ignore"):
        m = re.match(r"\[PW\] mark (\d+) shot (\S+) (begin|end)", line.strip())
        if not m:
            continue
        f, name, kind = int(m.group(1)), m.group(2), m.group(3)
        if kind == "begin":
            open_[name] = f
        elif name in open_:
            shots[name] = (rec, open_.pop(name), f)
    return shots


def part_frames(shots, part):
    """(frames dir, first frame, frame count) for a (shot, start, duration) part."""
    name, start, dur = part
    if name not in shots:
        sys.exit(f"shot '{name}' is missing from the capture logs")
    rec, a, b = shots[name]
    first = a + int(round(start * FPS))
    n = int(round(dur * FPS))
    if first + n > b:
        print(f"  warning: {name} {start}+{dur}s runs {(first + n - b) / FPS:.2f}s past the shot's end; clamped")
        n = b - first
    return rec, first, n


# --------------------------------------------------------------------------------------- cards
def text_size(text, font, size):
    out = run(["magick", "-font", font, "-pointsize", str(size), f"label:{text}", "-format", "%w %h", "info:"],
              capture_output=True, text=True).stdout.split()
    return int(out[0]), int(out[1])


def shadowed(src, dst, opacity=28, blur=14, dy=10, color=INK):
    magick(src, "(", "+clone", "-background", color, "-shadow", f"{opacity}x{blur}+0+{dy}", ")", "+swap",
           "-background", "none", "-layers", "merge", "+repage", dst)


def caption_card(work, key, icon, accent, head, sub):
    """A cream 'cloud-paper' panel like the game's toasts: icon disc, headline, subline."""
    hw, hh = text_size(head, FONT_HEAD, 60)
    sw, sh = text_size(sub, FONT_BODY, 33)
    pad, disc = 30, 112
    cw = pad + disc + 28 + max(hw, sw) + pad + 14
    ch = 164
    base = os.path.join(work, f"cap_{key}_base.png")
    magick("-size", f"{cw}x{ch}", "xc:none",
           "-fill", PAPER, "-draw", f"roundrectangle 0,0 {cw - 1},{ch - 1} 44,44",
           "-fill", accent, "-draw", f"circle {pad + disc // 2},{ch // 2} {pad + disc // 2},{ch // 2 - disc // 2}",
           "-fill", "#FFFFFF66", "-draw", f"circle {pad + disc // 2},{ch // 2} {pad + disc // 2},{ch // 2 - disc // 2 + 8}",
           "(", os.path.join(ICONS, icon + ".png"), "-resize", "94x94", ")",
           "-geometry", f"+{pad + (disc - 94) // 2}+{(ch - 94) // 2}", "-composite",
           "-font", FONT_HEAD, "-pointsize", 60, "-fill", INK, "-annotate", f"+{pad + disc + 28}+{28 + 58}", head,
           "-font", FONT_BODY, "-pointsize", 33, "-fill", INK_SOFT, "-annotate", f"+{pad + disc + 28}+{28 + 58 + 50}", sub,
           base)
    out = os.path.join(work, f"cap_{key}.png")
    shadowed(base, out, 30, 16, 10)
    return out


def logo(work, scale=1.0):
    """The title-screen logo: Pip beside 'Pocket / Weather' in white Fredoka with a periwinkle outline."""
    out = os.path.join(work, f"logo_{scale:.2f}.png")
    if os.path.exists(out):
        return out
    size = int(190 * scale)
    words = []
    for word in ("Pocket", "Weather"):
        p = os.path.join(work, f"logo_{word}_{scale:.2f}.png")
        magick("-size", f"{int(1400 * scale)}x{int(320 * scale)}", "xc:none", "-font", FONT_HEAD, "-pointsize", size,
               "-gravity", "center", "-stroke", LOGO_OUTLINE, "-strokewidth", max(4, int(22 * scale)),
               "-fill", LOGO_OUTLINE, "-annotate", "+0+0", word, "-stroke", "none", "-fill", "white",
               "-annotate", "+0+0", word, "-trim", "+repage", p)
        words.append(p)
    (w1, h1), (w2, h2) = [tuple(map(int, run(["magick", "identify", "-format", "%w %h", p], capture_output=True,
                                             text=True).stdout.split())) for p in words]
    pip = int(250 * scale)
    gap = int(30 * scale)
    cw = pip + gap + max(w1, w2 + int(70 * scale)) + 40
    ch = h1 + h2 + int(10 * scale) + 40
    flat = os.path.join(work, f"logo_flat_{scale:.2f}.png")
    magick("-size", f"{cw}x{ch}", "xc:none",
           "(", os.path.join(ICONS, "pip.png"), "-resize", f"{pip}x{pip}", ")",
           "-geometry", f"+20+{max(0, (h1 - pip) // 2 + int(20 * scale))}", "-composite",
           words[0], "-geometry", f"+{20 + pip + gap}+20", "-composite",
           words[1], "-geometry", f"+{20 + pip + gap + int(70 * scale)}+{20 + h1 + int(10 * scale)}", "-composite",
           flat)
    shadowed(flat, out, 32, int(14 * scale) + 2, int(10 * scale))
    return out


def pill(work, key, text, font=FONT_BODY, size=40, fill=PAPER, ink=INK, padx=44, h=80):
    tw, _ = text_size(text, font, size)
    w = tw + padx * 2
    base = os.path.join(work, f"pill_{key}_base.png")
    magick("-size", f"{w}x{h}", "xc:none", "-fill", fill, "-draw", f"roundrectangle 0,0 {w - 1},{h - 1} {h // 2},{h // 2}",
           "-font", font, "-pointsize", size, "-fill", ink, "-gravity", "center", "-annotate", "+0+2", text, base)
    out = os.path.join(work, f"pill_{key}.png")
    shadowed(base, out, 26, 12, 8)
    return out


def pop_frames(work, key, src, frames=26):
    """A pop-in (ease-out-back from 55% to 100%) rendered onto a fixed canvas as an image sequence."""
    d = os.path.join(work, f"pop_{key}")
    if os.path.isdir(d):
        shutil.rmtree(d)
    os.makedirs(d)
    w, h = map(int, run(["magick", "identify", "-format", "%w %h", src], capture_output=True, text=True).stdout.split())
    for i in range(frames):
        k = i / (frames - 1)
        c1, c3 = 1.70158, 2.70158
        e = 1 + c3 * (k - 1) ** 3 + c1 * (k - 1) ** 2
        s = max(0.05, 0.55 + 0.45 * e)
        magick(src, "-resize", f"{s * 100:.2f}%", "-background", "none", "-gravity", "center", "-extent", f"{w}x{h}",
               os.path.join(d, f"p{i:03d}.png"))
    return os.path.join(d, "p%03d.png"), w, h


# ------------------------------------------------------------------------------------ segments
def ease_x(x0, t0, t1, dist=150):
    """Overlay x: slides in from the left with ease-out, and back out at t1."""
    return (f"{x0}-{dist}*pow(1-min(1,max(0,(t-{t0})/0.5)),3)"
            f"-{dist}*pow(min(1,max(0,(t-{t1})/0.4)),2)")


def render_segment(shots, beat, work, idx):
    out = os.path.join(work, f"seg{idx:02d}_{beat['id']}.mp4")
    parts = [part_frames(shots, p) for p in beat["parts"]]
    dur = sum(n for _, _, n in parts) / FPS
    if os.environ.get("PW_REUSE_SEGMENTS") and os.path.exists(out):    # audio-only iterations
        return out, dur, parts
    cmd = ["ffmpeg", "-y", "-v", "error"]
    for rec, first, n in parts:
        cmd += ["-framerate", str(FPS), "-start_number", str(first), "-t", f"{n / FPS + 0.1:.3f}",
                "-i", os.path.join(rec, "f%05d.jpg")]
    fil = []
    for i, (_, _, n) in enumerate(parts):
        fil.append(f"[{i}:v]trim=end_frame={n},setpts=PTS-STARTPTS,scale={W}:{H},setsar=1,format=yuv420p[p{i}]")
    if len(parts) > 1:
        fil.append("".join(f"[p{i}]" for i in range(len(parts))) + f"concat=n={len(parts)}:v=1:a=0[base]")
    else:
        fil.append("[p0]null[base]")
    nin = len(parts)
    last = "base"
    kind = beat.get("kind")

    if beat.get("cap"):
        icon, accent, head, sub = beat["cap"]
        card = caption_card(work, beat["id"], icon, accent, head, sub)
        ch = int(run(["magick", "identify", "-format", "%h", card], capture_output=True, text=True).stdout)
        t0, t1 = 0.35, min(dur, beat.get("cap_until", dur)) - 0.62
        cmd += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.3f}", "-i", card]
        fil.append(f"[{nin}:v]format=rgba,fade=t=in:st={t0}:d=0.4:alpha=1,fade=t=out:st={t1}:d=0.38:alpha=1[cap]")
        fil.append(f"[{last}][cap]overlay=x='{ease_x(44, t0, t1)}':y={H - ch - 34}:eval=frame:shortest=1[c]")
        nin += 1
        last = "c"

    if kind in ("title", "end"):
        scale = 1.0 if kind == "title" else 0.72
        lg = logo(work, scale)
        seq, lw, lh = pop_frames(work, f"{kind}_logo", lg)
        t_logo = 0.35 if kind == "title" else 0.45
        y_logo = 70 if kind == "title" else 120
        cmd += ["-framerate", str(FPS), "-i", seq]
        fil.append(f"[{nin}:v]format=rgba,setpts=PTS-STARTPTS+{t_logo}/TB[lg]")
        fil.append(f"[{last}][lg]overlay=x={(W - lw) // 2}:y={y_logo}:eof_action=repeat[l1]")
        nin += 1
        last = "l1"
        if kind == "title":
            items = [(pill(work, "tag", "a tiny cloud helps a miniature world through its day", size=42), 1.15,
                      y_logo + lh - 40)]
        else:
            items = [(pill(work, "endtag", "One summer. Twelve tiny worlds. One very small cloud.", size=40), 1.0,
                      y_logo + lh - 30),
                     (pill(work, "url", URL, font=FONT_HEAD, size=46, fill=CORAL, ink="#FFFFFF", h=92), 1.7, H - 230),
                     (pill(work, "builds", "Linux and web builds on GitHub", size=32, h=64), 2.2, H - 128)]
        for j, (img, t_in, y) in enumerate(items):
            iw = int(run(["magick", "identify", "-format", "%w", img], capture_output=True, text=True).stdout)
            cmd += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.3f}", "-i", img]
            fil.append(f"[{nin}:v]format=rgba,fade=t=in:st={t_in}:d=0.5:alpha=1[t{j}]")
            fil.append(f"[{last}][t{j}]overlay=x={(W - iw) // 2}:y='{y}+24*pow(1-min(1,max(0,(t-{t_in})/0.6)),3)'"
                       f":eval=frame:shortest=1[o{j}]")
            nin += 1
            last = f"o{j}"
        if kind == "end":
            # and out to black at the very end
            fil.append(f"[{last}]fade=t=out:st={dur - 0.9}:d=0.9:color=black[fo]")
            last = "fo"

    fil.append(f"[{last}]format=yuv420p[v]")
    cmd += ["-filter_complex", ";".join(fil), "-map", "[v]", "-r", str(FPS), "-frames:v", str(int(round(dur * FPS))),
            "-c:v", "libx264", "-preset", "medium", "-crf", "12", "-pix_fmt", "yuv420p", out]
    run(cmd)
    return out, dur, parts


# --------------------------------------------------------------------------------------- audio
def decode(path):
    raw = run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
              capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def db(x):
    return 10 ** (x / 20)


def place(track, clip, at, gain=1.0):
    i = int(round(at * SR))
    j = min(len(track), i + len(clip))
    if j > i:
        track[i:j] += clip[:j - i] * gain


def fades(clip, fin, fout):
    clip = clip.copy()
    n = len(clip)
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        clip[:a] *= np.sin(np.linspace(0, np.pi / 2, a))[:, None]
    if b > 0:
        clip[n - b:] *= np.cos(np.linspace(0, np.pi / 2, b))[:, None]
    return clip


def looped(music, seconds, offset=0.0):
    n = int(seconds * SR)
    reps = int(np.ceil((n + offset * SR) / len(music))) + 1
    return np.tile(music, (reps, 1))[int(offset * SR):int(offset * SR) + n]


def envelope(x, hold=0.25, smooth=0.12):
    """Peak envelope held for `hold` seconds either side (so the duck starts just before a sound
    lands and lingers after it) and smoothed without phase lag."""
    peak = maximum_filter1d(np.abs(x).max(axis=1), size=int(hold * SR))
    a = np.exp(-1 / (smooth * SR))
    return filtfilt([1 - a], [1, -a], peak)


def build_audio(timeline, starts, total, work):
    sources = {}
    game = np.zeros((int(total * SR) + SR, 2), np.float32)
    marks = {}
    for k, (beat, (start, dur, parts)) in enumerate(zip(timeline, starts)):
        clips = []
        for rec, first, n in parts:
            if rec not in sources:
                sources[rec] = np.fromfile(os.path.join(rec, "audio.f32"), dtype=np.float32).reshape(-1, 2)
            c = sources[rec][first * SPF:(first + n) * SPF]
            clips.append(fades(c, 0.006, 0.006))
        seg = np.concatenate(clips) if clips else np.zeros((0, 2), np.float32)
        tin = beat["trans"][1] if beat.get("trans") else 0.0
        tout = timeline[k + 1]["trans"][1] if k + 1 < len(timeline) and timeline[k + 1].get("trans") else 0.0
        seg = fades(seg, max(tin, 0.01), max(tout, 0.01))
        place(game, seg, start, db(GAME_GAIN.get(beat["id"], 0.0)))
        marks[beat["id"]] = (start, dur)

    title_t, title_d = marks["title"]
    end_t, end_d = marks["end"]
    mont_t, mont_d = marks["montage"]
    wedding = decode(os.path.join(MUSIC, "music_wedding.ogg"))
    morning = decode(os.path.join(MUSIC, "music_morning.ogg"))

    bed = np.zeros_like(game)
    # cold open: the wedding waltz, softly, fading as the title flashes in
    cold = looped(wedding, title_t + 0.3)
    place(bed, fades(cold, 1.2, 0.8), 0.0, db(-7))
    # from the title to the end: the morning theme (G major, the key every gameplay note was recorded in)
    main_len = end_t + end_d - title_t
    main = fades(looped(morning, main_len), 0.05, 3.0)
    gain = np.full(len(main), db(-2), np.float32)
    i0, i1 = int((mont_t - title_t) * SR), int((mont_t - title_t + mont_d) * SR)
    gain[i0:i1] = db(-1)                                      # the montage leans in
    place(bed, main * gain[:, None], title_t)
    # stingers: a rainbow run as the logo pops, the day-saved fanfare on the end card
    place(bed, decode(os.path.join(MUSIC, "sting_rainbow.ogg")), title_t + 0.3, db(-2))
    place(bed, decode(os.path.join(MUSIC, "sting_day_saved.ogg")), end_t + 0.4, db(-2))

    # duck the bed under the captured sound: up to 7 dB when the game speaks up
    env = envelope(game)
    env_db = 20 * np.log10(np.maximum(env, 1e-6))
    duck_db = np.clip((env_db + 30.0) * 0.4, 0, 7.0)
    for name in ("title", "end"):                             # cards: the music leads
        t, d = marks[name]
        duck_db[int(t * SR):int((t + d) * SR)] *= 0.3
    duck = db(-duck_db).astype(np.float32)
    bed *= duck[:, None]
    mix = game + bed
    mix = mix[:int(total * SR)]

    def rms_db(x):
        return 10 * np.log10(np.mean(x.astype(np.float64) ** 2) + 1e-12)
    print("  beat            game dB   music dB (after ducking)")
    for name, (t, d) in marks.items():
        a, b = int(t * SR), int((t + d) * SR)
        print(f"  {name:<14} {rms_db(game[a:b]):8.1f} {rms_db(bed[a:b]):9.1f}")
    # gentle fade at the very end
    tail = int(1.0 * SR)
    mix[-tail:] *= np.linspace(1, 0, tail)[:, None] ** 2
    path = os.path.join(work, "mix.wav")
    wavfile.write(path, SR, mix.astype(np.float32))
    wavfile.write(os.path.join(work, "stem_game.wav"), SR, game[:len(mix)].astype(np.float32))
    wavfile.write(os.path.join(work, "stem_music.wav"), SR, bed[:len(mix)].astype(np.float32))
    np.save(os.path.join(work, "duck_db.npy"), duck_db[::SR // 100])
    return path


def loudnorm_args(wav, target=-15.0, tp=-1.5, lra=11.0):
    out = run(["ffmpeg", "-hide_banner", "-i", wav, "-af", f"loudnorm=I={target}:TP={tp}:LRA={lra}:print_format=json",
               "-f", "null", "-"], capture_output=True, text=True).stderr
    m = json.loads(out[out.rindex("{"):out.rindex("}") + 1])
    return (f"loudnorm=I={target}:TP={tp}:LRA={lra}:measured_I={m['input_i']}:measured_TP={m['input_tp']}:"
            f"measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true")


# ------------------------------------------------------------------------------ README media
def make_teaser(shots, work, out):
    """A seamless 8-9 s animated WebP: the clips crossfade, and the last one into the first again."""
    parts = [part_frames(shots, p) for p in TEASER]
    parts.append(parts[0])
    cmd = ["ffmpeg", "-y", "-v", "error"]
    for rec, first, n in parts:
        cmd += ["-framerate", str(FPS), "-start_number", str(first), "-t", f"{n / FPS + 0.1:.3f}",
                "-i", os.path.join(rec, "f%05d.jpg")]
    fil = [f"[{i}:v]trim=end_frame={n},setpts=PTS-STARTPTS,scale={TEASER_W}:-2:flags=lanczos,fps={TEASER_FPS},"
           f"format=yuv420p[p{i}]" for i, (_, _, n) in enumerate(parts)]
    last, acc, x = "p0", parts[0][2] / FPS, TEASER_X
    for k in range(1, len(parts)):
        fil.append(f"[{last}][p{k}]xfade=transition=fade:duration={x}:offset={acc - x:.4f}[x{k}]")
        acc += parts[k][2] / FPS - x
        last = f"x{k}"
    loop_end = acc - parts[-1][2] / FPS + x          # the repeated first clip is fully in: back where we began
    fil.append(f"[{last}]trim=start={x}:end={loop_end:.4f},setpts=PTS-STARTPTS[v]")
    run(cmd + ["-filter_complex", ";".join(fil), "-map", "[v]", "-c:v", "libwebp_anim", "-q:v",
               os.environ.get("PW_TEASER_Q", "70"), "-compression_level", "6", "-loop", "0", "-an", out])
    print(f"{out}: {loop_end - x:.1f}s loop, {os.path.getsize(out) / 1e6:.1f} MB")


def make_poster(trailer, t, total, work, out):
    """A still from the title card with a play button, for the README to link to the MP4."""
    frame = os.path.join(work, "poster_frame.png")
    run(["ffmpeg", "-y", "-v", "error", "-ss", f"{t:.2f}", "-i", trailer, "-frames:v", "1", frame])
    base = os.path.join(work, "play_base.png")
    magick("-size", "250x250", "xc:none", "-fill", PAPER, "-draw", "circle 125,125 125,10",
           "-fill", CORAL, "-draw", "polygon 102,78 102,172 182,125", base)
    btn = os.path.join(work, "play.png")
    shadowed(base, btn, 40, 14, 10)
    label = pill(work, "watch", f"Watch the trailer  {int(total) // 60}:{int(total) % 60:02d}", font=FONT_HEAD, size=44,
                 h=88)
    magick(frame, btn, "-gravity", "center", "-geometry", "+0+150", "-composite",
           label, "-gravity", "center", "-geometry", "+0+330", "-composite",
           "-resize", "1280x720", "-quality", "88", out)
    print(f"{out}: {os.path.getsize(out) / 1e3:.0f} KB")


# ---------------------------------------------------------------------------------------- main
def main():
    if len(sys.argv) < 4 or len(sys.argv) % 2:
        sys.exit(__doc__)
    out = sys.argv[1]
    captures = list(zip(sys.argv[2::2], sys.argv[3::2]))
    work = os.path.join(os.path.dirname(os.path.abspath(captures[0][0])), "trailer_work")
    os.makedirs(work, exist_ok=True)
    shots = {}
    for rec, log in captures:
        read_shots(os.path.abspath(rec), log, shots)
    print(f"{len(shots)} shots in {len(captures)} capture(s)")

    segs = []
    for i, beat in enumerate(TIMELINE):
        path, dur, parts = render_segment(shots, beat, work, i)
        segs.append((path, dur, parts))
        print(f"  {beat['id']:<14} {dur:5.2f}s")

    # timeline: each beat starts where the previous ends, minus the crossfade into it
    starts, t = [], 0.0
    for beat, (_, dur, parts) in zip(TIMELINE, segs):
        tin = beat["trans"][1] if beat.get("trans") else 0.0
        start = t - tin
        starts.append((start, dur, parts))
        t = start + dur
    total = t
    print(f"trailer length {total:.2f}s")

    wav = build_audio(TIMELINE, starts, total, work)
    norm = loudnorm_args(wav)

    cmd = ["ffmpeg", "-y", "-v", "error"]
    for path, _, _ in segs:
        cmd += ["-i", path]
    cmd += ["-i", wav]
    fil, last, acc = [], "0:v", segs[0][1]
    for k in range(1, len(segs)):
        kind, d = TIMELINE[k]["trans"]
        nxt = f"x{k}"
        fil.append(f"[{last}][{k}:v]xfade=transition={kind}:duration={d}:offset={acc - d:.4f}[{nxt}]")
        acc += segs[k][1] - d
        last = nxt
    fil.append(f"[{last}]format=yuv420p[v]")
    fil.append(f"[{len(segs)}:a]{norm},aresample={SR}[a]")
    master = os.path.join(work, "trailer_master.mkv")
    run(cmd + ["-filter_complex", ";".join(fil), "-map", "[v]", "-map", "[a]", "-t", f"{total:.3f}",
               "-c:v", "libx264", "-preset", "fast", "-crf", "12", "-pix_fmt", "yuv420p", "-r", str(FPS),
               "-c:a", "pcm_f32le", master])
    # two-pass encode sized to land just under the size budget (README media must stay small)
    vbit = int(TARGET_MB * 8e6 / total - 192e3 - 24e3)
    final = os.path.join(work, "trailer_full.mp4")
    x264 = ["-c:v", "libx264", "-preset", "slow", "-profile:v", "high", "-pix_fmt", "yuv420p", "-r", str(FPS),
            "-b:v", str(vbit), "-maxrate", str(vbit * 2), "-bufsize", str(vbit * 4),
            "-passlogfile", os.path.join(work, "x264pass")]
    run(["ffmpeg", "-y", "-v", "error", "-i", master, "-map", "0:v", *x264, "-pass", "1", "-an", "-f", "null", "-"])
    run(["ffmpeg", "-y", "-v", "error", "-i", master, "-map", "0:v", "-map", "0:a", *x264, "-pass", "2",
         "-c:a", "aac", "-b:a", "192k", "-ar", str(SR), "-movflags", "+faststart", final])
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    shutil.copyfile(final, out)
    size = os.path.getsize(out) / 1e6
    print(f"{out}: {total:.1f}s, {size:.1f} MB")

    media = os.path.dirname(os.path.abspath(out))
    title_start = next(s for b, (s, _, _) in zip(TIMELINE, starts) if b["id"] == "title")
    make_poster(out, title_start + 3.2, total, work, os.path.join(media, "trailer-poster.jpg"))
    make_teaser(shots, work, os.path.join(media, "teaser.webp"))
    magick(logo(work, 0.6), "-strip", "-define", "png:compression-level=9", os.path.join(media, "logo.png"))

    # two frames from every beat, for checking
    check = os.path.join(work, "check")
    shutil.rmtree(check, ignore_errors=True)
    os.makedirs(check)
    for beat, (start, dur, _) in zip(TIMELINE, starts):
        for frac in (0.25, 0.6):
            run(["ffmpeg", "-y", "-v", "error", "-ss", f"{start + dur * frac:.2f}", "-i", out, "-frames:v", "1",
                 "-q:v", "3", os.path.join(check, f"{start + dur * frac:06.2f}_{beat['id']}.jpg")])
    with open(os.path.join(work, "beats.json"), "w") as f:
        json.dump([{"id": b["id"], "start": round(s, 2), "dur": round(d, 2),
                    "caption": b["cap"][2] if b.get("cap") else None} for b, (s, d, _) in zip(TIMELINE, starts)], f, indent=1)
    print(f"beat frames in {check}")


if __name__ == "__main__":
    main()
