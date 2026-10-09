#!/usr/bin/env node
// Checks a served copy of the web build (GitHub Pages, or Builds/pages on a local static server)
// in a headless browser. No npm packages needed; Node >= 22 for the global WebSocket.
//
//   node Tools/check-pages.mjs <url> [--firefox] [--play] [--out <dir>]
//   node Tools/check-pages.mjs https://nearbycoder.github.io/PocketWeather/
//
// By default: open the page in a fresh profile, wait for the game to reach its title screen, and
// exit 0 only if it did with no console errors, no uncaught exceptions, no failed or 4xx/5xx
// requests, no browser dialog, and neither of the page's error cards showing.
//
// --play adds a short session with a real browser's autoplay rule (sound only after input): the
// game's audio must still be suspended before any input and running after the first click (on
// the title's Settings gear; headless Chromium never holds sound back, so there only the second
// half is checked). In Settings, Graphics is set to Medium (the slider's middle notch)
// and Esc closes it; then a click on the title, Start on Day 1's postcard, flying Pip with the
// mouse and holding to rain, and the arrow keys; the phone's on-screen touch buttons must stay hidden
// throughout (a desktop has no touchscreen). A reload in the same profile must come back on
// Medium graphics with the day counted as played (browser storage survives a reload).
//
// Chrome: CHROME=/path, else the newest cached Playwright headless shell under
// ~/.cache/ms-playwright, else google-chrome-stable. Firefox: FIREFOX=/path, else /usr/bin/firefox
// (over WebDriver BiDi). Screenshots and console.txt go to --out (default
// Recordings/check-pages/<browser>); the throwaway profile lives beside them and is removed.
import { spawn } from "node:child_process";
import { mkdirSync, writeFileSync, rmSync, readdirSync, existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { createServer } from "node:net";
import { homedir } from "node:os";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const FIREFOX = args.includes("--firefox");
const PLAY = args.includes("--play");
const oi = args.indexOf("--out");
const URL_ = args.find((a, i) => !a.startsWith("--") && !(oi >= 0 && i === oi + 1));
if (!URL_) { console.error("usage: node Tools/check-pages.mjs <url> [--firefox] [--play] [--out <dir>]"); process.exit(2); }
const OUT = oi >= 0 ? args[oi + 1] : join(ROOT, "Recordings", "check-pages", FIREFOX ? "firefox" : "chrome");
const W = 1280, H = 720;
const BOOT_MS = 300000;
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const freePort = () => new Promise((res) => { const sv = createServer(); sv.listen(0, "127.0.0.1", () => { const p = sv.address().port; sv.close(() => res(p)); }); });
const PORT = await freePort();

function findChrome() {
  if (process.env.CHROME) return process.env.CHROME;
  const base = join(homedir(), ".cache", "ms-playwright");
  try {
    const shells = readdirSync(base).filter((d) => /^chromium_headless_shell-\d+$/.test(d)).sort((a, b) => parseInt(b.split("-")[1]) - parseInt(a.split("-")[1]));
    for (const d of shells) {
      const p = join(base, d, "chrome-headless-shell-linux64", "chrome-headless-shell");
      if (existsSync(p)) return p;
    }
  } catch {}
  return "google-chrome-stable";
}

const profile = join(OUT, `profile-${PORT}`);
rmSync(profile, { recursive: true, force: true });
mkdirSync(profile, { recursive: true });
let browser, browserName = "";
if (FIREFOX) {
  // a real visitor's autoplay rule: no sound (Web Audio included) until the page is interacted with
  writeFileSync(join(profile, "user.js"), [
    'user_pref("media.autoplay.default", 1);', 'user_pref("media.autoplay.blocking_policy", 0);', 'user_pref("media.autoplay.block-webaudio", true);',
    'user_pref("browser.shell.checkDefaultBrowser", false);', 'user_pref("datareporting.policy.dataSubmissionEnabled", false);',
    'user_pref("toolkit.telemetry.reportingpolicy.firstRun", false);',
  ].join("\n") + "\n");
  browser = spawn(process.env.FIREFOX || "/usr/bin/firefox", [
    "--headless", "--no-remote", "--profile", profile, `--remote-debugging-port=${PORT}`, `--width=${W}`, `--height=${H}`,
  ], { stdio: "ignore" });
} else {
  const exe = findChrome();
  browserName = exe;
  browser = spawn(exe, [
    "--headless=new", `--remote-debugging-port=${PORT}`, `--user-data-dir=${profile}`, `--window-size=${W},${H}`,
    "--no-first-run", "--no-default-browser-check", "--ignore-gpu-blocklist", "--enable-unsafe-swiftshader",
    "--autoplay-policy=user-gesture-required", "about:blank",
  ], { stdio: "ignore" });
}
browser.on("error", (e) => { console.error(`couldn't start the browser: ${e.message}`); process.exit(2); });

function cleanup() {
  try { browser.kill("SIGKILL"); } catch {}
  try { rmSync(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 }); } catch {}
}
process.on("exit", cleanup);
async function shutdown() {
  if (browser.exitCode === null && browser.signalCode === null) {
    const gone = new Promise((r) => browser.once("exit", r));
    try { browser.kill("SIGKILL"); } catch {}
    await Promise.race([gone, sleep(5000)]);
  }
  await sleep(500);
  cleanup();
}

