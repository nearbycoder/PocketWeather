#!/usr/bin/env node
// Plays the web build on emulated phones and tablets with real touch events, and measures what a
// phone would feel: whether it reaches the title, how much memory the tab peaks at, the download,
// the frame rate, and whether every step can be done by touch. Uses the blog's Playwright
// (PLAYWRIGHT_CORE=/path overrides it) and its patched WebKit (WEBKIT=/path/pw_run.sh) for iOS.
//
//   node Tools/mobile_check.mjs [--device iphone|iphone-portrait|ipad|pixel|pixel-portrait|desktop] [--dir Builds/pages] [--out <dir>] [--title-only]
//
//   iphone, ipad      headless WebKit with Playwright's "iPhone 15 landscape" / "iPad Pro 11 landscape"
//                     profiles (iPhone UA, coarse pointer, WebGL 2, no WebGPU)
//   pixel             headless Chromium with "Pixel 7 landscape"
//   *-portrait        the same phone held upright (the page's "turn sideways" card, then Play anyway)
//   desktop           headless Chromium, 1280x720, mouse only: the on-screen controls must never show
//
// Serves --dir (default Builds/pages, as GitHub Pages serves it) under /PocketWeather/ on a port
// of its own and counts the bytes each file sends. The session: boot to the title, tap it, tap
// Start on Day 1's postcard, then play by touch: drag Pip, hold still to rain, flick a gust, and
// with the on-screen buttons hold Rain with one finger while another steers (multi-touch), tap
// Gust, tap Pause and Resume. Memory is sampled every half second: the web content process's
// resident memory (the browser's own processes under this script, nothing else), the game's
// WebAssembly heap, and the bytes the game has handed WebGL (textures, render targets, buffers).
// iOS's per-tab limit isn't enforced by desktop WebKit, so the numbers are what to judge it by.
//
// --title-only stops at the title after 20 s, for comparing memory between builds over several runs.
// --recovery checks the page's answer to a visit killed while on screen (a phone short of memory):
// the running mark must be set once the game runs, and a load that finds it (pretended here, as a
// kill can't be) must say so on the loading card and start Auto graphics on Low.
//
// Writes summary.json, console.txt and screenshots to --out (default Recordings/mobile/<device>),
// prints a summary, and exits non-zero when a check fails.
import { createServer } from "node:http";
import { createRequire } from "node:module";
import { mkdirSync, writeFileSync, readFileSync, statSync, readdirSync, existsSync } from "node:fs";
import { dirname, join, extname, normalize } from "node:path";
import { homedir } from "node:os";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : def; };
const DEVICE = opt("--device", "iphone");
const DIR = opt("--dir", join(ROOT, "Builds", "pages"));
const OUT = opt("--out", join(ROOT, "Recordings", "mobile", DEVICE));
const require = createRequire(import.meta.url);
const pw = require(process.env.PLAYWRIGHT_CORE || join(homedir(), "Sites/blog/node_modules/playwright-core"));
const WEBKIT = process.env.WEBKIT || join(homedir(), ".cache/webkit-libs/webkit-2359/pw_run.sh");
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const t0 = Date.now();
const stamp = () => ((Date.now() - t0) / 1000).toFixed(1).padStart(6);

const PROFILES = {
  iphone: { engine: "webkit", device: "iPhone 15 landscape" },
  "iphone-portrait": { engine: "webkit", device: "iPhone 15" },
  ipad: { engine: "webkit", device: "iPad Pro 11 landscape" },
  "ipad-portrait": { engine: "webkit", device: "iPad Pro 11" },
  pixel: { engine: "chromium", device: "Pixel 7 landscape" },
  "pixel-portrait": { engine: "chromium", device: "Pixel 7" },
  desktop: { engine: "chromium", device: null },
};
const prof = PROFILES[DEVICE];
if (!prof) { console.error(`unknown --device ${DEVICE}; one of ${Object.keys(PROFILES).join(", ")}`); process.exit(2); }
const TOUCH = prof.device !== null;
// Headless browsers have no notch: the page reads --pw-test-inset-* as well as env(safe-area-inset-*),
// so the device's own insets are pretended (iPhone 15 and iPad Pro 11 as Safari reports them)
const INSETS = { iphone: [59, 59, 0, 21], "iphone-portrait": [0, 0, 59, 34], ipad: [0, 0, 0, 20], "ipad-portrait": [0, 0, 24, 20], pixel: [0, 0, 0, 0], "pixel-portrait": [0, 0, 0, 0], desktop: [0, 0, 0, 0] }[DEVICE];
const PORTRAIT = /portrait/.test(DEVICE);
const TITLE_ONLY = args.includes("--title-only");
const RECOVERY = args.includes("--recovery");

