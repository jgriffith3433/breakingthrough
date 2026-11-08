using System.Collections.Generic;
using UnityEngine;

// The maps of chapters 4 to 6, each in its own Unity scene (Assets/Maps/Map_Walk, Map_Reading, Map_Filed), built by OwnMap on load
// from the Model Kit exactly like chapters 2 and 3 (OwnMapChapters.cs; Docs/Levels4to6AndHub.md has the design, Docs/CampaignBible.md
// the map design sheets). Each also has a BOSS ARENA away from the route, built from arena kit pieces (OwnMap.DeclareArena names the
// spot the boss walks out of). The hub office is OfficeMap (OwnMapOffice.cs). Nothing here has a collider and nothing here is a
// built-in primitive: SceneValidation fails a scene that holds one.

// ---------------------------------------------------------------------------------------------- Chapter 4: the Long Walk
// A causeway of pale slabs over a floor of mist under a cold slate sky: railings either side with bollards and urns between, six
// lamp stilts in pairs at the three stops and six more between them (each lamp head has three folded legs), mist banks drifting
// along it, the last slabs broken off and fallen (hazard). Pale spires stand up out of the mist all round (three silhouettes). On the
// far side of the circle from the causeway (bearing 150) is the Servers' Dais: a tiered round dais with a violet ring inlaid in it,
// twelve folded-leg pillars and the paper stand. Set piece, The Paper Is Out: whenever The Server holds its paper out (Server.Offering)
// the stand's paper lifts and its lamp burns violet.
public class WalkMap : OwnMap {
    public override string Key { get { return "long-walk"; } }
    Transform paper;
    Light standLamp;
    float raise, lampBoost;
    int seenOffers;
    bool forced;
    float forcedUntil;
    float bearing;
    readonly System.Random rng = new System.Random(41);

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static float GroundY(float x, float z) {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, out hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return 0f;
    }

    // A point on the causeway's own grid: `along` metres out on its bearing, `side` metres to its right, on the ground.
    Vector3 At(float along, float side) {
        Vector3 p = Polar(bearing, along) + Quaternion.Euler(0f, bearing, 0f) * new Vector3(side, 0f, 0f);
        p.y = GroundY(p.x, p.z);
        return p;
    }

