using System.Collections.Generic;
using UnityEngine;

// The Arcade act change as an in-world beat (docs/world-bible.md, "The act changes"). It never takes the camera or the controls:
// play goes on under it and it lasts 3.6 s. ArcadeActs blends the look over the same seconds (the moon swings across the sky).
//   Act II  Cold Hour:  two Spider Bots break up through the lawn in front of Wallit (a crack ring, a heave of dust and plates,
//                       a stomp), the lamp posts go dark and flare back on one by one, Slice says "Legs. Count them.", Wallit "Pay up."
//   Act III Last Light: the ruin's beacon lights red, the lamp posts go out one by one (a soft smoke puff on each) while the moon
//                       turns to red, paper burns at the lawn's edge, Wallit says "Nobody touches her.", Slice "Clean."
// Only existing models, VFX and recorded lines. Once per run per act (ArcadeActs keeps the list). The old camera scene
// (CinematicAct.cs) is a playtest opt-in now.
public class ActBeat : MonoBehaviour {
    public const float Seconds = 3.6f;

    public static ActBeat Instance { get; private set; }
    public static bool Open { get { return Instance != null && Instance.running; } }
    public static int Beats { get; private set; }
    public static int TextOnly { get; private set; }
    public static int SpidersBroke { get; private set; }
    public static int LampsOut { get; private set; }
    public static bool BeaconLit { get; private set; }
    public static float LastSeconds { get; private set; }
    public static float ControlLostSeconds { get; private set; }   // always 0: nothing here freezes, holds or hides anything
    public static readonly List<string> Log = new List<string>();

    bool running;
    int act;
    float t;
    bool line1, line2, crack, burst, flash;
    int spiders, lamps;
    bool voiced;
    float nextPuff;
    Vector3[] spots = new Vector3[2];
    readonly List<Enemy> born = new List<Enemy>();

