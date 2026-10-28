using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows the story (Story.cs, Docs/Story.md): the intro and the finale as pages over the frozen game, the ending
// screen, and the short beats at wave starts. Any key or tap is the next page; Enter or Skip leaves the pages.
public class StoryScreen : MonoBehaviour {
    static StoryScreen instance;

    // Playtests that are not about the story set this so the intro does not wait for a key.
    public static bool SuppressIntro;

    public static bool IsOpen { get { return instance != null && instance.mode != Mode.None; } }
    public static bool EndingOpen { get { return instance != null && instance.mode == Mode.Ending; } }
    public static int IntroShown { get; private set; }
    public static int BeatsShown { get; private set; }
    public static int FinaleShown { get; private set; }
    public static int EndingsShown { get; private set; }
    public static readonly List<string> Events = new List<string>();

    public Button SkipButton { get; private set; }
    public Button KeepButton { get; private set; }
    public Button MenuButton { get; private set; }
    public string PageText { get { return lineText != null ? lineText.text : ""; } }
    public static StoryScreen Instance { get { return instance; } }

    enum Mode { None, Intro, Finale, Ending }

    const float PageGap = 0.3f;
    const float BeatSeconds = 2.4f;

    Mode mode = Mode.None;
    string[] pages;
    int page;
    float readyAt;
    float savedScale = 1f;
    Action<bool> onClose;

    GameObject root;
    Text nameText;
    Text lineText;
    Text hintText;
    Text titleText;
    GameObject pagePanel;
    GameObject endPanel;
    Text endBody;
    Text beatText;
    CanvasScaler scaler;
    int stamp = -1;
    Font font;

    readonly Queue<string> beats = new Queue<string>();
    float beatLeft;
    int beatWave;

    public static StoryScreen Ensure() {
        if (instance == null) {
            var go = new GameObject("Story");
            instance = go.AddComponent<StoryScreen>();
        }
        return instance;
    }

    void Awake() {
        instance = this;
        Events.Clear();
        IntroShown = 0;
        BeatsShown = 0;
        FinaleShown = 0;
        EndingsShown = 0;
        Build();
    }

    void OnDestroy() {
        if (instance == this) {
            instance = null;
            if (mode != Mode.None) {
                Time.timeScale = 1f;
            }
        }
    }

    // ---------------------------------------------------------------- public

    // The intro, at a run that starts on wave 1. done runs when it is finished or skipped.
    public void ShowIntro(Action done) {
        if (SuppressIntro) {
            Events.Add("intro|suppressed");
            done?.Invoke();
            return;
        }
        IntroShown++;
        Open(Mode.Intro, Story.Intro, _ => done?.Invoke());
    }

    // The finale pages, then the ending screen. closed(true) keeps going into overtime, false leaves for the menu.
    public void ShowFinale(string stats, Action<bool> closed) {
        FinaleShown++;
        endBody.text = Story.EndingLine + "\n\n" + stats;
        Open(Mode.Finale, Story.Finale, _ => ShowEnding(closed));
    }

    // Short beats at a wave's start. A new wave drops the ones still waiting.
    public void StartWave(int wave) {
        beats.Clear();
        beatLeft = 0f;
        beatWave = wave;
        foreach (string line in Story.BeatFor(wave)) {
            beats.Enqueue(line);
        }
    }

    // ---------------------------------------------------------------- pages

    void Open(Mode next, string[] list, Action<bool> closed) {
        mode = next;
        pages = list;
        page = 0;
        onClose = closed;
        savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        root.SetActive(true);
        pagePanel.SetActive(true);
        endPanel.SetActive(false);
        SkipButton.gameObject.SetActive(true);
        beatText.enabled = false;
        beats.Clear();
        ShowPage();
        string kind = next == Mode.Intro ? "intro" : "finale";
        Events.Add(kind + "|open");
        RunAnalytics.Screen(kind);
    }

    void ShowPage() {
        string line = pages[page];
        string speaker = Story.Speaker(line);
        nameText.text = Story.Name(speaker);
        nameText.color = SpeakerColor(speaker);
        lineText.text = Story.Text(line);
        lineText.fontStyle = speaker == "N" ? FontStyle.Bold : FontStyle.Normal;
        SetHint();
        readyAt = Time.unscaledTime + PageGap;
        Events.Add((mode == Mode.Intro ? "intro" : "finale") + "-page|" + (page + 1) + "|" + speaker + "|" + Story.Text(line).Replace('\n', ' '));
    }