// ---------------------------------------------------------------- static server under /PocketWeather/
const TYPES = { ".html": "text/html", ".js": "application/javascript", ".css": "text/css", ".png": "image/png", ".jpg": "image/jpeg", ".json": "application/json" };
const served = new Map();   // path -> bytes
const server = createServer((req, res) => {
  const u = decodeURIComponent(new URL(req.url, "http://x").pathname);
  if (!u.startsWith("/PocketWeather/")) { res.writeHead(404); res.end(); return; }
  let p = normalize(join(DIR, u.slice("/PocketWeather/".length) || "index.html"));
  if (!p.startsWith(normalize(DIR))) { res.writeHead(403); res.end(); return; }
  if (existsSync(p) && statSync(p).isDirectory()) p = join(p, "index.html");
  if (!existsSync(p)) { res.writeHead(404); res.end(); return; }
  const body = readFileSync(p);
  served.set(u, (served.get(u) || 0) + body.length);
  res.writeHead(200, { "Content-Type": TYPES[extname(p)] || "application/octet-stream", "Content-Length": body.length });
  res.end(body);
});
await new Promise((r) => server.listen(0, "127.0.0.1", r));
const URL_ = `http://127.0.0.1:${server.address().port}/PocketWeather/`;

// ---------------------------------------------------------------- what the page counts for us
// WebGL allocations (textures, renderbuffers, buffers) by object, the WebAssembly memory, frames
const INIT = `(() => {
  const st = window.pwMeasure = { gl: 0, glPeak: 0, tex: 0, rb: 0, buf: 0, frames: 0, wasm: 0, wasmPeak: 0, mems: [] };
  const size = new WeakMap();   // object -> [kind, bytes]
  const kinds = { tex: 0, rb: 0, buf: 0 };
  const set = (obj, kind, bytes) => {
    if (!obj) return;
    const old = size.get(obj); if (old) { kinds[old[0]] -= old[1]; st.gl -= old[1]; }
    size.set(obj, [kind, bytes]); kinds[kind] += bytes; st.gl += bytes;
    st.tex = kinds.tex; st.rb = kinds.rb; st.buf = kinds.buf; if (st.gl > st.glPeak) st.glPeak = st.gl;
  };
  const add = (obj, kind, bytes) => { const old = size.get(obj); set(obj, kind, (old ? old[1] : 0) + bytes); };
  const free = (obj) => { const old = obj && size.get(obj); if (old) { kinds[old[0]] -= old[1]; st.gl -= old[1]; size.delete(obj); st.tex = kinds.tex; st.rb = kinds.rb; st.buf = kinds.buf; } };
  const BPP = { 0x8058: 4, 0x8C43: 4, 0x881A: 8, 0x8814: 16, 0x822F: 8, 0x8230: 16, 0x822D: 4, 0x822E: 8, 0x8229: 1, 0x822B: 2, 0x8C3A: 4, 0x8059: 4, 0x8D62: 2, 0x8056: 2, 0x8057: 2,
    0x81A5: 2, 0x81A6: 4, 0x8CAC: 4, 0x88F0: 4, 0x8CAD: 8, 0x8D48: 1, 0x1908: 4, 0x1907: 3, 0x8051: 3, 0x8C41: 3, 0x8F96: 4, 0x8F97: 4, 0x8C3D: 4, 0x881B: 6, 0x8815: 12 };
  const bpp = (f) => BPP[f] || 4;
  for (const C of [window.WebGL2RenderingContext, window.WebGLRenderingContext]) {
    if (!C) continue;
    const P = C.prototype, bound = new WeakMap();   // ctx -> { tex2d, cube, tex3d, array, rb, buffers }
    const b = (gl) => { let s = bound.get(gl); if (!s) bound.set(gl, s = {}); return s; };
    const wrap = (name, fn) => { const o = P[name]; if (o) P[name] = function (...a) { const r = o.apply(this, a); try { fn.call(this, a, r); } catch (e) {} return r; }; };
    wrap("bindTexture", function (a) { b(this)[a[0]] = a[1]; });
    wrap("bindRenderbuffer", function (a) { b(this).rb = a[1]; });
    wrap("bindBuffer", function (a) { b(this)["buf" + a[0]] = a[1]; });
    const target = (t) => (t >= 0x8515 && t <= 0x851A ? 0x8513 : t);   // cube faces share the cube map
    wrap("texStorage2D", function (a) { const [t, lv, f, w, h] = a; let n = 0; for (let i = 0; i < lv; i++) n += Math.max(1, w >> i) * Math.max(1, h >> i); set(b(this)[t], "tex", n * bpp(f) * (t === 0x8513 ? 6 : 1)); });
    wrap("texStorage3D", function (a) { const [t, lv, f, w, h, d] = a; let n = 0; for (let i = 0; i < lv; i++) n += Math.max(1, w >> i) * Math.max(1, h >> i) * (t === 0x8C1A ? d : Math.max(1, d >> i)); set(b(this)[t], "tex", n * bpp(f)); });
    wrap("texImage2D", function (a) { if (a.length >= 8) { const [t, lv, f, w, h] = a; add(b(this)[target(t)], "tex", w * h * bpp(f)); } else { const [t, , f, , , src] = a; if (src && src.width) add(b(this)[target(t)], "tex", src.width * src.height * bpp(f)); } });
    wrap("compressedTexImage2D", function (a) { const t = a[0], d = a[6]; const n = typeof d === "number" ? d : d && d.byteLength || 0; add(b(this)[target(t)], "tex", n); });
    wrap("renderbufferStorage", function (a) { set(b(this).rb, "rb", a[2] * a[3] * bpp(a[1])); });
    wrap("renderbufferStorageMultisample", function (a) { set(b(this).rb, "rb", Math.max(1, a[1]) * a[3] * a[4] * bpp(a[2])); });
    wrap("bufferData", function (a) { const n = typeof a[1] === "number" ? a[1] : a[1] ? (a.length >= 5 && a[4] ? a[4] * (a[1].BYTES_PER_ELEMENT || 1) : a[1].byteLength) : 0; set(b(this)["buf" + a[0]], "buf", n); });
    wrap("deleteTexture", function (a) { free(a[0]); });
    wrap("deleteRenderbuffer", function (a) { free(a[0]); });
    wrap("deleteBuffer", function (a) { free(a[0]); });
  }
  // the game's WebAssembly memory: whichever Memory its module exports or imports
  const note = (m) => { if (m instanceof WebAssembly.Memory && !st.mems.includes(m)) st.mems.push(m); };
  const scan = (x) => { if (!x) return; for (const k in x) { const v = x[k]; if (v instanceof WebAssembly.Memory) note(v); else if (v && typeof v === "object") for (const j in v) note(v[j]); } };
  for (const fn of ["instantiate", "instantiateStreaming"]) {
    const o = WebAssembly[fn]; if (!o) continue;
    WebAssembly[fn] = function (src, imports) { scan(imports); console.log("[measure] wasm " + fn + " starts"); return o.apply(this, arguments).then((r) => { scan((r.instance || r).exports); console.log("[measure] wasm instantiated"); return r; }); };
  }
  const tick = () => { st.frames++; let w = 0; for (const m of st.mems) w += m.buffer.byteLength; st.wasm = w; if (w > st.wasmPeak) st.wasmPeak = w; requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
})();`;

