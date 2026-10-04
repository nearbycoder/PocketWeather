"""DSP building blocks and instruments for Pocket Weather's procedural audio (numpy + scipy).

Everything is mono float64 at SR unless noted; `Mix` places sounds into a stereo bus with a
reverb send and handles seamless loop wrapping.
"""
import math
import wave

import numpy as np
from scipy import signal

SR = 44100
RNG = np.random.default_rng(1234)


def seed(n):
    global RNG
    RNG = np.random.default_rng(n)


def midi_hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def t_axis(dur):
    return np.arange(int(round(dur * SR))) / SR


def noise(dur):
    return RNG.standard_normal(int(round(dur * SR)))


def pink(dur):
    n = int(round(dur * SR))
    w = RNG.standard_normal(n)
    # Voss-ish via filtering white noise (Paul Kellet's economy filter)
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, w) * 3.5


def env_ad(dur, attack=0.005, decay=1.0, curve=1.0):
    t = t_axis(dur)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-np.maximum(t - attack, 0) / max(decay, 1e-4) * curve)
    return a * d


def env_adsr(dur, a=0.01, d=0.1, s=0.7, r=0.2, hold=None):
    n = int(round(dur * SR))
    t = np.arange(n) / SR
    hold = dur - r if hold is None else hold
    e = np.where(t < a, t / max(a, 1e-4),
                 np.where(t < a + d, 1 - (1 - s) * (t - a) / max(d, 1e-4), s))
    rel = np.clip((t - hold) / max(r, 1e-4), 0, 1)
    return e * (1 - rel)


def fade(x, fin=0.002, fout=0.01):
    n = len(x)
    i = min(n, int(round(fin * SR)))
    o = min(n, int(round(fout * SR)))
    if i > 0:
        x[:i] *= np.linspace(0, 1, i)
    if o > 0:
        x[n - o:] *= np.linspace(1, 0, o)
    return x


