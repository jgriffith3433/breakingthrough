using UnityEngine;
using UnityEngine.UI;

// The one line a chapter's download shows: "Filing Chapter 2... 41%" on a night-blue plate with a thin bar under it, at the bottom
// of the screen inside the safe area (clear of a phone's home bar), over whatever is up. It is the Department's own word for
// the wait, never a spinner, and it takes no taps (the screen under it stays alive: nothing freezes while the files arrive).
// Shown by OnDemand.Need, hidden when the last fetch is in. Draws on its own overlay canvas, above the menus and cards.
public class OnDemandLine : MonoBehaviour {
    static OnDemandLine instance;

    public static bool IsUp { get { return instance != null && instance.root != null && instance.root.activeSelf; } }
    public static string Shown { get { return instance != null && instance.text != null ? instance.text.text : ""; } }
    public static int Showings { get; private set; }
    // The plate's rectangle in canvas units (x, y from the bottom-left, width, height) and the canvas's size, for the layout probe.
    public static Rect PlateRect { get { return instance != null ? instance.lastRect : new Rect(); } }
    public static Vector2 CanvasSize { get { return UiFit.Size; } }
    public static float SafeBottom { get { return UiFit.SafeInsets().w; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
        Showings = 0;
    }

    GameObject root;
    CanvasScaler scaler;
    RectTransform plate;
    RectTransform fill;
    Text text;
    string label = "";
    int stamp = -1;
    Rect lastRect;
    float shownAt;

    public static void Show(string label) {
        if (instance == null) {
            var go = new GameObject("On Demand Line");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<OnDemandLine>();
            instance.Build();
        }
        instance.label = string.IsNullOrEmpty(label) ? "Filing the chapter" : label;
        instance.root.SetActive(true);
        instance.shownAt = Time.unscaledTime;
        instance.stamp = -1;
        instance.Refresh();
        Showings++;
    }

    public static void Hide() {
        if (instance != null && instance.root != null) {
            instance.root.SetActive(false);
        }
    }

    void Build() {
        root = new GameObject("Canvas");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        scaler = root.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);

        var plateGo = new GameObject("Plate");
        plateGo.transform.SetParent(root.transform, false);
        plate = plateGo.AddComponent<RectTransform>();
        var plateImage = plateGo.AddComponent<Image>();
        UiSkin.Round(plateImage);
        plateImage.color = new Color(0.04f, 0.05f, 0.1f, 0.9f);
        plateImage.raycastTarget = false;

        var textGo = new GameObject("Line");
        textGo.transform.SetParent(plateGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(18f, 12f);
        textRect.offsetMax = new Vector2(-18f, -4f);
        text = textGo.AddComponent<Text>();
        text.font = UiSkin.Font(UiSkin.FontRole.Label);
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.92f, 0.72f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var trackGo = new GameObject("Track");
        trackGo.transform.SetParent(plateGo.transform, false);
        var track = trackGo.AddComponent<RectTransform>();
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.offsetMin = new Vector2(18f, 7f);
        track.offsetMax = new Vector2(-18f, 11f);
        var trackImage = trackGo.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.14f);
        trackImage.raycastTarget = false;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(trackGo.transform, false);
        fill = fillGo.AddComponent<RectTransform>();
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fillGo.AddComponent<Image>();
        fillImage.color = new Color(0.86f, 0.2f, 0.18f, 1f);   // the Department's red stamp
        fillImage.raycastTarget = false;
        root.SetActive(false);
    }

    void Update() {
        if (root == null || !root.activeSelf) {
            return;
        }
        Refresh();
    }

    // The words and the bar; only the bytes' share changes between frames, and only while the line is up.
    void Refresh() {
        if (stamp != UiFit.Stamp) {
            stamp = UiFit.Stamp;
            UiFit.Apply(scaler);
            Place();
        }
        float p = OnDemand.Progress;
        text.text = label + "... " + Mathf.RoundToInt(p * 100f) + "%";
        fill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
    }

    // Bottom middle, above the home bar, as wide as the screen's safe width allows (at most 760 units).
    void Place() {
        Vector2 size = UiFit.Size;
        Vector4 safe = UiFit.SafeInsets();
        float width = Mathf.Min(760f, size.x - 32f - safe.x - safe.y);
        float height = 60f;
        float y = safe.w + 20f;
        float x = (safe.x - safe.y) * 0.5f;
        plate.anchorMin = new Vector2(0.5f, 0f);
        plate.anchorMax = new Vector2(0.5f, 0f);
        plate.pivot = new Vector2(0.5f, 0f);
        plate.anchoredPosition = new Vector2(x, y);
        plate.sizeDelta = new Vector2(width, height);
        lastRect = new Rect(size.x * 0.5f + x - width * 0.5f, y, width, height);
    }
}
