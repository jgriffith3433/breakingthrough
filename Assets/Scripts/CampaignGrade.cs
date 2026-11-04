using UnityEngine;

// The Department grades every chapter it files, and a filed case file can be re-run on Hard. Campaign only: Arcade has no
// ChapterRun, so every number here reads as "no effect" there.
//
// Rank: two scores of 0..3 added up. Time is the chapter's play seconds against its par (cutscenes and pauses do not
// count); damage is what Slice took (the Hard blows count at their Hard size). 6 is S, 5 is A, 3-4 is B, under 3 is C.
// The best rank per chapter is kept in the case file, one value for Standard and one for Hard (CampaignSave.Slot).
public static class CampaignGrade {
    public const int None = 0, C = 1, B = 2, A = 3, S = 4;

    // Par seconds per chapter, by index (Story.Chapters). A guess from how long each plays with the bat in hand.
    static readonly float[] Par = { 150f, 300f, 360f, 330f, 360f, 300f };

    // Damage thresholds, in Slice's health: none is 3 points, up to Clean 2, up to Scuffed 1, more 0.
    public const int CleanDamage = 12, ScuffedDamage = 35;

    // Hard: bots carry this much more health and every blow on Slice lands this much harder.
    public const float HardHealth = 1.75f;
    public const int HardDamage = 2;

    public static float ParOf(int chapter) {
        return Par[Mathf.Clamp(chapter, 0, Par.Length - 1)];
    }

    public static int TimePoints(int chapter, float seconds) {
        float par = ParOf(chapter);
        return seconds <= par ? 3 : seconds <= par * 1.4f ? 2 : seconds <= par * 2f ? 1 : 0;
    }

    public static int DamagePoints(int damage) {
        return damage <= 0 ? 3 : damage <= CleanDamage ? 2 : damage <= ScuffedDamage ? 1 : 0;
    }

    public static int Rank(int chapter, float seconds, int damage) {
        int points = TimePoints(chapter, seconds) + DamagePoints(damage);
        return points >= 6 ? S : points == 5 ? A : points >= 3 ? B : C;
    }

    public static string Letter(int rank) {
        return rank == S ? "S" : rank == A ? "A" : rank == B ? "B" : rank == C ? "C" : "-";
    }

    // "RANK A   2:14 (par 2:30)   Slice took 9" for the Chapter Complete card.
    public static string Line(int chapter, int rank, float seconds, int damage, bool hard) {
        return "RANK " + Letter(rank) + (hard ? " (HARD)" : "") + "   " + Clock(seconds) + " (par " + Clock(ParOf(chapter)) + ")   Slice took " + damage;
    }

    static string Clock(float seconds) {
        int total = Mathf.RoundToInt(seconds);
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    // Playtest: the last blow on Slice, as the bot meant it and as it landed.
    public static int LastRaw { get; private set; }
    public static int LastLanded { get; private set; }
    public static int Blows { get; private set; }

    // Hard is on while a chapter run is playing a file that is filed and set to Hard.
    public static bool Hard {
        get { return ChapterRun.Instance != null && ChapterRun.Instance.Hard; }
    }

    // A blow on Slice: doubled on Hard. Called from Musician.Hurt, which every bot and boss attack goes through.
    public static int Blow(int amount) {
        LastRaw = amount;
        amount = Hard ? amount * HardDamage : amount;
        LastLanded = amount;
        Blows++;
        return amount;
    }

    // A bot's health (hits to drop) on Hard.
    public static int Health(int hits) {
        return Hard ? Mathf.Max(1, Mathf.RoundToInt(hits * HardHealth)) : hits;
    }

    // Is the file open now filed (every chapter cleared), so that Hard may be chosen?
    public static bool Filed(CampaignSave.Slot s) {
        return s != null && s.used && s.cleared >= CampaignSave.ChapterCount;
    }

    public static string DifficultyLabel(bool hard) {
        return hard ? "Difficulty: HARD" : "Difficulty: Standard";
    }

    public static string DifficultyNote(bool hard) {
        return hard ? "Hard: bots carry " + Mathf.RoundToInt((HardHealth - 1f) * 100f) + "% more health and every blow on Slice lands x" + HardDamage + "." : "Standard.";
    }
}
