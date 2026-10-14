using UnityEngine;

// Balance knobs in one place. Change a number here, press Play, feel the difference.
public static class Tuning {
    // When the wave powers turn on. They used to be 20 / 30 / 40 / 50, which most runs never saw.
    public const int JumpWave = 8;
    public const int AutoWave = 12;
    public const int StompWave = 16;
    public const int ClimbWave = 25;
    // Jump-landing hurts birds that debuted this many waves before the current one.
    public const int JumpLandGap = 8;
    // From ClimbWave, birds this many waves past their debut climb you instead of the musician.
    public const int ClimbGap = 5;

    // Breaks between waves. Space or Enter skips both.
    public const float BreakSeconds = 5f;
    public const float BuildSeconds = 30f;

    // In-run experience. Level L needs LevelBase + L * LevelStep + L * L * LevelCurve feathers.
    public const float LevelBase = 5f;
    public const float LevelStep = 4f;
    public const float LevelCurve = 0.5f;
    public const int StartRerolls = 1;

    // Feathers each bird drops on death. Bigger birds are worth more.
    public static int FeathersFor(FowlKind kind, bool elite) {
        int value;
        switch (kind) {
            case FowlKind.Turkey: value = 1; break;
            case FowlKind.Hen: value = 6; break;
            case FowlKind.Rooster: value = 10; break;
            default: value = 10 + (int)kind * 3; break;
        }
        return elite ? value * 4 : value;
    }

    // Pickup pull radius before the Magnet boon.
    public const float BasePickup = 5f;

    // Elites. From EliteWave a share of turkeys spawn with a trait, more as waves climb.
    public const int EliteWave = 3;
    public static float EliteChance(int wave) {
        if (wave < EliteWave) {
            return 0f;
        }
        return Mathf.Min(0.35f, 0.06f + (wave - EliteWave) * 0.012f);
    }

    // The golden gobbler runs from you and drops a treasure chest. One every few waves.
    public const int GoldenFirstWave = 4;
    public const int GoldenEvery = 4;
    public const float GoldenEscapeSeconds = 22f;

    // Decade birds arrive with a boss on their debut wave.
    public const float BossHealthScale = 4f;
    public const float BossSummonEvery = 6f;

    // Wave events start here, roll this often, and never land on boss or build waves.
    public const int EventFirstWave = 6;
    public const float EventChance = 0.4f;

    // Combo. A kill within this window keeps the streak. Each kill adds this much feather bonus.
    public const float ComboWindow = 2.4f;
    public const float ComboFeatherPerKill = 0.01f;
    public const float ComboFeatherMax = 0.5f;

    // Coins. Every cleared wave pays something. Bosses and chests pay more.
    public static int CoinsForWave(int wave) {
        return 1 + wave / 10;
    }
    public const int CoinsForBoss = 6;
    public const int CoinsForGolden = 3;

    // Bird health grows with the wave on top of the authored value, and faster after wave 20,
    // so a snowballing build still meets a wall. About 1.7x at 30, 3.3x at 50, 12x at 100.
    public static float HealthScale(int wave) {
        float late = Mathf.Max(0, wave - 20);
        return 1f + Mathf.Max(0, wave - 1) * 0.02f + late * late * 0.0015f;
    }
}
