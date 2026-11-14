using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// In-engine cinematic player: a reel of world shots (camera dolly/orbit/cut, actors on marks with walk/idle/tell,
// voice through VoiceDirector reason "cutscene", SFX, letterbox, captions). Time keeps running so feet and poses
// animate; the follow camera still ticks underneath and this writes the view in LateUpdate. Skip (Esc, Enter, a
// desktop click, or the touch Skip pill) ends the whole scene and blends to the follow camera in under a second.
// Data: Resources/Cinematics/<id>.json. Missing actors fall back to Cutscene.PlayShots(fallback).
[DefaultExecutionOrder(2000)]
public partial class CinematicDirector : MonoBehaviour {
    [Serializable] public class Mark {
        public string id;
        public float[] at;
        public float[] from;
        public float[] to;
        // Polar marks for chapter maps: [bearingDeg, alongMetres, sideMetres] from the circle (Musician).
        public float[] atPolar;
        public float[] fromPolar;
        public float[] toPolar;
        public float yaw;
        public string anim;
        public bool tell;
        // Extra height above the ground. JsonUtility leaves missing floats at 0 (= actor default: yobot hangs at 2.4, spider/mega stand at 0.55). Set a positive lift to stand a yobot on the ground.
        public float lift;
    }
    [Serializable] public class Shot {
        public string id;
        public float seconds = 4f;
        public string camMode = "dolly";
        public float[] camFrom;
        public float[] camTo;
        public float[] lookAt;
        // Polar camera: [bearingDeg, along, side, heightY] from the circle.
        public float[] camFromPolar;
        public float[] camToPolar;
        public float[] lookPolar;
        public float fov = 55f;
        public string[] frame;
        public string speaker = "N";
        public string line;
        public string sfx;
        public float[] stamp;
        public bool slam;
        public bool postmark;   // Chapter 2: slam a torn postmark onto the gate
        public bool belt;       // Chapter 3: belt of cleared things slides toward the shredder
        public bool paper;      // Chapter 4: the Server folds a leg and offers its paper
        public bool addressStamp; // ADDRESS FILED / RECEIVED: the stamp comes down and prints ADDRESS
        public bool finaleCarry;  // ADDRESS FILED: player holds swing to carry the stamp; bats 3 notices
        public bool doors;      // RECEIVED: six doors rise around the ring (one per lost thing)
        public bool keepsakes;  // RECEIVED: six lost things laid in the ring (seen, not only narrated)
        public bool credits;    // credits scroll over the live scene on this shot
        public bool handoff;
        public Mark[] actors;
    }
    [Serializable] public class Reel {
        public string id;
        public string fallback;
        public float seconds;
        public Shot[] shots;
    }

    public static CinematicDirector Instance { get; private set; }
    // True while shots play, live credits scroll, or the 0.6 s camera handoff is still blending onto the follow camera.
    public static bool IsOpen { get { return Instance != null && (Instance.open || Instance.handingOff || Instance.bossRun || Instance.actRun || Instance.creditsOnly); } }
    // Other playtests turn the Chapter 1 opening off so their bot starts with control.
    public static bool SuppressOpening;
    const float MaxVoiceSeconds = 12f;
    public static readonly List<string> Events = new List<string>();
    public static int ShotsShown { get; private set; }
    public static int VoiceLines { get; private set; }
    public static int Skipped { get; private set; }
    public static int Finished { get; private set; }
    public static float LastHandoffSeconds { get; private set; }
    public static float CaptionBottom01 { get; private set; }
    public static int DoorsShown { get; private set; }
    public static bool AddressStampSeen { get; private set; }
    public static float CreditsTextPx { get; private set; }
    public static float CreditsSkipMs { get; private set; }
    public static bool CreditsLive { get; private set; }
    // Finale carry beat (Chapter 6 ADDRESS FILED): hold swing, bat notices, release to print.
    public static bool FinaleCarryActive { get; private set; }
    public static bool FinaleCarryReady { get; private set; }
    public static float FinaleCarryProgress { get; private set; }
    public static float FinaleCarrySeconds { get; private set; }
    public static int FinaleCarryBats { get; private set; }
    public static int FinaleCarryMisses { get; private set; }
    public static int FinaleLampsLit { get; private set; }
    public static float FinaleHoldPromptWidthPx { get; private set; }
    public static float FinaleHoldPromptCenterY01 { get; private set; }
    public static bool FinaleTimingOpen { get; private set; }

    public int ShotIndex { get { return shotIndex; } }
    public int ShotCount { get { return reel != null && reel.shots != null ? reel.shots.Length : 0; } }
    public string ShotId { get { return reel != null && shotIndex >= 0 && shotIndex < ShotCount ? reel.shots[shotIndex].id : ""; } }
    public string ReelId { get { return reel != null ? reel.id : ""; } }
    public string Subtitle { get { return caption != null ? caption.text : ""; } }
    public string SubtitleSpeaker { get; private set; }
    public bool SkipPillShown { get { return skipPill != null && skipPill.activeSelf; } }

    bool open;
    bool handingOff;
    Reel reel;
    int shotIndex = -1;
    float shotStart;
    float shotLength;
    bool shotSpoke;
    bool shotSfx;
    bool shotStamp;
    bool shotSlam;
    Action done;
    Camera cam;
    ZeldaStyleCamera zelda;
    float savedFov = 60f;
    Vector3 viewPos;
    Quaternion viewRot = Quaternion.identity;
    bool viewSet;

    LevelYard yard;
    bool chapterHost;          // ChapterRun in scene2 (EdgeMap / FloorMap), not LevelYard
    Vector3 circle;            // Musician / ring centre for polar marks
    float chapterBearing;      // chapter route bearing (gate / belt)
    Transform wallit;
    CharacterController wallitBody;
    MovementMotor wallitMotor;
    Transform slice;
    Animator sliceAnim;
    Companion sliceCompanion;
    Enemy courier;
    Enemy yobot;
    Enemy spider;
    Enemy mega;
    bool courierOwned;
    bool yobotOwned;
    bool spiderOwned;
    bool megaOwned;
    bool shotPostmark;
    bool shotBelt;
    bool shotPaper;
    bool shotAddressStamp;
    bool shotDoors;
    bool shotKeepsakes;
    bool shotCredits;
    GameObject postmarkGo;
    Transform postmarkHead;
    GameObject addressStampGo;
    Transform addressStampHead;
    readonly List<GameObject> doorGos = new List<GameObject>();
    Text creditsScroll;
    RectTransform creditsRt;
    float creditsStart = -1f;
    bool creditsOnly;          // PlayCreditsLive: orbit the ring and scroll, no shot reel
    string creditsBody;

