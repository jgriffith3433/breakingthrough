using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Slice as partner: she names a bot about to reach her circle or flank Wallit (one clipped word), paints that bot with a
// short-lived ring in her accent colour and an off-screen edge arrow, and unlocks a guard move by Arcade act.
// Act 0 (Night Shift, waves 1-10): callouts only.
// Act 1 (Cold Hour, 11-20): one parry per wave (block pose, metallic clink, knockback 2 m).
// Act 2 (Last Light, 21+): one ground stamp per wave (stun nearby bots 1.5 s).
// Companion chapters get callouts plus the Cold Hour parry (she is already fighting beside him).
// Lines go through VoiceDirector at bark priority so they never stack. Docs/Story.md "Slice as partner".
public class SliceCallouts : MonoBehaviour {
    public static SliceCallouts Instance { get; private set; }

    // Accent from Cast slice colour (#DBE6FF).
    public static readonly Color Accent = new Color(0.86f, 0.90f, 1f, 1f);

    public const float CircleWarn = 7.5f;
    public const float FlankReach = 11f;
    public const float MarkerSeconds = 2.6f;
    public const float ParryKnock = 2f;
    public const float StampStun = 1.5f;
    public const float StampRadius = 6.5f;

    // One-word callouts (clipped). Ids via SliceVoice.LineId -> slice_left, slice_mine, ...
    public static readonly string[] Callouts = {
        "Left.", "Right.", "Behind.", "Ahead.", "Spider.", "Mine.",
        "Close.", "Flank.", "Hold.", "Clear.", "Yo.", "Down."
    };
    public static readonly string[] ParryLine = { "Blocked." };
    public static readonly string[] StampLine = { "Stamp." };

    public static readonly List<string> Events = new List<string>();
    public static int CalloutCount { get; private set; }
    public static int ParryCount { get; private set; }
    public static int StampCount { get; private set; }
    public static int VoiceOverlaps { get; private set; }
    public static bool MarkerVisible { get; private set; }
    public static bool ArrowVisible { get; private set; }
    public static string LastWord { get; private set; } = "";
    public static Vector2 LastArrowAnchor { get; private set; }
    public static Rect LastCaptionRect { get; private set; }

