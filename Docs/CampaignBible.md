# The Place Department: Campaign bible

"Where things that should not exist are given an address."

> **Generated file.** The screenplay lives as data in `Assets/Resources/Story/campaign_script.json` (loaded by
> `CampaignScript.cs`, checked by the `bible` playtest). This page is rendered from it by `python3 Tools/Story/render_bible.py`
> from the repo root: edit the JSON (or `Tools/Story/bible_head.md` for this opening), then re-render. The opening and
> closing lines that exist in `Assets/Scripts/Story.cs` are canon; scenes and choices marked **proposed** await J and are not
> wired into `Story.Chapters` yet.

This is the Campaign told in full: six chapters and the secret ending, in the three case files. It builds on `Docs/Story.md`
(premise, circle, finale) and `Assets/Resources/Cast/cast.json` (speaker codes, names, colours) and changes none of them.
Arcade keeps its own thread; Campaign is the longer telling of the same story.

Line format is `Story.cs`'s: `<code>|<text>`. Codes: `W` Wallit, `S` Slice, `Y` Yo Bot, `M` Mega Yo Bot, `K` the Courier, `P` the Plated
Alpha, `F` the Foreman, `H` the Half-Built, `V` the Server, `R` the Registrar, `O` the Order-Bearer, `N` narration.

## Voices and rules

- **Wallit** talks money and is brash. He never apologizes or admits fear; he converts it to money.
- **Slice** is clipped, a few words, never a joke. She never explains herself fully.
- **Yo Bots** are chirpy and read their orders as cheerful notices. They never understand what they say.
- No piano, no office jokes, no bow, no birds, and the retired bow-era cast does not appear. The Department is a thing that files.
- The Courier, the Stamp and RECEIVED stay. A chapter is one stamp: Chapter 1 RETURN TO SENDER, 2 INVALID, 3 REISSUED, 4 SERVED,
  5 DENIED, 6 ADDRESS FILED, and the ending RECEIVED.
- The creature behind the order is never seen, named or quoted.
- No camera shake in any cutscene. The playfield (150 m round the circle) stays free of solid objects.

## Story spine

**Logline.** A wallet with a bat is stamped "Return to sender", finds no sender, and ends up inside the one filed address in a
hollow where unfiled things are cleared. He defends it for the guard who keeps it, and learns the order against the address
was never about him.

1. Wallit is thrown out with no address. Slice offers him the circle: inside it he exists. [canon]
2. He looks for his sender. There is none; the Hollow ends at a wall the Department built. [canon]
3. The bots are the Department's clearing crew, working to a schedule, stamped out on the Clearing Floor. [canon]
4. The Spider Bots are process servers: they walk to an address and wait while the order is read. The order was aimed at
   Slice's circle before Wallit came. [canon]
5. The order is a form nobody signed. It can be denied. [canon]
6. The Order-Bearer falls; Wallit reads "Strike this address", stamped DENIED; Slice files the circle. ADDRESS FILED. [canon]
7. Ending: with all six lost things, the stamp prints ADDRESS and nothing strikes it. RECEIVED. [canon]

**Where the bots come from.** Yo Bots are the Clearing Crew; the bolts they drop are the Floor's own pay, so Wallit is collecting
what is owed. Plated and Mega Yo Bots are the Floor sending the same unit again, heavier. Spider Bots are process servers.

## Sources read

`Docs/Story.md`, `Assets/Scripts/Story.cs` (chapter data, cutscenes, the finale, the secret ending), `Assets/Resources/Cast/cast.json`,
`ChapterMap.cs` (map looks and layouts), `Courier.cs`, `Server.cs`, `BossPhases.cs`, `AGENTS.md`.

## Character arcs

### Wallit

- Start: Wants a refund and a sender to bill.
- Turns:
  - Ch1: takes the stamp on himself for Slice
  - Ch2: finds the postmark that is half his
  - Ch3: sees he could be one of the cleared things
  - Ch4: has no joke when Slice says before you
  - Ch5: uses the Department's stamp against it
  - Ch6: reads the order and says DENIED
- End: Chooses an address for others, not for himself: he gives the stamp away.

### Slice

- Start: Stay in the circle. Explains nothing.
- Turns:
  - Ch1: names what pays him
  - Ch2: leaves the ring to follow him, staked
  - Ch3: her longest beat is It isn't
  - Ch4: says the order was aimed at her before him
  - Ch5: lets him use the stamp, asks only whether he has the nerve
  - Ch6: files the circle herself
- End: Admits she needs him, in one word, and writes the door number.

### The Courier

- Start: A cheerful delivery with a clipboard.
- Turns:
  - Ch1: reads the notice, lands it, is broken
  - Ch5 (flag notice_kept): its notice is the page Wallit stamps first
  - Ending: its clipboard is one of the things set down in the ring
- End: Its last line is a question it cannot answer: defeat?

### The Foreman

- Start: A Mega Yo Bot that reads the order on its charges.
- Turns:
  - Ch3: overtime, the only one who understands the shift is a debt
  - Ch3 choice: can be made to read the order wrong
- End: Order... unread.

### The Server

- Start: A Spider Bot holding out a paper like a courtesy.
- Turns:
  - Ch4: offers, waits, offers
  - refused three times it strikes, because that is the rule, not the want
- End: Delivery... undelivered... chirp?

### The Department

- Start: Stamp, wall, floor, paper.
- Turns:
  - see department.arc
- End: Denied by its own stamp.

### The Department (summary)

A process with no face. Ch1 it only stamps. Ch2 it answers from behind a wall. Ch3 it is a floor that makes crews. Ch4 it is politeness: it serves, it waits. Ch5 it is a form nobody signed. Ch6 it is denied. Ending: a thing it cannot strike is given a door. It never hates anyone; it only clears. The creature behind the order is never seen, named or quoted.

## Choice flags

