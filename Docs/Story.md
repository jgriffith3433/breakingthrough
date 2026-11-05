# The Place Department: the story

"Where things that should not exist are given an address."

This is the story the game tells. `Assets/Scripts/Story.cs` holds every line below as data; if you change a line, change
it in both. Voices: **Wallit** talks money. **Slice** is clipped, a few words, never a joke. **Yo Bots** are chirpy.
Warrok, the bow and the birds are retired and never appear here.

## The premise

The Place Department files things that should not exist. A thing that is filed has an address, and a thing with an
address is allowed to stay. A thing without one is cleared away.

**Wallit** is a wallet with a wooden bat, the kind of thing that should not exist. The Department stamped him
"Return to sender", found no sender, and threw him into the Moonlit Hollow with a bat and no refund.

**Slice** is already there. She is the Department's guard of **the circle**: a ring on the ground, the one address in the
Hollow that is filed. Inside it, things exist. Slice stands in it with her scalpel knife out and does not play the piano
any more.

## Why the circle matters

The circle is the only address in the Hollow. Wallit has none, so while he is inside it he is on the books: he exists,
he can be hurt, he can be paid. If Slice falls, the circle is struck off and everything in it is cleared with her. The
run ends when Slice falls. That is the whole game: Wallit swings, Slice holds the ring. The game-over panel says so: **Address struck off**.

## Who comes

**Yo Bots** are the Department's chirpy clearing crew ("Uploading victory!"). They come in small, then Plated, then
Mega. **Spider Bots** are the big ones: they walk on six legs and stab. Both carry the same order: strike this address.
The creature behind the order is never seen and never named.

## The thread

Every wave start gets a short beat. A beat is one to three lines, one speaker each, shown for about two seconds.
Boss waves (10, 20 and 30) get a title card and a longer beat.

- **Act one, waves 1 to 9: the arrangement.** Wallit is here for the coins, Slice is here for the circle. The Yo Bots are
  cheerful about erasing them. Wallit works out that the bolts the bots drop are a wage, and Slice admits she needs him.
- **Act two, waves 10 to 19: the plating.** The Plated Yo Bot arrives on wave 10. Slice is hurt for the first time and says
  so. Wallit stops talking about himself and starts talking about her debt ("I'm covering you").
- **Act three, waves 20 to 29: the order.** The Mega Yo Bot arrives on wave 20 and the Yo Bots stop chirping and read the
  order aloud. Wave 30 is the first Spider Bot, the one that carries the order itself.

## The finale: wave 30

Wave 30's boss is the Spider Bot. When the last bot of wave 30 falls, the order drops, and Wallit reads it: "Strike this
address", stamped DENIED. Slice files the circle herself. The ending screen reads **ADDRESS FILED**, and gives the run:
waves, level, streak, coins. Any key goes back to the menu; **Enter keeps going** into overtime, where the waves
continue and the run still ends when Slice falls.

## The intro

Shown once at the start of a run that begins at wave 1, over the frozen Hollow. Five pages, any key or tap for the next,
**Skip** (or Enter) leaves at once.

1. The tagline and the place.
2. Wallit says who he is.
3. Slice says what the circle is.
4. Slice says why it matters.
5. A Yo Bot arrives. Wallit says what to do.

## Wallit talks

