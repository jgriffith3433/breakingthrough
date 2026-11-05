using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// The map a Campaign chapter plays on. The chapter is data (Story.Chapter.Map names one of the Defs below); starting the
// chapter loads that map as its own scene on top of the game scene (additive: the game scene stays the active scene), sets
// the chapter's lighting (sun, ambient, fog, sky colour) and leaving unloads it again: the lighting goes back to what the
// game scene had, the map's objects, meshes and materials are destroyed with the scene, and unused assets are released.
//
// Chapters 1 to 3 (hollow, hollow-edge, clearing-floor) are real scenes of their own: Assets/Maps/Map_Hollow.unity, Map_Edge.unity
// and Map_Floor.unity, loaded additively; OwnMap.cs and OwnMapChapters.cs build each place inside its scene (terrain, sky, props,
// route, landmark, hazard, set piece, music). Their layout is no longer drawn here.
//
// The six places follow Docs/CampaignBible.md. Until the bible's own models are made (GPU work, not started) each map is a
// stand-in dressed from the arena's existing art (the Lantern model and plain blocks). Dressing sits outside 150 m of the
// circle and has no colliders: bots walk straight lines, and the playfield stays free of anything solid.
public class ChapterMap : MonoBehaviour {
    public struct Look {
        public string Name;          // the map's scene is "Map_<Name>"
        public string Place;
        public string Scene;         // a map with its own Unity scene (Assets/Maps/<Scene>.unity); empty: dressed in code (Dress)
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
        new Look { Name = "hollow", Scene = "Map_Hollow", Place = "The Moonlit Hollow", Sun = C(0.62f, 0.72f, 1f), SunIntensity = 0.9f, SunAngles = new Vector3(48f, 35f, 0f),
            Sky = C(0.05f, 0.07f, 0.14f), Equator = C(0.16f, 0.2f, 0.3f), Ground = C(0.07f, 0.08f, 0.11f), Fog = C(0.1f, 0.13f, 0.22f), FogDensity = 0.0062f,
            Dressing = C(0.16f, 0.2f, 0.3f), Posts = 6, Lamp = C(0.7f, 0.8f, 1f) },
        new Look { Name = "hollow-edge", Scene = "Map_Edge", Place = "The edge of the Hollow", Sun = C(0.95f, 0.68f, 0.52f), SunIntensity = 0.75f, SunAngles = new Vector3(26f, 200f, 0f),
            Sky = C(0.1f, 0.07f, 0.13f), Equator = C(0.27f, 0.22f, 0.27f), Ground = C(0.1f, 0.07f, 0.08f), Fog = C(0.22f, 0.17f, 0.21f), FogDensity = 0.0085f,
            Dressing = C(0.2f, 0.21f, 0.28f), Posts = 1, Lamp = C(1f, 0.78f, 0.4f) },
        new Look { Name = "clearing-floor", Scene = "Map_Floor", Place = "The Clearing Floor", Sun = C(1f, 0.62f, 0.32f), SunIntensity = 0.55f, SunAngles = new Vector3(70f, 100f, 0f),
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
    public OwnMap Own { get; private set; }          // the chapter's own scene content (null for maps still dressed in code)
    public bool OwnScene { get { return !string.IsNullOrEmpty(Applied.Scene); } }
    AsyncOperation loading;
    Material oldSkybox;

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
        stamp = Loads;
        Remember();
        if (!string.IsNullOrEmpty(def.Scene)) {
            // The chapter's own scene, from the build's scene list. It fills in on the next frames (see Update).
            loading = SceneManager.LoadSceneAsync(def.Scene, LoadSceneMode.Additive);
            MapScene = SceneManager.GetSceneByName(def.Scene);
            Apply();
            Debug.Log("[ChapterMap] loading " + def.Scene + " (" + def.Place + ")");
            return;
        }
        MapScene = SceneManager.CreateScene("Map_" + def.Name);
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
        oldSkybox = RenderSettings.skybox;
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
        if (OwnScene && Own == null && MapScene.isLoaded) {
            Fill();
        }
        if (!applied && Time.frameCount > 2) {
            applied = true;
            Apply();
        }
    }

    // The scene is loaded: its "Map Root" object gets the chapter's map component, which builds the place inside the scene.
    void Fill() {
        MapScene = SceneManager.GetSceneByName(Applied.Scene);
        GameObject host = null;
        foreach (GameObject go in MapScene.GetRootGameObjects()) {
            if (go.name == "Map Root") {
                host = go;
                break;
            }
        }
        if (host == null) {
            Debug.LogWarning("ChapterMap: " + Applied.Scene + " has no Map Root");
            host = new GameObject("Map Root");
            SceneManager.MoveGameObjectToScene(host, MapScene);
        }
        ChapterOf(Applied.Name, out Story.Chapter chapter);
        Own = OwnMap.Attach(Applied.Name, host);
        if (Own == null) {
            return;
        }
        Own.Boot(chapter);
        Features.AddRange(Own.Features);
        Apply();
        LoadedBytes = Profiler.GetTotalAllocatedMemoryLong() + Profiler.GetMonoUsedSizeLong();
        Debug.Log("[ChapterMap] loaded " + Applied.Scene + " (" + Applied.Place + ")");
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
            if (Own != null && Own.Sky != null) {
                RenderSettings.skybox = Own.Sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            } else {
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
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
            RenderSettings.skybox = oldSkybox;
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
        if (OwnScene) {
            Scene own = SceneManager.GetSceneByName(Applied.Scene);
            if (own.isLoaded) {
                SceneManager.UnloadSceneAsync(own);
            } else if (loading != null && !loading.isDone) {
                // Left before the scene finished loading: close it the moment it has.
                string leftover = Applied.Scene;
                loading.completed += _ => {
                    Scene late = SceneManager.GetSceneByName(leftover);
                    if (late.isLoaded) {
                        SceneManager.UnloadSceneAsync(late);
                    }
                };
            }
        } else if (MapScene.isLoaded) {
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
        if (def.Name == "long-walk") {
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
