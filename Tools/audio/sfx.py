"""Procedural sound effects, raindrop note samples and ambience loops for Pocket Weather."""
import math

import numpy as np

import dsp
from dsp import (SR, bandpass, bell, curve, env_ad, fade, glock, highpass, kalimba, lowpass, marimba, midi_hz,
                 noise, normalize, pink, pluck, resonator, seed, sweep_phase, t_axis, voice, Mix, master)


def T(d):
    return t_axis(d)


# ----------------------------------------------------------------------------- raindrop notes (C5 = MIDI 72)

def note_kalimba():
    return normalize(kalimba(midi_hz(72), 1.0, 1.6), 0.85)


def note_marimba():
    return normalize(marimba(midi_hz(72), 1.0, 1.0), 0.85)


def note_glock():
    return normalize(glock(midi_hz(84), 1.0, 1.6, bright=0.7), 0.8)


def note_water():
    """A glassy 'bloop' (pitch rises like a drop into a glass of water)."""
    d = 0.45
    ph, f = sweep_phase(midi_hz(72) * 0.82, midi_hz(72) * 1.05, d, "exp")
    t = T(d)
    x = np.sin(ph) * np.exp(-t * 9) + 0.25 * np.sin(2 * ph) * np.exp(-t * 20)
    x *= np.clip(t / 0.002, 0, 1)
    return normalize(fade(x, 0.0005, 0.05), 0.8)


def note_wood():
    d = 0.35
    t = T(d)
    f = midi_hz(72)
    x = np.sin(2 * math.pi * f * t) * np.exp(-t * 18) + 0.4 * np.sin(2 * math.pi * f * 2.5 * t) * np.exp(-t * 40)
    x += bandpass(noise(d), 1500, 5000) * np.exp(-t * 250) * 0.3
    return normalize(fade(x, 0.0005, 0.03), 0.8)


def note_pluck():
    return normalize(pluck(midi_hz(72), 1.0, 1.4, bright=0.75), 0.8)


NOTES = {"note_kalimba": note_kalimba, "note_marimba": note_marimba, "note_glock": note_glock,
         "note_water": note_water, "note_wood": note_wood, "note_pluck": note_pluck}


# ----------------------------------------------------------------------------- loops

def rain_loop():
    seed(101)
    d = 6.0
    x = lowpass(highpass(pink(d + 1), 400), 6000) * 0.25
    # dense small taps
    n = len(x)
    for _ in range(900):
        i = dsp.RNG.integers(0, n - 2000)
        L = dsp.RNG.integers(200, 900)
        f = dsp.RNG.uniform(2500, 8000)
        tt = np.arange(L) / SR
        x[i:i + L] += np.sin(2 * math.pi * f * tt) * np.exp(-tt * dsp.RNG.uniform(200, 500)) * dsp.RNG.uniform(0.05, 0.25)
    y = dsp.loop_crossfade(x, 1.0)[0]
    return normalize(y, 0.6)


def drink_loop():
    seed(102)
    d = 3.0
    x = bandpass(pink(d + 0.5), 200, 1800) * 0.4
    n = len(x)
    for _ in range(70):
        i = dsp.RNG.integers(0, n - 6000)
        L = dsp.RNG.integers(1500, 4000)
        f0 = dsp.RNG.uniform(250, 600)
        ph, _ = sweep_phase(f0, f0 * dsp.RNG.uniform(1.6, 2.4), L / SR)
        L = len(ph)
        tt = np.arange(L) / SR
        x[i:i + L] += np.sin(ph) * np.exp(-tt * 25) * dsp.RNG.uniform(0.2, 0.5)
    y = dsp.loop_crossfade(x, 0.5)[0]
    return normalize(y, 0.6)


def fire_loop():
    seed(103)
    d = 5.0
    x = lowpass(pink(d + 1), 500) * 0.5
    x *= 0.7 + 0.3 * np.sin(2 * math.pi * 0.7 * T(d + 1) + 1)
    n = len(x)
    for _ in range(260):
        i = dsp.RNG.integers(0, n - 2000)
        L = dsp.RNG.integers(80, 600)
        burst = highpass(noise(L / SR), 1500) * np.exp(-np.arange(L) / SR * 400) * dsp.RNG.uniform(0.2, 1.0)
        x[i:i + L] += burst
    y = dsp.loop_crossfade(x, 0.8)[0]
    return normalize(y, 0.6)


