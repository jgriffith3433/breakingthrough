using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The chapter-end ceremony (replaces the Chapter Complete stats panel, Chapters 1-5): a case-file dossier slides in over the
// field, four stats type onto the form one by one with a typewriter tick, a big rubber stamp slams the rank (S/A/B/C) with a
// thud, an ink splat and a brass sting that grows with the rank, FILED stamps across the whole sheet, Wallit says one existing
// line chosen by rank, and Continue appears. A tap finishes the step it is in; a second tap skips to Continue. Built in code on
// the card's canvas, on unscaled time (the game is frozen behind the card). Special Elite and Fraunces are the Department's
// faces; the paper and ink are drawn here (no atlas art exists yet, so UiSkin.Rounded and Glow stand in).
// Phone: text is 28 units or more (about 16 px on a 390 wide screen), the stamp is the largest thing on the sheet, and the
// Continue and Menu buttons are 88 units (about 52 px). `ceremony` playtest (Playtest.CeremonyFlow).
public class ChapterCeremony : MonoBehaviour {
    public struct Stat {
        public string Label, Value;
        public Stat(string label, string value) { Label = label; Value = value; }
    }

    // What the playtest reads.
    public static ChapterCeremony Instance { get; private set; }
    public static int StatsTyped, Ticks, Taps, Stamps;
    public static string RankStamped = "", LineSaid = "", StingPlayed = "";
    public static bool FiledShown, ContinueShown, Skipped;
    public static float StartedAt, ContinueAt;

    public Button ContinueButton { get; private set; }
    public Button MenuButton { get; private set; }
    public bool Running { get; private set; }
    public float ContinueHeightPx { get { return ContinueButton == null ? 0f : ((RectTransform)ContinueButton.transform).rect.height * Screen.height / Mathf.Max(1f, UiFit.Size.y); } }
    public Text FirstRowText { get { return rowValues.Count > 0 ? rowValues[0] : null; } }
    public float SmallestTextUnits { get; private set; }
    public GameObject Catcher { get { return catcher != null ? catcher.gameObject : null; } }
    public RectTransform Panel { get { return panel; } }
    public RectTransform StampBox { get { return stampBox; } }

    // The line Wallit says by rank, all from his existing banks (WallitVoice): S a home run, A a clean sweep, B balanced books,
    // C he is charging for it.
    public static string LineFor(int rank) {
        return rank == CampaignGrade.S ? WallitVoice.HomeRun[1] : rank == CampaignGrade.A ? WallitVoice.WaveClear[0] : rank == CampaignGrade.B ? WallitVoice.WaveClear[1] : WallitVoice.Hurt[3];
    }

    // Seconds of the wait before each step, scaled by tap fast-forward.
    const float CharSeconds = 0.035f;

    Font typewriter, serif;
    RectTransform panel, stampBox, rowsRoot, buttons;
    Text header, stampLetter, rankCaption, filed, wallitLine, footer;
    Image splat, paper, catcher;
    readonly List<Text> rowLabels = new List<Text>(), rowValues = new List<Text>();
    AudioSource speaker;
    AudioClip tickClip, thudClip, splatClip;
    int rank;
    string line;
    Stat[] stats;
    Action onContinue, onMenu;
    int tapCount;
    bool fast;
    float canvasScale = 1f;

