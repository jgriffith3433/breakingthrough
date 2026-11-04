using System.Collections.Generic;

// The story as data. Docs/Story.md explains it; change a line in both. A line is "<speaker>|<text>" with speaker
// N narrator, W Wallit (talks money), S Slice (clipped), Y Yo Bot (chirpy), T title card.
public static class Story {
    // Wave 30 is the first Spider Bot and carries the order. Clearing it plays the finale.
    public const int FinalWave = 30;

    public static readonly string[] Intro = {
        "N|THE PLACE DEPARTMENT\nWhere things that should not exist are given an address.",
        "W|Wallit. One wallet, one bat, no refunds. They stamped me \"Return to sender\" and threw me in here.",
        "S|Slice. This is the circle: the one address in the Hollow that's filed. Inside it, things exist.",
        "S|Outside, they're cleared. If I fall, the circle goes, and everything in it. Don't let me fall.",
        "Y|Hello! Uploading victory! Striking this address now!",
        "W|Guard Slice. Swing at anything chirpy. Pay up.",
    };

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
        public string[] Cutscene;     // played by Cutscene.cs over the chapter's map when Begin is pressed (null: none)
    }

    public static readonly Chapter[] Chapters = {
        new Chapter {
            Id = "return-to-sender", Title = "Chapter 1: Return to Sender", Place = "The Moonlit Hollow",
            Objective = ChapterObjective.HoldThenBoss, Target = 6, Goal = "Yo Bots", Boss = BotKind.YoBot, BossName = "The Courier", BossHits = 40,
            Crew = new[] { BotKind.YoBot }, Every = 3.4f, Cap = 4, Power = 1,
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
                "Y|Notice... undelivered... uploading... defeat?",
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
            Done = new[] {
                "W|Bolts. Somebody's paying me.",
                "S|Not somebody. Something.",
            },
        },
        new Chapter {
            Id = "no-fixed-address", Title = "Chapter 2: No Fixed Address", Place = "The edge of the Hollow",
            Objective = ChapterObjective.GateAndBack, Target = 70, Bearing = 150f, Goal = "the gate",
            Waves = 3, WaveSize = 4, Boss = BotKind.PlatedYoBot, BossName = "The Plated Alpha", BossHits = 55, PlainBoss = true,
            Crew = new[] { BotKind.YoBot }, Every = 3.8f, Cap = 5, Power = 4,
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
            },
            // The Gate: plays when Wallit reaches it. His bat knocks; the wall answers with the same stamp from the other side.
            GateScene = new[] {
                "B|on",
                "C|wallit",
                "N|THE GATE\nNo handle. No hinges. No doormat. Wallit knocks with the bat.",
                "W|Hello? Sender? I'd like a word, and a receipt.",
                "C|wide",
                "N|The wall answers. From the other side, pressed through the dark, the same stamp: RETURN TO SENDER.",
                "C|wallit",
                "W|Nothing. Not even a doormat. I'd say that's rude, but I'm the one with no address.",
                "N|At the foot of the gate, a postmark is torn into the stone. Half of it is his.",
                "W|That's my stamp. Torn.",
                "C|slice",
                "S|Wallit. Come back.",
            },
            Done = new[] {
                "S|You have an address. The circle.",
                "W|Leased. By you.",
                "S|Free.",
                "W|Nothing's free.",
                "S|Then pay with swings.",
            },
        },
        new Chapter {
            Id = "clearing-floor", Title = "Chapter 3: The Clearing Floor", Place = "A sunken yard lit by conveyor lamps",
            Objective = ChapterObjective.FloorRun, Target = 105, Goal = "the lever", WaveSize = 6,
            Boss = BotKind.MegaYoBot, BossName = "The Foreman", BossHits = 70, PlainBoss = true,
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot }, Every = 3.6f, Cap = 5, Power = 8,
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
                "N|The lever drops. Down the line the hooks swing empty, and the belt coughs and stops.",
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
            Done = new[] {
                "W|They were only ever stamped out. Nobody asked them.",
                "S|Nobody asks. Come inside the ring.",
                "W|Next time I'm billing the Floor.",
            },
        },
        new Chapter {
            Id = "process-servers", Title = "Chapter 4: The Process Servers", Place = "The Long Walk",
            Objective = ChapterObjective.FloorRun, Target = 95, Goal = "the last lamp", WaveSize = 5, Walk = true,
            Boss = BotKind.SpiderBot, BossName = "The Server", BossHits = 60,
            Crew = new[] { BotKind.SpiderBot, BotKind.PlatedYoBot }, Every = 6.5f, Cap = 3, Power = 26,
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
                    Lines = new[] {
                        "N|The last pair of lamps. Ahead the slabs end in the mist, and something tall is walking up them, slowly, to wait.",
                    },
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
            Done = new[] {
                "W|It waited for me to take it. Three times. Refused all three.",
                "S|That is how it's done. Refuse the paper, and it has to strike.",
                "W|And then?",
                "S|And then you are standing in front of it.",
            },
        },
        new Chapter {
            Id = "strike-this-address", Title = "Chapter 5: Strike This Address", Place = "The Reading Room",
            Objective = ChapterObjective.HoldThenBoss, Target = 20, Goal = "the pages", Waves = 5, WaveSize = 4,
            Boss = BotKind.TitanSpiderBot, BossName = "The Registrar", BossHits = 80, PlainBoss = true,
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot, BotKind.MegaYoBot }, Every = 3.2f, Cap = 6, Power = 24,
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
                "N|THE STAMP, REVERSED\nHe presses the Department's own stamp to the first page. The ink shows nothing, then DENIED. Every form on the walls turns over at once, blank.",
                "C|slice",
                "S|Five pages. The room reads them all, and the Crew comes with each. Hold the circle at the desk.",
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
            Id = "address-filed", Title = "Chapter 6: Address Filed", Place = "The Moonlit Hollow, changed",
            Objective = ChapterObjective.HoldThenBoss, Target = 12, Waves = 3, WaveSize = 4, Final = true, Goal = "the Crew",
            Boss = BotKind.SpiderBot, BossName = "The Order", BossHits = 90, PlainBoss = true,
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot, BotKind.MegaYoBot }, Every = 3f, Cap = 6, Power = 30,
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
                "C|slice",
                "S|Numbered.",
                "C|wallit",
                "W|A door number. Does it come with a door?",
                "C|wide",
                "N|The camera pulls back. The Hollow is quiet.",
            },
            Done = new[] {
                "N|The Spider Bot comes apart. The order drops.",
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

    public static readonly string[] Finale = {
        "N|The Spider Bot comes apart. The order drops.",
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

    public static string Name(string speaker) {
        switch (speaker) {
            case "W": return "WALLIT";
            case "S": return "SLICE";
            case "Y": return "YO BOT";
            default: return "";
        }
    }

    public static IEnumerable<string> All() {
        foreach (string line in Intro) yield return line;
        foreach (string line in Finale) yield return line;
        foreach (string[] beat in Beats) foreach (string line in beat) yield return line;
        foreach (string line in Overtime) yield return line;
    }
}
