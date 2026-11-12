using UnityEngine;
using UnityEngine.UI;

// The illustrated loading card between a chapter's (or the hub's) hand-over and its first frame: seven case-file cards, one
// per chapter and one for the hub (LoadCards/card_<id>, painted, fetched on demand with its chapter: group chN, the hub's in
// core), the place's name in the Department typeface, a paper progress strip that fills only as the load really advances
// (scene in, run up, map filled in) and one short voiced line from Wallit or Slice about the place (an existing voice
// file, heard once, with its words under it). It sits under the ScreenTransition sheet, which peels to reveal it, survives the
// scene change on its own object and fades out when the place is up. With no card art it falls back to the first still of
// the chapter's opening reel, then a night plate. It never runs in Arcade.
public class LoadScreen : MonoBehaviour {
    const float MinSeconds = 1.2f;
    const float MaxHold = 3.4f;     // the voiced line is let finish, but the card never waits longer than this for it
    const float MaxSeconds = 25f;
    const float FadeSeconds = 0.45f;
    const int HubCard = 6;

    // One card per place: id (art LoadCards/card_<id>), the place's name, its line and who says it (voice file key).
    class Card {
        public string Id, Title, Place, Voice, Words;
        public Vector4 Focus = new Vector4(0f, 1f, 0f, 1f);   // the painted subject in the art: x0, x1, top y0, y1 (0..1 from the top-left)
    }

    static readonly Card[] Cards = {
        new Card { Id = "ch1", Focus = new Vector4(.10f, .93f, .38f, .75f), Voice = "slice_the_clearing_crew_stamped_out_here_one_batch_for_every_unfiled_thing_in_the_hollow",
            Words = "The Clearing Crew. Stamped out here, one batch for every unfiled thing in the Hollow." },
        new Card { Id = "ch2", Focus = new Vector4(0f, 1f, .30f, .72f), Voice = "slice_seventy_metres_then_back", Words = "Seventy metres. Then back." },
        new Card { Id = "ch3", Focus = new Vector4(.17f, .83f, .25f, .75f), Voice = "slice_pull_the_lever_the_belt_stops_the_crew_stops", Words = "Pull the lever. The belt stops. The crew stops." },
        new Card { Id = "ch4", Voice = "slice_walk_the_lamps_hold_each_i_ll_keep_the_ring", Words = "Walk the lamps. Hold each. I'll keep the ring." },
        new Card { Id = "ch5", Voice = "wallit_pages_four_and_five_are_the_small_print_nobody_reads_the_small_print_i_do",
            Words = "Pages four and five are the small print. Nobody reads the small print. I do." },
        new Card { Id = "ch6", Focus = new Vector4(0f, 1f, .45f, 1f), Voice = "slice_the_wall_is_gone_the_circle_is_brighter", Words = "The wall is gone. The circle is brighter." },
        new Card { Id = "hub", Focus = new Vector4(.22f, .78f, .12f, .88f), Title = "The Office", Place = "Where the files are kept", Voice = "slice_two_doors_wallit_s_is_next_to_mine_knock_on_it",
            Words = "Two doors. Wallit's is next to mine. Knock on it." },
    };

    public static string[] CardIds { get { var ids = new string[Cards.Length]; for (int i = 0; i < ids.Length; i++) { ids[i] = Cards[i].Id; } return ids; } }
    public static string ArtKey(int card) { return "LoadCards/card_" + Cards[card].Id; }
    public static string VoiceKey(int card) { return "Voice/" + Cards[card].Voice; }
    public static int HubIndex { get { return HubCard; } }

    static LoadScreen instance;

    public static bool IsUp { get { return instance != null && instance.canvasGo != null && instance.canvasGo.activeSelf; } }
    public static int Shown { get; private set; }
    public static string LastTitle { get; private set; } = "";
    public static bool LastHadArt { get; private set; }
    public static bool LastHadCardArt { get; private set; }
    public static bool LastVoiced { get; private set; }
    public static string LastCard { get; private set; } = "";
    public static string LastWords { get; private set; } = "";
    public static float LastProgress { get; private set; }
    public static float LastSeconds { get; private set; }
    public static readonly System.Collections.Generic.List<string> Events = new System.Collections.Generic.List<string>();
    public static LoadScreen Instance { get { return instance; } }
    public string TitleShown { get { return title != null ? title.text : ""; } }
    public string PlaceShown { get { return place != null ? place.text : ""; } }

