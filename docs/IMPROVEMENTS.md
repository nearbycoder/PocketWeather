# Pocket Weather: improvements, round 1

Written 2026-10-06 on the `improvements` branch, starting from `41a6f09` (v0.1.0). This is the
plan for the round. Nothing in it is implemented yet.

## Baseline (measured today)

Everything was run on this machine against a fresh `Tools/unity.sh build` of HEAD.

| Check | Result |
| --- | --- |
| Linux build (`Tools/unity.sh build`) | Succeeded, 133 MB, no compiler errors |
| `Tools/selftest.sh` (full) | **All passed**: validator, audio loop seams, keyboard 19/19, gamepad 24/24, touch 18/18, UI audit 9/9 at 1600x900, 1600x720, 1200x900 and 720x1280, AutoPilot 12/12 with 12/12 delights, no exceptions. The known flaky "rain waters the bed" check passed this time. |
| Expert AutoPilot | Every day was saved before par, 19 to 82 s into a 120 to 200 s day. It finished 1.3 to 4 game-hours ahead of par, with 3 oopses across the whole campaign. |
| Screenshot tour (`-pwCapture`) | All 12 days, pause, settings, fail and ending screens rendered with no exceptions. I reviewed them. |
| Web smoke (`node Tools/web_smoke.mjs`, desktop and `--phone`) | Both pass with 0 console errors. Boot (to `GameRoot booted`) took 1.4 to 2.2 s from localhost. Under SwiftShader, Auto graphics drops to Low, as expected. |
| Web download | 32.6 MB with gzip: data 22.8 MB, wasm 9.7 MB, framework 75 KB. Brotli would be 27.4 MB (data 20.4, wasm 7.0). |
| Web boot over an emulated network (Chrome DevTools throttling, 60 ms latency) | 7.6 s at 50 Mbps, **14.2 s at 20 Mbps, 33.7 s at 8 Mbps** |

Things I could not verify: how the game sounds, how it feels to a human, real touchscreens and
controllers, real phone GPUs, and any macOS or Windows build.

## What I found

Each item below comes from the code, the running game or the screenshots.

1. **The web version isn't really playable in a browser yet.** The blog lists Windows, macOS,
   Linux and Web. The release has a Linux zip and a web zip that has to be unzipped and served
   with `python3 -m http.server`. The web build uses Unity's default template: on desktop the game
   sits in a fixed 960x600 box on a white page with a Unity logo footer, and the tab title is
   "Unity Web Player | Pocket Weather" (`/tmp/pw-web/w04_play.png`). The "Made with Unity" splash
   is on (`m_ShowUnitySplashScreen: 1`), and its logo texture alone is 2.7 MB of build data. Phone
   browsers render at full devicePixelRatio (2 to 3x) with MSAA x4 and bokeh depth of field, and
   Auto quality has to discover the slowdown again on every visit. The touch design is the
   strongest thing about the game, and the platform that suits it best is the one that's hardest
   to reach.
2. **The "just right" band is invisible.** A bed's bubble ring fills to the bottom of its band
   (`BedNeed.Progress = Moisture / BandMin`) and then just stays full until the bed suddenly turns
   soggy. Nothing on screen shows the top of the band. Day 2's tomatoes have a band of 14 to 24
   (about one second of full rain), so on Days 2, 8, 11 and 12 overwatering is guesswork. The
   plan promised this ("L2 shows the bed's band on the bubble ring", PLAN §9), but it wasn't built.
3. **Bubbles show who wants something, not what to do.** Laundry shows a shirt, the windmill
   shows a windmill, boats show a boat, and shade-seekers show a sun. Nothing says "gust" or
   "shade" (see the Day 5, 7, 10 and 12 screenshots). On the wedding day the tray holds four
   near-identical sun icons.
4. **Several new ideas arrive with no hint.** `Onboarding.cs` has hints for move, drink, band,
   shade, gust (boats only), rainbow, fire, pond and sunny. Day 5 (laundry wants gusts and hates
   rain), Day 7 (the windmill wants repeated gusts), Day 9's campfire (keep it lit) and Day 10's
   sandcastle (keep it dry) have none, because their levels have an empty `teach`. There's also
   no way to look up the controls in game after the first hint fades: the pause menu has none.
5. **The music repeats a lot.** The "afternoon" loop is **38 s** long and plays on six days (5,
   6, 7, 8, 10, 11), about 16 minutes of a 30-minute summer. The wedding waltz is a 27 s loop
   under a 200 s day. The "evening" track (46 s) is generated and shipped (0.9 MB) but never used:
   no level references it.
