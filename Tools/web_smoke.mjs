#!/usr/bin/env node
// Smoke test for the WebGL build in headless Chrome (over the DevTools protocol) or Firefox (over
// WebDriver BiDi). No npm packages needed; Node >= 22 for the global WebSocket.
//
//   node Tools/web_smoke.mjs [outDir] [--phone] [--portrait] [--mouse] [--firefox] [--throttle <Mbps>] [--dir <build folder>] [--url <url>]
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
// --url opens a page that's already served (e.g. Builds/pages under /PocketWeather/ on a local
// static server, as GitHub Pages serves it) instead of serving --dir, whose size is still reported.
//
// Serves Builds/WebGL on localhost, opens it in a throwaway Chrome profile with touch emulation,
// collects the game's console log, then uses real browser touch events: tap the title, tap the
// first level card, tap Start, drag Pip and hold to rain, then hide the page behind another tab for
// 3 s (the game's audio must stop while it's hidden). A phone must go fullscreen at its first tap
// on the game, and again at the first tap after the page comes back (but not after leaving
// fullscreen on purpose); a desktop never. A phone held sideways must draw its HUD and menus
// big enough (0.47 and 0.43 CSS px per design unit). The ambience, fetched after boot like the
// music, must arrive and start. Last, the page's reload card: an error event from another script
// must show nothing, losing the WebGL context must show the card within 1 s and its Reload must
// boot the game again with its progress (the boot's save line), and an error from the game's own
// files must show the card, whose "Try to carry on" closes it. No browser dialog (alert) may open
// at any point. Screenshots at each step; exits non-zero if the game never boots, logs exceptions
// or fails a check.
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
const ti = args.indexOf("--throttle"), di = args.indexOf("--dir"), ui_ = args.indexOf("--url");
const MBPS = ti >= 0 ? parseFloat(args[ti + 1]) : 0;
const WEBDIR = di >= 0 ? args[di + 1] : join(ROOT, "Builds/WebGL");
const URL_ = ui_ >= 0 ? args[ui_ + 1] : "";
const OUT = args.find((a, i) => !a.startsWith("--") && !(ti >= 0 && i === ti + 1) && !(di >= 0 && i === di + 1) && !(ui_ >= 0 && i === ui_ + 1)) || join(ROOT, "Recordings", "web-smoke");
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
const server = URL_ ? null : spawn("python3", ["-m", "http.server", String(PORT), "--bind", "127.0.0.1"], { cwd: WEBDIR, stdio: "ignore" });
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
  try { server?.kill(); } catch {}
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

