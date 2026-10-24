using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The shop between runs. Coins bought here are already in MetaProgress.
public class MainMenu : MonoBehaviour {
    Font font;
    Text coinText;
    Text bestText;
    Text purseText;
    readonly Text[] titles = new Text[MetaProgress.Catalog.Length];
    readonly Text[] abouts = new Text[MetaProgress.Catalog.Length];
    readonly Text[] ranks = new Text[MetaProgress.Catalog.Length];
    readonly Text[] costs = new Text[MetaProgress.Catalog.Length];
    readonly Button[] buttons = new Button[MetaProgress.Catalog.Length];
    readonly Image[] rows = new Image[MetaProgress.Catalog.Length];
    Text celebrate;
    RawImage art;
    Text startText;
    Button lowerStart;
    Button raiseStart;
    int startWave = 1;
    int flashRow = -1;
    float flash;
    float playReadyAt;
    GameObject canvasRoot;
    UiMode builtMode;
    int builtStamp = -1;
    bool builtOnce;
    float builtAt;
    RectTransform copier;
    RectTransform sun;
    RectTransform shafts;
    RectTransform cabinets;
    RectTransform secondHand;
    RectTransform minuteHand;
    RectTransform hourHand;
    Image boardDot;
    Text boardNumber;
    Text quoteWho;
    Text quoteLine;
    readonly MenuStamp[] stamps = new MenuStamp[MetaProgress.Catalog.Length];
    Vector2 parallax;
    int quoteIndex;
    float quoteAt;
    bool cuePaper;
    bool cueThunk;

    // One palette for the whole menu: the records room by daylight. Ink on paper, red and blue stamp pads,
    // manila folders, a clipboard's brown board.
    static readonly Color Ink = new Color(0.17f, 0.14f, 0.12f);
    static readonly Color InkSoft = new Color(0.36f, 0.31f, 0.26f);
    static readonly Color Gold = new Color(0.58f, 0.38f, 0.02f);
    static readonly Color Paper = new Color(0.98f, 0.95f, 0.85f);
    static readonly Color RowPaper = new Color(0.99f, 0.97f, 0.9f, 0.98f);
    static readonly Color Manila = new Color(0.91f, 0.77f, 0.49f);
    static readonly Color Board = new Color(0.4f, 0.29f, 0.19f);
    static readonly Color StampRed = new Color(0.72f, 0.16f, 0.13f);
    static readonly Color StampBlue = new Color(0.14f, 0.3f, 0.6f);
    static readonly Color Leaf = new Color(0.17f, 0.38f, 0.2f);
    static readonly Color Amber = new Color(1f, 0.74f, 0.2f);
    static readonly Color[] Tabs = {
        new Color(0.78f, 0.25f, 0.2f), new Color(0.22f, 0.4f, 0.7f), new Color(0.3f, 0.55f, 0.3f), new Color(0.9f, 0.6f, 0.18f), new Color(0.55f, 0.35f, 0.62f)
    };

    // Who talks at the front desk, in turn. Wallit talks money, Slice is clipped, the Yo Bots chirp.
    static readonly string[,] Quotes = {
        { "Wallit", "Pay up." },
        { "Slice", "Clean." },
        { "Yo Bot", "Uploading victory." },
        { "Wallit", "Coins first. Questions never." },
        { "Slice", "Keep them off the keys." },
        { "Yo Bot", "Please take a number!" }
    };

    // The Play and Settings buttons, for the playtest's touch and settings scenarios.
    public Button PlayButton { get; private set; }
    public Button SettingsButton { get; private set; }

    void Awake() {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playReadyAt = Time.unscaledTime + 0.35f;
        MetaProgress.BeginRun();
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Build();
        Refresh();
        RunAnalytics.Menu();
    }

