using System.Collections.Generic;
using UnityEngine;

// A chapter's own map, built inside its own Unity scene (Assets/Maps/Map_<Name>.unity), not out of the shared arena's
// blocks. ChapterMap loads the scene additively and attaches the chapter's OwnMap subclass to the scene's "Map Root"
// object; the subclass builds the place: a terrain skirt shaped for the place, a sky of its own, lamps, props, a laid
// route, a landmark, a hazard, a scripted set piece and a music bed.
//
// Rules held here: movers meet no solid scenery (bots walk straight lines; the playfield within 150 m of the circle
// stays free of anything that shoves Wallit). Kit walls and buildings still get CamBlock colliders so the camera can
// sphere-cast against them without making the field solid. No spark/glitter particles, no camera shake. Everything is
// drawn in vertex colour on the bots' knoll shader, so it sits in the same night as the rest of the game. `Features`
// names each piece for the playtest ("route:", "landmark:", "hazard:", "setpiece:").
public abstract class OwnMap : MonoBehaviour {
    public enum Shape { Box, Cylinder, Cone }

    public readonly List<string> Features = new List<string>();
    public readonly List<string> Log = new List<string>();   // what the set piece did, for the playtest
    public int Beats { get; protected set; }
    public Material Sky { get; private set; }
    public AudioSource Music { get; private set; }
    public int Pieces { get; private set; }
    public abstract string Key { get; }

    protected Story.Chapter chapter;
    protected Vector3 center;
    protected Transform root;
    protected readonly List<Object> made = new List<Object>();
    readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
    Material flat, glowing;
    AudioSource oneShot;

    // The subclass for a map name (ChapterMap.Defs), added to the scene's root object.
    public static OwnMap Attach(string key, GameObject host) {
        switch (key) {
            case "hollow": return host.AddComponent<HollowMap>();
            case "hollow-edge": return host.AddComponent<EdgeMap>();
            case "clearing-floor": return host.AddComponent<FloorMap>();
            case "long-walk": return host.AddComponent<WalkMap>();
            case "reading-room": return host.AddComponent<ReadingMap>();
            case "hollow-filed": return host.AddComponent<FiledMap>();
            case "office": return host.AddComponent<OfficeMap>();
        }
        return null;
    }

    public void Boot(Story.Chapter data) {
        chapter = data;
        root = transform;
        center = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        flat = new Material(BirdArt.Shared) { name = "Own map (flat)" };
        if (flat.HasProperty("_RimStrength")) {
            flat.SetFloat("_RimStrength", 0.1f);
        }
        glowing = new Material(BirdArt.Glowing) { name = "Own map (glow)" };
        made.Add(flat);
        made.Add(glowing);
        Build();
        Debug.Log("[OwnMap] " + Key + " built: " + Pieces + " pieces, " + Features.Count + " features");
    }

    protected abstract void Build();

    // A page of the Reading Room has been read (ChapterRun.PageRead); the other places ignore it.
    public virtual void PageRead(int page) { }

    // The chapter's boss arena (a kit-built dais with its own props: ar_server_dais, ar_gallery_dais, ar_threshold_dais), away from the
    // route. The boss walks out of it (WaveDirector.SpawnForChapter reads BossSpot). No colliders. The map builds the pieces; this
    // names the spot and returns a group for them.
    public bool HasArena { get; private set; }
    public Vector3 BossSpot { get; private set; }
    public float ArenaBearing { get; private set; }

    // The walking surface of a boss dais: an invisible lathed floor on the Ground layer, flat at the dais's top with a gentle
    // skirt (under 40 degrees, so Wallit's controller and the bots' ground snap walk straight up it) down to a metre below.
    // Without it everyone stood on the bowl with their legs inside the stone. A floor, not a wall: nothing blocks a straight line.
    protected void DaisFloor(Transform parent, Vector3 at, float r, float top) {
        var km = new KitMesh { Name = "Dais Floor" };
        float o = r + (top + 1f) / 0.8f;
        km.Lathe(new List<Vector2> { new Vector2(0f, -1f), new Vector2(o, -1f), new Vector2(r, top), new Vector2(0f, top) }, 32, false);
        var go = new GameObject("Dais Floor");
        go.layer = LevelDress.GroundLayer();
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        Mesh m = km.ToMesh();
        made.Add(m);
        go.AddComponent<MeshCollider>().sharedMesh = m;
    }

