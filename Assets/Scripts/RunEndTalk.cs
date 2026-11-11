using System.Collections.Generic;
using UnityEngine;

// Picks one Wallit/Slice exchange for the Arcade run-end screen from facts about that run
// (Docs/Story.md, "Run-end exchanges"). Never repeats the same id within five runs.
public static class RunEndTalk {
    public const int RecentCap = 5;

    public struct Pick {
        public string Id;
        public string Wallit;
        public string Slice;
        public string When;
    }

    public struct Facts {
        public int Wave;
        public int Cleared;
        public bool NewBest;
        public bool SliceNearlyFell;
        public bool BossKill;
        public string KilledBy;
        public int CoinsThisRun;
        public int HomeRuns;
    }

    static readonly List<string> recent = new List<string>();
    static bool loaded;
    public static string LastId { get; private set; }
    public static Pick LastPick { get; private set; }

    public static Pick Choose(Facts facts) {
        Ensure();
        for (int i = 0; i < Story.RunEnds.Length; i++) {
            Story.RunEnd row = Story.RunEnds[i];
            if (!Matches(row.When, facts)) {
                continue;
            }
            if (recent.Contains(row.Id) && row.When != "default") {
                continue;
            }
            return Commit(row);
        }
        // Every match was recent: take the first that matches, even a repeat, then the default.
        for (int i = 0; i < Story.RunEnds.Length; i++) {
            Story.RunEnd row = Story.RunEnds[i];
            if (Matches(row.When, facts)) {
                return Commit(row);
            }
        }
        return Commit(Story.RunEnds[Story.RunEnds.Length - 1]);
    }

    // Playtest: clear the recent window so five losses can each pick a different exchange.
    public static void ClearRecent() {
        Ensure();
        recent.Clear();
        Save();
    }

    public static string[] RecentIds() {
        Ensure();
        return recent.ToArray();
    }

    static Pick Commit(Story.RunEnd row) {
        recent.Remove(row.Id);
        recent.Insert(0, row.Id);
        while (recent.Count > RecentCap) {
            recent.RemoveAt(recent.Count - 1);
        }
        Save();
        LastId = row.Id;
        LastPick = new Pick { Id = row.Id, Wallit = row.Wallit, Slice = row.Slice, When = row.When };
        return LastPick;
    }

    static bool Matches(string when, Facts f) {
        switch (when) {
            case "early": return f.Wave < 5;
            case "yo_bot": return !f.BossKill && IsYo(f.KilledBy);
            case "spider": return !f.BossKill && IsSpider(f.KilledBy);
            case "boss": return f.BossKill;
            case "new_best": return f.NewBest;
            case "nearly": return f.SliceNearlyFell;
            case "near_decade": return f.Wave % 10 >= 8 && f.Wave < 30;
            case "act2": return f.Wave >= 10 && f.Wave < 20;
            case "act3": return f.Wave >= 20;
            case "payday": return f.CoinsThisRun >= 15;
            case "longball": return f.HomeRuns >= 3;
            case "default": return true;
            default: return false;
        }
    }

    static bool IsYo(string slug) {
        if (string.IsNullOrEmpty(slug)) {
            return false;
        }
        return slug.IndexOf("spider") < 0 && slug.IndexOf("yo") >= 0;
    }

    static bool IsSpider(string slug) {
        return !string.IsNullOrEmpty(slug) && slug.IndexOf("spider") >= 0;
    }

    static void Ensure() {
        if (loaded) {
            return;
        }
        loaded = true;
        if (MetaProgress.Sandbox) {
            return;
        }
        string packed = PlayerPrefs.GetString("bt_run_end_recent", "");
        if (string.IsNullOrEmpty(packed)) {
            return;
        }
        string[] parts = packed.Split(',');
        for (int i = 0; i < parts.Length && recent.Count < RecentCap; i++) {
            if (!string.IsNullOrEmpty(parts[i])) {
                recent.Add(parts[i]);
            }
        }
    }

    static void Save() {
        if (MetaProgress.Sandbox) {
            return;
        }
        PlayerPrefs.SetString("bt_run_end_recent", string.Join(",", recent.ToArray()));
        PlayerPrefs.Save();
    }
}
