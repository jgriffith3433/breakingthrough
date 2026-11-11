using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.UI;

// The "bot-animation" scenario: the Yo Bots and Spider Bots as creatures (CharacterRig.cs). One run, four things to see:
//   1. 60 s of Arcade with a per-frame trace of animation state against ground speed. Zero state/motion mismatches, and the
//      speed bands show Walk where the bots move and Idle where they stand.
//   2. Flinches on every blow and a death clip of at least 0.4 s before a killed bot is removed.
//   3. A crowd of 15+ Yo Bots and Spider Bots (setup: dropped round Wallit, two slowed) on the phone fps budget, then waves 3, 5,
//      7 and 9 sampled for fps.
//   4. The wave 10 boss (the Plated Alpha): the rear-up pose held through every ground-mark tell and the stagger at phase two,
//      logged with the animation state.
// On a phone viewport (portrait, or under 500 px tall) every move is a finger on the on-screen stick, Swing and the cards.
public static partial class Playtest {
    static bool animTouch, animStickDown, animTapping;
    static float animNextSwing;
    static Vector2 animStickAt;

    const float AnimCrowdWarnFps = 30f;
    const float AnimCrowdBugFps = 20f;

    static IEnumerator BotAnimRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Log("bot-animation at " + Screen.width + "x" + Screen.height + ", " + (animTouch ? "touch" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 30f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        CharacterRig.StartTrace();
        run.Event("setup", "animation trace on: every bot's state, ground speed, playback and cycle each frame");

        // 1 and 2: a minute of ordinary Arcade.
        run.Scene("Arcade: 60 s traced");
        yield return AnimFight(player, 60f, null);
        if (run.Done) {
            yield break;
        }
        AnimReport("arcade-60s", true);

        // 3: the crowd.
        yield return AnimCrowd(player);
        if (run.Done) {
            yield break;
        }

        // Waves 3-9, sampled for fps (setup: the wave key).
        float waveFpsMin = 999f;
        int[] sampled = { 3, 5, 7, 9 };
        for (int i = 0; i < sampled.Length && !run.Done && !WaveDirector.IsLost; i++) {
            run.Event("setup", "testing wave key to wave " + sampled[i]);
            yield return GoWave(sampled[i]);
            float fpsBefore = fpsSum;
            int framesBefore = fpsFrames;
            yield return AnimFight(player, 6f, null);
            float avg = fpsFrames > framesBefore ? (fpsSum - fpsBefore) / (fpsFrames - framesBefore) : 0f;
            waveFpsMin = Mathf.Min(waveFpsMin, avg);
            run.Event("anim-wave-fps", "wave " + sampled[i] + ": " + Enemy.Roster.Count + " bots, avg " + avg.ToString("0.0") + " fps");
        }
        if (waveFpsMin < 999f) {
            run.Metric("animWaveFpsMin", Mathf.Round(waveFpsMin * 10f) / 10f);
            if (waveFpsMin < AnimCrowdBugFps) {
                run.Problem(PlaytestSeverity.Bug, "waves 3-9 dropped to " + waveFpsMin.ToString("0.0") + " fps with animated bots");
            }
        }
        if (run.Done || WaveDirector.IsLost) {
            yield break;
        }

        // 4: the wave 10 boss.
        yield return AnimBoss(player);
        if (run.Done) {
            yield break;
        }
        AnimReport("run", false);
        TouchControls.LiftBotFingers();
        PlayInput.Release();
    }

    // One batter's frame, by keys or by fingers. Skips breaks, picks cards.
    static IEnumerator AnimFight(CharacterMovement player, float seconds, System.Func<bool> stop) {
        float start = Time.realtimeSinceStartup;
        float cardAt = -1f;
        while (!run.Done && Time.realtimeSinceStartup - start < seconds && (stop == null || !stop())) {
            SampleFps();
            if (WaveDirector.IsLost) {
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                TouchControls.LiftBotFingers();
                animStickDown = false;
                PlayInput.Hold(KeyCode.Mouse0, false);
                PlayInput.Hold(KeyCode.LeftShift, false);
                if (cardAt < 0f) {
                    cardAt = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup - cardAt > 0.8f) {
                    if (animTouch) {
                        Button card = Card(Random.Range(0, 3));
                        if (card != null) {
                            Tap(card.gameObject);
                        }
                    } else {
                        PlayInput.Tap(KeyCode.Alpha1 + Random.Range(0, 3));
                    }
                    cardAt = -1f;
                }
                yield return null;
                continue;
            }
            cardAt = -1f;
            if (animTouch) {
                AnimFingers(player);
            } else {
                Batter(player, WaveDirector.IsBuilding, null, true);
                CheckLock(player);
            }
            run.Note(Enemy.Roster.Count + " bots, " + CharacterRig.Frames + " frames traced");
            yield return null;
        }
    }

    // Fingers only: the stick toward the nearest bot, Swing when it is in reach, Next wave in a break.
    static void AnimFingers(CharacterMovement player) {
        float now = Time.realtimeSinceStartup;
        if (animTapping) {
            TouchControls.BotFinger(1, Vector2.zero, false);
            TouchControls.BotFinger(3, Vector2.zero, false);
            animTapping = false;
        }
        Enemy near = GuardTarget(player, null);
        if (near != null && player != null) {
            Vector3 flat = near.transform.position - player.transform.position;
            flat.y = 0f;
            float gap = flat.magnitude - near.BodyScale * 0.3f;
            Vector2 dir = gap > BatEquipment.Reach * 0.6f ? StickToward(flat) : Vector2.zero;
            if (dir.magnitude > 0.05f) {
                if (!animStickDown) {
                    if (TouchControls.Spot("Stick", out Vector2 at)) {
                        animStickAt = at;
                        TouchControls.BotFinger(0, at, true);
                        animStickDown = true;
                    }
                } else {
                    TouchControls.BotFinger(0, animStickAt + Vector2.ClampMagnitude(dir, 1f) * TouchControls.StickReach * 0.9f, true);
                }
            } else if (animStickDown) {
                TouchControls.BotFinger(0, animStickAt, false);
                animStickDown = false;
            }
            if (gap <= BatEquipment.Reach * 0.95f && now >= animNextSwing && TouchControls.Spot("Swing", out Vector2 swing)) {
                TouchControls.BotFinger(1, swing, true);
                animTapping = true;
                animNextSwing = now + 0.2f;
            }
        } else if (animStickDown) {
            TouchControls.BotFinger(0, animStickAt, false);
            animStickDown = false;
        }
        if (WaveDirector.Waiting && now >= animNextSwing && TouchControls.Spot("Next", out Vector2 next)) {
            TouchControls.BotFinger(3, next, true);
            animTapping = true;
            animNextSwing = now + 1.5f;
        }
    }

    // Setup: ten Yo Bots and six Spider Bots round Wallit (16 with the wave's own, at least 15), two of each kind slowed. The
    // bot holds still so the footage reads the legs; fps is measured across the walk in.
    static IEnumerator AnimCrowd(CharacterMovement player) {
        if (player == null || Musician.Instance == null) {
            yield break;
        }
        run.Event("setup", "a crowd of Yo Bots and Spider Bots dropped round Wallit for the phone frame budget");
        run.Scene("Crowd: animated Yo Bots and Spider Bots");
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        Vector3 origin = player.transform.position;
        var dropped = new List<Enemy>();
        for (int i = 0; i < 16; i++) {
            float ang = i / 16f * Mathf.PI * 2f;
            Vector3 at = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (9f + (i % 4) * 1.4f);
            Enemy e = SpawnBird(at, false, i % 8 < 5 ? BotKind.YoBot : BotKind.SpiderBot);
            if (e != null) {
                dropped.Add(e);
                if (i % 8 == 0 || i % 8 == 6) {
                    e.Chill(0.5f, 12f, 0f);   // slowed: the cycle must slow with the ground speed
                }
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
        float sum = 0f, worst = 999f;
        int frames = 0;
        float until = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < until && !run.Done) {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f) {
                float fps = 1f / dt;
                sum += fps;
                frames++;
                worst = Mathf.Min(worst, fps);
            }
            SampleFps();
            yield return null;
        }
        float avg = frames > 0 ? sum / frames : 0f;
        int alive = Enemy.Roster.Count;
        run.Event("crowd-fps", alive + " animated bots on screen (" + dropped.Count + " dropped): avg " + avg.ToString("0.0") + " fps, worst " + worst.ToString("0.0") + " fps" + (animTouch ? " on a phone viewport" : ""));
        run.Metric("animCrowdBots", alive);
        run.Metric("animCrowdFpsAvg", Mathf.Round(avg * 10f) / 10f);
        run.Metric("animCrowdFpsWorst", Mathf.Round(worst * 10f) / 10f);
        if (alive < 15) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: only " + alive + " bots in the crowd (wanted 15 or more)");
        }
        if (avg < AnimCrowdBugFps) {
            run.Problem(PlaytestSeverity.Bug, "a crowd of " + alive + " animated bots averaged " + avg.ToString("0.0") + " fps (the phone budget is " + AnimCrowdBugFps.ToString("0") + "+)");
        } else if (avg < AnimCrowdWarnFps) {
            run.Problem(PlaytestSeverity.Warning, "a crowd of " + alive + " animated bots averaged " + avg.ToString("0.0") + " fps (playable, tight)");
        }
        // Now fight them: flinches and deaths in the crowd.
        yield return AnimFight(player, 14f, null);
    }

