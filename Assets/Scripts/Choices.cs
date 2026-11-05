using System.Collections.Generic;
using UnityEngine;

// The campaign's dialogue choices (Docs/CampaignBible.md, "Choice flags"; Docs/Story.md, "Dialogue choices").
// A scene line "Q|<id>" stops Cutscene on a choice page (two buttons, keys 1 and 2). The pick is saved in the open case file
// (CampaignSave.Slot.choices, two bits a row) and its "Now" lines play at once. Later scenes read it with a conditional line:
//     "?|<id>:<key>|<speaker>|<text>"   is the line "<speaker>|<text>" when that option was taken, and nothing otherwise.
// Expand turns one line into the plain line (or null); Resolve does it for a whole array (the Done card, boss lines).
// Outside a chapter (Arcade, no open file) nothing is picked, so every ? line is silent and every Q line is skipped.
public static class Choices {
    public class Row {
        public string Id;          // "<chapter>.c<n>", as in the bible
        public int Chapter;        // 0-based
        public string Prompt;      // narration shown on the choice page
        public string[] Keys;      // the two options' keys
        public string[] Labels;    // the two buttons
        public string[][] Now;     // lines played right after the pick, per option
        public string Flag;        // the bible's name for the flag this choice sets (documentation: readers use Id and key)
    }

    public static readonly Row[] All = {
        new Row { Id = "1.c1", Chapter = 0, Flag = "notice_kept",
            Prompt = "The Courier's notice lies on the grass, undelivered. What does Wallit do with it?",
            Keys = new[] { "keep", "tear" }, Labels = new[] { "Pocket it. \"Evidence.\"", "Tear it. \"Void.\"" },
            Now = new[] { new[] { "W|Evidence. Every bill has a paper trail." }, new[] { "W|Void. I don't take bills I didn't order." } } },
        new Row { Id = "1.c2", Chapter = 0, Flag = "tipped_slice",
            Prompt = "The first bolts are in hand. Slice says nothing. Wallit decides.",
            Keys = new[] { "tip", "keep" }, Labels = new[] { "Hold one out to Slice. \"Your cut.\"", "Keep every bolt. \"Overhead.\"" },
            Now = new[] { new[] { "S|Not mine." }, new[] { "W|Overhead. Don't look at me like that." } } },
        new Row { Id = "2.c1", Chapter = 1, Flag = "knocked_twice",
            Prompt = "The wall has answered. How does Wallit answer back?",
            Keys = new[] { "once", "twice" }, Labels = new[] { "Knock once. Wait.", "Knock twice, then hard, with the bat." },
            Now = new[] { new[] { "N|Nothing answers a polite knock." }, new[] { "W|Open up. I know you're in there. It's a stamp, not a person." } } },
        new Row { Id = "2.c2", Chapter = 1, Flag = "left_ring",
            Prompt = "The Plated Alpha tops the rise. Slice has the ring. Where does Wallit stand?",
            Keys = new[] { "wait", "ahead" }, Labels = new[] { "Hold at the ring. Let it come to her.", "Go out to meet it. \"Keep up.\"" },
            Now = new[] { new[] { "S|Good." }, new[] { "S|I am." } } },
        new Row { Id = "3.c1", Chapter = 2, Flag = "foreman_misread",
            Prompt = "The Foreman holds out the order sheet to be read aloud. Wallit has a second sheet from the belt.",
            Keys = new[] { "true", "wrong" }, Labels = new[] { "Let it read the true order.", "Swap in the hat-tag. \"Read this one.\"" },
            Now = new[] { new[] { "F|STRIKE. THIS. ADDRESS." }, new[] { "F|Order, read aloud: ONE. HAT. BLUE. Uploading... that is not the order?" } } },
        new Row { Id = "3.c2", Chapter = 2, Flag = "half_built_freed",
            Prompt = "The Half-Built still hangs by one hook and wants finishing.",
            Keys = new[] { "leave", "unhook" }, Labels = new[] { "Leave it on the hook.", "Cut its hook. Let it drop." },
            Now = new[] { new[] { "W|Not for free." }, new[] { "H|Hello! Thank you? Uploading... I do not know what." } } },
        new Row { Id = "4.c1", Chapter = 3, Flag = "paper_taken",
            Prompt = "The Server holds out its paper. Taking it ends the offer.",
            Keys = new[] { "refuse", "take" }, Labels = new[] { "Refuse it. Strike the claw.", "Take the paper and read it." },
            Now = new[] { new[] { "W|Refused." }, new[] { "N|The paper reads: STRIKE THIS ADDRESS. No signature. The Server's leg unfolds." } } },
        new Row { Id = "4.c2", Chapter = 3, Flag = "slice_answered",
            Prompt = "The Server's paper was for Slice. Who answers?",
            Keys = new[] { "wallit", "slice" }, Labels = new[] { "Wallit steps between. \"Talk to me.\"", "Slice answers herself. \"Refused.\"" },
            Now = new[] { new[] { "V|Delivery is to the named address." }, new[] { "S|Refused. Mine to refuse." } } },
        new Row { Id = "5.c2", Chapter = 4, Flag = "page_read_aloud",
            Prompt = "The Registrar reads the order aloud. Wallit can read along.",
            Keys = new[] { "aloud", "silent" }, Labels = new[] { "Read the first page aloud with it.", "Read it silently." },
            Now = new[] { new[] { "W|STRIKE. THIS. ADDRESS. Sounds worse when it's yours." }, new[] { "S|Say it.", "W|No." } } },
        new Row { Id = "6.c1", Chapter = 5, Flag = "stamp_shared",
            Prompt = "The stamp is in Wallit's hand. The ring needs one more stamp.",
            Keys = new[] { "keep", "share" }, Labels = new[] { "Stamp it himself.", "Hand the stamp to Slice." },
            Now = new[] { new[] { "W|My stamp, my door." }, new[] { "S|Yours.", "W|Take it." } } },
        new Row { Id = "6.c2", Chapter = 5, Flag = "door_for_both",
            Prompt = "Slice writes a door number with her scalpel. Wallit has a request.",
            Keys = new[] { "one", "two" }, Labels = new[] { "One number, for the circle.", "Two numbers, side by side." },
            Now = new[] { new[] { "S|One." }, new[] { "W|Two. Put me next to you.", "S|...Clean." } } },
    };

