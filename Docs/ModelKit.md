# The Place Department: Model Kit spec

Status: spec, written and committed **before** any kit code (see Docs/CampaignDesign.md 6.2 for why: levels were test boxes). Companion to Docs/CampaignBible.md (chapter moods) and Docs/CampaignDesign.md (palettes). No GPU generation: every mesh and texture is authored in code (`Assets/Scripts/Kit*.cs`).

## 1. What the kit is

A shared set of **82** authored pieces, grouped into 9 palettes (six chapters, the street/office shared sets, boss arenas). Each piece is a real mesh built from bevelled boxes, lathes, extrusions with ear-clipped caps and wall panels with cut-out openings, framed with trims and sills. No piece is a primitive mesh or a recoloured copy: silhouettes differ, and each is checked by the validator (section 5).

Rules: a piece is **at least 24 vertices** and never a built-in mesh named Cube, Sphere, Capsule, Cylinder, Plane or Quad. A piece below 24 vertices would need a written reason in `KitCatalog` (none are planned: the smallest piece is the pale slab, about 80 triangles).

## 2. Grid and snapping

- Grid: **0.5 m** units. Wall bays are **4 m** (the street/house/door/shop pieces) or **3 m** (office and reading-room walls); heights **3 m** (storey) with the base line on y = 0.
- Pivot: bottom centre of the footprint for freestanding pieces; bottom-left-back corner is **not** used. Wall pieces pivot at the bottom centre of their span, facing +Z (front), thickness behind it.
- Each prefab carries a `KitPiece` component with `Palette`, `Footprint` (cells on the 0.5 m grid) and `Snap` size; `KitCatalog` lists them. Placement code multiplies grid cells by 0.5 and turns in 90 degree steps, so pieces butt without gaps.
- Playfield rule (AGENTS.md): the kit is for levels and maps with colliders; Arcade's 150 m playfield stays free of solids. The kit pieces have **no colliders** by default (levels add their own).

## 3. Materials and textures

One shader, `Knoll/KitLit` (`Resources/Shaders/KitLit.shader`): albedo + tangent-space normal map + vertex-colour ambient occlusion (dark at the foot, light at the top for grime), GPU-instancing on. Textures are generated in code (tileable, periodic noise; a normal map from the height field with wrap), then shared across every palette; palettes differ by **tint, texture choice and shape**, not tint alone.

| Texture set | Size | Content |
|---|---|---|
| brick | 256 px | cold red-brown brick, offset rows, mortar, soot |
| plaster | 256 px | chalk wall, hairline cracks, damp stains |
| wood | 256 px | grain planks, knots, worn edges |
| metal | 256 px | brushed steel, scratches, rivet dots, rust blooms |
| iron | 256 px | cast iron, pitted, flaked paint |
| brass | 128 px | polished brass, tarnish |
| stone | 256 px | irregular blocks, chips, lichen |
| concrete | 256 px | pits, aggregate, stains |
| slate | 256 px | overlapping shingle rows |
| cloth | 128 px | woven threads, folds |
| paper | 128 px | fibre, faint ruled lines |
| glass | 64 px | dark pane with streak reflection |

Memory: 12 sets, albedo + normal each, RGBA32 with mipmaps: 7 sets at 256 px (0.33 MB each map), 4 at 128 px, 1 at 64 px. Total about **8 MB** (limit 64 MB). Materials: one per (texture set, tint) pair, about 30, all sharing those textures.

## 4. Budgets

| Budget | Limit | How measured |
|---|---|---|
| Visible triangles in a WebGL level | **under 150,000** | `KitValidation.VisibleTriangles(camera)`: sums the triangles of every renderer inside the camera's frustum. KitPreview logs it for the gallery and each vignette |
| Textures, whole kit | **under 64 MB** | sum of unique textures: width x height x 4 bytes x 4/3 (mips) |
| One piece | **under 1,500 triangles** | the validator; planned sizes are in the tables below (estimates: the validator prints the real counts) |
| Materials per piece | 1 or 2 submeshes | `Renderer.sharedMaterials.Length` |
| Draw calls | shared materials, GPU instancing on | `material.enableInstancing` checked by the validator |

