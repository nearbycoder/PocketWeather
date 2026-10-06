# Hosting the web build

The web build is a folder of static files. It runs from any web server or static host: no special
server headers, no back end. The files are Brotli-compressed and the loader decompresses them
itself when the server doesn't send `Content-Encoding`, which is the case on GitHub Pages and
itch.io. A host that does send `Content-Encoding: br` for `.unityweb` files skips that step and
starts slightly sooner. `PW_WEB_COMPRESSION=gzip Tools/unity.sh webgl` builds a gzip version
instead.

Nothing here has been deployed. Where it goes is the owner's call.

## Build and package

```sh
Tools/unity.sh webgl          # Builds/WebGL (switches the editor's platform; slow the first time)
Tools/package_web.sh          # Builds/PocketWeather-<version>-web.zip, index.html at the root
node Tools/web_smoke.mjs      # boots and plays it in headless Chrome (--phone, --throttle 20)
```

To try it locally, run `Tools/serve_web.sh` (it also prints LAN addresses for a phone on the same
Wi-Fi).

## What a first-time visitor downloads

About 21 MB before the title screen. On an emulated connection the game boots in about 14 s at
20 Mbps and 23–24 s at 8 Mbps, with a loading card showing progress meanwhile (round 2's build
took 17–18 s and 32–33 s on the same runs). The music (7.9 MB, one file per track in
`StreamingAssets/Music/`) isn't part of that wait: the game fetches it in the background once it's
running, the title's track first, which plays a second or two after the title appears. The
measurements are in `docs/IMPROVEMENTS.md` under "Round 3 results". Browsers cache every file,
the music included (`dataCaching` is on, and the loader caches `.bundle` files), so later visits
start almost at once.

Upload the whole folder, `StreamingAssets/` included; without it the game runs silent.
`Tools/package_web.sh` zips all of it.

## Where it could go

- **GitHub Pages:** put the contents of the zip on a `gh-pages` branch (or in a `docs/`-style
  Pages folder) and enable Pages for the repository. Every file is well under Pages' 100 MB limit.
  The game would live at `https://nearbycoder.github.io/PocketWeather/`.
- **itch.io:** upload the zip as an HTML project, tick "This file will be played in the
  browser", and choose a 16:9 viewport (for example 1280x720) with the fullscreen button on. Also
  tick "Mobile friendly", since the game has touch controls.
- **The blog's own host:** copy the folder anywhere under the site, for example
  `/games/pocket-weather/play/`.

## Before publishing

- `og:image` in `index.html` is a relative path (`TemplateData/og.jpg`). Some link-preview
  crawlers need an absolute URL, so once the address is known, edit the built `index.html` (or
  the template in `Assets/WebGLTemplates/PocketWeather/index.html`) to use the full URL.
- The page is the game's own (`Assets/WebGLTemplates/PocketWeather`). Its images come from
  `python3 Tools/make_web_template.py`, which derives them from the logo, Pip's icon and the
  trailer poster.
- The browser support message appears when WebGL 2 isn't available.

## Known limits

- Phone browsers are only tested in Chrome's phone emulation (SwiftShader, no real GPU). Real
  phones, especially older ones, are untested. The page caps the render resolution at 1.5x on
  phones and 2x on desktops, and Auto graphics remembers when it had to drop to Low.
- Audio starts after the first tap or click (a browser rule).
