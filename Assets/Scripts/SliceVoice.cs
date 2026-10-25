using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Slice's personality. She is cool and clipped: a few words, never a joke about work. She talks at wave starts, kills,
// near-misses, when she is hurt and when she is low, and now and then trades a short exchange with Wallit.
// Every line shows as a subtitle; if Resources/Voice/<id>.mp3 exists (Hawkes Gen audio.voice, see audio.json) it plays too.
// Her body language is procedural on top of the idle clip (the rig has no more clips): head scans, a knife flip, a
// ready lean at a wave start, a flourish on a kill, a glance and step back on a near-miss, a stagger when hurt, a hunch
// when low. Musician.LateUpdate calls ApplyPose after the Animator has posed her.
public class SliceVoice : MonoBehaviour {
    public static SliceVoice Instance { get; private set; }

    public const int LowHealth = 30;

    // Lines. Ids are the audio.json keys / Resources/Voice file names.
    static readonly string[] Idle = { "Quiet.", "Waiting.", "Still here.", "Sharp enough.", "Hm.", "Come on." };
    static readonly string[] Wave = { "Here they come.", "Again.", "Eyes up.", "Incoming.", "Stay close." };
    static readonly string[] BossWave = { "That one's big.", "Don't miss." };
    static readonly string[] Kill = { "Clean.", "Done.", "Next.", "Tidy.", "One less." };
    static readonly string[] Hurt = { "Tch.", "Careful.", "A scratch.", "Rude.", "Not bad." };
    static readonly string[] Low = { "Getting thin.", "Still standing.", "Wallit. Now." };
    static readonly string[] Near = { "Close.", "Too close.", "Noted.", "Barely." };

    // Banter: speaker (W Wallit, S Slice) and line. Wallit talks money; Slice answers short.
    static readonly string[][] Banter = {
        new[] { "W|Pay up, bots.", "S|They can't hear you.", "W|They'll feel it." },
        new[] { "S|Behind you.", "W|I know. Pay up!", "S|Clean." },
        new[] { "W|Nobody touches her.", "S|I'm armed.", "W|Still mine to guard." },
        new[] { "W|Coins. Bolts. Mine.", "S|Take them.", "W|Pay up." },
        new[] { "W|You good, Slice?", "S|Fine. Keep swinging." },
    };

    // Behaviors she can show (distinct names are what the playtest counts).
    public const string BScan = "scan";
    public const string BFlip = "knife-flip";
    public const string BReady = "ready";
    public const string BFlourish = "flourish";
    public const string BGlance = "glance";
    public const string BStagger = "stagger";
    public const string BHunch = "hunch";
    public const string BBanter = "banter-turn";

    // Playtest and diagnostics: everything she said or did, in order ("kind|detail").
    public static readonly List<string> Events = new List<string>();
    public readonly HashSet<string> Behaviors = new HashSet<string>();
    public int LinesSpoken { get; private set; }
    public int Exchanges { get; private set; }
    public int BackToBack { get; private set; }
    public string LastLine { get; private set; } = "";
    public bool BanterRunning { get { return banterRunning; } }

    Text sliceText;
    Text wallitText;
    CanvasScaler scaler;
    int stamp = -1;
    float sliceShown;
    float wallitShown;
    float nextLineAt;
    float nextIdleAt = 9f;
    float nextBanterAt = 20f;
    bool lowSaid;
    int killsSince;
    float lastKillLineAt = -99f;
    float lastNearAt = -99f;
    int lastHealth = -1;
    AudioSource audioOut;

    string behavior = "";
    float behaviorT;
    float behaviorLen;
    float turnTo;
    bool banterRunning;
    Transform head;
    Transform spine;
    Transform knifeBone;
    float hunch;
    float lastReadyAt = -99f;

    public static void Ensure(Musician owner) {
        if (owner != null && Instance == null) {
            owner.gameObject.AddComponent<SliceVoice>();
        }
    }