let ws, nextId = 1, ctx;
const pending = new Map();
const log = [], errors = [], dialogs = [];
function send(method, params = {}) {
  const id = nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((res, rej) => pending.set(id, { res, rej: (e) => rej(new Error(`${method}: ${e.message}`)) }));
}
async function evaluate(expression) {
  if (!FIREFOX) return (await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true })).result.value;
  const r = await send("script.evaluate", { expression, target: { context: ctx }, awaitPromise: true });
  return r.result?.value;
}
async function shot(name) {
  const { data } = FIREFOX ? await send("browsingContext.captureScreenshot", { context: ctx }) : await send("Page.captureScreenshot", { format: "png" });
  writeFileSync(join(OUT, name + ".png"), Buffer.from(data, "base64"));
}
function noteError(text) { log.push("ERROR " + text); errors.push(text); }
function noteConsole(level, text) {
  text = text.trimEnd();
  log.push(text);
  // Unity's Debug.LogError and exceptions reach the console as errors; the page's own "[page]"
  // lines mean one of its error cards came up
  if (level === "error" || level === "assert" || /^\[page\]/.test(text) || /\b\w*Exception\b|RuntimeError|abort\(/.test(text)) errors.push(text);
}

// mouse and keys, the same over CDP and BiDi
async function mouse(type, x, y) {
  x = Math.round(x); y = Math.round(y);
  if (FIREFOX) {
    const a = { down: [{ type: "pointerMove", x, y }, { type: "pointerDown", button: 0 }], move: [{ type: "pointerMove", x, y }], up: [{ type: "pointerMove", x, y }, { type: "pointerUp", button: 0 }] }[type];
    await send("input.performActions", { context: ctx, actions: [{ type: "pointer", id: "mouse", parameters: { pointerType: "mouse" }, actions: a }] });
    return;
  }
  if (type === "down") await send("Input.dispatchMouseEvent", { type: "mouseMoved", x, y });
  const t = { down: "mousePressed", move: "mouseMoved", up: "mouseReleased" }[type];
  await send("Input.dispatchMouseEvent", { type: t, x, y, button: t === "mouseMoved" ? "none" : "left", buttons: t === "mouseReleased" ? 0 : 1, clickCount: 1 });
}
async function click(x, y) { await mouse("down", x, y); await sleep(90); await mouse("up", x, y); }
const KEYS = { Escape: ["", 27], ArrowRight: ["", 39], ArrowDown: ["", 40], ArrowLeft: ["", 37], ArrowUp: ["", 38] };
async function key(name, holdMs = 80) {
  const [wd, code] = KEYS[name];
  if (FIREFOX) {
    await send("input.performActions", { context: ctx, actions: [{ type: "key", id: "kb", actions: [{ type: "keyDown", value: wd }, { type: "pause", duration: holdMs }, { type: "keyUp", value: wd }] }] });
    return;
  }
  await send("Input.dispatchKeyEvent", { type: "rawKeyDown", key: name, code: name, windowsVirtualKeyCode: code });
  await sleep(holdMs);
  await send("Input.dispatchKeyEvent", { type: "keyUp", key: name, code: name, windowsVirtualKeyCode: code });
}

async function waitLog(re, ms, from = 0) { const t0 = Date.now(); while (Date.now() - t0 < ms) { if (log.slice(from).some((l) => re.test(l))) return true; await sleep(250); } return false; }
const lineAfter = (re, from = 0) => log.slice(from).find((l) => re.test(l)) || "";
const audioStates = async () => JSON.parse(await evaluate("JSON.stringify((window.pwAudioContexts || []).map((c) => c.state))"));
// the game's own report of its on-screen touch buttons (GameRoot.PwReportUi), or null on a build without it
const touchUi = async () => JSON.parse(await evaluate("(() => { try { window.pwUi = null; window.pwInstance.SendMessage('GameRoot', 'PwReportUi'); return JSON.stringify(window.pwUi ? { visible: window.pwUi.touch.visible, mode: window.pwUi.touchMode } : null); } catch (e) { return 'null'; } })()") || "null");
const cards = async () => JSON.parse(await evaluate("JSON.stringify(['pw-error', 'pw-crash'].filter((id) => { const e = document.getElementById(id); return e && !e.hidden; }))"));

async function connect() {
  if (FIREFOX) {
    for (let i = 0; i < 100 && !ws; i++) {
      try { const w = new WebSocket(`ws://127.0.0.1:${PORT}/session`); await new Promise((res, rej) => { w.onopen = res; w.onerror = rej; }); ws = w; } catch { await sleep(300); }
    }
    if (!ws) throw new Error("no WebDriver BiDi");
  } else {
    let targets;
    for (let i = 0; i < 100 && !targets; i++) { try { targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json(); } catch { await sleep(200); } }
    if (!targets) throw new Error("no DevTools endpoint");
    ws = new WebSocket(targets.find((t) => t.type === "page").webSocketDebuggerUrl);
    await new Promise((r) => (ws.onopen = r));
  }
  const requests = new Map();
  ws.onmessage = (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.message || m.error.message || m.error)) : p.res(m.result); return; }
    const p = m.params || {};
    switch (m.method) {
      case "Runtime.consoleAPICalled": noteConsole(p.type, p.args.map((a) => a.value ?? a.description ?? "").join(" ")); break;
      case "Runtime.exceptionThrown": noteError("uncaught " + (p.exceptionDetails.exception?.description || p.exceptionDetails.text)); break;
      case "Network.requestWillBeSent": requests.set(p.requestId, p.request.url); break;
      case "Network.responseReceived": if (p.response.status >= 400) noteError(`HTTP ${p.response.status} ${p.response.url}`); break;
      case "Network.loadingFailed": if (!p.canceled) noteError(`request failed (${p.errorText}) ${requests.get(p.requestId) || "?"}`); break;
      case "Page.javascriptDialogOpening": dialogs.push(p.message); send("Page.handleJavaScriptDialog", { accept: true }).catch(() => {}); break;
      case "log.entryAdded": if (p.type === "javascript") noteError("uncaught " + p.text); else noteConsole(p.level, p.text || ""); break;
      case "network.responseCompleted": if (p.response.status >= 400) noteError(`HTTP ${p.response.status} ${p.request.url}`); break;
      case "network.fetchError": if (!/NS_BINDING_ABORTED/.test(p.errorText)) noteError(`request failed (${p.errorText}) ${p.request.url}`); break;
      case "browsingContext.userPromptOpened": dialogs.push(p.message); send("browsingContext.handleUserPrompt", { context: p.context, accept: true }).catch(() => {}); break;
    }
  };
  if (FIREFOX) {
    const { capabilities } = await send("session.new", { capabilities: {} });
    browserName = `Firefox ${capabilities.browserVersion}`;
    await send("session.subscribe", { events: ["log.entryAdded", "browsingContext.userPromptOpened", "network.responseCompleted", "network.fetchError"] });
    ctx = (await send("browsingContext.create", { type: "tab" })).context;
    await send("browsingContext.setViewport", { context: ctx, viewport: { width: W, height: H }, devicePixelRatio: 1 });
  } else {
    const v = await send("Browser.getVersion");
    browserName = v.product;
    await send("Runtime.enable");
    await send("Page.enable");
    await send("Network.enable");
    await send("Emulation.setDeviceMetricsOverride", { width: W, height: H, deviceScaleFactor: 1, mobile: false });
  }
}
async function navigate(url) {
  if (FIREFOX) await send("browsingContext.navigate", { context: ctx, url, wait: "none" });
  else await send("Page.navigate", { url });
}
async function reload() {
  if (FIREFOX) await send("browsingContext.reload", { context: ctx, wait: "none" });
  else await send("Page.reload", { ignoreCache: false });
}

