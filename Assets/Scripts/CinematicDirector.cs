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
public class CinematicDirector : MonoBehaviour {
    [Serializable] public class Mark {
        public string id;
        public float[] at;
        public float[] from;
        public float[] to;
        public float yaw;
        public string anim;
        public bool tell;
    }
    [Serializable] public class Shot {
        public string id;
        public float seconds = 4f;
        public string camMode = "dolly";
        public float[] camFrom;
        public float[] camTo;
        public float[] lookAt;
        public float fov = 55f;
        public string[] frame;
        public string speaker = "N";
        public string line;
        public string sfx;
        public float[] stamp;
        public bool slam;
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
    // True while shots play or the 0.6 s camera handoff is still blending onto the follow camera.
    public static bool IsOpen { get { return Instance != null && (Instance.open || Instance.handingOff); } }
    // Other playtests turn the Chapter 1 opening off so their bot starts with control.
    public static bool SuppressOpening;
    public static readonly List<string> Events = new List<string>();
    public static int ShotsShown { get; private set; }
    public static int VoiceLines { get; private set; }
    public static int Skipped { get; private set; }
    public static int Finished { get; private set; }
    public static float LastHandoffSeconds { get; private set; }
    public static float CaptionBottom01 { get; private set; }

    public int ShotIndex { get { return shotIndex; } }
    public int ShotCount { get { return reel != null && reel.shots != null ? reel.shots.Length : 0; } }
    public string ShotId { get { return reel != null && shotIndex >= 0 && shotIndex < ShotCount ? reel.shots[shotIndex].id : ""; } }
    public string ReelId { get { return reel != null ? reel.id : ""; } }
    public string Subtitle { get { return caption != null ? caption.text : ""; } }
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
    Transform wallit;
    CharacterController wallitBody;
    MovementMotor wallitMotor;
    Transform slice;
    Animator sliceAnim;
    Enemy courier;
    bool courierOwned;

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
            if (wallit != null) {
                wallitBody = wallit.GetComponent<CharacterController>();
                wallitMotor = wallit.GetComponent<MovementMotor>();
                if (wallitMotor != null) {
                    wallitMotor.Stop();
                }
            }
            cam = level.Cam;
            if (cam != null) {
                zelda = cam.GetComponent<ZeldaStyleCamera>();
                savedFov = cam.fieldOfView;
            }
        }
        slice = host != null ? host.SliceBody : null;
        if (slice != null) {
            sliceAnim = slice.GetComponent<Animator>();
            if (sliceAnim == null) {
                sliceAnim = slice.GetComponentInChildren<Animator>();
            }
        }

        if (!ActorsReady(r)) {
            Events.Add("fallback|" + (r.fallback ?? "ch1_open"));
            open = false;
            VoiceDirector.Scene = false;
            string fb = string.IsNullOrEmpty(r.fallback) ? "ch1_open" : r.fallback;
            Cutscene.PlayShots(fb, () => {
                done?.Invoke();
                done = null;
            });
            return;
        }

