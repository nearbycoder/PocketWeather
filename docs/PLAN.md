# Pocket Weather: Game Design & Technical Plan

## 1. Pitch

**You're Pip, a tiny cloud helping a miniature world through its day. Soak up moisture, rain on
thirsty gardens, shade overheated animals and puff sailboats home before the sun goes down. But
every drop counts, and too much rain fixes one problem by causing another.**

Twelve single-screen tilt-shift dioramas take you through one summer in *Pocketvale*, ending at
Rosa and Tom's lakeside wedding. That's where you sneeze, soak the wedding, and have to make a
rainbow to save it.

## 2. Design pillars

1. **Direct, squishy, tactile.** You touch the world and the cloud floats right above your finger.
   Every input gets an instant, physical, cute response: the cloud squashes, swells, puffs its
   cheeks, and its eyes follow what you do.
2. **A world that visibly reacts.** Rain darkens the soil and leaves puddles. Dry grass turns
   green where you water it, and flowers pop open. Sheep sigh under your shade, cats hiss if you
   soak them, laundry flaps in your gusts, boats heel over. You learn the rules by poking at the
   world and seeing what happens, not by reading text.
3. **Every drop counts.** There's one resource, water, and three uses for it (rain, gust, and
   being big enough to cast shade). Every level has several needs competing for it, and
   over-doing anything has a cost.
4. **Music in the weather.** Each raindrop is a note in the background track's key, collecting
   vapor plays rising arpeggios, and finished needs ring chimes. Playing well sounds good.
5. **Cozy, short and replayable.** Levels run 1–3 minutes. Failing is gentle ("the sun has set,
   try again tomorrow"). Each level has three stamps to earn on separate runs.

## 3. Core loop

```
 ┌──────────── read the diorama: who needs what? (thought bubbles) ───────────┐
 │                                                                            │
 ▼                                                                            │
 drink (lake / pond / vapor motes) ──► spend: rain · gust · shade ──► world reacts
 ▲                                          │                                 │
 └────────────── empty? go drink ◄──────────┘     needs met ✓ / oops ✗ ───────┘
                                                   all met → sunset timelapse → stamps
```

Second to second: glide, aim, hold to rain, watch the meter, let go at the right moment.
Minute to minute: plan a route between water and needs, handle conflicts (the flowers sit next to
the drying laundry), find the delight.
Session to session: earn stamps, beat par times, find each level's secret delight, reach the
wedding.

## 4. Controls (designed for touch and mouse from day one)

The cloud floats at a fixed height over the diorama. **The pointer marks the spot on the ground
under the cloud**, which is where rain lands and shade falls. Because the camera looks down at
about 50°, the cloud is drawn *above* the pointer on screen, so a finger never hides it.

| Action | Touch | Mouse / keyboard | Gamepad |
| --- | --- | --- | --- |
| Move | Drag one finger | Cloud follows the cursor · WASD/arrows | Left stick |
| Rain | Hold the finger still for a moment (a ring fills), then drag to keep raining · or hold the on-screen 💧 button | Hold left button · Space | A / RT |
| Gust | Flick: a fast swipe, released quickly, blows in the swipe direction · or the on-screen 🌬 button | Right button: press, drag to aim (an arrow shows), release · E blows the way you're moving | X / RB (stick direction) |
| Drink | Automatic while over open water and not raining | same | same |
| Pause | ⏸ button | Esc / P | Start |

A soft ground reticle marks the target point, and while raining it turns into a rain-area ring.
All menus support mouse, touch, keyboard and gamepad navigation.

## 5. Mechanics in detail

### 5.1 The cloud (Pip)
- **Water** ranges from 0 to 100. Size scales with water (radius 0.55 to 1.25 world units), and
  colour goes from bright white when empty to soft blue-grey when full. Shade radius = cloud
  radius.
- **Movement:** a critically damped spring toward the target (≈0.12 s response) with a speed cap
  and slight overshoot. The body banks into turns and squashes and stretches along its velocity.
  Its 7–9 puffs follow with lag, wobble and breathing, like jelly.
- **Rain:** 14 water/s when held, carried by about 45 drops/s that spawn inside the cloud's
  footprint. Drops inherit the cloud's velocity, so rain from a moving cloud falls at a slant.
  Each drop carries its share of water to whatever it hits (bed, creature, pond, ground). A light
  drizzle starts first and ramps to a full shower over 0.35 s, so taps give precise small doses.
- **Empty:** rain sputters a few drops with a "pfft". Pip goes tiny, wispy and sweaty-faced.
- **Drink:** over open water and not raining, vapour tendrils spiral up and Pip gains 30
  water/s. The slurp sound rises in pitch as Pip fills. At full there's a "ding" and a sparkle,
  and the cheeks puff.
- **Vapour motes:** little glowing puffs (dew at dawn, steam from chimneys, kettles, hot springs
  and doused fires). Touching one gives +6 water and a rising arpeggio note that climbs with
  combos. Dew evaporates as the day warms up.
- **Gust:** costs 6 water (no gust below 6, just a weak "pff"). It's a 50° cone, 4.5 units long,
  in the aimed direction. Timing: anticipation (inhale and swell, 0.12 s), blow (cheeks puff,
  "o" mouth, wind streaks, leaves, whoosh), recoil.
- **Shade:** always on. There's a soft shadow disc directly under Pip (readability beats
  physical sun-angle shadows), and every surface inside that cylinder darkens through a global
  shader uniform, so the shade even falls on the sheep's backs.

