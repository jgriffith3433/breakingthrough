using System.Collections.Generic;
using UnityEngine;

// Campaign-only progression: XP from fights, levels, and four abilities Wallit earns on the way (Docs/Story.md, "The
// hub"). It lives in the open case file (CampaignSave.Slot.xp), never in MetaProgress, and does nothing outside a
// chapter: Arcade has no ChapterRun, so every number here reads as "no effect" there.
public static class CampaignProgress {
    public const int MaxLevel = 6;
    public const KeyCode RefundKey = KeyCode.C;
    public const KeyCode TossKey = KeyCode.V;
    public const KeyCode StampKey = KeyCode.B;
    public const KeyCode ShieldKey = KeyCode.G;
    // Ability slots: a key each, filled from the progression. Slot 0 is the dodge from the start, then Refund (level
    // 3), Coin Toss (4), Stamp (5) and Escrow (6). SlotFill holds the ability id (a row in Abilities with a case in Use).
    public const int SlotCount = 5;
    public const int SlotDodge = -2;
    public const int SlotEmpty = -1;
    public static readonly KeyCode[] SlotKeys = { CampaignMoves.DodgeKey, RefundKey, TossKey, StampKey, ShieldKey };
    public static readonly int[] SlotFill = { SlotDodge, 1, AbToss, AbStamp, AbShield };
    public const int AbToss = 4;
    public const int AbStamp = 5;
    public const int AbShield = 6;
    public const float RefundCooldown = 10f;
    public const float TossCooldown = 4f;
    public const float TossReach = 32f;
    public const float StampCooldown = 14f;
    public const float StampRange = 7f;
    public const float StampStun = 3f;
    public const float ShieldCooldown = 20f;
    public const float ShieldSeconds = 5f;
    public const float RefundRange = 8f;
    // Refund in Full: the Dead Letter's reward (BackRooms.DeadLetterBit in the case file's rooms). Not an Abilities row (those are packed
    // by index and unlock by level): it changes Refund when the file holds it.
    public const float FullRange = 12f;
    public const float FullCooldown = 6f;
    public const int FullHeal = 8;
    public static bool RefundFull {
        get { return Active && BackRooms.HasReward(CampaignSave.Get(CampaignSave.Active)); }
    }
    public static float RefundReach { get { return RefundFull ? FullRange : RefundRange; } }
    // An ability's blurb for the screen: "Press C: ..." says "Tap its button: ..." on a phone.
    public static string BlurbOf(Ability a) {
        int colon = a.Blurb.IndexOf(':');
        return UiFit.Touch && a.Blurb.StartsWith("Press ") && colon > 0 ? "Tap its button:" + a.Blurb.Substring(colon + 1) : a.Blurb;
    }