    Font font;
    CanvasScaler scaler;
    int uiStamp = -1;
    GameObject uiRoot;
    RectTransform topBar;
    RectTransform bottomBar;
    float barsNow;
    Text caption;
    RectTransform captionRt;
    Text namePlate;
    GameObject skipPill;
    Button skipButton;
    bool skipRevealed;
    float touchDownAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Instance = null;
        SuppressOpening = false;
        Events.Clear();
        ShotsShown = 0;
        VoiceLines = 0;
        Skipped = 0;
        Finished = 0;
        LastHandoffSeconds = 0f;
        CaptionBottom01 = 0f;
        DoorsShown = 0;
        AddressStampSeen = false;
        CreditsTextPx = 0f;
        CreditsSkipMs = 0f;
        CreditsLive = false;
        FinaleCarryActive = false;
        FinaleCarryReady = false;
        FinaleCarryProgress = 0f;
        FinaleCarrySeconds = 0f;
        FinaleCarryBats = 0;
        FinaleCarryMisses = 0;
        FinaleLampsLit = 0;
        FinaleHoldPromptWidthPx = 0f;
        FinaleHoldPromptCenterY01 = 0f;
        FinaleTimingOpen = false;
    }

    public static void Play(string id, LevelYard host, Action onDone) {
        Reel r = Load(id);
        if (r == null || r.shots == null || r.shots.Length == 0) {
            onDone?.Invoke();
            return;
        }
        if (Instance == null) {
            Instance = new GameObject("Cinematic Director").AddComponent<CinematicDirector>();
        }
        Instance.Begin(r, host, onDone);
    }

    // Chapter 2 / 3 openings in scene2: Wallit is the run's player, Slice is the companion or the ring figure.
    public static void PlayChapter(string id, Action onDone) {
        Reel r = Load(id);
        if (r == null || r.shots == null || r.shots.Length == 0) {
            onDone?.Invoke();
            return;
        }
        if (Instance == null) {
            Instance = new GameObject("Cinematic Director").AddComponent<CinematicDirector>();
        }
        Instance.BeginChapter(r, onDone);
    }

    public static bool HasWorld(string id) {
        return Load(id) != null;
    }

    public static Reel Load(string id) {
        TextAsset asset = Resources.Load<TextAsset>("Cinematics/" + id);
        if (asset == null) {
            return null;
        }
        Reel r = JsonUtility.FromJson<Reel>(asset.text);
        if (r == null || r.shots == null) {
            return null;
        }
        foreach (Shot s in r.shots) {
            if (s.seconds <= 0f) {
                s.seconds = 4f;
            }
            if (string.IsNullOrEmpty(s.speaker)) {
                s.speaker = "N";
            }
            if (s.fov <= 1f) {
                s.fov = 55f;
            }
        }
        return r;
    }

    void Begin(Reel r, LevelYard host, Action onDone) {
        if (open) {
            End(true);
        }
        yard = host;
        chapterHost = false;
        circle = LevelYard.RingCenter;
        chapterBearing = 0f;
        reel = r;
        done = onDone;
        shotIndex = -1;
        open = true;
        handingOff = false;
        skipRevealed = false;
        viewSet = false;
        Events.Add("open|" + r.id);
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();

        LevelScene level = LevelScene.Instance;
        if (level != null) {
            wallit = level.Player;
            BindWallit();
            cam = level.Cam;
            if (cam != null) {
                zelda = cam.GetComponent<ZeldaStyleCamera>();
                savedFov = cam.fieldOfView;
            }
        }
        sliceCompanion = null;
        slice = host != null ? host.SliceBody : null;
        BindSliceAnim();

        if (!ActorsReady(r)) {
            FallbackStill(r);
            return;
        }

        EnsureUi();
        uiRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        NextShot();
    }

    void BeginChapter(Reel r, Action onDone) {
        if (open) {
            End(true);
        }
        yard = null;
        chapterHost = true;
        circle = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        chapterBearing = RouteBearing();
        reel = r;
        done = onDone;
        shotIndex = -1;
        open = true;
        handingOff = false;
        skipRevealed = false;
        viewSet = false;
        Events.Add("open|" + r.id);
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();

        PlayerGrowth pg = PlayerGrowth.Instance;
        wallit = pg != null ? pg.transform : null;
        BindWallit();
        Camera main = Camera.main;
        if (main != null) {
            cam = main;
            zelda = main.GetComponent<ZeldaStyleCamera>();
            savedFov = main.fieldOfView;
        }
        // Chapter 2: Slice walks with Wallit (Companion). Chapter 3: she holds the ring (Musician figure).
        sliceCompanion = Companion.Instance;
        if (sliceCompanion != null) {
            slice = sliceCompanion.transform;
            sliceCompanion.Held = true;
        } else if (Musician.Instance != null) {
            slice = Musician.Instance.transform;
        } else {
            slice = null;
        }
        BindSliceAnim();

        if (!ActorsReady(r)) {
            if (sliceCompanion != null) {
                sliceCompanion.Held = false;
            }
            FallbackStill(r);
            return;
        }

        EnsureUi();
        uiRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        NextShot();
    }

    void BindWallit() {
        if (wallit == null) {
            return;
        }
        wallitBody = wallit.GetComponent<CharacterController>();
        wallitMotor = wallit.GetComponent<MovementMotor>();
        if (wallitMotor != null) {
            wallitMotor.Stop();
        }
    }

    void BindSliceAnim() {
        sliceAnim = null;
        if (slice == null) {
            return;
        }
        sliceAnim = slice.GetComponent<Animator>();
        if (sliceAnim == null) {
            sliceAnim = slice.GetComponentInChildren<Animator>();
        }
    }

    void FallbackStill(Reel r) {
        Events.Add("fallback|" + (r.fallback ?? "ch1_open"));
        open = false;
        VoiceDirector.Scene = false;
        string fb = string.IsNullOrEmpty(r.fallback) ? "ch1_open" : r.fallback;
        Cutscene.PlayShots(fb, () => {
            done?.Invoke();
            done = null;
        });
    }

    bool ActorsReady(Reel r) {
        if (wallit == null || slice == null) {
            return false;
        }
        bool needCourier = false;
        bool needYobot = false;
        bool needSpider = false;
        bool needMega = false;
        foreach (Shot s in r.shots) {
            if (s.actors == null) {
                continue;
            }
            foreach (Mark m in s.actors) {
                if (m == null) {
                    continue;
                }
                if (m.id == "courier") {
                    needCourier = true;
                }
                if (m.id == "yobot") {
                    needYobot = true;
                }
                if (m.id == "spider" || m.id == "server") {
                    needSpider = true;
                }
                if (m.id == "mega") {
                    needMega = true;
                }
            }
        }
        if (needCourier) {
            EnsureCourier();
            if (courier == null) {
                return false;
            }
        }
        if (needYobot) {
            EnsureYobot();
            if (yobot == null) {
                return false;
            }
        }
        if (needSpider) {
            EnsureSpider();
            if (spider == null) {
                return false;
            }
        }
        if (needMega) {
            EnsureMega();
            if (mega == null) {
                return false;
            }
        }
        return true;
    }

    void EnsureCourier() {
        if (courier != null) {
            return;
        }
        BotDef def = BotRoster.ForSpawn(BotKind.YoBot, 1);
        var go = new GameObject("Cinematic Courier");
        go.tag = "enemy";
        Vector3 p = Seat(18f, -4f);
        p.y += 0.55f + def.Scale * 0.08f;
        go.transform.position = p;
        go.transform.rotation = Quaternion.LookRotation(Vector3.left);
        Enemy e = go.AddComponent<Enemy>();
        e.ApplyBot(def);
        e.MakeCourier("The Courier", LevelYard.CourierHits);
        e.Dormant = true;
        e.Scripted = true;
        courier = e;
        courierOwned = true;
    }

    void EnsureYobot() {
        if (yobot != null) {
            return;
        }
        BotDef def = BotRoster.ForSpawn(BotKind.YoBot, 1);
        var go = new GameObject("Cinematic Half-Built");
        go.tag = "enemy";
        Vector3 p = PolarPoint(chapterBearing > 1f ? chapterBearing : 200f, 36f, 0f);
        // Hang at hook height on the gantry (landmark), not on the floor. Override with Mark.lift when standing.
        p.y = Ground(p.x, p.z) + 2.4f;
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, chapterBearing + 180f, 0f);
        Enemy e = go.AddComponent<Enemy>();
        e.ApplyBot(def);
        e.Dormant = true;
        e.Scripted = true;
        yobot = e;
        yobotOwned = true;
    }

    void EnsureSpider() {
        if (spider != null) {
            return;
        }
        BotDef def = BotRoster.ForSpawn(BotKind.SpiderBot, 1);
        var go = new GameObject("Cinematic Server");
        go.tag = "enemy";
        Vector3 p = PolarPoint(chapterBearing > 1f ? chapterBearing : 330f, 18f, 0f);
        p.y = Ground(p.x, p.z) + 0.55f;
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, chapterBearing + 180f, 0f);
        Enemy e = go.AddComponent<Enemy>();
        e.ApplyBot(def);
        e.MakeServer("The Server", 40);
        e.Dormant = true;
        e.Scripted = true;
        spider = e;
        spiderOwned = true;
    }

    void EnsureMega() {
        if (mega != null) {
            return;
        }
        BotDef def = BotRoster.ForSpawn(BotKind.MegaYoBot, 1);
        var go = new GameObject("Cinematic Mega");
        go.tag = "enemy";
        Vector3 p = PolarPoint(chapterBearing > 1f ? chapterBearing : 0f, 10f, 0f);
        p.y = Ground(p.x, p.z) + 0.55f;
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, chapterBearing + 180f, 0f);
        Enemy e = go.AddComponent<Enemy>();
        e.ApplyBot(def);
        e.Dormant = true;
        e.Scripted = true;
        mega = e;
        megaOwned = true;
    }

    void NextShot() {
        shotIndex++;
        if (reel == null || reel.shots == null || shotIndex >= reel.shots.Length) {
            End(false);
            return;
        }
        Shot s = reel.shots[shotIndex];
        shotStart = Time.unscaledTime;
        shotLength = Mathf.Max(0.4f, s.seconds);
        shotSpoke = false;
        shotSfx = false;
        shotStamp = false;
        shotSlam = false;
        shotPostmark = false;
        shotBelt = false;
        shotPaper = false;
        shotAddressStamp = false;
        shotDoors = false;
        shotKeepsakes = false;
        shotCredits = false;
        ShotsShown++;
        Events.Add("shot|" + s.id + "|" + shotIndex);
        PlaceActors(s, 0f);
        ApplyCamera(s, 0f);
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        if (caption != null) {
            caption.text = "";
        }
        if (namePlate != null) {
            namePlate.text = "";
        }
        if (s.finaleCarry) {
            BeginFinaleCarry(s);
        }
    }

    void Update() {
        TickPendingBoss();
        if (bossRun) {
            TickBoss();
            return;
        }
        if (actRun) {
            TickAct();
            return;
        }
        if (creditsOnly) {
            TickCreditsOnly();
            return;
        }
        if (!open) {
            return;
        }
        if (PauseMenu.IsOpen) {
            return;
        }
        float now = Time.unscaledTime;
        Shot s = reel.shots[shotIndex];
        if (FinaleCarryActive) {
            TickFinaleCarry();
            EaseBars(true);
            LayoutUi();
            return;
        }
        float u = Mathf.Clamp01((now - shotStart) / shotLength);
        PlaceActors(s, Ease(u));
        ApplyCamera(s, Ease(u));
        TickVoice(s, u);
        TickSfx(s, u);
        TickStamp(s, u);
        TickPostmark(s, u);
        TickBelt(s, u);
        TickPaper(s, u);
        TickAddressStamp(s, u);
        TickDoors(s, u);
        TickKeepsakes(s, u);
        TickCreditsShot(s, u);
        TickSkipInput();
        EaseBars(true);
        LayoutUi();
        // A shot never outlasts its own time plus the longest line it may wait for, whatever a voice clip reports.
        if (now - shotStart >= shotLength || now - shotStart >= Mathf.Max(0.4f, s.seconds) + MaxVoiceSeconds + 0.5f) {
            NextShot();
        }
    }

    void LateUpdate() {
        if ((bossRun || actRun) && cam != null && viewSet) {
            cam.transform.position = viewPos;
            cam.transform.rotation = viewRot;
            if (zelda != null) {
                zelda.CinematicHold(viewPos, viewRot);
            }
            return;
        }
        if ((!open && !creditsOnly) || !viewSet || cam == null) {
            return;
        }
        cam.transform.position = viewPos;
        cam.transform.rotation = viewRot;
        if (zelda != null) {
            zelda.CinematicHold(viewPos, viewRot);
        }
    }

    void TickVoice(Shot s, float u) {
        if (shotSpoke || string.IsNullOrEmpty(s.line) || u < 0.03f) {
            return;
        }
        shotSpoke = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        string who = string.IsNullOrEmpty(s.speaker) ? "N" : s.speaker;
        SubtitleSpeaker = who;
        if (namePlate != null) {
            namePlate.text = who == "W" ? "Wallit" : who == "S" ? "Slice" : who == "K" ? "The Courier" : who == "Y" ? "Yo Bot" : who == "M" ? "Mega Yo Bot" : who == "V" ? "The Server" : "";
            namePlate.color = who == "W" ? new Color(0.95f, 0.82f, 0.4f) : who == "S" ? new Color(0.75f, 0.7f, 0.95f) : DeptPalette.Cream;
        }
        if (caption != null) {
            caption.text = s.line;
        }
        float len;
        if (who == "W") {
            len = WallitVoice.Cutscene(s.line);
        } else if (who == "S") {
            len = SliceVoice.Instance != null ? SliceVoice.Cutscene(s.line) : CastVoice.SayNow("S", s.line, "cutscene");
        } else if (who == "Y" || who == "K" || who == "M" || who == "V") {
            // Courier (K), Yo Bot (Y), Mega (M) and Server (V) use cast voices; prefer the shot's code.
            len = CastVoice.SayNow(who, s.line, "cutscene");
        } else {
            len = CastVoice.SayNow("N", s.line, "cutscene");
        }
        VoiceLines++;
        Events.Add("voice|" + who + "|" + s.line);
        // A clip that never decoded can report a nonsense length: the shot keeps its own time (or the line's estimate).
        if (float.IsNaN(len) || float.IsInfinity(len) || len < 0f) {
            len = WallitVoice.EstimateSeconds(s.line);
        }
        len = Mathf.Min(len, MaxVoiceSeconds);
        if (len > shotLength - (Time.unscaledTime - shotStart)) {
            shotLength = (Time.unscaledTime - shotStart) + len + 0.05f;
        }
    }

    void TickSfx(Shot s, float u) {
        if (shotSfx || string.IsNullOrEmpty(s.sfx) || u < 0.12f) {
            return;
        }
        shotSfx = true;
        GameAudio.Play(s.sfx, 0.85f);
        Events.Add("sfx|" + s.sfx);
    }

    void TickStamp(Shot s, float u) {
        if (yard == null) {
            return;
        }
        if (!shotStamp && s.stamp != null && s.stamp.Length >= 2 && u >= 0.05f) {
            shotStamp = true;
            Vector3 at = Vec(s.stamp);
            yard.DropStampAt(at, "cinematic:" + s.id);
            Events.Add("stamp|" + s.id);
        }
        if (!shotSlam && s.slam && u >= 0.35f) {
            shotSlam = true;
            Events.Add("slam|" + s.id);
            // The stamp lands on Wallit: flinch and a short shove so the beat is felt, not only captioned.
            if (wallit != null) {
                CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HIT_REACT, 0.55f);
                if (LevelScene.Instance != null) {
                    Vector3 away = wallit.position - LevelYard.RingCenter;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) {
                        away = -wallit.forward;
                    }
                    LevelScene.Instance.Shove(away.normalized, 1.6f);
                }
            }
        }
    }

    void TickPostmark(Shot s, float u) {
        if (!s.postmark || shotPostmark || u < 0.08f) {
            return;
        }
        shotPostmark = true;
        Vector3 at = GatePoint();
        DropPostmarkAt(at);
        GameAudio.Play("stamp", 0.95f, 0.75f);
        Fx.Ring(at + Vector3.up * 0.4f, 8f, new Color(0.9f, 0.25f, 0.2f, 0.7f), 0.55f);
        Events.Add("postmark|" + s.id);
    }

    void TickBelt(Shot s, float u) {
        if (!s.belt || shotBelt || u < 0.1f) {
            return;
        }
        shotBelt = true;
        FloorMap floor = ChapterMap.Current != null ? ChapterMap.Current.Own as FloorMap : null;
        int moving = 0;
        if (floor != null) {
            moving = floor.PulseBelts(1.8f);
        }
        GameAudio.Play("paper_slide", 0.7f, 0.85f);
        Events.Add("belt|" + s.id + "|items=" + moving);
    }

    void DropPostmarkAt(Vector3 gate) {
        if (postmarkGo != null) {
            Destroy(postmarkGo);
        }
        float yaw = chapterBearing > 1f ? chapterBearing + 180f : 330f;
        // Face of the gate (toward the circle): a torn postmark pressed into the stone, not a floor decal.
        Vector3 face = gate + Quaternion.Euler(0f, chapterBearing, 0f) * new Vector3(0f, 1.7f, -0.35f);
        postmarkGo = new GameObject("Cinematic Postmark");
        postmarkGo.transform.position = face;
        postmarkGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        // Decal is XZ by default; stand it on the gate face (parent faces the circle).
        GameObject ink = LevelKit.Decal(postmarkGo.transform, "Ink", new Vector3(0f, 0f, 0.02f), new Vector2(2.4f, 1.8f), new Color(0.76f, 0.23f, 0.23f, 0.55f));
        ink.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Renderer r = ink.GetComponent<Renderer>();
        if (r != null) {
            r.material = new Material(r.sharedMaterial);
        }
        // Upright on the gate face, readable from the circle (never LookAt / never negative scale).
        TextMesh words = LevelKit.Text(postmarkGo.transform, "RETURN\nTO SENDER", new Vector3(0f, 0f, 0.05f), 0.055f, new Color(1f, 0.85f, 0.85f, 0.95f), false);
        words.name = "Words";
        // The parent faces the circle, so its +Z points at the reader; TextMesh reads only along its own +Z, so turn the words round.
        words.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        GameObject head = LevelDress.Put(postmarkGo.transform, "gt_press_head", face + Vector3.up * 6f, yaw, 0.45f);
        if (head != null) {
            head.name = "PostmarkHead";
            postmarkHead = head.transform;
            StartCoroutine(SlamPostmarkHead(face + postmarkGo.transform.forward * 0.15f));
        }
        if (wallit != null) {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HIT_REACT, 0.4f);
        }
    }

    System.Collections.IEnumerator SlamPostmarkHead(Vector3 land) {
        if (postmarkHead == null) {
            yield break;
        }
        Vector3 from = postmarkHead.position;
        float t0 = Time.unscaledTime;
        while (postmarkHead != null && Time.unscaledTime - t0 < 0.45f) {
            float k = Mathf.Clamp01((Time.unscaledTime - t0) / 0.45f);
            float e = k * k;
            postmarkHead.position = Vector3.Lerp(from, land, e);
            yield return null;
        }
        if (postmarkHead != null) {
            postmarkHead.position = land;
        }
    }

    Vector3 GatePoint() {
        EdgeMap edge = ChapterMap.Current != null ? ChapterMap.Current.Own as EdgeMap : null;
        if (edge != null) {
            return edge.GatePos;
        }
        float b = chapterBearing > 1f ? chapterBearing : 150f;
        float dist = ChapterRun.Instance != null ? Story.Chapters[ChapterRun.Instance.Chapter].Target + 4f : 74f;
        return PolarPoint(b, dist, 0f);
    }

    void TickSkipInput() {
        bool touch = UiFit.Touch;
        if (touch) {
            // The pill is up from the first frame (bottom right, inside the safe area); a tap anywhere skips after 1 s.
            if (!skipRevealed) {
                skipRevealed = true;
                if (skipPill != null) {
                    skipPill.SetActive(true);
                }
                Events.Add("skip-pill");
                touchDownAt = Time.unscaledTime;
                return;
            }
            if ((PlayInput.GetMouseButtonDown(0) || Input.touchCount > 0) && Time.unscaledTime - touchDownAt > 1f) {
                Skip();
            }
            if (PlayInput.GetKeyDown(KeyCode.Escape)) {
                Skip();
            }
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter)
            || PlayInput.GetMouseButtonDown(0)) {
            Skip();
        }
    }

    public void Skip() {
        if (actRun) {
            EndAct(true);
            return;
        }
        if (bossRun) {
            BossSkips++;
            BossEvents.Add("boss-skip|" + LastBossTitle);
            EndBoss(true);
            return;
        }
        if (creditsOnly) {
            EndCreditsLive(true);
            return;
        }
        if (!open) {
            return;
        }
        // Esc / Enter during the carry completes the stamp instead of dumping the whole ending.
        if (FinaleCarryActive) {
            Events.Add("finale-carry-skip");
            FinishFinaleCarry(true);
            return;
        }
        Skipped++;
        Events.Add("skip|" + ShotId);
        End(true);
    }

    void End(bool skipped) {
        if (!open) {
            return;
        }
        open = false;
        handingOff = true;
        float handoffStart = Time.unscaledTime;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        VoiceDirector.Scene = false;
        EaseBars(false);
        if (uiRoot != null) {
            uiRoot.SetActive(false);
        }
        if (courierOwned && courier != null) {
            if (courier.gameObject != null) {
                Destroy(courier.gameObject);
            }
            courier = null;
            courierOwned = false;
        } else if (courier != null) {
            courier.Scripted = false;
            courier.Dormant = true;
            courier = null;
        }
        if (yobotOwned && yobot != null) {
            if (yobot.gameObject != null) {
                Destroy(yobot.gameObject);
            }
            yobot = null;
            yobotOwned = false;
        } else if (yobot != null) {
            yobot.Scripted = false;
            yobot.Dormant = true;
            yobot = null;
        }
        if (spiderOwned && spider != null) {
            if (spider.gameObject != null) {
                Destroy(spider.gameObject);
            }
            spider = null;
            spiderOwned = false;
        } else if (spider != null) {
            spider.Scripted = false;
            spider.Dormant = true;
            spider = null;
        }
        if (megaOwned && mega != null) {
            if (mega.gameObject != null) {
                Destroy(mega.gameObject);
            }
            mega = null;
            megaOwned = false;
        } else if (mega != null) {
            mega.Scripted = false;
            mega.Dormant = true;
            mega = null;
        }
        // RECEIVED leaves its six doors up under the live credits; openings clear every prop.
        ClearEndingProps(DoorsShown > 0);
        if (sliceCompanion != null) {
            sliceCompanion.Held = false;
            sliceCompanion = null;
        }
        FloorMap floorClear = ChapterMap.Current != null ? ChapterMap.Current.Own as FloorMap : null;
        if (floorClear != null) {
            floorClear.ClearBeltBoost();
        }
        if (postmarkGo != null) {
            Destroy(postmarkGo);
            postmarkGo = null;
            postmarkHead = null;
        }
        // Seat Wallit at the play start and face the Ring so control begins where the yard expects.
        // Keep the last directed camera pose held until BeginHandoff so the blend starts from that shot.
        if (yard != null && wallit != null) {
            Vector3 start = yard.PlayerStart;
            start.y = LevelYard.H(start.x, start.z) + 0.2f;
            if (wallitBody != null) {
                wallitBody.enabled = false;
            }
            wallit.position = start;
            wallit.rotation = Quaternion.LookRotation(yard.PlayerFacing);
            if (wallitBody != null) {
                wallitBody.enabled = true;
            }
            if (wallitMotor != null) {
                wallitMotor.Stop();
            }
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
        } else if (chapterHost && wallit != null) {
            // Stay where the last shot left him (near the circle); face the route. No teleport pop.
            Vector3 face = PolarPoint(chapterBearing > 1f ? chapterBearing : 0f, 12f, 0f) - wallit.position;
            face.y = 0f;
            if (face.sqrMagnitude < 0.01f) {
                face = Quaternion.Euler(0f, chapterBearing, 0f) * Vector3.forward;
            }
            wallit.rotation = Quaternion.LookRotation(face.normalized);
            if (wallitMotor != null) {
                wallitMotor.Stop();
            }
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
        }
        if (yard != null && slice != null) {
            Vector3 sp = LevelYard.SlicePos;
            sp.y = LevelYard.H(sp.x, sp.z);
            slice.position = sp;
            slice.rotation = Quaternion.LookRotation(Vector3.back);
        } else if (chapterHost && slice != null && Companion.Instance == null) {
            // Ring Slice stays at the circle; companion Slice is left where the last shot put her (she walks with Wallit).
            Vector3 sp = circle;
            sp.y = Ground(sp.x, sp.z);
            slice.position = sp;
            slice.rotation = Quaternion.LookRotation(Vector3.back);
        }
        if (cam != null) {
            cam.fieldOfView = savedFov;
        }
        if (zelda != null) {
            // Blend from the live cinematic view onto the follow camera (do not SnapBehind first).
            zelda.BeginHandoff(0.6f);
            StartCoroutine(FinishHandoff(handoffStart, skipped, done));
            done = null;
            reel = null;
            shotIndex = -1;
            return;
        }
        handingOff = false;
        LastHandoffSeconds = Time.unscaledTime - handoffStart;
        Events.Add(skipped ? "skipped" : "done");
        Events.Add("handoff|" + LastHandoffSeconds.ToString("0.000"));
        if (!skipped) {
            Finished++;
        }
        Action cb = done;
        done = null;
        reel = null;
        shotIndex = -1;
        cb?.Invoke();
    }

    System.Collections.IEnumerator FinishHandoff(float handoffStart, bool skipped, Action cb) {
        while (zelda != null && zelda.HandoffBusy) {
            yield return null;
        }
        handingOff = false;
        LastHandoffSeconds = Time.unscaledTime - handoffStart;
        Events.Add(skipped ? "skipped" : "done");
        Events.Add("handoff|" + LastHandoffSeconds.ToString("0.000"));
        if (!skipped) {
            Finished++;
        }
        cb?.Invoke();
    }

    // ---------------------------------------------------------------- actors and camera

    void PlaceActors(Shot s, float u) {
        if (s.actors == null) {
            return;
        }
        foreach (Mark m in s.actors) {
            if (m == null || string.IsNullOrEmpty(m.id)) {
                continue;
            }
            Transform t = ActorOf(m.id);
            if (t == null) {
                continue;
            }
            Vector3 pos;
            if (!TryMarkPos(m, u, out pos)) {
                continue;
            }
            if (m.id == "wallit") {
                pos.y = Ground(pos.x, pos.z) + 0.2f;
                MoveWallit(pos, m, u);
            } else if (m.id == "courier" && courier != null) {
                pos.y = Ground(pos.x, pos.z) + 0.55f;
                courier.transform.position = pos;
                courier.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                if (m.tell || m.anim == "tell") {
                    courier.BeginTell();
                } else if (m.anim == "walk" && u >= 0.98f) {
                    courier.EndTell(false);
                }
            } else if (m.id == "yobot" && yobot != null) {
                // 0 (JSON default) keeps the Chapter 3 hook hang; a positive lift seats it on the ground (ch4/ch6).
                float lift = m.lift > 0.01f ? m.lift : 2.4f;
                pos.y = Ground(pos.x, pos.z) + lift;
                yobot.transform.position = pos;
                yobot.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
            } else if ((m.id == "spider" || m.id == "server") && spider != null) {
                float lift = m.lift > 0.01f ? m.lift : 0.55f;
                pos.y = Ground(pos.x, pos.z) + lift;
                spider.transform.position = pos;
                spider.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                if (m.tell || m.anim == "tell") {
                    spider.BeginTell();
                }
            } else if (m.id == "mega" && mega != null) {
                float lift = m.lift > 0.01f ? m.lift : 0.55f;
                pos.y = Ground(pos.x, pos.z) + lift;
                mega.transform.position = pos;
                mega.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
            } else {
                pos.y = Ground(pos.x, pos.z);
                t.position = pos;
                t.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                if (m.id == "slice") {
                    if (sliceCompanion != null) {
                        sliceCompanion.HeldMove(pos, m.anim == "walk");
                        if (m.anim != "walk") {
                            sliceCompanion.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                        }
                    } else if (sliceAnim != null && (m.anim == "turn" || m.anim == "idle")) {
                        int idle = Animator.StringToHash("idle");
                        if (sliceAnim.HasState(0, idle) && !sliceAnim.GetCurrentAnimatorStateInfo(0).IsName("idle")) {
                            sliceAnim.CrossFadeInFixedTime("idle", 0.15f, 0, 0f);
                        }
                    }
                }
            }
        }
    }

    bool TryMarkPos(Mark m, float u, out Vector3 pos) {
        if (m.fromPolar != null && m.fromPolar.Length >= 2 && m.toPolar != null && m.toPolar.Length >= 2) {
            pos = Vector3.Lerp(PolarMark(m.fromPolar), PolarMark(m.toPolar), u);
            return true;
        }
        if (m.atPolar != null && m.atPolar.Length >= 2) {
            pos = PolarMark(m.atPolar);
            return true;
        }
        if (m.from != null && m.from.Length >= 2 && m.to != null && m.to.Length >= 2) {
            pos = Vector3.Lerp(SeatXZ(m.from), SeatXZ(m.to), u);
            return true;
        }
        if (m.at != null && m.at.Length >= 2) {
            pos = SeatXZ(m.at);
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    void MoveWallit(Vector3 pos, Mark m, float u) {
        if (wallit == null) {
            return;
        }
        Vector3 flat = pos - wallit.position;
        flat.y = 0f;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float speed = flat.magnitude / dt;
        bool walking = m.anim == "walk" && flat.sqrMagnitude > 0.0004f;
        if (wallitBody != null && wallitBody.enabled) {
            wallitBody.Move(pos - wallit.position);
        } else {
            wallit.position = pos;
        }
        // Face the direction of travel when walking so return legs do not moonwalk.
        if (walking && flat.sqrMagnitude > 0.01f) {
            wallit.rotation = Quaternion.LookRotation(flat.normalized);
        } else {
            wallit.rotation = Quaternion.Euler(0f, m.yaw, 0f);
        }
        if (walking) {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_RUN);
            if (wallitMotor != null) {
                wallitMotor.Move(flat.normalized * Mathf.Clamp(speed, 1.5f, 5.5f));
            }
        } else {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
            if (wallitMotor != null) {
                wallitMotor.Stop();
            }
        }
    }

    static float RouteBearing() {
        if (ChapterRun.Instance == null) {
            return 0f;
        }
        Story.Chapter ch = Story.Chapters[ChapterRun.Instance.Chapter];
        if (ch.Bearing > 1f) {
            return ch.Bearing;
        }
        if (ch.Stops != null && ch.Stops.Length > 0) {
            return ch.Stops[0].Bearing;
        }
        return 0f;
    }

    Transform ActorOf(string id) {
        if (id == "wallit") {
            return wallit;
        }
        if (id == "slice") {
            return slice;
        }
        if (id == "courier") {
            return courier != null ? courier.transform : null;
        }
        if (id == "yobot") {
            return yobot != null ? yobot.transform : null;
        }
        if (id == "spider" || id == "server") {
            return spider != null ? spider.transform : null;
        }
        if (id == "mega") {
            return mega != null ? mega.transform : null;
        }
        return null;
    }

    void ApplyCamera(Shot s, float u) {
        if (cam == null) {
            return;
        }
        Vector3 from = CamPoint(s.camFromPolar, s.camFrom, cam.transform.position);
        Vector3 to = CamPoint(s.camToPolar, s.camTo, from);
        Vector3 look = CamLook(s.lookPolar, s.lookAt, s);
        float k = s.camMode == "cut" ? 1f : Ease(u);
        if (s.camMode == "orbit") {
            Vector3 pivot = look;
            float ang = Mathf.Lerp(-12f, 8f, k);
            Vector3 offset = Quaternion.Euler(0f, ang, 0f) * (Vector3.Lerp(from, to, 0.5f) - pivot);
            viewPos = pivot + offset;
        } else {
            viewPos = Vector3.Lerp(from, to, k);
        }
        float fov = s.fov > 1f ? s.fov : 55f;
        if (Phone()) {
            fov = Mathf.Clamp(fov + 10f, 50f, 72f);
            viewPos = PullBackForFrame(viewPos, look, s.frame, fov);
        }
        cam.fieldOfView = fov;
        Vector3 dir = look - viewPos;
        if (dir.sqrMagnitude < 0.01f) {
            dir = Vector3.forward;
        }
        viewRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        viewSet = true;
    }

    Vector3 PullBackForFrame(Vector3 pos, Vector3 look, string[] frame, float fov) {
        if (frame == null || frame.Length == 0 || cam == null) {
            return pos + (pos - look).normalized * 1.8f;
        }
        // Pull back until every named actor is inside a portrait-safe frustum (both speakers in shot).
        Vector3 pull = (pos - look);
        if (pull.sqrMagnitude < 0.01f) {
            pull = Vector3.back * 6f;
        }
        pull = pull.normalized;
        float dist = Mathf.Max(3f, Vector3.Distance(pos, look));
        float savedFovNow = cam.fieldOfView;
        Vector3 savedPos = cam.transform.position;
        Quaternion savedRot = cam.transform.rotation;
        for (int step = 0; step < 8; step++) {
            bool ok = true;
            Vector3 tryPos = look + pull * dist;
            Vector3 dir = look - tryPos;
            if (dir.sqrMagnitude < 0.01f) {
                dir = Vector3.forward;
            }
            cam.fieldOfView = fov;
            cam.transform.position = tryPos;
            cam.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            foreach (string id in frame) {
                Transform t = ActorOf(id);
                if (t == null) {
                    continue;
                }
                Vector3 vp = cam.WorldToViewportPoint(t.position + Vector3.up * 1.2f);
                // Tall phone: keep them in the middle band so captions and bars don't cover faces.
                if (vp.z <= 0.2f || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.28f || vp.y > 0.88f) {
                    ok = false;
                    break;
                }
            }
            if (ok) {
                cam.fieldOfView = savedFovNow;
                cam.transform.position = savedPos;
                cam.transform.rotation = savedRot;
                return tryPos;
            }
            dist += 1.4f;
        }
        cam.fieldOfView = savedFovNow;
        cam.transform.position = savedPos;
        cam.transform.rotation = savedRot;
        return look + pull * dist;
    }

    Vector3 lookFallback(Shot s) {
        if (s.frame != null && s.frame.Length > 0) {
            Transform t = ActorOf(s.frame[0]);
            if (t != null) {
                return t.position + Vector3.up * 1.4f;
            }
        }
        return circle + Vector3.up * 1.4f;
    }

    static bool Phone() {
        return UiFit.Touch && (UiFit.Mode == UiMode.Tall || Screen.height >= Screen.width);
    }

    static float Ease(float t) {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static Vector3 Vec(float[] a) {
        if (a == null || a.Length < 2) {
            return Vector3.zero;
        }
        if (a.Length >= 3) {
            return new Vector3(a[0], a[1], a[2]);
        }
        return new Vector3(a[0], 0f, a[1]);
    }

    Vector3 SeatXZ(float[] a) {
        Vector3 v = Vec(a);
        v.y = Ground(v.x, v.z);
        return v;
    }

    Vector3 Seat(float x, float z) {
        return new Vector3(x, Ground(x, z), z);
    }

    float Ground(float x, float z) {
        if (yard != null) {
            return LevelYard.H(x, z);
        }
        if (ChapterMap.Current != null && ChapterMap.Current.Own != null) {
            return ChapterMap.Current.Own.HeightAt(x, z);
        }
        return LevelDress.GroundY(x, z, circle.y);
    }

    Vector3 PolarPoint(float bearingDeg, float along, float side) {
        float a = bearingDeg * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 p = circle + fwd * along + right * side;
        p.y = Ground(p.x, p.z);
        return p;
    }

    Vector3 PolarMark(float[] polar) {
        float b = polar.Length > 0 ? polar[0] : chapterBearing;
        float along = polar.Length > 1 ? polar[1] : 0f;
        float side = polar.Length > 2 ? polar[2] : 0f;
        return PolarPoint(b, along, side);
    }

    Vector3 CamPoint(float[] polar, float[] abs, Vector3 fallback) {
        if (polar != null && polar.Length >= 3) {
            Vector3 p = PolarPoint(polar[0], polar[1], polar[2]);
            p.y = polar.Length >= 4 ? polar[3] : Ground(p.x, p.z) + 3f;
            return p;
        }
        if (abs != null && abs.Length >= 3) {
            return Vec(abs);
        }
        return fallback;
    }

    Vector3 CamLook(float[] polar, float[] abs, Shot s) {
        if (polar != null && polar.Length >= 3) {
            Vector3 p = PolarPoint(polar[0], polar[1], polar[2]);
            p.y = polar.Length >= 4 ? polar[3] : Ground(p.x, p.z) + 1.4f;
            return p;
        }
        if (abs != null && abs.Length >= 3) {
            return Vec(abs);
        }
        return lookFallback(s);
    }

    // ---------------------------------------------------------------- UI

    void EnsureUi() {
        if (uiRoot != null) {
            return;
        }
        font = UiSkin.Font(UiSkin.FontRole.Body);
        uiRoot = new GameObject("Cinematic UI");
        DontDestroyOnLoad(uiRoot);
        var canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 420;
        scaler = uiRoot.AddComponent<CanvasScaler>();
        uiRoot.AddComponent<GraphicRaycaster>();
        UiFit.Apply(scaler);

        topBar = Bar("Top Bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        bottomBar = Bar("Bottom Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(uiRoot.transform, false);
        namePlate = nameGo.AddComponent<Text>();
        namePlate.font = UiSkin.Font(UiSkin.FontRole.Label);
        namePlate.fontSize = 26;
        namePlate.alignment = TextAnchor.LowerLeft;
        namePlate.color = new Color(0.95f, 0.85f, 0.55f);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.08f, 0.16f);
        nameRt.anchorMax = new Vector2(0.5f, 0.22f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;

        var capGo = new GameObject("Caption");
        capGo.transform.SetParent(uiRoot.transform, false);
        caption = capGo.AddComponent<Text>();
        caption.font = font;
        caption.fontSize = 28;
        caption.alignment = TextAnchor.UpperLeft;
        caption.color = Color.white;
        caption.horizontalOverflow = HorizontalWrapMode.Wrap;
        caption.verticalOverflow = VerticalWrapMode.Overflow;
        captionRt = capGo.GetComponent<RectTransform>();

        skipPill = new GameObject("Skip Pill");
        skipPill.transform.SetParent(uiRoot.transform, false);
        var img = skipPill.AddComponent<Image>();
        img.color = new Color(0.08f, 0.1f, 0.16f, 0.82f);
        skipButton = skipPill.AddComponent<Button>();
        skipButton.onClick.AddListener(Skip);
        MenuButtonFx.Add(skipButton);
        var skipRt = skipPill.GetComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(1f, 1f);
        skipRt.anchorMax = new Vector2(1f, 1f);
        skipRt.pivot = new Vector2(1f, 1f);
        skipRt.sizeDelta = new Vector2(160f, 64f);
        skipRt.anchoredPosition = new Vector2(-24f, -24f);
        var skipLabel = new GameObject("Label");
        skipLabel.transform.SetParent(skipPill.transform, false);
        var st = skipLabel.AddComponent<Text>();
        st.font = UiSkin.Font(UiSkin.FontRole.Label);
        st.fontSize = 28;
        st.alignment = TextAnchor.MiddleCenter;
        st.color = Color.white;
        st.text = "Skip";
        var stRt = skipLabel.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.offsetMin = Vector2.zero;
        stRt.offsetMax = Vector2.zero;
        skipPill.SetActive(false);
        uiRoot.SetActive(false);
    }

    RectTransform Bar(string name, Vector2 amin, Vector2 amax) {
        var go = new GameObject(name);
        go.transform.SetParent(uiRoot.transform, false);
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = amin;
        return rt;
    }

    void EaseBars(bool on) {
        float target = on ? 1f : 0f;
        barsNow = Mathf.MoveTowards(barsNow, target, Time.unscaledDeltaTime * 2.4f);
        float h = Phone() ? 0.05f : 0.11f;
        float bar = h * barsNow * (scaler != null ? scaler.referenceResolution.y : 1080f);
        if (topBar != null) {
            topBar.sizeDelta = new Vector2(scaler != null ? scaler.referenceResolution.x : 1920f, bar);
        }
        if (bottomBar != null) {
            bottomBar.sizeDelta = new Vector2(scaler != null ? scaler.referenceResolution.x : 1920f, bar);
        }
    }

    void LayoutUi() {
        if (scaler == null) {
            return;
        }
        if (uiStamp != UiFit.Stamp) {
            uiStamp = UiFit.Stamp;
            UiFit.Apply(scaler);
        }
        // Captions sit above the bottom 15% on a phone (acceptance); desktop keeps them just above the letterbox.
        float bottom = Phone() ? 0.165f : 0.12f;
        float top = Phone() ? 0.30f : 0.28f;
        CaptionBottom01 = bottom;
        if (captionRt != null) {
            captionRt.anchorMin = new Vector2(0.08f, bottom);
            captionRt.anchorMax = new Vector2(0.92f, top);
            captionRt.offsetMin = Vector2.zero;
            captionRt.offsetMax = Vector2.zero;
        }
        if (namePlate != null) {
            var rt = namePlate.rectTransform;
            rt.anchorMin = new Vector2(0.08f, top);
            rt.anchorMax = new Vector2(0.5f, top + 0.05f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        if (skipPill != null && skipRevealed) {
            var safe = Screen.safeArea;
            float scale = scaler.referenceResolution.y / Mathf.Max(1f, Screen.height);
            var rt = skipPill.GetComponent<RectTransform>();
            // Bottom right, clear of the home bar; the pill is at least 88 units (about 52 px) tall.
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-(Screen.width - safe.xMax) * scale - 20f, safe.y * scale + 20f);
            rt.sizeDelta = new Vector2(200f, 88f);
        }
    }
}
