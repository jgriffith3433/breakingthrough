using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// The map a Campaign chapter plays on. The chapter is data (Story.Chapter.Map names one of the Defs below); starting the
// chapter loads that map as its own scene on top of the game scene (additive: the game scene stays the active scene), sets
// the chapter's lighting (sun, ambient, fog, sky colour) and leaving unloads it again: the lighting goes back to what the
// game scene had, the map's objects, meshes and materials are destroyed with the scene, and unused assets are released.
//
// Every place is a real scene of its own: Assets/Maps/Map_Hollow, Map_Edge, Map_Floor (chapters 1-3), Map_Walk, Map_Reading,
// Map_Filed (chapters 4-6) and Map_Office (the hub), loaded additively; OwnMap.cs, OwnMapChapters.cs and OwnMapLate.cs build each
// place inside its scene (terrain, sky, props, route, landmark, hazard, boss arena, set piece, music). The old shared-arena
// layouts and the code-dressed stand-in maps are gone.
//
// The places follow Docs/CampaignBible.md. Until the bible's own models are made (GPU work, not started) each map is built in
// code from flat vertex-colour blocks. Dressing sits outside 150 m of the circle (arenas excepted, see OwnMap.Arena) and has
// no colliders: bots walk straight lines, and the playfield stays free of anything solid.
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
        new Look { Name = "long-walk", Scene = "Map_Walk", Place = "The Long Walk", Sun = C(0.75f, 0.85f, 1f), SunIntensity = 0.6f, SunAngles = new Vector3(35f, 320f, 0f),
            Sky = C(0.06f, 0.07f, 0.1f), Equator = C(0.2f, 0.22f, 0.28f), Ground = C(0.08f, 0.09f, 0.12f), Fog = C(0.3f, 0.32f, 0.38f), FogDensity = 0.0125f,
            Dressing = C(0.5f, 0.52f, 0.6f), Posts = 12, Lamp = C(0.95f, 0.9f, 0.7f) },
        new Look { Name = "reading-room", Scene = "Map_Reading", Place = "The Reading Room", Sun = C(0.95f, 0.9f, 0.75f), SunIntensity = 0.65f, SunAngles = new Vector3(80f, 0f, 0f),
            Sky = C(0.05f, 0.05f, 0.07f), Equator = C(0.28f, 0.26f, 0.22f), Ground = C(0.1f, 0.09f, 0.08f), Fog = C(0.12f, 0.11f, 0.12f), FogDensity = 0.0045f,
            Dressing = C(0.9f, 0.87f, 0.75f), Posts = 8, Lamp = C(1f, 0.92f, 0.7f) },
        new Look { Name = "hollow-filed", Scene = "Map_Filed", Place = "The Moonlit Hollow, changed", Sun = C(0.8f, 0.85f, 1f), SunIntensity = 1.1f, SunAngles = new Vector3(55f, 30f, 0f),
            Sky = C(0.08f, 0.1f, 0.2f), Equator = C(0.24f, 0.28f, 0.42f), Ground = C(0.1f, 0.11f, 0.16f), Fog = C(0.14f, 0.18f, 0.3f), FogDensity = 0.0042f,
            Dressing = C(0.3f, 0.36f, 0.5f), Posts = 4, Lamp = C(1f, 0.95f, 0.8f) },
        // The hub between chapters (ChapterHub): the office, dressed for how far the case file has come (OfficeMap).
        new Look { Name = "office", Scene = "Map_Office", Place = "The Place Department, the office", Sun = C(1f, 0.86f, 0.64f), SunIntensity = 1.1f, SunAngles = new Vector3(50f, 330f, 0f),
            Sky = C(0.03f, 0.05f, 0.1f), Equator = C(0.36f, 0.29f, 0.24f), Ground = C(0.17f, 0.12f, 0.09f), Fog = C(0.13f, 0.09f, 0.07f), FogDensity = 0.0012f,
            Dressing = C(0.2f, 0.2f, 0.27f), Posts = 0, Lamp = C(1f, 0.85f, 0.55f) },
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
            // The map's own scene, from the build's scene list. It fills in on the next frames (see Update).
            loading = SceneManager.LoadSceneAsync(def.Scene, LoadSceneMode.Additive);
            MapScene = SceneManager.GetSceneByName(def.Scene);
            Apply();
            Debug.Log("[ChapterMap] loading " + def.Scene + " (" + def.Place + ")");
            return;
        }
        Debug.LogWarning("ChapterMap: map '" + def.Name + "' has no scene");
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

    // What the playtest reads: the chapter's boss arena, built by its own scene's OwnMap (WaveDirector.SpawnForChapter walks the
    // boss out of it).
    public bool HasArena { get { return Own != null && Own.HasArena; } }
    public Vector3 BossSpot { get { return Own != null ? Own.BossSpot : Vector3.zero; } }
    public float ArenaBearing { get { return Own != null ? Own.ArenaBearing : 0f; } }
    public readonly List<string> Features = new List<string>();

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
