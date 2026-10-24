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
    Image artShade;
    RectTransform drift1;
    RectTransform drift2;
    RectTransform copier;

    // One palette for the whole menu: ledger gold for headings and money, paper cream for text.
    static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
    static readonly Color Paper = new Color(1f, 0.96f, 0.86f);
    static readonly Color Stamp = new Color(0.7f, 0.9f, 1f, 0.9f);

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
            rows[flashRow].color = Color.Lerp(new Color(0.1f, 0.09f, 0.13f, 0.78f), new Color(0.55f, 0.4f, 0.12f, 0.9f), Mathf.Clamp01(flash));
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

        var backdrop = Image(canvasGo.transform, new Color(0.04f, 0.05f, 0.08f, 1f));
        Stretch(backdrop.rectTransform);
        Texture2D artTexture = Resources.Load<Texture2D>("UI/MenuArt");
        if (artTexture != null) {
            var artGo = new GameObject("Key Art");
            artGo.transform.SetParent(canvasGo.transform, false);
            art = artGo.AddComponent<RawImage>();
            art.texture = artTexture;
            art.raycastTarget = false;
            Stretch(art.rectTransform);
            artShade = Image(canvasGo.transform, new Color(0.02f, 0.03f, 0.06f, 0.25f));
            Stretch(artShade.rectTransform);
        }
        BuildAmbient(canvasGo.transform);
        // The entrance plays once; turning a phone rebuilds the menu without replaying it.
        builtAt = builtOnce ? -10f : Time.unscaledTime;
        builtOnce = true;

        if (builtMode != UiMode.Desktop) {
            BuildCompact(canvasGo.transform, builtMode == UiMode.Tall);
            return;
        }

        var left = Panel(canvasGo.transform, new Color(0.03f, 0.04f, 0.08f, 0.7f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(620f, -120f));
        Label(left, "THE PLACE DEPARTMENT", 24, FontStyle.Bold, Gold, new Vector2(36f, -30f), new Vector2(540f, 30f), TextAnchor.UpperLeft);
        Label(left, "Breaking Through", 64, FontStyle.Bold, Paper, new Vector2(36f, -58f), new Vector2(540f, 80f), TextAnchor.UpperLeft);
        Rule(left, new Vector2(36f, -140f), 120f);
        Label(left, "Where things that should not exist are given an address.", 22, FontStyle.Italic, Gold, new Vector2(36f, -154f), new Vector2(540f, 56f), TextAnchor.UpperLeft);
        Label(left, "Protect Slice. Level up on bolts, pick boons, build a run. Coins buy a stronger start.", 22, FontStyle.Normal, new Color(1f, 0.9f, 0.72f, 0.9f), new Vector2(36f, -214f), new Vector2(540f, 60f), TextAnchor.UpperLeft);

        coinText = Label(left, "", 36, FontStyle.Bold, Gold, new Vector2(36f, -284f), new Vector2(540f, 48f), TextAnchor.UpperLeft);
        bestText = Label(left, "", 28, FontStyle.Normal, new Color(0.85f, 0.95f, 0.8f), new Vector2(36f, -334f), new Vector2(540f, 40f), TextAnchor.UpperLeft);
        purseText = Label(left, "", 22, FontStyle.Normal, new Color(1f, 0.96f, 0.88f, 0.88f), new Vector2(36f, -376f), new Vector2(540f, 84f), TextAnchor.UpperLeft);
        celebrate = Label(left, "", 28, FontStyle.Bold, Gold, new Vector2(36f, -462f), new Vector2(540f, 62f), TextAnchor.UpperLeft);

        Label(left, "Start later", 22, FontStyle.Bold, new Color(1f, 0.94f, 0.8f), new Vector2(36f, -540f), new Vector2(540f, 28f), TextAnchor.UpperLeft);
        startText = Label(left, "", 22, FontStyle.Normal, new Color(1f, 0.96f, 0.88f, 0.9f), new Vector2(36f, -568f), new Vector2(540f, 52f), TextAnchor.UpperLeft);
        lowerStart = Button(left, "-10", new Vector2(36f, -626f), new Vector2(110f, 46f), 22, LowerStart);
        raiseStart = Button(left, "+10", new Vector2(158f, -626f), new Vector2(110f, 46f), 22, RaiseStart);

        var play = Button(left, "Play", new Vector2(36f, -694f), new Vector2(280f, 76f), 34, Play, true);
        PlayButton = play;
        SettingsButton = Button(left, "Settings", new Vector2(332f, -694f), new Vector2(220f, 76f), 28, OpenSettings);

        Label(left, UiFit.Touch ? "Tap Play to start. After a run, you come back here." : "Enter or click Play. After a run, M brings you back here.", 22, FontStyle.Italic, new Color(1f, 0.96f, 0.88f, 0.7f), new Vector2(36f, -782f), new Vector2(540f, 56f), TextAnchor.UpperLeft);
        if (!string.IsNullOrEmpty(WaveDirector.LastRun)) {
            Label(left, "Last run\n" + WaveDirector.LastRun, 20, FontStyle.Normal, Stamp, new Vector2(36f, -842f), new Vector2(540f, 100f), TextAnchor.UpperLeft);
        }
        Enter(left, new Vector2(-70f, 0f), 0f);
        Enter(play.GetComponent<RectTransform>(), new Vector2(0f, -24f), 0.45f);

        float rightHeight = 116f + MetaProgress.Catalog.Length * 76f;
        var right = Panel(canvasGo.transform, new Color(0.04f, 0.04f, 0.07f, 0.72f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -60f), new Vector2(900f, rightHeight));
        Label(right, "Upgrades", 42, FontStyle.Bold, new Color(1f, 0.94f, 0.8f), new Vector2(32f, -24f), new Vector2(900f, 56f), TextAnchor.UpperLeft);
        Rule(right, new Vector2(34f, -76f), 80f);
        Enter(right, new Vector2(70f, 0f), 0.12f);

        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            float y = -90f - i * 76f;
            var row = Panel(right, new Color(0.1f, 0.09f, 0.13f, 0.78f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 6f), new Vector2(-48f, 72f));
            rows[i] = row.GetComponent<Image>();
            UpgradeInfo info = MetaProgress.Catalog[i];
            titles[i] = Label(row, info.Name, 26, FontStyle.Bold, new Color(1f, 0.95f, 0.84f), new Vector2(18f, -8f), new Vector2(520f, 32f), TextAnchor.UpperLeft);
            abouts[i] = Label(row, info.About, 20, FontStyle.Normal, new Color(1f, 0.92f, 0.8f, 0.8f), new Vector2(18f, -42f), new Vector2(560f, 36f), TextAnchor.UpperLeft);
            ranks[i] = Label(row, "", 20, FontStyle.Normal, new Color(0.8f, 0.92f, 0.7f), new Vector2(410f, -8f), new Vector2(250f, 32f), TextAnchor.UpperRight);
            int captured = i;
            buttons[i] = Button(row, "Buy", new Vector2(-168f, -18f), new Vector2(150f, 52f), 22, () => Buy(captured));
            var buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-16f, -8f);
            costs[i] = buttons[i].GetComponentInChildren<Text>();
        }
    }

    // Phones: two panels, side by side when held sideways and stacked when upright. The left (or top) one keeps
    // the title and Play always in view and scrolls the rest; the upgrades scroll in the other.
    void BuildCompact(Transform canvas, bool tall) {
        Vector2 size = UiFit.Size;
        float infoHeight = 600f;
        float infoWidth = tall ? size.x - 40f : Mathf.Min(600f, size.x * 0.46f);

        RectTransform info;
        RectTransform shop;
        if (tall) {
            info = Panel(canvas, new Color(0.03f, 0.04f, 0.08f, 0.72f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(-40f, infoHeight));
            shop = Panel(canvas, new Color(0.04f, 0.04f, 0.07f, 0.76f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            shop.offsetMin = new Vector2(20f, 20f);
            shop.offsetMax = new Vector2(-20f, -(20f + infoHeight + 16f));
        } else {
            info = Panel(canvas, new Color(0.03f, 0.04f, 0.08f, 0.72f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(infoWidth, -40f));
            shop = Panel(canvas, new Color(0.04f, 0.04f, 0.07f, 0.76f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            shop.offsetMin = new Vector2(20f + infoWidth + 16f, 20f);
            shop.offsetMax = new Vector2(-20f, -20f);
        }

        Label(info, "Breaking Through", 46, FontStyle.Bold, Paper, new Vector2(24f, -16f), new Vector2(infoWidth - 48f, 60f), TextAnchor.UpperLeft);
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
        var later = Label(info, "Start later", 24, FontStyle.Bold, new Color(1f, 0.94f, 0.8f), Vector2.zero, new Vector2(infoWidth - 314f, 88f), TextAnchor.MiddleLeft);
        later.rectTransform.anchorMin = Vector2.zero;
        later.rectTransform.anchorMax = Vector2.zero;
        later.rectTransform.pivot = Vector2.zero;
        later.rectTransform.anchoredPosition = new Vector2(292f, 128f);

        RectTransform about = Scroller(info, new Vector2(12f, 228f), new Vector2(-12f, -84f), 10f);
        Flow(about, "Where things that should not exist are given an address.", 22, FontStyle.Italic, Gold);
        Flow(about, "Protect Slice. Level up on bolts, pick boons, build a run. Coins buy a stronger start.", 22, FontStyle.Normal, new Color(1f, 0.9f, 0.72f, 0.9f));
        coinText = Flow(about, "", 32, FontStyle.Bold, Gold);
        bestText = Flow(about, "", 24, FontStyle.Normal, new Color(0.85f, 0.95f, 0.8f));
        celebrate = Flow(about, "", 26, FontStyle.Bold, Gold);
        startText = Flow(about, "", 22, FontStyle.Normal, new Color(1f, 0.96f, 0.88f, 0.9f));
        purseText = Flow(about, "", 22, FontStyle.Normal, new Color(1f, 0.96f, 0.88f, 0.88f));
        Flow(about, UiFit.Touch ? "Tap Play to start. After a run, you come back here." : "Enter or click Play. After a run, M brings you back here.", 22, FontStyle.Italic, new Color(1f, 0.96f, 0.88f, 0.7f));
        if (!string.IsNullOrEmpty(WaveDirector.LastRun)) {
            Flow(about, "Last run\n" + WaveDirector.LastRun, 20, FontStyle.Normal, Stamp);
        }

        Label(shop, "Upgrades", 40, FontStyle.Bold, new Color(1f, 0.94f, 0.8f), new Vector2(24f, -16f), new Vector2(600f, 52f), TextAnchor.UpperLeft);
        Rule(shop, new Vector2(26f, -68f), 70f);
        Enter(info, tall ? new Vector2(0f, 60f) : new Vector2(-60f, 0f), 0f);
        Enter(shop, tall ? new Vector2(0f, -60f) : new Vector2(60f, 0f), 0.12f);
        Enter(play.GetComponent<RectTransform>(), new Vector2(0f, -20f), 0.45f);
        RectTransform list = Scroller(shop, new Vector2(12f, 12f), new Vector2(-12f, -76f), 8f);
        float rowHeight = tall ? 170f : 186f;
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            var row = Panel(list, new Color(0.1f, 0.09f, 0.13f, 0.78f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, rowHeight));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = rowHeight;
            rows[i] = row.GetComponent<Image>();
            UpgradeInfo info2 = MetaProgress.Catalog[i];
            titles[i] = Label(row, info2.Name, 28, FontStyle.Bold, new Color(1f, 0.95f, 0.84f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Column(titles[i].rectTransform, -10f, 36f);
            ranks[i] = Label(row, "", 22, FontStyle.Normal, new Color(0.8f, 0.92f, 0.7f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Column(ranks[i].rectTransform, -48f, 28f);
            abouts[i] = Label(row, info2.About, 21, FontStyle.Normal, new Color(1f, 0.92f, 0.8f, 0.8f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Column(abouts[i].rectTransform, -80f, rowHeight - 90f);
            int captured = i;
            buttons[i] = Button(row, "Buy", Vector2.zero, new Vector2(180f, 88f), 24, () => Buy(captured));
            var buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-14f, 0f);
            costs[i] = buttons[i].GetComponentInChildren<Text>();
        }
    }

    // A row's text column: full width left of the Buy button.
    static void Column(RectTransform rect, float top, float height) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(18f, top - height);
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
        var rule = Image(parent, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
        var rect = rule.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 4f);
    }

    // Slides a panel or button in from an offset and fades it up, after a delay.
    void Enter(RectTransform rect, Vector2 offset, float delay) {
        if (builtAt < 0f) {
            return;
        }
        rect.gameObject.AddComponent<MenuEntrance>().Setup(offset, delay);
    }

    // Two huge soft lights that wander behind the panels and a faint copier-light band that sweeps down the
    // screen now and then. All of it is slow, wide and low in contrast; no small moving specks.
    void BuildAmbient(Transform canvas) {
        drift1 = Glow(canvas, new Color(1f, 0.72f, 0.3f, 0.1f), 1300f);
        drift2 = Glow(canvas, new Color(0.35f, 0.55f, 1f, 0.1f), 1500f);
        copier = Glow(canvas, new Color(0.8f, 0.9f, 1f, 0.07f), 0f);
        copier.anchorMin = new Vector2(0f, 0.5f);
        copier.anchorMax = new Vector2(1f, 0.5f);
        copier.sizeDelta = new Vector2(0f, 260f);
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
        float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - builtAt) / 1.1f));
        if (art != null) {
            art.color = new Color(1f, 1f, 1f, appear);
        }
        if (drift1 != null) {
            drift1.anchoredPosition = new Vector2(-420f + Mathf.Sin(t * 0.11f) * 160f, -200f + Mathf.Cos(t * 0.09f) * 110f);
        }
        if (drift2 != null) {
            drift2.anchoredPosition = new Vector2(480f + Mathf.Cos(t * 0.08f) * 180f, 220f + Mathf.Sin(t * 0.1f) * 120f);
        }
        if (copier != null) {
            // Eleven seconds a pass: nine of them travelling top to bottom, two of rest below the screen.
            float f = Mathf.Repeat(t, 11f) / 9f;
            float y = Mathf.Lerp(1.15f, -0.15f, Mathf.Clamp01(f));
            copier.anchorMin = new Vector2(0f, y);
            copier.anchorMax = new Vector2(1f, y);
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

    RectTransform Panel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 sizeDelta) {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image);
        image.color = color;
        image.raycastTarget = false;
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

    Button Button(Transform parent, string label, Vector2 position, Vector2 size, int fontSize, UnityEngine.Events.UnityAction click, bool primary = false) {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        // A soft gold halo behind the button: it lights on hover and breathes on the primary one.
        var halo = new GameObject("Halo", typeof(RectTransform)).AddComponent<Image>();
        halo.transform.SetParent(go.transform, false);
        halo.sprite = UiSkin.Glow;
        halo.type = UnityEngine.UI.Image.Type.Sliced;
        halo.raycastTarget = false;
        halo.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
        halo.rectTransform.anchorMin = Vector2.zero;
        halo.rectTransform.anchorMax = Vector2.one;
        halo.rectTransform.offsetMin = new Vector2(-14f, -14f);
        halo.rectTransform.offsetMax = new Vector2(14f, 14f);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, 0.6f);
        image.color = primary ? new Color(0.22f, 0.38f, 0.22f, 1f) : new Color(0.28f, 0.22f, 0.12f, 1f);
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
            GameAudio.Play("hit", 0.14f, 1.4f);
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        down = false;
    }

    void Update() {
        bool live = button != null && button.interactable;
        float dt = Time.unscaledDeltaTime;
        hover = Mathf.MoveTowards(hover, over && live ? 1f : 0f, dt * 8f);
        press = Mathf.MoveTowards(press, down && live ? 1f : 0f, dt * 14f);
        float breathe = primary && live ? (Mathf.Sin(Time.unscaledTime * 2.2f) * 0.5f + 0.5f) : 0f;
        float scale = 1f + hover * 0.045f - press * 0.075f + breathe * 0.012f;
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

    public void Setup(Vector2 from, float delay) {
        rect = (RectTransform)transform;
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
        if (k >= 1f) {
            Destroy(group);
            Destroy(this);
        }
    }
}