6. **Small bugs and loose ends:**
   - **Settings → Touch buttons can't turn them off for touch players.** The toggle writes 1
     (always) or 0 (auto, which still shows them on touch). The "never" value (2) is unreachable
     (`Screens.cs:376`).
   - **The onboarding hint stays on screen over the pause menu** (tour screenshot `t03_pause`).
   - **M (mute) sets music back to 0.8** rather than the player's own volume (`GameFlow.Update`).
   - **Best finishing time is recorded but never shown** (`SaveData.bestHour`), so there's no
     target when replaying for the par stamp.
   - **The title and map always show Day 1's diorama.** `LoadDisplayLevel` computes the next
     unfinished day and then ignores it.
   - **The "rain waters the bed" check is flaky.** In KeyTest and PadTest, Pip can still be
     gliding (up to about 1.1 radii) when rain starts, so only part of the shower lands on the bed
     in the 1.2 s window.
7. **No macOS or Windows builds.** Mac Build Support is installed, so a macOS build is possible
   now, but it would be unsigned and untested (there's no Mac here). Without signing,
   Gatekeeper blocks it on download, and Apple Silicon needs at least an ad-hoc signature.
   Windows needs the Windows Build Support module, which isn't installed.
8. **Replayability ends at 36 stamps.** After that there's nothing new. Par is generous for an
   expert (the bot finishes 1.3 to 4 game-hours early), but the newcomer bot lands only 2.5 to 3
   hours inside par on the hardest days, so tightening par without human data would be guessing.

## Ranked list

Impact means how much it raises the game for a real player. Effort: S is under half a day, M is
about a day, L is several days. Risk is the chance of breaking something or of not being able to
verify it.

| # | Improvement | Impact | Effort | Risk |
| --- | --- | --- | --- | --- |
| 1 | **Hosted-ready web build**: branded full-window template, smaller and faster download, mobile resolution cap, a deploy-ready package (finding 1) | High: a link anyone can click, and the best platform for touch | M | Low to medium (Brotli fallback and splash licensing need checking) |
| 2 | **Moisture band on bed bubbles** (finding 2) | High for Days 2, 8, 11, 12: it turns guessing into a skill | S–M | Low |
| 3 | **"Wants" badges on need bubbles and tray** (finding 3) | Medium–high: every day is easier to read | S–M | Low |
| 4 | **Onboarding for the unhinted days, plus a controls card in pause** (finding 4) | Medium–high for first-time players | S | Low |
| 5 | **Fixes bundle**: touch-button setting, hint over pause, mute restores volume, best time shown, title shows your current day, flaky test made deterministic (finding 6) | Medium (each is small, and together they feel careful) | S | Low |
| 6 | **More music variety**: use the evening track, add B sections to the shortest loops, no loop on more than 3 days (finding 5) | Medium, but nobody can hear it | M | Medium: can't be judged by ear here, and the web download grows |
| 7 | macOS build, universal and unsigned, built from Linux (finding 7) | Medium for reach | S–M | High: untestable, and Gatekeeper friction without a Developer ID |
| 8 | Windows build (finding 7) | High for reach | S once the module is installed | Blocked: the owner has to install Windows Build Support |
| 9 | Persist Auto quality's downgrade between sessions (so phones start on Low) | Low–medium | S | Low (could fold into 1) |
| 10 | Post-campaign content (for example remixed "stormy" versions of days, or par challenges) | Medium for retention | L | Medium, and design work without playtest data |
| 11 | Retune par and difficulty from real playtests | High eventually | S per pass | Needs human playtesters |
| 12 | Native Android build | Medium | L | Needs the Android module and a device |

## Proposed scope for this round

Items 1 to 5. Item 6 if the web budget in item 1 leaves room. All five can be verified on this
machine, and none needs an owner decision to start.

### 1. Hosted-ready web build

What:

- A custom WebGL template in `Assets/WebGLTemplates/PocketWeather/`:
  - a full-window canvas that follows window resizes and phone rotation
  - a loading screen in the game's own style (sky gradient, Pip icon, cloud-paper progress pill,
    Fredoka title)
  - a proper `<title>`, description, Open Graph and Twitter-card tags, and a favicon made from
    the Pip icon
  - a friendly message for browsers without WebGL 2
  - no Unity footer
  - devicePixelRatio capped at 2 on desktop and 1.5 on phones