// ---------------------------------------------------------------- the browser's own processes
function children(pid) {
  try { return readFileSync(`/proc/${pid}/task/${pid}/children`, "utf8").trim().split(/\s+/).filter(Boolean).map(Number); } catch { return []; }
}
function procMem(pid) {
  try {
    const cmd = readFileSync(`/proc/${pid}/cmdline`, "utf8").replace(/\0/g, " ");
    const roll = readFileSync(`/proc/${pid}/smaps_rollup`, "utf8");
    const kb = (k) => parseInt((roll.match(new RegExp(`^${k}:\\s+(\\d+)`, "m")) || [0, "0"])[1]);
    const kind = /WebKitWebProcess|WPEWebProcess|--type=renderer/.test(cmd) ? "web" : /WebKitGPUProcess|WPEGPUProcess|--type=gpu-process/.test(cmd) ? "gpu" : "other";
    return { pid, kind, rss: kb("Rss") * 1024, pss: kb("Pss") * 1024 };
  } catch { return null; }
}
function tree() {
  const out = [], stack = children(process.pid);
  while (stack.length) { const p = stack.pop(); const m = procMem(p); if (m) out.push(m); stack.push(...children(p)); }
  return out;
}
const mem = { webRss: 0, webPss: 0, gpuRss: 0, totalPss: 0 };
let memTimer = null;
function sampleProcs() {
  const ps = tree();
  const sum = (k, f) => ps.filter((p) => !k || p.kind === k).reduce((n, p) => n + p[f], 0);
  // one web process per page here, but WebKit may keep a spare: the biggest is the game's
  const web = ps.filter((p) => p.kind === "web").sort((a, b) => b.rss - a.rss)[0];
  if (web) { mem.webRss = Math.max(mem.webRss, web.rss); mem.webPss = Math.max(mem.webPss, web.pss); }
  mem.gpuRss = Math.max(mem.gpuRss, sum("gpu", "rss"));
  mem.totalPss = Math.max(mem.totalPss, sum(null, "pss"));
  // a timeline in the console: every 0.25 s for the first 12 s, then every 2 s
  if ((Date.now() - t0 < 12000 && memTicks++ >= 0) || ++memTicks % 8 === 0) log.push(`${stamp()} [mem] web ${web ? (web.rss / 1048576).toFixed(0) : "-"} MB RSS, gpu ${(sum("gpu", "rss") / 1048576).toFixed(0)} MB, all ${(sum(null, "pss") / 1048576).toFixed(0)} MB PSS`);
}
let memTicks = 0;

