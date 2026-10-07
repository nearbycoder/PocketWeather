#!/usr/bin/env node
// Smoke test for the WebGL build in headless Chrome (over the DevTools protocol) or Firefox (over
// WebDriver BiDi). No npm packages needed; Node >= 22 for the global WebSocket.
//
//   node Tools/web_smoke.mjs [outDir] [--phone] [--portrait] [--mouse] [--firefox] [--throttle <Mbps>] [--dir <build folder>]
//   (outDir defaults to Recordings/web-smoke)
//
// --phone emulates an Android phone held landscape (844x390 CSS px, 2x); --portrait holds it
// upright (390x844) and checks the page's "turn sideways" card and its "Play anyway" button.
// --mouse is a desktop without a touchscreen (no touch emulation; the same steps with mouse
// events), to check the first hint speaks mouse there.
// --firefox runs the same steps in Firefox (FIREFOX=/path overrides /usr/bin/firefox) over WebDriver
// BiDi: a desktop with a mouse, or with --phone a touch-screen phone (touch actions, and the
// pointer reported coarse if Firefox supports BiDi's touch override). --throttle needs Chrome.
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
import { createServer } from "node:net";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const PORTRAIT = args.includes("--portrait");   // a phone held upright (implies --phone)
const PHONE = args.includes("--phone") || PORTRAIT;
const FIREFOX = args.includes("--firefox");
const MOUSE = (args.includes("--mouse") || FIREFOX) && !PHONE;   // a desktop with no touchscreen
const ti = args.indexOf("--throttle"), di = args.indexOf("--dir");
const MBPS = ti >= 0 ? parseFloat(args[ti + 1]) : 0;
const WEBDIR = di >= 0 ? args[di + 1] : join(ROOT, "Builds/WebGL");
const OUT = args.find((a, i) => !a.startsWith("--") && !(ti >= 0 && i === ti + 1) && !(di >= 0 && i === di + 1)) || join(ROOT, "Recordings", "web-smoke");
const W = PORTRAIT ? 390 : PHONE ? 844 : 1280, H = PORTRAIT ? 844 : PHONE ? 390 : 720;
// free ports each run: other projects on this machine run the same kind of smoke test, and a fixed
// port can silently serve (and "test") somebody else's build
const freePort = () => new Promise((res) => { const sv = createServer(); sv.listen(0, "127.0.0.1", () => { const p = sv.address().port; sv.close(() => res(p)); }); });
const PORT = await freePort(), CDP = await freePort();
const CHROME = process.env.CHROME || "google-chrome-stable";
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = [];

if (FIREFOX && MBPS > 0) { console.error("--throttle needs Chrome (WebDriver BiDi has no network throttling)"); process.exit(2); }
const server = spawn("python3", ["-m", "http.server", String(PORT), "--bind", "127.0.0.1"], { cwd: WEBDIR, stdio: "ignore" });
// a throwaway browser profile inside the repo's gitignored Recordings/ (not the shared /tmp)
const profile = join(ROOT, "Recordings", `${FIREFOX ? "firefox" : "chrome"}-profile-${PORT}`);
rmSync(profile, { recursive: true, force: true });
let browser;
if (FIREFOX) {
  mkdirSync(profile, { recursive: true });
  writeFileSync(join(profile, "user.js"), [
    'user_pref("media.autoplay.default", 0);', 'user_pref("media.autoplay.blocking_policy", 0);',
    'user_pref("browser.shell.checkDefaultBrowser", false);', 'user_pref("datareporting.policy.dataSubmissionEnabled", false);',
    'user_pref("toolkit.telemetry.reportingpolicy.firstRun", false);', 'user_pref("dom.w3c_touch_events.enabled", 1);',
  ].join("\n") + "\n");
  browser = spawn(process.env.FIREFOX || "/usr/bin/firefox", [
    "--headless", "--no-remote", "--profile", profile, `--remote-debugging-port=${CDP}`, `--width=${W}`, `--height=${H}`,
  ], { stdio: "ignore" });
} else {
  browser = spawn(CHROME, [
    "--headless=new", `--remote-debugging-port=${CDP}`, `--user-data-dir=${profile}`, `--window-size=${W},${H}`,
    "--no-first-run", "--no-default-browser-check", "--ignore-gpu-blocklist", "--enable-unsafe-swiftshader",
    "--autoplay-policy=no-user-gesture-required", "about:blank",
  ], { stdio: "ignore" });
}

