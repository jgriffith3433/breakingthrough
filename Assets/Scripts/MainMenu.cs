using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The front desk between runs, after hours. The key art fills the screen; the run setup sits on the left (on top
// when a phone is upright) and the case file on the right holds the upgrades, the briefing and the last run.
// Nothing scrolls: whatever doesn't fit the case file goes on another page (Prev and Next, the arrow keys, the
// mouse wheel or a swipe). Coins bought here are already in MetaProgress.
public class MainMenu : MonoBehaviour {
    const int TabUpgrades = 0;
    const int TabBriefing = 1;
    const int TabLastRun = 2;
    static readonly string[] TabNames = { "Upgrades", "Briefing", "Last run" };

    // A thing on a case-file page: an upgrade form or a paragraph. Pages are packed from these, top down.
    class Block {
        public RectTransform rect;
        public float height;
        public float top;
        public int page;
    }

    Font font;
    GameObject canvasRoot;
    UiMode builtMode;
    int builtStamp = -1;
    Rect builtSafe;
    float rebuildAt = -1f;
    bool builtOnce;
    float builtAt;
    bool cuePaper;

    // The shapes of this build, in canvas units.
    Vector2 size;
    bool compact;
    float textSmall;
    float textBody;
    float buttonHeight;

    Text coinText;
    Text bestText;
    Text nextText;
    Text startText;
    Text statusText;
    Text quoteText;
    Text pageText;
    string hint = "";
    float statusUntil;
    Button lowerStart;
    Button raiseStart;
    Button prevButton;
    Button nextButton;
    int startWave = 1;
    bool laterShown;
    float playReadyAt;

    readonly Text[] titles = new Text[MetaProgress.Catalog.Length];
    readonly Text[] abouts = new Text[MetaProgress.Catalog.Length];
    readonly Text[] ranks = new Text[MetaProgress.Catalog.Length];
    readonly Text[] costs = new Text[MetaProgress.Catalog.Length];
    readonly Button[] buttons = new Button[MetaProgress.Catalog.Length];
    readonly Image[] rows = new Image[MetaProgress.Catalog.Length];
    readonly RectTransform[] pipBoxes = new RectTransform[MetaProgress.Catalog.Length];
    readonly MenuStamp[] stamps = new MenuStamp[MetaProgress.Catalog.Length];
    int flashRow = -1;
    float flash;

    // The case file: its tabs, the page area and what is on each tab.
    readonly List<Block>[] blocks = { new List<Block>(), new List<Block>(), new List<Block>() };
    readonly Button[] tabButtons = new Button[3];
    RectTransform body;
    float bodyWidth;
    float bodyHeight;
    float rowPad;
    float rowTextWidth;
    float iconPad;
    float titleHeight;
    int tab;
    int page;
    int pageCount = 1;
    float flipReadyAt;

    RawImage art;
    float artFocus = 0.5f;
    float artWide = 1f;
    float artTall = 1f;
    RectTransform moon;
    RectTransform mistLow;
    RectTransform mistHigh;
    int quoteIndex;
    float quoteAt;

    // After hours at the Department: night ink, moonlight, brass fittings, manila folders and red stamp ink.
    static readonly Color Night = new Color(0.03f, 0.04f, 0.07f, 1f);
    static readonly Color CardFill = new Color(0.06f, 0.07f, 0.11f, 0.9f);
    static readonly Color RowFill = new Color(0.12f, 0.13f, 0.19f, 0.96f);
    static readonly Color RowLit = new Color(0.34f, 0.28f, 0.14f, 1f);
    static readonly Color ChipFill = new Color(0.07f, 0.08f, 0.12f, 0.86f);
    static readonly Color Brass = new Color(0.8f, 0.63f, 0.33f, 1f);
    static readonly Color BrassDim = new Color(0.5f, 0.4f, 0.22f, 0.9f);
    static readonly Color Moon = new Color(0.87f, 0.91f, 1f, 1f);
    static readonly Color MoonSoft = new Color(0.66f, 0.72f, 0.84f, 1f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.38f, 1f);
    static readonly Color GoldDeep = new Color(0.9f, 0.52f, 0.12f, 1f);
    static readonly Color Paper = new Color(0.97f, 0.94f, 0.86f, 1f);
    static readonly Color Manila = new Color(0.91f, 0.77f, 0.49f, 1f);
    static readonly Color Ink = new Color(0.14f, 0.11f, 0.09f, 1f);
    static readonly Color StampRed = new Color(0.76f, 0.17f, 0.14f, 1f);
    static readonly Color Glass = new Color(0.45f, 0.95f, 0.84f, 1f);
    static readonly Color[] Tabs = {
        new Color(0.86f, 0.3f, 0.24f), new Color(0.32f, 0.52f, 0.86f), new Color(0.38f, 0.7f, 0.4f), new Color(0.95f, 0.66f, 0.22f), new Color(0.68f, 0.46f, 0.82f)
    };

    // Who talks at the front desk, in turn. Wallit talks money, Slice is clipped, the Yo Bots chirp.
    static readonly string[,] Quotes = {
        { "Wallit", "Pay up." },
        { "Slice", "Clean." },
        { "Yo Bot", "Uploading victory." },
        { "Wallit", "Coins first. Questions never." },
        { "Slice", "Keep them out of the ring." },
        { "Yo Bot", "Please take a number!" }
    };

    // The Play and Settings buttons, for the playtest's touch and settings scenarios.
    public Button PlayButton { get; private set; }
    public Button SettingsButton { get; private set; }
    // The two modes: Campaign opens the case files, Arcade starts today's wave defense (PlayButton is the same button).
    public Button CampaignButton { get; private set; }
    public Button ArcadeButton { get { return PlayButton; } }

    // What the playtest reads: the hint, the start-wave stepper, and which button is the primary one.
    // True once Start ran to its end (menu built, coins and shop filled, menu music asked for): an exception in Start leaves it false.
    public bool Ready { get; private set; }
    public string HintText { get { return statusText != null ? statusText.text : ""; } }
    public bool HintTruncated { get { return statusText != null && statusText.preferredHeight > statusText.rectTransform.rect.height + 1f && statusText.fontSize <= statusText.resizeTextMinSize; } }
    public bool StepperVisible { get { return startText != null && startText.gameObject.activeInHierarchy; } }
    public string StepperText { get { return startText != null ? startText.text : ""; } }
    public bool CampaignIsPrimary { get { return CampaignButton != null && PlayButton != null && CampaignButton.GetComponent<RectTransform>().rect.width > PlayButton.GetComponent<RectTransform>().rect.width && CampaignButton.GetComponent<RectTransform>().anchoredPosition.y <= PlayButton.GetComponent<RectTransform>().anchoredPosition.y; } }

    void Awake() {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playReadyAt = Time.unscaledTime + 0.35f;
        MetaProgress.BeginRun();
    }

    void Start() {
        font = UiSkin.Font(UiSkin.FontRole.Body);
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Build();
        Refresh();
        MusicDirector.Ambience("");
        MusicDirector.Play("theme_wallit");
        RunAnalytics.Menu();
        Ready = true;
    }

    void Update() {
        UiFit.Notice();
        if (canvasRoot != null && (builtStamp != UiFit.Stamp || builtSafe != UnityEngine.Screen.safeArea)) {
            // The phone turned, its toolbar moved or the window was resized: lay the menu out again for the new
            // shape once it settles.
            if (rebuildAt < 0f) {
                rebuildAt = Time.unscaledTime + 0.2f;
            } else if (Time.unscaledTime >= rebuildAt) {
                rebuildAt = -1f;
                Destroy(canvasRoot);
                Build();
                Refresh();
            }
        }
        TickAmbient();
        if (flash > 0f && flashRow >= 0 && flashRow < rows.Length && rows[flashRow] != null) {
            flash -= Time.unscaledDeltaTime;
            rows[flashRow].color = Color.Lerp(RowFill, RowLit, Mathf.Clamp01(flash));
        }
        if (statusText != null && statusUntil > 0f && Time.unscaledTime >= statusUntil) {
            statusUntil = 0f;
            statusText.text = hint;
            statusText.color = MoonSoft;
        }
        if (SettingsPanel.IsOpen || SettingsPanel.ClosedFrame == Time.frameCount || CampaignPanel.IsOpen || CampaignPanel.ClosedFrame == Time.frameCount) {
            return;
        }
        // Gamepad and keyboard for the mode select: X or C opens the case files, Y or S Settings, A or Enter plays.
        if (PlayInput.GetKeyDown(KeyCode.C) || PlayInput.GetKeyDown(KeyCode.JoystickButton2)) {
            OpenCampaign();
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.S) || PlayInput.GetKeyDown(KeyCode.JoystickButton3)) {
            OpenSettings();
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.RightArrow) || PlayInput.GetKeyDown(KeyCode.PageDown)) {
            Flip(1);
        } else if (PlayInput.GetKeyDown(KeyCode.LeftArrow) || PlayInput.GetKeyDown(KeyCode.PageUp)) {
            Flip(-1);
        }
        // The wheel turns pages instead of scrolling.
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.1f && Time.unscaledTime >= flipReadyAt) {
            flipReadyAt = Time.unscaledTime + 0.3f;
            Flip(wheel < 0f ? 1 : -1);
        }
        if (Time.unscaledTime >= playReadyAt && (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.JoystickButton0))) {
            Play();
        }
    }

    void Play() {
        if (!MetaProgress.TryArmStart(startWave)) {
            Refresh();
            return;
        }
        if (startWave > 1) {
            GameAudio.PlayUI("coin", 0.55f);
        }
        SceneManager.LoadScene(MetaProgress.GameScene);
    }

    void OpenCampaign() {
        CampaignPanel.Open();
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

    // ---------------------------------------------------------------- building

    void Build() {
        var canvasGo = new GameObject("Menu Canvas");
        // Under the menu, so whatever reads the menu (the playtest's shop check) finds its words.
        canvasGo.transform.SetParent(transform, false);
        canvasRoot = canvasGo;
        builtMode = UiFit.Mode;
        builtStamp = UiFit.Stamp;
        builtSafe = UnityEngine.Screen.safeArea;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        // On a desktop the menu keys off the height, so a short or wide window still fits it without scrolling.
        UiFit.Apply(scaler, 1f);
        canvasGo.AddComponent<GraphicRaycaster>();

        compact = builtMode != UiMode.Desktop;
        size = CanvasSize();
        textSmall = 20f;
        textBody = compact ? 21f : 22f;
        buttonHeight = compact ? 88f : 64f;
        for (int t = 0; t < blocks.Length; t++) {
            blocks[t].Clear();
        }
        // Pieces only some layouts have: forget the last build's.
        quoteText = null;
        art = null;
        flashRow = -1;

        // The entrance plays once; turning a phone rebuilds the menu without replaying it.
        builtAt = builtOnce ? -10f : Time.unscaledTime;
        builtOnce = true;
        cuePaper = builtAt < 0f;
        quoteAt = Time.unscaledTime + 5.5f;

        Texture2D artTexture = Resources.Load<Texture2D>("UI/MenuArt");
        BuildBackdrop(canvasGo.transform, artTexture);

        // Everything else sits inside the screen's safe area, with a margin.
        float margin = compact ? 20f : 48f;
        Vector4 inset = SafeInsets();
        var desk = Node(canvasGo.transform, "Desk");
        desk.anchorMin = Vector2.zero;
        desk.anchorMax = Vector2.one;
        desk.offsetMin = new Vector2(inset.x + margin, inset.w + margin);
        desk.offsetMax = new Vector2(-(inset.y + margin), -(inset.z + margin));
        float w = size.x - inset.x - inset.y - margin * 2f;
        float h = size.y - inset.z - inset.w - margin * 2f;

        laterShown = MetaProgress.MaxStartWave() >= 10;
        hint = UiFit.Touch ? "Campaign tells the story. Arcade is the wave defense. Swipe the case file to turn pages." : "Campaign (C) is the story; Arcade (Enter) is the wave defense. Arrows turn the pages; Settings is S.";
        if (builtMode == UiMode.Tall) {
            BuildTall(desk, w, h);
        } else {
            BuildSide(desk, w, h);
        }
    }

    // Desktop and phones held sideways: the run setup on the left, the case file on the right.
    void BuildSide(RectTransform desk, float w, float h) {
        bool desktop = !compact;
        float gap = desktop ? 60f : 16f;
        float left = desktop ? Mathf.Clamp(w * 0.36f, 520f, 660f) : Mathf.Clamp(w * 0.4f, 420f, 560f);
        float card = desktop ? Mathf.Clamp(w - left - gap, 640f, 880f) : w - left - gap;
        card = Mathf.Min(card, w - left - gap);

        // Shade behind the left column so its words read over the art.
        var shade = Shade(desk.parent, new Color(Night.r, Night.g, Night.b, 0.82f), new Color(Night.r, Night.g, Night.b, 0f), true);
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = new Vector2(0f, 1f);
        shade.pivot = new Vector2(0f, 0.5f);
        shade.anchoredPosition = Vector2.zero;
        shade.sizeDelta = new Vector2(left + (desktop ? 340f : 120f), 0f);
        shade.SetSiblingIndex(desk.GetSiblingIndex());

        var column = Node(desk, "Run Setup");
        Place(column, 0f, 0f, left, h);
        float y = 0f;
        y = BuildHeader(column, left, desktop ? 330f : (h >= 760f ? 200f : 168f), desktop, y);
        y += desktop ? 26f : 12f;
        float chipHeight = desktop ? 120f : 92f;
        BuildChips(column, 0f, y, left, chipHeight);
        y += chipHeight;

        // From the bottom up: Play and Settings, the later start, the status line.
        float playHeight = desktop ? 170f : 150f;
        float laterHeight = desktop ? 72f : 88f;
        float statusHeight = desktop ? 64f : 52f;
        float bottom = h;
        bottom -= playHeight;
        BuildPlayRow(column, 0f, bottom, left, playHeight);
        if (laterShown) {
            bottom -= (desktop ? 18f : 12f) + laterHeight;
            BuildLater(column, 0f, bottom, left, laterHeight);
        }
        bottom -= (desktop ? 14f : 10f) + statusHeight;
        statusText = Label(column, hint, (int)textSmall, FontStyle.Italic, MoonSoft, 0f, bottom, left, statusHeight, TextAnchor.MiddleLeft);
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Truncate;
        statusText.resizeTextForBestFit = true;
        statusText.resizeTextMinSize = (int)textSmall;
        statusText.resizeTextMaxSize = (int)textBody;

        // The front desk's voices, where there's room for them.
        float spare = bottom - y;
        if (spare >= 64f) {
            float quoteHeight = Mathf.Min(desktop ? 110f : 70f, spare - 24f);
            quoteText = Label(column, "", desktop ? 34 : 26, FontStyle.BoldAndItalic, Moon, 0f, y + (spare - quoteHeight) * 0.5f, left, quoteHeight, TextAnchor.MiddleLeft);
            quoteText.verticalOverflow = VerticalWrapMode.Truncate;
            quoteText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            ShowQuote(quoteIndex);
        }
        Enter(column, new Vector2(-50f, 0f), 0f);

        var file = BuildCaseFile(desk, w - card, 0f, card, h);
        Enter(file, new Vector2(60f, 0f), 0.12f);
    }

    // A phone held upright: the title and numbers on top, the case file in the middle, Play at the bottom.
    void BuildTall(RectTransform desk, float w, float h) {
        var shade = Shade(desk.parent, new Color(Night.r, Night.g, Night.b, 0f), new Color(Night.r, Night.g, Night.b, 0.75f), false);
        shade.anchorMin = new Vector2(0f, 1f);
        shade.anchorMax = Vector2.one;
        shade.pivot = new Vector2(0.5f, 1f);
        shade.anchoredPosition = Vector2.zero;
        shade.sizeDelta = new Vector2(0f, 560f);
        shade.SetSiblingIndex(desk.GetSiblingIndex());

        var top = Node(desk, "Run Setup");
        Place(top, 0f, 0f, w, h);
        float y = BuildHeader(top, w, h >= 1300f ? 230f : 168f, false, 0f);
        y += 14f;
        BuildChips(top, 0f, y, w, 92f);
        y += 92f + 14f;

        float bottom = h;
        bottom -= 150f;
        BuildPlayRow(top, 0f, bottom, w, 150f);
        if (laterShown) {
            bottom -= 12f + 88f;
            BuildLater(top, 0f, bottom, w, 88f);
        }
        bottom -= 8f + 52f;
        statusText = Label(top, hint, (int)textSmall, FontStyle.Italic, MoonSoft, 0f, bottom, w, 52f, TextAnchor.MiddleCenter);
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Truncate;
        statusText.resizeTextForBestFit = true;
        statusText.resizeTextMinSize = (int)textSmall;
        statusText.resizeTextMaxSize = (int)textBody;
        bottom -= 12f;
        Enter(top, new Vector2(0f, 40f), 0f);

        var file = BuildCaseFile(desk, 0f, y, w, Mathf.Max(300f, bottom - y));
        Enter(file, new Vector2(0f, -50f), 0.12f);
    }

    // The department's name, the game's title in gold leaf, and the tagline. Returns where it ends.
    float BuildHeader(RectTransform parent, float w, float height, bool desktop, float y) {
        float kicker = desktop ? 26f : 24f;
        var dept = Label(parent, "THE  PLACE  DEPARTMENT", (int)kicker, FontStyle.Bold, Moon, 0f, y, w, kicker + 8f, TextAnchor.UpperLeft);
        dept.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        var rule = Fill(parent, new Color(StampRed.r, StampRed.g, StampRed.b, 0.95f));
        Place(rule.rectTransform, 0f, y + kicker + 12f, desktop ? 150f : 110f, 4f);
        y += kicker + 22f;

        float tagHeight = desktop ? 64f : 52f;
        float titleBox = Mathf.Max(70f, height - (kicker + 22f) - tagHeight);
        var title = Label(parent, "BREAKING THROUGH", desktop ? 120 : 96, FontStyle.Bold, Color.white, 0f, y, w, titleBox, TextAnchor.UpperLeft);
        title.verticalOverflow = VerticalWrapMode.Truncate;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 36;
        title.resizeTextMaxSize = desktop ? 120 : 96;
        title.lineSpacing = 0.9f;
        var foil = title.gameObject.AddComponent<MenuGradient>();
        foil.top = new Color(1f, 0.93f, 0.62f, 1f);
        foil.bottom = GoldDeep;
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.07f, 0.02f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        var drop = title.gameObject.AddComponent<Shadow>();
        drop.effectColor = new Color(0f, 0f, 0f, 0.65f);
        drop.effectDistance = new Vector2(4f, -6f);
        Sprite logoArt = UiArt.Logo;
        if (logoArt != null) {
            // The generated logo (Docs/UiArt.md) takes the title's box; the text stays (hidden) as the fallback and for readers.
            title.enabled = false;
            var logo = Fill(parent, Color.white);
            UiArt.Show(logo, logoArt);
            logo.rectTransform.anchorMin = new Vector2(0f, 1f);
            logo.rectTransform.anchorMax = new Vector2(0f, 1f);
            logo.rectTransform.pivot = new Vector2(0f, 1f);
            logo.rectTransform.anchoredPosition = new Vector2(0f, -y);
            logo.rectTransform.sizeDelta = new Vector2(w, titleBox);
        }
        y += titleBox;

        var tag = Label(parent, "Where things that should not exist are given an address.", desktop ? 26 : 22, FontStyle.Italic, Gold, 0f, y, w, tagHeight, TextAnchor.UpperLeft);
        tag.verticalOverflow = VerticalWrapMode.Truncate;
        tag.resizeTextForBestFit = true;
        tag.resizeTextMinSize = (int)textSmall;
        tag.resizeTextMaxSize = desktop ? 26 : 22;
        tag.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        y += tagHeight;

        if (desktop) {
            // Stamped beside the department's name, clear of the title.
            var open = Stamp(parent, "NOW OPEN", 30, StampRed, new Vector2(w - 110f, -22f), new Vector2(190f, 50f), -9f, 0.9f);
            open.Slam(builtAt < 0f ? -1f : 1.3f, builtAt < 0f);
        }
        return y;
    }

    // Three counters under the title: coins, the best wave and the ticket now serving.
    void BuildChips(RectTransform parent, float x, float y, float w, float h) {
        float gap = compact ? 10f : 14f;
        float chip = (w - gap * 2f) / 3f;
        coinText = Chip(parent, "COINS", Gold, x, y, chip, h);
        bestText = Chip(parent, "BEST", Glass, x + chip + gap, y, chip, h);
        nextText = Chip(parent, "TICKET", Moon, x + (chip + gap) * 2f, y, chip, h);
        nextText.text = "No. " + (MetaProgress.BestWave + 1).ToString("000");
    }

    Text Chip(RectTransform parent, string caption, Color tint, float x, float y, float w, float h) {
        var chip = Framed(parent, ChipFill, BrassDim, x, y, w, h);
        float captionSize = 20f;
        Label(chip, caption, (int)captionSize, FontStyle.Bold, MoonSoft, 14f, 10f, w - 28f, captionSize + 6f, TextAnchor.UpperLeft);
        var value = Label(chip, "", compact ? 36 : 48, FontStyle.Bold, tint, 14f, captionSize + 14f, w - 28f, h - captionSize - 22f, TextAnchor.MiddleLeft);
        value.verticalOverflow = VerticalWrapMode.Truncate;
        value.horizontalOverflow = HorizontalWrapMode.Wrap;
        value.resizeTextForBestFit = true;
        value.resizeTextMinSize = 22;
        value.resizeTextMaxSize = compact ? 36 : 48;
        return value;
    }

    // The mode select: Campaign (the case files) and Arcade (today's wave defense, the big red stamp), then Settings.
    void BuildPlayRow(RectTransform parent, float x, float y, float w, float h) {
        float gap = compact ? 10f : 14f;
        float bigHeight = Mathf.Round((h - gap) * 0.58f);
        float smallHeight = h - gap - bigHeight;
        float smallY = y + bigHeight + gap;
        float arcadeWidth = Mathf.Round((w - gap) * 0.64f);
        int size = compact ? 30 : 36;
        // Campaign is the front door: the big red stamp on its own row. Arcade and Settings sit under it.
        CampaignButton = MakeButton(parent, "Campaign", x, y, w, bigHeight, size + 4, OpenCampaign, true);
        var play = MakeButton(parent, "Arcade", x, smallY, arcadeWidth, smallHeight, compact ? 28 : 30, Play, false);
        PlayButton = play;
        SettingsButton = MakeButton(parent, "Settings", x + arcadeWidth + gap, smallY, w - arcadeWidth - gap, smallHeight, compact ? 24 : 26, OpenSettings);
        Enter(CampaignButton.GetComponent<RectTransform>(), new Vector2(0f, -24f), 0.4f);
        Enter(play.GetComponent<RectTransform>(), new Vector2(0f, -24f), 0.45f);
    }

    // The later start: -10, +10 and what the run will cost.
    void BuildLater(RectTransform parent, float x, float y, float w, float h) {
        float step = compact ? 110f : 96f;
        lowerStart = MakeButton(parent, "-10", x, y, step, h, compact ? 26 : 24, LowerStart);
        raiseStart = MakeButton(parent, "+10", x + step + 10f, y, step, h, compact ? 26 : 24, RaiseStart);
        float textX = x + step * 2f + 26f;
        startText = Label(parent, "", (int)textBody, FontStyle.Normal, Moon, textX, y, Mathf.Max(60f, w - (textX - x)), h, TextAnchor.MiddleLeft);
        startText.verticalOverflow = VerticalWrapMode.Truncate;
        startText.resizeTextForBestFit = true;
        startText.resizeTextMinSize = (int)textSmall;
        startText.resizeTextMaxSize = (int)textBody;
        startText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
    }

    // The case file: folder tabs along the top, the page in the middle, Prev and Next along the bottom.
    RectTransform BuildCaseFile(RectTransform parent, float x, float y, float w, float h) {
        var file = Framed(parent, CardFill, Brass, x, y, w, h, compact ? 6f : 10f);
        file.name = "Case File";
        float pad = compact ? 14f : 22f;
        float tabHeight = compact ? 88f : 64f;
        float footHeight = compact ? 88f : 60f;

        bool hasLastRun = !string.IsNullOrEmpty(WaveDirector.LastRun);
        int tabCount = hasLastRun ? 3 : 2;
        if (!hasLastRun && tab == TabLastRun) {
            tab = TabUpgrades;
        }
        float tabGap = compact ? 8f : 10f;
        float tabWidth = Mathf.Min(compact ? 260f : 230f, (w - pad * 2f - tabGap * (tabCount - 1)) / tabCount);
        for (int i = 0; i < 3; i++) {
            tabButtons[i] = null;
        }
        for (int i = 0; i < tabCount; i++) {
            int captured = i;
            tabButtons[i] = MakeButton(file, TabNames[i], pad + i * (tabWidth + tabGap), pad, tabWidth, tabHeight, compact ? 24 : 24, () => ShowTab(captured));
        }
        var rule = Fill(file, new Color(Brass.r, Brass.g, Brass.b, 0.45f));
        Place(rule.rectTransform, pad, pad + tabHeight + (compact ? 6f : 10f), w - pad * 2f, 2f);

        float bodyTop = pad + tabHeight + (compact ? 16f : 24f);
        float footTop = h - pad - footHeight;
        bodyWidth = w - pad * 2f;
        bodyHeight = Mathf.Max(120f, footTop - (compact ? 10f : 16f) - bodyTop);
        body = Node(file, "Page");
        Place(body, pad, bodyTop, bodyWidth, bodyHeight);
        // A clear backing, so a swipe anywhere on the page turns it.
        var catcher = body.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        body.gameObject.AddComponent<MenuSwipe>().Setup(Flip);

        float footButton = compact ? 160f : 140f;
        prevButton = MakeButton(file, "Prev", pad, footTop, footButton, footHeight, compact ? 26 : 24, () => Flip(-1));
        nextButton = MakeButton(file, "Next", w - pad - footButton, footTop, footButton, footHeight, compact ? 26 : 24, () => Flip(1));
        pageText = Label(file, "", (int)textBody, FontStyle.Bold, MoonSoft, pad + footButton + 10f, footTop, w - pad * 2f - footButton * 2f - 20f, footHeight, TextAnchor.MiddleCenter);

        BuildUpgrades();
        BuildBriefing();
        if (hasLastRun) {
            BuildLastRun();
        }
        for (int t = 0; t < blocks.Length; t++) {
            Pack(t);
        }
        return file;
    }

    // One requisition form per upgrade: a colored folder edge, the name, rank pips, what the rank does now, what
    // it is, and Buy on the right.
    void BuildUpgrades() {
        rowPad = compact ? 12f : 10f;
        float buyWidth = compact ? 170f : 150f;
        float buyHeight = compact ? 88f : 58f;
        titleHeight = compact ? 32f : 34f;
        // A shop icon (Resources/UiArt/upgrade_<id>.png) takes 64 units on the left; with no art the text keeps its old place.
        bool hasIcons = UiArt.Upgrade(MetaProgress.Catalog[0].Id) != null;
        iconPad = hasIcons ? 64f : 0f;
        rowTextWidth = bodyWidth - 26f - iconPad - buyWidth - rowPad * 2f;
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            UpgradeInfo info = MetaProgress.Catalog[i];
            var row = Node(body, "Form " + info.Name);
            Place(row, 0f, 0f, bodyWidth, 100f);
            var fill = Fill(row, RowFill);
            UiSkin.Round(fill, 0.7f);
            Stretch(fill.rectTransform);
            rows[i] = fill;
            var edge = Fill(row, Tabs[i % Tabs.Length]);
            UiSkin.Round(edge, 0.4f);
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);
            edge.rectTransform.anchoredPosition = Vector2.zero;
            edge.rectTransform.sizeDelta = new Vector2(10f, 0f);

            if (hasIcons) {
                var icon = Fill(row, Color.white);
                UiArt.Show(icon, UiArt.Upgrade(info.Id));
                icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(52f, 52f);
            }
            titles[i] = Label(row, info.Name, compact ? 26 : 28, FontStyle.Bold, Paper, 26f + iconPad, rowPad, rowTextWidth - 70f, titleHeight, TextAnchor.UpperLeft);
            titles[i].verticalOverflow = VerticalWrapMode.Truncate;
            titles[i].resizeTextForBestFit = true;
            titles[i].resizeTextMinSize = 20;
            titles[i].resizeTextMaxSize = compact ? 26 : 28;
            pipBoxes[i] = Node(row, "Rank");
            Place(pipBoxes[i], 26f + iconPad + rowTextWidth - 62f, rowPad + (titleHeight - 16f) * 0.5f, 62f, 16f);
            for (int p = 0; p < info.MaxRank; p++) {
                var pip = Fill(pipBoxes[i], new Color(1f, 1f, 1f, 0.15f));
                UiSkin.Disc(pip, 16f);
                Place(pip.rectTransform, 62f - (info.MaxRank - p) * 21f + 5f, 0f, 16f, 16f);
            }
            ranks[i] = Label(row, "", (int)textSmall, FontStyle.Bold, Glass, 26f + iconPad, 0f, rowTextWidth, 24f, TextAnchor.UpperLeft);
            abouts[i] = Label(row, info.About, (int)textSmall, FontStyle.Normal, MoonSoft, 26f + iconPad, 0f, rowTextWidth, 40f, TextAnchor.UpperLeft);

            int captured = i;
            buttons[i] = MakeButton(row, "Buy", 0f, 0f, buyWidth, buyHeight, compact ? 24 : 22, () => Buy(captured), false, StampRed);
            var buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-rowPad, 0f);
            costs[i] = buttons[i].GetComponentInChildren<Text>();
            stamps[i] = Stamp(row, "APPROVED", compact ? 26 : 28, StampRed, new Vector2(26f + iconPad + rowTextWidth * 0.55f, -50f), new Vector2(190f, 46f), -7f, 0f);
            blocks[TabUpgrades].Add(new Block { rect = row, height = 100f });
        }
    }

    // What the game is, who is in it, how coins work and how to play.
    void BuildBriefing() {
        int heading = compact ? 26 : 30;
        Paragraph(TabBriefing, "The case", heading, FontStyle.Bold, Gold);
        Paragraph(TabBriefing, "Things that should not exist keep turning up in the Hollow. The Place Department files them. Tonight the Yo Bots and Spider Bots are coming for the circle, and you are the one with the bat.", (int)textBody, FontStyle.Normal, Moon);
        Paragraph(TabBriefing, "Protect Slice. Level up on bolts, pick boons, build a run. Coins buy a stronger start.", (int)textBody, FontStyle.Normal, Moon);
        Paragraph(TabBriefing, "On file", heading, FontStyle.Bold, Gold);
        Paragraph(TabBriefing, "Wallit. A wallet with a bat. Talks money: \"Pay up.\"", (int)textBody, FontStyle.Normal, Paper);
        Paragraph(TabBriefing, "Slice. Guard of the circle, scalpel out. Clipped: \"Clean.\"", (int)textBody, FontStyle.Normal, Paper);
        Paragraph(TabBriefing, "Yo Bots and Spider Bots. Small and chirpy, big and leggy: \"Uploading victory.\"", (int)textBody, FontStyle.Normal, Paper);
        Paragraph(TabBriefing, "Coins", heading, FontStyle.Bold, Gold);
        Paragraph(TabBriefing, "Waves pay coins. Bosses and the Golden Yo Bot pay more. Every " + MetaProgress.RangeLabel() + " drops one. Spend them on upgrades, or on a later start once you've reached wave 10.", (int)textBody, FontStyle.Normal, Moon);
        Paragraph(TabBriefing, "How to play", heading, FontStyle.Bold, Gold);
        Paragraph(TabBriefing, UiFit.Touch
            ? "The stick moves Wallit. Swing, Lock and Jump are on the right. Campaign tells the story. Tap Arcade for the wave defense; after a run you come back here."
            : "WASD moves, click swings, hold to wind up a home run, Shift locks on. Enter or Arcade starts the wave defense; after a run, M brings you back here.", (int)textBody, FontStyle.Normal, Moon);
    }

    void BuildLastRun() {
        string last = WaveDirector.LastRun;
        int split = last.IndexOf('\n');
        string headline = split >= 0 ? last.Substring(0, split) : last;
        string boons = split >= 0 ? last.Substring(split + 1).Trim() : "";
        int heading = compact ? 26 : 30;
        Paragraph(TabLastRun, "Last run", heading, FontStyle.Bold, Gold);
        Paragraph(TabLastRun, headline, compact ? 24 : 26, FontStyle.Bold, Paper);
        if (boons.Length > 0) {
            Paragraph(TabLastRun, "Boons held", heading, FontStyle.Bold, Gold);
            Paragraph(TabLastRun, boons, (int)textBody, FontStyle.Normal, Moon);
        }
    }

    // A paragraph on a case-file page. One too tall for a page is split into parts at commas or full stops.
    void Paragraph(int onTab, string value, int fontSize, FontStyle style, Color color) {
        var text = Label(body, value, fontSize, style, color, 0f, 0f, bodyWidth, 10f, TextAnchor.UpperLeft);
        float height = Measure(text, value, bodyWidth);
        if (height > bodyHeight && value.Length > 40) {
            DestroyImmediate(text.gameObject);
            int cut = SplitPoint(value);
            Paragraph(onTab, value.Substring(0, cut).Trim(), fontSize, style, color);
            Paragraph(onTab, value.Substring(cut).Trim(), fontSize, style, color);
            return;
        }
        text.rectTransform.sizeDelta = new Vector2(bodyWidth, height);
        text.verticalOverflow = VerticalWrapMode.Truncate;
        blocks[onTab].Add(new Block { rect = text.rectTransform, height = Mathf.Min(height, bodyHeight) });
    }

    // Where to cut a long paragraph in two: the comma or full stop nearest its middle, else the nearest space.
    static int SplitPoint(string value) {
        int middle = value.Length / 2;
        int best = -1;
        for (int i = 1; i < value.Length - 1; i++) {
            char c = value[i];
            if ((c == ',' || c == '.') && value[i + 1] == ' ' && (best < 0 || Mathf.Abs(i - middle) < Mathf.Abs(best - middle))) {
                best = i + 1;
            }
        }
        if (best < 0) {
            best = value.LastIndexOf(' ', middle);
        }
        return best > 0 ? best : middle;
    }

    // ---------------------------------------------------------------- pages

    // The upgrade forms change height as their rank lines come and go: measure each one again.
    void MeasureRows() {
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            if (titles[i] == null) {
                continue;
            }
            float y = rowPad + titleHeight + 4f;
            string rankLine = ranks[i].text;
            ranks[i].gameObject.SetActive(rankLine.Length > 0);
            if (rankLine.Length > 0) {
                float rh = Measure(ranks[i], rankLine, rowTextWidth);
                Place(ranks[i].rectTransform, 26f + iconPad, y, rowTextWidth, rh);
                y += rh + 2f;
            }
            float ah = Measure(abouts[i], abouts[i].text, rowTextWidth);
            Place(abouts[i].rectTransform, 26f + iconPad, y, rowTextWidth, ah);
            y += ah + rowPad;
            float minimum = buttons[i].GetComponent<RectTransform>().sizeDelta.y + rowPad * 2f;
            float height = Mathf.Min(bodyHeight, Mathf.Max(y, minimum));
            Block block = blocks[TabUpgrades][i];
            block.height = height;
            block.rect.sizeDelta = new Vector2(bodyWidth, height);
            ((RectTransform)stamps[i].transform).anchoredPosition = new Vector2(26f + iconPad + rowTextWidth * 0.55f, -height * 0.5f);
        }
    }

    // Fills pages top down: a block that would run past the bottom starts the next page.
    void Pack(int onTab) {
        float gap = 10f;
        float y = 0f;
        int p = 0;
        foreach (Block block in blocks[onTab]) {
            if (y > 0f && y + block.height > bodyHeight + 0.5f) {
                p++;
                y = 0f;
            }
            block.page = p;
            block.top = y;
            y += block.height + gap;
        }
        if (onTab == tab) {
            pageCount = p + 1;
        }
    }

    int PagesOf(int onTab) {
        List<Block> list = blocks[onTab];
        return list.Count == 0 ? 1 : list[list.Count - 1].page + 1;
    }

    // The Upgrades pages, for the phone playtest's screen audit: how many there are, and a jump to one.
    public int ShopPages { get { return PagesOf(TabUpgrades); } }

    public void ShowShopPage(int index) {
        if (body == null) {
            return;
        }
        tab = TabUpgrades;
        page = Mathf.Clamp(index, 0, PagesOf(TabUpgrades) - 1);
        ShowPage(0);
    }

    void ShowTab(int next) {
        if (next == tab || tabButtons[next] == null) {
            return;
        }
        int dir = next > tab ? 1 : -1;
        tab = next;
        page = 0;
        MenuSfx.Paper(0.35f);
        ShowPage(dir);
    }

    void Flip(int dir) {
        int next = Mathf.Clamp(page + dir, 0, pageCount - 1);
        if (next == page) {
            return;
        }
        page = next;
        MenuSfx.Paper(0.35f);
        ShowPage(dir);
    }

    // Shows the current tab's current page; dir slides it in from that side (0: no slide).
    void ShowPage(int dir) {
        if (body == null) {
            return;
        }
        pageCount = PagesOf(tab);
        page = Mathf.Clamp(page, 0, pageCount - 1);
        for (int t = 0; t < blocks.Length; t++) {
            foreach (Block block in blocks[t]) {
                bool on = t == tab && block.page == page;
                if (block.rect.gameObject.activeSelf != on) {
                    block.rect.gameObject.SetActive(on);
                }
                if (!on) {
                    continue;
                }
                var old = block.rect.GetComponent<MenuEntrance>();
                if (old != null) {
                    DestroyImmediate(old);
                    var group = block.rect.GetComponent<CanvasGroup>();
                    if (group != null) {
                        DestroyImmediate(group);
                    }
                }
                block.rect.anchoredPosition = new Vector2(0f, -block.top);
                if (dir != 0) {
                    block.rect.gameObject.AddComponent<MenuEntrance>().Setup(new Vector2(dir * 46f, 0f), 0f);
                }
            }
        }
        bool paged = pageCount > 1;
        prevButton.gameObject.SetActive(paged);
        nextButton.gameObject.SetActive(paged);
        pageText.gameObject.SetActive(paged);
        prevButton.interactable = page > 0;
        nextButton.interactable = page < pageCount - 1;
        pageText.text = "Page " + (page + 1) + " of " + pageCount;
        for (int i = 0; i < tabButtons.Length; i++) {
            if (tabButtons[i] == null) {
                continue;
            }
            bool active = i == tab;
            var image = tabButtons[i].GetComponent<Image>();
            image.color = active ? Manila : new Color(0.16f, 0.17f, 0.24f, 1f);
            var label = tabButtons[i].GetComponentInChildren<Text>();
            label.color = active ? Ink : MoonSoft;
            label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    // ---------------------------------------------------------------- the shop

    void Buy(int index) {
        UpgradeInfo info = MetaProgress.Catalog[index];
        int cost = MetaProgress.Cost(info.Id);
        if (MetaProgress.TryBuy(info.Id)) {
            RunAnalytics.ShopBuy(info, cost);
            GameAudio.PlayUI("coin", 0.55f);
            flashRow = index;
            flash = 1f;
            if (stamps[index] != null) {
                stamps[index].Slam(0f, false);
            }
            Status("Filed: " + info.Name + ". " + MetaProgress.RankLine(info.Id), Gold);
        }
        Refresh();
        if (tab == TabUpgrades && index < blocks[TabUpgrades].Count && blocks[TabUpgrades][index].page != page) {
            // The form grew onto the next page: follow it.
            page = blocks[TabUpgrades][index].page;
            ShowPage(0);
        }
    }

    void Status(string line, Color color) {
        if (statusText == null) {
            return;
        }
        statusText.text = line;
        statusText.color = color;
        statusUntil = Time.unscaledTime + 3.5f;
        statusText.gameObject.AddComponent<MenuPop>();
    }

    void Refresh() {
        int maxStart = MetaProgress.MaxStartWave();
        while (startWave > 1 && (startWave > maxStart || MetaProgress.Coins < startWave)) {
            startWave -= 10;
        }
        // The stepper is only built once wave 10 has been reached (Build's laterShown); on a fresh save it does not exist.
        if (lowerStart != null && raiseStart != null && startText != null) {
            bool show = maxStart >= 10;
            lowerStart.gameObject.SetActive(show);
            raiseStart.gameObject.SetActive(show);
            startText.gameObject.SetActive(show);
            if (startWave <= 1) {
                startText.text = "Start wave: 1\nFree. Later starts cost 10 coins per 10 waves.";
            } else {
                startText.text = "Start wave: " + startWave + "\nArcade spends " + startWave + " coins.";
            }
            lowerStart.interactable = startWave > 1;
            raiseStart.interactable = startWave + 10 <= maxStart && MetaProgress.Coins >= startWave + 10;
        }

        coinText.text = MetaProgress.Coins.ToString();
        bestText.text = MetaProgress.BestWave > 0 ? MetaProgress.BestWave.ToString() : "None";

        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            UpgradeInfo info = MetaProgress.Catalog[i];
            int rank = MetaProgress.Rank(info.Id);
            bool maxed = rank >= info.MaxRank;
            ranks[i].text = rank > 0 ? MetaProgress.RankLine(info.Id) : "";
            abouts[i].text = info.About;
            costs[i].text = maxed ? "Maxed" : "Buy  " + MetaProgress.Cost(info.Id);
            buttons[i].interactable = MetaProgress.CanBuy(info.Id);
            for (int p = 0; p < pipBoxes[i].childCount; p++) {
                var pip = pipBoxes[i].GetChild(p).GetComponent<Image>();
                pip.color = p < rank ? Gold : new Color(1f, 1f, 1f, 0.15f);
            }
        }
        MeasureRows();
        Pack(TabUpgrades);
        ShowPage(0);
    }

    // ---------------------------------------------------------------- the backdrop

    // The key art fills the screen, cropped to its shape and drifting slowly; night shades top and bottom, a cool
    // moon glow and two slow banks of mist. Nothing small moves.
    void BuildBackdrop(Transform canvas, Texture2D texture) {
        var night = Fill(canvas, Night);
        Stretch(night.rectTransform);
        if (texture != null) {
            var artGo = new GameObject("Key Art", typeof(RectTransform));
            artGo.transform.SetParent(canvas, false);
            art = artGo.AddComponent<RawImage>();
            art.texture = texture;
            art.raycastTarget = false;
            Stretch(art.rectTransform);
            float artAspect = texture.width / (float)Mathf.Max(1, texture.height);
            float aspect = size.x / Mathf.Max(1f, size.y);
            artWide = aspect >= artAspect ? 1f : aspect / artAspect;
            artTall = aspect >= artAspect ? artAspect / aspect : 1f;
            // Upright phones see a slice of it: keep Wallit and Slice in it.
            artFocus = builtMode == UiMode.Tall ? 0.36f : 0.5f;
        }
        moon = Glow(canvas, new Color(0.62f, 0.74f, 1f, 0.12f), Mathf.Max(size.x, size.y) * 0.8f);
        moon.anchorMin = new Vector2(0.4f, 0.85f);
        moon.anchorMax = new Vector2(0.4f, 0.85f);
        mistLow = Mist(canvas, 0.2f, 0.09f);
        mistHigh = Mist(canvas, 0.42f, 0.05f);

        var top = Shade(canvas, new Color(Night.r, Night.g, Night.b, 0f), new Color(Night.r, Night.g, Night.b, 0.7f), false);
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = Vector2.one;
        top.pivot = new Vector2(0.5f, 1f);
        top.anchoredPosition = Vector2.zero;
        top.sizeDelta = new Vector2(0f, size.y * 0.3f);
        var foot = Shade(canvas, new Color(Night.r, Night.g, Night.b, 0.85f), new Color(Night.r, Night.g, Night.b, 0f), false);
        foot.anchorMin = Vector2.zero;
        foot.anchorMax = new Vector2(1f, 0f);
        foot.pivot = new Vector2(0.5f, 0f);
        foot.anchoredPosition = Vector2.zero;
        foot.sizeDelta = new Vector2(0f, size.y * 0.4f);
    }

    RectTransform Mist(Transform canvas, float height, float alpha) {
        var mist = Glow(canvas, new Color(0.78f, 0.84f, 0.95f, alpha), 0f);
        mist.anchorMin = new Vector2(0.5f, height);
        mist.anchorMax = new Vector2(0.5f, height);
        mist.sizeDelta = new Vector2(size.x * 1.6f, size.y * 0.22f);
        return mist;
    }

    // A full-width or full-height gradient between two colors (vertex colored, no texture).
    RectTransform Shade(Transform parent, Color from, Color to, bool horizontal) {
        var go = new GameObject("Shade", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        var ramp = go.AddComponent<MenuGradient>();
        // Bottom (or left) is from, top (or right) is to.
        ramp.horizontal = horizontal;
        ramp.bottom = from;
        ramp.top = to;
        return image.rectTransform;
    }

    void TickAmbient() {
        float t = Time.unscaledTime;
        float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - builtAt) / 0.9f));
        if (!cuePaper && t >= builtAt + 0.05f) {
            cuePaper = true;
            MenuSfx.Paper(0.5f);
        }
        if (art != null) {
            // A slow push and drift across the key art so the menu breathes.
            float zoom = 0.93f + Mathf.Sin(t * 0.05f) * 0.025f;
            float w = artWide * zoom;
            float h = artTall * zoom;
            float cx = Mathf.Clamp(artFocus + Mathf.Sin(t * 0.07f) * 0.025f, w * 0.5f, 1f - w * 0.5f);
            float cy = Mathf.Clamp(0.5f + Mathf.Cos(t * 0.05f) * 0.015f, h * 0.5f, 1f - h * 0.5f);
            art.uvRect = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
            art.color = new Color(1f, 1f, 1f, appear);
        }
        if (moon != null) {
            var image = moon.GetComponent<Image>();
            image.color = new Color(0.62f, 0.74f, 1f, (0.08f + Mathf.Sin(t * 0.4f) * 0.03f) * appear);
        }
        if (mistLow != null) {
            mistLow.anchoredPosition = new Vector2(Mathf.Sin(t * 0.045f) * size.x * 0.12f, 0f);
        }
        if (mistHigh != null) {
            mistHigh.anchoredPosition = new Vector2(Mathf.Sin(t * 0.03f + 2f) * size.x * 0.15f, 0f);
        }
        if (nextText != null && builtAt > 0f && t - builtAt < 1.6f) {
            float count = Mathf.Clamp01((t - builtAt - 0.5f) / 0.9f);
            int shown = Mathf.RoundToInt((MetaProgress.BestWave + 1) * Mathf.SmoothStep(0f, 1f, count));
            nextText.text = "No. " + Mathf.Max(1, shown).ToString("000");
        }
        if (quoteText != null && t >= quoteAt) {
            quoteAt = t + 5.5f;
            ShowQuote(quoteIndex + 1);
            quoteText.gameObject.AddComponent<MenuPop>();
        }
    }

    void ShowQuote(int index) {
        quoteIndex = index % (Quotes.Length / 2);
        if (quoteText != null) {
            quoteText.text = "\"" + Quotes[quoteIndex, 1] + "\"  " + Quotes[quoteIndex, 0];
        }
    }

    // ---------------------------------------------------------------- pieces

    // The canvas in units, as the scaler lays it out: compact modes from UiFit, desktop 1080 tall.
    static Vector2 CanvasSize() {
        if (UiFit.Compact) {
            return UiFit.Size;
        }
        float w = Mathf.Max(1, UnityEngine.Screen.width);
        float h = Mathf.Max(1, UnityEngine.Screen.height);
        return new Vector2(1080f * w / h, 1080f);
    }

    // The screen's notch and rounded corners, in canvas units: left, right, top, bottom.
    Vector4 SafeInsets() {
        float sw = UnityEngine.Screen.width;
        float sh = UnityEngine.Screen.height;
        Rect safe = UnityEngine.Screen.safeArea;
        if (sw <= 0f || sh <= 0f || safe.width <= 0f || safe.height <= 0f) {
            return Vector4.zero;
        }
        return new Vector4(
            Mathf.Max(0f, safe.xMin) / sw * size.x,
            Mathf.Max(0f, sw - safe.xMax) / sw * size.x,
            Mathf.Max(0f, sh - safe.yMax) / sh * size.y,
            Mathf.Max(0f, safe.yMin) / sh * size.y);
    }

    // How tall a text is at a width, in canvas units, with a little room to spare.
    static float Measure(Text text, string value, float width) {
        TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(width, 0f));
        float unit = Mathf.Max(0.0001f, text.pixelsPerUnit);
        float h = text.cachedTextGeneratorForLayout.GetPreferredHeight(value, settings) / unit;
        return Mathf.Ceil(h * 1.04f + 2f);
    }

    // Top-left placement in a parent's units, y counting down.
    static void Place(RectTransform rect, float x, float y, float w, float h) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static RectTransform Node(Transform parent, string name) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Slides a panel or button in from an offset and fades it up, after a delay. Only on the first build.
    void Enter(RectTransform rect, Vector2 offset, float delay, float scaleFrom = 1f) {
        if (builtAt < 0f) {
            return;
        }
        rect.gameObject.AddComponent<MenuEntrance>().Setup(offset, delay, scaleFrom);
    }

    // A panel with a thin edge: the edge color behind, the fill inset over it. Children go on the returned rect.
    RectTransform Framed(Transform parent, Color fill, Color edge, float x, float y, float w, float h, float lift = 0f) {
        var outer = Fill(parent, edge);
        bool art = UiArt.Frame != null && lift > 0f;
        if (art) {
            // The generated frame (Docs/UiArt.md) is the edge: brass-tinted, with the fill inset inside its border.
            UiSkin.Framed(outer, 1f);
            outer.color = Color.Lerp(Color.white, edge, 0.55f);
        } else {
            UiSkin.Round(outer, 0.8f);
        }
        Place(outer.rectTransform, x, y, w, h);
        if (lift > 0f) {
            var shadow = outer.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(lift * 0.5f, -lift);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        }
        var inner = Fill(outer.transform, fill);
        UiSkin.Round(inner, 0.8f);
        Stretch(inner.rectTransform);
        float inset = art ? 14f : 2f;
        inner.rectTransform.offsetMin = new Vector2(inset, inset);
        inner.rectTransform.offsetMax = new Vector2(-inset, -inset);
        return outer.rectTransform;
    }

    Text Label(Transform parent, string value, int fontSize, FontStyle style, Color color, float x, float y, float w, float h, TextAnchor anchor) {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Place(text.rectTransform, x, y, w, h);
        return text;
    }

    Image Fill(Transform parent, Color color) {
        var go = new GameObject("Image", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    RectTransform Glow(Transform parent, Color color, float diameter) {
        // Stretched whole, so a wide glow stays a soft oval, never a band with hard sides.
        var glow = Fill(parent, color);
        glow.sprite = UiSkin.Glow;
        glow.type = Image.Type.Simple;
        var rect = glow.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(diameter, diameter);
        return rect;
    }

    // A rubber stamp: bold ink between two rules, slammed down on cue. It rests at the given strength.
    MenuStamp Stamp(Transform parent, string word, int fontSize, Color color, Vector2 center, Vector2 area, float angle, float rest) {
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
            var rule = Fill(go.transform, color);
            rule.rectTransform.anchorMin = new Vector2(0f, i == 0 ? 1f : 0f);
            rule.rectTransform.anchorMax = new Vector2(1f, i == 0 ? 1f : 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, i == 0 ? 1f : 0f);
            rule.rectTransform.anchoredPosition = Vector2.zero;
            rule.rectTransform.sizeDelta = new Vector2(0f, 4f);
        }
        var text = Label(go.transform, word, fontSize, FontStyle.Bold, color, 0f, 0f, 0f, 0f, TextAnchor.MiddleCenter);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(text.rectTransform);
        var stamp = go.AddComponent<MenuStamp>();
        stamp.Setup(rest);
        return stamp;
    }

    // Primary is the red Play stamp; others are dark with a brass edge, or tinted.
    Button MakeButton(Transform parent, string label, float x, float y, float w, float h, int fontSize, UnityEngine.Events.UnityAction click, bool primary = false, Color? tint = null) {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        // A soft gold halo behind the button: it lights on hover and breathes on the primary one.
        var halo = new GameObject("Halo", typeof(RectTransform)).AddComponent<Image>();
        halo.transform.SetParent(go.transform, false);
        halo.sprite = UiSkin.Glow;
        halo.type = Image.Type.Sliced;
        halo.raycastTarget = false;
        halo.color = new Color(1f, 0.84f, 0.35f, 0f);
        halo.rectTransform.anchorMin = Vector2.zero;
        halo.rectTransform.anchorMax = Vector2.one;
        halo.rectTransform.offsetMin = new Vector2(-16f, -16f);
        halo.rectTransform.offsetMax = new Vector2(16f, 16f);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, primary ? 0.8f : 0.6f);
        image.color = tint ?? (primary ? StampRed : new Color(0.13f, 0.14f, 0.2f, 1f));
        var edge = go.AddComponent<Outline>();
        edge.effectColor = primary ? new Color(1f, 0.72f, 0.4f, 0.75f) : new Color(Brass.r, Brass.g, Brass.b, tint.HasValue ? 0.35f : 0.7f);
        edge.effectDistance = new Vector2(2f, -2f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.1f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;
        // No keyboard focus: Enter plays, it doesn't press whichever button was clicked last.
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(click);
        Place(image.rectTransform, x, y, w, h);

        var text = Label(go.transform, label, fontSize, primary ? FontStyle.Bold : FontStyle.Normal, Paper, 0f, 0f, 0f, 0f, TextAnchor.MiddleCenter);
        text.verticalOverflow = VerticalWrapMode.Truncate;
        Stretch(text.rectTransform);
        if (primary) {
            var lift = text.gameObject.AddComponent<Shadow>();
            lift.effectColor = new Color(0.25f, 0.03f, 0.02f, 0.8f);
            lift.effectDistance = new Vector2(2f, -3f);
        }
        var fx = go.AddComponent<MenuButtonFx>();
        fx.Setup(button, halo, primary);
        return button;
    }
}