    void SetHint() {
        if (pages == null || hintText == null) {
            return;
        }
        string count = (page + 1) + " / " + pages.Length;
        if (UiFit.Touch) {
            hintText.text = count + (UiFit.Mode == UiMode.Tall ? "\n" : "     ") + "Tap to continue   ·   Tap Skip to skip";
        } else {
            hintText.text = count + "     Any key: next   ·   Enter: skip";
        }
    }

    // A tap on the Skip button belongs to the button, not to "next page".
    bool TapOnSkip() {
        Vector2 at = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        return SkipButton != null && SkipButton.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)SkipButton.transform, at, null);
    }

    void Next() {
        if (page + 1 < pages.Length) {
            page++;
            ShowPage();
        } else {
            Finish();
        }
    }

    void Finish(bool skipped = false) {
        string kind = mode == Mode.Intro ? "intro" : "finale";
        Events.Add(kind + "|" + (skipped ? "skipped" : "done"));
        Action<bool> closed = onClose;
        onClose = null;
        if (mode == Mode.Intro) {
            Close();
        }
        closed?.Invoke(true);
    }

    void Close() {
        mode = Mode.None;
        root.SetActive(false);
        Time.timeScale = savedScale;
    }

    void ShowEnding(Action<bool> closed) {
        mode = Mode.Ending;
        EndingsShown++;
        onClose = closed;
        pagePanel.SetActive(false);
        endPanel.SetActive(true);
        readyAt = Time.unscaledTime + 0.5f;
        Events.Add("ending|open");
        RunAnalytics.Screen("ending");
    }

    void Leave(bool keepGoing) {
        if (mode != Mode.Ending) {
            return;
        }
        Events.Add("ending|" + (keepGoing ? "overtime" : "menu"));
        Action<bool> closed = onClose;
        onClose = null;
        Close();
        closed?.Invoke(keepGoing);
    }

    static Color SpeakerColor(string speaker) {
        switch (speaker) {
            case "W": return new Color(0.95f, 0.8f, 0.4f);
            case "S": return new Color(0.86f, 0.9f, 1f);
            case "Y": return new Color(0.55f, 1f, 0.7f);
            default: return new Color(1f, 0.92f, 0.72f);
        }
    }

    // ---------------------------------------------------------------- tick

    void Update() {
        if (stamp != UiFit.Stamp && scaler != null) {
            stamp = UiFit.Stamp;
            UiFit.Apply(scaler);
            Layout();
        }
        UiFit.Notice();
        if (mode == Mode.None) {
            TickBeats();
            return;
        }
        Time.timeScale = 0f;
        if (Time.unscaledTime < readyAt) {
            return;
        }
        bool enter = PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter);
        if (mode == Mode.Ending) {
            if (enter) {
                Leave(true);
            } else if (PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.Space)) {
                Leave(false);
            }
            return;
        }
        if (enter) {
            Finish(true);
        } else if (PlayInput.AnyKeyDown) {
            if (UiFit.Touch && TapOnSkip()) {
                return;
            }
            Next();
        }
    }

    void TickBeats() {
        if (beatText == null) {
            return;
        }
        if (PauseMenu.IsOpen || LevelUpScreen.IsOpen) {
            return;
        }
        beatLeft -= Time.deltaTime;
        if (beatLeft > 0f) {
            return;
        }
        beatText.enabled = false;
        if (beats.Count == 0) {
            return;
        }
        string line = beats.Dequeue();
        string speaker = Story.Speaker(line);
        if (speaker == "T") {
            UpgradeLook.Say(Story.Text(line), 2.6f, 300f);
            beatLeft = 1.2f;
            Events.Add("beat|" + beatWave + "|T|" + Story.Text(line));
            BeatsShown++;
            return;
        }
        beatText.enabled = true;
        beatText.color = SpeakerColor(speaker);
        beatText.text = Story.Name(speaker) + "  " + Story.Text(line);
        beatLeft = BeatSeconds;
        BeatsShown++;
        Events.Add("beat|" + beatWave + "|" + speaker + "|" + Story.Text(line));
        if (speaker == "W") {
            WallitVoice.OnBeat(Story.Text(line));
        }
    }

    // ---------------------------------------------------------------- build

    void Build() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("Story Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();
        stamp = UiFit.Stamp;
        UiFit.Apply(scaler);

        // Beats live outside the pages root so they show during play.
        beatText = MakeText(canvasGo.transform, "", 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        Anchor(beatText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(1100f, 50f));
        beatText.enabled = false;

        root = new GameObject("Pages");
        root.transform.SetParent(canvasGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.62f);
        var rr = dim.rectTransform;
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;

        pagePanel = Panel(root.transform, "Page", new Vector2(1300f, 460f));
        nameText = MakeText(pagePanel.transform, "", 40, FontStyle.Bold, TextAnchor.UpperCenter);
        Anchor(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1100f, 52f));
        lineText = MakeText(pagePanel.transform, "", 44, FontStyle.Normal, TextAnchor.MiddleCenter);
        Anchor(lineText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1160f, 260f));
        hintText = MakeText(pagePanel.transform, "", 24, FontStyle.Normal, TextAnchor.LowerCenter);
        hintText.color = new Color(1f, 0.97f, 0.9f, 0.7f);
        Anchor(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1000f, 36f));
        hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
        SkipButton = MakeButton(pagePanel.transform, "Skip", "Skip", new Vector2(190f, 64f), 28, () => {
            if (mode == Mode.Intro || mode == Mode.Finale) {
                Finish(true);
            }
        });
        Anchor((RectTransform)SkipButton.transform, new Vector2(1f, 0f), new Vector2(-130f, 60f), new Vector2(190f, 64f));

        endPanel = Panel(root.transform, "Ending", new Vector2(1000f, 640f));
        titleText = MakeText(endPanel.transform, Story.EndingTitle, 72, FontStyle.Bold, TextAnchor.UpperCenter);
        titleText.color = new Color(1f, 0.86f, 0.4f);
        Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 100f));
        endBody = MakeText(endPanel.transform, "", 32, FontStyle.Normal, TextAnchor.UpperCenter);
        Anchor(endBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(880f, 280f));
        KeepButton = MakeButton(endPanel.transform, "Keep going", "Keep going (Enter)", new Vector2(380f, 84f), 30, () => Leave(true));
        Anchor((RectTransform)KeepButton.transform, new Vector2(0.5f, 0f), new Vector2(-210f, 60f), new Vector2(380f, 84f));
        KeepButton.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.22f, 1f);
        MenuButton = MakeButton(endPanel.transform, "Menu", "Menu (Esc)", new Vector2(380f, 84f), 30, () => Leave(false));
        Anchor((RectTransform)MenuButton.transform, new Vector2(0.5f, 0f), new Vector2(210f, 60f), new Vector2(380f, 84f));
        root.SetActive(false);
        Layout();
    }

    // Desktop keeps the 1920x1080 numbers. Phones (Wide and Tall) fit the 660-unit short side: 20+ unit text, 88 unit buttons.
    void Layout() {
        UiMode ui = UiFit.Mode;
        Vector2 size = UiFit.Size;
        var pageRect = (RectTransform)pagePanel.transform;
        var endRect = (RectTransform)endPanel.transform;
        var skipRect = (RectTransform)SkipButton.transform;
        var keepRect = (RectTransform)KeepButton.transform;
        var menuRect = (RectTransform)MenuButton.transform;
        Text skipLabel = SkipButton.GetComponentInChildren<Text>();
        Text keepLabel = KeepButton.GetComponentInChildren<Text>();
        Text menuLabel = MenuButton.GetComponentInChildren<Text>();
        bool touch = UiFit.Touch;
        if (ui == UiMode.Desktop) {
            pageRect.sizeDelta = new Vector2(1300f, 460f);
            SetText(nameText, 40, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1100f, 52f));
            SetText(lineText, 44, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1160f, 260f));
            SetText(hintText, 24, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1000f, 36f));
            Anchor(skipRect, new Vector2(1f, 0f), new Vector2(-130f, 60f), new Vector2(190f, 64f));
            skipLabel.fontSize = 28;
            endRect.sizeDelta = new Vector2(1000f, 640f);
            SetText(titleText, 72, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 100f));
            SetText(endBody, 32, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(880f, 280f));
            Anchor(keepRect, new Vector2(0.5f, 0f), new Vector2(-210f, 60f), new Vector2(380f, 84f));
            Anchor(menuRect, new Vector2(0.5f, 0f), new Vector2(210f, 60f), new Vector2(380f, 84f));
            keepLabel.fontSize = 30;
            menuLabel.fontSize = 30;
        } else if (ui == UiMode.Wide) {
            float pw = Mathf.Min(1300f, size.x - 40f);
            pageRect.sizeDelta = new Vector2(pw, 580f);
            SetText(nameText, 32, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(pw - 60f, 42f));
            SetText(lineText, 38, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(pw - 80f, 320f));
            SetText(hintText, 22, new Vector2(0f, 0f), new Vector2(30f, 36f), new Vector2(pw - 340f, 60f));
            hintText.alignment = TextAnchor.LowerLeft;
            hintText.rectTransform.pivot = new Vector2(0f, 0f);
            Anchor(skipRect, new Vector2(1f, 0f), new Vector2(-150f, 24f + 44f), new Vector2(240f, 88f));
            skipLabel.fontSize = 34;
            float ew = Mathf.Min(1000f, size.x - 40f);
            endRect.sizeDelta = new Vector2(ew, 580f);
            SetText(titleText, 52, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(ew - 60f, 70f));
            SetText(endBody, 26, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(ew - 80f, 300f));
            float bw = Mathf.Min(380f, (ew - 80f) / 2f);
            Anchor(keepRect, new Vector2(0.5f, 0f), new Vector2(-(bw / 2f + 10f), 24f + 44f), new Vector2(bw, 88f));
            Anchor(menuRect, new Vector2(0.5f, 0f), new Vector2(bw / 2f + 10f, 24f + 44f), new Vector2(bw, 88f));
            keepLabel.fontSize = 28;
            menuLabel.fontSize = 28;
        } else {
            float pw = Mathf.Min(620f, size.x - 20f);
            float ph = Mathf.Min(900f, size.y - 120f);
            pageRect.sizeDelta = new Vector2(pw, ph);
            SetText(nameText, 32, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(pw - 40f, 42f));
            SetText(lineText, 36, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(pw - 50f, ph - 340f));
            SetText(hintText, 22, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(pw - 40f, 70f));
            Anchor(skipRect, new Vector2(0.5f, 0f), new Vector2(0f, 24f + 44f), new Vector2(Mathf.Min(300f, pw - 40f), 88f));
            skipLabel.fontSize = 34;
            float ew = Mathf.Min(620f, size.x - 20f);
            endRect.sizeDelta = new Vector2(ew, Mathf.Min(820f, size.y - 120f));
            SetText(titleText, 48, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(ew - 40f, 64f));
            SetText(endBody, 26, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(ew - 50f, endRect.sizeDelta.y - 330f));
            float bw = Mathf.Min(380f, ew - 40f);
            Anchor(keepRect, new Vector2(0.5f, 0f), new Vector2(0f, 24f + 44f + 100f), new Vector2(bw, 88f));
            Anchor(menuRect, new Vector2(0.5f, 0f), new Vector2(0f, 24f + 44f), new Vector2(bw, 88f));
            keepLabel.fontSize = 30;
            menuLabel.fontSize = 30;
        }
        if (ui == UiMode.Desktop) {
            hintText.alignment = TextAnchor.LowerCenter;
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
        } else if (ui == UiMode.Tall) {
            hintText.alignment = TextAnchor.LowerCenter;
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
        }
        skipLabel.text = "Skip";
        keepLabel.text = touch ? "Keep going" : "Keep going (Enter)";
        menuLabel.text = touch ? "Menu" : "Menu (Esc)";
        SetHint();
    }

    static void SetText(Text text, int size, Vector2 anchor, Vector2 position, Vector2 box) {
        text.fontSize = size;
        Anchor(text.rectTransform, anchor, position, box);
    }

    GameObject Panel(Transform parent, string name, Vector2 size) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image);
        image.color = new Color(0.05f, 0.04f, 0.06f, 0.9f);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return go;
    }

    // Pivot at the anchor's edge so positions are offsets from the middle of the edge they sit on.
    static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    Text MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor align) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = new Color(1f, 0.97f, 0.9f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
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
        Text text = MakeText(go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
        var tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return button;
    }
}
