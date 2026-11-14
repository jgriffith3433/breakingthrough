using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pause during a run: P or Escape, the pause button by the musician's bar, or the mouse leaving the game (Escape
// in a browser frees the mouse before the game hears it). The world stops like it does for a level-up card.
// Resume, open Settings (volume and how often the voices speak) or quit to the menu from here. Works in Arcade and in
// every chapter (chapters play in the game scene). Keyboard and gamepad: MenuNav, with Start / P / Esc / B to resume.
public class PauseMenu : MonoBehaviour {
    const float ButtonSize = 84f;

    public static bool IsOpen { get; private set; }
    // Playtest hook: pretends the browser freed the mouse this frame. MouseLeftIgnored counts the ones a cinematic refused.
    public static bool MouseLeftProbe;
    public static int MouseLeftIgnored;
    // The click that resumed should not also loose an arrow.
    public static bool HoldFire { get; private set; }

    static PauseMenu instance;

    // For the playtest bot.
    public static PauseMenu Instance {
        get { return instance; }
    }
    public Button PauseButton { get; private set; }
    public RectTransform Deck { get { return deck; } }
    public Button ResumeButton { get; private set; }
    public Button SettingsButton { get; private set; }
    public Button JournalButton { get; private set; }
    public Button BuildButton { get; private set; }
    public Button QuitButton { get; private set; }
    public Button SkipSceneButton { get; private set; }
    public Button RestartButton { get; private set; }
    // The frame a pause closed, so the Escape that closed it does not also act in the hub or the level under it.
    public static int ClosedFrame { get; private set; } = -1;
    public static int SceneSkips { get; private set; }
    public static int Restarts { get; private set; }

    Font font;
    CanvasScaler hudScaler;
    CanvasScaler menuScaler;
    GameObject root;
    RectTransform deck;
    RectTransform pauseRect;
    Text hint;
    Text quitLabel;
    bool quitArmed;
    bool restartArmed;
    Text restartLabel;
    bool wasLocked;
    int laidStamp = -1;
    Rect laidSafe;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern int HawkesPointerLocked();
#else
    static int HawkesPointerLocked() {
        return -1;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        IsOpen = false;
        HoldFire = false;
        // The game scene (Arcade and the chapter runs), the campaign's own scene (the hub) and the levels.
        string scene = SceneManager.GetActiveScene().name;
        if (scene != MetaProgress.GameScene && scene != CampaignSave.Scene && !scene.StartsWith("Level_")) {
            return;
        }
        if (FindAnyObjectByType<PauseMenu>() != null) {
            return;
        }
        new GameObject("Pause Menu").AddComponent<PauseMenu>();
    }

    // Nothing to pause during a level-up card, after the musician falls, or before the run is set up.
    static bool CanPause {
        get { return (WaveDirector.Instance != null && !WaveDirector.IsLost && !LevelUpScreen.IsOpen && !StoryScreen.IsOpen) || InHub || InLevel; }
    }

    // The hub: its yard is up (not the board or a chapter card) and the journal is closed.
    public static bool InHub {
        get {
            ChapterHub hub = ChapterHub.Instance;
            return hub != null && hub.Shown && !hub.BoardOpen && !(ChapterScene.Instance != null && ChapterScene.Instance.CardUp) && !JournalPanel.IsOpen;
        }
    }

    // A level (Chapter 1): until it is finished or on its way out.
    public static bool InLevel {
        get { return LevelScene.Instance != null && !LevelScene.Instance.Leaving && !LevelScene.Instance.Completed; }
    }

    void Awake() {
        instance = this;
    }

    void Start() {
        font = UiSkin.Font(UiSkin.FontRole.Body);
        Build();
        root.SetActive(false);
    }

