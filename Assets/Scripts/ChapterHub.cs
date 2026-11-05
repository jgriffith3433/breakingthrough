using UnityEngine;
using UnityEngine.UI;

// The hub between chapters (Docs/Story.md, "The hub"): the circle at rest, small enough to walk across. Wallit walks on the
// stick, Slice stands in the ring with her knife out, and the chapter board waits at the far side. Walk up and press E (or
// Enter) to read it: every chapter the case file has reached is on it, cleared ones can be replayed, the next one is
// marked. Everything is built here in code (flat, collider-free, like the chapter props); ChapterScene owns it and the
// chapter card the board leads to. Wallit's level and abilities (CampaignProgress) show top left.
public class ChapterHub : MonoBehaviour {
    public static ChapterHub Instance { get; private set; }

    // The hub is an office: one room, Slice's ring in the middle, the chapter board on the left wall, a vacant desk on the
    // right (the spot for NPCs later) and a door per chapter along the back wall. A door opens as the file reaches its
    // chapter (cleared + 1); walk through an open one to start that chapter. Locked doors stay shut.
    public static readonly Vector3 BoardPosition = new Vector3(-12.2f, 0f, 3f);
    public static readonly Vector3 DeskPosition = new Vector3(11.5f, 0f, 3f);
    public const float BoardReach = 3.6f;
    public const float RoomHalfWidth = 14.2f;
    public const float RoomBack = 14.4f;
    public const float RoomFront = -9f;
    const float WallZ = 15f;
    const float DoorSpacing = 4.6f;
    const float Speed = 6f;

    // For the playtest bot.
    public Transform Player { get; private set; }
    public bool BoardOpen { get; private set; }
    public bool NearBoard { get; private set; }
    public float Walked { get; private set; }
    public Button[] BoardButtons { get; private set; }
    public Button CloseButton { get; private set; }
    public string StatusText { get { return status != null ? status.text : ""; } }
    public string PromptText { get { return prompt != null ? prompt.text : ""; } }
    public string BoardText { get; private set; } = "";
    public int ArrivalShown { get; private set; }
    // Doors, for the playtest bot: index = chapter. DoorOpen is true once the file has reached the chapter.
    public int DoorCount { get { return CampaignSave.ChapterCount; } }
    public int EnteredDoor { get; private set; } = -1;
    public int NearDoor { get; private set; } = -1;
    public bool[] DoorOpen { get; private set; }
    public static Vector3 DoorPosition(int index) {
        return new Vector3((index - (CampaignSave.ChapterCount - 1) * 0.5f) * DoorSpacing, 0f, WallZ);
    }
    public string DoorText { get; private set; } = "";
    public Vector3 NpcSpot { get { return DeskPosition; } }