    protected Transform DeclareArena(string name, float bearing, float dist) {
        Transform g = Group(name);
        HasArena = true;
        BossSpot = Polar(bearing, dist);
        ArenaBearing = bearing;
        return g;
    }

    // The playtest's named setup shortcut: make the set piece happen now.
    public abstract void Trigger();

    protected void Beat(string what) {
        Beats++;
        Log.Add(what);
    }

    void OnDestroy() {
        foreach (Object o in made) {
            if (o != null) {
                Destroy(o);
            }
        }
        made.Clear();
    }

    // ------------------------------------------------------------------ Placing things
    protected Vector3 Polar(float bearing, float dist, float y = 0f) {
        float a = bearing * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * dist + Vector3.up * y;
    }

    protected Transform Group(string name) {
        var g = new GameObject(name);
        g.transform.SetParent(root, false);
        return g.transform;
    }

    protected GameObject Piece(Transform parent, string name, Vector3 pos, Vector3 size, Color color, float yaw = 0f, float roll = 0f, Shape shape = Shape.Box, bool glow = false) {
        var g = new GameObject(name);
        g.transform.SetParent(parent != null ? parent : root, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0f, yaw, roll);
        g.transform.localScale = size;
        g.AddComponent<MeshFilter>().sharedMesh = Tinted(shape, color);
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = glow ? glowing : flat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Pieces++;
        return g;
    }

