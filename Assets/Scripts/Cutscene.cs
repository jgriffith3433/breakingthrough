using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The chapter cutscene player. A scene is data (Story.Chapter.Cutscene, "<speaker>|<text>" lines): a named speaker with a
// portrait slot, typed lines, letterbox bars and a camera that moves to whoever is talking. The world freezes under it
// (Time.timeScale 0, like StoryScreen and the level-up cards) and it blocks input the way they do: IsOpen is checked by
// ChapterRun, the pause menu, the level-up cards, the bat, the fort and the touch controls.
//
// Lines:  N narration (no name), W Wallit, S Slice, Y Yo Bot: a page, typed out.
//         C|wallit, C|slice, C|wide: where the camera looks for the pages after it.
//         B|on, B|off: the letterbox bars slide in or out.
// Keys:   any key, click or tap finishes the typing, then goes to the next page; Enter or the Skip button leaves the scene.
// Voice:  W and S lines go through VoiceDirector ("cutscene" is a Key reason and the only one allowed while a scene is up).
//         Each page cuts the last page's voice before it speaks, so two voices never overlap. A line with no recording
//         (Resources/Voice/<speaker>_<line>.mp3) is the text alone and holds the floor for its estimated length.
// Portraits: Resources/Portraits/<wallit|slice|yobot>.png if the file exists, else a coloured disc with the initial.
[DefaultExecutionOrder(-5000)]
public partial class Cutscene : MonoBehaviour {
    static Cutscene instance;

    public static bool IsOpen { get { return instance != null && instance.open; } }
    public static Cutscene Instance { get { return instance; } }
    public static readonly List<string> Events = new List<string>();

    // Playtest and diagnostics.
    public static int PagesShown { get; private set; }
    public static int VoiceLines { get; private set; }
    public static int Overlaps { get; private set; }
    public static int Skipped { get; private set; }
    public static int Finished { get; private set; }

    public Button SkipButton { get; private set; }
    public string NameShown { get { return nameText != null ? nameText.text : ""; } }
    public string TextShown { get { return lineText != null ? lineText.text : ""; } }
    public string Focus { get; private set; } = "wide";
    public string PortraitKind { get; private set; } = "none";   // art, stand-in, or narration (no slot)
    public bool PortraitVisible { get { return portrait != null && portrait.gameObject.activeSelf; } }
    public string PortraitLetter { get { return portraitLetter != null ? portraitLetter.text : ""; } }
    public Color NameColour { get { return nameText != null ? nameText.color : Color.white; } }
    public float Bars { get { return barsNow; } }
    public bool Typing { get { return typed < full.Length; } }
    public int Page { get { return page; } }
    public int PageCount { get { return pageCount; } }
    public float CameraWeight { get { return weight; } }

    const float TypeSpeed = 48f;      // characters a second
    const float PageGap = 0.25f;
    const float BarFraction = 0.13f;  // of the canvas height

    bool open;
    string[] script;
    int cursor;
    int page;
    int pageCount;
    float readyAt;
    float savedScale = 1f;
    Action done;

    string full = "";
    float typed;
    string speaker = "N";
    bool barsOn;
    float barsNow;
    float weight;
    bool camOn;
    Vector3 naturalPos;
    Quaternion naturalRot;
    Camera cam;
    Vector3 focusPos;
    Quaternion focusRot = Quaternion.identity;
    bool focusSet;

    Font font;
    CanvasScaler scaler;
    int stamp = -1;
    GameObject root;
    RectTransform topBar;
    RectTransform bottomBar;
    RectTransform panel;
    Image portrait;
    Text portraitLetter;
    Text nameText;
    Text lineText;
    Text hintText;

    // Plays a scene over the running game. done runs when it ends, whether it was read through or skipped.
    public static void Play(string[] lines, Action done) {
        if (lines == null || lines.Length == 0) {
            done?.Invoke();
            return;
        }
        if (instance == null) {
            instance = new GameObject("Cutscene").AddComponent<Cutscene>();
        }
        instance.Begin(lines, done);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
        Events.Clear();
        PagesShown = 0;
        VoiceLines = 0;
        Overlaps = 0;
        Skipped = 0;
        Finished = 0;
        ShotsShown = 0;
        ShotsSkipped = 0;
        SubtitleLines = 0;
        DuckedLowest = 1f;
    }