    // Starts the beat for act `index` (1 = II, 2 = III). False when there is no arena to play it in.
    public static bool Play(int index) {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || Musician.Instance == null || index < 1 || index > 2 || Open) {
            return false;
        }
        if (Instance == null) {
            Instance = new GameObject("Act Beat").AddComponent<ActBeat>();
        }
        Instance.Begin(index, player.transform);
        return true;
    }

    void Begin(int index, Transform wallit) {
        act = index;
        running = true;
        t = 0f;
        line1 = line2 = crack = burst = flash = voiced = false;
        spiders = lamps = 0;
        nextPuff = 1.2f;
        born.Clear();
        Beats++;
        BeaconLit = false;
        Log.Add("beat-open|" + (index == 1 ? "Cold Hour" : "Last Light"));
        if (index == 1) {
            // Two spots in front of the camera (so a portrait phone sees them too), each side of Wallit, kept near the circle.
            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : wallit.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3 circle = Musician.Instance.transform.position;
            for (int i = 0; i < 2; i++) {
                Vector3 at = wallit.position + fwd * (11f + i * 2f) + side * (i == 0 ? -3.4f : 3.6f);
                Vector3 off = at - circle;
                off.y = 0f;
                if (off.magnitude > 30f) {
                    at = circle + off.normalized * 30f;
                }
                spots[i] = at;
            }
            NightAtmosphere.StageReveal = 0f;
        }
    }

    void Update() {
        if (!running) {
            return;
        }
        if (PauseMenu.IsOpen) {
            return;
        }
        t += Time.deltaTime;
        if (act == 1) {
            TickSpiders();
        } else {
            TickLights();
        }
        TickLines();
        if (t >= Seconds) {
            End();
        }
    }

    // ---------------------------------------------------------------- Act II

    void TickSpiders() {
        // 0 s: the ground cracks where they will come up (a ring and a low stomp), 0.7 s: they break through.
        if (!crack) {
            crack = true;
            for (int i = 0; i < 2; i++) {
                Vector3 g = Ground(spots[i]);
                Fx.Ring(g + Vector3.up * 0.05f, 6f, new Color(0.6f, 0.8f, 1f, 0.7f), 0.8f);
                Fx.Emit(FxKind.Smoke, g + Vector3.up * 0.3f, 2, new Color(0.5f, 0.62f, 0.78f, 0.4f), 3f, 0.8f, 1.5f, 1.8f);
            }
            GameAudio.Play("stomp", 0.55f, 0.5f);
        }
        if (!burst && t >= 0.7f && WaveDirector.Instance != null) {
            burst = true;
            for (int i = 0; i < 2; i++) {
                Vector3 g = Ground(spots[i]);
                Fx.Emit(FxKind.Flash, g + Vector3.up * 0.6f, 1, new Color(0.7f, 0.85f, 1f, 0.7f), 4f, 0f);
                Fx.Ring(g + Vector3.up * 0.05f, 9f, new Color(0.7f, 0.85f, 1f, 0.8f), 0.6f);
                Fx.Emit(FxKind.Smoke, g + Vector3.up * 0.6f, 3, new Color(0.42f, 0.4f, 0.4f, 0.55f), 4f, 2f, 2f, 2.2f);
                Fx.Emit(FxKind.Debris, g + Vector3.up * 0.4f, 4, new Color(0.35f, 0.3f, 0.26f, 1f), 1.1f, 5f, 1.5f, 1.6f);   // chunks of lawn
                Enemy e = WaveDirector.Instance.SpawnBurst(BotKind.SpiderBot, spots[i]);
                if (e != null) {
                    e.Hold(0.35f);   // they rear for a moment; Wallit is free the whole time
                    born.Add(e);
                    spiders++;
                    SpidersBroke++;
                }
            }
            GameAudio.Play("stomp", 0.95f, 0.65f);
            Log.Add("beat-spiders|" + spiders);
        }
        float reveal = Mathf.Clamp01((t - 0.9f) / 2.4f);
        NightAtmosphere.StageReveal = reveal;
        IList<Light> posts = NightAtmosphere.Lamps;
        if (posts != null) {
            lamps = Mathf.Min(posts.Count, Mathf.FloorToInt(reveal * (posts.Count + 2)));
            LampsOut = Mathf.Max(LampsOut, lamps);
        }
        if (t >= nextPuff && t < 3f) {
            nextPuff = t + 0.16f;
            Vector3 circle = Musician.Instance.transform.position;
            float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Random.Range(20f, 32f) + Vector3.up * 0.7f;
            Fx.Emit(FxKind.Smoke, at, 2, new Color(0.55f, 0.72f, 0.88f, 0.34f), 5.5f, 1.2f, 3f, 3.2f);   // the cold fog rolling in
        }
    }

    // ---------------------------------------------------------------- Act III

    void TickLights() {
        if (!flash && HollowPostOffice.Built) {
            flash = true;
            Vector3 head = HollowPostOffice.LandmarkBase + Vector3.up * 17f;
            Fx.Emit(FxKind.Flash, head, 1, new Color(1f, 0.18f, 0.12f, 0.9f), 9f, 0f);
            Fx.Ring(HollowPostOffice.LandmarkBase + Vector3.up * 0.3f, 16f, new Color(1f, 0.3f, 0.2f, 0.8f), 1.2f);
            GameAudio.Play("stomp", 0.6f, 0.5f);
        }
        if (!BeaconLit && FilmLight.BeaconNow > 0.2f) {
            BeaconLit = true;
            Log.Add("beat-beacon|" + FilmLight.BeaconNow.ToString("0.00"));
        }
        // The posts go out one after another from 0.3 s to 2.7 s, each with a soft puff of smoke.
        float progress = Mathf.Clamp01((t - 0.3f) / 2.4f);
        NightAtmosphere.LampOut = progress;
        IList<Light> posts = NightAtmosphere.Lamps;
        if (posts != null) {
            int out_ = Mathf.Min(posts.Count, Mathf.FloorToInt(progress * (posts.Count + 1)));
            while (lamps < out_) {
                Light l = posts[lamps];
                if (l != null) {
                    Fx.Emit(FxKind.Smoke, l.transform.position, 2, new Color(0.2f, 0.17f, 0.17f, 0.5f), 2.4f, 0.6f, 0.8f, 2f);
                    Fx.Emit(FxKind.Flash, l.transform.position, 1, new Color(1f, 0.5f, 0.2f, 0.4f), 2.2f, 0f);
                }
                lamps++;
            }
            LampsOut = Mathf.Max(LampsOut, lamps);
        }
        if (t >= Mathf.Max(nextPuff, 1.0f) && t < 3.2f) {
            nextPuff = t + 0.15f;
            Vector3 circle = Musician.Instance.transform.position;
            float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Random.Range(15f, 30f) + Vector3.up * 0.4f;
            Fx.Emit(FxKind.Flash, at, 1, new Color(1f, 0.45f, 0.14f, 0.55f), 2.2f, 0f);
            Fx.Emit(FxKind.Smoke, at + Vector3.up * 0.5f, 1, new Color(0.18f, 0.14f, 0.13f, 0.5f), 3f, 1.4f, 2f, 2.6f);
            Fx.Emit(FxKind.Debris, at, 1, new Color(0.96f, 0.9f, 0.74f, 1f), 0.8f, 3f, 1f, 1.6f);   // a scorched sheet
        }
    }

    // ---------------------------------------------------------------- the lines

    // One exchange of lines they already have (Story: Slice "Legs. Count them.", Wallit "Pay up."), spoken over play.
    void TickLines() {
        bool sliceFirst = act == 1;
        string first = act == 1 ? "Legs. Count them." : "Nobody touches her.";
        string second = act == 1 ? "Pay up." : "Clean.";
        if (!line1 && t >= (act == 1 ? 1.0f : 0.6f)) {
            line1 = true;
            Speak(sliceFirst, first);
        }
        if (!line2 && t >= 2.3f) {
            line2 = true;
            Speak(!sliceFirst, second);
        }
    }

    void Speak(bool slice, string line) {
        if (slice) {
            SliceVoice.Cutscene(line);
        } else {
            WallitVoice.Cutscene(line);
        }
        voiced = true;
        Log.Add("beat-line|" + (slice ? "Slice" : "Wallit") + "|" + line);
    }

    void End() {
        running = false;
        LastSeconds = t;
        NightAtmosphere.StageReveal = -1f;
        NightAtmosphere.LampOut = -1f;
        // A beat counts as text-only when nothing in the world changed or nobody spoke.
        bool world = act == 1 ? spiders > 0 : lamps > 0;
        if (!world || !voiced) {
            TextOnly++;
        }
        Log.Add("beat-end|" + (act == 1 ? "Cold Hour" : "Last Light") + "|" + t.ToString("0.00") + "|world " + world + "|voiced " + voiced);
    }

    static Vector3 Ground(Vector3 at) {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(at.x, at.y + 40f, at.z), Vector3.down, out hit, 100f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point;
        }
        return at;
    }
}
