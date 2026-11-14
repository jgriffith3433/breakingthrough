using Hawkes.Playtest;
using UnityEngine;

// Per-frame camera feel detector for the explore scenario (and any run that calls Ensure). Counts frames the camera is
// inside a collider, within 0.15 m of geometry, snaps more than 30 degrees in one frame, or jitters back and forth.
// Metrics are keyed by map name so every explore pass can report its own numbers.
[DefaultExecutionOrder(1000)]
public class CameraFeel : MonoBehaviour {
    public const float NearLimit = 0.15f;
    public const float SnapDegrees = 30f;
    public const float JitterMeters = 0.08f;

    public static CameraFeel Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesCamCut();
#endif
    public static int Cuts { get; private set; }
    static bool cutPending;

    // An intended cut (cutscene, act change, respawn, scene start): the next frame's jump is not a snap. Also tells the
    // page's playtest through hawkesPlaytest.cut().
    public static void Cut(string why) {
        Cuts++;
        cutPending = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { HawkesCamCut(); } catch (System.Exception) { }
#endif
    }

    // One small jolt when a heavy blow (a boss, an elite, a Spider Bot) lands on Wallit. It goes through the camera's
    // own shake, which TickFight's framing never touches, and is off with Reduce Motion. Light hits never call it.
    public static int Jolts { get; private set; }
    public static void HurtJolt() {
        if (ComfortOptions.ReducedMotion) {
            return;
        }
        Jolts++;
        ZeldaStyleCamera.Shake(0.07f * (UiFit.Touch ? 0.7f : 1f), 0.16f);
    }

    // Fight framing: a fight frame has a live bot within ZeldaStyleCamera.FightRange of Wallit; it is framed when Slice and
    // that nearest bot are both on screen (on a portrait phone also above the touch pads).
    public int FightFrames { get; private set; }
    public int FramedFrames { get; private set; }
    public float ClipSeconds { get; private set; }
    public float FramedPct { get { return FightFrames > 0 ? 100f * FramedFrames / FightFrames : 100f; } }
    public string LastUnframed { get; private set; }
    ZeldaStyleCamera follow;

    public int InsideFrames { get; private set; }
    public int NearFrames { get; private set; }
    public int SnapFrames { get; private set; }
    public int JitterFrames { get; private set; }
    public float MaxSnapDeg { get; private set; }
    public float MinGeomDist { get; private set; }
    public string MapKey = "map";

    Transform ignoreRoot;
    Vector3 lastPos;
    Quaternion lastRot;
    Vector3 lastDelta;
    bool primed;
    bool armed = true;

    public static CameraFeel Ensure(Camera cam = null) {
        if (cam == null) {
            cam = Camera.main;
        }
        if (cam == null) {
            return null;
        }
        CameraFeel feel = cam.GetComponent<CameraFeel>();
        if (feel == null) {
            feel = cam.gameObject.AddComponent<CameraFeel>();
        }
        Instance = feel;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        feel.ignoreRoot = player != null ? player.transform : null;
        return feel;
    }

    public void ResetCounts(string map) {
        MapKey = string.IsNullOrEmpty(map) ? "map" : map;
        InsideFrames = 0;
        NearFrames = 0;
        SnapFrames = 0;
        JitterFrames = 0;
        MaxSnapDeg = 0f;
        MinGeomDist = 999f;
        FightFrames = 0;
        FramedFrames = 0;
        ClipSeconds = 0f;
        primed = false;
        armed = true;
    }

    public void Arm(bool on) {
        armed = on;
        if (!on) {
            primed = false;
        }
    }

    void LateUpdate() {
        if (!armed || LevelUpScreen.IsOpen || PauseMenu.IsOpen || Cutscene.IsOpen) {
            primed = false;
            return;
        }
        if (follow == null) {
            follow = GetComponent<ZeldaStyleCamera>();
        }
        // Intended cuts (photo rig, scene load, a directed shot or its handoff) are not this camera.
        if (cutPending || (follow != null && follow.HandoffBusy)) {
            cutPending = false;
            primed = false;
            return;
        }
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        if (CamBlock.PointInsideSolid(pos, ignoreRoot, 0.04f)) {
            InsideFrames++;
            ClipSeconds += Time.unscaledDeltaTime;
        }
        SampleFraming();
        float dist = CamBlock.NearestSolidDistance(pos, ignoreRoot, 1.5f);
        if (dist < MinGeomDist) {
            MinGeomDist = dist;
        }
        if (dist <= NearLimit) {
            NearFrames++;
        }
        if (primed) {
            float snap = Quaternion.Angle(lastRot, rot);
            if (snap > MaxSnapDeg) {
                MaxSnapDeg = snap;
            }
            if (snap > SnapDegrees) {
                SnapFrames++;
            }
            Vector3 delta = pos - lastPos;
            // Jitter: this frame's move reverses the last one by a noticeable amount (back-and-forth shove).
            if (lastDelta.sqrMagnitude > 1e-6f && delta.sqrMagnitude > 1e-6f) {
                float along = Vector3.Dot(delta.normalized, lastDelta.normalized);
                if (along < -0.5f && delta.magnitude > JitterMeters && lastDelta.magnitude > JitterMeters) {
                    JitterFrames++;
                }
            }
            lastDelta = delta;
        } else {
            lastDelta = Vector3.zero;
        }
        lastPos = pos;
        lastRot = rot;
        primed = true;
    }