| Flag | Set by | Read by |
|---|---|---|
| `notice_kept` | Ch1 choice 1: keep the Courier's notice | Ch5 The Stamp, Reversed; Ending |
| `tipped_slice` | Ch1 choice 2: pay Slice the first bolt | Ch3 The Belt; Ch6 Filed |
| `knocked_twice` | Ch2 choice 1: knock twice at the gate | Ch2 Done card; Ch4 Served |
| `left_ring` | Ch2 choice 2: wait for Slice at the ring vs go on ahead | Ch2 boss lines |
| `foreman_misread` | Ch3 choice 1: hand the Foreman the wrong sheet | Ch3 Foreman fight (phase two lines) |
| `paper_taken` | Ch4 choice 1: Wallit takes the paper instead of refusing it | Ch4 Server fight; Ch5 reading |
| `slice_answered` | Ch4 choice 2: let Slice answer the Server herself | Ch6 Filed |
| `page_read_aloud` | Ch5 choice 1: read the first page aloud | Ch5 Registrar lines |
| `stamp_shared` | Ch6 choice 1: hand Slice the stamp | Ending |
| `door_for_both` | Ch6 choice 2: ask for two door numbers | Ending |

## Chapter 1: Return to Sender (stamp: RETURN TO SENDER)

Status: built; text extended here as proposed. Emotion: Alone, then not alone. Anger turning into a first debt.

### Script

**Scene 1.1: The Stamp** (The Moonlit Hollow, frozen) [canon]  
*The stamp falls on leather; a wallet rolls into the ring.*

> **NARRATION:** Out of the dark comes a stamp: RETURN TO SENDER. Its ink is the only bright thing in the Hollow.  
> **WALLIT:** Ow! That's extra!  
> **SLICE:** You're inside. Inside, you exist.  
> **WALLIT:** Does existing come with a receipt?  
> **SLICE:** No.  
> **WALLIT:** Where do I sign for this bat?  
> **SLICE:** Swing it.  
> **YO BOT:** Hello! Uploading victory! Striking this address now!  
> **WALLIT:** Pay up, bots.  

**Scene 1.2: Six Deliveries** (The circle) [proposed]  
*The first wave: move, bat, lock-on. Bolts drop.*

> **YO BOT:** Rush delivery! Rush delivery!  
> **WALLIT:** They are fast, I'll give them that. Pay up.  
> **SLICE:** Lock on. Shift.  
> **WALLIT:** I know how to hit a thing, Slice.  
> **SLICE:** Then hit it.  
> **WALLIT:** Bolts. Somebody's paying me.  
> **SLICE:** Not somebody. Something.  

**Scene 1.3: The Notice** (The circle, dusk) [canon]  
*The Courier arrives with a clipboard and reads the order out.*

> **THE COURIER:** Special delivery! Notice of striking!  
> **SLICE:** The Courier. It reads the order out, then it lands.  
> **WALLIT:** Then it can read me the bill.  
> **SLICE:** Red is where it lands. Move, or break it.  

**Scene 1.4: Notice Undelivered** (The circle) [canon + proposed (last line)]  
*The Courier falls. The notice floats down, unsigned.*

> **THE COURIER:** Notice... undelivered... uploading... defeat?  
> **WALLIT:** Return to sender.  
> **SLICE:** Clean.  
> **NARRATION:** The notice drifts down. Nobody has signed it.  

### Scripted set piece: The Courier's Notice [built (Courier.cs; the mailbox flag answers it in HollowMap; the drop's boss in the Sorting Yard, LevelYard.cs)]

The Courier lifts the notice; a red mark fills on the circle's ground over two seconds. Break the wind-up with a tenth of its health, or stand in the mark and take the stamp yourself. Wallit takes the stamp: Slice is spared.

