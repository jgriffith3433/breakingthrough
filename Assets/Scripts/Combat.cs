using System.Collections.Generic;
using UnityEngine;

public enum DamageKind {
    Plain,
    Arrow,
    Burn,
    Blast,
    Lightning,
    Blade,
    Pulse,
    Bat
}

// What a hit does once boons are involved: crits, marks, fire, frost, lightning, bombs.
public static class Combat {
    static int killCount;
    static int bountyCount;
    static int bombDepth;

    // What the bat boons did this run, for the playtest.
    public static int Burned { get; private set; }
    public static int Slowed { get; private set; }
    public static int Arcs { get; private set; }
    public static int Shorted { get; private set; }
    public static int SweetSpots { get; private set; }
    public static int CalledBolts { get; private set; }

    public static void BeginRun() {
        killCount = 0;
        bountyCount = 0;
        bombDepth = 0;
        Burned = 0;
        Slowed = 0;
        Arcs = 0;
        Shorted = 0;
        SweetSpots = 0;
        CalledBolts = 0;
    }

    public static float ArrowBase() {
        float raw = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.ArrowDamage : 1f;
        return raw * RunBoons.DamageMult;
    }

    // One bat hit's base damage, before the swing's own multiplier.
    public static float BatBase() {
        return ArrowBase();
    }

    // The bat strikes a bot. Scale is the swing's multiplier (finisher, smash, home run, a bot knocked into
    // another). Power is how hard a bot it drops is flung. Tip: it met the bat's sweet spot. True when this blow
    // dropped the bot.
    public static bool BatHit(Enemy foe, float scale, float power, bool allowChain, bool tip = false) {
        return Strike(foe, power, scale, allowChain, DamageKind.Bat, tip);
    }

    // A hit with every boon on it: crits, Called Shot, executes, Pine Tar, Ice Bat and Live Wire.
    static bool Strike(Enemy foe, float power, float scale, bool allowChain, DamageKind kind, bool tip) {
        if (foe == null || !foe.Alive) {
            return false;
        }
        float amount = ArrowBase() * scale;
        ZeldaStyleCamera cam = Juice.Cam;
        bool called = cam != null && cam.LockedOnGameObject == foe.gameObject;
        if (called) {
            amount *= RunBoons.MarkMult;
        }
        float tipCrit = tip ? RunBoons.TipCrit : 0f;
        bool crit = Random.value < RunBoons.CritChance + tipCrit;
        if (crit) {
            amount *= RunBoons.CritMult;
            if (tipCrit > 0f) {
                SweetSpots++;
            }
        }
        float execute = RunBoons.ExecuteBelow;
        if (execute > 0f && !foe.Boss && foe.Health01 - amount / Mathf.Max(1f, foe.HitsToDrop) < execute) {
            amount = Mathf.Max(amount, foe.HitsToDrop * foe.Health01 + 1f);
            crit = true;
        }
        RunLog.NoteHit(amount);
        Vector3 at = foe.transform.position;
        bool dropped = foe.Damage(amount, kind, crit, -1f, power);
        if (crit && dropped && (foe.BodyScale > 3f || foe.Elite)) {
            Juice.HitStop(0.05f);
        }
        if (crit && tipCrit > 0f) {
            Fx.Ring(at + Vector3.up * 0.2f, 1.6f * Mathf.Sqrt(Mathf.Max(1f, foe.BodyScale)), new Color(1f, 0.35f, 0.3f, 0.85f), 0.3f);
        }

        // Called Shot: the locked bot pays out extra bolts when it falls.
        int callRank = RunBoons.Rank(BoonId.Mark);
        if (called && dropped && callRank > 0) {
            int extra = Mathf.Max(callRank, Mathf.CeilToInt(Tuning.BoltsFor(foe.Kind, foe.Elite || foe.Boss) * 0.25f * callRank));
            Pickups.DropBolts(at, extra);
            CalledBolts += extra;
        }

        int fire = RunBoons.Rank(BoonId.Firebrand);
        if (fire > 0 && !dropped) {
            foe.Ignite(ArrowBase() * (0.25f + 0.15f * fire), 3f);
            Burned++;
        }
        int frost = RunBoons.Rank(BoonId.Frost);
        if (frost > 0 && !dropped) {
            foe.Chill(0.25f * frost, 2f, frost >= 3 && Random.value < 0.2f ? 1.1f : 0f);
            Slowed++;
        }
        if (allowChain) {
            bool storm = RunBoons.Has(BoonId.StormQuiver);
            int chain = RunBoons.Rank(BoonId.Chain);
            if (storm || (chain > 0 && Random.value < 0.15f * chain)) {
                Lightning(at + Vector3.up, foe, storm ? 4 : 3, ArrowBase() * (storm ? 0.6f : 0.75f));
            }
        }
        return dropped;
    }

    // Blizzard Bat: frost blown through the swing's arc. Slows everything in it; a finisher freezes.
    public static void Frost(Enemy foe, bool freeze) {
        if (foe == null || !foe.Alive) {
            return;
        }
        foe.Chill(0.55f, 2.2f, freeze ? 1.2f : 0f);
        Slowed++;
    }

