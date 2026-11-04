using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The journal of one case file (Journal.cs): optional objectives and hidden finds in three tabs, Active, Finished and
// Collected, turned with Prev/Next (or the arrow keys). Opened from the case files panel, the pause menu and the hub (J).
// Same look and sizing as LorePanel: one 760x700 layout scaled to fit a phone, 88-unit buttons.
public class JournalPanel : MonoBehaviour {
    const float PanelWidth = 760f;
    const float PanelHeight = 700f;

    static JournalPanel instance;

    public static bool IsOpen {
        get { return instance != null && instance.root != null && instance.root.activeSelf; }
    }

    // The frame it closed, so the Escape that closed it does nothing else.
    public static int ClosedFrame { get; private set; } = -1;

    public static JournalPanel Instance {
        get { return instance; }
    }

    // For the playtest bot.
    public Button PrevButton { get; private set; }
    public Button NextButton { get; private set; }
    public Button BackButton { get; private set; }
    public string TitleText { get { return heading.text; } }
    public string EntryTitle { get { return entryTitle.text; } }
    public string TabLabel { get { return entryTitle.text; } }
    public string SummaryText { get { return counter.text; } }
    public string BodyText { get { return body.text; } }
    public int Page { get { return page; } }
    public int Slot { get { return slotIndex; } }

    Font font;
    GameObject root;
    CanvasScaler scaler;
    RectTransform panel;
    Text heading;
    Text entryTitle;
    Text body;
    Text counter;
    int slotIndex;
    int page;
    int laidStamp = -1;
    string reopenTo = "campaign";

    public static void Open(int slot) {
        if (instance == null) {
            new GameObject("Journal").AddComponent<JournalPanel>();
        }
        instance.slotIndex = Mathf.Clamp(slot, 0, CampaignSave.SlotCount - 1);
        instance.page = 0;
        instance.reopenTo = PauseMenu.IsOpen ? "pause" : "campaign";
        instance.root.SetActive(true);
        instance.Fill();
        instance.Layout();
        RunAnalytics.Screen("journal");
    }

    public static void Close() {
        if (!IsOpen) {
            return;
        }
        instance.root.SetActive(false);
        ClosedFrame = Time.frameCount;
        RunAnalytics.Screen(instance.reopenTo);
    }

    void Awake() {
        instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Build();
        root.SetActive(false);
    }

    void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
    }

    void Update() {
        if (!IsOpen) {
            return;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        if (PlayInput.GetKeyDown(KeyCode.LeftArrow)) {
            Turn(-1);
        } else if (PlayInput.GetKeyDown(KeyCode.RightArrow)) {
            Turn(1);
        }
        if (MenuNav.Back) {
            Close();
        }
    }

    void Turn(int by) {
        int n = Journal.TabNames.Length;
        page = (page + by + n) % n;
        Fill();
    }

    // One tab per page: Active (objectives and finds still open), Finished (objectives met), Collected (finds kept).
    void Fill() {
        CampaignSave.Slot slot = CampaignSave.Get(slotIndex);
        bool paid = Journal.PaidInFull(slot);
        heading.text = "Journal: case file " + (slotIndex + 1) + (paid ? "  -  " + Journal.PaidTitle : "");
        heading.color = paid ? new Color(1f, 0.85f, 0.45f) : new Color(1f, 0.97f, 0.9f);
        entryTitle.text = Journal.TabNames[page];
        body.text = Journal.TabText(slot, page);
        counter.text = (page + 1) + " / " + Journal.TabNames.Length + "     " + Journal.Summary(slot);
    }

    void Build() {
        var canvasGo = new GameObject("Journal Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 595;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(canvasGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.85f);
        Stretch(dim.rectTransform);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(root.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        UiSkin.Round(panelImage);
        panelImage.color = new Color(0.07f, 0.06f, 0.09f, 0.97f);
        panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        heading = MakeText(panel, "Journal", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(heading.rectTransform, new Vector2(0f, -24f), new Vector2(PanelWidth - 60f, 60f), 1f);
        entryTitle = MakeText(panel, "", 38, FontStyle.Bold, TextAnchor.MiddleCenter);
        entryTitle.color = new Color(1f, 0.85f, 0.45f);
        Place(entryTitle.rectTransform, new Vector2(0f, -120f), new Vector2(PanelWidth - 80f, 60f), 1f);
        body = MakeText(panel, "", 28, FontStyle.Normal, TextAnchor.UpperLeft);
        Place(body.rectTransform, new Vector2(0f, -190f), new Vector2(PanelWidth - 100f, 320f), 1f);
        body.fontSize = 24;
        body.resizeTextForBestFit = true;
        body.resizeTextMinSize = 16;
        body.resizeTextMaxSize = 26;
        body.verticalOverflow = VerticalWrapMode.Truncate;
        counter = MakeText(panel, "", 22, FontStyle.Italic, TextAnchor.MiddleCenter);
        counter.color = new Color(0.8f, 0.85f, 0.95f, 0.8f);
        Place(counter.rectTransform, new Vector2(0f, -520f), new Vector2(PanelWidth - 60f, 36f), 1f);

        PrevButton = MakeButton(panel, "Prev", () => Turn(-1), new Vector2(40f, 28f), new Vector2(0f, 0f), new Vector2(200f, 88f));
        NextButton = MakeButton(panel, "Next", () => Turn(1), new Vector2(-40f, 28f), new Vector2(1f, 0f), new Vector2(200f, 88f));
        BackButton = MakeButton(panel, "Back", Close, new Vector2(0f, 28f), new Vector2(0.5f, 0f), new Vector2(220f, 88f));
        BackButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        MenuNav.Attach(root, 595);
        Layout();
    }

    void Layout() {
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler, 0.5f);
        Vector2 size = UiFit.Size;
        float scale = 1f;
        if (UiFit.Compact) {
            scale = Mathf.Min(1f, (size.x - 40f) / PanelWidth, (size.y - 40f) / PanelHeight);
        }
        panel.localScale = new Vector3(scale, scale, 1f);
    }

    Button MakeButton(Transform parent, string name, UnityEngine.Events.UnityAction click, Vector2 position, Vector2 anchor, Vector2 size) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, 0.6f);
        image.color = new Color(0.28f, 0.22f, 0.12f, 1f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.08f, 0.85f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.45f);
        button.colors = colors;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(click);
        var rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        MenuButtonFx.Add(button);
        Text label = MakeText(go.transform, name, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        return button;
    }

    Text MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor anchor) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.9f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size, float anchorY) {
        rect.anchorMin = new Vector2(0.5f, anchorY);
        rect.anchorMax = new Vector2(0.5f, anchorY);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
