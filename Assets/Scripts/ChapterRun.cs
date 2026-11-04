using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A Campaign chapter, played in the game scene with Wallit's own controls and bat and Slice at the circle. The chapter is
// data (Story.Chapters): where it is, the objective (clear a count, survive a time, reach a place, defeat the boss), the
// bots that come, the lines before and after. WaveDirector stands aside (Phase.Chapter) and spawns what this asks for.
// The objective shows in a line at the bottom; meeting it saves the case file on to the next chapter and shows the
// Chapter Complete card. ChapterScene (Assets/Campaign.unity) is the card before the chapter and hands over to this.
public class ChapterRun : MonoBehaviour {
    public static ChapterRun Instance { get; private set; }

    // Set by ChapterScene's Begin button for the one game-scene load that follows; the next scene load takes it.
    static int armed = -1;

    public static void Begin(int chapter) {
        armed = chapter;
        SceneManager.LoadScene(MetaProgress.GameScene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    // Any other scene load drops the hand-over, so Arcade (and the menu's Play) can never become a chapter.
    static void Loaded(Scene scene, LoadSceneMode mode) {
        int chapter = armed;
        armed = -1;
        if (scene.name == MetaProgress.GameScene && chapter >= 0 && Instance == null && CampaignSave.Active >= 0) {
            var go = new GameObject("Chapter Run");
            go.AddComponent<ChapterRun>();
            go.GetComponent<ChapterRun>().Setup(chapter);
        }
    }

    public int Chapter { get; private set; }
    public string Title { get; private set; }
    public string Place { get; private set; }
    public int Power { get; private set; } = 1;
    public string ObjectiveLine { get; private set; } = "";
    public bool Completed { get; private set; }
    public bool Failed { get; private set; }
    public int Progress { get; private set; }     // bots down, seconds held, metres left, or 0/1 for the boss
    public float PlaySeconds { get; private set; }
    public Vector3 Marker { get; private set; }
    public bool HasMarker { get; private set; }
    public bool HasBoss { get { return data.Objective == Story.ChapterObjective.HoldThenBoss; } }
    public bool BossUp { get { return boss != null && boss.Alive; } }
    public Enemy BossEnemy { get { return boss; } }
    public bool BossDown { get { return bossSpawned && !BossUp; } }
    public Button NextButton { get; private set; }
    public Button MenuButton { get; private set; }
    public bool CardUp { get { return card != null && card.activeSelf; } }
    public string CardText { get { return cardBody != null ? cardTitle.text + " | " + cardBody.text : ""; } }
    public static readonly List<string> Events = new List<string>();

    const float ReachRadius = 4.5f;
    const float RingRadius = 34f;

    Story.Chapter data;
    Font font;
    Text objective;
    GameObject card;
    Text cardTitle;
    Text cardBody;
    float spawnIn = 2.5f;
    Enemy boss;
    bool bossSpawned;
    bool bossCalled;      // HoldThenBoss: the bots are down and the boss is on its way
    float defeatIn = -1f; // HoldThenBoss: the defeat beat before the card
    readonly List<string> queue = new List<string>();
    float queueIn;
    float clock;
    float nextRing;
    GameObject beacon;
    bool toNext;
    float saved;
    int stamp = -1;
    RectTransform cardRect;
    RectTransform column;
    CanvasScaler scaler;
    Transform player;

    void Awake() {
        Instance = this;
    }

    // Called as the scene loads, before WaveDirector.Start reads Power.
    void Setup(int chapter) {
        Chapter = Mathf.Clamp(chapter, 0, CampaignSave.ChapterCount - 1);
        data = Story.Chapters[Chapter];
        Title = data.Title;
        Place = data.Place;
        Power = data.Power;
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Events.Clear();
        Events.Add("start|" + data.Id + "|" + data.Objective + "|" + data.Target);
        if (data.Objective == Story.ChapterObjective.SurviveTime) {
            clock = data.Target;
        }
        if (data.Objective == Story.ChapterObjective.ReachPlace) {
            MakeMarker();
        }
        BuildUi();
        RunAnalytics.Screen("chapter_play_" + (Chapter + 1));
        if (data.Cutscene != null && data.Cutscene.Length > 0) {
            // The chapter's opening scene plays over the frozen map; reading it or skipping it hands over to play.
            Events.Add("cutscene|" + data.Id);
            Cutscene.Play(data.Cutscene, () => Events.Add("play|" + data.Id));
        } else {
            Say(data.Lines.Length > 0 ? data.Lines[0] : "", 3.2f);
        }
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        if (beacon != null) {
            Destroy(beacon);
        }
        SavePlayTime();
        if (!toNext) {
            CampaignSave.Close();
        }
        if (Time.timeScale == 0f) {
            Time.timeScale = 1f;
        }
    }

    void Update() {
        if (data.Id == null) {
            return;
        }
        if (Completed) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter)) {
                if (NextButton != null && NextButton.gameObject.activeSelf) {
                    Next();
                } else {
                    ToMenu();
                }
            }
            return;
        }
        if (Failed || WaveDirector.IsLost || PauseMenu.IsOpen || StoryScreen.IsOpen || LevelUpScreen.IsOpen || Cutscene.IsOpen) {
            return;
        }
        PlaySeconds += Time.unscaledDeltaTime;
        TickQueue();
        if (player == null && PlayerGrowth.Instance != null) {
            player = PlayerGrowth.Instance.transform;
        }
        Tick();
        ObjectiveLine = Line();
        if (objective != null) {
            objective.text = ObjectiveLine;
        }
        if (stamp != UiFit.Stamp) {
            Layout();
        }
    }

    void Tick() {
        WaveDirector director = WaveDirector.Instance;
        if (director == null) {
            return;
        }
        int alive = Alive();
        spawnIn -= Time.deltaTime;
        switch (data.Objective) {
            case Story.ChapterObjective.ClearCount:
                Progress = RunLog.RunKills;
                if (spawnIn <= 0f && alive < data.Cap && RunLog.RunKills + alive < data.Target) {
                    SpawnCrew(director);
                }
                if (RunLog.RunKills >= data.Target) {
                    Complete();
                }
                break;
            case Story.ChapterObjective.SurviveTime:
                clock -= Time.deltaTime;
                Progress = Mathf.Max(0, Mathf.CeilToInt(clock));
                if (spawnIn <= 0f && alive < data.Cap) {
                    SpawnCrew(director);
                }
                if (clock <= 0f) {
                    Complete();
                }
                break;
            case Story.ChapterObjective.ReachPlace:
                if (spawnIn <= 0f && alive < data.Cap) {
                    SpawnCrew(director);
                }
                if (player != null) {
                    Vector3 d = Marker - player.position;
                    d.y = 0f;
                    Progress = Mathf.CeilToInt(d.magnitude);
                    if (d.magnitude <= ReachRadius) {
                        Complete();
                    }
                }
                if (Time.time >= nextRing) {
                    nextRing = Time.time + 1.4f;
                    Fx.Ring(Marker + Vector3.up * 0.2f, ReachRadius * 2.4f, new Color(1f, 0.82f, 0.38f, 0.8f), 1.2f);
                }
                break;
            case Story.ChapterObjective.HoldThenBoss:
                if (!bossCalled) {
                    Progress = RunLog.RunKills;
                    if (spawnIn <= 0f && alive < data.Cap && RunLog.RunKills + alive < data.Target) {
                        SpawnCrew(director);
                    }
                    if (RunLog.RunKills >= data.Target) {
                        bossCalled = true;
                        spawnIn = 3.5f;
                        Events.Add("boss-called|" + data.BossName);
                        Tell(data.BossLines);
                    }
                } else if (!bossSpawned) {
                    if (spawnIn <= 0f) {
                        bossSpawned = true;
                        boss = director.SpawnForChapter(data.Boss, true, RingRadius, data.BossName, data.BossHits);
                        Events.Add("boss|" + boss.Title + "|" + boss.HitsToDrop);
                    }
                } else if (defeatIn > 0f) {
                    defeatIn -= Time.deltaTime;
                    if (defeatIn <= 0f) {
                        Complete();
                    }
                } else if (!BossUp) {
                    // The Courier is down: a short beat over the field, then the card.
                    Progress = 1;
                    defeatIn = (data.DefeatLines != null ? data.DefeatLines.Length : 0) * 2.8f + 1f;
                    Events.Add("defeat|" + data.BossName);
                    GameAudio.Play("stamp", 0.8f, 0.8f);
                    Tell(data.DefeatLines);
                }
                break;
            case Story.ChapterObjective.DefeatBoss:
                if (!bossSpawned && spawnIn <= 0f) {
                    bossSpawned = true;
                    boss = director.SpawnForChapter(data.Boss, true, RingRadius);
                    spawnIn = data.Every;
                } else if (bossSpawned) {
                    Progress = boss == null || !boss.Alive ? 1 : 0;
                    if (spawnIn <= 0f && alive < data.Cap) {
                        SpawnCrew(director);
                    }
                    if (Progress == 1) {
                        Complete();
                    }
                }
                break;
        }
    }

    void SpawnCrew(WaveDirector director) {
        spawnIn = data.Every;
        BotKind kind = data.Crew[Random.Range(0, data.Crew.Length)];
        director.SpawnForChapter(kind, false, RingRadius + Random.Range(0f, 10f));
    }

    static int Alive() {
        int n = 0;
        foreach (Enemy e in Enemy.Roster) {
            if (e != null && e.Alive) {
                n++;
            }
        }
        return n;
    }

    string Line() {
        switch (data.Objective) {
            case Story.ChapterObjective.ClearCount:
                return "Down " + data.Target + " " + data.Goal + ":  " + Mathf.Min(Progress, data.Target) + " / " + data.Target;
            case Story.ChapterObjective.SurviveTime:
                return "Hold the circle against " + data.Goal + ":  " + (Progress / 60) + ":" + (Progress % 60).ToString("00");
            case Story.ChapterObjective.ReachPlace:
                return "Reach " + data.Goal + ":  " + Progress + " m";
            case Story.ChapterObjective.HoldThenBoss:
                if (!bossCalled) {
                    return "Hold the circle:  " + Mathf.Min(Progress, data.Target) + " / " + data.Target + " " + data.Goal + " down";
                }
                if (defeatIn > 0f) {
                    return data.BossName + " is down";
                }
                return "Bring down " + data.BossName + (bossSpawned ? "  (break the wind-up, or stand in the red)" : "  (it's coming)");
            default:
                return "Bring down " + data.Goal + (bossSpawned ? "" : "  (it's coming)");
        }
    }

    void Complete() {
        if (Completed) {
            return;
        }
        Completed = true;
        Events.Add("complete|" + data.Id + "|" + Mathf.RoundToInt(PlaySeconds));
        GameAudio.Play("stamp", 0.7f);
        CampaignSave.Complete(Chapter);
        SavePlayTime();
        foreach (Enemy e in new List<Enemy>(Enemy.Roster)) {
            if (e != null) {
                Destroy(e.gameObject);
            }
        }
        if (beacon != null) {
            beacon.SetActive(false);
        }
        bool more = Chapter + 1 < CampaignSave.ChapterCount;
        cardTitle.text = "CHAPTER COMPLETE";
        var body = new System.Text.StringBuilder(data.Title);
        foreach (string raw in data.Done) {
            body.Append("\n").Append(Spoken(raw));
        }
        body.Append("\n\n").Append(more ? "Next: " + Story.Chapters[Chapter + 1].Title : "The case file is filed.");
        cardBody.text = body.ToString();
        NextButton.gameObject.SetActive(more);
        card.SetActive(true);
        objective.text = "";
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RunAnalytics.Screen("chapter_complete_" + (Chapter + 1));
    }

    public void OnLost() {
        Failed = true;
        Events.Add("lost|" + data.Id);
        SavePlayTime();
    }

    // Adds the time played since the last save to the case file.
    void SavePlayTime() {
        if (PlaySeconds > saved) {
            CampaignSave.Record(PlaySeconds - saved);
            saved = PlaySeconds;
        }
    }

    void Next() {
        Time.timeScale = 1f;
        toNext = true;
        RunAnalytics.Screen("chapter_" + (CampaignSave.Get(CampaignSave.Active).chapter + 1));
        SceneManager.LoadScene(CampaignSave.Scene);
    }

    void ToMenu() {
        Time.timeScale = 1f;
        RunAnalytics.Screen("menu");
        SceneManager.LoadScene(MetaProgress.MenuScene);
    }

    // Lines said one after another over the field (the boss's arrival, the defeat beat).
    void Tell(string[] lines) {
        if (lines == null) {
            return;
        }
        queue.AddRange(lines);
        if (queueIn <= 0f) {
            queueIn = 0.01f;
        }
    }

    void TickQueue() {
        if (queue.Count == 0) {
            return;
        }
        queueIn -= Time.unscaledDeltaTime;
        if (queueIn <= 0f) {
            string raw = queue[0];
            queue.RemoveAt(0);
            Say(raw, 3f);
            Events.Add("line|" + raw);
            queueIn = 2.8f;
        }
    }

    static string Spoken(string raw) {
        char who = raw[0];
        string body = raw.Substring(2);
        string name = who == 'W' ? "Wallit" : who == 'S' ? "Slice" : who == 'Y' ? "Yo Bot" : "";
        return name.Length > 0 ? name + ": \"" + body + "\"" : body;
    }

    static void Say(string raw, float seconds) {
        if (!string.IsNullOrEmpty(raw)) {
            UpgradeLook.Say(Spoken(raw), seconds, 230f);
        }
    }

    // The place: a soft disc on the ground and a pale pillar, no collider (the playfield stays free of anything solid).
    void MakeMarker() {
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        float a = data.Bearing * Mathf.Deg2Rad;
        Vector3 at = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * data.Target;
        at.y = origin.y;
        if (Physics.Raycast(new Vector3(at.x, 60f, at.z), Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        Marker = at;
        HasMarker = true;
        beacon = new GameObject("Chapter Place");
        beacon.transform.position = at;
        var shader = SafeShader.Find("Sprites/Default");
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Place Disc";
        Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(beacon.transform, false);
        disc.transform.localScale = new Vector3(ReachRadius * 2f, 0.04f, ReachRadius * 2f);
        disc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Place Pillar";
        Destroy(pillar.GetComponent<Collider>());
        pillar.transform.SetParent(beacon.transform, false);
        pillar.transform.localScale = new Vector3(2.6f, 14f, 2.6f);
        pillar.transform.localPosition = new Vector3(0f, 14f, 0f);
        if (shader != null) {
            disc.GetComponent<Renderer>().material = new Material(shader) { color = new Color(1f, 0.82f, 0.38f, 0.45f) };
            pillar.GetComponent<Renderer>().material = new Material(shader) { color = new Color(1f, 0.9f, 0.6f, 0.18f) };
        }
    }

    // ---------------------------------------------------------------- UI

    void BuildUi() {
        var canvasGo = new GameObject("Chapter Run Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var back = new GameObject("Objective", typeof(RectTransform));
        back.transform.SetParent(canvasGo.transform, false);
        var backImage = back.AddComponent<Image>();
        UiSkin.Round(backImage, 0.5f);
        backImage.color = new Color(0.05f, 0.06f, 0.1f, 0.72f);
        backImage.raycastTarget = false;
        var brect = backImage.rectTransform;
        brect.anchorMin = new Vector2(0.5f, 0f);
        brect.anchorMax = new Vector2(0.5f, 0f);
        brect.pivot = new Vector2(0.5f, 0f);
        brect.anchoredPosition = new Vector2(0f, 190f);
        brect.sizeDelta = new Vector2(900f, 70f);
        objective = Label(brect, "", 32, FontStyle.Bold, new Color(1f, 0.82f, 0.38f));
        objective.rectTransform.anchorMin = Vector2.zero;
        objective.rectTransform.anchorMax = Vector2.one;
        objective.rectTransform.offsetMin = Vector2.zero;
        objective.rectTransform.offsetMax = Vector2.zero;

        card = new GameObject("Chapter Complete", typeof(RectTransform));
        card.transform.SetParent(canvasGo.transform, false);
        var shade = card.AddComponent<Image>();
        shade.color = new Color(0.02f, 0.03f, 0.06f, 0.86f);
        cardRect = shade.rectTransform;
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.one;
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;

        column = new GameObject("Column", typeof(RectTransform)).GetComponent<RectTransform>();
        column.SetParent(cardRect, false);
        column.anchorMin = new Vector2(0.5f, 0.5f);
        column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(1400f, 900f);
        cardTitle = Label(column, "CHAPTER COMPLETE", 72, FontStyle.Bold, new Color(1f, 0.82f, 0.38f));
        Anchor(cardTitle.rectTransform, 0f, 0f, 1300f, 100f);
        cardBody = Label(column, "", 32, FontStyle.BoldAndItalic, new Color(0.87f, 0.91f, 1f));
        Anchor(cardBody.rectTransform, 0f, 120f, 1300f, 580f);
        NextButton = MakeButton(column, "Next chapter", 380f, 88f, 760f, Next);
        MenuButton = MakeButton(column, "Menu", 260f, 88f, 760f, ToMenu);
        card.SetActive(false);
        Layout();
    }

    void Layout() {
        stamp = UiFit.Stamp;
        float scale = 1f;
        if (UiFit.Compact) {
            Vector2 size = UiFit.Size;
            scale = Mathf.Min(1f, (size.x - 40f) / 1400f, (size.y - 40f) / 900f);
        }
        column.localScale = new Vector3(scale, scale, 1f);
        var brect = objective.transform.parent.GetComponent<RectTransform>();
        brect.sizeDelta = new Vector2(Mathf.Min(900f, UiFit.Size.x - 40f), 70f);
        bool more = Chapter + 1 < CampaignSave.ChapterCount;
        NextButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-170f, -760f);
        MenuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(more ? 270f : 0f, -760f);
    }

    static void Anchor(RectTransform rect, float x, float y, float w, float h) {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    Text Label(RectTransform parent, string value, int size, FontStyle style, Color color) {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Min(size, 20);
        text.resizeTextMaxSize = size;
        go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return text;
    }

    Button MakeButton(RectTransform parent, string label, float w, float h, float y, UnityEngine.Events.UnityAction onClick) {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Round(image, 0.6f);
        image.color = new Color(0.13f, 0.14f, 0.2f, 1f);
        var edge = go.AddComponent<Outline>();
        edge.effectColor = new Color(0.8f, 0.63f, 0.33f, 0.7f);
        edge.effectDistance = new Vector2(2f, -2f);
        var button = go.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.onClick.AddListener(onClick);
        MenuButtonFx.Add(button);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -y);
        rect.sizeDelta = new Vector2(w, h);
        Text text = Label(rect, label, 36, FontStyle.Bold, new Color(0.97f, 0.94f, 0.86f));
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}
