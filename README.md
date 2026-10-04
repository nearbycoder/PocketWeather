# Pocket Weather

**You're Pip, a tiny cloud helping a miniature world through its day.** Soak up water, rain on
thirsty gardens, shade overheated animals and puff sailboats home before the sun goes down. But
every drop counts, and too much rain fixes one problem by causing another.

Twelve single-screen tilt-shift dioramas follow one summer in Pocketvale, from Rosa's first
flower bed to her lakeside wedding with Tom, where Pip sneezes, soaks the whole party, and has to
make a rainbow to save the day.

Built with Unity 6000.6.2f1 (URP). Every model is generated with scripted Blender 4.5, and all
music and sound effects are synthesised with numpy. There are no stock or third-party assets
apart from the two OFL fonts (Fredoka and Nunito).

## Running it

A Linux build lives in `Builds/Linux/`:

```sh
Tools/play.sh                 # windowed 1600x900 (uses native Wayland when available)
Builds/Linux/PocketWeather.x86_64
```

A web build lives in `Builds/WebGL/`. Browsers won't run it from `file://`, so serve it:

```sh
Tools/serve_web.sh            # http://localhost:8080, plus LAN addresses for a phone or tablet
```

## Controls

The pointer marks the spot **on the ground under Pip**. That's where rain lands and shade falls.
The camera looks down at an angle, so Pip floats above your cursor or finger and is never hidden.

