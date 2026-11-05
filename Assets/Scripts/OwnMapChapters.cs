using System.Collections.Generic;
using UnityEngine;

// The three chapter maps that have their own scenes: the Moonlit Hollow (chapter 1), the edge of the Hollow (chapter 2) and
// the Clearing Floor (chapter 3). Each is built from nothing but its own code in its own scene (OwnMap.cs says how), and each
// differs from the others in terrain shape, sky, palette, props, route, hazard and set piece. Docs/CampaignBible.md has the
// map design sheets these follow.

// ---------------------------------------------------------------------------------------------- Chapter 1: the Moonlit Hollow
// A grassy bowl under a cold moon, hills rising round it, dead pines on the ridge. A pale stepping-stone trail runs to the
// Fallen Mailbox (landmark) with its red RETURN TO SENDER stamp; nine standing stones ring the circle; the Mist Bog to the west
// pulses a cold surge (hazard). Set piece, The Notice: the mailbox flag swings up and its lamp burns red each time the Courier
// winds up a notice (Courier.Winds).
public class HollowMap : OwnMap {
    public override string Key { get { return "hollow"; } }
    const float Trail = 20f;
    Transform flag;
    Light flagLamp;
    float flagUp, lampBoost;
    int seenWinds;
    float nextSurge = 5f;
    Vector3 bogAt;
    bool forced;

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        Terrain("Hollow Hills",
            (deg, d) => Mathf.SmoothStep(0f, 1f, (d - 215f) / 130f) * (14f + 26f * Noise(deg * 3f, d, 1f)) + 2.5f * Noise(deg * 5f, d * 0.8f, 2f) * Mathf.SmoothStep(0f, 1f, (d - 205f) / 20f),
            (deg, d, h) => Color.Lerp(C(0.06f, 0.11f, 0.12f), C(0.2f, 0.27f, 0.34f), Mathf.Clamp01(h / 30f) + 0.15f * Noise(deg * 7f, d, 3f)));
        SetSky(C(0.03f, 0.05f, 0.12f), C(0.15f, 0.21f, 0.35f), C(0.03f, 0.04f, 0.07f), C(0.85f, 0.9f, 1f), new Vector3(-0.35f, 0.42f, 0.83f), 0.014f, 0.5f);
        MakeMusic(12f, new[] { 110f, 164.8f, 220f, 261.6f }, 2f, 0f, 0f, 6f, 659.3f, 0.3f);

        // Dead pines along the ridge.
        Transform pines = Group("Dead Pines");
        var rng = new System.Random(11);
        for (int i = 0; i < 70; i++) {
            float b = (float)rng.NextDouble() * 360f;
            float d = 240f + (float)rng.NextDouble() * 170f;
            float h = 6f + (float)rng.NextDouble() * 10f;
            Vector3 p = Polar(b, d);
            p.y = Mathf.SmoothStep(0f, 1f, (d - 215f) / 130f) * 18f;
            Piece(pines, "Pine Trunk", p + Vector3.up * h * 0.4f, new Vector3(0.8f, h * 0.8f, 0.8f), C(0.06f, 0.05f, 0.08f), 0f, 0f, Shape.Cylinder);
            Piece(pines, "Pine Crown", p + Vector3.up * (h * 0.8f + 2f), new Vector3(4.5f, h * 0.7f, 4.5f), C(0.05f, 0.09f, 0.11f), 0f, 0f, Shape.Cone);
        }

        Transform route = Group("Stepping-stone Trail");
        int n = 0;
        for (float d = 45f; d <= 150f; d += 5f, n++) {
            float side = Mathf.Sin(n * 2.3f) * 0.9f;
            Piece(route, "Stepping Stone " + n, Polar(Trail, d, 0.09f) + Quaternion.Euler(0f, Trail, 0f) * new Vector3(side, 0f, 0f), new Vector3(2.2f + (n % 3) * 0.3f, 0.2f, 2.2f), n % 2 == 0 ? C(0.62f, 0.68f, 0.82f) : C(0.5f, 0.56f, 0.7f), n * 37f, 0f, Shape.Cylinder);
        }
        Features.Add("route:stepping-stone trail from the circle out to the mailbox, bearing 20");

