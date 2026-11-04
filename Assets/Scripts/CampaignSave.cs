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
    }

    // Save format. 1: the first files (no stamp on the oldest; no xp, unlocks, errands or lore bits). 2: stamped, every
    // field checked on load, the lore log derived from the chapters filed. 3: the journal's finds and goals
    // bits (nothing to derive: older files start with none). Bump it with a step in Migrate.
    public const int Version = 3;
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
            s.cleared = Mathf.Clamp(s.cleared, 0, ChapterCount);
            s.chapter = Mathf.Clamp(s.chapter, 0, ChapterCount - 1);
            s.reached = Mathf.Clamp(Mathf.Max(s.reached, s.chapter), 0, ChapterCount - 1);
            s.xp = Mathf.Max(0, s.xp);
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
