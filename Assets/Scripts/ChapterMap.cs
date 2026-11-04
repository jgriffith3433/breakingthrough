using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// The map a Campaign chapter plays on. The chapter is data (Story.Chapter.Map names one of the Defs below); starting the
// chapter loads that map as its own scene on top of the game scene (additive: the game scene stays the active scene), sets
// the chapter's lighting (sun, ambient, fog, sky colour) and leaving unloads it again: the lighting goes back to what the
// game scene had, the map's objects, meshes and materials are destroyed with the scene, and unused assets are released.
//
// The six places follow Docs/CampaignBible.md. Until the bible's own models are made (GPU work, not started) each map is a
// stand-in dressed from the arena's existing art (the Lantern model and plain blocks). Dressing sits outside 150 m of the
// circle and has no colliders: bots walk straight lines, and the playfield stays free of anything solid.
public class ChapterMap : MonoBehaviour {
    public struct Look {
        public string Name;          // the map's scene is "Map_<Name>"
        public string Place;
        public Color Sun;            // the directional light
        public float SunIntensity;
        public Vector3 SunAngles;
        public Color Sky;            // sky colour: the camera background and the ambient light's top
        public Color Equator;
        public Color Ground;
        public Color Fog;
        public float FogDensity;
        public Color Dressing;       // the stand-in blocks' colour
        public int Posts;            // lantern posts in the ring beyond the playfield
        public Color Lamp;           // the posts' light
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    // One row per place in the bible. Map names are what Story.Chapters[i].Map holds.
    public static readonly Look[] Defs = {
        new Look { Name = "hollow", Place = "The Moonlit Hollow", Sun = C(0.62f, 0.72f, 1f), SunIntensity = 0.9f, SunAngles = new Vector3(48f, 35f, 0f),
            Sky = C(0.05f, 0.07f, 0.14f), Equator = C(0.16f, 0.2f, 0.3f), Ground = C(0.07f, 0.08f, 0.11f), Fog = C(0.1f, 0.13f, 0.22f), FogDensity = 0.0062f,
            Dressing = C(0.16f, 0.2f, 0.3f), Posts = 6, Lamp = C(0.7f, 0.8f, 1f) },
        new Look { Name = "hollow-edge", Place = "The edge of the Hollow", Sun = C(0.5f, 0.55f, 0.8f), SunIntensity = 0.7f, SunAngles = new Vector3(40f, 200f, 0f),
            Sky = C(0.04f, 0.05f, 0.1f), Equator = C(0.12f, 0.15f, 0.25f), Ground = C(0.05f, 0.06f, 0.09f), Fog = C(0.08f, 0.1f, 0.18f), FogDensity = 0.0085f,
            Dressing = C(0.2f, 0.21f, 0.28f), Posts = 1, Lamp = C(0.6f, 0.7f, 1f) },
        new Look { Name = "clearing-floor", Place = "The Clearing Floor", Sun = C(1f, 0.62f, 0.32f), SunIntensity = 0.55f, SunAngles = new Vector3(70f, 100f, 0f),
            Sky = C(0.09f, 0.05f, 0.04f), Equator = C(0.3f, 0.17f, 0.1f), Ground = C(0.1f, 0.06f, 0.04f), Fog = C(0.17f, 0.09f, 0.06f), FogDensity = 0.0075f,
            Dressing = C(0.28f, 0.16f, 0.1f), Posts = 10, Lamp = C(1f, 0.6f, 0.28f) },
        new Look { Name = "long-walk", Place = "The Long Walk", Sun = C(0.75f, 0.85f, 1f), SunIntensity = 0.6f, SunAngles = new Vector3(35f, 320f, 0f),
            Sky = C(0.06f, 0.07f, 0.1f), Equator = C(0.2f, 0.22f, 0.28f), Ground = C(0.08f, 0.09f, 0.12f), Fog = C(0.3f, 0.32f, 0.38f), FogDensity = 0.0125f,
            Dressing = C(0.5f, 0.52f, 0.6f), Posts = 12, Lamp = C(0.95f, 0.9f, 0.7f) },
        new Look { Name = "reading-room", Place = "The Reading Room", Sun = C(0.95f, 0.9f, 0.75f), SunIntensity = 0.65f, SunAngles = new Vector3(80f, 0f, 0f),
            Sky = C(0.05f, 0.05f, 0.07f), Equator = C(0.28f, 0.26f, 0.22f), Ground = C(0.1f, 0.09f, 0.08f), Fog = C(0.12f, 0.11f, 0.12f), FogDensity = 0.0045f,
            Dressing = C(0.9f, 0.87f, 0.75f), Posts = 8, Lamp = C(1f, 0.92f, 0.7f) },
        new Look { Name = "hollow-filed", Place = "The Moonlit Hollow, changed", Sun = C(0.8f, 0.85f, 1f), SunIntensity = 1.1f, SunAngles = new Vector3(55f, 30f, 0f),
            Sky = C(0.08f, 0.1f, 0.2f), Equator = C(0.24f, 0.28f, 0.42f), Ground = C(0.1f, 0.11f, 0.16f), Fog = C(0.14f, 0.18f, 0.3f), FogDensity = 0.0042f,
            Dressing = C(0.3f, 0.36f, 0.5f), Posts = 4, Lamp = C(1f, 0.95f, 0.8f) },
    };

