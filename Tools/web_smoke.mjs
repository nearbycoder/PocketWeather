#!/usr/bin/env node
// Smoke test for the WebGL build in headless Chrome, driven over the DevTools protocol (no npm
// packages needed; Node >= 22 for the global WebSocket).
//
//   node Tools/web_smoke.mjs [outDir] [--phone] [--throttle <Mbps>] [--dir <build folder>]  (default /tmp/pw-web)
//
// --phone emulates an Android phone held landscape (844x390 CSS px, 2x).
// --throttle 20 emulates a 20 Mbps connection with 60 ms latency, to time a realistic first visit
// (a fresh browser profile each run, so nothing is cached). --dir serves another build folder
// (default Builds/WebGL), e.g. an older release, to compare load times under the same conditions.
//
// Serves Builds/WebGL on localhost, opens it in a throwaway Chrome profile with touch emulation,
// collects the game's console log, then uses real browser touch events: tap the title, tap the
// first level card, tap Start, drag Pip and hold to rain. Screenshots at each step; exits non-zero
// if the game never boots or logs exceptions.
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync, rmSync, readdirSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { tmpdir } from "node:os";
import { createServer } from "node:net";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const PHONE = args.includes("--phone");
const ti = args.indexOf("--throttle"), di = args.indexOf("--dir");
const MBPS = ti >= 0 ? parseFloat(args[ti + 1]) : 0;
const WEBDIR = di >= 0 ? args[di + 1] : join(ROOT, "Builds/WebGL");
const OUT = args.find((a, i) => !a.startsWith("--") && !(ti >= 0 && i === ti + 1) && !(di >= 0 && i === di + 1)) || "/tmp/pw-web";
const W = PHONE ? 844 : 1280, H = PHONE ? 390 : 720;
// free ports each run: other projects on this machine run the same kind of smoke test, and a fixed
// port can silently serve (and "test") somebody else's build
const freePort = () => new Promise((res) => { const sv = createServer(); sv.listen(0, "127.0.0.1", () => { const p = sv.address().port; sv.close(() => res(p)); }); });
const PORT = await freePort(), CDP = await freePort();
const CHROME = process.env.CHROME || "google-chrome-stable";
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = [];

const server = spawn("python3", ["-m", "http.server", String(PORT), "--bind", "127.0.0.1"], { cwd: WEBDIR, stdio: "ignore" });
const profile = join(tmpdir(), `pw-chrome-profile-${PORT}`);
rmSync(profile, { recursive: true, force: true });
const chrome = spawn(CHROME, [
  "--headless=new", `--remote-debugging-port=${CDP}`, `--user-data-dir=${profile}`, `--window-size=${W},${H}`,
  "--no-first-run", "--no-default-browser-check", "--ignore-gpu-blocklist", "--enable-unsafe-swiftshader",
  "--autoplay-policy=no-user-gesture-required", "about:blank",
], { stdio: "ignore" });

function cleanup() { try { chrome.kill(); } catch {} try { server.kill(); } catch {} setTimeout(() => { try { rmSync(profile, { recursive: true, force: true }); } catch {} }, 500); }
process.on("exit", cleanup);

async function json(url) { for (let i = 0; i < 50; i++) { try { return await (await fetch(url)).json(); } catch { await sleep(200); } } throw new Error("no CDP"); }

let ws, nextId = 1;
const pending = new Map();
function send(method, params = {}) {
  const id = nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((res, rej) => pending.set(id, { res, rej }));
}

async function shot(name) {
  const { data } = await send("Page.captureScreenshot", { format: "png" });
  writeFileSync(join(OUT, name + ".png"), Buffer.from(data, "base64"));
  console.log("shot", name);
}
async function touch(type, x, y) {
  await send("Input.dispatchTouchEvent", { type, touchPoints: type === "touchEnd" ? [] : [{ x, y, radiusX: 4, radiusY: 4, force: 1, id: 1 }] });
}
async function tap(x, y) { await touch("touchStart", x, y); await sleep(90); await touch("touchEnd", x, y); }
async function waitLog(re, ms, from = 0) { const t0 = Date.now(); while (Date.now() - t0 < ms) { if (log.slice(from).some((l) => re.test(l))) return true; await sleep(250); } return false; }