        EnsureUi();
        uiRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        NextShot();
    }

    bool ActorsReady(Reel r) {
        if (wallit == null || slice == null) {
            return false;
        }
        bool needCourier = false;
        foreach (Shot s in r.shots) {
            if (s.actors == null) {
                continue;
            }
            foreach (Mark m in s.actors) {
                if (m != null && m.id == "courier") {
                    needCourier = true;
                }
            }
        }
        if (needCourier) {
            EnsureCourier();
            if (courier == null) {
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
    }

    void Update() {
        if (!open) {
            return;
        }
        if (PauseMenu.IsOpen) {
            return;
        }
        float now = Time.unscaledTime;
        Shot s = reel.shots[shotIndex];
        float u = Mathf.Clamp01((now - shotStart) / shotLength);
        PlaceActors(s, Ease(u));
        ApplyCamera(s, Ease(u));
        TickVoice(s, u);
        TickSfx(s, u);
        TickStamp(s, u);
        TickSkipInput();
        EaseBars(true);
        LayoutUi();
        if (now - shotStart >= shotLength) {
            NextShot();
        }
    }

    void LateUpdate() {
        if (!open || !viewSet || cam == null) {
            return;
        }
        cam.transform.position = viewPos;
        cam.transform.rotation = viewRot;
        if (zelda != null) {
            zelda.CinematicHold(viewPos, viewRot);
        }
    }

    void TickVoice(Shot s, float u) {
        if (shotSpoke || string.IsNullOrEmpty(s.line) || u < 0.08f) {
            return;
        }
        shotSpoke = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        string who = string.IsNullOrEmpty(s.speaker) ? "N" : s.speaker;
        if (namePlate != null) {
            namePlate.text = who == "W" ? "Wallit" : who == "S" ? "Slice" : who == "K" ? "The Courier" : who == "Y" ? "Yo Bot" : "";
            namePlate.color = who == "W" ? new Color(0.95f, 0.82f, 0.4f) : who == "S" ? new Color(0.75f, 0.7f, 0.95f) : new Color(0.7f, 0.9f, 1f);
        }
        if (caption != null) {
            caption.text = s.line;
        }
        float len;
        if (who == "W") {
            len = WallitVoice.Cutscene(s.line);
        } else if (who == "S") {
            len = SliceVoice.Instance != null ? SliceVoice.Cutscene(s.line) : CastVoice.SayNow("S", s.line, "cutscene");
        } else if (who == "Y" || who == "K") {
            // Courier (K) and Yo Bot (Y) share the chirpy bank; prefer K when the shot names the Courier.
            len = CastVoice.SayNow(who, s.line, "cutscene");
        } else {
            len = CastVoice.SayNow("N", s.line, "cutscene");
        }
        VoiceLines++;
        Events.Add("voice|" + who + "|" + s.line);
        if (len > shotLength - (Time.unscaledTime - shotStart)) {
            shotLength = (Time.unscaledTime - shotStart) + len + 0.25f;
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

    void TickSkipInput() {
        bool touch = UiFit.Touch;
        if (touch) {
            if (PlayInput.GetMouseButtonDown(0) || Input.touchCount > 0) {
                if (!skipRevealed) {
                    skipRevealed = true;
                    if (skipPill != null) {
                        skipPill.SetActive(true);
                    }
                    Events.Add("skip-pill");
                    touchDownAt = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - touchDownAt > 0.15f && skipButton != null) {
                    // A second tap anywhere after the pill is up skips (phone acceptance).
                    Skip();
                }
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
        if (!open) {
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
        }
        if (slice != null) {
            Vector3 sp = LevelYard.SlicePos;
            sp.y = LevelYard.H(sp.x, sp.z);
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
            if (m.from != null && m.from.Length >= 2 && m.to != null && m.to.Length >= 2) {
                pos = Vector3.Lerp(SeatXZ(m.from), SeatXZ(m.to), u);
            } else if (m.at != null && m.at.Length >= 2) {
                pos = SeatXZ(m.at);
            } else {
                continue;
            }
            if (m.id == "wallit") {
                pos.y = LevelYard.H(pos.x, pos.z) + 0.2f;
                MoveWallit(pos, m, u);
            } else if (m.id == "courier" && courier != null) {
                pos.y = LevelYard.H(pos.x, pos.z) + 0.55f;
                courier.transform.position = pos;
                courier.transform.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                if (m.tell || m.anim == "tell") {
                    courier.BeginTell();
                } else if (m.anim == "walk" && u >= 0.98f) {
                    courier.EndTell(false);
                }
            } else {
                pos.y = LevelYard.H(pos.x, pos.z);
                t.position = pos;
                t.rotation = Quaternion.Euler(0f, m.yaw, 0f);
                if (m.id == "slice" && sliceAnim != null) {
                    if (m.anim == "turn" || m.anim == "idle") {
                        int idle = Animator.StringToHash("idle");
                        if (sliceAnim.HasState(0, idle) && !sliceAnim.GetCurrentAnimatorStateInfo(0).IsName("idle")) {
                            sliceAnim.CrossFadeInFixedTime("idle", 0.15f, 0, 0f);
                        }
                    }
                }
            }
        }
    }

    void MoveWallit(Vector3 pos, Mark m, float u) {
        if (wallit == null) {
            return;
        }
        Vector3 flat = pos - wallit.position;
        flat.y = 0f;
        bool walking = m.anim == "walk" && flat.sqrMagnitude > 0.01f;
        if (wallitBody != null && wallitBody.enabled) {
            Vector3 delta = pos - wallit.position;
            wallitBody.Move(delta);
        } else {
            wallit.position = pos;
        }
        wallit.rotation = Quaternion.Euler(0f, m.yaw, 0f);
        if (walking) {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_RUN);
            if (wallitMotor != null) {
                Vector3 dir = flat.normalized * 3.2f;
                wallitMotor.Move(dir);
            }
        } else {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
            if (wallitMotor != null) {
                wallitMotor.Stop();
            }
        }
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
        return null;
    }

    void ApplyCamera(Shot s, float u) {
        if (cam == null) {
            return;
        }
        Vector3 from = s.camFrom != null && s.camFrom.Length >= 3 ? Vec(s.camFrom) : cam.transform.position;
        Vector3 to = s.camTo != null && s.camTo.Length >= 3 ? Vec(s.camTo) : from;
        Vector3 look = s.lookAt != null && s.lookAt.Length >= 3 ? Vec(s.lookAt) : lookFallback(s);
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
        return LevelYard.RingCenter + Vector3.up * 1.4f;
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

    static Vector3 SeatXZ(float[] a) {
        Vector3 v = Vec(a);
        v.y = LevelYard.H(v.x, v.z);
        return v;
    }

    static Vector3 Seat(float x, float z) {
        return new Vector3(x, LevelYard.H(x, z), z);
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
            rt.anchoredPosition = new Vector2(-(Screen.width - safe.xMax) * scale - 20f, -(safe.yMax - (safe.y + safe.height)) * scale - 20f);
            rt.sizeDelta = new Vector2(Mathf.Max(160f, 88f), Mathf.Max(64f, 88f));
        }
    }
}