    public static Look? Find(string name) {
        foreach (Look look in Defs) {
            if (look.Name == name) {
                return look;
            }
        }
        return null;
    }

    public static ChapterMap Current { get; private set; }
    public static int Loads { get; private set; }
    public static int Unloads { get; private set; }

    // For the playtest.
    public string MapName { get; private set; }
    public Scene MapScene { get; private set; }
    public long BaselineBytes { get; private set; }
    public long LoadedBytes { get; private set; }
    public Look Applied { get; private set; }

    // What the game scene had, put back on unload.
    Color sunColor, ambientSky, ambientEquator, ambientGround, fogColor, camColor;
    float sunIntensity, fogDensity;
    Quaternion sunRotation;
    bool fog;
    Light sun;
    Camera cam;
    CameraClearFlags clear;
    UnityEngine.Rendering.AmbientMode ambientMode;
    FogMode fogMode;
    GameObject root;
    readonly List<Object> made = new List<Object>();
    bool applied;
    int stamp;

    // Loads the map a chapter names. Null when the chapter names none (or one that does not exist).
    public static ChapterMap Load(string name) {
        Unload();
        Look? def = Find(name);
        if (def == null) {
            Debug.LogWarning("ChapterMap: no map named '" + name + "'");
            return null;
        }
        var go = new GameObject("Chapter Map");
        var map = go.AddComponent<ChapterMap>();
        map.Begin(def.Value);
        Current = map;
        Loads++;
        return map;
    }

    // Leaves the map: lighting back, scene unloaded, unused assets released.
    public static void Unload() {
        if (Current == null) {
            return;
        }
        ChapterMap map = Current;
        Current = null;
        map.Finish();
    }

    void Begin(Look def) {
        BaselineBytes = Profiler.GetTotalAllocatedMemoryLong() + Profiler.GetMonoUsedSizeLong();
        MapName = def.Name;
        Applied = def;
        MapScene = SceneManager.CreateScene("Map_" + def.Name);
        stamp = Loads;
        Remember();
        root = new GameObject("Map " + def.Name);
        Dress(def);
        SceneManager.MoveGameObjectToScene(root, MapScene);
        Apply();
        LoadedBytes = Profiler.GetTotalAllocatedMemoryLong() + Profiler.GetMonoUsedSizeLong();
        Debug.Log("[ChapterMap] loaded Map_" + def.Name + " (" + def.Place + ")");
    }

    void Remember() {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            if (light.type == LightType.Directional && light.gameObject.scene != MapScene) {
                sun = light;
                break;
            }
        }
        if (sun != null) {
            sunColor = sun.color;
            sunIntensity = sun.intensity;
            sunRotation = sun.transform.rotation;
        }
        ambientMode = RenderSettings.ambientMode;
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        fog = RenderSettings.fog;
        fogMode = RenderSettings.fogMode;
        fogColor = RenderSettings.fogColor;
        fogDensity = RenderSettings.fogDensity;
        cam = Camera.main;
        if (cam != null) {
            clear = cam.clearFlags;
            camColor = cam.backgroundColor;
        }
    }

    // NightAtmosphere sets the Hollow's moonlight on its own first frame; the chapter's look goes on after it (and again
    // for a moment, in case the game scene's own setup runs late).
    void Update() {
        if (!applied && Time.frameCount > 2) {
            applied = true;
            Apply();
        }
    }