let ws, nextId = 1, ctx, pageId, coarse = !FIREFOX;   // coarse: the page reports a finger as its main pointer when PHONE   // ctx: the BiDi browsing context (Firefox)
const pending = new Map();
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
  console.log("shot", name);
  return data;
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
// A hidden page (another tab in front; a phone does the same when it switches apps) must go quiet:
// the page suspends the game's audio contexts while hidden and resumes them when it's back
let quiet = { ok: true, note: "not checked" };
const dialogs = [];
// The page's reload card (see the header). Returns where the checks began and where the reloaded
// game's log starts: the console in between (a lost context, a pretend crash) isn't held against it.
async function crashCheck() {
  // (an older page has no card: then these say it's hidden, and the Reload step is skipped)
  const visible = async () => !!(await evaluate("!!document.getElementById('pw-crash') && !document.getElementById('pw-crash').hidden"));
  const title = async () => (await evaluate("(document.getElementById('pw-crash-title') || {}).textContent || ''")) || "";
  const carryOn = async () => !!(await evaluate("!!document.getElementById('pw-crash-carry-on') && !document.getElementById('pw-crash-carry-on').hidden"));
  const tapButton = async (id) => {
    const b = JSON.parse((await evaluate(`JSON.stringify(document.getElementById('${id}')?.getBoundingClientRect() ?? null)`)) || "null");
    if (b) await tap(b.x + b.width / 2, b.y + b.height / 2);
  };
  const start = log.length;
  // another script's error is left alone
  await evaluate("window.dispatchEvent(new ErrorEvent('error', { message: \"TypeError: someone else's bug\", filename: location.origin + '/extension.js', lineno: 3 })), true");
  await sleep(600);
  const stray = await visible();
  const strayLogged = log.slice(start).some((l) => /\[page\] left alone a problem from another script: TypeError: someone else's bug/.test(l));
  // losing the picture shows the card, with Reload only
  const t0 = Date.now();
  const lost = await evaluate("(() => { const gl = document.getElementById('unity-canvas').getContext('webgl2'); const x = gl && gl.getExtension('WEBGL_lose_context'); if (x) x.loseContext(); return !!x; })()");
  let lostMs = -1;
  for (let i = 0; i < 30 && lost; i++) { if (await visible()) { lostMs = Date.now() - t0; break; } await sleep(100); }
  const lostTitle = await title(), lostCarry = await carryOn();
  await shot("w09_lost_card");
  // Reload boots the game again, with the progress it had (Day 1 was started above)
  const reload = log.length;
  await tapButton("pw-crash-reload");
  // (the save line comes a moment after the graphics line that marks a boot)
  const rebooted = lostMs >= 0 && await waitLog(/\[PW\] graphics/, 300000, reload) && await waitLog(/\[PW\] save: /, 20000, reload);
  const save = log.slice(reload).find((l) => /\[PW\] save: /.test(l)) || "";
  const played = parseInt((save.match(/save: (\d+) days played/) || [0, "0"])[1]);
  await sleep(4000);
  const cardAfter = await visible();
  // the game's own failure (a pretend WebAssembly trap from its framework file) shows the card,
  // and "Try to carry on" closes it
  const crash = log.length;
  await evaluate("window.dispatchEvent(new ErrorEvent('error', { message: 'RuntimeError: unreachable (web_smoke pretends)', filename: location.origin + '/Build/WebGL.framework.js', lineno: 1 })), true");
  await sleep(600);
  const stopped = await visible(), stoppedTitle = await title(), stoppedCarry = await carryOn();
  await shot("w10_stopped_card");
  if (stopped && stoppedCarry) await tapButton("pw-crash-carry-on");
  await sleep(500);
  const closed = !(await visible());
  const ok = !stray && strayLogged && lost && lostMs >= 0 && lostMs <= 1000 && /lost sight/.test(lostTitle) && !lostCarry &&
    rebooted && played >= 1 && !cardAfter && stopped && /got lost/.test(stoppedTitle) && stoppedCarry && closed && dialogs.length === 0;
  const note = `another script's error: ${stray ? "SHOWED THE CARD" : "nothing shown"}${strayLogged ? "" : " (not logged as left alone)"}; ` +
    `lost context: ${!lost ? "COULDN'T LOSE IT" : lostMs < 0 ? "NO CARD" : `card in ${lostMs} ms ("${lostTitle}", carry-on ${lostCarry ? "SHOWN" : "hidden"})`}; ` +
    `Reload: ${rebooted ? `booted again, ${save.replace(/.*save: /, "").trim() || "no save line"}` : "DIDN'T BOOT"}${cardAfter ? ", card STILL UP" : ""}; ` +
    `game error: ${stopped ? `card ("${stoppedTitle}", carry-on ${stoppedCarry ? "shown" : "MISSING"})${closed ? ", closed by Try to carry on" : ", NOT CLOSED"}` : "NO CARD"}; ` +
    `browser dialogs ${dialogs.length}${dialogs.length ? `: "${dialogs[0].split("\n")[0].slice(0, 90)}"` : ""}`;
  return { ok, note, start, rebootLog: reload, crashLog: crash };
}
let fullscreen = "";
let refullscreen = null;   // fullscreen when the page came back, after the next tap, and after leaving it on purpose and tapping
// mean brightness (0 to 1) of a screenshot, worked out by the browser itself
async function brightness(png) {
  return await evaluate(`new Promise((done) => { const i = new Image(); i.onload = () => { const c = document.createElement("canvas"); c.width = 64; c.height = 32;
    const g = c.getContext("2d"); g.drawImage(i, 0, 0, 64, 32); const d = g.getImageData(0, 0, 64, 32).data; let s = 0;
    for (let k = 0; k < d.length; k += 4) s += d[k] + d[k + 1] + d[k + 2]; done(s / (d.length / 4) / 765); }; i.onerror = () => done(-1); i.src = "data:image/png;base64,${png}"; })`);
}
async function hiddenCheck() {
  const audio = async () => JSON.parse(await evaluate("JSON.stringify([document.visibilityState, (window.pwAudioContexts || []).map((c) => [c.state, c.currentTime])])"));
  const [, before] = await audio();
  if (!before.length) return { ok: false, note: "no audio context found (window.pwAudioContexts is empty)" };
  let other;
  if (FIREFOX) other = (await send("browsingContext.create", { type: "tab", background: false })).context;
  else other = (await send("Target.createTarget", { url: "about:blank", background: false })).targetId;
  await sleep(500);
  const [vis, start] = await audio();
  await sleep(3000);
  const [, end] = await audio();
  if (FIREFOX) { await send("browsingContext.activate", { context: ctx }); await send("browsingContext.close", { context: other }); }
  else { await send("Target.activateTarget", { targetId: pageId }); await send("Target.closeTarget", { targetId: other }); }
  await sleep(2500);
  const [back, after] = await audio();
  // the page draws again (fullscreen ending while hidden once left the game's canvas black)
  const lum = await brightness(await shot("w07_back"));
  const paused = log.some((l) => /\[PW\] paused: the game lost focus/.test(l));
  if (vis !== "hidden") return { ok: !FIREFOX ? false : true, note: `the page didn't report itself hidden (${vis}), so nothing was checked` };
  const hiddenRun = Math.max(...end.map((c, i) => c[1] - start[i][1]));
  const backRun = Math.min(...after.map((c, i) => c[1] - end[i][1]));
  const ok = hiddenRun < 0.3 && back === "visible" && backRun > 1 && lum > 0.15;
  return { ok, note: `audio clock moved ${hiddenRun.toFixed(2)} s in 3 s hidden (${end.map((c) => c[0]).join(", ")}), ${backRun.toFixed(2)} s in 2.5 s after coming back (${after.map((c) => c[0]).join(", ")}); play ${paused ? "paused itself" : "did NOT pause"}; brightness on return ${lum.toFixed(2)}${lum > 0.15 ? "" : " (BLACK)"}${ok ? "" : " (it should be quiet while hidden and play again after)"}` };
}
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
    pageId = page.id;
    ws = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((r) => (ws.onopen = r));
  }
  ws.onmessage = (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.rej(new Error(m.message || m.error.message || m.error)) : p.res(m.result); return; }
    if (m.method === "Runtime.consoleAPICalled") log.push(m.params.args.map((a) => a.value ?? a.description ?? "").join(" "));
    if (m.method === "Runtime.exceptionThrown") log.push("EXCEPTION " + (m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text));
    if (m.method === "log.entryAdded") log.push((m.params.type === "javascript" ? "EXCEPTION " : "") + (m.params.text || ""));
    // a browser dialog (Unity's alert() for an unhandled error): note it and dismiss it, so the page
    // isn't left blocked
    if (m.method === "Page.javascriptDialogOpening") { dialogs.push(m.params.message); send("Page.handleJavaScriptDialog", { accept: true }).catch(() => {}); }
    if (m.method === "browsingContext.userPromptOpened") { dialogs.push(m.params.message); send("browsingContext.handleUserPrompt", { context: m.params.context, accept: true }).catch(() => {}); }
  };
  if (FIREFOX) {
    const { capabilities } = await send("session.new", { capabilities: {} });
    console.log(`Firefox ${capabilities.browserVersion}`);
    await send("session.subscribe", { events: ["log.entryAdded", "browsingContext.userPromptOpened"] });
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
  const url = URL_ || `http://127.0.0.1:${PORT}/index.html`;
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
    // and a phone held sideways draws its menus bigger (logged at boot), so fewer units fit
    const boost = parseFloat(((log.filter((l) => /\[PW\] menu scale: /.test(l)).pop() || "").match(/, x([\d.]+) at /) || [0, "1"])[1]);
    const cw = (aspect < 1 ? 1200 : aspect > 16 / 9 ? 1080 * aspect : 1920) / boost;
    const ch = (aspect < 1 ? 1200 / aspect : aspect > 16 / 9 ? 1080 : 1920 / aspect) / boost;
    const ui = (dx, dy) => [r.x + r.width * (0.5 + dx / cw), r.y + r.height * (0.5 - dy / ch)];
    // held upright and scaled up, the menus use their narrow layouts (a taller postcard, Start at
    // the bottom in the middle while a new player has no Encore)
    const narrow = aspect < 1 && boost > 1;
    await tap(...at(0.5, 0.5));                    // title: tap anywhere (a fresh profile goes straight to Day 1's postcard)
    await sleep(3500);
    await shot("w02_after_title");
    // a phone's first tap on the game asks for fullscreen; desktops stay as they are
    fullscreen = await evaluate("document.fullscreenElement ? document.fullscreenElement.id || document.fullscreenElement.tagName : ''");
    if (!narrow) await tap(...ui(-739, 151));      // map's Day 1 card, for builds that show the map first (harmless on the postcard)
    await sleep(3000);
    await shot("w03_postcard");
    await tap(...(narrow ? ui(0, -395) : ui(300, -220)));   // postcard: Start
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
    quiet = await hiddenCheck();
    // coming back from another app: a phone whose fullscreen ended while hidden is asked again at
    // its next tap; leaving fullscreen with the page showing sticks. The taps land beside the pause
    // menu (play paused itself while hidden), on its dimmed backdrop.
    const fsNow = async () => await evaluate("document.fullscreenElement ? document.fullscreenElement.id || document.fullscreenElement.tagName : ''");
    const backdrop = ui(-cw / 2 + (narrow ? 50 : 120), 0);
    const lost = await fsNow();
    await tap(...backdrop);
    await sleep(1200);
    const again = await fsNow();
    let kept = "";
    if (again) {
      await evaluate("document.exitFullscreen().then(() => 'left')");
      await sleep(800);
      await tap(...backdrop);
      await sleep(1200);
      kept = await fsNow();
    }
    refullscreen = { lost, again, kept };
    await shot("w08_after_return_tap");
  }
  console.log(`hidden page: ${quiet.note}`);
  // a phone's HUD is drawn at least 0.47 CSS px per design unit (44 px pause button), whatever
  // the canvas's pixel ratio
  const hs = (log.find((l) => /\[PW\] HUD scale: /.test(l)) || "").match(/HUD scale: ([\d.]+) CSS px per design unit, top bar x([\d.]+) at (\S+) \(([\d.]+) px/);
  const hudCss = hs ? parseFloat(hs[1]) * parseFloat(hs[2]) : 0;
  const hudOk = !booted || !PHONE || hudCss >= 0.47;
  console.log(`HUD scale: ${hs ? `${hudCss.toFixed(3)} CSS px per design unit (canvas ${hs[3]}, ${hs[4]} px per CSS px, top bar x${hs[2]})` : "not logged (a desktop-sized screen isn't scaled)"}${hudOk ? "" : " (too small for a phone)"}`);
  // a phone, held either way, draws its menus at least 0.43 CSS px per design unit (28-unit text
  // is then 12 px); a desktop window keeps them at their design scale
  const ms = (log.filter((l) => /\[PW\] menu scale: /.test(l)).pop() || "").match(/menu scale: ([\d.]+) CSS px per design unit, x([\d.]+) at (\S+)/);
  const menuCss = ms ? parseFloat(ms[1]) : 0;
  const menuOk = !booted || (PHONE ? menuCss >= 0.43 : !ms);
  console.log(`menu scale: ${ms ? `${menuCss.toFixed(3)} CSS px per design unit (x${ms[2]} at ${ms[3]})` : "not logged (design scale)"}${menuOk ? "" : PHONE ? " (too small for a phone)" : " (should stay at its design scale here)"}`);
  // (Firefox without BiDi's touch override reports no touch points, so the page can't tell it's a phone)
  const fsChecked = !(PHONE && !coarse);
  const fsOk = !booted || !fsChecked || (PHONE ? fullscreen === "pw-page" : fullscreen === "");
  console.log(`fullscreen after the first tap: ${fullscreen || "no"}${!fsChecked ? " (not checked: this browser can't pose as a touch-screen phone)" : fsOk ? "" : PHONE ? " (a phone should go fullscreen)" : " (a desktop should NOT go fullscreen)"}`);
  // after switching apps a phone asks again (desktops never go fullscreen); leaving on purpose sticks
  const rf = refullscreen;
  const rfOk = !booted || !fsChecked || !rf || (PHONE ? rf.lost === "" && rf.again === "pw-page" && rf.kept === "" : rf.again === "");
  if (rf) console.log(`fullscreen after coming back: ${rf.lost || "no"}; after the next tap: ${rf.again || "no"}${PHONE ? `; left on purpose, then a tap: ${rf.kept || "no"}` : ""}` +
    `${!fsChecked ? " (not checked)" : rfOk ? "" : PHONE ? " (a phone should ask again after coming back, but not after leaving on purpose)" : " (a desktop should NOT go fullscreen)"}`);
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
  let crash = { ok: !booted, note: "not run (the game didn't boot)", start: log.length, rebootLog: log.length, crashLog: log.length };
  if (booted) crash = await crashCheck();
  console.log(`reload card: ${crash.note}${crash.ok ? "" : " (FAILED)"}`);
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
  // the ambience loops arrive after boot (like the music) and start once they're in
  const amb = log.map((l) => l.match(/\[PW\] ambience (\w+): playing/)).filter(Boolean).map((m) => m[1]);
  const ambFetched = log.map((l) => l.match(/\[PW\] fetched amb_(\S+) in ([\d.]+) s/)).filter(Boolean).map((m) => `${m[1]} (${m[2]} s)`);
  const ambOk = !booted || amb.length > 0;
  console.log(`ambience: ${amb.length ? `playing ${[...new Set(amb)].join(", ")}` : "never started"}; fetched ${ambFetched.join(", ") || "none"}${ambOk ? "" : " (the ambience should arrive and play)"}`);
  writeFileSync(join(OUT, "console.txt"), log.join("\n"));
  // the console between losing the context and the reloaded game's boot, and after the pretend
  // crash, belongs to the reload-card checks; the page's own "[page]" lines are expected
  const ex = log.filter((l, i) => (i < crash.start || (i >= crash.rebootLog && i < crash.crashLog)) && /exception|error/i.test(l) && !/favicon/i.test(l) && !/^\[page\]/.test(l));
  console.log(`console lines ${log.length}, error/exception lines ${ex.length}`);
  ex.slice(0, 15).forEach((l) => console.log("  " + l.slice(0, 200)));
  for (const l of log.filter((l) => /\[PW\]/.test(l)).slice(0, 12)) console.log("  " + l.slice(0, 160));
  await shutdown();
  process.exit(booted && ex.length === 0 && crash.ok && dialogs.length === 0 && remembered && rotateOk && hintOk && promptOk && clockOk && ambOk && quiet.ok && fsOk && rfOk && hudOk && menuOk ? 0 : 1);
})().catch(async (e) => { console.error(e); await shutdown(); process.exit(2); });
