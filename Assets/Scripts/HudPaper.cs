using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The run HUD as the Department's paperwork (Arcade): Slice's address card (tears in stages, flutters when hit, taped
// back on heal), the coin ledger (odometer digits, cha-ching on gains), the wave postmark (re-stamped each wave with the
// act around its rim, a red seal on boss waves) and the level ink stamp (fills toward the next level).
// Art is one atlas, Resources/UiArt/hud_atlas.png (Tools/UiArt/Docs in Docs/UiArt.md). With no atlas, Active stays
// false and the old text HUD in RunHud / Musician / WaveDirector shows as before. Every change pops under 0.2 s.
public class HudPaper : MonoBehaviour {
    public static HudPaper Instance { get; private set; }
    public static bool Active { get { return Instance != null && Instance.ready; } }

    // Probe numbers for the playtest.
    public static int CardStage { get { return Instance != null ? Instance.stage : 0; } }
    public static int StageChanges { get; private set; }
    public static int Hits { get; private set; }
    public static int Heals { get; private set; }
    public static int CoinGains { get; private set; }
    public static int Restamps { get; private set; }
    public static int SealsShown { get; private set; }
    public static bool SealUp { get { return Instance != null && Instance.seal != null && Instance.seal.gameObject.activeSelf; } }
    public static int ShownCoins { get { return Instance != null ? Mathf.RoundToInt(Instance.coinShown) : 0; } }
    public static int StampLevel { get; private set; }

    // Pixel rects in hud_atlas.png, x, y from the top, w, h.
    static readonly RectInt Tape = new RectInt(58, 26, 140, 76);
    static readonly RectInt CoinIcon = new RectInt(283, 26, 76, 76);
    static readonly RectInt[] Cards = {
        new RectInt(10, 138, 238, 108), new RectInt(264, 138, 240, 108),
        new RectInt(521, 138, 240, 108), new RectInt(776, 138, 240, 108) };
    static readonly RectInt LedgerRect = new RectInt(10, 360, 364, 142);
    static readonly RectInt StampRect = new RectInt(543, 264, 196, 240);
    static readonly RectInt[] Marks = {
        new RectInt(10, 520, 238, 240), new RectInt(266, 520, 238, 240),
        new RectInt(522, 520, 238, 240), new RectInt(778, 520, 238, 240) };
    static readonly RectInt SealRect = new RectInt(48, 803, 166, 206);
    static readonly Color Ink = new Color(0.17f, 0.10f, 0.08f);
    static readonly Color[] MarkInk = {
        new Color(1f, 0.86f, 0.45f), new Color(0.7f, 0.88f, 1f), new Color(1f, 0.62f, 0.55f), new Color(1f, 0.82f, 0.4f) };

    Texture2D atlas;
    bool ready;
    CanvasScaler scaler;
    Font label;
    Font number;
    readonly Dictionary<RectInt, UnityEngine.Sprite> sprites = new Dictionary<RectInt, UnityEngine.Sprite>();

    // Slice's card.
    RectTransform cardRoot;
    Image card;
    Image tape;
    Image patch;
    Text cardName;
    Text cardNumber;
    int stage;
    int lastHealth = -1;
    float cardPop;
    float flutter;
    float patchLeft;

    // Ledger.
    RectTransform ledger;
    RectTransform window;
    Image coinIcon;
    Text gainText;
    float gainLeft;
    readonly List<Column> columns = new List<Column>();
    float coinShown = -1f;
    int coinSeen = -1;
    float ledgerPop;
    float chaDelay;

    // Postmark.
    RectTransform markRoot;
    Image mark;
    Text markNumber;
    Image seal;
    int waveSeen = -1;
    float markPop;
    float sealPop;

    // Ink stamp.
    RectTransform stampRoot;
    Image stampImage;
    Image ink;
    Text stampLevel;
    float inkShown;
    int levelSeen = -1;
    float stampPop;
    float stampSlam;

    UiMode laidOut;
    int laidStamp = -1;
    float markSize;
    float digitFont;

    class Column {
        public RectTransform root;
        public Text a;
        public Text b;
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
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene || FindAnyObjectByType<HudPaper>() != null) {
            return;
        }
        new GameObject("Paper Hud").AddComponent<HudPaper>();
    }

    void Awake() {
        Instance = this;
        StageChanges = 0;
        Hits = 0;
        Heals = 0;
        CoinGains = 0;
        Restamps = 0;
        SealsShown = 0;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Start() {
        atlas = Resources.Load<Texture2D>("UiArt/hud_atlas");
        if (atlas == null) {
            return;
        }
        atlas.wrapMode = TextureWrapMode.Clamp;
        label = UiSkin.Font(UiSkin.FontRole.Label);
        number = UiSkin.Font(UiSkin.FontRole.Number);

        var canvasGo = new GameObject("Paper Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 141;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        Transform top = canvasGo.transform;

        // Slice's card, pinned at the top middle; pivot at the pin so it swings from it.
        cardRoot = Node("Slice Card", top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        card = Pic("Card", cardRoot, Cards[0]);
        Stretch(card.rectTransform);
        patch = Pic("Tape Patch", cardRoot, Tape);
        patch.rectTransform.anchorMin = patch.rectTransform.anchorMax = new Vector2(0.82f, 0.42f);
        patch.rectTransform.localEulerAngles = new Vector3(0f, 0f, -38f);
        patch.gameObject.SetActive(false);
        tape = Pic("Tape", cardRoot, Tape);
        tape.rectTransform.anchorMin = tape.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        tape.rectTransform.localEulerAngles = new Vector3(0f, 0f, 4f);
        cardName = Txt("Name", cardRoot, label, TextAnchor.MiddleLeft, Ink);
        cardName.text = Musician.Name.ToUpperInvariant();
        cardNumber = Txt("Health", cardRoot, number, TextAnchor.MiddleRight, new Color(0.6f, 0.08f, 0.07f));

        // The ledger.
        ledger = Node("Ledger", top, new Vector2(0f, 1f), new Vector2(0f, 1f));
        var ledgerImage = Pic("Ledger Art", ledger, LedgerRect);
        Stretch(ledgerImage.rectTransform);
        coinIcon = Pic("Coin", ledger, CoinIcon);
        coinIcon.rectTransform.anchorMin = coinIcon.rectTransform.anchorMax = new Vector2(0.15f, 0.5f);
        window = Node("Digits", ledger, Vector2.zero, Vector2.one);
        gainText = Txt("Gain", ledger, number, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.4f));
        gainText.text = "";

        // The postmark and its seal.
        markRoot = Node("Postmark", top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        mark = Pic("Mark", markRoot, Marks[0]);
        Stretch(mark.rectTransform);
        markNumber = Txt("Wave", markRoot, number, TextAnchor.MiddleCenter, MarkInk[0]);
        seal = Pic("Seal", markRoot, SealRect);
        seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0.88f, 0.1f);
        seal.gameObject.SetActive(false);

        // The ink stamp.
        stampRoot = Node("Ink Stamp", top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        ink = Pic("Ink", stampRoot, new RectInt(808, 330, 160, 170));
        ink.sprite = null;
        ink.color = new Color(0.78f, 0.12f, 0.1f, 1f);
        ink.rectTransform.anchorMin = new Vector2(0.345f, 0.1f);
        ink.rectTransform.anchorMax = new Vector2(0.655f, 0.1f);
        ink.rectTransform.pivot = new Vector2(0.5f, 0f);
        stampImage = Pic("Stamp", stampRoot, StampRect);
        Stretch(stampImage.rectTransform);
        // The gauge window is opaque in the art, so the ink is drawn over it.
        ink.transform.SetAsLastSibling();
        stampLevel = Txt("Level", top, label, TextAnchor.UpperCenter, new Color(1f, 0.94f, 0.8f));
        stampLevel.rectTransform.pivot = new Vector2(0.5f, 1f);
        stampLevel.rectTransform.anchorMin = stampLevel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        var outline = stampLevel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);

        ready = true;
        if (Musician.Instance != null) {
            Musician.Instance.SetBarHidden(true);
        }
        Layout();
    }

    // ---------------------------------------------------------------- layout

    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler);
        bool desk = laidOut == UiMode.Desktop;
        bool tall = laidOut == UiMode.Tall;
        int small = desk ? 26 : 28;   // phone units are about 0.58 px: 28 stays over 16 px
        Vector2 cardSize = desk ? new Vector2(264f, 117f) : new Vector2(224f, 99f);
        Place(cardRoot, new Vector2(0f, -4f), cardSize);
        Pos(cardName, new Vector2(cardSize.x * 0.10f, cardSize.y * 0.18f), new Vector2(cardSize.x * 0.50f, cardSize.y * 0.5f), small);
        Pos(cardNumber, new Vector2(cardSize.x * 0.60f, cardSize.y * 0.18f), new Vector2(cardSize.x * 0.30f, cardSize.y * 0.5f), desk ? 40 : 34);
        Place(tape.rectTransform, new Vector2(0f, 10f), new Vector2(cardSize.x * 0.30f, cardSize.x * 0.30f * 76f / 140f));
        Place(patch.rectTransform, Vector2.zero, new Vector2(cardSize.x * 0.30f, cardSize.x * 0.30f * 76f / 140f));

        Vector2 ledgerSize = desk ? new Vector2(196f, 76f) : tall ? new Vector2(156f, 61f) : new Vector2(176f, 69f);
        Place(ledger, desk ? new Vector2(36f, -10f) : tall ? new Vector2(12f, -8f) : new Vector2(24f, -14f), ledgerSize);
        float coinSize = ledgerSize.y * 0.62f;
        Place(coinIcon.rectTransform, Vector2.zero, new Vector2(coinSize, coinSize));
        window.anchorMin = new Vector2(0.30f, 0.17f);
        window.anchorMax = new Vector2(0.975f, 0.89f);
        window.offsetMin = window.offsetMax = Vector2.zero;
        Place(gainText.rectTransform, new Vector2(ledgerSize.x * 0.30f, -ledgerSize.y - 2f), new Vector2(ledgerSize.x, 34f));
        gainText.rectTransform.anchorMin = gainText.rectTransform.anchorMax = new Vector2(0f, 0f);
        gainText.rectTransform.pivot = new Vector2(0f, 1f);
        gainText.fontSize = small;
        BuildColumns(ledgerSize);

        markSize = desk ? 124f : 96f;
        Place(markRoot, new Vector2(cardSize.x * 0.5f + 10f + markSize * 0.5f, -6f), new Vector2(markSize, markSize));
        Pos(markNumber, Vector2.zero, new Vector2(markSize * 0.55f, markSize * 0.45f), desk ? 40 : 30);
        markNumber.rectTransform.anchorMin = markNumber.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        markNumber.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        markNumber.rectTransform.anchoredPosition = new Vector2(0f, -markSize * 0.04f);
        Place(seal.rectTransform, Vector2.zero, new Vector2(markSize * 0.42f, markSize * 0.42f * 206f / 166f));

        Vector2 stampSize = desk ? new Vector2(62f, 76f) : new Vector2(46f, 56f);
        if (tall) {
            // Portrait: under the ledger, where the top-left corner has room.
            stampRoot.anchorMin = stampRoot.anchorMax = new Vector2(0f, 1f);
            stampLevel.rectTransform.anchorMin = stampLevel.rectTransform.anchorMax = new Vector2(0f, 1f);
            stampRoot.pivot = new Vector2(0f, 1f);
            Place(stampRoot, new Vector2(14f, -76f), stampSize);
            stampLevel.rectTransform.pivot = new Vector2(0f, 1f);
            Place(stampLevel.rectTransform, new Vector2(14f + stampSize.x + 6f, -92f), new Vector2(120f, 34f));
            stampLevel.alignment = TextAnchor.UpperLeft;
        } else {
            stampRoot.anchorMin = stampRoot.anchorMax = new Vector2(0.5f, 1f);
            stampRoot.pivot = new Vector2(0.5f, 1f);
            stampLevel.rectTransform.anchorMin = stampLevel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            stampLevel.rectTransform.pivot = new Vector2(0.5f, 1f);
            stampLevel.alignment = TextAnchor.UpperCenter;
            float sx = -(cardSize.x * 0.5f + 10f + stampSize.x * 0.5f);
            Place(stampRoot, new Vector2(sx, -6f), stampSize);
            Place(stampLevel.rectTransform, new Vector2(sx, -6f - stampSize.y - 2f), new Vector2(120f, 34f));
        }
        stampLevel.fontSize = small;
        ink.rectTransform.sizeDelta = new Vector2(0f, 0f);
    }

    void BuildColumns(Vector2 ledgerSize) {
        for (int i = 0; i < columns.Count; i++) {
            Destroy(columns[i].root.gameObject);
        }
        columns.Clear();
        int cols = 6;
        float w = ledgerSize.x * 0.675f;
        float h = ledgerSize.y * 0.72f;
        float colW = w / cols;
        digitFont = Mathf.Clamp(Mathf.Min(colW * 1.45f, h * 0.95f), 28f, 44f);
        for (int i = 0; i < cols; i++) {
            var col = new Column();
            var go = new GameObject("Col", typeof(RectTransform), typeof(RectMask2D));
            go.transform.SetParent(window, false);
            col.root = (RectTransform)go.transform;
            col.root.anchorMin = col.root.anchorMax = new Vector2(0f, 0.5f);
            col.root.pivot = new Vector2(0f, 0.5f);
            col.root.sizeDelta = new Vector2(colW, h);
            // Column 0 is the units digit, drawn at the right.
            col.root.anchoredPosition = new Vector2(colW * (cols - 1 - i), 0f);
            col.a = Txt("A", col.root, number, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.4f));
            col.b = Txt("B", col.root, number, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.4f));
            foreach (Text t in new[] { col.a, col.b }) {
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(colW + 6f, h + 6f);
                t.fontSize = Mathf.RoundToInt(digitFont);
            }
            columns.Add(col);
        }
    }

    // ---------------------------------------------------------------- per frame

    void Update() {
        if (!ready) {
            return;
        }
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        float dt = Time.unscaledDeltaTime;
        bool chapter = ChapterRun.Instance != null;
        bool lost = WaveDirector.IsLost;
        TickCard(dt);
        ledger.gameObject.SetActive(!chapter);
        markRoot.gameObject.SetActive(!chapter);
        stampRoot.gameObject.SetActive(!chapter);
        stampLevel.gameObject.SetActive(!chapter && !lost);
        if (!chapter) {
            TickLedger(dt);
            TickMark(dt);
            TickStamp(dt);
        }
    }

    static float Pop(ref float t, float dt) {
        t = Mathf.Max(0f, t - dt);
        float k = t / 0.18f;
        return 1f + 0.28f * k * k;
    }

    void TickCard(float dt) {
        Musician slice = Musician.Instance;
        if (slice == null) {
            return;
        }
        int health = slice.Health;
        float ratio = slice.MaxHealth <= 0 ? 0f : health / (float)slice.MaxHealth;
        int now = ratio > 0.75f ? 0 : ratio > 0.5f ? 1 : ratio > 0.25f ? 2 : 3;
        if (lastHealth >= 0 && health < lastHealth) {
            Hits++;
            cardPop = 0.18f;
            flutter = 1f;
            patchLeft = 0f;
            if (now > stage) {
                StageChanges++;
                flutter = 1.6f;
                GameAudio.PlayUI("paper", 0.8f, 0.9f + 0.08f * now);
            }
        } else if (lastHealth >= 0 && health > lastHealth) {
            Heals++;
            cardPop = 0.18f;
            patchLeft = 3.5f;
            GameAudio.PlayUI("paper_slide", 0.7f, 1.2f);
        }
        stage = now;
        lastHealth = health;
        card.sprite = Cut(Cards[stage]);
        card.color = slice.Alive ? Color.white : new Color(0.6f, 0.55f, 0.55f, 1f);
        cardNumber.text = health.ToString();
        cardNumber.color = ratio <= 0.25f ? new Color(0.78f, 0.06f, 0.05f) : new Color(0.5f, 0.07f, 0.06f);
        // Taped over the tear for a few seconds after a heal, then the tape lifts off (the card stays as torn).
        patchLeft = Mathf.Max(0f, patchLeft - dt);
        patch.gameObject.SetActive(patchLeft > 0f && stage > 0);
        tape.gameObject.SetActive(true);
        flutter = Mathf.MoveTowards(flutter, 0f, dt * 1.6f);
        float swing = Mathf.Sin((1f - flutter) * 18f) * flutter * 7f;
        cardRoot.localEulerAngles = new Vector3(0f, 0f, swing);
        cardRoot.localScale = Vector3.one * Pop(ref cardPop, dt);
    }

    void TickLedger(float dt) {
        int coins = MetaProgress.Coins;
        if (coinSeen < 0) {
            coinSeen = coins;
            coinShown = coins;
        }
        if (coins > coinSeen) {
            CoinGains++;
            ledgerPop = 0.18f;
            gainLeft = 1.3f;
            gainText.text = "+" + (coins - coinSeen);
            GameAudio.PlayUI("coin", 0.9f, 1f);
            chaDelay = 0.09f;
        } else if (coins < coinSeen) {
            coinShown = coins;   // spent in the shop: no roll, it just reads true
        }
        coinSeen = coins;
        if (chaDelay > 0f) {
            chaDelay -= dt;
            if (chaDelay <= 0f) {
                GameAudio.PlayUI("coin", 0.7f, 1.4f);
            }
        }
        float diff = coins - coinShown;
        if (Mathf.Abs(diff) > 0.001f) {
            coinShown = Mathf.MoveTowards(coinShown, coins, Mathf.Max(6f, Mathf.Abs(diff) * 3.2f) * dt);
        }
        gainLeft = Mathf.Max(0f, gainLeft - dt);
        gainText.color = new Color(1f, 0.88f, 0.4f, Mathf.Clamp01(gainLeft / 0.4f));
        ledger.localScale = Vector3.one * Pop(ref ledgerPop, dt);

        float s = Mathf.Min(coinShown, 999999f);
        int digitsNeeded = Mathf.Max(3, Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1f, coins))) + 1);
        float h = columns[0].root.rect.height;
        float place = 1f;
        for (int i = 0; i < columns.Count; i++) {
            Column col = columns[i];
            bool used = i < digitsNeeded || s >= place;
            col.root.gameObject.SetActive(used);
            long whole = (long)Mathf.Floor(s / place);
            float f;
            if (i == 0) {
                f = s - Mathf.Floor(s);
            } else {
                float lower = (s % place) / place;   // 0..1 through the lower digits
                f = Mathf.Clamp01((lower - 0.9f) / 0.1f);
            }
            col.a.text = (whole % 10).ToString();
            col.b.text = ((whole + 1) % 10).ToString();
            col.a.rectTransform.anchoredPosition = new Vector2(0f, f * h);
            col.b.rectTransform.anchoredPosition = new Vector2(0f, f * h - h);
            place *= 10f;
        }
    }

    void TickMark(float dt) {
        int wave = WaveDirector.Wave;
        if (wave != waveSeen) {
            bool first = waveSeen < 0;
            waveSeen = wave;
            int act = Tuning.IsOvertime(wave) ? 3 : Mathf.Clamp(Tuning.ActOf(wave), 0, 2);
            mark.sprite = Cut(Marks[act]);
            markNumber.color = MarkInk[act];
            markNumber.text = wave.ToString();
            markRoot.localEulerAngles = new Vector3(0f, 0f, Random.Range(-9f, 9f));
            bool boss = wave % 10 == 0;
            if (!first) {
                Restamps++;
                markPop = 0.18f;
                GameAudio.PlayUI("stamp", 0.8f, 1f);
            }
            seal.gameObject.SetActive(boss);
            if (boss) {
                SealsShown++;
                sealPop = 0.18f;
                if (!first) {
                    GameAudio.PlayUI("stamp_heavy", 0.8f, 1f);
                }
            }
        }
        float k = markPop / 0.18f;
        markRoot.localScale = Vector3.one * (1f + 0.55f * k);
        markRoot.GetComponentInChildren<Image>().color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.4f - k));
        markPop = Mathf.Max(0f, markPop - dt);
        seal.rectTransform.localScale = Vector3.one * (1f + 1.2f * (sealPop / 0.18f));
        sealPop = Mathf.Max(0f, sealPop - dt);
    }

    void TickStamp(float dt) {
        int level = RunBoons.Level;
        float target = RunBoons.LevelProgress;
        if (levelSeen < 0) {
            levelSeen = level;
        }
        if (level > levelSeen) {
            // Slammed down on the page: a heavy stamp, the ink starts over.
            stampPop = 0.18f;
            stampSlam = 0.18f;
            GameAudio.PlayUI("stamp", 0.8f, 0.85f);
            inkShown = 0f;
        }
        levelSeen = level;
        StampLevel = level;
        inkShown = Mathf.MoveTowards(inkShown, target, dt * 2.5f);
        if (target < inkShown - 0.3f) {
            inkShown = target;
        }
        stampLevel.text = "Lv " + level;
        Vector2 size = stampRoot.rect.size;
        float full = size.y * 0.67f;
        ink.rectTransform.sizeDelta = new Vector2(size.x * 0.0f, 0f);
        ink.rectTransform.anchorMin = new Vector2(0.35f, 0.095f);
        ink.rectTransform.anchorMax = new Vector2(0.65f, 0.095f);
        ink.rectTransform.sizeDelta = new Vector2(0f, full * Mathf.Clamp01(inkShown));
        ink.color = inkShown > 0.97f ? Color.Lerp(new Color(0.78f, 0.12f, 0.1f), new Color(1f, 0.55f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : new Color(0.78f, 0.12f, 0.1f);
        stampSlam = Mathf.Max(0f, stampSlam - dt);
        float pop = Pop(ref stampPop, dt);
        stampRoot.localScale = Vector3.one * pop;
        stampLevel.rectTransform.localScale = Vector3.one * pop;
    }

    // ---------------------------------------------------------------- building

    UnityEngine.Sprite Cut(RectInt r) {
        UnityEngine.Sprite s;
        if (!sprites.TryGetValue(r, out s)) {
            float scale = atlas.height / 1024f;
            var rect = new Rect(r.x * scale, atlas.height - (r.y + r.height) * scale, r.width * scale, r.height * scale);
            s = UnityEngine.Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprites[r] = s;
        }
        return s;
    }

    Image Pic(string name, Transform parent, RectInt rect) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = Cut(rect);
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x == 0f && anchorMax.x == 1f ? 0.5f : anchorMin.x, 1f);
        if (anchorMin == Vector2.zero && anchorMax == Vector2.one) {
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        return rect;
    }

    Text Txt(string name, Transform parent, Font font, TextAnchor align, Color color) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size) {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // A text placed by its box from the parent's lower-left.
    static void Pos(Text text, Vector2 from, Vector2 size, int fontSize) {
        text.fontSize = fontSize;
        RectTransform r = text.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = from;
        r.sizeDelta = size;
    }
}
