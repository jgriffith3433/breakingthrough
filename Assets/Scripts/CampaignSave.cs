using System;
using UnityEngine;

// Campaign progress: three case files (save slots). Saved as JSON under "hawkes.campaign" in PlayerPrefs (localStorage
// on the web). Separate from MetaProgress, which is Arcade's coins and shop. A sandboxed run (the playtest) keeps the
// files in memory and never writes.
public static class CampaignSave {
    public const int SlotCount = 3;
    public const string Scene = "Campaign";
    const string Key = "hawkes.campaign";

    [Serializable]
    public class Slot {
        public bool used;
        public int chapter;      // 0-based index into Story.Chapters: the chapter the file is on
        public int reached;      // furthest chapter opened
        public int cleared;      // chapters finished: the Chapter Complete card was reached
        public float seconds;    // time on the books
        public long saved;       // UTC ticks of the last save
        public int xp;           // Campaign XP earned in this file's fights (CampaignProgress): level and abilities follow from it
        public int unlocked;     // bit per CampaignProgress.Abilities row, set at the level-up that unlocked it
        public int errands;      // bit per chapter: Chime's errand done and the lost thing (Story.Errands) kept
        public int lore;         // bit per Lore.All row, set when the story reveals it (the file keeps its own log)
        public int finds;        // bit per chapter: its hidden find is in the journal (Journal.Finds)
        public int goals;        // bit per chapter: its optional objective is met (Journal.Goals)
        public int rooms;        // bit per chapter: its back room was entered (BackRooms.All); bit ChapterCount: the Dead Letter fell, Refund in Full is earned
        public int ranks;        // best Standard rank per chapter, 3 bits each (CampaignGrade: 0 none, 1 C, 2 B, 3 A, 4 S)
        public int hardRanks;    // the same for Hard
        public bool hard;        // Hard is chosen: only ever true on a filed file (every chapter cleared)
        public int plus;         // New Game+ loops begun on this file (NewGamePlus): xp, unlocks and Refund in Full carry over, the chapters start again
        public bool secret;      // the secret ending (Story.SecretEnding) has been seen on this file
    }

    // Save format. 1: the first files (no stamp on the oldest; no xp, unlocks, errands or lore bits). 2: stamped, every
    // field checked on load, the lore log derived from the chapters filed. 3: the journal's finds and goals
    // bits (nothing to derive: older files start with none). 4: the back rooms' bits and the Dead Letter's reward (older files
    // start with none). 5: the chapter ranks, Standard and Hard, and the Hard choice (older files start with none, Standard).
    // 6: New Game+ loops and the secret ending seen (older files start at none: nothing to derive).
    // Bump it with a step in Migrate.
    public const int Version = 6;
    const string BadKey = "hawkes.campaign.bad";

    [Serializable]
    class Data {
        public int version;   // 0 when the JSON has none: the oldest files
        public int last = -1;
        public Slot[] slots;
    }

    static Data data;
    static string sandboxJson;

    // What the last load did, for the case files panel and the playtest: "" (nothing to say), "migrated" or "corrupt".
    public static string LoadResult { get; private set; } = "";
    public static int LoadedVersion { get; private set; } = Version;
    // A line for the player, shown once on the case files; empty when the load was clean.
    public static string Notice { get; private set; } = "";
    public const string CorruptNotice = "Case files unreadable. The Department filed new ones.";
    public const string MigratedNotice = "Old case files found. The Department updated them.";

    // The file the chapter scene is playing; -1 when none is open.
    public static int Active { get; private set; } = -1;

    public static void AckNotice() {
        Notice = "";
    }

    // Reads save text into files. Never throws: text that is not a save (garbage, a cut-off write, the wrong shape)
    // gives null and the caller starts clean.
    static Data Parse(string json) {
        Data parsed;
        try {
            parsed = JsonUtility.FromJson<Data>(json);
        } catch (Exception) {
            return null;
        }
        return parsed != null && parsed.slots != null && json.TrimStart().StartsWith("{") ? parsed : null;
    }

