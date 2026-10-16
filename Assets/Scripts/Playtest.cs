using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Hawkes playtest bot (contract v1, C:/Projects/hawkesstudio/docs/playtest.md). It plays through PlayInput,
// the same keys and mouse a player uses: Enter on the menu, Shift to lock on, the left button to shoot, 1-3 for
// cards, Enter to skip a break. Setup only: it plays in MetaProgress's sandbox with fresh ranks, so it never
// touches the player's save, and it loads the menu scene if the game isn't on it. Nothing runs until a scenario starts.
public static class Playtest {
    const string Product = "theplacedepartment";
    const float RunCap = 900f;

    static readonly PlaytestScenario[] Scenarios = {
        new PlaytestScenario("smoke", "Boot, menu, start a run, survive about 30 s", 45, "smoke"),
        new PlaytestScenario("run", "A full run until the musician falls, picking level-ups", 600, "full"),
        new PlaytestScenario("menus", "Shop menu, start a run, a level-up card, reroll and pick", 120, "menus"),
    };

    static PlaytestRun run;
    static int levelUps;
    static int lastWave;
    static float fpsSum;
    static int fpsFrames;
    static float worstFps = 999f;
    static float fpsNow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() {
        HawkesPlaytest.Register(Product, Scenarios, Bot, IsReady, Stop);
    }

    static bool IsReady() {
        string scene = SceneManager.GetActiveScene().name;
        return scene == MetaProgress.MenuScene ? Object.FindAnyObjectByType<MainMenu>() != null : WaveDirector.Instance != null;
    }

    static void Stop() {
        PlayInput.Release();
        if (Time.timeScale > 1f) {
            Time.timeScale = 1f;
        }
    }

    static IEnumerator Bot(string id, PlaytestOptions options, PlaytestRun current) {
        run = current;
        levelUps = 0;
        lastWave = 0;
        fpsSum = 0f;
        fpsFrames = 0;
        worstFps = 999f;
        PlayInput.Release();
        MetaProgress.EnterSandbox(true);
        Random.InitState(options.seed);
        Application.logMessageReceived += OnLog;
        try {
            yield return ToMenu();
            if (id == "menus") {
                yield return ShopMenu();
            }
            yield return StartRun();
            if (run.Done) {
                yield break;
            }
            if (id == "smoke") {
                yield return Play(options, 30f, false);
            } else if (id == "run") {
                yield return Play(options, RunCap, false);
            } else {
                yield return Play(options, 240f, true);
            }
            if (!run.Done) {
                run.Finish(PlaytestOutcome.Done);
            }
        } finally {
            Application.logMessageReceived -= OnLog;
            Stop();
        }
    }

    // Setup: the run always starts from the menu, as a player's does.
    static IEnumerator ToMenu() {
        run.Scene("Main Menu");
        if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
            run.Log("loading the menu scene (setup)");
            Time.timeScale = 1f;
            SceneManager.LoadScene(MetaProgress.MenuScene);
        }
        yield return run.Until(() => Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the main menu");
        if (Resources.Load<Texture2D>("UI/MenuArt") == null) {
            run.Problem(PlaytestSeverity.Polish, "menu key art Resources/UI/MenuArt is missing");
        }
        yield return new WaitForSecondsRealtime(1f);
    }

