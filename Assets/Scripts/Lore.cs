using System.Collections.Generic;
using UnityEngine;

// The lore log: what the Department's files say, one entry per thing the story has shown. An entry unlocks when the story
// reveals it: Reveal 0 on opening a case file, otherwise when that chapter is filed (the Chapter Complete card says "New
// lore"). Unlocks are a bit per entry in CampaignSave.Slot.lore, so each case file keeps its own. Docs/Story.md, "The lore log".
// Voice: the Department's own filing language, flat and polite; the creature behind the order is never named.
public static class Lore {
    public class Entry {
        public string Title;
        public int Reveal;     // 0: on opening the file; n: when Chapter n is filed
        public string Text;
    }

    // Order is the bit: never reorder or reuse a row, only add at the end (max 31).
    public static readonly Entry[] All = {
        new Entry { Title = "The Circle", Reveal = 0,
            Text = "The one filed address in the Moonlit Hollow. A thing inside it exists. A thing outside it is cleared. Slice keeps it with a scalpel knife and does not play the piano any more." },
        new Entry { Title = "Wallit", Reveal = 0,
            Text = "One wallet, one wooden bat, no refunds. Stamped RETURN TO SENDER and thrown into the Hollow. No sender was found. While he stands in the circle he is on the books: he can be hurt, and he can be paid." },
        new Entry { Title = "The Place Department", Reveal = 0,
            Text = "Files the things that should not exist. A filed thing has an address and is allowed to stay. An unfiled thing is cleared. The Department does not hate anyone. It only clears." },
        new Entry { Title = "The Courier", Reveal = 1,
            Text = "An Alpha Yo Bot with a clipboard. It reads a notice of striking aloud, then delivers it to the circle. Answered with: Return to sender. The first sign that the bots work from paper." },
        new Entry { Title = "The Boundary Wall", Reveal = 2,
            Text = "The Hollow ends at a Department wall, 205 metres out, with a gate that has no handle. A knock is answered with the same RETURN TO SENDER stamp, pressed through from the other side. The postmark torn into the stone is half of Wallit's own." },
        new Entry { Title = "Plated Yo Bots", Reveal = 2,
            Text = "A Yo Bot sent again with armour when the cheap one failed. The Plated Alpha carries a red plate stamped INVALID. Wallit says so does his refund." },
        new Entry { Title = "The Clearing Floor", Reveal = 3,
            Text = "A sunken yard beyond the broken gate, lit by conveyor lamps. One batch of Yo Bots is stamped out for every unfiled thing in the Hollow. The belt carries the cleared things away: a hat, a clock, a small bell. It ends at a red lamp that is a lens, turned toward the circle." },
        new Entry { Title = "Yo Bot Origins: the Clearing Crew", Reveal = 3,
            Text = "Yo Bots are the Clearing Crew. They chirp because every job closes with a report: Uploading victory means filed as cleared. Plated and Mega Yo Bots are the same unit sent again, heavier." },
        new Entry { Title = "Bolts", Reveal = 3,
            Text = "A Yo Bot is held together by its pay. The bolts it drops are what the Floor owes it. Wallit has been collecting what is owed." },
        new Entry { Title = "The Foreman", Reveal = 3,
            Text = "A Mega Yo Bot grown big and amber, the order held on its chest. It reads the order aloud: STRIKE. THIS. ADDRESS. It did not write it." },
        new Entry { Title = "Spider Bot Origins: Process Servers", Reveal = 4,
            Text = "Spider Bots are not clearers. They are process servers: deliveries with one paper to hand over, who stand and wait while it is read. Six legs, because they walk the whole length of the path. The paper is an address, struck." },
        new Entry { Title = "Slice's Address", Reveal = 4,
            Text = "The paper the Server offered was Slice's own. Her address was struck before Wallit came. The Server will not strike until it has been refused three times. That is how it works." },
        new Entry { Title = "The Order", Reveal = 5,
            Text = "STRIKE THIS ADDRESS. Every copy is identical, and nobody signs it. An order that is not signed can be denied. Pressed with the RETURN TO SENDER stamp, a page shows nothing, then DENIED." },
        new Entry { Title = "The Registrar", Reveal = 5,
            Text = "A gold Titan Spider Bot with a ledger held open. It keeps the Department's copies and reads the pages with every leg. Struck from the record." },
        new Entry { Title = "Address Filed", Reveal = 6,
            Text = "The order that carried the wave 30 Spider Bot was only a form, and a form can be answered. DENIED. Slice filed the circle herself. The creature behind the order is not named here, because nothing here names it." },
    };

    // Entries revealed by the last Unlock call, for the Chapter Complete card.
    public static string[] LastUnlocked = new string[0];

    public static bool IsUnlocked(CampaignSave.Slot slot, int index) {
        return slot != null && slot.used && index >= 0 && index < All.Length && (slot.lore & (1 << index)) != 0;
    }

    public static int UnlockedCount(CampaignSave.Slot slot) {
        int n = 0;
        for (int i = 0; i < All.Length; i++) {
            n += IsUnlocked(slot, i) ? 1 : 0;
        }
        return n;
    }

    // Unlocks every entry whose Reveal is at most `filed` chapters filed (0 = the file just opened). Returns whether
    // anything new unlocked; the new titles are in LastUnlocked.
    public static bool Unlock(CampaignSave.Slot slot, int filed) {
        var fresh = new List<string>();
        for (int i = 0; i < All.Length; i++) {
            int bit = 1 << i;
            if (All[i].Reveal <= filed && (slot.lore & bit) == 0) {
                slot.lore |= bit;
                fresh.Add(All[i].Title);
            }
        }
        LastUnlocked = fresh.ToArray();
        return fresh.Count > 0;
    }
}