    // Brings an older file up to Version and makes every field safe. Returns whether anything was older.
    static bool Migrate(Data d) {
        bool older = d.version < Version;
        if (d.version < 2) {
            // v1: no lore bits were kept, and the numbers were never checked. Rebuild what can be derived.
            string[] keep = Lore.LastUnlocked;
            for (int i = 0; i < d.slots.Length; i++) {
                Slot s = d.slots[i];
                if (s == null || !s.used) {
                    continue;
                }
                Lore.Unlock(s, Mathf.Clamp(s.cleared, 0, ChapterCount));
            }
            Lore.LastUnlocked = keep;
        }
        for (int i = 0; i < d.slots.Length; i++) {
            Slot s = d.slots[i];
            if (s == null) {
                continue;
            }
            int mask = (1 << ChapterCount) - 1;   // v3: a journal bit only for a chapter that exists (older text has none: 0)
            s.finds &= mask;
            s.goals &= mask;
            s.rooms &= (1 << (ChapterCount + 1)) - 1;   // v4: a bit per back room and one for the Dead Letter's reward (older text has none: 0)
            int rankMask = (1 << (3 * ChapterCount)) - 1;   // v5: 3 bits a chapter
            s.ranks &= rankMask;
            s.hardRanks &= rankMask;
            s.cleared = Mathf.Clamp(s.cleared, 0, ChapterCount);
            s.hard = s.hard && s.cleared >= ChapterCount;   // Hard only on a filed file
            s.chapter = Mathf.Clamp(s.chapter, 0, ChapterCount - 1);
            s.reached = Mathf.Clamp(Mathf.Max(s.reached, s.chapter), 0, ChapterCount - 1);
            s.xp = Mathf.Max(0, s.xp);
            s.plus = Mathf.Clamp(s.plus, 0, 99);   // v6
            s.seconds = float.IsNaN(s.seconds) || float.IsInfinity(s.seconds) ? 0f : Mathf.Max(0f, s.seconds);
        }
        d.version = Version;
        return older;
    }

    static void Ensure() {
        if (data != null) {
            return;
        }
        string json = MetaProgress.Sandbox ? sandboxJson : PlayerPrefs.GetString(Key, "");
        LoadResult = "";
        LoadedVersion = Version;
        Notice = "";
        if (!string.IsNullOrEmpty(json)) {
            data = Parse(json);
            if (data == null) {
                // Unreadable: keep the bad text aside (never over a good copy), start clean and say so.
                LoadResult = "corrupt";
                Notice = CorruptNotice;
                if (!MetaProgress.Sandbox) {
                    PlayerPrefs.SetString(BadKey, json);
                    PlayerPrefs.DeleteKey(Key);
                    PlayerPrefs.Save();
                }
            } else {
                LoadedVersion = data.version;
                if (data.version > Version) {
                    // Saved by a newer build: leave it be, read what we know.
                    LoadResult = "newer";
                } else if (Migrate(data)) {
                    LoadResult = "migrated";
                    Notice = MigratedNotice;
                    if (!MetaProgress.Sandbox) {
                        PlayerPrefs.SetString(Key + ".v" + LoadedVersion, json);   // the old text, kept once
                    }
                }
            }
        }
        if (data == null) {
            data = new Data { version = Version };
        }
        if (data.slots == null || data.slots.Length != SlotCount) {
            var fixedSlots = new Slot[SlotCount];
            for (int i = 0; i < SlotCount && data.slots != null && i < data.slots.Length; i++) {
                fixedSlots[i] = data.slots[i];
            }
            data.slots = fixedSlots;
        }
        for (int i = 0; i < SlotCount; i++) {
            if (data.slots[i] == null) {
                data.slots[i] = new Slot();
            }
        }
        if (LoadResult == "migrated") {
            Save();   // the updated files are written at once; the old text is kept under its version
        }
    }