    public static int Index(string id) {
        for (int i = 0; i < All.Length; i++) {
            if (All[i].Id == id) {
                return i;
            }
        }
        return -1;
    }

    public static Row Get(string id) {
        int i = Index(id);
        return i >= 0 ? All[i] : null;
    }

    // ---- the packed word (2 bits a row; 0 not made, 1 first option, 2 second)

    public static int With(int word, int row, int option) {
        int shift = row * 2;
        return (word & ~(3 << shift)) | ((Mathf.Clamp(option, 0, 1) + 1) << shift);
    }

    public static int Read(int word, int row) {
        return (word >> (row * 2)) & 3;
    }

    // Load-time check: rows that do not exist are dropped, and the unused pattern 3 means nothing made.
    public static int Clean(int word) {
        int clean = 0;
        for (int i = 0; i < All.Length; i++) {
            int v = Read(word, i);
            if (v == 1 || v == 2) {
                clean |= v << (i * 2);
            }
        }
        return clean;
    }

    // ---- the open case file

    // 0 not made (or no open file), 1 or 2.
    public static int Picked(string id) {
        int row = Index(id);
        if (row < 0 || CampaignSave.Active < 0) {
            return 0;
        }
        return Read(CampaignSave.Get(CampaignSave.Active).choices, row);
    }

    public static bool Is(string id, string key) {
        Row r = Get(id);
        int p = Picked(id);
        return r != null && p > 0 && r.Keys[p - 1] == key;
    }

    public static int MadeCount() {
        int n = 0;
        foreach (Row r in All) {
            if (Picked(r.Id) > 0) {
                n++;
            }
        }
        return n;
    }

    public static void Pick(string id, int option) {
        int row = Index(id);
        if (row >= 0) {
            CampaignSave.Choose(row, option);
        }
    }

    // ---- conditional lines

    // "?|<id>:<key>|S|text" becomes "S|text" when that option was taken, else null. Any other line comes back as it is.
    public static string Expand(string line) {
        if (line == null || !line.StartsWith("?|")) {
            return line;
        }
        string rest = line.Substring(2);
        int bar = rest.IndexOf('|');
        if (bar < 0) {
            return null;
        }
        string cond = rest.Substring(0, bar);
        int colon = cond.IndexOf(':');
        if (colon < 0 || !Is(cond.Substring(0, colon), cond.Substring(colon + 1))) {
            return null;
        }
        return rest.Substring(bar + 1);
    }

    public static string[] Resolve(string[] lines) {
        if (lines == null) {
            return null;
        }
        var kept = new List<string>(lines.Length);
        foreach (string line in lines) {
            string l = Expand(line);
            if (l != null && !l.StartsWith("Q|")) {
                kept.Add(l);
            }
        }
        return kept.ToArray();
    }
}