    static IEnumerator ShopMenu() {
        run.Note("checking the shop");
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.InstanceID);
        run.Metric("menuButtons", buttons.Length);
        if (buttons.Length == 0) {
            run.Problem(PlaytestSeverity.Bug, "the menu has no buttons");
            yield break;
        }
        // One click on each shop row, through the UI event system like a mouse click. The sandbox keeps it off the save.
        int coins = MetaProgress.Coins;
        int clicked = 0;
        foreach (Button button in buttons) {
            if (button == null || !button.isActiveAndEnabled || !button.interactable) {
                continue;
            }
            // Buy rows and "-10" only: Play is pressed with Enter below, and "+10" would arm a paid later start.
            Text label = button.GetComponentInChildren<Text>();
            string text = label != null ? label.text : "";
            if (text != "Buy" && text != "-10") {
                continue;
            }
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            clicked++;
            yield return new WaitForSecondsRealtime(0.25f);
            if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
                break;
            }
        }
        run.Metric("menuClicks", clicked);
        run.Metric("coinsSpent", coins - MetaProgress.Coins);
        run.Event("shop", clicked + " buttons clicked");
        if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
            yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene || WaveDirector.Instance != null, 30f, "the scene after a menu click");
            if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
                run.Log("a menu button started the run");
                yield break;
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
    }

    static IEnumerator StartRun() {
        if (WaveDirector.Instance == null) {
            run.Note("pressing Enter to play");
            PlayInput.Tap(KeyCode.Return);
            yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after pressing Enter");
            if (run.Done) {
                yield break;
            }
        }
        run.Scene("Wave 1");
        run.Event("run-start");
        Random.InitState(run.Options.seed);
        if (Musician.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "no musician in the game scene");
        }
        if (Object.FindAnyObjectByType<CharacterMovement>() == null) {
            run.Problem(PlaytestSeverity.Bug, "no player (CharacterMovement) in the game scene");
        }
        if (Object.FindAnyObjectByType<BowEquipment>() == null) {
            run.Problem(PlaytestSeverity.Warning, "no BowEquipment on the player");
        }
        yield return null;
    }

    static IEnumerator Play(PlaytestOptions options, float seconds, bool menusOnly) {
        float start = Time.realtimeSinceStartup;
        float nextMetrics = 0f;
        float nextSkip = 0f;
        float cardSeenAt = -1f;
        float lastKillAt = start;
        int lastKills = 0;
        float waveSince = start;
        bool rerolled = false;
        bool warnedKills = false;
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - start;
            SampleFps();

            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (elapsed >= seconds) {
                run.Event("time-up", seconds.ToString("0") + " s");
                yield break;
            }

            if (options.speed > 1f && Time.timeScale > 0.9f && Time.timeScale < options.speed) {
                Time.timeScale = options.speed;
            }

            if (LevelUpScreen.IsOpen) {
                PlayInput.Hold(KeyCode.Mouse0, false);
                PlayInput.Hold(KeyCode.LeftShift, false);
                PlayInput.Axis("Horizontal", 0f);
                PlayInput.Axis("Vertical", 0f);
                if (cardSeenAt < 0f) {
                    cardSeenAt = now;
                    run.Event("level-up", "level " + RunBoons.Level);
                }
                if (now - cardSeenAt > 0.6f) {
                    if (menusOnly && !rerolled && RunBoons.Rerolls > 0) {
                        rerolled = true;
                        PlayInput.Tap(KeyCode.R);
                        run.Event("reroll");
                        cardSeenAt = now;
                    } else {
                        int pick = Random.Range(0, 3);
                        PlayInput.Tap(KeyCode.Alpha1 + pick);
                        int picksBefore = RunBoons.Picks;
                        yield return null;
                        yield return null;
                        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore && pick > 0) {
                            PlayInput.Tap(KeyCode.Alpha1);
                            yield return null;
                            yield return null;
                        }
                        if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                            yield return new WaitForSecondsRealtime(1f);
                            if (LevelUpScreen.IsOpen && RunBoons.Picks == picksBefore) {
                                run.Problem(PlaytestSeverity.Bug, "the level-up card did not close after pressing a number key");
                                run.Finish(PlaytestOutcome.Stuck);
                                yield break;
                            }
                        }
                        levelUps++;
                        run.Add("levelUps");
                        run.Log("picked card " + (pick + 1) + " (picks " + picksBefore + " -> " + RunBoons.Picks + ")");
                        cardSeenAt = -1f;
                        if (menusOnly) {
                            yield return new WaitForSecondsRealtime(1.5f);
                            if (Time.timeScale <= 0.01f) {
                                run.Problem(PlaytestSeverity.Bug, "the game stayed paused after a level-up pick");
                                run.Finish(PlaytestOutcome.Stuck);
                            }
                            yield break;
                        }
                    }
                }
                yield return null;
                continue;
            }
            cardSeenAt = -1f;

            // Shoot: Shift locks on to the biggest bird in reach, the left button draws and looses.
            bool building = WaveDirector.IsBuilding;
            PlayInput.Hold(KeyCode.LeftShift, !building && Enemy.Roster.Count > 0);
            PlayInput.Hold(KeyCode.Mouse0, !building && !LevelUpScreen.HoldFire && Enemy.Roster.Count > 0);

            // Stay by the piano: walk back with WASD (camera-relative) when drifted away.
            Steer(player);

            // Breaks and build phases: skip them with Enter, as a player in a hurry does.
            if ((building || Enemy.Roster.Count == 0) && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }

            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                if (lastWave != 0) {
                    run.Event("wave", wave.ToString());
                }
                lastWave = wave;
                waveSince = now;
                warnedKills = false;
                run.Scene("Wave " + wave);
            }
            if (RunLog.RunKills != lastKills) {
                lastKills = RunLog.RunKills;
                lastKillAt = now;
            }
            if (!warnedKills && Enemy.Roster.Count > 0 && now - lastKillAt > 45f) {
                warnedKills = true;
                run.Problem(PlaytestSeverity.Warning, "no kills for 45 s at wave " + wave + " with " + Enemy.Roster.Count + " birds alive");
            }
            if (now - waveSince > 240f) {
                run.Problem(PlaytestSeverity.Bug, "wave " + wave + " did not end in 240 s (" + Enemy.Roster.Count + " birds alive)");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }

            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(elapsed);
                run.Progress(seconds > 0f ? elapsed / seconds : 0f);
                run.Note(building ? "build phase" : Enemy.Roster.Count + " birds, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
            }
            yield return null;
        }
    }

    static void Steer(CharacterMovement player) {
        float h = 0f;
        float v = 0f;
        Camera cam = Camera.main;
        if (player != null && Musician.Instance != null && cam != null) {
            Vector3 to = Musician.Instance.transform.position - player.transform.position;
            to.y = 0f;
            if (to.magnitude > 9f) {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                Vector3 right = cam.transform.right;
                right.y = 0f;
                to.Normalize();
                v = Vector3.Dot(fwd.normalized, to);
                h = Vector3.Dot(right.normalized, to);
            }
        }
        PlayInput.Axis("Horizontal", h);
        PlayInput.Axis("Vertical", v);
    }

    static IEnumerator GameOver() {
        PlayInput.Release();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        run.Event("lost", "wave " + WaveDirector.Wave);
        run.Scene("Game Over");
        yield return new WaitForSecondsRealtime(2.5f);
        PlayInput.Tap(KeyCode.Space);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene, 20f, "the menu after the game-over screen");
        if (!run.Done) {
            run.Scene("Main Menu");
            run.Finish(PlaytestOutcome.Lost);
        }
    }

    static void SampleFps() {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) {
            return;
        }
        fpsNow = Mathf.Lerp(fpsNow <= 0f ? 1f / dt : fpsNow, 1f / dt, 0.1f);
        fpsSum += 1f / dt;
        fpsFrames++;
        if (fpsFrames > 30 && fpsNow < worstFps) {
            worstFps = fpsNow;
        }
    }

    static void Metrics(float elapsed) {
        run.Metric("wave", WaveDirector.Wave);
        run.Metric("kills", RunLog.RunKills);
        run.Metric("damage", Mathf.Round(RunLog.RunDamage));
        run.Metric("shots", RunLog.RunShots);
        run.Metric("hits", RunLog.RunHits);
        run.Metric("level", RunBoons.Level);
        run.Metric("levelUps", levelUps);
        run.Metric("birdsAlive", Enemy.Roster.Count);
        run.Metric("bestStreak", Combo.Best);
        run.Metric("coinsRun", MetaProgress.CoinsThisRun);
        run.Metric("runSeconds", Mathf.Round(elapsed));
        if (Musician.Instance != null) {
            run.Metric("musicianHp", Musician.Instance.Health);
            run.Metric("musicianDamageTaken", Musician.Instance.DamageTaken);
        }
        run.Metric("fps", Mathf.Round(fpsNow));
        if (fpsFrames > 0) {
            run.Metric("fpsAverageGame", Mathf.Round(fpsSum / fpsFrames));
            run.Metric("fpsWorstGame", Mathf.Round(Mathf.Min(worstFps, 999f)));
        }
    }

    // Errors are caught by the package; warnings about missing pieces are worth a line too.
    static void OnLog(string message, string stack, LogType type) {
        if (run == null || run.Done || type != LogType.Warning) {
            return;
        }
        string first = message.Split('\n')[0];
        string lower = first.ToLowerInvariant();
        if (lower.Contains("not assigned") || lower.Contains("not found") || lower.Contains("missing")) {
            run.Problem(PlaytestSeverity.Warning, first);
        }
    }
}
