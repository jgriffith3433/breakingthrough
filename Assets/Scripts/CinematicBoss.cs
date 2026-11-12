using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Boss entrances: every named boss (the six campaign bosses and the three Arcade set pieces) arrives as an event on the
// CinematicDirector, 4 to 8 s: time slows, the camera swings from Wallit to the boss arriving (dropping in, stepping over the
// wall, unfolding, rising), the boss plays its rig's signature move (the rear-up tell, then the slam), a name card is stamped
// in the Department typeface, a stinger hits on the slam and the boss track follows it, the boss speaks its taunt, and the
// play camera comes back. Any tap, click or Enter/Space skips. It changes no health, phase or tell: the boss is held by
// Enemy.Scripted while it plays and put back exactly as it was.
// Once per run in Arcade (waves 10, 20, 30); once per case file in the campaign (CampaignSave.Slot.bosses), and again when a
// cleared chapter is replayed. Docs/Story.md, "Boss entrances".
public partial class CinematicDirector {
    public enum BossResult { Unavailable, Played, Seen }

    class BossCard {
        public string Match;     // lower-case part of the boss's title
        public string Title;     // stamped on the card
        public string Sub;       // the work-order line under it
        public string Entry;     // drop, hang, rise, unfold, climb
        public string Code;      // speaker code of the taunt (the cast voice that has the file)
        public string Line;      // the existing taunt line
        public float Root;       // the stinger's root note, Hz
        public bool Mini;        // a mid-chapter mini-boss: its own save bit
    }

    static readonly BossCard[] Cards = {
        new BossCard { Match = "courier", Title = "THE COURIER", Sub = "Notice of striking", Entry = "rise", Code = "Y", Line = "Special delivery! Notice of striking!", Root = 98f },
        new BossCard { Match = "plated", Title = "THE PLATED ALPHA", Sub = "Plate 0010: INVALID", Entry = "drop", Code = "Y", Line = "Notice! This plate is stamped: INVALID!", Root = 110f },
        new BossCard { Match = "foreman", Title = "THE FOREMAN", Sub = "Work order 0031", Entry = "unfold", Code = "Y", Line = "Order, read aloud: STRIKE. THIS. ADDRESS.", Root = 87f },
        new BossCard { Match = "half-built", Title = "THE HALF-BUILT", Sub = "Still on its hook", Entry = "hang", Code = "Y", Line = "I am... not finished! Finish me?", Root = 131f, Mini = true },
        new BossCard { Match = "server", Title = "THE SERVER", Sub = "Signature required", Entry = "climb", Code = "Y", Line = "Order delivery! Signature required!", Root = 104f },
        new BossCard { Match = "registrar", Title = "THE REGISTRAR", Sub = "Page ten, filing", Entry = "unfold", Code = "Y", Line = "Registrar on the floor! Please remain seated!", Root = 92f },
        new BossCard { Match = "order", Title = "THE ORDER-BEARER", Sub = "Sealed. Carried. Unsigned.", Entry = "climb", Code = "Y", Line = "Order delivery! Signature required!", Root = 73f },
    };

    // Timeline, in unscaled seconds from the start.
    const float TWallit = 0.9f;      // the camera holds on Wallit
    const float TSwing = 1.5f;       // then swings to the boss
    const float ArriveFrom = 0.5f;   // the boss arrives
    const float ArriveTo = 2.0f;
    const float TRear = 2.0f;        // the signature move: rear back and hold
    const float TSlam = 3.0f;        // the slam: stinger, name card and stamp on one beat
    const float TVoice = 3.4f;
    const float TTrack = 3.9f;       // the boss track, once the stinger has landed
    const float MinLength = 6.0f;
    const float MaxLength = 7.8f;
    const float SkipAfter = 0.6f;
    const float HandBack = 0.3f;

