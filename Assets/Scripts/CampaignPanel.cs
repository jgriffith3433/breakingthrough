using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The case files: three Campaign save slots, opened from the main menu's Campaign button. Pressing a file opens it (an
// empty one starts Chapter 1) and loads the chapter scene; Erase asks twice. Same look and sizing rules as the
// Settings panel: one layout scaled to fit a phone, 88-unit buttons.
public class CampaignPanel : MonoBehaviour {
    const float PanelWidth = 920f;
    const float PanelHeight = 700f;
    const float RowHeight = 124f;

    static CampaignPanel instance;

    public static bool IsOpen {
        get { return instance != null && instance.root != null && instance.root.activeSelf; }
    }

    // The frame it closed, so the Escape that closed it does nothing else.
    public static int ClosedFrame { get; private set; } = -1;

    // For the playtest bot.
    public static CampaignPanel Instance {
        get { return instance; }
    }
    public Button[] SlotButtons { get; private set; } = new Button[CampaignSave.SlotCount];
    public Button[] EraseButtons { get; private set; } = new Button[CampaignSave.SlotCount];
    public Button[] PlusButtons { get; private set; } = new Button[CampaignSave.SlotCount];   // New Game+ on a filed file, straight from the menu
    public string RanksShown(int index) { return slotRanks[index] != null ? slotRanks[index].text : ""; }
    public Button BackButton { get; private set; }
    public Button LoreButton { get; private set; }
    public Button JournalButton { get; private set; }

    const string SubtitleText = "Pick a case file. The Department keeps your place.";
    Text subtitle;
    // What the line under the title says now (the load notice, or the usual line). For the playtest.
    public string SubtitleShown { get { return subtitle != null ? subtitle.text : ""; } }

    Font font;
    GameObject root;
    CanvasScaler scaler;
    RectTransform panel;
    readonly Text[] slotTitles = new Text[CampaignSave.SlotCount];
    readonly Text[] slotInfo = new Text[CampaignSave.SlotCount];
    readonly Text[] slotRanks = new Text[CampaignSave.SlotCount];
    readonly Text[] eraseLabels = new Text[CampaignSave.SlotCount];
    readonly Image[] slotImages = new Image[CampaignSave.SlotCount];
    int armedErase = -1;
    bool opening;
    int laidStamp = -1;

    public static void Open() {
        if (instance == null) {
            new GameObject("Case Files").AddComponent<CampaignPanel>();
        }
        instance.armedErase = -1;
        instance.root.SetActive(true);
        instance.Fill();
        instance.Layout();
        RunAnalytics.Screen("campaign");
    }

    public static void Close() {
        if (!IsOpen) {
            return;
        }
        instance.root.SetActive(false);
        CampaignSave.AckNotice();
        ClosedFrame = Time.frameCount;
        RunAnalytics.Screen("menu");
    }

    void Awake() {
        instance = this;
        font = UiSkin.Font(UiSkin.FontRole.Body);
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
        if (LorePanel.IsOpen || JournalPanel.IsOpen) {
            return;
        }
        if (MenuNav.Back && Time.frameCount != LorePanel.ClosedFrame && Time.frameCount != JournalPanel.ClosedFrame) {
            Close();
        }
    }

