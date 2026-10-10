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
            case 10: return Make(FowlKind.Hen, "Hen", 10, 3f, 1.18f, 0.92f, 1.12f, 0.72f, 1.15f, 3.2f, 1.3f, false, false, new Color(1.55f, 1.22f, 0.82f), 10, "A Hen. Three times a turkey. Your arrows have to earn it.");
            case 20: return Make(FowlKind.Rooster, "Rooster", 15, 3.5f, 1f, 1.15f, 1f, 1.35f, 2.4f, 4.2f, 1.5f, true, true, new Color(1.65f, 0.32f, 0.22f), 20, "A Rooster. It rushes, and it takes more arrows than a hen.");
            case 30: return Make(FowlKind.Goose, "Goose", 20, 4.1f, 1.05f, 1f, 1.25f, 1.05f, 1.8f, 3.6f, 1.8f, true, false, new Color(0.82f, 0.9f, 0.98f), 30, "A Goose. Long, loud, and thick.");
            case 40: return Make(FowlKind.Swan, "Swan", 25, 4.7f, 1f, 1.05f, 1.2f, 0.95f, 1.7f, 3.4f, 2.2f, false, false, new Color(1.65f, 1.65f, 1.75f), 40, "A Swan. Big wings and a long health bar.");
            case 50: return Make(FowlKind.Emu, "Emu", 30, 5.4f, 0.85f, 1.4f, 0.9f, 1.55f, 2.5f, 3.8f, 1.4f, true, true, new Color(1.15f, 0.72f, 0.42f), 50, "An Emu. Tall, and it closes the gap.");
            case 60: return Make(FowlKind.Ostrich, "Ostrich", 35, 6.2f, 0.9f, 1.45f, 0.95f, 1.7f, 2.8f, 3.5f, 1.3f, false, true, new Color(1.15f, 1.02f, 0.88f), 60, "An Ostrich. A running wall.");
            case 70: return Make(FowlKind.Cassowary, "Cassowary", 40, 7f, 1f, 1.2f, 1f, 1.45f, 2.6f, 3.6f, 1.2f, true, true, new Color(0.32f, 0.26f, 0.42f), 70, "A Cassowary. Dark, fast, and stubborn.");
            case 80: return Make(FowlKind.Terror, "Terror", 45, 8.1f, 1.1f, 1.25f, 1.15f, 1.1f, 2f, 3.8f, 1.5f, false, true, new Color(0.55f, 0.3f, 0.26f), 80, "A Terror. Old, huge, and angry.");
            case 90: return Make(FowlKind.Roc, "Roc", 50, 9.3f, 1.35f, 0.85f, 1.4f, 0.9f, 1.6f, 3f, 2.4f, false, false, new Color(0.42f, 0.55f, 0.9f), 90, "A Roc. It covers the hill.");
            case 100: return Make(FowlKind.Thunderbird, "Thunderbird", 50, 11f, 1.4f, 1.1f, 1.45f, 0.85f, 1.5f, 3.2f, 2.6f, false, false, new Color(1.45f, 1.12f, 0.32f), 100, "The Thunderbird. The last bird on the hill.");
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
