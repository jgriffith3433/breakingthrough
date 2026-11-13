using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// In-fight barks for the six campaign bosses (Docs/Story.md, "Boss barks"). Each boss has three short lines:
// phase change, taking a home run, and defeating Wallit (or striking the circle when Slice falls). Lines play through
// VoiceDirector (reasons boss-phase / boss-homerun / boss-win) so they never overlap Wallit, Slice or another cast line.
// Captions sit in the upper third via UiFit.PlaceCaption. Bot-family bosses (Courier, Plated Alpha, Foreman) get a light
// robotic high-pass on the mixer source; spider bosses play clean. Voice files: Resources/Voice/<castid>_<slug>.mp3.
public class BossVoice : MonoBehaviour {
    public static readonly string[] CourierPhase = { "Second notice! Rush delivery!" };
    public static readonly string[] CourierHome = { "Air mail! Whoa!" };
    public static readonly string[] CourierWin = { "Return to sender. Done!" };

    public static readonly string[] PlatedPhase = { "Stamp: VOID. Continue." };
    public static readonly string[] PlatedHome = { "Impact logged." };
    public static readonly string[] PlatedWin = { "Address: INVALID." };

    public static readonly string[] ForemanPhase = { "Overtime. Read louder." };
    public static readonly string[] ForemanHome = { "That ain't billable!" };
    public static readonly string[] ForemanWin = { "Shift closed. Cleared." };

    public static readonly string[] ServerPhase = { "Final offer. Please sign." };
    public static readonly string[] ServerHome = { "Delivery delayed. Briefly." };
    public static readonly string[] ServerWin = { "Served. Kindly remain." };

    public static readonly string[] RegistrarPhase = { "Page two. Initial here." };
    public static readonly string[] RegistrarHome = { "Margin note: struck." };
    public static readonly string[] RegistrarWin = { "Filed. Case closed." };

    public static readonly string[] OrderPhase = { "The seal turns." };
    public static readonly string[] OrderHome = { "The order... holds." };
    public static readonly string[] OrderWin = { "Address. Struck." };

    static BossVoice instance;
    public static int Spoken { get; private set; }
    public static readonly HashSet<string> Heard = new HashSet<string>();
    public static readonly List<string> Events = new List<string>();
    public static int CaptionTopFracTimes100 { get; private set; }

