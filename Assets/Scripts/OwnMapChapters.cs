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

// ---------------------------------------------------------------------------------------------- Chapter 2: the Gate Hall
// The edge of the Hollow at last light, dressed from the Model Kit (Docs/Levels1to3.md, 3): the Arcade's meadow is hidden and a
// pale paved apron baked from the live ground takes its place; a chalk road of paving slabs with paired chalk lamps runs from
// the circle to a paved plaza (ticket booths, turnstiles, barrier rails, bollards, INVALID boards) and the gate, a stone arch
// sealed by a handleless door between pillars, in the Department's wall (bays and towers on an arc round the circle) with a
// watchtower whose searchlight sweeps; Slice's red radius is painted on the apron; the big stamp press straddles the road (hazard
// and set piece): its head wakes when Wallit comes near and falls on a red INVALID mark in time, leaving a print each time. A
// blank signpost loop leaves the road and rejoins it. Tall towers behind the wall and roofs round the rest make the skyline.
public class EdgeMap : OwnMap {
    public override string Key { get { return "hollow-edge"; } }
    Slammer press;
    Transform pressFrame;
    Transform prints;
    Vector3 markAt;
    float bearing;
    int printed;
    Material redMat;
    readonly System.Random rng = new System.Random(5);

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static float GroundY(float x, float z) {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, out hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return 0f;
    }

    // A point on the road's own grid: `along` metres out on the gate's bearing, `side` metres to its right, on the ground.
    Vector3 At(float along, float side) {
        Vector3 p = Polar(bearing, along) + Quaternion.Euler(0f, bearing, 0f) * new Vector3(side, 0f, 0f);
        p.y = GroundY(p.x, p.z);
        return p;
    }

    GameObject Put(Transform g, string id, float along, float side, float yawLocal = 0f, float scale = 1f) {
        return LevelDress.Put(g, id, At(along, side), bearing + 180f + yawLocal, scale);
    }