    protected Light Lamp(Transform parent, Vector3 pos, Color color, float range, float intensity) {
        var g = new GameObject("Lamp");
        g.transform.SetParent(parent != null ? parent : root, false);
        g.transform.position = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    // A laid path of flat slabs along a bearing.
    protected void Path(Transform parent, float bearing, float from, float to, float width, float step, Color a, Color b, float y = 0.1f) {
        int i = 0;
        for (float d = from; d <= to; d += step, i++) {
            Piece(parent, "Slab " + i, Polar(bearing, d, y), new Vector3(width, 0.08f, step * 0.86f), i % 2 == 0 ? a : b, bearing);
        }
    }

    // ------------------------------------------------------------------ Meshes in vertex colour (no assets, no colliders)
    Mesh Tinted(Shape shape, Color color) {
        Color32 c32 = color;
        c32.a = 255;
        int key = c32.r | (c32.g << 8) | (c32.b << 16) | ((int)shape << 24);
        if (meshes.TryGetValue(key, out Mesh m)) {
            return m;
        }
        m = shape == Shape.Box ? BoxMesh() : PrismMesh(14, shape == Shape.Cone ? 0f : 1f);
        var colors = new Color32[m.vertexCount];
        for (int i = 0; i < colors.Length; i++) {
            colors[i] = c32;
        }
        m.colors32 = colors;
        m.name = "Own " + shape;
        meshes[key] = m;
        made.Add(m);
        return m;
    }

    static Mesh BoxMesh() {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        foreach (Vector3 nrm in normals) {
            Vector3 u = Mathf.Abs(nrm.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(nrm, u);
            int s = v.Count;
            v.Add((nrm + u + w) * 0.5f);
            v.Add((nrm + u - w) * 0.5f);
            v.Add((nrm - u - w) * 0.5f);
            v.Add((nrm - u + w) * 0.5f);
            for (int i = 0; i < 4; i++) {
                n.Add(nrm);
            }
            // Clockwise seen from outside (Unity's front face): (v1 - v0) x (v2 - v0) points along nrm. The old order pointed in and
            // only showed because the shared material draws both sides; normals are set above, so lighting does not change.
            t.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
        }
        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        return mesh;
    }

    // Unit height and width (centred): topRatio 1 is a cylinder, 0 a cone.
    static Mesh PrismMesh(int sides, float topRatio) {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i <= sides; i++) {
            float a = i / (float)sides * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            v.Add(dir * 0.5f + Vector3.down * 0.5f);
            v.Add(dir * 0.5f * topRatio + Vector3.up * 0.5f);
            Vector3 nrm = (dir + Vector3.up * (1f - topRatio) * 0.5f).normalized;
            n.Add(nrm);
            n.Add(nrm);
        }
        for (int i = 0; i < sides; i++) {
            int b = i * 2;
            t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });   // outward, like the caps below (was wound inward)
        }
        int cap = v.Count;
        v.Add(Vector3.down * 0.5f);
        n.Add(Vector3.down);
        for (int i = 0; i <= sides; i++) {
            float a = i / (float)sides * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Sin(a) * 0.5f, -0.5f, Mathf.Cos(a) * 0.5f));
            n.Add(Vector3.down);
        }
        for (int i = 0; i < sides; i++) {
            t.AddRange(new[] { cap, cap + 2 + i, cap + 1 + i });
        }
        if (topRatio > 0f) {
            int top = v.Count;
            v.Add(Vector3.up * 0.5f);
            n.Add(Vector3.up);
            for (int i = 0; i <= sides; i++) {
                float a = i / (float)sides * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f * topRatio + Vector3.up * 0.5f);
                n.Add(Vector3.up);
            }
            for (int i = 0; i < sides; i++) {
                t.AddRange(new[] { top, top + 1 + i, top + 2 + i });
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        return mesh;
    }

    // ------------------------------------------------------------------ Terrain: a skirt round the playfield, shaped for the place
    // Starts just outside the arena's ground (205 m) and runs out to 560 m. `height(angleDeg, dist)` is the lift; `tint`
    // the vertex colour for a height and distance. One mesh, no collider.
    const int TerrainAround = 120;
    const int TerrainRings = 14;
    const float TerrainInner = 203f, TerrainOuter = 560f;
    System.Func<float, float, float> terrainHeight;

    static float TerrainRing(int r) {
        float u = r / (float)TerrainRings;
        return Mathf.Lerp(TerrainInner, TerrainOuter, u * u * 0.6f + u * 0.4f);
    }

    protected void Terrain(string name, System.Func<float, float, float> height, System.Func<float, float, float, Color> tint) {
        const int around = TerrainAround;
        const int rings = TerrainRings;
        terrainHeight = height;
        var v = new Vector3[(around + 1) * (rings + 1)];
        var c = new Color32[v.Length];
        for (int r = 0; r <= rings; r++) {
            float dist = TerrainRing(r);
            for (int a = 0; a <= around; a++) {
                float deg = a / (float)around * 360f;
                float rad = deg * Mathf.Deg2Rad;
                float h = height(deg % 360f, dist);
                v[r * (around + 1) + a] = center + new Vector3(Mathf.Sin(rad) * dist, h - 0.12f, Mathf.Cos(rad) * dist);
                Color32 col = tint(deg % 360f, dist, h);
                col.a = 255;
                c[r * (around + 1) + a] = col;
            }
        }
        var t = new List<int>();
        for (int r = 0; r < rings; r++) {
            for (int a = 0; a < around; a++) {
                int i = r * (around + 1) + a;
                int j = i + around + 1;
                t.AddRange(new[] { i, j, i + 1, i + 1, j, j + 1 });
            }
        }
        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.colors32 = c;
        mesh.RecalculateNormals();
        made.Add(mesh);
        var g = new GameObject(name);
        g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var rend = g.AddComponent<MeshRenderer>();
        rend.sharedMaterial = flat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    // The terrain skirt's own surface at x, z (world y), as drawn: its rings are 15-20 m apart, so the mesh between them is flat
    // triangles, not `height` itself (a piece set on `height` floats or sinks by up to metres on a terraced rim). Inside the
    // skirt, the live ground.
    protected float TerrainY(float x, float z) {
        float dx = x - center.x, dz = z - center.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (terrainHeight == null || dist < TerrainInner) {
            return LevelDress.GroundY(x, z, center.y);
        }
        int r = 0;
        while (r < TerrainRings - 1 && TerrainRing(r + 1) <= dist) {
            r++;
        }
        float d0 = TerrainRing(r), d1 = TerrainRing(r + 1);
        float fr = Mathf.Clamp01((dist - d0) / (d1 - d0));
        float deg = Mathf.Repeat(Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 360f);
        float fa = deg / 360f * TerrainAround;
        int a = Mathf.Clamp(Mathf.FloorToInt(fa), 0, TerrainAround - 1);
        fa = Mathf.Clamp01(fa - a);
        // The mesh's quad (r, a)-(r + 1, a + 1) is split on the diagonal from (r, a + 1) to (r + 1, a).
        float yi = TerrainVertex(r, a), yj = TerrainVertex(r + 1, a), yi1 = TerrainVertex(r, a + 1), yj1 = TerrainVertex(r + 1, a + 1);
        float y = fa + fr <= 1f ? yi + fa * (yi1 - yi) + fr * (yj - yi) : yj1 + (1f - fa) * (yj - yj1) + (1f - fr) * (yi1 - yj1);
        return center.y + y;
    }

    float TerrainVertex(int r, int a) {
        float deg = a / (float)TerrainAround * 360f;
        return terrainHeight(deg % 360f, TerrainRing(r)) - 0.12f;
    }

    // The lowest the drawn ground gets under a footprint (`radius` m round `at`): an upright piece set here has no gap under it.
    protected float LowestTerrain(Vector3 at, float radius) {
        float y = TerrainY(at.x, at.z);
        for (int k = 0; k < 8; k++) {
            float a = k * 45f * Mathf.Deg2Rad;
            y = Mathf.Min(y, TerrainY(at.x + Mathf.Sin(a) * radius, at.z + Mathf.Cos(a) * radius));
        }
        return y;
    }

    // ------------------------------------------------------------------ Ground skin: what a piece really stands on
    // LevelDress.GroundSkin bakes the visible floor as a grid of rays a hand's breadth (2 cm) above the live ground, flat between its
    // points; on the bowl's curves it is a few centimetres off the collider that GroundY reads, which is all a decal has to spare.
    // Skin() bakes it and keeps the grid, so SkinY reads the surface people see.
    bool hasSkin;
    float skinX0, skinZ0, skinSX, skinSZ, skinR2;
    int skinNX, skinNZ;
    readonly Dictionary<long, float> skinCorner = new Dictionary<long, float>();

    protected GameObject Skin(Transform parent, string name, float radius, float step, Material mat, float tile) {
        // The same grid as LevelDress.GridMesh makes from these numbers.
        float x0 = center.x - radius, z0 = center.z - radius, x1 = center.x + radius, z1 = center.z + radius;
        skinNX = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / step));
        skinNZ = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / step));
        skinSX = (x1 - x0) / skinNX;
        skinSZ = (z1 - z0) / skinNZ;
        skinX0 = x0;
        skinZ0 = z0;
        skinR2 = radius * radius;
        skinCorner.Clear();
        hasSkin = true;
        return LevelDress.GroundSkin(parent, name, center, radius, step, mat, tile);
    }

    // The drawn floor's height at x, z: the skin where there is one, the live ground elsewhere.
    protected float SkinY(float x, float z) {
        float cx = x - center.x, cz = z - center.z;
        if (!hasSkin || cx * cx + cz * cz > skinR2) {
            return LevelDress.GroundY(x, z, center.y);
        }
        float fx = (x - skinX0) / skinSX, fz = (z - skinZ0) / skinSZ;
        int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, skinNX - 1), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, skinNZ - 1);
        fx = Mathf.Clamp01(fx - i);
        fz = Mathf.Clamp01(fz - j);
        // GridMesh's triangles (a, c, b) and (b, c, d) share the diagonal from b (i + 1, j) to c (i, j + 1).
        float ya = SkinCorner(i, j), yb = SkinCorner(i + 1, j), yc = SkinCorner(i, j + 1), yd = SkinCorner(i + 1, j + 1);
        return fx + fz <= 1f ? ya + fx * (yb - ya) + fz * (yc - ya) : yd + (1f - fx) * (yc - yd) + (1f - fz) * (yb - yd);
    }

    float SkinCorner(int i, int j) {
        long key = ((long)i << 32) | (uint)j;
        float y;
        if (!skinCorner.TryGetValue(key, out y)) {
            y = LevelDress.GroundY(skinX0 + i * skinSX, skinZ0 + j * skinSZ, center.y) + 0.02f;
            skinCorner[key] = y;
        }
        return y;
    }

    // How a flat piece lies on the drawn floor at `at` (only x, z are read): tilted to the floor's slope across its footprint
    // (`halfX` by `halfZ` m in its own frame after `yaw`), and raised until its base clears the floor at nine points over that
    // footprint, then `lift` more. A slab takes a small negative lift (its underside just in the floor, no gap at its edge); a
    // decal takes 1 to 4 cm (never in the floor, never z-fighting with it).
    protected Pose LayPose(Vector3 at, float yaw, float halfX, float halfZ, float lift) {
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        Vector3 rx = turn * Vector3.right, rz = turn * Vector3.forward;
        float y0 = SkinY(at.x, at.z);
        Vector3 px = at + rx * halfX, nx = at - rx * halfX, pz = at + rz * halfZ, nz = at - rz * halfZ;
        float sx = (SkinY(px.x, px.z) - SkinY(nx.x, nx.z)) / (2f * halfX);
        float sz = (SkinY(pz.x, pz.z) - SkinY(nz.x, nz.z)) / (2f * halfZ);
        float raise = 0f;
        for (int u = -1; u <= 1; u++) {
            for (int v = -1; v <= 1; v++) {
                Vector3 q = at + rx * (u * halfX) + rz * (v * halfZ);
                raise = Mathf.Max(raise, SkinY(q.x, q.z) - (y0 + sx * u * halfX + sz * v * halfZ));
            }
        }
        Vector3 ax = (rx + Vector3.up * sx).normalized, az = (rz + Vector3.up * sz).normalized;
        Vector3 up = Vector3.Cross(az, ax).normalized;
        return new Pose(new Vector3(at.x, y0 + raise + lift, at.z), Quaternion.LookRotation(az, up));
    }

    protected GameObject Lay(GameObject go, float yaw, float halfX, float halfZ, float lift) {
        Pose p = LayPose(go.transform.position, yaw, halfX, halfZ, lift);
        go.transform.SetPositionAndRotation(p.position, p.rotation);
        return go;
    }

    // The lowest the drawn floor gets under an upright piece's footprint (`halfX` by `halfZ` m after `yaw`): stand a wall, a
    // machine or a plinth here and no edge of it floats; the high side goes a little into the ground instead.
    protected float Lowest(Vector3 at, float yaw, float halfX, float halfZ) {
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        Vector3 rx = turn * Vector3.right, rz = turn * Vector3.forward;
        float y = float.MaxValue;
        for (int u = -1; u <= 1; u++) {
            for (int v = -1; v <= 1; v++) {
                Vector3 q = at + rx * (u * halfX) + rz * (v * halfZ);
                y = Mathf.Min(y, SkinY(q.x, q.z));
            }
        }
        return y;
    }

    // Deterministic smooth noise in [0,1] for hills, so a map looks the same every time it loads.
    protected static float Noise(float x, float y, float seed) {
        return Mathf.PerlinNoise(x * 0.013f + seed * 17.3f, y * 0.013f + seed * 5.1f);
    }

    // ------------------------------------------------------------------ Sky
    protected void SetSky(Color top, Color horizon, Color below, Color disc, Vector3 discDir, float discSize, float haze) {
        Shader shader = Shader.Find("Knoll/ChapterSky");
        if (shader == null && Shader.Find("Knoll/PanoSky") == null) {
            return;
        }
        // A painted panorama (Resources/Skies/<Key>.png) wins; the gradient below is the fallback when there is none.
        Material pano = LevelDress.PanoSky(Key, Color.white, 0f);
        if (pano != null) {
            Sky = pano;
            made.Add(Sky);
            return;
        }
        if (shader == null) {
            return;
        }
        Sky = new Material(shader) { name = "Sky " + Key };
        Sky.SetColor("_Top", top);
        Sky.SetColor("_Horizon", horizon);
        Sky.SetColor("_Ground", below);
        Sky.SetColor("_Disc", disc);
        Sky.SetVector("_DiscDir", discDir);
        Sky.SetFloat("_DiscSize", discSize);
        Sky.SetFloat("_Haze", haze);
        made.Add(Sky);
    }

    // ------------------------------------------------------------------ Music and hits, made in code (no GPU, no files)
    // A loop of held tones (frequencies snapped to whole cycles so it joins seamlessly), a slow swell, an optional pulse and an
    // optional struck note on a beat. Follows the Settings music volume (SoundSettings.Apply reads looping sources).
    protected void MakeMusic(float seconds, float[] notes, float swells, float pulseHz, float pulseDepth, float strikeEvery, float strikeHz, float volume) {
        const int rate = 22050;
        int count = Mathf.RoundToInt(seconds * rate);
        var data = new float[count];
        for (int n = 0; n < notes.Length; n++) {
            float f = Mathf.Max(1f, Mathf.Round(notes[n] * seconds)) / seconds;
            float detune = 1f + 0.0018f * (n % 2 == 0 ? 1f : -1f);
            float phase = n * 1.7f;
            for (int i = 0; i < count; i++) {
                float t = i / (float)rate;
                float swell = 0.55f + 0.45f * Mathf.Sin(Mathf.PI * 2f * (swells * t / seconds + n * 0.23f));
                float tone = Mathf.Sin(Mathf.PI * 2f * f * t + phase) * 0.6f + Mathf.Sin(Mathf.PI * 2f * f * 2f * detune * t) * 0.18f + Mathf.Sin(Mathf.PI * 2f * f * 3f * t) * 0.06f;
                data[i] += tone * swell / notes.Length;
            }
        }
        if (pulseHz > 0f) {
            float ph = Mathf.Max(1f, Mathf.Round(pulseHz * seconds)) / seconds;
            for (int i = 0; i < count; i++) {
                float t = i / (float)rate;
                float p = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * ph * t);
                data[i] *= 1f - pulseDepth * Mathf.Pow(p, 3f);
            }
        }
        if (strikeEvery > 0f) {
            int hits = Mathf.Max(1, Mathf.RoundToInt(seconds / strikeEvery));
            float f = Mathf.Max(1f, Mathf.Round(strikeHz * strikeEvery)) / strikeEvery;
            for (int h = 0; h < hits; h++) {
                int start = Mathf.RoundToInt(h * seconds / hits * rate);
                for (int i = 0; i < rate * 2 && i < count; i++) {
                    float t = i / (float)rate;
                    int at = (start + i) % count;
                    data[at] += Mathf.Sin(Mathf.PI * 2f * f * t) * Mathf.Exp(-t * 2.4f) * 0.35f;
                }
            }
        }
        float peak = 0.0001f;
        for (int i = 0; i < count; i++) {
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < count; i++) {
            data[i] = data[i] / peak * 0.8f;
        }
        AudioClip clip = AudioClip.Create("Music " + Key, count, 1, rate, false);
        clip.SetData(data, 0);
        made.Add(clip);
        var g = new GameObject("Map Music");
        g.transform.SetParent(root, false);
        Music = g.AddComponent<AudioSource>();
        Music.clip = clip;
        Music.loop = true;
        Music.spatialBlend = 0f;
        Music.volume = volume;
        Music.playOnAwake = false;
        Music.Play();
        SoundSettings.Apply();
    }

    // A short low thump, for a press or a crusher landing.
    AudioClip thud;

    protected void Thud(float volume = 0.8f) {
        if (thud == null) {
            const int rate = 22050;
            var data = new float[rate / 2];
            for (int i = 0; i < data.Length; i++) {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(Mathf.PI * 2f * (62f - 30f * t) * t) * Mathf.Exp(-t * 9f) * 0.9f + (Mathf.PerlinNoise(i * 0.31f, 0.5f) - 0.5f) * Mathf.Exp(-t * 30f);
            }
            thud = AudioClip.Create("Thud", data.Length, 1, rate, false);
            thud.SetData(data, 0);
            made.Add(thud);
            oneShot = gameObject.AddComponent<AudioSource>();
            oneShot.spatialBlend = 0f;
            oneShot.playOnAwake = false;
        }
        oneShot.PlayOneShot(thud, volume * SoundSettings.EffectsLevel);
    }

    // ------------------------------------------------------------------ Hazards
    // A blow on a patch of ground: bots in it (not bosses) take a plain hit and are thrown; Wallit in it flinches. Returns bots hit.
    protected int Strike(Vector3 at, float radius, float power, out bool wallit) {
        int hit = 0;
        wallit = false;
        float r2 = radius * radius;
        foreach (Enemy e in new List<Enemy>(Enemy.Roster)) {
            if (e == null || !e.Alive || e.Boss) {
                continue;
            }
            Vector3 d = e.transform.position - at;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) {
                e.Hit(power);
                e.Knock(d.normalized * 7f + Vector3.up * 3f, 0.3f);
                hit++;
            }
        }
        PlayerGrowth p = PlayerGrowth.Instance;
        if (p != null) {
            Vector3 d = p.transform.position - at;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) {
                p.OnHit();
                wallit = true;
            }
        }
        return hit;
    }

    // A head that rises, hangs on a red warning light, and slams down on its mark; calls `landed` at the slam. Used by the
    // stamp press (armed when Wallit comes near) and the Floor's crushers (always cycling until the belt stops).
    public class Slammer : MonoBehaviour {
        public float High, Low, Period = 3.4f, Offset;
        public float ArmRadius;          // 0: always cycling; else only while Wallit is within this many metres
        public bool Frozen;              // the belt has stopped: hold high and quiet
        public bool Forced;              // the playtest's setup shortcut: act as if armed
        public Light Warning;
        public System.Action<int> Landed;
        // A frame laid to the ground's slope: with Guide set the head runs along it from Track (the frame's origin), High and Low
        // being metres along Guide, so it stays square over the bed. Zero: High and Low are world heights and the head moves on y.
        public Vector3 Track, Guide;
        public int Slams { get; private set; }
        float clock;
        bool slammed;

        void Update() {
            if (Frozen) {
                Place(1f);
                SetWarning(0f);
                return;
            }
            bool armed = ArmRadius <= 0f || Forced;
            if (!armed) {
                PlayerGrowth p = PlayerGrowth.Instance;
                if (p != null) {
                    Vector3 d = p.transform.position - transform.position;
                    d.y = 0f;
                    armed = d.magnitude <= ArmRadius;
                }
            }
            if (!armed) {
                Place(1f);
                SetWarning(0f);
                clock = 0f;
                slammed = false;
                return;
            }
            clock += Time.deltaTime;
            float t = Mathf.Repeat(clock + Offset, Period) / Period;
            // Rise slowly, hang with the warning lit, slam down, rest.
            float k = t < 0.55f ? Mathf.SmoothStep(0f, 1f, t / 0.55f) : t < 0.7f ? 1f : t < 0.78f ? 1f - (t - 0.7f) / 0.08f : 0f;
            Place(k);
            SetWarning(t >= 0.4f && t < 0.78f ? Mathf.Clamp01((t - 0.4f) / 0.3f) : t >= 0.78f && t < 0.84f ? 1.6f : 0f);
            if (t >= 0.78f && !slammed) {
                slammed = true;
                Slams++;
                if (Landed != null) {
                    Landed(Slams);
                }
            } else if (t < 0.7f) {
                slammed = false;
            }
        }

        void Place(float k) {
            if (Guide != Vector3.zero) {
                transform.position = Track + Guide * Mathf.Lerp(Low, High, k);
                return;
            }
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(Low, High, k);
            transform.position = p;
        }

        void SetWarning(float level) {
            if (Warning != null) {
                Warning.intensity = level * 3.2f;
            }
        }
    }
}
