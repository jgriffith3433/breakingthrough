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
        // Intended cuts (photo rig, scene load) are not this camera; only the live follow cam is measured.
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        if (CamBlock.PointInsideSolid(pos, ignoreRoot, 0.04f)) {
            InsideFrames++;
        }
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