function cleanup() {
  try { browser.kill("SIGKILL"); } catch {}
  try { server.kill(); } catch {}
  try { rmSync(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 }); } catch {}
}
process.on("exit", cleanup);
// the browser keeps writing to its profile until it has exited, so wait for that before removing it
async function shutdown() {
  if (browser.exitCode === null && browser.signalCode === null) {
    const gone = new Promise((r) => browser.once("exit", r));
    try { browser.kill("SIGKILL"); } catch {}
    await Promise.race([gone, sleep(5000)]);
  }
  await sleep(500);
  cleanup();
}

async function json(url) { for (let i = 0; i < 50; i++) { try { return await (await fetch(url)).json(); } catch { await sleep(200); } } throw new Error("no CDP"); }

let ws, nextId = 1, ctx, coarse = !FIREFOX;   // coarse: the page reports a finger as its main pointer when PHONE   // ctx: the BiDi browsing context (Firefox)
const pending = new Map();
function send(method, params = {}) {
  const id = nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((res, rej) => pending.set(id, { res, rej: (e) => rej(new Error(`${method}: ${e.message}`)) }));
}
async function evaluate(expression) {
  if (!FIREFOX) return (await send("Runtime.evaluate", { expression, returnByValue: true })).result.value;
  const r = await send("script.evaluate", { expression, target: { context: ctx }, awaitPromise: false });
  return r.result?.value;
}

async function shot(name) {
  const { data } = FIREFOX ? await send("browsingContext.captureScreenshot", { context: ctx }) : await send("Page.captureScreenshot", { format: "png" });
  writeFileSync(join(OUT, name + ".png"), Buffer.from(data, "base64"));
  console.log("shot", name);
}
async function touch(type, x, y) {
  if (FIREFOX) {
    // WebDriver actions keep the pointer's state between calls, so a drag can be sent step by step
    const a = { touchStart: [{ type: "pointerMove", x: Math.round(x), y: Math.round(y) }, { type: "pointerDown", button: 0 }],
      touchMove: [{ type: "pointerMove", x: Math.round(x), y: Math.round(y) }], touchEnd: [{ type: "pointerUp", button: 0 }] }[type];
    await send("input.performActions", { context: ctx, actions: [{ type: "pointer", id: MOUSE ? "mouse" : "finger", parameters: { pointerType: MOUSE ? "mouse" : "touch" }, actions: a }] });
    return;
  }
  if (MOUSE) {
    const t = { touchStart: "mousePressed", touchMove: "mouseMoved", touchEnd: "mouseReleased" }[type];
    if (t === "mousePressed") await send("Input.dispatchMouseEvent", { type: "mouseMoved", x, y });
    await send("Input.dispatchMouseEvent", { type: t, x, y, button: t === "mouseMoved" ? "none" : "left", buttons: t === "mouseReleased" ? 0 : 1, clickCount: 1 });
    return;
  }
  await send("Input.dispatchTouchEvent", { type, touchPoints: type === "touchEnd" ? [] : [{ x, y, radiusX: 4, radiusY: 4, force: 1, id: 1 }] });
}
async function tap(x, y) { await touch("touchStart", x, y); await sleep(90); await touch("touchEnd", x, y); }
async function waitLog(re, ms, from = 0) { const t0 = Date.now(); while (Date.now() - t0 < ms) { if (log.slice(from).some((l) => re.test(l))) return true; await sleep(250); } return false; }