    // Playtest setup: forget what is in memory and keep it there (see EnterSandbox).
    public static void EnterSandbox() {
        EnterSandbox(null);
    }

    // Playtest setup: the same, with save text standing where the stored files would (an old format, a corrupt one).
    public static void EnterSandbox(string json) {
        sandboxJson = json;
        data = null;
        Active = -1;
        Ensure();
    }

    // Playtest: the files as they would be written now.
    public static string ToText() {
        Ensure();
        return JsonUtility.ToJson(data);
    }

    public static Slot Get(int index) {
        Ensure();
        return data.slots[Mathf.Clamp(index, 0, SlotCount - 1)];
    }

    // The file most recently opened, or -1.
    public static int Last {
        get {
            Ensure();
            return data.last >= 0 && data.last < SlotCount && data.slots[data.last].used ? data.last : -1;
        }
    }

    public static int UsedCount {
        get {
            Ensure();
            int n = 0;
            foreach (Slot s in data.slots) {
                n += s.used ? 1 : 0;
            }
            return n;
        }
    }

    public static int ChapterCount {
        get { return Story.Chapters.Length; }
    }

    // Opens a file: an empty one starts at chapter 1. Returns the chapter index to play.
    public static int Open(int index) {
        Ensure();
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        Slot s = data.slots[index];
        if (!s.used) {
            s.used = true;
            s.chapter = 0;
            s.reached = 0;
            s.seconds = 0f;
        }
        s.chapter = Mathf.Clamp(s.chapter, 0, ChapterCount - 1);
        Lore.Unlock(s, s.cleared);
        Active = index;
        data.last = index;
        Touch(s);
        Save();
        return s.chapter;
    }

    // The open file's chapter was finished: the file moves on to the next one (the last stays put, marked cleared).
    // Saved at once, so leaving from the Chapter Complete card still resumes at the next chapter. Returns the chapter
    // the file is on now.
    public static int Complete(int chapter) {
        if (Active < 0) {
            return chapter;
        }
        Slot s = Get(Active);
        s.cleared = Mathf.Max(s.cleared, chapter + 1);
        s.chapter = Mathf.Clamp(Mathf.Max(s.chapter, chapter + 1), 0, ChapterCount - 1);
        s.reached = Mathf.Max(s.reached, s.chapter);
        Lore.Unlock(s, s.cleared);
        Touch(s);
        Save();
        return s.chapter;
    }

    // The best rank kept for a chapter (CampaignGrade.None when never graded), Standard or Hard.
    public static int BestRank(Slot s, int chapter, bool hard) {
        int shift = 3 * Mathf.Clamp(chapter, 0, ChapterCount - 1);
        return ((hard ? s.hardRanks : s.ranks) >> shift) & 7;
    }

    // Keeps a rank on the open file if it beats the best (Standard and Hard keep their own). Returns whether it was a new best.
    public static bool Grade(int chapter, int rank, bool hard) {
        if (Active < 0) {
            return false;
        }
        Slot s = Get(Active);
        chapter = Mathf.Clamp(chapter, 0, ChapterCount - 1);
        if (rank <= BestRank(s, chapter, hard)) {
            return false;
        }
        int shift = 3 * chapter;
        int cleared = ~(7 << shift);
        if (hard) {
            s.hardRanks = (s.hardRanks & cleared) | (rank << shift);
        } else {
            s.ranks = (s.ranks & cleared) | (rank << shift);
        }
        Touch(s);
        Save();
        return true;
    }

    // Hard on or off for the open file. Refused (false) unless the file is filed.
    public static bool SetHard(bool hard) {
        if (Active < 0) {
            return false;
        }
        Slot s = Get(Active);
        if (hard && !CampaignGrade.Filed(s)) {
            return false;
        }
        s.hard = hard;
        Touch(s);
        Save();
        return true;
    }

