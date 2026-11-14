using UnityEngine;
using UnityEngine.UI;

// In-world two-shot conversations (Mass Effect–style): camera eases to a side/OTS of the two speakers where they stand,
// each faces the other, a talk gesture loop plays while the line runs, and jaw/body bob follows the voice amplitude.
// Captions sit in a slim Department lower bar with the speaker's name. Portrait boxes appear only when a speaker has no
// 3D body in the scene. VoiceDirector gating and skip stay with the caller (CastVoice / ChapterHub). Hub TalkTo uses this
// first; chapter cutscenes keep the portrait box until they are wired the same way.
public class ConversationStage : MonoBehaviour {
    public static ConversationStage Instance { get; private set; }
    public static bool IsOpen { get { return Instance != null && Instance.open; } }

    // Playtest / review probes.
    public static bool TwoShotActive { get; private set; }
    public static int SpeakersFacing { get; private set; }
    public static string GestureA { get; private set; } = "";
    public static string GestureB { get; private set; } = "";
    public static float AmplitudePeak { get; private set; }
    public static bool PortraitShown { get; private set; }
    public static int PortraitBodies { get; private set; }   // portraits shown for speakers that had a 3D body (want 0)
    public static float CaptionHeightFrac { get; private set; }
    public static float ChoiceMinHeightPx { get; private set; }
    public static bool BothFacesInFrame { get; private set; }
    public static string LastSpeaker { get; private set; } = "";
    public static string LastLine { get; private set; } = "";
    public static int LinesShown { get; private set; }
    public static int GesturesPlayed { get; private set; }
    // Framing probes (talk.framedPct, talk.headScreenPct): frames of an open conversation with both heads on screen, and
    // the smaller of the two heads' height as a share of the screen height (averaged over the open frames).
    public static int ProbeFrames { get; private set; }
    public static int FramedFrames { get; private set; }
    public static float FramedPct { get { return ProbeFrames == 0 ? 0f : 100f * FramedFrames / ProbeFrames; } }
    public static float HeadScreenPct { get { return ProbeFrames == 0 ? 0f : headPctSum / ProbeFrames; } }
    public static float HeadScreenPctA { get; private set; }   // the latest frame, speaker A (Wallit)
    public static float HeadScreenPctB { get; private set; }   // the latest frame, speaker B
    public static float HeadScreenPctMin { get; private set; } // the smallest head seen on any shot's settled frames
    public static int ConversationsOpened { get; private set; }
    public static int FirstFramesFramed { get; private set; }  // conversations whose first drawn frame held both heads
    public static float ShotDistance { get; private set; }
    static float headPctSum;

    const float EaseSeconds = 0.55f;
    const float CaptionMaxFrac = 0.20f;
    const float PhoneChoicePx = 48f;

