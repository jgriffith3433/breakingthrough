using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Model Kit's preview scene (Assets/Maps/KitPreview.unity is an empty scene; this fills it in, like Campaign.unity).
// Three zones, far enough apart that a photo sees only one: the street corner vignette (origin), the interior room vignette
// (x = 100) and the gallery (z = 100): every piece on a labelled grid, one row per palette, with a photo stop for every few
// pieces. The kit-preview playtest walks the camera through the stops (Go) and photographs each one. Left and Right step the
// stops by hand, Escape returns to the menu.
public class KitPreview : MonoBehaviour {
    public static KitPreview Instance { get; private set; }
    public const string SceneName = "KitPreview";

    public struct Shot {
        public string Name, Zone, Palette;
        public Vector3 Pos, Look;
        public float Fov;
        public int Pieces;
    }

    public readonly List<Shot> Shots = new List<Shot>();
    public KitReport Report { get; private set; }
    public Camera Cam { get; private set; }
    public int Current { get; private set; } = -1;
    public int PiecesPlaced { get; private set; }
    public int GalleryPieces { get; private set; }
    public int VignettePieces { get; private set; }
    public int LastVisibleTriangles { get; private set; }
    public int MaxVisibleTriangles { get; private set; }
    public readonly List<string> Timeline = new List<string>();

