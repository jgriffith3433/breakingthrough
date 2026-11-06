# The Place Department: level design for Chapters 4 to 6, their boss arenas and the hub office

Status: design, written and committed **before** any scene work (J's brief: "chapters 4-6, the boss arenas and the hub office become distinct, dressed, believable places built entirely from the Model Kit"). Companions: Docs/CampaignBible.md (map design sheets, set pieces), Docs/CampaignDesign.md (4.4 to 4.7), Docs/Levels1to3.md (the rules this continues), Docs/ModelKit.md (the pieces). No GPU generation: every mesh, texture, light and sound is authored in code.

## 0. What is wrong today

- `WalkMap`, `ReadingMap`, `FiledMap` (OwnMapLate.cs) are `OwnMap.Piece` boxes, cylinders and cones in vertex colour: a causeway of flat slabs, a "hall" of 48 plain wall panels, rows of 0.2 m thick form boards, a dais that is a stack of three flat discs with twelve square pillars (`OwnMap.Arena`). `SceneValidation` counts every one of them as a built-in primitive.
- The hub (`ChapterHub`, plus `OfficeMap`) is a room of `GameObject.CreatePrimitive(Cube)` blocks: a floor slab, three wall slabs, doors that are four boxes, a desk that is four boxes, lodgers who are three to five boxes, a board that is two posts and some quads. It is the screen players see most and it looks like a prototype.
- Chapters 4, 5 and 6 and the three boss arenas share one language (flat tinted blocks), so they do not look different from each other.

## 1. Rules for all of it (held by the scene validator, section 9)

Everything in Docs/Levels1to3.md section 1 holds. In addition:

1. **No visible primitive**: no cube, plane, capsule, cylinder, sphere, quad, and no `OwnMap.Piece` (the old vertex-colour boxes). Everything visible is a kit piece (`KitCatalog.Make`), a baked ground or terrain mesh with its own name, or a TextMesh label. The old `OwnMap.Arena` (cylinders) is removed; arenas are kit pieces.
2. **Every new piece is authored**, never a scaled primitive, never a recoloured copy (the kit validator rejects both): 61 pieces added in `KitCatalogLate.cs`, listed in section 8 and in Docs/ModelKit.md section 10.
3. **No old boxes left hidden inside a model.** Nothing is built and then hidden: the primitive code is deleted (`ChapterHub.Block`, `Flat`, `Part`, the `OwnMap.Piece` calls in the three maps).
4. **No boxed-in corridors of flat walls.** The hub has a bay rhythm (window bays, door bays, board bay, mail-slot bay, bookshelf bay) with pilasters, a cornice and wainscot on every wall, and a window view; the three outdoor places have no walls near the route at all; the Reading Room's wall is a ring of bays of a different scale with arched windows, pilasters and form racks.
5. **Variety**: seeded jitter (yaw, small offsets, never above 1.2 times or below 0.85 times scale), and at least three pieces for each kind of clutter. Chapters 4, 5 and 6 each use their own palette (`walk`, `reading`, `filed`), their own ground texture, their own sky and lighting, their own silhouette (section 7).
6. **Colliders**: none. The playfield within 150 m of the circle stays free of anything solid; the hub clamps Wallit in code (`ChapterHub.Walk`) as it always has. Nothing here adds a collider; the scene scan reports colliders and expects 0 in these scenes.
7. **Each scene stays separate** (decision m_098402ce499b): Map_Walk, Map_Reading, Map_Filed and Map_Office are still their own scenes loaded additively by `ChapterMap`; the hub's room is built by `OfficeMap` in Map_Office, not by the campaign scene's `ChapterHub` (which keeps only the people, the interactables, the UI and the door leaves).
8. **Nothing is moved that gameplay uses**: stops (30/62/95 m on bearing 330), the Servers' Dais (bearing 150, 78 m), the Reading Room's door (180), desk (6, 3), Gallery (bearing 0, 80 m), the filed path (180), the Threshold (bearing 20, 80 m), the hub's board (-12.2, 3), desk (11.5, 3), doors (z = 15, x every 4.6 m), lodger spots, errand spots, the ring (radius 4), `RoomHalfWidth`, `RoomBack`, `RoomFront`. Boss mechanics (`Server.Offering`, `PageRead`, `WaveDirector.SpawnForChapter` reading `BossSpot`, the stamp marks) are unchanged.

## 2. The hub: the Office (vertical slice, built first)

Mood: warm lamp-light at the end of a long night; the room is **where the file is kept**. Palette `#2A2118 #7A5C2E #E8D9B0 #F2CC66`, softer than any chapter. It is the only place with a roof-line and a window view, and it gets brighter, warmer and fuller as the file is filed.

### 2.1 Top-down sketch (north up; hub coordinates, one `.` is about 1 m; the camera looks from the south, open front)

```
 z=+15  [door1][door2][door3][door4][door5][door6]    BACK WALL: mail-slot bay, six themed doors, pilasters
        x=-11.5 -6.9  -2.3   2.3   6.9  11.5
 z=+14  (brass door number plate over the middle, once six are filed)
        pigeonholes   hanging lamp      hanging lamp    cabinets
 z=+11  [bookshelf]        . coat stand   BRIM drifts      [plant]       [bookshelf]   (Brim's spot -9,10)
                       runner rug runs from ring to the doors
 z= +8  waiting bench                      errand spots (9,10) (6,7) (-6,11.5)           pneumatic tube in the NE corner
 z= +3  [BOARD] (-12.2, 3)         ( ring r = 4 )                  [DESK] (11.5, 3) TICK sits, desk lamp, chair
 z= +1  window  ---- window ----   ( SLICE at 0,0 )          ---- window ---- window   WINDOWS in both side walls, each
 z= -3  bay        bay                brass ring inlay            bay     bay          with the skyline view outside
 z= -5  CHIME (-8.5,-5)   SPARE (8.5,-4.5)   favour/notice spots along both side walls
 z= -9  front of the room: open to the camera, a low rail and floor lamps mark the edge
        x=-14.2 ................................... x=+14.2
```

### 2.2 Route (visible from the start)

Wallit arrives by the ring (4.5, -6). The brass **ring inlay** with Slice is the centre; a **runner rug** runs from it straight to the six doors, lit by two hanging lamps; the row of doors is the far wall, each under a lamp. The board on the left wall and Tick's desk on the right wall are the two side goals. Window light falls across the floor from both sides.

### 2.3 Landmarks

1. **Six themed doors** on the back wall (section 2.4), each a frame piece and a leaf that swings (the leaf is its own piece, `ChapterHub` turns it). Open doors are ajar and warm; shut doors are closed and cold; the lamp over each is a lit lens (amber filed, green next, dark locked).
2. **The brass ring** with the Ring's warmth (it goes from lamp-blue to gold with the file).
3. **The chapter board**: a big board with six slip frames and a brass title plate on the left wall.
4. **The windows and what is outside them**: a skyline of the Department's roofs with lit windows and, far off, the Dead Letter Office's clock tower. The light through the glass goes from night blue (nothing filed) to dawn gold (all six filed).
5. **Tick's desk** with its lamp, chair and plate, and **the hanging lamps** (four, one over the ring, one over the desk, two over the rug).

### 2.4 The six doors (each fits its chapter)

| Door | Frame (piece) | Leaf (piece) | Reads as |
|---|---|---|---|
| 1 Return to Sender | `of_door_frame_1` post-office slot arch: brick surround, brass letter-slot, RETURN plate | `of_door_leaf_1` planked door with a mail flap | the Return Hall's shut doors |
| 2 No Fixed Address | `of_door_frame_2` civic arch: pale pilasters, a crossed INVALID bar across the lintel | `of_door_leaf_2` barred iron door with no handle | the gate with no handle |
| 3 The Clearing Floor | `of_door_frame_3` riveted steel hatch frame with a caged lamp | `of_door_leaf_3` riveted plate door with a hook rail on it | the factory floor |
| 4 The Process Servers | `of_door_frame_4` tall narrow iron frame with a folded-leg lamp head | `of_door_leaf_4` slatted long door, violet glass | the Long Walk's lamps |
| 5 Strike This Address | `of_door_frame_5` white paper-panelled frame, pinned forms | `of_door_leaf_5` blank form door, ruled lines | the Reading Room's white door |
| 6 Address Filed | `of_door_frame_6` pale round arch, gold keystone | `of_door_leaf_6` bare white door, gold number plate | the bare threshold |

### 2.5 Off-route rewards (all existing mechanics, now visible)

- **Talk**: four lodgers, each a modelled character with a name, standing at their `HubLife` spot. Tick the wall clock with legs (case, brass face, pendulum), Brim the bowler with a ribbon, Spare the half-built Yo Bot (open chassis, one claw, lid off), Chime the bell (bell, clapper, handle). Brim and Chime hover.
- **Pickups**: Chime's lost thing and the favour item (`of_lost_thing`, a lit brass tag), the loose notices (`of_notice_slip` lying on the floor with a red stamp).
- **Secret**: the pigeonholes in the north-east corner next to the pneumatic tube (flavour for the back rooms: the slot is labelled with the numbers of the back rooms found).
- **Pinned slips and wall lamps**: an errand finished pins a slip on the left wall; a conversation had lights a sconce on the right wall.

### 2.6 Encounters

None. The hub is the reward and the checkpoint.

### 2.7 Set dressing

Kit pieces: `of_wall_bay` and `of_wall_window` bays with wainscot and picture rail (left, right, back), `of_wainscot_door` and `of_mail_slot_wall` in the back wall's gaps, `of_pilaster`s, `of_floor` (a baked board floor, GridMesh, wood texture), `of_rug` and `rd_runner`, `of_desk`, `of_chair`, `of_desk_lamp`, `of_filing_cabinet` banks (grow with the file), `of_pigeonholes`, `of_coat_stand`, `rd_bookshelf` bays, `of_waiting_bench`, `of_plant`, `of_pneumatic_tube`, `of_hanging_lamp`, `of_wall_lamp`, `of_paper_stack`, `of_stamp_seal`. Seeded placement for the clutter, never the same twice in a row.

### 2.8 Lighting and ambience

Interior, warm: a dim blue directional "moon" through the windows (rotating toward gold with the file), tri-light ambient `#383C52 / #261D18 / #14110F`, light fog `#0D1220` 0.0012, a point light at each hanging lamp (`#FFD08A`, range 14), the desk lamp (range 7), a window glow on each side (`glow`, range 18, the colour of the hour), the doors' lamps (small, coloured by state). 12 point lights at most.

**The office changes** (`OfficeMap.Dress(cleared, pinned, heard)`, redresses without reloading): window light and skyline lamps (night to dawn), brass ring warmth, one more filing-cabinet bank per chapter, paper stacks by the desk, a stamp seal on each filed door's plaque, the door number plate when six are filed, pinned slips, wall sconces. `OfficeMap.Look` keeps the fingerprint the `hub-life` playtest reads.

### 2.9 What makes it unlike the chapters

Roofed and walled, wood and brass and paper, warm lamp pools, a view out; the others are open skies. It is small, crowded and lived in, while chapters 4 to 6 are bare.

## 3. Chapter 4: The Long Walk (The Process Servers). Map `long-walk`

Mood: **cold slate dusk over a pit of mist, a single lit road.** Palette `#0A0F1A #2B3550 #9FB1D6 #E5E1D3 #7A5CC7`. Time: the last blue hour, a pale low moon behind the arena side. Texture family: pale stone and iron (walk palette), violet glass lamps.

### 3.1 Top-down sketch (circle at the origin, causeway on bearing 330 to the north-west, dais on bearing 150)

```
                      pale spires (needles, banded towers, obelisks) standing up out of the mist, 250-400 m
                    ~ ~ ~ mist banks drift along the road ~ ~ ~
   LAMP 3 (95 m) [stilt pair]  <- the way turns round here; last slabs fallen into the pit past it (hazard)
        |   railings both sides, bollards and urns every few bays
   LAMP 2 (62 m) [stilt pair]    arrow sign, stone bench: a place to stop (talk, Slice's note)
        |
   LAMP 1 (30 m) [stilt pair]    the causeway starts: three-slab-wide, kerbed, 3 m of plaza round the circle
        |
   ( CIRCLE: plaza kerb ring, Slice, mooring bollards )
        |
     (far side, bearing 150)
   SERVERS' DAIS (78 m): round violet-inlaid dais, 12 folded-leg pillars, the PAPER STAND where The Server waits
```

### 3.2 Route

A pale slab causeway, three slabs wide, with a railing each side, leaves the circle's plaza on bearing 330 and runs past three lamp stops (a pair of lamp stilts each, the heads with three folded legs) to the last lamp, where it turns round. Everything beyond the railings is mist and far spires: a causeway with no cover. The Server's dais is behind the player at the start, seen as a violet glow through the mist on the far side of the circle.

### 3.3 Landmarks

1. **The lamp stilts**, violet glass heads, one pair per stop, each visible from the previous one.
2. **The pale spires** standing out of the mist at 250 to 400 m, in three different silhouettes (needle, banded tower, stepped obelisk).
3. **The fallen slabs** at the road's end: the causeway breaks off into the pit.
4. **The paper stand** on the dais, lit violet.

### 3.4 Off-route rewards

- **Talk**: the stone bench and arrow sign at the middle lamp (62 m, off to the side): the opening line "Mind the lamps" is placed there as a Slice note on the stand's tag; the bench is lit by the middle lamp.
- **Secret**: a mooring bollard row on a stub pier off the road at 80 m (bearing 300, the Journal's find) with a lit urn; the Back Room's clue.
- **Pickup**: the hidden find (Journal `Thrice-Refused Paper`, 70 m bearing 120) lies on the plaza by the dais side; bolts from each Server.

### 3.5 Encounters

Unchanged (ChapterPacing: Servers appear at the lamps, three at most; the Server at the dais is the boss).

### 3.6 Set dressing

`wk_causeway_slab` laid in rows with seeded yaw (0/180), a missing and a cracked slab here and there, `wk_railing` bays with `wk_mooring_bollard` and `wk_mist_urn` between, `wk_lamp_stilt` pairs, `wk_plaza_kerb` ring round the circle, `wk_arrow_sign`, `wk_stone_bench`, `wk_pylon` on the outer pier, `wk_spire_a/b/c` and `wk_mist_bank` (a soft, noise-displaced lump, pale, three variants of yaw) floating at the pit.

### 3.7 Lighting

Cold directional (`#BFD9FF`, 0.6, from the north-west), ambient `#33384A / #282B3A / #14161E`, fog `#4C5260` at 0.0125 (heavy), skybox from near-black `#060814` to a slate horizon `#4C5580`, a faint moon disc. Lamps: a violet point light at each stop (`#9D6BFF`, range 34), the stand's lamp (violet, range 30, burns up when the paper is out), a pale glow at the circle plaza. 6 point lights.

### 3.8 What makes it unlike 1 to 3

One long straight line of light through nothing: thin vertical stilts and spires, violet glass, mist; open sky and no solid ground in sight. Chapter 1 is a round organic night yard, 2 a formal civic plaza at dusk, 3 an amber sunken machine.

## 4. Chapter 4's boss arena: the Servers' Dais (bearing 150, 78 m)

Shape: a **round, low, tiered dais** (22 m radius), inlaid with a violet ring and eight radial seams, with **twelve slim pillars** crowned with folded legs (the lamp-head family) evenly round it and a **gap on the causeway side** (the entrance). The **paper stand** (post, table, paper sheet) stands on the far rim; The Server waits behind it.

- **Readable shape:** a clean circle with a bright violet inlay ring that contrasts with the dark mist floor; two steps up from the field.
- **Cover/hazards the boss mechanics use:** the paper stand is the boss's anchor (`Server.Offering`: the paper lifts, the stand's lamp burns violet, a ring flashes). The twelve pillars are the "lamps": Servers appear only at lamp pillars; the player may circle the dais.
- **Dramatic and unlike the route:** the route is cool, narrow, thin and open; the dais is wide, ringed, violet-lit and walled with pillars. The camera sees it from the circle as a violet ring in the mist.

## 5. Chapter 5: The Reading Room (Strike This Address). Map `reading-room`

Mood: **a vast, bare, paper-white round hall under a dark vault**, dust gold in the lamp pools, everything the same until it goes blank. Palette `#14110A #4A3F23 #F0E7C8 #E6B85C #8A1E1E`. The only interior chapter that is roofed.

### 5.1 Top-down sketch (circle at the origin; hall radius 125 m; door on bearing 180 to the south)

```
                          GALLERY (arena, bearing 0, 80 m): sunk until the 5th page is read
                          ringed steps, the Great Ledger, columns
       form racks 4,5 ....................................... form racks 1,2   (ten racks, five sections, round the wall)
     ................ hall wall: 24 bays of tall arched windows between pilasters ................
   .  [colonnade of 12 hall columns at 70 m]   chandeliers (6) hanging from the vault at 40 m
   .      page lecterns x5 round the circle (30 m): each lifts and settles in turn
   .         ( CIRCLE inlaid in brass, 20 m )  EMPTY DESK (6, 3) + chair, the one desk
   .                     |
   .   bookshelf rows ---+--- bookshelf rows       the carpet aisle (runner) from the door to the desk
   .   reading desks     |      reading desks      green banker's lamps on the desks
                     [WHITE DOOR] (bearing 180, 123 m): tall, pale, ruled like a form
```

### 5.2 Route

From the white door the red carpet runner runs 110 m straight in to the empty desk by the brass-inlaid circle, between two files of tall bookshelves and reading desks. The fight is at the circle. The five lecterns ring it at 30 m.

### 5.3 Landmarks

1. **The ten form racks**, five sections of two, covering the wall: tall frames of identical forms that go blank section by section.
2. **The white door** with its ruled leaf and frame.
3. **The vault**: a ribbed dome with hanging chandeliers (warm gold pools on the white floor).
4. **The Gallery** rising out of the floor behind the desk after the fifth page.

### 5.4 Off-route rewards

- **Talk**: the reading desks at 45 m (bearing 235) with a banker's lamp and a ledger open to the Registrar's first line (Slice's note: the ledger has no entries): flavour.
- **Secret**: the shelves' end bay at bearing 290, 50 m (the Journal's find, `Ledger Leaf`): a shelf with one missing book, lit by a lamp.
- **Pickup**: pages read give XP and the forms go blank (the Forms Turn); bolts from each wave.

### 5.5 Encounters

Unchanged: five waves at the circle, one per page; the Registrar at the Gallery.

### 5.6 Set dressing

`rd_hall_wall` bays (arched windows lit, pilasters, wainscot), `rd_hall_column` (30 m, fluted), `rd_vault` (ribbed dome seen from below), `rd_hall_chandelier`, `rd_form_rack` (full) and `rd_form_rack_blank` (swapped in when a section is read), `rd_circle_inlay`, `rd_runner`, `rd_bookshelf` files, `rd_reading_desk`, `of_desk` and `of_chair` (the empty desk), `rd_lectern` and `rd_page` (the moving page), `rd_white_door`, `of_desk_lamp`.

### 5.7 Lighting

Interior: a dim top light (`#F5E6B5`, 0.5 from straight above), ambient `#3A352B / #2B261D / #14110C`, fog `#14110C` 0.0045, skybox black with a gold haze. Six chandeliers (gold point lights, range 90), one lamp each at the desk and the door (range 20), a red wash on the carpet near the door. 10 point lights.

### 5.8 What makes it unlike 1 to 4

Enclosed, symmetric, tall, gold-and-paper white under a dome, with a floor of parquet and carpet: scale and a repeated unit (racks, shelves, windows) that is **allowed to feel like a form** and then goes blank. No sky, no mist, no ground texture of stone.

## 6. Chapter 5's boss arena: the Registrar's Gallery (bearing 0, 80 m)

Shape: a **ringed dais of three tiers** (22 m radius, brass ring between steps) round the **Great Ledger** (a large open book on a stand), enclosed by **eight tall columns** and a **curved brass rail** with a gap toward the circle. It is sunk 9 m under the floor and **rises** after the fifth page (`ReadingMap.Update`).

- **Readable shape:** concentric rings, gold on dark stone, round and bright against the white hall.
- **Cover/hazards the boss mechanics use:** the Ledger is the boss's anchor (the Registrar, a gold Titan Spider Bot with a ledger); the columns give cover and break line of sight; the rail edges the dais.
- **Dramatic and unlike the route:** the approach is a long red aisle through ordered shelves under white light; the Gallery rises out of the floor as a gold ring with columns and a lamp over the Ledger.

## 7. Chapter 6: Address Filed. Map `hollow-filed`

Mood: **dawn after the stamp, the Moonlit Hollow changed**: the Department's wall is gone to stumps, bright rolling grass, the circle drawn in white and brighter, a pale path to nothing and a gold seal. Palette `#0E1730 #324C86 #E8EEFF #F2CC66 #FFFFFF`. Open and quiet, the calm before the door.

### 7.1 Top-down sketch (circle at the origin, path on bearing 180, Threshold on bearing 20)

```
        far: freestanding bare doorframes standing in the grass (the wall's gaps), distant hills
   broken wall stumps round the rim (190 m), rubble, a gap where it stood
                                         THRESHOLD (arena, bearing 20, 80 m)
                                       white doorframe on a stair and dais, banners, stamp marks
   ( CIRCLE drawn in white, 20 m, brighter )   banners flank the path every 25 m
        |
        |   pale slab path (two slabs wide, plinth and lantern every 40 m)
        v
   path to nothing (180 m) ... a GOLD SEAL hanging in the air past its end
```

### 7.2 Route

The pale slab path leaves the circle on bearing 180, with a banner pole at every 25 m on alternate sides and a plinth with a gold lantern every 40 m, and runs on to 180 m where it stops at nothing; the great gold seal hangs just beyond. The Threshold, off the path to the north-north-east, is a bright doorframe on a dais you see from the start.

### 7.3 Landmarks

1. **The gold seal** at the path's end, hovering and turning slowly.
2. **The Threshold's bare white doorframe** at 80 m on bearing 20.
3. **The broken stumps** of the Department wall at 190 m and the freestanding doorframes standing alone in the grass beyond them.
4. **The white circle**, which unrolls into a line of writing and a number when the chapter is filed (the Door Number set piece).

### 7.4 Off-route rewards

- **Talk**: a plinth at 80 m on the path with the lantern lit: the first words of the Order's last page (flavour) and Slice's note.
- **Secret**: the freestanding doorframe at bearing 80, 75 m (the Journal's find, `A Cleared Hat`... the Circle's Stamp is at bearing 190, 65 m on the path's side): a small stone plinth with the circle's stamp.
- **Pickup**: the hidden find, the bolts from the three rushes.

### 7.5 Encounters

Unchanged: three rushes at the circle, then the Order-Bearer at the Threshold.

### 7.6 Set dressing

`fd_pale_slab` path, `fd_banner` poles, `fd_plinth` with `fd_lantern` (a lit gold lantern), `fd_wall_stump` (two variants) and `fd_rubble`, `fd_free_door` (a bare freestanding frame, leaning), `fd_circle_mark` (36 white marks that unroll), `fd_gold_seal`, `fd_pillar`, `fd_stair`, `fd_threshold`, `fd_dais`. The ground is a baked skin (grass texture `grass`, new set) in rolling swells.

### 7.7 Lighting

Dawn: a bright low sun (`#FFF0C2`, 1.1, from the east-south-east at 30 degrees), ambient `#3D4A6B / #4A5C80 / #1A1D2B`, fog `#6A7DA8` 0.0042, skybox from `#12204C` to a pale gold horizon with the sun disc. Lights: a gold glow at the seal (range 40), the lanterns (small, range 10), the Threshold's lamp. 8 point lights.

### 7.8 What makes it unlike 1 to 5

The only bright, green, open daylight place; white and gold stone against a pale sky; a few big shapes (doorframes, stumps, a seal) on rolling grass, no water, no machinery, no roof, no mist.

## 8. Chapter 6's boss arena: the Threshold (bearing 20, 80 m)

Shape: a **wide round dais** (22 m) reached by a stair, with the **bare white doorframe** at its far side, **two white pillars and two banners** flanking the stair, and **four red stamp marks** in front of the frame (the Order-Bearer's hazard). A gold ring is inlaid in the dais and eight gold radial lines run to the rim.

- **Readable shape:** white on green, the only hard vertical in an open field, with a gold ring in the floor.
- **Cover/hazards the boss mechanics use:** the stamp marks flash red every seven seconds (`FiledMap.Update`: a ring and a hit if Wallit stands on one); the frame is where the Order-Bearer walks out; the pillars are cover.
- **Dramatic and unlike the route:** the path is quiet and bright and goes nowhere; the Threshold is a lit door in a ring with red stamp marks at its foot.

## 9. Pieces added (61, in `Assets/Scripts/KitCatalogLate.cs`; spec in Docs/ModelKit.md section 10)

| Palette | Pieces |
|---|---|
| office (hub), 33 | `of_wall_bay`, `of_wall_window`, `of_wall_corner`, `of_ceiling_beam`, `of_pilaster`, `of_skyline_view`, `of_door_frame_1..6`, `of_door_leaf_1..6`, `of_chapter_board`, `of_lodger_tick`, `of_lodger_brim`, `of_lodger_spare`, `of_lodger_chime`, `of_lost_thing`, `of_notice_slip`, `of_hanging_lamp`, `of_wall_lamp`, `of_waiting_bench`, `of_plant`, `of_ring_inlay`, `of_number_plate`, `of_stamp_seal`, `of_paper_stack` |
| walk (ch 4), 8 | `wk_spire_a`, `wk_spire_b`, `wk_spire_c`, `wk_mist_bank`, `wk_paper_stand`, `wk_paper_sheet`, `wk_pier_post`, `wk_fallen_slab` |
| reading (ch 5), 9 | `rd_hall_wall`, `rd_hall_column`, `rd_vault`, `rd_hall_chandelier`, `rd_form_rack`, `rd_form_rack_blank`, `rd_circle_inlay`, `rd_page`, `rd_white_door` |
| filed (ch 6), 6 | `fd_wall_stump`, `fd_rubble`, `fd_free_door`, `fd_circle_mark`, `fd_gold_seal`, `fd_grass_tuft` |
| arena, 5 | `ar_server_dais`, `ar_server_pillar`, `ar_gallery_dais`, `ar_threshold_dais`, `ar_stamp_mark` |

Plus a new texture set `grass` (KitTextures) for Chapter 6's ground and the existing sets for everything else.

## 10. Side by side

| | Ch4 Long Walk | Ch5 Reading Room | Ch6 Filed Hollow | Hub Office |
|---|---|---|---|---|
| Time | blue hour dusk | no time, lamplight | dawn | night to dawn through the window |
| Palette | slate, pale, violet | dark wood, parchment, gold | white, gold, grass green | wood, brass, paper, lamp amber |
| Ground | mist floor with a pale causeway | parquet, carpet, brass inlay | dawn grass, pale path | board floor, rug |
| Shape | one long thin line | round hall, rings | open field, a few big shapes | a room with bays |
| Skyline | spires in mist | vault, hall wall of windows | stumps, freestanding doorframes, hills | window view of roofs and the clock tower |
| Hero props | lamp stilts, railings | racks, columns, chandeliers, shelves | seal, doorframes, banners, lanterns | six doors, board, lodgers |
| Arena | violet tiered dais, folded-leg pillars, paper stand | rising three-tier gallery, ledger, columns, rail | white dais, bare frame, stair, stamp marks | n/a |
| Light | violet lamps, heavy fog | gold chandelier pools | low gold sun | warm lamp pools, window glow |

## 11. How it is checked

- `SceneValidation.ScanChapter` (shared with chapters 1-3) now also covers the hub: `ChapterRoots` returns the office map's scene plus the hub world (people, interactables, door leaves). `Scan` reports renderers, kit pieces, built-in primitive meshes (by mesh name, which includes the old `Own Box/Cylinder/Cone`), triangles, visible triangles (under 150,000), estimated draw calls (under 400), lights and colliders.
- `Assets/Tests/PlayMode/LevelValidationTests.cs` gets one test per place: `Chapter4LongWalk...`, `Chapter5ReadingRoom...`, `Chapter6FiledHollow...`, `HubOffice...` (the hub builds its room through `ChapterMap.Load("office")` in a scene with `ChapterHub`; the test loads the map and the hub's dressing) and fails on any primitive or a budget breach. `KitValidationTests` checks the 61 new pieces (24+ vertices, 1,500 triangles, instanced, no copies).
- Playtest scenario `levels-4-6` (`PlaytestLevels.cs`, same pattern as `levels-1-3`): with real input on the menus and the sandbox save it opens the hub by the Hub button (events `level-load` hub, `scene-scan` hub, photos of the hub: doors, board, desk, windows, ring, rug, lodgers, back wall), then for each of chapters 4, 5 and 6 Campaign, case file on that chapter, Begin; the scene fills in (seconds and memory: bug over 25 s or 1,024 MB), `scene-scan`, and photos of every region **including the boss arena and its approach** (events `photo`, `photo-region`; metrics `levelTriangles4..6`, `levelDrawCalls4..6`, `levelPrimitives4..6`, `hubTriangles`, `hubPrimitives`, `hubDrawCalls`). The objectives, doors, set pieces, dialogues and boss fights of the chapters are played end to end by `chapter4`, `chapter5`, `chapter6`, `hub`, `hub-doors`, `hub-npcs` and `hub-life` (beta also runs them); nothing they read has moved.