    protected override void Build() {
        Story.Stop[] stops = chapter.Stops;
        bearing = stops != null && stops.Length > 0 ? stops[stops.Length - 1].Bearing : 330f;
        float length = stops != null && stops.Length > 0 ? stops[stops.Length - 1].Dist + 24f : 120f;
        Color violet = C(0.48f, 0.36f, 0.78f), lampViolet = C(0.62f, 0.4f, 1f);
        gameObject.AddComponent<ArenaHider>();

        // The pit: the ground falls away past the playfield into mist; far cliffs stand up round it.
        Terrain("Pit of Mist",
            (deg, d) => -Mathf.SmoothStep(0f, 1f, (d - 206f) / 38f) * 46f + Mathf.SmoothStep(0f, 1f, (d - 400f) / 140f) * (60f + 40f * Noise(deg * 2f, d, 8f)),
            (deg, d, h) => Color.Lerp(C(0.62f, 0.66f, 0.76f), C(0.12f, 0.14f, 0.22f), Mathf.Clamp01((h + 44f) / 40f)) * (0.94f + 0.1f * Noise(deg * 7f, d, 9f)));
        SetSky(C(0.025f, 0.035f, 0.08f), C(0.3f, 0.34f, 0.5f), C(0.2f, 0.22f, 0.32f), C(0.82f, 0.84f, 0.95f), new Vector3(0.45f, 0.28f, -0.85f), 0.012f, 0.65f);
        MakeMusic(14f, new[] { 130.8f, 196f, 293.7f, 392f }, 3f, 0f, 0f, 3.5f, 523.3f, 0.27f);
        LevelDress.GroundSkin(Group("Mist Floor"), "Mist Floor", center, 205f, 5f, LevelDress.GroundMaterial("walk", "stone", C(0.32f, 0.38f, 0.56f)), 9f);

        // The pale spires standing out of the mist: needles, banded towers and stepped obelisks.
        Transform spires = Group("Pale Spires");
        string[] spireIds = { "wk_spire_a", "wk_spire_b", "wk_spire_c" };
        for (int i = 0; i < 34; i++) {
            float b = (float)rng.NextDouble() * 360f;
            float d = 250f + (float)rng.NextDouble() * 150f;
            Vector3 p = Polar(b, d, -30f);
            LevelDress.Put(spires, spireIds[i % 3], p, b + i * 37f, 0.85f + (float)rng.NextDouble() * 0.35f);
        }

        // The causeway: three slabs wide, railings with bollards and urns between, the last bays broken off.
        Transform route = Group("Causeway");
        float broken = length - 10f;
        for (float d = 14f; d < length; d += 2f) {
            for (int c = -1; c <= 1; c++) {
                if (d > broken && (rng.Next(0, 3) != 0 || c == 0)) {
                    continue;
                }
                GameObject slab = LevelDress.Put(route, "wk_causeway_slab", At(d, c * 2f) + Vector3.down * (0.31f - (float)rng.NextDouble() * 0.03f), rng.Next(0, 2) * 180f);
                slab.name = "Slab";
            }
        }
        for (float d = 16f; d < broken; d += 4f) {
            foreach (float side in new[] { -3.4f, 3.4f }) {
                LevelDress.Put(route, "wk_railing", At(d, side) + Vector3.up * 0.01f, bearing - 90f);
                if (((int)(d / 4f) % 3) == 0) {
                    LevelDress.Put(route, "wk_mist_urn", At(d + 2f, side * 1.08f) + Vector3.up * 0.01f, rng.Next(0, 360));
                } else {
                    LevelDress.Put(route, "wk_mooring_bollard", At(d + 2f, side * 1.04f) + Vector3.up * 0.01f, rng.Next(0, 360));
                }
            }
        }
        // The circle's plaza: a ring of mooring bollards and urns round Slice.
        for (int i = 0; i < 12; i++) {
            float b = i * 30f + 15f;
            Vector3 p = Polar(b, 12f);
            p.y = GroundY(p.x, p.z) + 0.1f;
            LevelDress.Put(route, i % 2 == 0 ? "wk_mooring_bollard" : "wk_mist_urn", p, b);
        }
        Features.Add("route:the causeway of pale slabs out from the circle along bearing " + bearing + ", railed on both sides, through " + (stops != null ? stops.Length : 3) + " lamp stops to " + (stops != null && stops.Length > 0 ? stops[stops.Length - 1].Name : "the last lamp"));

        // Mist banks drifting along the road and lying out in the pit.
        Transform mist = Group("Mist Banks");
        for (int i = 0; i < 9; i++) {
            LevelDress.Put(mist, "wk_mist_bank", At(18f + i * 13f, (i % 2 == 0 ? 1 : -1) * (11f + (i % 3) * 3f)) + Vector3.up * 0.05f, bearing + i * 20f, 0.9f + (i % 3) * 0.1f);
        }
        for (int i = 0; i < 16; i++) {
            float b = i * 22.5f + (float)rng.NextDouble() * 10f;
            Vector3 p = Polar(b, 120f + (float)rng.NextDouble() * 80f);
            p.y = GroundY(p.x, p.z) + 0.05f;
            LevelDress.Put(mist, "wk_mist_bank", p, b * 3f, 1f + (float)rng.NextDouble() * 0.2f);
        }

        // Lamp stilts: a pair at each stop (with a violet light), and a pair between them; each head has three folded legs.
        Transform stilts = Group("Lamp Stilts");
        if (stops != null) {
            for (int i = 0; i < stops.Length; i++) {
                foreach (float side in new[] { -5f, 5f }) {
                    LevelDress.Put(stilts, "wk_lamp_stilt", At(stops[i].Dist, side), bearing + side * 4f);
                }
                LevelDress.PointLight(stilts, At(stops[i].Dist, 0f) + Vector3.up * 5f, lampViolet, 34f, 1.5f);
                if (i > 0) {
                    float mid = (stops[i].Dist + stops[i - 1].Dist) * 0.5f;
                    foreach (float side in new[] { -5f, 5f }) {
                        LevelDress.Put(stilts, "wk_lamp_stilt", At(mid, side), bearing + 180f + side * 4f);
                    }
                }
            }
        }
        Features.Add("landmark:twelve lamp stilts in pairs along the causeway and at the three stops, one lamp for every leg, each head with three folded legs");

        // Off the road: the middle stop's bench and arrow sign, a mooring pier stub with its urn.
        Transform rest = Group("Middle Lamp Rest");
        LevelDress.Put(rest, "wk_stone_bench", At(64f, 8.4f) + Vector3.up * 0.1f, bearing + 90f, 1.1f);
        LevelDress.Put(rest, "wk_arrow_sign", At(58f, 7.2f) + Vector3.up * 0.1f, bearing + 200f, 1.1f);
        LevelDress.Put(rest, "wk_mist_urn", At(61f, 9.6f) + Vector3.up * 0.1f, 20f);
        LevelDress.Put(rest, "wk_pier_post", At(80f, -9.4f) + Vector3.up * 0.1f, bearing - 90f);
        LevelDress.Put(rest, "wk_mist_urn", At(80f, -12.4f) + Vector3.up * 0.7f, 60f);
        LevelDress.Put(rest, "wk_pylon", At(30f, 12f) + Vector3.up * 0.1f, bearing, 1.2f);
        LevelDress.PointLight(rest, At(64f, 8.4f) + Vector3.up * 3f, lampViolet, 12f, 0.8f);

        // The last slabs have broken off and fallen into the pit.
        Transform gap = Group("Fallen Slabs");
        for (int i = 0; i < 7; i++) {
            Vector3 p = At(broken + 1f + i * 1.7f, ((i % 3) - 1) * 2.4f) + Vector3.down * (1.4f + i * 0.5f);
            LevelDress.PutEuler(gap, "wk_fallen_slab", p, new Vector3(i * 11f, bearing + i * 29f, i * 7f));
        }
        Features.Add("hazard:the pit of mist under the causeway with its last slabs fallen in: no cover and no edge to hide behind, and Servers appear only at the lamps");

        // The boss arena, on the far side of the circle from the causeway: the Servers' Dais.
        float arena = (bearing + 180f) % 360f;
        Transform dais = DeclareArena("Servers' Dais", arena, 78f);
        Vector3 at = Polar(arena, 78f);
        at.y = LevelDress.GroundY(at.x, at.z, at.y);
        GameObject floor = LevelDress.Put(dais, "ar_server_dais", at + Vector3.up * 0.02f, arena);
        DaisFloor(dais, at, 23.5f, 0.92f);
        LevelDress.Tint(floor, 0, C(0.6f, 0.62f, 0.78f));
        LevelDress.Tint(floor, 1, violet * 1.4f);
        for (int i = 0; i < 12; i++) {
            float a = arena + 15f + i * 30f;
            Vector3 p = at + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 24.8f;
            LevelDress.Put(dais, "ar_server_pillar", p, a + 180f);
        }
        Transform stand = Group("Paper Stand");
        LevelDress.Put(stand, "wk_paper_stand", at + Vector3.up * 0.9f, bearing);
        paper = LevelDress.Put(stand, "wk_paper_sheet", at + Vector3.up * 4.25f, bearing).transform;
        standLamp = Lamp(stand, at + Vector3.up * 8f, violet, 30f, 0.6f);
        Lamp(dais, at + Vector3.up * 12f, violet * 0.9f, 60f, 0.7f);
        Features.Add("arena:the Servers' Dais, a tiered round dais with a violet ring inlaid in it and twelve folded-leg pillars at 78 m on bearing " + arena + ", the far side of the circle from the causeway, with a paper stand where The Server waits");
        Features.Add("setpiece:The Paper Is Out: the stand's paper lifts and its lamp burns violet whenever The Server holds its paper out to be refused");
        seenOffers = Server.Offers;
    }

