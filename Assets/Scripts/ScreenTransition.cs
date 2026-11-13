using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Department's page turn, used by every scene and panel change: a sheet of file paper slides over the screen, a rubber
// stamp slams down on it (ENTER, FILED, STRUCK OFF or RETURNED for the context) with a thud, the swap happens under it
// (a scene load, a panel opening), and the sheet peels away. Cover and peel run 0.5 to 0.8 s in all with the paper-slide
// and stamp sounds, so there is never a hard cut and never a black frame: the sheet is paper, and it is already up when the
// load blocks a frame. A chapter's load card (LoadScreen) sits under the sheet and is what the peel reveals.
//
// Callers: ScreenTransition.Load(kind, scene) for a scene change, ScreenTransition.Go(kind, swap, loadsScene) for anything
// else (a panel that opens under the sheet). One at a time: a call made while a transition runs does its swap at once.
// Runs on unscaled time (the game may be frozen) and never touches the camera.
public class ScreenTransition : MonoBehaviour {
    public enum Kind { Enter, Filed, StruckOff, Returned }

    public static readonly string[] Words = { "ENTER", "FILED", "STRUCK OFF", "RETURNED" };

    const float SlideIn = 0.26f;    // the sheet comes over
    const float Slam = 0.08f;       // the stamp lands
    const float Hold = 0.06f;       // it sits there a beat
    const float SlideOut = 0.26f;   // the sheet peels away
    // From the call to the swap: the slide, the slam and the hold.
    public const float CoverSeconds = SlideIn + Slam + Hold;
    const float LoadWait = 30f;     // a scene that never reports in is let go after this
    // The sheet is never allowed to stay up past this (LoadWait plus the slide, stamp and peel with a wide margin): if the
    // page turn has not finished by then, or its coroutine died, the sheet is taken down so the screen under it is seen.
    const float MaxCover = 45f;

    static ScreenTransition instance;
    static bool sceneArrived;

    public static bool Busy { get; private set; }
    public static int Count { get; private set; }
    public static int Stamps { get; private set; }
    // Times the sheet had to be taken down by force (a step threw, or it stayed up past MaxCover). Should stay 0.
    public static int Recoveries { get; private set; }
    public static Kind LastKind { get; private set; }
    // Cover to peeled, minus the time the scene load itself held the thread (that is the load, not the animation).
    public static float LastSeconds { get; private set; }
    public static float LastTotalSeconds { get; private set; }
    public static float LastCoveredSeconds { get; private set; }   // how long the sheet sat fully over the screen
    public static float LongestCoveredSeconds { get; private set; }
    public static string LastWord { get; private set; } = "";
    public static readonly List<string> Events = new List<string>();

