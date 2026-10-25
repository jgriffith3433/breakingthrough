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