    public static string RefundName { get { return RefundFull ? "Refund in Full" : "Refund"; } }

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
        new Ability { Level = 4, Name = "Coin Toss", Blurb = "Press V: a coin thrown at the nearest bot, a hard sting from range. 4 s." },
        new Ability { Level = 5, Name = "Stamp Out", Blurb = "Press B: DENIED! A stamp on the ground stuns every bot around him. 14 s." },
        new Ability { Level = 6, Name = "Escrow", Blurb = "Press G: nothing touches Wallit for 5 s. 20 s." },
    };

    // What the playtest reads: one line per XP gain, level-up and Refund, cleared when a chapter starts.
    public static readonly List<string> Log = new List<string>();
    public static int Refunds { get; private set; }
    public static int RefundHits { get; private set; }
    public static int Receipts { get; private set; }
    public static float RefundReadyAt { get; private set; }
    public static int Tosses { get; private set; }
    public static int TossHits { get; private set; }
    public static int Stamps { get; private set; }
    public static int StampedBots { get; private set; }
    public static int Shields { get; private set; }
    static readonly float[] readyAt = new float[7];
    static float shieldUntil;

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

    // Unlocked by level, or already on the case file's books (Slot.unlocked, written at the level-up).
    public static bool Has(int ability) {
        if (!Active) {
            return false;
        }
        return Level >= Abilities[ability].Level || (CampaignSave.Get(CampaignSave.Active).unlocked & (1 << ability)) != 0;
    }

    // Ability ids stored on the file, for the playtest's "the unlock persists" check.
    public static int UnlockedMask(int slot) {
        return CampaignSave.Get(slot).unlocked;
    }

    public static bool ShieldUp {
        get { return Active && Time.time < shieldUntil; }
    }

    // Seconds left before ability i can go again (0 = ready).
    public static float Wait(int ability) {
        if (ability == 1) {
            return Mathf.Max(0f, RefundReadyAt - Time.time);
        }
        return Mathf.Max(0f, readyAt[ability] - Time.time);
    }

    // 0..1 through the current level (1 at the top level), for the results sheet's XP bar.
    public static float LevelFraction(int xp) {
        int level = LevelFor(xp);
        if (level >= MaxLevel) {
            return 1f;
        }
        return Mathf.Clamp01((xp - XpAt(level)) / (float)(XpAt(level + 1) - XpAt(level)));
    }

    // The next ability a level opens, as "LV 4 Receipt" (empty at the top level).
    public static string NextUnlock(int level) {
        int best = -1;
        for (int i = 0; i < Abilities.Length; i++) {
            if (Abilities[i].Level > level && (best < 0 || Abilities[i].Level < Abilities[best].Level)) {
                best = i;
            }
        }
        return best < 0 ? "" : "LV " + Abilities[best].Level + " " + Abilities[best].Name;
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
        Tosses = 0;
        TossHits = 0;
        Stamps = 0;
        StampedBots = 0;
        Shields = 0;
        shieldUntil = 0f;
        for (int i = 0; i < readyAt.Length; i++) {
            readyAt[i] = 0f;
        }
        killsForReceipt = 0;
        CampaignMoves.Begin();
    }

    // "X: Dodge", "C: Refund ready", "V: empty": what a slot shows on the HUD line.
    public static string SlotLabel(int slot) {
        string key = UiFit.Touch ? "" : SlotKeys[slot].ToString() + ": ";
        int fill = SlotFill[slot];
        if (fill == SlotDodge) {
            return key + "Dodge" + (CampaignMoves.DodgeWait > 0f ? " " + Mathf.CeilToInt(CampaignMoves.DodgeWait) + " s" : "");
        }
        if (fill >= 0 && Has(fill)) {
            float wait = Wait(fill);
            string name = fill == 1 ? RefundName : Abilities[fill].Name;
            if (fill == AbShield && ShieldUp) {
                return key + name + " on " + Mathf.CeilToInt(shieldUntil - Time.time) + " s";
            }
            return key + name + (wait > 0f ? " " + Mathf.CeilToInt(wait) + " s" : " ready");
        }
        return fill >= 0 ? key + "LV " + Abilities[fill].Level : key + "empty";
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
        if (fill == AbToss) {
            return Toss(player);
        }
        if (fill == AbStamp) {
            return Stamp(player.position);
        }
        if (fill == AbShield) {
            return Escrow(player.position);
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
                        slot.unlocked |= 1 << i;
                        Log.Add("levelup|" + level + "|" + Abilities[i].Name);
                        Announce(level, Abilities[i]);
                    }
                }
            }
        }
        if (after > before) {
            CampaignSave.Flush();
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

    // XP paid outside a fight (Chime's errand in the hub): the same levels and unlock bits as a kill, no on-screen banner.
    public static void Grant(int amount) {
        if (CampaignSave.Active < 0) {
            return;
        }
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        int before = LevelFor(slot.xp);
        slot.xp += amount;
        int after = LevelFor(slot.xp);
        Log.Add("xp|+" + amount + "|" + slot.xp + "|level " + after);
        for (int level = before + 1; level <= after; level++) {
            for (int i = 0; i < Abilities.Length; i++) {
                if (Abilities[i].Level == level) {
                    slot.unlocked |= 1 << i;
                    Log.Add("levelup|" + level + "|" + Abilities[i].Name);
                }
            }
        }
        CampaignSave.Flush();
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
        RefundReadyAt = Time.time + (RefundFull ? FullCooldown : RefundCooldown);
        Refunds++;
        var roster = new List<Enemy>(Enemy.Roster);
        int hit = 0;
        foreach (Enemy foe in roster) {
            if (foe == null || !foe.Alive || foe.Climbing) {
                continue;
            }
            Vector3 flat = foe.transform.position - at;
            flat.y = 0f;
            if (flat.magnitude > RefundReach) {
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
        Fx.Ring(at + Vector3.up * 0.2f, RefundReach * 2f, new Color(1f, 0.82f, 0.38f, 0.8f), 0.5f);
        GameAudio.Play("batsmash", 0.6f, 1.2f);
        if (RefundFull && Musician.Instance != null && Musician.Instance.Alive) {
            Musician.Instance.Heal(FullHeal);   // in full: Slice is paid back too
        }
        Log.Add("refund|" + hit + " bots" + (RefundFull ? "|in full" : ""));
        UpgradeLook.Say(RefundFull ? "REFUND IN FULL!" : "REFUND!", 1.2f, 200f);
        return true;
    }

    // Coin Toss: a coin flies at the nearest bot (the ones in front first) and stings it from range.
    public static bool Toss(Transform player) {
        if (!Has(AbToss) || Wait(AbToss) > 0f) {
            return false;
        }
        Enemy target = null;
        float best = float.MaxValue;
        foreach (Enemy foe in Enemy.Roster) {
            if (foe == null || !foe.Alive || foe.Climbing) {
                continue;
            }
            Vector3 flat = foe.transform.position - player.position;
            flat.y = 0f;
            float d = flat.magnitude;
            if (d > TossReach) {
                continue;
            }
            float ahead = d > 0.1f ? Vector3.Dot(flat / d, player.forward) : 1f;
            float score = d * (ahead > 0.3f ? 1f : 2.5f);
            if (score < best) {
                best = score;
                target = foe;
            }
        }
        if (target == null) {
            return false;
        }
        readyAt[AbToss] = Time.time + TossCooldown;
        Tosses++;
        CoinShot.Throw(player.position + Vector3.up * 1.2f, target);
        GameAudio.Play("batswing", 0.5f, 1.5f);
        Log.Add("toss|" + BotRoster.Slug(target.Kind));
        return true;
    }

    // The coin landed.
    public static void TossLanded(Enemy foe) {
        if (foe == null || !foe.Alive) {
            return;
        }
        TossHits++;
        foe.Damage(Combat.BatBase() * 1.8f, DamageKind.Pulse, false);
        if (foe != null && foe.Alive && !foe.Boss) {
            foe.Knock((foe.transform.position - (PlayerGrowth.Instance != null ? PlayerGrowth.Instance.transform.position : foe.transform.position)).normalized * 6f, 0.3f);
        }
        GameAudio.Play("batcrack", 0.5f, 1.4f);
        Log.Add("toss-hit|" + TossHits);
    }

    // Stamp Out: the ground around Wallit is stamped DENIED. Bots in range are stunned (bosses for less) and stung.
    public static bool Stamp(Vector3 at) {
        if (!Has(AbStamp) || Wait(AbStamp) > 0f) {
            return false;
        }
        readyAt[AbStamp] = Time.time + StampCooldown;
        Stamps++;
        var roster = new List<Enemy>(Enemy.Roster);
        int hit = 0;
        foreach (Enemy foe in roster) {
            if (foe == null || !foe.Alive || foe.Climbing) {
                continue;
            }
            Vector3 flat = foe.transform.position - at;
            flat.y = 0f;
            if (flat.magnitude > StampRange) {
                continue;
            }
            hit++;
            bool dropped = foe.Damage(Combat.BatBase() * 0.4f, DamageKind.Pulse, false);
            if (!dropped && foe != null) {
                foe.Short(StampStun);
            }
        }
        StampedBots += hit;
        Fx.Ring(at + Vector3.up * 0.2f, StampRange * 2f, new Color(0.9f, 0.25f, 0.2f, 0.85f), 0.6f);
        Fx.Ring(at + Vector3.up * 0.2f, StampRange, new Color(1f, 0.82f, 0.38f, 0.7f), 0.4f);
        GameAudio.Play("batsmash", 0.8f, 0.8f);
        Log.Add("stamp|" + hit + " bots");
        UpgradeLook.Say("DENIED!", 1.2f, 200f);
        return true;
    }

    // Escrow: Wallit's account is held; nothing reaches him until the seconds run out.
    public static bool Escrow(Vector3 at) {
        if (!Has(AbShield) || Wait(AbShield) > 0f) {
            return false;
        }
        readyAt[AbShield] = Time.time + ShieldCooldown;
        shieldUntil = Time.time + ShieldSeconds;
        Shields++;
        Fx.Ring(at + Vector3.up * 0.2f, 4f, new Color(0.62f, 0.95f, 0.86f, 0.85f), 0.5f);
        GameAudio.Play("coin", 0.7f, 0.8f);
        Log.Add("escrow|" + ShieldSeconds + " s");
        UpgradeLook.Say("IN ESCROW", 1.2f, 200f);
        return true;
    }

    // "LV 2   XP 34/90" and, on a second line, each slot: "X: Dodge   C: Refund ready   V: Coin Toss 3 s"
    public static string HudLine() {
        int xp = Xp;
        int level = LevelFor(xp);
        string line = "LV " + level + "   XP " + (level >= MaxLevel ? xp + " (max)" : xp + "/" + XpAt(level + 1));
        line += "\n";
        for (int i = 0; i < SlotCount; i++) {
            line += (i > 0 ? "   " : "") + SlotLabel(i);
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

// The Coin Toss coin: a flat gold disc that homes on its bot and lands with the sting. No collider.
public class CoinShot : MonoBehaviour {
    Enemy target;
    Vector3 last;
    float life = 2f;

    public static void Throw(Vector3 from, Enemy target) {
        GameObject go = KitMesh.Disc("Coin Toss");   // a turned coin, never a primitive
        go.transform.position = from;
        go.transform.localScale = new Vector3(0.45f, 0.05f, 0.45f);
        UpgradeLook.Paint(go, new Color(1f, 0.82f, 0.25f));
        CoinShot shot = go.AddComponent<CoinShot>();
        shot.target = target;
        shot.last = target.transform.position + Vector3.up * 1f;
    }

    void Update() {
        life -= Time.deltaTime;
        if (target != null && target.Alive) {
            last = target.transform.position + Vector3.up * 1f;
        }
        Vector3 to = last - transform.position;
        float step = 34f * Time.deltaTime;
        transform.Rotate(0f, 0f, 900f * Time.deltaTime, Space.Self);
        if (to.magnitude <= step || life <= 0f) {
            CampaignProgress.TossLanded(target);
            Destroy(gameObject);
            return;
        }
        transform.position += to.normalized * step;
    }
}