    public override void Trigger() {
        forced = true;
        forcedUntil = Time.time + 2.5f;
        Beat("paper-out|forced");
    }

    void Update() {
        if (paper == null) {
            return;
        }
        if (Server.Offers < seenOffers) {
            seenOffers = Server.Offers;
        }
        if (Server.Offers > seenOffers) {
            seenOffers = Server.Offers;
            Beat("paper-out|offer " + seenOffers);
            Fx.Ring(paper.position - Vector3.up * 2.8f, 16f, new Color(0.55f, 0.4f, 0.9f, 0.8f), 1f);
        }
        bool out_ = Server.Offering || (forced && Time.time < forcedUntil);
        lampBoost = Mathf.MoveTowards(lampBoost, out_ ? 1f : 0f, Time.deltaTime * 2f);
        raise = Mathf.MoveTowards(raise, out_ ? 1f : 0f, Time.deltaTime * 1.2f);
        Vector3 p = paper.position;
        float baseY = BossSpot.y + 4.25f;
        p.y = baseY + raise * 1.4f;
        paper.position = p;
        standLamp.intensity = 0.6f + lampBoost * 2.6f;
    }
}

// ---------------------------------------------------------------------------------------------- Chapter 5: the Reading Room
// A vast round hall under a ribbed vault, built from kit pieces: a ring of 24 wall bays with tall arched windows (33 m wide, 34 m
// high), a colonnade of twelve 30 m columns, six chandeliers hanging from the vault, ten racks of identical forms against the wall in
// five sections (one per page; as each page's wave falls that section's racks are swapped for bare ones, PageRead) and, at the fifth,
// the Registrar's Gallery rises out of the floor behind the desk. A red carpet aisle runs from the white door (bearing 180) between
// files of bookshelves and reading desks to the empty desk beside the circle (brass inlaid in the parquet); five page lecterns round
// the circle lift and settle their pages in turn (hazard, telegraphed). The Gallery (arena, bearing 0, 80 m) is three ringed steps
// round the Great Ledger, with a rail and tall columns.
public class ReadingMap : OwnMap {
    public override string Key { get { return "reading-room"; } }
    const float Door = 180f;
    const float HallRadius = 125f;
    const int Pages = 5;
    readonly GameObject[][] racks = new GameObject[Pages][];
    readonly List<ChapterMap.MapMover> pages = new List<ChapterMap.MapMover>();
    Transform gallery;
    Transform columns;
    float galleryRise = -9f;
    bool open;
    int blanked;
    readonly System.Random rng = new System.Random(55);

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    // A point on the hall floor: `along` metres from the circle toward the door, `side` metres to the right of the aisle.
    Vector3 Aisle(float along, float side) {
        float a = Door * Mathf.Deg2Rad;
        Vector3 axis = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        Vector3 right = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
        Vector3 p = center + axis * along + right * side;
        p.y = LevelDress.GroundY(p.x, p.z, center.y) + 0.01f;
        return p;
    }

