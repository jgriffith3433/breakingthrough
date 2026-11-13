using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The character gallery (Docs/CharacterModels.md): every named character standing in a row, idle, each with a label, built at
// run time in its own scene (CharacterGallery.SceneName, created in code: no asset to merge). It hides the other cameras and
// overlay canvases while it is open and restores them on Close. Shots frame the whole row, the heroes, the bosses, the bots
// and the hub's lodgers; every shot can be taken in colour or as a black-fill silhouette (the recognisability test).
public class CharacterGallery : MonoBehaviour {
    public const string SceneName = "CharacterGallery";
    public static CharacterGallery Instance { get; private set; }

    public struct Shot {
        public string Name;
        public Vector3 Eye, Target;
        public string[] Who;
        public bool Silhouette;
    }

    public class Entry {
        public string Id, Label, Group;
        public GameObject Root;
        public int Triangles;
        public string Idle;
    }

    public readonly List<Entry> Entries = new List<Entry>();
    public readonly List<Shot> Shots = new List<Shot>();
    public Camera Cam;
    public int LastVisibleTriangles;
    public bool Silhouette { get; private set; }

    Scene scene;
    GameObject world;
    readonly List<Camera> hiddenCameras = new List<Camera>();
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    readonly Dictionary<Renderer, Material[]> colour = new Dictionary<Renderer, Material[]>();
    Material black;
    Light sun;

    static readonly string[] Order = { "wallit", "slice", "yo_bot", "spider_bot", "courier", "plated_alpha", "foreman", "server", "registrar", "order_bearer", "tick", "brim", "spare", "chime" };
    const float Spacing = 2.3f;

    public static CharacterGallery Open() {
        if (Instance != null) {
            return Instance;
        }
        Scene s = SceneManager.CreateScene(SceneName);
        SceneManager.SetActiveScene(s);
        var go = new GameObject("Character Gallery");
        var g = go.AddComponent<CharacterGallery>();
        g.scene = s;
        g.Build();
        Instance = g;
        return g;
    }

    public void Close() {
        foreach (Camera c in hiddenCameras) {
            if (c != null) {
                c.enabled = true;
            }
        }
        foreach (Canvas c in hiddenCanvases) {
            if (c != null) {
                c.enabled = true;
            }
        }
        Instance = null;
        if (scene.IsValid()) {
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    void Build() {
        foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) {
            if (c.enabled) {
                hiddenCameras.Add(c);
                c.enabled = false;
            }
        }
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) {
            if (c.enabled && c.renderMode != RenderMode.WorldSpace) {
                hiddenCanvases.Add(c);
                c.enabled = false;
            }
        }
        world = new GameObject("Gallery World");
        world.transform.position = new Vector3(3000f, 0f, 0f);   // far from anything else that is loaded
        black = new Material(SafeShader.Solid()) { color = Color.black };

        var camGo = new GameObject("Gallery Camera");
        camGo.transform.SetParent(world.transform, false);
        Cam = camGo.AddComponent<Camera>();
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.16f, 0.17f, 0.24f);
        Cam.fieldOfView = 38f;
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 120f;
        Cam.depth = 100f;
        camGo.tag = "MainCamera";
        if (FindFirstObjectByType<AudioListener>() == null) {
            camGo.AddComponent<AudioListener>();
        }