    // Best ranks of a file as "1 A  2 B  3 -", Standard then Hard when any Hard is kept.
    public static string RankLine(Slot s) {
        var line = new System.Text.StringBuilder();
        for (int i = 0; i < ChapterCount; i++) {
            line.Append(i > 0 ? "  " : "").Append(i + 1).Append(' ').Append(CampaignGrade.Letter(BestRank(s, i, false)));
            if (BestRank(s, i, true) > 0) {
                line.Append('/').Append(CampaignGrade.Letter(BestRank(s, i, true))).Append('h');
            }
        }
        return line.ToString();
    }

    // The secret ending's condition: every lost thing of the open loop is in the journal (all six finds) when Chapter 6 is filed.
    public static bool SecretEarned(Slot s) {
        return s != null && s.used && Journal.FindCount(s) == Journal.Count;
    }

    // Keeps the secret ending on the open file. Returns whether it was the first time.
    public static bool MarkSecret() {
        if (Active < 0) {
            return false;
        }
        Slot s = Get(Active);
        bool first = !s.secret;
        s.secret = true;
        Touch(s);
        Save();
        return first;
    }

    // New Game+: a filed file starts its chapters again from Chapter 1. Wallit's account carries over (xp, so level and
    // abilities, and Refund in Full); the loop's own business starts clean (finds, extras, errands, back rooms, Hard) so the
    // journal can be filled again. Ranks, lore, time on the books and the secret ending seen stay. Refused (false) unless filed.
    public static bool NewGamePlus(int index) {
        Ensure();
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        Slot s = data.slots[index];
        if (!s.used || s.cleared < ChapterCount) {
            return false;
        }
        s.plus = Mathf.Min(99, s.plus + 1);
        s.cleared = 0;
        s.chapter = 0;
        s.reached = 0;
        s.finds = 0;
        s.goals = 0;
        s.errands = 0;
        s.rooms &= 1 << ChapterCount;   // only the Dead Letter's reward is kept
        s.hard = false;
        Touch(s);
        Save();
        return true;
    }

    // Adds time on the books to the open file and saves it.
    public static void Record(float seconds) {
        if (Active < 0) {
            return;
        }
        Slot s = Get(Active);
        s.seconds += Mathf.Max(0f, seconds);
        Touch(s);
        Save();
    }

    public static void Erase(int index) {
        Ensure();
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        data.slots[index] = new Slot();
        if (data.last == index) {
            data.last = -1;
        }
        if (Active == index) {
            Active = -1;
        }
        Save();
    }

    public static void Close() {
        Active = -1;
    }

    static void Touch(Slot s) {
        s.saved = DateTime.UtcNow.Ticks;
    }

    // Writes the files now (a level-up, a chapter done).
    public static void Flush() {
        Save();
    }

    static void Save() {
        if (MetaProgress.Sandbox || LoadResult == "newer") {
            return;   // a file from a newer build is never written over by this one
        }
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // "Chapter 1: Return to Sender" for a file, or "No file yet".
    public static string Describe(Slot s) {
        if (!s.used) {
            return "No file yet";
        }
        string title = Story.Chapters[Mathf.Clamp(s.chapter, 0, ChapterCount - 1)].Title;
        title = s.cleared >= ChapterCount ? title + " (filed)" : title;
        title = s.plus > 0 ? title + "  NG+" + s.plus : title;
        return s.xp > 0 ? title + "  LV " + CampaignProgress.LevelFor(s.xp) : title;
    }

    public static string Played(Slot s) {
        if (!s.used) {
            return "";
        }
        return Clock(s) + " on the books";
    }

    public static string Clock(Slot s) {
        int total = Mathf.FloorToInt(s.seconds);
        return (total / 3600) + ":" + ((total / 60) % 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    // "2/6 filed (33%)": completion of a file.
    public static string Completion(Slot s) {
        int done = Mathf.Clamp(s.cleared, 0, ChapterCount);
        return done + "/" + ChapterCount + " filed (" + Mathf.RoundToInt(100f * done / ChapterCount) + "%)";
    }
}