    void Awake() {
        instance = this;
        Build();
        BuildShots();
        BuildChoice();
    }

    void OnDestroy() {
        if (instance == this) {
            if (open) {
                WallitVoice.Silence();
                SliceVoice.Silence();
                VoiceDirector.Scene = false;
                Time.timeScale = savedScale;
            }
            instance = null;
        }
    }

    // ---------------------------------------------------------------- playing

    void Begin(string[] lines, Action onDone) {
        shotMode = false;
        shotRoot.SetActive(false);
        subtitle.gameObject.SetActive(false);
        script = lines;
        cursor = 0;
        page = 0;
        pageCount = 0;
        foreach (string line in lines) {
            string who = Story.Speaker(line);
            if (who != "C" && who != "B" && who != "?") {
                pageCount++;
            }
        }
        done = onDone;
        open = true;
        barsOn = true;
        Focus = "wide";
        savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        root.SetActive(true);
        panel.gameObject.SetActive(true);
        SkipButton.gameObject.SetActive(true);
        Events.Add("open|" + pageCount);
        RunAnalytics.Screen("cutscene");
        NextPage();
    }

    // Reads directives up to the next page, then shows it. Past the last page the scene ends.
    void NextPage() {
        while (cursor < script.Length) {
            string line = Choices.Expand(script[cursor++]);   // a ? line is the plain line, or nothing if its choice went the other way
            if (line == null) {
                continue;
            }
            string who = Story.Speaker(line);
            string arg = Story.Text(line).Trim().ToLowerInvariant();
            if (who == "C") {
                Focus = arg;
                Events.Add("camera|" + arg);
                continue;
            }
            if (who == "B") {
                barsOn = arg != "off";
                Events.Add("bars|" + (barsOn ? "on" : "off"));
                continue;
            }
            if (who == "Q") {
                Choices.Row row = Choices.Get(Story.Text(line).Trim());
                if (row == null || CampaignSave.Active < 0 || Choices.Picked(row.Id) > 0) {
                    continue;   // no such choice, no open file (Arcade), or already made on this file: not asked twice
                }
                ShowChoice(row);
                return;
            }
            ShowPage(who, Story.Text(line));
            return;
        }
        Close(false);
    }

    void ShowPage(string who, string text) {
        page++;
        PagesShown++;
        speaker = who;
        full = text;
        typed = 0f;
        nameText.text = Story.Name(who);
        nameText.color = SpeakerColor(who);
        lineText.text = "";
        lineText.fontStyle = who == "N" ? FontStyle.Bold : FontStyle.Normal;
        SetPortrait(who);
        SetHint();
        readyAt = Time.unscaledTime + PageGap;
        Speak(who, text);
        Events.Add("page|" + page + "|" + who + "|" + text.Replace('\n', ' '));
    }

    // One voice at a time: the last page's voice is cut and the floor freed before this page's line asks the director.
    void Speak(string who, string text) {
        WallitVoice.Silence();
        SliceVoice.Silence();
        if (who != "W" && who != "S") {
            Cast.Spoke(who, text);
            return;
        }
        if (!WallitVoice.FloorFree) {
            Overlaps++;
            Events.Add("overlap|" + who + "|" + text);
            return;
        }
        if (!VoiceDirector.Allow(text, "cutscene", VoiceDirector.Priority.Key)) {
            return;
        }
        float seconds = who == "W" ? WallitVoice.Cutscene(text) : SliceVoice.Cutscene(text);
        VoiceLines++;
        Events.Add("voice|" + who + "|" + seconds.ToString("0.00"));
    }

    void Close(bool skipped) {
        if (!open) {
            return;
        }
        open = false;
        ClosedFrame = Time.frameCount;
        HideChoice();
        WallitVoice.Silence();
        SliceVoice.Silence();
        VoiceDirector.Scene = false;
        barsOn = false;
        Time.timeScale = savedScale;
        panel.gameObject.SetActive(false);
        SkipButton.gameObject.SetActive(false);
        if (shotMode) {
            if (skipped) {
                ShotsSkipped++;
            }
            EndShots();
        }
        if (skipped) {
            Skipped++;
        } else {
            Finished++;
        }
        Events.Add(skipped ? "skipped|" + page + "/" + pageCount : "done|" + page + "/" + pageCount);
        Action finish = done;
        done = null;
        finish?.Invoke();
    }

