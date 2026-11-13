using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Arcade run-end: Address struck off, counting waves/coins, best stamp, goals ladder, cast exchange, Retry.
// Docs/Story.md "Run-end exchanges". Built into WaveDirector's lost panel; chapter losses stay on the simple body.
public class RunEndScreen : MonoBehaviour {
    public static RunEndScreen Instance { get; private set; }
    public static bool IsOpen { get; private set; }
    public static bool CountingDone { get; private set; }
    public static bool BestStamped { get; private set; }
    public static int GoalsStamped { get; private set; }
    public static string ExchangeId { get; private set; }
    public static string LastSubtitle { get; private set; }
    public static float OpenedAt { get; private set; }
    public static bool RetryPressed { get; private set; }

    public Button RetryButton { get; private set; }
    public Button MenuButton { get; private set; }

    Text title;
    Text waveLabel;
    Text coinLabel;
    Text bestLabel;
    Text goalLabel;
    Text goalStamp;
    Text subtitle;
    Text hint;
    RectTransform panel;
    CanvasGroup group;
    Coroutine play;
    bool arcade;
    bool ready;
    bool leaving;
    float leaveAt;
    bool retry;

    public static RunEndScreen Ensure(Transform lostPanel) {
        if (Instance != null) {
            return Instance;
        }
        // A RectTransform from the start: a plain Transform made the stretch below a no-op and every child anchored to a point.
        var go = new GameObject("RunEnd", typeof(RectTransform));
        go.transform.SetParent(lostPanel, false);
        var screen = go.AddComponent<RunEndScreen>();
        screen.Build(lostPanel as RectTransform);
        return screen;
    }

    void Awake() {
        Instance = this;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        IsOpen = false;
    }

    public void OpenArcade(int wave, int cleared, int coins, bool newBest, bool nearly, bool boss, string killedBy, int homers, int kills, int smashes) {
        arcade = true;
        IsOpen = true;
        CountingDone = false;
        BestStamped = false;
        GoalsStamped = 0;
        ExchangeId = "";
        LastSubtitle = "";
        RetryPressed = false;
        OpenedAt = Time.unscaledTime;
        ready = false;
        leaving = false;
        retry = false;
        gameObject.SetActive(true);
        if (title != null) {
            title.text = "Address struck off";
        }
        if (bestLabel != null) {
            bestLabel.text = "";
            bestLabel.gameObject.SetActive(false);
        }
        if (goalStamp != null) {
            goalStamp.text = "";
            goalStamp.gameObject.SetActive(false);
        }
        if (subtitle != null) {
            subtitle.text = "";
        }
        if (hint != null) {
            hint.text = "";
        }
        if (RetryButton != null) {
            RetryButton.gameObject.SetActive(true);
            RetryButton.interactable = false;
        }
        if (MenuButton != null) {
            MenuButton.gameObject.SetActive(true);
            MenuButton.interactable = false;
        }
        LayoutForPhone();
        if (play != null) {
            StopCoroutine(play);
        }
        play = StartCoroutine(PlayArcade(wave, cleared, coins, newBest, nearly, boss, killedBy, homers, kills, smashes));
    }

    public void OpenChapter(string body) {
        arcade = false;
        IsOpen = true;
        CountingDone = true;
        ready = true;
        leaving = false;
        retry = false;
        RetryPressed = false;
        OpenedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        if (title != null) {
            title.text = "Address struck off";
        }
        if (waveLabel != null) {
            waveLabel.text = body;
        }
        if (coinLabel != null) {
            coinLabel.text = "";
        }
        if (goalLabel != null) {
            goalLabel.text = "";
        }
        if (bestLabel != null) {
            bestLabel.gameObject.SetActive(false);
        }
        if (goalStamp != null) {
            goalStamp.gameObject.SetActive(false);
        }
        if (subtitle != null) {
            subtitle.text = "";
        }
        if (RetryButton != null) {
            RetryButton.gameObject.SetActive(false);
        }
        if (MenuButton != null) {
            MenuButton.GetComponentInChildren<Text>().text = UiFit.Touch ? "Tap to go on" : "Menu";
            MenuButton.gameObject.SetActive(true);
            MenuButton.interactable = true;
        }
        if (hint != null) {
            hint.text = UiFit.Touch ? "Tap to go on" : "Press any key";
        }
        LayoutForPhone();
    }

