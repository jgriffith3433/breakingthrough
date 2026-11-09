using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "campaign-beats" scenario (Docs/CampaignDesign.md, 8 and 8.1): Chapter 1 as its own scene. Real input throughout: the
// menu is tapped, the bot walks on the stick (WASD relative to the camera), presses E at Slice, the notices and the flag,
// holds Shift to lock on and clicks to swing. Named setup shortcuts: the sandbox case file (never the real save) and the two
// stamps dropped under Wallit's feet (one inside the Ring, one outside) so the hazard is seen in one sitting. The level's own
// timeline (LevelYard.Timeline) is replayed as events: level-start, talk, notice, pickup, encounter-wake, enemy-woke,
// hazard-hit, verb-stamp-floor, ring-safe, encounter-clear, boss-arrive, boss-wake, boss-call, boss-down, notice-down,
// exit-open, choice, level-complete; barks come from LevelBarks. The drop has a boss after the crew: the Courier (Courier.cs),
// fought with the same lock and swing, its notices logged as boss-tell / boss-land / boss-dodged / boss-interrupt / boss-phase.
public static class PlaytestBeats {
    static PlaytestRun run;
    static int drained;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Wait(float seconds) {
        yield return new WaitForSecondsRealtime(seconds);
    }

    // The level's timeline, as playtest events, in order.
    static void Drain() {
        LevelScene level = LevelScene.Instance;
        if (level == null || level.Yard == null) {
            return;
        }
        while (drained < level.Yard.Timeline.Count) {
            string raw = level.Yard.Timeline[drained++];
            int cut = raw.IndexOf('|');
            run.Event(cut > 0 ? raw.Substring(0, cut) : raw, cut > 0 ? raw.Substring(cut + 1) : "");
        }
    }

    // The Courier's own log (Courier.Events), as the boss events the other chapter playtests use.
    static int courierSeen;
    static void DrainCourier() {
        if (Courier.Events.Count < courierSeen) {
            courierSeen = 0;
        }
        for (; courierSeen < Courier.Events.Count; courierSeen++) {
            string ev = Courier.Events[courierSeen];
            string kind = ev == "phase2" ? "boss-phase" : ev.StartsWith("wind|") ? "boss-tell" : ev.StartsWith("land|") ? "boss-land"
                : ev.StartsWith("dodged|") ? "boss-dodged" : ev.StartsWith("interrupt|") ? "boss-interrupt" : "boss-attack";
            run.Event(kind, "The Courier " + ev + (Courier.MarkUp ? " (red mark up)" : ""));
        }
    }

