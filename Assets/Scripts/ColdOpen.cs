using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The cold open (Docs/Story.md, "The intro"): the first seconds of an Arcade run, played in the real scene with the real
// Wallit, Slice and Yo Bot, and no text pages. The RETURN TO SENDER stamp drops onto Wallit (stamp_heavy), he lands in
// the circle as its ring lights, Slice says one line that already exists in her bank (her subtitle is the only text),
// and the first Yo Bot is already rushing in. Control is never taken: any key, tap, stick push or swing ends it on that
// frame, and Wallit is put where he would have landed. It plays once per browser (PlayerPrefs, which is localStorage on
// the web); a playtest or sandbox keeps the flag in memory only.
public class ColdOpen : MonoBehaviour {
    public const string Key = "hawkes.coldopen";
    public const string SliceLine = "Stay close.";   // SliceVoice's wave bank; its voice file is slice_stay_close
    public const float DropHeight = 7f;
    public const float DropSeconds = 0.8f;
    public const float LandAt = 0.9f;                // seconds from the start: the stamp lands, the ring lights
    public const float LineAt = 1.6f;
    public const float BotCue = 1.0f;                // the first Yo Bot is released as Wallit lands
    public const float EndAt = 4.5f;
    public const float MaxSeconds = 8f;              // a hard cap, whatever happens

    // Playtests that are not about the cold open set this so a fresh sandbox run goes straight to play.
    public static bool Suppress;

    public static bool Playing { get { return instance != null && instance.active; } }
    public static bool PlayedThisRun { get; private set; }
    public static int Starts { get; private set; }
    public static int Ends { get; private set; }
    public static float StartedAt { get; private set; } = -1f;
    public static float EndedAt { get; private set; } = -1f;
    public static string EndWhy { get; private set; } = "";
    public static readonly List<string> Events = new List<string>();

    static ColdOpen instance;
    static bool sandboxSeen;

    // Seen on this browser (or in this sandbox). The playtest clears it with Forget for a fresh save.
    public static bool Seen {
        get { return MetaProgress.Sandbox ? sandboxSeen : PlayerPrefs.GetInt(Key, 0) == 1; }
    }

    public static void Forget() {
        sandboxSeen = false;
        if (!MetaProgress.Sandbox) {
            PlayerPrefs.DeleteKey(Key);
        }
    }

    static void MarkSeen() {
        if (MetaProgress.Sandbox) {
            sandboxSeen = true;
            return;
        }
        PlayerPrefs.SetInt(Key, 1);
        PlayerPrefs.Save();
    }