// ---------------------------------------------------------------- browser
const engine = pw[prof.engine];
const launch = { headless: true };
if (prof.engine === "webkit") launch.executablePath = WEBKIT;
else {
  const base = join(homedir(), ".cache", "ms-playwright");
  const shell = readdirSync(base).filter((d) => /^chromium_headless_shell-\d+$/.test(d)).sort((a, b) => parseInt(b.split("-")[1]) - parseInt(a.split("-")[1]))
    .map((d) => join(base, d, "chrome-headless-shell-linux64", "chrome-headless-shell")).find(existsSync);
  launch.executablePath = process.env.CHROME || shell;
  launch.args = ["--ignore-gpu-blocklist", "--enable-unsafe-swiftshader", "--enable-precise-memory-info"];
}
const browser = await engine.launch(launch);
const ctxOpts = prof.device ? { ...pw.devices[prof.device] } : { viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1, hasTouch: false, isMobile: false };
const context = await browser.newContext(ctxOpts);
await context.addInitScript(INIT);
// --recovery: on a load after sessionStorage.pwSimKill is set, the mark a killed visit leaves is there
await context.addInitScript(`try { if (sessionStorage.getItem("pwSimKill")) { sessionStorage.removeItem("pwSimKill"); localStorage.setItem("pw.web.running", String(Date.now() - 5000)); } } catch (e) {}`);
await context.addInitScript(`document.addEventListener("DOMContentLoaded", () => { const s = document.documentElement.style; ${JSON.stringify(INSETS)}.forEach((v, i) => s.setProperty("--pw-test-inset-" + ["left", "right", "top", "bottom"][i], v + "px")); });`);
// This Linux WebKit plays sound through GStreamer's autoaudiosink, which isn't installed here, and
// its web process dies about 20 s after an AudioContext opens the missing sink (seen with
// --webkit-audio; with no Web Audio at all it doesn't). An iPhone has its speaker, so in WebKit the
// game gets a silent stand-in that never opens an output: an OfflineAudioContext whose clock
// runs in real time once resumed, and which, like iOS Safari, stays suspended until resume() is
// called inside a user gesture (window.pwResumeAsked records each call). A gesture is what iOS
// counts: a trusted touchend, click, mouseup/down or key press being handled (not touchstart), as
// Playwright's evaluate() looks like a gesture to WebKit's own navigator.userActivation. Nothing is heard, and
// this WebKit can't decode the game's AAC sound effects either (no GStreamer AAC decoder here).
if (process.env.PW_EXTRA_INIT) await context.addInitScript({ path: process.env.PW_EXTRA_INIT });   // (debugging)
if (prof.engine === "webkit" && !args.includes("--webkit-audio")) await context.addInitScript(`(() => {
  window.pwResumeAsked = [];
  let inGesture = false;
  for (const t of ["touchend", "click", "mousedown", "mouseup", "pointerup", "keydown"])
    window.addEventListener(t, (e) => { if (!e.isTrusted || (t === "pointerup" && e.pointerType !== "touch" && e.pointerType !== "mouse")) return; inGesture = true; setTimeout(() => { inGesture = false; }, 0); }, true);
  class SilentAudioContext extends OfflineAudioContext {
    constructor(o) { super(2, 4410, (o && o.sampleRate) || 44100); this.pwState = "suspended"; this.pwAcc = 0; this.pwT0 = 0; }
    get state() { return this.pwState; }
    get currentTime() { return this.pwState === "running" ? this.pwAcc + (performance.now() - this.pwT0) / 1000 : this.pwAcc; }
    get baseLatency() { return 0.005; }
    get outputLatency() { return 0.02; }
    pwSet(s) { if (s === this.pwState) return; if (s === "running") this.pwT0 = performance.now(); else if (this.pwState === "running") this.pwAcc = this.currentTime; this.pwState = s; this.dispatchEvent(new Event("statechange")); }
    resume() {
      const gesture = inGesture;
      window.pwResumeAsked.push({ t: Math.round(performance.now()), gesture });
      if (gesture && this.pwState !== "closed") this.pwSet("running");
      return gesture ? Promise.resolve() : new Promise(() => {});   // iOS leaves it pending
    }
    suspend() { if (this.pwState === "running") this.pwSet("suspended"); return Promise.resolve(); }
    close() { this.pwSet("closed"); return Promise.resolve(); }
    // Unity streams long clips (the music) through an Audio element; here it's a silent gain node
    createMediaElementSource(el) { const g = this.createGain(); g.mediaElement = el; return g; }
  }
  window.AudioContext = window.webkitAudioContext = SilentAudioContext;
  // and those Audio elements would open the missing sink too: they keep their src but never load it
  const RealAudio = window.Audio;
  window.Audio = function () {
    const a = new RealAudio();
    let src = "";
    Object.defineProperty(a, "src", { get: () => src, set: (v) => { src = String(v); } });
    a.load = () => {}; a.play = () => Promise.resolve(); a.pause = () => {};
    return a;
  };
})();`);
const page = await context.newPage();
const log = [], errors = [];
// this WebKit has no AAC decoder (above): its "Decoding failed" for each sound effect isn't the game's
// and Chrome sends touches uncancelable when the page's main thread is too busy to answer (a few fps
// here); html's touch-action: none means nothing scrolls either way
const envNoise = (t) => (prof.engine === "webkit" && /EncodingError: Decoding failed|^Decode error: null|Loading FSB failed|getFrequency\(\) is not supported/.test(t)) ||
  (prof.engine === "chromium" && /^Ignored attempt to cancel a touch(start|move|end) event with cancelable=false/.test(t) && ++uncancelable);