    void Fill() {
        for (int i = 0; i < CampaignSave.SlotCount; i++) {
            CampaignSave.Get(i);   // loads the files, so the notice below is current
        }
        bool note = !string.IsNullOrEmpty(CampaignSave.Notice);
        subtitle.text = note ? CampaignSave.Notice : SubtitleText;
        subtitle.color = note ? new Color(1f, 0.78f, 0.35f, 1f) : new Color(0.8f, 0.85f, 0.95f, 0.8f);
        for (int i = 0; i < CampaignSave.SlotCount; i++) {
            CampaignSave.Slot slot = CampaignSave.Get(i);
            slotTitles[i].text = "Case file " + (i + 1) + (slot.used ? ": " + CampaignSave.Describe(slot) : "");
            slotInfo[i].text = slot.used ? CampaignSave.Clock(slot) + "  |  " + CampaignSave.Completion(slot) + "  |  Lore " + Lore.UnlockedCount(slot) + "/" + Lore.All.Length + "  |  " + (Journal.PaidInFull(slot) ? Journal.PaidTitle : "Journal " + (Journal.FindCount(slot) + Journal.GoalCount(slot)) + "/" + (Journal.Count * 2)) + (slot.hard ? "  |  HARD" : "") : "No file yet. Press to open a new one.";
            slotRanks[i].text = slot.used ? RanksLine(slot) : "";
            PlusButtons[i].gameObject.SetActive(CampaignGrade.Filed(slot));
            slotImages[i].color = slot.used ? new Color(0.3f, 0.24f, 0.12f, 1f) : new Color(0.14f, 0.15f, 0.21f, 1f);
            EraseButtons[i].gameObject.SetActive(slot.used);
            eraseLabels[i].text = armedErase == i ? "Sure?" : "Erase";
            EraseButtons[i].GetComponent<Image>().color = armedErase == i ? new Color(0.7f, 0.16f, 0.12f, 1f) : new Color(0.28f, 0.2f, 0.18f, 1f);
        }
    }

    // "Ranks 1 A  2 B  3 S  4 -  5 -  6 -", with the loop on a New Game+ file.
    static string RanksLine(CampaignSave.Slot slot) {
        return "Ranks " + CampaignSave.RankLine(slot) + (slot.plus > 0 ? "   |   NEW GAME+ " + slot.plus : "");
    }

    // New Game+ from the case files: the filed file starts again at Chapter 1 (harder bots, an echo line a chapter) and opens.
    public void PlusSlot(int index) {
        if (CampaignSave.NewGamePlus(index)) {
            RunAnalytics.Screen("new_game_plus");
            OpenSlot(index);
        }
    }

    // Opens the file and goes to the chapter scene.
    public void OpenSlot(int index) {
        CampaignSave.AckNotice();
        int chapter = CampaignSave.Open(index);
        RunAnalytics.Screen("chapter_" + (chapter + 1));
        GameAudio.PlayUI("stamp", 0.6f);
        if (opening) {
            return;
        }
        opening = true;
        // The campaign's portraits, skies and lodgers come down now, on this choice, with the progress line up; the menu
        // and Arcade never carried them. With nothing to fetch (the editor, a build that kept them) this is at once.
        OnDemand.Need(new[] { OnDemand.Core }, "Pulling the case file", () => {
            opening = false;
            SceneManager.LoadScene(CampaignSave.Scene);
        });
    }

    // The lore log of the file last opened (or the first file in use).
    void OpenLore() {
        int index = CampaignSave.Last;
        for (int i = 0; index < 0 && i < CampaignSave.SlotCount; i++) {
            index = CampaignSave.Get(i).used ? i : -1;
        }
        LorePanel.Open(Mathf.Max(0, index));
    }

    // The journal of the file last opened (or the first file in use).
    void OpenJournal() {
        int index = CampaignSave.Last;
        for (int i = 0; index < 0 && i < CampaignSave.SlotCount; i++) {
            index = CampaignSave.Get(i).used ? i : -1;
        }
        JournalPanel.Open(Mathf.Max(0, index));
    }

    void EraseSlot(int index) {
        if (armedErase != index) {
            armedErase = index;
            Fill();
            return;
        }
        armedErase = -1;
        CampaignSave.Erase(index);
        Fill();
    }

