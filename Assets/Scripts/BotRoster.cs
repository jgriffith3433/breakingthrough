using UnityEngine;

// The bots, smallest to largest. One new kind every 10 waves. Keep this order: kill counts, the wave list,
// spawn pacing and the coin tally (index 1) are packed by it.
public enum BotKind {
    YoBot,
    PlatedYoBot,
    MegaYoBot,
    SpiderBot,
    StalkerSpiderBot,
    RunnerSpiderBot,
    RammerSpiderBot,
    ShadowSpiderBot,
    CrusherSpiderBot,
    ColossusSpiderBot,
    TitanSpiderBot
}

public struct BotDef {
    public BotKind Kind;
    public string Name;
    public int Hits;
    public float Scale;
    public float Wide;
    public float Tall;
    public float Deep;
    public float Walk;
    public float Chase;
    public float Jump;
    // How high a jump-jet hop climbs.
    public float HopHeight;
    public bool Weave;
    public bool Charge;
    public Color Tint;
    public int UnlockWave;
    public string Blurb;
}

public static class BotRoster {
    public static BotDef YoBot() {
        return new BotDef {
            Kind = BotKind.YoBot,
            Name = "Yo Bot",
            Hits = 3,
            Scale = 1f,
            Wide = 1f,
            Tall = 1f,
            Deep = 1f,
            Walk = 1.15f,
            Chase = 2.1f,
            Jump = 4f,
            HopHeight = 1.6f,
            Weave = true,
            Tint = new Color(1.25f, 0.95f, 0.4f),
            UnlockWave = 1,
            Blurb = "Yo Bots. Small, loud, and walking straight at the piano. You grow every time one drops."
        };
    }

    public static BotDef ForTier(int unlockWave) {
        switch (unlockWave) {
            case 10: return Make(BotKind.PlatedYoBot, "Plated Yo Bot", 60, 4.6f, 1.18f, 0.92f, 1.12f, 0.72f, 1.15f, 3.2f, 1.3f, false, false, new Color(0.95f, 1.1f, 1.25f), 10, "A Plated Yo Bot. Your height, bolted shut, and slow to come apart.");
            case 20: return Make(BotKind.MegaYoBot, "Mega Yo Bot", 270, 5.4f, 1f, 1.15f, 1f, 1.35f, 2.4f, 4.2f, 1.5f, true, true, new Color(1.65f, 0.32f, 0.22f), 20, "A Mega Yo Bot. As tall as you, and it rushes the piano in bursts.");
            case 30: return Make(BotKind.SpiderBot, "Spider Bot", 480, 8.2f, 1.05f, 1f, 1.25f, 1.05f, 1.8f, 3.6f, 1.8f, true, false, new Color(0.55f, 1.05f, 1.1f), 30, "A Spider Bot. Eight legs, twice the size of anything so far, and a long way from scrap.");
            case 40: return Make(BotKind.StalkerSpiderBot, "Stalker Spider Bot", 660, 9.4f, 1f, 1.05f, 1.2f, 0.95f, 1.7f, 3.4f, 2.2f, false, false, new Color(1.6f, 1.62f, 1.75f), 40, "A Stalker Spider Bot. Quiet on its legs, with a long health bar.");
            case 50: return Make(BotKind.RunnerSpiderBot, "Runner Spider Bot", 840, 10.8f, 0.85f, 1.4f, 0.9f, 1.55f, 2.5f, 3.8f, 1.4f, true, true, new Color(1.6f, 0.82f, 0.3f), 50, "A Runner Spider Bot. Long legs, and it closes the gap.");
            case 60: return Make(BotKind.RammerSpiderBot, "Rammer Spider Bot", 990, 12.4f, 0.9f, 1.45f, 0.95f, 1.7f, 2.8f, 3.5f, 1.3f, false, true, new Color(1.3f, 1.12f, 0.8f), 60, "A Rammer Spider Bot. A running wall with a plate on the front.");
            case 70: return Make(BotKind.ShadowSpiderBot, "Shadow Spider Bot", 1260, 14f, 1f, 1.2f, 1f, 1.45f, 2.6f, 3.6f, 1.2f, true, true, new Color(0.42f, 0.3f, 0.62f), 70, "A Shadow Spider Bot. Dark, your height, and stubborn.");
            case 80: return Make(BotKind.CrusherSpiderBot, "Crusher Spider Bot", 1500, 16.2f, 1.1f, 1.25f, 1.15f, 1.1f, 2f, 3.8f, 1.5f, false, true, new Color(1.05f, 0.5f, 0.32f), 80, "A Crusher Spider Bot. Huge, rusted, and it does not stop.");
            case 90: return Make(BotKind.ColossusSpiderBot, "Colossus Spider Bot", 1800, 18.6f, 1.35f, 0.85f, 1.4f, 0.9f, 1.6f, 3f, 2.4f, false, false, new Color(0.48f, 0.68f, 1.4f), 90, "A Colossus Spider Bot. It stands over you, and the health bar is long.");
            case 100: return Make(BotKind.TitanSpiderBot, "Titan Spider Bot", 2700, 28f, 1.4f, 1.1f, 1.45f, 0.85f, 1.5f, 3.2f, 2.6f, false, false, new Color(1.45f, 1.12f, 0.32f), 100, "The Titan. A gold Spider Bot bigger than you. The last boss.");
            default: return YoBot();
        }
    }

