using System.Collections.Generic;
using UnityEngine;

// How a Campaign chapter is paced (J: "too much fighting"). Nothing in a chapter spawns on a timer. A chapter has at most
// three ENCOUNTERS and then its boss, and they come in this order:
//   1. a quiet opening: Wallit walks (explore), the opening scene plays (talk) and he takes the chapter's Receipt (pickup);
//   2. each encounter is placed, asleep (Enemy.Dormant), at a fixed spot: on the route in the chapters that walk one
//      (Chapters 2, 3, 4), or out from the circle in the ones that hold it (5, 6). It wakes when Wallit walks up to it, or
//      when a blow lands on one of its bots. A route encounter he walks past is skipped;
//   3. between encounters there is a breath (Quiet seconds) before the next one is even placed.
// The boss is called when the encounters are down, as before. This class also counts the seconds spent in combat, so the
// playtest can report each chapter's combat share (Summary). Chapter 1's own level (LevelYard) is placed the same way.
// Arcade has no ChapterRun, so none of this exists there.
public class ChapterPacing {
    public const float ExploreMetres = 30f;
    public const float WakeRadius = 15f;
    public const float Quiet = 10f;
    public const float PickupRadius = 3.6f;

    public class Fight {
        public int Count;            // bots in it (the chapter's mini-boss, if it carries one, is extra)
        public bool Mini;            // the chapter's mini-boss wakes with this one
        public bool Route;           // placed on the way out (Leg == RouteLeg), else out from the circle once Wallit is home
        public int RouteLeg;
        public int Home;             // which home encounter this is (sets its bearing)
        public int State;            // 0 waiting, 1 placed and asleep, 2 awake, 3 down, 4 skipped
        public Vector3 At;
        public float WokeAt;
        public readonly List<Enemy> Bots = new List<Enemy>();
    }

    public readonly List<Fight> Fights = new List<Fight>();
    public bool Explored { get; private set; }
    public bool Spoke { get; private set; }
    public bool Took { get; private set; }
    public bool OpeningDone { get; private set; }
    public float Walked { get; private set; }
    public float Seconds { get; private set; }
    public float CombatSeconds { get; private set; }
    public float FirstAttackAt { get; private set; } = -1f;
    public float OpeningAt { get; private set; } = -1f;
    public bool ReceiptPending { get { return receipt != null; } }
    public Vector3 ReceiptAt { get { return receiptAt; } }
    public float CombatShare { get { return Seconds > 0f ? CombatSeconds / Seconds : 0f; } }
    public bool AllDone {
        get {
            for (int i = 0; i < Fights.Count; i++) {
                if (Fights[i].State < 3) {
                    return false;
                }
            }
            return true;
        }
    }
    public int Woken {
        get {
            int n = 0;
            for (int i = 0; i < Fights.Count; i++) {
                n += Fights[i].State >= 2 && Fights[i].State != 4 ? 1 : 0;
            }
            return n;
        }
    }

    readonly ChapterRun run;
    readonly Story.Chapter data;
    GameObject receipt;
    Transform receiptSpin;
    Vector3 receiptAt;
    Vector3 legStart;
    Vector3 last;
    bool hasLast;
    float quietUntil;
    float nextRing;

    public ChapterPacing(ChapterRun run, Story.Chapter data) {
        this.run = run;
        this.data = data;
        Vector3 circle = Circle();
        legStart = circle;
        Plan();
        PlaceReceipt(circle);
        ChapterRun.Events.Add("pacing-plan|" + data.Id + "|" + Fights.Count + " encounters + boss|" + Fights.Count + " placed asleep, no timed spawns");
    }

    static Vector3 Circle() {
        return Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
    }

    static float Flat(Vector3 v) {
        v.y = 0f;
        return v.magnitude;
    }

