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
    int openedFrame = -1;
    int closedFrame = -1;
    bool inputReady;
    CanvasScaler scaler;
    RectTransform deck;
    readonly Vector2[] homes = new Vector2[3];
    GameObject reroll;
    Text rerollLabel;
    UiMode laidOut;
    int laidStamp = -1;

    class Card {
        public GameObject Go;
        public Image Halo;
        public Image Frame;
        public Image Face;
        public Image Icon;
        public Text Name;
        public Text Rank;
        public Text About;
        public Text Key;
        public Text Tag;
        public Image Back;
        public Text BackNumber;
        public Text Stamp;
        public float StampedAt;
        public CanvasGroup Group;
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
        stamping = false;
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
        openedFrame = -1;
        closedFrame = -1;
        inputReady = false;
    }

    void Start() {
        font = UiSkin.Font(UiSkin.FontRole.Body);
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Build();
        root.SetActive(false);
    }

    void OnDestroy() {
        stamping = false;
        if (IsOpen || easeBackAt >= 0f) {
            IsOpen = false;
            inputReady = false;
            easeBackAt = -1f;
            Time.timeScale = 1f;
        }
    }

    // The jackpot: the world eases to a stop under a short build-up, the three cards are dealt face down as Department forms and
    // flip one after another (rising pitch, the rarest last with a gold stamp and the biggest sound), a pick is stamped APPROVED
    // and play eases back to speed. Everything runs on unscaled time. A pick is allowed at any moment after the deal starts.
    public const float EaseDown = 0.3f;
    public const float EaseUp = 0.25f;
    const float FlipGap = 0.26f;
    const float FlipSeconds = 0.2f;
    const float FirstFlip = 0.35f;
    const float StampHold = 0.5f;
    const float HumanDelay = 0.2f;

    public static readonly List<string> Events = new List<string>();
    // For the playtest: when the screen opened, the last card flipped, the pick and the close (unscaled seconds), and the pick.
    public static float OpenedAtTime { get; private set; }
    public static float ClosedAtTime { get; private set; }
    public static bool Stamping { get { return stamping; } }
    public static int Flipped { get; private set; }

    static LevelUpScreen current { get { return instance; } }
    // For the playtest: the card objects (taps), and whether the rarest card is the last one to flip.
    public static GameObject CardObject(int index) {
        return current != null && index >= 0 && index < current.cards.Length ? current.cards[index].Go : null;
    }
    public static int CardCount { get { return current != null ? current.offer.Count : 0; } }
    public static bool RarestLast { get; private set; }
    // Boxes inside card i that must not overlap: (what, text or icon rect).
    public static List<KeyValuePair<string, RectTransform>> CardParts(int index) {
        var parts = new List<KeyValuePair<string, RectTransform>>();
        if (current == null || index < 0 || index >= current.cards.Length || !current.cards[index].Go.activeSelf) {
            return parts;
        }
        Card c = current.cards[index];
        parts.Add(new KeyValuePair<string, RectTransform>("tag", c.Tag.rectTransform));
        parts.Add(new KeyValuePair<string, RectTransform>("icon", c.Icon.rectTransform));
        parts.Add(new KeyValuePair<string, RectTransform>("name", c.Name.rectTransform));
        parts.Add(new KeyValuePair<string, RectTransform>("line", c.About.rectTransform));
        parts.Add(new KeyValuePair<string, RectTransform>("key", c.Key.rectTransform));
        return parts;
    }
    public static bool StampShown(int index) {
        return current != null && index >= 0 && index < current.cards.Length && current.cards[index].Stamp.gameObject.activeSelf && current.cards[index].Stamp.text == "APPROVED";
    }

    static bool stamping;
    float dealStart;
    float stampAt;
    int pickedIndex = -1;
    readonly float[] flipAt = new float[3];
    readonly int[] order = new int[3];
    readonly bool[] flipHeard = new bool[3];
    float easeBackAt = -1f;

    void Log(string what) {
        Events.Add((Time.unscaledTime - OpenedAtTime).ToString("0.00") + "s " + what);
    }

    void Update() {
        if (HoldFire && !PlayInput.GetMouseButton(0)) {
            HoldFire = false;
        }
        if (!IsOpen) {
            TickEaseBack();
            if (WaveDirector.IsLost || PauseMenu.IsOpen || StoryScreen.IsOpen || Cutscene.IsOpen || root == null || Time.frameCount <= closedFrame) {
                return;
            }
            if (RunBoons.TakePendingChest()) {
                Open(true);
            } else if (RunBoons.TakePendingLevel()) {
                Open(false);
            }
            return;
        }

        float now = Time.unscaledTime;
        // The world eases to a stop, and stays stopped.
        float down = Mathf.Clamp01((now - OpenedAtTime) / EaseDown);
        Time.timeScale = WaveDirector.IsLost ? 0f : Mathf.Lerp(1f, 0f, down * down * (3f - 2f * down));
        if (down >= 1f) {
            Time.timeScale = 0f;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (laidStamp != UiFit.Stamp) {
            Layout();
            Fill();
        }
        TickCards(now);
        if (stamping) {
            if (now - stampAt >= StampHold) {
                Finish();
            }
            return;
        }
        if (!inputReady || Time.frameCount <= openedFrame || now - OpenedAtTime < HumanDelay) {
            return;
        }
        for (int i = 0; i < 3 && offer.Count > 0; i++) {
            if (PlayInput.GetKeyDown(KeyCode.Alpha1 + i) || PlayInput.GetKeyDown(KeyCode.Keypad1 + i)) {
                Choose(Mathf.Min(i, offer.Count - 1));
                return;
            }
        }
        if (offer.Count > 0 && (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter))) {
            Choose(0);
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.R)) {
            Reroll();
        }
    }

    // Play eases back to full speed after a pick. A pause or a loss in that moment takes over.
    void TickEaseBack() {
        if (easeBackAt < 0f) {
            return;
        }
        if (WaveDirector.IsLost || PauseMenu.IsOpen) {
            easeBackAt = -1f;
            return;
        }
        float up = Mathf.Clamp01((Time.unscaledTime - easeBackAt) / EaseUp);
        Time.timeScale = Mathf.Lerp(0.2f, 1f, up * up * (3f - 2f * up));
        if (up >= 1f) {
            Time.timeScale = 1f;
            easeBackAt = -1f;
        }
    }

    // Deals three cards face down, then schedules each flip: rarest last.
    void BeginDeal(float delay) {
        dealStart = Time.unscaledTime + delay;
        pickedIndex = -1;
        stamping = false;
        Flipped = 0;
        for (int i = 0; i < 3; i++) {
            order[i] = i;
            flipHeard[i] = false;
            cards[i].Stamp.gameObject.SetActive(false);
        }
        // Slot order by rarity, lowest first; ties keep their slot.
        System.Array.Sort(order, (a, b) => {
            int ra = a < offer.Count ? (int)RunBoons.Info(offer[a]).Rarity : -1;
            int rb = b < offer.Count ? (int)RunBoons.Info(offer[b]).Rarity : -1;
            return ra != rb ? ra.CompareTo(rb) : a.CompareTo(b);
        });
        int rank = 0;
        RarestLast = true;
        int top = -1;
        for (int k = 0; k < 3; k++) {
            int slot = order[k];
            if (slot >= offer.Count) {
                continue;
            }
            flipAt[slot] = FirstFlip + rank * FlipGap;
            int rarity = (int)RunBoons.Info(offer[slot]).Rarity;
            RarestLast &= rarity >= top;
            top = Mathf.Max(top, rarity);
            rank++;
        }
    }

    void TickCards(float now) {
        float local = now - dealStart;
        int lastSlot = -1;
        for (int k = 0; k < 3; k++) {
            if (order[k] < offer.Count) {
                lastSlot = order[k];
            }
        }
        int heard = 0;
        for (int i = 0; i < cards.Length; i++) {
            Card card = cards[i];
            if (card == null || !card.Go.activeSelf) {
                continue;
            }
            // Dealt in from below, one after another.
            float stagger = Mathf.Clamp01((local - i * 0.08f) / 0.25f);
            float ease = 1f - (1f - stagger) * (1f - stagger);
            Vector2 at = homes[i] + Vector2.down * (1f - ease) * 140f;
            // The flip: the card turns edge-on, the face takes over halfway.
            float p = Mathf.Clamp01((local - flipAt[i]) / FlipSeconds);
            if (pickedIndex == i) {
                p = 1f;
            }
            bool faceUp = p >= 0.5f;
            float width = Mathf.Abs(Mathf.Cos(Mathf.PI * p));
            card.Go.transform.localScale = new Vector3(local < 0f ? 0f : Mathf.Max(0.02f, width), 1f, 1f);
            card.Halo.rectTransform.localScale = new Vector3(Mathf.Max(0.02f, width), 1f, 1f);
            card.Face.gameObject.SetActive(faceUp);
            card.Back.gameObject.SetActive(!faceUp);
            card.Frame.rectTransform.anchoredPosition = at;
            card.Halo.rectTransform.anchoredPosition = at;
            if (faceUp) {
                heard++;
                if (!flipHeard[i]) {
                    flipHeard[i] = true;
                    OnFlipped(i, i == lastSlot);
                }
            }
            bool rare = i < offer.Count && RunBoons.Info(offer[i]).Rarity != Rarity.Common;
            Color halo = card.Halo.color;
            float glow = !faceUp ? 0.05f : (rare ? Mathf.Lerp(0.2f, 0.6f, 0.5f + 0.5f * Mathf.Sin(now * 3f + i)) : 0.16f);
            halo.a = glow * ease * (pickedIndex >= 0 && pickedIndex != i ? 0.3f : 1f);
            card.Halo.color = halo;
            // A picked card's neighbours fall back, so the stamp is the one thing lit.
            card.Go.transform.localScale = new Vector3(card.Go.transform.localScale.x, pickedIndex >= 0 && pickedIndex != i ? 0.94f : 1f, 1f);
            card.Group.alpha = pickedIndex >= 0 && pickedIndex != i ? 0.45f : 1f;
            // The stamp thumps down: big and wide at first, then home.
            if (card.Stamp.gameObject.activeSelf) {
                float s = Mathf.Clamp01((now - card.StampedAt) / 0.14f);
                float size = Mathf.Lerp(2.6f, 1f, 1f - (1f - s) * (1f - s));
                card.Stamp.rectTransform.localScale = new Vector3(size, size, 1f);
                card.Stamp.canvasRenderer.SetAlpha(Mathf.Clamp01(s * 3f));
            }
        }
        Flipped = heard;
    }

    void OnFlipped(int slot, bool last) {
        BoonInfo info = RunBoons.Info(offer[slot]);
        int rankInOrder = System.Array.IndexOf(order, slot);
        float pitch = 0.9f + 0.14f * rankInOrder;
        if (last) {
            // The rarest, last: the biggest sound, and a gold stamp on anything above common.
            UiSound.Stamp(1f, 0.85f);
            if (info.Rarity != Rarity.Common || info.Evolution) {
                StampOn(cards[slot], info.Evolution ? "EVOLVED" : info.Rarity.ToString().ToUpper(), new Color(1f, 0.82f, 0.25f), 8f);
            }
        }
        UiSound.Flip(pitch);
        Log("flip card " + (slot + 1) + " " + info.Rarity + (last ? " (last, stamp)" : "") + " pitch " + pitch.ToString("0.00") + " sound cardflip");
    }

    static void StampOn(Card card, string word, Color color, float tilt) {
        card.Stamp.text = word;
        card.Stamp.color = color;
        card.Stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        card.Stamp.gameObject.SetActive(true);
        card.StampedAt = Time.unscaledTime;
    }

    void Open(bool chest) {
        fromChest = chest;
        offer = RunBoons.Roll(3, chest);
        if (offer.Count == 0) {
            // Everything is maxed. Pay out instead.
            IsOpen = false;
            inputReady = false;
            if (root != null) {
                root.SetActive(false);
            }
            Time.timeScale = WaveDirector.IsLost ? 0f : 1f;
            Musician.Instance?.Heal(10);
            return;
        }
        IsOpen = true;
        inputReady = true;
        easeBackAt = -1f;
        openedFrame = Time.frameCount;
        openedAt = Time.unscaledTime;
        OpenedAtTime = openedAt;
        Events.Clear();
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(null);
        }
        RunAnalytics.CardsOpen(chest);
        WallitVoice.OnLevelUp(chest);
        root.SetActive(true);
        heading.text = chest ? "Treasure!  Pick one" : "Level " + RunBoons.Level + "!  Pick one";
        heading.color = chest ? new Color(1f, 0.82f, 0.3f) : new Color(0.55f, 0.95f, 1f);
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        Fill();
        BeginDeal(0.1f);
        UiSound.BuildUp();
        TickCards(Time.unscaledTime);
        Log("open " + (chest ? "chest" : "level " + RunBoons.Level) + ", world easing to a stop, build-up sound, cards dealt face down");
    }

    void Fill() {
        bool touch = UiFit.Touch;
        for (int i = 0; i < cards.Length; i++) {
            Card card = cards[i];
            card.Stamp.gameObject.SetActive(false);
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
            card.Halo.color = new Color(tint.r, tint.g, tint.b, 0.05f);
            card.Face.color = Color.Lerp(new Color(0.07f, 0.06f, 0.08f, 0.97f), tint, 0.12f);
            card.Name.text = info.Name;
            card.Name.color = Color.Lerp(Color.white, tint, 0.5f);
            card.Rank.text = "";
            card.About.text = OneLine(info.About);
            UiArt.Show(card.Icon, UiArt.Boon(info.Id));
            card.Tag.text = info.Evolution ? "EVOLUTION" : info.Rarity.ToString().ToUpper() + "  ·  " + RunBoons.RankText(info.Id);
            card.Tag.color = tint;
            card.Key.text = touch ? "Tap to pick" : "[" + (i + 1) + "]";
            card.BackNumber.text = "FORM B-" + (7 + i * 3 + RunBoons.Level % 3);
        }
        if (touch) {
            footer.text = "Tap a card to choose.";
            reroll.SetActive(RunBoons.Rerolls > 0);
            rerollLabel.text = "Reroll (" + RunBoons.Rerolls + " left)";
            return;
        }
        reroll.SetActive(false);
        footer.text = RunBoons.Rerolls > 0
            ? "1 2 3 or click to choose.   R rerolls (" + RunBoons.Rerolls + " left)"
            : "1 2 3 or click to choose.";
    }

    // One short line: the card's first sentence.
    static string OneLine(string about) {
        int stop = about.IndexOf(". ");
        return stop > 0 ? about.Substring(0, stop + 1) : about;
    }

    void Reroll() {
        if (stamping || !RunBoons.SpendReroll()) {
            return;
        }
        RunAnalytics.Reroll();
        offer = RunBoons.Roll(3, fromChest);
        if (offer.Count == 0) {
            IsOpen = false;
            inputReady = false;
            closedFrame = Time.frameCount;
            root.SetActive(false);
            Time.timeScale = WaveDirector.IsLost ? 0f : 1f;
            Musician.Instance?.Heal(10);
            return;
        }
        inputReady = true;
        Fill();
        BeginDeal(0f);
        UiSound.Slide(true);
        Log("reroll, three new forms dealt face down");
    }

    void Choose(int index) {
        if (!IsOpen || !inputReady || offer.Count == 0 || stamping) {
            return;
        }
        index = Mathf.Clamp(index, 0, offer.Count - 1);
        BoonInfo info = RunBoons.Info(offer[index]);
        RunBoons.Take(offer[index]);
        RunAnalytics.CardTaken(fromChest, offer, index);
        inputReady = false;
        HoldFire = true;
        pickedIndex = index;
        flipHeard[index] = true;
        stamping = true;
        stampAt = Time.unscaledTime;
        Time.timeScale = 0f;
        // The picked card turns up if it was still face down, and is stamped APPROVED.
        StampOn(cards[index], "APPROVED", new Color(0.9f, 0.16f, 0.14f), -11f);
        UiSound.Stamp(0.95f, info.Evolution ? 0.75f : 1f);
        UpgradeLook.Say(info.Name + (info.Evolution ? "  ★" : ""), 1.6f, 200f);
        if (PlayerGrowth.Instance != null) {
            Vector3 at = PlayerGrowth.Instance.transform.position;
            Color tint = RunBoons.RarityColor(info.Rarity);
            Fx.Ring(at, 6f * PlayerGrowth.Instance.Scale, new Color(tint.r, tint.g, tint.b, 0.9f), 0.6f);
        }
        Log("pick card " + (index + 1) + " " + info.Name + ", APPROVED stamp, sound stamp");
    }

    // The stamp has had its beat: close, and play eases back to speed.
    void Finish() {
        stamping = false;
        IsOpen = false;
        inputReady = false;
        closedFrame = Time.frameCount;
        ClosedAtTime = Time.unscaledTime;
        root.SetActive(false);
        if (WaveDirector.IsLost) {
            Time.timeScale = 0f;
        } else {
            easeBackAt = Time.unscaledTime;
            Time.timeScale = 0.2f;
        }
        Log("closed, play easing back to speed");
    }

    void Build() {
        var canvasGo = new GameObject("Boon Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(canvasGo.transform, false);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.02f, 0.04f, 0.72f);
        Stretch(dim.rectTransform);

        // Everything but the dimmer, so phones can scale or restack it as one.
        var deckGo = new GameObject("Deck", typeof(RectTransform));
        deckGo.transform.SetParent(root.transform, false);
        deck = (RectTransform)deckGo.transform;
        deck.anchorMin = new Vector2(0.5f, 0.5f);
        deck.anchorMax = new Vector2(0.5f, 0.5f);
        deck.pivot = new Vector2(0.5f, 0.5f);
        deck.sizeDelta = new Vector2(1400f, 760f);

        heading = MakeText(deck, "Level up", 64, FontStyle.Bold, TextAnchor.MiddleCenter);
        Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1400f, 90f));

        footer = MakeText(deck, "", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
        footer.color = new Color(1f, 0.95f, 0.85f, 0.85f);
        Place(footer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(1400f, 40f));

        for (int i = 0; i < cards.Length; i++) {
            cards[i] = MakeCard(deck, i);
        }

        // Touch has no R key: a button rerolls.
        reroll = new GameObject("Reroll");
        reroll.transform.SetParent(deck, false);
        var rerollImage = reroll.AddComponent<Image>();
        UiSkin.Round(rerollImage, 0.6f);
        rerollImage.color = new Color(0.28f, 0.22f, 0.12f, 1f);
        var rerollButton = reroll.AddComponent<Button>();
        rerollButton.targetGraphic = rerollImage;
        rerollButton.onClick.AddListener(Reroll);
        MenuButtonFx.Add(rerollButton);
        rerollLabel = MakeText(reroll.transform, "Reroll", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        Stretch(rerollLabel.rectTransform);
        reroll.SetActive(false);
        Layout();
    }

    // Desktop: the row of three as drawn. Wide (a phone held sideways): the row of three at full size, sized to the safe
    // area's height instead of scaled down, so no text shrinks. Tall: the cards stack, each laid out across. Both phone
    // layouts sit inside Screen.safeArea (UiFit.SafeInsets); no touch pad is up while the cards are.
    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler, 0.5f);
        Vector2 size = UiFit.Size;
        Vector4 safe = laidOut == UiMode.Desktop ? Vector4.zero : UiFit.SafeInsets();
        bool tall = laidOut == UiMode.Tall;
        bool wide = laidOut == UiMode.Wide;
        float width = size.x - safe.x - safe.y - 40f;
        float height = size.y - safe.z - safe.w - (wide ? 20f : 40f);
        float scale = tall ? Mathf.Min(1f, height / 1290f) : 1f;
        deck.localScale = new Vector3(scale, scale, 1f);
        // The deck's middle is the safe area's middle.
        deck.anchoredPosition = laidOut == UiMode.Desktop ? Vector2.zero : new Vector2((safe.x - safe.y) * 0.5f, (safe.w - safe.z) * 0.5f);
        deck.sizeDelta = tall ? new Vector2(size.x, 1290f) : (wide ? new Vector2(Mathf.Max(width, 600f), height) : new Vector2(1400f, 760f));

        float top = wide ? height * 0.5f : 0f;
        Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, tall ? 545f : (wide ? top - 32f : 330f)), new Vector2(tall ? width : (wide ? width : 1400f), wide ? 64f : 90f));
        heading.fontSize = tall ? 52 : (wide ? 48 : 64);
        // Wide: the footer sits bottom left and Reroll bottom right on one row, 88 units tall.
        float rowY = wide ? -top + 44f : 0f;
        Place(footer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(wide ? -150f : 0f, tall ? -510f : (wide ? rowY : -330f)), new Vector2(tall ? width : (wide ? Mathf.Max(300f, width - 330f) : 1400f), wide ? 80f : 40f));
        footer.fontSize = wide ? 24 : 26;
        Vector2 rerollAt = tall ? new Vector2(0f, -592f) : (wide ? new Vector2(Mathf.Max(300f, width) * 0.5f - 150f, rowY) : new Vector2(540f, -340f));
        Place((RectTransform)reroll.transform, new Vector2(0.5f, 0.5f), rerollAt, new Vector2(300f, wide ? 88f : 96f));

        float cardWidth = wide ? Mathf.Min(430f, (width - 32f) / 3f) : size.x - 40f - safe.x - safe.y;
        float cardHeight = wide ? Mathf.Clamp(height - 70f - 100f, 380f, 500f) : 300f;
        float cardTop = wide ? top - 70f : 0f;
        for (int i = 0; i < cards.Length; i++) {
            Card card = cards[i];
            Vector2 frame = tall ? new Vector2(cardWidth, cardHeight) : (wide ? new Vector2(cardWidth, cardHeight) : new Vector2(430f, 500f));
            if (tall) {
                homes[i] = new Vector2(0f, (1 - i) * 330f);
            } else if (wide) {
                homes[i] = new Vector2((i - 1) * (cardWidth + 16f), cardTop - cardHeight * 0.5f);
            } else {
                homes[i] = new Vector2((i - 1) * 470f, 0f);
            }
            Place(card.Frame.rectTransform, new Vector2(0.5f, 0.5f), homes[i], frame);
            Place(card.Halo.rectTransform, new Vector2(0.5f, 0.5f), homes[i], frame + new Vector2(90f, 90f));
            float inner = frame.x - 40f;
            card.Rank.gameObject.SetActive(false);
            if (tall) {
                // Stacked: the icon on the left, the words in a column beside it.
                float col = inner - 170f;
                float colX = 95f;
                Place(card.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-frame.x * 0.5f + 95f, -64f), new Vector2(150f, 150f));
                Lay(card.Tag, 22, new Vector2(colX, -14f), new Vector2(col, 30f));
                Lay(card.Name, 36, new Vector2(colX, -48f), new Vector2(col, 50f));
                Lay(card.About, 26, new Vector2(colX, -104f), new Vector2(col, 130f));
                Lay(card.Key, 24, new Vector2(colX, 12f), new Vector2(col, 32f));
            } else if (wide) {
                float iconSize = 84f;
                Lay(card.Tag, 20, new Vector2(0f, -10f), new Vector2(inner, 28f));
                Place(card.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(iconSize, iconSize));
                Lay(card.Name, 34, new Vector2(0f, -134f), new Vector2(inner, 46f));
                Lay(card.About, 24, new Vector2(0f, -186f), new Vector2(inner, cardHeight - 186f - 52f));
                Lay(card.Key, 24, new Vector2(0f, 10f), new Vector2(inner, 34f));
            } else {
                Lay(card.Tag, 20, new Vector2(0f, -16f), new Vector2(380f, 30f));
                Place(card.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(140f, 140f));
                Lay(card.Name, 38, new Vector2(0f, -198f), new Vector2(390f, 56f));
                Lay(card.About, 26, new Vector2(0f, -260f), new Vector2(370f, 110f));
                Lay(card.Key, 30, new Vector2(0f, 36f), new Vector2(380f, 40f));
            }
        }
    }

    static void Lay(Text text, int fontSize, Vector2 position, Vector2 size) {
        text.fontSize = fontSize;
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
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
        UiSkin.Framed(card.Frame);
        Place(card.Frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((index - 1) * 470f, 0f), new Vector2(430f, 500f));
        var button = card.Go.AddComponent<Button>();
        button.targetGraphic = card.Frame;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.08f, 0.85f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.45f);
        button.colors = colors;
        int captured = index;
        button.onClick.AddListener(() => {
            if (Time.unscaledTime - OpenedAtTime >= HumanDelay) {
                Choose(captured);
            }
        });
        card.Group = card.Go.AddComponent<CanvasGroup>();
        MenuButtonFx.Add(button);

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

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(faceGo.transform, false);
        card.Icon = iconGo.AddComponent<Image>();
        card.Icon.raycastTarget = false;
        card.Icon.enabled = false;
        Place(card.Icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(96f, 96f));

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

        // The back: a Department form, face down until it flips.
        var backGo = new GameObject("Back");
        backGo.transform.SetParent(card.Go.transform, false);
        card.Back = backGo.AddComponent<Image>();
        UiSkin.Round(card.Back);
        card.Back.color = new Color(0.86f, 0.8f, 0.66f, 1f);
        card.Back.raycastTarget = false;
        var backRect = card.Back.rectTransform;
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = new Vector2(6f, 6f);
        backRect.offsetMax = new Vector2(-6f, -6f);
        Color ink = new Color(0.18f, 0.14f, 0.1f);
        Text dept = MakeText(backGo.transform, "PLACE DEPARTMENT", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        dept.color = ink;
        Place(dept.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(360f, 34f));
        card.BackNumber = MakeText(backGo.transform, "FORM B-7", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
        card.BackNumber.color = ink;
        Place(card.BackNumber.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(360f, 60f));
        for (int line = 0; line < 3; line++) {
            var rule = new GameObject("Rule").AddComponent<Image>();
            rule.transform.SetParent(backGo.transform, false);
            rule.color = new Color(0.18f, 0.14f, 0.1f, 0.35f);
            rule.raycastTarget = false;
            Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f - line * 26f), new Vector2(300f, 3f));
        }
        Text pending = MakeText(backGo.transform, "PENDING", 26, FontStyle.Italic, TextAnchor.MiddleCenter);
        pending.color = new Color(0.5f, 0.12f, 0.1f);
        Place(pending.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(300f, 36f));
        foreach (Outline o in backGo.GetComponentsInChildren<Outline>()) {
            o.effectColor = new Color(0.86f, 0.8f, 0.66f, 0f);
        }

        // The stamp, over everything.
        card.Stamp = MakeText(card.Go.transform, "APPROVED", 56, FontStyle.Bold, TextAnchor.MiddleCenter);
        card.Stamp.horizontalOverflow = HorizontalWrapMode.Overflow;
        card.Stamp.verticalOverflow = VerticalWrapMode.Overflow;
        card.Stamp.GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);
        card.Stamp.GetComponent<Outline>().effectColor = new Color(0.1f, 0.03f, 0.03f, 0.95f);
        Place(card.Stamp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(380f, 90f));
        card.Stamp.gameObject.SetActive(false);
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