Planned total for all 82 pieces once each: about 27490 triangles. A dressed level repeats pieces (instanced), and levels should stay within 150k visible by keeping about 150 placed pieces or fewer in view (average 450 triangles gives 68k).

## 5. Measuring and validating

- **Tools > Kit > Validate Kit** (editor) and the PlayMode test `KitValidationTests` call `KitValidation.Run()`. It builds every piece from `KitCatalog` and fails if a mesh is named Cube/Sphere/Capsule/Cylinder/Plane/Quad, is a plain box (12 triangles or fewer), has under 24 vertices, has no texture on its material, is not instanced, has the same vertex/triangle/size signature as another piece (a recoloured copy), has fewer than 8 pieces in a chapter palette or 60 overall, or if the totals exceed section 4.
- It prints one line per piece (`vertices`, `triangles`, `textureMB`, materials) and the totals.
- The playtest scenario `kit-preview` logs the same report as events and metrics (`kitPieces`, `kitTriangles`, `kitTextureMB`, `kitPrimitives`).
- Tools > Kit > Build Kit Assets bakes meshes, materials, textures and prefabs into `Assets/Kit/<palette>/`. The runtime builds from the same code, so the game and the playtest never depend on the baked files.

- Bevels smaller than 1.1 cm are dropped by `KitMesh.Box` (a chamfered box costs 44 triangles against 12), so only visible chamfers are paid for.
- Editor auto-bake: on editor load `KitBuilder` bakes `Assets/Kit` if the prefabs are missing; **Tools > Kit > Build Kit Assets** does it by hand (batch: `-executeMethod KitBuilder.BuildAll`; validation: `KitBuilder.ValidateBatch`).
- Playtest: scenario `kit-preview` loads `Assets/Maps/KitPreview.unity` (an empty scene `KitPreview.cs` fills: the street corner at the origin, the interior room at x = 100, the gallery at z = 100 and up) and photographs each stop.

## 6. Pieces per palette

### Street (shared town; Ch1-2 edges, Ch4 lamp stops)

Mood: slate dusk, brick and plaster, awnings, lamp iron. 13 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `st_shopfront` | 4 m bay: brick wall with a big shop window (frame, mullion, sill) and a recessed door with step | 650 | brick+glass |
| `st_awning` | scalloped canvas canopy on two iron brackets | 220 | cloth |
| `st_lamp_post` | lathe: stepped plinth, fluted shaft, collar, curved arm, caged lantern | 460 | iron+glass |
| `st_bench` | slatted seat and back, scrolled iron ends | 320 | wood+iron |
| `st_bin` | tapered lathe body with rolled rim, domed lid, two handles | 260 | metal |
| `st_crate` | bevelled slat box with corner braces and a lid gap | 220 | wood |
| `st_hydrant` | lathe barrel, domed cap, two side nozzles, base flange | 300 | metal |
| `st_house_wall` | 4 m plaster wall, two shuttered windows with frames and sills, string course | 560 | plaster+glass |
| `st_door_wall` | 4 m plaster wall with framed door, transom, step and lintel | 480 | plaster+wood |
| `st_roof_pitched` | gable roof with eaves, fascia and ridge cap, shingle rows | 260 | slate |
| `st_roof_flat` | flat roof slab with parapet and coping | 140 | concrete |
| `st_chimney` | tapering brick stack with cap and flue | 160 | brick |
| `st_kerb` | kerb stone and paving strip with chamfered edge | 110 | concrete |

### Office (the hub; Ch4-5 interiors)

Mood: warm lamp-light, wood, paper, brass. 11 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `of_desk` | pedestal desk: top with overhang, two drawer stacks with fronts and pulls, kick plate | 520 | wood+brass |
| `of_chair` | swivel chair: round seat, curved back, post, five-star base | 360 | wood+iron |
| `of_filing_cabinet` | four-drawer cabinet with label frames, pulls and plinth | 320 | metal |
| `of_stamp_press` | bench stamp press: base plate, column, lever arm, round stamp head | 420 | iron+brass |
| `of_pneumatic_tube` | vertical lathe tube with flanges and an elbow into the wall | 360 | brass |
| `of_mail_slot_wall` | 3 m panelled wall with a row of brass mail slots and flaps | 480 | wood+brass |
| `of_pigeonholes` | shelf block of 4x4 pigeonholes with a moulded top | 420 | wood |
| `of_rug` | trimmed rug with border step and fringe blocks | 140 | cloth |
| `of_desk_lamp` | lathe base, bent arm, conical shade | 280 | brass |
| `of_coat_stand` | lathe pole, tripod feet, four hooks | 260 | wood |
| `of_wainscot_door` | 3 m wall: panelled wainscot, plaster upper, door with frame and architrave | 520 | wood+plaster |

