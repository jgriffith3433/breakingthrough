using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Wallit talks. Brash, angry, always about money ("Pay up."), never a joke about work. He barks at kills and streaks,
// big hits and home runs, when he is hurt, at wave starts and level-ups, trades lines with Slice, and speaks the
// story beats' Wallit lines. Every line shows as a subtitle; if Resources/Voice/<id>.mp3 exists (Hawkes Gen audio.voice,
// ids in Resources/Voice/audio.json) it plays too, and its loudness drives WallitMouth. With no file the mouth runs a
// syllable rhythm for as long as the subtitle shows, so he never talks with a shut mouth.
// One voice at a time: a line waits for the floor (Slice's line, his own) and is dropped if it waits too long, so
// Wallit never talks over Slice and Slice never over him. No line repeats back to back. Runs on unscaled time: the
// level-up card freezes the game and he still cheers.
public class WallitVoice : MonoBehaviour {
    public static WallitVoice Instance { get; private set; }

    // Lines. Ids are the audio.json keys / Resources/Voice file names (wallit_<line>).
    public static readonly string[] Kill = { "Pay up.", "Paid in full.", "Cha-ching.", "Next!", "Exact change.", "Keep the change.", "Sold!", "No refunds." };
    public static readonly string[] Streak = { "Streak! Tip jar's full!", "Cash rain!", "Count 'em!", "Jackpot!", "Compound interest!", "Don't stop, don't stop!" };
    public static readonly string[] Big = { "Interest due!", "Collected!", "Feel that?", "That's the fee!", "Read the fine print!", "Smash and grab!", "Overdue!" };
    public static readonly string[] HomeRun = { "Home run! Pay up!", "Out of the park!", "Gone! No refund!", "Grand slam!", "Returned to sender!" };
    public static readonly string[] Hurt = { "Ow! That's extra!", "Hey! Watch the leather!", "You'll pay for that!", "Ow. Charging you.", "Rude!", "Bruised my billfold!", "Hands off!", "That's going on your tab!" };
    public static readonly string[] Wave = { "Here we go. Pay up.", "Line up, bots.", "Open for business.", "Cash only!", "Round two. Bring cash.", "Come on, then!", "Step up, pay up.", "Ready to collect." };
    public static readonly string[] LevelUp = { "Level up! Raise!", "Profit!", "Upgrade me. I'm good for it.", "Bonus time!", "Now we're rich." };
    public static readonly string[] Chest = { "Treasure! Mine!", "Open it! Open it!", "Finders keepers!" };
    public static readonly string[] SliceLow = { "Slice! Hold on!", "I'm coming!", "Not on my tab!", "Nobody collects her!" };
    public static readonly string[] BossWave = { "Big bill. Big payday.", "Good. Bigger tip." };
    public static readonly string[] Golden = { "Gold! Mine!", "Shiny. Mine.", "That one pays double." };

    // Lines Wallit says in Slice's banter (SliceVoice.Banter), voiced here so they get a mouth and a file too.
    public static readonly string[] BanterLines = { "Pay up, bots.", "They'll feel it.", "I know. Pay up!", "Nobody touches her.", "Still mine to guard.", "Coins. Bolts. Mine.", "Pay up.", "You good, Slice?" };

    // Playtest and diagnostics: everything he said ("kind|why|line|seconds").
    public static readonly List<string> Events = new List<string>();
    public static int LinesSpoken { get; private set; }
    public static int BackToBack { get; private set; }
    public static int Overlaps { get; private set; }
    public static int Dropped { get; private set; }
    public static string LastLine { get; private set; } = "";
    public static readonly Dictionary<string, int> ByReason = new Dictionary<string, int>();

    // The shared floor (unscaled time): who is speaking and until when.
    public static string FloorOwner { get; private set; } = "";
    public static float FloorUntil { get; private set; }
    public static bool FloorFree { get { return Time.unscaledTime >= FloorUntil; } }

    // 0..1 how open the mouth should be right now (read by WallitMouth), and whether he is mid line.
    public static float MouthOpen { get; private set; }
    public static bool Speaking { get; private set; }
    public static string Current { get; private set; } = "";

    struct Pending {
        public string Line;
        public string Why;
        public float Expires;
    }

