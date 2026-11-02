# The Place Department: Campaign bible

"Where things that should not exist are given an address."

This is the plan for Campaign mode: six chapters told in order, in the three case files. It builds on `Docs/Story.md`
(the premise, the circle, the three acts, the wave 30 finale) and changes none of it. Arcade keeps its own thread
(wave beats, intro, ADDRESS FILED); Campaign is the longer telling of the same story. **Content only: nothing here is
wired into `Story.Chapters` yet** (only Chapter 1's placeholder is).

Line format matches `Story.cs`: `W` Wallit, `S` Slice, `Y` Yo Bot, `N` narration. Voices hold: **Wallit** talks money and is
brash. **Slice** is clipped, a few words, never a joke. **Yo Bots** are chirpy and read their orders as cheerful
notices. No piano, no office jokes, no bow, no birds. Warrok is retired (see open questions).

Status tags: **[canon]** already in Story.md or the game. **[proposed]** new here, awaiting J.

## 1. Sources read

- `Docs/Story.md`, `Assets/Scripts/Story.cs` (intro, 30 wave beats, finale, Chapter 1 placeholder), `AGENTS.md`,
  `README.md`, `BotRoster.cs` (bot kinds, blurbs, unlock waves).
- Website `releases.json` (1.0.0 to 1.6.1) and the game's own `Tools/Web/releases.json` (1.0.0).
- Release history shows the cast changing: the first site notes (1.0.0, 2026-10-18) describe a pirate pianist, Warrok with a
  bow, and birds. 1.3.0 to 1.5.0 introduce 3D Wallit, Slice with a knife, the Yo Bots and Spider Bots, and "a clearer point
  to its story". Only the latter cast is canon now.

## 2. Story spine

**Logline.** A wallet with a bat is stamped "Return to sender", finds no sender, and ends up inside the one filed address in
a hollow where unfiled things are cleared. He defends it for the guard who keeps it, and learns that the order against
the address was never really about her.

**Spine in six beats.**

1. Wallit is thrown out with no address. Slice offers him the circle: inside it he exists. [canon]
2. He tries to find his sender. There is none; the Hollow ends at a wall the Department built. [proposed]
3. The bots are not wild. They are the Department's clearing crew, working to a schedule. Wallit finds where it is kept. [proposed]
4. The bigger machines, the Spider Bots, are not clearers but **servers**: they walk to an address to deliver the order
   in person. Slice learns the order has been aimed at her circle since before Wallit arrived. [proposed]
5. The order is a form, not a creature's will. It can be answered. Wallit and Slice decide to answer it. [proposed]
6. The Spider Bot carries the order; it falls; Wallit reads "Strike this address", stamped DENIED; Slice files the
   circle herself. ADDRESS FILED. [canon, = Arcade wave 30]

**Themes.** Existing means being on the books. Money is how Wallit says he cares: debts, tabs, wages, tips. Slice's
care is shown, not said. The Department never hates anyone; it only clears.

**Arc.** Wallit: from "I want a refund" to "I'm covering you" (Story.md act two) to choosing an address of his own.
Slice: from "Stay in the circle" to admitting she needs him ("Slice admits she needs him", act one) to filing the circle
herself. The unseen creature behind the order stays unseen and unnamed. [canon]

## 3. Where the bots come from

Settled in the game: both kinds are sent by the Department's order to strike the circle's address; Yo Bots are "the
Department's chirpy clearing crew"; they come Small, Plated, Mega; Spider Bots walk on six legs and stab; the wave 30
Spider Bot carries the order itself; bots drop bolts, which Wallit decides are a wage. [canon]

Proposed answers for Campaign (tagged so J can overrule):

- **Yo Bots: the Clearing Crew.** Cheap units stamped out at the **Clearing Floor** (Chapter 3), one batch per unfiled
  thing in the Hollow. They chirp because every job closes with a report ("Uploading victory!" means "filed as cleared").
  Plating (wave 10) and Mega (wave 20) are the Floor re-issuing the same unit heavier when the cheap ones fail. [proposed]
- **Bolts.** The bolts they drop are the Floor's own fasteners; a Yo Bot is held together by its pay. Wallit sees this:
  he is taking back what the Department owes. Explains "bolts are wages". [proposed]
- **Spider Bots: the Process Servers.** Not clearers. Servers deliver an order to an address and wait while it is read.
  Six legs, because they walk the whole length of the Hollow's paths rather than fly or roll. The first one (wave 30) carries
  the order as a physical form. The later kinds in the roster (Stalker, Runner, Rammer, Shadow, Crusher, Colossus, Titan)
  are server models for harder-to-serve addresses, and appear only in Arcade overtime and any post-ending chapters. [proposed]
- **The order.** A form printed by the Department; it is read aloud by Mega Yo Bots from wave 20 and served by the
  Spider Bot. Nobody inside the Department reads it. The creature behind it is never named or shown. [canon + proposed]

## 4. Chapters

Each chapter has the space (a Campaign scene, built in code like `ChapterScene`, free of solid objects within 150 m
of the circle where fighting happens), the objective, dialogue beats, a cutscene and a boss. Boss kinds use the existing
roster so no new enemy art is needed for chapters 1 to 3 and 6.

### Chapter 1: Return to Sender

- **Space:** The Moonlit Hollow, with the circle at the center. [canon, placeholder exists]
- **Objective:** Hold the circle through one wave of Yo Bots; the first bolt is collected. (Tutorial: move, bat, lock-on.)
- **Dialogue beats:**
  - W: "Return to sender. Which sender? I want a refund." [canon]
  - S: "No sender. No refund. Stay in the circle." [canon]
  - W: "Where do I sign for this bat?" / S: "Swing it."
  - Y: "Hello! Uploading victory!" / W: "Pay up, bots." [canon, wave 1 beat]
  - After the wave, W: "Bolts. Somebody's paying me." / S: "Not somebody. Something."
- **Cutscene:** *The Stamp.* Over the frozen Hollow: a hand-cut RETURN TO SENDER stamp falls on Wallit's leather, the
  ink the only bright thing in the dark. He lands in the grass and the circle's ring lights as he rolls into it.
- **Boss:** **The Courier**, an Alpha Yo Bot with a clipboard, a "notice of striking" (first Alpha of the Yo Bot line). [proposed]

### Chapter 2: No Fixed Address

- **Space:** The Hollow's edge: the Boundary wall (205 m), a gate with no handle, a signpost with nothing on it. [proposed]
- **Objective:** Walk out from the circle to the gate and back under fire; Slice insists on a radius. Survive three waves.
  Wallit finds a postmark that is his own, torn.
- **Dialogue beats:**
  - W: "There's a door. Doors have senders."
  - S: "Doors have locks. Come back."
  - W (at the gate): "Nothing. Not even a doormat. I'd say that's rude, but I'm the one with no address."
  - S: "You have one. The circle."
  - W: "Leased. By you." / S: "Free." / W: "Nothing's free." / S: "Then pay with swings."
  - Y: "Address unknown! Deleting!" [canon wave 4 beat]
- **Cutscene:** *The Gate.* Wallit's bat knocks on the wall; it answers with the same stamp, from the other side,
  RETURN TO SENDER, pressed through from the dark. The wall is a Department wall.
- **Boss:** **The Plated Alpha**, a Plated Yo Bot with a stamped plate that says INVALID. [canon: Plated debuts wave 10]

### Chapter 3: The Clearing Floor

- **Space:** A sunken yard beyond a broken gate, lit by conveyor lamps: rows of un-built Yo Bot chassis on hooks, and a
  slow belt carrying flat, cleared things (a hat, a clock, a small bell). [proposed]
- **Objective:** Reach the Floor's lever from the circle in three legs, with Slice following. Break the belt feed so no new
  Yo Bots arrive while the order is read. (The circle must stay lit: Slice holds a second ring they carry with a
  stake in the floor.)
- **Dialogue beats:**
  - Y (a half-built one, on a hook): "Hello! I am... not finished! Uploading victory?"
  - W: "They're all the same unit."
  - S: "Spare parts. Spare orders."
  - W: "Those bolts are the same ones they've been paying me."
  - S: "Then we collect."
  - W (at the belt): "Cleared things. Somebody's hat. Somebody's clock. That could be me."
  - S: "It isn't." (Slice's longest beat; she does not explain.)
- **Cutscene:** *The Belt.* The camera follows the belt past the cleared things into a chute lit by a single red lamp.
  The lamp is a lens: it turns toward the Hollow, and the circle blinks once.
- **Boss:** **The Foreman**, a Mega Yo Bot Alpha that reads the order aloud on its charges. [canon: Mega debuts wave 20]

### Chapter 4: The Process Servers

- **Space:** The Long Walk, a causeway of stone slabs over a pit of mist, with six stilts of lamp-light on either side
  (six lamps, one per leg). A single path, no cover. [proposed]
- **Objective:** Cross it with Slice walking behind; stop at each lamp to hold the new circle until a Spider Bot arrives, then
  put it down. Three crossings.
- **Dialogue beats:**
  - Y: "Spiders en route! Chirp!" [canon wave 26 beat]
  - S: "Mind the legs." [canon]
  - W: "They're not bigger Yo Bots. They're... deliveries."
  - S: "Process servers. They stand and wait while it's read."
  - W: "Read what?"
  - S: "My address. Struck."
  - W: "Since when?"
  - S: "Before you." (First time she says it plainly. W has no joke.)
- **Cutscene:** *Served.* A Spider Bot stops a step short of the circle, folds one leg, and offers a paper on the tip of the
  claw, politely, the way a courier would. Slice does not take it. It waits.
- **Boss:** **The Server**, the first full Spider Bot (Alpha), which will not strike until the paper is refused three
  times. [canon: Spider Bot debuts wave 30]

### Chapter 5: Strike This Address

- **Space:** The Reading Room, a bare round hall: the circle drawn on its floor at the scale of the Hollow's, the walls lined
  with forms, all identical, all this order. A desk in the center, empty. [proposed]
- **Objective:** Read the order. Defend the desk while Wallit reads each line aloud, because each reading draws a wave. Five waves,
  one per page.
- **Dialogue beats:**
  - Y: "Order, read aloud: STRIKE. THIS. ADDRESS." [canon wave 20 beat]
  - S: "It's reading it out." / W: "Then I'm reading mine. Pay up." [canon]
  - W: "It's not signed."
  - S: "Nobody signs it. That's how it works."
  - W: "Then it can be denied."
  - S: "Denied by whom?"
  - W: "By the guy with the stamp." (He holds up the RETURN TO SENDER stamp he picked up at the gate.)
  - S: "Wallit. That's a lot of nerve."
  - W: "Nerve's free. The swing's extra."
- **Cutscene:** *The Stamp, Reversed.* Wallit presses the Department's own stamp against the first page; the ink shows
  nothing, then DENIED. All the forms on the walls turn over at once, blank.
- **Boss:** **The Registrar**, a Titan Spider Bot with gold trim (`BirdArt.Glowing`) that reads the pages with
  every leg. [proposed; existing Titan model]

### Chapter 6: Address Filed

- **Space:** The Moonlit Hollow, as in Chapter 1, now changed: the wall gone, a path leading nowhere, the circle brighter. [canon + proposed]
- **Objective:** The wave 30 finale: kill the last bot of the Alpha wave, take the order, read it, file the circle. Wave
  30 trimmed to the boss and Slice topped up, as in Arcade's `story` scenario.
- **Dialogue beats:** Finale lines from `Story.Finale` verbatim:
  - N: "The Spider Bot comes apart. The order drops."
  - W: "'Strike this address.' Stamped DENIED. Ha."
  - S: "Filed. Properly, this time."
  - W: "So we exist." / S: "Clean. Good swing, Wallit." / W: "Pay up."
- **Cutscene:** *Filed.* The ring around the circle becomes a line of writing, then a door number. Slice writes it with her
  scalpel. The camera pulls back to ADDRESS FILED.
- **Boss:** The Spider Bot that carries the order (wave 30's Alpha). [canon]
- **Ending:** Case file marked closed. Enter keeps going into Arcade overtime. [canon]

## 5. Campaign rules (to keep it coherent)

- Wallit never apologizes or admits fear; he converts it to money. Slice never explains herself fully. Yo Bots never
  understand what they say.
- No jokes about office work. No piano. The Department is a thing that files, not a joke.
- The creature behind the order is never seen, never named, never quoted.
- A chapter is one stamp: Chapter 1 stamps RETURN TO SENDER, chapter 6 stamps ADDRESS FILED. Chapters 2 to 5 should
  each have a stamp title (done above).
- Boss bots reuse the roster's models; no new art is needed to ship chapters 1 to 3 and 6.

## 6. Open canon questions (not settled by the lore)

Each has the answer this bible assumes, so the work can go on unless J overrules.

1. **Warrok.** AGENTS.md and Story.md retire Warrok, the bow and the birds, yet the brief, release notes 1.0.0 to 1.6.1
   on the site still name them. Assumed: stays retired and never appears; old release notes are history. Should the
   website's 1.6.1 note ("get straight to Warrok and the birds") be corrected?
2. **Who is the Department?** A person, a machine, a building, a rule? Assumed: a process with no face; its parts are the
   bots, the walls and the stamps.
3. **Who made Slice, and why does she guard the circle?** Story.md says she is "the Department's guard", but does she work
   for the same Department that sends the order? Assumed: yes, once; she stopped. Why she did is unsettled.
4. **Why did she stop playing the piano?** Retired detail; assumed to stay unexplained.
5. **What is Wallit?** A wallet "that should not exist". Is he a lost thing, a made thing, or a filed thing that went wrong? Whose money was in him?
6. **Who is the "sender"?** Is there one? Assumed: no one; the stamp is automatic.
7. **Are the cleared things on the Floor alive?** Assumed: no, filed objects, but Wallit fears they might be.
8. **Where do bots go when they fall?** Assumed: their bolts remain, the shell goes to scrap and the Floor re-issues them.
9. **Why 30 waves and then overtime?** Assumed: the order is served after 30 refusals; overtime is the Department trying again.
10. **Do later Spider Bot kinds (Stalker to Titan) mean more orders?** Assumed: server models for harder addresses;
    Campaign does not show them.
11. **Does the player ever get an address of their own?** Assumed: after Chapter 6 Wallit is "on the books" through the circle but no more.
    A sequel hook could give him his own.
12. **Chapter length and saves.** Should each chapter be one wave set or a stand-alone map? Assumed: stand-alone
    scene per chapter via `ChapterScene`, saved at chapter start in the case file.
13. **Chapter 1's current text.** The placeholder is two lines; assumed to be expanded, not replaced.
14. **Voice.** Does each new beat get a Hawkes Gen voice line, or are subtitles enough? Assumed: subtitles first.

## 7. What's next (not done here)

- Put chapter text into `Story.Chapters` (data) and the cutscene cards in `StoryScreen`/`ChapterScene`.
- Chapter scenes (spaces for 2 to 5) need level layout, models for the Floor and Reading Room, and cutscene art.
- A `campaign` playtest scenario once chapters exist (extends `modes`).
- GPU work (key art, cutscene stills, chapter backdrops, models): not started. See the estimate in the PR summary.
