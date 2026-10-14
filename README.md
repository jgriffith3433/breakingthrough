# Breaking Through

A roguelike wave-defense game in Unity 6.3 LTS (`6000.3.7f1`) on the built-in render pipeline. You play Warrok, a bow-wielding brute in a moonlit hollow, and you protect a pirate playing the piano from endless waves of birds. You grow every wave, level up on feathers, and pick boons that build into a different run each time. Between runs, coins buy permanent upgrades.

The project folder is `breakingthrough`. The Unity product name is still `Controller`.

## Contents

- [Running it](#running-it)
- [How a run works](#how-a-run-works)
- [Controls](#controls)
- [Levels and boons](#levels-and-boons)
- [Surprises](#surprises)
- [Growth and arrows](#growth-and-arrows)
- [Birds](#birds)
- [Wave powers](#wave-powers)
- [Building](#building)
- [Shop](#shop)
- [The map](#the-map)
- [Art](#art)
- [Animation](#animation)
- [Run log and autopilot](#run-log-and-autopilot)
- [Project layout](#project-layout)

## Running it

| Scene | Role |
|---|---|
| `Assets/MainMenu.unity` | Shop and run setup. Build index 0 |
| `Assets/scene2.unity` | The Moonlit Hollow, where runs happen. Build index 1 |

A build starts at the menu. Pressing Play with `scene2` open skips the menu and starts a run. After a loss, any key returns to the shop.

To build for the web, use **Tools → Build → WebGL**. **Run Full Deploy** publishes into the website repo at `C:\Projects\theplacedepartmentwebsite` (`Game/vN/PlaceDepartment`), points the site's game page at the new version, and opens it locally. AGENTS.md has the details.

## How a run works

Waves count up from 1 and never stop. You lose when the musician's health reaches 0. There is no timer.

- **Musician health:** starts at 100, plus 25 per Stage Hands rank. A turkey's peck takes 1, an elite's takes 2, and a boss's takes 3. The bar flashes on each hit and throbs when health is low.
- **Bird counts:** turkeys arrive at `2 + 1.5 × wave` until wave 20. After that it's 3 more per wave, and from wave 50 they swarm. For example, wave 1 has 4 turkeys, wave 10 has 17 turkeys and 2 hens, wave 30 has 62, wave 50 has 122, and wave 100 has 572.
- **New species:** every 10th wave adds a new bird. It debuts as a pair, one of which is that wave's Alpha boss, then 2 more join each wave until its on-screen share is full. After wave 100 the same birds repeat, only larger.
- **Bird list:** the right side of the screen lists every bird in the current wave, how many are left, and a bar that fills as they fall.
- **Breaks:** 5 seconds between waves. Before waves 5, 15, 25 and so on there's a 30-second build window instead. **Enter** skips either one.

Every balance number in this file lives in `Assets/Scripts/Tuning.cs`.

## Controls

The mouse orbits the camera. The cursor locks during waves and unlocks while you build or pick a card.

| Input | What it does |
|---|---|
| **W A S D** | Move relative to the camera |
| **Mouse** | Orbit |
| **Hold left click** | Shoot, one shot per draw |
| **Hold Shift** | Lock onto the largest bird in range. Release to aim at the crosshair |
| **Space** | Jump. Before wave 8 you must release it between jumps. From wave 8, hold it to keep hopping |
| **F** | Shake off climbing birds (from wave 25) |
| **1 2 3 / click** | Pick a boon card |
| **R** | Reroll the boon cards |
| **Enter** | Skip the break or build countdown |
| **Tab** | During a build, cycle Wall, Spikes, Tower |
| **Q / E** | During a build, rotate the piece 45° |
| **Click** | During a build, place the piece between the rings |
| **= / numpad +** | Testing: next wave |
| **- / numpad -** | Testing: previous wave (not below 1) |
| **F9** | Editor only: toggle the autopilot balance probe |

Lock-on reaches 36 m, plus 1.25 m for each wave after the first, up to 170 m. You can shoot while jumping, because the bow lives on the upper-body animation layer. The tip at the bottom of the screen changes as new rules arrive.

## Levels and boons

Every bird drops feathers when it dies: 1 for a turkey, 6 for a hen, 10 for a rooster, and more for bigger birds. Elites and bosses drop four times as many. Walk near feathers to pull them in. When a wave ends, every feather left on the ground flies to you.

Feathers fill the level bar at the bottom of the screen. Each level pauses the game and offers three boons. Press **1**, **2** or **3**, or click a card. **R** rerolls. You get one reroll per run, plus one per Four-Leaf Clover rank. Boons you already hold show up more often, so a build takes shape over a run. Your boons are listed on the left.

The level curve is `5 + 4L + 0.5L²` feathers for level L, which works out to about one level per wave in the mid game.

| Boon | Rarity | Ranks | Effect |
|---|---|---|---|
| Heavy Heads | Common | 5 | +25% arrow damage |
| Quick Draw | Common | 5 | +18% draw speed, through the `FireSpeed` animator parameter |
| Standing Ovation | Common | 4 | Musician +15 max health, and heals 6 after each wave |
| Lodestone | Common | 3 | Feathers pull from 70% farther |
| Fleet Foot | Common | 3 | +12% move speed |
| Hunter's Mark | Common | 3 | Your locked bird takes +35% damage |
| Split Shot | Rare | 4 | +1 arrow per shot, fanned out |
| Bodkin Points | Rare | 3 | Arrows pass through one more bird |
| Eagle Eye | Rare | 5 | +10% crit chance. Crits deal 2x, plus 0.25x per rank |
| Ricochet | Rare | 3 | Arrows bounce to one more nearby bird |
| Firebrand | Rare | 3 | Hits set birds on fire. At rank 3, burning birds spread fire when they die |
| Frost Tips | Rare | 3 | Hits slow birds by 25% per rank. Rank 3 can freeze them |
| Bounty Hunter | Rare | 3 | A coin every 60, 45 or 30 kills |
| Seeker Fletching | Rare | 2 | Arrows curve toward birds |
| Tower Song | Rare | 3 | +1 tower in stock, and towers fire 30% faster |
| Chain Lightning | Epic | 3 | 15% chance per rank that lightning arcs to 3 more birds |
| Powder Keg | Epic | 3 | Birds explode when they die, and explosions can chain |
| Whirling Quills | Epic | 4 | Two quill blades per rank circle you and cut birds |
| Encore | Epic | 3 | The piano sends out a damaging shockwave every few seconds |
| Volley | Epic | 3 | Every 8th, 6th or 4th shot also fires a ring of arrows |
| Executioner | Epic | 2 | Birds under 12% or 24% health die on the next hit (not bosses) |
| Second Wind | Epic | 1 | Once a wave, if the musician drops below 25 health, a blast clears the stage and heals 20 |

When both parents of an evolution are at max rank, a gold evolution card shows up almost every time:

| Evolution | Parents | Effect |
|---|---|---|
| Storm Quiver | Split Shot + Chain Lightning | Every arrow chains lightning |
| Phoenix Arrows | Firebrand + Powder Keg | Burning birds explode in fire, and explosions set birds on fire |
| Grand Finale | Encore + Standing Ovation | Encore fires twice as often, reaches twice as far, and heals |
| Hailstorm | Frost Tips + Volley | Every 3rd shot is a volley, and volleys freeze |

## Surprises

- **Elites.** From wave 3, some turkeys spawn with a trait and a spinning gem over their heads: Sprinter, Bruiser, Glider, Splitter, Charger, Blinker, Swarmer or Titan. They're tougher, drop four times the feathers, and occasionally drop a chest.
- **Golden Gobbler.** On wave 4 and every 4th wave after, a gold turkey spends 22 seconds running from you. Catch it for a treasure chest and 3 coins. A chest opens a card pick that favors rare and epic boons.
- **Alpha bosses.** On every 10th wave, one of that decade's birds arrives as an Alpha. It has 4x health, a red gem, and a boss bar at the top of the screen, and it calls in turkeys around itself. It drops a chest and 6 coins.
- **Wave events.** From wave 6, a wave has a 40% chance to roll an event, never on boss or build waves. The events are Stampede, Air Raid, Feast, Golden Hour, Elite Surge and Blood Moon. The current event shows at the top left.
- **Streaks.** Kills less than 2.4 seconds apart build a streak, shown next to the crosshair. Each kill in the streak adds 1% more feathers, up to +50%, and a long streak also makes you draw a little faster.

## Growth and arrows

You grow every wave. Size is `1 + progress × 0.11`, where progress is `wave - 1` plus a little for each kill this wave. Growth also speeds up movement and pulls the camera back.

Arrow damage is `floor(1 + (wave - 1) × 0.5)`, plus 6 per Keen Arrows rank, plus a tiny bonus for kills this wave. Boons multiply it from there.

| Wave | 1 | 5 | 10 | 20 | 30 | 40 | 70 | 100 |
|---|---|---|---|---|---|---|---|---|
| Base damage | 1 | 3 | 5 | 10 | 15 | 20 | 35 | 50 |

The **Rockets! Not arrows!** upgrade keeps the bow but swaps the arrow for a rocket. A rocket explodes on a bird or on the ground. The blast radius is about 4.2 m at rank 1, 7.4 m at rank 2 and 10.6 m at rank 3. Splash damage is 65% of a direct hit.

## Birds

| Debut wave | Bird | Authored health | Arrows at debut |
|---|---|---|---|
| 1 | Turkey | 3 | 3 |
| 10 | Hen | 60 | 12 |
| 20 | Rooster | 270 | 27 |
| 30 | Goose | 480 | 32 |
| 40 | Swan | 660 | 33 |
| 50 | Emu | 840 | 34 |
| 60 | Ostrich | 990 | 33 |
| 70 | Cassowary | 1260 | 36 |
| 80 | Terror | 1500 | 38 |
| 90 | Roc | 1800 | 40 |
| 100 | Thunderbird | 2700 | 54 |

- **Health scaling:** at spawn, health is multiplied by `Tuning.HealthScale(wave)`. That's about 1.2x at wave 10, 1.7x at 30, 3.3x at 50 and 12x at 100, so a build that snowballs still hits a wall eventually.
- **Size scaling:** spawn size is multiplied by `1.18 + (wave - 1) × 0.012`, plus another 2.5% per wave after wave 100. The decade birds are meant to feel like bosses, about your height or bigger.
- **Arrival:** birds come one at a time, mixed together, after a short pause at the start of a wave. Turkeys are about a third of a second apart, and bigger birds leave longer gaps.
- **How many at once:** about 16 early, rising to 120 in late waves.
- **Spawn ring:** turkeys start 30–48 m out. Each later wave pushes the ring out by about half a meter and widens it, up to 150 m. Each spawn picks the most open spot in the ring.
- **Movement:** birds walk in a straight line at the piano, with no pathfinding. Walls in the build ring stop them.

## Wave powers

These unlock for everyone at fixed waves. The wave numbers are in `Tuning.cs`.

| Wave | Power |
|---|---|
| 8 (`JumpWave`) | Landing a jump hurts birds whose species debuted at least 8 waves earlier (`JumpLandGap`). Turkeys count as debuting at wave 0 |
| 12 (`AutoWave`) | The bow fires on its own |
| 16 (`StompWave`) | With a small turkey at your feet and you on the ground, you stomp in place |
| 25 (`ClimbWave`) | Birds whose species debuted at least 5 waves earlier climb you instead of going for the piano. Jumping and stomping stop until you press **F** |

Each power is listed on the left once you reach its wave, and the newest one stays gold for a few seconds.

## Building

During a build window the mouse is free. Buy pieces with coins and click to place them in the ring around the piano, between the inner red ring (6.5 m) and the outer gold ring (12.5 m). **Q** and **E** rotate the piece, and **Tab** switches between pieces.

| Piece | Starting count | Price | What it does |
|---|---|---|---|
| Wall | 6 | 1 coin | Blocks birds. Stops 8 turkeys, then breaks |
| Spikes | 3 | 2 coins | Doesn't block. Kills 4 turkeys, then breaks |
| Tower | 1 | 3 coins | Blocks and shoots nearby birds. Kills 6, then falls after 8 birds reach it |

Stone Pile and Watch Kit raise the starting counts. Pieces darken as they wear out, then sink away.

## Shop

Coins and ranks are saved in PlayerPrefs (`bt_coins`, `bt_best`, `bt_reached`, `bt_hens`, `bt_next`, `bt_ranks`), so they survive losing and restarting.

Coins come from:
- each cleared wave: `1 + wave / 10`
- each Alpha boss: 6
- each Golden Gobbler: 3
- Bounty Hunter, as you kill
- hens: one coin every 10–20 hens

There are eight upgrades with three ranks each. The ranks cost 2, 5 and 8 coins. The first wave of each run lists what you brought.

| Upgrade | Effect |
|---|---|
| Keen Arrows | +6 arrow damage per rank |
| Rockets! Not arrows! | The bow fires rockets. Each rank widens the blast |
| Long Stride | +25% move speed per rank |
| Stone Pile | +5 starting walls and +8 stops per wall, per rank |
| Watch Kit | +2 starting towers per rank. Towers shoot farther and faster, and last longer |
| Four-Leaf Clover | +1 reroll per run per rank, and rare and epic boons show up more |
| Opening Act | Start each run holding one random boon per rank |
| Stage Hands | The musician starts with +25 health per rank |

Once you've reached wave 10, the menu can start your next run later. Each step of 10 waves costs 10 coins, and you can only buy starts at waves you've already reached. The coins are spent when you press Play. The run after that starts from wave 1 unless you buy another start.

The menu also shows a summary of your last run.

## The map

The game takes place in the **Moonlit Hollow**, an open meadow bowl centered on the piano. It's designed for a wave game where birds walk straight lines and never need pathfinding:

- **Stone plaza:** a ring of flagstones under the piano, with four lantern posts and strings of colored bulbs around it.
- **Level ring:** flat ground out to 14 m, which covers the build zone.
- **Meadow:** wide and gently rolling, out to 150 m, which is the full spawn range. Four dirt lanes run in from the edge. Grass clumps and wildflower drifts are the only things on it, and birds walk straight through them.
- **Rim:** from 145 m the ground rises steeply into a dense pine forest with mossy boulders, so the hollow reads as an enclosed bowl. An invisible wall at 205 m stops you from climbing out.

`Assets/Editor/BuildArena.cs` builds the map into `scene2`. It runs automatically when the scene opens without an `Arena` object, and **Tools → Arena → Rebuild Arena** rebuilds it. The ground has a mesh collider on the Ground layer. Scenery has no colliders and is merged into a few meshes per sector, saved in `Assets/Art/Arena/Generated/`.

## Art

The art direction is a moonlit blue night, warm lantern light, and soft magic.

**Birds.** All 11 birds are low-poly Blender models in `Assets/Resources/Models/Birds/<Kind>.fbx`.
- **Construction:** smooth-shaded and painted with smooth vertex-color gradients. Each is built from separate parts: `Body`, `Head`, `WingL`, `WingR`, `LegL` and `LegR`.
- **Animation:** `Enemy.AnimateModel` animates the parts in code: a walk cycle, a waddle and bounce, a head bob, a peck when attacking, idle wing shuffles, and fast flapping in flight. Big birds kick up dust as they walk.
- **Tints:** turkeys get one of eight subtle tints, elites lean toward their gem color, and the Golden Gobbler is solid gold.
- **Hits and status:** a hit flashes the bird white. Burning, frozen, slowed and golden birds glow with a tint instead of spraying particles.
- **Hit capsule:** fitted to each model's body and head.

**Props.** `Chest.fbx`, `Feather.fbx`, `Quill.fbx` and `Lantern.fbx` are in `Assets/Resources/Models/`, loaded through `ArtLibrary.Make`. The feather pickups and the quill blades are drawn unlit, so they glow.

**Map scenery.** Pines, a broadleaf tree, rocks, a boulder, a bush, grass and flowers are in `Assets/Art/Arena/`.

**Where the art comes from.** All the models come from scripts in `Tools/Blender/`:
- `bt_birds.py` makes the birds and the lantern.
- `bt_arena.py` makes the map scenery.
- `BT_Art.blend` holds the working scenes and the menu key-art scene.

To rebuild a model, run the script in Blender and call `export("Turkey")` for a bird or `export_arena()` for the scenery. `Assets/Editor/ModelImportRules.cs` imports these models with smooth normals (a 60° crease angle), readable meshes and no imported materials.

**Atmosphere.** `NightAtmosphere` builds the night at runtime:
- cool moonlight, deep blue fog and trilight ambient light
- flickering warm lantern lights
- strings of bulbs
- slow ground mist that follows the player
- music notes rising from the piano

**Camera finish.** `NightGrade` on the main camera adds bloom, cool shadows with warm highlights, contrast, saturation and a vignette. Its fields can be tuned live in the inspector. Its shaders (`BTPost.shader`, `BTParticle.shader`) are in `Assets/Resources/Shaders/`, so WebGL builds include them.

**Effects.** `Fx` runs one pooled particle system each for flash, smoke, feather, ring, note and dust, with textures drawn in code. The effects are deliberately calm. A hit is a soft flash, a death throws a few feathers in the bird's colors, and an explosion is a flash, smoke and a ground ring. There's no camera shake, and no spark, glitter or ember spray.

**Shader.** Birds, scenery and the ground all use `Knoll/VertexColorLit`. On top of plain vertex color it adds:

- meadow color patches on grass greens (`_Meadow`)
- a moonlit rim on upright surfaces (`_RimColor`, `_RimStrength`): strong on birds, nearly off on scenery
- the hit flash and status tint (`_Flash`)
- a warm window-glow band that birds turn off (`_GlowBand`)
- GPU instancing

`KnollLit.mat` is the double-sided world material.

**UI.** `UiSkin` draws rounded, nine-sliced panels and pill-shaped bars in code, used by the menu, the boon cards and every bar. Boon cards rise in, and rare and epic cards get a pulsing halo. The menu backdrop, `Assets/Resources/UI/MenuArt.png`, is key art rendered in Blender, with a slow drift. It still shows the birds as they looked before they were smoothed.

## Animation

The player is the Warrok model (`Assets/Art/Warrok W Kurniawan.fbx`) on the object tagged `Player`. Its visible child is tagged `playerchild`. The bow is parented to the left hand at a fixed local pose (see AGENTS.md).

`Assets/Editor/SetupWarrok.cs` imports the Mixamo clips onto the Warrok avatar and writes `Assets/hero/Hero.controller`. The base layer is driven by the `State` integer:

| State | Clip |
|---|---|
| 0 | Aim idle |
| 1 | Walk |
| 2 | Full-body shoot, used only if the upper-body layer is missing |
| 3 | Jump |
| 4 | Aim walk |
| 5 | Stomp |
| 7 | Shake off |

Shooting plays on a second layer, Upper Body (masked by `Assets/hero/UpperBody.mask`). The `Fire` trigger starts it, and its speed follows the `FireSpeed` float, which defaults to 1 and is raised by Quick Draw and streaks.

## Run log and autopilot

Each wave writes one line to `runlog.csv` in `Application.persistentDataPath`, and a `[RunLog]` line to the console. Each line records:
- the outcome and how long the wave took
- the musician's health before and after
- kills by bird, shots and hits, and damage dealt
- level, best streak and coins
- the wave event, boons picked that wave, and the full build

Autopilot waves are tagged `-auto`.

In the editor, **F9** turns on `DevAutopilot`. It plays as a perfect archer standing by the piano (80% accuracy), picks random cards, skips breaks and runs at 3x speed. It sandboxes `MetaProgress`, so it never writes coins or ranks to your real save. Use it to find roughly where builds fall apart, then play that stretch yourself.

## Project layout

| Path | What's there |
|---|---|
| `Assets/Scripts/` | Runtime game code |
| `Assets/Editor/` | Editor-only setup: map builder, animator wiring, import rules, WebGL deploy |
| `Assets/Resources/Models/` | Birds and props, loaded at runtime |
| `Assets/Resources/Shaders/` | Post-process and particle shaders |
| `Assets/Resources/Sfx/` | Sounds: `shot`, `hit`, `death`, `stomp`, `launch`, `coin`, `shake` |
| `Assets/Resources/UI/` | Menu key art |
| `Assets/Art/Arena/` | Map scenery models, and generated map meshes in `Generated/` |
| `Assets/Art/Knoll/` | The world shader, sky shader and materials |
| `Tools/Blender/` | Blender scripts and the `.blend` for all generated art |

Layers: Default, TransparentFX, Ignore Raycast, Ground, Water, UI, Enemy.
