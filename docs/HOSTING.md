# Hosting the web build

The web build is a folder of static files. It runs from any web server or static host: no special
server headers, no back end. The files are Brotli-compressed and the loader decompresses them
itself when the server doesn't send `Content-Encoding`, which is the case on GitHub Pages and
itch.io. A host that does send `Content-Encoding: br` for `.unityweb` files skips that step and
starts slightly sooner. `PW_WEB_COMPRESSION=gzip Tools/unity.sh webgl` builds a gzip version
instead.

The owner has chosen GitHub Pages (below); it hasn't been deployed yet.

## Build and package

```sh
Tools/unity.sh webgl          # Builds/WebGL (switches the editor's platform; slow the first time)
Tools/package_web.sh          # Builds/PocketWeather-<version>-web.zip, index.html at the root
node Tools/web_smoke.mjs      # boots and plays it in headless Chrome (--phone, --throttle 20)
```

To try it locally, run `Tools/serve_web.sh` (it also prints LAN addresses for a phone on the same
Wi-Fi).

## What a first-time visitor downloads

About 17 MB before the title screen. On an emulated connection the game booted in 12.5 s at
20 Mbps and 19.8 s at 8 Mbps in round 8, with a loading card showing progress meanwhile (round
7's 21 MB build took about 14 s and 23.5 s in earlier runs; round 2's took 17–18 s and 32–33 s).
The music (7.9 MB, one file per track in `StreamingAssets/Music/`) and the ambience loops (2.4 MB,
`StreamingAssets/Ambience/`) aren't part of that wait: the game fetches them in the background
once it's running, the title's track and its ambience first, which play a second or two after the
title appears. The measurements are in `docs/IMPROVEMENTS.md` under "Round 3 results" and "Round 8
results". Browsers cache every file, the music included (`dataCaching` is on, and the loader
caches `.bundle` files), so later visits start almost at once.

Upload the whole folder, `StreamingAssets/` included; without it the game runs silent.
`Tools/package_web.sh` zips all of it.

## GitHub Pages

```sh
Tools/build-pages.sh               # builds the player, then stages Builds/pages (gitignored)
Tools/build-pages.sh --no-build    # stages Builds/pages from the existing Builds/WebGL
node Tools/check-pages.mjs https://nearbycoder.github.io/PocketWeather/            # title check, headless Chromium
node Tools/check-pages.mjs https://nearbycoder.github.io/PocketWeather/ --firefox  # the same in Firefox
```