    GameObject canvasGo;
    CanvasScaler scaler;
    RectTransform canvasRect;
    RectTransform sheet;
    RectTransform stamp;
    Text stampText;
    Image flash;
    Text header;
    int stampFrame = -1;
    float coverBegan = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
        sceneArrived = false;
        Busy = false;
        Count = 0;
        Stamps = 0;
        Recoveries = 0;
        LastSeconds = 0f;
        LastTotalSeconds = 0f;
        LastCoveredSeconds = 0f;
        LongestCoveredSeconds = 0f;
        LastWord = "";
        Events.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Arrived;
        SceneManager.sceneLoaded += Arrived;
    }

    static void Arrived(Scene scene, LoadSceneMode mode) {
        sceneArrived = true;
    }

    // A scene change under the stamp.
    public static void Load(Kind kind, string scene) {
        Go(kind, () => SceneManager.LoadScene(scene), true);
    }

    // Any change under the stamp: `swap` runs while the sheet covers the screen. `loadsScene` holds the peel until the new
    // scene has reported in.
    public static void Go(Kind kind, Action swap, bool loadsScene = false) {
        if (Busy || !Application.isPlaying) {
            if (swap != null) {
                swap();
            }
            return;
        }
        if (instance == null) {
            var go = new GameObject("Screen Transition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenTransition>();
        }
        Busy = true;
        instance.StartCoroutine(instance.Run(kind, swap, loadsScene));
    }

    // Runs the page turn step by step so an exception in any step (a sound, the swap, a card) ends the turn instead of killing
    // the coroutine with the paper still up: a dead coroutine used to leave the sheet over the screen for the whole session.
    IEnumerator Run(Kind kind, Action swap, bool loadsScene) {
        // The swap is the point of the turn (a scene load, a panel change): it runs once, even when a step before it throws,
        // or the player would be left in the scene they asked to leave.
        bool swapStarted = false;
        Action once = swap == null ? null : (Action)(() => {
            if (!swapStarted) {
                swapStarted = true;
                swap();
            }
        });
        var steps = Steps(kind, once, loadsScene);
        while (true) {
            bool more;
            try {
                more = steps.MoveNext();
            } catch (Exception e) {
                Debug.LogException(e);
                Recoveries++;
                more = false;
                if (once != null && !swapStarted) {
                    try {
                        once();
                    } catch (Exception swapError) {
                        Debug.LogException(swapError);
                    }
                }
            }
            if (!more) {
                break;
            }
            yield return steps.Current;
        }
        Release();
    }

    // The sheet comes down and the next turn may start (also the end of a normal turn).
    void Release() {
        coverBegan = -1f;
        if (canvasGo != null) {
            canvasGo.SetActive(false);
        }
        Busy = false;
    }

    IEnumerator Steps(Kind kind, Action swap, bool loadsScene) {
        if (canvasGo == null) {
            Build();
        }
        string word = Words[(int)kind];
        stampText.text = word;
        header.text = "THE PLACE DEPARTMENT   -   " + word + "   -   FILE COPY";
        stamp.gameObject.SetActive(false);
        flash.color = new Color(1f, 1f, 1f, 0f);
        SetSheet(0f);
        canvasGo.SetActive(true);
        coverBegan = Time.unscaledTime;
        LayoutStamp();
        float started = Time.realtimeSinceStartup;
        GameAudio.PlayUI("paper_slide", 0.8f);

        // The sheet slides over.
        float t = 0f;
        while (t < SlideIn) {
            t += Time.unscaledDeltaTime;
            SetSheet(Ease(Mathf.Clamp01(t / SlideIn)));
            yield return null;
        }
        SetSheet(1f);

        // The stamp slams down: big and faint to true size and ink, then a settle.
        stamp.gameObject.SetActive(true);
        t = 0f;
        while (t < Slam) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Slam);
            float scale = Mathf.Lerp(2.1f, 1f, k * k);
            stamp.localScale = new Vector3(scale, scale, 1f);
            stampText.canvasRenderer.SetAlpha(Mathf.Lerp(0.25f, 1f, k));
            yield return null;
        }
        stamp.localScale = Vector3.one;
        stampText.canvasRenderer.SetAlpha(1f);
        GameAudio.PlayUI("stamp_heavy", 0.9f);
        Stamps++;
        float landed = Time.realtimeSinceStartup;
        t = 0f;
        while (t < Hold) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Hold);
            float bump = 1f + 0.05f * Mathf.Sin(k * Mathf.PI);
            stamp.localScale = new Vector3(bump, bump, 1f);
            flash.color = new Color(1f, 0.97f, 0.9f, 0.22f * (1f - k));
            yield return null;
        }
        stamp.localScale = Vector3.one;
        flash.color = new Color(1f, 1f, 1f, 0f);

        // The swap happens under the paper.
        float coverStart = Time.realtimeSinceStartup;
        sceneArrived = false;
        if (swap != null) {
            swap();
        }
        if (loadsScene) {
            float waited = 0f;
            while (!sceneArrived && waited < LoadWait) {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        // Two frames so the new scene's Awake/Start have drawn once under the sheet.
        yield return null;
        yield return null;
        float covered = Time.realtimeSinceStartup - landed;
        float load = Time.realtimeSinceStartup - coverStart;

        // The sheet peels away, stamp and all.
        float peelStart = Time.realtimeSinceStartup;
        t = 0f;
        while (t < SlideOut) {
            t += Time.unscaledDeltaTime;
            float k = Ease(Mathf.Clamp01(t / SlideOut));
            SetSheet(1f + k);
            yield return null;
        }
        float end = Time.realtimeSinceStartup;

        Count++;
        LastKind = kind;
        LastWord = word;
        LastTotalSeconds = end - started;
        LastCoveredSeconds = covered;
        LongestCoveredSeconds = Mathf.Max(LongestCoveredSeconds, covered);
        // The animation alone: slide, stamp and peel, with the swap's own time (the load) taken out.
        LastSeconds = (coverStart - started) + (end - peelStart);
        Events.Add(word + "|total=" + LastTotalSeconds.ToString("0.00") + "|covered=" + covered.ToString("0.00")
            + "|swap=" + load.ToString("0.00") + "|peel=" + (end - peelStart).ToString("0.00"));
    }

    // 0 = off to the right, 1 = covering, 2 = gone off to the left.
    void SetSheet(float p) {
        float x = 1f - p;
        sheet.anchorMin = new Vector2(x, 0f);
        sheet.anchorMax = new Vector2(x + 1f, 1f);
        sheet.offsetMin = Vector2.zero;
        sheet.offsetMax = Vector2.zero;
    }

    static float Ease(float k) {
        return 1f - (1f - k) * (1f - k) * (1f - k);
    }

    void Update() {
        // Watchdog: a page turn that has not finished in MaxCover seconds is let go, so the player never sits on a blank sheet.
        if (canvasGo != null && canvasGo.activeSelf && coverBegan >= 0f && Time.unscaledTime - coverBegan > MaxCover) {
            StopAllCoroutines();
            Recoveries++;
            Debug.LogWarning("[ScreenTransition] the sheet was still up after " + MaxCover + " s; taking it down.");
            Release();
            return;
        }
        if (canvasGo != null && canvasGo.activeSelf && stampFrame != UiFit.Stamp) {
            UiFit.Apply(scaler, 0.5f);
            LayoutStamp();
        }
    }

    // The stamp is 80% of the screen across, whatever the screen: a phone in portrait gets a smaller word, not an overflow.
    void LayoutStamp() {
        stampFrame = UiFit.Stamp;
        UiFit.Apply(scaler, 0.5f);
        Canvas.ForceUpdateCanvases();
        float width = canvasRect != null ? canvasRect.rect.width : 1920f;
        float w = Mathf.Min(1500f, Mathf.Max(300f, width * 0.84f));
        stamp.sizeDelta = new Vector2(w, w * 0.2f);
        stampText.resizeTextMaxSize = Mathf.RoundToInt(w * 0.16f);
    }

    void Build() {
        canvasGo = new GameObject("Transition Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // over the load card (900), the menus and the panels
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // The sheet: file paper with a shadow on its leading edge and a header line.
        var sheetGo = new GameObject("Sheet", typeof(RectTransform));
        sheetGo.transform.SetParent(canvasGo.transform, false);
        sheet = (RectTransform)sheetGo.transform;
        var paper = sheetGo.AddComponent<Image>();
        paper.sprite = PaperSprite();
        paper.color = Color.white;
        paper.raycastTarget = true;   // the sheet eats taps while it is up

        var edge = Child(sheet, "Edge");
        edge.anchorMin = new Vector2(0f, 0f);
        edge.anchorMax = new Vector2(0f, 1f);
        edge.pivot = new Vector2(1f, 0.5f);
        edge.sizeDelta = new Vector2(40f, 0f);
        var edgeImage = edge.gameObject.AddComponent<Image>();
        edgeImage.sprite = EdgeSprite();
        edgeImage.raycastTarget = false;

        header = MakeWords(sheet, "Header", 26, new Color(0.35f, 0.3f, 0.22f, 0.9f), TextAnchor.UpperCenter);
        var hr = header.rectTransform;
        hr.anchorMin = new Vector2(0.04f, 0.9f);
        hr.anchorMax = new Vector2(0.96f, 0.97f);
        hr.offsetMin = hr.offsetMax = Vector2.zero;
        header.resizeTextForBestFit = true;
        header.resizeTextMinSize = 20;
        header.resizeTextMaxSize = 26;

        // Two ruled lines of the form, under the header and over the footer.
        Rule(sheet, 0.885f);
        Rule(sheet, 0.115f);

        // The stamp: a red frame and the word, tilted.
        var stampGo = Child(sheet, "Stamp");
        stamp = stampGo;
        stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, 0.5f);
        stamp.pivot = new Vector2(0.5f, 0.5f);
        stamp.localRotation = Quaternion.Euler(0f, 0f, -7f);
        Color ink = new Color(0.72f, 0.1f, 0.1f, 1f);
        Frame(stamp, ink, 10f);
        stampText = MakeWords(stamp, "Word", 150, ink, TextAnchor.MiddleCenter);
        stampText.fontStyle = FontStyle.Bold;
        var sr = stampText.rectTransform;
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.offsetMin = new Vector2(14f, 8f);
        sr.offsetMax = new Vector2(-14f, -8f);
        stampText.resizeTextForBestFit = true;
        stampText.resizeTextMinSize = 24;
        stampText.resizeTextMaxSize = 150;

        // A soft flash as the stamp lands (no sparks, no shake).
        var flashRect = Child(canvasGo.transform, "Flash");
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
        flash = flashRect.gameObject.AddComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;

        canvasGo.SetActive(false);
    }

    static RectTransform Child(Transform parent, string name) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Rule(RectTransform parent, float y) {
        var r = Child(parent, "Rule");
        r.anchorMin = new Vector2(0.04f, y);
        r.anchorMax = new Vector2(0.96f, y);
        r.sizeDelta = new Vector2(0f, 3f);
        var image = r.gameObject.AddComponent<Image>();
        image.color = new Color(0.35f, 0.3f, 0.22f, 0.45f);
        image.raycastTarget = false;
    }

    static void Frame(RectTransform parent, Color color, float thick) {
        for (int i = 0; i < 4; i++) {
            var bar = Child(parent, "Frame");
            bool horizontal = i < 2;
            bool low = (i % 2) == 0;
            bar.anchorMin = horizontal ? new Vector2(0f, low ? 0f : 1f) : new Vector2(low ? 0f : 1f, 0f);
            bar.anchorMax = horizontal ? new Vector2(1f, low ? 0f : 1f) : new Vector2(low ? 0f : 1f, 1f);
            bar.sizeDelta = horizontal ? new Vector2(0f, thick) : new Vector2(thick, 0f);
            var image = bar.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }

    static Text MakeWords(Transform parent, string name, int size, Color color, TextAnchor anchor) {
        var rect = Child(parent, name);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = UiSkin.Font(UiSkin.FontRole.Label);
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    // Warm file paper with fibre grain, made once.
    static Sprite paperSprite;
    static Sprite PaperSprite() {
        if (paperSprite != null) {
            return paperSprite;
        }
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        var rng = new System.Random(12);
        for (int y = 0; y < n; y++) {
            for (int x = 0; x < n; x++) {
                float grain = (float)rng.NextDouble() * 0.05f;
                float fibre = ((x * 7 + y * 3) % 23 == 0) ? 0.025f : 0f;
                float v = 0.93f - grain - fibre;
                tex.SetPixel(x, y, new Color(v, v * 0.95f, v * 0.82f, 1f));
            }
        }
        tex.Apply();
        paperSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return paperSprite;
    }

    static Sprite edgeSprite;
    static Sprite EdgeSprite() {
        if (edgeSprite != null) {
            return edgeSprite;
        }
        const int n = 32;
        var tex = new Texture2D(n, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < n; x++) {
            float a = Mathf.Pow(x / (n - 1f), 2f) * 0.45f;
            tex.SetPixel(x, 0, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();
        edgeSprite = Sprite.Create(tex, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f), 100f);
        return edgeSprite;
    }
}
