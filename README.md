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

## Controls

The pointer marks the spot **on the ground under Pip**. That's where rain lands and shade falls.
The camera looks down at an angle, so Pip floats above your cursor or finger and is never hidden.

| Action | Mouse | Keyboard | Gamepad | Touch |
| --- | --- | --- | --- | --- |
| Move | Pip follows the cursor | WASD / arrows | Left stick | Drag |
| Rain | Hold left button | Hold Space | Hold A / RT | Hold still until the ring fills, or the 💧 button |
| Gust | Right button: press, drag to aim, release | E (blows the way you're moving) | X / RB | Flick, or the 🌬 button |
| Drink | Automatic over open water when not raining | | | |
| Pause | Esc / P | | Start | ⏸ button |

M toggles the music. Menus work with mouse, touch, keyboard and gamepad.

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
postcards, pause and settings (volumes, fullscreen, screen shake, tilt-shift blur, hints, touch
buttons, reset progress), a results card, an ending, wordless device-aware onboarding hints,
and save/progress in PlayerPrefs.

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
    Fx/          Fx (particles, splashes, ripples), CameraRig (auto-framing), PostFx
    Audio/       AudioHub (music, ambience, buses), Sfx (pooled one-shots, musical rain notes)
    UI/          UiKit (runtime uGUI kit), Hud, Screens (title, map, postcard, pause, settings,
                 results, ending), Onboarding
    Automation/  AutoPilot (bot that plays every level), Capture (scripted screenshot tour)
  Editor/        BuildScript (batch build), ProjectSetup (URP asset, renderer, volume)
  Shaders/       Toon, Ground, Water, CloudPuff, CloudFace, Rainbow, Sky, Fx, WetMapUpdate
  Resources/     Levels/*.json, Models/*.fbx (+ Terrain/), Audio/, Icons/, Fonts/
ArtSource/       Blender generators: pw_lib (kit), props_core, props_world, characters,
                 terrain (one island per level JSON), icons, contact_sheet, build_all
Tools/           unity.sh, play.sh, make_levels.py, validate_levels.py, audio/ (synth, sfx, music)
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
Tools/unity.sh                # open the editor
```

## Testing

- `python3 Tools/validate_levels.py` statically checks every level: referenced models and icons
  exist, needs sit inside Pip's reachable area and on land (boats on water), and it estimates
  a water budget and a lower-bound completion time against par and sundown.
- `Tools/play.sh -pwAutopilot /tmp/auto -pwSpeed 2` runs a bot that plays every level through
  the same input API the player uses and prints PASS/FAIL with finishing hour vs par, oopses
  and water used. Add `-pwOnly 6,7` for specific levels, `-pwDelights` to also chase each
  secret, `-pwVerbose` for a trace. Screenshots and `report.txt` land in the given folder.
- `Tools/play.sh -pwCapture /tmp/shots` takes a scripted screenshot tour.
- Gameplay video: add `-pwVideo /tmp/vid` to an AutoPilot run to record frames and audio on a
  fixed 30 fps clock (smooth however slow the machine is), then
  `python3 Tools/make_video.py /tmp/vid <player log> out.mp4` adds captions and encodes it.
  The showcase in `Builds/PocketWeather_gameplay.mp4` came from
  `-pwOnly 1,2,3,4,6,9,12 -pwDelights -pwFreshSave`.
- Other flags: `-pwLevel levelNN` boots straight into a level, `-pwUnlockAll`, `-pwFreshSave`.
  Automated runs use a separate save slot, so they never touch real progress.

## Status

What has been verified:

- All twelve levels are complete and pass the static validator.
- The AutoPilot finishes all twelve levels before par in the Linux build, and also finds all
  twelve secret delights (including catching the bouquet in the finale). The player log has no
  exceptions or missing-asset warnings during these runs.
- Title, map, postcards, HUD, results card and the finale were reviewed from screenshots.

What hasn't been, or is known to be rough:

- **No human playtesting.** Par times were set against the bot, which plays faster than a person
  (it knows every rule and never hesitates). Par aims to sit roughly 1.5 to 3 times above the
  bot's time, but the real difficulty curve, especially Heatwave (11), needs people to play it.
- **Touch and gamepad** are implemented, but have only been exercised through the same input
  API the bot uses, never on a real touchscreen or controller. There is no mobile build yet.
- **Audio** was never listened to by a person during development. It was checked numerically
  (loudness, peaks, staying in key), so the mix may need adjusting by ear.
- **Performance** wasn't profiled. Development ran on a heavily shared machine, where the game
  sometimes dropped to low frame rates; gameplay stays correct but the cloud gets a little
  sluggish below about 10 fps.