    const float Gap = 0.35f;
    readonly List<Pending> pending = new List<Pending>();
    static readonly Dictionary<string, float[]> envelopes = new Dictionary<string, float[]>();
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    string recent1 = "";
    string recent2 = "";
    AudioSource audioOut;
    float[] envelope;
    float lineStart;
    float lineLength;
    AudioClip lineClip;
    bool lineHasFile;
    float shown;
    Text text;
    CanvasScaler scaler;
    int stamp = -1;
    float smooth;
    float liveMax;
    readonly float[] liveBuf = new float[256];
    float nextKillAt;
    int killsSince;
    float nextHurtAt;
    float nextBigAt;
    float nextStreakAt;
    float nextWaveAt;

    // Wallit's model is built by SetupWallit, which also adds WallitMouth; this finds or adds both for the live player.
    public static void Ensure(GameObject player) {
        if (player == null) {
            return;
        }
        if (Instance == null) {
            Instance = player.GetComponent<WallitVoice>();
            if (Instance == null) {
                Instance = player.AddComponent<WallitVoice>();
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Events.Clear();
        ByReason.Clear();
        LinesSpoken = 0;
        BackToBack = 0;
        Overlaps = 0;
        Dropped = 0;
        LastLine = "";
        FloorUntil = 0f;
        FloorOwner = "";
        MouthOpen = 0f;
        Speaking = false;
        Instance = null;
    }

    void Awake() {
        Instance = this;
        Events.Clear();
        ByReason.Clear();
        LinesSpoken = 0;
        BackToBack = 0;
        Overlaps = 0;
        Dropped = 0;
        LastLine = "";
        audioOut = gameObject.AddComponent<AudioSource>();
        audioOut.playOnAwake = false;
        audioOut.spatialBlend = 0f;
        BuildSubtitle();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
            Speaking = false;
            MouthOpen = 0f;
        }
    }

    // ---------------------------------------------------------------- the floor

    // Slice (or anyone else) takes the floor for a line. A claim over somebody else's line is counted as an overlap.
    public static void Claim(string who, float seconds) {
        float now = Time.unscaledTime;
        if (now < FloorUntil - 0.05f && FloorOwner != who) {
            Overlaps++;
            Events.Add("overlap|" + FloorOwner + " vs " + who + "|" + who + "|0");
        }
        FloorOwner = who;
        FloorUntil = now + seconds + Gap;
    }

    // How long a line takes when there is no recording: about 13 letters a second plus a beat.
    public static float EstimateSeconds(string line) {
        return Mathf.Clamp(0.5f + line.Length * 0.075f, 1.1f, 6f);
    }

    public static float FloorLeft { get { return Mathf.Max(0f, FloorUntil - Time.unscaledTime); } }

    // ---------------------------------------------------------------- hooks

    public static void OnKill(Enemy foe, int combo) {
        if (Instance == null || foe == null) {
            return;
        }
        Instance.Killed(foe, combo);
    }

    public static void OnBigHit(string what) {
        if (Instance != null && Time.unscaledTime >= Instance.nextBigAt) {
            Instance.nextBigAt = Time.unscaledTime + 5f;
            Instance.Queue(Instance.Pick(Big), what, 1.2f);
        }
    }

    public static void OnHomeRun() {
        if (Instance != null) {
            Instance.nextBigAt = Time.unscaledTime + 5f;
            Instance.Queue(Instance.Pick(HomeRun), "home-run", 1.5f);
        }
    }

    public static void OnHurt() {
        if (Instance != null && Time.unscaledTime >= Instance.nextHurtAt) {
            Instance.nextHurtAt = Time.unscaledTime + 4f;
            Instance.Queue(Instance.Pick(Hurt), "hurt", 1.2f);
        }
    }

    public static void OnWave(int wave) {
        if (Instance == null || Time.unscaledTime < Instance.nextWaveAt) {
            return;
        }
        Instance.nextWaveAt = Time.unscaledTime + 3f;
        if (Story.HasWallitBeat(wave)) {
            return;
        }
        Instance.Queue(Instance.Pick(wave % 10 == 0 ? BossWave : Wave), "wave", 4.5f);
    }

    public static void OnLevelUp(bool chest) {
        if (Instance != null) {
            Instance.Queue(Instance.Pick(chest ? Chest : LevelUp), chest ? "chest" : "level-up", 3f);
        }
    }

    // Playtest setup: asks for one line (a kill bark) so a close-up can see his mouth.
    public static void Test() {
        if (Instance != null) {
            Instance.Queue(Instance.Pick(Kill), "setup", 8f);
        }
    }

    // Slice said something he answers: low health gets a worried reply, a boss wave a greedy one.
    public static void OnSliceSaid(string why) {
        if (Instance == null) {
            return;
        }
        if (why == "low") {
            Instance.Queue(Instance.Pick(SliceLow), "slice-low", 4f);
        }
    }

    // A Wallit line of a story beat: spoken when the floor is free, within a few seconds of its subtitle.
    public static void OnBeat(string line) {
        if (Instance != null) {
            Instance.Queue(line, "beat", 4f);
        }
    }

    // A line of Slice's banter: he speaks it now, whatever is queued. Returns how long it takes.
    public static float Banter(string line) {
        if (Instance == null) {
            return 0f;
        }
        return Instance.Speak(line, "banter");
    }

    void Killed(Enemy foe, int combo) {
        float now = Time.unscaledTime;
        killsSince++;
        if (foe.Golden || foe.Boss) {
            Queue(Pick(Golden), "golden", 1.5f);
            return;
        }
        if (combo >= 5 && combo % 5 == 0 && now >= nextStreakAt) {
            nextStreakAt = now + 8f;
            Queue(Pick(Streak), "streak", 1.5f);
            return;
        }
        if (killsSince >= 3 && now >= nextKillAt) {
            killsSince = 0;
            nextKillAt = now + 5f;
            Queue(Pick(Kill), "kill", 1.2f);
        }
    }

    // ---------------------------------------------------------------- speaking

    // A line that is not either of the last two said (by anyone: Slice's last line counts too).
    string Pick(string[] bank) {
        string slice = SliceVoice.Instance != null ? SliceVoice.Instance.LastLine : "";
        for (int i = 0; i < 12; i++) {
            string line = bank[Random.Range(0, bank.Length)];
            if (line != LastLine && line != recent1 && line != recent2 && line != slice) {
                return line;
            }
        }
        for (int i = 0; i < bank.Length; i++) {
            if (bank[i] != LastLine && bank[i] != slice) {
                return bank[i];
            }
        }
        return null;
    }

    void Queue(string line, string why, float patience) {
        if (string.IsNullOrEmpty(line)) {
            return;
        }
        // A newer bark replaces an older one for the same reason; the queue stays short.
        for (int i = pending.Count - 1; i >= 0; i--) {
            if (pending[i].Why == why) {
                pending.RemoveAt(i);
            }
        }
        if (pending.Count >= 3) {
            pending.RemoveAt(0);
            Dropped++;
        }
        pending.Add(new Pending { Line = line, Why = why, Expires = Time.unscaledTime + patience });
    }

    bool BanterRunning {
        get { return SliceVoice.Instance != null && SliceVoice.Instance.BanterRunning; }
    }

    // Speaks at once: subtitle, voice file, mouth. Takes the floor.
    float Speak(string line, string why) {
        if (line == LastLine) {
            BackToBack++;
        }
        recent2 = recent1;
        recent1 = LastLine;
        LastLine = line;
        LinesSpoken++;
        int count;
        ByReason.TryGetValue(why, out count);
        ByReason[why] = count + 1;

        lineClip = LoadClip(line);
        lineHasFile = lineClip != null;
        lineLength = lineHasFile ? lineClip.length : EstimateSeconds(line);
        envelope = lineHasFile ? EnvelopeOf(line, lineClip) : null;
        lineStart = Time.unscaledTime;
        liveMax = 0f;
        Claim("wallit", lineLength);
        Speaking = true;
        Current = line;
        if (lineHasFile && audioOut != null && SoundSettings.EffectsLevel > 0f) {
            audioOut.Stop();
            audioOut.clip = lineClip;
            audioOut.volume = SoundSettings.EffectsLevel;
            audioOut.Play();
        }
        shown = Mathf.Max(2.4f, lineLength + 0.5f);
        if (text != null && why != "beat") {
            text.text = "WALLIT  " + line;
            text.enabled = true;
        }
        Events.Add("line|" + why + "|" + line + "|" + lineLength.ToString("0.00") + (lineHasFile ? "|voice" : "|subtitle"));
        return lineLength;
    }

    // "Pay up!" -> wallit_pay_up (audio.json keys / Resources/Voice file names)
    public static string LineId(string line) {
        return "wallit_" + SliceVoice.LineId(line).Substring(6);
    }

    static AudioClip LoadClip(string line) {
        string id = LineId(line);
        AudioClip clip;
        if (!clips.TryGetValue(id, out clip)) {
            clip = Resources.Load<AudioClip>("Voice/" + id);
            clips[id] = clip;
        }
        return clip;
    }

    // Loudness of the recording in 20 ms steps, scaled so its louder parts reach 1: the mouth follows it.
    static float[] EnvelopeOf(string line, AudioClip clip) {
        string id = LineId(line);
        float[] env;
        if (envelopes.TryGetValue(id, out env)) {
            return env;
        }
        env = null;
        try {
            int channels = Mathf.Max(1, clip.channels);
            var data = new float[clip.samples * channels];
            if (clip.samples > 0 && clip.GetData(data, 0)) {
                int step = Mathf.Max(1, clip.frequency / 50);
                env = new float[clip.samples / step + 1];
                float peak = 0.0001f;
                for (int w = 0; w < env.Length; w++) {
                    double sum = 0.0;
                    int n = 0;
                    for (int s = w * step; s < (w + 1) * step && s < clip.samples; s++) {
                        float v = data[s * channels];
                        sum += v * v;
                        n++;
                    }
                    env[w] = n > 0 ? Mathf.Sqrt((float)(sum / n)) : 0f;
                    peak = Mathf.Max(peak, env[w]);
                }
                for (int w = 0; w < env.Length; w++) {
                    env[w] = Mathf.Clamp01(env[w] / (peak * 0.8f));
                }
            }
        } catch (System.Exception) {
            env = null;
        }
        envelopes[id] = env;
        return env;
    }

    // ---------------------------------------------------------------- tick

    void Update() {
        float now = Time.unscaledTime;
        if (stamp != UiFit.Stamp && scaler != null) {
            stamp = UiFit.Stamp;
            UiFit.Apply(scaler);
        }
        shown -= Time.unscaledDeltaTime;
        if (text != null && shown <= 0f) {
            text.enabled = false;
        }
        if (Speaking && now >= lineStart + lineLength) {
            Speaking = false;
            Current = "";
            if (audioOut != null && audioOut.isPlaying) {
                audioOut.Stop();
            }
        }

        // The mouth: the recording's loudness where there is one, else a syllable rhythm over the line's length.
        float target = 0f;
        if (Speaking) {
            float t = now - lineStart;
            if (envelope != null && envelope.Length > 1) {
                float f = Mathf.Clamp(t * 50f, 0f, envelope.Length - 1.001f);
                int i = (int)f;
                target = Mathf.Lerp(envelope[i], envelope[i + 1], f - i);
            } else if (lineHasFile && audioOut != null && audioOut.isPlaying && (liveMax > 0.002f || t < 0.3f)) {
                // No readable samples (compressed import): follow what is coming out of the speaker instead.
                audioOut.GetOutputData(liveBuf, 0);
                float sum = 0f;
                for (int i = 0; i < liveBuf.Length; i++) {
                    sum += liveBuf[i] * liveBuf[i];
                }
                float level = Mathf.Sqrt(sum / liveBuf.Length);
                liveMax = Mathf.Max(liveMax, level);
                target = Mathf.Clamp01(level * 7f);
            } else {
                float beat = t * 5.2f;
                float vowel = 0.45f + 0.55f * Mathf.PerlinNoise(Mathf.Floor(beat) * 1.7f, 0.3f);
                target = Mathf.Abs(Mathf.Sin(beat * Mathf.PI)) * vowel;
                float tail = lineLength - t;
                if (tail < 0.12f) {
                    target *= tail / 0.12f;
                }
            }
        }
        float rate = target > smooth ? 28f : 16f;
        smooth = Mathf.MoveTowards(smooth, target, rate * Time.unscaledDeltaTime);
        MouthOpen = smooth;

        // Dispatch: one waiting line at a time, when nobody has the floor and Slice is not trading lines with him.
        for (int i = pending.Count - 1; i >= 0; i--) {
            if (now > pending[i].Expires) {
                pending.RemoveAt(i);
                Dropped++;
            }
        }
        if (pending.Count > 0 && FloorFree && !Speaking && !BanterRunning && !WaveDirector.IsLost) {
            Pending next = pending[0];
            pending.RemoveAt(0);
            if (next.Line != LastLine) {
                Speak(next.Line, next.Why);
            } else {
                Dropped++;
            }
        }
    }

    // ---------------------------------------------------------------- subtitle

    void BuildSubtitle() {
        var canvasGo = new GameObject("Wallit Subtitles");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 151;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var go = new GameObject("Line");
        go.transform.SetParent(canvasGo.transform, false);
        text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.95f, 0.8f, 0.4f);
        text.raycastTarget = false;
        text.enabled = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 190f);
        rect.sizeDelta = new Vector2(1000f, 44f);
        stamp = UiFit.Stamp;
        UiFit.Apply(scaler);
    }
}
