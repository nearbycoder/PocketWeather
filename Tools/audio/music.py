"""Procedural music for Pocket Weather: six looping tracks plus stingers.

Each looping track is composed on a chord grid and exported with its chord timeline so the
game can pick raindrop notes that sit inside the current chord.
"""
import numpy as np

from dsp import (SR, Mix, bass, bell, brush, clap, epiano, glock, hat, kalimba, kick, marimba, master,
                 midi_hz, musicbox, pad, pluck, seed, shaker)
import dsp


class Song:
    def __init__(self, name, bpm, beats_per_bar, chords, bars_per_chord=2):
        self.name = name
        self.bpm = bpm
        self.bpb = beats_per_bar
        self.chords = chords                    # [(name, [midi...], bass_midi), ...]
        self.bars_per_chord = bars_per_chord
        self.beats_per_chord = beats_per_bar * bars_per_chord
        self.total_beats = len(chords) * self.beats_per_chord
        self.spb = 60.0 / bpm
        self.mix = Mix(self.total_beats * self.spb, tail=3.5)

    def t(self, beat):
        return beat * self.spb

    def add(self, x, beat, pan=0.0, gain=1.0, rev=0.2, jitter=0.006):
        j = (dsp.RNG.random() - 0.5) * 2 * jitter
        self.mix.add(x, max(0.0, self.t(beat) + j), pan, gain, rev)

    def chord_at(self, beat):
        i = int(beat // self.beats_per_chord) % len(self.chords)
        return self.chords[i]

    def each_chord(self):
        for i, c in enumerate(self.chords):
            yield i * self.beats_per_chord, c

    def meta(self):
        return {
            "name": self.name, "bpm": self.bpm, "beatsPerBar": self.bpb, "lengthBeats": self.total_beats,
            "chords": [{"start": i * self.beats_per_chord, "beats": self.beats_per_chord, "name": c[0], "notes": c[1]}
                       for i, c in enumerate(self.chords)],
        }

    def render(self, rms=-18.0):
        out = self.mix.render(loop=True)
        return master(out, target_rms_db=rms)


def vel(base=0.7, spread=0.12):
    return max(0.05, base + (dsp.RNG.random() - 0.5) * 2 * spread)


def melody(song, notes, inst, gain=1.0, pan=0.0, rev=0.3, octave=0):
    for (b, d, m) in notes:
        f = midi_hz(m + 12 * octave)
        dur = max(0.25, d * song.spb + 0.6)
        song.add(inst(f, vel(0.8, 0.08), dur=dur) if inst not in (pad,) else inst([f], d * song.spb), b, pan, gain, rev)


def phrase_melody(song, start, chords, scale, lo, hi, seed_, rest_last=True):
    """A generated B-section melody: for each chord a rhythm cell, strong beats on chord tones and
    weak beats on scale tones, moving mostly by step, every fourth chord ending on a long chord
    tone, so it stays in key and inside the harmony the rain notes also use. Its own RNG, so the
    rest of the arrangement's randomness is untouched."""
    rng = np.random.default_rng(seed_)
    bpc, bpb = song.beats_per_chord, song.bpb
    cells = ([[2, 1, 1, 2, 2], [1, 1, 2, 1, 1, 2], [3, 1, 2, 2], [1.5, .5, 2, 1.5, .5, 2], [2, 2, 1, 1, 2]] if bpc == 8 else
             [[2, 1, 3], [1, 1, 1, 3], [3, 2, 1], [2, 1, 2, 1], [1, 2, 3]])
    pcs_scale = sorted({n % 12 for n in scale})
    pool = lambda pcs: [m for m in range(lo, hi + 1) if m % 12 in pcs]
    prev = int(np.mean([lo, hi]))
    out = []
    for i, (_, notes, _) in enumerate(chords):
        b0 = start + i * bpc
        tones = pool({n % 12 for n in notes})
        end_of_phrase = i % 4 == 3
        if end_of_phrase:
            m = min(tones, key=lambda c: abs(c - prev))
            out.append((b0, bpc - (2 if rest_last else 0), m))
            prev = m
            continue
        t = 0.0
        for d in cells[rng.integers(len(cells))]:
            strong = (t % bpb) == 0
            cand = tones if strong else pool(pcs_scale)
            near = sorted(cand, key=lambda c: (abs(c - prev) + (2.5 if c == prev else 0), rng.random()))[:3]
            k = int(rng.choice(3, p=[0.6, 0.3, 0.1]))   # mostly the nearest note: steps, not leaps
            m = int(near[min(k, len(near) - 1)])
            if rng.random() < 0.12 and not strong:
                t += d
                continue        # a breath
            out.append((b0 + t, d, m))
            prev = m
            t += d
    return out


# ----------------------------------------------------------------------------- tracks

def morning():
    seed(11)
    G, D, Em, C = ("G", [55, 59, 62], 43), ("D", [50, 54, 57], 42), ("Em", [52, 55, 59], 40), ("C", [48, 52, 55], 36)
    s = Song("morning", 92, 4, [G, D, Em, C, G, D, C, D])
    arp = [0, 1, 2, 3, 2, 1, 2, 1]
    for start, (name, notes, root) in s.each_chord():
        tones = notes + [notes[0] + 12]
        for k in range(s.beats_per_chord * 2):
            b = start + k * 0.5
            m = tones[arp[k % 8]] + 12
            s.add(kalimba(midi_hz(m), vel(0.55 if k % 2 else 0.7)), b, pan=-0.25 + 0.5 * ((k % 4) / 3), gain=0.55, rev=0.25)
        for bar in range(2):
            b = start + bar * 4
            s.add(bass(midi_hz(root), vel(0.8), 1.2), b, gain=0.7, rev=0.05)
            s.add(bass(midi_hz(root + 7), vel(0.7), 0.9), b + 2, gain=0.6, rev=0.05)
            s.add(bass(midi_hz(root + 12), vel(0.5), 0.4), b + 3.5, gain=0.45, rev=0.05)
        s.add(pad([midi_hz(n) for n in notes], s.beats_per_chord * s.spb, vel(0.6, 0.02), attack=1.2, release=1.5, cutoff=1100), start, gain=0.55, rev=0.4)
    for b8 in range(s.total_beats * 2):
        b = b8 * 0.5
        s.add(shaker(0.9 if b8 % 2 else 0.45), b, pan=0.35, gain=0.55, rev=0.1, jitter=0.01)
        if b8 % 8 == 0 or b8 % 8 == 5:
            s.add(kick(0.55), b, gain=0.6, rev=0.02, jitter=0.0)
        if b8 % 4 == 2:
            s.add(brush(0.7), b, pan=-0.15, gain=0.6, rev=0.15)
    mel = [(2, 1, 71), (3, 1, 74), (4, 2, 79), (6, .5, 78), (6.5, .5, 76), (7, 1, 74), (8, 1.5, 69), (9.5, .5, 71), (10, 1, 69),
           (11, 1, 66), (12, 3, 74), (17, 1, 76), (18, 1, 79), (19, 1, 83), (20, 1.5, 81), (21.5, .5, 79), (22, 2, 76),
           (24, 1, 79), (25, 1, 76), (26, 1, 74), (27, 1, 72), (28, 2, 74), (32, 1, 71), (33, 1, 74), (34, 1, 79), (35, 1, 83),
           (36, 1, 81), (37, 1, 79), (38, 2, 74), (40, 1, 78), (41, 1, 81), (42, 1, 86), (43, 1, 81), (44, 3, 78),
           (48, 1, 76), (49, 1, 79), (50, 1.5, 84), (51.5, .5, 83), (52, 2, 81), (54, 1, 79), (55, 1, 76),
           (56, 1, 78), (57, 1, 76), (58, 1, 74), (59, 1, 69), (60, 3, 74)]
    melody(s, mel, marimba, gain=0.6, pan=0.15, rev=0.3)
    return s


def afternoon():
    seed(22)
    D = ("D", [50, 54, 57, 62], 38)
    Bm = ("Bm", [47, 50, 54, 59], 35)
    G = ("G", [43, 50, 55, 59], 43)
    A = ("A", [45, 52, 57, 61], 45)
    Fsm = ("F#m", [42, 49, 54, 57], 42)
    A_ = [D, Bm, G, A, D, Fsm, G, A]
    s = Song("afternoon", 100, 4, A_ + A_)   # A: kalimba then glockenspiel; B: e-piano tune
    strum = [(0, "d", 0.9), (1, "d", 0.6), (1.5, "u", 0.5), (2.5, "u", 0.55), (3, "d", 0.7), (3.5, "u", 0.5)]
    for start, (name, notes, root) in s.each_chord():
        voicing = [n + 12 for n in notes]
        for bar in range(2):
            for (off, d, v) in strum:
                b = start + bar * 4 + off
                order = voicing if d == "d" else list(reversed(voicing))
                for i, m in enumerate(order):
                    s.add(pluck(midi_hz(m), vel(v * 0.75, 0.08), 1.3, bright=0.55), b + i * 0.012 / s.spb, pan=-0.3, gain=0.45, rev=0.18, jitter=0.003)
            s.add(bass(midi_hz(root), vel(0.8), 1.0), start + bar * 4, gain=0.75, rev=0.05)
            s.add(bass(midi_hz(root + 7), vel(0.65), 0.6), start + bar * 4 + 2, gain=0.6, rev=0.05)
            s.add(bass(midi_hz(root + 12), vel(0.55), 0.3), start + bar * 4 + 3, gain=0.4, rev=0.05)
        s.add(pad([midi_hz(n + 12) for n in notes[1:]], s.beats_per_chord * s.spb, 0.5, attack=1.0, release=1.5, cutoff=1300), start, gain=0.4, rev=0.4)
    for b8 in range(s.total_beats * 2):
        b = b8 * 0.5
        s.add(hat(0.8 if b8 % 2 else 0.5), b, pan=0.3, gain=0.5, rev=0.05, jitter=0.008)
        if b8 % 8 in (0, 3, 4):
            s.add(kick(0.6), b, gain=0.6, rev=0.02, jitter=0)
        if b8 % 8 in (2, 6):
            s.add(clap(0.5), b, pan=0.1, gain=0.35, rev=0.25)
    mel = [(32, .5, 74), (32.5, .5, 78), (33, 1, 81), (34, 1.5, 86), (35.5, .5, 85), (36, 1, 83), (37, 1, 81), (38, 2, 78),
           (40, 1, 81), (41, .5, 78), (41.5, .5, 81), (42, 1, 85), (43, 1, 81), (44, 3, 78),
           (48, 1, 79), (49, 1, 83), (50, 1, 86), (51, 1, 83), (52, 1, 81), (53, 1, 79), (54, 2, 74),
           (56, 1, 76), (57, 1, 78), (58, 1, 81), (59, 1, 85), (60, 3, 81)]
    melody(s, mel, lambda f, v, dur: glock(f, v, dur=dur, bright=0.8), gain=0.55, pan=0.25, rev=0.35)
    counter = [(2, 1, 66), (3, 1, 69), (6, 2, 74), (10, 1, 71), (11, 1, 74), (14, 2, 78), (18, 1, 74), (19, 1, 71), (22, 2, 67),
               (26, 1, 69), (27, 1, 73), (30, 2, 76)]
    melody(s, counter, kalimba, gain=0.45, pan=0.3, rev=0.3)
    D_MAJOR = [50, 52, 54, 55, 57, 59, 61]
    melody(s, phrase_melody(s, 64, A_, D_MAJOR, 69, 83, 2202), lambda f, v, dur: epiano(f, v, dur=dur), gain=0.55, pan=0.2, rev=0.35)
    return s


def evening():
    seed(33)
    Am, F, C, G = ("Am", [57, 60, 64], 45), ("F", [53, 57, 60], 41), ("C", [55, 60, 64], 36), ("G", [55, 59, 62], 43)
    s = Song("evening", 84, 4, [Am, F, C, G, Am, F, G, G])
    for start, (name, notes, root) in s.each_chord():
        for bar in range(2):
            b = start + bar * 4
            for i, m in enumerate(notes):
                s.add(epiano(midi_hz(m), vel(0.6), dur=2.6 * s.spb), b + i * 0.01, pan=-0.2 + 0.2 * i, gain=0.55, rev=0.3)
            for i, m in enumerate(notes):
                s.add(epiano(midi_hz(m), vel(0.45), dur=0.8 * s.spb), b + 2.5 + i * 0.01, pan=-0.2 + 0.2 * i, gain=0.45, rev=0.3)
            s.add(bass(midi_hz(root), vel(0.75), 2.2), b, gain=0.7, rev=0.05)
            s.add(bass(midi_hz(root + 7 if bar == 0 else root + 12), vel(0.55), 0.8), b + 3, gain=0.45, rev=0.05)
        s.add(pad([midi_hz(n - 12) for n in notes], s.beats_per_chord * s.spb, 0.6, attack=1.5, release=2.0, cutoff=900), start, gain=0.6, rev=0.5)
    for b8 in range(s.total_beats * 2):
        b = b8 * 0.5
        s.add(shaker(0.7 if b8 % 2 else 0.3), b, pan=0.4, gain=0.4, rev=0.15, jitter=0.012)
        if b8 % 8 == 0:
            s.add(kick(0.45), b, gain=0.55, rev=0.02, jitter=0)
        if b8 % 8 == 4:
            s.add(brush(0.6), b, gain=0.5, rev=0.2)
    mel = [(4, 1, 76), (5, 1, 72), (6, 2, 69), (12, 1, 77), (13, 1, 76), (14, 2, 72), (20, 1, 79), (21, 1, 76), (22, 2, 72),
           (28, 1, 74), (29, 1, 71), (30, 2, 67), (36, .5, 76), (36.5, .5, 79), (37, 1, 81), (38, 2, 76), (44, 1, 77), (45, 1, 81),
           (46, 2, 84), (52, 1, 83), (53, 1, 79), (54, 2, 74), (60, 1, 71), (61, 1, 74), (62, 2, 79)]
    melody(s, mel, musicbox, gain=0.6, pan=0.2, rev=0.4)
    return s


def title():
    seed(44)
    F, Am, Bb, C, Dm = ("F", [53, 57, 60], 41), ("Am", [57, 60, 64], 45), ("Bb", [58, 62, 65], 46), ("C", [55, 60, 64], 48), ("Dm", [50, 53, 57], 38)
    s = Song("title", 72, 4, [F, Am, Bb, C, F, Dm, Bb, C])
    for start, (name, notes, root) in s.each_chord():
        tones = [notes[0], notes[1], notes[2], notes[0] + 12, notes[1] + 12, notes[2] + 12]
        pattern = [0, 2, 3, 4, 5, 4, 3, 2]
        for k in range(s.beats_per_chord * 2):
            m = tones[pattern[k % 8]] + 12
            s.add(musicbox(midi_hz(m), vel(0.5 if k % 2 else 0.62)), start + k * 0.5, pan=-0.3 + 0.6 * ((k % 8) / 7), gain=0.5, rev=0.45)
        s.add(pad([midi_hz(n) for n in notes] + [midi_hz(root)], s.beats_per_chord * s.spb, 0.65, attack=2.0, release=2.5, cutoff=1000, vib=1), start, gain=0.75, rev=0.55)
        s.add(bass(midi_hz(root), vel(0.6), 3.0), start, gain=0.55, rev=0.1)
        s.add(bass(midi_hz(root), vel(0.5), 2.5), start + 4, gain=0.45, rev=0.1)
    mel = [(32, 1.5, 72), (33.5, .5, 74), (34, 2, 77), (36, 1, 76), (37, 1, 74), (38, 2, 72), (40, 1.5, 74), (41.5, .5, 77), (42, 2, 81),
           (44, 2, 77), (48, 1, 77), (49, 1, 79), (50, 2, 82), (52, 1, 81), (53, 1, 79), (54, 2, 77), (56, 2, 79), (58, 1, 76), (59, 1, 72), (60, 3, 79)]
    melody(s, mel, lambda f, v, dur: epiano(f, v, dur=dur), gain=0.6, pan=0.1, rev=0.45)
    return s


def night():
    seed(55)
    Em, C, G, D = ("Em", [52, 55, 59], 40), ("C", [48, 52, 55], 36), ("G", [55, 59, 62], 43), ("D", [50, 54, 57], 38)
    s = Song("night", 70, 4, [Em, C, G, D, Em, C, G, D])
    for start, (name, notes, root) in s.each_chord():
        for bar in range(2):
            b = start + bar * 4
            pick = [(0, root + 12), (0.5, notes[1] + 12), (1, notes[2] + 12), (1.5, notes[1] + 12), (2, root + 19), (2.5, notes[2] + 12), (3, notes[1] + 12), (3.5, notes[2] + 12)]
            for (off, m) in pick:
                s.add(pluck(midi_hz(m), vel(0.55), 1.6, bright=0.35), b + off, pan=-0.25, gain=0.5, rev=0.35)
            s.add(bass(midi_hz(root), vel(0.6), 2.5), b, gain=0.6, rev=0.1)
        s.add(pad([midi_hz(n) for n in notes], s.beats_per_chord * s.spb, 0.55, attack=2.0, release=2.5, cutoff=800), start, gain=0.6, rev=0.6)
    mel = [(32, 1, 71), (33, 1, 74), (34, 2, 76), (36, 2, 79), (38, 2, 76), (40, 1, 72), (41, 1, 76), (42, 2, 79), (44, 3, 76),
           (48, 1, 74), (49, 1, 71), (50, 2, 67), (52, 2, 71), (54, 2, 74), (56, 1, 78), (57, 1, 74), (58, 2, 69), (60, 3, 71)]
    melody(s, mel, musicbox, gain=0.55, pan=0.25, rev=0.5)
    return s


def wedding():
    seed(66)
    F, C, Dm, Bb = ("F", [53, 57, 60], 41), ("C", [52, 55, 60], 36), ("Dm", [50, 53, 57], 38), ("Bb", [50, 53, 58], 34)
    A = [F, C, Dm, Bb, F, C, Bb, C]
    s = Song("wedding", 108, 3, A + A + A)   # A: glockenspiel tune; B: music box; C: the tune again on marimba
    for start, (name, notes, root) in s.each_chord():
        for bar in range(2):
            b = start + bar * 3
            s.add(bass(midi_hz(root), vel(0.8), 0.8), b, gain=0.75, rev=0.1)
            for beat in (1, 2):
                for i, m in enumerate(notes):
                    s.add(marimba(midi_hz(m + 12), vel(0.45), 0.5), b + beat + i * 0.004, pan=-0.2 + 0.2 * i, gain=0.4, rev=0.25)
        s.add(pad([midi_hz(n) for n in notes] + [midi_hz(notes[0] + 12)], s.beats_per_chord * s.spb, 0.7, attack=0.8, release=1.5, cutoff=1500, vib=1.5), start, gain=0.6, rev=0.5)
    mel = [(0, 2, 72), (2, 1, 77), (3, 2, 81), (5, 1, 79), (6, 2, 76), (8, 1, 79), (9, 3, 72), (12, 2, 74), (14, 1, 77), (15, 2, 81),
           (17, 1, 79), (18, 2, 77), (20, 1, 74), (21, 3, 70), (24, 2, 72), (26, 1, 77), (27, 2, 84), (29, 1, 81), (30, 2, 79),
           (32, 1, 76), (33, 3, 72), (36, 2, 74), (38, 1, 77), (39, 2, 82), (41, 1, 81), (42, 2, 79), (44, 1, 76), (45, 3, 79)]
    melody(s, mel, lambda f, v, dur: glock(f, v, dur=dur, bright=0.55), gain=0.65, pan=0.15, rev=0.4)
    F_MAJOR = [53, 55, 57, 58, 60, 62, 64]
    melody(s, phrase_melody(s, 48, A, F_MAJOR, 72, 86, 6606), musicbox, gain=0.6, pan=-0.15, rev=0.45)
    melody(s, [(b + 96, d, m) for (b, d, m) in mel], marimba, gain=0.6, pan=0.15, rev=0.4)
    for b in (0, 24, 48, 96, 120):
        s.add(bell(midi_hz(65), 0.6, 3.0), b, pan=0.4, gain=0.35, rev=0.5)
    return s


TRACKS = [morning, afternoon, evening, title, night, wedding]


# ----------------------------------------------------------------------------- stingers

def stinger_mix(length):
    return Mix(length, tail=2.5)


def day_saved():
    seed(77)
    m = stinger_mix(4.5)
    notes = [67, 71, 74, 79, 83, 86, 91]
    for i, n in enumerate(notes):
        m.add(glock(midi_hz(n), 0.8, 2.5), 0.07 * i, pan=-0.4 + 0.13 * i, gain=0.7, rev=0.35)
        m.add(kalimba(midi_hz(n - 12), 0.6), 0.07 * i, pan=0.3 - 0.1 * i, gain=0.5, rev=0.3)
    m.add(pad([midi_hz(n) for n in (55, 59, 62, 67, 71)], 2.2, 0.9, attack=0.25, release=1.8, cutoff=2500), 0.45, gain=1.1, rev=0.5)
    m.add(bell(midi_hz(79), 0.7, 3.0), 0.5, gain=0.4, rev=0.5)
    m.add(bass(midi_hz(43), 0.9, 2.0), 0.45, gain=0.8, rev=0.1)
    for i in range(12):
        m.add(glock(midi_hz(91 + (i % 3) * 2), 0.3, 0.8), 0.6 + 0.09 * i, pan=(i % 5 - 2) * 0.3, gain=0.25, rev=0.5)
    return master(m.render(loop=False), target_rms_db=-16)


def sunset():
    seed(78)
    m = stinger_mix(4.0)
    for i, n in enumerate((76, 74, 71, 67, 64)):
        m.add(musicbox(midi_hz(n), 0.7, 2.0), 0.28 * i, pan=0.3 - 0.15 * i, gain=0.6, rev=0.45)
    m.add(pad([midi_hz(n) for n in (52, 55, 59, 64)], 2.5, 0.7, attack=0.6, release=1.6, cutoff=900), 0.3, gain=0.9, rev=0.5)
    return master(m.render(loop=False), target_rms_db=-19)


def level_start():
    seed(79)
    m = stinger_mix(2.2)
    for i, n in enumerate((60, 64, 67, 72, 76, 79)):
        m.add(pluck(midi_hz(n), 0.6, 1.5, bright=0.8), 0.05 * i, pan=-0.5 + 0.2 * i, gain=0.6, rev=0.4)
    return master(m.render(loop=False), target_rms_db=-19)


def stamp():
    seed(80)
    m = stinger_mix(1.6)
    t = np.arange(int(round(0.25 * SR))) / SR
    thump = np.sin(2 * np.pi * 90 * t * (1 - t)) * np.exp(-t * 22) + dsp.lowpass(dsp.noise(0.25), 1200) * np.exp(-t * 60) * 0.6
    m.add(thump, 0, gain=0.9, rev=0.1)
    m.add(glock(midi_hz(84), 0.7, 1.5), 0.05, gain=0.5, rev=0.4)
    m.add(glock(midi_hz(91), 0.6, 1.5), 0.13, gain=0.45, rev=0.4)
    return master(m.render(loop=False), target_rms_db=-16)


def need_met():
    seed(81)
    m = stinger_mix(1.4)
    m.add(glock(midi_hz(84), 0.8, 1.4, bright=0.9), 0, pan=-0.1, gain=0.6, rev=0.4)
    m.add(glock(midi_hz(91), 0.8, 1.4, bright=0.9), 0.09, pan=0.1, gain=0.6, rev=0.4)
    m.add(kalimba(midi_hz(79), 0.6), 0.0, gain=0.4, rev=0.3)
    for i in range(5):
        m.add(glock(midi_hz(96 + i * 2), 0.25, 0.5), 0.15 + 0.04 * i, pan=(i - 2) * 0.25, gain=0.25, rev=0.5)
    return master(m.render(loop=False), target_rms_db=-17)


def rainbow():
    seed(82)
    m = stinger_mix(2.8)
    notes = [60, 64, 67, 71, 72, 76, 79, 83, 84, 88]
    for i, n in enumerate(notes):
        m.add(pluck(midi_hz(n), 0.55, 1.8, bright=0.9), 0.06 * i, pan=-0.6 + 0.13 * i, gain=0.5, rev=0.45)
        m.add(glock(midi_hz(n + 12), 0.3, 1.2), 0.06 * i + 0.02, pan=0.6 - 0.13 * i, gain=0.3, rev=0.5)
    m.add(pad([midi_hz(n) for n in (60, 64, 67, 71)], 1.5, 0.6, attack=0.3, release=1.2, cutoff=3000), 0.2, gain=0.6, rev=0.6)
    return master(m.render(loop=False), target_rms_db=-17)


STINGERS = {"day_saved": day_saved, "sunset": sunset, "level_start": level_start, "stamp": stamp,
            "need_met": need_met, "rainbow": rainbow}
