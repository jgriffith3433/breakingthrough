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
    // What the playtest reads: the camera's drift, how many bots are up over the hedge now, the coin flips and the push-in.
    public int BotsUp { get; private set; }
    public int CoinFlips { get; private set; }
    public int PeeksSeen { get; private set; }
    public bool Pushing { get { return pushAt >= 0f; } }
    public float PushDepth { get { return pushK; } }
    public Vector3 CameraLocal { get { return cam != null ? cam.transform.localPosition : Vector3.zero; } }

    Camera cam;
    RenderTexture target;
    bool handedOff;
    Transform wallit;
    Transform slice;
    Transform batHolder;
    Quaternion batRest;
    Transform coin;
    Light lampLight;
    readonly System.Collections.Generic.List<Transform> bots = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<Vector3> botHome = new System.Collections.Generic.List<Vector3>();
    readonly System.Collections.Generic.List<bool> botPeeks = new System.Collections.Generic.List<bool>();
    readonly System.Collections.Generic.List<Transform> papers = new System.Collections.Generic.List<Transform>();
    float born;
    bool tall;
    Vector3 camBase;
    Vector3 lookBase;
    float pushAt = -1f;
    float pushK;
    Vector2 par;
    float coinStart = -1f;
    float nextCoin = 1.2f;
    float twirlStart = -1f;
    float nextTwirl = 3f;
    bool wasUp;

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
        tall = aspect < 1f;
        int h = 720;
        int w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 360, 1280);
        if (tall) {
            // A phone: a light picture, the work is the frame rate.
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
        ground.transform.localScale = new Vector3(40f, 0.05f, 40f);
        ground.transform.localPosition = new Vector3(0f, -0.05f, 6f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.1f, 0.14f, 0.2f));

        // The circle Slice guards: a ring of paler stone around her.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "Guard Circle";
        ring.transform.SetParent(root, false);
        ring.transform.localScale = new Vector3(5.6f, 0.02f, 5.6f);
        ring.transform.localPosition = new Vector3(0.4f, 0.01f, 3.0f);
        ring.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.22f, 0.22f, 0.33f));

        // Slice behind, Wallit in front.
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

        // The hedge along the back of the field, and Yo Bots behind it that peek over and duck back.
        MakeHedge(root);
        AddBot(root, BotKind.YoBot, new Vector3(-3.2f, 0f, 9.7f), 172f, 1f, true);
        AddBot(root, BotKind.YoBot, new Vector3(0.2f, 0f, 9.9f), 186f, 1f, true);
        AddBot(root, BotKind.PlatedYoBot, new Vector3(3.4f, 0f, 9.6f), 194f, 1f, true);
        AddBot(root, BotKind.YoBot, new Vector3(-6.2f, 0f, 9.8f), 160f, 1f, true);
        AddBot(root, BotKind.SpiderBot, new Vector3(-5.8f, 0f, 15.5f), 165f, 1f, false);

        MakeLamp(root, new Vector3(3.6f, 0f, 5.6f));
        MakeMoon(root);
        for (int i = 0; i < 9; i++) {
            var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(paper.GetComponent<Collider>());
            paper.name = "Drifting Paper";
            paper.transform.SetParent(root, false);
            paper.transform.localScale = new Vector3(0.3f, 0.01f, 0.22f);
            paper.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.86f, 0.84f, 0.76f));
            paper.transform.localPosition = new Vector3(Mathf.Lerp(-9f, 9f, i / 8f), 0.5f + (i % 4) * 0.55f, 1.5f + (i * 1.9f) % 9f);
            papers.Add(paper.transform);
        }
        var coinGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(coinGo.GetComponent<Collider>());
        coinGo.name = "Coin";
        coinGo.transform.SetParent(root, false);
        coinGo.transform.localScale = new Vector3(0.16f, 0.012f, 0.16f);
        coinGo.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(1f, 0.8f, 0.3f));
        coinGo.SetActive(false);
        coin = coinGo.transform;

        // One palette: every renderer on the stage shares the layer, the light and the fog colour.
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            t.gameObject.layer = StageLayer;
        }

        MakeLight(root, "Lamp Fill", new Vector3(30f, 200f, 0f), new Color(1f, 0.82f, 0.56f), 0.9f);
        MakeLight(root, "Moon", new Vector3(25f, 20f, 0f), new Color(0.5f, 0.62f, 1f), 0.7f);

        var camGo = new GameObject("Stage Camera");
        camGo.transform.SetParent(root, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Fog;
        cam.cullingMask = 1 << StageLayer;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 90f;
        cam.fieldOfView = tall ? 42f : 34f;
        cam.targetTexture = target;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.depth = -50f;
        cam.useOcclusionCulling = false;
        // Tall: the cast sits in the upper two thirds (the look point is low, so they ride high), Play has the lower third.
        // Wide: the cast sits right of the menu column (the look point is left of them).
        camBase = tall ? new Vector3(0.7f, 2.3f, -5.6f) : new Vector3(0.9f, 1.9f, -5.0f);
        lookBase = tall ? new Vector3(0.3f, -0.15f, 4.2f) : new Vector3(-1.5f, 1.0f, 4.2f);
        PlaceCamera(0f);

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

    // Hedge: a row of dark rounded clumps, a metre tall, in front of where the bots stand.
    void MakeHedge(Transform root) {
        Material leaf = Matte(new Color(0.07f, 0.16f, 0.12f));
        for (int i = 0; i < 17; i++) {
            var clump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(clump.GetComponent<Collider>());
            clump.name = "Hedge";
            clump.transform.SetParent(root, false);
            float x = -9.6f + i * 1.2f;
            clump.transform.localScale = new Vector3(1.9f, 1.25f + 0.12f * Mathf.Sin(i * 2.3f), 1.3f);
            clump.transform.localPosition = new Vector3(x, 0.45f, 8.7f + 0.15f * Mathf.Sin(i * 1.7f));
            clump.GetComponent<MeshRenderer>().sharedMaterial = leaf;
        }
    }

    // A lamp post with a warm glass and a light that flickers.
    void MakeLamp(Transform root, Vector3 at) {
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pole.GetComponent<Collider>());
        pole.name = "Lamp Post";
        pole.transform.SetParent(root, false);
        pole.transform.localScale = new Vector3(0.12f, 1.3f, 0.12f);
        pole.transform.localPosition = at + new Vector3(0f, 1.3f, 0f);
        pole.GetComponent<MeshRenderer>().sharedMaterial = Matte(new Color(0.05f, 0.05f, 0.08f));
        var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(glass.GetComponent<Collider>());
        glass.name = "Lamp Glass";
        glass.transform.SetParent(root, false);
        glass.transform.localScale = Vector3.one * 0.38f;
        glass.transform.localPosition = at + new Vector3(0f, 2.75f, 0f);
        var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.82f, 0.45f) };
        glass.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var lightGo = new GameObject("Lamp Light");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = at + new Vector3(0f, 2.6f, -0.3f);
        lampLight = lightGo.AddComponent<Light>();
        lampLight.type = LightType.Point;
        lampLight.color = new Color(1f, 0.74f, 0.4f);
        lampLight.range = 11f;
        lampLight.intensity = 1.6f;
        lampLight.shadows = LightShadows.None;
        lampLight.cullingMask = 1 << StageLayer;
    }

    static void MakeMoon(Transform root) {
        var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(moon.GetComponent<Collider>());
        moon.name = "Moon";
        moon.transform.SetParent(root, false);
        moon.transform.localScale = Vector3.one * 5f;
        moon.transform.localPosition = new Vector3(-8f, 15f, 60f);
        moon.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.8f, 0.86f, 1f) };
    }

    void AddBot(Transform root, BotKind kind, Vector3 at, float yaw, float scale, bool peeks) {
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
        botPeeks.Add(peeks);
        if (peeks) {
            holder.localPosition = at + new Vector3(0f, -1.5f, 0f);
        }
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
                batHolder = bat.transform;
                batRest = bat.transform.localRotation;
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

    // The camera: a slow drift over the Hollow, a slight parallax from the pointer or a tilted phone, and on Play a push-in
    // toward Slice's ring.
    void PlaceCamera(float t) {
        Vector3 drift = new Vector3(Mathf.Sin(t * 0.18f) * 0.55f, Mathf.Sin(t * 0.13f) * 0.12f, Mathf.Sin(t * 0.09f) * 0.35f);
        if (ComfortOptions.ReducedMotion) {
            drift *= 0.25f;
        }
        Vector3 pos = camBase + drift + new Vector3(par.x * 0.5f, par.y * 0.25f, 0f);
        Vector3 look = lookBase + new Vector3(par.x * 0.25f, par.y * 0.1f, 0f);
        if (pushK > 0f) {
            // In toward the ring: stop a couple of metres short of Slice and look at her.
            Vector3 ring = new Vector3(0.9f, 1.25f, 3.8f);
            Vector3 end = ring + new Vector3(-0.25f, 0.15f, -2.6f);
            float e = pushK * pushK * (3f - 2f * pushK);
            pos = Vector3.Lerp(pos, end, e);
            look = Vector3.Lerp(look, ring, e);
            cam.fieldOfView = Mathf.Lerp(tall ? 42f : 34f, 30f, e);
        }
        cam.transform.localPosition = pos;
        cam.transform.LookAt(cam.transform.parent.position + look);
    }

    // Play was pressed: fly in toward Slice's ring. Returns the seconds it takes.
    public float PushIn() {
        if (pushAt < 0f) {
            pushAt = Time.unscaledTime;
        }
        return 0.75f;
    }

    // The last frame is kept for the run to dissolve out of: the picture outlives this menu.
    public RenderTexture TakePicture() {
        handedOff = true;
        return target;
    }

    void Update() {
        if (cam == null) {
            return;
        }
        float t = Time.unscaledTime - born;
        float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f));
        if (Image != null) {
            Image.color = new Color(1f, 1f, 1f, ease);
        }
        // Parallax: the pointer on a desktop, a tilted phone on touch. Smoothed so it never twitches.
        Vector2 want = Vector2.zero;
        if (UiFit.Touch) {
            Vector3 a = Input.acceleration;
            if (a.sqrMagnitude > 0.0001f) {
                want = new Vector2(Mathf.Clamp(a.x * 2f, -1f, 1f), Mathf.Clamp((a.y + 0.7f) * 2f, -1f, 1f));
            }
        } else if (UnityEngine.Screen.width > 0 && UnityEngine.Screen.height > 0) {
            Vector3 m = Input.mousePosition;
            want = new Vector2(Mathf.Clamp((m.x / UnityEngine.Screen.width - 0.5f) * 2f, -1f, 1f), Mathf.Clamp((m.y / UnityEngine.Screen.height - 0.5f) * 2f, -1f, 1f));
        }
        if (ComfortOptions.ReducedMotion) {
            want *= 0.3f;
        }
        par = Vector2.Lerp(par, want, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        if (pushAt >= 0f) {
            pushK = Mathf.Clamp01((Time.unscaledTime - pushAt) / 0.75f);
        }
        PlaceCamera(t);

        // Lamp light flickers: two noises, never dark.
        if (lampLight != null) {
            float f = Mathf.PerlinNoise(t * 7f, 3.1f) * 0.6f + Mathf.PerlinNoise(t * 19f, 8.7f) * 0.3f;
            lampLight.intensity = 1.1f + f * 1.0f;
        }
        TickBots(t);
        TickCoinAndBat(t);
        for (int i = 0; i < papers.Count; i++) {
            Transform p = papers[i];
            Vector3 pos = p.localPosition;
            pos.x += (0.55f + 0.12f * i % 3) * Time.unscaledDeltaTime;
            pos.y = 0.6f + (i % 4) * 0.5f + Mathf.Sin(t * 0.9f + i * 1.7f) * 0.35f;
            pos.z += Mathf.Sin(t * 0.5f + i) * 0.2f * Time.unscaledDeltaTime;
            if (pos.x > 10f) {
                pos.x = -10f;
            }
            p.localPosition = pos;
            p.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f + i) * 50f, t * (40f + i * 9f), Mathf.Cos(t * 0.9f + i * 2f) * 40f);
        }
    }

    // Each Yo Bot rises over the hedge, looks about, and ducks back, on its own beat.
    void TickBots(float t) {
        int up = 0;
        for (int i = 0; i < bots.Count; i++) {
            if (bots[i] == null) {
                continue;
            }
            if (!botPeeks[i]) {
                continue;
            }
            float period = 6.5f + i * 1.9f;
            float phase = (t + i * 2.3f) % period;
            // Up for ~2.6 s of each cycle: 0.35 s rising, a hold, 0.3 s ducking.
            float rise = 0f;
            if (phase < 2.6f) {
                rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / 0.35f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((2.6f - phase) / 0.3f));
            }
            if (rise > 0.8f) {
                up++;
            }
            bots[i].localPosition = botHome[i] + new Vector3(0f, -1.5f + 1.5f * rise, 0f);
        }
        BotsUp = up;
        bool isUp = up > 0;
        if (isUp && !wasUp) {
            PeeksSeen++;
        }
        wasUp = isUp;
    }

    // Wallit flips a coin now and then and twirls the bat on his shoulder.
    void TickCoinAndBat(float t) {
        if (wallit == null) {
            return;
        }
        if (coinStart < 0f && t > nextCoin) {
            coinStart = t;
            CoinFlips++;
            coin.gameObject.SetActive(true);
        }
        if (coinStart >= 0f) {
            float k = (t - coinStart) / 1.0f;
            Vector3 hand = wallit.localPosition + new Vector3(0.15f, 1.15f, 0f);
            if (k >= 1f) {
                coin.gameObject.SetActive(false);
                coinStart = -1f;
                nextCoin = t + 2.2f;
            } else {
                coin.localPosition = hand + new Vector3(0f, 1.6f * 4f * k * (1f - k), 0f);
                coin.localRotation = Quaternion.Euler(k * 1080f, 0f, 15f);
            }
        }
        if (batHolder != null) {
            if (twirlStart < 0f && t > nextTwirl) {
                twirlStart = t;
            }
            if (twirlStart >= 0f) {
                float k = (t - twirlStart) / 1.1f;
                if (k >= 1f) {
                    twirlStart = -1f;
                    nextTwirl = t + 4.5f;
                    batHolder.localRotation = batRest;
                } else {
                    float e = Mathf.SmoothStep(0f, 1f, k);
                    batHolder.localRotation = batRest * Quaternion.Euler(0f, e * 720f, 0f);
                }
            }
        }
    }

    void OnDestroy() {
        if (target != null && !handedOff) {
            target.Release();
            Destroy(target);
        }
    }
}