    public static BotDef ForKind(BotKind kind) {
        return kind == BotKind.YoBot ? YoBot() : ForTier(UnlockOf(kind));
    }

    // Yo Bots walk on two legs and swipe with claws; Spider Bots walk on eight and stab with the front pair.
    public static bool IsSpider(BotKind kind) {
        return kind >= BotKind.SpiderBot;
    }

    // The kind as analytics and the run log name it: yo_bot, plated_yo_bot, spider_bot, titan_spider_bot.
    public static string Slug(BotKind kind) {
        return ForKind(kind).Name.ToLowerInvariant().Replace(' ', '_');
    }

    public static int Total(int wave) {
        int count = Mathf.Max(0, wave);
        for (int tier = 10; tier <= 100; tier += 10) {
            int extra = wave - (tier - 1);
            if (extra > 0) {
                count += extra;
            }
        }
        return count;
    }

    public static string Intro(int wave) {
        if (wave <= 1) {
            return YoBot().Blurb;
        }
        if (wave % 10 == 0 && wave <= 100) {
            return ForTier(wave).Blurb;
        }
        if (wave == 5) {
            return "Yo Bots come apart fast now.";
        }
        int decade = (wave / 10) * 10;
        if (decade >= 10) {
            return "Yo Bots, and every bot you have met. The " + ForTier(Mathf.Min(100, decade)).Name + " is the big one.";
        }
        return "More Yo Bots. You are getting bigger.";
    }

    public static BotDef ForSpawn(BotKind kind, int wave) {
        BotDef def = ForKind(kind);
        def.Scale *= 1.18f + (Mathf.Max(1, wave) - 1) * 0.012f;
        if (wave > 100) {
            def.Scale *= 1f + (wave - 100) * 0.025f;
        }
        return def;
    }

    public static int DebutOf(BotKind kind) {
        return kind == BotKind.YoBot ? 0 : UnlockOf(kind);
    }

    static int UnlockOf(BotKind kind) {
        return kind == BotKind.YoBot ? 1 : (int)kind * 10;
    }

    static BotDef Make(BotKind kind, string name, int hits, float scale, float wide, float tall, float deep, float walk, float chase, float jump, float hop, bool weave, bool charge, Color tint, int unlock, string blurb) {
        return new BotDef {
            Kind = kind,
            Name = name,
            Hits = hits,
            Scale = scale,
            Wide = wide,
            Tall = tall,
            Deep = deep,
            Walk = walk,
            Chase = chase,
            Jump = jump,
            HopHeight = hop,
            Weave = weave,
            Charge = charge,
            Tint = tint,
            UnlockWave = unlock,
            Blurb = blurb
        };
    }
}
