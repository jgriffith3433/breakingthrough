using System.Collections.Generic;
using UnityEngine;

// The story as data. Docs/Story.md explains it; change a line in both. A line is "<speaker>|<text>" with speaker
// N narrator, W Wallit (talks money), S Slice (clipped), Y Yo Bot (chirpy), K the Courier, T title card.
public static class Story {
    // Wave 30 is the first Spider Bot and carries the order. Clearing it plays the finale.
    public const int FinalWave = 30;

    // The first seconds of an Arcade run are not text: ColdOpen.cs plays them (Docs/Story.md, "The intro"). What the old
    // six pages said now lives in the lore log (Lore.cs, "The First Night").

    // Arcade run-end: Wallit and Slice trade one short exchange when Slice falls (Docs/Story.md, "Run-end exchanges").
    // Id, condition key (RunEndTalk), Wallit's line, Slice's line. Order is priority: first matching id not used in the
    // last five runs plays. Voices: Wallit money, Slice clipped. Never reorder without updating RunEndTalk.
    public class RunEnd {
        public string Id;
        public string When;
        public string Wallit;
        public string Slice;
    }

    public static readonly RunEnd[] RunEnds = {
        new RunEnd { Id = "early", When = "early", Wallit = "Short shift. No tip.", Slice = "Again." },
        new RunEnd { Id = "yo_bot", When = "yo_bot", Wallit = "Chirpy took the ring.", Slice = "Noted." },
        new RunEnd { Id = "spider", When = "spider", Wallit = "Six legs. One bill.", Slice = "Bigger next." },
        new RunEnd { Id = "boss", When = "boss", Wallit = "Big one cashed out.", Slice = "Hold." },
        new RunEnd { Id = "new_best", When = "new_best", Wallit = "New high. Still short.", Slice = "Closer." },
        new RunEnd { Id = "nearly", When = "nearly", Wallit = "You almost fell.", Slice = "I didn't." },
        new RunEnd { Id = "near_decade", When = "near_decade", Wallit = "One stamp from the next.", Slice = "Then take it." },
        new RunEnd { Id = "act2", When = "act2", Wallit = "Plates cost extra.", Slice = "Cover me." },
        new RunEnd { Id = "act3", When = "act3", Wallit = "Order's still out.", Slice = "Denied later." },
        new RunEnd { Id = "payday", When = "payday", Wallit = "Purse is fat. Ring's gone.", Slice = "Spend it right." },
        new RunEnd { Id = "longball", When = "longball", Wallit = "Returned a few.", Slice = "Keep swinging." },
        new RunEnd { Id = "default", When = "default", Wallit = "No refunds.", Slice = "Clean. Again." },
    };

    // The hub: the circle between chapters (Docs/Story.md, "The hub"). Said on arrival, by chapters cleared (index 0 is one
    // cleared). Two lines each, "<speaker>|<text>": Wallit money, Slice clipped.
    public static readonly string[][] HubArrival = {
        new[] { "W|One notice, returned. Where's my tip?", "S|Board. Next case." },
        new[] { "W|Door's down. Still no sender.", "S|The circle's yours. The lease is paid. Board." },
        new[] { "W|Feed's broken. Their pay is mine now.", "S|Second ring holds. Next." },
        new[] { "W|Refused the paper three times. Put that in writing.", "S|Noted. Board." },
        new[] { "W|The order's a form. A form can be answered.", "S|Then answer it. Board." },
        new[] { "W|It's filed. All of it.", "S|Clean. Replays are on the board." },
    };

    // What Slice and Wallit say whenever he stands in her ring (HubAmbience), by chapters cleared (index 0 is one cleared). Each stage has
    // two exchanges that take turns, "<speaker>|<text>": Wallit money, Slice clipped.
    public static readonly string[][][] HubRing = {
        new[] { new[] { "W|Standing in your ring. Rent's due.", "S|Paid. In kind. Stay." }, new[] { "W|One notice down. Billing it.", "S|Sender unknown. Billing void." } },
        new[] { new[] { "W|The gate stamped me back. Interest owed.", "S|Door's yours. Stay clear of the lamp." }, new[] { "W|You held the ring while I was out.", "S|Always. Don't thank me." } },
        new[] { new[] { "W|Belt's quiet. I miss the noise. Cheap.", "S|Quiet is rent. Take it." }, new[] { "W|Foreman said \"not finished.\" Neither am I.", "S|Good. Keep swinging." } },
        new[] { new[] { "W|Three refusals. Clean ones.", "S|Counted. Four would be rude." }, new[] { "W|The Server still holds out its paper.", "S|Let it. Ring is full." } },
        new[] { new[] { "W|All those forms. Blank now.", "S|Blank is a fresh account." }, new[] { "W|Registrar read slow. I read faster.", "S|You did. Don't boast." } },
        new[] { new[] { "W|It's filed. I can stand here free.", "S|Clean. Stand as long as you like." }, new[] { "W|Number on the wall. Mine?", "S|Yours. Keep the receipt." } },
    };

    // The hub's four lodgers (Docs/Story.md, "The lodgers"): things that should not exist and found the circle. What each says
    // changes with the chapters the file has cleared (Lines[cleared - 1], so six per lodger). Chime also gives the errands:
    // one per chapter, "<ask>", fetched from the hub's floor, paid in XP and a lost thing kept in the case file.
    public class Lodger {
        public string Id;
        public string Name;
        public string Colour;
        public string[] Lines;
    }

