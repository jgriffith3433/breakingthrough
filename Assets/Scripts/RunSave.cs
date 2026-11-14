using System;
using UnityEngine;

// Arcade's run in progress. At each wave clear a small snapshot is written (the wave to start next, its act, the boons and
// their ranks, Slice's health, the bolts toward the next level, the seed); the menu offers "Continue run (wave N)" while one
// exists and the run restarts that wave from its start, never mid-fight. A fall or a finished run clears it; quitting to
// the menu or closing the tab keeps it. Sandboxed runs (playtests) keep it in memory only, like CampaignSave.
public static class RunSave {
    const string Key = "bt_run";
    const int Version = 1;

    [Serializable]
    public class Data {
        public int v;
        public int wave;
        public int act;
        public int[] boons;
        public int health;
        public int maxHealth;
        public int level;
        public int bolts;
        public int rerolls;
        public int picks;
        public int pendingCards;
        public int pendingWaveCards;
        public int coins;
        public int seed;
    }

    static string memory;       // sandbox copy
    static Data cached;
    static bool cacheValid;
    static bool resuming;
    static int runSeed;

    // True while this scene's run has a snapshot it keeps fresh (a boon taken during the break re-saves it).
    public static bool Live { get; private set; }

    static string Read() {
        if (MetaProgress.Sandbox) {
            return memory;
        }
        return PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key, "") : null;
    }

    public static Data Peek() {
        if (cacheValid) {
            return cached;
        }
        Data data = null;
        try {
            string text = Read();
            if (!string.IsNullOrEmpty(text)) {
                data = JsonUtility.FromJson<Data>(text);
                if (data == null || data.v != Version || data.wave < 2 || data.boons == null) {
                    data = null;
                }
            }
        } catch (Exception) {
            data = null;
        }
        cached = data;
        cacheValid = true;
        return data;
    }

    public static bool Exists { get { return Peek() != null; } }
    public static int Wave { get { Data d = Peek(); return d != null ? d.wave : 0; } }

    static void Write(Data data) {
        string text = JsonUtility.ToJson(data);
        cached = data;
        cacheValid = true;
        if (MetaProgress.Sandbox) {
            memory = text;
            return;
        }
        PlayerPrefs.SetString(Key, text);
        PlayerPrefs.Save();
    }

    public static void Clear() {
        Live = false;
        resuming = false;
        cached = null;
        cacheValid = true;
        if (MetaProgress.Sandbox) {
            memory = null;
            return;
        }
        if (PlayerPrefs.HasKey(Key)) {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    // The sandbox switched on: read from its own memory from now on.
    public static void SourceChanged() {
        cacheValid = false;
    }

    // A playtest or a new sandbox starts with no snapshot of its own.
    public static void ForgetMemory() {
        memory = null;
        cacheValid = false;
        Live = false;
        resuming = false;
    }

    // The menu's Continue: the next game scene restores the snapshot.
    public static bool ArmResume() {
        if (!Exists) {
            return false;
        }
        resuming = true;
        return true;
    }

    public static bool Resuming { get { return resuming; } }

    // Called as a new run starts: a resumed one keeps its seed, a fresh one rolls a new one and has no snapshot yet.
    public static void BeginRun() {
        Live = false;
        if (!resuming) {
            runSeed = UnityEngine.Random.Range(1, int.MaxValue);
        }
    }

    // After the run's systems are reset: put the snapshot back. Returns the wave to start, or 0 when there is none.
    public static int Restore() {
        if (!resuming) {
            return 0;
        }
        resuming = false;
        Data d = Peek();
        if (d == null) {
            return 0;
        }
        runSeed = d.seed;
        UnityEngine.Random.InitState(d.seed + d.wave);
        RunBoons.Restore(d.boons, d.level, d.bolts, d.rerolls, d.picks, d.pendingCards, d.pendingWaveCards);
        if (Musician.Instance != null) {
            Musician.Instance.Restore(d.maxHealth, d.health);
        }
        MetaProgress.RestoreCoinsThisRun(d.coins);
        Live = true;
        return d.wave;
    }

    // At a wave clear (nextWave is the wave about to start) and when a card is taken during the break.
    public static void Snapshot(int nextWave) {
        var d = new Data {
            v = Version,
            wave = nextWave,
            act = Tuning.ActOf(nextWave),
            boons = RunBoons.RanksCopy(),
            health = Musician.Instance != null ? Musician.Instance.Health : 0,
            maxHealth = Musician.Instance != null ? Musician.Instance.MaxHealth : 0,
            level = RunBoons.Level,
            bolts = RunBoons.Feathers,
            rerolls = RunBoons.Rerolls,
            picks = RunBoons.Picks,
            pendingCards = RunBoons.PendingLevels,
            pendingWaveCards = RunBoons.PendingWaveCards,
            coins = MetaProgress.CoinsThisRun,
            seed = runSeed
        };
        Write(d);
        Live = true;
    }

    // A boon or card resolved during the break: keep the snapshot true to it.
    public static void Touch() {
        if (!Live) {
            return;
        }
        Data old = Peek();
        if (old != null) {
            Snapshot(old.wave);
        }
    }
}
