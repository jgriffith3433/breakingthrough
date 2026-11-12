using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Arcade act change as a staged event (docs/world-bible.md, "The act changes"). After the wave 10 and wave 20 bosses the
// cut to the next act becomes a 6.2 s scene on the CinematicDirector: the camera pulls up and back over the whole Hollow with
// Wallit and Slice kept in frame (phone included), ArcadeActs blends sky, light, grade and score to the next act's look over
// the same seconds, the world visibly changes, Wallit and Slice trade one of their own lines, the act card is stamped in the
// Department typeface in the upper third on the bar, and the play camera comes back (0.3 s hand-off, then controls).
//   Act II  Cold Hour:  the lamp posts go dark and flare on one by one (NightAtmosphere.StageReveal), fog rolls in over the hedges.
//   Act III Last Light: the post-office ruin's red beacon lights and starts sweeping the lawn (FilmLight), paper burns at the
//                       edges of the lawn in big soft glows and smoke, never tiny sparks.
// Once per run per act (ArcadeActs keeps the list). Any tap, click or Enter/Space skips after half a second. Arcade only, and in a
// playtest only when a scenario opts in (ActScenesOn), so the other scenarios keep their old cut.
public partial class CinematicDirector {
    public const float ActSeconds = 6.2f;
    const float ActRise = 2.3f;       // the camera is at the top of its pull-up here
    const float ActDown = 5.2f;       // and starts coming back here
    const float ActStamp = 3.2f;      // the card lands on a bar (the score's bars are 2 s: 1.6 s in, the second bar line)
    const float ActSkipAfter = 0.5f;

    public static bool ActScenesOn;   // a playtest opts in
    public static bool ActScenesAllowed { get { return !Playtest.Running || ActScenesOn; } }
    public static bool ActOpen { get { return Instance != null && Instance.actRun; } }
    public static bool ActCardUp { get; private set; }
    public static float ActCardCenterFromTop { get; private set; }
    public static float LastActSeconds { get; private set; }
    public static int ActScenes { get; private set; }
    public static int ActSkips { get; private set; }
    public static int ActLampsFlared { get; private set; }
    public static bool ActBeaconSweeping { get; private set; }
    public static bool ActPairInFrame { get; private set; }
    public static readonly List<string> ActEvents = new List<string>();

    bool actRun;
    int actIndex;
    string actBig = "", actSub = "";
    float actStart;
    Action actDone;
    Vector3 actStartPos, actHighPos, actLook;
    Quaternion actStartRot, actHighRot;
    float actStartFov, actHighFov;
    bool actStamped, actLine1, actLine2, actFlash, actBeaconSeen;
    float actNextPuff;
    readonly List<Enemy> actHeld = new List<Enemy>();
    float actCaptionUntil;

    // Plays the scene for act `act` (1 = II, 2 = III). Returns false when it cannot (the caller keeps the old banner).
    public static bool PlayAct(int act, string title, Action done) {
        Camera camera = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (camera == null || player == null || Musician.Instance == null || act < 1 || act > 2 || !ActScenesAllowed || IsOpen) {
            return false;
        }
        if (Instance == null) {
            Instance = new GameObject("Cinematic Director").AddComponent<CinematicDirector>();
        }
        Instance.BeginAct(act, title, player.transform, camera, done);
        return true;
    }

    void BeginAct(int act, string title, Transform wallit, Camera camera, Action onDone) {
        actIndex = act;
        int split = title.IndexOf("  ", StringComparison.Ordinal);
        actBig = split > 0 ? title.Substring(0, split) : title;
        actSub = split > 0 ? title.Substring(split + 2) : "";
        actDone = onDone;
        cam = camera;
        zelda = cam.GetComponent<ZeldaStyleCamera>();
        savedFov = cam.fieldOfView;
        actStartPos = cam.transform.position;
        actStartRot = cam.transform.rotation;
        actStartFov = savedFov;
        actRun = true;
        actStamped = actLine1 = actLine2 = actFlash = actBeaconSeen = false;
        actNextPuff = 0.8f;
        actCaptionUntil = 0f;
        ActCardUp = false;
        ActPairInFrame = false;
        ActLampsFlared = 0;
        ActBeaconSweeping = false;
        ActScenes++;
        actStart = Time.unscaledTime;
        ActEvents.Add("act-open|" + actBig);
        Events.Add("act-open|" + actBig);
        PlanActShot(wallit);

        actHeld.Clear();
        foreach (Enemy other in new List<Enemy>(Enemy.Roster)) {
            if (other != null && other.Alive) {
                other.Hold(ActSeconds + 2f);
                actHeld.Add(other);
            }
        }
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        EnsureUi();
        EnsureBossUi();
        uiRoot.SetActive(true);
        skipPill.SetActive(false);
        bossCard.SetActive(false);
        if (caption != null) {
            caption.text = "";
        }
        if (namePlate != null) {
            namePlate.text = "";
        }
        if (actIndex == 1) {
            NightAtmosphere.StageReveal = 0f;   // the posts go dark at once, then flare on one by one
        }
        ActShot(0f);
    }

