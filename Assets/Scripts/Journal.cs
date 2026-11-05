using System.Text;
using UnityEngine;

// The Department's journal: one hidden find and one optional objective in each Campaign chapter, kept per case file
// (CampaignSave.Slot.finds / goals, a bit per chapter). Docs/Story.md, "The journal". A find is a lost thing lying out in
// the chapter's field (walk onto it to take it; no marker, only a soft ring when Wallit is close). An objective is an
// extra the chapter never asks for, judged when the chapter is filed. Having every find and every objective is Paid in
// Full: the bat wears gold bands and the case file carries the stamp. Chime's hub errands (Story.Errands) are separate.
// Voice: the Department's own filing language, flat and polite; Slice clipped, Wallit money-minded.
public static class Journal {
    public enum GoalKind {
        SliceUntouched,   // Slice takes no damage in the chapter
        HomeRuns,         // Target home runs
        SmashHits,        // Target bots hit by ground smashes
        Finishers,        // Target combo finishers
        Within,           // filed within Target seconds
    }

    public class Find {
        public string Name;
        public string Hint;     // where to look, said as the Department would
        public string Text;     // what it is, shown once it is in the journal
        public float Dist;      // metres from the circle
        public float Bearing;   // degrees from the circle
    }

    public class Goal {
        public string Name;
        public string Ask;      // the objective, as the journal lists it
        public string Done;     // said when it is met
        public GoalKind Kind;
        public int Target;
    }

    // Index = chapter. Never reorder: a bit per row is saved. Distances stay inside the 150 m playfield and off the
    // bearings the chapters use for their own stops (150, 200 to 210, 330).
    public static readonly Find[] Finds = {
        new Find { Name = "Unsent Postcard", Dist = 60f, Bearing = 40f,
            Hint = "Sixty paces north-east, face up in the grass.",
            Text = "A postcard with no sender and a half-rubbed address. It says: Wish you were filed." },
        new Find { Name = "Key With No Lock", Dist = 55f, Bearing = 270f,
            Hint = "West, where the Hollow thins toward the wall.",
            Text = "A brass key that fits no handle. The gate has none. Somebody kept it anyway." },
        new Find { Name = "A Cleared Hat", Dist = 75f, Bearing = 80f,
            Hint = "East of the Floor's lamps, off the belt.",
            Text = "A hat that fell off the belt. The label inside reads BRIM. Nobody has come for it." },
        new Find { Name = "Thrice-Refused Paper", Dist = 70f, Bearing = 120f,
            Hint = "South-east, away from the Long Walk's lamps.",
            Text = "A notice stamped REFUSED three times and never struck. It is the only paper that ever came back." },
        new Find { Name = "Ledger Leaf", Dist = 50f, Bearing = 290f,
            Hint = "West of the desk, blown clear of the room.",
            Text = "One page from the Registrar's ledger. Pressed with the stamp it showed nothing, then DENIED." },
        new Find { Name = "The Circle's Stamp", Dist = 65f, Bearing = 190f,
            Hint = "South of the changed circle, where the light thins.",
            Text = "A small stamp, worn smooth. The face reads ADDRESS, filed backwards, so it prints true." },
    };

    public static readonly Goal[] Goals = {
        new Goal { Name = "Clean Hands", Kind = GoalKind.SliceUntouched, Target = 1,
            Ask = "File the chapter without Slice taking a scratch.", Done = "S|Clean." },
        new Goal { Name = "Long Ball", Kind = GoalKind.HomeRuns, Target = 2,
            Ask = "Hit 2 home runs.", Done = "W|Two out of the yard. That's a tip." },
        new Goal { Name = "Ground Rules", Kind = GoalKind.SmashHits, Target = 6,
            Ask = "Hit 6 bots with ground smashes.", Done = "W|Flat. Every one. Pay up." },
        new Goal { Name = "Finishing Touches", Kind = GoalKind.Finishers, Target = 12,
            Ask = "Land 12 combo finishers.", Done = "S|Third hits. All twelve." },
        new Goal { Name = "Quick Reader", Kind = GoalKind.Within, Target = 540,
            Ask = "File the chapter within 9 minutes.", Done = "W|Under time. Charge extra." },
        new Goal { Name = "Last Word", Kind = GoalKind.HomeRuns, Target = 4,
            Ask = "Hit 4 home runs.", Done = "S|The last word was a ball. Good." },
    };

    public const string PaidTitle = "PAID IN FULL";
    public const string PaidLine = "S|Every find kept. Every extra filed. Paid in full.";

    public static int Count { get { return Finds.Length; } }

