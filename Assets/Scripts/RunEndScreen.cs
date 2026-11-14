using System.Collections;
using System.Collections.Generic;
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
    Text rankLabel;
    public static string RankLine = "";
    public static string RankLetter = "";
    Text goalLabel;
    Text goalStamp;
    Text subtitle;
    Text hint;
    RectTransform panel;
    CanvasGroup group;
    Coroutine play;
    bool arcade;
    // The build band: the boons held when the run ended, one icon and level each, in its own band between the goal
    // lines and the cast subtitle. Its cells are rebuilt on every layout and it is never given a pixel another band uses.
    RectTransform buildBand;
    readonly List<GameObject> buildCells = new List<GameObject>();
    List<BuildList.Entry> buildHeld = new List<BuildList.Entry>();
    public static int BuildHeld { get; private set; }
    public static int BuildShown { get; private set; }
    public RectTransform BuildBand { get { return buildBand; } }
    bool ready;
    bool leaving;
    float leaveAt;
    bool retry;
    float readyAt;                 // Retry works this long after the sheet opens (a swing mashed through the fall must not press it)
    public const float Grace = 0.45f;

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
        readyAt = Time.unscaledTime + Grace;
        leaving = false;
        retry = false;
        gameObject.SetActive(true);
        buildHeld = BuildList.Held();   // what the run ended with (a retry clears the ranks)
        BuildHeld = buildHeld.Count;
        if (title != null) {
            title.text = "Address struck off";
        }
        RankLine = "";
        RankLetter = "";
        if (rankLabel != null) {
            rankLabel.text = "";
            rankLabel.gameObject.SetActive(false);
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
        FocusRetry();
        if (play != null) {
            StopCoroutine(play);
        }
        play = StartCoroutine(PlayArcade(wave, cleared, coins, newBest, nearly, boss, killedBy, homers, kills, smashes));
    }

    public void OpenChapter(string body) {
        arcade = false;
        IsOpen = true;
        CountingDone = true;
        ready = false;
        readyAt = Time.unscaledTime + Grace;
        leaving = false;
        retry = false;
        RetryPressed = false;
        OpenedAt = Time.unscaledTime;
        gameObject.SetActive(true);
        buildHeld = new List<BuildList.Entry>();
        BuildHeld = 0;
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
        if (rankLabel != null) {
            rankLabel.gameObject.SetActive(false);
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
            RetryButton.gameObject.SetActive(true);   // a chapter's Retry: the same chapter from its opening
            RetryButton.interactable = false;
        }
        if (MenuButton != null) {
            MenuButton.GetComponentInChildren<Text>().text = "Menu";
            MenuButton.gameObject.SetActive(true);
            MenuButton.interactable = false;
        }
        if (hint != null) {
            hint.text = "";
        }
        LayoutForPhone();
        FocusRetry();
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
        {
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
    }

    // Retry is the first button and holds the focus ring, so Enter, Space or a pad A is one press.
    void FocusRetry() {
        if (RetryButton != null && UnityEngine.EventSystems.EventSystem.current != null) {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(RetryButton.gameObject);
        }
    }

    void Update() {
        if (IsOpen && !ready && !leaving && Time.unscaledTime >= readyAt) {
            // Retry and Menu work a moment after the sheet is up; the counting and the spoken lines go on beside them.
            ready = true;
            if (RetryButton != null) {
                RetryButton.interactable = true;
            }
            if (MenuButton != null) {
                MenuButton.interactable = true;
            }
            FocusRetry();
        }
        if (IsOpen && panel != null && (UiFit.Stamp != layoutStamp || panel.rect.size != layoutSize)) {
            LayoutForPhone();
        }
    }

    int layoutStamp = -1;
    Vector2 layoutSize;

    void LayoutForPhone() {
        layoutStamp = UiFit.Stamp;
        if (panel != null) {
            layoutSize = panel.rect.size;
        }
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
        Rect size = panel != null ? panel.rect : new Rect(0f, 0f, 900f, 700f);
        float W = size.width;
        float textW = Mathf.Min(W - 40f, 1000f);
        // Bands, top down: title, counts, goals; bottom up: buttons, then the cast subtitle's own band above them.
        // No two bands share a pixel, so the subtitle can never print over the FILED line or the goal ladder.
        int fsTitle = phone ? 40 : 52, fsWave = phone ? 32 : 34, fsCoin = phone ? 28 : 30, fsBest = phone ? 26 : 28, fsGoal = phone ? 26 : 24, fsSub = phone ? 26 : 26;
        int goalLines = textW >= 1000f ? 1 : (phone ? 3 : 2);
        float goalH = fsGoal * 1.3f * goalLines + 6f;
        float subH = fsSub * 1.3f * (textW >= 1000f ? 1 : 2) + 8f;
        float bandTop;   // height from the bottom the buttons occupy
        bool row = phone && !tall;   // landscape phone: Retry and Menu side by side
        float retryH = phone ? (tall ? 140f : 100f) : 96f;   // 140 units is about 80 px on a 390 px phone: well over the 64 px a thumb needs
        float retryW = phone ? (tall ? Mathf.Min(640f, W - 40f) : 380f) : 420f;
        if (row) {
            float menuW = 220f;
            float total = retryW + 16f + menuW;
            if (RetryButton != null) {
                PlaceButton(RetryButton, -total * 0.5f + retryW * 0.5f, 20f, retryW, retryH, 38);
            }
            if (MenuButton != null) {
                PlaceButton(MenuButton, total * 0.5f - menuW * 0.5f, 20f, menuW, 72f, 24);
            }
            bandTop = 20f + retryH + 14f;
        } else {
            float menuH = phone ? 72f : 64f;
            float menuY = phone ? 20f : 36f;
            float retryY = menuY + menuH + (phone ? 16f : 8f);
            if (RetryButton != null) {
                PlaceButton(RetryButton, 0f, retryY, retryW, retryH, phone ? 40 : 42);
            }
            if (MenuButton != null) {
                PlaceButton(MenuButton, 0f, menuY, phone ? Mathf.Min(260f, retryW * 0.55f) : 280f, menuH, phone ? 24 : 26);
            }
            bandTop = retryY + retryH + 14f;
        }
        float y = 20f;
        y = PlaceRow(title, fsTitle, y, textW, fsTitle * 1.3f) + 6f;
        if (arcade) {
            y = PlaceRow(waveLabel, fsWave, y, textW, fsWave * 1.3f) + 4f;
            y = PlaceRow(coinLabel, fsCoin, y, textW, fsCoin * 1.3f) + 4f;
            y = PlaceRow(bestLabel, fsBest, y, textW, fsBest * 1.3f) + 8f;
            y = PlaceRow(rankLabel, phone ? 24 : 26, y, textW, (phone ? 24 : 26) * 1.3f * (textW >= 1000f ? 1 : 2) + 6f) + 6f;
            y = PlaceRow(goalStamp, fsGoal, y, textW, goalH) + 4f;
            y = PlaceRow(goalLabel, fsGoal, y, textW, goalH) + 4f;
            PlaceBottomRow(subtitle, fsSub, bandTop, textW, subH);
            float room = size.height - y - (bandTop + subH) - 10f;
            PlaceBuildBand(y + 4f, textW, room, phone);
        } else {
            PlaceBuildBand(0f, 0f, 0f, phone);
            // A chapter loss: its body takes everything between the title and the buttons.
            float room = Mathf.Max(60f, size.height - y - bandTop - 8f);
            PlaceRow(waveLabel, 30, y, textW, room);
            PlaceRow(coinLabel, fsCoin, y, 0f, 0f);
            PlaceRow(bestLabel, fsBest, y, 0f, 0f);
            PlaceRow(rankLabel, fsBest, y, 0f, 0f);
            PlaceRow(goalStamp, fsGoal, y, 0f, 0f);
            PlaceRow(goalLabel, fsGoal, y, 0f, 0f);
            PlaceBottomRow(subtitle, fsSub, bandTop, 0f, 0f);
        }
    }

    // Icons of the boons held, wrapped into rows, each with its level on the icon's corner. Cells are 72 units (64 on a
    // phone, 48 when the sheet is short); what does not fit becomes a "+N" cell, and with no room at all the band is hidden.
    void PlaceBuildBand(float fromTop, float w, float room, bool phone) {
        for (int i = 0; i < buildCells.Count; i++) {
            Destroy(buildCells[i]);
        }
        buildCells.Clear();
        BuildShown = 0;
        if (buildBand == null) {
            var go = new GameObject("Build Band", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            buildBand = (RectTransform)go.transform;
        }
        float cell = phone ? 64f : 72f;
        if (room < cell) {
            cell = 48f;
        }
        int n = buildHeld.Count;
        if (w <= 0f || n == 0 || room < cell) {
            buildBand.gameObject.SetActive(false);
            return;
        }
        int cols = Mathf.Max(1, Mathf.FloorToInt(w / cell));
        int rows = Mathf.Min(Mathf.CeilToInt(n / (float)cols), Mathf.Max(1, Mathf.FloorToInt(room / cell)));
        int slots = cols * rows;
        int shown = n <= slots ? n : slots - 1;
        buildBand.gameObject.SetActive(true);
        buildBand.anchorMin = buildBand.anchorMax = new Vector2(0.5f, 1f);
        buildBand.pivot = new Vector2(0.5f, 1f);
        buildBand.anchoredPosition = new Vector2(0f, -fromTop);
        buildBand.sizeDelta = new Vector2(cols * cell, rows * cell);
        for (int i = 0; i < shown; i++) {
            BuildList.Entry e = buildHeld[i];
            var go = new GameObject("Boon " + e.Name, typeof(RectTransform));
            go.transform.SetParent(buildBand, false);
            var image = go.AddComponent<Image>();
            if (!UiArt.Show(image, UiArt.Boon(e.Id))) {
                image.enabled = true;
                image.color = RunBoons.RarityColor(e.Rarity);
            }
            SetCell(image.rectTransform, i, cols, cell);
            Text level = MakeText(go.transform, BuildList.Level(e), cell < 60f ? 20 : 22, TextAnchor.LowerRight, new Vector2(1f, 0f), Vector2.zero, new Vector2(cell - 4f, 26f));
            level.color = e.Evolution ? new Color(1f, 0.85f, 0.45f) : Color.white;
            var lr = level.rectTransform;
            lr.pivot = new Vector2(1f, 0f);
            lr.anchoredPosition = new Vector2(-2f, 0f);
            level.horizontalOverflow = HorizontalWrapMode.Overflow;
            level.verticalOverflow = VerticalWrapMode.Overflow;
            buildCells.Add(go);
        }
        if (shown < n) {
            var go = new GameObject("More", typeof(RectTransform));
            go.transform.SetParent(buildBand, false);
            Text more = MakeText(go.transform, "+" + (n - shown), 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cell, cell));
            more.rectTransform.anchorMin = more.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            more.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            SetCell((RectTransform)go.transform, shown, cols, cell);
            buildCells.Add(go);
        }
        BuildShown = shown;
    }

    static void SetCell(RectTransform rect, int index, int cols, float cell) {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2((index % cols) * cell + 2f, -(index / cols) * cell - 2f);
        rect.sizeDelta = new Vector2(cell - 4f, cell - 4f);
    }

    static float PlaceRow(Text text, int font, float fromTop, float w, float h) {
        if (text == null) {
            return fromTop;
        }
        text.fontSize = font;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -fromTop);
        rect.sizeDelta = new Vector2(w, h);
        return fromTop + h;
    }

    static void PlaceBottomRow(Text text, int font, float fromBottom, float w, float h) {
        if (text == null) {
            return;
        }
        text.fontSize = font;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, fromBottom);
        rect.sizeDelta = new Vector2(w, h);
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

        RankLine = ArcadeRank.Line(cleared, kills, Combo.Best, ArcadeRank.BestClean, out RankLetter);
        if (rankLabel != null) {
            rankLabel.gameObject.SetActive(true);
            rankLabel.text = RankLine;
        }
        GameAudio.PlayUI("stamp", 0.7f, 1.1f);
        yield return new WaitForSecondsRealtime(0.35f);

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
        if (!ready || leaving) {
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
        if (doRetry) {
            DownFlow.NoteRetryTap();
        }
        if (play != null) {
            StopCoroutine(play);
            play = null;
        }
        WallitVoice.Silence();
        SliceVoice.Silence();
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
        rankLabel = MakeText(transform, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(800f, 70f));
        rankLabel.color = new Color(0.98f, 0.9f, 0.62f);
        rankLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        rankLabel.gameObject.SetActive(false);
        bestLabel = MakeText(transform, "", 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -214f), new Vector2(780f, 40f));
        bestLabel.color = new Color(1f, 0.86f, 0.35f);
        bestLabel.gameObject.SetActive(false);
        goalStamp = MakeText(transform, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(780f, 36f));
        goalStamp.color = new Color(0.95f, 0.78f, 0.4f);
        goalStamp.gameObject.SetActive(false);
        goalLabel = MakeText(transform, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(800f, 48f));
        goalLabel.color = new Color(0.9f, 0.92f, 0.85f, 0.95f);
        subtitle = MakeText(transform, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(820f, 40f));
        subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitle.verticalOverflow = VerticalWrapMode.Truncate;
        subtitle.alignment = TextAnchor.LowerCenter;
        // The hint never carries words now; it used to sit inside Retry's rect and count as an overlap.
        hint = MakeText(transform, "", 22, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(700f, 32f));
        hint.gameObject.SetActive(false);
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