    // The lodgers (Story.Lodgers): Tick sits at the desk's chair, the others stand about the room. Chime (the bell) gives the
    // errands. Index = Story.Lodgers row.
    public const int ChimeIndex = 3;
    public const float LodgerReach = 3.2f;
    public const float PickupReach = 1.6f;
    public static readonly Vector3[] LodgerSpots = {
        new Vector3(10f, 0f, 3f), new Vector3(-9f, 0f, 10f), new Vector3(8.5f, 0f, -4.5f), new Vector3(-8.5f, 0f, -5f),
    };
    // Where Chime's lost thing lies, one spot per chapter's errand.
    public static readonly Vector3[] ErrandSpots = {
        new Vector3(4.5f, 0f, -6.5f), new Vector3(-5f, 0f, -7f), new Vector3(-10.5f, 0f, 8f),
        new Vector3(9f, 0f, 10f), new Vector3(6f, 0f, 7f), new Vector3(-6f, 0f, 11.5f),
    };
    public int LodgerCount { get { return Story.Lodgers.Length; } }
    public int NearLodger { get; private set; } = -1;
    public int Talks { get; private set; }
    public int TalkedTo { get; private set; } = -1;
    public string TalkText { get; private set; } = "";
    public int ErrandActive { get; private set; } = -1;
    public bool ErrandCarried { get; private set; }
    public int ErrandPaid { get; private set; } = -1;
    public int ErrandsDone { get { return CountBits(CampaignSave.Get(CampaignSave.Active).errands); } }
    public Vector3 ErrandItemAt { get { return ErrandActive >= 0 ? ErrandSpots[ErrandActive] : Vector3.zero; } }
    // Where a lodger is now: the office moves them about as the file advances (HubLife.Spots).
    public Vector3 LodgerAt(int i) { return HubLife.SpotOf(i, CampaignSave.Get(CampaignSave.Active).cleared); }
    // Optional conversations (HubLife.Chats) and the side errands (favours and loose notices), for the playtest bot and the status line.
    public int ChatsHeard { get { return HubLife.HeardCount(CampaignSave.Get(CampaignSave.Active)); } }
    public int ChatsWaiting { get { return HubLife.PendingCount(CampaignSave.Get(CampaignSave.Active)); } }
    public int SideDone { get { return HubLife.SideCount(CampaignSave.Get(CampaignSave.Active)); } }
    public int PendingChatOf(int lodger) { return HubLife.Pending(CampaignSave.Get(CampaignSave.Active), lodger); }
    public string LastChatId { get; private set; } = "";
    public int FavourActive { get; private set; } = -1;
    public bool FavourCarried { get; private set; }
    public Vector3 FavourItemAt { get { return FavourActive >= 0 ? HubLife.Sides[FavourActive].Spot : Vector3.zero; } }
    public string DeskName { get { return deskPlate != null ? deskPlate.text : ""; } }
    public int NoticesLying { get; private set; }
    public int NoticePinned { get; private set; } = -1;
    // The loose notices on the floor now (rows of HubLife.Sides) and where they lie.
    public int FirstNotice() {
        for (int i = 0; i < noticeItems.Length; i++) {
            if (noticeItems[i] != null && noticeItems[i].gameObject.activeSelf) {
                return i;
            }
        }
        return -1;
    }
    public string LodgerName(int i) { return Story.Lodgers[i].Name; }
    // What a lodger says now: the line for the chapters the file has cleared.
    public string LodgerLine(int i) {
        int cleared = Mathf.Clamp(CampaignSave.Get(CampaignSave.Active).cleared, 1, Story.Lodgers[i].Lines.Length);
        return Story.Lodgers[i].Lines[cleared - 1];
    }
    // Rebuilds what depends on the file's progress (doors, lodgers, errand) after it changed.
    public void Refresh() {
        DressOffice();
        SetDoors();
        ResetErrand();
        PlaceLodgers();
    }