| Action | Mouse | Keyboard | Gamepad | Touch |
| --- | --- | --- | --- | --- |
| Move | Pip follows the cursor | WASD / arrows | Left stick | Drag |
| Rain | Hold left button | Hold Space | Hold A / RT | Hold still until the ring fills, or the 💧 button |
| Gust | Right button: press, drag to aim, release | E (blows the way you're moving) | X / RB (aim with the right stick) | Flick, or the 🌬 button |
| Drink | Automatic over open water when not raining | | | |
| Pause | Esc / P | | Start | ⏸ button |

M toggles the music. Menus work with mouse, touch, keyboard and gamepad (d-pad/stick to move,
A to choose, B to go back). For players who find holding a button tiring, **Settings → Tap to
rain** makes every rain control a toggle (it switches off by itself when Pip runs dry). The game
pauses itself when the window loses focus or a phone sends it to the background.

## Rules

- **Water** is your only resource (0 to 100). Raining spends it, a gust costs 6, and Pip's size
  (and therefore the shade cast) grows with how full it is. Drink from ponds, the sea, or the
  little vapour motes (morning dew, chimney steam, doused fires).
- Everything that wants something shows a **thought bubble** with a progress ring:
  - **Beds** (flowers, vegetables, crops, a pig's wallow) want moisture inside a green band.
    Above it they go soggy until the sun dries them back.
  - **Shade-seekers** (sheep, cows, a donkey, picnickers, campers) cool down under Pip. Raining
    on them makes them grumpy.
  - **Boats** are pushed by gusts into their dock or buoy. **Laundry** is dried by gusts and
    soaked again by rain. **Windmills** spin up after a few good gusts.
  - **Fires** must be rained out before they spread. The **campfire**, the **sandcastle** and the
    **wedding cake** must stay dry. **Sunflowers** want sun, not shade. The **duck pond** must not
    be drunk below its line.
  - **Rainbows** appear where it has just rained, once Pip moves off and the sun shines on the
    mist. Anyone under the arc is delighted (and forgives being rained on).
- When every need is met at the same moment, the day is saved: the rest of the day plays as a
  timelapse into a starry night. If the sun sets first, try again tomorrow.
- Each level has three **stamps**: *Day saved*, *Before par* (finish before the par hour on the
  sun track) and *Delight*, a secret reaction hinted by a riddle in the pause menu.

## Content

| # | Level | New idea |
| --- | --- | --- |
| 1 | First Drops | move, rain, drink |
| 2 | Just Right | moisture bands, sogginess |
| 3 | Sunny Pasture | shade |
| 4 | Becalmed | gusts and boats |
| 5 | Washing Day | laundry, combining verbs |
| 6 | Rainbow Picnic | rainbows |
| 7 | Windmill Hill | windmill charge |
| 8 | Duck Pond Park | finite water, pond line |
| 9 | Campfire Night | fires (evening into night) |
| 10 | Regatta | several boats, keep-dry |
| 11 | Heatwave | fast drying, sunflowers want sun |
| 12 | The Wedding | finale: the sneeze, then a rainbow for the couple |

Also included: title screen over a live diorama, a map of Pocketvale with stamps, level
postcards, pause and settings (volumes, graphics Auto/High/Low, fullscreen, screen shake,
tilt-shift blur, hints, touch buttons, tap-to-rain, reset progress), a results card, a
sunset card with a tip for whatever was left undone, an ending, device-aware onboarding hints
(mouse, touch, keyboard and gamepad wording), and save/progress in PlayerPrefs.

## Project layout

```
Assets/
  Scripts/
    Core/        GameRoot (boot), GameFlow (state machine), SaveData, GameSettings, Tween, Res
    Level/       LevelData (JSON schema + campaign list), Level (builds a diorama from JSON,
                 world queries, rain/gust routing), DayCycle, LevelScript (per-level events, finale)
    Cloud/       Cloud (water, movement, rain, drink, gust), CloudInput (mouse/touch/keys/pad/
                 virtual), CloudVisual (puffs, SDF face, expressions), RainSystem (instanced drops)
    Needs/       Need base, BedNeed/SunnyNeed, ShadeNeed, BoatNeed, MoreNeeds (laundry, windmill,
                 rainbow wish, fire, campfire, keep-dry, pond line, delights)
    World/       WaterBody, WetMap (wetness/greenness render texture), Rainbows, Motes, Critter,
                 Scatter (set dressing), AmbientLife (butterflies, fireflies)
    Fx/          Fx (particles, splashes, ripples), CameraRig (auto-framing), PostFx (tilt-shift
                 DoF, bloom, grading), Quality (Auto/High/Low graphics)
    Audio/       AudioHub (music, ambience, buses), Sfx (pooled one-shots, musical rain notes),
                 MasterLimiter (look-ahead limiter on the listener)
    UI/          UiKit (runtime uGUI kit), Hud, Screens (title, map, postcard, pause, settings,
                 results, ending), Onboarding
    Automation/  AutoPilot (bot that plays every level, expert or newcomer), Capture (scripted
                 screenshot tour), KeyTest / PadTest / TouchTest (virtual-device self-tests),
                 UiAudit, PerfProbe,
                 Recorder (gameplay video)
  Editor/        BuildScript (batch build), ProjectSetup (URP asset, renderer, volume)
  Shaders/       Toon, Ground, Water, CloudPuff, CloudFace, Rainbow, Sky, Fx, WetMapUpdate
  Resources/     Levels/*.json, Models/*.fbx (+ Terrain/), Audio/, Icons/, Fonts/
ArtSource/       Blender generators: pw_lib (kit), props_core, props_world, characters,
                 terrain (one island per level JSON), icons, contact_sheet, build_all
Tools/           unity.sh, play.sh, selftest.sh, serve_web.sh, web_smoke.mjs, make_levels.py,
                 validate_levels.py, make_video.py,
                 audio/ (synth, sfx, music)
docs/            BRIEF.md, PLAN.md (design and technical plan)
```

The scene contains only a bootstrap; everything else is built from code and the level JSON.

## Rebuilding

```sh
# Levels: JSON is generated from Tools/make_levels.py, then terrain meshes are rebuilt from it
python3 Tools/make_levels.py
blender -b -P ArtSource/build_all.py -- --group terrain

# All models (groups: core, props, characters, terrain; --only a,b; --preview DIR renders checks)
blender -b -P ArtSource/build_all.py

# Icons, and a contact sheet of model previews for review
blender -b -P ArtSource/icons.py
blender -b -P ArtSource/build_all.py -- --preview /tmp/prev --no-export
python3 ArtSource/contact_sheet.py /tmp/prev /tmp/prev/sheet.png

# Audio (needs the venv in Tools/.venv with numpy + scipy)
Tools/.venv/bin/python Tools/audio/synth.py

# Unity (Tools/unity.sh works around the editor's libxml2.so.2 dependency on Arch/CachyOS)
Tools/unity.sh compile        # import + compile, print errors
Tools/unity.sh build          # Builds/Linux/PocketWeather.x86_64
Tools/unity.sh webgl          # Builds/WebGL (switches the editor's platform; slow the first time)
Tools/unity.sh                # open the editor
```

## Testing

`Tools/selftest.sh` runs everything below that can run unattended (validator, keyboard, gamepad and
touch self-tests, the UI audit at four window shapes, and the AutoPilot campaign with delights)
and prints a one-line verdict per check; `--quick` skips the campaign.

- `python3 Tools/validate_levels.py` statically checks every level: referenced models and icons
  exist, needs sit inside Pip's reachable area and on land (boats on water), and it estimates
  a water budget and a lower-bound completion time against par and sundown.
- `Tools/play.sh -pwAutopilot /tmp/auto -pwSpeed 2` runs a bot that plays every level through
  the same input API the player uses and prints PASS/FAIL with finishing hour vs par, oopses
  and water used. Add `-pwOnly 6,7` for specific levels, `-pwDelights` to also chase each
  secret, `-pwVerbose` for a trace. Screenshots and `report.txt` land in the given folder.
- `Tools/play.sh -pwTouchTest` adds a virtual touchscreen and plays real touch gestures through
  it: drag to fly, hold to rain, drag while raining, flick to gust (and a slow drag that must
  *not* gust), tapping HUD and menu buttons. 16 checks, PASS/FAIL in the log, then it quits.
- `Tools/play.sh -pwKeyTest` drives a virtual keyboard from the title: Enter and arrows through
  the menus, arrows/WASD to fly and stop, Space to rain, Esc to pause and resume, E to gust.
  14 checks.
- `Tools/play.sh -pwPadTest` does the same with a virtual gamepad, starting at the title screen:
  A/B/d-pad through the title, map and postcard, stick to fly and stop, A to rain, Start/B to
  pause and resume, X/RB with right-stick aiming to gust. 22 checks.
- `Tools/play.sh -pwUiAudit` opens every screen (title, map, postcard, HUD, pause, settings,
  results, sunset, ending) and checks each button, slider and toggle actually receives a tap at
  its centre (nothing invisible on top) and sits fully on screen. Passes at 16:9, 20:9, 4:3 and
  portrait; it's what caught the title's untappable Settings gear.
- `Tools/play.sh -pwAutopilot /tmp/perf -pwPerf` logs `[Perf]` frame-time stats per level
  (average, p95, p99, worst, GC collections) with vsync and the frame cap turned off.
- `-pwNewcomer` makes the AutoPilot play like a first-timer: it pauses to look around, spends a
  few seconds working out each new kind of need, aims Pip and its gusts imprecisely, and lets go
  of the rain a beat late. It's a rough stand-in for a new player when judging par.
- `-pwDrainPond` starts Duck Pond Park with the pond far below the ducks' line, to prove a
  drained pond can be rained back up in time.
- `-pwLowQuality` forces Low graphics; `-pwFakeSlow` simulates a slow machine to exercise the
  Auto graphics downgrade.
- `Tools/play.sh -pwCapture /tmp/shots` takes a scripted screenshot tour (title, map, postcard,
  pause, settings, every level, the sunset card and the ending). `PW_W=1200 PW_H=900 Tools/play.sh ...` picks the window size,
  which is how 20:9, 4:3 and portrait layouts were checked.
- Gameplay video: add `-pwVideo /tmp/vid` to an AutoPilot run to record frames and audio on a
  fixed 30 fps clock (smooth however slow the machine is), then
  `python3 Tools/make_video.py /tmp/vid <player log> out.mp4` adds captions and encodes it.
  The showcase in `Builds/PocketWeather_gameplay.mp4` came from
  `-pwOnly 1,2,3,4,6,9,12 -pwDelights -pwFreshSave`.
- Other flags: `-pwLevel levelNN` boots straight into a level, `-pwUnlockAll`, `-pwFreshSave`.
  Automated runs and self-tests use a separate save slot, so they never touch real progress.

## Status

What has been verified (on the Linux build unless noted):

- All twelve levels are complete and pass the static validator.
- The expert AutoPilot finishes all twelve levels before par and finds all twelve secret delights
  (including catching the bouquet in the finale), with no exceptions or missing-asset warnings.
- The newcomer AutoPilot (`-pwNewcomer`) also finishes all twelve. Sampled over eight seeds, Day 2
  and Heatwave, the levels most likely to trip a beginner, landed about 2.5 to 3 game-hours inside
  par. A drained duck pond can be rained back up in time (`-pwDrainPond`).
- Keyboard (14 checks), touch (18), gamepad (24) and UI-reachability (9 screens x 4 window
  shapes) self-tests pass through the real Input System; the
  gamepad test drives the whole game from the title screen.
- The WebGL build runs in headless Chrome (`node Tools/web_smoke.mjs`, add `--phone` for an
  emulated Android phone in landscape): it boots in about 2 s and is played through title, map,
  postcard and a level using real browser touch events, with no errors in the console.
- Layout was checked from screenshots at 16:9, 20:9, 4:3 and portrait window shapes.
- Audio was checked on the recorded in-game mix, not by ear: about -14 LUFS integrated, 7 LU
  range, and after adding the master limiter, no clipped samples (before it, ~2000 samples
  clipped across 30 of 220 seconds).
- Performance: with vsync off, the bot playing every level averaged 2 to 8 ms per frame on the
  development machine's integrated Radeon 8060S (shared with other busy projects); 99% of frames
  were under 17 ms; on Vulkan the GPU itself took about 1.6 ms a frame. The only spike is building
  a level (about 50 ms of work), hidden behind the cloud-wipe transition.
- The Linux player runs on OpenGL Core by default. A full campaign on Vulkan (`-force-vulkan`)
  also passed 12/12 with every delight, with the GPU at 1.5 to 2.0 ms a frame on every level.

What hasn't been, or is known to be rough:

- **No human playtesting.** The newcomer bot is a heuristic stand-in (pauses, learning time,
  sloppy aim, late reactions), not a person. Real players will be slower and will misread things
  the bot can't, so par times and the difficulty curve still need people.
- **No real touchscreen or controller.** Touch and gamepad are exercised through virtual devices
  and emulated browser touch only. The quickest real-device test is `Tools/unity.sh webgl`, then
  `Tools/serve_web.sh` and opening the printed address on a phone on the same network. There is
  no native Android/iOS build (those Unity modules aren't installed on this machine), and phone
  browsers' performance is untested.
- **Audio has never been listened to by a person**, so balance, tone and repetitiveness may need
  adjusting by ear.
- **Graphics on weak GPUs:** Auto quality drops to Low when frame times stay high. On a pure
  software renderer (SwiftShader, in the headless browser test) the game stayed correct but ran at
  about 5 fps. No real low-end GPU has been tried.
- **Depth of field was invisible until late in development:** URP had been stripping its shaders
  from builds, so earlier screenshots and the first gameplay video had no tilt-shift blur. It now
  renders and was tuned in a build, but only against screenshots.
