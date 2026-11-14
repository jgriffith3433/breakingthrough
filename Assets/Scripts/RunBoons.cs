using System.Collections.Generic;
using UnityEngine;

// The ids keep their bow-era names and order: analytics (choice, card_pick, upgrades) and RunAnalytics read them, and
// All is indexed by them. The comment is what each one is now.
public enum BoonId {
    Heavy, // Corked Bat
    QuickDraw, // Quick Hands
    SplitShot, // Wide Arc
    Pierce, // Follow Through
    EagleEye, // Sweet Spot
    Ricochet, // Line Drive
    Firebrand, // Pine Tar
    Frost, // Ice Bat
    Chain, // Live Wire
    BirdBomb,
    Orbit,
    Encore,
    Ovation,
    Magnet,
    Fleet,
    Bounty,
    Mark, // Called Shot
    Volley, // Grand Slam
    Executioner,
    Homing, // Long Ball
    Turret,
    SecondWind,
    // Evolutions. Offered once both parents are maxed.
    StormQuiver, // Thunder Bat
    Phoenix, // Phoenix Bat
    GrandFinale,
    Hailstorm // Blizzard Bat
}

// What a boon does to the swing. The wave-clear card offers one from each of three different families, so the first pick is
// a real choice (an element, a reach, an impact) and not three stat bumps. None means a plain stat or helper boon.
// Guard is Slice's side (her health, the Encore thump, the towers, the blades round Wallit); Tricks is the rest of the kit
// (what bolts, speed, coins, marks and kegs do). Every boon has a family, so an offer can always be told apart by it.
public enum BoonFamily { None, Element, Reach, Impact, Guard, Tricks }

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
        B(BoonId.Heavy, "Corked Bat", "+25% bat damage.", 5, Rarity.Common),
        B(BoonId.QuickDraw, "Quick Hands", "+18% swing speed.", 5, Rarity.Common),
        B(BoonId.SplitShot, "Wide Arc", "The swing sweeps 20° wider per rank.", 4, Rarity.Rare),
        B(BoonId.Pierce, "Follow Through", "Each swing hits 3 more bots per rank.", 3, Rarity.Rare),
        B(BoonId.EagleEye, "Sweet Spot", "Hits on the bat's tip: +15% crit chance per rank. Crits hit harder with rank.", 5, Rarity.Rare),
        B(BoonId.Ricochet, "Line Drive", "Bots you knock back hit +1 bot behind them per rank.", 3, Rarity.Rare),
        B(BoonId.Firebrand, "Pine Tar", "Hits set bots burning. At rank 3, burning bots pass it on when they fall.", 3, Rarity.Rare),
        B(BoonId.Frost, "Ice Bat", "Hits slow bots 25% per rank. Rank 3 freezes them for a moment.", 3, Rarity.Rare),
        B(BoonId.Chain, "Live Wire", "15% per rank: a hit arcs to 3 more bots, and they short out.", 3, Rarity.Epic),
        B(BoonId.BirdBomb, "Powder Keg", "Bots burst when they fall, hurting the bots around them.", 3, Rarity.Epic),
        B(BoonId.Orbit, "Whirling Blades", "Blades circle you and cut bots that come close. +1 blade per rank.", 4, Rarity.Epic),
        B(BoonId.Encore, "Encore", "The circle thumps out a shockwave around Slice every few seconds.", 3, Rarity.Epic),
        B(BoonId.Ovation, "Standing Ovation", "Slice +15 max health and heals 6 after each wave.", 4, Rarity.Common),
        B(BoonId.Magnet, "Lodestone", "Bolts fly to you from 70% farther.", 3, Rarity.Common),
        B(BoonId.Fleet, "Fleet Foot", "+12% move speed.", 3, Rarity.Common),
        B(BoonId.Bounty, "Bounty Hunter", "Every 60 kills drops a coin. Faster with rank.", 3, Rarity.Rare),
        B(BoonId.Mark, "Called Shot", "Your locked bot takes +35% damage per rank and drops extra bolts.", 3, Rarity.Common),
        B(BoonId.Volley, "Grand Slam", "Every 8th swing is a full spin that hits all around you. Sooner with rank.", 3, Rarity.Epic),
        B(BoonId.Executioner, "Executioner", "Bots under 12% health per rank drop on the next hit.", 2, Rarity.Epic),
        B(BoonId.Homing, "Long Ball", "Each swing also bats a bolt at the nearest bot out of reach.", 2, Rarity.Rare),
        B(BoonId.Turret, "Tower Song", "+1 tower in stock now. Towers shoot 30% faster per rank.", 3, Rarity.Rare),
        B(BoonId.SecondWind, "Second Wind", "Once per wave, if Slice drops below 25, a blast clears the stage and heals 20.", 1, Rarity.Epic),
        E(BoonId.StormQuiver, "Thunder Bat", "Every hit arcs, and every spin calls down lightning. Live Wire + Grand Slam.", BoonId.Chain, BoonId.Volley),
        E(BoonId.Phoenix, "Phoenix Bat", "Burning bots explode in fire, and fire spreads from every blast. Pine Tar + Powder Keg.", BoonId.Firebrand, BoonId.BirdBomb),
        E(BoonId.GrandFinale, "Grand Finale", "Encore twice as often, twice as wide, and it heals Slice. Encore + Standing Ovation.", BoonId.Encore, BoonId.Ovation),
        E(BoonId.Hailstorm, "Blizzard Bat", "Every swing blows frost through its arc, and finishers freeze. Ice Bat + Wide Arc.", BoonId.Frost, BoonId.SplitShot)
    };

    static readonly int[] ranks = new int[All.Length];

    public static int Level { get; private set; }
    public static int Feathers { get; private set; }
    public static int NextAt { get; private set; }
    public static int PendingLevels { get; private set; }
    // Levels gained this run (kills and bolts filled the bar), reported by the playtest as run.levelUps.
    public static int LevelUps { get; private set; }
    // Of the pending cards, how many are wave-clear cards (no level gained), and whether the card just taken was one.
    static int pendingWaveCards;
    public static bool LastCardWasWave { get; private set; }
    public static int Rerolls { get; private set; }
    public static int Picks { get; private set; }
    // The offers made this run, for the playtest's picks.families and picks.deadOffers (see Audit).
    public static int Offers { get; private set; }
    public static int DeadOffers { get; private set; }
    public static int FamilySum { get; private set; }
    public static int MinFamilies { get; private set; }
    public static int RerollsTaken { get; private set; }
    public static int PendingChests { get; private set; }
    public static string LastPick { get; private set; }
    public static BoonFamily FirstFamily { get; private set; }
    public static BoonId FirstBoon { get; private set; }

    static float featherCarry;

    // Evolutions this run, in the order they happened, and the wave each one was taken in (run.evolutions in the playtest).
    public static readonly List<BoonId> Evolved = new List<BoonId>();
    public static readonly List<int> EvolvedWave = new List<int>();
    public static int Evolutions { get { return Evolved.Count; } }

    public static void BeginRun() {
        for (int i = 0; i < ranks.Length; i++) {
            ranks[i] = 0;
        }
        Level = 0;
        LevelUps = 0;
        Feathers = 0;
        featherCarry = 0f;
        PendingLevels = 0;
        pendingWaveCards = 0;
        LastCardWasWave = false;
        PendingChests = 0;
        Picks = 0;
        Offers = 0;
        DeadOffers = 0;
        FamilySum = 0;
        MinFamilies = 99;
        RerollsTaken = 0;
        LastPick = null;
        FirstFamily = BoonFamily.None;
        FirstBoon = BoonId.Heavy;
        BoonFx.Reset();
        Evolved.Clear();
        EvolvedWave.Clear();
        EvolutionCeremony.Reset();
        Rerolls = Tuning.StartRerolls + MetaProgress.Rank(UpgradeId.Clover);
        NextAt = Need(0);
        int gifts = MetaProgress.Rank(UpgradeId.OpeningAct);
        for (int i = 0; i < gifts; i++) {
            List<BoonId> roll = Roll(1, false, null);
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
            LevelUps++;
            PendingLevels++;
            NextAt = Need(Level);
        }
    }

    // A card for clearing an early wave: one pick of three, no level gained.
    public static void AddWaveCard() {
        PendingLevels++;
        pendingWaveCards++;
    }

    public static void AddChest() {
        PendingChests++;
    }

    public static bool TakePendingLevel() {
        if (PendingLevels <= 0) {
            return false;
        }
        // Level cards come first; the wave-clear cards (no level) are what is left over.
        LastCardWasWave = pendingWaveCards > 0 && PendingLevels <= pendingWaveCards;
        if (LastCardWasWave) {
            pendingWaveCards--;
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
        RerollsTaken++;
        return true;
    }

    public static void GiveReroll() {
        Rerolls++;
    }

    public static int[] RanksCopy() {
        return (int[])ranks.Clone();
    }

    public static int PendingWaveCards { get { return pendingWaveCards; } }

    // A saved run comes back: ranks, level, bolts and what is still owed as cards. Side effects that live outside
    // this class (the Turret's tower stock) are given again; Slice's health comes from her own snapshot.
    public static void Restore(int[] saved, int level, int bolts, int rerolls, int picks, int pendingCards, int waveCards) {
        for (int i = 0; i < ranks.Length; i++) {
            ranks[i] = saved != null && i < saved.Length ? Mathf.Clamp(saved[i], 0, All[i].MaxRank) : 0;
        }
        Level = Mathf.Max(0, level);
        NextAt = Need(Level);
        Feathers = Mathf.Clamp(bolts, 0, Mathf.Max(0, NextAt - 1));
        featherCarry = 0f;
        Rerolls = Mathf.Max(0, rerolls);
        Picks = Mathf.Max(0, picks);
        PendingLevels = Mathf.Max(0, pendingCards);
        pendingWaveCards = Mathf.Clamp(waveCards, 0, PendingLevels);
        PendingChests = 0;
        for (int i = 0; i < ranks[(int)BoonId.Turret]; i++) {
            FortStock.Add(FortKind.Tower);
        }
        Evolved.Clear();
        EvolvedWave.Clear();
        for (int i = 0; i < All.Length; i++) {
            if (All[i].Evolution && ranks[i] > 0) {
                Evolved.Add(All[i].Id);
                EvolvedWave.Add(0);
            }
        }
        FirstFamily = BoonFamily.None;
        for (int i = 0; i < All.Length; i++) {
            if (ranks[i] > 0) {
                FirstFamily = FamilyOf(All[i].Id);
                FirstBoon = All[i].Id;
                break;
            }
        }
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.RefreshStats();
        }
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
        if (FirstFamily == BoonFamily.None) {
            FirstFamily = FamilyOf(id);
            FirstBoon = id;
        }
        ranks[index]++;
        Picks++;
        LastPick = info.Name;
        if (id == BoonId.Turret) {
            FortStock.Add(FortKind.Tower);
            if (PlayerGrowth.Instance != null) {
                BoonFx.Proc(BoonId.Turret, PlayerGrowth.Instance.transform.position + Vector3.up * 1.1f, "+1 TOWER", 2.6f);
            }
        }
        if (id == BoonId.Ovation && Musician.Instance != null) {
            Musician.Instance.RaiseMax(15);
            BoonFx.Proc(BoonId.Ovation, Musician.Instance.transform.position + Vector3.up * 1.1f, "+15 MAX HP", 3.6f);
        }
        if (id == BoonId.GrandFinale && Musician.Instance != null) {
            Musician.Instance.Heal(40);
            BoonFx.Proc(BoonId.GrandFinale, Musician.Instance.transform.position + Vector3.up * 1.1f, "+40 HP", 4.2f);
        }
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.RefreshStats();
        }
        RunLog.NotePick(info.Name);
        if (info.Evolution) {
            Evolved.Add(id);
            EvolvedWave.Add(WaveDirector.Wave);
            EvolutionCeremony.Play(id);
        }
        RunSave.Touch();
    }

    // The evolution a boon is a parent of (or the evolution itself), if any. Each parent belongs to one evolution.
    public static bool EvolutionOf(BoonId id, out BoonInfo evolution) {
        for (int i = 0; i < All.Length; i++) {
            if (All[i].Evolution && (All[i].Id == id || All[i].NeedA == id || All[i].NeedB == id)) {
                evolution = All[i];
                return true;
            }
        }
        evolution = default(BoonInfo);
        return false;
    }

    // The boon a card pairs with: a parent's partner, or for an evolution its second parent.
    public static bool PartnerOf(BoonId id, out BoonId partner, out BoonInfo evolution) {
        partner = id;
        if (!EvolutionOf(id, out evolution)) {
            return false;
        }
        partner = evolution.Id == id ? evolution.NeedB : (evolution.NeedA == id ? evolution.NeedB : evolution.NeedA);
        return true;
    }

    // Would taking this boon finish an evolution: it is the evolution itself, or the last rank of a parent whose partner is
    // already maxed (so the evolution is the very next thing offered).
    public static bool Completes(BoonId id) {
        BoonInfo evo;
        if (!EvolutionOf(id, out evo) || ranks[(int)evo.Id] > 0) {
            return false;
        }
        if (evo.Id == id) {
            return Offerable(evo);
        }
        BoonId partner = evo.NeedA == id ? evo.NeedB : evo.NeedA;
        return Maxed(partner) && ranks[(int)id] == Info(id).MaxRank - 1;
    }


    // A boon a pick can use: not at its top rank, and an evolution only once both parents are maxed (it does nothing before).
    static bool Offerable(BoonInfo info) {
        if (ranks[(int)info.Id] >= info.MaxRank) {
            return false;
        }
        if (info.Evolution) {
            return Maxed(info.NeedA) && Maxed(info.NeedB);
        }
        return true;
    }

    // The same test, asked of an offer already made: a maxed boon, a boon twice, or an evolution before its parents.
    public static bool IsDead(BoonId id) {
        return !Offerable(Info(id));
    }

    // Reads an offer into the run's numbers: how many families it spans, and whether it holds a dead boon. Called once per
    // offer the picker deals (a reroll deals another and is counted again). Returns the family count.
    public static int Audit(List<BoonId> offer) {
        var seen = new HashSet<BoonFamily>();
        bool dead = false;
        for (int i = 0; i < offer.Count; i++) {
            seen.Add(FamilyOf(offer[i]));
            if (IsDead(offer[i]) || offer.IndexOf(offer[i]) != i) {
                dead = true;
            }
        }
        Offers++;
        FamilySum += seen.Count;
        MinFamilies = Mathf.Min(MinFamilies, seen.Count);
        if (dead) {
            DeadOffers++;
        }
        return seen.Count;
    }

    public static bool Maxed(BoonId id) {
        return ranks[(int)id] >= Info(id).MaxRank;
    }

    public static BoonFamily FamilyOf(BoonId id) {
        switch (id) {
            case BoonId.Firebrand: case BoonId.Frost: case BoonId.Chain: return BoonFamily.Element;
            case BoonId.SplitShot: case BoonId.Pierce: case BoonId.Volley: return BoonFamily.Reach;
            case BoonId.Ricochet: case BoonId.Heavy: case BoonId.EagleEye: case BoonId.Homing: return BoonFamily.Impact;
            case BoonId.Ovation: case BoonId.Encore: case BoonId.SecondWind: case BoonId.Turret: case BoonId.Orbit: return BoonFamily.Guard;
            case BoonId.QuickDraw: case BoonId.Magnet: case BoonId.Fleet: case BoonId.Bounty: case BoonId.Mark: case BoonId.Executioner: case BoonId.BirdBomb: return BoonFamily.Tricks;
            default:
                BoonInfo info = All[(int)id];
                return info.Evolution ? FamilyOf(info.NeedA) : BoonFamily.None;
        }
    }

    public static string FamilyName(BoonFamily family) {
        switch (family) {
            case BoonFamily.Element: return "ELEMENT";
            case BoonFamily.Reach: return "REACH";
            case BoonFamily.Impact: return "IMPACT";
            case BoonFamily.Guard: return "GUARD";
            case BoonFamily.Tricks: return "TRICKS";
            default: return "";
        }
    }

    // One consistent colour per boon family so the player can read their build from the fight.
    public static Color FamilyColor(BoonFamily family) {
        switch (family) {
            case BoonFamily.Element: return new Color(1f, 0.42f, 0.14f, 1f);
            case BoonFamily.Reach:   return new Color(0.16f, 0.88f, 1f, 1f);
            case BoonFamily.Impact:  return new Color(1f, 0.82f, 0.16f, 1f);
            case BoonFamily.Guard:   return new Color(0.22f, 0.95f, 0.52f, 1f);
            case BoonFamily.Tricks:  return new Color(0.84f, 0.34f, 1f, 1f);
            default:                 return new Color(1f, 0.95f, 0.8f, 1f);
        }
    }

    public static Color ColorOf(BoonId id) {
        return FamilyColor(FamilyOf(id));
    }

    public static BoonFamily LeadFamily {
        get {
            if (FirstFamily != BoonFamily.None) {
                return FirstFamily;
            }
            for (int i = 0; i < All.Length; i++) {
                if (ranks[i] > 0) {
                    return FamilyOf(All[i].Id);
                }
            }
            return BoonFamily.None;
        }
    }

    public static int HeldCount {
        get {
            int n = 0;
            for (int i = 0; i < All.Length; i++) {
                if (ranks[i] > 0) {
                    n++;
                }
            }
            return n;
        }
    }

    // The card after a wave: one boon from each family, never two from the same, each one you can see on the bat in the
    // next wave (a flame or frost or arc on the swing's trail, a wider sweep, a knock-back ring). Falls back to a plain roll.
    public static List<BoonId> RollFamilies(List<BoonId> avoid) {
        var picks = new List<BoonId>();
        BoonFamily[] families = { BoonFamily.Element, BoonFamily.Reach, BoonFamily.Impact };
        for (int f = 0; f < families.Length; f++) {
            float total = 0f;
            for (int i = 0; i < All.Length; i++) {
                if (FamilyOf(All[i].Id) == families[f] && Offerable(All[i]) && !Avoided(All[i].Id, avoid)) {
                    total += Weight(All[i], false);
                }
            }
            if (total <= 0f) {
                continue;
            }
            float roll = Random.value * total;
            for (int i = 0; i < All.Length; i++) {
                if (FamilyOf(All[i].Id) != families[f] || !Offerable(All[i]) || Avoided(All[i].Id, avoid)) {
                    continue;
                }
                roll -= Weight(All[i], false);
                if (roll <= 0f) {
                    picks.Add(All[i].Id);
                    break;
                }
            }
        }
        if (picks.Count < 3) {
            return Roll(3, false, avoid);
        }
        return picks;
    }

    // A reroll deals different boons when there are enough others to deal.
    static bool Avoided(BoonId id, List<BoonId> avoid) {
        return avoid != null && avoid.Contains(id);
    }

    // Up to `count` different boons a pick can use. A chest leans rarer, ready evolutions nearly always show, and a boon that
    // brings an evolution one step closer is weighted up (EvolutionLean). Each next boon leans away from the families already
    // dealt, and a hand that still holds one family only swaps its last boon for another family's when one exists, so a
    // three-card offer spans at least two families. `avoid` is the offer a reroll replaces.
    public static List<BoonId> Roll(int count, bool chest, List<BoonId> avoid) {
        var pool = new List<BoonInfo>();
        for (int i = 0; i < All.Length; i++) {
            if (Offerable(All[i]) && !Avoided(All[i].Id, avoid)) {
                pool.Add(All[i]);
            }
        }
        if (pool.Count < count && avoid != null) {
            // Too few new ones: the old offer may come back before the hand is short.
            return Roll(count, chest, null);
        }
        var picks = new List<BoonId>();
        for (int i = 0; i < pool.Count && picks.Count < count; i++) {
            if (pool[i].Evolution && Random.value < (chest ? 1f : 0.85f)) {
                picks.Add(pool[i].Id);
            }
        }
        // One step from an evolution: the boon that finishes it is nearly always on offer (a chest always shows it).
        if (count >= 2 && picks.Count < count && Random.value < (chest ? 1f : 0.7f)) {
            for (int i = 0; i < pool.Count; i++) {
                if (!pool[i].Evolution && Completes(pool[i].Id) && !picks.Contains(pool[i].Id)) {
                    picks.Add(pool[i].Id);
                    break;
                }
            }
        }
        int guard = 0;
        while (picks.Count < count && guard++ < 200) {
            BoonId next;
            if (!Draw(pool, picks, chest, out next)) {
                break;
            }
            picks.Add(next);
        }
        if (count >= 2 && picks.Count >= 2 && Families(picks) < 2) {
            // Swap the last non-evolution boon for the best one from another family.
            var rest = new List<BoonId>(picks);
            int swap = rest.Count - 1;
            while (swap > 0 && Info(rest[swap]).Evolution) {
                swap--;
            }
            BoonFamily keep = FamilyOf(rest[0]);
            rest.RemoveAt(swap);
            var other = new List<BoonInfo>();
            for (int i = 0; i < pool.Count; i++) {
                if (FamilyOf(pool[i].Id) != keep && !rest.Contains(pool[i].Id)) {
                    other.Add(pool[i]);
                }
            }
            BoonId pick;
            if (Draw(other, rest, chest, out pick)) {
                picks[swap] = pick;
            }
        }
        return picks;
    }

    static int Families(List<BoonId> picks) {
        var seen = new HashSet<BoonFamily>();
        for (int i = 0; i < picks.Count; i++) {
            seen.Add(FamilyOf(picks[i]));
        }
        return seen.Count;
    }

    // One weighted draw from `pool`, not already in `picks`, leaning away from families `picks` holds.
    static bool Draw(List<BoonInfo> pool, List<BoonId> picks, bool chest, out BoonId drawn) {
        drawn = BoonId.Heavy;
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) {
            if (!picks.Contains(pool[i].Id)) {
                total += DrawWeight(pool[i], picks, chest);
            }
        }
        if (total <= 0f) {
            return false;
        }
        float roll = Random.value * total;
        for (int i = 0; i < pool.Count; i++) {
            if (picks.Contains(pool[i].Id)) {
                continue;
            }
            roll -= DrawWeight(pool[i], picks, chest);
            if (roll <= 0f) {
                drawn = pool[i].Id;
                return true;
            }
        }
        for (int i = pool.Count - 1; i >= 0; i--) {
            if (!picks.Contains(pool[i].Id)) {
                drawn = pool[i].Id;
                return true;
            }
        }
        return false;
    }

    static float DrawWeight(BoonInfo info, List<BoonId> picks, bool chest) {
        float weight = Weight(info, chest);
        BoonFamily family = FamilyOf(info.Id);
        for (int i = 0; i < picks.Count; i++) {
            if (FamilyOf(picks[i]) == family) {
                weight *= 0.35f;
            }
        }
        return weight;
    }

    // How much closer taking this boon brings an evolution: x4 when it would max a parent whose partner is already maxed (the
    // evolution is offered next), x2.2 when the partner is maxed and this one is still climbing, x1.5 when both parents are held.
    static float EvolutionLean(BoonInfo info) {
        if (info.Evolution) {
            return 1f;
        }
        float lean = 1f;
        for (int i = 0; i < All.Length; i++) {
            BoonInfo e = All[i];
            if (!e.Evolution || ranks[(int)e.Id] > 0 || (e.NeedA != info.Id && e.NeedB != info.Id)) {
                continue;
            }
            BoonId partner = e.NeedA == info.Id ? e.NeedB : e.NeedA;
            float one;
            if (Maxed(partner)) {
                one = ranks[(int)info.Id] == info.MaxRank - 1 ? 8f : 3.5f;
            } else if (ranks[(int)partner] > 0 && ranks[(int)info.Id] > 0) {
                one = 2.5f;
            } else if (ranks[(int)partner] > 0) {
                one = 1.8f;   // the partner is held: this is the one still missing
            } else {
                one = 1f;
            }
            lean = Mathf.Max(lean, one);
        }
        return lean;
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
        return weight * EvolutionLean(info);
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

    // Wide Arc: degrees added to every swing's arc.
    public static float ArcBonus {
        get { return 20f * Rank(BoonId.SplitShot); }
    }

    // How many bots one combo swing can hit. Follow Through raises it; the smash and the spin hit everything.
    public static int SwingHits(bool finisher) {
        return (finisher ? Tuning.BatHitsFinisher : Tuning.BatHits) + 3 * Rank(BoonId.Pierce);
    }

    // Base crit chance anywhere on the bat; Sweet Spot adds TipCrit on the outer part of the barrel.
    public static float CritChance {
        get { return 0.05f; }
    }

    public static float TipCrit {
        get { return 0.15f * Rank(BoonId.EagleEye); }
    }

    public static float CritMult {
        get { return 2f + 0.25f * Rank(BoonId.EagleEye); }
    }

    // Line Drive: how many bots a knocked-back bot hits on its way.
    public static int LineDrive {
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

    // Grand Slam: every Nth swing is a spin. 0 without it.
    public static int SpinEvery {
        get {
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