    // The high shot: behind the circle, high, looking at Wallit and Slice with the ruin on the horizon. It is pushed back until
    // both of them sit inside the middle of the frame at this screen's shape (a portrait phone needs far more distance).
    void PlanActShot(Transform wallit) {
        Vector3 circle = HollowPostOffice.Built ? HollowPostOffice.Circle : Musician.Instance.transform.position;
        Vector3 ruin = HollowPostOffice.Built ? HollowPostOffice.LandmarkBase : circle + Vector3.forward * 60f;
        Vector3 toRuin = ruin - circle;
        toRuin.y = 0f;
        toRuin = toRuin.sqrMagnitude > 1f ? toRuin.normalized : Vector3.forward;
        Vector3 w = wallit.position;
        Vector3 s = Musician.Instance.transform.position;
        Vector3 mid = (w + s) * 0.5f;
        actLook = Vector3.Lerp(mid, ruin, 0.22f) + Vector3.up * 1f;
        actHighFov = Phone() ? 70f : 58f;
        float aspect = Mathf.Max(0.3f, cam.aspect);
        float d = 28f;
        for (int i = 0; i < 20; i++) {
            Vector3 pos = mid - toRuin * d + Vector3.up * (d * 0.62f);
            Quaternion rot = Quaternion.LookRotation((actLook - pos).normalized, Vector3.up);
            actHighPos = pos;
            actHighRot = rot;
            if (InFrame(pos, rot, actHighFov, aspect, w + Vector3.up, s + Vector3.up)) {
                break;
            }
            d += 5f;
        }
    }

    // True when every point is in the middle of the frame, clear of the card (top) and the bars: x 0.14..0.86, y 0.12..0.7.
    static bool InFrame(Vector3 pos, Quaternion rot, float vfov, float aspect, params Vector3[] points) {
        float ty = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
        float tx = ty * aspect;
        Quaternion inv = Quaternion.Inverse(rot);
        foreach (Vector3 p in points) {
            Vector3 l = inv * (p - pos);
            if (l.z < 1f) {
                return false;
            }
            float x = 0.5f + l.x / l.z / (2f * tx);
            float y = 0.5f + l.y / l.z / (2f * ty);
            if (x < 0.14f || x > 0.86f || y < 0.12f || y > 0.7f) {
                return false;
            }
        }
        return true;
    }

    void TickAct() {
        if (PauseMenu.IsOpen) {
            return;
        }
        if (cam == null || Musician.Instance == null) {
            EndAct(true);
            return;
        }
        float t = Time.unscaledTime - actStart;
        if (t > ActSkipAfter && SkipTapped()) {
            ActSkips++;
            ActEvents.Add("act-skip|" + actBig);
            Events.Add("act-skip|" + actBig);
            EndAct(true);
            return;
        }
        ActShot(t);
        TickActWorld(t);
        TickActLines(t);
        TickActCard(t);
        EaseBars(true);
        LayoutUi();
        if (t >= ActSeconds) {
            EndAct(false);
        }
    }

