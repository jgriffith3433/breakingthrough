using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pick one of three boons. Opens on a level up or a treasure chest and pauses the hill.
public class LevelUpScreen : MonoBehaviour {
    public static bool IsOpen { get; private set; }
    // The click that picked a card should not also loose an arrow.
    public static bool HoldFire { get; private set; }

    Font font;
    GameObject root;
    Text heading;
    Text footer;
    readonly Card[] cards = new Card[3];
    List<BoonId> offer = new List<BoonId>();
    bool fromChest;
    float openedAt;

    class Card {
        public GameObject Go;
        public Image Halo;
        public Image Frame;
        public Image Face;
        public Text Name;
        public Text Rank;
        public Text About;
        public Text Key;
        public Text Tag;
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
        IsOpen = false;
        HoldFire = false;
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<LevelUpScreen>() != null) {
            return;
        }
        new GameObject("Level Up").AddComponent<LevelUpScreen>();
    }

    static LevelUpScreen instance;

    // The autopilot's hand. -1 picks a random card.
    public static void DevPick(int index) {
        if (instance == null || !IsOpen || instance.offer.Count == 0) {
            return;
        }
        instance.Choose(index < 0 ? Random.Range(0, instance.offer.Count) : Mathf.Min(index, instance.offer.Count - 1));
    }

    void Awake() {
        instance = this;
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Build();
        root.SetActive(false);
    }

    void OnDestroy() {
        if (IsOpen) {
            IsOpen = false;
            Time.timeScale = 1f;
        }
    }

    void Update() {
        if (HoldFire && !Input.GetMouseButton(0)) {
            HoldFire = false;
        }
        if (!IsOpen) {
            if (WaveDirector.IsLost || root == null) {
                return;
            }
            if (RunBoons.TakePendingChest()) {
                Open(true);
            } else if (RunBoons.TakePendingLevel()) {
                Open(false);
            }
            return;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Cards rise in, and rare halos breathe.
        float rise = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.3f);
        for (int i = 0; i < cards.Length; i++) {
            if (cards[i] == null || !cards[i].Go.activeSelf) {
                continue;
            }
            float stagger = Mathf.Clamp01(rise * 1.4f - i * 0.15f);
            float ease = 1f - (1f - stagger) * (1f - stagger);
            Vector2 home = new Vector2((i - 1) * 470f, 0f);
            cards[i].Frame.rectTransform.anchoredPosition = home + Vector2.down * (1f - ease) * 120f;
            cards[i].Halo.rectTransform.anchoredPosition = cards[i].Frame.rectTransform.anchoredPosition;
            Color halo = cards[i].Halo.color;
            halo.a = Mathf.Lerp(0.15f, 0.55f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f + i)) * ease;
            cards[i].Halo.color = halo;
        }
        if (Time.unscaledTime - openedAt < 0.35f) {
            return;
        }
        for (int i = 0; i < offer.Count; i++) {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) {
                Choose(i);
                return;
            }
        }
        if (Input.GetKeyDown(KeyCode.R)) {
            Reroll();
        }
    }

    void Open(bool chest) {
        fromChest = chest;
        offer = RunBoons.Roll(3, chest);
        if (offer.Count == 0) {
            // Everything is maxed. Pay out instead.
            Musician.Instance?.Heal(10);
            return;
        }
        IsOpen = true;
        openedAt = Time.unscaledTime;
        Time.timeScale = 0f;
        root.SetActive(true);
        heading.text = chest ? "Treasure!  Pick one" : "Level " + RunBoons.Level + "!  Pick one";
        heading.color = chest ? new Color(1f, 0.82f, 0.3f) : new Color(0.55f, 0.95f, 1f);
        Fill();
        GameAudio.Play("coin", 0.7f, chest ? 0.9f : 1.15f);
    }

    void Fill() {
        for (int i = 0; i < cards.Length; i++) {
            Card card = cards[i];
            if (i >= offer.Count) {
                card.Go.SetActive(false);
                card.Halo.gameObject.SetActive(false);
                continue;
            }
            card.Go.SetActive(true);
            card.Halo.gameObject.SetActive(true);
            BoonInfo info = RunBoons.Info(offer[i]);
            Color tint = RunBoons.RarityColor(info.Rarity);
            card.Frame.color = tint;
            card.Halo.color = new Color(tint.r, tint.g, tint.b, info.Rarity == Rarity.Common ? 0.18f : 0.45f);
            card.Face.color = Color.Lerp(new Color(0.07f, 0.06f, 0.08f, 0.97f), tint, 0.12f);
            card.Name.text = info.Name;
            card.Name.color = Color.Lerp(Color.white, tint, 0.5f);
            card.Rank.text = RunBoons.RankText(info.Id);
            card.About.text = info.About;
            card.Tag.text = info.Rarity.ToString().ToUpper();
            card.Tag.color = tint;
            card.Key.text = "[" + (i + 1) + "]";
        }
        footer.text = RunBoons.Rerolls > 0
            ? "1 2 3 or click to choose.   R rerolls (" + RunBoons.Rerolls + " left)"
            : "1 2 3 or click to choose.";
    }

    void Reroll() {
        if (!RunBoons.SpendReroll()) {
            return;
        }
        offer = RunBoons.Roll(3, fromChest);
        Fill();
        GameAudio.Play("shot", 0.4f, 1.4f);
    }

    void Choose(int index) {
        if (!IsOpen || index < 0 || index >= offer.Count) {
            return;
        }
        if (Time.unscaledTime - openedAt < 0.35f) {
            return;
        }
        BoonInfo info = RunBoons.Info(offer[index]);
        RunBoons.Take(offer[index]);
        IsOpen = false;
        HoldFire = true;
        root.SetActive(false);
        Time.timeScale = WaveDirector.IsLost ? 0f : 1f;
        UpgradeLook.Say(info.Name + (info.Evolution ? "  ★" : ""), 1.6f, 200f);
        if (PlayerGrowth.Instance != null) {
            Vector3 at = PlayerGrowth.Instance.transform.position;
            Color tint = RunBoons.RarityColor(info.Rarity);
            Fx.Ring(at, 6f * PlayerGrowth.Instance.Scale, new Color(tint.r, tint.g, tint.b, 0.9f), 0.6f);
        }
        GameAudio.Play("coin", 0.8f, info.Evolution ? 0.7f : 1.3f);
    }

    void Build() {
        var canvasGo = new GameObject("Boon Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(canvasGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.72f);
        Stretch(dim.rectTransform);

        heading = MakeText(root.transform, "Level up", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1400f, 90f));

        footer = MakeText(root.transform, "", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
        footer.color = new Color(1f, 0.95f, 0.85f, 0.85f);
        Place(footer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(1400f, 40f));

        for (int i = 0; i < cards.Length; i++) {
            cards[i] = MakeCard(root.transform, i);
        }
    }

    Card MakeCard(Transform parent, int index) {
        var card = new Card();
        var haloGo = new GameObject("Halo " + (index + 1));
        haloGo.transform.SetParent(parent, false);
        card.Halo = haloGo.AddComponent<Image>();
        card.Halo.sprite = UiSkin.Glow;
        card.Halo.type = Image.Type.Sliced;
        card.Halo.raycastTarget = false;
        Place(card.Halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((index - 1) * 470f, 0f), new Vector2(520f, 590f));
        card.Go = new GameObject("Card " + (index + 1));
        card.Go.transform.SetParent(parent, false);
        card.Frame = card.Go.AddComponent<Image>();
        UiSkin.Round(card.Frame);
        Place(card.Frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((index - 1) * 470f, 0f), new Vector2(430f, 500f));
        var button = card.Go.AddComponent<Button>();
        button.targetGraphic = card.Frame;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        int captured = index;
        button.onClick.AddListener(() => Choose(captured));

        var faceGo = new GameObject("Face");
        faceGo.transform.SetParent(card.Go.transform, false);
        card.Face = faceGo.AddComponent<Image>();
        UiSkin.Round(card.Face);
        card.Face.raycastTarget = false;
        var faceRect = card.Face.rectTransform;
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = new Vector2(6f, 6f);
        faceRect.offsetMax = new Vector2(-6f, -6f);

        card.Tag = MakeText(faceGo.transform, "", 20, FontStyle.Bold, TextAnchor.UpperCenter);
        Place(card.Tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(380f, 30f));
        card.Name = MakeText(faceGo.transform, "", 40, FontStyle.Bold, TextAnchor.UpperCenter);
        Place(card.Name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(390f, 110f));
        card.Rank = MakeText(faceGo.transform, "", 24, FontStyle.Italic, TextAnchor.UpperCenter);
        card.Rank.color = new Color(1f, 0.88f, 0.55f);
        Place(card.Rank.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(380f, 34f));
        card.About = MakeText(faceGo.transform, "", 27, FontStyle.Normal, TextAnchor.UpperCenter);
        Place(card.About.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(370f, 200f));
        card.Key = MakeText(faceGo.transform, "", 30, FontStyle.Bold, TextAnchor.LowerCenter);
        card.Key.color = new Color(1f, 1f, 1f, 0.7f);
        Place(card.Key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(380f, 40f));
        return card;
    }

    Text MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor anchor) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.9f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
