using System.Collections.Generic;
using UnityEngine;

// The maps of chapters 4 to 6 and the hub, each in its own Unity scene (Assets/Maps/Map_Walk, Map_Reading, Map_Filed, Map_Office),
// built by OwnMap on load exactly like chapters 1 to 3 (OwnMapChapters.cs). Docs/CampaignBible.md has the map design sheets they
// follow; Docs/Story.md ("The places") says what each one is for. Chapters 4 and 6 and 5 each also have a BOSS ARENA away from
// the route (OwnMap.Arena): a dais the boss walks out of. Nothing here has a collider.

// ---------------------------------------------------------------------------------------------- Chapter 4: the Long Walk
// A causeway of pale slabs over a pit of mist under a cold slate sky: parapets either side, six lamp stilts in pairs at the three
// stops (each lamp head has three folded legs), mist banks drifting along it, the last slabs fallen into the pit (hazard). The
// rim of the pit falls away into the fog all round and thin pale spires stand up out of it. On the far side of the circle from
// the causeway (bearing 150) is the Servers' Dais with its paper stand. Set piece, The Paper Is Out: whenever The Server holds
// its paper out (Server.Offering) the stand's paper lifts and its lamp burns violet.
public class WalkMap : OwnMap {
    public override string Key { get { return "long-walk"; } }
    Transform paper;
    Light standLamp;
    float raise, lampBoost;
    int seenOffers;
    bool forced;
    float forcedUntil;
    float bearing;

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        Story.Stop[] stops = chapter.Stops;
        bearing = stops != null && stops.Length > 0 ? stops[stops.Length - 1].Bearing : 330f;
        float length = stops != null && stops.Length > 0 ? stops[stops.Length - 1].Dist + 24f : 120f;
        Color slab = C(0.62f, 0.66f, 0.78f), slab2 = C(0.52f, 0.56f, 0.68f), parapet = C(0.55f, 0.58f, 0.7f), pit = C(0.17f, 0.21f, 0.31f);
        Color iron = C(0.1f, 0.12f, 0.2f), lamp = C(0.9f, 0.88f, 0.7f), paperColor = C(0.9f, 0.88f, 0.8f), violet = C(0.48f, 0.36f, 0.78f);
        Quaternion face = Quaternion.Euler(0f, bearing, 0f);

        // The pit: the ground falls away past the playfield into mist; far cliffs stand up round it.
        Terrain("Pit of Mist",
            (deg, d) => -Mathf.SmoothStep(0f, 1f, (d - 206f) / 38f) * 46f + Mathf.SmoothStep(0f, 1f, (d - 400f) / 140f) * (60f + 40f * Noise(deg * 2f, d, 8f)),
            (deg, d, h) => Color.Lerp(C(0.62f, 0.66f, 0.76f), C(0.12f, 0.14f, 0.22f), Mathf.Clamp01((h + 44f) / 40f)) * (0.94f + 0.1f * Noise(deg * 7f, d, 9f)));
        SetSky(C(0.025f, 0.035f, 0.08f), C(0.3f, 0.34f, 0.5f), C(0.2f, 0.22f, 0.32f), C(0.82f, 0.84f, 0.95f), new Vector3(0.45f, 0.28f, -0.85f), 0.012f, 0.65f);
        MakeMusic(14f, new[] { 130.8f, 196f, 293.7f, 392f }, 3f, 0f, 0f, 3.5f, 523.3f, 0.27f);

        Transform spires = Group("Pale Spires");
        var rng = new System.Random(41);
        for (int i = 0; i < 34; i++) {
            float b = (float)rng.NextDouble() * 360f;
            float d = 250f + (float)rng.NextDouble() * 150f;
            float h = 34f + (float)rng.NextDouble() * 50f;
            Vector3 p = Polar(b, d, -30f);
            Piece(spires, "Spire", p + Vector3.up * h * 0.5f, new Vector3(3.2f + (i % 3), h, 3.2f + (i % 3)), Color.Lerp(slab2, pit, 0.35f), b, 0f, Shape.Cylinder);
            Piece(spires, "Spire Cap", p + Vector3.up * (h + 0.6f), new Vector3(5.8f, 1.2f, 5.8f), slab, b, 0f, Shape.Cylinder);
        }

