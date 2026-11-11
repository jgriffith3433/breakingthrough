using System;
using System.Collections.Generic;
using UnityEngine;

// Arcade's short goals ladder (Docs/Story.md, "Run-end exchanges"). Twelve rungs in Resources/Data/run_goals.json.
// Progress is career (waves reached, clears, home runs, kills, smashes) plus this-run peaks (coins, waves cleared).
// Completing a goal unlocks coins or a bat look/trail (BatMarks via HasLook). Never reorder the JSON rows: bits are by index.
public static class RunGoals {
    public const int Count = 12;

    public class Goal {
        public string Id;
        public string Title;
        public string Kind;
        public int Target;
        public string Reward;
        public int Amount;
        public string Look;
        public string Blurb;
    }

    public struct Progress {
        public Goal Goal;
        public int Current;
        public int Target;
        public bool Done;
        public bool JustDone;
    }

    static Goal[] goals;
    static int doneBits;
    static int careerHomers;
    static int careerKills;
    static int careerSmashes;
    static int bestRunCoins;
    static int bestRunCleared;
    static bool loaded;
    static readonly List<string> justDone = new List<string>();

    public static IReadOnlyList<string> JustDone { get { return justDone; } }
    public static int CareerHomers { get { Ensure(); return careerHomers; } }
    public static int CareerKills { get { Ensure(); return careerKills; } }
    public static int CareerSmashes { get { Ensure(); return careerSmashes; } }

    public static Goal[] All {
        get {
            Ensure();
            return goals;
        }
    }

    public static bool Has(string id) {
        Ensure();
        int i = IndexOf(id);
        return i >= 0 && (doneBits & (1 << i)) != 0;
    }

    public static bool HasLook(string look) {
        if (string.IsNullOrEmpty(look)) {
            return false;
        }
        Ensure();
        for (int i = 0; i < goals.Length; i++) {
            if (goals[i].Reward == "look" && goals[i].Look == look && (doneBits & (1 << i)) != 0) {
                return true;
            }
        }
        return false;
    }

    // Call when the Arcade run ends (Slice fell). Adds this run's counters, stamps newly finished goals, pays rewards.
    public static Progress[] SettleRun(int waveReached, int wavesCleared, int coinsThisRun, int homers, int kills, int smashes) {
        Ensure();
        justDone.Clear();
        careerHomers += Mathf.Max(0, homers);
        careerKills += Mathf.Max(0, kills);
        careerSmashes += Mathf.Max(0, smashes);
        if (coinsThisRun > bestRunCoins) {
            bestRunCoins = coinsThisRun;
        }
        if (wavesCleared > bestRunCleared) {
            bestRunCleared = wavesCleared;
        }
        var finished = new List<Progress>();
        for (int i = 0; i < goals.Length; i++) {
            Goal g = goals[i];
            int cur = CurrentOf(g, waveReached, wavesCleared, coinsThisRun);
            bool was = (doneBits & (1 << i)) != 0;
            bool now = cur >= g.Target;
            if (now && !was) {
                doneBits |= 1 << i;
                justDone.Add(g.Id);
                Pay(g);
                finished.Add(new Progress { Goal = g, Current = cur, Target = g.Target, Done = true, JustDone = true });
            }
        }
        Save();
        return finished.ToArray();
    }

    public static Progress Next() {
        Ensure();
        for (int i = 0; i < goals.Length; i++) {
            if ((doneBits & (1 << i)) != 0) {
                continue;
            }
            Goal g = goals[i];
            int cur = CurrentOf(g, MetaProgress.ReachedWave, MetaProgress.BestWave, bestRunCoins);
            return new Progress { Goal = g, Current = Mathf.Min(cur, g.Target), Target = g.Target, Done = false, JustDone = false };
        }
        Goal last = goals[goals.Length - 1];
        return new Progress { Goal = last, Current = last.Target, Target = last.Target, Done = true, JustDone = false };
    }

    // Playtest / sandbox: clear career bits without touching the real save when Sandbox is on.
    public static void EnterSandboxFresh() {
        Ensure();
        doneBits = 0;
        careerHomers = 0;
        careerKills = 0;
        careerSmashes = 0;
        bestRunCoins = 0;
        bestRunCleared = 0;
        justDone.Clear();
    }