    // Where a find lies: the circle's position, a bearing and a distance away, on the ground.
    public static Vector3 FindPoint(int chapter, Vector3 circle) {
        Find f = Finds[Mathf.Clamp(chapter, 0, Finds.Length - 1)];
        float a = f.Bearing * Mathf.Deg2Rad;
        Vector3 at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * f.Dist;
        at.y = circle.y;
        if (Physics.Raycast(new Vector3(at.x, 60f, at.z), Vector3.down, out RaycastHit hit, 120f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        return at;
    }

    public static bool HasFind(CampaignSave.Slot s, int chapter) {
        return s != null && s.used && chapter >= 0 && chapter < Finds.Length && (s.finds & (1 << chapter)) != 0;
    }

    public static bool HasGoal(CampaignSave.Slot s, int chapter) {
        return s != null && s.used && chapter >= 0 && chapter < Goals.Length && (s.goals & (1 << chapter)) != 0;
    }

    public static int FindCount(CampaignSave.Slot s) {
        int n = 0;
        for (int i = 0; i < Finds.Length; i++) {
            n += HasFind(s, i) ? 1 : 0;
        }
        return n;
    }

    public static int GoalCount(CampaignSave.Slot s) {
        int n = 0;
        for (int i = 0; i < Goals.Length; i++) {
            n += HasGoal(s, i) ? 1 : 0;
        }
        return n;
    }

    public static bool PaidInFull(CampaignSave.Slot s) {
        return FindCount(s) == Finds.Length && GoalCount(s) == Goals.Length;
    }

    // The case file being played right now (null outside a chapter): the gilt bat reads it.
    public static bool GiltBat {
        get {
            if (ChapterRun.Instance == null || CampaignSave.Active < 0) {
                return false;
            }
            return PaidInFull(CampaignSave.Get(CampaignSave.Active));
        }
    }

    // Takes a find into the open file and saves it at once. Returns false when it was already kept.
    public static bool Keep(int chapter) {
        if (CampaignSave.Active < 0 || chapter < 0 || chapter >= Finds.Length) {
            return false;
        }
        CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
        int bit = 1 << chapter;
        if ((s.finds & bit) != 0) {
            return false;
        }
        s.finds |= bit;
        CampaignSave.Flush();
        return true;
    }

    // Marks an objective met in the open file and saves it at once. Returns false when it was already met.
    public static bool Meet(int chapter) {
        if (CampaignSave.Active < 0 || chapter < 0 || chapter >= Goals.Length) {
            return false;
        }
        CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
        int bit = 1 << chapter;
        if ((s.goals & bit) != 0) {
            return false;
        }
        s.goals |= bit;
        CampaignSave.Flush();
        return true;
    }

    // Whether the chapter's objective holds, given what this play of it did.
    public static bool Holds(int chapter, int sliceDamage, int homeRuns, int smashHits, int finishers, float seconds) {
        Goal g = Goals[Mathf.Clamp(chapter, 0, Goals.Length - 1)];
        switch (g.Kind) {
            case GoalKind.SliceUntouched: return sliceDamage <= 0;
            case GoalKind.HomeRuns: return homeRuns >= g.Target;
            case GoalKind.SmashHits: return smashHits >= g.Target;
            case GoalKind.Finishers: return finishers >= g.Target;
            case GoalKind.Within: return seconds <= g.Target;
        }
        return false;
    }

    // What the journal shows for one tab of a case file. Tabs: 0 Active, 1 Finished, 2 Collected.
    public static readonly string[] TabNames = { "Active", "Finished", "Collected" };

    public static string TabText(CampaignSave.Slot s, int tab) {
        var sb = new StringBuilder();
        int reached = s != null && s.used ? Mathf.Clamp(Mathf.Max(s.reached, s.chapter), 0, Goals.Length - 1) : -1;
        int rows = 0;
        if (tab == 0) {
            for (int i = 0; i <= reached; i++) {
                if (!HasGoal(s, i)) {
                    sb.Append("Ch ").Append(i + 1).Append(" - ").Append(Goals[i].Name).Append(": ").Append(Goals[i].Ask).Append("\n");
                    rows++;
                }
                if (!HasFind(s, i)) {
                    sb.Append("Ch ").Append(i + 1).Append(" - lost thing, ").Append(Finds[i].Hint).Append("\n");
                    rows++;
                }
                if (HasFind(s, i) && !BackRooms.Entered(s, i)) {
                    sb.Append("Ch ").Append(i + 1).Append(" - back room, the clue says: ").Append(BackRooms.All[i].Clue).Append("\n");
                    rows++;
                }
            }
            if (!BackRooms.HasReward(s) && BackRooms.Entered(s, BackRooms.EncounterChapter)) {
                sb.Append("Ch ").Append(BackRooms.EncounterChapter + 1).Append(" - ").Append(BackRooms.EncounterName).Append(" still waits in its back room.\n");
                rows++;
            }
            if (rows == 0) {
                sb.Append(reached < 0 ? "No case file yet." : "Nothing open. Every chapter reached is squared away.");
            }
        } else if (tab == 1) {
            for (int i = 0; i < Goals.Length; i++) {
                if (HasGoal(s, i)) {
                    sb.Append("Ch ").Append(i + 1).Append(" - ").Append(Goals[i].Name).Append(": ").Append(Goals[i].Ask).Append("\n");
                    rows++;
                }
            }
            if (rows == 0) {
                sb.Append("Nothing finished yet. The extras are never asked for.");
            }
        } else {
            for (int i = 0; i < Finds.Length; i++) {
                if (HasFind(s, i)) {
                    sb.Append(Finds[i].Name).Append(": ").Append(Finds[i].Text).Append("\n");
                    rows++;
                }
            }
            for (int i = 0; i < BackRooms.All.Length; i++) {
                if (BackRooms.Entered(s, i)) {
                    sb.Append(BackRooms.All[i].Name).Append(": ").Append(BackRooms.All[i].Text).Append("\n");
                    rows++;
                }
            }
            if (BackRooms.HasReward(s)) {
                sb.Append(BackRooms.RewardName).Append(": ").Append(BackRooms.RewardText).Append("\n");
                rows++;
            }
            if (rows == 0) {
                sb.Append("Nothing kept yet. Each chapter hides one lost thing.");
            }
        }
        return sb.ToString().TrimEnd('\n');
    }

    // "Finds 2/6   Extras 1/6" and the stamp when it is full.
    public static string Summary(CampaignSave.Slot s) {
        string line = "Finds " + FindCount(s) + "/" + Finds.Length + "   Extras " + GoalCount(s) + "/" + Goals.Length + "   Rooms " + BackRooms.Count(s) + "/" + BackRooms.All.Length;
        line = PaidInFull(s) ? line + "   " + PaidTitle : line;
        return s != null && s.secret ? line + "   " + Story.SecretTitle : line;
    }
}
