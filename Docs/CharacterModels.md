# Character models (spec)

Written before the build. Goal: every character reads at a glance from the camera distance (a 3rd-person camera 6-9 m back,
a character about 40-120 px tall) and shows personality standing still. No GPU generation: every mesh is authored in code
with `KitMesh` (lathes, extrusions, bevelled boxes), textured from `KitTextures` sets (fabric, leather, metal, brass, paper,
wood, glass) and parented as a part hierarchy so the procedural animation drives the parts. Code: `CharacterKit*.cs`
(builders), `CharacterRig.cs` (the animator), `CharacterGallery.cs` + `CharacterGallery` scene, `CharacterValidation.cs`.
Canon is `Docs/CampaignBible.md` and `Docs/CampaignDesign.md`. No Warrok, no bow, no birds, no piano.

## 1. Rules

- **Body is never a primitive.** Torsos, heads and limbs are lathes (tapered), extrusions or bevelled boxes with 24+ vertices.
  Small primitives are allowed only as accents (buttons, eyes), and the builders do not use them: everything is `KitMesh`.
- **One model per character.** No recolour of another character's mesh. Yo Bot variants (Plated, Mega) get their own plate
  and size parts on the Yo Bot body, they do not replace it.
- **Black-fill test.** Each silhouette below must differ from every other by outline alone (head shape, prop, leg count).
- **Budgets.** Under 6,000 triangles per character, 12,000 for bosses (Courier, Foreman, Server, Registrar, Order-Bearer).
  Targets are lower (see table); `CharacterValidation` prints every character's count and fails over budget.
- **Part names stay.** Yo Bots keep `Body`, `Head`, `ClawL`, `ClawR`, `LegL`, `LegR`; Spider Bots `Body`, `Head`,
  `LegL1-3`, `LegR1-3` (1 is the front pair), each part pivoting where it joins, +Z front, feet on 0 (AGENTS.md "Bot models").
  Hitboxes live on `Enemy`/`Musician`, not on the mesh, so the model never changes them.
- **Scale.** Wallit 1.0 m tall (a small wallet), Slice 1.7 m, Yo Bot 0.9 m, Spider Bot 0.8 m before `ForSpawn` scales it,
  bosses are the roster's boss scale of the same models plus props.

## 2. The cast

Budgets: target / limit triangles. "Idle" is what the model does when nothing happens (the personality loop).

### Wallit (with bat)
- **Silhouette:** a squat bent-open wallet on two short legs, one wide lopsided "mouth" flap, a long bat resting on the
  shoulder: a tall diagonal line over a short round-cornered block. Reads as "a rectangle with a stick".
- **Details:** (1) leather body with a folded flap that hangs like a cap and a crooked grin cut into the front; (2) bills
  sticking out of the pocket, one always loose; (3) the wooden bat (tapered lathe, grip tape, scuffed barrel) held in a
  back-hand, a stamp-ink smudge on the flap. Stance: feet wide, weight on the back foot, chin up.
- **Palette:** brown leather `#6b4426`, cream stitching, green bills `#6f9a5b`, bat wood `#c99a5b`, stamp red `#b3262a` smudge.
- **Idle:** bounces on his toes, taps the barrel on his palm every few seconds, checks the pocket (a bill flicks), flap grin widens.
- **Budget:** 3,500 / 6,000.
- **Animations:** `walk` = leg swing + body bob + bat sways; `swing1-3`, `groundSmash`, `homeRunCharge/Release` raise and drop the
  bat arm on the existing `BatEquipment` timings (contact frame unchanged); hurt = flap snaps back, bills fly; death = flops open flat.

### Slice
- **Silhouette:** a tall narrow figure with a long coat hem and a big top-knot, scalpel out to the side: a thin vertical
  with a flared skirt and a single bright line (the blade). The only tall slim silhouette in the cast.