- `PlayerSettings` for the web:
  - turn the "Made with Unity" splash off if the licence allows (Unity 6 made it optional on
    Personal; I'll check this in the editor)
  - per-platform Vorbis quality overrides for music and ambience on WebGL
  - mesh compression where it doesn't hurt how things look
  - Brotli with decompression fallback, if the fallback's decompression time on load doesn't cost
    more than the download saves. Otherwise keep gzip.
- Persist Auto quality's downgrade (item 9) so returning phone players start on Low.
- `Tools/package_web.sh`: builds or zips `Builds/WebGL` into
  `Builds/PocketWeather-<version>-web.zip`. It must work from any static host with no special
  headers (GitHub Pages, itch.io, the blog's own host), with a short `DEPLOY.md` note. **Nothing
  will be deployed.**
- `Tools/web_smoke.mjs` gains a `--throttle <Mbps>` option and reports download size and time to
  boot.

Acceptance:

- Total download is **≤ 22 MB** (from 32.6 MB), and boot at an emulated 20 Mbps is **≤ 10 s**
  (from 14.2 s).
- The desktop browser shows the game filling the window with no Unity branding. The phone
  emulation fills the screen, as it does today.
- `web_smoke` passes on desktop and `--phone` with 0 console errors.
- The Linux self-test still passes in full.
- The README's Play-it section and badges are updated to match the truth.

How I'll verify: run web_smoke desktop and phone, unthrottled and at 20 and 8 Mbps, and record
the numbers before and after in this file. Review the loading-screen and in-game screenshots
myself. Diff the build report to see where the size went.

### 2. Moisture band on bed bubbles

What: bed bubbles get a ring that maps moisture onto 0 to about 1.25 × the top of the band.
- The band is drawn as a green arc behind the fill.
- The fill is blue below the band, mint inside it and coral when soggy.
- A small tick marks the top of the band.
- When moisture is within 10% of the top of the band, the bubble gives a gentle pulse while
  rain is landing on it.

Other needs keep their current ring.

Acceptance:

- On Day 2, screenshots of a bed at thirsty, in-band and soggy moisture clearly show where
  "just right" is. A new `-pwScript band` capture sets the three moisture levels directly.
- The expert AutoPilot campaign still passes 12/12, and the newcomer bot is no worse on oopses
  from sogginess than today's baseline, which I'll record before the change.
- The UI audit passes at all four window shapes.

How I'll verify: capture script screenshots reviewed by eye, then the selftest and newcomer runs.

### 3. "Wants" badges on bubbles and tray

What: a small badge on each bubble and tray icon shows the verb that helps:
- 💧 rain: beds, fires, rainbow wishes (rain beside them)
- ☁ shade: shade-seekers
- 🌬 gust: boats, laundry, windmill
- 🚫💧 "keep dry": campfire, sandcastle, cake
- ☀ "no shade": sunflowers

It uses the existing icon set where it can. Any new badge icons come from `ArtSource/icons.py`
(the generator), not hand-made art. The badge hides once the need is met.

Acceptance:

- Every need type in the 12 levels maps to a badge, and an automated check walks all levels'
  needs and fails on any unmapped type.
- The tour screenshots for Days 5, 7, 10 and 12 show the badges legibly at 1600x900 and 720x1280.
- The UI audit passes.

### 4. Onboarding gaps and a controls card

What:
- Add `teach` tags and device-aware hints for:
  - laundry: "Blow the washing dry", with the flick or right-drag hand aimed at the line
  - windmill: "Keep blowing the sails"
  - campfire: "Keep the campfire lit"
  - keep-dry: "Keep the sandcastle dry"
- The gust hint targets whatever gust-need comes first, not only boats.
- The pause menu gains a compact controls row for the device in use.
- Levels are regenerated through `Tools/make_levels.py`, not by editing the JSON by hand.

Acceptance:

- On a fresh save, Days 5, 7, 9 and 10 each show their hint within 3 s of play starting.
  KeyTest/TouchTest gain a check that a hint appears on Day 5.
- The pause controls row matches the last device used (checked in the keyboard, gamepad and
  touch self-tests).
- The validator passes.

### 5. Fixes bundle

What:
- Touch buttons becomes Auto / On / Off, using the existing cycle-button pattern from Graphics.
- The hint and hand are hidden while paused and come back on resume.
- M remembers and restores the player's music volume.
- The postcard and results show "Best 9:40" once a day has been finished.
- The title and map show the next unfinished day's diorama.
- KeyTest and PadTest wait until Pip has settled over the bed before measuring the rain.

Acceptance:
- Each fix has a self-test check where one is practical: a touch-buttons Off check in
  TouchTest, a hint-hidden-on-pause check in KeyTest, and a best-time label check in KeyTest's
  results step.
- Keyboard and gamepad tests pass **10 runs in a row** with no flake (run them in a loop and
  record the result here).

### 6. (Stretch) More music variety

What:
- Assign the unused evening track to Regatta and Heatwave.
- In `Tools/audio/music.py`, extend "afternoon" and "wedding" with a second section (new melody
  over the same chord loop) so each loop runs at least 75 s.
- Regenerate with `Tools/audio/synth.py`.

Acceptance:
- No track plays on more than 3 days.
- The loop-seam check passes, and loudness stays within ±1 LU of today's.
- The web download is still within item 1's budget.
- The README says plainly that this was done by measurement, not by ear.

## Blocked, or needs the owner

- **Where the web build will be hosted.** The packaging will work on GitHub Pages, itch.io or the
  blog's own static host. Picking one and publishing it is the owner's call, since this effort
  doesn't deploy. The blog should link to it once it's up.
- **macOS.** Should I build an unsigned universal `.app` (testable by nobody here, and Gatekeeper
  will warn), or wait until there's an Apple Developer ID for signing and notarisation
  (`rcodesign` can do both from Linux)?
- **Windows** needs Windows Build Support installed in Unity Hub (6000.6.2f1).
- **The blog lists Windows and macOS**, which don't exist yet. Either the builds arrive or the
  page should change.
- Human ears and hands are still needed for audio, feel and difficulty. This round doesn't
  change that, and the README will keep saying so.
