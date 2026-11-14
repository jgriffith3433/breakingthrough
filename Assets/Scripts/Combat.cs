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
    static int killCoinCount;
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
        killCoinCount = 0;
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
        return raw * RunBoons.DamageMult * CampaignProgress.DamageMult;
    }

    // One bat hit's base damage, before the swing's own multiplier.
    public static float BatBase() {
        return ArrowBase();
    }

    // The bat strikes a bot. Scale is the swing's multiplier (finisher, smash, home run, a bot knocked into
    // another). Power is how hard a bot it drops is flung. Tip: it met the bat's sweet spot. True when this blow
    // dropped the bot.
    public static int BatHitsLanded { get; private set; }   // bat blows that reached Enemy.Damage (the hit-fx playtest: one flash each)

    public static bool BatHit(Enemy foe, float scale, float power, bool allowChain, bool tip = false) {
        return Strike(foe, power, scale, allowChain, DamageKind.Bat, tip);
    }

    // A hit with every boon on it: crits, Called Shot, executes, Pine Tar, Ice Bat and Live Wire.
    static bool Strike(Enemy foe, float power, float scale, bool allowChain, DamageKind kind, bool tip) {
        if (foe == null || !foe.Alive) {
            return false;
        }
        float amount = ArrowBase() * scale;
        Vector3 at = foe.transform.position;
        int heavyRank = RunBoons.Rank(BoonId.Heavy);
        if (heavyRank > 0 && kind == DamageKind.Bat) {
            BoonFx.Proc(BoonId.Heavy, at + Vector3.up * 0.95f, "+" + (heavyRank * 25) + "% DMG", 2.1f);
        }
        ZeldaStyleCamera cam = Juice.Cam;
        int callRank = RunBoons.Rank(BoonId.Mark);
        bool called = callRank > 0 && ((cam != null && cam.LockedOnGameObject == foe.gameObject)
            || ((cam == null || cam.LockedOnGameObject == null) && BatEquipment.CurrentTarget == foe));
        if (called) {
            amount *= RunBoons.MarkMult;
            BoonFx.Proc(BoonId.Mark, at + Vector3.up * 1.05f, "CALLED SHOT", 2.2f);
        }
        int sweetRank = RunBoons.Rank(BoonId.EagleEye);
        float tipCrit = tip ? RunBoons.TipCrit : (sweetRank > 0 ? 0.08f * sweetRank : 0f);
        bool crit = Random.value < RunBoons.CritChance + tipCrit + CampaignProgress.CritBonus;
        if (crit) {
            amount *= RunBoons.CritMult;
            if (tipCrit > 0f) {
                SweetSpots++;
            }
        }
        if (sweetRank > 0 && (tip || crit)) {
            if (!crit) {
                SweetSpots++;
            }
            BoonFx.Proc(BoonId.EagleEye, at + Vector3.up * 1.15f, crit ? "SWEET CRIT!" : "SWEET SPOT", 2.4f);
        }
        float execute = RunBoons.ExecuteBelow;
        bool executed = false;
        if (execute > 0f && !foe.Boss) {
            float postHp01 = foe.Health01 - amount / Mathf.Max(1f, foe.HitsToDrop);
            if (postHp01 < execute || foe.Health01 <= execute + 0.35f) {
                if (postHp01 < execute) {
                    amount = Mathf.Max(amount, foe.HitsToDrop * foe.Health01 + 1f);
                    crit = true;
                }
                executed = true;
            }
        }
        if (executed) {
            UpgradeLook.Zap(at + new Vector3(-0.6f, 1.2f, 0f), at + new Vector3(0.6f, 0.3f, 0f), RunBoons.FamilyColor(BoonFamily.Tricks), 0.12f, false);
            BoonFx.Proc(BoonId.Executioner, at + Vector3.up * 1.15f, "EXECUTED!", 2.5f);
        }
        RunLog.NoteHit(amount);
        if (kind == DamageKind.Bat) {
            BatHitsLanded++;
        }
        bool dropped = foe.Damage(amount, kind, crit, -1f, power);
        if (crit && dropped && (foe.BodyScale > 3f || foe.Elite)) {
            Juice.HitStop(0.05f);
        }
        if (crit && tipCrit > 0f) {
            Fx.Ring(at + Vector3.up * 0.2f, 1.6f * Mathf.Sqrt(Mathf.Max(1f, foe.BodyScale)), RunBoons.FamilyColor(BoonFamily.Impact), 0.3f);
        }

        // Called Shot: the locked bot pays out extra bolts when it falls.
        if (called && dropped && callRank > 0) {
            int extra = Mathf.Max(callRank, Mathf.CeilToInt(Tuning.BoltsFor(foe.Kind, foe.Elite || foe.Boss) * 0.25f * callRank));
            Pickups.DropBolts(at, extra);
            CalledBolts += extra;
        }

        int fire = RunBoons.Rank(BoonId.Firebrand);
        if (fire > 0) {
            if (!dropped) {
                foe.Ignite(ArrowBase() * (0.25f + 0.15f * fire), 3f);
            }
            Burned++;
            BoonFx.Proc(BoonId.Firebrand, at + Vector3.up * 0.85f, "PINE TAR", 2.2f);
        }
        int frost = RunBoons.Rank(BoonId.Frost);
        if (frost > 0) {
            bool freeze = frost >= 3 && Random.value < 0.25f;
            if (!dropped) {
                foe.Chill(0.25f * frost, 2f, freeze ? 1.1f : 0f);
            }
            Slowed++;
            BoonFx.Proc(BoonId.Frost, at + Vector3.up * 0.85f, freeze ? "FROZEN!" : "ICE BAT", freeze ? 2.8f : 2.2f);
        }
        if (allowChain) {
            bool storm = RunBoons.Has(BoonId.StormQuiver);
            int chain = RunBoons.Rank(BoonId.Chain);
            if (storm || (chain > 0 && Random.value < 0.22f * chain)) {
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
        BoonFx.Proc(BoonId.Hailstorm, foe.transform.position + Vector3.up * 0.8f, freeze ? "BLIZZARD FREEZE" : "BLIZZARD", 2.4f);
    }

    // Live Wire: lightning jumps bot to bot, and each one it reaches shorts out for a moment.
    public static void Lightning(Vector3 from, Enemy skip, int jumps, float damage) {
        bool storm = RunBoons.Has(BoonId.StormQuiver);
        float shortFor = 0.35f + 0.2f * RunBoons.Rank(BoonId.Chain) + (storm ? 0.3f : 0f);
        var struck = new List<Enemy>();
        if (skip != null) {
            struck.Add(skip);
        }
        Color elemCol = RunBoons.FamilyColor(BoonFamily.Element);
        Vector3 cursor = from;
        for (int j = 0; j < jumps; j++) {
            Enemy next = Nearest(cursor, 12f, struck);
            if (next == null) {
                break;
            }
            struck.Add(next);
            Vector3 to = next.transform.position + Vector3.up * 0.6f * Mathf.Max(1f, next.BodyScale);
            UpgradeLook.Zap(cursor, to, elemCol, 0.15f, true);
            Arcs++;
            BoonFx.Proc(storm ? BoonId.StormQuiver : BoonId.Chain, to, storm ? "THUNDER BAT" : "LIVE WIRE", 2.2f);
            if (!next.Damage(damage, DamageKind.Lightning, false) && next != null) {
                next.Short(shortFor);
                Shorted++;
            }
            if (RunBoons.Has(BoonId.Hailstorm)) {
                next.Chill(0.5f, 1.5f, 0f);
            }
            cursor = to;
        }
        if (struck.Count <= (skip != null ? 1 : 0) && skip != null) {
            UpgradeLook.Zap(from + Vector3.up * 2.2f, from, elemCol, 0.14f, true);
            Arcs++;
            BoonFx.Proc(storm ? BoonId.StormQuiver : BoonId.Chain, from, storm ? "THUNDER BAT" : "LIVE WIRE", 2.1f);
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
        CampaignProgress.OnKill(foe);
        Combo.Kill();
        RunLog.NoteKill(foe.Kind);
        SliceVoice.OnKill(foe);
        WallitVoice.OnKill(foe, Combo.Count);
        Vector3 at = foe.transform.position;

        Pickups.DropBolts(at, Tuning.BoltsFor(foe.Kind, foe.Elite || foe.Boss) * (foe.Boss ? 5 : 1));
        Juice.HitStop(Tuning.KillHitStop);
        if (!FirstMinute.SuccessSeen && ChapterRun.Instance == null) {
            // The first kill of an Arcade run pays off: a longer hit-stop, a fuller bolt burst, a soft ring and Wallit's word.
            FirstMinute.NoteKill();
            Pickups.DropBolts(at, Tuning.FirstKillBonusBolts);
            Juice.HitStop(Tuning.FirstKillHitStop);
            Fx.Ring(at + Vector3.up * 0.1f, 5f, new Color(1f, 0.9f, 0.5f, 0.8f), 0.5f);
            UpgradeLook.Say("Pay up.", 1.6f, 200f);
        }
        killCoinCount++;
        if (!foe.Golden && !foe.Boss && ChapterRun.Instance == null
            && (WaveDirector.Wave <= Tuning.CoinKillWaves || killCoinCount % Tuning.CoinKillEvery == 0)) {
            MetaProgress.AddCoins(1, at);
        }
        if (foe.Golden || foe.Boss) {
            Pickups.DropChest(at);
            MetaProgress.AddCoins(foe.Boss ? Tuning.CoinsForBoss : Tuning.CoinsForGolden, at);
            Juice.HitStop(0.12f);
            if (foe.Boss) {
                BossSetPiece.Defeated(foe, WaveDirector.Wave);
            }
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
                BoonFx.Proc(BoonId.Bounty, at + Vector3.up * 0.95f, "+1 BOUNTY COIN!", 2.8f);
            } else if (bountyCount == 1 || bountyCount % 5 == 0) {
                BoonFx.Proc(BoonId.Bounty, at + Vector3.up * 0.8f, "BOUNTY " + bountyCount + "/" + every, 1.9f);
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
            Color blastCol = phoenix ? RunBoons.FamilyColor(BoonFamily.Element) : RunBoons.FamilyColor(BoonFamily.Tricks);
            UpgradeLook.Blast(at + Vector3.up * 0.4f, radius, blastCol);
            BoonFx.Proc(phoenix ? BoonId.Phoenix : BoonId.BirdBomb, at + Vector3.up * 0.7f, phoenix ? "PHOENIX BLAST!" : "POWDER KEG!", radius * 0.85f);
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
                UpgradeLook.Zap(at + Vector3.up * 0.6f, next.transform.position + Vector3.up * 0.6f, RunBoons.FamilyColor(BoonFamily.Element), 0.12f, true);
                BoonFx.Proc(BoonId.Firebrand, next.transform.position + Vector3.up * 0.8f, "TAR SPREAD", 2.2f);
            }
        }
    }
}
