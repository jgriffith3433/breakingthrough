using System.IO;
using System.Text;
using UnityEngine;

// One line per wave so a run can be read back: how long it took, what hurt, what you picked.
// Written to runlog.csv in Application.persistentDataPath and echoed to the console.
public static class RunLog {
    static string runId;
    static float waveStart;
    static int musicianStart;
    static int swings;
    static int connects;
    static int hits;
    static float dealt;
    static int kills;
    static readonly int[] killsByKind = new int[WaveDirector.KindCount];
    static readonly StringBuilder picks = new StringBuilder();
    static bool headerChecked;

    // Whole-run totals, for the playtest report.
    // Swings taken, swings that struck at least one bird, and birds struck (one swing can hit several).
    public static int RunSwings { get; private set; }
    public static int RunConnects { get; private set; }
    public static int RunHits { get; private set; }
    public static int RunKills { get; private set; }
    public static float RunDamage { get; private set; }

    public static string FilePath {
        get { return Path.Combine(Application.persistentDataPath, "runlog.csv"); }
    }

    public static void BeginRun() {
        runId = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
        RunSwings = 0;
        RunConnects = 0;
        RunHits = 0;
        RunKills = 0;
        RunDamage = 0f;
        Reset();
    }

    // Picks made during the break belong to the wave about to start, so they are kept.
    public static void BeginWave() {
        string kept = picks.ToString();
        Reset();
        picks.Append(kept);
    }

    static void Reset() {
        waveStart = Time.time;
        musicianStart = Musician.Instance != null ? Musician.Instance.Health : 0;
        swings = 0;
        connects = 0;
        hits = 0;
        dealt = 0f;
        kills = 0;
        for (int i = 0; i < killsByKind.Length; i++) {
            killsByKind[i] = 0;
        }
        picks.Length = 0;
    }

    public static void NoteSwing() {
        swings++;
        RunSwings++;
    }

    // A swing struck something. Once per swing.
    public static void NoteConnect() {
        connects++;
        RunConnects++;
    }

    public static void NoteHit(float amount) {
        hits++;
        dealt += amount;
        RunHits++;
        RunDamage += amount;
    }

    public static void NoteKill(BotKind kind) {
        kills++;
        RunKills++;
        int index = (int)kind;
        if (index >= 0 && index < killsByKind.Length) {
            killsByKind[index]++;
        }
    }

    public static void NotePick(string name) {
        if (picks.Length > 0) {
            picks.Append('|');
        }
        picks.Append(name);
    }

    public static void EndWave(int wave, string outcome) {
        if (DevAutopilot.On) {
            outcome += "-auto";
        }
        int hp = Musician.Instance != null ? Musician.Instance.Health : 0;
        float seconds = Time.time - waveStart;
        var byKind = new StringBuilder();
        for (int i = 0; i < killsByKind.Length; i++) {
            if (killsByKind[i] <= 0) {
                continue;
            }
            if (byKind.Length > 0) {
                byKind.Append('|');
            }
            byKind.Append((BotKind)i).Append(':').Append(killsByKind[i]);
        }
        string line = string.Join(",", new[] {
            runId,
            wave.ToString(),
            outcome,
            seconds.ToString("0.0"),
            musicianStart.ToString(),
            hp.ToString(),
            kills.ToString(),
            byKind.ToString(),
            swings.ToString(),
            hits.ToString(),
            Mathf.RoundToInt(dealt).ToString(),
            RunBoons.Level.ToString(),
            Combo.Best.ToString(),
            MetaProgress.CoinsThisRun.ToString(),
            Quote(WaveDirector.EventName),
            Quote(picks.ToString()),
            Quote(RunBoons.Summary())
        });
        Debug.Log("[RunLog] wave " + wave + " " + outcome + " in " + seconds.ToString("0.0") + "s, musician " + musicianStart + "→" + hp
            + ", kills " + kills + ", swings " + swings + " (" + connects + " connected, " + hits + " bots hit), level " + RunBoons.Level + (picks.Length > 0 ? ", picked " + picks : ""));
        try {
            if (!headerChecked) {
                headerChecked = true;
                if (!File.Exists(FilePath)) {
                    File.AppendAllText(FilePath, "run,wave,outcome,seconds,musician_start,musician_end,kills,kills_by_kind,swings,hits,damage,level,best_streak,coins_run,event,picked,boons\n");
                }
            }
            File.AppendAllText(FilePath, line + "\n");
        } catch (System.Exception) {
            // WebGL and read-only folders: the console line is enough.
        }
        Reset();
    }

    static string Quote(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }
        return "\"" + value.Replace("\"", "'") + "\"";
    }
}