### Chapter 1 Sorting Yard (cold night, gold ring, red ink)

Mood: moon stone, dead gravel, post-office iron. 10 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `yd_standing_stone` | faceted leaning monolith (irregular extruded polygon with bevelled top) | 130 | stone |
| `yd_ring_segment` | curved kerb arc of the gold ring, raised lip | 180 | stone |
| `yd_fallen_mailbox` | fallen post box on its side: arched top, flap, post stump | 340 | metal |
| `yd_post_box` | pillar box: lathe body, domed cap, slot frame, plinth | 380 | metal |
| `yd_fence` | picket fence section with posts, rail and pointed pickets | 260 | wood |
| `yd_sorting_trestle` | long sorting table on A-frame legs with pigeon rail | 280 | wood |
| `yd_sack_pile` | three lumpy mail sacks with tied necks (lathe, noise-displaced) | 300 | cloth |
| `yd_gravel_slab` | path slab with chamfer and cracked inset | 90 | concrete |
| `yd_signpost` | post with two arrow boards and a cap | 170 | wood |
| `yd_stump` | gnarled stump: lathe with root flares and a cut top | 260 | wood |

### Chapter 2 Gate Hall (chalk blue, INVALID red)

Mood: chalk plaster, blue-grey steel, turnstile iron. 9 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `gt_turnstile` | three-arm turnstile: lathe hub, arms, side posts and footplate | 520 | iron |
| `gt_gate_arch` | round arch gateway with voussoirs, keystone and jambs | 460 | stone |
| `gt_ticket_booth` | booth: base, counter, window with frame, hipped roof | 480 | plaster+glass |
| `gt_stamp_press_large` | big press: frame, piston, platen and ink pad | 620 | iron |
| `gt_barrier_rail` | low rail with posts and a rope-hook bar | 210 | iron |
| `gt_bollard` | lathe bollard with banded cap | 190 | iron |
| `gt_invalid_board` | framed sign board on two legs with a bevelled panel | 160 | wood |
| `gt_chalk_lamp` | paired lamp standard: pillar, twin arms, frosted globes | 430 | iron+glass |
| `gt_gate_pillar` | square pillar with plinth, shaft, cornice and ball finial | 300 | stone |

### Chapter 3 Clearing Floor (amber factory, rust, steel)

Mood: oily steel, rust, amber caged lamps. 9 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `fl_conveyor` | belt segment: side rails, rollers, belt surface, legs | 640 | metal |
| `fl_crusher` | press: twin posts, crosshead, piston and plate | 520 | iron |
| `fl_hook_rail` | I-beam rail with six hanging hooks | 420 | iron |
| `fl_chassis_rack` | racking: uprights, shelves, hung chassis frames | 470 | metal |
| `fl_girder` | I-beam with flanges, web and rivet heads | 170 | metal |
| `fl_pipe_run` | pipe with flanges, bolt rings and a valve wheel | 340 | metal |
| `fl_barrel` | ribbed drum with rolling hoops and a bung | 260 | metal |
| `fl_catwalk` | grating panel with bars, handrail and posts | 380 | metal |
| `fl_cage_lamp` | wall bracket with caged bulb lathe | 260 | iron+glass |

### Chapter 4 Long Walk (slate dusk, violet lamp heads)