    // WaveDirector polls this while Lost: handles Retry / Menu / any-key, starts the panel close.
    // Returns true once the leave has been requested (WaveDirector owns the scene load after CloseSeconds).
    public bool TickLeave() {
        if (!IsOpen || leaving) {
            return false;
        }
        if (!ready) {
            return false;
        }
        if (arcade) {
            if (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter)
                || PlayInput.GetKeyDown(KeyCode.JoystickButton0)) {
                BeginLeave(true);
                return true;
            }
            if (PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.JoystickButton1)
                || MenuNav.Back) {
                BeginLeave(false);
                return true;
            }
            return false;
        }
        if (PlayInput.AnyKeyDown) {
            BeginLeave(false);
            return true;
        }
        return false;
    }

    void Update() {
        if (IsOpen && panel != null && UiFit.Stamp != layoutStamp) {
            LayoutForPhone();
        }
    }

    int layoutStamp = -1;

    void LayoutForPhone() {
        layoutStamp = UiFit.Stamp;
        // Fill the lost panel; WaveDirector.LayoutHud owns the panel's phone scale/position.
        var root = transform as RectTransform;
        if (root != null) {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
        }
        UiMode mode = UiFit.Mode;
        bool tall = mode == UiMode.Tall;
        bool phone = mode != UiMode.Desktop;
        float retryH = phone ? (tall ? 100f : 88f) : 96f;
        float retryW = phone ? (tall ? 640f : 560f) : 420f;
        if (RetryButton != null && RetryButton.gameObject.activeSelf) {
            PlaceButton(RetryButton, 0f, phone ? (28f + (tall ? 72f : 56f)) : 108f, retryW, retryH, phone ? 36 : 42);
        }
        if (MenuButton != null) {
            PlaceButton(MenuButton, 0f, phone ? 20f : 36f, phone ? Mathf.Min(260f, retryW * 0.55f) : 280f, phone ? (tall ? 72f : 56f) : 64f, phone ? 24 : 26);
        }
    }

    static void PlaceButton(Button button, float x, float yFromBottom, float w, float h, int font) {
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, yFromBottom);
        rect.sizeDelta = new Vector2(w, h);
        Text label = button.GetComponentInChildren<Text>();
        if (label != null) {
            label.fontSize = font;
        }
    }

    IEnumerator PlayArcade(int wave, int cleared, int coins, bool newBest, bool nearly, bool boss, string killedBy, int homers, int kills, int smashes) {
        int showWave = Mathf.Max(1, wave);
        int showCoins = Mathf.Max(0, coins);
        if (waveLabel != null) {
            waveLabel.text = "Wave 0";
        }
        if (coinLabel != null) {
            coinLabel.text = "+0 coins";
        }

        float waveStep = Mathf.Clamp(0.55f / Mathf.Max(1, showWave), 0.04f, 0.12f);
        for (int n = 1; n <= showWave; n++) {
            if (waveLabel != null) {
                waveLabel.text = "Wave " + n;
            }
            GameAudio.PlayUI("coin", 0.28f, 0.9f + (n % 5) * 0.03f);
            yield return new WaitForSecondsRealtime(waveStep);
        }
        if (waveLabel != null) {
            waveLabel.text = cleared <= 0
                ? "Fell on wave " + showWave
                : "Fell on wave " + showWave + "  ·  cleared " + cleared;
        }

        float coinStep = Mathf.Clamp(0.7f / Mathf.Max(1, showCoins), 0.03f, 0.1f);
        for (int n = 1; n <= showCoins; n++) {
            if (coinLabel != null) {
                coinLabel.text = "+" + n + (n == 1 ? " coin" : " coins");
            }
            if (n == showCoins || n % 2 == 0) {
                GameAudio.PlayUI("coin", 0.45f, 1f);
            }
            yield return new WaitForSecondsRealtime(coinStep);
        }
        if (showCoins == 0 && coinLabel != null) {
            coinLabel.text = "No new coins";
        }
        CountingDone = true;

        if (newBest) {
            if (bestLabel != null) {
                bestLabel.gameObject.SetActive(true);
                bestLabel.text = "NEW BEST  " + MetaProgress.BestWave;
            }
            GameAudio.PlayUI("stamp_heavy", 0.85f, 1f);
            BestStamped = true;
            yield return new WaitForSecondsRealtime(0.45f);
        } else if (bestLabel != null) {
            bestLabel.gameObject.SetActive(true);
            bestLabel.text = MetaProgress.BestWave > 0 ? "Best cleared: " + MetaProgress.BestWave : "";
        }

        RunGoals.Progress[] finished = RunGoals.SettleRun(showWave, cleared, showCoins, homers, kills, smashes);
        for (int i = 0; i < finished.Length; i++) {
            RunGoals.Progress p = finished[i];
            if (goalStamp != null) {
                goalStamp.gameObject.SetActive(true);
                goalStamp.text = "FILED  " + p.Goal.Title + "  ·  " + p.Goal.Blurb;
            }
            GameAudio.PlayUI("stamp", 0.8f, 1f);
            GoalsStamped++;
            yield return new WaitForSecondsRealtime(0.55f);
        }
        RunGoals.Progress next = RunGoals.Next();
        if (goalLabel != null) {
            if (next.Done) {
                goalLabel.text = "Ladder complete.";
            } else {
                goalLabel.text = "Next: " + next.Goal.Title + "  " + next.Current + "/" + next.Target
                    + (string.IsNullOrEmpty(next.Goal.Blurb) ? "" : "  ·  " + next.Goal.Blurb);
            }
        }

        RunEndTalk.Pick pick = RunEndTalk.Choose(new RunEndTalk.Facts {
            Wave = showWave,
            Cleared = cleared,
            NewBest = newBest,
            SliceNearlyFell = nearly,
            BossKill = boss,
            KilledBy = killedBy ?? "",
            CoinsThisRun = showCoins,
            HomeRuns = homers
        });
        ExchangeId = pick.Id;
        yield return SpeakLine(true, pick.Wallit);
        yield return SpeakLine(false, pick.Slice);

        ready = true;
        if (RetryButton != null) {
            RetryButton.interactable = true;
        }
        if (MenuButton != null) {
            MenuButton.interactable = true;
        }
        if (hint != null) {
            hint.text = "";
        }
        play = null;
    }

    IEnumerator SpeakLine(bool wallit, string line) {
        LastSubtitle = (wallit ? "WALLIT  " : "SLICE  ") + line;
        if (subtitle != null) {
            subtitle.text = LastSubtitle;
        }
        float len;
        if (wallit) {
            WallitVoice.Silence();
            len = WallitVoice.Cutscene(line);
        } else {
            SliceVoice.Silence();
            len = SliceVoice.Cutscene(line);
        }
        yield return new WaitForSecondsRealtime(Mathf.Max(1.1f, len + 0.15f));
    }

    void OnRetry() {
        if (!ready || leaving || !arcade) {
            return;
        }
        BeginLeave(true);
    }

    void OnMenu() {
        if (!ready || leaving) {
            return;
        }
        BeginLeave(false);
    }

    void BeginLeave(bool doRetry) {
        if (leaving) {
            return;
        }
        leaving = true;
        retry = doRetry;
        RetryPressed = doRetry;
        IsOpen = false;
        leaveAt = Time.unscaledTime + PanelFx.CloseSeconds;
        if (WaveDirector.Instance != null) {
            WaveDirector.Instance.BeginRunEndLeave(doRetry);
        }
        GameAudio.PlayUI("stamp", 0.5f, 1.1f);
    }

    void Build(RectTransform lost) {
        panel = lost;
        var root = transform as RectTransform;
        if (root != null) {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.localScale = Vector3.one;
        }
        // Clear the old body-only layout's stray children except the panel image; WaveDirector still owns lostPanel.
        title = MakeText(transform, "Address struck off", 52, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(760f, 70f));
        title.color = new Color(1f, 0.45f, 0.35f);
        waveLabel = MakeText(transform, "", 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(780f, 48f));
        coinLabel = MakeText(transform, "", 30, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -162f), new Vector2(780f, 42f));
        bestLabel = MakeText(transform, "", 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(780f, 40f));
        bestLabel.color = new Color(1f, 0.86f, 0.35f);
        bestLabel.gameObject.SetActive(false);
        goalStamp = MakeText(transform, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(780f, 36f));
        goalStamp.color = new Color(0.95f, 0.78f, 0.4f);
        goalStamp.gameObject.SetActive(false);
        goalLabel = MakeText(transform, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(800f, 48f));
        goalLabel.color = new Color(0.9f, 0.92f, 0.85f, 0.95f);
        subtitle = MakeText(transform, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(820f, 40f));
        subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        subtitle.verticalOverflow = VerticalWrapMode.Truncate;
        hint = MakeText(transform, "", 22, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(700f, 32f));
        RetryButton = MakeButton(transform, "Retry", new Vector2(0f, 108f), new Vector2(420f, 96f), 42, OnRetry, true);
        MenuButton = MakeButton(transform, "Menu", new Vector2(0f, 36f), new Vector2(280f, 64f), 26, OnMenu, false);
        // Hide the old lostBody so it does not stack under the new layout.
        foreach (Text old in lost.GetComponentsInChildren<Text>(true)) {
            if (old.transform.parent == lost && old != title) {
                // WaveDirector's title/body sit on lostPanel; hide body only.
                if (old.text == "" || old.fontSize == 30) {
                    old.gameObject.SetActive(false);
                }
            }
        }
    }

    static Text MakeText(Transform parent, string value, int size, TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 box) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = UiSkin.Font(UiSkin.FontRole.Body);
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorPoint;
        rect.anchorMax = anchorPoint;
        rect.pivot = anchorPoint.y > 0.5f ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        return text;
    }

    static Button MakeButton(Transform parent, string label, Vector2 posFromBottom, Vector2 size, int fontSize, UnityEngine.Events.UnityAction click, bool primary) {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image);
        image.color = primary ? new Color(0.72f, 0.18f, 0.16f, 0.98f) : new Color(0.18f, 0.14f, 0.12f, 0.95f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(click);
        MenuButtonFx.Add(button);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = posFromBottom;
        rect.sizeDelta = size;
        var text = MakeText(go.transform, label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        text.raycastTarget = false;
        var tRect = text.rectTransform;
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;
        tRect.pivot = new Vector2(0.5f, 0.5f);
        return button;
    }
}