    void Apply() {
        Look def = Applied;
        if (sun != null) {
            sun.color = def.Sun;
            sun.intensity = def.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(def.SunAngles);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(def.Sky, def.Equator, 0.6f);
        RenderSettings.ambientEquatorColor = def.Equator;
        RenderSettings.ambientGroundColor = def.Ground;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = def.Fog;
        RenderSettings.fogDensity = def.FogDensity;
        if (cam != null) {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = def.Sky;
        }
    }

    void Finish() {
        {
            if (sun != null) {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.transform.rotation = sunRotation;
            }
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            if (cam != null) {
                cam.clearFlags = clear;
                cam.backgroundColor = camColor;
            }
        }
        foreach (Object o in made) {
            if (o != null) {
                Destroy(o);
            }
        }
        made.Clear();
        if (MapScene.isLoaded) {
            SceneManager.UnloadSceneAsync(MapScene);
        }
        Unloads++;
        Debug.Log("[ChapterMap] unloaded Map_" + MapName);
        Destroy(gameObject);
        Resources.UnloadUnusedAssets();
    }

    void OnDestroy() {
        // The game scene is going away (a single scene load): the map's scene goes with it; just forget it.
        if (Current == this) {
            Current = null;
            Unloads++;
        }
    }

    // The bytes the managed and native heaps hold now, for the playtest's baseline comparison.
    public static long Memory() {
        return Profiler.GetTotalAllocatedMemoryLong() + Profiler.GetMonoUsedSizeLong();
    }

    // Stand-in dressing from the arena's art, beyond the playfield: a ring of lantern posts and a low wall of plain blocks.
    // Nothing has a collider.
    void Dress(Look def) {
        Layout(def);
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        var shared = new Material(SafeShader.Find("Standard") ?? SafeShader.Find("Sprites/Default") ?? BirdArt.Shared.shader) { color = def.Dressing };
        made.Add(shared);
        const float radius = 165f;
        for (int i = 0; i < def.Posts; i++) {
            float a = (i + 0.5f) / def.Posts * Mathf.PI * 2f;
            Vector3 spot = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            GameObject post = ArtLibrary.Make("Lantern");
            if (post == null) {
                post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(post.GetComponent<Collider>());
                post.transform.localScale = new Vector3(1.2f, 9f, 1.2f);
                spot.y = 4.5f;
                post.GetComponent<Renderer>().sharedMaterial = shared;
            } else {
                post.transform.localScale = Vector3.one * 3f;
            }
            post.transform.SetParent(root.transform, false);
            post.transform.position = spot;
            var lampObj = new GameObject("Lamp");
            lampObj.transform.SetParent(post.transform, false);
            lampObj.transform.position = spot + Vector3.up * 8f;
            Light lamp = lampObj.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = def.Lamp;
            lamp.range = 30f;
            lamp.intensity = 1.4f;
            lamp.shadows = LightShadows.None;
        }
        for (int i = 0; i < 24; i++) {
            float a = (i + 0.5f) / 24f * Mathf.PI * 2f;
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(block.GetComponent<Collider>());
            block.name = "Rim Block " + i;
            block.transform.SetParent(root.transform, false);
            block.transform.position = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 185f + Vector3.up * 3f;
            block.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            block.transform.localScale = new Vector3(28f, 6f + (i % 3) * 3f, 3f);
            block.GetComponent<Renderer>().sharedMaterial = shared;
        }
    }

    // ---------------------------------------------------------------- Chapter layouts
    // What makes chapters 1 to 3 different places and not one ring in three colours: each has a ROUTE (a laid path the
    // chapter's objective follows), LANDMARKS and one HAZARD or SET PIECE. Everything is a plain block in the map's scene,
    // no colliders, and the route runs from the circle outward along the chapter's own data (Story.Chapters: the gate's
    // bearing and distance, the Floor's stops), so the way you walk is the way the map is drawn. `Features` names each
    // piece for the playtest ("route:", "landmark:", "hazard:").
    public readonly List<string> Features = new List<string>();
    Vector3 center;

    Vector3 Polar(float bearing, float dist, float y = 0f) {
        float a = bearing * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * dist + Vector3.up * y;
    }

    readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();

    GameObject Piece(Transform parent, string name, Vector3 pos, Vector3 size, Color color, float yaw = 0f, float roll = 0f, PrimitiveType kind = PrimitiveType.Cube) {
        GameObject g = GameObject.CreatePrimitive(kind);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent != null ? parent : root.transform, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0f, yaw, roll);
        g.transform.localScale = size;
        if (!mats.TryGetValue(color, out Material m)) {
            m = new Material(SafeShader.Find("Sprites/Default") ?? BirdArt.Shared.shader) { color = color };
            mats[color] = m;
            made.Add(m);
        }
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    GameObject Group(string name) {
        var g = new GameObject(name);
        g.transform.SetParent(root.transform, false);
        return g;
    }

    // A path of flat slabs from `from` to `to` metres along a bearing, with an optional side offset.
    void Path(Transform parent, float bearing, float from, float to, float width, float step, Color a, Color b, float y = 0.1f) {
        int i = 0;
        for (float d = from; d <= to; d += step, i++) {
            Piece(parent, "Slab " + i, Polar(bearing, d, y), new Vector3(width, 0.08f, step * 0.86f), i % 2 == 0 ? a : b, bearing);
        }
    }

    static bool ChapterOf(string map, out Story.Chapter chapter) {
        foreach (Story.Chapter c in Story.Chapters) {
            if (c.Map == map) {
                chapter = c;
                return true;
            }
        }
        chapter = default(Story.Chapter);
        return false;
    }

    void Layout(Look def) {
        center = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        ChapterOf(def.Name, out Story.Chapter chapter);
        if (def.Name == "hollow") {
            LayoutHollow();
        } else if (def.Name == "hollow-edge") {
            LayoutEdge(chapter);
        } else if (def.Name == "clearing-floor") {
            LayoutFloor(chapter);
        } else if (def.Name == "long-walk") {
            LayoutWalk(chapter);
        } else if (def.Name == "reading-room") {
            LayoutReading(chapter);
        } else if (def.Name == "hollow-filed") {
            LayoutFiled();
        }
    }

    // A chapter's boss arena: a raised round floor with its own rim, on the far side of the circle from the route. The chapter's
    // boss walks out of it (WaveDirector.SpawnForChapter reads BossSpot). Flat slabs only, no colliders.
    public bool HasArena { get; private set; }
    public Vector3 BossSpot { get; private set; }
    public float ArenaBearing { get; private set; }

    void Arena(string name, float bearing, float dist, float radius, Color floor, Color rim, Color mark) {
        Transform g = Group(name).transform;
        Vector3 at = Polar(bearing, dist);
        Piece(g, "Arena Floor", at + Vector3.up * 0.16f, new Vector3(radius * 2f, 0.04f, radius * 2f), floor, 0f, 0f, PrimitiveType.Cylinder);
        Piece(g, "Arena Inner", at + Vector3.up * 0.2f, new Vector3(radius * 1.2f, 0.03f, radius * 1.2f), mark, 0f, 0f, PrimitiveType.Cylinder);
        for (int i = 0; i < 12; i++) {
            float a = i * 30f;
            Vector3 p = at + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (radius + 1.5f);
            Piece(g, "Arena Pillar " + i, p + Vector3.up * (3f + (i % 2) * 1.5f), new Vector3(2f, 6f + (i % 2) * 3f, 2f), rim, a);
        }
        HasArena = true;
        BossSpot = at;
        ArenaBearing = bearing;
    }

    // Chapter 4, the Long Walk: a causeway (the chapter lays its slabs and lamps) with a low parapet either side and a pit of mist
    // under it; six lamp stilts (the stops), a gap where slabs have fallen away (hazard) and, on the other side of the circle, the
    // Servers' Dais where The Server waits to be refused.
    void LayoutWalk(Story.Chapter chapter) {
        Color parapet = C(0.55f, 0.57f, 0.65f), pit = C(0.34f, 0.36f, 0.42f), iron = C(0.16f, 0.17f, 0.22f), lamp = C(1f, 0.82f, 0.45f), paper = C(0.92f, 0.9f, 0.8f);
        Story.Stop[] stops = chapter.Stops;
        float bearing = stops != null && stops.Length > 0 ? stops[stops.Length - 1].Bearing : 330f;
        float length = stops != null && stops.Length > 0 ? stops[stops.Length - 1].Dist + 24f : 120f;
        Quaternion face = Quaternion.Euler(0f, bearing, 0f);
        Transform route = Group("Causeway Parapets").transform;
        for (float d = 16f; d <= length; d += 8f) {
            foreach (float side in new[] { -3.6f, 3.6f }) {
                Piece(route, "Parapet", Polar(bearing, d, 0.45f) + face * new Vector3(side, 0f, 0f), new Vector3(0.7f, 0.9f, 6.6f), parapet, bearing);
            }
        }
        Features.Add("route:the causeway from the circle out along bearing " + bearing + ", walled by low parapets, to " + (stops != null && stops.Length > 0 ? stops[stops.Length - 1].Name : "the last lamp"));
        Transform mist = Group("Pit of Mist").transform;
        Piece(mist, "Mist Pit", Polar(bearing, length * 0.5f, -0.6f), new Vector3(60f, 0.1f, length + 40f), pit * 0.7f, bearing);
        for (int i = 0; i < 6; i++) {
            Piece(mist, "Mist Bank " + i, Polar(bearing, 20f + i * 18f, 1f) + face * new Vector3((i % 2 == 0 ? 1 : -1) * 14f, 0f, 0f), new Vector3(16f, 1.8f, 12f), C(0.62f, 0.64f, 0.7f), bearing + i * 20f);
        }
        Transform stilts = Group("Lamp Stilts").transform;
        if (stops != null) {
            for (int i = 0; i < stops.Length; i++) {
                foreach (float side in new[] { -5f, 5f }) {
                    Vector3 p = Polar(stops[i].Bearing, stops[i].Dist) + face * new Vector3(side, 0f, 0f);
                    Piece(stilts, "Stilt", p + Vector3.up * 4f, new Vector3(0.5f, 8f, 0.5f), iron, bearing);
                    Piece(stilts, "Stilt Lamp", p + Vector3.up * 8.6f, new Vector3(1.4f, 1.4f, 1.4f), lamp, bearing);
                }
            }
        }
        Features.Add("landmark:six lamp stilts in pairs at the three stops, one lamp for every leg");
        Transform gap = Group("Fallen Slabs").transform;
        Vector3 gapAt = Polar(bearing, length - 10f);
        for (int i = 0; i < 4; i++) {
            Piece(gap, "Fallen Slab " + i, gapAt + face * new Vector3((i - 1.5f) * 2f, -1.4f - i * 0.5f, i * 1.3f), new Vector3(1.8f, 0.4f, 3f), pit, bearing + i * 14f, i * 11f);
        }
        Features.Add("hazard:the pit of mist under the causeway, with the last slabs fallen into it (the set piece: no cover, no edge to hide behind)");
        float arena = (bearing + 180f) % 360f;
        Arena("Servers' Dais", arena, 78f, 22f, pit * 1.1f, iron, lamp * 0.55f);
        Transform stand = Group("Paper Stand").transform;
        Vector3 at = Polar(arena, 78f);
        Piece(stand, "Stand Post", at + Vector3.up * 1.2f, new Vector3(0.4f, 2.4f, 0.4f), iron);
        Piece(stand, "Stand Paper", at + Vector3.up * 2.6f, new Vector3(2f, 1.5f, 0.1f), paper, arena);
        Features.Add("arena:the Servers' Dais, a round dais with twelve pillars at 78 m on bearing " + arena + ", the far side of the circle from the causeway, where The Server walks out with its paper");
    }

    // Chapter 5, the Reading Room: a carpeted aisle from the door to the desk, rows of form racks, five page lecterns (one per
    // reading) and the Registrar's Gallery, a ring of ledgers where it reads with every leg.
    void LayoutReading(Story.Chapter chapter) {
        Color carpet = C(0.5f, 0.12f, 0.14f), rack = C(0.25f, 0.18f, 0.14f), form = C(0.92f, 0.89f, 0.78f), brass = C(0.85f, 0.7f, 0.3f), stone = C(0.24f, 0.25f, 0.32f);
        const float door = 180f;
        Transform aisle = Group("Carpet Aisle").transform;
        Path(aisle, door, 10f, 58f, 3.4f, 4f, carpet, carpet * 0.8f, 0.12f);
        Features.Add("route:a carpet aisle from the door on bearing 180 straight in to the desk by the circle");
        Transform racks = Group("Form Racks").transform;
        Quaternion face = Quaternion.Euler(0f, door, 0f);
        for (int i = 0; i < 5; i++) {
            foreach (float side in new[] { -12f, 12f }) {
                Vector3 p = Polar(door, 22f + i * 7f) + face * new Vector3(side, 0f, 0f);
                Piece(racks, "Form Rack", p + Vector3.up * 3f, new Vector3(1.4f, 6f, 5f), rack, door);
                Piece(racks, "Rack Forms", p + face * new Vector3(0f, 3.4f, 0.8f), new Vector3(1.5f, 4.2f, 3.6f), form, door);
            }
        }
        Features.Add("landmark:ten racks of identical forms down both sides of the aisle, all this order");
        Transform lect = Group("Page Lecterns").transform;
        for (int i = 0; i < 5; i++) {
            float b = i * 72f + 36f;
            Vector3 p = Polar(b, 30f);
            Piece(lect, "Lectern", p + Vector3.up * 0.8f, new Vector3(1.2f, 1.6f, 1.2f), rack, b);
            GameObject page = Piece(lect, "Page " + (i + 1), p + Vector3.up * 1.9f, new Vector3(1.4f, 0.1f, 1f), form, b, 20f);
            page.AddComponent<MapMover>().Set(MapMover.Mode.Press, 2.1f, 1.8f, 4f, i * 0.8f);
        }
        Features.Add("hazard:five page lecterns round the circle, each page lifting and settling in turn (the set piece: the reading, one page per wave)");
        Arena("Registrar's Gallery", 0f, 80f, 22f, stone, rack, brass * 0.6f);
        Vector3 at = Polar(0f, 80f);
        Piece(null, "Great Ledger", at + Vector3.up * 1.5f, new Vector3(5f, 3f, 3f), brass, 0f);
        Piece(null, "Ledger Pages", at + Vector3.up * 3.1f, new Vector3(4.6f, 0.2f, 2.6f), form, 0f);
        Features.Add("arena:the Registrar's Gallery, a ringed dais at 80 m on bearing 0 round the Great Ledger, opposite the door, where the Registrar walks out");
    }

    // Chapter 6, the Hollow filed: the Hollow of chapter 1, changed. The wall is gone (broken stumps on the rim), the path of slabs
    // runs on out to nothing, the circle burns brighter in a ring of white, and the Threshold, a bare doorframe on a dais, is where
    // the Spider Bot carrying the order walks out.
    void LayoutFiled() {
        Color pale = C(0.7f, 0.76f, 0.9f), stone = C(0.3f, 0.36f, 0.5f), white = C(0.95f, 0.95f, 0.9f), wall = C(0.1f, 0.12f, 0.18f), ink = C(0.85f, 0.18f, 0.2f);
        const float path = 180f;
        Transform route = Group("Path to Nothing").transform;
        Path(route, path, 124f, 175f, 3.2f, 5f, pale, pale * 0.8f, 0.1f);
        Piece(route, "Path End", Polar(path, 182f, 0.1f), new Vector3(3.2f, 0.08f, 1.4f), ink * 0.9f, path);
        Features.Add("route:the pale slab path out of the circle on bearing 180, run on to 180 m where it stops at nothing");
        Transform ringGroup = Group("Brighter Circle").transform;
        for (int i = 0; i < 36; i++) {
            float b = i * 10f;
            Piece(ringGroup, "Circle Mark " + i, Polar(b, 20f, 0.1f), new Vector3(0.5f, 0.05f, 3.4f), white, b + 90f);
        }
        Features.Add("landmark:the circle drawn in white round the guard, brighter than it has ever been");
        Transform stumps = Group("Wall Stumps").transform;
        for (int i = 0; i < 8; i++) {
            float b = 90f + i * 22f;
            Piece(stumps, "Wall Stump " + i, Polar(b, 190f, 1.5f + (i % 3)), new Vector3(10f, 3f + (i % 3) * 2f, 1.4f), wall, b, (i % 2 == 0 ? 6f : -4f));
        }
        Features.Add("hazard:the broken stumps of the Department wall at 190 m, a gap where it stood (set piece: nothing left to stop the order walking in)");
        Arena("The Threshold", 20f, 80f, 22f, stone * 1.2f, stone, white * 0.55f);
        Vector3 at = Polar(20f, 80f);
        Piece(null, "Door Post L", at + Quaternion.Euler(0f, 20f, 0f) * new Vector3(-3f, 5f, 0f), new Vector3(1f, 10f, 1f), white, 20f);
        Piece(null, "Door Post R", at + Quaternion.Euler(0f, 20f, 0f) * new Vector3(3f, 5f, 0f), new Vector3(1f, 10f, 1f), white, 20f);
        Piece(null, "Door Lintel", at + Vector3.up * 10.4f, new Vector3(7f, 1f, 1f), white, 20f);
        Features.Add("arena:the Threshold, a ringed dais at 80 m on bearing 20 with a bare white doorframe, away from the path, where the Spider Bot that carries the order walks out");
    }

    // Chapter 1, the Moonlit Hollow: a pale stepping-stone trail (the way the bots and the Courier come in), a ring of
    // standing stones, the Fallen Mailbox with its stamp (landmark) and a mist bog with dead trunks (the set piece).
    void LayoutHollow() {
        Color stone = C(0.3f, 0.34f, 0.46f), pale = C(0.62f, 0.68f, 0.82f), dark = C(0.07f, 0.09f, 0.14f), bog = C(0.1f, 0.2f, 0.22f), ink = C(0.85f, 0.18f, 0.2f), wood = C(0.12f, 0.1f, 0.12f), steel = C(0.45f, 0.5f, 0.62f);
        const float trail = 20f;
        Transform route = Group("Stepping-stone Trail").transform;
        Path(route, trail, 45f, 150f, 3.2f, 5f, pale, pale * 0.8f);
        Features.Add("route:stepping-stone trail from the circle out to the mailbox, bearing 20");
        Transform ring = Group("Standing Stones").transform;
        for (int i = 0; i < 9; i++) {
            float b = i * 40f + 10f;
            Piece(ring, "Standing Stone " + i, Polar(b, 62f, 3.5f), new Vector3(2.2f, 7f + (i % 3) * 1.5f, 1.6f), stone, b, (i % 2 == 0 ? 4f : -3f));
        }
        Features.Add("landmark:ring of nine standing stones round the circle");
        Transform box = Group("Fallen Mailbox").transform;
        Vector3 at = Polar(trail, 125f);
        Piece(box, "Mailbox Post", at + Vector3.up * 5f, new Vector3(1.2f, 10f, 1.2f), wood, trail, 12f);
        Piece(box, "Mailbox Body", at + new Vector3(0f, 10.4f, 0f) + Quaternion.Euler(0f, trail, 0f) * new Vector3(1.8f, 0f, 0f), new Vector3(6f, 3.6f, 3.6f), steel, trail, 12f);
        Piece(box, "Mailbox Flag", at + new Vector3(0f, 12.6f, 0f), new Vector3(0.4f, 3f, 1.8f), ink, trail, 12f);
        Piece(box, "Stamp Slab", at + Quaternion.Euler(0f, trail, 0f) * new Vector3(0f, 0.3f, -9f), new Vector3(9f, 0.12f, 4f), ink, trail);
        Features.Add("landmark:the Fallen Mailbox with its red RETURN TO SENDER stamp, 125 m out on the trail");
        Transform mire = Group("Mist Bog").transform;
        Vector3 bogAt = Polar(285f, 95f);
        Piece(mire, "Bog", bogAt + Vector3.up * 0.07f, new Vector3(46f, 0.03f, 46f), bog, 0f, 0f, PrimitiveType.Cylinder);
        Piece(mire, "Bog Rim", bogAt + Vector3.up * 0.05f, new Vector3(52f, 0.03f, 52f), dark, 0f, 0f, PrimitiveType.Cylinder);
        for (int i = 0; i < 5; i++) {
            float a = i * 72f * Mathf.Deg2Rad;
            Piece(mire, "Dead Trunk " + i, bogAt + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 12f + Vector3.up * (4f + i % 2), new Vector3(1f, 8f + i % 2 * 2f, 1f), wood, 0f, (i - 2) * 5f);
        }
        Features.Add("hazard:the Mist Bog, a dark sink of dead trunks 95 m out to the west (set piece: the bots do not cross it)");
    }

    // Chapter 2, the edge of the Hollow: a chalk-lined road with lanterns out to the gate (the route is the chapter's own
    // gate bearing and distance), the Department wall running off either side, Slice's radius painted on the ground
    // (landmark) and a stamp press that falls beside the road (the hazard).
    void LayoutEdge(Story.Chapter chapter) {
        float bearing = chapter.Bearing;
        float gate = chapter.Target;
        Color chalk = C(0.8f, 0.78f, 0.7f), iron = C(0.16f, 0.17f, 0.22f), wall = C(0.1f, 0.11f, 0.15f), red = C(0.85f, 0.14f, 0.12f), lamp = C(1f, 0.82f, 0.45f), stone = C(0.2f, 0.21f, 0.27f);
        Transform road = Group("Chalk Road").transform;
        Path(road, bearing, 14f, gate - 6f, 1.6f, 4f, chalk, chalk * 0.85f);
        for (int i = 0; i < 6; i++) {
            float d = 18f + i * ((gate - 24f) / 5f);
            foreach (float side in new[] { -4f, 4f }) {
                Vector3 p = Polar(bearing, d) + Quaternion.Euler(0f, bearing, 0f) * new Vector3(side, 0f, 0f);
                Piece(road, "Lamp Post", p + Vector3.up * 1.8f, new Vector3(0.3f, 3.6f, 0.3f), iron);
                Piece(road, "Lamp", p + Vector3.up * 3.9f, new Vector3(0.9f, 0.9f, 0.9f), lamp);
            }
        }
        Features.Add("route:chalk road with paired lamps from the circle to the gate, " + gate + " m on bearing " + bearing);
        Transform walls = Group("Department Wall").transform;
        for (int i = -5; i <= 5; i++) {
            if (Mathf.Abs(i) <= 1) {
                continue;
            }
            float b = bearing + i * 7f;
            Piece(walls, "Wall Section " + i, Polar(b, gate + 6f, 3.5f), new Vector3(2f * Mathf.PI * (gate + 6f) * 7f / 360f, 7f, 1.2f), wall, b);
        }
        Features.Add("landmark:the Department wall running off either side of the gate");
        Transform ringGroup = Group("Slice's Radius").transform;
        for (int i = 0; i < 48; i++) {
            float b = i * 7.5f;
            Piece(ringGroup, "Radius Mark " + i, Polar(b, 24f, 0.09f), new Vector3(0.6f, 0.05f, 3.6f), red, b + 90f);
        }
        Features.Add("landmark:Slice's radius, a red painted ring 24 m out that she tells Wallit not to leave");
        Transform press = Group("Stamp Press").transform;
        Vector3 baseAt = Polar(bearing + 9f, 46f);
        Piece(press, "Press Post L", baseAt + Quaternion.Euler(0f, bearing, 0f) * new Vector3(-4f, 5f, 0f), new Vector3(1f, 10f, 1f), iron, bearing);
        Piece(press, "Press Post R", baseAt + Quaternion.Euler(0f, bearing, 0f) * new Vector3(4f, 5f, 0f), new Vector3(1f, 10f, 1f), iron, bearing);
        Piece(press, "Press Beam", baseAt + Vector3.up * 10.4f, new Vector3(10f, 1f, 1f), iron, bearing);
        Piece(press, "Press Mark", baseAt + Vector3.up * 0.1f, new Vector3(7f, 0.06f, 5f), red * 0.7f, bearing);
        GameObject plate = Piece(press, "Press Plate", baseAt + Vector3.up * 8f, new Vector3(7f, 1.2f, 5f), red, bearing);
        plate.AddComponent<MapMover>().Set(MapMover.Mode.Press, 8f, 1.2f, 3.4f, 0f);
        Features.Add("hazard:the stamp press beside the road at 46 m, its plate falling every few seconds on a red INVALID mark");
    }

    // Chapter 3, the Clearing Floor: a belt-lined yard. The route is the Floor's own stops; a hook gantry arches over it
    // every few metres (landmark), a moving belt of cleared things runs on each side, and crushers cycle by the way
    // (the hazard). A red lamp lights the lever's chute.
    void LayoutFloor(Story.Chapter chapter) {
        Color iron = C(0.16f, 0.17f, 0.22f), steel = C(0.55f, 0.58f, 0.66f), belt = C(0.12f, 0.12f, 0.16f), lamp = C(1f, 0.74f, 0.35f), red = C(0.9f, 0.14f, 0.1f);
        Color[] thing = { C(0.35f, 0.3f, 0.4f), C(1f, 0.74f, 0.35f), C(0.8f, 0.65f, 0.3f) };
        Story.Stop[] stops = chapter.Stops;
        if (stops == null || stops.Length == 0) {
            return;
        }
        float bearing = stops[stops.Length - 1].Bearing;
        float length = stops[stops.Length - 1].Dist + 20f;
        Transform route = Group("Yard Floor Route").transform;
        Path(route, bearing, 16f, length, 6f, 6f, belt * 1.7f, belt * 1.35f);
        for (int i = 0; i < stops.Length; i++) {
            Vector3 p = Polar(stops[i].Bearing, stops[i].Dist, 0.14f);
            Piece(route, "Stop Plate " + (i + 1), p, new Vector3(9f, 0.06f, 9f), lamp * 0.8f, stops[i].Bearing);
        }
        Features.Add("route:the yard floor path through " + stops.Length + " lit stops, " + stops[0].Name + " to " + stops[stops.Length - 1].Name);
        Transform gantry = Group("Hook Gantries").transform;
        Quaternion face = Quaternion.Euler(0f, bearing, 0f);
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
            Transform b = Group("Belt " + (side < 0 ? "West" : "East")).transform;
            Vector3 mid = Polar(bearing, length * 0.5f) + face * new Vector3(side * 17f, 0f, 0f);
            Piece(b, "Belt Bed", mid + Vector3.up * 0.5f, new Vector3(3.4f, 1f, length - 10f), belt, bearing);
            for (int k = 0; k < 6; k++) {
                GameObject item = Piece(b, "Cleared Thing " + k, mid + face * new Vector3(0f, 1.4f, 0f), new Vector3(1.4f, 1.1f, 1.4f), thing[k % 3], bearing);
                MapMover mover = item.AddComponent<MapMover>();
                mover.Set(MapMover.Mode.Belt, 0f, 0f, side < 0 ? 3f : -3f, (k / 6f) * (length - 10f));
                mover.SetLine(mid + face * new Vector3(0f, 1.4f, 0f), face * Vector3.forward, length - 10f);
            }
        }
        Features.Add("landmark:two belts of cleared things (a hat, a clock, a bell) sliding along the route");
        Transform crush = Group("Crushers").transform;
        for (int i = 0; i < 3; i++) {
            float d = 38f + i * 24f;
            int side = i % 2 == 0 ? 1 : -1;
            Vector3 at = Polar(bearing, d) + face * new Vector3(side * 9f, 0f, 0f);
            Piece(crush, "Crusher Frame", at + Vector3.up * 5.5f, new Vector3(0.8f, 11f, 0.8f), iron, bearing);
            Piece(crush, "Crusher Mark", at + Vector3.up * 0.1f, new Vector3(5f, 0.06f, 5f), red * 0.7f, bearing);
            GameObject head = Piece(crush, "Crusher Head", at + Vector3.up * 7f, new Vector3(5f, 1.6f, 5f), red, bearing);
            head.AddComponent<MapMover>().Set(MapMover.Mode.Press, 7f, 1.0f, 2.6f, i * 0.9f);
        }
        Features.Add("hazard:three crushers beside the route (at 38, 62 and 86 m) that slam down on red marks in turn");
        Vector3 chute = Polar(stops[stops.Length - 1].Bearing, stops[stops.Length - 1].Dist);
        Piece(null, "Chute Lamp", chute + new Vector3(0f, 12f, 0f), new Vector3(3f, 3f, 0.6f), red, bearing);
    }

