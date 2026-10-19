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

    public static void BeginRun() {
        killCount = 0;
        bountyCount = 0;
        bombDepth = 0;
    }

    public static float ArrowBase() {
        float raw = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.ArrowDamage : 1f;
        return raw * RunBoons.DamageMult;
    }

    // One bat hit's base damage, before the swing's own multiplier.
    public static float BatBase() {
        return ArrowBase();
    }

    // The bat strikes a bird. Scale is the swing's multiplier (finisher, smash, home run, a bird knocked into
    // another). Power is how hard a bird it drops is flung. True when this blow dropped the bird.
    public static bool BatHit(Enemy foe, float scale, float power, bool allowChain) {
        return Strike(foe, power, scale, allowChain, DamageKind.Bat);
    }

    // A hit with every boon on it: crits, marks, executes, fire, frost and lightning.
    static bool Strike(Enemy foe, float power, float scale, bool allowChain, DamageKind kind) {
        if (foe == null || !foe.Alive) {
            return false;
        }
        float amount = ArrowBase() * scale;
        ZeldaStyleCamera cam = Juice.Cam;
        if (cam != null && cam.LockedOnGameObject == foe.gameObject) {
            amount *= RunBoons.MarkMult;
        }
        bool crit = Random.value < RunBoons.CritChance;
        if (crit) {
            amount *= RunBoons.CritMult;
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

        int fire = RunBoons.Rank(BoonId.Firebrand);
        if (fire > 0 && !dropped) {
            foe.Ignite(ArrowBase() * (0.25f + 0.15f * fire), 3f);
        }
        int frost = RunBoons.Rank(BoonId.Frost);
        if (frost > 0 && !dropped) {
            foe.Chill(0.25f * frost, 2f, frost >= 3 && Random.value < 0.2f ? 1.1f : 0f);
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

    public static void Lightning(Vector3 from, Enemy skip, int jumps, float damage) {
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
            next.Damage(damage, DamageKind.Lightning, false);
            if (RunBoons.Rank(BoonId.Frost) >= 3 && RunBoons.Has(BoonId.Hailstorm)) {
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

    // Splash damage around a point. Used by rockets, bombs, encore, second wind.
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
