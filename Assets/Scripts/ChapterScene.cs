using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Campaign's chapter scene (Assets/Campaign.unity is an empty scene; this builds everything in it). For now a
// placeholder: the chapter card, the place, and Wallit and Slice trading the premise from Story.Chapters. The open
// case file's time on the books is saved when you leave. Begin plays the chapter (ChapterRun, in the game scene); Esc,
// M or the Menu button goes back to the main menu.
public class ChapterScene : MonoBehaviour {
    public static ChapterScene Instance { get; private set; }

    // Set when a chapter's Chapter Complete card leads on: the next load of this scene opens in the hub (ChapterHub), not on
    // the chapter card. Taken once.
    public static bool HubNext;

    // For the playtest bot.
    public Button MenuButton { get; private set; }
    public Button BeginButton { get; private set; }
    public Button HubButton { get; private set; }
    public ChapterHub Hub { get; private set; }
    public bool CardUp { get { return cardCanvas != null; } }
    public int Chapter { get; private set; }
    public int LinesShown { get; private set; }
    public int LineCount { get; private set; }
    public string Title { get; private set; }
    public float Open { get; private set; }

    const float LineEvery = 1.4f;

    Font font;
    GameObject cardCanvas;
    Text[] lines;
    float startedAt;
    float cardAt;
    int shown;
    bool leaving;
    bool handoff;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == CampaignSave.Scene && Instance == null) {
            new GameObject("Chapter").AddComponent<ChapterScene>();
        }
    }

    void Awake() {
        Instance = this;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (CampaignSave.Active < 0) {
            // Opened straight from the editor: use the last file, or the first.
            CampaignSave.Open(Mathf.Max(0, CampaignSave.Last));
        }
        Chapter = Mathf.Clamp(CampaignSave.Get(CampaignSave.Active).chapter, 0, CampaignSave.ChapterCount - 1);
        Story.Chapter chapter = Story.Chapters[Chapter];
        Title = chapter.Title;
        LineCount = chapter.Lines.Length;
        startedAt = Time.unscaledTime;
        cardAt = startedAt;
        bool hub = HubNext && CampaignSave.Get(CampaignSave.Active).cleared > 0;
        HubNext = false;

        if (Camera.main == null) {
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 1f);
            cam.cullingMask = 0;
        }
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        if (hub) {
            Hub = ChapterHub.Open(this);
            RunAnalytics.Screen("hub");
        } else {
            Build(chapter);
        }
        RunAnalytics.Screen("chapter_" + (Chapter + 1));
        RunAnalytics.CampaignStart(Chapter, CampaignSave.Scene);
    }

    // The chapter card for chapter index, from the board (replays included) or the card's own Hub button.
    public void ShowCard(int index) {
        Chapter = Mathf.Clamp(index, 0, CampaignSave.ChapterCount - 1);
        Story.Chapter chapter = Story.Chapters[Chapter];
        Title = chapter.Title;
        LineCount = chapter.Lines.Length;
        shown = 0;
        LinesShown = 0;
        cardAt = Time.unscaledTime;
        if (Hub != null) {
            Hub.SetShown(false);
        }
        Build(chapter);
        RunAnalytics.Screen("chapter_" + (Chapter + 1));
    }

    // Back to the hub from a card (builds it the first time).
    public void ShowHub() {
        if (cardCanvas != null) {
            Destroy(cardCanvas);
            cardCanvas = null;
            lines = null;
            BeginButton = null;
            MenuButton = null;
            HubButton = null;
        }
        if (Hub == null) {
            Hub = ChapterHub.Open(this);
        }
        Hub.SetShown(true);
        RunAnalytics.Screen("hub");
    }

    public void LeaveToMenu() {
        ToMenu();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        Leave();
    }

    void Update() {
        Open = Time.unscaledTime - cardAt;
        if (cardCanvas == null || lines == null) {
            return;
        }
        int want = Mathf.Min(LineCount, 1 + Mathf.FloorToInt((Open - 1.2f) / LineEvery));
        while (shown < want) {
            lines[shown].gameObject.SetActive(true);
            shown++;
            LinesShown = shown;
            GameAudio.Play("paper", 0.5f);
        }
        if (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter)) {
            BeginChapter();
        }
        if (PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.M)) {
            if (Hub != null && Hub.Shown == false && PlayInput.GetKeyDown(KeyCode.Escape) && CampaignSave.Get(CampaignSave.Active).cleared > 0) {
                ShowHub();
            } else {
                ToMenu();
            }
        }
    }

    // Saves the time on the books once.
    void Leave() {
        if (leaving) {
            return;
        }
        leaving = true;
        CampaignSave.Record(Time.unscaledTime - startedAt);
        if (!handoff) {
            CampaignSave.Close();
        }
    }

    // Plays the chapter: the file stays open for the game scene.
    void BeginChapter() {
        if (handoff) {
            return;
        }
        handoff = true;
        Leave();
        GameAudio.Play("stamp", 0.6f);
        RunAnalytics.Screen("chapter_start_" + (Chapter + 1));
        ChapterRun.Begin(Chapter);
    }

    void ToMenu() {
        Leave();
        RunAnalytics.Screen("menu");
        SceneManager.LoadScene(MetaProgress.MenuScene);
    }

    void Build(Story.Chapter chapter) {
        var canvasGo = new GameObject("Chapter Canvas");
        cardCanvas = canvasGo;
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var column = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
        column.SetParent(canvasGo.transform, false);
        column.anchorMin = new Vector2(0.5f, 0.5f);
        column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(1000f, 640f);
        float scale = 1f;
        if (UiFit.Compact) {
            Vector2 size = UiFit.Size;
            scale = Mathf.Min(1f, (size.x - 40f) / 1000f, (size.y - 40f) / 640f);
        }
        column.localScale = new Vector3(scale, scale, 1f);

        Label(column, "CASE FILE " + (CampaignSave.Active + 1), 26, FontStyle.Bold, new Color(0.66f, 0.72f, 0.84f), 0f, 0f, 36f);
        Label(column, chapter.Title, 64, FontStyle.Bold, new Color(1f, 0.82f, 0.38f), 0f, 44f, 90f);
        Label(column, chapter.Place, 30, FontStyle.Italic, new Color(0.87f, 0.91f, 1f), 0f, 140f, 44f);

        lines = new Text[chapter.Lines.Length];
        for (int i = 0; i < lines.Length; i++) {
            string raw = chapter.Lines[i];
            char who = raw[0];
            string body = raw.Substring(2);
            string name = who == 'W' ? "Wallit" : who == 'S' ? "Slice" : who == 'Y' ? "Yo Bot" : "";
            Color tint = who == 'W' ? new Color(1f, 0.82f, 0.38f) : who == 'S' ? new Color(0.62f, 0.95f, 0.86f) : new Color(0.87f, 0.91f, 1f);
            lines[i] = Label(column, name.Length > 0 ? name + ": \"" + body + "\"" : body, 32, FontStyle.BoldAndItalic, tint, 0f, 200f + i * 62f, 56f);
            lines[i].gameObject.SetActive(false);
        }

        if (CampaignSave.Get(CampaignSave.Active).cleared > 0) {
            // A file with chapters behind it: the hub (the board of cleared chapters) is one press away.
            BeginButton = MakeButton(column, "Begin", 280f, 88f, -300f, 540f, BeginChapter);
            HubButton = MakeButton(column, "Hub", 280f, 88f, 0f, 540f, ShowHub);
            MenuButton = MakeButton(column, "Menu", 280f, 88f, 300f, 540f, ToMenu);
        } else {
            BeginButton = MakeButton(column, "Begin", 300f, 88f, -170f, 540f, BeginChapter);
            MenuButton = MakeButton(column, "Menu", 260f, 88f, 170f, 540f, ToMenu);
        }
    }

    internal Text Label(RectTransform parent, string value, int size, FontStyle style, Color color, float x, float y, float h) {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Min(size, 20);
        text.resizeTextMaxSize = size;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(980f, h);
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        return text;
    }

    internal Button MakeButton(RectTransform parent, string label, float w, float h, float x, float y, UnityEngine.Events.UnityAction onClick) {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, 0.6f);
        image.color = new Color(0.13f, 0.14f, 0.2f, 1f);
        var edge = go.AddComponent<Outline>();
        edge.effectColor = new Color(0.8f, 0.63f, 0.33f, 0.7f);
        edge.effectDistance = new Vector2(2f, -2f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(onClick);
        MenuButtonFx.Add(button);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        Text text = Label(rect, label, 36, FontStyle.Bold, new Color(0.97f, 0.94f, 0.86f), 0f, 0f, h);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}
