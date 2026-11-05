# The Place Department: Campaign design spec

Design only. No gameplay code comes with this page. It is the plan the vertical slices are built and judged against.
Read with `Docs/CampaignBible.md` (screenplay, choices, map sheets), `Docs/Story.md` (canon) and `AGENTS.md`.

J: "Make it fun. The most important thing is to make this a fun game that looks good too. The characters need to have
personality and they need to be expressed in-game and through the events in the game and the story needs to be expressed
in game as well."

## 0. What is wrong today, and the rule that follows

What ships now: every chapter is `ChapterRun` in the game scene with `WaveDirector` in `Phase.Chapter`. The chapters differ
by a skybox, some props and a text card, but the loop is the Arcade loop: stand at the circle, bots arrive, count kills.
That is "the wave game with a text intro".

**Forbidden (memory m_098402ce499b):** building any campaign chapter on `WaveDirector`, `Phase.Chapter`, or
`ChapterRun`-in-the-arena. `ChapterRun`'s chapter flows (`HoldThenBoss`, `FloorRun`, `GateAndBack`) are retired as the
campaign's engine; they stay only so old files and playtests load until each slice replaces its chapter. Arcade is
untouched and stays the wave game.

**What a campaign level is:** its own scene (`Assets/Maps/Level_<n>_<name>.unity`, new scenes, not the `Map_*` skirts),
its own geometry that Wallit walks through and is stopped by, its own objectives driven by a per-level script
(`LevelScript`, a plain MonoBehaviour per level), a hand-placed cast, and **no wave timer**. Enemies are placed
encounters (a room, a corridor, a yard) that wake when Wallit enters them, not a spawner.

Reuse, not rebuild: Wallit's controller, bat, `Combat`, `Enemy` stats and models, `Companion`, `Cutscene`/`CutsceneShots`,
`Choices`, `CampaignSave`, `CampaignProgress`, `Journal`, voices, `Fx`, `MusicDirector`. These are engine pieces, not wave pieces.

**Open call for J (the one rule this spec bends).** `AGENTS.md` says nothing solid within 150 m of the piano and bots walk
straight lines. That is right for Arcade and for the one held fight below. A level that is a corridor, a yard, a causeway
cannot be collider-free: Wallit must hit walls and doors. Proposal: campaign levels use real colliders on their own
scenes, and each encounter has its own authored walking lines (waypoints per encounter, `Enemy.Route`), still no
pathfinding. Camera-shake rule stands untouched (only the bat's small contact jolt). If J says no, levels keep the
open-field shape and use gates and low fences the bots simply walk through; section 4 notes where that costs the design.

## 1. Player fantasy (two sentences)

You are Wallit, a loud little wallet with a wooden bat who has been thrown away and decides everyone who wronged him owes
him, collecting in cash and swings from a Department that files people into nothing. You walk into its rooms with Slice at
your shoulder, make the place pay for each door it shut on you, and leave behind an address that is finally real.

## 2. The cast in play

Every character is expressed three ways: what they **do when idle**, how they **react to events**, and **how they move and
fight**. Lines below are samples in the character's voice, to be banked in `WallitVoice`, `SliceVoice`, `CastVoice`, gated
by `VoiceDirector` (one voice at a time; nothing talks constantly). Voice rules from canon: Wallit money, brash, never admits
fear (he converts it to money); Slice clipped, a few words, never a joke; Yo Bots chirpy and never understand what they say;
no piano, no office jokes, no Warrok, bow or birds; the creature behind the order is never seen.

### 2.1 Wallit

- **Personality:** a refund-seeker with a short fuse and a soft centre he would bill you for mentioning. Everything is an
  invoice. Brash outside, scared of being "cleared" inside, which he hides as haggling.
- **Moves like:** a swagger-run, bat on his shoulder when nothing is near; bills flutter from his top on a hard stop. He
  leans into corners. He pats his pocket when picking something up.
- **Cares about:** receipts (pickups are "receipts" he sticks in his pocket), tips, exact change, the postmark half.
- **Idle barks** (after 16 s still, in a safe room): counts his bills ("Forty. Forty-one. Somebody short-changed me."), taps
  the bat on a wall and listens, reads a plaque aloud with a price tag voice.
- **Reacts to events:** fall from a ledge ("That's a fee."); door locked ("Locks have keys. Keys have owners."); finds an
  item ("Finders keepers."); Slice goes down ("Slice! Hold on!" and he runs to her, ignoring bots); a bot he spared
  ("Don't make me bill you."); low on health ("Not a scratch. Okay, a scratch. Extra.").
- **Fights like:** the three-hit Deposit / Withdrawal / Overdraft! combo, home run, smash (existing). New feel: **he
  swaggers after a streak**: five hits without being hit and his idle becomes a coin-flip on the bat for a beat.
- **Items:** the bat, the Courier's notice (if kept), the torn postmark, the Department stamp (taken in Ch5).

### 2.2 Slice

- **Personality:** a guard who has decided not to explain. Dry, watchful, loyal through action. Her care is shown by where
  she stands. Never a joke; at most one word that lands like one ("Noted.").
- **Moves like:** economical. She stops one step behind his right shoulder, never ahead. Her knife is always out, point down
  when calm; she turns her whole body to a sound before she turns her head.
- **Cares about:** the ring, the number on a door, things that were lost (she touches the lost things with two fingers).
- **Idle barks:** "Left." (a threat he has not seen), "Quiet." (she holds him back with a hand), "Closer." (she wants him
  within the ring). When he talks too long she says "Wallit." once.
- **Reacts to events:** he opens a door ("Check it."); he takes a hit ("Careful." only after the third); a bot stops at a
  mark ("Red is where it lands."); he spares something ("Noted."); he finds a lost thing ("Keep it."); he uses the stamp
  ("That's a lot of nerve.").
- **Fights like:** she is a companion in chapters where story puts her beside him (2, then 4, 6) using `Companion.cs`; she
  guards a doorway while he works a room; she **calls the telegraph** for each boss. She goes down; the revive is E held.
  Wallit's rule: he always revives before he swings. If he leaves her down for 20 s she stays down and says "Noted."
  with an edge (canon, ch2).
- **Plays on the player's side:** a **Hold** order (hold E near her: she plants and guards a spot) and **Come** (tap F: she
  returns to his shoulder). Two orders, no menu.