(async () => {
  if (FIREFOX) {
    for (let i = 0; i < 100 && !ws; i++) {
      try { const w = new WebSocket(`ws://127.0.0.1:${CDP}/session`); await new Promise((res, rej) => { w.onopen = res; w.onerror = rej; }); ws = w; } catch { await sleep(300); }
    }
    if (!ws) throw new Error("no WebDriver BiDi");
  } else {
    const targets = await json(`http://127.0.0.1:${CDP}/json/list`);
    const page = targets.find((t) => t.type === "page");
    ws = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((r) => (ws.onopen = r));
  }
  ws.onmessage = (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.message || m.error.message || m.error)) : p.res(m.result); return; }
    if (m.method === "Runtime.consoleAPICalled") log.push(m.params.args.map((a) => a.value ?? a.description ?? "").join(" "));
    if (m.method === "Runtime.exceptionThrown") log.push("EXCEPTION " + (m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text));
    if (m.method === "log.entryAdded") log.push((m.params.type === "javascript" ? "EXCEPTION " : "") + (m.params.text || ""));
  };
  if (FIREFOX) {
    const { capabilities } = await send("session.new", { capabilities: {} });
    console.log(`Firefox ${capabilities.browserVersion}`);
    await send("session.subscribe", { events: ["log.entryAdded"] });
    // a fresh tab: the one Firefox opens with is a privileged page that can't be resized
    ctx = (await send("browsingContext.create", { type: "tab" })).context;
    await send("browsingContext.setViewport", { context: ctx, viewport: { width: W, height: H }, devicePixelRatio: PHONE ? 2 : 1 });
    if (PHONE) {
      // a coarse main pointer, as on a phone (BiDi's touch override; older Firefox lacks it)
      try { await send("emulation.setTouchOverride", { contexts: [ctx], maxTouchPoints: 5 }); coarse = true; console.log("touch override: on"); }
      catch { console.log("touch override: not supported, so the page reports a mouse until the first touch (checked instead: the hint switches to touch)"); }
    }
  } else {
    await send("Runtime.enable");
    await send("Page.enable");
    await send("Emulation.setDeviceMetricsOverride", { width: W, height: H, deviceScaleFactor: PHONE ? 2 : 1, mobile: PHONE });
    if (PHONE) await send("Emulation.setUserAgentOverride", { userAgent: "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/154.0 Mobile Safari/537.36" });
    if (!MOUSE) await send("Emulation.setTouchEmulationEnabled", { enabled: true, maxTouchPoints: 5 });
  }
  if (MBPS > 0) {
    await send("Network.enable");
    await send("Network.emulateNetworkConditions", { offline: false, latency: 60, downloadThroughput: MBPS * 125000, uploadThroughput: 2 * 125000 });
  }
  const buildDir = join(WEBDIR, "Build");
  const bytes = readdirSync(buildDir).reduce((n, f) => n + statSync(join(buildDir, f)).size, 0);
  console.log(`download ${(bytes / 1048576).toFixed(1)} MB${MBPS > 0 ? ` at ${MBPS} Mbps` : " (localhost, unthrottled)"}`);
  const t0 = Date.now();
  const url = `http://127.0.0.1:${PORT}/index.html`;
  if (FIREFOX) await send("browsingContext.navigate", { context: ctx, url, wait: "none" });
  else await send("Page.navigate", { url });
  if (MBPS > 0) { await sleep(2500); await shot("w00_loading"); }   // the page's own loading card, mid-download

  let rotateOk = true;
  if (PORTRAIT) {
    // the page asks phones held upright to turn sideways; "Play anyway" must dismiss it
    await sleep(1500);
    const vis = async () => await evaluate("!document.getElementById('pw-rotate').hidden");
    const shown = await vis();
    await shot("w00_rotate");
    const b = JSON.parse(await evaluate("JSON.stringify(document.getElementById('pw-rotate-play').getBoundingClientRect())"));
    await tap(b.x + b.width / 2, b.y + b.height / 2);
    await sleep(500);
    const gone = !(await vis());
    rotateOk = shown && gone;
    console.log(`rotate card: shown ${shown ? "yes" : "NO"}, dismissed by Play anyway ${gone ? "yes" : "NO"}`);
  }
  const booted = await waitLog(/\[PW\] graphics/, 300000);
  console.log(booted ? `booted in ${((Date.now() - t0) / 1000).toFixed(1)}s` : "did not boot within 300s");
  await sleep(6000);
  await shot("w01_title");
  if (booted) {
    // the canvas fills the page in the default template's centre box; find it
    const r = JSON.parse(await evaluate("JSON.stringify(document.querySelector('#unity-canvas').getBoundingClientRect())"));
    const at = (fx, fy) => [r.x + r.width * fx, r.y + r.height * fy];
    // UI positions in the game's 1920x1080 design units (offset from the screen centre), mapped
    // the way its CanvasScaler (Expand) does, so taps land on the same buttons at any aspect
    const aspect = r.width / r.height;
    // portrait screens use a 1200-wide design (Ui.PortraitWidth)
    const cw = aspect < 1 ? 1200 : aspect > 16 / 9 ? 1080 * aspect : 1920;
    const ch = aspect < 1 ? 1200 / aspect : aspect > 16 / 9 ? 1080 : 1920 / aspect;
    const ui = (dx, dy) => [r.x + r.width * (0.5 + dx / cw), r.y + r.height * (0.5 - dy / ch)];
    await tap(...at(0.5, 0.5));                    // title: tap anywhere (a fresh profile goes straight to Day 1's postcard)
    await sleep(3500);
    await shot("w02_after_title");
    await tap(...ui(-739, 151));                   // map's Day 1 card, for builds that show the map first (harmless on the postcard)
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
    await waitLog(/\[PW\] music clock /, 20000);   // logged 10 s into a track
  }
  // a machine that needed Auto graphics to drop to Low starts on Low next visit (headless Chrome's
  // software GPU always does): reload in the same profile and check the second boot's verdict
  let remembered = true;
  if (booted && log.some((l) => /Auto graphics: .* switching to Low/.test(l))) {
    const first = log.length;
    if (FIREFOX) await send("browsingContext.reload", { context: ctx, wait: "none" });
    else await send("Page.reload", { ignoreCache: false });
    const again = await waitLog(/\[PW\] graphics/, 300000, first);
    const line = log.slice(first).find((l) => /\[PW\] graphics/.test(l)) || "";
    remembered = again && /graphics: low \(Auto\)/.test(line);
    console.log(`second visit remembers Low: ${remembered ? "yes" : "NO"} (${line.slice(0, 60)})`);
  }
  // the title's prompt, before anything was touched: "Tap" on a phone, "Click" with a mouse
  const prompt = (log.find((l) => /\[PW\] title prompt: /.test(l)) || "").replace(/.*title prompt: /, "").trim();
  const promptOk = !booted || (PHONE ? !coarse || prompt === "Tap to play" : MOUSE ? prompt === "Click to play" : true);
  console.log(`title prompt before any input: "${prompt || "not logged"}"${promptOk ? "" : " (WRONG for this device)"}`);
  // before any touch in play, the first hint must already speak touch on a phone
  const firstHint = (log.find((l) => /\[PW\] hint: /.test(l)) || "").replace(/.*\[PW\] hint: /, "").trim();
  // and the game's guess before any input (logged at boot) matches the device
  const guess = (log.find((l) => /\[PW\] touch-first device: /.test(l)) || "").replace(/.*device: /, "").trim();
  const guessOk = !booted || (PHONE ? !coarse || guess === "yes" : MOUSE ? guess === "no" : true);
  console.log(`touch-first guess before any input: ${guess || "not logged"}${guessOk ? "" : " (WRONG for this device)"}`);
  const hintOk = guessOk && (!booted || (PHONE ? (coarse ? firstHint === "Drag to fly" : log.some((l) => /\[PW\] hint: Drag to fly/.test(l))) : MOUSE ? firstHint === "Point to fly" : true));
  const wanted = PHONE && !coarse ? "switched to \"Drag to fly\" after the first touch" : PHONE ? "touch wording, as it should be on a phone" : MOUSE ? "mouse wording, as it should be without a touchscreen" : "";
  console.log(`first hint: "${firstHint || "none"}"${wanted ? (hintOk ? ` (${wanted})` : ` (NOT the ${wanted.split(",")[0]})`) : ""}`);
  // the rain's notes follow the music's chords, so the game's idea of where the music is must move
  // with real time (Unity can't read a compressed web source's position)
  const clock = log.find((l) => /\[PW\] music clock /.test(l)) || "";
  const cm = clock.match(/real ([\d.]+) s, chords follow ([\d.]+) s/);
  const clockOk = !booted || (cm != null && Math.abs(parseFloat(cm[1]) - parseFloat(cm[2])) < 1.5);
  console.log(`music clock: ${clock ? clock.replace(/.*music clock /, "") : "not logged"}${clockOk ? "" : " (the chords DON'T follow the music)"}`);
  writeFileSync(join(OUT, "console.txt"), log.join("\n"));
  const ex = log.filter((l) => /exception|error/i.test(l) && !/favicon/i.test(l));
  console.log(`console lines ${log.length}, error/exception lines ${ex.length}`);
  ex.slice(0, 15).forEach((l) => console.log("  " + l.slice(0, 200)));
  for (const l of log.filter((l) => /\[PW\]/.test(l)).slice(0, 12)) console.log("  " + l.slice(0, 160));
  await shutdown();
  process.exit(booted && ex.length === 0 && remembered && rotateOk && hintOk && promptOk && clockOk ? 0 : 1);
})().catch(async (e) => { console.error(e); await shutdown(); process.exit(2); });