def windmill_loop():
    seed(104)
    d = 4.0
    t = T(d + 0.5)
    x = lowpass(pink(d + 0.5), 900) * (0.6 + 0.4 * np.sin(2 * math.pi * 1.0 * t))
    for k in range(4):
        i = int(round((k + 0.3) * SR))
        L = int(round(0.35 * SR))
        f = curve([(0, 180), (0.35, 140)], 0.35)
        cr = dsp.pulse_train(f, 0.35, 0.1) * env_ad(0.35, 0.05, 0.2)
        x[i:i + L] += resonator(cr, 900, 5) * 0.3
    y = dsp.loop_crossfade(x, 0.5)[0]
    return normalize(y, 0.5)


LOOPS = {"rain_loop": rain_loop, "drink_loop": drink_loop, "fire_loop": fire_loop, "windmill_loop": windmill_loop}


# ----------------------------------------------------------------------------- one-shots

def gust():
    seed(201)
    d = 1.1
    t = T(d)
    inhale = bandpass(noise(d), 600, 2500) * np.clip(t / 0.15, 0, 1) * (t < 0.16) * 0.25
    ph_f = curve([(0, 400), (0.15, 400), (0.35, 1800), (1.1, 500)], d)
    blow = np.zeros_like(t)
    w = noise(d)
    # moving bandpass approximated by mixing fixed bands with time weights
    for lo, hi, c in ((300, 900, 500), (700, 1800, 1200), (1500, 3500, 2200)):
        band = bandpass(w, lo, hi)
        weight = np.exp(-((ph_f - c) / 600) ** 2)
        blow += band * weight
    env = np.clip((t - 0.14) / 0.06, 0, 1) * np.exp(-np.maximum(t - 0.2, 0) * 3.2)
    x = inhale + blow * env * 1.2
    return normalize(fade(x, 0.002, 0.1), 0.85)


def puff():
    seed(202)
    d = 0.4
    t = T(d)
    x = bandpass(noise(d), 500, 2000) * np.clip(t / 0.02, 0, 1) * np.exp(-t * 9)
    return normalize(fade(x), 0.5)


def full():
    seed(203)
    m = Mix(1.2, tail=1.5)
    m.add(glock(midi_hz(88), 0.9, 1.5, bright=0.9), 0, gain=0.8, rev=0.3)
    m.add(glock(midi_hz(95), 0.7, 1.2, bright=0.9), 0.06, gain=0.6, rev=0.3)
    t = T(0.25)
    m.add(np.sin(2 * math.pi * 300 * t * (1 + t * 4)) * np.exp(-t * 20) * 0.6, 0, gain=0.5, rev=0.1)
    return master(m.render(loop=False).mean(axis=0), -16)


def bloom():
    seed(204)
    d = 0.3
    ph, _ = sweep_phase(500, 1400, 0.08)
    t = T(0.08)
    pop = np.sin(ph) * np.exp(-t * 40)
    x = np.zeros(int(round(d * SR)))
    x[:len(pop)] += pop
    x += bandpass(noise(d), 2000, 8000) * np.exp(-T(d) * 60) * 0.25
    x += kalimba(midi_hz(84), 0.5, d)[:len(x)] * 0.6
    return normalize(fade(x), 0.8)


def oops():
    seed(205)
    d = 0.7
    ph, f = sweep_phase(900, 300, d, "exp")
    t = T(d)
    vib = 1 + 0.04 * np.sin(2 * math.pi * 18 * t)
    x = np.sin(ph * vib) * env_ad(d, 0.01, 0.5) + 0.3 * np.sin(2 * ph) * env_ad(d, 0.01, 0.3)
    return normalize(fade(x), 0.75)