### 2.3 The lodgers (hub)

Four lost things that stayed. None fights. Each has a daily routine **in the room**, visible without talking, and a
chapter-by-chapter change (already in `HubLife`; this spec says how it shows).

| Lodger | Look in play | Routine you can watch | Changes as the file advances |
|---|---|---|---|
| **Tick** (clock) | wall clock with legs, sits at the desk | winds himself, checks the others' time, stamps nothing | desk plate changes; by Ch5 he keeps his own hour; the hub clock stops running fast |
| **Brim** (hat) | floats left of the desk, tips to the room | tries on shelves and hooks, "is this my head?" | stops hovering and sits on a hook by Ch6 |
| **Spare** (half-built Yo Bot) | one-armed chirping bot, lid crooked | sweeps the floor with the wrong arm, chirps at nothing | Wallit's pickup of the Lid (Ch3 favour) puts the lid on straight |
| **Chime** (bell) | small bell that rings when a lost thing is near | rings once at the door that is next; her ring is the hub's compass | errand giver; the ring sounds a clear note by the end |

Hub behaviour: bumping a lodger makes them react (Spare chirps, Brim tips, Tick glares); Wallit's idle barks name them.

### 2.4 Bots

Not one flavour of target: each bot has a **habit**, so a room reads before it is fought.

- **Yo Bots (the Clearing Crew):** they march in lines of three and **close their report as they go** ("Rush delivery!",
  "Filed as cleared!"). A crew is a patrol, not a spawn: it has a route and a checkpoint it salutes. They stop for the
  Courier and bow to a Foreman. They chirp louder when they see Slice; they chirp **off-key** in Ch6 (frightened).
- **Variants** (existing): Dispatch (rushes), Returned (splits), Reissue (tougher), Queue-Skip (blinks), Clerk (dodges), Sealed
  (juggernaut). Each one **is introduced by a place's story**, not by a timer: the Queue-Skip hops from a queue marker in
  Ch4, the Clerk dodges because it files pages in the Reading Room.
- **Spider Bots (process servers):** polite. They **stop and offer a paper** before striking. This is a gameplay rule in
  any room with one: standing still near a server means being served. Refuse (hit the paper) to clear it, or take it.