Wallit barks while he fights, in `WallitVoice.cs` (banks) and by voice file (`Resources/Voice/wallit_<line>.mp3`, ids in
`Resources/Voice/audio.json`, made with Hawkes Gen `audio.voice` in his Dose Of Destruction voice). He is brash and angry and
everything is money: a kill is "Paid in full." or "Exact change.", a streak is "Cash rain!", a big hit is "Interest due!",
a home run is "Returned to sender!", a hit on him is "Ow! That's extra!", a wave start is "Cash only!", a level-up is
"Profit!", a chest is "Finders keepers!", and when Slice runs low he answers "Slice! Hold on!". No jokes about work, and
the wave beats' Wallit lines above are spoken too. One voice at a time: he waits for Slice to finish and never repeats a
line back to back. Nobody talks all the time: `VoiceDirector.cs` gates every line (Slice's and his). Story beats, boss waves and Slice running low are key and always play; wave starts, level-ups, chests and home runs follow after a short gap; kill, streak, hurt, idle and banter barks need 16 s of quiet and a cooldown per line and per reason. His mouth is a part on the painted face (`WallitMouth.cs`) that opens and closes with the loudness of
what he says.

## Campaign and Arcade

The main menu offers two ways in. **Arcade** is the Moonlit Hollow as it has always been: waves, bolts, boons, the shop, the
run that ends when Slice falls (everything above). **Campaign** tells the story in order, one **chapter** at a time, in
three **case files** (save slots). A case file is where the Department keeps your place: empty, it reads "No file yet";
opened, it reads the chapter you are on and how long you have been on the books. The shop's coins and upgrades are
Arcade's and stay Arcade's.

Chapters are numbered and named after a stamp, six in all (`Docs/CampaignBible.md` tells them). Each is data in
`Story.Chapters`: a title, a place, an objective (down a count of bots, hold the circle for a time, reach a place, or bring
down a boss), the bots that come, the lines said before and after. Begin on the chapter card plays it in the Hollow with
Wallit's own controls and bat and Slice at the circle; the objective sits in a line at the bottom, meeting it shows
**Chapter Complete**, and the case file moves on to the next chapter (the last one reads "filed"). Chapter 1 teaches the bat:
hold the circle against six Yo Bots, then bring down the Courier (below).

- Wallit: "Return to sender. Which sender? I want a refund."
- Slice: "No sender. No refund. Stay in the circle."

`Assets/Scripts/Story.cs` holds the chapter data (`Story.Chapters`); `CampaignSave.cs` holds the case files;
The full screenplay for the six chapters and the ending (scene scripts, choices, set pieces, shot lists, map sheets, open questions) is in `Docs/CampaignBible.md`, rendered from `Assets/Resources/Story/campaign_script.json` (`CampaignScript.cs`, `bible` playtest). `CampaignPanel.cs` is the slot screen; `ChapterScene.cs` builds the placeholder in `Assets/Campaign.unity`.

### Grades and Hard

The Department grades every chapter it files. **Chapter Complete** reads a rank: S, A, B or C, from the chapter's time against its
par and what Slice took (a clean, quick file is S). The case file keeps the best rank for each chapter, and the chapter card shows
it. Once every chapter is filed, the case file may be run again on **Hard**: the chapter card gains a Difficulty button. On
Hard the bots carry 75% more health and every blow on Slice lands twice as hard, the HUD says HARD, and Hard keeps its own best
ranks. Code: `CampaignGrade.cs`, `grade` playtest.

## Chapter scenes

A chapter can open with a **cutscene**: Begin on the chapter card, and before the fighting the Hollow freezes under letterbox bars. A
panel at the bottom names who is talking (WALLIT, SLICE, YO BOT; narration has no name), with a portrait slot beside it, and types
the line out. The camera moves to whoever speaks, or pulls back to take in both. Any key, click or tap finishes the typing and then
turns the page; Enter or the Skip button leaves the scene and the chapter starts at once. Wallit and Slice are voiced, one at a time:
a page's voice is cut when you turn it, and nothing else speaks until the scene is over (`VoiceDirector`: "cutscene" is a key
reason and the only one allowed while a scene is up). The scene is data in `Story.Chapters` (`Cutscene`), played by `Cutscene.cs`.

Chapter 1's scene is **The Stamp** (`Docs/CampaignBible.md`): a stamp comes down out of the dark, RETURN TO SENDER, the only bright
ink in the Hollow. It lands on a wallet's leather; he hits the grass and rolls, and the circle's ring lights around him.

- Slice: "You're inside. Inside, you exist."
- Wallit: "Does existing come with a receipt?" Slice: "No."
- Wallit: "Where do I sign for this bat?" Slice: "Swing it."
- A Yo Bot: "Hello! Uploading victory! Striking this address now!" Wallit: "Pay up, bots."

### Chapter 1's boss: the Courier

After the first Yo Bots the Courier comes: an Alpha Yo Bot (bigger, amber-tinted, red crest) with a clipboard and a "notice of
striking". Yo Bot: "Special delivery! Notice of striking!" Slice names it ("It reads the order out, then it lands.") and Wallit
answers with the bill. Its health bar sits at the top of the screen under its name. It never swipes. It walks to 7.5 m of Slice,
stops and lifts the notice overhead; a **red mark fills on the circle's ground** (the telegraph, about two seconds), and when it
fills the notice lands on Slice for 12. Two answers, which Slice says aloud the first time ("Break the wind-up. Or stand in it."):
hit the Courier for a tenth of its health before the mark fills (it drops the notice, "Notice dropped!", and stands dazed), or
stand in the mark and take the stamp yourself ("Wallit takes the stamp"; Slice is spared). Under half health it winds up faster
and calls two Yo Bots with every notice. The defeat beat: the Courier, coming apart, "Notice... undelivered... uploading...
defeat?"; Wallit "Return to sender."; Slice "Clean." Then Chapter Complete, and the file moves to Chapter 2. The fight is
`Courier.cs`; the chapter flow (`HoldThenBoss`) is in `ChapterRun.cs`, the lines in `Story.Chapters[0]`.

### Chapter 2: No Fixed Address

