using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Right-side list of every bird in this wave, how many are left, and a bar that fills as they fall.
public class WaveRoster : MonoBehaviour {
    const float BarWidth = 300f;
    const float RowHeight = 62f;

    GameObject panel;
    RectTransform list;
    RectTransform headerRect;
    CanvasScaler scaler;
    UiMode laidOut;
    int laidStamp = -1;
    float rowHeight = RowHeight;
    Row[] rows;
    int[] total;
    int[] left;

    class Row {
        public GameObject Root;
        public Text Name;
        public Text Count;
        public RectTransform Fill;
        public Image FillImage;
    }

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
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<WaveRoster>() != null) {
            return;
        }
        var go = new GameObject("Wave Roster");
        go.AddComponent<WaveRoster>();
    }

    void Awake() {
        int kinds = WaveDirector.KindCount;
        rows = new Row[kinds];
        total = new int[kinds];
        left = new int[kinds];

        var canvasGo = new GameObject("Roster");
        panel = canvasGo;
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(canvasGo.transform, false);
        var header = headerGo.AddComponent<Text>();
        header.font = font;
        header.fontSize = 22;
        header.alignment = TextAnchor.UpperRight;
        header.color = new Color(0.79f, 0.71f, 0.54f, 1f);
        header.text = "This wave";
        header.raycastTarget = false;
        var outline = headerGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        headerRect = header.rectTransform;
        headerRect.anchorMin = new Vector2(1f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(1f, 1f);
        headerRect.anchoredPosition = new Vector2(-36f, -24f);
        headerRect.sizeDelta = new Vector2(BarWidth, 32f);

        var listGo = new GameObject("Lines");
        listGo.transform.SetParent(canvasGo.transform, false);
        list = listGo.AddComponent<RectTransform>();
        list.anchorMin = new Vector2(1f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(1f, 1f);
        list.anchoredPosition = new Vector2(-36f, -58f);
        list.sizeDelta = new Vector2(BarWidth, kinds * RowHeight);

        for (int i = 0; i < kinds; i++) {
            rows[i] = BuildRow(list, font, i);
        }
        Layout();
    }

    // Phones: top right in landscape, under the wave line on the right in portrait, with tighter rows.
    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler);
        float top = laidOut == UiMode.Desktop ? -24f : laidOut == UiMode.Wide ? -16f : -340f;
        float side = laidOut == UiMode.Desktop ? -36f : -24f;
        rowHeight = laidOut == UiMode.Desktop ? RowHeight : 52f;
        headerRect.anchoredPosition = new Vector2(side, top);
        list.anchoredPosition = new Vector2(side, top - 34f);
        list.sizeDelta = new Vector2(BarWidth, rows.Length * rowHeight);
        int fontSize = laidOut == UiMode.Desktop ? 24 : 22;
        foreach (Row row in rows) {
            row.Name.fontSize = fontSize;
            row.Count.fontSize = fontSize;
        }
    }

    Row BuildRow(RectTransform parent, Font font, int index) {
        var rowGo = new GameObject("Kind " + index);
        rowGo.transform.SetParent(parent, false);
        var rowRect = rowGo.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(1f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(1f, 1f);
        rowRect.sizeDelta = new Vector2(BarWidth, RowHeight);

        var row = new Row();
        row.Root = rowGo;
        row.Name = MakeLabel(rowGo.transform, font, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(190f, 28f));
        row.Count = MakeLabel(rowGo.transform, font, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(140f, 28f));

        var trackGo = new GameObject("Track");
        trackGo.transform.SetParent(rowGo.transform, false);
        var track = trackGo.AddComponent<Image>();
        UiSkin.Bar(track);
        track.color = new Color(0.08f, 0.07f, 0.06f, 0.88f);
        track.raycastTarget = false;
        var trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(0f, 1f);
        trackRect.anchorMax = new Vector2(0f, 1f);
        trackRect.pivot = new Vector2(0f, 1f);
        trackRect.anchoredPosition = new Vector2(0f, -32f);
        trackRect.sizeDelta = new Vector2(BarWidth, 12f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(trackGo.transform, false);
        row.FillImage = fillGo.AddComponent<Image>();
        UiSkin.Bar(row.FillImage);
        row.FillImage.raycastTarget = false;
        row.Fill = row.FillImage.rectTransform;
        row.Fill.anchorMin = new Vector2(0f, 0.5f);
        row.Fill.anchorMax = new Vector2(0f, 0.5f);
        row.Fill.pivot = new Vector2(0f, 0.5f);
        row.Fill.anchoredPosition = Vector2.zero;
        row.Fill.sizeDelta = new Vector2(0f, 12f);
        return row;
    }

    static Text MakeLabel(Transform parent, Font font, TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 size) {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorPoint;
        rect.anchorMax = anchorPoint;
        rect.pivot = anchor == TextAnchor.UpperRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    void Update() {
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        bool show = Time.timeScale > 0f && WaveDirector.Instance != null;
        if (panel != null && panel.activeSelf != show) {
            panel.SetActive(show);
        }
        if (!show) {
            return;
        }

        WaveDirector.Instance.CopyRoster(total, left);
        int shown = 0;
        for (int kind = rows.Length - 1; kind >= 0; kind--) {
            Row row = rows[kind];
            if (total[kind] <= 0) {
                row.Root.SetActive(false);
                continue;
            }
            row.Root.SetActive(true);
            var rect = (RectTransform)row.Root.transform;
            rect.anchoredPosition = new Vector2(0f, -shown * rowHeight);
            shown++;

            FowlKind fowl = (FowlKind)kind;
            FowlDef def = fowl == FowlKind.Turkey ? FowlRoster.Turkey() : FowlRoster.ForTier(FowlRoster.DebutOf(fowl));
            int remain = Mathf.Max(0, left[kind]);
            bool done = remain == 0;
            row.Name.text = def.Name;
            row.Count.text = remain + " left";
            Color ink = done ? new Color(0.55f, 0.52f, 0.46f, 0.7f) : new Color(1f, 0.97f, 0.9f, 0.96f);
            row.Name.color = ink;
            row.Count.color = ink;
            float ratio = Mathf.Clamp01((total[kind] - remain) / (float)total[kind]);
            row.Fill.sizeDelta = new Vector2(BarWidth * ratio, 12f);
            Color tint = def.Tint;
            tint.r = Mathf.Clamp01(tint.r);
            tint.g = Mathf.Clamp01(tint.g);
            tint.b = Mathf.Clamp01(tint.b);
            tint.a = done ? 0.45f : 0.95f;
            row.FillImage.color = tint;
        }
    }
}
