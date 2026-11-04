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
        public BotKind[] Crew;        // the bots that come at the circle during the chapter
        public float Every;           // seconds between bots
        public int Cap;               // most bots alive at once
        public int Power;             // the wave number the bots' strength follows
        public string[] Lines;        // before: shown on the chapter card ("<speaker>|<text>")
        public string[] Done;         // after: shown on the Chapter Complete card
    }

    public static readonly Chapter[] Chapters = {
        new Chapter {
            Id = "return-to-sender", Title = "Chapter 1: Return to Sender", Place = "The Moonlit Hollow",
            Objective = ChapterObjective.ClearCount, Target = 8, Goal = "Yo Bots",
            Crew = new[] { BotKind.YoBot }, Every = 3.4f, Cap = 4, Power = 1,
            Lines = new[] {
                "W|Return to sender. Which sender? I want a refund.",
                "S|No sender. No refund. Stay in the circle.",
                "W|Where do I sign for this bat?",
                "S|Swing it.",
            },
            Done = new[] {
                "W|Bolts. Somebody's paying me.",
                "S|Not somebody. Something.",
            },
        },
        new Chapter {
            Id = "no-fixed-address", Title = "Chapter 2: No Fixed Address", Place = "The edge of the Hollow",
            Objective = ChapterObjective.ReachPlace, Target = 70, Bearing = 150f, Goal = "the gate",
            Crew = new[] { BotKind.YoBot }, Every = 3.8f, Cap = 5, Power = 4,
            Lines = new[] {
                "W|There's a door. Doors have senders.",
                "S|Doors have locks. Come back.",
                "Y|Address unknown! Deleting!",
            },
            Done = new[] {
                "W|Nothing. Not even a doormat. I'd say that's rude, but I'm the one with no address.",
                "S|You have one. The circle.",
                "W|Leased. By you.",
                "S|Free.",
                "W|Nothing's free.",
                "S|Then pay with swings.",
            },
        },
        new Chapter {
            Id = "clearing-floor", Title = "Chapter 3: The Clearing Floor", Place = "A sunken yard lit by conveyor lamps",
            Objective = ChapterObjective.DefeatBoss, Boss = BotKind.MegaYoBot, Goal = "the Foreman",
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot }, Every = 5f, Cap = 5, Power = 10,
            Lines = new[] {
                "Y|Hello! I am... not finished! Uploading victory?",
                "W|They're all the same unit.",
                "S|Spare parts. Spare orders.",
                "W|Those bolts are the same ones they've been paying me.",
                "S|Then we collect.",
            },
            Done = new[] {
                "W|Cleared things. Somebody's hat. Somebody's clock. That could be me.",
                "S|It isn't.",
            },
        },
        new Chapter {
            Id = "process-servers", Title = "Chapter 4: The Process Servers", Place = "The Long Walk",
            Objective = ChapterObjective.SurviveTime, Target = 60, Goal = "the Servers",
            Crew = new[] { BotKind.PlatedYoBot, BotKind.SpiderBot }, Every = 7f, Cap = 4, Power = 20,
            Lines = new[] {
                "Y|Spiders en route! Chirp!",
                "S|Mind the legs.",
                "W|They're not bigger Yo Bots. They're deliveries.",
                "S|Process servers. They stand and wait while it's read.",
            },
            Done = new[] {
                "W|Read what?",
                "S|My address. Struck.",
                "W|Since when?",
                "S|Before you.",
            },
        },
        new Chapter {
            Id = "strike-this-address", Title = "Chapter 5: Strike This Address", Place = "The Reading Room",
            Objective = ChapterObjective.ClearCount, Target = 20, Goal = "the pages",
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot, BotKind.MegaYoBot }, Every = 3.2f, Cap = 6, Power = 24,
            Lines = new[] {
                "Y|Order, read aloud: STRIKE. THIS. ADDRESS.",
                "S|It's reading it out.",
                "W|Then I'm reading mine. Pay up.",
            },
            Done = new[] {
                "W|It's not signed.",
                "S|Nobody signs it. That's how it works.",
                "W|Then it can be denied.",
                "S|Wallit. That's a lot of nerve.",
                "W|Nerve's free. The swing's extra.",
            },
        },
        new Chapter {
            Id = "address-filed", Title = "Chapter 6: Address Filed", Place = "The Moonlit Hollow, changed",
            Objective = ChapterObjective.DefeatBoss, Boss = BotKind.SpiderBot, Goal = "the Spider Bot with the order",
            Crew = new[] { BotKind.YoBot, BotKind.PlatedYoBot }, Every = 6f, Cap = 4, Power = 30,
            Lines = new[] {
                "S|The wall is gone. The circle is brighter.",
                "W|Then let's file it. Pay up.",
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
