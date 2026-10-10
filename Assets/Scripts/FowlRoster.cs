using UnityEngine;

public enum FowlKind {
    Turkey,
    Hen,
    Rooster,
    Goose,
    Swan,
    Emu,
    Ostrich,
    Cassowary,
    Terror,
    Roc,
    Thunderbird
}

public struct FowlDef {
    public FowlKind Kind;
    public string Name;
    public int Hits;
    public float Scale;
    public float Wide;
    public float Tall;
    public float Deep;
    public float Walk;
    public float Chase;
    public float Jump;
    public float FlightHeight;
    public bool Weave;
    public bool Charge;
    public Color Tint;
    public int UnlockWave;
    public string Blurb;
}

public static class FowlRoster {
    public static FowlDef Turkey() {
        return new FowlDef {
            Kind = FowlKind.Turkey,
            Name = "Turkey",
            Hits = 1,
            Scale = 1f,
            Wide = 1f,
            Tall = 1f,
            Deep = 1f,
            Walk = 1.15f,
            Chase = 2.1f,
            Jump = 4f,
            FlightHeight = 1.6f,
            Weave = true,
            Tint = Color.white,
            UnlockWave = 1,
            Blurb = "Turkeys. One arrow each. They wander differently, and you grow when they drop."
        };
    }

    public static FowlDef ForTier(int unlockWave) {
        switch (unlockWave) {
            case 10: return Make(FowlKind.Hen, "Hen", 20, 4.6f, 1.18f, 0.92f, 1.12f, 0.72f, 1.15f, 3.2f, 1.3f, false, false, new Color(1.55f, 1.22f, 0.82f), 10, "A Hen. About your height. Four arrows when she first shows up.");
            case 20: return Make(FowlKind.Rooster, "Rooster", 90, 5.4f, 1f, 1.15f, 1f, 1.35f, 2.4f, 4.2f, 1.5f, true, true, new Color(1.65f, 0.32f, 0.22f), 20, "A Rooster. As tall as you, and it takes a lot of arrows.");
            case 30: return Make(FowlKind.Goose, "Goose", 160, 8.2f, 1.05f, 1f, 1.25f, 1.05f, 1.8f, 3.6f, 1.8f, true, false, new Color(0.82f, 0.9f, 0.98f), 30, "A Goose. Twice the old size, and much harder to drop.");
            case 40: return Make(FowlKind.Swan, "Swan", 220, 9.4f, 1f, 1.05f, 1.2f, 0.95f, 1.7f, 3.4f, 2.2f, false, false, new Color(1.65f, 1.65f, 1.75f), 40, "A Swan. About your height, with a long health bar.");
            case 50: return Make(FowlKind.Emu, "Emu", 280, 10.8f, 0.85f, 1.4f, 0.9f, 1.55f, 2.5f, 3.8f, 1.4f, true, true, new Color(1.15f, 0.72f, 0.42f), 50, "An Emu. Tall as you, and it closes the gap.");
            case 60: return Make(FowlKind.Ostrich, "Ostrich", 330, 12.4f, 0.9f, 1.45f, 0.95f, 1.7f, 2.8f, 3.5f, 1.3f, false, true, new Color(1.15f, 1.02f, 0.88f), 60, "An Ostrich. A running wall about your size.");
            case 70: return Make(FowlKind.Cassowary, "Cassowary", 420, 14f, 1f, 1.2f, 1f, 1.45f, 2.6f, 3.6f, 1.2f, true, true, new Color(0.32f, 0.26f, 0.42f), 70, "A Cassowary. Dark, your height, and stubborn.");
            case 80: return Make(FowlKind.Terror, "Terror", 500, 16.2f, 1.1f, 1.25f, 1.15f, 1.1f, 2f, 3.8f, 1.5f, false, true, new Color(0.55f, 0.3f, 0.26f), 80, "A Terror. Huge, and it takes a lot of arrows.");
            case 90: return Make(FowlKind.Roc, "Roc", 600, 18.6f, 1.35f, 0.85f, 1.4f, 0.9f, 1.6f, 3f, 2.4f, false, false, new Color(0.42f, 0.55f, 0.9f), 90, "A Roc. It covers you, and the health bar is long.");
            case 100: return Make(FowlKind.Thunderbird, "Thunderbird", 900, 28f, 1.4f, 1.1f, 1.45f, 0.85f, 1.5f, 3.2f, 2.6f, false, false, new Color(1.45f, 1.12f, 0.32f), 100, "The Thunderbird. Bigger than you. The last boss.");
            default: return Turkey();
        }
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
            return "One turkey. One arrow. You grow as they fall.";
        }
        if (wave % 10 == 0 && wave <= 100) {
            FowlDef debut = ForTier(wave);
            int arrows = ArrowsFor(debut.Hits, wave);
            string shot = arrows <= 1 ? "One arrow drops it." : "It takes " + arrows + " arrows right now.";
            return debut.Blurb + " " + shot;
        }
        if (wave == 5) {
            return "Your bow fires on its own now. Turkeys still drop in one arrow.";
        }
        if (wave == 19) {
            return "You are big enough. One arrow drops a Hen.";
        }
        int decade = (wave / 10) * 10;
        if (decade >= 10) {
            FowlDef current = ForTier(decade);
            int arrows = ArrowsFor(current.Hits, wave);
            string shot = arrows <= 1 ? "One arrow drops a " + current.Name + "." : "A " + current.Name + " takes " + arrows + " arrows.";
            return "Turkeys, and everything you have met. " + shot;
        }
        return "More turkeys. One arrow each. You are getting bigger.";
    }

    public static FowlDef ForSpawn(FowlKind kind, int wave) {
        FowlDef def = kind == FowlKind.Turkey ? Turkey() : ForTier(UnlockOf(kind));
        def.Scale *= 1.18f + (Mathf.Max(1, wave) - 1) * 0.012f;
        if (wave > 100) {
            def.Scale *= 1f + (wave - 100) * 0.025f;
        }
        return def;
    }

    static int UnlockOf(FowlKind kind) {
        switch (kind) {
            case FowlKind.Hen: return 10;
            case FowlKind.Rooster: return 20;
            case FowlKind.Goose: return 30;
            case FowlKind.Swan: return 40;
            case FowlKind.Emu: return 50;
            case FowlKind.Ostrich: return 60;
            case FowlKind.Cassowary: return 70;
            case FowlKind.Terror: return 80;
            case FowlKind.Roc: return 90;
            case FowlKind.Thunderbird: return 100;
            default: return 1;
        }
    }

    public static int ArrowsFor(int hits, int wave) {
        int damage = PlayerGrowth.DamageAtWave(wave);
        return Mathf.Max(1, Mathf.CeilToInt(hits / (float)damage));
    }

    static FowlDef Make(FowlKind kind, string name, int hits, float scale, float wide, float tall, float deep, float walk, float chase, float jump, float flight, bool weave, bool charge, Color tint, int unlock, string blurb) {
        return new FowlDef {
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
            FlightHeight = flight,
            Weave = weave,
            Charge = charge,
            Tint = tint,
            UnlockWave = unlock,
            Blurb = blurb
        };
    }
}