### 5.2 Expressions (parametric SDF face shader, continuously blended)
Parameters: eye openness, eye "happy arc", eye squeeze (> <), pupil look offset, mouth curve,
mouth open, mouth width, blush, sweat-drop, sparkle. Presets:
idle-happy · blink · look-at · drinking (eyes closed, "o") · full (cheeks puffed) · raining-light
(content "mm") · raining-hard (squeezed) · empty/tired · gust (puffed "o") · delighted (^^ +
sparkles) · oops (wide eyes + sweat drop) · worried (when a need is failing) · sleepy (sunset) ·
sneeze ("ah… ah… ACHOO" in the finale).
The eyes look at whatever Pip is raining on, the nearest needy thing, or the direction of travel.

### 5.3 The day
Each level is one day. The sun runs from dawn to dusk over `dayLength` seconds (100–200).
Lighting, sky gradient, ambient light and fog animate continuously: peach dawn → white-gold noon
→ amber afternoon → pink-orange sunset → violet dusk. The HUD sun-track shows the time and the
**par** marker.
- When every need is met at the same moment, the **day is saved**. The world celebrates and the
  rest of the day plays as a 4-second timelapse (sun sets, windows light up, fireflies, Pip
  yawns), then the stamps card appears.
- If the sun sets before that, the level ends with "The sun has set", showing which needs were
  left, with Retry as the default.

### 5.4 Needs (shown as thought bubbles with icon + progress ring)
| Need | Rule | Feedback |
| --- | --- | --- |
| **Bed** (flowers, veg, crops, wallow) | Moisture 0..max with a green target band; met while inside the band; above the band it's *soggy* (unmet) until the sun dries it back; optional dry rate | Sprouts grow → bloom with a petal pop; soggy = droop + puddle; soil darkens |
| **Shade-seeker** (sheep, cow, dog, cat, people) | Comfort fills while shaded (3–6 s) and drains slowly in sun; once full it's met and stays met... unless rained on: then grumpy (unmet, comfort halved, 2.5 s sulk) | Panting + sweat when hot; happy sigh, lies down; shakes off water / hisses / opens umbrella |
| **Sailboat** | Physics boat on water; gust impulse ∝ cone alignment & distance; met when inside the dock/buoy zone (then it moors) | Sail fills, boat heels, wake; bell rings when moored |
| **Laundry** | Wetness 1 → 0 from gusts (≈3 gusts); met when dry; rain re-wets it | Drips, flaps, colours brighten when dry |
| **Windmill** | Charge from gusts with slow decay; met at full, then stays spinning | Blades spin up, creaks, flour puffs |
| **Rainbow wish** | Met when a rainbow forms over the character | Kid jumps, hearts |
| **Fire** (haystack, bush) | Intensity drops with rain and spreads to nearby flammables if left too long; gusts make it flare and spread; met when every fire is out | Flames, smoke, sizzle + steam (collectable motes) |
| **Keep dry** (sandcastle, cake, campfire, market stall) | Met by default; rain breaks it, then it's restored after a few seconds (rebuilt/relit) or by gusting dry | Melting castle, sputtering campfire, worried faces |
| **Keep sunny** (sunflower) | Met unless shaded; droops while shaded and recovers 2 s after | Turns to face the sun |
| **Pond line** | Met while the finite pond stays above its line; drinking lowers it, rain raises it | Ducks fret, shoreline shows |

