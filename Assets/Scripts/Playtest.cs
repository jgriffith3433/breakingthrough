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
        new PlaytestScenario("smoke", "Boot, menu, start a run, about 30 s of play: the bot walks to bots near the musician and swings the bat (lock-on nearest, combo swings)", 45, "smoke"),
        new PlaytestScenario("run", "A full run until the musician falls, picking level-ups: the bot guards the musician with the bat (walks to the nearest bot near the piano, locks on, swings, walks back). Should pass wave 5 without 'no kills for 45 s'", 600, "full"),
        new PlaytestScenario("bat", "The bat: a 3-hit combo on wave 1's Yo Bots (hits and knockback, the third further), a ground smash on small Yo Bots dropped in front of the player (setup), then a charged home run into a line of Yo Bots (setup) that bowls them over; lock-on picks the nearest bot throughout. Then a minute of play", 200, "full"),
        new PlaytestScenario("menus", "Shop menu, start a run, a level-up card, reroll and pick", 120, "menus"),
        new PlaytestScenario("words", "Reads the menu, the shop, the help line, build tips, pause, level-up cards and the game-over screen for retired cast words. Setup: the testing wave key visits the tip waves, then wave 2 is held until Slice falls", 300, "menus"),
        new PlaytestScenario("touch", "Touch only, for a phone-sized screen (mobile, or 844x390): the menu fits, tap Play, walk to the bots with the on-screen stick and tap Swing, tap a level-up card, play to wave 2", 330, "menus"),
        new PlaytestScenario("settings", "Press Tap to play, open Settings from the menu: sound off and on, music and effects sliders, Done; start a run, pause (button, then P), Settings from the pause menu, resume, play a little. Any screen size", 100, "menus"),
        new PlaytestScenario("bots", "The bots: wave 1's small Yo Bots walk at the musician (their distance to him is logged as they come), destroyed bots drop bolts the bot picks up as XP, and the enemy list on the right names them. Setup shortcut: the testing wave key (=) jumps to wave 30, where the first Spider Bot arrives as the Alpha, a large enemy with the boss bar, then the bot fights it for 30 s", 300, "content"),
        new PlaytestScenario("analytics", "Hawkes Analytics: start a run, clear wave 1, then hold fire on wave 2 so the bots take the musician. Every analytics event is logged as it goes out (run_start, level_start and level_end per wave, card picks, the death and the bot kind that landed it, run_end) with the build version and env; on the game-over screen the \"How was that run?\" card must show, and the bot rates it 4 stars", 240, "full"),
        new PlaytestScenario("musician", "Slice, the musician (the pirate pianist's model stands in): her health bar is titled Slice. The bot holds fire by the piano so wave 1's Yo Bots reach her: she stands, stabs the one in reach with her knife (a pale slash; it takes damage) at most once every 2.5 s, logged per stab; then the bot clears the wave and she sits back down to play. Then it holds fire from wave 2 (setup: the testing wave key (=) if she outlasts it) until the bots empty her bar and the run ends", 330, "content"),
        new PlaytestScenario("upgrades", "The bat upgrades: the shop lists Corked Core and Aluminum! Not wood! (setup: both owned in the sandbox) and the metal bat is in the player's hands; wave 1's first level-up cards are read off the screen (bat upgrades, no retired cast words); setup: the testing wave key (=) to wave 14 for tougher Plated Yo Bots and a handful of bat boons granted (Wide Arc, Pine Tar, Ice Bat, Live Wire, Grand Slam, Long Ball and more), each one's mark on the bat logged; then the bot plays until each effect is seen: a wider swing, bots burning, bots slowed, chain sparks shorting bots out, a 360 spin swing, batted bolts landing. Then Thunder Bat is granted for a lightning spin, and 30 s of play", 330, "content"),
        new PlaytestScenario("wallit", "Wallit's model (not wired to gameplay yet): after Tap to play and a short run start, spawns the Wallit prefab by the piano, checks wallet body / bills / gloves / sneakers / bat from front and side, then plays each clip on its Animator (idle, run, strafes, back-pedal, swing 1-3, ground smash, home-run charge/release, jump, landing, hit, shake-off, victory, death) and logs torn-limb or missing-clip problems", 180, "content"),
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
    // The analytics scenario: from this wave on the bot holds fire, so the bots end the run.
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
    // The musician scenario: Slice's stabs and stands told so far.
    static int sliceStabsTold;
    static float sliceLastStab;
    static float sliceGapMin;
    static bool sliceWasUp;
    static float sliceClearAt;
    static int sliceSits;
    static int sliceShortGaps;
    static int sliceNoDamage;

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
            if (id == "words") {
                yield return WordsMenu();
            }
            if (id == "upgrades") {
                UpgradesShop();
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
            } else if (id == "bots") {
                yield return BotsRun(options);
            } else if (id == "musician") {
                yield return MusicianRun(options);
            } else if (id == "upgrades") {
                yield return UpgradesRun(options);
                if (!run.Done) {
                    UpgradesReport();
                    yield return Play(options, 30f, false);
                }
            } else if (id == "bat") {
                yield return BatRun(options);
                if (!run.Done) {
                    yield return Play(options, 60f, false);
                }
                if (!run.Done) {
                    BatReport();
                }
            } else if (id == "wallit") {
                yield return WallitRun(options);
            } else if (id == "words") {
                yield return WordsRun();
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
                    run.Event("hold-fire", "wave " + wave + ": the bot stops swinging so the bots reach the musician");
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
                run.Problem(PlaytestSeverity.Warning, "no kills for 45 s at wave " + wave + " with " + Enemy.Roster.Count + " bots alive");
            }
            if (now - waveSince > 240f) {
                run.Problem(PlaytestSeverity.Bug, "wave " + wave + " did not end in 240 s (" + Enemy.Roster.Count + " bots alive)");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }

            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(elapsed);
                AnalyticsTimeline();
                run.Progress(seconds > 0f ? elapsed / seconds : 0f);
                run.Note(building ? "build phase" : Enemy.Roster.Count + " bots, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
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
            run.Event("lock-switch", "right click: the lock moved to the next nearest bot, " + locked.name + " at " + dist.ToString("0.0") + " m (" + inReach + " in reach)");
            return;
        }
        if (nearest == null || nearest.gameObject == locked || dist <= best + 0.75f) {
            lockNearest++;
            if (batRun || lockNearest <= 6) {
                run.Event("lock-on", "the nearest bot: " + locked.name + " at " + dist.ToString("0.0") + " m of " + inReach + " in reach" + (hadOne ? " (the last one fell)" : ""));
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
        run.Note("swinging the 3-hit combo at wave 1's Yo Bots");
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
        run.Event("combo", BatEquipment.Swings + " swings, " + BatEquipment.Finishers + " finishers (swing 3), " + BatEquipment.KnockedBack + " bots knocked back");
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
        run.Event("setup", "a line of 4 Yo Bots dropped 6.5 to 12 m in front of the player, walking in toward the piano");

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
            run.Log("the home run flew without hitting another bot; trying again");
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
    static IEnumerator Fight(CharacterMovement player, System.Func<bool> goal, float seconds, string what, List<Enemy> only, bool lockOn, bool bat = true) {
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
                run.Problem(PlaytestSeverity.Bug, "the " + (bat ? "bat" : "upgrades") + " scenario did not see " + what + " in " + seconds.ToString("0") + " s");
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
            run.Problem(PlaytestSeverity.Bug, "no 3-hit combo landed (no swing 3 hit a bot)");
        }
        if (BatEquipment.KnockedBack == 0) {
            run.Problem(PlaytestSeverity.Bug, "the bat hit no bot hard enough to knock it back");
        }
        if (BatEquipment.SmashHits == 0) {
            run.Problem(PlaytestSeverity.Bug, "no ground smash hit a small bot");
        }
        if (BatEquipment.HomeRuns == 0) {
            run.Problem(PlaytestSeverity.Bug, "no home run swing went off after holding the button");
        } else if (BatEquipment.HomeRunKnocks == 0) {
            run.Problem(PlaytestSeverity.Bug, "the home run launched a bot that knocked into no other bot");
        }
        run.Event("bat-report", BatEquipment.Swings + " swings, " + BatEquipment.Finishers + " finishers, " + BatEquipment.SmashHits + " smash hits, "
            + BatEquipment.HomeRuns + " home runs knocking into " + BatEquipment.HomeRunKnocks + " bots, lock nearest " + lockNearest + " of " + lockChecks);
    }

    // ---------------------------------------------------------------- wallit model

    static readonly string[] WallitClips = {
        "idle", "run", "strafeLeft", "strafeRight", "backPedal",
        "swing1", "swing2", "swing3", "groundSmash",
        "homeRunCharge", "homeRunRelease",
        "jump", "landing", "hitReact", "shakeOff", "victory", "death"
    };

    // Spawns the Wallit prefab (Resources/Wallit/Wallit) by the piano, checks look, plays every clip. Not wired to
    // the player — Warrok stays in control. Setup shortcut: none; needs the Gen bake committed under Assets/Art/Wallit.
    static IEnumerator WallitRun(PlaytestOptions options) {
        run.Scene("Wallit showcase");
        // Hold the wave so bots don't crowd the shot.
        PlayInput.PadKey(KeyCode.Equals, 0.05f);
        yield return Wait(0.2f);
        PlayInput.Release();

        GameObject prefab = Resources.Load<GameObject>("Wallit/Wallit");
        if (prefab == null) {
            run.Event("wallit-prefab", "missing");
            run.Problem(PlaytestSeverity.Bug, "Wallit prefab missing at Resources/Wallit/Wallit (run Tools → Wallit → Build Prefab after Gen's FBX lands in Assets/Art/Wallit)");
            yield break;
        }
        run.Event("wallit-prefab", "loaded");

        Vector3 at = Musician.Instance != null ? Musician.Instance.transform.position + Vector3.forward * 4f : Vector3.zero;
        if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        GameObject wallit = Object.Instantiate(prefab, at, Quaternion.identity);
        wallit.name = "WallitShowcase";

        Animator animator = wallit.GetComponentInChildren<Animator>();
        if (animator == null) {
            run.Problem(PlaytestSeverity.Bug, "Wallit prefab has no Animator");
            Object.Destroy(wallit);
            yield break;
        }

        bool human = animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid;
        run.Event("wallit-humanoid", human
            ? "avatar valid human (" + animator.avatar.name + ")"
            : "avatar missing or not a valid Humanoid");
        if (!human) {
            run.Problem(PlaytestSeverity.Bug, "Wallit import is not a valid Humanoid avatar");
        }

        // Front and side looks: park the camera, log what renderers say about wallet / bills / bat.
        yield return WallitLook(wallit, "front", at + wallit.transform.forward * 3.2f + Vector3.up * 1.2f);
        yield return WallitLook(wallit, "side", at + wallit.transform.right * 3.2f + Vector3.up * 1.2f);

        int played = 0;
        int missing = 0;
        for (int i = 0; i < WallitClips.Length; i++) {
            string clip = WallitClips[i];
            if (!AnimatorHasState(animator, 0, clip)) {
                run.Event("wallit-clip", clip + ": missing");
                missing++;
                continue;
            }
            animator.SetInteger("Clip", i);
            animator.Play(clip, 0, 0f);
            yield return Wait(1.15f);
            bool torn = WallitLooksTorn(wallit);
            run.Event("wallit-clip", clip + (torn ? ": torn" : ": ok"));
            if (torn) {
                run.Problem(PlaytestSeverity.Bug, "Wallit clip '" + clip + "' looks torn (a limb or the bat stretched far from the body)");
            }
            played++;
            run.Progress((i + 1f) / WallitClips.Length);
        }
        run.Metric("wallitClipsPlayed", played);
        run.Metric("wallitClipsMissing", missing);
        if (missing > 0) {
            run.Problem(PlaytestSeverity.Bug, missing + " Wallit clips missing from the Animator");
        }
        run.Event("wallit-report", played + " clips played, " + missing + " missing, humanoid " + (human ? "ok" : "bad"));
        Object.Destroy(wallit);
    }

    static IEnumerator WallitLook(GameObject wallit, string angle, Vector3 camPos) {
        Camera cam = Camera.main;
        if (cam != null) {
            cam.transform.position = camPos;
            cam.transform.LookAt(wallit.transform.position + Vector3.up * 0.9f);
        }
        yield return Wait(0.6f);
        string parts = WallitPartLine(wallit);
        run.Event("wallit-look-" + angle, parts);
        if (parts.IndexOf("wallet") < 0 && parts.IndexOf("body") < 0) {
            run.Problem(PlaytestSeverity.Warning, "Wallit " + angle + " view did not name a wallet/body mesh (" + parts + ")");
        }
    }

    static string WallitPartLine(GameObject wallit) {
        var bits = new List<string>();
        foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>()) {
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("bill") || n.Contains("cash") || n.Contains("money"))
                bits.Add("bills:" + r.gameObject.name);
            else if (n.Contains("bat"))
                bits.Add("bat:" + r.gameObject.name);
            else if (n.Contains("glove") || n.Contains("hand"))
                bits.Add("gloves:" + r.gameObject.name);
            else if (n.Contains("shoe") || n.Contains("sneaker") || n.Contains("foot"))
                bits.Add("sneakers:" + r.gameObject.name);
            else if (n.Contains("wallet") || n.Contains("body") || n.Contains("torso") || n.Contains("head"))
                bits.Add("wallet:" + r.gameObject.name);
        }
        if (bits.Count == 0) {
            foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>()) {
                bits.Add(r.gameObject.name);
                if (bits.Count >= 6)
                    break;
            }
        }
        return bits.Count > 0 ? string.Join(", ", bits.ToArray()) : "no renderers";
    }

    static bool WallitLooksTorn(GameObject wallit) {
        // Heuristic: any skinned bone or renderer center more than 3.5 m from the root is a torn limb.
        Vector3 root = wallit.transform.position;
        foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>()) {
            if (Vector3.Distance(root, r.bounds.center) > 3.5f)
                return true;
        }
        foreach (Transform t in wallit.GetComponentsInChildren<Transform>()) {
            if (t.name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                if (Vector3.Distance(root, t.position) > 3.2f)
                    return true;
            }
        }
        return false;
    }

    static bool AnimatorHasState(Animator animator, int layer, string stateName) {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;
        int hash = Animator.StringToHash(stateName);
        return animator.HasState(layer, hash);
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
        var go = new GameObject(small ? "Drone" : "Yo Bot");
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
            enemy.ApplyDrone();
        } else {
            enemy.ApplyBot(BotRoster.ForSpawn(BotKind.YoBot, WaveDirector.Wave));
        }
        return enemy;
    }

    // ---------------------------------------------------------------- musician

    // Slice's knife. 1: the bar says Slice. 2: the bot waits by the piano (no swings) until wave 1's Yo Bots reach her
    // and she has stabbed three times, each logged with its damage and the gap since the last. 3: the bot clears the
    // wave and she must sit back down to play. 4: it holds fire from wave 2 until the bots empty her bar.
    static IEnumerator MusicianRun(PlaytestOptions options) {
        Musician slice = Musician.Instance;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (slice == null) {
            yield break;
        }
        sliceStabsTold = 0;
        sliceLastStab = -99f;
        sliceGapMin = 999f;
        sliceWasUp = false;
        sliceClearAt = -1f;
        sliceSits = 0;
        sliceShortGaps = 0;
        sliceNoDamage = 0;

        run.Scene("Slice: the health bar");
        string title = slice.Title;
        run.Event("health-bar", "titled \"" + title + "\", " + slice.Health + " / " + slice.MaxHealth);
        if (title != Musician.Name) {
            run.Problem(PlaytestSeverity.Bug, "the musician's health bar says \"" + title + "\", not \"" + Musician.Name + "\"");
        }

        yield return run.Until(() => YoBotsAlive() > 0, 25f, "wave 1's Yo Bots");
        if (run.Done) {
            yield break;
        }
        run.Scene("Slice: the knife");
        run.Event("hold-fire", "the bot waits by the piano without swinging, so wave 1's Yo Bots reach Slice");
        yield return GuardSlice(player, () => slice.Stabs >= 3 || slice.Health < slice.MaxHealth * 0.5f, 90f, false, "three stabs");
        if (run.Done) {
            yield break;
        }
        if (slice.Stabs == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice never stabbed: Yo Bots reached her but the knife did nothing");
        }

        run.Scene("Slice: back to the keys");
        run.Event("fight", "the bot clears the wave; Slice should sit back down and play");
        yield return GuardSlice(player, () => sliceSits > 0 && slice.Seated && sliceClearAt > 0f && Time.time - sliceClearAt > 2f, 90f, true, "Slice sitting back down at the piano");
        if (!run.Done && slice.Seated) {
            run.Event("slice-playing", "nothing in reach for " + (Time.time - sliceClearAt).ToString("0.0") + " s: she is on the bench, the piano clip playing");
        }
        if (run.Done) {
            yield break;
        }

        run.Scene("Slice: the run ends");
        run.Event("hold-fire", "from here the bot holds fire, so the bots empty Slice's bar");
        float holdStart = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        int skips = 0;
        while (!run.Done) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            WatchSlice(slice);
            if (WaveDirector.IsLost) {
                if (slice.Health > 0) {
                    run.Problem(PlaytestSeverity.Bug, "the run ended with Slice at " + slice.Health + " health");
                }
                SliceReport(slice);
                yield return GameOver();
                yield break;
            }
            if (now - holdStart > 200f) {
                run.Problem(PlaytestSeverity.Bug, "the run did not end in 200 s of holding fire (Slice at " + slice.Health + ")");
                SliceReport(slice);
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            Batter(player, true, null, false);
            bool clear = WaveDirector.IsBuilding || Enemy.Roster.Count == 0;
            if (clear && now >= nextSkip) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            // Setup: if she outlasts the early waves, the testing wave key brings bigger ones.
            if (now - holdStart > 60f + skips * 30f && skips < 4) {
                skips++;
                for (int i = 0; i < 3; i++) {
                    PlayInput.Tap(KeyCode.Equals);
                    yield return null;
                    yield return null;
                }
                run.Event("setup", "testing wave key (=) three times: wave " + WaveDirector.Wave);
            }
            int wave = WaveDirector.Wave;
            if (wave != lastWave) {
                lastWave = wave;
                run.Event("wave", wave.ToString());
                run.Scene("Wave " + wave);
            }
            yield return null;
        }
    }

    // The batter (or not, when idle), cards and breaks, watching Slice, until goal. Not seeing it in time is a bug.
    static IEnumerator GuardSlice(CharacterMovement player, System.Func<bool> goal, float seconds, bool fight, string what) {
        Musician slice = Musician.Instance;
        float start = Time.realtimeSinceStartup;
        float nextSkip = 0f;
        float nextMetrics = 0f;
        while (!run.Done && slice != null && !goal()) {
            float now = Time.realtimeSinceStartup;
            SampleFps();
            WatchSlice(slice);
            if (WaveDirector.IsLost) {
                SliceReport(slice);
                yield return GameOver();
                yield break;
            }
            if (now - start > seconds) {
                run.Problem(PlaytestSeverity.Bug, "the musician scenario did not see " + what + " in " + seconds.ToString("0") + " s");
                yield break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return PickCard();
                continue;
            }
            bool building = WaveDirector.IsBuilding;
            Batter(player, !fight || building, null, fight);
            if (fight && (building || Enemy.Roster.Count == 0) && now >= nextSkip && sliceSits > 0) {
                nextSkip = now + 1.5f;
                PlayInput.Tap(KeyCode.Return);
            }
            if (now >= nextMetrics) {
                nextMetrics = now + 0.5f;
                Metrics(now - run.StartTime);
                run.Note(Enemy.Roster.Count + " bots, Slice " + slice.Health + (slice.Standing ? ", standing" : slice.Seated ? ", at the keys" : ""));
            }
            yield return null;
        }
        WatchSlice(slice);
    }

    // One timeline line per stab (who, how much, the health it took, the gap since the last) and per stand and sit.
    static void WatchSlice(Musician slice) {
        if (slice == null) {
            return;
        }
        bool up = !slice.Seated;
        if (up && !sliceWasUp) {
            run.Event("slice-stands", "a bot came within " + Musician.StabReach.ToString("0.0") + " m: she gets up from the bench");
        }
        if (!up && sliceWasUp) {
            sliceSits++;
            string after = sliceClearAt > 0f ? ", " + (Time.time - sliceClearAt).ToString("0.0") + " s after the last bot left her reach" : "";
            run.Event("slice-sits", "back on the bench playing" + after);
        }
        sliceWasUp = up;
        bool near = false;
        Vector3 at = slice.transform.position;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            Vector3 to = enemy.transform.position - at;
            to.y = 0f;
            if (to.magnitude - enemy.BodyScale * 0.22f <= Musician.StabReach) {
                near = true;
                break;
            }
        }
        if (near) {
            sliceClearAt = -1f;
        } else if (sliceClearAt < 0f) {
            sliceClearAt = Time.time;
        }

        while (sliceStabsTold < slice.Stabs) {
            sliceStabsTold++;
            float when = slice.LastStabAt;
            string gap = "first stab";
            if (sliceLastStab > -50f) {
                float g = when - sliceLastStab;
                sliceGapMin = Mathf.Min(sliceGapMin, g);
                gap = g.ToString("0.00") + " s after the last";
                if (g < Musician.StabCooldown - 0.05f) {
                    sliceShortGaps++;
                    run.Problem(PlaytestSeverity.Bug, "Slice stabbed again after " + g.ToString("0.00") + " s, under her " + Musician.StabCooldown.ToString("0.0") + " s cooldown");
                }
            }
            sliceLastStab = when;
            Enemy foe = slice.LastStabbed;
            string who = foe != null ? foe.Title ?? foe.name : "a bot";
            bool hurt = slice.LastStabHealthAfter < slice.LastStabHealthBefore;
            if (!hurt) {
                sliceNoDamage++;
            }
            run.Event("stab", "#" + slice.Stabs + " " + who + (slice.LastStabBig ? " (large: flinch-cut)" : "") + ": " + slice.LastStabDamage.ToString("0.0") + " blade damage, its health "
                + Mathf.RoundToInt(slice.LastStabHealthBefore * 100f) + "% -> " + Mathf.RoundToInt(slice.LastStabHealthAfter * 100f) + "%" + (slice.LastStabHealthAfter <= 0f ? " (destroyed)" : "") + ", " + gap);
            run.Add("stabs");
        }
    }

    static void SliceReport(Musician slice) {
        if (slice == null) {
            return;
        }
        run.Metric("stabs", slice.Stabs);
        run.Metric("flinchCuts", slice.FlinchCuts);
        run.Metric("sits", sliceSits);
        if (sliceGapMin < 900f) {
            run.Metric("stabGapMin", Mathf.Round(sliceGapMin * 100f) / 100f);
        }
        if (sliceNoDamage > 0) {
            run.Problem(PlaytestSeverity.Bug, sliceNoDamage + " of Slice's stabs did no damage");
        }
        run.Event("slice-report", slice.Stabs + " stabs (" + slice.FlinchCuts + " flinch-cuts on large bots), shortest gap "
            + (sliceGapMin < 900f ? sliceGapMin.ToString("0.00") + " s" : "n/a") + ", " + sliceShortGaps + " under the cooldown, sat back down " + sliceSits + " times");
    }

    // ---------------------------------------------------------------- words

    // Menu, shop, help, build tips, pause, cards and game over, read off the screen. The testing wave key is the
    // setup shortcut that lands on each tip. Success is an old-words event of 0 for every screen.
    static IEnumerator WordsMenu() {
        oldHits = 0;
        run.Scene("Words: menu");
        yield return new WaitForSecondsRealtime(0.6f);
        ScanUi("menu");
        int copy = 0;
        foreach (WaveEvent ev in System.Enum.GetValues(typeof(WaveEvent))) {
            copy += ScanLine("event", WaveDirector.EventLine(ev));
        }
        for (int i = 0; i < RunBoons.All.Length; i++) {
            copy += ScanLine("card", RunBoons.All[i].Name);
            copy += ScanLine("card", RunBoons.All[i].About);
        }
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            copy += ScanLine("shop", MetaProgress.Catalog[i].Name);
            copy += ScanLine("shop", MetaProgress.Catalog[i].About);
        }
        copy += ScanLine("fort", DefencePiece.PlaceLesson(FortKind.Wall));
        copy += ScanLine("fort", DefencePiece.PlaceLesson(FortKind.Spike));
        copy += ScanLine("fort", DefencePiece.PlaceLesson(FortKind.Tower));
        copy += ScanLine("thanks", RunAnalytics.Thanks);
        copy += ScanLine("purse", "Waves pay coins. Bosses and the Golden Yo Bot pay more. Every " + MetaProgress.RangeLabel() + " drops one. Pay up.");
        run.Metric("copyHits", copy);
        run.Event("copy-scan", copy + " retired-cast hits in event, card, shop, fort and thanks lines");
        run.Progress(0.05f);
    }

    static IEnumerator WordsRun() {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        run.Scene("Words: wave 1");
        yield return run.Until(() => !string.IsNullOrEmpty(HelpLine()), 20f, "the wave 1 tip");
        yield return new WaitForSecondsRealtime(0.4f);
        NoteHelp("wave-1");
        ScanUi("help");

        run.Scene("Words: pause");
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "the pause menu");
        yield return new WaitForSecondsRealtime(0.4f);
        ScanUi("pause");
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => !PauseMenu.IsOpen, 5f, "play to resume");

        int[] stops = { 2, 4, 5, 8, 12, 16, 25 };
        foreach (int wave in stops) {
            run.Event("setup", "testing wave key to wave " + wave);
            yield return GoWave(wave);
            if (run.Done || WaveDirector.IsLost) {
                break;
            }
            if (wave == 5) {
                yield return FortTips();
            } else {
                float waited = Time.realtimeSinceStartup;
                while (string.IsNullOrEmpty(PlayerGrowth.Banner) && Time.realtimeSinceStartup - waited < 1.2f) {
                    yield return null;
                }
                if (!string.IsNullOrEmpty(PlayerGrowth.Banner)) {
                    run.Event("banner", PlayerGrowth.Banner);
                } else if (wave == 8 || wave == 16 || wave == 25) {
                    run.Problem(PlaytestSeverity.Bug, "the wave " + wave + " tip did not show");
                }
                NoteHelp("wave-" + wave);
                ScanUi("help");
            }
            run.Progress(0.35f);
        }

        if (!run.Done && !WaveDirector.IsLost) {
            run.Event("setup", "testing wave key back to wave 1 for a level-up card");
            yield return GoWave(1);
            run.Scene("Words: cards");
            float start = Time.realtimeSinceStartup;
            float nextSkip = 0f;
            while (!run.Done && !LevelUpScreen.IsOpen && !WaveDirector.IsLost && Time.realtimeSinceStartup - start < 110f) {
                bool building = WaveDirector.IsBuilding;
                Batter(player, building, null, true);
                float now = Time.realtimeSinceStartup;
                if ((building || Enemy.Roster.Count == 0) && now >= nextSkip) {
                    nextSkip = now + 1.5f;
                    PlayInput.Tap(KeyCode.Return);
                }
                run.Progress(0.55f);
                run.Note(Enemy.Roster.Count + " bots, level " + RunBoons.Level);
                yield return null;
            }
            if (LevelUpScreen.IsOpen) {
                yield return new WaitForSecondsRealtime(0.5f);
                ScanUi("cards");
                CheckBoonTexts();
                yield return PickCard();
            } else if (!WaveDirector.IsLost) {
                run.Problem(PlaytestSeverity.Bug, "no level-up card opened");
            }
        }

        if (!run.Done && !WaveDirector.IsLost) {
            run.Event("setup", "testing wave key to wave 2, then hold fire so Slice falls");
            yield return GoWave(2);
            run.Scene("Words: game over");
            PlayInput.Release();
            float die = Time.realtimeSinceStartup;
            while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - die < 140f) {
                Batter(player, true, null, false);
                run.Progress(0.8f);
                run.Note("holding fire, Slice " + (Musician.Instance != null ? Musician.Instance.Health : 0));
                yield return null;
            }
        }
        if (WaveDirector.IsLost) {
            yield return new WaitForSecondsRealtime(0.6f);
            ScanUi("gameover");
            ScanLine("thanks", RunAnalytics.Thanks);
            run.Event("feedback-thanks", RunAnalytics.Thanks);
            if (HawkesAnalyticsPageAskShown(RunAnalytics.AskId) == 1) {
                yield return new WaitForSecondsRealtime(1.2f);
                if (HawkesAnalyticsPageRate(RunAnalytics.AskId, 4) == 1) {
                    run.Event("feedback-card", "4 stars, then: " + RunAnalytics.Thanks);
                }
                yield return new WaitForSecondsRealtime(1.5f);
            }
            run.Metric("oldWords", oldHits);
            run.Event("old-words-total", oldHits.ToString());
            yield return GameOver();
        } else if (!run.Done) {
            run.Problem(PlaytestSeverity.Bug, "Slice did not fall, so the game-over screen was not read");
            run.Metric("oldWords", oldHits);
        }
    }

    static IEnumerator GoWave(int target) {
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        for (int guard = 0; guard < 50 && WaveDirector.Wave != target && !run.Done && !WaveDirector.IsLost; guard++) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else if (PauseMenu.IsOpen) {
                PlayInput.Tap(KeyCode.P);
            } else {
                PlayInput.Tap(WaveDirector.Wave < target ? KeyCode.Equals : KeyCode.Minus);
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
        if (WaveDirector.Wave != target && !run.Done && !WaveDirector.IsLost) {
            run.Problem(PlaytestSeverity.Bug, "the testing wave key did not reach wave " + target + " (at " + WaveDirector.Wave + ")");
        }
        yield return new WaitForSecondsRealtime(0.45f);
    }

    static IEnumerator FortTips() {
        for (int i = 0; i < 3; i++) {
            yield return new WaitForSecondsRealtime(0.35f);
            run.Event("fort-tip", HelpLine());
            ScanUi("tips");
            if (i < 2) {
                PlayInput.Tap(KeyCode.Tab);
            }
        }
    }

    static void NoteHelp(string where) {
        run.Event("help", where + ": " + HelpLine());
    }

    static string HelpLine() {
        ControlsHelp help = Object.FindAnyObjectByType<ControlsHelp>();
        if (help == null) {
            return "";
        }
        Text line = help.GetComponentInChildren<Text>();
        return line != null ? line.text : "";
    }

    static int ScanUi(string screen) {
        int hits = 0;
        var seen = new HashSet<string>();
        Text[] labels = Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
        foreach (Text label in labels) {
            if (label == null || !label.isActiveAndEnabled || string.IsNullOrEmpty(label.text)) {
                continue;
            }
            if (!OldWords.IsMatch(label.text) || !seen.Add(label.text)) {
                continue;
            }
            hits++;
            oldHits++;
            string quote = label.text.Replace("\n", " ");
            if (quote.Length > 140) {
                quote = quote.Substring(0, 140);
            }
            run.Problem(PlaytestSeverity.Bug, screen + " shows retired cast words: \"" + quote + "\"");
        }
        run.Event("old-words", screen + ": " + hits);
        return hits;
    }

    static int ScanLine(string screen, string text) {
        if (string.IsNullOrEmpty(text) || !OldWords.IsMatch(text)) {
            return 0;
        }
        oldHits++;
        run.Problem(PlaytestSeverity.Bug, screen + " copy has retired cast words: \"" + text + "\"");
        return 1;
    }

    // ---------------------------------------------------------------- upgrades

    // Retired cast: the bow, the birds, and the words that went with them. Word boundaries, so "elbow" and "bowled" stay.
    static readonly System.Text.RegularExpressions.Regex OldWords = new System.Text.RegularExpressions.Regex(@"\b(arrows?|bows?|quivers?|fletchings?|archers?|birds?|feathers?|turkeys?|gobblers?|pecks?|flocks?|hens?)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    static int oldHits;
    static readonly string[] Effects = { "wide-swing", "burning", "slowing", "chain-sparks", "spin-swing", "batted-bolt" };
    static readonly HashSet<string> effectsSeen = new HashSet<string>();

    // The shop rows read off the menu: both bat upgrades must be there, and nothing about arrows. Setup: the sandbox
    // owns rank 1 of each (no coins spent, nothing saved), so the run starts with the metal bat.
    static void UpgradesShop() {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        var shown = new List<string>();
        bool bowWords = false;
        if (menu != null) {
            foreach (Text label in menu.GetComponentsInChildren<Text>(true)) {
                for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
                    if (label.text == MetaProgress.Catalog[i].Name && !shown.Contains(label.text)) {
                        shown.Add(label.text);
                    }
                }
                if (OldWords.IsMatch(label.text)) {
                    bowWords = true;
                    run.Problem(PlaytestSeverity.Bug, "shop text has retired cast words: \"" + label.text + "\"");
                }
            }
        }
        run.Event("shop-items", shown.Count + " upgrades on the menu: " + string.Join(", ", shown.ToArray()));
        foreach (UpgradeId id in new[] { UpgradeId.Keen, UpgradeId.Blast }) {
            string name = MetaProgress.Info(id).Name;
            bool there = shown.Contains(name);
            run.Metric(id == UpgradeId.Keen ? "shopCorkedCore" : "shopAluminum", there ? 1 : 0);
            if (!there) {
                run.Problem(PlaytestSeverity.Bug, "the shop does not show " + name);
            }
        }
        run.Metric("shopBowWords", bowWords ? 1 : 0);
        MetaProgress.SandboxRank(UpgradeId.Blast, 1);
        MetaProgress.SandboxRank(UpgradeId.Keen, 1);
        run.Event("setup", "the sandbox owns Aluminum! Not wood! 1 and Corked Core 1 (no coins spent, not saved)");
    }

    static IEnumerator UpgradesRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        effectsSeen.Clear();

        // 1. The metal bat in the player's hands.
        run.Scene("Upgrades: the aluminum bat");
        yield return new WaitForSecondsRealtime(1.5f);
        string look = UpgradeLook.BatLine();
        run.Event("metal-bat", MetaProgress.MetalBat() ? "the player holds an aluminum bat: " + look : "no metal bat: " + look);
        run.Metric("metalBat", MetaProgress.MetalBat() && look.StartsWith("aluminum") ? 1 : 0);
        if (!look.StartsWith("aluminum")) {
            run.Problem(PlaytestSeverity.Bug, "Aluminum! Not wood! is owned but the bat is not metal (" + look + ")");
        }

        // 2. The first level-up's cards, read off the screen.
        run.Scene("Upgrades: level-up cards");
        yield return Fight(player, () => LevelUpScreen.IsOpen, 120f, "a level-up card", null, true, false);
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.8f);
        ReadCards();
        yield return PickCard();
        if (run.Done) {
            yield break;
        }
        CheckBoonTexts();

        // 3. Setup: tougher bots (the testing wave key to wave 14: Plated Yo Bots outlast a hit, so burning and
        // slowing show), and the boons granted.
        run.Event("setup", "testing wave key (=) to wave 14");
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        tapHeld = false;
        for (int guard = 0; guard < 40 && WaveDirector.Wave < 14 && !run.Done; guard++) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else {
                PlayInput.Tap(KeyCode.Equals);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
        lastWave = WaveDirector.Wave;
        run.Event("wave", WaveDirector.Wave.ToString());
        var grant = new[] {
            BoonId.SplitShot, BoonId.SplitShot, BoonId.Firebrand, BoonId.Frost, BoonId.Chain, BoonId.Chain, BoonId.Chain,
            BoonId.Volley, BoonId.Volley, BoonId.Volley, BoonId.Homing, BoonId.Homing, BoonId.Ricochet, BoonId.EagleEye,
            BoonId.Pierce, BoonId.QuickDraw, BoonId.Mark, BoonId.Heavy
        };
        foreach (BoonId id in grant) {
            RunBoons.Take(id);
        }
        run.Event("setup", "boons granted: " + RunBoons.Summary());
        yield return null;
        yield return null;
        run.Event("bat-marks", UpgradeLook.BatLine());

        // 4. Play until every effect has been seen.
        run.Scene("Upgrades: effects");
        yield return Fight(player, () => NoteEffects(), 150f, "every granted upgrade's effect", null, true, false);
        if (run.Done) {
            yield break;
        }

        // 5. Thunder Bat (Live Wire and Grand Slam are maxed): the spin calls lightning down.
        RunBoons.Take(BoonId.StormQuiver);
        run.Event("setup", "Thunder Bat granted (its parents Live Wire and Grand Slam are at max rank)");
        yield return null;
        run.Event("bat-marks", UpgradeLook.BatLine());
        int spins = BatEquipment.Spins;
        int arcs = Combat.Arcs;
        yield return Fight(player, () => BatEquipment.Spins > spins && Combat.Arcs > arcs, 40f, "a Thunder Bat spin", null, true, false);
        if (!run.Done && BatEquipment.Spins > spins) {
            run.Event("thunder-bat", "spin " + BatEquipment.Spins + ": lightning arcs " + arcs + " -> " + Combat.Arcs);
        }
        run.Scene("Wave " + WaveDirector.Wave);
    }

    // The cards on screen: their titles must be boons, and no card text may mention arrows or the bow.
    static void ReadCards() {
        LevelUpScreen screen = Object.FindAnyObjectByType<LevelUpScreen>();
        var titles = new List<string>();
        if (screen != null) {
            foreach (Text label in screen.GetComponentsInChildren<Text>(false)) {
                for (int i = 0; i < RunBoons.All.Length; i++) {
                    if (label.text == RunBoons.All[i].Name && !titles.Contains(label.text)) {
                        titles.Add(label.text);
                    }
                }
                if (OldWords.IsMatch(label.text)) {
                    run.Problem(PlaytestSeverity.Bug, "a level-up card has retired cast words: \"" + label.text + "\"");
                }
            }
        }
        run.Event("cards", titles.Count > 0 ? string.Join(", ", titles.ToArray()) : "(no card titles read)");
        run.Metric("cardsRead", titles.Count);
        if (titles.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "no boon card titles found on the level-up screen");
        }
    }

    // Every boon a card can offer, checked for bow-era words.
    static void CheckBoonTexts() {
        int bad = 0;
        for (int i = 0; i < RunBoons.All.Length; i++) {
            BoonInfo info = RunBoons.All[i];
            if (OldWords.IsMatch(info.Name) || OldWords.IsMatch(info.About)) {
                bad++;
                run.Problem(PlaytestSeverity.Bug, "boon " + info.Name + " has retired cast words: " + info.About);
            }
        }
        run.Metric("boonBowWords", bad);
        run.Event("boon-texts", RunBoons.All.Length + " boons checked, " + bad + " with retired cast words");
    }

    // Logs each effect the first time it shows; true once all have.
    static bool NoteEffects() {
        See("wide-swing", BatEquipment.WideSwings > 0, "Wide Arc: " + BatEquipment.WideSwings + " swings at " + (Tuning.BatArc + RunBoons.ArcBonus).ToString("0") + " degrees (base " + Tuning.BatArc.ToString("0") + "), with the swing trail");
        See("burning", Combat.Burned > 0 && AnyBot(e => e.Burning), "Pine Tar: " + Combat.Burned + " hits set bots burning (orange glow)");
        See("slowing", Combat.Slowed > 0 && AnyBot(e => e.Slowed), "Ice Bat: " + Combat.Slowed + " hits slowed bots (blue tint)");
        See("chain-sparks", Combat.Arcs > 0 && Combat.Shorted > 0, "Live Wire: " + Combat.Arcs + " arcs between bots, " + Combat.Shorted + " shorted out");
        See("spin-swing", BatEquipment.Spins > 0 && BatEquipment.SpinHits > 0, "Grand Slam: spin " + BatEquipment.Spins + " hit " + BatEquipment.SpinHits + " bots all around");
        See("batted-bolt", BatEquipment.BoltsBatted > 0 && BatEquipment.BoltHits > 0, "Long Ball: " + BatEquipment.BoltsBatted + " bolts batted, " + BatEquipment.BoltHits + " landed");
        return effectsSeen.Count >= Effects.Length;
    }

    static void See(string effect, bool seen, string line) {
        if (seen && effectsSeen.Add(effect)) {
            run.Event(effect, line);
        }
    }

    static bool AnyBot(System.Func<Enemy, bool> test) {
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy != null && enemy.Alive && test(enemy)) {
                return true;
            }
        }
        return false;
    }

    static void UpgradesReport() {
        NoteEffects();
        Metrics(Time.realtimeSinceStartup - run.StartTime);
        foreach (string effect in Effects) {
            run.Metric("effect_" + effect.Replace("-", "_"), effectsSeen.Contains(effect) ? 1 : 0);
            if (!effectsSeen.Contains(effect)) {
                run.Problem(PlaytestSeverity.Bug, "the upgrades scenario never saw " + effect);
            }
        }
        run.Metric("wideSwings", BatEquipment.WideSwings);
        run.Metric("burned", Combat.Burned);
        run.Metric("slowed", Combat.Slowed);
        run.Metric("arcs", Combat.Arcs);
        run.Metric("shorted", Combat.Shorted);
        run.Metric("spins", BatEquipment.Spins);
        run.Metric("spinHits", BatEquipment.SpinHits);
        run.Metric("boltsBatted", BatEquipment.BoltsBatted);
        run.Metric("boltHits", BatEquipment.BoltHits);
        run.Metric("lineDriveHits", BatEquipment.LineDriveHits);
        run.Metric("sweetSpots", Combat.SweetSpots);
        run.Metric("calledBolts", Combat.CalledBolts);
        run.Metric("metalBlasts", BatEquipment.MetalBlasts);
        run.Event("upgrades-report", effectsSeen.Count + " of " + Effects.Length + " effects seen; line drives " + BatEquipment.LineDriveHits
            + ", sweet spots " + Combat.SweetSpots + ", Called Shot bolts " + Combat.CalledBolts + ", metal-bat blasts " + BatEquipment.MetalBlasts);
    }

    // ---------------------------------------------------------------- bots

    // Wave 1 is Yo Bots walking at the musician; the bot fights them and picks up their bolts. Then the testing wave
    // key takes it to wave 30, where the first Spider Bot arrives as the Alpha with the boss bar.
    static IEnumerator BotsRun(PlaytestOptions options) {
        yield return run.Until(() => YoBotsAlive() > 0, 20f, "wave 1's Yo Bots");
        if (run.Done) {
            yield break;
        }
        // Hold fire a few seconds so the footage shows them walking in.
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        float before = YoBotDistance();
        int count = YoBotsAlive();
        run.Scene("Wave 1: Yo Bots");
        yield return new WaitForSecondsRealtime(3.5f);
        float after = YoBotDistance();
        run.Event("yo-bots", count + " Yo Bots, average distance to the musician " + before.ToString("0.0") + " m -> " + after.ToString("0.0") + " m");
        run.Metric("yoBotsWave1", count);
        run.Metric("yoBotClosing", Mathf.Round((before - after) * 10f) / 10f);
        if (!(after < before - 0.3f)) {
            run.Problem(PlaytestSeverity.Bug, "wave 1's Yo Bots are not walking toward the musician (" + before.ToString("0.0") + " m -> " + after.ToString("0.0") + " m)");
        }
        LogEnemyList("wave 1");

        int levelBefore = RunBoons.Level;
        int xpBefore = RunBoons.Feathers;
        yield return Play(options, 30f, false);
        if (run.Done) {
            yield break;
        }
        int bolts = Pickups.BoltsCollected;
        run.Metric("boltsCollected", bolts);
        run.Event("bolts", bolts + " bolts picked up from " + RunLog.RunKills + " destroyed bots: level " + levelBefore + " (" + xpBefore + " XP) -> level " + RunBoons.Level + " (" + RunBoons.Feathers + " XP)");
        if (bolts == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bolts picked up after " + RunLog.RunKills + " kills");
        }

        // Setup shortcut: the testing wave key, one press per wave, as a tester would.
        run.Event("setup", "testing wave key (=) to wave 30");
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        for (int guard = 0; guard < 60 && WaveDirector.Wave < 30 && !run.Done; guard++) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else {
                PlayInput.Tap(KeyCode.Equals);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
        if (WaveDirector.Wave != 30) {
            run.Problem(PlaytestSeverity.Bug, "the wave key did not reach wave 30 (wave " + WaveDirector.Wave + ")");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Scene("Wave 30: Spider Bot");
        run.Event("wave", "30");
        lastWave = 30;
        yield return run.Until(() => SpiderBoss() != null, 60f, "the Alpha Spider Bot on wave 30");
        if (run.Done) {
            yield break;
        }
        Enemy boss = SpiderBoss();
        yield return new WaitForSecondsRealtime(1f);
        if (boss != null) {
            float tall = HeightOf(boss);
            PlayerGrowth growth = PlayerGrowth.Instance;
            float player = growth != null ? growth.BodyHeight : 0f;
            bool bar = BossBarShows(boss.Title);
            run.Event("spider-bot", boss.Title + ": " + tall.ToString("0.0") + " m tall (the player is " + player.ToString("0.0") + " m), boss bar " + (bar ? "up" : "missing"));
            run.Metric("spiderBotHeight", Mathf.Round(tall * 10f) / 10f);
            run.Metric("bossBar", bar ? 1 : 0);
            if (!bar) {
                run.Problem(PlaytestSeverity.Bug, "no boss bar for " + boss.Title);
            }
        }
        LogEnemyList("wave 30");
        yield return Play(options, 30f, false);
    }

    static int YoBotsAlive() {
        int count = 0;
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy != null && enemy.Alive && enemy.Kind == BotKind.YoBot) {
                count++;
            }
        }
        return count;
    }

    static float YoBotDistance() {
        if (Musician.Instance == null) {
            return 0f;
        }
        Vector3 at = Musician.Instance.transform.position;
        float sum = 0f;
        int count = 0;
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy == null || !enemy.Alive || enemy.Kind != BotKind.YoBot) {
                continue;
            }
            Vector3 flat = enemy.transform.position - at;
            flat.y = 0f;
            sum += flat.magnitude;
            count++;
        }
        return count > 0 ? sum / count : 0f;
    }

    static Enemy SpiderBoss() {
        foreach (Enemy enemy in Enemy.Roster) {
            if (enemy != null && enemy.Alive && enemy.Boss && BotRoster.IsSpider(enemy.Kind)) {
                return enemy;
            }
        }
        return null;
    }

    static float HeightOf(Enemy enemy) {
        float top = enemy.transform.position.y;
        foreach (Renderer skin in enemy.GetComponentsInChildren<Renderer>()) {
            if (skin is MeshRenderer && skin.transform.parent != null && skin.transform.parent.name == "Bot") {
                top = Mathf.Max(top, skin.bounds.max.y);
            }
        }
        return top - enemy.transform.position.y;
    }

    // The boss bar's name label, as drawn at the top of the screen.
    static bool BossBarShows(string title) {
        RunHud hud = Object.FindAnyObjectByType<RunHud>();
        if (hud == null) {
            return false;
        }
        foreach (Text label in hud.GetComponentsInChildren<Text>(false)) {
            if (label.text == title && label.isActiveAndEnabled) {
                return true;
            }
        }
        return false;
    }

    // The names in the list on the right, read from the screen.
    static void LogEnemyList(string when) {
        WaveRoster roster = Object.FindAnyObjectByType<WaveRoster>();
        var names = new List<string>();
        if (roster != null) {
            foreach (Text label in roster.GetComponentsInChildren<Text>(false)) {
                if (!label.isActiveAndEnabled || label.text.EndsWith(" left") || label.text == "This wave" || string.IsNullOrEmpty(label.text)) {
                    continue;
                }
                names.Add(label.text);
            }
        }
        run.Event("enemy-list", when + ": " + (names.Count > 0 ? string.Join(", ", names) : "(empty)"));
        if (names.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "the enemy list on the right is empty on " + when);
        }
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
                run.Note(building ? "build phase" : Enemy.Roster.Count + " bots, musician " + (Musician.Instance != null ? Musician.Instance.Health : 0));
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
        run.Metric("botsAlive", Enemy.Roster.Count);
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