    // A point at its ground height (Polar alone is y = 0 over a floor that rises and falls with the bowl).
    static Vector3 G(Vector3 p, float lift = 0f) {
        p.y = LevelDress.GroundY(p.x, p.z, p.y) + lift;
        return p;
    }

    protected override void Build() {
        gameObject.AddComponent<ArenaHider>();
        SetSky(C(0.04f, 0.035f, 0.025f), C(0.22f, 0.17f, 0.09f), C(0.08f, 0.06f, 0.04f), C(0.9f, 0.72f, 0.4f), new Vector3(0f, 0.9f, 0.1f), 0.01f, 0.4f);
        MakeMusic(16f, new[] { 98f, 146.8f, 196f, 246.9f, 293.7f }, 4f, 0f, 0f, 8f, 392f, 0.26f);

        // The floor: parquet, baked from the live ground.
        LevelDress.GroundSkin(Group("Hall Floor"), "Parquet Floor", center, 138f, 6f, LevelDress.GroundMaterial("reading", "wood", C(0.62f, 0.5f, 0.38f)), 5f);

        // The hall: a ring of wall bays, a colonnade of columns, a ribbed vault and chandeliers.
        Transform hall = Group("Hall");
        const int bays = 24;
        for (int i = 0; i < bays; i++) {
            float b = (i + 0.5f) / bays * 360f;
            LevelDress.Put(hall, "rd_hall_wall", Polar(b, HallRadius), b + 180f);
        }
        for (int i = 0; i < 12; i++) {
            float b = 15f + i * 30f;
            LevelDress.Put(hall, "rd_hall_column", Polar(b, 112f), b);
        }
        LevelDress.Put(hall, "rd_vault", center, 0f);
        Transform lights = Group("Chandeliers");
        for (int i = 0; i < 6; i++) {
            float b = i * 60f + 30f;
            LevelDress.Put(lights, "rd_hall_chandelier", Polar(b, 58f, 10f), b);
            LevelDress.PointLight(lights, Polar(b, 58f, 9f), C(1f, 0.82f, 0.5f), 90f, 1.3f);
        }
        LevelDress.PointLight(lights, Polar(0f, 0f, 30f), C(1f, 0.9f, 0.7f), 70f, 0.8f);

        // The circle drawn in brass on the parquet.
        LevelDress.Put(Group("Circle"), "rd_circle_inlay", center + Vector3.up * 0.03f, 0f);

        // Forms on the wall: ten racks in five sections, two to a page. Each section's racks are swapped for bare ones as its page is read.
        Transform wall = Group("Wall Racks of Forms");
        for (int s = 0; s < Pages; s++) {
            racks[s] = new GameObject[2];
            for (int k = 0; k < 2; k++) {
                float b = s * 72f + 20f + k * 32f;
                racks[s][k] = LevelDress.Put(wall, "rd_form_rack", G(Polar(b, HallRadius - 1.8f), 1.5f), b + 180f);
                racks[s][k].name = "Form Rack " + (s + 1) + "." + (k + 1);
            }
        }
        Features.Add("landmark:ten racks of identical forms lining the wall in five sections, one per page; each section turns blank as its page is read");

        // The carpet aisle between files of bookshelves and reading desks, the empty desk and the white door.
        Transform aisle = Group("Carpet Aisle");
        for (float d = 9f; d < HallRadius - 8f; d += 3f) {
            for (int c = -1; c <= 1; c++) {
                LevelDress.Tilt(LevelDress.Put(aisle, "rd_runner", Aisle(d, c * 1.2f), Door));
            }
        }
        Transform files = Group("Shelves and Desks");
        for (float d = 28f; d < HallRadius - 14f; d += 4f) {
            foreach (int side in new[] { -1, 1 }) {
                LevelDress.Put(files, "rd_bookshelf", Aisle(d, side * 6.6f), Door + side * 90f, 1f + (float)rng.NextDouble() * 0.15f);
            }
        }
        for (float d = 32f; d < HallRadius - 20f; d += 8f) {
            foreach (int side in new[] { -1, 1 }) {
                Vector3 p = Aisle(d, side * 11f);
                LevelDress.Put(files, "rd_reading_desk", p, Door + side * 90f + (float)rng.Next(-8, 8));
                LevelDress.Put(files, "of_desk_lamp", p + Vector3.up * 0.8f, Door + side * 90f + 40f);
            }
        }
        Vector3 deskAt = G(center + new Vector3(6f, 0f, 3f));
        Transform desk = Group("Empty Desk");
        LevelDress.Put(desk, "of_desk", deskAt + new Vector3(-0.85f, 0f, 0f), Door);
        LevelDress.Put(desk, "of_desk", deskAt + new Vector3(0.85f, 0f, 0f), Door);
        LevelDress.Put(desk, "of_chair", deskAt + new Vector3(0.4f, 0f, 1.5f), 160f);
        LevelDress.Put(desk, "of_desk_lamp", deskAt + new Vector3(-0.85f, 0.77f, 0.1f), 120f);
        LevelDress.PointLight(desk, deskAt + Vector3.up * 2f, C(1f, 0.82f, 0.5f), 14f, 0.9f);
        Transform doorG = Group("White Door");
        Vector3 doorAt = G(Polar(Door, HallRadius - 3f));
        LevelDress.Put(doorG, "rd_white_door", doorAt, 0f);
        LevelDress.PointLight(doorG, doorAt + new Vector3(0f, 12f, 6f), C(1f, 0.95f, 0.85f), 50f, 1f);
        LevelDress.PointLight(doorG, Aisle(HallRadius - 16f, 0f) + Vector3.up * 3f, C(1f, 0.3f, 0.25f), 24f, 0.5f);
        Features.Add("route:the red carpet aisle from the white door on bearing 180 straight in between files of shelves and reading desks to the empty desk by the circle");

        // Five page lecterns round the circle: each page lifts and settles in turn.
        Transform lect = Group("Page Lecterns");
        for (int i = 0; i < Pages; i++) {
            float b = i * 72f + 36f;
            Vector3 p = G(Polar(b, 30f));
            LevelDress.Put(lect, "rd_lectern", p, b + 180f);
            GameObject page = LevelDress.PutEuler(lect, "rd_page", p + Vector3.up * 1.4f, new Vector3(0f, b + 180f, -22f));
            page.name = "Page " + (i + 1);
            var mover = page.AddComponent<ChapterMap.MapMover>();
            mover.Set(ChapterMap.MapMover.Mode.Press, p.y + 1.75f, p.y + 1.4f, 4f, i * 0.8f);
            pages.Add(mover);
        }
        Features.Add("hazard:five page lecterns round the circle, each page lifting and settling in turn (telegraphed), and the forms going blank page by page");

        // The Registrar's Gallery: three ringed steps round the Great Ledger, a rail with a gap toward the circle and tall columns;
        // sunk under the floor until the fifth page is read.
        Transform arena = DeclareArena("Registrar's Gallery", 0f, 80f);
        gallery = arena;
        Vector3 at = Polar(0f, 80f);
        at.y = LevelDress.GroundY(at.x, at.z, at.y);
        LevelDress.Put(arena, "ar_gallery_dais", at + Vector3.up * 0.02f, 0f);
        DaisFloor(arena, at, 23.5f, 1.52f);
        LevelDress.Put(arena, "rd_great_ledger", at + Vector3.up * 2f, 0f, 1.8f);
        var colGo = new GameObject("Gallery Columns");
        colGo.transform.SetParent(arena, false);
        columns = colGo.transform;
        for (int i = 0; i < 8; i++) {
            float a = i * 45f + 22.5f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 30f) {
                continue;
            }
            LevelDress.Put(columns, "rd_hall_column", at + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 26.5f, a);
        }
        columns.gameObject.SetActive(false);   // the tall columns stand up with the Gallery, after the fifth page
        Lamp(arena, at + Vector3.up * 14f, C(1f, 0.85f, 0.5f), 60f, 1.1f);
        Features.Add("arena:the Registrar's Gallery, three ringed steps round the Great Ledger with a rail and tall columns at 80 m on bearing 0, opposite the door; it rises out of the floor after the fifth page");
        Features.Add("setpiece:The Forms Turn: as each page's wave falls its section of forms on the wall turns blank, and after the fifth the whole room is bare and the Gallery rises behind the desk");
        gallery.position = Vector3.down * 9f;
    }

    // One page read: its racks go bare. After the last the Gallery rises.
    public override void PageRead(int page) {
        if (page < 1 || page > Pages) {
            return;
        }
        Blank(page - 1);
        blanked = Mathf.Max(blanked, page);
        Beat("forms-blank|" + page);
        GameAudio.Play("page_turn", 0.7f, 0.95f + page * 0.03f);   // the forms turning over are heard, not just seen
        Fx.Ring(Polar(0f, 30f, 0.3f), 40f, new Color(0.95f, 0.85f, 0.55f, 0.7f), 0.9f);
        if (page >= Pages) {
            open = true;
        }
    }

    // The section's racks are swapped for the same frames with the forms taken off (rd_form_rack_blank), in place.
    void Blank(int section) {
        for (int k = 0; k < racks[section].Length; k++) {
            GameObject old = racks[section][k];
            if (old == null || old.name.EndsWith(" (blank)")) {
                continue;
            }
            GameObject bare = LevelDress.Put(old.transform.parent, "rd_form_rack_blank", old.transform.position, old.transform.eulerAngles.y);
            bare.name = old.name + " (blank)";
            Destroy(old);
            racks[section][k] = bare;
        }
    }

    // The playtest's setup shortcut: the five pages read at once, then the Gallery rises.
    public override void Trigger() {
        for (int i = 1; i <= Pages; i++) {
            PageRead(i);
        }
    }

    void Update() {
        if (gallery == null) {
            return;
        }
        galleryRise = Mathf.MoveTowards(galleryRise, open ? 0f : -9f, Time.deltaTime * 3f);
        if (open && !columns.gameObject.activeSelf) {
            columns.gameObject.SetActive(true);
        }
        Vector3 p = gallery.position;
        p.y = galleryRise;
        gallery.position = p;
    }
}

