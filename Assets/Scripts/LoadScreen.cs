using UnityEngine;
using UnityEngine.UI;

// The load screen between a chapter's card and its first frame (and between a chapter restart and the new run). Shown by
// ChapterRun.Begin before the game scene loads, it survives the scene change on its own object, shows the chapter's title,
// place and the Department's one-line note over the chapter's art (the first still of its opening reel,
// Resources/Cinematics/Art/<reel's first back>; with no art yet, a night plate in the chapter's colours), and fades out once
// the new ChapterRun is up and its map has filled in (or after a safety time). The still is released as it leaves, so it
// costs no memory during play. It never runs in Arcade: only ChapterRun.Begin shows it.
public class LoadScreen : MonoBehaviour {
    const float MinSeconds = 0.9f;
    const float MaxSeconds = 25f;
    const float FadeSeconds = 0.45f;

    static LoadScreen instance;

    public static bool IsUp { get { return instance != null && instance.canvasGo != null && instance.canvasGo.activeSelf; } }
    public static int Shown { get; private set; }
    public static string LastTitle { get; private set; } = "";
    public static bool LastHadArt { get; private set; }
    public static float LastSeconds { get; private set; }
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
    Texture2D still;
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
        LastSeconds = 0f;
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

    void Open(int chapter) {
        if (canvasGo == null) {
            Build();
        }
        Story.Chapter data = Story.Chapters[chapter];
        title.text = data.Title;
        place.text = data.Place;
        note.text = "Filing the chapter…";
        ReleaseStill();
        Cutscene.Reel reel = Cutscene.LoadReel("ch" + (chapter + 1) + "_open");
        if (reel != null && reel.shots.Length > 0 && !string.IsNullOrEmpty(reel.shots[0].back)) {
            still = Resources.Load<Texture2D>("Cinematics/Art/" + reel.shots[0].back);
        }
        art.texture = still;
        art.enabled = still != null;
        plate.color = still != null ? new Color(0f, 0f, 0f, 0.5f) : new Color(0.04f, 0.05f, 0.1f, 1f);
        FitArt();
        group.alpha = 1f;
        canvasGo.SetActive(true);
        shownAt = Time.unscaledTime;
        fading = -1f;
        Shown++;
        LastTitle = data.Title;
        LastHadArt = still != null;
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
            if (up >= MaxSeconds || (up >= MinSeconds && ChapterIsUp())) {
                fading = 0f;
                LastSeconds = up;
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
            Resources.UnloadAsset(still);
            still = null;
        }
    }

    // Crops the still to fill the screen, no stretching.
    void FitArt() {
        if (art == null || still == null) {
            return;
        }
        float screen = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
        float image = (float)still.width / Mathf.Max(1, still.height);
        Rect r = new Rect(0f, 0f, 1f, 1f);
        if (screen > image) {
            r.height = image / screen;
            r.y = (1f - r.height) * 0.5f;
        } else {
            r.width = screen / image;
            r.x = (1f - r.width) * 0.5f;
        }
        art.uvRect = r;
    }

    void Build() {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        place = Words(font, "Place", 30, FontStyle.Italic, new Color(0.87f, 0.91f, 1f), new Vector2(0f, 60f));
        title = Words(font, "Title", 72, FontStyle.Bold, new Color(1f, 0.82f, 0.38f), new Vector2(0f, 6f));
        note = Words(font, "Note", 26, FontStyle.Normal, new Color(1f, 0.97f, 0.9f, 0.75f), new Vector2(0f, -130f));
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

    Text Words(Font font, string name, int size, FontStyle style, Color color, Vector2 at) {
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
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = new Vector2(1400f, size * 1.6f);
        return text;
    }
}