    public static readonly Lodger[] Lodgers = {
        new Lodger { Id = "tick", Name = "TICK", Colour = "#B9C7E6", Lines = new[] {
            "Tick. Notice delivered on the hour, as written. Slice stood. Good hour.",
            "Tock. The gate stamped you back. Doors keep time too. Yours is still open.",
            "Slow belt, red lamp. Someone's hour was stopped there. Mine nearly was.",
            "Three refusals. I counted each one. A paper refused thrice is still a paper. Mind it.",
            "The order has no signature. Nobody was ever keeping its time. Strange, to know that.",
            "It's late and it's filed. The hour's yours now, Wallit. Spend it.",
        } },
        new Lodger { Id = "brim", Name = "BRIM", Colour = "#E6B7A1", Lines = new[] {
            "Did you ever have a head? I had one once. I miss the weight.",
            "A postmark, half yours, torn. Half a name is a better start than I had.",
            "Hats on that belt. Dozens. I looked for mine. I didn't look away.",
            "The spiders bow, did you see? Politely. I'd tip my brim, if I could tip.",
            "DENIED. Good stamp. The forms went blank, and blank is a fresh start.",
            "Filed. I have an address now: Brim, the circle, left of the desk. Grand.",
        } },
        new Lodger { Id = "spare", Name = "SPARE", Colour = "#8CFFB3", Lines = new[] {
            "Hello! Uploading victory! I am... mostly uploading! Chirp!",
            "Hello! Plated Alpha, stamped INVALID! Sorry! I was told to cheer!",
            "Hello! Hooks! I know hooks! I was on one! Not finished, now free!",
            "Chirp! Spiders en route! En route to... I have no route. I live here!",
            "Hello! Every page the same! I read them all! Wallit read faster! Chirp!",
            "Hello! Filed! Uploading... nothing! I'm just here! Chirp chirp!",
        } },
        new Lodger { Id = "chime", Name = "CHIME", Colour = "#FFD54F", Lines = new[] {
            "Ding. Notice returned, tip owed. I've a thing that wants fetching.",
            "Ding. The gate. I heard it ring from here. Another errand, if you'll carry it.",
            "Ding. The belt carried a bell like me once. Fetch this and I'll pay.",
            "Ding. Three refusals, and I rang for each. One more errand. It pays.",
            "Ding. Blank forms. Everyone lost something in those. Fetch one back.",
            "Ding. Filed. Last errand. It pays in full, as the books go.",
        } },
    };

    // Chime's errands, one per chapter cleared: the lost thing, what Chime says when sending Wallit, what Chime says at the
    // hand-over, and the XP it pays. The thing stays in the case file (Slot.errands) as a collectible.
    public class Errand {
        public string Thing;
        public string Ask;
        public string Thanks;
        public int Xp;
    }

    public static readonly Errand[] Errands = {
        new Errand { Thing = "Brass Clapper", Ask = "My clapper rolled under the carpet. Bring it. 20 XP.", Thanks = "Ding! My own tongue. Paid in full: 20 XP.", Xp = 20 },
        new Errand { Thing = "Torn Postmark", Ask = "A scrap of postmark came in on your boots. Find it. 30 XP.", Thanks = "Ding! The other half of a name. Paid: 30 XP.", Xp = 30 },
        new Errand { Thing = "Belt Token", Ask = "A brass token from the belt, dropped near the ring. Bring it. 40 XP.", Thanks = "Ding! Off the belt, back in hand. Paid: 40 XP.", Xp = 40 },
        new Errand { Thing = "Lamp Wick", Ask = "A wick from the Long Walk's lamps is lying about. Fetch it. 50 XP.", Thanks = "Ding! One lamp, lit again. Paid: 50 XP.", Xp = 50 },
        new Errand { Thing = "Blank Form", Ask = "One of the blank forms blew in. Bring it here. 60 XP.", Thanks = "Ding! Blank, and mine. Paid: 60 XP.", Xp = 60 },
        new Errand { Thing = "Filed Stub", Ask = "The stub of the last filing is somewhere in the room. Fetch it. 80 XP.", Thanks = "Ding! Filed and kept. Paid: 80 XP.", Xp = 80 },
    };

    public const string ChimeIdle = "Ding. Nothing owed. Clear another chapter and I'll find something lost.";
    public const string ChimeWaiting = "Ding. It's still out there: ";

    // Chapter 1's level (LevelYard, the Sorting Yard): the Courier at the drop, after the Clearing Crew (Docs/Story.md,
    // "Chapter 1's boss: the Courier"). It arrives with Chapters[0].BossLines and falls with Chapters[0].DefeatLines; these are
    // the fight's own few words, each one a banked voice clip (Resources/Voice/<cast>_<slug>.mp3), said once.
    public static readonly string[] CourierBreath = {
        "S|Big one next. Be ready.",                              // the crew is down and Slice walks with him
        "W|Something big's coming. I can smell the invoice.",     // the crew is down and he is alone
    };
    public const string CourierTip = "S|It's reading it out.";                          // the first notice goes up
    public const string CourierTaken = "W|Nobody touches her.";                         // he stood in the red for Slice
    public const string CourierBroken = "W|Not on my tab.";                             // he broke the wind-up
    public const string CourierSecond = "Y|Second notice! This address is still unknown.";   // half its health gone
    public const string CourierCall = "Y|Return to sender! Return to sender!";          // the Yo Bots it calls

    // Campaign chapters (Docs/CampaignBible.md). A chapter is data: where it is, what the objective is, who the boss is,
    // what is said before (Lines) and after (Done). ChapterRun plays it in the game scene with Wallit's own controls.
    public enum ChapterObjective {
        ClearCount,   // Target bots down
        SurviveTime,  // Target seconds with Slice standing
        ReachPlace,   // Wallit walks Target metres from the circle along Bearing to the place
        DefeatBoss,   // the Alpha of Boss falls
        HoldThenBoss, // Target bots down, then the Alpha of Boss (named BossName) falls
        FloorRun,     // walk out through Stops (a scene or lines at each, the last is the lever that breaks the feed), back to the circle, WaveSize bots down, then the named Boss falls
        GateAndBack,  // walk Target metres out to the gate on Bearing (GateScene plays), walk back to the circle, hold Waves waves of WaveSize bots, then the named Boss falls
    }