### 5.5 Rainbows (the signature move)
"Rainbows appear where it just rained, once the sun comes back out." Rain builds a sparkling
**mist** over an area (a CPU grid that decays over 6 s). When an area holds enough mist (about
1.2 s of rain) and Pip moves off so the sun shines on it for 0.6 s during daylight, a rainbow arcs
over it for 7 s, with a chime chord, sparkles and delighted bystanders. Rainbows cancel the
grumpiness of anyone they arc over (wet picnickers forgive you) and fulfil rainbow wishes.
Cooldown 6 s. It's taught in level 6 so the wedding finale plays as a "you know what to do!"
moment.

## 6. Levels: twelve dioramas, one summer in Pocketvale

Every level adds one new idea and combines it with earlier ones. Each has **three stamps**:
☀ *Day saved* (finish) · ⏱ *Before par* (finish before the par hour) · ✿ *Delight* (a secret,
hinted with a riddle in the pause menu). Stamps are kept across runs.

| # | Diorama | New idea | Needs | Water | Delight | Day / Par |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | **First Drops** — Rosa's flower garden | move · rain · drink | 2 flower beds | spring pond (∞), Pip starts at 40 | Wake the snail (rain on it) | 120 s / 11:00 |
| 2 | **Just Right** — the allotment | target band, sogginess | carrots, cabbages, tomatoes (narrow bands) | rain barrel pond (∞) + dew | Grow the prize pumpkin (huge water) | 130 s / 12:00 |
| 3 | **Sunny Pasture** | shade | 3 hot sheep, 1 sapling | stream (∞) | Rain on the mud hollow so the lamb splashes | 130 s / 12:00 |
| 4 | **Becalmed** — Tom's harbour | gust | Tom's boat home, 2 window boxes | sea (∞) | Ring the buoy bell with a gust | 140 s / 13:00 |
| 5 | **Washing Day** | combining; keep-dry | 2 laundry lines, veg bed, napping dog (shade) | well-pond (∞), chimney steam | Rain on the puddle-jumping kid | 150 s / 13:00 |
| 6 | **Rainbow Picnic** — Rosa & Tom's date | rainbow | a kid's rainbow wish, 2 hot picnickers, wildflower patch | brook (∞) | A rainbow over Rosa & Tom (hearts) | 150 s / 14:00 |
| 7 | **Windmill Hill** | windmill charge | windmill, wheat field (large bed), donkey shade | pond (∞), dew | Fly the kite | 160 s / 14:00 |
| 8 | **Duck Pond Park** | finite water | 2 big flowerbeds, old man on the bench (shade), pond line for the ducks | finite duck pond, fountain motes, dew | Fill the birdbath for the robin | 170 s / 15:00 |
| 9 | **Campfire Night** (evening→night) | fire | haystack fire (spreads), keep the campfire lit, 2 hot campers | lake (∞), fire steam | Gust the campfire into a roar | 160 s / 19:30 |
| 10 | **Regatta** — the beach | multiple boats, keep-dry | 2 boats to buoys, sandcastle, 2 sunbathers | sea (∞) | Shower the seal on its rock | 170 s / 15:00 |
| 11 | **Heatwave Farm** | drying, keep-sunny | pumpkins (dry fast), pig wallow, 2 cows, sunflower in sun | trough pond (∞), dew | Shade the ice-cream cart | 180 s / 15:30 |
| 12 | **The Wedding** — lakeside chapel | finale: sneeze + rainbow | flower arch, 3 hot guests, keep the cake dry, boat with musicians → *ACHOO* → rainbow for the couple | lake (∞) | Catch the bouquet | 200 s / 18:00 |

