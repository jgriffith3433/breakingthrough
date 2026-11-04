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
}
