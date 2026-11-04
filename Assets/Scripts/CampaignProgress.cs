using System.Collections.Generic;
using UnityEngine;

// Campaign-only progression: XP from fights, levels, and four abilities Wallit earns on the way (Docs/Story.md, "The
// hub"). It lives in the open case file (CampaignSave.Slot.xp), never in MetaProgress, and does nothing outside a
// chapter: Arcade has no ChapterRun, so every number here reads as "no effect" there.
public static class CampaignProgress {
    public const int MaxLevel = 5;
    public const KeyCode RefundKey = KeyCode.C;
    // Ability slots: a key each, filled from the progression. Slot 0 is the dodge from the start, slot 1 takes Refund
    // at level 3, slot 2 is kept for the next ability (a row in Abilities with a case in Use).
    public const int SlotCount = 3;
    public const int SlotDodge = -2;
    public const int SlotEmpty = -1;
    public static readonly KeyCode[] SlotKeys = { CampaignMoves.DodgeKey, RefundKey, KeyCode.V };
    public static readonly int[] SlotFill = { SlotDodge, 1, SlotEmpty };
    public const float RefundCooldown = 10f;
    public const float RefundRange = 8f;

    public struct Ability {
        public int Level;
        public string Name;
        public string Blurb;
    }

    // Index = ability id. Unlocked by level, in this order.
    public static readonly Ability[] Abilities = {
        new Ability { Level = 2, Name = "Spare Change", Blurb = "+15% bat damage." },
        new Ability { Level = 3, Name = "Refund", Blurb = "Press C: a ring shoves bots off Wallit and stings them. 10 s." },
        new Ability { Level = 4, Name = "Receipt", Blurb = "Every sixth kill, Slice is paid back 6 health." },
        new Ability { Level = 5, Name = "Chargeback", Blurb = "+12% chance to crit with the bat." },
    };

    // What the playtest reads: one line per XP gain, level-up and Refund, cleared when a chapter starts.
    public static readonly List<string> Log = new List<string>();
    public static int Refunds { get; private set; }
    public static int RefundHits { get; private set; }
    public static int Receipts { get; private set; }
    public static float RefundReadyAt { get; private set; }

    static int killsForReceipt;

    // True while a chapter of an open case file is being played.
    public static bool Active {
        get { return ChapterRun.Instance != null && CampaignSave.Active >= 0; }
    }

    public static int Xp {
        get { return CampaignSave.Active >= 0 ? CampaignSave.Get(CampaignSave.Active).xp : 0; }
    }

    public static int Level {
        get { return LevelFor(Xp); }
    }

    // Total XP at which a level starts: 0, 30, 90, 180, 300.
    public static int XpAt(int level) {
        level = Mathf.Clamp(level, 1, MaxLevel);
        return 15 * (level - 1) * level;
    }

    public static int LevelFor(int xp) {
        int level = 1;
        while (level < MaxLevel && xp >= XpAt(level + 1)) {
            level++;
        }
        return level;
    }

    public static bool Has(int ability) {
        return Active && Level >= Abilities[ability].Level;
    }

    public static int UnlockedCount(int xp) {
        int level = LevelFor(xp);
        int n = 0;
        for (int i = 0; i < Abilities.Length; i++) {
            n += level >= Abilities[i].Level ? 1 : 0;
        }
        return n;
    }

    public static float DamageMult {
        get { return Has(0) ? 1.15f : 1f; }
    }

    public static float CritBonus {
        get { return Has(3) ? 0.12f : 0f; }
    }

    public static bool RefundReady {
        get { return Has(1) && Time.time >= RefundReadyAt; }
    }

    public static void BeginChapter() {
        Log.Clear();
        Refunds = 0;
        RefundHits = 0;
        Receipts = 0;
        RefundReadyAt = 0f;
        killsForReceipt = 0;
        CampaignMoves.Begin();
    }

    // "X: Dodge", "C: Refund ready", "V: empty": what a slot shows on the HUD line.
    public static string SlotLabel(int slot) {
        string key = SlotKeys[slot].ToString() + ": ";
        int fill = SlotFill[slot];
        if (fill == SlotDodge) {
            return key + "Dodge" + (CampaignMoves.DodgeWait > 0f ? " " + Mathf.CeilToInt(CampaignMoves.DodgeWait) + " s" : "");
        }
        if (fill == 1 && Has(1)) {
            float wait = RefundReadyAt - Time.time;
            return key + (wait > 0f ? "Refund " + Mathf.CeilToInt(wait) + " s" : "Refund ready");
        }
        return key + "empty";
    }