**Difficulty curve:** levels 1–4 teach one verb each, with generous time and wide bands. 5–8
combine verbs, add constraints (keep-dry, finite water) and tighten par. 9–11 add pressure
(spreading fire, heat-drying, many simultaneous needs). 12 is a victory lap that uses everything,
with an authored twist.

**Narrative beats (told through title postcards and the dioramas, almost no text):** Rosa the
florist and Tom the fisherman. L1 Rosa's garden → L4 Tom's becalmed boat → L6 their picnic date
(rainbow, hearts) → L7 flour for the cake → L8 the park where he proposes (a ring box on the
bench) → L10 the invitation kite at the regatta → L12 the wedding. Pip "sneezes" from the arch's
pollen, soaks everyone, and saves the day with a rainbow. The bride throws the bouquet and the
cloud catches it. Credits play over a flyover of every diorama at sunset.

## 7. Art direction

- **The look:** a toy-like, tilt-shift miniature. Each level is a floating chunk of earth (rounded
  square slab) with layered soil strata on the cut sides, little roots and pebbles. Seas and
  lakes that reach the edge show a glassy water cross-section in the side wall. It hangs in a
  soft gradient sky with drifting distant clouds.
- **Shapes:** chunky, rounded, bevelled; nothing sharp. People are wooden peg-dolls (rounded
  cylinder body, ball head, simple hair/hat caps, dot eyes). Animals are simplified toys (sheep
  = fluffy ball cluster + black face). Trees are lollipop/cone canopies.
- **Palette:** saturated pastels with warm light and cool lavender shadows. Grass #8CCB5E →
  lush #5DB548 (watered) / parched #C9B26A (heatwave). Soil #8A5A3C, wet soil #5A3826, water
  #4FB6D8 → deep #2A7FA8, sand #F2D9A0, roofs terracotta #E07A5F / slate #5C7A99, wood #C8935B,
  accent flowers coral #FF6F61, butter #FFD45C, lilac #B79CFF. Pip: white #FFFFFF → full #A9B9D6.
- **Lighting:** one animated directional sun with soft shadows, gradient ambient that follows the
  day, a custom stylised lit shader (soft ramp, coloured shadows, rim light, wetness darkening +
  gloss, global cloud-shade darkening, greenness tint), SSAO.
- **Camera:** perspective, narrow FOV (~24°), pitched ~52°, framing the whole diorama. A very
  slow idle sway and a gentle push-in on level start and finish.
- **Post:** Bokeh depth of field focused on the diorama centre for the tilt-shift miniature
  blur, bloom, ACES/neutral tonemapping, a slight saturation lift, vignette, and colour
  adjustments driven by time of day.
- **World FX:** custom raindrops (instanced streaks), splashes, ripple rings on water, wetness +
  greenness render textures painted from above (soil darkens, puddles shine, grass greens),
  vapour tendrils, wind streaks, leaves and petals, rainbow arc shader, fire and smoke, sparkles,
  fireflies.