        Transform stones = Group("Standing Stones");
        for (int i = 0; i < 9; i++) {
            float b = i * 40f + 10f;
            Piece(stones, "Standing Stone " + i, Polar(b, 62f, 3.5f), new Vector3(2.2f, 7f + (i % 3) * 1.5f, 1.6f), C(0.3f, 0.34f, 0.46f), b, i % 2 == 0 ? 4f : -3f);
        }
        Features.Add("landmark:ring of nine standing stones round the circle");

        Transform box = Group("Fallen Mailbox");
        Vector3 at = Polar(Trail, 125f);
        Color wood = C(0.12f, 0.1f, 0.12f), red = C(0.85f, 0.18f, 0.2f);
        Piece(box, "Mailbox Post", at + Vector3.up * 5f, new Vector3(1.2f, 10f, 1.2f), wood, Trail, 12f);
        Piece(box, "Mailbox Body", at + new Vector3(0f, 10.4f, 0f) + Quaternion.Euler(0f, Trail, 0f) * new Vector3(1.8f, 0f, 0f), new Vector3(6f, 3.6f, 3.6f), C(0.45f, 0.5f, 0.62f), Trail, 12f);
        flag = Piece(box, "Mailbox Flag", at + new Vector3(0f, 12.6f, 0f), new Vector3(0.4f, 3f, 1.8f), red, Trail, 12f, Shape.Box, true).transform;
        Piece(box, "Stamp Slab", at + Quaternion.Euler(0f, Trail, 0f) * new Vector3(0f, 0.3f, -9f), new Vector3(9f, 0.12f, 4f), red, Trail);
        flagLamp = Lamp(box, at + new Vector3(0f, 9f, 0f), C(1f, 0.2f, 0.2f), 26f, 0.5f);
        Features.Add("landmark:the Fallen Mailbox with its red RETURN TO SENDER stamp, 125 m out on the trail");