In the Sorting Yard (Chapter 1's level) it comes after the Clearing Crew: the drop box rattles and it steps out from behind it, reads its arrival lines, and walks at Wallit. The mark fills under Slice as she walks with him (the first notice over three seconds); if it lands she goes down until Wallit revives her, and while she is down the mark is under Wallit. Under half health it calls two Yo Bots with each notice. It falls, its notice drifts down unsigned and the return slot opens: E on the notice is choice 1.c1 keep; walking out without it is tear.

### Choices

**1.c1** at 1.4 Notice Undelivered: The notice lies on the grass. What does Wallit do?
- *keep* (Pocket it. 'Evidence.') sets `notice_kept`. Now: **WALLIT:** Evidence. Every bill has a paper trail.. Later: Ch5 The Stamp, Reversed: Wallit presses the stamp to this notice first and Slice says 'You kept it.' W: 'I keep receipts.'
- *tear* (Tear it. 'Void.'). Now: **WALLIT:** Void. I don't take bills I didn't order.. Later: Ch5: Wallit has to pull a page off the wall instead; Slice: 'You tore the first one. We take another.'

**1.c2** at 1.2 Six Deliveries: First bolts in hand. Slice says nothing. Wallit decides.
- *tip* (Hold one out to Slice. 'Your cut.') sets `tipped_slice`. Now: **SLICE:** Not mine.. Later: Ch3 The Belt: Slice says 'It isn't' and adds 'You tipped. Cleared things don't tip.' Ch6: she files the bolt into the door.
- *keep* (Keep every bolt. 'Overhead.'). Now: **WALLIT:** Overhead. Don't look at me like that.. Later: Ch3 The Belt: Slice says only 'It isn't.' The line lands drier.

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | Black. Far above, a hand-cut stamp turns, RETURN TO SENDER inked in red. | Static, tilt down 40 degrees over four seconds | NARRATION | Out of the dark comes a stamp: RETURN TO SENDER. Its ink is the only bright thing in the Hollow. | Low cello pedal, no melody; one cold bell |
| 2 | The stamp falls onto brown leather; ink flashes; the Hollow's grass takes the wallet's roll. | Follow the fall, hard cut to ground level at the landing | WALLIT | Ow! That's extra! | The pedal breaks into a short run of strings |
| 3 | The circle's ring lights around him in pale gold, one stone at a time. | Slow crane up to a top-down of the ring | NARRATION | (none) | Held chord, bell on each stone |
| 4 | Slice, standing in the ring, knife down, face in shadow. | Low angle, push in on her eyes | SLICE | You're inside. Inside, you exist. | Strings drop out; room tone only |
| 5 | Wallit's wallet-face, looking up, bills falling out of his pocket. | Over-the-shoulder from Slice | WALLIT | Does existing come with a receipt? | Silence, then one dry plucked note |
| 6 | Wide: a Yo Bot crests the hill, chirping, behind them the whole Hollow. | Pull back to a wide, Wallit and Slice small in the ring | YO BOT | Hello! Uploading victory! Striking this address now! | Pulse enters: bright, thin, unfriendly |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Courier, split, on one knee; its clipboard falls flat to the grass. | Locked wide, 2 s hold | THE COURIER | Notice... undelivered... uploading... defeat? | Pulse cuts. A single dying chirp |
| 2 | Wallit stands over the clipboard, bat on his shoulder; the notice drifts down beside him. | Medium, handheld sway removed (no shake), slow orbit 90 degrees | WALLIT | Return to sender. | Cello pedal returns, warmer |
| 3 | Slice's knife is lowered, then re-raised to the dark. | Cut to her hand and blade, then her face | SLICE | Clean. | Bell, one note |
| 4 | Wallit gathers three bolts into his coin slot, looking at them. | Close on his hands | WALLIT | Bolts. Somebody's paying me. | Held string, rising a half step |
| 5 | Slice, eyes on the far dark past the Hollow's wall. | Push in on her profile, then past her to the wall | SLICE | Not somebody. Something. | Pedal falls to a low drone; hold |

### Map design sheet: hollow

- **Setting:** The Moonlit Hollow: a grassy bowl under a cold moon with the circle at its centre, the wall of the Department a long way out at 205 m.
- **Landmarks:** The circle: nine standing stones around a pale ring; Fallen Mailbox with a red RETURN TO SENDER stamp on its flag; Mist Bog of dead trunks to the west
- **Route:** Stepping-stone trail at bearing 20 out to the Fallen Mailbox; the fight stays at the circle.
- **Hazards:** Mist Bog: slows sightlines to 25 m, nothing solid; The Courier's red notice mark on the circle's ground
- **Boss arena:** The circle itself. The Courier walks in from the Mailbox side to 7.5 m of Slice.
- **Palette:** `#0B1226` `#1E2F57` `#C9D6FF` `#F2CC66` `#C23B3B`
- **Props:** standing stones (9); fallen mailbox; dead trunks; chalk stepping stones; lantern posts (4); the red stamp ink splash

## Chapter 2: No Fixed Address (stamp: INVALID)

Status: built; text extended here as proposed. Emotion: Hope against a wall. A thing that was his, torn in half.

### Script

**Scene 2.1: A Door With No Name** (The Hollow's last lantern) [canon]  
*Wallit sees the gate and goes for it.*

> **WALLIT:** There's a door. Doors have senders.  
> **SLICE:** Doors have locks. Come back.  
> **WALLIT:** Locks have keys. Keys have owners. Owners get paid.  
> **SLICE:** Seventy metres. Then back.  
> **YO BOT:** Address unknown! Deleting!  
> **WALLIT:** Cover me, Slice. I'm collecting a refund.  
> **SLICE:** Ring's staked. I'm coming.  

**Scene 2.2: The Road** (Chalk road between paired lamps) [canon + proposed]  
*Slice walks one step behind. Bots come. The Second Notice.*

> **SLICE:** Left.  
> **WALLIT:** I saw it.  
> **SLICE:** You saw it late.  
> **YO BOT:** Second notice! This address is still unknown!  
> **SLICE:** It repeats. It does not give up.  
> **WALLIT:** Neither do I. Pay up.  

**Scene 2.3: The Gate** (The gate) [canon]  
*He knocks. The wall answers with the same stamp. A torn postmark: half of it his.*

> **WALLIT:** Hello? Sender? I'd like a word, and a receipt.  
> **NARRATION:** The wall answers. From the other side, pressed through the dark, the same stamp: RETURN TO SENDER.  
> **WALLIT:** Nothing. Not even a doormat. I'd say that's rude, but I'm the one with no address.  
> **NARRATION:** At the foot of the gate, a postmark is torn into the stone. Half of it is his.  
> **WALLIT:** That's my stamp. Torn.  
> **SLICE:** Wallit. Come back.  

**Scene 2.4: Plate Stamped Invalid** (The circle) [canon]  
*Three waves, then the Plated Alpha.*

> **THE PLATED ALPHA:** Notice! This plate is stamped: INVALID!  
> **SLICE:** The Plated Alpha. Plate says invalid.  
> **WALLIT:** So does my refund. Pay up.  
> **THE PLATED ALPHA:** Plate... voided... uploading... nothing?  
> **WALLIT:** Void your own stamp.  
> **SLICE:** Clean.  
> **SLICE:** You have an address. The circle.  
> **WALLIT:** Leased. By you.  
> **SLICE:** Free.  
> **WALLIT:** Nothing's free.  
> **SLICE:** Then pay with swings.  

### Scripted set piece: The Stamp Press [built (EdgeMap in OwnMapChapters.cs: wakes as Wallit nears, lights its mark, slams, leaves a print)]

Beside the chalk road a Department stamp press drops its plate on a red INVALID mark every four seconds, flat and telegraphed. Wallit can lure bots under it. Slice calls the timing: 'Now.' One press, one Yo Bot.

### Choices

**2.c1** at 2.3 The Gate: The wall answers. How does Wallit answer back?
- *once* (Knock once. Wait.). Now: **NARRATION:** Nothing answers a polite knock.. Later: Ch2 Done card is the canon one.
- *twice* (Knock twice, then a third time, hard, with the bat.) sets `knocked_twice`. Now: **WALLIT:** Open up. I know you're in there. It's a stamp, not a person.. Later: Ch2 Done card gains S: 'You knock like you're owed.' W: 'I am.' Ch4 Served: the Server notes the knocks: 'Three knocks on record.'

**2.c2** at 2.1 A Door With No Name: Slice stakes the ring and says she is coming.
- *wait* (Wait for her to catch up.). Now: **SLICE:** Good.. Later: Ch2 boss lines: Slice stays within a step; the Plated Alpha stamps once before phase two.
- *ahead* (Go on ahead. 'Keep up.') sets `left_ring`. Now: **SLICE:** I am.. Later: Ch2 boss lines: Slice is down once earlier (revive E hold) and says 'Noted.' with a hard edge.

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Hollow's edge: a last lantern guttering; beyond it blue dark and a thin gate. | Slow dolly out from the lantern to a wide | NARRATION | Past the last lantern the Hollow runs out. Far off, a gate stands in the dark with nothing written on it. | Low drone, distant wind chime |
| 2 | Wallit, hands on hips, bat leaning on his shoulder, looking to the gate. | Medium, slight low angle | WALLIT | There's a door. Doors have senders. | Drone holds; a small hopeful two-note figure |
| 3 | Slice, one hand on the ring's last stone. | Cut to profile, shallow focus | SLICE | Doors have locks. Come back. | Figure ends; held low note |
| 4 | Close on Wallit's boots stepping over the ring's edge. | Low tracking shot | WALLIT | Locks have keys. Keys have owners. Owners get paid. | Pace enters: soft low drum, 70 bpm |
| 5 | Slice drives her knife into the ground; the ring's light jumps to a second ring. | Close on blade then crane up | SLICE | Ring's staked. I'm coming. | Drum doubles; strings on the beat |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Plated Alpha, plate cracked down the middle; INVALID splits into IN and VALID. | Medium, slow push | THE PLATED ALPHA | Plate... voided... uploading... nothing? | Drum stops; a clipped metallic ring |
| 2 | Wallit kicks the plate flat. | Over-the-shoulder, 2 s hold | WALLIT | Void your own stamp. | Silence |
| 3 | Back at the ring; Slice pulls the staked knife and the second ring folds into the first. | Wide, rings merging | SLICE | You have an address. The circle. | Warm bell, one note |
| 4 | Wallit looks at the torn half-postmark in his hand. | Close on the stone chip | WALLIT | Leased. By you. | Strings reply under him |
| 5 | The two stand shoulder to shoulder at the ring, looking back at the dark gate. | Pull back to a wide, locked | SLICE | Then pay with swings. | Resolving chord; fade on the drone |

### Map design sheet: hollow-edge

- **Setting:** The edge of the Hollow: the Department's wall at 205 m, a gate with no handle, a signpost with nothing on it.
- **Landmarks:** The gate with no handle (70 m, bearing 150); The blank signpost; Slice's radius: a red ring painted on the grass
- **Route:** Chalk road with paired lamps from the circle to the gate, then back along the same chalk.
- **Hazards:** The stamp press, its plate falling on a red INVALID mark every four seconds; Bots arrive the whole way out
- **Boss arena:** The circle again; the Plated Alpha walks in from the gate side.
- **Palette:** `#0A1020` `#243A5C` `#8FA3C9` `#D8D1BE` `#C23B3B`
- **Props:** gate and wall (flat blocks, no colliders); blank signpost; paired lamps (10); stamp press; chalk road tiles; torn postmark stone

## Chapter 3: The Clearing Floor (stamp: REISSUED)

Status: built; text extended here as proposed. Emotion: Recognition. The enemy is a factory, and Wallit sees himself on its belt.

### Script

**Scene 3.1: Past the Broken Gate** (The yard's lip) [canon]  
*A sunken yard of hooks and unfinished chassis.*

> **NARRATION:** Past the broken gate the ground drops away into a yard lit by lamps. Rows of Yo Bot chassis hang on hooks.  
> **THE HALF-BUILT:** Hello! I am... not finished! Uploading victory?  
> **WALLIT:** They're all the same unit.  
> **SLICE:** Spare parts. Spare orders.  

**Scene 3.2: The Belt** (The belt) [canon]  
*Cleared things ride past: a hat, a clock, a small bell.*

> **NARRATION:** A slow belt carries flat, cleared things past the hooks: a hat, a clock, a small bell.  
> **WALLIT:** Cleared things. Somebody's hat. Somebody's clock. That could be me.  
> **SLICE:** It isn't.  
> **NARRATION:** The belt runs on into a chute lit by a single red lamp. The lamp is a lens. It turns toward the Hollow, and the circle blinks once.  
> **WALLIT:** It's watching the circle.  
> **SLICE:** Pull the lever. The belt stops. The Crew stops.  

**Scene 3.3: The Lever** (The lever, 105 m out) [canon + proposed]  
*He pulls it. The hooks swing empty.*

> **WALLIT:** One lever. No sign. Somebody trusts the floor.  
> **WALLIT:** Those bolts are the same ones they've been paying me.  
> **SLICE:** Then we collect.  
> **NARRATION:** The hooks swing empty. The belt stops with a cleared clock still on it, ticking.  

**Scene 3.4: Order, Read Aloud** (The circle) [canon]  
*The last batch, then the Foreman, who reads the order.*

> **THE FOREMAN:** Order, read aloud: STRIKE. THIS. ADDRESS.  
> **WALLIT:** It reads it like a bill of lading.  
> **THE FOREMAN:** Order... unread...  
> **WALLIT:** Paid in full.  
> **SLICE:** Clean.  

### Scripted set piece: The Belt Stops [built (FloorMap in OwnMapChapters.cs: the lever halts belts and crushers, dims the lamps)]

At the lever the whole yard's sound cuts at once: conveyors, lamps, chirps. For three seconds only the cleared clock ticks. Then the Half-Built, hanging by one hook, asks: 'Finish me?' Slice: 'Don't.' Wallit: 'Wasn't going to. Not for free.'

### Choices

**3.c1** at 3.4 Order, Read Aloud: The Foreman holds out the order sheet to be read. Wallit has a second sheet in the cleared pile.
- *true* (Let it read the true order.). Now: **THE FOREMAN:** STRIKE. THIS. ADDRESS.. Later: Foreman's phase two is the canon overtime: march plus page together.
- *wrong* (Swap in the hat-tag from the belt. 'Read this one.') sets `foreman_misread`. Now: **THE FOREMAN:** Order, read aloud: ONE. HAT. BLUE. Uploading... that is not the order?. Later: Foreman phase two begins with a stalled line and a shorter march; Slice: 'It reads what it is handed.'

**3.c2** at 3.3 The Lever: The Half-Built hangs by one hook and wants finishing.
- *leave* (Leave it on the hook.). Now: **WALLIT:** Not for free.. Later: Ch5: the Clerk's line 'Page turned!' has no echo.
- *unhook* (Cut its hook. Let it drop.). Now: **THE HALF-BUILT:** Hello! Thank you? Uploading... I do not know what.. Later: Ending: a half-built Yo Bot is among the things given a door; its line is 'Hello! I am... received?'

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The lip of a sunken yard lit amber; hooks hang in rows like a ribcage. | Crane over the lip, slow tilt down | NARRATION | Past the broken gate the ground drops away into a yard lit by lamps. Rows of Yo Bot chassis hang on hooks. | Slow industrial pulse; steam breath |
| 2 | A half-built Yo Bot twitches on a hook, one eye lit. | Close, rack focus from Wallit to the bot | THE HALF-BUILT | Hello! I am... not finished! Uploading victory? | Pulse skips a beat, thin chirp |
| 3 | Wallit studies the rows: every chassis the same. | Slow pan across the hooks, matching frames | WALLIT | They're all the same unit. | Pulse steadies |
| 4 | Slice at his shoulder, reading the yard's lamps. | Two-shot, still | SLICE | Spare parts. Spare orders. | Low drone under the pulse |
| 5 | Wide: the belt visible far off, glowing. | Pull back and up to reveal the yard's scale | NARRATION | (none) | Brass swell, then cut |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Foreman on its knees, the order sheet fluttering off its chest. | Medium, low | THE FOREMAN | Order... unread... | Pulse stops; a last factory sigh |
| 2 | Wallit, bat on shoulder, lets a bolt tumble from the Foreman's seam into his palm. | Close on hand | WALLIT | Paid in full. | Quiet bell |
| 3 | Slice looks at the dead belt, the red lamp dark. | Push in on her eyes in the lamp's last glow | SLICE | Clean. | Drone only |
| 4 | The red lamp, once more, turns a degree toward the Hollow, and blinks. | Cut to the lamp lens, 2 s hold | NARRATION | (none) | One cold bell; cut to silence |

### Map design sheet: clearing-floor

- **Setting:** A sunken yard beyond the broken gate, lit by conveyor lamps: rows of un-built chassis on hooks and a slow belt of flat, cleared things.
- **Landmarks:** The broken gate (36 m, bearing 200); The belt (70 m); The lever (105 m); The red-lamp chute
- **Route:** Lit yard path through three stops, then back along the same path to the circle.
- **Hazards:** Three crushers slamming in turn along the route; Yo Bots arriving the whole way
- **Boss arena:** The circle; the Foreman walks out of the hooks' end, to the south of the yard.
- **Palette:** `#1B120A` `#5C3B14` `#E8A33D` `#C9C2B0` `#C23B3B`
- **Props:** hook gantries (6) with chassis; belts (2) with hat, clock and bell; crushers (3); red-lamp chute; lever stand; sorting bins

## Chapter 4: The Process Servers (stamp: SERVED)

Status: built; text extended here as proposed. Emotion: Dread and politeness. The cruelty is the courtesy.

### Script

**Scene 4.1: The Long Walk** (A causeway of stone slabs over a pit of mist) [canon]  
*Six lamps, a single path, no cover.*

> **YO BOT:** Spiders en route! Chirp!  
> **SLICE:** Mind the legs.  
> **WALLIT:** They're not bigger Yo Bots. They're... deliveries.  
> **SLICE:** Process servers. They stand and wait while it's read.  

**Scene 4.2: Served** (The first lamp) [canon]  
*A Spider Bot offers a paper on the tip of a claw.*

> **WALLIT:** Read what?  
> **SLICE:** My address. Struck.  
> **WALLIT:** Since when?  
> **SLICE:** Before you.  
> **NARRATION:** The Spider Bot folds one leg and holds out a paper. It waits.  

**Scene 4.3: Refusals** (The circle) [canon]  
*The Server offers three times. Each is refused with a blow.*

> **THE SERVER:** Please accept this delivery.  
> **WALLIT:** Refused.  
> **THE SERVER:** Please accept this delivery.  
> **WALLIT:** Refused. Still refused.  
> **THE SERVER:** This is your third offer. After this, service is by force.  
> **SLICE:** It's polite. That's the worst of it.  

**Scene 4.4: Undelivered** (The circle) [canon]  
*It strikes after the third; it falls.*

> **THE SERVER:** Delivery... undelivered... chirp?  
> **WALLIT:** Return to sender.  
> **SLICE:** Clean.  

### Scripted set piece: The Paper Is Out [built (Server.cs)]

The Server stops a step short of the circle, folds a leg and holds the paper out. It will not strike while the paper is out. Wallit refuses it by landing a blow. It backs off and offers again, three times. Its health cannot fall below 70% until the third refusal.

### Choices

**4.c1** at 4.3 Refusals: The paper is out. Taking it ends the offer.
- *refuse* (Refuse it. Strike the Server's claw.). Now: **WALLIT:** Refused.. Later: Canon fight: three refusals, then force.
- *take* (Take the paper and read it.) sets `paper_taken`. Now: **NARRATION:** The paper reads: STRIKE THIS ADDRESS. No signature. The Server's leg unfolds.. Later: The Server skips the second offer and winds up at once but with a shorter march; Ch5 the reading opens with Wallit already knowing the order is unsigned: W: 'I've seen it. I don't need to be told.'

**4.c2** at 4.2 Served: The Server's paper is for Slice. Who answers?
- *wallit* (Wallit steps between: 'Talk to me.'). Now: **THE SERVER:** Delivery is to the named address.. Later: Canon: Slice stays at the circle.
- *slice* (Slice answers herself: 'Refused.') sets `slice_answered`. Now: **SLICE:** Refused. Mine to refuse.. Later: Ch6 Filed: Slice writes the door number without being asked; Wallit: 'You answered it first.'

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | A causeway of pale slabs runs out over a pit of mist; paired lamps like six folded legs. | Long lateral track along the parapet | NARRATION | (none) | Three held notes, string harmonics |
| 2 | Far off a Spider Bot lifts a foreleg and tilts its glass toward them. | Long lens, shallow depth | YO BOT | Spiders en route! Chirp! | A single bright chirp, then silence |
| 3 | Slice, eyes on the bot. | Close profile | SLICE | Mind the legs. | Harmonics hold |
| 4 | Wallit hefts the bat, watching the walk of the legs. | Medium, slight handheld sway off (no shake): locked | WALLIT | They're not bigger Yo Bots. They're... deliveries. | Low hum underneath |
| 5 | The Server stops, folds one leg, offers a paper on its claw tip. | Push in on the paper | SLICE | Process servers. They stand and wait while it's read. | A polite minor figure on harp, nothing else |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Server stands in pieces on a lamp stand, its paper fluttering to the pit. | Wide, then tilt down with the paper | THE SERVER | Delivery... undelivered... chirp? | Harp figure stops mid-phrase |
| 2 | Wallit watches the paper vanish in the mist. | Over-the-shoulder, hold | WALLIT | Return to sender. | Silence, then one low bell |
| 3 | Slice, blade lowered, looks along the causeway where the next lamp is lit. | Slow push to her face | SLICE | Clean. | Harp resumes, a half step higher |
| 4 | The next lamp, six legs of light, glows on the far side. | Locked wide, fade on the lamp | NARRATION | (none) | Harmonics, fade |

### Map design sheet: long-walk

- **Setting:** The Long Walk: a causeway of stone slabs over a pit of mist, with six lamp stilts on either side, a single path, no cover.
- **Landmarks:** Lamp 1 (30 m); Lamp 2 (62 m); Lamp 3 (95 m), where the way turns round; The Servers' Dais (78 m, bearing 150)
- **Route:** Causeway at bearing 330 through three lamp stops, turning back at the last.
- **Hazards:** The pit with the last slabs fallen in; Servers appear only at lamps
- **Boss arena:** The Servers' Dais at bearing 150, with a paper stand where the Server waits.
- **Palette:** `#0A0F1A` `#2B3550` `#9FB1D6` `#E5E1D3` `#7A5CC7`
- **Props:** slab causeway; parapets; lamp stilts (12); mist pit; paper stand; folded-leg lamp heads

## Chapter 5: Strike This Address (stamp: DENIED)

Status: built; text extended here as proposed. Emotion: Cold clarity, then nerve. The order has no author, so it has no power.

### Script

**Scene 5.1: The Reading Room** (A bare round hall) [canon]  
*The circle drawn on the floor at the Hollow's scale; forms on every wall.*

> **MEGA YO BOT:** Order, read aloud: STRIKE. THIS. ADDRESS.  
> **SLICE:** It's reading it out.  
> **WALLIT:** Then I'm reading mine. Pay up.  
> **WALLIT:** It's not signed.  
> **SLICE:** Nobody signs it. That's how it works.  

**Scene 5.2: The Stamp, Reversed** (The desk) [canon]  
*Wallit presses the Department's own stamp to the first page.*

> **WALLIT:** Then it can be denied.  
> **SLICE:** Denied by whom?  
> **WALLIT:** By the guy with the stamp.  
> **SLICE:** Wallit. That's a lot of nerve.  
> **WALLIT:** Nerve's free. The swing's extra.  
> **NARRATION:** The ink shows nothing, then DENIED. All the forms on the walls turn over at once, blank.  

**Scene 5.3: Five Pages** (The circle) [canon + proposed]  
*Five waves, one per page. The Clerk files what it cannot read.*

> **YO BOT:** Page turned!  
> **SLICE:** One.  
> **WALLIT:** Two. Three. Don't count with me.  
> **SLICE:** Four.  
> **WALLIT:** Five. I read it.  

**Scene 5.4: The Ledger** (The Registrar's Gallery) [canon]  
*The Registrar reads every page with every leg.*

> **THE REGISTRAR:** Register... closed... uploading... nothing?  
> **WALLIT:** Struck from the record.  
> **SLICE:** Clean.  
> **WALLIT:** Then we answer it. Pay up.  

### Scripted set piece: The Forms Turn [built (BuildReadingRoom, PageRead)]

As each page's wave falls, that page's section of forms on the wall turns blank. At the fifth the whole room is white and the Registrar's Gallery opens behind the desk.

### Choices

**5.c1** at 5.2 The Stamp, Reversed: Which page takes the stamp?
- *notice* (The Courier's notice (needs notice_kept).). Now: **SLICE:** You kept it. / W|I keep receipts.. Later: Stamped DENIED on the Courier's name; Ending: the clipboard is among the set-down things.
- *wall* (A page off the wall.). Now: **SLICE:** You tore the first one. We take another.. Later: Same DENIED; the Registrar has a different phase-two taunt.

**5.c2** at 5.1 The Reading Room: The Mega Yo Bot reads the order aloud. Wallit can read along.
- *aloud* (Read the first page aloud with it.) sets `page_read_aloud`. Now: **WALLIT:** STRIKE. THIS. ADDRESS. Sounds worse when it's yours.. Later: Registrar fight: R: 'Reader recognized.' Slice, flat: 'It knows you.'
- *silent* (Read it silently.). Now: **SLICE:** Say it. / W|No.. Later: Registrar fight: canon, no reader line.

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | A bare round hall, the circle on its floor, every wall lined with identical forms. | Slow orbit, 360 degrees over six seconds | NARRATION | (none) | Held choir pad, no words |
| 2 | A Mega Yo Bot at a lectern reads the forms in an even voice. | Low angle, centered | MEGA YO BOT | Order, read aloud: STRIKE. THIS. ADDRESS. | Choir pad swells on each word |
| 3 | Slice near the desk, hand on the circle's edge. | Medium, locked | SLICE | It's reading it out. | Pad holds |
| 4 | Wallit holds the RETURN TO SENDER stamp up to the light. | Close on the stamp | WALLIT | Then I'm reading mine. Pay up. | Pad drops; one dry strike of a wooden block |
| 5 | Wallit's eyes on the form: no signature line. | Insert on the blank line | WALLIT | It's not signed. | Silence |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Registrar, gold trim cracked, ledger open on its knees, every page blank. | Wide, locked | THE REGISTRAR | Register... closed... uploading... nothing? | Pad drops to a single held note |
| 2 | Wallit presses the stamp on the final page; DENIED appears. | Insert on the ink | WALLIT | Struck from the record. | Wooden block, one strike |
| 3 | Slice reading the white walls, then the door. | Slow pan to the door, now open | SLICE | Clean. | Pad warms by a third |
| 4 | Wallit and Slice, side by side at the door. | Pull out to a wide | WALLIT | Then we answer it. Pay up. | Brass stab, then silence |

### Map design sheet: reading-room

- **Setting:** The Reading Room: a bare round hall, the circle drawn on its floor at the Hollow's scale, walls lined with identical forms, an empty desk beside it.
- **Landmarks:** The empty desk; Page lecterns (5); The Registrar's Gallery (80 m, bearing 0) round the Great Ledger; The door (bearing 180)
- **Route:** Carpet aisle from the door to the desk; the fight is at the circle.
- **Hazards:** Pages that lift and settle on the lecterns (telegraphed); Forms blank page by page, then the Gallery opens
- **Boss arena:** The Registrar's Gallery: a round dais with the Great Ledger.
- **Palette:** `#14110A` `#4A3F23` `#F0E7C8` `#E6B85C` `#8A1E1E`
- **Props:** wall racks of forms (10); page lecterns (5); desk; Great Ledger; carpet aisle; white door

## Chapter 6: Address Filed (stamp: ADDRESS FILED)

Status: built; text extended here as proposed. Emotion: Release. The order is answered; the cost is that someone has to write the door.

### Script

**Scene 6.1: The Wall Is Gone** (The Moonlit Hollow, changed) [canon]  
*The wall of the Department is broken stumps; a pale path leads nowhere.*

> **SLICE:** The wall is gone. The circle is brighter.  
> **WALLIT:** Then let's file it. Pay up.  

**Scene 6.2: Three Waves** (The circle) [proposed]  
*The Sealed Yo Bots; the order stirs.*

> **YO BOT:** Sealed! Uploading... uploading...  
> **SLICE:** They're not chirping right.  
> **WALLIT:** Frightened bots. That's new.  

**Scene 6.3: The Order** (The Threshold) [canon + proposed]  
*The Order-Bearer, scroll across its back with a red seal.*

> **THE ORDER-BEARER:** Order, in force: STRIKE. THIS. ADDRESS.  
> **SLICE:** Red is where it lands. Move, or break it.  
> **WALLIT:** Signature required. I'll sign it for you.  
> **THE ORDER-BEARER:** Signature... required... uploading... nothing?  

**Scene 6.4: Filed** (The circle) [canon]  
*The order drops; DENIED; Wallit carries the stamp to Slice's ring with his own hands.*

> **NARRATION:** The Spider Bot comes apart. The order drops.  
> **WALLIT:** 'Strike this address.' Stamped DENIED. Ha.  
> **SLICE:** Filed. Properly, this time.  
> **WALLIT:** So we exist.  
> **SLICE:** Clean. Good swing, Wallit.  
> **WALLIT:** Pay up.  

### Scripted set piece: The Door Number [built — playable finale carry]

After DENIED, the camera settles on Slice's ring. The player **holds swing** to carry the stamp to her circle while three of the
Order-Bearer's last notices rain down (timing icons; a miss knocks Wallit back, never ends the beat). On release the stamp prints
ADDRESS on the ring, lamps around the ring light one by one (≥4), Slice's closing line plays, and the Chapter 6 theme swells into
ADDRESS FILED. Interaction 20–40 s; keys, gamepad and touch. RECEIVED after the card is unchanged. Code: `CinematicDirector`
`finaleCarry` on `ending_filed_world`; proof playtest `finale-carry`.

### Choices

**6.c1** at 6.4 Filed: The stamp is in Wallit's hand. The ring needs one more stamp.
- *keep* (Stamp it himself.). Now: **WALLIT:** My stamp, my door.. Later: Ending: the stamp stays in Wallit's hand.
- *share* (Hand the stamp to Slice.) sets `stamp_shared`. Now: **SLICE:** Yours. / W|Take it.. Later: Ending: Slice presses the RECEIVED stamp herself.

**6.c2** at 6.4 Filed: Slice writes a door number with her scalpel. Wallit has a request.
- *one* (One number, for the circle.). Now: **SLICE:** One.. Later: Ending: one door; the lost things get theirs.
- *two* (Two numbers, one beside the other.) sets `door_for_both`. Now: **WALLIT:** Two. Put me next to you. / S|...Clean.. Later: Ending: the door has two numbers; credits card: a second line.

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Hollow at dawn-blue, the wall a row of broken stumps, a path of pale slabs running to nothing. | Slow aerial pullback to reveal the missing wall | NARRATION | (none) | Cello pedal from Chapter 1, now with a third above it |
| 2 | Slice at the ring, looking to where the wall was. | Medium, locked | SLICE | The wall is gone. The circle is brighter. | Pedal holds |
| 3 | Wallit swings the bat once, testing it, grinning. | Low angle | WALLIT | Then let's file it. Pay up. | Pulse enters, bright |
| 4 | A great gold seal rises at the far end of the pale path. | Push toward the seal | NARRATION | (none) | Brass note, held |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The Order-Bearer collapses; the order floats down in the dawn light. | Wide, then follow the page down | NARRATION | The Spider Bot comes apart. The order drops. | All silent; a held breath |
| 2 | Wallit reads the order in his hands; the stamp falls on it, DENIED. | Over-the-shoulder, insert on stamp | WALLIT | 'Strike this address.' Stamped DENIED. Ha. | Wooden block, then a bell |
| 3 | Slice writes the door number with her scalpel across the ring. | Top-down, slow rotation | SLICE | Filed. Properly, this time. | Strings reply, full and warm |
| 4 | Wallit looks at the number and the ring glowing under his feet. | Medium | WALLIT | So we exist. | Chord rises |
| 5 | Two figures, small, in a gold ring at dawn. | Pull back to a very wide; title: ADDRESS FILED | SLICE | Clean. Good swing, Wallit. | Resolve; ring the bell once |
| 6 | Black. Wallit's voice, over the card. | None | WALLIT | Pay up. | Silence; then fade into the credits theme |

### Map design sheet: hollow-filed

- **Setting:** The Moonlit Hollow, changed: the wall gone, a pale path leading nowhere, the circle drawn in white and brighter.
- **Landmarks:** The pale path run on to 180 m where it stops at nothing; Broken stumps of the Department wall (190 m); The Threshold (80 m, bearing 20): a bare white doorframe on a dais
- **Route:** The pale path out to the Threshold; the fight is at the circle.
- **Hazards:** Sealed Yo Bots (juggernauts, stamped shut); The Order-Bearer's stamp marks
- **Boss arena:** The Threshold: a bare white doorframe on a dais, bearing 20.
- **Palette:** `#0E1730` `#324C86` `#E8EEFF` `#F2CC66` `#FFFFFF`
- **Props:** broken wall stumps; pale slab path; white doorframe; gold seal; circle in white; dawn-lit grass

Gate: All six lost things in the case file when Chapter 6 is filed (the Unsent Postcard, Key With No Lock, A Cleared Hat, Thrice-Refused Paper, Ledger Leaf, The Circle's Stamp).

## Ending: RECEIVED

Status: built (Story.SecretEnding); screenplay extended here as proposed. Emotion: Grace. The Department's tool is given back as a gift.

### Script

**Scene E.1: Set Down** (The filed circle) [canon]  
*Wallit sets the six things down in the ring.*

> **NARRATION:** Wallit sets six things down in the ring: a postcard, a key, a hat, a paper, a leaf, a stamp.  
> **SLICE:** No address.  
> **WALLIT:** Then give it one. You've got the stamp.  

**Scene E.2: Stamped ADDRESS** (The ring) [canon]  
*The stamp prints ADDRESS; nothing strikes it.*

> **NARRATION:** The stamp prints ADDRESS. Nothing strikes it.  
> **NARRATION:** Every lost thing in the Hollow gets a door.  
> **YO BOT:** Delivery! ... Oh. It's for us?  

**Scene E.3: Received** (The ring) [canon]  
*The last word is not RETURN TO SENDER.*

> **WALLIT:** Return to sender?  
> **SLICE:** Not this time. Received.  
> **WALLIT:** Received. In full. Pay up.  

### Scripted set piece: The Doors Open

Every lost thing set in the ring rises one at a time and a door appears behind each. The Courier's clipboard (if notice_kept) is among them; so is a half-built Yo Bot (if Ch3 unhook). The last door is Slice's. It has a number in her handwriting. If door_for_both it has two numbers.

### Choices

**E.c1** at E.3 Received: The RECEIVED stamp is ready. Who presses it?
- *wallit* (Wallit presses it.). Now: **WALLIT:** Received. In full.. Later: Credits card: Wallit's line over RECEIVED.
- *slice* (Slice presses it (needs stamp_shared).). Now: **SLICE:** Received.. Later: Credits card: Slice's line over RECEIVED. Wallit: 'First time you've said it.'

### Opening cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The filed circle at dawn, six small things in a row inside it. | Top-down, then slow descent | NARRATION | Wallit sets six things down in the ring: a postcard, a key, a hat, a paper, a leaf, a stamp. | Cello pedal; harp figure from Chapter 4, now resolved |
| 2 | Slice looks at the six things, the ring, and Wallit. | Medium two-shot | SLICE | No address. | Pedal holds |
| 3 | Wallit lifts the stamp. | Close on the stamp | WALLIT | Then give it one. You've got the stamp. | Pedal lifts a step |

### Closing cutscene shot list

| # | Shot | Camera | Speaker | Voice line | Music |
|---|---|---|---|---|---|
| 1 | The stamp comes down on the ring: ADDRESS. | Insert, then pull back as doors rise | NARRATION | The stamp prints ADDRESS. Nothing strikes it. | Bells in a line, no pulse |
| 2 | A small Yo Bot with a paper, puzzled, in front of a new door. | Low angle, soft focus | YO BOT | Delivery! ... Oh. It's for us? | A single, kind chirp |
| 3 | Wallit and Slice in the gold light; the stamp reads RECEIVED. | Slow pull back to a wide | SLICE | Not this time. Received. | Full strings resolve |
| 4 | Wallit, grinning, bat on his shoulder, in front of the stamped ring. | Medium, hold | WALLIT | Received. In full. Pay up. | Final bell; fade to the credits |

### Map design sheet: hollow-filed

- **Setting:** The same filed Hollow as Chapter 6, an hour later: soft light, six small things in the ring.
- **Landmarks:** The ring with the six things; The doors that appear one by one; The Threshold left open
- **Route:** None; the scene plays at the circle.
- **Hazards:** None
- **Boss arena:** None.
- **Palette:** `#14223F` `#4B6AA5` `#F4F0E0` `#F2CC66` `#FFFFFF`
- **Props:** six lost things; new doors (up to 7); RECEIVED stamp; dawn grass; the Courier's clipboard (if kept)

## Open canon questions

Each carries the answer this bible assumes, so work can go on unless J overrules.

Q1. **Who is the Department? A person, a machine, a building, a rule?** Assumed: A process with no face; its parts are the bots, the walls and the stamps.
Q2. **Did Slice once work for the Department's order, and why did she stop?** Assumed: Yes, once. Why is never said; her care is shown, not told.
Q3. **What is Wallit: a lost thing, a made thing, or a filed thing that went wrong? Whose money was in him?** Assumed: Unsaid. The torn postmark is half his; the other half is never found.
Q4. **Who is the sender?** Assumed: No one. The stamp is automatic.
Q5. **Are the cleared things on the Floor alive?** Assumed: No, but Wallit fears they might be; Ending lets J decide whether they wake.
Q6. **Does a Half-Built Yo Bot count as unfiled?** Assumed: Yes; it appears in the ending only if Ch3 unhook was chosen.
Q7. **Should the choices change real outcomes, or only later lines?** Assumed: Only lines and one scene variant, so no save-format change is needed beyond a flag word in the case file.
Q8. **Should choices be made in dialogue boxes (Cutscene.cs supports none yet) or in the world?** Assumed: In the dialogue box with two buttons; requires a Cutscene.cs choice page.
Q9. **Who is the Courier's sender?** Assumed: Unknown. It reads from a clipboard that is blank when examined.
Q10. **Why 30 waves in Arcade and 3 in Chapter 6?** Assumed: Arcade is the Department trying again; the campaign is the Department being answered.
Q11. **Does the player ever get an address of their own?** Assumed: Wallit is on the books through the circle only. A sequel hook could give him a door of his own.
Q12. **Should every line have a Hawkes Gen voice?** Assumed: Subtitles first; Wallit and Slice voiced where files exist.

## What's next (not done here)

- Wire the text into `Story.Chapters` (the lines are already in `Story.cs` format), and add a choice page to `Cutscene.cs` for the choices (open question Q8).
- Store the choice flags in `CampaignSave.Slot` (a flag word; save version bump with a `Migrate` step).
- GPU work (cutscene stills, backdrops, models, music): see the estimate in the summary. Nothing was generated.
