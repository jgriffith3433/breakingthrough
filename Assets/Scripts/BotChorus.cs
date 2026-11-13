using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The bots talk. Yo Bots are a chirpy chorus (waking, spotting Wallit, hit, launched by a home run, falling); Spider Bots are
// polite ("You have been served."). A line is a Resources/Voice/<yobot|spiderbot>_<slug>.mp3 (Hawkes Gen, ids in audio.json)
// played from the bot's own position (GameAudio.PlayVoiceAt), so a bark off screen tells the player where the trouble is.
// It is a Bark: the VoiceDirector drops it when anyone is talking, and a given line may not play again inside 60 s. One voice
// at a time: the line takes the shared floor, and its subtitle is one short line placed by UiFit. With no file the subtitle
// and the floor still run. Enemy calls the three On... hooks (one line each); the rest is polled here, never per frame.
public class BotChorus : MonoBehaviour {
    public static readonly string[] YoWake = { "Uploading victory.", "Good morning, customer!", "Online and delighted!", "Booting cheerfully!" };
    public static readonly string[] YoSpot = { "Wallet detected!", "Found him! Hi!", "Target acquired. Yay!" };
    public static readonly string[] YoHit = { "Ow! Rude!", "Ooh, that stung!", "Feedback received!", "Error! Ow!" };
    public static readonly string[] YoLaunch = { "Wheeeee!", "Going up!", "Tell my charger I love it!" };
    public static readonly string[] YoFall = { "Powering down. Bye!", "Goodbye, circuit!", "Upload failed. Hi!", "Thanks for having me!" };
    public static readonly string[] Spider = {
        "You have been served.", "Kindly hold still.", "Apologies in advance.", "Your appointment is now.",
        "Please accept this leg.", "Regrettably, you are overdue.", "Do excuse the stab."
    };

    public const float BotCooldown = 60f;
    const float Range = 48f;       // farther than this nobody hears a bot
    const float SpotRange = 15f;

    static BotChorus instance;
    public static int Spoken { get; private set; }
    public static readonly HashSet<string> Heard = new HashSet<string>();
    public static readonly List<string> Events = new List<string>();

    struct Request { public string Why; public Vector3 At; public bool Spider; public float Expires; public bool Set; }