    public static ChapterCeremony Begin(RectTransform card, string chapterTitle, int fileNumber, Stat[] rows, int rank, string footerText, Action onContinue, Action onMenu) {
        Reset();
        var go = new GameObject("Ceremony", typeof(RectTransform));
        go.transform.SetParent(card, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var c = go.AddComponent<ChapterCeremony>();
        c.rank = rank;
        c.stats = rows;
        c.line = LineFor(rank);
        c.onContinue = onContinue;
        c.onMenu = onMenu;
        c.Build(chapterTitle, fileNumber, footerText);
        Instance = c;
        c.Running = true;
        StartedAt = Time.unscaledTime;
        c.StartCoroutine(c.Play());
        return c;
    }

    static void Reset() {
        StatsTyped = 0; Ticks = 0; Taps = 0; Stamps = 0;
        RankStamped = ""; LineSaid = ""; StingPlayed = "";
        FiledShown = false; ContinueShown = false; Skipped = false;
        ContinueAt = 0f;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    // ------------------------------------------------------------------ build

    void Build(string chapterTitle, int fileNumber, string footerText) {
        typewriter = UiSkin.Font(UiSkin.FontRole.Label);
        serif = UiSkin.Font(UiSkin.FontRole.Body);
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.spatialBlend = 0f;
        speaker.ignoreListenerPause = true;
        tickClip = Synth("tick", 0.03f, (t, i, n) => (Mathf.Sin(i * 0.62f) * 0.5f + Noise(i) * 0.5f) * Mathf.Exp(-t * 9f));
        thudClip = Synth("thud", 0.42f, (t, i, n) => (Mathf.Sin(2f * Mathf.PI * (70f - 30f * t) * t) * 0.9f + Noise(i) * 0.25f * Mathf.Exp(-t * 18f)) * Mathf.Exp(-t * 6.5f));
        splatClip = Synth("splat", 0.28f, (t, i, n) => Noise(i * 3) * Mathf.Exp(-t * 11f) * (0.6f + 0.4f * Mathf.Sin(i * 0.05f)));

        // Tap catcher under everything: a tap anywhere fast-forwards.
        catcher = new GameObject("Tap Catcher", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Image>();
        catcher.transform.SetParent(transform, false);
        catcher.color = new Color(0f, 0f, 0f, 0f);
        Stretch(catcher.rectTransform);
        catcher.GetComponent<Button>().onClick.AddListener(Tapped);
        var nav = catcher.GetComponent<Button>().navigation;
        nav.mode = Navigation.Mode.None;
        catcher.GetComponent<Button>().navigation = nav;

        Vector2 size = UiFit.Size;
        bool landscape = UiFit.Compact && size.x > size.y * 1.1f;
        const float PanelW = 1000f, PanelH = 850f;
        float scale = landscape ? Mathf.Min(1f, (size.y - 30f) / PanelH) : 1f;
        float width = Mathf.Min(PanelW, (size.x - 30f) / Mathf.Max(0.01f, scale));
        float buttonsRoom = landscape ? 0f : 130f;
        if (!landscape) {
            scale = Mathf.Min(1f, (size.y - buttonsRoom - 30f) / PanelH);
        }
        canvasScale = scale;

        panel = new GameObject("Dossier", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        paper = panel.GetComponent<Image>();
        UiSkin.Round(paper, 0.5f);
        paper.color = new Color(0.93f, 0.89f, 0.78f, 1f);
        paper.raycastTarget = false;
        var edge = panel.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.35f, 0.27f, 0.15f, 0.9f);
        edge.effectDistance = new Vector2(3f, -3f);
        panel.anchorMin = panel.anchorMax = new Vector2(landscape ? 0.36f : 0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(width, PanelH);
        panel.localScale = new Vector3(scale, scale, 1f);
        panel.anchoredPosition = new Vector2(0f, -15f);

        Color ink = new Color(0.14f, 0.12f, 0.1f, 1f);
        Color faded = new Color(0.4f, 0.34f, 0.26f, 1f);
        header = Make(panel, "FILE " + fileNumber + "  " + chapterTitle.ToUpperInvariant(), 28, typewriter, ink, TextAnchor.MiddleLeft, 24f, 22f, width - 48f, 56f);
        // A rule under the header.
        var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        rule.transform.SetParent(panel, false);
        rule.color = faded;
        rule.raycastTarget = false;
        PlaceTop(rule.rectTransform, 24f, 82f, width - 48f, 3f);

        rowsRoot = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
        rowsRoot.SetParent(panel, false);
        PlaceTop(rowsRoot, 0f, 0f, width, PanelH);
        for (int i = 0; i < stats.Length; i++) {
            float y = 100f + i * 66f;
            Text label = Make(rowsRoot, "", 28, typewriter, faded, TextAnchor.MiddleLeft, 24f, y, width * 0.38f, 56f);
            Text value = Make(rowsRoot, "", 30, typewriter, ink, TextAnchor.MiddleLeft, 24f + width * 0.38f, y, width * 0.62f - 48f, 56f);
            rowLabels.Add(label);
            rowValues.Add(value);
        }

        // The stamp: a soft ink splat under a huge letter.
        stampBox = new GameObject("Rank Stamp", typeof(RectTransform)).GetComponent<RectTransform>();
        stampBox.SetParent(panel, false);
        PlaceTop(stampBox, (width - 340f) * 0.5f, 372f, 340f, 340f);
        splat = new GameObject("Ink Splat", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        splat.transform.SetParent(stampBox, false);
        splat.sprite = UiSkin.Glow;
        splat.raycastTarget = false;
        Stretch(splat.rectTransform);
        splat.color = new Color(0f, 0f, 0f, 0f);
        Color rankInk = RankInk(rank);
        stampLetter = Make(stampBox, CampaignGrade.Letter(rank), 300, typewriter, rankInk, TextAnchor.MiddleCenter, 0f, 0f, 340f, 340f);
        stampLetter.resizeTextForBestFit = false;
        stampLetter.horizontalOverflow = HorizontalWrapMode.Overflow;
        stampLetter.verticalOverflow = VerticalWrapMode.Overflow;
        stampLetter.GetComponent<Shadow>().enabled = false;
        var letterEdge = stampLetter.gameObject.AddComponent<Outline>();
        letterEdge.effectColor = new Color(rankInk.r * 0.5f, rankInk.g * 0.5f, rankInk.b * 0.5f, 0.9f);
        letterEdge.effectDistance = new Vector2(4f, -4f);
        stampBox.localRotation = Quaternion.Euler(0f, 0f, -7f);
        rankCaption = Make(panel, "RANK", 28, typewriter, faded, TextAnchor.MiddleCenter, (width - 300f) * 0.5f, 340f, 300f, 40f);
        stampBox.gameObject.SetActive(false);
        rankCaption.gameObject.SetActive(false);

        // FILED across the whole sheet.
        filed = Make(panel, "FILED", 190, typewriter, new Color(0.13f, 0.25f, 0.62f, 0.88f), TextAnchor.MiddleCenter, 0f, 330f, width, 260f);
        filed.resizeTextForBestFit = false;
        filed.horizontalOverflow = HorizontalWrapMode.Overflow;
        filed.verticalOverflow = VerticalWrapMode.Overflow;
        filed.GetComponent<Shadow>().enabled = false;
        var filedEdge = filed.gameObject.AddComponent<Outline>();
        filedEdge.effectColor = new Color(0.13f, 0.25f, 0.62f, 0.6f);
        filedEdge.effectDistance = new Vector2(5f, -5f);
        filed.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 13f);
        filed.gameObject.SetActive(false);

        wallitLine = Make(panel, "", 32, serif, ink, TextAnchor.MiddleCenter, 24f, 724f, width - 48f, 56f);
        wallitLine.fontStyle = FontStyle.Italic;
        footer = Make(panel, footerText, 28, typewriter, faded, TextAnchor.MiddleCenter, 24f, 786f, width - 48f, 50f);
        footer.gameObject.SetActive(false);

        // Continue and Menu: 88 units, a thumb target.
        buttons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        buttons.SetParent(transform, false);
        if (landscape) {
            buttons.anchorMin = buttons.anchorMax = new Vector2(0.82f, 0.5f);
            buttons.pivot = new Vector2(0.5f, 0.5f);
            buttons.sizeDelta = new Vector2(420f, 200f);
            buttons.anchoredPosition = Vector2.zero;
        } else {
            buttons.anchorMin = new Vector2(0.5f, 0f);
            buttons.anchorMax = new Vector2(0.5f, 0f);
            buttons.pivot = new Vector2(0.5f, 0f);
            buttons.sizeDelta = new Vector2(Mathf.Min(900f, size.x - 24f), 88f);
            buttons.anchoredPosition = new Vector2(0f, 22f);
        }
        float menuW = 190f;
        float contW = landscape ? 420f : buttons.sizeDelta.x - menuW - 16f;
        ContinueButton = MakeButton(buttons, "Continue", contW, 88f, landscape ? new Vector2(0f, 52f) : new Vector2(-(menuW + 16f) * 0.5f, 0f), () => Finish(onContinue));
        MenuButton = MakeButton(buttons, "Menu", landscape ? 420f : menuW, 88f, landscape ? new Vector2(0f, -52f) : new Vector2((contW + 16f) * 0.5f, 0f), () => Finish(onMenu));
        buttons.gameObject.SetActive(false);

        // The smallest text the sheet shows, in canvas units (the playtest turns it into px).
        SmallestTextUnits = 28f * scale;

        // It slides in from below the screen.
        panel.anchoredPosition = new Vector2(0f, -size.y);
    }

    static Color RankInk(int rank) {
        return rank == CampaignGrade.S ? new Color(0.78f, 0.52f, 0.06f, 0.96f) : rank == CampaignGrade.A ? new Color(0.72f, 0.12f, 0.1f, 0.96f) : rank == CampaignGrade.B ? new Color(0.16f, 0.3f, 0.55f, 0.96f) : new Color(0.38f, 0.32f, 0.26f, 0.96f);
    }

    // ------------------------------------------------------------------ the show

    IEnumerator Play() {
        Vector2 size = UiFit.Size;
        float homeY = -15f;
        float startY = -size.y;
        // 1. The dossier slides in.
        yield return Step(0.55f, t => {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, homeY, e));
        });
        panel.anchoredPosition = new Vector2(0f, homeY);
        yield return Wait(0.2f);

        // 2. Each stat types onto the form, label then value, a tick every second letter.
        for (int i = 0; i < stats.Length; i++) {
            yield return Type(rowLabels[i], stats[i].Label, 2);
            yield return Type(rowValues[i], stats[i].Value, 2);
            StatsTyped++;
            ChapterRun.Events.Add("ceremony|stat|" + stats[i].Label + "|" + stats[i].Value);
            yield return Wait(0.22f);
        }
        yield return Wait(0.3f);

        // 3. The rank stamp comes down.
        rankCaption.gameObject.SetActive(true);
        stampBox.gameObject.SetActive(true);
        yield return Step(0.22f, t => {
            float s = Mathf.Lerp(3.4f, 1f, t * t);
            stampBox.localScale = new Vector3(s, s, 1f);
            stampLetter.color = Fade(RankInk(rank), t);
        });
        stampBox.localScale = Vector3.one;
        stampLetter.color = RankInk(rank);
        Thud(0.95f, 1f);
        Play2(splatClip, 0.6f, 1f);
        Stamps++;
        RankStamped = CampaignGrade.Letter(rank);
        StingPlayed = "rank_" + RankStamped.ToLowerInvariant();
        GameAudio.PlayUI("stamp_heavy", 0.9f);
        MusicDirector.Stinger(StingPlayed);
        ChapterRun.Events.Add("ceremony|rank|" + RankStamped + "|sting " + StingPlayed);
        float splatSize = rank == CampaignGrade.S ? 1.7f : rank == CampaignGrade.A ? 1.4f : rank == CampaignGrade.B ? 1.2f : 1f;
        Color splatInk = RankInk(rank);
        yield return Step(0.35f, t => {
            float s = Mathf.Lerp(0.6f, splatSize, 1f - Mathf.Pow(1f - t, 3f));
            splat.rectTransform.localScale = new Vector3(s, s, 1f);
            splat.color = new Color(splatInk.r, splatInk.g, splatInk.b, Mathf.Lerp(0.55f, 0.3f, t));
        });
        // The stamp holds while its sting rings: longer for a better rank.
        yield return Wait(rank == CampaignGrade.S ? 1.1f : rank == CampaignGrade.A ? 0.9f : 0.6f);

        // 4. FILED across the sheet.
        filed.gameObject.SetActive(true);
        yield return Step(0.16f, t => {
            float s = Mathf.Lerp(2.4f, 1f, t * t);
            filed.rectTransform.localScale = new Vector3(s, s, 1f);
            filed.color = Fade(new Color(0.13f, 0.25f, 0.62f, 0.88f), t);
        });
        filed.rectTransform.localScale = Vector3.one;
        Thud(0.8f, 0.75f);
        FiledShown = true;
        Stamps++;
        ChapterRun.Events.Add("ceremony|filed");
        yield return Wait(0.35f);

        // 5. Wallit says his line.
        wallitLine.text = "“" + line + "”";
        float spoken = WallitVoice.Cutscene(line);
        LineSaid = line;
        ChapterRun.Events.Add("ceremony|wallit|" + CampaignGrade.Letter(rank) + "|" + line);
        yield return Wait(Mathf.Clamp(spoken, 0.8f, 2.2f));
        footer.gameObject.SetActive(true);
        yield return Wait(0.2f);
        ShowContinue();
    }

    void ShowContinue() {
        if (ContinueShown) {
            return;
        }
        // Whatever was skipped lands in its finished state.
        for (int i = 0; i < stats.Length; i++) {
            rowLabels[i].text = stats[i].Label;
            rowValues[i].text = stats[i].Value;
        }
        StatsTyped = Mathf.Max(StatsTyped, stats.Length);
        panel.anchoredPosition = new Vector2(0f, -15f);
        rankCaption.gameObject.SetActive(true);
        stampBox.gameObject.SetActive(true);
        stampBox.localScale = Vector3.one;
        stampLetter.color = RankInk(rank);
        if (RankStamped.Length == 0) {
            RankStamped = CampaignGrade.Letter(rank);
            Stamps++;
        }
        splat.rectTransform.localScale = Vector3.one;
        splat.color = new Color(RankInk(rank).r, RankInk(rank).g, RankInk(rank).b, 0.3f);
        filed.gameObject.SetActive(true);
        filed.rectTransform.localScale = Vector3.one;
        filed.color = new Color(0.13f, 0.25f, 0.62f, 0.88f);
        FiledShown = true;
        if (wallitLine.text.Length == 0) {
            wallitLine.text = "“" + line + "”";
        }
        footer.gameObject.SetActive(true);
        buttons.gameObject.SetActive(true);
        catcher.gameObject.SetActive(false);
        ContinueShown = true;
        Running = false;
        ContinueAt = Time.unscaledTime;
        ChapterRun.Events.Add("ceremony|continue|" + (Time.unscaledTime - StartedAt).ToString("0.0") + " s" + (Skipped ? "|skipped" : ""));
        GameAudio.PlayUI("ui_click", 0.4f);
    }

    void Finish(Action then) {
        if (then != null) {
            then();
        }
    }

    // ------------------------------------------------------------------ tap and keys

    void Update() {
        if (!Running) {
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter) || PlayInput.GetKeyDown(KeyCode.JoystickButton0)) {
            Tapped();
        }
    }

    // First tap: the step in hand finishes at once. A second: the rest is skipped and Continue shows.
    public void Tapped() {
        if (!Running) {
            return;
        }
        tapCount++;
        Taps++;
        if (tapCount >= 2) {
            Skipped = true;
            StopAllCoroutines();
            ShowContinue();
            return;
        }
        fast = true;
    }

    // ------------------------------------------------------------------ steps

    // Runs `apply(0..1)` over `seconds` of unscaled time; a tap lands it on 1 at once.
    IEnumerator Step(float seconds, Action<float> apply) {
        float t0 = Time.unscaledTime;
        while (true) {
            float t = Mathf.Clamp01((Time.unscaledTime - t0) / seconds);
            if (fast) {
                t = 1f;
            }
            apply(t);
            if (t >= 1f) {
                break;
            }
            yield return null;
        }
        fast = false;
    }

    IEnumerator Wait(float seconds) {
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < seconds && !fast) {
            yield return null;
        }
        fast = false;
    }