def bell_ship():
    seed(206)
    m = Mix(2.5, tail=2)
    m.add(bell(midi_hz(76), 0.9, 2.5), 0, gain=0.8, rev=0.3)
    m.add(bell(midi_hz(76), 0.6, 2.0), 0.35, gain=0.5, rev=0.3)
    return master(m.render(loop=False).mean(axis=0), -17)


def sail():
    seed(207)
    d = 0.6
    t = T(d)
    flap = np.zeros_like(t)
    for k in range(4):
        i = int(round((0.05 + 0.09 * k) * SR))
        L = int(round(0.06 * SR))
        flap[i:i + L] += bandpass(noise(0.06), 200, 1500) * np.exp(-np.arange(L) / SR * 50) * (1 - k * 0.2)
    whoosh = bandpass(noise(d), 400, 1500) * env_ad(d, 0.05, 0.25) * 0.4
    return normalize(fade(flap + whoosh), 0.7)


def splash():
    seed(208)
    d = 0.6
    t = T(d)
    x = bandpass(noise(d), 600, 6000) * env_ad(d, 0.005, 0.15)
    for _ in range(12):
        i = dsp.RNG.integers(0, int(round(0.3 * SR)))
        L = 2000
        f = dsp.RNG.uniform(600, 1600)
        ph, _ = sweep_phase(f, f * 1.8, L / SR)
        L = len(ph)
        x[i:i + L] += np.sin(ph) * np.exp(-np.arange(L) / SR * 40) * 0.3
    return normalize(fade(x), 0.75)


def sizzle():
    seed(209)
    d = 1.2
    t = T(d)
    x = highpass(noise(d), 3000) * env_ad(d, 0.02, 0.5)
    x *= 0.7 + 0.3 * np.abs(np.sin(2 * math.pi * 23 * t))
    return normalize(fade(x), 0.6)


def flap():
    seed(210)
    d = 0.35
    x = np.zeros(int(round(d * SR)))
    for k in range(3):
        i = int(round(0.08 * k * SR))
        L = int(round(0.07 * SR))
        x[i:i + L] += bandpass(noise(0.07), 300, 2500) * np.exp(-np.arange(L) / SR * 40)
    return normalize(fade(x), 0.6)


def creak():
    seed(211)
    d = 0.8
    f = curve([(0, 220), (0.4, 160), (0.8, 190)], d)
    x = dsp.pulse_train(f, d, 0.08) * env_ad(d, 0.05, 0.35)
    x = resonator(x, 800, 4) + resonator(x, 1700, 6) * 0.4
    return normalize(fade(x), 0.55)


def ui_pop():
    seed(212)
    d = 0.18
    ph, _ = sweep_phase(600, 900, 0.06)
    t = T(0.06)
    x = np.zeros(int(round(d * SR)))
    x[:len(t)] += np.sin(ph) * np.exp(-t * 50)
    x += marimba(midi_hz(79), 0.4, d)[:len(x)] * 0.5
    return normalize(fade(x), 0.7)


def ui_tick():
    seed(213)
    d = 0.08
    t = T(d)
    x = np.sin(2 * math.pi * 1800 * t) * np.exp(-t * 90) + bandpass(noise(d), 3000, 8000) * np.exp(-t * 200) * 0.3
    return normalize(fade(x), 0.4)


def ui_back():
    seed(214)
    d = 0.2
    ph, _ = sweep_phase(700, 450, 0.1)
    t = T(0.1)
    x = np.zeros(int(round(d * SR)))
    x[:len(t)] += np.sin(ph) * np.exp(-t * 35)
    return normalize(fade(x), 0.6)


def whoosh():
    seed(215)
    d = 0.9
    t = T(d)
    x = bandpass(noise(d), 300, 2500) * np.sin(np.clip(t / d, 0, 1) * math.pi) ** 2
    return normalize(fade(x), 0.6)


def mote():
    """Collecting a vapour mote: soft bubbly blip (pitched by the game)."""
    seed(216)
    d = 0.35
    x = kalimba(midi_hz(72), 0.8, d)
    ph, _ = sweep_phase(700, 1400, 0.05)
    t = T(0.05)
    x[:len(t)] += np.sin(ph) * np.exp(-t * 60) * 0.4
    return normalize(fade(x), 0.75)


