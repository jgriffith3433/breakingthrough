using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "place-audio" scenario (docs/world-bible.md, Areas; PlaceAudio.cs): the Moonlit Hollow's own air. Logs the bed that plays
// (ambience event) and the spot emitters on real objects (spot events), walks Wallit toward the nearest spot on the keys (or the
// stick on a phone viewport) and logs the spot's gain at the start and end of the walk (it must rise), counts footsteps per
// surface (grass outside the circle, stone on its flags; none twice in a row), a jump landing, and samples the bed while a voice
// line holds the floor and while it does not (it must dip). Hub and chapters play the same code through their own scenarios.
public static partial class Playtest {
    static IEnumerator PlaceAudioRun(PlaytestOptions options) {
        bool touch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(touch);
        run.Event("place-audio-setup", touch ? "touch 390x844, stick finger" : "keys");
        yield return run.Until(() => WaveDirector.Instance != null && PlaceAudio.Instance != null && PlaceAudio.Instance.SpotCount > 0, 40f, "the Hollow's spots");
        if (run.Done) {
            yield break;
        }
        PlaceAudio place = PlaceAudio.Instance;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        run.Scene("The Hollow's air");
        run.Event("ambience", "bed " + MusicDirector.AmbienceCurrent + ", playing " + MusicDirector.AmbiencePlaying + ", volume " + MusicDirector.AmbienceVolume.ToString("0.00"));
        for (int i = 0; i < place.SpotCount; i++) {
            run.Event("spot", place.SpotName(i) + " at " + place.SpotPosition(i).ToString("0") + ", gain " + place.GainOf(i, player.transform.position).ToString("0.00"));
        }

        // Walk toward the nearest spot and watch it get louder.
        int near = 0;
        float best = float.MaxValue;
        for (int i = 0; i < place.SpotCount; i++) {
            float d = (place.SpotPosition(i) - player.transform.position).magnitude;
            if (d < best) {
                best = d;
                near = i;
            }
        }
        float gainStart = place.GainOf(near, player.transform.position);
        float stop = Time.realtimeSinceStartup + 25f;
        bool stickDown = false;
        Vector2 stickAt = Vector2.zero;
        while (!run.Done && Time.realtimeSinceStartup < stop && !WaveDirector.IsLost) {
            Vector3 flat = place.SpotPosition(near) - player.transform.position;
            flat.y = 0f;
            if (flat.magnitude < 5f) {
                break;
            }
            Vector2 dir = StickToward(flat);
            if (touch) {
                if (!stickDown && TouchControls.Spot("Stick", out stickAt)) {
                    TouchControls.BotFinger(0, stickAt, true);
                    stickDown = true;
                } else if (stickDown) {
                    TouchControls.BotFinger(0, stickAt + Vector2.ClampMagnitude(dir, 1f) * TouchControls.StickReach * 0.9f, true);
                }
            } else {
                PlayInput.Hold(KeyCode.W, dir.y > 0.35f);
                PlayInput.Hold(KeyCode.S, dir.y < -0.35f);
                PlayInput.Hold(KeyCode.D, dir.x > 0.35f);
                PlayInput.Hold(KeyCode.A, dir.x < -0.35f);
            }
            yield return null;
        }
        if (touch) {
            TouchControls.BotFinger(0, stickAt, false);
        } else {
            foreach (KeyCode k in new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D }) {
                PlayInput.Hold(k, false);
            }
        }
        float gainEnd = place.GainOf(near, player.transform.position);
        run.Event("spot-approach", place.SpotName(near) + " gain " + gainStart.ToString("0.00") + " -> " + gainEnd.ToString("0.00") + (gainEnd > gainStart ? " (louder)" : " (NOT louder)"));
        run.Metric("placeSpotGainStart", Mathf.Round(gainStart * 100f) / 100f);
        run.Metric("placeSpotGainEnd", Mathf.Round(gainEnd * 100f) / 100f);

        // The bed under a voice line and without one.
        float quiet = 0f, voiced = 0f;
        int nq = 0, nv = 0;
        float until = Time.realtimeSinceStartup + 50f;
        while (!run.Done && Time.realtimeSinceStartup < until && (nq < 30 || nv < 30)) {
            bool voice = !WallitVoice.FloorFree || CastVoice.Speaking;
            if (voice && nv < 30) {
                voiced += MusicDirector.AmbienceVolume;
                nv++;
            } else if (!voice && nq < 30) {
                quiet += MusicDirector.AmbienceVolume;
                nq++;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
        if (nq > 0 && nv > 0) {
            run.Event("bed-duck", "bed " + (quiet / nq).ToString("0.000") + " with the floor free, " + (voiced / nv).ToString("0.000") + " under a voice line");
        } else {
            run.Event("bed-duck", "no voice line in the window; bed " + (nq > 0 ? (quiet / nq).ToString("0.000") : "-"));
        }

        run.Event("footsteps", "steps " + PlaceAudio.Steps + " (grass " + PlaceAudio.StepsOn[0] + ", stone " + PlaceAudio.StepsOn[1] + ", wood " + PlaceAudio.StepsOn[2] + ", metal " + PlaceAudio.StepsOn[3]
            + "), landings " + PlaceAudio.Lands + ", same variant twice in a row " + GameAudio.Repeats + ", last surface " + PlaceAudio.LastSurface);
        run.Metric("placeSteps", PlaceAudio.Steps);
        run.Metric("placeLandings", PlaceAudio.Lands);
        foreach (string line in PlaceAudio.Log) {
            run.Event("place-log", line);
        }
        run.Finish(PlaytestOutcome.Done);
    }
}
