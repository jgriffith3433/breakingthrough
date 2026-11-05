using UnityEngine;

// The hub's life between chapters (Docs/Story.md, "The hub after the chapters"): what the lodgers say beyond their one-liners
// (Chats, two unlock after every chapter, three each for Tick, Brim, Spare and Chime), the side errands (Side: a favour for a
// lodger and a loose notice per chapter, on top of Chime's lost thing), where everyone is as the file advances (Spots, Desk) and
// the ending lines those errands change (read through Choices.Expand as "?|chat:<id>|", "?|side:<id>|" and "?|sides:<n>|").
// ChapterHub plays all of it; the case file keeps it in Slot.chats and Slot.side (CampaignSave version 8). Campaign only.
public static class HubLife {
    // ---------------------------------------------------------------- Conversations

    // One optional conversation. Needs = chapters cleared before it opens. Lines: "W|" Wallit, "L|" the lodger, "S|" Slice.
    public class Chat {
        public string Id;
        public int Lodger;     // index in Story.Lodgers: 0 Tick, 1 Brim, 2 Spare, 3 Chime
        public int Needs;
        public string[] Lines;
    }

    public const int Tick = 0, Brim = 1, Spare = 2, Chime = 3;

    // Never reorder: Slot.chats keeps one bit per row.
    public static readonly Chat[] Chats = {
        new Chat { Id = "tick1", Lodger = Tick, Needs = 1, Lines = new[] {
            "L|Nobody has wound me since the Department stopped. I keep the hour out of habit.",
            "W|Who pays you for it?",
            "L|Nobody. That is the habit.",
            "W|Then I'll start the tab. Pay up, eventually.",
        } },
        new Chat { Id = "spare1", Lodger = Spare, Needs = 1, Lines = new[] {
            "L|Chirp! I was half a Yo Bot on a hook. Now I am half a Yo Bot in a nice room!",
            "W|You're not afraid of the circle?",
            "L|Slice looked at me. Then she looked away. That is a welcome! Chirp!",
        } },
        new Chat { Id = "brim1", Lodger = Brim, Needs = 2, Lines = new[] {
            "L|The gate rang when you came back. A hat knows a door when it hears one.",
            "W|You've got no head. How do you hear?",
            "L|Through the brim, Wallit. Everything arrives through the brim.",
        } },
        new Chat { Id = "chime1", Lodger = Chime, Needs = 2, Lines = new[] {
            "L|Ding. I ring when a thing is lost. It rang all through your chapter.",
            "W|What did it ring for?",
            "L|Everything. Ding. It is a loud room, the world.",
            "S|Noted.",
        } },
        new Chat { Id = "tick2", Lodger = Tick, Needs = 3, Lines = new[] {
            "L|The belt had a clock too. It ran slow on purpose. Slow is cheaper.",
            "W|Slow is never cheaper. Ask my bill.",
            "L|Then you and the foreman disagree. I side with the bill.",
        } },
        new Chat { Id = "spare2", Lodger = Spare, Needs = 3, Lines = new[] {
            "L|They called me a spare. Kept in case. Chirp... in case of what?",
            "W|In case somebody needed you.",
            "L|Somebody does?",
            "W|I'm standing here, aren't I. Exact change says yes.",
        } },
        new Chat { Id = "brim2", Lodger = Brim, Needs = 4, Lines = new[] {
            "L|Three refusals on the Long Walk. I counted them from here.",
            "W|You were in the hub.",
            "L|Hats go where heads go, Wallit. In spirit. I had your head in mind.",
        } },
        new Chat { Id = "chime2", Lodger = Chime, Needs = 4, Lines = new[] {
            "L|Ding. The server's bell was mine once. A cousin. It rang for papers.",
            "W|And you?",
            "L|I ring for what is missing. Ding. Which is better, I think.",
        } },
        new Chat { Id = "tick3", Lodger = Tick, Needs = 5, Lines = new[] {
            "L|Blank forms, all of them. That is a quiet kind of hour. Nobody owes anything in it.",
            "W|Sounds bad for business.",
            "L|It sounds like rest. I would like some. Is that allowed?",
            "W|Take it. I'll bill the Department.",
        } },
        new Chat { Id = "brim3", Lodger = Brim, Needs = 5, Lines = new[] {
            "L|The pages went blank and I felt lighter. I think I was a form once. A form for a head.",
            "W|Then you're due a head.",
            "L|I would settle for a wall. A wall with a hook. An address, Wallit.",
        } },
        new Chat { Id = "spare3", Lodger = Spare, Needs = 6, Lines = new[] {
            "L|It's filed! The order is gone! What do I cheer for now?",
            "W|Anything. Yourself.",
            "L|Uploading... me! Chirp! ...Oh. That one worked.",
            "S|Clean.",
        } },
        new Chat { Id = "chime3", Lodger = Chime, Needs = 6, Lines = new[] {
            "L|Ding. All six chapters, all six things back. The room is quieter now.",
            "W|Quiet is cheap.",
            "L|Quiet is cheap, and I rang for it anyway. Ding. Thank you, Wallit.",
        } },
    };