def lowpass(x, fc, order=2):
    fc = min(fc, SR * 0.45)
    b, a = signal.butter(order, fc / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def highpass(x, fc, order=2):
    b, a = signal.butter(order, fc / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    hi = min(hi, SR * 0.45)
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def resonator(x, fc, q):
    """Two-pole resonant bandpass (formant)."""
    w0 = 2 * math.pi * fc / SR
    alpha = math.sin(w0) / (2 * q)
    b = [alpha, 0, -alpha]
    a = [1 + alpha, -2 * math.cos(w0), 1 - alpha]
    return signal.lfilter(b, a, x)


def sweep_phase(f0, f1, dur, curve="exp"):
    t = t_axis(dur)
    if curve == "exp" and f0 > 0 and f1 > 0:
        f = f0 * (f1 / f0) ** (t / dur)
    else:
        f = f0 + (f1 - f0) * (t / dur)
    return 2 * math.pi * np.cumsum(f) / SR, f


def osc_phase_from_freq(f):
    return 2 * math.pi * np.cumsum(f) / SR


def saw_bl(phase_inc_f, dur, harmonics=None):
    """Band-limited sawtooth from a frequency array (additive)."""
    f = np.asarray(phase_inc_f)
    ph = osc_phase_from_freq(f)
    fmax = float(np.max(f))
    nh = harmonics or max(1, int((SR * 0.45) / max(fmax, 1)))
    nh = min(nh, 60)
    out = np.zeros_like(ph)
    for k in range(1, nh + 1):
        out += np.sin(k * ph) / k
    return out * (2 / math.pi)


def pulse_train(f, dur, width=0.3, nh=40):
    f = np.asarray(f)
    ph = osc_phase_from_freq(f)
    fmax = float(np.max(f))
    nh = min(nh, max(1, int(SR * 0.45 / max(fmax, 1))))
    out = np.zeros_like(ph)
    for k in range(1, nh + 1):
        out += np.sin(math.pi * k * width) / k * np.cos(k * ph)
    return out


def normalize(x, peak=0.9):
    m = np.max(np.abs(x)) or 1.0
    return x * (peak / m)


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def pad_to(x, n):
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


# ----------------------------------------------------------------------------- instruments

def kalimba(f, vel=1.0, dur=1.8):
    t = t_axis(dur)
    x = np.sin(2 * math.pi * f * t) * np.exp(-t * 2.6)
    x += 0.22 * np.sin(2 * math.pi * f * 5.93 * t) * np.exp(-t * 16)
    x += 0.10 * np.sin(2 * math.pi * f * 2.0 * t + 0.3) * np.exp(-t * 6)
    click = bandpass(noise(dur), 1800, 6000) * np.exp(-t * 500) * 0.15
    x = (x + click) * np.clip(t / 0.002, 0, 1)
    return fade(x * vel * 0.6, 0.0005, 0.05)


def marimba(f, vel=1.0, dur=1.2):
    t = t_axis(dur)
    x = np.sin(2 * math.pi * f * t) * np.exp(-t * 4.2)
    x += 0.32 * np.sin(2 * math.pi * f * 3.98 * t) * np.exp(-t * 16)
    x += 0.08 * np.sin(2 * math.pi * f * 9.9 * t) * np.exp(-t * 45)
    x += lowpass(noise(dur), 3000) * np.exp(-t * 300) * 0.08
    x *= np.clip(t / 0.0015, 0, 1)
    return fade(x * vel * 0.6, 0.0005, 0.05)


def glock(f, vel=1.0, dur=2.0, bright=1.0):
    t = t_axis(dur)
    modes = ((1.0, 1.0, 1.4), (2.756, 0.45 * bright, 3.8), (5.404, 0.22 * bright, 8.0), (8.933, 0.1 * bright, 14.0))
    x = np.zeros_like(t)
    for ratio, amp, dec in modes:
        if f * ratio < SR * 0.45:
            x += amp * np.sin(2 * math.pi * f * ratio * t) * np.exp(-t * dec)
    x *= np.clip(t / 0.001, 0, 1)
    return fade(x * vel * 0.45, 0.0005, 0.05)


def musicbox(f, vel=1.0, dur=1.8):
    t = t_axis(dur)
    x = np.sin(2 * math.pi * f * t) * np.exp(-t * 2.2)
    x += 0.35 * np.sin(2 * math.pi * f * 3.01 * t) * np.exp(-t * 7)
    x += 0.18 * np.sin(2 * math.pi * f * 5.98 * t) * np.exp(-t * 15)
    x += 0.06 * np.sin(2 * math.pi * f * 1.003 * t + 1.0) * np.exp(-t * 2.2)
    x *= np.clip(t / 0.0008, 0, 1)
    return fade(x * vel * 0.5, 0.0003, 0.05)


def epiano(f, vel=1.0, dur=1.6, release=0.3):
    t = t_axis(dur + release)
    idx = 1.6 * np.exp(-t * 3.5) * (0.6 + 0.4 * vel) + 0.25
    x = np.sin(2 * math.pi * f * t + idx * np.sin(2 * math.pi * f * t))
    x += 0.05 * np.sin(2 * math.pi * f * 14.0 * t) * np.exp(-t * 35)
    amp = np.exp(-t * 1.2) * np.clip(t / 0.003, 0, 1)
    amp *= np.clip(1 - (t - dur) / release, 0, 1)
    x = x * amp
    x = 0.8 * x + 0.2 * np.roll(x, int(round(0.0006 * SR)))
    return fade(x * vel * 0.4, 0.001, 0.05)


def pluck(f, vel=1.0, dur=1.5, bright=0.6, decay=0.996):
    """Karplus-Strong string (ukulele/guitar) via an IIR comb."""
    n = int(round(dur * SR))
    N = max(2, int(round(SR / f)))
    burst = noise(N / SR + 0.001)[:N]
    burst = lowpass(burst, 2000 + 6000 * bright)
    x = np.zeros(n)
    x[:N] = burst
    a = np.zeros(N + 2)
    a[0] = 1
    a[N] = -decay * 0.5
    a[N + 1] = -decay * 0.5
    y = signal.lfilter([1.0], a, x)
    y = lowpass(y, 5000)
    env = np.clip(np.arange(n) / (0.002 * SR), 0, 1)
    return fade(y * env * vel * 0.5, 0.0005, 0.08)


def bass(f, vel=1.0, dur=0.9):
    t = t_axis(dur)
    x = np.sin(2 * math.pi * f * t) + 0.25 * np.sin(4 * math.pi * f * t) + 0.08 * np.sin(6 * math.pi * f * t)
    x *= np.exp(-t * 2.2) * np.clip(t / 0.006, 0, 1)
    x = lowpass(x, 900)
    return fade(x * vel * 0.5, 0.001, 0.06)


def pad(freqs, dur, vel=1.0, attack=0.6, release=1.2, cutoff=1400, detune=0.006, vib=0.0):
    t = t_axis(dur + release)
    out = np.zeros_like(t)
    for f in freqs:
        for d in (-detune, 0, detune):
            fr = f * (1 + d) * (1 + vib * 0.003 * np.sin(2 * math.pi * 5.2 * t + f))
            out += saw_bl(fr, dur + release, harmonics=18)
    out = lowpass(out, cutoff, 2)
    env = np.clip(t / attack, 0, 1) * np.clip(1 - (t - dur) / release, 0, 1)
    out *= env
    return out * vel * 0.12 / max(1, len(freqs) ** 0.5)


def shaker(vel=1.0):
    dur = 0.12
    t = t_axis(dur)
    x = bandpass(noise(dur), 4500, 11000) * np.exp(-t * 45) * np.clip(t / 0.006, 0, 1)
    return x * vel * 0.25


def hat(vel=1.0):
    dur = 0.08
    t = t_axis(dur)
    return highpass(noise(dur), 8000) * np.exp(-t * 90) * vel * 0.2


def kick(vel=1.0):
    dur = 0.35
    ph, _ = sweep_phase(120, 45, dur)
    t = t_axis(dur)
    x = np.sin(ph) * np.exp(-t * 9) + lowpass(noise(dur), 2000) * np.exp(-t * 200) * 0.2
    return fade(x * vel * 0.8, 0.0005, 0.02)


def brush(vel=1.0):
    dur = 0.25
    t = t_axis(dur)
    x = bandpass(noise(dur), 1500, 6000) * np.exp(-t * 14) * np.clip(t / 0.01, 0, 1)
    return x * vel * 0.22


def clap(vel=1.0):
    dur = 0.3
    t = t_axis(dur)
    x = np.zeros_like(t)
    for k, off in enumerate((0, 0.009, 0.018)):
        i = int(round(off * SR))
        b = bandpass(noise(0.02), 900, 4000) * np.exp(-np.arange(int(round(0.02 * SR))) / SR * 150)
        x[i:i + len(b)] += b * (0.7 + 0.3 * k)
    x += bandpass(noise(dur), 800, 3500) * np.exp(-t * 18) * 0.5 * (t > 0.02)
    return x * vel * 0.4


def bell(f, vel=1.0, dur=3.0):
    t = t_axis(dur)
    partials = ((0.5, 0.6, 0.6), (1.0, 1.0, 1.0), (1.183, 0.5, 1.6), (1.506, 0.45, 2.0), (2.0, 0.4, 2.2), (2.514, 0.25, 3.0), (2.662, 0.2, 3.5), (3.011, 0.15, 4.0))
    x = np.zeros_like(t)
    for r, a, d in partials:
        if f * r < SR * 0.45:
            x += a * np.sin(2 * math.pi * f * r * t + r) * np.exp(-t * d)
    x *= np.clip(t / 0.002, 0, 1)
    return fade(x * vel * 0.3, 0.0005, 0.1)


# ----------------------------------------------------------------------------- voice (formants)

VOWELS = {
    "a": (800, 1200, 2500), "e": (500, 1900, 2600), "i": (320, 2300, 3000),
    "o": (500, 900, 2400), "u": (330, 800, 2300), "ae": (660, 1700, 2400), "oo": (300, 870, 2240),
}


def voice(f0_curve, vowel_curve, dur, breath=0.05, vib=0.0, bright=1.0, nasal=0.0):
    """f0_curve: array of Hz per sample; vowel_curve: list of (time, vowel) keyframes."""
    n = int(round(dur * SR))
    f0 = pad_to(np.asarray(f0_curve, dtype=float), n)
    f0[f0 <= 0] = 1
    if vib > 0:
        f0 = f0 * (1 + vib * np.sin(2 * math.pi * 6.0 * np.arange(n) / SR))
    src = pulse_train(f0, dur, width=0.25 + 0.1 * bright)
    src += noise(dur) * breath
    # time-varying formants: render each vowel segment and crossfade
    keys = sorted(vowel_curve)
    out = np.zeros(n)
    times = [k[0] for k in keys] + [dur]
    for i, (tk, v) in enumerate(keys):
        F = VOWELS[v]
        y = resonator(src, F[0], 6) * 1.0 + resonator(src, F[1], 9) * 0.6 * bright + resonator(src, F[2], 12) * 0.25 * bright
        if nasal > 0:
            y += resonator(src, 1400, 14) * nasal
        w = np.zeros(n)
        a, b = int(round(tk * SR)), int(round(times[i + 1] * SR))
        xf = int(round(0.03 * SR))
        w[max(0, a - xf):min(n, b + xf)] = 1
        # smooth window edges
        if a - xf > 0:
            w[a - xf:a + xf] = np.linspace(0, 1, 2 * xf)[: len(w[a - xf:a + xf])]
        if b + xf < n:
            w[b - xf:b + xf] = np.linspace(1, 0, 2 * xf)[: len(w[b - xf:b + xf])]
        out += y * w
    return out


def curve(points, dur):
    """Piecewise-linear curve from [(t, value), ...] sampled per sample."""
    n = int(round(dur * SR))
    t = np.arange(n) / SR
    ts = [p[0] for p in points]
    vs = [p[1] for p in points]
    return np.interp(t, ts, vs)


# ----------------------------------------------------------------------------- reverb & mixing

def reverb_ir(dur=2.2, damp=0.45, seed_n=7):
    rng = np.random.default_rng(seed_n)
    n = int(round(dur * SR))
    t = np.arange(n) / SR
    out = []
    for ch in range(2):
        w = rng.standard_normal(n)
        env = np.exp(-t / (dur / 6.5))
        lo = lowpass(w, 3500)
        lo2 = lowpass(w, 1200)
        mixc = np.clip(t / dur * 1.6, 0, 1)
        x = (lo * (1 - mixc * damp) + lo2 * mixc * damp) * env
        # early reflections
        for k, (dt, g) in enumerate(((0.011, 0.5), (0.019, 0.4), (0.027, 0.33), (0.041, 0.25), (0.053, 0.2))):
            i = int(round((dt + ch * 0.003 * k) * SR))
            if i < n:
                x[i] += g
        x[:int(round(0.004 * SR))] = 0
        out.append(x / np.sqrt(np.sum(x ** 2)))
    return np.stack(out)


class Mix:
    """Stereo bus with a reverb send. Times in seconds. Loop wrapping for seamless loops."""

    def __init__(self, length, tail=3.0):
        self.length = length
        self.n = int(round(length * SR))
        self.tail = int(round(tail * SR))
        self.dry = np.zeros((2, self.n + self.tail))
        self.send = np.zeros((2, self.n + self.tail))

    def add(self, x, t, pan=0.0, gain=1.0, rev=0.2):
        i = int(round(t * SR))
        if i >= self.n + self.tail or len(x) == 0:
            return
        x = x[: self.n + self.tail - i] * gain
        l = math.cos((pan + 1) * math.pi / 4)
        r = math.sin((pan + 1) * math.pi / 4)
        self.dry[0, i:i + len(x)] += x * l
        self.dry[1, i:i + len(x)] += x * r
        if rev > 0:
            self.send[0, i:i + len(x)] += x * l * rev
            self.send[1, i:i + len(x)] += x * r * rev

    def render(self, loop=True, ir=None, wet=1.0):
        ir = reverb_ir() if ir is None else ir
        wetsig = np.stack([signal.fftconvolve(self.send[c], ir[c])[: self.n + self.tail] for c in range(2)])
        out = self.dry + wetsig * wet
        if loop:
            # wrap the ringing tail back to the start for a seamless loop
            out[:, :self.tail] += out[:, self.n:self.n + self.tail]
            out = out[:, :self.n]
        return out


def master(x, target_rms_db=-17.0, peak_db=-1.0, drive=1.3):
    rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    x = x * (10 ** (target_rms_db / 20) / rms)
    x = soft_clip(x, drive)
    peak = np.max(np.abs(x)) + 1e-9
    lim = 10 ** (peak_db / 20)
    if peak > lim:
        x = x * (lim / peak)
    return x


def write_wav(path, x, stereo=None):
    x = np.asarray(x)
    if x.ndim == 1:
        x = x[None, :]
    x = np.clip(x, -1, 1)
    data = (x.T * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(data.shape[1])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def loop_crossfade(x, xf=1.0):
    """Make a mono/stereo loop seamless by crossfading the last `xf` seconds into the start."""
    n = int(round(xf * SR))
    x = np.array(x, dtype=float)
    if x.ndim == 1:
        x = x[None, :]
    body = x[:, :-n].copy()
    tail = x[:, -n:]
    ramp = np.linspace(0, 1, n)
    body[:, :n] = body[:, :n] * ramp + tail * (1 - ramp)
    return body
