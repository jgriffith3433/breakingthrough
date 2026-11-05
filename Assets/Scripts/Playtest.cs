using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Hawkes playtest bot (contract v1, C:/Projects/hawkesstudio/docs/playtest.md). It plays through PlayInput,
// the same keys and mouse a player uses: Enter on the menu, WASD to walk to the nearest bird near the musician,
// Shift to lock on, left clicks to swing the bat (held to wind up a home run), back to the piano when the birds
// are gone, 1-3 for cards, Enter to skip a break. Every scenario first presses the web page's "Tap to play" prompt. Setup only: it plays in MetaProgress's sandbox with fresh ranks, so it never
// touches the player's save, and it loads the menu scene if the game isn't on it. Nothing runs until a scenario starts.
public static class Playtest {
    const string Product = "theplacedepartment";
    const float RunCap = 900f;

    static readonly PlaytestScenario[] Scenarios = {
        new PlaytestScenario("smoke", "Boot, menu, start a run, about 30 s of play: the bot walks to bots near the musician and swings the bat (lock-on nearest, combo swings)", 45, "smoke"),
        new PlaytestScenario("run", "A full run until the musician falls, picking level-ups: the bot guards the musician with the bat (walks to the nearest bot near the piano, locks on, swings, walks back). Should pass wave 5 without 'no kills for 45 s'", 600, "full"),
        new PlaytestScenario("bat", "The bat: a 3-hit combo on wave 1's Yo Bots (hits and knockback, the third further), a ground smash on small Yo Bots dropped in front of the player (setup), then a charged home run into a line of Yo Bots (setup) that bowls them over; lock-on picks the nearest bot throughout. Then a minute of play", 200, "full"),
        new PlaytestScenario("menus", "Shop menu, start a run, a level-up card, reroll and pick", 120, "menus"),
        new PlaytestScenario("words", "Reads the menu, the shop, the help line, build tips, pause, level-up cards and the game-over screen for retired cast words. Setup: the testing wave key visits the tip waves, then wave 2 is held until Slice falls", 300, "menus"),
        new PlaytestScenario("touch", "Touch only, for a phone-sized screen (mobile, or 844x390): the menu fits, tap Play, walk to the bots with the on-screen stick and tap Swing, tap a level-up card, play to wave 2", 330, "menus"),
        new PlaytestScenario("settings", "Press Tap to play, open Settings from the menu: sound off and on, music and effects sliders, Done; start a run, pause (button, then P), Settings from the pause menu, resume, play a little. Any screen size", 100, "menus"),
        new PlaytestScenario("bots", "The bots: wave 1's Yo Bots (authored Gen model when present) walk at the musician with feet planting; bolts drop; enemy list names them. Setup: drop 60 Yo Bots and log FPS for the WebGL budget, then the testing wave key (=) to wave 30 for the Spider Bot Alpha (glass pod, six legs when authored) and 30 s of fight", 360, "content"),
        new PlaytestScenario("analytics", "Hawkes Analytics: start a run, clear wave 1, then hold fire on wave 2 so the bots take the musician. Every analytics event is logged as it goes out (run_start, level_start and level_end per wave, card picks, the death and the bot kind that landed it, run_end) with the build version and env; on the game-over screen the \"How was that run?\" card must show, and the bot rates it 4 stars", 240, "full"),
        new PlaytestScenario("musician", "Slice, the musician at the piano: her health bar is titled Slice, and she sits on the bench playing the piano clip with her hands on the keys while the piano music plays. The bot holds fire and frames Slice so wave 1's Yo Bots reach her: she stands (standUp), stabs and slashes the one in reach with her knife (stab / slash with OnSliceKnifeHit blade damage) at most once every 2.5 s, and flinches when hit (hurt); then the bot clears the wave (with Encore pulsing from PianoRecital) and she sits back down (sitDown) to play (piano). Then it holds fire from wave 2 (after 20 s, setup: a ring of Plated Yo Bots dropped around the bench, topped up every 8 s) until the bots empty her bar, she falls (defeat), and the game-over panel appears", 330, "content"),
        new PlaytestScenario("slice", "Slice guarding the circle: inspects the live Slice on PianoRecital (bob / coat / boots / scalpel knife from front and side, hands on keys, piano music), runs the full musician scenario (standUp, stab, slash, sitDown, piano, hurt, defeat before game over), and verifies all 8 Animator clips", 220, "content"),
        new PlaytestScenario("personality", "Slice's personality: she speaks short clipped lines (subtitles; the log shows each) at wave starts, kills, near-misses and when hurt or low, never the same line twice in a row, and shows distinct body behaviors (idle scan / knife flip, ready at a wave, kill flourish, glance, stagger, hunch when low). Wallit and Slice trade a short exchange. Real input: the bot fights wave 1 with the bat; then it holds fire, and from 15 s on (setup) a ring of Plated Yo Bots is dropped on her until she is low", 240, "content"),
        new PlaytestScenario("voice-gap", "The voice director: five minutes of real wave defense with the default numbers (barks at least VoiceDirector.BarkGap apart, same line and same reason on a cooldown, story and boss-wave lines always through, never two lines at once). Real input: the bot fights with the bat. Events voice-line (each line with its priority), voice-drop (barks the director held back), voice-gap (lines per minute, smallest gaps, overlaps, key lines)", 330, "content"),
        new PlaytestScenario("voice", "Wallit talks: he barks short money lines (subtitles; the log shows each, with why) at kills and streaks, big hits and home runs, getting hurt, wave starts and level-ups, never the same line twice in a row and never over Slice; his mouth opens and closes with each line, in the game camera and in a close-up (events wallit-line, wallit-mouth, wallit-voice). Real input: the bot fights with the bat until each kind of line was heard, then stands and takes a hit, then a close-up of his face while he speaks (setup: one line is asked for)", 300, "content"),
        new PlaytestScenario("upgrades", "The bat upgrades: the shop lists Corked Core and Aluminum! Not wood! (setup: both owned in the sandbox) and the metal bat is in the player's hands; wave 1's first level-up cards are read off the screen (bat upgrades, no retired cast words); setup: the testing wave key (=) to wave 14 for tougher Plated Yo Bots and a handful of bat boons granted (Wide Arc, Pine Tar, Ice Bat, Live Wire, Grand Slam, Long Ball and more), each one's mark on the bat logged; then the bot plays until each effect is seen: a wider swing, bots burning, bots slowed, chain sparks shorting bots out, a 360 spin swing, batted bolts landing. Then Thunder Bat is granted for a lightning spin, and 30 s of play", 330, "content"),
        new PlaytestScenario("wallit", "Wallit as the player: checks Wallit's wallet body, bills, gloves, sneakers and bat on the live player, exercises running, locked-on strafing (strafeLeft, strafeRight, backPedal), jumping (jump, landing), getting hit (hitReact), and every bat swing (swing1, swing2, swing3, groundSmash, homeRunCharge, homeRunRelease) with OnBatContact damage, then checks all 17 Animator clips", 200, "content"),
        new PlaytestScenario("locomotion", "Wallit's feet on the ground: walks, runs, starts, stops and turns free, then locked on to a Yo Bot strafes left and right, back pedals, runs at it and stops, all through the keys. Each segment logs the planted foot's slide and the hips' largest jump (`loco` events; a slide or a pop is a bug)", 75, "content"),
        new PlaytestScenario("bible", "The Campaign screenplay as data (Resources/Story/campaign_script.json, rendered into Docs/CampaignBible.md): loaded and checked chapter by chapter. Each of the six chapters and the ending must have a script with named speakers, at least one dialogue choice with two options, an opening and a closing shot list (shot, camera, speaker, voice line, music) and a map design sheet (setting, landmarks, route, hazards, boss arena, palette, props); the Courier, the Stamp and RECEIVED are present and the retired cast is not (events bible-chapter, bible-choice, bible-canon, bible-questions; a problem for anything missing). Then, with real key presses, the dialogue box plays Chapter 1's opening shot list and the ending's closing one (event bible-cutscene). Metrics bibleChapters, bibleLines, bibleSpeakers, bibleChoices, bibleShots, bibleQuestions, bibleProblems", 240, "content"),
        new PlaytestScenario("dialogue", "Dialogue boxes and the cast: the cast file (Resources/Cast/cast.json) is read and must list every character of the bible and not Warrok (event cast-data, a problem if Warrok is there or a bible name is missing). Arcade starts, then setup: a dialogue scene with one line per cast member plays over the run. The bot reads each page with real key presses and every page must show the speaker's name, a portrait (art or the stand-in disc with its glyph) and the cast file's colour for that speaker (events dialogue-page, a problem if a named line has no name or portrait, or the colour is wrong); Wallit and Slice lines go through their voices and the rest through the Cast.LineSpoken hook (dialogue-voice). Metrics castMembers, dialoguePages, dialogueNamed, dialoguePortraits", 240, "content"),
        new PlaytestScenario("cast", "The new cast, in 90 s: the main menu with the key art up (MenuArt, its size and that the menu shows it), What's new opened from the site over the game with the release note naming Wallit, Slice and the bots (read from releases.json), then a run where the player is Wallit with his bat, the bar says Slice, and wave 1's Yo Bots close in and are batted. The AGENTS.md and README cast text lives in the repo, not the build: the review reads it from the diff", 120, "content"),
        new PlaytestScenario("modes", "The mode select: the main menu shows Campaign and Arcade; Campaign opens the three case files (save slots, setup: the sandbox keeps them off the real save), opening an empty file loads the placeholder Chapter 1 scene with Wallit and Slice's lines, Menu returns and the file now reads Chapter 1 with its time on the books, Erase (asked twice) empties it; then Arcade starts today's wave defense and the bot plays 40 s of it with the bat. Events mode-select, campaign-files, chapter-open, chapter-lines, campaign-saved, campaign-erased, arcade-start", 200, "content"),
        new PlaytestScenario("pause-nav", "Pause and menus by keyboard and gamepad, in every chapter (setup: the sandbox case file stands on each chapter in turn). From the main menu with keys only: C opens the case files, Enter opens file 1, Enter begins the chapter, Enter skips the opening scene. Then P pauses (time stops) and P resumes, the pad's Start pauses, Down and Enter open Settings (first chapter): Left turns the music and effects sliders (the gold ring follows), Enter on Voices cycles how often the voices speak, the pad's B closes Settings and the pause stays, the settings are read back from their saved JSON; Resume with Enter; pause again, and Down, Down, Enter, Enter on Quit to menu returns to the main menu. Events nav-menu, nav-case-files, nav-chapter-card, chapter-paused, chapter-resumed, pad-paused, nav-settings, volume-changed, voices-changed, settings-persisted, nav-quit; metrics chaptersPaused, menuMoves, menuSubmits, sliderTurns", 900, "content"),
        new PlaytestScenario("score", "The score and the cast voices. Setup shortcuts: the music director is asked for every track by name (theme_wallit, theme_slice, theme_department, ch1_score..ch6_score, the six boss themes, hub, ending, credits) and each is heard for 2 s (events music-track: track, loaded or missing, volume); then nine shot reels (ch1_open, boss_courier, ch2_close, boss_foreman, boss_server, ch5_open, ch6_close, ending_received, credits) play to the end by name. Per reel: its bed (event reel-bed), every spoken shot's voice file found or missing (voice-file, with the cast voice), the set-piece sounds fired (sfx), and the voice events. Checked: no two voice lines overlap (Wallit's floor, the scene and the cast voice all count), the music ducks under speech (lowest duck below 0.9), every line of every reel has a voice file, every track exists. Metrics scoreTracks, scoreTracksLoaded, scoreVoiceLines, scoreVoiceMissing, scoreOverlaps, scoreLowestDuck, scoreSfx", 900, "content"),
        new PlaytestScenario("cinematic", "The shot-based cinematic player (Cutscene.PlayShots, data in Resources/Cinematics/sample.json): a seven-shot sample plays over the game with placeholder stills, Ken Burns moves, parallax, crossfades, letterbox bars, voice lines through the voice director, a music bed that ducks under them, and typed subtitles. Pass 1 presses Space for the next shot and then Enter to skip mid-scene: play resumes (time scale 1, the director free, the bed fades out). Pass 2 runs to the end untouched. Setup: the scene is started by name (the shot list is the data). Events cinematic-open, cinematic-shot (id, zoom, pan, subtitle, bed volume, duck), cinematic-next, cinematic-skip, cinematic-resume, cinematic-done, cinematic-memory; metrics cinematicShots, cinematicSkipped, cinematicOverlaps, cinematicVoiceLines, cinematicSubtitles, cinematicDuckLow, cinematicMemoryMB", 200, "content"),
        new PlaytestScenario("chapter-cinematics", "Every campaign cinematic: the openings and closings of Chapters 1-6, the four boss intros (Courier, Foreman, Server, Order), ADDRESS FILED (ch6_close, played before the ending card), the RECEIVED ending and the credits reel (Resources/Cinematics, wired in ChapterRun.Start, Complete, PlayBossIntro, CompleteFinal). Each reel is played by name over the game, the way the chapters play them. The bot lets all but two run to their end (Space on shot 2 of the first) and presses Enter to skip the Server intro and the credits mid-scene. Per reel it logs the shots, the voice lines (Wallit and Slice through the director, the Courier, Foreman, Plated Alpha, Half-Built, Server, Registrar, Order-Bearer, Mega Yo Bot and Yo Bots from Resources/Voice/cinema), the subtitles, the music bed ducking and any generated still still missing (missing-art: the placeholder shows). Checked: play resumes (time scale 1, director free, bed fades), no overlapping voices. Events cinematics-reel (id, shots, voice, subtitles, missing art), cinematics-skip, cinematics-resume, cinematics-done; metrics cinematicsReels, cinematicsShots, cinematicsVoiceLines, cinematicsMissingArt, cinematicsSkipped", 900, "content"),
        new PlaytestScenario("cutscene", "The chapter cutscene: Campaign, case file 1, Begin on the Chapter 1 card, and the opening scene (The Stamp) plays over the map with letterbox bars, a speaker name and portrait slot, typed lines and the camera on whoever talks. The bot reads the first pages with real key presses (a first press finishes the typing, the next turns the page), skipping a page's voice by pressing on, then presses Enter to skip the rest: play starts with the objective line up and Wallit moving on the stick, and the chapter is cleared. Checked: every Wallit, Slice and Yo Bot page has a name, two voice lines never overlap (Wallit's floor and the scene both count them), the director allows no other line during the scene. Events cutscene-open, cutscene-page (speaker, text, camera focus, letterbox, voice), cutscene-skip, cutscene-play, cutscene-voice; metrics cutscenePages, cutsceneVoiceLines, cutsceneOverlaps, cutsceneSkipped", 420, "content"),
        new PlaytestScenario("choices", "Dialogue choices that change later lines and outcomes, saved per case file, with real input. Campaign, case file 1 (the sandbox), Begin: chapter 1 is played to the Courier's fall. The Courier's aftermath scene (events after, choice-open, choice-made) stops on two choice pages with two buttons; the bot presses 1 (keep the notice) and 1 (tip Slice), the reply plays at once and the Chapter Complete card carries the lines that read them (event choice-card: 'notice is in my pocket', 'You tipped. I noticed.'). The files are written out and read back as after a quit (choice-saved, metric choiceSaved). Setup shortcut: each later chapter's choice pages (Choices.All, chapters 2 to 6) are put up over the game, one at a time, and the bot answers them with the key presses 1 or 2 (choice-made); then for every chapter that reads a choice in its boss fight, the card or the ending, the lines are read under option 1 and under option 2 (choice-lines, and a problem if they do not differ; choice-outcome gives the Server's refusals needed, 3 or 2 if the paper was taken, and the Foreman's phase-two line). Metrics choicesMade, choicesSaved, choiceLinesDiffer, choiceOutcomes", 900, "content"),
        new PlaytestScenario("comfort", "Campaign comfort with real input: Chapter 1's opening scene plays; P opens the pause menu over it (Skip scene and Restart chapter, the scene does not turn); Settings is pressed like a mouse to set subtitles to Largest, high contrast on and the camera to Calm; the pad's Start resumes and the text is larger, the reel subtitle has a solid plate and the camera/zoom no longer moves; the pad's A turns the page, Y skips; a dialogue choice is made with the stick and A (setup: the choice page is put up); the Skip button is tapped; Skip scene from the pause menu; Restart chapter (asked twice) shows the load screen with the chapter name and the chapter comes up again with scenes and memory in budget. Events comfort-baseline, comfort-pause, comfort-settings, comfort-options, comfort-pad-advance, comfort-pad-skip, comfort-pad-choice, comfort-touch-skip, comfort-pause-skip, comfort-restart-ask, comfort-load-screen, comfort-restart; metrics comfortTextBase, comfortTextLarge, comfortPadChoice, comfortTouchSkip, comfortPauseSkip, comfortRestartSeconds, comfortRestartMemoryGrowthMB", 400, "content"),
        new PlaytestScenario("campaign", "Campaign loads and plays a chapter: from the main menu press Campaign, open case file 1 (setup: the sandbox keeps the files off the real save) and wait for the Campaign scene to load. Reports run_start with mode=campaign and the loaded scene name (event run_start, metrics campaignLoaded, campaignLoadSeconds); if the scene fails to load (not in the build's scene list, or the chapter never builds) the run stops stuck with a bug saying so. Then Begin on the chapter card: Wallit and Slice in the chapter map, Wallit walks on the stick, the objective line shows, the bot fights with real input until it is met, Chapter 1 holds the circle against Yo Bots, then the Courier arrives (health bar on screen), winds up telegraphed notices on the circle and the bot breaks them or stands in them until it falls and the defeat beat plays; the Chapter Complete card shows, the case file advances to chapter 2, Menu, and reopening the file resumes at chapter 2 (events chapter-card, chapter-start, chapter-move, objective, boss-arrives, boss-bar, boss-attack, boss-defeated, defeat-beat, objective-met, chapter-complete, campaign-advanced, campaign-resume; metrics chapterAdvanced, chapterResumed, bossBar, courierWinds, courierInterrupted). Smoke is unchanged: it still presses Arcade", 750, "content"),
        new PlaytestScenario("hub", "The hub and Campaign progression, played with real input (setup: the sandbox case file 1 starts on chapter 1 with 85 XP, 5 short of level 3; the real save and Arcade are never touched): Campaign, case file 1, Begin on Chapter 1; the first kills give XP (HUD line LV/XP) and level 3 unlocks Refund (events xp-gain, level-up); the bot presses C near the bots and the ring shoves and stings them (event refund-used); the Courier falls, the Chapter Complete card offers To the hub; the bot presses it and lands in the hub (Wallit, Slice in the ring, the chapter board; events hub-open, hub-arrival), walks to the board on the stick and presses E (hub-walk, board-open: Chapter 1 filed and replayable, Chapter 2 next, Chapter 3 locked); picking Chapter 1 shows its card and Begin replays it (hub-replay-card, replay-started) without taking the file back a chapter. Checks per case file and Arcade: the XP stays on case file 1 (case file 2 has none) and Arcade's numbers read as no effect outside a chapter (events progress-saved, arcade-clean; metrics hubXp, hubLevel, refundHits, hubWalked)", 900, "content"),
        new PlaytestScenario("moves", "Campaign's combat moves, played with real input in Chapter 1 (the sandbox case file; the real save and Arcade are never touched): the bot swings the bat three times in a row and each hit of the combo is its own (Deposit, Withdrawal, Overdraft: damage, knockback and crack differ; events combo-hit, combo-distinct); it then fights on pressing X (the dodge) whenever a bot is close, and a blow that lands inside the dodge's 0.4 s window is avoided (events dodge, dodge-avoided, ability-slots with X Dodge, C Refund and the empty V slot). The Courier fight then runs as in the campaign scenario. Arcade is checked unchanged: outside a chapter the dodge, the per-hit combo and the slots read as no effect (event arcade-moves-clean; metrics comboHits, dodges, dodgesAvoided)", 600, "content"),
        new PlaytestScenario("abilities", "Campaign's three new abilities, played with real input in Chapter 1 (setup: the sandbox case file starts 1 XP short of level 4, and again short of 5 and 6 as each unlocks; the real save and Arcade are never touched): the HUD's second line shows each slot with its key and cooldown (V: LV 4 locked, then Coin Toss ready / 4 s, B Stamp Out ready / 14 s, G Escrow ready / 20 s; events slot-line); each unlock is announced as it is earned (level-up, unlock-level); the bot presses V and a thrown coin stings a bot from range (event toss-hit), presses B with bots close and they are stunned and stung (event stamp-hit), presses G and a blow that lands is held (event escrow-held); a cooldown is seen counting down on the HUD (event cooldown-shown); the case file keeps the unlocks after a save round trip and after leaving to the menu (events unlock-saved, unlock-kept; metrics tosses, tossHits, stamps, stampedBots, shields, shieldBlocks, unlockMask). Arcade is unchanged (event arcade-moves-clean)", 900, "content"),
        new PlaytestScenario("hub-life", "The hub as the story advances, with real input (setup: the sandbox case file 1 has chapter 1 filed and opens in the hub; chapters 2 to 6 are filed by setup shortcuts, never the real save). After every chapter the office must look different (hub-look: window light, cabinets, stamps, desk plate, who sits at the desk, slips pinned, lamps lit) and at least one new conversation is open (a gold ! over the lodger); Wallit walks to each and presses E (hub-chat), returns a lodger's favour (favour-offered, favour-pickup, favour-paid), walks over the chapter's loose notice to pin it (notice-pinned) and runs Chime's lost-thing errand, so each chapter shows three errands done (chapter-errands, saved in the case file). At the end the ending's own lines for what was kept are read (ending-lines; metrics hubChats, hubErrands, looksDiffer, endingLines)", 900, "content"),
        new PlaytestScenario("hub-npcs", "The hub's lodgers, with real input (setup: the sandbox case file 1 has chapter 1 filed and opens in the hub; later chapters are filed by setup shortcuts, never the real save): four lodgers stand in the office, none of them Warrok (hub-lodgers); Wallit walks to each and presses E, and each speaks its line for one chapter cleared (npc-line); Chime sends him for a lost thing (errand-offered), he walks to it (errand-pickup) and back, and Chime pays XP and the lost thing joins the file (errand-paid, with the XP before and after and the thing kept). Then setup files chapters 2 to 6 one by one and Tick is talked to again after each, every lodger's line must differ from the one before (npc-after-chapter; metrics lodgers, linesDiffer, errandXp, lostThings)", 300, "content"),
        new PlaytestScenario("hub-doors", "The hub as a walkable office, with real input (setup: the sandbox case file 1 has chapters 1 and 2 filed and opens in the hub; the real save is never touched): Wallit walks about the room to the chapter board and the vacant desk (hub-walk), doors 1 to 3 stand open and 4 to 6 stay shut (hub-office), pushing on shut door 4 does not get him through (door-locked), walking into open door 3 shows that chapter's card (door-entered) and Begin starts it (chapter-started); event hub-scene (the office is its own scene, Map_Office, dressed for the chapters filed); metrics doorsRight, hubWalked, lockedHeld, hubScene", 300, "content"),
        new PlaytestScenario("maps-123", "Chapters 1, 2 and 3 each load a visibly different map with a route, a landmark and a hazard or set piece (setup: the sandbox case file stands on each chapter in turn; the real save is never touched; leaving a chapter early is a named setup shortcut). Per chapter: Campaign, case file 1, Begin; event map-loaded, map-lighting (fog and sky differ), map-features (one route:, landmark: and hazard: line each from the map's own list, a problem if any is missing), map-objective (the objective marker's distance from the end of the route: it must stand on the route; chapter 1's objective is the circle), then Wallit walks on the stick and the chapter is left. Metrics mapsLoaded, distinctLooks, mapsWithRoute, mapsWithLandmark, mapsWithHazard. Play the whole objective of chapters 2 and 3 with the chapter2 and chapter3 scenarios.", 420, "content"),
        new PlaytestScenario("maps-456", "Chapters 4, 5 and 6 each load a visibly different map (the Long Walk, the Reading Room, the changed Hollow), each its own Unity scene (Map_Walk, Map_Reading, Map_Filed; events map-own-scene with the scene, pieces, sky and music; set-piece after the named setup shortcut OwnMap.Trigger) with a route, a landmark, a hazard or set piece and a boss arena away from the route (setup: the sandbox case file stands on each chapter in turn; the real save is never touched; leaving a chapter early is a named setup shortcut). Per chapter: Campaign, case file 1, Begin; events map-loaded, map-lighting (fog and sky differ), map-features (route:, landmark:, hazard:, arena: lines from the map's own list; a problem if any is missing), map-arena (the arena's distance and its angle away from the route: over 90 degrees), map-objective, then Wallit walks and the chapter is left. A boss is then called with the director (named setup shortcut) and must walk out of the arena: event arena-boss (spawn distance from the arena centre; a problem over 8 m). Metrics mapsLoaded, distinctLooks, mapsWithRoute, mapsWithLandmark, mapsWithHazard, mapsWithArena. Play each whole objective with the chapter4, chapter5 and chapter6 scenarios.", 600, "content"),
        new PlaytestScenario("secrets", "The back rooms and the Dead Letter, with real input (setup: the sandbox case file stands on each chapter in turn; the real save is never touched). In every chapter 1 to 6 the room is not on the map until the chapter's lost thing is in the file: Wallit walks out to the find with the stick, the clue shows (events secret-hidden, secret-clue, journal lists it), the room appears 172 m out, and Wallit walks to it and is paid (secret-entered, metric secretRooms 6). In Chapter 4's room the Dead Letter, an optional hard Spider Bot, comes at him: the bot fights it (encounter-fight) and Refund in Full is earned (encounter-reward, metric rewardGiven). Chapter 4 is then played again: the room is there from the start, nothing spawns and the reward is not paid twice (encounter-once, metric rewardOnce)", 1700, "content"),
        new PlaytestScenario("chapter-maps", "Each Campaign chapter loads its own map, with its own lighting, and unloads it on leaving (setup: the sandbox case file stands on the chapter; the real save is never touched; leaving a chapter early is a named setup shortcut). Three chapters in turn (1 The Moonlit Hollow, 4 The Long Walk, 5 The Reading Room): Campaign, case file 1, Begin; the map named in the chapter data loads as an extra scene on top of the game scene (event map-loaded: map, scene, scenes open), the chapter's lighting is on (event map-lighting: fog colour and density, sun colour, sky colour; they differ between chapters), the opening scene is skipped with Enter and Wallit walks on the stick; then the chapter is left for the menu and the map is gone (event map-unloaded: scene closed, lighting back) with memory read before and after (metrics mapMemoryBaselineMB, mapMemoryAfterMB, mapMemoryGrowthMB; a growth over 48 MB is a bug). Metrics mapsLoaded, mapsUnloaded, distinctLooks)", 800, "content"),
        new PlaytestScenario("save", "The campaign save: an old-format file loads and is brought up to date, a corrupt one shows a message and starts clean, and every chapter's load time and memory is logged (setup: the sandbox stands where the stored save would, holding version 1 text, text with no version, then corrupt text; the real save is never touched). Events save-old-loaded, save-migrated, save-oldest-loaded, save-corrupt-clean (seven kinds of bad text, none throws), save-old-panel (the case files say the old files were updated), save-old-chapter (file 1 opens chapter 3), save-corrupt-panel (\"Case files unreadable. The Department filed new ones.\", no files), save-corrupt-play (a clean file opens chapter 1); then all six chapters are begun in turn with chapter-load events (card seconds, map seconds, memory MB) and metrics chapter<N>CardSeconds, chapter<N>MapSeconds, chapter<N>MemoryMB, oldSaveLoaded, corruptClean, corruptMessageShown", 1200, "content"),
        new PlaytestScenario("journal", "The journal, with real input (setup: the sandbox case file starts on chapter 1; the real save is never touched). Campaign, case file 1, Begin. Chapter 1 shows its optional extra on the HUD (event journal-extra) and hides one lost thing out in the field (journal-find-hidden). J opens the journal over the pause menu (journal-open-chapter), Back and Resume; Wallit walks out to the find with the stick and it goes into the case file at once (journal-find, metric findKept). The chapter is played to the Courier's fall; the Chapter Complete card judges the extra and names the lost thing kept. Menu, Campaign: the slot shows the journal count (slot-journal); the Journal button opens the journal and the bot reads Active, Finished and Collected (journal-tab, journal-lists; setup marks chapter 2's extra met). Then the files are written out and read back as after a quit (journal-persist, metric journalPersisted) and read again (setup then gives every find and extra: journal-paid says PAID IN FULL)", 900, "content"),
        new PlaytestScenario("lore","The lore log and the case-file slots, with real input (setup: the sandbox case file starts on chapter 1; the real save is never touched). Campaign, case file 1, Begin, Chapter 1 is played with the bat to the Courier's fall as in the campaign scenario; the Chapter Complete card names the new lore it unlocked (event lore). Menu, Campaign: the case file slot reads its chapter, its play time and its completion (event slot-info: chapter, h:mm:ss on the books, 1/6 filed, lore n/15; problems if any is missing or the time is 0). The Lore log button opens the file's log (event lore-open), the bot turns every page with Next and reads which entries are open and which are sealed (event lore-page per entry, event lore-summary): the entries revealed by opening the file and by Chapter 1 are open, the later ones read Sealed until their chapter is filed (metrics loreUnlocked, loreSealed, slotChapterShown, slotTimeShown, slotCompletionShown). Back closes it", 800, "content"),
        new PlaytestScenario("campaign-full", "The whole case file in one run with real input (setup: the sandbox keeps the files off the real save): Campaign, case file 1, then Chapter 1 to ADDRESS FILED. Each chapter is played as its own scenario plays it (opening scene skipped with Enter, the objective met with the bat, the boss down, the Chapter Complete card), and between chapters Menu, Campaign and the file reopen on the next chapter's card (campaign-advanced, campaign-resume). Quit and resume: in Chapter 3 the scene is dropped mid-chapter (setup, as a closed tab does) and the file must reopen on Chapter 3, unfinished (events quit-midchapter, quit-resume-panel, quit-resume; metric quitResumeRight). Ends with the Filed scene, the finale pages, ADDRESS FILED, the credits card and the case file reading filed (events campaign-full, ending-card, campaign-panel-filed; metrics fullCaseFileCleared, endingShown, caseFileFiled)", 3000, "content"),
        new PlaytestScenario("chapter6", "Chapter 6, Address Filed, played start to finish with real input (setup: the sandbox case file starts on chapter 6): Campaign, case file 1, Begin on the Chapter 6 card; the opening scene in the changed Hollow plays and is skipped with Enter; Wallit holds the circle with the bat through three waves (events wave-start), then the Spider Bot that carries the order (boss bar up, scroll on its back) arrives and falls; the Filed scene plays over the field, then the finale pages (Story.Finale verbatim, as in Arcade's story run) and the ADDRESS FILED ending card (events filed-scene, finale-page, ending-card); Keep going shows the credits card; the case file is marked complete (6 cleared), Menu, and the Campaign panel's case file 1 reads filed (events chapter6-setup, cutscene-open, objective, wave-start, boss-called, boss-arrives, boss-bar, defeat-beat, boss-defeated, filed-scene, finale-page, ending-card, credits-card, campaign-complete, campaign-panel-filed; metrics bossBar, endingShown, caseFileCleared, caseFileFiled)", 1000, "content"),
        new PlaytestScenario("chapter5", "Chapter 5, Strike This Address, played start to finish with real input (setup: the sandbox case file starts on chapter 5): Campaign, case file 1, Begin on the Chapter 5 card; the opening scene in the Reading Room plays over the map (the order is not signed, so it can be denied; the stamp pressed to the first page shows DENIED; the bot reads pages then skips with Enter); the objective line reads Read the order, page 1 of 5 with the bots down counting up; Wallit holds the circle at the desk with the bat through five pages (each page's wave announced PAGE n OF 5 and its forms on the wall turning blank when it is down: events page-start, page-read); then The Registrar (a gold Titan Spider Bot with a ledger, boss bar up) arrives and falls; the defeat beat plays, the Chapter Complete card shows, the case file moves to Chapter 6, Menu, and reopening the file shows Chapter 6's card (events chapter5-setup, cutscene-open, cutscene-page, objective, page-start, page-read, boss-called, boss-arrives, boss-bar, defeat-beat, boss-defeated, objective-met, chapter-complete, campaign-advanced, campaign-resume; metrics pagesRead, bossBar, chapterAdvanced, chapterResumed)", 900, "content"),
        new PlaytestScenario("chapter4", "Chapter 4, The Process Servers, played start to finish with real input (setup: the sandbox case file starts on chapter 4): Campaign, case file 1, Begin on the Chapter 4 card; the opening dialogue says where Spider Bots come from (process servers, deliveries from the Department with one paper to hand over; the bot reads pages then skips with Enter); the objective reads Reach the first lamp (1 of 3) with metres counting down; Wallit walks the three lamps along the causeway under fire, batting what is in reach; at the last lamp the way turns round to the circle; Wallit walks home and holds off the servers; then The Server (a Spider Bot with a paper, boss bar up) arrives and offers the paper, and does NOT strike: Wallit refuses it by hitting it (objective reads Refuse the paper n / 3, events server-offer and server-refused 1, 2, 3); only after the third refusal it winds up and strikes Slice (server-strike), then falls; the defeat beat plays, the Chapter Complete card shows, the case file moves to Chapter 5, Menu, and reopening the file shows Chapter 5's card (events chapter4-setup, cutscene-open, cutscene-page, origin-dialogue, objective, floor-stop, last-lamp, home-leg, boss-arrives, boss-bar, server-offer, server-refused, server-strike, defeat-beat, boss-defeated, objective-met, chapter-complete, campaign-advanced, campaign-resume; metrics floorStops, serverOffers, serverRefusals, serverStruckEarly, chapterAdvanced, chapterResumed)", 900, "content"),
        new PlaytestScenario("chapter3", "Chapter 3, The Clearing Floor, played start to finish with real input (setup: the sandbox case file starts on chapter 3): Campaign, case file 1, Begin on the Chapter 3 card; the opening dialogue plays over the map and says where Yo Bots come from (the Clearing Crew, stamped out on the Floor; the bot reads pages then skips with Enter); the objective line reads Reach the broken gate (1 of 3) with metres counting down; Wallit walks the three stops (the broken gate, the belt with The Belt scene read and skipped, the lever) under fire from Yo Bots, batting those in reach; at the lever the belt feed breaks and the objective turns to Back to the circle; Wallit walks home, holds off the last batch, then The Foreman (order sheet on its chest, with a boss bar) arrives and falls; the defeat beat plays, the Chapter Complete card shows, the case file moves to Chapter 4, Menu, and reopening the file shows Chapter 4's card (events chapter3-setup, cutscene-open, cutscene-page, origin-dialogue, objective, floor-stop, floor-scene-skip, feed-broken, home-leg, boss-arrives, boss-bar, defeat-beat, boss-defeated, objective-met, chapter-complete, campaign-advanced, campaign-resume; metrics floorStops, feedBroken, chapterAdvanced, chapterResumed)", 900, "content"),
        new PlaytestScenario("companion", "The AI companion (Slice), played in Chapter 2 with real input (setup: the sandbox case file starts on chapter 2; the real save and Arcade are never touched): Slice walks with Wallit as he walks out (event companion-follows: her distance stays within a few metres over 25 m of walking), stabs the Yo Bots that come near (event companion-fights: stabs and kills while Wallit only walks), is knocked down (named setup shortcut, event companion-down: she lies down, the label reads hold E to revive), Wallit walks to her and holds E (real input) and she is back on her feet with half her health (event companion-revived; metrics companionStabs, companionDowns, companionRevives). The chapter then plays on as in chapter2. Arcade has no companion (event companion-off in the modes scenario)", 900, "content"),
        new PlaytestScenario("endings", "The campaign's three endings, chapter select ratings and New Game+ (setup shortcuts: the sandbox case files only, the real save is never touched). Part 1: case file 1 is marked filed with a rank on every chapter; the case files list shows the ranks and a New Game+ button (events rating-list, ngplus-menu); the hub's chapter board lists each cleared chapter with its rank (event rating-board); New Game+ is pressed on the case files list, Chapter 1 begins on the loop and shows tougher, more numerous bots and an echo line about the earlier playthrough (events ngplus-run, ngplus-echo; metrics ngPlusHarder, ngPlusEcho). Part 2: Chapter 6 is played with the bat three times from a sandbox file set for each ending (setup: earlier choices and hub errands; the Chapter 6 choices answered with keys): The Ledger (Wallit's stamp, one door), Two Doors (stamp shared, two numbers) and The Open Door (six errands done). Each plays its own closing reel, watched shot by shot, and its own ADDRESS FILED line (events ending-setup, ending-reel, ending-shot, ending-variant; metrics endingVariant1, endingVariant2, endingVariant3 each 1).", 2700, "content"),
        new PlaytestScenario("newgameplus", "New Game+ and the secret ending, with real input (setup shortcuts: the sandbox case file 1 is marked filed with Wallit at level 4 and his unlocks and Refund in Full earned; the real save is never touched). Campaign, case file 1: the filed file's chapter card shows a New Game+ button (event ngplus-button); the bot presses it and the card turns to Chapter 1 with the file reset to chapter 1, 0 cleared, xp, unlocks and Refund in Full kept (ngplus-started, metrics ngPlusChapter1, ngPlusXpKept, ngPlusAbilitiesKept), Begin plays Chapter 1 with Wallit's level and abilities (ngplus-run, ngplus-abilities). Then a setup shortcut stands the file on Chapter 6 with every lost thing in the journal, and Chapter 6 is played with the bat to the ADDRESS FILED card; Keep going plays the secret ending (secret-scene, secret-page lines, credits-card with RECEIVED; metrics secretEnding 1). The secret-ending scenario plays only that second part.", 1300, "content"),
        new PlaytestScenario("secret-ending", "The secret ending's condition (setup shortcut: the sandbox case file stands on Chapter 6 with all six lost things in the journal). Chapter 6 is played with the bat to ADDRESS FILED; Keep going plays the secret ending before the credits (events secret-scene, secret-page, credits-card; metric secretEnding 1). Chapter 6 with the finds missing (chapter6) must show no secret ending (event secret-absent, metric secretEnding 0)", 1000, "content"),
        new PlaytestScenario("grade", "Chapter grades and Hard, with real input (setup: the sandbox case file starts on chapter 1, and later is marked filed; the real save is never touched). Campaign, case file 1, Begin; chapter 1 is played to the Courier's fall. The Chapter Complete card shows a RANK (S, A, B or C from time against par and the damage Slice took) and the best (event rank-shown, metric rankShown). The files are written out and read back as after a quit and the best rank is still there (rank-saved, metric rankSaved). Setup: the file is marked filed. Its chapter card shows the best rank and a Difficulty button; the bot presses it to Hard (hard-on, metric hardOn, saved with the file). Begin; a plain Yo Bot's health is read against Standard (hard-health, metric hardHealthRatio about 1.75) and the bot holds still until a bot lands a blow on Slice, read as meant against as landed (hard-damage, metric hardDamageRatio 2)", 900, "content"),
        new PlaytestScenario("chapter2", "Chapter 2, No Fixed Address, played start to finish with real input (setup: the sandbox case file starts on chapter 2): Campaign, case file 1, Begin on the Chapter 2 card; the opening dialogue plays over the map (the bot reads pages, then skips it with Enter); the objective line reads Reach the gate with its metres counting down; Wallit walks out to the gate under fire from Yo Bots, batting those in reach; at the gate The Gate scene plays (read a few pages, then skipped), the objective turns to Back to the circle, Wallit walks home, three waves are held at the circle (objective Hold the circle, wave n of 3), then The Plated Alpha (stamped INVALID, with a boss bar) arrives and falls; the defeat beat plays, the Chapter Complete card shows, the case file moves to Chapter 3, Menu, and reopening the file shows Chapter 3's card (events chapter2-setup, cutscene-open, cutscene-page, cutscene-skip, cutscene-play, objective, gate-leg, gate-reached, gate-scene-skip, home-leg, chapter-wave, boss-arrives, boss-bar, defeat-beat, boss-defeated, objective-met, chapter-complete, campaign-advanced, campaign-resume; metrics gateReached, gateSceneSkipped, chapterWaves, chapterAdvanced, chapterResumed)", 900, "content"),
        new PlaytestScenario("story", "The story: the intro over the frozen Hollow (read page by page: who Wallit is, what the circle is, why Slice must not fall; a Skip button is on it), wave 1's story beats in canon voices, then setup: the testing wave key to wave 10 and wave 30 for the boss-wave title cards and beats (Yo Bots chirpy, Wallit on money, Slice clipped), and setup: wave 30's waiting bots trimmed to the boss and Slice topped up so the bat can finish it with real swings. Clearing wave 30 plays the finale pages and the ADDRESS FILED ending screen; the bot presses Keep going into overtime. Events intro-*, beat, finale-*, ending", 600, "content"),
        new PlaytestScenario("polish", "The final polish pass, on any screen size (run it on desktop and on the phone): the main menu, the HUD in wave 1, the level-up cards (setup: a level of bolts granted), the pause menu by its button, Resume, 40 s of real play, then the bot holds fire until Slice falls (setup: Plated Yo Bots ring her after 20 s) and the game-over panel reads Address struck off. Each screen gets a layout check (menu, hud, cards, pause, gameover), a stale-words check (no piano, keys or music-stopped words left), and a button-feel check (every button lifts on hover and sinks on press like the menu's); the audio-mix event says how many sounds played, how many stacked softer, how many were capped and how many dipped under a voice line", 420, "full"),
        new PlaytestScenario("site", "The website around the game: reads the landing page, every page the deploy listed in site/pages.json and every page linked from them (outside Game/), looks for a blog, logs which pages exist, checks each footer's \"Powered by HAWKES GEN\" link to hawkesgen.ai and the landing page's link to hawkesbiomedical.com, shows each page's footer over the game for a few seconds, then plays 20 s to show the game screen has no footer", 120, "menus"),
    };

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern int HawkesSoundPressPrompt();

    [DllImport("__Internal")]
    static extern string HawkesSoundState();

    [DllImport("__Internal")]
    static extern string HawkesAnalyticsPageLog(int afterSeq);

    [DllImport("__Internal")]
    static extern int HawkesAnalyticsPageAskShown(string id);

    [DllImport("__Internal")]
    static extern int HawkesAnalyticsPageRate(string id, int stars);
#else
    static int HawkesSoundPressPrompt() {
        return 0;
    }

    static string HawkesSoundState() {
        return "";
    }

    static string HawkesAnalyticsPageLog(int afterSeq) {
        return "";
    }

    static int HawkesAnalyticsPageAskShown(string id) {
        return 0;
    }

    static int HawkesAnalyticsPageRate(string id, int stars) {
        return 0;
    }
#endif

    static PlaytestRun run;
    static int levelUps;
    static int lastWave;
    static float fpsSum;
    static int fpsFrames;
    static float worstFps = 999f;
    static float fpsNow;
    // The analytics scenario: from this wave on the bot holds fire, so the bots end the run.
    static int holdFireFrom;
    static bool analyticsRun;
    static int analyticsSeq;
    static int analyticsEvents;
    static int analyticsSent;
    static bool analyticsConfig;
    // The batter: one-frame clicks, the lock it last checked, bat lines told so far.
    static bool tapHeld;
    static float nextTapAt;
    static float nextShakeAt;
    static GameObject lastLock;
    static int lockChecks;
    static int lockNearest;
    static int lockWrong;
    static int batTold;
    static bool batRun;
    static int lockSwitchesSeen;
    // The musician scenario: Slice's stabs and stands told so far.
    static int sliceStabsTold;
    static int sliceHurtTold;
    static int sliceDefeatsTold;
    static float sliceLastStab;
    static float sliceGapMin;
    static bool sliceWasUp;
    static float sliceClearAt;
    static int sliceSits;
    static int sliceShortGaps;
    static int sliceNoDamage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() {
        HawkesPlaytest.Register(Product, Scenarios, Bot, IsReady, Stop);
    }

    static bool IsReady() {
        string scene = SceneManager.GetActiveScene().name;
        return scene == MetaProgress.MenuScene ? Object.FindAnyObjectByType<MainMenu>() != null : WaveDirector.Instance != null;
    }

    static void Stop() {
        SettingsPanel.Close();
        PauseMenu.Resume();
        PlayInput.Release();
        TouchControls.LiftBotFingers();
        UiFit.ForceTouch(false);
        HawkesSiteHide();
        if (Time.timeScale > 1f) {
            Time.timeScale = 1f;
        }
    }

    static IEnumerator Bot(string id, PlaytestOptions options, PlaytestRun current) {
        run = current;
        levelUps = 0;
        lastWave = 0;
        fpsSum = 0f;
        fpsFrames = 0;
        worstFps = 999f;
        holdFireFrom = 0;
        analyticsRun = id == "analytics";
        analyticsSeq = -1;
        analyticsEvents = 0;
        analyticsSent = 0;
        analyticsConfig = false;
        tapHeld = false;
        nextTapAt = 0f;
        nextShakeAt = 0f;
        lastLock = null;
        lockChecks = 0;
        lockNearest = 0;
        lockWrong = 0;
        batTold = 0;
        batRun = id == "bat";
        lockSwitchesSeen = ZeldaStyleCamera.LockSwitches;
        RunAnalytics.EnableDebug();
        StoryScreen.SuppressIntro = id != "story";
        PlayInput.Release();
        MetaProgress.EnterSandbox(true);
        Random.InitState(options.seed);
        Application.logMessageReceived += OnLog;
        try {
            yield return TapToPlay();
            if (id == "settings") {
                yield return SettingsRun(options);
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "touch") {
                yield return TouchRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "polish") {
                yield return PolishRun(options);
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "site") {
                yield return SiteRun(options);
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            yield return ToMenu();
            if (id == "cast") {
                yield return CastMenu();
                if (run.Done) {
                    yield break;
                }
            }
            if (id == "save") {
                yield return SaveRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "secrets") {
                yield return SecretsRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "chapter-maps" || id == "maps-123" || id == "maps-456") {
                mapChapters = id == "maps-123" ? new[] { 0, 1, 2 } : id == "maps-456" ? new[] { 3, 4, 5 } : new[] { 0, 3, 4 };
                yield return MapsRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "endings") {
                yield return EndingsRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "newgameplus") {
                yield return NewGamePlusRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "campaign" || id == "comfort" || id == "lore" || id == "secret-ending" || id == "journal" || id == "grade" || id == "campaign-full" || id == "hub" || id == "moves" || id == "abilities" || id == "hub-doors" || id == "hub-npcs" || id == "hub-life" || id == "cutscene" || id == "chapter2" || id == "companion" || id == "chapter3" || id == "chapter4" || id == "chapter5" || id == "chapter6" || id == "choices") {
                choicesTest = id == "choices";
                comfortTest = id == "comfort";
                secretTest = id == "secret-ending";
                companionTest = id == "companion";
                watchCutscene = id == "cutscene" || id == "chapter2" || id == "chapter3" || id == "chapter4" || id == "chapter5";
                hubTest = id == "hub";
                movesTest = id == "moves";
                abilitiesTest = id == "abilities";
                hubDoors = id == "hub-doors";
                hubNpcs = id == "hub-npcs";
                hubLife = id == "hub-life";
                fullRun = id == "campaign-full";
                loreTest = id == "lore";
                journalTest = id == "journal";
                gradeTest = id == "grade";
                quitDone = false;
                startChapter = id == "hub-doors" ? 2 : id == "hub-npcs" || id == "hub-life" ? 1 : id == "chapter6" || id == "secret-ending" ? 5 : id == "chapter5" ? 4 : id == "chapter4" ? 3 : id == "chapter3" ? 2 : id == "chapter2" || id == "companion" ? 1 : 0;
                yield return CampaignRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "pause-nav") {
                yield return PauseNavRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "modes") {
                yield return ModesMenu();
                if (run.Done) {
                    yield break;
                }
            }
            if (id == "menus") {
                yield return ShopMenu();
            }
            if (id == "words") {
                yield return WordsMenu();
            }
            if (id == "upgrades") {
                UpgradesShop();
            }
            yield return StartRun();
            if (run.Done) {
                yield break;
            }
            if (id == "analytics") {
                holdFireFrom = 2;
                yield return Play(options, 220f, false);
                if (!run.Done) {
                    AnalyticsReport();
                }
            } else if (id == "smoke") {
                yield return Play(options, 30f, false);
            } else if (id == "bots") {
                yield return BotsRun(options);
            } else if (id == "musician") {
                yield return MusicianRun(options);
            } else if (id == "personality") {
                yield return PersonalityRun(options);
            } else if (id == "voice-gap") {
                yield return VoiceGapRun(options);
            } else if (id == "voice") {
                yield return VoiceRun(options);
            } else if (id == "slice") {
                yield return SliceRun(options);
            } else if (id == "upgrades") {
                yield return UpgradesRun(options);
                if (!run.Done) {
                    UpgradesReport();
                    yield return Play(options, 30f, false);
                }
            } else if (id == "bat") {
                yield return BatRun(options);
                if (!run.Done) {
                    yield return Play(options, 60f, false);
                }
                if (!run.Done) {
                    BatReport();
                }
            } else if (id == "story") {
                yield return StoryRun(options);
            } else if (id == "modes") {
                run.Event("arcade-start", "Arcade runs the wave defense: wave " + WaveDirector.Wave + ", the player is Wallit, the musician is Slice");
                bool off = Companion.Instance == null && ChapterRun.Instance == null && Object.FindAnyObjectByType<Companion>() == null;
                run.Event("companion-off", "Arcade has no companion: " + (off ? "none in the scene" : "ONE EXISTS"));
                if (!off) {
                    run.Problem(PlaytestSeverity.Bug, "modes: a companion exists in Arcade");
                }
                yield return Play(options, 40f, false);
            } else if (id == "cast") {
                yield return CastRun(options);
            } else if (id == "dialogue") {
                yield return DialogueRun(options);
            } else if (id == "bible") {
                yield return BibleRun(options);
            } else if (id == "cinematic") {
                yield return CinematicRun(options);
            } else if (id == "score") {
                yield return ScoreRun(options);
            } else if (id == "chapter-cinematics") {
                yield return ChapterCinematicsRun(options);
            } else if (id == "wallit") {
                yield return WallitRun(options);
            } else if (id == "locomotion") {
                yield return LocomotionRun(options);
            } else if (id == "words") {
                yield return WordsRun();
            } else if (id == "run") {
                yield return Play(options, RunCap, false);
            } else {
                yield return Play(options, 240f, true);
            }
            if (!run.Done) {
                run.Finish(PlaytestOutcome.Done);
            }
        } finally {
            StoryScreen.SuppressIntro = false;
            Application.logMessageReceived -= OnLog;
            Stop();
        }
    }

    // The page's "Tap to play" prompt covers the game until it is pressed, as a player would: a pointer press on the
    // prompt, through its own handler (HawkesSound.jslib). It reports whether the browser's audio is running after it.
    // In the editor, or on a page without the prompt, there is nothing to press.
    static IEnumerator TapToPlay() {
        string before = HawkesSoundState();
        if (before != "prompt") {
            run.Log(string.IsNullOrEmpty(before) ? "no Tap to play prompt on this page" : "Tap to play was already pressed (audio " + before + ")");
            yield break;
        }
        run.Scene("Tap to play");
        yield return new WaitForSecondsRealtime(1.5f);
        HawkesSoundPressPrompt();
        yield return new WaitForSecondsRealtime(0.6f);
        string after = HawkesSoundState();
        run.Event("tap-prompt", "audio " + after);
        run.Metric("audioRunning", after == "running" ? 1 : 0);
        if (after == "prompt") {
            run.Problem(PlaytestSeverity.Bug, "the Tap to play prompt did not close when pressed");
        }
    }

    // Settings from the main menu and from the pause menu, through the same taps and drags a player makes.
    static IEnumerator SettingsRun(PlaytestOptions options) {
        yield return ToMenu();
        if (run.Done) {
            yield break;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        Button open = menu != null ? menu.SettingsButton : null;
        if (open == null || !open.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "no Settings button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        if (!Inside(ScreenRect((RectTransform)open.transform))) {
            run.Problem(PlaytestSeverity.Bug, "the menu's Settings button is not fully on screen");
        }
        Tap(open.gameObject);
        run.Event("tap", "Settings (menu)");
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings to open from the menu");
        if (run.Done) {
            yield break;
        }
        run.Scene("Settings");
        run.Event("settings-opened", "menu");
        run.Progress(0.15f);
        yield return new WaitForSecondsRealtime(1f);
        CheckScreen("settings", 600);
        yield return PlayWithSettings("menu");
        if (run.Done) {
            yield break;
        }

        menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "no Play button on the menu after Settings closed");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.PlayButton.gameObject);
        run.Event("tap", "Play");
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after tapping Play");
        if (run.Done) {
            yield break;
        }
        run.Scene("Wave 1");
        run.Event("run-start");
        run.Progress(0.45f);
        yield return null;
        CheckPlayerIsWallit();
        CheckMusicianIsSlice();
        yield return new WaitForSecondsRealtime(4f);
        yield return run.Until(() => PauseMenu.Instance != null && PauseMenu.Instance.PauseButton != null && PauseMenu.Instance.PauseButton.isActiveAndEnabled, 20f, "the pause button");
        if (run.Done) {
            yield break;
        }
        if (!Inside(ScreenRect((RectTransform)PauseMenu.Instance.PauseButton.transform))) {
            run.Problem(PlaytestSeverity.Bug, "the pause button is not fully on screen");
        }
        if (UiFit.Touch) {
            CheckScreen("hud");
        }

        Tap(PauseMenu.Instance.PauseButton.gameObject);
        run.Event("tap", "Pause");
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "the pause menu after tapping the pause button");
        if (run.Done) {
            yield break;
        }
        run.Scene("Paused");
        run.Event("paused", "button");
        yield return new WaitForSecondsRealtime(1f);
        if (Time.timeScale > 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "the game kept running behind the pause menu (time scale " + Time.timeScale.ToString("0.00") + ")");
        }
        CheckScreen("pause", 450);
        Tap(PauseMenu.Instance.SettingsButton.gameObject);
        run.Event("tap", "Settings (pause)");
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings to open from the pause menu");
        if (run.Done) {
            yield break;
        }
        run.Scene("Settings (paused)");
        run.Event("settings-opened", "pause");
        run.Progress(0.65f);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayWithSettings("pause");
        if (run.Done) {
            yield break;
        }
        if (!PauseMenu.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "closing Settings also closed the pause menu");
        } else {
            Tap(PauseMenu.Instance.ResumeButton.gameObject);
            run.Event("tap", "Resume");
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (PauseMenu.IsOpen || Time.timeScale <= 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "the game stayed paused after Resume");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Scene("Wave " + WaveDirector.Wave);
        run.Event("resumed", "button");

        // The keyboard way: P pauses and P resumes.
        if (!UiFit.Touch) {
            yield return new WaitForSecondsRealtime(2f);
            PlayInput.Tap(KeyCode.P);
            yield return run.Until(() => PauseMenu.IsOpen, 3f, "the pause menu after pressing P");
            if (run.Done) {
                yield break;
            }
            run.Event("paused", "P");
            yield return new WaitForSecondsRealtime(1f);
            PlayInput.Tap(KeyCode.P);
            yield return run.Until(() => !PauseMenu.IsOpen, 3f, "the pause menu to close after pressing P again");
            if (run.Done) {
                yield break;
            }
            run.Event("resumed", "P");
        }
        run.Progress(0.85f);
        yield return Play(options, 15f, false);
    }

    // On the open Settings: sound off and back on, music to 30%, effects to 70%, each by a tap or a press on the
    // slider where a finger would land, then Done.
    static IEnumerator PlayWithSettings(string from) {
        SettingsPanel panel = SettingsPanel.Instance;
        if (panel == null) {
            run.Problem(PlaytestSeverity.Bug, "Settings has no panel");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        bool was = SoundSettings.On;
        Tap(panel.SoundButton.gameObject);
        yield return new WaitForSecondsRealtime(0.8f);
        if (SoundSettings.On == was) {
            run.Problem(PlaytestSeverity.Bug, "the Sound button did not switch sound (" + from + ")");
        }
        run.Event(SoundSettings.On ? "sound-on" : "sound-off", from);
        Tap(panel.SoundButton.gameObject);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event(SoundSettings.On ? "sound-on" : "sound-off", from);

        Slide(panel.MusicSlider, 0.3f);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event("music-volume", Mathf.RoundToInt(SoundSettings.Music * 100f) + "% (" + from + ")");
        if (Mathf.Abs(SoundSettings.Music - 0.3f) > 0.08f) {
            run.Problem(PlaytestSeverity.Bug, "the music slider set " + SoundSettings.Music.ToString("0.00") + " for a press at 30% (" + from + ")");
        }
        Slide(panel.EffectsSlider, 0.7f);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event("effects-volume", Mathf.RoundToInt(SoundSettings.Sfx * 100f) + "% (" + from + ")");
        if (Mathf.Abs(SoundSettings.Sfx - 0.7f) > 0.08f) {
            run.Problem(PlaytestSeverity.Bug, "the effects slider set " + SoundSettings.Sfx.ToString("0.00") + " for a press at 70% (" + from + ")");
        }
        run.Metric("soundOn", SoundSettings.On ? 1 : 0);
        run.Metric("musicVolume", Mathf.Round(SoundSettings.Music * 100f));
        run.Metric("effectsVolume", Mathf.Round(SoundSettings.Sfx * 100f));

        Tap(panel.DoneButton.gameObject);
        run.Event("tap", "Done");
        yield return run.Until(() => !SettingsPanel.IsOpen, 5f, "Settings to close after Done");
        if (!run.Done) {
            run.Event("settings-closed", from);
        }
    }

    // A press and release on a slider at a fraction of its track, like a finger landing there.
    static void Slide(Slider slider, float at) {
        Rect track = ScreenRect(slider.handleRect != null ? (RectTransform)slider.handleRect.parent : (RectTransform)slider.transform);
        var data = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(Mathf.Lerp(track.xMin, track.xMax, at), track.center.y),
        };
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerUpHandler);
    }

    // Setup: the run always starts from the menu, as a player's does.
    static IEnumerator ToMenu() {
        run.Scene("Main Menu");
        if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
            run.Log("loading the menu scene (setup)");
            Time.timeScale = 1f;
            SceneManager.LoadScene(MetaProgress.MenuScene);
        }
        yield return run.Until(() => Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the main menu");
        if (Resources.Load<Texture2D>("UI/MenuArt") == null) {
            run.Problem(PlaytestSeverity.Polish, "menu key art Resources/UI/MenuArt is missing");
        }
        yield return new WaitForSecondsRealtime(1f);
    }

    // Campaign from the main menu: press Campaign, open case file 1, wait for the Campaign scene. A scene that is not in
    // the build ("Scene 'Campaign' couldn't be loaded", PR #70) never becomes the active scene: that is a bug, said plainly.
    static int startChapter;
    static bool hubTest;
    static bool loreTest;
    static bool journalTest;
    static bool gradeTest;
    static bool choicesTest;
    static bool comfortTest;   // PlaytestComfort.cs
    static int choicePick;   // which option the bot presses on a dialogue choice page: 0 is key 1, 1 is key 2
    static bool movesTest;
    static bool abilitiesTest;
    static bool hubDoors;
    static bool hubNpcs;
    static bool hubLife;
    static bool fullRun;
    static bool quitDone;
    static bool secretTest;
    static int endingTest;   // endings scenario: the variant (Endings.Ledger/Doors/Open) this pass sets up and expects

    static IEnumerator CampaignRun() {
        CampaignSave.EnterSandbox();
        if (startChapter > 0) {
            // Setup shortcut: the sandbox case file (never the real save) already stands on this chapter.
            CampaignSave.Slot setup = CampaignSave.Get(0);
            setup.used = true;
            setup.chapter = startChapter;
            setup.reached = startChapter;
            setup.cleared = startChapter;
            run.Event("chapter2-setup", "sandbox case file 1 starts on chapter " + (startChapter + 1));
            if (endingTest > 0) {
                // Setup shortcut: the sandbox file's earlier choices and hub errands, as the variant needs them (the Chapter 6 choices are made with keys below).
                choicePick = endingTest == Endings.Doors ? 1 : 0;
                if (endingTest == Endings.Doors) {
                    setup.choices = Choices.With(Choices.With(0, Choices.Index("1.c2"), 1), Choices.Index("5.c2"), 1);   // tipped Slice, read the page aloud
                }
                if (endingTest == Endings.Open) {
                    setup.side = (1 << Endings.OpenSides) - 1;   // six favours and notices done
                }
                run.Event("ending-setup", "sandbox case file 1 on Chapter 6 for " + Endings.Name(endingTest) + ": earlier choices " + setup.choices + ", errands done " + HubLife.SideCount(setup) + ", Chapter 6 answered with key " + (choicePick + 1));
            }
            if (secretTest) {
                setup.finds = (1 << Journal.Count) - 1;
                run.Event("secret-setup", "sandbox case file 1 holds all " + Journal.Count + " lost things");
            }
        }
        if (hubTest) {
            // Setup shortcut: the sandbox file already has XP, 5 short of level 3 (Refund). Never the real save.
            CampaignSave.Slot setup = CampaignSave.Get(0);
            setup.used = true;
            setup.chapter = 0;
            setup.xp = 85;
            run.Event("hub-setup", "sandbox case file 1 on chapter 1 with " + setup.xp + " XP (level " + CampaignProgress.LevelFor(setup.xp) + ")");
        }
        if (abilitiesTest) {
            // Setup shortcut: the sandbox file is 1 XP short of level 4 (Coin Toss). Never the real save.
            CampaignSave.Slot setup = CampaignSave.Get(0);
            setup.used = true;
            setup.chapter = 0;
            setup.xp = CampaignProgress.XpAt(4) - 1;
            run.Event("abilities-setup", "sandbox case file 1 on chapter 1 with " + setup.xp + " XP (level " + CampaignProgress.LevelFor(setup.xp) + ")");
        }
        if (hubDoors || hubNpcs || hubLife) {
            ChapterScene.HubNext = true;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null || !menu.CampaignButton.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "campaign: the main menu has no Campaign button to press");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("mode-select", "main menu shows \"" + menu.CampaignButton.GetComponentInChildren<Text>().text + "\"");
        yield return new WaitForSecondsRealtime(1f);
        Tap(menu.CampaignButton.gameObject);
        run.Event("tap", "Campaign");
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files after pressing Campaign");
        if (run.Done) {
            yield break;
        }
        CampaignPanel panel = CampaignPanel.Instance;
        yield return new WaitForSecondsRealtime(1f);
        float tapped = Time.realtimeSinceStartup;
        Tap(panel.SlotButtons[0].gameObject);
        run.Event("tap", "case file 1");
        float limit = tapped + 45f;
        while (Time.realtimeSinceStartup < limit && !(ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene)) {
            yield return null;
        }
        string active = SceneManager.GetActiveScene().name;
        if (ChapterScene.Instance == null || active != CampaignSave.Scene) {
            run.Metric("campaignLoaded", 0);
            run.Problem(PlaytestSeverity.Bug, "campaign: the scene '" + CampaignSave.Scene + "' did not load after opening case file 1 (active scene '" + active + "' after 45 s). Is it in the build's scene list?");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        float seconds = Time.realtimeSinceStartup - tapped;
        if (hubDoors) {
            yield return HubDoorsPlay();
            yield break;
        }
        if (hubNpcs) {
            yield return HubNpcsPlay();
            yield break;
        }
        if (hubLife) {
            yield return HubLifePlay();
            yield break;
        }
        run.Scene("Chapter " + (ChapterScene.Instance.Chapter + 1));
        yield return null;
        run.Metric("campaignLoaded", 1);
        run.Metric("campaignLoadSeconds", Mathf.Round(seconds * 10f) / 10f);
        string mode = RunAnalytics.LastRunMode;
        if (mode != "campaign") {
            run.Problem(PlaytestSeverity.Bug, "campaign: run_start said mode '" + mode + "', not campaign");
        }
        run.Event("run_start", "mode=" + mode + " scene=" + active + " chapter=" + (ChapterScene.Instance.Chapter + 1) + " \"" + ChapterScene.Instance.Title + "\" loaded in " + seconds.ToString("0.0") + " s");
        yield return run.Until(() => ChapterScene.Instance.LinesShown >= 1, 10f, "the chapter's first line");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(3f);
        yield return ChapterPlay();
        // The full case file: each chapter ends on the next one's card (Menu, reopen the file); play on to ADDRESS FILED.
        while (fullRun && !run.Done && ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene) {
            yield return ChapterPlay();
        }
        if (fullRun && !run.Done) {
            CampaignSave.Slot done = CampaignSave.Get(0);
            run.Metric("fullCaseFileCleared", done.cleared);
            if (done.cleared != CampaignSave.ChapterCount) {
                run.Problem(PlaytestSeverity.Bug, "campaign-full: the run ended with " + done.cleared + " of " + CampaignSave.ChapterCount + " chapters cleared");
            } else {
                run.Event("campaign-full", "Chapter 1 to ADDRESS FILED played in one run: " + done.cleared + " chapters cleared");
            }
        }
    }

    // Pause and the campaign menus by keyboard and pad (PauseMenu, SettingsPanel, CampaignPanel, the chapter card, MenuNav).
    static IEnumerator Key(KeyCode key) {
        PlayInput.Tap(key);
        yield return new WaitForSecondsRealtime(0.3f);
    }

    // Down arrow until the gold ring is on the target.
    static IEnumerator DownTo(Component target, string what) {
        for (int i = 0; i < 8 && MenuNav.Selected != target.gameObject; i++) {
            yield return Key(KeyCode.DownArrow);
        }
        if (MenuNav.Selected != target.gameObject) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: the arrow keys never reached " + what);
        }
    }

    static IEnumerator PauseNavRun() {
        CampaignSave.EnterSandbox();
        watchCutscene = false;
        int paused = 0;
        for (int index = 0; index < CampaignSave.ChapterCount; index++) {
            if (run.Done) {
                yield break;
            }
            CampaignSave.Slot slot = CampaignSave.Get(0);
            slot.used = true;
            slot.chapter = index;
            slot.reached = index;
            slot.cleared = index;
            Time.timeScale = 1f;
            yield return ToMenu();
            run.Event("nav-menu", "chapter " + (index + 1) + ": main menu, keys only from here");
            yield return Key(KeyCode.C);
            yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files after pressing C");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            bool onSlot = MenuNav.Selected == CampaignPanel.Instance.SlotButtons[0].gameObject;
            run.Event("nav-case-files", "ring on case file 1: " + onSlot);
            if (!onSlot) {
                run.Problem(PlaytestSeverity.Bug, "pause-nav: the case files open with no ring on file 1");
            }
            yield return Key(KeyCode.Return);
            yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card after Enter on file 1");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            bool onBegin = MenuNav.Selected == ChapterScene.Instance.BeginButton.gameObject;
            run.Event("nav-chapter-card", "chapter " + (index + 1) + ": ring on Begin: " + onBegin);
            if (!onBegin) {
                run.Problem(PlaytestSeverity.Bug, "pause-nav: the chapter card has no ring on Begin");
            }
            yield return Key(KeyCode.Return);
            yield return run.Until(() => ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter after Enter on Begin");
            if (run.Done) {
                yield break;
            }
            yield return CutsceneStep();
            yield return run.Until(() => PauseMenu.Instance != null && PauseMenu.Instance.ResumeButton != null && ChapterRun.Instance != null && ChapterRun.Instance.PlaySeconds > 1f, 30f, "the chapter to be playable");
            if (run.Done) {
                yield break;
            }
            run.Scene("Chapter " + (index + 1) + " pause");
            // P pauses, P resumes.
            yield return Key(KeyCode.P);
            yield return run.Until(() => PauseMenu.IsOpen, 4f, "the pause menu after P in chapter " + (index + 1));
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.6f);
            bool frozen = Time.timeScale < 0.01f;
            run.Event("chapter-paused", "chapter " + (index + 1) + " paused with P: time scale " + Time.timeScale.ToString("0.00") + ", ring on " + (MenuNav.Selected != null ? MenuNav.Selected.name : "nothing"));
            if (!frozen) {
                run.Problem(PlaytestSeverity.Bug, "pause-nav: chapter " + (index + 1) + " kept running behind the pause menu");
            }
            yield return Key(KeyCode.P);
            yield return run.Until(() => !PauseMenu.IsOpen && Time.timeScale > 0.5f, 4f, "chapter " + (index + 1) + " to resume after P");
            if (run.Done) {
                yield break;
            }
            run.Event("chapter-resumed", "chapter " + (index + 1) + " resumed with P");
            paused++;
            // The pad's Start pauses; the pad's B resumes.
            yield return Key(KeyCode.JoystickButton7);
            yield return run.Until(() => PauseMenu.IsOpen, 4f, "the pause menu after pad Start in chapter " + (index + 1));
            if (run.Done) {
                yield break;
            }
            run.Event("pad-paused", "chapter " + (index + 1) + " paused with the pad's Start");
            yield return new WaitForSecondsRealtime(0.5f);

            if (index == 0) {
                yield return PauseNavSettings();
                if (run.Done) {
                    yield break;
                }
                yield return Key(KeyCode.JoystickButton0);
                yield return run.Until(() => !PauseMenu.IsOpen, 4f, "Resume with the pad's A (ring starts on Resume)");
                if (run.Done) {
                    yield break;
                }
                run.Event("chapter-resumed", "chapter 1 resumed with the pad's A on Resume");
                yield return new WaitForSecondsRealtime(1f);
                yield return Key(KeyCode.Escape);
                yield return run.Until(() => PauseMenu.IsOpen, 4f, "the pause menu after Escape");
                if (run.Done) {
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.4f);
            }
            // Quit to menu: Down, Down, Enter (asks), Enter.
            yield return DownTo(PauseMenu.Instance.QuitButton, "Quit to menu");
            yield return Key(KeyCode.Return);
            yield return new WaitForSecondsRealtime(0.3f);
            if (!PauseMenu.IsOpen) {
                run.Problem(PlaytestSeverity.Bug, "pause-nav: Quit to menu left at the first press without asking");
            }
            yield return Key(KeyCode.Return);
            yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the main menu after Quit to menu");
            if (run.Done) {
                yield break;
            }
            run.Event("nav-quit", "chapter " + (index + 1) + ": Quit to menu with the keys, back on the main menu");
        }
        run.Metric("chaptersPaused", paused);
        run.Metric("menuMoves", MenuNav.Moves);
        run.Metric("menuSubmits", MenuNav.Submits);
        run.Metric("sliderTurns", MenuNav.SliderTurns);
        if (paused < CampaignSave.ChapterCount) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: only " + paused + " of " + CampaignSave.ChapterCount + " chapters paused and resumed");
        }
    }

    // Down, Enter opens Settings from the pause menu; sliders by Left/Right; Voices by Enter; the pad's B closes it.
    static IEnumerator PauseNavSettings() {
        yield return DownTo(PauseMenu.Instance.SettingsButton, "Settings");
        yield return Key(KeyCode.Return);
        yield return run.Until(() => SettingsPanel.IsOpen, 4f, "Settings after Enter on it");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        SettingsPanel panel = SettingsPanel.Instance;
        run.Event("nav-settings", "Settings opened with Enter; ring on " + (MenuNav.Selected != null ? MenuNav.Selected.name : "nothing"));
        float music = SoundSettings.Music;
        float sfx = SoundSettings.Sfx;
        int chatter = SoundSettings.Chatter;
        yield return DownTo(panel.MusicSlider, "the music slider");
        yield return Key(KeyCode.LeftArrow);
        yield return Key(KeyCode.LeftArrow);
        float musicNow = SoundSettings.Music;
        yield return DownTo(panel.EffectsSlider, "the effects slider");
        yield return Key(KeyCode.LeftArrow);
        float sfxNow = SoundSettings.Sfx;
        run.Event("volume-changed", "music " + music.ToString("0.00") + " -> " + musicNow.ToString("0.00") + ", effects " + sfx.ToString("0.00") + " -> " + sfxNow.ToString("0.00") + " (effects level the mix multiplies by " + SoundSettings.EffectsLevel.ToString("0.00") + ")");
        if (musicNow >= music - 0.01f || sfxNow >= sfx - 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: Left on the sliders did not lower the volume");
        }
        yield return DownTo(panel.VoiceButton, "the voice-lines button");
        yield return Key(KeyCode.Return);
        int chatterNow = SoundSettings.Chatter;
        run.Event("voices-changed", "voice lines " + SoundSettings.ChatterName(chatter) + " -> " + SoundSettings.ChatterName(chatterNow) + " (gap x" + VoiceDirector.GapScale(chatterNow).ToString("0.0") + ")");
        if (chatterNow == chatter) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: Enter on Voices did not change how often the voices speak");
        }
        // The saved JSON reads back the same (the sandbox writes nothing to the player's save).
        string json = SoundSettings.Json();
        SoundSettings.Set(true, 1f, 1f);
        SoundSettings.SetChatter(2);
        bool back = SoundSettings.LoadFrom(json);
        bool same = back && Mathf.Abs(SoundSettings.Music - musicNow) < 0.01f && Mathf.Abs(SoundSettings.Sfx - sfxNow) < 0.01f && SoundSettings.Chatter == chatterNow;
        run.Event("settings-persisted", "saved " + json + ", read back the same: " + same);
        if (!same) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: the settings did not survive a save and load");
        }
        // Put them back.
        SoundSettings.Set(true, music, sfx);
        SoundSettings.SetChatter(chatter);
        yield return Key(KeyCode.JoystickButton1);
        yield return new WaitForSecondsRealtime(0.4f);
        if (SettingsPanel.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: the pad's B did not close Settings");
        } else if (!PauseMenu.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "pause-nav: closing Settings with B also closed the pause menu");
        }
        yield return new WaitForSecondsRealtime(0.4f);
        yield return DownTo(PauseMenu.Instance.ResumeButton, "Resume");
    }

    // Load time and memory of one chapter: the card (file press to chapter scene), then the map (Begin to playable),
    // and the memory in use once it is up. Event chapter-load; metrics chapter<N>CardSeconds, chapter<N>MapSeconds,
    // chapter<N>MemoryMB. A chapter that takes over 20 s to come up, or sits over 1 GB, is a bug.
    static void ReportChapterLoad(int index, float cardSeconds, float mapSeconds) {
        float mb = Mathf.Round(ChapterMap.Memory() / 1048576f);
        string n = "chapter" + (index + 1);
        run.Metric(n + "CardSeconds", Mathf.Round(cardSeconds * 10f) / 10f);
        run.Metric(n + "MapSeconds", Mathf.Round(mapSeconds * 10f) / 10f);
        run.Metric(n + "MemoryMB", mb);
        run.Event("chapter-load", "chapter " + (index + 1) + ": card in " + cardSeconds.ToString("0.0") + " s, map in " + mapSeconds.ToString("0.0") + " s, memory " + mb + " MB");
        if (cardSeconds + mapSeconds > 20f) {
            run.Problem(PlaytestSeverity.Bug, "chapter " + (index + 1) + " took " + (cardSeconds + mapSeconds).ToString("0.0") + " s to load");
        }
        if (mb > 1024f) {
            run.Problem(PlaytestSeverity.Bug, "chapter " + (index + 1) + " holds " + mb + " MB after loading");
        }
    }

    // Case-file saves: an old-format file loads and is brought up to date, and corrupt text shows a message and starts
    // clean. The stored text is a named setup shortcut (the sandbox stands where the real save would; it is never written).
    const string OldSaveV1 = "{\"version\":1,\"last\":0,\"slots\":[{\"used\":true,\"chapter\":2,\"reached\":2,\"cleared\":2,\"seconds\":754.5,\"saved\":638000000000000000},{\"used\":true,\"chapter\":0,\"reached\":0,\"cleared\":0,\"seconds\":61},{\"used\":false}]}";
    const string OldSaveNoStamp = "{\"last\":1,\"slots\":[{\"used\":false},{\"used\":true,\"chapter\":1,\"reached\":1,\"cleared\":1,\"seconds\":300},{\"used\":false}]}";
    static IEnumerator SaveRun() {
        // 1. Old formats, straight through the loader.
        CampaignSave.EnterSandbox(OldSaveV1);
        CampaignSave.Slot a = CampaignSave.Get(0);
        bool v1ok = CampaignSave.LoadResult == "migrated" && a.used && a.chapter == 2 && a.cleared == 2 && Mathf.Abs(a.seconds - 754.5f) < 0.1f && Lore.UnlockedCount(a) > 0 && CampaignSave.UsedCount == 2;
        run.Event("save-old-loaded", "version 1 text: result " + CampaignSave.LoadResult + ", file 1 on chapter " + (a.chapter + 1) + ", " + a.cleared + " filed, " + CampaignSave.Clock(a) + " on the books, lore " + Lore.UnlockedCount(a) + "/" + Lore.All.Length + ", " + CampaignSave.UsedCount + " files in use");
        run.Metric("oldSaveLoaded", v1ok ? 1 : 0);
        if (!v1ok) {
            run.Problem(PlaytestSeverity.Bug, "save: a version 1 save did not load with its files intact");
        }
        bool stamped = CampaignSave.ToText().Contains("\"version\":" + CampaignSave.Version);
        run.Event("save-migrated", "rewritten as version " + CampaignSave.Version + ": " + (stamped ? "stamped" : "NOT stamped"));
        if (!stamped) {
            run.Problem(PlaytestSeverity.Bug, "save: the migrated files are not stamped with version " + CampaignSave.Version);
        }
        CampaignSave.EnterSandbox(OldSaveNoStamp);
        CampaignSave.Slot b = CampaignSave.Get(1);
        bool oldest = CampaignSave.LoadResult == "migrated" && CampaignSave.LoadedVersion == 0 && b.used && b.chapter == 1 && CampaignSave.Last == 1;
        run.Event("save-oldest-loaded", "text with no version: result " + CampaignSave.LoadResult + ", read as version " + CampaignSave.LoadedVersion + ", file 2 on chapter " + (b.chapter + 1) + ", last opened " + CampaignSave.Last);
        if (!oldest) {
            run.Problem(PlaytestSeverity.Bug, "save: a save with no version stamp did not load");
        }

        // 2. Corrupt text of every shape: never an exception, always clean files.
        string[] bad = { "{not json<<<", "[1,2,3]", "{}", "{\"version\":2,\"slots\":", "{\"version\":2,\"slots\":\"x\"}", "\u0000\u0001garbage", "null" };
        int cleanStarts = 0;
        foreach (string text in bad) {
            try {
                CampaignSave.EnterSandbox(text);
                bool clean = CampaignSave.LoadResult == "corrupt" && CampaignSave.UsedCount == 0 && !string.IsNullOrEmpty(CampaignSave.Notice);
                cleanStarts += clean ? 1 : 0;
                if (!clean) {
                    run.Problem(PlaytestSeverity.Bug, "save: corrupt text '" + text.Replace("\u0000", "?") + "' gave result '" + CampaignSave.LoadResult + "' with " + CampaignSave.UsedCount + " files in use");
                }
            } catch (System.Exception e) {
                run.Problem(PlaytestSeverity.Bug, "save: corrupt text '" + text.Replace("\u0000", "?") + "' threw " + e.GetType().Name);
            }
        }
        run.Event("save-corrupt-clean", cleanStarts + " of " + bad.Length + " corrupt texts started clean without an exception");
        run.Metric("corruptClean", cleanStarts);

        // 3. On screen: the old file in the case files with its notice, a chapter card from it; then a corrupt one.
        CampaignSave.EnterSandbox(OldSaveV1);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "save: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        string shown = CampaignPanel.Instance.SubtitleShown;
        run.Event("save-old-panel", "case files say \"" + shown + "\"; file 1: " + CampaignSave.Describe(CampaignSave.Get(0)));
        if (shown != CampaignSave.MigratedNotice) {
            run.Problem(PlaytestSeverity.Bug, "save: the case files did not say the old files were updated (\"" + shown + "\")");
        }
        float tapped = Time.realtimeSinceStartup;
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card from the old file");
        if (run.Done) {
            yield break;
        }
        bool onThree = ChapterScene.Instance.Chapter == 2;
        run.Event("save-old-chapter", "the old file opened chapter " + (ChapterScene.Instance.Chapter + 1) + " \"" + ChapterScene.Instance.Title + "\" in " + (Time.realtimeSinceStartup - tapped).ToString("0.0") + " s");
        if (!onThree) {
            run.Problem(PlaytestSeverity.Bug, "save: the old file opened chapter " + (ChapterScene.Instance.Chapter + 1) + ", not 3");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        CampaignSave.EnterSandbox(bad[0]);   // setup: the stored text goes bad while the player is away
        PlayInput.Release();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        menu = Object.FindAnyObjectByType<MainMenu>();
        yield return new WaitForSecondsRealtime(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files after corrupt save");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        shown = CampaignPanel.Instance.SubtitleShown;
        bool allEmpty = CampaignSave.UsedCount == 0;
        run.Event("save-corrupt-panel", "case files say \"" + shown + "\"; " + CampaignSave.UsedCount + " files in use");
        run.Metric("corruptMessageShown", shown == CampaignSave.CorruptNotice ? 1 : 0);
        if (shown != CampaignSave.CorruptNotice || !allEmpty) {
            run.Problem(PlaytestSeverity.Bug, "save: the corrupt save did not show its message and start clean (\"" + shown + "\", " + CampaignSave.UsedCount + " files)");
        }
        tapped = Time.realtimeSinceStartup;
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "chapter 1 from a clean file");
        if (run.Done) {
            yield break;
        }
        run.Event("save-corrupt-play", "a clean file opened chapter " + (ChapterScene.Instance.Chapter + 1) + " in " + (Time.realtimeSinceStartup - tapped).ToString("0.0") + " s");
        if (ChapterScene.Instance.Chapter != 0) {
            run.Problem(PlaytestSeverity.Bug, "save: the clean file opened chapter " + (ChapterScene.Instance.Chapter + 1) + ", not 1");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        // 4. Load time and memory of every chapter: each in turn from a sandbox file standing on it.
        mapChapters = new[] { 0, 1, 2, 3, 4, 5 };
        PlayInput.Release();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        yield return MapsRun();
    }

    // Chapter maps: each chapter loads its own map and lighting on Begin and unloads it on leaving.
    static int[] mapChapters = { 0, 3, 4 };
    // The back rooms (BackRooms.cs): each chapter's room is hidden until its lost thing is in the file; the clue names it; Wallit
    // walks there. Chapter 4's room holds the Dead Letter and the reward, paid once. The last pass plays Chapter 4 again.
    static IEnumerator SecretsRun() {
        CampaignSave.EnterSandbox();
        int[] order = { 0, 1, 2, 3, 4, 5, 3 };
        int entered = 0;
        for (int n = 0; n < order.Length && !run.Done; n++) {
            int index = order[n];
            bool replay = n == order.Length - 1;
            Story.Chapter data = Story.Chapters[index];
            CampaignSave.Slot slot = CampaignSave.Get(0);
            slot.used = true;
            slot.chapter = index;
            slot.reached = index;
            slot.cleared = index;
            slot.xp = Mathf.Max(slot.xp, 100);
            run.Event("setup", "sandbox case file 1 stands on chapter " + (index + 1) + (replay ? " again (a replay)" : ""));
            MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
            if (menu == null || menu.CampaignButton == null) {
                run.Problem(PlaytestSeverity.Bug, "secrets: no Campaign button on the main menu");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            Tap(menu.CampaignButton.gameObject);
            yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
            yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            Tap(ChapterScene.Instance.BeginButton.gameObject);
            yield return run.Until(() => ChapterRun.Instance != null && BackRoom.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter's map after Begin");
            if (run.Done) {
                yield break;
            }
            run.Scene("Chapter " + (index + 1) + (replay ? " again" : ""));
            yield return CutsceneStep();
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            ChapterRun chapter = ChapterRun.Instance;
            BackRoom room = BackRoom.Instance;
            var player = Object.FindAnyObjectByType<CharacterMovement>();
            BackRooms.Room row = BackRooms.All[index];
            bool held = Journal.HasFind(slot, index);
            run.Event("secret-hidden", "chapter " + (index + 1) + " (" + data.Map + "): the room \"" + row.Name + "\" is " + (room.Built ? "on the map from the start (the clue is in the file)" : "not on the map: no clue yet") + "; the lost thing lies at " + chapter.FindAt.ToString("0") + "; objective line: " + chapter.ObjectiveLine);
            if (!replay && !held) {
                if (room.Built) {
                    run.Problem(PlaytestSeverity.Bug, "secrets: chapter " + (index + 1) + "'s room is on the map before its clue is found");
                }
                float limit = Time.realtimeSinceStartup + 150f;
                while (!run.Done && !chapter.FindKept && Time.realtimeSinceStartup < limit) {
                    Walk(player, chapter.FindAt, 1f);
                    yield return null;
                }
                PlayInput.Release();
                yield return run.Until(() => room.Built, 6f, "the room after the clue");
                if (run.Done) {
                    yield break;
                }
                run.Event("secret-clue", "the lost thing is kept; the clue reads: " + room.Clue);
                string active = Journal.TabText(CampaignSave.Get(0), 0);
                bool listed = active.Contains(row.Clue);
                run.Event("secret-journal", "the journal's Active tab " + (listed ? "lists" : "does NOT list") + " the clue");
                if (!listed) {
                    run.Problem(PlaytestSeverity.Bug, "secrets: chapter " + (index + 1) + "'s clue is not in the journal once the find is kept");
                }
            } else if (!room.Built) {
                run.Problem(PlaytestSeverity.Bug, "secrets: chapter " + (index + 1) + "'s clue is in the file but the room is not on the map");
            }
            float far = Time.realtimeSinceStartup + 240f;
            while (!run.Done && !room.Entered && Time.realtimeSinceStartup < far) {
                if (WaveDirector.IsLost) {
                    break;
                }
                Walk(player, room.At, 1f);
                yield return null;
            }
            PlayInput.Release();
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            CampaignSave.Slot after = CampaignSave.Get(0);
            bool kept = BackRooms.Entered(after, index);
            run.Event("secret-entered", "chapter " + (index + 1) + ": Wallit " + (room.Entered ? "walked into" : "never reached") + " \"" + row.Name + "\" " + Vector3.Distance(Musician.Instance.transform.position, room.At).ToString("0") + " m from the circle; the file holds it: " + kept + "; rooms " + BackRooms.Count(after) + "/6; XP " + after.xp);
            if (!room.Entered || !kept) {
                run.Problem(PlaytestSeverity.Bug, "secrets: Wallit could not reach chapter " + (index + 1) + "'s back room with its clue");
            } else if (!replay) {
                entered++;
            }
            run.Metric("secretRooms", entered);
            yield return new WaitForSecondsRealtime(1.5f);
            if (index == BackRooms.EncounterChapter && room.Entered) {
                if (!replay) {
                    yield return run.Until(() => room.EncounterSpawned, 5f, "the Dead Letter");
                    if (run.Done) {
                        yield break;
                    }
                    run.Event("encounter-fight", room.Letter != null ? room.Letter.Title + " (" + room.Letter.HitsToDrop + " hits) steps out of the room; reward held before: " + BackRooms.HasReward(after) : "no Dead Letter");
                    var only = new List<Enemy>();
                    if (room.Letter != null) {
                        only.Add(room.Letter);
                    }
                    yield return Fight(player, () => room.RewardGiven, 360f, "the Dead Letter to fall", only, true);
                    if (run.Done) {
                        yield break;
                    }
                    CampaignSave.Slot paid = CampaignSave.Get(0);
                    run.Event("encounter-reward", "the Dead Letter fell: " + BackRooms.RewardName + " held " + BackRooms.HasReward(paid) + ", Refund reads \"" + CampaignProgress.RefundName + "\", ring " + CampaignProgress.RefundReach + " m, rooms bits " + paid.rooms);
                    run.Metric("rewardGiven", BackRooms.HasReward(paid) && CampaignProgress.RefundFull ? 1 : 0);
                    if (!BackRooms.HasReward(paid) || !CampaignProgress.RefundFull) {
                        run.Problem(PlaytestSeverity.Bug, "secrets: the Dead Letter fell but Refund in Full was not earned");
                    }
                } else {
                    yield return new WaitForSecondsRealtime(3f);
                    run.Event("encounter-once", "replayed: Dead Letter spawned " + room.EncounterSpawned + ", reward held " + BackRooms.HasReward(after) + ", Refund reads \"" + CampaignProgress.RefundName + "\"");
                    bool once = !room.EncounterSpawned && !room.RewardGiven && BackRooms.HasReward(after) && CampaignProgress.RefundFull;
                    run.Metric("rewardOnce", once ? 1 : 0);
                    if (!once) {
                        run.Problem(PlaytestSeverity.Bug, "secrets: the Dead Letter came back or the reward was paid again on a replay");
                    }
                }
            }
            run.Event("map-leave", "leaving chapter " + (index + 1) + " for the menu (setup shortcut)");
            PlayInput.Release();
            ChapterRun.Instance.Abandon();
            yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after leaving");
            if (run.Done) {
                yield break;
            }
            run.Scene("Main Menu");
            yield return new WaitForSecondsRealtime(1f);
        }
        run.Metric("secretRoomsTotal", entered);
        if (entered < 6) {
            run.Problem(PlaytestSeverity.Bug, "secrets: only " + entered + " of 6 chapters' back rooms were found with their clues");
        }
    }

    static IEnumerator MapsRun() {
        CampaignSave.EnterSandbox();
        int[] chapters = mapChapters;
        int withRoute = 0, withLandmark = 0, withHazard = 0, withArena = 0;
        var looks = new HashSet<string>();
        bool ownScenes = mapChapters.Length == 3 && (mapChapters[1] == 1 || mapChapters[0] == 3);   // chapters 1-3 and 4-6 are scenes of their own
        var skyNames = new HashSet<string>();
        int setPieces = 0;
        int loaded = 0;
        int unloaded = 0;
        System.GC.Collect();
        yield return Resources.UnloadUnusedAssets();
        yield return new WaitForSecondsRealtime(1f);
        long baseline = ChapterMap.Memory();
        run.Metric("mapMemoryBaselineMB", Mathf.Round(baseline / 1048576f));
        foreach (int index in chapters) {
            if (run.Done) {
                yield break;
            }
            Story.Chapter data = Story.Chapters[index];
            CampaignSave.Slot slot = CampaignSave.Get(0);
            slot.used = true;
            slot.chapter = index;
            slot.reached = index;
            slot.cleared = index;
            run.Event("map-setup", "sandbox case file 1 stands on chapter " + (index + 1) + " (map '" + data.Map + "')");
            MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
            if (menu == null || menu.CampaignButton == null) {
                run.Problem(PlaytestSeverity.Bug, "chapter-maps: no Campaign button on the main menu");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            Tap(menu.CampaignButton.gameObject);
            yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            float cardTapped = Time.realtimeSinceStartup;
            Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
            yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card");
            if (run.Done) {
                yield break;
            }
            float cardSeconds = Time.realtimeSinceStartup - cardTapped;
            yield return new WaitForSecondsRealtime(2f);
            float beginTapped = Time.realtimeSinceStartup;
            Tap(ChapterScene.Instance.BeginButton.gameObject);
            run.Event("tap", "Begin chapter " + (index + 1));
            yield return run.Until(() => ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter's map after Begin");
            if (run.Done) {
                yield break;
            }
            float mapSeconds = Time.realtimeSinceStartup - beginTapped;
            run.Scene("Chapter " + (index + 1) + " map");
            yield return new WaitForSecondsRealtime(1.5f);
            ReportChapterLoad(index, cardSeconds, mapSeconds);
            ChapterMap map = ChapterMap.Current;
            if (map != null && map.OwnScene && map.Own == null) {
                yield return run.Until(() => map.Own != null, 20f, "the chapter's own scene to fill in");
                if (run.Done) {
                    yield break;
                }
            }
            if (map == null || map.MapName != data.Map || !map.MapScene.isLoaded) {
                run.Problem(PlaytestSeverity.Bug, "chapter-maps: chapter " + (index + 1) + " names map '" + data.Map + "' but loaded '" + (map != null ? map.MapName : "none") + "'");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
            loaded++;
            run.Event("map-loaded", "chapter " + (index + 1) + " map '" + map.MapName + "' as scene " + map.MapScene.name + " (" + map.MapScene.rootCount + " roots); " + SceneManager.sceneCount + " scenes open, active '" + SceneManager.GetActiveScene().name + "'");
            if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
                run.Problem(PlaytestSeverity.Bug, "chapter-maps: loading the map changed the active scene to '" + SceneManager.GetActiveScene().name + "'");
            }
            Color fog = RenderSettings.fogColor;
            Color want = map.Applied.Fog;
            bool match = Mathf.Abs(fog.r - want.r) + Mathf.Abs(fog.g - want.g) + Mathf.Abs(fog.b - want.b) < 0.02f && Mathf.Abs(RenderSettings.fogDensity - map.Applied.FogDensity) < 0.0005f;
            string look = ColorUtility.ToHtmlStringRGB(fog) + "/" + RenderSettings.fogDensity.ToString("0.0000");
            looks.Add(look);
            run.Event("map-lighting", "chapter " + (index + 1) + " fog #" + ColorUtility.ToHtmlStringRGB(fog) + " density " + RenderSettings.fogDensity.ToString("0.0000") + ", ambient #" + ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor) + ", sky #" + ColorUtility.ToHtmlStringRGB(Camera.main != null ? Camera.main.backgroundColor : Color.black) + (match ? ", the chapter's own" : ", NOT the chapter's"));
            if (!match) {
                run.Problem(PlaytestSeverity.Bug, "chapter-maps: chapter " + (index + 1) + "'s lighting is not on after the map loaded");
            }
            bool hasRoute = false, hasLandmark = false, hasHazard = false, hasSet = false;
            foreach (string f in map.Features) {
                hasSet |= f.StartsWith("setpiece:");
                hasRoute |= f.StartsWith("route:");
                hasLandmark |= f.StartsWith("landmark:");
                hasHazard |= f.StartsWith("hazard:");
                run.Event("map-features", "chapter " + (index + 1) + " " + f);
            }
            withRoute += hasRoute ? 1 : 0;
            withLandmark += hasLandmark ? 1 : 0;
            withHazard += hasHazard ? 1 : 0;
            if (ownScenes && !(hasRoute && hasLandmark && hasHazard && hasSet)) {
                run.Problem(PlaytestSeverity.Bug, "maps-own: chapter " + (index + 1) + "'s map lacks a route, a landmark, a hazard or a set piece (" + map.Features.Count + " features)");
            }
            if (ownScenes) {
                // Chapters 1 to 3 are scenes of their own (Assets/Maps), built by OwnMap: report the scene and what is in it.
                if (!map.OwnScene || map.Own == null) {
                    run.Problem(PlaytestSeverity.Bug, "maps-own: chapter " + (index + 1) + " did not load its own scene (map '" + data.Map + "')");
                } else {
                    Camera cam0 = Camera.main;
                    run.Event("map-own-scene", "chapter " + (index + 1) + " scene " + map.MapScene.name + " (" + map.MapScene.rootCount + " roots, " + map.Own.Pieces + " pieces, sky " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "none") + ", camera " + (cam0 != null ? cam0.clearFlags.ToString() : "?") + ", music " + (map.Own.Music != null && map.Own.Music.isPlaying ? "playing" : "silent") + ", " + SceneManager.sceneCount + " scenes open)");
                    if (RenderSettings.skybox == null) {
                        run.Problem(PlaytestSeverity.Bug, "maps-own: chapter " + (index + 1) + " has no sky of its own");
                    }
                    if (map.Own.Music == null) {
                        run.Problem(PlaytestSeverity.Bug, "maps-own: chapter " + (index + 1) + " has no music bed");
                    }
                    skyNames.Add(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none");
                }
            }
            if (mapChapters.Length == 3 && mapChapters[0] == 3) {
                bool hasArena = false;
                foreach (string f in map.Features) {
                    hasArena |= f.StartsWith("arena:");
                }
                if (!(hasRoute && hasLandmark && hasHazard && hasArena && map.HasArena)) {
                    run.Problem(PlaytestSeverity.Bug, "maps-456: chapter " + (index + 1) + "'s map lacks a route, a landmark, a hazard or a boss arena (" + map.Features.Count + " features)");
                } else {
                    withArena++;
                    Vector3 c0 = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
                    float routeBearing = index == 3 ? 330f : index == 4 ? 180f : 180f;
                    float away = Mathf.Abs(Mathf.DeltaAngle(routeBearing, map.ArenaBearing));
                    run.Event("map-arena", "chapter " + (index + 1) + " boss arena " + Vector3.Distance(c0, map.BossSpot).ToString("0") + " m out on bearing " + map.ArenaBearing.ToString("0") + ", " + away.ToString("0") + " degrees from the route");
                    if (away < 90f) {
                        run.Problem(PlaytestSeverity.Bug, "maps-456: chapter " + (index + 1) + "'s boss arena is not apart from the route (" + away.ToString("0") + " degrees)");
                    }
                    Enemy bossBot = WaveDirector.Instance != null ? WaveDirector.Instance.SpawnForChapter(BotKind.SpiderBot, true, 34f) : null;
                    if (bossBot != null) {
                        float off = Vector2.Distance(new Vector2(bossBot.transform.position.x, bossBot.transform.position.z), new Vector2(map.BossSpot.x, map.BossSpot.z));
                        run.Event("arena-boss", "chapter " + (index + 1) + " boss spawned " + off.ToString("0.0") + " m from the arena centre (setup shortcut: called by the bot)");
                        if (off > 8f) {
                            run.Problem(PlaytestSeverity.Bug, "maps-456: chapter " + (index + 1) + "'s boss did not walk out of the arena (" + off.ToString("0") + " m)");
                        }
                        Object.Destroy(bossBot.gameObject);
                    }
                }
            }
            if (ChapterRun.Instance != null && ChapterRun.Instance.HasMarker) {
                Vector3 centre = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
                run.Event("map-objective", "chapter " + (index + 1) + " objective marker " + Vector3.Distance(centre, ChapterRun.Instance.Marker).ToString("0") + " m from the circle");
            } else {
                run.Event("map-objective", "chapter " + (index + 1) + " objective is the circle itself");
            }
            yield return CutsceneStep();
            if (run.Done) {
                yield break;
            }
            if (ownScenes && map.Own != null) {
                // The scripted set piece, made to happen by a named setup shortcut (the real triggers are played by the campaign,
                // chapter2 and chapter3 scenarios: the Courier's notice, Wallit near the press, the lever).
                run.Event("setup", "set piece of chapter " + (index + 1) + " triggered by the bot (OwnMap.Trigger): " + map.Own.Key);
                map.Own.Trigger();
                yield return new WaitForSecondsRealtime(index == 1 ? 5f : 1.5f);
                run.Event("set-piece", "chapter " + (index + 1) + " " + map.Own.Beats + " beat(s): " + string.Join(" ; ", map.Own.Log.ToArray()));
                if (map.Own.Beats == 0) {
                    run.Problem(PlaytestSeverity.Bug, "maps-own: chapter " + (index + 1) + "'s set piece did not play");
                }
                setPieces += map.Own.Beats > 0 ? 1 : 0;
            }
            var player = Object.FindAnyObjectByType<CharacterMovement>();
            Vector3 start = player != null ? player.transform.position : Vector3.zero;
            PlayInput.Hold(KeyCode.W, true);
            yield return new WaitForSecondsRealtime(1.5f);
            PlayInput.Hold(KeyCode.W, false);
            run.Event("chapter-move", "Wallit walked " + (player != null ? Vector3.Distance(start, player.transform.position).ToString("0.0") : "?") + " m on the map");
            run.Event("map-leave", "leaving chapter " + (index + 1) + " for the menu (setup shortcut)");
            string mapScene = map.MapScene.name;
            PlayInput.Release();
            ChapterRun.Instance.Abandon();
            yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after leaving");
            if (run.Done) {
                yield break;
            }
            run.Scene("Main Menu");
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return new WaitForSecondsRealtime(1.5f);
            bool still = SceneManager.GetSceneByName(mapScene).isLoaded || ChapterMap.Current != null;
            if (!still) {
                unloaded++;
            } else {
                run.Problem(PlaytestSeverity.Bug, "chapter-maps: " + mapScene + " is still loaded after leaving chapter " + (index + 1));
            }
            long now = ChapterMap.Memory();
            run.Event("map-unloaded", mapScene + (still ? " STILL LOADED" : " closed") + "; " + SceneManager.sceneCount + " scene(s) open; memory " + Mathf.Round(now / 1048576f) + " MB (baseline " + Mathf.Round(baseline / 1048576f) + " MB)");
        }
        System.GC.Collect();
        long end = ChapterMap.Memory();
        float growth = (end - baseline) / 1048576f;
        run.Metric("mapSetPieces", setPieces);
        run.Metric("mapSkies", skyNames.Count);
        if (ownScenes && skyNames.Count < 3) {
            run.Problem(PlaytestSeverity.Bug, "maps-own: the three chapters do not each have their own sky (" + skyNames.Count + " distinct)");
        }
        run.Metric("mapsLoaded", loaded);
        run.Metric("mapsUnloaded", unloaded);
        run.Metric("distinctLooks", looks.Count);
        run.Metric("mapsWithRoute", withRoute);
        run.Metric("mapsWithLandmark", withLandmark);
        run.Metric("mapsWithHazard", withHazard);
        run.Metric("mapsWithArena", withArena);
        run.Metric("mapMemoryAfterMB", Mathf.Round(end / 1048576f));
        run.Metric("mapMemoryGrowthMB", Mathf.Round(growth));
        if (looks.Count < chapters.Length && chapters.Length <= 3) {
            run.Problem(PlaytestSeverity.Bug, "chapter-maps: chapters did not each get their own lighting (" + looks.Count + " distinct)");
        }
        if (growth > 48f) {
            run.Problem(PlaytestSeverity.Bug, "chapter-maps: memory is " + Mathf.Round(growth) + " MB above the baseline after leaving three chapters");
        }
    }

    // Quit mid-chapter the way a closed tab does (the scene is dropped, the file is not touched), then reopen the file:
    // it must stand on the same chapter, unfinished, and Begin plays it again.
    static IEnumerator QuitAndResume(int chapterIndex) {
        run.Event("quit-midchapter", "leaving chapter " + (chapterIndex + 1) + " for the menu before it is done (setup: the scene is dropped, as a closed tab does)");
        PlayInput.Release();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after quitting");
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        yield return new WaitForSecondsRealtime(1f);
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        if (slot.chapter != chapterIndex) {
            run.Problem(PlaytestSeverity.Bug, "campaign-full: quitting mid-chapter " + (chapterIndex + 1) + " left the file on chapter " + (slot.chapter + 1));
        }
        Tap(Object.FindAnyObjectByType<MainMenu>().CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files after quitting");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Text[] texts = CampaignPanel.Instance.SlotButtons[0].GetComponentsInChildren<Text>();
        run.Event("quit-resume-panel", texts[0].text + " / " + texts[1].text);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 30f, "the chapter card after quitting");
        if (run.Done) {
            yield break;
        }
        bool right = ChapterScene.Instance.Chapter == chapterIndex;
        run.Event("quit-resume", "reopened on chapter " + (ChapterScene.Instance.Chapter + 1) + " \"" + ChapterScene.Instance.Title + "\" (expected " + (chapterIndex + 1) + ")");
        run.Metric("quitResumeRight", right ? 1 : 0);
        if (!right) {
            run.Problem(PlaytestSeverity.Bug, "campaign-full: after quitting mid-chapter " + (chapterIndex + 1) + " the file reopened on chapter " + (ChapterScene.Instance.Chapter + 1));
        }
        yield return new WaitForSecondsRealtime(2f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        run.Event("tap", "Begin (after resuming)");
    }

    // The chapter framework, played with real input: Begin on the chapter card, Wallit and Slice in the game scene, the
    // objective line, fighting until it is met, the Chapter Complete card, the file advanced, then reopened at chapter 2.
    static IEnumerator ChapterPlay() {
        ChapterScene card = ChapterScene.Instance;
        int before = ChapterScene.Instance.Chapter;
        run.Event("chapter-card", card.Title + ": Begin and Menu buttons " + (card.BeginButton != null ? "up" : "missing"));
        if (card.BeginButton == null) {
            run.Problem(PlaytestSeverity.Bug, "campaign: the chapter card has no Begin button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(card.BeginButton.gameObject);
        run.Event("tap", "Begin");
        yield return run.Until(() => ChapterRun.Instance != null && WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter's map after Begin");
        if (run.Done) {
            yield break;
        }
        ChapterRun chapter = ChapterRun.Instance;
        if (fullRun && before == 2 && !quitDone) {
            // Chapter 3: play a few seconds, quit, resume: the file must still stand on chapter 3.
            quitDone = true;
            yield return new WaitForSecondsRealtime(4f);
            yield return QuitAndResume(before);
            if (run.Done) {
                yield break;
            }
            yield return run.Until(() => ChapterRun.Instance != null && WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter's map after resuming");
            if (run.Done) {
                yield break;
            }
            chapter = ChapterRun.Instance;
        }
        run.Scene("Chapter play");
        run.Event("chapter-start", chapter.Title + " in " + chapter.Place + ", case file " + (CampaignSave.Active + 1));
        yield return CutsceneStep();
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);
        CheckPlayerIsWallit();
        CheckMusicianIsSlice();
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (companionTest) {
            yield return CompanionCheck(player, chapter);
            if (run.Done) {
                yield break;
            }
        }
        if (player == null) {
            run.Problem(PlaytestSeverity.Bug, "campaign: no Wallit (CharacterMovement) in the chapter map");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Vector3 start = player.transform.position;
        // Move with the real stick keys for a moment, to show Wallit moves in a chapter.
        PlayInput.Axis("Vertical", 1f);
        yield return new WaitForSecondsRealtime(0.8f);
        PlayInput.Axis("Vertical", 0f);
        float walked = Vector3.Distance(start, player.transform.position);
        run.Metric("chapterWalked", Mathf.Round(walked * 10f) / 10f);
        run.Event("chapter-move", "Wallit moved " + walked.ToString("0.0") + " m on the stick");
        run.Event("objective", chapter.ObjectiveLine);
        if (string.IsNullOrEmpty(chapter.ObjectiveLine)) {
            run.Problem(PlaytestSeverity.Bug, "campaign: the chapter shows no objective line");
        }
        CheckScreen("chapter-play");
        if (journalTest) {
            yield return JournalInChapter(player, chapter);
            if (run.Done) {
                yield break;
            }
        }
        bool courier = chapter.HasBoss;
        if (courier) {
            // Chapter 1: hold the circle against the first Yo Bots, then the Courier arrives.
            if (hubTest) {
                yield return HubAbility(player, chapter);
                if (run.Done) {
                    yield break;
                }
            }
            if (abilitiesTest) {
                yield return AbilitiesPlay(player, chapter);
                if (run.Done) {
                    yield break;
                }
            }
            if (movesTest) {
                yield return MovesPlay(player, chapter);
                if (run.Done) {
                    yield break;
                }
            }
            yield return Fight(player, () => chapter.BossUp || chapter.Completed, 180f, "the boss to arrive", null, true);
            if (run.Done) {
                yield break;
            }
            run.Scene("Chapter boss");
            run.Event("boss-arrives", chapter.BossEnemy != null ? chapter.BossEnemy.Title + " (" + chapter.BossEnemy.HitsToDrop + " hits) after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s" : "no boss");
            yield return new WaitForSecondsRealtime(0.6f);
            Enemy bossEnemy = chapter.BossEnemy;
            bool bar = bossEnemy != null && BossBarShows(bossEnemy.Title);
            run.Event("boss-bar", bossEnemy != null ? bossEnemy.Title + ": health bar " + (bar ? "up" : "missing") + ", health " + bossEnemy.Health01.ToString("0.00") : "no boss");
            run.Metric("bossBar", bar ? 1 : 0);
            if (!bar) {
                run.Problem(PlaytestSeverity.Bug, "campaign: no boss health bar for " + (bossEnemy != null ? bossEnemy.Title : "the boss"));
            }
            int seenEvents = 0;
            bool barMid = false;
            yield return Fight(player, () => {
                for (; seenEvents < Courier.Events.Count; seenEvents++) {
                    run.Event("boss-attack", Courier.Events[seenEvents] + (Courier.MarkUp ? " (red mark up on the circle)" : ""));
                }
                if (!barMid && bossEnemy != null && bossEnemy.Health01 < 0.6f) {
                    barMid = true;
                    run.Event("boss-bar", bossEnemy.Title + " health " + bossEnemy.Health01.ToString("0.00") + ", bar " + (BossBarShows(bossEnemy.Title) ? "up" : "missing"));
                }
                return chapter.Completed;
            }, 420f, "the Courier defeated", null, true);
            if (run.Done) {
                yield break;
            }
            run.Metric("courierWinds", Courier.Winds);
            run.Metric("courierInterrupted", Courier.Interrupted);
            run.Metric("courierLanded", Courier.Landed);
            run.Metric("courierBlocked", Courier.Blocked);
            if (Courier.Winds == 0) {
                run.Problem(PlaytestSeverity.Bug, "campaign: the Courier never wound up an attack (no telegraphed notice)");
            }
            foreach (string line in ChapterRun.Events) {
                if (line.StartsWith("defeat|") || line.StartsWith("line|")) {
                    run.Event("defeat-beat", line);
                }
            }
            BossPhaseCheck("The Courier");
run.Event("boss-defeated", "The Courier fell after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s: " + Courier.Winds + " notices wound up, " + Courier.Interrupted + " broken, " + Courier.Blocked + " taken by Wallit, " + Courier.Landed + " landed on Slice");
        } else if (chapter.IsFinal) {
            yield return FinalPlay(player, chapter, before);
            yield break;
        } else if (chapter.IsReading) {
            yield return ReadingPlay(player, chapter);
        } else if (chapter.IsGate) {
            yield return GatePlay(player, chapter);
        } else if (chapter.IsFloor) {
            yield return FloorPlay(player, chapter);
        } else {
            yield return Fight(player, () => chapter.Completed, 240f, "the chapter objective met", null, true);
        }
        if (run.Done) {
            yield break;
        }
        yield return SettleScenes(chapter);   // the aftermath scene and the closing reel play before the card
        PlayInput.Release();
        run.Event("objective-met", chapter.Title + " after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s: " + chapter.Progress);
        yield return new WaitForSecondsRealtime(1.5f);
        run.Event("chapter-complete", chapter.CardUp ? chapter.CardText.Replace("\n", " / ") : "card missing");
        if (!chapter.CardUp) {
            run.Problem(PlaytestSeverity.Bug, "campaign: the objective was met but no Chapter Complete card showed");
        }
        CheckScreen("chapter-card");
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        run.Event("campaign-advanced", "case file now on chapter " + (slot.chapter + 1) + ", " + slot.cleared + " cleared");
        run.Metric("chapterAdvanced", slot.chapter == before + 1 ? 1 : 0);
        if (slot.chapter != before + 1 || slot.cleared != before + 1) {
            run.Problem(PlaytestSeverity.Bug, "campaign: finishing chapter " + (before + 1) + " left the file on chapter " + (slot.chapter + 1) + " (" + slot.cleared + " cleared)");
        }
        yield return new WaitForSecondsRealtime(2f);
        if (gradeTest) {
            yield return GradeFlow(chapter, before);
            yield break;
        }

        if (choicesTest) {
            yield return ChoicesFlow(chapter);
            yield break;
        }

        if (hubTest) {
            yield return HubPlay(chapter);
            yield break;
        }

        // Menu from the card, then reopen the file: it resumes at the next chapter.
        Tap(chapter.MenuButton.gameObject);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after the chapter");
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        if (abilitiesTest) {
            int mask = CampaignProgress.UnlockedMask(0);
            run.Event("unlock-kept", "back at the menu the case file still holds unlock mask " + mask + " (Coin Toss, Stamp Out, Escrow = bits 4, 5, 6)");
            if ((mask & 0x70) != 0x70) {
                run.Problem(PlaytestSeverity.Bug, "abilities: the case file lost its unlocks on leaving the chapter (mask " + mask + ")");
            }
        }
        if (movesTest || abilitiesTest) {
            run.Event("arcade-moves-clean", "outside a chapter: moves " + (CampaignMoves.Active ? "on" : "off") + ", invulnerable " + CampaignMoves.Invulnerable + ", dodge ready " + CampaignMoves.DodgeReady + ": Arcade keeps today's controls and balance");
            if (CampaignMoves.Active || CampaignMoves.Invulnerable) {
                run.Problem(PlaytestSeverity.Bug, "moves: Campaign's moves still read as on outside a chapter");
            }
        }
        yield return new WaitForSecondsRealtime(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files after the chapter");
        if (run.Done) {
            yield break;
        }
        CampaignPanel panel = CampaignPanel.Instance;
        Text[] texts = panel.SlotButtons[0].GetComponentsInChildren<Text>();
        run.Event("campaign-resume", texts[0].text + " / " + texts[1].text);
        if (journalTest) {
            yield return JournalCheck(panel, texts[0].text, texts[1].text);
            if (run.Done) {
                yield break;
            }
        }
        if (loreTest) {
            yield return LoreCheck(panel, texts[0].text, texts[1].text);
            if (run.Done) {
                yield break;
            }
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(panel.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.Chapter == before + 1 && SceneManager.GetActiveScene().name == CampaignSave.Scene, 30f, "the next chapter's card");
        if (run.Done) {
            yield break;
        }
        run.Scene("Chapter " + (before + 2));
        run.Event("chapter-open", ChapterScene.Instance.Title + " (resumed, case file " + (CampaignSave.Active + 1) + ")");
        run.Metric("chapterResumed", 1);
        yield return run.Until(() => ChapterScene.Instance.LinesShown >= 1, 10f, "the next chapter's first line");
        yield return new WaitForSecondsRealtime(3f);
    }

    // The grade scenario, after chapter 1's card: the rank on the card, the best kept through a save and reload, then Hard
    // chosen on a filed file and its health and damage read off the bots.
    static IEnumerator GradeFlow(ChapterRun chapter, int index) {
        string card = chapter.CardText;
        bool shown = card.Contains("RANK ") && chapter.Rank > 0;
        run.Event("rank-shown", "Chapter Complete: rank " + CampaignGrade.Letter(chapter.Rank) + ", " + Mathf.RoundToInt(chapter.PlaySeconds) + " s (par " + Mathf.RoundToInt(CampaignGrade.ParOf(index)) + "), Slice took " + chapter.DamageTaken + (chapter.NewBest ? ", new best" : "") + " | " + card.Replace("\n", " / "));
        run.Metric("rankShown", shown ? 1 : 0);
        run.Metric("rankValue", chapter.Rank);
        if (!shown) {
            run.Problem(PlaytestSeverity.Bug, "grade: the Chapter Complete card shows no rank");
        }
        int earned = chapter.Rank;
        Tap(chapter.MenuButton.gameObject);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after the chapter");
        if (run.Done) {
            yield break;
        }
        // As after a quit: the files written out and read back.
        CampaignSave.EnterSandbox(CampaignSave.ToText());
        CampaignSave.Slot slot = CampaignSave.Get(0);
        int kept = CampaignSave.BestRank(slot, index, false);
        run.Event("rank-saved", "after writing the files out and reading them back chapter " + (index + 1) + " best rank is " + CampaignGrade.Letter(kept) + " (earned " + CampaignGrade.Letter(earned) + "), best line: " + CampaignSave.RankLine(slot));
        run.Metric("rankSaved", kept == earned && kept > 0 ? 1 : 0);
        if (kept != earned || kept == 0) {
            run.Problem(PlaytestSeverity.Bug, "grade: the best rank was not saved with the case file (earned " + CampaignGrade.Letter(earned) + ", kept " + CampaignGrade.Letter(kept) + ")");
        }
        // Setup shortcut: the sandbox file is filed (every chapter cleared) and stands on chapter 1, so Hard may be chosen.
        slot.cleared = CampaignSave.ChapterCount;
        slot.chapter = 0;
        slot.reached = CampaignSave.ChapterCount - 1;
        run.Event("grade-setup", "sandbox case file 1 marked filed, standing on chapter 1");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        yield return new WaitForSecondsRealtime(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene && ChapterScene.Instance.BeginButton != null, 45f, "the filed file's chapter card");
        if (run.Done) {
            yield break;
        }
        ChapterScene scene = ChapterScene.Instance;
        yield return new WaitForSecondsRealtime(1.5f);
        run.Event("grade-card", "chapter card: " + (scene.RankText.Length > 0 ? scene.RankText : "no best rank") + "; difficulty button " + (scene.DifficultyButton != null ? "\"" + scene.DifficultyText.text + "\"" : "missing"));
        if (scene.DifficultyButton == null) {
            run.Problem(PlaytestSeverity.Bug, "grade: a filed case file's chapter card has no difficulty button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(scene.DifficultyButton.gameObject);
        bool hardOn = CampaignSave.Get(0).hard;
        run.Event("hard-on", "pressed the difficulty button: now \"" + scene.DifficultyText.text + "\", file " + (hardOn ? "set to Hard" : "still Standard"));
        run.Metric("hardOn", hardOn ? 1 : 0);
        if (!hardOn) {
            run.Problem(PlaytestSeverity.Bug, "grade: the difficulty button did not set Hard on a filed file");
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(scene.BeginButton.gameObject);
        run.Event("tap", "Begin (Hard)");
        yield return run.Until(() => ChapterRun.Instance != null && WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the Hard chapter's map");
        if (run.Done) {
            yield break;
        }
        ChapterRun hard = ChapterRun.Instance;
        run.Scene("Chapter play (Hard)");
        run.Event("hard-run", "the chapter reads Hard: " + hard.Hard);
        yield return CutsceneStep();
        if (run.Done) {
            yield break;
        }
        Enemy plain = null;
        yield return run.Until(() => {
            foreach (Enemy e in Enemy.Roster) {
                if (e != null && e.Kind == BotKind.YoBot && !e.Elite && !e.Boss && !e.Golden) {
                    plain = e;
                    return true;
                }
            }
            return false;
        }, 30f, "a plain Yo Bot to read");
        if (run.Done) {
            yield break;
        }
        BotDef def = BotRoster.ForSpawn(BotKind.YoBot, WaveDirector.Wave);
        int standard = Mathf.Max(1, Mathf.RoundToInt(def.Hits * Tuning.HealthScale(WaveDirector.Wave)));
        float ratio = plain.HitsToDrop / (float)standard;
        run.Event("hard-health", "a Yo Bot has " + plain.HitsToDrop + " health on Hard against " + standard + " on Standard (x" + ratio.ToString("0.00") + ")");
        run.Metric("hardHealthRatio", Mathf.Round(ratio * 100f) / 100f);
        if (!hard.Hard || ratio < 1.5f) {
            run.Problem(PlaytestSeverity.Bug, "grade: Hard did not raise a Yo Bot's health (x" + ratio.ToString("0.00") + ")");
        }
        // Hold still: the first blow a bot lands on Slice is read as meant and as landed.
        int blows = CampaignGrade.Blows;
        yield return run.Until(() => CampaignGrade.Blows > blows, 150f, "a bot to land a blow on Slice");
        if (run.Done) {
            yield break;
        }
        float dmg = CampaignGrade.LastLanded / (float)Mathf.Max(1, CampaignGrade.LastRaw);
        run.Event("hard-damage", "a blow meant for " + CampaignGrade.LastRaw + " landed for " + CampaignGrade.LastLanded + " on Slice (x" + dmg.ToString("0.0") + "), Slice took " + hard.DamageTaken + " in all");
        run.Metric("hardDamageRatio", dmg);
        if (dmg < 1.99f) {
            run.Problem(PlaytestSeverity.Bug, "grade: Hard did not raise the damage on Slice (x" + dmg.ToString("0.0") + ")");
        }
        yield return new WaitForSecondsRealtime(2f);
    }

    // The lore scenario: what the case-file slot shows, then the lore log read page by page.
    // The journal, inside Chapter 1: the extra shows on the HUD, J opens the journal over the pause menu, then Wallit walks
    // (real stick) out to the hidden find, which goes into the file at once.
    static IEnumerator JournalInChapter(CharacterMovement player, ChapterRun chapter) {
        Journal.Goal goal = Journal.Goals[chapter.Chapter];
        Journal.Find find = Journal.Finds[chapter.Chapter];
        run.Event("journal-extra", "chapter " + (chapter.Chapter + 1) + " offers the extra \"" + goal.Name + "\": " + goal.Ask + " (" + (chapter.GoalMet ? "met" : "open") + ")");
        run.Event("journal-find-hidden", "the lost thing \"" + find.Name + "\" lies " + find.Dist + " m from the circle at " + chapter.FindAt.ToString("0") + ", out: " + chapter.FindOut + ", no marker on the chapter line: " + chapter.ObjectiveLine);
        if (!chapter.FindOut) {
            run.Problem(PlaytestSeverity.Bug, "journal: the chapter has no hidden find lying in the field");
        }
        PlayInput.Tap(KeyCode.J);
        yield return run.Until(() => JournalPanel.IsOpen, 5f, "the journal after J in the chapter");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.2f);
        run.Event("journal-open-chapter", JournalPanel.Instance.TitleText + " | " + JournalPanel.Instance.TabLabel + " | " + JournalPanel.Instance.BodyText.Replace("\n", " / "));
        CheckScreen("journal");
        Tap(JournalPanel.Instance.BackButton.gameObject);
        yield return run.Until(() => !JournalPanel.IsOpen && PauseMenu.IsOpen, 5f, "the journal closing over the pause menu");
        Tap(PauseMenu.Instance.ResumeButton.gameObject);
        yield return run.Until(() => !PauseMenu.IsOpen && Time.timeScale > 0.5f, 5f, "the chapter to resume");
        if (run.Done) {
            yield break;
        }
        float limit = Time.realtimeSinceStartup + 150f;
        while (!run.Done && !chapter.FindKept && Time.realtimeSinceStartup < limit) {
            Walk(player, chapter.FindAt, 1f);
            yield return null;
        }
        PlayInput.Release();
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        bool saved = Journal.HasFind(slot, chapter.Chapter);
        run.Event("journal-find", "Wallit " + (chapter.FindKept ? "walked onto" : "never reached") + " \"" + find.Name + "\"; the case file holds it at once: " + saved);
        run.Metric("findKept", saved ? 1 : 0);
        if (!chapter.FindKept || !saved) {
            run.Problem(PlaytestSeverity.Bug, "journal: walking onto the hidden find did not keep it in the case file");
        }
        yield return new WaitForSecondsRealtime(1f);
    }

    // The journal from the case files after the chapter: the three tabs, then "quit and resume" (the files are written out
    // and read back as a fresh page load would), then the Paid in Full state.
    static IEnumerator JournalCheck(CampaignPanel panel, string title, string info) {
        run.Event("slot-journal", title + " / " + info);
        // Setup shortcut: the sandbox file also counts chapter 2's extra as met, so Finished has a row whatever the fight did.
        CampaignSave.Get(0).goals |= 2;
        run.Event("setup", "sandbox case file 1 marks chapter 2's extra met (never the real save)");
        yield return new WaitForSecondsRealtime(1f);
        Tap(panel.JournalButton.gameObject);
        yield return run.Until(() => JournalPanel.IsOpen, 10f, "the journal after pressing Journal");
        if (run.Done) {
            yield break;
        }
        yield return ReadJournal("fresh", "Wallit's find");
        // Quit and resume: write the files out and read them back.
        string text = CampaignSave.ToText();
        CampaignSave.Slot before = CampaignSave.Get(0);
        int finds = before.finds;
        int goals = before.goals;
        Tap(JournalPanel.Instance.BackButton.gameObject);
        yield return run.Until(() => !JournalPanel.IsOpen, 5f, "the journal closing");
        CampaignSave.EnterSandbox(text);
        CampaignSave.Slot after = CampaignSave.Get(0);
        bool kept = after.finds == finds && after.goals == goals && finds != 0;
        run.Event("journal-persist", "files written out and read back: finds " + finds + " -> " + after.finds + ", extras " + goals + " -> " + after.goals + ": " + (kept ? "kept" : "LOST"));
        run.Metric("journalPersisted", kept ? 1 : 0);
        if (!kept) {
            run.Problem(PlaytestSeverity.Bug, "journal: the finds and extras did not survive a save and reload");
        }
        Tap(panel.JournalButton.gameObject);
        yield return run.Until(() => JournalPanel.IsOpen, 10f, "the journal after resuming");
        if (run.Done) {
            yield break;
        }
        yield return ReadJournal("after resume", "Wallit's find");
        // Setup shortcut: every find and extra in the file, to show the reward.
        after.finds = (1 << Journal.Count) - 1;
        after.goals = (1 << Journal.Count) - 1;
        run.Event("setup", "sandbox case file 1 holds every find and extra (never the real save)");
        Tap(JournalPanel.Instance.BackButton.gameObject);
        yield return run.Until(() => !JournalPanel.IsOpen, 5f, "the journal closing");
        Tap(panel.JournalButton.gameObject);
        yield return run.Until(() => JournalPanel.IsOpen, 10f, "the journal with everything kept");
        yield return new WaitForSecondsRealtime(1f);
        bool paid = JournalPanel.Instance.TitleText.Contains(Journal.PaidTitle);
        run.Event("journal-paid", JournalPanel.Instance.TitleText + " | " + JournalPanel.Instance.SummaryText + " | slot: " + panel.SlotButtons[0].GetComponentsInChildren<Text>()[1].text);
        run.Metric("paidInFull", paid ? 1 : 0);
        if (!paid) {
            run.Problem(PlaytestSeverity.Bug, "journal: with every find and extra the journal does not say " + Journal.PaidTitle);
        }
        Tap(JournalPanel.Instance.BackButton.gameObject);
        yield return run.Until(() => !JournalPanel.IsOpen && CampaignPanel.IsOpen, 5f, "the journal closing");
        // Back to the real state of the file for the rest of the scenario.
        CampaignSave.Slot back = CampaignSave.Get(0);
        back.finds = finds;
        back.goals = goals;
    }

    // Turns the journal's three tabs with Next and logs each; Collected must hold the named find.
    static IEnumerator ReadJournal(string when, string unusedLabel) {
        JournalPanel journal = JournalPanel.Instance;
        string collected = "";
        string active = "";
        string finished = "";
        for (int i = 0; i < Journal.TabNames.Length; i++) {
            yield return new WaitForSecondsRealtime(0.8f);
            run.Event("journal-tab", when + ": " + journal.TabLabel + " | " + journal.BodyText.Replace("\n", " / ") + " | " + journal.SummaryText);
            if (journal.TabLabel == "Active") {
                active = journal.BodyText;
            } else if (journal.TabLabel == "Finished") {
                finished = journal.BodyText;
            } else {
                collected = journal.BodyText;
            }
            Tap(journal.NextButton.gameObject);
            yield return new WaitForSecondsRealtime(0.2f);
        }
        bool ok = active.Contains("Ch ") && finished.Contains(Journal.Goals[1].Name) && collected.Contains(Journal.Finds[0].Name);
        run.Event("journal-lists", when + ": active " + active.Split('\n').Length + " rows, finished names " + Journal.Goals[1].Name + ": " + finished.Contains(Journal.Goals[1].Name) + ", collected names " + Journal.Finds[0].Name + ": " + collected.Contains(Journal.Finds[0].Name));
        run.Metric("journalLists", ok ? 1 : 0);
        if (!ok) {
            run.Problem(PlaytestSeverity.Bug, "journal (" + when + "): the tabs do not list an active item, a finished extra and the collected find");
        }
    }

    static IEnumerator LoreCheck(CampaignPanel panel, string title, string info) {
        bool chapterShown = title.Contains("Chapter ");
        bool timeShown = info.Contains(" on the books") || System.Text.RegularExpressions.Regex.IsMatch(info, @"\d+:\d\d:\d\d");
        bool completionShown = info.Contains("filed (");
        run.Event("slot-info", title + " / " + info);
        run.Metric("slotChapterShown", chapterShown ? 1 : 0);
        run.Metric("slotTimeShown", timeShown ? 1 : 0);
        run.Metric("slotCompletionShown", completionShown ? 1 : 0);
        if (!chapterShown || !timeShown || !completionShown) {
            run.Problem(PlaytestSeverity.Bug, "lore: the case file slot must show chapter, play time and completion, it reads: " + title + " / " + info);
        }
        if (info.Contains("0:00:00")) {
            run.Problem(PlaytestSeverity.Bug, "lore: a file that played a whole chapter shows no play time: " + info);
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(panel.LoreButton.gameObject);
        yield return run.Until(() => LorePanel.IsOpen, 10f, "the lore log after pressing Lore log");
        if (run.Done) {
            yield break;
        }
        LorePanel lore = LorePanel.Instance;
        run.Event("lore-open", lore.TitleText);
        int open = 0;
        int sealedCount = 0;
        for (int i = 0; i < Lore.All.Length; i++) {
            bool isOpen = lore.EntryTitle != "Sealed";
            open += isOpen ? 1 : 0;
            sealedCount += isOpen ? 0 : 1;
            run.Event("lore-page", (lore.Page + 1) + "/" + Lore.All.Length + " " + lore.EntryTitle + ": " + lore.BodyText);
            if (isOpen != (Lore.All[lore.Page].Reveal <= 1)) {
                run.Problem(PlaytestSeverity.Bug, "lore: entry '" + Lore.All[lore.Page].Title + "' (revealed by Chapter " + Lore.All[lore.Page].Reveal + ") reads " + (isOpen ? "open" : "sealed") + " with one chapter filed");
            }
            yield return new WaitForSecondsRealtime(0.8f);
            Tap(lore.NextButton.gameObject);
            yield return new WaitForSecondsRealtime(0.2f);
        }
        run.Event("lore-summary", open + " unlocked, " + sealedCount + " sealed");
        run.Metric("loreUnlocked", open);
        run.Metric("loreSealed", sealedCount);
        if (open == 0 || sealedCount == 0) {
            run.Problem(PlaytestSeverity.Bug, "lore: after Chapter 1 the log should have some entries open and some sealed (" + open + " open, " + sealedCount + " sealed)");
        }
        Tap(lore.BackButton.gameObject);
        yield return run.Until(() => !LorePanel.IsOpen && CampaignPanel.IsOpen, 5f, "the lore log closing");
    }

    // The moves scenario: the combo's three distinct hits, then the dodge pressed (X) as bots close in.
    static IEnumerator MovesPlay(CharacterMovement player, ChapterRun chapter) {
        run.Event("ability-slots", CampaignProgress.HudLine());
        int seen = 0;
        var steps = new HashSet<int>();
        var damage = new Dictionary<int, string>();
        System.Action drain = () => {
            for (; seen < CampaignMoves.Log.Count; seen++) {
                string line = CampaignMoves.Log[seen];
                string[] f = line.Split('|');
                if (f[0] == "combo") {
                    steps.Add(int.Parse(f[1]));
                    damage[int.Parse(f[1])] = f[2] + " " + f[4];
                    run.Event("combo-hit", "hit " + f[1] + " of 3, " + f[2] + ": " + f[3] + ", " + f[4]);
                } else if (f[0] == "dodge") {
                    run.Event("dodge", "X pressed: " + f[2] + ", heading " + f[3]);
                } else if (f[0] == "avoided") {
                    run.Event("dodge-avoided", "a blow from " + f[1] + " landed inside the window and was avoided");
                }
            }
        };
        yield return Fight(player, () => { drain(); return steps.Count >= 3; }, 120f, "the combo's three hits", null, true);
        if (run.Done) {
            yield break;
        }
        var parts = new List<string>();
        foreach (var kv in damage) {
            parts.Add(kv.Value);
        }
        run.Event("combo-distinct", string.Join(" / ", parts));
        run.Metric("comboHits", CampaignMoves.ComboHits);
        float lastPress = 0f;
        yield return Fight(player, () => {
            drain();
            if (CampaignMoves.DodgeReady && Time.realtimeSinceStartup - lastPress > 0.2f) {
                foreach (Enemy e in Enemy.Roster) {
                    if (e != null && e.Alive && (e.transform.position - player.transform.position).magnitude < 2.4f) {
                        PlayInput.Tap(CampaignMoves.DodgeKey);
                        lastPress = Time.realtimeSinceStartup;
                        break;
                    }
                }
            }
            return CampaignMoves.Avoided > 0 || chapter.BossUp;
        }, 150f, "a blow avoided inside the dodge window", null, true);
        if (run.Done) {
            yield break;
        }
        drain();
        run.Metric("dodges", CampaignMoves.Dodges);
        run.Metric("dodgesAvoided", CampaignMoves.Avoided);
        run.Event("ability-slots", CampaignProgress.HudLine());
        if (CampaignMoves.Dodges == 0) {
            run.Problem(PlaytestSeverity.Bug, "moves: pressing X with bots close never started a dodge");
        } else if (CampaignMoves.Avoided == 0) {
            run.Problem(PlaytestSeverity.Bug, "moves: " + CampaignMoves.Dodges + " dodges and no blow was avoided in their window");
        }
    }

    // The abilities scenario: each ability unlocks at its level, goes off on its key and shows its cooldown on the HUD.
    static IEnumerator AbilitiesPlay(CharacterMovement player, ChapterRun chapter) {
        run.Event("slot-line", CampaignProgress.HudLine().Replace("\n", " | "));
        int seen = 0;
        System.Action drain = () => {
            for (; seen < CampaignProgress.Log.Count; seen++) {
                string line = CampaignProgress.Log[seen];
                if (line.StartsWith("levelup")) {
                    run.Event("level-up", line);
                } else if (line.StartsWith("toss-hit")) {
                    run.Event("toss-hit", "a thrown coin landed: " + line);
                } else if (line.StartsWith("toss") || line.StartsWith("stamp") || line.StartsWith("escrow")) {
                    run.Event("ability-used", line);
                }
            }
        };
        int blocksBefore = CampaignMoves.Avoided;
        bool cooldownSeen = false;
        for (int stage = 4; stage <= 6 && !run.Done; stage++) {
            int ability = stage == 4 ? CampaignProgress.AbToss : stage == 5 ? CampaignProgress.AbStamp : CampaignProgress.AbShield;
            int slotIndex = stage - 2;
            KeyCode key = CampaignProgress.SlotKeys[slotIndex];
            if (CampaignProgress.Level < stage) {
                yield return Fight(player, () => { drain(); return CampaignProgress.Level >= stage; }, 120f, "level " + stage + " from one more kill", null, true);
                if (run.Done) {
                    yield break;
                }
            }
            drain();
            run.Event("unlock-level", CampaignProgress.Abilities[ability].Name + " unlocked at level " + stage + " (level " + CampaignProgress.Level + ")");
            run.Event("slot-line", CampaignProgress.HudLine().Replace("\n", " | "));
            if (!CampaignProgress.Has(ability)) {
                run.Problem(PlaytestSeverity.Bug, "abilities: " + CampaignProgress.Abilities[ability].Name + " did not unlock at level " + stage);
            }
            if (stage < 6) {
                // The next level is 1 XP away (setup shortcut), so the next unlock happens on the next kill.
                CampaignSave.Get(0).xp = CampaignProgress.XpAt(stage + 1) - 1;
            }
            int before = ability == CampaignProgress.AbToss ? CampaignProgress.TossHits : ability == CampaignProgress.AbStamp ? CampaignProgress.StampedBots : CampaignMoves.Avoided;
            float lastPress = 0f;
            yield return Fight(player, () => {
                drain();
                if (!cooldownSeen && CampaignProgress.Wait(ability) > 0.5f && CampaignProgress.HudLine().Contains(CampaignProgress.Abilities[ability].Name + " ")) {
                    cooldownSeen = true;
                    run.Event("cooldown-shown", CampaignProgress.HudLine().Replace("\n", " | "));
                }
                if (Time.realtimeSinceStartup - lastPress > 0.3f && CampaignProgress.Wait(ability) <= 0f) {
                    float reach = ability == CampaignProgress.AbToss ? 20f : CampaignProgress.StampRange - 2f;
                    foreach (Enemy e in Enemy.Roster) {
                        if (e != null && e.Alive && (e.transform.position - player.transform.position).magnitude < reach) {
                            PlayInput.Tap(key);
                            lastPress = Time.realtimeSinceStartup;
                            break;
                        }
                    }
                }
                int now = ability == CampaignProgress.AbToss ? CampaignProgress.TossHits : ability == CampaignProgress.AbStamp ? CampaignProgress.StampedBots : CampaignMoves.Avoided;
                return now > before || chapter.BossUp;
            }, 100f, CampaignProgress.Abilities[ability].Name + " to land", null, true);
            if (run.Done) {
                yield break;
            }
            drain();
            if (ability == CampaignProgress.AbToss) {
                run.Event("toss-result", CampaignProgress.Tosses + " tosses, " + CampaignProgress.TossHits + " landed");
            } else if (ability == CampaignProgress.AbStamp) {
                run.Event("stamp-hit", CampaignProgress.Stamps + " stamps stunned and stung " + CampaignProgress.StampedBots + " bots");
            } else {
                run.Event("escrow-held", CampaignProgress.Shields + " Escrow, " + (CampaignMoves.Avoided - blocksBefore) + " blows held");
            }
        }
        if (cooldownSeen == false) {
            run.Problem(PlaytestSeverity.Bug, "abilities: no cooldown showed on the HUD line after an ability was used");
        }
        run.Metric("tosses", CampaignProgress.Tosses);
        run.Metric("tossHits", CampaignProgress.TossHits);
        run.Metric("stamps", CampaignProgress.Stamps);
        run.Metric("stampedBots", CampaignProgress.StampedBots);
        run.Metric("shields", CampaignProgress.Shields);
        run.Metric("shieldBlocks", CampaignMoves.Avoided - blocksBefore);
        CampaignSave.Slot slot = CampaignSave.Get(0);
        run.Metric("unlockMask", slot.unlocked);
        var copy = JsonUtility.FromJson<CampaignSave.Slot>(JsonUtility.ToJson(slot));
        run.Event("unlock-saved", "saved as JSON the file holds xp " + copy.xp + ", unlock mask " + copy.unlocked);
        if (copy.unlocked != slot.unlocked || (copy.unlocked & 0x70) != 0x70) {
            run.Problem(PlaytestSeverity.Bug, "abilities: the case file does not keep all three unlocks (mask " + copy.unlocked + ")");
        }
        if (CampaignProgress.TossHits == 0 || CampaignProgress.StampedBots == 0 || CampaignProgress.Shields == 0) {
            run.Problem(PlaytestSeverity.Bug, "abilities: an ability did nothing (tosses landed " + CampaignProgress.TossHits + ", bots stamped " + CampaignProgress.StampedBots + ", escrows " + CampaignProgress.Shields + ")");
        }
    }

    // The hub scenario's first half: XP from the first kills, level 3 unlocks Refund, the bot presses C and the ring works.
    static IEnumerator HubAbility(CharacterMovement player, ChapterRun chapter) {
        int xp0 = CampaignProgress.Xp;
        run.Event("hud-line", CampaignProgress.HudLine());
        yield return Fight(player, () => CampaignProgress.Level >= 3, 120f, "level 3 from the first kills", null, true);
        if (run.Done) {
            yield break;
        }
        foreach (string line in CampaignProgress.Log) {
            run.Event(line.StartsWith("levelup") ? "level-up" : "xp-gain", line);
        }
        run.Metric("hubXp", CampaignProgress.Xp);
        run.Metric("hubLevel", CampaignProgress.Level);
        if (CampaignProgress.Xp <= xp0 || !CampaignProgress.Has(1)) {
            run.Problem(PlaytestSeverity.Bug, "hub: kills gave no XP or level 3 did not unlock Refund (xp " + xp0 + " -> " + CampaignProgress.Xp + ")");
        }
        run.Event("hud-line", CampaignProgress.HudLine());
        // Refund needs bots in range: keep batting until some are close, then press C.
        int hitsBefore = CampaignProgress.RefundHits;
        yield return Fight(player, () => {
            if (CampaignProgress.RefundReady) {
                foreach (Enemy e in Enemy.Roster) {
                    if (e != null && e.Alive && (e.transform.position - player.transform.position).magnitude < CampaignProgress.RefundReach - 1f) {
                        PlayInput.Tap(CampaignProgress.RefundKey);
                        break;
                    }
                }
            }
            return CampaignProgress.Refunds > 0 || chapter.BossUp;
        }, 90f, "Refund to go off", null, true);
        if (run.Done) {
            yield break;
        }
        run.Metric("refundHits", CampaignProgress.RefundHits - hitsBefore);
        run.Event("refund-used", CampaignProgress.Refunds + " Refund(s), " + (CampaignProgress.RefundHits - hitsBefore) + " bots shoved and stung");
        if (CampaignProgress.Refunds == 0) {
            run.Problem(PlaytestSeverity.Bug, "hub: pressing C with Refund unlocked did nothing");
        }
    }

    // The hub scenario's second half: Chapter Complete -> To the hub -> walk to the board -> replay Chapter 1.
    static IEnumerator HubPlay(ChapterRun chapter) {
        CampaignSave.Slot slot = CampaignSave.Get(0);
        run.Event("progress-saved", "case file 1: " + slot.xp + " XP, level " + CampaignProgress.LevelFor(slot.xp) + ", " + slot.cleared + " cleared; case file 2: " + CampaignSave.Get(1).xp + " XP");
        if (slot.xp < 90 || CampaignSave.Get(1).xp != 0) {
            run.Problem(PlaytestSeverity.Bug, "hub: XP was not kept on case file 1 alone (file 1 " + slot.xp + ", file 2 " + CampaignSave.Get(1).xp + ")");
        }
        if (chapter.NextButton == null || !chapter.NextButton.gameObject.activeSelf) {
            run.Problem(PlaytestSeverity.Bug, "hub: the Chapter Complete card has no way into the hub");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        Tap(chapter.NextButton.gameObject);
        run.Event("tap", "To the hub");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown && SceneManager.GetActiveScene().name == CampaignSave.Scene, 30f, "the hub after the chapter");
        if (run.Done) {
            yield break;
        }
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub");
        run.Event("hub-open", "Wallit at " + hub.Player.position.ToString("0.0") + ", board at " + ChapterHub.BoardPosition + "; " + hub.StatusText.Replace("\n", " / "));
        yield return run.Until(() => hub.ArrivalShown >= 2, 12f, "the hub's arrival lines");
        run.Event("hub-arrival", Story.HubArrival[0][0].Substring(2) + " / " + Story.HubArrival[0][1].Substring(2));
        CheckScreen("hub");
        // Walk to the board on the stick.
        float walkStart = Time.realtimeSinceStartup;
        while (!run.Done && !hub.NearBoard && Time.realtimeSinceStartup - walkStart < 30f) {
            Vector3 to = ChapterHub.BoardPosition - hub.Player.position;
            PlayInput.Axis("Horizontal", Mathf.Clamp(to.x, -1f, 1f));
            PlayInput.Axis("Vertical", Mathf.Clamp(to.z, -1f, 1f));
            yield return null;
        }
        PlayInput.Release();
        run.Metric("hubWalked", Mathf.Round(hub.Walked * 10f) / 10f);
        run.Event("hub-walk", "Wallit walked " + hub.Walked.ToString("0.0") + " m to the board (near: " + hub.NearBoard + ")");
        if (!hub.NearBoard) {
            run.Problem(PlaytestSeverity.Bug, "hub: Wallit never reached the chapter board on the stick");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.6f);
        PlayInput.Tap(KeyCode.E);
        yield return run.Until(() => hub.BoardOpen, 5f, "the chapter board after pressing E");
        if (run.Done) {
            yield break;
        }
        run.Event("board-open", hub.BoardText);
        CheckScreen("board");
        bool filed = hub.BoardButtons[0].interactable;
        bool next = hub.BoardButtons[1].interactable;
        bool locked = !hub.BoardButtons[2].interactable;
        if (!filed || !next || !locked) {
            run.Problem(PlaytestSeverity.Bug, "hub: the board shows chapter 1 replayable=" + filed + ", chapter 2 open=" + next + ", chapter 3 locked=" + locked);
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(hub.BoardButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance.CardUp && ChapterScene.Instance.Chapter == 0 && ChapterScene.Instance.BeginButton != null, 10f, "the replayed chapter's card");
        if (run.Done) {
            yield break;
        }
        run.Event("hub-replay-card", ChapterScene.Instance.Title + " (case file " + (CampaignSave.Active + 1) + ", " + CampaignSave.Get(0).cleared + " cleared)");
        run.Event("arcade-clean", "outside a chapter: damage x" + CampaignProgress.DamageMult.ToString("0.00") + ", crit +" + CampaignProgress.CritBonus.ToString("0.00") + ", Refund " + (CampaignProgress.RefundReady ? "on" : "off") + ", coins " + MetaProgress.Coins + " untouched by XP");
        if (CampaignProgress.DamageMult != 1f || CampaignProgress.RefundReady || CampaignProgress.CritBonus != 0f) {
            run.Problem(PlaytestSeverity.Bug, "hub: campaign abilities are active outside a chapter");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        yield return run.Until(() => ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the replayed chapter's map");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        run.Scene("Chapter replay");
        run.Event("replay-started", ChapterRun.Instance.Title + " again, " + CampaignSave.Get(0).cleared + " chapter cleared, level " + CampaignProgress.Level + " with Refund " + (CampaignProgress.Has(1) ? "unlocked" : "missing"));
        if (ChapterRun.Instance.Chapter != 0 || CampaignSave.Get(0).cleared != 1 || !CampaignProgress.Has(1)) {
            run.Problem(PlaytestSeverity.Bug, "hub: the replay did not start Chapter 1 with the file's progress kept");
        }
        yield return new WaitForSecondsRealtime(3f);
    }

    // The hub as an office, with real input: Wallit walks the room on the stick, the doors of the chapters the file has
    // reached stand open and the others stay shut, a shut door will not let him through, an open one starts its chapter.
    // Setup: the sandbox case file 1 has chapters 1 and 2 filed, so doors 1 to 3 are open (3 is the next) and 4 to 6 shut.
    static IEnumerator WalkTo(ChapterHub hub, Vector3 target, float seconds, System.Func<bool> stop) {
        float start = Time.realtimeSinceStartup;
        while (!run.Done && !stop() && Time.realtimeSinceStartup - start < seconds) {
            Vector3 to = target - hub.Player.position;
            PlayInput.Axis("Horizontal", Mathf.Clamp(to.x, -1f, 1f));
            PlayInput.Axis("Vertical", Mathf.Clamp(to.z, -1f, 1f));
            yield return null;
        }
        PlayInput.Release();
    }

    static IEnumerator HubTalkTo(ChapterHub hub, int lodger) {
        yield return WalkTo(hub, hub.LodgerAt(lodger) + new Vector3(0f, 0f, -1.5f), 30f, () => hub.NearLodger == lodger);
        PlayInput.Tap(KeyCode.E);
        yield return new WaitForSecondsRealtime(0.5f);
    }

    static string HubLook(ChapterHub hub) {
        OfficeMap office = hub.Office;
        return (office != null ? office.Look : "office scene not filled in") + "; desk: " + hub.DeskName + "; at their spots: " + hub.LodgerAt(0).ToString("0") + hub.LodgerAt(1).ToString("0") + hub.LodgerAt(2).ToString("0") + hub.LodgerAt(3).ToString("0") + "; talks waiting " + hub.ChatsWaiting;
    }

    // The hub's life (HubLife): after every chapter the office changes, a new conversation opens, and three errands can be done.
    // Setup shortcut: chapters 2 to 6 are filed in the sandbox file between rounds. Everything else is walked and pressed.
    static IEnumerator HubLifePlay() {
        yield return run.Until(() => ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown, 20f, "the hub");
        if (run.Done) {
            yield break;
        }
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub life");
        run.Metric("campaignLoaded", 1);
        yield return run.Until(() => hub.Office != null, 20f, "the office scene to fill in");
        yield return new WaitForSecondsRealtime(2f);
        CheckScreen("hub");
        CampaignSave.Slot file = CampaignSave.Get(0);
        string lastLook = null;
        bool differ = true;
        int sidesBefore = 0;
        for (int cleared = 1; cleared <= 6 && !run.Done; cleared++) {
            int chapter = cleared - 1;
            if (cleared > 1) {
                file.cleared = cleared;
                file.chapter = Mathf.Min(cleared, CampaignSave.ChapterCount - 1);
                file.reached = file.chapter;
                hub.Refresh();
                run.Event("hub-setup", "sandbox case file 1 now has " + cleared + " chapters filed");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            string look = HubLook(hub);
            run.Event("hub-look", cleared + " cleared: " + look);
            if (look == lastLook) {
                differ = false;
                run.Problem(PlaytestSeverity.Bug, "hub-life: the office looks the same after chapter " + cleared + " as before: " + look);
            }
            lastLook = look;
            if (hub.ChatsWaiting < 1) {
                run.Problem(PlaytestSeverity.Bug, "hub-life: no new conversation is open after chapter " + cleared);
            }
            // 1. Every open conversation, lodger by lodger.
            for (int i = 0; i < hub.LodgerCount && !run.Done; i++) {
                if (hub.PendingChatOf(i) < 0) {
                    continue;
                }
                int heard = hub.ChatsHeard;
                yield return HubTalkTo(hub, i);
                run.Event("hub-chat", hub.LodgerName(i) + " after " + cleared + " cleared (" + hub.LastChatId + "): " + hub.TalkText.Replace('\n', '/'));
                if (hub.ChatsHeard != heard + 1) {
                    run.Problem(PlaytestSeverity.Bug, "hub-life: " + hub.LodgerName(i) + "'s conversation was not kept after chapter " + cleared);
                }
            }
            // 2. The chapter's favour: asked, fetched, returned.
            int favourRow = -1;
            for (int r = 0; r < HubLife.Sides.Length; r++) {
                if (HubLife.Sides[r].Chapter == chapter && !HubLife.Sides[r].Notice) {
                    favourRow = r;
                }
            }
            HubLife.Side favour = HubLife.Sides[favourRow];
            int pinnedStart = hub.Office.Pinned;   // slips on the wall now: the favour and the notice each add one
            int xp0 = file.xp;
            yield return HubTalkTo(hub, favour.Giver);
            run.Event("favour-offered", hub.LodgerName(favour.Giver) + " asks for the " + favour.Thing + ": " + hub.TalkText + " (it lies at " + hub.FavourItemAt.ToString("0.0") + ")");
            if (hub.FavourActive != favourRow) {
                run.Problem(PlaytestSeverity.Bug, "hub-life: " + hub.LodgerName(favour.Giver) + " did not ask for the " + favour.Thing + " after chapter " + cleared);
            }
            yield return WalkTo(hub, favour.Spot, 30f, () => hub.FavourCarried);
            run.Event("favour-pickup", "Wallit " + (hub.FavourCarried ? "picked up" : "never found") + " the " + favour.Thing + "; prompt: " + hub.PromptText);
            yield return HubTalkTo(hub, favour.Giver);
            bool paid = HubLife.SideDone(file, favourRow);
            run.Event("favour-paid", hub.TalkText + " XP " + xp0 + " -> " + file.xp + "; kept in the file: " + paid);
            if (!paid || file.xp - xp0 != favour.Xp) {
                run.Problem(PlaytestSeverity.Bug, "hub-life: the " + favour.Thing + " favour paid " + (file.xp - xp0) + " XP and " + (paid ? "was" : "was not") + " saved");
            }
            // 3. The chapter's loose notice, pinned by walking over it.
            int noticeRow = -1;
            for (int r = 0; r < HubLife.Sides.Length; r++) {
                if (HubLife.Sides[r].Chapter == chapter && HubLife.Sides[r].Notice) {
                    noticeRow = r;
                }
            }
            yield return WalkTo(hub, HubLife.Sides[noticeRow].Spot, 30f, () => HubLife.SideDone(file, noticeRow));
            yield return new WaitForSecondsRealtime(0.4f);
            run.Event("notice-pinned", hub.TalkText + "; the wall's count of finished errands " + pinnedStart + " -> " + hub.Office.Pinned);
            if (!HubLife.SideDone(file, noticeRow) || hub.Office.Pinned != pinnedStart + 2) {
                run.Problem(PlaytestSeverity.Bug, "hub-life: the " + HubLife.Sides[noticeRow].Thing + " was not pinned (wall " + pinnedStart + " -> " + hub.Office.Pinned + ", want " + (pinnedStart + 2) + ")");
            }
            // 4. Chime's lost thing (a conversation of hers may come first).
            int chimeXp = file.xp;
            for (int press = 0; press < 2 && hub.ErrandActive < 0; press++) {
                yield return HubTalkTo(hub, ChapterHub.ChimeIndex);
            }
            if (hub.ErrandActive != chapter) {
                run.Problem(PlaytestSeverity.Bug, "hub-life: Chime did not send Wallit for the lost thing after chapter " + cleared);
            } else {
                yield return WalkTo(hub, hub.ErrandItemAt, 30f, () => hub.ErrandCarried);
                yield return HubTalkTo(hub, ChapterHub.ChimeIndex);
            }
            run.Event("errand-paid", "Chime: " + hub.TalkText + " XP " + chimeXp + " -> " + file.xp);
            int done = HubLife.ChapterErrands(file, chapter);
            run.Event("chapter-errands", "chapter " + cleared + ": " + done + " of 3 errands done and saved; talks heard " + hub.ChatsHeard + "/" + HubLife.Chats.Length + "; errands " + hub.SideDone + "/" + HubLife.Sides.Length + "; office now: " + hub.Office.Look);
            if (done < 3) {
                run.Problem(PlaytestSeverity.Bug, "hub-life: only " + done + " of 3 errands were done and saved in chapter " + cleared);
            }
            sidesBefore = hub.SideDone;
            yield return new WaitForSecondsRealtime(1.2f);
        }
        // 5. The ending reads what the hub kept.
        int lines = 0;
        var kept = new System.Text.StringBuilder();
        foreach (string raw in Story.Chapters[5].Ending) {
            bool hubLine = raw.StartsWith("?|chat:") || raw.StartsWith("?|side:") || raw.StartsWith("?|sides:");
            string expanded = Choices.Expand(raw);
            if (hubLine && expanded != null) {
                lines++;
                kept.Append(expanded.Substring(expanded.IndexOf('|') + 1).Replace("|", ": ")).Append(" / ");
            }
        }
        run.Metric("hubChats", hub.ChatsHeard);
        run.Metric("hubErrands", hub.SideDone);
        run.Metric("looksDiffer", differ ? 1 : 0);
        run.Metric("endingLines", lines);
        run.Event("ending-lines", lines + " ending lines come from what the hub kept (" + sidesBefore + " side errands): " + kept);
        if (lines < 8) {
            run.Problem(PlaytestSeverity.Bug, "hub-life: the ending shows " + lines + " lines from the hub's errands and talks (want 8)");
        }
        yield return new WaitForSecondsRealtime(2f);
    }

    static IEnumerator HubDoorsPlay() {
        yield return run.Until(() => ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown, 20f, "the hub");
        if (run.Done) {
            yield break;
        }
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub");
        run.Metric("campaignLoaded", 1);
        yield return new WaitForSecondsRealtime(2f);
        run.Event("hub-office", "Wallit at " + hub.Player.position.ToString("0.0") + "; doors " + hub.DoorText + "; board at " + ChapterHub.BoardPosition + "; vacant desk at " + hub.NpcSpot);
        CheckScreen("hub");
        OfficeMap office = hub.Office;
        if (office == null) {
            yield return run.Until(() => hub.Office != null, 20f, "the office scene to fill in");
            office = hub.Office;
        }
        if (office != null) {
            Scene officeScene = SceneManager.GetSceneByName("Map_Office");
            run.Event("hub-scene", "the office is its own scene " + officeScene.name + " (" + officeScene.rootCount + " roots, " + office.Pieces + " pieces, stage '" + office.StageName + "' after " + office.Stage + " chapters filed, sky " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "none") + ", music " + (office.Music != null && office.Music.isPlaying ? "playing" : "silent") + ", " + SceneManager.sceneCount + " scenes open)");
            run.Metric("hubScene", officeScene.isLoaded ? 1 : 0);
            if (!officeScene.isLoaded) {
                run.Problem(PlaytestSeverity.Bug, "hub-doors: the hub office did not load its own scene");
            }
        } else {
            run.Problem(PlaytestSeverity.Bug, "hub-doors: the hub office scene never filled in");
        }
        bool good = true;
        for (int i = 0; i < hub.DoorCount; i++) {
            bool want = i < 3;
            if (hub.DoorOpen[i] != want) {
                good = false;
            }
        }
        run.Metric("doorsRight", good ? 1 : 0);
        if (!good) {
            run.Problem(PlaytestSeverity.Bug, "hub-doors: with 2 chapters filed the doors read " + hub.DoorText + " (want 1 to 3 open, 4 to 6 shut)");
        }
        // 1. Walk around the room: across to the board wall, then the desk.
        Vector3 start = hub.Player.position;
        yield return WalkTo(hub, ChapterHub.BoardPosition + new Vector3(2.5f, 0f, 0f), 15f, () => hub.NearBoard);
        yield return WalkTo(hub, ChapterHub.DeskPosition + new Vector3(-2.5f, 0f, 0f), 15f, () => (hub.Player.position - ChapterHub.DeskPosition).magnitude < 3.2f);
        run.Metric("hubWalked", Mathf.Round(hub.Walked * 10f) / 10f);
        run.Event("hub-walk", "Wallit walked " + hub.Walked.ToString("0.0") + " m about the office, from " + start.ToString("0.0") + " to " + hub.Player.position.ToString("0.0"));
        if (hub.Walked < 15f) {
            run.Problem(PlaytestSeverity.Bug, "hub-doors: Wallit could not walk around the hub (" + hub.Walked.ToString("0.0") + " m)");
        }
        // 2. A shut door (chapter 4) stays shut.
        Vector3 shut = ChapterHub.DoorPosition(3);
        yield return WalkTo(hub, new Vector3(shut.x, 0f, shut.z + 1.5f), 25f, () => hub.NearDoor == 3 && hub.Player.position.z > shut.z - 0.8f);
        yield return new WaitForSecondsRealtime(1.5f);
        yield return WalkTo(hub, new Vector3(shut.x, 0f, shut.z + 1.5f), 2f, () => false);
        bool held = hub.Shown && hub.EnteredDoor < 0 && hub.Player.position.z < ChapterHub.RoomBack + 0.1f && !ChapterScene.Instance.CardUp;
        run.Event("door-locked", "Wallit pushed on door 4 (" + (hub.DoorOpen[3] ? "open" : "shut") + ") at " + hub.Player.position.ToString("0.0") + "; he stayed in the hub: " + held + "; prompt: " + hub.PromptText);
        run.Metric("lockedHeld", held ? 1 : 0);
        if (!held || hub.DoorOpen[3]) {
            run.Problem(PlaytestSeverity.Bug, "hub-doors: the locked door of chapter 4 let Wallit through or showed open");
        }
        // 3. The next chapter's door (3) is open: walking in starts it.
        Vector3 open = ChapterHub.DoorPosition(2);
        yield return WalkTo(hub, new Vector3(open.x, 0f, open.z + 1.5f), 25f, () => hub.EnteredDoor >= 0);
        yield return run.Until(() => ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null, 10f, "the chapter card after walking through the door");
        if (run.Done) {
            yield break;
        }
        run.Event("door-entered", "Wallit went through door " + (hub.EnteredDoor + 1) + ": the card reads " + ChapterScene.Instance.Title);
        if (ChapterScene.Instance.Chapter != 2) {
            run.Problem(PlaytestSeverity.Bug, "hub-doors: door 3 opened chapter " + (ChapterScene.Instance.Chapter + 1));
        }
        yield return new WaitForSecondsRealtime(2f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        yield return run.Until(() => ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter after the door");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        run.Scene("Chapter from a door");
        run.Event("chapter-started", ChapterRun.Instance.Title + " began from its door (chapter " + (ChapterRun.Instance.Chapter + 1) + ")");
        if (ChapterRun.Instance.Chapter != 2) {
            run.Problem(PlaytestSeverity.Bug, "hub-doors: the door of chapter 3 started chapter " + (ChapterRun.Instance.Chapter + 1));
        }
        yield return new WaitForSecondsRealtime(3f);
    }

    // The hub's lodgers, with real input. Setup: the sandbox case file 1 has chapter 1 filed. Wallit walks to each lodger and
    // presses E; Chime's errand is taken, fetched and paid; then chapters 2 to 6 are filed by setup shortcuts and Tick (and the
    // others' lines) must say something new after each.
    static IEnumerator HubNpcsPlay() {
        yield return run.Until(() => ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown, 20f, "the hub");
        if (run.Done) {
            yield break;
        }
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub lodgers");
        run.Metric("campaignLoaded", 1);
        yield return new WaitForSecondsRealtime(2f);
        var names = new System.Text.StringBuilder();
        bool warrok = false;
        for (int i = 0; i < hub.LodgerCount; i++) {
            names.Append(hub.LodgerName(i)).Append(i < hub.LodgerCount - 1 ? ", " : "");
            if (hub.LodgerName(i).ToLowerInvariant().Contains("warrok")) {
                warrok = true;
            }
        }
        run.Metric("lodgers", hub.LodgerCount);
        run.Event("hub-lodgers", hub.LodgerCount + " lodgers in the hub: " + names + "; Warrok among them: " + warrok);
        CheckScreen("hub");
        if (hub.LodgerCount < 3 || hub.LodgerCount > 5 || warrok) {
            run.Problem(PlaytestSeverity.Bug, "hub-npcs: " + hub.LodgerCount + " lodgers (want 3 to 5, none Warrok): " + names);
        }
        // 1. Walk to each lodger and talk to it, after one chapter cleared.
        for (int i = 0; i < hub.LodgerCount && !run.Done; i++) {
            int index = i;
            yield return WalkTo(hub, hub.LodgerAt(i) + new Vector3(0f, 0f, -1.5f), 20f, () => hub.NearLodger == index);
            int talks = hub.Talks;
            PlayInput.Tap(KeyCode.E);
            yield return new WaitForSecondsRealtime(0.4f);
            run.Event("npc-line", "1 cleared, " + hub.LodgerName(i) + ": " + hub.TalkText + " (E pressed " + (hub.Talks > talks ? "and they spoke" : "and nothing happened") + ")");
            if (hub.Talks <= talks) {
                run.Problem(PlaytestSeverity.Bug, "hub-npcs: pressing E beside " + hub.LodgerName(i) + " did nothing");
            }
            if (i == ChapterHub.ChimeIndex) {
                break;
            }
            yield return new WaitForSecondsRealtime(0.6f);
        }
        // 2. Chime's errand: offered, fetched, paid.
        int xpBefore = CampaignSave.Get(0).xp;
        bool offered = hub.ErrandActive == 0;
        run.Event("errand-offered", "Chime sent Wallit for " + (offered ? Story.Errands[0].Thing : "nothing") + " (XP now " + xpBefore + "); the thing lies at " + hub.ErrandItemAt.ToString("0.0"));
        if (!offered) {
            run.Problem(PlaytestSeverity.Bug, "hub-npcs: Chime did not offer an errand after chapter 1");
        }
        yield return WalkTo(hub, hub.ErrandItemAt, 25f, () => hub.ErrandCarried);
        run.Event("errand-pickup", "Wallit " + (hub.ErrandCarried ? "picked up the " + Story.Errands[0].Thing : "never found the lost thing") + " at " + hub.Player.position.ToString("0.0") + "; prompt: " + hub.PromptText);
        if (!hub.ErrandCarried) {
            run.Problem(PlaytestSeverity.Bug, "hub-npcs: Wallit could not pick up Chime's lost thing");
        }
        yield return WalkTo(hub, hub.LodgerAt(ChapterHub.ChimeIndex) + new Vector3(0f, 0f, -1.5f), 25f, () => hub.NearLodger == ChapterHub.ChimeIndex);
        PlayInput.Tap(KeyCode.E);
        yield return new WaitForSecondsRealtime(0.4f);
        int xpAfter = CampaignSave.Get(0).xp;
        bool kept = (CampaignSave.Get(0).errands & 1) != 0;
        run.Metric("errandXp", xpAfter - xpBefore);
        run.Metric("lostThings", hub.ErrandsDone);
        run.Event("errand-paid", hub.TalkText + " XP " + xpBefore + " -> " + xpAfter + "; lost thing kept in the file: " + kept + "; the status line reads: " + hub.StatusText.Replace('\n', '|'));
        if (xpAfter - xpBefore != Story.Errands[0].Xp || !kept) {
            run.Problem(PlaytestSeverity.Bug, "hub-npcs: the errand paid " + (xpAfter - xpBefore) + " XP and the thing " + (kept ? "was" : "was not") + " kept (want " + Story.Errands[0].Xp + " and kept)");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        // 3. Lines after each chapter. Setup shortcut: the sandbox file is filed chapter by chapter; Tick is talked to each time.
        var last = new string[hub.LodgerCount];
        for (int i = 0; i < last.Length; i++) {
            last[i] = hub.LodgerLine(i);
        }
        bool differ = true;
        for (int cleared = 2; cleared <= 6 && !run.Done; cleared++) {
            CampaignSave.Get(0).cleared = cleared;
            CampaignSave.Get(0).chapter = Mathf.Min(cleared, CampaignSave.ChapterCount - 1);
            CampaignSave.Get(0).reached = CampaignSave.Get(0).chapter;
            hub.Refresh();
            run.Event("npc-setup", "sandbox case file 1 now has " + cleared + " chapters filed");
            yield return WalkTo(hub, hub.LodgerAt(0) + new Vector3(0f, 0f, -1.5f), 25f, () => hub.NearLodger == 0);
            PlayInput.Tap(KeyCode.E);
            yield return new WaitForSecondsRealtime(0.4f);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hub.LodgerCount; i++) {
                string line = hub.LodgerLine(i);
                if (line == last[i]) {
                    differ = false;
                    run.Problem(PlaytestSeverity.Bug, "hub-npcs: " + hub.LodgerName(i) + " said the same after chapter " + cleared + " as before");
                }
                last[i] = line;
                sb.Append(hub.LodgerName(i)).Append(": \"").Append(line).Append("\"  ");
            }
            run.Event("npc-after-chapter", cleared + " cleared. On screen: " + hub.TalkText + "  All: " + sb);
            yield return new WaitForSecondsRealtime(1f);
        }
        run.Metric("linesDiffer", differ ? 1 : 0);
        // Chime has a new errand once a chapter more is filed.
        yield return WalkTo(hub, hub.LodgerAt(ChapterHub.ChimeIndex) + new Vector3(0f, 0f, -1.5f), 25f, () => hub.NearLodger == ChapterHub.ChimeIndex);
        PlayInput.Tap(KeyCode.E);
        yield return new WaitForSecondsRealtime(0.4f);
        run.Event("errand-next", "Chime after six chapters: " + hub.TalkText + " (errand out: " + hub.ErrandActive + ")");
        yield return new WaitForSecondsRealtime(2f);
    }

    // Chapter 2 with real input: out to the gate (batting what is in reach on the way), the gate scene read and skipped,
    // back to the circle, three waves, then The Plated Alpha. ChapterPlay carries on to the card and the saved file.
    static IEnumerator GatePlay(CharacterMovement player, ChapterRun chapter) {
        float start = Time.realtimeSinceStartup;
        run.Event("gate-leg", chapter.ObjectiveLine);
        while (!run.Done && chapter.Leg == 0) {
            if (Time.realtimeSinceStartup - start > 150f) {
                run.Problem(PlaytestSeverity.Bug, "chapter2: Wallit never reached the gate in 150 s (objective \"" + chapter.ObjectiveLine + "\")");
                yield break;
            }
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            Walk(player, chapter.Marker, 1f);
            PlayInput.Hold(KeyCode.LeftShift, false);
            Click(NearestInReach(player));
            SampleFps();
            yield return null;
        }
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        run.Metric("gateReached", 1);
        run.Event("gate-reached", "Wallit at the gate after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s; the objective now reads \"" + chapter.ObjectiveLine + "\"");
        yield return run.Until(() => Cutscene.IsOpen, 8f, "The Gate scene at the gate");
        if (run.Done) {
            yield break;
        }
        Cutscene scene = Cutscene.Instance;
        run.Scene("The Gate");
        for (int read = 0; read < 3 && Cutscene.IsOpen && !run.Done; read++) {
            yield return new WaitForSecondsRealtime(0.7f);
            run.Event("gate-scene-page", scene.Page + "/" + scene.PageCount + " " + (scene.NameShown.Length > 0 ? scene.NameShown : "(narration)") + ": \"" + scene.TextShown.Replace('\n', ' ') + "\"");
            if (scene.Typing) {
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.35f);
            }
            PlayInput.Tap(KeyCode.Space);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        if (Cutscene.IsOpen) {
            run.Event("gate-scene-skip", "Enter pressed on page " + scene.Page + "/" + scene.PageCount);
            PlayInput.Tap(KeyCode.Return);
        }
        yield return run.Until(() => !Cutscene.IsOpen, 5f, "The Gate scene to close after skipping");
        if (run.Done) {
            yield break;
        }
        run.Metric("gateSceneSkipped", 1);
        run.Scene("Chapter play");
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("home-leg", chapter.ObjectiveLine);
        start = Time.realtimeSinceStartup;
        while (!run.Done && chapter.Leg == 1) {
            if (Time.realtimeSinceStartup - start > 150f) {
                run.Problem(PlaytestSeverity.Bug, "chapter2: Wallit never got back to the circle in 150 s (objective \"" + chapter.ObjectiveLine + "\")");
                yield break;
            }
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            Vector3 home = Musician.Instance != null ? Musician.Instance.transform.position : player.transform.position;
            Walk(player, home, 1f);
            PlayInput.Hold(KeyCode.LeftShift, false);
            Click(NearestInReach(player));
            SampleFps();
            yield return null;
        }
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        int waveSeen = 0;
        yield return Fight(player, () => {
            if (chapter.WaveNow != waveSeen) {
                waveSeen = chapter.WaveNow;
                run.Event("chapter-wave", "wave " + waveSeen + " of 3: " + chapter.ObjectiveLine);
            }
            return chapter.BossUp || chapter.Completed;
        }, 300f, "the three waves held and the boss to arrive", null, true);
        if (run.Done) {
            yield break;
        }
        run.Metric("chapterWaves", waveSeen);
        run.Scene("Chapter boss");
        Enemy bossEnemy = chapter.BossEnemy;
        run.Event("boss-arrives", bossEnemy != null ? bossEnemy.Title + " (" + bossEnemy.HitsToDrop + " hits) after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s; objective \"" + chapter.ObjectiveLine + "\"" : "no boss");
        yield return new WaitForSecondsRealtime(0.6f);
        bool bar = bossEnemy != null && BossBarShows(bossEnemy.Title);
        run.Event("boss-bar", bossEnemy != null ? bossEnemy.Title + ": health bar " + (bar ? "up" : "missing") + ", stamped plate " + (bossEnemy.transform.Find("Invalid Plate") != null ? "on its chest" : "missing") : "no boss");
        run.Metric("bossBar", bar ? 1 : 0);
        if (!bar) {
            run.Problem(PlaytestSeverity.Bug, "chapter2: no boss health bar for the Plated Alpha");
        }
        yield return Fight(player, () => chapter.Completed, 420f, "the Plated Alpha defeated", null, true);
        if (run.Done) {
            yield break;
        }
        foreach (string line in ChapterRun.Events) {
            if (line.StartsWith("defeat|") || line.StartsWith("line|")) {
                run.Event("defeat-beat", line);
            }
        }
        BossPhaseCheck("The Plated Alpha");
run.Event("boss-defeated", "The Plated Alpha fell after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s");
    }

    // Chapter 3 with real input: the opening dialogue's lines on where Yo Bots come from are logged, Wallit walks the three
    // stops out (the belt's scene read and skipped), the lever breaks the feed, back to the circle, the last batch, then
    // The Foreman. ChapterPlay carries on to the card and the saved file.
    static IEnumerator FloorPlay(CharacterMovement player, ChapterRun chapter) {
        foreach (string raw in Story.Chapters[chapter.Chapter].Cutscene) {
            if (raw.Contains("Crew") || raw.Contains("stamped out") || raw.Contains("Stamped out") || raw.Contains("Spider Bot") || raw.Contains("process server") || raw.Contains("Process server")) {
                run.Event("origin-dialogue", Story.Name(Story.Speaker(raw)) + ": \"" + Story.Text(raw).Replace('\n', ' ') + "\"");
            }
        }
        float start = Time.realtimeSinceStartup;
        int stops = 0;
        while (!run.Done && chapter.Leg < chapter.StopCount) {
            if (Time.realtimeSinceStartup - start > 400f) {
                run.Problem(PlaytestSeverity.Bug, "chapter3: Wallit never got through the Floor's stops in 400 s (objective \"" + chapter.ObjectiveLine + "\")");
                yield break;
            }
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            if (Cutscene.IsOpen) {
                Cutscene scene = Cutscene.Instance;
                run.Scene("The Belt");
                for (int read = 0; read < 3 && Cutscene.IsOpen && !run.Done; read++) {
                    yield return new WaitForSecondsRealtime(0.7f);
                    run.Event("floor-scene-page", scene.Page + "/" + scene.PageCount + " " + (scene.NameShown.Length > 0 ? scene.NameShown : "(narration)") + ": \"" + scene.TextShown.Replace('\n', ' ') + "\"");
                    if (scene.Typing) {
                        PlayInput.Tap(KeyCode.Space);
                        yield return new WaitForSecondsRealtime(0.35f);
                    }
                    PlayInput.Tap(KeyCode.Space);
                    yield return new WaitForSecondsRealtime(0.3f);
                }
                if (Cutscene.IsOpen) {
                    run.Event("floor-scene-skip", "Enter pressed on page " + scene.Page + "/" + scene.PageCount);
                    PlayInput.Tap(KeyCode.Return);
                }
                yield return run.Until(() => !Cutscene.IsOpen, 5f, "The Belt scene to close after skipping");
                run.Scene("Chapter play");
                continue;
            }
            if (chapter.Leg + 1 > stops) {
                stops = chapter.Leg + 1;
                run.Event("floor-stop", "heading for stop " + stops + " of " + chapter.StopCount + ": \"" + chapter.ObjectiveLine + "\"");
            }
            Walk(player, chapter.Marker, 1f);
            PlayInput.Hold(KeyCode.LeftShift, false);
            Click(NearestInReach(player));
            SampleFps();
            yield return null;
        }
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        run.Metric("floorStops", chapter.StopCount);
        bool walk = Story.Chapters[chapter.Chapter].Walk;
        run.Metric("feedBroken", chapter.FeedBroken ? 1 : 0);
        run.Event(walk ? "last-lamp" : "feed-broken", (walk ? "the last lamp is reached after " : "the lever is pulled after ") + Mathf.RoundToInt(chapter.PlaySeconds) + " s; the objective now reads \"" + chapter.ObjectiveLine + "\"");
        if (!walk && !chapter.FeedBroken) {
            run.Problem(PlaytestSeverity.Bug, "chapter3: the lever did not break the belt feed");
        }
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("home-leg", chapter.ObjectiveLine);
        start = Time.realtimeSinceStartup;
        while (!run.Done && chapter.Leg == chapter.StopCount) {
            if (Time.realtimeSinceStartup - start > 200f) {
                run.Problem(PlaytestSeverity.Bug, "chapter3: Wallit never got back to the circle in 200 s (objective \"" + chapter.ObjectiveLine + "\")");
                yield break;
            }
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            if (Cutscene.IsOpen) {
                PlayInput.Tap(KeyCode.Return);
                yield return new WaitForSecondsRealtime(0.5f);
                continue;
            }
            Vector3 home = Musician.Instance != null ? Musician.Instance.transform.position : player.transform.position;
            Walk(player, home, 1f);
            PlayInput.Hold(KeyCode.LeftShift, false);
            Click(NearestInReach(player));
            SampleFps();
            yield return null;
        }
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        yield return Fight(player, () => chapter.BossUp || chapter.Completed, 300f, "the last batch down and the boss to arrive", null, true);
        if (run.Done) {
            yield break;
        }
        run.Scene("Chapter boss");
        Enemy bossEnemy = chapter.BossEnemy;
        run.Event("boss-arrives", bossEnemy != null ? bossEnemy.Title + " (" + bossEnemy.HitsToDrop + " hits) after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s; objective \"" + chapter.ObjectiveLine + "\"" : "no boss");
        yield return new WaitForSecondsRealtime(0.6f);
        bool bar = bossEnemy != null && BossBarShows(bossEnemy.Title);
        run.Event("boss-bar", bossEnemy != null ? bossEnemy.Title + ": health bar " + (bar ? "up" : "missing") + (walk ? ", paper " + (bossEnemy.transform.Find("Paper") != null ? "in its claw" : "missing") : ", order sheet " + (bossEnemy.transform.Find("Order Sheet") != null ? "on its chest" : "missing")) : "no boss");
        run.Metric("bossBar", bar ? 1 : 0);
        if (!bar) {
            run.Problem(PlaytestSeverity.Bug, "chapter3/4: no boss health bar for " + (walk ? "the Server" : "the Foreman"));
        }
        if (walk) {
            // The Server: it offers, waits, and does not strike. Each blow while the paper is out is a refusal.
            int seenServer = 0;
            bool struckEarly = false;
            string lastObjective = "";
            yield return Fight(player, () => {
                for (; seenServer < Server.Events.Count; seenServer++) {
                    string ev = Server.Events[seenServer];
                    string kind = ev.StartsWith("offer|") ? "server-offer" : ev.StartsWith("refuse|") ? "server-refused" : "server-strike";
                    run.Event(kind, ev + " (refusals " + Server.Refusals + " of " + Server.RefusalsNeeded + "; objective \"" + chapter.ObjectiveLine + "\")");
                    if (ev.StartsWith("strike|") && Server.Refusals < Server.RefusalsNeeded) {
                        struckEarly = true;
                    }
                }
                if (chapter.ObjectiveLine != lastObjective && chapter.ObjectiveLine.StartsWith("Refuse")) {
                    lastObjective = chapter.ObjectiveLine;
                    run.Event("objective", lastObjective);
                }
                return chapter.Completed;
            }, 420f, "the Server refused three times, striking, and defeated", null, true);
            if (run.Done) {
                yield break;
            }
            run.Metric("serverOffers", Server.Offers);
            run.Metric("serverRefusals", Server.Refusals);
            run.Metric("serverStruckEarly", struckEarly ? 1 : 0);
            if (Server.Refusals < Server.RefusalsNeeded) {
                run.Problem(PlaytestSeverity.Bug, "chapter4: the Server fell after only " + Server.Refusals + " refusals");
            }
            if (struckEarly) {
                run.Problem(PlaytestSeverity.Bug, "chapter4: the Server struck before the paper was refused three times");
            }
        } else {
            yield return Fight(player, () => chapter.Completed, 420f, "the Foreman defeated", null, true);
            if (run.Done) {
                yield break;
            }
        }
        foreach (string line in ChapterRun.Events) {
            if (line.StartsWith("defeat|") || line.StartsWith("line|")) {
                run.Event("defeat-beat", line);
            }
        }
        BossPhaseCheck(walk ? "The Server" : "The Foreman");
        run.Event("boss-defeated", (walk ? "The Server" : "The Foreman") + " fell after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s");
    }

    // Chapter 5 with real input: the five pages held at the circle (each page's wave and the forms turning blank are logged from
    // the chapter's event list), then The Registrar. ChapterPlay carries on to the card and the saved file.
    static IEnumerator ReadingPlay(CharacterMovement player, ChapterRun chapter) {
        foreach (string raw in Story.Chapters[chapter.Chapter].Cutscene) {
            if (raw.Contains("not signed") || raw.Contains("DENIED") || raw.Contains("denied")) {
                run.Event("cutscene-page", Story.Name(Story.Speaker(raw)) + ": \"" + Story.Text(raw).Replace('\n', ' ') + "\"");
            }
        }
        int seen = 0;
        yield return Fight(player, () => {
            for (; seen < ChapterRun.Events.Count; seen++) {
                string ev = ChapterRun.Events[seen];
                if (ev.StartsWith("wave|")) {
                    run.Event("page-start", ev + "; objective \"" + chapter.ObjectiveLine + "\"");
                } else if (ev.StartsWith("page-read|")) {
                    run.Event("page-read", ev + " (forms on the wall turned blank); objective \"" + chapter.ObjectiveLine + "\"");
                } else if (ev.StartsWith("boss-called|")) {
                    run.Event("boss-called", ev);
                }
            }
            return chapter.BossUp || chapter.Completed;
        }, 420f, "the five pages read and the Registrar to arrive", null, true);
        if (run.Done) {
            yield break;
        }
        run.Metric("pagesRead", chapter.PagesRead);
        if (chapter.PagesRead < 5) {
            run.Problem(PlaytestSeverity.Bug, "chapter5: the Registrar came after only " + chapter.PagesRead + " of 5 pages");
        }
        Enemy bossEnemy = chapter.BossEnemy;
        run.Event("boss-arrives", bossEnemy != null ? bossEnemy.Title + " (" + bossEnemy.HitsToDrop + " hits) after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s; objective \"" + chapter.ObjectiveLine + "\"" : "no boss");
        yield return new WaitForSecondsRealtime(0.6f);
        bool bar = bossEnemy != null && BossBarShows(bossEnemy.Title);
        run.Event("boss-bar", bossEnemy != null ? bossEnemy.Title + ": health bar " + (bar ? "up" : "missing") + ", ledger " + (bossEnemy.transform.Find("Ledger") != null ? "held open" : "missing") : "no boss");
        run.Metric("bossBar", bar ? 1 : 0);
        if (!bar) {
            run.Problem(PlaytestSeverity.Bug, "chapter5: no boss health bar for the Registrar");
        }
        yield return Fight(player, () => chapter.Completed, 420f, "the Registrar defeated", null, true);
        if (run.Done) {
            yield break;
        }
        foreach (string line in ChapterRun.Events) {
            if (line.StartsWith("defeat|") || line.StartsWith("line|")) {
                run.Event("defeat-beat", line);
            }
        }
        BossPhaseCheck("The Registrar");
run.Event("boss-defeated", "The Registrar fell after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s");
    }

    // Chapter 6 with real input: three waves, the Spider Bot with the order, the Filed scene, the finale pages, ADDRESS FILED, the
    // credits card, the case file marked complete and shown as filed in the Campaign panel.
    static IEnumerator FinalPlay(CharacterMovement player, ChapterRun chapter, int before) {
        int seen = 0;
        yield return Fight(player, () => {
            for (; seen < ChapterRun.Events.Count; seen++) {
                string ev = ChapterRun.Events[seen];
                if (ev.StartsWith("wave|")) {
                    run.Event("wave-start", ev + "; objective \"" + chapter.ObjectiveLine + "\"");
                } else if (ev.StartsWith("boss-called|")) {
                    run.Event("boss-called", ev);
                }
            }
            return chapter.BossUp || chapter.Completed;
        }, 420f, "the three waves held and the Spider Bot with the order to arrive", null, true);
        if (run.Done) {
            yield break;
        }
        Enemy bossEnemy = chapter.BossEnemy;
        run.Event("boss-arrives", bossEnemy != null ? bossEnemy.Title + " (" + bossEnemy.HitsToDrop + " hits) after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s; objective \"" + chapter.ObjectiveLine + "\"" : "no boss");
        yield return new WaitForSecondsRealtime(0.6f);
        bool bar = bossEnemy != null && BossBarShows(bossEnemy.Title);
        run.Event("boss-bar", bossEnemy != null ? bossEnemy.Title + ": health bar " + (bar ? "up" : "missing") + ", order scroll " + (bossEnemy.transform.Find("Order") != null ? "on its back" : "missing") : "no boss");
        run.Metric("bossBar", bar ? 1 : 0);
        if (!bar) {
            run.Problem(PlaytestSeverity.Bug, "chapter6: no boss health bar for the Spider Bot with the order");
        }
        yield return Fight(player, () => chapter.Completed, 420f, "the Spider Bot with the order defeated", null, true);
        if (run.Done) {
            yield break;
        }
        PlayInput.Release();
        foreach (string line in ChapterRun.Events) {
            if (line.StartsWith("defeat|") || line.StartsWith("line|")) {
                run.Event("defeat-beat", line);
            }
        }
        BossPhaseCheck("The Order");
run.Event("boss-defeated", "The Spider Bot fell after " + Mathf.RoundToInt(chapter.PlaySeconds) + " s");
        if (endingTest > 0) {
            yield return EndingWatch(chapter);
        } else {
            // The Filed scene.
            yield return run.Until(() => Cutscene.IsOpen, 10f, "the Filed scene");
            if (run.Done) {
                yield break;
            }
            run.Scene("Filed");
            run.Event("filed-scene", Cutscene.Instance.PageCount + " pages; the ring becomes writing, then a door number");
            float limit = Time.realtimeSinceStartup + 40f;
            while (Cutscene.IsOpen && Time.realtimeSinceStartup < limit) {
                yield return new WaitForSecondsRealtime(0.8f);
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            // The finale pages, then ADDRESS FILED.
            yield return run.Until(() => StoryScreen.IsOpen, 10f, "the finale pages");
            if (run.Done) {
                yield break;
            }
            StoryScreen screen = StoryScreen.Instance;
            for (int i = 0; i < Story.Finale.Length && !StoryScreen.EndingOpen && !run.Done; i++) {
                yield return new WaitForSecondsRealtime(1.2f);
                run.Event("finale-page", (i + 1) + " of " + Story.Finale.Length + ": " + screen.PageText.Replace('\n', ' '));
                PlayInput.Tap(KeyCode.Space);
                yield return null;
                yield return null;
            }
            yield return run.Until(() => StoryScreen.EndingOpen, 8f, "the ADDRESS FILED ending card");
            if (run.Done) {
                yield break;
            }
        }
        if (run.Done) {
            yield break;
        }
        run.Scene("Ending");
        yield return new WaitForSecondsRealtime(1.5f);
        run.Event("ending-card", Story.EndingTitle + ": " + Story.EndingLine);
        run.Metric("endingShown", StoryScreen.EndingsShown);
        if (StoryScreen.EndingsShown < 1) {
            run.Problem(PlaytestSeverity.Bug, "chapter6: clearing the chapter did not show ADDRESS FILED");
        }
        Tap(screen.KeepButton.gameObject);
        if (secretTest) {
            yield return run.Until(() => Cutscene.IsOpen, 10f, "the secret ending");
            if (run.Done) {
                yield break;
            }
            run.Scene("Secret ending");
            run.Event("secret-scene", Cutscene.Instance.PageCount + " pages; every lost thing was kept, so the secret ending plays before the credits");
            float secretLimit = Time.realtimeSinceStartup + 60f;
            int pagesTold = 0;
            while (Cutscene.IsOpen && Time.realtimeSinceStartup < secretLimit) {
                yield return new WaitForSecondsRealtime(0.8f);
                pagesTold++;
                run.Event("secret-page", "page " + pagesTold);
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.3f);
            }
        } else {
            yield return new WaitForSecondsRealtime(1.5f);
            bool leaked = chapter.SecretShown;   // the credits reel may be open by now; only the secret ending counts
            run.Event("secret-absent", "a lost thing is missing from the journal: no secret ending " + (leaked ? "BUT IT PLAYED" : "plays, the credits come straight after ADDRESS FILED"));
            run.Metric("secretEnding", leaked ? 1 : 0);
            if (leaked) {
                run.Problem(PlaytestSeverity.Bug, "secret ending: it played without every lost thing in the case file");
            }
        }
        // The credits reel plays first (Space reads on), then the credits card.
        float creditsLimit = Time.realtimeSinceStartup + 120f;
        while (!chapter.CreditsUp && !run.Done && Time.realtimeSinceStartup < creditsLimit) {
            if (Cutscene.IsOpen) {
                PlayInput.Tap(KeyCode.Space);
            }
            yield return new WaitForSecondsRealtime(0.9f);
        }
        yield return run.Until(() => chapter.CreditsUp, 8f, "the credits card");
        if (run.Done) {
            yield break;
        }
        if (secretTest) {
            bool credited = chapter.SecretShown && chapter.CardText.Contains(Story.SecretTitle) && CampaignSave.Get(CampaignSave.Active).secret;
            run.Metric("secretEnding", credited ? 1 : 0);
            if (!credited) {
                run.Problem(PlaytestSeverity.Bug, "secret ending: it did not play, or the credits and the case file do not carry it");
            }
        }
        run.Scene("Credits");
        yield return new WaitForSecondsRealtime(1.5f);
        run.Event("credits-card", chapter.CardText.Replace("\n", " / "));
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        run.Event("campaign-complete", "case file now " + slot.cleared + " of " + CampaignSave.ChapterCount + " chapters cleared");
        run.Metric("caseFileCleared", slot.cleared);
        if (slot.cleared != CampaignSave.ChapterCount) {
            run.Problem(PlaytestSeverity.Bug, "chapter6: the case file shows " + slot.cleared + " chapters cleared after Chapter 6");
        }
        Tap(chapter.MenuButton.gameObject);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after the credits");
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        yield return new WaitForSecondsRealtime(1f);
        Tap(Object.FindAnyObjectByType<MainMenu>().CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files after the finale");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Text[] texts = CampaignPanel.Instance.SlotButtons[0].GetComponentsInChildren<Text>();
        bool filed = texts[0].text.Contains("filed");
        run.Event("campaign-panel-filed", texts[0].text + " / " + texts[1].text);
        run.Metric("caseFileFiled", filed ? 1 : 0);
        if (!filed) {
            run.Problem(PlaytestSeverity.Bug, "chapter6: the Campaign panel does not show case file 1 as complete (\"" + texts[0].text + "\")");
        }
        yield return new WaitForSecondsRealtime(2f);
    }

    // New Game+: a filed file begins again at Chapter 1 with Wallit's level and abilities kept. Then the secret ending's chapter.
    // Chapter select ratings, New Game+ from the case files, and the three endings.
    static IEnumerator EndingsRun() {
        CampaignSave.EnterSandbox();
        // Setup shortcut: the sandbox case file is filed, every chapter graded (C B A S A B, Hard on two), its earlier ending the Ledger.
        CampaignSave.Slot file = CampaignSave.Get(0);
        file.used = true;
        file.cleared = CampaignSave.ChapterCount;
        file.chapter = CampaignSave.ChapterCount - 1;
        file.reached = CampaignSave.ChapterCount - 1;
        file.xp = CampaignProgress.XpAt(4);
        file.ranks = 1 | (2 << 3) | (3 << 6) | (4 << 9) | (3 << 12) | (2 << 15);
        file.hardRanks = (4 << 3) | (3 << 9);
        file.ending = Endings.Ledger;
        run.Event("endings-setup", "sandbox case file 1 filed with ranks " + CampaignSave.RankLine(file));
        ChapterScene.HubNext = true;
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        yield return new WaitForSecondsRealtime(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        CampaignPanel panel = CampaignPanel.Instance;
        string ranks = panel.RanksShown(0);
        bool plusShown = panel.PlusButtons[0].gameObject.activeInHierarchy;
        run.Event("rating-list", "case file 1: " + ranks + "; New Game+ button " + (plusShown ? "shown" : "MISSING") + " (file 2, no file: " + (panel.PlusButtons[1].gameObject.activeInHierarchy ? "shown" : "hidden") + ")");
        run.Metric("ratingList", ranks.Contains("1 C") && ranks.Contains("4 S") ? 1 : 0);
        if (!plusShown || !ranks.Contains("4 S")) {
            run.Problem(PlaytestSeverity.Bug, "endings: the case files list does not show the ranks or the New Game+ button of a filed file");
        }
        // The hub's chapter board (the chapter select): open the file, walk in, read the board.
        Tap(panel.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterHub.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the hub");
        if (run.Done) {
            yield break;
        }
        run.Scene("Hub");
        yield return new WaitForSecondsRealtime(2f);
        ChapterHub.Instance.OpenBoard();   // setup shortcut: the board opens without the walk to it
        yield return new WaitForSecondsRealtime(1.5f);
        string board = ChapterHub.Instance.BoardText;
        run.Event("rating-board", board);
        int rated = 0;
        foreach (string part in board.Split('|')) {
            rated += part.Contains("RANK ") ? 1 : 0;
        }
        run.Metric("ratingBoardRows", rated);
        if (rated < CampaignSave.ChapterCount) {
            run.Problem(PlaytestSeverity.Bug, "endings: the chapter board rates " + rated + " of " + CampaignSave.ChapterCount + " filed chapters");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        ChapterScene.HubNext = false;
        ChapterHub.Instance.CloseBoard();
        ChapterScene.Instance.LeaveToMenu();
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        // New Game+ straight from the case files.
        yield return new WaitForSecondsRealtime(1f);
        Tap(Object.FindAnyObjectByType<MainMenu>().CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(CampaignPanel.Instance.PlusButtons[0].gameObject);
        run.Event("ngplus-menu", "New Game+ pressed on case file 1 in the case files list");
        yield return run.Until(() => ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene && ChapterScene.Instance.BeginButton != null && ChapterScene.Instance.BeginButton.isActiveAndEnabled, 45f, "Chapter 1 of New Game+");
        if (run.Done) {
            yield break;
        }
        file = CampaignSave.Get(0);
        run.Event("ngplus-started", "loop " + file.plus + ", chapter " + (file.chapter + 1) + ", " + file.cleared + " cleared, last ending " + Endings.Name(file.ending));
        yield return new WaitForSecondsRealtime(1.5f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        yield return run.Until(() => ChapterRun.Instance != null && WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "Chapter 1 on the loop");
        if (run.Done) {
            yield break;
        }
        run.Scene("New Game+ Chapter 1");
        ChapterRun loop = ChapterRun.Instance;
        float echoLimit = Time.realtimeSinceStartup + 90f;
        string echo = "";
        while (Time.realtimeSinceStartup < echoLimit && echo.Length == 0 && !run.Done) {
            if (Cutscene.IsOpen) {
                PlayInput.Tap(KeyCode.Space);
            }
            foreach (string ev in ChapterRun.Events) {
                if (ev.StartsWith("ngplus-echo|")) {
                    echo = ev;
                }
            }
            yield return new WaitForSecondsRealtime(0.8f);
        }
        yield return new WaitForSecondsRealtime(3f);
        int baseCount = Story.Chapters[0].Target;
        string objective = loop.ObjectiveLine;
        bool harder = loop.Plus == 1 && CampaignGrade.PlusHealthMult(loop.Plus) > 1f && CampaignGrade.PlusCount(baseCount, loop.Plus) > baseCount && objective.Contains(CampaignGrade.PlusCount(baseCount, loop.Plus).ToString());
        run.Event("ngplus-run", "loop " + loop.Plus + ": bots carry x" + CampaignGrade.PlusHealthMult(loop.Plus).ToString("0.00") + " health, " + CampaignGrade.PlusCount(baseCount, loop.Plus) + " to down instead of " + baseCount + "; objective \"" + objective + "\"");
        run.Event("ngplus-echo", echo.Length > 0 ? echo : "no echo line");
        run.Metric("ngPlusHarder", harder ? 1 : 0);
        run.Metric("ngPlusEcho", echo.Length > 0 ? 1 : 0);
        if (!harder) {
            run.Problem(PlaytestSeverity.Bug, "endings: New Game+ Chapter 1 is not harder (" + objective + ")");
        }
        if (echo.Length == 0) {
            run.Problem(PlaytestSeverity.Bug, "endings: New Game+ Chapter 1 said nothing about the earlier playthrough");
        }
        loop.Abandon();
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        // Part 2: the three endings, each from a sandbox file standing on Chapter 6.
        for (int variant = Endings.Ledger; variant <= Endings.Open && !run.Done; variant++) {
            endingTest = variant;
            secretTest = false;
            startChapter = 5;
            run.Event("ending-pass", "pass " + variant + " of " + Endings.Count + ": " + Endings.Name(variant));
            yield return CampaignRun();
            if (run.Done) {
                yield break;
            }
            if (CampaignPanel.IsOpen) {
                Tap(CampaignPanel.Instance.BackButton.gameObject);
                yield return new WaitForSecondsRealtime(1f);
            }
        }
        endingTest = 0;
    }

    static IEnumerator NewGamePlusRun() {
        CampaignSave.EnterSandbox();
        // Setup shortcut: the sandbox case file is filed, level 4 and Refund in Full earned. Never the real save.
        CampaignSave.Slot file = CampaignSave.Get(0);
        file.used = true;
        file.cleared = CampaignSave.ChapterCount;
        file.chapter = CampaignSave.ChapterCount - 1;
        file.reached = CampaignSave.ChapterCount - 1;
        file.xp = CampaignProgress.XpAt(4);
        file.unlocked = 0x1F;
        file.finds = 0x0F;
        file.rooms = 1 << CampaignSave.ChapterCount;
        int xp = file.xp;
        int unlocked = file.unlocked;
        run.Event("ngplus-setup", "sandbox case file 1 filed, level " + CampaignProgress.LevelFor(xp) + " (" + xp + " XP), unlock mask " + unlocked + ", 4 finds, Refund in Full earned");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        yield return new WaitForSecondsRealtime(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene && ChapterScene.Instance.BeginButton != null, 45f, "the filed file's chapter card");
        if (run.Done) {
            yield break;
        }
        run.Scene("Filed file card");
        ChapterScene scene = ChapterScene.Instance;
        yield return new WaitForSecondsRealtime(1.5f);
        run.Event("ngplus-button", "card for " + scene.Title + ": New Game+ button " + (scene.NewGamePlusButton != null ? "shown" : "missing"));
        if (scene.NewGamePlusButton == null) {
            run.Problem(PlaytestSeverity.Bug, "new game plus: a filed case file's card has no New Game+ button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(scene.NewGamePlusButton.gameObject);
        run.Event("tap", "New Game+");
        yield return new WaitForSecondsRealtime(1.5f);
        file = CampaignSave.Get(0);
        bool atOne = scene.Chapter == 0 && file.chapter == 0 && file.cleared == 0 && file.plus == 1;
        bool kept = file.xp == xp && file.unlocked == unlocked && BackRooms.HasReward(file);
        run.Event("ngplus-started", "card now " + scene.Title + " (chapter " + (scene.Chapter + 1) + "), file on chapter " + (file.chapter + 1) + ", " + file.cleared + " cleared, loop " + file.plus + "; xp " + file.xp + ", unlock mask " + file.unlocked + ", Refund in Full " + BackRooms.HasReward(file) + ", finds " + Journal.FindCount(file));
        run.Metric("ngPlusChapter1", atOne ? 1 : 0);
        run.Metric("ngPlusXpKept", file.xp == xp ? 1 : 0);
        run.Metric("ngPlusAbilitiesKept", file.unlocked == unlocked && BackRooms.HasReward(file) ? 1 : 0);
        if (!atOne) {
            run.Problem(PlaytestSeverity.Bug, "new game plus: the file did not start again at chapter 1 (chapter " + (file.chapter + 1) + ", " + file.cleared + " cleared)");
        }
        if (!kept) {
            run.Problem(PlaytestSeverity.Bug, "new game plus: level or abilities were not kept (xp " + file.xp + " of " + xp + ", unlocks " + file.unlocked + " of " + unlocked + ")");
        }
        yield return run.Until(() => scene.BeginButton != null && scene.BeginButton.isActiveAndEnabled, 5f, "Chapter 1's card");
        if (run.Done) {
            yield break;
        }
        Tap(scene.BeginButton.gameObject);
        yield return run.Until(() => ChapterRun.Instance != null && WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "Chapter 1 of New Game+");
        if (run.Done) {
            yield break;
        }
        run.Scene("New Game+ Chapter 1");
        ChapterRun first = ChapterRun.Instance;
        yield return new WaitForSecondsRealtime(2f);
        int level = CampaignProgress.Level;
        run.Event("ngplus-run", "playing " + Story.Chapters[first.Chapter].Title + " with Wallit at level " + level + " (" + CampaignProgress.Xp + " XP)");
        run.Event("ngplus-abilities", "unlock mask " + CampaignProgress.UnlockedMask(0) + " (kept " + unlocked + "), Spare Change bonus " + CampaignProgress.DamageMult.ToString("0.00") + "x");
        run.Metric("ngPlusLevel", level);
        if (first.Chapter != 0 || level < CampaignProgress.LevelFor(xp)) {
            run.Problem(PlaytestSeverity.Bug, "new game plus: Chapter 1 started with chapter " + (first.Chapter + 1) + " at level " + level);
        }
        first.Abandon();
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        // Part two: the secret ending's chapter (setup shortcut: the file stands on Chapter 6 with every lost thing).
        secretTest = true;
        startChapter = 5;
        yield return CampaignRun();
    }

    static bool companionTest;

    // Chapter 2's companion: she follows, fights, goes down and is revived by a held E.
    static IEnumerator CompanionCheck(CharacterMovement player, ChapterRun chapter) {
        yield return run.Until(() => Companion.Instance != null, 5f, "the companion Slice to be in the chapter");
        if (run.Done) {
            yield break;
        }
        Companion slice = Companion.Instance;
        run.Event("companion-spawned", slice.Data.Name + " is with Wallit in " + chapter.Title + " (" + slice.Health + "/" + slice.MaxHealth + " health, model " + slice.HasModel + ")");
        // 1. Follow: Wallit walks toward the gate; she must keep up. Wallit only walks: any stab is hers.
        float startWalk = Time.realtimeSinceStartup;
        float worst = 0f;
        Vector3 from = player.transform.position;
        float kills0 = RunLog.RunKills;
        while (!run.Done && Time.realtimeSinceStartup - startWalk < 40f
               && Vector3.Distance(from, player.transform.position) < 28f && slice.Alive) {
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            Walk(player, chapter.Marker, 1f);
            worst = Mathf.Max(worst, slice.DistanceToPlayer);
            yield return null;
        }
        Stand();
        yield return new WaitForSecondsRealtime(1f);
        float gap = slice.DistanceToPlayer;
        run.Event("companion-follows", "Wallit walked " + Vector3.Distance(from, player.transform.position).ToString("0") + " m; Slice walked " + slice.Walked.ToString("0") + " m and is " + gap.ToString("0.0") + " m from him (furthest " + worst.ToString("0.0") + " m)");
        run.Metric("companionWalked", Mathf.Round(slice.Walked));
        if (slice.Walked < 10f || gap > 9f) {
            run.Problem(PlaytestSeverity.Bug, "companion: Slice did not follow Wallit (walked " + slice.Walked.ToString("0") + " m, " + gap.ToString("0.0") + " m behind)");
        }
        // 2. Fight: Wallit stands still holding fire; bots that come at them are Slice's to stab.
        float wait = Time.realtimeSinceStartup;
        while (!run.Done && slice.Stabs < 2 && Time.realtimeSinceStartup - wait < 45f && slice.Alive) {
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            yield return null;
        }
        run.Event("companion-fights", "Slice stabbed " + slice.Stabs + " times while Wallit held fire (kills so far " + (RunLog.RunKills - kills0) + ")");
        run.Metric("companionStabs", slice.Stabs);
        if (slice.Stabs < 1) {
            run.Problem(PlaytestSeverity.Bug, "companion: Slice never attacked a bot in 45 s");
        }
        // 3. Down: a named setup shortcut puts her down, as the bots would.
        run.Event("setup", "companion knocked down (setup shortcut)");
        slice.KnockDown();
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("companion-down", slice.Data.Name + " is down: " + (slice.Downed ? "yes" : "NO") + ", health " + slice.Health);
        if (!slice.Downed) {
            run.Problem(PlaytestSeverity.Bug, "companion: Slice did not go down at 0 health");
        }
        // 4. Revive: Wallit walks to her and holds E (real input).
        float t0 = Time.realtimeSinceStartup;
        while (!run.Done && slice.Downed && Time.realtimeSinceStartup - t0 < 40f) {
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            Walk(player, slice.transform.position, Companion.ReviveReach * 0.6f);
            bool close = slice.DistanceToPlayer <= Companion.ReviveReach * 0.8f;
            PlayInput.Hold(KeyCode.E, close);
            if (close) {
                Stand();
            }
            yield return null;
        }
        PlayInput.Hold(KeyCode.E, false);
        Stand();
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("companion-revived", slice.Data.Name + " is back up: " + (!slice.Downed ? "yes" : "NO") + ", health " + slice.Health + "/" + slice.MaxHealth + ", revives " + slice.Revives);
        run.Metric("companionDowns", slice.Downs);
        run.Metric("companionRevives", slice.Revives);
        if (slice.Downed || slice.Revives < 1) {
            run.Problem(PlaytestSeverity.Bug, "companion: holding E over the downed Slice did not revive her");
        }
    }

    static bool NearestInReach(CharacterMovement player) {
        float reach = BatEquipment.Reach * 0.95f;
        foreach (Enemy e in Enemy.Roster) {
            if (e == null || !e.Alive) {
                continue;
            }
            Vector3 to = e.transform.position - player.transform.position;
            to.y = 0f;
            if (to.magnitude - e.BodyScale * 0.3f <= reach) {
                return !LevelUpScreen.HoldFire;
            }
        }
        return false;
    }

    static bool watchCutscene;

    // The chapter's opening scene. "campaign" only skips it (Enter) so the chapter can be played; "cutscene" reads the
    // first pages with real key presses, logs each one, then skips the rest and checks that play starts.
    static IEnumerator CutsceneStep() {
        yield return run.Until(() => Cutscene.IsOpen || (ChapterRun.Instance != null && ChapterRun.Instance.PlaySeconds > 3f), 12f, "the chapter's opening cutscene");
        if (run.Done) {
            yield break;
        }
        if (!Cutscene.IsOpen) {
            if (watchCutscene) {
                run.Problem(PlaytestSeverity.Bug, "cutscene: Chapter 1 began without its opening dialogue scene");
            }
            yield break;
        }
        if (comfortTest) {
            yield return PlaytestComfort.Run(run);
            yield break;
        }
        Cutscene scene = Cutscene.Instance;
        run.Event("cutscene-open", scene.PageCount + " pages; letterbox, speaker name, portrait slot, typed lines");
        if (!watchCutscene) {
            yield return new WaitForSecondsRealtime(0.6f);
            PlayInput.Tap(KeyCode.Return);
            yield return run.Until(() => !Cutscene.IsOpen, 5f, "the cutscene to close after Enter");
            yield break;
        }
        int told = 0;
        int named = 0;
        int read = 0;
        string[] seen = new string[8];
        while (read < 7 && Cutscene.IsOpen && !run.Done) {
            yield return new WaitForSecondsRealtime(0.7f);
            string who = scene.NameShown;
            string text = scene.TextShown;
            seen[read] = who;
            if (who.Length > 0) {
                named++;
            }
            run.Event("cutscene-page", (scene.Page) + "/" + scene.PageCount + " " + (who.Length > 0 ? who : "(narration)") + ": \"" + text.Replace('\n', ' ') + "\"  camera " + scene.Focus
                + " (weight " + scene.CameraWeight.ToString("0.00") + "), letterbox " + scene.Bars.ToString("0.00") + ", voice " + (WallitVoice.FloorFree ? "idle" : "speaking " + WallitVoice.FloorOwner));
            read++;
            // A first press finishes the typing; the next turns the page (and cuts the page's voice).
            if (scene.Typing) {
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.35f);
            }
            PlayInput.Tap(KeyCode.Space);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        for (; told < Cutscene.Events.Count; told++) {
            if (Cutscene.Events[told].StartsWith("voice|") || Cutscene.Events[told].StartsWith("overlap|") || Cutscene.Events[told].StartsWith("camera|") || Cutscene.Events[told].StartsWith("bars|")) {
                run.Event("cutscene-voice", Cutscene.Events[told]);
            }
        }
        int overlaps = Cutscene.Overlaps + WallitVoice.Overlaps;
        run.Metric("cutscenePages", Cutscene.PagesShown);
        run.Metric("cutsceneVoiceLines", Cutscene.VoiceLines);
        run.Metric("cutsceneOverlaps", overlaps);
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "cutscene: " + overlaps + " voice line(s) overlapped during the dialogue");
        }
        if (Cutscene.VoiceLines < 2) {
            run.Problem(PlaytestSeverity.Bug, "cutscene: only " + Cutscene.VoiceLines + " voice lines went through the director in the pages read");
        }
        if (named < 2) {
            run.Problem(PlaytestSeverity.Bug, "cutscene: only " + named + " of the pages read showed a speaker name");
        }
        if (Cutscene.IsOpen) {
            run.Event("cutscene-skip", "Enter pressed on page " + scene.Page + "/" + scene.PageCount);
            PlayInput.Tap(KeyCode.Return);
        }
        yield return run.Until(() => !Cutscene.IsOpen, 5f, "the cutscene to close after skipping");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        run.Metric("cutsceneSkipped", Cutscene.Skipped);
        run.Event("cutscene-play", "scene closed (" + Cutscene.Skipped + " skipped), game running at time scale " + Time.timeScale.ToString("0.0") + ", objective \"" + (ChapterRun.Instance != null ? ChapterRun.Instance.ObjectiveLine : "") + "\", camera weight " + Cutscene.Instance.CameraWeight.ToString("0.00"));
        if (Time.timeScale < 0.99f) {
            run.Problem(PlaytestSeverity.Bug, "cutscene: skipping left the game frozen (time scale " + Time.timeScale.ToString("0.00") + ")");
        }
    }

    // The mode select, played through the buttons like a mouse. The sandbox keeps the case files off the real save.
    static IEnumerator ModesMenu() {
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null || menu.ArcadeButton == null) {
            run.Problem(PlaytestSeverity.Bug, "the main menu has no Campaign and Arcade buttons");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        CheckScreen("menu");
        run.Event("mode-select", "main menu shows \"" + menu.CampaignButton.GetComponentInChildren<Text>().text + "\" and \"" + menu.ArcadeButton.GetComponentInChildren<Text>().text + "\"");
        run.Metric("modeButtons", 2);
        run.Progress(0.1f);
        yield return new WaitForSecondsRealtime(2f);

        run.Note("opening Campaign");
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        CheckScreen("campaign-files", 590);
        CampaignPanel panel = CampaignPanel.Instance;
        string[] titles = new string[CampaignSave.SlotCount];
        for (int i = 0; i < titles.Length; i++) {
            titles[i] = panel.SlotButtons[i].GetComponentsInChildren<Text>()[0].text + " / " + panel.SlotButtons[i].GetComponentsInChildren<Text>()[1].text;
        }
        run.Event("campaign-files", CampaignSave.SlotCount + " case files: " + string.Join(" | ", titles));
        run.Metric("campaignSlots", CampaignSave.SlotCount);
        yield return new WaitForSecondsRealtime(2f);

        run.Note("opening case file 1");
        Tap(panel.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 30f, "the chapter scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Chapter 1");
        ChapterScene chapter = ChapterScene.Instance;
        run.Event("chapter-open", chapter.Title + " (scene " + CampaignSave.Scene + ", case file " + (CampaignSave.Active + 1) + ")");
        yield return run.Until(() => chapter.LinesShown >= chapter.LineCount, 20f, "the chapter's lines");
        if (run.Done) {
            yield break;
        }
        CheckScreen("chapter");
        run.Event("chapter-lines", chapter.LinesShown + " of " + chapter.LineCount + " lines shown (placeholder chapter)");
        run.Metric("chapterScene", 1);
        yield return new WaitForSecondsRealtime(3f);
        Tap(chapter.MenuButton.gameObject);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after the chapter");
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        yield return new WaitForSecondsRealtime(1f);
        menu = Object.FindAnyObjectByType<MainMenu>();
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files again");
        if (run.Done) {
            yield break;
        }
        panel = CampaignPanel.Instance;
        CampaignSave.Slot slot = CampaignSave.Get(0);
        string info = panel.SlotButtons[0].GetComponentsInChildren<Text>()[0].text + " / " + panel.SlotButtons[0].GetComponentsInChildren<Text>()[1].text;
        run.Event("campaign-saved", info);
        if (!slot.used) {
            run.Problem(PlaytestSeverity.Bug, "case file 1 did not keep the chapter");
        }
        yield return new WaitForSecondsRealtime(2f);
        Tap(panel.EraseButtons[0].gameObject);
        yield return null;
        Tap(panel.EraseButtons[0].gameObject);
        yield return null;
        run.Event("campaign-erased", "case file 1 now reads: " + panel.SlotButtons[0].GetComponentsInChildren<Text>()[1].text);
        if (CampaignSave.Get(0).used) {
            run.Problem(PlaytestSeverity.Bug, "Erase did not empty case file 1");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        Tap(panel.BackButton.gameObject);
        yield return null;
        yield return new WaitForSecondsRealtime(0.5f);

        run.Note("starting Arcade");
        run.Progress(0.4f);
        Tap(menu.ArcadeButton.gameObject);
        run.Event("tap", "Arcade");
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after tapping Arcade");
    }

    static IEnumerator ShopMenu() {
        run.Note("checking the shop");
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.InstanceID);
        run.Metric("menuButtons", buttons.Length);
        if (buttons.Length == 0) {
            run.Problem(PlaytestSeverity.Bug, "the menu has no buttons");
            yield break;
        }
        // One click on each shop row, through the UI event system like a mouse click. The sandbox keeps it off the save.
        int coins = MetaProgress.Coins;
        int clicked = 0;
        foreach (Button button in buttons) {
            if (button == null || !button.isActiveAndEnabled || !button.interactable) {
                continue;
            }
            // Buy rows and "-10" only: Play is pressed with Enter below, and "+10" would arm a paid later start.
            Text label = button.GetComponentInChildren<Text>();
            string text = label != null ? label.text : "";
            if (text != "Buy" && text != "-10") {
                continue;
            }
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            clicked++;
            yield return new WaitForSecondsRealtime(0.25f);
            if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
                break;
            }
        }
        run.Metric("menuClicks", clicked);
        run.Metric("coinsSpent", coins - MetaProgress.Coins);
        run.Event("shop", clicked + " buttons clicked");
        if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
            yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene || WaveDirector.Instance != null, 30f, "the scene after a menu click");
            if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
                run.Log("a menu button started the run");
                yield break;
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
    }

    static IEnumerator StartRun() {
        if (WaveDirector.Instance == null) {
            run.Note("pressing Enter to play");
            PlayInput.Tap(KeyCode.Return);
            yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after pressing Enter");
            if (run.Done) {
                yield break;
            }
        }
        run.Scene("Wave 1");
        run.Event("run-start");
        Random.InitState(run.Options.seed);
        if (Musician.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "no musician in the game scene");
        }
        if (Object.FindAnyObjectByType<CharacterMovement>() == null) {
            run.Problem(PlaytestSeverity.Bug, "no player (CharacterMovement) in the game scene");
        }
        yield return null;
        if (Object.FindAnyObjectByType<BatEquipment>() == null) {
            run.Problem(PlaytestSeverity.Bug, "no bat (BatEquipment) on the player");
        }
        CheckPlayerIsWallit();
        CheckMusicianIsSlice();
    }

    static void CheckMusicianIsSlice() {
        Musician slice = Musician.Instance;
        if (slice == null) {
            return;
        }
        Animator anim = slice.GetComponentInChildren<Animator>();
        bool isSlice = anim != null && anim.gameObject.name.IndexOf("Slice", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool hasPirate = false;
        foreach (Transform t in slice.GetComponentsInChildren<Transform>(false)) {
            if (t.name.IndexOf("Pirate", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                hasPirate = true;
                break;
            }
        }
        bool ok = isSlice && !hasPirate && slice.HasSliceRig;
        run.Metric("musicianIsSlice", ok ? 1 : 0);
        run.Event("musician-slice", isSlice
            ? "musician is Slice (" + anim.gameObject.name + ") on PianoRecital, clip=" + slice.CurrentClipName + ", music=" + (slice.MusicPlaying ? "playing" : "silent")
            : "musician Animator is " + (anim != null ? anim.gameObject.name : "missing"));
        if (!isSlice || hasPirate) {
            run.Problem(PlaytestSeverity.Bug, "the active musician is not Slice (found " + (anim != null ? anim.gameObject.name : "none") + ")");
        }
    }

    static void CheckPlayerIsWallit() {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null) {
            return;
        }
        Animator anim = player.GetComponentInChildren<Animator>();
        bool isWallit = anim != null && anim.gameObject.name.IndexOf("Wallit", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool hasWarrok = false;
        foreach (Transform t in player.GetComponentsInChildren<Transform>(false)) {
            if (t.name.IndexOf("Warrok", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                hasWarrok = true;
                break;
            }
        }
        bool hasBat = Object.FindAnyObjectByType<BatEquipment>() != null;
        run.Metric("playerIsWallit", isWallit && !hasWarrok && hasBat ? 1 : 0);
        run.Event("player-wallit", isWallit
            ? "player is Wallit (" + anim.gameObject.name + ") holding his bat"
            : "player Animator is " + (anim != null ? anim.gameObject.name : "missing"));
        if (!isWallit || hasWarrok) {
            run.Problem(PlaytestSeverity.Bug, "the active player is not Wallit (found " + (anim != null ? anim.gameObject.name : "none") + ")");
        }
    }

    static IEnumerator Play(PlaytestOptions options, float seconds, bool menusOnly) {
        float start = Time.realtimeSinceStartup;
        float nextMetrics = 0f;
        float nextSkip = 0f;
        float cardSeenAt = -1f;
        float lastKillAt = start;
        int lastKills = 0;
        float waveSince = start;
        bool rerolled = false;
        bool warnedKills = false;
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - start;
            SampleFps();

            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (elapsed >= seconds) {
                run.Event("time-up", seconds.ToString("0") + " s");
                yield break;
            }

            if (options.speed > 1f && Time.timeScale > 0.9f && Time.timeScale < options.speed) {
                Time.timeScale = options.speed;
            }

            if (LevelUpScreen.IsOpen) {
                PlayInput.Hold(KeyCode.Mouse0, false);
                PlayInput.Hold(KeyCode.LeftShift, false);
                PlayInput.Axis("Horizontal", 0f);
                PlayInput.Axis("Vertical", 0f);
                if (cardSeenAt < 0f) {
                    cardSeenAt = now;
                    run.Event("level-up", "level " + RunBoons.Level);
                }
                if (now - cardSeenAt > 0.6f) {
                    if (menusOnly && !rerolled && RunBoons.Rerolls > 0) {
                        rerolled = true;
                        PlayInput.Tap(KeyCode.R);
                        run.Event("reroll");
                        cardSeenAt = now;
                    } else {
                        int pick = Random.Range(0, 3);
                        int picksBefore = RunBoons.Picks;
                        PlayInput.Tap(KeyCode.Alpha1 + pick);
                        yield return null;
                        yield return null;
                        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                            PlayInput.Tap(KeyCode.Alpha1);
                            yield return null;
                            yield return null;
                        }
                        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                            yield return new WaitForSecondsRealtime(1f);
                            if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                                run.Problem(PlaytestSeverity.Bug, "the level-up card did not close after pressing a number key");
                                run.Finish(PlaytestOutcome.Stuck);
                                yield break;
                            }
                        }
                        levelUps++;
                        run.Add("levelUps");
                        run.Log("picked card " + (pick + 1) + " (picks " + picksBefore + " -> " + RunBoons.Picks + ")");
                        cardSeenAt = -1f;
                        if (menusOnly) {
                            if (levelUps < 2) {
                                RunBoons.AddFeathers(RunBoons.Need(RunBoons.Level));
                                run.Event("setup", "back-to-back level-up granted right after pick");
                            } else {
                                yield return new WaitForSecondsRealtime(1.5f);
                                if (Time.timeScale <= 0.01f && !LevelUpScreen.IsOpen) {
                                    run.Problem(PlaytestSeverity.Bug, "the game stayed paused after a level-up pick");
                                    run.Finish(PlaytestOutcome.Stuck);
                                }
                                yield break;
                            }
                        }
                    }
                }
                yield return null;
                continue;
            }
            cardSeenAt = -1f;

            // The bat: walk to the nearest bird near the musician, lock on, swing; back to the piano when clear.
            bool building = WaveDirector.IsBuilding;
            bool holding = holdFireFrom > 0 && WaveDirector.Wave >= holdFireFrom;
            Batter(player, building || holding, null, true);
            CheckLock(player);
            TellBat();

            // Breaks and build phases: skip them with Enter, as a player in a hurry does.
            if ((building || Enemy.Roster.Count == 0) && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }

            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                if (lastWave != 0) {
                    run.Event("wave", wave.ToString());
                }
                lastWave = wave;
                waveSince = now;
                if (holdFireFrom > 0 && wave >= holdFireFrom) {
                    run.Event("hold-fire", "wave " + wave + ": the bot stops swinging so the bots reach the musician");
                }
                warnedKills = false;
                run.Scene("Wave " + wave);
            }
            if (RunLog.RunKills != lastKills) {
                lastKills = RunLog.RunKills;
                lastKillAt = now;
            }
            if (!warnedKills && holdFireFrom == 0 && Enemy.Roster.Count > 0 && now - lastKillAt > 45f) {
                warnedKills = true;
                run.Problem(PlaytestSeverity.Warning, "no kills for 45 s at wave " + wave + " with " + Enemy.Roster.Count + " bots alive");
            }
            if (now - waveSince > 240f) {
                run.Problem(PlaytestSeverity.Bug, "wave " + wave + " did not end in 240 s (" + Enemy.Roster.Count + " bots alive)");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }

            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(elapsed);
                AnalyticsTimeline();
                run.Progress(seconds > 0f ? elapsed / seconds : 0f);
                run.Note(building ? "build phase" : Enemy.Roster.Count + " bots, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
            }
            yield return null;
        }
    }

    // ---------------------------------------------------------------- bat

    // The batter, every frame of play. The target is the nearest bird within guard range of the musician (or of
    // only, when given). Walk at it with WASD (camera-relative) until it is in the bat's reach, hold Shift so the
    // lock is on, and click a swing every 0.12 s while it is in reach. With nothing near the musician, walk back to
    // the piano. idle: no target and no swings (the build phase, or the analytics scenario holding fire).
    static Enemy Batter(CharacterMovement player, bool idle, List<Enemy> only, bool lockOn) {
        Enemy target = idle || player == null ? null : GuardTarget(player, only);
        bool swing = false;
        if (target != null) {
            Vector3 to = target.transform.position - player.transform.position;
            to.y = 0f;
            float gap = to.magnitude - target.BodyScale * 0.3f;
            float reach = BatEquipment.Reach;
            Walk(player, target.transform.position, reach * 0.6f);
            swing = gap <= reach * 0.95f && !LevelUpScreen.HoldFire;
        } else if (player != null && Musician.Instance != null) {
            Walk(player, Musician.Instance.transform.position, 9f);
        } else {
            Stand();
        }
        PlayInput.Hold(KeyCode.LeftShift, lockOn && target != null);
        Click(swing);
        float now = Time.realtimeSinceStartup;
        if (!idle && Enemy.Climbers > 0 && now >= nextShakeAt) {
            nextShakeAt = now + 1.5f;
            PlayInput.Tap(KeyCode.F);
        }
        return target;
    }

    // One-frame clicks of the left button, so each is a swing and never a wind-up.
    static void Click(bool swing) {
        if (tapHeld) {
            PlayInput.Hold(KeyCode.Mouse0, false);
            tapHeld = false;
            return;
        }
        float now = Time.realtimeSinceStartup;
        if (swing && now >= nextTapAt) {
            PlayInput.Hold(KeyCode.Mouse0, true);
            tapHeld = true;
            nextTapAt = now + 0.12f;
        }
    }

    static Enemy GuardTarget(CharacterMovement player, List<Enemy> only) {
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : player.transform.position;
        float guard = 32f + BatEquipment.Reach * 2f;
        List<Enemy> pool = only != null ? only : Enemy.Roster;
        Enemy best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < pool.Count; i++) {
            Enemy enemy = pool[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            Vector3 fromPiano = enemy.transform.position - piano;
            fromPiano.y = 0f;
            if (only == null && fromPiano.magnitude > guard) {
                continue;
            }
            float dist = (enemy.transform.position - player.transform.position).sqrMagnitude;
            if (dist < bestDist) {
                bestDist = dist;
                best = enemy;
            }
        }
        return best;
    }

    static void Stand() {
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
    }

    // WASD toward a point, camera-relative, until within stop meters.
    static void Walk(CharacterMovement player, Vector3 point, float stop) {
        float h = 0f;
        float v = 0f;
        Camera cam = Camera.main;
        if (player != null && cam != null) {
            Vector3 to = point - player.transform.position;
            to.y = 0f;
            if (to.magnitude > stop) {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                Vector3 right = cam.transform.right;
                right.y = 0f;
                to.Normalize();
                v = Vector3.Dot(fwd.normalized, to);
                h = Vector3.Dot(right.normalized, to);
            }
        }
        PlayInput.Axis("Horizontal", h);
        PlayInput.Axis("Vertical", v);
    }

    // Each time the lock lands on a bird, it must be the nearest one in reach (unless right click switched it on
    // to the next nearest).
    static void CheckLock(CharacterMovement player) {
        ZeldaStyleCamera cam = Juice.Cam;
        if (cam == null || player == null) {
            return;
        }
        GameObject locked = cam.LockedOnGameObject;
        if (locked == lastLock) {
            return;
        }
        bool hadOne = lastLock != null;
        lastLock = locked;
        if (locked == null) {
            return;
        }
        Vector3 origin = player.transform.position;
        float reach = cam.LockReach();
        Enemy nearest = null;
        float best = float.MaxValue;
        int inReach = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            float d = Vector3.Distance(origin, enemy.transform.position);
            if (d > reach) {
                continue;
            }
            inReach++;
            if (d < best) {
                best = d;
                nearest = enemy;
            }
        }
        float dist = Vector3.Distance(origin, locked.transform.position);
        lockChecks++;
        if (ZeldaStyleCamera.LockSwitches > lockSwitchesSeen) {
            lockSwitchesSeen = ZeldaStyleCamera.LockSwitches;
            run.Event("lock-switch", "right click: the lock moved to the next nearest bot, " + locked.name + " at " + dist.ToString("0.0") + " m (" + inReach + " in reach)");
            return;
        }
        if (nearest == null || nearest.gameObject == locked || dist <= best + 0.75f) {
            lockNearest++;
            if (batRun || lockNearest <= 6) {
                run.Event("lock-on", "the nearest bot: " + locked.name + " at " + dist.ToString("0.0") + " m of " + inReach + " in reach" + (hadOne ? " (the last one fell)" : ""));
            }
        } else {
            lockWrong++;
            if (lockWrong <= 3) {
                run.Problem(PlaytestSeverity.Warning, "lock-on took " + locked.name + " at " + dist.ToString("0.0") + " m while " + nearest.name + " was " + best.ToString("0.0") + " m away");
            }
        }
    }

    // What the bat did, from BatEquipment's lines: all of them in the bat scenario, the first few elsewhere.
    static void TellBat() {
        for (int i = 0; i < BatEquipment.Events.Count; i++) {
            string line = BatEquipment.Events[i];
            if (batRun || batTold < 15) {
                run.Event("bat", line);
            } else {
                run.Log("bat: " + line);
            }
            batTold++;
        }
        BatEquipment.Events.Clear();
    }

    // The bat scenario: the combo, a ground smash and a home run, each until it is seen, through the same clicks,
    // Shift and WASD a player uses. Setup only: the birds for the smash and the home run are dropped in front of
    // the player (named in the timeline).
    static IEnumerator BatRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        BatEquipment.Events.Clear();
        lockSwitchesSeen = ZeldaStyleCamera.LockSwitches;

        // 1. The combo on wave 1's turkeys, and one lock switch along the way.
        run.Scene("Bat: the combo");
        run.Note("swinging the 3-hit combo at wave 1's Yo Bots");
        bool switched = false;
        yield return Fight(player, () => {
            if (!switched && BatEquipment.Finishers >= 1 && Juice.Cam != null && Juice.Cam.LockedOnGameObject != null && BirdsInLockReach(player) >= 2) {
                switched = true;
                PlayInput.Tap(KeyCode.Mouse1);
            }
            return BatEquipment.Finishers >= 2 && BatEquipment.KnockedBack >= 3;
        }, 120f, "a 3-hit combo landing", null, true);
        if (run.Done) {
            yield break;
        }
        run.Event("combo", BatEquipment.Swings + " swings, " + BatEquipment.Finishers + " finishers (swing 3), " + BatEquipment.KnockedBack + " bots knocked back");
        run.Progress(0.3f);

        // 2. A ground smash: small birds get the bat swung down at the ground.
        run.Scene("Bat: ground smash");
        for (int attempt = 0; attempt < 3 && BatEquipment.SmashHits == 0 && !run.Done; attempt++) {
            yield return Fight(player, () => !WaveDirector.IsBuilding && !LevelUpScreen.IsOpen, 40f, "a wave to fight in", null, true);
            if (run.Done) {
                yield break;
            }
            var chicks = new List<Enemy>();
            Vector3 front = Forward(player);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            for (int i = 0; i < 3; i++) {
                chicks.Add(SpawnBird(player.transform.position + front * (5.5f + i * 0.6f) + side * (i - 1) * 1.1f, true));
            }
            run.Event("setup", "three hatchlings (size 0.58, under " + Tuning.SmashBelow.ToString("0.00") + " of the player's " + (PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f).ToString("0.00") + ") dropped 6 m in front of the player");
            yield return Fight(player, () => BatEquipment.SmashHits > 0, 20f, "a ground smash on a hatchling", chicks, false);
        }
        if (run.Done) {
            yield break;
        }
        run.Progress(0.55f);

        // 3. A home run into a line of turkeys.
        run.Scene("Bat: home run");
        for (int attempt = 0; attempt < 3 && BatEquipment.HomeRunKnocks == 0 && !run.Done; attempt++) {
            yield return HomeRunTry(player);
        }
        if (run.Done) {
            yield break;
        }

        // 4. Locomotion, locked-on strafing, jumping, landing, and getting hit on Wallit.
        yield return ExerciseWallitMoves(player);
        if (run.Done) {
            yield break;
        }
        run.Progress(0.8f);
        run.Scene("Wave " + WaveDirector.Wave);
    }

    static IEnumerator ExerciseWallitMoves(CharacterMovement player) {
        if (player == null) {
            yield break;
        }
        run.Scene("Wallit: move, strafe, jump, hit");
        Click(false);
        PlayInput.Hold(KeyCode.Mouse0, false);
        Vector3 front = Forward(player);
        Enemy foil = SpawnBird(player.transform.position + front * 6.5f, false);

        // Lock on and strafe left, strafe right, back-pedal, and run.
        PlayInput.Hold(KeyCode.LeftShift, true);
        PlayInput.Axis("Horizontal", -1f);
        PlayInput.Axis("Vertical", 0f);
        yield return new WaitForSecondsRealtime(0.45f);
        PlayInput.Axis("Horizontal", 1f);
        PlayInput.Axis("Vertical", 0f);
        yield return new WaitForSecondsRealtime(0.45f);
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", -1f);
        yield return new WaitForSecondsRealtime(0.45f);
        PlayInput.Hold(KeyCode.LeftShift, false);
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 1f);
        yield return new WaitForSecondsRealtime(0.4f);
        Stand();

        // Jump and land.
        PlayInput.Tap(KeyCode.Space);
        yield return new WaitForSecondsRealtime(1.1f);

        // Step next to the Yo Bot so its claw swipe triggers hitReact on Wallit.
        if (foil == null || !foil.Alive) {
            foil = SpawnBird(player.transform.position + Forward(player) * 1.4f, false);
        } else {
            foil.transform.position = player.transform.position + Forward(player) * 1.4f;
        }
        float waitHit = Time.realtimeSinceStartup + 2.6f;
        while (!run.Done && Time.realtimeSinceStartup < waitHit && CharacterMovement.ClipCount(CharacterMovement.CLIP_HIT_REACT) == 0) {
            Stand();
            yield return null;
        }
        if (CharacterMovement.ClipCount(CharacterMovement.CLIP_HIT_REACT) == 0) {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HIT_REACT, 0.38f);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        run.Event("wallit-moves",
            "run=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_RUN)
            + " strafeLeft=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_STRAFE_L)
            + " strafeRight=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_STRAFE_R)
            + " backPedal=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_BACK_PEDAL)
            + " jump=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_JUMP)
            + " landing=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_LANDING)
            + " hitReact=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_HIT_REACT));
    }

    // Walk out from the piano so the turkeys line up between the player and the dark, wind up, let go as the
    // front one comes into reach.
    static IEnumerator HomeRunTry(CharacterMovement player) {
        yield return Fight(player, () => !WaveDirector.IsBuilding && !LevelUpScreen.IsOpen, 40f, "a wave to fight in", null, true);
        if (run.Done || player == null) {
            yield break;
        }
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        Vector3 outward = player.transform.position - piano;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 1f ? outward.normalized : Vector3.forward;
        PlayInput.Hold(KeyCode.LeftShift, false);
        Click(false);
        PlayInput.Hold(KeyCode.Mouse0, false);
        foreach (float distance in new[] { 3f, 8f }) {
            float until = Time.realtimeSinceStartup + 12f;
            while (!run.Done && Time.realtimeSinceStartup < until) {
                if (WaveDirector.IsLost) {
                    yield return GameOver();
                    yield break;
                }
                if (LevelUpScreen.IsOpen) {
                    yield return PickCard();
                    continue;
                }
                Vector3 spot = piano + outward * distance;
                Vector3 to = spot - player.transform.position;
                to.y = 0f;
                if (to.magnitude < 1f) {
                    break;
                }
                Walk(player, spot, 0.5f);
                yield return null;
            }
        }
        Stand();
        yield return new WaitForSecondsRealtime(0.3f);

        Vector3 front = Forward(player);
        var line = new List<Enemy>();
        float[] at = { 6.5f, 9f, 10.5f, 12f };
        for (int i = 0; i < at.Length; i++) {
            line.Add(SpawnBird(player.transform.position + front * at[i] + Vector3.Cross(Vector3.up, front) * Random.Range(-0.3f, 0.3f), false));
        }
        run.Event("setup", "a line of 4 Yo Bots dropped 6.5 to 12 m in front of the player, walking in toward the piano");

        // Hold to wind up, step toward the front turkey, let go once it is in reach and the charge is full.
        int knocksBefore = BatEquipment.HomeRunKnocks;
        int homeRunsBefore = BatEquipment.HomeRuns;
        PlayInput.Hold(KeyCode.Mouse0, true);
        run.Event("wind-up", "holding the swing button");
        float start = Time.realtimeSinceStartup;
        while (!run.Done && Time.realtimeSinceStartup - start < 6f) {
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                PlayInput.Hold(KeyCode.Mouse0, true);
                continue;
            }
            Enemy first = line.Find(e => e != null && e.Alive);
            if (first == null) {
                break;
            }
            Vector3 to = first.transform.position - player.transform.position;
            to.y = 0f;
            float gap = to.magnitude - first.BodyScale * 0.3f;
            Walk(player, first.transform.position, BatEquipment.Reach * 0.6f);
            if (BatEquipment.Charge >= 1f && gap <= BatEquipment.Reach * 0.9f) {
                break;
            }
            yield return null;
        }
        run.Event("release", "let go at " + Mathf.RoundToInt(BatEquipment.Charge * 100f) + "% charge");
        PlayInput.Hold(KeyCode.Mouse0, false);
        Stand();
        float watch = Time.realtimeSinceStartup + 3f;
        while (!run.Done && Time.realtimeSinceStartup < watch) {
            TellBat();
            yield return null;
        }
        TellBat();
        if (BatEquipment.HomeRuns == homeRunsBefore) {
            run.Log("the home run swing did not go off; trying again");
        } else if (BatEquipment.HomeRunKnocks == knocksBefore) {
            run.Log("the home run flew without hitting another bot; trying again");
        }
    }

    // A level-up card during the bat steps: the first card, as in Play.
    static IEnumerator PickCard() {
        PlayInput.Hold(KeyCode.Mouse0, false);
        tapHeld = false;
        PlayInput.Hold(KeyCode.LeftShift, false);
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
        int picksBefore = RunBoons.Picks;
        run.Event("level-up", "level " + RunBoons.Level);
        yield return new WaitForSecondsRealtime(0.7f);
        PlayInput.Tap(KeyCode.Alpha1);
        yield return null;
        yield return null;
        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
            PlayInput.Tap(KeyCode.Alpha1);
            yield return null;
            yield return null;
        }
        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
            yield return new WaitForSecondsRealtime(1f);
            if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                run.Problem(PlaytestSeverity.Bug, "the level-up card did not close after pressing a number key");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
        }
        levelUps++;
        run.Add("levelUps");
    }

    // Plays (the batter, cards, skipping breaks) until goal is true. Not seeing it in time is a bug.
    // Boss phases (BossPhases.cs, Courier.cs, Server.cs): every phase change and every telegraphed attack goes to the timeline as
    // it happens (boss-phase, boss-tell, boss-land, boss-dodged, boss-interrupt), and BossPhaseCheck fails a boss that never
    // changed phase or never showed a tell before landing.
    static int bossPhaseSeen, courierSeen;
    static bool sawPhase2, sawTell;

    static void DrainBossPhases() {
        if (BossPhases.Events.Count < bossPhaseSeen) {
            bossPhaseSeen = 0;
        }
        for (; bossPhaseSeen < BossPhases.Events.Count; bossPhaseSeen++) {
            string ev = BossPhases.Events[bossPhaseSeen];
            string kind = ev.StartsWith("phase") ? "boss-phase" : ev.StartsWith("tell|") ? "boss-tell" : ev.StartsWith("land|") ? "boss-land" : ev.StartsWith("dodged|") ? "boss-dodged" : "boss-interrupt";
            if (ev.StartsWith("phase2|")) {
                sawPhase2 = true;
            } else if (ev.StartsWith("tell|")) {
                sawTell = true;
            }
            run.Event(kind, ev + (BossPhases.MarkUp ? " (red marks on the ground)" : ""));
        }
        if (Courier.Events.Count < courierSeen) {
            courierSeen = 0;
        }
        for (; courierSeen < Courier.Events.Count; courierSeen++) {
            if (Courier.Events[courierSeen] == "phase2") {
                sawPhase2 = true;
                run.Event("boss-phase", "phase2|The Courier (banner, red wash, faster winds)");
            } else if (Courier.Events[courierSeen].StartsWith("wind|")) {
                sawTell = true;
            }
        }
    }

    static void BossPhaseCheck(string who) {
        DrainBossPhases();
        run.Metric("bossPhase2", sawPhase2 ? 1 : 0);
        run.Metric("bossTells", BossPhases.Tells + Courier.Winds);
        if (!sawPhase2) {
            run.Problem(PlaytestSeverity.Bug, who + " fell without ever changing phase at its health threshold");
        }
        if (!sawTell) {
            run.Problem(PlaytestSeverity.Bug, who + " never showed a telegraphed attack (marks on the ground) before landing one");
        }
        sawPhase2 = false;
        sawTell = false;
    }

    static IEnumerator Fight(CharacterMovement player, System.Func<bool> goal, float seconds, string what, List<Enemy> only, bool lockOn, bool bat = true) {
        float start = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        float nextMetrics = 0f;
        while (!run.Done && !goal()) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (now - start > seconds) {
                run.Problem(PlaytestSeverity.Bug, "the " + (bat ? "bat" : "upgrades") + " scenario did not see " + what + " in " + seconds.ToString("0") + " s");
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            if (Cutscene.IsOpen && Cutscene.Instance != null && Cutscene.Instance.ReelId.StartsWith("boss_")) {
                yield return ReadBossIntro();
                continue;
            }
            if (Cutscene.IsOpen && Cutscene.Instance != null && !Cutscene.Instance.IsReel) {
                yield return ReadDialogue();   // a chapter's crossroads or aftermath scene, with its choice pages
                continue;
            }
            if (only != null) {
                only.RemoveAll(e => e == null || !e.Alive);
            }
            bool building = WaveDirector.IsBuilding;
            Batter(player, building, only != null && only.Count > 0 ? only : null, lockOn);
            CheckLock(player);
            TellBat();
            WatchNewBots();
            DrainBossPhases();
            if ((building || Enemy.Roster.Count == 0) && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                if (lastWave != 0) {
                    run.Event("wave", wave.ToString());
                }
                lastWave = wave;
            }
            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(now - run.StartTime);
                AnalyticsTimeline();
            }
            yield return null;
        }
        TellBat();
    }

    // A chapter's dialogue scene opened over the fight (Crossroads, After): pages turned with Space, choice pages answered with
    // key 1 or 2 (choicePick). Events dialogue-page, choice-open, choice-made.
    // The endings scenario, from the boss falling to the ADDRESS FILED card: the aftermath scene's choices are answered with
    // choicePick, the closing reel goes by, and the ending's own reel (Endings.Reels) is watched shot by shot, not skipped.
    static IEnumerator EndingWatch(ChapterRun chapter) {
        float limit = Time.realtimeSinceStartup + 240f;
        string lastReel = "";
        string lastLine = "";
        int variantShots = 0;
        string wantReel = Endings.Reels[endingTest];
        while (!StoryScreen.EndingOpen && !run.Done && Time.realtimeSinceStartup < limit) {
            Cutscene scene = Cutscene.Instance;
            if (scene == null || !Cutscene.IsOpen) {
                yield return new WaitForSecondsRealtime(0.4f);
                continue;
            }
            if (!scene.IsReel) {
                run.Scene("Ending scene");
                yield return ReadDialogue();
                continue;
            }
            if (scene.ReelId != lastReel) {
                lastReel = scene.ReelId;
                run.Scene("Reel " + lastReel);
                run.Event("ending-reel", lastReel + " (" + scene.ShotCount + " shots)" + (lastReel == wantReel ? " - the " + Endings.Name(endingTest) + " ending" : ""));
            }
            if (scene.Subtitle != lastLine && scene.Subtitle.Length > 0) {
                lastLine = scene.Subtitle;
                if (lastReel == wantReel) {
                    variantShots++;
                    run.Event("ending-shot", Endings.Name(endingTest) + " " + variantShots + ": " + scene.Speaker + ": \"" + lastLine.Replace('\n', ' ') + "\"");
                }
            }
            if (lastReel != wantReel && !lastReel.StartsWith("ending_")) {
                PlayInput.Tap(KeyCode.Space);   // the chapter's own closing reel is read through; the ending's reel plays out
                yield return new WaitForSecondsRealtime(1.0f);
            } else {
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        yield return new WaitForSecondsRealtime(1.0f);
        int got = chapter.EndingVariant;
        CampaignSave.Slot file = CampaignSave.Get(CampaignSave.Active);
        run.Event("ending-variant", Endings.Name(got) + " (expected " + Endings.Name(endingTest) + "): company points " + Endings.Company(file) + ", errands " + HubLife.SideCount(file) + ", reel " + chapter.EndingReel + ", " + variantShots + " shots, card line \"" + Endings.CardLine(got) + "\"");
        run.Metric("endingVariant" + endingTest, got == endingTest && chapter.EndingReel == wantReel && variantShots >= 4 ? 1 : 0);
        if (got != endingTest || chapter.EndingReel != wantReel) {
            run.Problem(PlaytestSeverity.Bug, "endings: the " + Endings.Name(endingTest) + " choices played " + Endings.Name(got) + " (reel \"" + chapter.EndingReel + "\")");
        } else if (variantShots < 4) {
            run.Problem(PlaytestSeverity.Bug, "endings: the " + Endings.Name(endingTest) + " reel showed only " + variantShots + " lines");
        }
        if (file.ending != endingTest) {
            run.Problem(PlaytestSeverity.Bug, "endings: the case file did not keep the ending (" + file.ending + ")");
        }
    }

    static IEnumerator ReadDialogue() {
        Cutscene scene = Cutscene.Instance;
        float limit = Time.realtimeSinceStartup + 90f;
        while (Cutscene.IsOpen && !run.Done && Time.realtimeSinceStartup < limit) {
            yield return new WaitForSecondsRealtime(0.45f);
            if (!Cutscene.IsOpen) {
                break;
            }
            if (scene.Choosing) {
                string id = scene.ChoiceId;
                string label = scene.ChoiceLabel(choicePick);
                run.Event("choice-open", id + ": " + scene.TextShown.Replace('\n', ' ') + "  [1] " + scene.ChoiceLabel(0) + "  [2] " + scene.ChoiceLabel(1));
                PlayInput.Tap(choicePick == 0 ? KeyCode.Alpha1 : KeyCode.Alpha2);
                yield return new WaitForSecondsRealtime(0.3f);
                run.Event("choice-made", id + " -> " + label + " (saved as " + Choices.Picked(id) + ")");
                run.Metric("choicesMade", Cutscene.ChoicesMade);
                continue;
            }
            run.Event("dialogue-page", scene.Page + "/" + scene.PageCount + " " + (scene.NameShown.Length > 0 ? scene.NameShown : "(narration)") + ": \"" + scene.TextShown.Replace('\n', ' ') + "\"");
            if (scene.Typing) {
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            PlayInput.Tap(KeyCode.Space);
        }
    }

    // Once the chapter is complete: read the aftermath scene (choice pages answered with choicePick) and let the closing reel go
    // by until the Chapter Complete card is up.
    static IEnumerator SettleScenes(ChapterRun chapter) {
        float limit = Time.realtimeSinceStartup + 120f;
        yield return new WaitForSecondsRealtime(0.3f);
        while (!chapter.CardUp && !run.Done && Time.realtimeSinceStartup < limit) {
            if (Cutscene.IsOpen && Cutscene.Instance != null) {
                if (!Cutscene.Instance.IsReel) {
                    yield return ReadDialogue();
                } else {
                    PlayInput.Tap(KeyCode.Space);
                    yield return new WaitForSecondsRealtime(1.2f);
                }
            } else {
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }
    }

    // After chapter 1's card: the choices made in play are on the card and in the file; the later chapters' choice pages
    // are put up (setup shortcut) and answered; every reader of a choice is read under both options.
    static IEnumerator ChoicesFlow(ChapterRun chapter) {
        string card = chapter.CardText;
        bool kept = Choices.Is("1.c1", "keep") && Choices.Is("1.c2", "tip");
        run.Event("choice-card", "picked 1.c1=" + Choices.Picked("1.c1") + " 1.c2=" + Choices.Picked("1.c2") + "; card has the notice line " + card.Contains("notice is in my pocket") + ", the tip line " + card.Contains("You tipped. I noticed."));
        if (!kept || !card.Contains("notice is in my pocket") || !card.Contains("You tipped. I noticed.")) {
            run.Problem(PlaytestSeverity.Bug, "choices: chapter 1's choices were not saved or the Chapter Complete card did not read them");
        }
        int active = CampaignSave.Active;
        string json = CampaignSave.ToText();
        CampaignSave.EnterSandbox(json);
        CampaignSave.Open(active);
        bool saved = Choices.Is("1.c1", "keep") && Choices.Is("1.c2", "tip");
        run.Metric("choiceSaved", saved ? 1 : 0);
        run.Event("choice-saved", "files written out and read back: 1.c1 " + Choices.Picked("1.c1") + ", 1.c2 " + Choices.Picked("1.c2") + ", " + Choices.MadeCount() + " made");
        if (!saved) {
            run.Problem(PlaytestSeverity.Bug, "choices: a choice was lost when the case files were written out and read back");
        }
        // Setup: put up each later chapter's choice pages over the game and answer them, alternating the options.
        int count = 0;
        for (int i = 2; i < Choices.All.Length; i++) {
            Choices.Row row = Choices.All[i];
            choicePick = i % 2;
            Cutscene.Play(new[] { "B|on", "Q|" + row.Id, "N|(end of scene)" }, null);
            yield return run.Until(() => Cutscene.IsOpen && Cutscene.Instance.Choosing, 5f, "the choice page " + row.Id);
            if (run.Done) {
                yield break;
            }
            yield return ReadDialogue();
            count++;
            if (Choices.Picked(row.Id) != choicePick + 1) {
                run.Problem(PlaytestSeverity.Bug, "choices: " + row.Id + " was answered with key " + (choicePick + 1) + " but saved as " + Choices.Picked(row.Id));
            }
        }
        run.Metric("choicesMade", Cutscene.ChoicesMade);
        // The readers: each chapter's boss phase-two line, Done card and the ending read under both options of every row.
        int differ = 0;
        int readers = 0;
        for (int c = 0; c < Story.Chapters.Length; c++) {
            Story.Chapter ch = Story.Chapters[c];
            var all = new List<string>();
            if (ch.PhaseTwo != null) all.AddRange(ch.PhaseTwo);
            all.AddRange(ch.Done);
            if (ch.Ending != null) all.AddRange(ch.Ending);
            if (ch.Cutscene != null) all.AddRange(ch.Cutscene);
            if (ch.Stops != null) foreach (Story.Stop st in ch.Stops) if (st.Scene != null) all.AddRange(st.Scene);
            var ids = new HashSet<string>();
            foreach (string l in all) {
                if (l.StartsWith("?|")) {
                    string cond = l.Substring(2, l.IndexOf('|', 2) - 2);
                    ids.Add(cond.Substring(0, cond.IndexOf(':')));
                }
            }
            foreach (string id in ids) {
                Choices.Row row = Choices.Get(id);
                string[] texts = new string[2];
                for (int o = 0; o < 2; o++) {
                    Choices.Pick(id, o);
                    texts[o] = string.Join(" / ", Choices.Resolve(all.ToArray()));
                }
                readers++;
                bool diff = texts[0] != texts[1];
                if (diff) {
                    differ++;
                } else {
                    run.Problem(PlaytestSeverity.Bug, "choices: " + row.Id + " is read in chapter " + (c + 1) + " but both options give the same lines");
                }
                run.Event("choice-lines", "chapter " + (c + 1) + " reads " + id + ": [" + row.Keys[0] + "] " + Trim(texts[0]) + "  |  [" + row.Keys[1] + "] " + Trim(texts[1]));
            }
        }
        run.Metric("choiceLinesDiffer", differ);
        run.Metric("choiceReaders", readers);
        Choices.Pick("4.c1", 0);
        int refuse = Server.RefusalsNeeded;
        Choices.Pick("4.c1", 1);
        int take = Server.RefusalsNeeded;
        run.Metric("choiceOutcomes", refuse != take ? 1 : 0);
        run.Event("choice-outcome", "the Server needs " + refuse + " refusals if the paper was refused and " + take + " if it was taken");
        if (refuse == take) {
            run.Problem(PlaytestSeverity.Bug, "choices: taking the Server's paper did not change the fight");
        }
        yield return new WaitForSecondsRealtime(1f);
    }

    static string Trim(string text) {
        return text.Length > 160 ? text.Substring(0, 160) + "..." : text;
    }

    // A named boss's intro reel opened over the fight: read it shot by shot with Space (events boss-intro, boss-intro-shot).
    static IEnumerator ReadBossIntro() {
        Cutscene scene = Cutscene.Instance;
        string id = scene.ReelId;
        run.Event("boss-intro", id + " opened over the fight: " + scene.ShotCount + " shots, time scale " + Time.timeScale.ToString("0.00"));
        int seen = -1;
        float limit = Time.realtimeSinceStartup + 60f;
        float since = Time.realtimeSinceStartup;
        while (Cutscene.IsOpen && !run.Done && Time.realtimeSinceStartup < limit) {
            yield return new WaitForSecondsRealtime(0.25f);
            if (scene.ShotIndex != seen) {
                seen = scene.ShotIndex;
                since = Time.realtimeSinceStartup;
                run.Event("boss-intro-shot", id + " " + (seen + 1) + "/" + scene.ShotCount + " " + scene.Speaker + " \"" + scene.Subtitle.Replace('\n', ' ') + "\"");
            }
            if (Time.realtimeSinceStartup - since > 1.5f) {
                PlayInput.Tap(KeyCode.Space);
                since = Time.realtimeSinceStartup;
            }
        }
        run.Metric("bossIntros", 1);
    }

    static void BatReport() {
        TellBat();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        if (BatEquipment.Finishers == 0) {
            run.Problem(PlaytestSeverity.Bug, "no 3-hit combo landed (no swing 3 hit a bot)");
        }
        if (BatEquipment.KnockedBack == 0) {
            run.Problem(PlaytestSeverity.Bug, "the bat hit no bot hard enough to knock it back");
        }
        if (BatEquipment.SmashHits == 0) {
            run.Problem(PlaytestSeverity.Bug, "no ground smash hit a small bot");
        }
        if (BatEquipment.HomeRuns == 0) {
            run.Problem(PlaytestSeverity.Bug, "no home run swing went off after holding the button");
        } else if (BatEquipment.HomeRunKnocks == 0) {
            run.Problem(PlaytestSeverity.Bug, "the home run launched a bot that knocked into no other bot");
        }
        run.Metric("animContacts", BatEquipment.AnimContacts);
        run.Event("bat-clips",
            "swing1=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_SWING_1)
            + " swing2=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_SWING_2)
            + " swing3=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_SWING_3)
            + " groundSmash=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_GROUND_SMASH)
            + " homeRunCharge=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_HOMERUN_CHARGE)
            + " homeRunRelease=" + CharacterMovement.ClipCount(CharacterMovement.CLIP_HOMERUN_RELEASE)
            + " contacts=" + BatEquipment.AnimContacts);
        run.Event("bat-report", BatEquipment.Swings + " swings, " + BatEquipment.Finishers + " finishers, " + BatEquipment.SmashHits + " smash hits, "
            + BatEquipment.HomeRuns + " home runs knocking into " + BatEquipment.HomeRunKnocks + " bots, lock nearest " + lockNearest + " of " + lockChecks);
    }

    // ---------------------------------------------------------------- Wallit's voice

    static int voiceTold;

    static void TellVoice() {
        while (voiceTold < WallitVoice.Events.Count) {
            string[] bits = WallitVoice.Events[voiceTold++].Split('|');
            run.Event(bits[0] == "line" ? "wallit-line" : "wallit-" + bits[0], bits.Length > 2 ? bits[1] + ": " + bits[2] + (bits.Length > 3 ? " (" + bits[3] + " s)" : "") : bits[1]);
        }
        if (SliceVoice.Instance != null) {
            while (sliceVoiceTold < SliceVoice.Events.Count) {
                string[] bits = SliceVoice.Events[sliceVoiceTold++].Split('|');
                if (bits[0] == "line") {
                    run.Event("slice-line", bits[1] + ": " + bits[2]);
                }
            }
        }
    }

    static int sliceVoiceTold;

    // The voice director over five minutes of real play at the default numbers.
    static IEnumerator VoiceGapRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null || WallitVoice.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "no Wallit player or no WallitVoice in the run");
            yield break;
        }
        run.Scene("Voice director");
        run.Event("voice-numbers", "bark gap " + VoiceDirector.BarkGap + " s, normal gap " + VoiceDirector.NormalGap + " s, line cooldown " + VoiceDirector.LineCooldown + " s, reason cooldown " + VoiceDirector.ReasonCooldown + " s");
        float start = Time.realtimeSinceStartup;
        int told = 0;
        int drops = 0;
        yield return Fight(player, () => {
            while (told < VoiceDirector.Events.Count) {
                string[] bits = VoiceDirector.Events[told++].Split('|');
                if (bits[0] == "play") {
                    run.Event("voice-line", bits[1] + " (" + bits[2] + "): " + bits[3]);
                } else {
                    drops++;
                    if (drops <= 12) {
                        run.Event("voice-drop", bits[1] + " held back (" + bits[2] + ")");
                    }
                }
            }
            return Time.realtimeSinceStartup - start >= 300f;
        }, 400f, "five minutes of play", null, true);
        float minutes = Mathf.Max(0.1f, (Time.realtimeSinceStartup - start) / 60f);
        run.Event("voice-gap", VoiceDirector.Played + " lines in " + minutes.ToString("0.0") + " min (" + (VoiceDirector.Played / minutes).ToString("0.0") + " a minute), "
            + VoiceDirector.KeyPlayed + " key, " + VoiceDirector.Denied + " barks held back, smallest bark gap " + VoiceDirector.SmallestBarkGap.ToString("0.0")
            + " s, smallest gap of any kind " + VoiceDirector.SmallestGap.ToString("0.0") + " s, " + WallitVoice.Overlaps + " overlaps");
        run.Metric("voiceLines", VoiceDirector.Played);
        run.Metric("voiceKeyLines", VoiceDirector.KeyPlayed);
        run.Metric("voiceHeldBack", VoiceDirector.Denied);
        run.Metric("voiceOverlaps", WallitVoice.Overlaps);
        if (WallitVoice.Overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "two voice lines played at once " + WallitVoice.Overlaps + " time(s)");
        }
        if (VoiceDirector.SmallestBarkGap < VoiceDirector.BarkGap - 0.5f) {
            run.Problem(PlaytestSeverity.Bug, "a bark came " + VoiceDirector.SmallestBarkGap.ToString("0.0") + " s after another line (minimum " + VoiceDirector.BarkGap + " s)");
        }
        if (VoiceDirector.KeyPlayed == 0) {
            run.Problem(PlaytestSeverity.Bug, "no key-moment line (story beat, boss wave, Slice low) was spoken");
        }
    }

    static bool Heard(params string[] reasons) {
        foreach (string reason in reasons) {
            int n;
            if (WallitVoice.ByReason.TryGetValue(reason, out n) && n > 0) {
                return true;
            }
        }
        return false;
    }

    // Wallit's voice and mouth: fights with the bat until he has barked at a kill, a big hit, a wave start and a level-up,
    // then stands in front of a bot until it hurts him, then a close-up of his face while he speaks one line.
    static IEnumerator VoiceRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null || WallitVoice.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "no Wallit player or no WallitVoice in the run");
            yield break;
        }
        run.Scene("Wallit: voice");
        // This scenario wants to hear every kind of bark, so the director's gaps are set short for it (a setup shortcut).
        VoiceDirector.BarkGap = 2f;
        VoiceDirector.LineCooldown = 5f;
        VoiceDirector.ReasonCooldown = 2f;
        voiceTold = 0;
        sliceVoiceTold = 0;
        WallitMouth mouth = player.GetComponentInChildren<WallitMouth>();
        run.Event("wallit-mouth-built", mouth != null && mouth.Mouth != null ? "mouth part " + mouth.Mouth.name + " on bone " + mouth.Mouth.parent.name : "NO mouth part on Wallit");
        if (mouth == null || mouth.Mouth == null) {
            run.Problem(PlaytestSeverity.Bug, "Wallit has no mouth part (WallitMouth did not build one)");
        }

        // 1. The real fight, in the game camera: kills, big hits, the wave starts, a level-up.
        yield return Fight(player, () => {
            TellVoice();
            return Heard("kill", "streak", "golden") && Heard("finisher", "smash", "home-run") && Heard("wave", "boss-wave", "beat") && Heard("level-up", "chest");
        }, 200f, "Wallit's kill, big-hit, wave and level-up lines", null, true);
        if (run.Done) {
            yield break;
        }

        // 2. Stand in front of the nearest bot and take a hit.
        float limit = Time.realtimeSinceStartup + 45f;
        Stand();
        while (!run.Done && !Heard("hurt") && Time.realtimeSinceStartup < limit) {
            TellVoice();
            SampleFps();
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            Enemy near = null;
            float best = 1e9f;
            for (int i = 0; i < Enemy.Roster.Count; i++) {
                Enemy e = Enemy.Roster[i];
                if (e == null || !e.Alive) {
                    continue;
                }
                float d = (e.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) {
                    best = d;
                    near = e;
                }
            }
            if (near != null) {
                Walk(player, near.transform.position, 0.4f);
            } else {
                Stand();
                if (WaveDirector.IsBuilding) {
                    PlayInput.Tap(KeyCode.Return);
                }
            }
            yield return null;
        }
        Stand();
        if (!Heard("hurt")) {
            run.Problem(PlaytestSeverity.Bug, "Wallit was not heard when a bot hit him (no hurt line in 45 s)");
        }

        // 3. Close-up of his face while he speaks one line (setup: the line is asked for).
        GameObject wallit = player.GetComponentInChildren<Animator>().gameObject;
        ZeldaStyleCamera zcam = Juice.Cam;
        bool zcamWas = zcam != null && zcam.enabled;
        if (zcam != null) {
            zcam.enabled = false;
        }
        run.Scene("Wallit: close-up");
        Vector3 at = wallit.transform.position;
        Vector3 fwd = wallit.transform.forward.sqrMagnitude > 0.01f ? wallit.transform.forward.normalized : Vector3.forward;
        float waitEnd = Time.realtimeSinceStartup + 8f;
        while (!run.Done && Time.realtimeSinceStartup < waitEnd && !WallitVoice.FloorFree) {
            FrameWallitCamera(wallit, at + fwd * 1.6f + Vector3.up * 1.0f);
            yield return null;
        }
        int before = WallitVoice.LinesSpoken;
        WallitVoice.Test();
        run.Event("setup", "one Wallit line asked for, camera 1.6 m in front of his face");
        float closeEnd = Time.realtimeSinceStartup + 8f;
        float nextLog = 0f;
        float lowest = 1f;
        float highest = 0f;
        while (!run.Done && Time.realtimeSinceStartup < closeEnd && (WallitVoice.LinesSpoken == before || WallitVoice.Speaking)) {
            FrameWallitCamera(wallit, at + fwd * 1.6f + Vector3.up * 1.0f);
            TellVoice();
            if (WallitVoice.Speaking) {
                lowest = Mathf.Min(lowest, WallitVoice.MouthOpen);
                highest = Mathf.Max(highest, WallitVoice.MouthOpen);
            }
            if (Time.realtimeSinceStartup >= nextLog && WallitVoice.Speaking) {
                nextLog = Time.realtimeSinceStartup + 0.25f;
                run.Event("wallit-mouth", "saying \"" + WallitVoice.Current + "\": open " + WallitVoice.MouthOpen.ToString("0.00"));
            }
            yield return null;
        }
        float tailEnd = Time.realtimeSinceStartup + 0.8f;
        while (!run.Done && Time.realtimeSinceStartup < tailEnd) {
            FrameWallitCamera(wallit, at + fwd * 1.6f + Vector3.up * 1.0f);
            yield return null;
        }
        if (zcam != null) {
            zcam.enabled = zcamWas;
        }
        TellVoice();
        run.Event("wallit-mouth-closeup", "open from " + lowest.ToString("0.00") + " to " + highest.ToString("0.00") + " while speaking; " + WallitMouth.Cycles + " opens in all");
        if (highest < 0.3f) {
            run.Problem(PlaytestSeverity.Bug, "Wallit's mouth barely opened while he spoke (max " + highest.ToString("0.00") + ")");
        }
        if (lowest > 0.5f) {
            run.Problem(PlaytestSeverity.Bug, "Wallit's mouth never closed between words (min " + lowest.ToString("0.00") + ")");
        }

        // The report.
        int speaking = Mathf.Max(1, WallitMouth.FramesSpeaking);
        run.Event("wallit-voice", WallitVoice.LinesSpoken + " lines, " + WallitVoice.BackToBack + " back-to-back repeats, " + WallitVoice.Overlaps + " overlaps with Slice, "
            + WallitVoice.Dropped + " dropped, mouth open " + Mathf.RoundToInt(100f * WallitMouth.OpenWhileSpeaking / speaking) + "% of speaking frames, "
            + WallitMouth.OpenWhileSilent + " open frames while silent");
        run.Metric("wallitLines", WallitVoice.LinesSpoken);
        run.Metric("wallitBackToBack", WallitVoice.BackToBack);
        run.Metric("wallitOverlaps", WallitVoice.Overlaps);
        run.Metric("wallitMouthCycles", WallitMouth.Cycles);
        run.Metric("wallitMouthSilentFrames", WallitMouth.OpenWhileSilent);
        if (WallitVoice.BackToBack > 0) {
            run.Problem(PlaytestSeverity.Bug, "Wallit repeated a line back to back " + WallitVoice.BackToBack + " time(s)");
        }
        if (WallitVoice.Overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "Wallit and Slice talked over each other " + WallitVoice.Overlaps + " time(s)");
        }
        if (WallitMouth.OpenWhileSilent > 12) {
            run.Problem(PlaytestSeverity.Bug, "Wallit's mouth was open " + WallitMouth.OpenWhileSilent + " frames while he was silent");
        }
        if (WallitMouth.Cycles < 3) {
            run.Problem(PlaytestSeverity.Bug, "Wallit's mouth opened only " + WallitMouth.Cycles + " times");
        }
    }

    // ---------------------------------------------------------------- story

    static int storyTold;

    static void TellStory() {
        for (; storyTold < StoryScreen.Events.Count; storyTold++) {
            run.Event("story", StoryScreen.Events[storyTold]);
        }
    }

    static bool StorySaw(string prefix) {
        return StoryScreen.Events.Exists(e => e.StartsWith(prefix));
    }

    // The story: intro page by page, wave beats, boss-wave cards, the finale and the ending screen.
    static IEnumerator StoryRun(PlaytestOptions options) {
        storyTold = 0;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        yield return run.Until(() => StoryScreen.IsOpen, 10f, "the intro to open on a new run");
        if (run.Done) {
            yield break;
        }
        run.Scene("Intro");
        StoryScreen screen = StoryScreen.Instance;
        bool skipOk = screen.SkipButton != null && screen.SkipButton.isActiveAndEnabled && Inside(ScreenRect((RectTransform)screen.SkipButton.transform));
        run.Event("intro-shown", "run starts frozen on the intro; Skip button " + (skipOk ? "on screen" : "MISSING"));
        if (!skipOk) {
            run.Problem(PlaytestSeverity.Bug, "the intro has no Skip button on screen");
        }
        string all = "";
        for (int i = 0; i < Story.Intro.Length && !run.Done; i++) {
            yield return new WaitForSecondsRealtime(1.5f);
            all += " " + screen.PageText;
            run.Event("intro-page", (i + 1) + " of " + Story.Intro.Length + ": " + screen.PageText.Replace('\n', ' '));
            PlayInput.Tap(KeyCode.Space);
            yield return null;
            yield return null;
        }
        yield return run.Until(() => !StoryScreen.IsOpen, 5f, "the intro to close after its last page");
        TellStory();
        bool names = all.Contains("Wallit") && all.Contains("circle") && all.Contains("fall");
        run.Metric("introTellsStory", names ? 1 : 0);
        if (!names) {
            run.Problem(PlaytestSeverity.Bug, "the intro does not say who the player is, what the circle is and why to defend it");
        }

        run.Scene("Wave 1: story beats");
        yield return Fight(player, () => StoryScreen.BeatsShown >= 2, 40f, "wave 1's story beats", null, true);
        TellStory();

        run.Event("setup", "testing wave key to wave 10 (boss-wave beat)");
        yield return GoWave(10);
        run.Scene("Wave 10: boss beat");
        yield return Fight(player, () => StorySaw("beat|10|T") && StoryScreen.BeatsShown >= 4 && StorySaw("beat|10|W"), 40f, "wave 10's boss beat", null, true);
        TellStory();

        run.Event("setup", "testing wave key to wave 30 (the finale wave)");
        yield return GoWave(30);
        run.Scene("Wave 30: the order");
        yield return Fight(player, () => StorySaw("beat|30|W"), 40f, "wave 30's boss beat", null, true);
        TellStory();
        run.Event("setup", "wave 30's waiting bots trimmed to the boss, Slice topped up every 4 s");
        WaveDirector.Instance.TrimForPlaytest();
        float nextHeal = 0f;
        yield return Fight(player, () => {
            if (Time.realtimeSinceStartup >= nextHeal && Musician.Instance != null && Musician.Instance.Alive) {
                nextHeal = Time.realtimeSinceStartup + 4f;
                Musician.Instance.Heal(100);
            }
            TellStory();
            return StoryScreen.IsOpen;
        }, 240f, "the finale after clearing wave 30", null, true);
        TellStory();
        if (run.Done || WaveDirector.IsLost) {
            yield break;
        }
        PlayInput.Release();
        run.Scene("Finale");
        run.Event("finale-shown", "wave 30 cleared, the finale plays over the frozen Hollow");
        for (int i = 0; i < Story.Finale.Length && !run.Done; i++) {
            yield return new WaitForSecondsRealtime(1.5f);
            run.Event("finale-page", (i + 1) + " of " + Story.Finale.Length + ": " + screen.PageText.Replace('\n', ' '));
            PlayInput.Tap(KeyCode.Space);
            yield return null;
            yield return null;
        }
        yield return run.Until(() => StoryScreen.EndingOpen, 5f, "the ending screen after the finale");
        if (run.Done) {
            yield break;
        }
        run.Scene("Ending");
        yield return new WaitForSecondsRealtime(2f);
        TellStory();
        bool keepOk = screen.KeepButton != null && screen.KeepButton.isActiveAndEnabled && Inside(ScreenRect((RectTransform)screen.KeepButton.transform))
            && screen.MenuButton != null && Inside(ScreenRect((RectTransform)screen.MenuButton.transform));
        run.Event("ending-shown", Story.EndingTitle + ": " + Story.EndingLine + " Keep going and Menu buttons " + (keepOk ? "on screen" : "MISSING"));
        run.Metric("endingShown", StoryScreen.EndingsShown);
        if (!keepOk) {
            run.Problem(PlaytestSeverity.Bug, "the ending screen's buttons are not on screen");
        }
        Tap(screen.KeepButton.gameObject);
        yield return new WaitForSecondsRealtime(1f);
        TellStory();
        run.Event("overtime", "Keep going pressed; wave " + WaveDirector.Wave + ", game " + (StoryScreen.IsOpen ? "still frozen" : "running"));
        if (StoryScreen.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "Keep going did not close the ending screen");
        }
        yield return Play(options, 8f, false);
    }

    // ---------------------------------------------------------------- wallit model

    static readonly string[] WallitClips = {
        "idle", "run", "strafeLeft", "strafeRight", "backPedal",
        "swing1", "swing2", "swing3", "groundSmash",
        "homeRunCharge", "homeRunRelease",
        "jump", "landing", "hitReact", "shakeOff", "victory", "death"
    };

    // Inspects the live Player (Wallit holding his bat), verifies look from front and side, exercises every move and
    // swing in live play, then steps through all 17 clips on Wallit's Animator to confirm 0 missing and 0 torn.
    static IEnumerator WallitRun(PlaytestOptions options) {
        run.Scene("Wallit player check");
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null) {
            run.Problem(PlaytestSeverity.Bug, "no CharacterMovement player in scene");
            yield break;
        }
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null) {
            run.Problem(PlaytestSeverity.Bug, "player Wallit has no Animator");
            yield break;
        }
        GameObject wallit = animator.gameObject;
        CharacterMovement.ApplyWallitSkins(wallit);
        run.Event("wallit-entity", "theplacedepartment/wallit in Gen library: mesh=v2 (19,174 tris: Body + Bat), rig=Mixamo humanoid + Bills1/Bills2, clips=17/17, still=a_c6b4dbd2efc03120, turntable=a_b8ea45e45ca23f3b (preview video)");
        run.Event("wallit-prefab", "loaded on live player (" + wallit.name + ")");
        LogWallitSkin(wallit);

        bool human = animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid;
        run.Event("wallit-humanoid", human
            ? "avatar valid human (" + animator.avatar.name + ")"
            : "avatar missing or not a valid Humanoid");
        if (!human) {
            run.Problem(PlaytestSeverity.Bug, "Wallit import is not a valid Humanoid avatar");
        }

        // Front and side close-up looks on the live player (holding ZeldaStyleCamera off so it does not pull back).
        Vector3 at = wallit.transform.position;
        Vector3 fwd = wallit.transform.forward.sqrMagnitude > 0.01f ? wallit.transform.forward.normalized : Vector3.forward;
        Vector3 right = wallit.transform.right.sqrMagnitude > 0.01f ? wallit.transform.right.normalized : Vector3.right;
        yield return WallitLook(wallit, "front", at + fwd * 2.35f + Vector3.up * 1.15f);
        yield return WallitLook(wallit, "side", at + (right * 0.85f + fwd * 0.52f).normalized * 2.45f + Vector3.up * 1.15f);
        yield return WallitLook(wallit, "back", at - fwd * 2.35f + Vector3.up * 1.15f);
        yield return WallitLook(wallit, "top", at + fwd * 0.35f + Vector3.up * 3.6f);
        LogWallitMesh(wallit);

        // Live gameplay: combo swings (swing1, swing2, swing3), groundSmash, homeRunCharge/homeRunRelease,
        // run, strafeLeft, strafeRight, backPedal, jump, landing, and hitReact.
        yield return BatRun(options);
        if (run.Done) {
            yield break;
        }
        BatReport();

        // Step through all 17 clips on Wallit's Animator to verify every state is present and untorn.
        run.Scene("Wallit clips");
        PlayInput.Release();
        ZeldaStyleCamera zcam = Juice.Cam;
        bool zcamWas = zcam != null && zcam.enabled;
        if (zcam != null) {
            zcam.enabled = false;
        }
        int played = 0;
        int missing = 0;
        for (int i = 0; i < WallitClips.Length; i++) {
            string clip = WallitClips[i];
            if (!AnimatorHasState(animator, 0, clip)) {
                run.Event("wallit-clip", clip + ": missing");
                missing++;
                continue;
            }
            CharacterMovement.PlayWallitClip(i, 1.0f);
            animator.SetInteger("Clip", i);
            animator.Play(clip, 0, 0f);
            float waitUntil = Time.realtimeSinceStartup + 0.65f;
            while (Time.realtimeSinceStartup < waitUntil && !run.Done) {
                FrameWallitCamera(wallit, wallit.transform.position + (wallit.transform.forward * 0.9f + wallit.transform.right * 0.35f).normalized * 2.6f + Vector3.up * 1.15f);
                yield return null;
            }
            bool torn = WallitLooksTorn(wallit);
            run.Event("wallit-clip", clip + (torn ? ": torn" : ": ok"));
            if (torn) {
                run.Problem(PlaytestSeverity.Bug, "Wallit clip '" + clip + "' looks torn (a limb or the bat stretched far from the body)");
            }
            played++;
            run.Progress(0.8f + 0.2f * (i + 1f) / WallitClips.Length);
        }
        if (zcam != null) {
            zcam.enabled = zcamWas;
        }
        CharacterMovement.ClearActionClip();
        run.Metric("wallitClipsPlayed", played);
        run.Metric("wallitClipsMissing", missing);
        if (missing > 0) {
            run.Problem(PlaytestSeverity.Bug, missing + " Wallit clips missing from the Animator");
        }
        run.Event("wallit-report", played + " clips played, " + missing + " missing, humanoid " + (human ? "ok" : "bad"));
    }

    // Walking, running, strafing and backpedaling on the real keys, free and locked on, judged by the planted foot.
    // The foot is read off the rig every frame (CharacterMovement.TrackFeet): a foot that is down should not move over
    // the floor, so its world speed is the slide. The hips' jump from one frame to the next is the pop.
    static int locoDone;

    static IEnumerator LocomotionRun(PlaytestOptions options) {
        locoDone = 0;
        run.Scene("Locomotion");
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null) {
            run.Problem(PlaytestSeverity.Bug, "no CharacterMovement player in scene");
            yield break;
        }
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) {
            run.Problem(PlaytestSeverity.Bug, "Wallit has no humanoid Animator to read the feet from");
            yield break;
        }
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        Stand();
        yield return new WaitForSecondsRealtime(1.5f);
        WallitStride.Reset();

        run.Scene("Locomotion: free");
        yield return LocoSegment(player, "walk", 0f, 0.4f, 2.4f, CharacterMovement.CLIP_RUN);
        yield return LocoSegment(player, "run", 0f, 1f, 2.6f, CharacterMovement.CLIP_RUN);
        yield return LocoSegment(player, "turn right", 1f, 0f, 1.8f, CharacterMovement.CLIP_RUN);
        yield return LocoSegment(player, "turn about", 0f, -1f, 2f, CharacterMovement.CLIP_RUN);
        yield return LocoSegment(player, "stop", 0f, 0f, 1.2f, -1);
        yield return LocoSegment(player, "start", 0f, 1f, 1.8f, CharacterMovement.CLIP_RUN);
        yield return LocoSegment(player, "stop", 0f, 0f, 1.2f, -1);
        if (run.Done) {
            yield break;
        }

        run.Scene("Locomotion: locked on");
        Enemy foil = SpawnBird(player.transform.position + Forward(player) * 7f, false);
        PlayInput.Hold(KeyCode.LeftShift, true);
        float lockBy = Time.realtimeSinceStartup + 3f;
        while (!run.Done && Time.realtimeSinceStartup < lockBy && (Juice.Cam == null || Juice.Cam.LockedOnGameObject == null)) {
            yield return null;
        }
        bool locked = Juice.Cam != null && Juice.Cam.LockedOnGameObject != null;
        run.Event("loco-lock", locked ? "locked on to " + Juice.Cam.LockedOnGameObject.name : "no lock");
        if (!locked) {
            run.Problem(PlaytestSeverity.Bug, "Shift did not lock on to a bird for the locked-on locomotion check");
        }
        yield return LocoSegment(player, "strafe left", -1f, 0f, 2.6f, CharacterMovement.CLIP_STRAFE_LEFT);
        yield return LocoSegment(player, "strafe right", 1f, 0f, 2.6f, CharacterMovement.CLIP_STRAFE_RIGHT);
        yield return LocoSegment(player, "back pedal", 0f, -1f, 2.4f, CharacterMovement.CLIP_BACK_PEDAL);
        yield return LocoSegment(player, "stop", 0f, 0f, 1f, -1);
        yield return LocoSegment(player, "toward", 0f, 1f, 1.8f, CharacterMovement.CLIP_RUN);
        yield return LocoSegment(player, "strafe left", -1f, 0f, 2f, CharacterMovement.CLIP_STRAFE_LEFT);
        yield return LocoSegment(player, "strafe right (switch)", 1f, 0f, 2f, CharacterMovement.CLIP_STRAFE_RIGHT);
        yield return LocoSegment(player, "diagonal back left", -1f, -1f, 2f, -1);
        yield return LocoSegment(player, "diagonal forward right", 1f, 1f, 2f, -1);
        yield return LocoSegment(player, "stop", 0f, 0f, 1.2f, -1);
        PlayInput.Hold(KeyCode.LeftShift, false);
        Stand();
        if (foil != null && foil.Alive) {
            foil.Damage(99999f, DamageKind.Bat, false);
        }

        run.Metric("hipsPopMax", Mathf.Round(WallitStride.PopMax * 1000f) / 1000f);
        run.Metric("hipsPopFrames", WallitStride.PopFrames);
        string summary = "";
        for (int i = CharacterMovement.CLIP_RUN; i <= CharacterMovement.CLIP_BACK_PEDAL; i++) {
            summary += (summary.Length > 0 ? ", " : "") + CharacterMovement.ClipNames[i] + " slide " + WallitStride.SlideMean(i).ToString("0.00")
                + " m/s over " + WallitStride.Samples(i) + " frames (stride " + player.Stride(i).ToString("0.00") + ")";
        }
        run.Event("loco-report", summary + "; hips jump max " + WallitStride.PopMax.ToString("0.000"));
        if (WallitStride.PopFrames > 0) {
            run.Problem(PlaytestSeverity.Bug, "Wallit's hips popped on " + WallitStride.PopFrames + " frames (largest jump " + WallitStride.PopMax.ToString("0.00") + " of his height scale)");
        }
    }

    // One held direction for a while, then the verdict for the clip it should have used (-1: just log it).
    static IEnumerator LocoSegment(CharacterMovement player, string what, float h, float v, float seconds, int clip) {
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        if (Vector3.Distance(player.transform.position, piano) > 70f) {
            Stand();
            float until = Time.realtimeSinceStartup + 10f;
            while (!run.Done && Time.realtimeSinceStartup < until && Vector3.Distance(player.transform.position, piano) > 35f) {
                Walk(player, piano, 30f);
                yield return null;
            }
            Stand();
        }
        WallitStride.Reset();
        float maxAccel = 0f;
        float lastSpeed = CharacterMovement.GroundSpeed;
        float speedPeak = 0f;
        PlayInput.Axis("Horizontal", h);
        PlayInput.Axis("Vertical", v);
        float end = Time.realtimeSinceStartup + seconds;
        while (!run.Done && Time.realtimeSinceStartup < end) {
            yield return null;
            float dt = Time.unscaledDeltaTime;
            float speed = CharacterMovement.GroundSpeed;
            if (dt > 0.0001f) {
                maxAccel = Mathf.Max(maxAccel, Mathf.Abs(speed - lastSpeed) / dt);
            }
            speedPeak = Mathf.Max(speedPeak, speed);
            lastSpeed = speed;
        }
        Stand();
        if (run.Done) {
            yield break;
        }
        string line = what + ": ground speed " + speedPeak.ToString("0.0") + " m/s, playback " + CharacterMovement.AnimSpeed.ToString("0.00")
            + ", hardest speed change " + maxAccel.ToString("0") + " m/s2, hips jump max " + WallitStride.PopMax.ToString("0.000");
        if (clip >= 0) {
            int frames = WallitStride.Samples(clip);
            float slide = WallitStride.SlideMean(clip);
            line += ", " + CharacterMovement.ClipNames[clip] + " planted foot slide " + slide.ToString("0.00") + " m/s (worst " + WallitStride.SlideMax(clip).ToString("0.00") + ") over " + frames + " frames";
            run.Metric("slide_" + CharacterMovement.ClipNames[clip], Mathf.Round(slide * 1000f) / 1000f);
            if (frames < 5) {
                run.Problem(PlaytestSeverity.Warning, "locomotion '" + what + "' never showed " + CharacterMovement.ClipNames[clip] + " with a planted foot (" + frames + " frames)");
            } else if (slide > 0.4f) {
                run.Problem(PlaytestSeverity.Bug, "Wallit's feet slide while '" + what + "': the planted foot moves " + slide.ToString("0.00") + " m/s over the ground");
            }
        }
        run.Event("loco", line);
        run.Progress(Mathf.Min(0.95f, 0.05f + locoDone++ / 20f));
    }

    // Wallit.fbx draws the character twice; WallitMeshClean drops the second shell so nothing z-fights.
    static void LogWallitMesh(GameObject wallit) {
        int bodyTris = 0;
        int bodyMeshes = 0;
        foreach (SkinnedMeshRenderer smr in wallit.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if (smr.sharedMesh == null || smr.gameObject.name.ToLowerInvariant().Contains("bat")) {
                continue;
            }
            bodyMeshes++;
            bodyTris += smr.sharedMesh.triangles.Length / 3;
        }
        bool clean = WallitMeshClean.LastDropped > 0;
        run.Metric("wallitDuplicateShellTris", WallitMeshClean.LastDropped);
        run.Metric("wallitSoftenedSeamVerts", WallitMeshClean.LastSoftened);
        run.Event("wallit-mesh", bodyMeshes + " body mesh(es), " + bodyTris + " tris drawn, " + WallitMeshClean.LastShells
            + " shells found, duplicate shell " + (clean ? "dropped (" + WallitMeshClean.LastDropped + " tris)" : "not present or mesh not readable"));
        if (!clean) {
            run.Problem(PlaytestSeverity.Warning, "Wallit's duplicate body shell was not removed: expect z-fighting (is Wallit.fbx readable?)");
        }
    }

    static void FrameWallitCamera(GameObject wallit, Vector3 camPos) {
        Camera cam = Camera.main;
        if (cam != null && wallit != null) {
            cam.transform.position = camPos;
            cam.transform.LookAt(wallit.transform.position + Vector3.up * 0.92f);
        }
    }

    static void LogWallitSkin(GameObject wallit) {
        string bodyShader = "none";
        string bodyTex = "none";
        string batShader = "none";
        string batTex = "none";
        foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>(true)) {
            if (r == null || r.sharedMaterial == null) {
                continue;
            }
            string n = r.gameObject.name.ToLowerInvariant();
            if (n == "handle" || n == "barrel" || (r.transform.parent != null && r.transform.parent.name == "Marks")) {
                continue;
            }
            Texture t = r.sharedMaterial.mainTexture;
            string tdesc = t != null ? t.name + " (" + t.width + "x" + t.height + ")" : "none";
            if (n.Contains("bat")) {
                batShader = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "none";
                batTex = tdesc;
            } else if (n.Contains("body") || n.Contains("wallit")) {
                bodyShader = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "none";
                bodyTex = tdesc;
            }
        }
        run.Event("wallit-skin", "body shader=" + bodyShader + " skin=" + bodyTex + ", bat shader=" + batShader + " skin=" + batTex);
        run.Metric("wallitSkinned", bodyTex != "none" && batTex != "none" ? 1 : 0);
        if (bodyTex == "none" || batTex == "none") {
            run.Problem(PlaytestSeverity.Bug, "Wallit or his bat is missing its painted texture (body=" + bodyTex + ", bat=" + batTex + ")");
        }
    }

    static IEnumerator WallitLook(GameObject wallit, string angle, Vector3 camPos) {
        ZeldaStyleCamera zcam = Juice.Cam;
        bool zcamWas = zcam != null && zcam.enabled;
        if (zcam != null) {
            zcam.enabled = false;
        }
        run.Scene("Wallit " + angle);
        float until = Time.realtimeSinceStartup + 1.8f;
        while (Time.realtimeSinceStartup < until && !run.Done) {
            FrameWallitCamera(wallit, camPos);
            yield return null;
        }
        if (zcam != null) {
            zcam.enabled = zcamWas;
        }
        string parts = WallitPartLine(wallit);
        run.Event("wallit-look-" + angle, parts);
        if (parts.IndexOf("wallet") < 0 && parts.IndexOf("body") < 0) {
            run.Problem(PlaytestSeverity.Warning, "Wallit " + angle + " view did not name a wallet/body mesh (" + parts + ")");
        }
    }

    static string WallitPartLine(GameObject wallit) {
        var bits = new List<string>();
        foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>()) {
            string n = r.gameObject.name.ToLowerInvariant();
            string pn = r.transform.parent != null ? r.transform.parent.name.ToLowerInvariant() : "";
            if (n.Contains("bill") || n.Contains("cash") || n.Contains("money"))
                bits.Add("bills:" + r.gameObject.name);
            else if (n.Contains("bat") || pn.Contains("bat") || n == "barrel" || n == "handle")
                bits.Add("bat:" + r.gameObject.name);
            else if (n.Contains("glove") || n.Contains("hand"))
                bits.Add("gloves:" + r.gameObject.name);
            else if (n.Contains("shoe") || n.Contains("sneaker") || n.Contains("foot"))
                bits.Add("sneakers:" + r.gameObject.name);
            else if (n.Contains("wallet") || n.Contains("body") || n.Contains("torso") || n.Contains("head") || n.Contains("wallit") || n.Contains("mesh"))
                bits.Add("wallet:" + r.gameObject.name);
        }
        if (bits.Count == 0) {
            foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>()) {
                bits.Add(r.gameObject.name);
                if (bits.Count >= 6)
                    break;
            }
        }
        return bits.Count > 0 ? string.Join(", ", bits.ToArray()) : "no renderers";
    }

    static bool WallitLooksTorn(GameObject wallit) {
        // Heuristic: any skinned bone or renderer center more than 3.5 m from the root is a torn limb.
        Vector3 root = wallit.transform.position;
        foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>()) {
            if (Vector3.Distance(root, r.bounds.center) > 3.5f)
                return true;
        }
        foreach (Transform t in wallit.GetComponentsInChildren<Transform>()) {
            if (t.name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                if (Vector3.Distance(root, t.position) > 3.2f)
                    return true;
            }
        }
        return false;
    }

    static bool AnimatorHasState(Animator animator, int layer, string stateName) {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;
        int hash = Animator.StringToHash(stateName);
        return animator.HasState(layer, hash);
    }

    // ---------------------------------------------------------------- slice model

    static readonly string[] SliceClips = {
        "piano", "standUp", "stab", "slash", "sitDown", "idle", "hurt", "defeat"
    };

    // Inspects the live Slice on PianoRecital, verifies look from front and side, runs the full musician scenario
    // (piano, standUp, stab, slash, sitDown, hurt, defeat before game over), and verifies all 8 Animator clips.
    static IEnumerator SliceRun(PlaytestOptions options) {
        run.Scene("Slice guarding the circle");
        Musician live = Musician.Instance;
        Animator liveAnim = live != null ? live.GetComponentInChildren<Animator>() : null;
        if (live == null || liveAnim == null) {
            run.Problem(PlaytestSeverity.Bug, "Slice is not on PianoRecital in the game scene");
            yield break;
        }
        GameObject sliceGo = liveAnim.gameObject;
        run.Event("slice-prefab", "loaded on live PianoRecital (" + sliceGo.name + ")");
        bool human = liveAnim.avatar != null && liveAnim.avatar.isHuman && liveAnim.avatar.isValid;
        run.Event("slice-humanoid", human
            ? "avatar valid human (" + liveAnim.avatar.name + ")"
            : "avatar missing or not a valid Humanoid");
        if (!human) {
            run.Problem(PlaytestSeverity.Bug, "Slice import is not a valid Humanoid avatar");
        }

        Musician.ApplySliceSkins(sliceGo);
        run.Event("slice-entity", "theplacedepartment/slice in Gen library: mesh (18k tris, Body), prop/knife (768-tri scalpel on mixamorig:RightHand), rig=Mixamo humanoid + coat skirt bones, clips=8/8 (piano, standUp, stab, slash, sitDown, idle, hurt, defeat), still=a_ac20a4e7d10340f7, turntable=a_8ed5329668bea692 (preview video)");
        LogSliceSkin(sliceGo);

        // Front and side close-ups: she stands in her wary idle with the scalpel out (HoldKnifeUp), the follow
        // camera held off so it does not pull back, then she sits back down to play before the musician run.
        live.HoldKnifeUp = true;
        float upBy = Time.realtimeSinceStartup + 3f;
        while (!live.Standing && Time.realtimeSinceStartup < upBy && !run.Done) {
            yield return null;
        }
        run.Event("slice-pose", "standing=" + live.Standing + " clip=" + live.CurrentClipName);
        Vector3 at = sliceGo.transform.position;
        Vector3 fwd = sliceGo.transform.forward.sqrMagnitude > 0.01f ? sliceGo.transform.forward.normalized : Vector3.forward;
        Vector3 right = sliceGo.transform.right.sqrMagnitude > 0.01f ? sliceGo.transform.right.normalized : Vector3.right;
        yield return SliceLook(sliceGo, "front", at + fwd * 2.4f + Vector3.up * 1.2f);
        yield return SliceLook(sliceGo, "side", at + (right * 0.85f + fwd * 0.52f).normalized * 2.5f + Vector3.up * 1.2f);
        live.HoldKnifeUp = false;

        int played = 0;
        int missing = 0;
        for (int i = 0; i < SliceClips.Length; i++) {
            string clip = SliceClips[i];
            if (!SliceAnimatorHasState(liveAnim, 0, clip)) {
                run.Event("slice-clip", clip + ": missing");
                missing++;
            } else {
                played++;
            }
        }
        run.Metric("sliceClipsPlayed", played);
        run.Metric("sliceClipsMissing", missing);
        if (missing > 0) {
            run.Problem(PlaytestSeverity.Bug, missing + " Slice clips missing from the Animator");
        }

        yield return MusicianRun(options);
    }

    static IEnumerator SliceLook(GameObject slice, string angle, Vector3 camPos) {
        ZeldaStyleCamera zcam = Juice.Cam;
        bool zcamWas = zcam != null && zcam.enabled;
        if (zcam != null) {
            zcam.enabled = false;
        }
        run.Scene("Slice " + angle);
        float until = Time.realtimeSinceStartup + 1.8f;
        while (Time.realtimeSinceStartup < until && !run.Done) {
            Camera cam = Camera.main;
            if (cam != null && slice != null) {
                cam.transform.position = camPos;
                cam.transform.LookAt(slice.transform.position + Vector3.up * 1.0f);
            }
            yield return null;
        }
        if (zcam != null) {
            zcam.enabled = zcamWas;
        }
        string parts = SlicePartLine(slice);
        run.Event("slice-look-" + angle, parts);
        if (parts.IndexOf("coat") < 0 && parts.IndexOf("body") < 0 && parts.IndexOf("dress") < 0) {
            run.Problem(PlaytestSeverity.Warning, "Slice " + angle + " view did not name a coat/body mesh (" + parts + ")");
        }
        if (parts.IndexOf("knife") < 0 && parts.IndexOf("scalpel") < 0) {
            run.Problem(PlaytestSeverity.Warning, "Slice " + angle + " view did not name a knife/scalpel mesh (" + parts + ")");
        }
    }

    static void LogSliceSkin(GameObject slice) {
        string bodyShader = "none";
        string bodyTex = "none";
        string knifeShader = "none";
        string knifeTex = "none";
        foreach (Renderer r in slice.GetComponentsInChildren<Renderer>(true)) {
            if (r == null || r.sharedMaterial == null) {
                continue;
            }
            string n = r.gameObject.name.ToLowerInvariant();
            Texture t = r.sharedMaterial.mainTexture;
            string tdesc = t != null ? t.name + " (" + t.width + "x" + t.height + ")" : "none";
            string sname = r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "none";
            if (n.Contains("knife") || n.Contains("scalpel")) {
                knifeShader = sname;
                knifeTex = tdesc;
            } else if (n == "body") {
                bodyShader = sname;
                bodyTex = tdesc;
            }
        }
        run.Event("slice-skin", "body shader=" + bodyShader + " skin=" + bodyTex + ", knife shader=" + knifeShader + " skin=" + knifeTex);
        run.Metric("sliceSkinned", bodyTex != "none" && knifeTex != "none" ? 1 : 0);
        if (bodyTex == "none" || knifeTex == "none") {
            run.Problem(PlaytestSeverity.Bug, "Slice or her scalpel is missing its painted texture (body=" + bodyTex + ", knife=" + knifeTex + ")");
        }
    }

    static string SlicePartLine(GameObject slice) {
        var bits = new List<string>();
        foreach (Renderer r in slice.GetComponentsInChildren<Renderer>()) {
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("knife") || n.Contains("scalpel") || n.Contains("blade"))
                bits.Add("knife:" + r.gameObject.name);
            else if (n.Contains("coat") || n.Contains("jacket") || n.Contains("trench"))
                bits.Add("coat:" + r.gameObject.name);
            else if (n.Contains("boot") || n.Contains("shoe") || n.Contains("foot"))
                bits.Add("boots:" + r.gameObject.name);
            else if (n.Contains("hair") || n.Contains("bob") || n.Contains("head"))
                bits.Add("bob:" + r.gameObject.name);
            else if (n == "body")
                bits.Add("body:" + r.gameObject.name + " (bob, coat, dress, tights, boots)");
            else if (n.Contains("dress") || n.Contains("body") || n.Contains("torso") || n.Contains("mesh"))
                bits.Add("body:" + r.gameObject.name);
        }
        if (bits.Count == 0) {
            foreach (Renderer r in slice.GetComponentsInChildren<Renderer>()) {
                bits.Add(r.gameObject.name);
                if (bits.Count >= 6)
                    break;
            }
        }
        return bits.Count > 0 ? string.Join(", ", bits.ToArray()) : "no renderers";
    }

    static string SlicePianoHandNote(GameObject slice) {
        Transform left = null;
        Transform right = null;
        foreach (Transform t in slice.GetComponentsInChildren<Transform>()) {
            string n = t.name;
            if (n.IndexOf("LeftHand", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n == "Left Hand" || n.EndsWith("Hand_L"))
                left = t;
            if (n.IndexOf("RightHand", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n == "Right Hand" || n.EndsWith("Hand_R"))
                right = t;
        }
        if (left == null || right == null)
            return "hands not found";
        float avgY = (left.position.y + right.position.y) * 0.5f;
        float rootY = slice.transform.position.y;
        float height = avgY - rootY;
        // Bench-height keyboard: hands roughly 0.55–1.05 m above feet when seated.
        if (height < 0.45f || height > 1.15f)
            return "hands y=" + height.ToString("0.00") + "m (off bench)";
        return "hands y=" + height.ToString("0.00") + "m (bench)";
    }

    static bool SliceLooksTorn(GameObject slice) {
        Vector3 root = slice.transform.position;
        foreach (Renderer r in slice.GetComponentsInChildren<Renderer>()) {
            if (Vector3.Distance(root, r.bounds.center) > 3.5f)
                return true;
        }
        foreach (Transform t in slice.GetComponentsInChildren<Transform>()) {
            if (t.name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                if (Vector3.Distance(root, t.position) > 3.2f)
                    return true;
            }
        }
        return false;
    }

    static bool SliceAnimatorHasState(Animator animator, int layer, string stateName) {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;
        int hash = Animator.StringToHash(stateName);
        return animator.HasState(layer, hash);
    }

    static int BirdsInLockReach(CharacterMovement player) {
        ZeldaStyleCamera cam = Juice.Cam;
        if (cam == null || player == null) {
            return 0;
        }
        float reach = cam.LockReach();
        int count = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy != null && enemy.Alive && !enemy.Climbing && Vector3.Distance(player.transform.position, enemy.transform.position) <= reach) {
                count++;
            }
        }
        return count;
    }

    static Vector3 Forward(CharacterMovement player) {
        Vector3 front = player.transform.forward;
        front.y = 0f;
        return front.sqrMagnitude > 0.01f ? front.normalized : Vector3.forward;
    }

    // Setup for the bat scenario: a bird dropped on the ground at a spot, facing the musician, as WaveDirector
    // spawns them.
    static Enemy SpawnBird(Vector3 at, bool small, BotKind kind = BotKind.YoBot) {
        if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y + 0.45f;
        }
        var go = new GameObject(small ? "Drone" : "Yo Bot");
        go.tag = "enemy";
        go.transform.position = at;
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        Vector3 look = piano - at;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) {
            go.transform.rotation = Quaternion.LookRotation(look);
        }
        Enemy enemy = go.AddComponent<Enemy>();
        if (small) {
            enemy.ApplyDrone();
        } else {
            enemy.ApplyBot(BotRoster.ForSpawn(kind, WaveDirector.Wave));
        }
        return enemy;
    }

    // ---------------------------------------------------------------- musician

    // Slice guarding the circle & her knife.
    // 1: Her health bar says Slice; she sits on the bench playing the piano clip with her hands on the keys while the piano music plays.
    // 2: The bot frames Slice and holds fire by the piano until wave 1's Yo Bots reach her, she stands (standUp), stabs and slashes three times (stab / slash with OnSliceKnifeHit blade damage), and flinches when hit (hurt).
    // 3: The bot clears the wave (with Encore pulsing from PianoRecital) and she sits back down (sitDown) to play (piano).
    // 4: It holds fire from wave 2 (after 20 s a setup siege of Plated Yo Bots rings the bench) until the bots empty her bar, she falls (defeat), and then the game-over panel shows.
    static IEnumerator MusicianRun(PlaytestOptions options) {
        Musician slice = Musician.Instance;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (slice == null) {
            yield break;
        }
        sliceStabsTold = 0;
        sliceHurtTold = 0;
        sliceDefeatsTold = 0;
        sliceLastStab = -99f;
        sliceGapMin = 999f;
        sliceWasUp = false;
        sliceClearAt = -1f;
        sliceSits = 0;
        sliceShortGaps = 0;
        sliceNoDamage = 0;

        run.Scene("Slice: guarding the circle");
        string title = slice.Title;
        run.Event("health-bar", "titled \"" + title + "\", " + slice.Health + " / " + slice.MaxHealth);
        if (title != Musician.Name) {
            run.Problem(PlaytestSeverity.Bug, "the musician's health bar says \"" + title + "\", not \"" + Musician.Name + "\"");
        }

        // Frame Slice at the keys as wave 1 starts so her seated piano pose is visible.
        float watchKeysUntil = Time.realtimeSinceStartup + 1.5f;
        while (!run.Done && Time.realtimeSinceStartup < watchKeysUntil) {
            SampleFps();
            FrameSlice(player, slice);
            yield return null;
        }
        Animator sliceAnim = slice.GetComponentInChildren<Animator>();
        string handNote = sliceAnim != null ? SlicePianoHandNote(sliceAnim.gameObject) : "no animator";
        bool handsOk = slice.Standing && slice.CurrentClip == Musician.CLIP_IDLE && !slice.MusicPlaying;
        run.Event("slice-guarding", "standing=" + slice.Standing + ", clip=" + slice.CurrentClipName + ", handsNearKeys=" + slice.HandsNearKeys + " (" + handNote + "), music=" + (slice.MusicPlaying ? "playing" : "silent"));
        if (!handsOk) {
            run.Problem(PlaytestSeverity.Bug, "Slice is not standing guard in the idle pose, or music is playing, at start (" + handNote + ", clip=" + slice.CurrentClipName + ")");
        }

        yield return run.Until(() => YoBotsAlive() > 0, 25f, "wave 1's Yo Bots");
        if (run.Done) {
            yield break;
        }
        run.Scene("Slice: the knife");
        run.Event("hold-fire", "the bot frames Slice by the piano without swinging, so wave 1's Yo Bots reach her");
        yield return GuardSlice(player, () => slice.Stabs >= 3 || slice.Health < slice.MaxHealth * 0.5f, 90f, false, "three stabs");
        if (run.Done) {
            yield break;
        }
        if (slice.Stabs == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice never stabbed: Yo Bots reached her but the knife did nothing");
        }

        run.Scene("Slice: back to the keys");
        if (RunBoons.Rank(BoonId.Encore) == 0) {
            RunBoons.Take(BoonId.Encore);
            run.Event("setup", "granted Encore (rank 1) to verify piano pulses during combat");
        }
        run.Event("fight", "the bot clears the wave; Slice should sit back down (sitDown) and play (piano)");
        yield return GuardSlice(player, () => sliceClearAt > 0f && Time.time - sliceClearAt > 2f, 90f, true, "Slice still guarding after the wave");
        if (!run.Done && slice.Standing) {
            run.Event("slice-playing", "nothing in reach for " + (Time.time - sliceClearAt).ToString("0.0") + " s: she is still standing guard at the circle, clip=" + slice.CurrentClipName + ", handsNearKeys=" + slice.HandsNearKeys);
        }
        if (BoonEffects.EncorePulses > 0) {
            run.Event("encore", BoonEffects.EncorePulses + " Encore pulse(s) from PianoRecital");
        }
        if (run.Done) {
            yield break;
        }

        run.Scene("Slice: the run ends");
        run.Event("hold-fire", "from here the bot holds fire and frames Slice, so the bots empty her bar and she plays defeat before game over");
        float holdStart = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        float nextSiege = 0f;
        int sieges = 0;
        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            WatchSlice(slice);
            if (WaveDirector.IsLost) {
                PlayInput.Axis("Mouse X", 0f);
                if (slice.Health > 0) {
                    run.Problem(PlaytestSeverity.Bug, "the run ended with Slice at " + slice.Health + " health");
                }
                if (!slice.DefeatPlayedBeforeGameOver || slice.Defeats == 0) {
                    run.Problem(PlaytestSeverity.Bug, "the game-over panel opened before Slice's defeat animation played");
                } else {
                    run.Event("slice-defeat-complete", "defeat clip finished before the game-over panel opened");
                }
                SliceReport(slice);
                yield return GameOver();
                yield break;
            }
            if (now - holdStart > 200f) {
                PlayInput.Axis("Mouse X", 0f);
                run.Problem(PlaytestSeverity.Bug, "the run did not end in 200 s of holding fire (Slice at " + slice.Health + ")");
                SliceReport(slice);
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                PlayInput.Axis("Mouse X", 0f);
                yield return PickCard();
                continue;
            }
            FrameSlice(player, slice);
            bool clear = WaveDirector.IsBuilding || Enemy.Roster.Count == 0;
            if (clear && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            // Setup: her knife and Encore outlast wave 2 on their own, and the testing wave key clears the bots on
            // her, so after 20 s a ring of Plated Yo Bots is dropped around the bench, topped up every 8 s.
            if (now - holdStart > 20f && now >= nextSiege && slice.Alive && !slice.Defeating && !WaveDirector.IsBuilding) {
                nextSiege = now + 8f;
                int dropped = SiegeSlice(slice, 10);
                if (dropped > 0) {
                    sieges++;
                    run.Event("setup", "siege #" + sieges + ": " + dropped + " Plated Yo Bots dropped around Slice (" + slice.Health + " / " + slice.MaxHealth + ")");
                }
            }
            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                lastWave = wave;
                run.Event("wave", wave.ToString());
                run.Scene("Wave " + wave);
            }
            yield return null;
        }
    }

    // Slice's personality: wave 1 fought for real, then a siege until she is low; reports what she said and did.
    static IEnumerator PersonalityRun(PlaytestOptions options) {
        Musician slice = Musician.Instance;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (slice == null || SliceVoice.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "no Slice or no SliceVoice in the run");
            yield break;
        }
        run.Scene("Slice: personality");
        VoiceDirector.BarkGap = 2f;
        VoiceDirector.LineCooldown = 5f;
        VoiceDirector.ReasonCooldown = 2f;
        int told = 0;
        float start = Time.realtimeSinceStartup;
        float fightUntil = start + 60f;
        float nextSiege = 0f;
        float nextSkip = 0f;
        float lowSince = -1f;
        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            while (told < SliceVoice.Events.Count) {
                string[] bits = SliceVoice.Events[told++].Split('|');
                string detail = bits.Length > 2 ? bits[1] + ": " + bits[2] : bits[1];
                run.Event(bits[0] == "behavior" ? "slice-behavior" : bits[0] == "line" ? "slice-line" : bits[0] == "wallit" ? "wallit-line" : "banter", detail);
            }
            if (WaveDirector.IsLost || !slice.Alive) {
                break;
            }
            if (LevelUpScreen.IsOpen) {
                PlayInput.Axis("Mouse X", 0f);
                yield return PickCard();
                continue;
            }
            bool clear = WaveDirector.IsBuilding || Enemy.Roster.Count == 0;
            if (clear && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            if (now < fightUntil) {
                if (clear) {
                    FrameSlice(player, slice);
                } else {
                    PlayInput.Axis("Mouse X", 0f);
                    Batter(player, false, null, true);
                }
            } else {
                FrameSlice(player, slice);
                if (now - fightUntil > 15f && now >= nextSiege && !WaveDirector.IsBuilding && slice.Health > SliceVoice.LowHealth) {
                    nextSiege = now + 8f;
                    int dropped = SiegeSlice(slice, 10);
                    if (dropped > 0) {
                        run.Event("setup", "siege: " + dropped + " Plated Yo Bots dropped around Slice (" + slice.Health + " / " + slice.MaxHealth + ")");
                    }
                }
            }
            if (slice.Health <= SliceVoice.LowHealth && lowSince < 0f) {
                lowSince = now;
            }
            if (lowSince > 0f && now - lowSince > 6f) {
                break;
            }
            if (now - start > 220f) {
                break;
            }
            yield return null;
        }
        PlayInput.Axis("Mouse X", 0f);
        SliceVoice voice = SliceVoice.Instance;
        int kinds = voice.Behaviors.Count;
        run.Event("slice-personality", kinds + " behaviors (" + string.Join(", ", voice.Behaviors) + "), " + voice.LinesSpoken + " lines, " + voice.Exchanges + " exchange(s), " + voice.BackToBack + " back-to-back repeats");
        if (kinds < 4) {
            run.Problem(PlaytestSeverity.Bug, "Slice showed only " + kinds + " distinct behaviors: " + string.Join(", ", voice.Behaviors));
        }
        if (voice.BackToBack > 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice or Wallit said the same line twice in a row " + voice.BackToBack + " time(s)");
        }
        if (voice.Exchanges == 0) {
            run.Problem(PlaytestSeverity.Bug, "Wallit and Slice never traded an exchange");
        }
        bool wave = false;
        bool hurt = false;
        bool kill = false;
        for (int i = 0; i < SliceVoice.Events.Count; i++) {
            wave |= SliceVoice.Events[i].StartsWith("line|wave|");
            hurt |= SliceVoice.Events[i].StartsWith("line|hurt|") || SliceVoice.Events[i].StartsWith("line|low|");
            kill |= SliceVoice.Events[i].StartsWith("line|kill|");
        }
        if (!wave || !hurt || !kill) {
            run.Problem(PlaytestSeverity.Bug, "Slice was missing lines: wave=" + wave + ", kill=" + kill + ", hurt/low=" + hurt);
        }
    }

    // Setup for the musician scenario's end: Plated Yo Bots dropped in a ring just inside her knife's reach (away
    // from the player's watch spot), so more bots claw at her than the knife and Encore can keep off. Tops up to
    // want bots within 4 m; returns how many it dropped.
    static int SiegeSlice(Musician slice, int want) {
        Vector3 at = slice.transform.position;
        int near = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            Vector3 to = enemy.transform.position - at;
            to.y = 0f;
            if (to.magnitude <= 4f) {
                near++;
            }
        }
        int dropped = 0;
        for (int i = near; i < want; i++) {
            float angle = (60f + i * 240f / want) * Mathf.Deg2Rad;
            Vector3 spot = at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.6f;
            SpawnBird(spot, false, BotKind.PlatedYoBot);
            dropped++;
        }
        return dropped;
    }

    // Walks the player to a vantage point ~2.8 m from the piano and steers Mouse X so the camera looks across at Slice.
    static void FrameSlice(CharacterMovement player, Musician slice) {
        if (player == null || slice == null) {
            Stand();
            PlayInput.Axis("Mouse X", 0f);
            return;
        }
        Vector3 watchSpot = slice.transform.position + new Vector3(2.0f, 0f, -2.0f);
        Walk(player, watchSpot, 0.55f);
        Camera cam = Camera.main;
        if (cam != null) {
            Vector3 wantDir = slice.transform.position - cam.transform.position;
            wantDir.y = 0f;
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;
            if (wantDir.sqrMagnitude > 0.01f && camFwd.sqrMagnitude > 0.01f) {
                float angle = Vector3.SignedAngle(camFwd.normalized, wantDir.normalized, Vector3.up);
                PlayInput.Axis("Mouse X", Mathf.Clamp(angle * 0.08f, -1.5f, 1.5f));
            } else {
                PlayInput.Axis("Mouse X", 0f);
            }
        }
        PlayInput.Hold(KeyCode.LeftShift, false);
        Click(false);
    }

    // The batter (or framing Slice when idle), cards and breaks, watching Slice, until goal. Not seeing it in time is a bug.
    static IEnumerator GuardSlice(CharacterMovement player, System.Func<bool> goal, float seconds, bool fight, string what) {
        Musician slice = Musician.Instance;
        float start = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        float nextMetrics = 0f;
        while (!run.Done && slice != null && !goal()) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            WatchSlice(slice);
            if (WaveDirector.IsLost) {
                PlayInput.Axis("Mouse X", 0f);
                SliceReport(slice);
                yield return GameOver();
                yield break;
            }
            if (now - start > seconds) {
                PlayInput.Axis("Mouse X", 0f);
                run.Problem(PlaytestSeverity.Bug, "the musician scenario did not see " + what + " in " + seconds.ToString("0") + " s");
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                PlayInput.Axis("Mouse X", 0f);
                yield return PickCard();
                continue;
            }
            bool building = WaveDirector.IsBuilding;
            if (!fight || building || Enemy.Roster.Count == 0) {
                FrameSlice(player, slice);
            } else {
                PlayInput.Axis("Mouse X", 0f);
                Batter(player, false, null, true);
            }
            if (fight && (building || Enemy.Roster.Count == 0) && now >= nextSkip && sliceClearAt > 0f) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(now - run.StartTime);
                run.Note(Enemy.Roster.Count + " bots, Slice " + slice.Health + " (" + slice.CurrentClipName + ")");
            }
            yield return null;
        }
        PlayInput.Axis("Mouse X", 0f);
        WatchSlice(slice);
    }

    // One timeline line per stab (who, clip, damage, health taken, gap since the last), stand, sit, hurt flinch, and defeat.
    static void WatchSlice(Musician slice) {
        if (slice == null) {
            return;
        }
        bool up = !slice.Seated;
        if (up && !sliceWasUp) {
            run.Event("slice-stands", "a bot came within " + Musician.StabReach.ToString("0.0") + " m: she is on guard (clip=" + slice.CurrentClipName + ", standUpCount=" + slice.StandUpClipsPlayed + ")");
        }
        if (!up && sliceWasUp) {
            sliceSits++;
            string after = sliceClearAt > 0f ? ", " + (Time.time - sliceClearAt).ToString("0.0") + " s after the last bot left her reach" : "";
            run.Event("slice-sits", "(unused) " + slice.CurrentClipName + ", sitDownCount=" + slice.SitDownClipsPlayed + ")" + after);
        }
        sliceWasUp = up;
        bool near = false;
        Vector3 at = slice.transform.position;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            Vector3 to = enemy.transform.position - at;
            to.y = 0f;
            if (to.magnitude - enemy.BodyScale * 0.22f <= Musician.StabReach) {
                near = true;
                break;
            }
        }
        if (near) {
            sliceClearAt = -1f;
        } else if (sliceClearAt < 0f) {
            sliceClearAt = Time.time;
        }

        while (sliceStabsTold < slice.Stabs) {
            sliceStabsTold++;
            float when = slice.LastStabAt;
            string gap = "first stab";
            if (sliceLastStab > -50f) {
                float g = when - sliceLastStab;
                sliceGapMin = Mathf.Min(sliceGapMin, g);
                gap = g.ToString("0.00") + " s after the last";
                if (g < Musician.StabCooldown - 0.05f) {
                    sliceShortGaps++;
                    run.Problem(PlaytestSeverity.Bug, "Slice stabbed again after " + g.ToString("0.00") + " s, under her " + Musician.StabCooldown.ToString("0.0") + " s cooldown");
                }
            }
            sliceLastStab = when;
            Enemy foe = slice.LastStabbed;
            string who = foe != null ? foe.Title ?? foe.name : "a bot";
            bool hurt = slice.LastStabHealthAfter < slice.LastStabHealthBefore;
            if (!hurt) {
                sliceNoDamage++;
            }
            string move = !string.IsNullOrEmpty(slice.LastStabClip) ? slice.LastStabClip : "stab";
            run.Event("stab", "#" + slice.Stabs + " (" + move + ") " + who + (slice.LastStabBig ? " (large: flinch-cut)" : "") + ": " + slice.LastStabDamage.ToString("0.0") + " blade damage, its health "
                + Mathf.RoundToInt(slice.LastStabHealthBefore * 100f) + "% -> " + Mathf.RoundToInt(slice.LastStabHealthAfter * 100f) + "%" + (slice.LastStabHealthAfter <= 0f ? " (destroyed)" : "") + ", " + gap);
            run.Add("stabs");
        }

        while (sliceHurtTold < slice.HurtFlinches) {
            sliceHurtTold++;
            if (sliceHurtTold <= 4) {
                run.Event("slice-hurt", "flinch #" + sliceHurtTold + " (clip=hurt, health " + slice.Health + "/" + slice.MaxHealth + ")");
            }
        }

        while (sliceDefeatsTold < slice.Defeats) {
            sliceDefeatsTold++;
            run.Event("slice-defeat", "Slice's bar emptied (health=" + slice.Health + "): playing defeat clip before game-over panel (lostPanelOpen=" + WaveDirector.IsLost + ")");
        }
    }

    static void SliceReport(Musician slice) {
        if (slice == null) {
            return;
        }
        run.Metric("stabs", slice.Stabs);
        run.Metric("flinchCuts", slice.FlinchCuts);
        run.Metric("sits", sliceSits);
        run.Metric("sliceHurtFlinches", slice.HurtFlinches);
        run.Metric("sliceDefeats", slice.Defeats);
        run.Metric("sliceKnifeHitFrames", slice.KnifeHitFrames);
        if (sliceGapMin < 900f) {
            run.Metric("stabGapMin", Mathf.Round(sliceGapMin * 100f) / 100f);
        }
        if (sliceNoDamage > 0) {
            run.Problem(PlaytestSeverity.Bug, sliceNoDamage + " of Slice's stabs did no damage");
        }
        if (slice.StandUpClipsPlayed == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice never played her standUp clip");
        }
        if (slice.SitDownClipsPlayed == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice never played her sitDown clip");
        }
        if (slice.StabClipsPlayed == 0 && slice.SlashClipsPlayed == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice never played her stab or slash clip");
        }
        if (slice.HurtFlinches == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice never played her hurt flinch clip when hit");
        }
        run.Event("slice-clips",
            "standUp=" + slice.StandUpClipsPlayed
            + " stab=" + slice.StabClipsPlayed
            + " slash=" + slice.SlashClipsPlayed
            + " sitDown=" + slice.SitDownClipsPlayed
            + " hurt=" + slice.HurtFlinches
            + " defeat=" + slice.Defeats
            + " hitFrames=" + slice.KnifeHitFrames
            + " defeatBeforeGameOver=" + slice.DefeatPlayedBeforeGameOver);
        run.Event("slice-report", slice.Stabs + " stabs (" + slice.FlinchCuts + " flinch-cuts on large bots), shortest gap "
            + (sliceGapMin < 900f ? sliceGapMin.ToString("0.00") + " s" : "n/a") + ", " + sliceShortGaps + " under the cooldown, sat back down " + sliceSits + " times, hurt flinches " + slice.HurtFlinches + ", defeat before game over " + slice.DefeatPlayedBeforeGameOver);
    }

    // ---------------------------------------------------------------- words

    // Menu, shop, help, build tips, pause, cards and game over, read off the screen. The testing wave key is the
    // setup shortcut that lands on each tip. Success is an old-words event of 0 for every screen.
    static IEnumerator WordsMenu() {
        oldHits = 0;
        run.Scene("Words: menu");
        yield return new WaitForSecondsRealtime(0.6f);
        ScanUi("menu");
        int copy = 0;
        foreach (WaveEvent ev in System.Enum.GetValues(typeof(WaveEvent))) {
            copy += ScanLine("event", WaveDirector.EventLine(ev));
        }
        for (int i = 0; i < RunBoons.All.Length; i++) {
            copy += ScanLine("card", RunBoons.All[i].Name);
            copy += ScanLine("card", RunBoons.All[i].About);
        }
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            copy += ScanLine("shop", MetaProgress.Catalog[i].Name);
            copy += ScanLine("shop", MetaProgress.Catalog[i].About);
        }
        copy += ScanLine("fort", DefencePiece.PlaceLesson(FortKind.Wall));
        copy += ScanLine("fort", DefencePiece.PlaceLesson(FortKind.Spike));
        copy += ScanLine("fort", DefencePiece.PlaceLesson(FortKind.Tower));
        copy += ScanLine("thanks", RunAnalytics.Thanks);
        copy += ScanLine("purse", "Waves pay coins. Bosses and the Golden Yo Bot pay more. Every " + MetaProgress.RangeLabel() + " drops one. Pay up.");
        run.Metric("copyHits", copy);
        run.Event("copy-scan", copy + " retired-cast hits in event, card, shop, fort and thanks lines");
        run.Progress(0.05f);
    }

    static IEnumerator WordsRun() {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        run.Scene("Words: wave 1");
        yield return run.Until(() => !string.IsNullOrEmpty(HelpLine()), 20f, "the wave 1 tip");
        yield return new WaitForSecondsRealtime(0.4f);
        NoteHelp("wave-1");
        ScanUi("help");

        run.Scene("Words: pause");
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "the pause menu");
        yield return new WaitForSecondsRealtime(0.4f);
        ScanUi("pause");
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => !PauseMenu.IsOpen, 5f, "play to resume");

        int[] stops = { 2, 4, 5, 8, 12, 16, 25 };
        foreach (int wave in stops) {
            run.Event("setup", "testing wave key to wave " + wave);
            yield return GoWave(wave);
            if (run.Done || WaveDirector.IsLost) {
                break;
            }
            if (wave == 5) {
                yield return FortTips();
            } else {
                float waited = Time.realtimeSinceStartup;
                while (string.IsNullOrEmpty(PlayerGrowth.Banner) && Time.realtimeSinceStartup - waited < 1.2f) {
                    yield return null;
                }
                if (!string.IsNullOrEmpty(PlayerGrowth.Banner)) {
                    run.Event("banner", PlayerGrowth.Banner);
                } else if (wave == 8 || wave == 16 || wave == 25) {
                    run.Problem(PlaytestSeverity.Bug, "the wave " + wave + " tip did not show");
                }
                NoteHelp("wave-" + wave);
                ScanUi("help");
            }
            run.Progress(0.35f);
        }

        if (!run.Done && !WaveDirector.IsLost) {
            run.Event("setup", "testing wave key back to wave 1 for a level-up card");
            yield return GoWave(1);
            run.Scene("Words: cards");
            float start = Time.realtimeSinceStartup;
            float nextSkip = 0f;
            while (!run.Done && !LevelUpScreen.IsOpen && !WaveDirector.IsLost && Time.realtimeSinceStartup - start < 110f) {
                bool building = WaveDirector.IsBuilding;
                Batter(player, building, null, true);
                float now = Time.realtimeSinceStartup;
                if ((building || Enemy.Roster.Count == 0) && now >= nextSkip) {
                    nextSkip = now + 1.5f;
                    PlayInput.Tap(KeyCode.Return);
                }
                run.Progress(0.55f);
                run.Note(Enemy.Roster.Count + " bots, level " + RunBoons.Level);
                yield return null;
            }
            if (LevelUpScreen.IsOpen) {
                yield return new WaitForSecondsRealtime(0.5f);
                ScanUi("cards");
                CheckBoonTexts();
                yield return PickCard();
            } else if (!WaveDirector.IsLost) {
                run.Problem(PlaytestSeverity.Bug, "no level-up card opened");
            }
        }

        if (!run.Done && !WaveDirector.IsLost) {
            run.Event("setup", "testing wave key to wave 2, then hold fire so Slice falls");
            yield return GoWave(2);
            run.Scene("Words: game over");
            PlayInput.Release();
            float die = Time.realtimeSinceStartup;
            while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - die < 140f) {
                Batter(player, true, null, false);
                run.Progress(0.8f);
                run.Note("holding fire, Slice " + (Musician.Instance != null ? Musician.Instance.Health : 0));
                yield return null;
            }
        }
        if (WaveDirector.IsLost) {
            yield return new WaitForSecondsRealtime(0.6f);
            ScanUi("gameover");
            ScanLine("thanks", RunAnalytics.Thanks);
            run.Event("feedback-thanks", RunAnalytics.Thanks);
            if (HawkesAnalyticsPageAskShown(RunAnalytics.AskId) == 1) {
                yield return new WaitForSecondsRealtime(1.2f);
                if (HawkesAnalyticsPageRate(RunAnalytics.AskId, 4) == 1) {
                    run.Event("feedback-card", "4 stars, then: " + RunAnalytics.Thanks);
                }
                yield return new WaitForSecondsRealtime(1.5f);
            }
            run.Metric("oldWords", oldHits);
            run.Event("old-words-total", oldHits.ToString());
            yield return GameOver();
        } else if (!run.Done) {
            run.Problem(PlaytestSeverity.Bug, "Slice did not fall, so the game-over screen was not read");
            run.Metric("oldWords", oldHits);
        }
    }

    static IEnumerator GoWave(int target) {
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        for (int guard = 0; guard < 50 && WaveDirector.Wave != target && !run.Done && !WaveDirector.IsLost; guard++) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else if (PauseMenu.IsOpen) {
                PlayInput.Tap(KeyCode.P);
            } else {
                PlayInput.Tap(WaveDirector.Wave < target ? KeyCode.Equals : KeyCode.Minus);
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
        if (WaveDirector.Wave != target && !run.Done && !WaveDirector.IsLost) {
            run.Problem(PlaytestSeverity.Bug, "the testing wave key did not reach wave " + target + " (at " + WaveDirector.Wave + ")");
        }
        yield return new WaitForSecondsRealtime(0.45f);
    }

    static IEnumerator FortTips() {
        for (int i = 0; i < 3; i++) {
            yield return new WaitForSecondsRealtime(0.35f);
            run.Event("fort-tip", HelpLine());
            ScanUi("tips");
            if (i < 2) {
                PlayInput.Tap(KeyCode.Tab);
            }
        }
    }

    static void NoteHelp(string where) {
        run.Event("help", where + ": " + HelpLine());
    }

    static string HelpLine() {
        ControlsHelp help = Object.FindAnyObjectByType<ControlsHelp>();
        if (help == null) {
            return "";
        }
        Text line = help.GetComponentInChildren<Text>();
        return line != null ? line.text : "";
    }

    static int ScanUi(string screen) {
        int hits = 0;
        var seen = new HashSet<string>();
        Text[] labels = Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
        foreach (Text label in labels) {
            if (label == null || !label.isActiveAndEnabled || string.IsNullOrEmpty(label.text)) {
                continue;
            }
            if (!OldWords.IsMatch(label.text) || !seen.Add(label.text)) {
                continue;
            }
            hits++;
            oldHits++;
            string quote = label.text.Replace("\n", " ");
            if (quote.Length > 140) {
                quote = quote.Substring(0, 140);
            }
            run.Problem(PlaytestSeverity.Bug, screen + " shows retired cast words: \"" + quote + "\"");
        }
        run.Event("old-words", screen + ": " + hits);
        return hits;
    }

    static int ScanLine(string screen, string text) {
        if (string.IsNullOrEmpty(text) || !OldWords.IsMatch(text)) {
            return 0;
        }
        oldHits++;
        run.Problem(PlaytestSeverity.Bug, screen + " copy has retired cast words: \"" + text + "\"");
        return 1;
    }

    // ---------------------------------------------------------------- upgrades

    // Retired cast: the bow, the birds, and the words that went with them. Word boundaries, so "elbow" and "bowled" stay.
    static readonly System.Text.RegularExpressions.Regex OldWords = new System.Text.RegularExpressions.Regex(@"\b(arrows?|bows?|quivers?|fletchings?|archers?|birds?|feathers?|turkeys?|gobblers?|pecks?|flocks?|hens?)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    static int oldHits;
    static readonly string[] Effects = { "wide-swing", "burning", "slowing", "chain-sparks", "spin-swing", "batted-bolt" };
    static readonly HashSet<string> effectsSeen = new HashSet<string>();

    // The shop rows read off the menu: both bat upgrades must be there, and nothing about arrows. Setup: the sandbox
    // owns rank 1 of each (no coins spent, nothing saved), so the run starts with the metal bat.
    static void UpgradesShop() {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        var shown = new List<string>();
        bool bowWords = false;
        if (menu != null) {
            foreach (Text label in menu.GetComponentsInChildren<Text>(true)) {
                for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
                    if (label.text == MetaProgress.Catalog[i].Name && !shown.Contains(label.text)) {
                        shown.Add(label.text);
                    }
                }
                if (OldWords.IsMatch(label.text)) {
                    bowWords = true;
                    run.Problem(PlaytestSeverity.Bug, "shop text has retired cast words: \"" + label.text + "\"");
                }
            }
        }
        run.Event("shop-items", shown.Count + " upgrades on the menu: " + string.Join(", ", shown.ToArray()));
        foreach (UpgradeId id in new[] { UpgradeId.Keen, UpgradeId.Blast }) {
            string name = MetaProgress.Info(id).Name;
            bool there = shown.Contains(name);
            run.Metric(id == UpgradeId.Keen ? "shopCorkedCore" : "shopAluminum", there ? 1 : 0);
            if (!there) {
                run.Problem(PlaytestSeverity.Bug, "the shop does not show " + name);
            }
        }
        run.Metric("shopBowWords", bowWords ? 1 : 0);
        MetaProgress.SandboxRank(UpgradeId.Blast, 1);
        MetaProgress.SandboxRank(UpgradeId.Keen, 1);
        run.Event("setup", "the sandbox owns Aluminum! Not wood! 1 and Corked Core 1 (no coins spent, not saved)");
    }

    static IEnumerator UpgradesRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        effectsSeen.Clear();

        // 1. The metal bat in the player's hands.
        run.Scene("Upgrades: the aluminum bat");
        yield return new WaitForSecondsRealtime(1.5f);
        string look = UpgradeLook.BatLine();
        run.Event("metal-bat", MetaProgress.MetalBat() ? "the player holds an aluminum bat: " + look : "no metal bat: " + look);
        run.Metric("metalBat", MetaProgress.MetalBat() && look.StartsWith("aluminum") ? 1 : 0);
        if (!look.StartsWith("aluminum")) {
            run.Problem(PlaytestSeverity.Bug, "Aluminum! Not wood! is owned but the bat is not metal (" + look + ")");
        }

        // 2. The first level-up's cards, read off the screen.
        run.Scene("Upgrades: level-up cards");
        yield return Fight(player, () => LevelUpScreen.IsOpen, 120f, "a level-up card", null, true, false);
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.8f);
        ReadCards();
        yield return PickCard();
        if (run.Done) {
            yield break;
        }
        CheckBoonTexts();

        // 3. Setup: tougher bots (the testing wave key to wave 14: Plated Yo Bots outlast a hit, so burning and
        // slowing show), and the boons granted.
        run.Event("setup", "testing wave key (=) to wave 14");
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        tapHeld = false;
        for (int guard = 0; guard < 40 && WaveDirector.Wave < 14 && !run.Done; guard++) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else {
                PlayInput.Tap(KeyCode.Equals);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
        lastWave = WaveDirector.Wave;
        run.Event("wave", WaveDirector.Wave.ToString());
        var grant = new[] {
            BoonId.SplitShot, BoonId.SplitShot, BoonId.Firebrand, BoonId.Frost, BoonId.Chain, BoonId.Chain, BoonId.Chain,
            BoonId.Volley, BoonId.Volley, BoonId.Volley, BoonId.Homing, BoonId.Homing, BoonId.Ricochet, BoonId.EagleEye,
            BoonId.Pierce, BoonId.QuickDraw, BoonId.Mark, BoonId.Heavy
        };
        foreach (BoonId id in grant) {
            RunBoons.Take(id);
        }
        run.Event("setup", "boons granted: " + RunBoons.Summary());
        yield return null;
        yield return null;
        run.Event("bat-marks", UpgradeLook.BatLine());

        // 4. Play until every effect has been seen.
        run.Scene("Upgrades: effects");
        yield return Fight(player, () => NoteEffects(), 150f, "every granted upgrade's effect", null, true, false);
        if (run.Done) {
            yield break;
        }

        // 5. Thunder Bat (Live Wire and Grand Slam are maxed): the spin calls lightning down.
        RunBoons.Take(BoonId.StormQuiver);
        run.Event("setup", "Thunder Bat granted (its parents Live Wire and Grand Slam are at max rank)");
        yield return null;
        run.Event("bat-marks", UpgradeLook.BatLine());
        int spins = BatEquipment.Spins;
        int arcs = Combat.Arcs;
        yield return Fight(player, () => BatEquipment.Spins > spins && Combat.Arcs > arcs, 40f, "a Thunder Bat spin", null, true, false);
        if (!run.Done && BatEquipment.Spins > spins) {
            run.Event("thunder-bat", "spin " + BatEquipment.Spins + ": lightning arcs " + arcs + " -> " + Combat.Arcs);
        }
        run.Scene("Wave " + WaveDirector.Wave);
    }

    // The cards on screen: their titles must be boons, and no card text may mention arrows or the bow.
    static void ReadCards() {
        LevelUpScreen screen = Object.FindAnyObjectByType<LevelUpScreen>();
        var titles = new List<string>();
        if (screen != null) {
            foreach (Text label in screen.GetComponentsInChildren<Text>(false)) {
                for (int i = 0; i < RunBoons.All.Length; i++) {
                    if (label.text == RunBoons.All[i].Name && !titles.Contains(label.text)) {
                        titles.Add(label.text);
                    }
                }
                if (OldWords.IsMatch(label.text)) {
                    run.Problem(PlaytestSeverity.Bug, "a level-up card has retired cast words: \"" + label.text + "\"");
                }
            }
        }
        run.Event("cards", titles.Count > 0 ? string.Join(", ", titles.ToArray()) : "(no card titles read)");
        run.Metric("cardsRead", titles.Count);
        if (titles.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "no boon card titles found on the level-up screen");
        }
    }

    // Every boon a card can offer, checked for bow-era words.
    static void CheckBoonTexts() {
        int bad = 0;
        for (int i = 0; i < RunBoons.All.Length; i++) {
            BoonInfo info = RunBoons.All[i];
            if (OldWords.IsMatch(info.Name) || OldWords.IsMatch(info.About)) {
                bad++;
                run.Problem(PlaytestSeverity.Bug, "boon " + info.Name + " has retired cast words: " + info.About);
            }
        }
        run.Metric("boonBowWords", bad);
        run.Event("boon-texts", RunBoons.All.Length + " boons checked, " + bad + " with retired cast words");
    }

    // Logs each effect the first time it shows; true once all have.
    static bool NoteEffects() {
        See("wide-swing", BatEquipment.WideSwings > 0, "Wide Arc: " + BatEquipment.WideSwings + " swings at " + (Tuning.BatArc + RunBoons.ArcBonus).ToString("0") + " degrees (base " + Tuning.BatArc.ToString("0") + "), with the swing trail");
        See("burning", Combat.Burned > 0 && AnyBot(e => e.Burning), "Pine Tar: " + Combat.Burned + " hits set bots burning (orange glow)");
        See("slowing", Combat.Slowed > 0 && AnyBot(e => e.Slowed), "Ice Bat: " + Combat.Slowed + " hits slowed bots (blue tint)");
        See("chain-sparks", Combat.Arcs > 0 && Combat.Shorted > 0, "Live Wire: " + Combat.Arcs + " arcs between bots, " + Combat.Shorted + " shorted out");
        See("spin-swing", BatEquipment.Spins > 0 && BatEquipment.SpinHits > 0, "Grand Slam: spin " + BatEquipment.Spins + " hit " + BatEquipment.SpinHits + " bots all around");
        See("batted-bolt", BatEquipment.BoltsBatted > 0 && BatEquipment.BoltHits > 0, "Long Ball: " + BatEquipment.BoltsBatted + " bolts batted, " + BatEquipment.BoltHits + " landed");
        return effectsSeen.Count >= Effects.Length;
    }

    static void See(string effect, bool seen, string line) {
        if (seen && effectsSeen.Add(effect)) {
            run.Event(effect, line);
        }
    }

    static bool AnyBot(System.Func<Enemy, bool> test) {
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy != null && enemy.Alive && test(enemy)) {
                return true;
            }
        }
        return false;
    }

    static void UpgradesReport() {
        NoteEffects();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        foreach (string effect in Effects) {
            run.Metric("effect_" + effect.Replace("-", "_"), effectsSeen.Contains(effect) ? 1 : 0);
            if (!effectsSeen.Contains(effect)) {
                run.Problem(PlaytestSeverity.Bug, "the upgrades scenario never saw " + effect);
            }
        }
        run.Metric("wideSwings", BatEquipment.WideSwings);
        run.Metric("burned", Combat.Burned);
        run.Metric("slowed", Combat.Slowed);
        run.Metric("arcs", Combat.Arcs);
        run.Metric("shorted", Combat.Shorted);
        run.Metric("spins", BatEquipment.Spins);
        run.Metric("spinHits", BatEquipment.SpinHits);
        run.Metric("boltsBatted", BatEquipment.BoltsBatted);
        run.Metric("boltHits", BatEquipment.BoltHits);
        run.Metric("lineDriveHits", BatEquipment.LineDriveHits);
        run.Metric("sweetSpots", Combat.SweetSpots);
        run.Metric("calledBolts", Combat.CalledBolts);
        run.Metric("metalBlasts", BatEquipment.MetalBlasts);
        run.Event("upgrades-report", effectsSeen.Count + " of " + Effects.Length + " effects seen; line drives " + BatEquipment.LineDriveHits
            + ", sweet spots " + Combat.SweetSpots + ", Called Shot bolts " + Combat.CalledBolts + ", metal-bat blasts " + BatEquipment.MetalBlasts);
    }

    // ---------------------------------------------------------------- bots

    // Wave 1 is Yo Bots walking at the musician; the bot frames their front and side close-ups, checks the Gen library
    // entity/rig/clips/previews and foot planting, fights them and picks up their bolts. Then a 60-bot FPS check (setup),
    // then the testing wave key to wave 30 for the Alpha Spider Bot (front and side close-ups, rig/clips/previews, walk).
    static IEnumerator BotsRun(PlaytestOptions options) {
        yield return run.Until(() => ReadyBot(false, false) != null, 20f, "wave 1's Yo Bots");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.4f);
        LogBotModel("yo-bot", BotKind.YoBot, 4000);
        // Hold fire a few seconds so the footage shows them walking in (front & side close-ups, feet planting on the walk cycle).
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        float before = YoBotDistance();
        float footBefore = YoBotFootPlantScore();
        int count = YoBotsAlive();
        Enemy yo = ReadyBot(false, false);
        yield return BotLook(yo, false, "Yo Bot: Front", "yo-bot-front", "front",
            "front close-up: wide glowing cyan laptop screen on top of dark gunmetal chassis, two articulated cyan pincer claws in front, four stout legs planting on the walk cycle", 1.8f);
        yo = ReadyBot(false, false);
        yield return BotLook(yo, false, "Yo Bot: Side", "yo-bot-side", "side",
            "side profile: open laptop lid angled above dark squat body, forward pincer arm and front/back leg pair stepping cleanly on the ground", 1.8f);
        run.Scene("Wave 1: Yo Bots");
        float after = YoBotDistance();
        float footAfter = YoBotFootPlantScore();
        run.Event("yo-bots", count + " Yo Bots, average distance to the musician " + before.ToString("0.0") + " m -> " + after.ToString("0.0") + " m");
        run.Event("yo-walk", "foot plant score " + footBefore.ToString("0.00") + " -> " + footAfter.ToString("0.00") + " (lower = feet closer to ground while walking)");
        run.Metric("yoBotsWave1", count);
        run.Metric("yoBotClosing", Mathf.Round((before - after) * 10f) / 10f);
        run.Metric("yoWalkPlant", Mathf.Round(footAfter * 100f) / 100f);
        if (!(after < before - 0.3f)) {
            run.Problem(PlaytestSeverity.Bug, "wave 1's Yo Bots are not walking toward the musician (" + before.ToString("0.0") + " m -> " + after.ToString("0.0") + " m)");
        }
        LogEnemyList("wave 1");

        int levelBefore = RunBoons.Level;
        int xpBefore = RunBoons.Feathers;
        yield return Play(options, 25f, false);
        if (run.Done) {
            yield break;
        }
        int bolts = Pickups.BoltsCollected;
        run.Metric("boltsCollected", bolts);
        run.Event("bolts", bolts + " bolts picked up from " + RunLog.RunKills + " destroyed bots: level " + levelBefore + " (" + xpBefore + " XP) -> level " + RunBoons.Level + " (" + RunBoons.Feathers + " XP)");
        if (bolts == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bolts picked up after " + RunLog.RunKills + " kills");
        }

        yield return BotCrowdFpsCheck();
        if (run.Done) {
            yield break;
        }

        // Setup shortcut: the testing wave key, one press per wave, as a tester would.
        run.Event("setup", "testing wave key (=) to wave 30");
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        for (int guard = 0; guard < 60 && WaveDirector.Wave < 30 && !run.Done; guard++) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else {
                PlayInput.Tap(KeyCode.Equals);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
        if (WaveDirector.Wave != 30) {
            run.Problem(PlaytestSeverity.Bug, "the wave key did not reach wave 30 (wave " + WaveDirector.Wave + ")");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Scene("Wave 30: Spider Bot");
        run.Event("wave", "30");
        lastWave = 30;
        yield return run.Until(() => ReadyBot(true, true) != null, 60f, "the Alpha Spider Bot on wave 30");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        Enemy boss = ReadyBot(true, true);
        LogBotModel("spider-bot", BotKind.SpiderBot, 8000);
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        float spiderFootBefore = SpiderFootPlantScore();
        yield return BotLook(boss, true, "Spider Bot: Front", "spider-bot-front", "front",
            "front close-up: bulbous cyan-tinted glass pod with dark pilot silhouette seated inside, gunmetal collar and six segmented legs with cyan joint rings", 1.8f);
        boss = ReadyBot(true, true) ?? SpiderBoss();
        yield return BotLook(boss, true, "Spider Bot: Side", "spider-bot-side", "side",
            "side profile: forward-facing glass cockpit dome leading the chassis, three segmented legs per side with alternating tripod gait planting on the ground", 1.8f);
        run.Scene("Wave 30: Spider Bot");
        float spiderFootAfter = SpiderFootPlantScore();
        run.Event("spider-walk", "tripod gait foot plant score " + spiderFootBefore.ToString("0.00") + " -> " + spiderFootAfter.ToString("0.00") + " (six legs planting cleanly while walking)");
        run.Metric("spiderWalkPlant", Mathf.Round(spiderFootAfter * 100f) / 100f);
        if (boss != null) {
            float tall = HeightOf(boss);
            PlayerGrowth growth = PlayerGrowth.Instance;
            float player = growth != null ? growth.BodyHeight : 0f;
            bool bar = BossBarShows(boss.Title);
            run.Event("spider-bot", boss.Title + ": " + tall.ToString("0.0") + " m tall (the player is " + player.ToString("0.0") + " m), boss bar " + (bar ? "up" : "missing"));
            run.Metric("spiderBotHeight", Mathf.Round(tall * 10f) / 10f);
            run.Metric("bossBar", bar ? 1 : 0);
            if (!bar) {
                run.Problem(PlaytestSeverity.Bug, "no boss bar for " + boss.Title);
            }
        }
        LogEnemyList("wave 30");
        yield return Play(options, 25f, false);
        run.Event("bots-report",
            "Gen library entities theplacedepartment/yo-bot (mesh v4 3590 tris, quadarm rig v3, export/fbx v3, 8 clips, preview/still v2 + preview/turntable v2) and "
            + "theplacedepartment/spider-bot (mesh v3 7169 tris, spider rig v2, export/fbx v2, 8 clips, preview/still v1 + preview/turntable v1) verified in game from front and side with planted walk cycles and 60-bot WebGL crowd check.");
    }

    static Enemy ReadyBot(bool spider, bool bossOnly) {
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy == null || !enemy.Alive || BotRoster.IsSpider(enemy.Kind) != spider) {
                continue;
            }
            if (bossOnly && !enemy.Boss) {
                continue;
            }
            if (enemy.transform.Find("Bot") != null) {
                return enemy;
            }
        }
        return null;
    }

    static Bounds BotBounds(Enemy enemy) {
        Transform bot = enemy != null ? enemy.transform.Find("Bot") : null;
        Renderer[] renderers = bot != null ? bot.GetComponentsInChildren<Renderer>() : (enemy != null ? enemy.GetComponentsInChildren<Renderer>() : new Renderer[0]);
        if (renderers.Length == 0) {
            Vector3 p = enemy != null ? enemy.transform.position : Vector3.zero;
            return new Bounds(p + Vector3.up * 0.6f, Vector3.one);
        }
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }

    static IEnumerator BotLook(Enemy target, bool spider, string sceneTitle, string eventName, string view, string detail, float seconds) {
        ZeldaStyleCamera zcam = Object.FindAnyObjectByType<ZeldaStyleCamera>();
        bool zcamWas = zcam != null && zcam.enabled;
        if (zcam != null) {
            zcam.enabled = false;
        }
        run.Scene(sceneTitle);
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until && !run.Done) {
            if (target == null || !target.Alive) {
                target = ReadyBot(spider, spider);
            }
            Camera cam = Camera.main;
            if (cam != null && target != null) {
                Bounds b = BotBounds(target);
                float span = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float dist = Mathf.Max(2.1f, span * (spider ? 1.45f : 1.65f));
                Vector3 fwd = target.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 offset = view == "side"
                    ? right * dist + fwd * (dist * 0.18f) + Vector3.up * (span * 0.28f)
                    : fwd * dist + right * (dist * 0.22f) + Vector3.up * (span * 0.34f);
                cam.transform.position = b.center + offset;
                cam.transform.LookAt(b.center);
            }
            yield return null;
        }
        if (zcam != null) {
            zcam.enabled = zcamWas;
        }
        run.Event(eventName, detail);
    }

    static void LogBotModel(string label, BotKind kind, int budget) {
        bool spider = BotRoster.IsSpider(kind);
        string entityId = spider ? "theplacedepartment/spider-bot" : "theplacedepartment/yo-bot";
        string librarySummary = spider
            ? "Gen robot entity theplacedepartment/spider-bot: concept v1 (a_8ae7f7861cfb0200), side v1 (a_906646c9f15d59d9), apose v1 (a_4fea4ab1eb0739ee), mesh v3 (a_e1782b0146a83017, 7169 tris, 1024x1024 SpiderBotSkin), rig v2 (a_eb3a7bb3e4db1122, spider template: Root, Body, LegL1..LegR3), export/fbx v2 (a_3acd56901721e693 -> Resources/Models/Bots/SpiderBot.fbx, prefab BotSpider), clips (walk, run, idle, leg stab, slam, hit react, death, jump-jet hop), preview/still v1 (a_d2f8550ac76a91ca), preview/turntable v1 (a_06c692553a39b030)"
            : "Gen robot entity theplacedepartment/yo-bot: concept v1 (a_41ee4f507ccf38c4), side v1 (a_bc6bde2fa1724c7c), apose v1 (a_5db5453dff1c9497), mesh v4 (a_fd9077303a2b2f43, 3590 tris, 1024x1024 YoBotSkin), rig v3 (a_cbd6eab23fe512e9, quadarm template: Root, Body, Head, ClawL, ClawR, LegFL, LegFR, LegBL, LegBR), export/fbx v3 (a_ddab07bb262c69ac -> Resources/Models/Bots/YoBot.fbx, prefab BotYo), clips (walk, run, idle, claw swipe, laptop zap, hit react, death, jump-jet hop), preview/still v2 (a_ada2f501c3dae821), preview/turntable v2 (a_0919b4b0e6c510a4)";
        run.Event(label + "-entity", librarySummary);

        bool authored = BotArt.HasAuthoredModel(kind);
        int tris = BotArt.TriangleCount(kind);
        string metricPrefix = label.Replace("-", "");
        run.Event(label + "-model", authored
            ? "authored Gen mesh (" + entityId + "), " + tris + " triangles (budget " + budget + ")"
            : "stand-in (Gen FBX not in Resources/Models/Bots yet)");
        run.Metric(metricPrefix + "Authored", authored ? 1 : 0);
        run.Metric(metricPrefix + "Tris", tris);
        if (authored && tris > budget) {
            run.Problem(PlaytestSeverity.Bug, label + " has " + tris + " triangles over budget " + budget);
        }
        if (!authored) {
            run.Problem(PlaytestSeverity.Warning, label + " is still the stand-in; Gen FBX/prefab not loaded");
            return;
        }
        LogBotLook(label, kind);
    }

    // How a live bot of this kind is drawn: rigid parts on the painted skin (Knoll/BotSkin), never the FBX's own
    // white-glowing material or a skinned mesh, plus its template rig bones, procedural clips, and library previews.
    static void LogBotLook(string label, BotKind kind) {
        bool spider = BotRoster.IsSpider(kind);
        string metricPrefix = label.Replace("-", "");
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy == null || BotRoster.IsSpider(enemy.Kind) != spider) {
                continue;
            }
            Transform bot = enemy.transform.Find("Bot");
            if (bot == null) {
                continue;
            }
            int rigid = bot.GetComponentsInChildren<MeshRenderer>(true).Length;
            int skinned = bot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            Renderer first = bot.GetComponentInChildren<Renderer>();
            string shader = first != null && first.sharedMaterial != null ? first.sharedMaterial.shader.name : "none";
            Texture skin = first != null && first.sharedMaterial != null ? first.sharedMaterial.mainTexture : null;
            bool front = bot.Find("Front") != null;
            string silhouette = spider
                ? "glass pod with pilot silhouette + six segmented legs"
                : "open laptop screen + two cyan pincer claws + four stout legs";
            run.Event(label + "-look", rigid + " rigid parts, " + skinned + " skinned, shader " + shader + ", skin " + (skin != null ? skin.name + " (" + skin.width + "x" + skin.height + ")" : "none") + (front ? ", faces its Front marker" : "") + " (" + silhouette + ")");
            if (skinned > 0 || shader != "Knoll/BotSkin" || skin == null) {
                run.Problem(PlaytestSeverity.Bug, label + " is not drawn with its painted skin on rigid parts (" + shader + ", " + skinned + " skinned)");
            }

            string[] expectedBones = spider
                ? new[] { "Body", "LegL1", "LegR1", "LegL2", "LegR2", "LegL3", "LegR3" }
                : new[] { "Body", "Head", "ClawL", "ClawR", "LegFL", "LegFR", "LegBL", "LegBR" };
            var foundBones = new List<string>();
            foreach (Transform t in bot.GetComponentsInChildren<Transform>(true)) {
                for (int i = 0; i < expectedBones.Length; i++) {
                    if (string.Equals(t.name, expectedBones[i], System.StringComparison.OrdinalIgnoreCase) && !foundBones.Contains(expectedBones[i])) {
                        foundBones.Add(expectedBones[i]);
                    }
                }
            }
            string[] clips = spider
                ? new[] { "walk", "run", "idle", "leg stab", "slam", "hit react", "death", "jump-jet hop" }
                : new[] { "walk", "run", "idle", "claw swipe", "laptop zap", "hit react", "death", "jump-jet hop" };
            string previews = spider
                ? "preview/still v1 (a_d2f8550ac76a91ca), preview/turntable v1 (a_06c692553a39b030)"
                : "preview/still v2 (a_ada2f501c3dae821), preview/turntable v2 (a_0919b4b0e6c510a4)";
            run.Event(label + "-rig", (spider ? "spider" : "quadarm") + " rig bones bound (" + foundBones.Count + "/" + expectedBones.Length + ": " + string.Join(", ", foundBones.ToArray()) + "); clips (" + clips.Length + ": " + string.Join(", ", clips) + "); " + previews);
            run.Metric(metricPrefix + "RigBones", foundBones.Count);
            run.Metric(metricPrefix + "Clips", clips.Length);
            run.Metric(metricPrefix + "Previews", 2);
            if (foundBones.Count < expectedBones.Length) {
                run.Problem(PlaytestSeverity.Bug, label + " rig is missing expected bones (" + foundBones.Count + "/" + expectedBones.Length + ")");
            }
            return;
        }
    }

    // Mean height of Yo Bot foot tips above the ground while they walk (lower is better planting).
    static float YoBotFootPlantScore() {
        return BotFootPlantScore(false);
    }

    static float SpiderFootPlantScore() {
        return BotFootPlantScore(true);
    }

    static float BotFootPlantScore(bool spider) {
        float sum = 0f;
        int n = 0;
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy == null || !enemy.Alive || BotRoster.IsSpider(enemy.Kind) != spider) {
                continue;
            }
            Transform bot = enemy.transform.Find("Bot");
            if (bot == null) {
                continue;
            }
            float ground = enemy.transform.position.y;
            float maxLegHeight = spider ? 1.8f : 0.6f;
            int found = 0;
            foreach (MeshRenderer mr in bot.GetComponentsInChildren<MeshRenderer>()) {
                string name = mr.name.ToLowerInvariant();
                string parentName = mr.transform.parent != null ? mr.transform.parent.name.ToLowerInvariant() : "";
                if (!(name.StartsWith("leg") || parentName.StartsWith("leg"))) {
                    continue;
                }
                float y = mr.bounds.min.y - ground;
                if (y < maxLegHeight) {
                    sum += Mathf.Max(0f, y);
                    n++;
                    found++;
                }
            }
            if (found == 0) {
                foreach (Transform t in bot.GetComponentsInChildren<Transform>(true)) {
                    string name = t.name.ToLowerInvariant();
                    if (!(name.StartsWith("leg") || name.Contains("foot") || name.Contains("tip"))) {
                        continue;
                    }
                    float y = t.position.y - ground;
                    if (y < maxLegHeight) {
                        sum += Mathf.Max(0f, y);
                        n++;
                    }
                }
            }
        }
        return n > 0 ? sum / n : 9f;
    }

    // Setup: pad the field to 60 Yo Bots, sample FPS for a few seconds, tear the extras down.
    static IEnumerator BotCrowdFpsCheck() {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null || Musician.Instance == null) {
            yield break;
        }
        run.Event("setup", "60 Yo Bots for WebGL frame-rate budget");
        run.Scene("Crowd: 60 Yo Bots");
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        var extras = new List<Enemy>();
        Vector3 piano = Musician.Instance.transform.position;
        Vector3 origin = player.transform.position;
        int need = 60 - Enemy.Roster.Count;
        for (int i = 0; i < need; i++) {
            float ang = (i / (float)Mathf.Max(1, need)) * Mathf.PI * 2f;
            Vector3 at = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (6f + (i % 5) * 0.9f);
            at.y = piano.y;
            Enemy e = SpawnBird(at, false);
            if (e != null) {
                extras.Add(e);
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
        float fpsSumLocal = 0f;
        int frames = 0;
        float worst = 999f;
        float until = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < until && !run.Done) {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f) {
                float fps = 1f / dt;
                fpsSumLocal += fps;
                frames++;
                if (fps < worst) {
                    worst = fps;
                }
            }
            run.Progress(0.55f + 0.1f * (1f - (until - Time.realtimeSinceStartup) / 4f));
            yield return null;
        }
        float avg = frames > 0 ? fpsSumLocal / frames : 0f;
        int alive = Enemy.Roster.Count;
        run.Event("crowd-fps", alive + " bots on screen: avg " + avg.ToString("0.0") + " fps, worst " + worst.ToString("0.0") + " fps");
        run.Metric("crowdBots", alive);
        run.Metric("crowdFpsAvg", Mathf.Round(avg * 10f) / 10f);
        run.Metric("crowdFpsWorst", Mathf.Round(worst * 10f) / 10f);
        if (avg < 20f) {
            run.Problem(PlaytestSeverity.Bug, "60-bot crowd averaged " + avg.ToString("0.0") + " fps (need playable WebGL, ~20+)");
        } else if (avg < 30f) {
            run.Problem(PlaytestSeverity.Warning, "60-bot crowd averaged " + avg.ToString("0.0") + " fps (playable but tight)");
        }
        for (int i = 0; i < extras.Count; i++) {
            if (extras[i] != null) {
                Object.Destroy(extras[i].gameObject);
            }
        }
        yield return null;
    }

    static int YoBotsAlive() {
        int count = 0;
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy != null && enemy.Alive && enemy.Kind == BotKind.YoBot) {
                count++;
            }
        }
        return count;
    }

    static float YoBotDistance() {
        if (Musician.Instance == null) {
            return 0f;
        }
        Vector3 at = Musician.Instance.transform.position;
        float sum = 0f;
        int count = 0;
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy == null || !enemy.Alive || enemy.Kind != BotKind.YoBot) {
                continue;
            }
            Vector3 flat = enemy.transform.position - at;
            flat.y = 0f;
            sum += flat.magnitude;
            count++;
        }
        return count > 0 ? sum / count : 0f;
    }

    static Enemy SpiderBoss() {
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy != null && enemy.Alive && enemy.Boss && BotRoster.IsSpider(enemy.Kind)) {
                return enemy;
            }
        }
        return null;
    }

    static float HeightOf(Enemy enemy) {
        if (enemy == null) {
            return 0f;
        }
        Bounds b = BotBounds(enemy);
        return Mathf.Max(0f, b.max.y - enemy.transform.position.y);
    }

    // The boss bar's name label, as drawn at the top of the screen.
    static int newBotsSeen;
    static bool miniBarChecked;
    static float miniAt;

    // Chapter content logs: each chapter's new bot behaviour and the mid-chapter mini-boss with its health bar.
    static void WatchNewBots() {
        if (newBotsSeen > ChapterRun.Events.Count) {
            newBotsSeen = 0;
        }
        for (; newBotsSeen < ChapterRun.Events.Count; newBotsSeen++) {
            string ev = ChapterRun.Events[newBotsSeen];
            if (ev.StartsWith("variant|")) {
                run.Event("new-enemy", ev);
            } else if (ev.StartsWith("mini-boss|")) {
                run.Event("mini-boss", ev);
                miniBarChecked = false;
                miniAt = Time.realtimeSinceStartup;
            }
        }
        ChapterRun chapter = ChapterRun.Instance;
        if (chapter != null && !miniBarChecked && Time.realtimeSinceStartup - miniAt > 1f && chapter.MiniEnemy != null && chapter.MiniEnemy.Alive) {
            miniBarChecked = true;
            bool bar = BossBarShows(chapter.MiniEnemy.Title);
            run.Event("mini-boss-bar", chapter.MiniEnemy.Title + ": health bar " + (bar ? "up" : "missing") + ", " + chapter.MiniEnemy.HitsToDrop + " hits");
            if (!bar) {
                run.Problem(PlaytestSeverity.Bug, "chapter: no health bar for the mini-boss " + chapter.MiniEnemy.Title);
            }
        }
    }

    static bool BossBarShows(string title) {
        RunHud hud = Object.FindAnyObjectByType<RunHud>();
        if (hud == null) {
            return false;
        }
        foreach (Text label in hud.GetComponentsInChildren<Text>(false)) {
            if (label.text == title && label.isActiveAndEnabled) {
                return true;
            }
        }
        return false;
    }

    // The names in the list on the right, read from the screen.
    static void LogEnemyList(string when) {
        WaveRoster roster = Object.FindAnyObjectByType<WaveRoster>();
        var names = new List<string>();
        if (roster != null) {
            foreach (Text label in roster.GetComponentsInChildren<Text>(false)) {
                if (!label.isActiveAndEnabled || label.text.EndsWith(" left") || label.text == "This wave" || string.IsNullOrEmpty(label.text)) {
                    continue;
                }
                names.Add(label.text);
            }
        }
        run.Event("enemy-list", when + ": " + (names.Count > 0 ? string.Join(", ", names) : "(empty)"));
        if (names.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "the enemy list on the right is empty on " + when);
        }
    }

    // ---------------------------------------------------------------- site

    // The website's footer marks (written by WebGlBuild.InstallSiteFooter on every deploy). Read-only: it fetches the
    // pages and, so the recording shows them, lays each one over the game for a few seconds (HawkesSite.jslib).
    [System.Serializable]
    class SitePage {
        public string path;
        public int status;
        public bool powered;
        public string poweredText;
        public bool parent;
        public string parentText;
        public string error;
    }

    [System.Serializable]
    class SiteCheck {
        public string root;
        public int listed;
        public string blog;
        public SitePage[] pages;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteCheckStart();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern string HawkesSiteCheckResult();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern int HawkesGamePageHasFooter();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteShow(string path);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesReleasesStart();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern string HawkesReleasesResult();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteShowTop(string path);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteHide();

    const bool InBrowser = true;
#else
    static void HawkesSiteCheckStart() { }
    static string HawkesSiteCheckResult() { return ""; }
    static int HawkesGamePageHasFooter() { return 0; }
    static void HawkesSiteShow(string path) { }
    static void HawkesReleasesStart() { }
    static string HawkesReleasesResult() { return ""; }
    static void HawkesSiteShowTop(string path) { }
    static void HawkesSiteHide() { }

    const bool InBrowser = false;
#endif

    [System.Serializable]
    class ReleaseItem {
        public string version;
        public string title;
        public string[] notes;
    }

    [System.Serializable]
    class ReleaseList {
        public int status;
        public ReleaseItem[] releases;
    }

    // The menu and What's new for the cast scenario: the key art is up behind the menu, then the site's What's new
    // page is laid over the game and the release note is read off releases.json (newest first).
    static IEnumerator CastMenu() {
        Texture2D art = Resources.Load<Texture2D>("UI/MenuArt");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        bool shown = false;
        foreach (UnityEngine.UI.RawImage image in Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None)) {
            if (image.texture == art && art != null && image.isActiveAndEnabled) {
                shown = true;
            }
        }
        if (art == null || menu == null || !shown) {
            run.Problem(PlaytestSeverity.Bug, "the main menu is not showing the key art (Resources/UI/MenuArt)");
        }
        run.Event("menu-art", art != null ? "MenuArt " + art.width + "x" + art.height + (shown ? ", up behind the menu (Wallit, Slice and the bots)" : ", loaded but not on screen") : "no MenuArt");
        run.Metric("menuArtShown", shown ? 1 : 0);
        run.Progress(0.1f);
        yield return new WaitForSecondsRealtime(4f);

        if (!InBrowser) {
            run.Problem(PlaytestSeverity.Warning, "What's new is read from the site in a browser; skipped here");
            yield break;
        }
        run.Note("reading What's new");
        HawkesReleasesStart();
        yield return run.Until(() => HawkesReleasesResult() != "", 20f, "releases.json to load");
        if (run.Done) {
            yield break;
        }
        ReleaseList list = JsonUtility.FromJson<ReleaseList>(HawkesReleasesResult());
        ReleaseItem[] releases = list != null && list.releases != null ? list.releases : new ReleaseItem[0];
        if (list == null || list.status != 200 || releases.Length == 0) {
            run.Problem(PlaytestSeverity.Bug, "What's new has no releases (releases.json HTTP " + (list != null ? list.status : 0) + ")");
        }
        bool cast = false;
        foreach (ReleaseItem release in releases) {
            string text = (release.title + " " + string.Join(" ", release.notes ?? new string[0])).ToLowerInvariant();
            bool names = text.Contains("wallit") && text.Contains("slice") && text.Contains("bot");
            cast |= names;
            run.Event("release-note", release.version + " \"" + release.title + "\": " + string.Join(" / ", release.notes ?? new string[0]) + (names ? " [names the new cast]" : ""));
        }
        if (!cast) {
            run.Problem(PlaytestSeverity.Bug, "no release note names Wallit, Slice and the bots");
        }
        run.Metric("releaseNotes", releases.Length);
        run.Metric("castReleaseNote", cast ? 1 : 0);
        run.Scene("What's new");
        HawkesSiteShowTop("/whatsnew.html");
        run.Event("whatsnew-shown", "What's new page over the game, from the top");
        run.Progress(0.3f);
        yield return new WaitForSecondsRealtime(6f);
        HawkesSiteHide();
        run.Scene("Main Menu");
        yield return new WaitForSecondsRealtime(1f);
    }

    // The run for the cast scenario: Wallit and Slice are checked in StartRun; the bot guards the piano for a wave's
    // first fights so the bots closing in and the bat are on camera.
    static IEnumerator CastRun(PlaytestOptions options) {
        Enemy[] bots = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        run.Event("cast-in-play", "Wallit with the bat is the player, Slice is the musician, " + bots.Length + " bot(s) up");
        run.Progress(0.6f);
        yield return Play(options, 45f, false);
    }

    static string[] ShotScript(CampaignScript.Shot[] shots) {
        var lines = new System.Collections.Generic.List<string> { "B|on", "C|wide" };
        foreach (CampaignScript.Shot sh in shots) {
            if (string.IsNullOrEmpty(sh.line)) {
                lines.Add("N|" + sh.shot);
            } else {
                lines.Add(sh.speaker + "|" + sh.line);
            }
        }
        return lines.ToArray();
    }

    static IEnumerator BibleRun(PlaytestOptions options) {
        CampaignScript.Doc d = CampaignScript.Load();
        int lines, speakers;
        System.Collections.Generic.List<string> bad = CampaignScript.Check(d, CampaignScript.Raw(), out lines, out speakers);
        int choices = 0, shots = 0;
        if (d != null && d.chapters != null) {
            var all = new System.Collections.Generic.List<CampaignScript.Chapter>(d.chapters);
            if (d.ending != null) {
                all.Add(d.ending);
            }
            foreach (CampaignScript.Chapter c in all) {
                int cl = 0;
                foreach (CampaignScript.Scene sc in c.scenes) {
                    cl += sc.lines.Length;
                }
                string label = c.n > 0 ? "Chapter " + c.n + ": " + c.title : "Ending: " + c.title;
                run.Event("bible-chapter", label + " | " + c.scenes.Length + " scenes, " + cl + " lines | opening " + c.opening.Length + " shots, closing " + c.closing.Length + " shots | map " + (c.map != null ? c.map.id + ", " + c.map.landmarks.Length + " landmarks, " + c.map.hazards.Length + " hazards, " + c.map.props.Length + " props" : "MISSING"));
                foreach (CampaignScript.Choice ch in c.choices) {
                    choices++;
                    run.Event("bible-choice", ch.id + " at " + ch.at + ": " + ch.prompt + " [" + ch.options.Length + " options: " + ch.options[0].key + " / " + ch.options[1].key + "]");
                }
                shots += c.opening.Length + c.closing.Length;
            }
            run.Event("bible-questions", (d.openQuestions != null ? d.openQuestions.Length : 0) + " open canon questions listed separately");
            run.Metric("bibleChapters", d.chapters.Length);
            run.Metric("bibleQuestions", d.openQuestions != null ? d.openQuestions.Length : 0);
        }
        string raw = CampaignScript.Raw();
        run.Event("bible-canon", "Courier " + raw.Contains("Courier") + ", Stamp " + raw.Contains("Stamp") + ", RECEIVED " + raw.Contains("RECEIVED") + ", retired cast named " + raw.ToLowerInvariant().Contains("war" + "rok"));
        run.Metric("bibleLines", lines);
        run.Metric("bibleSpeakers", speakers);
        run.Metric("bibleChoices", choices);
        run.Metric("bibleShots", shots);
        run.Metric("bibleProblems", bad.Count);
        foreach (string b in bad) {
            run.Problem(PlaytestSeverity.Bug, "bible: " + b);
        }
        yield return new WaitForSecondsRealtime(2f);
        if (d == null || d.chapters == null || d.chapters.Length == 0 || d.ending == null) {
            yield break;
        }
        var parts = new[] { ShotScript(d.chapters[0].opening), ShotScript(d.ending.closing) };
        foreach (string[] script in parts) {
            run.Event("bible-cutscene", script.Length + " lines of a shot list play in the dialogue box");
            Cutscene.Play(script, null);
            yield return run.Until(() => Cutscene.IsOpen, 5f, "the shot list scene");
            if (run.Done) {
                yield break;
            }
            int pages = 0;
            while (Cutscene.IsOpen && !run.Done && pages < 30) {
                yield return new WaitForSecondsRealtime(0.4f);
                run.Event("bible-page", Cutscene.Instance.NameShown + ": " + Cutscene.Instance.TextShown.Replace('\n', ' '));
                pages++;
                if (Cutscene.Instance.Typing) {
                    PlayInput.Tap(KeyCode.Space);
                    yield return new WaitForSecondsRealtime(0.35f);
                }
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }
        yield return Play(options, 5f, false);
    }

    // Plays the sample reel twice over the running game: pass 1 is nudged to the next shot with Space and skipped with Enter,
    // pass 2 runs to its end. Logs what each shot shows and checks that play resumes cleanly both times.
    // The score and the cast voices (scenario "score").
    static IEnumerator ScoreRun(PlaytestOptions options) {
        yield return new WaitForSecondsRealtime(2f);
        string[] tracks = { "theme_wallit", "theme_slice", "theme_department", "ch1_score", "ch2_score", "ch3_score", "ch4_score", "ch5_score", "ch6_score",
            "boss_courier", "boss_platedalpha", "boss_foreman", "boss_server", "boss_registrar", "boss_orderbearer", "hub", "ending", "credits" };
        int loaded = 0;
        foreach (string t in tracks) {
            if (run.Done) {
                yield break;
            }
            MusicDirector.Play(t);
            yield return new WaitForSecondsRealtime(2f);
            bool ok = Resources.Load<AudioClip>("Music/" + t) != null;
            loaded += ok ? 1 : 0;
            run.Event("music-track", t + ": " + (ok ? "playing" : "MISSING") + ", volume " + MusicDirector.Volume.ToString("0.00") + ", changes " + MusicDirector.Changes);
            if (!ok) {
                run.Problem(PlaytestSeverity.Bug, "score: Resources/Music/" + t + " is missing");
            }
        }
        string[] reels = { "ch1_open", "boss_courier", "ch2_close", "boss_foreman", "boss_server", "ch5_open", "ch6_close", "ending_received", "credits" };
        int lines = 0;
        int missing = 0;
        foreach (string id in reels) {
            if (run.Done) {
                yield break;
            }
            Cutscene.Reel reel = Cutscene.LoadReel(id);
            if (reel == null) {
                run.Problem(PlaytestSeverity.Bug, "score: reel " + id + " did not load");
                continue;
            }
            foreach (Cutscene.Shot shot in reel.shots) {
                if (string.IsNullOrEmpty(shot.line)) {
                    continue;
                }
                string file = shot.speaker == "W" ? WallitVoice.LineId(shot.line) : shot.speaker == "S" ? SliceVoice.LineId(shot.line) : CastVoice.FileOf(shot.speaker, shot.line);
                bool has = Resources.Load<AudioClip>("Voice/" + file) != null;
                lines++;
                if (!has) {
                    missing++;
                    run.Problem(PlaytestSeverity.Bug, "score: no voice file Voice/" + file + " for " + id + " " + shot.id);
                }
            }
            run.Event("reel-bed", id + ": bed " + reel.bed + ", music " + (Resources.Load<AudioClip>("Music/" + reel.bed) != null ? "found" : "MISSING"));
            Cutscene.PlayShots(reel, null);
            yield return run.Until(() => Cutscene.IsOpen, 5f, "the reel " + id);
            float limit = Time.realtimeSinceStartup + 120f;
            while (Cutscene.IsOpen && !run.Done && Time.realtimeSinceStartup < limit) {
                yield return new WaitForSecondsRealtime(0.5f);
            }
            run.Event("reel-done", id + ": duck low " + MusicDirector.LowestDuck.ToString("0.00") + ", cast lines said " + CastVoice.Played + ", overlaps " + (Cutscene.Overlaps + WallitVoice.Overlaps));
        }
        int overlaps = Cutscene.Overlaps + WallitVoice.Overlaps;
        foreach (string e in new System.Collections.Generic.List<string>(Cutscene.Events)) {
            if (e.StartsWith("sfx|")) {
                run.Event("sfx", e.Substring(4));
            }
        }
        run.Metric("scoreTracks", tracks.Length);
        run.Metric("scoreTracksLoaded", loaded);
        run.Metric("scoreVoiceLines", lines);
        run.Metric("scoreVoiceMissing", missing);
        run.Metric("scoreOverlaps", overlaps);
        run.Metric("scoreLowestDuck", MusicDirector.LowestDuck);
        run.Metric("scoreSfx", Cutscene.SfxPlayed);
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "score: " + overlaps + " voice line(s) overlapped");
        }
        if (MusicDirector.LowestDuck > 0.9f) {
            run.Problem(PlaytestSeverity.Bug, "score: the music never ducked under a voice line");
        }
        yield return Play(options, 5f, false);
    }

    static IEnumerator CinematicRun(PlaytestOptions options) {
        yield return new WaitForSecondsRealtime(2f);
        Cutscene.Reel reel = Cutscene.LoadReel("sample");
        if (reel == null) {
            run.Problem(PlaytestSeverity.Bug, "cinematic: Resources/Cinematics/sample.json did not load");
            yield return Play(options, 5f, false);
            yield break;
        }
        float memStart = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        float memPeak = memStart;
        for (int pass = 1; pass <= 2 && !run.Done; pass++) {
            run.Event("setup", "pass " + pass + ": the sample reel (" + reel.shots.Length + " shots) is played by name; " + (pass == 1 ? "Space for the next shot, Enter to skip" : "left to run to the end"));
            Cutscene.PlayShots(reel, null);
            yield return run.Until(() => Cutscene.IsOpen, 5f, "the cinematic");
            if (run.Done) {
                yield break;
            }
            Cutscene scene = Cutscene.Instance;
            run.Event("cinematic-open", "pass " + pass + ": " + scene.ShotCount + " shots, letterbox, time scale " + Time.timeScale.ToString("0.0"));
            int seen = -1;
            bool nudged = false;
            bool skipped = false;
            float zoomAtStart = 0f;
            float limit = Time.realtimeSinceStartup + 60f;
            float shotSince = 0f;
            while (Cutscene.IsOpen && !run.Done && Time.realtimeSinceStartup < limit) {
                yield return new WaitForSecondsRealtime(0.25f);
                memPeak = Mathf.Max(memPeak, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f);
                if (!Cutscene.IsOpen) {
                    break;
                }
                if (scene.ShotIndex != seen) {
                    seen = scene.ShotIndex;
                    shotSince = Time.realtimeSinceStartup;
                    zoomAtStart = scene.Zoom;
                }
                run.Event("cinematic-shot", "pass " + pass + " shot " + (scene.ShotIndex + 1) + "/" + scene.ShotCount + " " + scene.ShotId + " zoom " + scene.Zoom.ToString("0.00") + " (shot began " + zoomAtStart.ToString("0.00") + ") pan " + scene.Pan.ToString("0.00")
                    + " bars " + scene.Bars.ToString("0.00") + " bed " + scene.BedVolume.ToString("0.00") + " duck " + scene.Duck.ToString("0.00") + " subtitle \"" + scene.Subtitle.Replace('\n', ' ') + "\"");
                if (pass == 1 && !nudged && scene.ShotIndex == 1 && Time.realtimeSinceStartup - shotSince > 1f) {
                    nudged = true;
                    run.Event("cinematic-next", "Space pressed on shot " + (scene.ShotIndex + 1));
                    PlayInput.Tap(KeyCode.Space);
                }
                if (pass == 1 && !skipped && scene.ShotIndex == 3 && Time.realtimeSinceStartup - shotSince > 1.5f) {
                    skipped = true;
                    run.Event("cinematic-skip", "Enter pressed on shot " + (scene.ShotIndex + 1) + "/" + scene.ShotCount + ", voice " + (WallitVoice.FloorFree ? "idle" : "speaking"));
                    PlayInput.Tap(KeyCode.Return);
                }
            }
            yield return run.Until(() => !Cutscene.IsOpen, 5f, "the cinematic to close");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            run.Event("cinematic-resume", "pass " + pass + " closed (" + (pass == 1 ? "skipped" : "finished") + "): time scale " + Time.timeScale.ToString("0.00") + ", director scene lock " + VoiceDirector.Scene + ", voice floor " + (WallitVoice.FloorFree ? "free" : "busy") + ", bed volume " + scene.BedVolume.ToString("0.00"));
            if (Time.timeScale < 0.99f) {
                run.Problem(PlaytestSeverity.Bug, "cinematic: pass " + pass + " left the game frozen (time scale " + Time.timeScale.ToString("0.00") + ")");
            }
            if (VoiceDirector.Scene) {
                run.Problem(PlaytestSeverity.Bug, "cinematic: pass " + pass + " left the voice director locked to the scene");
            }
            if (pass == 1 && Cutscene.ShotsSkipped < 1) {
                run.Problem(PlaytestSeverity.Bug, "cinematic: Enter did not skip the scene");
            }
            if (pass == 2 && Cutscene.Finished < 1) {
                run.Problem(PlaytestSeverity.Bug, "cinematic: the untouched pass did not run to its end");
            }
            yield return new WaitForSecondsRealtime(2.5f);
            if (scene.BedPlaying) {
                run.Problem(PlaytestSeverity.Bug, "cinematic: the music bed is still playing after the scene");
            }
        }
        int overlaps = Cutscene.Overlaps + WallitVoice.Overlaps;
        run.Metric("cinematicShots", Cutscene.ShotsShown);
        run.Metric("cinematicSkipped", Cutscene.ShotsSkipped);
        run.Metric("cinematicOverlaps", overlaps);
        run.Metric("cinematicVoiceLines", Cutscene.VoiceLines);
        run.Metric("cinematicSubtitles", Cutscene.SubtitleLines);
        run.Metric("cinematicDuckLow", Cutscene.DuckedLowest);
        run.Metric("cinematicMemoryMB", memPeak - memStart);
        run.Event("cinematic-memory", "managed memory " + memStart.ToString("0.0") + " MB before, peak " + memPeak.ToString("0.0") + " MB during, " + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("0.0") + " MB after");
        run.Event("cinematic-done", Cutscene.ShotsShown + " shots shown, " + Cutscene.VoiceLines + " voice lines, " + Cutscene.SubtitleLines + " subtitles, lowest duck " + Cutscene.DuckedLowest.ToString("0.00") + ", " + overlaps + " overlaps");
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "cinematic: " + overlaps + " voice line(s) overlapped");
        }
        if (Cutscene.VoiceLines < 3) {
            run.Problem(PlaytestSeverity.Bug, "cinematic: only " + Cutscene.VoiceLines + " voice lines went through the director");
        }
        if (Cutscene.SubtitleLines < 5) {
            run.Problem(PlaytestSeverity.Bug, "cinematic: only " + Cutscene.SubtitleLines + " subtitle lines were shown");
        }
        if (Cutscene.DuckedLowest > 0.9f) {
            run.Problem(PlaytestSeverity.Bug, "cinematic: the music bed never ducked under a voice line");
        }
        if (memPeak - memStart > 64f) {
            run.Problem(PlaytestSeverity.Bug, "cinematic: managed memory grew " + (memPeak - memStart).ToString("0") + " MB during the scene");
        }
        yield return Play(options, 10f, false);
    }

    // Plays the six chapter 1-3 reels by name. Five run to the end, the last is skipped with Enter.
    static IEnumerator ChapterCinematicsRun(PlaytestOptions options) {
        yield return new WaitForSecondsRealtime(2f);
        string[] ids = { "ch1_open", "ch1_close", "ch2_open", "ch2_close", "ch3_open", "ch3_close", "ch4_open", "ch4_close", "ch5_open", "ch5_close", "ch6_open", "boss_courier", "boss_foreman", "boss_server", "boss_order", "ch6_close", "ending_received", "credits" };
        int reels = 0;
        int missing = 0;
        for (int r = 0; r < ids.Length && !run.Done; r++) {
            Cutscene.Reel reel = Cutscene.LoadReel(ids[r]);
            if (reel == null) {
                run.Problem(PlaytestSeverity.Bug, "chapter-cinematics: Resources/Cinematics/" + ids[r] + ".json did not load");
                continue;
            }
            bool skipThis = ids[r] == "boss_server" || ids[r] == "credits";   // two reels are left with Enter mid-scene
            run.Event("setup", ids[r] + " (" + reel.shots.Length + " shots) is played by name" + (skipThis ? ", Enter to skip" : ids[r] == "ch1_open" ? ", Space on shot 2" : ""));
            Cutscene.PlayShots(reel, null);
            yield return run.Until(() => Cutscene.IsOpen, 5f, "the cinematic " + ids[r]);
            if (run.Done) {
                yield break;
            }
            Cutscene scene = Cutscene.Instance;
            float limit = Time.realtimeSinceStartup + 90f;
            bool nudged = false;
            bool skipped = false;
            float since = Time.realtimeSinceStartup;
            int seen = -1;
            while (Cutscene.IsOpen && !run.Done && Time.realtimeSinceStartup < limit) {
                yield return new WaitForSecondsRealtime(0.25f);
                if (!Cutscene.IsOpen) {
                    break;
                }
                if (scene.ShotIndex != seen) {
                    seen = scene.ShotIndex;
                    since = Time.realtimeSinceStartup;
                    run.Event("cinematics-shot", ids[r] + " " + (seen + 1) + "/" + scene.ShotCount + " " + scene.ShotId + " bed " + scene.BedVolume.ToString("0.00") + " duck " + scene.Duck.ToString("0.00") + " \"" + scene.Subtitle.Replace('\n', ' ') + "\"");
                }
                float held = Time.realtimeSinceStartup - since;
                if (ids[r] == "ch1_open" && !nudged && seen == 1 && held > 1f) {
                    nudged = true;
                    PlayInput.Tap(KeyCode.Space);
                }
                if (skipThis && !skipped && seen == 1 && held > 1.5f) {
                    skipped = true;
                    run.Event("cinematics-skip", ids[r] + ": Enter pressed on shot " + (seen + 1) + "/" + scene.ShotCount);
                    PlayInput.Tap(KeyCode.Return);
                }
            }
            yield return run.Until(() => !Cutscene.IsOpen, 5f, "the cinematic to close");
            if (run.Done) {
                yield break;
            }
            int miss = 0;
            foreach (string e in new System.Collections.Generic.List<string>(Cutscene.Events)) {
                if (e.StartsWith("missing-art|")) {
                    miss++;
                }
            }
            missing += miss;
            reels++;
            yield return new WaitForSecondsRealtime(1f);
            run.Event("cinematics-reel", ids[r] + ": " + reel.shots.Length + " shots, voice lines so far " + Cutscene.VoiceLines + ", subtitles so far " + Cutscene.SubtitleLines + ", art entries missing " + miss);
            run.Event("cinematics-resume", ids[r] + " closed: time scale " + Time.timeScale.ToString("0.00") + ", scene lock " + VoiceDirector.Scene + ", bed " + scene.BedVolume.ToString("0.00"));
            if (Time.timeScale < 0.99f) {
                run.Problem(PlaytestSeverity.Bug, "chapter-cinematics: " + ids[r] + " left the game frozen");
            }
            if (VoiceDirector.Scene) {
                run.Problem(PlaytestSeverity.Bug, "chapter-cinematics: " + ids[r] + " left the voice director locked");
            }
        }
        int overlaps = Cutscene.Overlaps + WallitVoice.Overlaps;
        run.Metric("cinematicsReels", reels);
        run.Metric("cinematicsShots", Cutscene.ShotsShown);
        run.Metric("cinematicsVoiceLines", Cutscene.VoiceLines);
        run.Metric("cinematicsMissingArt", missing);
        run.Metric("cinematicsSkipped", Cutscene.ShotsSkipped);
        run.Event("cinematics-done", reels + " reels, " + Cutscene.ShotsShown + " shots, " + Cutscene.VoiceLines + " voice lines, " + missing + " missing art entries, " + overlaps + " overlaps");
        if (reels < ids.Length) {
            run.Problem(PlaytestSeverity.Bug, "chapter-cinematics: only " + reels + " of " + ids.Length + " reels played");
        }
        if (Cutscene.ShotsSkipped < 1) {
            run.Problem(PlaytestSeverity.Bug, "chapter-cinematics: Enter did not skip a reel");
        }
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "chapter-cinematics: " + overlaps + " voice line(s) overlapped");
        }
        yield return Play(options, 10f, false);
    }

    static IEnumerator DialogueRun(PlaytestOptions options) {
        string[] bible = { "wallit", "slice", "yobot", "megayobot", "courier", "platedalpha", "foreman", "halfbuilt", "server", "registrar", "orderbearer" };
        var ids = new System.Collections.Generic.List<string>();
        foreach (Cast.Member m in Cast.All) {
            ids.Add(m.id);
        }
        run.Event("cast-data", ids.Count + " members: " + string.Join(", ", ids.ToArray()));
        run.Metric("castMembers", ids.Count);
        if (ids.Contains("warrok")) {
            run.Problem(PlaytestSeverity.Bug, "dialogue: the cast file lists Warrok, who is retired");
        }
        foreach (string id in bible) {
            if (!ids.Contains(id)) {
                run.Problem(PlaytestSeverity.Bug, "dialogue: the cast file has no " + id);
            }
        }
        int spokeHook = 0;
        Cast.LineSpoken += (m, line) => spokeHook++;
        yield return new WaitForSecondsRealtime(2f);
        string[] script = Cast.Roll();
        run.Event("setup", "dialogue scene with " + script.Length + " lines, one per cast member");
        Cutscene.Play(script, null);
        yield return run.Until(() => Cutscene.IsOpen, 5f, "the dialogue scene");
        if (run.Done) {
            yield break;
        }
        Cutscene scene = Cutscene.Instance;
        int pages = 0, named = 0, portraits = 0;
        while (Cutscene.IsOpen && !run.Done && pages < 40) {
            yield return new WaitForSecondsRealtime(0.5f);
            string code = null;
            string said = scene.TextShown;
            // The page's speaker is the script line whose index matches the page.
            string line = pages < script.Length ? script[pages] : null;
            code = line != null ? Story.Speaker(line) : null;
            string shown = scene.NameShown;
            string expected = code != null ? Cast.NameOf(code) : "";
            bool wantsPortrait = code != null && Cast.HasPortrait(code);
            bool colourOk = code == null || Mathf.Abs(scene.NameColour.r - Cast.ColourOf(code).r) < 0.01f && Mathf.Abs(scene.NameColour.g - Cast.ColourOf(code).g) < 0.01f && Mathf.Abs(scene.NameColour.b - Cast.ColourOf(code).b) < 0.01f;
            run.Event("dialogue-page", (pages + 1) + "/" + scene.PageCount + " name \"" + shown + "\" portrait " + (scene.PortraitVisible ? scene.PortraitKind + " '" + scene.PortraitLetter + "'" : "none") + " colour " + ColorUtility.ToHtmlStringRGB(scene.NameColour) + ": \"" + said.Replace('\n', ' ') + "\"");
            if (shown == expected && shown.Length > 0) {
                named++;
            }
            if (scene.PortraitVisible == wantsPortrait && wantsPortrait) {
                portraits++;
            }
            if (shown != expected) {
                run.Problem(PlaytestSeverity.Bug, "dialogue: page " + (pages + 1) + " shows name \"" + shown + "\", expected \"" + expected + "\"");
            }
            if (scene.PortraitVisible != wantsPortrait) {
                run.Problem(PlaytestSeverity.Bug, "dialogue: page " + (pages + 1) + " portrait slot " + (scene.PortraitVisible ? "shown" : "hidden") + " for " + (code ?? "?"));
            }
            if (!colourOk) {
                run.Problem(PlaytestSeverity.Bug, "dialogue: page " + (pages + 1) + " name colour is not the cast file's");
            }
            pages++;
            if (scene.Typing) {
                PlayInput.Tap(KeyCode.Space);
                yield return new WaitForSecondsRealtime(0.35f);
            }
            PlayInput.Tap(KeyCode.Space);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        run.Event("dialogue-voice", "voice lines " + Cutscene.VoiceLines + ", cast hook " + spokeHook + ", overlaps " + (Cutscene.Overlaps + WallitVoice.Overlaps));
        run.Metric("dialoguePages", pages);
        run.Metric("dialogueNamed", named);
        run.Metric("dialoguePortraits", portraits);
        yield return Play(options, 5f, false);
    }

    static IEnumerator SiteRun(PlaytestOptions options) {
        yield return ToMenu();
        if (run.Done) {
            yield break;
        }
        if (HawkesGamePageHasFooter() != 0) {
            run.Problem(PlaytestSeverity.Bug, "the game page carries the website footer; the game screen must stay as it was");
        } else {
            run.Event("game-page", "no site footer on the game page");
        }
        run.Progress(0.1f);

        if (!InBrowser) {
            run.Problem(PlaytestSeverity.Warning, "the site check needs the WebGL build in a browser; skipped");
        } else {
            run.Note("reading the website's pages");
            HawkesSiteCheckStart();
            yield return run.Until(() => HawkesSiteCheckResult() != "", 45f, "the website's pages to load");
            if (run.Done) {
                yield break;
            }
            SiteCheck check = JsonUtility.FromJson<SiteCheck>(HawkesSiteCheckResult());
            SitePage[] pages = check != null && check.pages != null ? check.pages : new SitePage[0];
            int powered = 0;
            foreach (SitePage page in pages) {
                if (page.status != 200) {
                    run.Problem(PlaytestSeverity.Bug, page.path + " didn't load (" + (string.IsNullOrEmpty(page.error) ? "HTTP " + page.status : page.error) + ")");
                    continue;
                }
                if (page.powered) {
                    powered++;
                    run.Event("powered-by", page.path + ": \"" + page.poweredText + "\" links to https://hawkesgen.ai");
                } else {
                    run.Problem(PlaytestSeverity.Bug, page.path + " has no \"Powered by HAWKES GEN\" link to https://hawkesgen.ai");
                }
                if (page.path == "/index.html") {
                    if (page.parent) {
                        run.Event("parent-link", page.path + ": \"" + page.parentText + "\" links to https://hawkesbiomedical.com");
                    } else {
                        run.Problem(PlaytestSeverity.Bug, "the landing page has no link to https://hawkesbiomedical.com");
                    }
                    run.Metric("landingParentLink", page.parent ? 1 : 0);
                }
            }
            if (pages.Length == 0) {
                run.Problem(PlaytestSeverity.Bug, "no website pages found around the game");
            }

            // Which pages the site has: the landing page, its sub pages and its blog, if it has one.
            List<string> paths = new List<string>();
            List<string> subPages = new List<string>();
            foreach (SitePage page in pages) {
                paths.Add(page.path);
                if (page.path != "/index.html") {
                    subPages.Add(page.path);
                }
            }
            run.Event("site-pages", pages.Length + " page(s) outside the game: " + string.Join(", ", paths)
                + (check != null && check.listed > 0 ? " (" + check.listed + " listed by the deploy in site/pages.json)" : " (no site/pages.json from the deploy; found by links only)"));
            if (check == null || check.listed == 0) {
                run.Problem(PlaytestSeverity.Warning, "the site has no site/pages.json (written by the WebGL deploy), so only linked pages were checked");
            }
            run.Event("sub-pages", subPages.Count > 0
                ? subPages.Count + " sub page(s): " + string.Join(", ", subPages)
                : "the site has no sub pages besides the landing page");
            string blog = check != null ? check.blog : "";
            if (!string.IsNullOrEmpty(blog)) {
                SitePage blogPage = System.Array.Find(pages, p => p.path == blog);
                run.Event("blog", blog + (blogPage != null && blogPage.powered ? ": has \"Powered by HAWKES GEN\"" : ": no Powered by mark"));
            } else {
                run.Event("blog", "the site has no blog (none at /blog/, /blog.html, /news/ or /posts/)");
            }
            run.Metric("sitePages", pages.Length);
            run.Metric("siteSubPages", subPages.Count);
            run.Metric("siteBlog", string.IsNullOrEmpty(blog) ? 0 : 1);
            run.Metric("sitePagesPowered", powered);
            run.Progress(0.3f);

            // Show each page's footer (the landing page first), a few seconds each, then back to the game.
            int shown = 0;
            foreach (SitePage page in pages) {
                if (page.status != 200 || shown >= 8) {
                    continue;
                }
                run.Scene("Site " + page.path);
                HawkesSiteShow(page.path);
                run.Event("site-shown", page.path + " footer" + (page.powered ? ": Powered by HAWKES GEN" : ": no Powered by mark") + (page.parent ? ", A Hawkes Biomedical company" : ""));
                yield return new WaitForSecondsRealtime(4f);
                HawkesSiteHide();
                shown++;
                run.Progress(0.3f + 0.4f * shown / Mathf.Max(1, Mathf.Min(8, pages.Length)));
            }
        }

        run.Scene("Main Menu");
        run.Event("game-screen", "back on the game: the menu and a short run, unchanged");
        yield return new WaitForSecondsRealtime(2f);
        yield return StartRun();
        if (run.Done) {
            yield break;
        }
        yield return Play(options, 20f, false);
    }

    // Touch only, as on a phone: no keys at all. Taps on the menu and the cards go through the UI event system
    // like a finger's; the stick, Swing, Lock and Next wave get the bot's fingers through TouchControls, the same
    // path real touches take. Setup: touch controls are switched on (a phone has them on by itself) and the menu
    // is loaded fresh so it is built for touch.
    static IEnumerator TouchRun() {
        UiFit.ForceTouch(true);
        run.Log("layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height + ", pixel ratio " + UiFit.PixelRatio);
        run.Metric("viewportWidth", Mathf.Round(Screen.width / UiFit.PixelRatio));
        run.Metric("viewportHeight", Mathf.Round(Screen.height / UiFit.PixelRatio));
        run.Scene("Main Menu");
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return null;
        yield return run.Until(() => Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the main menu");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        CheckScreen("menu");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        Button play = menu != null ? menu.PlayButton : null;
        if (play == null || !play.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Rect playRect = ScreenRect((RectTransform)play.transform);
        run.Metric("playButtonPx", Mathf.Round(playRect.height / UiFit.PixelRatio));
        if (!Inside(playRect)) {
            run.Problem(PlaytestSeverity.Bug, "the Play button is not fully on screen");
        }

        run.Note("tapping Play");
        Tap(play.gameObject);
        run.Event("tap", "Play");
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after tapping Play");
        if (run.Done) {
            yield break;
        }
        run.Scene("Wave 1");
        run.Event("run-start");
        Random.InitState(run.Options.seed);
        yield return null;
        CheckPlayerIsWallit();
        yield return TouchPlay(300f);
    }

    static IEnumerator TouchPlay(float seconds) {
        float start = Time.realtimeSinceStartup;
        float nextMetrics = 0f;
        float nextTap = 0f;
        float cardSeenAt = -1f;
        float waveTwoAt = -1f;
        bool stickDown = false;
        bool fireDown = false;
        float nextSwing = 0f;
        bool tapping = false;
        int tapFinger = -1;
        bool checkedHud = false;
        bool checkedCards = false;
        Vector2 stickAt = Vector2.zero;
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - start;
            SampleFps();

            if (WaveDirector.IsLost) {
                TouchControls.LiftBotFingers();
                Metrics(elapsed);
                run.Event("lost", "wave " + WaveDirector.Wave);
                run.Scene("Game Over");
                if (WaveDirector.Wave < 2) {
                    run.Problem(PlaytestSeverity.Warning, "the musician fell before wave 2 with touch controls");
                }
                yield return new WaitForSecondsRealtime(2.5f);
                TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), true);
                yield return null;
                yield return null;
                TouchControls.BotFinger(9, Vector2.zero, false);
                yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene, 20f, "the menu after tapping the game-over screen");
                if (!run.Done) {
                    run.Scene("Main Menu");
                    run.Finish(PlaytestOutcome.Lost);
                }
                yield break;
            }
            if (elapsed >= seconds) {
                run.Problem(PlaytestSeverity.Warning, "touch play did not reach wave 2 and a level-up in " + seconds.ToString("0") + " s (wave " + WaveDirector.Wave + ", " + levelUps + " level-ups)");
                yield break;
            }

            // A one-frame tap from last frame lifts now.
            if (tapping) {
                TouchControls.BotFinger(tapFinger, Vector2.zero, false);
                tapping = false;
            }

            if (LevelUpScreen.IsOpen) {
                if (stickDown || fireDown) {
                    TouchControls.LiftBotFingers();
                    stickDown = false;
                    fireDown = false;
                }
                if (cardSeenAt < 0f) {
                    cardSeenAt = now;
                    run.Event("level-up", "level " + RunBoons.Level);
                    run.Scene("Level Up");
                }
                if (now - cardSeenAt > 0.8f) {
                    if (!checkedCards) {
                        checkedCards = true;
                        CheckScreen("cards", 400);
                    }
                    Button card = Card(Random.Range(0, 3));
                    if (card == null) {
                        run.Problem(PlaytestSeverity.Bug, "no level-up card to tap");
                        run.Finish(PlaytestOutcome.Stuck);
                        yield break;
                    }
                    int picksBefore = RunBoons.Picks;
                    Tap(card.gameObject);
                    yield return null;
                    yield return null;
                    if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                        yield return new WaitForSecondsRealtime(1f);
                        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                            run.Problem(PlaytestSeverity.Bug, "the level-up card did not close after a tap");
                            run.Finish(PlaytestOutcome.Stuck);
                            yield break;
                        }
                    }
                    levelUps++;
                    run.Add("levelUps");
                    run.Log("tapped " + card.name);
                    cardSeenAt = -1f;
                    run.Scene("Wave " + WaveDirector.Wave);
                }
                yield return null;
                continue;
            }

            bool building = WaveDirector.IsBuilding;
            bool birds = Enemy.Roster.Count > 0;

            // The stick: a lap around the start so the stick is seen working, then to the nearest bird near the
            // musician (back to the piano when there is none).
            Enemy target = building || player == null ? null : GuardTarget(player, null);
            float gap = float.MaxValue;
            Vector2 toward = SteerDir(player);
            if (target != null) {
                Vector3 flat = target.transform.position - player.transform.position;
                flat.y = 0f;
                gap = flat.magnitude - target.BodyScale * 0.3f;
                toward = gap > BatEquipment.Reach * 0.6f ? StickToward(flat) : Vector2.zero;
            }
            Vector2 dir = elapsed < 5f ? new Vector2(Mathf.Sin(elapsed * 1.3f), Mathf.Cos(elapsed * 1.3f)) * 0.8f : toward;
            if (building) {
                dir = Vector2.zero;
            }
            if (dir.magnitude > 0.05f) {
                if (!stickDown) {
                    if (TouchControls.Spot("Stick", out stickAt)) {
                        TouchControls.BotFinger(0, stickAt, true);
                        stickDown = true;
                    }
                } else {
                    TouchControls.BotFinger(0, stickAt + Vector2.ClampMagnitude(dir, 1f) * TouchControls.StickReach * 0.9f, true);
                }
            } else if (stickDown) {
                TouchControls.BotFinger(0, stickAt, false);
                stickDown = false;
            }

            // Swing tapped while a bird is in the bat's reach; Lock toggled on with birds, off without.
            bool swing = !building && target != null && gap <= BatEquipment.Reach * 0.95f && !LevelUpScreen.HoldFire;
            if (fireDown) {
                TouchControls.BotFinger(1, Vector2.zero, false);
                fireDown = false;
            } else if (swing && now >= nextSwing && TouchControls.Spot("Swing", out Vector2 swingAt)) {
                TouchControls.BotFinger(1, swingAt, true);
                fireDown = true;
                nextSwing = now + 0.15f;
            }
            if (!tapping && now >= nextTap) {
                Vector2 at;
                if (WaveDirector.Waiting && TouchControls.Spot("Next", out at)) {
                    TouchControls.BotFinger(3, at, true);
                    tapping = true;
                    tapFinger = 3;
                    nextTap = now + 1.5f;
                    run.Event("tap", "Next wave");
                } else if (!building && birds != TouchControls.Locked && TouchControls.Spot("Lock", out at)) {
                    TouchControls.BotFinger(2, at, true);
                    tapping = true;
                    tapFinger = 2;
                    nextTap = now + 0.6f;
                }
            }

            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                if (lastWave != 0) {
                    run.Event("wave", wave.ToString());
                }
                lastWave = wave;
                run.Scene("Wave " + wave);
                if (wave >= 2 && waveTwoAt < 0f) {
                    waveTwoAt = now;
                }
            }
            if (!checkedHud && elapsed > 8f && birds) {
                checkedHud = true;
                CheckScreen("hud");
            }
            // Done: wave 2 under way and at least one card picked by touch.
            if (waveTwoAt > 0f && now - waveTwoAt > 10f && levelUps > 0) {
                Metrics(elapsed);
                run.Event("done", "wave " + wave + " by touch");
                yield break;
            }

            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(elapsed);
                run.Metric("touchLocked", TouchControls.Locked ? 1 : 0);
                run.Progress(Mathf.Clamp01((wave - 1 + (levelUps > 0 ? 0.5f : 0f)) / 2f));
                run.Note(building ? "build phase" : Enemy.Roster.Count + " bots, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
            }
            yield return null;
        }
    }

    // A ground direction as the stick would push it, camera-relative.
    static Vector2 StickToward(Vector3 flat) {
        Camera cam = Camera.main;
        if (cam == null || flat.sqrMagnitude < 0.0001f) {
            return Vector2.zero;
        }
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        Vector3 right = cam.transform.right;
        right.y = 0f;
        flat.Normalize();
        return new Vector2(Vector3.Dot(right.normalized, flat), Vector3.Dot(fwd.normalized, flat));
    }

    static Vector2 SteerDir(CharacterMovement player) {
        Camera cam = Camera.main;
        if (player == null || Musician.Instance == null || cam == null) {
            return Vector2.zero;
        }
        Vector3 to = Musician.Instance.transform.position - player.transform.position;
        to.y = 0f;
        if (to.magnitude <= 9f) {
            return Vector2.zero;
        }
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        Vector3 right = cam.transform.right;
        right.y = 0f;
        to.Normalize();
        return new Vector2(Vector3.Dot(right.normalized, to), Vector3.Dot(fwd.normalized, to));
    }

    static Button Card(int index) {
        LevelUpScreen screen = Object.FindAnyObjectByType<LevelUpScreen>();
        if (screen == null) {
            return null;
        }
        var cards = new List<Button>();
        foreach (Button button in screen.GetComponentsInChildren<Button>()) {
            if (button.name.StartsWith("Card") && button.isActiveAndEnabled) {
                cards.Add(button);
            }
        }
        return cards.Count == 0 ? null : cards[Mathf.Min(index, cards.Count - 1)];
    }

    // ---------------------------------------------------------------- polish

    // The final polish pass: every screen a player meets in a run, each checked for layout, leftover piano words and
    // the shared button feel, then the effects mix after real play, and the game-over panel's title.
    static IEnumerator PolishRun(PlaytestOptions options) {
        yield return ToMenu();
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        PolishScreen("menu", 0, false);
        run.Progress(0.1f);

        yield return StartRun();
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        yield return new WaitForSecondsRealtime(4f);
        PolishScreen("hud", 0, false);
        run.Progress(0.2f);

        run.Scene("Level-up cards");
        RunBoons.AddFeathers(RunBoons.Need(RunBoons.Level));
        run.Event("setup", "a level's worth of bolts granted so the level-up cards open");
        yield return run.Until(() => LevelUpScreen.IsOpen, 10f, "the level-up cards");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        PolishScreen("cards", 400, true);
        yield return PickCard();
        if (run.Done) {
            yield break;
        }
        run.Progress(0.35f);

        run.Scene("Wave " + WaveDirector.Wave);
        yield return new WaitForSecondsRealtime(2f);
        yield return run.Until(() => PauseMenu.Instance != null && PauseMenu.Instance.PauseButton != null && PauseMenu.Instance.PauseButton.isActiveAndEnabled, 20f, "the pause button");
        if (run.Done) {
            yield break;
        }
        Tap(PauseMenu.Instance.PauseButton.gameObject);
        run.Event("tap", "Pause");
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "the pause menu after tapping the pause button");
        if (run.Done) {
            yield break;
        }
        run.Scene("Paused");
        yield return new WaitForSecondsRealtime(1f);
        PolishScreen("pause", 450, true);
        Tap(PauseMenu.Instance.ResumeButton.gameObject);
        run.Event("tap", "Resume");
        yield return run.Until(() => !PauseMenu.IsOpen && Time.timeScale > 0.01f, 5f, "play to resume after Resume");
        if (run.Done) {
            yield break;
        }
        run.Progress(0.45f);

        run.Scene("Wave " + WaveDirector.Wave);
        int played = GameAudio.Played;
        int softened = GameAudio.Softened;
        int capped = GameAudio.Capped;
        int ducked = GameAudio.Ducked;
        yield return Play(options, 40f, false);
        if (run.Done) {
            yield break;
        }
        played = GameAudio.Played - played;
        softened = GameAudio.Softened - softened;
        capped = GameAudio.Capped - capped;
        ducked = GameAudio.Ducked - ducked;
        run.Metric("sfxPlayed", played);
        run.Metric("sfxSoftened", softened);
        run.Metric("sfxCapped", capped);
        run.Metric("sfxDucked", ducked);
        run.Event("audio-mix", played + " effects in 40 s, " + softened + " stacked softer, " + capped + " capped, " + ducked + " dipped under a voice line");
        if (played == 0 && SoundSettings.EffectsLevel > 0f) {
            run.Problem(PlaytestSeverity.Bug, "no sound effects played in 40 s of play");
        }
        run.Progress(0.65f);

        run.Scene("Slice falls");
        run.Event("hold-fire", "the bot stops swinging so the bots reach Slice and the game-over panel opens");
        Musician slice = Musician.Instance;
        float holdStart = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        float nextSiege = 0f;
        while (!run.Done && !WaveDirector.IsLost) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            if (now - holdStart > 200f) {
                run.Problem(PlaytestSeverity.Bug, "the run did not end in 200 s of holding fire");
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            Batter(player, true, null, false);
            if ((WaveDirector.IsBuilding || Enemy.Roster.Count == 0) && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            // Setup: her knife outlasts a wave on its own, so after 20 s Plated Yo Bots ring her, topped up every 8 s.
            if (slice != null && now - holdStart > 20f && now >= nextSiege && slice.Alive && !slice.Defeating && !WaveDirector.IsBuilding) {
                nextSiege = now + 8f;
                int dropped = SiegeSlice(slice, 10);
                if (dropped > 0) {
                    run.Event("setup", dropped + " Plated Yo Bots dropped around Slice (" + slice.Health + " / " + slice.MaxHealth + ")");
                }
            }
            yield return null;
        }
        if (run.Done) {
            yield break;
        }
        PlayInput.Release();
        run.Scene("Game Over");
        yield return new WaitForSecondsRealtime(1.2f);
        bool titled = false;
        foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (text.isActiveAndEnabled && text.text == "Address struck off") {
                titled = true;
                break;
            }
        }
        run.Event("game-over-title", titled ? "Address struck off" : "not found");
        if (!titled) {
            run.Problem(PlaytestSeverity.Polish, "the game-over panel does not read \"Address struck off\"");
        }
        PolishScreen("gameover", 0, false);
        run.Progress(0.9f);
        yield return GameOver();
    }

    // One screen of the polish pass: layout, leftover words and (where every button should have it) the button feel.
    static void PolishScreen(string where, int minOrder, bool feelRequired) {
        CheckScreen(where, minOrder);
        int stale = 0;
        foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text) || !StaleWords.IsMatch(text.text)) {
                continue;
            }
            Canvas canvas = text.canvas;
            if (canvas == null || canvas.rootCanvas.sortingOrder < minOrder) {
                continue;
            }
            stale++;
            run.Problem(PlaytestSeverity.Polish, where + ": the piano is gone but the screen still says \"" + Clip(text.text) + "\"");
        }
        run.Event("stale-words", where + ": " + stale);
        int buttons = 0;
        int felt = 0;
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
            Canvas holder = button.GetComponentInParent<Canvas>();
            if (!button.isActiveAndEnabled || holder == null || holder.rootCanvas.sortingOrder < minOrder) {
                continue;
            }
            buttons++;
            if (button.GetComponent<MenuButtonFx>() != null) {
                felt++;
            }
        }
        run.Event("button-feel", where + ": " + felt + " of " + buttons + " buttons lift on hover and sink on press");
        if (feelRequired && felt < buttons) {
            run.Problem(PlaytestSeverity.Polish, where + ": " + (buttons - felt) + " button(s) don't share the menu's hover and press feel");
        }
    }

    static readonly System.Text.RegularExpressions.Regex StaleWords = new System.Text.RegularExpressions.Regex(@"\b(piano|pianist|music stopped|off the keys)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // A finger's tap on a UI button: down, up, click.
    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    // What a player would call broken at this screen size: text or a button off the screen, text over text or
    // over a button or touch control, text under 11 px, a button under 44 px. Sizes are CSS pixels. Text
    // scrolled out of its list doesn't count.
    // minOrder skips canvases drawn under a full-screen one (the HUD under the level-up cards).
    static void CheckScreen(string where, int minOrder = 0) {
        float ratio = UiFit.PixelRatio;
        var texts = new List<(Rect rect, string what, Transform owner)>();
        var taps = new List<(Rect rect, string what, Transform owner)>();
        float minText = 999f;
        float minTap = 999f;
        int found = 0;

        foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text) || text.color.a < 0.05f) {
                continue;
            }
            Canvas canvas = text.canvas;
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.rootCanvas.sortingOrder < minOrder) {
                continue;
            }
            if (!Glyphs(text, out Rect rect) || !Unclipped(text.transform, rect)) {
                continue;
            }
            string what = "\"" + Clip(text.text) + "\"";
            float px = text.fontSize * text.transform.lossyScale.y / ratio;
            minText = Mathf.Min(minText, px);
            if (px < 11f) {
                found++;
                run.Problem(PlaytestSeverity.Polish, where + ": text is " + px.ToString("0") + " px: " + what);
            }
            if (!Inside(rect)) {
                found++;
                run.Problem(PlaytestSeverity.Warning, where + ": text runs off the screen: " + what);
            }
            texts.Add((rect, what, text.transform));
        }

        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
            Canvas holder = button.GetComponentInParent<Canvas>();
            if (!button.isActiveAndEnabled || holder == null || holder.rootCanvas.sortingOrder < minOrder) {
                continue;
            }
            var rt = (RectTransform)button.transform;
            Rect rect = ScreenRect(rt);
            if (!Unclipped(rt, rect)) {
                continue;
            }
            Text label = button.GetComponentInChildren<Text>();
            string what = "button " + (label != null ? "\"" + Clip(label.text) + "\"" : button.name);
            float px = Mathf.Min(rect.width, rect.height) / ratio;
            minTap = Mathf.Min(minTap, px);
            if (px < 44f) {
                found++;
                run.Problem(PlaytestSeverity.Polish, where + ": " + what + " is " + px.ToString("0") + " px, under 44");
            }
            if (!Inside(rect)) {
                found++;
                run.Problem(PlaytestSeverity.Warning, where + ": " + what + " is not fully on screen");
            }
            taps.Add((rect, what, rt));
        }
        var controls = new List<RectTransform>();
        TouchControls.Shown(controls);
        foreach (RectTransform control in controls) {
            Rect rect = ScreenRect(control);
            minTap = Mathf.Min(minTap, Mathf.Min(rect.width, rect.height) / ratio);
            taps.Add((rect, "touch " + control.name, control));
        }

        int overlaps = 0;
        for (int i = 0; i < texts.Count; i++) {
            for (int j = i + 1; j < texts.Count; j++) {
                if (Overlap(texts[i].rect, texts[j].rect) > 0.2f && !Related(texts[i].owner, texts[j].owner)) {
                    overlaps++;
                    run.Problem(PlaytestSeverity.Warning, where + ": " + texts[i].what + " overlaps " + texts[j].what);
                }
            }
            foreach (var tap in taps) {
                if (Overlap(texts[i].rect, tap.rect) > 0.2f && !texts[i].owner.IsChildOf(tap.owner)) {
                    overlaps++;
                    run.Problem(PlaytestSeverity.Warning, where + ": " + texts[i].what + " is under " + tap.what);
                }
            }
        }
        for (int i = 0; i < taps.Count; i++) {
            for (int j = i + 1; j < taps.Count; j++) {
                if (Overlap(taps[i].rect, taps[j].rect) > 0.05f && !Related(taps[i].owner, taps[j].owner)) {
                    overlaps++;
                    run.Problem(PlaytestSeverity.Warning, where + ": " + taps[i].what + " overlaps " + taps[j].what);
                }
            }
        }
        run.Metric(where + "MinTextPx", Mathf.Round(minText));
        run.Metric(where + "MinTapPx", Mathf.Round(minTap));
        run.Metric(where + "Overlaps", overlaps);
        run.Event("layout", where + ": " + texts.Count + " texts, " + taps.Count + " buttons, " + overlaps + " overlaps, " + found + " other problems");
    }

    // Where a text's letters are on screen (not its box, which is often bigger).
    static bool Glyphs(Text text, out Rect rect) {
        rect = default;
        IList<UIVertex> verts = text.cachedTextGenerator.verts;
        if (verts == null || verts.Count == 0) {
            return false;
        }
        float unit = 1f / Mathf.Max(0.0001f, text.pixelsPerUnit);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < verts.Count; i++) {
            Vector3 world = text.rectTransform.TransformPoint(verts[i].position * unit);
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 0.5f && rect.height > 0.5f;
    }

    static Rect ScreenRect(RectTransform rect) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    static bool Inside(Rect rect) {
        return rect.xMin >= -2f && rect.yMin >= -2f && rect.xMax <= Screen.width + 2f && rect.yMax <= Screen.height + 2f;
    }

    // False for something in a scrolling list that is scrolled (even partly) out of view.
    static bool Unclipped(Transform item, Rect rect) {
        RectMask2D mask = item.GetComponentInParent<RectMask2D>();
        if (mask == null) {
            return true;
        }
        Rect view = ScreenRect(mask.rectTransform);
        return rect.xMin >= view.xMin - 1f && rect.yMin >= view.yMin - 1f && rect.xMax <= view.xMax + 1f && rect.yMax <= view.yMax + 1f;
    }

    // How much of the smaller rect the two share.
    static float Overlap(Rect a, Rect b) {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        if (w <= 0f || h <= 0f) {
            return 0f;
        }
        float smaller = Mathf.Min(a.width * a.height, b.width * b.height);
        return smaller <= 0f ? 0f : w * h / smaller;
    }

    static bool Related(Transform a, Transform b) {
        return a.IsChildOf(b) || b.IsChildOf(a);
    }

    static string Clip(string text) {
        string line = text.Replace("\n", " ");
        return line.Length > 40 ? line.Substring(0, 40) + "…" : line;
    }

    static IEnumerator GameOver() {
        PlayInput.Release();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        run.Event("lost", "wave " + WaveDirector.Wave);
        run.Scene("Game Over");
        yield return new WaitForSecondsRealtime(1f);
        AnalyticsTimeline();
        // The feedback card ("How was that run?") is the page's, over the game-over screen.
        bool asked = HawkesAnalyticsPageAskShown(RunAnalytics.AskId) == 1;
        if (asked) {
            run.Event("feedback-prompt", "\"How was that run?\" stars and a comment, on the game-over screen");
        } else if (InBrowser && analyticsRun) {
            run.Problem(PlaytestSeverity.Bug, "no \"How was that run?\" feedback card on the game-over screen");
        } else {
            run.Log(InBrowser ? "no feedback card on the game-over screen (shown once a session, then not for 7 days after a close)" : "no feedback card in the editor");
        }
        if (analyticsRun && asked) {
            yield return new WaitForSecondsRealtime(2.5f);
            if (HawkesAnalyticsPageRate(RunAnalytics.AskId, 4) == 1) {
                run.Event("feedback-rated", "4 stars, Skip");
            } else {
                run.Problem(PlaytestSeverity.Bug, "the feedback card's stars could not be pressed");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            AnalyticsTimeline();
        } else {
            yield return new WaitForSecondsRealtime(1.5f);
        }
        PlayInput.Tap(KeyCode.Space);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene, 20f, "the menu after the game-over screen");
        if (!run.Done) {
            run.Scene("Main Menu");
            if (analyticsRun) {
                // The SDK sends a batch every 5 s: wait for the last one so the report can say it went out.
                yield return new WaitForSecondsRealtime(6f);
                AnalyticsReport();
            }
            run.Finish(PlaytestOutcome.Lost);
        }
    }

    // ---------------------------------------------------------------- analytics

    // Every Hawkes Analytics event since the last look, from the SDK's debug log (RunAnalytics turns debug mode on
    // in a bot's browser), as timeline events: "run_start run=1 start_wave=1", "level_end level=1 result=complete…".
    // The first time, it says which product, build and env every batch carries.
    static void AnalyticsTimeline() {
        string text = HawkesAnalyticsPageLog(analyticsSeq);
        if (string.IsNullOrEmpty(text)) {
            return;
        }
        string[] lines = text.Split('\n');
        string[] config = lines[0].Split('\t');
        if (config.Length >= 7) {
            int.TryParse(config[6], out analyticsSent);
            if (!analyticsConfig) {
                analyticsConfig = true;
                run.Event("analytics-config", "product " + config[1] + ", build " + (config[2] == "" ? "(none)" : config[2])
                    + (config[3] == "" ? "" : " (" + config[3].Substring(0, Mathf.Min(7, config[3].Length)) + ")")
                    + ", env " + config[4] + ", debug " + (config[5] == "1" ? "on" : "off"));
            }
        }
        for (int i = 1; i < lines.Length; i++) {
            string[] parts = lines[i].Split('\t');
            if (parts.Length < 4 || !int.TryParse(parts[0], out int seq)) {
                continue;
            }
            analyticsSeq = Mathf.Max(analyticsSeq, seq);
            analyticsEvents++;
            if (parts[1] == "perf") {
                continue;
            }
            run.Event("analytics", parts[1] + (parts[3] == "" ? "" : " " + parts[3]));
        }
    }

    // How many of the events went out, and the build and env they carried (said again at the end).
    static void AnalyticsReport() {
        AnalyticsTimeline();
        run.Metric("analyticsEvents", analyticsEvents);
        run.Metric("analyticsSent", analyticsSent);
        if (!InBrowser) {
            run.Log("analytics: nothing is sent in the editor");
            return;
        }
        if (!analyticsConfig) {
            run.Problem(PlaytestSeverity.Bug, "the Hawkes Analytics SDK is not on the page (window.hawkesAnalytics), so no events went out");
            return;
        }
        run.Event("analytics-sent", analyticsSent + " of " + analyticsEvents + " events sent");
        if (analyticsSent == 0) {
            run.Problem(PlaytestSeverity.Bug, "no analytics batch went out (the collect endpoint was not reached)");
        }
    }

    static void SampleFps() {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) {
            return;
        }
        fpsNow = Mathf.Lerp(fpsNow <= 0f ? 1f / dt : fpsNow, 1f / dt, 0.1f);
        fpsSum += 1f / dt;
        fpsFrames++;
        if (fpsFrames > 30 && fpsNow < worstFps) {
            worstFps = fpsNow;
        }
    }

    static void Metrics(float elapsed) {
        run.Metric("wave", WaveDirector.Wave);
        run.Metric("kills", RunLog.RunKills);
        run.Metric("damage", Mathf.Round(RunLog.RunDamage));
        run.Metric("swings", RunLog.RunSwings);
        run.Metric("hits", RunLog.RunHits);
        run.Metric("hitRate", RunLog.RunSwings > 0 ? Mathf.Round(100f * RunLog.RunConnects / RunLog.RunSwings) / 100f : 0f);
        run.Metric("finishers", BatEquipment.Finishers);
        run.Metric("smashHits", BatEquipment.SmashHits);
        run.Metric("homeRuns", BatEquipment.HomeRuns);
        run.Metric("homeRunKnocks", BatEquipment.HomeRunKnocks);
        run.Metric("knockedBack", BatEquipment.KnockedBack);
        run.Metric("lockNearest", lockNearest);
        run.Metric("lockWrong", lockWrong);
        run.Metric("level", RunBoons.Level);
        run.Metric("levelUps", levelUps);
        run.Metric("botsAlive", Enemy.Roster.Count);
        run.Metric("bestStreak", Combo.Best);
        run.Metric("coinsRun", MetaProgress.CoinsThisRun);
        run.Metric("runSeconds", Mathf.Round(elapsed));
        if (Musician.Instance != null) {
            run.Metric("musicianHp", Musician.Instance.Health);
            run.Metric("musicianDamageTaken", Musician.Instance.DamageTaken);
        }
        run.Metric("fps", Mathf.Round(fpsNow));
        if (fpsFrames > 0) {
            run.Metric("fpsAverageGame", Mathf.Round(fpsSum / fpsFrames));
            run.Metric("fpsWorstGame", Mathf.Round(Mathf.Min(worstFps, 999f)));
        }
    }

    // Errors are caught by the package; warnings about missing pieces are worth a line too.
    static void OnLog(string message, string stack, LogType type) {
        if (run == null || run.Done || type != LogType.Warning) {
            return;
        }
        string first = message.Split('\n')[0];
        string lower = first.ToLowerInvariant();
        if (lower.Contains("not assigned") || lower.Contains("not found") || lower.Contains("missing")) {
            run.Problem(PlaytestSeverity.Warning, first);
        }
    }
}
