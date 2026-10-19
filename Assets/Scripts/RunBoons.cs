using System.Collections.Generic;
using UnityEngine;

public enum BoonId {
    Heavy,
    QuickDraw,
    SplitShot,
    Pierce,
    EagleEye,
    Ricochet,
    Firebrand,
    Frost,
    Chain,
    BirdBomb,
    Orbit,
    Encore,
    Ovation,
    Magnet,
    Fleet,
    Bounty,
    Mark,
    Volley,
    Executioner,
    Homing,
    Turret,
    SecondWind,
    // Evolutions. Offered once both parents are maxed.
    StormQuiver,
    Phoenix,
    GrandFinale,
    Hailstorm
}

public enum Rarity {
    Common,
    Rare,
    Epic,
    Legendary
}

public struct BoonInfo {
    public BoonId Id;
    public string Name;
    public string About;
    public int MaxRank;
    public Rarity Rarity;
    public BoonId NeedA;
    public BoonId NeedB;
    public bool Evolution;
}

// What you pick up during one run. Cleared when a run starts.
public static class RunBoons {
    public static readonly BoonInfo[] All = {
        B(BoonId.Heavy, "Heavy Heads", "+25% bat damage.", 5, Rarity.Common),
        B(BoonId.QuickDraw, "Quick Draw", "+18% swing speed.", 5, Rarity.Common),
        B(BoonId.SplitShot, "Split Shot", "The swing sweeps 20° wider per rank.", 4, Rarity.Rare),
        B(BoonId.Pierce, "Bodkin Points", "The bat reaches 10% farther per rank.", 3, Rarity.Rare),
        B(BoonId.EagleEye, "Eagle Eye", "+10% crit chance. Crits hit for double, more with rank.", 5, Rarity.Rare),
        B(BoonId.Ricochet, "Ricochet", "Birds you knock back bowl over +1 more bird.", 3, Rarity.Rare),
        B(BoonId.Firebrand, "Firebrand", "Hits set birds on fire. Burning birds spread it when they die at rank 3.", 3, Rarity.Rare),
        B(BoonId.Frost, "Frost Tips", "Hits slow birds 25% per rank. Rank 3 freezes them for a moment.", 3, Rarity.Rare),
        B(BoonId.Chain, "Chain Lightning", "15% per rank: a hit arcs lightning to 3 more birds.", 3, Rarity.Epic),
        B(BoonId.BirdBomb, "Powder Keg", "Birds burst when they die, hurting the flock around them.", 3, Rarity.Epic),
        B(BoonId.Orbit, "Whirling Quills", "Blades circle you and cut birds that come close. +1 blade per rank.", 4, Rarity.Epic),
        B(BoonId.Encore, "Encore", "The piano thumps out a shockwave every few seconds.", 3, Rarity.Epic),
        B(BoonId.Ovation, "Standing Ovation", "Musician +15 max health and heals 6 after each wave.", 4, Rarity.Common),
        B(BoonId.Magnet, "Lodestone", "Feathers fly to you from 70% farther.", 3, Rarity.Common),
        B(BoonId.Fleet, "Fleet Foot", "+12% move speed.", 3, Rarity.Common),
        B(BoonId.Bounty, "Bounty Hunter", "Every 60 kills drops a coin. Faster with rank.", 3, Rarity.Rare),
        B(BoonId.Mark, "Hunter's Mark", "Your locked bird takes +35% damage per rank.", 3, Rarity.Common),
        B(BoonId.Volley, "Volley", "Every 8th swing also sends out a shockwave. Sooner with rank.", 3, Rarity.Epic),
        B(BoonId.Executioner, "Executioner", "Birds under 12% health per rank drop on the next hit.", 2, Rarity.Epic),
        B(BoonId.Homing, "Seeker Fletching", "You turn to face birds from farther away.", 2, Rarity.Rare),
        B(BoonId.Turret, "Tower Song", "+1 tower in stock now. Towers shoot 30% faster per rank.", 3, Rarity.Rare),
        B(BoonId.SecondWind, "Second Wind", "Once per wave, if the musician drops below 25, a blast clears the stage and heals 20.", 1, Rarity.Epic),
        E(BoonId.StormQuiver, "Storm Quiver", "Every hit chains lightning. Split Shot + Chain Lightning.", BoonId.SplitShot, BoonId.Chain),
        E(BoonId.Phoenix, "Phoenix Arrows", "Burning birds explode in fire, and fire spreads from every blast. Firebrand + Powder Keg.", BoonId.Firebrand, BoonId.BirdBomb),
        E(BoonId.GrandFinale, "Grand Finale", "Encore twice as often, twice as wide, and it heals the musician. Encore + Standing Ovation.", BoonId.Encore, BoonId.Ovation),
        E(BoonId.Hailstorm, "Hailstorm", "Shockwaves freeze, and every 3rd swing sends one. Frost Tips + Volley.", BoonId.Frost, BoonId.Volley)
    };