    protected override void Build() {
        bearing = chapter.Bearing;
        float gate = chapter.Target;
        float wallR = gate + 4f;
        Color red = C(0.85f, 0.14f, 0.12f), amber = C(1f, 0.78f, 0.4f);
        gameObject.AddComponent<ArenaHider>();
        Terrain("Concrete Apron",
            (deg, d) => -Mathf.SmoothStep(0f, 1f, (d - 215f) / 70f) * 16f + 0.6f * Noise(deg * 4f, d, 4f),
            (deg, d, h) => d > 226f && d < 232f ? C(0.7f, 0.68f, 0.6f) : Color.Lerp(C(0.2f, 0.2f, 0.24f), C(0.07f, 0.06f, 0.08f), Mathf.Clamp01(-h / 12f)) * (0.92f + 0.12f * Noise(deg * 9f, d, 5f)));
        SetSky(C(0.1f, 0.07f, 0.13f), C(0.42f, 0.27f, 0.3f), C(0.1f, 0.06f, 0.07f), C(1f, 0.7f, 0.5f), new Vector3(0.34f, 0.2f, 0.92f), 0.03f, 0.32f);
        MakeMusic(12f, new[] { 73.4f, 110f, 146.8f, 207.7f }, 3f, 2f, 0.5f, 3f, 196f, 0.28f);
        redMat = LevelDress.GroundMaterial("gate", "concrete", new Color(1.1f, 0.2f, 0.18f));
        Transform apron = Group("Apron");
        Skin(apron, "Paved Apron", 205f, 4f, LevelDress.GroundMaterial("gate", "concrete", new Color(0.78f, 0.8f, 0.9f)), 6f);

        // The chalk road: three columns of paving slabs laid from the circle to the plaza, and the plaza's big flagstones. Each slab
        // lies along the apron's slope with its underside 2 cm into it (level, a slab was half buried and half proud on the bowl);
        // neighbours only meet edge to edge, so no two tops share a plane. None under the stamp press's bed (46 m, 7.6 by 5.2 m).
        Transform road = Group("Chalk Road");
        for (float d = 12f; d < gate - 24f; d += 2.5f) {
            for (int c = -1; c <= 1; c++) {
                float yaw = bearing + (rng.Next(0, 2) * 90f);
                if (Mathf.Abs(d - 46f) < 2.6f + 0.95f && Mathf.Abs(c * 1.9f) < 3.8f) {
                    continue;
                }
                Lay(LevelDress.PutFlat(road, "yd_gravel_slab", At(d, c * 1.9f), yaw, 1.9f, 0.55f), yaw, 0.95f, 0.95f, -0.02f);
            }
        }
        for (int j = 0; j < 5; j++) {
            for (int i = -3; i <= 3; i++) {
                Lay(LevelDress.PutFlat(road, "yd_gravel_slab", At(gate - 22f + j * 3.5f, i * 3.5f), bearing, 3.4f, 0.5f), bearing, 1.7f, 1.7f, -0.02f);
            }
        }
        Features.Add("route:chalk road of paving slabs with paired lamps from the circle to the gate plaza, " + gate + " m on bearing " + bearing);
        for (int i = 0; i < 5; i++) {
            float d = 20f + i * ((gate - 38f) / 4f);
            foreach (float side in new[] { -4.4f, 4.4f }) {
                Put(road, "gt_chalk_lamp", d, side, 0f, 1f);
            }
            LevelDress.PointLight(road, At(d, 0f) + Vector3.up * 3.4f, C(0.98f, 0.9f, 0.78f), 22f, 1.15f);
        }

        // The blank signpost loop: a second chalk path that leaves the road at 26 m, bows out to the east and rejoins at 60 m.
        Transform loop = Group("Signpost Loop");
        for (float t = 0f; t <= 1f; t += 0.055f) {
            float along = Mathf.Lerp(26f, 60f, t);
            float side = 24f * Mathf.Sin(Mathf.PI * t) + 5f;
            // Where it reaches the plaza it runs onto the flagstones: no slab laid over one (two slab tops a few mm apart z-fought).
            if (along > gate - 23.7f - 1.1f && Mathf.Abs(side) < 12.2f + 1.1f) {
                continue;
            }
            Lay(LevelDress.PutFlat(loop, "yd_gravel_slab", At(along, side), bearing + t * 70f, 1.5f, 0.55f), bearing + t * 70f, 0.75f, 0.75f, -0.02f);
        }
        GameObject sign = Put(loop, "yd_signpost", 43f, 29f, 20f, 1.4f);
        sign.name = "Blank Signpost";
        Put(loop, "yd_standing_stone", 45f, 35f, 160f, 0.8f).name = "Torn Postmark Stone";
        Put(loop, "wk_stone_bench", 41f, 26f, 90f, 1.2f);
        LevelDress.PointLight(loop, At(43f, 27f) + Vector3.up * 3f, C(0.9f, 0.85f, 0.75f), 10f, 0.7f);

        // The gate hall's plaza: booths, turnstiles, barrier rails, bollards and INVALID boards, then the gate itself.
        Transform plaza = Group("Gate Plaza");
        float g2 = gate + 4f;
        for (int s = -1; s <= 1; s += 2) {
            GameObject booth = Put(plaza, "gt_ticket_booth", g2 - 10f, s * 12f, s * 90f, 1.6f);
            booth.name = "Ticket Booth " + (s < 0 ? "W" : "E");
            LevelDress.PointLight(plaza, At(g2 - 10f, s * 10.2f) + Vector3.up * 2.4f, C(1f, 0.82f, 0.5f), 10f, 1.3f);
            Put(plaza, "gt_turnstile", g2 - 14f, s * 5.4f, 0f, 1.5f);
            Put(plaza, "gt_turnstile", g2 - 14f, s * 8.2f, 0f, 1.5f);
            for (int k = 0; k < 3; k++) {
                Put(plaza, "gt_barrier_rail", g2 - 18f - k * 0.1f, s * (3.4f + k * 2f), 0f, 1.2f);
                Put(plaza, "gt_bollard", g2 - 6f + k * 0.1f, s * (14f + k * 3f), 0f, 1.3f);
            }
            for (int k = 0; k < 3; k++) {
                GameObject board = Put(plaza, "gt_invalid_board", g2 - 6.5f, s * (2.6f + k * 2.6f), 0f, 1.7f);
                LevelDress.Tint(board, 1, red);
            }
            Put(plaza, "st_bench", g2 - 24f, s * 9f, s * 90f, 1.1f);
            Put(plaza, "st_bin", g2 - 23f, s * 11f, 0f, 1f);
        }
        Transform gateGroup = Group("Gatehouse");
        Vector3 gatePos = At(g2, 0f);
        GameObject arch = LevelDress.Put(gateGroup, "gt_gate_arch", gatePos, bearing + 180f, 2.5f);
        arch.name = "Gate Arch";
        LevelDress.Put(gateGroup, "gt_gate_door", gatePos + Quaternion.Euler(0f, bearing + 180f, 0f) * new Vector3(0f, 0f, 0.2f), bearing + 180f, 1f).name = "Gate Door (no handle)";
        for (int s = -1; s <= 1; s += 2) {
            GameObject pillar = Put(gateGroup, "gt_gate_pillar", g2, s * 6.8f, 0f, 2.1f);
            pillar.name = "Gate Pillar";
        }
        LevelDress.PointLight(gateGroup, At(g2 - 4f, 0f) + Vector3.up * 6f, amber, 34f, 1.6f);
        LevelDress.PointLight(gateGroup, At(g2 - 3f, -4f) + Vector3.up * 2f, C(1f, 0.55f, 0.35f), 12f, 0.8f);
        Features.Add("landmark:the gate arch with its sealed, handleless door between two pillars, three red INVALID boards before it and the amber-lit plaza");

        // The Department wall: bays and towers on an arc round the circle, a gap at the gate.
        Transform walls = Group("Department Wall");
        for (int s = -1; s <= 1; s += 2) {
            for (int i = 0; i < 12; i++) {
                float b = bearing + s * (8f + i * 5.1f);
                Vector3 p = Polar(b, wallR);
                bool isTower = i == 3 || i == 8;
                // On the lowest of the floor under its 6 m (or 6 m square) footing: no end of a bay hangs over the slope.
                p.y = (isTower ? Lowest(p, b + 180f, 3f, 3f) : Lowest(p, b + 180f, 3.1f, 0.8f)) - 0.03f;
                LevelDress.Put(walls, isTower ? "gt_wall_tower" : "gt_wall_bay", p, b + 180f, isTower ? 1.2f : 1f);
            }
        }
        Features.Add("landmark:the Department wall running off either side of the gate in bays and towers");

        // Slice's radius: dashes of red paint on the apron at 24 m.
        Transform ring = Group("Slice's Radius");
        // Each dash lies along the apron's slope 2 cm over the drawn skin (level, 5 cm over the collider, a 3.6 m dash dipped under the
        // skin at one end on the bowl's slope and z-fought with it).
        for (int i = 0; i < 48; i++) {
            float b = i * 7.5f;
            Vector3 p = Polar(b, 24f);
            GameObject dash = LevelKit.Decal(ring, "Radius Mark " + i, p, new Vector2(0.6f, 3.6f), Color.white);
            dash.GetComponent<MeshRenderer>().sharedMaterial = redMat;
            dash.transform.position = p;
            Lay(dash, b + 180f, 0.3f, 1.8f, 0.02f);
        }
        Features.Add("landmark:Slice's radius, a red painted ring 24 m out that she tells Wallit not to leave");

        // Watchtower with a sweeping searchlight, on the wall.
        Transform tower = Group("Watchtower");
        Vector3 towerAt = Polar(bearing + 38f, wallR - 6f);
        towerAt.y = Lowest(towerAt, bearing + 38f + 180f, 3.5f, 3.5f) - 0.03f;
        LevelDress.Put(tower, "gt_wall_tower", towerAt, bearing + 38f + 180f, 1.4f).name = "Watchtower";
        var pivot = new GameObject("Searchlight");
        pivot.transform.SetParent(tower, false);
        pivot.transform.position = towerAt + Vector3.up * 14.4f;
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

        // Supplies and notice boards off the road: no two alike.
        Transform stock = Group("Supplies");
        string[] picks = { "st_crate", "fl_barrel", "gt_invalid_board", "st_bin", "gt_bollard" };
        for (int i = 0; i < 16; i++) {
            float d = 28f + i * 2.4f;
            float side = (i % 2 == 0 ? 1f : -1f) * (9f + (float)rng.NextDouble() * 4f);
            string id = picks[i % picks.Length];
            GameObject g = LevelDress.Put(stock, id, At(d, side), (float)rng.NextDouble() * 360f, 1f + (float)rng.NextDouble() * 0.3f);
            if (id == "gt_invalid_board") {
                LevelDress.Tint(g, 1, red);
            }
        }

        // The big stamp press across the road: a frame with a print bed, and a head that rises, hangs on a red light and slams.
        Transform pressGroup = Group("Stamp Press");
        // The frame lies along the apron's slope (level, the feet of one side of its 8.4 m stood in the air) and the head runs square
        // to it, so the prints land flat on the bed.
        GameObject frame = Lay(LevelDress.Put(pressGroup, "gt_press_frame", At(46f, 0f), bearing, 1f), bearing, 4.1f, 2.6f, -0.02f);
        pressFrame = frame.transform;
        Vector3 baseAt = pressFrame.position;
        markAt = baseAt;
        LevelDress.Tint(frame, 1, red * 0.75f);
        GameObject head = LevelDress.Put(pressGroup, "gt_press_head", baseAt + pressFrame.up * 8f, bearing, 1f);
        head.transform.rotation = pressFrame.rotation;
        LevelDress.Tint(head, 1, red);
        press = head.AddComponent<Slammer>();
        press.Track = baseAt;
        press.Guide = pressFrame.up;
        press.High = 8f;
        press.Low = 0.26f;
        press.Period = 3.4f;
        press.ArmRadius = 22f;
        press.Warning = Lamp(pressGroup, baseAt + Vector3.up * 3f, C(1f, 0.15f, 0.1f), 14f, 0f);
        press.Landed = Landed;
        prints = Group("Stamp Prints");
        Features.Add("hazard:the stamp press across the road at 46 m, its head falling every few seconds on a red INVALID mark (it wakes when Wallit is within 22 m)");
        Features.Add("setpiece:The Stamp Press: it wakes as Wallit nears, lights its mark, slams, throws the Yo Bots under it and leaves a red INVALID print each time");

        // The skyline: towers behind the wall, rooftops round the open side. Tall enough to clear the wall from the road.
        Transform far = Group("Skyline");
        // Each on the lowest floor under its footing, so no corner of a 9-12 m tower or a 12 m row hangs over the slope.
        for (int i = 0; i < 8; i++) {
            float b = bearing - 70f + i * 20f;
            float scale = 1.8f + (i % 3) * 0.3f;
            Vector3 p = Polar(b, 108f + (i % 3) * 9f);
            p.y = Lowest(p, b + 180f, 2.5f * scale, 2.5f * scale) - 0.05f;
            LevelDress.Put(far, "gt_wall_tower", p, b + 180f, scale);
        }
        string[] rows = { "st_house_wall", "st_shopfront", "st_house_wall" };
        for (int i = 0; i < 7; i++) {
            float b = bearing + 85f + i * 28f;
            Vector3 p = Polar(b, 125f + (i % 2) * 18f);
            // The row stands on its front line (origin) and runs 2.5 m back; 3 bays of 4 m.
            p.y = Lowest(p + Quaternion.Euler(0f, b + 180f, 0f) * new Vector3(0f, 0f, -1.25f), b + 180f, 6.2f, 1.3f) - 0.05f;
            LevelDress.Facade(far, "Far Roofs " + i, p, b + 180f, rows, 3 + i % 3, null, i % 3 == 2, false);
        }
    }

