using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The shop between runs. Coins bought here are already in MetaProgress.
public class MainMenu : MonoBehaviour {
    Font font;
    Text coinText;
    Text bestText;
    Text purseText;
    readonly Text[] titles = new Text[MetaProgress.Catalog.Length];
    readonly Text[] abouts = new Text[MetaProgress.Catalog.Length];
    readonly Text[] ranks = new Text[MetaProgress.Catalog.Length];
    readonly Text[] costs = new Text[MetaProgress.Catalog.Length];
    readonly Button[] buttons = new Button[MetaProgress.Catalog.Length];

    void Awake() {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        MetaProgress.BeginRun();
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Build();
        Refresh();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) {
            Play();
        }
    }

    void Play() {
        SceneManager.LoadScene(MetaProgress.GameScene);
    }

    void Build() {
        var canvasGo = new GameObject("Menu Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var backdrop = Image(canvasGo.transform, new Color(0.04f, 0.05f, 0.08f, 1f));
        Stretch(backdrop.rectTransform);

        var left = Panel(canvasGo.transform, new Color(0.07f, 0.08f, 0.11f, 0.94f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(620f, -120f));
        Label(left, "Breaking Through", 64, FontStyle.Bold, new Color(1f, 0.96f, 0.86f), new Vector2(36f, -36f), new Vector2(540f, 80f), TextAnchor.UpperLeft);
        Label(left, "Protect the musician. Build between the big waves. Coins buy a stronger start.", 24, FontStyle.Normal, new Color(1f, 0.9f, 0.72f, 0.9f), new Vector2(36f, -124f), new Vector2(540f, 80f), TextAnchor.UpperLeft);

        coinText = Label(left, "", 36, FontStyle.Bold, new Color(1f, 0.84f, 0.35f), new Vector2(36f, -220f), new Vector2(540f, 48f), TextAnchor.UpperLeft);
        bestText = Label(left, "", 28, FontStyle.Normal, new Color(0.85f, 0.95f, 0.8f), new Vector2(36f, -274f), new Vector2(540f, 40f), TextAnchor.UpperLeft);
        purseText = Label(left, "", 24, FontStyle.Normal, new Color(1f, 0.96f, 0.88f, 0.88f), new Vector2(36f, -330f), new Vector2(540f, 120f), TextAnchor.UpperLeft);

        var play = Button(left, "Play", new Vector2(36f, -500f), new Vector2(280f, 72f), 32, Play);
        var playImage = play.GetComponent<Image>();
        playImage.color = new Color(0.22f, 0.38f, 0.22f, 1f);

        Label(left, "Enter or click Play. After a run, M brings you back here.", 22, FontStyle.Italic, new Color(1f, 0.96f, 0.88f, 0.7f), new Vector2(36f, -590f), new Vector2(540f, 70f), TextAnchor.UpperLeft);

        var right = Panel(canvasGo.transform, new Color(0.08f, 0.07f, 0.06f, 0.94f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-72f, 0f), new Vector2(980f, -120f));
        Label(right, "Upgrades", 42, FontStyle.Bold, new Color(1f, 0.94f, 0.8f), new Vector2(32f, -24f), new Vector2(900f, 56f), TextAnchor.UpperLeft);

        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            float y = -90f - i * 76f;
            var row = Panel(right, new Color(0.12f, 0.1f, 0.08f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-48f, 72f));
            UpgradeInfo info = MetaProgress.Catalog[i];
            titles[i] = Label(row, info.Name, 26, FontStyle.Bold, new Color(1f, 0.95f, 0.84f), new Vector2(18f, -8f), new Vector2(520f, 32f), TextAnchor.UpperLeft);
            abouts[i] = Label(row, info.About, 20, FontStyle.Normal, new Color(1f, 0.92f, 0.8f, 0.8f), new Vector2(18f, -42f), new Vector2(560f, 36f), TextAnchor.UpperLeft);
            ranks[i] = Label(row, "", 20, FontStyle.Normal, new Color(0.8f, 0.92f, 0.7f), new Vector2(500f, -8f), new Vector2(250f, 32f), TextAnchor.UpperRight);
            int captured = i;
            buttons[i] = Button(row, "Buy", new Vector2(-168f, -18f), new Vector2(150f, 52f), 22, () => Buy(captured));
            var buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-16f, -8f);
            costs[i] = buttons[i].GetComponentInChildren<Text>();
        }
    }

    void Buy(int index) {
        if (MetaProgress.TryBuy(MetaProgress.Catalog[index].Id)) {
            GameAudio.Play("shot", 0.35f);
        }
        Refresh();
    }

    void Refresh() {
        coinText.text = MetaProgress.Coins + (MetaProgress.Coins == 1 ? " coin" : " coins");
        bestText.text = MetaProgress.BestWave > 0 ? "Best wave cleared: " + MetaProgress.BestWave : "No wave cleared yet.";
        purseText.text = "Kill hens. Every " + MetaProgress.RangeLabel() + " drops a coin. Coins stay when you fall. Progress toward the next coin is " + MetaProgress.HensToward + " / " + MetaProgress.NextCoinAt + ".";

        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            UpgradeInfo info = MetaProgress.Catalog[i];
            int rank = MetaProgress.Rank(info.Id);
            bool maxed = rank >= info.MaxRank;
            ranks[i].text = rank + " / " + info.MaxRank;
            abouts[i].text = info.About + "  " + (rank > 0 ? MetaProgress.RankLine(info.Id) : "");
            costs[i].text = maxed ? "Maxed" : "Buy  " + MetaProgress.Cost(info.Id);
            buttons[i].interactable = MetaProgress.CanBuy(info.Id);
        }
    }

    Text Label(Transform parent, string value, int size, FontStyle style, Color color, Vector2 position, Vector2 sizeDelta, TextAnchor anchor) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }

    Image Image(Transform parent, Color color) {
        var go = new GameObject("Image");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    RectTransform Panel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 sizeDelta) {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    Button Button(Transform parent, string label, Vector2 position, Vector2 size, int fontSize, UnityEngine.Events.UnityAction click) {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.28f, 0.22f, 0.12f, 1f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.08f, 0.85f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.45f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        button.colors = colors;
        button.onClick.AddListener(click);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.96f, 0.86f);
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }
}
