using UnityEngine;

// The ids keep their old names (analytics' shop_ranks reads them, "Keen2 Blast1"); saved ranks are packed by number.
public enum UpgradeId {
    Keen = 0, // Corked Core
    Walls = 1,
    Draw = 2, // retired; slot stays so saved ranks do not shift
    Growth = 3, // retired
    Blast = 4, // Aluminum! Not wood!
    Purse = 5, // retired
    Stride = 6,
    Stomp = 7, // retired
    Spikes = 8, // retired
    Towers = 9,
    Clover = 10,
    OpeningAct = 11,
    StageHands = 12
}

public struct UpgradeInfo {
    public UpgradeId Id;
    public string Name;
    public string About;
    public int MaxRank;
}

// Coins and upgrades survive death, scene loads, and closing the game.
public static class MetaProgress {
    public const string MenuScene = "MainMenu";
    public const string GameScene = "scene2";

    public static readonly UpgradeInfo[] Catalog = {
        new UpgradeInfo { Id = UpgradeId.Keen, Name = "Corked Core", About = "A cork core in the barrel. Each rank adds 6 bat damage.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.Blast, Name = "Aluminum! Not wood!", About = "A metal bat: a louder crack, harder knockback, and the third combo hit blasts. Each rank blasts wider.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.Stride, Name = "Long Stride", About = "Each rank is 25% more speed.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.Walls, Name = "Stone Pile", About = "A much thicker wall line.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.Towers, Name = "Watch Kit", About = "More towers. They shoot farther and faster.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.Clover, Name = "Four-Leaf Clover", About = "+1 reroll each run. Rare and epic boons show up more.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.OpeningAct, Name = "Opening Act", About = "Start each run holding a random boon per rank.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.StageHands, Name = "Stage Hands", About = "The musician starts with +25 health per rank.", MaxRank = 3 }
    };

    public static int Coins { get; private set; }
    public static int BestWave { get; private set; }
    public static int ReachedWave { get; private set; }
    public static int HensToward { get; private set; }
    public static int NextCoinAt { get; private set; }
    public static int CoinsThisRun { get; private set; }
    public static string Toast { get; private set; }
    public static float PurseFlash { get; private set; }

    static int[] ranks = new int[13];
    static bool loaded;
    static float toastUntil;
    static int armedStart = 1;

    public static void BeginRun() {
        Ensure();
        CoinsThisRun = 0;
        Toast = null;
    }

    // For analytics: how much of the shop a player owns, as a number and as "Keen2 Clover1".
    public static int TotalRanks() {
        int total = 0;
        foreach (UpgradeInfo info in Catalog) {
            total += Rank(info.Id);
        }
        return total;
    }

    public static string ShopLine() {
        var parts = new System.Collections.Generic.List<string>();
        foreach (UpgradeInfo info in Catalog) {
            int rank = Rank(info.Id);
            if (rank > 0) {
                parts.Add(info.Id + rank.ToString());
            }
        }
        return string.Join(" ", parts.ToArray());
    }

    public static int Rank(UpgradeId id) {
        Ensure();
        int index = (int)id;
        if (index < 0 || index >= ranks.Length) {
            return 0;
        }
        return ranks[index];
    }

    public static UpgradeInfo Info(UpgradeId id) {
        for (int i = 0; i < Catalog.Length; i++) {
            if (Catalog[i].Id == id) {
                return Catalog[i];
            }
        }
        return Catalog[0];
    }

    public static int Cost(UpgradeId id) {
        return 2 + Rank(id) * 3;
    }

    public static bool CanBuy(UpgradeId id) {
        UpgradeInfo info = Info(id);
        int rank = Rank(id);
        return rank < info.MaxRank && Coins >= Cost(id);
    }

    public static bool TryBuy(UpgradeId id) {
        Ensure();
        UpgradeInfo info = Info(id);
        int rank = Rank(id);
        if (rank >= info.MaxRank) {
            return false;
        }
        int cost = Cost(id);
        if (Coins < cost) {
            return false;
        }
        Coins -= cost;
        ranks[(int)id] = rank + 1;
        ClampThreshold();
        Save();
        return true;
    }

    public static void SetBest(int wave) {
        Ensure();
        if (wave > BestWave) {
            BestWave = wave;
            Save();
        }
    }

    public static void NoteReached(int wave) {
        Ensure();
        if (wave > ReachedWave) {
            ReachedWave = wave;
            Save();
        }
    }

    public static int MaxStartWave() {
        Ensure();
        return (ReachedWave / 10) * 10;
    }

    public static bool TryArmStart(int wave) {
        Ensure();
        if (wave <= 1) {
            armedStart = 1;
            return true;
        }
        if (wave % 10 != 0 || wave > MaxStartWave() || Coins < wave) {
            return false;
        }
        Coins -= wave;
        armedStart = wave;
        Save();
        return true;
    }

    public static int TakeStartWave() {
        int wave = Mathf.Max(1, armedStart);
        armedStart = 1;
        return wave;
    }

    // Plated Yo Bots fill the coin tally (saved as HensToward, from when they were hens).
    public static int NoteBot(BotKind kind) {
        if (kind != BotKind.PlatedYoBot) {
            return 0;
        }
        Ensure();
        HensToward++;
        PurseFlash = 1f;
        if (HensToward < NextCoinAt) {
            Save();
            return 0;
        }

        int pay = Payout();
        Coins += pay;
        CoinsThisRun += pay;
        HensToward = 0;
        NextCoinAt = RollThreshold();
        Toast = pay == 1 ? "+1 coin" : "+" + pay + " coins";
        toastUntil = Time.unscaledTime + 2.4f;
        Save();
        GameAudio.Play("coin", 0.55f);
        return pay;
    }

    public static void TickToast() {
        if (Toast != null && Time.unscaledTime >= toastUntil) {
            Toast = null;
        }
        if (PurseFlash > 0f) {
            PurseFlash = Mathf.MoveTowards(PurseFlash, 0f, Time.unscaledDeltaTime * 2.5f);
        }
    }

    public static string StatusLine() {
        Ensure();
        string line = "Coins " + Coins + "    ·    " + HensToward + " / " + NextCoinAt + " hens";
        if (!string.IsNullOrEmpty(Toast)) {
            line += "    ·    " + Toast;
        }
        return line;
    }

    public static string RangeLabel() {
        Ensure();
        int min = ThresholdMin();
        int max = ThresholdMax();
        return min + " to " + max + " hens";
    }

    public static int AutoWave() {
        return Tuning.AutoWave;
    }

    public static int StageHealth() {
        return Rank(UpgradeId.StageHands) * 25;
    }

    // Coins from anything other than hens: cleared waves, bosses, chests, Bounty Hunter.
    public static void AddCoins(int amount, Vector3 at) {
        if (amount <= 0) {
            return;
        }
        Ensure();
        Coins += amount;
        CoinsThisRun += amount;
        PurseFlash = 1f;
        Toast = amount == 1 ? "+1 coin" : "+" + amount + " coins";
        toastUntil = Time.unscaledTime + 2.4f;
        Save();
        if (at != Vector3.zero) {
            UpgradeLook.Coins(at, Mathf.Min(amount, 6));
        }
        GameAudio.Play("coin", 0.55f);
    }

    public static bool TrySpend(int cost) {
        Ensure();
        if (cost <= 0 || Coins < cost) {
            return false;
        }
        Coins -= cost;
        Save();
        return true;
    }

    public static int StartWalls() {
        return 6 + Rank(UpgradeId.Walls) * 5;
    }

    public static int StartSpikes() {
        return 3;
    }

    public static int StartTowers() {
        return 1 + Rank(UpgradeId.Towers) * 2;
    }

    public static int WallStops() {
        return DefencePiece.WallStops + Rank(UpgradeId.Walls) * 8;
    }

    public static int SpikeKills() {
        return DefencePiece.SpikeKills;
    }

    public static int TowerKills() {
        return DefencePiece.TowerKills + Rank(UpgradeId.Towers) * 3;
    }

    public static int TowerStops() {
        return DefencePiece.TowerStops + Rank(UpgradeId.Towers) * 4;
    }

    public static float TowerRange() {
        return 9.5f + Rank(UpgradeId.Towers) * 4f;
    }

    public static float TowerInterval() {
        return Mathf.Max(0.38f, 0.8f - Rank(UpgradeId.Towers) * 0.14f);
    }

    public static int BonusDamage() {
        return Rank(UpgradeId.Keen) * 6;
    }

    public static float GrowthPerWave() {
        return 0.11f;
    }

    public static float DrawHaste() {
        return 1f;
    }

    public static float Stride() {
        return 1f + 0.25f * Rank(UpgradeId.Stride);
    }

    public static int StompWave() {
        return Tuning.StompWave;
    }

    // Aluminum! Not wood!: the bat is metal.
    public static bool MetalBat() {
        return Rank(UpgradeId.Blast) > 0;
    }

    // The metal bat's small blast on the combo finisher, in meters at size 1.
    public static float BlastRadius() {
        int rank = Mathf.Max(1, Rank(UpgradeId.Blast));
        return 2.4f + (rank - 1) * 1.2f;
    }

    public static string RankLine(UpgradeId id) {
        UpgradeInfo info = Info(id);
        int rank = Rank(id);
        if (rank <= 0) {
            return "Not bought";
        }
        switch (id) {
            case UpgradeId.Keen:
                return "+" + (rank * 6) + " bat damage";
            case UpgradeId.Walls:
                return "Start with " + StartWalls() + " walls. Each stops " + WallStops() + ".";
            case UpgradeId.Towers:
                int towers = StartTowers();
                return "Start with " + towers + (towers == 1 ? " tower" : " towers") + ". Range " + TowerRange().ToString("0") + "m.";
            case UpgradeId.Blast:
                return "Metal bat. Finishers blast " + BlastRadius().ToString("0.#") + "m.";
            case UpgradeId.Stride:
                return "+" + (rank * 25) + "% move speed";
            case UpgradeId.Clover:
                return (Tuning.StartRerolls + rank) + " rerolls a run";
            case UpgradeId.OpeningAct:
                return rank == 1 ? "Start with 1 boon" : "Start with " + rank + " boons";
            case UpgradeId.StageHands:
                return "Musician starts at " + (Musician.BaseHealth + StageHealth());
            default:
                return rank + " / " + info.MaxRank;
        }
    }

    static int Payout() {
        return 1;
    }

    static int ThresholdMin() {
        return 10;
    }

    static int ThresholdMax() {
        return 20;
    }

    static int RollThreshold() {
        return Random.Range(ThresholdMin(), ThresholdMax() + 1);
    }

    static void ClampThreshold() {
        int min = ThresholdMin();
        int max = ThresholdMax();
        if (NextCoinAt < min || NextCoinAt > max) {
            NextCoinAt = RollThreshold();
        }
    }

    static void Ensure() {
        if (loaded) {
            return;
        }
        loaded = true;
        Coins = PlayerPrefs.GetInt("bt_coins", 0);
        BestWave = PlayerPrefs.GetInt("bt_best", 0);
        ReachedWave = PlayerPrefs.HasKey("bt_reached") ? PlayerPrefs.GetInt("bt_reached", 0) : BestWave;
        HensToward = PlayerPrefs.GetInt("bt_hens", 0);
        NextCoinAt = PlayerPrefs.GetInt("bt_next", 0);
        string packed = PlayerPrefs.GetString("bt_ranks", "");
        string[] parts = string.IsNullOrEmpty(packed) ? new string[0] : packed.Split(',');
        for (int i = 0; i < ranks.Length; i++) {
            int value = 0;
            if (i < parts.Length) {
                int.TryParse(parts[i], out value);
            }
            int max = 0;
            for (int c = 0; c < Catalog.Length; c++) {
                if ((int)Catalog[c].Id == i) {
                    max = Catalog[c].MaxRank;
                }
            }
            ranks[i] = Mathf.Clamp(value, 0, max);
        }
        if (NextCoinAt <= 0) {
            NextCoinAt = RollThreshold();
            Save();
        } else {
            ClampThreshold();
        }
    }

    // The autopilot plays without touching the real save.
    public static bool Sandbox { get; private set; }

    // Playtest setup only: a shop rank in the sandbox, never saved.
    public static void SandboxRank(UpgradeId id, int rank) {
        if (!Sandbox) {
            return;
        }
        ranks[(int)id] = Mathf.Clamp(rank, 0, Info(id).MaxRank);
    }

    public static void EnterSandbox(bool freshRanks) {
        Ensure();
        Sandbox = true;
        if (freshRanks) {
            for (int i = 0; i < ranks.Length; i++) {
                ranks[i] = 0;
            }
        }
    }

    static void Save() {
        if (Sandbox) {
            return;
        }
        PlayerPrefs.SetInt("bt_coins", Coins);
        PlayerPrefs.SetInt("bt_best", BestWave);
        PlayerPrefs.SetInt("bt_reached", ReachedWave);
        PlayerPrefs.SetInt("bt_hens", HensToward);
        PlayerPrefs.SetInt("bt_next", NextCoinAt);
        var packed = new string[ranks.Length];
        for (int i = 0; i < ranks.Length; i++) {
            packed[i] = ranks[i].ToString();
        }
        PlayerPrefs.SetString("bt_ranks", string.Join(",", packed));
        PlayerPrefs.Save();
    }
}