(async () => {
  const targets = await json(`http://127.0.0.1:${CDP}/json/list`);
  const page = targets.find((t) => t.type === "page");
  ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((r) => (ws.onopen = r));
  ws.onmessage = (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.error.message)) : p.res(m.result); return; }
    if (m.method === "Runtime.consoleAPICalled") log.push(m.params.args.map((a) => a.value ?? a.description ?? "").join(" "));
    if (m.method === "Runtime.exceptionThrown") log.push("EXCEPTION " + (m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text));
  };
  await send("Runtime.enable");
  await send("Page.enable");
  await send("Emulation.setDeviceMetricsOverride", { width: W, height: H, deviceScaleFactor: PHONE ? 2 : 1, mobile: PHONE });
  if (PHONE) await send("Emulation.setUserAgentOverride", { userAgent: "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0 Mobile Safari/537.36" });
  await send("Emulation.setTouchEmulationEnabled", { enabled: true, maxTouchPoints: 5 });
  if (MBPS > 0) {
    await send("Network.enable");
    await send("Network.emulateNetworkConditions", { offline: false, latency: 60, downloadThroughput: MBPS * 125000, uploadThroughput: 2 * 125000 });
  }
  const buildDir = join(WEBDIR, "Build");
  const bytes = readdirSync(buildDir).reduce((n, f) => n + statSync(join(buildDir, f)).size, 0);
  console.log(`download ${(bytes / 1048576).toFixed(1)} MB${MBPS > 0 ? ` at ${MBPS} Mbps` : " (localhost, unthrottled)"}`);
  const t0 = Date.now();
  await send("Page.navigate", { url: `http://127.0.0.1:${PORT}/index.html` });
  if (MBPS > 0) { await sleep(2500); await shot("w00_loading"); }   // the page's own loading card, mid-download

  const booted = await waitLog(/\[PW\] graphics/, 300000);
  console.log(booted ? `booted in ${((Date.now() - t0) / 1000).toFixed(1)}s` : "did not boot within 300s");
  await sleep(6000);
  await shot("w01_title");
  if (booted) {
    // the canvas fills the page in the default template's centre box; find it
    const { result } = await send("Runtime.evaluate", { expression: "JSON.stringify(document.querySelector('#unity-canvas').getBoundingClientRect())", returnByValue: true });
    const r = JSON.parse(result.value);
    const at = (fx, fy) => [r.x + r.width * fx, r.y + r.height * fy];
    // UI positions in the game's 1920x1080 design units (offset from the screen centre), mapped
    // the way its CanvasScaler (Expand) does, so taps land on the same buttons at any aspect
    const aspect = r.width / r.height;
    const cw = aspect > 16 / 9 ? 1080 * aspect : 1920, ch = aspect > 16 / 9 ? 1080 : 1920 / aspect;
    const ui = (dx, dy) => [r.x + r.width * (0.5 + dx / cw), r.y + r.height * (0.5 - dy / ch)];
    await tap(...at(0.5, 0.5));                    // title: tap anywhere
    await sleep(3500);
    await shot("w02_map");
    await tap(...ui(-739, 151));                   // map: Day 1 card (top-left of the grid)
    await sleep(3000);
    await shot("w03_postcard");
    await tap(...ui(300, -220));                   // postcard: Start
    await sleep(3500);
    await shot("w04_play");
    // drag from Pip toward the middle, then hold still to rain
    const [sx, sy] = at(0.2, 0.42), [ex, ey] = at(0.35, 0.55);
    await touch("touchStart", sx, sy);
    for (let i = 1; i <= 12; i++) { await touch("touchMove", sx + (ex - sx) * i / 12, sy + (ey - sy) * i / 12); await sleep(60); }
    for (let i = 0; i < 30; i++) { await touch("touchMove", ex, ey); await sleep(70); }
    await shot("w05_rain");
    await touch("touchEnd", ex, ey);
    await sleep(1500);
    await shot("w06_after");
  }
  // a machine that needed Auto graphics to drop to Low starts on Low next visit (headless Chrome's
  // software GPU always does): reload in the same profile and check the second boot's verdict
  let remembered = true;
  if (booted && log.some((l) => /Auto graphics: .* switching to Low/.test(l))) {
    const first = log.length;
    await send("Page.reload", { ignoreCache: false });
    const again = await waitLog(/\[PW\] graphics/, 300000, first);
    const line = log.slice(first).find((l) => /\[PW\] graphics/.test(l)) || "";
    remembered = again && /graphics: low \(Auto\)/.test(line);
    console.log(`second visit remembers Low: ${remembered ? "yes" : "NO"} (${line.slice(0, 60)})`);
  }
  writeFileSync(join(OUT, "console.txt"), log.join("\n"));
  const ex = log.filter((l) => /exception|error/i.test(l) && !/favicon/i.test(l));
  console.log(`console lines ${log.length}, error/exception lines ${ex.length}`);
  ex.slice(0, 15).forEach((l) => console.log("  " + l.slice(0, 200)));
  for (const l of log.filter((l) => /\[PW\]/.test(l)).slice(0, 12)) console.log("  " + l.slice(0, 160));
  cleanup();
  process.exit(booted && ex.length === 0 && remembered ? 0 : 1);
})().catch((e) => { console.error(e); cleanup(); process.exit(2); });
