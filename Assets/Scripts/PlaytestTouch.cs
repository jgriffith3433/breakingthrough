using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "campaign-touch" scenario: the campaign on a phone, with fingers only. Play it in a mobile viewport (the `mobile` option, or
// 844x390). The menus are tapped like the other scenarios do; everything in the world is a finger on TouchControls: the stick
// (TouchControls.BotFinger), then the Interact button. In Chapter 1's Sorting Yard the bot walks to Slice on the stick and taps
// Interact to talk, walks to the Mailbox Flag and taps Interact to pick it up; then (named setup shortcut: the sandbox case file
// has Chapter 1 filed, the level's way out is taken) in the hub it walks to Chapter 2's door and taps Interact to open it. Events:
// touch-layout, interact-hidden, interact-shown, interact-tap, talk-done, pickup-done, hub-touch, journal-touch, door-opened.
public static class PlaytestTouch {
    static PlaytestRun run;
    const int StickId = 1;
    const int PadId = 2;
    static bool stickDown;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    // The stick, as a thumb: down on the stick's middle, then dragged out to `push` (-1..1 on each axis).
    static void Drive(Vector2 push) {
        if (push.sqrMagnitude < 0.0004f) {
            Rest();
            return;
        }
        if (!TouchControls.Spot("Stick", out Vector2 centre)) {
            return;
        }
        Vector2 at = stickDown ? centre + push.normalized * Mathf.Min(1f, push.magnitude) * TouchControls.StickReach * 0.9f : centre;
        TouchControls.BotFinger(StickId, at, true);
        stickDown = true;
    }

    static void Rest() {
        if (stickDown) {
            TouchControls.BotFinger(StickId, Vector2.zero, false);
            stickDown = false;
        }
    }

    // Toward a point in the level, where the stick is relative to the camera.
    static void SteerLevel(Vector3 target) {
        LevelScene level = LevelScene.Instance;
        Vector3 to = Flat(target - level.Player.position);
        if (to.magnitude < 0.05f) {
            Rest();
            return;
        }
        Vector3 fwd = Flat(level.Cam.transform.forward).normalized;
        Vector3 right = Flat(level.Cam.transform.right).normalized;
        Vector3 d = to.normalized;
        Drive(new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, fwd)) * 1.4f);
    }

    // In the hub the stick is the room's own axes.
    static void SteerHub(ChapterHub hub, Vector3 target) {
        Vector3 to = Flat(target - hub.Player.position);
        if (to.magnitude < 0.05f) {
            Rest();
            return;
        }
        Drive(new Vector2(to.x, to.z).normalized);
    }

    static IEnumerator WalkLevel(Vector3 target, float near, float seconds) {
        float start = Time.realtimeSinceStartup;
        while (!run.Done && Time.realtimeSinceStartup - start < seconds && Flat(target - LevelScene.Instance.Player.position).magnitude > near) {
            SteerLevel(target);
            yield return null;
        }
        Rest();
        yield return null;
    }

    static IEnumerator WalkHub(ChapterHub hub, Vector3 target, float near, float seconds) {
        float start = Time.realtimeSinceStartup;
        while (!run.Done && Time.realtimeSinceStartup - start < seconds && Flat(target - hub.Player.position).magnitude > near) {
            SteerHub(hub, target);
            yield return null;
        }
        Rest();
        yield return null;
    }

    // One finger down on a touch button for two frames, then up.
    static IEnumerator TapPad(string name) {
        if (!TouchControls.Spot(name, out Vector2 at)) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: no " + name + " button on screen to tap");
            yield break;
        }
        TouchControls.BotFinger(PadId, at, true);
        yield return null;
        yield return null;
        TouchControls.BotFinger(PadId, Vector2.zero, false);
        yield return null;
    }

    static bool PadShown(string name) {
        return TouchControls.Spot(name, out Vector2 _);
    }

    // Every touch button on screen, as css pixels: none under 44, none off the screen.
    static void CheckLayout(string where) {
        var rects = new List<RectTransform>();
        TouchControls.Shown(rects);
        float smallest = 9999f;
        int off = 0;
        var corners = new Vector3[4];
        foreach (RectTransform r in rects) {
            r.GetWorldCorners(corners);
            float w = (corners[2].x - corners[0].x) / UiFit.PixelRatio;
            float h = (corners[2].y - corners[0].y) / UiFit.PixelRatio;
            smallest = Mathf.Min(smallest, Mathf.Min(w, h));
            if (corners[0].x < -1f || corners[0].y < -1f || corners[2].x > Screen.width + 1f || corners[2].y > Screen.height + 1f) {
                off++;
            }
        }
        run.Event("touch-layout", where + ": " + rects.Count + " touch controls, smallest " + Mathf.Round(smallest) + " px, " + off + " off screen, layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height);
        run.Metric("touchControlMinPx_" + where, Mathf.Round(smallest));
        if (smallest < 44f) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: a touch control is only " + Mathf.Round(smallest) + " px across in " + where + " (want 44 or more)");
        }
        if (off > 0) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: " + off + " touch controls are partly off screen in " + where);
        }
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        stickDown = false;
        keyWords = 0;
        UiFit.ForceTouch(true);
        LevelBarks.Reset();
        CampaignSave.EnterSandbox();
        run.Log("layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height);
        yield return new WaitForSecondsRealtime(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "Chapter 1's card");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(3.5f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene");
        if (run.Done) {
            yield break;
        }
        LevelScene level = LevelScene.Instance;
        run.Scene(SceneManager.GetActiveScene().name);
        run.Event("level-scene", SceneManager.GetActiveScene().name + " with touch (" + UiFit.Mode + ")");
        yield return run.Until(() => PadShown("Stick") && PadShown("Swing"), 15f, "the touch controls");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);
        CheckLayout("level");
        bool hidden = !PadShown("Interact");
        run.Event("interact-hidden", "nothing in reach at the start: the Interact button is " + (hidden ? "hidden" : "SHOWN"));
        if (!hidden) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: the Interact button shows with nothing to use");
        }

        // 1. Talk to Slice: the stick to the Ring, the Interact button appears, one tap.
        yield return WalkLevel(LevelYard.SlicePos + new Vector3(0f, 0f, -1.5f), 0.5f, 60f);   // Interact reaches 2.7 m: stop within 2 m of her, not 4
        yield return run.Until(() => level.Yard.Prompt.Contains("talk to Slice") && PadShown("Interact"), 6f, "the Interact button beside Slice");
        if (run.Done) {
            yield break;
        }
        string prompt = level.Yard.Prompt;
        run.Event("interact-shown", "prompt \"" + prompt + "\", the button is on screen");
        run.Metric("promptSaysTap", prompt.StartsWith("Tap:") && !prompt.Contains("E:") ? 1 : 0);
        if (!prompt.StartsWith("Tap:")) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: on touch the prompt reads \"" + prompt + "\" (want Tap: ...)");
        }
        CheckLayout("level-interact");
        run.Event("interact-tap", "finger on Interact beside Slice");
        yield return TapPad("Interact");
        yield return run.Until(() => level.Yard.Talked, 80f, "the talk with Slice to end");
        if (run.Done) {
            yield break;
        }
        run.Event("talk-done", "Slice joined: " + level.Yard.SliceJoined + " (touch only)");
        run.Metric("touchTalk", level.Yard.Talked ? 1 : 0);

        // 2. Pick up the Mailbox Flag on the west trail.
        yield return WalkLevel(LevelYard.FlagPos + new Vector3(1.0f, 0f, -0.8f), 0.5f, 80f);   // within 1.8 m of the flag (reach 2.7 m)
        yield return run.Until(() => level.Yard.Prompt.Contains("pick up") && PadShown("Interact"), 6f, "the Interact button at the flag");
        if (run.Done) {
            yield break;
        }
        run.Event("interact-shown", "prompt \"" + level.Yard.Prompt + "\"");
        run.Event("interact-tap", "finger on Interact at the Mailbox Flag");
        yield return TapPad("Interact");
        yield return run.Until(() => level.Yard.FlagTaken, 5f, "the flag to be lifted");
        if (run.Done) {
            yield break;
        }
        run.Event("pickup-done", "the Mailbox Flag is in hand (touch only)");
        run.Metric("touchPickup", level.Yard.FlagTaken ? 1 : 0);

        KeyWords("level");

        // 3. The fight, fingers only: Lock is tapped once the crew is close, the stick closes in, Swing is tapped in reach.
        yield return run.Until(() => level.Yard.CrewWoken, 25f, "the Clearing Crew to wake");
        if (run.Done) {
            yield break;
        }
        CheckLayout("level-fight");
        float fightStart = Time.realtimeSinceStartup;
        float nextSwing = 0f;
        int swings = 0;
        bool lockTapped = false;
        while (!run.Done && !level.Yard.CrewCleared && Time.realtimeSinceStartup - fightStart < 240f) {
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
            if (nearest == null) {
                Rest();
                yield return null;
                continue;
            }
            if (!lockTapped && nd < 14f) {
                lockTapped = true;
                yield return TapPad("Lock");
                run.Event("touch-lock", "finger on Lock with the crew " + Mathf.RoundToInt(nd) + " m off");
            }
            if (nd > 2.1f) {
                SteerLevel(nearest.transform.position);
            } else {
                Rest();
            }
            if (nd < 2.6f && Time.realtimeSinceStartup >= nextSwing) {
                nextSwing = Time.realtimeSinceStartup + 0.5f;
                Rest();
                yield return TapPad("Swing");
                swings++;
                continue;
            }
            yield return null;
        }
        Rest();
        run.Metric("touchSwings", swings);
        run.Metric("touchFightSeconds", Mathf.Round(Time.realtimeSinceStartup - fightStart));
        run.Event("touch-fight", "the Clearing Crew " + (level.Yard.CrewCleared ? "cleared" : "NOT cleared") + " with " + swings + " Swing taps, " + level.Defeats + " defeats");
        if (!level.Yard.CrewCleared) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: the Clearing Crew was not cleared by touch in 240 s (" + level.Yard.CrewAlive + " left)");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        KeyWords("level-fight");

        // 4. The way out on the stick, the exit card, one tap on Interact back to the hub: Chapter 1 completed by touch.
        yield return new WaitForSecondsRealtime(3f);
        yield return WalkLevel(LevelYard.ExitPos + new Vector3(0f, 0f, -5f), 2f, 60f);
        yield return WalkLevel(LevelYard.ExitPos, 2.2f, 20f);
        yield return run.Until(() => level.Yard.Stage == LevelYard.Beat.Done, 12f, "the exit");
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => PadShown("Interact"), 6f, "the Interact button on the exit card");
        if (run.Done) {
            yield break;
        }
        run.Scene("ch1-exit-card");
        run.Event("exit-card", level.Yard.Prompt.Replace('\n', ' '));
        KeyWords("exit-card");
        run.Event("interact-tap", "finger on Interact on the exit card");
        yield return TapPad("Interact");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown && PadShown("Stick"), 45f, "the hub with touch controls");
        if (run.Done) {
            yield break;
        }
        CampaignSave.Slot file = CampaignSave.Get(0);
        run.Event("chapter-complete-touch", "Chapter 1 completed by touch only: " + file.cleared + " chapter filed, next is chapter " + (file.chapter + 1));
        run.Metric("touchChapterComplete", file.cleared >= 1 ? 1 : 0);
        if (file.cleared < 1) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: back in the hub but Chapter 1 is not filed");
        }
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub");
        yield return new WaitForSecondsRealtime(2f);
        CheckLayout("hub");
        bool hubPads = PadShown("Journal") && PadShown("Menu") && !PadShown("Swing");
        run.Event("hub-touch", "stick, Journal and Menu on screen, no bat buttons: " + hubPads + "; prompt \"" + hub.PromptText + "\"");
        if (!hubPads) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: the hub should show the stick, Journal and Menu and no Swing button");
        }
        if (hub.PromptText.Contains("WASD") || hub.PromptText.Contains("J: journal")) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: the hub still tells a phone to press keys: \"" + hub.PromptText + "\"");
        }

        // The journal by its button (closed by setup: the panel's own close is a UI tap the other scenarios cover).
        yield return TapPad("Journal");
        yield return run.Until(() => JournalPanel.IsOpen, 5f, "the journal after tapping Journal");
        if (run.Done) {
            yield break;
        }
        run.Event("journal-touch", "the journal opened from its touch button");
        KeyWords("journal");
        run.Metric("touchJournal", JournalPanel.IsOpen ? 1 : 0);
        yield return new WaitForSecondsRealtime(1f);
        JournalPanel.Close();
        yield return new WaitForSecondsRealtime(0.5f);

        // 4. Chapter 2's door: walk up to it, the Interact button offers it, one tap opens it.
        Vector3 door = ChapterHub.DoorPosition(1);
        yield return WalkHub(hub, new Vector3(door.x, 0f, door.z - 2.1f), 0.35f, 40f);
        yield return run.Until(() => hub.DoorUsable && PadShown("Interact"), 6f, "the Interact button at Chapter 2's door");
        if (run.Done) {
            yield break;
        }
        run.Event("interact-shown", "prompt \"" + hub.PromptText + "\"");
        KeyWords("hub-door");
        if (!hub.PromptText.Contains("Tap: open the door") || hub.EnteredDoor >= 0) {
            run.Problem(PlaytestSeverity.Bug, "campaign-touch: at the door the prompt reads \"" + hub.PromptText + "\" (entered " + hub.EnteredDoor + ")");
        }
        run.Event("interact-tap", "finger on Interact at door 2");
        yield return TapPad("Interact");
        yield return run.Until(() => hub.EnteredDoor == 1 && ChapterScene.Instance.CardUp, 10f, "Chapter 2's card after tapping Interact");
        if (run.Done) {
            yield break;
        }
        run.Event("door-opened", "Chapter 2's door opened by touch: the card reads " + ChapterScene.Instance.Title);
        run.Metric("touchDoor", 1);
        yield return new WaitForSecondsRealtime(2f);
        KeyWords("chapter-card");
        run.Metric("touchKeyWords", keyWords);
        run.Event("touch-words-total", keyWords + " lines on screen told a phone to press a key");
    }

    static int keyWords;

    // Keyboard words a phone can't press, in any text on screen.
    // Whole words only, so "NOTICE: " or "CHIME: " are not "E: ".
    static readonly System.Text.RegularExpressions.Regex KeyPhrases = new System.Text.RegularExpressions.Regex(
        @"\bE: |\b[Pp]ress [A-Z]\b|\b[Hh]old (E|Space)\b|\bWASD\b|\bAny key\b|\bEnter: |\bEsc\b|\(Enter\)|\bShift locks\b|\bright click\b|\bClick to\b");

    // Every active text on screen is read; one that names a key is a bug on touch.
    static void KeyWords(string where) {
        int found = 0;
        foreach (UnityEngine.UI.Text t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None)) {
            if (t == null || !t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || t.color.a < 0.05f) {
                continue;
            }
            if (KeyPhrases.IsMatch(t.text)) {
                found++;
                run.Problem(PlaytestSeverity.Bug, "campaign-touch: on touch the " + where + " screen says \"" + t.text.Replace('\n', ' ') + "\" (" + t.name + ")");
            }
        }
        keyWords += found;
        run.Event("touch-words", where + ": " + found + " texts name a key");
    }
}