- **UI:** soft rounded "cloud-paper" panels (cream #FFF8EC, ink #3B3A5A), Fredoka (OFL) for
  headings, Nunito (OFL) for body, 3D icons rendered in Blender, a squishy button press
  animation.

## 8. Audio direction

All audio is synthesised offline with numpy (`Tools/audio/synth.py`) into WAVs. There's no stock
audio.
- **Music:** gentle acoustic-toy band: kalimba, marimba, soft felt piano, warm pad, upright bass,
  brushed shaker, soft kick, glockenspiel accents, convolution reverb. Tracks: *Title*, *Morning*
  (L1–4), *Afternoon* (L5–8), *Evening* (L9–11), *Wedding* (L12 waltz), plus stingers (need met,
  day saved, sun set, stamp). Tempo and chord progression are exported as a chord timeline so
  gameplay notes stay in key.
- **Musical rain:** each landing drop (rate-limited to ~9 notes/s, randomised) plays a tone from
  the current chord on a timbre chosen by surface: leaves → kalimba, water → glass bloop, roofs →
  glock tink, soil → soft marimba. Pitch rises from left to right across the diorama like a
  xylophone.
- **SFX:** rain bed loop (intensity-mixed), drink slurp, full ding, gust whoosh (inhale + blow),
  bloom pop, need-met chime, oops boing, sizzle, crackle, splash, boat bell, windmill creak,
  laundry flap, toy-like animal voices (squeaky sheep, kazoo duck, meow), cheers/"aww" choir pad,
  UI ticks, Pip voice blips (yay / eep / hmph / achoo).
- **Ambience per level:** birds (synth FM chirps), breeze, water lapping, gulls, crickets at
  night, fire crackle.
- **Mix:** music ducks under stingers. Rain notes sit on a separate bus, kept under the music.
  Master limiter headroom −1 dBFS. Volume sliders for Music / SFX / Ambience.

## 9. UI / UX

- **Flow:** Boot → Title (live diorama + logo, "tap to play") → **Map** of Pocketvale (12 island
  buttons along a path, stamps under each, locked ones greyed) → **Postcard** (level name, art
  icon, the needs as icons, par, stamps) → Play → Day saved (timelapse + stamp animation, Next /
  Replay / Map) or Sunset (Retry / Map).
- **HUD:** Pip's water gauge (a cloud-shaped meter, top left) · sun-track (top centre) with par
  marker · needs tray (top right: icons ticking off) · pause. Needs also show as world-anchored
  thought bubbles with progress rings that shrink to a ✓ when met.
- **Onboarding without text walls:** L1 shows a ghost hand ("drag", then "hold" with a filling
  ring), the empty gauge wobbles, and the pond pulses with a drop icon. L2 shows the bed's band
  on the bubble ring. L3 has a shade hint ring. L4 has a flick hint. L6 has a rainbow hint
  vignette. Captions are at most 3 words.
- **Pause:** Resume · Restart · Map · Settings · delight riddle.
- **Settings:** Music / SFX / Ambience volume, fullscreen, screen shake, tilt-shift blur, show
  hints, on-screen touch buttons (auto/on/off), reset progress.
- **Save:** JSON in PlayerPrefs: stamps per level, best time, unlocked level, settings.
- **Transitions:** a puffy cloud wipe (cloud blobs sweep across and dissolve).

## 10. Game feel & juice list

Cloud: spring follow + overshoot · banking · squash/stretch · jelly puffs · breathing idle ·
blinks · eye tracking · swell on drink · squeeze pulses while raining · shrink as water drains ·
inhale-blow-recoil gust · hop on delight · shiver when overfull · sparkle at full.
World: drop splashes + ripple rings · soil darkening & greening · puddles · flower bounce when hit
· bloom pop + petal burst · sheep shake-off · cat hiss + dash · umbrellas pop open · boats heel
and leave wakes · trees, grass and smoke sway in gusts · leaves fly · laundry flaps · windmill
spin-up · fire flare & sizzle-steam · rainbows with sparkles · fireflies at dusk.
Feedback: need-met chime + bubble pop → ✓ flies to the HUD tray · progress rings · gauge wobble
when low · small camera shake on the sneeze · hit-stop (40 ms) on the day saved · camera push-in
on completion · confetti on the wedding · musical everything.

## 11. Technical architecture

Unity 6000.6.2f1, URP, new Input System, uGUI built at runtime. One `Main` scene holds only a
bootstrap. Everything else is constructed from code + data, which is robust to iterate on
headlessly.

```
Assets/
  Scripts/
    Core/        GameRoot (boot, state machine), SaveData, Settings, Events, Rng, Tween
    Level/       LevelData (JSON schema), LevelLibrary, LevelBuilder (terrain + props + needs),
                 LevelDirector (day timer, completion, stamps), DayCycle (sun/sky/ambient/post)
    Cloud/       CloudController (water, movement), CloudInput (pointer/touch/keys/pad gestures),
                 CloudVisual (puffs, squash, colour), CloudFace (shader params, expressions),
                 RainSystem (instanced drops, impacts), GustSystem, DrinkSystem
    World/       WetnessMap (RT painter), MistField + RainbowSystem, WaterBody, VaporMote,
                 Swayer/Reactor components, Creature (procedural anim), Boat physics
    Needs/       Need (base, events), BedNeed, ShadeNeed, BoatNeed, LaundryNeed, WindmillNeed,
                 RainbowWishNeed, FireNeed, KeepDryNeed, KeepSunnyNeed, PondLineNeed, Delights
    Audio/       AudioHub (buses, pooling), MusicDirector (beat/chord clock), RainNotes, SfxBank
    UI/          UiKit (runtime uGUI), Hud, NeedBubbles, TitleScreen, MapScreen, Postcard,
                 PauseMenu, SettingsMenu, ResultsCard, CloudWipe, Onboarding, TouchButtons
    Fx/          FxPool, CameraRig (framing, DOF focus, shake)
    Automation/  AutoPilot (bot that plays every level), Capture (scripted screenshots)
  Editor/        BuildScript (batch entry points), ProjectSetup (URP + volume + renderer),
                 ImportSettings (models/audio)
  Shaders/       PW/Toon (lit), PW/Ground (wetness/greenness), PW/Water, PW/CloudPuff,
                 PW/CloudFace (SDF), PW/Rainbow, PW/Sky, PW/Fx
  Resources/     Levels/*.json, Models/*.fbx, Audio/*.wav, Icons/*.png, Fonts/
ArtSource/       pw_lib.py (primitives/materials), props.py, characters.py, cloud.py,
                 terrain.py (per-level island from the level JSON), icons.py, build_all.py,
                 contact_sheet.py, *.blend outputs
Tools/           unity.sh, build.sh, play.sh, autopilot.sh, capture.sh, audio/synth.py,
                 validate_levels.py
```

- **Levels as data:** `Assets/Resources/Levels/levelNN.json` is the single source of truth for
  island shape, heights, water bodies, props, needs, sources, day length and par. Blender reads
  it to build the terrain mesh, Unity reads it to place everything, and the validator reads it to
  check budgets.
- **Rain physics:** each drop is raycast once at spawn (along its inherited velocity) to find its
  impact point and collider, then animated in C# and drawn with `Graphics.RenderMeshInstanced`.
  Impacts deliver exact water amounts to `IRainReceiver`s, paint the wetness map, spawn
  splashes/ripples and feed MistField and RainNotes.
- **Shade:** the global shader vector `_PW_CloudShade (x, z, radius, strength)`. Gameplay uses
  the same cylinder test.
- **Wetness/greenness:** a 256² RGBA RenderTexture over the island's XZ bounds. R = wetness
  (fades with sun), G = greenness (accumulates, persistent per run). Painted with a stamp shader.
  Toon/Ground shaders sample it by world XZ.
- **Determinism for testing:** fixed-step gameplay updates. The AutoPilot drives a virtual
  pointer through the same `CloudInput` API that devices use.

## 12. Asset list (Blender, scripted, exported FBX)

- **Pip:** puff lobes (3 shapes), face plane; the face is drawn by shader.
- **Terrain:** 12 island slabs generated from the level JSON (top surface with vertex-colour
  zones grass/path/sand/soil; strata sides; carved ponds and channels).
- **Nature:** round tree, pine, fruit tree, sapling (3 growth stages), bush, rocks ×3, reeds,
  lily pad, mushrooms, wildflower clumps, grass tufts, sunflower, hay bale/haystack, log.
- **Garden:** flower bed (soil frame) + flower heads (4 colours) + sprouts, carrot, cabbage,
  tomato vine, pumpkin (scalable), wheat patch, mud wallow, watering can, scarecrow, birdbath.
- **Buildings:** cottage ×2 (roof colours), barn, windmill (separate sails), chapel, harbour shed,
  market stall, well, fence segments, gate, bench, lamp post, signpost, mailbox, dock/pier,
  bridge, fountain, tent, campfire, picnic blanket + basket, beach umbrella, towel, sandcastle,
  ice-cream cart, wedding arch, cake table, chairs, laundry pole + clothes (shirt, sheet, sock).
- **Vehicles:** sailboat (separate sail), rowboat, buoy with bell.
- **Characters:** peg-person (body/head/hair/hat variants, recolourable), sheep, lamb, cow, pig,
  donkey, dog, cat, duck, frog, snail, robin, seal, crab, seagull.
- **Icons (rendered to PNG):** drop, sun, wind, flower, sheep, boat, shirt, windmill, rainbow,
  flame, castle, sunflower, heart, star, stamps, lock, level icons.

## 13. Milestones

| M | Content | Exit check |
| --- | --- | --- |
| M0 | Scaffold: git, Unity URP project, build script, Blender→FBX smoke test, audio synth smoke test | Linux player builds and runs, screenshot captured |
| M1 | **Core prototype:** cloud move/face/rain/drink/shade/gust on a greybox island + 1 bed, 1 sheep, 1 boat | Screenshots look alive. Feel iterated (spring constants, rain ramp, gust timing) |
| M2 | Systems: needs framework, wetness/greenness, rainbow, HUD + bubbles, level JSON + Blender terrain; levels 1–4 with real art | Levels 1–4 playable start to finish by the AutoPilot |
| M3 | Full content: all need types, all props/characters, levels 5–12, finale script | AutoPilot completes all 12 |
| M4 | Polish: menus, map, postcards, transitions, save/settings, onboarding, full audio + music, post FX, juice pass | Full playthrough screenshots reviewed, console clean |
| M5 | Verification + ship: validator, AutoPilot PASS on all levels and delights, README, Linux build in `Builds/` | Build launches, autopilot PASS |

Commit at the end of each milestone (and at stable points in between).

## 14. Risks & mitigations

| Risk | Mitigation |
| --- | --- |
| Editor quirks on CachyOS (libxml2, XWayland hang) | `Tools/unity.sh` sets `LD_LIBRARY_PATH` to a bundled libxml2 shim; the player runs with `-force-wayland` |
| I can't listen to the audio | Use well-understood synthesis recipes, measure loudness/peaks/spectra numerically, keep everything in key, keep a conservative mix |
| Can't test real touch hardware | Gesture logic is separated from devices; the AutoPilot exercises touch-style gestures; UI hit targets are ≥ 64 px at 1080p |
| Large asset count | A modular, parametric Blender kit (recolours, scale variants) and contact-sheet review |
| Tilt-shift blur hurting readability | Focus on the play plane with a moderate aperture; a setting to reduce or disable it |
| Shared-machine load (8 sessions) | Batch-mode Unity, headless Blender at low samples, close everything I start |
| Unclear rules | Each level introduces one idea with a wordless hint, bubbles always show what's wanted, oops feedback is immediate |
| Level solvability | `validate_levels.py` budget checks plus the AutoPilot actually completing every level before sunset in the built player |

## 15. The 5-minute prototype test

A new player picks it up and within five minutes:
1. **0:00–0:20** Drags and the little cloud squishes after their finger, eyes following. They
   wiggle it around just because it feels nice.
2. **0:20–1:00** Holds still and it rains: drops plink as notes in the song, the soil darkens,
   the grass greens, sprouts push up and pop into flowers. The cloud shrinks and goes "pfft" when
   empty. They float over the pond and it slurps itself fat again.
3. **1:00–2:30** Level 2: they overwater the carrots, see them droop in a puddle, and learn "just
   right". Level 3: their shadow cools a panting sheep, which sighs and flops over happily. They
   accidentally rain on one and it shakes itself off grumpily. They laugh.
4. **2:30–4:00** Level 4: a flick sends a whoosh that fills Tom's sail, and the boat heels into
   the dock with a bell. Then the sunset timelapse, a stamp thumps onto the postcard, and a
   ⏱ stamp is still empty.
5. **4:00–5:00** They replay to beat par, or press Next to see what the next diorama is.

**Pass condition:** they ask "what's in the next one?" or replay for the missing stamp without
being prompted. To get there, the cloud has to feel delightful to *move* even with no goals, the
rain has to sound musical, and the world's reactions have to be visible and funny.