    // The wave 10 boss: the Plated Alpha (BossPhases). Fought with the same batter; each tell and the phase change are
    // read off its body in the middle of the wind-up and the stagger.
    static IEnumerator AnimBoss(CharacterMovement player) {
        run.Event("setup", "testing wave key to wave 10 (the boss)");
        yield return GoWave(10);
        if (run.Done || WaveDirector.IsLost) {
            yield break;
        }
        run.Scene("Wave 10 boss: animation");
        WaveDirector.Instance.TrimForPlaytest();
        int intros = BossSetPiece.Intros;
        yield return run.Until(() => BossSetPiece.Intros > intros, 60f, "the wave 10 boss intro");
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => !BossSetPiece.Active, 10f, "the intro to end");
        int payoffs = BossSetPiece.Payoffs;
        float start = Time.realtimeSinceStartup;
        float nextHeal = 0f;
        int tellsSeen = BossPhases.Tells, tellsLogged = BossPhases.Tells;
        float tellAt = -1f;
        bool phaseSeen = false, phaseLogged = false;
        float phaseAt = -1f;
        float bestRear = 0f, bestStagger = 0f;
        var poses = new List<string>();
        int sampledTells = 0;
        while (!run.Done && !WaveDirector.IsLost && BossSetPiece.Payoffs == payoffs && Time.realtimeSinceStartup - start < 120f) {
            float now = Time.realtimeSinceStartup;
            if (now >= nextHeal && Musician.Instance != null && Musician.Instance.Alive) {
                nextHeal = now + 4f;
                Musician.Instance.Heal(100);
            }
            DrainBossPhases();
            if (BossPhases.Tells > tellsSeen) {
                tellsSeen = BossPhases.Tells;
                tellAt = now;
            }
            if (tellAt > 0f && now - tellAt > 0.8f && tellsLogged < tellsSeen) {
                tellsLogged = tellsSeen;
                sampledTells++;
                string pose = Enemy.BossAnim();
                poses.Add(pose);
                run.Event("boss-pose", "tell " + sampledTells + ": " + pose + " (red marks " + (BossPhases.MarkUp ? "up" : "down") + ")");
            }
            if (!phaseSeen && BossPhases.Events.Exists(e => e.StartsWith("phase2|"))) {
                phaseSeen = true;
                phaseAt = now;
            }
            if (phaseSeen && !phaseLogged && now - phaseAt > 0.35f) {
                phaseLogged = true;
                run.Event("boss-pose", "phase 2: " + Enemy.BossAnim());
            }
            if (BossPhases.MarkUp) {
                // The pose weights now, for the minimum the criterion asks of every tell.
                string nowPose = Enemy.BossAnim();
                float rear = ParsePose(nowPose, "rear");
                bestRear = Mathf.Max(bestRear, rear);
            }
            bestStagger = Mathf.Max(bestStagger, ParsePose(Enemy.BossAnim(), "stagger"));
            if (LevelUpScreen.IsOpen) {
                yield return AnimFight(player, 0.1f, null);
            } else if (animTouch) {
                AnimFingers(player);
            } else {
                Batter(player, WaveDirector.IsBuilding, null, true);
                CheckLock(player);
            }
            SampleFps();
            yield return null;
        }
        DrainBossPhases();
        yield return new WaitForSecondsRealtime(1.6f);   // the death clip, then removal
        TouchControls.LiftBotFingers();
        run.Scene("Wave 10 boss defeated");
        int tells = BossPhases.Tells;
        float minPeak = 99f;
        foreach (float peak in CharacterRig.TellPeaks) {
            minPeak = Mathf.Min(minPeak, peak);
        }
        int posed = 0;
        foreach (float peak in CharacterRig.TellPeaks) {
            if (peak >= 0.8f) {
                posed++;
            }
        }
        run.Metric("animBossTells", tells);
        run.Metric("animTellPoseMin", CharacterRig.TellPeaks.Count > 0 ? Mathf.Round(minPeak * 100f) / 100f : 0f);
        run.Metric("animStaggers", CharacterRig.StaggerPeaks.Count);
        run.Event("boss-pose-summary", tells + " tells, " + posed + " of " + CharacterRig.TellPeaks.Count + " held the rear-up pose (peak at least 0.8); " + CharacterRig.StaggerPeaks.Count + " staggers, strongest " + bestStagger.ToString("0.00"));
        if (tells == 0) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: the wave 10 boss never showed a ground-mark tell");
        } else if (posed < tells - 1) {
            // The last tell may have been cut short by the boss falling; every other one must have posed.
            run.Problem(PlaytestSeverity.Bug, "bot-animation: " + (tells - posed) + " of " + tells + " boss tells had no wind-up pose on the body");
        }
        if (!phaseSeen) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: the wave 10 boss never reached phase two");
        } else if (bestStagger < 0.8f) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: the wave 10 boss did not stagger at phase two (strongest " + bestStagger.ToString("0.00") + ")");
        }
    }

    static float ParsePose(string pose, string key) {
        int at = pose.IndexOf(key + " ", System.StringComparison.Ordinal);
        if (at < 0) {
            return 0f;
        }
        at += key.Length + 1;
        int end = pose.IndexOf(' ', at);
        string number = end < 0 ? pose.Substring(at) : pose.Substring(at, end - at);
        float value;
        return float.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0f;
    }

    // The trace, read: frames by state, state against ground speed in bands, mismatches, flinches and death clips.
    static void AnimReport(string label, bool strict) {
        int frames = CharacterRig.Frames;
        var names = System.Enum.GetNames(typeof(RigState));
        var parts = new List<string>();
        for (int i = 0; i < names.Length; i++) {
            parts.Add(names[i] + " " + CharacterRig.StateFrames[i]);
        }
        run.Event("anim-trace", label + ": " + frames + " bot-frames (" + string.Join(", ", parts) + "); " + CharacterRig.Windups + " wind-ups, "
            + CharacterRig.Strikes + " strikes, " + CharacterRig.FlinchesPlayed + " flinches, " + CharacterRig.Deaths + " deaths, "
            + CharacterRig.Tells + " boss tells, " + CharacterRig.Staggers + " staggers, " + CharacterRig.Mismatches + " mismatches");

        // State against speed: for each band of ground speed, where the frames went.
        float[] edges = { 0f, 0.12f, 0.8f, 1.6f, 99f };
        var count = new int[edges.Length - 1, 7];
        var playback = new float[edges.Length - 1];
        var walking = new int[edges.Length - 1];
        for (int i = 0; i < CharacterRig.Trace.Count; i++) {
            CharacterRig.Sample s = CharacterRig.Trace[i];
            for (int b = 0; b < edges.Length - 1; b++) {
                if (s.speed >= edges[b] && s.speed < edges[b + 1]) {
                    count[b, (int)s.state]++;
                    if (s.state == RigState.Walk) {
                        playback[b] += s.playback;
                        walking[b]++;
                    }
                    break;
                }
            }
        }
        for (int b = 0; b < edges.Length - 1; b++) {
            var row = new List<string>();
            int total = 0;
            for (int st = 0; st < 7; st++) {
                total += count[b, st];
                if (count[b, st] > 0) {
                    row.Add(names[st] + " " + count[b, st]);
                }
            }
            run.Event("anim-speed-band", "ground speed " + edges[b].ToString("0.00") + " to " + (edges[b + 1] > 90f ? "more" : edges[b + 1].ToString("0.00")) + " m/s: " + total + " frames, "
                + (row.Count > 0 ? string.Join(", ", row) : "none") + (walking[b] > 0 ? "; mean walk playback x" + (playback[b] / walking[b]).ToString("0.00") + " of the clip's speed" : ""));
        }
        // A few raw lines of the trace, evenly spread, so the footage can be checked by eye.
        int shown = 0;
        int step = Mathf.Max(1, CharacterRig.Trace.Count / 10);
        for (int i = 0; i < CharacterRig.Trace.Count && shown < 10; i += step, shown++) {
            CharacterRig.Sample s = CharacterRig.Trace[i];
            run.Event("anim-sample", "t " + s.time.ToString("0.00") + " bot " + s.id + " " + s.state + " speed " + s.speed.ToString("0.00") + " playback x" + s.playback.ToString("0.00") + " cycle " + s.cycle.ToString("0.00"));
        }
        foreach (string note in CharacterRig.Notes) {
            if (note.StartsWith("mismatch") || note.StartsWith("slide")) {
                run.Event("anim-mismatch", note);
            }
        }

        run.Metric("animFrames", frames);
        run.Metric("animMismatches", CharacterRig.Mismatches);
        run.Metric("animFlinches", CharacterRig.FlinchesPlayed);
        run.Metric("animDeaths", CharacterRig.Deaths);
        float minClip = 99f;
        foreach (float clip in CharacterRig.DeathClips) {
            minClip = Mathf.Min(minClip, clip);
        }
        run.Metric("animDeathClipMinS", CharacterRig.DeathClips.Count > 0 ? Mathf.Round(minClip * 100f) / 100f : 0f);
        run.Event("anim-death", CharacterRig.DeathClips.Count + " killed bots removed, shortest death clip " + (CharacterRig.DeathClips.Count > 0 ? minClip.ToString("0.00") + " s" : "none"));
        run.Event("anim-flinch", CharacterRig.FlinchesPlayed + " hit reactions played, " + CharacterRig.Windups + " attack wind-ups before " + CharacterRig.Strikes + " blows");

        if (frames < 600) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: only " + frames + " bot-frames traced (no animated bots walked?)");
        }
        if (CharacterRig.Mismatches > 0) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: " + CharacterRig.Mismatches + " frames where a bot's animation state disagreed with its motion (" + (CharacterRig.Notes.Count > 0 ? CharacterRig.Notes[0] : "see anim-mismatch") + ")");
        }
        if (strict) {
            if (CharacterRig.FlinchesPlayed == 0) {
                run.Problem(PlaytestSeverity.Bug, "bot-animation: no hit reaction was played in 60 s of fighting");
            }
            if (CharacterRig.Deaths == 0) {
                run.Problem(PlaytestSeverity.Bug, "bot-animation: no bot died in 60 s of fighting");
            } else if (CharacterRig.DeathClips.Count == 0) {
                run.Problem(PlaytestSeverity.Bug, "bot-animation: " + CharacterRig.Deaths + " bots died and none was removed after its death clip");
            }
        }
        if (CharacterRig.DeathClips.Count > 0 && minClip < 0.4f) {
            run.Problem(PlaytestSeverity.Bug, "bot-animation: a killed bot was removed after " + minClip.ToString("0.00") + " s (the death clip must play 0.4 s)");
        }
    }
}