    IEnumerator Type(Text target, string full, int tickEvery) {
        int shown = 0;
        float next = Time.unscaledTime;
        while (shown < full.Length) {
            if (fast) {
                shown = full.Length;
                target.text = full;
                break;
            }
            if (Time.unscaledTime >= next) {
                shown++;
                next = Time.unscaledTime + CharSeconds;
                target.text = full.Substring(0, shown);
                if (shown % tickEvery == 0 && full[shown - 1] != ' ') {
                    Tick();
                }
            }
            yield return null;
        }
        target.text = full;
        fast = false;
    }

    // ------------------------------------------------------------------ sound

    void Tick() {
        Ticks++;
        Play2(tickClip, 0.35f, UnityEngine.Random.Range(0.85f, 1.2f));   // the rate varies so no tick repeats exactly
    }

    void Thud(float volume, float pitch) {
        Play2(thudClip, volume, pitch);
    }

    void Play2(AudioClip clip, float volume, float pitch) {
        float level = SoundSettings.EffectsLevel;
        if (clip == null || level <= 0f) {
            return;
        }
        speaker.pitch = pitch;
        speaker.PlayOneShot(clip, Mathf.Clamp01(volume * level));
        GameAudio.NoteOther(GameAudio.Bus.UI, "ceremony_" + clip.name);
    }

