using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Settings: sound on/off, music volume and effects volume (SoundSettings). Opened from the main menu's Settings
// button and from the pause menu; changes are heard right away and saved as they are made. One panel for every
// layout, scaled down to fit a phone, with 88-unit buttons and a slider knob as big.
public class SettingsPanel : MonoBehaviour {
    const float PanelWidth = 680f;
    const float PanelHeight = 680f;

    static SettingsPanel instance;

    public static bool IsOpen {
        get { return instance != null && instance.root != null && instance.root.activeSelf; }
    }

    // The frame Settings closed, so the Escape that closed it doesn't also close the pause menu under it.
    public static int ClosedFrame { get; private set; } = -1;

    // For the playtest bot.
    public static SettingsPanel Instance {
        get { return instance; }
    }
    public Button SoundButton { get; private set; }
    public Slider MusicSlider { get; private set; }
    public Slider EffectsSlider { get; private set; }
    public Button DoneButton { get; private set; }

    Font font;
    GameObject root;
    CanvasScaler scaler;
    RectTransform panel;
    Text soundLabel;
    Image soundImage;
    Text musicValue;
    Text effectsValue;
    System.Action whenClosed;
    bool filling;
    float lastBlip;
    int laidStamp = -1;

    public static void Open(System.Action closed) {
        if (instance == null) {
            new GameObject("Settings").AddComponent<SettingsPanel>();
        }
        instance.whenClosed = closed;
        instance.root.SetActive(true);
        instance.Fill();
        instance.Layout();
        RunAnalytics.Screen("settings");
    }

    public static void Close() {
        if (!IsOpen) {
            return;
        }
        instance.root.SetActive(false);
        ClosedFrame = Time.frameCount;
        SoundSettings.Flush();
        RunAnalytics.SettingsClosed(PauseMenu.IsOpen ? "pause" : "menu");
        RunAnalytics.Screen(PauseMenu.IsOpen ? "pause" : WaveDirector.Instance != null ? "wave" : "menu");
        System.Action closed = instance.whenClosed;
        instance.whenClosed = null;
        closed?.Invoke();
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
            SoundSettings.Flush();
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
        if (PlayInput.GetKeyDown(KeyCode.Escape)) {
            Close();
        }
    }

    void Fill() {
        filling = true;
        bool on = SoundSettings.On;
        soundLabel.text = on ? "On" : "Off";
        soundImage.color = on ? new Color(0.22f, 0.38f, 0.22f, 1f) : new Color(0.3f, 0.26f, 0.24f, 1f);
        MusicSlider.value = SoundSettings.Music;
        EffectsSlider.value = SoundSettings.Sfx;
        musicValue.text = Percent(SoundSettings.Music);
        effectsValue.text = Percent(SoundSettings.Sfx);
        // Volumes still move with sound off, and take effect when it comes back on.
        float alpha = on ? 1f : 0.5f;
        musicValue.color = new Color(1f, 0.96f, 0.86f, alpha);
        effectsValue.color = new Color(1f, 0.96f, 0.86f, alpha);
        filling = false;
    }