    void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
        if (IsOpen) {
            IsOpen = false;
            Time.timeScale = 1f;
        }
    }

    public static void Open() {
        if (IsOpen || instance == null || instance.root == null || !CanPause) {
            return;
        }
        IsOpen = true;
        Time.timeScale = 0f;
        instance.root.SetActive(true);
        instance.hint.text = UiFit.Touch ? "" : "P, Esc or pad Start resumes.";
        instance.DisarmQuit();
        instance.ArrangeButtons();
        PanelFx.Show(instance.root, "pause", instance.deck);
        RunAnalytics.Screen("pause");
    }

    public static void Resume() {
        if (!IsOpen) {
            return;
        }
        SettingsPanel.Close();
        BuildPanel.Close();
        IsOpen = false;
        ClosedFrame = Time.frameCount;
        HoldFire = true;
        if (instance != null) {
            PanelFx.Hide(instance.root, "pause");
        }
        Time.timeScale = WaveDirector.IsLost ? 0f : 1f;
        RunAnalytics.Screen(WaveDirector.IsLost ? "game_over" : "wave");
    }

    // The campaign's journal, only in a chapter. It sits between Resume and Settings, so the deck is taller there.
    void OpenJournal() {
        if (ChapterRun.Instance != null) {
            JournalPanel.Open(CampaignSave.Active);
        }
    }

    void ArrangeButtons() {
        bool chapter = ChapterRun.Instance != null && CampaignSave.Active >= 0;
        // Resume, then Skip scene while a page reel or the in-engine Chapter 1 opening plays, Journal, Restart, Settings, Quit.
        SkipSceneButton.gameObject.SetActive((chapter && Cutscene.PageOpen) || (InLevel && CinematicDirector.IsOpen));
        JournalButton.gameObject.SetActive(chapter);
        BuildButton.gameObject.SetActive(WaveDirector.Instance != null && BuildList.Held().Count > 0);
        RestartButton.gameObject.SetActive(chapter && !ChapterRun.Instance.Completed);
        DisarmRestart();
        float y = -264f;
        foreach (Button b in new[] { SkipSceneButton, JournalButton, BuildButton, RestartButton, SettingsButton, QuitButton }) {
            if (!b.gameObject.activeSelf) {
                continue;
            }
            Place((RectTransform)b.transform, new Vector2(0f, y), new Vector2(320f, 88f));
            y -= 108f;
        }
        Place(hint.rectTransform, new Vector2(0f, y - 10f), new Vector2(460f, 32f));
        deck.sizeDelta = new Vector2(520f, -y + 90f);
        Layout();
    }

    // Every boon held, with level and effect (BuildPanel.cs). Only offered once the run holds one.
    void OpenBuild() {
        BuildPanel.Open();
    }

    void OpenSettings() {
        SettingsPanel.Open(null);
    }

    void DisarmQuit() {
        quitArmed = false;
        if (quitLabel != null) {
            quitLabel.text = "Quit to menu";
        }
    }

    void DisarmRestart() {
        restartArmed = false;
        if (restartLabel != null) {
            restartLabel.text = "Restart chapter";
        }
    }

    // The scene under the pause is over: the same as its Skip button, then play goes on.
    void SkipScene() {
        if (CinematicDirector.IsOpen) {
            SceneSkips++;
            Resume();
            CinematicDirector.Instance.Skip();
            return;
        }
        if (Cutscene.Instance == null || !Cutscene.PageOpen) {
            return;
        }
        SceneSkips++;
        Resume();
        Cutscene.Instance.SkipFromPause();
    }

    // Starts the chapter again from its opening (the file keeps its XP and finds). Asked twice, like Quit.
    void RestartChapter() {
        if (ChapterRun.Instance == null) {
            return;
        }
        if (!restartArmed) {
            restartArmed = true;
            restartLabel.text = "Sure? Press again";
            return;
        }
        Restarts++;
        SettingsPanel.Close();
        IsOpen = false;
        Time.timeScale = 1f;
        if (root != null) {
            root.SetActive(false);
        }
        RunAnalytics.Screen("chapter_restart");
        ChapterRun.Instance.Restart();
    }

    // Asks twice, like Erase on the case files: the run on the table is lost.
    void Quit() {
        if (!quitArmed) {
            quitArmed = true;
            quitLabel.text = "Sure? Press again";
            return;
        }
        QuitToMenu();
    }

    public static int Quits { get; private set; }

    public static void QuitToMenu() {
        Quits++;
        SettingsPanel.Close();
        IsOpen = false;
        Time.timeScale = 1f;
        if (instance != null && instance.root != null) {
            instance.root.SetActive(false);
        }
        RunAnalytics.Screen("menu");
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.Abandon();
        } else if (ChapterHub.Instance != null && ChapterScene.Instance != null) {
            ChapterScene.Instance.LeaveToMenu();
        } else {
            ScreenTransition.Load(ScreenTransition.Kind.Returned, MetaProgress.MenuScene);
        }
    }

    void Update() {
        if (HoldFire && !PlayInput.GetMouseButton(0)) {
            HoldFire = false;
        }
        if (root == null) {
            return;
        }
        if (laidStamp != UiFit.Stamp || Screen.safeArea != laidSafe) {
            Layout();
        }

        // Escape in a browser frees a locked mouse without the game hearing the key: take that as a pause.
        int locked = HawkesPointerLocked();
        bool lostMouse = wasLocked && locked == 0 && Cursor.lockState == CursorLockMode.Locked;
        if (MouseLeftProbe) {
            MouseLeftProbe = false;
            lostMouse = true;
        }
        // A cinematic or the credits owns the screen: the mouse leaving the page never pauses it (P and Start still do,
        // and the sequence holds its own clocks while paused).
        if (CinematicDirector.IsOpen || Cutscene.IsOpen) {
            if (lostMouse) {
                MouseLeftIgnored++;
            }
            lostMouse = false;
        }
        wasLocked = locked == 1;

        bool settings = SettingsPanel.IsOpen || SettingsPanel.ClosedFrame == Time.frameCount || JournalPanel.IsOpen || JournalPanel.ClosedFrame == Time.frameCount || BuildPanel.IsOpen || BuildPanel.ClosedFrame == Time.frameCount;
        bool own = ChapterHub.Instance != null || LevelScene.Instance != null;   // Esc leaves the hub and the level, so only P and Start pause there
        bool key = PlayInput.GetKeyDown(KeyCode.P) || MenuNav.PadStart || (MenuNav.Back && (IsOpen || !own));

        if (IsOpen) {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!CanPauseWhileOpen()) {
                Resume();
            } else if (key && !settings) {
                Resume();
            }
        } else if (CanPause && !settings && Cutscene.ClosedFrame != Time.frameCount && (key || lostMouse)) {
            Open();
        }

        bool showButton = !IsOpen && CanPause && !Cutscene.IsOpen && !JournalPanel.IsOpen;
        if (PauseButton.gameObject.activeSelf != showButton) {
            PauseButton.gameObject.SetActive(showButton);
        }
    }

    // A pause ends by itself if the run under it does (a scene change, a wave skip key from tooling).
    static bool CanPauseWhileOpen() {
        return (WaveDirector.Instance != null && !WaveDirector.IsLost) || ChapterHub.Instance != null || LevelScene.Instance != null;
    }

    void Build() {
        // The pause button, in the HUD's layer.
        var hudGo = new GameObject("Pause Button Canvas");
        hudGo.transform.SetParent(transform, false);
        var hudCanvas = hudGo.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 200;
        hudScaler = hudGo.AddComponent<CanvasScaler>();
        UiFit.Apply(hudScaler);
        hudGo.AddComponent<GraphicRaycaster>();
        PauseButton = MakeButton(hudGo.transform, "Pause", "II", new Vector2(ButtonSize, ButtonSize), 34, Open);
        PauseButton.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.72f);
        pauseRect = (RectTransform)PauseButton.transform;

        // The pause screen, over everything but Settings.
        var menuGo = new GameObject("Pause Canvas");
        menuGo.transform.SetParent(transform, false);
        var menuCanvas = menuGo.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 450;
        menuScaler = menuGo.AddComponent<CanvasScaler>();
        UiFit.Apply(menuScaler, 0.5f);
        menuGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(menuGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.66f);
        Stretch(dim.rectTransform);

        var deckGo = new GameObject("Panel");
        deckGo.transform.SetParent(root.transform, false);
        var deckImage = deckGo.AddComponent<Image>();
        UiSkin.Framed(deckImage);
        // With the generated frame the border takes a brass tint and its dark middle carries the text.
        deckImage.color = UiArt.Frame != null ? new Color(0.85f, 0.7f, 0.45f, 1f) : new Color(0.07f, 0.06f, 0.09f, 0.95f);
        deck = deckImage.rectTransform;
        deck.anchorMin = new Vector2(0.5f, 0.5f);
        deck.anchorMax = new Vector2(0.5f, 0.5f);
        deck.pivot = new Vector2(0.5f, 0.5f);
        deck.sizeDelta = new Vector2(520f, 580f);

        Text title = MakeText(deck, "Paused", 52, FontStyle.Bold);
        Place(title.rectTransform, new Vector2(0f, -30f), new Vector2(460f, 70f));
        Text line = MakeText(deck, "Clean. The bots will wait.", 24, FontStyle.Italic);
        line.color = new Color(1f, 0.9f, 0.72f, 0.85f);
        Place(line.rectTransform, new Vector2(0f, -100f), new Vector2(460f, 34f));

        ResumeButton = MakeButton(deck, "Resume", "Resume", new Vector2(320f, 88f), 32, Resume);
        ResumeButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        Place((RectTransform)ResumeButton.transform, new Vector2(0f, -156f), new Vector2(320f, 88f));
        JournalButton = MakeButton(deck, "Journal", "Journal", new Vector2(320f, 88f), 32, OpenJournal);
        Place((RectTransform)JournalButton.transform, new Vector2(0f, -264f), new Vector2(320f, 88f));
        JournalButton.gameObject.SetActive(false);
        BuildButton = MakeButton(deck, "Build", "Build", new Vector2(320f, 88f), 32, OpenBuild);
        BuildButton.gameObject.SetActive(false);
        SkipSceneButton = MakeButton(deck, "Skip scene", "Skip scene", new Vector2(320f, 88f), 32, SkipScene);
        SkipSceneButton.gameObject.SetActive(false);
        RestartButton = MakeButton(deck, "Restart", "Restart chapter", new Vector2(320f, 88f), 32, RestartChapter);
        RestartButton.GetComponent<Image>().color = new Color(0.36f, 0.16f, 0.14f, 1f);
        restartLabel = RestartButton.GetComponentInChildren<Text>();
        RestartButton.gameObject.SetActive(false);
        SettingsButton = MakeButton(deck, "Settings", "Settings", new Vector2(320f, 88f), 32, OpenSettings);
        Place((RectTransform)SettingsButton.transform, new Vector2(0f, -264f), new Vector2(320f, 88f));

        QuitButton = MakeButton(deck, "Quit", "Quit to menu", new Vector2(320f, 88f), 32, Quit);
        QuitButton.GetComponent<Image>().color = new Color(0.36f, 0.16f, 0.14f, 1f);
        Place((RectTransform)QuitButton.transform, new Vector2(0f, -372f), new Vector2(320f, 88f));
        quitLabel = QuitButton.GetComponentInChildren<Text>();

        hint = MakeText(deck, "", 22, FontStyle.Normal);
        hint.color = new Color(1f, 0.96f, 0.88f, 0.7f);
        Place(hint.rectTransform, new Vector2(0f, -490f), new Vector2(460f, 32f));
        MenuNav.Attach(root, 450);
        Layout();
    }

    // The pause button: right of the musician's bar on desktop; in a phone's landscape, between that bar and the
    // bird list; upright, at the right under the wave lines and above the bird list. The panel scales to fit.
    void Layout() {
        laidStamp = UiFit.Stamp;
        UiFit.Apply(hudScaler);
        UiFit.Apply(menuScaler, 0.5f);
        UiMode mode = UiFit.Mode;
        Vector4 safe = UiFit.SafeInsets();
        laidSafe = Screen.safeArea;
        bool own = ChapterHub.Instance != null || LevelScene.Instance != null;
        if (mode == UiMode.Desktop) {
            pauseRect.anchorMin = new Vector2(0.5f, 1f);
            pauseRect.anchorMax = new Vector2(0.5f, 1f);
            pauseRect.pivot = new Vector2(0f, 1f);
            pauseRect.sizeDelta = new Vector2(88f, 88f);
            pauseRect.anchoredPosition = new Vector2(340f, -20f);
        } else {
            pauseRect.anchorMin = new Vector2(1f, 1f);
            pauseRect.anchorMax = new Vector2(1f, 1f);
            pauseRect.pivot = new Vector2(1f, 1f);
            float btn = mode == UiMode.Wide ? 88f : ButtonSize;
            pauseRect.sizeDelta = new Vector2(btn, btn);
            Vector2 at = mode == UiMode.Wide ? new Vector2(-336f, -12f) : new Vector2(-24f, -256f);
            if (own) {
                // The hub and the levels hang Journal and Menu under the top-right corner: Pause sits left of them.
                at = new Vector2(-132f, -44f);
            }
            pauseRect.anchoredPosition = new Vector2(at.x - safe.y, at.y - safe.z);
        }
        Vector2 size = UiFit.Size;
        float usableW = size.x - safe.x - safe.y;
        float usableH = size.y - safe.z - safe.w;
        float scale = UiFit.Compact ? Mathf.Min(1f, (usableW - 40f) / 520f, (usableH - 40f) / deck.sizeDelta.y) : 1f;
        deck.localScale = new Vector3(scale, scale, 1f);
        // Off a notch: the panel sits in the middle of the safe area.
        deck.anchoredPosition = UiFit.Compact ? new Vector2((safe.x - safe.y) * 0.5f, (safe.w - safe.z) * 0.5f) : Vector2.zero;
    }

    Button MakeButton(Transform parent, string name, string label, Vector2 size, int fontSize, UnityEngine.Events.UnityAction click) {
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
        MenuButtonFx.Add(button);
        Text text = MakeText(go.transform, label, fontSize, FontStyle.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    Text MakeText(Transform parent, string value, int size, FontStyle style) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.97f, 0.9f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    // Top-centered in its parent.
    static void Place(RectTransform rect, Vector2 position, Vector2 size) {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
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