        Transform mire = Group("Mist Bog");
        bogAt = Polar(285f, 95f);
        Piece(mire, "Bog", bogAt + Vector3.up * 0.07f, new Vector3(46f, 0.03f, 46f), C(0.08f, 0.17f, 0.2f), 0f, 0f, Shape.Cylinder);
        Piece(mire, "Bog Rim", bogAt + Vector3.up * 0.05f, new Vector3(52f, 0.03f, 52f), C(0.05f, 0.07f, 0.1f), 0f, 0f, Shape.Cylinder);
        for (int i = 0; i < 8; i++) {
            float a = i * 45f * Mathf.Deg2Rad;
            Vector3 p = bogAt + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (9f + (i % 3) * 4f);
            Piece(mire, "Dead Trunk " + i, p + Vector3.up * (4f + i % 2), new Vector3(1f, 8f + i % 2 * 2f, 1f), wood, 0f, (i - 4) * 3f, Shape.Cylinder);
            Piece(mire, "Mist Pool " + i, bogAt + new Vector3(Mathf.Sin(a + 0.4f), 0f, Mathf.Cos(a + 0.4f)) * 16f + Vector3.up * 0.2f, new Vector3(7f, 0.2f, 7f), C(0.35f, 0.45f, 0.5f), 0f, 0f, Shape.Cylinder);
        }
        Features.Add("hazard:the Mist Bog, a dark sink of dead trunks 95 m out to the west that surges cold every few seconds (Wallit inside it flinches)");
        Features.Add("setpiece:The Notice: the mailbox flag swings up and its lamp burns red whenever the Courier winds up a notice");
        seenWinds = Courier.Winds;
    }

    public override void Trigger() {
        forced = true;
    }

    void Update() {
        if (flag == null) {
            return;
        }
        if (Courier.Winds < seenWinds) {
            seenWinds = Courier.Winds;
        }
        if (forced || Courier.Winds > seenWinds) {
            seenWinds = Courier.Winds;
            forced = false;
            lampBoost = 3.2f;
            Beat("notice|flag-up|" + seenWinds);
            Fx.Ring(flag.position - Vector3.up * 9f, 18f, new Color(1f, 0.25f, 0.25f, 0.8f), 1.1f);
        }
        lampBoost = Mathf.MoveTowards(lampBoost, 0f, Time.deltaTime);
        flagUp = Mathf.MoveTowards(flagUp, lampBoost > 0f ? 1f : 0f, Time.deltaTime * 1.5f);
        flag.localRotation = Quaternion.Euler(0f, Trail, 12f + flagUp * 70f);
        flagLamp.intensity = 0.5f + lampBoost * 2.2f;
        if (Time.time >= nextSurge) {
            nextSurge = Time.time + 6.5f;
            Fx.Ring(bogAt + Vector3.up * 0.2f, 46f, new Color(0.55f, 0.75f, 0.85f, 0.7f), 1.4f);
            PlayerGrowth p = PlayerGrowth.Instance;
            if (p != null) {
                Vector3 d = p.transform.position - bogAt;
                d.y = 0f;
                if (d.magnitude <= 23f) {
                    p.OnHit();
                    Log.Add("bog-surge|wallit");
                } else {
                    Log.Add("bog-surge");
                }
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------- Chapter 2: the edge of the Hollow
// A flat concrete apron that ends in a drop into fog, a dusk-amber sky, the Department wall with its gate, a chalk road with
// paired lamps from the circle to the gate, Slice's red radius ring, a watchtower whose searchlight sweeps the apron, and a
// stamp press across the road (hazard and set piece): it wakes when Wallit comes near and falls on a red INVALID mark in time,
// leaving a print each time.
public class EdgeMap : OwnMap {
    public override string Key { get { return "hollow-edge"; } }
    Slammer press;
    Transform prints;
    Vector3 markAt;
    float bearing;
    int printed;

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        bearing = chapter.Bearing;
        float gate = chapter.Target;
        Color chalk = C(0.8f, 0.78f, 0.7f), iron = C(0.16f, 0.17f, 0.22f), wall = C(0.14f, 0.15f, 0.2f), red = C(0.85f, 0.14f, 0.12f), amber = C(1f, 0.78f, 0.4f);
        Terrain("Concrete Apron",
            (deg, d) => -Mathf.SmoothStep(0f, 1f, (d - 215f) / 70f) * 16f + 0.6f * Noise(deg * 4f, d, 4f),
            (deg, d, h) => d > 226f && d < 232f ? C(0.7f, 0.68f, 0.6f) : Color.Lerp(C(0.2f, 0.2f, 0.24f), C(0.07f, 0.06f, 0.08f), Mathf.Clamp01(-h / 12f)) * (0.92f + 0.12f * Noise(deg * 9f, d, 5f)));
        SetSky(C(0.1f, 0.07f, 0.13f), C(0.42f, 0.27f, 0.3f), C(0.1f, 0.06f, 0.07f), C(1f, 0.7f, 0.5f), new Vector3(0.34f, 0.2f, 0.92f), 0.03f, 0.32f);
        MakeMusic(12f, new[] { 73.4f, 110f, 146.8f, 207.7f }, 3f, 2f, 0.5f, 3f, 196f, 0.28f);

        Transform road = Group("Chalk Road");
        Path(road, bearing, 14f, gate - 6f, 1.6f, 4f, chalk, chalk * 0.85f);
        Quaternion face = Quaternion.Euler(0f, bearing, 0f);
        for (int i = 0; i < 6; i++) {
            float d = 18f + i * ((gate - 24f) / 5f);
            foreach (float side in new[] { -4f, 4f }) {
                Vector3 p = Polar(bearing, d) + face * new Vector3(side, 0f, 0f);
                Piece(road, "Lamp Post", p + Vector3.up * 1.8f, new Vector3(0.3f, 3.6f, 0.3f), iron);
                Piece(road, "Lamp", p + Vector3.up * 3.9f, new Vector3(0.9f, 0.9f, 0.9f), amber, 0f, 0f, Shape.Box, true);
            }
            if (i % 2 == 1) {
                Lamp(road, Polar(bearing, d, 4f), amber, 22f, 1.1f);
            }
        }
        Features.Add("route:chalk road with paired lamps from the circle to the gate, " + gate + " m on bearing " + bearing);

        // The Department wall, an arc round the circle with a gap at the gate, and the gatehouse in the gap.
        Transform walls = Group("Department Wall");
        float radius = gate + 6f;
        for (int i = -5; i <= 5; i++) {
            if (Mathf.Abs(i) <= 1) {
                continue;
            }
            float b = bearing + i * 7f;
            float length = 2f * Mathf.PI * radius * 7f / 360f;
            Piece(walls, "Wall Section " + i, Polar(b, radius, 3.5f), new Vector3(length, 7f, 1.4f), wall, b);
            for (int k = -1; k <= 1; k++) {
                Piece(walls, "Crenel", Polar(b, radius, 7.5f) + Quaternion.Euler(0f, b, 0f) * new Vector3(k * length * 0.3f, 0f, 0f), new Vector3(length * 0.16f, 1.2f, 1.4f), wall * 1.25f, b);
            }
        }
        Transform gateGroup = Group("Gatehouse");
        Vector3 left = Polar(bearing - 5.5f, radius), right = Polar(bearing + 5.5f, radius);
        Piece(gateGroup, "Gate Pillar L", left + Vector3.up * 6.5f, new Vector3(3f, 13f, 3f), wall * 1.4f, bearing);
        Piece(gateGroup, "Gate Pillar R", right + Vector3.up * 6.5f, new Vector3(3f, 13f, 3f), wall * 1.4f, bearing);
        Piece(gateGroup, "Gate Lintel", Polar(bearing, radius, 12.6f), new Vector3(Vector3.Distance(left, right), 2f, 2.4f), wall * 1.4f, bearing);
        for (int i = -1; i <= 1; i++) {
            Piece(gateGroup, "Gate Notice Bar", Polar(bearing, radius - 1.4f, 12.6f) + face * new Vector3(i * 2.6f, 0f, 0f), new Vector3(1.6f, 0.7f, 0.2f), red, bearing, 0f, Shape.Box, true);
        }
        Lamp(gateGroup, Polar(bearing, radius - 2f, 9f), amber, 34f, 1.6f);
        Features.Add("landmark:the Department wall running off either side of a gatehouse, three red notice bars over the gap");

        Transform ring = Group("Slice's Radius");
        for (int i = 0; i < 48; i++) {
            float b = i * 7.5f;
            Piece(ring, "Radius Mark " + i, Polar(b, 24f, 0.09f), new Vector3(0.6f, 0.05f, 3.6f), red, b + 90f);
        }
        Features.Add("landmark:Slice's radius, a red painted ring 24 m out that she tells Wallit not to leave");

        // Watchtower with a sweeping searchlight.
        Transform tower = Group("Watchtower");
        Vector3 towerAt = Polar(bearing + 38f, radius - 6f);
        foreach (Vector2 leg in new[] { new Vector2(-1.6f, -1.6f), new Vector2(1.6f, -1.6f), new Vector2(-1.6f, 1.6f), new Vector2(1.6f, 1.6f) }) {
            Piece(tower, "Tower Leg", towerAt + new Vector3(leg.x, 6f, leg.y), new Vector3(0.4f, 12f, 0.4f), iron);
        }
        Piece(tower, "Tower Cabin", towerAt + Vector3.up * 13f, new Vector3(5.2f, 2.6f, 5.2f), wall * 1.5f);
        var pivot = new GameObject("Searchlight");
        pivot.transform.SetParent(tower, false);
        pivot.transform.position = towerAt + Vector3.up * 13.5f;
        var lightObj = new GameObject("Beam");
        lightObj.transform.SetParent(pivot.transform, false);
        lightObj.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        Light beam = lightObj.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.color = C(1f, 0.92f, 0.75f);
        beam.range = 150f;
        beam.spotAngle = 26f;
        beam.intensity = 2.6f;
        beam.shadows = LightShadows.None;
        pivot.AddComponent<Sweeper>().Set(bearing + 180f, 55f, 0.22f);
        Features.Add("landmark:a watchtower at the wall whose searchlight sweeps the apron");

        // Supplies stacked off the road.
        Transform stock = Group("Supplies");
        var rng = new System.Random(5);
        for (int i = 0; i < 9; i++) {
            float d = 26f + i * 5f;
            float side = (i % 2 == 0 ? 1f : -1f) * (8f + (float)rng.NextDouble() * 4f);
            Vector3 p = Polar(bearing, d) + face * new Vector3(side, 0f, 0f);
            if (i % 3 == 0) {
                Piece(stock, "Barrel", p + Vector3.up * 0.9f, new Vector3(1.3f, 1.8f, 1.3f), C(0.45f, 0.18f, 0.12f), 0f, 0f, Shape.Cylinder);
            } else {
                Piece(stock, "Crate", p + Vector3.up * 0.9f, new Vector3(2.2f, 1.8f, 2.2f), C(0.32f, 0.26f, 0.18f), (float)rng.NextDouble() * 90f);
            }
        }

        // The stamp press across the road.
        Transform pressGroup = Group("Stamp Press");
        Vector3 baseAt = Polar(bearing, 46f);
        markAt = baseAt;
        Piece(pressGroup, "Press Post L", baseAt + face * new Vector3(-5f, 5f, 0f), new Vector3(1f, 10f, 1f), iron, bearing);
        Piece(pressGroup, "Press Post R", baseAt + face * new Vector3(5f, 5f, 0f), new Vector3(1f, 10f, 1f), iron, bearing);
        Piece(pressGroup, "Press Beam", baseAt + Vector3.up * 10.4f, new Vector3(11f, 1f, 1.2f), iron, bearing);
        Piece(pressGroup, "Press Mark", baseAt + Vector3.up * 0.12f, new Vector3(7f, 0.05f, 5f), red * 0.7f, bearing);
        GameObject plate = Piece(pressGroup, "Press Plate", baseAt + Vector3.up * 8f, new Vector3(7f, 1.2f, 5f), red, bearing, 0f, Shape.Box, true);
        press = plate.AddComponent<Slammer>();
        press.High = 8f;
        press.Low = 1.2f;
        press.Period = 3.4f;
        press.ArmRadius = 22f;
        press.Warning = Lamp(pressGroup, baseAt + Vector3.up * 3f, C(1f, 0.15f, 0.1f), 14f, 0f);
        press.Landed = Landed;
        prints = Group("Stamp Prints");
        Features.Add("hazard:the stamp press across the road at 46 m, its plate falling every few seconds on a red INVALID mark (it wakes when Wallit is within 22 m)");
        Features.Add("setpiece:The Stamp Press: it wakes as Wallit nears, lights its mark, slams, throws the Yo Bots under it and leaves a red INVALID print each time");
    }

    void Landed(int n) {
        bool wallit;
        int bots = Strike(markAt, 4.2f, 4f, out wallit);
        Thud(0.9f);
        Fx.Ring(markAt + Vector3.up * 0.3f, 14f, new Color(1f, 0.3f, 0.2f, 0.8f), 0.8f);
        if (printed < 8) {
            Piece(prints, "Stamp Print " + n, markAt + Vector3.up * (0.14f + printed * 0.004f), new Vector3(6.4f, 0.03f, 4.4f), new Color(0.6f, 0.08f, 0.08f, 1f), bearing + (n % 3 - 1) * 4f);
            printed++;
        }
        Beat("press-slam|" + n + "|bots=" + bots + (wallit ? "|wallit" : ""));
    }

    public override void Trigger() {
        if (press != null) {
            press.Forced = true;
        }
    }

    // Turns a pivot back and forth round a bearing.
    public class Sweeper : MonoBehaviour {
        float centre, range, speed;

        public void Set(float c, float r, float s) {
            centre = c;
            range = r;
            speed = s;
        }

        void Update() {
            transform.rotation = Quaternion.Euler(0f, centre + Mathf.Sin(Time.time * speed) * range, 0f);
        }
    }
}

// ---------------------------------------------------------------------------------------------- Chapter 3: the Clearing Floor
// A sunken yard: terraced concrete walls climb out of it on every side under smog and a dull orange sky, with smokestacks on the
// rim. The route is the Floor's own stops, plates lit one by one; hook gantries arch over it hung with unfinished Yo Bot
// chassis, two belts of cleared things slide beside it, three crushers slam on red marks in turn (hazard). Set piece, The Belt
// Stops: pulling the lever (ChapterRun.FeedBroken) halts the belts and crushers, dims the lamps and drops the music.
public class FloorMap : OwnMap {
    public override string Key { get { return "clearing-floor"; } }
    readonly List<ChapterMap.MapMover> belts = new List<ChapterMap.MapMover>();
    readonly List<Slammer> crushers = new List<Slammer>();
    readonly List<Light> lamps = new List<Light>();
    readonly List<float> lampLevel = new List<float>();
    bool stopped;
    Vector3 leverAt;

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        Story.Stop[] stops = chapter.Stops;
        if (stops == null || stops.Length == 0) {
            return;
        }
        Color iron = C(0.16f, 0.17f, 0.22f), steel = C(0.55f, 0.58f, 0.66f), belt = C(0.12f, 0.12f, 0.16f), lamp = C(1f, 0.74f, 0.35f), red = C(0.9f, 0.14f, 0.1f), rust = C(0.34f, 0.16f, 0.1f);
        Terrain("Sunken Yard Walls",
            (deg, d) => {
                float wall = Mathf.SmoothStep(0f, 1f, (d - 212f) / 70f) * 30f;
                return Mathf.Floor(wall / 6f) * 6f + (wall % 6f) * 0.25f + (d > 290f ? 3f * Noise(deg * 3f, d, 6f) : 0f);
            },
            (deg, d, h) => Mathf.Repeat(h, 6f) < 1.4f ? C(0.4f, 0.28f, 0.18f) : Color.Lerp(rust, C(0.12f, 0.1f, 0.11f), Mathf.Clamp01(h / 30f)) * (0.9f + 0.2f * Noise(deg * 8f, d, 7f)));
        SetSky(C(0.07f, 0.04f, 0.05f), C(0.6f, 0.3f, 0.12f), C(0.12f, 0.06f, 0.04f), C(1f, 0.5f, 0.18f), new Vector3(-0.5f, 0.16f, 0.85f), 0.035f, 0.3f);
        MakeMusic(12f, new[] { 55f, 82.4f, 110f, 123.5f }, 2f, 3f, 0.55f, 1.5f, 98f, 0.3f);

        float bearing = stops[stops.Length - 1].Bearing;
        float length = stops[stops.Length - 1].Dist + 20f;
        Quaternion face = Quaternion.Euler(0f, bearing, 0f);

        Transform route = Group("Yard Floor Route");
        Path(route, bearing, 16f, length, 6f, 6f, belt * 1.7f, belt * 1.35f);
        for (int i = 0; i < stops.Length; i++) {
            Vector3 p = Polar(stops[i].Bearing, stops[i].Dist, 0.14f);
            Piece(route, "Stop Plate " + (i + 1), p, new Vector3(9f, 0.06f, 9f), lamp * 0.8f, stops[i].Bearing, 0f, Shape.Cylinder, true);
            Light l = Lamp(route, p + Vector3.up * 8f, lamp, 30f, 1.5f);
            lamps.Add(l);
            lampLevel.Add(l.intensity);
        }
        Features.Add("route:the yard floor path through " + stops.Length + " lit stops, " + stops[0].Name + " to " + stops[stops.Length - 1].Name);

        // The broken gate at the first stop and the lever at the last.
        Transform gateG = Group("Broken Gate");
        Vector3 gAt = Polar(stops[0].Bearing, stops[0].Dist);
        Piece(gateG, "Gate Post L", gAt + face * new Vector3(-5f, 4.5f, 0f), new Vector3(1.2f, 9f, 1.2f), iron, bearing, -5f);
        Piece(gateG, "Gate Post R", gAt + face * new Vector3(5f, 4f, 0f), new Vector3(1.2f, 8f, 1.2f), iron, bearing, 14f);
        Piece(gateG, "Gate Lintel Fallen", gAt + face * new Vector3(2f, 1f, 3f), new Vector3(8f, 0.9f, 1f), steel * 0.7f, bearing + 20f, 8f);
        Transform leverG = Group("Lever Housing");
        leverAt = Polar(stops[stops.Length - 1].Bearing, stops[stops.Length - 1].Dist);
        Piece(leverG, "Lever Housing", leverAt + face * new Vector3(0f, 2.5f, 4f), new Vector3(4f, 5f, 2.5f), iron * 1.3f, bearing);
        Piece(leverG, "Lever Handle", leverAt + face * new Vector3(0f, 6f, 2.6f), new Vector3(0.5f, 3.4f, 0.5f), red, bearing, 20f, Shape.Cylinder, true);
        Piece(leverG, "Chute Lamp", leverAt + new Vector3(0f, 12f, 0f), new Vector3(3f, 3f, 0.6f), red, bearing, 0f, Shape.Box, true);

        Transform gantry = Group("Hook Gantries");
        for (int i = 0; i < 6; i++) {
            Vector3 at = Polar(bearing, 24f + i * 15f);
            Piece(gantry, "Gantry Leg L", at + face * new Vector3(-9f, 6f, 0f), new Vector3(0.9f, 12f, 0.9f), iron, bearing);
            Piece(gantry, "Gantry Leg R", at + face * new Vector3(9f, 6f, 0f), new Vector3(0.9f, 12f, 0.9f), iron, bearing);
            Piece(gantry, "Gantry Rail", at + Vector3.up * 12.2f, new Vector3(19f, 0.8f, 0.8f), iron, bearing);
            for (int k = -1; k <= 1; k += 2) {
                Piece(gantry, "Hook", at + face * new Vector3(k * 4f, 10.4f, 0f), new Vector3(0.2f, 3.4f, 0.2f), iron, bearing);
                Piece(gantry, "Chassis", at + face * new Vector3(k * 4f, 8f, 0f), new Vector3(1.6f, 1.9f, 1.2f), steel, bearing);
            }
        }
        Features.Add("landmark:six hook gantries arching the route, hung with un-built Yo Bot chassis");

        for (int side = -1; side <= 1; side += 2) {
            Transform b = Group("Belt " + (side < 0 ? "West" : "East"));
            Vector3 mid = Polar(bearing, length * 0.5f) + face * new Vector3(side * 17f, 0f, 0f);
            Piece(b, "Belt Bed", mid + Vector3.up * 0.5f, new Vector3(3.4f, 1f, length - 10f), belt, bearing);
            Color[] thing = { C(0.35f, 0.3f, 0.4f), C(1f, 0.74f, 0.35f), C(0.8f, 0.65f, 0.3f) };
            for (int k = 0; k < 6; k++) {
                GameObject item = Piece(b, "Cleared Thing " + k, mid + face * new Vector3(0f, 1.4f, 0f), new Vector3(1.4f, 1.1f, 1.4f), thing[k % 3], bearing, 0f, k % 2 == 0 ? Shape.Box : Shape.Cylinder);
                var mover = item.AddComponent<ChapterMap.MapMover>();
                mover.Set(ChapterMap.MapMover.Mode.Belt, 0f, 0f, side < 0 ? 3f : -3f, (k / 6f) * (length - 10f));
                mover.SetLine(mid + face * new Vector3(0f, 1.4f, 0f), face * Vector3.forward, length - 10f);
                belts.Add(mover);
            }
        }
        Features.Add("landmark:two belts of cleared things (a hat, a clock, a bell) sliding along the route");

        Transform stacks = Group("Smokestacks");
        for (int i = 0; i < 7; i++) {
            float b = 20f + i * 51f;
            float h = 38f + (i % 3) * 10f;
            Vector3 p = Polar(b, 255f + (i % 2) * 20f);
            p.y = 24f;
            Piece(stacks, "Smokestack", p + Vector3.up * h * 0.5f, new Vector3(7f, h, 7f), rust * 0.8f, b, 0f, Shape.Cylinder);
            Piece(stacks, "Stack Mouth", p + Vector3.up * (h + 0.4f), new Vector3(7.4f, 1.2f, 7.4f), lamp, b, 0f, Shape.Cylinder, true);
        }

        Transform crush = Group("Crushers");
        for (int i = 0; i < 3; i++) {
            float d = 38f + i * 24f;
            int side = i % 2 == 0 ? 1 : -1;
            Vector3 at = Polar(bearing, d) + face * new Vector3(side * 6f, 0f, 0f);
            Piece(crush, "Crusher Frame", at + Vector3.up * 5.5f, new Vector3(0.8f, 11f, 0.8f), iron, bearing);
            Piece(crush, "Crusher Mark", at + Vector3.up * 0.12f, new Vector3(6f, 0.05f, 6f), red * 0.7f, bearing);
            GameObject head = Piece(crush, "Crusher Head", at + Vector3.up * 7f, new Vector3(6f, 1.6f, 6f), red, bearing, 0f, Shape.Box, true);
            var s = head.AddComponent<Slammer>();
            s.High = 7f;
            s.Low = 1f;
            s.Period = 2.8f;
            s.Offset = i * 0.9f;
            s.Warning = Lamp(crush, at + Vector3.up * 3f, C(1f, 0.15f, 0.1f), 12f, 0f);
            Vector3 mark = at;
            s.Landed = n => {
                bool wallit;
                int bots = Strike(mark, 3.4f, 3f, out wallit);
                Thud(0.5f);
                Fx.Ring(mark + Vector3.up * 0.3f, 10f, new Color(1f, 0.4f, 0.2f, 0.7f), 0.6f);
                if (bots > 0 || wallit) {
                    Log.Add("crusher|" + n + "|bots=" + bots + (wallit ? "|wallit" : ""));
                }
            };
            crushers.Add(s);
        }
        Features.Add("hazard:three crushers beside the route (at 38, 62 and 86 m) that slam down on red marks in turn");
        Features.Add("setpiece:The Belt Stops: pulling the lever halts the belts and crushers, dims the lamps and drops the music");
    }

    public override void Trigger() {
        Stop();
    }

    void Update() {
        if (!stopped && ChapterRun.Instance != null && ChapterRun.Instance.FeedBroken) {
            Stop();
        }
        if (stopped && Music != null) {
            Music.pitch = Mathf.MoveTowards(Music.pitch, 0.6f, Time.deltaTime * 0.25f);
            Music.volume = Mathf.MoveTowards(Music.volume, 0.1f, Time.deltaTime * 0.05f);
        }
    }

    void Stop() {
        if (stopped) {
            return;
        }
        stopped = true;
        foreach (ChapterMap.MapMover m in belts) {
            if (m != null) {
                m.enabled = false;
            }
        }
        foreach (Slammer s in crushers) {
            if (s != null) {
                s.Frozen = true;
            }
        }
        for (int i = 0; i < lamps.Count; i++) {
            if (lamps[i] != null) {
                lamps[i].intensity = lampLevel[i] * 0.3f;
            }
        }
        Thud(1f);
        Fx.Ring(leverAt + Vector3.up * 0.3f, 30f, new Color(1f, 0.7f, 0.3f, 0.8f), 1.4f);
        Beat("belt-stopped|belts=" + belts.Count + "|crushers=" + crushers.Count);
    }
}
