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
run ends when Slice falls. That is the whole game: Wallit swings, Slice holds the ring. The run-end screen still says
**Address struck off**, then counts the waves and coins, stamps a new best, shows the next goal on the ladder, and lets
Wallit and Slice trade one short voiced line about how it went before **Retry** starts the next run.

## Who comes

**Yo Bots** are the Department's chirpy clearing crew ("Uploading victory!"). They come in small, then Plated, then
Mega. **Spider Bots** are the big ones: they walk on six legs and stab. Both carry the same order: strike this address.
The creature behind the order is never seen and never named.

## The thread

Every wave start gets a short beat. A beat is one to three lines, one speaker each, shown for about two seconds.
Boss waves (10, 20 and 30) get a title card and a longer beat.

- **Act one / Night Shift, waves 1 to 10: the arrangement.** Wallit is here for the coins, Slice is here for the circle. The
  Yo Bots are cheerful about erasing them. Wallit works out that the bolts the bots drop are a wage, and Slice admits she
  needs him. She starts calling threats in one word ("Left." "Behind." "Mine.") so he can see what she sees (`SliceCallouts`).
- **Act two / Cold Hour, waves 11 to 20: the plating.** The Plated Yo Bot arrives on wave 10; Cold Hour opens at 11. Slice is
  hurt and says so. Wallit stops talking about himself and starts talking about her debt ("I'm covering you"). She can
  parry one bot per wave that reaches her circle or flanks him: a block pose, a metallic clink, the bot knocked back 2 m.
  The first Cold Hour wave she shows the parry on camera once.
- **Act three / Last Light, waves 21 to 30: the order.** The Mega Yo Bot arrives on wave 20; Last Light opens at 21. Yo Bots
  stop chirping and read the order aloud. Wave 30 is the Spider Bot that carries the order. She stamps the ground once per
  wave and stuns nearby bots for 1.5 s; the first Last Light wave she shows the stamp on camera once.

### Slice as partner (callouts and guard)

Slice is not only what Wallit protects. In Arcade and in chapters where she walks with him (`Companion`), she names the
bot that is about to reach her circle or flank him: one clipped word through `VoiceDirector` at bark priority (never
stacked), a short-lived ring marker in her accent colour on that bot, and an off-screen edge arrow. Wallit answers some
of those with money lines ("On it." "Billable."). Guard moves unlock by Arcade act (Night Shift callouts only; Cold Hour
adds the parry; Last Light adds the stamp). Code: `SliceCallouts.cs`. The `slice-partner` playtest proves three voiced
callouts and one Act 2 parry.

### Arcade boss set pieces (waves 10, 20, 30)

Each decade boss is a short set piece, not only a title card. The Alpha is drawn from the campaign roster so the fight
matches the story's weight: **wave 10 The Plated Alpha**, **wave 20 The Foreman**, **wave 30 The Order**. On spawn the
lights dim, the camera dollies out to the boss for about four seconds (skippable with one key or tap), and Wallit and
Slice trade a voiced line through the cutscene voice key (wave 10 is Slice's first crack and Wallit's debt; wave 20 the
order read aloud with Yo Bots joining in; wave 30 the sealed scroll). The boss bar carries the pale phase notch; half
health is the shared red-wash phase-two tell (`BossPhases` / `BossSetPiece`). On defeat: hit-stop, half a second of slow
motion, a bolt burst and Slice's line; wave 30 also drops the order on the grass with a red DENIED blotch (Wallit's
DENIED read stays on the finale card so it is not said twice). The wave clear and next act banner wait until the
payoff ends (`BossSetPiece.PayingOff`). Code: `BossSetPiece.cs`, hooked from `WaveDirector.SpawnOne` and
`Combat.OnKill`. The `boss-set-pieces` playtest walks 10/20/30 with the wave key.

### Boss entrances

Every named boss arrives as an event, 4 to 8 seconds on the `CinematicDirector` (`CinematicBoss.cs`). Time slows, the camera
leaves Wallit's shoulder and swings to the boss arriving, the boss plays its rig's signature move (the rear-up tell, then the
slam), its name and title are stamped across the upper third in the Department's typewriter face, a stinger hits on the slam
and the boss track follows it, the boss says its taunt (the first of its existing lines, in its own voice file), and the play
camera comes back in 0.3 s. Any tap, click or Enter/Space skips; the music still turns. Boss health, phases and tells are
untouched: the boss is held while it plays and put back as it was. Once per Arcade run, once per case file (a replay of a
cleared chapter plays it again).