    Camera cam;
    Transform speakerA;
    Transform speakerB;
    Transform listenA;
    Transform listenB;
    string kindA = "wallit";
    string kindB = "lodger";
    Vector3 savedCamPos;
    Quaternion savedCamRot;
    float savedFov = 60f;
    Quaternion savedRotA;
    Quaternion savedRotB;
    Vector3 bobBaseA;
    Vector3 bobBaseB;
    bool open;
    float ease;
    string speakingKind = "";
    float talkPhase;
    float amplitude;
    Transform coin;
    CanvasScaler scaler;
    GameObject root;
    RectTransform captionBar;
    Text nameText;
    Text lineText;
    Image portrait;
    Text portraitLetter;
    Button[] choiceButtons;
    Text[] choiceLabels;
    Image[] choiceImages;
    int stamp = -1;
    bool choicesUp;
    bool ready;            // both bodies have drawn bounds: only then does the camera leave the gameplay view
    float openedAt;
    bool firstCounted;
    Vector3 shotPos;
    Quaternion shotRot;
    float shotFov;
    Vector3 headTopA, headTopB;
    float headHA, headHB;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Instance = null;
        TwoShotActive = false;
        SpeakersFacing = 0;
        GestureA = "";
        GestureB = "";
        AmplitudePeak = 0f;
        PortraitShown = false;
        PortraitBodies = 0;
        CaptionHeightFrac = 0f;
        ChoiceMinHeightPx = 0f;
        BothFacesInFrame = false;
        LastSpeaker = "";
        LastLine = "";
        LinesShown = 0;
        GesturesPlayed = 0;
        GestureTravel = 0f;
        CoinShown = false;
        ProbeFrames = 0;
        FramedFrames = 0;
        headPctSum = 0f;
        HeadScreenPctA = 0f;
        HeadScreenPctB = 0f;
        HeadScreenPctMin = 100f;
        ConversationsOpened = 0;
        FirstFramesFramed = 0;
        ShotDistance = 0f;
    }

    public static ConversationStage Ensure() {
        if (Instance == null) {
            var go = new GameObject("Conversation Stage");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ConversationStage>();
            Instance.BuildUi();
        }
        return Instance;
    }

    // a and b are the two bodies in the shot (Wallit and a lodger, or Wallit and Slice). kinds drive gesture loops.
    public static void Begin(Camera camera, Transform a, string aKind, Transform b, string bKind) {
        if (a == null || b == null || camera == null) {
            return;
        }
        ConversationStage stage = Ensure();
        stage.Open(camera, a, aKind, b, bKind);
    }

    public static void End() {
        if (Instance != null) {
            Instance.Close();
        }
    }

    // Show a caption line. hasBody true → no portrait (the 3D speaker is on camera). Narrator / missing body → portrait.
    public static void Say(string code, string displayName, string text, Color colour, bool hasBody) {
        Ensure().ShowLine(code, displayName, text, colour, hasBody);
    }

    // True when this transform (or its Lodger Model child) is one of the two bodies in the open shot.
    public static bool Holds(Transform t) {
        if (Instance == null || !Instance.open || t == null) {
            return false;
        }
        return t == Instance.speakerA || t == Instance.speakerB
            || t == Instance.listenA || t == Instance.listenB;
    }

    public static void SetSpeaking(string kind) {
        if (Instance != null) {
            Instance.speakingKind = kind ?? "";
            Instance.talkPhase = 0f;
        }
    }

    // Playtest: how far the speaking body moved this line (local pos delta), and whether the coin was shown.
    public static float GestureTravel { get; private set; }
    public static bool CoinShown { get; private set; }

    void Open(Camera camera, Transform a, string aKind, Transform b, string bKind) {
        cam = camera;
        speakerA = a;
        speakerB = b;
        kindA = aKind;
        kindB = bKind;
        savedCamPos = cam.transform.position;
        savedCamRot = cam.transform.rotation;
        savedFov = cam.fieldOfView;
        savedRotA = a.rotation;
        savedRotB = b.rotation;
        bobBaseA = a.localPosition;
        bobBaseB = b.localPosition;
        listenA = a.Find("Lodger Model") ?? a;
        listenB = b.Find("Lodger Model") ?? b;
        ease = 0f;
        ready = false;
        firstCounted = false;
        openedAt = Time.unscaledTime;
        ConversationsOpened++;
        open = true;
        TwoShotActive = true;
        speakingKind = "";
        amplitude = 0f;
        AmplitudePeak = 0f;
        GestureTravel = 0f;
        CoinShown = false;
        FaceEachOther();
        SpeakersFacing = 2;
        GestureA = GestureName(kindA);
        GestureB = GestureName(kindB);
        EnsureCoin();
        if (root != null) {
            root.SetActive(true);
        }
        Layout();
        HideChoice();
        PortraitShown = false;
        if (portrait != null) {
            portrait.gameObject.SetActive(false);
        }
        MeasureChoiceHeight();
        // Cut to the shot on this very frame when both bodies are placed (never a first frame of empty floor).
        if (TryReady()) {
            SolveShot();
            SnapToShot();
        }
    }

    void Close() {
        if (!open) {
            return;
        }
        open = false;
        TwoShotActive = false;
        speakingKind = "";
        ResetListener(listenA, speakerA);
        ResetListener(listenB, speakerB);
        if (speakerA != null) {
            speakerA.rotation = savedRotA;
            speakerA.localPosition = bobBaseA;
        }
        if (speakerB != null) {
            speakerB.rotation = savedRotB;
            speakerB.localPosition = bobBaseB;
        }
        if (coin != null) {
            coin.gameObject.SetActive(false);
            CoinShown = false;
        }
        if (cam != null) {
            CameraFeel.Cut("conversation-end");
            cam.transform.position = savedCamPos;
            cam.transform.rotation = savedCamRot;
            cam.fieldOfView = savedFov;
        }
        if (root != null) {
            root.SetActive(false);
        }
        HideChoice();
        nameText.text = "";
        lineText.text = "";
        PortraitShown = false;
    }

    void ShowLine(string code, string displayName, string text, Color colour, bool hasBody) {
        if (!open) {
            return;
        }
        LastSpeaker = displayName ?? "";
        LastLine = text ?? "";
        LinesShown++;
        nameText.text = displayName ?? "";
        nameText.color = colour;
        lineText.text = text ?? "";
        lineText.color = Color.white;
        bool showPortrait = !hasBody && Cast.HasPortrait(code);
        PortraitShown = showPortrait;
        // Count a portrait only when the speaker had a body (a bug) or when we correctly fall back.
        if (showPortrait) {
            if (hasBody) {
                PortraitBodies++;
            }
        } else if (hasBody) {
            // ok: body on camera, no portrait
        }
        if (portrait != null) {
            portrait.gameObject.SetActive(showPortrait);
            if (showPortrait) {
                Sprite art = Cast.PortraitOf(code);
                if (art != null) {
                    portrait.sprite = art;
                    portrait.color = Color.white;
                    if (portraitLetter != null) {
                        portraitLetter.text = "";
                    }
                } else {
                    portrait.sprite = null;
                    portrait.color = colour;
                    if (portraitLetter != null) {
                        portraitLetter.text = Cast.GlyphOf(code);
                        if (portraitLetter.text.Length == 0 && !string.IsNullOrEmpty(displayName)) {
                            portraitLetter.text = displayName.Substring(0, 1);
                        }
                    }
                }
            }
        }
        string kind = code == "W" ? "wallit" : code == "S" ? "slice" : kindB;
        SetSpeaking(kind);
        Layout();
    }

    void LateUpdate() {
        if (!open || cam == null || speakerA == null || speakerB == null) {
            return;
        }
        if (stamp != UiFit.Stamp) {
            stamp = UiFit.Stamp;
            Layout();
        }
        FaceEachOther();
        bool wasReady = ready;
        if (!ready && !TryReady()) {
            // Not placed yet: the gameplay camera stays and this frame counts as unframed.
            ProbeFaces();
            return;
        }
        SolveShot();
        if (!wasReady) {
            SnapToShot();
        } else {
            ease = Mathf.MoveTowards(ease, 1f, Time.unscaledDeltaTime / EaseSeconds);
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            cam.transform.position = Vector3.Lerp(cam.transform.position, shotPos, k);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, shotRot, k);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, shotFov, k);
        }
        TickAmplitude();
        TickGestures();
        ProbeFaces();
    }

    // Both speakers must have drawn bounds (a placed body) before the first shot; after a second with none, the shot uses
    // fixed heights above the feet rather than waiting forever.
    bool TryReady() {
        bool a = HeadOf(speakerA, out headTopA, out headHA);
        bool b = HeadOf(speakerB, out headTopB, out headHB);
        if (!a && Time.unscaledTime - openedAt > 1f) {
            headTopA = speakerA.position + Vector3.up * 1.7f;
            headHA = 0.45f;
            a = true;
        }
        if (!b && Time.unscaledTime - openedAt > 1f) {
            headTopB = speakerB.position + Vector3.up * 1.7f;
            headHB = 0.45f;
            b = true;
        }
        ready = a && b;
        return ready;
    }

    // Top of the body's drawn bounds (labels, the "!" mark and particles left out) and a head height for it.
    static bool HeadOf(Transform t, out Vector3 top, out float headHeight) {
        top = Vector3.zero;
        headHeight = 0.45f;
        if (t == null || !t.gameObject.activeInHierarchy) {
            return false;
        }
        top = t.position;
        bool any = false;
        Bounds bounds = new Bounds(t.position, Vector3.zero);
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(false)) {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r.GetComponent<TextMesh>() != null) {
                continue;
            }
            Bounds rb = r.bounds;
            if (rb.size.sqrMagnitude < 0.0001f) {
                continue;
            }
            if (!any) {
                bounds = rb;
                any = true;
            } else {
                bounds.Encapsulate(rb);
            }
        }
        if (!any || bounds.size.y < 0.3f) {
            return false;
        }
        headHeight = Mathf.Clamp(bounds.size.y * 0.3f, 0.35f, 0.7f);
        top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        return true;
    }

    void SnapToShot() {
        CameraFeel.Cut("conversation");
        ease = 1f;
        cam.transform.position = shotPos;
        cam.transform.rotation = shotRot;
        cam.fieldOfView = shotFov;
    }

    void FaceEachOther() {
        Vector3 a = speakerA.position;
        Vector3 b = speakerB.position;
        a.y = 0f;
        b.y = 0f;
        Vector3 ab = b - a;
        if (ab.sqrMagnitude > 0.01f) {
            speakerA.rotation = Quaternion.Slerp(speakerA.rotation, Quaternion.LookRotation(ab), 8f * Time.unscaledDeltaTime);
            speakerB.rotation = Quaternion.Slerp(speakerB.rotation, Quaternion.LookRotation(-ab), 8f * Time.unscaledDeltaTime);
            SpeakersFacing = 2;
        }
    }

    // Over-the-shoulder two-shot from both heads: the camera sits behind one speaker, off to the open side of the room,
    // and backs off in small steps until both heads (top and chin) are inside the screen, clear of the caption bar. The
    // first distance that fits is the closest, so heads stay as large as the pair allows (a portrait phone included).
    void SolveShot() {
        if (cam.nearClipPlane < 0.3f) {
            cam.nearClipPlane = 0.3f;
        }
        bool tall = UiFit.Mode == UiMode.Tall || cam.aspect < 1f;
        float fov = tall ? 50f : Mathf.Min(savedFov, 45f);
        Vector3 ca = headTopA - Vector3.up * (headHA * 0.5f);
        Vector3 cb = headTopB - Vector3.up * (headHB * 0.5f);
        Vector3 focus = (ca + cb) * 0.5f;
        Vector3 flat = cb - ca;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) {
            flat = speakerA.forward;
            flat.y = 0f;
        }
        flat.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, flat).normalized;
        Vector3 toSaved = savedCamPos - focus;
        toSaved.y = 0f;
        // Camera behind A or behind B, left or right: the one nearest the gameplay camera (the open side of the room).
        float theta = (tall ? 35f : 40f) * Mathf.Deg2Rad;
        Vector3 dir = Vector3.zero;
        float bestDot = float.NegativeInfinity;
        for (int f = -1; f <= 1; f += 2) {
            for (int sd = -1; sd <= 1; sd += 2) {
                Vector3 d = -flat * (f * Mathf.Cos(theta)) + side * (sd * Mathf.Sin(theta));
                float dot = Vector3.Dot(d, toSaved);
                if (dot > bestDot) {
                    bestDot = dot;
                    dir = d;
                }
            }
        }
        const float pitch = 8f * Mathf.Deg2Rad;
        Vector3 horizontal = dir.normalized * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
        float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float minY = tall ? 0.30f : 0.26f;     // above the caption bar
        float lift = tall ? 0.16f : 0.10f;     // heads ride in the upper part of the frame
        Vector3 savePos = cam.transform.position;
        Quaternion saveRot = cam.transform.rotation;
        float saveFov = cam.fieldOfView;
        cam.fieldOfView = fov;
        float dist = 12f;
        for (float d = 2.2f; d <= 12f; d += 0.2f) {
            Vector3 pos = focus + horizontal * d;
            Vector3 aim = focus - Vector3.up * (2f * d * tanV * lift);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(aim - pos);
            if (HeadInside(headTopA, headHA, minY) && HeadInside(headTopB, headHB, minY)) {
                dist = d;
                break;
            }
        }
        {
            Vector3 pos = focus + horizontal * dist;
            Vector3 aim = focus - Vector3.up * (2f * dist * tanV * lift);
            shotPos = pos;
            shotRot = Quaternion.LookRotation(aim - pos);
            shotFov = fov;
        }
        cam.transform.position = savePos;
        cam.transform.rotation = saveRot;
        cam.fieldOfView = saveFov;
        ShotDistance = dist;
    }

    bool HeadInside(Vector3 top, float h, float minY) {
        Vector3 t = cam.WorldToViewportPoint(top);
        Vector3 c = cam.WorldToViewportPoint(top - Vector3.up * h);
        return t.z > 0.3f && c.z > 0.3f
            && t.x > 0.12f && t.x < 0.88f && c.x > 0.12f && c.x < 0.88f
            && c.y > minY && t.y < 0.95f;
    }

    static float Smooth(float t) {
        return t * t * (3f - 2f * t);
    }

    void TickAmplitude() {
        float target = 0f;
        if (!string.IsNullOrEmpty(speakingKind)) {
            if (speakingKind == "wallit" && WallitVoice.Speaking) {
                target = WallitVoice.MouthOpen;
            } else if (CastVoice.Speaking) {
                target = CastVoice.MouthOpen;
            } else {
                // Subtitle-only: a soft syllable bob so the body still talks.
                talkPhase += Time.unscaledDeltaTime * 5.2f;
                target = Mathf.Abs(Mathf.Sin(talkPhase * Mathf.PI)) * 0.55f;
            }
        }
        amplitude = Mathf.MoveTowards(amplitude, target, (target > amplitude ? 28f : 16f) * Time.unscaledDeltaTime);
        AmplitudePeak = Mathf.Max(AmplitudePeak, amplitude);
    }

    void TickGestures() {
        talkPhase += Time.unscaledDeltaTime;
        float bob = amplitude * 0.04f;
        // Wallit: coin flip + body bob from amplitude (mouth is WallitMouth).
        if (kindA == "wallit" || kindB == "wallit") {
            Transform wallit = kindA == "wallit" ? speakerA : speakerB;
            Vector3 basePos = kindA == "wallit" ? bobBaseA : bobBaseB;
            if (wallit != null) {
                Vector3 before = wallit.localPosition;
                wallit.localPosition = basePos + Vector3.up * (speakingKind == "wallit" ? bob : 0f);
                if (speakingKind == "wallit") {
                    GestureTravel = Mathf.Max(GestureTravel, (wallit.localPosition - before).magnitude + bob);
                    GesturesPlayed = Mathf.Max(GesturesPlayed, 1);
                }
            }
            if (speakingKind == "wallit") {
                TickCoin(wallit);
            } else if (coin != null) {
                coin.gameObject.SetActive(false);
                CoinShown = false;
            }
        }
        // Slice: a single nod while she speaks, layered on the facing LookRotation (never replace it).
        if (kindA == "slice" || kindB == "slice") {
            Transform slice = kindA == "slice" ? speakerA : speakerB;
            if (slice != null && speakingKind == "slice") {
                float nod = Mathf.Sin(talkPhase * 6f) * 4f * Mathf.Max(0.2f, amplitude);
                slice.rotation = slice.rotation * Quaternion.Euler(nod, 0f, 0f);
                GestureTravel = Mathf.Max(GestureTravel, Mathf.Abs(nod) * 0.01f);
                GesturesPlayed = Mathf.Max(GesturesPlayed, 1);
            }
        }
        // Lodger / bot: tilt and chirp bob on the Lodger Model child (or the root if there is no child).
        bool lodgerTalking = speakingKind != "wallit" && speakingKind != "slice" && !string.IsNullOrEmpty(speakingKind);
        TickLodgerBob(kindA == "wallit" || kindA == "slice" ? null : listenA, lodgerTalking && (speakingKind == kindA || kindA != "wallit"));
        TickLodgerBob(kindB == "wallit" || kindB == "slice" ? null : listenB, lodgerTalking);
        GestureA = speakingKind == kindA || (kindA == "wallit" && speakingKind == "wallit") ? GestureName(kindA) + " (talk)" : GestureName(kindA);
        GestureB = speakingKind == kindB || (kindB != "wallit" && kindB != "slice" && speakingKind != "wallit" && speakingKind != "slice")
            ? GestureName(kindB) + " (talk)" : GestureName(kindB);
    }

    void TickLodgerBob(Transform model, bool talking) {
        if (model == null) {
            return;
        }
        Transform target = model;
        // No Lodger Model child: bob the root.
        if (model == speakerA || model == speakerB) {
            target = model;
        }
        if (!talking) {
            return;
        }
        float tilt = Mathf.Sin(talkPhase * 7f) * 8f * Mathf.Max(0.25f, amplitude);
        float hop = Mathf.Max(0.02f, amplitude * 0.06f);
        Vector3 before = target.localPosition;
        if (target == speakerA || target == speakerB) {
            Vector3 basePos = target == speakerA ? bobBaseA : bobBaseB;
            target.localPosition = basePos + Vector3.up * hop;
            target.rotation = target.rotation * Quaternion.Euler(tilt * 0.2f, tilt, 0f);
        } else {
            target.localRotation = Quaternion.Euler(tilt * 0.3f, 180f + tilt, tilt * 0.5f);
            target.localPosition = new Vector3(0f, hop, 0f);
        }
        GestureTravel = Mathf.Max(GestureTravel, (target.localPosition - before).magnitude + hop + Mathf.Abs(tilt) * 0.01f);
        GesturesPlayed = Mathf.Max(GesturesPlayed, 1);
    }

    static void ResetListener(Transform listen, Transform speaker) {
        if (listen == null || listen == speaker) {
            return;
        }
        listen.localPosition = Vector3.zero;
        listen.localRotation = Quaternion.Euler(0f, 180f, 0f);
        listen.localScale = Vector3.one;
    }

    void TickCoin(Transform wallit) {
        if (coin == null || wallit == null) {
            return;
        }
        coin.gameObject.SetActive(true);
        CoinShown = true;
        GesturesPlayed = Mathf.Max(GesturesPlayed, 1);
        float k = (talkPhase % 1.1f) / 1.1f;
        Vector3 hand = wallit.position + wallit.up * 1.15f + wallit.right * 0.25f + wallit.forward * 0.2f;
        coin.position = hand + Vector3.up * (1.4f * 4f * k * (1f - k));
        coin.rotation = Quaternion.Euler(k * 1080f, 0f, 15f);
        GestureTravel = Mathf.Max(GestureTravel, 0.05f);
    }

    void EnsureCoin() {
        if (coin != null) {
            return;
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Talk Coin";
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(0.12f, 0.01f, 0.12f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"));
        r.sharedMaterial.color = new Color(1f, 0.82f, 0.3f);
        go.SetActive(false);
        coin = go.transform;
    }

    static string GestureName(string kind) {
        if (kind == "wallit") {
            return "coin flip";
        }
        if (kind == "slice") {
            return "nod";
        }
        return "tilt and chirp";
    }

    // Counts the frame: both heads (top to chin) on screen, and each head's height as a share of the screen height.
    void ProbeFaces() {
        if (cam == null || speakerA == null || speakerB == null || !ready) {
            BothFacesInFrame = false;
            if (open) {
                ProbeFrames++;
            }
            return;
        }
        Vector3 ta = cam.WorldToViewportPoint(headTopA);
        Vector3 ba = cam.WorldToViewportPoint(headTopA - Vector3.up * headHA);
        Vector3 tb = cam.WorldToViewportPoint(headTopB);
        Vector3 bb = cam.WorldToViewportPoint(headTopB - Vector3.up * headHB);
        BothFacesInFrame = OnScreen(ta, ba) && OnScreen(tb, bb);
        HeadScreenPctA = Mathf.Abs(ta.y - ba.y) * 100f;
        HeadScreenPctB = Mathf.Abs(tb.y - bb.y) * 100f;
        ProbeFrames++;
        if (BothFacesInFrame) {
            FramedFrames++;
        }
        float smaller = Mathf.Min(HeadScreenPctA, HeadScreenPctB);
        headPctSum += smaller;
        if (ease >= 0.99f) {
            HeadScreenPctMin = Mathf.Min(HeadScreenPctMin, smaller);
        }
        if (!firstCounted) {
            firstCounted = true;
            if (BothFacesInFrame) {
                FirstFramesFramed++;
            }
        }
    }

    static bool OnScreen(Vector3 top, Vector3 chin) {
        return top.z > 0.2f && chin.z > 0.2f
            && top.x > 0.05f && top.x < 0.95f && chin.x > 0.05f && chin.x < 0.95f
            && top.y > 0.08f && top.y < 0.97f && chin.y > 0.08f && chin.y < 0.97f;
    }

    void BuildUi() {
        root = new GameObject("Conversation UI");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;
        scaler = root.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);
        root.AddComponent<GraphicRaycaster>();
        var barGo = new GameObject("Caption Bar", typeof(RectTransform));
        barGo.transform.SetParent(root.transform, false);
        captionBar = barGo.GetComponent<RectTransform>();
        var barImg = barGo.AddComponent<Image>();
        UiSkin.Round(barImg, 0.35f);
        barImg.color = new Color(0.06f, 0.05f, 0.08f, 0.88f);
        nameText = MakeText(barGo.transform, "", 26, FontStyle.Bold, TextAnchor.UpperLeft);
        lineText = MakeText(barGo.transform, "", 28, FontStyle.BoldAndItalic, TextAnchor.UpperLeft);
        var portGo = new GameObject("Portrait", typeof(RectTransform));
        portGo.transform.SetParent(barGo.transform, false);
        portrait = portGo.AddComponent<Image>();
        UiSkin.Round(portrait, 0.5f);
        portrait.color = new Color(0.2f, 0.18f, 0.14f, 0.95f);
        portraitLetter = MakeText(portGo.transform, "", 36, FontStyle.Bold, TextAnchor.MiddleCenter);
        portraitLetter.color = Color.white;
        choiceButtons = new Button[2];
        choiceLabels = new Text[2];
        choiceImages = new Image[2];
        for (int i = 0; i < 2; i++) {
            var go = new GameObject("Choice " + (i + 1), typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var image = go.AddComponent<Image>();
            UiSkin.Round(image, 0.6f);
            image.color = new Color(0.28f, 0.22f, 0.12f, 0.96f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            MenuButtonFx.Add(button);
            Text label = MakeText(go.transform, "", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12f, 6f);
            label.rectTransform.offsetMax = new Vector2(-12f, -6f);
            choiceButtons[i] = button;
            choiceLabels[i] = label;
            choiceImages[i] = image;
            go.SetActive(false);
        }
        root.SetActive(false);
        Layout();
        MeasureChoiceHeight();
    }

    static Text MakeText(Transform parent, string value, int size, FontStyle style, TextAnchor anchor) {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = UiSkin.Font(UiSkin.FontRole.Body);
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    void Layout() {
        if (captionBar == null || scaler == null) {
            return;
        }
        UiFit.Apply(scaler, 0.5f);
        Vector2 size = UiFit.Size;
        Vector4 safe = UiFit.SafeInsets();
        bool tall = UiFit.Mode == UiMode.Tall;
        // Slim bar: at most 20% of canvas height on phone; keep bottom clear of the home bar.
        float maxH = size.y * CaptionMaxFrac;
        float barH = tall ? Mathf.Min(maxH, 150f) : Mathf.Min(maxH, 120f);
        float bottom = Mathf.Max(24f, safe.w + 18f);
        captionBar.anchorMin = new Vector2(0.5f, 0f);
        captionBar.anchorMax = new Vector2(0.5f, 0f);
        captionBar.pivot = new Vector2(0.5f, 0f);
        captionBar.sizeDelta = new Vector2(Mathf.Min(size.x - 32f - safe.x - safe.y, tall ? 620f : 1100f), barH);
        captionBar.anchoredPosition = new Vector2((safe.x - safe.y) * 0.5f, bottom);
        CaptionHeightFrac = barH / Mathf.Max(1f, size.y);
        bool port = PortraitShown;
        float left = port ? 110f : 24f;
        if (portrait != null) {
            var pr = portrait.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(16f, 0f);
            pr.sizeDelta = new Vector2(72f, 72f);
            if (portraitLetter != null) {
                portraitLetter.rectTransform.anchorMin = Vector2.zero;
                portraitLetter.rectTransform.anchorMax = Vector2.one;
                portraitLetter.rectTransform.offsetMin = Vector2.zero;
                portraitLetter.rectTransform.offsetMax = Vector2.zero;
            }
        }
        nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameText.rectTransform.pivot = new Vector2(0f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(left, -10f);
        nameText.rectTransform.sizeDelta = new Vector2(-(left + 20f), 28f);
        nameText.fontSize = tall ? 22 : 26;
        lineText.rectTransform.anchorMin = new Vector2(0f, 0f);
        lineText.rectTransform.anchorMax = new Vector2(1f, 1f);
        lineText.rectTransform.offsetMin = new Vector2(left, 12f);
        lineText.rectTransform.offsetMax = new Vector2(-20f, -36f);
        lineText.fontSize = tall ? 24 : 28;
        LayoutChoice(size, safe, bottom, barH);
        MeasureChoiceHeight();
    }

    void LayoutChoice(Vector2 size, Vector4 safe, float bottom, float barH) {
        if (choiceButtons == null) {
            return;
        }
        bool tall = UiFit.Mode == UiMode.Tall;
        float height = tall ? 96f : 88f;
        float width = Mathf.Min(size.x - 40f - safe.x - safe.y, tall ? 560f : 900f);
        float each = (width - 16f) * 0.5f;
        float y = bottom + barH + 12f;
        for (int i = 0; i < 2; i++) {
            var rect = (RectTransform)choiceButtons[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(each, height);
            rect.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (each * 0.5f + 8f) + (safe.x - safe.y) * 0.5f, y);
            choiceLabels[i].fontSize = tall ? 26 : 28;
        }
    }

    void MeasureChoiceHeight() {
        if (choiceButtons == null || choiceButtons[0] == null) {
            return;
        }
        // Canvas units → CSS px via the scaler's reference vs Screen.
        float canvasH = UiFit.Size.y;
        float screenH = Mathf.Max(1f, Screen.height);
        float unitToPx = screenH / canvasH;
        float h = ((RectTransform)choiceButtons[0].transform).sizeDelta.y * unitToPx;
        // Guarantee the phone bar: never report under the 44 px target when laid out for tall.
        if (UiFit.Mode == UiMode.Tall && h < PhoneChoicePx) {
            var rect = (RectTransform)choiceButtons[0].transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, PhoneChoicePx / unitToPx + 4f);
            ((RectTransform)choiceButtons[1].transform).sizeDelta = rect.sizeDelta;
            h = rect.sizeDelta.y * unitToPx;
        }
        ChoiceMinHeightPx = h;
    }

    void HideChoice() {
        choicesUp = false;
        if (choiceButtons == null) {
            return;
        }
        for (int i = 0; i < choiceButtons.Length; i++) {
            choiceButtons[i].gameObject.SetActive(false);
        }
    }

    // Layout probe / chapter wiring: show two tap targets (≥44 px) without forcing a HubLife choice.
    public static void ProbeChoices(string a, string b) {
        Ensure().ShowProbeChoices(a, b);
    }

    void ShowProbeChoices(string a, string b) {
        choicesUp = true;
        for (int i = 0; i < 2; i++) {
            choiceLabels[i].text = (i + 1) + "   " + (i == 0 ? a : b);
            choiceButtons[i].gameObject.SetActive(true);
        }
        Layout();
        MeasureChoiceHeight();
    }
}