    AudioSource source;
    AudioHighPassFilter highPass;
    Text text;
    CanvasScaler scaler;
    float lineEnd;
    int stamp = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        Spoken = 0;
        Heard.Clear();
        Events.Clear();
        waiting.Clear();
        CaptionTopFracTimes100 = 0;
    }

    static BossVoice Ensure() {
        if (instance == null) {
            var go = new GameObject("Boss Voice");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BossVoice>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.spatialBlend = 0f;
            instance.source.ignoreListenerPause = true;
            instance.highPass = go.AddComponent<AudioHighPassFilter>();
            instance.highPass.enabled = false;
            instance.highPass.cutoffFrequency = 900f;
            instance.highPass.highpassResonanceQ = 0.7f;
            instance.BuildCaption();
        }
        return instance;
    }

    // ---------------------------------------------------------------- hooks

    public static void OnPhase(Enemy boss) {
        SpeakFor(boss, "boss-phase", VoiceDirector.Priority.Key);
    }

    public static void OnHomeRun(Enemy boss) {
        SpeakFor(boss, "boss-homerun", VoiceDirector.Priority.Normal);
    }

    public static void OnWallitDown(Enemy boss) {
        SpeakFor(boss, "boss-win", VoiceDirector.Priority.Key);
    }

    // Live boss on the field, if any (for Slice-down / Wallit-down).
    public static Enemy LiveBoss() {
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy e = Enemy.Roster[i];
            if (e != null && e.Alive && e.Boss) {
                return e;
            }
        }
        return null;
    }

    static void SpeakFor(Enemy boss, string why, VoiceDirector.Priority priority) {
        if (boss == null || !boss.Boss) {
            return;
        }
        string code = CodeOf(boss);
        if (code == null) {
            return;
        }
        string[] bank = BankOf(code, why);
        if (bank == null || bank.Length == 0) {
            return;
        }
        string line = bank[0];
        if (!VoiceDirector.Allow(line, why, priority) || !WallitVoice.FloorFree) {
            // A busy floor (Wallit, Slice or the last boss still talking) must not cost the boss its line: hold it and
            // say it the moment the floor is free (a scene or the voices-off setting still drops it).
            Events.Add("hold|" + code + "|" + why + "|" + line);
            Ensure().Hold(boss, why, priority);
            return;
        }
        waiting.Remove(why);
        Ensure().Play(code, line, why, priority, YoFamily(code));
    }

    struct Waiting {
        public Enemy Boss;
        public VoiceDirector.Priority Priority;
        public float Until;
    }

    static readonly Dictionary<string, Waiting> waiting = new Dictionary<string, Waiting>();
    static readonly List<string> waitKeys = new List<string>();

    void Hold(Enemy boss, string why, VoiceDirector.Priority priority) {
        if (waiting.ContainsKey(why)) {
            return;   // already waiting: the first deadline stands
        }
        Waiting w;
        w.Boss = boss;
        w.Priority = priority;
        w.Until = Time.unscaledTime + (priority == VoiceDirector.Priority.Key ? 6f : 3f);
        waiting[why] = w;
    }

    void TickWaiting() {
        if (waiting.Count == 0) {
            return;
        }
        waitKeys.Clear();
        foreach (string k in waiting.Keys) {
            waitKeys.Add(k);
        }
        for (int i = 0; i < waitKeys.Count; i++) {
            string why = waitKeys[i];
            Waiting w = waiting[why];
            if (w.Boss == null || !w.Boss.Alive || Time.unscaledTime > w.Until) {
                waiting.Remove(why);
                continue;
            }
            if (VoiceDirector.Scene || !WallitVoice.FloorFree) {
                continue;
            }
            waiting.Remove(why);
            SpeakFor(w.Boss, why, w.Priority);
        }
    }

    public static string CodeOf(Enemy boss) {
        if (boss == null) {
            return null;
        }
        string t = boss.Title ?? "";
        if (boss.IsCourier || t.IndexOf("Courier") >= 0) {
            return "K";
        }
        if (t.IndexOf("Plated") >= 0) {
            return "P";
        }
        if (t.IndexOf("Foreman") >= 0) {
            return "F";
        }
        if (t.IndexOf("Server") >= 0) {
            return "V";
        }
        if (t.IndexOf("Registrar") >= 0) {
            return "R";
        }
        if (t.IndexOf("Order") >= 0) {
            return "O";
        }
        return null;
    }

    static bool YoFamily(string code) {
        return code == "K" || code == "P" || code == "F";
    }

    static string[] BankOf(string code, string why) {
        switch (code) {
            case "K":
                return why == "boss-phase" ? CourierPhase : why == "boss-homerun" ? CourierHome : CourierWin;
            case "P":
                return why == "boss-phase" ? PlatedPhase : why == "boss-homerun" ? PlatedHome : PlatedWin;
            case "F":
                return why == "boss-phase" ? ForemanPhase : why == "boss-homerun" ? ForemanHome : ForemanWin;
            case "V":
                return why == "boss-phase" ? ServerPhase : why == "boss-homerun" ? ServerHome : ServerWin;
            case "R":
                return why == "boss-phase" ? RegistrarPhase : why == "boss-homerun" ? RegistrarHome : RegistrarWin;
            case "O":
                return why == "boss-phase" ? OrderPhase : why == "boss-homerun" ? OrderHome : OrderWin;
            default:
                return null;
        }
    }

    void Play(string code, string line, string why, VoiceDirector.Priority priority, bool robotic) {
        Cast.Member m = Cast.Find(code);
        string who = m != null ? m.id : "boss";
        string name = m != null && !string.IsNullOrEmpty(m.name) ? m.name : who.ToUpperInvariant();
        string file = who + "_" + SliceVoice.LineId(line).Substring(6);
        AudioClip clip = OnDemand.Audio("Voice/" + file);
        if (clip == null) {
            clip = Resources.Load<AudioClip>("Voice/" + file);
        }
        float len = clip != null ? clip.length : WallitVoice.EstimateSeconds(line);
        highPass.enabled = robotic;
        if (clip != null && SoundSettings.EffectsLevel > 0f) {
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(SoundSettings.EffectsLevel);
            source.Play();
        }
        WallitVoice.Claim(who, len);
        VoiceDirector.Spoke(line, why, priority);
        Spoken++;
        Heard.Add(file);
        Events.Add("bark|" + who + "|" + why + "|" + (clip != null ? "voice" : "subtitle") + "|" + line);
        if (text != null) {
            text.text = name + "  " + line;
            text.enabled = true;
            lineEnd = Time.unscaledTime + Mathf.Max(1.6f, len);
            ReportCaption();
        }
    }

    void BuildCaption() {
        var canvasGo = new GameObject("Boss Caption");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);
        canvasGo.AddComponent<GraphicRaycaster>().enabled = false;
        text = new GameObject("Line").AddComponent<Text>();
        text.transform.SetParent(canvasGo.transform, false);
        text.font = UiSkin.Font(UiSkin.FontRole.Label);
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.92f, 0.78f, 1f);
        text.raycastTarget = false;
        text.enabled = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        UiFit.PlaceCaption(text.rectTransform, 520f, 0);
    }

    void ReportCaption() {
        if (text == null || !text.enabled) {
            return;
        }
        Vector3[] corners = new Vector3[4];
        text.rectTransform.GetWorldCorners(corners);
        float midY = (corners[0].y + corners[1].y) * 0.5f;
        float frac = 1f - (midY / Mathf.Max(1f, Screen.height));
        CaptionTopFracTimes100 = Mathf.RoundToInt(frac * 100f);
    }

    void Update() {
        TickWaiting();
        if (stamp != UiFit.CaptionStamp && scaler != null && text != null) {
            stamp = UiFit.CaptionStamp;
            UiFit.Apply(scaler);
            // Upper third on phone: PlaceCaption with a high desktop Y still anchors to the top band on Tall/Wide.
            UiFit.PlaceCaption(text.rectTransform, 520f, 0);
            ReportCaption();
        }
        if (text != null && text.enabled && Time.unscaledTime >= lineEnd) {
            text.enabled = false;
        }
    }
}