- **Details:** (1) black high-collar coat with a pleated, asymmetric hem and pale face under a heavy fringe; (2) scalpel in the
  right hand, long and thin, a cold highlight on the edge; (3) heavy boots and a studded belt with a bandage roll. Stance: hips
  square, still, knife hand low and loose.
- **Palette:** coat black `#17141c`, hair violet-black `#241a33`, skin pale `#d9d0d8`, steel `#c4ccd6`, belt studs brass.
- **Idle:** almost nothing, which is her personality: slow breath; every ~6 s the knife hand turns the blade over once and she
  fidgets one boot; eyes flick to the nearest bot. Never smiles.
- **Budget:** 3,800 / 6,000.
- **Animations:** `stand`/`stab`/`slash` (Musician `TickKnife`) lift and sweep the knife arm; hurt = head turns, coat swings;
  defeat = kneels then lies; she follows Wallit as the companion (root motion is `Companion`, the model only walks).

### Yo Bot
- **Silhouette:** a boxy little torso on two stubby feet with a flat wide lid-head and a single antenna ball, claws hanging
  forward. A "mailbox on legs".
- **Details:** (1) lid-head with a visor slit that glows; (2) jet pack canister on the back with two nozzles; (3) two jointed
  claws with three fingers. Plated: shoulder plates and a chest plate; Mega: bigger, thicker, horns on the lid.
- **Palette:** per-kind paint (BotArt `Look`), dark joints, visor glow teal-white.
- **Idle:** chirpy: head tilts side to side, antenna ball wobbles, claws click open and shut.
- **Budget:** 2,000 / 6,000.
- **Animations:** walk = alternating feet + body bob; tell = claws rise and the head leans back; strike = claws swipe forward;
  hurt = flash (MaterialPropertyBlock) + lean; death = `Fx.Death` plates, the model drops.

### Spider Bot
- **Silhouette:** a wide low oval abdomen with a small head and eight bent legs splayed: the only one wider than tall.
- **Details:** (1) eight legs of three jointed segments each (hip, knee, foot spike); (2) a head full of glass eyes, one big
  front pair; (3) abdomen with rear jets and a stamped plate.
- **Palette:** gunmetal `#3a3f4a` shell, orange eye glow, steel leg spikes (Titan: gold trim).
- **Idle:** legs shuffle in a ripple, head swivels, abdomen rises and sinks.
- **Budget:** 3,800 / 6,000.
- **Animations:** walk = alternating leg tripods; tell = front leg pair rises (the leg stab); strike = front legs drive down;
  hurt = shiver; death = legs fold.

### The Courier (boss, Ch1)
- **Silhouette:** a Yo Bot with a flat cap and a big clipboard held up like a shield, a rolled notice under the arm: a
  rectangle (the board) in front of a box body. Peaked cap brim breaks the lid line.
- **Details:** (1) clipboard with clip and paper sheets; (2) delivery cap with a badge; (3) satchel strap and a red-sealed notice.
- **Palette:** postal blue `#2f4d86`, cream paper, red seal `#b3262a`, brass buckle.
- **Idle:** cheerful: checks the clipboard, ticks something, looks up, chirps.
- **Budget:** 4,500 / 12,000.
- **Animations:** tell = lifts the notice (red mark fills the circle, `Courier.cs`); slam = drops the clipboard; hurt/death = clipboard falls flat.

### The Foreman (boss, Ch3)
- **Silhouette:** a Mega Yo Bot, tall and wide, hard hat, a big order board strapped to the chest and a hook-arm: a barrel
  with a hat and a sign.
- **Details:** (1) hard hat with a lamp; (2) the order board (the sheet is replaceable: true order, wrong order); (3) one
  hydraulic hook arm and a pocketed clipboard he checks.
- **Palette:** safety orange `#d9701c`, yellow hat `#e6b422`, dark steel, white sheet.
- **Idle:** checks the clipboard, taps the pen, glances at the cleared clock.
- **Budget:** 5,500 / 12,000.
- **Animations:** tell = board raised and read aloud, the march starts; strike = hook arm swings; death = on its knees, sheet fluttering off.

