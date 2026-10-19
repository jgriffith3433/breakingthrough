using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Hawkes playtest bot (contract v1, C:/Projects/hawkesstudio/docs/playtest.md). It plays through PlayInput,
// the same keys and mouse a player uses: Enter on the menu, WASD to walk to the nearest bird near the musician,
// Shift to lock on, left clicks to swing the bat (held to wind up a home run), back to the piano when the birds
// are gone, 1-3 for cards, Enter to skip a break. Every scenario first presses the web page's "Tap to play" prompt. Setup only: it plays in MetaProgress's sandbox with fresh ranks, so it never
// touches the player's save, and it loads the menu scene if the game isn't on it. Nothing runs until a scenario starts.
public static class Playtest {
    const string Product = "theplacedepartment";
    const float RunCap = 900f;

    static readonly PlaytestScenario[] Scenarios = {
        new PlaytestScenario("smoke", "Boot, menu, start a run, about 30 s of play: the bot walks to birds near the musician and swings the bat (lock-on nearest, combo swings)", 45, "smoke"),
        new PlaytestScenario("run", "A full run until the musician falls, picking level-ups: the bot guards the musician with the bat (walks to the nearest bird near the piano, locks on, swings, walks back). Should pass wave 5 without 'no kills for 45 s'", 600, "full"),
        new PlaytestScenario("bat", "The bat: a 3-hit combo on wave 1's turkeys (hits and knockback, the third further), a ground smash on small hatchlings dropped in front of the player (setup), then a charged home run into a line of turkeys (setup) that bowls them over; lock-on picks the nearest bird throughout. Then a minute of play", 200, "full"),
        new PlaytestScenario("menus", "Shop menu, start a run, a level-up card, reroll and pick", 120, "menus"),
        new PlaytestScenario("touch", "Touch only, for a phone-sized screen (mobile, or 844x390): the menu fits, tap Play, walk to the birds with the on-screen stick and tap Swing, tap a level-up card, play to wave 2", 330, "menus"),
        new PlaytestScenario("settings", "Press Tap to play, open Settings from the menu: sound off and on, music and effects sliders, Done; start a run, pause (button, then P), Settings from the pause menu, resume, play a little. Any screen size", 100, "menus"),
        new PlaytestScenario("analytics", "Hawkes Analytics: start a run, clear wave 1, then hold fire on wave 2 so the birds take the musician. Every analytics event is logged as it goes out (run_start, level_start and level_end per wave, card picks, the death and its bird, run_end) with the build version and env; on the game-over screen the \"How was that run?\" card must show, and the bot rates it 4 stars", 240, "full"),
        new PlaytestScenario("site", "The website around the game: reads the landing page, every page the deploy listed in site/pages.json and every page linked from them (outside Game/), looks for a blog, logs which pages exist, checks each footer's \"Powered by HAWKES GEN\" link to hawkesgen.com and the landing page's link to hawkesbiomedical.com, shows each page's footer over the game for a few seconds, then plays 20 s to show the game screen has no footer", 120, "menus"),
    };

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern int HawkesSoundPressPrompt();

    [DllImport("__Internal")]
    static extern string HawkesSoundState();

    [DllImport("__Internal")]
    static extern string HawkesAnalyticsPageLog(int afterSeq);

    [DllImport("__Internal")]
    static extern int HawkesAnalyticsPageAskShown(string id);

    [DllImport("__Internal")]
    static extern int HawkesAnalyticsPageRate(string id, int stars);
#else
    static int HawkesSoundPressPrompt() {
        return 0;
    }

    static string HawkesSoundState() {
        return "";
    }

    static string HawkesAnalyticsPageLog(int afterSeq) {
        return "";
    }

    static int HawkesAnalyticsPageAskShown(string id) {
        return 0;
    }

    static int HawkesAnalyticsPageRate(string id, int stars) {
        return 0;
    }
#endif

    static PlaytestRun run;
    static int levelUps;
    static int lastWave;
    static float fpsSum;
    static int fpsFrames;
    static float worstFps = 999f;
    static float fpsNow;
    // The analytics scenario: from this wave on the bot holds fire, so the birds end the run.
    static int holdFireFrom;
    static bool analyticsRun;
    static int analyticsSeq;
    static int analyticsEvents;
    static int analyticsSent;
    static bool analyticsConfig;
    // The batter: one-frame clicks, the lock it last checked, bat lines told so far.
    static bool tapHeld;
    static float nextTapAt;
    static float nextShakeAt;
    static GameObject lastLock;
    static int lockChecks;
    static int lockNearest;
    static int lockWrong;
    static int batTold;
    static bool batRun;
    static int lockSwitchesSeen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() {
        HawkesPlaytest.Register(Product, Scenarios, Bot, IsReady, Stop);
    }

    static bool IsReady() {
        string scene = SceneManager.GetActiveScene().name;
        return scene == MetaProgress.MenuScene ? Object.FindAnyObjectByType<MainMenu>() != null : WaveDirector.Instance != null;
    }

