using UnityEngine;

// Balance knobs in one place. Change a number here, press Play, feel the difference.
public static class Tuning {
    // When the wave powers turn on. They used to be 20 / 30 / 40 / 50, which most runs never saw.
    public const int JumpWave = 8;
    public const int AutoWave = 12;
    public const int StompWave = 16;
    public const int ClimbWave = 25;
    // Jump-landing hurts bots that debuted this many waves before the current one.
    public const int JumpLandGap = 8;
    // From ClimbWave, bots this many waves past their debut climb you instead of the musician.
    public const int ClimbGap = 5;

    // The bat. Reach is meters at size 1 and grows with PlayerGrowth.Scale. Damage is times Combat.BatBase.
    public const float BatReach = 3f;
    public const float BatArc = 140f;
    public const float BatArcFinisher = 170f;
    public const float BatDamage = 1f;
    public const float BatDamageFinisher = 1.8f;
    // Swings 1 and 2 knock birds back a step, so the next swing still reaches them; the finisher sends them off.
    public const float BatKnock = 3.5f;
    public const float BatKnockFinisher = 12f;
    // A target smaller than this share of your size gets the bat swung down at the ground instead.
    public const float SmashBelow = 0.75f;
    public const float SmashDamage = 1.3f;
    // Hold past ChargeStart to wind up; a release from HomeRunMin of a full charge is a home run.
    public const float ChargeStart = 0.22f;
    public const float ChargeSeconds = 0.9f;
    public const float HomeRunMin = 0.45f;
    public const float HomeRunDamage = 3f;
    public const float HomeRunSpeed = 26f;
    // The next combo swing has to start within this long of the last one ending, or the combo starts over.
    public const float ComboGap = 0.55f;
    // Most bots one combo swing can hit (nearest first). Follow Through adds 3 a rank; smash and spin hit them all.
    public const int BatHits = 6;
    public const int BatHitsFinisher = 8;

    // Bat boons. Sweet Spot's tip is the outer share of the bat's reach.
    public const float SweetSpotFrom = 0.6f;
    // Grand Slam's spin: reach times this, damage times BatBase.
    public const float SpinReach = 1.2f;
    public const float SpinDamage = 1.4f;
    // Long Ball: a bolt batted at the nearest bot between the bat's reach and this far (meters, times your size).
    public const float LongBallRange = 9f;
    public const float LongBallSpeed = 38f;
    // Aluminum! Not wood!: knockback times this, and a blast on the combo finisher.
    public const float MetalKnock = 1.3f;
    public const float MetalBlastDamage = 0.65f;

    // Breaks between waves. Space or Enter skips both.
    public const float BreakSeconds = 5f;
    public const float BuildSeconds = 30f;

    // In-run experience. Level L needs LevelBase + L * LevelStep + L * L * LevelCurve bolts.
    public const float LevelBase = 5f;
    public const float LevelStep = 4f;
    public const float LevelCurve = 0.5f;
    public const int StartRerolls = 1;

    // Bolts each bot drops when it is destroyed. Bigger bots are worth more.
    public static int BoltsFor(BotKind kind, bool elite) {
        int value;
        switch (kind) {
            case BotKind.YoBot: value = 1; break;
            case BotKind.PlatedYoBot: value = 6; break;
            case BotKind.MegaYoBot: value = 10; break;
            case BotKind.SpiderBot: value = 19; break;
            case BotKind.StalkerSpiderBot: value = 22; break;
            case BotKind.RunnerSpiderBot: value = 25; break;
            case BotKind.RammerSpiderBot: value = 28; break;
            case BotKind.ShadowSpiderBot: value = 31; break;
            case BotKind.CrusherSpiderBot: value = 34; break;
            case BotKind.ColossusSpiderBot: value = 37; break;
            default: value = 40; break;
        }
        return elite ? value * 4 : value;
    }

    // Pickup pull radius before the Magnet boon.
    public const float BasePickup = 5f;

    // Elites. From EliteWave a share of Yo Bots spawn with a trait, more as waves climb.
    public const int EliteWave = 3;
    public static float EliteChance(int wave) {
        if (wave < EliteWave) {
            return 0f;
        }
        return Mathf.Min(0.35f, 0.06f + (wave - EliteWave) * 0.012f);
    }

    // The golden Yo Bot runs from you and drops a treasure chest. One every few waves.
    public const int GoldenFirstWave = 4;
    public const int GoldenEvery = 4;
    public const float GoldenEscapeSeconds = 22f;

    // Each new kind of bot arrives with a boss on its debut wave.
    public const float BossHealthScale = 4f;
    public const float BossSummonEvery = 6f;

    // Wave events start here, roll this often, and never land on boss or build waves.
    public const int EventFirstWave = 6;
    public const float EventChance = 0.4f;

    // Combo. A kill within this window keeps the streak. Each kill adds this much bolt bonus.
    public const float ComboWindow = 2.4f;
    public const float ComboFeatherPerKill = 0.01f;
    public const float ComboFeatherMax = 0.5f;

    // Coins. Every cleared wave pays something. Bosses and chests pay more.
    public static int CoinsForWave(int wave) {
        return 1 + wave / 10;
    }
    public const int CoinsForBoss = 6;
    public const int CoinsForGolden = 3;

    // Bot health grows with the wave on top of the authored value, and faster after wave 20,
    // so a snowballing build still meets a wall. About 1.7x at 30, 3.3x at 50, 12x at 100.
    public static float HealthScale(int wave) {
        float late = Mathf.Max(0, wave - 20);
        return 1f + Mathf.Max(0, wave - 1) * 0.02f + late * late * 0.0015f;
    }
}
