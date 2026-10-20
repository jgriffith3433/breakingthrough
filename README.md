# Breaking Through

A roguelike wave-defense game in Unity 6.3 LTS (`6000.3.7f1`) on the built-in render pipeline. You play Wallit with a baseball bat in a moonlit hollow (Warrok's model for now; Wallit's model replaces it next), and you protect Slice, who plays the piano (the pirate pianist's model stands in until hers lands), from endless waves of Yo Bots and Spider Bots. You grow every wave, level up on bolts, and pick boons that build into a different run each time. Between runs, coins buy permanent upgrades.

Voices stay short. Wallit talks money ("Pay up."). Slice is cool and clipped ("Clean."). Yo Bots are chirpy ("Uploading victory.").

The project folder is `breakingthrough`. The Unity product name is still `Controller`.

## Contents

- [Running it](#running-it)
- [How a run works](#how-a-run-works)
- [Controls](#controls)
- [Levels and boons](#levels-and-boons)
- [Surprises](#surprises)
- [The bat](#the-bat)
- [Growth and damage](#growth-and-damage)
- [Bots](#bots)
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

- **Slice:** the musician. Her bar at the top is titled Slice. She keeps a small knife (her scalpel): when a bot comes within 1.8 m she gets up from the bench, turns to it and stabs it, at most once every 2.5 s, with a quick pale slash. A stab is blade damage worth 0.6 of the player's hit (at least 1), 15% more per Stage Hands rank, enough to finish a small Yo Bot over a few stabs. Large bots (Spider Bots, heavies, the Alpha) only get a flinch-cut, a quarter of that, and a shove back. After 1.2 s with nothing in reach she sits back down and plays. The music keeps playing throughout; the knife is a last line, the bat is the defence.
- **Slice's health:** starts at 100, plus 25 per Stage Hands rank. A Yo Bot's claw takes 1, an elite's takes 2, and a boss's takes 3. A Spider Bot's leg stab uses that same scale. The bar flashes on each hit and throbs when health is low.
- **Bot counts:** Yo Bots arrive at `2 + 1.5 × wave` until wave 20. After that it's 3 more per wave, and from wave 50 they swarm. For example, wave 1 has 4 Yo Bots, wave 10 has 17 Yo Bots and 2 Plated Yo Bots, wave 30 has 62, wave 50 has 122, and wave 100 has 572.
- **New kinds:** every 10th wave adds a new bot. It debuts as a pair, one of which is that wave's Alpha boss, then 2 more join each wave until its on-screen share is full. After wave 100 the same bots repeat, only larger.
- **Bot list:** the right side of the screen lists every bot in the current wave, how many are left, and a bar that fills as they fall.
- **Breaks:** 5 seconds between waves. Before waves 5, 15, 25 and so on there's a 30-second build window instead. **Enter** skips either one.

Every balance number in this file lives in `Assets/Scripts/Tuning.cs`.

## Controls

The mouse orbits the camera. The cursor locks during waves and unlocks while you build or pick a card.

| Input | What it does |
|---|---|
| **W A S D** | Move relative to the camera |
| **Mouse** | Orbit |
| **Left click** | Swing the bat. Click again as it comes through for the next swing of the combo |
| **Hold left click** | Wind up, then let go for a home run |
| **Hold Shift** | Lock onto the nearest bot in reach. It stays on that bot until it falls or you let go |
| **Right click / Tab** | While locked, move the lock to the next nearest bot |
| **Space** | Jump. Before wave 8 you must release it between jumps. From wave 8, hold it to keep hopping |
| **F** | Shake off climbing bots (from wave 25) |
| **1 2 3 / click** | Pick a boon card |
| **R** | Reroll the boon cards |
| **Enter** | Skip the break or build countdown |
| **Tab** | During a build, cycle Wall, Spikes, Tower |
| **Q / E** | During a build, rotate the piece 45° |
| **Click** | During a build, place the piece between the rings |
| **= / numpad +** | Testing: next wave |
| **- / numpad -** | Testing: previous wave (not below 1) |
| **F9** | Editor only: toggle the autopilot balance probe |

Lock-on reaches 26 m, plus 0.6 m for each wave after the first, up to 90 m, and more as you grow. You can swing while jumping. Touch screens have a Swing button (hold it to wind up) instead of Fire. The tip at the bottom of the screen changes as new rules arrive.

## Levels and boons

Every bot drops bolts when it's destroyed: 1 for a Yo Bot, 6 for a Plated Yo Bot, 10 for a Mega Yo Bot, and 19 to 40 for the Spider Bots (`Tuning.BoltsFor`). Elites and bosses drop four times as many. Walk near bolts to pull them in. When a wave ends, every bolt left on the ground flies to you. Bolts are your XP.

Bolts fill the level bar at the bottom of the screen. Each level pauses the game and offers three boons. Press **1**, **2** or **3**, or click a card. **R** rerolls. You get one reroll per run, plus one per Four-Leaf Clover rank. Boons you already hold show up more often, so a build takes shape over a run. Your boons are listed on the left.

The level curve is `5 + 4L + 0.5L²` bolts for level L, which works out to about one level per wave in the mid game.

| Boon | Rarity | Ranks | Effect | Mark |
|---|---|---|---|---|
| Corked Bat | Common | 5 | +25% bat damage | The barrel gets 7% fatter per rank |
| Quick Hands | Common | 5 | +18% swing and wind-up speed | White grip tape, a wrap per rank |
| Standing Ovation | Common | 4 | Slice +15 max health, and heals 6 after each wave | |
| Lodestone | Common | 3 | Bolts pull from 70% farther | |
| Fleet Foot | Common | 3 | +12% move speed | |
| Called Shot | Common | 3 | Your locked bot takes +35% damage per rank, and drops extra bolts when it falls (a quarter of its bolts per rank, at least 1 per rank) | A red ribbon off the knob |
| Wide Arc | Rare | 4 | The combo swings sweep 20° wider per rank, and come round further | A pale swoosh trails the bat |
| Follow Through | Rare | 3 | A combo swing hits the nearest 6 bots in its arc (the finisher 8); +3 per rank | A brass band per rank |
| Sweet Spot | Rare | 5 | Bots met on the outer 40% of the bat's reach have +15% crit chance per rank (a red ring flashes on a sweet-spot crit). Crits deal 2x, plus 0.25x per rank | A red ring near the tip |
| Line Drive | Rare | 3 | A bot you knock back hits one more bot behind it per rank, for 0.75x to 1.05x | A chalk stripe down the barrel |
| Pine Tar | Rare | 3 | Hits set bots burning. At rank 3, burning bots pass the fire on when they fall | Darker wood and soft flame tongues on the barrel |
| Ice Bat | Rare | 3 | Hits slow bots by 25% per rank. Rank 3 can freeze them | A frost shell and ice crystals |
| Bounty Hunter | Rare | 3 | A coin every 60, 45 or 30 kills | |
| Long Ball | Rare | 2 | Every swing also bats a bolt at the nearest bot out of reach (up to 9 m past the bat, 13.5 m at rank 2, times your size), for 0.8x or 1.05x. The only ranged option | A pouch of bolts on Wallit's hip |
| Tower Song | Rare | 3 | +1 tower in stock, and towers fire 30% faster | |
| Live Wire | Epic | 3 | 15% chance per rank that a hit arcs to 3 more bots; each one it reaches shorts out (stops, flickering blue) for a moment | A copper coil, a blue glow, and a thin arc up the barrel now and then |
| Powder Keg | Epic | 3 | Bots burst when they fall, and bursts can chain | |
| Whirling Blades | Epic | 4 | Blades circle you and cut bots that come close. +1 blade per rank | |
| Encore | Epic | 3 | The piano sends out a damaging shockwave every few seconds | |
| Grand Slam | Epic | 3 | Every 8th, 6th or 4th swing is a full 360° spin: it hits every bot within 1.2x reach for 1.4x and knocks them away | A gold knob |
| Executioner | Epic | 2 | Bots under 12% or 24% health fall on the next hit (not bosses) | |
| Second Wind | Epic | 1 | Once a wave, if Slice drops below 25 health, a blast clears the stage and heals 20 | |

When both parents of an evolution are at max rank, a gold evolution card shows up almost every time:

| Evolution | Parents | Effect | Mark |
|---|---|---|---|
| Thunder Bat | Live Wire + Grand Slam | Every hit arcs, bots short out longer, and every spin calls lightning down on 5 bots | A storm-blue barrel with a bright crackling glow |
| Phoenix Bat | Pine Tar + Powder Keg | Burning bots explode in fire, and explosions set bots on fire | Bigger, redder flames |
| Grand Finale | Encore + Standing Ovation | Encore fires twice as often, reaches twice as far, and heals | |
| Blizzard Bat | Ice Bat + Wide Arc | Every swing blows frost through its arc out to 1.4x reach, slowing every bot in it; finishers freeze them | A frosted white barrel with icicles |

The marks are built in code on the stand-in bat (`UpgradeLook.BatMarks`) and follow J's art rules: glow shells, flame
tongues and rings, no spark or ember particles. Boons that don't change the bat (Slice's, the piano's, the
orbit blades, towers and coins) show in the world instead.

## Surprises

- **Elites.** From wave 3, some Yo Bots spawn with a trait and a spinning gem over their heads: Sprinter, Bruiser, Jump-Jet, Splitter, Charger, Blinker, Swarmer or Juggernaut Yo Bot. They're tougher, drop four times the bolts, and occasionally drop a chest. A Splitter comes apart into two Drones.
- **Golden Yo Bot.** On wave 4 and every 4th wave after, a gold Yo Bot spends 22 seconds running from you. Catch it for a treasure chest and 3 coins. A chest opens a card pick that favors rare and epic boons.
- **Alpha bosses.** On every 10th wave, the bot that debuts that wave arrives as an Alpha (the first Spider Bot on wave 30). It has 4x health, a red gem, and a boss bar at the top of the screen, and it calls in Yo Bots around itself. It drops a chest and 6 coins.
- **Wave events.** From wave 6, a wave has a 40% chance to roll an event, never on boss or build waves. The events are Stampede, Jump-Jet Wave, Feast, Golden Hour (double bolts), Elite Surge (traited Yo Bots everywhere) and Blood Moon (tougher bots, more bolts). The current event shows at the top left.
- **Streaks.** Kills less than 2.4 seconds apart build a streak. Each kill in the streak adds 1% more bolts, up to +50%, and a long streak also makes you swing a little faster.

## The bat

The bat is pure melee: you walk up to bots and hit them. `Assets/Scripts/BatEquipment.cs` does all of it, and every number is in `Tuning.cs`.

- **The combo.** Three swings: right to left, back the other way, then a big finisher. Each one hits every bot in an arc in front of you (140°, the finisher 170°) out to the bat's reach, 3 m at size 1 and growing with you. The first two hit for 1x and knock bots back a step, so the next swing still reaches them; the finisher hits for 1.8x and sends them flying. A click during a swing goes straight into the next one. Stop for more than about half a second and the combo starts again at swing 1.
- **Ground smash.** When the bot you are swinging at is small (under 0.75 of your size), or most bots in the arc are, you swing down at the ground instead: a small area hit just in front of you for 1.3x. Early on that means Yo Bots. As you grow, Plated Yo Bots become small too.
- **Home run.** Hold the button to wind up (a bar fills under the middle of the screen, and a ring flashes when it's full). Let go past halfway and you hit one bot, the locked one or the nearest in front, for up to 3x. It flies off low and far, and every bot it flies into is hit and shoved along.
- **Contact.** Every hit has a crack, a blink of hit-stop and a small jolt of the camera. Big bots barely move when knocked back; bosses hardly at all.
- **Facing.** Without a lock you turn to the nearest bot in the arc as the bat comes round. Walking is a little slower while a swing is out, and slower still while you wind up.
- **Auto swing.** From wave 12 the bat swings by itself whenever a bot is in reach.

The model is a stand-in until Wallit's: two capsules on the right hand, posed in code, and the old recoil clip on the upper body for each swing. The boons and the shop dress it (see the Mark column above).

## Growth and damage

You grow every wave. Size is `1 + progress × 0.11`, where progress is `wave - 1` plus a little for each kill this wave. Growth also speeds up movement and pulls the camera back.

Base damage is `floor(1 + (wave - 1) × 0.5)`, plus 6 per Corked Core rank, plus a tiny bonus for kills this wave. Boons multiply it from there.

| Wave | 1 | 5 | 10 | 20 | 30 | 40 | 70 | 100 |
|---|---|---|---|---|---|---|---|---|
| Base damage | 1 | 3 | 5 | 10 | 15 | 20 | 35 | 50 |

The **Aluminum! Not wood!** upgrade swaps the wooden bat for a dented aluminum one: the crack rings higher and louder, knockback is 30% harder, and the third hit of the combo goes off in a small blast around the bot it hits. The blast radius is 2.4 m at rank 1, 3.6 m at rank 2 and 4.8 m at rank 3 (times the square root of your size). Splash damage is 65% of a base hit. The home run doesn't blast.

## Bots

`BotRoster.cs` (`BotKind`, in this order: kill counts, the wave list and the coin tally are packed by it). Yo Bots are the small tiers: an open laptop with a cyan HUD, claw arms and four jointed legs (jump-jets under the chassis). Spider Bots are the large ones: a navy glass cockpit pod with a pilot inside and six blade-tipped legs with cyan light strips. Each kind debuts as a pair, one of them the wave's Alpha with the boss bar.

| Debut wave | Bot | Authored health | Bolts | Look and blurb |
|---|---|---|---|---|
| 1 | Yo Bot | 3 | 1 | Gunmetal laptop with a cyan HUD, each with its own paint job. Small, loud, and walking straight at the piano |
| 10 | Plated Yo Bot | 60 | 6 | Steel with shoulder plates. Your height, bolted shut, and slow to come apart |
| 20 | Mega Yo Bot | 270 | 10 | Red, horned. As tall as you, and it rushes the piano in bursts |
| 30 | Spider Bot | 480 | 19 | Navy glass pod with a pilot, six blade legs. Twice the size of anything so far |
| 40 | Stalker Spider Bot | 660 | 22 | Silver. Quiet on its legs, with a long health bar |
| 50 | Runner Spider Bot | 840 | 25 | Orange, long legs. It closes the gap |
| 60 | Rammer Spider Bot | 990 | 28 | Sand, with a ram plate. A running wall |
| 70 | Shadow Spider Bot | 1260 | 31 | Near-black and violet. Dark, your height, and stubborn |
| 80 | Crusher Spider Bot | 1500 | 34 | Rust, with pincers. Huge, and it does not stop |
| 90 | Colossus Spider Bot | 1800 | 37 | Blue, with a ram plate. It stands over you |
| 100 | Titan Spider Bot | 2700 | 40 | Bronze with glowing gold trim and a crown of spikes. The last boss |

- **Attacks:** at the musician a Yo Bot swipes a claw and a Spider Bot stabs with a front leg. A Yo Bot's blow takes 1, an elite's 2, a boss's 3.
- **Jump jets:** bots hop on their jets now and then (a soft puff under their feet). The Jump-Jet Wave event makes them hop higher and more often.

- **Health scaling:** at spawn, health is multiplied by `Tuning.HealthScale(wave)`. That's about 1.2x at wave 10, 1.7x at 30, 3.3x at 50 and 12x at 100, so a build that snowballs still hits a wall eventually.
- **Size scaling:** spawn size is multiplied by `1.18 + (wave - 1) × 0.012`, plus another 2.5% per wave after wave 100. The bigger bots are meant to feel like bosses, about your height or bigger.
- **Arrival:** bots come one at a time, mixed together, after a short pause at the start of a wave. Yo Bots are about a third of a second apart, and bigger bots leave longer gaps.
- **How many at once:** about 16 early, rising to 120 in late waves.
- **Spawn ring:** Yo Bots start 30–48 m out. Each later wave pushes the ring out by about half a meter and widens it, up to 150 m. Each spawn picks the most open spot in the ring.
- **Movement:** bots walk in a straight line at the piano, with no pathfinding. Walls in the build ring stop them.

## Wave powers

These unlock for everyone at fixed waves. The wave numbers are in `Tuning.cs`.

| Wave | Power |
|---|---|
| 8 (`JumpWave`) | Landing a jump hurts bots whose kind debuted at least 8 waves earlier (`JumpLandGap`). Yo Bots count as debuting at wave 0 |
| 12 (`AutoWave`) | The bat swings on its own at bots in reach |
| 16 (`StompWave`) | With a small Yo Bot at your feet and you on the ground, you stomp in place |
| 25 (`ClimbWave`) | Bots whose kind debuted at least 5 waves earlier climb you instead of going for the piano. Jumping and stomping stop until you press **F** |

Each power is listed on the left once you reach its wave, and the newest one stays gold for a few seconds.

## Building

During a build window the mouse is free. Buy pieces with coins and click to place them in the ring around the piano, between the inner red ring (6.5 m) and the outer gold ring (12.5 m). **Q** and **E** rotate the piece, and **Tab** switches between pieces.

| Piece | Starting count | Price | What it does |
|---|---|---|---|
| Wall | 6 | 1 coin | Blocks bots. Stops 8 bots, then breaks |
| Spikes | 3 | 2 coins | Doesn't block. Kills 4 bots, then breaks |
| Tower | 1 | 3 coins | Blocks and shoots nearby bots. Kills 6, then falls after 8 bots reach it |

Stone Pile and Watch Kit raise the starting counts. Pieces darken as they wear out, then sink away.

## Shop

Coins and ranks are saved in PlayerPrefs (`bt_coins`, `bt_best`, `bt_reached`, `bt_hens`, `bt_next`, `bt_ranks`), so they survive losing and restarting.

Coins come from:
- each cleared wave: `1 + wave / 10`
- each Alpha boss: 6
- each Golden Yo Bot: 3
- Bounty Hunter, as you kill
- Plated Yo Bots: one coin every 10–20 of them (the tally still saves as `bt_hens`)

There are eight upgrades with three ranks each. The ranks cost 2, 5 and 8 coins. The first wave of each run lists what you brought.

| Upgrade | Effect |
|---|---|
| Corked Core | +6 bat damage per rank. A cork plug shows in the end of the barrel |
| Aluminum! Not wood! | A metal bat: a louder crack, harder knockback, and a small blast on the combo finisher. Each rank widens the blast |
| Long Stride | +25% move speed per rank |
| Stone Pile | +5 starting walls and +8 stops per wall, per rank |
| Watch Kit | +2 starting towers per rank. Towers shoot farther and faster, and last longer |
| Four-Leaf Clover | +1 reroll per run per rank, and rare and epic boons show up more |
| Opening Act | Start each run holding one random boon per rank |
| Stage Hands | Slice starts with +25 health per rank |

Once you've reached wave 10, the menu can start your next run later. Each step of 10 waves costs 10 coins, and you can only buy starts at waves you've already reached. The coins are spent when you press Play. The run after that starts from wave 1 unless you buy another start.

The menu also shows a summary of your last run.

## The map

The game takes place in the **Moonlit Hollow**, an open meadow bowl centered on the piano. It's designed for a wave game where bots walk straight lines and never need pathfinding:

- **Stone plaza:** a ring of flagstones under the piano, with four lantern posts and strings of colored bulbs around it.
- **Level ring:** flat ground out to 14 m, which covers the build zone.
- **Meadow:** wide and gently rolling, out to 150 m, which is the full spawn range. Four dirt lanes run in from the edge. Grass clumps and wildflower drifts are the only things on it, and bots walk straight through them.
- **Rim:** from 145 m the ground rises steeply into a dense pine forest with mossy boulders, so the hollow reads as an enclosed bowl. An invisible wall at 205 m stops you from climbing out.

`Assets/Editor/BuildArena.cs` builds the map into `scene2`. It runs automatically when the scene opens without an `Arena` object, and **Tools → Arena → Rebuild Arena** rebuilds it. The ground has a mesh collider on the Ground layer. Scenery has no colliders and is merged into a few meshes per sector, saved in `Assets/Art/Arena/Generated/`.

## Art

The art direction is a moonlit blue night, warm lantern light, and soft magic.

**Bots.** Real models come from Hawkes Gen's robot path (`character3d` with `form: robot`: Yo Bot `quadarm`, Spider Bot `spider`): textured meshes under ~4k / ~8k triangles, one 1K texture, rigid parts, procedural walk/run/idle/attack/hit/death/jump clips. They land as `Assets/Resources/Models/Bots/YoBot.fbx` and `SpiderBot.fbx`, with prefabs `BotYo` and `BotSpider` (`Tools → Bots → Build Prefabs`). Until those land, `BotArt` builds low-poly stand-ins in code (vertex colors); `Tools/Blender/bt_bots.py` can export the same.
- **Parts:** Yo Bots are `Body`, `Head`, `ClawL`, `ClawR`, front `LegL`/`LegFL` and `LegR`/`LegFR`, and optional back `LegBL`/`LegBR`. Spider Bots are `Body`, `Head` and `LegL1`–`LegL4`, `LegR1`–`LegR4` (1 is the front pair; Gen's six legs fill 1–3 per side). `Enemy.BindParts` also accepts Gen template names (`arm_l`, `leg_fl`, `leg1`…).
- **Animation:** gameplay still drives the parts in code (`Enemy.AnimateModel`) so dozens of bots stay cheap; Gen's clips live on the FBX for the library and previews. Yo Bots trot (four legs when present), swing claws, tuck for a jump-jet hop and swipe. Spider Bots walk on alternating groups, fold legs in a hop, and stab with a front leg.
- **Tints:** each Yo Bot gets one of eight paint jobs, elites lean toward their gem color, and the Golden Yo Bot is solid gold. Authored textures keep their albedo; stand-ins use the knoll vertex-color shader.
- **Hits and status:** a hit flashes the bot white. Burning, frozen, slowed and golden bots glow with a tint instead of spraying particles.
- **Hit capsule:** fitted to each model's body, head and legs (not the claws).

**Props.** `Chest.fbx`, `Quill.fbx` and `Lantern.fbx` are in `Assets/Resources/Models/`, loaded through `ArtLibrary.Make`. Bolt pickups are hex bolts built in code (brass for small drops, cyan for big ones) until a `Models/Bolt.fbx` exists. Bolts and the orbit blades (`Quill.fbx`) are drawn unlit, so they glow. The old bird models and `Feather.fbx` are still in `Resources/Models/` but nothing loads them.

**Map scenery.** Pines, a broadleaf tree, rocks, a boulder, a bush, grass and flowers are in `Assets/Art/Arena/`.

**Where the art comes from.** All the models come from scripts in `Tools/Blender/`:
- `bt_bots.py` makes the bot stand-ins (`export_all()` for all 11).
- `bt_birds.py` made the old birds, and still makes the lantern.
- `bt_arena.py` makes the map scenery.
- `BT_Art.blend` holds the working scenes and the menu key-art scene.

To rebuild a model, run the script in Blender and call `export("YoBot")` for a bot or `export_arena()` for the scenery. `Assets/Editor/ModelImportRules.cs` imports these models with smooth normals (a 60° crease angle), readable meshes and no imported materials.

**Atmosphere.** `NightAtmosphere` builds the night at runtime:
- cool moonlight, deep blue fog and trilight ambient light
- flickering warm lantern lights
- strings of bulbs
- slow ground mist that follows the player
- music notes rising from the piano

**Camera finish.** `NightGrade` on the main camera adds bloom, cool shadows with warm highlights, contrast, saturation and a vignette. Its fields can be tuned live in the inspector. Its shaders (`BTPost.shader`, `BTParticle.shader`) are in `Assets/Resources/Shaders/`, so WebGL builds include them.

**Effects.** `Fx` runs one pooled particle system each for flash, smoke, ring, note, dust and debris, with textures drawn in code. An old feather pool is still there and unused. The effects are deliberately calm. A hit is a soft flash, a destroyed bot gives a soft blue flash, a puff of dark smoke and a few chunky plates in its paint, a jump-jet hop is a small puff, and an explosion is a flash, smoke and a ground ring. There's no camera shake, and no spark, glitter or ember spray.

**Shader.** Bots, scenery and the ground all use `Knoll/VertexColorLit`. On top of plain vertex color it adds:

- meadow color patches on grass greens (`_Meadow`)
- a moonlit rim on upright surfaces (`_RimColor`, `_RimStrength`): strong on bots, nearly off on scenery
- the hit flash and status tint (`_Flash`)
- a warm window-glow band that bots turn off (`_GlowBand`)
- GPU instancing

`KnollLit.mat` is the double-sided world material.

**UI.** `UiSkin` draws rounded, nine-sliced panels and pill-shaped bars in code, used by the menu, the boon cards and every bar. Boon cards rise in, and rare and epic cards get a pulsing halo. The menu backdrop, `Assets/Resources/UI/MenuArt.png`, is key art rendered in Blender, with a slow drift. It still shows the birds as they looked before they were smoothed.

## Animation

The player is the Warrok model (`Assets/Art/Warrok W Kurniawan.fbx`) on the object tagged `Player`. Its visible child is tagged `playerchild`. The stand-in bat follows the right hand and is posed in code each frame (`BatEquipment.PoseBat`). The scene still has the old `BowEquipment` component on the player; it only adds `BatEquipment` and switches itself off.

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

Each swing plays the old shoot recoil on a second layer, Upper Body (masked by `Assets/hero/UpperBody.mask`), as a stand-in. The `Fire` trigger starts it, and its speed follows the `FireSpeed` float (1.4x swing speed, raised by Quick Hands and streaks).

## Run log and autopilot

Each wave writes one line to `runlog.csv` in `Application.persistentDataPath`, and a `[RunLog]` line to the console. Each line records:
- the outcome and how long the wave took
- the musician's health before and after
- kills by bot, swings and bots hit, and damage dealt
- level, best streak and coins
- the wave event, boons picked that wave, and the full build

Autopilot waves are tagged `-auto`.

In the editor, **F9** turns on `DevAutopilot`. It plays as a perfect batter who is always at the bot nearest the piano without walking there (80% of swings connect, the combo's third swing is the finisher), picks random cards, skips breaks and runs at 3x speed. It sandboxes `MetaProgress`, so it never writes coins or ranks to your real save. Use it to find roughly where builds fall apart, then play that stretch yourself.

## Project layout

| Path | What's there |
|---|---|
| `Assets/Scripts/` | Runtime game code |
| `Assets/Editor/` | Editor-only setup: map builder, animator wiring, import rules, WebGL deploy |
| `Assets/Resources/Models/` | Bots and props, loaded at runtime |
| `Assets/Resources/Shaders/` | Post-process and particle shaders |
| `Assets/Resources/Sfx/` | Sounds: `shot`, `hit`, `death`, `stomp`, `launch`, `coin`, `shake`, and the bat's `batswing`, `batcrack`, `batsmash`, `homerun` |
| `Assets/Resources/UI/` | Menu key art |
| `Assets/Art/Arena/` | Map scenery models, and generated map meshes in `Generated/` |
| `Assets/Art/Knoll/` | The world shader, sky shader and materials |
| `Tools/Blender/` | Blender scripts and the `.blend` for all generated art |

Layers: Default, TransparentFX, Ignore Raycast, Ground, Water, UI, Enemy.