    void Build() {
        var canvasGo = new GameObject("Case Files Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 590;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(canvasGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.78f);
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

        Text title = MakeText(panel, "Campaign", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0f, -24f), new Vector2(PanelWidth - 60f, 64f), new Vector2(0.5f, 1f), false);
        Text sub = MakeText(panel, SubtitleText, 24, FontStyle.Italic, TextAnchor.MiddleCenter);
        sub.color = new Color(0.8f, 0.85f, 0.95f, 0.8f);
        subtitle = sub;
        Place(sub.rectTransform, new Vector2(0f, -90f), new Vector2(PanelWidth - 60f, 40f), new Vector2(0.5f, 1f), false);

        float eraseWidth = 150f;
        float slotWidth = PanelWidth - 80f - 2f * eraseWidth - 28f;
        for (int i = 0; i < CampaignSave.SlotCount; i++) {
            int index = i;
            float y = -150f - i * (RowHeight + 16f);
            Button slotButton = MakeButton(panel, "Slot " + (i + 1), new Vector2(slotWidth, RowHeight), () => OpenSlot(index), out Text unused);
            Destroy(unused.gameObject);
            SlotButtons[i] = slotButton;
            slotImages[i] = slotButton.GetComponent<Image>();
            Place((RectTransform)slotButton.transform, new Vector2(40f, y), new Vector2(slotWidth, RowHeight), new Vector2(0f, 1f), true, true);
            slotTitles[i] = MakeText(slotButton.transform, "", 30, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(slotTitles[i].rectTransform, new Vector2(24f, -8f), new Vector2(slotWidth - 40f, 40f), new Vector2(0f, 1f), true, true);
            slotTitles[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            slotInfo[i] = MakeText(slotButton.transform, "", 22, FontStyle.Normal, TextAnchor.MiddleLeft);
            slotInfo[i].color = new Color(0.85f, 0.88f, 0.95f, 0.8f);
            Place(slotInfo[i].rectTransform, new Vector2(24f, -48f), new Vector2(slotWidth - 40f, 32f), new Vector2(0f, 1f), true, true);
            slotInfo[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            slotRanks[i] = MakeText(slotButton.transform, "", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            slotRanks[i].color = new Color(1f, 0.82f, 0.38f, 0.95f);
            Place(slotRanks[i].rectTransform, new Vector2(24f, -84f), new Vector2(slotWidth - 40f, 32f), new Vector2(0f, 1f), true, true);
            slotRanks[i].horizontalOverflow = HorizontalWrapMode.Overflow;

            EraseButtons[i] = MakeButton(panel, "Erase " + (i + 1), new Vector2(eraseWidth, RowHeight), () => EraseSlot(index), out eraseLabels[i]);
            Place((RectTransform)EraseButtons[i].transform, new Vector2(40f + slotWidth + 14f, y), new Vector2(eraseWidth, RowHeight), new Vector2(0f, 1f), true, true);
            eraseLabels[i].fontSize = 26;
            PlusButtons[i] = MakeButton(panel, "New Game+ " + (i + 1), new Vector2(eraseWidth, RowHeight), () => PlusSlot(index), out Text plusLabel);
            Place((RectTransform)PlusButtons[i].transform, new Vector2(40f + slotWidth + 14f + eraseWidth + 14f, y), new Vector2(eraseWidth, RowHeight), new Vector2(0f, 1f), true, true);
            plusLabel.text = "New Game+";
            plusLabel.fontSize = 24;
            PlusButtons[i].GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.4f, 1f);
        }

        LoreButton = MakeButton(panel, "Lore log", new Vector2(220f, 88f), OpenLore, out Text loreLabel);
        loreLabel.text = "Lore log";
        BottomButton(LoreButton, -236f);
        JournalButton = MakeButton(panel, "Journal", new Vector2(220f, 88f), OpenJournal, out Text journalLabel);
        journalLabel.text = "Journal";
        BottomButton(JournalButton, 0f);
        BackButton = MakeButton(panel, "Back", new Vector2(220f, 88f), Close, out Text backLabel);
        backLabel.text = "Back";
        BackButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        BottomButton(BackButton, 236f);
        MenuNav.Attach(root, 590);
        Layout();
    }

    static void BottomButton(Button button, float x) {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 28f);
        rect.sizeDelta = new Vector2(220f, 88f);
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

    Button MakeButton(Transform parent, string name, Vector2 size, UnityEngine.Events.UnityAction click, out Text label) {
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
        image.rectTransform.sizeDelta = size;
        MenuButtonFx.Add(button);
        label = MakeText(go.transform, name, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
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

    // Top-anchored: a centered row (pivot x 0.5) or, with left, a box whose left edge is at position.x. With topPivot
    // the box hangs from its top edge (rows with text inside) instead of being centered on position.y.
    static void Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor, bool left, bool topPivot = false) {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = left ? new Vector2(0f, topPivot ? 1f : 0.5f) : new Vector2(0.5f, 1f);
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