`Builds/pages` is the whole site: `index.html` at its root, a `.nojekyll` file, `Build/`,
`TemplateData/` and `StreamingAssets/`. Its contents go at the root of the `gh-pages` branch.
The script also points the link-preview tags (`og:url`, `og:image`) at the Pages address
(`PAGES_URL=...` for another), refuses root-relative URLs (Pages serves the game under the
case-sensitive `/PocketWeather/` subpath) and `.br`/`.gz` files (they'd need a `Content-Encoding`
header Pages can't send), and stops if a file nears GitHub's 100 MB limit.

`check-pages.mjs` exits 0 only when the page reaches the title screen with no console errors, no
failed or 4xx/5xx requests and no error card. `--play` adds a short session under a real browser's
autoplay rule: the audio must stay suspended until the first click and run after it, Graphics set
to Medium in Settings must still be Medium after a reload, and so must the played day. To try it
before deploying, serve a copy under the same subpath:

```sh
mkdir -p Recordings/pages-serve && rm -rf Recordings/pages-serve/PocketWeather
cp -r Builds/pages Recordings/pages-serve/PocketWeather
python3 -m http.server 8471 --bind 127.0.0.1 --directory Recordings/pages-serve &
node Tools/check-pages.mjs http://127.0.0.1:8471/PocketWeather/ --play
node Tools/web_smoke.mjs --mouse --url http://127.0.0.1:8471/PocketWeather/ --dir Builds/pages
```

`web_smoke.mjs`'s hidden-tab check needs Chrome (its default, `google-chrome-stable`) or Firefox:
Playwright's headless shell never reports the page hidden, and lets audio start without a click,
so `check-pages.mjs` only checks the audio's start after the click there.

## Where else it could go

- **GitHub Pages** (the owner's choice, see below): the game would live at
  `https://nearbycoder.github.io/PocketWeather/`.
- **itch.io:** upload the zip as an HTML project, tick "This file will be played in the
  browser", and choose a 16:9 viewport (for example 1280x720) with the fullscreen button on. Also
  tick "Mobile friendly", since the game has touch controls.
- **The blog's own host:** copy the folder anywhere under the site, for example
  `/games/pocket-weather/play/`.

## Before publishing

- `og:image` in `index.html` is a relative path (`TemplateData/og.jpg`). Some link-preview
  crawlers need an absolute URL: `Tools/build-pages.sh` writes the Pages one; for another host,
  edit the built `index.html` to use the full URL.
- The page is the game's own (`Assets/WebGLTemplates/PocketWeather`). Its images come from
  `python3 Tools/make_web_template.py`, which derives them from the logo, Pip's icon and the
  trailer poster.
- The browser support message appears when WebGL 2 isn't available.
- If the game crashes, or the browser takes its graphics back (phones do that to save memory),
  the page shows its own card with a Reload button instead of Unity's developer `alert()` or a
  frozen screen. Errors from other scripts on the page (an analytics snippet, a browser
  extension) are logged to the console and otherwise left alone, so a host's own scripts can't
  stop the game with a pop-up.

## Phones and tablets

```sh
node Tools/mobile_check.mjs --device iphone            # also ipad, pixel, iphone-portrait, pixel-portrait, desktop
node Tools/mobile_check.mjs --device iphone --title-only   # memory at the title only, for comparing builds
node Tools/mobile_check.mjs --device iphone --recovery     # a tab killed for memory is noticed at the next load
```

It serves `Builds/pages` under `/PocketWeather/` itself and plays it with real touch events in the
blog's Playwright: headless WebKit with the iPhone 15 and iPad Pro 11 profiles (coarse pointer,
WebGL 2, no WebGPU), and Chromium with the Pixel 7 profile. The notch insets are pretended (headless
browsers have none). This Linux WebKit has no audio output and no AAC decoder: the script gives
the game a silent audio context that, like iOS, only starts inside a tap, and stubs the music's
media elements.

What the page does for phones:

- The game's on-screen Rain and Gust buttons show on a touch-first device (coarse pointer, no fine
  one) and after any touch, and hide at the first key, mouse movement or gamepad press: the page
  tells them apart by its pointer events (`window.pwInput`) and the game asks for it each frame.
- The notch, rounded corners and home indicator reach the game through an invisible element padded
  by `env(safe-area-inset-*)` (`#pw-safe`); the HUD, title and map keep inside them.
- Sound resumes at the end of each tap, click or key press (iOS doesn't count the touchstart Unity
  asks on). No pinch or double-tap zoom, long-press callout, context menu or text selection.
- Once the game runs, the loader's 27 MB copy of the unpacked WebAssembly is let go. The
  WebAssembly heap may grow to 1 GB (it uses about 171 MB); past that the reload card shows.
- A visit killed while on screen (a phone short of memory kills the tab silently) leaves a mark in
  `localStorage`; the next load says so on the loading card and starts Auto graphics on Low.

## Known limits

- Phones and tablets are only tested in emulation (WebKit and Chromium on a software renderer).
  Real phones, especially older ones, are untested, and so is iOS's per-tab memory limit, which
  desktop WebKit doesn't enforce. In headless WebKit with the iPhone profile the tab's process
  peaked at about 1.1 GB, about 360 MB of it this WebKit's empty page and software WebGL. The page
  caps the render resolution at 1.5x on phones and 2x on desktops, and Auto graphics remembers
  when it had to drop to Low.
- Audio starts after the first tap or click (a browser rule).