    void Plan() {
        int size = run.Scaled(data.WaveSize);
        int total = run.Target();
        int stops = data.Stops != null ? data.Stops.Length : 0;
        if (data.Objective == Story.ChapterObjective.GateAndBack) {
            Fights.Add(new Fight { Count = size, Route = true, RouteLeg = 0 });
            Split(total, 2, 1);
        } else if (data.Objective == Story.ChapterObjective.FloorRun) {
            Fights.Add(new Fight { Count = Mathf.Max(3, size / 2), Route = true, RouteLeg = Mathf.Min(1, Mathf.Max(0, stops - 1)) });
            if (data.MiniName != null) {
                Fights.Add(new Fight { Count = 0, Mini = true, Route = true, RouteLeg = Mathf.Min(2, Mathf.Max(0, stops - 1)) });
            }
            Fights.Add(new Fight { Count = total, Home = 0 });
        } else {
            Split(total, data.Final || data.Waves >= 3 ? 3 : 2, 1);
        }
    }

    // Home encounters: the bots of the chapter's count spread over n fights, the mini-boss in fight index miniAt.
    void Split(int total, int n, int miniAt) {
        int left = total;
        for (int i = 0; i < n; i++) {
            int count = i == n - 1 ? left : total / n + (i < total % n ? 1 : 0);
            left -= count;
            Fights.Add(new Fight { Count = count, Home = i, Mini = data.MiniName != null && i == Mathf.Min(miniAt, n - 1) });
        }
    }