    static readonly int[] ranks = new int[All.Length];

    public static int Level { get; private set; }
    public static int Feathers { get; private set; }
    public static int NextAt { get; private set; }
    public static int PendingLevels { get; private set; }
    public static int Rerolls { get; private set; }
    public static int Picks { get; private set; }
    public static int PendingChests { get; private set; }
    public static string LastPick { get; private set; }

    static float featherCarry;

    public static void BeginRun() {
        for (int i = 0; i < ranks.Length; i++) {
            ranks[i] = 0;
        }
        Level = 0;
        Feathers = 0;
        featherCarry = 0f;
        PendingLevels = 0;
        PendingChests = 0;
        Picks = 0;
        LastPick = null;
        Rerolls = Tuning.StartRerolls + MetaProgress.Rank(UpgradeId.Clover);
        NextAt = Need(0);
        int gifts = MetaProgress.Rank(UpgradeId.OpeningAct);
        for (int i = 0; i < gifts; i++) {
            List<BoonId> roll = Roll(1, false);
            if (roll.Count > 0) {
                Take(roll[0]);
            }
        }
    }

    public static int Need(int level) {
        return Mathf.RoundToInt(Tuning.LevelBase + level * Tuning.LevelStep + level * level * Tuning.LevelCurve);
    }

    public static float LevelProgress {
        get { return NextAt <= 0 ? 0f : Mathf.Clamp01(Feathers / (float)NextAt); }
    }

    public static void AddFeathers(float amount) {
        featherCarry += amount * (1f + Combo.FeatherBonus) * WaveDirector.FeatherScale;
        int whole = Mathf.FloorToInt(featherCarry);
        if (whole <= 0) {
            return;
        }
        featherCarry -= whole;
        Feathers += whole;
        while (Feathers >= NextAt) {
            Feathers -= NextAt;
            Level++;
            PendingLevels++;
            NextAt = Need(Level);
        }
    }

    public static void AddChest() {
        PendingChests++;
    }

    public static bool TakePendingLevel() {
        if (PendingLevels <= 0) {
            return false;
        }
        PendingLevels--;
        return true;
    }

    public static bool TakePendingChest() {
        if (PendingChests <= 0) {
            return false;
        }
        PendingChests--;
        return true;
    }

    public static bool SpendReroll() {
        if (Rerolls <= 0) {
            return false;
        }
        Rerolls--;
        return true;
    }

    public static void GiveReroll() {
        Rerolls++;
    }

    public static int Rank(BoonId id) {
        return ranks[(int)id];
    }

    public static bool Has(BoonId id) {
        return ranks[(int)id] > 0;
    }

    public static BoonInfo Info(BoonId id) {
        return All[(int)id];
    }

