using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "fight-camera" scenario: 3 minutes of Arcade with the camera's fight framing measured the whole time.
//   Desktop: keys and mouse; the bot locks on (Shift) and now and then switches the lock to the next bot (right click) while
//            the view's turn is sampled every frame, so the 0.2 to 0.4 s ease is seen as numbers (lock-ease).
//   Phone (portrait, or under 500 px tall): fingers only. The camera soft-locks the nearest bot by itself (no Lock button);
//            the bot must keep fighting through wave 3 and Slice, the target and Wallit must stay above the pads.
// Metrics: fight.framedPct, feel.cameraClipSeconds, feel.cameraSnaps (CameraFeel).
public static partial class Playtest {
    static IEnumerator FightCameraRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
        }
        run.Log("fight-camera at " + Screen.width + "x" + Screen.height + ", " + (animTouch ? "touch, soft-lock" : "keys and mouse, Shift lock"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 30f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        CameraFeel feel = CameraFeel.Ensure(Camera.main);
        if (feel == null) {
            run.Problem(PlaytestSeverity.Bug, "fight-camera: no camera feel detector on the main camera");
            yield break;
        }
        feel.ResetCounts("arcade");
        ZeldaStyleCamera.FeelLockEases = 0;
        run.Event("setup", "camera feel detector reset: fight frames, framed share, clip seconds and snaps counted from here");

        float start = Time.realtimeSinceStartup;
        float total = 180f;
        float nextSwitch = start + 8f;
        float lastKillAt = start;
        int lastKills = RunLog.RunKills;
        int lastWave = WaveDirector.Wave;
        bool wave3 = false;
        int ease = 0;
        float stallFrom = -1f;
        while (!run.Done && !WaveDirector.IsLost) {
            float now = Time.realtimeSinceStartup;
            if (now - start >= total && (!animTouch || wave3 || now - start >= total + 60f)) {
                break;
            }
            // One chunk of fighting at a time, so the lock swap can be sampled between chunks.
            yield return AnimFight(player, 1f, null);
            if (RunLog.RunKills != lastKills) {
                lastKills = RunLog.RunKills;
                lastKillAt = now;
                stallFrom = -1f;
            }
            if (WaveDirector.Wave != lastWave) {
                lastWave = WaveDirector.Wave;
                run.Event("wave", lastWave.ToString());
                run.Scene("Wave " + lastWave);
            }
            if (!wave3 && WaveDirector.Wave >= 4) {
                wave3 = true;
                run.Event("phone-wave3", (animTouch ? "touch: " : "keys: ") + "wave 3 cleared at " + (now - start).ToString("0") + " s with the camera's lock on the nearest bot");
            }
            if (Enemy.Roster.Count > 0 && now - lastKillAt > 20f && stallFrom < 0f) {
                stallFrom = now;
                Enemy n = player != null ? GuardTarget(player, null) : null;
                float gapNow = n != null ? Vector3.Distance(n.transform.position, player.transform.position) : -1f;
                run.Event("stall", "no kill for 20 s: " + Enemy.Roster.Count + " bots, nearest guard target " + gapNow.ToString("0.0") + " m, lock " + (Juice.Cam != null && Juice.Cam.LockedOnGameObject != null ? Juice.Cam.LockedOnGameObject.name : "none") + ", reach " + BatEquipment.Reach.ToString("0.0"));
                run.Problem(PlaytestSeverity.Bug, "fight-camera: the bot made no kill for 20 s at wave " + WaveDirector.Wave + (animTouch ? " on touch" : ""));
            }
            if (!animTouch && ease < 6 && now >= nextSwitch && Enemy.Roster.Count >= 2 && Juice.Cam != null && Juice.Cam.LockedOnGameObject != null) {
                nextSwitch = now + 10f;
                yield return SampleLockSwitch();
                ease++;
            }
            run.Progress(Mathf.Clamp01((now - start) / total));
            run.Note(Enemy.Roster.Count + " bots, framed " + feel.FramedPct.ToString("0") + "%");
        }
        TouchControls.LiftBotFingers();
        PlayInput.Release();
        feel.Report(run);
        run.Event("framing", "fight frames " + feel.FightFrames + ", framed " + feel.FramedPct.ToString("0.0") + "%, clip " + feel.ClipSeconds.ToString("0.000") + " s, snaps " + feel.SnapFrames + ", cuts " + CameraFeel.Cuts + (feel.LastUnframed != null ? ", last unframed: " + feel.LastUnframed : ""));
        if (feel.FightFrames > 60 && feel.FramedPct < 90f) {
            run.Problem(PlaytestSeverity.Bug, "fight.framedPct " + feel.FramedPct.ToString("0.0") + " is under 90");
        }
        if (animTouch && !wave3) {
            run.Problem(PlaytestSeverity.Bug, "fight-camera: wave 3 was not finished on touch (wave " + WaveDirector.Wave + ")");
        }
    }

    // Right click moves the lock to the next bot; the main camera's turn is read every frame for 0.7 s.
    static IEnumerator SampleLockSwitch() {
        Camera cam = Camera.main;
        if (cam == null) {
            yield break;
        }
        int switches = ZeldaStyleCamera.LockSwitches;
        Quaternion from = cam.transform.rotation;
        PlayInput.Tap(KeyCode.Mouse1);
        float t0 = Time.realtimeSinceStartup;
        float worst = 0f;
        float total = 0f;
        float[] ang = new float[80];
        float[] at = new float[80];
        int n = 0;
        Quaternion last = from;
        while (Time.realtimeSinceStartup - t0 < 0.7f && n < ang.Length) {
            yield return null;
            Quaternion q = cam.transform.rotation;
            worst = Mathf.Max(worst, Quaternion.Angle(last, q));
            last = q;
            ang[n] = Quaternion.Angle(from, q);
            at[n] = Time.realtimeSinceStartup - t0;
            total = Mathf.Max(total, ang[n]);
            n++;
        }
        if (ZeldaStyleCamera.LockSwitches == switches) {
            run.Event("lock-ease", "right click found no next bot to switch to");
            yield break;
        }
        float t90 = 0.7f;
        for (int i = 0; i < n; i++) {
            if (ang[i] >= total * 0.9f) {
                t90 = at[i];
                break;
            }
        }
        run.Event("lock-ease", "the lock moved to the next bot: the view turned " + total.ToString("0.0") + " deg, 90% of it by " + t90.ToString("0.00") + " s, worst frame " + worst.ToString("0.0") + " deg (eases over " + ZeldaStyleCamera.LockEaseSeconds.ToString("0.00") + " s)");
        run.Metric("feel.lockEaseTurnDeg", Mathf.Round(total * 10f) / 10f);
        run.Metric("feel.lockEaseT90", Mathf.Round(t90 * 100f) / 100f);
    }
}
