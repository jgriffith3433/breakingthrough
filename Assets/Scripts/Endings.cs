using UnityEngine;

// The ADDRESS FILED ending in three variants, and what New Game+ remembers of the playthrough before it (Docs/Story.md,
// "Endings and New Game+"). Which one plays is read from the open case file when Chapter 6 is filed:
//   Two Doors  : Wallit took the crew's side. Company points (tipped Slice in 1, read the page aloud in 5, handed her the stamp
//                and asked for two numbers in 6) are 3 or more.
//   Open Door  : otherwise, the hub's errands were done: 6 or more of the side slips and favours (HubLife.SideCount).
//   Ledger     : everything else. Wallit's own stamp, his own door.
// Nothing here is saved on its own except Slot.ending (1-3, the variant last seen; 0 for a file that never reached it), which New
// Game+ reads for its echo lines after the loop's choices are cleared. Arcade has no case file, so it reads as none.
public static class Endings {
    public const int None = 0, Ledger = 1, Doors = 2, Open = 3;
    public const int Count = 3;
    public const int OpenSides = 6;       // errands for the Open Door
    public const int DoorsPoints = 3;     // company points for Two Doors

    public static readonly string[] Names = { "", "THE LEDGER", "TWO DOORS", "THE OPEN DOOR" };
    public static readonly string[] Reels = { "", "ending_ledger", "ending_doors", "ending_open" };

    // The card's line under ADDRESS FILED, one per variant (Story.EndingLine for a file with no variant).
    public static readonly string[] CardLines = {
        "",
        "One door, one name. Wallit signs the ledger. Slice guards it.",
        "Two doors, side by side. The circle has a neighbour now.",
        "The door stays open. The whole Hollow is on the books.",
    };

    // Company points: the choices that put Slice or the crew first.
    public static int Company(CampaignSave.Slot s) {
        if (s == null) {
            return 0;
        }
        return Is(s, "1.c2", "tip") + Is(s, "5.c2", "aloud") + Is(s, "6.c1", "share") + Is(s, "6.c2", "two");
    }

    static int Is(CampaignSave.Slot s, string id, string key) {
        int row = Choices.Index(id);
        if (row < 0) {
            return 0;
        }
        int v = Choices.Read(s.choices, row);
        return v > 0 && Choices.All[row].Keys[v - 1] == key ? 1 : 0;
    }

    public static int VariantOf(CampaignSave.Slot s) {
        if (s == null || !s.used) {
            return None;
        }
        if (Company(s) >= DoorsPoints) {
            return Doors;
        }
        return HubLife.SideCount(s) >= OpenSides ? Open : Ledger;
    }

    // The variant the open file would get now (None when no file is open).
    public static int Current {
        get { return CampaignSave.Active < 0 ? None : VariantOf(CampaignSave.Get(CampaignSave.Active)); }
    }

    public static string Name(int variant) {
        return variant > 0 && variant < Names.Length ? Names[variant] : "";
    }

    public static string CardLine(int variant) {
        return variant > 0 && variant < CardLines.Length ? CardLines[variant] : Story.EndingLine;
    }

    // ---- New Game+: one line a chapter that remembers the loop before (Slot.ending). Played as the chapter opens, by ChapterRun.

    // [chapter][variant - 1]: "S|text" lines, in the speaker codes of every scene.
    static readonly string[][] Echo = {
        new[] { "W|Last time this notice went in my ledger. This time I read the whole thing first.",
                "S|Last time you signed for the notice. Two names, one pen. Do it again.",
                "S|The Hollow knows our door. Somebody will knock." },
        new[] { "W|One door in my name. I still hear it close.",
                "S|Two doors. Wallit's is next to mine. Knock on it.",
                "Y|Lids on! The door opens for us this time!" },
        new[] { "S|You filed the ledger alone. The Foreman reads it back differently now.",
                "W|Two doors. Heavier bill, bigger crew. Keep up.",
                "S|Every slip on the wall was pinned. The Floor sends fewer now." },
        new[] { "W|Somebody at the Server's desk kept my name. Mine only.",
                "S|The Server kept two names last time. It will ask for two.",
                "W|The Server's paper is already pinned in the hub. Refused, in full." },
        new[] { "S|The Registrar reads one name aloud. Last time it was yours alone.",
                "W|Pages read, doors filed. Last time I read along. Louder this time.",
                "Y|The ledger knows us! Uploading... a door!" },
        new[] { "W|One door, in my hand. One stamp. I'll try it again.",
                "S|Two numbers, side by side. Same pen. Same line.",
                "S|The door never shut. It will stay open." },
    };

    // The line for a chapter of a file on its New Game+ loop, or null (not on a loop, or the file never saw an ending).
    public static string EchoFor(int chapter, int variant) {
        if (chapter < 0 || chapter >= Echo.Length || variant < 1 || variant > Count) {
            return null;
        }
        return Echo[chapter][variant - 1];
    }
}