let uncancelable = 0;
page.on("console", (m) => { const t = m.text(); log.push(`${stamp()} ${t}`); if (!envNoise(t) && (m.type() === "error" || /\b\w*Exception\b|RuntimeError|abort\(/.test(t))) errors.push(t); });
page.on("pageerror", (e) => { log.push(`${stamp()} PAGEERROR ${e.message}`); if (!envNoise(e.message)) errors.push(e.message); });
page.on("dialog", (d) => { log.push(`${stamp()} DIALOG ${d.message()}`); errors.push("dialog: " + d.message()); d.dismiss().catch(() => {}); });
page.on("crash", () => { log.push(`${stamp()} TAB CRASHED`); errors.push("tab crashed"); });
const waitLog = async (re, ms, from = 0) => { const s = Date.now(); while (Date.now() - s < ms) { if (log.slice(from).some((l) => re.test(l))) return true; await sleep(250); } return false; };

// real touch events: WebKit's own protocol (Playwright only exposes taps there), CDP in Chromium
let touchSend;
if (TOUCH && prof.engine === "webkit") {
  const s = page._connection.toImpl(page).delegate._pageProxySession;
  touchSend = (type, points) => s.send("Input.dispatchTouchEvent", { type, touchPoints: points.map((p) => ({ x: Math.round(p.x), y: Math.round(p.y), id: p.id })) });
} else if (TOUCH) {
  const cdp = await context.newCDPSession(page);
  touchSend = (type, points) => cdp.send("Input.dispatchTouchEvent", { type, touchPoints: points.map((p) => ({ x: p.x, y: p.y, id: p.id, radiusX: 4, radiusY: 4, force: 1 })) });
}
const fingers = new Map();   // id -> {x, y}
const pts = () => [...fingers.entries()].map(([id, p]) => ({ id, ...p }));
// (a desktop has one mouse: the same steps with its left button)
async function down(id, x, y) { if (!TOUCH) { await page.mouse.move(x, y); await page.mouse.down(); return; } fingers.set(id, { x, y }); await touchSend("touchStart", pts()); }
async function move(id, x, y) { if (!TOUCH) { await page.mouse.move(x, y); return; } fingers.set(id, { x, y }); await touchSend("touchMove", pts()); }
async function up(id) {
  if (!TOUCH) { await page.mouse.up(); return; }
  // WebKit's touchEnd lifts the points it's given; CDP's lifts the ones it's no longer given
  const lifted = { id, ...fingers.get(id) };
  fingers.delete(id);
  await touchSend("touchEnd", prof.engine === "webkit" ? [lifted] : pts());
}
async function tap(x, y) {
  if (!TOUCH) { await page.mouse.click(x, y, { delay: 90 }); return; }
  await down(9, x, y); await sleep(90); await up(9);
}
async function drag(id, x0, y0, x1, y1, steps, ms) { for (let i = 1; i <= steps; i++) { await move(id, x0 + (x1 - x0) * i / steps, y0 + (y1 - y0) * i / steps); await sleep(ms); } }
let shotN = 0;
async function shot(name) { const f = `${String(++shotN).padStart(2, "0")}_${name}.png`; await page.screenshot({ path: join(OUT, f) }); log.push(`${stamp()} shot ${f}`); return f; }

const measure = async () => JSON.parse(await page.evaluate(() => JSON.stringify((() => { const m = window.pwMeasure || {}; const jh = performance.memory ? performance.memory.usedJSHeapSize : 0; return { gl: m.gl, glPeak: m.glPeak, tex: m.tex, rb: m.rb, buf: m.buf, wasm: m.wasm, wasmPeak: m.wasmPeak, frames: m.frames, jsHeap: jh }; })())));
// the game's on-screen buttons and touch state, if this build reports them (see PW_ReportUi)
async function gameUi() {
  return JSON.parse(await page.evaluate(() => { try { if (!window.pwInstance) return "null"; window.pwUi = null; window.pwInstance.SendMessage("GameRoot", "PwReportUi"); return JSON.stringify(window.pwUi || null); } catch (e) { return "null"; } }));
}
const checks = [];
const check = (name, ok, note = "") => { checks.push({ name, ok: !!ok, note }); console.log(`${ok ? "ok  " : "FAIL"} ${name}${note ? ": " + note : ""}`); };
const fmtMB = (b) => `${(b / 1048576).toFixed(0)} MB`;

try {
  memTimer = setInterval(sampleProcs, 250);
  const env = await page.evaluate(() => ({ ua: navigator.userAgent, w: innerWidth, h: innerHeight, dpr: devicePixelRatio, coarse: matchMedia("(pointer: coarse)").matches, anyFine: matchMedia("(any-pointer: fine)").matches, gl2: !!document.createElement("canvas").getContext("webgl2"), gpu: !!navigator.gpu }));
  console.log(`${DEVICE}: ${prof.engine} ${browser.version()}, ${prof.device || "desktop"} ${env.w}x${env.h} @${env.dpr.toFixed(2)}, pointer ${env.coarse ? "coarse" : "fine"}${env.anyFine ? " (+fine)" : ""}, WebGL2 ${env.gl2 ? "yes" : "no"}, WebGPU ${env.gpu ? "yes" : "no"}`);
  const tNav = Date.now();
  await page.goto(URL_, { waitUntil: "commit" });
  if (PORTRAIT) {
    await sleep(1500);
    const shown = await page.evaluate(() => !document.getElementById("pw-rotate").hidden);
    await shot("rotate");
    const b = await page.evaluate(() => JSON.parse(JSON.stringify(document.getElementById("pw-rotate-play").getBoundingClientRect())));
    await tap(b.x + b.width / 2, b.y + b.height / 2);
    await sleep(500);
    check("upright: turn-sideways card, dismissed by Play anyway", shown && await page.evaluate(() => document.getElementById("pw-rotate").hidden));
  }
  const booted = await waitLog(/\[PW\] graphics/, 300000);
  const bootS = (Date.now() - tNav) / 1000;
  check("boots", booted, booted ? `${bootS.toFixed(1)} s to the game's first log line (localhost)` : "no [PW] graphics line in 300 s");
  if (!booted) throw new Error("didn't boot");
  await sleep(6000);
  await shot("title");
  const titleMem = await measure();
  const atTitle = { ...titleMem, procs: { ...mem } };
  const cards = await page.evaluate(() => ["pw-error", "pw-crash"].filter((id) => !document.getElementById(id).hidden));
  check("reaches the title with no error card", cards.length === 0 && log.some((l) => /\[PW\] title prompt/.test(l)), cards.join(", "));
  // the loader's copy of the unpacked WebAssembly is let go once the game runs (index.html)
  const wasmCopy = await page.evaluate(() => { const b = window.pwInstance && window.pwInstance.Module && window.pwInstance.Module.wasmBinary; return b ? b.byteLength : 0; });
  check("the loader's WebAssembly copy is freed", wasmCopy === 0, wasmCopy ? `${(wasmCopy / 1048576).toFixed(1)} MB still held` : "");
  let ui = await gameUi();
  if (ui) check("on-screen controls hidden on the title", !ui.touch.visible);
  if (RECOVERY) {
    const marked = await page.evaluate(() => !!localStorage.getItem("pw.web.running"));
    check("a phone's running visit is marked", marked === TOUCH, `mark ${marked ? "set" : "not set"}`);
    await page.evaluate(() => sessionStorage.setItem("pwSimKill", "1"));
    const from = log.length;
    await page.reload({ waitUntil: "commit" });
    await sleep(800);
    const card = await page.evaluate(() => { const e = document.getElementById("pw-recovered"); return e && !e.hidden ? e.textContent : ""; });
    await shot("recovered_loading");
    const again = await waitLog(/\[PW\] graphics: (low|high|medium|ultra) \(/, 300000, from);
    const gfx = (log.slice(from).find((l) => /\[PW\] graphics: (low|high|medium|ultra) \(/.test(l)) || "").replace(/.*graphics: /, "").split(",")[0];
    const said = log.slice(from).some((l) => /the last visit was cut short/.test(l));
    check("after a killed visit: the loading card says so", TOUCH ? !!card : !card, card || "no message");
    check("after a killed visit: Auto starts on Low", again && (TOUCH ? said && /^low \(Auto\)/.test(gfx) : !said), gfx);
    await sleep(3000);
    throw { done: true };
  }
  if (TITLE_ONLY) {
    await sleep(14000);
    sampleProcs();
    const m = await measure();
    const summary = { device: DEVICE, titleOnly: true, memory: { webProcessRssPeak: mem.webRss, webProcessPssPeak: mem.webPss, gpuProcessRssPeak: mem.gpuRss, allProcessesPssPeak: mem.totalPss, wasmHeapPeak: m.wasmPeak, webglPeak: m.glPeak, jsHeap: m.jsHeap, unity: ui ? ui.mem : null }, checks, errors };
    writeFileSync(join(OUT, "summary.json"), JSON.stringify(summary, null, 2));
    console.log(`title-only memory peaks: web process RSS ${fmtMB(mem.webRss)} (PSS ${fmtMB(mem.webPss)}), GPU process ${fmtMB(mem.gpuRss)}, browser ${fmtMB(mem.totalPss)} PSS; wasm heap ${fmtMB(m.wasmPeak)}; WebGL ${fmtMB(m.glPeak)}`);
    throw { done: true };
  }
  if (ui) check(TOUCH ? "a touch-first device starts in touch mode (before any touch)" : "a desktop starts out of touch mode", ui.touchMode === TOUCH);
  if (ui) log.push(`${stamp()} [ui] at the title: ${JSON.stringify(ui.mem)}, touch mode ${ui.touchMode}, safe ${JSON.stringify(ui.safe)}`);

  const r = await page.evaluate(() => JSON.parse(JSON.stringify(document.getElementById("unity-canvas").getBoundingClientRect())));
  const at = (fx, fy) => [r.x + r.width * fx, r.y + r.height * fy];
  const aspect = r.width / r.height;
  const boostOf = () => parseFloat(((log.filter((l) => /\[PW\] menu scale: /.test(l)).pop() || "").match(/, x([\d.]+) at /) || [0, "1"])[1]);
  const uiAt = (dx, dy) => { const k = boostOf(); const cw = (aspect < 1 ? 1200 : aspect > 16 / 9 ? 1080 * aspect : 1920) / k, ch = (aspect < 1 ? 1200 / aspect : aspect > 16 / 9 ? 1080 : 1920 / aspect) / k; return [r.x + r.width * (0.5 + dx / cw), r.y + r.height * (0.5 - dy / ch)]; };
  const narrow = () => aspect < 1 && boostOf() > 1;

  // audio: suspended until the first touch (WebKit holds sound back), running after it
  const audio = async () => page.evaluate(() => (window.pwAudioContexts || []).map((c) => c.state).join(","));
  const audioBefore = await audio();
  await tap(...at(0.5, 0.5));             // title: tap anywhere
  await sleep(3500);
  const audioAfter = await audio();
  const asked = await page.evaluate(() => JSON.stringify(window.pwResumeAsked || null));
  if (asked !== "null") log.push(`${stamp()} [audio] resume() calls: ${asked}`);
  // (WebKit: the silent stand-in keeps iOS's rule, so before the tap it must still be suspended;
  // Chromium's headless shell lets sound start without input, so there only "after" is checked)
  const beforeOk = prof.engine !== "webkit" || /suspended/.test(audioBefore);
  check("audio runs after the first tap", beforeOk && /running/.test(audioAfter) && !/suspended/.test(audioAfter), `before ${audioBefore || "none"}, after ${audioAfter || "none"}`);
  await shot("postcard");
  const ui2 = await gameUi();
  const start = ui2?.buttons?.find((b) => b.name === "Start")?.box;
  await tap(...(start ? [start.x, start.y] : narrow() ? uiAt(0, -395) : uiAt(300, -220)));
  const playing = await waitLog(/\[PW\] hint: /, 15000);
  await sleep(2500);
  await shot("play");
  check("Start on the postcard by tap", playing, playing ? "the day began (first hint shown)" : "no hint: still on the postcard?");

  // drag Pip, then hold still to rain, then flick
  const [sx, sy] = at(0.25, 0.45), [ex, ey] = at(0.42, 0.58);
  const f0 = (await measure()).frames, tf = Date.now();
  await down(1, sx, sy);
  await drag(1, sx, sy, ex, ey, 14, 50);
  for (let i = 0; i < 24; i++) { await move(1, ex, ey); await sleep(60); }
  await shot("drag_hold_rain");
  ui = await gameUi();
  if (ui) check("hold still to rain (one finger)", ui.raining, `raining ${ui.raining}`);
  await up(1);
  await sleep(800);
  const gustsBefore = ui ? ui.gusts : 0;
  const [fx0, fy0] = at(0.45, 0.6), [fx1, fy1] = at(0.7, 0.45);
  await down(2, fx0, fy0); await drag(2, fx0, fy0, fx1, fy1, 6, 35); await up(2);
  await sleep(800);
  ui = await gameUi();
  // a flick must start and end within 0.4 s, which takes a few frames to see: below 8 fps (a
  // loaded machine's software GL) it's reported, not judged
  const fpsNow = ((await measure()).frames - f0) / ((Date.now() - tf) / 1000);
  if (ui && TOUCH) {
    if (fpsNow >= 8 || ui.gusts > gustsBefore) check("flick to gust", ui.gusts > gustsBefore, `gusts ${gustsBefore} -> ${ui.gusts} at ${fpsNow.toFixed(1)} fps`);
    else console.log(`n/a  flick to gust: not judged at ${fpsNow.toFixed(1)} fps (gusts ${gustsBefore} -> ${ui.gusts})`);
  }
  await shot("after_flick");

  // the on-screen buttons
  if (ui && TOUCH) {
    check("on-screen controls shown in play on a touch device", ui.touch.visible, ui.touch.visible ? `rain ⌀${ui.touch.rain.d.toFixed(0)} CSS px, gust ⌀${ui.touch.gust.d.toFixed(0)}, pause ${ui.pause ? ui.pause.d.toFixed(0) : "?"}` : "hidden");
    if (ui.touch.visible) {
      const vw = r.width, vh = r.height, s = ui.safe;
      const inside = (b) => b.x - b.d / 2 >= s.l - 0.5 && b.x + b.d / 2 <= vw - s.r + 0.5 && b.y - b.d / 2 >= s.t - 0.5 && b.y + b.d / 2 <= vh - s.b + 0.5;
      check("buttons at least 44 pt", ui.touch.rain.d >= 44 && ui.touch.gust.d >= 44 && (!ui.pause || ui.pause.d >= 44));
      check("buttons inside the safe area", inside(ui.touch.rain) && inside(ui.touch.gust) && (!ui.pause || inside(ui.pause)), `insets l${s.l} r${s.r} t${s.t} b${s.b}`);
      // one finger holds Rain while another steers: Pip must move and rain at once
      const pipA = ui.pip;
      await down(3, ui.touch.rain.x, ui.touch.rain.y);
      await sleep(250);
      const [ax, ay] = at(0.3, 0.4), [bx, by] = at(0.5, 0.62);
      await down(4, ax, ay);
      await drag(4, ax, ay, bx, by, 16, 45);
      await shot("multitouch_rain_and_steer");
      const mid = await gameUi();
      await up(4);
      await sleep(200);
      await up(3);
      await sleep(300);
      const after = await gameUi();
      const moved = mid && pipA ? Math.hypot(mid.pip.x - pipA.x, mid.pip.y - pipA.y) : 0;
      check("multi-touch: hold Rain and steer with another finger", mid && mid.raining && moved > 20, `raining ${mid?.raining}, Pip moved ${moved.toFixed(0)} CSS px`);
      check("rain stops when the Rain finger lifts", after && !after.raining);
      const g0 = after.gusts;
      await tap(ui.touch.gust.x, ui.touch.gust.y);
      await sleep(500);
      const g1 = (await gameUi()).gusts;
      check("Gust button", g1 > g0, `gusts ${g0} -> ${g1}`);
    }
  }
  const frames = (await measure()).frames - f0, fps = frames / ((Date.now() - tf) / 1000);

  // pause by tap, then resume by tap
  ui = await gameUi();
  const pause = ui?.pause;
  await tap(...(pause ? [pause.x, pause.y] : uiAt(960 - 74, 540 - 74)));
  await sleep(1200);
  const paused = await waitLog(/\[PW\] state: Paused|paused/i, 2000, Math.max(0, log.length - 20));
  await shot("paused");
  ui = await gameUi();
  const resume = ui?.buttons?.find((b) => b.name === "Resume")?.box;
  if (resume) {
    await tap(resume.x, resume.y);
    await sleep(1000);
    const back = await gameUi();
    check("Pause and Resume by tap", ui.state === "Paused" && back.state === "Playing", `${ui.state} -> ${back.state}`);
  }
  await shot("resumed");

  // desktop: keyboard or mouse hides the controls at once (touch devices: a key press does too)
  if (ui) {
    await page.keyboard.down("ArrowRight"); await sleep(300); await page.keyboard.up("ArrowRight"); await sleep(300);
    const k = await gameUi();
    check(TOUCH ? "a key press hides the on-screen controls" : "no on-screen controls on a desktop (after keys)", !k.touch.visible);
    if (!TOUCH) { await page.mouse.move(400, 300); await page.mouse.move(600, 380, { steps: 5 }); await sleep(300); check("no on-screen controls on a desktop (after the mouse)", !(await gameUi()).touch.visible); }
  }
  await sleep(1000);
  const end = await measure();
  const endUi = await gameUi();
  sampleProcs();
  clearInterval(memTimer);
  await shot("end");

  const bytes = [...served.entries()];
  const sum = (re) => bytes.filter(([u]) => re.test(u)).reduce((n, [, b]) => n + b, 0);
  const summary = {
    device: DEVICE, engine: `${prof.engine} ${browser.version()}`, profile: prof.device, viewport: `${env.w}x${env.h}@${env.dpr}`, url: URL_,
    bootSeconds: +bootS.toFixed(1), fps: +fps.toFixed(1),
    download: { beforeTitle: sum(/\/(index\.html|Build\/|TemplateData\/)/), streamingAssets: sum(/StreamingAssets\//), total: sum(/./) },
    memory: {
      webProcessRssPeak: mem.webRss, webProcessPssPeak: mem.webPss, gpuProcessRssPeak: mem.gpuRss, allProcessesPssPeak: mem.totalPss,
      wasmHeapPeak: end.wasmPeak, wasmHeapEnd: end.wasm, webglPeak: end.glPeak, webglEnd: { total: end.gl, textures: end.tex, renderbuffers: end.rb, buffers: end.buf },
      jsHeapEnd: end.jsHeap, atTitle, unity: endUi ? endUi.mem : null,
    },
    checks, errors,
  };
  writeFileSync(join(OUT, "summary.json"), JSON.stringify(summary, null, 2));
  console.log(`download before the title ${fmtMB(summary.download.beforeTitle)}, streamed audio ${fmtMB(summary.download.streamingAssets)}`);
  console.log(`memory peaks: web process RSS ${fmtMB(mem.webRss)} (PSS ${fmtMB(mem.webPss)}), GPU process ${fmtMB(mem.gpuRss)}, all of the browser ${fmtMB(mem.totalPss)} PSS; wasm heap ${fmtMB(end.wasmPeak)}; WebGL ${fmtMB(end.glPeak)} peak (now textures ${fmtMB(end.tex)}, render targets ${fmtMB(end.rb)}, buffers ${fmtMB(end.buf)})`);
  console.log(`frame rate while playing: ${fps.toFixed(1)} fps (headless, software GL; load ${readFileSync("/proc/loadavg", "utf8").split(" ").slice(0, 3).join(" ")})`);
  check("no errors in the console", errors.length === 0, errors.slice(0, 3).join(" | "));
  if (uncancelable) console.log(`note: Chrome sent ${uncancelable} touch event(s) uncancelable (its busy-main-thread intervention, at ${fps.toFixed(1)} fps); nothing scrolls either way`);
} catch (e) {
  if (!e?.done) {   // (--title-only ends here on purpose)
    console.error(e);
    checks.push({ name: "ran to the end", ok: false, note: String(e).split("\n")[0] });
  }
} finally {
  clearInterval(memTimer);
  writeFileSync(join(OUT, "console.txt"), log.join("\n").slice(-2_000_000));
  await browser.close().catch(() => {});
  server.close();
}
const failed = checks.filter((c) => !c.ok);
console.log(failed.length ? `${failed.length} check(s) failed` : "all checks passed");
process.exit(failed.length ? 1 : 0);