// ---------------------------------------------------------------------------------------------- Chapter 6: the filed Hollow
// The Moonlit Hollow of chapter 1, changed, at dawn-blue: bright rolling grass (a grass skin baked from the live ground), no pines, the
// Department's wall gone to broken stumps and rubble on the rim (190 m) with bare doorframes standing alone in the grass beyond, a pale
// slab path running out on bearing 180 to 180 m (banners every 25 m, a lantern on a plinth every 40 m) where it stops at nothing, the
// circle drawn in white and brighter, a gold seal hanging at the path's end. The Threshold (arena, bearing 20, 80 m) is a bare white
// doorframe on a two-step dais with a gold ring in its floor, away from the path, marked with the Order-Bearer's red stamp marks
// (hazard: they flash now and then). Set piece, The Door Number: when the chapter is filed (or by the setup shortcut) the white ring
// unrolls into a line of writing and a number.
public class FiledMap : OwnMap {
    public override string Key { get { return "hollow-filed"; } }
    const float PathBearing = 180f;
    readonly List<Transform> marks = new List<Transform>();
    readonly List<Vector3> ringAt = new List<Vector3>();
    readonly List<Vector3> lineAt = new List<Vector3>();
    readonly List<Vector3> stampAt = new List<Vector3>();
    Transform number;
    TextMesh numberText;
    Transform seal;
    float write;
    bool writing;
    float nextStamp = 6f;
    bool numbered;
    readonly System.Random rng = new System.Random(66);

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static float GroundY(float x, float z) {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, out hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return 0f;
    }