def thunder():
    seed(217)
    d = 2.5
    t = T(d)
    x = lowpass(pink(d), 300) * env_ad(d, 0.05, 0.9) + lowpass(noise(d), 1500) * env_ad(d, 0.005, 0.12) * 0.5
    return normalize(fade(x), 0.8)


def applause():
    seed(218)
    d = 3.5
    x = np.zeros(int(round(d * SR)))
    for _ in range(600):
        tt = dsp.RNG.uniform(0, d - 0.1)
        dens = math.sin(min(1.0, tt / d) * math.pi) ** 0.5
        if dsp.RNG.random() > dens:
            continue
        i = int(round(tt * SR))
        L = int(round(0.02 * SR))
        x[i:i + L] += bandpass(noise(0.02), dsp.RNG.uniform(800, 1500), dsp.RNG.uniform(2500, 5000)) * np.exp(-np.arange(L) / SR * 180)
    return normalize(fade(x, 0.05, 0.6), 0.6)


def church_bells():
    seed(219)
    m = Mix(5.0, tail=3)
    seq = [77, 72, 74, 65, 77, 74, 72, 77]
    for i, n in enumerate(seq):
        m.add(bell(midi_hz(n), 0.8, 3.0), 0.55 * i, pan=-0.3 + 0.1 * i, gain=0.6, rev=0.5)
    return master(m.render(loop=False).mean(axis=0), -17)


def cheer():
    """Crowd 'hooray': layered voices + applause."""
    seed(220)
    d = 2.5
    x = applause()[: int(round(d * SR))] * 0.7
    for k in range(9):
        f0 = dsp.RNG.uniform(180, 420)
        st = dsp.RNG.uniform(0, 0.25)
        dd = dsp.RNG.uniform(0.6, 1.0)
        f = curve([(0, f0), (dd * 0.3, f0 * 1.35), (dd, f0 * 1.1)], dd)
        v = voice(f, [(0, "e"), (dd * 0.35, "a")], dd, breath=0.15, vib=0.02) * env_ad(dd, 0.05, 0.4)
        i = int(round(st * SR))
        x[i:i + len(v)] += v * 0.08
    return normalize(fade(x, 0.02, 0.5), 0.8)


def aww():
    seed(221)
    d = 1.6
    x = np.zeros(int(round(d * SR)))
    for k in range(7):
        f0 = dsp.RNG.uniform(200, 380)
        f = curve([(0, f0 * 1.15), (0.5, f0), (1.6, f0 * 0.92)], d)
        v = voice(f, [(0, "a"), (0.7, "o")], d, breath=0.2, vib=0.015) * env_ad(d, 0.12, 0.7)
        x += v * 0.12
    return normalize(fade(x, 0.02, 0.3), 0.7)


# ---- creatures (toy-like) ----------------------------------------------------------

def sheep():
    seed(230)
    d = 0.75
    f = curve([(0, 520), (0.1, 600), (0.75, 470)], d)
    v = voice(f, [(0, "e"), (0.15, "ae")], d, breath=0.08, vib=0.07, bright=1.2) * env_ad(d, 0.03, 0.4)
    trem = 1 + 0.35 * np.sin(2 * math.pi * 11 * T(d))
    return normalize(fade(v * trem), 0.75)


def cow():
    seed(231)
    d = 1.1
    f = curve([(0, 150), (0.3, 175), (1.1, 120)], d)
    v = voice(f, [(0, "oo"), (0.4, "o")], d, breath=0.04, vib=0.01, bright=0.8) * env_ad(d, 0.08, 0.7)
    return normalize(fade(v), 0.75)


def cat():
    seed(232)
    d = 0.6
    f = curve([(0, 600), (0.2, 900), (0.6, 650)], d)
    v = voice(f, [(0, "i"), (0.15, "a"), (0.4, "u")], d, breath=0.06, vib=0.02) * env_ad(d, 0.03, 0.35)
    return normalize(fade(v), 0.7)