    GameObject canvasGo;
    CanvasGroup group;
    RawImage art;
    Image plate;
    Text title;
    Text place;
    Text note;
    Text words;
    RectTransform fill;
    RectTransform strip;
    AudioSource voice;
    AudioClip voiceClip;
    bool voicePlayed;
    float voiceWaitUntil;
    float progress;
    int cardIndex;
    Texture2D still;
    Vector4 focus = new Vector4(0f, 1f, 0f, 1f);
    CanvasScaler scaler;
    float shownAt;
    float fading = -1f;
    int stamp = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
        Shown = 0;
        LastTitle = "";
        LastHadArt = false;
        LastHadCardArt = false;
        LastVoiced = false;
        LastCard = "";
        LastWords = "";
        LastProgress = 0f;
        LastSeconds = 0f;
        Events.Clear();
    }

    // Puts the screen up for a chapter (0-based). Called from ChapterRun.Begin, just before the scene load.
    public static void Show(int chapter) {
        if (chapter < 0 || chapter >= Story.Chapters.Length) {
            return;
        }
        if (instance == null) {
            var go = new GameObject("Load Screen");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadScreen>();
        }
        instance.Open(chapter);
    }

    // The hub's card, for a trip back to the office.
    public static void ShowHub() {
        if (instance == null) {
            var go = new GameObject("Load Screen");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadScreen>();
        }
        instance.Open(HubCard);
    }

    void Open(int card) {
        if (canvasGo == null) {
            Build();
        }
        Card data = Cards[card];
        cardIndex = card;
        if (card < Story.Chapters.Length) {
            Story.Chapter chapter = Story.Chapters[card];
            title.text = chapter.Title;
            place.text = chapter.Place;
        } else {
            title.text = data.Title;
            place.text = data.Place;
        }
        note.text = card == HubCard ? "Back to the office…" : "Filing the chapter…";
        words.text = "\u201C" + data.Words + "\u201D";
        ReleaseStill();
        still = OnDemand.Texture("LoadCards/card_" + data.Id);
        bool cardArt = still != null;
        if (still == null && card < Story.Chapters.Length) {
            Cutscene.Reel reel = Cutscene.LoadReel("ch" + (card + 1) + "_open");
            if (reel != null && reel.shots.Length > 0 && !string.IsNullOrEmpty(reel.shots[0].back)) {
                still = OnDemand.Texture("Cinematics/Art/" + reel.shots[0].back);
            }
        }
        focus = cardArt ? data.Focus : new Vector4(0f, 1f, 0f, 1f);
        art.texture = still;
        art.enabled = still != null;
        plate.color = still != null ? new Color(0f, 0f, 0f, 0.38f) : new Color(0.04f, 0.05f, 0.1f, 1f);
        FitArt();
        progress = 0f;
        SetFill(0f);
        group.alpha = 1f;
        canvasGo.SetActive(true);
        shownAt = Time.unscaledTime;
        fading = -1f;
        Shown++;
        LastTitle = title.text;
        LastCard = data.Id;
        LastWords = data.Words;
        LastHadArt = still != null;
        LastHadCardArt = cardArt;
        LastVoiced = false;
        LastProgress = 0f;
        StartVoice(data);
        Events.Add("card|" + data.Id + "|art=" + (cardArt ? "card" : still != null ? "reel" : "none"));
    }

    // The one voiced line: the clip if it is here, else asked for by key and played when it lands.
    void StartVoice(Card data) {
        voicePlayed = false;
        voiceClip = null;
        if (voice != null) {
            voice.Stop();
        }
        string key = "Voice/" + data.Voice;
        AudioClip clip = OnDemand.Audio(key);
        voiceWaitUntil = Time.unscaledTime + 1.5f;
        if (clip != null) {
            PlayVoice(clip, data);
            return;
        }
        int expect = cardIndex;
        OnDemand.FetchKey(key, o => {
            var fetched = o as AudioClip;
            if (fetched != null && cardIndex == expect && !voicePlayed && canvasGo != null && canvasGo.activeSelf) {
                PlayVoice(fetched, data);
            }
        });
    }

    void PlayVoice(AudioClip clip, Card data) {
        voicePlayed = true;
        voiceClip = clip;
        float level = SoundSettings.EffectsLevel;
        if (voice == null) {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }
        voice.clip = clip;
        voice.volume = Mathf.Clamp01(level);
        if (level > 0f) {
            voice.Play();
        }
        LastVoiced = true;
        GameAudio.NoteOther(GameAudio.Bus.Voice, data.Voice);
        Events.Add("voice|" + data.Id + "|" + clip.length.ToString("0.0") + "s");
    }

    void SetFill(float p) {
        if (fill != null) {
            fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        }
    }

    // How far the place has really come: scene in, run built, map filled in. Never a timer.
    float Readiness() {
        if (cardIndex == HubCard) {
            return ChapterHub.Instance != null ? 1f : (ChapterScene.Instance != null ? 0.6f : 0.15f);
        }
        if (LevelScene.Instance != null) {
            return 1f;
        }
        if (ChapterRun.Instance == null) {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("Level_") ? 0.6f : 0.15f;
        }
        ChapterMap map = ChapterMap.Current;
        if (map == null || !map.OwnScene) {
            return ChapterIsUp() ? 1f : 0.6f;
        }
        return map.Own != null ? 1f : 0.75f;
    }

    void Update() {
        if (canvasGo == null || !canvasGo.activeSelf) {
            return;
        }
        if (stamp != UiFit.Stamp) {
            stamp = UiFit.Stamp;
            UiFit.Apply(scaler);
            FitArt();
        }
        float up = Time.unscaledTime - shownAt;
        if (fading < 0f) {
            // The strip fills toward what is really done, at a pace the eye can follow, and never past it.
            float target = Readiness();
            progress = Mathf.Min(target, progress + Time.unscaledDeltaTime * 1.6f);
            SetFill(progress);
            LastProgress = progress;
            float hold = MinSeconds;
            if (voiceClip != null) {
                hold = Mathf.Clamp(voiceClip.length + 0.35f, MinSeconds, MaxHold);
            } else if (!voicePlayed && Time.unscaledTime < voiceWaitUntil) {
                hold = MaxHold;   // the line is still on its way
            }
            if (up >= MaxSeconds || (up >= hold && progress >= 0.999f && Readiness() >= 1f)) {
                fading = 0f;
                LastSeconds = up;
                Events.Add("done|" + LastCard + "|" + up.ToString("0.0") + "s");
            }
            return;
        }
        fading += Time.unscaledDeltaTime;
        group.alpha = Mathf.Clamp01(1f - fading / FadeSeconds);
        if (fading >= FadeSeconds) {
            canvasGo.SetActive(false);
            ReleaseStill();
        }
    }

    // For the playtest: the smallest text on the card in device pixels (best-fit text counts at the size it was fitted to).
    public float SmallestTextPixels() {
        float smallest = 999f;
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        foreach (Text t in canvasGo.GetComponentsInChildren<Text>()) {
            if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) {
                continue;
            }
            float size = t.resizeTextForBestFit && t.cachedTextGenerator.fontSizeUsedForBestFit > 0 ? t.cachedTextGenerator.fontSizeUsedForBestFit : t.fontSize;
            smallest = Mathf.Min(smallest, size * canvas.scaleFactor);
        }
        return smallest;
    }

    // For the playtest: every text and the strip sit inside the screen's safe area.
    public bool FitsScreen() {
        Rect safe = Screen.safeArea;
        var corners = new Vector3[4];
        foreach (Text t in canvasGo.GetComponentsInChildren<Text>()) {
            if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) {
                continue;
            }
            t.rectTransform.GetWorldCorners(corners);
            if (corners[0].x < safe.xMin - 1f || corners[2].x > safe.xMax + 1f || corners[0].y < safe.yMin - 1f || corners[2].y > safe.yMax + 1f) {
                return false;
            }
        }
        strip.GetWorldCorners(corners);
        return corners[0].x >= safe.xMin - 1f && corners[2].x <= safe.xMax + 1f && corners[0].y >= safe.yMin - 1f && corners[2].y <= safe.yMax + 1f;
    }

    // The new run exists and, if its chapter has a map scene, that scene has filled in.
    static bool ChapterIsUp() {
        if (ChapterRun.Instance == null) {
            return false;
        }
        ChapterMap map = ChapterMap.Current;
        return map == null || !map.OwnScene || map.Own != null;
    }

    void ReleaseStill() {
        if (still != null) {
            art.texture = null;
            OnDemand.Unload(still);
            still = null;
        }
    }

    // Crops the still to fill the screen, no stretching.
    void FitArt() {
        if (art == null || still == null) {
            return;
        }
        float screen = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
        // the smallest screen-shaped window that holds the painted subject, centred on it, kept inside the image
        float fw = (focus.y - focus.x) * still.width, fh = (focus.w - focus.z) * still.height;
        float cx = (focus.x + focus.y) * 0.5f * still.width, cy = (1f - (focus.z + focus.w) * 0.5f) * still.height;
        float w = Mathf.Max(fw, fh * screen), h = w / screen;
        if (w > still.width) { w = still.width; h = w / screen; }
        if (h > still.height) { h = still.height; w = h * screen; }
        Rect r = new Rect(Mathf.Clamp(cx - w * 0.5f, 0f, still.width - w) / still.width, Mathf.Clamp(cy - h * 0.5f, 0f, still.height - h) / still.height, w / still.width, h / still.height);
        art.uvRect = r;
    }

    void Build() {
        Font font = UiSkin.Font(UiSkin.FontRole.Body);
        canvasGo = new GameObject("Load Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        stamp = UiFit.Stamp;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;

        art = Fill(canvasGo.transform, "Art").gameObject.AddComponent<RawImage>();
        art.raycastTarget = false;
        plate = Fill(canvasGo.transform, "Plate").gameObject.AddComponent<Image>();
        plate.raycastTarget = true;   // the screen eats taps while it is up

        // Everything is placed by fractions of the screen so a portrait phone and a wide desktop both read: the name up high,
        // the line low, the strip under it. Sizes are in canvas units (26+ is 15 px or more on a phone).
        Font label = UiSkin.Font(UiSkin.FontRole.Label);
        place = Words(label, "Place", 30, FontStyle.Normal, new Color(0.87f, 0.91f, 1f), 0.8f, 0.86f);
        title = Words(label, "Title", 60, FontStyle.Bold, new Color(1f, 0.82f, 0.38f), 0.66f, 0.8f);
        words = Words(font, "Line", 32, FontStyle.Italic, new Color(1f, 0.97f, 0.9f), 0.3f, 0.42f);
        note = Words(label, "Note", 26, FontStyle.Normal, new Color(1f, 0.97f, 0.9f, 0.8f), 0.13f, 0.19f);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 34;
        title.resizeTextMaxSize = 60;
        words.resizeTextForBestFit = true;
        words.resizeTextMinSize = 26;
        words.resizeTextMaxSize = 32;

        // The paper progress strip: a slip of file paper with a red ink bar that fills.
        var stripGo = new GameObject("Strip", typeof(RectTransform));
        stripGo.transform.SetParent(canvasGo.transform, false);
        strip = (RectTransform)stripGo.transform;
        strip.anchorMin = new Vector2(0.12f, 0.085f);
        strip.anchorMax = new Vector2(0.88f, 0.12f);
        strip.offsetMin = strip.offsetMax = Vector2.zero;
        var paper = stripGo.AddComponent<Image>();
        paper.color = new Color(0.93f, 0.89f, 0.78f, 0.95f);
        paper.raycastTarget = false;
        var border = stripGo.AddComponent<Outline>();
        border.effectColor = new Color(0.2f, 0.16f, 0.1f, 0.9f);
        border.effectDistance = new Vector2(3f, -3f);
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(stripGo.transform, false);
        fill = (RectTransform)fillGo.transform;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(5f, 5f);
        fill.offsetMax = new Vector2(-5f, -5f);
        var ink = fillGo.AddComponent<Image>();
        ink.color = new Color(0.72f, 0.1f, 0.1f, 1f);
        ink.raycastTarget = false;
        canvasGo.SetActive(false);
    }

    static RectTransform Fill(Transform parent, string name) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    Text Words(Font font, string name, int size, FontStyle style, Color color, float yMin, float yMax) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.07f, yMin);
        rect.anchorMax = new Vector2(0.93f, yMax);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return text;
    }
}
