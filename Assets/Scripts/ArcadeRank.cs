using UnityEngine;

// The Arcade run's rank (S, A, B, C) on the run-end sheet. It is earned from what the run did: waves cleared, bots struck
// off, the best kill streak and the waves Slice came through untouched ("clean waves", the no-hit run). Nothing else
// moves it. The sheet prints the rank with the same four numbers, so the player can see where it came from.
public static class ArcadeRank {
    public static int CleanWaves { get; private set; }
    public static int BestClean { get; private set; }
    static int damageAtWaveStart;
    static int damageNow;

    public static void BeginRun() {
        CleanWaves = 0;
        BestClean = 0;
        damageAtWaveStart = 0;
        damageNow = 0;
    }

    // A wave's fighting begins: what Slice has taken so far is the line a clean wave must not cross.
    public static void WaveStarted() {
        damageAtWaveStart = Musician.Instance != null ? Musician.Instance.DamageTaken : 0;
    }

    // A wave was cleared: clean when Slice took nothing since it started. A hit breaks the run of clean waves.
    public static void WaveCleared() {
        damageNow = Musician.Instance != null ? Musician.Instance.DamageTaken : 0;
        if (damageNow <= damageAtWaveStart) {
            CleanWaves++;
            if (CleanWaves > BestClean) {
                BestClean = CleanWaves;
            }
        } else {
            CleanWaves = 0;
        }
    }

    public static int Points(int cleared, int kills, int bestStreak, int bestClean) {
        return Mathf.Max(0, cleared) * 10 + Mathf.Max(0, kills) / 5 + Mathf.Max(0, bestStreak) / 4 + Mathf.Max(0, bestClean) * 6;
    }

    public static string Letter(int points) {
        return points >= 260 ? "S" : points >= 150 ? "A" : points >= 70 ? "B" : "C";
    }

    // "Rank B  ·  5 waves cleared  ·  88 struck off  ·  streak 24  ·  2 waves untouched"
    public static string Line(int cleared, int kills, int bestStreak, int bestClean, out string letter) {
        letter = Letter(Points(cleared, kills, bestStreak, bestClean));
        return "Rank " + letter + "  ·  " + cleared + (cleared == 1 ? " wave" : " waves") + " cleared  ·  " + kills + " struck off  ·  streak " + bestStreak
            + "  ·  " + bestClean + (bestClean == 1 ? " wave" : " waves") + " untouched";
    }
}