- **Hurt reactions:** every bot flinches in its own way (Yo Bots spin their lids, Spiders fold a leg). No sparks (J's rule);
  chunky plates and soft smoke.

### 2.5 Bosses (each is a puzzle with a personality)

| Boss | Where | Personality | The thing it does that you must read |
|---|---|---|---|
| The Courier | Ch1, the Sorting Yard | cheerful delivery, clipboard | lifts a notice, red mark fills: break it or stand in it for Slice |
| The Plated Alpha | Ch2, the Gate Hall | pompous, "INVALID" | stamps; use the stamp presses in the room to ruin its plate |
| The Foreman | Ch3, the Hook Hall | burnt out overtime | marches; pull the lever to stop the belt under it |
| The Server | Ch4, the Dais | courtesy | offers three times; third refusal is force |
| The Registrar | Ch5, the Gallery | reads everything aloud | pages; denies a page with the stamp to stop a leg |
| The Order-Bearer | Ch6, the Threshold | the form itself, blank | stamp marks and marches; sign it |

Each boss has two phases (`BossPhases.cs`, canon) and a **defeat line** (canon). Each also has an **arena that is already the
puzzle**: a thing in the room does half the work (the press, the lever, the paper stand, the lectern), so reading the room
beats trading blows.

## 3. Story told through play (not text cards)

Rule: if a beat can be a place, an object, an enemy behaviour or a choice, it is not a card. Cutscenes shrink to **short
reels at chapter open/close** (already built) plus in-world bark exchanges that never freeze the game for more than a screen.

### 3.1 Beat table (the story, played)

| Story beat | How the player experiences it |
|---|---|
| Wallit has no address | He walks past a row of identical doors; every one stamps him "RETURN TO SENDER" on contact (a red mark on the floor) and shoves him back. The first door that *does not* stamp him is the circle's. |
| The Department is a process | Rooms have **forms to be filled**: a desk with a stamp and a tray; sitting a thing in the tray opens a door. The process is the puzzle. |
| Slice's circle is the only filed address | A glowing ring that follows the story: in chapters where Slice stays, enemies stop at its rim until a mark fills. Step in the ring to heal; step out to be "uncounted" (the HUD pill reads OFF THE BOOKS and nothing you pick up counts). |
| Bots are the Clearing Crew | You find the **line they walk back to**: the bots in Ch1 return to a drop-off box; in Ch3 you see where they are made. |
| Cleared things exist | Pickups are **someone's** things (hat, clock, bell); each pickup shows a one-line tag. You can carry one in your hand and drop it on a belt, a desk, a shelf. |
| The order is unsigned | Ch5: every form on the wall has a blank signature line; you walk to one and **press E to read it** (the first time you do so, nothing else happens). Ch6: you sign. |
| Slice's past (Q2) | A locked drawer in Ch5 holds a Department badge with her face on it. Wallit reads the plate. Slice takes it from him without a word (a short exchange, no cutscene). Never explained further. |

### 3.2 Environmental storytelling checklist

Each level has **three storytellers**: a *scar* (evidence something happened), a *relic* (a thing to pick up that has a tag),
and a *sound* (a thing you hear that tells you what the place is). Examples:

- Ch1: a scar of burnt stamp ink on the stones; a relic mailbox flag; the sound of a clipboard clicking from the dark.
- Ch3: scars where chassis were taken off hooks; a relic cleared clock still ticking; the belt's sound stops at the lever.
- Ch5: scars of pages torn from the wall; a relic lectern with a bookmark; a clerk's pen scratching with no clerk.

### 3.3 Choices with consequences (beyond a later line)

`Choices.cs` already stores them. This spec asks that each choice be **a thing in the world**, not a card, and that each
change something the player can see:

- **Notice kept / torn (Ch1):** the player stands over the dropped notice; E picks it up, or a swing tears it. Kept: the
  notice is in Wallit's hand in Ch5 and can be stamped (visible item). Torn: the first form in Ch5 is a torn one on the floor.
- **Tip Slice (Ch1):** the first three bolts drop; hold E on Slice to hand one over. She says "Not mine." and, in Ch3, her ring
  has a small brass bolt set in it. Visible in the hub's ring forever.
- **Knock once or hard (Ch2):** a real door; knocking with the bat three times opens a hatch with a different pickup.
- **Take the Server's paper (Ch4):** take it and the Server skips an offer; Wallit carries a **blank-signature paper** into
  Ch5 that opens the Reading Room's side door (a shortcut to the Registrar's first lectern).