    public static int ChatIndex(string id) {
        for (int i = 0; i < Chats.Length; i++) {
            if (Chats[i].Id == id) {
                return i;
            }
        }
        return -1;
    }

    public static bool Heard(CampaignSave.Slot s, int chat) {
        return s != null && chat >= 0 && chat < Chats.Length && (s.chats & (1 << chat)) != 0;
    }

    public static int HeardCount(CampaignSave.Slot s) {
        return Bits(s != null ? s.chats : 0);
    }

    // The next conversation this lodger has for the chapters cleared, or -1.
    public static int Pending(CampaignSave.Slot s, int lodger) {
        for (int i = 0; i < Chats.Length; i++) {
            if (Chats[i].Lodger == lodger && Chats[i].Needs <= s.cleared && !Heard(s, i)) {
                return i;
            }
        }
        return -1;
    }

    // How many conversations are open and unheard, over the whole room.
    public static int PendingCount(CampaignSave.Slot s) {
        int n = 0;
        for (int i = 0; i < Chats.Length; i++) {
            if (Chats[i].Needs <= s.cleared && !Heard(s, i)) {
                n++;
            }
        }
        return n;
    }

    public static string Spoken(Chat chat) {
        var sb = new System.Text.StringBuilder();
        string name = Story.Lodgers[chat.Lodger].Name;
        foreach (string line in chat.Lines) {
            if (sb.Length > 0) {
                sb.Append('\n');
            }
            string who = line[0] == 'W' ? "Wallit" : line[0] == 'S' ? "Slice" : name;
            sb.Append(who).Append(": \"").Append(line.Substring(2)).Append('"');
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Side errands

    // Favour: a lodger asks for a lost bit, Wallit carries it back. Notice: a loose slip lies in the room; walking over it pins it
    // to the wall. Each chapter has one of each, opened when that chapter is cleared. Chime's lost thing (Story.Errands) is the third.
    public class Side {
        public string Id;
        public int Chapter;     // 0-based: opens once Chapter + 1 are cleared
        public bool Notice;     // false: a favour for Giver
        public int Giver;       // lodger index (favours)
        public string Thing;
        public string Ask;      // the giver's request (favours); the notice's own text (notices)
        public string Thanks;
        public int Xp;
        public Vector3 Spot;    // hub coordinates
    }

    // Never reorder: Slot.side keeps one bit per row.
    public static readonly Side[] Sides = {
        new Side { Id = "lid", Chapter = 0, Giver = Spare, Thing = "Yo Bot Lid", Xp = 15, Spot = new Vector3(-11.5f, 0f, -3f),
            Ask = "My lid is somewhere in the room! I cannot find it without a head! Chirp! Please?",
            Thanks = "My lid! It clicks! I am a whole three-quarters now! Chirp!" },
        new Side { Id = "notice1", Chapter = 0, Notice = true, Thing = "Strike Notice", Xp = 10, Spot = new Vector3(-7.5f, 0f, -1f),
            Ask = "STRIKE THIS ADDRESS. Struck through, by hand.", Thanks = "Pinned." },
        new Side { Id = "ribbon", Chapter = 1, Giver = Brim, Thing = "Hatband", Xp = 20, Spot = new Vector3(12f, 0f, -6f),
            Ask = "My band slipped off at the gate. Dark ribbon, brass thread. Bring it? A hat is nothing without its band.",
            Thanks = "My band! Now I am a hat with a past. Thank you, Wallit." },
        new Side { Id = "notice2", Chapter = 1, Notice = true, Thing = "Gate Pass", Xp = 15, Spot = new Vector3(7.5f, 0f, 0.5f),
            Ask = "GATE PASS: one return, no questions.", Thanks = "Pinned." },
        new Side { Id = "pin", Chapter = 2, Giver = Tick, Thing = "Mainspring Pin", Xp = 25, Spot = new Vector3(-3f, 0f, 12f),
            Ask = "A pin fell out of me on the belt. Small, brass, important. I would fetch it myself, but I am a clock.",
            Thanks = "The pin. Now the hour holds. Thank you. I will not forget the minute." },
        new Side { Id = "notice3", Chapter = 2, Notice = true, Thing = "Belt Slip", Xp = 20, Spot = new Vector3(-8f, 0f, 5.5f),
            Ask = "BELT SLIP: 4,112 items. Not one of them delivered.", Thanks = "Pinned." },
        new Side { Id = "whistle", Chapter = 3, Giver = Spare, Thing = "Chirp Whistle", Xp = 30, Spot = new Vector3(13f, 0f, 8f),
            Ask = "My chirp has a whistle in it and it went missing on the Long Walk! Chirp... without it I only squeak!",
            Thanks = "CHIRP! Oh. I mean chirp, chirp. Marvellous. Thank you!" },
        new Side { Id = "notice4", Chapter = 3, Notice = true, Thing = "Lamp Notice", Xp = 25, Spot = new Vector3(11f, 0f, -1f),
            Ask = "LAMP NOTICE: three refusals, three lamps. All lit.", Thanks = "Pinned." },
        new Side { Id = "label", Chapter = 4, Giver = Brim, Thing = "Address Label", Xp = 35, Spot = new Vector3(-11f, 0f, 12f),
            Ask = "A label blew in from the reading room. Blank, but gummed. If it had my name on it... find it, Wallit.",
            Thanks = "Blank. Lovely. I will write my own name on it. Slowly." },
        new Side { Id = "notice5", Chapter = 4, Notice = true, Thing = "Denied Slip", Xp = 30, Spot = new Vector3(-13f, 0f, 0f),
            Ask = "DENIED. Stamped, and good.", Thanks = "Pinned." },
        new Side { Id = "key", Chapter = 5, Giver = Tick, Thing = "Winding Key", Xp = 40, Spot = new Vector3(3f, 0f, -8.5f),
            Ask = "The Department never gave me a key. There is one lying about, I can hear it. Wind me yourself, Wallit?",
            Thanks = "Tick. Tock. Tick. Mine, now. Thank you, Wallit. I will mind the hour." },
        new Side { Id = "notice6", Chapter = 5, Notice = true, Thing = "Filed Receipt", Xp = 35, Spot = new Vector3(0f, 0f, -8f),
            Ask = "RECEIVED. In full.", Thanks = "Pinned." },
    };

    public static int SideIndex(string id) {
        for (int i = 0; i < Sides.Length; i++) {
            if (Sides[i].Id == id) {
                return i;
            }
        }
        return -1;
    }

    public static bool SideDone(CampaignSave.Slot s, int row) {
        return s != null && row >= 0 && row < Sides.Length && (s.side & (1 << row)) != 0;
    }

    public static int SideCount(CampaignSave.Slot s) {
        return Bits(s != null ? s.side : 0);
    }

    public static bool SideOpen(CampaignSave.Slot s, int row) {
        return Sides[row].Chapter < s.cleared;
    }

    // The next open, undone favour this lodger asks for, or -1.
    public static int FavourFor(CampaignSave.Slot s, int lodger) {
        for (int i = 0; i < Sides.Length; i++) {
            if (!Sides[i].Notice && Sides[i].Giver == lodger && SideOpen(s, i) && !SideDone(s, i)) {
                return i;
            }
        }
        return -1;
    }

    // Open errands left in the room: favours and notices not done yet (Chime's lost things are counted by ChapterHub).
    public static int OpenCount(CampaignSave.Slot s) {
        int n = 0;
        for (int i = 0; i < Sides.Length; i++) {
            if (SideOpen(s, i) && !SideDone(s, i)) {
                n++;
            }
        }
        return n;
    }

    // Errands finished in a chapter, Chime's included (0 to 3).
    public static int ChapterErrands(CampaignSave.Slot s, int chapter) {
        int n = (s.errands & (1 << chapter)) != 0 ? 1 : 0;
        for (int i = 0; i < Sides.Length; i++) {
            if (Sides[i].Chapter == chapter && SideDone(s, i)) {
                n++;
            }
        }
        return n;
    }

    // ---------------------------------------------------------------- Where everyone is

    // The office as the file advances: Stage 0 (up to 1 filed), 1 (2-3), 2 (4-5), 3 (all six). One lodger takes the desk each
    // stage and the others move about; the plate on the desk says who is in.
    public static int Stage(int cleared) {
        return cleared >= 6 ? 3 : cleared >= 4 ? 2 : cleared >= 2 ? 1 : 0;
    }

    static readonly Vector3 DeskSpot = new Vector3(10f, 0f, 3f);

    static readonly Vector3[][] Spots = {
        new[] { DeskSpot, new Vector3(-9f, 0f, 10f), new Vector3(8.5f, 0f, -4.5f), new Vector3(-8.5f, 0f, -5f) },
        new[] { new Vector3(7f, 0f, 11f), new Vector3(-9f, 0f, 10f), DeskSpot, new Vector3(-8.5f, 0f, -5f) },
        new[] { new Vector3(7f, 0f, 11f), DeskSpot, new Vector3(-9f, 0f, -1.5f), new Vector3(-6f, 0f, -6.5f) },
        new[] { DeskSpot, new Vector3(-5f, 0f, 10f), new Vector3(5.5f, 0f, -3f), new Vector3(-5.5f, 0f, -3.5f) },
    };

    public static readonly string[] DeskPlate = { "TICK", "SPARE", "BRIM", "TICK" };

    public static Vector3 SpotOf(int lodger, int cleared) {
        return Spots[Stage(cleared)][lodger];
    }

    // ---------------------------------------------------------------- Helpers

    static int Bits(int v) {
        int n = 0;
        while (v != 0) {
            n += v & 1;
            v >>= 1;
        }
        return n;
    }

    // The condition of a "?|chat:<id>|", "?|side:<id>|" or "?|sides:<n>|" ending line, on the open case file.
    public static bool Holds(string kind, string key) {
        if (CampaignSave.Active < 0) {
            return false;
        }
        CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
        if (kind == "chat") {
            return Heard(s, ChatIndex(key));
        }
        if (kind == "side") {
            return SideDone(s, SideIndex(key));
        }
        int n;
        return kind == "sides" && int.TryParse(key, out n) && SideCount(s) >= n;
    }

    public static bool IsCondition(string kind) {
        return kind == "chat" || kind == "side" || kind == "sides";
    }
}