    // Playtest setup: force a goal complete so the next run can show its look.
    public static void SandboxComplete(string id) {
        if (!MetaProgress.Sandbox) {
            return;
        }
        Ensure();
        int i = IndexOf(id);
        if (i < 0 || (doneBits & (1 << i)) != 0) {
            return;
        }
        doneBits |= 1 << i;
        Pay(goals[i]);
        justDone.Add(id);
    }

    static int CurrentOf(Goal g, int waveReached, int wavesCleared, int runCoins) {
        switch (g.Kind) {
            case "reach_wave": return Mathf.Max(waveReached, MetaProgress.ReachedWave);
            case "clear_wave": return Mathf.Max(wavesCleared, MetaProgress.BestWave);
            case "career_homers": return careerHomers;
            case "career_kills": return careerKills;
            case "career_smashes": return careerSmashes;
            case "run_coins": return Mathf.Max(runCoins, bestRunCoins);
            case "run_cleared": return Mathf.Max(wavesCleared, bestRunCleared);
            default: return 0;
        }
    }

    static void Pay(Goal g) {
        if (g.Reward == "coins" && g.Amount > 0) {
            MetaProgress.GrantCoins(g.Amount);
        }
        // Looks are owned via HasLook; BatMarks rebuilds from that.
    }

    static int IndexOf(string id) {
        for (int i = 0; i < goals.Length; i++) {
            if (goals[i].Id == id) {
                return i;
            }
        }
        return -1;
    }

    static void Ensure() {
        if (loaded) {
            return;
        }
        loaded = true;
        goals = LoadFile();
        if (!MetaProgress.Sandbox) {
            doneBits = PlayerPrefs.GetInt("bt_goals", 0);
            careerHomers = PlayerPrefs.GetInt("bt_homers", 0);
            careerKills = PlayerPrefs.GetInt("bt_kills", 0);
            careerSmashes = PlayerPrefs.GetInt("bt_smashes", 0);
            bestRunCoins = PlayerPrefs.GetInt("bt_run_coins", 0);
            bestRunCleared = PlayerPrefs.GetInt("bt_run_cleared", 0);
        }
    }

    static void Save() {
        if (MetaProgress.Sandbox) {
            return;
        }
        PlayerPrefs.SetInt("bt_goals", doneBits);
        PlayerPrefs.SetInt("bt_homers", careerHomers);
        PlayerPrefs.SetInt("bt_kills", careerKills);
        PlayerPrefs.SetInt("bt_smashes", careerSmashes);
        PlayerPrefs.SetInt("bt_run_coins", bestRunCoins);
        PlayerPrefs.SetInt("bt_run_cleared", bestRunCleared);
        PlayerPrefs.Save();
    }

    static Goal[] LoadFile() {
        TextAsset raw = Resources.Load<TextAsset>("Data/run_goals");
        if (raw == null) {
            Debug.LogWarning("RunGoals: Resources/Data/run_goals.json missing; using empty ladder.");
            return new Goal[0];
        }
        try {
            Wrapper wrap = JsonUtility.FromJson<Wrapper>(raw.text);
            if (wrap == null || wrap.goals == null || wrap.goals.Length == 0) {
                return new Goal[0];
            }
            var list = new List<Goal>();
            for (int i = 0; i < wrap.goals.Length && i < Count; i++) {
                Row row = wrap.goals[i];
                list.Add(new Goal {
                    Id = row.id,
                    Title = row.title,
                    Kind = row.kind,
                    Target = row.target,
                    Reward = row.reward,
                    Amount = row.amount,
                    Look = row.look,
                    Blurb = row.blurb
                });
            }
            return list.ToArray();
        } catch (Exception e) {
            Debug.LogWarning("RunGoals: failed to parse run_goals.json: " + e.Message);
            return new Goal[0];
        }
    }

    [Serializable]
    class Wrapper {
        public Row[] goals;
    }

    [Serializable]
    class Row {
        public string id;
        public string title;
        public string kind;
        public int target;
        public string reward;
        public int amount;
        public string look;
        public string blurb;
    }
}
