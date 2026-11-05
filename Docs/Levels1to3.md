# The Place Department: level design for Chapters 1 to 3

Status: design, written and committed **before** any scene work (J's brief for "chapters 1-3 feel like three different, believable places"). Companions: Docs/CampaignBible.md (story beats and map sheets), Docs/CampaignDesign.md (4.1 to 4.3, palettes), Docs/ModelKit.md (the pieces; everything here is built from them). No GPU generation: every mesh, texture and sound is authored in code.

## 0. What is wrong today

- Chapter 1 (`LevelYard`, scene `Level_1_Yard`) is built from `LevelKit.Block` primitives: grey cylinders for stones and trunks, cubes for doors and walls, a 134 x 126 m box fence round a flat plane. It reads as a test room.
- Chapters 2 and 3 (`EdgeMap`, `FloorMap`, `ChapterRun.BuildGate/BuildFloor`) are `OwnMap.Piece` boxes and cylinders in vertex colour on the Arcade's grass bowl, with a flat bowl of Arcade scatter (trees, grass) still showing inside 150 m.
- The Model Kit (82 authored pieces) exists but no level uses it.

## 1. Rules for all three (held by the scene validator, section 6)

1. No renderer may use a built-in primitive mesh (Cube, Sphere, Capsule, Cylinder, Plane, Quad). The only exceptions are listed in `SceneValidation.Accents`: the flat ring that marks the objective and a sky or UI quad if one is ever needed (none today).
2. Ground is a **heightfield mesh** (Chapter 1) or a **skin baked from the live Arcade ground** (Chapters 2 and 3, see 4.2), tiled with a kit texture, never a stretched plane.
3. Buildings are real: facade bays (`st_house_wall`, `st_door_wall`, `st_shopfront`), gable or flat roofs, chimneys. No flat wall longer than 20 m without a break (a door, an alley, a step, a lamp, a corner).
4. Skyline: every chapter has at least one **landmark** that can be seen from the start and from the objective, and a **far skyline** (building roofs, stacks, pylons, hills) beyond the playable area.
5. No room of identical props: placement uses a seeded jitter in yaw, scale (0.85 to 1.2, never one piece scaled into something else) and a pick from at least three pieces for each kind of clutter.
6. Colliders are simple: invisible proxies (one `BoxCollider` per building or run of fence, one per stone) on their own objects. Kit pieces carry none. In Chapters 2 and 3 there are none at all (the playfield within 150 m of the circle stays free of anything solid, AGENTS.md) and every prop stands outside the walkable lane.
7. Lighting is real: a coloured sun, tri-light ambient, exponential fog, a gradient skybox (`Knoll/ChapterSky`), and **placed point lights** at every lamp, window and machine (shadows off; at most 14 in a level, range 9 to 26 m). The kit's lamp glass is emissive so lit lamps read from afar. Realtime lights only (WebGL build; no bake step in the build server).
8. Audio hooks stay: each chapter keeps its `MusicDirector` track and its `OwnMap` music bed; set pieces keep their sounds.

## 2. Chapter 1: the Sorting Yard (Return to Sender). Scene `Level_1_Yard`

Mood: cold night. Moon blue, gold ring, red ink. Dead-letter post-office yard behind the Department's Return Hall. Time of day: past midnight, moon high and a little behind. Lighting: moon directional `(0.62, 0.72, 1.0)` at 0.95 intensity from `(48, -32)`, trilight ambient sky `#4553 8C` / equator `#29334D` / ground `#12141C`, fog `#0D1426` exponential-squared 0.009 (the far skyline stays a dark silhouette), skybox from `#07102A` zenith to `#26365A` horizon with a pale moon disc, camera `Skybox`. Lights: 7 lamps (warm `#FFDB8C`, range 15) along the chalk and at the dock, a gold glow at the Ring (`#F2CC66`, range 18), a window glow on the Dead Letter Office (`#FFC866`, range 30), the clock face (emissive), and the Return Slot's amber strip.

### 2.1 Top-down sketch (north up, one `.` is about 4 m; coordinates are the live ones in `LevelYard`)

```
 z=+95                        [CLOCK TOWER]                      far skyline: roofs, stacks
                    [  DEAD LETTER OFFICE  3 storeys  ]
 z=+65  ~ ~ ~ fence ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~
        [ west  ]                                    gap=alley   [ north-east row ]
        [ houses]                         . . . [RETURN SLOT] (30,49)  exit, shutter
 z=+50   ^ dead trees        .                          '
         ^ spoil bank (+2.5 m) .  Mailbox (-41,14)       '
 z=+12  [FLAG](-38,12) <- n3 (-34,12)       [DROP BOX] (34,10) sorting dock:
                 n2 (-26,4.5)                  trestles, sacks, post boxes
 z=  0          n1 (-15,-3)  .   lamp          crew sleeps at (39,10)
                   .   .   . ' .  .  .  .  .  .  chalk east way
 z=-10         (  o o o o o  )  RING (0,-10), Slice (0,-9), nine standing stones r=10.5
                     |  gap faces the doors
 z=-30        chalk slabs, lamp pairs, signpost     |
 z=-48                 START (0,-48) facing north
 z=-59   [door][wall][door][wall][door][wall][door][wall][door]   RETURN HALL, five shut doors (RETURNED)
 z=-65   roofs, chimneys, wings of two storeys either side
```

### 2.2 Route (visible from the start)

Start in front of the Return Hall (five shut doors, ink on the step). Straight ahead the chalk slabs lead to the gold-lit Ring and, above it in the north, the **clock tower** of the Dead Letter Office with its lit face. West of the Ring a second chalk line bends up the spoil bank to the lost Mailbox; east a third runs to the sorting dock where the drop box stands. The exit, the **Return Slot** (a stone frame with a rusty shutter and an amber strip), is the north-east corner, seen from the dock as the one lit doorway.

Order: Ring (talk) -> west trail (three notices, the Mailbox Flag) -> the crew wakes at the drop (fight) -> Return Slot.

### 2.3 Landmarks that guide the eye

1. **Clock tower** (north, 95 m): a glowing face, readable from the start and from every corner of the yard.
2. **The nine standing stones with the gold ring** (centre): the first place, a gold lamp in the dark.
3. **The fallen post box with the red flag** in the west hollow.
4. **The Return Slot's amber strip** (north-east) once the crew falls: the objective turns into a light.

### 2.4 Three reasons to explore off the route

- **A talk:** the dead-letter bench and bins at the Return Hall's west wing: walking past it fires one Wallit bark ("Dead letters. Nobody's coming for these.", `bench` in the level timeline), besides Slice's Ring talk and the notices' reactions.
- **A secret:** on the spoil bank a ring of seven stumps (`yd_stump`) circles a carved standing stone, lit blue (a look-out for the player who climbs the hill). The **Back Room** (BackRooms.cs row 0) stays where the Journal clue puts it, 172 m out, now built from kit pieces (mail-slot walls, desk, pigeonholes, lamp).
- **A pickup:** the Mailbox Flag itself (the lost thing, red pennant, lit by a gold light) and the notices; the dock's trestles, sacks, post boxes and crates are the place the crew sleeps among.

### 2.5 Encounters

One deliberate encounter, the Clearing Crew at the drop (five Yo Bots, asleep on the sorting dock, wake when the flag is lifted or Wallit nears 16 m). Nothing awake before. The stamp floor stays the hazard (telegraph, slam, shove) with the Ring as the safe place.

### 2.6 Set dressing themes

Post-office night: pillar boxes, sacks of dead letters, sorting trestles with pigeonholes, crates, pitched slate roofs, chimneys, signposts, dead pines and stumps, chalk slabs, wooden picket fences. Everything cold and blue-grey except the lamp gold and the red ink.

### 2.7 What makes it unlike 2 and 3

Open **night yard with a ring of stone** and a **soft skyline of roofs under a moon**. Organic ground (spoil bank, hollow), dead trees, blue-grey stone and slate, round plan, gold light. No machinery, no sun.

## 3. Chapter 2: the Gate Hall (No Fixed Address). Map `hollow-edge` in the game scene

Mood: chalk and red. A cold grey-blue dusk, a long **chalk road** with paired lamps, a plaza, and the **gate with no handle** in the Department's wall. Time of day: last light, low sun from behind the gate (bearing 200 degrees, 26 degrees up). Lighting: sun `#F2AE85` 0.75, ambient equator `#453845`, fog `#382B36` 0.0085, a skybox from `#1A1221` to a `#6B454D` horizon with a large orange disc low over the wall (the existing sky), camera `Skybox`. Lights: the road's paired chalk lamps (`#FFC766`, range 22, one light per pair, five), the gate's amber wash (range 34), the press's red warning lamp (existing), the watchtower's searchlight sweep (existing), the ticket booths' windows (`#FFD08A`, range 10).

### 3.1 Top-down sketch (circle at the origin, bearing 150 degrees runs to the south-east)

```
                               far: wall running both ways, wall towers, hill roofs
        [booth]  [turnstile x3 in a row]  GATE ARCH + pillars  [booth]      <- 70 m, bearing 150
  watchtower+search                 | no handle, three red notice bars over the gap
        (bearing 188, 64 m)   plaza | open paved fan, bollards, barrier rails, INVALID boards
                              .     |
                         [stamp press, 46 m]   red mark, one press, one bot
            lamp pair .     .  |  .   . lamp pair      <- chalk road, five pairs 18 to 64 m
        blank signpost (loop)  chalk  .    .
                                |
                         (  CIRCLE, Slice's red radius ring, 24 m )
```

### 3.2 Route

Chalk road from the circle (bearing 150) with five paired lamps up to a paved plaza and the gate. The press at 46 m straddles the road; a signpost loop (blank signpost) curves off to the east and rejoins the road at 56 m. The way back is the same chalk.

### 3.3 Landmarks

1. **The gate arch** (`gt_gate_arch` flanked by `gt_gate_pillar`s with three red INVALID bars), 70 m out, lit amber from the plaza.
2. **The big stamp press** (`gt_stamp_press_large`'s frame, a moving platen) at 46 m on the road, red marks printed under it.
3. **The Department wall** running both ways from the gate, broken by wall towers, with a ticket-booth row, and the **watchtower** with its sweeping beam.
4. **Slice's red radius** painted in the apron round the circle.

### 3.4 Three reasons to explore

- **A talk:** the **ticket booth** east of the plaza has a clerk's lamp lit: Wallit can read the booth's window board (the blank signpost note, Journal goal) and Slice calls the press.
- **A secret:** the **blank signpost loop**; on its far side, behind the wall's tower, a stone with a torn half-postmark (the torn postmark stone from the bible's prop list) that the Back Room clue points to.
- **A pickup:** the **Receipt** at the opening, the lost thing near the loop's signpost (existing `ChapterRun` journal tag), bolts on the plaza from the Yo Bots dropped while they arrive.

### 3.5 Encounters

At most three (ChapterPacing), unchanged: the Second Notice (Plated Yo Bot) wakes by proximity on the road before the press, one placed group on the plaza, then three held waves at the circle after the gate scene and the Plated Alpha. The press is the hazard and the puzzle.

### 3.6 Set dressing

Chalk plaster and blue-grey steel: ticket booths, turnstiles, barrier rails with rope hooks, bollards, INVALID boards on legs, paired chalk lamps, gate pillars with ball finials, flags of notice. Paving (the ground skin) is pale chalk-grey concrete in 4 m bays with cracks and a painted red radius ring.

### 3.7 What makes it unlike 1 and 3

**Symmetry and formality**: a straight road, paired lamps, a gate, a wall: civic, pale, bureaucratic, dusk-orange behind it. Open plaza, hard-edged stone and steel, repeating on purpose (the Department), no trees at all.

## 4. Chapter 3: the Clearing Floor (The Clearing Floor). Map `clearing-floor` in the game scene

Mood: amber factory, rust and steel. A **sunken yard** whose walls climb on every side under a dull orange smoggy sky, smokestacks on the rim. Time of day: none (a closed sky of smog, the sun a dim orange disc low in the east). Lighting: sun `#FF9E52` 0.55 from `(70, 100)`, ambient equator `#4D2B1A`, fog `#2B170F` 0.0075 (heavy and warm), skybox `#120A0D` to `#99491F` horizon, camera `Skybox`. Lights: a caged lamp at every stop plate (range 30 amber, existing), cage lamps along the girders (amber, range 12, four), the red-lamp chute at the lever (red, range 26), the crushers' red warnings (existing); the lever dims every lamp to 30 % (existing set piece).

### 4.1 Top-down sketch (bearing 200 to 210 degrees, to the south-south-west; three levels)

```
  rim (+6 m):  stacks, girders overhead     [ catwalk along the lip, hooks, chassis racks ]
                                    broken gate (36 m)  STOP 1: gate posts, hook rails
                                          |
  mid (0 m): belt W [hat clock bell ->]   route   [<- cleared things] belt E        STOP 2: the belt (70 m)
                    crusher 38 m     crusher 62 m      crusher 86 m   (red marks, slam in turn)
                                          |
  low (-0 to -1.5): pit with pipes, barrels       STOP 3: lever stand (105 m), RED LAMP CHUTE
```

### 4.2 Route

A lit path of steel plates from the circle to three stops, each a lit plate with a caged lamp: the broken gate (36 m), the belt (70 m), the lever (105 m). The route is flat and clear (nothing solid within 150 m). **Elevation** comes from kit geometry beside it, not collisions: catwalks on the rim (y +3), hook rails and gantries overhead at y +3.4 to +3.7 from the route, racks along the lip, belts at waist height, girders crossing high. The rim's terraces (the terrain skirt) climb 30 m beyond 205 m.

### 4.3 Landmarks

1. **The red-lamp chute** at the lever: a red lens high on a stack, seen from the circle.
2. **Smokestacks** (7 on the rim, glowing mouths).
3. **Two belts of cleared things** (hat, clock, bell) sliding past either side of the route.
4. **Crushers** slamming on red marks in a rhythm.

### 4.4 Three reasons to explore

- **A talk:** the **Half-Built** hung by one hook off the route at the belt stop; the three-line exchange is its mini-boss opening (existing).
- **A secret:** a **maintenance catwalk** behind the chassis racks east of stop 1 leads to a cage lamp over a stamped plate (the Journal's find, existing position), and the Back Room's clue.
- **A pickup:** the **cleared thing** on the belt (the favour for Brim), bolts from each bot and the cleared clock that keeps ticking after the lever.

### 4.5 Encounters

At most three (ChapterPacing): the Half-Built's hall at stop 1, a Reissue Yo Bot group at the belt, a last batch at the lever; then the Foreman at the circle. Crushers kill bots that are lured under them.

### 4.6 Set dressing

Riveted steel and rust: conveyors, crushers, hook rails with hung chassis, chassis racks, girders, pipe runs, barrels, catwalks, cage lamps. The ground skin is oily dark concrete with amber light pools; no soft curves except barrels and pipes.

### 4.7 What makes it unlike 1 and 2

**Vertical and mechanical**: overhead rails, moving belts and slamming crushers, amber light and smog, a sunken bowl with the stacks on the rim. All hard steel and repeating machines (on purpose, the Clearing Crew is a factory) against Chapter 1's organic night and Chapter 2's pale civic symmetry.

## 5. Side by side

| | Ch1 Sorting Yard | Ch2 Gate Hall | Ch3 Clearing Floor |
|---|---|---|---|
| Time | night, moon | last dusk light | smog, no time |
| Palette | blue-grey, gold, red ink | chalk white, steel blue, red | rust, amber, steel |
| Ground | dark gravel, heightfield | pale paving, flat | oily concrete, flat |
| Shape | round plan, organic | straight axis, symmetric | sunken bowl, layered |
| Skyline | roofs + clock tower | wall, gate, watchtower | smokestacks, rim terraces |
| Hero props | stones, post boxes, trees | arch, turnstiles, press | belts, crushers, hooks |
| Trees | dead pines and stumps | none | none |
| Light | moon, gold lamps | low orange sun, paired lamps | caged amber lamps, red chute |

## 6. How it is checked

- `SceneValidation` (runtime class, `Assets/Scripts/SceneValidation.cs`) scans every active renderer and reports: renderers, **built-in primitive meshes** (by mesh name and by being a 12-triangle box), triangles, **estimated draw calls** (instanced batches per mesh and material), lights, colliders, and failures. Documented small accents are an allow-list by object name.
- `Assets/Tests/PlayMode/LevelValidationTests.cs` loads Chapter 1's scene, and Chapters 2 and 3's maps (additively over the game scene), waits for them to fill, runs `SceneValidation.Scan` and fails on any primitive renderer or on budgets (visible triangles under 150,000, estimated draw calls under 400) from Docs/ModelKit.md.
- Playtest scenario `levels-1-3` plays each chapter's regions (a named setup shortcut puts a photo camera at each region's vantage point), photographs them (event `photo` with the region name and the scan), logs the validation (`scene-scan`, metrics `levelTriangles1..3`, `levelDrawCalls1..3`, `levelPrimitives1..3`), the WebGL load time and memory per chapter. The chapters' objectives, set pieces, dialogues and encounters are played end to end by `campaign-beats` (Chapter 1), `chapter2` and `chapter3`, which beta also runs; the scenery change touches none of their positions (gate 70 m, stops 36/70/105 m, lever, flag and drop positions stay exactly where they were).