def hiss():
    seed(233)
    d = 0.6
    return normalize(fade(highpass(noise(d), 2500) * env_ad(d, 0.02, 0.3)), 0.5)


def dog():
    seed(234)
    d = 0.5
    x = np.zeros(int(round(d * SR)))
    for k in range(2):
        dd = 0.16
        f = curve([(0, 420), (0.04, 520), (0.16, 300)], dd)
        v = voice(f, [(0, "a")], dd, breath=0.2, bright=1.1) * env_ad(dd, 0.005, 0.07)
        i = int(round(k * 0.22 * SR))
        x[i:i + len(v)] += v
    return normalize(fade(x), 0.75)


def pig():
    seed(235)
    d = 0.45
    f = curve([(0, 240), (0.2, 300), (0.45, 200)], d)
    v = voice(f, [(0, "o"), (0.2, "i")], d, breath=0.35, bright=0.9, nasal=0.6) * env_ad(d, 0.02, 0.2)
    grunt = 1 + 0.6 * np.sign(np.sin(2 * math.pi * 28 * T(d)))
    return normalize(fade(v * grunt), 0.7)


def donkey():
    seed(236)
    d = 1.2
    x = np.zeros(int(round(d * SR)))
    for k, (f0, f1, vw) in enumerate(((700, 820, "i"), (260, 200, "a"))):
        dd = 0.5
        f = curve([(0, f0), (dd, f1)], dd)
        v = voice(f, [(0, vw)], dd, breath=0.2, vib=0.05, bright=1.1) * env_ad(dd, 0.04, 0.3)
        i = int(round(k * 0.55 * SR))
        x[i:i + len(v)] += v
    return normalize(fade(x), 0.7)


def duck():
    seed(237)
    d = 0.3
    f = curve([(0, 300), (0.3, 260)], d)
    v = voice(f, [(0, "ae")], d, breath=0.15, bright=1.3, nasal=1.0) * env_ad(d, 0.01, 0.15)
    return normalize(fade(v), 0.7)


def frog():
    seed(238)
    d = 0.4
    x = np.zeros(int(round(d * SR)))
    for k in range(2):
        dd = 0.15
        f = curve([(0, 140), (dd, 180)], dd)
        v = voice(f, [(0, "o")], dd, breath=0.1, bright=0.9) * env_ad(dd, 0.01, 0.06)
        v *= 1 + np.sign(np.sin(2 * math.pi * 40 * T(dd)))
        i = int(round(k * 0.18 * SR))
        x[i:i + len(v)] += v
    return normalize(fade(x), 0.7)


def gull():
    seed(239)
    d = 0.7
    f = curve([(0, 1100), (0.15, 1400), (0.7, 900)], d)
    v = voice(f, [(0, "i"), (0.2, "ae")], d, breath=0.1, vib=0.03, bright=1.2) * env_ad(d, 0.02, 0.3)
    return normalize(fade(v), 0.6)


def robin():
    seed(240)
    d = 0.8
    x = np.zeros(int(round(d * SR)))
    for k in range(5):
        dd = 0.1
        f0 = dsp.RNG.uniform(2500, 4000)
        ph, _ = sweep_phase(f0, f0 * dsp.RNG.uniform(0.7, 1.4), dd)
        tt = T(dd)
        i = int(round(k * 0.14 * SR))
        x[i:i + len(tt)] += np.sin(ph) * np.sin(np.pi * tt / dd)
    return normalize(fade(x), 0.55)


def seal():
    seed(241)
    d = 0.6
    f = curve([(0, 380), (0.2, 460), (0.6, 330)], d)
    v = voice(f, [(0, "a"), (0.3, "o")], d, breath=0.15, bright=1.0) * env_ad(d, 0.02, 0.3)
    return normalize(fade(v * (1 + 0.5 * np.sin(2 * math.pi * 14 * T(d)))), 0.7)


# ---- people & Pip --------------------------------------------------------------------