    ChapterScene scene;
    Camera cam;
    GameObject world;
    GameObject canvasGo;
    Text status;
    Text prompt;
    Text speech;
    Text talk;
    Transform[] lodgerRoots;
    TextMesh[] lodgerMarks;
    TextMesh deskPlate;
    Transform favourItem;
    TextMesh favourLabel;
    Transform[] noticeItems;
    Transform errandItem;
    TextMesh errandLabel;
    float talkUntil;
    GameObject board;
    Animator wallitAnim;
    Transform slice;
    Animator sliceAnim;
    float arrivedAt;
    int lastClip = -1;
    int laidStamp = -1;
    RectTransform column;
    CanvasScaler scaler;
    Transform[] leaves;
    float[] swing;

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
            ChapterMap.Unload();
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
            EnteredDoor = -1;
            CloseBoard();
            DressOffice();
            SetDoors();
            ResetErrand();
            PlaceLodgers();
            TalkText = "";
            talkUntil = 0f;
        }
        if (cam != null) {
            cam.cullingMask = shown ? -1 : 0;
        }
    }

    // The office scene's dressing (OfficeMap), null until its scene has filled in.
    public OfficeMap Office {
        get { return ChapterMap.Current != null ? ChapterMap.Current.Own as OfficeMap : null; }
    }

    void DressOffice() {
        OfficeMap office = Office;
        if (office != null) {
            CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
            office.Dress(slot.cleared, HubLife.SideCount(slot), HubLife.HeardCount(slot));
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

        // The office is its own scene (Assets/Maps/Map_Office.unity), loaded on top and dressed for how far the file has come.
        ChapterMap.Load("office");
        BuildRoom();
        BuildBoard();
        BuildDesk();
        BuildLodgers();
        PlaceLodgers();
        BuildDoors();
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

    // Floor, three walls (the front is open to the camera), the baseboard and Slice's ring on the carpet.
    void BuildRoom() {
        float w = RoomHalfWidth + 0.8f;
        Flat("Floor", PrimitiveType.Cube, new Vector3(0f, -0.06f, 2.5f), new Vector3(w * 2f, 0.05f, 27f), new Color(0.12f, 0.13f, 0.16f));
        Flat("Carpet", PrimitiveType.Cube, new Vector3(0f, -0.04f, 3f), new Vector3(w * 2f - 3f, 0.03f, 17f), new Color(0.1f, 0.15f, 0.17f));
        Flat("Back Wall", PrimitiveType.Cube, new Vector3(0f, 2.6f, WallZ + 0.3f), new Vector3(w * 2f, 5.4f, 0.6f), new Color(0.2f, 0.2f, 0.27f));
        Flat("Left Wall", PrimitiveType.Cube, new Vector3(-w - 0.3f, 2.6f, 2.5f), new Vector3(0.6f, 5.4f, 27.6f), new Color(0.17f, 0.17f, 0.23f));
        Flat("Right Wall", PrimitiveType.Cube, new Vector3(w + 0.3f, 2.6f, 2.5f), new Vector3(0.6f, 5.4f, 27.6f), new Color(0.17f, 0.17f, 0.23f));
        Flat("Baseboard", PrimitiveType.Cube, new Vector3(0f, 0.2f, WallZ - 0.05f), new Vector3(w * 2f, 0.4f, 0.12f), new Color(0.3f, 0.22f, 0.14f));
        Flat("Ring Edge", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(8.6f, 0.03f, 8.6f), new Color(0.85f, 0.66f, 0.34f));
        Flat("Ring", PrimitiveType.Cylinder, new Vector3(0f, -0.01f, 0f), new Vector3(8f, 0.03f, 8f), new Color(0.11f, 0.15f, 0.16f));
    }

    // Tick's desk on the right wall: a chair and a plate.
    void BuildDesk() {
        var desk = new GameObject("Vacant Desk");
        desk.transform.SetParent(world.transform, false);
        desk.transform.position = DeskPosition;
        desk.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        Block(desk.transform, "Top", new Vector3(0f, 1f, 0f), new Vector3(3.4f, 0.12f, 1.4f), new Color(0.33f, 0.22f, 0.13f));
        Block(desk.transform, "Front", new Vector3(0f, 0.5f, -0.55f), new Vector3(3.2f, 1f, 0.1f), new Color(0.26f, 0.17f, 0.1f));
        Block(desk.transform, "Chair", new Vector3(0f, 0.55f, 1.5f), new Vector3(0.9f, 1.1f, 0.9f), new Color(0.15f, 0.15f, 0.2f));
        Block(desk.transform, "Plate", new Vector3(0f, 1.15f, -0.4f), new Vector3(1.4f, 0.22f, 0.06f), new Color(0.85f, 0.66f, 0.34f));
        Label3D(desk.transform, "TICK", new Vector3(0f, 2.2f, 0f), 0.12f, new Color(0.7f, 0.72f, 0.85f));
        deskPlate = desk.transform.Find("Label").GetComponent<TextMesh>();
    }

    static int CountBits(int v) {
        int n = 0;
        while (v != 0) {
            n += v & 1;
            v >>= 1;
        }
        return n;
    }

    static Color Hex(string hex) {
        Color c;
        return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
    }

    // Four lodgers, each a few blocks and a name: Tick the clock, Brim the hat, Spare the half-built Yo Bot, Chime the bell.
    // Built facing the camera, collider-free, like the rest of the office; Brim and Chime hover a little.
    void BuildLodgers() {
        lodgerRoots = new Transform[Story.Lodgers.Length];
        lodgerMarks = new TextMesh[Story.Lodgers.Length];
        for (int i = 0; i < lodgerRoots.Length; i++) {
            Story.Lodger lodger = Story.Lodgers[i];
            var root = new GameObject("Lodger " + lodger.Name).transform;
            root.SetParent(world.transform, false);
            root.position = LodgerAt(i);
            lodgerRoots[i] = root;
            Color tint = Hex(lodger.Colour);
            Color dark = tint * 0.55f;
            dark.a = 1f;
            float top = 2f;
            if (lodger.Id == "tick") {
                Block(root, "Case", new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.5f, 0.45f), new Color(0.3f, 0.22f, 0.16f));
                Block(root, "Face", new Vector3(0f, 1.25f, -0.25f), new Vector3(0.7f, 0.7f, 0.05f), tint);
                Block(root, "Hand", new Vector3(0f, 1.38f, -0.3f), new Vector3(0.06f, 0.36f, 0.04f), dark);
                Block(root, "Hand2", new Vector3(0.1f, 1.25f, -0.3f), new Vector3(0.24f, 0.06f, 0.04f), dark);
                Block(root, "Pendulum", new Vector3(0f, 0.45f, -0.25f), new Vector3(0.08f, 0.5f, 0.04f), new Color(0.85f, 0.66f, 0.34f));
                top = 2.1f;
            } else if (lodger.Id == "brim") {
                Block(root, "Crown", new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 0.7f, 0.7f), tint);
                Block(root, "Brim", new Vector3(0f, 0.72f, 0f), new Vector3(1.5f, 0.08f, 1.5f), tint);
                Block(root, "Band", new Vector3(0f, 0.85f, -0.01f), new Vector3(0.74f, 0.14f, 0.74f), dark);
                top = 1.9f;
            } else if (lodger.Id == "spare") {
                Block(root, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(0.8f, 0.8f, 0.6f), tint);
                Block(root, "Head", new Vector3(0f, 1.4f, 0f), new Vector3(0.7f, 0.5f, 0.55f), tint);
                Block(root, "Eye", new Vector3(0f, 1.44f, -0.29f), new Vector3(0.5f, 0.14f, 0.04f), new Color(0.05f, 0.1f, 0.08f));
                Block(root, "ClawR", new Vector3(0.55f, 0.8f, 0f), new Vector3(0.22f, 0.5f, 0.22f), dark);
                Block(root, "LegL", new Vector3(-0.22f, 0.18f, 0f), new Vector3(0.2f, 0.36f, 0.2f), dark);
                top = 2f;
            } else {
                Block(root, "Bell", new Vector3(0f, 0.95f, 0f), new Vector3(0.95f, 0.95f, 0.95f), tint);
                Block(root, "Lip", new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 0.12f, 1.2f), tint);
                Block(root, "Handle", new Vector3(0f, 1.55f, 0f), new Vector3(0.22f, 0.25f, 0.22f), dark);
                Block(root, "Clapper", new Vector3(0f, 0.4f, 0f), new Vector3(0.24f, 0.24f, 0.24f), dark);
                top = 2.2f;
            }
            Label3D(root, lodger.Name, new Vector3(0f, top, -0.1f), 0.1f, tint);
            // A gold "!" over a lodger with a conversation not yet heard.
            Label3D(root, "!", new Vector3(0f, top + 0.55f, -0.1f), 0.2f, new Color(1f, 0.82f, 0.38f));
            lodgerMarks[i] = root.GetChild(root.childCount - 1).GetComponent<TextMesh>();
        }
        // Chime's lost thing, lit so it reads from across the room; shown only while an errand is out.
        var item = new GameObject("Errand Item").transform;
        item.SetParent(world.transform, false);
        Block(item, "Thing", new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.5f, 0.5f), new Color(1f, 0.82f, 0.38f));
        Label3D(item, "", new Vector3(0f, 1.3f, -0.1f), 0.09f, new Color(1f, 0.9f, 0.55f));
        errandLabel = item.Find("Label").GetComponent<TextMesh>();
        errandItem = item;
        errandItem.gameObject.SetActive(false);
        // A lodger's favour: its own item, and the loose notices (one slip per chapter, lit like the errand item).
        var fav = new GameObject("Favour Item").transform;
        fav.SetParent(world.transform, false);
        Block(fav, "Thing", new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.45f, 0.45f), new Color(0.6f, 0.95f, 0.85f));
        Label3D(fav, "", new Vector3(0f, 1.3f, -0.1f), 0.09f, new Color(0.7f, 1f, 0.9f));
        favourLabel = fav.Find("Label").GetComponent<TextMesh>();
        favourItem = fav;
        favourItem.gameObject.SetActive(false);
        noticeItems = new Transform[HubLife.Sides.Length];
        for (int i = 0; i < noticeItems.Length; i++) {
            if (!HubLife.Sides[i].Notice) {
                continue;
            }
            var slip = new GameObject("Notice " + HubLife.Sides[i].Id).transform;
            slip.SetParent(world.transform, false);
            slip.position = HubLife.Sides[i].Spot;
            Block(slip, "Slip", new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.04f, 0.9f), new Color(0.96f, 0.93f, 0.8f));
            Block(slip, "Stamp", new Vector3(0.1f, 0.11f, 0.1f), new Vector3(0.3f, 0.02f, 0.2f), new Color(0.8f, 0.14f, 0.14f));
            Label3D(slip, HubLife.Sides[i].Thing, new Vector3(0f, 1.1f, -0.1f), 0.08f, new Color(1f, 0.95f, 0.7f));
            noticeItems[i] = slip;
            slip.gameObject.SetActive(false);
        }
    }

    // Puts the lodgers where the file's stage has them, the desk plate on who sits there, the favour and the loose notices out.
    void PlaceLodgers() {
        if (lodgerRoots == null) {
            return;
        }
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        for (int i = 0; i < lodgerRoots.Length; i++) {
            lodgerRoots[i].position = LodgerAt(i);
            lodgerMarks[i].gameObject.SetActive(HubLife.Pending(slot, i) >= 0);
        }
        if (deskPlate != null) {
            deskPlate.text = HubLife.DeskPlate[HubLife.Stage(slot.cleared)];
        }
        FavourActive = -1;
        FavourCarried = false;
        if (favourItem != null) {
            favourItem.gameObject.SetActive(false);
        }
        NoticesLying = 0;
        for (int i = 0; i < noticeItems.Length; i++) {
            if (noticeItems[i] == null) {
                continue;
            }
            bool lying = HubLife.SideOpen(slot, i) && !HubLife.SideDone(slot, i);
            noticeItems[i].gameObject.SetActive(lying);
            NoticesLying += lying ? 1 : 0;
        }
    }

    void ResetErrand() {
        ErrandActive = -1;
        ErrandCarried = false;
        if (errandItem != null) {
            errandItem.gameObject.SetActive(false);
        }
    }

    void TickLodgers() {
        NearLodger = -1;
        float best = LodgerReach;
        for (int i = 0; i < lodgerRoots.Length; i++) {
            Vector3 at = LodgerAt(i);
            float d = (Player.position - at).magnitude;
            if (d <= best) {
                best = d;
                NearLodger = i;
            }
            string id = Story.Lodgers[i].Id;
            if (id == "brim" || id == "chime") {
                lodgerRoots[i].position = at + Vector3.up * (0.12f + Mathf.Sin(Time.unscaledTime * 1.6f + i) * 0.1f);
            }
        }
        if (ErrandActive >= 0 && !ErrandCarried) {
            errandItem.Rotate(0f, 90f * Time.unscaledDeltaTime, 0f);
            if ((Player.position - ErrandSpots[ErrandActive]).magnitude <= PickupReach) {
                ErrandCarried = true;
                errandItem.gameObject.SetActive(false);
                GameAudio.Play("coin", 0.6f, 1.2f);
                RunAnalytics.Screen("hub_errand_pickup");
            }
        }
        TickSide();
        if (talkUntil > 0f && Time.unscaledTime >= talkUntil) {
            TalkText = "";
            talkUntil = 0f;
        }
        talk.text = TalkText;
        if (NearLodger >= 0 && !BoardOpen && !NearBoard && (PlayInput.GetKeyDown(KeyCode.E) || PlayInput.GetKeyDown(KeyCode.JoystickButton0) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter))) {
            TalkTo(NearLodger);
        }
    }

    // E beside a lodger: their line for the chapters cleared. Chime also sends Wallit out for the next lost thing, or pays him
    // for the one he carries.
    void TalkTo(int i) {
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        int cleared = Mathf.Clamp(slot.cleared, 1, Story.Errands.Length);
        string text = LodgerLine(i);
        int chat = HubLife.Pending(slot, i);
        int favour = HubLife.FavourFor(slot, i);
        string who = Story.Lodgers[i].Name;
        if (chat >= 0) {
            // An optional conversation, the first time it is open: the whole exchange on screen, kept in the case file.
            HubLife.Chat c = HubLife.Chats[chat];
            slot.chats |= 1 << chat;
            LastChatId = c.Id;
            CampaignSave.Flush();
            text = null;
            TalkText = HubLife.Spoken(c);
            RunAnalytics.Screen("hub_chat_" + c.Id);
            PlaceLodgers();
            DressOffice();
        } else if (i != ChimeIndex && FavourCarried && FavourActive >= 0 && HubLife.Sides[FavourActive].Giver == i) {
            HubLife.Side s = HubLife.Sides[FavourActive];
            slot.side |= 1 << FavourActive;
            CampaignProgress.Grant(s.Xp);
            text = s.Thanks + " (" + s.Xp + " XP)";
            FavourActive = -1;
            FavourCarried = false;
            favourItem.gameObject.SetActive(false);
            GameAudio.Play("coin", 0.9f, 1.1f);
            RunAnalytics.Screen("hub_favour_paid");
            DressOffice();
        } else if (i != ChimeIndex && FavourActive >= 0 && HubLife.Sides[FavourActive].Giver == i) {
            text = "Still out there: the " + HubLife.Sides[FavourActive].Thing + ".";
        } else if (i != ChimeIndex && FavourActive < 0 && favour >= 0) {
            FavourActive = favour;
            favourItem.position = HubLife.Sides[favour].Spot;
            favourLabel.text = HubLife.Sides[favour].Thing;
            favourItem.gameObject.SetActive(true);
            text = HubLife.Sides[favour].Ask;
            RunAnalytics.Screen("hub_favour_start");
        } else if (i == ChimeIndex) {
            if (ErrandCarried) {
                int e = ErrandActive;
                slot.errands |= 1 << e;
                CampaignProgress.Grant(Story.Errands[e].Xp);
                ErrandPaid = e;
                ErrandActive = -1;
                ErrandCarried = false;
                text = Story.Errands[e].Thanks;
                GameAudio.Play("coin", 0.9f, 1.1f);
                RunAnalytics.Screen("hub_errand_paid");
            } else if (ErrandActive >= 0) {
                text = Story.ChimeWaiting + Story.Errands[ErrandActive].Thing + ".";
            } else {
                int next = -1;
                for (int e = 0; e < cleared && next < 0; e++) {
                    if ((slot.errands & (1 << e)) == 0) {
                        next = e;
                    }
                }
                if (next < 0) {
                    text = Story.ChimeIdle;
                } else {
                    ErrandActive = next;
                    errandItem.position = ErrandSpots[next];
                    errandLabel.text = Story.Errands[next].Thing;
                    errandItem.gameObject.SetActive(true);
                    text = text + " " + Story.Errands[next].Ask;
                    RunAnalytics.Screen("hub_errand_start");
                }
            }
        }
        TalkedTo = i;
        Talks++;
        if (text != null) {
            TalkText = who + ": \"" + text + "\"";
        }
        talk.color = Hex(Story.Lodgers[i].Colour);
        talkUntil = Time.unscaledTime + (text == null ? 16f : 9f);
        GameAudio.Play("paper", 0.5f);
        RunAnalytics.Screen("hub_npc_" + Story.Lodgers[i].Id);
    }

    // The favour's item is picked up by walking over it; a loose notice is pinned the same way (XP, a bit in the file, the wall
    // of the office gains a slip: OfficeMap.Dress).
    void TickSide() {
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        if (FavourActive >= 0 && !FavourCarried) {
            favourItem.Rotate(0f, 90f * Time.unscaledDeltaTime, 0f);
            if ((Player.position - HubLife.Sides[FavourActive].Spot).magnitude <= PickupReach) {
                FavourCarried = true;
                favourItem.gameObject.SetActive(false);
                GameAudio.Play("coin", 0.6f, 1.2f);
                RunAnalytics.Screen("hub_favour_pickup");
            }
        }
        for (int i = 0; i < noticeItems.Length; i++) {
            if (noticeItems[i] == null || !noticeItems[i].gameObject.activeSelf) {
                continue;
            }
            if ((Player.position - HubLife.Sides[i].Spot).magnitude <= PickupReach) {
                HubLife.Side s = HubLife.Sides[i];
                slot.side |= 1 << i;
                CampaignProgress.Grant(s.Xp);
                noticeItems[i].gameObject.SetActive(false);
                NoticesLying--;
                NoticePinned = i;
                TalkText = "Pinned: " + s.Thing + ". " + s.Ask + " (" + s.Xp + " XP)";
                talk.color = new Color(1f, 0.95f, 0.7f);
                talkUntil = Time.unscaledTime + 7f;
                GameAudio.Play("stamp", 0.5f);
                RunAnalytics.Screen("hub_notice_pinned");
                DressOffice();
            }
        }
    }

    // A door per chapter along the back wall: frame, a leaf that swings open when the file has reached it, a lamp (amber filed,
    // green next, dark locked) and the chapter number. Collider-free: the walk clamps Wallit to the room.
    void BuildDoors() {
        int count = CampaignSave.ChapterCount;
        leaves = new Transform[count];
        swing = new float[count];
        DoorOpen = new bool[count];
        for (int i = 0; i < count; i++) {
            var door = new GameObject("Door " + (i + 1));
            door.transform.SetParent(world.transform, false);
            door.transform.position = DoorPosition(i);
            Block(door.transform, "FrameL", new Vector3(-1.05f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 0.4f), new Color(0.35f, 0.25f, 0.15f));
            Block(door.transform, "FrameR", new Vector3(1.05f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 0.4f), new Color(0.35f, 0.25f, 0.15f));
            Block(door.transform, "FrameTop", new Vector3(0f, 3.25f, 0f), new Vector3(2.3f, 0.2f, 0.4f), new Color(0.35f, 0.25f, 0.15f));
            Block(door.transform, "Beyond", new Vector3(0f, 1.6f, 0.2f), new Vector3(1.9f, 3.1f, 0.05f), new Color(0.9f, 0.82f, 0.55f));
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(door.transform, false);
            hinge.localPosition = new Vector3(-0.95f, 0f, -0.05f);
            leaves[i] = hinge;
            Block(hinge, "Leaf", new Vector3(0.95f, 1.55f, 0f), new Vector3(1.9f, 3.1f, 0.12f), new Color(0.22f, 0.16f, 0.2f));
            Block(hinge, "Knob", new Vector3(1.65f, 1.5f, -0.1f), new Vector3(0.14f, 0.14f, 0.1f), new Color(0.85f, 0.66f, 0.34f));
            Block(door.transform, "Lamp", new Vector3(0f, 3.7f, -0.1f), new Vector3(0.4f, 0.22f, 0.1f), new Color(0.2f, 0.2f, 0.25f));
            Label3D(door.transform, (i + 1).ToString(), new Vector3(0f, 4.4f, -0.2f), 0.2f, new Color(1f, 0.82f, 0.38f));
        }
        SetDoors();
    }

    // Opens the doors the file has reached and keeps the rest shut; lamps follow.
    void SetDoors() {
        if (leaves == null) {
            return;
        }
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        int open = Mathf.Min(slot.cleared + 1, CampaignSave.ChapterCount);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < leaves.Length; i++) {
            DoorOpen[i] = i < open;
            Color lamp = i < slot.cleared ? new Color(0.9f, 0.65f, 0.25f) : i < open ? new Color(0.35f, 0.95f, 0.45f) : new Color(0.25f, 0.1f, 0.12f);
            Transform lampPart = leaves[i].parent.Find("Lamp");
            if (lampPart != null) {
                Paint(lampPart.gameObject, lamp);
            }
            sb.Append(i + 1).Append(DoorOpen[i] ? ":open " : ":shut ");
        }
        DoorText = sb.ToString().Trim();
    }

    void TickDoors() {
        NearDoor = -1;
        for (int i = 0; i < leaves.Length; i++) {
            Vector3 d = DoorPosition(i);
            Vector3 p = Player.position;
            bool close = Mathf.Abs(p.x - d.x) < DoorSpacing * 0.5f && p.z > d.z - 5f;
            if (close && NearDoor < 0) {
                NearDoor = i;
            }
            // Swing wide as Wallit nears an open door, or stay ajar once the file has reached it.
            float want = DoorOpen[i] ? (close ? 105f : 70f) : 0f;
            swing[i] = Mathf.MoveTowards(swing[i], want, 160f * Time.unscaledDeltaTime);
            leaves[i].localRotation = Quaternion.Euler(0f, -swing[i], 0f);
        }
        // Through an open door: that chapter's card. A shut one is a wall.
        if (NearDoor >= 0 && DoorOpen[NearDoor] && Player.position.z >= RoomBack - 0.05f && EnteredDoor < 0) {
            EnteredDoor = NearDoor;
            GameAudio.Play("stamp", 0.5f);
            RunAnalytics.Screen("hub_door_" + (NearDoor + 1));
            scene.ShowCard(NearDoor);
        }
    }

    void Block(Transform parent, string name, Vector3 local, Vector3 size, Color color) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = size;
        Paint(go, color);
    }

    void Label3D(Transform parent, string text, Vector3 local, float size, Color color) {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var mesh = go.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = font;
        mesh.text = text;
        mesh.characterSize = size;
        mesh.fontSize = 48;
        mesh.fontStyle = FontStyle.Bold;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    // Two posts and a pinned board of slips, facing the ring. The paper is lit so it reads from across the yard.
    void BuildBoard() {
        board = new GameObject("Chapter Board");
        board.transform.SetParent(world.transform, false);
        board.transform.position = BoardPosition;
        board.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
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
        if (JournalPanel.IsOpen) {
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.J) && CampaignSave.Active >= 0) {
            JournalPanel.Open(CampaignSave.Active);
            return;
        }
        if (MenuNav.Back && Time.frameCount != JournalPanel.ClosedFrame) {
            if (BoardOpen) {
                CloseBoard();
            } else {
                scene.LeaveToMenu();
                return;
            }
        }
        Walk();
        TickDoors();
        if (!Shown) {
            return;
        }
        TickSpeech();
        NearBoard = (Player.position - BoardPosition).magnitude <= BoardReach;
        TickLodgers();
        if (!BoardOpen) {
            prompt.text = NearDoor >= 0 && !DoorOpen[NearDoor] ? "Chapter " + (NearDoor + 1) + ": the door is shut" : NearDoor >= 0 ? "Chapter " + (NearDoor + 1) + ": " + Story.Chapters[NearDoor].Title : NearBoard ? "E: read the chapter board" : NearLodger >= 0 ? "E: talk to " + Story.Lodgers[NearLodger].Name : ErrandCarried ? "Carrying the " + Story.Errands[ErrandActive].Thing + ". Take it to Chime." : FavourCarried ? "Carrying the " + HubLife.Sides[FavourActive].Thing + ". Take it to " + Story.Lodgers[HubLife.Sides[FavourActive].Giver].Name + "." : "WASD: walk. Doors along the back wall. Board on the left, lodgers about the room. J: journal.";
            if (NearBoard && (PlayInput.GetKeyDown(KeyCode.E) || PlayInput.GetKeyDown(KeyCode.JoystickButton0) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter))) {
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
            Vector3 flat = new Vector3(Mathf.Clamp(next.x, -RoomHalfWidth, RoomHalfWidth), 0f, next.z);
            // The back wall is solid except through an open door.
            float limit = RoomBack;
            int at = -1;
            for (int i = 0; i < DoorCount; i++) {
                if (Mathf.Abs(flat.x - DoorPosition(i).x) < 0.9f) {
                    at = i;
                }
            }
            if (at >= 0 && DoorOpen[at]) {
                limit = RoomBack + 0.2f;
            }
            flat.z = Mathf.Clamp(flat.z, RoomFront, limit);
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
        Vector3 want = new Vector3(focus.x * 0.8f, 9f, Mathf.Max(focus.z - 10f, -13f));
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
        sb.Append("\nTALKS ").Append(HubLife.HeardCount(slot)).Append("/").Append(HubLife.Chats.Length).Append(" (").Append(HubLife.PendingCount(slot)).Append(" waiting)   ERRANDS ").Append(HubLife.SideCount(slot)).Append("/").Append(HubLife.Sides.Length);
        sb.Append("\nLOST THINGS ").Append(CountBits(slot.errands)).Append("/").Append(Story.Errands.Length).Append(":");
        for (int e = 0; e < Story.Errands.Length; e++) {
            if ((slot.errands & (1 << e)) != 0) {
                sb.Append(" ").Append(Story.Errands[e].Thing).Append(",");
            }
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
        Corner(status.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(1400f, 300f));
        status.alignment = TextAnchor.UpperLeft;
        status.resizeTextForBestFit = false;

        speech = scene.Label(root, "", 34, FontStyle.BoldAndItalic, Color.white, 0f, 0f, 60f);
        Corner(speech.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1500f, 64f));
        talk = scene.Label(root, "", 30, FontStyle.Bold, Color.white, 0f, 0f, 120f);
        Corner(talk.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(1500f, 120f));
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
        MenuNav.Attach(column.gameObject, 10);
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
