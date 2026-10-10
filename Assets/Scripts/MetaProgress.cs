using UnityEngine;

public enum UpgradeId {
    Keen = 0,
    Walls = 1,
    Draw = 2,
    Growth = 3,
    Blast = 4,
    Purse = 5,
    Stride = 6,
    Stomp = 7,
    Spikes = 8,
    Towers = 9
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
        new UpgradeInfo { Id = UpgradeId.Keen, Name = "Keen Arrows", About = "Arrows hit harder.", MaxRank = 6 },
        new UpgradeInfo { Id = UpgradeId.Walls, Name = "Stone Pile", About = "More walls to start, and each wall holds longer.", MaxRank = 5 },
        new UpgradeInfo { Id = UpgradeId.Draw, Name = "Quick Draw", About = "Auto-shoot starts earlier than wave 30, and fires faster.", MaxRank = 4 },
        new UpgradeInfo { Id = UpgradeId.Growth, Name = "Big Appetite", About = "You grow faster from wave to wave.", MaxRank = 5 },
        new UpgradeInfo { Id = UpgradeId.Blast, Name = "Heavy Arrows", About = "Explosions start before wave 70, and splash wider.", MaxRank = 4 },
        new UpgradeInfo { Id = UpgradeId.Purse, Name = "Fat Purse", About = "Coins come from fewer hens.", MaxRank = 4 },
        new UpgradeInfo { Id = UpgradeId.Stride, Name = "Long Stride", About = "You cover the hill faster.", MaxRank = 5 },
        new UpgradeInfo { Id = UpgradeId.Stomp, Name = "Iron Boots", About = "Foot stomps start before wave 40.", MaxRank = 3 },
        new UpgradeInfo { Id = UpgradeId.Spikes, Name = "Spike Kit", About = "More traps to start, and each trap kills more.", MaxRank = 5 },
        new UpgradeInfo { Id = UpgradeId.Towers, Name = "Watch Kit", About = "More towers to start, and each tower lasts longer.", MaxRank = 4 }
    };

    public static int Coins { get; private set; }
    public static int BestWave { get; private set; }
    public static int HensToward { get; private set; }
    public static int NextCoinAt { get; private set; }
    public static int CoinsThisRun { get; private set; }
    public static string Toast { get; private set; }

    static int[] ranks = new int[10];
    static bool loaded;
    static float toastUntil;

    public static void BeginRun() {
        Ensure();
        CoinsThisRun = 0;
        Toast = null;
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
        return Rank(id) + 1;
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
        int cost = rank + 1;
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

    public static void NoteBird(FowlKind kind) {
        if (kind != FowlKind.Hen) {
            return;
        }
        Ensure();
        HensToward++;
        if (HensToward < NextCoinAt) {
            Save();
            return;
        }

        int pay = Payout();
        Coins += pay;
        CoinsThisRun += pay;
        HensToward = 0;
        NextCoinAt = RollThreshold();
        Toast = pay == 1 ? "+1 coin" : "+" + pay + " coins";
        toastUntil = Time.unscaledTime + 2.4f;
        Save();
        GameAudio.Play("launch", 0.55f);
    }

    public static void TickToast() {
        if (Toast != null && Time.unscaledTime >= toastUntil) {
            Toast = null;
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
        return Mathf.Max(1, 30 - Rank(UpgradeId.Draw) * 3);
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

    public static int WallStops() {
        return DefencePiece.WallStops + Rank(UpgradeId.Walls) * 2;
    }

    public static int SpikeKills() {
        return DefencePiece.SpikeKills + Rank(UpgradeId.Spikes);
    }

    public static int TowerKills() {
        return DefencePiece.TowerKills + Rank(UpgradeId.Towers);
    }

    public static int TowerStops() {
        return DefencePiece.TowerStops + Rank(UpgradeId.Towers) * 2;
    }

    public static int BonusDamage() {
        return Rank(UpgradeId.Keen);
    }

    public static float GrowthPerWave() {
        return 0.11f + 0.02f * Rank(UpgradeId.Growth);
    }

    public static float DrawHaste() {
        return Mathf.Max(0.45f, 1f - 0.12f * Rank(UpgradeId.Draw));
    }

    public static float Stride() {
        return 1f + 0.08f * Rank(UpgradeId.Stride);
    }

    public static int StompWave() {
        return Mathf.Max(28, 40 - Rank(UpgradeId.Stomp) * 4);
    }

    public static int BoomWave() {
        return Mathf.Max(45, 70 - Rank(UpgradeId.Blast) * 6);
    }

    public static bool ArrowsExplode() {
        return WaveDirector.Wave >= BoomWave();
    }

    public static float StompScale() {
        return Mathf.Max(1.15f, PlayerGrowth.StompScale - 0.15f * Rank(UpgradeId.Stomp));
    }

    public static float BoomScale() {
        return Mathf.Max(1.2f, PlayerGrowth.BoomScale - 0.25f * Rank(UpgradeId.Blast));
    }

    public static float SplashRadius(float power) {
        float bonus = 1f + 0.22f * Rank(UpgradeId.Blast);
        return (0.8f + Mathf.Max(1f, power) * 0.45f) * bonus;
    }

    public static string RankLine(UpgradeId id) {
        UpgradeInfo info = Info(id);
        int rank = Rank(id);
        if (rank <= 0) {
            return "Not bought";
        }
        switch (id) {
            case UpgradeId.Keen:
                return "+" + rank + " arrow damage";
            case UpgradeId.Walls:
                return "Start with " + (6 + rank * 2) + " walls. Each stops " + WallStops() + ".";
            case UpgradeId.Spikes:
                return "Start with " + (3 + rank) + " spikes. Each kills " + SpikeKills() + ".";
            case UpgradeId.Towers:
                int towers = 1 + rank;
                return "Start with " + towers + (towers == 1 ? " tower" : " towers") + ". Each kills " + TowerKills() + " and holds " + TowerStops() + ".";
            case UpgradeId.Draw:
                return "Auto-shoot from wave " + AutoWave();
            case UpgradeId.Growth:
                return "Growth +" + Mathf.RoundToInt(rank * 0.02f / 0.11f * 100f) + "%";
            case UpgradeId.Blast:
                return "Explosions from wave " + BoomWave();
            case UpgradeId.Purse:
                return (rank >= 3 ? "2 coins every " : "A coin every ") + RangeLabel();
            case UpgradeId.Stride:
                return "+" + (rank * 8) + "% move speed";
            case UpgradeId.Stomp:
                return "Stomp from wave " + StompWave();
            default:
                return rank + " / " + info.MaxRank;
        }
    }

    static int Payout() {
        return Rank(UpgradeId.Purse) >= 3 ? 2 : 1;
    }

    static int ThresholdMin() {
        return Mathf.Max(4, 10 - Rank(UpgradeId.Purse) * 2);
    }

    static int ThresholdMax() {
        return Mathf.Max(ThresholdMin(), 20 - Rank(UpgradeId.Purse) * 3);
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

    static void Save() {
        PlayerPrefs.SetInt("bt_coins", Coins);
        PlayerPrefs.SetInt("bt_best", BestWave);
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