    Musician musician;
    int waveSeen = -1;
    int actSeen = -1;
    bool parriedThisWave;
    bool stampedThisWave;
    bool unlockParryShown;
    bool unlockStampShown;
    Enemy marked;
    float markLeft;
    LineRenderer ring;
    LineRenderer shock;
    float shockLeft;
    Image arrow;
    RectTransform arrowRect;
    Text caption;
    CanvasScaler scaler;
    int layoutStamp = -1;
    float nextScan;
    float lastCallAt = -99f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Instance = null;
        Events.Clear();
        CalloutCount = 0;
        ParryCount = 0;
        StampCount = 0;
        VoiceOverlaps = 0;
        MarkerVisible = false;
        ArrowVisible = false;
        LastWord = "";
    }

    public static void Ensure(Musician owner) {
        if (owner == null || owner.GetComponent<SliceCallouts>() != null) {
            return;
        }
        owner.gameObject.AddComponent<SliceCallouts>().musician = owner;
    }

    public static void Note(string what, string detail) {
        Events.Add(what + "|" + Time.unscaledTime.ToString("0.0") + "|" + detail);
    }

    // Guard unlock: Arcade act, or Cold Hour (parry) while Slice walks as companion.
    public static int GuardLevel() {
        if (ChapterRun.Instance != null && Companion.Instance != null && !Companion.Instance.Downed) {
            return 1;
        }
        if (WaveDirector.Instance == null || ChapterRun.Instance != null) {
            return 0;
        }
        return Tuning.ActOf(WaveDirector.Wave);
    }

    void Awake() {
        Instance = this;
        Events.Clear();
        CalloutCount = 0;
        ParryCount = 0;
        StampCount = 0;
        VoiceOverlaps = 0;
        BuildUi();
        BuildRing();
        BuildShock();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        if (ring != null) {
            Destroy(ring.gameObject);
        }
        if (shock != null) {
            Destroy(shock.gameObject);
        }
    }

    void Start() {
        if (musician == null) {
            musician = GetComponent<Musician>();
        }
    }

    void Update() {
        if (musician == null || !musician.Alive || musician.Away && Companion.Instance == null) {
            HideMarks();
            return;
        }
        if (PauseMenu.IsOpen || LevelUpScreen.IsOpen || Cutscene.IsOpen || StoryScreen.IsOpen || WaveDirector.IsLost) {
            HideMarks();
            return;
        }
        if (WaveDirector.Instance != null && WaveDirector.Wave != waveSeen) {
            waveSeen = WaveDirector.Wave;
            parriedThisWave = false;
            stampedThisWave = false;
            int act = Tuning.ActOf(waveSeen);
            if (act != actSeen) {
                actSeen = act;
                OnActUnlock(act);
            }
        }
        if (layoutStamp != UiFit.CaptionStamp && scaler != null) {
            layoutStamp = UiFit.CaptionStamp;
            UiFit.Apply(scaler);
            if (caption != null) {
                UiFit.PlaceCaption(caption.rectTransform, 240f, 0);
            }
        }
        TickMarker(Time.deltaTime);
        TickShock(Time.deltaTime);
        if (Time.unscaledTime < nextScan) {
            return;
        }
        nextScan = Time.unscaledTime + 0.18f;
        TickThreats();
    }

    // First wave of Cold Hour / Last Light: she shows the new move once on camera (voice + pose), so the act turn is lived.
    void OnActUnlock(int act) {
        if (act == 1 && !unlockParryShown) {
            unlockParryShown = true;
            Note("partner-unlock", "Cold Hour: Slice will parry");
            if (SliceVoice.Instance != null) {
                SliceVoice.Instance.SayCallout("Hold.");
            }
            LastWord = "Hold.";
            WallitVoice.OnCallout("Hold.");
        } else if (act >= 2 && !unlockStampShown) {
            unlockStampShown = true;
            Note("partner-unlock", "Last Light: Slice will stamp");
            if (SliceVoice.Instance != null) {
                SliceVoice.Instance.SayCallout("Stamp.");
            }
            LastWord = "Stamp.";
            WallitVoice.OnCallout("Stamp.");
        }
    }

    void TickThreats() {
        Vector3 sliceAt = SliceAt();
        Vector3 wallitAt = WallitAt();
        Vector3 wallitFwd = WallitForward();
        Enemy best = null;
        string word = null;
        float bestScore = float.MaxValue;
        bool forCircle = false;

        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy bot = Enemy.Roster[i];
            if (bot == null || !bot.Alive || bot.Climbing) {
                continue;
            }
            Vector3 pos = bot.transform.position;
            float toSlice = Flat(pos - sliceAt).magnitude;
            float toWallit = Flat(pos - wallitAt).magnitude;

            // About to reach her circle.
            if (toSlice <= CircleWarn && toSlice < bestScore) {
                best = bot;
                bestScore = toSlice;
                forCircle = true;
                word = WordFor(bot, wallitAt, wallitFwd, true, toSlice);
            }
            // Flanking Wallit (beside or behind, not already on Slice).
            else if (toWallit <= FlankReach && toSlice > StabNear()) {
                Vector3 from = Flat(pos - wallitAt);
                if (from.sqrMagnitude < 0.01f) {
                    continue;
                }
                float side = Vector3.Dot(from.normalized, wallitFwd);
                if (side > 0.35f) {
                    continue; // ahead of him: he can see it
                }
                float score = toWallit + 2f;
                if (score < bestScore) {
                    best = bot;
                    bestScore = score;
                    forCircle = false;
                    word = WordFor(bot, wallitAt, wallitFwd, false, toWallit);
                }
            }
        }

        int guard = GuardLevel();
        // Parry: bot on her circle, or one about to hit Wallit (she covers him).
        if (guard >= 1 && !parriedThisWave && best != null) {
            float toSlice = Flat(best.transform.position - sliceAt).magnitude;
            float toWallit = Flat(best.transform.position - wallitAt).magnitude;
            bool circleHit = forCircle && toSlice <= Musician.StabReach + 0.6f;
            bool coverHim = !forCircle && toWallit <= 3.2f;
            if (circleHit || coverHim) {
                DoParry(best);
                return;
            }
        }
        if (guard >= 2 && !stampedThisWave && forCircle && best != null && Flat(best.transform.position - sliceAt).magnitude <= 4.2f) {
            DoStamp(sliceAt);
            return;
        }
        if (best != null && !string.IsNullOrEmpty(word) && Time.unscaledTime - lastCallAt >= 2.5f) {
            TryCallout(best, word);
        }
    }

    string WordFor(Enemy bot, Vector3 wallitAt, Vector3 wallitFwd, bool forCircle, float dist) {
        if (BotRoster.IsSpider(bot.Kind)) {
            return "Spider.";
        }
        if (forCircle && dist <= Musician.StabReach + 1.2f) {
            return Random.value < 0.5f ? "Mine." : "Close.";
        }
        if (forCircle) {
            return Random.value < 0.35f ? "Mine." : DirWord(bot.transform.position, wallitAt, wallitFwd);
        }
        if (dist < 4.5f) {
            return "Close.";
        }
        return DirWord(bot.transform.position, wallitAt, wallitFwd);
    }

    static string DirWord(Vector3 botAt, Vector3 from, Vector3 fwd) {
        Vector3 flat = Flat(botAt - from);
        if (flat.sqrMagnitude < 0.01f) {
            return "Close.";
        }
        flat.Normalize();
        float ahead = Vector3.Dot(flat, fwd);
        if (ahead < -0.45f) {
            return "Behind.";
        }
        if (ahead > 0.55f) {
            return "Ahead.";
        }
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        return Vector3.Dot(flat, right) >= 0f ? "Right." : "Left.";
    }

    void TryCallout(Enemy bot, string word) {
        if (SliceVoice.Instance == null || bot == null) {
            return;
        }
        // Speak through SliceVoice so the floor, VoiceDirector bark gate and arcade keep stay consistent.
        if (!SliceVoice.Instance.SayCallout(word)) {
            return;
        }
        lastCallAt = Time.unscaledTime;
        CalloutCount++;
        LastWord = word;
        Mark(bot);
        Note("slice-callout", word + " on " + bot.name);
        // Some callouts get a money reply after her line clears.
        if (Random.value < 0.4f) {
            WallitVoice.OnCallout(word);
        }
    }

    void DoParry(Enemy bot) {
        if (bot == null || !bot.Alive || parriedThisWave) {
            return;
        }
        parriedThisWave = true;
        ParryCount++;
        Vector3 from = SliceAt();
        Vector3 dir = Flat(bot.transform.position - from);
        if (dir.sqrMagnitude < 0.01f) {
            dir = Flat(bot.transform.position - WallitAt());
        }
        if (dir.sqrMagnitude < 0.01f) {
            dir = Vector3.forward;
        }
        dir.Normalize();
        musician.BeginParry(bot, dir);
        bot.Knock(dir * 7f, 0.12f); // ~2 m shove with ground drag
        bot.Flinch(0.85f);
        GameAudio.Play("sliceparry", 0.7f, 1f);
        Mark(bot);
        PulseShock(bot.transform.position, 1.4f, 0.45f);
        if (SliceVoice.Instance != null) {
            SliceVoice.Instance.SayCallout(ParryLine[0]);
        }
        LastWord = ParryLine[0];
        WallitVoice.OnCallout(ParryLine[0]);
        Note("slice-parry", "knocked " + bot.name + " " + ParryKnock.ToString("0.0") + " m");
    }

    void DoStamp(Vector3 at) {
        if (stampedThisWave) {
            return;
        }
        stampedThisWave = true;
        StampCount++;
        musician.BeginStamp();
        GameAudio.Play("stamp", 0.65f, 0.85f);
        PulseShock(at, StampRadius * 0.85f, 0.7f);
        int hit = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy bot = Enemy.Roster[i];
            if (bot == null || !bot.Alive) {
                continue;
            }
            if (Flat(bot.transform.position - at).magnitude > StampRadius) {
                continue;
            }
            bot.Chill(0.85f, StampStun, StampStun);
            bot.Hold(StampStun);
            hit++;
            if (marked == null) {
                Mark(bot);
            }
        }
        if (SliceVoice.Instance != null) {
            SliceVoice.Instance.SayCallout(StampLine[0]);
        }
        LastWord = StampLine[0];
        Note("slice-stamp", "stunned " + hit + " bot(s) for " + StampStun.ToString("0.0") + " s");
    }

    void PulseShock(Vector3 at, float radius, float seconds) {
        if (shock == null) {
            return;
        }
        at.y = 0.06f;
        shock.transform.position = at;
        for (int i = 0; i < shock.positionCount; i++) {
            float ang = i / (float)shock.positionCount * Mathf.PI * 2f;
            shock.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
        }
        shockLeft = Mathf.Max(0.2f, seconds);
        shock.gameObject.SetActive(true);
    }

    void TickShock(float dt) {
        if (shock == null || shockLeft <= 0f) {
            if (shock != null) {
                shock.gameObject.SetActive(false);
            }
            return;
        }
        shockLeft -= dt;
        float a = Mathf.Clamp01(shockLeft / 0.7f);
        Color c = Accent;
        c.a = 0.65f * a;
        shock.startColor = c;
        shock.endColor = new Color(c.r, c.g, c.b, 0f);
        if (shockLeft <= 0f) {
            shock.gameObject.SetActive(false);
        }
    }

    void Mark(Enemy bot) {
        marked = bot;
        markLeft = MarkerSeconds;
        MarkerVisible = true;
        Note("callout-ring", bot != null ? bot.name : "?");
    }

    void TickMarker(float dt) {
        if (markLeft <= 0f || marked == null || !marked.Alive) {
            HideMarks();
            return;
        }
        markLeft -= dt;
        float a = Mathf.Clamp01(markLeft / MarkerSeconds);
        if (ring != null) {
            Vector3 p = marked.transform.position;
            p.y = 0.05f;
            ring.transform.position = p;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
            Color c = Accent;
            c.a = 0.55f * a * pulse;
            ring.startColor = c;
            ring.endColor = c;
            ring.gameObject.SetActive(true);
        }
        MarkerVisible = true;
        TickArrow(marked);
        TickCaption();
        if (markLeft <= 0f) {
            HideMarks();
        }
    }

    void TickArrow(Enemy bot) {
        Camera cam = Camera.main;
        if (arrow == null || arrowRect == null || cam == null || bot == null) {
            ArrowVisible = false;
            if (arrow != null) {
                arrow.gameObject.SetActive(false);
            }
            return;
        }
        Vector3 vp = cam.WorldToViewportPoint(bot.transform.position + Vector3.up);
        bool onScreen = vp.z > 0f && vp.x > 0.06f && vp.x < 0.94f && vp.y > 0.08f && vp.y < 0.92f;
        if (onScreen) {
            arrow.gameObject.SetActive(false);
            ArrowVisible = false;
            return;
        }
        Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f) {
            dir = -dir;
        }
        if (dir.sqrMagnitude < 0.0001f) {
            dir = Vector2.down;
        }
        dir.Normalize();
        // Clear of stick (left) and buttons (right) on phone; stay in the play area.
        UiMode mode = UiFit.Mode;
        float edgeX = mode == UiMode.Desktop ? 0.40f : (mode == UiMode.Tall ? 0.30f : 0.32f);
        float edgeY = mode == UiMode.Tall ? 0.26f : 0.34f;
        float scale = Mathf.Min(edgeX / Mathf.Max(0.001f, Mathf.Abs(dir.x)), edgeY / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
        Vector2 pos = new Vector2(0.5f, 0.5f) + dir * scale;
        if (mode == UiMode.Tall) {
            pos.y = Mathf.Clamp(pos.y, 0.24f, 0.72f);
            pos.x = Mathf.Clamp(pos.x, 0.18f, 0.82f);
        } else if (mode != UiMode.Desktop) {
            pos.y = Mathf.Clamp(pos.y, 0.20f, 0.82f);
            pos.x = Mathf.Clamp(pos.x, 0.18f, 0.82f);
        }
        arrowRect.anchorMin = pos;
        arrowRect.anchorMax = pos;
        arrowRect.anchoredPosition = Vector2.zero;
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 7f);
        arrow.color = new Color(Accent.r, Accent.g, Accent.b, pulse);
        arrow.gameObject.SetActive(true);
        ArrowVisible = true;
        LastArrowAnchor = pos;
        if (Events.Count < 80 || !Events[Events.Count - 1].StartsWith("callout-arrow")) {
            Note("callout-arrow", "anchor " + pos.x.ToString("0.00") + "," + pos.y.ToString("0.00"));
        }
    }

    void TickCaption() {
        if (caption == null) {
            return;
        }
        if (string.IsNullOrEmpty(LastWord) || markLeft <= 0f) {
            caption.enabled = false;
            return;
        }
        // One word only (strip trailing period for display? keep the period — clipped).
        caption.text = LastWord;
        caption.enabled = true;
        caption.color = Accent;
        LastCaptionRect = RectTransformToScreen(caption.rectTransform);
    }

    void HideMarks() {
        markLeft = 0f;
        marked = null;
        MarkerVisible = false;
        ArrowVisible = false;
        if (ring != null) {
            ring.gameObject.SetActive(false);
        }
        if (arrow != null) {
            arrow.gameObject.SetActive(false);
        }
        if (caption != null) {
            caption.enabled = false;
        }
    }

    Vector3 SliceAt() {
        if (Companion.Instance != null && !Companion.Instance.Downed && !musician.Away) {
            // Circle Slice is home; companion follows — threats near either still matter. Prefer circle when present.
        }
        if (Companion.Instance != null && musician.Away && !Companion.Instance.Downed) {
            return Companion.Instance.transform.position;
        }
        return musician != null ? musician.transform.position : Vector3.zero;
    }

    static Vector3 WallitAt() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    static Vector3 WallitForward() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) {
            return Vector3.forward;
        }
        Vector3 f = player.transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
    }

    static float StabNear() {
        return Musician.StabReach + 1.5f;
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    static Rect RectTransformToScreen(RectTransform rt) {
        if (rt == null) {
            return Rect.zero;
        }
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float xMin = corners[0].x, xMax = corners[2].x, yMin = corners[0].y, yMax = corners[2].y;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    void BuildRing() {
        ring = MakeAccentRing("Callout Ring", 0.12f, 0.85f);
    }

    void BuildShock() {
        shock = MakeAccentRing("Callout Shock", 0.22f, 2.5f);
    }

    static LineRenderer MakeAccentRing(string name, float width, float radius) {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.positionCount = 48;
        lr.material = new Material(SafeShader.Find("Sprites/Default"));
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        for (int i = 0; i < 48; i++) {
            float ang = i / 48f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
        }
        go.SetActive(false);
        return lr;
    }

    void BuildUi() {
        var canvasGo = new GameObject("Slice Callouts");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 152;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

        var capGo = new GameObject("Callout Word");
        capGo.transform.SetParent(canvasGo.transform, false);
        caption = capGo.AddComponent<Text>();
        caption.font = UiSkin.Font(UiSkin.FontRole.Label);
        caption.fontSize = 34;
        caption.fontStyle = FontStyle.Bold;
        caption.alignment = TextAnchor.MiddleCenter;
        caption.color = Accent;
        caption.raycastTarget = false;
        caption.enabled = false;
        var outline = capGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        UiFit.Apply(scaler);
        UiFit.PlaceCaption(caption.rectTransform, 240f, 0);

        var arrowGo = new GameObject("Callout Arrow");
        arrowGo.transform.SetParent(canvasGo.transform, false);
        arrow = arrowGo.AddComponent<Image>();
        arrow.raycastTarget = false;
        arrow.sprite = ArrowSprite();
        arrow.color = Accent;
        arrowRect = arrow.rectTransform;
        arrowRect.sizeDelta = new Vector2(72f, 72f);
        arrowGo.SetActive(false);
    }

    static Sprite ArrowSprite() {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(0f, 0f, 0f, 0f);
        var fill = Color.white;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                tex.SetPixel(x, y, clear);
            }
        }
        // Simple chevron pointing +X (rotation aims it).
        for (int y = 8; y < size - 8; y++) {
            float t = Mathf.Abs(y - size * 0.5f) / (size * 0.5f - 8f);
            int tip = Mathf.RoundToInt(Mathf.Lerp(size - 10, size * 0.45f, t));
            for (int x = tip - 6; x <= tip; x++) {
                if (x >= 0 && x < size) {
                    tex.SetPixel(x, y, fill);
                }
            }
        }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