### The Server (boss, Ch4)
- **Silhouette:** a Spider Bot with one long front leg folded out holding a flat paper tray: a spider with a waiter's arm.
- **Details:** (1) the paper arm (front-left leg with a flat claw tip holding a sheet); (2) a bow-tie plate under the head;
  (3) polished white-and-grey shell, polite.
- **Palette:** pale grey `#c9ccd2`, black bow-tie, cream paper, soft red wax seal.
- **Idle:** offers the paper, waits, withdraws, offers again; one leg taps.
- **Budget:** 5,000 / 12,000.
- **Animations:** offer = paper leg extends (no strike while out); refusal = leg snaps back; three refusals then the leg stabs; death = leg folds.

### The Registrar (boss, Ch5, listed for later)
A gold Titan Spider Bot with a ledger of pages held open. Not in this change (see "Left").

### The Order-Bearer (boss, Ch6)
- **Silhouette:** a tall faceless herald in a long robe-like plated skirt with a huge scroll across its back and a seal at the
  neck: a bell-shaped body, no face, a diagonal scroll.
- **Details:** (1) blank smooth head with no eyes, a single seal-red slit; (2) the scroll across the back with a red ribbon and
  seal; (3) a stamp-hammer arm.
- **Palette:** pale paper `#ece6d2`, red seal `#c71f1f`, ink black.
- **Idle:** utterly still, scroll edge lifting in a breeze; head turns exactly toward the nearest living thing.
- **Budget:** 5,500 / 12,000.
- **Animations:** tell = stamp arm rises, the mark appears; strike = stamp down; death = the seal cracks and the scroll falls open, blank.

### Hub NPCs (lodgers, small, 1,200 / 6,000 each)
- **Tick** (wall clock with legs): round clock face on two thin legs, pendulum tail. Brass and dark wood. Idle: winds himself
  (arm cranks a key), glances at the others' time, hands tick.
- **Brim** (floating hat): a wide-brimmed hat with a band and a floating tilt. Felt grey with a blue hatband. Idle: hovers, tips
  to the room, tries a slow turn "is this my head?".
- **Spare** (half-built Yo Bot): a Yo Bot torso with one arm, one leg stub and a crooked lid. Rust-orange. Idle: sweeps with the
  wrong arm, chirps at nothing, lid slips.
- **Chime** (small bell): a bell with a clapper and a small hanging loop. Brass. Idle: sways, rings once toward the next door.

## 3. Idle personality, as code

`CharacterRig` runs every character's idle in `Update`: a base breath/bob (a sine on the `Body`) plus a per-character
`Fidget` coroutine-free timer that picks one of 2-3 moves (Slice turns the blade, the Foreman checks his clipboard, Tick winds
his key, the Courier ticks the clipboard). `Walk(speed01)` swings limbs; `Tell(t01)` poses the wind-up from 0 to 1 (callers pass
their existing tell fraction, so timing is theirs); `Strike()` fires the swipe; `Hurt()` pulses a tint and leans;
`Die()` plays the collapse. Characters expose the same API so `Enemy`, `Musician`, `Companion` and hub code can call it.

## 4. Verification

- `CharacterValidation.Run()` builds every character, fails if any body mesh is a built-in primitive (by name or by being a plain
  8-vertex box), has under 24 vertices, or exceeds its budget; prints triangles per character. `CharacterValidationTests` runs it by reflection.
- `CharacterGallery` scene (all characters in a row in idle, labelled) and the `character-gallery` playtest photograph it:
  a line-up, a close shot of Wallit and Slice, a black-fill line-up (silhouette check), then rig checks (walk, tell, hurt, death).

## 5. Phasing

1. Spec (this file). 2. Vertical slice: Wallit and Slice finished, in the gallery. 3. The rest of the cast. 4. Gameplay wiring
(`Enemy` bots, the bosses and the hub) onto the authored rigs, left for a follow-up when the models are approved from the gallery.