def blips(f0s, vowels, gap=0.085, dur=0.07, breath=0.05, bright=1.1):
    total = gap * len(f0s) + 0.2
    x = np.zeros(int(round(total * SR)))
    for k, (f0, vw) in enumerate(zip(f0s, vowels)):
        f = curve([(0, f0), (dur, f0 * 1.06)], dur)
        v = voice(f, [(0, vw)], dur, breath=breath, bright=bright) * env_ad(dur, 0.006, 0.03)
        i = int(round(k * gap * SR))
        x[i:i + len(v)] += v
    return normalize(fade(x), 0.7)


def person_happy():
    seed(250)
    return blips([420, 500, 620], ["a", "e", "i"])


def person_upset():
    seed(251)
    return blips([380, 330, 260], ["o", "a", "u"], gap=0.1)


def pip_yay():
    seed(252)
    return blips([700, 820, 980], ["a", "e", "i"], gap=0.07, dur=0.06, bright=1.3)


def pip_eep():
    seed(253)
    d = 0.25
    f = curve([(0, 900), (0.08, 1300), (0.25, 1100)], d)
    v = voice(f, [(0, "i")], d, breath=0.05, bright=1.3) * env_ad(d, 0.01, 0.1)
    return normalize(fade(v), 0.7)


def pip_hmm():
    seed(254)
    d = 0.4
    f = curve([(0, 520), (0.2, 600), (0.4, 480)], d)
    v = voice(f, [(0, "u")], d, breath=0.05, bright=0.9) * env_ad(d, 0.03, 0.2)
    return normalize(fade(v), 0.6)


def pip_slurp():
    seed(255)
    d = 0.5
    f = curve([(0, 400), (0.5, 900)], d)
    v = voice(f, [(0, "oo")], d, breath=0.3, bright=0.8) * env_ad(d, 0.05, 0.3)
    return normalize(fade(v), 0.6)


def achoo():
    seed(256)
    d = 2.2
    x = np.zeros(int(round(d * SR)))
    # ah... ah...
    for k, st in enumerate((0.0, 0.55)):
        dd = 0.35
        f = curve([(0, 600 + k * 80), (dd, 760 + k * 120)], dd)
        v = voice(f, [(0, "a")], dd, breath=0.25, bright=1.1) * env_ad(dd, 0.06, 0.25)
        i = int(round(st * SR))
        x[i:i + len(v)] += v * 0.7
    # CHOO!
    st = 1.2
    dd = 0.45
    burst = bandpass(noise(dd), 1500, 7000) * env_ad(dd, 0.005, 0.08)
    f = curve([(0, 900), (dd, 500)], dd)
    v = voice(f, [(0, "u")], dd, breath=0.4, bright=1.2) * env_ad(dd, 0.01, 0.2)
    i = int(round(st * SR))
    x[i:i + len(v)] += v + burst * 1.2
    return normalize(fade(x), 0.85)


SFX = {
    "gust": gust, "puff": puff, "full": full, "bloom": bloom, "oops": oops, "bell": bell_ship, "sail": sail,
    "splash": splash, "sizzle": sizzle, "flap": flap, "creak": creak, "ui_pop": ui_pop, "ui_tick": ui_tick,
    "ui_back": ui_back, "whoosh": whoosh, "mote": mote, "thunder": thunder, "applause": applause,
    "church_bells": church_bells, "cheer": cheer, "aww": aww, "sheep": sheep, "cow": cow, "cat": cat, "hiss": hiss,
    "dog": dog, "pig": pig, "donkey": donkey, "duck": duck, "frog": frog, "gull": gull, "robin": robin, "seal": seal,
    "person_happy": person_happy, "person_upset": person_upset, "pip_yay": pip_yay, "pip_eep": pip_eep,
    "pip_hmm": pip_hmm, "pip_slurp": pip_slurp, "achoo": achoo,
}


# ----------------------------------------------------------------------------- ambience (stereo loops)

def chirp(rng):
    d = rng.uniform(0.06, 0.16)
    f0 = rng.uniform(2800, 5200)
    ph, _ = sweep_phase(f0, f0 * rng.uniform(0.6, 1.5), d)
    tt = T(d)
    return np.sin(ph + 0.6 * np.sin(2 * math.pi * rng.uniform(30, 90) * tt)) * np.sin(np.pi * tt / d) ** 2