    // Moves a dressing piece with no collider: a press (drops from high to low on a cycle) or a belt item (slides along a line).
    public class MapMover : MonoBehaviour {
        public enum Mode { Press, Belt }
        Mode mode;
        float high, low, period, offset, speed;
        Vector3 origin, along;
        float lineLength;
        public int Cycles { get; private set; }

        public void Set(Mode m, float highY, float lowY, float p, float off) {
            mode = m;
            high = highY;
            low = lowY;
            period = Mathf.Max(0.5f, p);
            offset = off;
            speed = p;
        }

        public void SetLine(Vector3 start, Vector3 dir, float length) {
            origin = start - dir * (length * 0.5f);
            along = dir;
            lineLength = length;
        }

        void Update() {
            Vector3 p = transform.position;
            if (mode == Mode.Press) {
                float t = Mathf.Repeat(Time.time + offset, period) / period;
                // Rise slowly, hang, slam down, rest.
                float k = t < 0.55f ? Mathf.SmoothStep(0f, 1f, t / 0.55f) : t < 0.7f ? 1f : t < 0.78f ? 1f - (t - 0.7f) / 0.08f : 0f;
                p.y = Mathf.Lerp(low, high, k);
            } else {
                float s = Mathf.Repeat(offset + Time.time * Mathf.Abs(speed), lineLength);
                p = origin + along * (speed >= 0f ? s : lineLength - s);
                p.y = transform.position.y;
            }
            transform.position = p;
        }
    }
}