    // A place on the way out in a FloorRun chapter: metres and bearing from the circle, what it is called, what is said or shown there.
    public struct Stop {
        public float Dist;
        public float Bearing;
        public string Name;
        public string[] Scene;   // a cutscene (B|, C|, N|, W|...) played on arrival; null: Lines are told over the field
        public string[] Lines;   // told over the field when there is no Scene (and after it, if both)
    }

    public struct Chapter {
        public string Id;
        public string Title;
        public string Place;
        public string Map;            // the map scene the chapter loads (ChapterMap.Defs name); its lighting goes with it
        public ChapterObjective Objective;
        public int Target;            // bots, seconds or metres, by Objective
        public float Bearing;         // degrees from the circle, for ReachPlace
        public string Goal;           // what the objective line calls the place or the boss
        public BotKind Boss;          // the Alpha for DefeatBoss
        public int BossHits;          // the named boss's health in bat hits (0: the Alpha's own)
        public string BossName;       // a named boss with its own fight ("The Courier"); null: the plain Alpha
        public string[] BossLines;    // said as the boss arrives
        public string[] DefeatLines;  // the defeat beat, said over the field before the Chapter Complete card
        public BotKind[] Crew;        // the bots that come at the circle during the chapter
        public float Every;           // seconds between bots
        public int Cap;               // most bots alive at once
        public int Power;             // the wave number the bots' strength follows
        public string[] Lines;        // before: shown on the chapter card ("<speaker>|<text>")
        public string[] Done;         // after: shown on the Chapter Complete card
        public bool PlainBoss;        // the named boss is a bigger Alpha with no special fight (not the Courier)
        public int Waves;             // GateAndBack: the waves held at the circle once Wallit is back
        public int WaveSize;          // GateAndBack: bots down to clear each of those waves
        public string[] WaveLines;    // GateAndBack: said as each wave starts
        public string[] GateScene;    // GateAndBack: the cutscene that plays when Wallit reaches the gate
        public Stop[] Stops;          // FloorRun: the places out to the lever, in order
        public bool Walk;             // FloorRun: a causeway walk (Chapter 4): no belt feed to break at the last stop, lamps instead of the Floor's props
        public bool Final;            // HoldThenBoss: the last chapter. Waves x WaveSize bots, then the named boss; clearing it plays Ending, the Finale pages, ADDRESS FILED and the credits (no Next button)
        public string[] Ending;       // Final: the cutscene played when the boss falls, before the Finale pages
        public BotTrait Variant;      // the chapter's new behaviour: every VariantEvery-th crew bot (a Yo Bot kind) is a VariantName with this trait
        public string VariantName;
        public int VariantEvery;
        public string VariantLine;    // said the first time one appears ("<speaker>|<text>")
        public string MiniName;       // a mini-boss midway through the chapter (null: none), with a health bar
        public BotKind MiniKind;
        public int MiniHits;
        public Color MiniTint;
        public string[] MiniLines;    // said as it arrives
        public string Companion;      // a Companion.Defs id that walks with Wallit this chapter (null: none)
        public string[] Cutscene;     // played by Cutscene.cs over the chapter's map when Begin is pressed (null: none)
        public string[] Crossroads;   // played when the boss is called, before its intro: dialogue choices that shape the fight (Q| and K| lines, Choices.cs)
        public string[] PhaseTwo;     // K| lines the companion says when the boss enters its second phase, by choice (null: none)
        public string[] After;        // played when the boss falls, before the closing reel and the card: choices that carry to later chapters
    }

