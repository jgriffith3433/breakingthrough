# Character models: silhouettes, details, idles, budgets

Goal: every character reads at a glance from the camera distance (about 9-12 m behind and above Wallit) and shows
personality while standing still. Sources: `Docs/CampaignBible.md` (arcs, voices), `Docs/CampaignDesign.md`, `Docs/Story.md`,
`AGENTS.md`. No Warrok, no bow, no birds. No GPU generation: every new body is authored in code (`CharacterKit.cs`) from
`KitMesh` (lathes, extrusions, tapered limbs, bevelled plates) with `KitTextures` materials (cloth, metal, brass, paper,
wood, glass), parented as named parts so the existing procedural animation drives them.

## Rules for all characters

- The body is never a capsule, cube or sphere. Small primitive accents (a button, a rivet) are allowed only under 12
  triangles and never as a body part. The validator (`CharacterValidation`, test `CharacterValidationTests`) fails on a
  built-in primitive mesh name in a body renderer, on a recoloured copy (same vertex/triangle/size signature as another
  character), and on a budget overrun. It prints triangles per character.
- Budget: under 6,000 triangles a character, under 12,000 for a boss. Bosses are the Courier, Foreman, Server, Registrar and
  the Order-Bearer.
- Part names are the contract with the animators. Yo Bot style rigs: `Body`, `Head`, `ClawL`, `ClawR`, `LegL`, `LegR`
  (each pivots where it joins). Spider style rigs: `Body`, `Head`, `LegL1`-`LegL3`, `LegR1`-`LegR3` (1 is the front pair that
  stabs). `Front` marker is optional (the head leads). +Z is front, feet on 0. Props hang off a part (`Clipboard` on `ClawR`).
- Hitboxes, tells and the companion are untouched: the capsule is fitted from the renderers (`Enemy.BuildModel`), claws
  excluded, so a body's silhouette must stay inside roughly the same footprint as the bot it replaces.
- Palette per character is distinct in hue AND value, so black-fill silhouettes and colour both separate them.

## Animation mapping (all procedural, `Enemy.AnimateModel` and friends)

| Beat | Bot rigs | Wallit / Slice |
|---|---|---|
| Idle | head look-around (`AnimateModel`), plus the character's own idle (`CharacterIdle`, below) | Animator `idle`; personality layer from `CharacterIdle` is the next part |
| Walk | stride phase swings legs and claws, body sway and bounce | Animator `run`, `strafe*`, `backPedal` (`Locomote`) |
| Tell / wind-up | `peckAnim` rises slowly (claw raises, head dips) then strikes fast; bosses use their own tell timers (`Courier` 2 s notice mark, `Server` paper out, `BossPhases`) which only read the position of `Clipboard`/`Paper` parts, not their shape | `swing1-3`, `groundSmash`, `homeRunCharge` with `OnBatContact` |
| Hit | `Enemy.TickFlash` property block on every renderer | `hitReact` |
| Death | `Fx.Death` flash, smoke, debris plates in `BotArt.PaintOf` colour | `death` / Slice `defeat` |

## Characters

### Wallit (with bat)
- Silhouette: a squat, wide wallet torso on two short legs, a fold flap on top like a hat brim, and a long bat held over one shoulder that breaks the outline into a diagonal.
- Details: 1) bill-fold body with stitched seam and a snap clasp; 2) a card sticking out of the slot like a tuft; 3) the wooden bat with a taped grip and a gilt band when the Journal's gilt bat is on.
- Palette: worn brown leather, brass clasp, cream card.
- Idle personality: pats his pocket, weighs the bat in his hand, one foot tap. Money-minded and impatient ("Pay up.").
- Budget: 6k. Source: `Assets/Art/Wallit` rigged FBX (Gen-authored, humanoid, `Wallit.controller`), kept as is.
- Status: existing rigged mesh; the slice for this part adds gallery and validation. The idle layer is listed in "Left for later".

### Slice
- Silhouette: tall and narrow, long dark coat that flares at the hem, one arm forward with a short bright blade, a hair mass that is wider than the shoulders.
- Details: 1) scalpel knife with a pale blade; 2) buckled belt and coat clasps; 3) heavy boots and a choker.
- Palette: black and deep plum, bone-white blade, one violet accent.
- Idle personality: stillness; she turns the scalpel in her fingers, checks the edge, glances at Wallit. Clipped, cool ("Clean.").
- Budget: 6k. Source: `Assets/Art/Slice/Slice.fbx` with `SliceSkin.png`, kept as is.

### Yo Bot
- Silhouette: small round-headed robot on two stubby legs with two claws held forward and a jet pack hump behind.
- Details: 1) visor with a cyan eye; 2) claws; 3) jet pack and lid. Palette: amber paint, grey trim, cyan eye.
- Idle: bobs and swivels its head, cheerful. Budget 3k. Source: `YoBot.fbx` plus `BotArt` stand-ins (existing).

### Spider Bot
- Silhouette: a low wide body on six jointed legs; the front pair rise like raised hands. Palette: dark steel, red glass.
- Idle: legs shift one at a time, head scans. Budget 5k. Source: `SpiderBot.fbx` (existing).

### The Courier (Chapter 1, boss)
- Silhouette: a pot-bellied Yo Bot body with a flat peaked cap, a heavy mail satchel on one hip and a tall clipboard held up like a flag.
- Details: 1) peaked cap with a brass badge; 2) satchel with a flap and strap across the chest; 3) clipboard with a clip and ruled notice.
- Palette: postal red and cream, brass badge, dark leather satchel.
- Idle: checks the clipboard (board tips toward the head), taps the pen on it, tips its cap to Slice. Chirpy, "Special delivery!".
- Animation mapping: `Courier.cs` slams and the notice mark use the `Clipboard` part position (`clipRest`); the new model keeps the part named `Clipboard`. Walk, tell, hit and death use the Yo Bot rig names.
- Budget: 9k (boss).