    static float Noise(int i) {
        unchecked {
            uint x = (uint)i * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x & 0xFFFF) / 32768f - 1f;
        }
    }

    static AudioClip Synth(string name, float seconds, Func<float, int, int, float> sample) {
        int rate = 22050;
        int n = Mathf.RoundToInt(seconds * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) {
            data[i] = Mathf.Clamp(sample((float)i / n, i, n), -1f, 1f) * 0.8f;
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ------------------------------------------------------------------ ui helpers

    static Color Fade(Color c, float t) {
        return new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(t * 3f));
    }

    static void Stretch(RectTransform r) {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static void PlaceTop(RectTransform r, float x, float y, float w, float h) {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    static Text Make(RectTransform parent, string value, int size, Font font, Color color, TextAnchor anchor, float x, float y, float w, float h) {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Min(size, 24);
        text.resizeTextMaxSize = size;
        go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.18f);
        PlaceTop(text.rectTransform, x, y, w, h);
        return text;
    }

    Button MakeButton(RectTransform parent, string label, float w, float h, Vector2 at, UnityEngine.Events.UnityAction click) {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, 0.6f);
        image.color = new Color(0.13f, 0.14f, 0.2f, 1f);
        var edge = go.AddComponent<Outline>();
        edge.effectColor = new Color(0.8f, 0.63f, 0.33f, 0.8f);
        edge.effectDistance = new Vector2(2f, -2f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(click);
        MenuButtonFx.Add(button);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = new Vector2(w, h);
        Text text = Make(rect, label, 36, typewriter, new Color(0.97f, 0.94f, 0.86f), TextAnchor.MiddleCenter, 0f, 0f, w, h);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}