    void Landed(int n) {
        bool wallit;
        int bots = Strike(markAt, 4.2f, 4f, out wallit);
        Thud(0.9f);
        Fx.Ring(markAt + Vector3.up * 0.3f, 14f, new Color(1f, 0.3f, 0.2f, 0.8f), 0.8f);
        if (printed < 8) {
            // On the bed's paper (its top is 0.22 m up the frame), 5 mm over it and 4 mm over the print before, each turned a little:
            // flat on the bed however the frame lies, never in the same plane as another print. They floated 4 cm over the bed.
            Vector3 printAt = pressFrame.TransformPoint(new Vector3(0f, 0.225f + printed * 0.004f, 0f));
            GameObject print = LevelKit.Decal(prints, "Stamp Print " + n, printAt, new Vector2(5.6f, 3.8f), Color.white);
            print.GetComponent<MeshRenderer>().sharedMaterial = redMat;
            print.transform.SetPositionAndRotation(printAt, pressFrame.rotation * Quaternion.Euler(0f, (n % 3 - 1) * 4f, 0f));
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
// A sunken yard in amber smog, dressed from the Model Kit (Docs/Levels1to3.md, 4): oily concrete baked from the live ground, a route
// through three lit stops (a broken gate with fallen lintel and hung chassis, the belt, the lever stand under its red lamp), six
// hook gantries arching the lane with un-built Yo Bot chassis hanging from their rails, racks and pipe runs along the lip, a
// maintenance catwalk raised on girders, two belts of conveyors carrying a hat, a clock and a bell, and three presses slamming on
// red marks in turn (hazard). Smokestacks and terraces on the rim make the skyline. Set piece, The Belt Stops: pulling the lever
// (ChapterRun.FeedBroken) halts the belts and presses, dims the lamps, darkens the red lens and drops the music.
public class FloorMap : OwnMap {
    public override string Key { get { return "clearing-floor"; } }
    readonly List<ChapterMap.MapMover> belts = new List<ChapterMap.MapMover>();
    readonly List<Slammer> crushers = new List<Slammer>();
    readonly List<Light> lamps = new List<Light>();
    readonly List<float> lampLevel = new List<float>();
    Material lens;
    bool stopped;
    Vector3 leverAt;
    float bearing;
    Vector3 axis, right;
    readonly System.Random rng = new System.Random(9);

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static float GroundY(float x, float z) {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, out hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return 0f;
    }

    // A point on the lane: `along` metres out on the route's bearing, `side` metres to its right, on the ground.
    Vector3 At(float along, float side) {
        Vector3 p = center + axis * along + right * side;
        p.y = GroundY(p.x, p.z);
        return p;
    }

    float Yaw(float extra) {
        return bearing + 180f + extra;
    }

    protected override void Build() {
        Story.Stop[] stops = chapter.Stops;
        if (stops == null || stops.Length == 0) {
            return;
        }
        Color lamp = C(1f, 0.74f, 0.35f), red = C(0.9f, 0.14f, 0.1f), rust = C(0.34f, 0.16f, 0.1f);
        gameObject.AddComponent<ArenaHider>();
        Terrain("Sunken Yard Walls",
            (deg, d) => {
                float wall = Mathf.SmoothStep(0f, 1f, (d - 212f) / 70f) * 30f;
                return Mathf.Floor(wall / 6f) * 6f + (wall % 6f) * 0.25f + (d > 290f ? 3f * Noise(deg * 3f, d, 6f) : 0f);
            },
            (deg, d, h) => Mathf.Repeat(h, 6f) < 1.4f ? C(0.4f, 0.28f, 0.18f) : Color.Lerp(rust, C(0.12f, 0.1f, 0.11f), Mathf.Clamp01(h / 30f)) * (0.9f + 0.2f * Noise(deg * 8f, d, 7f)));
        SetSky(C(0.07f, 0.04f, 0.05f), C(0.6f, 0.3f, 0.12f), C(0.12f, 0.06f, 0.04f), C(1f, 0.5f, 0.18f), new Vector3(-0.5f, 0.16f, 0.85f), 0.035f, 0.3f);
        MakeMusic(12f, new[] { 55f, 82.4f, 110f, 123.5f }, 2f, 3f, 0.55f, 1.5f, 98f, 0.3f);

        bearing = stops[stops.Length - 1].Bearing;
        float a = bearing * Mathf.Deg2Rad;
        axis = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        right = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
        float length = stops[stops.Length - 1].Dist + 20f;

        Transform floorG = Group("Yard Ground");
        Skin(floorG, "Oily Concrete", 205f, 4f, LevelDress.GroundMaterial("floor", "concrete", new Color(0.62f, 0.52f, 0.46f)), 5f);

        // The route: girders laid along both edges of the lane as kerbs, and a lit stop plate with two cage lamps on girder posts.
        Transform route = Group("Yard Floor Route");
        // Kerbs and plates lie along the floor's slope (level, a 3 m girder or a 9 m plate had one end in the floor and the other
        // in the air), their undersides 2 cm into the drawn floor.
        for (float d = 14f; d < length - 6f; d += 3f) {
            Lay(LevelDress.Put(route, "fl_girder", At(d, -3.6f), bearing + 90f, 1f), bearing + 90f, 1.5f, 0.15f, -0.02f);
            Lay(LevelDress.Put(route, "fl_girder", At(d, 3.6f), bearing + 90f, 1f), bearing + 90f, 1.5f, 0.15f, -0.02f);
        }
        for (int i = 0; i < stops.Length; i++) {
            Vector3 p = Polar(stops[i].Bearing, stops[i].Dist);
            p.y = GroundY(p.x, p.z);
            GameObject plate = Lay(LevelDress.Put(route, "ar_ring_floor", p, 0f, 0.78f), 0f, 3.3f, 3.3f, -0.02f);
            plate.name = "Stop Plate " + (i + 1);
            for (int s = -1; s <= 1; s += 2) {
                Vector3 post = p + right * (s * 4.8f);
                post.y = GroundY(post.x, post.z);
                LevelDress.PutEuler(route, "fl_girder", post + Vector3.up * 1.5f, new Vector3(0f, bearing, 90f), 1f);
                LevelDress.PutEuler(route, "fl_girder", post + Vector3.up * 4.5f, new Vector3(0f, bearing, 90f), 1f);
                Vector3 toPlate = (p - post);
                toPlate.y = 0f;
                LevelDress.Put(route, "fl_cage_lamp", post + Vector3.up * 5.6f, Mathf.Atan2(toPlate.x, toPlate.z) * Mathf.Rad2Deg, 2f);
            }
            Light l = LevelDress.PointLight(route, p + Vector3.up * 8f, lamp, 30f, 1.5f);
            lamps.Add(l);
            lampLevel.Add(l.intensity);
        }
        Features.Add("route:the yard floor path through " + stops.Length + " lit stops, " + stops[0].Name + " to " + stops[stops.Length - 1].Name);

        // The broken gate at the first stop: one pillar down, one leaning, the lintel on the ground, racks of chassis behind.
        Transform gateG = Group("Broken Gate");
        Vector3 gAt = Polar(stops[0].Bearing, stops[0].Dist);
        gAt.y = GroundY(gAt.x, gAt.z);
        // Each piece on the floor where it lies, not at the stop's height metres away. The leaning pillar's low corner (6 degrees
        // over its 1.7 m plinth) goes 9 cm further in.
        Vector3 standing = gAt + right * -5.5f;
        standing.y = Lowest(standing, bearing, 0.86f, 0.86f) - 0.12f;
        LevelDress.PutEuler(gateG, "gt_gate_pillar", standing, new Vector3(0f, bearing, -6f), 1.9f);
        // The fallen pillar lies along the floor from its plinth (rolled 90 degrees less the floor's own rise along its 6.2 m), resting
        // on its plinth and capital; it was propped 8 degrees up with its head in the air.
        Vector3 fallen = gAt + right * 5.5f;
        Vector3 fallenTop = fallen - Quaternion.Euler(0f, bearing + 10f, 0f) * Vector3.right * 6.2f;
        float fallenRise = SkinY(fallenTop.x, fallenTop.z) - SkinY(fallen.x, fallen.z);
        fallen.y = SkinY(fallen.x, fallen.z) + 0.8f;
        LevelDress.PutEuler(gateG, "gt_gate_pillar", fallen, new Vector3(0f, bearing + 10f, 90f - Mathf.Atan2(fallenRise, 6.2f) * Mathf.Rad2Deg), 1.9f);
        // The lintel (a 6 m girder) on the floor end to end, not one end 0.9 m up on nothing.
        Vector3 lintel = gAt + right * 2.5f + axis * 3f;
        Vector3 lintelEnd = Quaternion.Euler(0f, bearing + 24f, 0f) * Vector3.right * 3f;
        float lintelA = SkinY(lintel.x + lintelEnd.x, lintel.z + lintelEnd.z), lintelB = SkinY(lintel.x - lintelEnd.x, lintel.z - lintelEnd.z);
        lintel.y = (lintelA + lintelB) * 0.5f - 0.03f;
        LevelDress.PutEuler(gateG, "fl_girder", lintel, new Vector3(0f, bearing + 24f, Mathf.Atan2(lintelA - lintelB, 6f) * Mathf.Rad2Deg), 2f);
        for (int k = -1; k <= 1; k += 2) {
            Vector3 rackAt = gAt + axis * -8f + right * (k * 4f);
            rackAt.y = Lowest(rackAt, Yaw(0f), 1.16f, 0.57f) - 0.02f;
            LevelDress.Put(gateG, "fl_chassis_rack", rackAt, Yaw(0f), 1.3f);
        }

        // The lever: its stand with the lamp mast and its red lens (the chute's lamp, watching the circle).
        Transform leverG = Group("Lever Stand");
        leverAt = Polar(stops[stops.Length - 1].Bearing, stops[stops.Length - 1].Dist);
        leverAt.y = GroundY(leverAt.x, leverAt.z);
        Vector3 standAt = leverAt + axis * 4f;
        standAt.y = Lowest(standAt, Yaw(0f), 1.6f, 1.1f) - 0.02f;   // its own floor, not the lever spot's 4 m off; no corner in the air
        GameObject stand = LevelDress.Put(leverG, "fl_lever_stand", standAt, Yaw(0f), 1f);
        stand.name = "Lever Stand";
        LevelDress.Tint(stand, 1, red);
        MeshRenderer standRenderer = stand.GetComponent<MeshRenderer>();
        Material[] standMats = standRenderer.sharedMaterials;
        lens = new Material(standMats[1]);
        standMats[1] = lens;
        standRenderer.sharedMaterials = standMats;
        made.Add(lens);
        Light chute = LevelDress.PointLight(leverG, standAt + Vector3.up * 12f, red, 26f, 1.4f);
        lamps.Add(chute);
        lampLevel.Add(chute.intensity);

        // Hook gantries across the lane: girder posts, two hook rails, chassis hanging from the hooks above head height.
        Transform gantry = Group("Hook Gantries");
        for (int i = 0; i < 6; i++) {
            float d = 24f + i * 15f;
            for (int s = -1; s <= 1; s += 2) {
                Vector3 post = At(d, s * 4.2f);
                LevelDress.PutEuler(gantry, "fl_girder", post + Vector3.up * 1.5f, new Vector3(0f, bearing, 90f), 1f);
                LevelDress.PutEuler(gantry, "fl_girder", post + Vector3.up * 4.5f, new Vector3(0f, bearing, 90f), 1f);
            }
            for (int k = -1; k <= 1; k += 2) {
                Vector3 mid = At(d, k * 1.5f);
                GameObject rail = LevelDress.Put(gantry, "fl_hook_rail", mid + Vector3.up * 3.4f, bearing + 90f, 1f);
                rail.name = "Hook Rail";
                for (int h = 0; h < 2; h++) {
                    Vector3 hook = At(d + (h == 0 ? -0.75f : 0.75f), k * 1.5f + (h == 0 ? -0.25f : 0.25f));
                    LevelDress.Put(gantry, "fl_hung_chassis", hook + Vector3.up * 2.5f, bearing + (i + h) * 37f, 1f);
                }
            }
        }
        Features.Add("landmark:six hook gantries arching the route, hung with un-built Yo Bot chassis");

        // Racks and pipe runs along the lip, barrels and the maintenance catwalk raised on girders.
        Transform lip = Group("The Lip");
        for (int i = 0; i < 9; i++) {
            float d = 20f + i * 10f;
            float side = (i % 2 == 0 ? 1f : -1f) * 10.5f;
            Vector3 rackAt = At(d, side);
            rackAt.y = Lowest(rackAt, Yaw(side > 0f ? -90f : 90f), 1.16f, 0.57f) - 0.02f;   // all four feet down
            LevelDress.Put(lip, "fl_chassis_rack", rackAt, Yaw(side > 0f ? -90f : 90f), 1.3f);
        }
        for (int i = 0; i < 10; i++) {
            float d = 18f + i * 9f;
            LevelDress.Put(lip, "fl_pipe_run", At(d, i % 2 == 0 ? -13f : 14f), bearing + 90f + (i % 3) * 3f, 1.4f);
            LevelDress.Put(lip, "fl_barrel", At(d + 2f, i % 2 == 0 ? -12f : 12.8f), rng.Next(0, 360), 1f + (float)rng.NextDouble() * 0.2f);
            if (i % 3 == 0) {
                LevelDress.Put(lip, "fl_barrel", At(d + 3.1f, i % 2 == 0 ? -12.6f : 13.2f), rng.Next(0, 360), 1.1f);
            }
        }
        for (int k = 0; k < 8; k++) {
            Vector3 c = At(34f + k * 3f, -19f);
            LevelDress.Put(lip, "fl_catwalk", c + Vector3.up * 3f, bearing + 90f, 1f);
            if (k % 3 == 0) {
                LevelDress.PutEuler(lip, "fl_girder", c + Vector3.up * 1.5f + right * -0.3f, new Vector3(0f, bearing, 90f), 1f);
            }
        }
        LevelDress.Put(lip, "fl_cage_lamp", At(40f, -19.6f) + Vector3.up * 4.2f, Yaw(-90f), 2f);
        LevelDress.PointLight(lip, At(40f, -17f) + Vector3.up * 5f, lamp, 12f, 1.1f);

        // Two belts of conveyors beside the lane carrying cleared things: a hat, a clock, a bell.
        string[] things = { "fl_belt_hat", "fl_belt_clock", "fl_belt_bell" };
        for (int side = -1; side <= 1; side += 2) {
            Transform b = Group("Belt " + (side < 0 ? "West" : "East"));
            float from = side < 0 ? 45f : 60f, to = side < 0 ? 95f : 90f;
            // Each 3 m conveyor lies along the floor's slope; the belt's top (slat tops at 0.7 m) is read off each one at both ends,
            // so the items ride it up and down instead of one height for 50 m of uneven floor (they hovered or sank into the belt).
            // Local +x points back toward the circle (yaw bearing + 90): a conveyor starts at x = 1.5 and ends at x = -1.5.
            var beltFrom = new List<float>();
            var beltTo = new List<float>();
            for (float d = from; d < to; d += 3f) {
                GameObject conveyor = Lay(LevelDress.Put(b, "fl_conveyor", At(d + 1.5f, side * 17f), bearing + 90f, 1f), bearing + 90f, 1.5f, 0.5f, -0.02f);
                // 2 cm over the slats so a hat's wide brim clears a belt that tilts under it.
                beltFrom.Add(conveyor.transform.TransformPoint(new Vector3(1.5f, 0.7f, 0f)).y + 0.02f);
                beltTo.Add(conveyor.transform.TransformPoint(new Vector3(-1.5f, 0.7f, 0f)).y + 0.02f);
            }
            float span = to - from;
            Vector3 mid = At(from + span * 0.5f, side * 17f);
            for (int k = 0; k < 6; k++) {
                GameObject item = LevelDress.Put(b, things[k % 3], mid + Vector3.up * 0.78f, bearing + k * 40f, 1.5f);
                var mover = item.AddComponent<ChapterMap.MapMover>();
                mover.Set(ChapterMap.MapMover.Mode.Belt, 0f, 0f, side < 0 ? 3f : -3f, (k / 6f) * span);
                mover.SetLine(mid + Vector3.up * 0.78f, axis, span);
                mover.SetSurface(beltFrom.ToArray(), beltTo.ToArray(), 3f);
                belts.Add(mover);
            }
        }
        Features.Add("landmark:two belts of cleared things (a hat, a clock, a bell) sliding along the route");

        // The rim: smokestacks with glowing mouths, standing on the yard's terraced walls (the skirt, 255-275 m out). A fixed y = 24
        // left the 255 m ones 5 m over the terrace and sank the 275 m ones a metre; each 9-11 m plinth now goes on the lowest of the
        // drawn terrace under it, a little in.
        Transform stacks = Group("Smokestacks");
        for (int i = 0; i < 7; i++) {
            float b = 20f + i * 51f;
            float scale = 1f + (i % 3) * 0.12f;
            Vector3 p = Polar(b, 255f + (i % 2) * 20f);
            p.y = LowestTerrain(p, 5.5f * scale) - 0.1f;
            LevelDress.Put(stacks, "fl_stack", p, b, scale);
        }

        // Three presses beside the lane, slamming on red marks in turn.
        Transform crush = Group("Crushers");
        for (int i = 0; i < 3; i++) {
            float d = 38f + i * 24f;
            int side = i % 2 == 0 ? 1 : -1;
            // The frame lies along the floor's slope (level, one 8 m side's feet hung in the air), and its head runs square to it.
            GameObject frame = Lay(LevelDress.Put(crush, "fl_press_frame", At(d, side * 6.5f), bearing, 1f), bearing, 3.9f, 3.1f, -0.02f);
            Vector3 at = frame.transform.position;
            Vector3 up = frame.transform.up;
            LevelDress.Tint(frame, 1, red * 0.7f);
            GameObject head = LevelDress.Put(crush, "fl_press_head", at + up * 7f, bearing, 1f);
            head.transform.rotation = frame.transform.rotation;
            var s = head.AddComponent<Slammer>();
            s.Track = at;
            s.Guide = up;
            s.High = 7f;
            s.Low = 0.2f;
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
        if (lens != null) {
            lens.SetColor("_Color", new Color(0.25f, 0.05f, 0.05f, 1f));
            lens.SetColor("_EmissionColor", new Color(0.1f, 0.01f, 0.01f, 1f));
        }
        Thud(1f);
        GameAudio.Play("belt_stop", 0.9f);
        Fx.Ring(leverAt + Vector3.up * 0.3f, 30f, new Color(1f, 0.7f, 0.3f, 0.8f), 0.8f);
        Beat("belt-stopped|belts=" + belts.Count + "|crushers=" + crushers.Count);
    }
}