    void Awake() {
        Instance = this;
        Events.Clear();
        audioOut = gameObject.AddComponent<AudioSource>();
        audioOut.playOnAwake = false;
        audioOut.spatialBlend = 0f;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Start() {
        BuildSubtitles();
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman) {
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        }
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) {
            if (all[i].name == "Knife") {
                knifeBone = all[i];
                break;
            }
        }
        lastHealth = Musician.Instance != null ? Musician.Instance.Health : -1;
        Invoke(nameof(OpeningWave), 1.5f);
    }

    // Wave 1 may start before she exists: greet it if no wave start was seen yet.
    void OpeningWave() {
        if (lastReadyAt < 0f) {
            Ready(WaveDirector.Wave);
        }
    }

    // ---------------------------------------------------------------- hooks

    public static void OnWave(int wave) {
        if (Instance == null) {
            return;
        }
        Instance.Ready(wave);
    }

    public static void OnKill(Enemy foe) {
        if (Instance != null && foe != null) {
            Instance.Killed(foe);
        }
    }

    void Ready(int wave) {
        if (Time.time - lastReadyAt < 2f) {
            return;
        }
        lastReadyAt = Time.time;
        Begin(BReady, 1.3f);
        string[] bank = wave % 10 == 0 ? BossWave : Wave;
        Say(bank, "wave", 2, true);
    }

    void Killed(Enemy foe) {
        Musician slice = Musician.Instance;
        if (slice == null || !slice.Alive) {
            return;
        }
        float gap = (foe.transform.position - slice.transform.position).magnitude;
        bool hers = slice.LastStabbed == foe && Time.time - slice.LastStabAt < 0.6f;
        if (!hers && gap < 3.2f && Time.time - lastNearAt > 7f) {
            lastNearAt = Time.time;
            LookAtWallit();
            Begin(BGlance, 1.1f);
            Say(Near, "near-miss", 2, false);
            return;
        }
        killsSince++;
        if (hers || (killsSince >= 4 && Time.time - lastKillLineAt > 6f)) {
            killsSince = 0;
            lastKillLineAt = Time.time;
            Begin(BFlourish, 0.7f);
            Say(Kill, "kill", 1, false);
        }
    }

    // ---------------------------------------------------------------- tick

    void Update() {
        Musician slice = Musician.Instance;
        if (slice == null || Time.deltaTime <= 0f) {
            return;
        }
        float dt = Time.deltaTime;
        if (stamp != UiFit.Stamp && scaler != null) {
            stamp = UiFit.Stamp;
            UiFit.Apply(scaler);
        }
        sliceShown -= dt;
        wallitShown -= dt;
        if (sliceText != null) {
            sliceText.enabled = sliceShown > 0f;
        }
        if (wallitText != null) {
            wallitText.enabled = wallitShown > 0f;
        }
        if (behavior.Length > 0) {
            behaviorT += dt;
            if (behaviorT >= behaviorLen) {
                behavior = "";
            }
        }

        if (!slice.Alive) {
            hunch = Mathf.MoveTowards(hunch, 0f, dt * 2f);
            return;
        }
        if (slice.Health < lastHealth) {
            Begin(BStagger, 0.6f);
            if (slice.Health > LowHealth) {
                Say(Hurt, "hurt", 3, true);
            }
        }
        lastHealth = slice.Health;
        bool low = slice.Health <= LowHealth;
        hunch = Mathf.MoveTowards(hunch, low ? 1f : 0f, dt * 1.5f);
        if (low && !lowSaid) {
            lowSaid = true;
            Note("behavior", BHunch);
            Behaviors.Add(BHunch);
            Say(Low, "low", 3, true);
        } else if (!low && slice.Health > LowHealth + 25) {
            lowSaid = false;
        }

        bool quiet = Enemy.Roster.Count == 0 || slice.QuietFor > 5f;
        if (quiet && !banterRunning && behavior.Length == 0 && Time.time >= nextIdleAt && Time.time >= nextLineAt) {
            nextIdleAt = Time.time + Random.Range(10f, 16f);
            if (Random.value < 0.5f) {
                Begin(BScan, 3.2f);
            } else {
                Begin(BFlip, 0.7f);
            }
            if (Random.value < 0.6f) {
                Say(Idle, "idle", 1, false);
            }
        }
        if (!banterRunning && quiet && !low && Time.time >= nextBanterAt && Time.time >= nextLineAt) {
            StartCoroutine(TradeLines());
        }
    }

    System.Collections.IEnumerator TradeLines() {
        banterRunning = true;
        string[] pair = Banter[Random.Range(0, Banter.Length)];
        LookAtWallit();
        Begin(BBanter, 1.2f + pair.Length * 1.9f);
        int spoken = 0;
        for (int i = 0; i < pair.Length; i++) {
            string[] bits = pair[i].Split('|');
            bool wallit = bits[0] == "W";
            float spoke = Show(wallit, bits[1], "banter");
            spoken++;
            float wait = Mathf.Max(1.9f, spoke + 0.4f);
            while (wait > 0f) {
                wait -= Time.deltaTime;
                yield return null;
            }
        }
        Exchanges++;
        Note("banter", "exchange " + Exchanges + " (" + spoken + " lines)");
        nextBanterAt = Time.time + 75f;
        banterRunning = false;
    }

    // ---------------------------------------------------------------- speaking

    // Picks a line from the bank that is not the last one said (by anyone, any bank): no repeats back to back.
    void Say(string[] bank, string why, int priority, bool force) {
        if (banterRunning) {
            return;
        }
        if (!force && Time.time < nextLineAt) {
            return;
        }
        // One voice at a time: not over Wallit.
        if (!WallitVoice.FloorFree && WallitVoice.FloorOwner != "slice") {
            return;
        }
        string line = bank[Random.Range(0, bank.Length)];
        int guard = 0;
        while (line == LastLine && guard++ < 8) {
            line = bank[Random.Range(0, bank.Length)];
        }
        if (line == LastLine) {
            return;
        }
        nextLineAt = Time.time + 3.2f;
        Show(false, line, why);
    }

    // Returns how long the line takes to say.
    float Show(bool wallit, string line, string why) {
        if (line == LastLine) {
            BackToBack++;
        }
        LastLine = line;
        LinesSpoken++;
        string id = LineId(line);
        if (wallit && WallitVoice.Instance != null) {
            // His own voice, mouth and subtitle (WallitVoice); the log keeps the exchange in order.
            float len = WallitVoice.Banter(line);
            Note("wallit", why + "|" + line);
            return len;
        }
        if (wallit) {
            wallitShown = 2.4f;
            if (wallitText != null) {
                wallitText.text = "WALLIT  " + line;
            }
        } else {
            sliceShown = 2.4f;
            if (sliceText != null) {
                sliceText.text = "SLICE  " + line;
            }
            AudioClip clip = Resources.Load<AudioClip>("Voice/" + id);
            if (clip != null && audioOut != null && SoundSettings.EffectsLevel > 0f) {
                audioOut.PlayOneShot(clip, SoundSettings.EffectsLevel);
            }
            float len = clip != null ? clip.length : WallitVoice.EstimateSeconds(line);
            WallitVoice.Claim("slice", len);
            Note("line", why + "|" + line);
            WallitVoice.OnSliceSaid(why);
            return len;
        }
        Note("wallit", why + "|" + line);
        return WallitVoice.EstimateSeconds(line);
    }

    // "Here they come." -> slice_here_they_come (audio.json keys / Resources/Voice file names)
    public static string LineId(string line) {
        var sb = new System.Text.StringBuilder("slice_");
        bool gap = false;
        for (int i = 0; i < line.Length; i++) {
            char c = char.ToLowerInvariant(line[i]);
            if (char.IsLetterOrDigit(c)) {
                if (gap && sb.Length > 6) {
                    sb.Append('_');
                }
                gap = false;
                sb.Append(c);
            } else {
                gap = true;
            }
        }
        return sb.ToString();
    }

    void Note(string kind, string detail) {
        Events.Add(kind + "|" + detail);
    }

    // ---------------------------------------------------------------- body language

    void Begin(string name, float length) {
        behavior = name;
        behaviorT = 0f;
        behaviorLen = length;
        Behaviors.Add(name);
        Note("behavior", name);
    }

    void LookAtWallit() {
        var player = FindAnyObjectByType<CharacterMovement>();
        Musician slice = Musician.Instance;
        if (player == null || slice == null) {
            turnTo = 0f;
            return;
        }
        Vector3 to = player.transform.position - slice.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) {
            turnTo = 0f;
            return;
        }
        float angle = Vector3.SignedAngle(slice.transform.forward, to, Vector3.up);
        turnTo = Mathf.Clamp(angle, -60f, 60f);
    }

    // Called by Musician.LateUpdate after the Animator and her turn: adds the behavior on top.
    public void ApplyPose(Transform body) {
        if (body == null) {
            return;
        }
        float k = behavior.Length > 0 && behaviorLen > 0f ? behaviorT / behaviorLen : 0f;
        float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
        float headYaw = 0f;
        float lean = 0f;
        float spin = 0f;
        float bodyYaw = 0f;
        Vector3 shift = Vector3.zero;
        switch (behavior) {
            case BScan:
                headYaw = Mathf.Sin(k * Mathf.PI * 2f) * 40f * env;
                break;
            case BFlip:
                spin = k * 360f;
                break;
            case BReady:
                lean = 9f * env;
                headYaw = -6f * env;
                break;
            case BFlourish:
                spin = k * 360f;
                headYaw = 10f * env;
                break;
            case BGlance:
                bodyYaw = turnTo * env;
                headYaw = turnTo * 0.4f * env;
                shift = -body.forward * 0.18f * env;
                break;
            case BStagger:
                lean = -12f * env;
                shift = -body.forward * 0.12f * env;
                break;
            case BBanter:
                bodyYaw = turnTo * Mathf.Clamp01(Mathf.Min(k * 8f, (1f - k) * 8f));
                break;
        }
        if (hunch > 0.01f) {
            lean += (14f + Mathf.Sin(Time.time * 3f) * 2.5f) * hunch;
        }
        if (Mathf.Abs(bodyYaw) > 0.01f) {
            body.localRotation = Quaternion.AngleAxis(bodyYaw, Vector3.up) * body.localRotation;
        }
        if (shift != Vector3.zero) {
            body.position += shift;
        }
        if (spine != null && Mathf.Abs(lean) > 0.01f) {
            spine.rotation = Quaternion.AngleAxis(lean, body.right) * spine.rotation;
        }
        if (head != null && Mathf.Abs(headYaw) > 0.01f) {
            head.rotation = Quaternion.AngleAxis(headYaw, Vector3.up) * head.rotation;
        }
        if (knifeBone != null && Mathf.Abs(spin) > 0.01f) {
            knifeBone.localRotation = knifeBone.localRotation * Quaternion.AngleAxis(spin, Vector3.up);
        }
    }

    // ---------------------------------------------------------------- subtitles

    void BuildSubtitles() {
        var canvasGo = new GameObject("Slice Subtitles");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        wallitText = MakeLine(canvasGo.transform, font, 190f, new Color(0.95f, 0.8f, 0.4f));
        sliceText = MakeLine(canvasGo.transform, font, 140f, new Color(0.86f, 0.9f, 1f));
        stamp = UiFit.Stamp;
        UiFit.Apply(scaler);
    }

    static Text MakeLine(Transform parent, Font font, float y, Color color) {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.enabled = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1000f, 44f);
        return text;
    }
}