        // The causeway: slabs, parapets, mist.
        Transform route = Group("Causeway");
        Path(route, bearing, 14f, length, 5f, 4f, slab, slab2);
        for (float d = 16f; d <= length; d += 8f) {
            foreach (float side in new[] { -3.6f, 3.6f }) {
                Piece(route, "Parapet", Polar(bearing, d, 0.45f) + face * new Vector3(side, 0f, 0f), new Vector3(0.7f, 0.9f, 6.6f), parapet, bearing);
            }
        }
        Features.Add("route:the causeway of pale slabs out from the circle along bearing " + bearing + ", walled by low parapets, through " + (stops != null ? stops.Length : 3) + " lamp stops to " + (stops != null && stops.Length > 0 ? stops[stops.Length - 1].Name : "the last lamp"));
        Transform mist = Group("Mist Banks");
        for (int i = 0; i < 9; i++) {
            Piece(mist, "Mist Bank " + i, Polar(bearing, 18f + i * 13f, 0.7f) + face * new Vector3((i % 2 == 0 ? 1 : -1) * (11f + (i % 3) * 3f), 0f, 0f), new Vector3(15f, 1.6f, 11f), C(0.6f, 0.64f, 0.74f), bearing + i * 20f, 0f, Shape.Cylinder);
        }

        // Lamp stilts: a pair at each stop, each head with three folded legs.
        Transform stilts = Group("Lamp Stilts");
        if (stops != null) {
            for (int i = 0; i < stops.Length; i++) {
                foreach (float side in new[] { -5f, 5f }) {
                    Vector3 p = Polar(stops[i].Bearing, stops[i].Dist) + face * new Vector3(side, 0f, 0f);
                    Piece(stilts, "Stilt", p + Vector3.up * 4f, new Vector3(0.5f, 8f, 0.5f), iron, bearing);
                    Piece(stilts, "Stilt Lamp", p + Vector3.up * 8.8f, new Vector3(1.5f, 1.3f, 1.5f), lamp, bearing, 0f, Shape.Cylinder, true);
                    for (int leg = 0; leg < 3; leg++) {
                        float a = leg * 120f + side * 10f;
                        Vector3 foot = p + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.9f + Vector3.up * 7.7f;
                        Piece(stilts, "Folded Leg", foot, new Vector3(0.25f, 1.6f, 0.25f), iron, a, side > 0 ? 25f : -25f, Shape.Cone);
                    }
                }
                Lamp(stilts, Polar(stops[i].Bearing, stops[i].Dist, 9f), lamp, 34f, 1.4f);
            }
        }
        Features.Add("landmark:six lamp stilts in pairs at the three stops, one lamp for every leg, each head with three folded legs");

        // The last slabs have fallen into the pit.
        Transform gap = Group("Fallen Slabs");
        Vector3 gapAt = Polar(bearing, length - 6f);
        for (int i = 0; i < 5; i++) {
            Piece(gap, "Fallen Slab " + i, gapAt + face * new Vector3((i - 2f) * 2f, -1.4f - i * 0.6f, i * 1.6f), new Vector3(1.8f, 0.4f, 3f), pit, bearing + i * 14f, i * 11f);
        }
        Features.Add("hazard:the pit of mist under the causeway with its last slabs fallen in: no cover and no edge to hide behind, and Servers appear only at the lamps");

        // The boss arena, on the far side of the circle from the causeway.
        float arena = (bearing + 180f) % 360f;
        Arena("Servers' Dais", arena, 78f, 22f, slab2, parapet, violet * 0.7f);
        Transform stand = Group("Paper Stand");
        Vector3 at = Polar(arena, 78f);
        Piece(stand, "Stand Post", at + Vector3.up * 1.2f, new Vector3(0.4f, 2.4f, 0.4f), iron);
        Piece(stand, "Stand Table", at + Vector3.up * 2.4f, new Vector3(2.4f, 0.2f, 1.2f), iron, arena);
        paper = Piece(stand, "Stand Paper", at + Vector3.up * 3f, new Vector3(1.6f, 1.3f, 0.08f), paperColor, arena, 0f, Shape.Box, true).transform;
        standLamp = Lamp(stand, at + Vector3.up * 5f, violet, 30f, 0.6f);
        Features.Add("arena:the Servers' Dais, a round dais with twelve pillars at 78 m on bearing " + arena + ", the far side of the circle from the causeway, with a paper stand where The Server waits");
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
        float baseY = BossSpot.y + 3f;
        p.y = baseY + raise * 1.4f;
        paper.position = p;
        standLamp.intensity = 0.6f + lampBoost * 2.6f;
    }
}