    public static void Take(BoonId id) {
        BoonInfo info = Info(id);
        int index = (int)id;
        if (ranks[index] >= info.MaxRank) {
            return;
        }
        ranks[index]++;
        Picks++;
        LastPick = info.Name;
        if (id == BoonId.Turret) {
            FortStock.Add(FortKind.Tower);
        }
        if (id == BoonId.Ovation && Musician.Instance != null) {
            Musician.Instance.RaiseMax(15);
        }
        if (id == BoonId.GrandFinale && Musician.Instance != null) {
            Musician.Instance.Heal(40);
        }
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.RefreshStats();
        }
        RunLog.NotePick(info.Name);
    }

    static bool Offerable(BoonInfo info) {
        if (ranks[(int)info.Id] >= info.MaxRank) {
            return false;
        }
        if (info.Evolution) {
            return Maxed(info.NeedA) && Maxed(info.NeedB);
        }
        return true;
    }

    static bool Maxed(BoonId id) {
        return ranks[(int)id] >= Info(id).MaxRank;
    }

    // Three different boons. A chest leans rarer. Ready evolutions nearly always show.
    public static List<BoonId> Roll(int count, bool chest) {
        var picks = new List<BoonId>();
        var pool = new List<BoonInfo>();
        for (int i = 0; i < All.Length; i++) {
            if (Offerable(All[i])) {
                pool.Add(All[i]);
            }
        }
        for (int i = 0; i < pool.Count && picks.Count < count; i++) {
            if (pool[i].Evolution && Random.value < (chest ? 1f : 0.85f)) {
                picks.Add(pool[i].Id);
            }
        }
        int guard = 0;
        while (picks.Count < count && guard++ < 200) {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++) {
                if (!picks.Contains(pool[i].Id)) {
                    total += Weight(pool[i], chest);
                }
            }
            if (total <= 0f) {
                break;
            }
            float roll = Random.value * total;
            for (int i = 0; i < pool.Count; i++) {
                if (picks.Contains(pool[i].Id)) {
                    continue;
                }
                roll -= Weight(pool[i], chest);
                if (roll <= 0f) {
                    picks.Add(pool[i].Id);
                    break;
                }
            }
        }
        return picks;
    }

    static float Weight(BoonInfo info, bool chest) {
        float luck = MetaProgress.Rank(UpgradeId.Clover) * 0.25f;
        float weight;
        switch (info.Rarity) {
            case Rarity.Common: weight = chest ? 20f : 60f; break;
            case Rarity.Rare: weight = (chest ? 40f : 30f) * (1f + luck); break;
            case Rarity.Epic: weight = (chest ? 40f : 10f) * (1f + luck * 2f); break;
            default: weight = 30f; break;
        }
        // Boons you already hold come back more often, so builds form.
        if (ranks[(int)info.Id] > 0) {
            weight *= 1.6f;
        }
        return weight;
    }

    public static string RankText(BoonId id) {
        BoonInfo info = Info(id);
        int rank = Rank(id);
        if (info.Evolution) {
            return "EVOLUTION";
        }
        return rank <= 0 ? "New" : "Rank " + rank + " → " + (rank + 1);
    }

    public static Color RarityColor(Rarity rarity) {
        switch (rarity) {
            case Rarity.Rare: return new Color(0.35f, 0.62f, 1f);
            case Rarity.Epic: return new Color(0.78f, 0.42f, 1f);
            case Rarity.Legendary: return new Color(1f, 0.74f, 0.2f);
            default: return new Color(0.82f, 0.8f, 0.74f);
        }
    }

    public static string Summary() {
        var parts = new List<string>();
        for (int i = 0; i < All.Length; i++) {
            if (ranks[i] > 0) {
                parts.Add(All[i].Name + (All[i].MaxRank > 1 ? " " + ranks[i] : ""));
            }
        }
        return string.Join(", ", parts.ToArray());
    }

    // Stats the rest of the game reads.
    public static float DamageMult {
        get { return 1f + 0.25f * Rank(BoonId.Heavy); }
    }

    public static float DrawSpeed {
        get { return 1f + 0.18f * Rank(BoonId.QuickDraw) + Combo.DrawBonus; }
    }

    public static int ExtraArrows {
        get { return Rank(BoonId.SplitShot); }
    }

    public static int PierceCount {
        get { return Rank(BoonId.Pierce); }
    }

    public static float CritChance {
        get { return 0.05f + 0.1f * Rank(BoonId.EagleEye); }
    }

    public static float CritMult {
        get { return 2f + 0.25f * Rank(BoonId.EagleEye); }
    }

    public static int Bounces {
        get { return Rank(BoonId.Ricochet); }
    }

    public static float PickupRadius {
        get { return Tuning.BasePickup * (1f + 0.7f * Rank(BoonId.Magnet)); }
    }

    public static float MoveMult {
        get { return 1f + 0.12f * Rank(BoonId.Fleet); }
    }

    public static float TowerHaste {
        get { return 1f + 0.3f * Rank(BoonId.Turret); }
    }

    public static int VolleyEvery {
        get {
            if (Has(BoonId.Hailstorm)) {
                return 3;
            }
            int rank = Rank(BoonId.Volley);
            return rank <= 0 ? 0 : 10 - rank * 2;
        }
    }

    public static float ExecuteBelow {
        get { return 0.12f * Rank(BoonId.Executioner); }
    }

    public static float MarkMult {
        get { return 1f + 0.35f * Rank(BoonId.Mark); }
    }

    public static int BountyEvery {
        get {
            int rank = Rank(BoonId.Bounty);
            return rank <= 0 ? 0 : 75 - rank * 15;
        }
    }

    static BoonInfo B(BoonId id, string name, string about, int max, Rarity rarity) {
        return new BoonInfo { Id = id, Name = name, About = about, MaxRank = max, Rarity = rarity };
    }

    static BoonInfo E(BoonId id, string name, string about, BoonId a, BoonId b) {
        return new BoonInfo { Id = id, Name = name, About = about, MaxRank = 1, Rarity = Rarity.Legendary, NeedA = a, NeedB = b, Evolution = true };
    }
}