    // Live Wire: lightning jumps bot to bot, and each one it reaches shorts out for a moment.
    public static void Lightning(Vector3 from, Enemy skip, int jumps, float damage) {
        float shortFor = 0.35f + 0.2f * RunBoons.Rank(BoonId.Chain) + (RunBoons.Has(BoonId.StormQuiver) ? 0.3f : 0f);
        var struck = new List<Enemy>();
        if (skip != null) {
            struck.Add(skip);
        }
        Vector3 cursor = from;
        for (int j = 0; j < jumps; j++) {
            Enemy next = Nearest(cursor, 12f, struck);
            if (next == null) {
                break;
            }
            struck.Add(next);
            Vector3 to = next.transform.position + Vector3.up * 0.6f * Mathf.Max(1f, next.BodyScale);
            UpgradeLook.Zap(cursor, to);
            Arcs++;
            if (!next.Damage(damage, DamageKind.Lightning, false) && next != null) {
                next.Short(shortFor);
                Shorted++;
            }
            if (RunBoons.Has(BoonId.Hailstorm)) {
                next.Chill(0.5f, 1.5f, 0f);
            }
            cursor = to;
        }
        if (struck.Count > (skip != null ? 1 : 0)) {
            GameAudio.Play("shot", 0.35f, 1.8f);
        }
    }

    public static Enemy Nearest(Vector3 point, float range, List<Enemy> skip) {
        Enemy best = null;
        float bestDist = range * range;
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing || (skip != null && skip.Contains(enemy))) {
                continue;
            }
            float dist = (enemy.transform.position - point).sqrMagnitude;
            if (dist < bestDist) {
                bestDist = dist;
                best = enemy;
            }
        }
        return best;
    }

    // Splash damage around a point. Used by the aluminum bat's blast, bombs, encore, second wind.
    public static void Burst(Vector3 point, float radius, float damage, DamageKind kind, Enemy skip) {
        List<Enemy> roster = Enemy.Roster;
        var caught = new List<Enemy>();
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || enemy == skip || !enemy.Alive) {
                continue;
            }
            Vector3 flat = enemy.transform.position - point;
            flat.y *= 0.5f;
            float reach = radius + enemy.BodyScale * 0.3f;
            if (flat.sqrMagnitude <= reach * reach) {
                caught.Add(enemy);
            }
        }
        bool phoenix = RunBoons.Has(BoonId.Phoenix);
        for (int i = 0; i < caught.Count; i++) {
            if (phoenix) {
                caught[i].Ignite(ArrowBase() * 0.5f, 3f);
            }
            caught[i].Damage(damage, kind, false);
        }
    }

    public static void OnKill(Enemy foe) {
        killCount++;
        Combo.Kill();
        RunLog.NoteKill(foe.Kind);
        SliceVoice.OnKill(foe);
        Vector3 at = foe.transform.position;

        Pickups.DropBolts(at, Tuning.BoltsFor(foe.Kind, foe.Elite || foe.Boss) * (foe.Boss ? 5 : 1));
        if (foe.Golden || foe.Boss) {
            Pickups.DropChest(at);
            MetaProgress.AddCoins(foe.Boss ? Tuning.CoinsForBoss : Tuning.CoinsForGolden, at);
            Juice.HitStop(0.12f);
            UpgradeLook.Say(foe.Golden ? "Golden Yo Bot down!" : foe.Title + " falls!", 2.4f, 120f);
        } else if (foe.Elite && Random.value < 0.08f) {
            Pickups.DropChest(at);
        }

        int every = RunBoons.BountyEvery;
        if (every > 0) {
            bountyCount++;
            if (bountyCount >= every) {
                bountyCount = 0;
                MetaProgress.AddCoins(1, at);
            }
        }

        int bomb = RunBoons.Rank(BoonId.BirdBomb);
        bool phoenix = RunBoons.Has(BoonId.Phoenix);
        if ((bomb > 0 || (phoenix && foe.Burning)) && bombDepth < 3) {
            bombDepth++;
            float radius = 2.4f + bomb * 0.8f + foe.BodyScale * 0.25f;
            float damage = ArrowBase() * (0.6f + 0.3f * bomb) + foe.HitsToDrop * 0.1f;
            if (phoenix) {
                damage *= 1.5f;
                radius *= 1.3f;
            }
            UpgradeLook.Blast(at + Vector3.up * 0.4f, radius, phoenix ? new Color(1f, 0.4f, 0.1f) : new Color(1f, 0.7f, 0.3f));
            Burst(at, radius, damage, DamageKind.Blast, foe);
            bombDepth--;
        }

        int fire = RunBoons.Rank(BoonId.Firebrand);
        if (fire >= 3 && foe.Burning) {
            List<Enemy> near = new List<Enemy> { foe };
            for (int i = 0; i < 2; i++) {
                Enemy next = Nearest(at, 6f, near);
                if (next == null) {
                    break;
                }
                near.Add(next);
                next.Ignite(ArrowBase() * 0.5f, 3f);
            }
        }
    }
}