// ---------------------------------------------------------------------------------------------- Chapter 5: the Reading Room
// A bare round hall under a dark vault. The wall is lined with forms in five sections, one per page; as each page's wave falls
// that section turns blank (PageRead), and at the fifth the Registrar's Gallery rises out of the floor behind the desk. A carpet
// aisle runs from the white door (bearing 180) to the desk beside the circle; five page lecterns round the circle lift and settle
// their pages in turn (hazard, telegraphed). The Gallery (arena, bearing 0, 80 m) is a ringed dais round the Great Ledger.
public class ReadingMap : OwnMap {
    public override string Key { get { return "reading-room"; } }
    const float Door = 180f;
    const float HallRadius = 125f;
    const int Pages = 5;
    readonly List<Renderer>[] forms = new List<Renderer>[Pages];
    readonly List<ChapterMap.MapMover> pages = new List<ChapterMap.MapMover>();
    Transform gallery;
    float galleryRise = -9f;
    bool open;
    int blanked;

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        Color carpet = C(0.5f, 0.1f, 0.12f), wood = C(0.25f, 0.18f, 0.1f), form = C(0.94f, 0.9f, 0.78f), brass = C(0.9f, 0.72f, 0.36f), stone = C(0.12f, 0.1f, 0.07f), wallC = C(0.2f, 0.17f, 0.11f);
        Quaternion face = Quaternion.Euler(0f, Door, 0f);
        SetSky(C(0.04f, 0.035f, 0.025f), C(0.22f, 0.17f, 0.09f), C(0.08f, 0.06f, 0.04f), C(0.9f, 0.72f, 0.4f), new Vector3(0f, 0.9f, 0.1f), 0.01f, 0.4f);
        MakeMusic(16f, new[] { 98f, 146.8f, 196f, 246.9f, 293.7f }, 4f, 0f, 0f, 8f, 392f, 0.26f);

        // The hall: a ring of wall panels, a vault of beams, a floor ring.
        Transform hall = Group("Hall");
        const int panels = 48;
        float panelLength = 2f * Mathf.PI * HallRadius / panels + 0.6f;
        for (int i = 0; i < panels; i++) {
            float b = (i + 0.5f) / panels * 360f;
            Piece(hall, "Hall Wall " + i, Polar(b, HallRadius, 17f), new Vector3(panelLength, 34f, 2f), wallC * (0.9f + 0.2f * (i % 2)), b + 90f);
        }
        Piece(hall, "Hall Floor Ring", Polar(0f, 0f, 0.06f) + Vector3.zero, new Vector3(HallRadius * 2f + 8f, 0.06f, HallRadius * 2f + 8f), stone, 0f, 0f, Shape.Cylinder);
        Piece(hall, "Vault", Polar(0f, 0f, 62f), new Vector3(HallRadius * 2.2f, 2f, HallRadius * 2.2f), stone, 0f, 0f, Shape.Cylinder);
        for (int i = 0; i < 12; i++) {
            float b = i * 30f;
            Piece(hall, "Vault Beam", Polar(b, HallRadius * 0.5f, 60.5f), new Vector3(1.6f, 1.6f, HallRadius), wood, b);
        }
        for (int i = 0; i < 8; i++) {
            Lamp(hall, Polar(i * 45f, HallRadius * 0.55f, 40f), C(1f, 0.82f, 0.5f), 90f, 1.3f);
        }

        // Forms on the wall: five sections of 2 rows x 8 forms, one section per page (that is ten racks, two to a page).
        Transform wall = Group("Wall Racks of Forms");
        for (int s = 0; s < Pages; s++) {
            forms[s] = new List<Renderer>();
            for (int row = 0; row < 2; row++) {
                for (int k = 0; k < 8; k++) {
                    float b = s * 72f + 4f + k * 8.5f;
                    GameObject f = Piece(wall, "Form " + (s + 1) + "." + row + "." + k, Polar(b, HallRadius - 1.6f, 9f + row * 12f), new Vector3(6.2f, 9f, 0.2f), form, b + 90f);
                    forms[s].Add(f.GetComponent<Renderer>());
                    Piece(wall, "Form Rule", Polar(b, HallRadius - 1.7f, 9f + row * 12f), new Vector3(5f, 0.4f, 0.1f), C(0.3f, 0.26f, 0.2f), b + 90f);
                }
            }
        }
        Features.Add("landmark:ten racks of identical forms lining the wall in five sections, one per page; each section turns blank as its page is read");

