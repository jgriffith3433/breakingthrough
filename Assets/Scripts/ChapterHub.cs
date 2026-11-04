using UnityEngine;
using UnityEngine.UI;

// The hub between chapters (Docs/Story.md, "The hub"): the circle at rest, small enough to walk across. Wallit walks on the
// stick, Slice stands in the ring with her knife out, and the chapter board waits at the far side. Walk up and press E (or
// Enter) to read it: every chapter the case file has reached is on it, cleared ones can be replayed, the next one is
// marked. Everything is built here in code (flat, collider-free, like the chapter props); ChapterScene owns it and the
// chapter card the board leads to. Wallit's level and abilities (CampaignProgress) show top left.
public class ChapterHub : MonoBehaviour {
    public static ChapterHub Instance { get; private set; }

    public static readonly Vector3 BoardPosition = new Vector3(0f, 0f, 10f);
    public const float BoardReach = 3.6f;
    const float Speed = 6f;
    const float YardRadius = 17f;

    // For the playtest bot.
    public Transform Player { get; private set; }
    public bool BoardOpen { get; private set; }
    public bool NearBoard { get; private set; }
    public float Walked { get; private set; }
    public Button[] BoardButtons { get; private set; }
    public Button CloseButton { get; private set; }
    public string StatusText { get { return status != null ? status.text : ""; } }
    public string BoardText { get; private set; } = "";
    public int ArrivalShown { get; private set; }

    ChapterScene scene;
    Camera cam;
    GameObject world;
    GameObject canvasGo;
    Text status;
    Text prompt;
    Text speech;
    GameObject board;
    Animator wallitAnim;
    Transform slice;
    Animator sliceAnim;
    float arrivedAt;
    int lastClip = -1;
    int laidStamp = -1;
    RectTransform column;
    CanvasScaler scaler;

    public static ChapterHub Open(ChapterScene owner) {
        var go = new GameObject("Hub");
        var hub = go.AddComponent<ChapterHub>();
        hub.scene = owner;
        hub.Build();
        return hub;
    }