    // A slot's key was pressed. True when its ability went off.
    public static bool Use(int slot, Transform player) {
        int fill = SlotFill[slot];
        if (fill == SlotDodge) {
            if (Enemy.Climbers > 0 || (PlayerGrowth.Instance != null && PlayerGrowth.Instance.Rooted)) {
                return false;
            }
            Vector3 heading = CampaignDodge.Heading(player);
            if (!CampaignMoves.TryDodge(player.position, heading)) {
                return false;
            }
            CampaignDodge dash = player.GetComponentInParent<CampaignDodge>();
            if (dash == null && ChapterRun.Instance != null) {
                dash = ChapterRun.Instance.GetComponent<CampaignDodge>();
            }
            if (dash != null) {
                dash.Go(heading);
            }
            return true;
        }
        if (fill == 1) {
            return Refund(player.position);
        }
        return false;
    }

    public static int XpFor(Enemy foe) {
        int xp = Mathf.Max(5, Tuning.BoltsFor(foe.Kind, foe.Elite || foe.Boss) * 2);
        return foe.Boss ? xp + 50 : xp;
    }

    // A bot fell during a chapter: XP for the file, and Receipt's count.
    public static void OnKill(Enemy foe) {
        if (!Active) {
            return;
        }
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        int before = LevelFor(slot.xp);
        int gain = XpFor(foe);
        slot.xp += gain;
        int after = LevelFor(slot.xp);
        Log.Add("xp|+" + gain + "|" + slot.xp + "|level " + after);
        if (after > before) {
            CampaignSave.Flush();
            for (int level = before + 1; level <= after; level++) {
                for (int i = 0; i < Abilities.Length; i++) {
                    if (Abilities[i].Level == level) {
                        Log.Add("levelup|" + level + "|" + Abilities[i].Name);
                        Announce(level, Abilities[i]);
                    }
                }
            }
        }
        if (Has(2) && Musician.Instance != null && Musician.Instance.Alive) {
            killsForReceipt++;
            if (killsForReceipt >= 6) {
                killsForReceipt = 0;
                Receipts++;
                Musician.Instance.Heal(6);
                Log.Add("receipt|Slice paid back 6");
            }
        }
    }

    static void Announce(int level, Ability ability) {
        UpgradeLook.Say("LEVEL " + level + ": " + ability.Name.ToUpperInvariant() + "!", 3.2f, 240f);
        GameAudio.Play("coin", 0.9f, 1.3f);
        if (PlayerGrowth.Instance != null) {
            Fx.Ring(PlayerGrowth.Instance.transform.position + Vector3.up * 0.2f, 7f, new Color(1f, 0.82f, 0.38f, 0.9f), 0.7f);
        }
    }

    // Refund: bots within range are stung and shoved off Wallit (bosses are stung only). True when it went off.
    public static bool Refund(Vector3 at) {
        if (!RefundReady) {
            return false;
        }
        RefundReadyAt = Time.time + RefundCooldown;
        Refunds++;
        var roster = new List<Enemy>(Enemy.Roster);
        int hit = 0;
        foreach (Enemy foe in roster) {
            if (foe == null || !foe.Alive || foe.Climbing) {
                continue;
            }
            Vector3 flat = foe.transform.position - at;
            flat.y = 0f;
            if (flat.magnitude > RefundRange) {
                continue;
            }
            hit++;
            Vector3 away = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            bool dropped = foe.Damage(Combat.BatBase() * 0.5f, DamageKind.Pulse, false);
            if (!dropped && foe != null && !foe.Boss) {
                foe.Knock(away * 14f, 0.4f);
            }
        }
        RefundHits += hit;
        Fx.Ring(at + Vector3.up * 0.2f, RefundRange * 2f, new Color(1f, 0.82f, 0.38f, 0.8f), 0.5f);
        GameAudio.Play("batsmash", 0.6f, 1.2f);
        Log.Add("refund|" + hit + " bots");
        UpgradeLook.Say("REFUND!", 1.2f, 200f);
        return true;
    }

    // "LV 2  XP 34/90  C: Refund ready"
    public static string HudLine() {
        int xp = Xp;
        int level = LevelFor(xp);
        string line = "LV " + level + "   XP " + (level >= MaxLevel ? xp + " (max)" : xp + "/" + XpAt(level + 1));
        for (int i = 0; i < SlotCount; i++) {
            line += "   " + SlotLabel(i);
        }
        return line;
    }
}

// Rides on the ChapterRun object: the ability slots on their keys (the dodge, Refund). Waits for pauses, cards and
// scenes like the bat does.
public class CampaignAbilities : MonoBehaviour {
    void Awake() {
        gameObject.AddComponent<CampaignDodge>();
    }

    void Update() {
        if (!CampaignProgress.Active || PauseMenu.IsOpen || LevelUpScreen.IsOpen || StoryScreen.IsOpen || Cutscene.IsOpen || WaveDirector.IsLost || Time.timeScale == 0f) {
            return;
        }
        if (PlayerGrowth.Instance == null) {
            return;
        }
        for (int i = 0; i < CampaignProgress.SlotCount; i++) {
            if (PlayInput.GetKeyDown(CampaignProgress.SlotKeys[i])) {
                CampaignProgress.Use(i, PlayerGrowth.Instance.transform);
            }
        }
    }
}