- **Foreman's sheet (Ch3):** swapping the hat-tag stalls the line **in the room**: the belt reverses for a minute and drops a
  cleared hat at Brim's favour.
- **Ending (Ch6):** hand the stamp to Slice (she presses it herself) and ask for one or two numbers: the final doorway in the
  hub shows one or two plaques.

Each choice is **asked in play** (a prompt at the object, keys 1/2 or E/F) with at most one line of response. No choice
changes the save format beyond the existing flag bits.

## 4. Chapters and the hub

Common to all: **a level is a route, not a ring.** Start at the circle's door, explore, talk, take a thing, fight small
encounters on the way, reach the boss room, return (or not) to the door that leads to the hub. Median level length 12 to 18
minutes first time. Wave timer: none. Held-wave fight: **only Chapter 6**, because the story calls for the circle itself to be
struck and held while the door is written (section 4.7). Every other chapter has no fight where the player stands and waits
for bots.

Every chapter below names: layout, objective, signature mechanic / set piece, mood and palette, music, and the difference
from Arcade a viewer can see at a glance.

### 4.1 Chapter 1: Return to Sender (the Sorting Yard)

- **Layout:** a moonlit dead-letter yard around the circle: a ring of **nine standing stones**, a fallen mailbox on a
  chalk stepping-stone trail, a low hedge maze of dead trunks to the west (Mist Bog), a **drop-off box** where Yo Bots return
  their reports. One loop route, 90 m across, three rooms: the Ring, the Trail, the Bog. Shut doors at the ends.
- **Objective:** find out why he was stamped. Pick up the Mailbox Flag (lost thing), read three notices pinned along the
  trail, and reach the Courier's drop. Then fight it.