    // Events for the playtest: "boss-open|<title>", "boss-card|<title>|<sub>", "boss-stinger|<title>", "boss-voice|<title>|<line>",
    // "boss-track|<track>", "boss-end|<title>|<seconds>|<skipped>".
    public static readonly List<string> BossEvents = new List<string>();
    public static bool BossOpen { get { return Instance != null && Instance.bossRun; } }
    public static int BossIntros { get; private set; }
    public static int BossSkips { get; private set; }
    public static float LastBossSeconds { get; private set; }
    public static string LastBossTitle { get; private set; } = "";
    public static bool BossCardUp { get { return Instance != null && Instance.bossCardTitle != null && Instance.bossCard != null && Instance.bossCard.activeSelf; } }
    // The name card's middle, as a fraction of the screen from the top (0 top): the phone check wants it in the upper third.
    public static float BossCardCenterFromTop { get; private set; }
    // Set by the playtest to play a boss's entrance again (a setup shortcut); never set in play.
    public static bool IgnoreSeen;

    static readonly HashSet<string> seenThisRun = new HashSet<string>();

    bool bossRun;
    BossCard card;
    Enemy bossFoe;
    Vector3 bossSpot;
    Quaternion bossRot;
    Vector3 bossScale;
    bool bossWasScripted;
    float bossStart;
    float bossEnd;
    Action bossDone;
    int bossChapter;
    int bossWave;
    bool bossArrived, bossRose, bossSlammed, bossSpoke, bossTracked, bossShown;
    float bossLineLength;
    readonly List<Enemy> bossHeld = new List<Enemy>();
    readonly List<Renderer> bossHidden = new List<Renderer>();
    float bossFov;
    GameObject bossCard;
    Text bossCardTitle, bossCardSub;
    Image bossCardBar, bossFlash;
    AudioSource bossAudio;
    AudioClip bossStinger;
    Transform bossWallit;
    Vector3 bossWallitAt;

    // A new Arcade run: every set piece plays again.
    public static void NewRun() {
        seenThisRun.Clear();
        ArcadeActs.BossTheme = false;
    }

    static BossCard CardFor(string title, bool mini) {
        if (string.IsNullOrEmpty(title)) {
            return null;
        }
        string n = title.ToLowerInvariant();
        foreach (BossCard c in Cards) {
            if (n.Contains(c.Match) && c.Mini == mini) {
                return c;
            }
        }
        return null;
    }

    static bool IsMini(Enemy e) {
        return e != null && e.Title != null && e.Title.ToLowerInvariant().Contains("half-built");
    }