    void SampleFraming() {
        Camera cam = GetComponent<Camera>();
        Musician slice = Musician.Instance;
        if (cam == null || ignoreRoot == null || slice == null || !slice.gameObject.activeInHierarchy || WaveDirector.IsBuilding) {
            return;
        }
        Enemy near = null;
        float best = ZeldaStyleCamera.FightRange * ZeldaStyleCamera.FightRange;
        var roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive || e.Dormant || e.Climbing) {
                continue;
            }
            Vector3 d = e.transform.position - ignoreRoot.position;
            d.y = 0f;
            if (d.sqrMagnitude < best) {
                best = d.sqrMagnitude;
                near = e;
            }
        }
        if (near == null) {
            return;
        }
        FightFrames++;
        float floor = UiFit.Mode == UiMode.Tall ? 0.30f : 0.04f;
        bool ok = OnScreen(cam, slice.transform.position + Vector3.up, floor) && OnScreen(cam, near.transform.position + Vector3.up * 0.8f, floor)
            && OnScreen(cam, ignoreRoot.position + Vector3.up, floor);
        if (ok) {
            FramedFrames++;
        } else {
            LastUnframed = "slice " + Vector3.Distance(slice.transform.position, ignoreRoot.position).ToString("0.0") + " m, bot " + Mathf.Sqrt(best).ToString("0.0") + " m, pull " + ZeldaStyleCamera.FeelFightPull.ToString("0.0");
        }
    }

    static bool OnScreen(Camera cam, Vector3 world, float yFloor) {
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.z > 0f && v.x > 0.03f && v.x < 0.97f && v.y > yFloor && v.y < 0.97f;
    }

    public void Report(PlaytestRun run) {
        if (run == null) {
            return;
        }
        string m = MapKey;
        run.Metric("feel.cameraInsideFrames." + m, InsideFrames);
        run.Metric("feel.cameraNearFrames." + m, NearFrames);
        run.Metric("feel.cameraSnapFrames." + m, SnapFrames);
        run.Metric("feel.cameraJitterFrames." + m, JitterFrames);
        run.Metric("feel.cameraMaxSnapDeg." + m, Mathf.Round(MaxSnapDeg * 10f) / 10f);
        run.Metric("feel.cameraClipSeconds", Mathf.Round(ClipSeconds * 1000f) / 1000f);
        run.Metric("feel.cameraSnaps", SnapFrames);
        run.Metric("feel.cameraCuts", Cuts);
        run.Metric("fight.framedPct", Mathf.Round(FramedPct * 10f) / 10f);
        run.Metric("fight.frames", FightFrames);
        run.Metric("feel.lockEases", ZeldaStyleCamera.FeelLockEases);
        run.Metric("feel.lockEaseSeconds", ZeldaStyleCamera.FeelLastEaseSeconds);
        run.Metric("feel.cameraMinGeomDist." + m, Mathf.Round(MinGeomDist * 1000f) / 1000f);
        run.Event("camera-feel", m + ": inside " + InsideFrames + ", near " + NearFrames + ", snaps " + SnapFrames
            + ", jitter " + JitterFrames + ", maxSnap " + MaxSnapDeg.ToString("0.0") + " deg, minDist " + MinGeomDist.ToString("0.000") + " m");
        if (InsideFrames > 0) {
            run.Problem(PlaytestSeverity.Bug, "quality.camera-blocked: camera inside geometry for " + InsideFrames + " frame(s) on " + m);
        }
        if (SnapFrames > 0) {
            run.Problem(PlaytestSeverity.Bug, "quality.camera-snap: " + SnapFrames + " frame(s) turned over " + SnapDegrees + " deg on " + m);
        }
    }
}