    protected override void Build() {
        Color ink = C(0.85f, 0.16f, 0.18f);
        System.Func<float, float, float> rise = (deg, d) => Mathf.SmoothStep(0f, 1f, (d - 225f) / 160f) * (6f + 14f * Noise(deg * 3f, d, 21f)) + 1.2f * Noise(deg * 5f, d * 0.8f, 22f) * Mathf.SmoothStep(0f, 1f, (d - 205f) / 20f);
        gameObject.AddComponent<ArenaHider>();
        Terrain("Dawn Grass", rise,
            (deg, d, h) => Color.Lerp(C(0.22f, 0.36f, 0.38f), C(0.5f, 0.6f, 0.72f), Mathf.Clamp01(h / 16f) * 0.7f + 0.2f * Noise(deg * 7f, d, 23f)));
        SetSky(C(0.07f, 0.12f, 0.3f), C(0.62f, 0.72f, 0.92f), C(0.2f, 0.28f, 0.45f), C(1f, 0.92f, 0.6f), new Vector3(0.1f, 0.12f, -0.99f), 0.03f, 0.55f);
        MakeMusic(14f, new[] { 110f, 164.8f, 220f, 277.2f, 329.6f }, 2f, 0f, 0f, 7f, 659.3f, 0.3f);
        LevelDress.GroundSkin(Group("Dawn Grass Skin"), "Dawn Grass Skin", center, 205f, 4f, LevelDress.GroundMaterial("filed", "grass", C(1.05f, 1.08f, 1f)), 7f);

        // Tufts of grass scattered about the circle and the path.
        Transform tufts = Group("Grass Tufts");
        for (int i = 0; i < 150; i++) {
            float b = (float)rng.NextDouble() * 360f;
            float d = 26f + (float)rng.NextDouble() * 150f;
            if (Mathf.Abs(Mathf.DeltaAngle(b, PathBearing)) < 4f && d > 28f) {
                continue;
            }
            Vector3 p = Polar(b, d);
            p.y = GroundY(p.x, p.z);
            LevelDress.Put(tufts, "fd_grass_tuft", p, rng.Next(0, 360), 0.9f + (float)rng.NextDouble() * 0.5f);
        }

        // Broken stumps of the Department wall with rubble at their feet, and the bare doorframes standing alone beyond them.
        Transform stumps = Group("Wall Stumps");
        for (int i = 0; i < 12; i++) {
            float b = 60f + i * 20f;
            Vector3 p = Polar(b, 190f);
            p.y = GroundY(p.x, p.z);
            LevelDress.PutEuler(stumps, "fd_wall_stump", p, new Vector3(0f, b + (i % 2 == 0 ? 6f : -6f), i % 2 == 0 ? 3f : -2f), 0.85f + (i % 3) * 0.12f);
            if (i % 3 == 0) {
                Vector3 r = Polar(b + 4f, 186f);
                r.y = GroundY(r.x, r.z);
                LevelDress.Put(stumps, "fd_rubble", r, b * 1.7f);
            }
        }
        Features.Add("landmark:the broken stumps of the Department wall round the rim at 190 m with rubble at their feet, a gap where it stood");
        Transform doors = Group("Freestanding Doorframes");
        for (int i = 0; i < 10; i++) {
            float b = 20f + i * 36f + (float)rng.NextDouble() * 14f;
            float d = 215f + (float)rng.NextDouble() * 110f;
            Vector3 p = Polar(b, d, rise(b, d));
            LevelDress.PutEuler(doors, "fd_free_door", p, new Vector3(0f, b + 180f + rng.Next(-20, 20), rng.Next(-4, 5)), 0.9f + (float)rng.NextDouble() * 0.3f);
        }

        // The pale path to nothing, banners and lanterns along it, and the gold seal at its end.
        Transform route = Group("Path to Nothing");
        Quaternion face = Quaternion.Euler(0f, PathBearing, 0f);
        for (float d = 30f; d <= 178f; d += 1.4f) {
            for (int c = -1; c <= 0; c++) {
                float side = (c + 0.5f) * 1.5f;
                Vector3 p = Polar(PathBearing, d + (c == 0 ? 0.7f : 0f)) + face * new Vector3(side, 0f, 0f);
                p.y = GroundY(p.x, p.z) + 0.01f;
                LevelDress.Put(route, "fd_pale_slab", p, rng.Next(0, 6) * 60f);
            }
        }
        for (int i = 0; i < 6; i++) {
            float d = 40f + i * 25f;
            float side = i % 2 == 0 ? 3.4f : -3.4f;
            Vector3 p = Polar(PathBearing, d) + face * new Vector3(side, 0f, 0f);
            p.y = GroundY(p.x, p.z);
            LevelDress.Put(route, "fd_banner", p, PathBearing + (side > 0f ? 90f : 270f) + rng.Next(-8, 8));
        }
        for (int i = 0; i < 4; i++) {
            float d = 60f + i * 40f;
            float side = i % 2 == 0 ? -3.1f : 3.1f;
            Vector3 p = Polar(PathBearing, d) + face * new Vector3(side, 0f, 0f);
            p.y = GroundY(p.x, p.z);
            LevelDress.Put(route, "fd_plinth", p, PathBearing);
            LevelDress.Put(route, "fd_lantern", p + Vector3.up * 0.96f, PathBearing);
            LevelDress.PointLight(route, p + Vector3.up * 1.4f, C(1f, 0.9f, 0.55f), 11f, 0.9f);
        }
        GameObject sealGo = LevelDress.PutEuler(route, "fd_gold_seal", Polar(PathBearing, 196f, 15f), new Vector3(90f, PathBearing + 180f, 0f));
        seal = sealGo.transform;
        LevelDress.PointLight(route, Polar(PathBearing, 190f, 15f), C(1f, 0.85f, 0.45f), 42f, 1.4f);
        Features.Add("route:the pale slab path out of the circle on bearing 180, run on to 180 m where it stops at nothing, banners and lanterns along it, a great gold seal hanging past its end");

        // The circle drawn in white, brighter than it has ever been: 36 marks that can unroll into a line of writing.
        Transform ring = Group("Brighter Circle");
        for (int i = 0; i < 36; i++) {
            float b = i * 10f;
            Vector3 p = Polar(b, 20f, 0.1f);
            Transform m = LevelDress.Put(ring, "fd_circle_mark", p, b + 90f).transform;
            marks.Add(m);
            ringAt.Add(p);
            lineAt.Add(Polar(0f, 0f, 0.1f) + new Vector3(-26f + i * 1.5f, 0f, 36f));
        }
        GameObject numberGo = new GameObject("Door Number");
        numberGo.transform.SetParent(root, false);
        number = numberGo.transform;
        number.position = Polar(0f, 0f, 9f) + new Vector3(0f, 0f, 36f);
        number.rotation = Quaternion.Euler(0f, 0f, 0f);
        numberText = numberGo.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        numberText.font = font;
        numberText.text = "1";
        numberText.characterSize = 2.2f;
        numberText.fontSize = 48;
        numberText.fontStyle = FontStyle.Bold;
        numberText.anchor = TextAnchor.MiddleCenter;
        numberText.alignment = TextAlignment.Center;
        numberText.color = new Color(1f, 0.92f, 0.6f, 0f);
        numberGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        Features.Add("landmark:the circle drawn in white round the guard, brighter than it has ever been");

        // The Threshold: a two-step dais with a gold ring, the bare white doorframe on it, pillars and lanterns round the rim, away
        // from the path, and the Order-Bearer's stamp marks in front of the frame.
        float tb = 20f;
        Transform arena = DeclareArena("The Threshold", tb, 80f);
        Vector3 at = Polar(tb, 80f);
        at.y = LevelDress.GroundY(at.x, at.z, at.y);
        Quaternion f = Quaternion.Euler(0f, tb, 0f);
        LevelDress.Put(arena, "ar_threshold_dais", at + Vector3.up * 0.02f, tb);
        DaisFloor(arena, at, 23.5f, 0.82f);
        GameObject frame = LevelDress.Put(arena, "fd_free_door", at + Vector3.up * 0.8f, tb + 180f, 1.15f);
        frame.name = "Threshold Doorframe";
        for (int i = 0; i < 8; i++) {
            float a = tb + 22.5f + i * 45f;
            Vector3 p = at + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 21f + Vector3.up * 0.8f;
            if (i % 2 == 0) {
                LevelDress.Put(arena, "fd_pillar", p, a);
            } else {
                LevelDress.Put(arena, "fd_plinth", p, a);
                LevelDress.Put(arena, "fd_lantern", p + Vector3.up * 0.96f, a);
            }
        }
        for (int i = 0; i < 4; i++) {
            Vector3 p = at + f * new Vector3((i - 1.5f) * 7f, 0.81f, -11f + (i % 2) * 3f);
            GameObject mark = LevelDress.Put(arena, "ar_stamp_mark", p, tb + (i - 1.5f) * 6f);
            mark.name = "Stamp Mark " + i;
            LevelDress.Tint(mark, 0, ink * 0.75f);
            stampAt.Add(p);
        }
        Lamp(arena, at + Vector3.up * 8f, C(1f, 0.95f, 0.8f), 44f, 1.1f);
        Features.Add("arena:the Threshold, a two-step dais with a gold ring and a bare white doorframe at 80 m on bearing 20, away from the path, where the Spider Bot that carries the order walks out");
        Features.Add("hazard:the Order-Bearer's stamp marks in front of the Threshold, which flash red every few seconds (Wallit standing on one flinches)");
        Features.Add("setpiece:The Door Number: when the chapter is filed the white ring unrolls into a line of writing and a number appears over the circle");
    }