    // chapter: 0-based campaign chapter, or -1 in Arcade (wave says which set piece). done is called when the entrance ends,
    // played to its end or skipped. Unavailable: nothing to play here (the caller keeps its old behaviour); Seen: it has played
    // already this run or on this file (the caller carries on without it, and starts the boss track itself).
    public static BossResult PlayBoss(Enemy boss, int chapter, int wave, Action done) {
        BossCard c = CardFor(boss != null ? boss.Title : null, IsMini(boss));
        Camera cam = Camera.main;
        if (c == null || boss == null || !boss.Alive || cam == null || bossBusy()) {
            return BossResult.Unavailable;
        }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) {
            return BossResult.Unavailable;
        }
        if (!IgnoreSeen && SeenBefore(c, chapter, wave)) {
            return BossResult.Seen;
        }
        if (Instance == null) {
            Instance = new GameObject("Cinematic Director").AddComponent<CinematicDirector>();
        }
        Instance.BeginBoss(c, boss, player.transform, cam, chapter, wave, done);
        return BossResult.Played;
    }

    static bool bossBusy() {
        return Instance != null && (Instance.open || Instance.bossRun);
    }

    static int SaveBit(BossCard c, int chapter) {
        return 1 << (chapter + (c.Mini ? CampaignSave.ChapterCount : 0));
    }

    static bool SeenBefore(BossCard c, int chapter, int wave) {
        if (chapter >= 0 && CampaignSave.Active >= 0) {
            CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
            bool replay = s.cleared > chapter;   // a cleared chapter played again shows it again
            return (s.bosses & SaveBit(c, chapter)) != 0 && !replay;
        }
        return seenThisRun.Contains(c.Match + ":" + wave);
    }

    static void MarkSeen(BossCard c, int chapter, int wave) {
        if (chapter >= 0 && CampaignSave.Active >= 0) {
            CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
            int bit = SaveBit(c, chapter);
            if ((s.bosses & bit) == 0) {
                s.bosses |= bit;
                CampaignSave.Flush();
            }
            return;
        }
        seenThisRun.Add(c.Match + ":" + wave);
    }

    void BeginBoss(BossCard c, Enemy foe, Transform wallit, Camera camera, int chapter, int wave, Action onDone) {
        card = c;
        bossFoe = foe;
        bossWallit = wallit;
        bossChapter = chapter;
        bossWave = wave;
        bossDone = onDone;
        cam = camera;
        zelda = cam.GetComponent<ZeldaStyleCamera>();
        savedFov = cam.fieldOfView;
        bossFov = Phone() ? 64f : 55f;
        bossRun = true;
        bossArrived = bossRose = bossSlammed = bossSpoke = bossTracked = bossShown = false;
        bossLineLength = 0f;
        bossStart = Time.unscaledTime;
        bossEnd = MinLength;
        BossIntros++;
        LastBossTitle = foe.Title;
        MarkSeen(c, chapter, wave);
        BossEvents.Add("boss-open|" + foe.Title);
        Events.Add("boss-open|" + foe.Title);

        // The boss is held by the director and turned to face Wallit; everything else stands still for the length of it.
        bossSpot = foe.transform.position;
        bossRot = foe.transform.rotation;
        bossScale = foe.transform.localScale;
        bossWasScripted = foe.Scripted;
        foe.Scripted = true;
        Vector3 flat = bossWallit.position - bossSpot;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) {
            foe.transform.rotation = Quaternion.LookRotation(flat.normalized);
        }
        bossHeld.Clear();
        foreach (Enemy other in new List<Enemy>(Enemy.Roster)) {
            if (other != null && other != foe && other.Alive) {
                other.Hold(MaxLength + 2f);
                bossHeld.Add(other);
            }
        }
        // Keep the boss out of sight until it arrives.
        bossHidden.Clear();
        if (c.Entry != "unfold") {
            foreach (Renderer r in foe.GetComponentsInChildren<Renderer>()) {
                if (r.enabled) {
                    r.enabled = false;
                    bossHidden.Add(r);
                }
            }
        }
        if (bossAudio == null) {
            bossAudio = gameObject.AddComponent<AudioSource>();
            bossAudio.playOnAwake = false;
            bossAudio.spatialBlend = 0f;
            bossAudio.ignoreListenerPause = true;
        }
        bossStinger = MakeStinger(c.Root);

        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        EnsureUi();
        EnsureBossUi();
        uiRoot.SetActive(true);
        skipPill.SetActive(false);
        bossCard.SetActive(false);
        if (caption != null) {
            caption.text = "";
        }
        if (namePlate != null) {
            namePlate.text = "";
        }
        PlaceBoss(0f);
        BossCamera(0f);
    }

    // ---------------------------------------------------------------- per frame

    void TickBoss() {
        if (PauseMenu.IsOpen) {
            return;
        }
        if (bossFoe == null || !bossFoe.Alive || bossWallit == null || cam == null) {
            EndBoss(true);
            return;
        }
        float t = Time.unscaledTime - bossStart;
        if (t > SkipAfter && SkipTapped()) {
            BossSkips++;
            BossEvents.Add("boss-skip|" + bossFoe.Title);
            Events.Add("skip|" + card.Match);
            EndBoss(true);
            return;
        }
        Time.timeScale = TimeScaleAt(t);

        if (!bossRose && t >= TRear) {
            bossRose = true;
            bossFoe.BeginTell();   // the rig's own rear-up and tremble
        }
        if (!bossSlammed && t >= TSlam) {
            bossSlammed = true;
            Slam();
        }
        if (!bossSpoke && t >= TVoice) {
            bossSpoke = true;
            Speak(t);
        }
        if (!bossTracked && t >= TTrack) {
            StartTrack();
        }
        PlaceBoss(t);
        BossCamera(t);
        TickBossCard(t);
        EaseBars(true);
        LayoutUi();
        if (t >= bossEnd) {
            EndBoss(false);
        }
    }

    static bool SkipTapped() {
        if (PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter)
            || PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetMouseButtonDown(0)) {
            return true;
        }
        for (int i = 0; i < Input.touchCount; i++) {
            if (Input.GetTouch(i).phase == TouchPhase.Began) {
                return true;
            }
        }
        return false;
    }

    // Normal speed, then slow while the boss arrives and rears, back up after the slam.
    static float TimeScaleAt(float t) {
        if (t < 0.3f) {
            return Mathf.Lerp(1f, 0.3f, t / 0.3f);
        }
        if (t < TSlam) {
            return 0.3f;
        }
        return Mathf.Lerp(0.3f, 1f, Mathf.Clamp01((t - TSlam) / 0.8f));
    }

    void Slam() {
        bossFoe.EndTell(true);
        Vector3 at = bossSpot;
        Fx.Ring(at + Vector3.up * 0.15f, bossFoe.BodyScale * 5f, new Color(1f, 0.82f, 0.4f, 0.9f), 0.8f);
        Fx.Emit(FxKind.Dust, at + Vector3.up * 0.2f, 12, new Color(0.6f, 0.55f, 0.48f, 0.5f), 1.2f, 2f, 1.6f);
        GameAudio.Play("stomp", 1f, 0.55f);
        if (SoundSettings.On && bossStinger != null && bossAudio != null) {
            bossAudio.PlayOneShot(bossStinger, Mathf.Clamp01(SoundSettings.EffectsLevel) * 0.9f);
        }
        GameAudio.NoteOther(GameAudio.Bus.Effects, "boss_stinger");
        BossEvents.Add("boss-stinger|" + bossFoe.Title);
        Events.Add("boss-stinger|" + card.Match);
        ShowBossCard();
        if (bossChapter < 0 && bossWave == 10) {
            BossSetPiece.CrackSlice(bossFoe);   // Story act two: Slice's first crack, taken on camera
        }
    }

    void Speak(float t) {
        string text = card.Line;
        if (namePlate != null) {
            namePlate.text = card.Title.Substring(0, 1) + card.Title.Substring(1).ToLowerInvariant();
            namePlate.color = new Color(1f, 0.82f, 0.45f);
        }
        if (caption != null) {
            caption.text = text;
        }
        float len = CastVoice.SayNow(card.Code, text, "cutscene");
        if (len <= 0f) {
            len = WallitVoice.EstimateSeconds(text);
        }
        bossLineLength = len;
        bossEnd = Mathf.Clamp(Mathf.Max(MinLength, t + len + 0.5f), MinLength, MaxLength);
        BossEvents.Add("boss-voice|" + bossFoe.Title + "|" + text);
        Events.Add("voice|" + card.Code + "|" + text);
        VoiceLines++;
    }

    void StartTrack() {
        bossTracked = true;
        string track = MusicDirector.BossTrack(bossFoe.Title);
        if (track.Length == 0) {
            return;
        }
        MusicDirector.Play(track);
        if (bossChapter < 0) {
            ArcadeActs.BossTheme = true;   // Arcade's own score layers step back under the boss theme
        }
        BossEvents.Add("boss-track|" + track);
    }

    // ---------------------------------------------------------------- the boss's arrival

    void PlaceBoss(float t) {
        Transform tr = bossFoe.transform;
        float k = Mathf.Clamp01((t - ArriveFrom) / (ArriveTo - ArriveFrom));
        Vector3 away = bossSpot - bossWallit.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        float sc = Mathf.Max(1f, bossFoe.BodyScale);
        Vector3 pos = bossSpot;
        Vector3 scale = bossScale;
        switch (card.Entry) {
            case "drop":
            case "hang": {
                float height = card.Entry == "drop" ? 18f : 5.5f;
                float fall = k * k;
                pos.y += height * (1f - fall);
                if (card.Entry == "hang") {
                    pos += Vector3.Cross(Vector3.up, away) * Mathf.Sin(k * 9f) * 0.5f * (1f - k);
                }
                break;
            }
            case "rise":
                pos.y -= 2.6f * sc * (1f - Ease(k));
                break;
            case "climb": {
                float e = Ease(k);
                pos += away * (1f - e) * 11f;
                pos.y += Mathf.Sin(e * Mathf.PI) * 2.6f;   // over the wall
                break;
            }
            case "unfold": {
                float e = EaseOutBack(k);
                scale = new Vector3(bossScale.x * Mathf.Lerp(0.7f, 1f, e), bossScale.y * Mathf.Lerp(0.15f, 1f, e), bossScale.z * Mathf.Lerp(0.7f, 1f, e));
                break;
            }
        }
        tr.position = pos;
        tr.localScale = scale;
        if (t >= ArriveFrom && bossHidden.Count > 0) {
            foreach (Renderer r in bossHidden) {
                if (r != null) {
                    r.enabled = true;
                }
            }
            bossHidden.Clear();
            if (card.Entry == "rise") {
                Fx.Ring(bossSpot + Vector3.up * 0.15f, 9f, new Color(1f, 0.8f, 0.4f, 0.9f), 1f);
            }
        }
        if (!bossArrived && k >= 1f) {
            bossArrived = true;
            Fx.Ring(bossSpot + Vector3.up * 0.15f, sc * 4f, new Color(0.9f, 0.8f, 0.6f, 0.7f), 0.6f);
            Fx.Emit(FxKind.Dust, bossSpot + Vector3.up * 0.2f, 10, new Color(0.6f, 0.55f, 0.48f, 0.5f), 1.2f, 2f, 1.6f);
            GameAudio.Play("stomp", 0.9f, card.Entry == "drop" ? 0.5f : 0.75f);
        }
    }

    static float EaseOutBack(float k) {
        k = Mathf.Clamp01(k);
        float c = 1.70158f;
        float x = k - 1f;
        return 1f + (c + 1f) * x * x * x + c * x * x;
    }

    // ---------------------------------------------------------------- the camera

    void BossCamera(float t) {
        bool phone = Phone();
        Vector3 wallit = bossWallit.position;
        Vector3 toBoss = bossSpot - wallit;
        toBoss.y = 0f;
        toBoss = toBoss.sqrMagnitude > 0.01f ? toBoss.normalized : Vector3.forward;
        float sc = Mathf.Max(1f, bossFoe.BodyScale);

        // From Wallit: over his shoulder, the boss's direction ahead.
        Vector3 camA = wallit - toBoss * 3.4f + Vector3.up * 2.1f;
        Vector3 lookA = wallit + toBoss * 5f + Vector3.up * 1.2f;
        camA += toBoss * Mathf.Clamp01(t / TWallit) * 0.8f;

        // On the boss: low and wide, a slow orbit, the boss low in frame so the name card has the top.
        float reach = (8f + 2.4f * sc) * (phone ? 1.4f : 1f);
        float ang = Mathf.Lerp(-26f, 22f, Mathf.Clamp01((t - TWallit) / (MaxLength - TWallit)));
        Vector3 dir = Quaternion.Euler(0f, ang, 0f) * (-toBoss);
        Vector3 camC = bossSpot + dir * reach + Vector3.up * (1.3f + 0.8f * sc);
        Vector3 lookC = bossSpot + Vector3.up * (1.3f * sc + (phone ? 0.6f * sc : 0.1f));

        float e = Ease(Mathf.Clamp01((t - TWallit) / TSwing));
        Vector3 pos = Vector3.Lerp(camA, camC, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 2.5f);
        Vector3 look = Vector3.Lerp(lookA, lookC, e);
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 60f, Vector3.down, out hit, 120f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            pos.y = Mathf.Max(pos.y, hit.point.y + 1.2f);
        }
        viewPos = pos;
        Vector3 d = look - pos;
        viewRot = Quaternion.LookRotation(d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward, Vector3.up);
        viewSet = true;
        cam.fieldOfView = bossFov;
        cam.transform.position = viewPos;
        cam.transform.rotation = viewRot;
        if (zelda != null) {
            zelda.CinematicHold(viewPos, viewRot);
        }
    }

    // ---------------------------------------------------------------- the name card

    void EnsureBossUi() {
        if (bossCard != null) {
            return;
        }
        bossCard = new GameObject("Boss Card");
        bossCard.transform.SetParent(uiRoot.transform, false);
        var rt = bossCard.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var flashGo = new GameObject("Flash");
        flashGo.transform.SetParent(bossCard.transform, false);
        bossFlash = flashGo.AddComponent<Image>();
        bossFlash.color = new Color(1f, 0.95f, 0.8f, 0f);
        bossFlash.raycastTarget = false;
        var frt = flashGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;

        bossCardTitle = BossText("Title", 64, new Color(1f, 0.93f, 0.78f), TextAnchor.LowerCenter);
        bossCardTitle.resizeTextForBestFit = true;
        bossCardTitle.resizeTextMinSize = 26;
        bossCardTitle.resizeTextMaxSize = 84;
        bossCardTitle.fontStyle = FontStyle.Bold;
        bossCardSub = BossText("Sub", 30, new Color(0.95f, 0.4f, 0.32f), TextAnchor.UpperCenter);

        var barGo = new GameObject("Bar");
        barGo.transform.SetParent(bossCard.transform, false);
        bossCardBar = barGo.AddComponent<Image>();
        bossCardBar.color = new Color(0.78f, 0.1f, 0.1f, 0.95f);
        bossCardBar.raycastTarget = false;
    }

    Text BossText(string name, int size, Color color, TextAnchor anchor) {
        var go = new GameObject(name);
        go.transform.SetParent(bossCard.transform, false);
        var tx = go.AddComponent<Text>();
        tx.font = UiSkin.Font(UiSkin.FontRole.Label);   // Special Elite: the Department's typewriter and stamp face
        tx.fontSize = size;
        tx.alignment = anchor;
        tx.color = color;
        tx.raycastTarget = false;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        tx.verticalOverflow = VerticalWrapMode.Truncate;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(3f, -3f);
        return tx;
    }

    // The card sits in the upper third of the screen on a phone and on desktop alike (the boss is low in frame).
    void ShowBossCard() {
        bossShown = true;
        bossCardTitle.text = card.Title;
        bossCardSub.text = card.Sub;
        bossCard.SetActive(true);
        // y fractions from the bottom: title 0.76-0.86, bar 0.755-0.76, sub 0.69-0.75. The top letterbox is 5% on a phone.
        Place(bossCardTitle.rectTransform, 0.05f, 0.76f, 0.95f, 0.87f);
        Place(bossCardSub.rectTransform, 0.05f, 0.69f, 0.95f, 0.75f);
        var br = bossCardBar.rectTransform;
        br.anchorMin = new Vector2(0.12f, 0.752f);
        br.anchorMax = new Vector2(0.88f, 0.759f);
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        BossCardCenterFromTop = 1f - (0.69f + 0.87f) * 0.5f;
        BossEvents.Add("boss-card|" + card.Title + "|" + card.Sub);
        Events.Add("boss-card|" + card.Title);
    }

    static void Place(RectTransform rt, float x0, float y0, float x1, float y1) {
        rt.anchorMin = new Vector2(x0, y0);
        rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // The stamp: the title lands large and settles, a pale flash, the red bar draws out, the work-order line fades in.
    void TickBossCard(float t) {
        if (!bossShown) {
            return;
        }
        float since = Mathf.Max(0f, t - TSlam);
        float land = Mathf.Clamp01(since / 0.18f);
        float s = Mathf.Lerp(1.6f, 1f, land);
        bossCardTitle.rectTransform.localScale = new Vector3(s, s, 1f);
        Color tc = bossCardTitle.color;
        tc.a = land;
        bossCardTitle.color = tc;
        bossFlash.color = new Color(1f, 0.95f, 0.8f, Mathf.Clamp01(1f - since / 0.35f) * 0.45f);
        float draw = Mathf.Clamp01((since - 0.1f) / 0.3f);
        bossCardBar.rectTransform.localScale = new Vector3(draw, 1f, 1f);
        Color sc = bossCardSub.color;
        sc.a = Mathf.Clamp01((since - 0.3f) / 0.4f);
        bossCardSub.color = sc;
        // Gone with the last half second so the play camera has the screen.
        float left = bossEnd - t;
        if (left < 0.5f) {
            float a = Mathf.Clamp01(left / 0.5f);
            tc.a = a;
            bossCardTitle.color = tc;
            sc.a = Mathf.Min(sc.a, a);
            bossCardSub.color = sc;
            Color bc = bossCardBar.color;
            bc.a = 0.95f * a;
            bossCardBar.color = bc;
        }
    }

    // ---------------------------------------------------------------- the end

    void EndBoss(bool skipped) {
        if (!bossRun) {
            return;
        }
        bossRun = false;
        float seconds = Time.unscaledTime - bossStart;
        LastBossSeconds = seconds;
        Time.timeScale = 1f;
        if (bossFoe != null) {
            bossFoe.transform.position = bossSpot;
            bossFoe.transform.localScale = bossScale;
            bossFoe.Scripted = bossWasScripted;
            if (bossRose && !bossSlammed) {
                bossFoe.EndTell(false);
            }
        }
        foreach (Renderer r in bossHidden) {
            if (r != null) {
                r.enabled = true;
            }
        }
        bossHidden.Clear();
        foreach (Enemy e in bossHeld) {
            if (e != null) {
                e.Hold(0f);
            }
        }
        bossHeld.Clear();
        CastVoice.Silence();
        VoiceDirector.Scene = false;
        if (bossAudio != null && skipped) {
            bossAudio.Stop();
        }
        if (!bossTracked && bossFoe != null) {
            StartTrack();   // a skipped entrance still turns the music
        }
        EaseBars(false);
        if (uiRoot != null) {
            uiRoot.SetActive(false);
        }
        if (bossCard != null) {
            bossCard.SetActive(false);
            Color bc = bossCardBar.color;
            bc.a = 0.95f;
            bossCardBar.color = bc;
        }
        bossShown = false;
        if (cam != null) {
            cam.fieldOfView = savedFov;
        }
        if (zelda != null) {
            zelda.BeginHandoff(HandBack);   // the play camera is back at once; the blend is 0.3 s
        }
        viewSet = false;
        BossEvents.Add("boss-end|" + LastBossTitle + "|" + seconds.ToString("0.00") + "|" + (skipped ? "skipped" : "played"));
        Events.Add(skipped ? "skipped" : "done");
        if (!skipped) {
            Finished++;
        }
        Action cb = bossDone;
        bossDone = null;
        bossFoe = null;
        cb?.Invoke();
    }

    void OnDestroy() {
        if (bossRun) {
            bossRun = false;
            Time.timeScale = 1f;
            VoiceDirector.Scene = false;
        }
    }

    // ---------------------------------------------------------------- the stinger

    // A short hit made in code: a boom that falls from above its pitch, a brass-like chord on the boss's own root, and a
    // stamp of filtered noise. Every boss has its own root, so no two entrances play the same sample.
    static AudioClip MakeStinger(float root) {
        const int rate = 44100;
        int n = (int)(rate * 1.9f);
        var d = new float[n];
        var rng = new System.Random((int)(root * 100f));
        float[] ratios = { 1f, 1.498f, 2f, 2.378f };
        float lp = 0f;
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) {
            float t = i / (float)rate;
            float boom = Mathf.Sin(2f * Mathf.PI * root * 0.5f * (1f + 0.6f * Mathf.Exp(-t * 9f)) * t) * Mathf.Exp(-t * 3.2f);
            float chord = 0f;
            for (int r = 0; r < ratios.Length; r++) {
                float f = root * 2f * ratios[r];
                for (int h = 1; h <= 4; h++) {
                    chord += Mathf.Sin(2f * Mathf.PI * f * h * t) / h;
                }
            }
            chord *= 0.1f * Mathf.Exp(-t * 1.6f) * (1f - Mathf.Exp(-t * 60f));
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.25f;
            float stamp = lp * Mathf.Exp(-t * 40f) * 0.9f;
            float v = boom * 0.9f + chord + stamp;
            d[i] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }
        float gain = 0.7f / peak;
        int fade = (int)(rate * 0.06f);
        for (int i = 0; i < n; i++) {
            float g = gain;
            if (i > n - fade) {
                g *= (n - i) / (float)fade;
            }
            d[i] *= g;
        }
        AudioClip clip = AudioClip.Create("boss_stinger_" + (int)root, n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