| Boss | Card | Arrives | Taunt |
|---|---|---|---|
| The Courier (ch1) | THE COURIER / Notice of striking | rises out of the drop-box ring | "Special delivery! Notice of striking!" |
| The Plated Alpha (ch2, wave 10) | THE PLATED ALPHA / Plate 0010: INVALID | drops in | "Notice! This plate is stamped: INVALID!" |
| The Foreman (ch3, wave 20) | THE FOREMAN / Work order 0031 | unfolds off the belt | "Order, read aloud: STRIKE. THIS. ADDRESS." |
| The Half-Built (ch3 mini) | THE HALF-BUILT / Still on its hook | swings down off its hook | "I am... not finished! Finish me?" |
| The Server (ch4) | THE SERVER / Signature required | climbs over the wall | "Order delivery! Signature required!" |
| The Registrar (ch5) | THE REGISTRAR / Page ten, filing | unfolds | "Registrar on the floor! Please remain seated!" |
| The Order-Bearer (ch6, wave 30) | THE ORDER-BEARER / Sealed. Carried. Unsigned. | climbs over the wall | "Order delivery! Signature required!" |

After the taunt, Slice's and Wallit's replies (the rest of the chapter's boss lines) follow in the field as before. At wave
10 the entrance's slam is also Slice's first crack. Each boss has its own stinger root so no two entrances play the same
sound. The `boss-entrances` playtest plays all nine (three Arcade, seven campaign cards including the Registrar).

## The finale: wave 30

Wave 30's boss is **The Order** (the Spider Bot that carries the sealed order). When the last bot of wave 30 falls, the
order drops, and Wallit reads it: "Strike this address", stamped DENIED. Slice files the circle herself. The ending
screen reads **ADDRESS FILED**, and gives the run:
waves, level, streak, coins. Any key goes back to the menu; **Enter keeps going** into overtime, where the waves
continue and the run still ends when Slice falls.

## The intro

There is no text intro. An Arcade run that begins at wave 1 opens with a **cold open** of about 4 s (never over 8 s), played
in the game scene with the real Wallit, Slice and a Yo Bot (`ColdOpen.cs`, no stills, no pages):

1. The RETURN TO SENDER stamp (`stamp_heavy`) drops onto Wallit from above the circle.
2. He lands in the circle and the ring lights: soft rings out from Slice's feet, no sparks.
3. Slice says one line that is already in her bank, "Stay close." Its subtitle is the only text, in the caption band (the top
   band on phones, clear of the pads).
4. The first Yo Bot is already rushing in. Wallit speaks no line here; wave 1 adds no beat text and no second voice.

Control is never taken. Any key, tap, stick push or swing ends it on that frame, and Wallit is put where he would have
landed. It plays once per browser (`hawkes.coldopen`, PlayerPrefs, so localStorage on the web) and Enter or a tap skips it.
The player is swinging at a Yo Bot within about 10 s of pressing Play and has read nothing.

What the six pages used to say now lives in the lore log (Lore.cs, "The First Night", open from the start of a case file): the tagline,
Wallit's "One wallet, one bat, no refunds", Slice's "This is the circle: the one address in the Hollow that's filed. Inside it,
things exist. Outside, they're cleared. If I fall, the circle goes, and everything in it. Don't let me fall.", the Yo Bot's
"Hello! Uploading victory!" and "Guard Slice. Swing at anything chirpy. Pay up." Campaign chapters keep their own openings.

## Run-end exchanges (Arcade)

When Slice falls in Arcade, the run-end screen (`RunEndScreen.cs`) keeps the title **Address struck off**. Waves reached and
coins count up with sound; a new best gets a heavy stamp. A short goals ladder (`Resources/Data/run_goals.json`, twelve
rungs: reach waves 5/10/20/30, clear each act boss, career home runs and more) shows the next goal with progress and stamps
any goal just finished; unlocks are coins or a bat look/trail in `UpgradeLook` / `BatMarks`. Then Wallit and Slice trade
**one** short voiced exchange picked from facts about that run. The same exchange does not repeat within five runs
(`RunEndTalk`). Retry is one tap and starts the next Arcade run at once. Voices: Wallit money, Slice clipped. Data lives in
`Story.RunEnds` (change a line here and there). Conditions are checked in priority order; the first match that is not in the
last five plays.

| Id | When | Wallit | Slice |
|---|---|---|---|
| early | fell before wave 5 | Short shift. No tip. | Again. |
| yo_bot | a Yo Bot landed the last blow (not a boss) | Chirpy took the ring. | Noted. |
| spider | a Spider Bot landed the last blow (not a boss) | Six legs. One bill. | Bigger next. |
| boss | a boss landed the last blow | Big one cashed out. | Hold. |
| new_best | this run set a new best wave | New high. Still short. | Closer. |
| nearly | Slice was on the brink (≤30 HP) earlier in the run | You almost fell. | I didn't. |
| near_decade | wave ends in 8 or 9 (one stamp from a decade) | One stamp from the next. | Then take it. |
| act2 | fell in act two (waves 10–19) | Plates cost extra. | Cover me. |
| act3 | fell in act three or overtime (wave 20+) | Order's still out. | Denied later. |
| payday | fifteen or more coins this run | Purse is fat. Ring's gone. | Spend it right. |
| longball | three or more home runs this run | Returned a few. | Keep swinging. |
| default | fallback | No refunds. | Clean. Again. |

The subtitle on the screen is one line: `WALLIT  …` then `SLICE  …`, never both at once. Playtest: `run-end`.

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

## Quiet first: how a chapter is paced

The Hollow is mostly quiet. Yo Bots do not stream in on a timer: they are delivered, and a delivery is a place, not a clock.

- **The opening is always calm.** Wallit walks (the place is shown before it is fought over), the opening scene is the talk,
  and he picks up the chapter's **Receipt**, a lost slip lying on the route ("Receipt. Proof I was here." Wallit keeps
  money-minded things). No bot is awake until the three are done.
