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
    }

    [Serializable]
    class Data {
        public int version = 1;
        public int last = -1;
        public Slot[] slots = new Slot[SlotCount];
    }

    static Data data;

    // The file the chapter scene is playing; -1 when none is open.
    public static int Active { get; private set; } = -1;

    static void Ensure() {
        if (data != null) {
            return;
        }
        data = null;
        string json = MetaProgress.Sandbox ? "" : PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json)) {
            try {
                data = JsonUtility.FromJson<Data>(json);
            } catch (Exception) {
                data = null;
            }
        }
        if (data == null) {
            data = new Data();
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
    }

    // Playtest setup: forget what is in memory and keep it there (see EnterSandbox).
    public static void EnterSandbox() {
        data = null;
        Active = -1;
        Ensure();
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
        if (MetaProgress.Sandbox) {
            return;
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
        int total = Mathf.FloorToInt(s.seconds);
        return (total / 3600) + ":" + ((total / 60) % 60).ToString("00") + ":" + (total % 60).ToString("00") + " on the books";
    }
}