    public override void Trigger() {
        writing = true;
    }

    void Update() {
        if (number == null) {
            return;
        }
        if (!writing && ChapterRun.Instance != null && ChapterRun.Instance.Completed) {
            writing = true;
        }
        if (writing) {
            write = Mathf.MoveTowards(write, 1f, Time.deltaTime / 4f);
            float k = Mathf.SmoothStep(0f, 1f, write);
            for (int i = 0; i < marks.Count; i++) {
                if (marks[i] == null) {
                    continue;
                }
                marks[i].position = Vector3.Lerp(ringAt[i], lineAt[i] + Vector3.up * 0.3f, k);
                marks[i].rotation = Quaternion.Euler(0f, Mathf.LerpAngle(marks[i].eulerAngles.y, 90f, k), 0f);
            }
            Color c = numberText.color;
            c.a = Mathf.Clamp01((write - 0.7f) / 0.3f);
            numberText.color = c;
            if (write >= 1f && !numbered) {
                numbered = true;
                GameAudio.Play("seal_crack", 0.7f);
                Beat("door-number|1");
                Fx.Ring(number.position - Vector3.up * 8f, 30f, new Color(1f, 0.92f, 0.6f, 0.8f), 1.6f);
            }
        }
        if (seal != null) {
            seal.position += Vector3.up * (Mathf.Sin(Time.time * 0.6f) * 0.4f * Time.deltaTime);
        }
        if (Time.time >= nextStamp) {
            nextStamp = Time.time + 7f;
            foreach (Vector3 p in stampAt) {
                Fx.Ring(p + Vector3.up * 0.2f, 8f, new Color(0.95f, 0.2f, 0.2f, 0.7f), 0.7f);
            }
            PlayerGrowth pl = PlayerGrowth.Instance;
            if (pl != null) {
                foreach (Vector3 p in stampAt) {
                    Vector3 d = pl.transform.position - p;
                    d.y = 0f;
                    if (d.magnitude <= 3.2f) {
                        pl.OnHit();
                        Log.Add("stamp-mark|wallit");
                        break;
                    }
                }
            }
        }
    }
}