    // The Receipt: one visible pickup in the quiet opening, on the route toward the first place or out from the circle.
    void PlaceReceipt(Vector3 circle) {
        Vector3 at;
        bool legs = data.Objective == Story.ChapterObjective.GateAndBack || data.Objective == Story.ChapterObjective.FloorRun;
        if (legs && run.HasMarker) {
            Vector3 to = run.Marker - circle;
            to.y = 0f;
            float far = Mathf.Max(20f, to.magnitude * 0.3f);
            at = circle + to.normalized * far;
        } else {
            float a = (data.Bearing + 220f) * Mathf.Deg2Rad;
            at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 24f;
        }
        at.y = circle.y;
        if (Physics.Raycast(new Vector3(at.x, 60f, at.z), Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        receiptAt = at;
        receipt = new GameObject("Receipt");
        receipt.transform.position = at;
        receiptSpin = UpgradeLook.BuildBolt(receipt.transform, 1.1f);
        receiptSpin.localPosition = new Vector3(0f, 0.7f, 0f);
    }

    public void Talked(string what) {
        if (Spoke) {
            return;
        }
        Spoke = true;
        ChapterRun.Events.Add("talk|" + what + "|" + Mathf.RoundToInt(Seconds) + " s");
    }

    public void PickedUp(string what) {
        if (Took) {
            return;
        }
        Took = true;
        ChapterRun.Events.Add("pickup|" + what + "|" + Mathf.RoundToInt(Seconds) + " s");
    }

    // The chapter's leg changed (Wallit reached a stop): route encounters of the next leg are measured from here.
    public void LegChanged(Vector3 from) {
        legStart = from;
    }

    public void Tick(float dt, Transform player) {
        if (player == null) {
            return;
        }
        Seconds += dt;
        Vector3 pp = player.position;
        if (hasLast) {
            float step = Flat(pp - last);
            if (step < 4f) {
                Walked += step;
            }
        }
        last = pp;
        hasLast = true;
        if (!Explored && Walked >= ExploreMetres) {
            Explored = true;
            ChapterRun.Events.Add("explore|" + Mathf.RoundToInt(Walked) + " m walked|" + Mathf.RoundToInt(Seconds) + " s");
        }
        TickReceipt(pp);
        if (!OpeningDone && Explored && Spoke && Took) {
            OpeningDone = true;
            OpeningAt = Seconds;
            quietUntil = Seconds + 2f;
            ChapterRun.Events.Add("opening-done|explore, talk, pickup before any attack|" + Mathf.RoundToInt(Seconds) + " s");
        }
        if (AnyAwake(pp)) {
            CombatSeconds += dt;
        }
        TickFights(pp);
    }

    static bool AnyAwake(Vector3 pp) {
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy e = Enemy.Roster[i];
            if (e != null && e.Alive && !e.Dormant && Flat(e.transform.position - pp) < 50f) {
                return true;
            }
        }
        return false;
    }

    void TickReceipt(Vector3 pp) {
        if (receipt == null) {
            return;
        }
        receiptSpin.Rotate(0f, 90f * Time.deltaTime, 0f);
        float d = Flat(receiptAt - pp);
        if (d <= PickupRadius) {
            Object.Destroy(receipt);
            receipt = null;
            GameAudio.Play("coin", 0.7f, 1.2f);
            Fx.Ring(receiptAt + Vector3.up * 0.2f, 6f, new Color(1f, 0.88f, 0.5f, 0.8f), 0.8f);
            CampaignProgress.Grant(20);
            PickedUp("Receipt");
            ChapterRun.Say("W|Receipt. Proof I was here.", 3.2f);
        } else if (d <= 24f && Time.time >= nextRing) {
            nextRing = Time.time + 1.6f;
            Fx.Ring(receiptAt + Vector3.up * 0.2f, 3f, new Color(1f, 0.84f, 0.4f, 0.55f), 1.2f);
        }
    }

    Fight Current {
        get {
            for (int i = 0; i < Fights.Count; i++) {
                if (Fights[i].State < 3) {
                    return Fights[i];
                }
            }
            return null;
        }
    }

    void TickFights(Vector3 pp) {
        for (int i = 0; i < Fights.Count; i++) {
            Fight f = Fights[i];
            if (f.Route && f.State < 2 && run.Leg > f.RouteLeg) {
                Skip(i, f);
            }
        }
        Fight cur = Current;
        if (cur == null) {
            return;
        }
        int index = Fights.IndexOf(cur);
        if (cur.State == 0) {
            bool phase = cur.Route ? run.Leg == cur.RouteLeg : run.AtHome;
            if (OpeningDone && phase && Seconds >= quietUntil && !run.BossCalled) {
                Place(index, cur);
            }
        } else if (cur.State == 1) {
            bool hit = false;
            for (int i = 0; i < cur.Bots.Count; i++) {
                hit |= cur.Bots[i] != null && cur.Bots[i].Alive && !cur.Bots[i].Dormant;
            }
            if (hit || Flat(cur.At - pp) <= WakeRadius) {
                Wake(index, cur, hit ? "struck" : "Wallit walked up");
            } else if (Time.time >= nextRing) {
                nextRing = Time.time + 1.5f;
                Fx.Ring(cur.At + Vector3.up * 0.2f, 7f, new Color(0.9f, 0.4f, 0.35f, 0.5f), 1.3f);
            }
        } else if (cur.State == 2) {
            bool down = true;
            for (int i = 0; i < cur.Bots.Count; i++) {
                down &= cur.Bots[i] == null || !cur.Bots[i].Alive;
            }
            if (down) {
                cur.State = 3;
                quietUntil = Seconds + Quiet;
                ChapterRun.Events.Add("encounter-clear|" + (index + 1) + "|" + Mathf.RoundToInt(Seconds - cur.WokeAt) + " s fought|then " + Quiet + " s of quiet");
            }
        }
    }

    void Place(int index, Fight f) {
        Vector3 circle = Circle();
        if (f.Route) {
            Vector3 to = run.Marker - legStart;
            to.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * 5f;
            f.At = legStart + to * 0.72f + side;
        } else {
            float a = (data.Bearing + 40f + f.Home * 115f) * Mathf.Deg2Rad;
            f.At = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 26f;
        }
        for (int i = 0; i < f.Count; i++) {
            float a = i * Mathf.PI * 2f / Mathf.Max(1, f.Count);
            Enemy bot = run.PlaceCrew(f.At + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (f.Count > 1 ? 3.2f : 0f));
            if (bot != null) {
                f.Bots.Add(bot);
            }
        }
        f.State = 1;
        ChapterRun.Events.Add("encounter-place|" + (index + 1) + "|" + f.Count + " bots" + (f.Mini ? " + " + data.MiniName : "") + " asleep|" + (f.Route ? "on the route" : "out from the circle"));
    }

    void Wake(int index, Fight f, string cause) {
        f.State = 2;
        f.WokeAt = Seconds;
        if (FirstAttackAt < 0f) {
            FirstAttackAt = Seconds;
            ChapterRun.Events.Add("first-attack|" + Mathf.RoundToInt(Seconds) + " s into the chapter, after the opening (" + Mathf.RoundToInt(OpeningAt) + " s)");
        }
        GameAudio.Play("yobot_wake", 0.8f, 1f + 0.05f * (index % 3));
        for (int i = 0; i < f.Bots.Count; i++) {
            if (f.Bots[i] != null) {
                f.Bots[i].Dormant = false;
            }
        }
        if (f.Mini) {
            Enemy mini = run.WakeMini(f.At);
            if (mini != null) {
                f.Bots.Add(mini);
            }
        }
        ChapterRun.Events.Add("encounter-wake|" + (index + 1) + "|" + f.Bots.Count + " bots (" + cause + ")|" + Mathf.RoundToInt(Seconds) + " s");
        if (index == 0) {
            ChapterRun.Say("Y|Hello! Uploading victory! Striking this address now!", 3f);
        }
    }

    void Skip(int index, Fight f) {
        f.State = 4;
        for (int i = 0; i < f.Bots.Count; i++) {
            if (f.Bots[i] != null) {
                Object.Destroy(f.Bots[i].gameObject);
            }
        }
        f.Bots.Clear();
        if (f.Mini) {
            // The chapter's mini-boss must still be met before the boss: it joins the next encounter.
            for (int j = index + 1; j < Fights.Count; j++) {
                if (Fights[j].State == 0) {
                    Fights[j].Mini = true;
                    break;
                }
            }
        }
        ChapterRun.Events.Add("encounter-skipped|" + (index + 1) + "|Wallit went past, the bots stay asleep and are put away");
    }

    // The objective line while the chapter's hold is waiting for Wallit: look round first, then walk out to the next place.
    public string Line() {
        if (!run.AtHome || run.BossCalled) {
            return null;
        }
        if (!OpeningDone) {
            string todo = "";
            if (!Explored) {
                todo += "walk " + Mathf.CeilToInt(ExploreMetres - Walked) + " m more, ";
            }
            if (!Spoke) {
                todo += "hear the scene out, ";
            }
            if (!Took) {
                todo += "take the receipt (" + Mathf.RoundToInt(Flat(receiptAt - last)) + " m), ";
            }
            return "Quiet first. Look round: " + todo.TrimEnd(',', ' ');
        }
        Fight cur = Current;
        if (cur != null && cur.State <= 1) {
            if (cur.State == 0) {
                return "A breath. The next delivery is being placed";
            }
            return "Walk out to the delivery (" + (Fights.IndexOf(cur) + 1) + " of " + Fights.Count + "):  " + Mathf.RoundToInt(Flat(cur.At - last)) + " m";
        }
        return null;
    }

    public string Summary() {
        return "pacing|" + data.Id + "|encounters=" + Fights.Count + "|woken=" + Woken + "|combatSeconds=" + Mathf.RoundToInt(CombatSeconds)
            + "|playSeconds=" + Mathf.RoundToInt(Seconds) + "|combatShare=" + Mathf.RoundToInt(CombatShare * 100f) + "%"
            + "|firstAttack=" + Mathf.RoundToInt(FirstAttackAt) + "|opening=" + Mathf.RoundToInt(OpeningAt);
    }

    public void Dispose() {
        if (receipt != null) {
            Object.Destroy(receipt);
        }
    }
}