    void ActShot(float t) {
        float up = Ease(t / ActRise) * (1f - Ease((t - ActDown) / (ActSeconds - ActDown)));
        Vector3 pos = Vector3.Lerp(actStartPos, actHighPos, up);
        if (!ComfortOptions.ReducedMotion) {
            pos += Vector3.up * Mathf.Sin(up * Mathf.PI) * 1.5f;
            // A slow drift round the target while it holds, so the high shot is alive.
            float yaw = Mathf.Sin(t * 0.55f) * 4f * up;
            pos = actLook + Quaternion.Euler(0f, yaw, 0f) * (pos - actLook);
        }
        Quaternion rot = Quaternion.Slerp(actStartRot, actHighRot, up);
        if (up > 0.01f) {
            Vector3 aim = actLook - pos;
            if (aim.sqrMagnitude > 0.01f) {
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation(aim.normalized, Vector3.up), Mathf.Clamp01(up * 1.2f) * 0.5f);
            }
        }
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 80f, Vector3.down, out hit, 160f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            pos.y = Mathf.Max(pos.y, hit.point.y + 1.2f);
        }
        viewPos = pos;
        viewRot = rot;
        viewSet = true;
        cam.fieldOfView = Mathf.Lerp(actStartFov, actHighFov, up);
        cam.transform.position = viewPos;
        cam.transform.rotation = viewRot;
        if (zelda != null) {
            zelda.CinematicHold(viewPos, viewRot);
        }
        if (up > 0.95f && !ActPairInFrame) {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) {
                ActPairInFrame = InFrame(viewPos, viewRot, cam.fieldOfView, cam.aspect, player.transform.position + Vector3.up, Musician.Instance.transform.position + Vector3.up);
                ActEvents.Add("act-frame|" + ActPairInFrame);
            }
        }
    }

    // ---------------------------------------------------------------- the world changes

    void TickActWorld(float t) {
        Vector3 circle = HollowPostOffice.Built ? HollowPostOffice.Circle : Musician.Instance.transform.position;
        if (actIndex == 1) {
            // Cold Hour: the posts flare on one by one from 0.6 s, fog rolls in off the hedges.
            float reveal = Mathf.Clamp01((t - 0.6f) / 2.6f);
            NightAtmosphere.StageReveal = reveal;
            IList<Light> lamps = NightAtmosphere.Lamps;
            if (lamps != null) {
                ActLampsFlared = Mathf.Min(lamps.Count, Mathf.FloorToInt(reveal * (lamps.Count + 2)));
            }
            if (t >= actNextPuff && t < 4.4f) {
                actNextPuff = t + 0.14f;
                float a = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = UnityEngine.Random.Range(22f, 34f);
                Vector3 at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r + Vector3.up * 0.7f;
                Fx.Emit(FxKind.Smoke, at, 2, new Color(0.55f, 0.72f, 0.88f, 0.34f), 5.5f, 1.2f, 3f, 3.2f);
            }
        } else {
            // Last Light: the ruin's beacon lights (FilmLight sweeps it as the act's weight rises) and the paper burns at the edges.
            if (!actFlash && t >= 0.8f && HollowPostOffice.Built) {
                actFlash = true;
                Vector3 head = HollowPostOffice.LandmarkBase + Vector3.up * 17f;
                Fx.Emit(FxKind.Flash, head, 1, new Color(1f, 0.18f, 0.12f, 0.9f), 9f, 0f);
                Fx.Ring(HollowPostOffice.LandmarkBase + Vector3.up * 0.3f, 16f, new Color(1f, 0.3f, 0.2f, 0.8f), 1.2f);
                GameAudio.Play("stomp", 0.6f, 0.5f);
            }
            if (!actBeaconSeen && FilmLight.BeaconNow > 0.25f) {
                actBeaconSeen = true;
                ActBeaconSweeping = true;
                ActEvents.Add("act-beacon|" + FilmLight.BeaconNow.ToString("0.00"));
            }
            if (t >= Mathf.Max(actNextPuff, 1.2f) && t < 4.8f) {
                actNextPuff = t + 0.13f;
                float a = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r = UnityEngine.Random.Range(15f, 30f);
                Vector3 at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r + Vector3.up * 0.4f;
                Fx.Emit(FxKind.Flash, at, 1, new Color(1f, 0.45f, 0.14f, 0.55f), 2.2f, 0f);
                Fx.Emit(FxKind.Smoke, at + Vector3.up * 0.5f, 1, new Color(0.18f, 0.14f, 0.13f, 0.5f), 3f, 1.4f, 2f, 2.6f);
                Fx.Emit(FxKind.Debris, at, 1, new Color(0.96f, 0.9f, 0.74f, 1f), 0.8f, 3f, 1f, 1.6f);   // a scorched sheet
            }
        }
    }

    // ---------------------------------------------------------------- the lines

    // Wallit and Slice share one exchange of lines they already have (Story: Slice "Legs. Count them.", Wallit "Pay up.").
    void TickActLines(float t) {
        string first = actIndex == 1 ? "Legs. Count them." : "Nobody touches her.";
        string second = actIndex == 1 ? "Pay up." : "Clean.";
        bool sliceFirst = actIndex == 1;
        if (!actLine1 && t >= 1.0f) {
            actLine1 = true;
            ActSpeak(sliceFirst, first);
        }
        if (!actLine2 && t >= 4.0f) {
            actLine2 = true;
            ActSpeak(!sliceFirst, second);
        }
        if (actCaptionUntil > 0f && Time.unscaledTime > actCaptionUntil) {
            actCaptionUntil = 0f;
            if (caption != null) {
                caption.text = "";
            }
            if (namePlate != null) {
                namePlate.text = "";
            }
        }
    }

    void ActSpeak(bool slice, string line) {
        WallitVoice.Silence();
        SliceVoice.Silence();
        float length = slice ? SliceVoice.Cutscene(line) : WallitVoice.Cutscene(line);
        SubtitleSpeaker = slice ? "S" : "W";
        if (namePlate != null) {
            namePlate.text = slice ? "Slice" : "Wallit";
            namePlate.color = slice ? new Color(0.75f, 0.7f, 0.95f) : new Color(0.95f, 0.82f, 0.4f);
        }
        if (caption != null) {
            caption.text = line;
        }
        actCaptionUntil = Time.unscaledTime + Mathf.Clamp(length + 0.5f, 1.4f, 2.6f);
        ActEvents.Add("act-line|" + (slice ? "Slice" : "Wallit") + "|" + line);
        Events.Add("act-line|" + line);
    }

    // ---------------------------------------------------------------- the card

    void StampActCard() {
        actStamped = true;
        ArcadeActs.StingNow(actIndex);
        GameAudio.Play("stomp", 0.7f, 0.8f);
        GameAudio.NoteOther(GameAudio.Bus.Effects, "act_stamp");
        bossCardTitle.text = actBig;
        bossCardSub.text = actSub;
        bossCard.SetActive(true);
        // y from the bottom: title 0.76-0.87, bar 0.752, sub 0.69-0.75: the card's middle is in the upper third on every screen.
        Place(bossCardTitle.rectTransform, 0.05f, 0.76f, 0.95f, 0.87f);
        Place(bossCardSub.rectTransform, 0.05f, 0.69f, 0.95f, 0.75f);
        var br = bossCardBar.rectTransform;
        br.anchorMin = new Vector2(0.12f, 0.752f);
        br.anchorMax = new Vector2(0.88f, 0.759f);
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        ActCardUp = true;
        ActCardCenterFromTop = 1f - (0.69f + 0.87f) * 0.5f;
        ActEvents.Add("act-card|" + actBig + "|" + actSub);
        Events.Add("act-card|" + actBig);
    }

    void TickActCard(float t) {
        if (!actStamped && t >= ActStamp) {
            StampActCard();
        }
        if (!actStamped) {
            return;
        }
        float since = t - ActStamp;
        float land = Mathf.Clamp01(since / 0.18f);
        float s = Mathf.Lerp(1.6f, 1f, land);
        bossCardTitle.rectTransform.localScale = new Vector3(s, s, 1f);
        Color tc = bossCardTitle.color;
        tc.a = land;
        bossCardTitle.color = tc;
        bossFlash.color = new Color(1f, 0.95f, 0.8f, Mathf.Clamp01(1f - since / 0.35f) * 0.4f);
        bossCardBar.rectTransform.localScale = new Vector3(Mathf.Clamp01((since - 0.1f) / 0.3f), 1f, 1f);
        Color sc = bossCardSub.color;
        sc.a = Mathf.Clamp01((since - 0.3f) / 0.4f);
        float left = ActSeconds - t;
        float fade = Mathf.Clamp01(left / 0.5f);
        tc.a = Mathf.Min(tc.a, fade);
        sc.a = Mathf.Min(sc.a, fade);
        bossCardTitle.color = tc;
        bossCardSub.color = sc;
        Color bc = bossCardBar.color;
        bc.a = 0.95f * fade;
        bossCardBar.color = bc;
    }

    // ---------------------------------------------------------------- the end

    void EndAct(bool skipped) {
        if (!actRun) {
            return;
        }
        actRun = false;
        LastActSeconds = Time.unscaledTime - actStart;
        NightAtmosphere.StageReveal = -1f;
        if (!actStamped) {
            ArcadeActs.StingNow(actIndex);   // a skipped scene still turns the music
        }
        foreach (Enemy e in actHeld) {
            if (e != null) {
                e.Hold(0f);
            }
        }
        actHeld.Clear();
        CastVoice.Silence();
        WallitVoice.Silence();
        SliceVoice.Silence();
        VoiceDirector.Scene = false;
        EaseBars(false);
        if (uiRoot != null) {
            uiRoot.SetActive(false);
        }
        if (bossCard != null) {
            bossCard.SetActive(false);
            Color bc = bossCardBar.color;
            bc.a = 0.95f;
            bossCardBar.color = bc;
        }
        ActCardUp = false;
        if (cam != null) {
            cam.fieldOfView = savedFov;
        }
        if (zelda != null) {
            zelda.BeginHandoff(HandBack);   // the play camera is back at once; the blend is 0.3 s
        }
        viewSet = false;
        if (skipped) {
            ArcadeActs.FadeSeconds = 0.6f;   // the look does not hang half-blended
        }
        ActEvents.Add("act-end|" + actBig + "|" + LastActSeconds.ToString("0.00") + "|" + (skipped ? "skipped" : "played"));
        Events.Add(skipped ? "skipped" : "done");
        if (!skipped) {
            Finished++;
        }
        Action cb = actDone;
        actDone = null;
        cb?.Invoke();
    }
}