    public static readonly Chapter[] Chapters = {
        new Chapter {
            Id = "return-to-sender", Map = "hollow", Title = "Chapter 1: Return to Sender", Place = "The Moonlit Hollow",
            Objective = ChapterObjective.HoldThenBoss, Target = 6, Goal = "Yo Bots", Boss = BotKind.YoBot, BossName = "The Courier", BossHits = 40,
            Crew = new[] { BotKind.YoBot }, Every = 3.4f, Cap = 4, Power = 1,
            Variant = BotTrait.Sprinter, VariantName = "Dispatch Yo Bot", VariantEvery = 3, VariantLine = "Y|Rush delivery! Rush delivery!",
            Lines = new[] {
                "W|Return to sender. Which sender? I want a refund.",
                "S|No sender. No refund. Stay in the circle.",
            },
            BossLines = new[] {
                "Y|Special delivery! Notice of striking!",
                "S|The Courier. It reads the order out, then it lands.",
                "W|Then it can read me the bill.",
            },
            DefeatLines = new[] {
                "K|Notice... undelivered... uploading... defeat?",
                "W|Return to sender.",
                "S|Clean.",
            },
            // The Stamp (Docs/CampaignBible.md). Lines are "<speaker>|<text>": N narration, W Wallit, S Slice, Y Yo Bot.
            // "C|<focus>" moves the camera (wallit, slice, wide) for the lines after it; "B|on" and "B|off" slide the
            // letterbox bars in and out.
            Cutscene = new[] {
                "B|on",
                "C|wide",
                "N|THE STAMP\nOut of the dark comes a stamp: RETURN TO SENDER. Its ink is the only bright thing in the Hollow.",
                "C|wallit",
                "N|It lands on leather. A wallet hits the grass and rolls, and the circle's ring lights around him.",
                "W|Ow! That's extra!",
                "C|slice",
                "S|You're inside. Inside, you exist.",
                "C|wallit",
                "W|Does existing come with a receipt?",
                "C|slice",
                "S|No.",
                "C|wallit",
                "W|Where do I sign for this bat?",
                "C|slice",
                "S|Swing it.",
                "C|wide",
                "Y|Hello! Uploading victory! Striking this address now!",
                "C|wallit",
                "W|Pay up, bots.",
            },
            // The boss is down. The notice and the bolts are left on the grass; two choices (Docs/Story.md, "Dialogue choices").
            After = new[] {
                "B|on",
                "C|wide",
                "N|THE NOTICE\nThe Courier comes apart. Its notice drifts down onto the grass. Nobody has signed it.",
                "Q|1.c1",
                "C|wallit",
                "Q|1.c2",
            },
            Done = new[] {
                "W|Bolts. Somebody's paying me.",
                "S|Not somebody. Something.",
                "?|1.c1:keep|W|The notice is in my pocket. Evidence.",
                "?|1.c1:tear|S|You tore it. It will send another.",
                "?|1.c2:tip|S|You tipped. I noticed.",
            },
        },
        new Chapter {
            Id = "no-fixed-address", Map = "hollow-edge", Title = "Chapter 2: No Fixed Address", Place = "The edge of the Hollow",
            Objective = ChapterObjective.GateAndBack, Target = 70, Bearing = 150f, Goal = "the gate",
            Waves = 3, WaveSize = 4, Boss = BotKind.PlatedYoBot, BossName = "The Plated Alpha", BossHits = 55, PlainBoss = true,
            Companion = "slice",
            Crew = new[] { BotKind.YoBot }, Every = 3.8f, Cap = 5, Power = 4,
            Variant = BotTrait.Splitter, VariantName = "Returned Yo Bot", VariantEvery = 3, VariantLine = "Y|Return to sender! Return to sender!",
            MiniName = "The Second Notice", MiniKind = BotKind.PlatedYoBot, MiniHits = 32, MiniTint = new Color(1.2f, 0.7f, 0.6f),
            MiniLines = new[] { "Y|Second notice! This address is still unknown!", "S|It repeats. It does not give up.", "W|Neither do I. Pay up." },
            Lines = new[] {
                "W|There's a door. Doors have senders.",
                "S|Doors have locks. Come back.",
                "Y|Address unknown! Deleting!",
            },
            // Said as each of the three waves starts, once Wallit is back at the circle.
            WaveLines = new[] {
                "Y|Address unknown! Deleting!",
                "Y|Address still unknown! Deleting twice!",
                "S|Last wave. Something heavier follows.",
            },
            BossLines = new[] {
                "Y|Notice! This plate is stamped: INVALID!",
                "S|The Plated Alpha. Plate says invalid.",
                "W|So does my refund. Pay up.",
            },
            DefeatLines = new[] {
                "Y|Plate... voided... uploading... nothing?",
                "W|Void your own stamp.",
                "S|Clean.",
            },
            // The opening scene, over the frozen Hollow's edge (formats as Chapter 1's The Stamp).
            Cutscene = new[] {
                "B|on",
                "C|wide",
                "N|NO FIXED ADDRESS\nPast the last lantern the Hollow runs out. Far off, a gate stands in the dark with nothing written on it.",
                "C|wallit",
                "W|There's a door. Doors have senders.",
                "C|slice",
                "S|Doors have locks. Come back.",
                "C|wallit",
                "W|Locks have keys. Keys have owners. Owners get paid.",
                "C|slice",
                "S|Seventy metres. Then back.",
                "C|wide",
                "Y|Address unknown! Deleting!",
                "C|wallit",
                "W|Cover me, Slice. I'm collecting a refund.",
                "C|slice",
                "S|Ring's staked. I'm coming.",
            },
            // The Gate: plays when Wallit reaches it. His bat knocks; the wall answers with the same stamp from the other side.
            GateScene = new[] {
                "B|on",
                "C|wallit",
                "N|THE GATE\nNo handle. No hinges. No doormat. Wallit knocks with the bat.",
                "W|Hello? Sender? I'd like a word, and a receipt.",
                "C|wide",
                "C|wallit",
                "W|Nothing. Not even a doormat. I'd say that's rude, but I'm the one with no address.",
                "N|At the foot of the gate, a postmark is torn into the stone. Half of it is his.",
                "W|That's my stamp. Torn.",
                "Q|2.c1",
                "C|slice",
                "S|Wallit. Come back.",
                "?|2.c1:twice|S|You knock like you're owed.",
                "?|2.c1:twice|W|I am.",
            },
            // As the Plated Alpha is called: where Wallit stands decides what Slice says when it changes phase.
            Crossroads = new[] {
                "B|on",
                "C|wide",
                "N|THE RISE\nA plate catches the lantern light on the far rise. INVALID, it says, in red. It is coming down the hill.",
                "Q|2.c2",
            },
            PhaseTwo = new[] {
                "?|2.c2:wait|S|Still here. Plate's cracked.",
                "?|2.c2:ahead|S|Noted. You went out alone.",
            },
            Done = new[] {
                "S|You have an address. The circle.",
                "W|Leased. By you.",
                "S|Free.",
                "W|Nothing's free.",
                "S|Then pay with swings.",
                "?|2.c1:twice|S|Three knocks on record. They will remember.",
            },
        },
        new Chapter {
            Id = "clearing-floor", Map = "clearing-floor", Title = "Chapter 3: The Clearing Floor", Place = "A sunken yard lit by conveyor lamps",
            Objective = ChapterObjective.FloorRun, Target = 105, Goal = "the lever", WaveSize = 6,
            Boss = BotKind.MegaYoBot, BossName = "The Foreman", BossHits = 70, PlainBoss = true,
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot }, Every = 3.6f, Cap = 5, Power = 8,
            Variant = BotTrait.Bruiser, VariantName = "Reissue Yo Bot", VariantEvery = 4, VariantLine = "Y|Reissued! Heavier! Uploading victory!",
            MiniName = "The Half-Built", MiniKind = BotKind.MegaYoBot, MiniHits = 34, MiniTint = new Color(1f, 0.8f, 0.4f),
            MiniLines = new[] { "Y|I am... not finished! Finish me?", "S|Don't.", "W|Wasn't going to. Not for free." },
            Lines = new[] {
                "Y|Hello! I am... not finished! Uploading victory?",
                "W|They're all the same unit.",
                "S|Spare parts. Spare orders.",
            },
            Stops = new[] {
                new Stop {
                    Dist = 36f, Bearing = 200f, Name = "the broken gate",
                    Lines = new[] {
                        "N|Past the broken gate the ground drops away into a yard lit by lamps. Rows of Yo Bot chassis hang on hooks.",
                        "Y|Hello! I am... not finished! Uploading victory?",
                        "W|They're all the same unit.",
                        "S|The Clearing Crew. Stamped out here, one batch for every unfiled thing in the Hollow.",
                    },
                },
                new Stop {
                    Dist = 70f, Bearing = 205f, Name = "the belt",
                    Scene = new[] {
                        "B|on",
                        "C|wide",
                        "N|THE BELT\nA slow belt carries flat, cleared things past the hooks: a hat, a clock, a small bell.",
                        "C|wallit",
                        "W|Cleared things. Somebody's hat. Somebody's clock. That could be me.",
                        "C|slice",
                        "S|It isn't.",
                        "?|1.c2:tip|S|You tipped. Cleared things don't tip.",
                        "C|wide",
                        "N|The belt runs on into a chute lit by a single red lamp. The lamp is a lens. It turns toward the Hollow, and the circle blinks once.",
                        "C|wallit",
                        "W|It's watching the circle.",
                        "C|slice",
                        "S|Pull the lever. The belt stops. The Crew stops.",
                    },
                    Lines = new[] {
                        "W|Those bolts are the same ones they've been paying me.",
                        "S|Then we collect.",
                    },
                },
                new Stop {
                    Dist = 105f, Bearing = 210f, Name = "the lever",
                    Lines = new[] {
                        "W|One lever. No sign. Somebody trusts the floor.",
                        "S|Pull it, Wallit.",
                    },
                },
            },
            // Said as the lever is pulled and the feed breaks.
            WaveLines = new[] {
                "Y|Batch... interrupted? Uploading... victory?",
                "S|No new units. Back to the circle. The ring won't hold itself.",
            },
            BossLines = new[] {
                "Y|Order, read aloud: STRIKE. THIS. ADDRESS.",
                "S|The Foreman. It reads the order to the Crew. Don't let it finish.",
                "W|Then I'll read it the invoice.",
            },
            DefeatLines = new[] {
                "Y|Order... unread... uploading... nothing?",
                "W|Paid in full.",
                "S|Clean.",
            },
            // The opening scene, at the circle: Slice's second ring is staked, and where Yo Bots come from is said out loud.
            Cutscene = new[] {
                "B|on",
                "C|wide",
                "N|THE CLEARING FLOOR\nBeyond the broken gate a yard lies sunken in the dark, lit by lamps along a slow conveyor.",
                "C|wallit",
                "W|Every bot that's come at us. They're coming from down there.",
                "C|slice",
                "S|The Clearing Floor. The chirpy ones are stamped out there, one batch for every unfiled thing in the Hollow.",
                "C|wallit",
                "W|Stamped out. So they're the Department's. Employees.",
                "C|slice",
                "S|Crew. They chirp because every job closes with a report. \"Uploading victory\" means \"filed as cleared.\"",
                "C|wallit",
                "W|And the plates? The big ones?",
                "C|slice",
                "S|The Floor sends the same unit again, heavier, when the cheap ones fail.",
                "C|wallit",
                "W|And the bolts they drop?",
                "C|slice",
                "S|Their own pay. A Yo Bot is held together by its wages.",
                "C|wallit",
                "W|Then I've been collecting what's owed. Good. Now I want the lever.",
                "C|slice",
                "S|I'll hold a second ring here, staked in the floor. Go out, break the belt, and come back.",
            },
            // As the Foreman is called: Wallit has a second sheet from the belt. What it reads decides how phase two begins.
            Crossroads = new[] {
                "B|on",
                "C|wide",
                "N|THE ORDER, READ ALOUD\nThe Foreman steps off its plinth, the order held flat on its chest. It holds the sheet out to be read. In the cleared pile by Wallit's boot lies a hat-tag from the belt.",
                "Q|3.c1",
            },
            PhaseTwo = new[] {
                "?|3.c1:true|S|Overtime. March and page together. Watch the feet.",
                "?|3.c1:wrong|S|It reads what it is handed. It has not stopped to think.",
            },
            // The Foreman is down. The Half-Built is still on its hook.
            After = new[] {
                "B|on",
                "C|wide",
                "N|THE HALF-BUILT\nThe belt has stopped. In the chute, the Half-Built Yo Bot still hangs by one hook, one claw out, wanting finishing.",
                "Q|3.c2",
            },
            Done = new[] {
                "W|They were only ever stamped out. Nobody asked them.",
                "S|Nobody asks. Come inside the ring.",
                "W|Next time I'm billing the Floor.",
                "?|3.c2:unhook|S|You cut it down. Noted.",
            },
        },
        new Chapter {
            Id = "process-servers", Map = "long-walk", Title = "Chapter 4: The Process Servers", Place = "The Long Walk",
            Objective = ChapterObjective.FloorRun, Target = 95, Goal = "the last lamp", WaveSize = 5, Walk = true,
            Boss = BotKind.SpiderBot, BossName = "The Server", BossHits = 60,
            Crew = new[] { BotKind.SpiderBot, BotKind.PlatedYoBot }, Every = 6.5f, Cap = 3, Power = 26,
            Variant = BotTrait.Blinker, VariantName = "Queue-Skip Yo Bot", VariantEvery = 3, VariantLine = "Y|Skipping the queue! Chirp!",
            Lines = new[] {
                "Y|Spiders en route! Chirp!",
                "S|Mind the legs.",
                "W|They're not bigger Yo Bots. They're deliveries.",
            },
            Stops = new[] {
                new Stop {
                    Dist = 30f, Bearing = 330f, Name = "the first lamp",
                    Lines = new[] {
                        "N|The causeway is stone slabs over a pit of mist. A pair of lamps burns at each stop, one for every leg.",
                        "S|Mind the legs.",
                    },
                },
                new Stop {
                    Dist = 62f, Bearing = 330f, Name = "the middle lamp",
                    Lines = new[] {
                        "W|They keep coming down the same path. Nobody's lost. They know the address.",
                        "S|They know mine.",
                    },
                },
                new Stop {
                    Dist = 95f, Bearing = 330f, Name = "the last lamp",
                    Lines = new string[0],
                },
            },
            // Said at the last lamp as the way turns round to the circle.
            WaveLines = new[] {
                "S|It stands and waits. It will keep waiting. Back to the circle, Wallit.",
                "W|Waiting's billable.",
            },
            BossLines = new[] {
                "Y|Order delivery! Signature required!",
                "S|The Server. It will not strike until it has offered the paper three times and been refused three times.",
                "W|Refused. I can do refused. Free of charge.",
            },
            DefeatLines = new[] {
                "Y|Delivery... undelivered... chirp?",
                "W|Return to sender.",
                "S|Clean.",
            },
            // The opening scene, at the circle: where Spider Bots come from, and what a process server is.
            Cutscene = new[] {
                "B|on",
                "C|wide",
                "N|THE LONG WALK\nA causeway of stone slabs runs out over a pit of mist, six lamps on either side. A single path. No cover.",
                "C|wallit",
                "W|Spiders. Bigger than anything so far, and not bigger Yo Bots.",
                "C|slice",
                "S|They aren't. They're what the Yo Bots are for.",
                "C|wallit",
                "W|Meaning?",
                "C|slice",
                "S|Yo Bots clear. Spider Bots serve. Every Spider Bot comes down the Long Walk from the Department with one paper to hand over.",
                "C|wallit",
                "W|They're not bigger Yo Bots. They're... deliveries.",
                "C|slice",
                "S|Process servers. They stand and wait while it's read.",
                "C|wallit",
                "W|Read what?",
                "C|slice",
                "S|My address. Struck.",
                "C|wallit",
                "W|Since when?",
                "C|slice",
                "S|Before you.",
                "C|wallit",
                "W|...",
                "C|wide",
                "N|SERVED\nOut on the slabs a Spider Bot stops a step short of the lamps, folds one leg, and offers a paper on the tip of its claw, politely, the way a courier would. Slice does not take it. It waits.",
                "C|slice",
                "S|Walk the lamps. Hold each. I'll keep the ring.",
            },
            // As the Server is called: it holds out the paper before it holds out anything else.
            Crossroads = new[] {
                "B|on",
                "C|wide",
                "N|SERVED\nThe Server comes down the last slabs and stops a step short of the lamps. It folds one leg and offers a paper on the tip of its claw.",
                "Q|4.c1",
            },
            PhaseTwo = new[] {
                "?|4.c1:take|W|Signed by nobody. Told you.",
                "?|4.c1:refuse|S|Three refusals on the record. It has nothing left but force.",
            },
            // The Server is down. It had a paper for Slice.
            After = new[] {
                "B|on",
                "C|slice",
                "N|The Server's last paper slides along the slab, addressed to the circle. Slice looks at it.",
                "Q|4.c2",
            },
            Done = new[] {
                "?|2.c1:twice|V|Three knocks on record.",
                "W|It waited for me to take it. Three times. Refused all three.",
                "S|That is how it's done. Refuse the paper, and it has to strike.",
                "W|And then?",
                "S|And then you are standing in front of it.",
            },
        },
        new Chapter {
            Id = "strike-this-address", Map = "reading-room", Title = "Chapter 5: Strike This Address", Place = "The Reading Room",
            Objective = ChapterObjective.HoldThenBoss, Target = 20, Goal = "the pages", Waves = 5, WaveSize = 4,
            Boss = BotKind.TitanSpiderBot, BossName = "The Registrar", BossHits = 80, PlainBoss = true,
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot, BotKind.MegaYoBot }, Every = 3.2f, Cap = 6, Power = 24,
            Variant = BotTrait.Juker, VariantName = "Clerk Yo Bot", VariantEvery = 3, VariantLine = "Y|Page turned! Turning again!",
            MiniName = "The Clerk", MiniKind = BotKind.SpiderBot, MiniHits = 46, MiniTint = new Color(0.85f, 1.05f, 1.3f),
            MiniLines = new[] { "Y|Page ten! Filing in progress!", "S|It files what it cannot read.", "W|Then I'll make it a refund." },
            Lines = new[] {
                "Y|Order, read aloud: STRIKE. THIS. ADDRESS.",
                "S|It's reading it out.",
                "W|Then I'm reading mine. Pay up.",
            },
            // Said as each page begins: the order, one page at a time, and what Wallit makes of it.
            WaveLines = new[] {
                "Y|Page one! STRIKE.",
                "Y|Page two! THIS.",
                "Y|Page three! ADDRESS. Chirp!",
                "W|Pages four and five are the small print. Nobody reads the small print. I do.",
                "Y|Page five! BY ORDER OF NOBODY. Filed!",
            },
            BossLines = new[] {
                "Y|Registrar on the floor! Please remain seated!",
                "S|The Registrar. It reads the pages with every leg. Break it before it signs.",
                "W|Nobody signs it. That's the whole trick.",
            },
            DefeatLines = new[] {
                "Y|Register... closed... uploading... nothing?",
                "W|Struck from the record.",
                "S|Clean.",
            },
            // The opening scene, in the Reading Room: the order is not signed, so it can be denied.
            Cutscene = new[] {
                "B|on",
                "C|wide",
                "N|THE READING ROOM\nA bare round hall. The circle is drawn on its floor at the size of the Hollow's. The walls are lined with forms, all identical, all this order. In the middle, a desk. Empty.",
                "C|wallit",
                "W|They keep the order here. Every copy of it.",
                "C|slice",
                "S|Every Spider Bot walks out of this room with one.",
                "C|wide",
                "Y|Order, read aloud: STRIKE. THIS. ADDRESS.",
                "C|slice",
                "S|It's reading it out. Each reading calls the Crew.",
                "C|wallit",
                "W|Then I'm reading mine. Pay up.",
                "W|It's not signed.",
                "C|slice",
                "S|Nobody signs it. That's how it works.",
                "C|wallit",
                "W|Then it can be denied.",
                "C|slice",
                "S|Denied by whom?",
                "C|wallit",
                "W|By the guy with the stamp.",
                "N|Wallit holds up the RETURN TO SENDER stamp he picked up at the gate.",
                "C|slice",
                "S|Wallit. That's a lot of nerve.",
                "C|wallit",
                "W|Nerve's free. The swing's extra.",
                "C|wide",
                "N|THE STAMP, REVERSED\nHe presses the Department's own stamp to the first page. The ink shows nothing, then DENIED.",
                "C|slice",
                "S|Five pages. The room reads them all, and the Crew comes with each. Hold the circle at the desk.",
            },
            // As the Registrar is called: the order is read aloud once more, and Wallit can read with it.
            Crossroads = new[] {
                "B|on",
                "C|wide",
                "N|THE REGISTRAR\nA gold Spider Bot with a ledger on its back takes the desk. It opens the first page and begins to read.",
                "?|1.c1:keep|W|It's got my notice in there somewhere. I keep receipts.",
                "?|1.c1:keep|S|You kept it.",
                "?|1.c1:tear|S|You tore the first one. We take another.",
                "?|4.c1:take|W|I've seen it. I don't need to be told.",
                "Q|5.c2",
            },
            PhaseTwo = new[] {
                "?|5.c2:aloud|S|It knows you. Reader recognized.",
                "?|5.c2:silent|S|Page and stamp together. Keep moving.",
            },
            Done = new[] {
                "W|Every page turned over. DENIED.",
                "S|The room is blank. Nothing left to read.",
                "W|So the order is only a form.",
                "S|A form can be answered.",
                "W|Then we answer it. Pay up.",
            },
        },
        new Chapter {
            Id = "address-filed", Map = "hollow-filed", Title = "Chapter 6: Address Filed", Place = "The Moonlit Hollow, changed",
            Objective = ChapterObjective.HoldThenBoss, Target = 12, Waves = 3, WaveSize = 4, Final = true, Goal = "the Crew",
            Boss = BotKind.SpiderBot, BossName = "The Order", BossHits = 90, PlainBoss = true,
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot, BotKind.MegaYoBot }, Every = 3f, Cap = 6, Power = 30,
            Variant = BotTrait.Juggernaut, VariantName = "Sealed Yo Bot", VariantEvery = 4, VariantLine = "Y|Sealed and stamped! Delivering!",
            Lines = new[] {
                "S|The wall is gone. The circle is brighter.",
                "W|Then let's file it. Pay up.",
            },
            WaveLines = new[] {
                "Y|Order delivery! Signature required!",
                "S|Hold the circle. Nobody signs it.",
                "W|Last of the Crew. Then the big one. Exact change.",
            },
            BossLines = new[] {
                "Y|Order delivery! Signature required!",
                "S|Spider. It carries the order.",
                "W|Then it carries my bill.",
            },
            DefeatLines = new[] {
                "Y|Signature... required... uploading... nothing?",
                "S|Clean.",
            },
            // The opening scene: the Hollow as in Chapter 1, changed.
            Cutscene = new[] {
                "B|on",
                "C|wide",
                "N|THE MOONLIT HOLLOW\nThe wall is gone. A path of pale slabs leads out of the Hollow and stops at nothing. The circle is brighter than it has ever been.",
                "C|slice",
                "S|The wall is gone. The circle is brighter.",
                "C|wallit",
                "W|Then they're out of walls. Let's file it. Pay up.",
                "C|wide",
                "Y|Order delivery! Signature required!",
                "C|slice",
                "S|The order. It comes down the path, last. Hold the circle until it does.",
            },
            // Played when the Spider Bot that carries the order falls, then the Finale pages and the ending card.
            Ending = new[] {
                "B|on",
                "C|wide",
                "N|FILED\nThe ring around the circle becomes a line of writing, then a door number. Slice writes it with her scalpel, one stroke at a time.",
                "?|6.c2:one|N|One number. One door, for the circle.",
                "?|6.c2:two|N|Two numbers, side by side. Two doors, one beside the other.",
                "?|6.c1:share|S|I pressed the stamp myself.",
                "?|6.c1:keep|W|My stamp. My door.",
                // What the hub's errands and talks did (HubLife; read only when the case file kept them).
                "?|side:lid|N|Spare is in the doorway of the ring, lid on straight, cheering at nothing in particular.",
                "?|side:ribbon|N|Brim hangs on the wall by the door number, band on, left of the desk. It has an address.",
                "?|side:pin|N|Tick keeps the hour. Every pin where it should be.",
                "?|side:whistle|N|Spare chirps once, properly, and the Hollow answers.",
                "?|side:label|N|A gummed label on Brim's hook reads BRIM, in Brim's own slow hand.",
                "?|side:key|N|Tick is wound. Nobody had to be the Department to do it.",
                "?|sides:12|N|Twelve slips on the office wall, every one pinned. The Department files itself.",
                "?|chat:chime3|N|Chime rings once for the right address. It is the quietest bell in the Hollow.",
                "C|slice",
                "S|Numbered.",
                "C|wallit",
                "W|A door number. Does it come with a door?",
                "C|wide",
                "N|The camera pulls back. The Hollow is quiet.",
            },
            // The order is down. The ring needs one more stamp and a door number; both are Wallit's to ask (read by the Ending).
            After = new[] {
                "B|on",
                "C|wide",
                "N|THE LAST STAMP\nThe order drops, unsigned. The ring on the grass waits for one more stamp.",
                "?|1.c2:tip|S|You tipped me the first bolt. I filed it.",
                "Q|6.c1",
                "C|slice",
                "N|Slice takes her scalpel and begins on the door number, one stroke at a time.",
                "Q|6.c2",
            },
            Done = new[] {
                "W|\"Strike this address.\" Stamped DENIED. Ha.",
                "S|Filed. Properly, this time.",
                "W|So we exist.",
                "S|Clean. Good swing, Wallit.",
                "W|Pay up.",
            },
        },
    };

    // The credits card after ADDRESS FILED, at the end of the Campaign.
    public const string CreditsTitle = "THE PLACE DEPARTMENT";
    public const string Credits = "WALLIT: one wallet, one bat. Pay up.\nSLICE: guards the circle. Clean.\nYO BOTS: uploading victory.\nSPIDER BOTS: by order of nobody.\n\nThe circle stands. Slice and Wallit are on the books.\nThe case file is filed. Thanks for playing.\n\nPowered by HAWKES GEN";

    // The secret ending: after ADDRESS FILED, only when all six lost things are in the case file (CampaignSave.SecretEarned).
    public const string SecretTitle = "RECEIVED";
    public static readonly string[] SecretEnding = {
        "B|on",
        "C|wide",
        "N|SIX LOST THINGS\nA postcard. A key. A hat. A paper. A page. A stamp. Wallit sets them down in the ring, one at a time.",
        "C|wallit",
        "W|None of this is mine. I'm keeping it anyway.",
        "C|slice",
        "S|No address.",
        "C|wallit",
        "W|Then give it one. You've got the stamp.",
        "C|slice",
        "S|It prints true. Backwards, filed.",
        "C|wide",
        "N|The stamp comes down. For the first time it says ADDRESS, and nothing strikes it. Every lost thing in the Hollow gets a door.",
        "Y|Delivery! Delivery! ... Oh. It's for us?",
        "C|wallit",
        "W|Return to sender?",
        "C|slice",
        "S|Not this time. Received.",
        "C|wallit",
        "W|Received. In full. Pay up.",
    };
    public const string SecretCredits = "\n\nRECEIVED\nBRIM: has a door. Somebody will come.\nTICK: has the time.\nCHIME: rings for the right address.";

    public static readonly string[] Finale = {
        "W|\"Strike this address.\" Stamped DENIED. Ha.",
        "S|Filed. Properly, this time.",
        "W|So we exist.",
        "S|Clean. Good swing, Wallit.",
        "W|Pay up.",
    };

    public const string EndingTitle = "ADDRESS FILED";
    public const string EndingLine = "The circle stands. Slice and Wallit are on the books.";

    // Short beats at a wave's start, one speaker per line. Boss waves open with a title card.
    static readonly string[][] Beats = {
        new[] { "Y|Hello! Uploading victory!", "W|Pay up, bots." },
        new[] { "S|Again. Behind me, Wallit." },
        new[] { "W|Bolts are wages. Collect." },
        new[] { "Y|Address unknown! Deleting!" },
        new[] { "W|Walls cost. Worth it." },
        new[] { "Y|Victory loading... 94%!" },
        new[] { "S|Good swing.", "W|That's a tip. I keep tips." },
        new[] { "W|They want her circle. Not on my watch." },
        new[] { "S|Big one next. Be ready." },
        new[] { "T|WAVE 10: PLATED", "Y|Upgraded! Now with plates!", "S|Tch. Plated.", "W|Plates cost more. Pay up." },
        new[] { "S|Tougher. I'm fine." },
        new[] { "W|You're bleeding, Slice.", "S|A scratch." },
        new[] { "Y|Order received! Strike the address!" },
        new[] { "W|I'm covering you. Tab's open." },
        new[] { "S|Wallit. Stay close." },
        new[] { "Y|Uploading victory! Almost!" },
        new[] { "W|Nobody touches her. Nobody." },
        new[] { "S|You swing like you mean it." },
        new[] { "W|Something big's coming. I can smell the invoice." },
        new[] { "T|WAVE 20: MEGA", "Y|Order, read aloud: STRIKE. THIS. ADDRESS.", "S|It's reading it out.", "W|Then I'm reading mine. Pay up." },
        new[] { "S|Louder now." },
        new[] { "Y|Strike the address. Strike the address." },
        new[] { "W|Holding, Slice?", "S|Holding." },
        new[] { "S|Don't look at the order. Look at them." },
        new[] { "W|Twenty-five waves. I'm owed a bonus." },
        new[] { "Y|Spiders en route! Chirp!" },
        new[] { "S|Mind the legs." },
        new[] { "W|Last stretch. Pay up." },
        new[] { "S|One more. Then the big one." },
        new[] { "T|WAVE 30: THE ORDER", "Y|Order delivery! Signature required!", "S|Spider. It carries the order.", "W|Then it carries my bill." },
    };

    // Waves past the last beat (overtime) get the quiet kind.
    static readonly string[] Overtime = {
        "S|Overtime.", "W|Overtime pays double.", "S|Still standing.",
    };

    public static string[] BeatFor(int wave) {
        if (wave >= 1 && wave <= Beats.Length) {
            return Beats[wave - 1];
        }
        return new[] { Overtime[wave % Overtime.Length] };
    }

    // True when the wave's beat already has a Wallit line (WallitVoice then skips its own wave bark).
    public static bool HasWallitBeat(int wave) {
        foreach (string line in BeatFor(wave)) {
            if (Speaker(line) == "W") {
                return true;
            }
        }
        return false;
    }

    public static string Speaker(string line) {
        return line.Substring(0, line.IndexOf('|'));
    }

    public static string Text(string line) {
        return line.Substring(line.IndexOf('|') + 1);
    }

    // The speaker's name for the dialogue box: the cast file's (Resources/Cast/cast.json).
    public static string Name(string speaker) {
        return Cast.NameOf(speaker);
    }

    public static IEnumerable<string> All() {
        foreach (string line in Finale) yield return line;
        foreach (string[] beat in Beats) foreach (string line in beat) yield return line;
        foreach (string line in Overtime) yield return line;
    }
}
