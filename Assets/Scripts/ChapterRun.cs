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
    public bool Hard { get; private set; }          // the file is filed and set to Hard: bots carry more health, blows on Slice land harder (CampaignGrade)
    public int Rank { get; private set; }           // CampaignGrade rank of this run, set when Chapter Complete shows
    public int DamageTaken { get { return Musician.Instance != null ? Musician.Instance.DamageTaken : 0; } }
    public bool NewBest { get; private set; }
    public bool Failed { get; private set; }
    public int Progress { get; private set; }     // bots down, seconds held, metres left, or 0/1 for the boss
    public float PlaySeconds { get; private set; }
    public Vector3 Marker { get; private set; }
    public bool HasMarker { get; private set; }
    public bool IsGate { get { return data.Objective == Story.ChapterObjective.GateAndBack; } }
    public bool IsFloor { get { return data.Objective == Story.ChapterObjective.FloorRun; } }
    public int StopCount { get { return data.Stops != null ? data.Stops.Length : 0; } }
    public bool FeedBroken { get; private set; }
    public int Leg { get { return leg; } }   // GateAndBack: 0 out to the gate, 1 back to the circle, 2 the waves and the boss. FloorRun: 0..StopCount-1 the stops out, StopCount back to the circle, StopCount+1 the bots and the boss
    public int WaveNow { get { return waveNow; } }
    public bool HasBoss { get { return data.Objective == Story.ChapterObjective.HoldThenBoss && data.Waves == 0 && !data.Final; } }
    public bool IsFinal { get { return data.Final; } }   // Chapter 6: three waves, then the Spider Bot with the order; clearing it plays the ending and the credits
    public bool CreditsUp { get; private set; }
    public bool SecretShown { get; private set; }   // the secret ending played (every lost thing kept when Chapter 6 was filed)
    public bool IsReading { get { return data.Objective == Story.ChapterObjective.HoldThenBoss && data.Waves > 0 && !data.Final; } }   // Chapter 5: pages read in waves at the desk, then the Registrar
    public int PagesRead { get { return pagesRead; } }
    int crewSpawned;
    bool variantSeen, miniSpawned;
    Enemy mini;
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
    const float HomeRadius = 9f;

    Story.Chapter data;
    Font font;
    Text objective;
    GameObject card;
    Text cardTitle;
    Text cardBody;
    Text xpText;
    float spawnIn = 2.5f;
    Enemy boss;
    bool bossSpawned;
    bool reacted;         // the boss's phase-two line for a past choice has been told
    bool bossCalled;      // HoldThenBoss: the bots are down and the boss is on its way
    float defeatIn = -1f; // HoldThenBoss: the defeat beat before the card
    readonly List<string> queue = new List<string>();
    float queueIn;
    float clock;
    float nextRing;
    int leg;
    int waveNow;
    int pagesRead;    // the Reading Room: pages whose wave is down (their forms on the wall have gone blank)
    float creditsAt;
    float nextPulse;
    int killBase;     // kills before the waves began (GateAndBack): the hold counts from here
    GameObject gateProps;
    FloorState floorProps;
    GameObject beacon;
    bool toNext;
    float saved;
    int stamp = -1;
    RectTransform cardRect;
    RectTransform column;
    CanvasScaler scaler;
    Transform player;
    // The journal (Journal.cs): this chapter's hidden find and optional objective.
    GameObject findItem;
    Transform findSpin;
    Vector3 findAt;
    float findRing;
    Text extraText;
    int baseHomeRuns, baseSmash, baseFinishers;
    public bool FindKept { get; private set; }
    public bool GoalMet { get; private set; }
    public Vector3 FindAt { get { return findAt; } }
    public bool FindOut { get { return findItem != null; } }

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
        CampaignSave.Slot file = CampaignSave.Get(CampaignSave.Active);
        Hard = file.hard && CampaignGrade.Filed(file);
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Events.Clear();
        Events.Add("start|" + data.Id + "|" + data.Objective + "|" + data.Target + (Hard ? "|hard" : ""));
        CampaignProgress.BeginChapter();
        gameObject.AddComponent<CampaignAbilities>();
        if (!string.IsNullOrEmpty(data.Companion)) {
            // A companion walks with Wallit in the chapters that name one (Arcade has no ChapterRun, so never there).
            Companion.Spawn(data.Companion);
            Events.Add("companion|" + data.Companion);
        }
        if (!string.IsNullOrEmpty(data.Map)) {
            // The chapter's own map, from its data, loaded on top of the game scene with its own lighting.
            ChapterMap.Load(data.Map);
        }
        if (data.Objective == Story.ChapterObjective.SurviveTime) {
            clock = data.Target;
        }
        if (data.Objective == Story.ChapterObjective.ReachPlace || data.Objective == Story.ChapterObjective.GateAndBack || data.Objective == Story.ChapterObjective.FloorRun) {
            MakeMarker();
        }
        BuildUi();
        StartJournal();
        gameObject.AddComponent<BackRoom>();   // the chapter's secret room, once its clue is held (BackRooms.cs)
        RunAnalytics.Screen("chapter_play_" + (Chapter + 1));
        Cutscene.Reel openReel = Cutscene.LoadReel("ch" + (Chapter + 1) + "_open");
        if (openReel != null) {
            // Every chapter opens with a shot reel (Resources/Cinematics/ch<n>_open.json, Tools/Cinematics/make_reels.py).
            Events.Add("cinematic|" + data.Id + "|open");
            Cutscene.PlayShots(openReel, () => Events.Add("play|" + data.Id));
        } else if (data.Cutscene != null && data.Cutscene.Length > 0) {
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
        if (gateProps != null) {
            Destroy(gateProps);
        }
        if (findItem != null) {
            Destroy(findItem);
        }
        SavePlayTime();
        Companion.Despawn();
        ChapterMap.Unload();
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
        if (Completed && data.Final) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (CreditsUp && Time.unscaledTime > creditsAt && (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter) || PlayInput.GetKeyDown(KeyCode.JoystickButton0) || PlayInput.GetKeyDown(KeyCode.Space))) {
                ToMenu();
            }
            return;
        }
        if (Completed) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Enter, Space and the pad's A press whichever button the gold ring is on (MenuNav on the card).
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
        TickReaction();
        TickJournal();
        ObjectiveLine = Line();
        if (objective != null) {
            objective.text = ObjectiveLine;
        }
        if (xpText != null) {
            xpText.text = (Hard ? "HARD  " : "") + CampaignProgress.HudLine();
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
        if (data.Final && Time.time >= nextPulse && Musician.Instance != null) {
            // The circle is brighter than it was: a soft gold ring breathes out from it.
            nextPulse = Time.time + 2.6f;
            Fx.Ring(Musician.Instance.transform.position + Vector3.up * 0.2f, 14f, new Color(1f, 0.88f, 0.5f, 0.7f), 1.6f);
        }
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
                TickHoldBoss(director, alive);
                break;
            case Story.ChapterObjective.GateAndBack:
                if (leg < 2) {
                    TickGateLegs(director, alive);
                } else {
                    TickHoldBoss(director, alive);
                }
                break;
            case Story.ChapterObjective.FloorRun:
                if (leg <= StopCount) {
                    TickFloorLegs(director, alive);
                } else {
                    TickHoldBoss(director, alive);
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

    // Bots down, then the named boss, then the defeat beat and the card (Chapter 1; Chapter 2 once the waves are held).
    void TickHoldBoss(WaveDirector director, int alive) {
        TickMini(director);
        if (!bossCalled) {
            Progress = Kills();
            if (data.Waves > 0 && data.Objective != Story.ChapterObjective.FloorRun) {
                if (IsReading && waveNow == 0) {
                    StartWave(1);
                } else if (waveNow < data.Waves && Kills() >= waveNow * data.WaveSize) {
                    if (IsReading) {
                        PageRead(waveNow);
                    }
                    StartWave(waveNow + 1);
                }
                if (IsReading && Kills() >= Target() && pagesRead < data.Waves) {
                    PageRead(data.Waves);
                }
            }
            if (spawnIn <= 0f && alive < data.Cap && Kills() + alive < Target()) {
                SpawnCrew(director);
            }
            if (Kills() >= Target() && (data.MiniName == null || (miniSpawned && !MiniUp))) {
                bossCalled = true;
                spawnIn = 3.5f;
                Events.Add("boss-called|" + data.BossName);
                string introId = BossReelId(data.BossName);
                if (introId == null || Cutscene.LoadReel(introId) == null) {
                    Tell(data.BossLines);   // a boss with an intro reel says these lines in it instead
                }
                PlayScene(data.Crossroads, "crossroads");   // the choice that shapes this fight, before the boss's intro
            }
        } else if (!bossSpawned) {
            if (spawnIn <= 0f) {
                bossSpawned = true;
                boss = director.SpawnForChapter(data.Boss, true, RingRadius, data.BossName, data.BossHits, data.PlainBoss);
                Events.Add("boss|" + boss.Title + "|" + boss.HitsToDrop);
                PlayBossIntro();
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
    }

    int Kills() {
        return RunLog.RunKills - killBase;
    }

    // Bots to bring down before the boss: Chapter 1 sets it, Chapter 2 holds Waves waves of WaveSize.
    int Target() {
        if (data.Objective == Story.ChapterObjective.FloorRun) {
            return data.WaveSize;
        }
        if (IsReading) {
            return data.Waves * data.WaveSize;
        }
        return data.Objective == Story.ChapterObjective.GateAndBack ? data.Waves * data.WaveSize : data.Target;
    }

    // Chapter 2, out and back: Wallit walks Target metres to the gate (the scene plays there), then back to the circle,
    // with bots still coming. Then the waves and the boss are TickHoldBoss.
    void TickGateLegs(WaveDirector director, int alive) {
        if (spawnIn <= 0f && alive < data.Cap) {
            SpawnCrew(director);
        }
        if (player != null) {
            Vector3 d = Marker - player.position;
            d.y = 0f;
            Progress = Mathf.CeilToInt(d.magnitude);
            if (leg == 0 && d.magnitude <= ReachRadius) {
                ReachGate();
            } else if (leg == 1 && d.magnitude <= HomeRadius) {
                BackHome();
            }
        }
        if (Time.time >= nextRing && HasMarker) {
            nextRing = Time.time + 1.4f;
            Fx.Ring(Marker + Vector3.up * 0.2f, (leg == 0 ? ReachRadius : HomeRadius) * 2.4f, new Color(1f, 0.82f, 0.38f, 0.8f), 1.2f);
        }
    }

    // Chapter 3, out along the Floor's stops (the broken gate, the belt, the lever) and back, with bots still coming. The
    // last stop is the lever: pulling it breaks the belt feed. Then the bots and the Foreman are TickHoldBoss.
    void TickFloorLegs(WaveDirector director, int alive) {
        TickMini(director);
        if (spawnIn <= 0f && alive < data.Cap) {
            SpawnCrew(director);
        }
        if (player != null) {
            Vector3 d = Marker - player.position;
            d.y = 0f;
            Progress = Mathf.CeilToInt(d.magnitude);
            if (leg < StopCount && d.magnitude <= ReachRadius) {
                ReachStop();
            } else if (leg == StopCount && d.magnitude <= HomeRadius) {
                BackFromFloor();
            }
        }
        if (Time.time >= nextRing && HasMarker) {
            nextRing = Time.time + 1.4f;
            Fx.Ring(Marker + Vector3.up * 0.2f, (leg < StopCount ? ReachRadius : HomeRadius) * 2.4f, new Color(1f, 0.82f, 0.38f, 0.8f), 1.2f);
        }
    }

    void ReachStop() {
        Story.Stop stop = data.Stops[leg];
        Events.Add("stop|" + (leg + 1) + "|" + stop.Name + "|" + Mathf.RoundToInt(PlaySeconds));
        GameAudio.Play("stamp", 0.7f, 0.9f);
        bool last = leg == StopCount - 1;
        leg++;
        if (last) {
            if (data.Walk) {
                Events.Add("last-lamp|" + data.Id);
                Fx.Ring(Marker + Vector3.up * 0.3f, 14f, new Color(1f, 0.82f, 0.38f, 0.8f), 1.4f);
            } else {
                FeedBroken = true;
                Events.Add("feed-broken|" + data.Id);
                GameAudio.Play("stomp", 1f, 0.5f);
                Fx.Ring(Marker + Vector3.up * 0.3f, 18f, new Color(1f, 0.3f, 0.2f, 0.8f), 1.4f);
                if (floorProps != null) {
                    floorProps.SetBool("feed", false);
                }
            }
            // The marker turns around to the circle, as it does at Chapter 2's gate.
            if (Musician.Instance != null && beacon != null) {
                Marker = Musician.Instance.transform.position;
                beacon.transform.position = Marker;
            }
        } else {
            Vector3 next = StopPoint(data.Stops[leg]);
            Marker = next;
            if (beacon != null) {
                beacon.transform.position = next;
            }
        }
        if (stop.Scene != null && stop.Scene.Length > 0) {
            Events.Add("stop-scene|" + stop.Name);
            string[] after = stop.Lines;
            Cutscene.Play(stop.Scene, () => {
                Events.Add("stop-scene-done|" + stop.Name);
                Tell(after);
            });
        } else {
            Tell(stop.Lines);
        }
        if (last) {
            Tell(data.WaveLines);
        }
    }

    void BackFromFloor() {
        leg = StopCount + 1;
        killBase = RunLog.RunKills;
        spawnIn = 2.5f;
        Events.Add("home|" + Mathf.RoundToInt(PlaySeconds));
        if (beacon != null) {
            beacon.SetActive(false);
        }
        HasMarker = false;
    }

    Vector3 StopPoint(Story.Stop stop) {
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        float a = stop.Bearing * Mathf.Deg2Rad;
        Vector3 at = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * stop.Dist;
        at.y = origin.y;
        if (Physics.Raycast(new Vector3(at.x, 60f, at.z), Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        return at;
    }

    void ReachGate() {
        leg = 1;
        Events.Add("gate|reached|" + Mathf.RoundToInt(PlaySeconds));
        GameAudio.Play("stamp", 0.7f, 0.9f);
        // The marker turns around to the circle: the way back is the next leg.
        if (Musician.Instance != null && beacon != null) {
            Marker = Musician.Instance.transform.position;
            beacon.transform.position = Marker;
        }
        Events.Add("gate-scene|" + data.Id);
        Cutscene.Play(data.GateScene, () => Events.Add("gate-scene-done|" + data.Id));
    }

    void BackHome() {
        leg = 2;
        killBase = RunLog.RunKills;
        spawnIn = 2.5f;
        Events.Add("home|" + Mathf.RoundToInt(PlaySeconds));
        if (beacon != null) {
            beacon.SetActive(false);
        }
        HasMarker = false;
        StartWave(1);
    }

    // A page is read out when its wave is down: its forms on the wall turn over, blank.
    void PageRead(int page) {
        pagesRead = page;
        Events.Add("page-read|" + page + "|" + data.Waves);
        GameAudio.Play("stamp", 0.7f, 0.9f + page * 0.05f);
        // The Reading Room's own scene turns that page's forms over (ReadingMap).
        if (ChapterMap.Current != null && ChapterMap.Current.Own != null) {
            ChapterMap.Current.Own.PageRead(page);
        }
    }

    void StartWave(int w) {
        waveNow = w;
        Events.Add("wave|" + w + "|" + data.Waves);
        UpgradeLook.Say((IsReading ? "PAGE " : "WAVE ") + w + " OF " + data.Waves, 2.2f, 160f);
        if (data.WaveLines != null && w - 1 < data.WaveLines.Length) {
            Tell(new[] { data.WaveLines[w - 1] });
        }
    }

    void SpawnCrew(WaveDirector director) {
        spawnIn = data.Every;
        BotKind kind = data.Crew[Random.Range(0, data.Crew.Length)];
        Enemy bot = director.SpawnForChapter(kind, false, RingRadius + Random.Range(0f, 10f));
        crewSpawned++;
        if (data.VariantEvery > 0 && crewSpawned % data.VariantEvery == 0 && bot != null && !BotRoster.IsSpider(kind)) {
            bot.MakeVariant(data.Variant, data.VariantName);
            if (!variantSeen) {
                variantSeen = true;
                Events.Add("variant|" + data.Id + "|" + data.VariantName + "|" + data.Variant);
                UpgradeLook.Say("New: " + data.VariantName, 2.4f, 160f);
                Tell(new[] { data.VariantLine });
            }
        }
    }

    // The mini-boss midway: Chapter 2 and 5 at half the bots down, Chapter 3 once Wallit has reached the belt. The boss waits for it.
    bool MiniUp { get { return mini != null && mini.Alive; } }
    public Enemy MiniEnemy { get { return mini; } }

    void TickMini(WaveDirector director) {
        if (data.MiniName == null || miniSpawned) {
            return;
        }
        bool midway = data.Objective == Story.ChapterObjective.FloorRun ? leg >= 2 : Kills() >= Target() / 2 && (data.Objective != Story.ChapterObjective.GateAndBack || leg >= 2);
        if (!midway) {
            return;
        }
        miniSpawned = true;
        mini = director.SpawnMini(data.MiniKind, data.MiniName, data.MiniHits, data.MiniTint, RingRadius);
        Events.Add("mini-boss|" + data.MiniName + "|" + mini.HitsToDrop);
        Tell(data.MiniLines);
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
            case Story.ChapterObjective.FloorRun:
                if (leg < StopCount) {
                    return "Reach " + data.Stops[leg].Name + " (" + (leg + 1) + " of " + StopCount + "):  " + Progress + " m";
                }
                if (leg == StopCount) {
                    return (data.Walk ? "The last lamp is lit. Back to the circle:  " : "The feed is broken. Back to the circle:  ") + Progress + " m";
                }
                if (!bossCalled) {
                    return (data.Walk ? "Hold the circle against the servers:  " : "Hold the circle against the last batch:  ") + Mathf.Min(Progress, Target()) + " / " + Target() + " down";
                }
                if (defeatIn > 0f) {
                    return data.BossName + " is down";
                }
                if (data.Walk && bossSpawned && !BossDown) {
                    return Server.Struck ? "Bring down " + data.BossName
                        : "Refuse the paper (hit " + data.BossName + " when it offers):  " + Mathf.Min(Server.Refusals, Server.RefusalsNeeded) + " / " + Server.RefusalsNeeded;
                }
                return "Bring down " + data.BossName + (bossSpawned ? "" : "  (it's coming)");
            case Story.ChapterObjective.GateAndBack:
                if (leg == 0) {
                    return "Reach " + data.Goal + ":  " + Progress + " m";
                }
                if (leg == 1) {
                    return "Back to the circle:  " + Progress + " m";
                }
                if (!bossCalled) {
                    return "Hold the circle, wave " + waveNow + " of " + data.Waves + ":  " + Mathf.Min(Progress, Target()) + " / " + Target() + " down";
                }
                if (defeatIn > 0f) {
                    return data.BossName + " is down";
                }
                return "Bring down " + data.BossName + (bossSpawned ? "" : "  (it's coming)");
            case Story.ChapterObjective.HoldThenBoss:
                if (IsReading) {
                    if (!bossCalled) {
                        return "Read the order, page " + Mathf.Clamp(waveNow, 1, data.Waves) + " of " + data.Waves + ":  " + Mathf.Min(Progress, Target()) + " / " + Target() + " down";
                    }
                    if (defeatIn > 0f) {
                        return data.BossName + " is down";
                    }
                    return "Bring down " + data.BossName + (bossSpawned ? "" : "  (it's coming)");
                }
                if (!bossCalled) {
                    return "Hold the circle:  " + Mathf.Min(Progress, data.Target) + " / " + data.Target + " " + data.Goal + " down";
                }
                if (defeatIn > 0f) {
                    return data.BossName + " is down";
                }
                if (data.Final) {
                    return "Bring down the Spider Bot with the order" + (bossSpawned ? "" : "  (it's coming)");
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
        string extra = JudgeGoal();
        GameAudio.Play("stamp", 0.7f);
        CampaignSave.Complete(Chapter);
        Rank = CampaignGrade.Rank(Chapter, PlaySeconds, DamageTaken);
        NewBest = CampaignSave.Grade(Chapter, Rank, Hard);
        int best = CampaignSave.BestRank(CampaignSave.Get(CampaignSave.Active), Chapter, Hard);
        Events.Add("rank|" + CampaignGrade.Letter(Rank) + "|" + Mathf.RoundToInt(PlaySeconds) + "|" + DamageTaken + "|" + (Hard ? "hard" : "standard") + "|best " + CampaignGrade.Letter(best) + (NewBest ? "|new" : ""));
        SavePlayTime();
        foreach (Enemy e in new List<Enemy>(Enemy.Roster)) {
            if (e != null) {
                Destroy(e.gameObject);
            }
        }
        if (beacon != null) {
            beacon.SetActive(false);
        }
        if (data.Final) {
            PlayScene(data.After, "after", CompleteFinal);
            return;
        }
        // The aftermath scene (choices that carry to later chapters), then the closing reel, then the card.
        PlayScene(data.After, "after", () => {
            Cutscene.Reel closeReel = Cutscene.LoadReel("ch" + (Chapter + 1) + "_close");
            if (closeReel != null) {
                // Chapters 1-5 close with a reel before the Chapter Complete card.
                Events.Add("cinematic|" + data.Id + "|close");
                Cutscene.PlayShots(closeReel, () => ShowCompleteCard(extra, best));
            } else {
                ShowCompleteCard(extra, best);
            }
        });
    }

    // A chapter scene made of data that may hold dialogue choices (Q| lines) and lines that read them (?| lines). Nothing to
    // play when it has none: next runs at once.
    void PlayScene(string[] scene, string kind, System.Action next = null) {
        if (scene == null || scene.Length == 0) {
            next?.Invoke();
            return;
        }
        Events.Add(kind + "|" + data.Id);
        Cutscene.Play(scene, () => {
            Events.Add(kind + "-done|" + data.Id);
            next?.Invoke();
        });
    }

    // The boss's phase two: the companion answers a choice made earlier (Story.Chapter.PhaseTwo), once.
    void TickReaction() {
        if (reacted || boss == null || boss.Phase < 2 || data.PhaseTwo == null) {
            return;
        }
        reacted = true;
        string[] lines = Choices.Resolve(data.PhaseTwo);
        if (lines.Length > 0) {
            Events.Add("choice-reaction|" + data.Id + "|" + lines.Length);
        }
        Tell(lines);
    }

    void ShowCompleteCard(string extra, int best) {
        bool more = Chapter + 1 < CampaignSave.ChapterCount;
        cardTitle.text = "CHAPTER COMPLETE";
        var body = new System.Text.StringBuilder(data.Title);
        foreach (string raw in Choices.Resolve(data.Done)) {
            body.Append("\n").Append(Spoken(raw));
        }
        body.Append("\n\n").Append(CampaignGrade.Line(Chapter, Rank, PlaySeconds, DamageTaken, Hard));
        body.Append("\nBest " + CampaignGrade.Letter(best) + (Hard ? " on Hard" : "") + (NewBest ? "  (new best)" : ""));
        body.Append("\nLV " + CampaignProgress.Level + "   XP " + CampaignProgress.Xp);
        body.Append("\n").Append(extra);
        if (Lore.LastUnlocked.Length > 0) {
            body.Append("\nNew lore: ").Append(string.Join(", ", Lore.LastUnlocked));
            Events.Add("lore|" + string.Join(";", Lore.LastUnlocked));
        }
        body.Append("\n").Append(more ? "Back to the hub. Next on the board: " + Story.Chapters[Chapter + 1].Title : "The case file is filed. The board keeps every chapter.");
        cardBody.text = body.ToString();
        NextButton.gameObject.SetActive(true);
        card.SetActive(true);
        objective.text = "";
        if (extraText != null) {
            extraText.text = "";
        }
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RunAnalytics.Screen("chapter_complete_" + (Chapter + 1));
    }

    // The last chapter: the Filed scene, the Finale pages (Story.Finale, as in Arcade), the ADDRESS FILED ending card, then the
    // credits card. The case file is already marked filed (Complete saved it).
    void CompleteFinal() {
        objective.text = "";
        if (extraText != null) {
            extraText.text = "";
        }
        Events.Add("finale|start");
        RunAnalytics.Screen("chapter_complete_" + (Chapter + 1));
        string stats = "Case file " + (CampaignSave.Active + 1) + ": filed\n" + CampaignSave.Played(CampaignSave.Get(CampaignSave.Active)) + "\n" + CampaignGrade.Line(Chapter, Rank, PlaySeconds, DamageTaken, Hard);
        // ADDRESS FILED plays as a reel (ch6_close: the order drops, DENIED, the door number); the old page scene is the fallback.
        Cutscene.Reel filed = Cutscene.LoadReel("ch6_close");
        if (filed != null) {
            Events.Add("cinematic|" + data.Id + "|close");
            Cutscene.PlayShots(filed, () => {
                Events.Add("finale|reel");
                // The reel is fixed; the choices Wallit made are read after it, in the ending's own lines (the ? lines).
                string[] notes = Choices.Resolve(System.Array.FindAll(data.Ending, l => l.StartsWith("?|")));
                var scene = new List<string> { "B|on", "C|wide" };
                scene.AddRange(notes);
                PlayScene(notes.Length > 0 ? scene.ToArray() : null, "ending-notes", () => StoryScreen.Ensure().ShowEndCard(stats, keepGoing => AfterFiled(keepGoing)));
            });
            return;
        }
        Cutscene.Play(data.Ending, () => {
            Events.Add("finale|pages");
            StoryScreen.Ensure().ShowFinale(stats, keepGoing => AfterFiled(keepGoing));
        });
    }

    // After the ADDRESS FILED card: the secret ending (every lost thing kept) as a reel, then the credits reel and card.
    void AfterFiled(bool keepGoing) {
        Events.Add("finale|" + (keepGoing ? "credits" : "menu"));
        if (CampaignSave.SecretEarned(CampaignSave.Get(CampaignSave.Active))) {
            SecretShown = true;
            CampaignSave.MarkSecret();
            Events.Add("secret|ending");
            RunAnalytics.Screen("secret_ending");
            Cutscene.Reel received = Cutscene.LoadReel("ending_received");
            System.Action after = () => {
                if (keepGoing) {
                    PlayCreditsReel();
                } else {
                    ToMenu();
                }
            };
            if (received != null) {
                Cutscene.PlayShots(received, after);
            } else {
                Cutscene.Play(Story.SecretEnding, after);
            }
        } else if (keepGoing) {
            PlayCreditsReel();
        } else {
            ToMenu();
        }
    }

    void PlayCreditsReel() {
        Cutscene.Reel credits = Cutscene.LoadReel("credits");
        if (credits == null) {
            ShowCredits();
            return;
        }
        Events.Add("cinematic|" + data.Id + "|credits");
        Cutscene.PlayShots(credits, ShowCredits);
    }

    // The named boss's intro reel (boss_courier, boss_foreman, boss_server, boss_order) plays as it arrives, over the frozen field.
    void PlayBossIntro() {
        string id = BossReelId(data.BossName);
        Cutscene.Reel reel = id != null ? Cutscene.LoadReel(id) : null;
        if (reel == null) {
            return;
        }
        Events.Add("cinematic|" + data.Id + "|" + id);
        Cutscene.PlayShots(reel, () => Events.Add("boss-intro-done|" + id));
    }

    public static string BossReelId(string bossName) {
        switch (bossName) {
            case "The Courier": return "boss_courier";
            case "The Foreman": return "boss_foreman";
            case "The Server": return "boss_server";
            case "The Order": return "boss_order";
        }
        return null;
    }

    void ShowCredits() {
        CreditsUp = true;
        creditsAt = Time.unscaledTime + 0.7f;
        cardTitle.text = Story.CreditsTitle;
        cardBody.text = Story.Credits + (SecretShown ? Story.SecretCredits : "");
        NextButton.gameObject.SetActive(false);
        card.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RunAnalytics.Screen("credits");
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
        ChapterMap.Unload();
        Time.timeScale = 1f;
        toNext = true;
        ChapterScene.HubNext = true;
        RunAnalytics.Screen("hub");
        SceneManager.LoadScene(CampaignSave.Scene);
    }

    // Playtest setup shortcut: leave the chapter for the menu without finishing it.
    public void Abandon() {
        ToMenu();
    }

    void ToMenu() {
        ChapterMap.Unload();
        Time.timeScale = 1f;
        RunAnalytics.Screen("menu");
        SceneManager.LoadScene(MetaProgress.MenuScene);
    }

    // Lines said one after another over the field (the boss's arrival, the defeat beat).
    void Tell(string[] lines) {
        if (lines == null) {
            return;
        }
        queue.AddRange(Choices.Resolve(lines));   // lines that depend on a dialogue choice (Choices.cs) are settled here
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

    public static void Say(string raw, float seconds) {
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
        if (data.Objective == Story.ChapterObjective.FloorRun) {
            if (!data.Walk) {
                BuildFloor(origin);
            }
            at = StopPoint(data.Stops[0]);
        } else if (Physics.Raycast(new Vector3(at.x, 60f, at.z), Vector3.down, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        Marker = at;
        HasMarker = true;
        if (data.Objective == Story.ChapterObjective.GateAndBack) {
            BuildGate(at, origin);
        }
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

    // The gate with no handle, a stretch of the Department's wall either side and a signpost with nothing on it. Flat
    // blocks with no colliders (the playfield stays free of anything solid), turned to face the circle.
    void BuildGate(Vector3 at, Vector3 origin) {
        gateProps = new GameObject("Chapter Gate");
        gateProps.transform.position = at;
        Vector3 toCircle = origin - at;
        toCircle.y = 0f;
        gateProps.transform.rotation = Quaternion.LookRotation(toCircle.sqrMagnitude > 0.01f ? toCircle.normalized : Vector3.forward, Vector3.up);
        Color stone = new Color(0.2f, 0.21f, 0.27f, 1f);
        Color dark = new Color(0.1f, 0.11f, 0.15f, 1f);
        Color pale = new Color(0.82f, 0.8f, 0.72f, 1f);
        Block("Post L", new Vector3(-4.6f, 4.5f, 0f), new Vector3(1.4f, 9f, 1.4f), stone);
        Block("Post R", new Vector3(4.6f, 4.5f, 0f), new Vector3(1.4f, 9f, 1.4f), stone);
        Block("Lintel", new Vector3(0f, 9.4f, 0f), new Vector3(11.4f, 1.6f, 1.6f), stone);
        Block("Door", new Vector3(0f, 4.4f, 0.1f), new Vector3(7.8f, 8.8f, 0.5f), dark);
        Block("Wall L", new Vector3(-13.5f, 3.5f, 0f), new Vector3(16f, 7f, 1.2f), dark);
        Block("Wall R", new Vector3(13.5f, 3.5f, 0f), new Vector3(16f, 7f, 1.2f), dark);
        Block("Signpost", new Vector3(8.5f, 2f, 5f), new Vector3(0.35f, 4f, 0.35f), stone);
        Block("Blank Sign", new Vector3(8.5f, 3.9f, 5.25f), new Vector3(2.4f, 1.2f, 0.12f), pale);
    }

    // The Clearing Floor: at each stop a few flat, collider-free blocks (hooks with chassis, the belt with cleared things,
    // the lever and its red lamp), so the way out reads as a place. Nothing solid within the playfield.
    void BuildFloor(Vector3 origin) {
        gateProps = new GameObject("Chapter Floor");
        floorProps = gateProps.AddComponent<FloorState>();
        Color iron = new Color(0.16f, 0.17f, 0.22f, 1f);
        Color lamp = new Color(1f, 0.74f, 0.35f, 1f);
        Color chassis = new Color(0.55f, 0.58f, 0.66f, 1f);
        Color red = new Color(0.9f, 0.14f, 0.1f, 1f);
        for (int i = 0; i < data.Stops.Length; i++) {
            Vector3 at = StopPoint(data.Stops[i]);
            var holder = new GameObject("Stop " + (i + 1) + " " + data.Stops[i].Name);
            holder.transform.SetParent(gateProps.transform, false);
            holder.transform.position = at;
            Vector3 toCircle = origin - at;
            toCircle.y = 0f;
            holder.transform.rotation = Quaternion.LookRotation(toCircle.sqrMagnitude > 0.01f ? toCircle.normalized : Vector3.forward, Vector3.up);
            if (i == 0) {
                // Broken gate: two leaning posts and a hook row with chassis.
                Block(holder.transform, "Post L", new Vector3(-6f, 3.5f, 0f), new Vector3(1.2f, 7f, 1.2f), iron, 8f);
                Block(holder.transform, "Post R", new Vector3(6f, 2.2f, 0f), new Vector3(1.2f, 4.4f, 1.2f), iron, -14f);
                for (int k = -2; k <= 2; k++) {
                    Block(holder.transform, "Hook " + k, new Vector3(k * 4f, 6f, -9f), new Vector3(0.2f, 3f, 0.2f), iron, 0f);
                    Block(holder.transform, "Chassis " + k, new Vector3(k * 4f, 4.3f, -9f), new Vector3(1.6f, 1.8f, 1.2f), chassis, 0f);
                }
            } else if (i == data.Stops.Length - 1) {
                // Lever and a red lamp lens.
                Block(holder.transform, "Lever Post", new Vector3(0f, 1.6f, 0f), new Vector3(1f, 3.2f, 1f), iron, 0f);
                Block(holder.transform, "Lever", new Vector3(0f, 3.8f, 0f), new Vector3(0.3f, 3.4f, 0.3f), lamp, 35f);
                Block(holder.transform, "Red Lamp", new Vector3(0f, 9f, -6f), new Vector3(2.4f, 2.4f, 0.5f), red, 0f);
                Block(holder.transform, "Chute", new Vector3(0f, 1.5f, -8f), new Vector3(8f, 3f, 6f), iron, 0f);
            } else {
                // The belt, with cleared things riding it: a hat, a clock, a bell.
                Block(holder.transform, "Belt", new Vector3(0f, 0.5f, 0f), new Vector3(30f, 1f, 3.4f), iron, 0f);
                Block(holder.transform, "Hat", new Vector3(-8f, 1.5f, 0f), new Vector3(1.6f, 1.0f, 1.6f), new Color(0.35f, 0.3f, 0.4f, 1f), 0f);
                Block(holder.transform, "Clock", new Vector3(0f, 1.8f, 0f), new Vector3(1.6f, 1.6f, 0.4f), lamp, 0f);
                Block(holder.transform, "Bell", new Vector3(8f, 1.7f, 0f), new Vector3(1.0f, 1.4f, 1.0f), new Color(0.8f, 0.65f, 0.3f, 1f), 0f);
            }
        }
    }

    // Marks whether the belt feed still runs (the lamps shown on the props); a plain flag the chapter flips at the lever.
    public class FloorState : MonoBehaviour {
        bool feed = true;
        public void SetBool(string key, bool value) {
            if (key == "feed") {
                feed = value;
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) {
                    if (r.name == "Red Lamp") {
                        r.material.color = value ? new Color(0.9f, 0.14f, 0.1f, 1f) : new Color(0.25f, 0.05f, 0.05f, 1f);
                    }
                }
            }
        }
    }

    void Block(Transform parent, string name, Vector3 local, Vector3 size, Color color, float roll) {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        Destroy(cube.GetComponent<Collider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = local;
        cube.transform.localScale = size;
        cube.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        var shader = SafeShader.Find("Sprites/Default");
        if (shader != null) {
            cube.GetComponent<Renderer>().material = new Material(shader) { color = color };
        } else {
            UpgradeLook.Paint(cube, color);
        }
        cube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Block(string name, Vector3 local, Vector3 size, Color color) {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        Destroy(cube.GetComponent<Collider>());
        cube.transform.SetParent(gateProps.transform, false);
        cube.transform.localPosition = local;
        cube.transform.localScale = size;
        var shader = SafeShader.Find("Sprites/Default");
        if (shader != null) {
            cube.GetComponent<Renderer>().material = new Material(shader) { color = color };
        } else {
            UpgradeLook.Paint(cube, color);
        }
        cube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- Journal

    // The chapter's lost thing (a small brass tag lying out in the field, no collider) unless the file already holds it,
    // and the baselines the optional objective is judged from.
    void StartJournal() {
        CampaignSave.Slot slot = CampaignSave.Active >= 0 ? CampaignSave.Get(CampaignSave.Active) : null;
        baseHomeRuns = BatEquipment.HomeRuns;
        baseSmash = BatEquipment.SmashHits;
        baseFinishers = BatEquipment.Finishers;
        FindKept = Journal.HasFind(slot, Chapter);
        GoalMet = Journal.HasGoal(slot, Chapter);
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        findAt = Journal.FindPoint(Chapter, origin);
        if (!FindKept) {
            findItem = new GameObject("Hidden Find");
            findItem.transform.position = findAt;
            findSpin = UpgradeLook.BuildBolt(findItem.transform, 1.3f);
            findSpin.localPosition = new Vector3(0f, 0.7f, 0f);
        }
        Events.Add("journal|find " + Journal.Finds[Chapter].Name + (FindKept ? " already kept" : " hidden at " + findAt.ToString("0") + ", " + Journal.Finds[Chapter].Dist + " m out"));
        Events.Add("journal|extra " + Journal.Goals[Chapter].Name + ": " + Journal.Goals[Chapter].Ask + (GoalMet ? " (already met)" : ""));
        if (!GoalMet) {
            Tell(new[] { "N|Extra, never asked: " + Journal.Goals[Chapter].Name + ". " + Journal.Goals[Chapter].Ask });
        }
    }

    // Walking onto the find keeps it (saved at once); a soft ring shows when Wallit is within reach of finding it.
    void TickJournal() {
        if (findItem != null && player != null) {
            findSpin.Rotate(0f, 90f * Time.deltaTime, 0f);
            Vector3 d = findAt - player.position;
            d.y = 0f;
            if (d.magnitude <= 3.2f) {
                KeepFind();
            } else if (d.magnitude <= 22f && Time.time >= findRing) {
                findRing = Time.time + 1.6f;
                Fx.Ring(findAt + Vector3.up * 0.2f, 3f, new Color(1f, 0.84f, 0.4f, 0.55f), 1.2f);
            }
        }
        if (extraText != null) {
            extraText.text = GoalMet ? "Extra met: " + Journal.Goals[Chapter].Name : "Extra: " + Journal.Goals[Chapter].Name + " (" + Journal.Goals[Chapter].Ask + ")  J: journal";
        }
        if (PlayInput.GetKeyDown(KeyCode.J)) {
            PauseMenu.Open();
            if (PauseMenu.IsOpen) {
                JournalPanel.Open(CampaignSave.Active);
            }
        }
    }

    void KeepFind() {
        if (findItem == null) {
            return;
        }
        Destroy(findItem);
        findItem = null;
        FindKept = true;
        Journal.Keep(Chapter);
        GameAudio.Play("stamp", 0.5f, 1.3f);
        Fx.Ring(findAt + Vector3.up * 0.2f, 6f, new Color(1f, 0.88f, 0.5f, 0.8f), 0.8f);
        Events.Add("journal|found " + Journal.Finds[Chapter].Name);
        Say("N|Found: " + Journal.Finds[Chapter].Name + ". Kept in the journal.", 3.4f);
        PaidCheck();
    }

    // At the filing: the extra is judged from what this play did. Returns the line for the card.
    string JudgeGoal() {
        Musician slice = Musician.Instance;
        bool met = Journal.Holds(Chapter, slice != null ? slice.DamageTaken : 0, BatEquipment.HomeRuns - baseHomeRuns, BatEquipment.SmashHits - baseSmash, BatEquipment.Finishers - baseFinishers, PlaySeconds);
        string line;
        if (GoalMet) {
            line = "Extra already filed: " + Journal.Goals[Chapter].Name;
        } else if (met) {
            GoalMet = true;
            Journal.Meet(Chapter);
            line = "Extra filed: " + Journal.Goals[Chapter].Name + "  " + Spoken(Journal.Goals[Chapter].Done);
            Events.Add("journal|extra met " + Journal.Goals[Chapter].Name);
        } else {
            line = "Extra missed: " + Journal.Goals[Chapter].Name + ". " + Journal.Goals[Chapter].Ask + " It can be tried again from the board.";
            Events.Add("journal|extra missed " + Journal.Goals[Chapter].Name);
        }
        line += FindKept ? "\nLost thing kept: " + Journal.Finds[Chapter].Name : "\nA lost thing is still out there in this chapter.";
        if (Journal.PaidInFull(CampaignSave.Get(CampaignSave.Active))) {
            line += "\n" + Journal.PaidTitle + ". The bat is gilt.";
            Events.Add("journal|paid in full");
        }
        return line;
    }

    // The last find or extra to come in pays the file out.
    void PaidCheck() {
        if (CampaignSave.Active >= 0 && Journal.PaidInFull(CampaignSave.Get(CampaignSave.Active))) {
            Tell(new[] { Journal.PaidLine });
            Events.Add("journal|paid in full");
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

        extraText = Label(canvasGo.GetComponent<RectTransform>(), "", 22, FontStyle.Italic, new Color(0.85f, 0.9f, 1f));
        var erect = extraText.rectTransform;
        erect.anchorMin = new Vector2(0.5f, 0f);
        erect.anchorMax = new Vector2(0.5f, 0f);
        erect.pivot = new Vector2(0.5f, 0f);
        erect.anchoredPosition = new Vector2(0f, 150f);
        erect.sizeDelta = new Vector2(1100f, 36f);

        xpText = Label(canvasGo.GetComponent<RectTransform>(), "", 26, FontStyle.Bold, new Color(0.62f, 0.95f, 0.86f));
        var xrect = xpText.rectTransform;
        xrect.anchorMin = new Vector2(0f, 1f);
        xrect.anchorMax = new Vector2(0f, 1f);
        xrect.pivot = new Vector2(0f, 1f);
        xrect.anchoredPosition = new Vector2(24f, -150f);
        xrect.sizeDelta = new Vector2(1250f, 80f);
        xpText.alignment = TextAnchor.MiddleLeft;

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
        NextButton = MakeButton(column, "To the hub", 380f, 88f, 760f, Next);
        MenuButton = MakeButton(column, "Menu", 260f, 88f, 760f, ToMenu);
        MenuNav.Attach(card, 300);
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