The Hollow's edge. Wallit sees a gate far off (70 m from the circle, bearing 150 degrees) and goes: "There's a door. Doors have
senders." Slice: "Doors have locks. Come back." The chapter is four legs, shown by the objective line at the bottom of the
screen: **Reach the gate** (metres counting down, Yo Bots coming the whole way), **The Gate** scene (his bat knocks; the wall
answers with the same RETURN TO SENDER stamp pressed through from the other side; a postmark torn into the stone, half of it
his), **Back to the circle**, then **three waves** held at the circle ("Address unknown! Deleting!"; Slice: "Last wave.
Something heavier follows.") and **The Plated Alpha**, a Plated Yo Bot grown big with a red plate stamped INVALID on its
chest ("Notice! This plate is stamped: INVALID!" / Wallit: "So does my refund. Pay up."). It has no special attack: it is a wall
of plate to break. Defeat beat: "Plate... voided... uploading... nothing?" / "Void your own stamp." / "Clean." Chapter Complete
says Slice's piece: "You have an address. The circle." "Leased. By you." "Free." "Nothing's free." "Then pay with swings."
Then the file moves to Chapter 3. Both scenes (the opening and The Gate) are skippable with Enter or the Skip button. The gate
and wall are flat blocks with no colliders. Flow: `GateAndBack` in `ChapterRun.cs`, lines in `Story.Chapters[1]`, the boss
`Enemy.MakePlatedAlpha`.

**Slice walks with him (the companion).** In Chapter 2 Slice does not stay at the circle: she stakes the ring ("Ring's staked.
I'm coming.") and walks a step behind Wallit, on his right, with her scalpel out. She stabs and slashes what comes close, small
Yo Bots for real, the big ones for a flinch-cut and a shove. She is hurt the way he is, by the bots at her side, and when her
health (60) runs out she goes **down**: she lies where she fell ("Slice! Hold on!"), no bot finishes her, and she stays down until
Wallit stands over her and holds **E** for two seconds. She gets up with half her health and says "Noted." The ring she staked keeps
its bar and still ends the case if it is struck off, but the figure on it is the Slice beside him until the chapter ends. A chapter
turns a companion on with `Story.Chapter.Companion` (a `Companion.Defs` id); Chapters 1 and 3 to 6 keep her at the circle, as
written. Arcade has no companions. Code: `Companion.cs`; `companion` playtest.

### Chapter 3: The Clearing Floor

Where the bots come from. Wallit and Slice talk it over at the circle (the opening scene): the **Clearing Floor** is a sunken yard
beyond the broken gate where Yo Bots are stamped out, one batch for every unfiled thing in the Hollow. They are **the Clearing
Crew**: they chirp because every job closes with a report ("Uploading victory" means "filed as cleared"); plated and Mega Yo Bots
are the Floor sending the same unit again, heavier, when the cheap ones fail; the bolts they drop are their own pay, so Wallit has
been collecting what is owed. Slice stakes a second ring at the circle while he is out. The chapter is three stops, shown by the
objective line: **the broken gate** (36 m, bearing 200, a half-built Yo Bot on a hook: "I am... not finished!"), **the belt**
(70 m; the scene *The Belt*: cleared things ride past, a hat, a clock, a small bell, "That could be me." "It isn't."; the belt
runs into a chute with a red lamp that is a lens, and it turns to the circle) and **the lever** (105 m): pulling it breaks the
feed, the hooks swing empty. Then **back to the circle**, the last batch of six, and **The Foreman**, a Mega Yo Bot grown big and
amber with the order held on its chest, a wall of plate to break ("Order, read aloud: STRIKE. THIS. ADDRESS."). Defeat beat: "Order...
unread..." / "Paid in full." / "Clean." The file then moves to Chapter 4. Flow: `ChapterObjective.FloorRun` in `ChapterRun.cs`
(`Story.Stop` data, flat collider-free props built in code), lines in `Story.Chapters[2]`, the boss `Enemy.MakeForeman`; the
`chapter3` playtest plays it. Slice stays at the circle and does not follow (her second ring holds it); the Foreman reads the order
only in its lines, with no special attack yet.

### Boss phases

Every chapter boss has two phases. The boss bar carries a pale notch where the second begins (half health; 0.4 for the Server),
and crossing it is shown on screen: a red wash over the screen, the banner "PHASE TWO - <what changed>", a ring at the boss, a
hotter tint and a faster stride. The bar's name reads "- PHASE 2" from then on. No camera shake.

Every attack has a tell before it lands: red marks fill on the ground while the boss stands still and rings pulse; the blow lands
only when the fill reaches the rim. Slice says it once: "Red is where it lands. Move, or break it." Move out of a mark (or dodge on
X) and it misses; hit the boss for 8% of its health during the wind-up and it breaks ("Wind-up broken!"). Marks are flat, with no
collider. The marks: a **stamp** on Wallit, a **march** of three marks from the boss toward Slice, a **page** under Slice (as the
Courier's). Slice is hurt only by a march's last mark or a page.

- **The Courier** (ch. 1): notice on the circle. Phase 2: faster winds and two Yo Bots with every notice.
- **The Plated Alpha** (ch. 2): stamp. Phase 2: two stamps at once, wind-up 1.5 s instead of 2.2 s.
- **The Foreman** (ch. 3): march. Phase 2: a march and a page together ("overtime").
- **The Server** (ch. 4): phase 1 is the paper: it offers, is refused three times and only then strikes (unchanged). Phase 2 begins
  at 40% health: it serves in earnest with marches.
- **The Registrar** (ch. 5): page. Phase 2: a page and a stamp together.
- **The Order** (ch. 6): stamp. Phase 2: marches, and two Yo Bots called with each.

Code: `BossPhases.cs` (all but the Courier and the Server's paper), `Courier.cs`, `Server.cs`, `Enemy.PhaseAt`/`Phase`/`EnterPhase2`,
the notch in `RunHud`. The campaign playtests log `boss-phase`, `boss-tell`, `boss-land`, `boss-dodged`, `boss-interrupt` and fail a
boss that never changes phase or never shows a tell.

### Chapter 4: The Process Servers

Where the Spider Bots come from. The opening scene at the circle: the **Long Walk** is a causeway of stone slabs over a pit of mist,
six lamps in pairs along it (one per leg), a single path with no cover. Yo Bots clear; **Spider Bots serve**. They are **process
servers**, deliveries from the Department with one paper to hand over, and they stand and wait while it is read. Slice says plainly
what the paper is, for the first time: "My address. Struck." "Since when?" "Before you." (Wallit has no joke.) The scene *Served* shows
a Spider Bot stop a step short of the lamps, fold one leg and offer a paper on the tip of its claw, politely, the way a courier
would; Slice does not take it, and it waits. Wallit walks the three lamp stops (30, 62 and 95 m, bearing 330), the way turns round at
the last, and he holds the circle against the servers (Spider Bots and Plated Yo Bots). Then **The Server**, the first full Spider Bot,
arrives and **offers the paper and does not strike**. Refusing it means landing a blow on the Server while the paper is out (the
objective line counts "Refuse the paper n / 3"); it backs off and offers again. Only after the third refusal does it wind up and
strike Slice (`Server.StrikeDamage`), and then it is a plain Alpha to bring down. Until then its health cannot drop below 70%
(`Enemy.MinHealth01`), so it cannot fall unrefused. Defeat beat: "Delivery... undelivered... chirp?" / "Return to sender." / "Clean."
The file then moves to Chapter 5. Flow: `ChapterObjective.FloorRun` with `Chapter.Walk` in `ChapterRun.cs` (`BuildWalk`), lines in
`Story.Chapters[3]`, the boss `Enemy.MakeServer` + `Server.cs`; the `chapter4` playtest plays it. Slice stays at the circle.

### Chapter 5: Strike This Address

The Reading Room: a bare round hall with the circle on its floor at the Hollow's size, the walls lined with forms (all identical, all
this order), and an empty desk beside the circle. The opening scene has the Yo Bot read "STRIKE. THIS. ADDRESS." and the point is made
that **the order is not signed** ("Nobody signs it. That's how it works." / "Then it can be denied." / "By the guy with the stamp."),
and Wallit presses the RETURN TO SENDER stamp from the gate to the first page: the ink shows nothing, then DENIED, and every form turns
over blank (*The Stamp, Reversed*). Slice: "Wallit. That's a lot of nerve." / "Nerve's free. The swing's extra." The chapter is **five
pages**: each page is a wave of four bots (Yo Bots, Plated, Mega) held at the circle, announced PAGE n OF 5, and as it falls that
page's section of forms on the wall turns blank. The objective line reads "Read the order, page n of 5". Then **The Registrar**, a gold
Titan Spider Bot with a ledger held open, reads the pages with every leg and is a wall to break. Defeat beat: "Register... closed...
uploading... nothing?" / "Struck from the record." / "Clean." The Done card: the order is only a form, and a form can be answered
("Then we answer it. Pay up."). The file then moves to Chapter 6. Flow: `HoldThenBoss` with `Waves` in `ChapterRun.cs`
(`BuildReadingRoom`, `PageRead`), lines in `Story.Chapters[4]`, the boss `Enemy.MakeRegistrar`; the `chapter5` playtest plays it.

### Chapter 6: Address Filed

The Moonlit Hollow, changed: the wall is gone, a path of pale slabs leads out and stops at nothing, and the circle breathes a soft gold
ring. The opening scene has Slice say "The wall is gone. The circle is brighter." and Wallit "Then let's file it. Pay up." The chapter is
**three waves** of four bots (Yo Bots, Plated, Mega) at the circle, then **The Order**, the Spider Bot that carries the order (wave 30's
Alpha, a scroll with a red seal across its back). Defeat beat: "Signature... required... uploading... nothing?" / "Clean." Then *Filed*:
the ring becomes a line of writing, then a door number, which Slice writes with her scalpel; then the **Finale pages, verbatim from
`Story.Finale`** ("The Spider Bot comes apart. The order drops." ... "Pay up."), the **ADDRESS FILED** ending card, and a credits card
(`Story.Credits`). The case file is marked complete (6 cleared) and reads "(filed)" in the Campaign panel. There is no Next chapter.
Flow: `HoldThenBoss` with `Final` in `ChapterRun.cs` (`CompleteFinal`, `ShowCredits`, `BuildChangedHollow`), lines in `Story.Chapters[5]`,
the boss `Enemy.MakeOrderBearer`; the `chapter6` playtest plays it.

## New Game+ and the secret ending

**New Game+.** A filed case file (every chapter cleared) can be opened again from Chapter 1. Its chapter card gains a **New Game+** button
beside Difficulty. Pressing it is the Department re-opening the file: the chapters start over (Chapter 1, nothing cleared), but Wallit's
account stays: his XP, so his level and every ability, and **Refund in Full** if he earned it. The loop's own business is cleared so it can
be done again: the lost things, extras, Chime's errands, the back rooms and Hard. Best ranks, the lore log, time on the books and the
secret ending stay. The file reads `NG+1` (then 2, 3...). Code: `CampaignSave.NewGamePlus` (`Slot.plus`, save version 6),
`ChapterScene.StartNewGamePlus`.

**The secret ending: RECEIVED.** After ADDRESS FILED, if all **six lost things** are in the case file when Chapter 6 is filed (the
Unsent Postcard, Key With No Lock, A Cleared Hat, Thrice-Refused Paper, Ledger Leaf and The Circle's Stamp), one more scene plays before
the credits (`Story.SecretEnding`, `CampaignSave.SecretEarned`, `ChapterRun.CompleteFinal`). Wallit sets the six things down in the ring.
Slice: "No address." Wallit: "Then give it one. You've got the stamp." The stamp prints ADDRESS and nothing strikes it: every lost thing
in the Hollow gets a door. A Yo Bot, chirpy: "Delivery! ... Oh. It's for us?" Wallit: "Return to sender?" Slice: "Not this time.
Received." Wallit: "Received. In full. Pay up." The credits gain a RECEIVED block (Brim, Tick and Chime), and the file is marked
(`Slot.secret`, `RECEIVED` on the journal's counter). Without every find it never plays. A New Game+ loop clears the finds, so it is earned
again each loop. `newgameplus` and `secret-ending` playtests.

## New bots and mini-bosses in the chapters

Each chapter brings one new bot into its crew: every few bots in, one of the Yo Bots arrives as the chapter's own variant (named on
screen the first time, a Yo Bot chirps about it; `Chapter.Variant`, `VariantName`, `VariantEvery`; the elite machinery gives it its
behaviour). 1: **Dispatch Yo Bot** (fast, "Rush delivery!"). 2: **Returned Yo Bot** (comes apart into two drones, "Return to sender!").
3: **Reissue Yo Bot** (the Floor sending the unit again, tougher). 4: **Queue-Skip Yo Bot** (blinks ahead). 5: **Clerk Yo Bot**
(dodges the bat, "Page turned!"). 6: **Sealed Yo Bot** (a juggernaut, stamped shut).

Chapters 2, 3 and 5 also have a **mini-boss** midway, with its own health bar, who must fall before the chapter's boss is called:
**The Second Notice** (Ch2, a Plated Yo Bot that repeats the notice; half the bots down), **The Half-Built** (Ch3, a Mega Yo Bot off
a hook, "I am... not finished!"; once Wallit reaches the belt) and **The Clerk** (Ch5, a Spider Bot that files what it cannot read;
half the pages down). Data: `Chapter.Mini*`, `Enemy.MakeMini`, `WaveDirector.SpawnMini`. Arcade is untouched. Playtests log
`new-enemy`, `mini-boss` and `mini-boss-bar` in any chapter scenario.

## The places (chapter maps and the office)

Every place is its own Unity scene (`Assets/Maps/Map_*.unity`, built by `OwnMap` subclasses; map design sheets in `Docs/CampaignBible.md`).
Chapters 4 and 5 and 6 each have a boss arena on the far side of the circle from the route, a dais the boss walks out of.

- **The Long Walk (Chapter 4, `Map_Walk`)**: slate-blue dusk, a causeway of pale slabs over a pit of mist, six lamp stilts whose heads
  keep three folded legs, the last slabs fallen in. Arena: the Servers' Dais (bearing 150) with a paper stand. Set piece: the stand's
  paper lifts and its lamp burns violet whenever The Server holds its paper out.
- **The Reading Room (Chapter 5, `Map_Reading`)**: a bare round hall under a dark vault, lamp-gold. Five sections of forms on the wall go
  blank page by page; five lecterns lift their pages; a carpet aisle runs from the white door to the empty desk. Arena: the Registrar's
  Gallery (bearing 0) rises out of the floor after the fifth page.
- **The Moonlit Hollow, filed (Chapter 6, `Map_Filed`)**: dawn-blue grass, the Department wall gone to stumps, a pale path that stops at
  nothing, the circle in white. Arena: the Threshold (bearing 20), a white doorframe on a dais with the Order-Bearer's stamp marks.
  Set piece: when the chapter is filed the ring unrolls into a line of writing and a number, 1 (proposed: Wallit's first coin counted).
- **The office (the hub, `Map_Office`)**: the same walkable room (`ChapterHub`), dressed for the chapters filed. Window light goes from night
  to dawn, each door has a plaque in its chapter's colours and a red FILED stamp once cleared, the filing cabinets and paper stacks grow,
  the ring warms from blue to brass, and when all six are filed the brass door number hangs on the back wall.

## The hub and Wallit's account

Between chapters the file does not drop straight onto the next card. Chapter Complete leads **to the hub**: the Department's office, one
room with Slice's circle worn into the carpet at its middle. Slice stands in the ring with her knife out; Wallit walks in and they say
one line each about what he just did (`Story.HubArrival`, by chapters cleared: "One notice, returned. Where's my tip?" / "Board. Next case.").
Along the back wall there is **a door per chapter**, numbered 1 to 6. A door opens when the file reaches its chapter: filed chapters
(amber lamp) and the next one (green lamp) stand open, the rest (dark lamp) stay shut and Wallit cannot pass them. Walk through an open
door and that chapter's card comes up; Begin plays it. On the left wall hangs the **chapter board** of six slips: press E beside it for the
same list (*filed (replay)*, *next*, *locked*). On the right wall is Tick's desk (see "The lodgers"). A replay never takes the file back: `cleared` and the chapter the file is on only move forward. A file
with chapters behind it also has a **Hub** button on its card, so reopening a file reaches the office too. `ChapterHub.cs` builds it
(flat, collider-free, in code); `ChapterScene.cs` owns the card it leads to. The `hub-doors` playtest plays it.

Inside the circle Wallit is on the books, and the books keep an **account** for him: every bot he clears adds **XP** (5 for a Yo Bot, twice
its bolts for the heavier kinds, a boss pays 50 more), shown as `LV 2   XP 34/90` top left. A level is a clause the books accept: Wallit
earns seven abilities, all money (`CampaignProgress.cs`):

- **Spare Change** (level 2): +15% bat damage.
- **Refund** (level 3): press **C**; a ring shoves the bots off him and stings them (8 m, 10 s to recharge; bosses are stung, not shoved).
- **Receipt** (level 4): every sixth kill, Slice is paid back 6 health.
- **Chargeback** (level 5): +12% chance to crit with the bat.
- **Coin Toss** (level 4): press **V**; a coin flies at the nearest bot (the ones in front first) and stings it from range. 4 s.
- **Stamp Out** (level 5): press **B**; "DENIED!", a stamp on the ground stuns every bot within 7 m for 3 s (bosses for less) and stings them. 14 s.
- **Escrow** (level 6): press **G**; Wallit's account is held, nothing touches him for 5 s ("HELD"). 20 s.

Each active ability shows on the second line of the HUD (`X: Dodge   C: Refund 6 s   V: Coin Toss ready   B: LV 5`) with its key
and its cooldown counting down; a locked one reads `LV n`. A level-up writes the unlock into the case file (`Slot.unlocked`, a bit per
`Abilities` row), so it stays unlocked once earned.

Inside the circle Wallit also has two moves of his own, **Campaign only** (`CampaignMoves.cs`; `CampaignMoves.Enabled` is the flag):

- **The combo's three hits are distinct.** Deposit (a quick, light jab, 0.85x, short knock), Withdrawal (the backhand, 1.15x, shoves harder) and Overdraft! (the finisher, unchanged numbers); each has its own crack pitch and hit-stop and its name flashes on screen. Arcade's three swings stay as they were.
- **The dodge (X):** a 6 m dash along the stick (back, with none), untouchable for 0.4 s, 0.9 s to recharge. A blow that lands inside the window, a claw, a leg stab or the Courier's stamp taken for Slice, finds nothing ("MISSED ME"), and a bot at his shoulder cannot start climbing him.
- **Ability slots** (`CampaignProgress.SlotKeys`/`SlotFill`): X holds the dodge from the start, C takes Refund at level 3, V Coin Toss at 4, B Stamp Out at 5 and G Escrow at 6. A new active ability is a row in `Abilities` plus a case in `CampaignProgress.Use`. Touch buttons for X, V, B and G are not made yet.

Levels are 30, 90, 180, 300 and 450 XP (level 6 is the top). The account belongs to the case file (`CampaignSave.Slot.xp`), so three files hold three Wallits;
it is Campaign's alone. Arcade has its own boons and shop (`MetaProgress`) and none of this reaches it: outside a chapter every number
above reads as no effect. Slice does not level; she is already what the circle is.

## The lodgers

Four things that should not exist found the circle and stayed. They live in the hub (`Story.Lodgers`, `ChapterHub.cs`); walk up and press **E**. What
each says changes with the chapters the file has cleared (one line per chapter, six each). None is Warrok, and none fights. They are not office
workers: they are lost things, and they talk about what they lost.

- **Tick**, a clock, sits at the desk. Counts hours; each chapter he says what it did to the Department's time.
- **Brim**, a hat, hovers left of the desk. Wonders whose head it was; wants an address, and has one at the end.
- **Spare**, a half-built Yo Bot (the Half-Built's kind), chirpy and unfinished. Still cheerful about everything, including being free.
- **Chime**, a small bell like the ones on the belt, gives **errands**. After each chapter cleared she has one lost thing lying on the office floor (the Brass
  Clapper, Torn Postmark, Belt Token, Lamp Wick, Blank Form, Filed Stub). Wallit walks over it, carries it back, and Chime pays a tip in XP (20, 30, 40, 50, 60,
  80) and the thing is kept in the case file (`Slot.errands`, "LOST THINGS n/6" on the hub's top-left line). `CampaignProgress.Grant` pays the XP the way a kill does.
  Campaign only; the `hub-npcs` playtest plays it.

## Canon established here

- The lodgers: Tick (clock), Brim (hat), Spare (half-built Yo Bot) and Chime (bell) live in the hub; their lines change after each chapter. Chime's errands pay XP and a kept lost thing.

- The back rooms: each chapter's map hides one room the Department never filed, found by the clue on the chapter's lost thing. In Chapter 4's room, the Dead Letter (a Spider Bot carrying a paper that cannot be refused) guards Refund in Full, Wallit's one unique ability upgrade.

- The circle is the one filed address in the Moonlit Hollow; whatever is inside it exists.
- Wallit was stamped "Return to sender" and thrown in. He has no address of his own.
- Yo Bots and Spider Bots are sent to strike the circle's address. Wave 30's Spider Bot carries the order.
- The Place Department is the thing that files and clears. The creature behind the order is never named.
- The Stamp: Wallit arrives by stamp, RETURN TO SENDER landing on him out of the dark; the circle's ring lights as he rolls into it. Chapter 1 opens with it.
- Campaign is the story in chapters, kept in three case files; Arcade is the endless Hollow. Chapter 1 is "Return to Sender".
- The Courier: Chapter 1's boss, an Alpha Yo Bot with a clipboard that lands a notice of striking on the circle after a visible wind-up. Wallit's answer to a notice is "Return to sender."
- The gate: at the Hollow's edge the Department's wall answers a knock with the same RETURN TO SENDER stamp from the other side, and a postmark torn into the stone is half of Wallit's own.
- The Plated Alpha: Chapter 2's boss, a big Plated Yo Bot stamped INVALID. Its plate says INVALID, and Wallit says so does his refund.
- The Clearing Floor: a sunken yard beyond the broken gate where the Department stamps out Yo Bots, one batch per unfiled thing. Yo Bots are the Clearing Crew; they chirp because each job closes with a report ("Uploading victory" = "filed as cleared"); plated and Mega are the same unit sent again heavier; their bolts are their pay.
- The belt: carries flat cleared things (a hat, a clock, a small bell) into a chute with a red lamp that is a lens turned toward the circle. Pulling the lever breaks the feed.
- The Foreman: Chapter 3's boss, a Mega Yo Bot Alpha that reads the order aloud, an order sheet on its chest.
- The Long Walk: a causeway of stone slabs over a pit of mist, six lamps in pairs. Spider Bots are the process servers: deliveries from the Department, each with one paper (the address, struck) to hand over, who stand and wait while it is read. Slice: "My address. Struck." "Before you."
- The Reading Room: a bare round hall where the Department keeps every copy of the order. The order is not signed; the stamp pressed to a page turns it DENIED and the forms go blank.
- The Registrar: Chapter 5's boss, a gold Titan Spider Bot with a ledger that reads the pages with every leg.
- The Server: Chapter 4's boss, the first full Spider Bot. It offers the paper and will not strike until it has been refused three times.
- The hub: between chapters the circle is a walkable office with Slice in the ring, a door per chapter (open as the file reaches it, shut after), a chapter board of six slips (filed, next, locked) and Tick's desk and four lodgers. Cleared chapters can be replayed from it.
- Wallit's account: inside the circle he is on the books, and fights add to it (XP). Levels unlock Spare Change, Refund, Receipt, Chargeback, Coin Toss, Stamp Out and Escrow. Slice does not level. Campaign only, per case file.
- New in the chapters: one variant Yo Bot per chapter (Dispatch, Returned, Reissue, Queue-Skip, Clerk, Sealed) and mini-bosses The Second Notice (Ch2), The Half-Built (Ch3) and The Clerk (Ch5).

- New Game+: a filed file can start over from Chapter 1 keeping Wallit's level, abilities and Refund in Full.
- RECEIVED: the secret ending, shown after ADDRESS FILED only when all six lost things are kept; the stamp prints ADDRESS and the lost things get doors.

## The lore log

Campaign keeps a lore log per case file: what the Department's files say about the things the story has shown. Each entry is data in
`Assets/Scripts/Lore.cs` (`Lore.All`: title, the chapter that reveals it, text) and unlocks when that chapter is filed; three entries (the
circle, Wallit, the Department) are open from the start of a file. The Chapter Complete card says "New lore: ..." for what it unlocked. It is
read from the case files panel's **Lore log** button (`LorePanel.cs`, one entry per page, Prev/Next; sealed entries say which chapter opens
them). Bot origins live here: Yo Bots are the Clearing Crew (Chapter 3), Spider Bots are process servers (Chapter 4). The unseen creature
behind the order is never named in it. Unlocks are a bit per row in `CampaignSave.Slot.lore`, so never reorder or reuse a row. The case file
slot reads its chapter, time on the books, completion ("2/6 filed (33%)") and lore count. `lore` playtest.

## The journal

Every Campaign chapter hides one **lost thing** out in its field and offers one **extra** it never asks for. Both are data in
`Assets/Scripts/Journal.cs` (`Journal.Finds`, `Journal.Goals`, one row per chapter, never reordered) and kept per case file as a bit
per chapter (`CampaignSave.Slot.finds` and `.goals`; save version 3). A lost thing is a brass tag lying 50 to 75 m from the circle,
off the bearings the chapters use; there is no marker, only a soft ring when Wallit is near, and walking onto it keeps it at once.
An extra is judged when the chapter is filed and the Chapter Complete card says whether it was filed or missed (it can be tried
again from the board). Extras: Clean Hands (Slice untouched, Ch 1), Long Ball (2 home runs, Ch 2), Ground Rules (6 smash hits, Ch 3),
Finishing Touches (12 combo finishers, Ch 4), Quick Reader (filed in 9 minutes, Ch 5), Last Word (4 home runs, Ch 6). Lost things:
Unsent Postcard, Key With No Lock, A Cleared Hat (Brim's), Thrice-Refused Paper, Ledger Leaf, The Circle's Stamp.
The **journal** (`JournalPanel.cs`) has three tabs, Active, Finished and Collected. J opens it in a chapter (over the pause menu,
which also has a Journal button) and in the hub; the case files panel has a Journal button too. Every find and every extra is
**Paid in Full**: the journal and the case file slot say PAID IN FULL and Wallit's bat wears gold bands in every chapter
(`BatMarks`, via `Journal.GiltBat`). Voice: the Department's filing language; Slice clipped ("Clean."), Wallit on money. `journal` playtest.

## The back rooms

Every chapter's map keeps one **back room** the Department never filed: a small paper-floored room, 172 m from the circle (past the 150 m
playfield, inside the wall), with a doorway that faces the circle, a desk, pigeonholes and one amber lamp. It is **not on the map** until the
chapter's lost thing is in the case file. That lost thing is the **clue**: kept, it names the room's bearing and distance ("Door 1. 172 m
south-west of the circle. Knock twice."), the screen says it, the journal's Active tab lists it, and the room stands up with a soft gold ring
that pulses from 40 m. Walking in keeps the room (a bit per chapter, `CampaignSave.Slot.rooms`, save version 4), pays XP the first time (20,
25, 30, 35, 40, 50) and says one line. The rooms: **The Sorting Nook** (Ch1), **The Forgotten Door** (Ch2, the key fits), **The Spare Parts
Bin** (Ch3), **The Dead Letter Box** (Ch4), **The Closed Register** (Ch5, blank pages), **The Last Address** (Ch6, the circle's own door number,
not struck). They are flat and collider-free like the chapters' other props. The journal's Collected tab keeps each one; the case file shows
`Rooms n/6`. Paid in Full does not need them.

**The Dead Letter** is the one optional hard encounter. In the Dead Letter Box a Spider Bot steps out: a paper with no address to hand over,
which cannot be refused (so it is not the Server's game). It is a mini-boss with its own health bar (64 hits, violet), and it walks for the
circle. Bring it down and Wallit earns **Refund in Full**, once, kept in the case file (`rooms` bit 6): Refund's ring grows to 12 m, it
recharges in 6 s instead of 10, and Slice is paid back 8 each time. It reads "Refund in Full" on the HUD. Replaying Chapter 4 finds the room
open and the Dead Letter gone; the reward is never paid twice. Wallit: "Returned. In full. Pay up." Slice: "Clean." Data: `BackRooms.cs`
(`BackRooms.All`, the `BackRoom` component on the chapter run, `ChapterRun.Events` lines `secret|...`), `CampaignProgress.RefundFull`,
`WaveDirector.SpawnMiniAt`; the `secrets` playtest plays all six rooms and the Dead Letter.

## The cast file

Every voice in a dialogue box is a row in `Assets/Resources/Cast/cast.json`: Wallit, Slice, the Yo Bots (Yo Bot, Mega Yo Bot,
the Half-Built) and the named bosses (the Courier, the Plated Alpha, the Foreman, the Server, the Registrar, the Order-Bearer),
plus the narrator. Warrok is retired and has no row. Each row has a speaker code, name, colour, portrait path and voice hook.

### Cinematics

A cinematic is a reel of shots, not pages: each shot is a still (or a streamed video) with a slow Ken Burns move, a front layer that
drifts faster than the back, a crossfade in, letterbox bars, one voice line and a music level. The line is typed as a subtitle,
so the words are never the picture. A shot can also hand back to the game itself (`world`: wallit, slice or wide). Voices keep
the cast: Wallit money, Slice clipped, Yo Bots chirpy; Slice and Wallit lines go through the voice director one at a time.
Reels live in `Assets/Resources/Cinematics/<id>.json` and play with `Cutscene.PlayShots(id, done)` (`CutsceneShots.cs`);
`sample` is the Chapter 1 opening (the stamp, "Ow! That's extra!", the ring, "You're inside. Inside, you exist.") on placeholder art
from `Tools/Cinematics/make_placeholders.py`. Space or a click goes to the next shot; Enter, Escape or Skip leaves at any point.
The chapters still use their page scenes; moving each chapter's `opening` and `closing` shot lists from the screenplay into reels
is the next part, once the art is made.
