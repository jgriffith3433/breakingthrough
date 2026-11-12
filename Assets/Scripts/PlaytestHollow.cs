using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "hollow-landmark" scenario (docs/world-bible.md, A): the ruin of the Dead Letter Office behind the circle, and the rim dressing.
//   1. In the first frames of an Arcade run the office is up and its clock projects onto the play camera (landmark-view); on a phone
//      (390x844) it sits in the upper half of the view (landmark-phone).
//   2. The dressing: at least 12 props, none floating or sunk, none inside the 150 m the bots walk (prop-audit, props-clear).
//   3. The rim reads as hedge and picket on all four sides (boundary-sides).
//   4. Draw calls of the whole place stay under the phone budget (hollowDrawCalls).
//   5. 15 s of play with the place up, then a second look at the landmark.
public static partial class Playtest {
    static IEnumerator HollowLandmarkRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        yield return run.Until(() => WaveDirector.Instance != null && ControlsHelp.PlayedSeconds > 0f, 20f, "Arcade to start");
        yield return run.Until(() => HollowPostOffice.Built, 10f, "the Dead Letter Office to be built");
        if (run.Done) {
            yield break;
        }
        bool phone = UiFit.Touch || Screen.height > Screen.width;
        yield return null;
        HollowView("first-frame", phone);
        run.Scene("hollow-first-frame");

        run.Metric("hollowProps", HollowPostOffice.Props);
        run.Metric("hollowFloating", HollowPostOffice.Floating);
        run.Metric("hollowSunk", HollowPostOffice.Sunk);
        run.Metric("hollowWorstFloatCm", Mathf.Round(HollowPostOffice.WorstFloat * 100f));
        run.Metric("hollowWorstSinkCm", Mathf.Round(-HollowPostOffice.WorstSink * 100f));
        run.Metric("hollowLampPosts", HollowPostOffice.LampPosts);
        run.Metric("hollowParcels", HollowPostOffice.Parcels);
        run.Metric("hollowDrawCalls", HollowPostOffice.DrawCalls);
        run.Metric("hollowMinPropDistance", Mathf.Round(HollowPostOffice.MinDistanceFromCircle));
        run.Event("prop-audit", HollowPostOffice.Props + " props, " + HollowPostOffice.Floating + " floating, " + HollowPostOffice.Sunk + " sunk (worst float " + (HollowPostOffice.WorstFloat * 100f).ToString("0") + " cm, worst sink " + (-HollowPostOffice.WorstSink * 100f).ToString("0") + " cm)");
        if (HollowPostOffice.Props < 12) {
            run.Problem(PlaytestSeverity.Bug, "hollow-landmark: only " + HollowPostOffice.Props + " dressing props");
        }
        if (HollowPostOffice.Floating + HollowPostOffice.Sunk > 0) {
            run.Problem(PlaytestSeverity.Bug, "hollow-landmark: " + HollowPostOffice.Floating + " props float and " + HollowPostOffice.Sunk + " sink");
        }
        bool clear = HollowPostOffice.MinDistanceFromCircle >= 150f;
        run.Event("props-clear", "nearest prop " + HollowPostOffice.MinDistanceFromCircle.ToString("0") + " m from the circle (bots walk inside 150 m)");
        if (!clear) {
            run.Problem(PlaytestSeverity.Bug, "hollow-landmark: a prop stands " + HollowPostOffice.MinDistanceFromCircle.ToString("0") + " m from the circle, inside the bots' ground");
        }
        string sides = "N " + HollowPostOffice.SideHedges[0] + " E " + HollowPostOffice.SideHedges[1] + " S " + HollowPostOffice.SideHedges[2] + " W " + HollowPostOffice.SideHedges[3];
        run.Event("boundary-sides", "hedge and picket runs per side: " + sides);
        for (int i = 0; i < 4; i++) {
            run.Metric("hollowSide" + i, HollowPostOffice.SideHedges[i]);
            if (HollowPostOffice.SideHedges[i] < 20) {
                run.Problem(PlaytestSeverity.Bug, "hollow-landmark: side " + i + " of the rim has only " + HollowPostOffice.SideHedges[i] + " hedge runs");
            }
        }
        if (HollowPostOffice.DrawCalls > 60) {
            run.Problem(PlaytestSeverity.Bug, "hollow-landmark: " + HollowPostOffice.DrawCalls + " estimated draw calls for the dressing, over the phone budget of 60");
        }

        yield return ActsPlay(player, 15f, null);
        HollowView("after-play", phone);
        run.Scene("hollow-after-play");
        run.Event("hollow-done", "landmark and rim checked");
    }

    // Where the clock lands on the play camera, and whether Slice or a bot stands between: a 0..1 viewport point, y up.
    static void HollowView(string when, bool phone) {
        Camera cam = Camera.main;
        if (cam == null) {
            run.Problem(PlaytestSeverity.Bug, "hollow-landmark: no main camera");
            return;
        }
        Vector3 v = cam.WorldToViewportPoint(HollowPostOffice.ClockPoint);
        bool inView = v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
        float distance = new Vector2(HollowPostOffice.LandmarkBase.x - HollowPostOffice.Circle.x, HollowPostOffice.LandmarkBase.z - HollowPostOffice.Circle.z).magnitude;
        run.Event("landmark-view", when + ": the clock is at viewport (" + v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ") " + (inView ? "in view" : "OUT of view") + ", " + distance.ToString("0") + " m behind the circle, camera pitch " + cam.transform.eulerAngles.x.ToString("0"));
        run.Metric("landmarkViewY_" + when, Mathf.Round(v.y * 100f) / 100f);
        run.Metric("landmarkInView_" + when, inView ? 1 : 0);
        if (when == "first-frame" && !inView) {
            run.Problem(PlaytestSeverity.Bug, "hollow-landmark: the post office is not in the first frame of the run");
        }
        if (phone) {
            bool upper = inView && v.y >= 0.5f;
            run.Event("landmark-phone", when + ": clock at viewport y " + v.y.ToString("0.00") + (upper ? " (upper half)" : " (NOT in the upper half)"));
            if (!upper) {
                run.Problem(PlaytestSeverity.Bug, "hollow-landmark: on the phone view the landmark is not in the upper half (" + when + ")");
            }
        }
    }
}