    static void Stop() {
        SettingsPanel.Close();
        PauseMenu.Resume();
        PlayInput.Release();
        TouchControls.LiftBotFingers();
        UiFit.ForceTouch(false);
        HawkesSiteHide();
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
        holdFireFrom = 0;
        analyticsRun = id == "analytics";
        analyticsSeq = -1;
        analyticsEvents = 0;
        analyticsSent = 0;
        analyticsConfig = false;
        tapHeld = false;
        nextTapAt = 0f;
        nextShakeAt = 0f;
        lastLock = null;
        lockChecks = 0;
        lockNearest = 0;
        lockWrong = 0;
        batTold = 0;
        batRun = id == "bat";
        lockSwitchesSeen = ZeldaStyleCamera.LockSwitches;
        RunAnalytics.EnableDebug();
        PlayInput.Release();
        MetaProgress.EnterSandbox(true);
        Random.InitState(options.seed);
        Application.logMessageReceived += OnLog;
        try {
            yield return TapToPlay();
            if (id == "settings") {
                yield return SettingsRun(options);
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "touch") {
                yield return TouchRun();
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            if (id == "site") {
                yield return SiteRun(options);
                if (!run.Done) {
                    run.Finish(PlaytestOutcome.Done);
                }
                yield break;
            }
            yield return ToMenu();
            if (id == "menus") {
                yield return ShopMenu();
            }
            yield return StartRun();
            if (run.Done) {
                yield break;
            }
            if (id == "analytics") {
                holdFireFrom = 2;
                yield return Play(options, 220f, false);
                if (!run.Done) {
                    AnalyticsReport();
                }
            } else if (id == "smoke") {
                yield return Play(options, 30f, false);
            } else if (id == "bat") {
                yield return BatRun(options);
                if (!run.Done) {
                    yield return Play(options, 60f, false);
                }
                if (!run.Done) {
                    BatReport();
                }
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

    // The page's "Tap to play" prompt covers the game until it is pressed, as a player would: a pointer press on the
    // prompt, through its own handler (HawkesSound.jslib). It reports whether the browser's audio is running after it.
    // In the editor, or on a page without the prompt, there is nothing to press.
    static IEnumerator TapToPlay() {
        string before = HawkesSoundState();
        if (before != "prompt") {
            run.Log(string.IsNullOrEmpty(before) ? "no Tap to play prompt on this page" : "Tap to play was already pressed (audio " + before + ")");
            yield break;
        }
        run.Scene("Tap to play");
        yield return new WaitForSecondsRealtime(1.5f);
        HawkesSoundPressPrompt();
        yield return new WaitForSecondsRealtime(0.6f);
        string after = HawkesSoundState();
        run.Event("tap-prompt", "audio " + after);
        run.Metric("audioRunning", after == "running" ? 1 : 0);
        if (after == "prompt") {
            run.Problem(PlaytestSeverity.Bug, "the Tap to play prompt did not close when pressed");
        }
    }

    // Settings from the main menu and from the pause menu, through the same taps and drags a player makes.
    static IEnumerator SettingsRun(PlaytestOptions options) {
        yield return ToMenu();
        if (run.Done) {
            yield break;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        Button open = menu != null ? menu.SettingsButton : null;
        if (open == null || !open.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "no Settings button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        if (!Inside(ScreenRect((RectTransform)open.transform))) {
            run.Problem(PlaytestSeverity.Bug, "the menu's Settings button is not fully on screen");
        }
        Tap(open.gameObject);
        run.Event("tap", "Settings (menu)");
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings to open from the menu");
        if (run.Done) {
            yield break;
        }
        run.Scene("Settings");
        run.Event("settings-opened", "menu");
        run.Progress(0.15f);
        yield return new WaitForSecondsRealtime(1f);
        CheckScreen("settings", 600);
        yield return PlayWithSettings("menu");
        if (run.Done) {
            yield break;
        }

        menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "no Play button on the menu after Settings closed");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.PlayButton.gameObject);
        run.Event("tap", "Play");
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after tapping Play");
        if (run.Done) {
            yield break;
        }
        run.Scene("Wave 1");
        run.Event("run-start");
        run.Progress(0.45f);
        yield return new WaitForSecondsRealtime(4f);
        yield return run.Until(() => PauseMenu.Instance != null && PauseMenu.Instance.PauseButton != null && PauseMenu.Instance.PauseButton.isActiveAndEnabled, 20f, "the pause button");
        if (run.Done) {
            yield break;
        }
        if (!Inside(ScreenRect((RectTransform)PauseMenu.Instance.PauseButton.transform))) {
            run.Problem(PlaytestSeverity.Bug, "the pause button is not fully on screen");
        }
        if (UiFit.Touch) {
            CheckScreen("hud");
        }

        Tap(PauseMenu.Instance.PauseButton.gameObject);
        run.Event("tap", "Pause");
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "the pause menu after tapping the pause button");
        if (run.Done) {
            yield break;
        }
        run.Scene("Paused");
        run.Event("paused", "button");
        yield return new WaitForSecondsRealtime(1f);
        if (Time.timeScale > 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "the game kept running behind the pause menu (time scale " + Time.timeScale.ToString("0.00") + ")");
        }
        CheckScreen("pause", 450);
        Tap(PauseMenu.Instance.SettingsButton.gameObject);
        run.Event("tap", "Settings (pause)");
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings to open from the pause menu");
        if (run.Done) {
            yield break;
        }
        run.Scene("Settings (paused)");
        run.Event("settings-opened", "pause");
        run.Progress(0.65f);
        yield return new WaitForSecondsRealtime(1f);
        yield return PlayWithSettings("pause");
        if (run.Done) {
            yield break;
        }
        if (!PauseMenu.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "closing Settings also closed the pause menu");
        } else {
            Tap(PauseMenu.Instance.ResumeButton.gameObject);
            run.Event("tap", "Resume");
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (PauseMenu.IsOpen || Time.timeScale <= 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "the game stayed paused after Resume");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Scene("Wave " + WaveDirector.Wave);
        run.Event("resumed", "button");

        // The keyboard way: P pauses and P resumes.
        if (!UiFit.Touch) {
            yield return new WaitForSecondsRealtime(2f);
            PlayInput.Tap(KeyCode.P);
            yield return run.Until(() => PauseMenu.IsOpen, 3f, "the pause menu after pressing P");
            if (run.Done) {
                yield break;
            }
            run.Event("paused", "P");
            yield return new WaitForSecondsRealtime(1f);
            PlayInput.Tap(KeyCode.P);
            yield return run.Until(() => !PauseMenu.IsOpen, 3f, "the pause menu to close after pressing P again");
            if (run.Done) {
                yield break;
            }
            run.Event("resumed", "P");
        }
        run.Progress(0.85f);
        yield return Play(options, 15f, false);
    }

    // On the open Settings: sound off and back on, music to 30%, effects to 70%, each by a tap or a press on the
    // slider where a finger would land, then Done.
    static IEnumerator PlayWithSettings(string from) {
        SettingsPanel panel = SettingsPanel.Instance;
        if (panel == null) {
            run.Problem(PlaytestSeverity.Bug, "Settings has no panel");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        bool was = SoundSettings.On;
        Tap(panel.SoundButton.gameObject);
        yield return new WaitForSecondsRealtime(0.8f);
        if (SoundSettings.On == was) {
            run.Problem(PlaytestSeverity.Bug, "the Sound button did not switch sound (" + from + ")");
        }
        run.Event(SoundSettings.On ? "sound-on" : "sound-off", from);
        Tap(panel.SoundButton.gameObject);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event(SoundSettings.On ? "sound-on" : "sound-off", from);

        Slide(panel.MusicSlider, 0.3f);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event("music-volume", Mathf.RoundToInt(SoundSettings.Music * 100f) + "% (" + from + ")");
        if (Mathf.Abs(SoundSettings.Music - 0.3f) > 0.08f) {
            run.Problem(PlaytestSeverity.Bug, "the music slider set " + SoundSettings.Music.ToString("0.00") + " for a press at 30% (" + from + ")");
        }
        Slide(panel.EffectsSlider, 0.7f);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event("effects-volume", Mathf.RoundToInt(SoundSettings.Sfx * 100f) + "% (" + from + ")");
        if (Mathf.Abs(SoundSettings.Sfx - 0.7f) > 0.08f) {
            run.Problem(PlaytestSeverity.Bug, "the effects slider set " + SoundSettings.Sfx.ToString("0.00") + " for a press at 70% (" + from + ")");
        }
        run.Metric("soundOn", SoundSettings.On ? 1 : 0);
        run.Metric("musicVolume", Mathf.Round(SoundSettings.Music * 100f));
        run.Metric("effectsVolume", Mathf.Round(SoundSettings.Sfx * 100f));

        Tap(panel.DoneButton.gameObject);
        run.Event("tap", "Done");
        yield return run.Until(() => !SettingsPanel.IsOpen, 5f, "Settings to close after Done");
        if (!run.Done) {
            run.Event("settings-closed", from);
        }
    }

    // A press and release on a slider at a fraction of its track, like a finger landing there.
    static void Slide(Slider slider, float at) {
        Rect track = ScreenRect(slider.handleRect != null ? (RectTransform)slider.handleRect.parent : (RectTransform)slider.transform);
        var data = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(Mathf.Lerp(track.xMin, track.xMax, at), track.center.y),
        };
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerUpHandler);
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
        yield return null;
        if (Object.FindAnyObjectByType<BatEquipment>() == null) {
            run.Problem(PlaytestSeverity.Bug, "no bat (BatEquipment) on the player");
        }
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
                        if (LevelUpScreen.IsOpen && pick > 0) {
                            PlayInput.Tap(KeyCode.Alpha1);
                            yield return null;
                            yield return null;
                        }
                        if (LevelUpScreen.IsOpen) {
                            yield return new WaitForSecondsRealtime(1f);
                            if (LevelUpScreen.IsOpen) {
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

            // The bat: walk to the nearest bird near the musician, lock on, swing; back to the piano when clear.
            bool building = WaveDirector.IsBuilding;
            bool holding = holdFireFrom > 0 && WaveDirector.Wave >= holdFireFrom;
            Batter(player, building || holding, null, true);
            CheckLock(player);
            TellBat();

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
                if (holdFireFrom > 0 && wave >= holdFireFrom) {
                    run.Event("hold-fire", "wave " + wave + ": the bot stops swinging so the birds reach the musician");
                }
                warnedKills = false;
                run.Scene("Wave " + wave);
            }
            if (RunLog.RunKills != lastKills) {
                lastKills = RunLog.RunKills;
                lastKillAt = now;
            }
            if (!warnedKills && holdFireFrom == 0 && Enemy.Roster.Count > 0 && now - lastKillAt > 45f) {
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
                AnalyticsTimeline();
                run.Progress(seconds > 0f ? elapsed / seconds : 0f);
                run.Note(building ? "build phase" : Enemy.Roster.Count + " birds, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
            }
            yield return null;
        }
    }

    // ---------------------------------------------------------------- bat

    // The batter, every frame of play. The target is the nearest bird within guard range of the musician (or of
    // only, when given). Walk at it with WASD (camera-relative) until it is in the bat's reach, hold Shift so the
    // lock is on, and click a swing every 0.12 s while it is in reach. With nothing near the musician, walk back to
    // the piano. idle: no target and no swings (the build phase, or the analytics scenario holding fire).
    static Enemy Batter(CharacterMovement player, bool idle, List<Enemy> only, bool lockOn) {
        Enemy target = idle || player == null ? null : GuardTarget(player, only);
        bool swing = false;
        if (target != null) {
            Vector3 to = target.transform.position - player.transform.position;
            to.y = 0f;
            float gap = to.magnitude - target.BodyScale * 0.3f;
            float reach = BatEquipment.Reach;
            Walk(player, target.transform.position, reach * 0.6f);
            swing = gap <= reach * 0.95f && !LevelUpScreen.HoldFire;
        } else if (player != null && Musician.Instance != null) {
            Walk(player, Musician.Instance.transform.position, 9f);
        } else {
            Stand();
        }
        PlayInput.Hold(KeyCode.LeftShift, lockOn && target != null);
        Click(swing);
        float now = Time.realtimeSinceStartup;
        if (!idle && Enemy.Climbers > 0 && now >= nextShakeAt) {
            nextShakeAt = now + 1.5f;
            PlayInput.Tap(KeyCode.F);
        }
        return target;
    }

    // One-frame clicks of the left button, so each is a swing and never a wind-up.
    static void Click(bool swing) {
        if (tapHeld) {
            PlayInput.Hold(KeyCode.Mouse0, false);
            tapHeld = false;
            return;
        }
        float now = Time.realtimeSinceStartup;
        if (swing && now >= nextTapAt) {
            PlayInput.Hold(KeyCode.Mouse0, true);
            tapHeld = true;
            nextTapAt = now + 0.12f;
        }
    }

    static Enemy GuardTarget(CharacterMovement player, List<Enemy> only) {
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : player.transform.position;
        float guard = 32f + BatEquipment.Reach * 2f;
        List<Enemy> pool = only != null ? only : Enemy.Roster;
        Enemy best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < pool.Count; i++) {
            Enemy enemy = pool[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            Vector3 fromPiano = enemy.transform.position - piano;
            fromPiano.y = 0f;
            if (only == null && fromPiano.magnitude > guard) {
                continue;
            }
            float dist = (enemy.transform.position - player.transform.position).sqrMagnitude;
            if (dist < bestDist) {
                bestDist = dist;
                best = enemy;
            }
        }
        return best;
    }

    static void Stand() {
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
    }

    // WASD toward a point, camera-relative, until within stop meters.
    static void Walk(CharacterMovement player, Vector3 point, float stop) {
        float h = 0f;
        float v = 0f;
        Camera cam = Camera.main;
        if (player != null && cam != null) {
            Vector3 to = point - player.transform.position;
            to.y = 0f;
            if (to.magnitude > stop) {
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

    // Each time the lock lands on a bird, it must be the nearest one in reach (unless right click switched it on
    // to the next nearest).
    static void CheckLock(CharacterMovement player) {
        ZeldaStyleCamera cam = Juice.Cam;
        if (cam == null || player == null) {
            return;
        }
        GameObject locked = cam.LockedOnGameObject;
        if (locked == lastLock) {
            return;
        }
        bool hadOne = lastLock != null;
        lastLock = locked;
        if (locked == null) {
            return;
        }
        Vector3 origin = player.transform.position;
        float reach = cam.LockReach();
        Enemy nearest = null;
        float best = float.MaxValue;
        int inReach = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            float d = Vector3.Distance(origin, enemy.transform.position);
            if (d > reach) {
                continue;
            }
            inReach++;
            if (d < best) {
                best = d;
                nearest = enemy;
            }
        }
        float dist = Vector3.Distance(origin, locked.transform.position);
        lockChecks++;
        if (ZeldaStyleCamera.LockSwitches > lockSwitchesSeen) {
            lockSwitchesSeen = ZeldaStyleCamera.LockSwitches;
            run.Event("lock-switch", "right click: the lock moved to the next nearest bird, " + locked.name + " at " + dist.ToString("0.0") + " m (" + inReach + " in reach)");
            return;
        }
        if (nearest == null || nearest.gameObject == locked || dist <= best + 0.75f) {
            lockNearest++;
            if (batRun || lockNearest <= 6) {
                run.Event("lock-on", "the nearest bird: " + locked.name + " at " + dist.ToString("0.0") + " m of " + inReach + " in reach" + (hadOne ? " (the last one fell)" : ""));
            }
        } else {
            lockWrong++;
            if (lockWrong <= 3) {
                run.Problem(PlaytestSeverity.Warning, "lock-on took " + locked.name + " at " + dist.ToString("0.0") + " m while " + nearest.name + " was " + best.ToString("0.0") + " m away");
            }
        }
    }

    // What the bat did, from BatEquipment's lines: all of them in the bat scenario, the first few elsewhere.
    static void TellBat() {
        for (int i = 0; i < BatEquipment.Events.Count; i++) {
            string line = BatEquipment.Events[i];
            if (batRun || batTold < 15) {
                run.Event("bat", line);
            } else {
                run.Log("bat: " + line);
            }
            batTold++;
        }
        BatEquipment.Events.Clear();
    }

    // The bat scenario: the combo, a ground smash and a home run, each until it is seen, through the same clicks,
    // Shift and WASD a player uses. Setup only: the birds for the smash and the home run are dropped in front of
    // the player (named in the timeline).
    static IEnumerator BatRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        BatEquipment.Events.Clear();
        lockSwitchesSeen = ZeldaStyleCamera.LockSwitches;

        // 1. The combo on wave 1's turkeys, and one lock switch along the way.
        run.Scene("Bat: the combo");
        run.Note("swinging the 3-hit combo at wave 1's turkeys");
        bool switched = false;
        yield return Fight(player, () => {
            if (!switched && BatEquipment.Finishers >= 1 && Juice.Cam != null && Juice.Cam.LockedOnGameObject != null && BirdsInLockReach(player) >= 2) {
                switched = true;
                PlayInput.Tap(KeyCode.Mouse1);
            }
            return BatEquipment.Finishers >= 2 && BatEquipment.KnockedBack >= 3;
        }, 120f, "a 3-hit combo landing", null, true);
        if (run.Done) {
            yield break;
        }
        run.Event("combo", BatEquipment.Swings + " swings, " + BatEquipment.Finishers + " finishers (swing 3), " + BatEquipment.KnockedBack + " birds knocked back");
        run.Progress(0.3f);

        // 2. A ground smash: small birds get the bat swung down at the ground.
        run.Scene("Bat: ground smash");
        for (int attempt = 0; attempt < 3 && BatEquipment.SmashHits == 0 && !run.Done; attempt++) {
            yield return Fight(player, () => !WaveDirector.IsBuilding && !LevelUpScreen.IsOpen, 40f, "a wave to fight in", null, true);
            if (run.Done) {
                yield break;
            }
            var chicks = new List<Enemy>();
            Vector3 front = Forward(player);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            for (int i = 0; i < 3; i++) {
                chicks.Add(SpawnBird(player.transform.position + front * (5.5f + i * 0.6f) + side * (i - 1) * 1.1f, true));
            }
            run.Event("setup", "three hatchlings (size 0.58, under " + Tuning.SmashBelow.ToString("0.00") + " of the player's " + (PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f).ToString("0.00") + ") dropped 6 m in front of the player");
            yield return Fight(player, () => BatEquipment.SmashHits > 0, 20f, "a ground smash on a hatchling", chicks, false);
        }
        if (run.Done) {
            yield break;
        }
        run.Progress(0.55f);

        // 3. A home run into a line of turkeys.
        run.Scene("Bat: home run");
        for (int attempt = 0; attempt < 3 && BatEquipment.HomeRunKnocks == 0 && !run.Done; attempt++) {
            yield return HomeRunTry(player);
        }
        if (run.Done) {
            yield break;
        }
        run.Progress(0.8f);
        run.Scene("Wave " + WaveDirector.Wave);
    }

    // Walk out from the piano so the turkeys line up between the player and the dark, wind up, let go as the
    // front one comes into reach.
    static IEnumerator HomeRunTry(CharacterMovement player) {
        yield return Fight(player, () => !WaveDirector.IsBuilding && !LevelUpScreen.IsOpen, 40f, "a wave to fight in", null, true);
        if (run.Done || player == null) {
            yield break;
        }
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        Vector3 outward = player.transform.position - piano;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 1f ? outward.normalized : Vector3.forward;
        PlayInput.Hold(KeyCode.LeftShift, false);
        Click(false);
        PlayInput.Hold(KeyCode.Mouse0, false);
        foreach (float distance in new[] { 3f, 8f }) {
            float until = Time.realtimeSinceStartup + 12f;
            while (!run.Done && Time.realtimeSinceStartup < until) {
                if (WaveDirector.IsLost) {
                    yield return GameOver();
                    yield break;
                }
                if (LevelUpScreen.IsOpen) {
                    yield return PickCard();
                    continue;
                }
                Vector3 spot = piano + outward * distance;
                Vector3 to = spot - player.transform.position;
                to.y = 0f;
                if (to.magnitude < 1f) {
                    break;
                }
                Walk(player, spot, 0.5f);
                yield return null;
            }
        }
        Stand();
        yield return new WaitForSecondsRealtime(0.3f);

        Vector3 front = Forward(player);
        var line = new List<Enemy>();
        float[] at = { 6.5f, 9f, 10.5f, 12f };
        for (int i = 0; i < at.Length; i++) {
            line.Add(SpawnBird(player.transform.position + front * at[i] + Vector3.Cross(Vector3.up, front) * Random.Range(-0.3f, 0.3f), false));
        }
        run.Event("setup", "a line of 4 turkeys dropped 6.5 to 12 m in front of the player, walking in toward the piano");

        // Hold to wind up, step toward the front turkey, let go once it is in reach and the charge is full.
        int knocksBefore = BatEquipment.HomeRunKnocks;
        int homeRunsBefore = BatEquipment.HomeRuns;
        PlayInput.Hold(KeyCode.Mouse0, true);
        run.Event("wind-up", "holding the swing button");
        float start = Time.realtimeSinceStartup;
        while (!run.Done && Time.realtimeSinceStartup - start < 6f) {
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                PlayInput.Hold(KeyCode.Mouse0, true);
                continue;
            }
            Enemy first = line.Find(e => e != null && e.Alive);
            if (first == null) {
                break;
            }
            Vector3 to = first.transform.position - player.transform.position;
            to.y = 0f;
            float gap = to.magnitude - first.BodyScale * 0.3f;
            Walk(player, first.transform.position, BatEquipment.Reach * 0.6f);
            if (BatEquipment.Charge >= 1f && gap <= BatEquipment.Reach * 0.9f) {
                break;
            }
            yield return null;
        }
        run.Event("release", "let go at " + Mathf.RoundToInt(BatEquipment.Charge * 100f) + "% charge");
        PlayInput.Hold(KeyCode.Mouse0, false);
        Stand();
        float watch = Time.realtimeSinceStartup + 3f;
        while (!run.Done && Time.realtimeSinceStartup < watch) {
            TellBat();
            yield return null;
        }
        TellBat();
        if (BatEquipment.HomeRuns == homeRunsBefore) {
            run.Log("the home run swing did not go off; trying again");
        } else if (BatEquipment.HomeRunKnocks == knocksBefore) {
            run.Log("the home run flew without hitting another bird; trying again");
        }
    }

    // A level-up card during the bat steps: the first card, as in Play.
    static IEnumerator PickCard() {
        PlayInput.Hold(KeyCode.Mouse0, false);
        tapHeld = false;
        PlayInput.Hold(KeyCode.LeftShift, false);
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
        run.Event("level-up", "level " + RunBoons.Level);
        yield return new WaitForSecondsRealtime(0.7f);
        PlayInput.Tap(KeyCode.Alpha1);
        yield return null;
        yield return null;
        if (LevelUpScreen.IsOpen) {
            yield return new WaitForSecondsRealtime(1f);
            if (LevelUpScreen.IsOpen) {
                run.Problem(PlaytestSeverity.Bug, "the level-up card did not close after pressing a number key");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
        }
        levelUps++;
        run.Add("levelUps");
    }

    // Plays (the batter, cards, skipping breaks) until goal is true. Not seeing it in time is a bug.
    static IEnumerator Fight(CharacterMovement player, System.Func<bool> goal, float seconds, string what, List<Enemy> only, bool lockOn) {
        float start = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        float nextMetrics = 0f;
        while (!run.Done && !goal()) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            if (WaveDirector.IsLost) {
                yield return GameOver();
                yield break;
            }
            if (now - start > seconds) {
                run.Problem(PlaytestSeverity.Bug, "the bat scenario did not see " + what + " in " + seconds.ToString("0") + " s");
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            if (only != null) {
                only.RemoveAll(e => e == null || !e.Alive);
            }
            bool building = WaveDirector.IsBuilding;
            Batter(player, building, only != null && only.Count > 0 ? only : null, lockOn);
            CheckLock(player);
            TellBat();
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
            }
            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(now - run.StartTime);
                AnalyticsTimeline();
            }
            yield return null;
        }
        TellBat();
    }

