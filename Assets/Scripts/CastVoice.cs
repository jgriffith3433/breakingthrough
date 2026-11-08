using System.Collections.Generic;
using UnityEngine;

// Every named speaker's voice. Wallit and Slice speak through WallitVoice / SliceVoice; everyone else (the narrator, Yo Bots, the
// Courier, the bosses...) speaks here, each in their own voice (Resources/Cast/cast.json: voiceId, direction). A line is the file
// Resources/Voice/<castid>_<slug>.mp3 (Tools/Audio/collect_lines.py lists them; slug is SliceVoice.LineId's). One voice at a time:
// the line takes the shared floor (WallitVoice.Claim) and asks VoiceDirector. In-world lines (ChapterRun.Say: the objective
// beats, the Server's offer, the companion) wait their turn in a queue and never talk over a cutscene or another line.
public class CastVoice : MonoBehaviour {
    struct Pending { public string Code, Text, Why; }

    static CastVoice instance;
    public static int Played { get; private set; }
    public static int Missing { get; private set; }
    public static int Queued { get; private set; }
    public static readonly List<string> Events = new List<string>();

    AudioSource source;
    readonly List<Pending> queue = new List<Pending>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        Played = 0;
        Missing = 0;
        Queued = 0;
        Events.Clear();
    }

    static CastVoice Ensure() {
        if (instance == null) {
            var go = new GameObject("Cast Voice");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CastVoice>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.spatialBlend = 0f;
            instance.source.ignoreListenerPause = true;
        }
        return instance;
    }

    // The voice file name for a speaker code and a line.
    public static string FileOf(string code, string text) {
        Cast.Member m = Cast.Find(code);
        string id = m != null ? m.id : "narrator";
        return id + "_" + SliceVoice.LineId(text).Substring(6);
    }

    // A cutscene line, now (Cutscene has already cleared the floor). Returns how long it takes, 0 if the floor is busy.
    public static float SayNow(string code, string text, string why) {
        return Ensure().Speak(code, text, why);
    }

    // An in-world line (Wallit/Slice/others): spoken as soon as the floor is free and no scene is up.
    public static void SayLater(string code, string text, string why) {
        if (string.IsNullOrEmpty(text)) {
            return;
        }
        CastVoice v = Ensure();
        v.queue.Add(new Pending { Code = code, Text = text, Why = why });
        Queued++;
    }

    public static void Silence() {
        if (instance != null && instance.source != null) {
            instance.source.Stop();
        }
    }

    public static bool Speaking { get { return instance != null && instance.source != null && instance.source.isPlaying; } }

    float Speak(string code, string text, string why) {
        // Wallit and Slice speak through their own live voices (mouth and gestures) where those exist. Where they don't (a
        // chapter level with no Musician, the hub) their banked clips play here like any cast line: before, a Slice line in
        // Chapter 1 or the hub reserved the floor and played nothing.
        if (code == "W" && WallitVoice.Instance != null) {
            return WallitVoice.Cutscene(text);
        }
        if (code == "S" && SliceVoice.Instance != null) {
            return SliceVoice.Cutscene(text);
        }
        Cast.Spoke(code, text);
        Cast.Member m = Cast.Find(code);
        string who = m != null ? m.id : "narrator";
        if (!WallitVoice.FloorFree || !VoiceDirector.Allow(text, why, VoiceDirector.Priority.Key)) {
            Events.Add("held|" + who + "|" + text);
            return 0f;
        }
        string file = who + "_" + SliceVoice.LineId(text).Substring(6);
        AudioClip clip = Resources.Load<AudioClip>("Voice/" + file);
        float len;
        if (clip != null && SoundSettings.EffectsLevel > 0f) {
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(SoundSettings.EffectsLevel);
            source.Play();
            len = clip.length;
        } else {
            if (clip == null) {
                Missing++;
            }
            len = WallitVoice.EstimateSeconds(text);
        }
        WallitVoice.Claim(who, len);
        VoiceDirector.Spoke(text, why, VoiceDirector.Priority.Key);
        Played++;
        Events.Add("say|" + who + "|" + (clip != null ? "voice" : "subtitle") + "|" + len.ToString("0.00") + "|" + text.Replace('\n', ' '));
        return len;
    }

    void Update() {
        if (queue.Count == 0 || Cutscene.IsOpen || !WallitVoice.FloorFree) {
            return;
        }
        Pending p = queue[0];
        queue.RemoveAt(0);
        // Never a stack of stale lines: a long queue drops to its newest few.
        while (queue.Count > 3) {
            queue.RemoveAt(0);
        }
        Speak(p.Code, p.Text, p.Why);
    }
}