        // The carpet aisle, the empty desk and the white door.
        Transform aisle = Group("Carpet Aisle");
        Path(aisle, Door, 10f, HallRadius - 6f, 3.6f, 4f, carpet, carpet * 0.82f, 0.12f);
        Vector3 deskAt = Polar(0f, 0f) + new Vector3(6f, 0f, 3f);
        Transform desk = Group("Empty Desk");
        Piece(desk, "Desk Top", deskAt + Vector3.up * 1f, new Vector3(3.4f, 0.2f, 1.6f), wood, -20f);
        Piece(desk, "Desk Leg L", deskAt + Quaternion.Euler(0f, -20f, 0f) * new Vector3(-1.4f, 0.5f, 0f), new Vector3(0.2f, 1f, 1.3f), wood, -20f);
        Piece(desk, "Desk Leg R", deskAt + Quaternion.Euler(0f, -20f, 0f) * new Vector3(1.4f, 0.5f, 0f), new Vector3(0.2f, 1f, 1.3f), wood, -20f);
        Transform doorG = Group("White Door");
        Vector3 doorAt = Polar(Door, HallRadius - 2f);
        Piece(doorG, "Door Post L", doorAt + face * new Vector3(-4.2f, 7f, 0f), new Vector3(1.2f, 14f, 1.4f), form, Door);
        Piece(doorG, "Door Post R", doorAt + face * new Vector3(4.2f, 7f, 0f), new Vector3(1.2f, 14f, 1.4f), form, Door);
        Piece(doorG, "Door Lintel", doorAt + Vector3.up * 14.4f, new Vector3(10f, 1.4f, 1.4f), form, Door);
        Piece(doorG, "Door Leaf", doorAt + Vector3.up * 7f, new Vector3(7.2f, 14f, 0.4f), form * 0.95f, Door, 0f, Shape.Box, true);
        Features.Add("route:the carpet aisle from the white door on bearing 180 straight in to the empty desk by the circle");

        // Five page lecterns round the circle: each page lifts and settles in turn.
        Transform lect = Group("Page Lecterns");
        for (int i = 0; i < Pages; i++) {
            float b = i * 72f + 36f;
            Vector3 p = Polar(b, 30f);
            Piece(lect, "Lectern", p + Vector3.up * 0.8f, new Vector3(1.2f, 1.6f, 1.2f), wood, b);
            GameObject page = Piece(lect, "Page " + (i + 1), p + Vector3.up * 1.9f, new Vector3(1.4f, 0.1f, 1f), form, b, 20f, Shape.Box, true);
            var mover = page.AddComponent<ChapterMap.MapMover>();
            mover.Set(ChapterMap.MapMover.Mode.Press, 2.1f, 1.8f, 4f, i * 0.8f);
            pages.Add(mover);
        }
        Features.Add("hazard:five page lecterns round the circle, each page lifting and settling in turn (telegraphed), and the forms going blank page by page");