- **At most three deliveries, then the boss.** Each delivery is a few bots (and, in Chapters 2, 3 and 5, the mini-boss)
  asleep with their lids down at a fixed spot: on the route in Chapters 2, 3 and 4, out from the circle in 5 and 6.
  They wake when Wallit walks up to them. The first one says "Hello! Uploading victory! Striking this address now!" (Yo Bots
  stay chirpy). A delivery he walks past stays asleep and is put away; the mini-boss moves to the next one.
- **A breath between.** After a delivery falls, ten quiet seconds pass before the next is even placed. Slice and Wallit use
  them (Slice: "Clean."; Wallit: money). The boss comes when the deliveries are down, as before.
- Chapter 1 is its own level (one delivery, the Clearing Crew, then the Courier). Arcade is unchanged: its waves still come.

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
striking". Yo Bot: "Special delivery! Notice of striking!" Slice names it ("The Courier. It reads the order out, then it lands.") and
Wallit answers with the bill ("Then it can read me the bill."). It never swipes. It walks to 7.5 m of whoever the notice is for, stops
and lifts the notice overhead; a **red mark fills on the ground under them** (the telegraph, about two seconds, a ring pulsing over
it), and when it fills the notice lands. Two answers, which the screen names the first time ("Break it, or stand in it") as Slice
says "It's reading it out.": hit the Courier for a tenth of its health before the mark fills (it drops the notice, "Notice dropped!",
and stands dazed; Wallit: "Not on my tab."), or stand in the mark and take the stamp yourself ("Wallit takes the stamp"; Slice is
spared; Wallit: "Nobody touches her."). Under half health (the PHASE TWO banner) the Yo Bot voice says "Second notice! This address
is still unknown.", it winds up faster and calls two Yo Bots with every notice ("Return to sender! Return to sender!"). The defeat
beat: the Courier, coming apart, "Notice... undelivered... uploading... defeat?" (speaker K, its own voice); Wallit "Return to
sender."; Slice "Clean." The fight is `Courier.cs`; the lines are `Story.Chapters[0]` (`BossLines`, `DefeatLines`) and the
`Story.Courier*` lines.

