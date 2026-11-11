using UnityEngine;
using UnityEngine.UI;

// The first screen's cast: Wallit with his bat, Slice standing guard behind him and Yo Bots and a Spider Bot closing in,
// the real in-game models lit as one scene (a warm lamp from the front, cold moon rim from behind, one fog colour) and
// shown behind the menu through a RenderTexture. It stands in for Resources/UI/MenuArt when that picture is not there,
// so the first screen always shows the game and never a flat colour. Built in code, no GPU art, no colliders.
public class MenuStage : MonoBehaviour {
    const int StageLayer = 30;
    static readonly Vector3 Origin = new Vector3(0f, -4000f, 0f);
    static readonly Color Fog = new Color(0.07f, 0.09f, 0.16f, 1f);

    public RawImage Image { get; private set; }
    public int ModelsShown { get; private set; }

    Camera cam;
    RenderTexture target;
    Transform wallit;
    Transform slice;
    readonly System.Collections.Generic.List<Transform> bots = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<Vector3> botHome = new System.Collections.Generic.List<Vector3>();
    float born;

    // Makes the stage and puts its picture in `parent`, stretched to the anchors given. Returns null if nothing could be built.
    public static MenuStage Make(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float aspect) {
        var go = new GameObject("Menu Stage");
        go.transform.SetParent(parent, false);
        var stage = go.AddComponent<MenuStage>();
        stage.Build(parent, anchorMin, anchorMax, aspect);
        return stage;
    }

    void Build(Transform ui, Vector2 anchorMin, Vector2 anchorMax, float aspect) {
        born = Time.unscaledTime;
        int h = 720;
        int w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 360, 1280);
        if (aspect < 1f) {
            w = 540;
            h = Mathf.Clamp(Mathf.RoundToInt(w / aspect), 540, 1100);
        }
        target = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        target.name = "Menu Stage";
        target.antiAliasing = 1;

        var root = new GameObject("Stage World").transform;
        root.SetParent(transform, false);
        root.position = Origin;

