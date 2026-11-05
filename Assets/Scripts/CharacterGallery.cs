using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The character gallery (Docs/CharacterModels.md): Assets/Maps/CharacterGallery.unity is an empty scene and this fills it in.
// Every character stands in a row, in idle, on a stone floor with a name plate. Shots: the whole line, each group, Wallit and
// Slice close, and a black-fill line (the silhouette check). Left/Right step the shots, S toggles black fill, Escape leaves.
// The character-gallery playtest photographs every shot and exercises walk, tell, strike, hurt and death on the rigs.
public class CharacterGallery : MonoBehaviour {
    public static CharacterGallery Instance { get; private set; }
    public const string SceneName = "CharacterGallery";
    public const float Spacing = 2.6f;

    public struct Shot { public string Name; public Vector3 Pos, Look; public float Fov; public bool Black; }

    public readonly List<Shot> Shots = new List<Shot>();
    public readonly Dictionary<string, CharacterRig> Rigs = new Dictionary<string, CharacterRig>();
    public readonly Dictionary<string, GameObject> Figures = new Dictionary<string, GameObject>();
    public CharacterReport Report { get; private set; }
    public Camera Cam { get; private set; }
    public int Current { get; private set; } = -1;
    public bool Black { get; private set; }

    readonly Dictionary<Renderer, Material[]> realMaterials = new Dictionary<Renderer, Material[]>();
    Material blackMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == SceneName && Instance == null) {
            new GameObject("Character Gallery").AddComponent<CharacterGallery>();
        }
    }

    public static float XOf(int i) { return (i - (CharacterKit.All.Count - 1) * 0.5f) * Spacing; }

    void Awake() {
        Instance = this;
        Report = CharacterValidation.Run();
        BuildStage();
        BuildCharacters();
        BuildCamera();
        BuildShots();
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
        } else if (Input.GetKeyDown(KeyCode.S)) {
            SetBlack(!Black);
        } else if (Input.GetKeyDown(KeyCode.Escape)) {
            SceneManager.LoadScene(MetaProgress.MenuScene);
        }
    }

    void BuildStage() {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.6f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.05f;
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        sun.shadows = LightShadows.Soft;
        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.35f;
        fill.color = new Color(0.7f, 0.8f, 1f);
        fill.transform.rotation = Quaternion.Euler(20f, 160f, 0f);

        var floor = new GameObject("Floor");
        var km = new KitMesh();
        km.Name = "Gallery floor";
        km.Tex = 2f;
        float half = CharacterKit.All.Count * Spacing * 0.5f + 3f;
        km.Box(new Vector3(-half, -0.3f, -3.5f), new Vector3(half, 0f, 3.5f), 0.02f);
        floor.AddComponent<MeshFilter>().sharedMesh = km.ToMesh();
        floor.AddComponent<MeshRenderer>().sharedMaterial = KitTextures.Get("office", "stone");
    }

    void BuildCharacters() {
        CharacterKit.Init();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < CharacterKit.All.Count; i++) {
            CharacterDef d = CharacterKit.All[i];
            GameObject go = CharacterKit.Make(d.Id);
            go.transform.position = new Vector3(XOf(i), 0f, 0f);
            Figures[d.Id] = go;
            Rigs[d.Id] = go.GetComponent<CharacterRig>();
            var plate = new GameObject("Plate " + d.Name);
            plate.transform.SetParent(go.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.01f, 1.0f);
            plate.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            var tm = plate.AddComponent<TextMesh>();
            tm.text = d.Name;
            tm.font = font;
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.95f, 0.8f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        foreach (var kv in Figures) {
            foreach (MeshRenderer r in kv.Value.GetComponentsInChildren<MeshRenderer>()) {
                if (r.GetComponent<MeshFilter>() != null) {
                    realMaterials[r] = r.sharedMaterials;
                }
            }
        }
    }

    void BuildCamera() {
        var go = new GameObject("Gallery Camera");
        Cam = go.AddComponent<Camera>();
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.16f, 0.17f, 0.22f);
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 120f;
        go.tag = "MainCamera";
        if (go.GetComponent<AudioListener>() == null) {
            go.AddComponent<AudioListener>();
        }
    }

    float X(string id) {
        for (int i = 0; i < CharacterKit.All.Count; i++) {
            if (CharacterKit.All[i].Id == id) {
                return XOf(i);
            }
        }
        return 0f;
    }

    void BuildShots() {
        float cx = 0f;
        float width = CharacterKit.All.Count * Spacing;
        Shots.Add(new Shot { Name = "lineup", Pos = new Vector3(cx, 1.6f, -width * 0.62f), Look = new Vector3(cx, 0.9f, 0f), Fov = 40f });
        Shots.Add(new Shot { Name = "silhouettes", Pos = new Vector3(cx, 1.6f, -width * 0.62f), Look = new Vector3(cx, 0.9f, 0f), Fov = 40f, Black = true });
        float hx = (X("wallit") + X("spiderbot")) * 0.5f;
        Shots.Add(new Shot { Name = "heroes-and-bots", Pos = new Vector3(hx, 1.3f, -6.2f), Look = new Vector3(hx, 0.75f, 0f), Fov = 40f });
        float bx = (X("courier") + X("orderbearer")) * 0.5f;
        Shots.Add(new Shot { Name = "bosses", Pos = new Vector3(bx, 1.7f, -7.8f), Look = new Vector3(bx, 1f, 0f), Fov = 40f });
        float lx = (X("tick") + X("chime")) * 0.5f;
        Shots.Add(new Shot { Name = "lodgers", Pos = new Vector3(lx, 0.9f, -4.6f), Look = new Vector3(lx, 0.4f, 0f), Fov = 40f });
        Shots.Add(new Shot { Name = "wallit-close", Pos = new Vector3(X("wallit") + 0.7f, 0.85f, -2.3f), Look = new Vector3(X("wallit"), 0.55f, 0f), Fov = 36f });
        Shots.Add(new Shot { Name = "slice-close", Pos = new Vector3(X("slice") + 1.0f, 1.3f, -3.6f), Look = new Vector3(X("slice"), 0.95f, 0f), Fov = 36f });
        Shots.Add(new Shot { Name = "wallit-slice-silhouette", Pos = new Vector3((X("wallit") + X("slice")) * 0.5f, 1.1f, -4.8f), Look = new Vector3((X("wallit") + X("slice")) * 0.5f, 0.85f, 0f), Fov = 38f, Black = true });
    }

    public void SetBlack(bool on) {
        Black = on;
        if (on && blackMat == null) {
            blackMat = new Material(SafeShader.Find("Sprites/Default"));
            blackMat.color = Color.black;
        }
        foreach (var kv in realMaterials) {
            if (kv.Key == null) {
                continue;
            }
            if (on) {
                var arr = new Material[kv.Value.Length];
                for (int i = 0; i < arr.Length; i++) { arr[i] = blackMat; }
                kv.Key.sharedMaterials = arr;
            } else {
                kv.Key.sharedMaterials = kv.Value;
            }
        }
        Cam.backgroundColor = on ? new Color(0.92f, 0.92f, 0.88f) : new Color(0.16f, 0.17f, 0.22f);
    }

    public void Go(int i) {
        Current = i;
        Shot s = Shots[i];
        Cam.transform.position = s.Pos;
        Cam.transform.LookAt(s.Look);
        Cam.fieldOfView = s.Fov;
        SetBlack(s.Black);
    }
}