    void Update() {
        UiFit.Notice();
        bool compact = builtMode != UiMode.Desktop || UiFit.Compact;
        if (canvasRoot != null && builtStamp != UiFit.Stamp && compact) {
            // The phone turned or its toolbar moved: build the menu again for the new shape.
            Destroy(canvasRoot);
            Build();
            Refresh();
        }
        if (art != null) {
            // A slow drift across the key art so the menu breathes.
            float t = Time.unscaledTime;
            float zoom = 0.94f + Mathf.Sin(t * 0.05f) * 0.02f;
            art.uvRect = new Rect((1f - zoom) * 0.5f + Mathf.Sin(t * 0.07f) * 0.02f, (1f - zoom) * 0.5f + Mathf.Cos(t * 0.05f) * 0.01f, zoom, zoom);
        }
        TickAmbient();
        if (flash > 0f && flashRow >= 0 && flashRow < rows.Length && rows[flashRow] != null) {
            flash -= Time.deltaTime;
            rows[flashRow].color = Color.Lerp(RowPaper, new Color(1f, 0.84f, 0.4f, 1f), Mathf.Clamp01(flash));
        }
        if (SettingsPanel.IsOpen || SettingsPanel.ClosedFrame == Time.frameCount) {
            return;
        }
        if (Time.unscaledTime >= playReadyAt && (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.Space))) {
            Play();
        }
    }

    void Play() {
        if (!MetaProgress.TryArmStart(startWave)) {
            Refresh();
            return;
        }
        if (startWave > 1) {
            GameAudio.Play("coin", 0.55f);
        }
        SceneManager.LoadScene(MetaProgress.GameScene);
    }

    void OpenSettings() {
        SettingsPanel.Open(null);
    }

    void LowerStart() {
        startWave = Mathf.Max(1, startWave - 10);
        Refresh();
    }

    void RaiseStart() {
        int next = startWave + 10;
        if (next <= MetaProgress.MaxStartWave() && MetaProgress.Coins >= next) {
            startWave = next;
        }
        Refresh();
    }

    void Build() {
        var canvasGo = new GameObject("Menu Canvas");
        canvasRoot = canvasGo;
        builtMode = UiFit.Mode;
        builtStamp = UiFit.Stamp;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        UiFit.Apply(scaler);
        canvasGo.AddComponent<GraphicRaycaster>();

        BuildRoom(canvasGo.transform, builtMode != UiMode.Desktop);
        Texture2D artTexture = Resources.Load<Texture2D>("UI/MenuArt");
        // The entrance plays once; turning a phone rebuilds the menu without replaying it.
        builtAt = builtOnce ? -10f : Time.unscaledTime;
        builtOnce = true;
        cuePaper = builtAt < 0f;
        cueThunk = builtAt < 0f;
        quoteAt = Time.unscaledTime + 5.5f;

        if (builtMode != UiMode.Desktop) {
            BuildCompact(canvasGo.transform, builtMode == UiMode.Tall, artTexture);
            return;
        }

        // The board over the desk: the next ticket in line is the wave after the best one cleared.
        var board = Panel(canvasGo.transform, new Color(0.11f, 0.1f, 0.09f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(650f, -70f), new Vector2(340f, 176f), 4f);
        for (int w = 0; w < 2; w++) {
            var cable = Image(canvasGo.transform, new Color(0.2f, 0.18f, 0.16f));
            var cableRect = cable.rectTransform;
            cableRect.anchorMin = new Vector2(0f, 1f);
            cableRect.anchorMax = new Vector2(0f, 1f);
            cableRect.pivot = new Vector2(0.5f, 1f);
            cableRect.anchoredPosition = new Vector2(w == 0 ? 706f : 934f, 0f);
            cableRect.sizeDelta = new Vector2(6f, 70f);
            cable.transform.SetSiblingIndex(board.GetSiblingIndex());
        }
        boardDot = Image(board, Amber);
        UiSkin.Disc(boardDot, 14f);
        PlaceTopLeft(boardDot.rectTransform, new Vector2(62f, -23f), new Vector2(14f, 14f));
        Label(board, "NOW SERVING", 24, FontStyle.Bold, new Color(Amber.r, Amber.g, Amber.b, 0.9f), new Vector2(86f, -14f), new Vector2(230f, 32f), TextAnchor.UpperLeft);
        boardNumber = Label(board, "No. 000", 84, FontStyle.Bold, Amber, new Vector2(0f, -42f), new Vector2(340f, 100f), TextAnchor.UpperCenter);
        Label(board, TicketLine(), 20, FontStyle.Normal, new Color(Amber.r, Amber.g, Amber.b, 0.65f), new Vector2(0f, -142f), new Vector2(340f, 28f), TextAnchor.UpperCenter);
        Enter(board, new Vector2(0f, 150f), 0.2f);

        // The key art, pinned up as an exhibit photo.
        var photo = Panel(canvasGo.transform, new Color(0.99f, 0.98f, 0.94f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(820f, -392f), new Vector2(330f, 224f), 6f);
        photo.localRotation = Quaternion.Euler(0f, 0f, -3.5f);
        BuildPhoto(photo, artTexture, new Vector2(306f, 172f), new Vector2(12f, -12f));
        Label(photo, "EXHIBIT A  (after hours)", 20, FontStyle.Italic, InkSoft, new Vector2(12f, -190f), new Vector2(306f, 26f), TextAnchor.UpperCenter);
        var tape = Image(photo, new Color(1f, 0.94f, 0.55f, 0.75f));
        PlaceTopLeft(tape.rectTransform, new Vector2(120f, 10f), new Vector2(92f, 30f));
        tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 7f);
        Enter(photo, new Vector2(0f, 0f), 0.55f, 1.4f);

        var note = Panel(canvasGo.transform, new Color(1f, 0.92f, 0.45f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(820f, -604f), new Vector2(300f, 140f), 4f);
        note.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
        quoteWho = Label(note, "", 22, FontStyle.Bold, InkSoft, new Vector2(18f, -12f), new Vector2(264f, 28f), TextAnchor.UpperLeft);
        quoteLine = Label(note, "", 36, FontStyle.BoldAndItalic, Ink, new Vector2(18f, -42f), new Vector2(264f, 92f), TextAnchor.UpperLeft);
        ShowQuote(0);
        Enter(note, new Vector2(0f, 0f), 0.95f, 0.6f);

        BuildClock(canvasGo.transform, new Vector2(820f, -826f), 150f);

        // The folder: a manila cover with its tab, and the front-desk sheet inside it.
        var left = Panel(canvasGo.transform, Manila, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, -20f), new Vector2(600f, -140f), 8f);
        var tab = Panel(left, Manila, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(40f, -6f), new Vector2(280f, 50f));
        Label(tab, "FORM 7-B  FRONT DESK", 22, FontStyle.Bold, Ink, new Vector2(0f, -12f), new Vector2(280f, 30f), TextAnchor.UpperCenter);
        var sheet = Panel(left, RowPaper, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        sheet.offsetMin = new Vector2(16f, 16f);
        sheet.offsetMax = new Vector2(-16f, -16f);
        Label(sheet, "THE PLACE DEPARTMENT", 24, FontStyle.Bold, StampBlue, new Vector2(36f, -30f), new Vector2(330f, 30f), TextAnchor.UpperLeft);
        Label(sheet, "Breaking Through", 64, FontStyle.Bold, Ink, new Vector2(36f, -58f), new Vector2(520f, 80f), TextAnchor.UpperLeft);
        Rule(sheet, new Vector2(36f, -140f), 120f);
        Label(sheet, "Where things that should not exist are given an address.", 22, FontStyle.Italic, StampRed, new Vector2(36f, -154f), new Vector2(510f, 56f), TextAnchor.UpperLeft);
        Label(sheet, "Protect Slice. Level up on bolts, pick boons, build a run. Coins buy a stronger start.", 22, FontStyle.Normal, InkSoft, new Vector2(36f, -214f), new Vector2(510f, 60f), TextAnchor.UpperLeft);

        coinText = Label(sheet, "", 36, FontStyle.Bold, Gold, new Vector2(36f, -284f), new Vector2(510f, 48f), TextAnchor.UpperLeft);
        bestText = Label(sheet, "", 28, FontStyle.Normal, Leaf, new Vector2(36f, -334f), new Vector2(510f, 40f), TextAnchor.UpperLeft);
        purseText = Label(sheet, "", 22, FontStyle.Normal, InkSoft, new Vector2(36f, -376f), new Vector2(510f, 84f), TextAnchor.UpperLeft);
        celebrate = Label(sheet, "", 28, FontStyle.Bold, StampRed, new Vector2(36f, -462f), new Vector2(510f, 62f), TextAnchor.UpperLeft);

        Label(sheet, "Start later", 22, FontStyle.Bold, Ink, new Vector2(36f, -540f), new Vector2(510f, 28f), TextAnchor.UpperLeft);
        startText = Label(sheet, "", 22, FontStyle.Normal, InkSoft, new Vector2(36f, -568f), new Vector2(510f, 52f), TextAnchor.UpperLeft);
        lowerStart = Button(sheet, "-10", new Vector2(36f, -626f), new Vector2(110f, 46f), 22, LowerStart);
        raiseStart = Button(sheet, "+10", new Vector2(158f, -626f), new Vector2(110f, 46f), 22, RaiseStart);

        var play = Button(sheet, "Play", new Vector2(36f, -694f), new Vector2(280f, 76f), 34, Play, true);
        PlayButton = play;
        SettingsButton = Button(sheet, "Settings", new Vector2(332f, -694f), new Vector2(210f, 76f), 28, OpenSettings);

        Label(sheet, UiFit.Touch ? "Tap Play to start. After a run, you come back here." : "Enter or click Play. After a run, M brings you back here.", 22, FontStyle.Italic, InkSoft, new Vector2(36f, -782f), new Vector2(510f, 56f), TextAnchor.UpperLeft);
        if (!string.IsNullOrEmpty(WaveDirector.LastRun)) {
            Label(sheet, "Last run\n" + WaveDirector.LastRun, 20, FontStyle.Normal, StampBlue, new Vector2(36f, -842f), new Vector2(510f, 100f), TextAnchor.UpperLeft);
        }
        var open = Stamp(sheet, "NOW OPEN", 30, StampRed, new Vector2(466f, -44f), new Vector2(190f, 50f), -9f, 0.85f);
        open.Slam(builtAt < 0f ? -1f : 1.5f, builtAt < 0f);
        Enter(left, new Vector2(-70f, 0f), 0f);
        Enter(play.GetComponent<RectTransform>(), new Vector2(0f, -24f), 0.45f);

        // The clipboard: the upgrades are forms, each with a folder tab, filed with a stamp.
        float rightHeight = 116f + MetaProgress.Catalog.Length * 76f + 84f;
        var right = Panel(canvasGo.transform, Board, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -60f), new Vector2(880f, rightHeight), 8f);
        var clip = Panel(right, new Color(0.72f, 0.72f, 0.74f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(170f, 44f), 3f);
        var clipBar = Panel(clip, new Color(0.45f, 0.45f, 0.48f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(110f, 12f));
        clipBar.name = "Clip Bar";
        var paper = Panel(right, RowPaper, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        paper.offsetMin = new Vector2(22f, 22f);
        paper.offsetMax = new Vector2(-22f, -50f);
        Label(paper, "Upgrades", 42, FontStyle.Bold, Ink, new Vector2(32f, -16f), new Vector2(500f, 56f), TextAnchor.UpperLeft);
        Label(paper, "Requisition forms. Stamp one to file it.", 20, FontStyle.Italic, InkSoft, new Vector2(300f, -34f), new Vector2(500f, 28f), TextAnchor.UpperLeft);
        Rule(paper, new Vector2(34f, -68f), 80f);
        Enter(right, new Vector2(70f, 0f), 0.12f);

        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            float y = -84f - i * 76f;
            var row = Panel(paper, new Color(1f, 0.99f, 0.95f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 6f), new Vector2(-48f, 72f), 2f);
            rows[i] = row.GetComponent<Image>();
            Panel(row, Tabs[i % Tabs.Length], new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(12f, 0f)).name = "Folder Tab";
            UpgradeInfo info = MetaProgress.Catalog[i];
            titles[i] = Label(row, info.Name, 26, FontStyle.Bold, Ink, new Vector2(26f, -8f), new Vector2(520f, 32f), TextAnchor.UpperLeft);
            abouts[i] = Label(row, info.About, 20, FontStyle.Normal, InkSoft, new Vector2(26f, -42f), new Vector2(560f, 36f), TextAnchor.UpperLeft);
            ranks[i] = Label(row, "", 20, FontStyle.Bold, Leaf, new Vector2(340f, -8f), new Vector2(250f, 32f), TextAnchor.UpperRight);
            int captured = i;
            buttons[i] = Button(row, "Buy", new Vector2(-168f, -18f), new Vector2(150f, 52f), 22, () => Buy(captured), false, StampRed);
            var buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-16f, -8f);
            costs[i] = buttons[i].GetComponentInChildren<Text>();
            stamps[i] = Stamp(row, "APPROVED", 28, StampRed, new Vector2(500f, -36f), new Vector2(190f, 46f), -7f, 0f);
        }
    }

    // Phones: two panels, side by side when held sideways and stacked when upright. The left (or top) one keeps
    // the title and Play always in view and scrolls the rest; the upgrades scroll in the other.
    void BuildCompact(Transform canvas, bool tall, Texture2D artTexture) {
        Vector2 size = UiFit.Size;
        float infoHeight = 600f;
        float infoWidth = tall ? size.x - 40f : Mathf.Min(600f, size.x * 0.46f);

        RectTransform info;
        RectTransform shop;
        if (tall) {
            info = Panel(canvas, Manila, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-40f, infoHeight), 6f);
            shop = Panel(canvas, Board, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 6f);
            shop.offsetMin = new Vector2(20f, 20f);
            shop.offsetMax = new Vector2(-20f, -(20f + infoHeight + 16f));
        } else {
            info = Panel(canvas, Manila, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(infoWidth, -40f), 6f);
            shop = Panel(canvas, Board, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 6f);
            shop.offsetMin = new Vector2(20f + infoWidth + 16f, 20f);
            shop.offsetMax = new Vector2(-20f, -20f);
        }

        Label(info, "Breaking Through", 46, FontStyle.Bold, Ink, new Vector2(24f, -16f), new Vector2(infoWidth - 48f, 60f), TextAnchor.UpperLeft);
        Rule(info, new Vector2(26f, -74f), 90f);

        // Pinned to the bottom: Play, and the later-start buttons above it.
        var play = Button(info, "Play", Vector2.zero, new Vector2(280f, 96f), 36, Play, true);
        PinBottom(play, new Vector2(24f, 20f));
        PlayButton = play;
        SettingsButton = Button(info, "Settings", Vector2.zero, new Vector2(Mathf.Min(200f, infoWidth - 340f), 96f), 28, OpenSettings);
        PinBottom(SettingsButton, new Vector2(316f, 20f));
        lowerStart = Button(info, "-10", Vector2.zero, new Vector2(120f, 88f), 26, LowerStart);
        PinBottom(lowerStart, new Vector2(24f, 128f));
        raiseStart = Button(info, "+10", Vector2.zero, new Vector2(120f, 88f), 26, RaiseStart);
        PinBottom(raiseStart, new Vector2(156f, 128f));
        var later = Label(info, "Start later", 24, FontStyle.Bold, Ink, Vector2.zero, new Vector2(infoWidth - 314f, 88f), TextAnchor.MiddleLeft);
        later.rectTransform.anchorMin = Vector2.zero;
        later.rectTransform.anchorMax = Vector2.zero;
        later.rectTransform.pivot = Vector2.zero;
        later.rectTransform.anchoredPosition = new Vector2(292f, 128f);

        RectTransform about = Scroller(info, new Vector2(12f, 228f), new Vector2(-12f, -84f), 10f);
        Flow(about, "NOW SERVING  " + TicketLine(), 28, FontStyle.Bold, StampBlue);
        quoteLine = Flow(about, "", 24, FontStyle.BoldAndItalic, Ink);
        ShowQuote(0);
        FlowPhoto(about, artTexture, 190f);
        Flow(about, "Where things that should not exist are given an address.", 22, FontStyle.Italic, StampRed);
        Flow(about, "Protect Slice. Level up on bolts, pick boons, build a run. Coins buy a stronger start.", 22, FontStyle.Normal, InkSoft);
        coinText = Flow(about, "", 32, FontStyle.Bold, Gold);
        bestText = Flow(about, "", 24, FontStyle.Normal, Leaf);
        celebrate = Flow(about, "", 26, FontStyle.Bold, StampRed);
        startText = Flow(about, "", 22, FontStyle.Normal, InkSoft);
        purseText = Flow(about, "", 22, FontStyle.Normal, InkSoft);
        Flow(about, UiFit.Touch ? "Tap Play to start. After a run, you come back here." : "Enter or click Play. After a run, M brings you back here.", 22, FontStyle.Italic, InkSoft);
        if (!string.IsNullOrEmpty(WaveDirector.LastRun)) {
            Flow(about, "Last run\n" + WaveDirector.LastRun, 20, FontStyle.Normal, StampBlue);
        }

        Label(shop, "Upgrades", 40, FontStyle.Bold, Paper, new Vector2(24f, -16f), new Vector2(600f, 52f), TextAnchor.UpperLeft);
        Rule(shop, new Vector2(26f, -68f), 70f);
        Enter(info, tall ? new Vector2(0f, 60f) : new Vector2(-60f, 0f), 0f);
        Enter(shop, tall ? new Vector2(0f, -60f) : new Vector2(60f, 0f), 0.12f);
        Enter(play.GetComponent<RectTransform>(), new Vector2(0f, -20f), 0.45f);
        RectTransform list = Scroller(shop, new Vector2(12f, 12f), new Vector2(-12f, -76f), 8f);
        float rowHeight = tall ? 170f : 186f;
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            var row = Panel(list, new Color(1f, 0.99f, 0.95f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, rowHeight));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = rowHeight;
            rows[i] = row.GetComponent<Image>();
            Panel(row, Tabs[i % Tabs.Length], new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(14f, 0f)).name = "Folder Tab";
            UpgradeInfo info2 = MetaProgress.Catalog[i];
            titles[i] = Label(row, info2.Name, 28, FontStyle.Bold, Ink, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Column(titles[i].rectTransform, -10f, 36f);
            ranks[i] = Label(row, "", 22, FontStyle.Bold, Leaf, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Column(ranks[i].rectTransform, -48f, 28f);
            abouts[i] = Label(row, info2.About, 21, FontStyle.Normal, InkSoft, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Column(abouts[i].rectTransform, -80f, rowHeight - 90f);
            int captured = i;
            buttons[i] = Button(row, "Buy", Vector2.zero, new Vector2(180f, 88f), 24, () => Buy(captured), false, StampRed);
            var buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-14f, 0f);
            costs[i] = buttons[i].GetComponentInChildren<Text>();
            stamps[i] = Stamp(row, "APPROVED", 28, StampRed, new Vector2((tall ? size.x - 40f : size.x - 60f - infoWidth) * 0.36f, -rowHeight * 0.5f), new Vector2(190f, 46f), -7f, 0f);
        }
    }

    // A row's text column: full width left of the Buy button.
    static void Column(RectTransform rect, float top, float height) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(24f, top - height);
        rect.offsetMax = new Vector2(-208f, top);
    }

    static void PinBottom(Button button, Vector2 position) {
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
    }

    // A text that takes its own height in a scrolling column.
    Text Flow(RectTransform content, string value, int size, FontStyle style, Color color) {
        return Label(content, value, size, style, color, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
    }

    // A vertical scroll view inside a panel, with a scrollbar that is always shown. Returns the content,
    // which stacks its children top down and grows to fit them.
    RectTransform Scroller(Transform parent, Vector2 offsetMin, Vector2 offsetMax, float spacing) {
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(parent, false);
        var scrollRect = (RectTransform)scrollGo.transform;
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = offsetMin;
        scrollRect.offsetMax = offsetMax;
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        var viewGo = new GameObject("Viewport", typeof(RectTransform));
        viewGo.transform.SetParent(scrollGo.transform, false);
        var view = (RectTransform)viewGo.transform;
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = Vector2.zero;
        view.offsetMax = new Vector2(-26f, 0f);
        var catcher = viewGo.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        viewGo.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewGo.transform, false);
        var content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var stack = contentGo.AddComponent<VerticalLayoutGroup>();
        stack.padding = new RectOffset(12, 12, 8, 12);
        stack.spacing = spacing;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var barGo = new GameObject("Scrollbar", typeof(RectTransform));
        barGo.transform.SetParent(scrollGo.transform, false);
        var barRect = (RectTransform)barGo.transform;
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(16f, -8f);
        var track = barGo.AddComponent<Image>();
        UiSkin.Bar(track);
        track.color = new Color(1f, 0.96f, 0.88f, 0.14f);
        var bar = barGo.AddComponent<Scrollbar>();
        var areaGo = new GameObject("Sliding Area", typeof(RectTransform));
        areaGo.transform.SetParent(barGo.transform, false);
        var area = (RectTransform)areaGo.transform;
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = Vector2.zero;
        area.offsetMax = Vector2.zero;
        var handleGo = new GameObject("Handle", typeof(RectTransform));
        handleGo.transform.SetParent(areaGo.transform, false);
        var handle = handleGo.AddComponent<Image>();
        UiSkin.Bar(handle);
        handle.color = new Color(1f, 0.84f, 0.35f, 0.85f);
        handle.rectTransform.offsetMin = Vector2.zero;
        handle.rectTransform.offsetMax = Vector2.zero;
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;

        scroll.viewport = view;
        scroll.content = content;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return content;
    }

    void Buy(int index) {
        UpgradeInfo info = MetaProgress.Catalog[index];
        int cost = MetaProgress.Cost(info.Id);
        if (MetaProgress.TryBuy(info.Id)) {
            RunAnalytics.ShopBuy(info, cost);
            GameAudio.Play("coin", 0.55f);
            flashRow = index;
            flash = 1f;
            if (stamps[index] != null) {
                stamps[index].Slam(0f, false);
            }
            celebrate.text = info.Name + "\n" + MetaProgress.RankLine(info.Id);
        }
        Refresh();
    }

    void Refresh() {
        int maxStart = MetaProgress.MaxStartWave();
        while (startWave > 1 && (startWave > maxStart || MetaProgress.Coins < startWave)) {
            startWave -= 10;
        }
        if (maxStart < 10) {
            startText.text = "Reach wave 10 to buy a later start.";
        } else if (startWave <= 1) {
            startText.text = "This run starts at wave 1. Later starts cost 10 coins per 10 waves.";
        } else {
            startText.text = "This run starts at wave " + startWave + ". Play spends " + startWave + " coins.";
        }
        lowerStart.interactable = startWave > 1;
        raiseStart.interactable = startWave + 10 <= maxStart && MetaProgress.Coins >= startWave + 10;

        coinText.text = MetaProgress.Coins + (MetaProgress.Coins == 1 ? " coin" : " coins");
        bestText.text = MetaProgress.BestWave > 0 ? "Best wave cleared: " + MetaProgress.BestWave : "No wave cleared yet.";
        purseText.text = "Waves pay coins. Bosses and the Golden Yo Bot pay more. Every " + MetaProgress.RangeLabel() + " drops one. Pay up.";

        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            UpgradeInfo info = MetaProgress.Catalog[i];
            int rank = MetaProgress.Rank(info.Id);
            bool maxed = rank >= info.MaxRank;
            ranks[i].text = rank + " / " + info.MaxRank;
            abouts[i].text = info.About + "  " + (rank > 0 ? MetaProgress.RankLine(info.Id) : "");
            costs[i].text = maxed ? "Maxed" : "Buy  " + MetaProgress.Cost(info.Id);
            buttons[i].interactable = MetaProgress.CanBuy(info.Id);
        }
    }

    // A short gold rule under a heading.
    void Rule(Transform parent, Vector2 position, float width) {
        var rule = Image(parent, new Color(StampRed.r, StampRed.g, StampRed.b, 0.85f));
        var rect = rule.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 4f);
    }

    // Slides a panel or button in from an offset and fades it up, after a delay. A scale above 1 drops it in
    // like a pinned photo.
    void Enter(RectTransform rect, Vector2 offset, float delay, float scaleFrom = 1f) {
        if (builtAt < 0f) {
            return;
        }
        rect.gameObject.AddComponent<MenuEntrance>().Setup(offset, delay, scaleFrom);
    }

    string TicketLine() {
        int next = MetaProgress.BestWave + 1;
        return "No. " + next.ToString("000") + (MetaProgress.BestWave > 0 ? "  (wave " + next + " is next)" : "  (wave 1)");
    }

    void ShowQuote(int index) {
        quoteIndex = index % (Quotes.Length / 2);
        string who = Quotes[quoteIndex, 0];
        string line = Quotes[quoteIndex, 1];
        if (quoteWho != null) {
            quoteWho.text = who.ToUpper();
            quoteLine.text = line;
        } else if (quoteLine != null) {
            quoteLine.text = who + ": " + line;
        }
    }

    static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // The key art, in a frame, drifting slowly inside it.
    void BuildPhoto(Transform parent, Texture2D texture, Vector2 size, Vector2 position) {
        if (texture == null) {
            return;
        }
        var artGo = new GameObject("Key Art");
        artGo.transform.SetParent(parent, false);
        art = artGo.AddComponent<RawImage>();
        art.texture = texture;
        art.raycastTarget = false;
        PlaceTopLeft(art.rectTransform, position, size);
        artGo.AddComponent<RectMask2D>();
    }

    // Phones: the photo is one more line in the scrolling desk notes.
    void FlowPhoto(RectTransform content, Texture2D texture, float height) {
        var frame = Panel(content, new Color(0.99f, 0.98f, 0.94f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height), 4f);
        frame.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        if (texture == null) {
            return;
        }
        var artGo = new GameObject("Key Art");
        artGo.transform.SetParent(frame, false);
        art = artGo.AddComponent<RawImage>();
        art.texture = texture;
        art.raycastTarget = false;
        art.rectTransform.anchorMin = Vector2.zero;
        art.rectTransform.anchorMax = Vector2.one;
        art.rectTransform.offsetMin = new Vector2(10f, 34f);
        art.rectTransform.offsetMax = new Vector2(-10f, -10f);
        artGo.AddComponent<RectMask2D>();
        var caption = Label(frame, "EXHIBIT A  (after hours)", 20, FontStyle.Italic, InkSoft, Vector2.zero, new Vector2(0f, 28f), TextAnchor.LowerCenter);
        caption.rectTransform.anchorMin = new Vector2(0f, 0f);
        caption.rectTransform.anchorMax = new Vector2(1f, 0f);
        caption.rectTransform.pivot = new Vector2(0.5f, 0f);
        caption.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        caption.rectTransform.sizeDelta = new Vector2(0f, 30f);
    }

    // The records room by daylight: green wall, sun and window light, a row of filing cabinets that slide a
    // little with the mouse, and the floor. Everything is slow and soft; no small moving specks.
    void BuildRoom(Transform canvas, bool compact) {
        var wall = Image(canvas, new Color(0.78f, 0.83f, 0.73f, 1f));
        Stretch(wall.rectTransform);
        sun = Glow(canvas, new Color(1f, 0.96f, 0.76f, 0.5f), 1800f);
        sun.anchoredPosition = new Vector2(-300f, 380f);

        shafts = new GameObject("Light Shafts", typeof(RectTransform)).GetComponent<RectTransform>();
        shafts.SetParent(canvas, false);
        Stretch(shafts);
        for (int i = 0; i < 3; i++) {
            var shaft = Glow(shafts, new Color(1f, 0.97f, 0.82f, 0.16f), 280f);
            shaft.sizeDelta = new Vector2(300f + i * 90f, 2000f);
            shaft.localRotation = Quaternion.Euler(0f, 0f, 24f);
            shaft.anchoredPosition = new Vector2(-500f + i * 560f, 100f);
        }

        cabinets = new GameObject("Filing Cabinets", typeof(RectTransform)).GetComponent<RectTransform>();
        cabinets.SetParent(canvas, false);
        cabinets.anchorMin = Vector2.zero;
        cabinets.anchorMax = Vector2.one;
        cabinets.offsetMin = new Vector2(-70f, 0f);
        cabinets.offsetMax = new Vector2(70f, 0f);
        int count = compact ? 4 : 8;
        float scale = compact ? 0.7f : 1f;
        float[] heights = { 420f, 540f, 480f, 600f, 450f, 560f, 500f, 430f };
        string[] labels = { "A-F", "G-M", "N-S", "T-Z", "LOST", "FOUND", "UNFILED", "MISC" };
        for (int i = 0; i < count; i++) {
            float h = heights[i % heights.Length] * scale;
            Cabinet(cabinets, (i + 0.5f) / count, 1f / count, h, labels[i % labels.Length], i, compact ? 70f : 150f);
        }

        var floor = Image(canvas, new Color(0.52f, 0.39f, 0.28f, 1f));
        var floorRect = floor.rectTransform;
        floorRect.anchorMin = Vector2.zero;
        floorRect.anchorMax = new Vector2(1f, 0f);
        floorRect.pivot = new Vector2(0.5f, 0f);
        floorRect.anchoredPosition = Vector2.zero;
        floorRect.sizeDelta = new Vector2(0f, compact ? 70f : 150f);
        var skirting = Image(floor.transform, new Color(0.34f, 0.25f, 0.18f, 1f));
        skirting.rectTransform.anchorMin = new Vector2(0f, 1f);
        skirting.rectTransform.anchorMax = Vector2.one;
        skirting.rectTransform.pivot = new Vector2(0.5f, 0f);
        skirting.rectTransform.anchoredPosition = Vector2.zero;
        skirting.rectTransform.sizeDelta = new Vector2(0f, 16f);

        // A faint copier-light band that sweeps down the screen now and then.
        copier = Glow(canvas, new Color(1f, 0.98f, 0.88f, 0.1f), 0f);
        copier.anchorMin = new Vector2(0f, 0.5f);
        copier.anchorMax = new Vector2(1f, 0.5f);
        copier.sizeDelta = new Vector2(0f, 260f);
    }

    void Cabinet(RectTransform parent, float x, float share, float height, string label, int seed, float floorHeight) {
        var body = Image(parent, Color.Lerp(new Color(0.5f, 0.57f, 0.55f), new Color(0.58f, 0.62f, 0.56f), (seed % 3) * 0.5f));
        UiSkin.Round(body, 0.5f);
        var rect = body.rectTransform;
        rect.anchorMin = new Vector2(x, 0f);
        rect.anchorMax = new Vector2(x, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(Mathf.Min(250f, 1960f * share * 0.88f), height);
        rect.anchoredPosition = new Vector2(0f, floorHeight - 12f);
        int drawers = Mathf.Max(3, Mathf.RoundToInt(height / 130f));
        float drawerHeight = (height - 36f) / drawers;
        for (int i = 0; i < drawers; i++) {
            var front = Image(body.transform, new Color(0.62f, 0.69f, 0.65f, 1f));
            UiSkin.Round(front, 0.5f);
            var fr = front.rectTransform;
            fr.anchorMin = new Vector2(0f, 1f);
            fr.anchorMax = new Vector2(1f, 1f);
            fr.pivot = new Vector2(0.5f, 1f);
            fr.offsetMin = new Vector2(12f, -18f - (i + 1) * drawerHeight + 8f);
            fr.offsetMax = new Vector2(-12f, -18f - i * drawerHeight);
            var card = Image(front.transform, new Color(0.97f, 0.94f, 0.84f, 1f));
            card.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            card.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            card.rectTransform.pivot = new Vector2(0.5f, 1f);
            card.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            card.rectTransform.sizeDelta = new Vector2(84f, 26f);
            var tag = Label(card.transform, i == 0 ? label : (i % 2 == 0 ? "FORMS" : "COPIES"), 15, FontStyle.Bold, InkSoft, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            tag.rectTransform.anchorMin = Vector2.zero;
            tag.rectTransform.anchorMax = Vector2.one;
            tag.rectTransform.offsetMin = Vector2.zero;
            tag.rectTransform.offsetMax = Vector2.zero;
            var handle = Image(front.transform, new Color(0.3f, 0.34f, 0.33f, 1f));
            UiSkin.Bar(handle);
            handle.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            handle.rectTransform.pivot = new Vector2(0.5f, 0f);
            handle.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            handle.rectTransform.sizeDelta = new Vector2(70f, 10f);
        }
    }

    // A wall clock whose second hand ticks.
    void BuildClock(Transform canvas, Vector2 center, float diameter) {
        var rim = Image(canvas, new Color(0.2f, 0.17f, 0.15f, 1f));
        UiSkin.Disc(rim, diameter + 14f);
        PlaceCenter(rim.rectTransform, Vector2.zero, new Vector2(diameter + 14f, diameter + 14f));
        rim.rectTransform.anchorMin = new Vector2(0f, 1f);
        rim.rectTransform.anchorMax = new Vector2(0f, 1f);
        rim.rectTransform.anchoredPosition = center;
        var face = Image(rim.transform, new Color(0.98f, 0.96f, 0.9f, 1f));
        UiSkin.Disc(face, diameter);
        PlaceCenter(face.rectTransform, Vector2.zero, new Vector2(diameter, diameter));
        for (int i = 0; i < 4; i++) {
            var tick = Image(face.transform, Ink);
            PlaceCenter(tick.rectTransform, new Vector2(Mathf.Sin(i * Mathf.PI * 0.5f), Mathf.Cos(i * Mathf.PI * 0.5f)) * (diameter * 0.4f), new Vector2(8f, 8f));
        }
        hourHand = Hand(face.transform, diameter * 0.26f, 7f, Ink);
        minuteHand = Hand(face.transform, diameter * 0.38f, 5f, Ink);
        secondHand = Hand(face.transform, diameter * 0.42f, 3f, StampRed);
        Enter(rim.rectTransform, new Vector2(0f, 0f), 1.1f, 1.2f);
    }

    RectTransform Hand(Transform parent, float length, float width, Color color) {
        var hand = Image(parent, color);
        var rect = hand.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, length);
        return rect;
    }

    static void PlaceCenter(RectTransform rect, Vector2 position, Vector2 size) {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // A rubber stamp: bold ink between two rules, slammed down on cue. It rests at the given strength.
    MenuStamp Stamp(Transform parent, string word, int size, Color color, Vector2 center, Vector2 area, float angle, float rest) {
        var go = new GameObject("Stamp " + word, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = area;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        for (int i = 0; i < 2; i++) {
            var rule = Image(go.transform, color);
            rule.rectTransform.anchorMin = new Vector2(0f, i == 0 ? 1f : 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, i == 0 ? 1f : 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, i == 0 ? 1f : 0f);
            rule.rectTransform.anchoredPosition = Vector2.zero;
            rule.rectTransform.sizeDelta = new Vector2(0f, 4f);
        }
        var text = Label(go.transform, word, size, FontStyle.Bold, color, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        var stamp = go.AddComponent<MenuStamp>();
        stamp.Setup(rest);
        return stamp;
    }

    RectTransform Glow(Transform parent, Color color, float size) {
        var glow = Image(parent, color);
        glow.sprite = UiSkin.Glow;
        glow.type = UnityEngine.UI.Image.Type.Sliced;
        var rect = glow.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }

    void TickAmbient() {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - builtAt) / 0.9f));
        if (art != null) {
            art.color = new Color(1f, 1f, 1f, appear);
        }
        if (!cuePaper && t >= builtAt + 0.05f) {
            cuePaper = true;
            MenuSfx.Paper(0.5f);
        }
        if (!cueThunk && t >= builtAt + 1.1f) {
            // The photo lands on the wall.
            cueThunk = true;
            MenuSfx.Thunk(0.35f);
        }

        // Camera drift: the room slides against the desk with the mouse, and breathes on its own.
        Vector2 target = Vector2.zero;
        if (!UiFit.Touch && Screen.width > 0 && Screen.height > 0) {
            Vector3 mouse = PlayInput.MousePosition;
            target = new Vector2(Mathf.Clamp(mouse.x / Screen.width - 0.5f, -0.5f, 0.5f), Mathf.Clamp(mouse.y / Screen.height - 0.5f, -0.5f, 0.5f));
        }
        parallax = Vector2.Lerp(parallax, target, Mathf.Clamp01(dt * 2.5f));
        if (cabinets != null) {
            cabinets.anchoredPosition = new Vector2(-parallax.x * 60f + Mathf.Sin(t * 0.12f) * 22f, -parallax.y * 16f);
        }
        if (shafts != null) {
            shafts.anchoredPosition = new Vector2(-parallax.x * 24f + Mathf.Sin(t * 0.09f) * 40f, 0f);
        }
        if (sun != null) {
            sun.anchoredPosition = new Vector2(-300f + Mathf.Sin(t * 0.07f) * 90f - parallax.x * 14f, 380f + Mathf.Cos(t * 0.06f) * 40f);
        }
        if (copier != null) {
            // Eleven seconds a pass: nine of them travelling top to bottom, two of rest below the screen.
            float f = Mathf.Repeat(t, 11f) / 9f;
            float y = Mathf.Lerp(1.15f, -0.15f, Mathf.Clamp01(f));
            copier.anchorMin = new Vector2(0f, y);
            copier.anchorMax = new Vector2(1f, y);
        }

        if (boardNumber != null) {
            float count = Mathf.Clamp01((t - builtAt - 0.5f) / 0.9f);
            int shown = Mathf.RoundToInt((MetaProgress.BestWave + 1) * Mathf.SmoothStep(0f, 1f, count));
            boardNumber.text = "No. " + shown.ToString("000");
        }
        if (boardDot != null) {
            boardDot.color = new Color(Amber.r, Amber.g, Amber.b, Mathf.Repeat(t, 1.6f) < 1.1f ? 1f : 0.25f);
        }
        if (secondHand != null) {
            System.DateTime now = System.DateTime.Now;
            // The second hand ticks, overshooting a hair before it settles.
            float settle = Mathf.Clamp01(now.Millisecond / 180f);
            secondHand.localRotation = Quaternion.Euler(0f, 0f, -now.Second * 6f - (1f - settle) * 4f * Mathf.Sin(settle * Mathf.PI));
            minuteHand.localRotation = Quaternion.Euler(0f, 0f, -(now.Minute + now.Second / 60f) * 6f);
            hourHand.localRotation = Quaternion.Euler(0f, 0f, -((now.Hour % 12) + now.Minute / 60f) * 30f);
        }
        if ((quoteWho != null || quoteLine != null) && t >= quoteAt) {
            quoteAt = t + 5.5f;
            ShowQuote(quoteIndex + 1);
            if (quoteLine != null && quoteWho != null) {
                quoteLine.rectTransform.parent.gameObject.AddComponent<MenuPop>();
            }
        }
    }

    Text Label(Transform parent, string value, int size, FontStyle style, Color color, Vector2 position, Vector2 sizeDelta, TextAnchor anchor) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }

    Image Image(Transform parent, Color color) {
        var go = new GameObject("Image");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    RectTransform Panel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 sizeDelta, float lift = 0f) {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image);
        image.color = color;
        image.raycastTarget = false;
        if (lift > 0f) {
            // Paper sits above what is behind it: a soft drop shadow.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(lift * 0.6f, -lift);
            shadow.effectColor = new Color(0.1f, 0.08f, 0.04f, 0.28f);
        }
        var rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    Button Button(Transform parent, string label, Vector2 position, Vector2 size, int fontSize, UnityEngine.Events.UnityAction click, bool primary = false, Color? tint = null) {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        // A soft gold halo behind the button: it lights on hover and breathes on the primary one.
        var halo = new GameObject("Halo", typeof(RectTransform)).AddComponent<Image>();
        halo.transform.SetParent(go.transform, false);
        halo.sprite = UiSkin.Glow;
        halo.type = UnityEngine.UI.Image.Type.Sliced;
        halo.raycastTarget = false;
        halo.color = new Color(1f, 0.84f, 0.35f, 0f);
        halo.rectTransform.anchorMin = Vector2.zero;
        halo.rectTransform.anchorMax = Vector2.one;
        halo.rectTransform.offsetMin = new Vector2(-14f, -14f);
        halo.rectTransform.offsetMax = new Vector2(14f, 14f);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, 0.6f);
        image.color = tint ?? (primary ? Leaf : new Color(0.33f, 0.26f, 0.18f, 1f));
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.08f, 0.85f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.42f);
        colors.fadeDuration = 0.08f;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        button.colors = colors;
        button.onClick.AddListener(click);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Paper;
        text.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var fx = go.AddComponent<MenuButtonFx>();
        fx.Setup(button, halo, primary);
        return button;
    }
}