        // The Registrar's Gallery: a ringed dais round the Great Ledger, sunk under the floor until the fifth page is read.
        Transform arena = Arena("Registrar's Gallery", 0f, 80f, 22f, stone * 1.6f, wood, brass * 0.6f, 10);
        gallery = arena;
        Vector3 at = Polar(0f, 80f);
        Piece(arena, "Great Ledger", at + Vector3.up * 1.5f, new Vector3(5f, 3f, 3f), brass, 0f);
        Piece(arena, "Ledger Pages", at + Vector3.up * 3.1f, new Vector3(4.6f, 0.2f, 2.6f), form, 0f, 0f, Shape.Box, true);
        Features.Add("arena:the Registrar's Gallery, a ringed dais at 80 m on bearing 0 round the Great Ledger, opposite the door; it rises out of the floor after the fifth page");
        Features.Add("setpiece:The Forms Turn: as each page's wave falls its section of forms on the wall turns blank, and after the fifth the whole room is white and the Gallery rises behind the desk");
        gallery.position = Vector3.down * 9f;
    }

    // One page read: its forms go blank. After the last the Gallery rises.
    public override void PageRead(int page) {
        if (page < 1 || page > Pages) {
            return;
        }
        Blank(page - 1);
        blanked = Mathf.Max(blanked, page);
        Beat("forms-blank|" + page);
        Fx.Ring(Polar(0f, 30f, 0.3f), 40f, new Color(0.95f, 0.85f, 0.55f, 0.7f), 0.9f);
        if (page >= Pages) {
            open = true;
        }
    }

    void Blank(int section) {
        foreach (Renderer r in forms[section]) {
            if (r != null) {
                // The flat material reads vertex colour: swap the form's mesh for a dark one.
                var filter = r.GetComponent<MeshFilter>();
                filter.sharedMesh = DarkBox();
            }
        }
    }

    Mesh darkBox;

    Mesh DarkBox() {
        if (darkBox == null) {
            GameObject tmp = Piece(null, "tmp", Vector3.zero, Vector3.one, new Color(0.09f, 0.08f, 0.07f, 1f));
            darkBox = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        return darkBox;
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
        Vector3 p = gallery.position;
        p.y = galleryRise;
        gallery.position = p;
    }
}