// Colors a graphic from one color to another, bottom to top (or left to right). On text each letter gets its
// own sweep, like gold leaf; on a plain image it's one sweep across.
public class MenuGradient : BaseMeshEffect {
    public Color top = Color.white;
    public Color bottom = Color.white;
    public bool horizontal;

    public override void ModifyMesh(VertexHelper vh) {
        if (!IsActive()) {
            return;
        }
        int count = vh.currentVertCount;
        var vertex = new UIVertex();
        for (int start = 0; start + 3 < count; start += 4) {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = start; i < start + 4; i++) {
                vh.PopulateUIVertex(ref vertex, i);
                float at = horizontal ? vertex.position.x : vertex.position.y;
                min = Mathf.Min(min, at);
                max = Mathf.Max(max, at);
            }
            float span = Mathf.Max(0.0001f, max - min);
            for (int i = start; i < start + 4; i++) {
                vh.PopulateUIVertex(ref vertex, i);
                float at = horizontal ? vertex.position.x : vertex.position.y;
                Color shade = Color.Lerp(bottom, top, (at - min) / span);
                vertex.color = (Color32)(shade * (Color)vertex.color);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}

// A swipe across the case file turns its page.
public class MenuSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
    System.Action<int> flip;
    Vector2 from;

    public void Setup(System.Action<int> onFlip) {
        flip = onFlip;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        from = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) {
    }

    public void OnEndDrag(PointerEventData eventData) {
        Vector2 move = eventData.position - from;
        float need = Mathf.Max(40f, UnityEngine.Screen.width * 0.06f);
        if (flip != null && Mathf.Abs(move.x) > need && Mathf.Abs(move.x) > Mathf.Abs(move.y) * 1.2f) {
            flip(move.x < 0f ? 1 : -1);
        }
    }
}

// Hover lifts a button and lights its halo, a press sinks it, and the Play button breathes. Clicks tick.
// The pause, settings, level-up and story screens use it too (no halo), so every button in the game feels the same.
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler {
    // Adds the feel to a button built elsewhere.
    public static void Add(Button target, bool main = false) {
        if (target == null || target.GetComponent<MenuButtonFx>() != null) {
            return;
        }
        target.gameObject.AddComponent<MenuButtonFx>().Setup(target, null, main);
    }

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
            GameAudio.PlayUI("hit", 0.06f, 2f);
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

    // A screen that closes under the pointer gets no exit event, so it would reopen with the button still lifted.
    void OnDisable() {
        over = false;
        down = false;
        hover = 0f;
        press = 0f;
        kick = 0f;
        transform.localScale = Vector3.one;
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
// strength after a short hold. One that rests invisible is switched off between slams, so it never sits over
// the words under it.
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
        if (rest <= 0f) {
            gameObject.SetActive(false);
        }
    }

    // A negative delay just shows the stamp at rest, with no slam.
    public void Slam(float delay, bool quiet) {
        if (rest <= 0f && (delay < 0f || quiet)) {
            return;
        }
        gameObject.SetActive(true);
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
            if (rest <= 0f) {
                gameObject.SetActive(false);
            }
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
            GameAudio.PlayUI("stamp", volume);
        } else {
            GameAudio.PlayUI("stomp", volume * 0.5f, 1.6f);
        }
    }

    public static void Paper(float volume) {
        if (Has("paper", ref paperClip)) {
            GameAudio.PlayUI("paper", volume);
        }
    }
}