    // ---------------------------------------------------------------- tick

    void Update() {
        RestoreNatural();
        if (stamp != UiFit.Stamp && scaler != null) {
            stamp = UiFit.Stamp;
            UiFit.Apply(scaler);
            Layout();
        }
        float dt = Time.unscaledDeltaTime;
        TickBed(dt);
        barsNow = Mathf.MoveTowards(barsNow, barsOn ? 1f : 0f, dt * 2.2f);
        SetBars();
        if (!open) {
            return;
        }
        Time.timeScale = 0f;
        if (ComfortTick()) {
            return;
        }
        if (shotMode) {
            LayoutSubtitle();
            TickShots(dt);
            return;
        }
        if (typed < full.Length) {
            typed = Mathf.Min(full.Length, typed + TypeSpeed * dt);
            lineText.text = full.Substring(0, Mathf.FloorToInt(typed));
        }
        if (Time.unscaledTime < readyAt) {
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter) || PadSkip()) {
            Close(true);
            return;
        }
        if (choosing) {
            TickChoice();
            return;
        }
        if (PlayInput.AnyKeyDown || TouchBegan()) {
            if (UiFit.Touch && TapOnSkip()) {
                return;
            }
            Advance();
        }
    }

    // First press finishes the typing, the next one turns the page.
    void Advance() {
        if (typed < full.Length) {
            typed = full.Length;
            lineText.text = full;
            readyAt = Time.unscaledTime + 0.12f;
            return;
        }
        NextPage();
    }

    static bool TouchBegan() {
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
    }

    bool TapOnSkip() {
        Vector2 at = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        return SkipButton != null && SkipButton.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)SkipButton.transform, at, null);
    }

    // ---------------------------------------------------------------- camera

    // The game's camera script moves the camera in Update. Update runs first (execution order) and puts the camera back where
    // that script left it, so its own smoothing never sees the scene's shot; LateUpdate then lays the shot over it.
    void LateUpdate() {
        Shoot();
    }

    void RestoreNatural() {
        if (camOn && cam != null) {
            cam.transform.SetPositionAndRotation(naturalPos, naturalRot);
            camOn = false;
        }
    }

    // After the game's camera has moved: blend from where it put itself to the shot on whoever is talking.
    void Shoot() {
        float target = open ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, target, Time.unscaledDeltaTime * (open ? 1.8f : 1.5f));
        if (ComfortOptions.ReducedMotion) {
            weight = 0f;   // the camera stays where the game left it: no sweep to the speaker
        }
        if (weight <= 0.001f) {
            focusSet = false;
            return;
        }
        if (cam == null) {
            cam = Camera.main;
        }
        if (cam == null) {
            return;
        }
        Transform wallit = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.transform : null;
        Transform slice = Musician.Instance != null ? Musician.Instance.transform : null;
        naturalPos = cam.transform.position;
        naturalRot = cam.transform.rotation;
        camOn = true;
        if (open && ComputeShot(wallit, slice, out Vector3 pos, out Quaternion rot)) {
            if (!focusSet) {
                focusPos = naturalPos;
                focusRot = naturalRot;
                focusSet = true;
            }
            float k = 1f - Mathf.Exp(-4.5f * Time.unscaledDeltaTime);
            focusPos = Vector3.Lerp(focusPos, pos, k);
            focusRot = Quaternion.Slerp(focusRot, rot, k);
        }
        if (!focusSet) {
            return;
        }
        float eased = weight * weight * (3f - 2f * weight);
        cam.transform.SetPositionAndRotation(Vector3.Lerp(naturalPos, focusPos, eased), Quaternion.Slerp(naturalRot, focusRot, eased));
    }

    // The shot for the current focus. A close one sits between the speaker and the other character, a little to the side,
    // so the speaker faces the lens; "wide" takes in both from the side and above.
    bool ComputeShot(Transform wallit, Transform slice, out Vector3 pos, out Quaternion rot) {
        pos = Vector3.zero;
        rot = Quaternion.identity;
        if (wallit == null && slice == null) {
            return false;
        }
        Transform a = Focus == "slice" && slice != null ? slice : wallit != null ? wallit : slice;
        Transform b = a == slice ? wallit : slice;
        Vector3 from = b != null ? b.position : a.position - a.forward * 4f;
        Vector3 along = from - a.position;
        along.y = 0f;
        float distance = along.magnitude;
        along = distance > 0.1f ? along / distance : a.forward;
        Vector3 side = Vector3.Cross(Vector3.up, along);
        float grown = PlayerGrowth.Instance != null ? Mathf.Max(1f, PlayerGrowth.Instance.Scale) : 1f;
        if (Focus == "wide" && b != null) {
            Vector3 mid = (a.position + b.position) * 0.5f;
            float back = Mathf.Max(6f, distance * 0.9f + 4f) * grown;
            pos = mid + side * back + Vector3.up * (3.2f * grown);
            rot = Quaternion.LookRotation(mid + Vector3.up * (1.1f * grown) - pos, Vector3.up);
            return true;
        }
        float lens = Mathf.Min(2.8f, Mathf.Max(1.2f, distance * 0.5f)) * grown;
        Vector3 eye = a.position + Vector3.up * (1.2f * grown);
        pos = eye + along * lens + side * (1.1f * grown) + Vector3.up * (0.25f * grown);
        rot = Quaternion.LookRotation(eye - pos, Vector3.up);
        return true;
    }

    // ---------------------------------------------------------------- build

    static Color SpeakerColor(string who) {
        return Cast.ColourOf(who);
    }

    // Portrait slot: Resources/Portraits/<name>.png when the art exists, else a disc in the speaker's colour with the
    // initial. Narration has no portrait.
    void SetPortrait(string who) {
        bool has = Cast.HasPortrait(who);
        portrait.gameObject.SetActive(has);
        PortraitKind = has ? "none" : "narration";
        if (!has) {
            return;
        }
        Sprite art = Cast.PortraitOf(who);
        if (art != null) {
            portrait.sprite = art;
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            portrait.color = Color.white;
            portraitLetter.text = "";
            PortraitKind = "art";
        } else {
            UiSkin.Round(portrait, 1f);
            Color c = SpeakerColor(who);
            portrait.color = new Color(c.r * 0.38f, c.g * 0.38f, c.b * 0.38f, 1f);
            string glyph = Cast.GlyphOf(who);
            portraitLetter.text = glyph.Length > 0 ? glyph : Story.Name(who).Substring(0, 1);
            portraitLetter.color = c;
            PortraitKind = "stand-in";
        }
    }

    void SetHint() {
        if (hintText == null) {
            return;
        }
        string count = page + " / " + pageCount;
        if (UiFit.Touch) {
            hintText.text = count + (UiFit.Mode == UiMode.Tall ? "\n" : "     ") + "Tap to continue   ·   Tap Skip to skip";
        } else {
            hintText.text = count + (ComfortOptions.PadConnected ? "     A: next   ·   Y: skip   ·   Start: pause" : "     Any key: next   ·   Enter: skip   ·   P: pause");
        }
    }

    void SetBars() {
        if (topBar == null) {
            return;
        }
        float height = UiFit.Size.y * BarFraction * Mathf.SmoothStep(0f, 1f, barsNow);
        topBar.sizeDelta = new Vector2(0f, height);
        bottomBar.sizeDelta = new Vector2(0f, height);
        if (panel != null) {
            Vector2 p = panel.anchoredPosition;
            p.y = height + 16f;
            panel.anchoredPosition = p;
        }
        if (barsNow <= 0.001f && !open) {
            root.SetActive(false);
        }
    }

    void Build() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("Cutscene Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 330;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();
        stamp = UiFit.Stamp;
        UiFit.Apply(scaler);

        root = new GameObject("Scene", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        topBar = Bar("Letterbox Top", 1f);
        bottomBar = Bar("Letterbox Bottom", 0f);

        var panelGo = new GameObject("Dialogue", typeof(RectTransform));
        panelGo.transform.SetParent(root.transform, false);
        var back = panelGo.AddComponent<Image>();
        UiSkin.Round(back);
        back.color = new Color(0.05f, 0.04f, 0.06f, 0.9f);
        panel = back.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);

        var portraitGo = new GameObject("Portrait", typeof(RectTransform));
        portraitGo.transform.SetParent(panel, false);
        portrait = portraitGo.AddComponent<Image>();
        portrait.raycastTarget = false;
        portraitLetter = MakeText(portraitGo.transform, "", 84, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(portraitLetter.rectTransform);
        nameText = MakeText(panel, "", 40, FontStyle.Bold, TextAnchor.UpperLeft);
        lineText = MakeText(panel, "", 44, FontStyle.Normal, TextAnchor.UpperLeft);
        hintText = MakeText(panel, "", 24, FontStyle.Normal, TextAnchor.LowerLeft);
        hintText.color = new Color(1f, 0.97f, 0.9f, 0.7f);

        var skipGo = new GameObject("Skip", typeof(RectTransform));
        skipGo.transform.SetParent(root.transform, false);
        var skipImage = skipGo.AddComponent<Image>();
        UiSkin.Round(skipImage, 0.6f);
        skipImage.color = new Color(0.28f, 0.22f, 0.12f, 1f);
        SkipButton = skipGo.AddComponent<Button>();
        SkipButton.targetGraphic = skipImage;
        SkipButton.onClick.AddListener(() => Close(true));
        MenuButtonFx.Add(SkipButton);
        Text skipLabel = MakeText(skipGo.transform, "Skip", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(skipLabel.rectTransform);

        root.SetActive(false);
        Layout();
    }

    RectTransform Bar(string name, float anchorY) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, anchorY);
        rect.anchorMax = new Vector2(1f, anchorY);
        rect.pivot = new Vector2(0.5f, anchorY);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);
        return rect;
    }

    // Desktop keeps the 1920x1080 numbers. Phones (Wide and Tall) keep text at 20+ units and the button at 88.
    void Layout() {
        UiMode ui = UiFit.Mode;
        Vector2 size = UiFit.Size;
        var skipRect = (RectTransform)SkipButton.transform;
        Text skipLabel = SkipButton.GetComponentInChildren<Text>();
        var portraitRect = portrait.rectTransform;
        float width = Mathf.Min(1500f, size.x - 40f);
        float portraitSize;
        float nameSize;
        float lineSize;
        float height;
        if (ui == UiMode.Desktop) {
            portraitSize = 190f;
            nameSize = 40;
            lineSize = 44;
            height = 270f;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-40f, -30f);
            skipRect.sizeDelta = new Vector2(190f, 64f);
            skipLabel.fontSize = 28;
        } else {
            portraitSize = ui == UiMode.Tall ? 120f : 150f;
            nameSize = 30;
            lineSize = ui == UiMode.Tall ? 30 : 34;
            height = ui == UiMode.Tall ? 380f : 250f;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-16f, -16f);
            skipRect.sizeDelta = new Vector2(240f, 88f);
            skipLabel.fontSize = 34;
        }
        float grow = ComfortOptions.SubtitleScale;
        if (grow > 1f) {
            nameSize = Mathf.Round(nameSize * grow);
            lineSize = Mathf.Round(lineSize * grow);
            height = Mathf.Min(size.y * 0.6f, height * (1f + (grow - 1f) * 1.1f));
        }
        panel.sizeDelta = new Vector2(width, height);
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 1f);
        portraitRect.pivot = new Vector2(0f, 1f);
        portraitRect.anchoredPosition = new Vector2(24f, -24f);
        portraitRect.sizeDelta = new Vector2(portraitSize, portraitSize);
        float textLeft = portraitSize + 56f;
        float textWidth = width - textLeft - 28f;
        Place(nameText, (int)nameSize, textLeft, -22f, textWidth, nameSize + 14f);
        Place(lineText, (int)lineSize, textLeft, -22f - nameSize - 18f, textWidth, height - nameSize - 90f);
        Place(hintText, ui == UiMode.Desktop ? 24 : 22, textLeft, 12f, textWidth, ui == UiMode.Tall ? 64f : 34f);
        hintText.rectTransform.anchorMin = hintText.rectTransform.anchorMax = new Vector2(0f, 0f);
        hintText.rectTransform.pivot = new Vector2(0f, 0f);
        hintText.rectTransform.anchoredPosition = new Vector2(textLeft, 12f);
        SetHint();
        SetBars();
        LayoutChoice();
        ApplyComfort();
    }

    // Top-left anchored text box inside the panel.
    static void Place(Text text, int fontSize, float left, float top, float width, float height) {
        text.fontSize = fontSize;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, top);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    Text MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor align) {
        var go = new GameObject("Text", typeof(RectTransform));
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
}
