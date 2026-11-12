# The Place Department: world bible

"Where things that should not exist are given an address."

This is the one plan every agent builds to. It does not replace the detailed sheets; it joins them and holds them to the
living-world rule. Read with `Docs/Story.md` (premise, canon), `Docs/CampaignBible.md` (screenplay, flags),
`Docs/Levels1to3.md`, `Docs/Levels4to6AndHub.md` (map sheets), `Docs/ModelKit.md`, `AGENTS.md`.

**Canon, in short.** The Department files things. A filed thing has an address and may stay; an unfiled thing is cleared.
Wallit (wallet, wooden bat, talks money, never admits fear) is stamped RETURN TO SENDER and thrown into the Moonlit Hollow.
Slice (goth guard, scalpel knife, clipped: "Clean.") keeps the circle, the one filed address. Yo Bots (chirpy: "Uploading
victory!") are the Clearing Crew; Spider Bots are process servers. The order behind them is never seen, named or quoted. Not
in this world: Warrok, bow, birds, piano, office jokes. One stamp a chapter: RETURN TO SENDER, INVALID, REISSUED, SERVED, DENIED,
ADDRESS FILED, then RECEIVED. No camera shake (except the bat's contact jolt), no spark/ember/firefly particles, the 150 m
round the circle stays collider-free (campaign levels excepted).

**The rule everything below answers to.** A player never walks about 10 seconds without something to see, hear or do. Every
area has dressing, a landmark, life, ambient sound and several kinds of interaction; things move and change on their own; every
story beat is lived (an event, an acted cutscene with real events, sound or a voice line), never text alone.

**Honest status.** Written from the design docs and `AGENTS.md`; no build was played for this pass. "Has" means a doc says it is
built. "Gap" is where the docs or code say it falls short, or where a doc is silent. Gaps are the build list at the end of
each section and in the closing table.

---

## Areas

Eight places, each its own scene or map, plus six hidden rooms. Size is the walkable field; the circle is always the centre.

| # | Area | Mood | Looks / sounds | Size | In / out |
|---|---|---|---|---|---|
| A | **The Moonlit Hollow** (Arcade, scene2) | Cold blue night, lanterns, mist | Grassy bowl, Department wall at 205 m, lanterns, drifting notes; Yo Bot chirps, wind | 150 m field, wall 205 m | Menu > Arcade. Out: Slice falls (ADDRESS STRUCK OFF) or the finale |
| B | **The Sorting Yard** (Ch1, `Level_1_Yard`) | Cold midnight, red ink on blue | Dead-letter yard behind the Return Hall; chalk slabs, nine standing stones, trestles, sacks, post boxes; wind bed, lamp hum, flag flutter | ~134 x 126 m | Start before five shut doors; out through the Return Slot (amber shutter, NE) |
| C | **The Edge / Gate** (Ch2, `Map_Edge`) | Dry dusk, stamped-red, bureaucratic | Plaza, wall, gate arch with INVALID bars, booths, turnstiles, stamp press, watchtower beam; press thump, turnstile ratchet | circle to gate ~70 m, wall both ways | Out the plaza road to the gate, back to the circle |
| D | **The Clearing Floor** (Ch3, `Map_Floor`) | Orange furnace dark, loud | Belts of cleared things (hat, clock, bell), crushers on red marks, 7 stacks, red-lamp chute; belt rumble, crusher slam | 150 m field | Stops out and back; lever at the chute |
| E | **The Long Walk** (Ch4, `Map_Walk`) | Slate-blue dusk, polite, uneasy | Causeway of pale slabs over a mist pit, lamp stilts with folded legs, spires; lamp buzz | causeway ~250 m, Dais at 78 m (bearing 150) | Lamp stop to lamp stop; the last slabs have fallen |
| F | **The Reading Room** (Ch5, `Map_Reading`) | Lamp-gold, hushed, bare | Round hall r125 m, ribbed vault, chandeliers, form racks, lecterns, carpet aisle; paper hush | r125 m, Gallery at 80 m (bearing 0) | White door (south) to the empty desk |
| G | **The Hollow, filed** (Ch6, `Map_Filed`) | Dawn blue, quiet, almost kind | Wall gone to stumps, standalone doorframes, pale path ending at nothing, gold seal; morning wind | 190 m | Path from the south; Threshold at 80 m (bearing 20) |
| H | **The Office** (hub, `Map_Office`) | Warm lamplight, night to dawn by progress | One room, circle worn in the carpet, six doors, chapter board, Tick's desk, windows on the roofs; ticking, paper | one room (~30 m) | Chapter Complete > To the hub; a door starts a chapter |
| I | **The six back rooms** | Hushed, amber, secret | Paper-floored room, desk, pigeonholes, one lamp; 172 m out per chapter | small | Only after the chapter's lost thing is kept; the doorway faces the circle |

**What exists and where it falls short**

- Has: all eight places as scenes, kit-built, with lighting moods, ambience loops (`amb_hub`, `amb_ch1`..`amb_ch6`), score,
  set pieces per chapter, six back rooms. Arcade is unchanged and collider-free.
- Built: **the title is the Hollow, alive at night** (`MenuStage`, a small rendered stage behind the menu, the same Wallit, Slice and bot models, no new art): the camera drifts and shifts a little with the pointer or a tilted phone; Wallit flips a coin and twirls his bat; Slice stands in her ring; Yo Bots rise over the hedge, look about and duck back; the lamp flickers; paper drifts on the wind. The logo (THE PLACE DEPARTMENT / BREAKING THROUGH) slams in as the Department's rubber stamp with an ink splat and a thud, then Play eases up. Play pushes the camera in to Slice's ring and the run dissolves out of that last frame (no black). The title music bed is a separate task.
- Built: **every place has its own air** (`PlaceAudio.cs`): the Hollow's bed `amb_hollow` (crickets, a far owl, wind through loose
  paper), the hub's and chapters' beds `amb_hub`/`amb_ch1`..`amb_ch6`, fetched with their place (never in the first download),
  ducked under any voice line (deeper on a phone), plus three spot emitters per place on real objects, 3D with a linear rolloff and
  random gaps: the Hollow's lantern buzz, the Dead Letter Office's creaking sign, a post box's drip; the office's ringing desk
  phone, typewriters and radiator tick; the yard's lamp and sign, the gate's stamp press, the floor's crushers, the Long Walk's
  lamp stilts, the Reading Room's clock and chandeliers, the filed Hollow's doorframes. Wallit's wallet-flap steps change with the
  ground (grass, stone, wood, metal; 3 variants each, never the same twice in a row) and land with a heavier slap. `place-audio` playtest.
- Built: **Arcade (A) has three acts** (Night Shift, Cold Hour, Last Light, then Overtime) with their own light, sky, grade, score
  layer and enemy mix (see What happens).
- Gap: the docs record lighting and landmarks but **no per-area "gap clock"**: nothing has measured 10 s between things. Each
  area below carries a minimum density the validator or playtest must check.
- Gap: the 61 later kit pieces and several lodger/boss bodies are still code-built stand-ins (Gen art replacement approved by J,
  2026-11-07, budget in the memory decision).

## Landmarks

One far-visible thing per area, so a player can steer by it. All are already in a map sheet; the bible fixes which one leads.

| Area | Landmark (seen from) | Why it steers |
|---|---|---|
| A Hollow | **The Dead Letter Office ruin** (leaning brick front, arched door, a clock with no hands, giant rusted post boxes, sorting shelves spilling envelopes) 162 m out on the bearing Wallit faces at the start; the lantern ring and the circle's gold glow; a hedge and picket rim at 186 m | Home is always the light; the wall is the edge |
| B Yard | **The Dead Letter Office clock tower**, lit face, 95 m north (visible from the start) | Goes north-east to the amber Return Slot once the crew falls |
| C Gate | **The gate arch with three red INVALID bars**, lit amber, 70 m out, and the watchtower beam sweeping | The beam crosses the player every few seconds |
| D Floor | **The red-lamp chute** high on a stack, seven glowing stack mouths | Red means the lever; belts show where things go |
| E Walk | **The lamp stilts' violet heads**, one pair per stop; the broken road end | Next stop is always the next violet lamp |
| F Reading Room | **The white door** (behind) and the **ten form racks**, which go blank page by page | The room visibly empties as the player reads |
| G Filed Hollow | **The gold seal**, turning, then the bare **Threshold doorframe** | The seal is the first goal; the frame the last |
| H Office | **The six doors with status lamps**, and the **brass door number** that hangs when all six are filed | The room reads the file at a glance |
| I Back rooms | **A soft gold pulsing ring from 40 m** | Appears only once the clue is kept |

Built (`HollowPostOffice.cs`, `KitCatalogHollow.cs`): A now has the Dead Letter Office, the place the Department sent every unfiled letter; the ruin is what the Hollow was before it was filed. Hero props (the two giant post boxes and the shelves) are kit stand-ins until their Gen versions are baked under the same piece ids. Still open: a distant, slowly turning **Department stamp press silhouette
on the Hollow's wall** that lifts and falls on the wave clock, so Arcade has a far thing to steer by and a clock that is seen,
not read. (Beyond 150 m, no collider.)

## Who lives here

Routines are "through the day": the world has no clock face, so a day is a chapter's progress (Office) or a wave's arc (Hollow).

- **Wallit** (player). Reacts to the world in money: coins on pickups, a price on hazards ("That's extra!"). Voiced; subtitles only
  as backup.
- **Slice.** In the circle; idles, watches the nearest bot, stabs what gets close, flinches when hit. Calls threats in one word
  with a ring marker (`SliceCallouts`); by Arcade act she also parries (Cold Hour) or stamps to stun (Last Light). In chapters
  she follows when asked (Ch2) and can be downed and revived (held E). Two lines in a row at most, then silence; a nod is an
  animation, not text.

## Characters

### Wallit
- Want: get paid — coins, bolts, the fee for guarding Slice's circle.
- Arc: arrives for himself → covers Slice's debt → files the address and keeps swinging in overtime.
- Moment: first bot reaches Slice; he says "That's my account! Hands off!" and the fee pile shrinks (`StakesFeel`, `CoinPile`).
- Personality: brash, money-minded, loyal.
- Voice: `CwhRBWXzGAHq8TQ4Fs17` (eleven_v4 / ElevenLabs Wallit).
- Animations: idle, walk/run/strafe/backPedal (`CharacterMovement`), talk (mouth from `WallitMouth` on voiced lines), swing1–3, groundSmash, homeRunCharge/Release, hitReact, shakeOff, victory, death.

### Slice
- Want: keep the circle filed — the one address in the Hollow that may stay.
- Arc: alone at the ring → calls Wallit's targets as partner → parries, then stamps, as the acts harden.
- Moment: a bot closes on her or flanks him; she says one word ("Left." "Mine."), the bot lights with her ring, and (from Cold Hour) she blocks it back (`SliceCallouts`).
- Personality: cool, clipped, exact.
- Voice: `hv_heroine` (Hawkes Gen open / qwen3-tts).
- Animations: idle, stab, slash, hurt, defeat (`Musician` / Slice controller); companion walk when following (`Companion`); talk via subtitle + voice (no mouth rig); callout block pose and stamp from `SliceCallouts`.

### Yo Bot
- Want: clear the unfiled — erase Wallit and Slice's address.
- Arc: cheerful Clearing Crew → plated and mega variants → chorus that reads the order from wave 20.
- Moment: wakes with `yobot_wake` and chirps "Uploading victory!" while walking a straight line at the circle.
- Personality: chirpy, bright, relentless.
- Voice: `hv_kid`.
- Animations: idle (dormant / standing), walk cycle, talk (chirp bark via `CastVoice` / `hv_kid`), wind-up, claw strike, flinch, death (`CharacterRig` on Yo Bot parts).

### The Courier
- Want: deliver the notice of striking onto Slice.
- Arc: clipboard raised → stamps notices → breaks when the notice is broken or stood in.
- Moment: raises the clipboard; the stamp mark appears under Wallit or Slice (`Courier`).
- Personality: polite, official, brittle.
- Voice: `hv_young_hero`.
- Animations: idle (clipboard), walk, talk (bark), tell/stamp, stagger, death (`CharacterKit` courier).
- **Yo Bots / Plated / Mega** (the Clearing Crew). Sleep dormant at their drop in chapters, wake by proximity ("yobot_wake"), chirp
  while walking, read the order aloud from wave 20. A Yo Bot that has been struck sags then rights itself; a golden one runs.
- **Spider Bots** (process servers). Walk to an address and wait; the Server offers a paper and strikes only on the third refusal.
- **The named ones:** the Courier (Ch1, clipboard, stamps notices on Slice), the Plated Alpha (Ch2), the Foreman (Ch3, overtime),
  the Server (Ch4), the Registrar (Ch5, gold Titan with a ledger), the Order-Bearer (Ch6), the Dead Letter (Ch4 back room).
- **The lodgers** (Office, none fights): **Tick** (clock; pendulum swing, strikes the hour), **Brim** (hat; looks about, tips what it
  does not have), **Spare** (half-built Yo Bot; hops, chirps, tinkers), **Chime** (bell; rocks, rings; gives errands). The desk
  changes hands as the file advances (Tick, Spare, Brim, Tick). Each has a voice and three chats; a gold **!** marks an unheard one.
- **Ambient life that is not a character** (to build, see What happens): paper that drifts, chalk-dust, pigeons are retired
  (no birds): use **loose forms and notices** instead; moths are also out (tiny particles).

Reactions to the player, minimum: every named NPC and lodger turns toward Wallit within 6 m, speaks once per approach, and has
a different line each chapter cleared. Bots pause and read their notice when Wallit first enters their view.

## What happens

Per area: kinds of interaction (there must be at least four kinds: fight, talk, collect, operate, discover), events on their own,
ambient sound, what changes. "Gap clock" is the longest the docs allow between things; build and check to it.

### A. The Moonlit Hollow (Arcade)
- **Interactions:** bat combo/smash/home run, lock-on, jump-landing stomp, build forts (`BuildMode`), pick up bolts, open chests,
  take level-up cards, ability list.
- **On their own:** wave start beats, event waves (`WaveEvent`), golden Yo Bot dash, elite spawns, boss-wave cards, lanterns
  sway, notes drift; at waves 10/20/30 a skippable boss intro (lights dim, camera dolly, Wallit/Slice cutscene lines) and a
  defeat payoff (hit-stop, slow-mo, bolt burst, Slice line) before the next act banner (`BossSetPiece`).
- **Sound:** wind bed, Yo Bot chirp, bat crack, stamp thump from the wall, Slice and Wallit barks (`VoiceDirector`); boss
  intros reuse existing Wallit/Slice voice clips under the cutscene key.
- **Acts (built, `ArcadeActs.cs`):** waves 1-10 **Act I Night Shift** (moonlit blue, Yo Bots), 11-20 **Act II Cold Hour** (teal
  storm light, Spider Bots come down the wall), 21-30 **Act III Last Light** (bruised dusk, orange at the rim, every kind can
  roll an elite trait: the first mixed elite crews). After 30 the loop starts again under an **Overtime** red-ink tint. Each
  change crossfades light, sky, grade and score over 2 s and shows a banner with a chime sting. Light and sky only: nothing
  solid in the 150 m, no sparks.
- **Boss set pieces (built):** wave 10 **The Plated Alpha** (Slice takes a visible first crack mid-intro, Wallit covers her),
  wave 20 **The Foreman** (Yo Bots read the order aloud through act three via `ActThreeChorus`; wall press stamps faster),
  wave 30 **The Order** (fallen scroll with a red DENIED blotch; Wallit's DENIED line is on the finale card). Campaign
  roster bodies and two-phase tells (`BossPhases` red wash). Wave clear waits on `BossSetPiece.PayingOff`. Camera stays
  out in the open field (9 m+), never into scenery.
- **Changes over time:** gap, built: wave-power list grows; the acts above replace the old "colder from wave 10" tint. Earlier plan: from wave 10 the moon goes down a notch and the
  lanterns burn a colder white; from wave 20 the wall's stamp press (landmark) stamps in rhythm and the Yo Bots read the order
  aloud; at wave 30 dawn colour on the rim. No new objects in the 150 m, light and the wall only.
- **Gap clock:** the lull between waves has a break timer; fill it with a bolt sweep, a Chime-style tip from Wallit, a lantern
  relit by Slice. Never silence longer than 10 s.

### B. The Sorting Yard (Ch1)
- **Interactions:** talk to Slice at the Ring; read three notices on the west trail; pick up and plant the Mailbox Flag; fight the
  Clearing Crew at the drop; open the Return Slot; the Courier fight; hidden brass tag; back room (Sorting Nook).
- **On their own:** the clock hand ticks; the Mailbox flag flutters; the crew's sleeper snores until woken; red ink drips down the
  Return Hall doors; the Courier's clipboard flutters before its notice.
- **Sound:** wind bed, clock tick (from 60 m, louder at the tower), lamp hum, chalk scrape underfoot, `flag_flutter`,
  `yobot_wake`, `slot_shutter`.
- **Changes:** the amber strip at the slot lights after the crew falls; the doors of the Return Hall each read RETURNED in ink
  after a notice is read.
- **Gap clock:** start to Ring about 40 m (lamp pairs every ~8 m); Ring to the three notices, one every ~12 m (add a lamp or
  sound at 6 m between). Fine.

### C. The Edge / Gate (Ch2)
- **Interactions:** walk to the gate with Slice as companion (revive her), knock twice or not (choice), fight the gate guard crew,
  stamp press timing (walk through the gap), find the Torn Postmark, boss: the Plated Alpha, back room (Forgotten Door).
- **On their own:** the watchtower beam sweeps; turnstiles ratchet by themselves; the press prints red marks on a beat; booth lights
  flick.
- **Sound:** press thump 3 s cycle, turnstile ratchet, beam hum, distant tannoy of the Department (a **voice**, not text).
- **Changes:** INVALID bars on the arch shatter on the boss; the gate stays open on the return so the plaza has the arch behind.
- **Gap clock:** press every 3 s and beam every ~7 s give a see/hear event under 10 s on the whole road.

### D. The Clearing Floor (Ch3)
- **Interactions:** operate the lever at the chute; carry the Belt Token; dodge crushers; hand the Foreman the wrong sheet (choice);
  smash Yo Bots off belts (Ground Rules goal); back room (Spare Parts Bin).
- **On their own:** belts move cleared things past (hat, clock, bell); crushers slam on red marks; stacks glow; the last batch is placed dormant and wakes by proximity (nothing spawns on a timer).
- **Sound:** belt rumble, crusher slam, whistles; the Foreman's overtime bell.
- **Changes:** the lever stops one belt; the floor's light shifts from red to a tired amber when it stops.
- **Gap clock:** two belts and crushers at every point of the route: under 4 s.

### E. The Long Walk (Ch4)
- **Interactions:** walk stop to stop (lamp stops, no feed to break); the Server's paper: refuse (X) or take (choice); dodge
  between slabs; find the Thrice-Refused Paper; back room (Dead Letter Box: the Dead Letter fight, reward Refund in Full).
- **On their own:** stilt heads open a folded leg and close it; the pit's mist breathes; slabs behind the player settle and crack;
  the paper stand lifts and burns violet when the Server holds out its paper.
- **Sound:** lamp buzz, mist hush, slab thud underfoot, the Server's polite chirp; a bell on a refusal.
- **Gap clock:** a stilt every ~25 m is too sparse for a ~10 s run at 7 m/s (70 m): needs a thing every ~40 m. Gap: add
  **loose papers that skim the causeway** (a gust every ~8 s, rattling the slab edges), a **bell on the far spire**, and
  a **Delivery Chirp** the stilts call to each other.

### F. The Reading Room (Ch5)
- **Interactions:** read five pages at the lecterns (the Registrar reads along); read the first page aloud or not (choice); stamp
  the Courier's notice if it was kept; Blank Form errand; back room (Closed Register).
- **On their own:** forms on the wall go blank per page read; chandeliers sway; a page lifts as the player nears; dust in lamp
  shafts (soft, not sparks); the Gallery rises after the fifth page.
- **Sound:** paper hush, lectern thud, a voiced page read; the Registrar's ledger snaps.
- **Gap clock:** the five lecterns are about 25 m apart on the aisle; add a **form that flies off the wall and returns** every
  ~8 s and a **rubber-stamp tap** from the empty desk.

### G. The Hollow, filed (Ch6)
- **Interactions:** walk the path to the gold seal; hold three waves at the circle; the Order-Bearer; the vote at the Threshold:
  hand Slice the stamp (choice), ask for two door numbers (choice); the last page is read; the Door Number unrolls.
- **On their own:** standalone doorframes sway with no wind; the wall stumps shift; dawn colour rises through the chapter.
- **Sound:** morning wind, a held note under the Threshold, then the stamp: ADDRESS.
- **Gap clock:** the path is quiet by design; hold it to 10 s by letting each doorframe **open on a different room's sound**
  (hub ticking, belt hum, lamp buzz, paper hush), a sonic memory of the chapters.

### H. The Office (hub)
- **Interactions:** talk to four lodgers (three chats each), Chime's errand, a favour, a loose notice pinned to the wall, board,
  six doors, the journal, Slice's ring banter, Chime's bell, the Brass Door Number.
- **On their own:** Tick swings and strikes the hour; Brim tips a hat it does not have; Spare hops and chirps; Chime rings;
  the window goes from night to dawn; the desk changes hands; slips and lamps appear on the walls.
- **Sound:** ticking room, paper hush, clock strike, lodger voices, door latches, kettle-hiss of the radiator.
- **Gap clock:** no point in the room is more than a few seconds from a lodger action. Fine.

### I. The back rooms
- **Interactions:** knock twice, take the room's thing, say the line, optional fight (Dead Letter). **On their own:** the amber lamp
  flickers; pigeonhole drawers settle. **Sound:** a hush that opens on entry, then one chime.

### Time and weather (shared)
Light and mood change by progress, never by random weather: Arcade by wave, Campaign by chapter and by what is filed.
Stamps, ink, paper and light are the world's weather; no rain, no sparks, no embers.

## Story beats

0. **The fee shrinks (built, Arcade and Slice-guarded chapters, `CoinPile.cs`).** A pile of gold coins sits at the circle's edge, its
   height Slice's health. A bot's blow sends the top coins arcing to the bot with a clink; healing drops them back. Told by the
   pile and its sound, with Wallit's "Touch her, you pay double!" only as support. Canon: [Docs/Story.md](../Docs/Story.md), "The fee".

Each beat is lived. Format: **Where / When** — what the player does and sees; sound and voice; the consequence. Text, at most one
line, is only the subtitle of a voiced line. Beats marked **(built)** exist per the docs; **(build)** is the gap to close.

1. **The Stamp falls (built; Arcade version is the cold open, `ColdOpen.cs`; Campaign Chapter 1 is in-engine).** Arcade, first run
   on a browser: no text page. The RETURN TO SENDER stamp (`stamp_heavy`) drops onto Wallit, he lands in the circle as its ring
   lights, Slice says "Stay close." (subtitle only), and the first Yo Bot is already rushing in; any move or swing ends it.
   Plays once per browser. **Campaign Chapter 1 (Sorting Yard):** an eight-shot in-engine scene (`CinematicDirector`,
   `ch1_open_world`) — Wallit arrives on the west trail, Slice turns in her ring ("You're inside. Inside, you exist."), the
   Courier raises its clipboard ("Hello! Uploading victory! Striking this address now!"), the stamp lands on Wallit at the
   circle (flinch and shove), he answers ("Ow! That's extra!", "Return to sender."), and the camera settles behind him for
   play with no cut to black. Proof: playtest `opening-scene` (events `opening-shot`, `opening-move`, `opening-skip`).
2. **The first swing (built).** Yard, at the Ring. Wallit takes the bat; the first Clearing Crew bot wakes with `yobot_wake`.
   Combo hits are named (Deposit, Withdrawal, Overdraft!) by voice, not caption.
3. **The Courier stamps Slice (built).** Yard boss. A clipboard flutter, the notice lands on Slice; the player must dodge (X) a
   stamp aimed at her ("MISSED ME"). The Courier is broken; its last word is a question it cannot answer.
4. **The Gate (build as event).** Edge. Slice follows only when Wallit waits at the ring. The gate's tannoy speaks (voice) the
   word INVALID; the arch bars shatter on the Alpha. Needed: tannoy voice file, bar-shatter sound. Proof: playtest events `gate-tannoy`, `gate-bars-shatter`.
5. **The belt of cleared things (build).** Floor. Wallit sees his own kind (a hat, a clock) pass on the belt; Slice: "It isn't." (her
   longest line). The belt stops at the lever, and the stopped belt is the beat. Needed: a belt-stop sound and a pause in the
   music. Proof: events `belt-stop`, `slice-it-isnt`.
6. **The Server's three refusals (built).** Long Walk. Paper held out, X to refuse; at the third the stilt heads open and
   the Server strikes. Slice answers it herself if the player chose it (choice).
7. **The page nobody signed (build).** Reading Room. Five pages are read and the racks go blank one by one; the last page is
   blank, the Registrar stops mid-read: sound drops to the vault hum, then one stamp. Needed: the 5-page paper sound set. Proof: events `page-read-1`..`page-read-5`, `vault-hum`, `final-stamp`.
8. **Strike this address (built).** Threshold. The Order-Bearer is down, the order drops, Wallit reads it aloud: "Strike this
   address." stamped DENIED (the stamp hits the page on the word); Slice files the circle herself: ADDRESS FILED as a sound
   and a ring, plus the word once on the card.
9. **The Door Number (built, polish).** Filed Hollow. The ring unrolls into a line of writing and a number; the doorframes
   open on the chapters' sounds (see G).
10. **Back home (built).** Office. The brass door number hangs; the lodgers each say a last line; Chime's bell rings the right
    address. RECEIVED stamped on the ending card with all six lost things set down.
11. **Arcade finale (built).** Wave 30. The Spider Bot falls, the order drops, same reading as 8, then Enter keeps going.
12. **Address struck off, then again (built).** Arcade, whenever Slice falls. The run-end screen keeps the title, counts waves
    and coins with sound, stamps a new best, shows the next ladder goal (and stamps one just finished), then Wallit and Slice
    trade one voiced exchange from the run's facts (`Story.RunEnds`, never the same within five runs). Retry starts the next
    run at once. Sound: coin ticks, `stamp_heavy` on a best, goal stamp; voice files under `Resources/Voice/`. Proof: events
    `run-end-count`, `best-stamp`, `goal-done`, `cast-exchange`, `run-start` after Retry. Playtest: `run-end`.

Beat rule: every new beat gets a row here first, with where, when, the sound and voice it needs, the event that is the proof,
and a playtest event name.

- **The breach (Arcade and chapters that hold the circle).** A bot reaches Slice: the ring stutters and greys, the screen's edges lose colour, under 30% a heartbeat layer rises, and Wallit's first-hit line ("That's my account! Hands off!") is the only word. Seen and heard, never explained.

13. **Slice calls the shot (built).** Arcade (and companion chapters): a bot closes on her circle or flanks Wallit; she names it
    in one word, a pale ring marks that bot, an edge arrow points if it is off-screen; from Cold Hour she parries one per wave,
    from Last Light she stamps once to stun. Wallit sometimes answers ("On it." / "Billable."). Proof: events `slice-callout`,
    `callout-ring`, `callout-arrow`, `slice-parry`, `slice-stamp`; playtest `slice-partner`.

### Boss entrances (every boss)
Each boss is introduced on camera, never by a banner alone: slow time, a swing from Wallit to the boss arriving, the
rig's rear-up and slam, a stamped name card (e.g. THE FOREMAN / Work order 0031), a stinger, the boss's own taunt, then the
boss theme. Spec and card table: Docs/Story.md, "Boss entrances". Code `CinematicBoss.cs`; playtest `boss-entrances`.

## Gaps against the 10-second rule

| Area | Gap | Fix | Cost |
|---|---|---|---|
| A Hollow | stamp press silhouette still open | Dead Letter Office ruin (built, `HollowPostOffice`); three acts with light, sky, score and mix (built, `arcade-acts`); wall stamp press silhouette still open; painted act skies and recorded stems would be GPU extras | code |
| E Walk | stilts 25 m apart is too thin | skimming papers, far bell, stilt chirps | code + 2 sfx |
| F Reading | long stretches between lecterns | flying forms, desk stamp tap | code + 1 sfx |
| G Filed | quiet by design | doorframes open on old chapter sounds | code, reuse sfx |
| C Gate | tannoy voice not made | one `audio.voice` line | cents |
| All | no measured gap clock | a playtest walk that times the gaps (see playtest-scenario skill) | code, later |

## What is not decided here

- Gen art replacement of stand-ins: priced separately under the approved item (GPU estimate follows in the summary).
- The Arcade wall press and light arc: J asked for the vertical slice (2026-11-10) and they are built as `HollowLandmark.cs`; the stamp sfx is still to be made (cents, `audio.sfx`).
- Whether Arcade gets the wall stamp press and light arc: proposed here, J's call.