**In the Sorting Yard (Chapter 1's level, `LevelYard.cs`).** The Courier is the drop's boss, after the Clearing Crew. When the five
Yo Bots are down there is a breath (Slice: "Big one next. Be ready."; alone, Wallit: "Something big's coming. I can smell the
invoice."), then the drop box rattles (`courier_clip`), a ring opens behind it and the Courier steps out with its clipboard. It stands
asleep while it reads the arrival lines, then walks straight at Wallit (a blow wakes it early). There is no circle here, so the
notice is for Slice walking with him: the red fills under her (the first notice reads slower, three seconds, for a thumb on a
phone), and if it lands she goes down ("Slice! Hold on!") until Wallit revives her with a held E. While she is down, the notice is
for Wallit himself: step out of the red or break it. The objective line shows its health ("Break the Courier [||||....]"). Called
Yo Bots never number more than three at once and come apart with it when it falls. If Wallit goes down, the Courier goes back
behind the drop box asleep, keeping the damage done, and wakes when he walks back. When it falls its notice drifts down, unsigned,
the return slot's shutter lifts (`door_open`, the amber light comes up) and the defeat lines play. E on the notice keeps it
("Kept: the notice."; Wallit: "Evidence. Every bill has a paper trail.", choice 1.c1 keep); leaving through the slot without it
voids it (1.c1 tear). The `campaign-beats` playtest plays it end to end (`boss-arrive`, `boss-wake`, `boss-call`, `boss-down`,
`notice-down`, `exit-open`; metrics `courierWinds`, `courierInterrupted`, `courierBlocked`, `courierLanded`, `courierSeconds`).
In the Hollow (the old chapter run, `HoldThenBoss`) the same Courier walks to 7.5 m of Slice at the circle and the notice lands on
her for 12.

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

### Boss barks

Each campaign boss talks back in its own eleven_v4 voice during the fight (`BossVoice.cs`), never over Wallit or Slice
(`VoiceDirector` reasons `boss-phase`, `boss-homerun`, `boss-win`). Three short paperwork lines each (under ten words),
one at phase two, one when a home run lands on it, one when Wallit goes down (or Slice falls with that boss live). Captions
sit in the upper third on a phone. Yo-family bosses (Courier, Plated Alpha, Foreman) get a light robotic high-pass in the
mixer, not baked into the file. Voices: Courier chirpy postal clerk; Plated Alpha clipped metallic; Foreman gravelly site
boss; Server polite and insistent; Registrar dry and final; Order-Bearer slow and ceremonial. The `boss-barks` playtest
checks distinct voice ids and at least two barks per fight.

### Boss phases

Every chapter boss has two phases. The boss bar carries a pale notch where the second begins (half health; 0.4 for the Server),
and crossing it is shown on screen: a red wash over the screen, the banner "PHASE TWO - <what changed>", a ring at the boss, a
hotter tint and a faster stride. The bar's name reads "- PHASE 2" from then on. No camera shake.

Every attack has a tell before it lands: red marks fill on the ground while the boss stands still and rings pulse; the blow lands
only when the fill reaches the rim. Slice says it once: "Red is where it lands. Move, or break it." Move out of a mark (or dodge on
X) and it misses; hit the boss for 8% of its health during the wind-up and it breaks ("Wind-up broken!"). Marks are flat, with no
collider. The marks: a **stamp** on Wallit, a **march** of three marks from the boss toward Slice, a **page** under Slice (as the
Courier's). Slice is hurt only by a march's last mark or a page.

- **The Courier** (ch. 1): notice under Slice (the circle's ground in the Hollow; under her as she walks in the Sorting Yard, or
  under Wallit while she is down). Phase 2: faster winds and two Yo Bots with every notice.
- **The Plated Alpha** (ch. 2): stamp. Phase 2: two stamps at once, wind-up 1.5 s instead of 2.2 s.
- **The Foreman** (ch. 3): march. Phase 2: a march and a page together ("overtime").
- **The Server** (ch. 4): phase 1 is the paper: it offers, is refused three times and only then strikes (unchanged). Phase 2 begins
  at 40% health: it serves in earnest with marches.
- **The Registrar** (ch. 5): page. Phase 2: a page and a stamp together.
- **The Order** (ch. 6): stamp. Phase 2: marches, and two Yo Bots called with each.

Code: `BossPhases.cs` (all but the Courier and the Server's paper), `Courier.cs`, `Server.cs`, `Enemy.PhaseAt`/`Phase`/`EnterPhase2`,
the notch in `RunHud`. The campaign playtests log `boss-phase`, `boss-tell`, `boss-land`, `boss-dodged`, `boss-interrupt` and fail a
boss that never changes phase or never shows a tell.

### Dialogue choices

Eleven choices from the screenplay (`Docs/CampaignBible.md`, "Choice flags") are asked in the dialogue box: a page with the question
and two buttons (keys 1 and 2, a click or a tap; Enter still skips the scene and nothing is saved for a skipped choice). The pick is
saved in the open case file (two bits a choice, `CampaignSave.Slot.choices`, save version 7), the pick's own reply plays at once, and
a choice is asked once per file (New Game+ asks again). Scenes are data (`Story.Chapter`): `Cutscene`/`GateScene`/stop scenes,
`Crossroads` (played as the boss is called, before its intro reel), `After` (played when the boss falls, before the closing reel and
the card). `Q|<id>` asks a choice; `?|<id>:<key>|<speaker>|<text>` is a line that plays only if that option was taken (Done card,
`PhaseTwo` lines the companion says when the boss changes phase, the ending's notes after ADDRESS FILED). `Choices.cs` holds the rows.

| Chapter | Asked | Read later |
|---|---|---|
| 1 | Keep or tear the notice; tip Slice or keep the bolts (After) | Ch1 Done card; Ch5 Reading Room opening (the notice, the page off the wall); Ch3 belt (Slice on the tip); Ch6 last stamp |
| 2 | Knock once or hard at the gate (Gate scene); hold at the ring or go out to meet the Alpha (Crossroads) | Gate scene and Done card; Slice's phase-two line; Ch4 Done card ("Three knocks on record") |
| 3 | The true order or the hat-tag for the Foreman to read (Crossroads); leave or unhook the Half-Built (After) | Slice's phase-two line; Done card |
| 4 | Refuse or take the Server's paper (Crossroads); who answers for Slice (After) | **Outcome:** a taken paper means the Server strikes after two refusals, not three; phase-two line; Ch5 opening |
| 5 | Read the first page aloud or silent (Crossroads) | Slice's phase-two line ("It knows you.") |
| 6 | Who presses the last stamp; one door number or two (After) | The ending notes after ADDRESS FILED |

Slice's replies are the companion reacting. Playtest `choices` plays Chapter 1 and its choice pages, reads the card, reloads the file,
puts up the later pages and reads every reader under both options.

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
Alpha, a scroll with a red seal across its back). Defeat beat: "Signature... required... uploading... nothing?" / "Clean." Then *Filed*
as a short **playable beat** on the live Hollow (`CinematicDirector`, `ending_filed_world`, `finaleCarry`): the camera settles on Slice's
ring; Wallit **holds swing** (hold the Swing pad on touch) to carry the stamp to her circle while the Order-Bearer's last notices rain
down; he bats away **three** with timing prompts (a miss only knocks him back — the beat cannot fail). On release the stamp prints her
address on the ring, four or more lamps around the ring light one by one, Slice says her existing closing line ("Clean. Good swing,
Wallit.") in her voice, and the Chapter 6 theme swells into the **ADDRESS FILED** ending card and credits (`Story.Credits`). The beat
lasts about 20 to 40 s; existing Finale lines only, no new voice. The **RECEIVED** path after ADDRESS FILED is unchanged. The case file
is marked complete (6 cleared) and reads "(filed)" in the Campaign panel. There is no Next chapter.
Flow: `HoldThenBoss` with `Final` in `ChapterRun.cs` (`CompleteFinal`, `ShowCredits`, `BuildChangedHollow`), lines in `Story.Chapters[5]`,
the boss `Enemy.MakeOrderBearer`; playtests `chapter6` and `finale-carry`.

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

## Endings and New Game+

**Three ADDRESS FILED endings.** When Chapter 6 is filed, the case file's choices and hub errands pick one closing reel, played after
the `ch6_close` reel and before the ending notes and the ADDRESS FILED card (the card's line changes too). `Endings.cs` decides:
- **Two Doors** (`ending_doors`): 3 or more company points from tipping Slice (1.c2), reading the page aloud (5.c2), handing her the
  stamp (6.c1) and asking for two numbers (6.c2). Slice writes the number twice; the second door sits beside the first.
- **The Open Door** (`ending_open`): otherwise, 6 or more of the hub's side slips and favours done (`HubLife.SideCount`). Every lodger is on the books.
- **The Ledger** (`ending_ledger`): everything else. Wallit's stamp, his door, one name. Slice keeps the ring.
Voices stay as ever: Wallit money, Slice clipped ("Clean.", "Numbered."), Yo Bots chirpy. The three variant reels stay data
(`Tools/Cinematics/make_endings.py`); real keyframes named `ending_<variant>_<n>_back` replace stand-ins when they exist.
**Chapters 4–6 openings and the main ADDRESS FILED / RECEIVED payoffs are in-engine** (`CinematicDirector`,
`ch4_open_world` / `ch5_open_world` / `ch6_open_world`, `ending_filed_world`, `ending_received_world`): the Server offers its
paper on the causeway; ADDRESS FILED's stamp is **carried by the player** (hold swing, bat three falling notices, release to print
ADDRESS and light the ring lamps) before the ending card; six doors rise for RECEIVED; credits scroll over Wallit and Slice with
the credits score (still reels remain as fallback / load-card art only).

**Ratings and the chapter select.** The hub's chapter board lists each filed chapter with its best rank (and Hard rank); the case files list shows all six ranks per file.

**New Game+ from the case files.** A filed file has a New Game+ button on its row. Each loop sends bots with +25% health and +20% more bots (to
four loops, `CampaignGrade.PlusCount`/`PlusHealthMult`), and every chapter opens with one line that remembers the ending of the loop before
(`Endings.EchoFor`, saved as `Slot.ending`, save version 9; older files derive it from their choices and errands).

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

**The hub is alive (`HubAmbience.cs`).** No waves, no timer: the office is a place. While Wallit is about, each lodger keeps to its own idle
action: Tick swings like a pendulum and now and then strikes the hour, Brim looks about and tips a hat it does not have, Spare tinkers with
its one claw and hops with a chirp, Chime rocks on its loop and rings. Step into Slice's ring and she and Wallit trade a short exchange
(`Story.HubRing`, two per chapters-cleared stage, taking turns, none repeated from an earlier stage): after one notice, "Standing in your
ring. Rent's due." / "Paid. In kind. Stay."; after all six, "Number on the wall. Mine?" / "Yours. Keep the receipt." Wallit money, Slice
clipped. The `hub-ambience` playtest plays it.

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

## The hub after the chapters

The office is a place to stay in. The lodgers (Tick, Brim, Spare, Chime) each have a short arc of three optional conversations
(`HubLife.Chats`); two new ones open after every chapter, and a gold **!** hangs over a lodger with one unheard. Press E: the whole exchange
shows (Wallit, the lodger, sometimes Slice) and is kept in the case file (`Slot.chats`). Tick learns he may keep his own hour; Brim
wants a wall with a hook; Spare finds out a spare is what you keep in case somebody needs it, and somebody does; Chime rings for
what is missing, and at the end for the right address.

Each chapter also leaves **three errands**: Chime's lost thing (as before), a **favour** (a lodger asks for a lost bit: the Yo Bot Lid, Hatband,
Mainspring Pin, Chirp Whistle, Address Label, Winding Key; carry it back) and a loose **notice** (a slip lying in the room; walk over it and it
is pinned to the left wall). Favours and notices pay XP and are saved (`Slot.side`, save version 8). The office shows the story: the
desk is taken by a different lodger as the file advances (Tick, Spare, Brim, Tick again) and the others move; a slip appears on the
wall for every errand finished and a lamp lights on the right wall for every conversation had, on top of the window light, stamps
and cabinets. The ending reads what was kept: `?|side:<id>|`, `?|sides:12|` and `?|chat:<id>|` lines in `Story.Chapters[5].Ending`
(Spare's lid on straight, Brim's own name on its label, Tick wound, twelve slips pinned, Chime's quiet bell). New Game+ clears them.
Code: `HubLife.cs`, `ChapterHub.cs`, `OfficeMap.Dress`; the `hub-life` playtest plays all of it.

Voices and sound: the lodgers each have their own voice (Tick a measured wound-clock, Brim a muffled, wistful hat, Spare a small chirpy half-built Yo Bot, Chime a clear, ringing bell), spoken through `CastVoice` for every `Story.Lodgers` line and every `L|` chat line; the hub and each chapter carry a quiet ambience under the score (hub a ticking room, the Hollow wind, the belt yard's rumble, the Long Walk's lamp-buzz, the Reading Room's paper hush).

## Arcade's three acts

The long night has three hours. **Act I, Night Shift** (waves 1-10): moon-blue, the Yo Bots come chirping up the hill, "Uploading victory." **Act II, Cold Hour** (11-20): the light turns teal and the Spider Bots come down the wall; Slice: "Legs. Count them." **Act III, Last Light** (21-30): a bruised dusk, orange at the rim, and no crew is plain any more: Plated, Mega and Spider Bots wear elite crests. Wallit: "Pay up." After wave 30 the clock will not clock out: **Overtime** is the same three hours in red ink, and the banner says so. Light and sky only: no weather, no sparks, nothing solid in the playfield.

### The act changes

When the wave 10 and wave 20 bosses fall, the night does not cut to the next hour: it turns, in front of Wallit and Slice (once per run per act, about 6 s, a tap skips it). The camera lifts over the whole Hollow with both of them in frame, the sky and light blend to the next hour and the score follows. **Cold Hour:** the lamp posts go dark and flare back on one by one, fog rolls in over the hedges; Slice: "Legs. Count them." Wallit: "Pay up." **Last Light:** the ruin's red beacon lights and starts to sweep the lawn, and paper begins to burn at the edges; Wallit: "Nobody touches her." Slice: "Clean." The card is stamped in the Department typeface, ACT II / COLD HOUR or ACT III / LAST LIGHT, on a bar of the score. Light, smoke and big soft glows only: no sparks, no shake, nothing solid on the lawn. `CinematicAct.cs`; playtest `act-change`.

## Canon established here

- Arcade's acts: Night Shift (1-10), Cold Hour (11-20), Last Light (21-30), then Overtime in red ink; Spider Bots arrive in act II and elites of every kind in act III.

- The lodgers: Tick (clock), Brim (hat), Spare (half-built Yo Bot) and Chime (bell) live in the hub; their lines change after each chapter. Chime's errands pay XP and a kept lost thing.
- The lodgers' arcs: Tick keeps his own hour (a Winding Key), Brim wants an address (a Hatband, then an Address Label), Spare is a spare kept in case somebody needs it, Chime rings for the right address. Their favours and the loose notices change the ending's lines.

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

## The stakes

Losing Slice is losing the game, and the game never says so. When a bot reaches her the circle's ring (a soft gold ring on the ground
round her, `StakesFeel.cs`) stutters and dims toward grey, and the screen's edges drain of colour in proportion to the health she has
lost (`NightGrade.Stakes`, read by `BTPost.shader`). Under 30% a low heartbeat layer rises under the music. The first time a bot
reaches her in a run Wallit says one short key line about her, in his money voice, and never over another line: "That's my account!
Hands off!", "Touch her, you pay double!" or "She's not for sale. Back off!" (`WallitVoice.Stakes`, reason `stakes-line`). No text
explains the circle in wave 1. `stakes` playtest logs `slice-hit`, `slice-low`, `stakes-line`.

## The fee

Slice's pay is a pile of gold coins at the edge of the circle: **the fee**, the filing fee the Department set aside to keep the
address open, and the thing Wallit is paid to guard ("I'm covering you"). It is the stakes made into something you can count.
The pile's height is her health (24 coins, `CoinPile.cs`). When a Yo Bot or Spider Bot's blow lands on Slice, the top coins come
off the pile, arc through the air to the bot that struck her and vanish in a soft gold ring with a coin clink: the bot takes its
cut. Wallit's barks ("Touch her, you pay double!") only back this up. When she heals, the coins drop back one at a time. The
pile is chunky turned discs, no sparks, inside the ring and clear of the playfield's bots. No text. `stakes` playtest logs
`coin-pile`, `coins-taken` and the metrics `coinPileTaken`, `coinPileAfterHit`.

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

**In-engine openings (Chapters 1–3).** Directed scenes on `CinematicDirector` (`Resources/Cinematics/chN_open_world.json`):
- **Chapter 1 (Sorting Yard):** Wallit walks in on the west trail, Slice turns in her ring, the Courier raises its clipboard, and
  the RETURN TO SENDER stamp lands on Wallit at the circle. Eight shots. Playtest: `opening-scene`.
- **Chapter 2 (Hollow edge):** Wallit and Slice walk the chalk road toward the blank gate; a torn postmark slams onto the gate
  (RETURN TO SENDER); Slice stakes the ring and walks with him. Six shots under 60 s. Still reel `ch2_open` is fallback / load-card art only.
- **Chapter 3 (Clearing Floor):** the sunken yard and a half-built Yo Bot on a hook; Slice names the Clearing Crew; the belt
  carries cleared things (hat, clock, bell) toward the shredder; she stakes a second ring at the circle. Six shots under 60 s.
  Still reel `ch3_open` is fallback / load-card art only.

Existing voice lines and stamp/paper SFX only; Esc, Enter or the touch Skip pill ends the scene; the camera blends onto the
follow camera in under a second with no black frame. Playtest: `opening-ch23` (Chapters 2 and 3, including 390x844 touch).

Chapters 1–3 still close with reels (`ch1_close` .. `ch3_close`, written by `Tools/Cinematics/make_reels.py`). Chapters 4–6 keep
opening and closing still reels. Courier (K), Plated Alpha (P), Half-Built (H), Foreman (F) and Yo Bot (Y) lines play from
`Resources/Voice/cinema/<reel>_<shot>.mp3` when present. Wallit and Slice keep their voice systems.

Part two (chapters 4-6): `ch4_open` .. `ch6_close` complete the set, so every chapter opens and closes with a reel (`ChapterRun.Start`, `Complete`). The four named
bosses with their own fight get an intro reel as they arrive, over the frozen field (`boss_courier`, `boss_foreman`, `boss_server`, `boss_order`;
`ChapterRun.PlayBossIntro`, which replaces their `BossLines`). Chapter 6's `ch6_close` is ADDRESS FILED as a cutscene (the order drops, DENIED, the door number,
the title card) and plays before the ADDRESS FILED card (`StoryScreen.ShowEndCard`); then the RECEIVED reel `ending_received` when the secret is earned, then the
`credits` reel, then the credits card. All are skippable (Enter, Escape, Skip). New speaker voices: Server (V), Registrar (R), Order-Bearer (O), Mega Yo Bot (M),
from `Resources/Voice/cinema/<reel>_<shot>.mp3` once made. Page scenes remain as fallback when a reel is missing.

## Barks: who talks mid-fight

Wallit and Slice answer the moment: Wallit on the first blow of a run, a home run, a wave cleared and a boss's second phase; Slice on a new kind of bot and when she runs low. Each of those lines is said once in five minutes. The Yo Bots chorus in chirps (waking, spotting Wallit, hit, launched, falling: "Uploading victory.", "Wheeeee!") and the Spider Bots are polite ("You have been served."), each from the bot's own place, one voice at a time, a line no more than once a minute. Code: `BotChorus.cs`, `VoiceDirector` (reasons `first-hit`, `boss-phase`, `new-type`, `wave-clear`, `bot-*`).

## Turning the page (transitions and loading cards)

The Department files everything, so nothing in the game cuts. Every change of place is a sheet of file paper sliding over the
screen with a rubber stamp slammed on it, then peeling away: **ENTER** into a run or a chapter, **FILED** back to the hub (and
on a win), **STRUCK OFF** when Slice falls, **RETURNED** to the menu or on a retry. A chapter's or the hub's load ends on its case-file
card: the place's name, one line from Wallit or Slice about it (existing voice lines, nothing new) and a paper strip that fills as the
place truly loads. Code: `ScreenTransition.cs`, `LoadScreen.cs`; art `Resources/LoadCards/`.

## The wall gives (played, not told)

The Department's wall is the edge of the Hollow, and it is how the player sees the story move. Every wave cleared opens a door
in it for the length of the break (warm light spills out, Slice's circle burns a step brighter); at waves 10, 20 and 30 the wall
breaks and drops to stumps. Chapter 6 opens on those stumps ("The circle is brighter.") and opens the door again between its
three waves. No line explains it: Wallit and Slice only react ("Clean."). Code: `WorldPayoff.cs`; bible beat 15.