def loop_bed(noise_fn, length, xf=1.5):
    """A noise bed exactly `length` seconds long whose end flows into its start (no seam)."""
    return dsp.loop_crossfade(noise_fn(length + xf), xf)[0][: int(round(length * dsp.SR))]


def amb_meadow(seed_n=301, birds=40, length=30.0):
    seed(seed_n)
    rng = dsp.RNG
    m = Mix(length, tail=2)
    t = T(length)
    for ch, pan in ((0, -0.7), (1, 0.7)):
        # gusts of breeze on periods that divide the loop (15 s and 10 s), so the loop doesn't jump
        breeze = loop_bed(lambda d: lowpass(pink(d), 700), length) * (0.55 + 0.45 * np.sin(2 * math.pi * t / (length / (2 + ch)) + ch)) * 0.25
        m.add(breeze, 0, pan=pan, gain=1, rev=0)
    for _ in range(birds):
        st = rng.uniform(0, length)
        n = rng.integers(2, 6)
        pan = rng.uniform(-0.9, 0.9)
        g = rng.uniform(0.05, 0.16)
        for k in range(n):
            m.add(chirp(rng), st + k * rng.uniform(0.09, 0.18), pan=pan, gain=g, rev=0.4)
    out = m.render(loop=True)
    return master(out, -26, peak_db=-3)


def amb_sea(length=30.0):
    seed(302)
    rng = dsp.RNG
    m = Mix(length, tail=2)
    t = T(length)
    for ch, pan in ((0, -0.6), (1, 0.6)):
        w = loop_bed(lambda d: bandpass(pink(d), 150, 2500), length)
        # wave swells every 7.5 s and 10 s: both divide the 30 s loop
        swell = 0.25 + 0.75 * (0.5 + 0.5 * np.sin(2 * math.pi * t / (length / (4 - ch)) + ch * 2)) ** 3
        m.add(w * swell * 0.35, 0, pan=pan, gain=1, rev=0)
    for _ in range(5):
        m.add(gull() * 0.25, rng.uniform(0, length - 1), pan=rng.uniform(-0.8, 0.8), gain=0.5, rev=0.5)
    return master(m.render(loop=True), -25, peak_db=-3)


def amb_night(length=30.0):
    seed(303)
    rng = dsp.RNG
    m = Mix(length, tail=2)
    for ch, pan in ((0, -0.6), (1, 0.6)):
        m.add(loop_bed(lambda d: lowpass(pink(d), 500), length) * 0.15, 0, pan=pan, rev=0)
    for c in range(6):
        f = rng.uniform(4200, 5200)
        pan = rng.uniform(-0.8, 0.8)
        period = rng.uniform(0.6, 1.1)
        for k in range(int(length / period)):
            st = k * period + rng.uniform(0, 0.05)
            dd = 0.12
            tt = T(dd)
            trill = np.sin(2 * math.pi * f * tt) * (0.5 + 0.5 * np.sign(np.sin(2 * math.pi * 55 * tt))) * np.sin(np.pi * tt / dd)
            m.add(trill, st, pan=pan, gain=0.04, rev=0.3)
    for _ in range(3):
        st = rng.uniform(0, length - 2)
        dd = 0.5
        tt = T(dd)
        hoo = np.sin(2 * math.pi * 420 * tt * (1 - 0.05 * tt)) * np.sin(np.pi * tt / dd) ** 2
        m.add(hoo * 0.12, st, pan=rng.uniform(-0.5, 0.5), rev=0.6)
        m.add(hoo * 0.1, st + 0.7, pan=rng.uniform(-0.5, 0.5), rev=0.6)
    return master(m.render(loop=True), -27, peak_db=-3)


def amb_village():
    return amb_meadow(304, birds=55)


AMBIENCE = {"amb_meadow": amb_meadow, "amb_sea": amb_sea, "amb_night": amb_night, "amb_village": amb_village}