    Font font;
    Material groundStone, groundRoad, groundWood;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == SceneName && Instance == null) {
            new GameObject("Kit Preview").AddComponent<KitPreview>();
        }
    }

    void Awake() {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Report = KitValidation.Run();
        Timeline.Add("kit-report|" + Report.Summary());
        BuildLights();
        BuildCamera();
        BuildStreet();
        BuildOffice();
        BuildGallery();
        Timeline.Add("kit-built|pieces=" + PiecesPlaced + " shots=" + Shots.Count);
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            Go((Current + 1) % Shots.Count);
        } else if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            Go((Current + Shots.Count - 1) % Shots.Count);
        } else if (Input.GetKeyDown(KeyCode.Escape)) {
            SceneManager.LoadScene(MetaProgress.MenuScene);
        }
    }

    // ---- stage ------------------------------------------------------------------------------------------------------

    void BuildLights() {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.46f, 0.49f, 0.6f);
        RenderSettings.fog = false;
        var sun = new GameObject("Kit Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.9f, 0.76f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, 205f, 0f);
        var fill = new GameObject("Kit Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.55f, 0.65f, 1f);
        fill.intensity = 0.35f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(30f, 70f, 0f);
    }

    void BuildCamera() {
        var go = new GameObject("Kit Camera");
        go.tag = "MainCamera";
        Cam = go.AddComponent<Camera>();
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.1f, 0.13f, 0.22f);
        Cam.fieldOfView = 50f;
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 400f;
        go.AddComponent<AudioListener>();
    }

    Material Ground(string palette, string set, Color mul) {
        Material m = new Material(KitTextures.Get(palette, set));
        m.SetColor("_Color", KitTextures.Tint(palette, set) * mul);
        m.enableInstancing = true;
        return m;
    }

    // A slab of authored ground: a chamfered box mapped at 2 m a tile.
    GameObject Slab(string name, Vector3 centre, Vector3 size, Material mat, float tile = 2f) {
        var km = new KitMesh { Name = name, Tex = tile };
        km.BoxC(Vector3.zero, size, 0.02f);
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = centre;
        go.AddComponent<MeshFilter>().sharedMesh = km.ToMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    GameObject Put(string id, Vector3 pos, float yaw = 0f, Transform parent = null, float scale = 1f) {
        GameObject go = KitCatalog.Make(id, parent != null ? parent : transform);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        PiecesPlaced++;
        return go;
    }

    void AddShot(string name, string zone, string palette, Vector3 pos, Vector3 look, float fov, int pieces) {
        Shots.Add(new Shot { Name = name, Zone = zone, Palette = palette, Pos = pos, Look = look, Fov = fov, Pieces = pieces });
    }

    // ---- vignette A: a street corner --------------------------------------------------------------------------------

    void BuildStreet() {
        int before = PiecesPlaced;
        Vector3 o = Vector3.zero;
        groundStone = Ground("street", "concrete", new Color(0.8f, 0.8f, 0.85f));
        groundRoad = Ground("street", "stone", new Color(0.32f, 0.33f, 0.4f));
        Slab("Pavement", o + new Vector3(2f, -0.06f, 4f), new Vector3(20f, 0.12f, 9f), groundStone);
        Slab("Road", o + new Vector3(2f, -0.08f, 12f), new Vector3(20f, 0.1f, 8f), groundRoad, 3f);
        // two terraces meeting at a corner: A faces +z along x, B faces +x along z
        Put("st_shopfront", o + new Vector3(-2f, 0, 0), 0f);
        Put("st_house_wall", o + new Vector3(2f, 0, 0), 0f);
        Put("st_door_wall", o + new Vector3(6f, 0, 0), 0f);
        Put("st_door_wall", o + new Vector3(-4f, 0, 2.2f), 90f);
        Put("st_house_wall", o + new Vector3(-4f, 0, 6.2f), 90f);
        for (int i = 0; i < 3; i++) {
            Put("st_roof_pitched", o + new Vector3(-2f + i * 4f, 3.02f, -1.35f), 0f);
        }
        Put("st_roof_pitched", o + new Vector3(-5.35f, 3.02f, 2.2f), 90f);
        Put("st_roof_flat", o + new Vector3(-5.35f, 3.0f, 6.2f), 90f);
        Put("st_chimney", o + new Vector3(2.6f, 3.6f, -2.1f), 0f);
        Put("st_awning", o + new Vector3(-2f, 0.5f, 0.15f), 0f);
        for (int i = 0; i < 4; i++) {
            Put("st_kerb", o + new Vector3(-3f + i * 3f, 0, 7.8f), 0f);
        }
        Put("st_lamp_post", o + new Vector3(5f, 0, 6.8f), 0f);
        Put("st_bench", o + new Vector3(-0.4f, 0, 2.6f), 0f);
        Put("st_bin", o + new Vector3(1.6f, 0, 1.3f), 0f);
        Put("st_bin", o + new Vector3(2.35f, 0, 1.3f), 20f);
        Put("st_crate", o + new Vector3(4.6f, 0, 1.4f), 12f);
        Put("st_crate", o + new Vector3(5.5f, 0, 1.5f), -8f);
        Put("st_crate", o + new Vector3(5.0f, 0.88f, 1.4f), 30f);
        Put("st_hydrant", o + new Vector3(-2.2f, 0, 6.9f), 0f);
        var lamp = new GameObject("Street Lamp Light").AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.8f, 0.5f);
        lamp.range = 9f;
        lamp.intensity = 1.2f;
        lamp.transform.position = o + new Vector3(5.6f, 3.6f, 6.8f);
        VignettePieces += PiecesPlaced - before;
        AddShot("street-corner-wide", "street", "street", o + new Vector3(10.5f, 3.4f, 12.5f), o + new Vector3(-1f, 1.4f, 2f), 55f, 0);
        AddShot("street-shopfront", "street", "street", o + new Vector3(-1.5f, 1.6f, 6.2f), o + new Vector3(-2f, 1.5f, 0f), 48f, 0);
        AddShot("street-roofline", "street", "street", o + new Vector3(2f, 2.8f, 8f), o + new Vector3(1f, 3.0f, -1f), 52f, 0);
        AddShot("street-furniture", "street", "street", o + new Vector3(3.2f, 1.3f, 5.4f), o + new Vector3(3f, 0.7f, 1.4f), 50f, 0);
    }

    // ---- vignette B: an interior room -------------------------------------------------------------------------------

    void BuildOffice() {
        int before = PiecesPlaced;
        Vector3 o = new Vector3(100f, 0f, 0f);
        groundWood = Ground("office", "wood", new Color(0.8f, 0.7f, 0.6f));
        Slab("Floor", o + new Vector3(0f, -0.06f, 0f), new Vector3(6.4f, 0.12f, 6.4f), groundWood, 1.5f);
        Put("of_wainscot_door", o + new Vector3(-1.5f, 0, -3f), 0f);
        Put("of_mail_slot_wall", o + new Vector3(1.5f, 0, -3f), 0f);
        Put("of_mail_slot_wall", o + new Vector3(-3f, 0, -1.5f), 90f);
        Put("of_wainscot_door", o + new Vector3(-3f, 0, 1.5f), 90f);
        Put("of_rug", o + new Vector3(0.5f, 0, -0.4f), 0f);
        Put("of_desk", o + new Vector3(0.5f, 0, -1.4f), 0f);
        Put("of_chair", o + new Vector3(0.6f, 0, -0.35f), 190f);
        Put("of_desk_lamp", o + new Vector3(1.05f, 0.79f, -1.3f), -20f);
        Put("of_stamp_press", o + new Vector3(0.0f, 0.79f, -1.35f), 20f);
        Put("of_filing_cabinet", o + new Vector3(2.5f, 0, -2.6f), 0f);
        Put("of_pigeonholes", o + new Vector3(-2.75f, 0, 0.1f), 90f);
        Put("of_coat_stand", o + new Vector3(-2.3f, 0, -2.45f), 0f);
        Put("of_pneumatic_tube", o + new Vector3(2.9f, 0, -2.8f), 0f);
        var lamp = new GameObject("Desk Light").AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.82f, 0.5f);
        lamp.range = 8f;
        lamp.intensity = 1.6f;
        lamp.transform.position = o + new Vector3(1.1f, 1.4f, -1.0f);
        var ceiling = new GameObject("Room Light").AddComponent<Light>();
        ceiling.type = LightType.Point;
        ceiling.color = new Color(1f, 0.9f, 0.7f);
        ceiling.range = 10f;
        ceiling.intensity = 0.8f;
        ceiling.transform.position = o + new Vector3(0f, 2.8f, 0.5f);
        VignettePieces += PiecesPlaced - before;
        AddShot("office-room-wide", "office", "office", o + new Vector3(4.2f, 2.4f, 4.2f), o + new Vector3(-0.3f, 1.1f, -1.3f), 58f, 0);
        AddShot("office-desk", "office", "office", o + new Vector3(1.9f, 1.4f, 1.4f), o + new Vector3(0.4f, 0.8f, -1.3f), 48f, 0);
        AddShot("office-mail-wall", "office", "office", o + new Vector3(1.0f, 1.6f, 1.2f), o + new Vector3(0.6f, 1.4f, -3f), 50f, 0);
        AddShot("office-cabinets", "office", "office", o + new Vector3(0.3f, 1.4f, 2.2f), o + new Vector3(-2.2f, 1.1f, 0.2f), 55f, 0);
    }

    // ---- the gallery ------------------------------------------------------------------------------------------------

    void BuildGallery() {
        KitCatalog.Init();
        const float zBase = 100f;
        const float rowStep = 9f;
        for (int p = 0; p < KitCatalog.Palettes.Length; p++) {
            string palette = KitCatalog.Palettes[p];
            List<KitDef> defs = KitCatalog.OfPalette(palette);
            float z = zBase + p * rowStep;
            float cursor = 0f;
            var group = new List<GameObject>();
            float groupStart = 0f, groupHeight = 0f;
            int groupIndex = 0;
            Material tileMat = Ground(palette, "concrete", new Color(0.55f, 0.55f, 0.62f));
            for (int i = 0; i < defs.Count; i++) {
                KitDef d = defs[i];
                Mesh mesh = KitCatalog.MeshOf(d.Id);
                Vector3 size = mesh.bounds.size;
                float scale = Mathf.Min(1f, 5.2f / Mathf.Max(size.x, size.z), 4.6f / Mathf.Max(0.1f, size.y));
                float width = Mathf.Max(size.x, size.z) * scale;
                float x = cursor + width * 0.5f;
                GameObject go = Put(d.Id, new Vector3(x - mesh.bounds.center.x * scale, 0f, z), 0f, null, scale);
                GalleryPieces++;
                // the piece's own dust floor: a small chamfered tile so it reads against the dark
                Slab(d.Id + " tile", new Vector3(x, -0.05f, z), new Vector3(width + 0.5f, 0.1f, Mathf.Max(size.z * scale, 1.6f) + 0.6f), tileMat, 1.5f);
                MakeLabel(d.Id + (scale < 0.99f ? "  (x" + scale.ToString("0.00") + ")" : ""), new Vector3(x, 0.03f, z + Mathf.Max(size.z * scale, 1.2f) * 0.5f + 0.5f));
                group.Add(go);
                groupHeight = Mathf.Max(groupHeight, size.y * scale);
                cursor += width + 0.9f;
                bool endGroup = (cursor - groupStart) > 11f || i == defs.Count - 1;
                if (endGroup) {
                    float span = cursor - 0.9f - groupStart;
                    float cx = groupStart + span * 0.5f;
                    float dist = span * 0.62f + 4.5f;
                    float height = 1.4f + groupHeight * 0.45f;
                    AddShot("gallery-" + palette + "-" + (groupIndex + 1), "gallery", palette,
                        new Vector3(cx, height, z + dist), new Vector3(cx, groupHeight * 0.42f, z), 50f, group.Count);
                    groupIndex++;
                    group.Clear();
                    groupHeight = 0f;
                    groupStart = cursor;
                }
            }
            MakeLabel(palette.ToUpperInvariant() + "  (" + defs.Count + " pieces)", new Vector3(-2f, 0.03f, z + 3.2f), 0.22f);
        }
    }

    void MakeLabel(string text, Vector3 at, float size = 0.11f) {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(50f, 180f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(0.95f, 0.9f, 0.7f);
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    // ---- camera -----------------------------------------------------------------------------------------------------

    public void Go(int index) {
        if (index < 0 || index >= Shots.Count) {
            return;
        }
        Shot s = Shots[index];
        Current = index;
        Cam.transform.position = s.Pos;
        Cam.transform.LookAt(s.Look);
        Cam.fieldOfView = s.Fov;
        LastVisibleTriangles = KitValidation.VisibleTriangles(Cam);
        MaxVisibleTriangles = Mathf.Max(MaxVisibleTriangles, LastVisibleTriangles);
        Timeline.Add("photo|" + s.Name + " zone=" + s.Zone + " palette=" + s.Palette + " visibleTriangles=" + LastVisibleTriangles);
    }
}
