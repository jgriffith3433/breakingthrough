using System.Collections;
using Hawkes.Playtest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Runs the Hawkes playtest "smoke" scenario the way the browser runner does, through HawkesPlaytest.StartScenario.
// The game registers its bot at startup (Assets/Scripts/Playtest.cs); this test only boots the menu and watches.
public class PlaytestSmokeTest {
    [UnityTest, Timeout(240000)]
    public IEnumerator SmokeScenarioFinishes() {
        // Errors become problems in the report; the test judges the outcome, not each log line.
        LogAssert.ignoreFailingMessages = true;
        SceneManager.LoadScene("MainMenu");
        float until = Time.realtimeSinceStartup + 60f;
        while (!(HawkesPlaytest.Registered && HawkesPlaytest.IsReady) && Time.realtimeSinceStartup < until) {
            yield return null;
        }
        Assert.IsTrue(HawkesPlaytest.Registered, "HawkesPlaytest.Register was not called at startup");
        Assert.AreEqual("theplacedepartment", HawkesPlaytest.Product);
        Assert.IsTrue(HawkesPlaytest.IsReady, "the game never reported ready");

        PlaytestRun run = HawkesPlaytest.StartScenario("smoke", new PlaytestOptions { seed = 1, speed = 1f });
        until = Time.realtimeSinceStartup + 180f;
        while (!run.Done && Time.realtimeSinceStartup < until) {
            yield return null;
        }
        string report = HawkesPlaytest.ReportJson(run);
        Debug.Log("[Playtest] smoke report: " + report);
        Assert.IsTrue(run.Done, "the smoke scenario did not finish in 180 s");
        Assert.That(run.Outcome == PlaytestOutcome.Done || run.Outcome == PlaytestOutcome.Lost,
            "smoke ended " + run.Outcome + ": " + report);
        Time.timeScale = 1f;
    }
}
