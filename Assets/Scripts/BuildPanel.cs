using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The build: every boon held this run, with its rank and its one-line effect (RunBoons.About, the same words as the
// level-up card) and its card icon (UiArt.Boon). Two views read it: this panel, opened from Pause, and the run-end
// sheet's icon band (RunEndScreen). Nothing here changes the run.
public static class BuildList {
    public struct Entry {
        public BoonId Id;
        public string Name;
        public string About;
        public int Rank;
        public int Max;
        public bool Evolution;
        public Rarity Rarity;
    }

    // Evolutions first, then by rank, so the strongest part of the build leads. The sort is stable on enum order.
    public static List<Entry> Held() {
        var list = new List<Entry>();
        for (int i = 0; i < RunBoons.All.Length; i++) {
            BoonInfo info = RunBoons.All[i];
            int rank = RunBoons.Rank(info.Id);
            if (rank <= 0) {
                continue;
            }
            list.Add(new Entry { Id = info.Id, Name = info.Name, About = info.About, Rank = rank, Max = info.MaxRank, Evolution = info.Evolution, Rarity = info.Rarity });
        }
        var ordered = new List<Entry>();
        foreach (Entry e in list) {
            if (e.Evolution) {
                ordered.Add(e);
            }
        }
        for (int r = 6; r >= 1; r--) {
            foreach (Entry e in list) {
                if (!e.Evolution && e.Rank == r) {
                    ordered.Add(e);
                }
            }
        }
        foreach (Entry e in list) {
            if (!e.Evolution && e.Rank > 6) {
                ordered.Add(e);
            }
        }
        return ordered;
    }

    public static string Level(Entry e) {
        if (e.Evolution) {
            return "EVO";
        }
        return e.Max > 1 ? e.Rank + "/" + e.Max : "1";
    }
}

// The pause menu's Build panel: a scrolling list, one 100-unit row per boon (icon, name and level, effect). Rows are
// about 58 px tall on a 390 px phone and the effect is 24 units (14 px); it scrolls by drag, wheel or Up / Down.
public class BuildPanel : MonoBehaviour {
    const float PanelWidth = 760f;
    const float PanelHeight = 900f;
    const float RowHeight = 108f;
    const float Gap = 8f;

    static BuildPanel instance;

    public static bool IsOpen {
        get { return instance != null && instance.root != null && instance.root.activeSelf; }
    }
    public static int ClosedFrame { get; private set; } = -1;
    public static BuildPanel Instance { get { return instance; } }

    // For the playtest bot.
    public Button BackButton { get; private set; }
    public ScrollRect Scroll { get; private set; }
    public RectTransform Panel { get { return panel; } }
    public RectTransform Viewport { get { return viewport; } }
    public int RowCount { get; private set; }
    public float RowPx { get { return rowPx; } }
    public string CountText { get { return count.text; } }
    public List<string> RowTexts { get { return rowTexts; } }
    public bool CanScroll { get { return content != null && viewport != null && content.rect.height > viewport.rect.height + 1f; } }

    Font font;
    GameObject root;
    CanvasScaler scaler;
    RectTransform panel;
    RectTransform viewport;
    RectTransform content;
    Text count;
    float rowPx;
    int laidStamp = -1;
    readonly List<string> rowTexts = new List<string>();

    public static void Open() {
        if (instance == null) {
            new GameObject("Build Panel").AddComponent<BuildPanel>();
        }
        instance.Fill();
        instance.root.SetActive(true);
        instance.Layout();
        instance.Scroll.verticalNormalizedPosition = 1f;
        RunAnalytics.Screen("build");
    }

    public static void Close() {
        if (!IsOpen) {
            return;
        }
        instance.root.SetActive(false);
        ClosedFrame = Time.frameCount;
        RunAnalytics.Screen("pause");
    }

    void Awake() {
        instance = this;
        font = UiSkin.Font(UiSkin.FontRole.Body);
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
        float step = 0f;
        if (PlayInput.GetKey(KeyCode.DownArrow) || PlayInput.GetKey(KeyCode.S)) {
            step = -1f;
        } else if (PlayInput.GetKey(KeyCode.UpArrow) || PlayInput.GetKey(KeyCode.W)) {
            step = 1f;
        }
        if (step != 0f && CanScroll) {
            float span = content.rect.height - viewport.rect.height;
            Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition + step * 900f * Time.unscaledDeltaTime / span);
        }
        if (MenuNav.Back) {
            Close();
        }
    }

    // One row per boon held. Rebuilt on every open: the build changes between pauses.
    void Fill() {
        for (int i = content.childCount - 1; i >= 0; i--) {
            Destroy(content.GetChild(i).gameObject);
        }
        rowTexts.Clear();
        List<BuildList.Entry> held = BuildList.Held();
        RowCount = held.Count;
        float rowW = PanelWidth - 80f;
        for (int i = 0; i < held.Count; i++) {
            BuildList.Entry e = held[i];
            var row = new GameObject("Row " + e.Name, typeof(RectTransform));
            row.transform.SetParent(content, false);
            var bg = row.AddComponent<Image>();
            UiSkin.Round(bg, 0.5f);
            bg.color = e.Evolution ? new Color(0.3f, 0.2f, 0.06f, 0.95f) : new Color(0.14f, 0.12f, 0.17f, 0.95f);
            bg.raycastTarget = true;   // the row takes the drag, so a finger on it scrolls
            var rect = bg.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(rowW, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -(i * (RowHeight + Gap)) - Gap);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(row.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            if (!UiArt.Show(icon, UiArt.Boon(e.Id))) {
                icon.enabled = true;
                icon.color = RunBoons.RarityColor(e.Rarity);   // no art yet: a rarity-coloured plate keeps the row readable
            }
            var iRect = icon.rectTransform;
            iRect.anchorMin = iRect.anchorMax = new Vector2(0f, 0.5f);
            iRect.pivot = new Vector2(0f, 0.5f);
            iRect.anchoredPosition = new Vector2(12f, 0f);
            iRect.sizeDelta = new Vector2(84f, 84f);

            Text name = MakeText(row.transform, e.Name, 28, FontStyle.Bold, TextAnchor.UpperLeft);
            name.color = RunBoons.RarityColor(e.Rarity);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Anchor(name.rectTransform, new Vector2(108f, -8f), new Vector2(rowW - 108f - 150f, 36f));
            Text level = MakeText(row.transform, BuildList.Level(e), 28, FontStyle.Bold, TextAnchor.UpperRight);
            level.color = e.Evolution ? new Color(1f, 0.85f, 0.45f) : new Color(1f, 0.97f, 0.9f);
            var lRect = level.rectTransform;
            lRect.anchorMin = lRect.anchorMax = new Vector2(1f, 1f);
            lRect.pivot = new Vector2(1f, 1f);
            lRect.anchoredPosition = new Vector2(-14f, -8f);
            lRect.sizeDelta = new Vector2(140f, 36f);
            Text about = MakeText(row.transform, e.About, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            about.color = new Color(0.9f, 0.92f, 0.85f, 0.95f);
            about.resizeTextForBestFit = true;
            about.resizeTextMinSize = 24;
            about.resizeTextMaxSize = 24;
            about.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(about.rectTransform, new Vector2(108f, -44f), new Vector2(rowW - 108f - 14f, 60f));
            rowTexts.Add(e.Name + " " + BuildList.Level(e) + " - " + e.About);
        }
        content.sizeDelta = new Vector2(0f, Mathf.Max(1f, held.Count * (RowHeight + Gap) + Gap));
        if (held.Count == 0) {
            count.text = "Nothing yet. Level up and take a card.";
        } else {
            count.text = held.Count + (held.Count == 1 ? " boon" : " boons") + " held" + (held.Count > 5 ? "  ·  drag to scroll" : "");
        }
    }

    void Build() {
        var canvasGo = new GameObject("Build Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform));
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
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Text heading = MakeText(panel, "Your build", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
        Anchor(heading.rectTransform, new Vector2(0f, -22f), new Vector2(PanelWidth - 60f, 60f), true);
        count = MakeText(panel, "", 24, FontStyle.Italic, TextAnchor.MiddleCenter);
        count.color = new Color(1f, 0.9f, 0.72f, 0.9f);
        Anchor(count.rectTransform, new Vector2(0f, -84f), new Vector2(PanelWidth - 60f, 36f), true);

        var viewGo = new GameObject("Viewport", typeof(RectTransform));
        viewGo.transform.SetParent(panel, false);
        viewport = (RectTransform)viewGo.transform;
        viewport.anchorMin = new Vector2(0.5f, 1f);
        viewport.anchorMax = new Vector2(0.5f, 1f);
        viewport.pivot = new Vector2(0.5f, 1f);
        viewport.anchoredPosition = new Vector2(0f, -130f);
        viewport.sizeDelta = new Vector2(PanelWidth - 60f, PanelHeight - 130f - 130f);
        viewGo.AddComponent<RectMask2D>();
        var viewBg = viewGo.AddComponent<Image>();
        viewBg.color = new Color(0f, 0f, 0f, 0.001f);   // catches drags in the gaps between rows

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewport, false);
        content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        Scroll = viewGo.AddComponent<ScrollRect>();
        Scroll.viewport = viewport;
        Scroll.content = content;
        Scroll.horizontal = false;
        Scroll.vertical = true;
        Scroll.movementType = ScrollRect.MovementType.Clamped;
        Scroll.inertia = true;
        Scroll.scrollSensitivity = 60f;

        BackButton = MakeButton(panel, "Back", Close, new Vector2(0f, 24f), new Vector2(240f, 88f));
        BackButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        MenuNav.Attach(root, 560);
        Layout();
    }

    void Layout() {
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler, 0.5f);
        Vector2 size = UiFit.Size;
        float scale = 1f;
        if (UiFit.Compact) {
            Vector4 safe = UiFit.SafeInsets();
            scale = Mathf.Min(1f, (size.x - safe.x - safe.y - 24f) / PanelWidth, (size.y - safe.z - safe.w - 24f) / PanelHeight);
        }
        panel.localScale = new Vector3(scale, scale, 1f);
        rowPx = RowHeight * scale * (Screen.height / Mathf.Max(1f, size.y)) / Mathf.Max(0.01f, UiFit.PixelRatio);
    }

    Button MakeButton(Transform parent, string name, UnityEngine.Events.UnityAction click, Vector2 position, Vector2 size) {
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
        button.onClick.AddListener(click);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
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

    // Top-left of its parent, or top-centred when centred is true.
    static void Anchor(RectTransform rect, Vector2 position, Vector2 size, bool centred = false) {
        float x = centred ? 0.5f : 0f;
        rect.anchorMin = rect.anchorMax = new Vector2(x, 1f);
        rect.pivot = new Vector2(x, 1f);
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
