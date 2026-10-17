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
        new PlaytestScenario("touch", "Touch only, for a phone-sized screen (mobile, or 844x390): the menu fits, tap Play, move and fire with the on-screen stick and buttons, tap a level-up card, play to wave 2", 330, "menus"),
        new PlaytestScenario("site", "The website around the game: reads the landing page and every page linked from it (outside Game/), checks each footer's \"Powered by HAWKES GEN\" link to hawkesgen.com and the landing page's link to hawkesbiomedical.com, shows each page's footer over the game for a few seconds, then plays 20 s to show the game screen has no footer", 90, "menus"),
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
        PlayInput.Release();
        MetaProgress.EnterSandbox(true);
        Random.InitState(options.seed);
        Application.logMessageReceived += OnLog;
        try {
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
            run.Metric("sitePages", pages.Length);
            run.Metric("sitePagesPowered", powered);
            run.Progress(0.3f);

            // Show each page's footer (the landing page first), a few seconds each, then back to the game.
            int shown = 0;
            foreach (SitePage page in pages) {
                if (page.status != 200 || shown >= 6) {
                    continue;
                }
                run.Scene("Site " + page.path);
                HawkesSiteShow(page.path);
                run.Event("site-shown", page.path + " footer" + (page.powered ? ": Powered by HAWKES GEN" : ": no Powered by mark") + (page.parent ? ", A Hawkes Biomedical company" : ""));
                yield return new WaitForSecondsRealtime(4f);
                HawkesSiteHide();
                shown++;
                run.Progress(0.3f + 0.4f * shown / Mathf.Max(1, Mathf.Min(6, pages.Length)));
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
    // like a finger's; the stick, Fire, Lock and Next wave get the bot's fingers through TouchControls, the same
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

            // The stick: a lap around the start so the stick is seen working, then back to the piano when drifted.
            Vector2 dir = elapsed < 5f ? new Vector2(Mathf.Sin(elapsed * 1.3f), Mathf.Cos(elapsed * 1.3f)) * 0.8f : SteerDir(player);
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

            // Fire held while there are birds; Lock toggled on with them, off without.
            bool shoot = !building && birds && !LevelUpScreen.HoldFire;
            if (shoot && !fireDown && TouchControls.Spot("Fire", out Vector2 fireAt)) {
                TouchControls.BotFinger(1, fireAt, true);
                fireDown = true;
            } else if (!shoot && fireDown) {
                TouchControls.BotFinger(1, Vector2.zero, false);
                fireDown = false;
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