        var sunGo = new GameObject("Gallery Sun");
        sunGo.transform.SetParent(world.transform, false);
        sunGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.96f, 0.9f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.62f);

        // a floor slab built from the kit's geometry (a bevelled box, textured)
        var floorMesh = new KitMesh { Name = "Gallery Floor", Tex = 2f };
        floorMesh.Box(new Vector3(-20f, -0.3f, -8f), new Vector3(20f, 0f, 8f), 0.05f);
        var floor = new GameObject("Floor");
        floor.transform.SetParent(world.transform, false);
        floor.AddComponent<MeshFilter>().sharedMesh = floorMesh.ToMesh();
        floor.AddComponent<MeshRenderer>().sharedMaterial = KitTextures.Get("office", "stone");

        for (int i = 0; i < Order.Length; i++) {
            Place(Order[i], new Vector3((i - (Order.Length - 1) * 0.5f) * Spacing, 0f, 0f));
        }
        foreach (Renderer r in world.GetComponentsInChildren<Renderer>()) {
            colour[r] = r.sharedMaterials;
        }
        MakeShots();
    }

    // ---- characters --------------------------------------------------------------------------------------------------

    void Place(string id, Vector3 local) {
        var stand = new GameObject("Stand " + id);
        stand.transform.SetParent(world.transform, false);
        stand.transform.localPosition = local;
        var e = new Entry { Id = id };
        GameObject body = null;
        float top = 2f;
        switch (id) {
            case "wallit": body = MakeWallit(); e.Label = "WALLIT"; e.Group = "heroes"; e.Idle = "animator idle"; top = 2.1f; break;
            case "slice": body = MakeSlice(); e.Label = "SLICE"; e.Group = "heroes"; e.Idle = "animator idle"; top = 2.3f; break;
            case "yo_bot": body = MakeBot(BotKind.YoBot); e.Label = "YO BOT"; e.Group = "bots"; e.Idle = "head swivel and bob"; top = 1.4f; break;
            case "spider_bot": body = MakeBot(BotKind.SpiderBot); e.Label = "SPIDER BOT"; e.Group = "bots"; e.Idle = "leg shuffle and scan"; top = 1.6f; break;
            case "tick": case "brim": case "spare": case "chime":
                body = KitCatalog.Make("of_lodger_" + id);
                e.Label = id.ToUpperInvariant();
                e.Group = "lodgers";
                e.Idle = id == "tick" ? "sway" : id == "brim" ? "hover" : id == "spare" ? "droop" : "ring";
                top = 2.2f;
                if (body != null) {
                    body.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    body.AddComponent<GalleryLodgerIdle>().Id = id;
                }
                break;
            default:
                body = CharacterKit.Make(id);
                e.Label = CharacterKit.DefOf(id).Title.ToUpperInvariant();
                e.Group = "bosses";
                e.Idle = id == "courier" ? "checks clipboard, tips cap" : id == "foreman" ? "tilts hat, taps claw" : id == "server" ? "polite bow" : id == "registrar" ? "turns a page, peers" : id == "plated_alpha" ? "rolls shoulders, knocks plate" : "seal breath, scroll drift";
                top = 1.7f;
                AddBossProp(id, body);
                break;
        }
        if (body == null) {
            return;
        }
        body.transform.SetParent(stand.transform, false);
        if (id != "tick" && id != "brim" && id != "spare" && id != "chime") {
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
        } else {
            body.transform.localPosition = Vector3.zero;
        }
        foreach (Collider c in body.GetComponentsInChildren<Collider>(true)) {
            Destroy(c);
        }
        e.Root = stand;
        e.Triangles = CharacterKit.Triangles(body) + SkinnedTriangles(body);
        Label(stand.transform, e.Label, new Vector3(0f, top, 0f));
        Entries.Add(e);
    }

    // The bosses carry the same props their fights use.
    static void AddBossProp(string id, GameObject body) {
        string prop = id == "courier" ? "clipboard" : id == "foreman" ? "order_sheet" : id == "server" ? "paper" : null;
        if (prop == null || body == null) {
            return;
        }
        GameObject p = CharacterKit.Prop(prop);
        p.transform.SetParent(body.transform, false);
        Vector3 at = id == "courier" ? new Vector3(0.42f, 0.55f, 0.55f) : id == "foreman" ? new Vector3(0f, 0.62f, 0.72f) : new Vector3(0.3f, 0.5f, 0.7f);
        float inv = 1f / Mathf.Max(0.1f, body.transform.localScale.y);
        p.transform.localScale = Vector3.one * inv;
        p.transform.localPosition = at * inv;
        p.transform.localRotation = Quaternion.Euler(id == "server" ? -30f : -12f, 0f, 4f);
    }

    GameObject MakeWallit() {
        GameObject prefab = Resources.Load<GameObject>("Wallit/Wallit");
        if (prefab == null) {
            return null;
        }
        GameObject w = Instantiate(prefab);
        BirdArt.CalmEmission(w);
        CharacterMovement.ApplyWallitSkins(w);
        Strip(w);
        var anim = w.GetComponentInChildren<Animator>();
        if (anim != null) {
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // the bat rests on his right shoulder
            Transform hand = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            GameObject bat = CharacterKit.Prop("bat");
            bat.transform.SetParent(hand != null ? hand : w.transform, false);
            bat.transform.localPosition = hand != null ? Vector3.zero : new Vector3(0.4f, 0.9f, 0.1f);
            bat.transform.localRotation = Quaternion.Euler(hand != null ? 0f : -20f, 0f, hand != null ? 90f : 20f);
            bat.transform.localScale = Vector3.one / Mathf.Max(0.1f, hand != null ? hand.lossyScale.y : 1f);
        }
        return w;
    }

    GameObject MakeSlice() {
        GameObject prefab = Resources.Load<GameObject>("Slice/Slice");
        if (prefab == null) {
            return null;
        }
        GameObject s = Instantiate(prefab);
        Musician.ApplySliceSkins(s);
        Strip(s);
        var anim = s.GetComponentInChildren<Animator>();
        if (anim != null) {
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int idle = Animator.StringToHash("idle");
            if (anim.HasState(0, idle)) {
                anim.Play(idle, 0, 0f);
            }
        }
        return s;
    }

    static GameObject MakeBot(BotKind kind) {
        GameObject b = BotArt.Make(kind, Color.white, 0f);
        if (b != null) {
            b.AddComponent<GalleryBotIdle>().Spider = BotRoster.IsSpider(kind);
        }
        return b;
    }

    static void Strip(GameObject go) {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) {
            Destroy(c);
        }
        foreach (MonoBehaviour m in go.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (m is WallitAnimRelay || m is SliceAnimRelay) {
                Destroy(m);
            }
        }
    }

    static int SkinnedTriangles(GameObject go) {
        int t = 0;
        foreach (SkinnedMeshRenderer s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            Mesh m = s.sharedMesh;
            if (m == null) {
                continue;
            }
            for (int i = 0; i < m.subMeshCount; i++) {
                t += (int)(m.GetIndexCount(i) / 3);
            }
        }
        return t;
    }

    void Label(Transform parent, string text, Vector3 at) {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        var tm = go.AddComponent<TextMesh>();
        tm.font = UiSkin.Font(UiSkin.FontRole.Label);
        go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.07f;
        tm.fontSize = 48;
        tm.color = new Color(1f, 0.95f, 0.8f);
    }

    // ---- shots -------------------------------------------------------------------------------------------------------

    Vector3 RowPoint(string id) {
        foreach (Entry e in Entries) {
            if (e.Id == id) {
                return e.Root.transform.position;
            }
        }
        return world.transform.position;
    }

    static float X(int i) { return (i - (Order.Length - 1) * 0.5f) * Spacing; }

    void MakeShots() {
        Vector3 o = world.transform.position;
        Vector3 wide = o + new Vector3(0f, 3.4f, -22f), wideT = o + new Vector3(0f, 1.1f, 0f);
        float hx = (X(0) + X(1)) * 0.5f, bx = (X(2) + X(3)) * 0.5f, lx = (X(10) + X(13)) * 0.5f;
        string[] heroes = { "wallit", "slice" }, bots = { "yo_bot", "spider_bot" }, bosses = { "courier", "plated_alpha", "foreman", "server", "registrar", "order_bearer" }, lodgers = { "tick", "brim", "spare", "chime" };
        Shots.Add(new Shot { Name = "all", Eye = wide, Target = wideT });
        Shots.Add(new Shot { Name = "all-silhouette", Eye = wide, Target = wideT, Silhouette = true });
        Shots.Add(new Shot { Name = "heroes", Eye = o + new Vector3(hx, 1.9f, -7.5f), Target = o + new Vector3(hx, 1.1f, 0f), Who = heroes });
        Shots.Add(new Shot { Name = "heroes-silhouette", Eye = o + new Vector3(hx, 1.9f, -7.5f), Target = o + new Vector3(hx, 1.1f, 0f), Silhouette = true, Who = heroes });
        Shots.Add(new Shot { Name = "bots", Eye = o + new Vector3(bx, 1.9f, -8f), Target = o + new Vector3(bx, 0.9f, 0f), Who = bots });
        Shots.Add(new Shot { Name = "bosses", Eye = o + new Vector3(0f, 2.4f, -15f), Target = o + new Vector3(0f, 1f, 0f), Who = bosses });
        Shots.Add(new Shot { Name = "bosses-silhouette", Eye = o + new Vector3(0f, 2.4f, -15f), Target = o + new Vector3(0f, 1f, 0f), Silhouette = true, Who = bosses });
        Shots.Add(new Shot { Name = "lodgers", Eye = o + new Vector3(lx, 1.9f, -12f), Target = o + new Vector3(lx, 1f, 0f), Who = lodgers });
    }

    // Frames shot i and sets colour or silhouette. The characters not in the shot stay where they are (out of frame).
    public void Go(int i) {
        Shot s = Shots[i];
        Cam.transform.position = s.Eye;
        Cam.transform.LookAt(s.Target);
        SetSilhouette(s.Silhouette);
        Cam.backgroundColor = s.Silhouette ? new Color(0.92f, 0.9f, 0.82f) : new Color(0.16f, 0.17f, 0.24f);
        LastVisibleTriangles = KitValidation.VisibleTriangles(Cam);
    }

    public void SetSilhouette(bool on) {
        Silhouette = on;
        foreach (KeyValuePair<Renderer, Material[]> kv in colour) {
            Renderer r = kv.Key;
            if (r == null) {
                continue;
            }
            if (r.GetComponent<TextMesh>() != null) {
                continue;
            }
            if (on) {
                var arr = new Material[kv.Value.Length];
                for (int k = 0; k < arr.Length; k++) {
                    arr[k] = black;
                }
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            } else {
                r.sharedMaterials = kv.Value;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
        if (sun != null) {
            sun.enabled = !on;
        }
    }

    // Silhouette size of an entry on screen, in pixels (height), used by the playtest to check that nothing is a speck.
    public float ScreenHeight(Entry e) {
        var b = new Bounds(e.Root.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in e.Root.GetComponentsInChildren<Renderer>()) {
            if (r.GetComponent<TextMesh>() != null) {
                continue;
            }
            if (!any) {
                b = r.bounds;
                any = true;
            } else {
                b.Encapsulate(r.bounds);
            }
        }
        if (!any) {
            return 0f;
        }
        Vector3 lo = Cam.WorldToScreenPoint(new Vector3(b.center.x, b.min.y, b.center.z));
        Vector3 hi = Cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
        return Mathf.Abs(hi.y - lo.y);
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }
}

// A Yo Bot or Spider Bot standing idle in the gallery: the head looks about and the body bobs; a spider shifts one leg at a time.
public class GalleryBotIdle : MonoBehaviour {
    public bool Spider;
    Transform head;
    readonly List<Transform> legs = new List<Transform>();
    readonly List<Quaternion> rest = new List<Quaternion>();
    Quaternion headRest;

    void Start() {
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) {
            string n = t.name.ToLowerInvariant();
            if (n == "head") {
                head = t;
                headRest = t.localRotation;
            } else if (Spider && n.StartsWith("leg")) {
                legs.Add(t);
                rest.Add(t.localRotation);
            }
        }
    }

    void Update() {
        float t = Time.time;
        transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 1.6f)) * 0.03f, 0f);
        if (head != null) {
            head.localRotation = headRest * Quaternion.Euler(Mathf.Sin(t * 1.6f) * 3f, Mathf.Sin(t * 0.8f) * 22f, 0f);
        }
        for (int i = 0; i < legs.Count; i++) {
            float lift = Mathf.Max(0f, Mathf.Sin(t * 1.4f + i * 1.9f)) * 12f;
            legs[i].localRotation = rest[i] * Quaternion.Euler(0f, 0f, lift);
        }
    }
}

// Idle for the hub's lodger models in the gallery (the hub only hovers them): Tick sways, Brim hovers and tips, Spare droops
// and straightens, Chime rocks.
public class GalleryLodgerIdle : MonoBehaviour {
    public string Id;
    Vector3 start;
    Quaternion rest;

    void Start() {
        start = transform.localPosition;
        rest = transform.localRotation;
    }

    void Update() {
        float t = Time.time;
        switch (Id) {
            case "tick": transform.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 4f); break;
            case "brim":
                transform.localPosition = start + new Vector3(0f, 0.08f + Mathf.Sin(t * 1.5f) * 0.07f, 0f);
                transform.localRotation = rest * Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(t * 0.7f)) * 14f, 0f, 0f);
                break;
            case "spare": transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 5f + 3f, 0f, Mathf.Sin(t * 0.6f) * 3f); break;
            default: transform.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3.4f) * 6f * Mathf.Abs(Mathf.Sin(t * 0.5f))); break;
        }
    }
}