// The picture of the stage's last frame, kept over the run's first frames and dissolved away, so Play flows from the title
// into the game with no black between. It lives across the scene change on its own overlay canvas, and goes when the run is up.
public class MenuHandoff : MonoBehaviour {
    RawImage image;
    RenderTexture picture;
    float born;
    float upAt = -1f;
    public static bool Active { get; private set; }
    // Longest stretch the overlay was the only thing on screen, for the playtest's black-frame check.
    public static float CoverSeconds { get; private set; }

    public static void Begin(RenderTexture picture) {
        var go = new GameObject("Menu Handoff");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        var imgGo = new GameObject("Last Frame", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var raw = imgGo.AddComponent<RawImage>();
        raw.texture = picture;
        raw.raycastTarget = false;
        var rt = raw.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var h = go.AddComponent<MenuHandoff>();
        h.image = raw;
        h.picture = picture;
        h.born = Time.unscaledTime;
        Active = true;
        CoverSeconds = 0f;
    }

    void Update() {
        float now = Time.unscaledTime;
        bool running = WaveDirector.Instance != null;
        if (running && upAt < 0f) {
            upAt = now;
        }
        // Hold for the first frames of the run so the camera has settled, then dissolve over half a second.
        float fade = upAt < 0f ? 0f : Mathf.Clamp01((now - upAt - 0.15f) / 0.5f);
        // Never longer than 3 s on unscaled time, whatever the load does: the overlay takes no taps but it must not linger.
        if (upAt < 0f && now - born > 3f) {
            fade = 1f;
        }
        image.color = new Color(1f, 1f, 1f, 1f - fade);
        if (fade >= 1f) {
            CoverSeconds = now - born;
            Active = false;
            if (picture != null) {
                picture.Release();
                Destroy(picture);
            }
            Destroy(gameObject);
        }
    }
}