// ---------------------------------------------------------------------------------------------- Chapter 6: the filed Hollow
// The Moonlit Hollow of chapter 1, changed, at dawn-blue: bright rolling grass, no pines, the Department's wall gone to broken
// stumps on the rim (190 m), a pale slab path running out on bearing 180 to 180 m where it stops at nothing, the circle drawn in
// white and brighter, a gold seal hanging at the path's end. The Threshold (arena, bearing 20, 80 m) is a bare white doorframe on
// a dais, away from the path, marked with the Order-Bearer's red stamp marks (hazard: they flash now and then). Set piece, The
// Door Number: when the chapter is filed (or by the setup shortcut) the white ring unrolls into a line of writing and a number.
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

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        Color pale = C(0.78f, 0.84f, 0.96f), stone = C(0.3f, 0.36f, 0.52f), white = C(0.97f, 0.97f, 0.92f), wall = C(0.14f, 0.17f, 0.26f), ink = C(0.85f, 0.16f, 0.18f), gold = C(0.95f, 0.8f, 0.4f);
        Terrain("Dawn Grass",
            (deg, d) => Mathf.SmoothStep(0f, 1f, (d - 225f) / 160f) * (6f + 14f * Noise(deg * 3f, d, 21f)) + 1.2f * Noise(deg * 5f, d * 0.8f, 22f) * Mathf.SmoothStep(0f, 1f, (d - 205f) / 20f),
            (deg, d, h) => Color.Lerp(C(0.22f, 0.36f, 0.38f), C(0.5f, 0.6f, 0.72f), Mathf.Clamp01(h / 16f) * 0.7f + 0.2f * Noise(deg * 7f, d, 23f)));
        SetSky(C(0.07f, 0.12f, 0.3f), C(0.62f, 0.72f, 0.92f), C(0.2f, 0.28f, 0.45f), C(1f, 0.92f, 0.6f), new Vector3(0.1f, 0.12f, -0.99f), 0.03f, 0.55f);
        MakeMusic(14f, new[] { 110f, 164.8f, 220f, 277.2f, 329.6f }, 2f, 0f, 0f, 7f, 659.3f, 0.3f);

        // Broken stumps of the Department wall.
        Transform stumps = Group("Wall Stumps");
        for (int i = 0; i < 12; i++) {
            float b = 60f + i * 20f;
            Piece(stumps, "Wall Stump " + i, Polar(b, 190f, 1.5f + (i % 3)), new Vector3(10f, 3f + (i % 3) * 2f, 1.4f), wall, b, i % 2 == 0 ? 6f : -4f);
            if (i % 3 == 0) {
                Piece(stumps, "Wall Rubble", Polar(b + 4f, 187f, 0.5f), new Vector3(3f, 1f, 3f), wall * 1.3f, b * 1.7f);
            }
        }
        Features.Add("landmark:the broken stumps of the Department wall round the rim at 190 m, a gap where it stood");

        // The pale path to nothing, and the gold seal at its end.
        Transform route = Group("Path to Nothing");
        Path(route, PathBearing, 30f, 176f, 3.4f, 5f, pale, pale * 0.82f);
        Piece(route, "Path End", Polar(PathBearing, 182f, 0.1f), new Vector3(3.4f, 0.08f, 1.4f), ink * 0.9f, PathBearing);
        seal = Piece(route, "Gold Seal", Polar(PathBearing, 196f, 15f), new Vector3(14f, 14f, 0.6f), gold, PathBearing, 0f, Shape.Cylinder, true).transform;
        seal.rotation = Quaternion.Euler(90f, PathBearing, 0f);
        Features.Add("route:the pale slab path out of the circle on bearing 180, run on to 180 m where it stops at nothing, a great gold seal hanging past its end");

        // The circle drawn in white, brighter than it has ever been: 36 marks that can unroll into a line of writing.
        Transform ring = Group("Brighter Circle");
        for (int i = 0; i < 36; i++) {
            float b = i * 10f;
            Vector3 p = Polar(b, 20f, 0.1f);
            Transform m = Piece(ring, "Circle Mark " + i, p, new Vector3(0.5f, 0.05f, 3.4f), white, b + 90f, 0f, Shape.Box, true).transform;
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

        // The Threshold: a ringed dais with a bare white doorframe, away from the path, and the Order-Bearer's stamp marks.
        float tb = 20f;
        Transform arena = Arena("The Threshold", tb, 80f, 22f, stone * 1.25f, stone, white * 0.5f);
        Vector3 at = Polar(tb, 80f);
        Quaternion f = Quaternion.Euler(0f, tb, 0f);
        Piece(arena, "Door Post L", at + f * new Vector3(-3f, 5f, 0f), new Vector3(1f, 10f, 1f), white, tb);
        Piece(arena, "Door Post R", at + f * new Vector3(3f, 5f, 0f), new Vector3(1f, 10f, 1f), white, tb);
        Piece(arena, "Door Lintel", at + Vector3.up * 10.4f, new Vector3(7f, 1f, 1f), white, tb);
        Piece(arena, "Dais Step", at + f * new Vector3(0f, 0.4f, -5f), new Vector3(9f, 0.4f, 3f), stone * 1.4f, tb);
        for (int i = 0; i < 4; i++) {
            Vector3 p = at + f * new Vector3((i - 1.5f) * 7f, 0.26f, -11f + (i % 2) * 3f);
            Piece(arena, "Stamp Mark " + i, p, new Vector3(5f, 0.03f, 3.4f), ink * 0.75f, tb + (i - 1.5f) * 6f);
            stampAt.Add(p);
        }
        Lamp(arena, at + Vector3.up * 6f, C(1f, 0.95f, 0.8f), 40f, 1.1f);
        Features.Add("arena:the Threshold, a ringed dais at 80 m on bearing 20 with a bare white doorframe, away from the path, where the Spider Bot that carries the order walks out");
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

// ---------------------------------------------------------------------------------------------- The hub: the office
// The Place Department's office, the room ChapterHub makes walkable (Wallit, Slice's ring, the chapter board, six chapter doors).
// This is its own scene, dressed for how far the case file has come (`cleared` chapters filed), and re-dressed when it changes
// (ChapterHub.Refresh): the window light goes from night to dawn, a plaque over each door shows its chapter's colours and, once
// the chapter is filed, a stamp; the filing cabinets along the back wall grow; the ring's edge warms from lamp-blue to gold; and
// once all six are filed the wall carries the brass door number. Hub coordinates are the hub's own (ChapterHub.DoorPosition...).
public class OfficeMap : OwnMap {
    public override string Key { get { return "office"; } }
    Transform stage;
    public int Stage { get; private set; } = -1;
    public string StageName { get; private set; } = "";
    Light window;

    // The six chapters' palettes (the map design sheets), used on the door plaques.
    static readonly Color[] Plaques = {
        new Color(0.16f, 0.34f, 0.5f), new Color(0.85f, 0.45f, 0.25f), new Color(0.75f, 0.35f, 0.15f),
        new Color(0.45f, 0.5f, 0.8f), new Color(0.9f, 0.72f, 0.36f), new Color(0.6f, 0.75f, 1f),
    };

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    protected override void Build() {
        MakeMusic(12f, new[] { 82.4f, 123.5f, 164.8f }, 2f, 0f, 0f, 6f, 440f, 0.14f);
        Features.Add("route:the runner rug from Slice's ring to the row of chapter doors on the back wall");
        Features.Add("landmark:six chapter doors under plaques in their chapters' colours, the filed ones stamped");
        Features.Add("setpiece:The Office Changes: as the case file advances the window light goes from night to dawn, the filing cabinets grow and the brass door number appears when all six are filed");
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        Dress(slot.cleared, HubLife.SideCount(slot), HubLife.HeardCount(slot));
    }

    int dressKey = -1;
    public int Pinned { get; private set; }
    public int Lamps { get; private set; }
    // A fingerprint of what the office looks like now, for the playtest: it changes whenever anything on the walls does.
    public string Look { get; private set; } = "";

    // The office after `cleared` chapters, with `pinned` side errands finished (slips on the left wall) and `heard` hub
    // conversations had (a lit lamp each along the right wall).
    public void Dress(int cleared, int pinned = 0, int heard = 0) {
        cleared = Mathf.Clamp(cleared, 0, CampaignSave.ChapterCount);
        int key = (cleared * 64 + pinned) * 64 + heard;
        if (key == dressKey) {
            return;
        }
        dressKey = key;
        Pinned = pinned;
        Lamps = heard;
        if (stage != null) {
            Destroy(stage.gameObject);
        }
        Stage = cleared;
        stage = Group("Office Stage " + cleared);
        float dawn = cleared / (float)CampaignSave.ChapterCount;
        Color glow = Color.Lerp(C(0.5f, 0.62f, 0.95f), C(1f, 0.86f, 0.55f), dawn);
        Color brass = C(0.9f, 0.72f, 0.36f), wood = C(0.3f, 0.2f, 0.12f), paper = C(0.93f, 0.89f, 0.76f), steel = C(0.32f, 0.34f, 0.42f);
        StageName = cleared >= 6 ? "filed" : cleared >= 4 ? "late" : cleared >= 2 ? "middle" : "early";
        SetSky(Color.Lerp(C(0.02f, 0.03f, 0.08f), C(0.12f, 0.2f, 0.45f), dawn), Color.Lerp(C(0.1f, 0.14f, 0.3f), C(0.95f, 0.8f, 0.55f), dawn), C(0.06f, 0.07f, 0.12f), Color.Lerp(C(0.8f, 0.85f, 1f), C(1f, 0.9f, 0.6f), dawn), new Vector3(0.1f, 0.3f, 0.9f), 0.02f, 0.4f);
        if (Sky != null) {
            RenderSettings.skybox = Sky;
        }

        float halfWidth = ChapterHub.RoomHalfWidth + 0.8f;
        // Windows in the side walls, lit by the hour.
        for (int side = -1; side <= 1; side += 2) {
            for (int i = 0; i < 4; i++) {
                float z = -3f + i * 6f;
                Piece(stage, "Window Frame", new Vector3(side * (halfWidth - 0.25f), 3.1f, z), new Vector3(0.3f, 3.1f, 2.6f), wood, 0f);
                Piece(stage, "Window Glass", new Vector3(side * (halfWidth - 0.45f), 3.1f, z), new Vector3(0.1f, 2.7f, 2.2f), glow, 0f, 0f, Shape.Box, true);
            }
        }
        window = Lamp(stage, new Vector3(0f, 6f, 2f), glow, 60f, 0.6f + dawn * 0.5f);

        // A runner rug from the ring to the doors.
        Piece(stage, "Runner Rug", new Vector3(0f, 0.0f, 9f), new Vector3(2.6f, 0.03f, 10f), Color.Lerp(C(0.3f, 0.12f, 0.14f), C(0.6f, 0.4f, 0.16f), dawn), 0f);

        // A plaque over each door in its chapter's colours; a stamp on every filed one.
        for (int i = 0; i < CampaignSave.ChapterCount; i++) {
            Vector3 d = ChapterHub.DoorPosition(i);
            Piece(stage, "Plaque " + (i + 1), new Vector3(d.x, 5.1f, d.z - 0.3f), new Vector3(2.4f, 0.8f, 0.12f), Plaques[i % Plaques.Length], 0f);
            if (i < cleared) {
                Piece(stage, "Filed Stamp " + (i + 1), new Vector3(d.x + 0.7f, 5.1f, d.z - 0.42f), new Vector3(0.9f, 0.6f, 0.06f), C(0.8f, 0.14f, 0.14f), 12f, 0f, Shape.Box, true);
            }
        }

        // Filing cabinets along the back wall: one more bank for every chapter filed.
        for (int i = 0; i < cleared + 1; i++) {
            float x = -ChapterHub.RoomHalfWidth + 1.2f + i * 1.7f;
            if (Mathf.Abs(x) < 1f) {
                continue;
            }
            Piece(stage, "Cabinet " + i, new Vector3(x, 0.9f, ChapterHub.RoomBack - 1.1f), new Vector3(1.2f, 1.8f, 0.9f), steel, 0f);
            Piece(stage, "Cabinet Drawer " + i, new Vector3(x, 1.2f, ChapterHub.RoomBack - 1.58f), new Vector3(0.9f, 0.3f, 0.08f), brass, 0f);
        }
        // Stacks of paper grow on the floor by the desk.
        for (int i = 0; i < cleared; i++) {
            Piece(stage, "Paper Stack " + i, new Vector3(ChapterHub.DeskPosition.x - 2f - (i % 3) * 0.7f, 0.2f + (i / 3) * 0.4f, ChapterHub.DeskPosition.z + 2.2f), new Vector3(0.6f, 0.4f, 0.8f), paper, i * 17f);
        }
        // The ring warms as the file fills.
        Piece(stage, "Ring Warmth", new Vector3(0f, 0.0f, 0f), new Vector3(8.8f, 0.02f, 8.8f), Color.Lerp(C(0.2f, 0.3f, 0.45f), brass, dawn), 0f, 0f, Shape.Cylinder, true);

        if (cleared >= CampaignSave.ChapterCount) {
            // All six filed: the door number in brass above the back wall.
            Piece(stage, "Number Plate", new Vector3(0f, 8.2f, ChapterHub.RoomBack + 0.6f), new Vector3(3.4f, 2.4f, 0.2f), brass, 0f, 0f, Shape.Box, true);
            var g = new GameObject("Door Number");
            g.transform.SetParent(stage, false);
            g.transform.position = new Vector3(0f, 8.2f, ChapterHub.RoomBack + 0.4f);
            g.transform.rotation = Quaternion.identity;
            var t = g.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = font;
            t.text = "1";
            t.characterSize = 0.4f;
            t.fontSize = 48;
            t.fontStyle = FontStyle.Bold;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = C(0.2f, 0.12f, 0.05f);
            g.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        // Notices: a slip pinned on the left wall for every side errand finished, in rows of six under the board.
        for (int i = 0; i < pinned; i++) {
            float x = -halfWidth + 0.55f;
            Piece(stage, "Pinned Slip " + i, new Vector3(x, 1.5f + (i / 6) * 0.9f, -6f + (i % 6) * 1.5f), new Vector3(0.06f, 0.7f, 1f), paper, 0f, i % 2 == 0 ? 4f : -4f);
            Piece(stage, "Pin " + i, new Vector3(x + 0.06f, 1.8f + (i / 6) * 0.9f, -6f + (i % 6) * 1.5f), new Vector3(0.08f, 0.12f, 0.12f), C(0.8f, 0.14f, 0.14f), 0f, 0f, Shape.Box, true);
        }
        // Lamps: one lit on the right wall for every conversation had, so the room is brighter the more it has said.
        for (int i = 0; i < heard; i++) {
            Vector3 at = new Vector3(halfWidth - 0.7f, 2.2f, -8f + i * 1.7f);
            Piece(stage, "Wall Lamp " + i, at, new Vector3(0.2f, 0.35f, 0.35f), C(1f, 0.86f, 0.5f), 0f, 0f, Shape.Box, true);
        }
        if (heard > 0) {
            Lamp(stage, new Vector3(halfWidth - 2f, 3f, 0f), C(1f, 0.86f, 0.55f), 14f, 0.15f * heard);
        }
        Look = "stage " + StageName + ", window " + ColorUtility.ToHtmlStringRGB(glow) + ", " + (cleared + 1) + " cabinet banks, " + cleared + " stamps, " + pinned + " slips pinned, " + heard + " wall lamps lit";
        Beat("office-dressed|cleared=" + cleared + "|" + StageName);
    }

    public override void Trigger() {
        Dress(Mathf.Min(Stage + 1, CampaignSave.ChapterCount));
    }
}