### The Foreman (Chapter 3, boss)
- Silhouette: broad Mega Yo Bot with a hard hat, thick square shoulders and a big order sheet across the chest.
- Details: 1) hard hat with a lamp and a stripe; 2) order sheet with ruled lines and a stamp; 3) hi-vis chest strap and heavy piston arms.
- Palette: amber and soot grey, hi-vis lime stripe, pale sheet.
- Idle: checks his clipboard-sheet, tilts the hat back, taps the clipboard against a claw. Overtime, "Order... unread...".
- Animation: Yo Bot rig names; `BossPhases` march uses the walk. Budget 11k.

### The Server (Chapter 4, boss)
- Silhouette: tall spider with a narrow polite body, one front leg folded to hold out a paper on a thin arm.
- Details: 1) bow-tie plate under the head; 2) a paper held on the claw tip (part `Paper`, read by `Server.cs`); 3) pale visor with a calm blue eye.
- Palette: pale blue and ivory, steel legs.
- Idle: waits, perfectly still, then offers the paper a little (polite bob). Never hurries.
- Animation: spider rig names; `Server.Lower`/offer use `Paper`. Budget 10k.

### The Registrar (Chapter 5, boss)
- Silhouette: Titan Spider Bot with a lectern-like back and a ledger held open before it. Palette: gold and black, paper pages. Idle: turns a page. Budget 11k.

Built (code-authored like the others, `CharacterKit.BuildRegistrar`): spider rig plus a root part `Ledger` (open book, spine, two tilted page blocks) with a child `Page` that the idle turns; a lectern slope and gilt bands on `Body`, spectacles on `Head`. `Enemy.MakeRegistrar` uses it and falls back to the shared bot plus the ledger prop if the rig can't be made.

### The Plated Alpha (Chapter 2, boss)
- Silhouette: a Plated Yo Bot grown big: slab torso with riveted seams, three overlapping pauldron plates a side, a slit-visor helm with a brass crest, and the red INVALID plate bolted to the chest (part `Invalid Plate`, read by the playtest and the fight). Palette: steel, soot, red plate, brass. Idle: rolls the shoulders, knocks a claw on its plate. Yo Bot rig names. Budget 11.5k. `Enemy.MakePlatedAlpha`, same fallback.

### The Order (Order-Bearer, Chapter 6, boss)
- Silhouette: a spider body carrying a huge scroll across its back crossbar; red seal at the centre. No face behind it: the creature behind the order is never seen.
- Details: 1) sealed scroll with end-knobs; 2) red wax seal with a stamp; 3) ribbon streamers.
- Palette: parchment, seal red, black. Idle: seal pulses slowly; ribbons drift. Budget 11k.

### Hub NPCs (the lodgers, `Story.Lodgers`, kit pieces `of_lodger_*`)
- Tick (wall clock, `#B9C7E6`): round face on a wooden case with a pendulum below; idle: pendulum swing, hands tick. Palette pale blue and wood.
- Brim (bowler hat, `#E6B7A1`): a hat with no head, a coat collar floating beneath; idle: tips and hovers. Palette salmon and brass.
- Spare (half-built Yo Bot): one claw, one missing leg, exposed rib frame; idle: tries a claw, droops. Palette bare metal and lamp.
- Chime (the bell): a brass bell on a thin stand; idle: sways, a tiny ring. Palette brass and iron.
- Each under 2k triangles. Existing kit models, authored in `KitCatalogLateOffice.cs`; the gallery shows them with the same idle layer.

## Gallery and checks

- `CharacterGallery` (runtime scene, no asset): every character in a row, idle, labelled, with a black-fill silhouette mode (`G` or the playtest) so the silhouettes can be compared; one photo per group.
- `CharacterValidation.Run()` builds every CharacterKit id plus Yo Bot, Spider Bot, hub NPC models and reports triangles and
  primitive bodies. Test: `Assets/Tests/PlayMode/CharacterValidationTests.cs`.
- Playtest `characters` photographs the gallery; `chapter1`/`chapter3`/`chapter4` still play the fights.

## Left for later (not in this part)

Wallit and Slice idle personality layers on top of the Animator; rebuilding Wallit and Slice bodies is NOT planned (their
rigs and animation clips are the shipped, accepted ones). Courier, Foreman, Server and Order-Bearer rigs are this part;
The Registrar and Plated Alpha are now authored the same way (code-built from `KitMesh`). Still later: a Hawkes Gen
mesh pass (views3d, Blender decimate to 9-11k triangles, rigid split on the same part names) to replace these code bodies.


## Gen bodies (Tools/Bake)

The Courier, Foreman, Server and Order-Bearer, and the lodgers and landmark props listed in `Tools/Bake/README.md`, are Gen
meshes baked by `Tools/Bake/glb_bake.py` into `Resources/Baked/<id>.mesh.txt` (decimated, split rigidly into the part names above
by `Tools/Bake/specs/<id>.json`, kit textures tinted per part). `CharacterKit.Make` loads them when every required part exists and
otherwise builds the code body (kept as the fallback; `BakedMesh.Enabled = false` forces it). Concepts were made from one
picture each, so the back and sides are the model's guess. Triangles: Courier 7,000, Foreman 8,000, Server 7,900, Order-Bearer 8,000
(budget 12,000). The `characters` playtest logs `gen-body` / `gen-prop` per piece.