Mood: slab, mist, slim iron stilts. 9 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `wk_causeway_slab` | bevelled slab with inset seam and edge wear | 120 | stone |
| `wk_lamp_stilt` | tall tapering stilt, lathe collar, cradle with a violet lantern | 430 | iron+glass |
| `wk_plaza_kerb` | curved plaza kerb segment with chamfered top | 170 | stone |
| `wk_railing` | railing: posts, top rail, balusters | 320 | iron |
| `wk_mooring_bollard` | wide bollard with a chain eye and a cap | 210 | iron |
| `wk_pylon` | tapered pylon with banding and a flat cap | 300 | concrete |
| `wk_stone_bench` | slab bench on two block legs | 200 | stone |
| `wk_arrow_sign` | pole with a pointed arrow board and a ring | 210 | iron |
| `wk_mist_urn` | lathe urn with a flared lip and plinth | 260 | stone |

### Chapter 5 Reading Room (lamp gold, paper white)

Mood: dark wood, parchment, gilt. 9 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `rd_lectern` | slanted reading top, book ledge, tapered pedestal and foot | 360 | wood |
| `rd_bookshelf` | tall shelf: frame, five shelves, rows of book blocks | 720 | wood+cloth |
| `rd_reading_desk` | double desk with drawers, modesty panel and lip | 460 | wood |
| `rd_great_ledger` | open ledger on a stand: cover, page blocks, ribbon | 320 | paper+cloth |
| `rd_column` | fluted lathe column with base and capital | 610 | stone |
| `rd_arch_window_wall` | 3 m wall with an arched window, frame, sill and keystone | 620 | plaster+glass |
| `rd_form_board` | wall board with pinned forms (paper stacks) and a frame | 170 | paper+wood |
| `rd_chandelier` | ring chandelier: lathe hub, arms, six candle cups | 520 | brass |
| `rd_runner` | carpet runner with border, step and tassel blocks | 110 | cloth |

### Chapter 6 Threshold (dawn blue, white, gold)

Mood: pale stone, white plaster, gold trim. 9 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `fd_threshold` | door frame on a dais: jambs, lintel, cornice and number plate recess | 420 | stone+brass |
| `fd_dais` | three-tier round dais with chamfered steps | 260 | stone |
| `fd_pillar` | white pillar: plinth, round shaft, capital | 420 | plaster |
| `fd_round_arch` | tall round arch with keystone and rolled moulding | 430 | stone |
| `fd_pale_slab` | pale slab with fine chamfer | 80 | stone |
| `fd_plinth` | stepped plinth with a moulded cap | 210 | stone |
| `fd_banner` | pole, crossbar and a hanging banner with swallowtail | 260 | cloth+brass |
| `fd_stair` | straight stair flight with stringers and rails | 330 | stone |
| `fd_lantern` | hanging gold lantern: lathe cage, cap and chain links | 310 | brass+glass |

### Boss arenas (Courier, Foreman, Server, Registrar, Order-Bearer)

Mood: reuses its chapter's hero colour, shared silhouettes. 3 pieces.

| Piece | Silhouette | Tris | Material |
|---|---|---|---|
| `ar_ring_floor` | round arena floor: lathe disc, inset ring, outer lip | 360 | stone |
| `ar_brazier` | tripod brazier: legs, bowl, ember bed (matte), handle ring | 340 | iron |
| `ar_boss_gate` | wide gate wall with a centred arch and flanking buttresses | 520 | stone |

Total: 82 pieces.

## 7. Vertical slice, then the rest

1. **Street corner** (KitPreview vignette A): two shopfront/house bays meeting at a corner, awnings, a pitched roof, a chimney, lamp post, bench, bins, crates, a hydrant and kerb.
2. **Interior room** (vignette B): wainscot door wall, mail-slot wall, desk with chair and lamp, filing cabinet, pigeonholes, rug, coat stand, stamp press and pneumatic tube.
3. Both are built and checked against this spec first (`KitPreview` logs `vignette-street` and `vignette-office`); then the gallery of every piece on a labelled grid, one row per palette.

## 8. What differs between palettes (so chapters do not look alike)

- Street/House: brick and plaster, pitched slate roofs, awnings.
- Office/Reading: wood, brass, paper and cloth, round and moulded profiles.
- Yard/Gate: stone and iron, arches and posts, chalk-blue tint.
- Floor: riveted steel, grating, cages, amber tint, no soft curves.
- Walk: thin tall iron and slab stone, violet glass.
- Filed: white plaster and pale stone, gold trim, symmetrical doorframes.