    Request request;
    readonly Dictionary<Enemy, byte> seen = new Dictionary<Enemy, byte>();   // 1 woke, 2 spotted
    Transform listener;
    float nextPoll;
    float lineEnd;
    Text text;
    CanvasScaler scaler;
    int stamp = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        Spoken = 0;
        Heard.Clear();
        Events.Clear();
    }

    static BotChorus Get() {
        if (instance == null) {
            var go = new GameObject("Bot Chorus");
            instance = go.AddComponent<BotChorus>();
            instance.Build();
        }
        return instance;
    }

    public static string LineId(string line, bool spider) {
        return (spider ? "spiderbot_" : "yobot_") + SliceVoice.LineId(line).Substring(6);
    }

    // ---------------------------------------------------------------- hooks (Enemy)

    public static void OnHit(Enemy foe) {
        Note(foe, "bot-hit");
    }

    public static void OnLaunched(Enemy foe) {
        Note(foe, "bot-launch");
    }

    public static void OnFall(Enemy foe) {
        Note(foe, "bot-fall");
    }

    static void Note(Enemy foe, string why) {
        if (foe == null || foe.Boss) {
            return;
        }
        BotChorus c = Get();
        c.Ask(why, foe.transform.position, BotRoster.IsSpider(foe.Kind), 1.5f);
    }

    void Ask(string why, Vector3 at, bool spider, float patience) {
        // A fall or a launch outranks a hit; a waiting request is only replaced by one as important.
        if (request.Set && Time.unscaledTime < request.Expires && Rank(why) < Rank(request.Why)) {
            return;
        }
        request = new Request { Why = why, At = at, Spider = spider, Expires = Time.unscaledTime + patience, Set = true };
    }

    static int Rank(string why) {
        switch (why) {
            case "bot-launch": return 4;
            case "bot-fall": return 3;
            case "bot-hit": return 2;
            case "bot-spot": return 1;
            default: return 0;
        }
    }

    // ---------------------------------------------------------------- tick

    void Update() {
        float now = Time.unscaledTime;
        if (stamp != UiFit.CaptionStamp && scaler != null) {
            stamp = UiFit.CaptionStamp;
            UiFit.Apply(scaler);
            UiFit.PlaceCaption(text.rectTransform, 190f, 0);
        }
        if (text != null && text.enabled && now >= lineEnd) {
            text.enabled = false;
        }
        if (Cutscene.IsOpen || LevelUpScreen.IsOpen || PauseMenu.IsOpen || WaveDirector.IsLost || VoiceDirector.Scene) {
            request.Set = false;
            return;
        }
        if (now >= nextPoll) {
            nextPoll = now + 0.4f;
            Poll();
        }
        if (!request.Set) {
            return;
        }
        if (now > request.Expires) {
            request.Set = false;
            return;
        }
        if (!WallitVoice.FloorFree || WallitVoice.Waiting > 0 || (SliceVoice.Instance != null && SliceVoice.Instance.BanterRunning)) {
            return;
        }
        Request r = request;
        request.Set = false;
        if (Distance(r.At) > Range) {
            return;
        }
        string[] bank = r.Spider ? Spider : r.Why == "bot-wake" ? YoWake : r.Why == "bot-spot" ? YoSpot : r.Why == "bot-hit" ? YoHit : r.Why == "bot-launch" ? YoLaunch : YoFall;
        string line = Choose(bank, r.Spider ? "bot-spider" : r.Why);
        if (line != null) {
            Speak(line, r);
        }
    }

    float Distance(Vector3 at) {
        if (listener == null) {
            var player = FindAnyObjectByType<CharacterMovement>();
            listener = player != null ? player.transform : (Camera.main != null ? Camera.main.transform : null);
        }
        return listener == null ? 0f : Vector3.Distance(listener.position, at);
    }

    // A line of the bank the director lets through (not inside its 60 s cooldown), or null.
    static string Choose(string[] bank, string why) {
        int start = Random.Range(0, bank.Length);
        for (int i = 0; i < bank.Length; i++) {
            string line = bank[(start + i) % bank.Length];
            if (VoiceDirector.Allow(line, why, VoiceDirector.Priority.Bark)) {
                return line;
            }
        }
        return null;
    }

    void Speak(string line, Request r) {
        string id = LineId(line, r.Spider);
        AudioClip clip = Resources.Load<AudioClip>("Voice/" + id);
        float length = clip != null ? clip.length : WallitVoice.EstimateSeconds(line);
        string why = r.Spider ? "bot-spider" : r.Why;
        WallitVoice.Claim(r.Spider ? "spiderbot" : "yobot", length);
        VoiceDirector.Spoke(line, why, VoiceDirector.Priority.Bark);
        if (clip != null) {
            GameAudio.PlayVoiceAt(clip, id, r.At, 1f);
        }
        Spoken++;
        Heard.Add(id);
        Events.Add("bark|" + why + "|" + line + "|" + length.ToString("0.00") + (clip != null ? "|voice" : "|subtitle"));
        if (text != null) {
            text.text = (r.Spider ? "SPIDER BOT  " : "YO BOT  ") + line;
            text.enabled = true;
            lineEnd = Time.unscaledTime + Mathf.Max(1.8f, length + 0.4f);
        }
    }

    // Wake and spot are read from the roster twice a second; a new kind of bot and a boss's second phase go to Wallit and Slice.
    void Poll() {
        var roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive || e.Dormant) {
                continue;
            }
            byte flags;
            seen.TryGetValue(e, out flags);
            if (flags == 0) {
                seen[e] = 1;
                if (!e.Boss) {
                    SliceVoice.OnNewKind(e.Kind);
                    Ask("bot-wake", e.transform.position, BotRoster.IsSpider(e.Kind), 2.5f);
                }
            } else if ((flags & 2) == 0 && !e.Boss && Distance(e.transform.position) < SpotRange) {
                seen[e] = (byte)(flags | 2);
                Ask("bot-spot", e.transform.position, BotRoster.IsSpider(e.Kind), 1.5f);
            }
            if (e.Boss && e.Phase >= 2 && (flags & 4) == 0) {
                seen[e] = (byte)(seen[e] | 4);
                WallitVoice.OnBossPhaseTwo();
            }
        }
        if (seen.Count > 64) {
            var gone = new List<Enemy>();
            foreach (var pair in seen) {
                if (pair.Key == null || !pair.Key.Alive) {
                    gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < gone.Count; i++) {
                seen.Remove(gone[i]);
            }
        }
    }

    // ---------------------------------------------------------------- subtitle

    void Build() {
        var canvasGo = new GameObject("Bot Subtitles");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        var go = new GameObject("Line");
        go.transform.SetParent(canvasGo.transform, false);
        text = go.AddComponent<Text>();
        text.font = UiSkin.Font(UiSkin.FontRole.Label);
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = DeptPalette.CreamSoft;
        text.raycastTarget = false;
        text.enabled = false;
        // One line, always: a long one shrinks to fit the band instead of wrapping.
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = 28;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        stamp = UiFit.CaptionStamp;
        UiFit.Apply(scaler);
        UiFit.PlaceCaption(text.rectTransform, 190f, 0);
    }
}
