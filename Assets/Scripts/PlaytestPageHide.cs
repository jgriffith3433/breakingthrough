using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "page-hide" scenario (phone, 390x844, touch): the page is hidden and shown mid-wave. Play starts by a tap on Play; the
// hide goes through the same PageLifecycle.Hide the page's visibilitychange listener calls (the browser event itself can't
// be raised from inside the game). Then: Pause is up with Resume, time is stopped, the wave, Slice's health and the bolts
// are unchanged, the save was written once (save.writes), a second show changes nothing, and a tap on Resume plays on.
// Events: page-hidden, page-shown, save-writes, resume-tap. Metrics save.writes, pageHideKeptState.
public static class PlaytestPageHide {
    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    public static IEnumerator Run(PlaytestRun run) {
        UiFit.ForceTouch(true);
        CampaignSave.EnterSandbox();
        yield return new WaitForSecondsRealtime(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "page-hide: no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.PlayButton.gameObject);
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade");
        yield return new WaitForSecondsRealtime(8f);   // mid-wave: bots are walking in

        int wave = WaveDirector.Wave;
        int health = Musician.Instance != null ? Musician.Instance.Health : -1;
        int bolts = Pickups.BoltsCollected;
        run.Event("page-hidden", "wave " + wave + ", Slice " + health + ", bolts " + bolts + ": the page goes hidden");
        PageLifecycle.Hide("playtest");
        yield return new WaitForSecondsRealtime(1f);

        bool paused = PauseMenu.IsOpen && Time.timeScale < 0.01f;
        bool resume = PauseMenu.Instance != null && PauseMenu.Instance.ResumeButton != null && PauseMenu.Instance.ResumeButton.isActiveAndEnabled;
        run.Event("save-writes", "save.writes=" + PageLifecycle.LastHideWrites);
        run.Metric("save.writes", PageLifecycle.LastHideWrites);
        if (!paused || !resume) {
            run.Problem(PlaytestSeverity.Bug, "page-hide: after the page was hidden the game was not paused with Resume up (paused " + paused + ", Resume " + resume + ")");
        }
        if (PageLifecycle.LastHideWrites != 2) {
            run.Problem(PlaytestSeverity.Bug, "page-hide: the hide wrote " + PageLifecycle.LastHideWrites + " saves (want one case-file write and one shop write)");
        }

        // Back again: the state is the same, the menu is still up, and nothing was written a second time.
        int writesBefore = CampaignSave.Writes + MetaProgress.Writes;
        PageLifecycle.Show("playtest");
        yield return new WaitForSecondsRealtime(0.5f);
        bool same = WaveDirector.Wave == wave && (Musician.Instance == null || Musician.Instance.Health == health) && Pickups.BoltsCollected == bolts;
        run.Event("page-shown", "pause menu " + (PauseMenu.IsOpen ? "up" : "gone") + ", wave " + WaveDirector.Wave + ", bolts " + Pickups.BoltsCollected + ", same " + same);
        run.Metric("pageHideKeptState", same && PauseMenu.IsOpen ? 1 : 0);
        if (!same || !PauseMenu.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "page-hide: coming back did not show the same paused moment (same " + same + ", pause up " + PauseMenu.IsOpen + ")");
        }
        if (CampaignSave.Writes + MetaProgress.Writes != writesBefore) {
            run.Problem(PlaytestSeverity.Bug, "page-hide: showing the page wrote the save again");
        }

        if (resume) {
            Tap(PauseMenu.Instance.ResumeButton.gameObject);
            yield return new WaitForSecondsRealtime(0.8f);
            run.Event("resume-tap", "paused " + PauseMenu.IsOpen + ", timeScale " + Time.timeScale);
            if (PauseMenu.IsOpen || Time.timeScale < 0.5f) {
                run.Problem(PlaytestSeverity.Bug, "page-hide: Resume did not return to play");
            }
        }
        yield return new WaitForSecondsRealtime(2f);
    }
}