    static bool Seen(string name) {
        LevelScene level = LevelScene.Instance;
        if (level == null) {
            return false;
        }
        foreach (string e in level.Yard.Timeline) {
            if (e.StartsWith(name + "|")) {
                return true;
            }
        }
        return false;
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    // The stick's axes as the motor reads them: locked on, forward is toward the locked target (CharacterMovement), whatever
    // the camera lags to; otherwise the camera's. Steering by the camera while locked walks Wallit in circles round the lock.
    static void StickAxes(LevelScene level, out Vector3 fwd, out Vector3 right) {
        CharacterMovement cm = level.Player.GetComponent<CharacterMovement>();
        GameObject locked = cm != null && cm.ZeldaCam != null ? cm.ZeldaCam.LockedOnGameObject : null;
        Vector3 toward = locked != null ? Flat(locked.transform.position - level.Player.position) : Vector3.zero;
        if (toward.sqrMagnitude > 0.01f) {
            fwd = toward.normalized;
            right = new Vector3(fwd.z, 0f, -fwd.x);
            return;
        }
        fwd = Flat(level.Cam.transform.forward).normalized;
        right = Flat(level.Cam.transform.right).normalized;
    }

    // One frame of walking toward a point: the stick is relative to the camera, so the world direction is turned into it.
    static void Steer(Vector3 target) {
        LevelScene level = LevelScene.Instance;
        Vector3 to = Flat(target - level.Player.position);
        if (to.magnitude < 0.05f) {
            PlayInput.Axis("Horizontal", 0f);
            PlayInput.Axis("Vertical", 0f);
            return;
        }
        StickAxes(level, out Vector3 fwd, out Vector3 right);
        Vector3 d = to.normalized;
        PlayInput.Axis("Horizontal", Mathf.Clamp(Vector3.Dot(d, right) * 1.4f, -1f, 1f));
        PlayInput.Axis("Vertical", Mathf.Clamp(Vector3.Dot(d, fwd) * 1.4f, -1f, 1f));
    }

    static void Stop() {
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
    }

    // Walks until within `near` metres of the point (flat), or `seconds` pass, or `abort` says so. Returns whether it arrived.
    static IEnumerator WalkTo(Vector3 target, float near, float seconds, System.Func<bool> abort, System.Action<bool> result) {
        float start = Time.realtimeSinceStartup;
        float lastGood = start;
        float best = 9999f;
        bool arrived = false;
        while (!run.Done && Time.realtimeSinceStartup - start < seconds && (abort == null || !abort())) {
            float d = Flat(target - LevelScene.Instance.Player.position).magnitude;
            if (d <= near) {
                arrived = true;
                break;
            }
            if (d < best - 0.5f) {
                best = d;
                lastGood = Time.realtimeSinceStartup;
            }
            Steer(target);
            Drain();
            if (Time.realtimeSinceStartup - lastGood > 7f) {
                run.Problem(PlaytestSeverity.Bug, "campaign-beats: Wallit made no headway toward " + target.ToString("0") + " for 7 s (blocked by something solid?)");
                break;
            }
            yield return null;
        }
        Stop();
        result(arrived);
    }

    // The prompt's verb, whatever key it names: "E: talk to Slice" on a keyboard, "Tap: talk to Slice" where the browser
    // reports a coarse pointer (UiFit.Touch). E works either way, so the beat only cares about the verb.
    static bool PromptVerb(string verbStart) {
        string p = LevelScene.Instance.Yard.Prompt;
        int c = p.IndexOf(": ");
        return c >= 0 && p.Substring(c + 2).StartsWith(verbStart);
    }

    static IEnumerator Use(string verbStart, float seconds = 4f) {
        LevelScene level = LevelScene.Instance;
        LevelYard y = level.Yard;
        if (verbStart == "talk" && (y.Talking || y.Talked)) {
            run.Event("talk-already", "the talk with Slice had started before E (talking " + y.Talking + ", talked " + y.Talked + ")");
            yield break;
        }
        float end = Time.realtimeSinceStartup + seconds;
        while (!run.Done && !PromptVerb(verbStart) && Time.realtimeSinceStartup < end) {
            yield return null;
        }
        if (run.Done) {
            yield break;
        }
        if (!PromptVerb(verbStart)) {
            Vector3 me = level.Player.position;
            run.Event("use-missed", "wanted \"" + verbStart + "\", prompt \"" + y.Prompt + "\", key " + Interact.Key + ", Wallit at " + me.ToString("0.0")
                + ", Slice " + Flat(y.SliceFigureAt - me).magnitude.ToString("0.0") + " m away, talking " + y.Talking + ", talked " + y.Talked
                + ", stage " + y.Stage + ", reach " + LevelYard.UseReach);
            yield return run.Until(() => PromptVerb(verbStart), 0f, "the prompt \"" + Interact.Key + ": " + verbStart + "\"");
            yield break;
        }
        if (!y.Prompt.StartsWith("E:")) {
            run.Event("prompt-key", "the prompt reads \"" + y.Prompt + "\" on the desktop run (coarse pointer or a touch seen)");
        }
        PlayInput.Tap(KeyCode.E);
        yield return Wait(0.6f);
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        drained = 0;
        CampaignSave.EnterSandbox();
        LevelBarks.Reset();
        yield return Wait(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("mode-select", "Campaign");
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        run.Event("tap", "case file 1 (sandbox)");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "Chapter 1's card");
        if (run.Done) {
            yield break;
        }
        yield return Wait(3.5f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        run.Event("tap", "Begin");
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene");
        if (run.Done) {
            yield break;
        }
        LevelScene level = LevelScene.Instance;
        string scene = SceneManager.GetActiveScene().name;
        run.Scene(scene);
        run.Event("level-scene", scene);
        run.Event("wave-director", WaveDirector.Instance == null ? "absent" : "PRESENT");
        run.Event("chapter-run", ChapterRun.Instance == null ? "absent" : "PRESENT");
        run.Metric("levelLoaded", 1);
        if (scene == MetaProgress.GameScene || WaveDirector.Instance != null || ChapterRun.Instance != null) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: Chapter 1 is the wave game again (scene " + scene + ", wave director " + (WaveDirector.Instance != null) + ")");
        }
        yield return Wait(2f);

        // 1. Explore and talk: walk the chalk up to the Ring, press E at Slice, hear her out. No bot is awake.
        bool ok = false;
        // He stops as soon as her prompt is up (inside the Ring or within LevelYard.UseReach of her), whichever he meets first.
        yield return WalkTo(LevelYard.SlicePos + new Vector3(0f, 0f, -1.5f), 0.6f, 60f, () => level.Yard.Prompt.Contains("talk to Slice"), v => ok = v);
        run.Event("explore", "walked " + Mathf.RoundToInt(level.Walked) + " m from the doors to the Ring");
        run.Event("ring-position", "Wallit at " + level.Player.position.ToString("0.0") + ", " + Flat(level.Player.position - LevelYard.SlicePos).magnitude.ToString("0.0") + " m from Slice's post, " + Flat(level.Player.position - LevelYard.RingCenter).magnitude.ToString("0.0") + " m from the Ring's centre; prompt \"" + level.Yard.Prompt + "\"");
        yield return Use("talk");
        if (!run.Done && !level.Yard.Talking && !level.Yard.Talked) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: no talk prompt for Slice at the Ring (Wallit " + level.Player.position.ToString("0.0") + ", Slice " + level.Yard.SliceFigureAt.ToString("0.0") + ")");
        }
        yield return run.Until(() => level.Yard.Talked, 70f, "the talk with Slice to end");
        Drain();
        run.Event("talk-done", "Slice joined: " + level.Yard.SliceJoined);

        // 2. The hazard: setup shortcut, a stamp dropped under him inside the Ring (nothing happens), then one outside (shove).
        run.Event("setup", "stamp dropped under Wallit inside the Ring");
        level.Yard.DropStampAt(level.Player.position, "setup: inside the Ring");
        yield return Wait(2.6f);
        Drain();
        run.Metric("ringSafe", Seen("ring-safe") ? 1 : 0);
        if (!Seen("ring-safe")) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: a stamp landed on Wallit inside the Ring and the Ring did not hold (no ring-safe)");
        }
        yield return WalkTo(LevelYard.RingCenter + new Vector3(-9f, 0f, -9f), 1.5f, 15f, null, v => ok = v);
        Vector3 before = level.Player.position;
        run.Event("setup", "stamp dropped under Wallit outside the Ring");
        level.Yard.DropStampAt(level.Player.position, "setup: outside the Ring");
        yield return Wait(2.6f);
        Drain();
        float shoved = Flat(level.Player.position - before).magnitude;
        run.Metric("stampShoveMetres", Mathf.Round(shoved * 10f) / 10f);
        run.Metric("hazardHit", Seen("hazard-hit") ? 1 : 0);
        if (!Seen("hazard-hit") || shoved < 1.5f) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the stamp floor did not shove Wallit off a mark outside the Ring (moved " + shoved.ToString("0.0") + " m)");
        }

        // 3. The west trail: three notices, then the lost Mailbox Flag. Still no bot awake.
        Vector3[] trail = {
            new Vector3(-12f, 0f, -6f), new Vector3(-17.5f, 0f, -2f),
        };
        yield return WalkTo(trail[0], 1.5f, 25f, null, v => ok = v);
        yield return WalkTo(LevelYard.NoticePos[0] + new Vector3(2f, 0f, -1.5f), 1.2f, 20f, null, v => ok = v);
        yield return Use("read");
        yield return WalkTo(LevelYard.NoticePos[1] + new Vector3(2f, 0f, -1.5f), 1.2f, 25f, null, v => ok = v);
        yield return Use("read");
        yield return WalkTo(LevelYard.NoticePos[2] + new Vector3(2f, 0f, -1.5f), 1.2f, 25f, null, v => ok = v);
        yield return Use("read");
        yield return Wait(2.5f);
        yield return WalkTo(LevelYard.FlagPos + new Vector3(1.8f, 0f, -1.2f), 1.2f, 25f, null, v => ok = v);
        bool wokeBefore = level.Yard.CrewWoken;
        yield return Use("pick up");
        Drain();
        run.Event("explore-before-combat", Mathf.RoundToInt(level.Walked) + " m walked, " + Mathf.RoundToInt(level.Seconds) + " s in the level, crew awake: " + wokeBefore);
        run.Metric("metresBeforeCombat", Mathf.Round(level.Walked));
        run.Metric("noticesRead", level.Yard.NoticesRead);
        if (wokeBefore || Seen("enemy-woke")) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: bots were awake before the flag was lifted");
        }
        if (level.Walked < 40f || !level.Yard.Talked || !level.Yard.FlagTaken) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: explore/talk/pickup before combat not all met (walked " + Mathf.RoundToInt(level.Walked) + " m, talked " + level.Yard.Talked + ", flag " + level.Yard.FlagTaken + ")");
        }

        // 4. The fight: the crew wakes, walks at him, the bat does its work, stamps fall under the bots. Then the breath, and the
        // Courier steps out from behind the drop box: the same lock and swing until it falls and the slot opens (CrewCleared).
        yield return run.Until(() => level.Yard.CrewWoken, 20f, "the crew to wake");
        if (run.Done) {
            yield break;
        }
        Drain();
        run.Metric("secondsBeforeFirstCombat", Mathf.Round(level.Yard.SecondsBeforeFirstCombat));
        float fightStart = Time.realtimeSinceStartup;
        float nextSwing = 0f;
        int swings = 0;
        float crewSeconds = -1f;
        courierSeen = Courier.Events.Count;   // anything older is another run's; the Courier clears the list when it arrives
        int defeatsSeen = 0;
        float resumedAt = -1f;
        bool rewoke = false;
        while (!run.Done && !level.Yard.CrewCleared && Time.realtimeSinceStartup - fightStart < 330f) {
            if (crewSeconds < 0f && level.Yard.CrewDown) {
                crewSeconds = Time.realtimeSinceStartup - fightStart;
                run.Event("crew-down", "the Clearing Crew is down after " + Mathf.RoundToInt(crewSeconds) + " s; the Courier comes next");
            }
            DrainCourier();
            // A defeat: Wallit is put back at the checkpoint. Slice must stand beside him again (not lie out of sight at the drop) and
            // whatever was put back to sleep must wake without the bot knowing where it is (see the idle branch below).
            if (level.Defeats > defeatsSeen && level.Health >= LevelScene.MaxHealth) {
                defeatsSeen = level.Defeats;
                resumedAt = Time.realtimeSinceStartup;
                rewoke = false;
                Companion c = Companion.Instance;
                float toSlice = c != null ? Flat(c.transform.position - level.Player.position).magnitude : -1f;
                Enemy boss = level.Yard.TheCourier;
                float toBoss = boss != null ? Flat(boss.transform.position - level.Player.position).magnitude : -1f;
                run.Event("resume-check", "defeat " + defeatsSeen + ": Slice " + (c != null && c.Alive ? "up, " : "NOT up, ") + toSlice.ToString("0.0") + " m away; the Courier " + (boss != null && boss.Dormant ? "asleep " : "awake ") + toBoss.ToString("0.0") + " m away");
                run.Metric("resumeSliceUp" + defeatsSeen, c != null && c.Alive && toSlice < 6f ? 1 : 0);
                if (c == null || !c.Alive || toSlice >= 6f) {
                    run.Problem(PlaytestSeverity.Bug, "campaign-beats: after Wallit was downed Slice did not come back with him (" + (c == null ? "gone" : c.Alive ? "up but " + toSlice.ToString("0") + " m away" : "still down") + ")");
                }
            }
            if (resumedAt >= 0f && !rewoke && level.Yard.TheCourier != null && level.Yard.CourierCame && !level.Yard.TheCourier.Dormant) {
                rewoke = true;
                run.Event("boss-rewake", "the Courier is awake again " + Mathf.RoundToInt(Time.realtimeSinceStartup - resumedAt) + " s after the resume; objective: " + level.Hud.Objective.text);
                run.Metric("courierRewakeSeconds", Mathf.Round(Time.realtimeSinceStartup - resumedAt));
            }
            Enemy nearest = null;
            float nd = 9999f;
            foreach (Enemy e in Enemy.Roster) {
                if (e == null || !e.Alive || e.Dormant) {
                    continue;
                }
                float d = Flat(e.transform.position - level.Player.position).magnitude;
                if (d < nd) {
                    nd = d;
                    nearest = e;
                }
            }
            PlayInput.Hold(KeyCode.LeftShift, nearest != null && nd < 14f);
            if (nearest != null) {
                Steer(nd > 2.1f ? nearest.transform.position : level.Player.position);
                if (nd < 2.6f && Time.realtimeSinceStartup >= nextSwing) {
                    nextSwing = Time.realtimeSinceStartup + 0.5f;
                    PlayInput.Tap(KeyCode.Mouse0);
                    swings++;
                }
            } else if (!level.Yard.CrewWoken && !level.Yard.CrewDown && level.Hud.Objective.text.Contains("drop")) {
                Steer(LevelYard.DropPos);   // a defeat during the crew fight put them back to sleep: the objective points at the drop
            } else if (level.Yard.TheCourier != null && level.Yard.TheCourier.Alive && level.Yard.TheCourier.Dormant && level.Yard.CourierCame
                && !level.Yard.CourierDown && level.Hud.Objective.text.Contains("drop")
                && Flat(level.Yard.TheCourier.transform.position - level.Player.position).magnitude > 6f && Time.realtimeSinceStartup - resumedAt > 3f) {
                Steer(level.Yard.TheCourier.transform.position);   // the objective says "Back to the drop, east": a player walks there
            } else {
                Stop();
            }
            Drain();
            yield return null;
        }
        PlayInput.Hold(KeyCode.LeftShift, false);
        Stop();
        Drain();
        DrainCourier();
        run.Metric("fightSwings", swings);
        run.Metric("fightSeconds", Mathf.Round(Time.realtimeSinceStartup - fightStart));
        run.Metric("crewSeconds", Mathf.Round(Mathf.Max(0f, crewSeconds)));
        run.Metric("fightDefeats", level.Defeats);
        run.Metric("stampsLanded", level.Yard.StampsLanded);
        run.Metric("botsStamped", level.Yard.BotsStamped);
        if (!level.Yard.CrewDown) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the crew was not cleared in 330 s (" + level.Yard.CrewAlive + " left, " + swings + " swings)");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        // The Courier: it came, it read its notices with a tell each time, it changed phase, it fell, and the slot opened.
        run.Metric("courierCame", level.Yard.CourierCame ? 1 : 0);
        run.Metric("courierWinds", Courier.Winds);
        run.Metric("courierInterrupted", Courier.Interrupted);
        run.Metric("courierBlocked", Courier.Blocked);
        run.Metric("courierLanded", Courier.Landed);
        run.Metric("courierDodged", Courier.Dodged);
        run.Metric("courierSeconds", Mathf.Round(level.Yard.CourierSeconds));
        run.Metric("bossPhase2", Courier.Angry ? 1 : 0);
        run.Metric("bossTells", Courier.Winds);
        if (!level.Yard.CourierCame) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the crew fell and the Courier never came to the drop");
        } else if (Courier.Winds == 0) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the Courier never wound up a notice (no tell, no red mark)");
        }
        if (level.Yard.CourierDown && !Courier.Angry) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the Courier fell without entering phase two");
        }
        if (!level.Yard.CrewCleared) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the Courier was not brought down in 330 s (" + Mathf.RoundToInt((level.Yard.TheCourier != null ? level.Yard.TheCourier.Health01 : 0f) * 100f) + "% health left, " + Courier.Winds + " notices, " + swings + " swings)");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("boss-defeated", "The Courier fell after " + Mathf.RoundToInt(level.Yard.CourierSeconds) + " s awake: " + Courier.Winds + " notices, " + Courier.Interrupted + " broken, " + Courier.Blocked + " taken by Wallit, " + Courier.Landed + " landed, " + Courier.Dodged + " missed; the slot is " + (level.Yard.ExitOpen ? "open" : "SHUT"));
        if (!level.Yard.ExitOpen) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the Courier is down and the return slot did not open");
        }

        // 4b. After the fight: Slice back on her feet if a notice put her down (a held E), then the Courier's notice, kept.
        Companion slice = Companion.Instance;
        if (slice != null && slice.Downed) {
            yield return WalkTo(slice.transform.position, 1.6f, 20f, null, v => ok = v);
            PlayInput.Hold(KeyCode.E, true);
            yield return run.Until(() => Companion.Instance == null || !Companion.Instance.Downed, 6f, "Slice to be revived with a held E");
            PlayInput.Hold(KeyCode.E, false);
            run.Event("revive", "Slice " + (Companion.Instance != null && Companion.Instance.Alive ? "back on her feet" : "still down"));
            if (run.Done) {
                yield break;
            }
        }
        if (level.Yard.NoticeDown) {
            yield return Wait(2.5f);   // it drifts down
            yield return WalkTo(level.Yard.NoticeAt, 1.4f, 25f, null, v => ok = v);
            yield return Use("keep", 5f);
            Drain();
            run.Metric("noticeKept", level.Yard.NoticeKept ? 1 : 0);
            if (!level.Yard.NoticeKept) {
                run.Problem(PlaytestSeverity.Polish, "campaign-beats: the Courier's notice lay on the ground and E did not keep it");
            }
        } else {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: the Courier fell and left no notice");
        }

        // 5. The way out.
        yield return Wait(4f);
        Drain();
        yield return WalkTo(LevelYard.ExitPos + new Vector3(0f, 0f, -5f), 2f, 60f, () => level.Yard.Stage == LevelYard.Beat.Done, v => ok = v);
        yield return WalkTo(LevelYard.ExitPos, 2.2f, 20f, () => level.Yard.Stage == LevelYard.Beat.Done, v => ok = v);
        yield return run.Until(() => level.Yard.Stage == LevelYard.Beat.Done, 10f, "the exit");
        Drain();
        if (run.Done) {
            yield break;
        }
        run.Event("exit-card", level.Yard.Prompt.Replace('\n', ' '));
        run.Event("pacing", "chapter 1: " + LevelYard.Encounters + " encounter (the Clearing Crew, placed asleep) and the Courier before the exit, " + Mathf.RoundToInt(level.Yard.CombatSeconds) + " s of combat in " + Mathf.RoundToInt(level.Seconds) + " s = " + Mathf.RoundToInt(level.Yard.CombatShare * 100f) + "%");
        run.Metric("pacingEncounters1", LevelYard.Encounters);
        run.Metric("pacingCombatShare1", Mathf.Round(level.Yard.CombatShare * 100f));
        if (level.Yard.CombatShare >= 0.5f) {
            run.Problem(PlaytestSeverity.Bug, "campaign-beats: Chapter 1 was " + Mathf.RoundToInt(level.Yard.CombatShare * 100f) + "% combat, the goal is under half");
        }
        yield return Wait(1.5f);
        PlayInput.Tap(KeyCode.E);
        yield return run.Until(() => SceneManager.GetActiveScene().name == CampaignSave.Scene && ChapterScene.Instance != null, 30f, "the hub after the level");
        if (run.Done) {
            yield break;
        }
        CampaignSave.Slot file = CampaignSave.Get(0);
        run.Event("hub", "back from Level_1_Yard: " + file.cleared + " chapter cleared, next is chapter " + (file.chapter + 1));
        run.Metric("chapterAdvanced", file.cleared >= 1 ? 1 : 0);

        // 6. The cast, in play: barks from three speakers, none over another, none in the fight.
        int barks = LevelBarks.Log.Count;
        foreach (string b in LevelBarks.Log) {
            string[] p = b.Split('|');
            run.Event("bark", p[0] + " (" + (p.Length > 1 ? p[1] : "") + "): " + (p.Length > 2 ? p[2] : ""));
        }
        run.Metric("barks", barks);
        run.Metric("barkSpeakers", LevelBarks.Speakers.Count);
        run.Metric("barkOverlaps", LevelBarks.Overlaps);
        if (barks < 12 || LevelBarks.Speakers.Count < 3 || LevelBarks.Overlaps > 0) {
            run.Problem(PlaytestSeverity.Polish, "campaign-beats: " + barks + " barks from " + LevelBarks.Speakers.Count + " speakers, " + LevelBarks.Overlaps + " overlaps (wanted 12, 3, 0)");
        }
        run.Finish(PlaytestOutcome.Done);
    }
}