const results = [];
function check(name, ok, note) { results.push({ name, ok }); console.log(`${ok ? "ok  " : "FAIL"} ${name}: ${note}`); }

(async () => {
  await connect();
  console.log(`${browserName}, ${URL_}`);
  const t0 = Date.now();
  await navigate(URL_);
  const booted = await waitLog(/\[PW\] graphics: /, BOOT_MS);
  const bootS = (Date.now() - t0) / 1000;
  const title = booted && await waitLog(/\[PW\] title prompt: /, 30000);
  await sleep(4000);   // the title settles; late errors count
  await shot("c01_title");
  const loaderGone = !!(await evaluate("!!document.getElementById('pw-loader') && document.getElementById('pw-loader').classList.contains('done')"));
  const upCards = await cards();
  const bytes = await evaluate("performance.getEntriesByType('resource').concat(performance.getEntriesByType('navigation')).reduce((n, e) => n + (e.transferSize || e.encodedBodySize || 0), 0)");
  check("title", booted && title && loaderGone && upCards.length === 0,
    booted ? `booted in ${bootS.toFixed(1)} s (${lineAfter(/\[PW\] graphics: /).replace(/.*\[PW\] graphics: /, "").split(",")[0]}), ${(bytes / 1048576).toFixed(1)} MB fetched so far, title prompt "${lineAfter(/\[PW\] title prompt: /).replace(/.*prompt: /, "")}"` +
      `${loaderGone ? "" : ", loading card STILL UP"}${upCards.length ? `, card showing: ${upCards.join(", ")}` : ""}` : `did not reach the title within ${BOOT_MS / 1000} s${upCards.length ? ` (card showing: ${upCards.join(", ")})` : ""}`);

  if (PLAY && booted) {
    const r = JSON.parse(await evaluate("JSON.stringify(document.getElementById('unity-canvas').getBoundingClientRect())"));
    // the game's UI is laid out in 1920x1080 design units from the screen's centre (CanvasScaler
    // Expand); a 16:9 desktop window maps them straight
    const aspect = r.width / r.height;
    const cw = aspect > 16 / 9 ? 1080 * aspect : 1920, ch = aspect > 16 / 9 ? 1080 : 1920 / aspect;
    const ui = (dx, dy) => [r.x + r.width * (0.5 + dx / cw), r.y + r.height * (0.5 - dy / ch)];
    const at = (fx, fy) => [r.x + r.width * fx, r.y + r.height * fy];

    // sound waits for the first input, then starts
    const before = await audioStates();
    // headless Chromium lets audio start without a gesture whatever its flags say: ask a fresh
    // context (made by this script, so no gesture) whether this browser holds sound back at all
    const enforced = (await evaluate("(() => { const c = new AudioContext(), s = c.state, l = window.pwAudioContexts || []; c.close(); if (l.indexOf(c) >= 0) l.splice(l.indexOf(c), 1); return s; })()")) !== "running";
    await click(...ui(cw / 2 - 80, -ch / 2 + 80));   // the title's Settings gear (bottom right)
    await sleep(2000);
    const after = await audioStates();
    await shot("c02_settings");
    check("audio after input", before.length > 0 && (!enforced || !before.includes("running")) && after.includes("running"),
      `audio contexts before any input: ${before.join(", ") || "none"}; after the first click: ${after.join(", ") || "none"}` +
      (enforced ? "" : " (this browser doesn't hold sound back before a gesture, so only the start after the click was checked)"));

    // Graphics → Medium (the step slider's middle notch), then Esc saves and closes
    await click(...ui(21, 58));
    await sleep(800);
    await shot("c03_medium");
    await key("Escape");
    await sleep(1500);

    // play a moment of Day 1: title, the postcard's Start, fly Pip with the mouse and rain, then keys
    const playFrom = log.length;
    await click(...at(0.5, 0.45));
    await sleep(3500);
    await click(...ui(300, -220));
    await sleep(4000);
    await shot("c04_play");
    const touchInPlay = await touchUi();
    const [sx, sy] = at(0.2, 0.42), [ex, ey] = at(0.35, 0.55);
    await mouse("move", sx, sy);
    for (let i = 1; i <= 12; i++) { await mouse("move", sx + (ex - sx) * i / 12, sy + (ey - sy) * i / 12); await sleep(60); }
    await mouse("down", ex, ey);
    await sleep(2500);
    await shot("c05_rain");
    await mouse("up", ex, ey);
    for (const k of ["ArrowRight", "ArrowDown", "ArrowLeft", "ArrowUp"]) await key(k, 400);
    await sleep(1000);
    await shot("c06_keys");
    const touchAfter = await touchUi();
    // a desktop never shows the phone's on-screen Rain and Gust buttons
    if (touchInPlay || touchAfter)
      check("no on-screen touch buttons", !touchInPlay?.visible && !touchAfter?.visible && !touchInPlay?.mode,
        `in play: ${touchInPlay?.visible ? "SHOWN" : "hidden"} (touch mode ${touchInPlay?.mode ? "ON" : "off"}); after the mouse and keys: ${touchAfter?.visible ? "SHOWN" : "hidden"}`);
    const hints = log.slice(playFrom).filter((l) => /\[PW\] hint: /.test(l)).map((l) => l.replace(/.*hint: /, ""));
    check("play", hints.length > 0, `hints shown in play: ${hints.map((h) => `"${h}"`).join(", ") || "none (the day may not have started)"}`);
    const quality = lineAfter(/\[PW\] Auto graphics: /);
    if (quality) console.log(`     (${quality.replace(/.*\[PW\] /, "")}; ignored, the setting was Medium)`);

    // a reload keeps the setting and the progress
    await sleep(2000);
    const from = log.length;
    await reload();
    const again = await waitLog(/\[PW\] graphics: /, BOOT_MS, from) && await waitLog(/\[PW\] save: /, 20000, from);
    await sleep(3000);
    await shot("c07_reloaded");
    const gfx = lineAfter(/\[PW\] graphics: /, from).replace(/.*\[PW\] graphics: /, "").split(",")[0];
    const save = lineAfter(/\[PW\] save: /, from).replace(/.*\[PW\] save: /, "");
    const played = parseInt((save.match(/^(\d+) days played/) || [0, "0"])[1]);
    check("settings and progress survive a reload", again && /^medium \(Medium\)/.test(gfx) && played >= 1,
      again ? `graphics after reload: ${gfx}; save: ${save}` : "the reloaded page didn't boot");
  }

  check("no errors", errors.length === 0 && dialogs.length === 0,
    `${errors.length} error line(s), ${dialogs.length} browser dialog(s)${errors.length ? ":\n       " + errors.slice(0, 10).map((e) => e.slice(0, 200)).join("\n       ") : ""}`);
  writeFileSync(join(OUT, "console.txt"), log.join("\n") + "\n");
  console.log(`console: ${join(OUT, "console.txt")} (${log.length} lines); screenshots in ${OUT}`);
  const ok = results.every((r) => r.ok);
  console.log(ok ? "PASS" : "FAIL");
  await shutdown();
  process.exit(ok ? 0 : 1);
})().catch(async (e) => { console.error(e); await shutdown(); process.exit(2); });
