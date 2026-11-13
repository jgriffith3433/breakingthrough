using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "explore" scenario: walks every live map on the stick (Arcade, chapters 1-6, the hub), hugging what stops it, and
// reports stuck cells, falls, reachability and the per-frame camera feel detector (inside geometry, near 0.15 m, snaps
// over 30 deg, jitter). Also probes acceptance criteria: corner pull-in, 2 s idle recentre, lock-on ease. Real input only.
// Chapter 1 is Level_1_Yard (LevelScene.ChapterOneIsLevel must be on for explore, or Begin loads the old chapter run and
// this bot hangs waiting for a level scene that never starts).
public static class PlaytestExplore {
    static PlaytestRun run;
    const int StickFinger = 1;
    static bool stickDown;
    static bool loggedFirstMove;

    public static void Publish(PlaytestRun r) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        var cam = Camera.main;
        var chapter = ChapterRun.Instance;
        var level = LevelScene.Instance;
        var sb = new System.Text.StringBuilder();
        if (level != null) {
            sb.Append("chapter=Level_").Append(level.Chapter + 1).Append(" scene=").Append(level.SceneName);
            sb.Append(" objective=yard");
        } else {
            sb.Append("chapter=").Append(chapter == null ? "none" : chapter.name);
            sb.Append(" objective=").Append(chapter == null ? "" : chapter.ObjectiveLine);
        }
        if (player != null) {
            Vector3 p = player.transform.position;
            var cc = player.GetComponent<CharacterController>();
            sb.Append(" pos=").Append(p.x.ToString("0.0")).Append(',').Append(p.y.ToString("0.0")).Append(',').Append(p.z.ToString("0.0"));
            sb.Append(" grounded=").Append(cc != null && cc.isGrounded);
        }
        if (cam != null) {
            Vector3 c = cam.transform.position;
            sb.Append(" camera=").Append(c.x.ToString("0.0")).Append(',').Append(c.y.ToString("0.0")).Append(',').Append(c.z.ToString("0.0"));
            sb.Append(" fov=").Append(ZeldaStyleCamera.FeelFov.ToString("0.0"));
            sb.Append(" followDist=").Append(ZeldaStyleCamera.FeelFollowDist.ToString("0.00"));
        }
        r.Note(sb.ToString());
    }

    static void Steer(Vector3 dir) {
        var cam = Camera.main;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float h = Mathf.Clamp(Vector3.Dot(dir, right) * 1.4f, -1f, 1f);
        float v = Mathf.Clamp(Vector3.Dot(dir, fwd) * 1.4f, -1f, 1f);
        if (UiFit.Touch) {
            // Phone explore: move through the on-screen stick so the first touch move is real finger input.
            if (h * h + v * v < 0.0004f) {
                RestStick();
                return;
            }
            if (!TouchControls.Spot("Stick", out Vector2 centre)) {
                RestStick();
                return;
            }
            Vector2 push = new Vector2(h, v);
            Vector2 at = stickDown
                ? centre + push.normalized * Mathf.Min(1f, push.magnitude) * TouchControls.StickReach * 0.9f
                : centre;
            TouchControls.BotFinger(StickFinger, at, true);
            stickDown = true;
            NoteFirstMove(true);
            return;
        }
        PlayInput.Axis("Horizontal", h);
        PlayInput.Axis("Vertical", v);
        if (h * h + v * v > 0.01f) {
            NoteFirstMove(false);
        }
    }

    static void RestStick() {
        if (stickDown) {
            TouchControls.BotFinger(StickFinger, Vector2.zero, false);
            stickDown = false;
        }
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
    }

    static void NoteFirstMove(bool touch) {
        if (loggedFirstMove || run == null) {
            return;
        }
        loggedFirstMove = true;
        string map = SceneManager.GetActiveScene().name;
        run.Event("first-move", (touch ? "touch-stick" : "axes") + " on " + map);
        run.Metric("explore.firstMove." + map, 1);
        if (touch) {
            run.Metric("explore.touchMoveRegistered", 1);
            bool stickUp = TouchControls.Spot("Stick", out Vector2 _);
            run.Event("touch-stick", stickUp ? "on screen under the thumb" : "missing");
            run.Metric("explore.touchStickShown", stickUp ? 1 : 0);
            if (!stickUp) {
                run.Problem(PlaytestSeverity.Bug, "explore: touch Stick pad not on screen after entering " + map);
            }
        }
        run.Progress(0.35f);
    }

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Wait(float seconds) {
        yield return new WaitForSecondsRealtime(seconds);
    }

    static Vector3 Flat(Vector3 v) {
        return new Vector3(v.x, 0f, v.z);
    }

    // Full explore pass: Arcade, then each chapter, then the hub. Numbers are keyed by map name.
    public static IEnumerator RunAll(PlaytestRun r) {
        run = r;
        CampaignSave.EnterSandbox();
        loggedFirstMove = false;
        stickDown = false;
        // Phone explore (390x844 portrait, or any forced/coarse touch): show the stick so Steer uses BotFinger.
        if (UiFit.Touch || (Screen.height > Screen.width && Screen.width <= 500)) {
            UiFit.ForceTouch(true);
        }
        run.Event("setup", "explore: camera feel detector on; sandbox case file; every map walked; ChapterOneIsLevel=" + LevelScene.ChapterOneIsLevel + "; touch=" + UiFit.Touch);
        ZeldaStyleCamera.FeelMaxLockSnapDeg = 0f;
        ZeldaStyleCamera.FeelPullInMeters = 0f;
        ZeldaStyleCamera.FeelRecentred = false;

        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 20f, "Arcade wave");
        if (!run.Done) {
            yield return MapPass("arcade");
        }

        for (int index = 0; index < 6 && !run.Done; index++) {
            float leaveArcadeAt = Time.realtimeSinceStartup;
            yield return LoadChapter(index);
            if (run.Done) {
                yield break;
            }
            string map = index == 0
                ? SceneManager.GetActiveScene().name
                : (ChapterMap.Current != null ? ChapterMap.Current.MapName : "chapter" + (index + 1));
            if (index == 0) {
                float toCh1 = Time.realtimeSinceStartup - leaveArcadeAt;
                run.Metric("explore.arcadeToChapter1Seconds", Mathf.Round(toCh1 * 10f) / 10f);
                if (toCh1 > 30f) {
                    run.Problem(PlaytestSeverity.Bug, "explore: Arcade to Chapter 1 took " + toCh1.ToString("0.0") + " s (wanted under 30)");
                }
            }
            loggedFirstMove = false;
            yield return MapPass(map);
            yield return LeaveToMenu();
        }

        if (!run.Done) {
            yield return LoadHub();
            if (!run.Done) {
                yield return MapPass("hub");
            }
        }

        run.Metric("feel.fov", ZeldaStyleCamera.FeelFov);
        run.Metric("feel.cameraPullInMeters", Mathf.Round(ZeldaStyleCamera.FeelPullInMeters * 100f) / 100f);
        run.Metric("feel.maxLockSnapDeg", Mathf.Round(ZeldaStyleCamera.FeelMaxLockSnapDeg * 10f) / 10f);
        run.Metric("feel.recentred", ZeldaStyleCamera.FeelRecentred ? 1 : 0);
        if (ZeldaStyleCamera.FeelFov < 60f || ZeldaStyleCamera.FeelFov > 75f) {
            run.Problem(PlaytestSeverity.Bug, "explore: camera FOV " + ZeldaStyleCamera.FeelFov.ToString("0.0") + " outside 60-75");
        }
        if (!run.Done) {
            run.Event("explore-all-done", "every map explored with camera-feel numbers");
        }
    }

    // One map: short ring walk + corner / idle / lock probes, then report camera-feel metrics.
    static IEnumerator MapPass(string map) {
        CameraFeel feel = CameraFeel.Ensure();
        if (feel != null) {
            feel.ResetCounts(map);
        }
        run.Event("map-enter", map);
        run.Progress(map == "arcade" ? 0.1f : 0.3f);
        yield return Run(run, map, shortPass: true);
        if (run.Done) {
            yield break;
        }
        yield return FeelProbes(map);
        if (feel != null) {
            feel.Report(run);
        }
        Publish(run);
    }

    public static IEnumerator Run(PlaytestRun r) {
        yield return Run(r, SceneManager.GetActiveScene().name, shortPass: false);
    }

    public static IEnumerator Run(PlaytestRun r, string map, bool shortPass) {
        yield return r.Until(() => Object.FindAnyObjectByType<CharacterMovement>() != null, 20f, "player");
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null) {
            r.Problem(PlaytestSeverity.Bug, "explore: no player on map " + map);
            yield break;
        }
        Transform t = player.transform;
        Vector3 origin = t.position;
        float floor = origin.y;
        var stuck = new HashSet<Vector2Int>();
        int falls = 0;
        int probes = 0;
        int reached = 0;
        float[] radii = shortPass ? new[] { 12f } : new[] { 10f, 25f, 45f, 70f };
        int headings = shortPass ? 4 : 8;
        for (int ri = 0; ri < radii.Length && !r.Done; ri++) {
            for (int k = 0; k < headings && !r.Done; k++) {
                float a = k * (Mathf.PI * 2f / headings);
                Vector3 goal = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radii[ri];
                probes++;
                float best = Vector3.Distance(Flat(t.position), Flat(goal));
                float lastGain = Time.realtimeSinceStartup;
                float start = Time.realtimeSinceStartup;
                bool ok = false;
                float budget = shortPass ? 9f : 25f;
                while (Time.realtimeSinceStartup - start < budget) {
                    Vector3 to = Flat(goal) - Flat(t.position);
                    float d = to.magnitude;
                    if (d < 2.5f) {
                        ok = true;
                        break;
                    }
                    if (t.position.y < floor - 15f) {
                        falls++;
                        r.Event("fall", map + " at " + t.position.ToString("0") + " heading " + k);
                        t.position = origin;
                        break;
                    }
                    if (d < best - 0.5f) {
                        best = d;
                        lastGain = Time.realtimeSinceStartup;
                    } else if (Time.realtimeSinceStartup - lastGain > 3f) {
                        var cell = new Vector2Int(Mathf.RoundToInt(t.position.x / 5f), Mathf.RoundToInt(t.position.z / 5f));
                        if (stuck.Add(cell)) {
                            r.Event("stuck-cell", map + " " + cell + " toward " + goal.ToString("0"));
                        }
                        Steer(Quaternion.Euler(0f, 90f, 0f) * to.normalized);
                        PlayInput.Tap(KeyCode.Space);
                        yield return new WaitForSecondsRealtime(1f);
                        break;
                    }
                    Steer(to.normalized);
                    if (Time.frameCount % 30 == 0) {
                        Publish(r);
                    }
                    yield return null;
                }
                RestStick();
                if (ok) {
                    reached++;
                }
                float back = Time.realtimeSinceStartup;
                float backBudget = shortPass ? 5f : 12f;
                while (Time.realtimeSinceStartup - back < backBudget && Flat(t.position - origin).magnitude > 3f) {
                    Steer(Flat(origin - t.position).normalized);
                    yield return null;
                }
            }
        }
        RestStick();
        float reach = probes == 0 ? 0f : 100f * reached / probes;
        r.Event("map-explore", map + ": stuck " + stuck.Count + ", falls " + falls + ", reachable " + reach.ToString("0") + "% of " + probes);
        r.Metric("explore.stuckCells." + map, stuck.Count);
        r.Metric("explore.falls." + map, falls);
        r.Metric("explore.reachablePct." + map, reach);
        if (falls > 0) {
            r.Problem(PlaytestSeverity.Bug, "explore: fell through " + map + " " + falls + " time(s)");
        }
        if (stuck.Count > 0) {
            r.Problem(PlaytestSeverity.Warning, "explore: " + stuck.Count + " stuck cell(s) on " + map);
        }
    }

    // Acceptance probes: corner pull-in, idle recentre, lock-on ease. Real stick and keys only.
    static IEnumerator FeelProbes(string map) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        var cam = Object.FindAnyObjectByType<ZeldaStyleCamera>();
        if (player == null || cam == null) {
            run.Problem(PlaytestSeverity.Warning, "explore: feel probes skipped on " + map + " (no player or camera)");
            yield break;
        }
        Transform t = player.transform;
        CameraFeel feel = CameraFeel.Ensure();

        // Corner: walk backward into whatever is behind, hold look into the wall, watch followDist drop without a snap.
        float distBefore = ZeldaStyleCamera.FeelFollowDist;
        float pullBefore = ZeldaStyleCamera.FeelPullInMeters;
        int insideBefore = feel != null ? feel.InsideFrames : 0;
        int snapBefore = feel != null ? feel.SnapFrames : 0;
        float end = Time.realtimeSinceStartup + 2.2f;
        while (Time.realtimeSinceStartup < end) {
            PlayInput.Axis("Vertical", -1f);
            PlayInput.Axis("Mouse Y", 0.4f);
            yield return null;
        }
        PlayInput.Axis("Vertical", 0f);
        PlayInput.Axis("Mouse Y", 0f);
        yield return Wait(0.4f);
        float pulled = ZeldaStyleCamera.FeelPullInMeters - pullBefore;
        float distAfter = ZeldaStyleCamera.FeelFollowDist;
        bool pulledIn = pulled > 0.05f || distAfter < distBefore - 0.15f;
        run.Event("camera-corner", map + ": pullIn " + pulled.ToString("0.00") + " m, follow " + distBefore.ToString("0.00")
            + " -> " + distAfter.ToString("0.00") + ", inside+" + ((feel != null ? feel.InsideFrames : 0) - insideBefore)
            + ", snaps+" + ((feel != null ? feel.SnapFrames : 0) - snapBefore));
        run.Metric("feel.cornerPulled." + map, pulledIn ? 1 : 0);
        if (!pulledIn && map != "arcade") {
            // Arcade's open field often has nothing behind Wallit; chapters and the hub should.
            run.Problem(PlaytestSeverity.Warning, "explore: corner pull-in not seen on " + map);
        }
        if (feel != null && feel.InsideFrames > insideBefore) {
            run.Problem(PlaytestSeverity.Bug, "quality.camera-blocked: corner probe put the camera inside geometry on " + map);
        }
        if (feel != null && feel.SnapFrames > snapBefore) {
            run.Problem(PlaytestSeverity.Bug, "quality.camera-snap: corner probe snapped the camera on " + map);
        }

        // Idle recentre: yaw away from behind Wallit, then stop look and move for just over 2 s.
        ZeldaStyleCamera.FeelRecentred = false;
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
        float yawEnd = Time.realtimeSinceStartup + 0.45f;
        while (Time.realtimeSinceStartup < yawEnd) {
            PlayInput.Axis("Mouse X", 1f);
            yield return null;
        }
        PlayInput.Axis("Mouse X", 0f);
        // Recentre arms after 2 s idle, then needs another ~1–1.5 s to ease ~90 deg back behind Wallit.
        float idleEnd = Time.realtimeSinceStartup + 4.5f;
        while (Time.realtimeSinceStartup < idleEnd) {
            yield return null;
        }
        run.Event("camera-idle", map + ": recentred=" + ZeldaStyleCamera.FeelRecentred);
        run.Metric("feel.idleRecentre." + map, ZeldaStyleCamera.FeelRecentred ? 1 : 0);
        if (!ZeldaStyleCamera.FeelRecentred) {
            run.Problem(PlaytestSeverity.Bug, "explore: camera did not recentre behind Wallit after 2 s idle on " + map);
        }

        // Lock-on ease: hold Shift on the nearest living bot (spawn a Yo Bot if the roster is empty), release, watch snap deg.
        Enemy target = NearestBot(t.position, awakeOnly: true);
        if (target == null) {
            Enemy sleeper = NearestBot(t.position, awakeOnly: false);
            if (sleeper != null) {
                run.Event("setup", map + ": wake a dormant bot for the lock-on ease probe");
                sleeper.Dormant = false;
                target = sleeper;
            }
        }
        if (target == null && WaveDirector.Instance != null) {
            run.Event("setup", map + ": drop a Yo Bot for the lock-on ease probe");
            WaveDirector.Instance.SpawnPlaced(BotKind.YoBot, t.position + t.forward * 4f + Vector3.up * 0.2f);
            yield return Wait(0.3f);
            target = NearestBot(t.position, awakeOnly: true);
        }
        if (target == null) {
            run.Event("camera-lock", map + ": no bot to lock (skipped)");
            run.Metric("feel.lockEase." + map, 0);
            yield break;
        }
        float lockSnapBefore = ZeldaStyleCamera.FeelMaxLockSnapDeg;
        int lockSnapFramesBefore = feel != null ? feel.SnapFrames : 0;
        float lockEnd = Time.realtimeSinceStartup + 1.2f;
        while (Time.realtimeSinceStartup < lockEnd) {
            PlayInput.Hold(KeyCode.LeftShift, true);
            yield return null;
        }
        PlayInput.Hold(KeyCode.LeftShift, false);
        yield return Wait(0.6f);
        float lockSnap = ZeldaStyleCamera.FeelMaxLockSnapDeg - lockSnapBefore;
        int snapGain = feel != null ? feel.SnapFrames - lockSnapFramesBefore : 0;
        bool eased = snapGain == 0 && lockSnap < 30f;
        run.Event("camera-lock", map + ": maxFrameTurn " + lockSnap.ToString("0.0") + " deg, snapFrames+" + snapGain + ", eased=" + eased);
        run.Metric("feel.lockEase." + map, eased ? 1 : 0);
        run.Metric("feel.lockMaxFrameDeg." + map, Mathf.Round(lockSnap * 10f) / 10f);
        if (!eased) {
            run.Problem(PlaytestSeverity.Bug, "explore: lock-on/release snapped on " + map + " (frame turn " + lockSnap.ToString("0.0") + " deg, snap frames " + snapGain + ")");
        }
    }

    static Enemy NearestBot(Vector3 from, bool awakeOnly) {
        Enemy best = null;
        float bestSq = float.MaxValue;
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive) {
                continue;
            }
            if (awakeOnly && e.Dormant) {
                continue;
            }
            float sq = (e.transform.position - from).sqrMagnitude;
            if (sq < bestSq) {
                bestSq = sq;
                best = e;
            }
        }
        return best;
    }

    static IEnumerator LoadChapter(int index) {
        string n = (index + 1).ToString();
        CampaignSave.Slot slot = CampaignSave.Get(0);
        slot.used = true;
        slot.chapter = index;
        slot.reached = index;
        slot.cleared = index;
        RestStick();
        PlayInput.Release();
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            // Still on a play scene: go to the menu first.
            SceneManager.LoadScene(MetaProgress.MenuScene);
            yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "menu before chapter " + n);
            menu = Object.FindAnyObjectByType<MainMenu>();
        }
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "explore: no Campaign button for chapter " + n);
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return Wait(0.8f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.6f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        run.Event("tap", "Begin chapter " + n + " (explore)");
        float beginAt = Time.realtimeSinceStartup;
        if (index == 0) {
            // Level_1_Yard must load; if ChapterOneIsLevel was off, Begin starts the old chapter run and this waits forever.
            // Cap ~20 s and emit a stuck-cell report naming the active scene instead of a silent 60 s hang.
            const float ch1Budget = 20f;
            bool ready = false;
            while (!run.Done && Time.realtimeSinceStartup - beginAt < ch1Budget) {
                if (LevelScene.Instance != null && LevelScene.Instance.Yard != null
                    && SceneManager.GetActiveScene().name.StartsWith("Level_")) {
                    ready = true;
                    break;
                }
                if (Time.frameCount % 30 == 0) {
                    Publish(run);
                }
                yield return null;
            }
            if (!ready) {
                string scene = SceneManager.GetActiveScene().name;
                var cell = new Vector2Int(0, 0);
                run.Event("stuck-cell", "Level_1_Yard " + cell + " load-stall scene=" + scene
                    + " ChapterOneIsLevel=" + LevelScene.ChapterOneIsLevel
                    + " LevelScene=" + (LevelScene.Instance != null)
                    + " ChapterRun=" + (ChapterRun.Instance != null)
                    + " after " + (Time.realtimeSinceStartup - beginAt).ToString("0.0") + " s");
                run.Metric("explore.stuckCells.Level_1_Yard", 1);
                run.Problem(PlaytestSeverity.Bug, "explore: Chapter 1 did not load Level_1_Yard within "
                    + ch1Budget.ToString("0") + " s (active scene '" + scene + "', ChapterOneIsLevel="
                    + LevelScene.ChapterOneIsLevel + ")");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
            string map = SceneManager.GetActiveScene().name;
            float loadSec = Time.realtimeSinceStartup - beginAt;
            run.Scene(map);
            run.Event("chapter-map", "chapter 1 map " + map + " in " + loadSec.ToString("0.0") + " s");
            run.Event("map-change", "arcade -> " + map);
            run.Metric("explore.chapter1LoadSeconds", Mathf.Round(loadSec * 10f) / 10f);
            run.Progress(0.25f);
        } else {
            yield return run.Until(() => ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter's game scene");
            if (run.Done) {
                yield break;
            }
            yield return run.Until(() => ChapterMap.Current != null && ChapterMap.Current.Own != null, 30f, "the chapter's own map");
            if (run.Done) {
                yield break;
            }
            string mapName = ChapterMap.Current != null ? ChapterMap.Current.MapName : ("chapter" + n);
            run.Scene(mapName);
            run.Event("chapter-map", "chapter " + n + " map " + mapName);
            run.Event("map-change", "menu -> " + mapName);
            run.Progress(0.2f + 0.1f * index);
            yield return run.Until(() => Cutscene.IsOpen || ChapterRun.Instance.PlaySeconds > 3f, 12f, "opening");
            if (Cutscene.IsOpen) {
                yield return Wait(0.4f);
                PlayInput.Tap(KeyCode.Return);
                yield return run.Until(() => !Cutscene.IsOpen, 8f, "opening closed");
            }
        }
        yield return Wait(0.6f);
    }

    static IEnumerator LoadHub() {
        CampaignSave.Slot slot = CampaignSave.Get(0);
        slot.used = true;
        slot.chapter = 3;
        slot.reached = 3;
        slot.cleared = 3;
        PlayInput.Release();
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "menu before hub");
        if (run.Done) {
            yield break;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        yield return Wait(0.8f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.6f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.HubButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card with Hub");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1f);
        Tap(ChapterScene.Instance.HubButton.gameObject);
        run.Event("tap", "Hub (explore)");
        yield return run.Until(() => ChapterHub.Instance != null && ChapterHub.Instance.Shown, 45f, "the hub office");
        yield return Wait(1.5f);
    }

    static IEnumerator LeaveToMenu() {
        RestStick();
        PlayInput.Release();
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "menu after map");
        yield return Wait(0.6f);
    }
}