// Hover lifts a button and lights its halo, a press sinks it, and the Play button breathes. Clicks tick.
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler {
    Button button;
    Image halo;
    bool primary;
    bool over;
    bool down;
    float hover;
    float press;
    float kick;

    public void Setup(Button target, Image glow, bool main) {
        button = target;
        halo = glow;
        primary = main;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        over = true;
        if (button != null && button.interactable) {
            GameAudio.Play("hit", 0.06f, 2f);
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        over = false;
        down = false;
    }

    public void OnPointerDown(PointerEventData eventData) {
        down = true;
        if (button != null && button.interactable) {
            // A rubber stamp thunk.
            MenuSfx.Thunk(0.5f);
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (down) {
            // The stamp lifts: a small rebound.
            kick = 1f;
        }
        down = false;
    }

    void Update() {
        bool live = button != null && button.interactable;
        float dt = Time.unscaledDeltaTime;
        hover = Mathf.MoveTowards(hover, over && live ? 1f : 0f, dt * 8f);
        press = Mathf.MoveTowards(press, down && live ? 1f : 0f, dt * 14f);
        float breathe = primary && live ? (Mathf.Sin(Time.unscaledTime * 2.2f) * 0.5f + 0.5f) : 0f;
        kick = Mathf.MoveTowards(kick, 0f, dt * 5f);
        float scale = 1f + hover * 0.045f - press * 0.085f + breathe * 0.012f + Mathf.Sin(kick * Mathf.PI) * 0.04f;
        transform.localScale = new Vector3(scale, scale, 1f);
        if (halo != null) {
            float a = hover * 0.28f + breathe * 0.14f;
            halo.color = new Color(1f, 0.84f, 0.35f, a);
        }
    }
}

// Fades a panel or button up while it slides from an offset to where it was built.
public class MenuEntrance : MonoBehaviour {
    const float Length = 0.55f;
    RectTransform rect;
    CanvasGroup group;
    Vector2 home;
    Vector2 offset;
    float start;
    float scaleStart = 1f;

    public void Setup(Vector2 from, float delay, float scaleFrom = 1f) {
        rect = (RectTransform)transform;
        scaleStart = scaleFrom;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        home = rect.anchoredPosition;
        offset = from;
        start = Time.unscaledTime + delay;
        rect.anchoredPosition = home + offset;
    }

    void Update() {
        float k = Mathf.Clamp01((Time.unscaledTime - start) / Length);
        float e = 1f - (1f - k) * (1f - k) * (1f - k);
        group.alpha = e;
        rect.anchoredPosition = home + offset * (1f - e);
        if (scaleStart != 1f) {
            float sc = Mathf.Lerp(scaleStart, 1f, e);
            rect.localScale = new Vector3(sc, sc, 1f);
        }
        if (k >= 1f) {
            Destroy(group);
            Destroy(this);
        }
    }
}

// A rubber stamp that slams onto the page: it drops in large, lands with a thunk and settles to its resting
// strength after a short hold.
public class MenuStamp : MonoBehaviour {
    const float Drop = 0.14f;
    RectTransform rect;
    CanvasGroup group;
    float rest;
    float hold;
    float startAt = -1f;
    bool landed;

    public void Setup(float restAlpha) {
        rect = (RectTransform)transform;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        rest = restAlpha;
    }

    // A negative delay just shows the stamp at rest, with no slam.
    public void Slam(float delay, bool quiet) {
        if (delay < 0f || quiet) {
            startAt = -1f;
            group.alpha = rest;
            return;
        }
        hold = rest > 0f ? 0f : 1.3f;
        startAt = Time.unscaledTime + delay;
        landed = false;
    }

    void Update() {
        if (startAt < 0f) {
            return;
        }
        float k = (Time.unscaledTime - startAt) / Drop;
        if (k < 0f) {
            group.alpha = 0f;
            return;
        }
        if (k < 1f) {
            float e = k * k;
            float s = Mathf.Lerp(2.4f, 1f, e);
            rect.localScale = new Vector3(s, s, 1f);
            group.alpha = Mathf.Clamp01(k * 2f);
            return;
        }
        if (!landed) {
            landed = true;
            MenuSfx.Thunk(0.55f);
        }
        float since = Time.unscaledTime - startAt - Drop;
        // A hair of squash on landing.
        float squash = 1f + Mathf.Max(0f, 0.08f * (1f - since / 0.12f));
        rect.localScale = new Vector3(squash, squash, 1f);
        group.alpha = Mathf.Lerp(1f, rest, Mathf.Clamp01((since - hold) / 0.6f));
        if (since > hold + 0.65f) {
            rect.localScale = Vector3.one;
            group.alpha = rest;
            startAt = -1f;
        }
    }
}

// The sticky note pops a little each time it changes what it says.
public class MenuPop : MonoBehaviour {
    float start;

    void OnEnable() {
        start = Time.unscaledTime;
    }

    void Update() {
        float k = Mathf.Clamp01((Time.unscaledTime - start) / 0.25f);
        float s = 1f + 0.08f * Mathf.Sin(k * Mathf.PI);
        transform.localScale = new Vector3(s, s, 1f);
        if (k >= 1f) {
            transform.localScale = Vector3.one;
            Destroy(this);
        }
    }
}

// The front desk's sounds: a stamp thunk and a paper shuffle. Gen made the two clips; until they are in
// Resources/Sfx the old effects stand in so nothing goes quiet.
public static class MenuSfx {
    static int stampClip = -1;
    static int paperClip = -1;

    static bool Has(string name, ref int cache) {
        if (cache < 0) {
            cache = Resources.Load<AudioClip>("Sfx/" + name) != null ? 1 : 0;
        }
        return cache == 1;
    }

    public static void Thunk(float volume) {
        if (Has("stamp", ref stampClip)) {
            GameAudio.Play("stamp", volume);
        } else {
            GameAudio.Play("stomp", volume * 0.5f, 1.6f);
        }
    }

    public static void Paper(float volume) {
        if (Has("paper", ref paperClip)) {
            GameAudio.Play("paper", volume);
        }
    }
}