    static string Percent(float value) {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    void ToggleSound() {
        SoundSettings.Set(!SoundSettings.On, SoundSettings.Music, SoundSettings.Sfx);
        Fill();
        Blip(true);
    }

    void MusicChanged(float value) {
        if (filling) {
            return;
        }
        SoundSettings.Set(SoundSettings.On, value, SoundSettings.Sfx);
        Fill();
    }

    void EffectsChanged(float value) {
        if (filling) {
            return;
        }
        SoundSettings.Set(SoundSettings.On, SoundSettings.Music, value);
        Fill();
        Blip(false);
    }

    // A short arrow sound at the new effects level, so the slider is heard.
    void Blip(bool always) {
        if (!always && Time.unscaledTime - lastBlip < 0.18f) {
            return;
        }
        lastBlip = Time.unscaledTime;
        GameAudio.Play("shot", 0.6f, 1.2f);
    }

    void Build() {
        var canvasGo = new GameObject("Settings Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(canvasGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.72f);
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

        Text title = MakeText(panel, "Settings", 48, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0f, -28f), new Vector2(PanelWidth - 60f, 64f), new Vector2(0.5f, 1f));

        RowLabel("Sound", -170f);
        SoundButton = MakeButton(panel, "Sound", "On", new Vector2(220f, 88f), ToggleSound);
        soundImage = SoundButton.GetComponent<Image>();
        soundLabel = SoundButton.GetComponentInChildren<Text>();
        Place((RectTransform)SoundButton.transform, new Vector2(240f, -170f), new Vector2(220f, 88f), new Vector2(0f, 1f), true);

        RowLabel("Music", -300f);
        MusicSlider = MakeSlider("Music", -300f, MusicChanged);
        musicValue = ValueLabel(-300f);

        RowLabel("Effects", -430f);
        EffectsSlider = MakeSlider("Effects", -430f, EffectsChanged);
        effectsValue = ValueLabel(-430f);

        // What Hawkes Analytics (RunAnalytics) collects, said where a player of the game page can read it.
        Text privacy = MakeText(panel, "Anonymous gameplay statistics help improve the game.\nNo cookies, no personal data, no IP stored.", 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        privacy.color = new Color(1f, 0.97f, 0.9f, 0.62f);
        Place(privacy.rectTransform, new Vector2(0f, -492f), new Vector2(PanelWidth - 60f, 60f), new Vector2(0.5f, 1f));

        DoneButton = MakeButton(panel, "Done", "Done", new Vector2(260f, 88f), Close);
        DoneButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        var doneRect = (RectTransform)DoneButton.transform;
        doneRect.anchorMin = new Vector2(0.5f, 0f);
        doneRect.anchorMax = new Vector2(0.5f, 0f);
        doneRect.pivot = new Vector2(0.5f, 0f);
        doneRect.anchoredPosition = new Vector2(0f, 32f);
        doneRect.sizeDelta = new Vector2(260f, 88f);
        Layout();
    }

    // Every layout uses the same panel; a phone gets it scaled to fit inside a 20-unit margin.
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

    void RowLabel(string value, float y) {
        Text label = MakeText(panel, value, 32, FontStyle.Bold, TextAnchor.MiddleLeft);
        Place(label.rectTransform, new Vector2(44f, y), new Vector2(180f, 88f), new Vector2(0f, 1f), true);
    }

    Text ValueLabel(float y) {
        Text value = MakeText(panel, "", 28, FontStyle.Normal, TextAnchor.MiddleRight);
        Place(value.rectTransform, new Vector2(PanelWidth - 140f, y), new Vector2(100f, 88f), new Vector2(0f, 1f), true);
        return value;
    }

    // A slider whose whole row (88 units tall) takes the finger, with a knob as big as a button.
    Slider MakeSlider(string name, float y, UnityEngine.Events.UnityAction<float> changed) {
        var go = new GameObject(name + " Slider", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rect = (RectTransform)go.transform;
        Place(rect, new Vector2(240f, y), new Vector2(PanelWidth - 400f, 88f), new Vector2(0f, 1f), true);
        var catcher = go.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);

        var track = new GameObject("Track").AddComponent<Image>();
        track.transform.SetParent(go.transform, false);
        UiSkin.Bar(track);
        track.color = new Color(1f, 0.96f, 0.88f, 0.16f);
        track.raycastTarget = false;
        Band(track.rectTransform, 18f, 0f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Band((RectTransform)fillArea.transform, 18f, 0f);
        var fill = new GameObject("Fill").AddComponent<Image>();
        fill.transform.SetParent(fillArea.transform, false);
        UiSkin.Bar(fill);
        fill.color = new Color(1f, 0.84f, 0.35f, 0.9f);
        fill.raycastTarget = false;
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handleArea = new GameObject("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Band((RectTransform)handleArea.transform, 0f, 30f);
        var handle = new GameObject("Knob").AddComponent<Image>();
        handle.transform.SetParent(handleArea.transform, false);
        UiSkin.Disc(handle, 60f);
        handle.color = new Color(1f, 0.94f, 0.8f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(60f, 60f);

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        var colors = slider.colors;
        colors.highlightedColor = new Color(1.1f, 1.05f, 0.9f);
        colors.pressedColor = new Color(1f, 0.85f, 0.4f);
        slider.colors = colors;
        slider.onValueChanged.AddListener(changed);
        return slider;
    }

    // Full width (less an inset at each end), centered vertically, a given height (0: the full height).
    static void Band(RectTransform rect, float height, float inset) {
        rect.anchorMin = new Vector2(0f, height > 0f ? 0.5f : 0f);
        rect.anchorMax = new Vector2(1f, height > 0f ? 0.5f : 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-inset * 2f, height);
    }

    Button MakeButton(Transform parent, string name, string label, Vector2 size, UnityEngine.Events.UnityAction click) {
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
        image.rectTransform.sizeDelta = size;
        Text text = MakeText(go.transform, label, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
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

    // Top-anchored: a centered row (pivot x 0.5) or, with left, a box whose left edge is at position.x.
    static void Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor, bool left = false) {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = left ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
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
