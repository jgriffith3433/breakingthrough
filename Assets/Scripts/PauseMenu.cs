using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pause during a run: P or Escape, the pause button by the musician's bar, or the mouse leaving the game (Escape
// in a browser frees the mouse before the game hears it). The world stops like it does for a level-up card.
// Resume, or open Settings from here.
public class PauseMenu : MonoBehaviour {
    const float ButtonSize = 84f;

    public static bool IsOpen { get; private set; }
    // The click that resumed should not also loose an arrow.
    public static bool HoldFire { get; private set; }

    static PauseMenu instance;

    // For the playtest bot.
    public static PauseMenu Instance {
        get { return instance; }
    }
    public Button PauseButton { get; private set; }
    public Button ResumeButton { get; private set; }
    public Button SettingsButton { get; private set; }

    Font font;
    CanvasScaler hudScaler;
    CanvasScaler menuScaler;
    GameObject root;
    RectTransform deck;
    RectTransform pauseRect;
    Text hint;
    bool wasLocked;
    int laidStamp = -1;

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
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<PauseMenu>() != null) {
            return;
        }
        new GameObject("Pause Menu").AddComponent<PauseMenu>();
    }

    // Nothing to pause during a level-up card, after the musician falls, or before the run is set up.
    static bool CanPause {
        get { return WaveDirector.Instance != null && !WaveDirector.IsLost && !LevelUpScreen.IsOpen; }
    }

    void Awake() {
        instance = this;
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        instance.hint.text = UiFit.Touch ? "" : "P or Esc resumes.";
        RunAnalytics.Screen("pause");
        GameAudio.Play("shot", 0.35f, 0.7f);
    }

    public static void Resume() {
        if (!IsOpen) {
            return;
        }
        SettingsPanel.Close();
        IsOpen = false;
        HoldFire = true;
        if (instance != null) {
            instance.root.SetActive(false);
        }
        Time.timeScale = WaveDirector.IsLost ? 0f : 1f;
        RunAnalytics.Screen(WaveDirector.IsLost ? "game_over" : "wave");
    }

    void OpenSettings() {
        SettingsPanel.Open(null);
    }

    void Update() {
        if (HoldFire && !PlayInput.GetMouseButton(0)) {
            HoldFire = false;
        }
        if (root == null) {
            return;
        }
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }

        // Escape in a browser frees a locked mouse without the game hearing the key: take that as a pause.
        int locked = HawkesPointerLocked();
        bool lostMouse = wasLocked && locked == 0 && Cursor.lockState == CursorLockMode.Locked;
        wasLocked = locked == 1;

        bool settings = SettingsPanel.IsOpen || SettingsPanel.ClosedFrame == Time.frameCount;
        bool key = PlayInput.GetKeyDown(KeyCode.P) || PlayInput.GetKeyDown(KeyCode.Escape);

        if (IsOpen) {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!CanPauseWhileOpen()) {
                Resume();
            } else if (key && !settings) {
                Resume();
            }
        } else if (CanPause && !settings && (key || lostMouse)) {
            Open();
        }

        bool showButton = !IsOpen && CanPause;
        if (PauseButton.gameObject.activeSelf != showButton) {
            PauseButton.gameObject.SetActive(showButton);
        }
    }

    // A pause ends by itself if the run under it does (a scene change, a wave skip key from tooling).
    static bool CanPauseWhileOpen() {
        return WaveDirector.Instance != null && !WaveDirector.IsLost;
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
        UiSkin.Round(deckImage);
        deckImage.color = new Color(0.07f, 0.06f, 0.09f, 0.95f);
        deck = deckImage.rectTransform;
        deck.anchorMin = new Vector2(0.5f, 0.5f);
        deck.anchorMax = new Vector2(0.5f, 0.5f);
        deck.pivot = new Vector2(0.5f, 0.5f);
        deck.sizeDelta = new Vector2(520f, 470f);

        Text title = MakeText(deck, "Paused", 52, FontStyle.Bold);
        Place(title.rectTransform, new Vector2(0f, -30f), new Vector2(460f, 70f));
        Text line = MakeText(deck, "Clean. The bots will wait.", 24, FontStyle.Italic);
        line.color = new Color(1f, 0.9f, 0.72f, 0.85f);
        Place(line.rectTransform, new Vector2(0f, -100f), new Vector2(460f, 34f));

        ResumeButton = MakeButton(deck, "Resume", "Resume", new Vector2(320f, 88f), 32, Resume);
        ResumeButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        Place((RectTransform)ResumeButton.transform, new Vector2(0f, -156f), new Vector2(320f, 88f));
        SettingsButton = MakeButton(deck, "Settings", "Settings", new Vector2(320f, 88f), 32, OpenSettings);
        Place((RectTransform)SettingsButton.transform, new Vector2(0f, -264f), new Vector2(320f, 88f));

        hint = MakeText(deck, "", 22, FontStyle.Normal);
        hint.color = new Color(1f, 0.96f, 0.88f, 0.7f);
        Place(hint.rectTransform, new Vector2(0f, -384f), new Vector2(460f, 32f));
        Layout();
    }

    // The pause button: right of the musician's bar on desktop; in a phone's landscape, between that bar and the
    // bird list; upright, at the right under the wave lines and above the bird list. The panel scales to fit.
    void Layout() {
        laidStamp = UiFit.Stamp;
        UiFit.Apply(hudScaler);
        UiFit.Apply(menuScaler, 0.5f);
        UiMode mode = UiFit.Mode;
        if (mode == UiMode.Desktop) {
            pauseRect.anchorMin = new Vector2(0.5f, 1f);
            pauseRect.anchorMax = new Vector2(0.5f, 1f);
            pauseRect.pivot = new Vector2(0f, 1f);
            pauseRect.anchoredPosition = new Vector2(340f, -12f);
        } else {
            pauseRect.anchorMin = new Vector2(1f, 1f);
            pauseRect.anchorMax = new Vector2(1f, 1f);
            pauseRect.pivot = new Vector2(1f, 1f);
            pauseRect.anchoredPosition = mode == UiMode.Wide ? new Vector2(-336f, -12f) : new Vector2(-24f, -256f);
        }
        Vector2 size = UiFit.Size;
        float scale = UiFit.Compact ? Mathf.Min(1f, (size.x - 40f) / 520f, (size.y - 40f) / 470f) : 1f;
        deck.localScale = new Vector3(scale, scale, 1f);
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