    // Called by WaveDirector.Start on an Arcade run that begins at wave 1. Returns true when the open is playing.
    public static bool TryBegin() {
        PlayedThisRun = false;
        StartedAt = -1f;
        EndedAt = -1f;
        EndWhy = "";
        Events.Clear();
        if (Suppress || Seen) {
            return false;
        }
        CharacterMovement player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null || Musician.Instance == null) {
            return false;
        }
        MarkSeen();
        var go = new GameObject("Cold Open");
        instance = go.AddComponent<ColdOpen>();
        instance.player = player;
        PlayedThisRun = true;
        return true;
    }

    CharacterMovement player;
    MovementMotor motor;
    UnityEngine.CharacterController body;
    bool motorWas;
    bool bodyWas;
    Vector3 landing;
    Transform stamp;
    float t;
    bool active;
    bool landed;
    bool lined;
    bool released;
    float stampRise;

    void Start() {
        motor = player.GetComponent<MovementMotor>();
        body = player.GetComponent<UnityEngine.CharacterController>();
        landing = player.transform.position;
        motorWas = motor != null && motor.enabled;
        bodyWas = body != null && body.enabled;
        if (motor != null) {
            motor.enabled = false;
        }
        if (body != null) {
            body.enabled = false;
        }
        GameObject seal = KitCatalog.Make("of_stamp_seal");
        if (seal != null) {
            seal.name = "Return To Sender Stamp";
            stamp = seal.transform;
            // A big flat seal, face down, on its way onto Wallit.
            stamp.localScale = Vector3.one * 5f;
            stamp.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        active = true;
        StartedAt = Time.realtimeSinceStartup;
        Starts++;
        Events.Add("coldopen-start");
        Place(0f);
    }

    // Wallit and the stamp at a point of the drop (0 high up, 1 on the ground).
    void Place(float k) {
        float eased = k * k;
        float high = DropHeight * (1f - eased);
        player.transform.position = landing + Vector3.up * high;
        if (stamp != null) {
            float lift = stampRise > 0f ? stampRise : 0f;
            stamp.position = landing + Vector3.up * (high + 1.9f + lift * 9f);
        }
    }

    void Update() {
        if (!active) {
            return;
        }
        t += Time.unscaledDeltaTime;
        if (t > 0.25f && Skipped()) {
            End("input");
            return;
        }
        if (!landed) {
            Place(Mathf.Clamp01(t / DropSeconds));
            if (t >= LandAt) {
                Land();
            }
        } else {
            // The stamp lifts away over a second and is gone.
            stampRise += Time.unscaledDeltaTime;
            if (stamp != null) {
                stamp.position = landing + Vector3.up * (1.9f + stampRise * 9f);
                if (stampRise > 1f) {
                    Destroy(stamp.gameObject);
                    stamp = null;
                }
            }
        }
        if (landed && !released && t >= BotCue) {
            Release();
        }
        if (landed && !lined && t >= LineAt) {
            lined = true;
            SliceVoice.ColdLine(SliceLine);
            Events.Add("coldopen-line|" + SliceLine);
        }
        if (t >= EndAt || t >= MaxSeconds) {
            End("done");
        }
    }

    // Any key, tap, stick push or swing. The touch stick and Swing press through PlayInput, so this reads them too.
    static bool Skipped() {
        if (PlayInput.AnyKeyDown || PlayInput.GetMouseButtonDown(0)) {
            return true;
        }
        for (int i = 0; i < Input.touchCount; i++) {
            if (Input.GetTouch(i).phase == TouchPhase.Began) {
                return true;
            }
        }
        return Mathf.Abs(PlayInput.GetAxis("Horizontal")) > 0.15f || Mathf.Abs(PlayInput.GetAxis("Vertical")) > 0.15f;
    }

    void Land() {
        landed = true;
        player.transform.position = landing;
        GameAudio.Play("stamp_heavy", 0.9f, 1f);
        CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_LANDING);
        // The ring lights: three soft pulses out from Slice's feet, no sparks.
        Vector3 ring = Musician.Instance.transform.position + Vector3.up * 0.05f;
        Fx.Ring(landing + Vector3.up * 0.1f, 4f, new Color(1f, 0.9f, 0.6f, 0.8f), 0.5f);
        Fx.Ring(ring, 8f, new Color(1f, 0.85f, 0.45f, 0.7f), 0.8f);
        StartCoroutine(PulseRing(ring));
        Events.Add("coldopen-land");
    }

    System.Collections.IEnumerator PulseRing(Vector3 at) {
        for (int i = 0; i < 2; i++) {
            yield return new WaitForSecondsRealtime(0.45f);
            Fx.Ring(at, 10f + i * 4f, new Color(1f, 0.85f, 0.45f, 0.55f), 0.9f);
        }
    }

    // The first Yo Bot is already rushing in: let the wave director send it now.
    void Release() {
        released = true;
        if (WaveDirector.Instance != null) {
            WaveDirector.Instance.ColdOpenReleased();
        }
    }

    void End(string why) {
        if (!active) {
            return;
        }
        active = false;
        player.transform.position = landing;
        if (stamp != null) {
            Destroy(stamp.gameObject);
            stamp = null;
        }
        if (body != null) {
            body.enabled = bodyWas;
        }
        if (motor != null) {
            motor.enabled = motorWas;
        }
        if (!landed) {
            landed = true;
            GameAudio.Play("stamp_heavy", 0.9f, 1f);
            Fx.Ring(landing + Vector3.up * 0.1f, 4f, new Color(1f, 0.9f, 0.6f, 0.8f), 0.5f);
        }
        if (!released) {
            Release();
        }
        EndedAt = Time.realtimeSinceStartup;
        EndWhy = why;
        Ends++;
        Events.Add("coldopen-end|" + why + "|" + (EndedAt - StartedAt).ToString("0.0") + " s");
        Destroy(gameObject, 0.1f);
    }

    void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
        if (active) {
            // Scene left mid-open: hand the body back so nothing stays switched off.
            active = false;
            if (body != null) {
                body.enabled = bodyWas;
            }
            if (motor != null) {
                motor.enabled = motorWas;
            }
        }
    }

    // ---------------------------------------------------------------- words on screen (for the playtest)

    // Words a player could read on screen now: every visible Text outside the standing HUD canvases (the wave bar,
    // the power list, the roster, the boss bar, the touch pads, the pause button), numbers not counted. The subtitle, the swing hint and any banner count.
    public static int WordsOnScreen(List<string> into = null) {
        int words = 0;
        Text[] texts = Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
        foreach (Text text in texts) {
            if (text == null || !text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || text.color.a < 0.05f) {
                continue;
            }
            Canvas canvas = text.canvas != null ? text.canvas.rootCanvas : null;
            if (canvas == null || !canvas.isActiveAndEnabled) {
                continue;
            }
            string root = canvas.gameObject.name;
            if (root == "Run Canvas" || root == "Abilities" || root == "Roster" || root == "Wave Canvas" || root == "Touch Canvas" || root == "Pause Button Canvas" || root == "Pause Canvas") {
                continue;
            }
            CanvasGroup group = text.GetComponentInParent<CanvasGroup>();
            if (group != null && group.alpha < 0.05f) {
                continue;
            }
            int n = 0;
            foreach (string token in text.text.Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries)) {
                bool letters = false;
                foreach (char c in token) {
                    if (char.IsLetter(c)) {
                        letters = true;
                        break;
                    }
                }
                if (letters) {
                    n++;
                }
            }
            if (n > 0) {
                words += n;
                if (into != null) {
                    into.Add(root + ":" + text.text.Replace('\n', ' '));
                }
            }
        }
        return words;
    }
}