    static void BatReport() {
        TellBat();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        if (BatEquipment.Finishers == 0) {
            run.Problem(PlaytestSeverity.Bug, "no 3-hit combo landed (no swing 3 hit a bird)");
        }
        if (BatEquipment.KnockedBack == 0) {
            run.Problem(PlaytestSeverity.Bug, "the bat hit no bird hard enough to knock it back");
        }
        if (BatEquipment.SmashHits == 0) {
            run.Problem(PlaytestSeverity.Bug, "no ground smash hit a small bird");
        }
        if (BatEquipment.HomeRuns == 0) {
            run.Problem(PlaytestSeverity.Bug, "no home run swing went off after holding the button");
        } else if (BatEquipment.HomeRunKnocks == 0) {
            run.Problem(PlaytestSeverity.Bug, "the home run launched a bird that knocked into no other bird");
        }
        run.Event("bat-report", BatEquipment.Swings + " swings, " + BatEquipment.Finishers + " finishers, " + BatEquipment.SmashHits + " smash hits, "
            + BatEquipment.HomeRuns + " home runs knocking into " + BatEquipment.HomeRunKnocks + " birds, lock nearest " + lockNearest + " of " + lockChecks);
    }

    static int BirdsInLockReach(CharacterMovement player) {
        ZeldaStyleCamera cam = Juice.Cam;
        if (cam == null || player == null) {
            return 0;
        }
        float reach = cam.LockReach();
        int count = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy != null && enemy.Alive && !enemy.Climbing && Vector3.Distance(player.transform.position, enemy.transform.position) <= reach) {
                count++;
            }
        }
        return count;
    }

    static Vector3 Forward(CharacterMovement player) {
        Vector3 front = player.transform.forward;
        front.y = 0f;
        return front.sqrMagnitude > 0.01f ? front.normalized : Vector3.forward;
    }

    // Setup for the bat scenario: a bird dropped on the ground at a spot, facing the musician, as WaveDirector
    // spawns them.
    static Enemy SpawnBird(Vector3 at, bool small) {
        if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y + 0.45f;
        }
        var go = new GameObject(small ? "Hatchling" : "Turkey");
        go.tag = "enemy";
        go.transform.position = at;
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        Vector3 look = piano - at;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) {
            go.transform.rotation = Quaternion.LookRotation(look);
        }
        Enemy enemy = go.AddComponent<Enemy>();
        if (small) {
            enemy.ApplyHatchling();
        } else {
            enemy.ApplyFowl(FowlRoster.ForSpawn(FowlKind.Turkey, WaveDirector.Wave));
        }
        return enemy;
    }

    // ---------------------------------------------------------------- site

    // The website's footer marks (written by WebGlBuild.InstallSiteFooter on every deploy). Read-only: it fetches the
    // pages and, so the recording shows them, lays each one over the game for a few seconds (HawkesSite.jslib).
    [System.Serializable]
    class SitePage {
        public string path;
        public int status;
        public bool powered;
        public string poweredText;
        public bool parent;
        public string parentText;
        public string error;
    }

    [System.Serializable]
    class SiteCheck {
        public string root;
        public int listed;
        public string blog;
        public SitePage[] pages;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteCheckStart();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern string HawkesSiteCheckResult();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern int HawkesGamePageHasFooter();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteShow(string path);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesSiteHide();

    const bool InBrowser = true;
#else
    static void HawkesSiteCheckStart() { }
    static string HawkesSiteCheckResult() { return ""; }
    static int HawkesGamePageHasFooter() { return 0; }
    static void HawkesSiteShow(string path) { }
    static void HawkesSiteHide() { }

    const bool InBrowser = false;
#endif

    static IEnumerator SiteRun(PlaytestOptions options) {
        yield return ToMenu();
        if (run.Done) {
            yield break;
        }
        if (HawkesGamePageHasFooter() != 0) {
            run.Problem(PlaytestSeverity.Bug, "the game page carries the website footer; the game screen must stay as it was");
        } else {
            run.Event("game-page", "no site footer on the game page");
        }
        run.Progress(0.1f);

        if (!InBrowser) {
            run.Problem(PlaytestSeverity.Warning, "the site check needs the WebGL build in a browser; skipped");
        } else {
            run.Note("reading the website's pages");
            HawkesSiteCheckStart();
            yield return run.Until(() => HawkesSiteCheckResult() != "", 45f, "the website's pages to load");
            if (run.Done) {
                yield break;
            }
            SiteCheck check = JsonUtility.FromJson<SiteCheck>(HawkesSiteCheckResult());
            SitePage[] pages = check != null && check.pages != null ? check.pages : new SitePage[0];
            int powered = 0;
            foreach (SitePage page in pages) {
                if (page.status != 200) {
                    run.Problem(PlaytestSeverity.Bug, page.path + " didn't load (" + (string.IsNullOrEmpty(page.error) ? "HTTP " + page.status : page.error) + ")");
                    continue;
                }
                if (page.powered) {
                    powered++;
                    run.Event("powered-by", page.path + ": \"" + page.poweredText + "\" links to https://hawkesgen.com");
                } else {
                    run.Problem(PlaytestSeverity.Bug, page.path + " has no \"Powered by HAWKES GEN\" link to https://hawkesgen.com");
                }
                if (page.path == "/index.html") {
                    if (page.parent) {
                        run.Event("parent-link", page.path + ": \"" + page.parentText + "\" links to https://hawkesbiomedical.com");
                    } else {
                        run.Problem(PlaytestSeverity.Bug, "the landing page has no link to https://hawkesbiomedical.com");
                    }
                    run.Metric("landingParentLink", page.parent ? 1 : 0);
                }
            }
            if (pages.Length == 0) {
                run.Problem(PlaytestSeverity.Bug, "no website pages found around the game");
            }

            // Which pages the site has: the landing page, its sub pages and its blog, if it has one.
            List<string> paths = new List<string>();
            List<string> subPages = new List<string>();
            foreach (SitePage page in pages) {
                paths.Add(page.path);
                if (page.path != "/index.html") {
                    subPages.Add(page.path);
                }
            }
            run.Event("site-pages", pages.Length + " page(s) outside the game: " + string.Join(", ", paths)
                + (check != null && check.listed > 0 ? " (" + check.listed + " listed by the deploy in site/pages.json)" : " (no site/pages.json from the deploy; found by links only)"));
            if (check == null || check.listed == 0) {
                run.Problem(PlaytestSeverity.Warning, "the site has no site/pages.json (written by the WebGL deploy), so only linked pages were checked");
            }
            run.Event("sub-pages", subPages.Count > 0
                ? subPages.Count + " sub page(s): " + string.Join(", ", subPages)
                : "the site has no sub pages besides the landing page");
            string blog = check != null ? check.blog : "";
            if (!string.IsNullOrEmpty(blog)) {
                SitePage blogPage = System.Array.Find(pages, p => p.path == blog);
                run.Event("blog", blog + (blogPage != null && blogPage.powered ? ": has \"Powered by HAWKES GEN\"" : ": no Powered by mark"));
            } else {
                run.Event("blog", "the site has no blog (none at /blog/, /blog.html, /news/ or /posts/)");
            }
            run.Metric("sitePages", pages.Length);
            run.Metric("siteSubPages", subPages.Count);
            run.Metric("siteBlog", string.IsNullOrEmpty(blog) ? 0 : 1);
            run.Metric("sitePagesPowered", powered);
            run.Progress(0.3f);

            // Show each page's footer (the landing page first), a few seconds each, then back to the game.
            int shown = 0;
            foreach (SitePage page in pages) {
                if (page.status != 200 || shown >= 8) {
                    continue;
                }
                run.Scene("Site " + page.path);
                HawkesSiteShow(page.path);
                run.Event("site-shown", page.path + " footer" + (page.powered ? ": Powered by HAWKES GEN" : ": no Powered by mark") + (page.parent ? ", A Hawkes Biomedical company" : ""));
                yield return new WaitForSecondsRealtime(4f);
                HawkesSiteHide();
                shown++;
                run.Progress(0.3f + 0.4f * shown / Mathf.Max(1, Mathf.Min(8, pages.Length)));
            }
        }

        run.Scene("Main Menu");
        run.Event("game-screen", "back on the game: the menu and a short run, unchanged");
        yield return new WaitForSecondsRealtime(2f);
        yield return StartRun();
        if (run.Done) {
            yield break;
        }
        yield return Play(options, 20f, false);
    }

    // Touch only, as on a phone: no keys at all. Taps on the menu and the cards go through the UI event system
    // like a finger's; the stick, Swing, Lock and Next wave get the bot's fingers through TouchControls, the same
    // path real touches take. Setup: touch controls are switched on (a phone has them on by itself) and the menu
    // is loaded fresh so it is built for touch.
    static IEnumerator TouchRun() {
        UiFit.ForceTouch(true);
        run.Log("layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height + ", pixel ratio " + UiFit.PixelRatio);
        run.Metric("viewportWidth", Mathf.Round(Screen.width / UiFit.PixelRatio));
        run.Metric("viewportHeight", Mathf.Round(Screen.height / UiFit.PixelRatio));
        run.Scene("Main Menu");
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return null;
        yield return run.Until(() => Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the main menu");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        CheckScreen("menu");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        Button play = menu != null ? menu.PlayButton : null;
        if (play == null || !play.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Rect playRect = ScreenRect((RectTransform)play.transform);
        run.Metric("playButtonPx", Mathf.Round(playRect.height / UiFit.PixelRatio));
        if (!Inside(playRect)) {
            run.Problem(PlaytestSeverity.Bug, "the Play button is not fully on screen");
        }

        run.Note("tapping Play");
        Tap(play.gameObject);
        run.Event("tap", "Play");
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after tapping Play");
        if (run.Done) {
            yield break;
        }
        run.Scene("Wave 1");
        run.Event("run-start");
        Random.InitState(run.Options.seed);
        yield return TouchPlay(300f);
    }

    static IEnumerator TouchPlay(float seconds) {
        float start = Time.realtimeSinceStartup;
        float nextMetrics = 0f;
        float nextTap = 0f;
        float cardSeenAt = -1f;
        float waveTwoAt = -1f;
        bool stickDown = false;
        bool fireDown = false;
        float nextSwing = 0f;
        bool tapping = false;
        int tapFinger = -1;
        bool checkedHud = false;
        bool checkedCards = false;
        Vector2 stickAt = Vector2.zero;
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - start;
            SampleFps();

            if (WaveDirector.IsLost) {
                TouchControls.LiftBotFingers();
                Metrics(elapsed);
                run.Event("lost", "wave " + WaveDirector.Wave);
                run.Scene("Game Over");
                if (WaveDirector.Wave < 2) {
                    run.Problem(PlaytestSeverity.Warning, "the musician fell before wave 2 with touch controls");
                }
                yield return new WaitForSecondsRealtime(2.5f);
                TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), true);
                yield return null;
                yield return null;
                TouchControls.BotFinger(9, Vector2.zero, false);
                yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene, 20f, "the menu after tapping the game-over screen");
                if (!run.Done) {
                    run.Scene("Main Menu");
                    run.Finish(PlaytestOutcome.Lost);
                }
                yield break;
            }
            if (elapsed >= seconds) {
                run.Problem(PlaytestSeverity.Warning, "touch play did not reach wave 2 and a level-up in " + seconds.ToString("0") + " s (wave " + WaveDirector.Wave + ", " + levelUps + " level-ups)");
                yield break;
            }

            // A one-frame tap from last frame lifts now.
            if (tapping) {
                TouchControls.BotFinger(tapFinger, Vector2.zero, false);
                tapping = false;
            }

            if (LevelUpScreen.IsOpen) {
                if (stickDown || fireDown) {
                    TouchControls.LiftBotFingers();
                    stickDown = false;
                    fireDown = false;
                }
                if (cardSeenAt < 0f) {
                    cardSeenAt = now;
                    run.Event("level-up", "level " + RunBoons.Level);
                    run.Scene("Level Up");
                }
                if (now - cardSeenAt > 0.8f) {
                    if (!checkedCards) {
                        checkedCards = true;
                        CheckScreen("cards", 400);
                    }
                    Button card = Card(Random.Range(0, 3));
                    if (card == null) {
                        run.Problem(PlaytestSeverity.Bug, "no level-up card to tap");
                        run.Finish(PlaytestOutcome.Stuck);
                        yield break;
                    }
                    Tap(card.gameObject);
                    yield return null;
                    yield return null;
                    if (LevelUpScreen.IsOpen) {
                        yield return new WaitForSecondsRealtime(1f);
                        if (LevelUpScreen.IsOpen) {
                            run.Problem(PlaytestSeverity.Bug, "the level-up card did not close after a tap");
                            run.Finish(PlaytestOutcome.Stuck);
                            yield break;
                        }
                    }
                    levelUps++;
                    run.Add("levelUps");
                    run.Log("tapped " + card.name);
                    cardSeenAt = -1f;
                    run.Scene("Wave " + WaveDirector.Wave);
                }
                yield return null;
                continue;
            }

            bool building = WaveDirector.IsBuilding;
            bool birds = Enemy.Roster.Count > 0;

            // The stick: a lap around the start so the stick is seen working, then to the nearest bird near the
            // musician (back to the piano when there is none).
            Enemy target = building || player == null ? null : GuardTarget(player, null);
            float gap = float.MaxValue;
            Vector2 toward = SteerDir(player);
            if (target != null) {
                Vector3 flat = target.transform.position - player.transform.position;
                flat.y = 0f;
                gap = flat.magnitude - target.BodyScale * 0.3f;
                toward = gap > BatEquipment.Reach * 0.6f ? StickToward(flat) : Vector2.zero;
            }
            Vector2 dir = elapsed < 5f ? new Vector2(Mathf.Sin(elapsed * 1.3f), Mathf.Cos(elapsed * 1.3f)) * 0.8f : toward;
            if (building) {
                dir = Vector2.zero;
            }
            if (dir.magnitude > 0.05f) {
                if (!stickDown) {
                    if (TouchControls.Spot("Stick", out stickAt)) {
                        TouchControls.BotFinger(0, stickAt, true);
                        stickDown = true;
                    }
                } else {
                    TouchControls.BotFinger(0, stickAt + Vector2.ClampMagnitude(dir, 1f) * TouchControls.StickReach * 0.9f, true);
                }
            } else if (stickDown) {
                TouchControls.BotFinger(0, stickAt, false);
                stickDown = false;
            }

            // Swing tapped while a bird is in the bat's reach; Lock toggled on with birds, off without.
            bool swing = !building && target != null && gap <= BatEquipment.Reach * 0.95f && !LevelUpScreen.HoldFire;
            if (fireDown) {
                TouchControls.BotFinger(1, Vector2.zero, false);
                fireDown = false;
            } else if (swing && now >= nextSwing && TouchControls.Spot("Swing", out Vector2 swingAt)) {
                TouchControls.BotFinger(1, swingAt, true);
                fireDown = true;
                nextSwing = now + 0.15f;
            }
            if (!tapping && now >= nextTap) {
                Vector2 at;
                if (WaveDirector.Waiting && TouchControls.Spot("Next", out at)) {
                    TouchControls.BotFinger(3, at, true);
                    tapping = true;
                    tapFinger = 3;
                    nextTap = now + 1.5f;
                    run.Event("tap", "Next wave");
                } else if (!building && birds != TouchControls.Locked && TouchControls.Spot("Lock", out at)) {
                    TouchControls.BotFinger(2, at, true);
                    tapping = true;
                    tapFinger = 2;
                    nextTap = now + 0.6f;
                }
            }

            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                if (lastWave != 0) {
                    run.Event("wave", wave.ToString());
                }
                lastWave = wave;
                run.Scene("Wave " + wave);
                if (wave >= 2 && waveTwoAt < 0f) {
                    waveTwoAt = now;
                }
            }
            if (!checkedHud && elapsed > 8f && birds) {
                checkedHud = true;
                CheckScreen("hud");
            }
            // Done: wave 2 under way and at least one card picked by touch.
            if (waveTwoAt > 0f && now - waveTwoAt > 10f && levelUps > 0) {
                Metrics(elapsed);
                run.Event("done", "wave " + wave + " by touch");
                yield break;
            }

            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(elapsed);
                run.Metric("touchLocked", TouchControls.Locked ? 1 : 0);
                run.Progress(Mathf.Clamp01((wave - 1 + (levelUps > 0 ? 0.5f : 0f)) / 2f));
                run.Note(building ? "build phase" : Enemy.Roster.Count + " birds, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
            }
            yield return null;
        }
    }

    // A ground direction as the stick would push it, camera-relative.
    static Vector2 StickToward(Vector3 flat) {
        Camera cam = Camera.main;
        if (cam == null || flat.sqrMagnitude < 0.0001f) {
            return Vector2.zero;
        }
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        Vector3 right = cam.transform.right;
        right.y = 0f;
        flat.Normalize();
        return new Vector2(Vector3.Dot(right.normalized, flat), Vector3.Dot(fwd.normalized, flat));
    }

    static Vector2 SteerDir(CharacterMovement player) {
        Camera cam = Camera.main;
        if (player == null || Musician.Instance == null || cam == null) {
            return Vector2.zero;
        }
        Vector3 to = Musician.Instance.transform.position - player.transform.position;
        to.y = 0f;
        if (to.magnitude <= 9f) {
            return Vector2.zero;
        }
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        Vector3 right = cam.transform.right;
        right.y = 0f;
        to.Normalize();
        return new Vector2(Vector3.Dot(right.normalized, to), Vector3.Dot(fwd.normalized, to));
    }

    static Button Card(int index) {
        LevelUpScreen screen = Object.FindAnyObjectByType<LevelUpScreen>();
        if (screen == null) {
            return null;
        }
        var cards = new List<Button>();
        foreach (Button button in screen.GetComponentsInChildren<Button>()) {
            if (button.name.StartsWith("Card") && button.isActiveAndEnabled) {
                cards.Add(button);
            }
        }
        return cards.Count == 0 ? null : cards[Mathf.Min(index, cards.Count - 1)];
    }

    // A finger's tap on a UI button: down, up, click.
    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    // What a player would call broken at this screen size: text or a button off the screen, text over text or
    // over a button or touch control, text under 11 px, a button under 44 px. Sizes are CSS pixels. Text
    // scrolled out of its list doesn't count.
    // minOrder skips canvases drawn under a full-screen one (the HUD under the level-up cards).
    static void CheckScreen(string where, int minOrder = 0) {
        float ratio = UiFit.PixelRatio;
        var texts = new List<(Rect rect, string what, Transform owner)>();
        var taps = new List<(Rect rect, string what, Transform owner)>();
        float minText = 999f;
        float minTap = 999f;
        int found = 0;

        foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text) || text.color.a < 0.05f) {
                continue;
            }
            Canvas canvas = text.canvas;
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.rootCanvas.sortingOrder < minOrder) {
                continue;
            }
            if (!Glyphs(text, out Rect rect) || !Unclipped(text.transform, rect)) {
                continue;
            }
            string what = "\"" + Clip(text.text) + "\"";
            float px = text.fontSize * text.transform.lossyScale.y / ratio;
            minText = Mathf.Min(minText, px);
            if (px < 11f) {
                found++;
                run.Problem(PlaytestSeverity.Polish, where + ": text is " + px.ToString("0") + " px: " + what);
            }
            if (!Inside(rect)) {
                found++;
                run.Problem(PlaytestSeverity.Warning, where + ": text runs off the screen: " + what);
            }
            texts.Add((rect, what, text.transform));
        }

        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
            Canvas holder = button.GetComponentInParent<Canvas>();
            if (!button.isActiveAndEnabled || holder == null || holder.rootCanvas.sortingOrder < minOrder) {
                continue;
            }
            var rt = (RectTransform)button.transform;
            Rect rect = ScreenRect(rt);
            if (!Unclipped(rt, rect)) {
                continue;
            }
            Text label = button.GetComponentInChildren<Text>();
            string what = "button " + (label != null ? "\"" + Clip(label.text) + "\"" : button.name);
            float px = Mathf.Min(rect.width, rect.height) / ratio;
            minTap = Mathf.Min(minTap, px);
            if (px < 44f) {
                found++;
                run.Problem(PlaytestSeverity.Polish, where + ": " + what + " is " + px.ToString("0") + " px, under 44");
            }
            if (!Inside(rect)) {
                found++;
                run.Problem(PlaytestSeverity.Warning, where + ": " + what + " is not fully on screen");
            }
            taps.Add((rect, what, rt));
        }
        var controls = new List<RectTransform>();
        TouchControls.Shown(controls);
        foreach (RectTransform control in controls) {
            Rect rect = ScreenRect(control);
            minTap = Mathf.Min(minTap, Mathf.Min(rect.width, rect.height) / ratio);
            taps.Add((rect, "touch " + control.name, control));
        }

        int overlaps = 0;
        for (int i = 0; i < texts.Count; i++) {
            for (int j = i + 1; j < texts.Count; j++) {
                if (Overlap(texts[i].rect, texts[j].rect) > 0.2f && !Related(texts[i].owner, texts[j].owner)) {
                    overlaps++;
                    run.Problem(PlaytestSeverity.Warning, where + ": " + texts[i].what + " overlaps " + texts[j].what);
                }
            }
            foreach (var tap in taps) {
                if (Overlap(texts[i].rect, tap.rect) > 0.2f && !texts[i].owner.IsChildOf(tap.owner)) {
                    overlaps++;
                    run.Problem(PlaytestSeverity.Warning, where + ": " + texts[i].what + " is under " + tap.what);
                }
            }
        }
        for (int i = 0; i < taps.Count; i++) {
            for (int j = i + 1; j < taps.Count; j++) {
                if (Overlap(taps[i].rect, taps[j].rect) > 0.05f && !Related(taps[i].owner, taps[j].owner)) {
                    overlaps++;
                    run.Problem(PlaytestSeverity.Warning, where + ": " + taps[i].what + " overlaps " + taps[j].what);
                }
            }
        }
        run.Metric(where + "MinTextPx", Mathf.Round(minText));
        run.Metric(where + "MinTapPx", Mathf.Round(minTap));
        run.Metric(where + "Overlaps", overlaps);
        run.Event("layout", where + ": " + texts.Count + " texts, " + taps.Count + " buttons, " + overlaps + " overlaps, " + found + " other problems");
    }

    // Where a text's letters are on screen (not its box, which is often bigger).
    static bool Glyphs(Text text, out Rect rect) {
        rect = default;
        IList<UIVertex> verts = text.cachedTextGenerator.verts;
        if (verts == null || verts.Count == 0) {
            return false;
        }
        float unit = 1f / Mathf.Max(0.0001f, text.pixelsPerUnit);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < verts.Count; i++) {
            Vector3 world = text.rectTransform.TransformPoint(verts[i].position * unit);
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.width > 0.5f && rect.height > 0.5f;
    }

    static Rect ScreenRect(RectTransform rect) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    static bool Inside(Rect rect) {
        return rect.xMin >= -2f && rect.yMin >= -2f && rect.xMax <= Screen.width + 2f && rect.yMax <= Screen.height + 2f;
    }

    // False for something in a scrolling list that is scrolled (even partly) out of view.
    static bool Unclipped(Transform item, Rect rect) {
        RectMask2D mask = item.GetComponentInParent<RectMask2D>();
        if (mask == null) {
            return true;
        }
        Rect view = ScreenRect(mask.rectTransform);
        return rect.xMin >= view.xMin - 1f && rect.yMin >= view.yMin - 1f && rect.xMax <= view.xMax + 1f && rect.yMax <= view.yMax + 1f;
    }

    // How much of the smaller rect the two share.
    static float Overlap(Rect a, Rect b) {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        if (w <= 0f || h <= 0f) {
            return 0f;
        }
        float smaller = Mathf.Min(a.width * a.height, b.width * b.height);
        return smaller <= 0f ? 0f : w * h / smaller;
    }

    static bool Related(Transform a, Transform b) {
        return a.IsChildOf(b) || b.IsChildOf(a);
    }

    static string Clip(string text) {
        string line = text.Replace("\n", " ");
        return line.Length > 40 ? line.Substring(0, 40) + "…" : line;
    }

    static IEnumerator GameOver() {
        PlayInput.Release();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        run.Event("lost", "wave " + WaveDirector.Wave);
        run.Scene("Game Over");
        yield return new WaitForSecondsRealtime(1f);
        AnalyticsTimeline();
        // The feedback card ("How was that run?") is the page's, over the game-over screen.
        bool asked = HawkesAnalyticsPageAskShown(RunAnalytics.AskId) == 1;
        if (asked) {
            run.Event("feedback-prompt", "\"How was that run?\" stars and a comment, on the game-over screen");
        } else if (InBrowser && analyticsRun) {
            run.Problem(PlaytestSeverity.Bug, "no \"How was that run?\" feedback card on the game-over screen");
        } else {
            run.Log(InBrowser ? "no feedback card on the game-over screen (shown once a session, then not for 7 days after a close)" : "no feedback card in the editor");
        }
        if (analyticsRun && asked) {
            yield return new WaitForSecondsRealtime(2.5f);
            if (HawkesAnalyticsPageRate(RunAnalytics.AskId, 4) == 1) {
                run.Event("feedback-rated", "4 stars, Skip");
            } else {
                run.Problem(PlaytestSeverity.Bug, "the feedback card's stars could not be pressed");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            AnalyticsTimeline();
        } else {
            yield return new WaitForSecondsRealtime(1.5f);
        }
        PlayInput.Tap(KeyCode.Space);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene, 20f, "the menu after the game-over screen");
        if (!run.Done) {
            run.Scene("Main Menu");
            if (analyticsRun) {
                // The SDK sends a batch every 5 s: wait for the last one so the report can say it went out.
                yield return new WaitForSecondsRealtime(6f);
                AnalyticsReport();
            }
            run.Finish(PlaytestOutcome.Lost);
        }
    }

    // ---------------------------------------------------------------- analytics

    // Every Hawkes Analytics event since the last look, from the SDK's debug log (RunAnalytics turns debug mode on
    // in a bot's browser), as timeline events: "run_start run=1 start_wave=1", "level_end level=1 result=complete…".
    // The first time, it says which product, build and env every batch carries.
    static void AnalyticsTimeline() {
        string text = HawkesAnalyticsPageLog(analyticsSeq);
        if (string.IsNullOrEmpty(text)) {
            return;
        }
        string[] lines = text.Split('\n');
        string[] config = lines[0].Split('\t');
        if (config.Length >= 7) {
            int.TryParse(config[6], out analyticsSent);
            if (!analyticsConfig) {
                analyticsConfig = true;
                run.Event("analytics-config", "product " + config[1] + ", build " + (config[2] == "" ? "(none)" : config[2])
                    + (config[3] == "" ? "" : " (" + config[3].Substring(0, Mathf.Min(7, config[3].Length)) + ")")
                    + ", env " + config[4] + ", debug " + (config[5] == "1" ? "on" : "off"));
            }
        }
        for (int i = 1; i < lines.Length; i++) {
            string[] parts = lines[i].Split('\t');
            if (parts.Length < 4 || !int.TryParse(parts[0], out int seq)) {
                continue;
            }
            analyticsSeq = Mathf.Max(analyticsSeq, seq);
            analyticsEvents++;
            if (parts[1] == "perf") {
                continue;
            }
            run.Event("analytics", parts[1] + (parts[3] == "" ? "" : " " + parts[3]));
        }
    }

    // How many of the events went out, and the build and env they carried (said again at the end).
    static void AnalyticsReport() {
        AnalyticsTimeline();
        run.Metric("analyticsEvents", analyticsEvents);
        run.Metric("analyticsSent", analyticsSent);
        if (!InBrowser) {
            run.Log("analytics: nothing is sent in the editor");
            return;
        }
        if (!analyticsConfig) {
            run.Problem(PlaytestSeverity.Bug, "the Hawkes Analytics SDK is not on the page (window.hawkesAnalytics), so no events went out");
            return;
        }
        run.Event("analytics-sent", analyticsSent + " of " + analyticsEvents + " events sent");
        if (analyticsSent == 0) {
            run.Problem(PlaytestSeverity.Bug, "no analytics batch went out (the collect endpoint was not reached)");
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
        run.Metric("swings", RunLog.RunSwings);
        run.Metric("hits", RunLog.RunHits);
        run.Metric("hitRate", RunLog.RunSwings > 0 ? Mathf.Round(100f * RunLog.RunConnects / RunLog.RunSwings) / 100f : 0f);
        run.Metric("finishers", BatEquipment.Finishers);
        run.Metric("smashHits", BatEquipment.SmashHits);
        run.Metric("homeRuns", BatEquipment.HomeRuns);
        run.Metric("homeRunKnocks", BatEquipment.HomeRunKnocks);
        run.Metric("knockedBack", BatEquipment.KnockedBack);
        run.Metric("lockNearest", lockNearest);
        run.Metric("lockWrong", lockWrong);
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