        // Ground: one dark disc of kit-coloured paving, so the cast stands on something and not in the void.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ground.GetComponent<Collider>());
        ground.name = "Stage Floor";
        ground.transform.SetParent(root, false);
        ground.transform.localScale = new Vector3(26f, 0.05f, 26f);
        ground.transform.localPosition = new Vector3(0f, -0.05f, 4f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.12f, 0.13f, 0.2f));

        // The circle Slice guards: a ring of paler stone around her.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "Guard Circle";
        ring.transform.SetParent(root, false);
        ring.transform.localScale = new Vector3(5.2f, 0.02f, 5.2f);
        ring.transform.localPosition = new Vector3(0.4f, 0.01f, 3.2f);
        ring.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.2f, 0.2f, 0.3f));

        // Slice behind, Wallit in front, bots closing from the left of the frame.
        GameObject s = MakeSlice();
        if (s != null) {
            slice = s.transform;
            slice.SetParent(root, false);
            slice.localPosition = new Vector3(0.9f, 0f, 3.8f);
            slice.localRotation = Quaternion.Euler(0f, 195f, 0f);
            slice.localScale = Vector3.one * 1.05f;
            ModelsShown++;
        }
        GameObject wa = MakeWallit();
        if (wa != null) {
            wallit = wa.transform;
            wallit.SetParent(root, false);
            wallit.localPosition = new Vector3(-0.7f, 0f, 1.6f);
            wallit.localRotation = Quaternion.Euler(0f, 160f, 0f);
            ModelsShown++;
        }
        AddBot(root, BotKind.YoBot, new Vector3(-3.6f, 0f, 5.4f), 118f, 1f);
        AddBot(root, BotKind.YoBot, new Vector3(-1.7f, 0f, 7.2f), 150f, 1f);
        AddBot(root, BotKind.PlatedYoBot, new Vector3(2.9f, 0f, 7.6f), 190f, 1f);
        AddBot(root, BotKind.SpiderBot, new Vector3(-5.4f, 0f, 8.4f), 140f, 1f);

        // One palette: every renderer on the stage shares the layer, the light and the fog colour.
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            t.gameObject.layer = StageLayer;
        }

        MakeLight(root, "Lamp", new Vector3(30f, 200f, 0f), new Color(1f, 0.82f, 0.56f), 1.15f);
        MakeLight(root, "Moon", new Vector3(25f, 20f, 0f), new Color(0.5f, 0.62f, 1f), 0.65f);

        var camGo = new GameObject("Stage Camera");
        camGo.transform.SetParent(root, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Fog;
        cam.cullingMask = 1 << StageLayer;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 60f;
        cam.fieldOfView = aspect < 1f ? 40f : 34f;
        cam.targetTexture = target;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.depth = -50f;
        cam.useOcclusionCulling = false;
        camGo.transform.localPosition = aspect < 1f ? new Vector3(0.6f, 2.4f, -6.2f) : new Vector3(1.2f, 2.0f, -5.2f);
        camGo.transform.LookAt(root.position + new Vector3(aspect < 1f ? 0.2f : -0.4f, 1.2f, 4.2f));

        var imgGo = new GameObject("Stage Image", typeof(RectTransform));
        imgGo.transform.SetParent(ui, false);
        Image = imgGo.AddComponent<RawImage>();
        Image.texture = target;
        Image.raycastTarget = false;
        var rt = Image.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        // The stage's image is a child of the menu canvas, so it goes with it when the menu rebuilds.
        transform.SetParent(ui, false);
    }

    void AddBot(Transform root, BotKind kind, Vector3 at, float yaw, float scale) {
        GameObject b = BotArt.Make(kind, Color.white, 0f);
        if (b == null) {
            return;
        }
        foreach (Collider c in b.GetComponentsInChildren<Collider>(true)) {
            Destroy(c);
        }
        // The idle script moves its own object, so the bot stands inside a holder that keeps the spot.
        var holder = new GameObject("Bot " + kind).transform;
        holder.SetParent(root, false);
        holder.localPosition = at;
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        holder.localScale = Vector3.one * scale;
        b.transform.SetParent(holder, false);
        b.AddComponent<GalleryBotIdle>().Spider = BotRoster.IsSpider(kind);
        bots.Add(holder);
        botHome.Add(at);
        ModelsShown++;
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
            Transform hand = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            GameObject bat = CharacterKit.Prop("bat");
            if (bat != null) {
                bat.transform.SetParent(hand != null ? hand : w.transform, false);
                bat.transform.localPosition = hand != null ? Vector3.zero : new Vector3(0.4f, 0.9f, 0.1f);
                bat.transform.localRotation = Quaternion.Euler(hand != null ? 0f : -20f, 0f, hand != null ? 90f : 20f);
                bat.transform.localScale = Vector3.one / Mathf.Max(0.1f, hand != null ? hand.lossyScale.y : 1f);
            }
            int idle = Animator.StringToHash("idle");
            if (anim.HasState(0, idle)) {
                anim.Play(idle, 0, 0f);
            }
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

    static Material Matte(Color c) {
        Shader shader = SafeShader.For(c);
        if (shader == null) {
            shader = Shader.Find("Sprites/Default");
        }
        return new Material(shader) { color = c };
    }

    static void MakeLight(Transform root, string name, Vector3 euler, Color color, float intensity) {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.rotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = color;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.cullingMask = 1 << StageLayer;
    }

    void Update() {
        // The camera drifts a little so the first screen breathes; the bots shuffle on their idles.
        if (cam == null) {
            return;
        }
        float t = Time.unscaledTime - born;
        float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f));
        if (Image != null) {
            Image.color = new Color(1f, 1f, 1f, ease);
        }
        float sway = Mathf.Sin(t * 0.35f) * 0.25f;
        Vector3 p = cam.transform.localPosition;
        cam.transform.localPosition = new Vector3(p.x + (sway - lastSway), p.y, p.z);
        lastSway = sway;
    }

    float lastSway;

    void OnDestroy() {
        if (target != null) {
            target.Release();
            Destroy(target);
        }
    }
}