    void Awake() {
        Instance = this;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    // Shown while no chapter card is up.
    public void SetShown(bool shown) {
        if (world != null) {
            world.SetActive(shown);
        }
        if (canvasGo != null) {
            canvasGo.SetActive(shown);
        }
        if (shown) {
            arrivedAt = Time.unscaledTime;
            ArrivalShown = 0;
            CloseBoard();
        }
        if (cam != null) {
            cam.cullingMask = shown ? -1 : 0;
        }
    }

    public bool Shown {
        get { return canvasGo != null && canvasGo.activeSelf; }
    }

    void Build() {
        arrivedAt = Time.unscaledTime;
        cam = Camera.main;
        if (cam != null) {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.05f, 0.1f, 1f);
            cam.cullingMask = -1;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
        }
        world = new GameObject("Hub World");
        world.transform.SetParent(transform, false);

        var light = new GameObject("Hub Light").AddComponent<Light>();
        light.transform.SetParent(world.transform, false);
        light.type = LightType.Directional;
        light.color = new Color(0.65f, 0.72f, 1f);
        light.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Flat("Yard", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0f), new Vector3(YardRadius * 2f + 4f, 0.05f, YardRadius * 2f + 4f), new Color(0.08f, 0.12f, 0.13f));
        Flat("Ring Edge", PrimitiveType.Cylinder, new Vector3(0f, -0.03f, 0f), new Vector3(8.6f, 0.03f, 8.6f), new Color(0.85f, 0.66f, 0.34f));
        Flat("Ring", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(8f, 0.03f, 8f), new Color(0.11f, 0.15f, 0.16f));
        for (int i = 0; i < 6; i++) {
            // Worn slabs from the ring to the board.
            Flat("Slab", PrimitiveType.Cube, new Vector3(0f, -0.01f, 4.8f + i * 0.95f), new Vector3(1.6f, 0.03f, 0.75f), new Color(0.2f, 0.22f, 0.25f));
        }
        BuildBoard();
        BuildPeople();
        BuildUi();
        PlaceCamera(true);
    }

    void Flat(string name, PrimitiveType type, Vector3 at, Vector3 size, Color color) {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(world.transform, false);
        go.transform.localPosition = at;
        go.transform.localScale = size;
        Paint(go, color);
    }

    static void Paint(GameObject go, Color color) {
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Shader shader = SafeShader.Find("Sprites/Default");
        if (shader != null) {
            r.material = new Material(shader) { color = color };
        } else {
            UpgradeLook.Paint(go, color);
        }
    }

    // Two posts and a pinned board of slips, facing the ring. The paper is lit so it reads from across the yard.
    void BuildBoard() {
        board = new GameObject("Chapter Board");
        board.transform.SetParent(world.transform, false);
        board.transform.position = BoardPosition;
        Part("PostL", new Vector3(-2.3f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 0.25f), new Color(0.3f, 0.2f, 0.12f));
        Part("PostR", new Vector3(2.3f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 0.25f), new Color(0.3f, 0.2f, 0.12f));
        Part("Backing", new Vector3(0f, 2.0f, 0.05f), new Vector3(4.8f, 2.6f, 0.15f), new Color(0.24f, 0.16f, 0.1f));
        for (int i = 0; i < CampaignSave.ChapterCount; i++) {
            float col = i % 3;
            float row = i / 3;
            Part("Slip", new Vector3(-1.5f + col * 1.5f, 2.55f - row * 1.15f, -0.06f), new Vector3(1.15f, 0.85f, 0.04f), new Color(0.93f, 0.89f, 0.76f));
        }
        var text = new GameObject("Title");
        text.transform.SetParent(board.transform, false);
        text.transform.localPosition = new Vector3(0f, 3.55f, -0.1f);
        var mesh = text.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = font;
        mesh.text = "CHAPTER BOARD";
        mesh.characterSize = 0.16f;
        mesh.fontSize = 48;
        mesh.fontStyle = FontStyle.Bold;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(1f, 0.82f, 0.38f);
        text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    void Part(string name, Vector3 local, Vector3 size, Color color) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(board.transform, false);
        go.transform.localPosition = local;
        go.transform.localScale = size;
        Paint(go, color);
    }

    // Wallit (the same painted model as the chapters) and Slice in the ring. Stand-ins when a model is missing.
    void BuildPeople() {
        GameObject wallitPrefab = Resources.Load<GameObject>("Wallit/Wallit");
        GameObject wallit;
        if (wallitPrefab != null) {
            wallit = Instantiate(wallitPrefab);
            BirdArt.CalmEmission(wallit);
            CharacterMovement.ApplyWallitSkins(wallit);
            wallitAnim = wallit.GetComponent<Animator>();
            if (wallitAnim == null) {
                wallitAnim = wallit.GetComponentInChildren<Animator>();
            }
            if (wallitAnim != null) {
                wallitAnim.applyRootMotion = false;
            }
        } else {
            wallit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Paint(wallit, new Color(0.55f, 0.36f, 0.16f));
            wallit.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        }
        wallit.name = "Wallit";
        StripPhysics(wallit);
        wallit.transform.SetParent(world.transform, false);
        wallit.transform.position = new Vector3(4.5f, wallitPrefab != null ? 0f : 0.9f, -6f);
        wallit.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        Player = wallit.transform;

        GameObject slicePrefab = Resources.Load<GameObject>("Slice/Slice");
        GameObject sliceGo;
        if (slicePrefab != null) {
            sliceGo = Instantiate(slicePrefab);
            Musician.ApplySliceSkins(sliceGo);
            sliceAnim = sliceGo.GetComponent<Animator>();
            if (sliceAnim == null) {
                sliceAnim = sliceGo.GetComponentInChildren<Animator>();
            }
            if (sliceAnim != null) {
                sliceAnim.applyRootMotion = false;
                int idle = Animator.StringToHash("idle");
                if (sliceAnim.HasState(0, idle)) {
                    sliceAnim.Play(idle, 0, 0f);
                }
            }
        } else {
            sliceGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Paint(sliceGo, new Color(0.14f, 0.12f, 0.2f));
            sliceGo.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        }
        sliceGo.name = "Slice";
        StripPhysics(sliceGo);
        sliceGo.transform.SetParent(world.transform, false);
        sliceGo.transform.position = new Vector3(0f, slicePrefab != null ? 0f : 1f, 0f);
        slice = sliceGo.transform;
    }

    static void StripPhysics(GameObject go) {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) {
            Destroy(c);
        }
        foreach (MonoBehaviour m in go.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (m is WallitAnimRelay || m is SliceAnimRelay) {
                Destroy(m);
            }
        }
    }

    void Update() {
        if (!Shown || scene == null) {
            return;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        if (PlayInput.GetKeyDown(KeyCode.Escape)) {
            if (BoardOpen) {
                CloseBoard();
            } else {
                scene.LeaveToMenu();
                return;
            }
        }
        Walk();
        TickSpeech();
        NearBoard = (Player.position - BoardPosition).magnitude <= BoardReach;
        if (!BoardOpen) {
            prompt.text = NearBoard ? "E: read the chapter board" : "Walk to the chapter board (WASD)";
            if (NearBoard && (PlayInput.GetKeyDown(KeyCode.E) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter))) {
                OpenBoard();
            }
        } else {
            prompt.text = "";
        }
        status.text = StatusLine();
    }

    void LateUpdate() {
        if (Shown && Player != null) {
            PlaceCamera(false);
        }
    }

    void Walk() {
        Vector3 move = Vector3.zero;
        if (!BoardOpen) {
            move = new Vector3(PlayInput.GetAxis("Horizontal"), 0f, PlayInput.GetAxis("Vertical"));
            if (move.sqrMagnitude > 1f) {
                move.Normalize();
            }
        }
        bool moving = move.sqrMagnitude > 0.01f;
        if (moving) {
            Vector3 next = Player.position + move * Speed * Time.unscaledDeltaTime;
            Vector3 flat = new Vector3(next.x, 0f, next.z);
            if (flat.magnitude > YardRadius) {
                flat = flat.normalized * YardRadius;
            }
            // Slice's circle is hers: Wallit may cross the ring but not stand on her.
            if (flat.magnitude < 1.2f) {
                flat = flat.normalized * 1.2f;
            }
            next = new Vector3(flat.x, Player.position.y, flat.z);
            Walked += (next - Player.position).magnitude;
            Player.position = next;
            Player.rotation = Quaternion.Slerp(Player.rotation, Quaternion.LookRotation(move), 12f * Time.unscaledDeltaTime);
        }
        int clip = moving ? CharacterMovement.CLIP_RUN : CharacterMovement.CLIP_IDLE;
        if (wallitAnim != null && clip != lastClip) {
            lastClip = clip;
            CharacterMovement.PlayClip(wallitAnim, clip);
        }
        Vector3 toWallit = Player.position - slice.position;
        toWallit.y = 0f;
        if (toWallit.sqrMagnitude > 0.1f) {
            slice.rotation = Quaternion.Slerp(slice.rotation, Quaternion.LookRotation(toWallit), 3f * Time.unscaledDeltaTime);
        }
    }

    void PlaceCamera(bool snap) {
        if (cam == null || Player == null) {
            return;
        }
        Vector3 focus = Player.position + Vector3.up * 1f;
        Vector3 want = new Vector3(focus.x * 0.8f, 8.5f, focus.z - 10f);
        cam.transform.position = snap ? want : Vector3.Lerp(cam.transform.position, want, 6f * Time.unscaledDeltaTime);
        cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position);
    }

    // ---------------------------------------------------------------- Words

    string StatusLine() {
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        int level = CampaignProgress.LevelFor(slot.xp);
        var sb = new System.Text.StringBuilder();
        sb.Append("CASE FILE ").Append(CampaignSave.Active + 1).Append("   LV ").Append(level).Append("   XP ");
        sb.Append(level >= CampaignProgress.MaxLevel ? slot.xp + " (max)" : slot.xp + "/" + CampaignProgress.XpAt(level + 1));
        for (int i = 0; i < CampaignProgress.Abilities.Length; i++) {
            CampaignProgress.Ability a = CampaignProgress.Abilities[i];
            sb.Append("\n").Append(level >= a.Level ? a.Name + ": " + a.Blurb : "LV " + a.Level + ": ???");
        }
        return sb.ToString();
    }

    // Slice's and Wallit's arrival lines, one after the other, over the yard.
    void TickSpeech() {
        int cleared = Mathf.Clamp(CampaignSave.Get(CampaignSave.Active).cleared, 1, Story.HubArrival.Length);
        string[] lines = Story.HubArrival[cleared - 1];
        float t = Time.unscaledTime - arrivedAt - 0.8f;
        int index = t < 0f ? -1 : Mathf.FloorToInt(t / 3.6f);
        if (index < 0 || index >= lines.Length) {
            speech.text = "";
            if (index >= lines.Length) {
                ArrivalShown = lines.Length;
            }
            return;
        }
        ArrivalShown = index + 1;
        string raw = lines[index];
        speech.text = (raw[0] == 'W' ? "Wallit" : "Slice") + ": \"" + raw.Substring(2) + "\"";
        speech.color = raw[0] == 'W' ? new Color(1f, 0.82f, 0.38f) : new Color(0.62f, 0.95f, 0.86f);
    }

    // ---------------------------------------------------------------- UI

    void BuildUi() {
        canvasGo = new GameObject("Hub Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();
        RectTransform root = canvasGo.GetComponent<RectTransform>();

        status = scene.Label(root, "", 26, FontStyle.Bold, new Color(0.87f, 0.91f, 1f), 0f, 0f, 200f);
        Corner(status.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(900f, 190f));
        status.alignment = TextAnchor.UpperLeft;
        status.resizeTextForBestFit = false;

        speech = scene.Label(root, "", 34, FontStyle.BoldAndItalic, Color.white, 0f, 0f, 60f);
        Corner(speech.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1500f, 64f));
        prompt = scene.Label(root, "", 32, FontStyle.Bold, new Color(1f, 0.82f, 0.38f), 0f, 0f, 50f);
        Corner(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1100f, 56f));

        column = new GameObject("Board Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        column.SetParent(root, false);
        column.anchorMin = new Vector2(0.5f, 0.5f);
        column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(1000f, 840f);
        var back = column.gameObject.AddComponent<Image>();
        UiSkin.Round(back);
        back.color = new Color(0.07f, 0.06f, 0.09f, 0.96f);

        Text title = scene.Label(column, "CHAPTER BOARD", 56, FontStyle.Bold, new Color(1f, 0.82f, 0.38f), 0f, 16f, 80f);
        title.name = "Board Title";
        int count = CampaignSave.ChapterCount;
        BoardButtons = new Button[count];
        for (int i = 0; i < count; i++) {
            int index = i;
            BoardButtons[i] = scene.MakeButton(column, "Chapter " + (i + 1), 900f, 88f, 0f, 104f + i * 98f, () => PickChapter(index));
        }
        CloseButton = scene.MakeButton(column, "Close", 300f, 88f, 0f, 104f + count * 98f + 6f, CloseBoard);
        column.gameObject.SetActive(false);
        Layout();
    }

    static void Corner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    void Layout() {
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler, 0.5f);
        float scale = 1f;
        if (UiFit.Compact) {
            Vector2 size = UiFit.Size;
            scale = Mathf.Min(1f, (size.x - 40f) / 1000f, (size.y - 40f) / 840f);
        }
        column.localScale = new Vector3(scale, scale, 1f);
    }

    public void OpenBoard() {
        if (BoardOpen) {
            return;
        }
        BoardOpen = true;
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        int open = Mathf.Min(slot.cleared + 1, CampaignSave.ChapterCount);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < BoardButtons.Length; i++) {
            Story.Chapter chapter = Story.Chapters[i];
            bool filed = i < slot.cleared;
            bool next = !filed && i < open;
            string tag = filed ? "filed (replay)" : next ? "next" : "locked";
            string label = i < open ? chapter.Title + "   " + tag : "Chapter " + (i + 1) + ": locked";
            BoardButtons[i].GetComponentInChildren<Text>().text = label;
            BoardButtons[i].interactable = i < open;
            BoardButtons[i].GetComponent<Image>().color = filed ? new Color(0.3f, 0.24f, 0.12f, 1f) : next ? new Color(0.2f, 0.34f, 0.22f, 1f) : new Color(0.13f, 0.14f, 0.2f, 1f);
            sb.Append(label).Append(" | ");
        }
        BoardText = sb.ToString();
        column.gameObject.SetActive(true);
        GameAudio.Play("paper", 0.6f);
        RunAnalytics.Screen("chapter_board");
    }

    public void CloseBoard() {
        if (!BoardOpen) {
            return;
        }
        BoardOpen = false;
        if (column != null) {
            column.gameObject.SetActive(false);
        }
    }

    void PickChapter(int index) {
        CloseBoard();
        GameAudio.Play("stamp", 0.5f);
        scene.ShowCard(index);
    }
}
