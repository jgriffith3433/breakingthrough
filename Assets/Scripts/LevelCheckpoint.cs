using UnityEngine;

// Where a campaign level is picked up again. A checkpoint reached in Chapter 1's level is written into the open case file
// (CampaignSave.Slot.cp*, save version 11), so a reload, a closed tab or a phone that locked resumes there and not at the
// chapter's start. Celeste's rule: quitting puts you back at the last place you reached. Reaching Chapter Complete clears it
// (CampaignSave.Complete). Only the ids below exist; their order is their progress, never reorder them.
public static class LevelCheckpoint {
    public const string Ring = "ring";          // heard Slice out at the Ring
    public const string Trail = "trail";        // read a notice on the west trail
    public const string Flag = "flag";          // lifted the Mailbox Flag
    public const string Fight = "fight";        // the Clearing Crew is awake at the drop
    public const string Courier = "courier";    // the Courier is awake
    public const string Clear = "clear";        // the drop is cleared, the return slot is open

    static readonly string[] Order = { Ring, Trail, Flag, Fight, Courier, Clear };
    static readonly string[] Names = { "The Ring", "The west trail", "The Mailbox Flag", "The drop: the Clearing Crew", "The drop: the Courier", "The return slot" };

    // Bits in Slot.cpFlags: what the file had done at that checkpoint.
    public const int Talked = 1, FlagTaken = 2, NoticeKept = 4;

    public static int Rank(string id) {
        return string.IsNullOrEmpty(id) ? -1 : System.Array.IndexOf(Order, id);
    }

    public static string NameOf(string id) {
        int r = Rank(id);
        return r < 0 ? "" : Names[r];
    }

    // Is there a checkpoint on this file for this chapter (chapter index, 0-based)?
    public static bool Has(CampaignSave.Slot s, int chapter) {
        return s != null && s.used && s.cpChapter == chapter && Rank(s.cpId) >= 0 && s.cleared <= chapter && LevelScene.Replaces(chapter);
    }

    // "Continue: The Mailbox Flag" for the card and the case files.
    public static string Line(CampaignSave.Slot s, int chapter) {
        return Has(s, chapter) ? "Continue: " + NameOf(s.cpId) : "";
    }

    // Where Wallit stands when the checkpoint is loaded (flat spots, the level seats him on the drawn ground).
    public static Vector3 PositionOf(CampaignSave.Slot s) {
        switch (s.cpId) {
            case Ring:
                return LevelYard.SlicePos + new Vector3(0f, 0f, -3.5f);
            case Trail: {
                int top = 0;
                for (int i = 0; i < LevelYard.NoticePos.Length; i++) {
                    if ((s.cpNotices & (1 << i)) != 0) {
                        top = i;
                    }
                }
                return LevelYard.NoticePos[top] + new Vector3(1.5f, 0f, -1.5f);
            }
            case Flag:
                return LevelYard.FlagPos + new Vector3(2f, 0f, -1.5f);
            case Fight:
            case Courier:
                return LevelYard.FightCheckpoint;
            default:
                return LevelYard.DropPos + new Vector3(-6f, 0f, -3f);
        }
    }

    // Writes the checkpoint on the open file. Flags, notices, health and the clock always update; the place only moves forward.
    // Returns whether the place moved (a new checkpoint, not a repeat).
    public static bool Save(int chapter, string id, int flags, int notices, int health, float seconds) {
        if (CampaignSave.Active < 0 || Rank(id) < 0) {
            return false;
        }
        CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
        bool forward = s.cpChapter != chapter || Rank(id) > Rank(s.cpId);
        bool same = s.cpChapter == chapter && id == s.cpId;
        if (forward || same) {
            s.cpChapter = chapter;
            s.cpId = id;
        }
        s.cpFlags = flags;
        s.cpNotices = notices & 7;
        s.cpHealth = Mathf.Clamp(health, 1, LevelScene.MaxHealth);
        s.cpSeconds = Mathf.Max(0f, seconds);
        CampaignSave.Flush();
        return forward;
    }

    public static void Clear(CampaignSave.Slot s) {
        s.cpId = "";
        s.cpChapter = 0;
        s.cpFlags = 0;
        s.cpNotices = 0;
        s.cpHealth = 0;
        s.cpSeconds = 0f;
    }
}