- **Signature mechanic / set piece:** **The Stamp is a hazard that follows him.** Red RETURN TO SENDER marks fall on the
  floor at intervals. In the Ring they are harmless (Slice's circle exists); outside they shove Wallit back one step. He
  learns the circle is *the only place that does not push him out*.
- **Ending beat (pre-combat explore/talk/pickup):** before the first fight, Wallit walks the Trail, talks to Slice at the
  ring, picks up the Mailbox Flag. No bot is awake until the flag is lifted.
- **Mood / palette:** cold night. `#0B1226 #1E2F57 #C9D6FF #F2CC66 #C23B3B`. Gold ring, red ink.
- **Music:** low cello pedal; the Hollow breathes; a short bell on every stone he passes; Yo Bot pulse only inside an encounter.
- **Different from Arcade:** a trail with a destination, a hazard you read on the floor, a pickup, a talk; the fight is a
  boss with a puzzle and an encounter on the way, not a stand.

### 4.2 Chapter 2: No Fixed Address (the Gate Hall)

- **Layout:** a long chalk road with paired lamps to a **gate hall**: a corridor of turn-stile gates with stamp presses
  overhead. Two branches: the main hall and a **blank signpost loop** that returns to it.
- **Objective:** get through the gate. Locks need a key; keys are carried by **three Second Notices** (Plated Yo Bots) in
  separate rooms. Use the presses to ruin their plates.
- **Signature mechanic:** **stamp presses** (every 4 s on a red mark). Lure a Plated Yo Bot under one and it loses its plate
  (a 6-hit enemy becomes 2). Slice calls it: "Now." One press, one bot. Slice is a companion here and *stands on the safe
  mark you point her to* (Hold order).
- **Mood / palette:** `#0A1020 #243A5C #8FA3C9 #D8D1BE #C23B3B`. Cold blue, chalk white, red INVALID.
- **Music:** low drone with wind chime; a soft 70 bpm drum that doubles on each press.
- **Boss:** the Plated Alpha in the Gate Hall, stamped INVALID; the presses are its undoing.
- **Different from Arcade:** a hall with machinery you use against enemies, a companion that has a job, locked doors with
  keys you earn, the knock choice at the gate.

### 4.3 Chapter 3: The Clearing Floor (the Hook Hall)

- **Layout:** a sunken factory yard on **three levels**: the lip (hooks and chassis), the belt (a conveyor with flat cleared
  things), the lever stand. Ramps and stairs connect them. A chute with a red-lamp lens turns toward the circle.
- **Objective:** pull the lever at the far end; on the way, free or leave the Half-Built, take one cleared thing off the belt
  and carry it to Brim's favour.
- **Signature mechanic:** **the belt.** Cleared things ride it; Wallit can step on and ride, or knock bots onto it (it carries
  them to the crushers). Crushers slam in a rhythm; lure bots under (they die) or time your own run. The lever stops every belt
  and crusher for the rest of the level, including the ones you would have used.
- **Set piece (canon, kept):** the lever kills all sound for three seconds; only the cleared clock ticks.
- **Mood / palette:** `#1B120A #5C3B14 #E8A33D #C9C2B0 #C23B3B`. Amber factory light.
- **Music:** slow industrial pulse; steam breath on each crusher; the pulse cuts at the lever.
- **Boss:** the Foreman in the Hook Hall. Phase two is "overtime": a march and a page together. The belt is part of it.
- **Different from Arcade:** vertical space, a conveyor, hazards that kill bots and you, a choice that stalls the line.

### 4.4 Chapter 4: The Process Servers (the Long Walk)

- **Layout:** a **narrow causeway** of slabs over a pit of mist, six lamp stilts, three **lamp stops** (small round plazas)
  with a paper stand each, turning at the last. Falling off a slab is a respawn at the last lamp with a small fee ("That's a fee.").
- **Objective:** reach the Dais. At each lamp a Spider Bot is waiting with a paper. You do not need to fight them: you can
  refuse, take, or walk around (they step aside politely). Three lamps, one choice each.
- **Signature mechanic:** **service.** Standing near a Spider Bot makes it offer; a ring fills. Refuse by hitting the paper or
  step out; take it with E. The Server on the Dais works this same rule with three offers.
- **Mood / palette:** `#0A0F1A #2B3550 #9FB1D6 #E5E1D3 #7A5CC7`. Slate dusk, violet lamp heads.
- **Music:** three held notes, a polite harp figure that rises when an offer is out and stops when it is refused.
- **Boss:** the Server, plain Alpha after the third refusal.
- **Different from Arcade:** exposed, linear, no cover, enemies that wait instead of rush, hazards that are the level itself.

### 4.5 Chapter 5: Strike This Address (the Reading Room)

- **Layout:** a **round hall with wings**: a carpet aisle, five lecterns (each a small encounter room), the empty desk, the
  Registrar's Gallery that rises after the fifth page. A side door to the Closed Register back room.
- **Objective:** read five pages. Each lectern is an encounter and a puzzle: **press E at the lectern to start the page**, then
  clear the Clerks and Sealed Yo Bots that file it. The fifth opens the Gallery. Stamp the first page DENIED (the notice if kept).
- **Signature mechanic:** **the stamp.** From Ch5 Wallit carries the Department's stamp. Press B (Stamp Out) at a bot or page
  to deny it. Clerks dodge swings; the stamp does not miss them.
- **Mood / palette:** `#14110A #4A3F23 #F0E7C8 #E6B85C #8A1E1E`. Lamp-gold, paper white, forms in rows.
- **Music:** a held choir pad, no words; a dry wooden block marks each page; the pad warms as the walls go blank.
- **Boss:** the Registrar. Pages and stamps in phase two.
- **Different from Arcade:** a building you move through with five rooms, a tool that is a verb (deny), choices that unlock
  a side door, an environmental reveal of Slice's past.

### 4.6 Chapter 6: Address Filed (the Threshold) — the one held-wave fight

- **Layout:** the Hollow again, dawn-blue, the wall gone. A pale slab path runs to the **Threshold**, a white doorframe on a dais.
  The circle is Slice's, and this time the player **builds** around it before the order arrives.
- **Objective in three parts:** (1) **walk the path out** (no fighting: the first time the Hollow is empty), collect what
  the last chapters left (the lost things, the notice, the stamp); (2) **hold the ring**: the order stirs, **three** short rushes
  of Sealed Yo Bots strike at the ring while Slice writes the door (this is the only held fight in the campaign and the story
  needs it: the address cannot be written while it is being struck); (3) the Order-Bearer at the Threshold; sign it.
- **Signature mechanic:** **writing.** While Slice writes, a progress line draws on the ring. Anything that touches her stops it.
  Wallit defends the line, not a bar.
- **Mood / palette:** `#0E1730 #324C86 #E8EEFF #F2CC66 #FFFFFF`. Dawn, gold, white.
- **Music:** cello pedal from Ch1 with a third above it, a bright pulse only during the three rushes, a brass note at the seal.
- **Different from Arcade:** a calm walk first, a hold with a *purpose* and an on-screen artefact (the door number), a boss
  whose resolution is a signature.

### 4.7 The hub: the Office

- **Layout:** the office is a **walkable floor with four rooms** (the Ring, the Desk Room, the Hallway of doors, the Back Room
  with the chapter board), not one clamped box. Doors lead into the chapters; the door's room previews its place (the Ch3 door
  glows amber, Ch5's smells of paper). The back wall's brass door number appears as chapters are filed.
- **Objective:** none; the hub is the reward and the checkpoint. You can walk, talk, run errands, change gear (a bat mark,
  an ability loadout), replay a chapter (boss only or whole).
- **Signature mechanic:** **the board and the lodgers' changes.** What you did in a chapter reshapes the office (a hook for
  Brim, a lid on Spare, Tick's hour). Visible, not a menu.
- **Mood / palette:** warm lamp-light, `#2A2118 #7A5C2E #E8D9B0 #F2CC66`, softer than the chapters.
- **Music:** a short warm figure per lodger that layers in as you walk by them.
- **Different from Arcade:** it exists; Arcade has none.

## 5. Fun: loop, pacing, variety, rewards, difficulty, juice

### 5.1 The moment-to-moment loop (30 seconds)

**Read the room → choose a tool → act → collect → react.** Concretely: see the red mark or the waiting bot, decide
swing/smash/dodge/ability/use-the-room, land it, bolts and tags drop, Slice or Wallit says one line. The room is the
variable; the bat is constant and feels great.

### 5.2 Pacing curve per chapter (about 15 minutes)

| Minute | Beat |
|---|---|
| 0 to 2 | Arrive; quiet; a scar, a relic, a sound; first talk |
| 2 to 5 | First small encounter (3 to 4 bots, habit-led); first pickup |
| 5 to 8 | A puzzle that uses the room; a lodger-style aside from Slice; a choice |
| 8 to 11 | Second, harder encounter with a new variant; the signature set piece |
| 11 to 14 | The boss; two phases; the defeat line |
| 14 to 15 | Quiet return; Slice's line; the hub |

Rule: **no more than 90 seconds of continuous combat without a breath** (a talk, a pickup, a walk). Boss rooms have a
checkpoint at the door.

### 5.3 Variety

Each chapter adds **one verb** and retires nothing: Ch1 stamp-floor, Ch2 press, Ch3 belt/lever, Ch4 service (refuse/take),
Ch5 deny, Ch6 write. Each chapter adds **one bot habit**. No two chapters share an encounter shape (field, hall, three
levels, causeway, five rooms, ring).

### 5.4 Upgrades and rewards

Existing: XP levels (Spare Change, Refund, Receipt, Chargeback, Coin Toss, Stamp Out, Escrow), journal finds and goals,
back rooms, Refund in Full. New in the spec: **receipts**, one visible pickup per level room (a tag with a line), worth
XP and a lore line; **bat marks** from finds (gilt bands exist); **one gear slot** in the hub (bat mark or a coin charm that
changes a number, e.g. Corked Core variant of the swing); **ability pick** at levels 4+ (choose one of two). No loot tables.

### 5.5 Difficulty

Three **feel** settings in Settings, no menus in play: Gentle (bots 0.75 health, Slice 1.25), Standard, Hard (existing). Death in
a level is **a checkpoint at the room's door**, not a restart (it keeps pickups). Boss rooms checkpoint at the entrance.
Grades (S/A/B/C) stay, but time par is for the whole route, not for a stand.

### 5.6 Juice

- **Hit feedback:** hit-stop 0.05 s, bot flinch per type, plate chunks, soft smoke, "MISSED ME" on a dodge; no sparks, no
  camera shake except the bat's small jolt (existing, from `BatEquipment` only).
- **Camera:** `ZeldaStyleCamera` lock-on; in rooms with a ceiling the camera pulls out and tilts to keep Wallit visible;
  boss intros dolly with no shake. Letterbox on talk beats.
- **Sound:** every floor has a footstep tone (chalk, carpet, iron, slab); the bat's four sounds exist; each hazard has its
  own tell sound (press: low ring; belt: tick; lamp: a hum that rises). Voice lines duck effects (existing).
- **Numbers:** floaters in money ("+3", "-1 fee"), the streak counter, the combo's names.

## 6. Look: art direction, what stays primitive, what is modelled or generated, GPU cost

Style: night storybook, flat vertex-colour world lit by lamps, soft glows, chunky plates, the same palette discipline as
the menu key art (`Assets/Resources/UI/MenuArt.png`). Each chapter is one **hero colour** (ring gold, chalk blue,
factory amber, violet, paper gold, dawn white) on a dark base. Red is **reserved for what will hurt or what stamps**.

### 6.1 Characters

- Wallit, Slice, Yo Bot and Spider Bot models exist (`Assets/Art/*`, `Resources/Models/Bots`); do not regenerate.
- Bosses are today tinted and scaled Yo/Spider Bots. Keep that for slice 1 and add **one silhouette prop per boss** (Courier's
  clipboard and cap, Foreman's order board, Server's paper arm, Registrar's ledger, Order-Bearer's scroll): primitive props
  parented to bones first, modelled later.
- Lodgers are built from blocks (existing). Upgrade Tick, Brim, Spare, Chime to small modelled kits only if J approves GPU.

### 6.2 Places: what stays primitive and what needs a kit

| Stays primitive (vertex-colour blocks, no GPU) | Needs a modelled kit | Needs generated art |
|---|---|---|
| Ground skirts, walls, doors, hooks, stones, lamps, lecterns, forms on walls (flat quads), belts, slabs, mist | Stamp press, crusher, conveyor with a hat/clock/bell, Gate turnstile, lectern, desk, Great Ledger, doorframe, the mailbox | Chapter keyframes (open/close reels, replacing stand-ins), 6 skyboxes, the key props' decals (form text, door plaques), boss intro stills |

Wall decals (forms, plaques) are **text rendered in code** (no GPU).

### 6.3 GPU-cost estimate

`hawkesgen_estimate` returned "No access to project" for this agent's project ids, so **no price was obtained and none is
claimed here.** Nothing was generated or started. For J's approval the batch is:

| Item | Count | Tool |
|---|---|---|
| Chapter and ending keyframes (the stand-in reels' back/front layers) | about 24 | `generate_image` / `edit_image` |
| Skyboxes | 7 | `make_skybox` |
| Modelled kit pieces (section 6.2 middle column) | about 12 to 40 | `make_3d_model` |
| Music beds, one per chapter and the hub | 7 | `make_music` |

Voice and sound effects are cents each and not GPU. Ask Studio to run `hawkesgen_estimate` with a project it has access to
before approving; per-chapter slice 1 needs only its own 4 to 6 images, 1 skybox, 6 to 8 models and 1 bed.

## 7. Vertical slices

Build order. Each slice is **playable, reviewed on video against section 8, then the next**.

1. **Slice 1: Chapter 1, the Sorting Yard, ends on the pre-combat beat.**
   - A new scene `Level_1_Yard` with real geometry; `LevelScript` for it; Wallit and Slice placed; the stamp-floor hazard;
     the Mailbox Flag pickup and three notices; Slice's Hold/Come orders; **a talk at the ring, a pickup, an explore** all
     before any bot wakes.
   - Exits slice 1 when the Courier's drop is reached and the first encounter wakes (the Courier fight is slice 1b).
   - New runtime only: `LevelScript`, `Encounter` (wake on enter, route per bot), `Pickup`, `Prompt` (E/F). No `WaveDirector`.
2. **Slice 1b:** the Courier and the first encounter; choices 1.c1 and 1.c2 in the world.
3. **Slice 2: Chapter 2** with presses and the companion job. 4. **Slice 3: Chapter 3** with the belt and the lever.
5. **Slice 4: Chapter 4** with service. 6. **Slice 5: Chapter 5** with the stamp. 7. **Slice 6: Chapter 6** with the walk, the
   hold and the sign. 8. **Hub rebuild** into four rooms can happen after slice 2; do it before slice 4.

Each slice: its own `Level_*` scene in the build list (`EditorBuildSettings`, `SetupMainMenu.EnsureBuild`, `WebGlBuild.Build`),
its own scenario, a `Docs/Story.md` paragraph in the same change. Slice sizes: 1 is the one that proves the approach; if it
fails review, stop and revise this document before slice 2.

## 8. Acceptance criteria a reskin cannot pass

Every item is observable in a playtest event, metric or scene name.

1. **Different place, different scene.** Chapter N loads `Level_N_*` (event `level-scene` with name), not `scene2`. A frame
   dump of Chapter 1 and Chapter 3 shows different geometry, palette and camera height.
2. **No wave timer, no WaveDirector.** Log `wave-director: absent` and `phase: Chapter: absent`; no `PAGE n OF 5`-style
   wave counter, no "next wave" prompt. Grep gate: no `WaveDirector` or `Phase.Chapter` reference in `Level*.cs`.
3. **Explore, talk, pick up before combat.** In Chapter 1 the timeline shows `explore` (distance walked over 40 m), `talk`
   (a prompt used with Slice) and `pickup` (the Mailbox Flag) **before** the first `enemy-woke` event. Metric: `secondsBeforeFirstCombat` at least 60.
4. **Bots are placed, not spawned in waves.** Events `encounter-wake` with room id, bots in each is fixed by the level;
   bots that a player skips stay asleep (`encounter-skipped`).
5. **A hazard that is the place.** Per chapter, a `hazard` event whose cause is the level (press, belt, crusher, stamp floor,
   pit fall) and which damages a bot or Wallit at least once in a normal clear (`hazard-hit`).
6. **A signature verb used.** Per chapter one verb event (`verb-stamp-floor`, `verb-press-kill`, `verb-belt-ride`,
   `verb-service-refuse`, `verb-deny`, `verb-write`), required once.
7. **Characters expressed in play.** At least 12 idle/react barks across a chapter from at least 3 speakers, none during
   combat, none overlapping (`bark` events with speaker and `overlap: 0`). Slice's `hold` order obeyed once. A lodger
   reaction event in the hub.
8. **A choice shown in the world.** The chosen option changes an observable object (`choice-visible` with the object name),
   and a later chapter or the hub shows it (`choice-echo`).
9. **A boss whose room is the puzzle.** The boss event `boss-room-used` (press, lever, stand, lectern) before its death.
10. **Held fight limit.** Across the six chapters exactly one `held-fight` event (Chapter 6); any other is a failure.
11. **Checkpoints.** A forced death in a level resumes at the last room door with pickups kept (`checkpoint-resume`).
12. **Arcade unchanged.** The existing `smoke`, `run` and `bat` scenarios still pass.

### 8.1 The `campaign-beats` playtest scenario outline

Scenario id `campaign-beats` (Unity, `Playtest.cs`; use the playtest-scenario skill; sandboxed save via `CampaignSave.EnterSandbox`).

1. Menu → Campaign → file 1 → Begin. Wait for `level-scene` = `Level_1_Yard`. Fail if the active scene is `scene2`.
2. Skip the opening reel (Enter). Assert `wave-director: absent`.
3. Bot walks the trail (waypoints) for 45 s: log `explore` distance, `talk` with Slice at the ring (E), `pickup` Mailbox Flag.
   Assert no `enemy-woke` yet. Metric `secondsBeforeFirstCombat`.
4. Step onto an outside stamp mark: assert `hazard-hit` (pushed back). Step into the ring: assert no push.
5. Use Slice's Hold at a doorway and Come: `order-hold`, `order-come` events.
6. Enter the first encounter room: `encounter-wake`; kill the bots; assert a bark from at least two speakers with no overlap.
7. Make choice 1.c1 (keep the notice): assert `choice-visible` and the notice in Wallit's hand.
8. Courier boss: assert `boss-tell`, `boss-room-used`, `boss-phase`, defeat line; complete: `chapter-complete`.
9. Hub: walk into the door of Chapter 2: `level-scene` = `Level_2_Gate`; assert `choice-echo` for the tip if chosen.
10. Report fails on: wave-director present, no explore/talk/pickup before combat, any bark overlap, an error, or two chapters
    whose scene names or geometry hashes match.

Later slices extend the same scenario with one block each (press kill, belt ride, service refuse, deny, write).

## 9. Open questions for J

1. Real colliders in campaign levels (section 0), with authored routes and no pathfinding. Proposed: yes.
2. Slice's `Hold` and `Come` orders in all chapters, or only where she is a companion. Proposed: all.
3. Chapter 6 held fight as the only exception. Proposed: yes (story call: the address cannot be written while struck).
4. GPU approval for slice 1 only (4 to 6 images, 1 skybox, 6 to 8 models, 1 bed), after Studio prices it.

## 10. What is not changed here

No code, no scenes, no saves, no existing doc. Canon holds: Wallit money, Slice clipped, Yo Bots chirpy; no Warrok, bow,
birds or piano; the creature behind the order is never seen; no camera shake except the bat's jolt; no sparks.
